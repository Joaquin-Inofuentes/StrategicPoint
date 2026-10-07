using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Core;
using SP.UI;

namespace SP.Presentation
{
    // Pausa con [ESC]: congela el tiempo (Time.timeScale=0) y muestra el
    // panel de pausa, con un sub-panel de "Configuraciones" (sensibilidad
    // de mouse y volumen) navegable desde ahí mismo.
    public class PauseController : MonoBehaviour
    {
        GameObject pausePanel;
        GameObject settingsPanel;
        GameObject controlsPanel;
        GameObject confirmExitPanel;
        GameObject rebindPanel;
        GameOutcomeController outcome;
        SP.Player.PlayerInputDriver input;
        Text controlsListTxt;

        // Wireados desde HeadlessTestRunner para las opciones de
        // accesibilidad: tamaño de HUD y de mirilla.
        public CanvasScaler HudScaler;
        public AimUI AimUiRef;
        // Pedido explicito: "duplica los tamaños de letras de todo" -- esta
        // es la resolucion que representa el 1.00 de la barra de "Tamaño de
        // HUD" (ver mas abajo, HudScaler.referenceResolution = Base / v).
        // Bajarla a la mitad de la resolucion real de referencia (1920x1080)
        // duplica el HUD entero para CUALQUIER valor guardado de la barra,
        // sin correrle el rango ni el 1.00 de default que el jugador ya
        // conoce -- ver el mismo numero y la misma explicacion en
        // HeadlessTestRunner.BuildUI.
        static readonly Vector2 BaseReferenceResolution = new Vector2(960f, 540f);

        const string PrefVolume = "sp_volume";
        const string PrefSensitivity = "sp_sensitivity";
        const string PrefTurretSensitivity = "sp_turret_sensitivity";
        const string PrefHudScale = "sp_hud_scale";
        const string PrefCrosshairScale = "sp_crosshair_scale";
        const string PrefInvertY = "sp_invert_y";

        public bool IsPaused { get; private set; }

        // Escala de tiempo previa a la pausa (ver ShowPause).
        float escalaPrevia = 1f;

