using UnityEngine;
using UnityEngine.UI;
using SP.Combat;

namespace SP.UI
{
    // La mirilla que se ve al mantener el clic derecho (apuntar con zoom).
    //
    // Antes el "zoom" agrandaba x4 el anillo de la mira (quedaba enorme y
    // borroso) y difuminaba el fondo; ahora el zoom es REAL (el FOV sale del
    // aumento de cada arma, ver WeaponCatalog.Spec.ZoomFactor) y se dibuja una
    // reticula NITIDA, distinta para cada arma:
    //
    //   Punto        fusil            punto rojo dentro de un anillo fino
    //   Cruz         pistola          cruz con hueco central
    //   Anillo       ametralladora    anillo grueso con muescas
    //   Chevron      metralleta       cheurón invertido
    //   Mildot       lanzacohetes     cruz con puntos de estima
    //   Circulo      escopeta         circulo ancho de dispersion
    //   Telescopica  francotirador    cruz fina + tubo negro alrededor
    //
    // Las texturas se generan por codigo (256 px, borde suavizado) para que no
    // dependan de ningun asset y se vean nitidas sin importar la escala.
    public class MirillaView : MonoBehaviour
    {
        const int Lado = 256;

        static Sprite[] sprites;
        static Sprite mascara;

        Image reticula;
        Image tubo;
        CanvasGroup grupo;
        float alfa;

        public bool Visible => alfa > 0.02f;
        public ReticleStyle Estilo { get; private set; }
        public float Alfa => alfa;
        public Image Reticula => reticula;

        public static MirillaView Instancia { get; private set; }

        public static MirillaView Asegurar(Transform canvasRoot)
        {
            if (Instancia != null) return Instancia;
            if (canvasRoot == null) return null;
            var t = canvasRoot.Find("Mirilla");
            if (t != null)
            {
                Instancia = t.GetComponent<MirillaView>();
                if (Instancia != null) return Instancia;
            }

            var go = new GameObject("Mirilla", typeof(RectTransform), typeof(CanvasGroup), typeof(MirillaView));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var m = go.GetComponent<MirillaView>();
            m.grupo = go.GetComponent<CanvasGroup>();
            m.grupo.alpha = 0f; m.grupo.interactable = false; m.grupo.blocksRaycasts = false;

            // Tubo negro (solo la telescopica): un cuadrado enorme con un agujero circular.
            var tuboGO = new GameObject("Tubo", typeof(RectTransform), typeof(Image));
            tuboGO.transform.SetParent(go.transform, false);
            m.tubo = tuboGO.GetComponent<Image>();
            m.tubo.sprite = Mascara();
            m.tubo.color = Color.black;
            m.tubo.raycastTarget = false;
            var trt = (RectTransform)tuboGO.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(3600f, 3600f);   // agujero de 500 unidades de diametro (ver Mascara)

            var retGO = new GameObject("Reticula", typeof(RectTransform), typeof(Image));
            retGO.transform.SetParent(go.transform, false);
            m.reticula = retGO.GetComponent<Image>();
            m.reticula.raycastTarget = false;
            var rrt = (RectTransform)retGO.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(260f, 260f);

            Instancia = m;
            return m;
        }

        // zoom: si se esta apuntando. tinte: blanco/rojo/verde segun a que se apunta.
        public void Actualizar(bool zoom, ReticleStyle estilo, Color tinte)
        {
            if (estilo != Estilo || reticula.sprite == null)
            {
                Estilo = estilo;
                reticula.sprite = SpriteDe(estilo);
                bool scope = estilo == ReticleStyle.Telescopica;
                float lado = scope ? 480f : (estilo == ReticleStyle.Mildot ? 300f : 230f);
                reticula.rectTransform.sizeDelta = new Vector2(lado, lado);
                tubo.gameObject.SetActive(scope || estilo == ReticleStyle.Mildot);
                // El lanzacohetes lleva un tubo mas suave (visor, no tubo cerrado).
                tubo.color = scope ? Color.black : new Color(0f, 0f, 0f, 0.55f);
                float agujero = scope ? 500f : 300f;
                tubo.rectTransform.sizeDelta = Vector2.one * (3600f * agujero / 500f);
            }
            reticula.color = tinte;
            alfa = Mathf.MoveTowards(alfa, zoom ? 1f : 0f, Time.unscaledDeltaTime * 10f);
            grupo.alpha = alfa;
        }

        public void Ocultar()
        {
            alfa = 0f;
            if (grupo != null) grupo.alpha = 0f;
        }

