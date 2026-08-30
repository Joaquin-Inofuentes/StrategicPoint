using UnityEngine;
using SP.Core;
using SP.Combat;

namespace SP.Vehicles
{
    // Arma montada en el vehículo: gira sola (no con el chasis) y dispara
    // proyectiles del mismo pool que las armas de mano.
    public class TurretWeapon : MonoBehaviour
    {
        [SerializeField] float fireCooldown = 0.5f;
        [SerializeField] int damage = 45;
        [SerializeField] Color projectileColor = new Color(0.9f, 0.35f, 0.1f);
        [SerializeField] ProjectilePool pool;
        // Radio de la granada: 0 desactivaría la explosión: siempre tiene
        // zona de daño, pedido explícito ("q el proyectil tenga zona de
        // explosion").
        [SerializeField] float explosionRadius = 3f;
        // Grados por segundo: "que rote lento, que demore en tener la
        // mira en el cursor" -- antes giraba lo que el mouse moviera, ya
        // (instantáneo). Ahora persigue un ángulo objetivo con velocidad
        // limitada, así se nota el retraso.
        [SerializeField] float turnSpeedDegPerSec = 50f;
        public Transform Muzzle;

        float cooldownTimer;
        Vehicle vehicle;
        bool bootstrapped;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            vehicle = GetComponentInParent<Vehicle>();
        }

        public void SetPool(ProjectilePool projectilePool) => pool = projectilePool;

        public void RotateYaw(float yawDelta) => transform.Rotate(Vector3.up, yawDelta, Space.World);

        // Apunta hacia un punto del mundo con velocidad de giro limitada
        // (turnSpeedDegPerSec): el cañón persigue el ángulo objetivo en
        // vez de saltar directo a él, para que se note que "le cuesta"
        // seguir el blanco -- usado por el auto-apuntado en batalla.
        public void AimAt(Vector3 worldPoint, float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (vehicle != null && vehicle.IsDestroyed) return;
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            float desiredYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            float currentYaw = transform.eulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, turnSpeedDegPerSec * dt);
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        }

        // Qué tan cerca está el cañón de apuntar realmente a ese punto --
        // para no disparar mientras todavía está girando hacia el blanco.
        public bool IsAimedAt(Vector3 worldPoint, float toleranceDeg = 4f)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            float desiredYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            return Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw)) <= toleranceDeg;
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (cooldownTimer > 0f) cooldownTimer -= dt;
        }

        public bool TryFire()
        {
            if (!bootstrapped) Bootstrap();
            // Un tanque destruido no debería poder seguir disparando --
            // ojo que Tick()/TryFire() se llaman por método directo desde
            // WorldSimulationDriver, no por el Update() automático de
            // Unity, así que "enabled=false" solo no alcanza para
            // frenarlo: hay que chequear el estado real acá.
            if (vehicle != null && vehicle.IsDestroyed) return false;
            if (cooldownTimer > 0f || pool == null) return false;

            int shooterId = vehicle != null && vehicle.Gunner != null ? vehicle.Gunner.Id : -1;
            var team = vehicle != null && vehicle.Gunner != null ? vehicle.Gunner.Team : TeamId.Player;

            var spawnPos = Muzzle != null ? Muzzle.position : transform.position;
            pool.Spawn(spawnPos, transform.forward, shooterId, team, damage, projectileColor, explosionRadius);
            cooldownTimer = fireCooldown;
            return true;
        }
    }
}
