using UnityEngine;
using UnityEngine.UI;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (Application.targetFrameRate <= 0) Application.targetFrameRate = 144;   // sin tope el juego rendia al maximo y calentaba de mas
            if (instancia != null) return;
            instancia = new GameObject("HudPulido").AddComponent<HudPulido>();
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
            bool radialAbierto = radial != null && radial.Abierto;
            if (mira != null) mira.enabled = !radialAbierto;
            if (cartel != null && (radialAbierto || Time.timeScale == 0f) && cartel.gameObject.activeSelf) cartel.gameObject.SetActive(false);
        }
    }
}
