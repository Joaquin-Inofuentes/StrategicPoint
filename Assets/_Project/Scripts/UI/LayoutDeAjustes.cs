using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Acomoda toda la pantalla de Configuraciones en una sola tarjeta de dos columnas, en tiempo de ejecucion (las posiciones
    // guardadas en la escena no se usan):
    //   izquierda: SONIDO (Volumen, General, VFX, Voces) e INTERFAZ (tamano de HUD y de mirilla, idioma);
    //   derecha:   MIRA Y CONTROL (sensibilidades, invertir eje Y, efectos de camara) y PANTALLA (modo, resolucion + aplicar).
    // Titulo arriba, nota del mando y VOLVER abajo. Las filas tienen alturas fijas y toda la tarjeta se escala para entrar en el
    // area util del canvas; se reaplica cuando cambia el area (resolucion), el idioma o cualquier texto, asi ninguna fila se
    // pisa ni se sale. Antecedentes (ronda 13): etiquetas con Truncate que desaparecian, etiqueta y valor en la misma caja y
    // paneles fuera de pantalla al cambiar la resolucion.
    [DisallowMultipleComponent]
    public class LayoutDeAjustes : MonoBehaviour
    {
        public const float Relleno = 28f, Columna = 410f, Hueco = 36f;
        public const float AnchoTarjeta = Relleno * 2f + Columna * 2f + Hueco;
        public const float FraccionDelArea = 0.96f;
        // Alturas de fila.
        const float AltoSeccion = 32f, AltoSlider = 54f, AltoInterruptor = 42f, AltoBoton = 44f, HuecoSecciones = 14f, AnchoChip = 68f;

        static readonly string[] Sonido = { "Volumen", "General", "VFX", "Voces" };
        static readonly string[] Interfaz = { "Tamaño de HUD", "Tamaño de mirilla" };
        static readonly string[] Control = { "Sensibilidad de mouse", "Sensibilidad de torreta" };
        static readonly string[] Interruptores = { "InvertirEjeY", "EfectosDeCamara" };

        public struct Resultado
        {
            public float Alto, Escala;
        }

        RectTransform panel;
        Vector2 areaAplicada = new Vector2(-1f, -1f);
        int firma = int.MinValue;
        public Resultado Ultimo { get; private set; }
        public int Aplicaciones { get; private set; }

        struct Fila
        {
            public RectTransform rt;
            public float x, y, w, h;
            public TextAnchor? alineacion;
        }
        readonly List<Fila> filas = new List<Fila>();

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
            PanelAjustesExtra.Refrescar(panel);
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
            // Los numeros de los sliders (*_Value) cambian en cada tic y no mueven ninguna fila: fuera de la firma, si no el panel se
            // reacomodaba (y se corria) mientras se arrastraba un slider.
            foreach (var t in panel.GetComponentsInChildren<Text>(true))
                if (t != null && t.text != null && !t.name.EndsWith("_Value")) h = h * 31 + t.text.GetHashCode();
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

            float alto = Acomodar();
            float k = Mathf.Min(1f, area.y * FraccionDelArea / alto, area.x * FraccionDelArea / AnchoTarjeta);

            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(AnchoTarjeta, alto);
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = new Vector3(k, k, 1f);

            Ultimo = new Resultado { Alto = alto, Escala = k };
            firma = Firma();
        }

        // ---------------- la tarjeta ----------------
        // Devuelve el alto total. Todo se mide "desde el borde de arriba, hacia abajo" y se traduce al centro del panel.
        float Acomodar()
        {
            ArreglarTextos(panel);
            filas.Clear();
            float xIzq = -(Columna + Hueco) * 0.5f, xDer = (Columna + Hueco) * 0.5f;
            float anchoInterior = AnchoTarjeta - Relleno * 2f;

            var notaTr = SP.Core.BuscarHijo.Ruta(panel, "Nota");
            var nota = notaTr != null ? notaTr.GetComponent<Text>() : null;
            float altoNota = 0f;
            if (nota != null)
            {
                nota.rectTransform.sizeDelta = new Vector2(anchoInterior, 20f);
                altoNota = Mathf.Ceil(nota.preferredHeight) + 4f;
            }

            // 1) medir las dos columnas para conocer el alto total
            const float yInicio = 76f;
            float altoIzq = AltoSeccion + Sonido.Length * AltoSlider + HuecoSecciones + AltoSeccion + Interfaz.Length * AltoSlider + 8f + AltoBoton;
            float altoDer = AltoSeccion + Control.Length * AltoSlider + Interruptores.Length * AltoInterruptor + HuecoSecciones
                            + AltoSeccion + AltoBoton + 8f + 22f + AltoBoton;
            float alto = yInicio + Mathf.Max(altoIzq, altoDer) + 14f + altoNota + 10f + 50f + 22f;

            // 2) titulo
            Poner("Title", 0f, 16f, anchoInterior, 44f, TextAnchor.MiddleCenter);
            Poner("TituloSep", 0f, 64f, anchoInterior, 2f);

            // 3) columna izquierda
            float y = yInicio;
            y = Seccion("Sonido", xIzq, y, Sonido, null);
            y += HuecoSecciones;
            y = Seccion("Interfaz", xIzq, y, Interfaz, null);
            y += 8f;
            Poner("Idioma", xIzq, y, Columna, AltoBoton);

            // 4) columna derecha
            y = yInicio;
            y = Seccion("Control", xDer, y, Control, Interruptores);
            y += HuecoSecciones;
            Poner("Sec_Pantalla", xDer, y, Columna, 24f, TextAnchor.MiddleLeft);
            Poner("SecSep_Pantalla", xDer, y + 26f, Columna, 2f);
            y += AltoSeccion;
            Poner("Pantalla", xDer, y, Columna, AltoBoton);
            y += AltoBoton + 8f;
            Poner("ResolucionLabel", xDer, y, Columna, 20f, TextAnchor.MiddleLeft);
            y += 22f;
            const float anchoAplicar = 112f, huecoFila = 8f;
            float anchoLista = Columna - anchoAplicar - huecoFila;
            Poner("Resolucion", xDer - Columna * 0.5f + anchoLista * 0.5f, y, anchoLista, AltoBoton);
            Poner("Aplicar", xDer + Columna * 0.5f - anchoAplicar * 0.5f, y, anchoAplicar, AltoBoton);

            // 5) pie: nota y VOLVER
            float yPie = alto - 22f - 50f - 10f - altoNota;
            if (notaTr != null) Poner("Nota", 0f, yPie, anchoInterior, altoNota, TextAnchor.MiddleCenter);
            Poner("BackButton", 0f, alto - 22f - 50f, 260f, 50f);

            foreach (var f in filas)
            {
                Colocar(f.rt, f.x, f.y, f.w, f.h, alto, f.alineacion);
                if (f.rt.GetComponent<Button>() != null)
                    foreach (var et in f.rt.GetComponentsInChildren<Text>(true))
                    {
                        et.resizeTextForBestFit = true; et.resizeTextMinSize = 11; et.resizeTextMaxSize = 18;
                        et.verticalOverflow = VerticalWrapMode.Overflow;
                    }
            }
            return alto;
        }

        void Poner(string nombre, float x, float y, float w, float h, TextAnchor? alineacion = null)
        {
            var rt = SP.Core.BuscarHijo.Ruta(panel, nombre) as RectTransform;
            if (rt != null) filas.Add(new Fila { rt = rt, x = x, y = y, w = w, h = h, alineacion = alineacion });
        }

        // Titulo de seccion con su linea, los sliders (etiqueta, chip con el valor, barra) y los interruptores. Devuelve la y siguiente.
        float Seccion(string id, float x, float y, string[] sliders, string[] interruptores)
        {
            Poner("Sec_" + id, x, y, Columna, 24f, TextAnchor.MiddleLeft);
            Poner("SecSep_" + id, x, y + 26f, Columna, 2f);
            y += AltoSeccion;
            foreach (var n in sliders)
            {
                Poner(n + "_Label", x - AnchoChip * 0.5f - 4f, y, Columna - AnchoChip - 8f, 24f, TextAnchor.MiddleLeft);
                Poner(n + "_Chip", x + Columna * 0.5f - AnchoChip * 0.5f, y, AnchoChip, 24f);
                Poner(n + "_Value", x + Columna * 0.5f - AnchoChip * 0.5f, y, AnchoChip, 24f, TextAnchor.MiddleCenter);
                Poner(n + "_Slider", x, y + 26f, Columna, 24f);
                y += AltoSlider;
            }
            if (interruptores != null)
                foreach (var n in interruptores)
                {
                    Poner(n + "_Toggle", x, y + 4f, Columna, 34f);
                    y += AltoInterruptor;
                }
            return y;
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

        // Ningun texto se trunca (era la causa de las etiquetas invisibles) y los largos se parten en vez de desbordar.
        static void ArreglarTextos(Transform raiz)
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

            if (AnchoTarjeta * r.Escala > area.x + 0.5f) problemas.Add($"la tarjeta ({AnchoTarjeta * r.Escala:0}) es mas ancha que el area ({area.x:0})");
            if (r.Alto * r.Escala > area.y + 0.5f) problemas.Add($"la tarjeta ({r.Alto * r.Escala:0}) es mas alta que el area ({area.y:0})");

            RevisarPanel((RectTransform)settingsPanel.transform, problemas);
            return problemas;
        }

        static bool TieneContenido(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            var t = rt.GetComponent<Text>();
            if (t != null) return !string.IsNullOrWhiteSpace(t.text);
            return rt.GetComponent<Slider>() != null || rt.GetComponent<Toggle>() != null || rt.GetComponent<Button>() != null || rt.GetComponent<Dropdown>() != null;
        }

        static Rect CajaEn(RectTransform panel, RectTransform hijo)
        {
            var c = new Vector3[4];
            hijo.GetWorldCorners(c);
            var min = (Vector2)panel.InverseTransformPoint(c[0]);
            var max = (Vector2)panel.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        static void RevisarPanel(RectTransform panel, List<string> problemas)
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
                    problemas.Add($"'{rt.name}' se sale del panel");
                var t = rt.GetComponent<Text>();
                if (t != null)
                {
                    float need = t.preferredHeight;
                    if (need > rt.rect.height + 2f) problemas.Add($"'{rt.name}' necesita {need:0} de alto y tiene {rt.rect.height:0} ('{t.text.Replace('\n', '|')}')");
                }
                for (int j = i + 1; j < cajas.Count; j++)
                    if (c.Overlaps(cajas[j].caja)) problemas.Add($"'{rt.name}' se pisa con '{cajas[j].rt.name}'");
            }
            // Botones y rotulos de interruptor: la etiqueta tiene que caber.
            foreach (Transform h in panel)
            {
                if (!h.gameObject.activeInHierarchy) continue;
                if (h.GetComponent<Button>() == null && h.GetComponent<Toggle>() == null) continue;
                foreach (var et in h.GetComponentsInChildren<Text>())
                {
                    float need = et.preferredHeight;
                    if (need > ((RectTransform)et.transform).rect.height + 2f && et.text.Length > 0)
                        problemas.Add($"la etiqueta de '{h.name}' no cabe ('{et.text}')");
                }
            }
        }
    }
}
