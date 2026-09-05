using UnityEngine;
using SP.Core;
using SP.Combat;

namespace SP.Presentation
{
    // Circulo chato que representa a un soldado/vehiculo en el minimapa.
    // Vive en su propia capa (Minimap), que la cámara principal no ve y la
    // cámara del minimapa sí — así el minimapa no muestra el terreno ni la
    // geometría real, solo estos íconos de colores sobre fondo negro.
    public class MinimapIcon : MonoBehaviour
    {
        public Transform Target;
        [SerializeField] float height = 55f;
        // El icono es un circulo chato: rotarlo no cambia nada visible.
        // Para que el minimapa diga "hacia donde estas mirando" (no solo
        // "donde estas"), el icono del jugador suma una cuña que sí
        // rota con el yaw del mundo -- desde la camara cenital del
        // minimapa, la rotacion en Y es exactamente lo que se ve girar.
        // [SerializeField] a proposito, aunque son de uso interno: se
        // asignan por codigo al armar la escena en el Editor (fuera de
        // Play mode), y un campo privado comun NO sobrevive al domain
        // reload al entrar en Play -- ya paso con `arrow` en
        // DamageDirectionView, con `brain` en varias vistas, etc. Como
        // Unity SI serializa los campos marcados [SerializeField] junto
        // con la escena, esto evita tener que reconstruir un self-heal
        // por nombre en OnEnable para algo que ya es una referencia
        // directa al objeto correcto.
        [SerializeField] Transform directionMarker;
        MeshRenderer selfRenderer;

        // El minimapa mostraba a TODOS los enemigos del mapa siempre,
        // incluso a los que la escuadra nunca vio -- eso elimina la
        // exploracion y cualquier sorpresa. Con esto activado, el icono
        // solo se ve mientras algun soldado propio vivo lo tiene dentro
        // de su alcance de vision (el mismo valor que usa AiBrain para
        // sensar).
        [SerializeField] bool fogEnabled;
        // Publica porque quien resuelve la niebla ahora es WorldUiDirector:
        // arma UNA sola lista de observadores vivos por pase y la compara
        // contra todos los iconos, en vez de que cada icono dispare su
        // propia consulta a la grilla espacial con su propio timer.
        public const float FogVisionRange = 10f;

        public bool FogEnabled => fogEnabled;

        public void EnableFogOfWar()
        {
            fogEnabled = true;
            EnsureRenderer();
            if (selfRenderer != null) selfRenderer.enabled = false;
        }

        // selfRenderer es un campo privado comun: NO sobrevive al domain
        // reload al entrar en Play. OnEnable si vuelve a correr ahi, asi
        // que la referencia se recompone sola.
        void EnsureRenderer()
        {
            if (selfRenderer == null) selfRenderer = GetComponent<MeshRenderer>();
        }

        // Alta y baja en el unico recorrido de UI de mundo. Mismo patron
        // que SP.Core.WorldSystemsRegistry.
        void OnEnable()
        {
            EnsureRenderer();
            // Se arregla la escena en vivo y no solo la construccion nueva:
            // los iconos de SC_Gameplay estan serializados con la cuña, y
            // reconstruirlos a mano seria un diff de escena por soldado.
            if (Application.isPlaying && (esTriangulo || directionMarker != null)) ConvertirEnTriangulo();
            WorldUiDirector.Register(this);
        }

        void OnDisable() => WorldUiDirector.Unregister(this);

        public bool IsRendered
        {
            get
            {
                EnsureRenderer();
                return selfRenderer != null && selfRenderer.enabled;
            }
        }

        // El director necesita el punto del mundo que representa el icono
        // sin tener que tocar Target por su cuenta.
        public Vector3 TargetPosition => Target != null ? Target.position : transform.position;

        // Antes esto era un LateUpdate propio: con cincuenta unidades eran
        // cincuenta LateUpdate por frame solo para copiar una posicion.
        // Ahora lo llama WorldUiDirector desde un unico recorrido.
        // Devuelve false si el icono se destruyo por quedarse sin objetivo.
        public bool TickFollow()
        {
            if (Target == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return false;
            }
            transform.position = new Vector3(Target.position.x, height, Target.position.z);
            if (esTriangulo || directionMarker != null)
                transform.rotation = Quaternion.Euler(0f, Target.eulerAngles.y, 0f);
            return true;
        }

        // Solo se llama en los pases de reevaluacion del director (cada
        // ~0.25 s), el mismo espaciado que tenia el timer propio de cada
        // icono: la niebla no necesita resolverse por frame. El calculo de
        // "spotted" vive en el director porque ahi se hace por lote para
        // todos los iconos a la vez.
        public void ApplyFog(bool spotted)
        {
            // Sin el guard de Application.isPlaying: WorldUiDirector no
            // tiene [ExecuteAlways], asi que su LateUpdate (el unico
            // camino real hacia este metodo) ya no corre en Edit mode por
            // si solo. El guard no protegia ningun caso real -- solo le
            // impedia a la suite headless (D1) verificar la niebla, que
            // hasta ahora no tenia ningun Check().
            if (!fogEnabled) return;
            EnsureRenderer();
            if (selfRenderer == null) return;
            selfRenderer.enabled = spotted;
            if (directionMarker != null) directionMarker.gameObject.SetActive(false); // los enemigos no llevan flecha
        }

