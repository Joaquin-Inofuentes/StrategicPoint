using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Pantallas de victoria y derrota: UI distinta para cada una (colores,
    // texto) pero los mismos dos botones -- Reintentar (recarga esta
    // misma escena) y Salir (vuelve al menú principal).
    [DefaultExecutionOrder(-50)]
    public class GameOutcomeController : MonoBehaviour
    {
        // Servicio unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        // El suite (Edit mode, donde no corren Awake/OnEnable) llama Registrar() a mano.
        public static GameOutcomeController Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void Registrar()
        {
            if (Activo != null && Activo != this) SP.Core.GameLog.Line($"[AVISO] Segundo GameOutcomeController en la escena: {name}");
            Activo = this;
        }
        void QuitarRegistro() { if (Activo == this) Activo = null; }
        GameObject victoryPanel;
        GameObject defeatPanel;
        Text victoryStats;
        Text defeatStats;
        Text defeatReason;
        bool shown;

        // Cuanto duro la partida. Las bajas (propias y del enemigo) se
        // muestran en BuildStatsText via ActorRegistry.CountDead, que lee
        // el estado actual en vez de ir contando EntityDiedEvent por su
        // cuenta -- asi no depende de que este componente ya estuviera
        // suscripto ANTES de que ocurriera cada muerte de la partida.
        float startTime;

        void Awake() { Registrar(); startTime = Time.time; }
        void OnDestroy() => QuitarRegistro();

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
                // Ver SP.UI.Diagramador: el titulo y REINTENTAR estaban los
                // dos en y=0 y el boton tapaba el texto.
                SP.UI.Diagramador.AcomodarResultado(victoryPanel);
                SP.UI.Diagramador.AcomodarResultado(defeatPanel);
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
            if (defeatReason == null && defeatPanel != null)
            {
                var t = defeatPanel.transform.Find("Reason");
                if (t == null)
                {
                    var go = new GameObject("Reason");
                    t = go.transform;
                    t.SetParent(defeatPanel.transform, false);
                    defeatReason = go.AddComponent<Text>();
                    defeatReason.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    defeatReason.fontSize = 20;
                    defeatReason.alignment = TextAnchor.MiddleCenter;
                    defeatReason.color = Color.white;
                    var rt = go.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(600f, 40f);
                }
                else
                {
                    defeatReason = t.GetComponent<Text>();
                }
                if (defeatReason != null) SP.UI.FondoOpaco.Poner(defeatReason);
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
            if (btn == null) return;
            btn.onClick.AddListener(action);
            // Pedido explicito: sonido al pasar el mouse y al hacer click
            // en todos los botones.
            SP.UI.ButtonSfx.Attach(btn);
        }

        string BuildStatsText()
        {
            float elapsed = Time.time - startTime;
            int minutes = Mathf.FloorToInt(elapsed / 60f);
            int seconds = Mathf.FloorToInt(elapsed % 60f);
            int enemyDead = SP.Core.ActorRegistry.CountDead(TeamId.Enemy);
            int squadDead = SP.Core.ActorRegistry.CountDead(TeamId.Player);
            return $"Bajas enemigas: {enemyDead}   ·   Bajas propias: {squadDead}   ·   Tiempo: {minutes:00}:{seconds:00}";
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

        // "motivo" es el mismo texto legible que ya se empujaba como toast de 3 s (AlertQueue) --
        // pedido explicito: "cuando muere el civil me dice que perdi pero no me dice por que". El
        // toast se perdia porque Time.timeScale pasa a 0 casi en el mismo instante; ahora el motivo
        // queda fijo en la propia pantalla de derrota, no solo en un aviso que dura 3 s.
        public void ShowDefeat(string motivo = null)
        {
            if (defeatPanel == null || shown) return;
            shown = true;
            Time.timeScale = 0f;
            ReleaseCursor();
            if (defeatReason != null) defeatReason.text = string.IsNullOrEmpty(motivo) ? "" : SP.Core.Loc.Tr(motivo);
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
            SceneLoader.Cargar(SceneManager.GetActiveScene().name);
        }

        public void OnExitClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono salir");
            Time.timeScale = 1f;
            GameLog.Line("Iniciando escena de menu inicial");
            SceneLoader.Cargar("SC_MainMenu");
        }
    }
}
