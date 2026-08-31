using UnityEngine;
using SP.Core;

namespace SP.Combat
{
    // Dueño del pool de proyectiles. Cero Instantiate en combate: todo pasa por acá.
    // El pool en sí (ObjectPool<T>) es estado de runtime que no sobrevive a un
    // domain reload; se reconstruye solo en Awake a partir de campos serializados.
    public class ProjectilePool : MonoBehaviour
    {
        [SerializeField] Projectile prefab;
        [SerializeField] int prewarm = 24;

        ObjectPool<Projectile> pool;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (pool != null) return;
            if (prefab == null) return;
            pool = new ObjectPool<Projectile>(prefab, prewarm, transform);
        }

        public void Configure(Projectile projectilePrefab, int prewarmCount)
        {
            prefab = projectilePrefab;
            prewarm = prewarmCount;
            pool = null;
            Bootstrap();
        }

        public Projectile Spawn(Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int damage, Color? color = null, float explosionRadius = 0f, float gravity = 0f, SP.Vehicles.Vehicle sourceVehicle = null)
        {
            if (pool == null) Bootstrap();
            var p = pool.Get();
            p.Configure(this, position, direction, shooterId, shooterTeam, damage, color, explosionRadius, gravity, sourceVehicle);
            return p;
        }

        public void Release(Projectile p) => pool?.Release(p);

        public int FreeCount => pool?.FreeCount ?? 0;
    }
}
