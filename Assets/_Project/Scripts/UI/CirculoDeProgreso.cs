using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Anillo de progreso 0-1 reutilizable: un circulo de fondo (siempre
    // entero) mas un circulo de relleno encima (Image.type=Filled,
    // fillMethod=Radial360) que se completa segun SetProgreso(). B2 (la
    // recarga), B3 (la vida del enemigo bajo la mira) y A4 (revivir a un
    // caido) lo arman con Construir() en vez de repetir esta maquinaria
    // cada uno por su lado.
    public class CirculoDeProgreso : MonoBehaviour
    {
        [SerializeField] Image relleno;
        [SerializeField] Image fondo;

        public Image Relleno => relleno;
        public Image Fondo => fondo;

        // Sin sprite, una Image Filled no respeta fillAmount: dibuja un
        // quad liso y listo (bug 30, SpriteBlanco.cs). Pero el 1x1 blanco
        // de SpriteBlanco sirve para barras RECTAS -- aplicado a un
        // Radial360 el resultado es un cuadrado que se completa en cuña,
        // no un circulo. Este disco propio es lo que hace que "circulo
        // radial" se vea como un circulo de verdad.
        static Sprite discoCache;

        // Pedido explicito: la vida del enemigo (y la recarga) tiene que ser
        // SOLO EL CONTORNO: un anillo hueco que se va completando, no un disco
        // relleno tapando la mira. El sprite es un anillo (alfa 0 en el centro)
        // y la Image Radial360 lo usa de mascara: el relleno solo pinta donde
        // el anillo tiene alfa. Textura de 128 px con borde suavizado a ambos
        // lados para que se lea nitido aunque se escale.
        const float GrosorDelAnillo = 0.17f;   // fraccion del radio

        static Sprite Disco()
        {
            if (discoCache != null) return discoCache;
            const int lado = 128;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            float radio = lado * 0.5f;
            float radioInterno = radio * (1f - GrosorDelAnillo);
            var centro = new Vector2(radio, radio);
            var pixeles = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            {
                for (int x = 0; x < lado; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centro);
                    float afuera = Mathf.Clamp01(radio - d);
                    float adentro = Mathf.Clamp01(d - radioInterno);
                    float alfa = Mathf.Min(afuera, adentro);
                    pixeles[y * lado + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
                }
            }
            tex.SetPixels32(pixeles);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;

            discoCache = Sprite.Create(tex, new Rect(0f, 0f, lado, lado), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            discoCache.name = "AnilloDeProgreso";
            discoCache.hideFlags = HideFlags.HideAndDontSave;
            return discoCache;
        }

        static void EstirarCompleto(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static CirculoDeProgreso Construir(Transform padre, float diametro, Color colorFondo, Color colorRelleno)
        {
            var raizGO = new GameObject("CirculoDeProgreso", typeof(RectTransform));
            raizGO.transform.SetParent(padre, false);
            var raizRt = (RectTransform)raizGO.transform;
            raizRt.sizeDelta = new Vector2(diametro, diametro);

            var fondoGO = new GameObject("Fondo", typeof(Image));
            fondoGO.transform.SetParent(raizGO.transform, false);
            var fondoImg = fondoGO.GetComponent<Image>();
            fondoImg.sprite = Disco();
            fondoImg.color = colorFondo;
            EstirarCompleto((RectTransform)fondoGO.transform);

            var rellenoGO = new GameObject("Relleno", typeof(Image));
            rellenoGO.transform.SetParent(raizGO.transform, false);
            var rellenoImg = rellenoGO.GetComponent<Image>();
            rellenoImg.sprite = Disco();
            rellenoImg.color = colorRelleno;
            rellenoImg.type = Image.Type.Filled;
            rellenoImg.fillMethod = Image.FillMethod.Radial360;
            rellenoImg.fillOrigin = (int)Image.Origin360.Top;
            rellenoImg.fillClockwise = true;
            rellenoImg.fillAmount = 0f;
            EstirarCompleto((RectTransform)rellenoGO.transform);

            var comp = raizGO.AddComponent<CirculoDeProgreso>();
            comp.fondo = fondoImg;
            comp.relleno = rellenoImg;
            return comp;
        }

        public void SetProgreso(float valor01)
        {
            if (relleno != null) relleno.fillAmount = Mathf.Clamp01(valor01);
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
