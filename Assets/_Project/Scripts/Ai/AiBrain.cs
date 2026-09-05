using System;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Vehicles;

namespace SP.Ai
{
    // Postura de combate de una unidad. Libre es el comportamiento
    // historico y por defecto: las otras dos NO son un camino de codigo
    // alternativo, son modificadores que se aplican sobre las mismas tres
    // decisiones que ya existian (rango de vision, avanzar hacia el
    // objetivo, apretar el gatillo). Con Libre esos modificadores son
    // exactamente neutros: multiplicador 1f y guardas que devuelven true
    // antes de mirar cualquier otra cosa.
    public enum CombatStance
    {
        Libre,       // sin restricciones: identico al comportamiento de siempre
        Defensiva,   // ve menos lejos y no se despega de su puesto
        AltoElFuego  // detecta y encara, pero no dispara ni persigue
    }

    // Máquina de estados de un soldado no poseído. Sensa, persigue, ataca,
    // reacciona a que le disparen y a que le disparen a un aliado cercano,
    // y ejecuta órdenes explícitas del jugador (T). Cuando el soldado pasa
    // a ser poseído por el jugador, se suspende (IsPossessedByPlayer).
    public class AiBrain : MonoBehaviour
    {
        [SerializeField] float visionRange = 10f;
        [SerializeField] float attackRange = 6f;
        [SerializeField] float alertRadius = 30f;
        [SerializeField] float arriveThreshold = 0.6f;

        // Tolerancia de apuntado antes de disparar en Attack (ver el gate
        // en el case Attack de Tick()): mas floja que los 4 grados del
        // cañon del tanque a proposito -- un soldado de infanteria
        // apuntando a ojo no es tan mecanicamente preciso, y una
        // tolerancia muy ajustada haria el combate humano se sienta lento.
        [SerializeField] float aimToleranceDeg = 15f;

        // Modificadores de la postura Defensiva. En Libre no se leen: el
        // multiplicador efectivo es la constante 1f y la correa ni se
        // consulta (StanceAllowsPursuit sale por el return de arriba).
        [SerializeField] float defensiveVisionMultiplier = 0.6f;
        [SerializeField] float defensiveLeashRadius = 8f;

        // Item 224: cada cuantos ticks se rehace la consulta de sensado.
        // Con 1 el codigo queda literalmente en el comportamiento previo
        // (una consulta por tick); con 2 o 3 se reparte la carga.
        [SerializeField] int senseIntervalTicks = 3;

        Soldier self;
        Soldier target;
        Vector3 orderDestination;
        bool hasOrder;
        bool orderIsAttack;
        bool bootstrapped;
        Vehicle mountTarget;
        IDisposable damageSub;

        // Item pedido: "que le pueda decir a mis aliados que me sigan".
        // A quien sigo -- normalmente el soldado poseido por el jugador.
        // Separado de orderDestination porque ese es un punto FIJO
        // (MovingToOrder termina al llegar); Follow no termina nunca solo,
        // persigue una posicion que se mueve cada Tick hasta que lo
        // cancelen o lo interrumpa el combate.
        Soldier followTarget;

        // Attack-move: una orden de movimiento dada mientras el soldado ya
        // esta trabado en combate (Chase/Attack) NO corta el combate --
        // redirige el CAMINAR hacia este punto pero el apuntado y el
        // disparo (case Attack de Tick()) siguen atados al target de
        // siempre. Separado de orderDestination porque ese SI corta el
        // combate (ver IssueMoveOrder): dos campos, dos significados.
        Vector3? attackMoveDestination;

        // Distancia a la que se detiene detras del lider: 0 lo pegaria
        // encima del jugador (empujones, camara tapada); demasiado lejos
        // y "seguir" se ve identico a quedarse atras sin hacer nada.
        [SerializeField] float followStopDistance = 2.5f;

        // La postura NO se serializa a proposito, al reves que patrolRoute:
        // no se asigna al construir la escena sino en runtime (el jugador
        // la cambia durante la partida), y un valor guardado en la escena o
        // el prefab podria arrancar a un soldado en algo que no sea Libre
        // sin que nadie lo haya pedido. El default del campo es el default
        // del enum, asi que cualquier soldado nace en Libre.
        CombatStance stance = CombatStance.Libre;

        // "Puesto" de la postura Defensiva: el punto del que no se aleja.
        // Se fija al nacer y se re-ancla cuando le ponen Defensiva, porque
        // el origen que importa es donde estaba parado cuando le dieron la
        // orden, no donde spawneo diez minutos antes.
        Vector3 homePosition;

        // --- Cache del sensado repartido en el tiempo (item 224) ---
        Soldier sensedTarget;   // ultimo resultado de la consulta
        int tickCount;          // ticks simulados por ESTE cerebro
        int lastSenseTick;      // tick en que se calculo sensedTarget
        bool forceSense = true; // primer tick: sensar si o si

