using UnityEngine;
using SP.Core;
using SP.Combat;
using SP.Vehicles;

namespace SP.Ai
{
    // Avanza IA, armas y vehículos cada frame en Play mode real. El test
    // automático no usa esto: simula el mismo paso a mano para tener
    // control sobre el tiempo.
    [DefaultExecutionOrder(-100)]
    public class WorldSimulationDriver : MonoBehaviour
    {
        public static WorldSimulationDriver Instance { get; private set; }

        void Awake()
        {
            // Guarda de instancia unica: dos WorldSimulationDriver activos
            // tickearian el mundo (IA, armas, vehiculos, torretas) dos
            // veces por frame cada uno -- silencioso pero grave (cadencia
            // de disparo real al doble, doble avance de vehiculos).
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Ya existe un WorldSimulationDriver activo ({Instance.name}); se desactiva esta segunda instancia ({name}) para no duplicar la simulacion.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

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
        // Item 59: distancia (m) a la camara desde la que un soldado se tickea a media frecuencia.
        public const float DistanciaLod = 120f;
        public static bool LodPorDistancia = true;
        public static int LastSoldadosSaltadosPorLod { get; private set; }
        static int tickNumero;

        public static double LastRebuildMs { get; private set; }
        public static double LastAiWeaponMs { get; private set; }
        public static double LastVehicleMs { get; private set; }
        static readonly System.Diagnostics.Stopwatch profileWatch = new System.Diagnostics.Stopwatch();

        public static void Step(float dt)
        {
            SP.Combat.TeamCombatState.Tick(dt);

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
            //
            // Item 59 (LOD por distancia a la camara): los soldados a mas de
            // DistanciaLod metros de la camara principal no se ven, asi que ahi
            // SI se puede tickear uno de cada dos frames sin tartamudeo visible;
            // ese tick recibe el dt de los dos frames (el tiempo simulado es el
            // mismo, solo llega en pasos mas gruesos). Se desfasa por Id para
            // que la mitad lejana no caiga siempre en el mismo frame. Sin
            // camara (suite headless) o con LodPorDistancia apagado, todos los
            // soldados se tickean en todos los frames como antes.
            tickNumero++;
            var cam = LodPorDistancia ? CamaraPrincipal.Actual : null;
            var camPos = cam != null ? cam.transform.position : Vector3.zero;
            float lod2 = DistanciaLod * DistanciaLod;
            int saltados = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                float dtSoldado = dt;
                if (cam != null)
                {
                    if ((s.transform.position - camPos).sqrMagnitude > lod2)
                    {
                        s.DtLodPendiente += dt;
                        if (((tickNumero + s.Id) & 1) != 0) { saltados++; continue; }
                        dtSoldado = s.DtLodPendiente;
                    }
                    s.DtLodPendiente = 0f;
                }
                s.Brain?.Tick(dtSoldado);
                if (s.Weapon != null) s.Weapon.Tick(dtSoldado);
                // Pedido explicito: a los 3 s sin recibir daño, regenera
                // solo. Mismo camino de simulacion que Brain/Weapon, para
                // que la suite headless (SimStep) lo ejercite igual que el
                // juego real.
                s.Health?.Tick(dtSoldado);
            }
            LastSoldadosSaltadosPorLod = saltados;
            LastAiWeaponMs = profileWatch.Elapsed.TotalMilliseconds;

            // El pedido de curacion del menu de ordenes ([Q] sostenido).
            // Va aca y no en un Update propio porque este es el unico
            // camino de simulacion que corren por igual el juego y la
            // suite: un Update aparte quedaria sin cobertura.
            SP.Player.PedidoDeCuracion.Tick(dt);
            // A5: rescate automatico cuando muere el jugador. Mismo camino
            // de simulacion por el mismo motivo.
            SP.Player.RescateAutomatico.Tick(dt);
            // Ronda 11: derrota / recuperacion en calma de la escuadra completa (siempre evaluada, no una sola vez).
            SP.Mision.EstadoDePartida.Tick(dt);
            // G1: cuenta regresiva de los barriles encendidos hasta que
            // estallan solos. Mismo camino de simulacion por el mismo motivo.
            SP.Presentation.ObstacleMarker.Tick(dt);
            // G4: cruce entre musica de estrategia y de lucha segun si hay
            // combate cerca de la camara. Mismo camino de simulacion.
            SP.Presentation.MusicDirector.Tick(dt);

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
