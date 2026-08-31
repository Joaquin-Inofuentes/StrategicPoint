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

        // Caida por gravedad. 0 = trayectoria recta (armas de mano: el
        // proyectil viaja tan poco tiempo que un arco no aportaria nada y
        // volveria impredecible el tiro a quemarropa). >0 = arco de
        // tanque, que es lo que le da al artillero una habilidad propia
        // en vez de apuntar y listo.
        float gravity;
        Vector3 velocity;

        // BUG REAL encontrado al testear la balistica: el proyectil de la
        // torreta nace en la boca del cañon, que esta a menos de 2.5 m del
        // centro del chasis -- justo dentro del radio con el que el propio
        // proyectil detecta vehiculos. El tanque se pegaba un tiro a si
        // mismo en el PRIMER tick de cada disparo, sin salir nunca. Por eso
        // el vehiculo aparecia destruido una y otra vez en las pruebas y yo
        // lo atribuia a los enemigos.
        SP.Vehicles.Vehicle ignoreVehicle;
        public float Gravity => gravity;
        public Vector3 Velocity => velocity;

        const float RestScale = 0.2f; // debe coincidir con la escala del prefab (BuildAndSaveProjectilePrefab)
        const float TraceStretch = 2.75f;

        // Instancias actualmente en vuelo. Permite avanzar la simulación
        // manualmente (tests) sin depender del bucle de Update de Unity.
        public static readonly List<Projectile> ActiveInstances = new List<Projectile>();

        public void Configure(ProjectilePool owningPool, Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int dmg, Color? color = null, float explosionRadiusValue = 0f, float gravityValue = 0f, SP.Vehicles.Vehicle sourceVehicle = null)
        {
            gravity = gravityValue;
            ignoreVehicle = sourceVehicle;
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
            velocity = (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward) * speed;

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
            gravity = 0f;
            ignoreVehicle = null;
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

            if (gravity != 0f)
            {
                // Integracion exacta para aceleracion constante (velocity
                // Verlet): pos += v*dt + 0.5*a*dt^2. Con el Euler simple
                // de antes, la trayectoria real dependia del tamaño del
                // paso, asi que la marca de impacto previsto no podia
                // coincidir salvo que simulara con EXACTAMENTE el mismo dt
                // -- que es variable. Asi las dos describen la misma
                // parabola sin importar los fps.
                transform.position += velocity * dt + new Vector3(0f, -0.5f * gravity * dt * dt, 0f);
                velocity.y -= gravity * dt;
                // La trazadora tiene que seguir mirando hacia donde va
                // realmente, no hacia donde salio: si no, el arco se ve
                // como un proyectil recto desplazandose de costado.
                if (velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(velocity.normalized);
            }
            else
            {
                transform.position += transform.forward * speed * dt;
            }
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
                    ImpactFx.SpawnScaledByDamage(transform.position, ImpactFx.EnemyColor, damage);
                }
                Expire();
                return;
            }

            // No hay soldado en el camino: probamos vehículo y obstáculo,
            // para que el jugador tenga feedback de qué le pegó a qué (antes
            // el proyectil los atravesaba sin avisar nada).
            // WorldSystemsRegistry en vez de FindObjectsByType: esto corria
            // POR PROYECTIL y POR FRAME, o sea un barrido completo de la
            // escena por cada bala en vuelo. Ademas se toma el vehiculo MAS
            // CERCANO en rango y no "el primero que aparezca": el orden de
            // FindObjectsByType era arbitrario, asi que con dos vehiculos
            // pegados era impredecible a cual le pegaba.
            Vehicle nearestVehicle = null;
            float nearestVehicleDist = float.MaxValue;
            var vehicleList = SP.Core.WorldSystemsRegistry.Vehicles;
            for (int i = 0; i < vehicleList.Count; i++)
            {
                var v = vehicleList[i];
                if (v == null || v == ignoreVehicle) continue;
                float d = Vector3.Distance(v.transform.position, transform.position);
                if (d <= hitRadius + 1.5f && d < nearestVehicleDist) { nearestVehicleDist = d; nearestVehicle = v; }
            }
            {
                var vehicle = nearestVehicle;
                if (vehicle != null)
                {
                    if (explosionRadius > 0f) Explode(transform.position);
                    else
                    {
                        vehicle.TakeDamage(damage, ownerId);
                        EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Vehicle, transform.position));
                        // Blindaje: chispas metalicas rebotadas, no el
                        // mismo polvo generico que contra el suelo.
                        var awayFromHull = (transform.position - vehicle.transform.position).normalized;
                        ImpactFx.SpawnArmorSparks(transform.position, awayFromHull);
                    }
                    Expire();
                    return;
                }
            }

            // Mismo caso que los vehiculos: barrido por proyectil por
            // frame, reemplazado por el registro y el mas cercano en rango.
            ObstacleMarker nearestObstacle = null;
            float nearestObstacleDist = float.MaxValue;
            var obstacleList = SP.Core.WorldSystemsRegistry.Obstacles;
            for (int i = 0; i < obstacleList.Count; i++)
            {
                var o = obstacleList[i];
                if (o == null) continue;
                float d = Vector3.Distance(o.transform.position, transform.position);
                if (d <= hitRadius + 1f && d < nearestObstacleDist) { nearestObstacleDist = d; nearestObstacle = o; }
            }
            {
                var obstacle = nearestObstacle;
                if (obstacle != null)
                {
                    if (explosionRadius > 0f) Explode(transform.position);
                    else
                    {
                        // Los obstaculos eran inmortales: disparar contra
                        // la cobertura no cambiaba nada.
                        obstacle.TakeDamage(damage);
                        EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Obstacle, transform.position));
                        ImpactFx.SpawnScaledByDamage(transform.position, ImpactFx.ObstacleColor, damage);
                        var awayFromWall = (transform.position - obstacle.transform.position).normalized;
                        DecalPool.Spawn(DecalKind.BulletHole, transform.position, awayFromWall, 0.22f);
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
                    ImpactFx.SpawnScaledByDamage(transform.position, ImpactFx.GroundColor, damage);
                    DecalPool.Spawn(DecalKind.BulletHole, new Vector3(transform.position.x, 0.02f, transform.position.z), Vector3.up, 0.25f);
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
                float dist = Vector3.Distance(s.transform.position, point);
                if (dist > explosionRadius) continue;
                s.Health.TakeDamage(damage, ownerId);

                // Antes el daño en area no movia a nadie: una granada se
                // veia igual que un disparo puntual. El empuje es
                // proporcional a la cercania al centro y no toca el
                // estado de la IA -- el soldado sigue bajo su control
                // normal en el frame siguiente, solo cambio de lugar.
                Vector3 away = s.transform.position - point;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitSphere;
                float strength = 1f - Mathf.Clamp01(dist / explosionRadius);
                s.transform.position += away.normalized * strength * 2.2f;
            }

            // Tercer barrido por frame que tambien se va: la explosion si
            // reparte a TODOS los vehiculos en radio, asi que aca el orden
            // no importa y la semantica queda identica.
            var explosionVehicles = SP.Core.WorldSystemsRegistry.Vehicles;
            for (int i = 0; i < explosionVehicles.Count; i++)
            {
                var vehicle = explosionVehicles[i];
                if (vehicle == null || vehicle == ignoreVehicle) continue;
                if (Vector3.Distance(vehicle.transform.position, point) <= explosionRadius)
                    vehicle.TakeDamage(damage, ownerId);
            }

            EventBus.Instance.Publish(new EnvironmentHitEvent(ownerId, EnvironmentHitKind.Ground, point));
            ImpactFx.SpawnExplosion(point, explosionRadius);

            // Una explosion cerca se veia pero no se SENTIA: la camara
            // quedaba perfectamente quieta al lado de una granada. La
            // sacudida cae con la distancia y el tope global del rig
            // garantiza que nunca se pase, aunque exploten varias juntas.
            // Instance y no FindAnyObjectByType: Explode corre muy seguido.
            var rig = SP.CameraSystem.CameraRig.Instance;
            if (rig != null)
            {
                float dist = Vector3.Distance(rig.transform.position, point);
                float falloff = 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, explosionRadius * 4f));
                if (falloff > 0f)
                {
                    Vector3 away = rig.transform.position - point;
                    if (away.sqrMagnitude < 0.0001f) away = Vector3.up;
                    rig.KickDirectional(away.normalized, falloff * 0.35f);

                    // 181: fogonazo + sordera momentanea si la explosion
                    // fue MUY cerca. La sacudida sola no transmite que casi
                    // te alcanza; el destello si.
                    float veryClose = 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, explosionRadius * 1.5f));
                    if (veryClose > 0f)
                    {
                        SP.UI.ScreenFlashView.Explosion(veryClose);
                        SP.Presentation.AudioDucking.Duck(veryClose);
                    }
                }
            }
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
