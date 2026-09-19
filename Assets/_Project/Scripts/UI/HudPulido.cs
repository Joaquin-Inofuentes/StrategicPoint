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
        float proximaBusqueda;
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

        void LateUpdate()
        {
            if (Time.unscaledTime >= proximaBusqueda && (mira == null || cartel == null || radial == null))
            {
                proximaBusqueda = Time.unscaledTime + 1f;
                if (radial == null) radial = FindAnyObjectByType<MenuDeOrdenes>(FindObjectsInactive.Include);
                var raiz = GameObject.Find("Canvas");
                if (raiz != null)
                {
                    if (mira == null) { var t = raiz.transform.Find("Crosshair"); if (t != null) mira = t.GetComponent<Image>(); }
                    if (cartel == null) { var t = raiz.transform.Find("PromptText"); if (t != null) cartel = t.GetComponent<Text>(); }
                }
            }
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame) AjustesDeJuego.PonerHudMinimo(!AjustesDeJuego.HudMinimo);
            if (raizHud == null) { var c = GameObject.Find("Canvas"); if (c != null) raizHud = c.transform; }
            if (raizHud != null && ultimoHudMinimo != AjustesDeJuego.HudMinimo) { ultimoHudMinimo = AjustesDeJuego.HudMinimo; PanelAjustesExtra.AplicarHudMinimo(raizHud); }
            bool radialAbierto = radial != null && radial.Abierto;
            if (mira != null) mira.enabled = !radialAbierto;
            if (cartel != null && (radialAbierto || Time.timeScale == 0f) && cartel.gameObject.activeSelf) cartel.gameObject.SetActive(false);
        }
    }
}
