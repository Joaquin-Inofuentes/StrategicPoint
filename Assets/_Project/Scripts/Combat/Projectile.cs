using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Combat
{
    // Viaja, comprueba su propio impacto por distancia (sin física) y
    // se devuelve solo al pool. Nunca lo instancia nadie salvo el pool.
    // [DefaultExecutionOrder(-50)]: los proyectiles tienen que evaluar su
    // impacto DESPUES de que WorldSimulationDriver mueva a todo el mundo en
    // este mismo frame (-100), no antes -- si no, el orden relativo entre
    // "el soldado se movio" y "la bala revisa que toco" quedaba librado al
    // orden interno no documentado de Unity.
    [DefaultExecutionOrder(-50)]
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
        float effectiveSpeed;

        // BUG REAL encontrado al testear la balistica: el proyectil de la
        // torreta nace en la boca del cañon, que esta a menos de 2.5 m del
        // centro del chasis -- justo dentro del radio con el que el propio
        // proyectil detecta vehiculos. El tanque se pegaba un tiro a si
        // mismo en el PRIMER tick de cada disparo, sin salir nunca. Por eso
        // el vehiculo aparecia destruido una y otra vez en las pruebas y yo
        // lo atribuia a los enemigos.
        SP.Vehicles.Vehicle ignoreVehicle;

        // Item 194: si ya sono el silbido de esta bala. Es UNA sola vez por
        // proyectil: sin esto, mientras la bala atraviesa la esfera de 3 m
        // se cumple la condicion en varios ticks seguidos y saldrian cuatro
        // o cinco silbidos superpuestos por bala -- que ademas se comerian
        // el pool de voces entero en un tiroteo. Se resetea en OnDespawn,
        // junto a gravity e ignoreVehicle, porque el pool REUSA la misma
        // instancia: sin resetear, cada objeto del pool silbaria una unica
        // vez en toda la partida y nunca mas.
        bool whizzPlayed;

        public float Gravity => gravity;
        public Vector3 Velocity => velocity;

        int poolGeneration;
        public int PoolGeneration { get => poolGeneration; set => poolGeneration = value; }

        const float RestScale = 0.2f; // debe coincidir con la escala del prefab (BuildAndSaveProjectilePrefab)
        const float TraceStretch = 2.75f;

        // Instancias actualmente en vuelo. Permite avanzar la simulación
        // manualmente (tests) sin depender del bucle de Update de Unity.
        public static readonly List<Projectile> ActiveInstances = new List<Projectile>();

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload: sin
        // este reset, ActiveInstances arrastraria referencias fake-null de
        // proyectiles de la sesion de Play ANTERIOR (mismo patron que
        // AlertQueue.ResetOnLoad), inflando el contador de PerfHudView y
        // arriesgando a que AutoDemoRunner agarre una referencia vieja.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetActiveInstancesOnLoad() => ActiveInstances.Clear();

        // speedMultiplier: 1f para todo lo que ya existia (armas de mano,
        // que no lo pasan). El cañon del tanque pide 2f para que su
        // proyectil viaje al doble de la velocidad base sin tocar el
        // campo serializado que comparte el resto del pool.
        public void Configure(ProjectilePool owningPool, Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int dmg, Color? color = null, float explosionRadiusValue = 0f, float gravityValue = 0f, SP.Vehicles.Vehicle sourceVehicle = null, float speedMultiplier = 1f)
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
            effectiveSpeed = speed * speedMultiplier;
            velocity = (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward) * effectiveSpeed;

            // BUG REAL: este bloque vivia entero adentro de "if (color.HasValue)".
            // Un llamador que dispara sin pasar color (ej. pruebas que solo
            // piden daño) dejaba el Renderer SIN material asignar -- nunca,
            // ni una vez, en toda la vida del objeto pooleado -- y Unity
            // dibuja eso en el magenta de "sin material". El material ahora
            // se garantiza SIEMPRE (con blanco de base si no vino color),
            // y el tinte pedido se aplica encima si vino.
            AsegurarMaterial();
            if (color.HasValue && cachedRenderer != null) cachedRenderer.sharedMaterial.color = color.Value;
        }

        // El material se garantiza al DESPERTAR, no solo al disparar. Los
        // proyectiles del pool nacen todos juntos al arrancar la partida y
        // se quedan esperando; hasta que a uno le tocaba su primer disparo
        // tenia el Renderer sin material, y un renderer sin material se
        // dibuja magenta. Bastaba con que algo lo activara antes de
        // llamarle Launch (o con que quedara guardado asi en la escena,
        // que es lo que le paso a los 108 clones que habia horneados en
        // SC_Gameplay) para tener cubos rosas por el mapa.
        void Awake() => AsegurarMaterial();

        void AsegurarMaterial()
        {
            if (ownMaterialReady) return;

            if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null)
            {
                Debug.LogWarning($"[Projectile] {name}: no tiene Renderer, no se puede pintar.");
                return;
            }

            var baseMat = cachedRenderer.sharedMaterial;
            cachedRenderer.sharedMaterial = baseMat != null
                ? new Material(baseMat)
                : SP.Presentation.SafeMaterial.Create(Color.white);
            ownMaterialReady = true;
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
            whizzPlayed = false;
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
                transform.position += transform.forward * effectiveSpeed * dt;
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
                        PlayImpactSfx(EnvironmentHitKind.Vehicle, transform.position, 0.55f);
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
                        PlayImpactSfx(EnvironmentHitKind.Obstacle, transform.position, 0.5f);
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
                    PlayImpactSfx(EnvironmentHitKind.Ground, transform.position, 0.45f);
                    ImpactFx.SpawnScaledByDamage(transform.position, ImpactFx.GroundColor, damage);
                    DecalPool.Spawn(DecalKind.BulletHole, new Vector3(transform.position.x, 0.02f, transform.position.z), Vector3.up, 0.25f);
                }
                Expire();
                return;
            }

            // Item 194: va DESPUES de todos los chequeos de impacto y de
            // los return tempranos que traen. Una bala que te acaba de
            // pegar (o que pego al lado) ya no "pasa cerca": el silbido es
            // el sonido de la que FALLA, y sonarlo en el mismo tick del
            // impacto se leeria como que erraron cuando en realidad
            // acertaron.
            TryPlayNearMissWhizz();

            if (age >= lifetime) Expire();
        }

        // ------------------------------------------------------------------
        // Item 192: cola de impacto por material
        // ------------------------------------------------------------------

        // Hasta ahora EnvironmentHitEvent solo movia particulas: pegarle al
        // blindaje de un tanque, a una pared o al piso producia tres
        // efectos visuales distintos y CERO sonido. Con la camara mirando
        // para otro lado el jugador no se enteraba de nada.
        //
        // Prioridad media-baja por defecto: los impactos son el suceso mas
        // frecuente del juego y no pueden ganarle en el pool a un disparo
        // ni a una muerte. Si el limite de voces los descarta, se descartan
        // -- para eso esta.
        static void PlayImpactSfx(EnvironmentHitKind kind, Vector3 point, float volume, float priority = 0.4f)
        {
            // La suite headless corre en Edit mode. AudioDirector lo
            // vuelve a chequear, pero salir antes evita hasta generar el
            // clip la primera vez.
            if (!Application.isPlaying) return;

            SfxKind sfx;
            switch (kind)
            {
                // Blindaje: multi-parcial agudo, "tink".
                case EnvironmentHitKind.Vehicle: sfx = SfxKind.ImpactMetal; break;
                // Los obstaculos de este juego son cobertura solida
                // (ObstacleMarker: bloques que bloquean el paso), asi que
                // suenan a piedra y no a chapa ni a tierra.
                case EnvironmentHitKind.Obstacle: sfx = SfxKind.ImpactStone; break;
                default: sfx = SfxKind.ImpactDirt; break;
            }

            // Estatico y con Instance null-safe: si todavia no hay
            // director (Edit mode, o antes de que se construya) devuelve
            // false en silencio en vez de tirar.
            AudioDirector.PlayAt(sfx, point, volume, priority);
        }

        // ------------------------------------------------------------------
        // Item 194: silbido de bala cercana
        // ------------------------------------------------------------------

        // A que distancia del oido pasa a ser "cerca". Tres metros es el
        // radio pedido y es coherente con hitRadius (1): mas ancho y
        // silbaria cualquier bala del tiroteo, mas angosto y solo silbarian
        // las que igual te iban a pegar.
        const float WhizzRadius = 3f;

        // Solo para el camino de respaldo por camara (ver abajo): a la
        // velocidad de un proyectil de mano, en 0.12 s ya recorrio varios
        // metros y salio de la esfera. Sirve para que las balas PROPIAS,
        // que nacen justo encima del jugador, no silben al salir.
        const float WhizzFallbackMinAge = 0.12f;

        void TryPlayNearMissWhizz()
        {
            if (whizzPlayed) return;
            if (!Application.isPlaying) return;

            // COMO SE SABE QUIEN ES EL POSEIDO SIN ACOPLAR ESTO AL INPUT:
            // PlayerBrain no tiene ningun accesor estatico (hay que buscarlo
            // con FindAnyObjectByType, que es exactamente lo prohibido:
            // esto corre por proyectil y por frame). Pero KillFeedbackDirector
            // SI es un singleton estatico y YA tiene el PlayerBrain cableado
            // desde el constructor de escena, porque lo necesita para saber
            // si una baja fue tuya. Son dos derreferencias de campo, cero
            // barridos, y no le agrega ninguna dependencia nueva al
            // proyecto: SP.Combat ya usa SP.Presentation.
            var director = SP.Presentation.KillFeedbackDirector.Instance;
            var possessed = director != null && director.Brain != null ? director.Brain.Current : null;

            Vector3 earPos;
            if (possessed != null)
            {
                // El silbido es de las balas ENEMIGAS. Las propias, y las
                // de tu escuadra, no producen esa sensacion -- y en un
                // tiroteo con cincuenta aliados sonarian todo el tiempo.
                if (possessed.Team == ownerTeam) return;
                earPos = possessed.transform.position;
            }
            else
            {
                // RESPALDO DOCUMENTADO: sin poseido resuelto (Edit mode, o
                // el director todavia sin construir) se usa la camara como
                // aproximacion de "donde esta el jugador". Es honesto:
                // ahi vive el AudioListener, o sea es literalmente el punto
                // desde el que se escucha.
                //
                // Lo que se PIERDE es el filtro por equipo: sin soldado no
                // hay equipo del jugador contra el cual comparar ownerTeam.
                // Por eso este camino exige ademas una edad minima: en FPS
                // la camara esta encima del soldado poseido y sus propias
                // balas nacen dentro de la esfera de 3 m, asi que sin el
                // gate cada disparo tuyo se silbaria a si mismo.
                var cam = Camera.main;
                if (cam == null) return;
                if (age < WhizzFallbackMinAge) return;
                earPos = cam.transform.position;
            }

            // sqrMagnitude y no Distance: esto corre por proyectil y por
            // frame, y la raiz cuadrada no aporta nada para comparar.
            if ((transform.position - earPos).sqrMagnitude > WhizzRadius * WhizzRadius) return;

            whizzPlayed = true;
            // Prioridad BAJA a proposito (item 194): es ambientacion, no
            // informacion accionable. Si el pool esta saturado por disparos
            // e impactos, este es el primero que sobra.
            AudioDirector.PlayAt(SfxKind.BulletWhizz, transform.position, 0.4f, 0.2f);
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
                if (away.sqrMagnitude < 0.0001f)
                {
                    // Mismo criterio que el vector principal (arriba): el empuje de
                    // una granada es lateral, nunca vertical. Random.insideUnitSphere
                    // tenia componente Y y podia lanzar al soldado para arriba si
                    // caia justo en el epicentro.
                    var randomXZ = Random.insideUnitCircle;
                    away = new Vector3(randomXZ.x, 0f, randomXZ.y);
                    if (away.sqrMagnitude < 0.0001f) away = Vector3.forward; // caso degenerado de insideUnitCircle (~(0,0)): direccion fija
                }
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
            // Mas fuerte y mas prioritario que el impacto puntual: es una
            // granada, no una bala. Sigue siendo la cola de tierra y no un
            // sonido de explosion propio -- el evento que publica la
            // explosion ES Ground, y inventarle un SfxKind aparte seria
            // diseño nuevo, no el item 192.
            PlayImpactSfx(EnvironmentHitKind.Ground, point, 0.85f, 0.8f);
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
