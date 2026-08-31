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

            int stageBefore = Stage;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            int stageAfter = Stage;

            if (stageAfter != stageBefore) ApplyStageLook(stageAfter);
            if (currentHealth <= 0) Collapse();
        }

        void ApplyStageLook(int stage)
        {
            if (rend == null) return;
            // Se oscurece y se "asienta" (se achata y se hunde) en cada
            // etapa: dos señales distintas, para que se lea de lejos en
            // vista RTS y de cerca en primera persona.
            float darken = stage * 0.22f;
            rend.sharedMaterial.color = Color.Lerp(baseColor, Color.black, darken);

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
        }

        void SpawnDebris(int count, float speed)
        {
            var origin = transform.position + Vector3.up * baseScale.y * 0.4f;
            Color debrisColor = rend != null ? rend.sharedMaterial.color : baseColor;
            for (int i = 0; i < count; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 1.2f).normalized;
                DebrisPool.Spawn(origin + Random.insideUnitSphere * 0.4f, dir * speed * Random.Range(0.6f, 1.3f),
                    debrisColor, Random.Range(0.12f, 0.28f));
            }
        }
    }
}
