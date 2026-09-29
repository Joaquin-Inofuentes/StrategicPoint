using UnityEngine;

namespace SP.Presentation
{
    // Marcador de referencia para puntos clave de la mision (plaza,
    // helipuerto, refugio del civil): sin Renderer ni Collider, asi que es
    // invisible y no estorba en Play, pero se ve y se puede arrastrar como
    // cualquier GameObject en la ventana de Escena del Editor. Pedido
    // explicito: "un gameobject que no es visible pero sera para saber el
    // destino desde escena de manera simple" -- MisionDirector lee su
    // posicion en vez de tener el punto como un numero suelto en el codigo.
    public class MarcadorDeMision : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var c = new Color(1f, 0.85f, 0.25f, 0.9f);
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, 1.2f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
            UnityEditor.Handles.color = c;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3.3f, name);
        }
#endif
    }
}
