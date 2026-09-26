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
            var extra = settingsPanel.transform.Find(Nombre);
            if (extra == null) extra = Construir(rt);
            RefrescarTextos(extra);
            RefrescarValores(extra);
            // Ronda 13 (punto 6): el reacomodo ya no es un calculo de una sola vez al abrir: LayoutDeAjustes lo repite cada vez
            // que cambia el area del canvas (resolucion / tamano de interfaz), el idioma o algun texto del panel.
            var layout = LayoutDeAjustes.Asegurar(settingsPanel);
            layout.Aplicar();
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
            rt.sizeDelta = new Vector2(440f, 700f);
            go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.97f);

            Texto(go.transform, font, "PANTALLA Y ACCESIBILIDAD", new Vector2(0f, 245f), 22, TextAnchor.MiddleCenter, FontStyle.Bold);
            Boton(go.transform, font, "Pantalla", "PANTALLA", new Vector2(0f, 130f), () => { AjustesDeJuego.AlternarPantallaCompleta(); RefrescarValores(go.transform); });
            CrearDropdown(go.transform, font, "Resolucion", "RESOLUCION", new Vector2(0f, 70f));
            Boton(go.transform, font, "Escala", "TAMANO DE INTERFAZ", new Vector2(0f, 10f), () => { AjustesDeJuego.SiguienteEscala(); RefrescarValores(go.transform); });
            Boton(go.transform, font, "Idioma", "IDIOMA / LANGUAGE", new Vector2(0f, -50f), () => { SP.Core.Loc.Alternar(); RefrescarValores(go.transform); });
            Texto(go.transform, font, "Mando: stick izq. mover, stick der. mirar, RT disparar,\nA saltar, B agacharse, X recargar, RB/LB cambiar arma, Start pausa.", new Vector2(0f, -150f), 14, TextAnchor.MiddleCenter, FontStyle.Normal, new Vector2(400f, 100f));
            return go.transform;
        }

        // BUG REAL ("'Pantalla' se pisa con 'Aplicar'", detectado por LayoutDeAjustes.Diagnosticar y
        // por el crash de Fase21_Ajustes al invocar Button.onClick sobre lo que ya es un Dropdown):
        // esta fila creaba el dropdown, "Aplicar" y su label como HIJOS SUELTOS de "AjustesExtra", con
        // una posicion absoluta puesta una sola vez aca. LayoutDeAjustes.AcomodarExtra solo reacomoda
        // lo que figura en OrdenDeBotonesExtra por nombre directo ("Resolucion" entre ellos), y como
        // "Resolucion" ERA el propio GameObject del dropdown, Colocar() lo movia a el a la fila que le
        // tocara en la pila -- pero "Aplicar" y el label de al lado, al no estar en esa lista, se
        // quedaban clavados en su lugar original y terminaban pisando la fila de arriba ("Pantalla")
        // en cuanto el panel se reacomodaba a otra resolucion, escala de interfaz o idioma.
        // Ahora "Resolucion" es una FILA contenedora (como devuelve RefrescarTextos al buscar
        // "Resolucion/Label" y "Resolucion/Aplicar/Label", que ya esperaban esta forma) con el label,
        // el dropdown y "Aplicar" adentro, ubicados con anclas relativas al ancho de la fila: cuando
        // Colocar() la reposiciona entera, los tres se mueven juntos.
        static void CrearDropdown(Transform padre, Font font, string nombre, string titulo, Vector2 pos)
        {
            var fila = new GameObject(nombre, typeof(RectTransform));
            fila.transform.SetParent(padre, false);
            var filaRt = (RectTransform)fila.transform;
            filaRt.anchoredPosition = pos; filaRt.sizeDelta = new Vector2(400f, 48f);

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(filaRt, false);
            var lrt = (RectTransform)lblGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(10f, 0f); lrt.offsetMax = new Vector2(-260f, 0f);
            var ltx = lblGo.GetComponent<Text>();
            ltx.font = font; ltx.fontSize = 18; ltx.alignment = TextAnchor.MiddleLeft; ltx.color = Color.white; ltx.raycastTarget = false; ltx.text = titulo;

            var go = UnityEngine.UI.DefaultControls.CreateDropdown(new UnityEngine.UI.DefaultControls.Resources());
            go.name = "Dropdown";
            go.transform.SetParent(filaRt, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(150f, 0f); rt.sizeDelta = new Vector2(140f, 48f);
            var d = go.GetComponent<Dropdown>();
            d.ClearOptions();
            var l = AjustesDeJuego.Resoluciones();
            var opts = new List<string>();
            foreach(var r in l) opts.Add($"{r.width}x{r.height}");
            d.AddOptions(opts);
            d.value = AjustesDeJuego.IndiceResolucion();
            d.onValueChanged.AddListener(v => {
                PlayerPrefs.SetInt("sp_resolucion", v); PlayerPrefs.Save();
            });
            var t = go.GetComponentInChildren<Text>();
            if (t != null) { t.font = font; t.fontSize = 16; t.color = Color.black; }
            var t2 = go.transform.Find("Template/Viewport/Content/Item/Item Label")?.GetComponent<Text>();
            if (t2 != null) { t2.font = font; t2.fontSize = 16; t2.color = Color.black; }

            var btnGo = new GameObject("Aplicar", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(filaRt, false);
            var brt = (RectTransform)btnGo.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(90f, 48f);
            btnGo.GetComponent<Image>().color = new Color(0.22f, 0.32f, 0.45f, 1f);
            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(() => AjustesDeJuego.AplicarPantalla());
            ButtonSfx.Attach(btn);
            var btnTxt = new GameObject("Label", typeof(RectTransform), typeof(Text));
            btnTxt.transform.SetParent(btnGo.transform, false);
            var btrt = (RectTransform)btnTxt.transform;
            btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = btrt.offsetMax = Vector2.zero;
            var btx = btnTxt.GetComponent<Text>();
            btx.font = font; btx.fontSize = 16; btx.alignment = TextAnchor.MiddleCenter; btx.color = Color.white; btx.text = "APLICAR";
        }

        public static void RefrescarTextos(Transform extra)
        {
            if (extra == null) return;
            var t1 = extra.Find("Texto")?.GetComponent<Text>(); if (t1 != null) t1.text = SP.Core.Loc.T("PANTALLA Y ACCESIBILIDAD");
            var l1 = extra.Find("Pantalla/Label")?.GetComponent<Text>(); if (l1 != null) l1.text = SP.Core.Loc.T("PANTALLA");
            var l2 = extra.Find("Resolucion/Label")?.GetComponent<Text>(); if (l2 != null) l2.text = SP.Core.Loc.T("RESOLUCION");
            var l3 = extra.Find("Escala/Label")?.GetComponent<Text>(); if (l3 != null) l3.text = SP.Core.Loc.T("TAMANO DE INTERFAZ");
            var l4 = extra.Find("Idioma/Label")?.GetComponent<Text>(); if (l4 != null) l4.text = "IDIOMA / LANGUAGE";
            var btnA = extra.Find("Resolucion/Aplicar/Label")?.GetComponent<Text>(); if (btnA != null) btnA.text = SP.Core.Loc.T("APLICAR");
            var t2 = extra.Find("Texto (1)")?.GetComponent<Text>(); if (t2 != null) t2.text = SP.Core.Loc.T("Mando: stick izq. mover, stick der. mirar, RT disparar,\nA saltar, B agacharse, X recargar, RB/LB cambiar arma, Start pausa.");
        }

        public static void RefrescarValores(Transform extra)
        {
            if (extra == null) return;
            Poner(extra, "Pantalla", SP.Core.Loc.T(AjustesDeJuego.PantallaCompleta ? "COMPLETA" : "VENTANA"));
            Poner(extra, "Escala", AjustesDeJuego.TextoEscala());
            Poner(extra, "Idioma", SP.Core.Loc.T(SP.Core.Loc.Actual == SP.Core.Idioma.Es ? "ESPANOL" : "ENGLISH") + "  [F12]");
        }
        static void Poner(Transform extra, string boton, string texto)
        {
            var t = extra.Find(boton);
            var tx = t != null ? t.Find("Value")?.GetComponent<Text>() : null;
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

        static void Boton(Transform padre, Font font, string nombre, string titulo, Vector2 pos, UnityEngine.Events.UnityAction accion)
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
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(20f, 0f); trt.offsetMax = new Vector2(-150f, 0f);
            var tx = t.GetComponent<Text>();
            tx.font = font; tx.fontSize = 20; tx.alignment = TextAnchor.MiddleLeft; tx.color = Color.white; tx.raycastTarget = false; tx.text = titulo;
            
            var v = new GameObject("Value", typeof(RectTransform), typeof(Text));
            v.transform.SetParent(go.transform, false);
            var vrt = (RectTransform)v.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = new Vector2(250f, 0f); vrt.offsetMax = new Vector2(-20f, 0f);
            var vx = v.GetComponent<Text>();
            vx.font = font; vx.fontSize = 20; vx.alignment = TextAnchor.MiddleRight; vx.color = Color.white; vx.raycastTarget = false; vx.text = "";
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
