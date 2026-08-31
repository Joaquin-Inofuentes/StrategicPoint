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

            var parent = arrow.rectTransform.parent as RectTransform;
            float w, h;
            if (parent != null)
            {
                w = parent.rect.width;
                h = parent.rect.height;
            }
            else
            {
                // Screen.width/height estan en PIXELES y todo lo demas aca
                // esta en unidades de canvas (ScaleWithScreenSize a
                // 1920x1080). Mezclarlos daba una flecha con el margen y la
                // posicion en la escala equivocada, asi que el fallback se
                // divide por el scaleFactor para pasar a unidades de canvas.
                float scale = arrow.canvas != null ? arrow.canvas.scaleFactor : 1f;
                if (scale <= 0f) scale = 1f;
                w = Screen.width / scale;
                h = Screen.height / scale;
            }

            // Mathf.Max: si Report() corre en el primer frame, antes del
            // layout pass, el rect del padre todavia mide 0 y restarle el
            // margen daba mitades NEGATIVAS, o sea la flecha en el borde
            // OPUESTO al de la baja.
            float halfW = Mathf.Max(0f, w * 0.5f - EdgeMargin);
            float halfH = Mathf.Max(0f, h * 0.5f - EdgeMargin);

            // La direccion se calcula en PIXELES del canvas, no en viewport
            // normalizado: en viewport los dos ejes van 0..1 aunque la
            // pantalla sea 16:9, asi que al escalarlos despues por mitades
            // distintas el angulo de la flecha no coincidia con la
            // direccion real en pantalla (~9 grados de error en diagonal).
            //
            // Detras de la camara: z negativo invierte el viewport, hay
            // que espejar o la flecha apunta al lado contrario.
            Vector2 dir = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
            if (vp.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 0.000001f) dir = Vector2.up;
            dir.Normalize();

            // Se busca el borde del RECTANGULO, no el de la elipse inscrita:
            // escalar cada eje por su propia mitad dejaba la flecha muy
            // adentro cuando la baja caia hacia una esquina.
            float sx = dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float sy = dir.y != 0f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            arrow.rectTransform.anchoredPosition = dir * Mathf.Min(sx, sy);

            // localRotation y no rotation: el canvas es Screen Space -
            // Camera, o sea que tiene rotacion propia en el mundo, y fijar
            // la rotacion de MUNDO ignoraba la del canvas y torcia la flecha.
            arrow.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

            arrow.gameObject.SetActive(true);
            // unscaledTime: la ultima baja dispara camara lenta (timeScale
            // 0.25) y con Time.time esta flecha duraba 4.4 s reales en vez
            // de 1.1. Es UI, no debe estirarse con la camara lenta.
            hideAt = Time.unscaledTime + VisibleSeconds;
        }

        void Update()
        {
            if (arrow == null || !arrow.gameObject.activeSelf) return;
            if (Time.unscaledTime >= hideAt) arrow.gameObject.SetActive(false);
        }
    }
}
