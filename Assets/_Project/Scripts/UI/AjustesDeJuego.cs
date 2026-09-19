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
            Daltonismo = PlayerPrefs.GetInt(PrefDalto, 0) == 1;
            HudMinimo = PlayerPrefs.GetInt(PrefHudMin, 0) == 1;
            Escala = Mathf.Clamp(PlayerPrefs.GetInt(PrefEscala, 100) / 100f, 1f, 1.5f);
            if (!Mathf.Approximately(Escala, 1f)) AplicarEscala();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
            if (Application.isEditor) return;   // en el Editor no se toca la ventana del juego ni la calidad
            if (PlayerPrefs.HasKey(PrefCalidad)) QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(PrefCalidad), 0, QualitySettings.names.Length - 1), true);
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

        // ---- Calidad
        public static string TextoCalidad() => QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "-";
        public static void SiguienteCalidad()
        {
            int n = QualitySettings.names.Length; if (n == 0) return;
            int nuevo = (QualitySettings.GetQualityLevel() + 1) % n;
            QualitySettings.SetQualityLevel(nuevo, true);
            PlayerPrefs.SetInt(PrefCalidad, nuevo); PlayerPrefs.Save();
        }

        // ---- Accesibilidad
        public static void PonerDaltonismo(bool v) { Daltonismo = v; PlayerPrefs.SetInt(PrefDalto, v ? 1 : 0); PlayerPrefs.Save(); }
        public static void PonerHudMinimo(bool v) { HudMinimo = v; PlayerPrefs.SetInt(PrefHudMin, v ? 1 : 0); PlayerPrefs.Save(); }

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
            var extra = settingsPanel.transform.Find(Nombre);
            if (extra == null) extra = Construir(rt);
            var canvas = settingsPanel.GetComponentInParent<Canvas>();
            var raiz = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            if (raiz != null)
            {
                float alto = 940f + 20f, ancho = 480f + 440f + 30f;
                float k = Mathf.Min(1f, raiz.rect.height / alto, raiz.rect.width / ancho);
                rt.localScale = new Vector3(k, k, 1f);
                // el panel principal queda a la izquierda; el bloque extra cuelga a su derecha
                rt.anchoredPosition = new Vector2(-(440f * 0.5f + 15f) * k, 0f);
            }
            Refrescar(extra);
        }

        static Transform Construir(RectTransform padre)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject(Nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(15f, 0f);
            rt.sizeDelta = new Vector2(440f, 620f);
            go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.97f);

            Texto(go.transform, font, "PANTALLA Y ACCESIBILIDAD", new Vector2(0f, 270f), 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            Boton(go.transform, font, "Pantalla", new Vector2(0f, 190f), () => { AjustesDeJuego.AlternarPantallaCompleta(); Refrescar(go.transform); });
            Boton(go.transform, font, "Resolucion", new Vector2(0f, 130f), () => { AjustesDeJuego.SiguienteResolucion(); Refrescar(go.transform); });
            Boton(go.transform, font, "Calidad", new Vector2(0f, 70f), () => { AjustesDeJuego.SiguienteCalidad(); Refrescar(go.transform); });
            Boton(go.transform, font, "Daltonismo", new Vector2(0f, -10f), () => { AjustesDeJuego.PonerDaltonismo(!AjustesDeJuego.Daltonismo); Refrescar(go.transform); });
            Boton(go.transform, font, "HudMinimo", new Vector2(0f, -70f), () => { AjustesDeJuego.PonerHudMinimo(!AjustesDeJuego.HudMinimo); Refrescar(go.transform); });
            Boton(go.transform, font, "Escala", new Vector2(0f, -130f), () => { AjustesDeJuego.SiguienteEscala(); Refrescar(go.transform); });
            Texto(go.transform, font, "Daltonismo cambia verde y rojo por azul y naranja.\nHUD minimo oculta mision, minimapa y escuadra (tecla F10).\nMando: stick izq. mover, stick der. mirar, RT disparar,\nA saltar, B agacharse, X recargar, RB/LB cambiar arma, Start pausa.", new Vector2(0f, -225f), 14, TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(400f, 130f));
            return go.transform;
        }

        static void Refrescar(Transform extra)
        {
            if (extra == null) return;
            Poner(extra, "Pantalla", "PANTALLA: " + (AjustesDeJuego.PantallaCompleta ? "COMPLETA" : "VENTANA"));
            Poner(extra, "Resolucion", "RESOLUCION: " + AjustesDeJuego.TextoResolucion());
            Poner(extra, "Calidad", "CALIDAD: " + AjustesDeJuego.TextoCalidad());
            Poner(extra, "Daltonismo", "DALTONISMO: " + (AjustesDeJuego.Daltonismo ? "SI" : "NO"));
            Poner(extra, "HudMinimo", "HUD MINIMO: " + (AjustesDeJuego.HudMinimo ? "SI" : "NO"));
            Poner(extra, "Escala", "TAMANO DE INTERFAZ: " + AjustesDeJuego.TextoEscala());
        }
        static void Poner(Transform extra, string boton, string texto)
        {
            var t = extra.Find(boton);
            var tx = t != null ? t.GetComponentInChildren<Text>() : null;
            if (tx != null) tx.text = texto;
        }

        static void Texto(Transform padre, Font font, string s, Vector2 pos, int size, TextAnchor anchor, FontStyle style, Vector2? tam = null)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = tam ?? new Vector2(400f, 34f);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.fontStyle = style; t.text = s; t.color = Color.white; t.raycastTarget = false;
        }

        static void Boton(Transform padre, Font font, string nombre, Vector2 pos, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(400f, 48f);
            go.GetComponent<Image>().color = new Color(0.22f, 0.32f, 0.45f, 1f);
            var b = go.GetComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(accion);
            ButtonSfx.Attach(b);
            var t = new GameObject("Label", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(go.transform, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tx = t.GetComponent<Text>();
            tx.font = font; tx.fontSize = 20; tx.alignment = TextAnchor.MiddleCenter; tx.color = Color.white; tx.raycastTarget = false; tx.text = nombre;
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
