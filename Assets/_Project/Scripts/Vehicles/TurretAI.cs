using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;

namespace SP.Vehicles
{
    // Apuntado automático de la torreta cuando no hay un artillero humano
    // adentro: busca al enemigo vivo más cercano en rango, gira el cañón
    // hacia él (con el mismo giro lento de TurretWeapon.AimAt) y dispara
    // apenas queda bien apuntado. Así el tanque también participa de la
    // batalla sin que el jugador tenga que estar manejando la torreta.
    public class TurretAI : MonoBehaviour
    {
        [SerializeField] float range = 40f;
        [SerializeField] float retargetInterval = 0.4f;

        TurretWeapon turret;
        Vehicle vehicle;
        VehicleMotor motor;
        Soldier target;
        float retargetTimer;
        bool bootstrapped;

        // Liderado de blanco: antes se apuntaba siempre a la posicion
        // ACTUAL del enemigo, sin importar que tan rapido se moviera --
        // contra un blanco corriendo, el tiro (que tarda en llegar, no es
        // instantaneo) sistematicamente caia atras. Se estima la
        // velocidad por diferencia de posicion entre ticks (no hay
        // Rigidbody: el soldado se mueve por transform directo) y se
        // apunta a donde va a ESTAR cuando el proyectil llegue, no a
        // donde esta ahora.
        Vector3 lastTargetPos;
        bool hasLastTargetPos;

        // VehicleBrain lo consulta para el otro lado de la misma regla:
        // con un solo tripulante, mientras la torreta esta trabada en un
        // blanco no hay que arrancar a manejar sola hacia una orden vieja
        // -- esa unica persona no puede estar disparando Y conduciendo a
        // la vez.
        public bool IsEngaging => target != null && target.Health.IsAlive;

        void Awake() => Bootstrap();

        // Publico e idempotente, igual que VehicleBrain/TurretWeapon: Awake
        // NO corre al hacer AddComponent en Edit mode (esta clase no tiene
        // [ExecuteAlways]), asi que WorldSystemsRegistry.EnsurePopulated
        // necesita poder llamar esto a mano durante la construccion de la
        // escena en la suite headless. Sin esto quedaba "registrado" con
        // turret/vehicle en null, y Tick() haria un no-op silencioso para
        // siempre por la guarda de la linea de abajo.
        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            turret = GetComponent<TurretWeapon>();
            vehicle = GetComponentInParent<Vehicle>();
            if (vehicle != null) motor = vehicle.GetComponent<VehicleMotor>();
            WorldSystemsRegistry.Register(this);
        }

        void OnDestroy() => WorldSystemsRegistry.Unregister(this);

        // Antes el traspaso de control era invisible: el jugador veia la
        // torreta moverse sola sin saber por que (o la veia quieta
        // esperando que la IA la usara). Se publica en los dos sentidos.
        bool? lastAiInControl;

