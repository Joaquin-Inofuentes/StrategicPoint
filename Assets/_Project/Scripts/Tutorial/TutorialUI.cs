using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.Tutorial
{
    // Cuadro de dialogo del tutorial (columna derecha, debajo del minimapa):
    //
    //   TUTORIAL · PASO 3 / 17
    //   DISPARAR                      <- titulo del paso
    //   [=========-------]            <- avance de TODO el tutorial
    //   Instruccion actual (la elige TutorialManager segun las banderas)
    //   [ ] sub-paso 1   [x] sub-paso 2 ...   <- casillas (RawImage)
    //   [W][A][S][D] / mouse / teclas   <- KeyCapView (RawImage que se iluminan)
    //   PISTA: ...                       <- aparece si el jugador se traba
    //
    // Se arma por codigo bajo el Canvas del juego: no depende de la escena.
    public class TutorialUI : MonoBehaviour
    {
        public const string Nombre = "TutorialUI";
        const int MaxSubs = 3;

        Font fuente;
        RectTransform panel, teclasRaiz, barraRelleno;
        Image panelFondo, borde, destello;
        Text cabecera, titulo, instruccion, pista, banner;
        readonly Text[] subTextos = new Text[MaxSubs];
        readonly RawImage[] subCasillas = new RawImage[MaxSubs];
        readonly List<KeyCapView> teclas = new List<KeyCapView>();
        float pop, destelloT, bannerT;
        Vector2 posBase;

        // Para las pruebas y el log: lo que el jugador esta viendo.
        public string TextoInstruccion => instruccion != null ? instruccion.text : "";
        public string TextoTitulo => titulo != null ? titulo.text : "";
        public string TextoCabecera => cabecera != null ? cabecera.text : "";
        public string TextoPista => pista != null ? pista.text : "";
        public string TextoBanner => banner != null && bannerT > 0f ? banner.text : "";
        public IReadOnlyList<KeyCapView> Teclas => teclas;
        public int SubsVisibles { get; private set; }
        public RectTransform Panel => panel;

        public static TutorialUI Crear(Transform canvas)
        {
            var existente = canvas.Find(Nombre);
            if (existente != null) Destroy(existente.gameObject);

            var raiz = new GameObject(Nombre, typeof(RectTransform));
            raiz.transform.SetParent(canvas, false);
            var rrt = (RectTransform)raiz.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            var ui = raiz.AddComponent<TutorialUI>();
            ui.Construir(rrt);
            return ui;
        }

        Text NuevoTexto(string nombre, Transform padre, int tam, FontStyle estilo, Color color, TextAnchor ancla)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = fuente; t.fontSize = tam; t.fontStyle = estilo; t.color = color; t.alignment = ancla;
            t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Anclar(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        void Construir(RectTransform raiz)
        {
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- Panel ----
            var pgo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pgo.transform.SetParent(raiz, false);
            panel = (RectTransform)pgo.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            posBase = new Vector2(-12f, -128f);
            panel.anchoredPosition = posBase;
            panel.sizeDelta = new Vector2(356f, 260f);
            panelFondo = pgo.GetComponent<Image>();
            panelFondo.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);
            panelFondo.raycastTarget = false;

            var bgo = new GameObject("Borde", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgo.transform.SetParent(panel, false);
            var brt = (RectTransform)bgo.transform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(0f, 4f);
            borde = bgo.GetComponent<Image>();
            borde.color = new Color(0.30f, 0.85f, 1f);
            borde.raycastTarget = false;

            cabecera = NuevoTexto("Cabecera", panel, 12, FontStyle.Bold, new Color(0.30f, 0.85f, 1f), TextAnchor.UpperLeft);
            Anclar(cabecera.rectTransform, 14, 8, 328, 16);
            titulo = NuevoTexto("Titulo", panel, 19, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
            Anclar(titulo.rectTransform, 14, 22, 328, 28);

            // Barra de avance de todo el tutorial.
            var fondoBarra = new GameObject("Barra", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fondoBarra.transform.SetParent(panel, false);
            Anclar((RectTransform)fondoBarra.transform, 14, 52, 328, 5);
            fondoBarra.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            fondoBarra.GetComponent<Image>().raycastTarget = false;
            var rgo = new GameObject("Relleno", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rgo.transform.SetParent(fondoBarra.transform, false);
            barraRelleno = (RectTransform)rgo.transform;
            barraRelleno.anchorMin = Vector2.zero; barraRelleno.anchorMax = new Vector2(0f, 1f);
            barraRelleno.pivot = new Vector2(0f, 0.5f); barraRelleno.offsetMin = barraRelleno.offsetMax = Vector2.zero;
            rgo.GetComponent<Image>().color = new Color(0.30f, 0.86f, 0.46f);
            rgo.GetComponent<Image>().raycastTarget = false;

            instruccion = NuevoTexto("Instruccion", panel, 15, FontStyle.Normal, new Color(0.94f, 0.96f, 1f), TextAnchor.UpperLeft);
            Anclar(instruccion.rectTransform, 14, 58, 328, 52);

            for (int i = 0; i < MaxSubs; i++)
            {
                var cgo = new GameObject("Casilla" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                cgo.transform.SetParent(panel, false);
                Anclar((RectTransform)cgo.transform, 16, 113 + i * 20, 16, 16);
                subCasillas[i] = cgo.GetComponent<RawImage>();
                subCasillas[i].raycastTarget = false;
                subCasillas[i].texture = TutorialTextures.Casilla(false);
                subTextos[i] = NuevoTexto("Sub" + i, panel, 13, FontStyle.Normal, new Color(0.82f, 0.86f, 0.94f), TextAnchor.MiddleLeft);
                subTextos[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                Anclar(subTextos[i].rectTransform, 38, 112 + i * 20, 304, 18);
            }

            var tgo = new GameObject("Teclas", typeof(RectTransform));
            tgo.transform.SetParent(panel, false);
            teclasRaiz = (RectTransform)tgo.transform;
            Anclar(teclasRaiz, 14, 168, 328, 90);

            pista = NuevoTexto("Pista", panel, 12, FontStyle.Bold, new Color(1f, 0.86f, 0.35f), TextAnchor.LowerLeft);
            Anclar(pista.rectTransform, 14, 259, 328, 24);

            // ---- Destello de paso completo (toda la pantalla, verde suave) ----
            var dgo = new GameObject("Destello", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dgo.transform.SetParent(raiz, false);
            var drt = (RectTransform)dgo.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
            destello = dgo.GetComponent<Image>();
            destello.color = new Color(0.3f, 1f, 0.5f, 0f);
            destello.raycastTarget = false;

            // ---- Cartel grande "PASO COMPLETADO" ----
            banner = NuevoTexto("Banner", raiz, 40, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            banner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            banner.rectTransform.anchoredPosition = new Vector2(-190f, -70f);
            banner.rectTransform.sizeDelta = new Vector2(560f, 80f);
            banner.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sombra = banner.gameObject.AddComponent<Shadow>();
            sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sombra.effectDistance = new Vector2(2f, -2f);
            banner.gameObject.SetActive(false);
        }

        // ---- API ----
        public void MostrarPaso(int indice, int total, string tituloPaso, string[] textosSub, string layoutTeclas, Color acento)
        {
            cabecera.text = $"TUTORIAL · PASO {indice + 1} / {total}";
            cabecera.color = acento;
            borde.color = acento;
            titulo.text = tituloPaso;

            SubsVisibles = Mathf.Min(MaxSubs, textosSub != null ? textosSub.Length : 0);
            for (int i = 0; i < MaxSubs; i++)
            {
                bool ver = i < SubsVisibles;
                subCasillas[i].gameObject.SetActive(ver);
                subTextos[i].gameObject.SetActive(ver);
                if (ver) { subTextos[i].text = textosSub[i]; subCasillas[i].texture = TutorialTextures.Casilla(false); subTextos[i].color = new Color(0.82f, 0.86f, 0.94f); }
            }
            ArmarTeclas(layoutTeclas);
            pista.text = "";
            pop = 1f;

            // Alto del cuadro segun el contenido (asi no tapa los paneles del HUD).
            float y = 112f;
            for (int i = 0; i < SubsVisibles; i++)
            {
                Anclar((RectTransform)subCasillas[i].transform, 16, y + 1 + i * 20, 16, 16);
                Anclar(subTextos[i].rectTransform, 38, y + i * 20, 304, 18);
            }
            y += SubsVisibles * 20 + (SubsVisibles > 0 ? 6 : 0);
            bool cluster = layoutTeclas == "W A S D";
            float altoTeclas = string.IsNullOrEmpty(layoutTeclas) ? 0f : (cluster ? 90f : 50f);
            Anclar(teclasRaiz, 14, y, 328, altoTeclas);
            y += altoTeclas + 2f;
            Anclar(pista.rectTransform, 14, y, 328, 26);
            panel.sizeDelta = new Vector2(356f, y + 30f);
        }

        public void Refrescar(string textoInstruccion, bool[] hechas, string[] textosSub, string textoPista, float progresoTotal)
        {
            if (instruccion.text != textoInstruccion) { instruccion.text = textoInstruccion; pop = Mathf.Max(pop, 0.5f); }
            for (int i = 0; i < SubsVisibles; i++)
            {
                bool h = hechas != null && i < hechas.Length && hechas[i];
                if (textosSub != null && i < textosSub.Length && subTextos[i].text != textosSub[i]) subTextos[i].text = textosSub[i];
                subCasillas[i].texture = TutorialTextures.Casilla(h);
                subTextos[i].color = h ? new Color(0.55f, 0.95f, 0.68f) : new Color(0.82f, 0.86f, 0.94f);
            }
            pista.text = textoPista ?? "";
            barraRelleno.anchorMax = new Vector2(Mathf.Clamp01(progresoTotal), 1f);
        }

        // Teclas del paso: "W A S D" arma el cluster de movimiento; el resto
        // es una fila de fichas separadas por espacios ("Shift + RMB").
        void ArmarTeclas(string layout)
        {
            foreach (var t in teclas) if (t != null) Destroy(t.gameObject);
            teclas.Clear();
            for (int i = teclasRaiz.childCount - 1; i >= 0; i--) Destroy(teclasRaiz.GetChild(i).gameObject);
            if (string.IsNullOrEmpty(layout)) return;

            var fichas = layout.Split(' ');
            bool cluster = layout == "W A S D";
            if (cluster)
            {
                // Distribucion de teclado: W arriba, A S D abajo.
                float cx = teclasRaiz.sizeDelta.x * 0.5f;
                Poner("W", cx, 22f); Poner("A", cx - 48f, 68f); Poner("S", cx, 68f); Poner("D", cx + 48f, 68f);
                return;
            }

            float ancho = 0f;
            var anchos = new float[fichas.Length];
            for (int i = 0; i < fichas.Length; i++)
            {
                string f = fichas[i];
                anchos[i] = f == "+" ? 18f : (f == "MOUSE" || f == "LMB" || f == "RMB") ? 34f : (f == "Shift" || f == "Ctrl" || f == "Tab" || f == "Espacio") ? 68f : 42f;
                ancho += anchos[i] + 10f;
            }
            ancho -= 10f;
            float x = (teclasRaiz.sizeDelta.x - ancho) * 0.5f;
            for (int i = 0; i < fichas.Length; i++)
            {
                if (fichas[i] == "+")
                {
                    var plus = NuevoTexto("Mas", teclasRaiz, 26, FontStyle.Bold, new Color(0.7f, 0.76f, 0.88f), TextAnchor.MiddleCenter);
                    var prt = plus.rectTransform;
                    prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
                    prt.anchoredPosition = new Vector2(x + 10f, 0f); prt.sizeDelta = new Vector2(24f, 36f);
                    plus.text = "+";
                }
                else Poner(fichas[i], x + anchos[i] * 0.5f, 25f);
                x += anchos[i] + 10f;
            }
        }

        void Poner(string token, float x, float yDesdeArriba)
        {
            var k = KeyCapView.Crear(teclasRaiz, token, fuente);
            var rt = (RectTransform)k.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -yDesdeArriba);
            teclas.Add(k);
        }

        public bool TeclaEncendida(string token)
        {
            foreach (var k in teclas) if (k != null && k.Token == token) return k.Encendida;
            return false;
        }

        public bool TeclaHecha(string token)
        {
            foreach (var k in teclas) if (k != null && k.Token == token) return k.Hecha;
            return false;
        }

        public void OcultarPanel() { if (panel != null) panel.gameObject.SetActive(false); }

        public void DestelloDePaso(string texto, Color color)
        {
            destelloT = 1f;
            destello.color = new Color(color.r, color.g, color.b, 0.28f);
            Mensaje(texto, color);
        }

        public void Mensaje(string texto, Color color)
        {
            banner.text = texto;
            banner.color = color;
            banner.gameObject.SetActive(true);
            bannerT = 2.2f;
        }

        void Update()
        {
            pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 3f);
            float k = 1f + pop * 0.045f;
            panel.localScale = new Vector3(k, k, 1f);

            if (destelloT > 0f)
            {
                destelloT = Mathf.MoveTowards(destelloT, 0f, Time.unscaledDeltaTime * 2.2f);
                var c = destello.color; c.a = destelloT * 0.28f; destello.color = c;
            }
            if (bannerT > 0f)
            {
                bannerT -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(bannerT / 0.6f);
                var c = banner.color; c.a = a; banner.color = c;
                float e = 1f + Mathf.Clamp01((bannerT - 1.8f) / 0.4f) * 0.25f;
                banner.transform.localScale = new Vector3(e, e, 1f);
                if (bannerT <= 0f) banner.gameObject.SetActive(false);
            }
        }
    }
}