        // Del plan del usuario: "En el minimapa hay 2 cubitos por soldado.
        // Corregirlo para q sea solo un triangulo simple".
        //
        // Eran dos de verdad: el disco (un Cylinder) MAS una cuña blanca
        // (un Cube) colgada adelante para indicar hacia donde mira. Desde
        // la camara cenital del minimapa eso no se lee como una unidad con
        // frente: se lee como dos manchas por soldado, y con la escuadra
        // junta el minimapa era un amontonamiento.
        //
        // Ahora es UNA sola pieza: el propio icono pasa a ser un triangulo
        // que apunta adonde mira la unidad. Misma cantidad de objetos que
        // un enemigo, la mitad que antes, y el frente se lee de un vistazo.
        [SerializeField] bool esTriangulo;

        // Compartida por todos los iconos: no tiene sentido una malla de
        // tres vertices por soldado.
        static Mesh mallaTriangulo;

        static Mesh MallaTriangulo()
        {
            if (mallaTriangulo != null) return mallaTriangulo;
            var m = new Mesh();
            m.name = "MinimapTriangulo";
            // Chato en XZ y apuntando a +Z, que es el frente de la unidad.
            // Encaja en el mismo circulo de radio 0,5 que ocupaba el disco,
            // asi que el localScale que ya tenia el icono sigue sirviendo.
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0.55f),
                new Vector3(-0.45f, 0f, -0.40f),
                new Vector3(0.45f, 0f, -0.40f),
            };
            // Las dos vueltas: el triangulo se ve desde arriba y desde
            // abajo. Una sola cara obliga a acertar el sentido de giro, y
            // si se erra el icono desaparece sin ningun error en consola.
            m.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            m.hideFlags = HideFlags.HideAndDontSave;
            mallaTriangulo = m;
            return m;
        }

        // El nombre historico se conserva: sigue significando "esta unidad
        // muestra hacia donde mira". Lo que cambio es COMO lo muestra.
        public void EnableDirectionMarker(int layer, float iconRadius)
        {
            ConvertirEnTriangulo();
        }

        public bool ConvertirEnTriangulo()
        {
            // La cuña vieja se borra aunque ya no la cree nadie: los
            // soldados de la escena YA la tienen guardada en el .unity, y
            // sin esto seguirian con los dos cubitos para siempre.
            if (directionMarker != null)
            {
                var go = directionMarker.gameObject;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
                directionMarker = null;
            }

            var filtro = GetComponent<MeshFilter>();
            if (filtro == null) return false;
            filtro.sharedMesh = MallaTriangulo();
            esTriangulo = true;
            return true;
        }

        // Color fijo para los obstaculos del minimapa (D1): no son un
        // bando -- no atacan, no se poseen -- asi que no comparten paleta
        // con el azul de la escuadra ni el rojo enemigo. Un gris piedra
        // que se lee como "terreno", no como unidad.
        public static readonly Color ObstacleMinimapColor = new Color(0.55f, 0.50f, 0.42f);

        const string ObstaclesRootName = "ObstaculoIconosRoot";

        // Un icono de minimapa por cada ObstacleMarker de la escena (D1).
        // Los soldados y vehiculos ya traian el suyo puesto a mano en
        // SC_Gameplay, pero nadie armaba el de los obstaculos: el
        // minimapa no decia nada del terreno hasta acercarse. Idempotente
        // por destruir-y-rearmar (mismo patron que
        // SP.Core.Coberturas.Registrar): llamarlo de nuevo no duplica.
        public static int RegistrarObstaculos(Color color, float radius = 1.4f)
        {
            var previo = GameObject.Find(ObstaclesRootName);
            if (previo != null)
            {
                if (Application.isPlaying) Destroy(previo);
                else DestroyImmediate(previo);
            }

            int layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8; // TagManager trae "Minimap" fijo en el indice 8.

            var root = new GameObject(ObstaclesRootName).transform;
            var marcas = FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var marca in marcas)
            {
                var icon = Spawn(marca.transform, color, layer, radius);
                icon.transform.SetParent(root, true);
            }
            return marcas.Length;
        }

        public static MinimapIcon Spawn(Transform target, Color color, int layer, float radius = 1.6f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "MinimapIcon";
            go.layer = layer;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.localScale = new Vector3(radius, 0.2f, radius);

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = SafeMaterial.Create(color);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var icon = go.AddComponent<MinimapIcon>();
            icon.Target = target;
            return icon;
        }
    }
}
