using UnityEngine;
using UnityEngine.UI;
using SP.Actors;

namespace SP.UI
{
    // Vida del soldado que estás manejando ahora mismo. Antes no existía:
    // la única forma de saber cuánta vida te quedaba era buscarte a vos
    // mismo en la lista lateral de la escuadra (que muestra a los tres
    // igual, sin distinguir cuál sos) o deducirlo del viñeteo rojo al
    // recibir un golpe. En un FPS eso es información de primera línea.
    public class PlayerHealthView : MonoBehaviour
    {
        Text label;
        Image fill;

        // Verde arriba de 60%, amarillo entre 25 y 60, rojo abajo de 25:
        // el color solo ya dice "estás bien / cuidado / te morís", sin
        // tener que leer el número.
        static readonly Color HighColor = new Color(0.35f, 0.85f, 0.4f);
        static readonly Color MidColor = new Color(0.95f, 0.8f, 0.25f);
        static readonly Color LowColor = new Color(0.95f, 0.25f, 0.2f);

        public void Bind(Text text, Image fillImage)
        {
            label = text;
            fill = fillImage;
        }

        void OnEnable()
        {
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
            }

            if (label == null) label = transform.Find("Text")?.GetComponent<Text>();
            if (fill == null) fill = transform.Find("BarBG/BarFill")?.GetComponent<Image>();
            
            if (label != null) label.gameObject.SetActive(false);
        }

        // Verde bien saturado y mas claro que HighColor: tiene que leerse
        // como "subiendo sola", no confundirse con el verde de "estas
        // bien" de siempre.
        static readonly Color RegenColor = new Color(0.45f, 1f, 0.55f);

        public void UpdateFrom(Soldier soldier)
        {
            if (soldier == null || soldier.Health == null) return;

            var health = soldier.Health;
            float frac = health.MaxHealth > 0 ? (float)health.Current / health.MaxHealth : 0f;

            // Feedback de "curandose" pedido explicito: el numero solo (que
            // ademas cambia de a poco, un punto entero por vez) no se nota
            // en medio de un tiroteo. La flecha y el "+" en el texto se ven
            // sin tener que hacer cuentas con el numero de antes.
            // (Desactivado en T-10: solo barra)

            if (fill == null) return;
            fill.fillAmount = frac;

            if (health.IsRegenerating)
            {
                // Pulso mas lento y mas suave que el de vida critica: es
                // una buena noticia, no una alarma. Mismo patron de seno
                // que ya usa el pulso critico de mas abajo, para no
                // inventar una segunda forma de animar lo mismo.
                float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f));
                fill.color = new Color(RegenColor.r * pulse, RegenColor.g * pulse, RegenColor.b * pulse);
                return;
            }

            var baseColor = frac > 0.6f ? HighColor : frac > 0.25f ? MidColor : LowColor;

            // Abajo del 25% la barra late. Un rojo fijo se vuelve parte
            // del decorado después de unos segundos; el pulso obliga al
            // ojo a volver ahí, que es justo cuando más importa.
            if (frac <= 0.25f && frac > 0f)
            {
                float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
                baseColor = new Color(baseColor.r * pulse, baseColor.g * pulse, baseColor.b * pulse);
            }

            fill.color = AjustesDeJuego.Adaptar(baseColor);
        }
    }
}
