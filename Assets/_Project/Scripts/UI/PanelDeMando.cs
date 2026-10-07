using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Tutorial;

namespace SP.UI
{
    // WP10 (#101): el panel de INSTRUCCIONES del mando tactico (arriba a la derecha, bajo el minimapa). Una lista corta de pasos con las teclas y botones del mouse
    // dibujados (los keycaps del tutorial): el paso actual en amarillo, los hechos en verde con tilde, los que faltan en gris. Debajo, una franja de aviso
    // (por que el reloj esta detenido, sector caido, alguien cayo...). No es interactivo: solo se mira, el input va por la vista tactica de siempre.
    public class PanelDeMando : MonoBehaviour
    {
        public struct Paso
        {
            public string texto;
            public bool clicIzq, clicDer, arrastre;   // iconos del mouse
            public string[] teclas;                    // teclas del teclado
        }

        public static PanelDeMando Instancia { get; private set; }
        static readonly Color Oro = new Color(1f, 0.86f, 0.22f, 1f), Verde = new Color(0.45f, 1f, 0.55f, 1f), Gris = new Color(0.62f, 0.68f, 0.78f, 1f);
        const float AnchoDelPanel = 580f;

        Canvas lienzo;
        RectTransform panel, lista;
        Text titulo, avisoTexto;
        Image avisoFondo;
        readonly List<Text> textos = new List<Text>();
        readonly List<Text> glifos = new List<Text>();
        readonly List<Image> fondos = new List<Image>();
        string firma = "";
        public int Actual { get; private set; } = -1;
        public int Hechos { get; private set; }
        public string TextoActual { get; private set; } = "";
        public string TextoAviso => avisoTexto != null && avisoFondo != null && avisoFondo.gameObject.activeSelf ? avisoTexto.text : "";
        public bool Visible => lienzo != null && lienzo.gameObject.activeSelf;
        public int Filas => textos.Count;
        public RectTransform RectDelPanel => panel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; }

