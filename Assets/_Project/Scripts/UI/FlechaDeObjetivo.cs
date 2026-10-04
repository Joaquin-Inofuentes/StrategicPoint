using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Bug #048: "quiero una flechita igual que las de los aliados pero para el helicoptero, que me diga donde esta y me
    // vaya diciendo su distancia en m, asi veo como se va acercando".
    //
    // Misma lectura que OffscreenAllyMarkerView (triangulo en el borde, espejado si el objetivo queda detras de la camara),
    // pero para UN objetivo y siempre visible: fuera de pantalla va en el borde apuntando hacia el; dentro de pantalla
    // queda encima del objetivo apuntando hacia abajo, con la distancia, para poder seguirlo aunque sea un punto en el cielo.
    public class FlechaDeObjetivo : MonoBehaviour
    {
        const float MargenBorde = 48f;

        public Transform Objetivo;
        public string Nombre = "HELICOPTERO";
        public Color ColorFlecha = new Color(0.35f, 1f, 0.5f, 0.95f);

        public bool Visible { get; private set; }
        public bool EnBorde { get; private set; }
        public string TextoActual => etiqueta != null ? etiqueta.text : "";

        RectTransform lienzo;
        Image flecha;
        Text etiqueta;

        public static FlechaDeObjetivo Crear(Transform objetivo, string nombre, Color color)
        {
            var go = new GameObject("FlechaDeObjetivo_" + nombre, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var escalador = go.GetComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1280f, 720f);
            escalador.matchWidthOrHeight = 0.5f;
            var f = go.AddComponent<FlechaDeObjetivo>();
            f.Objetivo = objetivo; f.Nombre = nombre; f.ColorFlecha = color;
            f.lienzo = (RectTransform)go.transform;

            f.flecha = new GameObject("Flecha", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            f.flecha.transform.SetParent(go.transform, false);
            f.flecha.raycastTarget = false;
            f.flecha.rectTransform.sizeDelta = new Vector2(30f, 26f);
            f.flecha.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            f.etiqueta = new GameObject("Distancia", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            f.etiqueta.transform.SetParent(go.transform, false);
            f.etiqueta.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            f.etiqueta.fontSize = 14; f.etiqueta.fontStyle = FontStyle.Bold;
            f.etiqueta.alignment = TextAnchor.MiddleCenter;
            f.etiqueta.horizontalOverflow = HorizontalWrapMode.Overflow;
            f.etiqueta.raycastTarget = false;
            f.etiqueta.rectTransform.sizeDelta = new Vector2(160f, 20f);
            f.etiqueta.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            f.Ocultar();
            return f;
        }

        public void Quitar() { if (this != null) Destroy(gameObject); }

        void Ocultar()
        {
            Visible = false;
            if (flecha != null) flecha.gameObject.SetActive(false);
            if (etiqueta != null) etiqueta.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (Objetivo == null || !Objetivo.gameObject.activeInHierarchy || cam == null) { Ocultar(); return; }

            float w = lienzo.rect.width, h = lienzo.rect.height;
            float halfW = Mathf.Max(0f, w * 0.5f - MargenBorde);
            // Arriba estan el panel del objetivo (izquierda) y el temporizador (centro): la flecha frena antes de pisarlos.
            float yMax = Mathf.Max(0f, h * 0.5f - 165f);
            float yMin = -Mathf.Max(0f, h * 0.5f - OffscreenAllyMarkerView.MargenInferior);

            // La distancia se mide desde el soldado que maneja el jugador (no desde la camara, que va detras).
            var drv = SP.Player.PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            var desde = yo != null ? yo.transform.position : cam.transform.position;
            float metros = Vector3.Distance(desde, Objetivo.position);

            var punto = Objetivo.position + Vector3.up * 2.5f;
            var vp = cam.WorldToViewportPoint(punto);
            bool enPantalla = vp.z > 0f && vp.x >= 0.03f && vp.x <= 0.97f && vp.y >= 0.05f && vp.y <= 0.92f;

            SpriteTriangulo.Aplicar(flecha);
            flecha.color = ColorFlecha;
            etiqueta.color = Color.white;
            flecha.gameObject.SetActive(true);
            etiqueta.gameObject.SetActive(true);
            Visible = true;
            EnBorde = !enPantalla;
            etiqueta.text = $"{Nombre} · {metros:0} m";

            if (enPantalla)
            {
                // Encima del objetivo, apuntando hacia el.
                var pos = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
                flecha.rectTransform.anchoredPosition = pos + new Vector2(0f, 22f);
                flecha.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                etiqueta.rectTransform.anchoredPosition = pos + new Vector2(0f, 46f);
                return;
            }

            Vector2 dir = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
            if (vp.z < 0f) dir = -dir;   // detras de la camara: espejar
            if (dir.sqrMagnitude < 0.000001f) dir = Vector2.up;
            dir.Normalize();
            float sx = dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float sy = dir.y > 0f ? yMax / dir.y : dir.y < 0f ? yMin / dir.y : float.MaxValue;
            var borde = dir * Mathf.Min(sx, sy);
            flecha.rectTransform.anchoredPosition = borde;
            flecha.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            var pe = borde - dir * 34f;
            // La etiqueta no se sale de la pantalla (en los costados quedaba cortada por la mitad).
            pe.x = Mathf.Clamp(pe.x, -w * 0.5f + 85f, w * 0.5f - 85f);
            etiqueta.rectTransform.anchoredPosition = pe;
        }
    }
}
