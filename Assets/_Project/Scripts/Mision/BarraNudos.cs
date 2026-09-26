using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.Mision
{
    // Barra de rescate por nudos en el espacio de mundo sobre el civil (T-31)
    public class BarraNudos : MonoBehaviour
    {
        MisionDirector director;
        Canvas canvas;
        Image barraFondo;
        Image barraRelleno;
        Text textoInstruccion;
        Text textoNudos;
        Camera cam;

        public static BarraNudos Crear(MisionDirector dir)
        {
            if (dir == null || dir.Civil == null) return null;
            var go = new GameObject("BarraNudos");
            var bn = go.AddComponent<BarraNudos>();
            bn.director = dir;
            bn.Inicializar();
            return bn;
        }

        void Inicializar()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            gameObject.AddComponent<CanvasScaler>();

            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 60f);
            rt.localScale = Vector3.one * 0.012f;

            // Fondo de la barra
            var fondoGo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondoGo.transform.SetParent(transform, false);
            var rtFondo = fondoGo.GetComponent<RectTransform>();
            rtFondo.sizeDelta = new Vector2(160f, 16f);
            rtFondo.anchoredPosition = new Vector2(0f, -5f);
            barraFondo = fondoGo.GetComponent<Image>();
            barraFondo.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // Relleno de la barra
            var rellenoGo = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
            rellenoGo.transform.SetParent(fondoGo.transform, false);
            var rtRelleno = rellenoGo.GetComponent<RectTransform>();
            rtRelleno.anchorMin = Vector2.zero;
            rtRelleno.anchorMax = new Vector2(0f, 1f);
            rtRelleno.pivot = new Vector2(0f, 0.5f);
            rtRelleno.sizeDelta = Vector2.zero;
            barraRelleno = rellenoGo.GetComponent<Image>();
            barraRelleno.color = new Color(0.2f, 0.8f, 0.3f, 0.95f);

            // Texto instruccion "[ESPACIO] DESATAR"
            var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(transform, false);
            var rtTxt = txtGo.GetComponent<RectTransform>();
            rtTxt.sizeDelta = new Vector2(200f, 24f);
            rtTxt.anchoredPosition = new Vector2(0f, 15f);
            textoInstruccion = txtGo.GetComponent<Text>();
            textoInstruccion.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            textoInstruccion.fontSize = 14;
            textoInstruccion.alignment = TextAnchor.MiddleCenter;
            textoInstruccion.color = Color.white;
            textoInstruccion.text = Loc.Tr("[ESPACIO] DESATAR");

            // Texto contador nudos
            var numGo = new GameObject("Contador", typeof(RectTransform), typeof(Text));
            numGo.transform.SetParent(transform, false);
            var rtNum = numGo.GetComponent<RectTransform>();
            rtNum.sizeDelta = new Vector2(100f, 16f);
            rtNum.anchoredPosition = new Vector2(0f, -5f);
            textoNudos = numGo.GetComponent<Text>();
            textoNudos.font = textoInstruccion.font;
            textoNudos.fontSize = 11;
            textoNudos.alignment = TextAnchor.MiddleCenter;
            textoNudos.color = Color.white;
        }

        void LateUpdate()
        {
            if (director == null || director.Civil == null)
            {
                Destroy(gameObject);
                return;
            }

            if (cam == null) cam = CamaraPrincipal.Actual ?? Camera.main;

            // Posicionar sobre la cabeza del civil
            Vector3 targetPos = director.Civil.transform.position + Vector3.up * 2.1f;
            transform.position = targetPos;

            // Billboard hacia la camara
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }

            // Actualizar progreso
            float progreso = director.ProgresoRescate;
            if (barraRelleno != null)
            {
                barraRelleno.rectTransform.anchorMax = new Vector2(progreso, 1f);
            }

            if (textoNudos != null)
            {
                textoNudos.text = $"{director.NudosDesatados} / {MisionDirector.NudosParaLiberar}";
            }
        }
    }
}
