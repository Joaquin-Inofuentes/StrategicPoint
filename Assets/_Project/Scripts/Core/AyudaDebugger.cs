using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SP.Core
{
    // Bugs #045 y #048: Ctrl+Borrar es el atajo de Unity (URP) para el "Rendering Debugger" (Display Stats, Frequently
    // Used, Rendering...). Aparecia de golpe, tapaba media pantalla y no habia forma obvia de cerrarlo; y en el dialogo de
    // reporte de bug, Ctrl+Borrar (borrar una palabra) lo abria por encima del texto.
    //
    //  - Mientras el debugger esta abierto, abajo a la derecha se ve el atajo para cerrarlo.
    //  - Mientras el dialogo de bug esta abierto, el atajo del debugger queda desactivado (y si estaba abierto se cierra):
    //    Ctrl+Borrar ahi borra la palabra anterior, como en cualquier editor (eso lo hace ReporteDeBug).
    public class AyudaDebugger : MonoBehaviour
    {
        public static AyudaDebugger Instancia { get; private set; }
        public static bool CartelVisible => Instancia != null && Instancia.cartel != null && Instancia.cartel.activeSelf;

        GameObject cartel;
        bool desactivadoPorMi, habilitadoPrevio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            if (Instancia != null) return;
            var go = new GameObject("AyudaDebugger");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<AyudaDebugger>();
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        void Update()
        {
            var dm = DebugManager.instance;
            if (ReporteDeBug.Abierto)
            {
                if (dm.displayRuntimeUI) dm.displayRuntimeUI = false;
                if (!desactivadoPorMi) { desactivadoPorMi = true; habilitadoPrevio = dm.enableRuntimeUI; }
                dm.enableRuntimeUI = false;
            }
            else if (desactivadoPorMi)
            {
                // Se devuelve el valor que tenia (en un build final puede estar apagado y no hay que prenderlo).
                desactivadoPorMi = false;
                dm.enableRuntimeUI = habilitadoPrevio;
            }

            bool abierto = dm.displayRuntimeUI;
            if (abierto && cartel == null) Construir();
            if (cartel != null && cartel.activeSelf != abierto) cartel.SetActive(abierto);
        }

        void Construir()
        {
            cartel = new GameObject("AyudaDebugger_Cartel", typeof(Canvas), typeof(CanvasScaler));
            cartel.transform.SetParent(transform, false);
            var canvas = cartel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var escalador = cartel.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1280f, 720f);
            escalador.matchWidthOrHeight = 0.5f;

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fondo.transform.SetParent(cartel.transform, false);
            fondo.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
            fondo.raycastTarget = false;
            var borde = fondo.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(1f, 0.78f, 0.25f, 0.95f);
            borde.effectDistance = new Vector2(2f, -2f);
            var rf = fondo.rectTransform;
            rf.anchorMin = rf.anchorMax = rf.pivot = new Vector2(1f, 0f);
            // Arriba del panel de armas (que ocupa la esquina hasta ~150 px).
            rf.anchoredPosition = new Vector2(-12f, 160f);
            rf.sizeDelta = new Vector2(360f, 58f);

            var t = new GameObject("Texto", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(fondo.transform, false);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 15;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.text = "RENDERING DEBUGGER (herramienta de Unity)\nPara cerrarlo: <b><color=#FFD040>Ctrl + Borrar (Backspace)</color></b>";
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 4f); rt.offsetMax = new Vector2(-6f, -4f);
        }
    }
}
