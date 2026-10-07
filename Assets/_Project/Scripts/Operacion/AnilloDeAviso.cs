using System.Collections.Generic;
using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // WP9b (#097): telegrafia de impacto en el piso, reutilizable (cañonazo del jefe, cohetes del helicoptero). Un circulo rojo de radio "radio"
    // que pulsa (cada vez mas rapido) y se va llenando de adentro hacia afuera durante "segundos"; al llegar a 1 el aviso termina y el que lo
    // pidio hace caer el golpe. Pool de 6 anillos (dos quads planos cada uno, un solo material por textura, color por MaterialPropertyBlock).
    public class AnilloDeAviso : MonoBehaviour
    {
        public const int Cupo = 6;
        public static readonly Color ColorDeAlerta = new Color(1f, 0.12f, 0.08f, 1f);

        static readonly List<AnilloDeAviso> pool = new List<AnilloDeAviso>();
        static Material matAro, matRelleno;
        static Texture2D texAro, texRelleno;
        static Mesh malla;
        static GameObject raizDelPool;
        static readonly RaycastHit[] bufferPiso = new RaycastHit[10];

        Transform aro, relleno;
        MeshRenderer rAro, rRelleno;
        MaterialPropertyBlock bloque;
        float edad, duracion, radio;
        Color color;

        public bool Activo { get; private set; }
        public float Progreso01 => duracion > 0f ? Mathf.Clamp01(edad / duracion) : 1f;
        public float Edad => edad;
        public float Duracion => duracion;
        public float Radio => radio;
        public Vector3 Centro { get; private set; }

        public static int Activos { get { int n = 0; foreach (var a in pool) if (a != null && a.Activo) n++; return n; } }
        public static IReadOnlyList<AnilloDeAviso> Todos => pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { pool.Clear(); matAro = null; matRelleno = null; texAro = null; texRelleno = null; malla = null; raizDelPool = null; }

        // Muestra un aviso en el piso bajo "centro" (la altura del piso se mide con un rayo hacia abajo). Devuelve el anillo para poder leerlo o cancelarlo.
        public static AnilloDeAviso Mostrar(Vector3 centro, float radio, float segundos, Color? color = null)
        {
            if (!Application.isPlaying) return null;
            var a = Tomar();
            a.Iniciar(centro, radio, segundos, color ?? ColorDeAlerta);
            return a;
        }

        public static void CancelarTodos() { foreach (var a in pool) if (a != null) a.Cancelar(); }

        static AnilloDeAviso Tomar()
        {
            foreach (var a in pool) if (a != null && !a.Activo) return a;
            if (pool.Count < Cupo)
            {
                var a = Crear();
                pool.Add(a);
                return a;
            }
            // Pool lleno: pisa el mas viejo.
            AnilloDeAviso masViejo = null;
            foreach (var a in pool) if (a != null && (masViejo == null || a.edad > masViejo.edad)) masViejo = a;
            return masViejo != null ? masViejo : Crear();
        }

        static Texture2D TexturaDeAro()
        {
            if (texAro != null) return texAro;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Aro grueso en el borde (0.86..0.98) y una linea fina interior en 0.5 (marca de "medio camino").
                    float aro = Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) / 0.07f);
                    float fina = Mathf.Clamp01(1f - Mathf.Abs(r - 0.5f) / 0.012f) * 0.45f;
                    float corte = Mathf.Clamp01((1f - r) / 0.02f);
                    float a = Mathf.Clamp01(Mathf.Max(aro, fina) * corte);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return texAro = tex;
        }

        static Texture2D TexturaDeRelleno()
        {
            if (texRelleno != null) return texRelleno;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Lerp(0.55f, 0.85f, r) * Mathf.Clamp01((1f - r) / 0.02f);   // un poco mas denso hacia el borde
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return texRelleno = tex;
        }

        static Mesh Malla()
        {
            if (malla != null) return malla;
            var m = new Mesh { name = "AvisoPlano", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
            return malla = m;
        }

        static void AsegurarMateriales()
        {
            if (matAro != null) return;
            matAro = ParticleMaterialFactory.CreateTransparent(Color.white, TexturaDeAro());
            if (matAro.HasProperty("_Cull")) matAro.SetInt("_Cull", 0);
            matRelleno = ParticleMaterialFactory.CreateTransparent(Color.white, TexturaDeRelleno());
            if (matRelleno.HasProperty("_Cull")) matRelleno.SetInt("_Cull", 0);
        }

        // Para AnilloDeSubida (#111): mismo material de aro y la misma malla plana.
        internal static Material MaterialDeAro() { AsegurarMateriales(); return matAro; }
        internal static Mesh MallaPlana() => Malla();
        internal static float AlturaDelPisoEn(Vector3 p) => AlturaDelPiso(p);

        static AnilloDeAviso Crear()
        {
            AsegurarMateriales();
            if (raizDelPool == null) raizDelPool = new GameObject("AnillosDeAviso") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var go = new GameObject("AnilloDeAviso");
            go.transform.SetParent(raizDelPool.transform, false);
            var a = go.AddComponent<AnilloDeAviso>();
            a.bloque = new MaterialPropertyBlock();
            a.relleno = NuevoQuad(go.transform, "Relleno", matRelleno, out a.rRelleno);
            a.aro = NuevoQuad(go.transform, "Aro", matAro, out a.rAro);
            go.SetActive(false);
            return a;
        }

        static Transform NuevoQuad(Transform padre, string nombre, Material mat, out MeshRenderer r)
        {
            var q = new GameObject(nombre);
            q.transform.SetParent(padre, false);
            q.AddComponent<MeshFilter>().sharedMesh = Malla();
            r = q.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }

        static float AlturaDelPiso(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 2.5f, p.z), Vector3.down, bufferPiso, 12f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MinValue; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var c = bufferPiso[i].collider;
                if (c == null || c.GetComponentInParent<SP.Actors.Soldier>() != null || c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (bufferPiso[i].point.y > mejor) { mejor = bufferPiso[i].point.y; hay = true; }
            }
            return hay ? mejor : Mathf.Max(0f, p.y - 0.8f);
        }

        void Iniciar(Vector3 c, float r, float seg, Color col)
        {
            radio = r; duracion = Mathf.Max(0.05f, seg); edad = 0f; color = col;
            float y = AlturaDelPiso(c) + 0.3f;
            Centro = new Vector3(c.x, y, c.z);
            transform.position = Centro;
            transform.rotation = Quaternion.identity;
            aro.localScale = new Vector3(r * 2f, 1f, r * 2f);
            relleno.localScale = new Vector3(0.01f, 1f, 0.01f);
            Activo = true;
            gameObject.SetActive(true);
            Pintar();
        }

        public void Cancelar()
        {
            Activo = false;
            if (gameObject != null) gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Activo) return;
            edad += Time.deltaTime;
            if (edad >= duracion) { Cancelar(); return; }
            Pintar();
        }

        void Pintar()
        {
            float k = Progreso01;
            // Relleno: crece de adentro hacia afuera (ease-in: lento al principio, rapido al final).
            float rr = radio * 2f * Mathf.Max(0.02f, k * k * 0.35f + k * 0.65f);
            relleno.localScale = new Vector3(rr, 1f, rr);
            // Pulso: la frecuencia sube de 2 a 8 Hz hacia el impacto.
            float fase = edad * Mathf.Lerp(2f, 8f, k) * Mathf.PI * 2f;
            float pulso = 0.5f + 0.5f * Mathf.Sin(fase);
            var ca = color; ca.a = Mathf.Lerp(0.55f, 1f, pulso);
            var cr = color; cr.a = Mathf.Lerp(0.30f, 0.62f, k) * (0.8f + 0.2f * pulso);
            bloque.SetColor("_BaseColor", ca); bloque.SetColor("_Color", ca);
            rAro.SetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", cr); bloque.SetColor("_Color", cr);
            rRelleno.SetPropertyBlock(bloque);
        }

        void OnDestroy() { pool.Remove(this); }
    }
}
