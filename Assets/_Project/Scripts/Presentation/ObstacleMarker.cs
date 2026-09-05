using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Marca un cubo como "obstáculo" para que un proyectil lo detecte y
    // el jugador tenga feedback distinto al pegarle a un obstáculo en vez
    // de a un enemigo o un vehículo.
    //
    // Antes eran inmortales: disparar contra la cobertura no producia
    // ningun cambio, asi que el escenario era decorado y no un elemento
    // tactico. Ahora tienen vida y pasan por etapas visibles hasta el
    // colapso, que libera escombros del pool compartido.
    public class ObstacleMarker : MonoBehaviour
    {
        [SerializeField] int maxHealth = 150;
        [SerializeField] int currentHealth = -1;

        // G1: el barril. No es un tipo de objeto aparte -- es este mismo
        // obstaculo con un comportamiento extra: el primer impacto lo
        // enciende (sigue en pie, sigue siendo cobertura) y unos segundos
        // despues estalla solo, dañe alrededor y recien ahi colapsa. Un
        // barril que ya colapso por daño normal mientras ardia no vuelve
        // a estallar (Estallar() lo chequea).
        [SerializeField] bool esExplosivo = false;
        [SerializeField] float radioExplosion = 6f;
        [SerializeField] int danoExplosion = 60;
        [SerializeField] float demoraExplosion = 2.5f;

        public bool EsExplosivo => esExplosivo;
        public bool EstaEncendido { get; private set; }
        float temporizadorExplosion;

        static readonly Color ColorFuego = new Color(1f, 0.42f, 0.05f);
        static readonly List<ObstacleMarker> Encendidos = new List<ObstacleMarker>();

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload:
        // sin este reset, Encendidos arrastraria referencias fake-null de
        // barriles de la sesion de Play ANTERIOR (mismo patron que
        // Projectile.ResetActiveInstancesOnLoad).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEncendidosOnLoad() => Encendidos.Clear();

        // Se llama desde WorldSimulationDriver.Step, el mismo unico camino
        // de simulacion que ya usan PedidoDeCuracion y RescateAutomatico:
        // asi la suite headless (que avanza el tiempo a mano) ejercita
        // exactamente esto, no una copia aparte.
        public static void Tick(float dt)
        {
            for (int i = Encendidos.Count - 1; i >= 0; i--)
            {
                var m = Encendidos[i];
                if (m == null) { Encendidos.RemoveAt(i); continue; }
                m.temporizadorExplosion -= dt;
                if (m.temporizadorExplosion <= 0f)
                {
                    Encendidos.RemoveAt(i);
                    m.Estallar();
                }
            }
        }

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth < 0 ? maxHealth : currentHealth;
        public bool IsCollapsed { get; private set; }

        // Umbrales de etapa como fraccion de vida. Tres etapas antes del
        // colapso: intacto, agrietado, muy dañado.
        static readonly float[] StageThresholds = { 0.66f, 0.33f };
        public int Stage
        {
            get
            {
                float frac = maxHealth > 0 ? (float)CurrentHealth / maxHealth : 0f;
                if (frac <= 0f) return 3;
                if (frac <= StageThresholds[1]) return 2;
                if (frac <= StageThresholds[0]) return 1;
                return 0;
            }
        }

        MeshRenderer rend;
        Color baseColor;
        Vector3 baseScale;
        bool cached;

        void CacheIfNeeded()
        {
            if (cached) return;
            cached = true;
            rend = GetComponent<MeshRenderer>();
            if (rend != null) baseColor = rend.sharedMaterial.color;
            baseScale = transform.localScale;
            if (currentHealth < 0) currentHealth = maxHealth;
        }

        // OnEnable/OnDisable y no Awake/OnDestroy: asi el alta/baja
        // tambien acompaña a un obstaculo que se desactiva al derrumbarse,
        // sin dejar una referencia muerta en el registro.
        void OnEnable()
        {
            CacheIfNeeded();
            SP.Core.WorldSystemsRegistry.Register(this);
        }

        void OnDisable() => SP.Core.WorldSystemsRegistry.Unregister(this);

        void Awake() => CacheIfNeeded();

        public void TakeDamage(int amount)
        {
            CacheIfNeeded();
            if (IsCollapsed) return;

            // G1: "primer impacto: se prende fuego" -- va antes que
            // cualquier otra cosa, para que encienda aunque ese mismo tiro
            // ya lo hubiera dejado en la ultima etapa o lo hubiera matado
            // por daño normal (Collapse() de abajo revisa IsCollapsed y no
            // vuelve a colapsar dos veces).
            if (esExplosivo && !EstaEncendido) Encender();

            int stageBefore = Stage;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            int stageAfter = Stage;

            if (stageAfter != stageBefore) ApplyStageLook(stageAfter);
            if (currentHealth <= 0) Collapse();
        }

        void ApplyStageLook(int stage)
        {
            if (rend == null) return;
            float darken = stage * 0.22f;
            // Si ya esta encendido, una etapa nueva (otro balazo mientras
            // arde) no le pisa el tinte de fuego con el oscurecimiento
            // normal -- se oscurece EL FUEGO, no el color de base.
            var colorBase = EstaEncendido ? ColorFuego : baseColor;
            CubeFxReactor.WriteTint(rend, Color.Lerp(colorBase, Color.black, darken));

            float squash = 1f - stage * 0.12f;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * squash, baseScale.z);
            transform.position = new Vector3(transform.position.x, baseScale.y * squash * 0.5f, transform.position.z);

            // Cada etapa suelta un poco de escombro: la etapa se ve Y se
            // oye/nota, no es solo un cambio de tinte silencioso.
            SpawnDebris(6, 4f);
        }

        void Collapse()
        {
            IsCollapsed = true;
            SpawnDebris(14, 7f);
            gameObject.SetActive(false);
            // El obstaculo que se cayo abrio un paso que la grilla de
            // navegacion todavia cree cerrado: sin esto los soldados
            // seguirian rodeando un escombro que ya no existe.
            SP.Core.NavService.Invalidate();
            // Y por el mismo motivo, las coberturas que daba este obstaculo
            // ya no cubren de nada: sin reregistrar, la IA seguiria yendo a
            // esconderse detras de un escombro.
            SP.Core.Coberturas.Registrar();
        }

        void Encender()
        {
            EstaEncendido = true;
            temporizadorExplosion = demoraExplosion;
            if (!Encendidos.Contains(this)) Encendidos.Add(this);
            if (rend != null) CubeFxReactor.WriteTint(rend, ColorFuego);
        }

        // Estalla sola por el timer, no por quedarse sin vida -- un barril
        // puede seguir de pie (Stage bajo) y explotar igual a los N
        // segundos. Reusa Projectile.ExplodeAt (misma caida de daño y
        // linea de vista que la granada del tanque) para el daño de area,
        // y Collapse() para la destruccion propia del cubo (escombros,
        // invalidar nav, reregistrar coberturas) -- ningun barril estalla
        // dos veces porque Collapse ya puso IsCollapsed en true.
        void Estallar()
        {
            EstaEncendido = false;
            if (IsCollapsed) return;
            var punto = transform.position + Vector3.up * baseScale.y * 0.5f;
            SP.Combat.Projectile.ExplodeAt(punto, radioExplosion, danoExplosion, ownerId: -1, spareTeam: null);
            Collapse();
        }

        void SpawnDebris(int count, float speed)
        {
            var origin = transform.position + Vector3.up * baseScale.y * 0.4f;
            Color debrisColor = rend != null ? CubeFxReactor.ReadTint(rend) : baseColor;
            for (int i = 0; i < count; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 1.2f).normalized;
                DebrisPool.Spawn(origin + Random.insideUnitSphere * 0.4f, dir * speed * Random.Range(0.6f, 1.3f),
                    debrisColor, Random.Range(0.12f, 0.28f));
            }
        }
    }
}
