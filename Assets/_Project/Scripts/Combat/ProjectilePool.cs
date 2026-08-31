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
            // El contador mide a ESTE pool: al rearmarlo con otro tamanio, lo
            // medido antes ya no dice nada del nuevo.
            ExhaustedCount = 0;
            Bootstrap();
        }

        // Cuantas veces el pool se quedo sin instancias libres y ObjectPool
        // tuvo que Instantiate EN CALIENTE, en pleno combate. Es el numero
        // que dice si el prewarm quedo corto: si termina la partida en 0, el
        // dimensionado alcanzo; si sube, cada unidad es un Instantiate (con
        // su Awake, su alta en escena y su basura) dentro de un frame de
        // juego. No se resetea solo -- es acumulado de la vida del pool.
        public int ExhaustedCount { get; private set; }

        // Piso del dimensionado: por debajo de esto el pool no amortigua ni
        // la primera rafaga (el propio jugador solo ya dispara varias balas
        // en vuelo a la vez), y prellenar 8 objetos no le cuesta nada a
        // nadie.
        public const int MinimumPrewarm = 8;

        // Techo de cordura: si alguien pasa parametros absurdos (o un valor
        // sin inicializar), mejor un pool grande que un Instantiate de miles
        // de objetos al armar la escena.
        public const int MaximumPrewarm = 512;

        // Dimensionado del pool en vez de la constante magica que usaba
        // HeadlessTestRunner (un 24 sin origen). Es funcion pura y estatica a
        // proposito: se puede razonar y verificar sin escena, sin Play mode y
        // sin instanciar nada.
        //
        // Cuantos proyectiles hay en vuelo a la vez, en regimen: cada unidad
        // mete `fireRatePerSecond` disparos por segundo y cada disparo vive
        // `projectileLifetime` segundos, asi que el conjunto sostiene
        // unidades * cadencia * vida. Se redondea HACIA ARRIBA porque 23.1 en
        // vuelo con 23 en el pool ya obliga a instanciar en caliente.
        public static int RecommendedPrewarm(int unitCount, float fireRatePerSecond, float projectileLifetime)
        {
            // Los NaN no se atrapan con las comparaciones de abajo (cualquier
            // comparacion con NaN da false), hay que preguntarlo aparte.
            if (float.IsNaN(fireRatePerSecond) || float.IsNaN(projectileLifetime)) return MinimumPrewarm;
            if (float.IsInfinity(fireRatePerSecond) || float.IsInfinity(projectileLifetime)) return MaximumPrewarm;
            if (unitCount <= 0 || fireRatePerSecond <= 0f || projectileLifetime <= 0f) return MinimumPrewarm;

            // En double para que un unitCount grande por una cadencia alta no
            // desborde antes de poder acotarlo.
            double inFlight = (double)unitCount * fireRatePerSecond * projectileLifetime;
            if (inFlight >= MaximumPrewarm) return MaximumPrewarm;

            int recommended = (int)System.Math.Ceiling(inFlight);
            return recommended < MinimumPrewarm ? MinimumPrewarm : recommended;
        }

        public Projectile Spawn(Vector3 position, Vector3 direction, int shooterId, TeamId shooterTeam, int damage, Color? color = null, float explosionRadius = 0f, float gravity = 0f, SP.Vehicles.Vehicle sourceVehicle = null, float speedMultiplier = 1f)
        {
            if (pool == null) Bootstrap();
            // Se pregunta ANTES del Get: ObjectPool.Get() instancia en el
            // acto cuando la pila de libres esta vacia, y despues de la
            // llamada ya no hay forma de distinguir un reuso de un
            // Instantiate.
            if (pool != null && pool.FreeCount == 0) ExhaustedCount++;
            var p = pool.Get();
            p.Configure(this, position, direction, shooterId, shooterTeam, damage, color, explosionRadius, gravity, sourceVehicle, speedMultiplier);
            return p;
        }

        public void Release(Projectile p) => pool?.Release(p);

        public int FreeCount => pool?.FreeCount ?? 0;
    }
}
