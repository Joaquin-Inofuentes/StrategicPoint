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
        void Update() => Step(Time.deltaTime);

        // Extraido a estatico para que HeadlessTestRunner.SimStep (la
        // simulacion manual que corre la suite en Edit mode) llame EXACTAMENTE
        // esto y no una copia que fue divergiendo con el tiempo. Antes SimStep
        // tenia su propia version que usaba GetComponent<AiBrain>() en vez del
        // Brain cacheado, FindObjectsByType en vez de WorldSystemsRegistry, y
        // -- el hueco real -- nunca tickeaba TurretAI. La suite quedaba
        // validando una simulacion distinta de la que corre en el juego real,
        // asi que los items 222/223 (los cacheos de este mismo archivo) no
        // tenian ninguna cobertura.
        // Acumuladores de solo lectura para el arnes de benchmark (item 235
        // companion). Envuelven los mismos bloques de siempre con un
        // cronometro reusado (sin asignar por llamada): no cambian ninguna
        // logica ni el orden de ejecucion, y su costo es un Restart()/lectura
        // de Stopwatch por bloque -- despreciable frente a lo que miden.
        public static double LastRebuildMs { get; private set; }
        public static double LastAiWeaponMs { get; private set; }
        public static double LastVehicleMs { get; private set; }
        static readonly System.Diagnostics.Stopwatch profileWatch = new System.Diagnostics.Stopwatch();

        public static void Step(float dt)
        {
            // Reparte a los soldados vivos en celdas ANTES de que nadie
            // pregunte "hay un enemigo cerca" este tick -- una sola vez
            // por Update, no una vez por soldado que sensa.
            profileWatch.Restart();
            SpatialGrid.Rebuild();
            LastRebuildMs = profileWatch.Elapsed.TotalMilliseconds;

            profileWatch.Restart();

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
            LastAiWeaponMs = profileWatch.Elapsed.TotalMilliseconds;

            profileWatch.Restart();
            var vehicleBrains = WorldSystemsRegistry.VehicleBrains;
            for (int i = 0; i < vehicleBrains.Count; i++)
                if (vehicleBrains[i] != null) vehicleBrains[i].Tick(dt);

            var turrets = WorldSystemsRegistry.TurretWeapons;
            for (int i = 0; i < turrets.Count; i++)
                if (turrets[i] != null) turrets[i].Tick(dt);

            var turretAis = WorldSystemsRegistry.TurretAis;
            for (int i = 0; i < turretAis.Count; i++)
                if (turretAis[i] != null) turretAis[i].Tick(dt);
            LastVehicleMs = profileWatch.Elapsed.TotalMilliseconds;
        }
    }
}