        public static PanelDeMando Asegurar()
        {
            if (Instancia != null) return Instancia;
            var go = new GameObject("PanelDeMando", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var p = go.AddComponent<PanelDeMando>();
            p.Armar(go);
            Instancia = p;
            return p;
        }

        public static void Quitar()
        {
            if (Instancia == null) return;
            Destroy(Instancia.gameObject);
            Instancia = null;
        }

        void Armar(GameObject go)
        {
            lienzo = go.GetComponent<Canvas>();
            lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
            lienzo.sortingOrder = 60;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;

            var rp = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rp.transform.SetParent(go.transform, false);
            panel = (RectTransform)rp.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-18f, -352f);
            panel.sizeDelta = new Vector2(AnchoDelPanel, 100f);
            rp.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.86f);
            rp.GetComponent<Image>().raycastTarget = false;
            var v = rp.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 10, 12); v.spacing = 4f;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            rp.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titulo = NuevoTexto("Titulo", panel, "DIRIGIR LA DEFENSA", 24, FontStyle.Bold, Oro, TextAnchor.MiddleLeft);
            var lt = titulo.gameObject.AddComponent<LayoutElement>(); lt.preferredHeight = 34f; lt.minHeight = 34f;
            var sub = NuevoTexto("Subtitulo", panel, "Desde la radio mandas a tu equipo con la vista tactica", 16, FontStyle.Normal, Gris, TextAnchor.MiddleLeft);
            var ls = sub.gameObject.AddComponent<LayoutElement>(); ls.preferredHeight = 24f; ls.minHeight = 24f;

            var rl = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rl.transform.SetParent(panel, false);
            lista = (RectTransform)rl.transform;
            var vl = rl.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 3f; vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            // Franja de aviso (debajo de la lista).
            var ra = new GameObject("Aviso", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            ra.transform.SetParent(panel, false);
            avisoFondo = ra.GetComponent<Image>(); avisoFondo.color = new Color(1f, 0.82f, 0.2f, 0.22f); avisoFondo.raycastTarget = false;
            var la = ra.GetComponent<LayoutElement>(); la.minHeight = 40f; la.preferredHeight = 52f;
            avisoTexto = NuevoTexto("Texto", ra.transform, "", 18, FontStyle.Bold, Oro, TextAnchor.MiddleCenter);
            var rt = (RectTransform)avisoTexto.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(8f, 2f); rt.offsetMax = new Vector2(-8f, -2f);
            avisoTexto.horizontalOverflow = HorizontalWrapMode.Wrap; avisoTexto.verticalOverflow = VerticalWrapMode.Overflow;
            ra.SetActive(false);

            // #124: la radio ya no se suelta con [E] (en RTS [E] sube la camara): boton en el panel.
            var rb = new GameObject("BotonSoltarRadio", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            rb.transform.SetParent(panel, false);
            botonSoltar = rb.GetComponent<Button>();
            var ib = rb.GetComponent<Image>(); ib.color = new Color(0.55f, 0.16f, 0.12f, 0.92f);
            var lb = rb.GetComponent<LayoutElement>(); lb.minHeight = 38f; lb.preferredHeight = 38f;
            var tb = NuevoTexto("Texto", rb.transform, "SOLTAR LA RADIO (EL RELOJ SE DETIENE)", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            var rtb = (RectTransform)tb.transform; rtb.anchorMin = Vector2.zero; rtb.anchorMax = Vector2.one; rtb.offsetMin = rtb.offsetMax = Vector2.zero;
            botonSoltar.targetGraphic = ib;
            botonSoltar.onClick.AddListener(() => AlSoltarRadio?.Invoke());
            rb.SetActive(false);
        }

        Button botonSoltar;
        public System.Action AlSoltarRadio;
        public bool BotonSoltarVisible => botonSoltar != null && botonSoltar.gameObject.activeSelf;
        public void MostrarBotonSoltar(bool si) { if (botonSoltar != null && botonSoltar.gameObject.activeSelf != si) botonSoltar.gameObject.SetActive(si); }
        public void PulsarBotonSoltar() { if (botonSoltar != null && botonSoltar.gameObject.activeSelf) botonSoltar.onClick.Invoke(); }

        // Pone la lista de pasos. "hechos" marca los completados; "actual" es el paso en amarillo (-1 = ninguno).
        public void Fijar(IList<Paso> pasos, IList<bool> hechos, int actual)
        {
            if (pasos == null) return;
            string f = ComponerFirma(pasos);
            if (f != firma) { firma = f; Reconstruir(pasos); }
            int n = 0, ultimoHecho = -1;
            for (int i = 0; i < pasos.Count && i < textos.Count; i++) if (hechos != null && i < hechos.Count && hechos[i]) ultimoHecho = i;
            for (int i = 0; i < pasos.Count && i < textos.Count; i++)
            {
                bool hecho = hechos != null && i < hechos.Count && hechos[i];
                bool esActual = i == actual && !hecho;
                if (hecho) n++;
                // Los pasos cumplidos se pliegan (queda solo el ultimo): el panel no tapa la vista tactica cuando ya no hace falta.
                var fila = fondos[i].gameObject;
                bool mostrar = !hecho || i == ultimoHecho;
                if (fila.activeSelf != mostrar) fila.SetActive(mostrar);
                glifos[i].text = hecho ? "✓" : esActual ? "▶" : "•";
                glifos[i].color = hecho ? Verde : esActual ? Oro : Gris;
                textos[i].color = hecho ? Verde : esActual ? Oro : Gris;
                textos[i].fontStyle = esActual ? FontStyle.Bold : FontStyle.Normal;
                fondos[i].color = esActual ? new Color(1f, 0.82f, 0.2f, 0.16f) : new Color(1f, 1f, 1f, 0f);
            }
            Hechos = n; Actual = actual;
            TextoActual = actual >= 0 && actual < pasos.Count ? pasos[actual].texto : "";
        }

        public void Aviso(string texto, Color color)
        {
            if (avisoFondo == null) return;
            bool hay = !string.IsNullOrEmpty(texto);
            avisoFondo.gameObject.SetActive(hay);
            if (!hay) return;
            if (avisoTexto.text != texto) avisoTexto.text = texto;
            avisoTexto.color = color;
            avisoFondo.color = new Color(color.r, color.g, color.b, 0.2f);
        }

        public void Mostrar(bool si) { if (lienzo != null) lienzo.gameObject.SetActive(si); }

        static string ComponerFirma(IList<Paso> pasos)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in pasos)
            {
                sb.Append(p.texto).Append('|').Append(p.clicIzq ? '1' : '0').Append(p.clicDer ? '1' : '0').Append(p.arrastre ? '1' : '0');
                if (p.teclas != null) foreach (var t in p.teclas) sb.Append('+').Append(t);
                sb.Append(';');
            }
            return sb.ToString();
        }

        void Reconstruir(IList<Paso> pasos)
        {
            for (int i = lista.childCount - 1; i >= 0; i--) Destroy(lista.GetChild(i).gameObject);
            textos.Clear(); glifos.Clear(); fondos.Clear();
            for (int i = 0; i < pasos.Count; i++)
            {
                var p = pasos[i];
                var fila = new GameObject("Paso_" + (i + 1), typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                fila.transform.SetParent(lista, false);
                var im = fila.GetComponent<Image>(); im.raycastTarget = false; fondos.Add(im);
                var h = fila.GetComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(6, 6, 4, 4); h.spacing = 8f; h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

                var g = NuevoTexto("Glifo", fila.transform, "•", 22, FontStyle.Bold, Gris, TextAnchor.MiddleCenter);
                var lg = g.gameObject.AddComponent<LayoutElement>(); lg.minWidth = lg.preferredWidth = 24f; lg.flexibleWidth = 0f;
                glifos.Add(g);

                // Teclas y mouse.
                var cj = new GameObject("Teclas", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                cj.transform.SetParent(fila.transform, false);
                var hk = cj.GetComponent<HorizontalLayoutGroup>(); hk.spacing = 3f; hk.childAlignment = TextAnchor.MiddleLeft;
                hk.childControlWidth = true; hk.childControlHeight = true; hk.childForceExpandWidth = false; hk.childForceExpandHeight = false;
                var lk = cj.GetComponent<LayoutElement>(); lk.flexibleWidth = 0f;
                if (p.clicIzq) Mouse(cj.transform, 1);
                if (p.arrastre) Mouse(cj.transform, 3);
                if (p.clicDer) Mouse(cj.transform, 2);
                if (p.teclas != null) foreach (var t in p.teclas) Tecla(cj.transform, t);
                if (!p.clicIzq && !p.clicDer && !p.arrastre && (p.teclas == null || p.teclas.Length == 0)) Destroy(cj);

                var t2 = NuevoTexto("Texto", fila.transform, p.texto, 19, FontStyle.Normal, Gris, TextAnchor.MiddleLeft);
                t2.horizontalOverflow = HorizontalWrapMode.Wrap; t2.verticalOverflow = VerticalWrapMode.Overflow;
                var lt = t2.gameObject.AddComponent<LayoutElement>(); lt.flexibleWidth = 1f; lt.minWidth = 0f;
                textos.Add(t2);
            }
        }

        static void Mouse(Transform padre, int modo)
        {
            var go = new GameObject("Mouse" + modo, typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(padre, false);
            var raw = go.GetComponent<RawImage>(); raw.texture = TutorialTextures.Mouse(modo); raw.raycastTarget = false;
            var le = go.GetComponent<LayoutElement>(); float alto = 34f; le.minHeight = le.preferredHeight = alto; le.minWidth = le.preferredWidth = alto * 56f / 84f;
        }

        static void Tecla(Transform padre, string tok)
        {
            float ancho = Mathf.Max(30f, Mathf.Round(14f + 9f * tok.Length));
            var go = new GameObject("Tecla_" + tok, typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(padre, false);
            var raw = go.GetComponent<RawImage>(); raw.texture = TutorialTextures.Tecla((int)ancho, 30, TutorialTextures.Estado.Normal); raw.raycastTarget = false;
            var le = go.GetComponent<LayoutElement>(); le.minWidth = le.preferredWidth = ancho; le.minHeight = le.preferredHeight = 30f;
            var t = NuevoTexto("Etiqueta", go.transform, tok, tok.Length > 3 ? 14 : 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            var rt = (RectTransform)t.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static Text NuevoTexto(string nombre, Transform padre, string s, int size, FontStyle estilo, Color color, TextAnchor ancla)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size; t.fontStyle = estilo; t.color = color; t.alignment = ancla; t.text = s; t.raycastTarget = false;
            return t;
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }
    }
}
