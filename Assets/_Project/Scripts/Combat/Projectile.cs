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
        // 0 = proyectil normal (solo le pega a lo que toca). >0 = granada
        // de tanque: al impactar, reparte daño a todo lo que esté en este
        // radio y dibuja la esfera de explosión (ImpactFx.SpawnExplosion)
        // en vez del impacto chico normal.
        float explosionRadius;

        const float RestScale = 0.2f; // debe coincidir con la escala del prefab (BuildAndSaveProjectilePrefab)
        const float TraceStretch = 2.75f;

        // Instancias actualmente en vuelo. Permite avanzar la simulación
        // manualmente (tests) sin depender del bucle de Update de Unity.
        public static readonly List<Projectile> ActiveInstances = new List<Projectile>();

        public void Configure(ProjectilePool owningPool, Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int dmg, Color? color = null, float explosionRadiusValue = 0f)
        {
            pool = owningPool;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward);
            // Trazadora: a la velocidad real del proyectil, una esfera de
            // 0.2 unidades es practicamente invisible a simple vista. Se
            // estira en Z local (que ya coincide con la direccion de
            // vuelo por el LookRotation de arriba) para que se lea la
            // trayectoria, no solo el punto de impacto.
            transform.localScale = new Vector3(RestScale, RestScale, RestScale * TraceStretch);
            ownerId = shooterId;
            ownerTeam = shooterTeam;
            damage = dmg;
            explosionRadius = explosionRadiusValue;

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
            // Vuelve a escala de reposo uniforme: sin esto, la proxima vez
            // que el pool reutilice este mismo objeto para un impacto (no
            // un vuelo), o si algo lo inspecciona en el pool, quedaria
            // con el estiramiento de la trazadora del disparo anterior.
            transform.localScale = Vector3.one * RestScale;
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
                if (explosionRadius > 0f) Explode(transform.position);
                else
                {
                    hit.Health.TakeDamage(damage, ownerId);
                    ImpactFx.Spawn(transform.position, ImpactFx.EnemyColor);
                }
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
                    if (explosionRadius > 0f) Explode(transform.position);
                    else
                    {
                        vehicle.TakeDamage(damage, ownerId);
                        EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Vehicle, transform.position));
                        ImpactFx.Spawn(transform.position, ImpactFx.VehicleColor);
                    }
                    Expire();
                    return;
                }
            }

            foreach (var obstacle in Object.FindObjectsByType<ObstacleMarker>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(obstacle.transform.position, transform.position) <= hitRadius + 1f)
                {
                    if (explosionRadius > 0f) Explode(transform.position);
                    else
                    {
                        EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Obstacle, transform.position));
                        ImpactFx.Spawn(transform.position, ImpactFx.ObstacleColor);
                    }
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
                if (explosionRadius > 0f) Explode(transform.position);
                else
                {
                    EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Ground, transform.position));
                    ImpactFx.Spawn(transform.position, ImpactFx.GroundColor);
                }
                Expire();
                return;
            }

            if (age >= lifetime) Expire();
        }

        // Granada del tanque: reparte daño a todo lo que esté adentro del
        // radio (soldados enemigos Y vehículos), y dibuja la esfera de
        // explosión que pide el jugador -- crece rápido y se achica de
        // golpe, representando visualmente la zona de daño real.
        void Explode(Vector3 point)
        {
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.Health.IsAlive || s.Team == ownerTeam || !s.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(s.transform.position, point) <= explosionRadius)
                    s.Health.TakeDamage(damage, ownerId);
            }

            foreach (var vehicle in Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(vehicle.transform.position, point) <= explosionRadius)
                    vehicle.TakeDamage(damage, ownerId);
            }

            EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Ground, point));
            ImpactFx.SpawnExplosion(point, explosionRadius);
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
