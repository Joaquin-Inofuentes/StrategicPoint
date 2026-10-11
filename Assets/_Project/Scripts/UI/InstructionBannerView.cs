using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Texto persistente abajo-centro: qué tecla apretar ahora, o que no hay
    // nada que hacer. Lo actualiza quien conduce el flujo de la misión.
    //
    // Pedido explicito: "quiero q no cambie su tamaño y posicion, q se
    // mantengan bien, q no sea editado por codigo". Antes Acomodar() (ver
    // historial de git) reposicionaba y redimensionaba este RectTransform
    // en OnEnable/SetText -- arriba-centro, 450x78, con outline y fondo
    // opaco armados en runtime via FondoOpaco -- porque esos valores nunca
    // se habian llevado a la escena. Esos valores YA estan guardados en
    // SC_Gameplay.unity ahora (Text y BG, mismo layout/outline/color que
    // Acomodar() producia): este componente vuelve a ser lo que el nombre
    // promete, solo contenido de texto, cero manejo de transform/estilo.
    public class InstructionBannerView : MonoBehaviour
    {
        Text label;

        public void Bind(Text text) => label = text;

        // Pedido: "mucha menos UI": de la lista de atajos del contexto solo quedan los 3 primeros (el resto esta en la pausa > controles).
        static string Resumir(string message)
        {
            var partes = message.Split(new[] { "   ·   " }, System.StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length <= 3) return message;
            return string.Join("   ·   ", partes[0], partes[1], partes[2]);
        }

        public void SetText(string message)
        {
            // `label` no se serializa (no es [SerializeField]): tras el
            // domain reload al entrar en Play mode queda null aunque Bind()
            // ya se haya llamado en editor, así que se re-busca sola.
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label != null) { label.text = message; label.enabled = false; }   // se muestra en el texto unico de abajo al centro
            if (string.IsNullOrEmpty(message)) AvisoUnico.QuitarFijo("instruccion"); else AvisoUnico.FijoPermanente("instruccion", Resumir(message));
        }
    }
}