        // [SerializeField] NO es decorativo aca: la ruta se asigna por
        // codigo al construir la escena (HeadlessTestRunner.SetPatrolRoute)
        // y un campo privado sin serializar NO sobrevive el domain reload
        // al entrar a Play. Sin esto los 4 enemigos de patrulla quedaban
        // clavados de pie para siempre, en estado Patrol, mientras el
        // LineRenderer de sus rondas -- que si se serializa -- seguia
        // dibujando los circuitos naranjas en el mapa. Se leia como una IA
        // rota, y no daba ningun error: el case Patrol hace un no-op
        // silencioso cuando patrolRoute es null.
        [SerializeField] Vector3[] patrolRoute;
        int patrolIndex;

        // Antes IssueMoveOrder reemplazaba el destino anterior, asi que no
        // se podian planificar rutas: cada orden borraba la anterior.
        readonly System.Collections.Generic.Queue<Vector3> orderQueue = new System.Collections.Generic.Queue<Vector3>();
        public int QueuedOrderCount => orderQueue.Count;
        public System.Collections.Generic.IEnumerable<Vector3> QueuedDestinations => orderQueue;

        // --- Rodeo de obstaculos (NavService) ---
        // Ruta calculada para esquivar el "Muro" y compañia. VACIA es el
        // caso normal y significa "linea recta": el A* solo corre cuando
        // la linea recta esta cortada, y solo al RECIBIR la orden, no por
        // frame. Un mapa despejado paga exactamente lo mismo que antes.
        readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>();
        int pathIndex;

        public int RemainingPathPoints => Mathf.Max(0, path.Count - pathIndex);
        public System.Collections.Generic.IReadOnlyList<Vector3> CurrentPath => path;

        // Llegada a un waypoint INTERMEDIO. Mas flojo que arriveThreshold a
        // proposito: exigirle 0.6 a cada esquina hace que el soldado frene
        // y corrija en cada una en vez de doblar de largo.
        const float WaypointArriveThreshold = 1.2f;

        // Anti-atasco. La grilla se construye una vez y no sabe de cuerpos
        // que se mueven; si algo lo deja trabado (una esquina, un obstaculo
        // que aparecio despues), se recalcula la ruta UNA vez en vez de
        // quedarse empujando la pared para siempre.
        const float StuckSeconds = 1f;
        const float StuckProgressSqr = 0.09f; // 30 cm
        float stuckTimer;
        Vector3 stuckAnchor;
        bool repathed;

        // Ronda de patrulla: mientras no haya nada más que hacer (Patrol),
        // camina de waypoint en waypoint en loop. Se corta solo si el
        // sensado detecta un enemigo (como cualquier otra cosa en Patrol).
        public void SetPatrolRoute(Vector3[] points)
        {
            patrolRoute = points;
            patrolIndex = 0;
        }

        public AiState State { get; private set; } = AiState.Patrol;
        public bool IsPossessedByPlayer { get; set; }
        public Soldier CurrentTarget => target;

        // ------------------------------------------------------------------
        // Item 212: postura de combate
        // ------------------------------------------------------------------
        // Setter publico: lo maneja la UI / las ordenes del jugador. Cambiar
        // de postura invalida el sensado cacheado y fuerza una consulta
        // nueva, porque el rango de vision efectivo acaba de cambiar y
        // servir el resultado calculado con el rango anterior seria mentir.
        public CombatStance Stance
        {
            get => stance;
            set
            {
                if (stance == value) return;
                stance = value;
                sensedTarget = null;
                forceSense = true;
                if (value == CombatStance.Defensiva) homePosition = transform.position;
            }
        }

        public Vector3 HomePosition => homePosition;

        // Rango de vision que usa el sensado. En Libre y en AltoElFuego el
        // multiplicador es la constante 1f, y x * 1f es bit a bit el mismo
        // float que x: la consulta recibe exactamente visionRange, el mismo
        // valor que recibia antes de existir las posturas.
        public float EffectiveVisionRange => visionRange * StanceVisionMultiplier;

        float StanceVisionMultiplier =>
            stance == CombatStance.Defensiva ? defensiveVisionMultiplier : 1f;

        // ------------------------------------------------------------------
        // Item 224: sensado repartido en el tiempo (verificable desde afuera)
        // ------------------------------------------------------------------
        public int SenseIntervalTicks
        {
            get => senseIntervalTicks;
            // Menos de 1 seria division por cero en el modulo del desfasaje.
            set => senseIntervalTicks = Mathf.Max(1, value);
        }

        // Antiguedad del objetivo cacheado, en ticks. Mientras el soldado
        // este en un estado que sensa (Patrol / Idle / orden de movimiento
        // simple) nunca supera SenseIntervalTicks - 1: ese es el tope de
        // obsolescencia que se puede verificar desde afuera. En Chase y
        // Attack sigue creciendo porque ahi no se sensa -- tampoco se
        // sensaba antes -- y el cache se revalida antes de volver a usarse.
        public int TicksSinceLastSense => tickCount - lastSenseTick;

