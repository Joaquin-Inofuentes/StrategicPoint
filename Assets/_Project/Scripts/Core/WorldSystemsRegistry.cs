using System.Collections.Generic;
using SP.Vehicles;

namespace SP.Core
{
    // Mismo patron que ActorRegistry, pero para los tres tipos que
    // WorldSimulationDriver necesita recorrer cada frame. Antes ese driver
    // llamaba Object.FindObjectsByType tres veces por Update -- un barrido
    // completo de la escena, tres veces, sesenta veces por segundo. Con
    // estas listas cacheadas el costo de descubrir "que vehiculos/torretas
    // existen" pasa de ser pagado cada frame a pagarse solo al aparecer o
    // destruirse uno.
    public static class WorldSystemsRegistry
    {
        static readonly List<VehicleBrain> vehicleBrains = new List<VehicleBrain>();
        static readonly List<TurretWeapon> turretWeapons = new List<TurretWeapon>();
        static readonly List<TurretAI> turretAis = new List<TurretAI>();
        // Vehiculos y obstaculos: Projectile.Tick los barria con
        // FindObjectsByType POR PROYECTIL y POR FRAME (dos barridos, mas un
        // tercero en Explode). Con 50 unidades disparando y ~30 proyectiles
        // en vuelo eso son ~60 barridos completos de escena por frame: un
        // orden de magnitud peor que los 3 por frame que ya se habian
        // sacado del driver de simulacion.
        static readonly List<Vehicle> vehicles = new List<Vehicle>();
        static readonly List<SP.Presentation.ObstacleMarker> obstacles = new List<SP.Presentation.ObstacleMarker>();

        public static IReadOnlyList<VehicleBrain> VehicleBrains => vehicleBrains;
        public static IReadOnlyList<TurretWeapon> TurretWeapons => turretWeapons;
        public static IReadOnlyList<TurretAI> TurretAis => turretAis;
        public static IReadOnlyList<Vehicle> Vehicles => vehicles;
        public static IReadOnlyList<SP.Presentation.ObstacleMarker> Obstacles => obstacles;

        public static void Register(VehicleBrain v) { if (!vehicleBrains.Contains(v)) vehicleBrains.Add(v); }
        public static void Unregister(VehicleBrain v) => vehicleBrains.Remove(v);

        public static void Register(TurretWeapon t) { if (!turretWeapons.Contains(t)) turretWeapons.Add(t); }
        public static void Unregister(TurretWeapon t) => turretWeapons.Remove(t);

        public static void Register(TurretAI a) { if (!turretAis.Contains(a)) turretAis.Add(a); }
        public static void Unregister(TurretAI a) => turretAis.Remove(a);

        public static void Register(Vehicle v) { if (!vehicles.Contains(v)) vehicles.Add(v); }
        public static void Unregister(Vehicle v) => vehicles.Remove(v);

        public static void Register(SP.Presentation.ObstacleMarker o) { if (!obstacles.Contains(o)) obstacles.Add(o); }
        public static void Unregister(SP.Presentation.ObstacleMarker o) => obstacles.Remove(o);

        // El alta se hace desde OnEnable, que NO corre en Edit mode para
        // un MonoBehaviour sin [ExecuteAlways]. La suite headless construye
        // y simula la escena en Edit mode, asi que sin esto el registro
        // queda vacio ahi y los proyectiles no le pegarian a ningun
        // vehiculo ni obstaculo: la suite dejaria de cubrir ese camino
        // entero sin dar ningun error. Se paga una sola vez.
        static bool populated;

        public static void EnsurePopulated()
        {
            if (populated) return;
            populated = true;

            foreach (var v in UnityEngine.Object.FindObjectsByType<Vehicle>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                Register(v);
            foreach (var o in UnityEngine.Object.FindObjectsByType<SP.Presentation.ObstacleMarker>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                Register(o);
        }

        public static void Clear()
        {
            vehicleBrains.Clear();
            turretWeapons.Clear();
            turretAis.Clear();
            vehicles.Clear();
            obstacles.Clear();
            populated = false;
        }
    }
}
