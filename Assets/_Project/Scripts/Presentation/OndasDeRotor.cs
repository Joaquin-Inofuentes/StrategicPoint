using UnityEngine;

namespace SP.Presentation
{
    // Bug #075: ondas de viento circulares que se expanden por el piso bajo el helicoptero. Pool propio de 10 quads planos con
    // una textura de anillo generada (un material compartido, color y alfa por MaterialPropertyBlock). Cada 0,35 s sale un anillo
    // de 2 a 16 m de radio en 1,2 s con alfa 0,5 a 0 y color arena. Lo maneja el Helicoptero cuando esta cerca del piso.
    public class OndasDeRotor : MonoBehaviour
    {
        public const int Cupo = 10;
        public const float Intervalo = 0.35f, Duracion = 1.2f, RadioInicial = 2f, RadioFinal = 16f, AlfaInicial = 0.5f;
        public static readonly Color ColorArena = new Color(0.85f, 0.78f, 0.6f, 1f);

        class Onda { public Transform t; public MeshRenderer r; public float edad = -1f; public float alfa; public float radio0; }
        readonly Onda[] ondas = new Onda[Cupo];
        MaterialPropertyBlock bloque;
        static Material material;
        static Texture2D texturaAnillo;
        static Mesh mallaPlana;
        float proxima;
        bool armado;
        public int Emitidas { get; private set; }

        public int Activas { get { int n = 0; foreach (var o in ondas) if (o != null && o.edad >= 0f) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { material = null; texturaAnillo = null; mallaPlana = null; }

        void Awake() => Armar();

        void OnDestroy()
        {
            if (padreDeOndas != null) Destroy(padreDeOndas);
        }

        static Texture2D TexturaDeAnillo()
        {
            if (texturaAnillo != null) return texturaAnillo;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Anillo fino hacia el borde con cola suave hacia adentro y corte suave hacia afuera.
                    float cuerpo = Mathf.Clamp01(1f - Mathf.Abs(r - 0.84f) / 0.16f);
                    float cola = Mathf.Clamp01((r - 0.4f) / 0.44f) * 0.3f;
                    float borde = Mathf.Clamp01((1f - r) / 0.06f);
                    float a = Mathf.Clamp01(Mathf.Max(cuerpo, cola) * borde);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return texturaAnillo = tex;
        }

        static Mesh MallaPlana()
        {
            if (mallaPlana != null) return mallaPlana;
            var m = new Mesh { name = "OndaPlana", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
            return mallaPlana = m;
        }

        void Armar()
        {
            if (armado) return;
            armado = true;
            if (material == null)
            {
                material = ParticleMaterialFactory.CreateTransparent(Color.white, TexturaDeAnillo());
                if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0);
            }
            bloque = new MaterialPropertyBlock();
            var raiz = new GameObject("OndasDeRotor") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            for (int i = 0; i < Cupo; i++)
            {
                var go = new GameObject("Onda");
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = MallaPlana();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                ondas[i] = new Onda { t = go.transform, r = r };
            }
            padreDeOndas = raiz;   // se destruye junto con este componente
        }

        GameObject padreDeOndas;

        void OnDisable() { foreach (var o in ondas) if (o != null) { o.edad = -1f; if (o.t != null) o.t.gameObject.SetActive(false); } }

        // Se llama cada frame: si emitir, sale un anillo cada Intervalo. alfa01 escala el alfa inicial (cercania al piso).
        public void Tick(bool emitir, Vector3 centro, float alfa01, float dt)
        {
            if (!armado) Armar();
            if (emitir && Time.time >= proxima)
            {
                proxima = Time.time + Intervalo;
                Emitir(centro, alfa01);
            }
            for (int i = 0; i < Cupo; i++)
            {
                var o = ondas[i];
                if (o.edad < 0f) continue;
                o.edad += dt;
                float k = o.edad / Duracion;
                if (k >= 1f) { o.edad = -1f; o.t.gameObject.SetActive(false); continue; }
                float radio = Mathf.Lerp(RadioInicial, RadioFinal, 1f - (1f - k) * (1f - k));   // sale rapido y frena
                o.t.localScale = new Vector3(radio * 2f, 1f, radio * 2f);
                var c = ColorArena; c.a = o.alfa * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, k)));   // 0,5 sostenido casi la mitad de la vida y se apaga hacia el final
                bloque.SetColor("_BaseColor", c);
                bloque.SetColor("_Color", c);
                o.r.SetPropertyBlock(bloque);
            }
        }

        void Emitir(Vector3 centro, float alfa01)
        {
            Onda libre = null, masVieja = null;
            foreach (var o in ondas)
            {
                if (o.edad < 0f) { libre = o; break; }
                if (masVieja == null || o.edad > masVieja.edad) masVieja = o;
            }
            var w = libre ?? masVieja;
            w.edad = 0f;
            w.alfa = AlfaInicial * Mathf.Clamp01(alfa01);
            w.t.position = centro;
            w.t.rotation = Quaternion.identity;
            w.t.localScale = new Vector3(RadioInicial * 2f, 1f, RadioInicial * 2f);
            w.t.gameObject.SetActive(true);
            Emitidas++;
        }
    }
}
