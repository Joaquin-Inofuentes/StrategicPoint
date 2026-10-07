using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // WP10 (#101): la flecha roja que anuncia por donde entra una oleada. Una flecha grande (12 m) en el piso, apoyada sobre la calle por la que
    // van a entrar y apuntando al sector, y otra gemela (mas grande) en el minimapa. Pulsa mientras dura el aviso. Pool chico: como mucho
    // salen dos oleadas a la vez (A+C).
    public class FlechaDeOleada : MonoBehaviour
    {
        public const float Largo = 12f, Ancho = 5.2f;
        static readonly List<FlechaDeOleada> pool = new List<FlechaDeOleada>();
        static Texture2D tex;
        static Material matMundo, matMapa;
        static Mesh quad;
        static GameObject raizDelPool;

        Transform mundo, mapa;
        MeshRenderer rMundo, rMapa;
        MaterialPropertyBlock bloque;
        float edad, duracion;
        public bool Activa { get; private set; }
        public Vector3 Centro { get; private set; }
        public Vector3 Direccion { get; private set; }
        public float Edad => edad;
        public static readonly Color ColorDeAlerta = new Color(1f, 0.16f, 0.1f, 1f);

        public static int Activas { get { int n = 0; foreach (var f in pool) if (f != null && f.Activa) n++; return n; } }
        public static IReadOnlyList<FlechaDeOleada> Todas => pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { pool.Clear(); tex = null; matMundo = null; matMapa = null; quad = null; raizDelPool = null; }

        // Muestra una flecha cuyo centro esta en "centro" y que apunta hacia "direccion" (horizontal). Dura "segundos" (y se desvanece al final).
        public static FlechaDeOleada Mostrar(Vector3 centro, Vector3 direccion, float segundos)
        {
            if (!Application.isPlaying) return null;
            var f = Tomar();
            f.Iniciar(centro, direccion, segundos);
            return f;
        }

        public static void QuitarTodas() { foreach (var f in pool) if (f != null) f.Apagar(); }

        static FlechaDeOleada Tomar()
        {
            foreach (var f in pool) if (f != null && !f.Activa) return f;
            var n = Crear();
            pool.Add(n);
            return n;
        }

        static Texture2D Textura()
        {
            if (tex != null) return tex;
            // Flecha apuntando a +v (arriba de la textura): cola rectangular y cabeza triangular, con dos chevrones claros adentro.
            const int w = 128, h = 256;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f;      // -1..1
                    float v = (y + 0.5f) / h;                // 0..1
                    float a = 0f;
                    const float cabeza = 0.55f;               // la cabeza ocupa el 45% de arriba
                    if (v < cabeza) a = Mathf.Abs(u) < 0.42f ? 1f : 0f;                              // cola
                    else a = Mathf.Abs(u) < Mathf.Lerp(1f, 0f, (v - cabeza) / (1f - cabeza)) ? 1f : 0f;   // cabeza
                    // borde suave
                    float borde = v < cabeza ? Mathf.Clamp01((0.42f - Mathf.Abs(u)) / 0.04f) : Mathf.Clamp01((Mathf.Lerp(1f, 0f, (v - cabeza) / (1f - cabeza)) - Mathf.Abs(u)) / 0.06f);
                    a = Mathf.Min(a, 1f) * Mathf.Clamp01(borde + 0.0f);
                    // chevrones: franjas diagonales mas claras (quedan como relieve: alfa algo menor)
                    float chev = Mathf.Repeat(v * 5.5f - Mathf.Abs(u) * 1.4f, 1f);
                    float claro = chev < 0.22f ? 0.55f : 1f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a * claro) * 255f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return tex = t;
        }

        static Mesh Quad()
        {
            if (quad != null) return quad;
            var m = new Mesh { name = "FlechaQuad", hideFlags = HideFlags.HideAndDontSave };
            // En el plano XZ con el "frente" (v) hacia +Z: la rotacion de Y la apunta.
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
            return quad = m;
        }

        static FlechaDeOleada Crear()
        {
            if (matMundo == null)
            {
                matMundo = DiamondGizmo.NuevoMaterialTransparente(Color.white, Textura());
                matMapa = DiamondGizmo.NuevoMaterialTransparente(Color.white, Textura());
            }
            if (raizDelPool == null) raizDelPool = new GameObject("FlechasDeOleada") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var go = new GameObject("FlechaDeOleada");
            go.transform.SetParent(raizDelPool.transform, false);
            var f = go.AddComponent<FlechaDeOleada>();
            f.bloque = new MaterialPropertyBlock();

            var gm = new GameObject("Mundo");
            gm.transform.SetParent(go.transform, false);
            gm.AddComponent<MeshFilter>().sharedMesh = Quad();
            f.rMundo = gm.AddComponent<MeshRenderer>();
            f.rMundo.sharedMaterial = matMundo;
            f.rMundo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; f.rMundo.receiveShadows = false;
            f.mundo = gm.transform;

            int layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8;
            var gp = new GameObject("Minimapa") { layer = layer };
            gp.transform.SetParent(go.transform, false);
            gp.AddComponent<MeshFilter>().sharedMesh = Quad();
            f.rMapa = gp.AddComponent<MeshRenderer>();
            f.rMapa.sharedMaterial = matMapa;
            f.rMapa.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; f.rMapa.receiveShadows = false;
            f.mapa = gp.transform;
            go.SetActive(false);
            return f;
        }

        void Iniciar(Vector3 centro, Vector3 dir, float seg)
        {
            dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            Centro = new Vector3(centro.x, 0f, centro.z); Direccion = dir;
            duracion = Mathf.Max(0.2f, seg); edad = 0f;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            mundo.SetPositionAndRotation(new Vector3(centro.x, 0.36f, centro.z), rot);
            mundo.localScale = new Vector3(Ancho, 1f, Largo);
            // En el minimapa la flecha se agranda para que se lea (el mapa muestra ~110 m de radio).
            mapa.SetPositionAndRotation(new Vector3(centro.x, 46f, centro.z), rot);
            mapa.localScale = new Vector3(Ancho * 2.2f, 1f, Largo * 2.2f);
            Activa = true;
            gameObject.SetActive(true);
            Pintar();
        }

        public void Apagar()
        {
            Activa = false;
            if (gameObject != null) gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Activa) return;
            edad += Time.deltaTime;
            if (edad >= duracion) { Apagar(); return; }
            Pintar();
        }

        void Pintar()
        {
            float restante = duracion - edad;
            float pulso = 0.5f + 0.5f * Mathf.Sin(edad * Mathf.Lerp(4f, 9f, Mathf.Clamp01(edad / duracion)) * Mathf.PI * 2f * 0.5f);
            float fundido = Mathf.Clamp01(restante / 0.8f);
            var c = ColorDeAlerta; c.a = Mathf.Lerp(0.55f, 1f, pulso) * fundido;
            bloque.SetColor("_BaseColor", c); bloque.SetColor("_Color", c);
            rMundo.SetPropertyBlock(bloque);
            rMapa.SetPropertyBlock(bloque);
            // La flecha "avanza" hacia el sector: un pequeno vaiven a lo largo de su eje.
            mundo.position = new Vector3(Centro.x, 0.36f, Centro.z) + Direccion * (0.8f * Mathf.Sin(edad * 5f));
        }

        void OnDestroy() { pool.Remove(this); }
    }
}
