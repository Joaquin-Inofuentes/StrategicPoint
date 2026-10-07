using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Tutorial;

namespace SP.UI
{
    // Tabla de CONTROLES de la pausa (bug #100): reemplaza el bloque de texto unico de ~110 lineas (que se cortaba y se pisaba) por
    // una lista con scroll: un encabezado por contexto y, por cada atajo, una fila con las teclas dibujadas (un keycap por tecla,
    // el mouse con su icono) a la izquierda y la descripcion con salto de linea a la derecha. Sale de ControlsTable (misma fuente de
    // verdad que el cartel contextual). Se arma una sola vez por apertura SOLO si cambio el idioma o alguna tecla remapeada.
    public static class TablaDeControles
    {
        public const string Nombre = "TablaDeControles";
        public const float AnchoTeclas = 180f;
        const float AltoTecla = 30f, AltoMouse = 32f, Separacion = 4f;
        const int FuenteDescripcion = 16;

        static readonly Color FilaA = new Color(1f, 1f, 1f, 0.035f), FilaB = new Color(1f, 1f, 1f, 0f);
        static readonly Color Oro = new Color(1f, 0.82f, 0.3f, 1f);
        const string ClicDer = "Clic der.";
        const string Marca = "\u0001";

        static Font Fuente => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Cambia si cambia el idioma o alguna tecla de las que muestra la tabla (remapeo): se usa para decidir si reconstruir.
        public static int Firma()
        {
            int h = SP.Core.Loc.Actual == SP.Core.Idioma.En ? 7 : 3;
            foreach (var ctx in ControlsTable.AllContexts)
                foreach (var e in ControlsTable.For(ctx))
                    h = h * 31 + (e.ActionId != null ? SP.Player.KeyBindings.DisplayName(e.ActionId).GetHashCode() : 1);
            return h;
        }

        public static RectTransform Existente(RectTransform contenedor)
        {
            var t = contenedor != null ? contenedor.Find(Nombre) : null;
            return t as RectTransform;
        }

        // Contenido (el Content del ScrollRect) o null si no esta armada.
        public static RectTransform Contenido(RectTransform contenedor)
        {
            var t = Existente(contenedor);
            if (t == null) return null;
            var sr = t.GetComponent<ScrollRect>();
            return sr != null ? sr.content : null;
        }

