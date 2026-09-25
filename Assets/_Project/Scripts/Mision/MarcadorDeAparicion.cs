using UnityEngine;

namespace SP.Mision
{
    // Marcador puramente visual (Gizmos, sin Renderer/Collider: cero costo e invisible en Game
    // view/build) que queda en el punto exacto donde aparecio un enemigo. Pedido explicito:
    // "quiero q en la escena se vean los puntos de aparicion para los enemigos ... quiero ver
    // esos emptys". A PROPOSITO no reemplaza la dispersion aleatoria de MisionDirector
    // (PosicionesDispersas/PosicionesEnFranja): esta ya fue un pedido explicito anterior
    // ("antes eran coordenadas fijas... se leia artificial/en fila", ver comentarios de
    // LanzarLineasEnemigas) para que cada partida varie -- este marcador solo hace visible DONDE
    // y CUANDO aparecio cada uno, sin fijar nada.
    public class MarcadorDeAparicion : MonoBehaviour
    {
        public string Grupo;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, new Vector3(0.4f, 0.4f, 0.4f));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.3f, Grupo);
#endif
        }
    }
}
