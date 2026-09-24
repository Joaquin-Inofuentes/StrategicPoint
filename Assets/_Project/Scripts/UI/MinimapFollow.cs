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
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static MinimapFollow Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
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

        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable()
        {
            RegistrarActivo();
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

            // La RawImage vive en el Canvas, no bajo esta camara: la referencia viene serializada en la escena. Si falta
            // se avisa UNA vez (rectSearchDone) en vez de barrer la escena buscando por nombre.
            SP.Core.GameLog.Line("[MinimapFollow] falta la referencia 'minimapRect' (" + MinimapImageName + ") en la escena");
            return null;
        }

        Camera ResolveCamera()
        {
            if (minimapCamera == null) minimapCamera = GetComponent<Camera>();
            return minimapCamera;
        }

        // ------------------------------------------------------------
        // TAMAÑO DEL MINIMAPA: mini (por defecto) y expandido
        // ------------------------------------------------------------
        // GameObject : Cameras/MinimapCamera  (este componente)
        // Script     : MinimapFollow
        // Variables  : tamanoMini      -> tamaño por defecto (arranca asi)
        //              tamanoExpandido -> tamaño al apretar [M]
        // Se editan en el Inspector y el marco (UI_Canvas/Canvas/MinimapBorder)
        // se redimensiona al instante, sin dar Play.
        //
        // Antes habia dos mecanismos (x1.8 sobre el tamaño de la escena y un
        // ciclo de tres tamaños guardado en PlayerPrefs) y ganaba el segundo:
        // la escena decia 40 pero en juego salia 320 porque un indice viejo
        // en PlayerPrefs lo pisaba al arrancar. Ahora hay UNA fuente de
        // verdad -- estas dos variables -- y nada se recuerda entre partidas:
        // siempre se arranca en mini.
        [Header("Tamaño del minimapa (px de referencia del Canvas)")]
        [Tooltip("Tamaño por defecto (mini). El juego siempre arranca con este.")]
        public Vector2 tamanoMini = new Vector2(150f, 150f);
        [Tooltip("Tamaño al expandir con [M].")]
        public Vector2 tamanoExpandido = new Vector2(320f, 320f);

        // Nombre del RectTransform que se redimensiona: el marco entero, no
        // la RawImage sola -- MinimapBorder es el padre de MinimapImage y
        // del resto de la decoracion (MinimapFrame, N), anclado por su
        // esquina (pivot 1,1) para no desplazarse al crecer.
        const string BorderName = "MinimapBorder";

        [SerializeField] RectTransform borderRect;
        bool borderAvisado;

        public bool Agrandado { get; private set; }

        // Marco que se redimensiona (MinimapBorder). Lo asigna la escena serializada o quien la arma.
        public RectTransform Marco { get => ResolveBorder(); set => borderRect = value; }

        RectTransform ResolveBorder()
        {
            if (borderRect != null) return borderRect;
            if (!borderAvisado) { borderAvisado = true; SP.Core.GameLog.Line("[MinimapFollow] falta la referencia 'borderRect' (" + BorderName + ") en la escena"); }
            return null;
        }

        // Deja el minimapa en mini. Lo llama el arranque de la escena (y el
        // Editor al tocar las variables), para que el primer frame ya sea
        // el definitivo.
        // Pedido explicito: "que haya muchisimo contraste entre environment
        // y soldados e interactuables" -- el verde oscuro de antes competia
        // con el gris de los obstaculos y no dejaba tanto margen contra los
        // colores saturados de los rombos. Un fondo casi negro (no negro
        // puro, para poder distinguirlo de "no hay nada renderizado" si algo
        // falla) maximiza el contraste contra CUALQUIER color de icono.
        public static readonly Color ColorDeFondo = new Color(0.025f, 0.03f, 0.035f);

        public void AplicarTamanoInicial()
        {
            // Escenas ya horneadas guardaron 107 px (ilegible): se sube al nuevo minimo y el fondo deja de ser negro puro.
            if (tamanoMini.x < 140f) tamanoMini = new Vector2(150f, 150f);
            var camMini = GetComponent<Camera>();
            // Siempre se fuerza (no solo cuando ya era negro puro): asi una
            // escena horneada con el verde oscuro viejo tambien se pone al
            // dia sin tener que retocarla a mano.
            if (camMini != null) camMini.backgroundColor = ColorDeFondo;
            Agrandado = false;
            indiceTamanoFijo = 0;
            var b = ResolveBorder();
            if (b != null) b.sizeDelta = tamanoMini;
        }

        // Compatibilidad: antes recuperaba el tamaño guardado. Ya no se
        // guarda nada; el tamaño "guardado" es siempre el mini.
        public void AplicarTamanoGuardado() => AplicarTamanoInicial();

        // [M]: mini <-> expandido. Devuelve el nuevo estado (true =
        // expandido). Como los dos tamaños son variables fijas no hay
        // deriva posible por mas veces que se apriete.
        public bool AlternarTamano()
        {
            var b = ResolveBorder();
            if (b == null) return Agrandado;
            Agrandado = !Agrandado;
            indiceTamanoFijo = Agrandado ? 2 : 0;
            b.sizeDelta = Agrandado ? tamanoExpandido : tamanoMini;
            return Agrandado;
        }

        // [L]: mini -> medio -> expandido -> mini. No se guarda.
        int indiceTamanoFijo;
        public int IndiceTamanoFijo => indiceTamanoFijo;

        public Vector2 TamanoFijo(int indice)
        {
            indice = Mathf.Clamp(indice, 0, 2);
            return indice == 0 ? tamanoMini : indice == 2 ? tamanoExpandido : (tamanoMini + tamanoExpandido) * 0.5f;
        }

        public int CiclarTamanoFijo()
        {
            var b = ResolveBorder();
            indiceTamanoFijo = (indiceTamanoFijo + 1) % 3;
            Agrandado = indiceTamanoFijo == 2;
            if (b != null) b.sizeDelta = TamanoFijo(indiceTamanoFijo);
            return indiceTamanoFijo;
        }

#if UNITY_EDITOR
        // Al cambiar tamanoMini en el Inspector (sin Play) el marco se
        // actualiza solo. delayCall: no se puede tocar otro objeto de la
        // escena desde dentro de OnValidate.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                var b = ResolveBorder();
                if (b != null && !Agrandado) b.sizeDelta = tamanoMini;
            };
        }
#endif
    }
}
