using UnityEngine;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.UI
{
    // Bug #079: carteles de bajas seguidas tipo LoL (DOBLE / TRIPLE / CUADRUPLE / PENTA KILL) y el aviso fijo de la FURIA.
    // Pantalla propia (canvas overlay sobre el HUD, bajo el popup de HEADSHOT). Referencia 1920x1080: el cartel va 300 px sobre el
    // centro, en Black Ops One de 64 pt, entra con escala 1.6 -> 1.0 en 0.18 s, dura 1.6 s y sale con fundido.
    public class CartelDeRacha : MonoBehaviour
    {
        public const float Entrada = 0.18f, Vida = 1.6f, Fundido = 0.4f;
        const float EscalaDeEntrada = 1.6f;
        static CartelDeRacha instancia;
        public static CartelDeRacha Instancia => instancia;

        Text titulo, furia;
        float inicio = -99f;
        int nivel;
        bool furiaVisible;
        string textoFuria = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => instancia = null;

        static CartelDeRacha Asegurar()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("CartelDeRacha");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 58;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var c = go.AddComponent<CartelDeRacha>();
            c.titulo = NuevoTexto(go.transform, "Titulo", 64, new Vector2(0f, 300f), new Vector2(1400f, 110f));
            c.furia = NuevoTexto(go.transform, "Furia", 30, new Vector2(0f, 222f), new Vector2(1400f, 50f));
            c.titulo.gameObject.SetActive(false);
            c.furia.gameObject.SetActive(false);
            instancia = c;
            return c;
        }

        static Text NuevoTexto(Transform padre, string nombre, int tam, Vector2 pos, Vector2 caja)
        {
            var t = new GameObject(nombre, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(padre, false);
            t.raycastTarget = false;
            t.font = tam >= FuentesBelicas.UmbralTitulo ? (FuentesBelicas.Titulo != null ? FuentesBelicas.Titulo : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")) : FuentesBelicas.Texto;
            t.fontSize = tam;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sombra = t.gameObject.AddComponent<Outline>();
            sombra.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sombra.effectDistance = new Vector2(3f, -3f);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = caja;
            return t;
        }

        // Color por nivel: sube de blanco calido a rojo intenso.
        static Color ColorDeNivel(int n) => n <= 2 ? new Color(1f, 0.95f, 0.6f) : n == 3 ? new Color(1f, 0.68f, 0.15f) : n == 4 ? new Color(1f, 0.38f, 0.1f) : new Color(1f, 0.12f, 0.2f);

        public static void Mostrar(string texto, int nivelDeRacha)
        {
            if (!Application.isPlaying) return;
            var c = Asegurar();
            c.titulo.text = texto;
            c.titulo.color = ColorDeNivel(nivelDeRacha);
            c.nivel = nivelDeRacha;
            c.inicio = Time.unscaledTime;
            c.titulo.gameObject.SetActive(true);
            c.Pintar();
        }

        // Aviso fijo mientras dura la FURIA; null/"" lo oculta.
        public static void MostrarFuria(string texto)
        {
            if (!Application.isPlaying) return;
            var c = Asegurar();
            c.textoFuria = texto ?? "";
            c.furiaVisible = !string.IsNullOrEmpty(c.textoFuria);
            c.furia.text = c.textoFuria;
            c.furia.gameObject.SetActive(c.furiaVisible);
        }

        public static bool Visible => instancia != null && instancia.titulo != null && instancia.titulo.gameObject.activeSelf && instancia.titulo.color.a > 0.01f;
        public static string Texto => instancia != null && instancia.titulo != null && instancia.titulo.gameObject.activeSelf ? instancia.titulo.text : "";
        public static bool FuriaVisible => instancia != null && instancia.furia != null && instancia.furia.gameObject.activeSelf;
        public static string TextoFuria => instancia != null && FuriaVisible ? instancia.furia.text : "";
        public static float EscalaActual => instancia != null && instancia.titulo != null ? instancia.titulo.rectTransform.localScale.x : 1f;

        // Para capturas: congela el cartel en el instante t (segundos desde que entro).
        public static float? InstanteForzado;

        void Update()
        {
            if (titulo.gameObject.activeSelf) Pintar();
            if (furiaVisible)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                var col = Color.Lerp(new Color(1f, 0.25f, 0.15f), new Color(1f, 0.7f, 0.3f), p);
                furia.color = col;
            }
        }

        void Pintar()
        {
            float t = InstanteForzado ?? (Time.unscaledTime - inicio);
            if (t >= Vida) { titulo.gameObject.SetActive(false); return; }
            // Entrada: golpe de escala 1.6 -> 1.0 (con salida suave). Un PENTA llega un poco mas fuerte.
            float k = Mathf.Clamp01(t / Entrada);
            float suave = 1f - (1f - k) * (1f - k);
            float desde = nivel >= 5 ? EscalaDeEntrada * 1.15f : EscalaDeEntrada;
            titulo.rectTransform.localScale = Vector3.one * Mathf.Lerp(desde, 1f, suave);
            float alfa = t < Vida - Fundido ? 1f : 1f - (t - (Vida - Fundido)) / Fundido;
            var c = ColorDeNivel(nivel); c.a = Mathf.Clamp01(alfa);
            titulo.color = c;
        }
    }
}
