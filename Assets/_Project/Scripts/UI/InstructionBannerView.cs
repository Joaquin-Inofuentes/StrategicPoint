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

        public void SetText(string message)
        {
            // `label` no se serializa (no es [SerializeField]): tras el
            // domain reload al entrar en Play mode queda null aunque Bind()
            // ya se haya llamado en editor, así que se re-busca sola.
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label != null) label.text = message;
        }
    }
}
