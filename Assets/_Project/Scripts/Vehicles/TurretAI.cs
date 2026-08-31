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
        Soldier target;
        float retargetTimer;
        bool bootstrapped;

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
            // Si hay un artillero de carne y hueso adentro, la IA se
            // aparta: el mouse del jugador manda.
            bool hasHumanGunner = vehicle != null && vehicle.Gunner != null;
            PublishControlChange(!hasHumanGunner);
            if (hasHumanGunner) return;

            retargetTimer -= dt;
            if (retargetTimer <= 0f || target == null || !target.Health.IsAlive)
            {
                retargetTimer = retargetInterval;
                target = ActorRegistry.FindNearestEnemyInRange(transform.position, TeamId.Player, range);
            }

            if (target == null || !target.Health.IsAlive) return;

            var aimPoint = target.transform.position;
            turret.AimAt(aimPoint, dt);
            if (turret.IsAimedAt(aimPoint)) turret.TryFire();
        }
    }
}
