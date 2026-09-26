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

        void OnEnable()
        {
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
                    hudSlider.onValueChanged.AddListener(v =>
                    {
                        if (HudScaler != null) HudScaler.referenceResolution = BaseReferenceResolution / v;
                        PlayerPrefs.SetFloat(PrefHudScale, v);
                        if (hudValueTxt != null) hudValueTxt.text = v.ToString("0.00");
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
            // El panel tiene que dibujarse ENCIMA de todo el HUD (la mision, el roster, el radial...), que
            // se crean despues y por eso quedaban tapando los menus. Y el audio se pausa de verdad: antes
            // timeScale=0 congelaba el juego pero la musica y los ambientes seguian sonando.
            transform.SetAsLastSibling();
            AudioListener.pause = true;
            GameLog.Line("Se puso en pausa el juego");
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
            SP.UI.PanelAjustesExtra.Preparar(settingsPanel);
            GameLog.Line("Se entro a configuraciones");
        }

        public void OnSettingsBackClicked()
        {
            if (settingsPanel == null || !settingsPanel.activeSelf) return;
            settingsPanel.SetActive(false);
            GameLog.Line("Se salio de configuraciones");
        }

        void RefreshControlsList()
        {
            if (controlsListTxt != null)
            {
                controlsListTxt.text = SP.UI.ControlsTable.FullText();
                controlsListTxt.lineSpacing = 1.12f;   // el texto iba pegado al borde y con el interlineado apretado
                var rt = controlsListTxt.rectTransform;
                if (rt.offsetMin.x < 18f) { rt.offsetMin = new Vector2(20f, rt.offsetMin.y); rt.offsetMax = new Vector2(-20f, rt.offsetMax.y); }
            }
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
