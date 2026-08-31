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
            if (directionMarker != null)
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
            if (!fogEnabled || !Application.isPlaying) return;
            EnsureRenderer();
            if (selfRenderer == null) return;
            selfRenderer.enabled = spotted;
            if (directionMarker != null) directionMarker.gameObject.SetActive(false); // los enemigos no llevan flecha
        }

        public void EnableDirectionMarker(int layer, float iconRadius)
        {
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "DirectionMarker";
            nose.layer = layer;
            var col = nose.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            nose.transform.SetParent(transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.15f, iconRadius * 0.9f);
            nose.transform.localScale = new Vector3(iconRadius * 0.5f, 0.2f, iconRadius * 0.7f);

            var rend = nose.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            rend.sharedMaterial = new Material(shader) { color = Color.white };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            directionMarker = nose.transform;
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
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = color };
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var icon = go.AddComponent<MinimapIcon>();
            icon.Target = target;
            return icon;
        }
    }
}
