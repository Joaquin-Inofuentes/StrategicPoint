using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Core;

namespace SP.Presentation
{
    // Pausa con [ESC]: congela el tiempo (Time.timeScale=0) y muestra el
    // panel de pausa, con un sub-panel de "Configuraciones" (sensibilidad
    // de mouse y volumen) navegable desde ahí mismo.
    public class PauseController : MonoBehaviour
    {
        GameObject pausePanel;
        GameObject settingsPanel;

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

            // Igual que en MainMenuController: los onClick.AddListener()
            // hechos al armar la escena en el Editor no sobreviven a Play
            // mode, hay que conectarlos acá en tiempo real. Una sola vez:
            // OnEnable puede volver a correr (p.ej. tras des/activar el
            // panel) y no hay que duplicar el listener.
            if (buttonsWired) return;
            buttonsWired = true;

            WireButton(pausePanel, "ContinueButton", OnContinueClicked);
            WireButton(pausePanel, "SettingsButton", OnSettingsClicked);
            WireButton(settingsPanel, "BackButton", OnSettingsBackClicked);

            if (settingsPanel != null)
            {
                var volumeSlider = settingsPanel.transform.Find("Volumen_Slider")?.GetComponent<Slider>();
                if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(v => AudioListener.volume = v);
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

            if (IsPaused) OnContinueClicked();
            else ShowPause();
        }

        public void ShowPause()
        {
            if (pausePanel == null || IsPaused) return;
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
            if (pausePanel != null) pausePanel.SetActive(false);
            GameLog.Line("Se selecciono continuar");
            GameLog.Line("Se saco la pantalla de pausa");
        }

        public void OnSettingsClicked()
        {
            if (settingsPanel == null) return;
            settingsPanel.SetActive(true);
            GameLog.Line("Se entro a configuraciones");
        }

        public void OnSettingsBackClicked()
        {
            if (settingsPanel == null) return;
            settingsPanel.SetActive(false);
            GameLog.Line("Se salio de configuraciones");
        }
    }
}
