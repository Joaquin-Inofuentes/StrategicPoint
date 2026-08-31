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
            //
            // Item 224: este bucle sigue tickeando a TODOS los soldados en
            // TODOS los frames, y eso es deliberado. Lo que se reparte en el
            // tiempo es solo la consulta de sensado, adentro de AiBrain
            // (SenseNearestEnemy): saltear el Tick entero cada N frames
            // saltearia tambien la maquina de estados y el movimiento, y un
            // soldado que se mueve 1 de cada 3 frames se ve tartamudeando.
            // El ahorro es invisible; el tartamudeo, no. Cada cerebro lleva
            // su propio contador de ticks y se desfasa por Id, asi que la
            // carga de sensado ya queda repartida entre frames sin que este
            // bucle tenga que saber nada del tema.
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
