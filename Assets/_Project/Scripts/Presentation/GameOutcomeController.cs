using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
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
        Text victoryStats;
        Text defeatStats;
        bool shown;

        // Bajas propias y del enemigo durante la partida, mas cuanto duro.
        // Antes la pantalla de fin solo decia gano/perdio, sin dato alguno
        // de como fue, lo que no invita a mejorar ni a intentar de nuevo
        // con otra estrategia.
        int enemyKills;
        int squadLosses;
        float startTime;
        IDisposable deathSub;

        void Awake() => startTime = Time.time;

        void TrackDeaths(EntityDiedEvent evt)
        {
            var soldier = ActorRegistry.FindById(evt.ActorId);
            if (soldier == null) return;
            if (soldier.Team == TeamId.Enemy) enemyKills++;
            else if (soldier.Team == TeamId.Player) squadLosses++;
        }

        // Para que PauseController sepa que no debe abrirse encima --
        // antes [ESC] con la pantalla de Victoria/Derrota puesta abría
        // TAMBIÉN el menú de pausa arriba, dos paneles con botones
        // distintos (Reintentar/Salir Y Continuar/Configuraciones) a la
        // vez, una confusión total.
        public bool IsShowing => shown;

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
            if (victoryStats == null && victoryPanel != null)
            {
                var t = victoryPanel.transform.Find("Stats");
                if (t != null) victoryStats = t.GetComponent<Text>();
            }
            if (defeatStats == null && defeatPanel != null)
            {
                var t = defeatPanel.transform.Find("Stats");
                if (t != null) defeatStats = t.GetComponent<Text>();
            }

            // Igual que en AimUI/DamageVignetteView: la suscripcion hecha
            // en Editor al armar la escena no sobrevive al domain reload
            // de Play mode.
            deathSub?.Dispose();
            deathSub = EventBus.Instance.Subscribe<EntityDiedEvent>(TrackDeaths);

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

        void OnDisable() => deathSub?.Dispose();

        static void WireButton(GameObject panel, string childName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null) return;
            var t = panel.transform.Find(childName);
            var btn = t != null ? t.GetComponent<Button>() : null;
            if (btn != null) btn.onClick.AddListener(action);
        }

        string BuildStatsText()
        {
            float elapsed = Time.time - startTime;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            return $"Bajas enemigas: {enemyKills}   ·   Bajas propias: {squadLosses}   ·   Tiempo: {minutes:00}:{seconds:00}";
        }

        // Foco de teclado en Reintentar al abrir cada pantalla: es la
        // accion mas probable, y sin esto el teclado no servia hasta
        // clickear una vez con el mouse.
        static void FocusRetryButton(GameObject panel)
        {
            if (panel == null || EventSystem.current == null) return;
            var t = panel.transform.Find("RetryButton");
            if (t != null) EventSystem.current.SetSelectedGameObject(t.gameObject);
        }

        // BUG REAL: PlayerInputDriver libera el cursor cuando MUERE el
        // poseido (pasa a vista RTS antes de llamar ShowDefeat), pero
        // BattleManager llama ShowVictory() directo, sin tocar el cursor
        // para nada. Si ganabas jugando en primera persona (el caso mas
        // comun), el cursor seguia bloqueado e invisible en el centro de
        // la pantalla encima de la propia pantalla de victoria: los
        // botones RESPONDIAN bien a un click real (probado con un evento
        // de mouse inyectado), pero el jugador no tenia forma de mover ni
        // ver el cursor para hacer ese click. Se libera aca, en las dos
        // pantallas, en vez de confiar en que quien las dispara se
        // acuerde de hacerlo -- un solo lugar, valido para cualquier
        // camino que termine llamando a estas dos.
        static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ShowVictory()
        {
            if (victoryPanel == null || shown) return;
            shown = true;
            GameLog.Line("Ganaste");
            Time.timeScale = 0f;
            ReleaseCursor();
            if (victoryStats != null) victoryStats.text = BuildStatsText();
            victoryPanel.SetActive(true);
            FocusRetryButton(victoryPanel);
            GameLog.Line("Pantalla de ganar activa");
        }

        public void ShowDefeat()
        {
            if (defeatPanel == null || shown) return;
            shown = true;
            Time.timeScale = 0f;
            ReleaseCursor();
            if (defeatStats != null) defeatStats.text = BuildStatsText();
            defeatPanel.SetActive(true);
            FocusRetryButton(defeatPanel);
            GameLog.Line("Pantalla de perder activa");
        }

        // Un doble/triple click en Reintentar o Salir (el dedo no siempre
        // levanta el mouse justo a tiempo) disparaba la acción -- y su
        // log -- una vez por click, aunque SceneManager.LoadScene ya
        // había arrancado el cambio de escena con el primero. Con esto
        // solo el primer click de cada uno hace algo.
        bool actionTaken;

        public void OnRetryClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono reintentar");
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void OnExitClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono salir");
            Time.timeScale = 1f;
            GameLog.Line("Iniciando escena de menu inicial");
            SceneManager.LoadScene("SC_MainMenu");
        }
    }
}
