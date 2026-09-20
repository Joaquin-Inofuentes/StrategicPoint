using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;
using SP.Presentation;

namespace SP.Ai
{
    // Parte "tactica" del cerebro: coberturas (ordenadas por el jugador o
    // buscadas por el propio enemigo en combate), seguir al jugador cuando se
    // aleja, y eleccion DINAMICA del objetivo por distancia y vision.
    public partial class AiBrain
    {
        // ------------------------------------------------------------------
        // Cobertura
        // ------------------------------------------------------------------
        bool enCobertura;          // parado en su cobertura, agachado
        bool yendoACobertura;      // caminando hacia ella
        bool coberturaPorOrden;    // la pidio el jugador (el enemigo la decide solo)
        Vector3 coberturaPunto;
        Collider coberturaDueno;
        float relojVigilancia;
        float relojTactico;

        // El vehiculo al que este soldado esta yendo a subir (o null).
        public SP.Vehicles.Vehicle MountTargetVehicle => mountTarget;

        public bool EnCobertura => enCobertura;
        public bool YendoACobertura => yendoACobertura;
        public bool CoberturaPorOrden => coberturaPorOrden;
        public Vector3 CoberturaPunto => coberturaPunto;

        // Solo los enemigos buscan cobertura por su cuenta en pleno combate.
        bool BuscaCoberturaSolo => self != null && (self.Team == TeamId.Enemy || (Humanizada && Herido && !IsPossessedByPlayer));

        // El jugador manda a este soldado a cubrirse en 'punto': camina, se
        // agacha y se queda ahi hasta recibir otra orden.
        public void IssueCoverOrder(Vector3 punto, Collider dueno)
        {
            if (!bootstrapped) Bootstrap();
            // Una orden de cobertura corta el combate en curso: el soldado
            // obedece (IssueMoveOrder, en combate, solo redirige el camino).
            target = null;
            IssueMoveOrder(punto);   // libera cualquier cobertura previa
            coberturaPunto = punto;
            coberturaDueno = dueno;
            coberturaPorOrden = true;
            yendoACobertura = true;
            enCobertura = false;
        }

        // Toda orden nueva del jugador suelta la cobertura y el "seguir solo".
        // "Todos quietos": no sigue al jugador ni se mueve hasta recibir otra orden.
        bool quieto;
        CombatStance posturaAntesDeQuieto = CombatStance.Libre;
        public bool Quieto
        {
            get => quieto;
            set
            {
                if (value == quieto) return;
                quieto = value;
                if (value)
                {
                    // Plantado: en Libre pasa a Defensiva (no persigue); en otra postura (p. ej. el
                    // alto el fuego del tutorial) se respeta.
                    posturaAntesDeQuieto = Stance;
                    if (Stance == CombatStance.Libre) Stance = CombatStance.Defensiva;
                }
                else Stance = posturaAntesDeQuieto;
            }
        }

        // Civil: no reacciona al combate (ni ve enemigos ni devuelve fuego), solo sigue ordenes.
        public bool Pasivo { get; set; }

        void NuevaOrden()
        {
            LiberarCobertura();
            seguirAuto = false;
            if (Quieto) Quieto = false;
        }

        // Feedback de deteccion: el enemigo que ve a la escuadra marca un "!"
        // rojo sobre su cabeza y suena un aviso (limitado para no saturar).
        static float ultimoAvisoDeteccion = -99f;
        float proximoAvisoPropio;

        void AvisarDeteccion(Soldier detectado)
        {
            if (self.Team != TeamId.Enemy) return;
            if (State == AiState.Chase || State == AiState.Attack) return;
            if (Time.time < proximoAvisoPropio || Time.unscaledTime - ultimoAvisoDeteccion < 0.5f) return;
            proximoAvisoPropio = Time.time + 6f;
            ultimoAvisoDeteccion = Time.unscaledTime;
            Feedback.Accion(SfxKind.EnemySpotted, "!", self.transform.position, Feedback.Bad, aviso: false, pulso: false, volumen: 0.45f);
        }

        void LiberarCobertura()
        {
            bool estaba = enCobertura || yendoACobertura;
            enCobertura = false;
            yendoACobertura = false;
            coberturaPorOrden = false;
            coberturaDueno = null;
            if (estaba && self != null && self.Motor != null) self.Motor.SetCrouching(false);
        }

        // Llegada a la cobertura ordenada.
        void EntrarEnCobertura()
        {
            enCobertura = true;
            yendoACobertura = false;
            hasOrder = false;
            ClearPath();
            SetState(AiState.Idle);
            self.Motor.SetCrouching(true);
            // Mira hacia afuera del obstaculo (hacia donde vendria el enemigo).
            if (coberturaDueno != null)
            {
                var c = coberturaDueno.bounds.center;
                var f = coberturaPunto - new Vector3(c.x, coberturaPunto.y, c.z);
                f.y = 0f;
                if (f.sqrMagnitude > 0.01f) self.Motor.LookTowards(coberturaPunto + f.normalized * 5f, 10f);
            }
            Feedback.Accion(SfxKind.CoverTake, $"{self.DisplayName.ToUpperInvariant()} EN COBERTURA",
                self.transform.position, Feedback.Cover, aviso: false, pulso: true, volumen: 0.5f);
        }

