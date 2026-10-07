using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.Presentation
{
    // Menú de inicio: [Jugar] carga la escena de gameplay, [Salir] cierra
    // el juego (o sale de Play mode si esto corre en el Editor).
    public class MainMenuController : MonoBehaviour
    {
        // button.onClick.AddListener(...) hecho en un script de Editor
        // (al armar la escena, fuera de Play mode) NO sobrevive a Play
        // mode: UnityEvent solo conserva los listeners "persistentes"
        // (los cargados a mano en el Inspector), no los agregados por
        // código estando en Edit mode. Por eso la conexión real pasa acá,
        // en Awake, que sí corre durante Play mode.
        void Awake()
        {
            // El cartel contextual ("MANTEN [E]...") sobrevive al cambio de escena: en el menu quedaba pegado el de la partida anterior.
            InteractHintView.Mostrar(null);
            var canvasRoot = transform.parent;
            if (canvasRoot == null) return;
            var playBtn = canvasRoot.Find("PlayButton")?.GetComponent<Button>();
            var tutorialBtn = canvasRoot.Find("TutorialButton")?.GetComponent<Button>();
            var exitBtn = canvasRoot.Find("ExitButton")?.GetComponent<Button>();
            if (tutorialBtn != null) { tutorialBtn.onClick.AddListener(OnTutorialClicked); SP.UI.ButtonSfx.Attach(tutorialBtn); }
            if (playBtn != null) { playBtn.onClick.AddListener(OnPlayClicked); SP.UI.ButtonSfx.Attach(playBtn); }
            if (exitBtn != null) { exitBtn.onClick.AddListener(OnExitClicked); SP.UI.ButtonSfx.Attach(exitBtn); }
            PrepararContinuar(canvasRoot, playBtn, tutorialBtn, exitBtn);

            confirmExitPanel = canvasRoot.Find("ConfirmExitPanel")?.gameObject;
            if (confirmExitPanel != null)
            {
                var noBtn = confirmExitPanel.transform.Find("NoButton")?.GetComponent<Button>();
                var yesBtn = confirmExitPanel.transform.Find("YesButton")?.GetComponent<Button>();
                if (noBtn != null) { noBtn.onClick.AddListener(OnConfirmExitNo); SP.UI.ButtonSfx.Attach(noBtn); }
                if (yesBtn != null) { yesBtn.onClick.AddListener(OnConfirmExitYes); SP.UI.ButtonSfx.Attach(yesBtn); }
            }
        }

        GameObject confirmExitPanel;

        // ------------------------------------------------------------------
        // P10 (#120): CONTINUAR. Aparece solo si hay una partida guardada valida de la Operacion (si el archivo esta roto o es de otra version
        // no aparece y el aviso queda en el log). Se crea en runtime clonando JUGAR y se reacomoda la columna de botones.
        // ------------------------------------------------------------------
        Button botonContinuar;
        public Button BotonContinuar => botonContinuar;

        void PrepararContinuar(Transform raiz, Button jugar, Button tutorial, Button salir)
        {
            if (jugar == null || !PartidaGuardada.HayPartida()) return;
            var info = PartidaGuardada.UltimaInfo();
            var go = Instantiate(jugar.gameObject, raiz);
            go.name = "ContinueButton";
            go.GetComponent<Image>().color = new Color(0.9f, 0.62f, 0.2f);
            var t = go.GetComponentInChildren<Text>(true);
            if (t != null)
            {
                // Dos textos: el titulo del boton (un poco hacia arriba) y, debajo, "OBJETIVO n/6 · fecha" en chico (con una sola Text de dos
                // lineas el renglon chico quedaba recortado).
                t.text = "CONTINUAR";
                t.rectTransform.offsetMin = new Vector2(t.rectTransform.offsetMin.x, t.rectTransform.offsetMin.y + 14f);
                var subGO = new GameObject("Detalle", typeof(RectTransform), typeof(Text));
                subGO.transform.SetParent(t.transform.parent, false);
                var sub = subGO.GetComponent<Text>();
                sub.font = t.font; sub.fontSize = 13; sub.fontStyle = FontStyle.Bold; sub.alignment = TextAnchor.LowerCenter; sub.raycastTarget = false;
                sub.color = new Color(1f, 0.96f, 0.85f, 0.95f); sub.text = info.Texto;
                sub.horizontalOverflow = HorizontalWrapMode.Overflow; sub.verticalOverflow = VerticalWrapMode.Overflow;
                var srt = sub.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = new Vector2(0f, 5f); srt.offsetMax = Vector2.zero;
            }
            botonContinuar = go.GetComponent<Button>();
            botonContinuar.onClick.RemoveAllListeners();
            botonContinuar.onClick.AddListener(OnContinuarClicked);
            SP.UI.ButtonSfx.Attach(botonContinuar);
            // Columna: CONTINUAR, JUGAR, TUTORIAL, SALIR (el panel de fondo crece 70 hacia abajo).
            Colocar(botonContinuar, 62f); Colocar(jugar, -8f); Colocar(tutorial, -78f); Colocar(salir, -148f);
            var panel = raiz.Find("PanelBotones") as RectTransform;
            if (panel != null) { panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + 70f); panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, panel.anchoredPosition.y - 35f); }
        }

        static void Colocar(Button b, float y)
        {
            if (b == null) return;
            var rt = (RectTransform)b.transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        public void OnContinuarClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono continuar la partida guardada");
            if (!PartidaGuardada.Continuar()) actionTaken = false;
        }

        void Start() => GameLog.Line("Pantalla de menu cargada");


        // Un doble click en Jugar (pasa seguido: el segundo click del
        // mouse cae antes de que la escena termine de cambiar) disparaba
        // "Se selecciono iniciar partida" dos veces en el log por una
        // sola intención del jugador.
        bool actionTaken;

        public void OnPlayClicked()
        {
            if (actionTaken) return;
            // JUGAR no arranca directo: primero se elige el NIVEL (y despues, en el nivel 1, la dificultad).
            MostrarNiveles();
        }

        // ------------------------------------------------------------------
        // Selector de niveles (grid de 2x2). Nivel 1 y 2 juegan; 3 y 4 estan "desactivados": se ven apagados y al hacerles click
        // sale un cartel de "en desarrollo" (por eso NO usan Button.interactable=false, que ignora el click).
        // ------------------------------------------------------------------
        GameObject panelNiveles, popupEnDesarrollo;
        public GameObject PanelNiveles => panelNiveles;
        public GameObject PopupEnDesarrollo => popupEnDesarrollo;
        public string UltimoNivelPedido { get; private set; }

        static readonly string[] NombresDeNivel = { "NIVEL 1", "NIVEL 2", "NIVEL 3", "NIVEL 4" };
        static readonly string[] TitulosDeNivel = { "STRATEGIC POINT", "OPERACION CUARTEL", "PROXIMAMENTE", "PROXIMAMENTE" };
        static readonly string[] DescripcionesDeNivel =
        {
            "Defende el punto estrategico: ordenes a tu escuadra, vehiculos y vista tactica RTS.",
            "Infiltrate en un cuartel, abri 3 puestos de control, hackea el centro de datos, huí en el tanque y resisti hasta el helicoptero.",
            "", ""
        };
        static readonly bool[] NivelDisponible = { true, true, false, false };

        public void MostrarNiveles()
        {
            var raiz = transform.parent;
            if (raiz == null) { MostrarDificultad(); return; }
            if (panelNiveles != null) { panelNiveles.SetActive(true); return; }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelNiveles = new GameObject("PanelNiveles", typeof(RectTransform), typeof(Image));
            panelNiveles.transform.SetParent(raiz, false);
            panelNiveles.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 1f);
            var frt = panelNiveles.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

            Texto(panelNiveles.transform, font, "ELEGI EL NIVEL", F(54), new Vector2(0.5f, 0.9f), new Vector2(1200f * E, 90f * E), Color.white);

            // El grid: GridLayoutGroup 2 columnas, celdas fijas, centrado.
            var gridGO = new GameObject("GridNiveles", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(panelNiveles.transform, false);
            var grt = gridGO.GetComponent<RectTransform>();
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(1000f * E, 560f * E);
            grt.anchoredPosition = new Vector2(0f, -10f);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(480f * E, 260f * E);
            grid.spacing = new Vector2(40f * E, 40f * E);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var colores = new[] { new Color(0.2f, 0.45f, 0.62f), new Color(0.62f, 0.33f, 0.16f), new Color(0.2f, 0.22f, 0.26f), new Color(0.2f, 0.22f, 0.26f) };
            for (int i = 0; i < 4; i++)
            {
                int n = i + 1;
                var celda = new GameObject("Nivel" + n, typeof(RectTransform), typeof(Image), typeof(Button));
                celda.transform.SetParent(gridGO.transform, false);
                celda.GetComponent<Image>().color = colores[i];
                bool ok = NivelDisponible[i];
                var tc = new Color(1f, 1f, 1f, ok ? 1f : 0.42f);
                Texto(celda.transform, font, NombresDeNivel[i], F(44), new Vector2(0.5f, 0.82f), new Vector2(440f * E, 60f * E), tc);
                Texto(celda.transform, font, TitulosDeNivel[i], F(26), new Vector2(0.5f, 0.62f), new Vector2(440f * E, 40f * E), ok ? new Color(1f, 0.93f, 0.6f) : new Color(1f, 1f, 1f, 0.35f));
                if (ok) Texto(celda.transform, font, DescripcionesDeNivel[i], F(18), new Vector2(0.5f, 0.27f), new Vector2(440f * E, 120f * E), new Color(1f, 1f, 1f, 0.88f));
                else Texto(celda.transform, font, "BLOQUEADO", F(22), new Vector2(0.5f, 0.3f), new Vector2(440f * E, 40f * E), new Color(1f, 1f, 1f, 0.3f));
                var b = celda.GetComponent<Button>();
                int nivelPedido = n;
                b.onClick.AddListener(() => ElegirNivel(nivelPedido));
                SP.UI.ButtonSfx.Attach(b);
            }

            var volver = new GameObject("Volver", typeof(RectTransform), typeof(Image), typeof(Button));
            volver.transform.SetParent(panelNiveles.transform, false);
            volver.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.34f);
            var vrt = volver.GetComponent<RectTransform>();
            vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.07f);
            vrt.sizeDelta = new Vector2(260f * E, 56f * E);
            Texto(volver.transform, font, "VOLVER", F(26), new Vector2(0.5f, 0.5f), new Vector2(240f * E, 50f * E), Color.white);
            var vb = volver.GetComponent<Button>();
            vb.onClick.AddListener(() => panelNiveles.SetActive(false));
            SP.UI.ButtonSfx.Attach(vb);
        }

        public void ElegirNivel(int n)
        {
            if (actionTaken) return;
            UltimoNivelPedido = "Nivel " + n;
            if (n < 1 || n > NivelDisponible.Length || !NivelDisponible[n - 1]) { MostrarEnDesarrollo(n); return; }
            if (n == 1) { MostrarDificultad(); return; }
            actionTaken = true;
            GameLog.Line("Se selecciono el nivel 2: Operacion Cuartel");
            SceneLoader.Cargar("SC_Operacion");
        }

        // Ventana de confirmacion: "NIVEL 3 · EN DESARROLLO".
        public void MostrarEnDesarrollo(int n)
        {
            var raiz = transform.parent;
            if (raiz == null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (popupEnDesarrollo == null)
            {
                popupEnDesarrollo = new GameObject("PopupEnDesarrollo", typeof(RectTransform), typeof(Image));
                popupEnDesarrollo.transform.SetParent(raiz, false);
                popupEnDesarrollo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);   // vela el resto y bloquea los clicks de atras
                var prt = popupEnDesarrollo.GetComponent<RectTransform>();
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;

                var caja = new GameObject("Caja", typeof(RectTransform), typeof(Image));
                caja.transform.SetParent(popupEnDesarrollo.transform, false);
                caja.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
                var crt = caja.GetComponent<RectTransform>();
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(760f * E, 360f * E);
                var tituloGO = new GameObject("Titulo", typeof(RectTransform), typeof(Text));
                tituloGO.transform.SetParent(caja.transform, false);
                var t = tituloGO.GetComponent<Text>();
                t.font = font; t.fontSize = F(40); t.fontStyle = FontStyle.Bold; t.color = new Color(1f, 0.82f, 0.3f); t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                var trt = t.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.72f); trt.anchoredPosition = Vector2.zero; trt.sizeDelta = new Vector2(700f * E, 70f * E);
                tituloGO.name = "TextoTitulo";
                Texto(caja.transform, font, "Este nivel todavia esta en desarrollo.\nProximamente.", F(24), new Vector2(0.5f, 0.45f), new Vector2(680f * E, 110f * E), Color.white);
                var ok = new GameObject("Aceptar", typeof(RectTransform), typeof(Image), typeof(Button));
                ok.transform.SetParent(caja.transform, false);
                ok.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.62f);
                var ort = ok.GetComponent<RectTransform>();
                ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0.14f);
                ort.sizeDelta = new Vector2(260f * E, 56f * E);
                Texto(ok.transform, font, "ACEPTAR", F(26), new Vector2(0.5f, 0.5f), new Vector2(240f * E, 50f * E), Color.white);
                var ob = ok.GetComponent<Button>();
                ob.onClick.AddListener(() => popupEnDesarrollo.SetActive(false));
                SP.UI.ButtonSfx.Attach(ob);
            }
            var titulo = popupEnDesarrollo.transform.Find("Caja/TextoTitulo")?.GetComponent<Text>();
            if (titulo != null) titulo.text = "NIVEL " + n + " · EN DESARROLLO";
            popupEnDesarrollo.transform.SetAsLastSibling();
            popupEnDesarrollo.SetActive(true);
        }

        GameObject panelDificultad;

        // Ronda 11 (punto 4): el lienzo del menu es de 960x540 (ver MenuSceneBuilder) y este panel estaba dibujado con medidas de
        // 1920x1080: las tres tarjetas sumaban 1410 de ancho sobre 960 y se salian de la pantalla. Todo se dibuja a media escala.
        const float E = 0.5f;
        static int F(int px) => Mathf.Max(11, Mathf.RoundToInt(px * E));

        public void MostrarDificultad()
        {
            var raiz = transform.parent;
            if (raiz == null) { IniciarPartida(NivelDificultad.Facil); return; }
            if (panelDificultad != null) { panelDificultad.SetActive(true); return; }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelDificultad = new GameObject("PanelDificultad", typeof(RectTransform), typeof(Image));
            panelDificultad.transform.SetParent(raiz, false);
            var fondo = panelDificultad.GetComponent<Image>();
            fondo.color = new Color(0.04f, 0.05f, 0.07f, 1f);   // opaco: con 0,96 se transparentaban el titulo y los botones del menu de atras
            var frt = panelDificultad.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

            Texto(panelDificultad.transform, font, "ELEGI LA DIFICULTAD", F(54), new Vector2(0.5f, 0.86f), new Vector2(1200f * E, 90f * E), Color.white);
            Texto(panelDificultad.transform, font, "Cambia la vida y el daño de los enemigos, de tu escuadra y el tuyo.", F(22), new Vector2(0.5f, 0.78f), new Vector2(1200f * E, 40f * E), new Color(0.75f, 0.8f, 0.88f));

            var niveles = new[] { NivelDificultad.Facil, NivelDificultad.Medio, NivelDificultad.Dificil };
            var colores = new[] { new Color(0.2f, 0.55f, 0.35f), new Color(0.2f, 0.42f, 0.7f), new Color(0.65f, 0.22f, 0.2f) };
            for (int i = 0; i < 3; i++)
            {
                var nivel = niveles[i];
                var d = Dificultad.Datos(nivel);
                bool elegida = nivel == Dificultad.Actual;   // FACIL por defecto; despues, la ultima que elegiste
                if (elegida)
                {
                    var marco = new GameObject("Marco_" + d.Nombre, typeof(RectTransform), typeof(Image));
                    marco.transform.SetParent(panelDificultad.transform, false);
                    marco.GetComponent<Image>().color = Color.white;
                    marco.GetComponent<Image>().raycastTarget = false;
                    var mrt = marco.GetComponent<RectTransform>();
                    mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.45f);
                    mrt.anchoredPosition = new Vector2((i - 1) * 470f * E, 0f);
                    mrt.sizeDelta = new Vector2(440f * E + 8f, 360f * E + 8f);
                }
                var go = new GameObject("Dificultad_" + d.Nombre, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panelDificultad.transform, false);
                go.GetComponent<Image>().color = colores[i];
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.45f);
                rt.anchoredPosition = new Vector2((i - 1) * 470f * E, 0f);
                rt.sizeDelta = new Vector2(440f * E, 360f * E);
                Texto(go.transform, font, d.Nombre, F(40), new Vector2(0.5f, 0.86f), new Vector2(420f * E, 60f * E), Color.white);
                if (elegida) Texto(go.transform, font, nivel == NivelDificultad.Facil && !PlayerPrefs.HasKey("sp_dificultad_v2") ? "POR DEFECTO" : "ELEGIDA", F(18), new Vector2(0.5f, 0.945f), new Vector2(300f * E, 26f * E), new Color(1f, 0.95f, 0.6f));
                Texto(go.transform, font, Dificultad.Resumen(nivel), F(22), new Vector2(0.5f, 0.52f), new Vector2(420f * E, 150f * E), Color.white, TextAnchor.MiddleLeft);
                Texto(go.transform, font, d.Frase, F(20), new Vector2(0.5f, 0.15f), new Vector2(400f * E, 80f * E), new Color(1f, 1f, 1f, 0.85f));
                var b = go.GetComponent<Button>();
                b.onClick.AddListener(() => IniciarPartida(nivel));
                SP.UI.ButtonSfx.Attach(b);
            }

            var volver = new GameObject("Volver", typeof(RectTransform), typeof(Image), typeof(Button));
            volver.transform.SetParent(panelDificultad.transform, false);
            volver.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.34f);
            var vrt = volver.GetComponent<RectTransform>();
            vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.12f);
            vrt.sizeDelta = new Vector2(260f * E, 56f * E);
            Texto(volver.transform, font, "VOLVER", F(26), new Vector2(0.5f, 0.5f), new Vector2(240f * E, 50f * E), Color.white);
            var vb = volver.GetComponent<Button>();
            vb.onClick.AddListener(() => panelDificultad.SetActive(false));
            SP.UI.ButtonSfx.Attach(vb);
        }

        static void Texto(Transform padre, Font font, string texto, int tam, Vector2 ancla, Vector2 caja, Color color, TextAnchor alinear = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.text = texto; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.color = color;
            t.alignment = alinear; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = ancla;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = caja;
        }

        public void IniciarPartida(NivelDificultad nivel)
        {
            if (actionTaken) return;
            actionTaken = true;
            Dificultad.Actual = nivel;
            GameLog.Line($"Se selecciono iniciar partida (dificultad {Dificultad.Datos(nivel).Nombre})");
            SceneLoader.Cargar("SC_Gameplay");
        }

        public void OnTutorialClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono el tutorial");
            SceneLoader.Cargar("SC_Tutorial");
        }

        // Un click accidental en SALIR (pegado a JUGAR y TUTORIAL) no debe
        // cerrar la aplicacion sin aviso -- mismo motivo y mismo patron que
        // el ConfirmExitPanel de la pausa (PauseController.OnMenuClicked).
        public void OnExitClicked()
        {
            if (actionTaken) return;
            if (confirmExitPanel == null) { OnConfirmExitYes(); return; }
            if (confirmExitPanel.activeSelf) return;
            confirmExitPanel.SetActive(true);
        }

        public void OnConfirmExitNo()
        {
            if (confirmExitPanel == null || !confirmExitPanel.activeSelf) return;
            confirmExitPanel.SetActive(false);
        }

        public void OnConfirmExitYes()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono salir del juego");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
