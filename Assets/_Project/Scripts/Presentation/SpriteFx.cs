using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Ronda 12: los impactos de granada/canon eran una esfera de color y las marcas en el piso, quads grises lisos (placeholder).
    // Ahora son sprites reales (Kenney Particle Pack, CC0: Resources/UI/Impactos): fuego, humo, chispas, tierra y quemaduras.
    public static class SpritesReales
    {
        public const string Carpeta = "UI/Impactos/";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => cache.Clear();

        // 512 px = 1 unidad: la escala del objeto es directamente el tamano en metros.
        public static Sprite Obtener(string nombre)
        {
            if (cache.TryGetValue(nombre, out var s) && s != null) return s;
            var tex = SP.Core.RecursosCache.Cargar<Texture2D>(Carpeta + nombre);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            s.name = nombre;
            cache[nombre] = s;
            return s;
        }
    }

    // Un sprite de efecto que mira a la camara, crece, sube, gira y se desvanece. Pool fijo: el mas viejo se recicla.
    public class SpriteFx : MonoBehaviour
    {
        public const int Cupo = 96;
        static readonly List<SpriteFx> pool = new List<SpriteFx>();
        static Transform root;
        public static int Activos { get { int n = 0; for (int i = 0; i < pool.Count; i++) if (pool[i] != null && pool[i].gameObject.activeSelf) n++; return n; } }
        public static int Lanzados { get; private set; }

        SpriteRenderer sr;
        Color color;
        float edad, dur, tam0, tam1, giro, roll;
        Vector3 vel; float freno;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { pool.Clear(); root = null; Lanzados = 0; }

        public static void LimpiarTodo()
        {
            foreach (var f in pool) if (f != null) { if (Application.isPlaying) Destroy(f.gameObject); else DestroyImmediate(f.gameObject); }
            pool.Clear();
            if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); root = null; }
        }

        static SpriteFx Tomar()
        {
            if (root == null)
            {
                pool.Clear();
                var g = new GameObject("SpriteFxPool") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
                root = g.transform;
            }
            pool.RemoveAll(x => x == null);
            for (int i = 0; i < pool.Count; i++) if (!pool[i].gameObject.activeSelf) return pool[i];
            if (pool.Count >= Cupo)
            {
                // Cupo lleno: se recicla el que mas avanzo.
                SpriteFx mas = pool[0];
                for (int i = 1; i < pool.Count; i++) if (pool[i].edad / Mathf.Max(0.01f, pool[i].dur) > mas.edad / Mathf.Max(0.01f, mas.dur)) mas = pool[i];
                return mas;
            }
            var go = new GameObject("SpriteFx") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            go.transform.SetParent(root, false);
            var fx = go.AddComponent<SpriteFx>();
            fx.sr = go.AddComponent<SpriteRenderer>();
            fx.sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.sr.receiveShadows = false;
            pool.Add(fx);
            return fx;
        }

        public static SpriteFx Lanzar(string sprite, Vector3 pos, Color color, float tam0, float tam1, float dur, float giroGradosPorS = 0f, Vector3 velocidad = default, float freno = 0f, int orden = 0)
        {
            if (!Application.isPlaying) return null;
            var s = SpritesReales.Obtener(sprite);
            if (s == null) return null;
            var fx = Tomar();
            if (fx.sr == null) fx.sr = fx.GetComponent<SpriteRenderer>();
            fx.sr.sprite = s;
            fx.sr.sortingOrder = orden;
            fx.color = color; fx.tam0 = tam0; fx.tam1 = tam1; fx.dur = Mathf.Max(0.02f, dur);
            fx.giro = giroGradosPorS; fx.vel = velocidad; fx.freno = freno; fx.edad = 0f;
            fx.roll = Random.Range(0f, 360f);
            fx.transform.position = pos;
            fx.transform.localScale = Vector3.one * tam0;
            fx.sr.color = color;
            fx.gameObject.SetActive(true);
            fx.Orientar();
            Lanzados++;
            return fx;
        }

        void Orientar()
        {
            var cam = SP.Core.CamaraPrincipal.Actual;
            var rot = cam != null ? cam.transform.rotation : Quaternion.identity;
            transform.rotation = rot * Quaternion.Euler(0f, 0f, roll);
        }

        void Update()
        {
            edad += Time.deltaTime;
            float k = Mathf.Clamp01(edad / dur);
            if (k >= 1f) { gameObject.SetActive(false); return; }
            if (vel.sqrMagnitude > 0f)
            {
                transform.position += vel * Time.deltaTime;
                vel = Vector3.Lerp(vel, Vector3.zero, freno * Time.deltaTime);
            }
            roll += giro * Time.deltaTime;
            // Crece rapido al principio (curva de salida) y se apaga en la segunda mitad.
            float t = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.one * Mathf.Lerp(tam0, tam1, t);
            var c = color; c.a = color.a * (k < 0.15f ? k / 0.15f : Mathf.Clamp01((1f - k) / 0.85f));
            sr.color = c;
            Orientar();
        }

        // ---- Composiciones ----
        static readonly Color Fuego = new Color(1f, 0.55f, 0.12f, 1f), Llama = new Color(1f, 0.85f, 0.35f, 1f), Humo = new Color(0.33f, 0.31f, 0.29f, 0.75f), Tierra = new Color(0.45f, 0.36f, 0.27f, 0.9f);

        // Explosion de granada / obus / carga: destello, bola de fuego, humo que sube, tierra y chispas. radio = radio de dano.
        public static int Explosion(Vector3 pos, float radio)
        {
            int n = 0;
            float r = Mathf.Max(0.5f, radio);
            if (Lanzar("flare_01", pos, new Color(1f, 0.95f, 0.75f, 1f), r * 1.2f, r * 3.2f, 0.16f, 0f, default, 0f, 5) != null) n++;
            if (Lanzar("fire_01", pos, Fuego, r * 0.7f, r * 2.2f, 0.55f, Random.Range(-40f, 40f), default, 0f, 3) != null) n++;
            if (Lanzar("fire_02", pos + Vector3.up * 0.2f, Llama, r * 0.5f, r * 1.5f, 0.4f, Random.Range(-60f, 60f), default, 0f, 4) != null) n++;
            string[] humos = { "smoke_01", "smoke_02", "smoke_04" };
            for (int i = 0; i < 3; i++)
            {
                var off = Random.insideUnitSphere * r * 0.35f; off.y = Mathf.Abs(off.y) * 0.5f;
                if (Lanzar(humos[i], pos + off, Humo, r * 0.9f, r * 2.6f, Random.Range(1.4f, 2.2f), Random.Range(-20f, 20f), Vector3.up * Random.Range(0.8f, 1.6f), 0.4f, 1) != null) n++;
            }
            for (int i = 0; i < 4; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized;
                if (Lanzar(i % 2 == 0 ? "dirt_01" : "dirt_03", pos + Vector3.up * 0.3f, Tierra, r * 0.35f, r * 0.9f, 0.9f, Random.Range(-90f, 90f), dir * Random.Range(3f, 7f), 2.2f, 2) != null) n++;
            }
            for (int i = 0; i < 6; i++)
            {
                var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y);
                if (Lanzar("spark_01", pos, Llama, 0.4f, 0.1f, 0.5f, 0f, dir * Random.Range(6f, 12f), 3f, 6) != null) n++;
            }
            return n;
        }

        // Golpe de bala contra una superficie: chispazo corto + una voluta de polvo. color = tono de la superficie.
        public static void Golpe(Vector3 pos, Color color, float tam)
        {
            Lanzar("spark_01", pos, new Color(1f, 0.9f, 0.6f, 1f), tam * 1.4f, tam * 0.5f, 0.12f, 0f, default, 0f, 6);
            Lanzar("smoke_02", pos, new Color(color.r, color.g, color.b, 0.55f), tam * 0.8f, tam * 2.2f, 0.5f, Random.Range(-30f, 30f), Vector3.up * 0.5f, 1f, 1);
        }
    }
}
