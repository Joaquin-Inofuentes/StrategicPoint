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

        public static IReadOnlyList<VehicleBrain> VehicleBrains => vehicleBrains;
        public static IReadOnlyList<TurretWeapon> TurretWeapons => turretWeapons;
        public static IReadOnlyList<TurretAI> TurretAis => turretAis;

        public static void Register(VehicleBrain v) { if (!vehicleBrains.Contains(v)) vehicleBrains.Add(v); }
        public static void Unregister(VehicleBrain v) => vehicleBrains.Remove(v);

        public static void Register(TurretWeapon t) { if (!turretWeapons.Contains(t)) turretWeapons.Add(t); }
        public static void Unregister(TurretWeapon t) => turretWeapons.Remove(t);

        public static void Register(TurretAI a) { if (!turretAis.Contains(a)) turretAis.Add(a); }
        public static void Unregister(TurretAI a) => turretAis.Remove(a);

        public static void Clear()
        {
            vehicleBrains.Clear();
            turretWeapons.Clear();
            turretAis.Clear();
        }
    }
}