        // ------------------------------------------------------------------
        // Texturas
        // ------------------------------------------------------------------
        static Sprite SpriteDe(ReticleStyle e)
        {
            if (sprites == null) sprites = new Sprite[System.Enum.GetValues(typeof(ReticleStyle)).Length];
            int i = (int)e;
            if (sprites[i] == null) sprites[i] = Generar(e);
            return sprites[i];
        }

        // Agujero circular de 500 unidades de diametro cuando la imagen mide 3600.
        static Sprite Mascara()
        {
            if (mascara != null) return mascara;
            const int n = 1024;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float r = n * (250f / 3600f);
            var c = new Vector2(n * 0.5f, n * 0.5f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01((d - r) / 1.2f + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            mascara = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            mascara.hideFlags = HideFlags.HideAndDontSave;
            return mascara;
        }

        // Rasterizador minimo con cobertura por distancia (anti-alias de ~1 px).
        static float Cov(float d, float grosor) => Mathf.Clamp01(grosor * 0.5f - d + 0.5f);

        static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        static Sprite Generar(ReticleStyle estilo)
        {
            var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[Lado * Lado];
            float h = Lado * 0.5f;
            for (int y = 0; y < Lado; y++)
                for (int x = 0; x < Lado; x++)
                {
                    var p = new Vector2(x + 0.5f - h, y + 0.5f - h);   // centro en (0,0), unidades = px de la textura
                    float a = Pintar(estilo, p, h);
                    px[y * Lado + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, Lado, Lado), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sp.name = "Mirilla_" + estilo;
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        static float Pintar(ReticleStyle e, Vector2 p, float h)
        {
            float r = p.magnitude;
            float a = 0f;
            switch (e)
            {
                case ReticleStyle.Punto:
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.5f), 2.4f));                       // anillo fino
                    a = Mathf.Max(a, Cov(r, 9f));                                               // punto central
                    foreach (var d in Cuatro()) a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.5f, d * h * 0.72f), 2.4f)); // patillas
                    break;
                case ReticleStyle.Cruz:
                    foreach (var d in Cuatro()) a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.12f, d * h * 0.62f), 2.6f));
                    a = Mathf.Max(a, Cov(r, 4f));
                    break;
                case ReticleStyle.Anillo:
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.6f), 5f));
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.18f), 2f));
                    a = Mathf.Max(a, Cov(r, 6f));
                    foreach (var d in Cuatro()) a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.6f, d * h * 0.85f), 5f));
                    break;
                case ReticleStyle.Chevron:
                    a = Mathf.Max(a, Cov(DistSegmento(p, new Vector2(0, h * 0.02f), new Vector2(-h * 0.34f, -h * 0.3f)), 4f));
                    a = Mathf.Max(a, Cov(DistSegmento(p, new Vector2(0, h * 0.02f), new Vector2(h * 0.34f, -h * 0.3f)), 4f));
                    a = Mathf.Max(a, Cov(r, 6f));
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.72f), 2f));
                    break;
                case ReticleStyle.Mildot:
                    foreach (var d in Cuatro()) a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.1f, d * h * 0.9f), 2f));
                    foreach (var d in Cuatro())
                        for (int k = 1; k <= 4; k++) a = Mathf.Max(a, Cov(Vector2.Distance(p, d * h * (0.1f + 0.2f * k)), 6f));
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.94f), 2.4f));
                    break;
                case ReticleStyle.Circulo:
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.85f), 3f));
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.85f, d * h * 0.7f), 3f));
                    }
                    a = Mathf.Max(a, Cov(r, 5f));
                    break;
                case ReticleStyle.Telescopica:
                    // Cruz fina de borde a borde con postes gruesos en el exterior.
                    foreach (var d in Cuatro())
                    {
                        a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.05f, d * h * 0.98f), 1.6f));
                        a = Mathf.Max(a, Cov(DistSegmento(p, d * h * 0.55f, d * h * 0.98f), 6f));
                        for (int k = 1; k <= 4; k++) a = Mathf.Max(a, Cov(Vector2.Distance(p, d * h * (0.1f + 0.09f * k)), 3.6f));
                    }
                    a = Mathf.Max(a, Cov(r, 3f));
                    a = Mathf.Max(a, Cov(Mathf.Abs(r - h * 0.985f), 3f));
                    break;
            }
            return a;
        }

        static Vector2[] cuatro;
        static Vector2[] Cuatro() => cuatro ??= new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    }
}
