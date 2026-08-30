using UnityEngine;
using SP.Core;
using SP.Combat;
using SP.Vehicles;

namespace SP.Ai
{
    // Avanza IA, armas y vehículos cada frame en Play mode real. El test
    // automático no usa esto: simula el mismo paso a mano para tener
    // control sobre el tiempo.
    public class WorldSimulationDriver : MonoBehaviour
    {
        void Update()
        {
            float dt = Time.deltaTime;

            // Reparte a los soldados vivos en celdas ANTES de que nadie
            // pregunte "hay un enemigo cerca" este tick -- una sola vez
            // por Update, no una vez por soldado que sensa.
            SpatialGrid.Rebuild();

            // Antes: GetComponent<AiBrain>() por soldado por frame, y tres
            // Object.FindObjectsByType (un barrido completo de la escena
            // cada uno) para vehiculos, torretas y su IA. Con cincuenta
            // soldados eso es tres mil GetComponent y ciento ochenta
            // barridos de escena por segundo, solo para descubrir "que
            // existe". Ahora todo sale de listas cacheadas que se
            // mantienen al alta/baja de cada objeto (Soldier.Brain,
            // WorldSystemsRegistry).
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                s.Brain?.Tick(dt);
                if (s.Weapon != null) s.Weapon.Tick(dt);
            }

            var vehicleBrains = WorldSystemsRegistry.VehicleBrains;
            for (int i = 0; i < vehicleBrains.Count; i++)
                if (vehicleBrains[i] != null) vehicleBrains[i].Tick(dt);

            var turrets = WorldSystemsRegistry.TurretWeapons;
            for (int i = 0; i < turrets.Count; i++)
                if (turrets[i] != null) turrets[i].Tick(dt);

            var turretAis = WorldSystemsRegistry.TurretAis;
            for (int i = 0; i < turretAis.Count; i++)
                if (turretAis[i] != null) turretAis[i].Tick(dt);
        }
    }
}
