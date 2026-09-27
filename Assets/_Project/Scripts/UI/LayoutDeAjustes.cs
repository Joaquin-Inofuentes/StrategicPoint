using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Ronda 13 (punto 6): el panel de Configuraciones se rompia con la resolucion, con el tamano de interfaz y al cambiar de
    // idioma varias veces. Causas medidas en SC_Gameplay:
    //   * el panel principal mide 480x940 y el extra 440x700 con posiciones escritas a mano; PanelAjustesExtra.Preparar solo
    //     los escalaba UNA vez al abrir: cambiar el tamano de interfaz (que cambia la referencia de TODOS los CanvasScaler)
    //     o la resolucion con el panel abierto lo dejaba fuera de pantalla o cortado;
    //   * las etiquetas de los sliders y de los toggles miden 24 de alto con fuente 18 y "Truncate" vertical: con la fuente del
    //     juego la linea no entra y el texto DESAPARECE (era el "texto de los toggles no se ve");
    //   * etiqueta y valor de cada slider ocupaban la MISMA caja (400x24), el bloque de notas del panel extra se salia del
    //     panel y pisaba el ultimo boton.
    // Esto lo acomoda entero en tiempo de ejecucion (las posiciones ya estan guardadas en el .unity): dos columnas, filas con
    // alturas medidas del texto y todo el conjunto escalado para entrar en el area util del canvas. Se reaplica cuando cambia
    // el area (resolucion o escala), el idioma o el contenido de los textos, asi cualquier boton del panel lo deja bien.
    [DisallowMultipleComponent]
    public class LayoutDeAjustes : MonoBehaviour
    {
        public const float AnchoPrincipal = 480f, AnchoExtra = 440f, Separacion = 16f;
        public const float AnchoContenido = 440f, Margen = 20f;
        public const float FraccionDelArea = 0.96f;

        static readonly string[] OrdenDeSliders =
        {
            "Sensibilidad de mouse", "Sensibilidad de torreta", "Volumen", "General", "VFX", "Voces", "Tamaño de HUD", "Tamaño de mirilla",
        };
        static readonly string[] OrdenDeBotonesExtra =
        {
            "Pantalla", "Resolucion", "Calidad", "Daltonismo", "HudMinimo", "Escala", "Idioma", "Subtitulos",
        };

        public struct Resultado
        {
            public float AltoPrincipal, AltoExtra, Escala;
            public bool Apilado;
        }

        RectTransform panel;
        Vector2 areaAplicada = new Vector2(-1f, -1f);
        int firma = int.MinValue;
        public Resultado Ultimo { get; private set; }
        public int Aplicaciones { get; private set; }

        public static LayoutDeAjustes Asegurar(GameObject settingsPanel)
        {
            if (settingsPanel == null) return null;
            var l = settingsPanel.GetComponent<LayoutDeAjustes>();
            if (l == null) l = settingsPanel.AddComponent<LayoutDeAjustes>();
            l.panel = (RectTransform)settingsPanel.transform;
            return l;
        }

        void Awake() { panel = (RectTransform)transform; }
        void OnEnable() { SP.Core.Loc.Cambio += AlCambiarIdioma; firma = int.MinValue; }
        void OnDisable() { SP.Core.Loc.Cambio -= AlCambiarIdioma; }

        // El idioma cambia con F12 o con el boton: el texto del boton IDIOMA y el de los demas se reescriben antes del reacomodo.
        void AlCambiarIdioma()
        {
            if (panel == null) return;
            var extra = panel.Find("AjustesExtra");
            if (extra != null) PanelAjustesExtra.Refrescar(extra);
            firma = int.MinValue;
        }

        void Update()
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) return;
            int f = Firma();
            var area = AreaDelCanvas();
            if (f == firma && (area - areaAplicada).sqrMagnitude < 0.01f) return;
            Aplicar(area);
        }

        int Firma()
        {
            int h = SP.Core.Loc.Actual == SP.Core.Idioma.En ? 7 : 3;
            h = h * 31 + Mathf.RoundToInt(AjustesDeJuego.Escala * 100f);
            foreach (var t in panel.GetComponentsInChildren<Text>(true)) if (t != null && t.text != null) h = h * 31 + t.text.GetHashCode();
            return h;
        }

        public Vector2 AreaDelCanvas()
        {
            var canvas = panel != null ? panel.GetComponentInParent<Canvas>() : null;
            var raiz = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (raiz != null && raiz.rect.width > 1f && raiz.rect.height > 1f) return raiz.rect.size;
            return new Vector2(960f, 540f);
        }

        public void Aplicar() => Aplicar(AreaDelCanvas());

        public void Aplicar(Vector2 area)
        {
            if (panel == null) panel = (RectTransform)transform;
            areaAplicada = area;
            Aplicaciones++;

            var extra = panel.Find("AjustesExtra") as RectTransform;
            float h1 = AcomodarPrincipal();
            float h2 = extra != null ? AcomodarExtra(extra) : 0f;

            float kLado = Mathf.Min(1f, area.y * FraccionDelArea / Mathf.Max(h1, h2), area.x * FraccionDelArea / (AnchoPrincipal + (extra != null ? Separacion + AnchoExtra : 0f)));
            float kApilado = extra != null
                ? Mathf.Min(1f, area.y * FraccionDelArea / (h1 + Separacion + h2), area.x * FraccionDelArea / Mathf.Max(AnchoPrincipal, AnchoExtra))
                : kLado;
            // Apilado solo cuando mejora de verdad la escala (pantallas verticales o muy angostas).
            bool apilado = extra != null && kApilado > kLado * 1.15f;
            float k = apilado ? kApilado : kLado;

            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(AnchoPrincipal, h1);
            panel.localScale = new Vector3(k, k, 1f);
            if (extra != null)
            {
                extra.sizeDelta = new Vector2(AnchoExtra, h2);
                if (apilado)
                {
                    extra.anchorMin = extra.anchorMax = new Vector2(0.5f, 0f);
                    extra.pivot = new Vector2(0.5f, 1f);
                    extra.anchoredPosition = new Vector2(0f, -Separacion);
                    panel.anchoredPosition = new Vector2(0f, (Separacion + h2) * 0.5f * k);
                }
                else
                {
                    extra.anchorMin = extra.anchorMax = new Vector2(1f, 0.5f);
                    extra.pivot = new Vector2(0f, 0.5f);
                    extra.anchoredPosition = new Vector2(Separacion, 0f);
                    panel.anchoredPosition = new Vector2(-(Separacion + AnchoExtra) * 0.5f * k, 0f);
                }
            }
            else panel.anchoredPosition = Vector2.zero;

            Ultimo = new Resultado { AltoPrincipal = h1, AltoExtra = h2, Escala = k, Apilado = apilado };
            firma = Firma();
        }

        // ---------------- panel principal ----------------
        float AcomodarPrincipal()
        {
            ArreglarTextos(panel, extra: false);

            var titulo = panel.Find("Title") as RectTransform;
            if (titulo != null)
            {
                titulo.anchorMin = titulo.anchorMax = titulo.pivot = new Vector2(0.5f, 1f);
                titulo.anchoredPosition = new Vector2(0f, -14f);
                titulo.sizeDelta = new Vector2(AnchoContenido, 44f);
            }

            // Primero se mide el alto total para poder poner las filas de arriba hacia abajo respecto del centro.
            var sliders = new List<string>();
            foreach (var n in OrdenDeSliders) if (panel.Find(n + "_Slider") != null) sliders.Add(n);
            foreach (Transform h in panel.transform)
                if (h.name.EndsWith("_Slider") && !sliders.Contains(h.name.Substring(0, h.name.Length - 7))) sliders.Add(h.name.Substring(0, h.name.Length - 7));
            var toggles = new List<string>();
            foreach (Transform h in panel.transform) if (h.name.EndsWith("_Toggle")) toggles.Add(h.name.Substring(0, h.name.Length - 7));

            float alturaLinea = 26f;
            foreach (var n in sliders) alturaLinea = Mathf.Max(alturaLinea, LineaDe(panel.Find(n + "_Label")));
            float pasoSlider = alturaLinea + 2f + 20f + 14f;
            float pasoToggle = 38f;
            float alto = 64f + sliders.Count * pasoSlider + (toggles.Count > 0 ? 10f + toggles.Count * pasoToggle : 0f) + 14f + 56f + 22f;

            float y = 64f;   // desde el borde de arriba, hacia abajo
            foreach (var n in sliders)
            {
                var etiqueta = panel.Find(n + "_Label") as RectTransform;
                var valor = panel.Find(n + "_Value") as RectTransform;
                var slider = panel.Find(n + "_Slider") as RectTransform;
                Colocar(etiqueta, -AnchoContenido * 0.5f + 150f, y, 300f, alturaLinea, alto, TextAnchor.MiddleLeft);
                Colocar(valor, AnchoContenido * 0.5f - 50f, y, 100f, alturaLinea, alto, TextAnchor.MiddleRight);
                Colocar(slider, 0f, y + alturaLinea + 2f, AnchoContenido, 20f, alto, null);
                y += pasoSlider;
            }
            y += 10f;
            foreach (var n in toggles)
            {
                var caja = panel.Find(n + "_Toggle") as RectTransform;
                var etiqueta = panel.Find(n + "_Label") as RectTransform;
                float filaAlto = 30f;
                Colocar(caja, -AnchoContenido * 0.5f + 12f, y + (filaAlto - 24f) * 0.5f, 24f, 24f, alto, null);
                Colocar(etiqueta, -AnchoContenido * 0.5f + 12f + 12f + 10f + 190f, y, 380f, filaAlto, alto, TextAnchor.MiddleLeft);
                y += pasoToggle;
            }
            y += 14f;
            var volver = panel.Find("BackButton") as RectTransform;
            if (volver != null) Colocar(volver, 0f, y, 260f, 56f, alto, null);
            return alto;
        }

        // Pone un rect por su borde superior (yTop, medido hacia abajo desde el borde de arriba del panel) y su centro x.
        static void Colocar(RectTransform rt, float xCentro, float yTop, float ancho, float alto, float altoPanel, TextAnchor? alineacion)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ancho, alto);
            rt.anchoredPosition = new Vector2(xCentro, altoPanel * 0.5f - yTop - alto * 0.5f);
            var t = rt.GetComponent<Text>();
            if (t != null && alineacion.HasValue) t.alignment = alineacion.Value;
        }

        static float LineaDe(Transform etiqueta)
        {
            var t = etiqueta != null ? etiqueta.GetComponent<Text>() : null;
            return t == null ? 26f : Mathf.Max(26f, Mathf.Ceil(t.fontSize * 1.6f));
        }

        // ---------------- panel extra ----------------
        float AcomodarExtra(RectTransform extra)
        {
            ArreglarTextos(extra, extra: true);
            const float pad = 16f;

            Text titulo = null, nota = null;
            foreach (Transform h in extra)
            {
                var t = h.GetComponent<Text>();
                if (t == null || h.GetComponent<Button>() != null) continue;
                if (titulo == null) titulo = t; else if (nota == null) nota = t;
            }
            var botones = new List<RectTransform>();
            foreach (var n in OrdenDeBotonesExtra) { var b = extra.Find(n) as RectTransform; if (b != null) botones.Add(b); }

            float altoNota = 0f;
            if (nota != null)
            {
                nota.rectTransform.sizeDelta = new Vector2(AnchoExtra - pad * 2f, 20f);
                altoNota = Mathf.Ceil(nota.preferredHeight) + 10f;
            }
            float alto = pad + 34f + 12f + botones.Count * 52f + 8f + altoNota + pad;

            float y = pad;
            if (titulo != null) { Colocar(titulo.rectTransform, 0f, y, AnchoExtra - pad * 2f, 34f, alto, TextAnchor.MiddleCenter); }
            y += 34f + 12f;
            foreach (var b in botones)
            {
                Colocar(b, 0f, y, AnchoExtra - pad * 2f, 44f, alto, null);
                var etiqueta = b.GetComponentInChildren<Text>();
                if (etiqueta != null)
                {
                    etiqueta.resizeTextForBestFit = true;
                    etiqueta.resizeTextMinSize = 11;
                    etiqueta.resizeTextMaxSize = 20;
                    etiqueta.verticalOverflow = VerticalWrapMode.Overflow;
                    var er = etiqueta.rectTransform;
                    er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one; er.offsetMin = new Vector2(8f, 2f); er.offsetMax = new Vector2(-8f, -2f);
                }
                y += 52f;
            }
            y += 8f;
            if (nota != null) Colocar(nota.rectTransform, 0f, y, AnchoExtra - pad * 2f, altoNota, alto, TextAnchor.UpperCenter);
            return alto;
        }

        // Ningun texto se trunca (era la causa de las etiquetas invisibles) y los largos se parten en vez de desbordar.
        static void ArreglarTextos(Transform raiz, bool extra)
        {
            foreach (var t in raiz.GetComponentsInChildren<Text>(true))
            {
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.raycastTarget = false;
            }
        }

        // ---------------- diagnostico (suite y pruebas) ----------------
        // Devuelve una descripcion por cada defecto; lista vacia = el panel esta bien para esa area.
        public static List<string> Diagnosticar(GameObject settingsPanel, Vector2 area)
        {
            var problemas = new List<string>();
            if (settingsPanel == null) { problemas.Add("sin panel"); return problemas; }
            var l = settingsPanel.GetComponent<LayoutDeAjustes>();
            if (l == null) { problemas.Add("sin LayoutDeAjustes"); return problemas; }
            var r = l.Ultimo;

            float anchoGrupo = r.Apilado ? Mathf.Max(AnchoPrincipal, AnchoExtra) : AnchoPrincipal + Separacion + AnchoExtra;
            float altoGrupo = r.Apilado ? r.AltoPrincipal + Separacion + r.AltoExtra : Mathf.Max(r.AltoPrincipal, r.AltoExtra);
            if (anchoGrupo * r.Escala > area.x + 0.5f) problemas.Add($"el conjunto ({anchoGrupo * r.Escala:0}) es mas ancho que el area ({area.x:0})");
            if (altoGrupo * r.Escala > area.y + 0.5f) problemas.Add($"el conjunto ({altoGrupo * r.Escala:0}) es mas alto que el area ({area.y:0})");

            RevisarPanel((RectTransform)settingsPanel.transform, "principal", problemas);
            var extra = settingsPanel.transform.Find("AjustesExtra") as RectTransform;
            if (extra != null) RevisarPanel(extra, "extra", problemas);
            return problemas;
        }

        static bool TieneContenido(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            var t = rt.GetComponent<Text>();
            if (t != null) return !string.IsNullOrWhiteSpace(t.text);
            return rt.GetComponent<Slider>() != null || rt.GetComponent<Toggle>() != null || rt.GetComponent<Button>() != null;
        }

        static Rect CajaEn(RectTransform panel, RectTransform hijo)
        {
            var c = new Vector3[4];
            hijo.GetWorldCorners(c);
            var min = (Vector2)panel.InverseTransformPoint(c[0]);
            var max = (Vector2)panel.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        static void RevisarPanel(RectTransform panel, string nombre, List<string> problemas)
        {
            var cajas = new List<(RectTransform rt, Rect caja)>();
            foreach (Transform h in panel)
            {
                var rt = h as RectTransform;
                if (!TieneContenido(rt)) continue;
                cajas.Add((rt, CajaEn(panel, rt)));
            }
            var limite = panel.rect;
            for (int i = 0; i < cajas.Count; i++)
            {
                var (rt, c) = cajas[i];
                if (c.xMin < limite.xMin - 0.5f || c.xMax > limite.xMax + 0.5f || c.yMin < limite.yMin - 0.5f || c.yMax > limite.yMax + 0.5f)
                    problemas.Add($"[{nombre}] '{rt.name}' se sale del panel");
                var t = rt.GetComponent<Text>();
                if (t != null)
                {
                    float need = t.preferredHeight;
                    if (need > rt.rect.height + 2f) problemas.Add($"[{nombre}] '{rt.name}' necesita {need:0} de alto y tiene {rt.rect.height:0} ('{t.text.Replace('\n', '|')}')");
                }
                for (int j = i + 1; j < cajas.Count; j++)
                    if (c.Overlaps(cajas[j].caja)) problemas.Add($"[{nombre}] '{rt.name}' se pisa con '{cajas[j].rt.name}'");
            }
            // Botones del panel extra: su etiqueta tiene que caber dentro del boton.
            foreach (Transform h in panel)
            {
                var b = h.GetComponent<Button>();
                if (b == null || !h.gameObject.activeInHierarchy) continue;
                var et = h.GetComponentInChildren<Text>();
                if (et == null) continue;
                float need = et.preferredHeight;
                if (need > ((RectTransform)h).rect.height + 2f) problemas.Add($"[{nombre}] la etiqueta de '{h.name}' no cabe en el boton ('{et.text}')");
            }
        }
    }
}
