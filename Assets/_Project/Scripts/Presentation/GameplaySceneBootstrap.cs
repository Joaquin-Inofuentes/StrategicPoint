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
        }

        IEnumerator ShowTabHintDelayed()
        {
            yield return new WaitForSeconds(3.5f);
            if (PlayerPrefs.GetInt(PrefUsedTab, 0) == 0)
                ModeToast.Show("[TAB] cambia entre vista en primera persona y vista tactica", 3f);
        }
    }
}
