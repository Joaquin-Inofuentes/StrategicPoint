using UnityEngine;

namespace SP.Presentation
{
    // Marca el circuito de patrulla de un enemigo con una esfera por punto,
    // en vez de un LineRenderer. Pedido explicito: la línea competía
    // visualmente con el resto del HUD/FX y no dejaba distinguir un punto
    // de giro de otro. Las esferas van en la capa "Waypoints" -- CameraRig
    // se asegura de que esa capa SI este en el culling mask de la camara
    // principal (ver CameraRig.ShowWaypointsOnMainCamera): pedido
    // explicito, el jugador tiene que poder verlas para confirmar que un
    // enemigo sigue su ronda.
    public class PatrolRouteLine : MonoBehaviour
    {
        public const string LayerName = "Waypoints";
        // Pedido explicito: "el enemigo esta muy lejos y no veo las
        // esferas" -- 0.35 (0.7m de diametro) se perdia de vista a las
        // distancias reales del mapa (peleas a 40-80m).
        const float SphereRadius = 0.6f;

        // Pedido explicito: "quiero que siempre siga las posiciones reales
        // del mundo, no las relativas". Antes AiBrain.patrolRoute guardaba
        // una COPIA congelada de estos puntos (un Vector3[] tomado en el
        // instante de Spawn); mover una esfera en el editor no cambiaba en
        // nada la ronda real. Exponer los Transform de las esferas deja que
        // AiBrain.SetPatrolWaypoints los use directamente: la IA lee
        // marker.position cada vez, asi que sigue la posicion de verdad,
        // la mueva quien la mueva y cuando sea.
        public Transform[] Markers { get; private set; }

        public static PatrolRouteLine Spawn(Vector3[] points, Color color, float height = 0.05f)
        {
            var root = new GameObject("PatrolRoute");
            var marker = root.AddComponent<PatrolRouteLine>();
            marker.Markers = new Transform[points.Length];

            int layer = LayerMask.NameToLayer(LayerName);

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
                if (rend != null) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                if (layer >= 0) sphere.layer = layer;

                marker.Markers[i] = sphere.transform;
            }

            // El material se aplica en Awake, no aca -- ver el comentario
            // de ReaplicarMaterial.
            marker.ReaplicarMaterial();

            return marker;
        }

        // BUG REAL que esto corrige: SafeMaterial.Create devuelve un
        // Material con HideFlags.HideAndDontSave (a propósito -- así no
        // se acumulan basura entre sesiones). Eso significa que NO
        // sobrevive a que la escena se guarde y se vuelva a cargar desde
        // disco: el GameObject persiste (es contenido de escena de
        // verdad, no un efecto momentáneo), pero la referencia al
        // material se pierde y el Renderer queda con sharedMaterial nulo
        // -- exactamente el sintoma que SafeMaterial dice evitar para FX
        // efímeros, pero este marcador de ruta NO es efímero: se crea una
        // vez en el editor y se guarda en el .unity. Awake reaplica el
        // material fresco cada vez que el objeto se carga (Play mode,
        // build, reload de escena), asi que nunca depende de que ese
        // material en particular haya sobrevivido el viaje a disco.
        void Awake() => ReaplicarMaterial();

        void ReaplicarMaterial()
        {
            var mat = SafeMaterial.Create(Color.yellow);
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.sharedMaterial = mat;
        }
    }
}
