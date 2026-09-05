using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Texto persistente abajo-centro: qué tecla apretar ahora, o que no hay
    // nada que hacer. Lo actualiza quien conduce el flujo de la misión.
    public class InstructionBannerView : MonoBehaviour
    {
        Text label;

        public void Bind(Text text) => label = text;

        bool acomodado;

        void OnEnable() => Acomodar();

        // Del plan: "El mensaje del comienzo. Fondo opaco para ver q dice
        // y arriba centro". Estaba abajo de todo (anclado a y=0, a 20 px
        // del borde inferior), con un fondo BLANCO al 80% de opacidad
        // sobre el que el texto blanco casi no se leia, y ademas el texto
        // caia 6 px mas arriba que el centro de su propio fondo.
        //
        // Se hace aca, en runtime, porque estas posiciones ya estan
        // guardadas en la escena: cambiar solo el constructor deja el
        // .unity real igual de ilegible.
        void Acomodar()
        {
            if (acomodado) return;
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label == null) return;
            acomodado = true;

            // El fondo viejo, blanco y suelto, sobra: el nuevo va colgado
            // del propio texto y lo sigue.
            var viejoBg = transform.Find("BG");
            if (viejoBg != null) viejoBg.gameObject.SetActive(false);

            FondoOpaco.LlevarArribaAlCentro(label.rectTransform);

            // El cartel lista TODOS los controles del momento y arriba solo
            // entran 450 px de ancho (ver FondoOpaco.AnchoLibreArriba): en
            // una linea el texto se cortaba a la mitad de una frase. Se
            // reparte en varias lineas y la caja crece con el.
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 18;
            var rt = label.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 78f);

            FondoOpaco.Poner(label);
        }

        public void SetText(string message)
        {
            // `label` no se serializa (no es [SerializeField]): tras el
            // domain reload al entrar en Play mode queda null aunque Bind()
            // ya se haya llamado en editor, así que se re-busca sola.
            if (label == null) label = GetComponentInChildren<Text>(true);
            Acomodar();
            if (label != null) label.text = message;
        }
    }
}
