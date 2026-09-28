using System.Collections;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Simulacion
{
    // SC_Simulacion: escena aparte (no SC_Gameplay) para probar rendimiento y
    // estabilidad con muchos soldados en pantalla a la vez, sin arriesgar la
    // mision real. Arma una batalla de RefuerzosAliados (mas la escuadra
    // original) contra los enemigos y tanques que YA trae el nivel
    // (reposicionados a un solo punto de choque en vez de su ruta de
    // patrulla original), enciende el medidor de rendimiento (PerfHudView)
    // y activa Modo Dios para que la prueba no corte por la muerte del
    // jugador.
    //
    // Pedido explicito: 30 soldados propios contra 20 enemigos. El nivel ya
    // trae ~20 enemigos y 3 tanques enemigos (mas el Blindado propio); esto
    // solo agrega refuerzos propios hasta 30 y junta a todos en un mismo
    // lugar para que efectivamente peleen (si no, cada uno sigue su propia
    // ruta de patrulla y nunca se cruzan).
    public class SimulacionBatallaBootstrap : MonoBehaviour
    {
        [SerializeField] GameObject prefabAliado;
        [SerializeField] int totalAliadosDeseado = 30;
        [SerializeField] Vector3 puntoDeChoque = new Vector3(0f, 0.8f, 170f);
        [SerializeField] float radioDeChoque = 22f;
        [SerializeField] bool modoDios = true;

        IEnumerator Start()
        {
            // Un frame de margen: GameplaySceneBootstrap (ProjectilePool,
            // ActorRegistry, etc.) corre en su propio Start y el orden entre
            // dos Start no esta garantizado.
            yield return null;

            if (modoDios) SP.Core.ModoDios.Poner(true);

            var perf = FindAnyObjectByType<SP.UI.PerfHudView>(FindObjectsInactive.Include);
            if (perf != null && !perf.Visible) perf.Toggle();

            SpawnRefuerzos();
            JuntarEnUnSoloChoque();
            SP.Core.ApoyoEnElPiso.ApoyarATodos();
            Physics.SyncTransforms();

            GameLog.Line($"[Simulacion] {ActorRegistry.All.Count} actores en escena, batalla armada en {puntoDeChoque}");
        }

        void SpawnRefuerzos()
        {
            if (prefabAliado == null) return;
            int actuales = 0;
            foreach (var s in ActorRegistry.All) if (s.Team == TeamId.Player) actuales++;
            int faltan = totalAliadosDeseado - actuales;
            if (faltan <= 0) return;

            var pool = SP.Combat.ProjectilePool.Activo;
            var roles = new[] { RoleType.Assault, RoleType.Flanker, RoleType.Sniper, RoleType.Medic };
            var root = new GameObject("RefuerzosSimulacion");

            for (int i = 0; i < faltan; i++)
            {
                float x = puntoDeChoque.x - radioDeChoque + (i % 9) * (radioDeChoque * 2f / 9f);
                float z = puntoDeChoque.z - 25f - (i / 9) * 4f; // llegan desde atras, no adentro del choque
                var pos = new Vector3(x, 0.8f, z);
                var go = Instantiate(prefabAliado, pos, Quaternion.identity, root.transform);
                go.name = "Refuerzo_" + i;
                var s = go.GetComponent<Soldier>();
                if (s == null) { Destroy(go); continue; }
                var role = roles[i % roles.Length];
                s.Configure("Refuerzo_" + i, TeamId.Player, role, 180);
                if (s.Weapon != null && pool != null) s.Weapon.SetPool(pool);
                if (s.Brain != null) s.Brain.enabled = true;
            }
        }

        // Sin esto cada bando sigue su propia ruta de patrulla/seguimiento y
        // la "batalla" nunca pasa de dos grupos caminando por separado.
        void JuntarEnUnSoloChoque()
        {
            int i = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                if (s.name.StartsWith("Refuerzo_")) continue; // ya nacen bien puestos
                bool enemigo = s.Team == TeamId.Enemy;
                float x = puntoDeChoque.x - radioDeChoque + (i % 10) * (radioDeChoque * 2f / 10f);
                float z = puntoDeChoque.z + (enemigo ? 1f : -1f) * (12f + (i / 10) * 4f);
                s.transform.position = new Vector3(x, 0.8f, z);
                s.transform.rotation = Quaternion.Euler(0f, enemigo ? 180f : 0f, 0f);
                if (s.Brain != null) s.Brain.ReactivarNavegacion();
                i++;
            }

            // BUG ENCONTRADO probando esto: colocar los tanques muy juntos
            // (o contra un obstaculo del escenario) hacia que dos colliders
            // se pisaran, y ApoyoEnElPiso.Apoyar (que apoya sobre la
            // superficie MAS ALTA debajo, a proposito, para no atravesar
            // cajas) terminaba "parqueando" uno arriba del otro/del
            // obstaculo -- se leia igual que el "tanque que vuela" que se
            // pidio revisar, pero el problema nunca fue el tanque en
            // MOVIMIENTO (VehicleMotor no toca Y, y la explosion final solo
            // desprende la torreta, ver DetachedTurretFlight) sino la
            // superposicion al colocarlos de una. En vez de adivinar un
            // hueco libre en el borde del camino (lleno de arboles/rocas),
            // se los reparte sobre el camino de tierra central (ancho y sin
            // obstaculos, ver las capturas de la propia mision) separados
            // en Z, no en X.
            int t = 0;
            foreach (var v in SP.Vehicles.Vehicle.Todos)
            {
                v.transform.position = new Vector3(puntoDeChoque.x, 0f, puntoDeChoque.z - 30f - t * 20f);
                v.transform.rotation = Quaternion.identity;
                t++;
            }
        }
    }
}
