using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Opciones que no tenian donde vivir: pantalla completa, resolucion, calidad, paleta para daltonismo y
    // HUD minimo. Se guardan en PlayerPrefs y se aplican solas al arrancar cada escena.
    public static class AjustesDeJuego
    {
        const string PrefCompleta = "sp_pantalla_completa", PrefRes = "sp_resolucion", PrefCalidad = "sp_calidad",
                     PrefDalto = "sp_daltonismo", PrefHudMin = "sp_hud_minimo", PrefEscala = "sp_escala_interfaz";

        public static bool Daltonismo { get; private set; }
        public static bool HudMinimo { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Daltonismo = false; HudMinimo = false; Escala = 1f; }

        // Tamano de la interfaz (accesibilidad, item 28): 100 %, 125 % o 150 %. Se aplica achicando la resolucion de
        // referencia de los CanvasScaler del HUD, la pausa y el menu (todos de 960x540).
        public const float AlturaDeReferencia = 540f;
        public static readonly float[] Escalas = { 1f, 1.25f, 1.5f };
        public static float Escala { get; private set; } = 1f;
        public static string TextoEscala() => Mathf.RoundToInt(Escala * 100f) + " %";
        public static void SiguienteEscala()
        {
            int i = System.Array.FindIndex(Escalas, e => Mathf.Approximately(e, Escala));
            PonerEscala(Escalas[(i + 1) % Escalas.Length]);
        }
        public static void PonerEscala(float e)
        {
            Escala = Mathf.Clamp(e, 1f, 1.5f);
            PlayerPrefs.SetInt(PrefEscala, Mathf.RoundToInt(Escala * 100f)); PlayerPrefs.Save();
            AplicarEscala();
        }
        public static void AplicarEscala()
        {
            foreach (var cs in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                float h = cs.referenceResolution.y;
                bool nuestro = false;
                foreach (var e in Escalas) if (Mathf.Abs(h - AlturaDeReferencia / e) < 0.6f) nuestro = true;
                if (!nuestro) continue;   // solo los canvas de 960x540 (y sus versiones escaladas)
                float ancho = 960f / Escala, alto = AlturaDeReferencia / Escala;
                cs.referenceResolution = new Vector2(ancho, alto);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            Daltonismo = false;
            HudMinimo = false;
            Escala = Mathf.Clamp(PlayerPrefs.GetInt(PrefEscala, 100) / 100f, 1f, 1.5f);
            if (!Mathf.Approximately(Escala, 1f)) AplicarEscala();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
            if (Application.isEditor) return;   // en el Editor no se toca la ventana del juego ni la calidad
            if (PlayerPrefs.HasKey(PrefRes) || PlayerPrefs.HasKey(PrefCompleta)) AplicarPantalla();
        }

        static void AlCargarEscena(UnityEngine.SceneManagement.Scene e, UnityEngine.SceneManagement.LoadSceneMode m) { if (!Mathf.Approximately(Escala, 1f)) AplicarEscala(); }

        // ---- Pantalla
        public static bool PantallaCompleta => PlayerPrefs.HasKey(PrefCompleta) ? PlayerPrefs.GetInt(PrefCompleta) == 1 : Screen.fullScreen;

        public static List<Resolution> Resoluciones()
        {
            var lista = new List<Resolution>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width < 800) continue;
                bool repetida = false;
                foreach (var e in lista) if (e.width == r.width && e.height == r.height) { repetida = true; break; }
                if (!repetida) lista.Add(r);
            }
            if (lista.Count == 0) lista.Add(new Resolution { width = Screen.width, height = Screen.height });
            return lista;
        }

        public static int IndiceResolucion()
        {
            var l = Resoluciones();
            int guardado = PlayerPrefs.GetInt(PrefRes, -1);
            if (guardado >= 0 && guardado < l.Count) return guardado;
            for (int i = 0; i < l.Count; i++) if (l[i].width == Screen.width && l[i].height == Screen.height) return i;
            return l.Count - 1;
        }
        public static string TextoResolucion() { var l = Resoluciones(); var r = l[Mathf.Clamp(IndiceResolucion(), 0, l.Count - 1)]; return $"{r.width}x{r.height}"; }

        public static void AlternarPantallaCompleta() { PlayerPrefs.SetInt(PrefCompleta, PantallaCompleta ? 0 : 1); PlayerPrefs.Save(); AplicarPantalla(); }
        public static void SiguienteResolucion() { var l = Resoluciones(); PlayerPrefs.SetInt(PrefRes, (IndiceResolucion() + 1) % l.Count); PlayerPrefs.Save(); AplicarPantalla(); }

        public static void AplicarPantalla()
        {
            var l = Resoluciones();
            var r = l[Mathf.Clamp(IndiceResolucion(), 0, l.Count - 1)];
            if (Application.isEditor) return;
            Screen.SetResolution(r.width, r.height, PantallaCompleta ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        // ---- Accesibilidad
        public static void PonerDaltonismo(bool v) { Daltonismo = v; }
        public static void PonerHudMinimo(bool v) { HudMinimo = v; }

        // Verde -> azul y rojo -> naranja: la pareja rojo/verde es la que confunden las formas mas comunes de daltonismo.
        public static Color Adaptar(Color c)
        {
            if (!Daltonismo) return c;
            float a = c.a;
            if (c.g > c.r * 1.15f && c.g > c.b * 1.15f) return new Color(0.25f, 0.62f, 0.98f, a);
            if (c.r > c.g * 1.7f && c.r > c.b * 1.7f) return new Color(0.98f, 0.62f, 0.10f, a);
            return c;
        }
    }

    // Panel a la derecha de Configuraciones con lo anterior. Se arma solo la primera vez que se abre.
    public static class PanelAjustesExtra
    {
        const string Nombre = "AjustesExtra";
        static readonly string[] Ocultos = { "MisionHud", "MinimapBorder", "GroupCards", "SelectionCount", "MissionStatus", "PerfHud" };

        // Escala el panel para que entre en pantalla (a 720p el original de 940 de alto dejaba fuera VOLVER) y agrega el bloque extra.
        public static void Preparar(GameObject settingsPanel)
        {
            if (settingsPanel == null) return;
            var rt = (RectTransform)settingsPanel.transform;
            var extra = SP.Core.BuscarHijo.Ruta(settingsPanel.transform, Nombre);
            // Un bloque armado con el formato viejo (sin "Titulo") se descarta y se rearma con el nuevo diseno.
            if (extra != null && SP.Core.BuscarHijo.Ruta(extra, "Titulo") == null)
            {
                if (Application.isPlaying) Object.Destroy(extra.gameObject); else Object.DestroyImmediate(extra.gameObject);
                extra = null;
            }
            if (extra == null) extra = Construir(rt);
            Refrescar(extra);
            // Ronda 13 (punto 6): el reacomodo ya no es un calculo de una sola vez al abrir: LayoutDeAjustes lo repite cada vez
            // que cambia el area del canvas (resolucion / tamano de interfaz), el idioma o algun texto del panel.
            var layout = LayoutDeAjustes.Asegurar(settingsPanel);
            layout.Aplicar();
        }

        // Paleta del bloque (la misma familia oscura + dorado del resto del HUD).
        static readonly Color FondoPanel = new Color(0.05f, 0.06f, 0.09f, 0.97f);
        static readonly Color FondoBoton = new Color(0.16f, 0.23f, 0.33f, 1f);
        static readonly Color FondoActivo = new Color(0.2f, 0.45f, 0.3f, 1f);
        static readonly Color Dorado = new Color(1f, 0.82f, 0.3f);
        static readonly Color Gris = new Color(0.72f, 0.78f, 0.86f);

        static Transform Construir(RectTransform padre)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject(Nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(15f, 0f);
            rt.sizeDelta = new Vector2(440f, 700f);
            go.GetComponent<Image>().color = FondoPanel;
            go.GetComponent<Image>().raycastTarget = false;

            Texto(go.transform, font, "Titulo", "PANTALLA Y ACCESIBILIDAD", 22, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            Texto(go.transform, font, "SecPantalla", "PANTALLA", 14, TextAnchor.MiddleLeft, FontStyle.Bold, Dorado);
            Separador(go.transform, "SepPantalla");
            Boton(go.transform, font, "Pantalla", "MODO", () => { AjustesDeJuego.AlternarPantallaCompleta(); Refrescar(go.transform); });
            Texto(go.transform, font, "ResolucionLabel", "RESOLUCION", 14, TextAnchor.MiddleLeft, FontStyle.Bold, Gris);
            CrearDropdown(go.transform, font, "Resolucion");
            Texto(go.transform, font, "SecAccesibilidad", "ACCESIBILIDAD", 14, TextAnchor.MiddleLeft, FontStyle.Bold, Dorado);
            Separador(go.transform, "SepAccesibilidad");
            Boton(go.transform, font, "Escala", "TAMANO DE INTERFAZ", () => { AjustesDeJuego.SiguienteEscala(); Refrescar(go.transform); });
            Boton(go.transform, font, "Idioma", "IDIOMA / LANGUAGE", () => { SP.Core.Loc.Alternar(); Refrescar(go.transform); });
            Boton(go.transform, font, "Daltonismo", "DALTONISMO", () => { AjustesDeJuego.PonerDaltonismo(!AjustesDeJuego.Daltonismo); Refrescar(go.transform); });
            Boton(go.transform, font, "HudMinimo", "HUD MINIMO  [F10]", () => { AjustesDeJuego.PonerHudMinimo(!AjustesDeJuego.HudMinimo); Refrescar(go.transform); });
            Boton(go.transform, font, "Subtitulos", "SUBTITULOS DE SONIDO", () => { SP.Presentation.Subtitulos.Poner(!SP.Presentation.Subtitulos.Activos); Refrescar(go.transform); });
            Texto(go.transform, font, "Nota", "Mando: stick izq. mover, stick der. mirar, RT disparar, A saltar, B agacharse, X recargar, RB/LB cambiar arma, Start pausa.", 13, TextAnchor.UpperCenter, FontStyle.Normal, Gris);
            return go.transform;
        }

        static Image ImagenDe(Transform raiz, string ruta)
        {
            var t = SP.Core.BuscarHijo.Ruta(raiz, ruta);
            return t != null ? t.GetComponent<Image>() : null;
        }

        static void Separador(Transform padre, string nombre)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var im = go.GetComponent<Image>();
            im.color = new Color(Dorado.r, Dorado.g, Dorado.b, 0.45f);
            im.raycastTarget = false;
        }

        static void CrearDropdown(Transform padre, Font font, string nombre)
        {
            var go = UnityEngine.UI.DefaultControls.CreateDropdown(new UnityEngine.UI.DefaultControls.Resources());
            go.name = nombre;
            go.transform.SetParent(padre, false);
            var d = go.GetComponent<Dropdown>();
            d.ClearOptions();
            var opts = new List<string>();
            foreach (var r in AjustesDeJuego.Resoluciones()) opts.Add($"{r.width}x{r.height}");
            d.AddOptions(opts);
            d.value = AjustesDeJuego.IndiceResolucion();
            d.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("sp_resolucion", v); PlayerPrefs.Save();
                AjustesDeJuego.AplicarPantalla();
            });

            // Estilo oscuro: el desplegable por defecto es blanco con letra negra y desentona con todo lo demas.
            var fondo = go.GetComponent<Image>(); if (fondo != null) fondo.color = FondoBoton;
            foreach (var t in go.GetComponentsInChildren<Text>(true)) { t.font = font; t.fontSize = 18; t.color = Color.white; t.fontStyle = FontStyle.Bold; }
            var flecha = ImagenDe(go.transform, "Arrow"); if (flecha != null) flecha.color = Dorado;
            var plantilla = ImagenDe(go.transform, "Template"); if (plantilla != null) plantilla.color = new Color(0.08f, 0.1f, 0.15f, 1f);
            var itemBg = ImagenDe(go.transform, "Template/Viewport/Content/Item/Item Background"); if (itemBg != null) itemBg.color = FondoBoton;
            var marca = ImagenDe(go.transform, "Template/Viewport/Content/Item/Item Checkmark"); if (marca != null) marca.color = Dorado;

            // Boton APLICAR a la derecha de la misma fila (aplica la resolucion elegida sin depender del cambio de valor).
            var btnGo = new GameObject("Aplicar", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(padre, false);
            btnGo.GetComponent<Image>().color = FondoActivo;
            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = btnGo.GetComponent<Image>();
            btn.onClick.AddListener(() => AjustesDeJuego.AplicarPantalla());
            ButtonSfx.Attach(btn);
            var btnTxt = new GameObject("Label", typeof(RectTransform), typeof(Text));
            btnTxt.transform.SetParent(btnGo.transform, false);
            var btrt = (RectTransform)btnTxt.transform;
            btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = btrt.offsetMax = Vector2.zero;
            var btx = btnTxt.GetComponent<Text>();
            btx.font = font; btx.fontSize = 18; btx.fontStyle = FontStyle.Bold; btx.alignment = TextAnchor.MiddleCenter; btx.color = Color.white; btx.text = "APLICAR"; btx.raycastTarget = false;
        }

        public static void Refrescar(Transform extra)
        {
            if (extra == null) return;
            Poner(extra, "Pantalla", SP.Core.Loc.T(AjustesDeJuego.PantallaCompleta ? "COMPLETA" : "VENTANA"));
            Poner(extra, "Escala", AjustesDeJuego.TextoEscala());
            Poner(extra, "Idioma", SP.Core.Loc.T(SP.Core.Loc.Actual == SP.Core.Idioma.Es ? "ESPANOL" : "ENGLISH"));
            Poner(extra, "Daltonismo", AjustesDeJuego.Daltonismo ? "SI" : "NO");
            Poner(extra, "HudMinimo", AjustesDeJuego.HudMinimo ? "SI" : "NO");
            Poner(extra, "Subtitulos", SP.Presentation.Subtitulos.Activos ? "SI" : "NO");
        }
        static void Poner(Transform extra, string boton, string texto)
        {
            var t = SP.Core.BuscarHijo.Ruta(extra, boton);
            var vt = SP.Core.BuscarHijo.Ruta(t, "Value");
            var tx = vt != null ? vt.GetComponent<Text>() : null;
            if (tx != null) tx.text = texto;
            // Un boton "SI/NO" se pinta verde cuando esta activo: se ve el estado sin leer.
            var im = t != null ? t.GetComponent<Image>() : null;
            if (im != null && (boton == "Daltonismo" || boton == "HudMinimo" || boton == "Subtitulos"))
                im.color = texto == "SI" ? FondoActivo : FondoBoton;
        }

        static void Texto(Transform padre, Font font, string nombre, string s, int size, TextAnchor anchor, FontStyle style, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.fontStyle = style; t.text = s; t.color = color; t.raycastTarget = false;
        }

        static void Boton(Transform padre, Font font, string nombre, string titulo, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            go.GetComponent<Image>().color = FondoBoton;
            var b = go.GetComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(accion);
            ButtonSfx.Attach(b);

            var t = new GameObject("Label", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(go.transform, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(0.62f, 1f); trt.offsetMin = new Vector2(16f, 2f); trt.offsetMax = new Vector2(-4f, -2f);
            var tx = t.GetComponent<Text>();
            tx.font = font; tx.fontSize = 18; tx.fontStyle = FontStyle.Bold; tx.alignment = TextAnchor.MiddleLeft; tx.color = Color.white; tx.raycastTarget = false; tx.text = titulo;
            tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 11; tx.resizeTextMaxSize = 18;

            var v = new GameObject("Value", typeof(RectTransform), typeof(Text));
            v.transform.SetParent(go.transform, false);
            var vrt = (RectTransform)v.transform;
            vrt.anchorMin = new Vector2(0.62f, 0f); vrt.anchorMax = new Vector2(1f, 1f); vrt.offsetMin = new Vector2(4f, 2f); vrt.offsetMax = new Vector2(-16f, -2f);
            var vx = v.GetComponent<Text>();
            vx.font = font; vx.fontSize = 18; vx.fontStyle = FontStyle.Bold; vx.alignment = TextAnchor.MiddleRight; vx.color = Dorado; vx.raycastTarget = false; vx.text = "";
            vx.resizeTextForBestFit = true; vx.resizeTextMinSize = 11; vx.resizeTextMaxSize = 18;
        }

        // HUD minimo: oculta lo secundario con un CanvasGroup (no se destruye ni se desactiva nada que otro script maneje).
        public static void AplicarHudMinimo(Transform canvas)
        {
            if (canvas == null) return;
            foreach (var n in Ocultos)
            {
                var t = canvas.Find(n);
                if (t == null) continue;
                // Escala 0 (y no solo alfa): varios de estos paneles tienen su propio Canvas o reescriben su alfa cada frame.
                t.localScale = AjustesDeJuego.HudMinimo ? Vector3.zero : Vector3.one;
            }
        }
    }
}