        public Soldier LastSensedTarget => sensedTarget;

        // Para dibujar la linea de destino en RTS (punto 26 del backlog):
        // solo tiene sentido mientras hay una orden de movimiento simple
        // en curso, no durante una persecucion de combate.
        public Vector3? CurrentOrderDestination => hasOrder && State == AiState.MovingToOrder ? orderDestination : (Vector3?)null;

        // Verificable desde afuera (tests, UI): a quien sigo mientras
        // realmente estoy en Follow. Null en cualquier otro estado, igual
        // que CurrentOrderDestination con MovingToOrder.
        public Soldier FollowTarget => State == AiState.Follow ? followTarget : null;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            self = GetComponent<Soldier>();
            homePosition = transform.position;
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnAnyDamage);
        }

        void OnDestroy() => damageSub?.Dispose();

        void SetState(AiState next)
        {
            if (State == next) return;
            State = next;
            EventBus.Instance.Publish(new AiStateChangedEvent(self.Id, next.ToString()));
        }

        void OnAnyDamage(DamageTakenEvent evt)
        {
            if (self == null || !self.Health.IsAlive) return;

            var attacker = ActorRegistry.FindById(evt.AttackerId);
            if (attacker == null || !attacker.Health.IsAlive) return;

            // Me dispararon a mí: reacciono aunque esté fuera de mi rango de visión normal.
            if (evt.TargetId == self.Id)
            {
                if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder || State == AiState.Follow)
                {
                    target = attacker;
                    hasOrder = false;
                    followTarget = null;
                    SetState(AiState.Chase);
                }
                return;
            }

            // Le dispararon a un aliado cerca de mí: me sumo a la pelea.
            var victim = ActorRegistry.FindById(evt.TargetId);
            if (victim == null || victim == self || victim.Team != self.Team) return;
            if (State != AiState.Idle && State != AiState.Patrol) return;

            float dist = Vector3.Distance(self.transform.position, victim.transform.position);
            if (dist > alertRadius) return;

            // BUG REAL: el radio se medía contra la VICTIMA y despues se
            // tomaba de objetivo al ATACANTE, sin acotar a que distancia
            // estaba ese atacante. O sea que si a un aliado a cinco metros
            // le disparaba un francotirador desde ochenta, este soldado
            // abandonaba su patrulla y se iba a cruzar medio mapa a
            // perseguir a alguien que nunca vio. Con un mapa de 160 metros
            // de largo y una torreta que alcanza 40, se llega solo.
            //
            // Escuchar el tiroteo tiene sentido; convertirlo en un objetivo
            // a cualquier distancia, no.
            if (Vector3.Distance(self.transform.position, attacker.transform.position) > alertRadius) return;

            target = attacker;
            hasOrder = false;
            SetState(AiState.Chase);
        }

        // queued=false (por defecto) es la orden de siempre: borra todo lo
        // planificado y va a este punto. queued=true encola este punto
        // detras de lo que ya haya, sin interrumpir el tramo en curso.
        public void IssueMoveOrder(Vector3 point, bool queued = false)
        {
            if (!bootstrapped) Bootstrap();

            if (queued && hasOrder && State == AiState.MovingToOrder && mountTarget == null)
            {
                orderQueue.Enqueue(point);
                return;
            }

            // Attack-move: pedido explicito ("que se mueva pero no deje de
            // atacar"). Si ya esta trabado con un objetivo vivo, esta orden
            // NO lo suelta -- solo le dice hacia donde caminar mientras
            // sigue disparando. Encolar (Shift) durante combate no aplica:
            // no hay "combate en curso" que encolar detras, se ignora el
            // flag y se redirige igual.
            if (target != null && target.Health.IsAlive &&
                (State == AiState.Chase || State == AiState.Attack || State == AiState.MovingToAttackOrder))
            {
                attackMoveDestination = point;
                PlanPathTo(point);
                GameLog.Line($"{self.DisplayName} avanza a {point} sin dejar de atacar a {target.DisplayName}");
                return;
            }

            target = null;
            hasOrder = true;
            orderIsAttack = false;
            mountTarget = null;
            orderQueue.Clear();
            orderDestination = point;
            attackMoveDestination = null;
            PlanPathTo(point);
            SetState(AiState.MovingToOrder);
            // Pedido explicito ("usa mejor los estados"): una orden del
            // jugador corta el combate en curso (a proposito, ver el
            // comentario de arriba del metodo), pero antes el PROXIMO
            // sensado quedaba sujeto al intervalo repartido de siempre
            // (hasta senseIntervalTicks-1 ticks sirviendo el cache viejo).
            // Justo despues de una orden es cuando mas importa que la
            // proxima consulta sea fresca: si hay un enemigo encima, se
            // re-engancha en Chase en el tick que sigue, no unos ticks
            // despues.
            forceSense = true;
        }

        // ------------------------------------------------------------------
        // Rodeo de obstaculos
        // ------------------------------------------------------------------
        // Se llama UNA vez por orden, no por frame. Si la linea recta al
        // destino esta libre (el caso comun) NavService devuelve false sin
        // tocar el A*, la ruta queda vacia y todo se mueve como siempre.
        void PlanPathTo(Vector3 destination)
        {
            path.Clear();
            pathIndex = 0;
            repathed = false;
            ResetStuckWatch();

            if (!SP.Core.NavService.TryFindDetour(self.transform.position, destination, path))
            {
                path.Clear();
                return;
            }

            // El primer punto de la ruta ES la posicion actual: arrancar
            // ahi seria "llegar" al instante y perder un tramo.
            pathIndex = 1;
            GameLog.Line($"{self.DisplayName} rodea un obstaculo: {path.Count - 1} tramos hasta {destination}");
        }

        void ClearPath()
        {
            path.Clear();
            pathIndex = 0;
            repathed = false;
        }

        void ResetStuckWatch()
        {
            stuckTimer = 0f;
            stuckAnchor = self != null ? self.transform.position : Vector3.zero;
        }

        // Igual que Motor.MoveTowards, pero pasando por los waypoints de la
        // ruta si hay una. Devuelve true al llegar al destino FINAL.
        bool AdvanceTo(Vector3 destination, float threshold, float dt)
        {
            TickStuckWatch(destination, dt);

            if (pathIndex >= path.Count)
                return self.Motor.MoveTowards(destination, threshold, dt);

            bool last = pathIndex == path.Count - 1;
            // El ultimo punto de la ruta se reemplaza por el destino real:
            // el A* devuelve el centro de un nodo de la grilla, y frenar
            // ahi dejaria al soldado hasta a un nodo del punto pedido.
            Vector3 waypoint = last ? destination : path[pathIndex];

            if (!self.Motor.MoveTowards(waypoint, last ? threshold : WaypointArriveThreshold, dt))
                return false;

            pathIndex++;
            if (pathIndex < path.Count) return false;

            ClearPath();
            return true;
        }

        void TickStuckWatch(Vector3 destination, float dt)
        {
            stuckTimer += dt;
            if (stuckTimer < StuckSeconds) return;

            Vector3 progress = self.transform.position - stuckAnchor;
            progress.y = 0f;
            bool stuck = progress.sqrMagnitude < StuckProgressSqr;
            ResetStuckWatch();

            if (!stuck) return;

            // SEGUNDO ATASCO: la ruta ya se recalculo una vez y el soldado
            // sigue sin avanzar. Eso significa que el destino no se puede
            // alcanzar -- tipicamente porque cae DENTRO de un solido (un
            // arbol, el Muro, una barricada).
            //
            // BUG REAL medido: sin esto el soldado empujaba contra el
            // obstaculo indefinidamente. A los 20 segundos seguia en
            // MovingToOrder a 0,70 m del destino, y como la orden nunca
            // terminaba tampoco publicaba OrderCompletedEvent, ni sacaba
            // el siguiente punto de la cola, ni volvia a Patrol: el
            // soldado quedaba inutil por el resto de la partida.
            //
            // Se da la orden por cumplida donde se pudo llegar. Es lo
            // honesto: el jugador ve al soldado detenerse y volver a estar
            // disponible, en vez de un cuerpo empujando una pared.
            if (repathed && State == AiState.MovingToOrder)
            {
                GameLog.Line($"{self.DisplayName} no puede acercarse mas: el destino esta bloqueado");
                if (orderQueue.Count > 0)
                {
                    orderDestination = orderQueue.Dequeue();
                    PlanPathTo(orderDestination);
                    return;
                }
                EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));
                hasOrder = false;
                ClearPath();
                SetState(AiState.Patrol);
                forceSense = true;
                return;
            }

            // Primer atasco: se recalcula la ruta UNA vez. Reintentar sin
            // limite seria correr un A* por segundo por cada soldado
            // trabado contra otro cuerpo.
            if (repathed) return;
            repathed = true;

            path.Clear();
            pathIndex = 0;
            if (SP.Core.NavService.TryFindDetour(self.transform.position, destination, path))
            {
                pathIndex = 1;
                GameLog.Line($"{self.DisplayName} estaba trabado: recalcula la ruta ({path.Count - 1} tramos)");
            }
        }

        public void IssueMountOrder(Vehicle vehicle)
        {
            if (vehicle == null) return;
            if (!bootstrapped) Bootstrap();
            target = null;
            hasOrder = true;
            orderIsAttack = false;
            mountTarget = vehicle;
            orderQueue.Clear();
            orderDestination = vehicle.transform.position;
            PlanPathTo(orderDestination);
            SetState(AiState.MovingToOrder);
            forceSense = true;
        }

        // "Que me sigan": a diferencia de IssueMoveOrder, no hay un punto
        // fijo que borrar/reemplazar -- Follow se re-evalua cada Tick
        // contra la posicion ACTUAL de leader. Pisa cualquier orden previa
        // (mueve, ataca, montar) igual que las demas Issue*.
        public void IssueFollowOrder(Soldier leader)
        {
            if (leader == null || leader == self) return;
            if (!bootstrapped) Bootstrap();
            target = null;
            hasOrder = true;
            orderIsAttack = false;
            mountTarget = null;
            orderQueue.Clear();
            ClearPath();
            followTarget = leader;
            SetState(AiState.Follow);
            forceSense = true;
        }

        public void IssueAttackOrder(Soldier enemy)
        {
            if (!bootstrapped) Bootstrap();
            target = enemy;
            hasOrder = true;
            orderIsAttack = true;
            mountTarget = null;
            orderQueue.Clear();
            orderDestination = self.transform.position;
            ClearPath();
            SetState(AiState.MovingToAttackOrder);
        }

        // Una orden dada por error no se podia deshacer: el soldado
        // caminaba hasta el destino equivocado y habia que esperar a que
        // llegara para recien ahi poder redirigirlo. No cancela Chase ni
        // Attack (esos son reacciones al combate, no una orden que el
        // jugador pueda simplemente retirar) ni al vehiculo objetivo de un
        // Mount ya en curso a mitad de camino, que se maneja aparte.
        public void CancelOrder()
        {
            if (!bootstrapped) Bootstrap();
            hasOrder = false;
            orderIsAttack = false;
            mountTarget = null;
            followTarget = null;
            attackMoveDestination = null;
            orderQueue.Clear();
            ClearPath();
            if (State == AiState.MovingToOrder || State == AiState.MovingToAttackOrder || State == AiState.Follow)
            {
                target = null;
                SetState(AiState.Patrol);
                forceSense = true;
            }
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (IsPossessedByPlayer || self == null) return;
            if (!self.gameObject.activeInHierarchy) return;

            if (!self.Health.IsAlive)
            {
                SetState(AiState.Dead);
                return;
            }

            // Reloj propio de este cerebro (item 224). Cuenta solo los ticks
            // que realmente simula: si esta poseido, inactivo o muerto sale
            // antes y no avanza, asi que el intervalo de sensado se mide en
            // ticks de IA reales y no en frames de reloj de pared.
            tickCount++;

            if (target != null && !target.Health.IsAlive)
                target = null;

            if (target == null && (State == AiState.Chase || State == AiState.Attack))
            {
                if (orderIsAttack) { hasOrder = false; orderIsAttack = false; }
                // El objetivo murio/desaparecio a mitad de un attack-move:
                // el destino pedido sigue en pie, se convierte en una
                // orden de movimiento normal en vez de perderse.
                if (attackMoveDestination.HasValue)
                {
                    orderDestination = attackMoveDestination.Value;
                    attackMoveDestination = null;
                    PlanPathTo(orderDestination);
                    hasOrder = true;
                    SetState(AiState.MovingToOrder);
                }
                else
                {
                    SetState(!hasOrder ? AiState.Patrol : followTarget != null ? AiState.Follow : AiState.MovingToOrder);
                }
            }

            // El sensado puede interrumpir una patrulla u orden de movimiento
            // simple, pero no una orden de ataque ni una de subir a un
            // vehículo ya en curso: esas son deliberadas.
            bool onProtectedOrder = State == AiState.MovingToAttackOrder ||
                (State == AiState.MovingToOrder && mountTarget != null);
            if (State != AiState.Chase && State != AiState.Attack && !onProtectedOrder)
            {
                // Misma guarda de sensado de siempre; lo unico que cambia es
                // de donde sale el resultado: la consulta ahora pasa por el
                // cache repartido en el tiempo y por el rango efectivo de la
                // postura (en Libre, visionRange tal cual).
                var sensed = SenseNearestEnemy();
                if (sensed != null)
                {
                    target = sensed;
                    hasOrder = false;
                    SetState(AiState.Chase);
                }
            }

            switch (State)
            {
                case AiState.Patrol:
                    if (patrolRoute != null && patrolRoute.Length > 0)
                    {
                        if (self.Motor.MoveTowards(patrolRoute[patrolIndex], 1f, dt))
                            patrolIndex = (patrolIndex + 1) % patrolRoute.Length;
                    }
                    break;

                case AiState.Idle:
                    break;

                case AiState.MovingToOrder:
                    if (mountTarget != null && (mountTarget.gameObject == null || mountTarget.IsDestroyed))
                    {
                        hasOrder = false;
                        mountTarget = null;
                        SetState(AiState.Patrol);
                        break;
                    }

                    Vector3 moveTarget = mountTarget != null ? mountTarget.transform.position : orderDestination;
                    if (AdvanceTo(moveTarget, arriveThreshold, dt))
                    {
                        if (mountTarget != null)
                        {
                            hasOrder = false;
                            mountTarget.Mount(self);
                            mountTarget = null;
                            return; // el GameObject quedó inactivo: no tocar más estado.
                        }

                        if (orderQueue.Count > 0)
                        {
                            orderDestination = orderQueue.Dequeue();
                            PlanPathTo(orderDestination);
                            break;
                        }

                        EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));
                        hasOrder = false;
                        SetState(AiState.Patrol);
                    }
                    break;

                case AiState.Follow:
                    // El lider murio, se desactivo (subio a un vehiculo) o
                    // se cancelo por otro lado: no hay a quien seguir, se
                    // suelta la orden en vez de quedarse persiguiendo un
                    // punto viejo para siempre.
                    if (followTarget == null || !followTarget.Health.IsAlive || !followTarget.gameObject.activeInHierarchy)
                    {
                        hasOrder = false;
                        followTarget = null;
                        SetState(AiState.Patrol);
                        break;
                    }
                    // A diferencia de MovingToOrder, el destino se
                    // recalcula CADA tick contra la posicion actual del
                    // lider: por eso Follow nunca "llega" y termina solo,
                    // solo se corta si lo cancelan o el combate lo saca.
                    self.Motor.MoveTowards(followTarget.transform.position, followStopDistance, dt);
                    break;

                case AiState.Chase:
                case AiState.MovingToAttackOrder:
                    if (target == null) { SetState(AiState.Patrol); break; }
                    float d = Vector3.Distance(self.transform.position, target.transform.position);

                    // La linea de tiro se pregunta ACA, no solo al gatillar.
                    // Sin esta mitad, el gate de disparo hacia oscilar el
                    // estado: Chase veia al enemigo en rango y pasaba a
                    // Attack, Attack no tenia linea y volvia a Chase, y asi
                    // en cada tick. MEDIDO: 300 cambios de estado en 300
                    // ticks, o sea sesenta AiStateChangedEvent por segundo
                    // por soldado, cada uno repintando el indicador de
                    // estado de la escuadra.
                    //
                    // Preguntando aca el estado queda quieto: sin linea se
                    // sigue en Chase (acercandose o buscando el angulo), y
                    // recien se entra en Attack cuando de verdad se puede
                    // disparar.
                    if (d <= attackRange && TieneLineaDeTiro(target)) SetState(AiState.Attack);
                    // Attack-move con el objetivo todavia fuera de rango:
                    // camina hacia el punto pedido (no hacia el enemigo) --
                    // en cuanto entra en rango, el caso de arriba lo manda
                    // a Attack igual, este destino ya no importa mas ahi.
                    else if (attackMoveDestination.HasValue)
                    {
                        if (AdvanceTo(attackMoveDestination.Value, arriveThreshold, dt))
                            attackMoveDestination = null;
                    }
                    // En Libre StanceAllowsPursuit devuelve true de entrada,
                    // asi que este else-if ejecuta el MISMO MoveTowards de
                    // antes y la rama de abajo es inalcanzable.
                    else if (StanceAllowsPursuit(target.transform.position))
                        self.Motor.MoveTowards(target.transform.position, attackRange * 0.85f, dt);
                    else HoldStancePosition(dt);
                    break;

                case AiState.Attack:
                    if (target == null || !target.Health.IsAlive) { SetState(AiState.Patrol); break; }
                    float dd = Vector3.Distance(self.transform.position, target.transform.position);
                    if (dd > attackRange) { SetState(hasOrder ? AiState.MovingToAttackOrder : AiState.Chase); break; }

                    self.Motor.LookTowards(target.transform.position, dt);
                    self.Weapon.Tick(dt);
                    // BUG REAL: antes se disparaba en el MISMO tick en que se
                    // entraba en Attack, sin importar hacia donde mirara
                    // todavia el cuerpo -- LookTowards gira gradual
                    // (turnSpeedDegPerSec), pero TryFire calcula su propia
                    // direccion al target de forma independiente, asi que el
                    // proyectil salia perfecto mientras el arma en pantalla
                    // seguia apuntando para otro lado. Se veia como "dispara
                    // para cualquier lado", sobre todo al arrancar la
                    // partida: varios aliados sensan un enemigo ya dentro de
                    // rango en el mismo tick (Patrol/Idle -> Attack directo,
                    // sin haber girado nunca hacia el). Mismo gate que ya
                    // usa TurretAI (IsAimedAt) antes de su propio TryFire.
                    Vector3 flatDir = target.transform.position - self.transform.position;
                    flatDir.y = 0f;
                    float aimAngleDeg = flatDir.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(self.transform.forward, flatDir);
                    bool aimedAtTarget = flatDir.sqrMagnitude < 0.0001f || aimAngleDeg <= aimToleranceDeg;

                    // Sin linea de tiro no se gatilla: se vuelve a Chase para
                    // buscar el angulo. Quedarse quieto disparandole a la
                    // pared es lo que hacia antes.
                    if (!TieneLineaDeTiro(target))
                    {
                        SetState(hasOrder ? AiState.MovingToAttackOrder : AiState.Chase);
                        break;
                    }
                    // En Libre StanceAllowsFire es true y el TryFire es el
                    // mismo de siempre. AltoElFuego encara y sigue al
                    // enemigo con la mira, pero no aprieta el gatillo.
                    if (StanceAllowsFire && !aimedAtTarget)
                        Debug.Log($"[FireGate] BLOQUEADO: {self.name} encara a {target.name} pero angulo={aimAngleDeg:F1} > tolerancia {aimToleranceDeg} -- sin el gate esto disparaba a la nada", target.gameObject);
                    if (StanceAllowsFire && aimedAtTarget)
                    {
                        bool fired = self.Weapon.TryFire(self.transform.position, (target.transform.position - self.transform.position).normalized);
                        if (fired)
                        {
                            string moving = attackMoveDestination.HasValue ? " (en movimiento)" : "";
                            Debug.Log($"[FireGate] DISPARA{moving}: {self.name} -> {target.name} | angulo={aimAngleDeg:F1} (tolerancia {aimToleranceDeg}) | dist={dd:F1}", target.gameObject);
                        }
                    }

                    // Attack-move: se traslada hacia el destino pedido SIN
                    // reorientar el cuerpo (Motor.Move, no MoveTowards) --
                    // LookTowards ya giro el torso hacia el target unas
                    // lineas arriba, y girarlo de nuevo hacia el destino
                    // aca rompería el angulo de apuntado que el gate de
                    // arriba acaba de validar.
                    if (attackMoveDestination.HasValue)
                    {
                        // Si hay una ruta calculada (habia algo en el
                        // medio), el que manda es el waypoint en curso y
                        // no el destino final: caminar en linea recta
                        // hacia el destino mientras se dispara es
                        // exactamente lo que metia al soldado contra el
                        // Muro con el gatillo apretado.
                        bool onDetour = pathIndex < path.Count - 1;
                        Vector3 amGoal = onDetour ? path[pathIndex] : attackMoveDestination.Value;

                        Vector3 amDelta = amGoal - self.transform.position;
                        amDelta.y = 0f;
                        float amDist = amDelta.magnitude;

                        if (onDetour)
                        {
                            if (amDist <= WaypointArriveThreshold) pathIndex++;
                            else self.Motor.Move(amDelta / amDist, dt);
                        }
                        else if (amDist <= arriveThreshold)
                        {
                            attackMoveDestination = null;
                            ClearPath();
                        }
                        else self.Motor.Move(amDelta / amDist, dt);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Linea de tiro
        // ------------------------------------------------------------------
        // BUG REAL: la IA no miraba NUNCA si habia algo en el medio. Sensaba
        // por distancia pura y disparaba con solo tener al enemigo en rango
        // y encarado. O sea que "veia" a traves del Muro, de las barricadas
        // y de los arboles.
        //
        // Mientras las balas atravesaban el escenario el sintoma era otro
        // (te mataban a traves de una pared). Ahora que el proyectil choca
        // de verdad, sin esto el soldado se queda parado descargando el
        // cargador contra la cobertura, sin hacerle un rasguño al enemigo y
        // sin moverse jamas: el combate se traba para siempre.
        //
        // El rayo va de cuerpo a cuerpo -- el transform del soldado ya esta
        // a la altura del pecho -- y usa la MISMA definicion de pared que
        // SoldierMotor y que el proyectil. Si las tres no coincidieran,
        // habria angulos donde la IA cree tener tiro, la bala choca y nadie
        // entiende por que.
        static readonly RaycastHit[] BufferVision = new RaycastHit[8];

        public bool TieneLineaDeTiro(Soldier objetivo)
        {
            if (objetivo == null || self == null) return false;

            var desde = self.transform.position;
            var hasta = objetivo.transform.position;
            var delta = hasta - desde;
            float dist = delta.magnitude;
            if (dist < 0.0001f) return true;

            var dir = delta / dist;
            int n = Physics.RaycastNonAlloc(desde, dir, BufferVision, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = BufferVision[i].collider;
                if (c == null) continue;
                // El propio cuerpo y el del objetivo no tapan nada: son las
                // dos puntas del rayo.
                if (c.transform.IsChildOf(self.transform)) continue;
                if (c.transform.IsChildOf(objetivo.transform)) continue;
                if (!SP.Core.NavService.BlocksMovement(c)) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Item 212: modificadores de postura
        // ------------------------------------------------------------------
        // Ninguno de estos dos metodos reescribe una decision: se enchufan
        // como condicion sobre las decisiones que ya existian. La primera
        // linea de cada uno es la salida neutra de Libre, para que la
        // postura por defecto recorra el mismo camino de antes.
        bool StanceAllowsPursuit(Vector3 targetPosition)
        {
            if (stance == CombatStance.Libre) return true;
            if (stance == CombatStance.AltoElFuego) return false;
            // Defensiva: persigo mientras el objetivo siga dentro de la
            // burbuja alrededor de mi puesto. Se mide contra la posicion del
            // objetivo y no contra la mia para que el soldado no quede
            // oscilando justo sobre el borde de la correa.
            return Vector3.Distance(homePosition, targetPosition) <= defensiveLeashRadius;
        }

        bool StanceAllowsFire => stance != CombatStance.AltoElFuego;

        // Que hace cuando la postura le prohibe avanzar. Solo se llama con
        // target != null (lo garantiza el case de Chase).
        void HoldStancePosition(float dt)
        {
            // Defensiva: si venia persiguiendo cuando le cambiaron la
            // postura, o lo arrastro una orden previa, vuelve caminando a su
            // puesto en vez de quedarse clavado lejos de casa.
            if (stance == CombatStance.Defensiva &&
                Vector3.Distance(self.transform.position, homePosition) > arriveThreshold)
            {
                self.Motor.MoveTowards(homePosition, arriveThreshold, dt);
                return;
            }

            // Ya esta en su puesto (o es AltoElFuego): no avanza, pero
            // mantiene al enemigo encarado. Sigue detectandolo, que es
            // justo lo que pide la postura.
            self.Motor.LookTowards(target.transform.position, dt);
        }

        // ------------------------------------------------------------------
        // Item 224: sensado repartido en el tiempo
        // ------------------------------------------------------------------
        // Se reparte SOLO esta consulta ("cual es el enemigo mas cercano"),
        // que es lo caro y lo que se puede diferir. La maquina de estados y
        // el movimiento siguen corriendo todos los ticks: mover a un soldado
        // 1 de cada N frames se ve como un tartamudeo, y eso seria un precio
        // visible a cambio de un ahorro invisible.
        //
        // OBSOLESCENCIA ACOTADA: el objetivo que devuelve este metodo puede
        // tener hasta SenseIntervalTicks - 1 ticks de antiguedad (con N=3,
        // hasta 2 ticks; a 60 fps, 33 ms). Es aceptable porque las escalas
        // no se parecen: un soldado camina a 5 m/s y el rango de vision es
        // de 10 m, asi que en 2 ticks recorre unos 17 cm -- necesita cientos
        // de frames para entrar o salir del rango de vision. El unico efecto
        // observable es reaccionar hasta 2 frames tarde a un enemigo que
        // aparece, y el desfasaje por soldado hace que ni siquiera reaccionen
        // todos tarde a la vez.
        //
        // El desfasaje es Id % N y no un random a proposito: reparte la carga
        // igual de bien pero es determinista, asi dos corridas identicas dan
        // el mismo resultado y las pruebas headless siguen siendo repetibles.
        Soldier SenseNearestEnemy()
        {
            // CRITICO: si el objetivo cacheado murio o se desactivo (se subio
            // a un vehiculo) se descarta AHORA y se re-sensa sin esperar el
            // intervalo. Un soldado apuntandole 3 frames a un cadaver es un
            // bug que se ve en pantalla.
            //
            // Ojo con lo que esto NO es: no es un filtro de la busqueda. La
            // consulta sigue siendo la misma de siempre y sigue SIN filtrar
            // por activeInHierarchy (ver el comentario de SpatialGrid.
            // Rebuild: el barrido original tampoco lo filtraba, y un soldado
            // montado igual podia ser sensado). Si la busqueda original
            // hubiera devuelto a ese soldado inactivo, esta tambien lo
            // devuelve: lo unico que se fuerza es volver a preguntarle al
            // mundo en vez de servir un puntero viejo sin revisar. La regla
            // de a quien se detecta no cambia.
            if (sensedTarget != null && !IsCachedSenseUsable(sensedTarget))
            {
                sensedTarget = null;
                forceSense = true;
            }

            if (forceSense || senseIntervalTicks <= 1 ||
                (tickCount + (self.Id % senseIntervalTicks)) % senseIntervalTicks == 0)
            {
                forceSense = false;
                lastSenseTick = tickCount;
                sensedTarget = ActorRegistry.FindNearestEnemyInRange(
                    self.transform.position, self.Team, EffectiveVisionRange);
            }

            return sensedTarget;
        }

        // Se evalua sobre el CACHE, nunca sobre los candidatos de la
        // busqueda. Un objetivo cacheado sirve mientras siga existiendo,
        // vivo y activo; si no, se re-sensa en el acto.
        static bool IsCachedSenseUsable(Soldier s)
        {
            return s != null && s.Health != null && s.Health.IsAlive &&
                   s.gameObject.activeInHierarchy;
        }
    }
}
