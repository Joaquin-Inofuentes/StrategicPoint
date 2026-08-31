using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Las bajas fuera de encuadre solo llegaban por el feed de texto, que
    // se pierde entre el resto de la informacion. Una flecha breve en el
    // borde, en la direccion real de la baja, se lee sin leer.
    //
    // Deliberadamente NO se muestra nada cuando la baja fue en pantalla:
    // ahi ya se vio, y un aviso extra seria ruido sobre algo que el
    // jugador acaba de presenciar.
    public class OffscreenKillMarkerView : MonoBehaviour
    {
        Image arrow;
        float hideAt;
        const float VisibleSeconds = 1.1f;
        const float EdgeMargin = 60f;

        public bool IsShowing => arrow != null && arrow.gameObject.activeSelf;
        public Vector2 ArrowPosition => arrow != null ? arrow.rectTransform.anchoredPosition : Vector2.zero;

        public void Bind(Image image)
        {
            arrow = image;
            if (arrow != null) arrow.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (arrow == null)
            {
                var t = transform.Find("Arrow");
                if (t != null) arrow = t.GetComponent<Image>();
            }
        }

        public void Report(Vector3 worldPosition)
        {
            if (arrow == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            var vp = cam.WorldToViewportPoint(worldPosition);
            bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            if (onScreen) return;

            // Detras de la camara: z negativo invierte el viewport, hay
            // que espejar o la flecha apunta al lado contrario.
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 0.000001f) dir = Vector2.up;
            dir.Normalize();

            var parent = arrow.rectTransform.parent as RectTransform;
            float halfW = (parent != null ? parent.rect.width : Screen.width) * 0.5f - EdgeMargin;
            float halfH = (parent != null ? parent.rect.height : Screen.height) * 0.5f - EdgeMargin;

            arrow.rectTransform.anchoredPosition = new Vector2(dir.x * halfW, dir.y * halfH);
            arrow.rectTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            arrow.gameObject.SetActive(true);
            hideAt = Time.time + VisibleSeconds;
        }

        void Update()
        {
            if (arrow == null || !arrow.gameObject.activeSelf) return;
            if (Time.time >= hideAt) arrow.gameObject.SetActive(false);
        }
    }
}
