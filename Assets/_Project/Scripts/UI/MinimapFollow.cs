using UnityEngine;

namespace SP.UI
{
    // Cámara cenital del minimapa: sigue al objetivo actual (soldado o
    // vehículo poseído) desde arriba, mirando siempre hacia abajo.
    //
    // Ademas de seguir al objetivo, esta clase es la unica duenia del
    // mapeo mundo <-> minimapa. Vive aca y no en el driver de input
    // porque los tres datos que definen el encuadre (posicion de la
    // camara, orthographicSize y la RenderTexture) los tiene esta camara:
    // duplicarlos en otro lado seria garantizar que un dia se
    // desincronicen.
    //
    // El mapeo es CERRADO y exacto: la camara es ortografica y cenital
    // pura (rotacion 90,0,0), asi que no hay perspectiva ni division por
    // w. Mundo -> minimapa es una escala lineal, y minimapa -> mundo es
    // esa misma escala invertida. Por eso el viaje de ida y vuelta
    // devuelve el mismo punto salvo error de coma flotante, y se puede
    // verificar sin escena ni raycasts.
    public class MinimapFollow : MonoBehaviour
    {
        public Transform Target;
        [SerializeField] float height = 60f;

        // Los tres campos de abajo van SERIALIZADOS a proposito. Un campo
        // privado normal asignado al construir la escena en el editor NO
        // sobrevive el domain reload al entrar en Play: quedaria en null y
        // las conversiones devolverian false para siempre, en silencio.
        [SerializeField] Camera minimapCamera;

        // RectTransform de la RawImage donde se dibuja la RenderTexture del
        // minimapa. Es el marco de referencia de los puntos locales que
        // entran y salen de las conversiones.
        [SerializeField] RectTransform minimapRect;

        // Altura del "suelo" a la que se devuelven los puntos convertidos.
        // El minimapa es una vista 2D: la Y del mundo se pierde en la ida y
        // hay que reponerla en la vuelta. Con esto el round-trip es exacto
        // para cualquier punto que este a esta altura, que es el caso de
        // todas las ordenes de movimiento del juego.
        [SerializeField] float groundY = 0f;

        // Mismo limite de mapa que usan CameraRig (mapHalfExtent) y
        // OrderService (90). Un clic mas alla de esto no es una orden
        // valida: no hay suelo ahi y la unidad se quedaria empujando el
        // borde. Se rechaza en vez de clampear, para que el driver pueda
        // dar feedback de "orden rechazada" en vez de mover a un lugar que
        // el jugador no pidio.
        [SerializeField] float mapHalfExtent = 90f;

        // Nombre del hijo del Canvas que se busca si minimapRect llega
        // vacio. Ultimo rescate, no el camino normal.
        const string MinimapImageName = "MinimapImage";

        // Tolerancia de borde. Sin esto, un punto que cae EXACTAMENTE en el
        // limite del rect puede rebotar a 1.0000001 al volver de la
        // conversion inversa y ser rechazado, rompiendo el round-trip justo
        // en las esquinas.
        const float EdgeTolerance = 1e-4f;
        const float WorldTolerance = 1e-3f;

        // Se busca UNA sola vez por activacion: GameObject.Find recorre la
        // escena y no puede correr por frame ni por clic.
        bool rectSearchDone;

        public RectTransform MinimapRect
        {
            get => ResolveRect();
            set { minimapRect = value; rectSearchDone = value != null; }
        }

        public Camera MinimapCamera
        {
            get => ResolveCamera();
            set => minimapCamera = value;
        }

        public float GroundY { get => groundY; set => groundY = value; }
        public float MapHalfExtent { get => mapHalfExtent; set => mapHalfExtent = value; }

        void OnEnable()
        {
            // Tras el domain reload se permite un unico reintento de
            // rescate; si el campo serializado vino bien, ResolveRect ni
            // llega a buscar.
            rectSearchDone = minimapRect != null;
            ResolveCamera();
        }

        void LateUpdate()
        {
            if (Target == null) return;
            transform.position = new Vector3(Target.position.x, height, Target.position.z);
        }

        // Convierte un punto local del RectTransform de la RawImage (el
        // mismo espacio que devuelve RectTransformUtility.ScreenPointToLocal
        // PointInRectangle) a una posicion del mundo sobre el suelo.
        // Devuelve false si el punto cae fuera de la imagen o si el mundo
        // resultante cae fuera de los limites del mapa.
        public bool TryMinimapPointToWorld(Vector2 localPoint, out Vector3 world)
        {
            world = Vector3.zero;

            var rect = ResolveRect();
            var cam = ResolveCamera();
            if (rect == null || cam == null) return false;

            Rect r = rect.rect;
            if (r.width <= 0f || r.height <= 0f) return false;

            // Normalizado [0..1] dentro del rect. Coincide exactamente con
            // la UV de la RenderTexture porque la RawImage la estira
            // completa sobre su rect (uvRect por defecto).
            float nx = (localPoint.x - r.xMin) / r.width;
            float ny = (localPoint.y - r.yMin) / r.height;
            if (nx < -EdgeTolerance || nx > 1f + EdgeTolerance) return false;
            if (ny < -EdgeTolerance || ny > 1f + EdgeTolerance) return false;

            float halfH = cam.orthographicSize;
            float halfW = halfH * ViewAspect(cam);
            if (halfW <= 0f || halfH <= 0f) return false;

            // Con rotacion (90,0,0) el eje derecho de la camara es +X del
            // mundo y su eje arriba es +Z. Por eso el mapeo es directo: X
            // con X, Y de la imagen con Z del mundo, sin matrices.
            Vector3 center = CameraCenter(cam);
            world = new Vector3(
                center.x + (nx - 0.5f) * 2f * halfW,
                groundY,
                center.z + (ny - 0.5f) * 2f * halfH);

            return IsInsideWorld(world);
        }

