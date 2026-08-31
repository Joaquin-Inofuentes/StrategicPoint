using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        GameOutcomeController outcome;
        SP.Player.PlayerInputDriver input;

        // Wireados desde HeadlessTestRunner para las opciones de
        // accesibilidad: tamaño de HUD y de mirilla.
        public CanvasScaler HudScaler;
        public AimUI AimUiRef;
        static readonly Vector2 BaseReferenceResolution = new Vector2(1920f, 1080f);

        const string PrefVolume = "sp_volume";
        const string PrefSensitivity = "sp_sensitivity";
        const string PrefTurretSensitivity = "sp_turret_sensitivity";
        const string PrefHudScale = "sp_hud_scale";
        const string PrefCrosshairScale = "sp_crosshair_scale";
        const string PrefInvertY = "sp_invert_y";

        public bool IsPaused { get; private set; }

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
            if (confirmExitPanel == null)
            {
                var t = transform.Find("ConfirmExitPanel");
                if (t != null) confirmExitPanel = t.gameObject;
            }
            if (outcome == null) outcome = FindAnyObjectByType<GameOutcomeController>();
            if (input == null) input = FindAnyObjectByType<SP.Player.PlayerInputDriver>();

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
                AudioListener.volume = savedVolume;
                if (volumeSlider != null)
                {
                    volumeSlider.SetValueWithoutNotify(savedVolume);
                    if (volumeValueTxt != null) volumeValueTxt.text = savedVolume.ToString("0.00");
                    volumeSlider.onValueChanged.AddListener(v =>
                    {
                        AudioListener.volume = v;
                        PlayerPrefs.SetFloat(PrefVolume, v);
                        if (volumeValueTxt != null) volumeValueTxt.text = v.ToString("0.00");
                    });
                }

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

        static void WireButton(GameObject panel, string childName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null) return;
            var t = panel.transform.Find(childName);
            var btn = t != null ? t.GetComponent<Button>() : null;
            if (btn != null) btn.onClick.AddListener(action);
        }

        void Update()
        {
            if (Keyboard.current == null || !Application.isPlaying) return;
            if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;
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
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
            GameLog.Line("Se puso en pausa el juego");
        }

        public void OnContinueClicked()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
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
            GameLog.Line("Se entro a configuraciones");
        }

        public void OnSettingsBackClicked()
        {
            if (settingsPanel == null || !settingsPanel.activeSelf) return;
            settingsPanel.SetActive(false);
            GameLog.Line("Se salio de configuraciones");
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
            controlsPanel.SetActive(!controlsPanel.activeSelf);
        }

        public void OnControlsClicked()
        {
            if (controlsPanel == null || controlsPanel.activeSelf) return;
            controlsPanel.SetActive(true);
            GameLog.Line("Se entro a controles");
        }

        public void OnControlsBackClicked()
        {
            if (controlsPanel == null || !controlsPanel.activeSelf) return;
            controlsPanel.SetActive(false);
            GameLog.Line("Se salio de controles");
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
            SceneManager.LoadScene("SC_MainMenu");
        }
    }
}
