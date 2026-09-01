using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Un unico recorrido para TODA la UI de mundo (barras de vida,
    // marcador de poseido, iconos de minimapa) mas nivel de detalle por
    // distancia y por encuadre.
    //
    // Antes cada vista tenia su propio LateUpdate. Con cincuenta soldados
    // eso significaba, POR FRAME:
    //   - 50 llamadas a Camera.main, que es una busqueda por tag y no es
    //     gratis (aca se resuelve UNA vez y se guarda);
    //   - 50 "foreach (Transform child in transform)", que asigna un
    //     enumerador por barra por frame -- basura pura para el GC (aca
    //     los hijos se cachean en la propia vista y se recorren por
    //     indice);
    //   - 50 consultas de niebla de guerra sueltas, cada una barriendo la
    //     grilla espacial por su cuenta (aca se arma UNA sola lista de
    //     observadores vivos por pase y se compara contra todos los
    //     iconos).
    //
    // Mismo patron de alta/baja que SP.Core.WorldSystemsRegistry: listas
    // ESTATICAS que cada vista llena en OnEnable y vacia en OnDisable. Es
    // a proposito y no un array cableado desde el Editor: las referencias
    // privadas asignadas al construir la escena NO sobreviven el domain
    // reload al entrar en Play, mientras que OnEnable vuelve a correr
    // siempre.
    public class WorldUiDirector : MonoBehaviour
    {
        // ------------------------------------------------------------
        // Registro estatico
        // ------------------------------------------------------------
        static readonly List<HealthBarView> healthBars = new List<HealthBarView>();
        static readonly List<MinimapIcon> minimapIcons = new List<MinimapIcon>();
        static readonly List<PossessedMarkerView> possessedMarkers = new List<PossessedMarkerView>();

        public static void Register(HealthBarView v) { if (v != null && !healthBars.Contains(v)) healthBars.Add(v); }
        public static void Unregister(HealthBarView v) => healthBars.Remove(v);

        public static void Register(MinimapIcon v) { if (v != null && !minimapIcons.Contains(v)) minimapIcons.Add(v); }
        public static void Unregister(MinimapIcon v) => minimapIcons.Remove(v);

        public static void Register(PossessedMarkerView v) { if (v != null && !possessedMarkers.Contains(v)) possessedMarkers.Add(v); }
        public static void Unregister(PossessedMarkerView v) => possessedMarkers.Remove(v);

        // ------------------------------------------------------------
        // Contadores de verificacion
        // ------------------------------------------------------------
        // Cuantos elementos de UI de mundo estan dados de alta ahora
        // mismo. Es el universo que recorre el unico LateUpdate.
        public static int RegisteredCount => healthBars.Count + minimapIcons.Count + possessedMarkers.Count;

        // Cuantos de esos quedaron efectivamente dibujados en el ultimo
        // pase. Alejar la camara tiene que hacerlo bajar.
        public static int VisibleCount { get; private set; }

        // Cuantos estan apagados EXCLUSIVAMENTE por LOD (distancia o
        // fuera de encuadre). Separado de VisibleCount a proposito: una
        // barra apagada porque su soldado no recibio daño no es un
        // elemento "descartado por lejania", y mezclarlos haria imposible
        // verificar el item.
        public static int CulledCount { get; private set; }

        // ------------------------------------------------------------
        // Ajustes de LOD
        // ------------------------------------------------------------
        // [SerializeField] y no const para poder tunearlos en la escena.
        // Mas alla de esta distancia el elemento se apaga: a esa altura
        // una barra de vida ocupa un par de pixeles y no comunica nada.
        [SerializeField] float maxVisibleDistance = 60f;

        // La visibilidad se reevalua a intervalo, NO todos los frames:
        // mismo enfoque que ya usaba MinimapIcon con su timer de niebla
        // (0.3 s). Un elemento que entra o sale de encuadre puede tardar
        // hasta un cuarto de segundo en reaccionar, que es imperceptible
        // y ahorra el 90% de las cuentas de proyeccion.
        [SerializeField] float evaluateInterval = 0.25f;

        // Margen alrededor del viewport (fraccion de pantalla) para que
        // ese retardo de reevaluacion no se vea como un parpadeo en los
        // bordes: el elemento se apaga recien un poco despues de salir.
        [SerializeField] float viewportMargin = 0.15f;

        // ------------------------------------------------------------
        // Estado de instancia
        // ------------------------------------------------------------
        static WorldUiDirector active;
        static readonly List<WorldUiDirector> enabledInstances = new List<WorldUiDirector>();
        public bool IsDrivingUpdates => active == this;

        // Camera.main resuelto UNA vez. Se re-resuelve solo si quedo en
        // null (cambio de escena, camara destruida): el operador == de
        // UnityEngine.Object ya devuelve true para un objeto destruido.
        Camera cam;
        float nextEvaluateAt;

        // Se reusa entre pases para no asignar una lista nueva cada vez.
        readonly List<Vector3> fogObservers = new List<Vector3>();

        void OnEnable()
        {
            enabledInstances.Add(this);
            if (active == null) active = this;
            // La camara vieja no vale tras un cambio de escena.
            cam = null;
            nextEvaluateAt = 0f;
        }

        void OnDisable()
        {
            enabledInstances.Remove(this);
            if (active == this)
            {
                active = enabledInstances.Count > 0 ? enabledInstances[0] : null;
            }
        }

        // Un solo LateUpdate en todo el juego para la UI de mundo. Si por
        // accidente hubiera dos directores en la escena, solo el primero
        // recorre: dos pases dejarian los contadores al doble y harian el
        // trabajo dos veces.
        void LateUpdate()
        {
            if (active != this) return;
            Tick();
        }

        // Publico para poder recorrer a mano desde una suite headless de
        // Editor, donde LateUpdate no corre (ningun MonoBehaviour del
        // proyecto tiene [ExecuteAlways]).
        public void Tick()
        {
            EnsureCamera();

            bool hasCam = cam != null;
            Vector3 camPos = hasCam ? cam.transform.position : Vector3.zero;
            Quaternion camRot = hasCam ? cam.transform.rotation : Quaternion.identity;

            bool reevaluate = Time.time >= nextEvaluateAt;
            if (reevaluate) nextEvaluateAt = Time.time + Mathf.Max(0.05f, evaluateInterval);

            int visible = 0;
            int culled = 0;

            // Recorridos hacia atras: apagar o destruir un elemento puede
            // disparar su OnDisable en el acto, que se da de baja de la
            // lista que estamos recorriendo. Yendo de atras para adelante
            // eso nunca saltea un indice pendiente.
            for (int i = healthBars.Count - 1; i >= 0; i--)
            {
                var bar = healthBars[i];
                if (bar == null) { healthBars.RemoveAt(i); continue; }

                if (reevaluate) bar.SetLodAllowed(IsWithinLod(bar.transform.position, camPos, hasCam));
                if (!bar.LodAllowed) culled++;

                // OJO: Tick() compone el LOD con la regla propia de la
                // barra (visible ~3.5 s tras daño o curacion). El LOD
                // solo resta; nunca toca esa ventana de tiempo, asi que
                // un soldado dañado que sale y vuelve a encuadre muestra
                // la barra el tiempo que le quedaba, exactamente igual
                // que antes.
                if (bar.Tick())
                {
                    visible++;
                    if (hasCam) bar.ApplyBillboard(camRot);
                }
            }

            for (int i = possessedMarkers.Count - 1; i >= 0; i--)
            {
                var pm = possessedMarkers[i];
                if (pm == null) { possessedMarkers.RemoveAt(i); continue; }

                if (reevaluate)
                {
                    if (pm.TryGetLodProbe(out Vector3 probe)) pm.SetLodAllowed(IsWithinLod(probe, camPos, hasCam));
                    else pm.SetLodAllowed(true); // sin poseido no hay nada que apagar
                }
                if (!pm.LodAllowed) culled++;

                if (pm.Tick()) visible++;
            }

            // La niebla, por lote: una sola pasada por el registro de
            // actores para juntar a los observadores vivos, y despues una
            // comparacion de distancias contra todos los iconos. Antes
            // cada icono resolvia su propia consulta contra la grilla
            // espacial con su propio timer.
            if (reevaluate) RebuildFogObservers();

            for (int i = minimapIcons.Count - 1; i >= 0; i--)
            {
                var icon = minimapIcons[i];
                if (icon == null) { minimapIcons.RemoveAt(i); continue; }

                // Si su objetivo desaparecio, el icono se destruye solo y
                // ya no cuenta para nada este frame.
                if (!icon.TickFollow()) continue;

                // A proposito: los iconos de minimapa NO se apagan por
                // distancia a la camara principal. El minimapa esta para
                // mostrar lo que esta lejos; recortarlo por distancia
                // seria un cambio de reglas de juego disfrazado de
                // optimizacion. Lo que se centraliza aca es su
                // actualizacion y su consulta de niebla.
                if (reevaluate && icon.FogEnabled) icon.ApplyFog(IsSpotted(icon.TargetPosition));
                if (icon.IsRendered) visible++;
            }

            VisibleCount = visible;
            CulledCount = culled;
        }

        void EnsureCamera()
        {
            if (cam != null) return;
            cam = Camera.main;
        }

        // Criterio de LOD: dentro del radio y dentro del encuadre.
        //
        // En RTS la camara es ortografica y cenital a ~30 unidades de
        // altura sobre el centro del paneo, asi que la distancia sigue
        // siendo un buen discriminante al alejarse: con el zoom afuera del
        // todo entran en cuadro unidades a mas de cien unidades de la
        // camara, y son justamente esas las que se apagan.
        bool IsWithinLod(Vector3 worldPos, Vector3 camPos, bool hasCam)
        {
            // Sin camara no hay criterio posible. Se devuelve true a
            // proposito: el LOD solo puede QUITAR elementos que ya
            // estarian visibles, nunca apagar por falta de informacion.
            if (!hasCam) return true;

            float max = Mathf.Max(0f, maxVisibleDistance);
            if ((worldPos - camPos).sqrMagnitude > max * max) return false;

            var vp = cam.WorldToViewportPoint(worldPos);
            // z <= 0 es "detras de la camara" tanto en perspectiva como en
            // ortografica.
            if (vp.z <= 0f) return false;
            return vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin
                && vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin;
        }

        // Mismo predicado que usaba MinimapIcon con
        // ActorRegistry.FindNearestEnemyInRange(pos, TeamId.Enemy, r):
        // "algun soldado vivo que NO sea del bando enemigo, dentro del
        // rango de vision". Sin filtro de activeInHierarchy, igual que la
        // grilla espacial (un soldado montado en un vehiculo esta
        // desactivado pero sigue vivo y sigue viendo).
        void RebuildFogObservers()
        {
            fogObservers.Clear();
            var all = ActorRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.Team == TeamId.Enemy) continue;
                var hp = s.Health;
                if (hp == null || !hp.IsAlive) continue;
                fogObservers.Add(s.transform.position);
            }
        }

        bool IsSpotted(Vector3 worldPos)
        {
            float r = MinimapIcon.FogVisionRange;
            float r2 = r * r;
            for (int i = 0; i < fogObservers.Count; i++)
                if ((fogObservers[i] - worldPos).sqrMagnitude <= r2) return true;
            return false;
        }

        // ------------------------------------------------------------
        // Alta masiva y limpieza (mismo contrato que WorldSystemsRegistry)
        // ------------------------------------------------------------
        static bool populated;

        // El alta se hace desde OnEnable, que NO corre en Edit mode para
        // un MonoBehaviour sin [ExecuteAlways]. Una suite headless que
        // construya la escena en Edit mode y quiera contar o recorrer la
        // UI de mundo tiene que llamar a esto primero; si no, las listas
        // quedan vacias ahi. Se paga una sola vez.
        public static void EnsurePopulated()
        {
            if (populated) return;
            populated = true;

            foreach (var v in UnityEngine.Object.FindObjectsByType<HealthBarView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Register(v);
            foreach (var v in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Register(v);
            foreach (var v in UnityEngine.Object.FindObjectsByType<PossessedMarkerView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Register(v);
        }

        public static void Clear()
        {
            healthBars.Clear();
            minimapIcons.Clear();
            possessedMarkers.Clear();
            VisibleCount = 0;
            CulledCount = 0;
            populated = false;
        }

        // ------------------------------------------------------------
        // Red de seguridad
        // ------------------------------------------------------------
        // Las vistas ya no tienen LateUpdate propio: si nadie recorre, no
        // se actualizan. En Edit mode eso da igual (tampoco corrian antes),
        // pero en Play mode una escena sin el director dejaria las barras
        // congeladas para siempre. Esto NO reemplaza cablearlo a mano: si
        // ya hay uno en la escena, no crea nada.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            // -= antes de += porque con "Enter Play Mode" sin domain
            // reload los estaticos sobreviven y la suscripcion se
            // duplicaria en cada entrada a Play.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureExists();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureExists();

        static void EnsureExists()
        {
            // active ya esta seteado si la escena traia el suyo: el
            // OnEnable de los objetos de escena corre antes que
            // AfterSceneLoad.
            if (active != null) return;
            // FindAnyObjectByType y no FindFirstObjectByType (obsoleto):
            // alcanza con saber si existe alguno, no cual.
            if (FindAnyObjectByType<WorldUiDirector>() != null) return;
            var go = new GameObject("WorldUiDirector");
            go.AddComponent<WorldUiDirector>();
        }
    }
}