        // Conversion directa: posicion del mundo -> punto local de la
        // RawImage. Existe para poder verificar el round-trip
        // (mundo -> minimapa -> mundo) sin escena, y de paso sirve para
        // dibujar marcadores encima del minimapa. Devuelve false si el
        // punto esta fuera del mapa o fuera del encuadre actual del
        // minimapa (que sigue a la unidad, asi que no ve el mapa entero).
        public bool TryWorldToMinimapPoint(Vector3 world, out Vector2 localPoint)
        {
            localPoint = Vector2.zero;

            var rect = ResolveRect();
            var cam = ResolveCamera();
            if (rect == null || cam == null) return false;
            if (!IsInsideWorld(world)) return false;

            Rect r = rect.rect;
            if (r.width <= 0f || r.height <= 0f) return false;

            float halfH = cam.orthographicSize;
            float halfW = halfH * ViewAspect(cam);
            if (halfW <= 0f || halfH <= 0f) return false;

            Vector3 center = CameraCenter(cam);
            float nx = 0.5f + (world.x - center.x) / (2f * halfW);
            float ny = 0.5f + (world.z - center.z) / (2f * halfH);
            if (nx < -EdgeTolerance || nx > 1f + EdgeTolerance) return false;
            if (ny < -EdgeTolerance || ny > 1f + EdgeTolerance) return false;

            localPoint = new Vector2(r.xMin + nx * r.width, r.yMin + ny * r.height);
            return true;
        }

        // El centro del encuadre es donde esta la camara en el plano XZ.
        // Se lee de la camara y no de este transform por si algun dia la
        // camara del minimapa deja de compartir GameObject con este
        // componente.
        static Vector3 CameraCenter(Camera cam) => cam.transform.position;

        // Relacion de aspecto del encuadre. Se toma de la RenderTexture y
        // no de cam.aspect porque cam.aspect puede llegar en 0 fuera de
        // Play mode o con la camara recien creada por script, y un 0 ahi
        // colapsaria todo el eje X del mapeo.
        static float ViewAspect(Camera cam)
        {
            var rt = cam.targetTexture;
            if (rt != null && rt.height > 0) return (float)rt.width / rt.height;
            return cam.aspect > 1e-4f ? cam.aspect : 1f;
        }

        // TERCER lugar donde vivia el mismo limite equivocado (los otros
        // dos eran CameraRig y OrderService): un cuadrado de +-90 centrado
        // en el ORIGEN. El terreno de esta escena va de x=-24,8 a 33,6 y de
        // z=-22,3 a 137,7, asi que:
        //   - clickear el minimapa por encima de z=90 se RECHAZABA, y son
        //     casi cincuenta metros de terreno perfectamente jugable donde
        //     el minimapa no daba ninguna orden;
        //   - y al oeste aceptaba hasta x=-90, sesenta metros fuera del
        //     piso, mandando la orden a un punto que no existe.
        // Se usa el terreno medido por NavService, con el campo serializado
        // como respaldo para una escena sin colliders.
        bool IsInsideWorld(Vector3 p)
        {
            if (SP.Core.NavService.TryArea(out var limites))
            {
                return p.x >= limites.min.x - WorldTolerance && p.x <= limites.max.x + WorldTolerance
                    && p.z >= limites.min.z - WorldTolerance && p.z <= limites.max.z + WorldTolerance;
            }

            float limit = mapHalfExtent + WorldTolerance;
            return Mathf.Abs(p.x) <= limit && Mathf.Abs(p.z) <= limit;
        }

        RectTransform ResolveRect()
        {
            if (minimapRect != null) return minimapRect;
            if (rectSearchDone) return null;
            rectSearchDone = true;

            // Rescate por nombre. Ojo: la RawImage NO es hija de esta
            // camara (vive dentro del Canvas), asi que transform.Find no
            // alcanza y hay que buscar en la escena. Corre como maximo una
            // vez por activacion, nunca por frame ni por clic.
            var go = GameObject.Find(MinimapImageName);
            if (go != null) minimapRect = go.GetComponent<RectTransform>();
            return minimapRect;
        }

        Camera ResolveCamera()
        {
            if (minimapCamera == null) minimapCamera = GetComponent<Camera>();
            return minimapCamera;
        }
    }
}
