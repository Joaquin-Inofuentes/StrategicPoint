using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Combat
{
    // Viaja, comprueba su propio impacto por distancia (sin física) y
    // se devuelve solo al pool. Nunca lo instancia nadie salvo el pool.
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] float speed = 40f;
        [SerializeField] float lifetime = 3f;
        [SerializeField] float hitRadius = 1f;
        [SerializeField] float groundImpactHeight = 0.15f;

        static int nextInstanceId = 1;

        int damage;
        int ownerId;
        TeamId ownerTeam;
        float age;
        bool active;
        ProjectilePool pool;
        int instanceId;
        Renderer cachedRenderer;
        bool ownMaterialReady;

        // Instancias actualmente en vuelo. Permite avanzar la simulación
        // manualmente (tests) sin depender del bucle de Update de Unity.
        public static readonly List<Projectile> ActiveInstances = new List<Projectile>();

        public void Configure(ProjectilePool owningPool, Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int dmg, Color? color = null)
        {
            pool = owningPool;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward);
            ownerId = shooterId;
            ownerTeam = shooterTeam;
            damage = dmg;

            if (color.HasValue)
            {
                if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
                if (cachedRenderer == null)
                {
                    Debug.LogWarning($"[Projectile] {name}: no tiene Renderer, no se puede pintar.");
                }
                else if (!ownMaterialReady)
                {
                    var baseMat = cachedRenderer.sharedMaterial;
                    var freshMat = baseMat != null
                        ? new Material(baseMat)
                        : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    cachedRenderer.sharedMaterial = freshMat;
                    ownMaterialReady = true;
                    cachedRenderer.sharedMaterial.color = color.Value;
                }
                else
                {
                    cachedRenderer.sharedMaterial.color = color.Value;
                }
            }
        }

        public void OnSpawn()
        {
            age = 0f;
            active = true;
            instanceId = nextInstanceId++;
            if (!ActiveInstances.Contains(this)) ActiveInstances.Add(this);
        }

        public void OnDespawn()
        {
            active = false;
            ActiveInstances.Remove(this);
        }

        void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (!active) return;

            transform.position += transform.forward * speed * dt;
            age += dt;

            // Un soldado montado en un vehículo queda inactivo (oculto); no
            // debe poder recibir impactos mientras está ahí adentro.
            var hit = ActorRegistry.FindNearest(transform.position, s =>
                s.Health.IsAlive &&
                s.Team != ownerTeam &&
                s.gameObject.activeInHierarchy &&
                Vector3.Distance(s.transform.position, transform.position) <= hitRadius);

            if (hit != null)
            {
                hit.Health.TakeDamage(damage, ownerId);
                ImpactFx.Spawn(transform.position, ImpactFx.EnemyColor);
                Expire();
                return;
            }

            // No hay soldado en el camino: probamos vehículo y obstáculo,
            // para que el jugador tenga feedback de qué le pegó a qué (antes
            // el proyectil los atravesaba sin avisar nada).
            foreach (var vehicle in Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(vehicle.transform.position, transform.position) <= hitRadius + 1.5f)
                {
                    vehicle.TakeDamage(damage, ownerId);
                    EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Vehicle, transform.position));
                    ImpactFx.Spawn(transform.position, ImpactFx.VehicleColor);
                    Expire();
                    return;
                }
            }

            foreach (var obstacle in Object.FindObjectsByType<ObstacleMarker>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(obstacle.transform.position, transform.position) <= hitRadius + 1f)
                {
                    EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Obstacle, transform.position));
                    ImpactFx.Spawn(transform.position, ImpactFx.ObstacleColor);
                    Expire();
                    return;
                }
            }

            // Suelo: el proyectil no tiene gravedad (viaja recto), así que
            // esto solo dispara si se apunta hacia abajo o desde baja
            // altura -- pero cuando pasa, tiene que avisar igual que
            // cualquier otro impacto (antes lo atravesaba sin más).
            if (transform.position.y <= groundImpactHeight)
            {
                EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Ground, transform.position));
                ImpactFx.Spawn(transform.position, ImpactFx.GroundColor);
                Expire();
                return;
            }

            if (age >= lifetime) Expire();
        }

        void Expire()
        {
            if (!active) return;
            active = false;
            EventBus.Instance.Publish(new ProjectileReturnedEvent(instanceId));
            pool?.Release(this);
        }
    }
}