        void PublishControlChange(bool aiInControl)
        {
            if (lastAiInControl == aiInControl) return;
            lastAiInControl = aiInControl;
            EventBus.Instance.Publish(new TurretControlChangedEvent(vehicle, aiInControl));
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (turret == null) return;

            // BUG REAL: esto disparaba solo, sin nadie a bordo -- un
            // tanque vacio (sin conductor, artillero ni pasajero, de
            // ningun equipo) se comportaba como una torreta automatica
            // hostil a los enemigos del jugador aunque no lo tripulara
            // nadie. Sin tripulacion, ningun equipo lo controla: no
            // apunta, no dispara, y se limpia el blanco que tuviera para
            // no quedarselo apuntando de un tick al otro cuando alguien
            // suba despues.
            if (vehicle == null || vehicle.OccupantCount == 0)
            {
                PublishControlChange(false);
                target = null;
                return;
            }

            // Si hay un artillero de carne y hueso adentro, la IA se
            // aparta: el mouse de quien lo maneje manda (jugador o, si
            // algun dia hay un enemigo humano-controlado, ese enemigo).
            bool hasHumanGunner = vehicle.Gunner != null;
            PublishControlChange(!hasHumanGunner);
            if (hasHumanGunner)
            {
                target = null;
                return;
            }

            // Pedido explicito: con UN solo tripulante (a bordo, de
            // cualquier equipo) esa persona maneja O dispara, nunca las
            // dos cosas a la vez -- no puede estar conduciendo Y
            // operando la torreta al mismo tiempo. Con dos o mas adentro
            // esto no aplica (uno bien puede manejar mientras el otro
            // tira). Se corta ANTES de retargetear: si ya tenia un
            // blanco trabado, arrancar a andar se lo hace soltar en vez
            // de dispararle igual mientras el chasis se mueve.
            if (vehicle.OccupantCount == 1 && motor != null && !motor.IsStopped)
            {
                target = null;
                return;
            }

            // A quien apunta la IA depende de QUIEN esta adentro, no de
            // que el tanque sea "siempre del jugador": la tripulacion
            // puede ser propia o enemiga (un vehiculo capturado), y tiene
            // que dispararle al equipo CONTRARIO al de su propia gente,
            // nunca a la propia -- amigo o enemigo lo decide la
            // tripulacion real, no una constante fija.
            var crewTeam = vehicle.Occupants[0].Team;
            var enemyTeam = crewTeam == TeamId.Player ? TeamId.Enemy : TeamId.Player;

            var previousTarget = target;
            retargetTimer -= dt;
            if (retargetTimer <= 0f || target == null || !target.Health.IsAlive || target.Team != enemyTeam)
            {
                retargetTimer = retargetInterval;
                target = ActorRegistry.FindNearestEnemyInRange(transform.position, crewTeam, range);
            }
            // Un blanco nuevo no tiene una posicion previa comparable: sin
            // este corte, la "velocidad" saldria de restar la posicion del
            // enemigo VIEJO contra la del nuevo -- un salto enorme y sin
            // sentido que mandaria el primer tiro a cualquier lado.
            if (target != previousTarget) hasLastTargetPos = false;

            if (target == null || !target.Health.IsAlive) return;

            var aimPoint = ComputeLeadAimPoint(target, dt);
            turret.AimAt(aimPoint, dt);
            // Mismo motivo que el gate de AiBrain: sin linea de tiro no se
            // gatilla. Antes la torreta elegia al enemigo mas cercano en 40
            // metros y disparaba, hubiera o no una pared en el medio --
            // invisible mientras los proyectiles atravesaban el escenario,
            // pero ahora seria el tanque bombardeando la barricada que
            // tiene adelante mientras el enemigo mira.
            if (turret.IsAimedAt(aimPoint) && HayLineaDeTiro(aimPoint)) turret.TryFire();
        }

        Vector3 ComputeLeadAimPoint(Soldier t, float dt)
        {
            var pos = t.transform.position;
            Vector3 velocity = Vector3.zero;
            if (hasLastTargetPos && dt > 0.0001f) velocity = (pos - lastTargetPos) / dt;
            lastTargetPos = pos;
            hasLastTargetPos = true;

            float dist = Vector3.Distance(transform.position, pos);
            float projectileSpeed = TurretWeapon.ProjectileSpeed * TurretWeapon.SpeedMultiplier;
            float leadTime = projectileSpeed > 0.01f ? dist / projectileSpeed : 0f;
            // Tope de 1.5s: mas alla de eso la prediccion lineal (un
            // blanco puede girar o frenar en el medio) ya acumula mas
            // error del que corrige, y liderar de mas es peor que no
            // liderar.
            leadTime = Mathf.Min(leadTime, 1.5f);
            return pos + velocity * leadTime;
        }

        static readonly RaycastHit[] BufferVision = new RaycastHit[8];

        // Rayo desde la boca del cañon (o desde la torreta si no hay boca)
        // hasta el punto al que se apunta, con la MISMA definicion de pared
        // que usan SoldierMotor, Projectile y AiBrain.
        bool HayLineaDeTiro(Vector3 punto)
        {
            var desde = turret != null && turret.Muzzle != null
                ? turret.Muzzle.position
                : transform.position;

            var delta = punto - desde;
            float dist = delta.magnitude;
            if (dist < 0.0001f) return true;

            int n = Physics.RaycastNonAlloc(desde, delta / dist, BufferVision, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = BufferVision[i].collider;
                if (c == null) continue;
                // El propio vehiculo no se tapa a si mismo.
                if (c.transform.IsChildOf(transform.root)) continue;
                if (!SP.Core.NavService.BlocksMovement(c)) continue;
                return false;
            }
            return true;
        }
    }
}
