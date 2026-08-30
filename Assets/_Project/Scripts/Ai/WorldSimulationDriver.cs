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

            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                s.GetComponent<AiBrain>()?.Tick(dt);
                if (s.Weapon != null) s.Weapon.Tick(dt);
            }

            foreach (var v in Object.FindObjectsByType<VehicleBrain>(FindObjectsSortMode.None))
                v.Tick(dt);

            foreach (var t in Object.FindObjectsByType<TurretWeapon>(FindObjectsSortMode.None))
                t.Tick(dt);

            foreach (var ai in Object.FindObjectsByType<TurretAI>(FindObjectsSortMode.None))
                ai.Tick(dt);
        }
    }
}
