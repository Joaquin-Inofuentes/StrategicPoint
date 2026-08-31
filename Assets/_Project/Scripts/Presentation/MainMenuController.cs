using UnityEngine;
using UnityEngine.SceneManagement;
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
            var canvasRoot = transform.parent;
            if (canvasRoot == null) return;
            var playBtn = canvasRoot.Find("PlayButton")?.GetComponent<Button>();
            var exitBtn = canvasRoot.Find("ExitButton")?.GetComponent<Button>();
            if (playBtn != null) playBtn.onClick.AddListener(OnPlayClicked);
            if (exitBtn != null) exitBtn.onClick.AddListener(OnExitClicked);
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
            actionTaken = true;
            GameLog.Line("Se selecciono iniciar partida");
            SceneManager.LoadScene("SC_Gameplay");
        }

        public void OnExitClicked()
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
