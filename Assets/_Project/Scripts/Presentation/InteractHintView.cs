using UnityEngine;
using UnityEngine.UI;

namespace SP.Presentation
{
    // Pedido explicito: "cuando me acerco a un interactuable quiero abajo
    // un texto que diga: Apreté [E] para entrar". El rombo con engranaje
    // (InteractGearMarker) reemplazo a los carteles de texto en su momento,
    // pero sin ninguna palabra un jugador nuevo no tiene forma de saber QUE
    // hace la tecla resaltada -- este texto chico abajo de pantalla vuelve
    // a decirlo, sin tapar el centro de la vista (el rombo sigue siendo el
    // que resalta EN el objetivo).
    //
    // Mismo patron que AccionesEnCursoView: instancia unica autoconstruida,
    // DontDestroyOnLoad, Canvas propio en ScreenSpaceOverlay.
    public class InteractHintView : MonoBehaviour
    {
        static InteractHintView instancia;

        Canvas canvas;
        Text texto;
        Font fuente;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (!Application.isPlaying) return;
            Asegurar();
        }

        public static InteractHintView Asegurar()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("InteractHintView");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<InteractHintView>();
            return instancia;
        }

        void Awake()
        {
            if (instancia != null && instancia != this) { Destroy(gameObject); return; }
            instancia = this;
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Construir();
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        void Construir()
        {
            var go = new GameObject("InteractHintCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;   // debajo del panel de pausa (900) y de los carteles de mision, encima del HUD de fondo
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1280f, 720f);
            cs.matchWidthOrHeight = 0.5f;

            var textoGO = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Shadow));
            textoGO.transform.SetParent(go.transform, false);
            texto = textoGO.GetComponent<Text>();
            texto.font = fuente;
            texto.fontSize = 24;
            texto.fontStyle = FontStyle.Bold;
            texto.alignment = TextAnchor.LowerCenter;
            texto.color = new Color(1f, 0.92f, 0.55f);
            texto.raycastTarget = false;
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = texto.rectTransform;
            // Centrado abajo, un poco arriba del borde -- lejos de la mirilla
            // (centro) y de los paneles de municion/escuadra (esquinas).
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 130f);
            rt.sizeDelta = new Vector2(700f, 40f);
            var sh = textoGO.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);

            textoGO.SetActive(false);
        }

        public static void Mostrar(string mensaje)
        {
            var v = Asegurar();
            if (string.IsNullOrEmpty(mensaje)) { Ocultar(); return; }
            if (!v.texto.gameObject.activeSelf) v.texto.gameObject.SetActive(true);
            if (v.texto.text != mensaje) v.texto.text = mensaje;
        }

        public static void Ocultar()
        {
            if (instancia != null && instancia.texto != null && instancia.texto.gameObject.activeSelf)
                instancia.texto.gameObject.SetActive(false);
        }
    }
}
