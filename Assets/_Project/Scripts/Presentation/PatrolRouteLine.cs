using UnityEngine;

namespace SP.Presentation
{
    // Marca el circuito de patrulla de un enemigo con una esfera por punto,
    // en vez de un LineRenderer. Pedido explicito: la línea competía
    // visualmente con el resto del HUD/FX y no dejaba distinguir un punto
    // de giro de otro. Las esferas van en la capa "Waypoints", que
    // CameraRig excluye de la cámara principal (ver
    // CameraRig.HideWaypointsFromMainCamera) -- son referencia de
    // depuración/diseño de rutas, no algo que el jugador deba ver en
    // pantalla durante la partida.
    public class PatrolRouteLine : MonoBehaviour
    {
        public const string LayerName = "Waypoints";
        const float SphereRadius = 0.35f;

        public static PatrolRouteLine Spawn(Vector3[] points, Color color, float height = 0.05f)
        {
            var root = new GameObject("PatrolRoute");
            var marker = root.AddComponent<PatrolRouteLine>();

            int layer = LayerMask.NameToLayer(LayerName);
            var mat = SafeMaterial.Create(Color.yellow);

            for (int i = 0; i < points.Length; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Waypoint_" + i;
                sphere.transform.SetParent(root.transform, true);
                sphere.transform.position = points[i] + Vector3.up * height;
                sphere.transform.localScale = Vector3.one * SphereRadius * 2f;

                // Es un marcador visual, no un obstáculo: sin esto cada
                // esfera bloquearía el navmesh/raycasts de apuntado en el
                // punto exacto donde un enemigo patrulla.
                var col = sphere.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                var rend = sphere.GetComponent<Renderer>();
                if (rend != null) { rend.sharedMaterial = mat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

                if (layer >= 0) sphere.layer = layer;
            }

            return marker;
        }
    }
}