        public void Bind(GameObject pause, GameObject settings)
        {
            pausePanel = pause;
            settingsPanel = settings;
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        bool buttonsWired;

        // Los carteles con canvas propio (pista de interaccion, acciones en curso, mision) van en ordenes 35-40 y quedaban
        // ENCIMA de la pausa y de Configuraciones. Ademas, mientras vivian en el canvas del HUD, el slider de "Tamano de HUD"
        // (que cambia la resolucion de referencia de ESE canvas) agrandaba y corria la propia pantalla de Configuraciones bajo el
        // mouse (bug #099). Todo el arbol de la pausa pasa a un Canvas RAIZ propio ("CanvasPausa", orden 900, 960x540 fijo) que
        // el HUD no toca. Es de runtime: no se serializa en la escena.
        public const string NombreCanvasPausa = "CanvasPausa";

        void SubirALaCapaDePausa()
        {
            if (!(transform is RectTransform)) return;
            var actual = GetComponentInParent<Canvas>();
            if (actual != null && actual.rootCanvas.gameObject.name == NombreCanvasPausa) return;   // ya esta en su canvas
            var go = new GameObject(NombreCanvasPausa, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 900;
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = BaseReferenceResolution;
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.matchWidthOrHeight = 0.5f;
            // En la misma escena que el controlador (no en la activa, por si hay escenas aditivas).
            if (gameObject.scene.IsValid() && gameObject.scene != go.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            transform.SetParent(go.transform, false);
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void OnEnable()
        {
            SubirALaCapaDePausa();
            if (pausePanel == null)
            {
                var t = transform.Find("PausePanel");
                if (t != null) pausePanel = t.gameObject;
            }
            if (settingsPanel == null)
            {
                var t = transform.Find("SettingsPanel");
                if (t != null) settingsPanel = t.gameObject;
            }
            if (controlsPanel == null)
            {
                var t = transform.Find("ControlsPanel");
                if (t != null) controlsPanel = t.gameObject;
            }
            if (controlsListTxt == null && controlsPanel != null)
            {
                var t = controlsPanel.transform.Find("List");
                if (t != null) controlsListTxt = t.GetComponent<Text>();
            }
            if (confirmExitPanel == null)
            {
                var t = transform.Find("ConfirmExitPanel");
                if (t != null) confirmExitPanel = t.gameObject;
            }
            // CANCELAR y SALIR se solapaban 80 pixeles: en esa franja el
            // click se lo llevaba el boton equivocado. Ver SP.UI.Diagramador.
            SP.UI.Diagramador.AcomodarConfirmarSalida(confirmExitPanel);
            if (rebindPanel == null)
            {
                var t = transform.Find("RebindPanel");
                if (t != null) rebindPanel = t.gameObject;
            }
            if (outcome == null) outcome = GameOutcomeController.Activo;
            if (input == null) input = SP.Player.PlayerInputDriver.Activo;

            // Igual que en MainMenuController: los onClick.AddListener()
            // hechos al armar la escena en el Editor no sobreviven a Play
            // mode, hay que conectarlos acá en tiempo real. Una sola vez:
            // OnEnable puede volver a correr (p.ej. tras des/activar el
            // panel) y no hay que duplicar el listener.
            if (buttonsWired) return;
            buttonsWired = true;

            WireButton(pausePanel, "ContinueButton", OnContinueClicked);
            WireButton(pausePanel, "SettingsButton", OnSettingsClicked);
            WireButton(pausePanel, "ControlsButton", OnControlsClicked);
            WireButton(pausePanel, "MenuButton", OnMenuClicked);
            WireButton(settingsPanel, "BackButton", OnSettingsBackClicked);
            WireButton(controlsPanel, "BackButton", OnControlsBackClicked);
            WireButton(controlsPanel, "RebindButton", OnRebindClicked);
            WireButton(rebindPanel, "BackButton", OnRebindBackClicked);
            WireButton(rebindPanel, "ResetButton", OnRebindResetClicked);
            WireButton(confirmExitPanel, "YesButton", OnConfirmExitYes);
            WireButton(confirmExitPanel, "NoButton", OnConfirmExitNo);

            if (settingsPanel != null)
            {
                var volumeValueTxt = settingsPanel.transform.Find("Volumen_Value")?.GetComponent<Text>();
                var volumeSlider = settingsPanel.transform.Find("Volumen_Slider")?.GetComponent<Slider>();
                // Persistencia: sin esto, cambiar volumen o sensibilidad
                // se perdia apenas se recargaba la escena (que es
                // exactamente lo que hace Reintentar) y habia que
                // reconfigurar en cada intento.
                float savedVolume = PlayerPrefs.GetFloat(PrefVolume, 1f);
                if (volumeSlider != null)
                {
                    volumeSlider.SetValueWithoutNotify(savedVolume);
                    if (volumeValueTxt != null) volumeValueTxt.text = savedVolume.ToString("0.00");
                    volumeSlider.onValueChanged.AddListener(v =>
                    {
                        AudioDirector.SetMasterGain(v);
                        if (volumeValueTxt != null) volumeValueTxt.text = v.ToString("0.00");
                    });
                }

                // Pedido explicito: sliders de volumen por canal ademas del
                // master de arriba -- General (Ambiente/musica), VFX
                // (efectos de disparo/impacto/pisadas) y Voces (acuses de
                // orden). Usan AudioDirector.GainFor/SetGain, que ya
                // persiste en PlayerPrefs con su propia clave por canal, asi
                // que aca no hace falta una constante Pref* nueva.
                WireChannelSlider(settingsPanel, "General", SfxChannel.Ambient);
                WireChannelSlider(settingsPanel, "VFX", SfxChannel.Sfx);
                WireChannelSlider(settingsPanel, "Voces", SfxChannel.Voice);

                var sensValueTxt = settingsPanel.transform.Find("Sensibilidad de mouse_Value")?.GetComponent<Text>();
                var sensSlider = settingsPanel.transform.Find("Sensibilidad de mouse_Slider")?.GetComponent<Slider>();
                float savedSensitivity = PlayerPrefs.GetFloat(PrefSensitivity, 0.15f);
                if (input != null) input.LookSensitivity = savedSensitivity;
                if (sensSlider != null)
                {
                    sensSlider.SetValueWithoutNotify(savedSensitivity);
                    if (sensValueTxt != null) sensValueTxt.text = savedSensitivity.ToString("0.00");
                    sensSlider.onValueChanged.AddListener(v =>
                    {
                        if (input != null) input.LookSensitivity = v;
                        PlayerPrefs.SetFloat(PrefSensitivity, v);
                        if (sensValueTxt != null) sensValueTxt.text = v.ToString("0.00");
                    });
                }

                var turretValueTxt = settingsPanel.transform.Find("Sensibilidad de torreta_Value")?.GetComponent<Text>();
                var turretSlider = settingsPanel.transform.Find("Sensibilidad de torreta_Slider")?.GetComponent<Slider>();
                float savedTurretSens = PlayerPrefs.GetFloat(PrefTurretSensitivity, 0.15f);
                if (input != null) input.TurretSensitivity = savedTurretSens;
                if (turretSlider != null)
                {
                    turretSlider.SetValueWithoutNotify(savedTurretSens);
                    if (turretValueTxt != null) turretValueTxt.text = savedTurretSens.ToString("0.00");
                    turretSlider.onValueChanged.AddListener(v =>
                    {
                        if (input != null) input.TurretSensitivity = v;
                        PlayerPrefs.SetFloat(PrefTurretSensitivity, v);
                        if (turretValueTxt != null) turretValueTxt.text = v.ToString("0.00");
                    });
                }

                // Tamaño de HUD: en modo ScaleWithScreenSize, CanvasScaler
                // no expone un multiplicador directo -- la forma real de
                // agrandar/achicar toda la UI es achicar/agrandar la
                // resolucion de referencia (menos referencia = mismos
                // pixeles de diseño ocupan mas pantalla real).
                var hudValueTxt = settingsPanel.transform.Find("Tamaño de HUD_Value")?.GetComponent<Text>();
                var hudSlider = settingsPanel.transform.Find("Tamaño de HUD_Slider")?.GetComponent<Slider>();
                float savedHudScale = PlayerPrefs.GetFloat(PrefHudScale, 1f);
                if (HudScaler != null) HudScaler.referenceResolution = BaseReferenceResolution / savedHudScale;
                if (hudSlider != null)
                {
                    hudSlider.SetValueWithoutNotify(savedHudScale);
                    if (hudValueTxt != null) hudValueTxt.text = savedHudScale.ToString("0.00");
                    // Mientras se arrastra solo cambia el numero: la resolucion de referencia (que re-escala TODO el HUD) se aplica
                    // al soltar. Con teclado o mando no hay puntero apretado y se aplica en el acto.
                    hudSliderRef = hudSlider;
                    hudSlider.onValueChanged.AddListener(v =>
                    {
                        if (hudValueTxt != null) hudValueTxt.text = v.ToString("0.00");
                        var arrastre = hudSlider.GetComponent<SP.UI.SliderDeAjuste>();
                        if (arrastre != null && arrastre.Apretado) { hudPendiente = true; return; }
                        AplicarEscalaDeHud(v);
                    });
                }

                var crossValueTxt = settingsPanel.transform.Find("Tamaño de mirilla_Value")?.GetComponent<Text>();
                var crossSlider = settingsPanel.transform.Find("Tamaño de mirilla_Slider")?.GetComponent<Slider>();
                float savedCrossScale = PlayerPrefs.GetFloat(PrefCrosshairScale, 1f);
                if (AimUiRef != null) AimUiRef.SetCrosshairScale(savedCrossScale);
                if (crossSlider != null)
                {
                    crossSlider.SetValueWithoutNotify(savedCrossScale);
                    if (crossValueTxt != null) crossValueTxt.text = savedCrossScale.ToString("0.00");
                    crossSlider.onValueChanged.AddListener(v =>
                    {
                        if (AimUiRef != null) AimUiRef.SetCrosshairScale(v);
                        PlayerPrefs.SetFloat(PrefCrosshairScale, v);
                        if (crossValueTxt != null) crossValueTxt.text = v.ToString("0.00");
                    });
                }

                var invertToggle = settingsPanel.transform.Find("InvertirEjeY_Toggle")?.GetComponent<Toggle>();
                bool savedInvertY = PlayerPrefs.GetInt(PrefInvertY, 0) == 1;
                if (input != null) input.InvertLookY = savedInvertY;
                if (invertToggle != null)
                {
                    invertToggle.SetIsOnWithoutNotify(savedInvertY);
                    invertToggle.onValueChanged.AddListener(v =>
                    {
                        if (input != null) input.InvertLookY = v;
                        PlayerPrefs.SetInt(PrefInvertY, v ? 1 : 0);
                    });
                }

                // Interruptor de efectos de camara: sacudida, balanceo al
                // caminar, destellos, viñeta de velocidad y latido. Es la
                // principal causa de mareo en un FPS y hasta ahora no habia
                // forma de apagarlo sin apagar el resto del juego. El
                // estado vive en CameraFxSettings (estatico + PlayerPrefs),
                // no en un campo de componente, para que sobreviva el
                // domain reload y lo puedan consultar sistemas repartidos.
                var camFxToggle = settingsPanel.transform.Find("EfectosDeCamara_Toggle")?.GetComponent<Toggle>();
                if (camFxToggle != null)
                {
                    camFxToggle.SetIsOnWithoutNotify(SP.CameraSystem.CameraFxSettings.Enabled);
                    camFxToggle.onValueChanged.AddListener(v => SP.CameraSystem.CameraFxSettings.Enabled = v);
                }
            }
        }

        Slider hudSliderRef;
        bool hudPendiente, hudSoltadoConectado;

        void AplicarEscalaDeHud(float v)
        {
            hudPendiente = false;
            if (HudScaler != null) HudScaler.referenceResolution = BaseReferenceResolution / v;
            PlayerPrefs.SetFloat(PrefHudScale, v);
            PlayerPrefs.Save();
        }

        // Aplica el tamano de HUD pendiente (el que se dejo en el slider al soltar o al cerrar la pantalla a mitad de un arrastre).
        void AplicarHudPendiente()
        {
            if (hudPendiente && hudSliderRef != null) AplicarEscalaDeHud(hudSliderRef.value);
        }

        void ConectarSoltadoDelHud()
        {
            if (hudSoltadoConectado || hudSliderRef == null) return;
            var arrastre = hudSliderRef.GetComponent<SP.UI.SliderDeAjuste>();
            if (arrastre == null) return;   // todavia sin vestir, se conecta en la proxima apertura
            arrastre.Soltado += _ => AplicarHudPendiente();
            hudSoltadoConectado = true;
        }

        // Mismo patron que el slider de Volumen de arriba (Find por nombre
        // + SetValueWithoutNotify + onValueChanged), pero generico por
        // canal de AudioDirector en vez de tocar AudioListener.volume.
        static void WireChannelSlider(GameObject panel, string label, SfxChannel channel)
        {
            if (panel == null) return;
            var valueTxt = panel.transform.Find(label + "_Value")?.GetComponent<Text>();
            var slider = panel.transform.Find(label + "_Slider")?.GetComponent<Slider>();
            if (slider == null) return;

            float saved = AudioDirector.GainFor(channel);
            slider.SetValueWithoutNotify(saved);
            if (valueTxt != null) valueTxt.text = saved.ToString("0.00");
            slider.onValueChanged.AddListener(v =>
            {
                AudioDirector.SetGain(channel, v);
                if (valueTxt != null) valueTxt.text = v.ToString("0.00");
            });
        }

        static void WireButton(GameObject panel, string childName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null) return;
            var t = panel.transform.Find(childName);
            var btn = t != null ? t.GetComponent<Button>() : null;
            if (btn == null) return;
            btn.onClick.AddListener(action);
            // Pedido explicito: sonido al pasar el mouse y al hacer click
            // en todos los botones -- un solo punto de union para los once
            // botones de este controlador.
            SP.UI.ButtonSfx.Attach(btn);
        }

        void Update()
        {
            if (Keyboard.current == null || !Application.isPlaying) return;
            if (SP.Core.SesionLog.DialogoAbierto) return;   // [Esc] cancela el reporte de bug, no abre la pausa
            if (!Keyboard.current.escapeKey.wasPressedThisFrame && !SP.Player.MandoFps.Pausa) return;
            // La partida ya terminó (ganaste/perdiste): [ESC] no debe
            // abrir un menú de pausa encima de esa pantalla.
            if (outcome != null && outcome.IsShowing) return;
            // Tampoco a mitad de la cámara de muerte (se congela bien
            // técnicamente, pero interrumpir esa escena breve con la
            // pausa se siente como un accidente, no una pausa a propósito).
            if (!IsPaused && input != null && input.IsHandlingDeath) return;

            // [ESC] va "un paso atrás" a la vez, nunca salta dos pantallas
            // de golpe: confirmar salida -> controles/config -> pausa.
            if (confirmExitPanel != null && confirmExitPanel.activeSelf) OnConfirmExitNo();
            else if (rebindPanel != null && rebindPanel.activeSelf) OnRebindBackClicked();
            else if (controlsPanel != null && controlsPanel.activeSelf) OnControlsBackClicked();
            else if (settingsPanel != null && settingsPanel.activeSelf) OnSettingsBackClicked();
            else if (IsPaused) OnContinueClicked();
            else ShowPause();
        }

        public void ShowPause()
        {
            if (pausePanel == null || IsPaused) return;
            if (outcome != null && outcome.IsShowing) return;
            if (input != null && input.IsHandlingDeath) return;
            IsPaused = true;
            // Se guarda la escala que HABIA, no se asume que era 1. El
            // juego tiene otro sistema que la toca: al morir el ultimo
            // enemigo, KillFeedbackDirector pone camara lenta (0.25) por
            // 0,9 segundos reales. Pausar y despausar dentro de esa
            // ventana escribia un 1 fijo encima y cancelaba la camara
            // lenta de la victoria -- el momento mas cuidado del juego se
            // perdia por abrir el menu.
            escalaPrevia = Time.timeScale;
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
            PrepararBotonesDeGuardado();
            // El panel tiene que dibujarse ENCIMA de todo el HUD (la mision, el roster, el radial...), que
            // se crean despues y por eso quedaban tapando los menus. Y el audio se pausa de verdad: antes
            // timeScale=0 congelaba el juego pero la musica y los ambientes seguian sonando.
            transform.SetAsLastSibling();
            AudioListener.pause = true;
            GameLog.Line("Se puso en pausa el juego");
        }

        // ---------------------------------------------------------------- P10 (#120): guardar partida desde la pausa
        // Los botones se crean en runtime (clonando CONTINUAR) solo en la Operacion; asi no hace falta rehacer las escenas.
        Button botonGuardar, botonCargar;
        Text notaDeGuardado;
        public Button BotonGuardar => botonGuardar;
        public Button BotonCargar => botonCargar;
        public string TextoDeLaNota => notaDeGuardado != null ? notaDeGuardado.text : "";

        void PrepararBotonesDeGuardado()
        {
            if (pausePanel == null || SP.Operacion.OperacionDirector.Instancia == null) return;
            if (botonGuardar == null)
            {
                var cont = pausePanel.transform.Find("ContinueButton") as RectTransform;
                if (cont == null) return;
                botonGuardar = ClonarBoton(cont, "SaveButton", "GUARDAR PARTIDA", new Color(0.25f, 0.5f, 0.7f), OnGuardarClicked);
                botonCargar = ClonarBoton(cont, "LoadButton", "CARGAR ÚLTIMO GUARDADO", new Color(0.4f, 0.4f, 0.6f), OnCargarClicked);
                // Se reacomoda la columna: titulo arriba, CONTINUAR, GUARDAR, CARGAR y despues el resto.
                var titulo = pausePanel.transform.Find("Title") as RectTransform;
                if (titulo != null) titulo.anchoredPosition = new Vector2(0f, 200f);
                Mover(pausePanel, "ContinueButton", 120f); Mover(pausePanel, "SaveButton", 58f); Mover(pausePanel, "LoadButton", -4f);
                Mover(pausePanel, "SettingsButton", -66f); Mover(pausePanel, "ControlsButton", -128f); Mover(pausePanel, "MenuButton", -190f);
                var ngo = new GameObject("NotaDeGuardado", typeof(RectTransform), typeof(Text));
                ngo.transform.SetParent(pausePanel.transform, false);
                notaDeGuardado = ngo.GetComponent<Text>();
                notaDeGuardado.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); notaDeGuardado.fontSize = 13; notaDeGuardado.fontStyle = FontStyle.Bold;
                notaDeGuardado.alignment = TextAnchor.MiddleCenter; notaDeGuardado.raycastTarget = false;
                var nrt = notaDeGuardado.rectTransform; nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f); nrt.anchoredPosition = new Vector2(0f, -240f); nrt.sizeDelta = new Vector2(760f, 30f);
            }
            RefrescarBotonesDeGuardado();
        }

        static void Mover(GameObject panel, string nombre, float y)
        {
            var rt = panel.transform.Find(nombre) as RectTransform;
            if (rt != null) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        Button ClonarBoton(RectTransform modelo, string nombre, string texto, Color color, UnityEngine.Events.UnityAction accion)
        {
            var go = Instantiate(modelo.gameObject, modelo.parent);
            go.name = nombre;
            go.GetComponent<Image>().color = color;
            var t = go.GetComponentInChildren<Text>(true);
            if (t != null) { t.text = texto; t.resizeTextForBestFit = true; t.resizeTextMinSize = 10; t.resizeTextMaxSize = t.fontSize; }
            var b = go.GetComponent<Button>();
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(accion);
            SP.UI.ButtonSfx.Attach(b);
            return b;
        }

        // Guardar: habilitado salvo cinematica / en el aire / caido / subiendo al heli. Cargar: solo si hay partida valida.
        public void RefrescarBotonesDeGuardado()
        {
            if (botonGuardar == null) return;
            string bloqueo = PartidaGuardada.MotivoDeBloqueo();
            botonGuardar.interactable = bloqueo == null;
            bool hay = PartidaGuardada.HayPartida();
            botonCargar.interactable = hay;
            botonCargar.gameObject.SetActive(hay);
            if (bloqueo != null) { notaDeGuardado.text = bloqueo; notaDeGuardado.color = new Color(1f, 0.55f, 0.4f); }
            else { notaDeGuardado.text = PartidaGuardada.TextoDeCheckpoint; notaDeGuardado.color = new Color(0.7f, 0.78f, 0.9f); }
        }

        public void OnGuardarClicked()
        {
            string r = PartidaGuardada.GuardarManual();
            GameLog.Line("Pausa: guardar partida -> " + r);
            RefrescarBotonesDeGuardado();
            if (notaDeGuardado != null && r == "PARTIDA GUARDADA") { notaDeGuardado.text = "PARTIDA GUARDADA · " + PartidaGuardada.UltimaInfo().Texto; notaDeGuardado.color = new Color(0.5f, 1f, 0.6f); }
        }

        bool cargaPedida;
        public void OnCargarClicked()
        {
            if (cargaPedida) return;
            if (!PartidaGuardada.HayPartida()) return;
            cargaPedida = true;
            GameLog.Line("Pausa: cargar ultimo guardado");
            IsPaused = false;
            PartidaGuardada.Continuar();
        }

        public void OnContinueClicked()
        {
            if (!IsPaused) return;
            IsPaused = false;
            // Se devuelve la escala que habia antes de pausar. El 0 no se
            // restaura nunca: si se llego a pausar con el tiempo ya
            // congelado por otra cosa, despausar tiene que devolver el
            // control igual y no dejar el juego clavado.
            AudioListener.pause = false;
            Time.timeScale = escalaPrevia > 0.0001f ? escalaPrevia : 1f;
            escalaPrevia = 1f;
            AplicarHudPendiente();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (confirmExitPanel != null) confirmExitPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            GameLog.Line("Se selecciono continuar");
            GameLog.Line("Se saco la pantalla de pausa");
        }

        public void OnSettingsClicked()
        {
            // Ya estaba abierto: un doble click no debería volver a
            // loguear "se entró a configuraciones" como si fuera la
            // primera vez.
            if (settingsPanel == null || settingsPanel.activeSelf) return;
            settingsPanel.SetActive(true);
            // El menu de pausa se esconde mientras se ve Configuraciones: sus botones asomaban entre los dos paneles.
            if (IsPaused && pausePanel != null) pausePanel.SetActive(false);
            SP.UI.PanelAjustesExtra.Preparar(settingsPanel);
            ConectarSoltadoDelHud();
            GameLog.Line("Se entro a configuraciones");
        }

        public void OnSettingsBackClicked()
        {
            if (settingsPanel == null || !settingsPanel.activeSelf) return;
            AplicarHudPendiente();
            settingsPanel.SetActive(false);
            if (IsPaused && pausePanel != null) pausePanel.SetActive(true);
            GameLog.Line("Se salio de configuraciones");
        }

        // Pantalla de CONTROLES (bug #100): tabla con scroll (SP.UI.TablaDeControles) en vez de un bloque de texto que se pisaba.
        // El panel se agranda a 820x500 dentro del canvas de 540, se oculta REMAPEAR (a pedido) y VOLVER queda centrado.
        // Hay que llamarlo con el panel ACTIVO: el layout de la tabla se calcula en el momento.
        void RefreshControlsList()
        {
            if (controlsPanel == null) return;
            var panel = (RectTransform)controlsPanel.transform;
            panel.sizeDelta = new Vector2(820f, 500f);
            if (controlsListTxt != null && controlsListTxt.gameObject.activeSelf) controlsListTxt.gameObject.SetActive(false);
            var rebind = panel.Find("RebindButton");
            if (rebind != null && rebind.gameObject.activeSelf) rebind.gameObject.SetActive(false);
            var volver = panel.Find("BackButton") as RectTransform;
            if (volver != null) volver.anchoredPosition = new Vector2(0f, -216f);
            SP.UI.TablaDeControles.Asegurar(panel, 60f, 76f, 20f);
        }

        // Abre/cierra el panel de controles SIN pausar el juego -- para
        // consultar los atajos en pleno movimiento (tecla dedicada, no la
        // pausa) sin perder el hilo de lo que esta pasando en pantalla.
        // Solo funciona si no hay ya otra pantalla de por medio (pausa,
        // fin de partida), para no abrir controles encima de esas.
        public bool IsControlsOverlayOpen => controlsPanel != null && controlsPanel.activeSelf && !IsPaused;

        public void ToggleControlsOverlay()
        {
            if (controlsPanel == null || IsPaused) return;
            if (outcome != null && outcome.IsShowing) return;
            if (input != null && input.IsHandlingDeath) return;
            bool nextState = !controlsPanel.activeSelf;
            controlsPanel.SetActive(nextState);
            if (nextState) { transform.SetAsLastSibling(); RefreshControlsList(); }
        }

        public void OnControlsClicked()
        {
            if (controlsPanel == null || controlsPanel.activeSelf) return;
            controlsPanel.SetActive(true);
            transform.SetAsLastSibling();
            RefreshControlsList();
            GameLog.Line("Se entro a controles");
        }

        public void OnControlsBackClicked()
        {
            if (controlsPanel == null || !controlsPanel.activeSelf) return;
            controlsPanel.SetActive(false);
            GameLog.Line("Se salio de controles");
        }

        // Panel de remapeo (item 208). Es una capa mas adentro que
        // controles, asi que ESC lo cierra antes de cerrar controles.
        public void OnRebindClicked()
        {
            if (rebindPanel == null || rebindPanel.activeSelf) return;
            rebindPanel.SetActive(true);
            var view = rebindPanel.GetComponent<SP.UI.KeyRebindView>();
            if (view != null) view.RefreshAll();
            GameLog.Line("Se entro a remapear teclas");
        }

        public void OnRebindBackClicked()
        {
            if (rebindPanel == null || !rebindPanel.activeSelf) return;
            rebindPanel.SetActive(false);
            RefreshControlsList();
            GameLog.Line("Se salio de remapear teclas");
        }

        public void OnRebindResetClicked()
        {
            SP.Player.KeyBindings.ResetToDefaults();
            var view = rebindPanel != null ? rebindPanel.GetComponent<SP.UI.KeyRebindView>() : null;
            if (view != null) view.RefreshAll();
            RefreshControlsList();
            GameLog.Line("Se restauraron los controles de fabrica");
        }

        // Abandonar la partida es irreversible (se pierde el progreso), y
        // hacerlo por un click accidental en un menu es de las peores
        // frustraciones posibles -- por eso pasa primero por confirmacion
        // en vez de cargar la escena directo.
        public void OnMenuClicked()
        {
            if (confirmExitPanel == null || confirmExitPanel.activeSelf) return;
            confirmExitPanel.SetActive(true);
        }

        public void OnConfirmExitNo()
        {
            if (confirmExitPanel == null || !confirmExitPanel.activeSelf) return;
            confirmExitPanel.SetActive(false);
        }

        bool exitConfirmed;
        public void OnConfirmExitYes()
        {
            if (exitConfirmed) return;
            exitConfirmed = true;
            GameLog.Line("Se selecciono volver al menu desde pausa");
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneLoader.Cargar("SC_MainMenu");
        }
    }
}
