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

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (cooldownTimer > 0f) cooldownTimer -= dt;
        }

        public bool TryFire()
        {
            if (!bootstrapped) Bootstrap();
            if (cooldownTimer > 0f || pool == null) return false;

            int shooterId = vehicle != null && vehicle.Gunner != null ? vehicle.Gunner.Id : -1;
            var team = vehicle != null && vehicle.Gunner != null ? vehicle.Gunner.Team : TeamId.Player;

            var spawnPos = Muzzle != null ? Muzzle.position : transform.position;
            pool.Spawn(spawnPos, transform.forward, shooterId, team, damage, projectileColor);
            cooldownTimer = fireCooldown;
            return true;
        }
    }
}
