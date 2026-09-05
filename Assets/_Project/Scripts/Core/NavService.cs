using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // EL PEGAMENTO QUE FALTABA.
    //
    // El proyecto ya tenia WaypointGraph (A*) y FlowField, escritos y
    // cubiertos por la suite headless... y en la partida real NADIE los
    // construia. El unico Build() vivia en HeadlessTestRunner, y
    // PlayerInputDriver.NavGraph se quedaba en null toda la partida (el
    // comentario "Bug 14" de su Start() cuenta que se conecto la vista
    // previa de ruta, pero conectarla a null no dibuja nada). Resultado:
    // el pathfinding existia, estaba testeado, y no se usaba nunca. Los
    // soldados iban siempre en linea recta.
    //
    // Este servicio construye UNA grilla del mapa REAL a partir de los
    // colliders de la escena y se la presta a quien la pida: AiBrain para
    // rodear, PathPreview para dibujar el rodeo.
    //
    // Es static y no un MonoBehaviour a proposito: no hace falta acordarse
    // de arrastrarlo a la escena, y una escena vieja (SC_TestLevel) gana
    // el rodeo sin tocarle un solo GameObject.
    public static class NavService
    {
        // Separacion entre nodos. Con el piso actual (~58 x 160) son unos
        // 2.400 nodos: una consulta de fisica por nodo, UNA vez por
        // partida. Bajarlo a 1 cuadruplica ese costo para afinar un rodeo
        // que a 2 metros ya se ve correcto con cubos de 2 m de lado.
        public const float Spacing = 2f;

        // Cuanto se infla cada obstaculo al marcar nodos intransitables.
        // El cuerpo del soldado mide 0.9 de lado (radio ~0.45); con este
        // margen el camino no ROZA la pared, que es justo lo que haria que
        // el deslizamiento de SoldierMotor lo frenara contra la esquina.
        public const float Clearance = 0.75f;

        // Altura a la que se sondea el mundo. La sonda va de 0.4 a 1.4 de
        // alto: por ENCIMA del piso (su cara superior esta en y=0, y el
        // piso tambien es un collider solido) y dentro del cuerpo de
        // cualquier cubo parado sobre el. Sondear a ras del suelo marcaria
        // el mapa entero como bloqueado.
        const float ProbeCenterY = 0.9f;
        const float ProbeHalfHeight = 0.5f;

        // Valvula de seguridad: un collider perdido a 10.000 metros no
        // puede hacer que la grilla explote a millones de nodos.
        const float MaxHalfExtent = 250f;

        static readonly WaypointGraph graph = new WaypointGraph();
        static readonly Collider[] overlapBuffer = new Collider[16];
        static bool dirty = true;

        public static WaypointGraph Graph { get { EnsureBuilt(); return graph; } }
        public static bool IsReady => graph.IsBuilt;

        // Limites REALES del terreno jugable, sacados de los colliders de
        // la escena al construir la grilla. Existe porque antes el unico
        // limite del proyecto era un cuadrado fijo de +-90 centrado en el
        // origen (CameraRig.mapHalfExtent), y el piso de esta escena no es
        // ni cuadrado ni esta centrado: mide 58 x 160 y su centro esta en
        // (4.4, 57.7). Con el limite viejo la camara RTS paneaba 60 metros
        // fuera del terreno -- pantalla vacia, sin referencia para volver --
        // y una orden de retirada podia mandar a la escuadra a caminar
        // fuera del mundo.
        static Bounds area;
        static bool hayArea;

        // Devuelve false si todavia no se pudo medir (escena sin colliders
        // o fuera de Play); el que llama decide su propio respaldo en vez
        // de comerse un Bounds vacio sin enterarse.
        public static bool TryArea(out Bounds limites)
        {
            EnsureBuilt();
            limites = area;
            return hayArea;
        }

        // Un obstaculo que se derrumba abre un paso que antes no existia.
        // Tambien lo llama el cambio de escena (ver Reset).
        public static void Invalidate()
        {
            dirty = true;
            hayArea = false;
        }

        // Los statics sobreviven entre corridas en Edit mode (la suite no
        // hace domain reload) Y entre CARGAS DE ESCENA en el juego real:
        // una clase estatica no se entera de que cambio el mundo.
        //
        // BUG REAL, y de los peores porque es silencioso: una partida
        // arranca en SC_MainMenu. Ahi alguien consulta el area (la camara
        // lo hace al acotar su paneo), NavService construye contra los
        // colliders del MENU -- que no tiene ninguno --, marca dirty en
        // false y se queda con "no hay area". Al cargar SC_Gameplay nada
        // vuelve a invalidar, asi que la grilla y los limites del mapa se
        // quedaban con lo del menu para toda la partida: el rodeo de
        // obstaculos no se calculaba y los limites de camara, minimapa y
        // retirada caian al respaldo viejo de +-90. O sea que las
        // correcciones de limites no llegaban a aplicarse nunca en una
        // partida de verdad, solo al entrar en Play directo sobre la
        // escena de gameplay.
        //
        // Se engancha a sceneLoaded para que el mundo nuevo siempre se
        // vuelva a medir.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            dirty = true;
            hayArea = false;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        }

        static void AlCargarEscena(UnityEngine.SceneManagement.Scene escena,
                                   UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            Invalidate();
        }

        public static void EnsureBuilt()
        {
            if (!dirty) return;
            dirty = false;
            Build();
        }

        // QUE CUENTA COMO PARED. Un soldado NO frena a otro (siempre se
        // atravesaron entre ellos; cambiarlo seria rediseñar formaciones y
        // reagrupamiento), un vehiculo tampoco -- hay que poder llegar
        // hasta el para montarlo -- y los triggers menos todavia (pickups
        // de armas, zonas). Queda lo que de verdad es escenario: Muro,
        // Obstaculo_*, y cualquier cubo que alguien agregue mañana sin
        // tener que acordarse de marcarlo con nada.
        public static bool BlocksMovement(Collider c)
        {
            if (c == null || !c.enabled || c.isTrigger) return false;
            if (!c.gameObject.activeInHierarchy) return false;
            var t = c.transform;
            if (t.GetComponentInParent<SP.Actors.Soldier>() != null) return false;
            if (t.GetComponentInParent<SP.Vehicles.Vehicle>() != null) return false;
            if (t.GetComponentInParent<SP.Combat.Projectile>() != null) return false;
            return true;
        }

        // Devuelve true SOLO si hace falta desviarse. Si la linea recta
        // esta libre devuelve false sin correr A*, y el que llama sigue
        // con su MoveTowards de siempre: el caso comun no paga ni un nodo.
        public static bool TryFindDetour(Vector3 from, Vector3 to, List<Vector3> result)
        {
            if (result == null) return false;
            result.Clear();
            if (!Application.isPlaying) return false;

            EnsureBuilt();
            if (!graph.IsBuilt) return false;
            if (graph.HasLineOfSight(from, to)) return false;
            if (!graph.TryFindPath(from, to, result) || result.Count <= 2)
            {
                result.Clear();
                return false;
            }
            return true;
        }

        static void Build()
        {
            bool any = false;
            Bounds area_ = default;

            var colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var c in colliders)
            {
                if (c == null || c.isTrigger) continue;
                if (!any) { area_ = c.bounds; any = true; }
                else area_.Encapsulate(c.bounds);
            }

            hayArea = any;
            if (!any) return; // graph queda sin construir: TryFindDetour devuelve false y todo sigue en linea recta

            area = area_;

            float pad = Spacing * 2f;
            var min = new Vector3(
                Mathf.Max(area_.min.x - pad, -MaxHalfExtent), 0f,
                Mathf.Max(area_.min.z - pad, -MaxHalfExtent));
            var max = new Vector3(
                Mathf.Min(area_.max.x + pad, MaxHalfExtent), 0f,
                Mathf.Min(area_.max.z + pad, MaxHalfExtent));

            graph.Build(min, max, Spacing, IsBlockedAt);

            Debug.Log($"[NavService] Grilla lista: {graph.Columns}x{graph.Rows} nodos, " +
                      $"{graph.BlockedCount} bloqueados, spacing {Spacing}, area {min} .. {max}");
        }

        static bool IsBlockedAt(Vector3 nodeCenter)
        {
            float half = Spacing * 0.5f + Clearance;
            var center = new Vector3(nodeCenter.x, ProbeCenterY, nodeCenter.z);
            var extents = new Vector3(half, ProbeHalfHeight, half);

            int n = Physics.OverlapBoxNonAlloc(center, extents, overlapBuffer,
                                               Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (BlocksMovement(overlapBuffer[i])) return true;
            return false;
        }
    }
}