        // Vigila que el obstaculo que lo cubre siga en pie. Barato: cuatro
        // veces por segundo.
        void TickCobertura(float dt)
        {
            if (!enCobertura && !yendoACobertura) return;
            relojVigilancia += dt;
            if (relojVigilancia < 0.25f) return;
            relojVigilancia = 0f;
            if (coberturaDueno == null || Coberturas.Vigente(coberturaDueno)) return;
            PerderCobertura();
        }

        // "Se dio cuenta": la cobertura se destruyo. Busca otra cerca; si no
        // hay, pelea de pie.
        void PerderCobertura()
        {
            bool ordenada = coberturaPorOrden;
            var donde = self.transform.position;
            enCobertura = false;
            yendoACobertura = false;
            coberturaDueno = null;
            Feedback.Accion(SfxKind.CoverLost,
                ordenada ? $"¡{self.DisplayName.ToUpperInvariant()}: COBERTURA DESTRUIDA!" : "¡COBERTURA DESTRUIDA!",
                donde, Feedback.Warn, aviso: ordenada, pulso: false, volumen: 0.6f);
            self.Motor.SetCrouching(false);

            if (Coberturas.TryCercano(donde, 14f, out var nuevo, out var dueno) && (nuevo - donde).sqrMagnitude > 0.5f)
            {
                if (ordenada) IssueCoverOrder(nuevo, dueno);
                else
                {
                    coberturaPunto = nuevo;
                    coberturaDueno = dueno;
                    yendoACobertura = true;
                    PlanPathTo(nuevo);
                }
            }
            else coberturaPorOrden = false;
        }

        // Enemigo en combate: va a una cobertura desde la que pueda seguir
        // disparando, se agacha y dispara. Devuelve true mientras esta
        // CORRIENDO hacia ella (ese tick no dispara).
        bool TickCoberturaTactica(float dt)
        {
            if (!BuscaCoberturaSolo || target == null) return false;

            if (yendoACobertura)
            {
                self.Motor.SetCrouching(false);
                if (FuegoDeCoberturaHerido(dt)) return true;
                if (AdvanceTo(coberturaPunto, 0.6f, dt)) EntrarEnCoberturaTactica();
                else return true;
            }

            if (enCobertura) return false;

            relojTactico -= dt;
            if (relojTactico > 0f) return false;
            relojTactico = 1.2f + (self.Id % 5) * 0.15f;

            float dActual = Vector3.Distance(self.transform.position, target.transform.position);
            if (!Coberturas.TryCoberturaDeTiro(self.transform.position, target, self, 12f,
                    EffectiveAttackRange * 0.45f, EffectiveAttackRange * 0.9f, out var punto, out var dueno)) return false;
            // Solo si de verdad cambia algo: no correr 1 m, ni alejarse.
            if ((punto - self.transform.position).sqrMagnitude < 1.5f * 1.5f) return false;
            if (Vector3.Distance(punto, target.transform.position) > dActual + 2f) return false;

            coberturaPunto = punto;
            coberturaDueno = dueno;
            coberturaPorOrden = false;
            yendoACobertura = true;
            PlanPathTo(punto);
            Feedback.Accion(SfxKind.EnemySpotted, "¡A CUBIERTO!", self.transform.position, Feedback.Bad, aviso: false, pulso: false, volumen: 0.3f);
            return true;
        }

        void EntrarEnCoberturaTactica()
        {
            enCobertura = true;
            yendoACobertura = false;
            self.Motor.SetCrouching(true);
        }

        // ------------------------------------------------------------------
        // Seguir al jugador cuando se aleja
        // ------------------------------------------------------------------
        bool seguirAuto;
        static float ultimoAvisoDeSeguir = -99f;

        public bool SiguiendoAlJugador => seguirAuto;

        void TickSeguirAlJugador()
        {
            if (self.Team != TeamId.Player || Quieto) return;
            var lider = AjustesDeEscuadra.Lider;
            if (lider == null || lider == self || lider.Health == null || !lider.Health.IsAlive || !lider.gameObject.activeInHierarchy)
            {
                if (seguirAuto) DejarDeSeguirAuto();
                return;
            }

            var d = lider.transform.position - self.transform.position;
            d.y = 0f;
            float dist = d.magnitude;

            if (seguirAuto)
            {
                bool enCombate = State == AiState.Chase || State == AiState.Attack || State == AiState.MovingToAttackOrder;
                if (State != AiState.Follow && !enCombate) { seguirAuto = false; return; }
                if (State == AiState.Follow && dist <= AjustesDeEscuadra.DistanciaParaDetenerse) DejarDeSeguirAuto();
                return;
            }

            bool libre = (State == AiState.Idle || State == AiState.Patrol) && target == null && !hasOrder && mountTarget == null;
            if (!libre || yendoACobertura) return;
            if (enCobertura && !AjustesDeEscuadra.SeguirDesdeCobertura) return;
            if (dist <= AjustesDeEscuadra.DistanciaParaSeguir) return;

            ComenzarSeguirAlJugador(lider);
        }

