using UnityEngine;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.UI
{
    // Ronda 13 (T5, decision D7): el UNICO feedback visual del revivir del jugador (y del medico). Un engranaje de 64 px centrado en la
    // mira: fondo gris al 40 % y relleno verde Radial360 (origen arriba, sentido horario) que carga con el progreso; gira a 30 grados
    // por segundo. Una sola instancia (el jugador y el medico la comparten). El sonido y el anillo final los pone FeedbackDeRevivir.
    // El objeto raiz se llama "EngranajeDeRevivir" y el hijo de relleno "Relleno" (lo lee el arnes y Bug976a).
    public sealed class EngranajeDeRevivir : MonoBehaviour
    {
        public const string Nombre = "EngranajeDeRevivir";
        public const float Lado = 64f;
        public const float GradosPorSegundo = 30f;
        static readonly Color ColorFondo = new Color(0.55f, 0.55f, 0.55f, 0.40f);
        static readonly Color ColorRelleno = new Color(0.35f, 0.9f, 0.45f, 0.95f);

        static EngranajeDeRevivir instancia;
        static Sprite sprite;

        Image fondo, relleno;
        RectTransform rt;

        public static bool Visible => instancia != null && instancia.gameObject.activeSelf;
        public static float Fill => instancia != null && instancia.relleno != null ? instancia.relleno.fillAmount : -1f;

        static Sprite Sprite_()
        {
            if (sprite != null) return sprite;
            var tex = DiamondGizmo.TexturaEngranajeConAlfa();
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "EngranajeDeRevivir";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // padre = el lienzo de la mira (AimUiRef.transform.parent). Crea la instancia la primera vez (o si el lienzo se recargo).
        public static void Mostrar(Transform padre, float progreso01)
        {
            if (instancia == null)
            {
                if (padre == null) return;
                instancia = Construir(padre);
            }
            else if (padre != null && instancia.transform.parent != padre) instancia.transform.SetParent(padre, false);
            if (!instancia.gameObject.activeSelf) instancia.gameObject.SetActive(true);
            instancia.relleno.fillAmount = Mathf.Clamp01(progreso01);
        }

        public static void Ocultar()
        {
            if (instancia != null && instancia.gameObject.activeSelf) instancia.gameObject.SetActive(false);
        }

        static EngranajeDeRevivir Construir(Transform padre)
        {
            var raiz = new GameObject(Nombre, typeof(RectTransform));
            raiz.transform.SetParent(padre, false);
            var rt = (RectTransform)raiz.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Lado, Lado);

            var f = Hijo("Fondo", raiz.transform, ColorFondo);
            var r = Hijo("Relleno", raiz.transform, ColorRelleno);
            r.type = Image.Type.Filled;
            r.fillMethod = Image.FillMethod.Radial360;
            r.fillOrigin = (int)Image.Origin360.Top;
            r.fillClockwise = true;
            r.fillAmount = 0f;

            var comp = raiz.AddComponent<EngranajeDeRevivir>();
            comp.fondo = f; comp.relleno = r; comp.rt = rt;
            return comp;
        }

        static Image Hijo(string nombre, Transform padre, Color c)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var img = go.GetComponent<Image>();
            img.sprite = Sprite_();
            img.color = c;
            img.raycastTarget = false;
            var t = (RectTransform)go.transform;
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero;
            return img;
        }

        void Update()
        {
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, -GradosPorSegundo * Time.unscaledTime);
        }

        void OnDestroy() { if (instancia == this) instancia = null; }
    }
}
// 
