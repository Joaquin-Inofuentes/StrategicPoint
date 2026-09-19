using System;
using UnityEngine;
using UnityEngine.AI;
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
    public partial class AiBrain : MonoBehaviour
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

        // Tarea 2: tiempo maximo que persigue/ataca sin linea de tiro antes de
        // abandonar el target y volver a su patrulla u orden previa.
        [SerializeField] float tiempoMaximoSinVisibilidad = 5f;
        float segundosSinLineaDeTiro;

        // El NavMeshAgent es SOLO el planificador de ruta: updatePosition y
        // updateRotation quedan apagados y quien mueve el transform sigue
        // siendo SoldierMotor (colision con Deslizador, altura del pivote,
        // y el SoldierAnimatorDriver mide su desplazamiento). BUG REAL de
        // la primera version: con el agent moviendo el transform, apoyaba
        // el PIVOTE del soldado (0.8 m sobre los pies) en el suelo del
        // NavMesh -- todos quedaban enterrados -- y ademas peleaba con
        // Motor.Move/LookTowards de Attack/Follow/cobertura, que movian el
        // mismo transform por otro lado (tirones, animacion rota).
        NavMeshAgent agent;
        Vector3 agentDestino;
        bool agentTieneDestino;

        Soldier self;
        Soldier target;
        Vector3 orderDestination;
        bool hasOrder;
        bool orderIsAttack;
        bool bootstrapped;
        Vehicle mountTarget;
        IDisposable damageSub;
        IDisposable shotSub;

        // Item pedido: "que le pueda decir a mis aliados que me sigan".
        // A quien sigo -- normalmente el soldado poseido por el jugador.
        // Separado de orderDestination porque ese es un punto FIJO
        // (MovingToOrder termina al llegar); Follow no termina nunca solo,
        // persigue una posicion que se mueve cada Tick hasta que lo
        // cancelen o lo interrumpa el combate.
        Soldier followTarget;

        // Ranura de formacion al seguir, en espacio LOCAL del lider (Z+ =
        // su frente): (0,0,0) por defecto, que preserva el comportamiento
        // historico (seguir pegado al lider mismo, a followStopDistance
        // de el). OrderService.IssueFollowOrderForSelection le asigna una
        // ranura distinta a cada aliado de una escuadra para que no
        // converjan todos al mismo punto -- ver comentario en Follow mas
        // abajo.
        Vector3 followOffsetLocal;

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
        //
        // patrolRoute (Vector3[]) es el camino VIEJO: una copia CONGELADA
        // de las posiciones en el momento en que se llamo SetPatrolRoute.
        // BUG REAL reportado: "movi los waypoints y no se corrigio, quiero
        // que siempre siga las posiciones reales del mundo, no las
        // relativas" -- mover la esfera de un waypoint en el editor (o en
        // runtime) no cambiaba nada, porque el soldado caminaba contra el
        // Vector3 copiado, no contra la esfera. patrolWaypoints (Transform[])
        // es el camino NUEVO: referencias reales a los marcadores (las
        // mismas esferas de PatrolRouteLine), asi que leer su .position
        // siempre da la posicion ACTUAL en el mundo, la mueva quien la
        // mueva. patrolRoute se mantiene solo como respaldo para la suite
        // headless, que construye la escena en Edit mode sin marcadores de
        // verdad.
        [SerializeField] Vector3[] patrolRoute;
        [SerializeField] Transform[] patrolWaypoints;
        int patrolIndex;

        int PatrolCount => patrolWaypoints != null && patrolWaypoints.Length > 0
            ? patrolWaypoints.Length
            : (patrolRoute != null ? patrolRoute.Length : 0);

        // BUG REAL: si patrolWaypoints esta seteado (modo Transform) pero
        // el marcador de ESTE indice fue destruido en runtime, el operador
        // ?: caia a patrolRoute[i] -- y en este modo patrolRoute es SIEMPRE
        // null (ver SetPatrolWaypoints), asi que tiraba
        // NullReferenceException y abortaba el tick de IA de TODOS los
        // soldados de ese frame (WorldSimulationDriver.Step los recorre sin
        // try/catch). Si el marcador murio, quedarse en el lugar es mejor
        // que crashear la simulacion entera.
        Vector3 PatrolPointAt(int i) => patrolWaypoints != null && patrolWaypoints.Length > 0
            ? (patrolWaypoints[i] != null ? patrolWaypoints[i].position : transform.position)
            : patrolRoute[i];

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
            patrolWaypoints = null;
            patrolIndex = 0;
        }

        // Camino nuevo (ver el comentario arriba de patrolWaypoints): la
        // ronda queda atada a los Transform reales, no a una copia.
        public void SetPatrolWaypoints(Transform[] waypoints)
        {
            patrolWaypoints = waypoints;
            patrolRoute = null;
            patrolIndex = 0;
        }

        public AiState State { get; private set; } = AiState.Patrol;

        bool isPossessedByPlayer;
        public bool IsPossessedByPlayer
        {
            get => isPossessedByPlayer;
            set
            {
                if (isPossessedByPlayer == value) return;
                isPossessedByPlayer = value;
                OnPossessionChanged(value);
            }
        }

        void EnsureAgent()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
        }

        void SyncAgentSettings()
        {
            if (agent == null) return;
            agent.speed = 5f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            // El agent solo planifica: no toca el transform (ver el comentario
            // de la declaracion del campo).
            agent.updatePosition = false;
            agent.updateRotation = false;
            // Los soldados siempre se atravesaron entre si; la evitacion del
            // agent los frenaria contra companeros que el motor no frena.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            // Distancia del pivote a los pies: con el pivote en el centro del
            // cuerpo (BoxCollider centrado) el agent tiene que saber que su
            // "suelo" queda por debajo del transform.
            var col = GetComponent<Collider>();
            if (col != null) agent.baseOffset = Mathf.Max(0f, transform.position.y - col.bounds.min.y);
        }

        bool AgentActivo => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        // Los ajustes van ANTES y DESPUES de habilitar: si updatePosition
        // quedara en true aunque sea un frame, el agent apoya el pivote en
        // el suelo del NavMesh y el soldado se hunde.
        void HabilitarAgente()
        {
            SyncAgentSettings();
            agent.enabled = true;
            SyncAgentSettings();
            SincronizarAgente();
        }

        // El motor es quien mueve el cuerpo; el agent tiene que saber donde
        // quedo para que su ruta y su remainingDistance partan de ahi. Se
        // llama todos los ticks, asi tambien cubre los movimientos que hace
        // AiBrain directo por el motor (Chase, strafing, Follow).
        void SincronizarAgente()
        {
            if (!AgentActivo) return;
            agent.nextPosition = transform.position - Vector3.up * agent.baseOffset;
        }

        // Lo llama Vehicle.Dismount: el agent se apago al subir (Mount) y
        // tiene que volver a encenderse con los mismos ajustes (updatePosition
        // apagado) que el resto de los casos.
        public void ReactivarNavegacion()
        {
            EnsureAgent();
            if (agent == null || IsPossessedByPlayer || !gameObject.activeInHierarchy) return;
            agentTieneDestino = false;
            HabilitarAgente();
        }

        void OnPossessionChanged(bool possessed)
        {
            EnsureAgent();
            if (agent == null) return;
            agentTieneDestino = false;
            if (possessed)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;
            }
            else
            {
                if (gameObject.activeInHierarchy)
                {
                    HabilitarAgente();
                }
            }
        }

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

        // Alcances de vision y de ataque (los del nivel: aliados 20/12, enemigos 22/13). Los soldados
        // que aparecen en juego (oleadas, refuerzos) los reciben de aca, no del prefab (10/6).
        public void ConfigurarAlcances(float vision, float ataque)
        {
            visionRange = vision;
            attackRange = ataque;
            forceSense = true;
        }

        // Rango de vision que usa el sensado. En Libre y en AltoElFuego el
        // multiplicador es la constante 1f, y x * 1f es bit a bit el mismo
        // float que x: la consulta recibe exactamente visionRange, el mismo
        // valor que recibia antes de existir las posturas.
        public float EffectiveVisionRange => visionRange * StanceVisionMultiplier;

        float StanceVisionMultiplier =>
            stance == CombatStance.Defensiva ? defensiveVisionMultiplier : 1f;

        // RoleType.Sniper estaba declarado (Soldado_2_Kes nace con este rol
        // en SC_Gameplay) y no tenia NINGUN efecto de juego -- se comportaba
        // identico a Assault salvo por la etiqueta en el HUD. Mismo patron
        // que las posturas de arriba: un multiplicador que en el caso
        // neutro (cualquier otro rol) es la constante 1f. Un francotirador
        // ahora entra en combate y se sostiene en posicion desde mas lejos;
        // la precision extra (menos dispersion del arma) vive en
        // WeaponHolder.MultiplicadorRol, que lee el mismo Role.
        [SerializeField] float sniperAttackRangeMultiplier = 1.6f;

        public float EffectiveAttackRange => attackRange * RoleAttackRangeMultiplier;

        // Armas de corto alcance (metralleta, escopeta): el soldado se acerca a 70% del alcance normal,
        // que es donde su dispersion rinde. Sin esto se quedaban a 12 m disparando al aire y perdian
        // contra un fusil (medido en el banco de balance: flanqueador 0% vs fusilero a 12 m).
        public const float FactorDeAlcanceCorto = 0.7f;

        float RoleAttackRangeMultiplier
        {
            get
            {
                if (self == null) return 1f;
                if (self.Role == SP.Combat.RoleType.Sniper) return sniperAttackRangeMultiplier;
                var w = self.Weapon;
                if (w != null && (w.CurrentWeaponKind == SP.Combat.WeaponKind.Smg || w.CurrentWeaponKind == SP.Combat.WeaponKind.Shotgun)) return FactorDeAlcanceCorto;
                return 1f;
            }
        }

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

        // A donde va la orden de movimiento vigente aunque en este instante
        // el estado sea otro (Chase/Attack: el combate NO borra la orden, el
        // soldado la retoma al terminar). Lo usa la limpieza de marcadores
        // del recorrido: con CurrentOrderDestination un tiroteo a mitad de
        // camino hacia parecer "cumplido" el tramo y se borraba su marca.
        public Vector3? PendingMoveDestination =>
            hasOrder && !orderIsAttack && mountTarget == null ? orderDestination : (Vector3?)null;

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
            shotSub = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFiredNearby);

            EnsureAgent();
            if (agent != null)
            {
                if (IsPossessedByPlayer) agent.enabled = false;
                else HabilitarAgente();
            }
        }

        void OnDestroy()
        {
            damageSub?.Dispose();
            shotSub?.Dispose();
        }

        void SetState(AiState next)
        {
            if (State == next) return;
            // La cobertura elegida vale para la persecucion en curso. Si el
            // soldado sale de Chase (llego, lo perdio, le dieron una orden),
            // arrastrarla al proximo combate lo mandaria a esconderse detras
            // de un obstaculo que ya no tiene nada que ver.
            if (next != AiState.Chase) SoltarCobertura();
            // Agachado (ver el case Attack de Tick()) solo tiene sentido
            // MIENTRAS se dispara: al salir de Attack por cualquier motivo
            // -- el objetivo murio, se perdio la linea de tiro, una orden
            // lo saco -- hay que pararse de nuevo, no dejarlo agachado
            // caminando o persiguiendo.
            if (State == AiState.Attack && next != AiState.Attack && self != null && self.Motor != null)
            {
                // La cobertura ORDENADA por el jugador mantiene al soldado
                // agachado aunque el combate termine; la que eligio el enemigo
                // por su cuenta vale solo mientras dura el ataque.
                if (!coberturaPorOrden)
                {
                    enCobertura = false;
                    yendoACobertura = false;
                    self.Motor.SetCrouching(false);
                }
            }
            State = next;
            EventBus.Instance.Publish(new AiStateChangedEvent(self.Id, next.ToString()));
        }

        void OnAnyDamage(DamageTakenEvent evt)
        {
            if (self == null || Pasivo || !self.Health.IsAlive) return;

            var attacker = ActorRegistry.FindById(evt.AttackerId);
            if (attacker == null || !attacker.Health.IsAlive) return;

            // Me dispararon a mí: reacciono aunque esté fuera de mi rango de visión normal.
            if (evt.TargetId == self.Id)
            {
                ultimoAtacanteId = attacker.Id;
                tiempoUltimoAtaque = Time.time;
                if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder || State == AiState.Follow)
                {
                    target = attacker;
                    if (State != AiState.MovingToOrder) hasOrder = false;
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

        // Pedido explicito ("zona de escucha de disparos aliados"): antes
        // SOLO se reaccionaba a OnAnyDamage, es decir, a un tiro que
        // CONECTA. Un enemigo tirandole a un aliado detras de una cobertura
        // -- disparo tras disparo sin acertar ni uno -- no atraia a nadie
        // mas a la pelea, por mas cerca que estuviera. Un tiroteo se OYE
        // aunque no le pegue a nadie: mismo radio que ya usaba el aviso por
        // impacto (alertRadius), asi que "que tan lejos se escucha" queda
        // definido en un solo numero para las dos señales.
        void OnShotFiredNearby(ShotFiredEvent evt)
        {
            if (self == null || Pasivo || !self.Health.IsAlive) return;
            // Mismo criterio que OnAnyDamage: si ya esta ocupado (Chase,
            // Attack, siguiendo una orden) el tiro no lo interrumpe.
            if (State != AiState.Idle && State != AiState.Patrol) return;

            var shooter = ActorRegistry.FindById(evt.ShooterId);
            if (shooter == null || !shooter.Health.IsAlive || shooter.Team == self.Team) return;

            if (Vector3.Distance(self.transform.position, shooter.transform.position) > alertRadius) return;

            target = shooter;
            hasOrder = false;
            SetState(AiState.Chase);
        }

        // queued=false (por defecto) es la orden de siempre: borra todo lo
        // planificado y va a este punto. queued=true encola este punto
        // detras de lo que ya haya, sin interrumpir el tramo en curso.
        public void IssueMoveOrder(Vector3 point, bool queued = false)
        {
            if (!bootstrapped) Bootstrap();
            NuevaOrden();

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

            EnsureAgent();
            if (AgentActivo)
            {
                SincronizarAgente();
                if (agent.SetDestination(destination))
                {
                    agentDestino = destination;
                    agentTieneDestino = true;
                    return;
                }
                agentTieneDestino = false;
            }

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
            agentTieneDestino = false;
            if (AgentActivo) agent.ResetPath();
        }

        void ResetStuckWatch()
        {
            stuckTimer = 0f;
            stuckAnchor = self != null ? self.transform.position : Vector3.zero;
        }

        // Igual que Motor.MoveTowards, pero pasando por los waypoints de la
        // ruta si hay una. Devuelve true al llegar al destino FINAL.
        // Hasta donde acercarse cuando NO hay linea de tiro. No es el
        // alcance del arma: es practicamente "hasta tocarlo", porque el
        // problema era justamente frenar antes de tener angulo.
        const float DistanciaDeContacto = 1.5f;

        // Cada cuanto se rehace la ruta de rodeo. El usuario lo pidio con
        // este numero: "con tasa de refresco cada 0.5 segundos para validar
        // antes de disparar para saber si se debe mover o disparar".
        //
        // Y no es solo economia de A*: la primera version replanificaba
        // cuando la ruta estaba vacia, o sea practicamente por tick, y cada
        // PlanPathTo vuelve el indice a 0. El punto 0 de una ruta es la
        // posicion ACTUAL del soldado, asi que llegaba a el, avanzaba el
        // indice, y al tick siguiente volvia a empezar: la ruta se
        // recorria entera sin moverse un centimetro. Medido: 0,0 m en 15
        // segundos, peor que no hacer nada.
        const float RefrescoDeRodeo = 0.5f;

        // Si el enemigo se corrio mas que esto, la ruta vieja lleva a donde
        // ya no esta y se rehace sin esperar al refresco.
        const float RehacerRutaSiSeMovio = 3f;

        Vector3 destinoRodeo;
        bool tieneRodeo;
        float relojDeRodeo;

        // --- Cobertura (F3) ---
        // A que distancia como maximo se acepta ir a buscar una cobertura.
        // Mas lejos que esto, caminar hasta ahi cuesta mas que el rodeo
        // directo y el soldado se aleja del combate.
        public const float RadioDeBusquedaDeCobertura = 25f;
        // Mismo criterio que el rodeo: la eleccion se rehace cada medio
        // segundo, no cada tick. Sin el reloj, PlanPathTo resetea el indice
        // de la ruta todos los frames y el soldado consume el camino sin
        // moverse (bug ya medido en el rodeo: 0,0 m en 15 s).
        const float RefrescoDeCobertura = 0.5f;
        const float DistanciaEnLaCobertura = 0.6f;
        Vector3 coberturaElegida;
        bool tieneCobertura;
        float relojDeCobertura;

        public bool VaHaciaUnaCobertura => tieneCobertura;
        public Vector3 CoberturaElegida => coberturaElegida;

        // BUG REAL reportado por el usuario: "aprieto [Y] (Siganme) dos
        // veces y el primero rehace bien el camino pero el segundo queda
        // trabado". Causa: Follow llamaba a self.Motor.MoveTowards DIRECTO
        // contra la ranura de formacion, sin pasar por AdvanceTo ni
        // PlanPathTo -- no usaba el rodeo de NavService ni TickStuckWatch.
        // Si la ranura de un aliado quedaba del otro lado de un obstaculo
        // respecto de su posicion actual, empujaba contra el para siempre.
        // Generalizado a un solo helper: Patrol y el regreso al puesto en
        // Defensiva (HoldStancePosition) tenian el MISMO problema --
        // MoveTowards directo, sin A* ni deteccion de atasco -- asi que
        // quedarse trabado contra un obstaculo en plena ronda de patrulla
        // era igual de posible que en Follow. Cada llamador pasa su propio
        // trio destino/tiene/reloj por ref para no mezclar el estado de
        // una orden de seguimiento con el de una ronda de patrulla.
        bool AvanzarConRodeo(Vector3 objetivo, float umbral, float dt, ref Vector3 destino, ref bool tieneDestino, ref float reloj)
        {
            reloj += dt;
            bool seMovioMucho = tieneDestino && Vector3.Distance(destino, objetivo) > RehacerRutaSiSeMovio;
            if (!tieneDestino || seMovioMucho || reloj >= RefrescoDeRodeo)
            {
                destino = objetivo;
                tieneDestino = true;
                reloj = 0f;
                // Se replanifica DESDE LA POSICION ACTUAL, asi que empezar
                // de nuevo por el punto 0 no pierde el avance: cada ruta
                // nueva arranca donde el soldado esta parado ahora.
                PlanPathTo(objetivo);
            }
            return AdvanceTo(objetivo, umbral, dt);
        }

        void RodearHasta(Vector3 objetivo, float dt) =>
            AvanzarConRodeo(objetivo, DistanciaDeContacto, dt, ref destinoRodeo, ref tieneRodeo, ref relojDeRodeo);

        Vector3 destinoSeguimiento;
        bool tieneSeguimiento;
        float relojDeSeguimiento;

        void SeguirHasta(Vector3 objetivo, float umbral, float dt) =>
            AvanzarConRodeo(objetivo, umbral, dt, ref destinoSeguimiento, ref tieneSeguimiento, ref relojDeSeguimiento);

        Vector3 destinoPatrulla;
        bool tienePatrulla;
        float relojDePatrulla;

        Vector3 destinoRegresoAPuesto;
        bool tieneRegresoAPuesto;
        float relojDeRegresoAPuesto;

        // F3: en vez de rodear al enemigo al descubierto, ir a la cobertura
        // mas cercana DESDE LA QUE SE LE PUEDE DISPARAR. La segunda mitad
        // es la que importa: esconderse donde no se puede tirar es peor que
        // quedarse afuera -- se deja de hacer daño y ademas no hay motivo
        // para volver a salir.
        //
        // Devuelve false si no hay ninguna que sirva, y ahi el llamador se
        // queda con el rodeo de siempre.
        bool CubrirseDe(Soldier objetivo, float dt)
        {
            relojDeCobertura += dt;
            if (!tieneCobertura || relojDeCobertura >= RefrescoDeCobertura)
            {
                relojDeCobertura = 0f;
                Vector3 elegida;
                // El 0,85 del alcance es el mismo margen con el que ya se
                // frena el acercamiento normal: una cobertura a esa
                // distancia del enemigo es una posicion de tiro de verdad,
                // no una que queda a un paso de quedarse corta.
                tieneCobertura = SP.Core.Coberturas.TryMejorCobertura(
                    self.transform.position, objetivo, self, RadioDeBusquedaDeCobertura,
                    EffectiveAttackRange * 0.85f, out elegida);
                if (!tieneCobertura) return false;
                // Solo se replanifica cuando la eleccion CAMBIA: repetir
                // PlanPathTo al mismo punto cada medio segundo tira el
                // avance de la ruta a la basura.
                if ((elegida - coberturaElegida).sqrMagnitude > 0.01f || RemainingPathPoints == 0)
                {
                    coberturaElegida = elegida;
                    PlanPathTo(elegida);
                }
            }
            if (!tieneCobertura) return false;

            // Se para ENCIMA de la cobertura, no a distancia de contacto:
            // el punto de una cobertura es ocuparla. Frenando a 1,5 m el
            // soldado queda al descubierto justo al lado del obstaculo.
            AdvanceTo(coberturaElegida, DistanciaEnLaCobertura, dt);
            return true;
        }

        void SoltarCobertura()
        {
            tieneCobertura = false;
            relojDeCobertura = 0f;
        }

        bool AdvanceTo(Vector3 destination, float threshold, float dt)
        {
            EnsureAgent();
            if (TryAdvanceWithAgent(destination, threshold, dt, out bool llegoConAgent))
                return llegoConAgent;

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

        // Devuelve false si el agent no puede conducir este tramo (no hay
        // NavMesh bajo el soldado, o no hay ruta valida): el llamador sigue
        // con el A* casero de siempre, asi la suite headless (sin NavMesh
        // bakeado) y cualquier escena sin bakear se comportan como antes.
        // Devuelve true si el agent se hizo cargo; "llego" dice si ya llego.
        bool TryAdvanceWithAgent(Vector3 destination, float threshold, float dt, out bool llego)
        {
            llego = false;
            if (!AgentActivo) return false;

            SincronizarAgente();

            if (!agentTieneDestino || (agentDestino - destination).sqrMagnitude > 0.25f)
            {
                if (!agent.SetDestination(destination))
                {
                    agentTieneDestino = false;
                    return false;
                }
                agentDestino = destination;
                agentTieneDestino = true;
            }

            TickStuckWatchAgent(destination, dt);
            // El vigilante pudo dar la orden por cumplida (ClearPath).
            if (!agentTieneDestino || !AgentActivo) return true;

            // Sin primera ruta todavia: quieto este frame. Con una ruta
            // vieja (replanificacion periodica) se sigue caminando por ella
            // para no frenar cada medio segundo.
            if (agent.pathPending && !agent.hasPath) return true;
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                agentTieneDestino = false;
                return false;
            }

            Vector3 alDestino = destination - self.transform.position;
            alDestino.y = 0f;
            if (alDestino.magnitude <= threshold ||
                (!agent.pathPending && agent.remainingDistance <= threshold))
            {
                ClearPath();
                llego = true;
                return true;
            }

            // El agent decide POR DONDE (la esquina siguiente de su ruta);
            // el motor decide COMO (colision, giro, animacion). El paso se
            // acota a la distancia que falta para no pasarse de la esquina y
            // oscilar alrededor de ella.
            Vector3 esquina = agent.steeringTarget;
            Vector3 delta = esquina - self.transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0.001f)
            {
                self.Motor.LookTowards(esquina, dt);
                float paso = Mathf.Max(self.Motor.MoveSpeed * dt, 0.0001f);
                self.Motor.Move(delta / dist * Mathf.Min(1f, dist / paso), dt);
            }
            return true;
        }

        // Mismo contrato que TickStuckWatch (1 s sin avanzar 30 cm ->
        // replanifica una vez; segundo atasco en MovingToOrder -> orden
        // cumplida donde llego, drena la cola, OrderCompletedEvent), pero
        // la replanificacion es agent.SetDestination. Solo mide progreso
        // real: un PathPartial (destino fuera del NavMesh) NO cuenta como
        // atasco mientras el soldado siga avanzando hacia el borde.
        void TickStuckWatchAgent(Vector3 destination, float dt)
        {
            stuckTimer += dt;
            if (stuckTimer < StuckSeconds) return;

            Vector3 progress = self.transform.position - stuckAnchor;
            progress.y = 0f;
            bool stuck = progress.sqrMagnitude < StuckProgressSqr;
            ResetStuckWatch();

            if (!stuck) return;

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

            if (repathed) return;
            repathed = true;
            agent.SetDestination(destination);
            GameLog.Line($"{self.DisplayName} estaba trabado: recalcula la ruta por NavMesh");
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
            NuevaOrden();
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
        public void IssueFollowOrder(Soldier leader, Vector3 formationOffsetLocal = default)
        {
            if (leader == null || leader == self) return;
            if (!bootstrapped) Bootstrap();
            NuevaOrden();
            target = null;
            hasOrder = true;
            orderIsAttack = false;
            mountTarget = null;
            orderQueue.Clear();
            ClearPath();
            followTarget = leader;
            followOffsetLocal = formationOffsetLocal;
            SetState(AiState.Follow);
            forceSense = true;
        }

        public void IssueAttackOrder(Soldier enemy)
        {
            if (!bootstrapped) Bootstrap();
            NuevaOrden();
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
            NuevaOrden();
            hasOrder = false;
            orderIsAttack = false;
            mountTarget = null;
            followTarget = null;
            followOffsetLocal = Vector3.zero;
            attackMoveDestination = null;
            orderQueue.Clear();
            ClearPath();
            SoltarCobertura();
            target = null;
            SetState(AiState.Patrol);
            forceSense = true;
        }

        // Los aliados libres corren cuando el jugador corre ([Shift]) y ellos van hacia una
        // orden, siguen al lider o se retiran. En combate o quietos caminan normal.
        void ActualizarCarrera()
        {
            bool correr = self.Team == SP.Combat.TeamId.Player && AjustesDeEscuadra.Correr && !IsPossessedByPlayer
                && (State == AiState.MovingToOrder || State == AiState.Follow);
            self.Motor.SetRunning(correr);
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (IsPossessedByPlayer || self == null) return;
            if (!self.gameObject.activeInHierarchy) return;

            SincronizarAgente();
            ActualizarCarrera();

            // BUG REAL: Tick() no tenia ningun case Dead ni ninguna
            // transicion de SALIDA de Dead -- una vez que IsAlive pasaba a
            // false, State quedaba en Dead para siempre y el switch de mas
            // abajo hacia no-op cada tick. RescateAutomatico y
            // PlayerInputDriver.TryRevivir revivien con Health.Initialize
            // (full HP, IsAlive=true) pero jamas tocan AiState: sin esto un
            // aliado revivido quedaba vivo pero catatonico -- no patrullaba,
            // no volvia a su puesto, no reaccionaba a nada -- hasta que el
            // sensado encontraba un enemigo de pura casualidad. Mismo
            // reseteo de estado por-vida que CancelOrder: lo que estuviera
            // persiguiendo/siguiendo/cubriendose ANTES de morir no tiene
            // sentido despues de revivir.
            if (State == AiState.Dead && self.Health.IsAlive)
            {
                target = null;
                hasOrder = false;
                orderIsAttack = false;
                mountTarget = null;
                followTarget = null;
                followOffsetLocal = Vector3.zero;
                attackMoveDestination = null;
                orderQueue.Clear();
                ClearPath();
                SoltarCobertura();
                SetState(AiState.Patrol);
                forceSense = true;
            }

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

            // Tactica: vigilar la cobertura, seguir al jugador si se aleja,
            // revisar si hay un blanco mejor.
            TickCobertura(dt);
            TickSeguirAlJugador();
            TickRetarget(dt);
            if (enCobertura) self.Motor.SetCrouching(true);

            // BUG REAL: solo se chequeaba IsAlive. Un soldado que sube a un
            // vehiculo sigue vivo pero Vehicle.Mount lo desactiva
            // (gameObject.SetActive(false)) -- sin este chequeo, quien lo
            // tenia de target quedaba trabado persiguiendo/atacando a un
            // objetivo invisible e inalcanzable para siempre, porque el
            // re-sensado esta apagado mientras State es Chase o Attack.
            if (target != null && (!target.Health.IsAlive || !target.gameObject.activeInHierarchy))
                target = null;

            // Tarea 2: Si pierde la línea de tiro durante combate/persecución,
            // cuenta el tiempo continuo sin visión. Al superar el umbral, suelta el objetivo.
            bool enCombateConTarget = State == AiState.Chase || State == AiState.Attack || State == AiState.MovingToAttackOrder;
            if (target != null && enCombateConTarget)
            {
                if (TieneLineaDeTiro(target))
                {
                    segundosSinLineaDeTiro = 0f;
                }
                else
                {
                    segundosSinLineaDeTiro += dt;
                    if (segundosSinLineaDeTiro >= tiempoMaximoSinVisibilidad)
                    {
                        target = null;
                        segundosSinLineaDeTiro = 0f;
                    }
                }
            }
            else
            {
                segundosSinLineaDeTiro = 0f;
            }

            if (target == null && (State == AiState.Chase || State == AiState.Attack || State == AiState.MovingToAttackOrder))
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
                else if (hasOrder)
                {
                    PlanPathTo(orderDestination);
                    SetState(followTarget != null ? AiState.Follow : AiState.MovingToOrder);
                }
                else if (orderQueue.Count > 0)
                {
                    orderDestination = orderQueue.Dequeue();
                    hasOrder = true;
                    PlanPathTo(orderDestination);
                    SetState(AiState.MovingToOrder);
                }
                else
                {
                    SetState(AiState.Patrol);
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
                    AvisarDeteccion(sensed);
                    target = sensed;
                    if (State != AiState.MovingToOrder) hasOrder = false;
                    SetState(AiState.Chase);
                }
            }

            switch (State)
            {
                case AiState.Patrol:
                    int patrolCount = PatrolCount;
                    if (patrolCount > 0 && !enCobertura)
                    {
                        if (AvanzarConRodeo(PatrolPointAt(patrolIndex), 1f, dt, ref destinoPatrulla, ref tienePatrulla, ref relojDePatrulla))
                            patrolIndex = (patrolIndex + 1) % patrolCount;
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

                    // Apuntar al PIVOTE del vehiculo (adentro del chasis)
                    // solo funcionaba porque el chasis era atravesable para
                    // el movimiento. Ahora que NavService.BlocksMovement
                    // trata al vehiculo como pared solida (el soldado ya no
                    // lo atraviesa caminando), el destino real de la
                    // caminata es el punto abordable mas cercano DEL LADO
                    // DE AFUERA del casco (Vehicle.ClosestBoardingPoint) --
                    // si no, el soldado quedaria empujado contra el casco
                    // sin poder acercarse lo suficiente al pivote como para
                    // disparar arriveThreshold.
                    Vector3 moveTarget = mountTarget != null ? mountTarget.ClosestBoardingPoint(self.transform.position) : orderDestination;
                    float umbralLlegada = mountTarget != null ? Mathf.Max(arriveThreshold, 1.0f) : arriveThreshold;
                    if (AdvanceTo(moveTarget, umbralLlegada, dt))
                    {
                        if (mountTarget != null)
                        {
                            hasOrder = false;
                            bool subio = mountTarget.Mount(self);
                            mountTarget = null;
                            if (subio) return; // el GameObject quedó inactivo: no tocar más estado.

                            // BUG REAL: Mount() devuelve false si el vehiculo se
                            // lleno mientras caminaba (o se destruyo), y aca se
                            // hacia "return" igual: el soldado quedaba en
                            // MovingToOrder sin orden, empujando el casco. Ahora
                            // suelta la orden y vuelve a lo suyo.
                            GameLog.Line($"{self.DisplayName} no pudo subir: el vehiculo esta lleno");
                            ClearPath();
                            SetState(AiState.Patrol);
                            forceSense = true;
                            break;
                        }

                        // Orden de cobertura cumplida: se agacha y se queda.
                        if (coberturaPorOrden && yendoACobertura && orderQueue.Count == 0)
                        {
                            EntrarEnCobertura();
                            break;
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
                    //
                    // BUG REAL: sin ranura, TODOS los aliados de una orden
                    // de seguir en grupo apuntaban al mismo punto (el
                    // lider) con el mismo followStopDistance -- llegaban
                    // al mismo circulo alrededor de el y quedaban parados
                    // unos encima de otros. Con una ranura asignada
                    // (OrderService.IssueFollowOrderForSelection reparte
                    // una por soldado, en formacion) cada uno persigue un
                    // punto DISTINTO relativo al lider, expresado en su
                    // espacio local para que rote con el.
                    bool tieneRanura = followOffsetLocal.sqrMagnitude > 0.0001f;
                    Vector3 puntoASeguir = tieneRanura
                        ? followTarget.transform.position + followTarget.transform.TransformDirection(followOffsetLocal)
                        : followTarget.transform.position;
                    float umbralSeguimiento = tieneRanura ? Mathf.Max(arriveThreshold, 0.5f) : followStopDistance;
                    SeguirHasta(puntoASeguir, umbralSeguimiento, dt);
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
                    if (d <= EffectiveAttackRange && TieneLineaDeTiro(target)) SetState(AiState.Attack);
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
                    {
                        // Del plan del usuario: "Si hay un obstaculo entre
                        // enemigo y aliado. Ninguno de los 2 se asoma para
                        // disparar. Si hay obstaculo no deben disparar deben
                        // seguir acercandose hasta disparar".
                        //
                        // Pasaba por dos motivos a la vez, y hacia falta
                        // arreglar los dos:
                        //
                        //   1. Se caminaba en LINEA RECTA al enemigo
                        //      (Motor.MoveTowards), sin usar el A* que este
                        //      mismo componente ya usa para las ordenes de
                        //      movimiento. Contra el Muro eso es empujar
                        //      contra la pared para siempre.
                        //
                        //   2. Se frenaba al 85% del alcance de tiro. Con un
                        //      obstaculo en medio, "en rango" no quiere decir
                        //      "puedo disparar": el soldado llegaba a esa
                        //      distancia, se paraba, y ahi se quedaba.
                        //
                        // Medido con el Muro justo en medio, tras 15
                        // segundos y en las tres separaciones probadas
                        // (6, 10 y 16 m): CERO de daño hecho, y el soldado
                        // clavado a 5,1 / 6,5 / 9,5 m.
                        //
                        // Sin linea de tiro se rodea por la ruta y se sigue
                        // acercando hasta tenerla. Con linea, el
                        // acercamiento de siempre.
                        if (TieneLineaDeTiro(target))
                        {
                            // Con linea de tiro, el acercamiento de siempre:
                            // frenar al 85% del alcance esta bien, porque
                            // desde ahi ya se puede disparar.
                            self.Motor.MoveTowards(target.transform.position, EffectiveAttackRange * 0.85f, dt);
                        }
                        else
                        {
                            // Sin linea de tiro hay que RODEAR. Caminar
                            // derecho contra el obstaculo no sirve: el
                            // empuje es perpendicular a la cara y la
                            // proyeccion del deslizamiento da cero, asi que
                            // el soldado se queda pegado a la pared para
                            // siempre. Medido, empujando derecho: llegaba a
                            // 1,5 m del muro y ahi se quedaba, 0 de daño en
                            // 15 segundos.
                            // F3: primero se busca una cobertura desde la
                            // que SI se pueda disparar. Solo si no hay
                            // ninguna a mano se cae al rodeo de siempre,
                            // que es lo que arreglo el caso del Muro.
                            if (!CubrirseDe(target, dt))
                                RodearHasta(target.transform.position, dt);
                        }
                    }
                    else HoldStancePosition(dt);
                    break;

                case AiState.Attack:
                    if (target == null || !target.Health.IsAlive) { SetState(AiState.Patrol); break; }
                    float dd = Vector3.Distance(self.transform.position, target.transform.position);
                    if (dd > EffectiveAttackRange) { SetState(hasOrder ? AiState.MovingToAttackOrder : AiState.Chase); break; }

                    // Enemigo: busca una cobertura desde la que seguir
                    // disparando (ver AiBrain.Tactica). Corriendo hacia ella no
                    // dispara este tick.
                    if (TickCoberturaTactica(dt)) break;

                    // Pedido explicito ("se agachan y disparan?"): mismo
                    // SetCrouching de G2 que ya usa el jugador con Ctrl --
                    // WeaponHolder.MultiplicadorPostura ya lee
                    // owner.Motor.IsCrouching sin importar si el dueño es
                    // el jugador o la IA, solo que del lado de la IA nadie
                    // lo llamaba nunca. Agachado TODO el tiempo que dura
                    // Attack, haya cobertura cerca o no: presentar menos
                    // blanco mientras se dispara vale hasta a cielo abierto.
                    self.Motor.SetCrouching(true);

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
                    if (StanceAllowsFire && aimedAtTarget)
                    {
                        // BUG REAL, mismo que ya se encontro y arreglo del
                        // lado del jugador (ver PlayerBrain.Fire): el
                        // proyectil nace en la boca del arma (WeaponHolder.
                        // TryFire usa Muzzle.position para el spawn), pero
                        // ANTES la direccion se calculaba desde el CENTRO
                        // DEL CUERPO -- dos origenes distintos para el mismo
                        // disparo. Con el canio a varias decenas de cm de
                        // altura/costado respecto del pivote del soldado, la
                        // trazadora salia de la boca del arma pero volaba en
                        // paralelo a esa direccion, no realmente hacia
                        // adelante del cañon: se veia "torcida" cada vez que
                        // disparaba un aliado o un enemigo, que es la
                        // inmensa mayoria de los tiros que el jugador ve en
                        // pantalla. Ahora la direccion sale de la MISMA boca
                        // del arma que usa el spawn.
                        var muzzle = self.Weapon.Muzzle;
                        Vector3 origenDisparo = muzzle != null ? muzzle.position : self.transform.position;
                        Vector3 direccionDisparo = target.transform.position - origenDisparo;
                        if (direccionDisparo.sqrMagnitude < 0.0001f) direccionDisparo = self.transform.forward;
                        self.Weapon.TryFire(self.transform.position, direccionDisparo.normalized);
                    }

                    // Attack-move: se traslada hacia el destino pedido SIN
                    // reorientar el cuerpo (Motor.Move, no MoveTowards) --
                    // LookTowards ya giro el torso hacia el target unas
                    // lineas arriba, y girarlo de nuevo hacia el destino
                    // aca rompería el angulo de apuntado que el gate de
                    // arriba acaba de validar.
                    if (attackMoveDestination.HasValue)
                    {
                        // BUG REAL: a diferencia de todo otro consumidor de
                        // path/pathIndex (AdvanceTo, que usan RodearHasta,
                        // CubrirseDe y MovingToOrder), este attack-move
                        // nunca llamaba a TickStuckWatch. Si algo bloqueaba
                        // el tramo (un vehiculo que se cruzo, un obstaculo
                        // nuevo) el soldado empujaba contra el con el
                        // gatillo apretado, sin deteccion de atasco ni
                        // replanificacion, por el resto del combate.
                        TickStuckWatch(attackMoveDestination.Value, dt);

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
        // El raycast en si vive en NavService.HayLineaDeTiro: para elegir
        // una cobertura hay que poder preguntar lo mismo desde un punto
        // cualquiera, no solo desde el propio cuerpo. Aca queda la version
        // comoda de "yo, hacia ese soldado".
        public bool TieneLineaDeTiro(Soldier objetivo)
        {
            if (objetivo == null || self == null) return false;
            return SP.Core.NavService.HayLineaDeTiro(
                self.transform.position, objetivo.transform.position,
                self.transform, objetivo.transform);
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
            // En cobertura por orden del jugador: se queda ahi.
            if (enCobertura && coberturaPorOrden) return false;
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
            if (enCobertura && coberturaPorOrden)
            {
                if (Vector3.Distance(self.transform.position, coberturaPunto) > 1.2f)
                    AvanzarConRodeo(coberturaPunto, 0.6f, dt, ref destinoRegresoAPuesto, ref tieneRegresoAPuesto, ref relojDeRegresoAPuesto);
                else if (target != null) self.Motor.LookTowards(target.transform.position, dt);
                return;
            }

            // Defensiva: si venia persiguiendo cuando le cambiaron la
            // postura, o lo arrastro una orden previa, vuelve caminando a su
            // puesto en vez de quedarse clavado lejos de casa.
            if (stance == CombatStance.Defensiva &&
                Vector3.Distance(self.transform.position, homePosition) > arriveThreshold)
            {
                AvanzarConRodeo(homePosition, arriveThreshold, dt, ref destinoRegresoAPuesto, ref tieneRegresoAPuesto, ref relojDeRegresoAPuesto);
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
            if (Pasivo) return null;
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
                sensedTarget = MejorObjetivoVisible();
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

        // ------------------------------------------------------------------
        // Gizmos de depuracion: radios de deteccion/ataque y hacia donde
        // apunta o va cada unidad, aliada o enemiga. Pedido explicito:
        // "solo visibles en escena, NO en gameplay". OnDrawGizmos es
        // exactamente eso por definicion de Unity -- se dibuja en la vista
        // Scene del editor (con Gizmos activado) y NUNCA en Game view, ni
        // en un build final, sin necesidad de ningun #if ni chequeo de
        // Application.isPlaying: Unity ni siquiera llama a este metodo
        // fuera del editor. No usa Debug.DrawLine (eso SI queda dibujado
        // sobre Game view mientras Gizmos este activado ahi) ni ningun
        // LineRenderer/mesh real (eso SI seria visible en gameplay/build).
        // ------------------------------------------------------------------
        void OnDrawGizmos()
        {
            if (self == null) self = GetComponent<Soldier>();
            if (self == null) return;

            bool esAliado = self.Team == SP.Combat.TeamId.Player;
            Color colorDeteccion = esAliado ? new Color(0.3f, 0.7f, 1f, 0.6f) : new Color(1f, 0.55f, 0.15f, 0.6f);
            Color colorAtaque = new Color(1f, 0.15f, 0.15f, 0.7f);

            Vector3 pos = transform.position;

            Gizmos.color = colorDeteccion;
            DibujarCirculo(pos, EffectiveVisionRange, colorDeteccion);

            Gizmos.color = colorAtaque;
            DibujarCirculo(pos, EffectiveAttackRange, colorAtaque);

            // Linea hacia el objetivo trabado (a donde "apunta"/pelea).
            if (target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos + Vector3.up * 1.5f, target.transform.position + Vector3.up * 1.5f);
            }

            // Linea hacia adonde va caminando (orden de movimiento o
            // abordaje de vehiculo en curso).
            if (hasOrder && State == AiState.MovingToOrder)
            {
                Vector3 destino = mountTarget != null ? mountTarget.ClosestBoardingPoint(pos) : orderDestination;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos + Vector3.up * 0.1f, destino + Vector3.up * 0.1f);
                Gizmos.DrawWireSphere(destino, 0.3f);
            }
        }

        // Circulo PLANO (en XZ) de radio dado, aproximado con segmentos --
        // Gizmos.DrawWireSphere dibuja una esfera completa (3 anillos), que
        // en una vista de arriba se lee peor que un solo anillo en el
        // plano del piso.
        static void DibujarCirculo(Vector3 centro, float radio, Color color)
        {
            if (radio <= 0f) return;
            Gizmos.color = color;
            const int segmentos = 32;
            Vector3 anterior = centro + new Vector3(radio, 0f, 0f);
            for (int i = 1; i <= segmentos; i++)
            {
                float ang = (i / (float)segmentos) * Mathf.PI * 2f;
                Vector3 actual = centro + new Vector3(Mathf.Cos(ang) * radio, 0f, Mathf.Sin(ang) * radio);
                Gizmos.DrawLine(anterior, actual);
                anterior = actual;
            }
        }
    }
}
