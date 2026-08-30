using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SP.Core;

namespace SP.Presentation
{
    // Pantallas de victoria y derrota: UI distinta para cada una (colores,
    // texto) pero los mismos dos botones -- Reintentar (recarga esta
    // misma escena) y Salir (vuelve al menú principal).
    public class GameOutcomeController : MonoBehaviour
    {
        GameObject victoryPanel;
        GameObject defeatPanel;
        bool shown;

        public void Bind(GameObject victory, GameObject defeat)
        {
            victoryPanel = victory;
            defeatPanel = defeat;
            victoryPanel.SetActive(false);
            defeatPanel.SetActive(false);
        }

        bool buttonsWired;

        void OnEnable()
        {
            if (victoryPanel == null)
            {
                var t = transform.Find("VictoryPanel");
                if (t != null) victoryPanel = t.gameObject;
            }
            if (defeatPanel == null)
            {
                var t = transform.Find("DefeatPanel");
                if (t != null) defeatPanel = t.gameObject;
            }

            // Mismo motivo que en MainMenuController/PauseController: los
            // onClick.AddListener() de un script de Editor no sobreviven
            // a Play mode.
            if (buttonsWired) return;
            buttonsWired = true;

            WireButton(victoryPanel, "RetryButton", OnRetryClicked);
            WireButton(victoryPanel, "ExitButton", OnExitClicked);
            WireButton(defeatPanel, "RetryButton", OnRetryClicked);
            WireButton(defeatPanel, "ExitButton", OnExitClicked);
        }

        static void WireButton(GameObject panel, string childName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null) return;
            var t = panel.transform.Find(childName);
            var btn = t != null ? t.GetComponent<Button>() : null;
            if (btn != null) btn.onClick.AddListener(action);
        }

        public void ShowVictory()
        {
            if (victoryPanel == null || shown) return;
            shown = true;
            GameLog.Line("Ganaste");
            Time.timeScale = 0f;
            victoryPanel.SetActive(true);
            GameLog.Line("Pantalla de ganar activa");
        }

        public void ShowDefeat()
        {
            if (defeatPanel == null || shown) return;
            shown = true;
            Time.timeScale = 0f;
            defeatPanel.SetActive(true);
            GameLog.Line("Pantalla de perder activa");
        }

        public void OnRetryClicked()
        {
            GameLog.Line("Se selecciono reintentar");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void OnExitClicked()
        {
            GameLog.Line("Se selecciono salir");
            Time.timeScale = 1f;
            GameLog.Line("Iniciando escena de menu inicial");
            SceneManager.LoadScene("SC_MainMenu");
        }
    }
}
