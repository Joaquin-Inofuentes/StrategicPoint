using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SP.UI
{
    // Ajustes de HUD que valen para TODAS las escenas de juego (gameplay, tutorial, pruebas) sin tocar
    // cada constructor de escena: se crea solo al cargar la escena.
    //  - Con el radial abierto la mira se oculta: su texto central ("apunta con el mouse") quedaba pisado.
    //  - Con el juego en pausa el cartel de apuntado se oculta (pisaba el titulo PAUSA).
    public class HudPulido : MonoBehaviour
    {
        static HudPulido instancia;
        Image mira;
        Text cartel;
        MenuDeOrdenes radial;
        bool bootstrapped;
        Transform raizHud;
        bool ultimoHudMinimo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
            Crear();
        }
        static void AlCargarEscena(UnityEngine.SceneManagement.Scene escena, UnityEngine.SceneManagement.LoadSceneMode modo) => Crear();

        static void Crear()
        {
            if (Application.targetFrameRate <= 0) Application.targetFrameRate = 144;   // sin tope el juego rendia al maximo y calentaba de mas
            if (instancia != null) return;
            instancia = new GameObject("HudPulido").AddComponent<HudPulido>();
            AliadosSinEstorbo.Asegurar();
        }

        // Se resuelve UNA vez en el primer LateUpdate (ya corrieron todos los Start de la escena, incluido el que arma el HUD).
        // Antes esto barria la escena entera cada segundo mientras faltara algo, para siempre.
        void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            radial = MenuDeOrdenes.Activo;
            // El canvas del HUD es el padre de la mira del driver (ya no se busca por el nombre "Canvas").
            var driver = SP.Player.PlayerInputDriver.Activo;
            var raiz = driver != null && driver.AimUiRef != null ? driver.AimUiRef.transform.parent : null;
            if (raiz == null)
            {
                // Sin canvas (menu principal, escena de prueba) no hay nada que pulir: no se reintenta un barrido por frame.
                SP.Core.GameLog.Line("[HudPulido] sin Canvas en la escena: se desactiva");
                enabled = false;
                return;
            }
            raizHud = raiz;
            var t = raizHud.Find("Crosshair"); if (t != null) mira = t.GetComponent<Image>();
            t = raizHud.Find("PromptText"); if (t != null) cartel = t.GetComponent<Text>();
        }

        void LateUpdate()
        {
            Bootstrap();
            if (!enabled) return;
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame) AjustesDeJuego.PonerHudMinimo(!AjustesDeJuego.HudMinimo);
            if (raizHud != null && ultimoHudMinimo != AjustesDeJuego.HudMinimo) { ultimoHudMinimo = AjustesDeJuego.HudMinimo; PanelAjustesExtra.AplicarHudMinimo(raizHud); }
            bool radialAbierto = radial != null && radial.Abierto;
            if (mira != null) mira.enabled = !radialAbierto;
            if (cartel != null && (radialAbierto || Time.timeScale == 0f) && cartel.gameObject.activeSelf) cartel.gameObject.SetActive(false);
        }
    }
}