        // Arma (o rearma) la tabla dentro del contenedor, con margenes arriba/abajo/lados. El contenedor tiene que estar ACTIVO para
        // que el layout calcule ahora mismo los tamanos.
        public static RectTransform Construir(RectTransform contenedor, float arriba = 60f, float abajo = 76f, float lados = 20f)
        {
            if (contenedor == null) return null;
            var previa = Existente(contenedor);
            if (previa != null)
            {
                if (Application.isPlaying) Object.Destroy(previa.gameObject); else Object.DestroyImmediate(previa.gameObject);
                // Destroy es diferido: se la saca del camino para que Find no la devuelva mas.
                previa.name = Nombre + "_vieja";
                previa.gameObject.SetActive(false);
            }

            var raiz = Rect(Nombre, contenedor);
            raiz.anchorMin = Vector2.zero; raiz.anchorMax = Vector2.one;
            raiz.offsetMin = new Vector2(lados, abajo); raiz.offsetMax = new Vector2(-lados, -arriba);
            var fondoRaiz = raiz.gameObject.AddComponent<Image>();
            fondoRaiz.color = new Color(0f, 0f, 0f, 0.001f); fondoRaiz.raycastTarget = true;   // para recibir la rueda del mouse

            var viewport = Rect("Viewport", raiz);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(-14f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f; vlg.padding = new RectOffset(4, 4, 4, 12);
            vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Barra de desplazamiento a la derecha.
            var barra = Rect("Barra", raiz);
            barra.anchorMin = new Vector2(1f, 0f); barra.anchorMax = new Vector2(1f, 1f); barra.pivot = new Vector2(1f, 0.5f);
            barra.offsetMin = new Vector2(-8f, 0f); barra.offsetMax = Vector2.zero;
            var pista = barra.gameObject.AddComponent<Image>(); pista.color = new Color(1f, 1f, 1f, 0.08f);
            var zona = Rect("Zona", barra); Estirar(zona);
            var asa = Rect("Asa", zona); Estirar(asa);
            var asaIm = asa.gameObject.AddComponent<Image>(); asaIm.color = new Color(1f, 0.82f, 0.3f, 0.7f);
            var sb = barra.gameObject.AddComponent<Scrollbar>();
            sb.handleRect = asa; sb.targetGraphic = asaIm; sb.direction = Scrollbar.Direction.BottomToTop;

            var sr = raiz.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport; sr.content = content; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f; sr.inertia = true;
            sr.verticalScrollbar = sb; sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            bool alterna = false;
            foreach (var ctx in ControlsTable.AllContexts)
            {
                Encabezado(content, SP.Core.Loc.T(ControlsTable.HeaderFor(ctx)));
                alterna = false;
                foreach (var e in ControlsTable.For(ctx))
                {
                    Fila(content, e, alterna);
                    alterna = !alterna;
                }
            }

            ultimaFirma[contenedor] = Firma();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            sr.verticalNormalizedPosition = 1f;
            return raiz;
        }

        static readonly Dictionary<RectTransform, int> ultimaFirma = new Dictionary<RectTransform, int>();

        // Construye solo si no existe o si cambio el idioma / alguna tecla.
        public static RectTransform Asegurar(RectTransform contenedor, float arriba = 60f, float abajo = 76f, float lados = 20f)
        {
            var actual = Existente(contenedor);
            if (actual != null && ultimaFirma.TryGetValue(contenedor, out var f) && f == Firma()) return actual;
            return Construir(contenedor, arriba, abajo, lados);
        }

        // ------------------------------------------------------------------ piezas
        static void Encabezado(Transform padre, string texto)
        {
            var rt = Rect("Encabezado", padre);
            var im = rt.gameObject.AddComponent<Image>(); im.color = new Color(1f, 0.82f, 0.3f, 0.16f); im.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 32f; le.minHeight = 32f;
            var t = Texto("Texto", rt, texto, 18, FontStyle.Bold, Oro, TextAnchor.MiddleLeft);
            var trt = (RectTransform)t.transform; Estirar(trt); trt.offsetMin = new Vector2(10f, 0f); trt.offsetMax = new Vector2(-6f, 0f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static void Fila(Transform padre, ControlEntry e, bool alterna)
        {
            var rt = Rect("Fila", padre);
            var im = rt.gameObject.AddComponent<Image>(); im.color = alterna ? FilaA : FilaB; im.raycastTarget = false;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 4, 4); h.spacing = 14f; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

            var teclas = Rect("Teclas", rt);
            var hk = teclas.gameObject.AddComponent<HorizontalLayoutGroup>();
            hk.spacing = Separacion; hk.childAlignment = TextAnchor.MiddleLeft;
            hk.childControlWidth = true; hk.childControlHeight = true; hk.childForceExpandWidth = false; hk.childForceExpandHeight = false;
            var lk = teclas.gameObject.AddComponent<LayoutElement>(); lk.minWidth = AnchoTeclas; lk.preferredWidth = AnchoTeclas; lk.flexibleWidth = 0f;
            string crudo = e.ActionId != null ? SP.Player.KeyBindings.DisplayName(e.ActionId) : e.Key;
            foreach (var tok in Tokens(crudo)) Tecla(teclas, tok);

            var d = Texto("Descripcion", rt, SP.Core.Loc.T(e.Description), FuenteDescripcion, FontStyle.Normal, new Color(0.9f, 0.93f, 0.98f, 1f), TextAnchor.MiddleLeft);
            d.horizontalOverflow = HorizontalWrapMode.Wrap; d.verticalOverflow = VerticalWrapMode.Overflow;
            var ld = d.gameObject.AddComponent<LayoutElement>(); ld.flexibleWidth = 1f; ld.minWidth = 0f;
        }

        // "Shift+Clic der." -> Shift, Clic der.; "F1 / F2 / F3" -> F1, F2, F3; "[ ]" -> [, ]; "Ctrl+4..9" -> Ctrl, 4..9.
        public static List<string> Tokens(string crudo)
        {
            var r = new List<string>();
            if (string.IsNullOrEmpty(crudo)) return r;
            string s = crudo.Replace(ClicDer, Marca);
            foreach (var p in s.Split(new[] { '/', '+', ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                r.Add(Traducir(p == Marca ? ClicDer : p));
            return r;
        }

        static string Traducir(string t)
        {
            switch (t.ToUpperInvariant())
            {
                case "SPACE": return "ESPACIO";
                case "BACKQUOTE": return "`";
                case "COMMA": return ",";
                case "PERIOD": return ".";
                case "SEMICOLON": return ";";
                case "MINUS": return "-";
                case "EQUALS": return "=";
                case "SLASH": return "/";
                case "BACKSLASH": return "\\";
                case "QUOTE": return "'";
                case "LEFTBRACKET": return "[";
                case "RIGHTBRACKET": return "]";
                case "LEFTSHIFT": case "RIGHTSHIFT": return "Shift";
                case "LEFTCTRL": case "RIGHTCTRL": return "Ctrl";
                case "LEFTALT": case "RIGHTALT": return "Alt";
                case "ESCAPE": return "ESC";
                case "TAB": return "TAB";
            }
            if (t.Length == 6 && t.StartsWith("DIGIT", System.StringComparison.OrdinalIgnoreCase)) return t.Substring(5);
            return t;
        }

        static void Tecla(Transform padre, string tok)
        {
            int mouse = tok == "Clic" ? 1 : tok == ClicDer ? 2 : (tok == "Rueda" || tok == "Mouse") ? 3 : 0;
            var rt = Rect("Tecla", padre);
            var raw = rt.gameObject.AddComponent<RawImage>(); raw.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            if (mouse > 0)
            {
                raw.texture = TutorialTextures.Mouse(mouse);
                float h = AltoMouse, w = AltoMouse * 56f / 84f;
                le.minWidth = le.preferredWidth = w; le.minHeight = le.preferredHeight = h;
                rt.name = "Tecla_Mouse" + mouse;
                return;
            }
            float ancho = Mathf.Max(30f, Mathf.Round(14f + 9f * tok.Length));
            raw.texture = TutorialTextures.Tecla((int)ancho, (int)AltoTecla, TutorialTextures.Estado.Normal);
            le.minWidth = le.preferredWidth = ancho; le.minHeight = le.preferredHeight = AltoTecla;
            rt.name = "Tecla_" + tok;
            var t = Texto("Etiqueta", rt, tok, tok.Length > 3 ? 14 : 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Estirar((RectTransform)t.transform);
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static RectTransform Rect(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            return (RectTransform)go.transform;
        }

        static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Text Texto(string nombre, Transform padre, string s, int size, FontStyle estilo, Color color, TextAnchor ancla)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = Fuente; t.fontSize = size; t.fontStyle = estilo; t.color = color; t.alignment = ancla; t.text = s; t.raycastTarget = false;
            return t;
        }
    }
}
