using System.Collections;
using UnityEngine;
using SP.Core;
using SP.UI;

namespace SP.Presentation
{
    // Marca en el log de flujo que la escena de gameplay terminó de
    // cargar (Start corre después de que todo el resto ya se construyó),
    // y muestra el objetivo de la mision al arrancar -- antes la partida
    // empezaba sin decir que hacer, y el jugador deducia el objetivo
    // matando cosas hasta que aparecia la pantalla de victoria.
    public class GameplaySceneBootstrap : MonoBehaviour
    {
        public PhaseBannerView ObjectiveBanner;
        public ModeToastView ModeToast;

        const string PrefUsedTab = "sp_used_tab";

        void Start()
        {
            // Prellenado de los escombros en runtime (no al construir la
            // escena): asi el primer estallido real no paga la creacion
            // de 64 objetos, y nada de esto termina guardado en la escena.
            DebrisPool.Prewarm();

            // TODAS las barras del juego estaban rotas: una Image con
            // type = Filled solo respeta fillAmount si tiene sprite, y no
            // habia una sola que lo tuviera. Ver SP.UI.SpriteBlanco.
            // Un barrido al arrancar, no por frame.
            int reparadas = 0;
            foreach (var raiz in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                reparadas += SP.UI.SpriteBlanco.RepararTodo(raiz);
            if (reparadas > 0) GameLog.Line($"Se repararon {reparadas} barras de la interfaz (Filled sin sprite)");

            // Los soldados estaban a cinco alturas distintas respecto del
            // piso -- dos enemigos flotando 1,60 m, otros dos a 0,80, y la
            // escuadra hundida 20 cm. Ver SP.Core.ApoyoEnElPiso: se apoya
            // cada uno con su propio collider, una vez, al arrancar.
            int apoyados = SP.Core.ApoyoEnElPiso.ApoyarATodos();
            if (apoyados > 0) GameLog.Line($"Se apoyaron {apoyados} soldados que estaban flotando o hundidos");

            GameLog.Line("Inicio partida");
            GameLog.Line("Cargo la escena");
            if (ObjectiveBanner != null)
                ObjectiveBanner.Show("Elimina a todos los enemigos\nmanteniendo viva a tu escuadra", 3f);

            // El cambio de vista FPS/RTS es la mecanica central del juego
            // y nada la explicaba: se podia jugar la partida entera sin
            // descubrirla. Se avisa una vez, despues del cartel de
            // objetivo, y nunca mas una vez que el jugador la usa (el
            // propio TAB marca el PlayerPref, ver PlayerInputDriver).
            if (ModeToast != null && PlayerPrefs.GetInt(PrefUsedTab, 0) == 0)
                StartCoroutine(ShowTabHintDelayed());

            // 52: guiar la PRIMERA accion. El cartel de objetivo dice QUE
            // hay que lograr, pero no que hacer en el primer segundo: un
            // jugador nuevo se quedaba parado sin saber por donde empezar.
            // Va por la cola de alertas (216) para no pisarse con el aviso
            // de TAB, que se dispara en la misma ventana de tiempo.
            if (PlayerPrefs.GetInt(PrefFirstActionShown, 0) == 0)
            {
                SP.UI.AlertQueue.Push("Avanza con [WASD] y dispara con clic izquierdo",
                                      SP.UI.AlertPriority.Baja, 3.5f);
                PlayerPrefs.SetInt(PrefFirstActionShown, 1);
                PlayerPrefs.Save();
            }
        }

        // Una sola vez en la vida del jugador, igual que el aviso de TAB:
        // repetirlo en cada partida seria ruido para quien ya sabe jugar.
        const string PrefFirstActionShown = "sp_first_action_shown";

        IEnumerator ShowTabHintDelayed()
        {
            yield return new WaitForSeconds(3.5f);
            if (PlayerPrefs.GetInt(PrefUsedTab, 0) == 0)
                ModeToast.Show("[TAB] cambia entre vista en primera persona y vista tactica", 3f);
        }
    }
}