        void ComenzarSeguirAlJugador(Soldier lider)
        {
            bool deCobertura = enCobertura;
            LiberarCobertura();
            target = null;
            hasOrder = true;
            orderIsAttack = false;
            mountTarget = null;
            orderQueue.Clear();
            orderDestination = self.transform.position;   // si el combate lo desvia, no vuelve a un punto viejo
            ClearPath();
            followTarget = lider;
            // Ranura detras del lider, repartida por Id para que no se apilen.
            followOffsetLocal = new Vector3(((self.Id % 3) - 1) * 2.2f, 0f, -2.5f - (self.Id % 2) * 1.5f);
            seguirAuto = true;
            SetState(AiState.Follow);
            forceSense = true;

            if (Time.unscaledTime - ultimoAvisoDeSeguir > 4f)
            {
                ultimoAvisoDeSeguir = Time.unscaledTime;
                Feedback.Accion(SfxKind.FollowCall, deCobertura ? "ALIADOS TE SIGUEN (DEJAN LA COBERTURA)" : "ALIADOS TE SIGUEN",
                    self.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.5f);
            }
        }

        void DejarDeSeguirAuto()
        {
            seguirAuto = false;
            if (State != AiState.Follow) return;
            hasOrder = false;
            followTarget = null;
            followOffsetLocal = Vector3.zero;
            ClearPath();
            SetState(AiState.Idle);
        }

        // ------------------------------------------------------------------
        // Objetivo dinamico: distancia + vision
        // ------------------------------------------------------------------
        // Antes: "el enemigo mas cercano en el radio de vision", y una vez
        // elegido se lo perseguia hasta perderlo. Ahora se pondera:
        //   - distancia (mas cerca = mejor)
        //   - vision: dentro del radio de vision se lo "siente" (como
        //     siempre); mas alla, hasta 1,6x, solo si esta DENTRO DEL CONO
        //     frontal y con LINEA DE TIRO
        //   - sin linea de tiro pesa mas (mejor uno que se ve que uno
        //     detras de una pared)
        //   - quien acaba de dispararme pesa mas; el herido, un poco mas
        //   - el objetivo actual tiene ventaja (histeresis): no salta de uno
        //     a otro por diferencias minimas
        public const float AlcanceExtendido = 1.6f;
        public const float SemiconoDeVision = 100f;
        readonly List<Soldier> candidatos = new List<Soldier>(16);
        int ultimoAtacanteId = -1;
        float tiempoUltimoAtaque = -99f;
        float relojRetarget;

        public float PuntajeDeObjetivo(Soldier s)
        {
            if (s == null || self == null) return float.MaxValue;
            var pos = self.transform.position;
            float dist = Vector3.Distance(pos, s.transform.position);
            bool linea = TieneLineaDeTiro(s);
            float p = dist;
            if (!linea) p += 12f;
            if (s.Id == ultimoAtacanteId && Time.time - tiempoUltimoAtaque < 4f) p -= 10f;
            if (s.Health != null && s.Health.MaxHealth > 0 && s.Health.Current < s.Health.MaxHealth * 0.4f) p -= 3f;
            if (s == target) p -= 5f;
            return p;
        }

        bool EnElCono(Soldier s)
        {
            var d = s.transform.position - self.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(self.transform.forward, d) <= SemiconoDeVision;
        }

        Soldier MejorObjetivoVisible()
        {
            float vision = EffectiveVisionRange;
            var equipo = self.Team;
            SpatialGrid.QueryInRange(self.transform.position, vision * AlcanceExtendido, candidatos,
                s => s.Health != null && s.Health.IsAlive && s.Team != equipo);

            Soldier mejor = null;
            float mejorPuntaje = float.MaxValue;
            for (int i = 0; i < candidatos.Count; i++)
            {
                var s = candidatos[i];
                float dist = Vector3.Distance(self.transform.position, s.transform.position);
                if (dist > vision)
                {
                    // Vision extendida: cono + linea de tiro.
                    if (!EnElCono(s) || !TieneLineaDeTiro(s)) continue;
                }
                float p = PuntajeDeObjetivo(s);
                if (p < mejorPuntaje) { mejorPuntaje = p; mejor = s; }
            }
            return mejor;
        }

        // En pleno combate: cada 0.5 s se revisa si aparecio un blanco
        // claramente mejor que el actual (una orden de atacar explicita no se
        // cambia).
        void TickRetarget(float dt)
        {
            if (orderIsAttack || target == null) return;
            if (State != AiState.Chase && State != AiState.Attack) return;
            relojRetarget += dt;
            if (relojRetarget < 0.5f) return;
            relojRetarget = 0f;

            var mejor = MejorObjetivoVisible();
            if (mejor == null || mejor == target) return;
            if (PuntajeDeObjetivo(mejor) + 4f >= PuntajeDeObjetivo(target)) return;
            target = mejor;
            segundosSinLineaDeTiro = 0f;
        }
    }
}
