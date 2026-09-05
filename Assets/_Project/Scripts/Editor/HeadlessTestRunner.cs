using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using SP.Core;
using SP.Combat;
using SP.Actors;
using SP.Ai;
using SP.Player;
using SP.CameraSystem;
using SP.UI;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Construye el entorno de prueba (suelo, obstáculos, prefabs, 3 soldados,
    // cámara, UI, pool de proyectiles) y corre las 3 fases del guion de test
    // pedido, avanzando la simulación a mano (sin depender de Play mode) y
    // volcando cada paso a la consola con timer. Pensado para -batchmode.
    public static class HeadlessTestRunner
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_TestLevel.unity";

        static AimUI aimUiRef;
        static SelectedSoldierUI rosterUiRef;
        static InstructionBannerView instructionUiRef;
        static PhaseBannerView phaseBannerRef;
        static NearbySquadListView squadListRef;
        static Image selectionBoxRef;
        static MinimapFollow minimapFollowRef;
        static Transform canvasRootRef;
        static DeadNoticeView deadNoticeRef;
        static WeaponStatusView weaponStatusRef;
        static PlayerHealthView playerHealthRef;
        static MissionStatusView missionStatusRef;
        static SelectionCountView selectionCountRef;
        static ModeToastView modeToastRef;
        static SP.UI.MenuDeOrdenes ordenesMenuRef;
        static CanvasScaler hudScalerRef;
        static Text victoryStatsRef;
        static Text defeatStatsRef;
        static VehicleStatusView vehicleStatusRef;
        static TurretAimView turretAimRef;
        static OffscreenKillMarkerView offscreenKillRef;
        static DamageVignetteView damageVignetteRef;
        static SP.UI.PerfHudView perfHudRef;
        static SP.UI.GroupCardsView groupCardsRef;
        static SP.UI.OffscreenAllyMarkerView offscreenAlliesRef;
        static SP.Core.WaypointGraph inputDriverNavGraph;
        static KillFeedView killFeedRef;
        static PauseController pauseControllerRef;
        static GameOutcomeController outcomeControllerRef;

        static int cachedMinimapLayer = -1;

        // El minimapa necesita una capa propia: su cámara solo renderiza esa
        // capa (íconos de colores) y la cámara principal la ignora, así el
        // jugador nunca ve los íconos flotando en el mundo real. Los layers
        // 8..31 son de uso libre; se reusa el primero que ya se llame
        // "Minimap" o, si no existe, el primer slot libre.
        static int GetOrCreateMinimapLayer()
        {
            if (cachedMinimapLayer >= 0) return cachedMinimapLayer;

            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == "Minimap")
                {
                    cachedMinimapLayer = i;
                    return i;
                }
            }
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = "Minimap";
                    tagManager.ApplyModifiedProperties();
                    cachedMinimapLayer = i;
                    return i;
                }
            }
            cachedMinimapLayer = 31; // fallback: último layer, casi nunca ocupado
            return cachedMinimapLayer;
        }

        // Pedido explicito: "material simple comun", no una busqueda de
        // shader por nombre. Antes esto hacia Shader.Find("Universal
        // Render Pipeline/Lit") a mano -- si esa cadena no resolvia bien
        // en el momento (variantes sin compilar, orden de carga, lo que
        // sea) el objeto quedaba con el shader de error. En vez de
        // adivinar el shader correcto para la pipeline activa, se CLONA
        // el material que UNITY MISMO le pone a un primitivo nuevo: eso
        // ya esta resuelto para la pipeline real del proyecto (sea URP,
        // Built-in o lo que sea) sin que este codigo tenga que saber su
        // nombre. Un template cacheado y clonado por color, en vez de
        // instanciar un primitivo por cada material pedido.
        static Material defaultMaterialTemplate;
        static Material CreateFlatMaterial(Color color)
        {
            if (defaultMaterialTemplate == null)
            {
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                defaultMaterialTemplate = new Material(temp.GetComponent<Renderer>().sharedMaterial);
                if (Application.isPlaying) UnityEngine.Object.Destroy(temp);
                else UnityEngine.Object.DestroyImmediate(temp);
            }

            var mat = new Material(defaultMaterialTemplate);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        static void BuildLighting()
        {
            var lightGO = new GameObject("SunLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.98f, 0.93f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // Luz de relleno tenue para que el lado en sombra no quede negro.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

            // Una escena nueva (EditorSceneManager.NewScene) hereda el
            // skybox default de Unity, "Default-Skybox/Skybox/Procedural" --
            // un shader del Built-in Render Pipeline, no de URP. La camara
            // nunca lo dibuja (clearFlags es SolidColor), pero
            // RenderSettings.defaultReflectionMode queda en Skybox igual.
            // Se saca por las dudas (Custom + textura null = sin reflejo
            // ambiental, cosmetico) aunque NO sea la causa del magenta
            // reportado en obstaculos/UI: probado y descartado -- el
            // magenta sigue identico con esto en Custom. Esa investigacion
            // sigue abierta.
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
        }

        [MenuItem("Strategic Point/Run All Tests Headless")]
        public static void RunAll()
        {
            bool ok;
            try
            {
                ok = RunOnceCore(logSuccessPhase: true);
            }
            catch (Exception)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // Una corrida completa (construir escena + las 7 fases), extraida de
        // RunAll para que RunMany pueda invocarla N veces seguidas en el
        // mismo proceso sin duplicar el manejo de excepciones/exit code.
        // Devuelve false (sin lanzar) ante un check fallido; SI relanza una
        // excepcion real (bug de verdad, no una aserción) para que quede
        // visible con su stack trace completo.
        static bool RunOnceCore(bool logSuccessPhase)
        {
            // Se resetea al arrancar: sin esto, una segunda corrida en la
            // misma sesion de Editor arrastraria las fallas de la anterior
            // (los estaticos sobreviven entre invocaciones de RunAll).
            failedChecks = 0;
            failedCheckMessages.Clear();

            TestLog.Begin();
            BuildAndRun();

            if (failedChecks > 0)
            {
                var detail = string.Join("\n  - ", failedCheckMessages);
                Debug.LogError($"[TEST FALLIDO] {failedChecks} check(s) no se cumplieron:\n  - {detail}");
                return false;
            }

            if (logSuccessPhase) TestLog.Phase("TODAS LAS FASES COMPLETADAS CON EXITO");
            return true;
        }

        // Pedido explicito: correr la suite muchas veces seguidas (100 por
        // defecto) para cazar dos clases de bug que UNA sola corrida no
        // puede ver: flakiness (algo que depende de un orden/tiempo que a
        // veces cae distinto) y fugas de estado estatico entre corridas
        // (registros/pools/suscripciones que sobreviven a BuildAndRun y se
        // acumulan). BuildAndRun ya reconstruye la escena entera cada vez
        // (ver su comentario "BUG REAL encontrado al repetir RunAll()
        // varias veces seguidas"), asi que esto reusa exactamente ese mismo
        // camino real, no una copia aparte.
        [MenuItem("Strategic Point/Correr 100 iteraciones (flakiness y fugas de estado)")]
        public static void RunManyHeadless() => RunMany(100);

        public static void RunMany(int iterations)
        {
            failedChecks = 0;
            failedCheckMessages.Clear();

            int failures = 0;
            var failureDetails = new List<string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long memBefore = System.GC.GetTotalMemory(false);

            for (int i = 1; i <= iterations; i++)
            {
                bool ok;
                try
                {
                    ok = RunOnceCore(logSuccessPhase: false);
                }
                catch (Exception ex)
                {
                    ok = false;
                    Debug.LogError($"[ITER {i}/{iterations}] Excepcion no capturada (bug real, no un Check fallido): {ex}");
                }

                if (!ok)
                {
                    failures++;
                    string detail = failedCheckMessages.Count > 0 ? string.Join(" | ", failedCheckMessages) : "ver excepcion arriba";
                    failureDetails.Add($"iter {i}: {detail}");
                }
                else if (i % 10 == 0 || i == iterations)
                {
                    Debug.Log($"[RunMany] {i}/{iterations} OK (memoria administrada: {System.GC.GetTotalMemory(false) / 1024 / 1024} MB)");
                }
            }

            sw.Stop();
            long memAfter = System.GC.GetTotalMemory(true);
            Debug.Log($"[RunMany] {iterations - failures}/{iterations} iteraciones OK en {sw.Elapsed.TotalSeconds:0.0}s ({sw.Elapsed.TotalSeconds / iterations:0.00}s/iter). Memoria administrada: {memBefore / 1024 / 1024} MB -> {memAfter / 1024 / 1024} MB (delta {(memAfter - memBefore) / 1024 / 1024} MB).");

            if (failures > 0)
            {
                Debug.LogError($"[RunMany] {failures}/{iterations} iteraciones fallaron:\n  - {string.Join("\n  - ", failureDetails)}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------
        // Arnes de rendimiento (item 235: sin esto, cualquier afirmacion
        // de rendimiento es una opinion). Construye un mundo aislado (NO
        // toca ni guarda SC_TestLevel.unity) con N unidades en una grilla
        // determinista, y mide el costo real de WorldSimulationDriver.Step
        // con 30 pasos de calentamiento descartados y 300 cronometrados.
        // Reporta mediana y p95 -- nunca el promedio, que un solo pico de
        // GC arruina y esconde justo lo que interesa.
        // ---------------------------------------------------------------
        public struct BenchResult
        {
            public int UnitCount;
            public float MedianMs, P95Ms;
            public float RebuildMs, AiWeaponMs, VehicleMs, ProjectileMs;
        }

        public static readonly List<BenchResult> LastBenchmarkResults = new List<BenchResult>();

        [MenuItem("Strategic Point/Benchmark de rendimiento")]
        public static void RunPerformanceBenchmarks()
        {
            LastBenchmarkResults.Clear();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("N,medianaMs,p95Ms,rebuildMs,aiArmaMs,vehiculoMs,proyectilMs");

            // Los tres tamaños que importan: 10 es el caso de hoy, 60 el
            // realista, 200 el de estres. Medir solo a 200 miente -- este
            // mismo proyecto ya vio una estructura salir MAS LENTA que la
            // fuerza bruta con pocas unidades (SpatialGrid a CellSize=8).
            foreach (var n in new[] { 10, 60, 200 })
            {
                var r = BenchmarkWithUnitCount(n);
                LastBenchmarkResults.Add(r);
                sb.AppendLine($"{r.UnitCount},{r.MedianMs:0.000},{r.P95Ms:0.000},{r.RebuildMs:0.000},{r.AiWeaponMs:0.000},{r.VehicleMs:0.000},{r.ProjectileMs:0.000}");
                Debug.Log($"[Benchmark] N={r.UnitCount}  mediana={r.MedianMs:0.000}ms  p95={r.P95Ms:0.000}ms  (rebuild={r.RebuildMs:0.000} ai+arma={r.AiWeaponMs:0.000} vehiculo={r.VehicleMs:0.000} proyectiles={r.ProjectileMs:0.000})");
            }

            LastBenchmarkCsv = sb.ToString();
            Debug.Log("[Benchmark] CSV completo (para diffear entre corridas):\n" + LastBenchmarkCsv);
        }

        public static string LastBenchmarkCsv { get; private set; } = "";

        static BenchResult BenchmarkWithUnitCount(int unitCount)
        {
            // Mundo aislado: NewScene sin guardar nada, para no pisar
            // SC_TestLevel.unity ni sus referencias estaticas (killFeedRef,
            // etc, que quedarian apuntando a objetos de una escena vieja).
            DestroyTransientRuntimeAssets();
            EventBus.Instance.ClearAll();
            ActorRegistry.Clear();
            SP.Core.WorldSystemsRegistry.Clear();
            Projectile.ActiveInstances.Clear();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();
            BuildObstacles();

            var soldierPrefab = BuildAndSaveSoldierPrefab();
            var projectilePrefab = BuildAndSaveProjectilePrefab();
            var poolGO = new GameObject("BenchPool");
            var pool = poolGO.AddComponent<ProjectilePool>();
            pool.Configure(projectilePrefab, SP.Combat.ProjectilePool.RecommendedPrewarm(unitCount, 3f, 3f));

            // Semilla FIJA: la misma corrida de N siempre reparte a las
            // unidades en las mismas posiciones, para que dos benchmarks
            // (antes/despues de un cambio) sean comparables entre si.
            var rng = new System.Random(12345);
            var playerColor = new Color(0.95f, 0.35f, 0.30f);
            var enemyColor = new Color(0.95f, 0.25f, 0.20f);
            int half = unitCount / 2;
            for (int i = 0; i < unitCount; i++)
            {
                bool isPlayer = i < half;
                var pos = new Vector3((float)(rng.NextDouble() * 160.0 - 80.0), 0.8f, (float)(rng.NextDouble() * 160.0 - 80.0));
                SpawnSoldier(soldierPrefab, (isPlayer ? "Bench_P_" : "Bench_E_") + i,
                    isPlayer ? TeamId.Player : TeamId.Enemy, RoleType.Assault, pos,
                    isPlayer ? playerColor : enemyColor, pool, 100);
            }
            SP.Core.WorldSystemsRegistry.EnsurePopulated();

            const float dt = 0.05f;
            const int warmupSteps = 30;
            const int measuredSteps = 300;

            for (int i = 0; i < warmupSteps; i++) SimStep(dt);

            // Forzar el GC ANTES de medir, no durante: construir tres
            // mundos seguidos (10, 60 y 200 unidades) en la misma llamada
            // acumula basura, y una coleccion que cae justo en medio de la
            // ventana medida se atribuye al paso que estaba corriendo en
            // ese instante -- no es costo real de esa fase, es ruido del
            // recolector. Practica estandar de benchmarking: forzarlo en
            // el setup, nunca dejar que interrumpa la muestra.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            var samples = new float[measuredSteps];
            double rebuildAcc = 0, aiAcc = 0, vehicleAcc = 0, projAcc = 0;
            // Ademas del promedio, se guarda el desglose por fase del paso
            // MAS LENTO de toda la corrida: un promedio no dice nada sobre
            // de donde viene una cola larga (p95 muy por encima de la
            // mediana), porque la diluye entre 300 pasos. El paso peor
            // SI dice que fase domino ese pico en particular.
            float worstStepMs = -1f;
            float worstRebuild = 0f, worstAi = 0f, worstVehicle = 0f, worstProj = 0f;
            int worstStepIndex = -1;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < measuredSteps; i++)
            {
                double t0 = sw.Elapsed.TotalMilliseconds;
                SimStep(dt);
                float stepMs = (float)(sw.Elapsed.TotalMilliseconds - t0);
                samples[i] = stepMs;
                rebuildAcc += WorldSimulationDriver.LastRebuildMs;
                aiAcc += WorldSimulationDriver.LastAiWeaponMs;
                vehicleAcc += WorldSimulationDriver.LastVehicleMs;
                projAcc += LastProjectileMs;
                if (stepMs > worstStepMs)
                {
                    worstStepMs = stepMs;
                    worstStepIndex = i;
                    worstRebuild = (float)WorldSimulationDriver.LastRebuildMs;
                    worstAi = (float)WorldSimulationDriver.LastAiWeaponMs;
                    worstVehicle = (float)WorldSimulationDriver.LastVehicleMs;
                    worstProj = (float)LastProjectileMs;
                }
            }

            System.Array.Sort(samples);
            float median = samples[measuredSteps / 2];
            int idx95 = Mathf.Clamp(Mathf.CeilToInt(measuredSteps * 0.95f) - 1, 0, measuredSteps - 1);
            float p95 = samples[idx95];

            Debug.Log($"[Benchmark] N={unitCount} paso mas lento: #{worstStepIndex} total={worstStepMs:0.000}ms (rebuild={worstRebuild:0.000} ai+arma={worstAi:0.000} vehiculo={worstVehicle:0.000} proyectiles={worstProj:0.000})");

            return new BenchResult
            {
                UnitCount = unitCount,
                MedianMs = median,
                P95Ms = p95,
                RebuildMs = (float)(rebuildAcc / measuredSteps),
                AiWeaponMs = (float)(aiAcc / measuredSteps),
                VehicleMs = (float)(vehicleAcc / measuredSteps),
                ProjectileMs = (float)(projAcc / measuredSteps),
            };
        }

        // ---------------------------------------------------------------
        // Arnes de equivalencia (regla de oro del proyecto convertida en
        // codigo: una optimizacion no debe cambiar las reglas de juego).
        // Ya se rompio una vez: una version anterior de SpatialGrid filtro
        // a los soldados inactivos y cambio en silencio a quien detectaba
        // la IA. K consultas con semilla fija contra un barrido de fuerza
        // bruta independiente, en mundos de N in {10, 60, 200}, exige CERO
        // discrepancias.
        // ---------------------------------------------------------------
        public static int LastEquivalenceMismatches { get; private set; }

        [MenuItem("Strategic Point/Verificar equivalencia SpatialGrid")]
        public static void RunEquivalenceCheck()
        {
            int total = 0;
            foreach (var n in new[] { 10, 60, 200 })
            {
                int mismatches = EquivalenceCheckWithUnitCount(n, 5000);
                total += mismatches;
                Debug.Log($"[Equivalencia] N={n}: {mismatches} discrepancias sobre 5000 consultas.");
            }
            LastEquivalenceMismatches = total;
            if (total == 0)
                Debug.Log("[Equivalencia] SpatialGrid.FindNearestInRange es equivalente a fuerza bruta en N=10,60,200. 0 discrepancias.");
            else
                Debug.LogError($"[Equivalencia] {total} discrepancias totales -- SpatialGrid NO es equivalente a fuerza bruta.");
        }

        static int EquivalenceCheckWithUnitCount(int unitCount, int queries)
        {
            DestroyTransientRuntimeAssets();
            EventBus.Instance.ClearAll();
            ActorRegistry.Clear();
            SP.Core.WorldSystemsRegistry.Clear();
            Projectile.ActiveInstances.Clear();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();

            var soldierPrefab = BuildAndSaveSoldierPrefab();
            var projectilePrefab = BuildAndSaveProjectilePrefab();
            var poolGO = new GameObject("EquivPool");
            var pool = poolGO.AddComponent<ProjectilePool>();
            pool.Configure(projectilePrefab, 4);

            var rng = new System.Random(999);
            var playerColor = new Color(0.95f, 0.35f, 0.30f);
            var enemyColor = new Color(0.95f, 0.25f, 0.20f);

            // Casos de borde SEMBRADOS a proposito, no dejados al azar:
            // posiciones exactas sobre multiplos de CellSize (20, el punto
            // donde un soldado cae justo en el borde entre dos celdas), y
            // coordenadas negativas (ejercitan el Offset = 1<<20 del
            // empaquetado de claves). Un tercio de las unidades nace
            // desactivada (simula ir montado en un vehiculo): SpatialGrid
            // NO filtra por activeInHierarchy a proposito, igual que el
            // FindNearest original, y este es el escenario que exactamente
            // detecto esa diferencia la primera vez.
            int half = unitCount / 2;
            for (int i = 0; i < unitCount; i++)
            {
                bool isPlayer = i < half;
                Vector3 pos;
                if (i < 6)
                {
                    // Multiplos exactos de CellSize, con signo alternado.
                    float mult = ((i / 2) + 1) * 20f * (i % 2 == 0 ? 1f : -1f);
                    pos = new Vector3(mult, 0.8f, mult * 0.5f);
                }
                else
                {
                    pos = new Vector3((float)(rng.NextDouble() * 200.0 - 100.0), 0.8f, (float)(rng.NextDouble() * 200.0 - 100.0));
                }
                var s = SpawnSoldier(soldierPrefab, (isPlayer ? "Equiv_P_" : "Equiv_E_") + i,
                    isPlayer ? TeamId.Player : TeamId.Enemy, RoleType.Assault, pos,
                    isPlayer ? playerColor : enemyColor, pool, 100);
                if (i % 3 == 0) s.gameObject.SetActive(false);
            }

            SP.Core.SpatialGrid.Rebuild();

            int mismatches = 0;
            for (int q = 0; q < queries; q++)
            {
                var point = new Vector3((float)(rng.NextDouble() * 240.0 - 120.0), 0.8f, (float)(rng.NextDouble() * 240.0 - 120.0));
                float range = (float)(rng.NextDouble() * 55.0 + 5.0);
                var excludeTeam = rng.Next(2) == 0 ? TeamId.Player : TeamId.Enemy;

                var gridResult = ActorRegistry.FindNearestEnemyInRange(point, excludeTeam, range);
                var bruteResult = ActorRegistry.FindNearest(point, s =>
                    s.Health.IsAlive && s.Team != excludeTeam && Vector3.Distance(point, s.transform.position) <= range);

                if (!ResultsEquivalent(gridResult, bruteResult, point))
                {
                    mismatches++;
                    if (mismatches <= 5)
                        Debug.LogWarning($"[Equivalencia] N={unitCount} query={q}: point={point} range={range:0.0} excludeTeam={excludeTeam} grid={(gridResult != null ? gridResult.name : "null")} brute={(bruteResult != null ? bruteResult.name : "null")}");
                }
            }
            return mismatches;
        }

        // Mismo Id -> identico. Id distinto pero MISMA distancia (dentro de
        // epsilon) -> dos candidatos equidistantes, un resultado
        // legitimamente distinto y no una falla real. Sin esta segunda
        // rama el arnes daria falsos positivos en cualquier empate y
        // terminaria ignorado, que es peor que no tenerlo.
        static bool ResultsEquivalent(Soldier a, Soldier b, Vector3 point)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Id == b.Id) return true;
            float da = Vector3.Distance(point, a.transform.position);
            float db = Vector3.Distance(point, b.transform.position);
            return Mathf.Abs(da - db) < 0.0001f;
        }

        // ---------------------------------------------------------------
        // Escenarios de estres con carga realista (item 5 del plan de
        // cierre): el objetivo declarado del proyecto es 50+ soldados, y
        // hasta este punto ninguna mejora de rendimiento se habia medido
        // con carga real -- solo con los ~10 de la escena de test.
        // ---------------------------------------------------------------
        public struct StressResult
        {
            public int OrderMarkerActiveAfter50;
            public bool OrderMarkerWithinBudget;
            public int ProjectilesExhaustedCount;
            public bool ProjectilePoolHeld;
            public int RingSpawnsAfterFill;
            public int RingSpawnsAfter200Changes;
            public bool RingPoolStoppedGrowing;
        }

        public static StressResult LastStressResult;

        [MenuItem("Strategic Point/Estres con carga realista (50+)")]
        public static void RunStressScenarios()
        {
            DestroyTransientRuntimeAssets();
            EventBus.Instance.ClearAll();
            ActorRegistry.Clear();
            SP.Core.WorldSystemsRegistry.Clear();
            Projectile.ActiveInstances.Clear();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLighting();
            BuildGround();

            const int StressSoldierCount = 50;
            var soldierPrefab = BuildAndSaveSoldierPrefab();
            var projectilePrefab = BuildAndSaveProjectilePrefab();
            var poolGO = new GameObject("StressPool");
            var pool = poolGO.AddComponent<ProjectilePool>();
            pool.Configure(projectilePrefab, SP.Combat.ProjectilePool.RecommendedPrewarm(StressSoldierCount, 3f, 3f));

            var rng = new System.Random(555);
            var playerColor = new Color(0.95f, 0.35f, 0.30f);
            var soldiers = new List<Soldier>(StressSoldierCount);
            for (int i = 0; i < StressSoldierCount; i++)
            {
                var pos = new Vector3((float)(rng.NextDouble() * 100.0 - 50.0), 0.8f, (float)(rng.NextDouble() * 100.0 - 50.0));
                soldiers.Add(SpawnSoldier(soldierPrefab, "Stress_" + i, TeamId.Player, RoleType.Assault, pos, playerColor, pool, 100));
            }

            var result = new StressResult();

            // (a) Ordenar a las 50 de una: OrderMarkerFx.Spawn se llama UNA
            // VEZ POR SOLDADO. El tope duro (64) tiene que aguantar el lote
            // entero sin desbordar.
            foreach (var s in soldiers)
                SP.Presentation.OrderMarkerFx.Spawn(s.transform.position, SP.Presentation.OrderMarkerFx.MoveColor);
            result.OrderMarkerActiveAfter50 = SP.Presentation.OrderMarkerFx.ActiveCount;
            result.OrderMarkerWithinBudget = SP.Presentation.OrderMarkerFx.TotalCount <= SP.Presentation.OrderMarkerFx.Budget;

            // (b) Fuego sostenido con >=30 proyectiles en vuelo a la vez:
            // el pool prewarmeado no debe tener que instanciar en caliente.
            for (int i = 0; i < 40; i++)
            {
                var shooter = soldiers[i % soldiers.Count];
                pool.Spawn(shooter.transform.position, Vector3.forward, shooter.Id, TeamId.Player, 10);
            }
            result.ProjectilesExhaustedCount = pool.ExhaustedCount;
            result.ProjectilePoolHeld = pool.ExhaustedCount == 0;

            // (c) 200 cambios de seleccion con las 50 unidades: tras el
            // llenado inicial del pool de anillos, reusar no debe
            // instanciar primitivas nuevas.
            var ringManagerGO = new GameObject("StressRingManager");
            var ringManager = ringManagerGO.AddComponent<SP.Presentation.SelectionRingManager>();
            var onSelChanged = GetRequiredMethod(typeof(SP.Presentation.SelectionRingManager), "OnSelectionChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            SP.Presentation.SelectionRingFx.ResetSpawnCount();
            var allIds = soldiers.ConvertAll(s => s.Id);
            onSelChanged.Invoke(ringManager, new object[] { new SelectionChangedEvent(allIds) });
            result.RingSpawnsAfterFill = SP.Presentation.SelectionRingFx.SpawnCount;
            int subsetSize = Mathf.Clamp(StressSoldierCount / 5, 1, StressSoldierCount);

            for (int i = 0; i < 200; i++)
            {
                // Alterna entre la escuadra completa y un subconjunto, que
                // es el patron real de jugar (seleccionar todo, despues
                // acotar a unos pocos, despues volver a todos).
                var subset = i % 2 == 0 ? allIds : allIds.GetRange(0, subsetSize);
                onSelChanged.Invoke(ringManager, new object[] { new SelectionChangedEvent(subset) });
            }
            result.RingSpawnsAfter200Changes = SP.Presentation.SelectionRingFx.SpawnCount;
            result.RingPoolStoppedGrowing = result.RingSpawnsAfter200Changes == result.RingSpawnsAfterFill;

            LastStressResult = result;

            Debug.Log($"[Estres] OrderMarker: activos={result.OrderMarkerActiveAfter50} dentroDelPresupuesto={result.OrderMarkerWithinBudget}");
            Debug.Log($"[Estres] ProjectilePool: exhaustedCount={result.ProjectilesExhaustedCount} sostuvoElPrewarm={result.ProjectilePoolHeld}");
            Debug.Log($"[Estres] SelectionRingFx: spawnsTrasLlenado={result.RingSpawnsAfterFill} spawnsTras200Cambios={result.RingSpawnsAfter200Changes} poolDejoDeCrecer={result.RingPoolStoppedGrowing}");

            bool allGood = result.OrderMarkerWithinBudget && result.ProjectilePoolHeld && result.RingPoolStoppedGrowing;
            if (allGood) Debug.Log("[Estres] Los tres escenarios de carga realista (50 unidades) pasaron.");
            else Debug.LogError("[Estres] Al menos un escenario de carga realista fallo -- ver el detalle arriba.");
        }

        // Construye el mismo mundo que el test automático pero sin correr
        // las fases: deja a Vega libre, sana y parada junto al vehículo,
        // lista para una demo manual en Play mode (E para subir, etc).
        [MenuItem("Strategic Point/Construir nivel para demo (sin test)")]
        public static void BuildDemoScene()
        {
            BuildAndRun(runPhases: false);
        }

        static void BuildAndRun(bool runPhases = true)
        {
            DestroyTransientRuntimeAssets();
            EventBus.Instance.ClearAll();
            ActorRegistry.Clear();
            Projectile.ActiveInstances.Clear();
            // BUG REAL encontrado al repetir RunAll() varias veces seguidas
            // sin recompilar entremedio (sin domain reload, que es lo que
            // normalmente lo resetea): EnsurePopulated() solo puebla una
            // vez por sesion (guarda "populated"), asi que sin este Clear
            // el registro seguia apuntando a los Vehicle/TurretWeapon/etc.
            // de la escena ANTERIOR, ya destruidos -- una referencia
            // "fake-null" de Unity. FindVehicleContaining (poseer a un
            // montado) y FindTheVehicle ([U]/[I]) fallaban en silencio
            // porque el vehiculo que buscaban no era el de la escena
            // recien construida.
            SP.Core.WorldSystemsRegistry.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildGround();
            BuildObstacles();
            BuildLightProps();
            SP.Presentation.OrderMarkerFx.Prewarm();
            SP.Presentation.AttackLineManager.Prewarm();
            SP.Presentation.OrderLineManager.Prewarm();

            var soldierPrefab = BuildAndSaveSoldierPrefab();
            var projectilePrefab = BuildAndSaveProjectilePrefab();
            var vehiclePrefab = BuildAndSaveVehiclePrefab();
            var colorVehicle = new Color(0.98f, 0.65f, 0.15f);

            var poolGO = new GameObject("ProjectilePool");
            var pool = poolGO.AddComponent<ProjectilePool>();
            // 229: el 24 era una constante magica. Ahora se deriva de
            // unidades x cadencia x vida del proyectil, que es lo que
            // realmente determina cuantos hay en vuelo a la vez.
            pool.Configure(projectilePrefab, SP.Combat.ProjectilePool.RecommendedPrewarm(12, 3f, 3f));

            var colorVega = new Color(0.95f, 0.35f, 0.30f);
            var colorKes = new Color(0.62f, 0.52f, 0.95f);
            var colorDoc = new Color(0.30f, 0.85f, 0.55f);
            var colorEnemy = new Color(0.95f, 0.25f, 0.20f);

            var vega = SpawnSoldier(soldierPrefab, "Soldado_1_Vega", TeamId.Player, RoleType.Assault, new Vector3(0, 0.8f, 0), colorVega, pool, 100);
            var kes = SpawnSoldier(soldierPrefab, "Soldado_2_Kes", TeamId.Player, RoleType.Sniper, new Vector3(2f, 0.8f, -1.5f), colorKes, pool, 100);
            var doc = SpawnSoldier(soldierPrefab, "Soldado_3_Doc", TeamId.Player, RoleType.Medic, new Vector3(-2f, 0.8f, -1.5f), colorDoc, pool, 100);
            var squad = new List<Soldier> { vega, kes, doc };

            var vehicle = SpawnVehicle(vehiclePrefab, new Vector3(6f, 0.6f, -4f), colorVehicle, pool);
            var weaponPickups = BuildWeaponPickups();
            var patrolEnemies = BuildPatrolEnemies(soldierPrefab, pool, colorEnemy);

            var camGO = new GameObject("MainCamera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.91f, 0.96f);
            camGO.AddComponent<AudioListener>();
            // La camara se creaba con AddComponent<Camera> pelado, sin
            // UniversalAdditionalCameraData, o sea con el post-procesado
            // apagado. Los items 176 y 178 lo necesitan; PostFxDirector se
            // encarga de que encenderlo no cambie el look (ver su cabecera).
            SP.Presentation.PostFxDirector.EnableOnCamera(cam);
            var rig = camGO.AddComponent<CameraRig>();
            rig.SetCamera(cam);
            rig.SetMode(ControlMode.Fps);

            var servicesGO = new GameObject("GameServices");
            var playerBrain = servicesGO.AddComponent<PlayerBrain>();
            var aimTargeting = servicesGO.AddComponent<AimTargeting>();
            var selection = servicesGO.AddComponent<SelectionController>();
            playerBrain.Possess(vega);
            rig.FollowFps(vega);

            BuildUI(squad, cam, playerBrain);

            var inputDriver = servicesGO.AddComponent<PlayerInputDriver>();
            inputDriver.Brain = playerBrain;
            inputDriver.Aim = aimTargeting;
            inputDriver.Rig = rig;
            inputDriver.Selection = selection;
            inputDriver.Squad = squad;
            inputDriver.AimUiRef = aimUiRef;
            inputDriver.Instructions = instructionUiRef;
            inputDriver.SelectionBox = selectionBoxRef;
            inputDriver.Vehicle = vehicle;
            inputDriver.WeaponPickups = weaponPickups;
            inputDriver.MinimapRef = minimapFollowRef;
            inputDriver.DeadNotice = deadNoticeRef;
            inputDriver.WeaponStatus = weaponStatusRef;
            inputDriver.VehicleStatus = vehicleStatusRef;
            inputDriver.DamageVignette = damageVignetteRef;
            inputDriver.PerfHud = perfHudRef;
            inputDriver.GroupCards = groupCardsRef;
            if (offscreenAlliesRef != null) offscreenAlliesRef.SetSquad(inputDriver.Squad);
            inputDriver.NavGraph = inputDriverNavGraph;
            inputDriver.TurretAim = turretAimRef;
            inputDriver.Outcome = outcomeControllerRef;
            inputDriver.PauseRef = pauseControllerRef;
            inputDriver.PlayerHealth = playerHealthRef;
            inputDriver.SelectionCount = selectionCountRef;
            inputDriver.ModeToast = modeToastRef;
            inputDriver.OrdenesMenu = ordenesMenuRef;
            servicesGO.AddComponent<WorldSimulationDriver>();
            servicesGO.AddComponent<SelectionRingManager>();
            var possessedMarker = servicesGO.AddComponent<PossessedMarkerView>();
            possessedMarker.SetInitial(playerBrain.Current);
            var killDirector = servicesGO.AddComponent<KillFeedbackDirector>();
            killDirector.Brain = playerBrain;
            killDirector.OffscreenMarker = offscreenKillRef;
            killDirector.Outcome = outcomeControllerRef;
            // El feed ya no se suscribe solo al bus: lo dispara el director
            // despues de actualizar su estado, para que el texto no salga
            // corrido una baja (ver comentario en KillFeedView.ShowKill).
            killDirector.Feed = killFeedRef;
            servicesGO.AddComponent<AttackLineManager>();
            servicesGO.AddComponent<OrderLineManager>();
            servicesGO.AddComponent<FloatingDamageTextManager>();
            servicesGO.AddComponent<PostFxDirector>();
            // EnsureVoices explicito: OnEnable no corre en Edit mode, y
            // sin el las 30 fuentes del pool no existirian mientras la
            // suite headless construye y simula la escena.
            servicesGO.AddComponent<AudioDirector>().EnsureVoices();
            servicesGO.AddComponent<WorldUiDirector>();

            // Grafo de navegacion + vista previa de ruta (items 218/226/227).
            // Los obstaculos ya estan en el registro, asi que el predicado de
            // bloqueo no necesita ningun barrido de escena.
            // EL GRAFO DEL JUEGO, no uno propio. Antes esto armaba un
            // WaypointGraph a mano con: limites de -90 a 90 (el cuadrado
            // que ya se corrigio en el resto del proyecto por no parecerse
            // al terreno), espaciado 4 (el juego usa 2) y un predicado de
            // bloqueo que solo conocia los ObstacleMarker por distancia.
            //
            // O sea que la suite validaba la vista previa de ruta contra un
            // grafo con otra resolucion, otros limites y otra idea de que
            // es un obstaculo que el que de verdad usan los soldados. El
            // test podia pasar con el sistema real roto, que es la peor
            // clase de test: da confianza sin cubrir nada.
            //
            // NO se construye la grilla aca: en este punto la escena esta a
            // medio armar y medirla daria un mundo del tamaño de lo que ya
            // exista (medido: devolvia el area del vehiculo, 2,2 x 3,6 m,
            // en una escena cuyo piso mide 160 x 160). Se invalida y se
            // deja que se construya sola la primera vez que alguien la
            // use, con la escena ya completa.
            SP.Core.ActorRegistry.Invalidate();
            SP.Core.NavService.Invalidate();
            var navGraph = SP.Core.NavService.Graph;
            var pathPreview = servicesGO.AddComponent<SP.Ai.PathPreview>();
            pathPreview.Attach(navGraph);
            inputDriverNavGraph = navGraph;
            // En Edit mode OnEnable no corre, asi que los vehiculos y
            // obstaculos no se dan de alta solos en el registro que
            // ahora usan los proyectiles. Sin esto la suite simularia
            // impactos contra un mundo sin vehiculos ni obstaculos.
            SP.Core.WorldSystemsRegistry.EnsurePopulated();
            var bootstrap = servicesGO.AddComponent<GameplaySceneBootstrap>();
            bootstrap.ObjectiveBanner = phaseBannerRef;
            bootstrap.ModeToast = modeToastRef;

            var battleManager = servicesGO.AddComponent<BattleManager>();
            battleManager.Squad = squad;
            battleManager.Enemies = patrolEnemies;
            battleManager.Outcome = outcomeControllerRef;

            if (missionStatusRef != null)
            {
                missionStatusRef.Squad = squad;
                missionStatusRef.Enemies = patrolEnemies;
                missionStatusRef.Refresh();
            }

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Ahora si: la escena esta completa, asi que la proxima consulta
            // mide el mundo entero y no un mundo a medio armar.
            SP.Core.NavService.Invalidate();
            SP.Core.ActorRegistry.Invalidate();

            TestLog.Step("Entorno de prueba construido: 3 soldados, vehiculo, armas, minimapa, camara y UI listos");

            if (runPhases)
            {
                RunPhase1(vega, kes, doc, pool, soldierPrefab, colorEnemy);
                RunPhase2(playerBrain, rig, aimTargeting, selection, vega, kes, doc, soldierPrefab, colorEnemy, pool);
                RunPhase3(playerBrain, rig, selection, aimTargeting, vega, kes, doc, soldierPrefab, colorEnemy, pool, vehicle);
                RunPhase4(playerBrain, rig, vehicle, weaponPickups, vega, kes, doc);
                RunPhase5(rig, vehicle, vega, kes, doc);
                RunPhase6(rig, vehicle, pool, vega, kes, doc);
                RunPhase7(inputDriver, vehicle, vega, kes, doc);
                RunPhase8(inputDriver, vehicle, vega, kes, doc, soldierPrefab, colorEnemy, pool);
                RunPhase9(inputDriver, vehicle, vega, kes, doc, soldierPrefab, colorEnemy, pool);

                // El cartel de "Felicidades, completaste la Fase N" se
                // queda ENGANCHADO visible para siempre si no se limpia
                // acá: PhaseBannerView.Show() solo arranca la corutina que
                // lo esconde a los N segundos cuando Application.isPlaying
                // es true, pero este test corre en Edit mode (isPlaying
                // false), así que la corutina nunca corre. El GameObject
                // queda con SetActive(true) grabado en la escena, y la
                // próxima vez que alguien le da Play a mano, hereda ese
                // estado y el cartel aparece trabado en pantalla desde el
                // primer frame, sin depender de si corrió el demo o no.
                // Se apaga el Text hijo (lo mismo que hace PunchAndHide al
                // final), NO el GameObject del propio PhaseBannerView: si
                // se apagara el contenedor, un StartCoroutine posterior de
                // verdad en Play mode fallaría por estar en un GameObject
                // inactivo.
                if (phaseBannerRef != null)
                {
                    var label = phaseBannerRef.GetComponentInChildren<Text>(true);
                    if (label != null) label.gameObject.SetActive(false);
                }
            }
            else
            {
                // Deja a los 3 soldados sanos, parados junto al vehiculo,
                // sin nadie montado, listo para probar a mano.
                vehicle.transform.position = new Vector3(6f, 0.6f, 4f);
                vehicle.transform.rotation = Quaternion.identity;
                vega.transform.position = vehicle.transform.position + new Vector3(-3.5f, 0f, 0f);
                kes.transform.position = vehicle.transform.position + new Vector3(-3.5f, 0f, 1.5f);
                doc.transform.position = vehicle.transform.position + new Vector3(-3.5f, 0f, -1.5f);
                rig.FollowFps(vega);

                // Enemigo pre-armado pero apagado: el AutoDemoRunner lo
                // activa en el momento justo de la Fase 1, sin tener que
                // instanciar un prefab en tiempo de ejecución (Play mode no
                // puede llamar a este script de Editor).
                var demoEnemy = SpawnSoldier(soldierPrefab, "Enemigo_Demo", TeamId.Enemy, RoleType.Enemy, new Vector3(0f, 0.8f, 12f), colorEnemy, pool, 180);
                demoEnemy.gameObject.SetActive(false);

                var runner = servicesGO.AddComponent<SP.Presentation.AutoDemoRunner>();
                runner.Brain = playerBrain;
                runner.Rig = rig;
                runner.Selection = selection;
                runner.Aim = aimTargeting;
                runner.InputDriver = inputDriver;
                runner.Squad = squad;
                runner.DemoVehicle = vehicle;
                runner.WeaponPickups = weaponPickups;
                runner.DemoEnemy = demoEnemy;
                runner.PatrolEnemies = patrolEnemies;

                // Ya NO se planta el boton "Test" en el canvas: era UI de
                // desarrollo colgada del mismo HUD que ve el jugador, y
                // terminaba dentro del build. La demo se dispara desde
                // afuera (StartDemo o "-autodemo"), no desde la pantalla.

                TestLog.Step("Demo lista: Vega junto al vehiculo, Kes y Doc cerca. AutoDemoRunner armado (se arranca con StartDemo o con -autodemo).");
            }

            EditorSceneManager.MarkSceneDirty(scene);

            // BUG REAL encontrado en testeo (no en las fases mismas, en
            // el propio runner): con runPhases=true, este segundo
            // SaveScene persistia al ARCHIVO en disco el estado en que
            // quedaban las fases -- soldados a mitad de combate, y peor,
            // a veces montados en el vehiculo. Vehicle.seats es un
            // Dictionary privado que no sobrevive el domain reload: un
            // soldado guardado montado (GameObject inactivo) quedaba
            // inactivo PARA SIEMPRE la proxima vez que alguien abriera
            // esta escena y le diera Play, porque el vehiculo ya no
            // recordaba tenerlo adentro para poder bajarlo. El primer
            // SaveScene (arriba, antes de correr las fases) ya dejo en
            // disco exactamente el estado inicial limpio que se queria
            // -- las fases son solo verificacion en memoria, no hace
            // falta volver a grabar lo que dejan a mitad de camino.
            if (!runPhases) EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---------------------------------------------------------------
        // FASE 1 · combate FPS básico
        // ---------------------------------------------------------------
        static void RunPhase1(Soldier vega, Soldier kes, Soldier doc, ProjectilePool pool, GameObject soldierPrefab, Color enemyColor)
        {
            TestLog.Phase("FASE 1 - Combate FPS basico");
            TestLog.Step("Inicio de partida");

            Vector3 startPos = vega.transform.position;
            for (int i = 0; i < 10; i++) vega.Motor.Move(Vector3.forward, 0.05f);
            bool moved = Vector3.Distance(startPos, vega.transform.position) > 0.1f;
            Check($"Movimiento verificado contra transform de {vega.DisplayName} (WASD), desplazamiento {(vega.transform.position - startPos).magnitude:0.00} m", moved);

            int freeBefore = pool.FreeCount;
            bool fired = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
            Check($"Disparo: se creo proyectil de {vega.DisplayName} con exito (click)", fired);

            SimulateSeconds(3.2f);
            Check("El proyectil volvio al pool", pool.FreeCount == freeBefore);

            var enemy1 = SpawnSoldier(soldierPrefab, "Enemigo_1", TeamId.Enemy, RoleType.Enemy, vega.transform.position + vega.transform.forward * 15f + Vector3.up * 0.8f, enemyColor, pool, 180);
            TestLog.Step($"Aparecio enemigo: {enemy1.DisplayName}");

            Vector3 dirToEnemy = (enemy1.transform.position - vega.transform.position).normalized;
            bool fired2 = vega.Weapon.TryFire(vega.transform.position, dirToEnemy);
            Check($"Disparo de nuevo: proyectil de {vega.DisplayName} en camino hacia {enemy1.DisplayName}", fired2);

            bool tookDamage = SimulateUntil(() => enemy1.Health.Current < enemy1.Health.MaxHealth, 2f);
            Check($"Proyectil de {vega.DisplayName} hizo dano a {enemy1.DisplayName} ({enemy1.Health.Current}/{enemy1.Health.MaxHealth} vida)", tookDamage);

            var enemyBrain = enemy1.GetComponent<AiBrain>();
            bool chasing = SimulateUntil(() => enemyBrain.State == AiState.Chase, 2f);
            Check($"{enemy1.DisplayName} cambio de estado a: Perseguir a {vega.DisplayName}", chasing);

            bool attacking = SimulateUntil(() => enemyBrain.State == AiState.Attack, 6f);
            Check($"{enemy1.DisplayName} se acerco lo suficiente y empezo a atacar", attacking);

            int vegaHpBefore = vega.Health.Current;
            bool enemyFired = SimulateUntil(() => vega.Health.Current < vegaHpBefore, 2f);
            Check($"Creando proyectil de {enemy1.DisplayName}: impacto a {vega.DisplayName} ({vega.Health.Current}/{vega.Health.MaxHealth} vida)", enemyFired);

            var kesBrain = kes.GetComponent<AiBrain>();
            var docBrain = doc.GetComponent<AiBrain>();
            bool allyAlerted = SimulateUntil(() => kesBrain.State == AiState.Chase || docBrain.State == AiState.Chase, 2f);
            string alertedName = kesBrain.State == AiState.Chase ? kes.DisplayName : (docBrain.State == AiState.Chase ? doc.DisplayName : "ninguno");
            Check($"{alertedName} se entero de que {vega.DisplayName} esta siendo atacado: atacara a {enemy1.DisplayName}", allyAlerted);

            bool enemyDied = SimulateUntil(() => !enemy1.Health.IsAlive, 8f);
            Check($"{enemy1.DisplayName} cayo", enemyDied);
            TestLog.Step($"Animacion y efecto de caida en {enemy1.transform.position}");

            TestLog.Phase("FASE 1 FINALIZADA");
            phaseBannerRef?.Show("Felicidades!\nTerminaste la Fase 1.\nAhora viene la Fase 2");
        }

        // ---------------------------------------------------------------
        // FASE 2 · posesion, camara y ordenes
        // ---------------------------------------------------------------
        static void RunPhase2(PlayerBrain brain, CameraRig rig, AimTargeting aim, SelectionController selection,
            Soldier vega, Soldier kes, Soldier doc, GameObject soldierPrefab, Color enemyColor, ProjectilePool pool)
        {
            TestLog.Phase("FASE 2 - Posesion, camara y ordenes");
            FullHeal(vega, kes, doc);

            brain.RotateYaw(15f);
            rig.FollowFps(brain.Current);
            bool camFollows = Vector3.Distance(rig.transform.position, brain.Current.EyeAnchor.position) < 0.01f;
            Check("Movimiento de camara verificado (sigue al soldado poseido)", camFollows);

            Ray rayToKes = new Ray(vega.transform.position + Vector3.up * 0.5f, kes.transform.position - vega.transform.position);
            var resultAlly = aim.Evaluate(rayToKes, vega);
            Check($"Apuntando... {(resultAlly.Type == AimTargetType.Ally ? resultAlly.Soldier.DisplayName + " en la mira" : "nada en la mira")}", resultAlly.Type == AimTargetType.Ally && resultAlly.Soldier == kes);

            aimUiRef.UpdateFromAimResult(resultAlly);
            Check("Se activo la UI de apuntado con exito", aimUiRef.IsVisible && aimUiRef.CurrentPrompt.Contains(kes.DisplayName));

            TestLog.Step("Se simula apretar F");
            PossessionService.Swap(brain, kes);
            Check($"Cambio a soldado con exito. El soldado actual es {brain.Current.DisplayName}", brain.Current == kes);

            Vector3 vegaPosBeforeMove = vega.transform.position;
            Vector3 kesPosBefore = kes.transform.position;
            TestLog.Step("Probando moverse");
            for (int i = 0; i < 10; i++) brain.Move(kes.transform.forward, 0.05f);
            bool kesMoved = Vector3.Distance(kesPosBefore, kes.transform.position) > 0.1f;
            bool vegaUnaffected = Vector3.Distance(vegaPosBeforeMove, vega.transform.position) < 0.001f;
            Check($"{kes.DisplayName} se movio con exito", kesMoved);

            Quaternion rotBefore = kes.transform.rotation;
            TestLog.Step("Probando rotar");
            brain.RotateYaw(45f);
            Check($"{kes.DisplayName} se movio y roto con exito. No afecto al soldado anterior ({vega.DisplayName})", rotBefore != kes.transform.rotation && vegaUnaffected);

            Check("UI validada: se resalta al soldado seleccionado", rosterUiRef.IsHighlighted(kes.Id));

            Vector3 targetGroundPoint = kes.transform.position + kes.transform.forward * 6f;
            targetGroundPoint.y = 0f;
            Ray rayToGround = new Ray(targetGroundPoint + Vector3.up * 5f, Vector3.down);
            var resultGround = aim.Evaluate(rayToGround, kes);
            Check("Se apunto al suelo", resultGround.Type == AimTargetType.Ground);

            aimUiRef.UpdateFromAimResult(resultGround);
            Check($"Aparecio UI: \"{aimUiRef.CurrentPrompt}\" (tecla T para ir a esa posicion)", aimUiRef.CurrentPrompt.Contains("T"));

            TestLog.Step("Se simula apretar T");
            var nearestFree = OrderService.FindNearestFreeAlly(resultGround.Point, TeamId.Player, kes);
            OrderService.IssueMoveOrder(nearestFree, resultGround.Point);
            var nfBrain = nearestFree.GetComponent<AiBrain>();
            Check($"El soldado mas cercano ({nearestFree.DisplayName}) cambio de estado a: Ir a objetivo", nfBrain.State == AiState.MovingToOrder);

            bool arrived = SimulateUntil(() => nfBrain.State != AiState.MovingToOrder, 6f);
            Check($"{nearestFree.DisplayName} llego al objetivo", arrived);

            var enemy2 = SpawnSoldier(soldierPrefab, "Enemigo_2", TeamId.Enemy, RoleType.Enemy, nearestFree.transform.position + Vector3.forward * 16f + Vector3.up * 0.8f, enemyColor, pool, 150);
            OrderService.IssueAttackOrder(nearestFree, enemy2);
            TestLog.Step($"Se dio la orden de atacar a {enemy2.DisplayName}");
            Check($"{nearestFree.DisplayName} cambio de estado a: Yendo", nfBrain.State == AiState.MovingToAttackOrder || nfBrain.State == AiState.Attack);

            bool nowAttacking = SimulateUntil(() => nfBrain.State == AiState.Attack, 4f);
            Check($"{nearestFree.DisplayName} cambio de estado a: Atacando", nowAttacking);

            bool resolved = SimulateUntil(() => !enemy2.Health.IsAlive || !nearestFree.Health.IsAlive, 10f);
            string winner = !enemy2.Health.IsAlive ? nearestFree.DisplayName : enemy2.DisplayName;
            string loser = !enemy2.Health.IsAlive ? enemy2.DisplayName : nearestFree.DisplayName;
            Check($"Gano {winner} sobre {loser} -- {nearestFree.DisplayName} quedo con {nearestFree.Health.Current} vida, {enemy2.DisplayName} con {enemy2.Health.Current} vida", resolved);

            TestLog.Phase("FASE 2 FINALIZADA");
            phaseBannerRef?.Show("Felicidades!\nTerminaste la Fase 2.\nAhora viene la Fase 3");
        }

        // ---------------------------------------------------------------
        // FASE 3 · vista RTS y seleccion multiple
        // ---------------------------------------------------------------
        static void RunPhase3(PlayerBrain brain, CameraRig rig, SelectionController selection, AimTargeting aim,
            Soldier vega, Soldier kes, Soldier doc, GameObject soldierPrefab, Color enemyColor, ProjectilePool pool, Vehicle vehicle)
        {
            TestLog.Phase("FASE 3 - Vista RTS y seleccion multiple");
            FullHeal(vega, kes, doc);

            TestLog.Step("Se apreto TAB");
            rig.ToggleMode();
            Check("Se cambio a vista RTS", rig.Mode == ControlMode.Rts);
            rig.SetRtsView(vega.transform.position);

            Ray rayDownToVega = new Ray(vega.transform.position + Vector3.up * 20f, Vector3.down);
            var res = aim.Evaluate(rayDownToVega, null);
            Check($"Se apunto a {(res.Soldier != null ? res.Soldier.DisplayName : "nadie")}", res.Type == AimTargetType.Ally && res.Soldier == vega);

            selection.SelectSingle(vega);
            Check("Feedback de seleccion funciono", selection.Selected.Count == 1 && selection.Selected[0] == vega);

            selection.AddToSelection(kes);
            Check("Se seleccionaron 2 soldados", selection.Selected.Count == 2);

            Vector3 dest = vega.transform.position + new Vector3(10f, 0f, 10f);
            OrderService.IssueMoveOrderForSelection(selection.Selected, dest);
            TestLog.Step($"Se dio la orden de ir a {dest}");

            var enemy3 = SpawnSoldier(soldierPrefab, "Enemigo_3", TeamId.Enemy, RoleType.Enemy, dest + new Vector3(3f, 0.8f, 0f), enemyColor, pool, 120);
            var vegaBrain = vega.GetComponent<AiBrain>();
            bool detected = SimulateUntil(() => vegaBrain.State == AiState.Chase || vegaBrain.State == AiState.Attack, 6f);
            Check($"{vega.DisplayName} detecto que hay un enemigo cerca", detected);

            bool attacking3 = SimulateUntil(() => vegaBrain.State == AiState.Attack, 4f);
            Check($"{vega.DisplayName} cambio a estado: Atacando", attacking3);

            bool resolved3 = SimulateUntil(() => !enemy3.Health.IsAlive || !vega.Health.IsAlive, 10f);
            string w3 = !enemy3.Health.IsAlive ? vega.DisplayName : enemy3.DisplayName;
            string l3 = !enemy3.Health.IsAlive ? enemy3.DisplayName : vega.DisplayName;
            Check($"{w3} vencio a {l3}", resolved3);

            TestLog.Step($"Probando vehiculo: orden de subir a bordo ({vehicle.name})");
            vehicle.transform.position = dest + new Vector3(-6f, -0.2f, 0f);
            // Doc está libre en este punto (Kes es la poseída desde la Fase
            // 2: un soldado poseído no ejecuta órdenes de IA, por diseño).
            OrderService.IssueMountOrder(doc, vehicle);
            bool mounted = SimulateUntil(() => vehicle.Occupants.Count > 0, 12f);
            Check($"{doc.DisplayName} subio al vehiculo (ocupantes {vehicle.Occupants.Count}/{vehicle.Capacity})", mounted);

            // Doc quedó adentro del vehículo (oculto): el siguiente swap usa
            // a Vega, que está libre y ya resolvió su combate.
            selection.SelectSingle(vega);
            Check("Se selecciono 1 solo soldado", selection.Selected.Count == 1 && selection.Selected[0] == vega);

            TestLog.Step("Se aprieta la tecla F");
            PossessionService.Swap(brain, vega);
            Check($"Se cambio a soldado {brain.Current.DisplayName}", brain.Current == vega);

            rig.ToggleMode();
            Check("Volvio a vista FPS", rig.Mode == ControlMode.Fps);
            rig.FollowFps(vega);

            TestLog.Step("Probando movimiento de camara y personaje");
            Vector3 finalBefore = vega.transform.position;
            for (int i = 0; i < 8; i++) brain.Move(vega.transform.forward, 0.05f);
            Quaternion finalRotBefore = vega.transform.rotation;
            brain.RotateYaw(30f);
            Check("Todo validado con exito", Vector3.Distance(finalBefore, vega.transform.position) > 0.05f && finalRotBefore != vega.transform.rotation);

            TestLog.Phase("FASE 3 FINALIZADA");
            phaseBannerRef?.Show("Felicidades!\nTerminaste la Fase 3.\nAhora viene la Fase 4");
        }

        // ---------------------------------------------------------------
        // FASE 4 · vehiculo: asientos, torreta, conduccion y armas
        // ---------------------------------------------------------------
        static void RunPhase4(PlayerBrain brain, CameraRig rig, Vehicle vehicle, List<WeaponPickup> pickups,
            Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 4 - Vehiculo, torreta y armas");
            FullHeal(vega, kes, doc);

            // Estado limpio: nadie debe seguir montado de una fase anterior
            // (un soldado inactivo no recibe ticks y su enfriamiento nunca bajaria).
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);

            var motor = vehicle.GetComponent<VehicleMotor>();
            var vBrain = vehicle.GetComponent<VehicleBrain>();
            var turret = vehicle.GetComponentInChildren<TurretWeapon>();

            // Reubicamos todo cerca para que la prueba sea determinista.
            vehicle.transform.position = new Vector3(20f, 0.6f, 20f);
            vehicle.transform.rotation = Quaternion.identity;
            vega.transform.position = vehicle.transform.position + new Vector3(-4f, 0f, 0f);
            kes.transform.position = vehicle.transform.position + new Vector3(-4f, 0f, 1.5f);
            doc.transform.position = vehicle.transform.position + new Vector3(20f, 0f, 20f); // lejos: no debe subir sola

            TestLog.Step("Simulando acercarse y apretar E: Vega sube de conductor");
            bool mounted = vehicle.Mount(vega, VehicleSeatRole.Driver);
            Check($"Vega subio como conductor (ocupantes {vehicle.OccupantCount}/{vehicle.Capacity})", mounted);

            bool kesAutoMounted = Vector3.Distance(kes.transform.position, vehicle.transform.position) <= 6f && vehicle.Mount(kes);
            Check($"Kes (aliada cercana) subio automaticamente al acercarse Vega (asiento: {vehicle.RoleOf(kes)})", kesAutoMounted);

            Check("Doc (lejos) NO subio automaticamente", vehicle.RoleOf(doc) == null);

            TestLog.Step("Probando aceleracion: manteniendo W");
            float speedBefore = motor.CurrentSpeed;
            Vector3 posBefore = vehicle.transform.position;
            for (int i = 0; i < 30; i++) motor.Drive(1f, 0f, 0.05f);
            Check($"El vehiculo acelero de {speedBefore:0.0} a {motor.CurrentSpeed:0.0} u/s y avanzo {Vector3.Distance(posBefore, vehicle.transform.position):0.00} m", motor.CurrentSpeed > speedBefore && motor.CurrentSpeed > 0f);

            TestLog.Step("Probando frenado (tecla G)");
            for (int i = 0; i < 40; i++) motor.Brake(0.05f);
            Check($"El vehiculo freno hasta detenerse (velocidad {motor.CurrentSpeed:0.00})", motor.IsStopped);

            TestLog.Step("Vega aprieta 2: pasa a la torreta, Kes (dentro) queda como unica ocupante para conducir");
            vehicle.Dismount(vega);
            vega.gameObject.SetActive(false);
            vehicle.Mount(vega, VehicleSeatRole.Gunner);
            Check($"Vega ahora es artillero (asiento: {vehicle.RoleOf(vega)})", vehicle.RoleOf(vega) == VehicleSeatRole.Gunner);

            TestLog.Step("Probando torreta: disparo");
            bool turretFired = turret.TryFire();
            Check("La torreta disparo un proyectil", turretFired);

            TestLog.Step("Orden por clic derecho: la camioneta va sola a un punto");
            Vector3 dest = vehicle.transform.position + new Vector3(15f, 0f, 0f);
            vBrain.IsPlayerDriving = false;
            vBrain.IssueMoveOrder(dest);
            bool arrived = false;
            for (int i = 0; i < 200; i++)
            {
                vBrain.Tick(0.05f);
                turret.Tick(0.05f);
                if (!vBrain.HasOrder) { arrived = true; break; }
            }
            Check($"La camioneta condujo sola hasta el punto pedido (distancia final {Vector3.Distance(vehicle.transform.position, dest):0.00} m)", arrived);

            TestLog.Step("Probando las 3 armas recogibles: equipar y disparar cada una");
            foreach (var pickup in pickups)
            {
                doc.transform.position = pickup.transform.position;
                pickup.EquipOn(doc.Weapon, doc.Id);
                bool fired = doc.Weapon.TryFire(doc.transform.position, doc.transform.forward);
                Check($"Arma {pickup.Kind} (color {pickup.Color}) equipada y disparada por {doc.DisplayName}", fired);
                SimulateSeconds(0.5f);
            }

            TestLog.Phase("FASE 4 FINALIZADA");
            phaseBannerRef?.Show("Felicidades!\nCompletaste las 4 fases.", 3f);
        }

        // ---------------------------------------------------------------
        // FASE 5 - Regresion de sistemas nuevos (item 2 del plan de cierre).
        // Casi todo lo agregado esta ultima tanda se diseño con funciones
        // ESTATICAS PURAS justamente para poder testearlas sin Play mode --
        // estaban escritas pero nadie las llamaba desde la suite. Esta fase
        // las llama a todas, con Check() (que ahora corta la suite de
        // verdad, ver el commit "La suite ahora falla de verdad").
        // ---------------------------------------------------------------
        static void RunPhase5(CameraRig rig, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 5 - Regresion de sistemas nuevos");

            // --- AudioDirector.SelectVictim: robo de voces por audibilidad ---
            var v0 = new SP.Presentation.VoiceState[3];
            v0[0] = new SP.Presentation.VoiceState { Free = true };
            Check("SelectVictim elige la primera voz LIBRE",
                SP.Presentation.AudioDirector.SelectVictim(v0, 0.5f, 10f) == 0);

            var v1 = new SP.Presentation.VoiceState[2];
            v1[0] = new SP.Presentation.VoiceState { Free = false, Audibility = 0.9f, ExpiresAt = 100f };
            v1[1] = new SP.Presentation.VoiceState { Free = false, Audibility = 5f, ExpiresAt = 1f };
            Check("SelectVictim elige la voz VENCIDA (ExpiresAt < now) aunque sea mas audible",
                SP.Presentation.AudioDirector.SelectVictim(v1, 0.5f, 10f) == 1);

            var v2 = new SP.Presentation.VoiceState[2];
            v2[0] = new SP.Presentation.VoiceState { Free = false, Audibility = 0.9f, ExpiresAt = 100f };
            v2[1] = new SP.Presentation.VoiceState { Free = false, Audibility = 0.2f, ExpiresAt = 100f };
            Check("SelectVictim roba la voz de MENOR audibilidad cuando la nueva le gana",
                SP.Presentation.AudioDirector.SelectVictim(v2, 0.5f, 10f) == 1);
            Check("SelectVictim descarta (-1) si la nueva NO le gana a la peor voz",
                SP.Presentation.AudioDirector.SelectVictim(v2, 0.1f, 10f) == -1);
            Check("SelectVictim descarta (-1) en empate exacto (no corta un sonido a la mitad para nada)",
                SP.Presentation.AudioDirector.SelectVictim(v2, 0.2f, 10f) == -1);
            Check("SelectVictim con array vacio devuelve -1",
                SP.Presentation.AudioDirector.SelectVictim(new SP.Presentation.VoiceState[0], 1f, 0f) == -1);

            // --- AudioDirector.Attenuation / CutoffFor: monotonia ---
            Check("Attenuation es 1 adentro de MinDistance",
                Mathf.Approximately(SP.Presentation.AudioDirector.Attenuation(2f), 1f));
            Check("Attenuation es 0 mas alla de MaxDistance",
                Mathf.Approximately(SP.Presentation.AudioDirector.Attenuation(500f), 0f));
            bool attenNoCreciente = true, cutoffEstrictamenteDecreciente = true;
            float prevAtten = SP.Presentation.AudioDirector.Attenuation(0f);
            float prevCutoff = SP.Presentation.AudioDirector.CutoffFor(0f);
            // CutoffFor solo promete ser estrictamente decreciente DENTRO de
            // [0, MaxDistance] (90) -- mas alla, Clamp01 aplana k a 1 a
            // proposito, asi que dos distancias mas alla del maximo dan el
            // mismo corte. Probar hasta 90 y no mas, o el test reporta una
            // falla que en realidad es del propio test, no del codigo.
            for (float d = 5f; d <= SP.Presentation.AudioDirector.MaxDistance; d += 5f)
            {
                float atten = SP.Presentation.AudioDirector.Attenuation(d);
                float cutoff = SP.Presentation.AudioDirector.CutoffFor(d);
                if (atten > prevAtten + 0.0001f) attenNoCreciente = false;
                if (cutoff >= prevCutoff - 0.0001f) cutoffEstrictamenteDecreciente = false;
                prevAtten = atten; prevCutoff = cutoff;
            }
            Check("Attenuation es monotona NO creciente con la distancia", attenNoCreciente);
            Check("CutoffFor es estrictamente decreciente con la distancia", cutoffEstrictamenteDecreciente);

            // --- AudioDirector.NextPitch: rango y variacion ---
            bool pitchEnRango = true, pitchVaria = false;
            float firstPitch = SP.Presentation.AudioDirector.NextPitch();
            for (int i = 0; i < 20; i++)
            {
                float p = SP.Presentation.AudioDirector.NextPitch();
                if (p < SP.Presentation.AudioDirector.MinPitch || p > SP.Presentation.AudioDirector.MaxPitch) pitchEnRango = false;
                if (!Mathf.Approximately(p, firstPitch)) pitchVaria = true;
            }
            Check("NextPitch siempre cae en [MinPitch, MaxPitch]", pitchEnRango);
            Check("NextPitch varia entre llamadas (no es una constante)", pitchVaria);

            // --- AudioDirector.GainFor/SetGain: canales independientes ---
            SP.Presentation.AudioDirector.SetGain(SP.Presentation.SfxChannel.Sfx, 1f);
            SP.Presentation.AudioDirector.SetGain(SP.Presentation.SfxChannel.Ui, 1f);
            SP.Presentation.AudioDirector.SetGain(SP.Presentation.SfxChannel.Sfx, 0.3f);
            Check("Bajar la ganancia de Sfx NO afecta a Ui",
                Mathf.Approximately(SP.Presentation.AudioDirector.GainFor(SP.Presentation.SfxChannel.Sfx), 0.3f)
                && Mathf.Approximately(SP.Presentation.AudioDirector.GainFor(SP.Presentation.SfxChannel.Ui), 1f));
            SP.Presentation.AudioDirector.SetGain(SP.Presentation.SfxChannel.Sfx, 1f);

            // --- WaypointGraph / FlowField: rodea obstaculos, no se cuelga ---
            var wallGraph = new SP.Core.WaypointGraph();
            wallGraph.Build(new Vector3(-20f, 0f, -20f), new Vector3(20f, 0f, 20f), 2f,
                p => Mathf.Abs(p.x) < 1.5f && p.z < 10f);
            var wallPath = new List<Vector3>();
            bool wallFound = wallGraph.TryFindPath(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), wallPath);
            Check("WaypointGraph encuentra camino rodeando un muro con hueco", wallFound && wallPath.Count > 2);

            var openGraph = new SP.Core.WaypointGraph();
            openGraph.Build(new Vector3(-20f, 0f, -20f), new Vector3(20f, 0f, 20f), 2f, p => false);
            var openPath = new List<Vector3>();
            openGraph.TryFindPath(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), openPath);
            float openLen = 0f;
            for (int i = 1; i < openPath.Count; i++) openLen += Vector3.Distance(openPath[i - 1], openPath[i]);
            Check($"Sin obstaculos el camino ES la linea recta (largo {openLen:0.0} ~= 20.0)", Mathf.Abs(openLen - 20f) < 2.5f);

            var blockedGraph = new SP.Core.WaypointGraph();
            blockedGraph.Build(new Vector3(-20f, 0f, -20f), new Vector3(20f, 0f, 20f), 2f, p => Mathf.Abs(p.x) < 1.5f);
            var blockedPath = new List<Vector3>();
            bool blockedFound = blockedGraph.TryFindPath(new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f), blockedPath);
            Check("Con destino inalcanzable TryFindPath devuelve false (no se cuelga)", !blockedFound);

            var flow = new SP.Core.FlowField();
            flow.Attach(wallGraph);
            flow.Compute(new Vector3(10f, 0f, 0f));
            Check("FlowField.IsReachable es true del lado alcanzable del muro con hueco",
                flow.IsReachable(new Vector3(-10f, 0f, 0f)));

            // --- OrderService.FormationPoints: geometria por tipo ---
            var linea = SP.Player.OrderService.FormationPoints(Vector3.zero, Vector3.forward, 5, SP.Player.FormationKind.Linea);
            bool lineaSinProfundidad = true;
            foreach (var p in linea) if (Mathf.Abs(p.z) > 0.01f) lineaSinProfundidad = false;
            Check("Formacion Linea: todos los puntos a la misma profundidad", lineaSinProfundidad);

            var columna = SP.Player.OrderService.FormationPoints(Vector3.zero, Vector3.forward, 5, SP.Player.FormationKind.Columna);
            bool columnaSinLateral = true;
            foreach (var p in columna) if (Mathf.Abs(p.x) > 0.01f) columnaSinLateral = false;
            Check("Formacion Columna: todos los puntos sobre el mismo eje (sin variacion lateral)", columnaSinLateral);

            // --- OrderService.SpreadOf: dispersion baja con formacion apretada ---
            var disperso = new List<Vector3>();
            var rngSpread = new System.Random(42);
            for (int i = 0; i < 20; i++)
                disperso.Add(new Vector3((float)(rngSpread.NextDouble() * 80 - 40), 0f, (float)(rngSpread.NextDouble() * 80 - 40)));
            float spreadDisperso = SP.Player.OrderService.SpreadOf(disperso);
            var apretado = new List<Vector3>(SP.Player.OrderService.FormationPoints(Vector3.zero, Vector3.forward, 20, SP.Player.FormationKind.Cuadricula));
            float spreadApretado = SP.Player.OrderService.SpreadOf(apretado);
            Check($"SpreadOf de una formacion apretada ({spreadApretado:0.0}) es menor que uno disperso ({spreadDisperso:0.0})",
                spreadApretado < spreadDisperso);

            // --- SelectionController.IsWounded: casos de borde ---
            Check("IsWounded con max<=0 no divide por cero (false)", !SelectionController.IsWounded(10, 0, 0.5f));
            Check("IsWounded con vida al umbral exacto es false (estricto)", !SelectionController.IsWounded(50, 100, 0.5f));
            Check("IsWounded por debajo del umbral es true", SelectionController.IsWounded(30, 100, 0.5f));
            Check("IsWounded con vida llena es false", !SelectionController.IsWounded(100, 100, 0.5f));

            // --- CameraRig: presupuesto de sacudida, interruptor, balanceo ---
            if (rig != null)
            {
                bool fxWasEnabled = SP.CameraSystem.CameraFxSettings.Enabled;
                SP.CameraSystem.CameraFxSettings.Enabled = true;
                for (int i = 0; i < 10; i++) rig.KickDirectional(Vector3.forward, 1f);
                Check($"10 sacudidas de magnitud 1 quedan acotadas al tope ({rig.ShakeOffset.magnitude:0.000} <= {rig.MaxShakeMagnitude})",
                    rig.ShakeOffset.magnitude <= rig.MaxShakeMagnitude + 0.001f);

                SP.CameraSystem.CameraFxSettings.Enabled = false;
                var dummyRig = new GameObject("DummyRig").AddComponent<CameraRig>();
                Vector3 shakeBeforeOff = dummyRig.ShakeOffset;
                dummyRig.KickDirectional(Vector3.forward, 1f);
                Vector3 shakeAfterOff = dummyRig.ShakeOffset;
                Check("Con efectos de camara apagados, KickDirectional no acumula nada nuevo",
                    shakeAfterOff == shakeBeforeOff);
                UnityEngine.Object.DestroyImmediate(dummyRig.gameObject);
                SP.CameraSystem.CameraFxSettings.Enabled = fxWasEnabled;
            }

            // --- KeyBindings: set -> persiste -> reset ---
            SP.Player.KeyBindings.ResetToDefaults();
            var teclaOriginal = SP.Player.KeyBindings.Get(SP.Player.KeyBindings.Recargar);
            SP.Player.KeyBindings.Set(SP.Player.KeyBindings.Recargar, UnityEngine.InputSystem.Key.U);
            SP.Player.KeyBindings.InvalidateCache();
            bool persistio = SP.Player.KeyBindings.Get(SP.Player.KeyBindings.Recargar) == UnityEngine.InputSystem.Key.U;
            SP.Player.KeyBindings.ResetToDefaults();
            bool volvioAlDefault = SP.Player.KeyBindings.Get(SP.Player.KeyBindings.Recargar) == teclaOriginal;
            Check("KeyBindings: set sobrevive InvalidateCache (persiste en PlayerPrefs)", persistio);
            Check("KeyBindings: ResetToDefaults vuelve al valor de fabrica", volvioAlDefault);

            // --- ControlsTable: los 6 contextos tienen atajos, nada vacio ---
            string controlsProblem;
            Check("ControlsTable.Validate no encuentra huecos", SP.UI.ControlsTable.Validate(out controlsProblem));

            // --- GroupCardsView.Summarize: vacio y con muertos ---
            SP.UI.GroupCardsView.Summarize(null, out int vivosNull, out float vidaNull);
            Check("GroupCardsView.Summarize con grupo null da 0 vivos", vivosNull == 0);
            var grupoConMuerto = new List<Soldier> { vega };
            bool vegaEstabaViva = vega.Health.IsAlive;
            vega.Health.TakeDamage(999999, -1);
            SP.UI.GroupCardsView.Summarize(grupoConMuerto, out int vivosTrasMorir, out float vidaTrasMorir);
            Check("GroupCardsView.Summarize no cuenta a los muertos", vivosTrasMorir == 0);
            // FullHeal restaura a Vega para que las fases futuras (si las hubiera) no la hereden muerta.
            FullHeal(vega, kes, doc);

            // --- AlertQueue.SelectNext: prioridad y FIFO ---
            var pending = new SP.UI.PendingAlert[3];
            pending[0] = new SP.UI.PendingAlert { Message = "baja", Priority = SP.UI.AlertPriority.Baja, Seconds = 1f, EnqueuedAt = 0f };
            pending[1] = new SP.UI.PendingAlert { Message = "critica", Priority = SP.UI.AlertPriority.Critica, Seconds = 1f, EnqueuedAt = 1f };
            pending[2] = new SP.UI.PendingAlert { Message = "media", Priority = SP.UI.AlertPriority.Media, Seconds = 1f, EnqueuedAt = 0.5f };
            Check("AlertQueue.SelectNext elige la de MAYOR prioridad sin importar el orden de llegada",
                SP.UI.AlertQueue.SelectNext(pending, 10f) == 1);

            var empatados = new SP.UI.PendingAlert[2];
            empatados[0] = new SP.UI.PendingAlert { Message = "primera", Priority = SP.UI.AlertPriority.Media, Seconds = 1f, EnqueuedAt = 5f };
            empatados[1] = new SP.UI.PendingAlert { Message = "segunda", Priority = SP.UI.AlertPriority.Media, Seconds = 1f, EnqueuedAt = 2f };
            Check("AlertQueue.SelectNext en empate de prioridad elige FIFO (el que se encolo primero)",
                SP.UI.AlertQueue.SelectNext(empatados, 10f) == 1);

            var vencida = new SP.UI.PendingAlert[1];
            vencida[0] = new SP.UI.PendingAlert { Message = "vieja", Priority = SP.UI.AlertPriority.Alta, Seconds = 1f, EnqueuedAt = 0f };
            Check("AlertQueue.SelectNext ignora un aviso vencido", SP.UI.AlertQueue.SelectNext(vencida, 10f) == -1);

            // --- Projectile contra vehiculo via WorldSystemsRegistry ---
            if (vehicle != null)
            {
                int vidaAntes = vehicle.Health.Current;
                vehicle.TakeDamage(15, -1);
                Check($"Projectile/registro: el vehiculo sigue recibiendo daño real ({vidaAntes} -> {vehicle.Health.Current})",
                    vehicle.Health.Current < vidaAntes);
            }

            // --- Items 39, 42, 65, 94 del backlog: verificables sin
            // Play mode (no dependen de una corrutina ni de un OnDamage
            // gateado por Application.isPlaying). Los que SI necesitan
            // Play mode real (15, 34, 38, 52, 63, 66, 68, 95) quedan en
            // RunPlayModeProbe(), documentado y corrido aparte.

            // 39: velocidad legible, con unidad y formato estable.
            if (vehicleStatusRef != null && vehicle != null)
            {
                var motorForFormat = vehicle.GetComponent<VehicleMotor>();
                vehicleStatusRef.UpdateFrom(vehicle, motorForFormat, false);
                var speedFieldInfo = GetRequiredField(typeof(VehicleStatusView), "speedLabel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var speedLabelText = ((Text)speedFieldInfo.GetValue(vehicleStatusRef)).text;
                bool formatoValido = System.Text.RegularExpressions.Regex.IsMatch(speedLabelText, @"^R? ?\d+[,.]\d u/s");
                Check($"Velocidad del vehiculo en formato legible ('{speedLabelText}')", formatoValido);
            }

            // 42: victoria y derrota con paleta y titulo claramente distintos.
            if (outcomeControllerRef != null)
            {
                var victoryPanel = outcomeControllerRef.transform.Find("VictoryPanel");
                var defeatPanel = outcomeControllerRef.transform.Find("DefeatPanel");
                if (victoryPanel != null && defeatPanel != null)
                {
                    var vImg = victoryPanel.GetComponent<Image>();
                    var dImg = defeatPanel.GetComponent<Image>();
                    var vTitle = victoryPanel.GetComponentInChildren<Text>(true);
                    var dTitle = defeatPanel.GetComponentInChildren<Text>(true);
                    bool coloresDistintos = vImg != null && dImg != null && vImg.color != dImg.color;
                    bool titulosDistintos = vTitle != null && dTitle != null && vTitle.text != dTitle.text
                        && !string.IsNullOrEmpty(vTitle.text) && !string.IsNullOrEmpty(dTitle.text);
                    Check($"Victoria y derrota tienen paleta distinta (victoria={vImg?.color} derrota={dImg?.color})", coloresDistintos);
                    Check($"Victoria y derrota tienen titulo distinto ('{vTitle?.text}' vs '{dTitle?.text}')", titulosDistintos);
                }
            }

            // 65: vida del enemigo visible en el panel de info al apuntarle.
            if (aimUiRef != null)
            {
                var enemyForAimCheck = ActorRegistry.FindNearest(vega.transform.position, s => s.Team == TeamId.Enemy && s.Health.IsAlive);
                if (enemyForAimCheck != null)
                {
                    aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = enemyForAimCheck, Point = enemyForAimCheck.transform.position });
                    var infoPanelField = GetRequiredField(typeof(AimUI), "soldierInfoText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var infoText = ((Text)infoPanelField.GetValue(aimUiRef))?.text ?? "";
                    Check($"Panel de info al apuntar a un enemigo muestra su vida ('{infoText}')",
                        infoText.Contains("Vida") && infoText.Contains("[Enemigo]"));
                }
            }

            // 15: barra de recarga legible -- ReadinessFraction01 baja
            // durante la recarga y el color cambia (naranja recargando,
            // verde listo).
            if (weaponStatusRef != null && vega.Weapon != null)
            {
                // BUG DE ESTE TEST encontrado y corregido: WeaponHolder.Reload()
                // devuelve false y no hace nada si CurrentAmmo ya esta al
                // maximo -- correcto, no tiene sentido recargar un cargador
                // lleno. Pero eso hacia que este check fuera FLAKY segun
                // cuanto habia disparado Vega en las fases anteriores. Se
                // dispara una vez primero para garantizar que haya algo
                // que recargar, sin importar el estado previo. Antes de
                // eso, Tick(1f) limpia cualquier cooldown de disparo que
                // hubiera quedado pendiente de una fase anterior -- sin
                // esto, TryFire podia fallar en silencio por cooldown
                // activo, dejando el cargador lleno y arruinando todo el
                // resto de la secuencia (verificado: pasaba de verdad).
                vega.Weapon.Tick(1f);
                vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
                vega.Weapon.Reload();
                weaponStatusRef.UpdateFrom(vega.Weapon);
                var fillField = GetRequiredField(typeof(WeaponStatusView), "fill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fillDuringReload = (Image)fillField.GetValue(weaponStatusRef);
                bool bajoDurantelaRecarga = fillDuringReload.fillAmount < 1f;
                bool colorRecargando = fillDuringReload.color == new Color(0.95f, 0.6f, 0.2f);
                Check($"Barra de recarga baja durante la recarga (fillAmount={fillDuringReload.fillAmount:0.00}) y cambia de color",
                    bajoDurantelaRecarga && colorRecargando);
                // Terminar la recarga a mano (Edit mode no avanza el reloj
                // solo) y confirmar que vuelve a 1 y a verde.
                for (int i = 0; i < 60 && vega.Weapon.IsReloading; i++) vega.Weapon.Tick(0.05f);
                // SEGUNDO BUG DE ESTE TEST encontrado y corregido: el
                // cooldown de disparo queda CONGELADO mientras
                // IsReloading es true (correcto: no tiene sentido que
                // corra un cooldown de disparo mientras el arma esta
                // desarmada recargando), y solo retoma su cuenta regresiva
                // DESPUES de que termina la recarga. El disparo que se
                // hizo arriba para forzar la recarga dejaba un cooldown
                // pendiente que el loop de arriba nunca le daba tiempo de
                // consumir, porque para de tickear apenas IsReloading pasa
                // a false. Verificado con el arma real: justo al terminar
                // la recarga el cooldown seguia entero (0.3s), y recien
                // se vaciaba tras un segundo mas de ticks.
                for (int i = 0; i < 20; i++) vega.Weapon.Tick(0.05f);
                weaponStatusRef.UpdateFrom(vega.Weapon);
                var fillAfterReload = (Image)fillField.GetValue(weaponStatusRef);
                Check($"Tras terminar la recarga, la barra vuelve a lleno y a verde (fillAmount={fillAfterReload.fillAmount:0.00})",
                    Mathf.Approximately(fillAfterReload.fillAmount, 1f) && fillAfterReload.color == new Color(0.4f, 0.85f, 0.45f));
            }

            // 94: impacto segun material -- colores y clips distintos por tipo.
            Check("ImpactFx.VehicleColor y EnemyColor son distintos (feedback visual por tipo de impacto)",
                ImpactFx.VehicleColor != ImpactFx.EnemyColor);
            Check("ImpactFx.ObstacleColor y GroundColor son distintos",
                ImpactFx.ObstacleColor != ImpactFx.GroundColor);
            var clipMetal = SP.Presentation.GenericSfx.Get(SP.Presentation.SfxKind.ImpactMetal);
            var clipDirt = SP.Presentation.GenericSfx.Get(SP.Presentation.SfxKind.ImpactDirt);
            var clipStone = SP.Presentation.GenericSfx.Get(SP.Presentation.SfxKind.ImpactStone);
            Check("Los 3 sonidos de impacto por material son clips distintos entre si",
                clipMetal != clipDirt && clipDirt != clipStone && clipMetal != clipStone);

            TestLog.Phase("FASE 5 FINALIZADA");
        }

        // ---------------------------------------------------------------
        // FASE 6: pedido del jugador tras probar el tanque en vivo --
        // orden de "seguir", cañon a doble velocidad y vibracion de
        // camara solo para quien esta ADENTRO del vehiculo.
        // ---------------------------------------------------------------
        static void RunPhase6(CameraRig rig, Vehicle vehicle, ProjectilePool pool, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 6 - Orden de seguir, cañon del tanque y vibracion por vehiculo");

            // Fases anteriores pueden dejar gente montada en el vehiculo
            // (Kes/Vega quedan asi tras las pruebas de Mount/Dismount de
            // fases previas): un soldado inactivo hace que AiBrain.Tick
            // salga por la guarda de activeInHierarchy ANTES de llegar a
            // ninguna logica de estado, asi que Follow jamas se moveria
            // por una razon ajena a la orden en si. Se arranca de una base
            // limpia bajando a todos.
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);

            // --- Orden "que me sigan" ---
            // Lejos de cualquier enemigo a proposito: Follow es
            // interrumpible por sensado (mismo diseño que MovingToOrder,
            // a propósito -- si aparece un enemigo mientras seguis al
            // lider, tiene que entrar en combate). Midiendo el
            // desplazamiento CERCA de una patrulla enemiga ese sensado
            // desviaba a Kes a Chase en el primerisimo tick, y el
            // movimiento medido pasaba a ser el de perseguir a ESE
            // enemigo, no el de acercarse al lider -- nada que ver con si
            // Follow mueve o no. Aislado en una esquina vacia del mapa se
            // mide la mecanica en si misma, sin la interferencia.
            var kesBrain = kes.GetComponent<AiBrain>();
            // Vega llega MUERTA a esta fase (cae en el combate de fases
            // anteriores y el juego no tiene revivir): Health.Heal() no
            // hace nada sobre un muerto a proposito, asi que Initialize
            // es la unica forma de darle vida de nuevo para este test.
            // Con el MISMO Id que ya tenia -- pasar uno distinto es el
            // bug real que ya aparecio esta sesion (Health.ActorId
            // desincronizado de Soldier.Id).
            vega.Health.Initialize(vega.Id, vega.Health.MaxHealth);
            vega.transform.position = new Vector3(300f, 0.6f, 300f);
            kes.transform.position = vega.transform.position + new Vector3(12f, 0f, 0f);
            OrderService.IssueFollowOrder(kes, vega);
            Check("IssueFollowOrder pone al soldado en estado Follow", kesBrain.State == AiState.Follow);
            Check("FollowTarget expone al lider mientras esta siguiendo", kesBrain.FollowTarget == vega);

            float distBefore = Vector3.Distance(kes.transform.position, vega.transform.position);
            for (int i = 0; i < 400 && kesBrain.State == AiState.Follow; i++) kesBrain.Tick(0.05f);
            float distAfter = Vector3.Distance(kes.transform.position, vega.transform.position);
            Check($"Seguir al lider reduce la distancia y se estabiliza cerca (antes={distBefore:0.0}m despues={distAfter:0.0}m)",
                distAfter < distBefore && distAfter <= 3.2f);

            kesBrain.CancelOrder();
            Check("CancelOrder saca a Kes de Follow y vuelve a Patrol", kesBrain.State == AiState.Patrol);
            Check("CancelOrder limpia FollowTarget", kesBrain.FollowTarget == null);

            // Si el lider se desactiva a mitad de Follow (ej.: sube a un
            // vehiculo), no debe quedar persiguiendo un punto muerto para
            // siempre -- mismo patron de bug que ya aparecio con
            // OffscreenAllyMarkerView y AiBrain.patrolRoute esta sesion.
            OrderService.IssueFollowOrder(kes, vega);
            vega.gameObject.SetActive(false);
            kesBrain.Tick(0.05f);
            Check("Si el lider se desactiva (ej. sube a un vehiculo), Follow se suelta solo y vuelve a Patrol",
                kesBrain.State == AiState.Patrol && kesBrain.FollowTarget == null);
            vega.gameObject.SetActive(true);

            // --- Cañon del tanque: proyectil al doble de velocidad ---
            var pNormal = pool.Spawn(vehicle.transform.position, Vector3.forward, -1, TeamId.Enemy, 10, null, 0f, 0f, null, 1f);
            float speedNormal = pNormal.Velocity.magnitude;
            pool.Release(pNormal);
            var pDoble = pool.Spawn(vehicle.transform.position, Vector3.forward, -1, TeamId.Enemy, 10, null, 0f, 0f, null, 2f);
            float speedDoble = pDoble.Velocity.magnitude;
            pool.Release(pDoble);
            Check($"speedMultiplier=2 duplica la velocidad real del proyectil (normal={speedNormal:0.0} doble={speedDoble:0.0})",
                Mathf.Approximately(speedDoble, speedNormal * 2f));

            // El cañon del tanque tiene que pedir SU multiplicador: no
            // alcanza con que el pool lo soporte, TurretWeapon.TryFire
            // tiene que usarlo de verdad.
            //
            // Este check decia "el doble de la base" con un 2 escrito a
            // mano, y fallo -- bien -- al subir la bala de mano de 40 a
            // 160 m/s. El invariante que importa nunca fue el factor: es
            // que el OBUS conserve su velocidad, porque vuela con arco y
            // gravedad y cuadruplicarlo lo convertiria en otra arma. Por
            // eso el multiplicador bajo de 2 a 0,5 en el mismo cambio.
            // Ahora se comprueban las dos cosas: que use su constante, y
            // que la velocidad absoluta siga siendo la historica de 80.
            var turret = vehicle.GetComponentInChildren<TurretWeapon>();
            var cdField = GetRequiredField(typeof(TurretWeapon), "cooldownTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cdField.SetValue(turret, 0f);
            // Snapshot POR IDENTIDAD y no "distinto de pNormal/pDoble": el
            // pool acaba de recibir esos dos de vuelta con Release(), asi
            // que estan arriba de la pila de libres y es MUY probable que
            // el propio TryFire() de aca abajo reuse esa misma instancia.
            // Comparar contra el snapshot de ANTES de disparar identifica
            // al proyectil nuevo sin importar si el objeto es reciclado.
            var activeBeforeFire = new HashSet<Projectile>(Projectile.ActiveInstances);
            bool fired = turret.TryFire();
            Projectile shellFired = null;
            foreach (var p in Projectile.ActiveInstances)
                if (!activeBeforeFire.Contains(p)) { shellFired = p; break; }
            Check("El cañon del tanque efectivamente disparo un proyectil nuevo",
                fired && shellFired != null);
            if (shellFired != null)
            {
                Check($"El proyectil real del cañon usa el multiplicador del cañon (velocidad={shellFired.Velocity.magnitude:0.0} = base {speedNormal:0.0} x {TurretWeapon.SpeedMultiplier})",
                    Mathf.Approximately(shellFired.Velocity.magnitude, speedNormal * TurretWeapon.SpeedMultiplier));
                Check($"El obus conserva su velocidad historica de 80 m/s pese a que la bala de mano se cuadruplico (velocidad={shellFired.Velocity.magnitude:0.0})",
                    Mathf.Abs(shellFired.Velocity.magnitude - 80f) < 0.5f);
            }

            // --- Sin vibracion de camara al disparar (pedido explicito) ---
            // Antes disparar con el jugador adentro sacudia la camara
            // (rig.KickDirectional); con rafaga sostenida eso se sentia
            // como un temblor constante en vez de un golpe puntual, y se
            // saco. Ahora el disparo NUNCA mueve ShakeOffset, este
            // adentro o afuera del vehiculo.
            bool fxWasEnabled = SP.CameraSystem.CameraFxSettings.Enabled;
            SP.CameraSystem.CameraFxSettings.Enabled = true;

            var instanceField = GetRequiredProperty(typeof(CameraRig), "Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var previousInstance = instanceField.GetValue(null);
            instanceField.SetValue(null, rig);

            for (int i = 0; i < 40; i++) turret.Tick(0.1f); // vaciar cooldown del disparo anterior
            vehicle.PlayerAboard = true;
            Vector3 shakeBefore = rig.ShakeOffset;
            turret.TryFire();
            Vector3 shakeAfter = rig.ShakeOffset;
            Check("Disparar el cañon con el jugador adentro NO mueve la vibracion de camara (se saco a pedido)",
                shakeAfter == shakeBefore);

            instanceField.SetValue(null, previousInstance);
            SP.CameraSystem.CameraFxSettings.Enabled = fxWasEnabled;
            vehicle.PlayerAboard = false;

            TestLog.Phase("FASE 6 FINALIZADA");
        }

        // ---------------------------------------------------------------
        // FASE 7: pedidos tras probar victoria/derrota y el vehiculo en
        // vivo -- poseer a un montado, ciclar/subir de a uno, barra de
        // vida de enemigo, y el cableado de los botones de fin de partida.
        // ---------------------------------------------------------------
        static void RunPhase7(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 7 - Poseer montado, subir de a uno, barra de vida enemiga, botones de fin");

            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);

            // --- Poseer a un aliado montado en el vehiculo (antes se rechazaba) ---
            vehicle.Mount(kes, VehicleSeatRole.Gunner);
            Check("Kes queda inactiva al montar (verificacion de la propia prueba)", !kes.gameObject.activeInHierarchy);
            bool poseidoMontado = inputDriver.TryPossess(kes);
            Check("TryPossess sobre un aliado montado en el vehiculo ahora tiene exito (antes se rechazaba)",
                poseidoMontado && inputDriver.Brain.Current == kes);
            Check("Al poseer a un montado, PlayerAboard queda en true (broker de la vibracion del cañon)",
                vehicle.PlayerAboard);
            var currentSeatField = GetRequiredField(typeof(PlayerInputDriver), "currentSeat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var seatAfterPossess = currentSeatField.GetValue(inputDriver);
            Check("El asiento asignado tras poseer al montado es el que de verdad ocupaba (Gunner)",
                seatAfterPossess != null && seatAfterPossess.ToString() == "Gunner");

            // Bajar y devolver el control a Vega para las pruebas siguientes.
            vehicle.Dismount(kes);
            currentSeatField.SetValue(inputDriver, null);
            inputDriver.TryPossess(vega);

            // --- Duracion de transicion x2 con lerp ---
            var durField = GetRequiredField(typeof(PlayerInputDriver), "PossessTransitionDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            float duracion = (float)durField.GetRawConstantValue();
            Check($"La duracion de transicion de posesion es el doble de la base (0.35s x2 = {duracion:0.00}s)",
                Mathf.Approximately(duracion, 0.7f));

            // --- Ciclar con [Q] ahora SI incluye a los montados ---
            vehicle.Mount(kes, VehicleSeatRole.Passenger1);
            var cycleMethod = GetRequiredMethod(typeof(PlayerInputDriver), "CycleLivingAlly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cycleMethod.Invoke(inputDriver, new object[] { 1 });
            Check("Ciclar con [Q] ahora SI puede terminar poseyendo a un aliado montado en el vehiculo",
                !inputDriver.Brain.Current.gameObject.activeInHierarchy || inputDriver.Brain.Current == kes);
            vehicle.Dismount(kes);
            currentSeatField.SetValue(inputDriver, null);
            inputDriver.TryPossess(vega);

            // --- [U] subir de a uno: el mas cercano no-tasqueado cada vez ---
            var findNextMethod = GetRequiredMethod(typeof(PlayerInputDriver), "FindNextSquadmateToBoard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var primerCandidato = (Soldier)findNextMethod.Invoke(inputDriver, new object[] { vehicle });
            Check("FindNextSquadmateToBoard encuentra a alguien para subir cuando el vehiculo esta vacio",
                primerCandidato != null);
            if (primerCandidato != null)
            {
                var brainCand = primerCandidato.GetComponent<AiBrain>();
                brainCand.IssueMountOrder(vehicle);
                var segundoCandidato = (Soldier)findNextMethod.Invoke(inputDriver, new object[] { vehicle });
                Check("Tras encargarle a uno que suba, el siguiente candidato es OTRO distinto (no repite al mismo)",
                    segundoCandidato != primerCandidato);
                brainCand.CancelOrder();
            }

            // BUG REAL encontrado y corregido tras probar "subir a un
            // muerto": Vehicle.Mount() no chequeaba Health.IsAlive.
            int docMaxHp = doc.Health.MaxHealth;
            doc.Health.TakeDamage(9999, -1);
            bool montoAUnMuerto = vehicle.Mount(doc);
            Check("Vehicle.Mount() rechaza a un soldado muerto (antes lo montaba igual)",
                !montoAUnMuerto && vehicle.OccupantCount == 0);
            doc.Health.Initialize(doc.Id, docMaxHp); // revivido: no contamina el resto de la suite

            // --- [I] bajar a todos: la misma DismountAll que ya prueba la Fase 6, ---
            // pero disparada por la tecla dedicada (sin apuntar), verificando
            // que FindTheVehicle resuelve el vehiculo sin necesitar aim.
            vehicle.Mount(kes, VehicleSeatRole.Passenger1);
            vehicle.Mount(doc, VehicleSeatRole.Passenger2);
            var findVehicleMethod = GetRequiredMethod(typeof(PlayerInputDriver), "FindTheVehicle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var vehiculoEncontrado = (Vehicle)findVehicleMethod.Invoke(inputDriver, null);
            Check("FindTheVehicle resuelve el vehiculo sin necesitar apuntarle (para las teclas [U]/[I])",
                vehiculoEncontrado == vehicle);
            var dismountAllMethod = GetRequiredMethod(typeof(PlayerInputDriver), "DismountAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dismountAllMethod.Invoke(inputDriver, new object[] { vehicle });
            Check("[I] (DismountAll via FindTheVehicle) baja a todos los ocupantes", vehicle.OccupantCount == 0);
            Check("Kes vuelve a estar activa tras bajar", kes.gameObject.activeInHierarchy);
            Check("Doc vuelve a estar activo tras bajar", doc.gameObject.activeInHierarchy);

            // --- Orden de movimiento del vehiculo desde RTS: antes fallaba en
            // silencio si nadie lo manejaba (TryIssueVehicleMoveOrder
            // devolvia false sin avisar por que). El fix agrega feedback
            // explicito en PlayerInputDriver y usa el vehiculo REALMENTE
            // seleccionado (parametro opcional), no solo el campo Vehicle
            // fijo por Inspector.
            var selectionForVehicle = inputDriver.GetComponent<SelectionController>() ?? UnityEngine.Object.FindAnyObjectByType<SelectionController>();
            selectionForVehicle.SelectVehicle(vehicle);
            Check("SelectVehicle deja al vehiculo como seleccionado", selectionForVehicle.SelectedVehicle == vehicle);
            var puntoLejano = vehicle.transform.position + new Vector3(50f, 0f, 50f);
            bool ordenSinConductor = inputDriver.TryIssueVehicleMoveOrder(puntoLejano, vehicle);
            Check("TryIssueVehicleMoveOrder rechaza la orden si nadie maneja (sin conductor)", !ordenSinConductor);

            vehicle.Mount(vega, VehicleSeatRole.Driver);
            var vbParaOrden = vehicle.GetComponent<VehicleBrain>();
            bool ordenConConductor = inputDriver.TryIssueVehicleMoveOrder(puntoLejano, vehicle);
            Check("TryIssueVehicleMoveOrder SI emite la orden una vez que hay conductor",
                ordenConConductor && vbParaOrden.HasOrder);
            vbParaOrden.Stop();
            vehicle.Dismount(vega);
            currentSeatField.SetValue(inputDriver, null);
            inputDriver.TryPossess(vega);

            // --- Click derecho en FPS: seleccionar aliado y ordenarle una posicion ---
            var selection = inputDriver.GetComponent<SelectionController>() ?? UnityEngine.Object.FindAnyObjectByType<SelectionController>();
            selection.SelectSingle(kes);
            Check("SelectSingle deja seleccionado solo al aliado apuntado",
                selection.Selected.Count == 1 && selection.Selected[0] == kes);
            var destino = new Vector3(15f, 0f, 15f);
            OrderService.IssueMoveOrderForSelection(selection.Selected, destino);
            var kesBrainOrder = kes.GetComponent<AiBrain>();
            Check("Tras click derecho en el piso con alguien seleccionado, esa orden de movimiento SI se emite",
                kesBrainOrder.State == AiState.MovingToOrder);
            kesBrainOrder.CancelOrder();

            // --- Barra de vida de enemigos: SI se actualiza en tiempo real ---
            Soldier enemigoParaBarra = null;
            foreach (var s in ActorRegistry.All) if (s.Team == TeamId.Enemy && s.Health.IsAlive) { enemigoParaBarra = s; break; }
            if (enemigoParaBarra != null)
            {
                var barra = enemigoParaBarra.GetComponentInChildren<SP.Presentation.HealthBarView>(true);
                var lodSetter = GetRequiredMethod(typeof(SP.Presentation.HealthBarView), "SetLodAllowed");
                lodSetter.Invoke(barra, new object[] { true });
                // OnAnyDamage sale temprano si !Application.isPlaying (a
                // proposito: la suite corre en Edit mode y no hay frame
                // que dibujar ahi). Sin la suscripcion, hideAt nunca se
                // pondria al dia solo -- se fuerza a mano, mismo criterio
                // que el resto de la suite usa con otros sistemas
                // gateados por Application.isPlaying.
                var hideAtField = GetRequiredField(typeof(SP.Presentation.HealthBarView), "hideAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                hideAtField.SetValue(barra, 999999f);
                int hpAntes = enemigoParaBarra.Health.Current;
                enemigoParaBarra.Health.TakeDamage(40, -1);
                barra.Tick();
                var fillField = GetRequiredField(typeof(SP.Presentation.HealthBarView), "fill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fill = (Image)fillField.GetValue(barra);
                float esperado = (float)enemigoParaBarra.Health.Current / enemigoParaBarra.Health.MaxHealth;
                Check($"La barra de vida del enemigo SI se actualiza con el daño real (hp {hpAntes}->{enemigoParaBarra.Health.Current}, fill={fill.fillAmount:0.00} esperado={esperado:0.00})",
                    Mathf.Approximately(fill.fillAmount, esperado));
            }

            // --- Botones de victoria/derrota: cableado real (el click en vivo
            // ya se verifico a mano con un evento de mouse real inyectado) ---
            if (outcomeControllerRef != null)
            {
                var victoryPanel = outcomeControllerRef.transform.Find("VictoryPanel");
                var retryBtn = victoryPanel.Find("RetryButton").GetComponent<Button>();
                var callsField = GetRequiredField(typeof(UnityEngine.Events.UnityEventBase), "m_Calls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var calls = callsField.GetValue(retryBtn.onClick);
                var runtimeCallsField = GetRequiredField(calls.GetType(), "m_RuntimeCalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var runtimeCalls = runtimeCallsField.GetValue(calls) as System.Collections.IList;
                Check("El boton REINTENTAR tiene su listener de verdad enganchado (no solo en apariencia)",
                    runtimeCalls != null && runtimeCalls.Count > 0);
                var es = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
                Check("Hay un EventSystem con InputSystemUIInputModule (el que de verdad procesa clicks reales)",
                    es != null && es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null);
            }

            TestLog.Phase("FASE 7 FINALIZADA");
        }

        // ---------------------------------------------------------------
        // FASE 8: las cinco tareas grandes del plan -- menu de ordenes con
        // [Q] sostenido, trazado de camino, coberturas, atropellar y mira.
        // ---------------------------------------------------------------
        static void RunPhase8(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
            GameObject soldierPrefab, Color enemyColor, ProjectilePool pool)
        {
            TestLog.Phase("FASE 8 - Menu de ordenes, camino trazado, coberturas, atropello y mira");

            // --- E2: el umbral que separa tocar de mantener ---
            SP.Player.KeyBindings.ForzarInicioDePulsacion(SP.Player.KeyBindings.CiclarPosesion, 0.1f);
            bool aLos100ms = SP.Player.KeyBindings.HayPulsacionRegistrada(
                SP.Player.KeyBindings.CiclarPosesion, PlayerInputDriver.SostenerParaMenu);
            Check("Un toque de 0,1 s NO llega al umbral de mantener (no abre el menu)", !aLos100ms);

            SP.Player.KeyBindings.ForzarInicioDePulsacion(SP.Player.KeyBindings.CiclarPosesion, 0.4f);
            bool aLos400ms = SP.Player.KeyBindings.HayPulsacionRegistrada(
                SP.Player.KeyBindings.CiclarPosesion, PlayerInputDriver.SostenerParaMenu);
            Check("Sostener 0,4 s SI llega al umbral de mantener (abre el menu)", aLos400ms);

            var menu = inputDriver.OrdenesMenu;
            Check("El menu de ordenes existe en el canvas y arranca cerrado", menu != null && !menu.Abierto);
            if (menu != null)
            {
                menu.Abrir();
                Check("Abrir() deja el menu visible", menu.Abierto);
                menu.Cerrar();
                Check("Cerrar() lo vuelve a esconder", !menu.Abierto);
            }
            Check($"El menu ofrece las 5 ordenes del plan ({SP.UI.MenuDeOrdenes.Opciones.Length})",
                SP.UI.MenuDeOrdenes.Opciones.Length == SP.UI.MenuDeOrdenes.CantidadDeOpciones);

            // --- E2: los dos gestos de la MISMA tecla, uno y el otro ---
            // Se ejercen por ResolverGestoDeQ, que es adonde llega la
            // lectura del teclado. Que la lectura produzca esos booleanos
            // en el momento correcto lo prueba el umbral de arriba.
            inputDriver.Squad = new List<Soldier> { vega, kes, doc };
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.IsPossessedByPlayer = false;
            }
            inputDriver.Brain.Possess(vega);
            inputDriver.OrdenesMenu.Cerrar();

            var antesDelToque = inputDriver.Brain.Current;
            inputDriver.ResolverGestoDeQ(toque: true, sostenido: false, sigueApretada: false);
            Check($"Toque corto: cicla de soldado ({antesDelToque.DisplayName} -> {inputDriver.Brain.Current.DisplayName}) y el menu NO aparece",
                inputDriver.Brain.Current != antesDelToque && !inputDriver.OrdenesMenu.Abierto);

            var antesDeMantener = inputDriver.Brain.Current;
            inputDriver.ResolverGestoDeQ(toque: false, sostenido: true, sigueApretada: true);
            Check("Mantener: aparece el menu y NO cicla de soldado",
                inputDriver.OrdenesMenu.Abierto && inputDriver.Brain.Current == antesDeMantener);

            inputDriver.ResolverGestoDeQ(toque: false, sostenido: false, sigueApretada: false);
            Check("Al soltar despues de mantener, el menu se cierra y tampoco cicla",
                !inputDriver.OrdenesMenu.Abierto && inputDriver.Brain.Current == antesDeMantener);

            // --- E2: las cinco ordenes hacen algo medible ---
            inputDriver.Squad = new List<Soldier> { vega, kes, doc };
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.IsPossessedByPlayer = false;
                s.Brain.CancelOrder();
            }
            inputDriver.Brain.Possess(vega);
            vega.Brain.IsPossessedByPlayer = true;
            OrderService.ManejadoAMano = vega;
            inputDriver.Selection.Clear();

            var destinatarios = inputDriver.DestinatariosDeOrden();
            Check($"Sin seleccion, las ordenes del menu igual le llegan a la escuadra viva menos el poseido ({destinatarios.Count})",
                destinatarios.Count == 2 && !destinatarios.Contains(vega));

            bool linea = inputDriver.EjecutarOrdenDelMenu(1);
            Check("Opcion 1 (formacion en linea) se emite y les deja destino a los dos aliados",
                linea && kes.Brain.CurrentOrderDestination.HasValue && doc.Brain.CurrentOrderDestination.HasValue);
            Vector3 destinoKesLinea = kes.Brain.CurrentOrderDestination ?? Vector3.zero;

            bool cuna = inputDriver.EjecutarOrdenDelMenu(2);
            Check("Opcion 2 (cuña) tambien se emite y cambia el destino respecto de la linea",
                cuna && kes.Brain.CurrentOrderDestination.HasValue
                     && (kes.Brain.CurrentOrderDestination.Value - destinoKesLinea).sqrMagnitude > 0.01f);

            bool seguir = inputDriver.EjecutarOrdenDelMenu(3);
            Check("Opcion 3 (siganme) deja a los dos aliados siguiendo al poseido",
                seguir && kes.Brain.State == AiState.Follow && doc.Brain.State == AiState.Follow);

            bool alto = inputDriver.EjecutarOrdenDelMenu(4);
            Check("Opcion 4 (alto) les cancela la orden a los dos",
                alto && !kes.Brain.CurrentOrderDestination.HasValue
                     && !doc.Brain.CurrentOrderDestination.HasValue
                     && kes.Brain.State != AiState.Follow && doc.Brain.State != AiState.Follow);

            // Opcion 5: el enfermero llega y cura de verdad.
            SP.Player.PedidoDeCuracion.Cancelar();
            vega.Health.TakeDamage(60, -1);
            int vidaAntes = vega.Health.Current;
            bool pedido = inputDriver.EjecutarOrdenDelMenu(5);
            Check($"Opcion 5 (necesito curarme) encuentra a quien mandar ({(SP.Player.PedidoDeCuracion.Enfermero != null ? SP.Player.PedidoDeCuracion.Enfermero.DisplayName : "nadie")})",
                pedido && SP.Player.PedidoDeCuracion.Activo);

            // Se lo pone al lado a mano: lo que se prueba aca es que curar
            // ocurre, no que sepa caminar (eso ya lo cubre la orden de seguir).
            SP.Player.PedidoDeCuracion.Enfermero.transform.position = vega.transform.position + Vector3.right * 1f;
            for (int i = 0; i < 60; i++) SimStep(0.05f);
            Check($"Con el enfermero al lado, la vida del herido sube ({vidaAntes} -> {vega.Health.Current})",
                vega.Health.Current > vidaAntes);

            vega.Health.Initialize(vega.Id, vega.Health.MaxHealth);
            SimStep(0.05f);
            Check("Con el herido ya lleno, el pedido de curacion se cierra solo",
                !SP.Player.PedidoDeCuracion.Activo);

            SP.Player.PedidoDeCuracion.Cancelar();
            OrderService.ManejadoAMano = null;
            vega.Brain.IsPossessedByPlayer = false;

            // --- C4: trazar un recorrido y ejecutarlo ---
            SP.Player.TrazadoDeCamino.Limpiar();
            kes.Brain.CancelOrder();
            // Lejos del origen a proposito: todos los obstaculos y los
            // enemigos de la escena viven dentro de |x|,|z| < 8, y un
            // recorrido que los cruza mide la navegacion (o el combate),
            // no el recorrido. El primer intento trazaba por (6,6), que
            // pasa por adentro de Obstaculo_1.
            kes.transform.position = new Vector3(30f, kes.transform.position.y, 30f);

            var recorrido = new Vector3[]
            {
                new Vector3(36f, 0f, 30f), new Vector3(36f, 0f, 36f),
                new Vector3(30f, 0f, 36f), new Vector3(24f, 0f, 30f),
            };
            int marcados = 0;
            foreach (var punto in recorrido)
                if (SP.Player.TrazadoDeCamino.Marcar(punto)) marcados++;
            Check($"Trazar 4 puntos con [Ctrl] deja 4 puntos en el recorrido ({marcados})",
                marcados == 4 && SP.Player.TrazadoDeCamino.Cantidad == 4);

            Check("Un punto pegado al anterior no suma un tramo de la nada",
                !SP.Player.TrazadoDeCamino.Marcar(recorrido[3] + new Vector3(0.2f, 0f, 0f))
                && SP.Player.TrazadoDeCamino.Cantidad == 4);

            int tramos = SP.Player.TrazadoDeCamino.Ejecutar(new List<Soldier> { kes });
            Check($"[Espacio] emite los 4 tramos y deja el trazado vacio ({tramos})",
                tramos == 4 && SP.Player.TrazadoDeCamino.Cantidad == 0);
            Check($"El soldado queda con 1 tramo en curso y 3 encolados ({kes.Brain.QueuedOrderCount} en cola)",
                kes.Brain.QueuedOrderCount == 3 && kes.Brain.CurrentOrderDestination.HasValue);

            // Que de verdad los recorra, y en orden: se anota a que
            // distancia minima paso de cada punto y en que instante.
            var masCerca = new float[recorrido.Length];
            var cuando = new int[recorrido.Length];
            for (int i = 0; i < recorrido.Length; i++) { masCerca[i] = float.MaxValue; cuando[i] = -1; }
            for (int paso = 0; paso < 1600; paso++)
            {
                SimStep(0.05f);
                for (int i = 0; i < recorrido.Length; i++)
                {
                    var plano = kes.transform.position; plano.y = 0f;
                    float d = Vector3.Distance(plano, recorrido[i]);
                    if (d < masCerca[i]) { masCerca[i] = d; if (d < 2f && cuando[i] < 0) cuando[i] = paso; }
                }
            }
            bool pasoPorTodos = true, enOrden = true;
            for (int i = 0; i < recorrido.Length; i++)
            {
                if (masCerca[i] >= 2f) pasoPorTodos = false;
                if (i > 0 && (cuando[i] < 0 || cuando[i - 1] < 0 || cuando[i] < cuando[i - 1])) enOrden = false;
            }
            Check($"Pasa a menos de 2 m de los 4 puntos (minimos: {masCerca[0]:0.0} / {masCerca[1]:0.0} / {masCerca[2]:0.0} / {masCerca[3]:0.0} m)",
                pasoPorTodos);
            Check($"Y los recorre EN ORDEN (pasos: {cuando[0]} -> {cuando[1]} -> {cuando[2]} -> {cuando[3]})", enOrden);

            SP.Player.TrazadoDeCamino.Limpiar();
            kes.Brain.CancelOrder();

            // --- G3: atropellar con el vehiculo en movimiento ---
            // Lejos de todo y con conductor propio adentro, asi el filtro
            // de bando tiene a quien respetar.
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vehicle.transform.position = new Vector3(50f, vehicle.transform.position.y, 50f);
            vehicle.transform.rotation = Quaternion.identity;   // mira a +Z
            vega.Brain.CancelOrder();
            vehicle.Mount(vega, VehicleSeatRole.Driver);
            var motorAtropello = vehicle.GetComponent<VehicleMotor>();

            // Frenar hasta cero ANTES de poner a nadie delante: el
            // vehiculo llega a esta fase con la velocidad que le dejaron
            // las anteriores (11 m/s), y Drive(0) solo le saca 4 m/s por
            // segundo. El primer intento de este test medio "quieto" a
            // 7,3 m/s y atropello al enemigo antes de empezar.
            for (int i = 0; i < 200 && !motorAtropello.IsStopped; i++) motorAtropello.Brake(0.05f);
            vehicle.transform.position = new Vector3(50f, vehicle.transform.position.y, 50f);
            vehicle.transform.rotation = Quaternion.identity;
            Check($"El vehiculo arranca la prueba frenado ({motorAtropello.CurrentSpeed:0.00} m/s)",
                motorAtropello.IsStopped);

            var victima = SpawnSoldier(soldierPrefab, "Atropellado", TeamId.Enemy, RoleType.Enemy,
                new Vector3(50f, 0f, 54f), enemyColor, pool, 100);
            var aliadoEnMedio = SpawnSoldier(soldierPrefab, "AliadoEnMedio", TeamId.Player, RoleType.Assault,
                new Vector3(50f, 0f, 54f), enemyColor, pool, 100);
            SP.Core.ApoyoEnElPiso.Apoyar(victima.transform);
            SP.Core.ApoyoEnElPiso.Apoyar(aliadoEnMedio.transform);
            victima.Brain.enabled = false;
            aliadoEnMedio.Brain.enabled = false;

            // Quieto: el motor tiene que estar tocando al enemigo y no
            // hacerle nada. Se lo pone pegado al casco a proposito.
            victima.transform.position = vehicle.transform.position + new Vector3(0f, 0f, 1.6f);
            int vidaQuieto = victima.Health.Current;
            for (int i = 0; i < 20; i++) motorAtropello.Drive(0f, 0f, 0.05f);
            Check($"Vehiculo quieto ({motorAtropello.CurrentSpeed:0.00} m/s) pegado al enemigo: 0 de daño ({vidaQuieto} -> {victima.Health.Current})",
                Mathf.Abs(motorAtropello.CurrentSpeed) < SP.Vehicles.Atropello.VelocidadMinima
                && victima.Health.Current == vidaQuieto);

            // Andando: se acelera hasta 8 m/s con el enemigo mas adelante.
            victima.transform.position = new Vector3(50f, victima.transform.position.y, 62f);
            aliadoEnMedio.transform.position = new Vector3(50f, aliadoEnMedio.transform.position.y, 62.5f);
            var rotAntes = aliadoEnMedio.transform.rotation;
            var rotVictimaAntes = victima.transform.rotation;
            float velocidadAlChocar = 0f;
            for (int i = 0; i < 200 && victima.Health.IsAlive; i++)
            {
                motorAtropello.Drive(1f, 0f, 0.05f);
                velocidadAlChocar = motorAtropello.CurrentSpeed;
            }
            Check($"Vehiculo a {velocidadAlChocar:0.0} m/s: el enemigo muere atropellado ({victima.Health.Current} de vida)",
                !victima.Health.IsAlive && velocidadAlChocar >= 8f);
            Check($"Y el cuerpo queda tirado, no de pie (angulo con la vertical: {Quaternion.Angle(rotVictimaAntes, victima.transform.rotation):0} grados)",
                Quaternion.Angle(rotVictimaAntes, victima.transform.rotation) > 45f);
            Check($"El aliado que iba en el camino NO fue atropellado por los suyos ({aliadoEnMedio.Health.Current} de vida)",
                aliadoEnMedio.Health.IsAlive && aliadoEnMedio.Health.Current == aliadoEnMedio.Health.MaxHealth
                && Quaternion.Angle(rotAntes, aliadoEnMedio.transform.rotation) < 1f);

            vehicle.Dismount(vega);
            UnityEngine.Object.DestroyImmediate(victima.gameObject);
            UnityEngine.Object.DestroyImmediate(aliadoEnMedio.gameObject);

            // --- F1: registrar los puntos de cobertura ---
            var solidos = SP.Core.Coberturas.Solidos();
            Check($"Los obstaculos solidos de la escena son los 4 Obstaculo_N, ni el piso ni las armas tiradas ({solidos.Count})",
                solidos.Count == 4);

            int coberturas = SP.Core.Coberturas.Registrar();
            Check($"Se registran 4 coberturas por obstaculo solido ({coberturas} para {solidos.Count})",
                coberturas == 4 * solidos.Count);

            bool ningunaAdentro = true;
            float masCercaDeUnMuro = float.MaxValue;
            foreach (var p in SP.Core.Coberturas.Puntos)
            {
                var alrededor = Physics.OverlapSphere(p + Vector3.up * 0.5f, SP.Core.Coberturas.RadioLibre,
                    ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in alrededor)
                    if (SP.Core.NavService.BlocksMovement(c)) ningunaAdentro = false;
                foreach (var c in solidos)
                    masCercaDeUnMuro = Mathf.Min(masCercaDeUnMuro, Vector3.Distance(c.ClosestPoint(p), p));
            }
            Check("Ninguna cobertura cae adentro de un collider", ningunaAdentro);
            Check($"Y todas quedan pegadas a su obstaculo (la mas cercana a {masCercaDeUnMuro:0.00} m de la cara)",
                masCercaDeUnMuro < SP.Core.Coberturas.DistanciaDeLaCara + 0.1f);

            var marcasEnEscena = GameObject.Find(SP.Core.Coberturas.NombreDelRoot);
            Check($"Las coberturas quedan marcadas en el mapa ({(marcasEnEscena != null ? marcasEnEscena.transform.childCount : 0)} marcas)",
                marcasEnEscena != null && marcasEnEscena.transform.childCount == coberturas);

            // --- F2: linea de tiro desde una cobertura ---
            // Obstaculo_1 esta en (6, 3) y mide 2x2. El enemigo se pone
            // justo del otro lado: la cobertura de la cara opuesta al
            // enemigo NO puede tirarle, la del costado SI.
            var bloqueo = solidos[0];
            foreach (var c in solidos) if (c.name == "Obstaculo_1") bloqueo = c;
            var centroBloqueo = bloqueo.bounds.center;
            // Muy resistente a proposito: lo que se mide es DONDE termina
            // parado el soldado, y si el blanco se muere a mitad de camino
            // el combate se corta y la medicion no dice nada.
            var blanco = SpawnSoldier(soldierPrefab, "BlancoDetrasDelMuro", TeamId.Enemy, RoleType.Enemy,
                centroBloqueo + new Vector3(0f, 0f, 4f), enemyColor, pool, 100000);
            SP.Core.ApoyoEnElPiso.Apoyar(blanco.transform);
            blanco.Brain.enabled = false;

            var caraOpuesta = new Vector3(centroBloqueo.x, bloqueo.bounds.min.y, centroBloqueo.z - (bloqueo.bounds.extents.z + 1f));
            var costado = new Vector3(centroBloqueo.x + (bloqueo.bounds.extents.x + 1f), bloqueo.bounds.min.y, centroBloqueo.z);
            bool desdeOpuesta = SP.Core.Coberturas.HayLineaDeTiroDesde(caraOpuesta + Vector3.up, blanco, null);
            bool desdeCostado = SP.Core.Coberturas.HayLineaDeTiroDesde(costado + Vector3.up, blanco, null);
            Check($"Con el obstaculo en medio, la cobertura de la cara OPUESTA no tiene linea de tiro ({desdeOpuesta})",
                !desdeOpuesta);
            Check($"Y la del costado SI la tiene ({desdeCostado})", desdeCostado);

            // --- F3: el soldado va a cubrirse y desde ahi tiene tiro ---
            // Con su propio obstaculo a campo abierto y no con los de la
            // escena: el primer intento medio junto al origen y el soldado
            // se trababa contra las tres armas tiradas en el piso (que
            // bloquean el paso), asi que lo que se media era eso y no la
            // cobertura.
            var obstaculoDePrueba = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstaculoDePrueba.name = "ObstaculoDePrueba";
            obstaculoDePrueba.transform.position = new Vector3(40f, 1f, 40f);
            obstaculoDePrueba.transform.localScale = new Vector3(2f, 2f, 2f);
            UnityEngine.Object.DestroyImmediate(blanco.gameObject);
            blanco = SpawnSoldier(soldierPrefab, "BlancoDetrasDelObstaculo", TeamId.Enemy, RoleType.Enemy,
                new Vector3(40f, 0f, 44f), enemyColor, pool, 100000);
            SP.Core.ApoyoEnElPiso.Apoyar(blanco.transform);
            // enabled = false NO alcanza: SimStep llama Tick() a mano sobre
            // todos los cerebros, sin mirar el enabled del componente. Sin
            // esto el blanco devuelve el fuego y mata al soldado a mitad de
            // la medicion. IsPossessedByPlayer es la unica salida temprana
            // real de Tick.
            blanco.Brain.enabled = false;
            blanco.Brain.IsPossessedByPlayer = true;
            SP.Core.NavService.Invalidate();
            int coberturasConPrueba = SP.Core.Coberturas.Registrar();
            Check($"El obstaculo nuevo suma sus 4 coberturas ({coberturasConPrueba})",
                coberturasConPrueba == coberturas + 4);

            kes.Brain.CancelOrder();
            kes.Brain.IsPossessedByPlayer = false;
            kes.transform.position = new Vector3(40f, kes.transform.position.y, blanco.transform.position.z - 15f);
            SP.Core.ApoyoEnElPiso.Apoyar(kes.transform);
            // La vision de la IA es de 10 m y el escenario del plan pide
            // 15: sin esto el soldado ni se entera de que hay un enemigo y
            // lo que se mediria es el sensado, no la cobertura.
            var campoDeVision = GetRequiredField(typeof(AiBrain), "visionRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float visionOriginal = (float)campoDeVision.GetValue(kes.Brain);
            campoDeVision.SetValue(kes.Brain, 20f);
            // A la misma altura que el soldado: el blanco recien creado
            // quedaba con y=0 y el soldado apoyado en y=0,80, asi que el
            // rayo de la linea de tiro bajaba hasta rozar el piso y daba
            // false por el Ground, no por el obstaculo. En terreno plano
            // los dos estan a la misma altura.
            blanco.transform.position = new Vector3(blanco.transform.position.x,
                kes.transform.position.y, blanco.transform.position.z);
            Physics.SyncTransforms();

            float distInicial = Vector3.Distance(kes.transform.position, blanco.transform.position);
            bool tiroAlEmpezar = kes.Brain.TieneLineaDeTiro(blanco);

            for (int i = 0; i < 400; i++) SimStep(0.05f);

            float aLaCobertura = float.MaxValue;
            Vector3 masCercana = Vector3.zero;
            foreach (var p in SP.Core.Coberturas.Puntos)
            {
                float d = Vector3.Distance(new Vector3(kes.transform.position.x, p.y, kes.transform.position.z), p);
                if (d < aLaCobertura) { aLaCobertura = d; masCercana = p; }
            }
            bool tiroAlFinal = kes.Brain.TieneLineaDeTiro(blanco);
            Check($"Y termina ATACANDO desde ahi, no solo parado ({kes.Brain.State}, vivo={kes.Health.IsAlive})",
                kes.Brain.State == AiState.Attack && kes.Health.IsAlive);
            Check($"Arranca a {distInicial:0.0} m del enemigo y SIN linea de tiro (verificacion de la propia prueba)",
                distInicial > 12f && !tiroAlEmpezar);
            Check($"Termina a menos de 1,5 m de una cobertura ({aLaCobertura:0.00} m de {masCercana}), no donde arranco",
                aLaCobertura < 1.5f);
            Check($"Y desde ahi SI le puede disparar ({tiroAlFinal})", tiroAlFinal);

            campoDeVision.SetValue(kes.Brain, visionOriginal);
            UnityEngine.Object.DestroyImmediate(obstaculoDePrueba);
            UnityEngine.Object.DestroyImmediate(blanco.gameObject);
            kes.Brain.CancelOrder();
            SP.Core.Coberturas.Limpiar();

            // --- H1: la mira que amplia de verdad ---
            // Se llega por el mismo camino que en el juego (el visor del
            // arma), no construyendo la optica a mano: lo que se prueba es
            // el cableado, no la clase suelta.
            inputDriver.Brain.Possess(vega);
            var visorMetodo = GetRequiredMethod(typeof(PlayerInputDriver), "UpdateWeaponViewmodel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            inputDriver.Rig.SetZoomed(true);
            vega.Weapon.EquipWeapon(WeaponKind.Rifle, 20, 0.2f, Color.white);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });

            var mira = inputDriver.Mira;
            Check("El arma tiene optica cuando se apunta", mira != null);

            float fovPrincipal = inputDriver.Rig.Cam.fieldOfView;
            Check($"El FOV de la optica es MENOR que el de la camara principal ({mira.Optica.fieldOfView:0.0} contra {fovPrincipal:0.0} grados)",
                mira.Optica.fieldOfView < fovPrincipal);
            // Y tambien menor que el DESTINO del zoom: la principal tarda
            // varios frames en bajar de 60 a 25, y medido en Play la optica
            // llegaba a quedar en 27 contra 25, o sea mas abierta.
            Check($"Y tambien menor que el FOV al que va el zoom ({mira.Optica.fieldOfView:0.0} contra {inputDriver.Rig.FovObjetivo:0.0} grados)",
                mira.Optica.fieldOfView < inputDriver.Rig.FovObjetivo);
            Check($"La optica renderiza a su propia RenderTexture, ya creada ({SP.Presentation.MiraOptica.LadoDeLaTextura} px)",
                mira.Textura != null && mira.Textura.IsCreated() && mira.Optica.targetTexture == mira.Textura);
            Check($"Y no se filma a si misma: recorta por delante del visor del arma ({mira.Optica.nearClipPlane:0.00} m)",
                mira.Optica.nearClipPlane >= SP.Presentation.MiraOptica.RecorteCercano);

            // Apuntando, el arma y su optica tienen que ENTRAR en el
            // encuadre. Medido antes del arreglo: viewport x = 1,88 con el
            // FOV de zoom, o sea el arma casi al doble del borde derecho.
            // Se fuerza el estado final del centrado (el lerp tarda unos
            // frames y aca no hay frames que pasen).
            var campoApuntado = GetRequiredField(typeof(PlayerInputDriver), "apuntadoVisual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campoApuntado.SetValue(inputDriver, 1f);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            float fovAntes = inputDriver.Rig.Cam.fieldOfView;
            inputDriver.Rig.Cam.fieldOfView = inputDriver.Rig.FovDeZoom;
            var enPantalla = inputDriver.Rig.Cam.WorldToViewportPoint(mira.Tubo.transform.position);
            inputDriver.Rig.Cam.fieldOfView = fovAntes;
            Check($"Apuntando, la optica queda DENTRO del encuadre con el FOV de zoom (viewport {enPantalla.x:0.00}, {enPantalla.y:0.00})",
                enPantalla.x > 0f && enPantalla.x < 1f && enPantalla.y > 0f && enPantalla.y < 1f && enPantalla.z > 0f);

            Check("Con el rifle es un tubo que amplia, y lo que muestra ES la textura de la optica",
                mira.Amplia && mira.Tubo.activeInHierarchy
                && mira.Tubo.GetComponent<MeshRenderer>().sharedMaterial.mainTexture == mira.Textura
                && mira.Optica.enabled);

            vega.Weapon.EquipWeapon(WeaponKind.Pistol, 20, 0.2f, Color.white);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            Check("Con la pistola es un cubo que marca el objetivo, sin aumento ni camara prendida",
                !mira.Amplia && mira.Tubo.GetComponent<MeshRenderer>().sharedMaterial.mainTexture == null
                && !mira.Optica.enabled);

            vega.Weapon.EquipWeapon(WeaponKind.Heavy, 20, 0.2f, Color.white);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            Check("Y el pesado vuelve a llevar tubo con aumento", mira.Amplia && mira.Optica.enabled);

            inputDriver.Rig.SetZoomed(false);
            visorMetodo.Invoke(inputDriver, new object[] { vega.Weapon });
            Check("Sin apuntar, la optica se esconde y su camara se apaga (no se paga un render por frame de gusto)",
                !mira.Tubo.activeInHierarchy && !mira.Optica.enabled);

            vega.Weapon.EquipWeapon(WeaponKind.Rifle, 20, 0.2f, Color.white);

            TestLog.Phase("FASE 8 FINALIZADA");
        }

        // ---------------------------------------------------------------
        // FASE 9 · las 23 tareas S/M que quedan del plan (numeradas #1 a
        // #23 en la hoja de ruta). Se completa una a la vez: cada tarea
        // suma sus Check() aca mismo, sin abrir una fase nueva por tarea.
        // ---------------------------------------------------------------
        static void RunPhase9(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
            GameObject soldierPrefab, Color enemyColor, ProjectilePool pool)
        {
            TestLog.Phase("FASE 9 - Tarea #1: obstaculos y enemigos vistos en el minimapa");

            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }

            // --- #1 / D1: obstaculos y enemigos vistos en el minimapa ---
            // El minimapa mostraba a la escuadra y a los vehiculos (puestos
            // a mano en SC_Gameplay) pero MinimapIcon.Spawn nunca se llamaba
            // para un obstaculo: grep confirma que el unico llamador vivia
            // en este mismo archivo, para soldados y vehiculos.
            int obstaculoIconosAntes = 0;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null) obstaculoIconosAntes++;
            Check($"Antes de la tarea, ningun obstaculo tenia icono en el minimapa ({obstaculoIconosAntes})",
                obstaculoIconosAntes == 0);

            var obstaculosEnEscena = UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include);
            int creados = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);
            Check($"Se crea un icono de minimapa por cada obstaculo ({creados} para {obstaculosEnEscena.Length} en escena)",
                creados == obstaculosEnEscena.Length && creados == 4);

            bool losCuatroVisibles = true;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null && !ic.IsRendered)
                    losCuatroVisibles = false;
            Check("Y los 4 quedan siempre visibles: son terreno, no dependen de la niebla de guerra", losCuatroVisibles);

            // Idempotente por destruir-y-rearmar: llamarlo de nuevo no deja
            // 8 iconos superpuestos sobre los mismos 4 obstaculos.
            int segundaVez = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);
            int totalTrasRepetir = 0;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null) totalTrasRepetir++;
            Check($"Registrarlos dos veces no duplica iconos ({totalTrasRepetir} tras llamarlo de nuevo, {segundaVez} creados la segunda vez)",
                segundaVez == 4 && totalTrasRepetir == 4);

            // La niebla de guerra sobre un enemigo (EnableFogOfWar +
            // WorldUiDirector.ApplyFog) ya existia en el codigo pero nunca
            // tuvo un Check(): la tarea pide explicitamente "enemigos que
            // la escuadra tenga a la vista", que es este mecanismo.
            // Rebarrer() de entrada: RebuildFogObservers lee
            // ActorRegistry.All directo, y Soldier.Awake (quien registra)
            // no corre en Edit mode -- sin esto el barrido de niebla ve
            // CERO observadores aunque Vega, Kes y Doc esten vivos y
            // parados ahi (medido: ActorRegistry.All.Count == 0).
            SP.Core.ActorRegistry.Rebarrer();
            // nextEvaluateAt se fuerza a 0 por reflexion para no depender
            // de que pasen 0,25 s reales de Time.time entre las dos
            // mediciones (lejos/cerca) de esta misma llamada a eval.
            var director = UnityEngine.Object.FindAnyObjectByType<WorldUiDirector>();
            var campoProximaEvaluacion = GetRequiredField(typeof(WorldUiDirector), "nextEvaluateAt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var enemigoDeNiebla = SpawnSoldier(soldierPrefab, "EnemigoDeNiebla", TeamId.Enemy, RoleType.Enemy,
                new Vector3(60f, 0.8f, 60f), enemyColor, pool, 100);
            enemigoDeNiebla.Brain.enabled = false;
            enemigoDeNiebla.Brain.IsPossessedByPlayer = true;
            MinimapIcon iconoEnemigoDeNiebla = null;
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include))
                if (ic.Target == enemigoDeNiebla.transform) iconoEnemigoDeNiebla = ic;

            // OnEnable no corre en Edit mode (mismo motivo por el que
            // WorldSystemsRegistry.EnsurePopulated existe unas lineas mas
            // arriba, para obstaculos y vehiculos): sin esto el icono
            // recien creado nunca se da de alta en la lista estatica que
            // recorre Tick(), y ApplyFog no se le llama nunca aunque
            // IsSpotted ya de por si de true. Se registra solo este icono
            // (no EnsurePopulated entero) para no dejar el flag global
            // "populated" en true y que una segunda corrida en la misma
            // sesion de Editor se quede sin registrar sus propios iconos.
            WorldUiDirector.Register(iconoEnemigoDeNiebla);

            vega.transform.position = enemigoDeNiebla.transform.position + new Vector3(30f, 0f, 0f);
            campoProximaEvaluacion.SetValue(director, 0f);
            director.Tick();
            Check($"Un enemigo a 30 m de la escuadra no se ve en el minimapa (visible={iconoEnemigoDeNiebla.IsRendered})",
                !iconoEnemigoDeNiebla.IsRendered);

            vega.transform.position = enemigoDeNiebla.transform.position + new Vector3(8f, 0f, 0f);
            campoProximaEvaluacion.SetValue(director, 0f);
            director.Tick();
            Check($"Y acercando un aliado a 8 m, aparece (visible={iconoEnemigoDeNiebla.IsRendered})",
                iconoEnemigoDeNiebla.IsRendered);

            UnityEngine.Object.DestroyImmediate(enemigoDeNiebla.gameObject);
            vega.Brain.CancelOrder();

            // --- #2 / D2: [M] agranda y minimiza el minimapa ---
            TestLog.Phase("FASE 9 - Tarea #2: [M] agranda y minimiza el minimapa");
            var borderRect = GameObject.Find("MinimapBorder").GetComponent<RectTransform>();
            Vector2 tamanoDePartida = borderRect.sizeDelta;

            bool agrandadoTrasM = minimapFollowRef.AlternarTamano();
            Check($"[M] agranda el minimapa ({tamanoDePartida} -> {borderRect.sizeDelta})",
                agrandadoTrasM && borderRect.sizeDelta.x > tamanoDePartida.x);

            bool agrandadoTrasSegundoM = minimapFollowRef.AlternarTamano();
            Check($"Y el segundo [M] lo devuelve EXACTO al tamaño de partida ({borderRect.sizeDelta})",
                !agrandadoTrasSegundoM && borderRect.sizeDelta == tamanoDePartida);

            for (int i = 0; i < 10; i++) minimapFollowRef.AlternarTamano();
            Check($"Ni tras 5 ciclos completos hay deriva ({borderRect.sizeDelta})",
                borderRect.sizeDelta == tamanoDePartida);

            // --- #3 / D3: [L] cicla el tamaño del minimapa ---
            // El indice de PlayerPrefs es del jugador (persiste entre
            // sesiones de Editor de verdad, igual que sp_crosshair_scale):
            // la primera llamada fija un punto de referencia CONOCIDO en
            // vez de asumir que el tamaño de partida de la escena (228)
            // coincide con lo que haya quedado guardado de una corrida
            // anterior -- si no, este Check() sale flaky segun el ultimo
            // indice que alguien haya dejado guardado.
            TestLog.Phase("FASE 9 - Tarea #3: [L] cicla el tamaño del minimapa");
            int indiceDeReferencia = minimapFollowRef.CiclarTamanoFijo();
            Vector2 tamanoDeReferencia = borderRect.sizeDelta;
            minimapFollowRef.CiclarTamanoFijo();
            minimapFollowRef.CiclarTamanoFijo();
            int indiceTrasTres = minimapFollowRef.CiclarTamanoFijo();
            Check($"Tres [L] seguidos vuelven al mismo tamaño (indice {indiceDeReferencia} -> {indiceTrasTres}, {tamanoDeReferencia} -> {borderRect.sizeDelta})",
                indiceTrasTres == indiceDeReferencia && borderRect.sizeDelta == tamanoDeReferencia);

            minimapFollowRef.CiclarTamanoFijo();
            Vector2 tamanoElegido = borderRect.sizeDelta;
            borderRect.sizeDelta = new Vector2(999f, 999f); // valor cualquiera, simulando la escena a medio cargar
            minimapFollowRef.AplicarTamanoGuardado();
            Check($"Y el tamaño elegido se recuerda: 'recargar la escena' lo vuelve a aplicar ({tamanoElegido})",
                borderRect.sizeDelta == tamanoElegido);

            // --- #4 / B1: un circulo radial reutilizable ---
            TestLog.Phase("FASE 9 - Tarea #4: un circulo radial reutilizable");
            var canvasGO = GameObject.Find("Canvas");
            var circulo = SP.UI.CirculoDeProgreso.Construir(canvasGO.transform, 64f, Color.black, Color.cyan);
            Check("El relleno es Filled/Radial360 y ya tiene sprite (sin eso fillAmount no dibuja nada, bug 30)",
                circulo.Relleno.type == Image.Type.Filled
                && circulo.Relleno.fillMethod == Image.FillMethod.Radial360
                && circulo.Relleno.sprite != null
                && circulo.Fondo.sprite != null);

            float[] valoresDePrueba = { 0f, 0.33f, 0.5f, 1f };
            bool todosExactos = true;
            foreach (var v in valoresDePrueba)
            {
                circulo.SetProgreso(v);
                if (!Mathf.Approximately(circulo.Relleno.fillAmount, v)) todosExactos = false;
            }
            Check($"Los 4 valores de prueba (0 / 0,33 / 0,5 / 1) dan el fillAmount exacto",
                todosExactos);

            UnityEngine.Object.DestroyImmediate(circulo.gameObject);

            // --- #5 / B2: la recarga se ve como circulo sobre la mira ---
            TestLog.Phase("FASE 9 - Tarea #5: la recarga se ve como circulo sobre la mira");
            var campoReloadDuration = GetRequiredField(typeof(WeaponHolder), "reloadDuration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float duracionDeRecarga = (float)campoReloadDuration.GetValue(vega.Weapon);

            // Reload() se rechaza en silencio con el cargador lleno (y
            // H1, arriba, lo deja lleno tras EquipWeapon): hay que gastar
            // al menos una bala antes de poder recargar de verdad.
            vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
            bool sePudoRecargar = vega.Weapon.Reload();
            Check("Se pudo iniciar la recarga (habia menos balas que el cargador)", sePudoRecargar);
            aimUiRef.UpdateReloadCircle(vega.Weapon);
            var campoCirculoRecarga = GetRequiredField(typeof(AimUI), "circuloRecarga",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var circuloRecarga = (CirculoDeProgreso)campoCirculoRecarga.GetValue(aimUiRef);
            Check("Al recargar, el circulo de progreso aparece sobre la mira", circuloRecarga.gameObject.activeSelf);

            vega.Weapon.Tick(duracionDeRecarga * 0.5f);
            aimUiRef.UpdateReloadCircle(vega.Weapon);
            Check($"A los {duracionDeRecarga * 0.5f:0.00} s de {duracionDeRecarga:0.00}, el circulo va por la mitad (fillAmount={circuloRecarga.Relleno.fillAmount:0.00})",
                Mathf.Abs(circuloRecarga.Relleno.fillAmount - 0.5f) < 0.05f);

            vega.Weapon.Tick(duracionDeRecarga);
            aimUiRef.UpdateReloadCircle(vega.Weapon);
            Check("Y al terminar la recarga, el circulo se esconde", !circuloRecarga.gameObject.activeSelf);

            // --- #6 / B3: al apuntar a un enemigo, su vida como circulo ---
            TestLog.Phase("FASE 9 - Tarea #6: al apuntar a un enemigo, su vida como circulo");
            var enemigoParaB3 = SpawnSoldier(soldierPrefab, "EnemigoParaB3", TeamId.Enemy, RoleType.Enemy,
                new Vector3(70f, 0.8f, 70f), enemyColor, pool, 180);
            enemigoParaB3.Brain.enabled = false;
            enemigoParaB3.Brain.IsPossessedByPlayer = true;
            enemigoParaB3.Health.TakeDamage(120, -1); // 180 - 120 = 60 de 180 => 0,33

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = enemigoParaB3, Point = enemigoParaB3.transform.position });
            var campoCirculoVida = GetRequiredField(typeof(AimUI), "circuloVidaEnemigo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var circuloVida = (CirculoDeProgreso)campoCirculoVida.GetValue(aimUiRef);
            float fraccionEsperada = (float)enemigoParaB3.Health.Current / enemigoParaB3.Health.MaxHealth;
            Check($"Apuntando a un enemigo con {enemigoParaB3.Health.Current}/{enemigoParaB3.Health.MaxHealth} de vida, el circulo muestra esa fraccion ({circuloVida.Relleno.fillAmount:0.00} vs {fraccionEsperada:0.00})",
                circuloVida.gameObject.activeSelf && Mathf.Abs(circuloVida.Relleno.fillAmount - fraccionEsperada) < 0.01f);

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Ground, Point = enemigoParaB3.transform.position });
            Check("Y apuntando al piso, el circulo de vida se apaga", !circuloVida.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(enemigoParaB3.gameObject);

            // --- #7 / B4: circulo de apuntado en la base del objetivo ---
            TestLog.Phase("FASE 9 - Tarea #7: circulo de apuntado en la base del objetivo");
            var enemigoParaB4 = SpawnSoldier(soldierPrefab, "EnemigoParaB4", TeamId.Enemy, RoleType.Enemy,
                new Vector3(80f, 0.8f, 80f), enemyColor, pool, 100);
            enemigoParaB4.Brain.enabled = false;
            enemigoParaB4.Brain.IsPossessedByPlayer = true;

            var metodoUpdateAimRing = GetRequiredMethod(typeof(PlayerInputDriver), "UpdateAimRing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var campoAimRing = GetRequiredField(typeof(PlayerInputDriver), "aimRing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var metodoLateUpdateAnillo = GetRequiredMethod(typeof(SelectionRingFx), "LateUpdate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var resultadoEnemigoB4 = new AimResult { Type = AimTargetType.Enemy, Soldier = enemigoParaB4, Point = enemigoParaB4.transform.position, HitTransform = enemigoParaB4.transform };
            metodoUpdateAimRing.Invoke(inputDriver, new object[] { resultadoEnemigoB4 });
            var anilloDeApuntado = (SelectionRingFx)campoAimRing.GetValue(inputDriver);
            // LateUpdate no corre solo en Edit mode: se fuerza una vez,
            // igual que con WorldUiDirector.Tick() en la tarea #1.
            metodoLateUpdateAnillo.Invoke(anilloDeApuntado, null);

            Physics.SyncTransforms();
            float baseDelCollider = enemigoParaB4.GetComponentInChildren<Collider>().bounds.min.y;
            Check($"Apuntando a un enemigo, el anillo existe y su Y ({anilloDeApuntado.transform.position.y:0.00}) coincide con la base de su collider ({baseDelCollider:0.00})",
                anilloDeApuntado != null && anilloDeApuntado.gameObject.activeSelf
                && Mathf.Abs(anilloDeApuntado.transform.position.y - baseDelCollider) < 0.05f);

            metodoUpdateAimRing.Invoke(inputDriver, new object[] { new AimResult { Type = AimTargetType.None } });
            Check("Y sin objetivo bajo la mira, el anillo se oculta", !anilloDeApuntado.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(enemigoParaB4.gameObject);

            // --- #8 / B5: cursor rojo si es destruible, y latido por tipo ---
            TestLog.Phase("FASE 9 - Tarea #8: cursor rojo si es destruible, y latido por tipo");
            var campoTint = GetRequiredField(typeof(AimUI), "currentAimTint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var obstaculoConMarker = UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include)[0];
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Obstacle,
                Point = obstaculoConMarker.transform.position, HitTransform = obstaculoConMarker.transform });
            var tintDestructible = (Color)campoTint.GetValue(aimUiRef);
            Check($"Sobre un obstaculo CON ObstacleMarker (destructible), el tinte es rojo {tintDestructible}",
                tintDestructible.r > 0.7f && tintDestructible.g < 0.3f && tintDestructible.b < 0.3f);

            var muroDePrueba = GameObject.CreatePrimitive(PrimitiveType.Cube);
            muroDePrueba.name = "MuroDePrueba";
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Obstacle,
                Point = muroDePrueba.transform.position, HitTransform = muroDePrueba.transform });
            var tintMuro = (Color)campoTint.GetValue(aimUiRef);
            Check($"Y sobre uno SIN ObstacleMarker (pared fija, no destructible), el tinte NO es rojo {tintMuro}",
                !(tintMuro.r > 0.7f && tintMuro.g < 0.3f && tintMuro.b < 0.3f));
            UnityEngine.Object.DestroyImmediate(muroDePrueba);

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Ally, Soldier = kes });
            float frecuenciaAliado = aimUiRef.CurrentPulseFrequency;
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = kes });
            float frecuenciaEnemigo = aimUiRef.CurrentPulseFrequency;
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle });
            float frecuenciaVehiculo = aimUiRef.CurrentPulseFrequency;
            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Obstacle,
                Point = obstaculoConMarker.transform.position, HitTransform = obstaculoConMarker.transform });
            float frecuenciaObstaculo = aimUiRef.CurrentPulseFrequency;

            var frecuenciasDistintas = new HashSet<float> { frecuenciaAliado, frecuenciaEnemigo, frecuenciaVehiculo, frecuenciaObstaculo };
            Check($"Las 4 frecuencias de latido son distintas y positivas ({frecuenciaAliado}, {frecuenciaEnemigo}, {frecuenciaVehiculo}, {frecuenciaObstaculo})",
                frecuenciasDistintas.Count == 4 && frecuenciaAliado > 0f && frecuenciaEnemigo > 0f && frecuenciaVehiculo > 0f && frecuenciaObstaculo > 0f);

            // --- #9-#11 / A1+A2+A3: la camara de muerte espera en vez de
            // cambiar sola. DeathSequence es una corrutina
            // (StartCoroutine): no corre en Edit mode, asi que el timing
            // real -- la espera, [Espacio] adelantando el cambio, los 5 s
            // de A3 -- se midio en Play mode sobre SC_Gameplay (no hay
            // Check() posible aca para eso). Lo que si se puede verificar
            // en Edit mode es la pieza de datos que esa espera usa.
            TestLog.Phase("FASE 9 - Tareas #9-#11: A1+A2+A3, la camara de muerte espera en vez de cambiar sola");
            Check($"La espera antes de pasar a RTS es de {PlayerInputDriver.EsperaMaximaTrasMorir} s (A3)",
                PlayerInputDriver.EsperaMaximaTrasMorir == 5f);

            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }
            var elegidoTrasMorirVega = OrderService.FindNearestFreeAlly(vega.transform.position, TeamId.Player, vega);
            Check($"El aliado que A2 pide con [Espacio] es el vivo mas cercano, nunca el propio muerto ({elegidoTrasMorirVega?.DisplayName})",
                elegidoTrasMorirVega != null && elegidoTrasMorirVega != vega && elegidoTrasMorirVega.Health.IsAlive);

            // --- #12 / A4: mantener [E] 5 s revive a un caido ---
            // TryRevivir toma "sostenido lo suficiente" como parametro (no
            // lee Keyboard.current el mismo) exactamente para esto: se
            // puede simular con ForzarInicioDePulsacion + HayPulsacionRegistrada
            // sin depender de un teclado real, que no existe en Edit mode.
            TestLog.Phase("FASE 9 - Tarea #12: mantener [E] 5 s revive a un caido");
            doc.Health.TakeDamage(999999, -1);
            Check($"Doc esta muerto para la prueba ({doc.Health.Current} de vida)", !doc.Health.IsAlive);

            KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 3f);
            bool revivioA3s = inputDriver.TryRevivir(doc, KeyBindings.HayPulsacionRegistrada(KeyBindings.Interactuar, PlayerInputDriver.TiempoDeRevivir));
            Check($"A los 3 s de {PlayerInputDriver.TiempoDeRevivir} sigue muerto ({doc.Health.IsAlive})",
                !revivioA3s && !doc.Health.IsAlive);

            // +0.1 en vez de EXACTO: HayPulsacionRegistrada compara con
            // Time.unscaledTime, que tras horas de sesion de Editor pierde
            // precision de punto flotante justo en el limite (>=) y el
            // check salia flaky sin que el mecanismo real tuviera nada
            // roto -- en el juego de verdad nadie suelta la tecla en el
            // milisegundo EXACTO del umbral.
            KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, PlayerInputDriver.TiempoDeRevivir + 0.1f);
            bool revivioA5s = inputDriver.TryRevivir(doc, KeyBindings.HayPulsacionRegistrada(KeyBindings.Interactuar, PlayerInputDriver.TiempoDeRevivir));
            Check($"Y a los {PlayerInputDriver.TiempoDeRevivir} s, Health.IsAlive pasa a true ({doc.Health.Current}/{doc.Health.MaxHealth})",
                revivioA5s && doc.Health.IsAlive && doc.Health.Current == doc.Health.MaxHealth);

            // --- #13 / A5: un aliado libre va a revivirte y frena el timer ---
            TestLog.Phase("FASE 9 - Tarea #13: un aliado libre va a revivirte y frena el timer");
            SP.Player.RescateAutomatico.Cancelar();
            vega.transform.position = new Vector3(95f, 0.8f, 95f);
            kes.transform.position = vega.transform.position + new Vector3(10f, 0f, 0f);
            SP.Core.ApoyoEnElPiso.Apoyar(kes.transform);
            doc.transform.position = new Vector3(-95f, 0.8f, -95f); // lejos, no interfiere

            vega.Health.TakeDamage(999999, -1);
            bool solicitoRescate = SP.Player.RescateAutomatico.Solicitar(vega);
            Check($"Con un aliado a 10 m y ningun enemigo cerca, se pide el rescate ({SP.Player.RescateAutomatico.Rescatista?.DisplayName})",
                solicitoRescate && SP.Player.RescateAutomatico.Activo && SP.Player.RescateAutomatico.Rescatista == kes);

            // Se lo pone al lado a mano: lo que se prueba aca es que
            // revivir ocurre, no que sepa caminar (eso ya lo cubre la
            // orden de seguir -- mismo criterio que el enfermero de
            // PedidoDeCuracion unas fases atras).
            kes.transform.position = vega.transform.position + Vector3.right * 1f;
            for (int i = 0; i < 80; i++) SimStep(0.05f); // 4 s de canal
            Check($"A los 4 s de canal (de {SP.Player.RescateAutomatico.TiempoDeCanal} s) sigue muerto", !vega.Health.IsAlive);

            for (int i = 0; i < 30; i++) SimStep(0.05f); // +1.5 s: pasa los 5 s
            Check($"Y a los ~{SP.Player.RescateAutomatico.TiempoDeCanal} s, Health.IsAlive pasa a true ({vega.Health.Current}/{vega.Health.MaxHealth})",
                vega.Health.IsAlive && vega.Health.Current == vega.Health.MaxHealth);
            Check("El rescate se cierra solo tras revivir", !SP.Player.RescateAutomatico.Activo);

            SP.Player.RescateAutomatico.Cancelar();

            TestLog.Phase("FASE 9 FINALIZADA (13/23)");

            // --- #14 / E1: [F] sobre un enemigo da la orden de atacar ---
            TestLog.Phase("FASE 9 - Tarea #14: [F] sobre un enemigo da la orden de atacar");
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }
            inputDriver.Brain.Possess(vega);
            vega.Brain.IsPossessedByPlayer = true;
            var estadoDeVegaAntes = vega.Brain.State;

            inputDriver.Selection.SelectSingle(kes);
            inputDriver.Selection.AddToSelection(doc);

            var enemigoParaE1 = SpawnSoldier(soldierPrefab, "EnemigoParaE1", TeamId.Enemy, RoleType.Enemy,
                new Vector3(85f, 0.8f, 85f), enemyColor, pool, 150);
            enemigoParaE1.Brain.enabled = false;
            enemigoParaE1.Brain.IsPossessedByPlayer = true;

            aimUiRef.UpdateFromAimResult(new AimResult { Type = AimTargetType.Enemy, Soldier = enemigoParaE1,
                Point = enemigoParaE1.transform.position, HitTransform = enemigoParaE1.transform });
            Check($"Apuntando a un enemigo, el cartel invita a atacar (\"{aimUiRef.CurrentPrompt}\")",
                aimUiRef.CurrentPrompt.Contains("F") && aimUiRef.CurrentPrompt.Contains(enemigoParaE1.DisplayName));

            OrderService.IssueAttackOrderForSelection(inputDriver.Selection.Selected, enemigoParaE1);
            SimulateSeconds(1f); // deja que Chase/Attack se resuelva tras la orden

            bool kesEnCombate = kes.Brain.State == AiState.Chase || kes.Brain.State == AiState.MovingToAttackOrder || kes.Brain.State == AiState.Attack;
            bool docEnCombate = doc.Brain.State == AiState.Chase || doc.Brain.State == AiState.MovingToAttackOrder || doc.Brain.State == AiState.Attack;
            Check($"[F] deja a los 2 seleccionados yendo a atacar o atacando ({kes.Brain.State}, {doc.Brain.State})",
                kesEnCombate && docEnCombate);
            Check($"Y al poseido no le llega la orden: su estado no cambio ({estadoDeVegaAntes} -> {vega.Brain.State})",
                vega.Brain.State == estadoDeVegaAntes);

            UnityEngine.Object.DestroyImmediate(enemigoParaE1.gameObject);
            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();
            inputDriver.Selection.Clear();

            TestLog.Phase("FASE 9 FINALIZADA (14/23)");

            // --- #15 / E3: doble [T] reparte, Shift+[T] distribuye ---
            TestLog.Phase("FASE 9 - Tarea #15: doble [T] reparte, Shift+[T] distribuye");
            var puntoT = new Vector3(90f, 0f, 90f);
            inputDriver.IssueGroundOrderT(puntoT, false);
            Check("El primer [T] manda a alguien",
                kes.Brain.CurrentOrderDestination.HasValue || doc.Brain.CurrentOrderDestination.HasValue);

            inputDriver.IssueGroundOrderT(puntoT, false); // segundo T rapido, mismo punto
            Check($"Y el segundo [T] rapido reparte al OTRO, no repite al mismo (Kes={kes.Brain.CurrentOrderDestination.HasValue}, Doc={doc.Brain.CurrentOrderDestination.HasValue})",
                kes.Brain.CurrentOrderDestination.HasValue && doc.Brain.CurrentOrderDestination.HasValue);

            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();

            // Shift+[T] con 3 libres: Vega esta poseido, asi que un
            // conductor temporal toma el mando para dejar a Vega, Kes y
            // Doc libres los tres a la vez.
            var conductorTemporalE3 = SpawnSoldier(soldierPrefab, "ConductorTemporalE3", TeamId.Player, RoleType.Assault,
                new Vector3(200f, 0.8f, 200f), new Color(0.25f, 0.55f, 0.98f), pool, 100);
            inputDriver.Brain.Possess(conductorTemporalE3);
            conductorTemporalE3.Brain.IsPossessedByPlayer = true;
            vega.Brain.CancelOrder();

            var puntoShiftT = new Vector3(-90f, 0f, -90f);
            inputDriver.IssueGroundOrderT(puntoShiftT, true);

            Vector3? destinoVega = vega.Brain.CurrentOrderDestination;
            Vector3? destinoKes = kes.Brain.CurrentOrderDestination;
            Vector3? destinoDoc = doc.Brain.CurrentOrderDestination;
            Check($"Shift+[T] con 3 libres les da destino a los 3 (Vega={destinoVega.HasValue}, Kes={destinoKes.HasValue}, Doc={destinoDoc.HasValue})",
                destinoVega.HasValue && destinoKes.HasValue && destinoDoc.HasValue);

            float dVK = Vector3.Distance(destinoVega.Value, destinoKes.Value);
            float dVD = Vector3.Distance(destinoVega.Value, destinoDoc.Value);
            float dKD = Vector3.Distance(destinoKes.Value, destinoDoc.Value);
            Check($"Y los 3 destinos quedan a mas de 1,8 m entre si ({dVK:0.00}, {dVD:0.00}, {dKD:0.00})",
                dVK > 1.8f && dVD > 1.8f && dKD > 1.8f);

            UnityEngine.Object.DestroyImmediate(conductorTemporalE3.gameObject);
            inputDriver.Brain.Possess(vega);
            vega.Brain.IsPossessedByPlayer = true;
            vega.Brain.CancelOrder();
            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();

            TestLog.Phase("FASE 9 FINALIZADA (15/23)");

            // --- #16 / C1: bajo cada unidad: vida, tipo y ocupantes ---
            TestLog.Phase("FASE 9 - Tarea #16: bajo cada unidad: vida, tipo y ocupantes");
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);
            vehicle.Mount(kes);
            vehicle.Mount(doc);

            var etiquetaVehiculo = UnitLabelView.Construir(vehicle.transform);
            WorldUiDirector.Register(etiquetaVehiculo);
            var etiquetaVega = UnitLabelView.Construir(vega.transform);
            WorldUiDirector.Register(etiquetaVega);

            var directorC1 = UnityEngine.Object.FindAnyObjectByType<WorldUiDirector>();
            var campoProximaEvaluacionC1 = GetRequiredField(typeof(WorldUiDirector), "nextEvaluateAt",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var camaraPrincipalC1 = Camera.main;
            bool orthoOriginalC1 = camaraPrincipalC1 != null && camaraPrincipalC1.orthographic;

            // FPS primero: las etiquetas tienen que quedar apagadas.
            if (camaraPrincipalC1 != null) camaraPrincipalC1.orthographic = false;
            campoProximaEvaluacionC1.SetValue(directorC1, 0f);
            directorC1.Tick();
            Check($"En FPS (camara no ortografica), la etiqueta se apaga (visible={etiquetaVehiculo.IsVisible})",
                !etiquetaVehiculo.IsVisible);

            // RTS: aparecen, con la vida/tipo/ocupacion correctos.
            if (camaraPrincipalC1 != null) camaraPrincipalC1.orthographic = true;
            campoProximaEvaluacionC1.SetValue(directorC1, 0f);
            directorC1.Tick();
            Check($"En RTS, con 2 montados de 4, la etiqueta del vehiculo dice 2/4 (\"{etiquetaVehiculo.CurrentText}\")",
                etiquetaVehiculo.IsVisible && etiquetaVehiculo.CurrentText == $"Vehiculo  2/{vehicle.Capacity}");
            Check($"Y la del aliado muestra su tipo y su vida (\"{etiquetaVega.CurrentText}\")",
                etiquetaVega.IsVisible && etiquetaVega.CurrentText == $"Aliado  {vega.Health.Current}/{vega.Health.MaxHealth}");

            vehicle.Dismount(doc);
            campoProximaEvaluacionC1.SetValue(directorC1, 0f);
            directorC1.Tick();
            Check($"Al bajar uno, pasa a 1/4 (\"{etiquetaVehiculo.CurrentText}\")",
                etiquetaVehiculo.CurrentText == $"Vehiculo  1/{vehicle.Capacity}");

            if (camaraPrincipalC1 != null) camaraPrincipalC1.orthographic = orthoOriginalC1;
            vehicle.Dismount(kes);
            UnityEngine.Object.DestroyImmediate(etiquetaVehiculo.transform.parent.gameObject);
            UnityEngine.Object.DestroyImmediate(etiquetaVega.transform.parent.gameObject);

            TestLog.Phase("FASE 9 FINALIZADA (16/23)");

            // --- #17 / C2: circulos para unidades, cuadrados para interactuables ---
            TestLog.Phase("FASE 9 - Tarea #17: circulos para unidades, cuadrados para interactuables");
            int obstaculosRegistradosC2 = MinimapIcon.RegistrarObstaculos(MinimapIcon.ObstacleMinimapColor);

            var todosLosIconosC2 = UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsInactive.Include);
            int cuadrados = 0, circulosDeObstaculo = 0, circulosDeUnidad = 0;
            foreach (var ic in todosLosIconosC2)
            {
                bool esObstaculo = ic.Target != null && ic.Target.GetComponent<ObstacleMarker>() != null;
                if (esObstaculo)
                {
                    if (ic.EsCuadrado) cuadrados++; else circulosDeObstaculo++;
                }
                else if (!ic.EsCuadrado) circulosDeUnidad++;
            }
            Check($"Los {obstaculosRegistradosC2} obstaculos (interactuables) tienen icono CUADRADO ({cuadrados} cuadrados, {circulosDeObstaculo} circulos entre ellos)",
                cuadrados == obstaculosRegistradosC2 && circulosDeObstaculo == 0);
            Check($"Y las unidades (soldados, vehiculo) siguen con icono CIRCULAR, la forma distingue la categoria ({circulosDeUnidad} circulos de unidad)",
                circulosDeUnidad == todosLosIconosC2.Length - obstaculosRegistradosC2);

            TestLog.Phase("FASE 9 FINALIZADA (17/23)");

            // --- #18 / C3: marca de montable y orden de ir a montar ---
            TestLog.Phase("FASE 9 - Tarea #18: marca de montable y orden de ir a montar");
            foreach (var s in new[] { vega, kes, doc })
            {
                s.gameObject.SetActive(true);
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Brain.CancelOrder();
                s.Brain.IsPossessedByPlayer = false;
            }
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);

            var rellenoC3 = new List<Soldier>();
            for (int i = 0; i < vehicle.Capacity; i++)
            {
                var s = SpawnSoldier(soldierPrefab, $"RellenoC3_{i}", TeamId.Player, RoleType.Assault,
                    vehicle.transform.position, new Color(0.25f, 0.55f, 0.98f), pool, 100);
                vehicle.Mount(s);
                rellenoC3.Add(s);
            }
            Check($"El vehiculo queda lleno ({vehicle.OccupantCount}/{vehicle.Capacity})", !vehicle.HasAnyRoom);

            inputDriver.Selection.SelectSingle(vega);
            var metodoIndicadorRts = GetRequiredMethod(typeof(PlayerInputDriver), "UpdateVehicleMountIndicatorRts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var campoMountIndicator = GetRequiredField(typeof(PlayerInputDriver), "mountIndicator",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            metodoIndicadorRts.Invoke(inputDriver, new object[] { new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = vehicle.transform.position } });
            var indicadorC3 = (VehicleMountIndicator)campoMountIndicator.GetValue(inputDriver);
            Check($"Con el vehiculo lleno, la marca dice IMPOSIBLE (puede={indicadorC3.UltimoPuedeMontar})",
                indicadorC3.UltimoPuedeMontar == false);

            vehicle.Dismount(rellenoC3[0]);
            metodoIndicadorRts.Invoke(inputDriver, new object[] { new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = vehicle.transform.position } });
            Check($"Con lugar libre, la marca dice que SI se puede (puede={indicadorC3.UltimoPuedeMontar})",
                indicadorC3.UltimoPuedeMontar == true);

            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);
            foreach (var s in rellenoC3) if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);

            vehicle.transform.position = new Vector3(60f, 0.6f, 60f);
            doc.transform.position = vehicle.transform.position + new Vector3(-8f, 0f, 0f);
            SP.Core.ApoyoEnElPiso.Apoyar(doc.transform);
            OrderService.IssueMountOrder(doc, vehicle);
            bool subioC3 = SimulateUntil(() => vehicle.Occupants.Count > 0, 12f);
            Check($"Con lugar, el soldado camina y sube (RoleOf={vehicle.RoleOf(doc)})",
                subioC3 && vehicle.RoleOf(doc) != null);

            vehicle.Dismount(doc);
            inputDriver.Selection.Clear();

            TestLog.Phase("FASE 9 FINALIZADA (18/23)");

            // --- #19 / G1: el barril se incendia con un disparo y despues explota ---
            TestLog.Phase("FASE 9 - Tarea #19: el barril se incendia con un disparo y despues explota");

            var barrilGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrilGo.name = "BarrilDePrueba";
            barrilGo.transform.position = new Vector3(80f, 0.75f, 80f);
            barrilGo.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            var barril = barrilGo.AddComponent<ObstacleMarker>();
            var campoEsExplosivo = GetRequiredField(typeof(ObstacleMarker), "esExplosivo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campoEsExplosivo.SetValue(barril, true);

            var testigoCerca = SpawnSoldier(soldierPrefab, "TestigoCercaDelBarril", TeamId.Player, RoleType.Assault,
                barrilGo.transform.position + new Vector3(3f, 0f, 0f), new Color(0.25f, 0.55f, 0.98f), pool, 100);
            testigoCerca.Brain.CancelOrder();
            testigoCerca.Brain.IsPossessedByPlayer = true; // no se mueve solo durante la medicion
            SP.Core.ApoyoEnElPiso.Apoyar(testigoCerca.transform);

            var testigoLejos = SpawnSoldier(soldierPrefab, "TestigoLejosDelBarril", TeamId.Player, RoleType.Assault,
                barrilGo.transform.position + new Vector3(12f, 0f, 0f), new Color(0.25f, 0.55f, 0.98f), pool, 100);
            testigoLejos.Brain.CancelOrder();
            testigoLejos.Brain.IsPossessedByPlayer = true;
            SP.Core.ApoyoEnElPiso.Apoyar(testigoLejos.transform);

            int vidaCercaAntes = testigoCerca.Health.Current;
            int vidaLejosAntes = testigoLejos.Health.Current;

            barril.TakeDamage(10);
            Check($"Un disparo enciende el barril y sigue en pie (encendido={barril.EstaEncendido}, colapsado={barril.IsCollapsed})",
                barril.EstaEncendido && !barril.IsCollapsed);

            bool exploto = SimulateUntil(() => barril.IsCollapsed, 6f);
            Check($"A los pocos segundos el barril queda destruido ({exploto})", exploto);
            Check($"El testigo a 3 m del barril perdio vida ({vidaCercaAntes} -> {testigoCerca.Health.Current})",
                testigoCerca.Health.Current < vidaCercaAntes);
            Check($"El testigo a 12 m del barril NO perdio vida ({vidaLejosAntes} -> {testigoLejos.Health.Current})",
                testigoLejos.Health.Current == vidaLejosAntes);

            testigoCerca.Brain.IsPossessedByPlayer = false;
            testigoLejos.Brain.IsPossessedByPlayer = false;
            UnityEngine.Object.DestroyImmediate(testigoCerca.gameObject);
            UnityEngine.Object.DestroyImmediate(testigoLejos.gameObject);
            UnityEngine.Object.DestroyImmediate(barrilGo);

            TestLog.Phase("FASE 9 FINALIZADA (19/23)");

            // --- #20 / G2: Ctrl para agacharse ---
            TestLog.Phase("FASE 9 - Tarea #20: Ctrl para agacharse");

            vega.gameObject.SetActive(true);
            vega.Health.Initialize(vega.Id, vega.Health.MaxHealth);
            vega.Brain.CancelOrder();
            vega.Brain.IsPossessedByPlayer = true; // congela la IA durante la medicion
            vega.Motor.SetCrouching(false); // estado limpio, por si algo lo dejo agachado antes

            Physics.SyncTransforms();
            var colliderVega = vega.GetComponent<Collider>();
            float alturaDePie = colliderVega.bounds.size.y;

            vega.Motor.SetCrouching(true);
            Physics.SyncTransforms();
            float alturaAgachado = colliderVega.bounds.size.y;
            Check($"Agachado, la altura del collider baja ({alturaAgachado:0.00} m < {alturaDePie:0.00} m)",
                alturaAgachado < alturaDePie);

            vega.Motor.SetCrouching(false);
            Physics.SyncTransforms();
            float alturaFinal = colliderVega.bounds.size.y;
            Check($"Al soltar Ctrl, la altura vuelve EXACTA a la de pie ({alturaFinal:0.0000} == {alturaDePie:0.0000})",
                Mathf.Abs(alturaFinal - alturaDePie) < 0.0001f);

            // Dispersion: misma racha acumulada (spreadDeg), leida DE PIE y
            // AGACHADO -- SpreadDegEfectivo es el numero real que usa el
            // proximo tiro, sin depender del azar de ApplySpread (Random.Range).
            if (vega.Weapon.CurrentAmmo < 3) { vega.Weapon.Reload(); SimulateSeconds(2f); }
            vega.Weapon.Tick(10f); // decae cualquier racha de una fase anterior a 0
            for (int i = 0; i < 3; i++) { vega.Weapon.Tick(1f); vega.Weapon.TryFire(vega.transform.position, vega.transform.forward); }
            float spreadDePie = vega.Weapon.SpreadDegEfectivo;
            vega.Motor.SetCrouching(true);
            float spreadAgachado = vega.Weapon.SpreadDegEfectivo;
            Check($"Con la misma racha, agachado dispersa menos que de pie ({spreadAgachado:0.00} grados < {spreadDePie:0.00} grados)",
                spreadDePie > 0f && spreadAgachado < spreadDePie);

            vega.Motor.SetCrouching(false);
            vega.Brain.IsPossessedByPlayer = false;
            vega.Brain.CancelOrder();

            TestLog.Phase("FASE 9 FINALIZADA (20/23)");

            // --- #21 / G4: musica de lucha y de estrategia, que cambia sola ---
            TestLog.Phase("FASE 9 - Tarea #21: musica de lucha y de estrategia");

            var camTransform = Camera.main.transform;
            var posOriginalCamara = camTransform.position;
            var rotOriginalCamara = camTransform.rotation;
            // Aislada: bien lejos de cualquier soldado que haya quedado de
            // una fase anterior, para que "sin combate cerca" no dependa
            // de que nadie mas haya quedado atacando por ahi.
            camTransform.position = new Vector3(500f, 20f, 500f);
            camTransform.rotation = Quaternion.identity;

            SimulateSeconds(2f);
            Check($"Sin combate cerca, la musica de lucha queda en 0 ({MusicDirector.GananciaLucha:0.00})",
                MusicDirector.GananciaLucha < 0.01f);

            var testigoLucha = SpawnSoldier(soldierPrefab, "TestigoDeLuchaG4", TeamId.Enemy, RoleType.Enemy,
                camTransform.position + camTransform.forward * 10f, enemyColor, pool, 100);
            var metodoSetState = GetRequiredMethod(typeof(AiBrain), "SetState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            metodoSetState.Invoke(testigoLucha.Brain, new object[] { AiState.Attack });
            testigoLucha.Brain.IsPossessedByPlayer = true; // congela el estado forzado: Tick() nunca corre y no lo pisa

            bool subioRapido = SimulateUntil(() => MusicDirector.GananciaLucha > 0.8f, 2f);
            Check($"Con un enemigo atacando a 10 m, la musica de lucha sube por encima de 0,8 en menos de 2 s ({MusicDirector.GananciaLucha:0.00})",
                subioRapido);

            testigoLucha.Health.TakeDamage(9999, -1);
            bool bajoDeNuevo = SimulateUntil(() => MusicDirector.GananciaLucha < 0.05f, 3f);
            Check($"Al morir el enemigo, la musica de lucha vuelve a bajar ({MusicDirector.GananciaLucha:0.00})",
                bajoDeNuevo);

            UnityEngine.Object.DestroyImmediate(testigoLucha.gameObject);
            camTransform.position = posOriginalCamara;
            camTransform.rotation = rotOriginalCamara;

            TestLog.Phase("FASE 9 FINALIZADA (21/23)");

            // --- #23 / H2: la linea roja de ataque no debe verse en FPS ---
            // El usuario reportaba "algo negro que le apunta al enemigo en
            // FPS": era AttackLineManager, una linea fina pensada para
            // verse desde arriba en RTS. De cerca en FPS (alignment View,
            // el default del LineRenderer, encara cada punto DE CARA A LA
            // CAMARA) un extremo casi pegado a la camara proyecta como un
            // triangulo enorme y oscuro. El arreglo: no dibujarla fuera de
            // RTS (mismo cam.orthographic que ya separa los dos modos en
            // todo el proyecto).
            TestLog.Phase("FASE 9 - Tarea #23 / Bug H2: la linea de ataque no se dibuja en FPS");

            doc.gameObject.SetActive(true);
            doc.Health.Initialize(doc.Id, doc.Health.MaxHealth);
            doc.Brain.CancelOrder();
            doc.Brain.IsPossessedByPlayer = false;

            var testigoH2 = SpawnSoldier(soldierPrefab, "TestigoDeAtaqueH2", TeamId.Enemy, RoleType.Enemy,
                doc.transform.position + new Vector3(4f, 0f, 0f), enemyColor, pool, 100);

            var campoTargetH2 = GetRequiredField(typeof(AiBrain), "target",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var metodoSetStateH2 = GetRequiredMethod(typeof(AiBrain), "SetState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            campoTargetH2.SetValue(doc.Brain, testigoH2);
            metodoSetStateH2.Invoke(doc.Brain, new object[] { AiState.Attack });

            var attackLineManager = UnityEngine.Object.FindAnyObjectByType<AttackLineManager>();
            var metodoUpdateAttackLine = GetRequiredMethod(typeof(AttackLineManager), "Update",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool orthographicOriginal = Camera.main.orthographic;

            Camera.main.orthographic = true; // RTS: control -- la linea tiene que seguir existiendo aca
            metodoUpdateAttackLine.Invoke(attackLineManager, null);
            bool lineaEnRts = GameObject.Find("AttackLine") != null;
            Check($"Control: en RTS la linea de ataque SI se dibuja ({lineaEnRts})", lineaEnRts);

            Camera.main.orthographic = false; // FPS: H2 -- no tiene que existir
            metodoUpdateAttackLine.Invoke(attackLineManager, null);
            bool lineaEnFps = GameObject.Find("AttackLine") != null;
            Check($"H2: en FPS la linea de ataque NO se dibuja ({lineaEnFps})", !lineaEnFps);

            Camera.main.orthographic = orthographicOriginal;
            campoTargetH2.SetValue(doc.Brain, null);
            doc.Brain.CancelOrder();
            UnityEngine.Object.DestroyImmediate(testigoH2.gameObject);

            TestLog.Phase("FASE 9 FINALIZADA (23/23)");
        }

        // ---------------------------------------------------------------
        // Simulación manual (independiente del Update de Unity)
        // ---------------------------------------------------------------
        static void SimStep(float dt)
        {
            // Un solo camino de simulacion: WorldSimulationDriver.Step es
            // EXACTAMENTE lo que corre en Play mode real (Brain cacheado,
            // WorldSystemsRegistry, y ahora tambien TurretAI). Antes esto
            // tenia su propia copia divergente que dejaba a TurretAI sin
            // tickear -- la suite pasaba en verde sin haber ejercitado nunca
            // esa IA.
            WorldSimulationDriver.Step(dt);

            // Los proyectiles SI quedan aparte, y es la unica diferencia
            // real entre los dos caminos: en Play mode cada Projectile se
            // tickea solo via su propio Update() (Projectile.cs), que no
            // corre en Edit mode. Ese self-tick ya es asincrono respecto
            // del resto de la simulacion incluso en Play real, asi que
            // tickearlo en un paso aparte aca no cambia ninguna regla de
            // juego -- es la misma falta de orden garantizado que ya existe.
            profileWatch.Restart();
            var projectiles = Projectile.ActiveInstances.ToArray();
            foreach (var p in projectiles) p.Tick(dt);
            LastProjectileMs = profileWatch.Elapsed.TotalMilliseconds;
        }

        // Mismo criterio que WorldSimulationDriver.LastRebuildMs/etc: un
        // cronometro reusado, sin asignar por llamada, solo para el arnes
        // de benchmark. No cambia la logica de SimStep.
        public static double LastProjectileMs { get; private set; }
        static readonly System.Diagnostics.Stopwatch profileWatch = new System.Diagnostics.Stopwatch();

        static void SimulateSeconds(float totalSeconds, float dt = 0.05f)
        {
            int steps = Mathf.CeilToInt(totalSeconds / dt);
            for (int i = 0; i < steps; i++) SimStep(dt);
        }

        static bool SimulateUntil(Func<bool> condition, float maxSeconds, float dt = 0.05f)
        {
            int steps = Mathf.CeilToInt(maxSeconds / dt);
            for (int i = 0; i < steps; i++)
            {
                if (condition()) return true;
                SimStep(dt);
            }
            return condition();
        }

        static void FullHeal(params Soldier[] soldiers)
        {
            foreach (var s in soldiers) s.Health.Heal(s.Health.MaxHealth);
        }

        // Antes Check() solo emitia un Warn: la suite imprimia "TODAS LAS
        // FASES COMPLETADAS CON EXITO" y salia con codigo 0 aunque fallara
        // TODO. El cartel final no significaba nada. Ahora se cuentan las
        // fallas y RunAll corta en serio.
        static int failedChecks;
        static readonly List<string> failedCheckMessages = new List<string>();

        public static int FailedCheckCount => failedChecks;
        public static IReadOnlyList<string> FailedCheckMessages => failedCheckMessages;

        // BUG REAL del audit del propio runner: GetField/GetMethod/GetProperty
        // devuelven null en silencio si el miembro no existe (por ejemplo tras un
        // rename en el codigo de gameplay), y el .SetValue/.GetValue/.Invoke de la
        // linea siguiente revienta con un NullReferenceException que no dice ni
        // el nombre del miembro ni el tipo. Estos wrappers fallan con un mensaje
        // que dice exactamente que se busco y donde.
        static System.Reflection.FieldInfo GetRequiredField(System.Type type, string name, System.Reflection.BindingFlags flags)
        {
            var fi = type.GetField(name, flags);
            if (fi == null)
                throw new System.InvalidOperationException($"[HeadlessTestRunner] No se encontro el campo '{name}' en {type.FullName} (flags={flags}). Se habra renombrado en el codigo de gameplay?");
            return fi;
        }

        static System.Reflection.MethodInfo GetRequiredMethod(System.Type type, string name, System.Reflection.BindingFlags flags)
        {
            var mi = type.GetMethod(name, flags);
            if (mi == null)
                throw new System.InvalidOperationException($"[HeadlessTestRunner] No se encontro el metodo '{name}' en {type.FullName} (flags={flags}). Se habra renombrado en el codigo de gameplay?");
            return mi;
        }

        static System.Reflection.MethodInfo GetRequiredMethod(System.Type type, string name)
            => GetRequiredMethod(type, name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        static System.Reflection.PropertyInfo GetRequiredProperty(System.Type type, string name, System.Reflection.BindingFlags flags)
        {
            var pi = type.GetProperty(name, flags);
            if (pi == null)
                throw new System.InvalidOperationException($"[HeadlessTestRunner] No se encontro la propiedad '{name}' en {type.FullName} (flags={flags}). Se habra renombrado en el codigo de gameplay?");
            return pi;
        }

        static void Check(string message, bool condition)
        {
            if (condition) { TestLog.Step(message); return; }
            failedChecks++;
            failedCheckMessages.Add(message);
            TestLog.Warn($"{message} -- NO SE CUMPLIO EN EL TIEMPO ESPERADO");
        }

        // ---------------------------------------------------------------
        // Construcción de prefabs y entorno
        // ---------------------------------------------------------------
        // Colores de equipo para el minimapa: fijos, no derivados del color
        // de cuerpo de cada unidad (ver comentario en SpawnSoldier).
        static readonly Color PlayerMinimapColor = new Color(0.25f, 0.55f, 0.98f);
        static readonly Color EnemyMinimapColor = new Color(0.95f, 0.15f, 0.12f);

        static Soldier SpawnSoldier(GameObject prefab, string name, TeamId team, RoleType role, Vector3 position, Color color, ProjectilePool pool, int maxHealth)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;

            var rend = instance.GetComponentInChildren<MeshRenderer>();
            // 230: un material COMPARTIDO por equipo en vez de uno por
            // soldado. Antes eran 50 materiales en memoria y cero batching
            // posible. El color individual va por MaterialPropertyBlock,
            // que no rompe el batch.
            if (rend != null)
            {
                rend.sharedMaterial = GetOrCreateTeamMaterial(color);
                SP.Presentation.CubeFxReactor.WriteTint(rend, color);
            }

            var soldier = instance.GetComponent<Soldier>();
            soldier.Configure(name, team, role, maxHealth);
            soldier.Bootstrap();

            var weapon = instance.GetComponent<WeaponHolder>();
            weapon.Bootstrap();
            weapon.SetPool(pool);
            weapon.SetTuning(team == TeamId.Player ? 26 : 8, team == TeamId.Player ? 0.3f : 0.9f);

            var brain = instance.GetComponent<AiBrain>();
            brain.Bootstrap();

            var fx = instance.GetComponent<CubeFxReactor>();
            fx?.Bootstrap();

            // Marca de alerta sobre la cabeza: solo los enemigos, para que
            // el jugador sepa si ya lo detectaron antes de que empiece a
            // disparar (el componente mismo se auto-desactiva si no es
            // del equipo enemigo, por si algun dia se llama por error).
            if (team == TeamId.Enemy) instance.AddComponent<EnemyAlertIndicatorView>();
            else instance.AddComponent<SquadStateIndicatorView>();

            // El color del cuerpo (color) varia por soldado para
            // distinguirlos entre si de cerca -- pero eso significa que
            // Vega (colorVega = 0.95,0.35,0.30) y los enemigos
            // (colorEnemy = 0.95,0.25,0.20) terminan con un rojo casi
            // identico en el minimapa, donde la lectura tiene que ser
            // instantanea. El minimapa usa un color de EQUIPO fijo,
            // desacoplado del color de cuerpo, y un radio mayor para los
            // enemigos: dos señales independientes, no solo una.
            var minimapColor = team == TeamId.Player ? PlayerMinimapColor : EnemyMinimapColor;
            float minimapRadius = team == TeamId.Player ? 1.6f : 2f;
            var minimapIcon = MinimapIcon.Spawn(instance.transform, minimapColor, GetOrCreateMinimapLayer(), minimapRadius);
            // Solo la escuadra propia necesita mostrar hacia donde mira:
            // es la unica que el jugador puede llegar a controlar, y
            // sumarselo tambien a los enemigos ensuciaria el minimapa sin
            // aportar nada que el jugador pueda usar.
            if (team == TeamId.Player) minimapIcon.EnableDirectionMarker(GetOrCreateMinimapLayer(), minimapRadius);
            else minimapIcon.EnableFogOfWar();

            var healthBar = instance.GetComponentInChildren<HealthBarView>(true);
            healthBar?.Bootstrap();

            return soldier;
        }

        static GameObject BuildAndSaveSoldierPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "P_Soldier_Base";
            root.transform.localScale = new Vector3(0.9f, 1.6f, 0.9f);

            var eye = new GameObject("EyeAnchor").transform;
            eye.SetParent(root.transform);
            eye.localPosition = new Vector3(0f, 0.3f, 0.3f);
            eye.localRotation = Quaternion.identity;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform);
            muzzle.localPosition = new Vector3(0f, 0.1f, 0.55f);

            // Arma visible: un cubo chico pegado al costado del cuerpo, para
            // que se note a simple vista qué arma tiene equipada cada uno
            // (mismo color que sus proyectiles) tanto en FPS como en RTS.
            var weaponVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weaponVisual.name = "WeaponVisual";
            weaponVisual.transform.SetParent(root.transform, false);
            var wvParentScale = root.transform.localScale;
            weaponVisual.transform.localScale = new Vector3(0.15f / wvParentScale.x, 0.15f / wvParentScale.y, 0.55f / wvParentScale.z);
            weaponVisual.transform.localPosition = new Vector3(0.32f / wvParentScale.x, 0f, 0.15f / wvParentScale.z);
            var wvCol = weaponVisual.GetComponent<Collider>();
            if (wvCol != null) UnityEngine.Object.DestroyImmediate(wvCol);
            var wvRenderer = weaponVisual.GetComponent<MeshRenderer>();
            wvRenderer.sharedMaterial = CreateFlatMaterial(new Color(0.55f, 0.68f, 0.78f));

            root.AddComponent<Health>();
            root.AddComponent<SoldierMotor>();

            var wh = root.AddComponent<WeaponHolder>();
            wh.Muzzle = muzzle;
            wh.WeaponVisualRenderer = wvRenderer;

            var soldierComp = root.AddComponent<Soldier>();
            soldierComp.EyeAnchor = eye;

            root.AddComponent<AiBrain>();
            root.AddComponent<AudioSource>();
            root.AddComponent<CubeFxReactor>();

            BuildHealthBar(root.transform);

            Directory.CreateDirectory("Assets/_Project/Prefabs");
            string path = "Assets/_Project/Prefabs/P_Soldier_Base.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildHealthBar(Transform root)
        {
            var anchor = new GameObject("HealthBarAnchor").transform;
            anchor.SetParent(root);
            anchor.localPosition = new Vector3(0f, 0.62f, 0f);
            anchor.localRotation = Quaternion.identity;

            // El cubo padre tiene escala no uniforme (0.9, 1.6, 0.9): sin
            // esto la barra saldría deformada.
            var parentScale = root.localScale;
            anchor.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

            var canvasGO = new GameObject("HealthBarCanvas", typeof(Canvas));
            canvasGO.transform.SetParent(anchor, false);
            canvasGO.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = canvasGO.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(100f, 14f);

            var bgGO = new GameObject("BG", typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);
            bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill", typeof(Image));
            fillGO.transform.SetParent(canvasGO.transform, false);
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.color = new Color(0.35f, 0.9f, 0.4f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            var fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);

            canvasGO.AddComponent<HealthBarView>();
        }

        static Projectile BuildAndSaveProjectilePrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "P_Projectile";
            go.transform.localScale = Vector3.one * 0.2f;

            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);

            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null) rend.sharedMaterial = CreateFlatMaterial(new Color(1f, 0.92f, 0.35f));

            go.AddComponent<Projectile>();

            Directory.CreateDirectory("Assets/_Project/Prefabs");
            string path = "Assets/_Project/Prefabs/P_Projectile.prefab";
            var savedGO = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return savedGO.GetComponent<Projectile>();
        }

        static GameObject BuildAndSaveVehiclePrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "P_Vehicle_Blindado";
            root.transform.localScale = new Vector3(2.2f, 1.4f, 3.6f);
            root.AddComponent<Vehicle>();
            root.AddComponent<VehicleMotor>();
            root.AddComponent<VehicleBrain>();

            var driverEye = new GameObject("DriverEye").transform;
            driverEye.SetParent(root.transform, false);
            // Y=0.3 (mundo 0.42, escala 1.4 del chasis) quedaba bien
            // ADENTRO del cubo sólido del chasis (techo real en mundo
            // 0.7): la cámara del conductor terminaba con la lente
            // literalmente atravesando la carrocería, un quilombo de
            // recorte cerca del near clip que tapaba toda la pantalla con
            // un triángulo naranja. Subida por encima del techo (mundo
            // ~0.91) y corrida cerca del capot (mundo ~1.62, adentro
            // todavía del límite 1.8) para que la vista quede despejada,
            // como sentado arriba en vez de adentro del bloque.
            driverEye.localPosition = new Vector3(0f, 0.65f, 0.45f);

            // TurretPivot neutraliza la escala no uniforme del chasis (2.2/1.4/3.6)
            // para que sus hijos (torreta, mira, boca) usen unidades normales.
            var turretPivot = new GameObject("TurretPivot");
            turretPivot.transform.SetParent(root.transform, false);
            turretPivot.transform.localPosition = new Vector3(0f, 0.36f, -0.08f);
            var parentScale = root.transform.localScale;
            turretPivot.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

            var turretVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            turretVisual.name = "TurretVisual";
            turretVisual.transform.SetParent(turretPivot.transform, false);
            turretVisual.transform.localScale = new Vector3(0.6f, 0.5f, 0.9f);
            var tvCol = turretVisual.GetComponent<Collider>();
            if (tvCol != null) UnityEngine.Object.DestroyImmediate(tvCol);

            // Cañón: un cubo bien fino y largo que sí se nota como "el
            // arma que dispara" (antes solo estaba TurretVisual, un cubo
            // corto que además quedaba pintado del mismo color que todo
            // el chasis -- imposible de distinguir a simple vista). Se
            // excluye del repintado uniforme en SpawnVehicle por nombre.
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "TurretBarrel";
            barrel.transform.SetParent(turretPivot.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            barrel.transform.localScale = new Vector3(0.16f, 0.16f, 0.9f);
            var barrelCol = barrel.GetComponent<Collider>();
            if (barrelCol != null) UnityEngine.Object.DestroyImmediate(barrelCol);
            // OJO: el color se pinta recién en SpawnVehicle, NO acá. Un
            // Material nuevo asignado en este punto es un objeto de
            // memoria transitorio -- PrefabUtility.SaveAsPrefabAsset no lo
            // adopta como sub-asset del prefab, así que al destruir este
            // `root` de armado la referencia queda rota (sharedMaterial
            // NULL) y cualquier .color sobre ella tira NullReference la
            // primera vez que se instancia el prefab y algo lo toca (por
            // ejemplo, subir alguien y disparar RefreshOccupancyColor()).
            // El material default del primitivo (el mismo que ya usan
            // root y TurretVisual) SÍ sobrevive porque es un asset real,
            // no uno creado en memoria.

            var turret = turretPivot.AddComponent<TurretWeapon>();
            turretPivot.AddComponent<TurretAI>();

            var gunnerEye = new GameObject("GunnerEye").transform;
            gunnerEye.SetParent(turretPivot.transform, false);
            gunnerEye.localPosition = new Vector3(0f, 0.4f, 0f);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turretPivot.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.8f);
            turret.Muzzle = muzzle;

            Directory.CreateDirectory("Assets/_Project/Prefabs");
            string path = "Assets/_Project/Prefabs/P_Vehicle_Blindado.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static Vehicle SpawnVehicle(GameObject prefab, Vector3 position, Color color, ProjectilePool pool)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Vehiculo_Blindado";
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;
            if (instance.GetComponent<AudioSource>() == null) instance.AddComponent<AudioSource>();
            instance.AddComponent<VehicleAudioFeedback>();

            var mat = CreateFlatMaterial(color);
            transientRuntimeAssets.Add(mat);
            // El cañón queda afuera del repintado: necesita mantener SU
            // propio color oscuro (fijo) para leerse como "el arma" en vez
            // de camuflarse con el chasis, sea cual sea el color que le
            // toque a este vehículo en particular. Se pinta acá (objeto de
            // escena ya instanciado, no el prefab) para que el Material
            // nuevo sobreviva -- ver el comentario en
            // BuildAndSaveVehiclePrefab sobre por qué no se puede pintar
            // en el momento de armar el prefab.
            Renderer barrelRend = null;
            foreach (var rend in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (rend.gameObject.name == "TurretBarrel") barrelRend = rend;
                else rend.sharedMaterial = mat;
            }
            if (barrelRend != null) { var barrelMat = CreateFlatMaterial(new Color(0.12f, 0.12f, 0.13f)); transientRuntimeAssets.Add(barrelMat); barrelRend.sharedMaterial = barrelMat; }

            // Va despues de repintar el chasis por prolijidad, pero el orden
            // en realidad da igual: Awake NO corre al hacer AddComponent en
            // Edit mode (esta clase no tiene [ExecuteAlways]), asi que el
            // cacheo del color base ocurre recien al entrar a Play, cuando
            // el material pintado ya esta serializado en la escena.
            instance.AddComponent<VehicleFxReactor>();

            var turret = instance.GetComponentInChildren<TurretWeapon>();
            turret.SetPool(pool);

            MinimapIcon.Spawn(instance.transform, color, GetOrCreateMinimapLayer(), 2.4f);

            return instance.GetComponent<Vehicle>();
        }

        // El bloque de un toggle son ~28 lineas identicas salvo nombre,
        // etiqueta y altura. Con el segundo toggle ya no valia la pena
        // repetirlas. PauseController los busca por "<name>_Toggle".
        static Toggle BuildLabeledToggle(Transform parent, string name, string label, float y, bool initialValue)
        {
            var toggleGO = new GameObject(name + "_Toggle", typeof(Toggle));
            toggleGO.transform.SetParent(parent, false);
            var rt = toggleGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-140f, y);
            rt.sizeDelta = new Vector2(24f, 24f);

            var bgGO = new GameObject("Background", typeof(Image));
            bgGO.transform.SetParent(toggleGO.transform, false);
            bgGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f);
            StretchFull(bgGO.GetComponent<RectTransform>());

            var checkGO = new GameObject("Checkmark", typeof(Image));
            checkGO.transform.SetParent(bgGO.transform, false);
            checkGO.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.45f);
            StretchFull(checkGO.GetComponent<RectTransform>());

            var toggle = toggleGO.GetComponent<Toggle>();
            toggle.targetGraphic = bgGO.GetComponent<Image>();
            toggle.graphic = checkGO.GetComponent<Image>();
            toggle.isOn = initialValue;

            var labelGO = new GameObject(name + "_Label", typeof(Text));
            labelGO.transform.SetParent(parent, false);
            var labelTxt = labelGO.GetComponent<Text>();
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = Color.white;
            labelTxt.fontSize = FontCuerpo;
            labelTxt.text = label;
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = new Vector2(20f, y);
            labelRt.sizeDelta = new Vector2(260f, 24f);

            return toggle;
        }

        // Panel de remapeo (item 208). Sin esta pantalla el remapeo existia
        // solo como API: seguia haciendo falta editar codigo o PlayerPrefs a
        // mano, o sea que el item quedaba a medias.
        static void BuildRebindPanel(Transform parent, SP.Presentation.PauseController pauseController)
        {
            var panelGO = new GameObject("RebindPanel", typeof(Image));
            panelGO.transform.SetParent(parent, false);
            panelGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 620f);

            var titleGO = new GameObject("Title", typeof(Text));
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleTxt = titleGO.GetComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;
            titleTxt.fontSize = FontSubtitulo;
            titleTxt.text = "REMAPEAR CONTROLES";
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 260f);
            titleRt.sizeDelta = new Vector2(680f, 40f);

            // Dos columnas: 18 acciones en una sola no entran en pantalla.
            var acciones = new System.Collections.Generic.List<string>(SP.Player.KeyBindings.AllActions);
            var botones = new Button[acciones.Count];
            var etiquetas = new Text[acciones.Count];
            for (int i = 0; i < acciones.Count; i++)
            {
                int col = i / 9;
                int row = i % 9;
                float x = col == 0 ? -175f : 175f;
                float y = 200f - row * 46f;
                var b = BuildUIButton(panelGO.transform, "Rebind_" + acciones[i], "", new Vector2(x, y), new Color(0.18f, 0.2f, 0.26f));
                var brt = b.GetComponent<RectTransform>();
                brt.sizeDelta = new Vector2(330f, 38f);
                botones[i] = b;
                etiquetas[i] = b.GetComponentInChildren<Text>();
                if (etiquetas[i] != null) etiquetas[i].fontSize = FontChico;
            }

            var view = panelGO.AddComponent<SP.UI.KeyRebindView>();
            view.Bind(botones, etiquetas, acciones.ToArray());

            var resetBtn = BuildUIButton(panelGO.transform, "ResetButton", "RESTAURAR", new Vector2(-110f, -250f), new Color(0.55f, 0.4f, 0.3f));
            resetBtn.onClick.AddListener(pauseController.OnRebindResetClicked);
            var backBtn = BuildUIButton(panelGO.transform, "BackButton", "VOLVER", new Vector2(110f, -250f), new Color(0.5f, 0.5f, 0.5f));
            backBtn.onClick.AddListener(pauseController.OnRebindBackClicked);

            panelGO.SetActive(false);
        }

        // Materiales/RenderTexture creados para objetos de ESCENA (no assets
        // persistidos como los prefabs) — huerfanos tras EditorSceneManager.NewScene
        // si no se destruyen a mano. Se limpian al arrancar cada rebuild.
        static readonly List<UnityEngine.Object> transientRuntimeAssets = new List<UnityEngine.Object>();

        static void DestroyTransientRuntimeAssets()
        {
            foreach (var obj in transientRuntimeAssets)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            transientRuntimeAssets.Clear();
        }

        // Un material por COLOR pedido (o sea, por equipo), cacheado. La
        // clave es el color exacto: dos equipos distintos siguen teniendo
        // materiales distintos, pero los 50 soldados de un mismo equipo
        // comparten uno solo.
        static readonly System.Collections.Generic.Dictionary<Color, Material> teamMaterials =
            new System.Collections.Generic.Dictionary<Color, Material>();

        static Material GetOrCreateTeamMaterial(Color color)
        {
            Material mat;
            if (teamMaterials.TryGetValue(color, out mat) && mat != null) return mat;
            mat = CreateFlatMaterial(color);
            teamMaterials[color] = mat;
            return mat;
        }

        // Item 54: habia QUINCE tamaños de fuente distintos repartidos por
        // el HUD (12,13,14,15,16,18,20,22,24,28,30,40,44,48,72), muchos
        // separados por un solo punto -- diferencias invisibles que no
        // comunicaban ninguna jerarquia, solo inconsistencia. Siete
        // niveles con nombre, y cada texto elige por lo que ES, no por un
        // numero suelto.
        const int FontDisplay = 72;      // resultado de la partida
        const int FontTitulo = 44;       // titulo de pantalla o panel grande
        const int FontEncabezado = 28;   // encabezado de seccion
        const int FontSubtitulo = 22;    // banner, aviso destacado
        const int FontCuerpo = 18;       // texto normal de UI y botones
        const int FontChico = 14;        // etiquetas, listas, valores
        const int FontMicro = 12;        // notas al pie, leyendas

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(160f, 1f, 160f);
            var groundMat = CreateFlatMaterial(new Color(0.82f, 0.85f, 0.88f)); transientRuntimeAssets.Add(groundMat); ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
        }

        static void BuildObstacles()
        {
            // Misma altura para los 4 (antes iban de 1.5 a 3, se veían
            // desparejos); todos parados sobre el piso (y = mitad de la altura).
            const float height = 2f;
            Vector3[] positionsXZ =
            {
                new Vector3(6f, 0f, 3f),
                new Vector3(-6f, 0f, 4f),
                new Vector3(4f, 0f, -6f),
                new Vector3(-5f, 0f, -3f)
            };

            for (int i = 0; i < positionsXZ.Length; i++)
            {
                var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
                o.name = $"Obstaculo_{i + 1}";
                o.transform.position = new Vector3(positionsXZ[i].x, height * 0.5f, positionsXZ[i].z);
                o.transform.localScale = new Vector3(2f, height, 2f);
                var obsMat = CreateFlatMaterial(new Color(0.93f, 0.78f, 0.55f)); transientRuntimeAssets.Add(obsMat); o.GetComponent<MeshRenderer>().sharedMaterial = obsMat;
                o.AddComponent<ObstacleMarker>();
            }
        }

        // Bidones livianos sobre el camino que hace el vehiculo en la
        // fase 4: son lo unico que le da al tanque algo que alterar al
        // pasar, en vez de atravesar el escenario sin dejar rastro.
        static void BuildLightProps()
        {
            Vector3[] spots = { new Vector3(8f, 0f, -4f), new Vector3(11f, 0f, -3f), new Vector3(9.5f, 0f, -6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prop.name = $"Bidon_{i + 1}";
                prop.transform.position = new Vector3(spots[i].x, 0.45f, spots[i].z);
                prop.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
                var lightMat = CreateFlatMaterial(new Color(0.45f, 0.55f, 0.35f)); transientRuntimeAssets.Add(lightMat); prop.GetComponent<MeshRenderer>().sharedMaterial = lightMat;
                var col = prop.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                prop.AddComponent<LightProp>();
            }
        }

        static List<WeaponPickup> BuildWeaponPickups()
        {
            var defs = new (string name, WeaponKind kind, int dmg, float cooldown, Color color, Vector3 pos)[]
            {
                ("Arma_Rifle",  WeaponKind.Rifle,  26, 0.30f, new Color(0.55f, 0.68f, 0.78f), new Vector3(3f, 0.4f, -3f)),
                ("Arma_Pistola",WeaponKind.Pistol, 14, 0.15f, new Color(0.95f, 0.88f, 0.20f), new Vector3(4f, 0.4f, -3f)),
                ("Arma_Pesada", WeaponKind.Heavy,  50, 0.80f, new Color(0.85f, 0.35f, 0.10f), new Vector3(5f, 0.4f, -3f)),
            };

            var list = new List<WeaponPickup>();
            foreach (var d in defs)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = d.name;
                go.transform.position = d.pos;
                go.transform.localScale = Vector3.one * 0.5f;
                var pickupMat = CreateFlatMaterial(d.color); transientRuntimeAssets.Add(pickupMat); go.GetComponent<MeshRenderer>().sharedMaterial = pickupMat;

                var pickup = go.AddComponent<WeaponPickup>();
                pickup.Configure(d.kind, d.dmg, d.cooldown, d.color);
                list.Add(pickup);
            }
            return list;
        }

        // 4 enemigos patrullando en loop por waypoints fijos, cada uno con
        // su ronda dibujada con un LineRenderer de verdad (se ve en Game
        // view y en las capturas, no solo en el editor como un Gizmo).
        static List<Soldier> BuildPatrolEnemies(GameObject soldierPrefab, ProjectilePool pool, Color enemyColor)
        {
            var routes = new[]
            {
                new[] { new Vector3(20f, 0.8f, 20f), new Vector3(30f, 0.8f, 20f), new Vector3(30f, 0.8f, 30f), new Vector3(20f, 0.8f, 30f) },
                new[] { new Vector3(-20f, 0.8f, 20f), new Vector3(-30f, 0.8f, 20f), new Vector3(-30f, 0.8f, 30f), new Vector3(-20f, 0.8f, 30f) },
                new[] { new Vector3(20f, 0.8f, -20f), new Vector3(30f, 0.8f, -20f), new Vector3(30f, 0.8f, -30f), new Vector3(20f, 0.8f, -30f) },
                new[] { new Vector3(-20f, 0.8f, -20f), new Vector3(-30f, 0.8f, -20f), new Vector3(-30f, 0.8f, -30f), new Vector3(-20f, 0.8f, -30f) },
            };

            var enemies = new List<Soldier>();
            for (int i = 0; i < routes.Length; i++)
            {
                var enemy = SpawnSoldier(soldierPrefab, $"Enemigo_Patrulla_{i + 1}", TeamId.Enemy, RoleType.Enemy, routes[i][0], enemyColor, pool, 180);
                enemy.GetComponent<AiBrain>().SetPatrolRoute(routes[i]);
                PatrolRouteLine.Spawn(routes[i], new Color(0.95f, 0.6f, 0.2f));
                enemies.Add(enemy);
            }
            return enemies;
        }

        static void BuildUI(List<Soldier> squad, Camera cam, PlayerBrain playerBrain)
        {
            // Hace falta para que los botones de pausa/config/victoria-
            // derrota reciban clicks de verdad: InputSystemUIInputModule
            // (no el StandaloneInputModule viejo), porque el proyecto
            // tiene Active Input Handling = solo "Input System" nuevo.
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRootRef = canvasGO.transform;
            var canvas = canvasGO.GetComponent<Canvas>();
            // ScreenSpaceCamera (no Overlay): así la UI queda compuesta DENTRO
            // del render de la cámara y aparece en las capturas de pantalla,
            // que graban el render target de la cámara, no el overlay final.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            // Muy pegado al near clip: casi nada de geometría del mundo
            // puede meterse delante y tapar la UI cuando el jugador choca
            // contra algo (antes estaba a 1 unidad, una distancia de choque
            // habitual).
            canvas.planeDistance = 0.35f;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hudScalerRef = scaler;

            var crossGO = new GameObject("Crosshair", typeof(Image));
            crossGO.transform.SetParent(canvasGO.transform, false);
            var crossImg = crossGO.GetComponent<Image>();
            crossImg.color = Color.white;
            var crt = crossGO.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(6f, 6f);

            var promptGO = new GameObject("PromptText", typeof(Text));
            promptGO.transform.SetParent(canvasGO.transform, false);
            var promptTxt = promptGO.GetComponent<Text>();
            promptTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptTxt.alignment = TextAnchor.MiddleCenter;
            promptTxt.color = Color.white;
            promptTxt.fontSize = FontSubtitulo;
            var prt = promptGO.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, -40f);
            prt.sizeDelta = new Vector2(420f, 30f);
            AddOutline(promptTxt);

            // Aviso de "SIN MUNICION"/"RECARGANDO" bajo el cartel de
            // punteria: mismo centro horizontal, un poco mas abajo para
            // no pisarse con el prompt contextual.
            var ammoWarnGO = new GameObject("AmmoWarningText", typeof(Text));
            ammoWarnGO.transform.SetParent(canvasGO.transform, false);
            var ammoWarnTxt = ammoWarnGO.GetComponent<Text>();
            ammoWarnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ammoWarnTxt.alignment = TextAnchor.MiddleCenter;
            ammoWarnTxt.color = new Color(0.95f, 0.55f, 0.2f);
            ammoWarnTxt.fontSize = FontSubtitulo;
            ammoWarnTxt.fontStyle = FontStyle.Bold;
            var awrt = ammoWarnGO.GetComponent<RectTransform>();
            awrt.anchorMin = awrt.anchorMax = new Vector2(0.5f, 0.5f);
            awrt.anchoredPosition = new Vector2(0f, -70f);
            awrt.sizeDelta = new Vector2(320f, 30f);
            AddOutline(ammoWarnTxt);
            ammoWarnGO.SetActive(false);

            var aimUIGO = new GameObject("AimUI", typeof(RectTransform), typeof(AimUI));
            aimUIGO.transform.SetParent(canvasGO.transform, false);
            var aimUi = aimUIGO.GetComponent<AimUI>();
            aimUi.Bind(promptTxt, crossImg);
            aimUi.BindAmmoWarning(ammoWarnTxt);
            aimUi.Initialize();
            aimUiRef = aimUi;

            // Panel de info al apuntar a un aliado (vida/arma/especialidad),
            // justo arriba del texto de instrucciones para que no se pisen.
            var soldierInfoGO = new GameObject("SoldierInfoPanel", typeof(Image));
            soldierInfoGO.transform.SetParent(canvasGO.transform, false);
            soldierInfoGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            var siRt = soldierInfoGO.GetComponent<RectTransform>();
            siRt.anchorMin = new Vector2(0.5f, 0f);
            siRt.anchorMax = new Vector2(0.5f, 0f);
            siRt.pivot = new Vector2(0.5f, 0f);
            siRt.anchoredPosition = new Vector2(0f, 66f);
            siRt.sizeDelta = new Vector2(560f, 30f);

            var siTextGO = new GameObject("Text", typeof(Text));
            siTextGO.transform.SetParent(soldierInfoGO.transform, false);
            var siText = siTextGO.GetComponent<Text>();
            siText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            siText.alignment = TextAnchor.MiddleCenter;
            siText.color = Color.white;
            siText.fontSize = FontCuerpo;
            StretchFull(siTextGO.GetComponent<RectTransform>());

            aimUi.BindSoldierInfo(soldierInfoGO, siText);
            soldierInfoGO.SetActive(false);

            // Panel de info al apuntar a un vehículo: 4 cuadrados de asiento
            // (verde = libre, gris muy oscuro = ocupado), mismo lugar que el
            // panel de soldado (nunca se muestran los dos a la vez).
            var vehicleInfoGO = new GameObject("VehicleInfoPanel", typeof(Image));
            vehicleInfoGO.transform.SetParent(canvasGO.transform, false);
            vehicleInfoGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            var viRt = vehicleInfoGO.GetComponent<RectTransform>();
            viRt.anchorMin = new Vector2(0.5f, 0f);
            viRt.anchorMax = new Vector2(0.5f, 0f);
            viRt.pivot = new Vector2(0.5f, 0f);
            viRt.anchoredPosition = new Vector2(0f, 66f);
            viRt.sizeDelta = new Vector2(260f, 34f);

            var seatLabels = new[] { "Conductor", "Pasajero 1", "Pasajero 2", "Artillero" };
            var seatSquares = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var seatGO = new GameObject($"Seat_{seatLabels[i]}", typeof(Image));
                seatGO.transform.SetParent(vehicleInfoGO.transform, false);
                var seatImg = seatGO.GetComponent<Image>();
                seatImg.color = new Color(0.15f, 0.15f, 0.16f);
                var seatRt = seatGO.GetComponent<RectTransform>();
                seatRt.anchorMin = seatRt.anchorMax = new Vector2(0f, 0.5f);
                seatRt.pivot = new Vector2(0f, 0.5f);
                seatRt.sizeDelta = new Vector2(26f, 26f);
                seatRt.anchoredPosition = new Vector2(14f + i * 60f, 0f);
                seatSquares[i] = seatImg;

                var seatLabelGO = new GameObject("Label", typeof(Text));
                seatLabelGO.transform.SetParent(vehicleInfoGO.transform, false);
                var seatLabelTxt = seatLabelGO.GetComponent<Text>();
                seatLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                seatLabelTxt.alignment = TextAnchor.UpperCenter;
                seatLabelTxt.color = Color.white;
                seatLabelTxt.fontSize = FontMicro;
                seatLabelTxt.text = seatLabels[i].Replace("Pasajero ", "Pas.");
                var seatLabelRt = seatLabelGO.GetComponent<RectTransform>();
                seatLabelRt.anchorMin = seatLabelRt.anchorMax = new Vector2(0f, 0.5f);
                seatLabelRt.pivot = new Vector2(0.5f, 1f);
                seatLabelRt.sizeDelta = new Vector2(56f, 14f);
                seatLabelRt.anchoredPosition = new Vector2(14f + i * 60f + 13f, -14f);
            }

            aimUi.BindVehicleInfo(vehicleInfoGO, seatSquares);
            vehicleInfoGO.SetActive(false);

            // HUD de arma: qué arma, munición y barra de recarga/enfriamiento.
            var wsGO = new GameObject("WeaponStatus", typeof(Image), typeof(WeaponStatusView));
            wsGO.transform.SetParent(canvasGO.transform, false);
            wsGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            var wsRt = wsGO.GetComponent<RectTransform>();
            wsRt.anchorMin = new Vector2(1f, 0f);
            wsRt.anchorMax = new Vector2(1f, 0f);
            wsRt.pivot = new Vector2(1f, 0f);
            wsRt.anchoredPosition = new Vector2(-16f, 72f);
            wsRt.sizeDelta = new Vector2(220f, 46f);

            var wsTextGO = new GameObject("Text", typeof(Text));
            wsTextGO.transform.SetParent(wsGO.transform, false);
            var wsText = wsTextGO.GetComponent<Text>();
            wsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wsText.alignment = TextAnchor.UpperCenter;
            wsText.color = Color.white;
            wsText.fontSize = FontChico;
            var wsTextRt = wsTextGO.GetComponent<RectTransform>();
            wsTextRt.anchorMin = new Vector2(0f, 0f);
            wsTextRt.anchorMax = new Vector2(1f, 1f);
            wsTextRt.offsetMin = new Vector2(4f, 12f);
            wsTextRt.offsetMax = new Vector2(-4f, -4f);

            var wsBarBgGO = new GameObject("BarBG", typeof(Image));
            wsBarBgGO.transform.SetParent(wsGO.transform, false);
            wsBarBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var wsBarBgRt = wsBarBgGO.GetComponent<RectTransform>();
            wsBarBgRt.anchorMin = new Vector2(0f, 0f);
            wsBarBgRt.anchorMax = new Vector2(1f, 0f);
            wsBarBgRt.pivot = new Vector2(0f, 0f);
            wsBarBgRt.anchoredPosition = new Vector2(6f, 6f);
            wsBarBgRt.sizeDelta = new Vector2(-12f, 8f);

            var wsBarFillGO = new GameObject("BarFill", typeof(Image));
            wsBarFillGO.transform.SetParent(wsBarBgGO.transform, false);
            var wsBarFillImg = wsBarFillGO.GetComponent<Image>();
            wsBarFillImg.color = new Color(0.4f, 0.85f, 0.45f);
            wsBarFillImg.type = Image.Type.Filled;
            wsBarFillImg.fillMethod = Image.FillMethod.Horizontal;
            wsBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            wsBarFillImg.fillAmount = 1f;
            StretchFull(wsBarFillGO.GetComponent<RectTransform>());

            var weaponStatusView = wsGO.GetComponent<WeaponStatusView>();
            weaponStatusView.Bind(wsText, wsBarFillImg);
            weaponStatusRef = weaponStatusView;

            // HUD de vida propia: justo ENCIMA del de arma, en la misma
            // columna del rincón inferior derecho. Antes no existía --
            // no había manera de saber cuánta vida te quedaba salvo
            // buscarte en la lista de la escuadra.
            var phGO = new GameObject("PlayerHealth", typeof(Image), typeof(PlayerHealthView));
            phGO.transform.SetParent(canvasGO.transform, false);
            phGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            var phRt = phGO.GetComponent<RectTransform>();
            phRt.anchorMin = new Vector2(1f, 0f);
            phRt.anchorMax = new Vector2(1f, 0f);
            phRt.pivot = new Vector2(1f, 0f);
            phRt.anchoredPosition = new Vector2(-16f, 124f);
            phRt.sizeDelta = new Vector2(220f, 46f);

            var phTextGO = new GameObject("Text", typeof(Text));
            phTextGO.transform.SetParent(phGO.transform, false);
            var phText = phTextGO.GetComponent<Text>();
            phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phText.alignment = TextAnchor.UpperCenter;
            phText.color = Color.white;
            phText.fontSize = FontChico;
            var phTextRt = phTextGO.GetComponent<RectTransform>();
            phTextRt.anchorMin = new Vector2(0f, 0f);
            phTextRt.anchorMax = new Vector2(1f, 1f);
            phTextRt.offsetMin = new Vector2(4f, 12f);
            phTextRt.offsetMax = new Vector2(-4f, -4f);

            var phBarBgGO = new GameObject("BarBG", typeof(Image));
            phBarBgGO.transform.SetParent(phGO.transform, false);
            phBarBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var phBarBgRt = phBarBgGO.GetComponent<RectTransform>();
            phBarBgRt.anchorMin = new Vector2(0f, 0f);
            phBarBgRt.anchorMax = new Vector2(1f, 0f);
            phBarBgRt.pivot = new Vector2(0f, 0f);
            phBarBgRt.anchoredPosition = new Vector2(6f, 6f);
            phBarBgRt.sizeDelta = new Vector2(-12f, 8f);

            var phBarFillGO = new GameObject("BarFill", typeof(Image));
            phBarFillGO.transform.SetParent(phBarBgGO.transform, false);
            var phBarFillImg = phBarFillGO.GetComponent<Image>();
            phBarFillImg.color = new Color(0.35f, 0.85f, 0.4f);
            phBarFillImg.type = Image.Type.Filled;
            phBarFillImg.fillMethod = Image.FillMethod.Horizontal;
            phBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            phBarFillImg.fillAmount = 1f;
            StretchFull(phBarFillGO.GetComponent<RectTransform>());

            var playerHealthView = phGO.GetComponent<PlayerHealthView>();
            playerHealthView.Bind(phText, phBarFillImg);
            playerHealthRef = playerHealthView;

            // Estado de misión: arriba y al centro, el único panel que se
            // ve igual en FPS y en RTS (no es info de puntería, es el
            // marcador de la partida).
            var msGO = new GameObject("MissionStatus", typeof(Image), typeof(MissionStatusView));
            msGO.transform.SetParent(canvasGO.transform, false);
            msGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.8f);
            var msRt = msGO.GetComponent<RectTransform>();
            msRt.anchorMin = new Vector2(0.5f, 1f);
            msRt.anchorMax = new Vector2(0.5f, 1f);
            msRt.pivot = new Vector2(0.5f, 1f);
            msRt.anchoredPosition = new Vector2(0f, -14f);
            msRt.sizeDelta = new Vector2(360f, 34f);

            var msTextGO = new GameObject("Text", typeof(Text));
            msTextGO.transform.SetParent(msGO.transform, false);
            var msText = msTextGO.GetComponent<Text>();
            msText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            msText.alignment = TextAnchor.MiddleCenter;
            msText.color = Color.white;
            msText.fontSize = FontChico;
            StretchFull(msTextGO.GetComponent<RectTransform>());
            missionStatusRef = msGO.GetComponent<MissionStatusView>();

            // Contador de seleccionados: justo debajo del estado de
            // mision, destacado y propio en vez de perdido dentro del
            // texto de ayuda de RTS.
            var selCountGO = new GameObject("SelectionCount", typeof(Image), typeof(SelectionCountView));
            selCountGO.transform.SetParent(canvasGO.transform, false);
            selCountGO.GetComponent<Image>().color = new Color(0.85f, 0.65f, 0.1f, 0.85f);
            var selCountRt = selCountGO.GetComponent<RectTransform>();
            selCountRt.anchorMin = new Vector2(0.5f, 1f);
            selCountRt.anchorMax = new Vector2(0.5f, 1f);
            selCountRt.pivot = new Vector2(0.5f, 1f);
            selCountRt.anchoredPosition = new Vector2(0f, -54f);
            selCountRt.sizeDelta = new Vector2(200f, 28f);

            var selCountTextGO = new GameObject("Text", typeof(Text));
            selCountTextGO.transform.SetParent(selCountGO.transform, false);
            var selCountText = selCountTextGO.GetComponent<Text>();
            selCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            selCountText.alignment = TextAnchor.MiddleCenter;
            selCountText.color = Color.black;
            selCountText.fontSize = FontChico;
            selCountText.fontStyle = FontStyle.Bold;
            StretchFull(selCountTextGO.GetComponent<RectTransform>());
            var selCountView = selCountGO.GetComponent<SelectionCountView>();
            selCountView.Bind(selCountText);
            selectionCountRef = selCountView;

            // HUD del vehículo: mismo rincón que el de arma (nunca se
            // muestran los dos juntos), pero con velocímetro, barra de
            // vida del tanque y quién es el artillero.
            var vsGO = new GameObject("VehicleStatus", typeof(Image), typeof(VehicleStatusView));
            vsGO.transform.SetParent(canvasGO.transform, false);
            vsGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            var vsRt = vsGO.GetComponent<RectTransform>();
            vsRt.anchorMin = new Vector2(1f, 0f);
            vsRt.anchorMax = new Vector2(1f, 0f);
            vsRt.pivot = new Vector2(1f, 0f);
            vsRt.anchoredPosition = new Vector2(-16f, 72f);
            vsRt.sizeDelta = new Vector2(220f, 88f);

            var vsSpeedGO = new GameObject("SpeedText", typeof(Text));
            vsSpeedGO.transform.SetParent(vsGO.transform, false);
            var vsSpeedTxt = vsSpeedGO.GetComponent<Text>();
            vsSpeedTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vsSpeedTxt.alignment = TextAnchor.UpperCenter;
            vsSpeedTxt.color = Color.white;
            vsSpeedTxt.fontSize = FontCuerpo;
            var vsSpeedRt = vsSpeedGO.GetComponent<RectTransform>();
            vsSpeedRt.anchorMin = new Vector2(0f, 1f);
            vsSpeedRt.anchorMax = new Vector2(1f, 1f);
            vsSpeedRt.pivot = new Vector2(0.5f, 1f);
            vsSpeedRt.anchoredPosition = new Vector2(0f, -4f);
            vsSpeedRt.sizeDelta = new Vector2(-8f, 22f);

            // Antes solo se sabía en qué asiento estabas leyendo el texto
            // largo de controles (que además cambia todo el rato). Un
            // rótulo fijo y siempre visible en el propio HUD del vehículo.
            var vsSeatGO = new GameObject("SeatText", typeof(Text));
            vsSeatGO.transform.SetParent(vsGO.transform, false);
            var vsSeatTxt = vsSeatGO.GetComponent<Text>();
            vsSeatTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vsSeatTxt.alignment = TextAnchor.UpperCenter;
            vsSeatTxt.color = new Color(0.6f, 0.85f, 1f);
            vsSeatTxt.fontSize = FontMicro;
            vsSeatTxt.fontStyle = FontStyle.Bold;
            var vsSeatRt = vsSeatGO.GetComponent<RectTransform>();
            vsSeatRt.anchorMin = new Vector2(0f, 1f);
            vsSeatRt.anchorMax = new Vector2(1f, 1f);
            vsSeatRt.pivot = new Vector2(0.5f, 1f);
            vsSeatRt.anchoredPosition = new Vector2(0f, -26f);
            vsSeatRt.sizeDelta = new Vector2(-8f, 16f);

            var vsGunnerGO = new GameObject("GunnerText", typeof(Text));
            vsGunnerGO.transform.SetParent(vsGO.transform, false);
            var vsGunnerTxt = vsGunnerGO.GetComponent<Text>();
            vsGunnerTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vsGunnerTxt.alignment = TextAnchor.UpperCenter;
            vsGunnerTxt.color = new Color(0.85f, 0.85f, 0.85f);
            vsGunnerTxt.fontSize = FontMicro;
            var vsGunnerRt = vsGunnerGO.GetComponent<RectTransform>();
            vsGunnerRt.anchorMin = new Vector2(0f, 1f);
            vsGunnerRt.anchorMax = new Vector2(1f, 1f);
            vsGunnerRt.pivot = new Vector2(0.5f, 1f);
            vsGunnerRt.anchoredPosition = new Vector2(0f, -42f);
            vsGunnerRt.sizeDelta = new Vector2(-8f, 16f);

            var vsBarBgGO = new GameObject("HealthBarBG", typeof(Image));
            vsBarBgGO.transform.SetParent(vsGO.transform, false);
            vsBarBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var vsBarBgRt = vsBarBgGO.GetComponent<RectTransform>();
            vsBarBgRt.anchorMin = new Vector2(0f, 0f);
            vsBarBgRt.anchorMax = new Vector2(1f, 0f);
            vsBarBgRt.pivot = new Vector2(0f, 0f);
            vsBarBgRt.anchoredPosition = new Vector2(6f, 6f);
            vsBarBgRt.sizeDelta = new Vector2(-12f, 10f);

            var vsBarFillGO = new GameObject("HealthBarFill", typeof(Image));
            vsBarFillGO.transform.SetParent(vsBarBgGO.transform, false);
            var vsBarFillImg = vsBarFillGO.GetComponent<Image>();
            vsBarFillImg.color = new Color(0.4f, 0.85f, 0.45f);
            vsBarFillImg.type = Image.Type.Filled;
            vsBarFillImg.fillMethod = Image.FillMethod.Horizontal;
            vsBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            vsBarFillImg.fillAmount = 1f;
            StretchFull(vsBarFillGO.GetComponent<RectTransform>());

            var vehicleStatusView = vsGO.GetComponent<VehicleStatusView>();
            vehicleStatusView.Bind(vsSpeedTxt, vsBarFillImg, vsGunnerTxt);
            vsGO.SetActive(false);
            vehicleStatusRef = vehicleStatusView;

            BuildTurretAimUI(canvasGO);
            BuildOffscreenKillMarker(canvasGO);

            // Viñeta de daño: cubre toda la pantalla, arranca invisible
            // (alfa 0) y solo se ve un instante cuando el poseído recibe
            // un golpe.
            var vignetteGO = new GameObject("DamageVignette", typeof(Image), typeof(DamageVignetteView));
            vignetteGO.transform.SetParent(canvasGO.transform, false);
            vignetteGO.transform.SetAsFirstSibling();
            StretchFull(vignetteGO.GetComponent<RectTransform>());
            var vignetteView = vignetteGO.GetComponent<DamageVignetteView>();
            vignetteView.Bind(vignetteGO.GetComponent<Image>(), playerBrain);
            damageVignetteRef = vignetteView;

            // Pulso rojo de vida baja (177). Image PROPIA y no la del
            // vignette: el vignette escribe su color cada frame en el
            // flash de daño, asi que compartir la Image daria un
            // parpadeo sucio en vez de dos señales legibles.
            var pulseGO = new GameObject("LowHealthPulse", typeof(RectTransform), typeof(LowHealthPulseView));
            pulseGO.transform.SetParent(canvasGO.transform, false);
            pulseGO.transform.SetAsFirstSibling();
            StretchFull(pulseGO.GetComponent<RectTransform>());
            var pulseImgGO = new GameObject("Pulse", typeof(Image));
            pulseImgGO.transform.SetParent(pulseGO.transform, false);
            var pulseImg = pulseImgGO.GetComponent<Image>();
            pulseImg.color = new Color(0.85f, 0.1f, 0.1f, 0f);
            pulseImg.raycastTarget = false;
            StretchFull(pulseImgGO.GetComponent<RectTransform>());
            var pulseView = pulseGO.GetComponent<LowHealthPulseView>();
            pulseView.Bind(pulseImg);
            pulseView.Brain = playerBrain;

            // Destello a pantalla completa (181 explosion, 184 cambio de
            // modo). Va como ULTIMO hermano, al reves que el vignette: un
            // fogonazo de explosion tiene que tapar tambien el HUD.
            // Flechas hacia aliados fuera de encuadre (63).
            var allyArrowsGO = new GameObject("OffscreenAllies", typeof(RectTransform), typeof(OffscreenAllyMarkerView));
            allyArrowsGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(allyArrowsGO.GetComponent<RectTransform>());
            var allyArrows = new Image[8];
            for (int i = 0; i < allyArrows.Length; i++)
            {
                var aGO = new GameObject("AllyArrow_" + i, typeof(Image));
                aGO.transform.SetParent(allyArrowsGO.transform, false);
                var img = aGO.GetComponent<Image>();
                img.color = new Color(0.45f, 0.75f, 0.95f, 0.85f);
                img.raycastTarget = false;
                var art = aGO.GetComponent<RectTransform>();
                art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
                art.sizeDelta = new Vector2(16f, 22f);
                aGO.SetActive(false);
                allyArrows[i] = img;
            }
            var allyMarkerView = allyArrowsGO.GetComponent<OffscreenAllyMarkerView>();
            allyMarkerView.Bind(allyArrows);
            offscreenAlliesRef = allyMarkerView;

            // Tarjetas de grupo de control (215). Arriba a la derecha,
            // debajo del minimapa.
            var cardsGO = new GameObject("GroupCards", typeof(RectTransform), typeof(GroupCardsView));
            cardsGO.transform.SetParent(canvasGO.transform, false);
            var cardsRt = cardsGO.GetComponent<RectTransform>();
            cardsRt.anchorMin = cardsRt.anchorMax = new Vector2(1f, 1f);
            cardsRt.pivot = new Vector2(1f, 1f);
            cardsRt.anchoredPosition = new Vector2(-16f, -260f);
            cardsRt.sizeDelta = new Vector2(180f, 200f);
            var cardSlots = new Text[GroupCardsView.SlotCount];
            for (int i = 0; i < GroupCardsView.SlotCount; i++)
            {
                var slotGO = new GameObject("Slot_" + (i + 1), typeof(Text));
                slotGO.transform.SetParent(cardsGO.transform, false);
                var st = slotGO.GetComponent<Text>();
                st.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                st.alignment = TextAnchor.MiddleRight;
                st.fontSize = FontChico;
                st.raycastTarget = false;
                var srt = slotGO.GetComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(1f, 1f);
                srt.anchoredPosition = new Vector2(0f, -i * 20f);
                srt.sizeDelta = new Vector2(180f, 18f);
                cardSlots[i] = st;
            }
            var cardsView = cardsGO.GetComponent<GroupCardsView>();
            cardsView.Bind(cardSlots);
            groupCardsRef = cardsView;

            // Panel de diagnostico (235). Apagado por defecto: medir no
            // debe alterar lo medido. Se prende con [P].
            var perfGO = new GameObject("PerfHud", typeof(RectTransform), typeof(PerfHudView));
            perfGO.transform.SetParent(canvasGO.transform, false);
            var perfRt = perfGO.GetComponent<RectTransform>();
            perfRt.anchorMin = perfRt.anchorMax = new Vector2(0f, 1f);
            perfRt.pivot = new Vector2(0f, 1f);
            perfRt.anchoredPosition = new Vector2(16f, -16f);
            perfRt.sizeDelta = new Vector2(420f, 110f);
            var perfTextGO = new GameObject("Text", typeof(Text));
            perfTextGO.transform.SetParent(perfGO.transform, false);
            var perfTxt = perfTextGO.GetComponent<Text>();
            perfTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            perfTxt.alignment = TextAnchor.UpperLeft;
            perfTxt.color = new Color(0.6f, 1f, 0.7f);
            perfTxt.fontSize = FontChico;
            perfTxt.raycastTarget = false;
            StretchFull(perfTextGO.GetComponent<RectTransform>());
            var perfView = perfGO.GetComponent<PerfHudView>();
            perfView.Bind(perfTxt);
            perfHudRef = perfView;

            var flashGO = new GameObject("ScreenFlash", typeof(RectTransform), typeof(ScreenFlashView));
            flashGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(flashGO.GetComponent<RectTransform>());
            var flashImgGO = new GameObject("Flash", typeof(Image));
            flashImgGO.transform.SetParent(flashGO.transform, false);
            var flashImg = flashImgGO.GetComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;
            StretchFull(flashImgGO.GetComponent<RectTransform>());
            flashGO.GetComponent<ScreenFlashView>().Bind(flashImg);
            flashGO.transform.SetAsLastSibling();

            // Flecha que apunta hacia de donde vino el ultimo golpe. OJO:
            // el mismo bug que ya paso una vez con KillFeedView -- si el
            // Image a ocultar vive en el MISMO GameObject que el
            // componente de vista, Bind() desactivando ese Image apaga
            // TODO el GameObject (y con el, el componente entero, para
            // siempre). El Image va en un hijo aparte.
            var dmgDirGO = new GameObject("DamageDirection", typeof(RectTransform), typeof(DamageDirectionView));
            dmgDirGO.transform.SetParent(canvasGO.transform, false);
            var dmgDirRt = dmgDirGO.GetComponent<RectTransform>();
            dmgDirRt.anchorMin = dmgDirRt.anchorMax = new Vector2(0.5f, 0.5f);
            dmgDirRt.sizeDelta = new Vector2(26f, 26f);
            dmgDirRt.anchoredPosition = new Vector2(0f, 160f); // arriba del centro, gira alrededor del jugador

            var dmgDirImgGO = new GameObject("Arrow", typeof(Image));
            dmgDirImgGO.transform.SetParent(dmgDirGO.transform, false);
            var dmgDirImg = dmgDirImgGO.GetComponent<Image>();
            StretchFull(dmgDirImgGO.GetComponent<RectTransform>());

            var dmgDirView = dmgDirGO.GetComponent<DamageDirectionView>();
            dmgDirView.Bind(dmgDirImg, playerBrain);
            dmgDirView.Initialize();

            // "SOLDADO ABATIDO": cartel grande centrado, un poco arriba del
            // medio para no pisar la mirilla, arranca oculto. OJO: el
            // Text va en un hijo, NO en el mismo GameObject que
            // KillFeedView -- si Bind() apaga el GameObject del propio
            // componente (como pasaba antes), OnEnable/OnDisable lo
            // desuscribe de EventBus y nunca más vuelve a escuchar nada.
            var killFeedGO = new GameObject("KillFeed", typeof(RectTransform), typeof(KillFeedView));
            killFeedGO.transform.SetParent(canvasGO.transform, false);
            StretchFull(killFeedGO.GetComponent<RectTransform>());

            var killFeedTextGO = new GameObject("Text", typeof(Text));
            killFeedTextGO.transform.SetParent(killFeedGO.transform, false);
            var killFeedTxt = killFeedTextGO.GetComponent<Text>();
            killFeedTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            killFeedTxt.alignment = TextAnchor.MiddleCenter;
            killFeedTxt.fontSize = FontTitulo;
            killFeedTxt.fontStyle = FontStyle.Bold;
            killFeedTxt.color = new Color(0.95f, 0.25f, 0.15f);
            var killFeedRt = killFeedTextGO.GetComponent<RectTransform>();
            killFeedRt.anchorMin = killFeedRt.anchorMax = new Vector2(0.5f, 0.5f);
            killFeedRt.anchoredPosition = new Vector2(0f, 160f);
            killFeedRt.sizeDelta = new Vector2(700f, 60f);

            var killFeedView = killFeedGO.GetComponent<KillFeedView>();
            killFeedView.Bind(killFeedTxt);
            killFeedRef = killFeedView;

            // Cartel "X está muerto", centrado, que se desvanece solo.
            var deadGO = new GameObject("DeadNotice", typeof(RectTransform), typeof(CanvasGroup), typeof(DeadNoticeView));
            deadGO.transform.SetParent(canvasGO.transform, false);
            var deadRt = deadGO.GetComponent<RectTransform>();
            deadRt.anchorMin = deadRt.anchorMax = new Vector2(0.5f, 0.5f);
            deadRt.anchoredPosition = new Vector2(0f, 80f);
            deadRt.sizeDelta = new Vector2(420f, 60f);

            var deadBgGO = new GameObject("BG", typeof(Image));
            deadBgGO.transform.SetParent(deadGO.transform, false);
            deadBgGO.GetComponent<Image>().color = new Color(0.55f, 0.1f, 0.1f, 0.85f);
            StretchFull(deadBgGO.GetComponent<RectTransform>());

            var deadTextGO = new GameObject("Text", typeof(Text));
            deadTextGO.transform.SetParent(deadGO.transform, false);
            var deadText = deadTextGO.GetComponent<Text>();
            deadText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            deadText.alignment = TextAnchor.MiddleCenter;
            deadText.color = Color.white;
            deadText.fontSize = FontSubtitulo;
            StretchFull(deadTextGO.GetComponent<RectTransform>());

            var deadNotice = deadGO.GetComponent<DeadNoticeView>();
            deadNotice.Bind(deadText, deadGO.GetComponent<CanvasGroup>());
            deadNoticeRef = deadNotice;

            // Aviso de modo (VISTA RTS / VISTA FPS): arriba y al centro,
            // lejos del cartel de "esta muerto" para que nunca compitan
            // por el mismo lugar en pantalla.
            var toastGO = new GameObject("ModeToast", typeof(RectTransform), typeof(CanvasGroup), typeof(ModeToastView));
            toastGO.transform.SetParent(canvasGO.transform, false);
            var toastRt = toastGO.GetComponent<RectTransform>();
            toastRt.anchorMin = toastRt.anchorMax = new Vector2(0.5f, 1f);
            toastRt.pivot = new Vector2(0.5f, 1f);
            toastRt.anchoredPosition = new Vector2(0f, -90f);
            toastRt.sizeDelta = new Vector2(260f, 40f);

            var toastBgGO = new GameObject("BG", typeof(Image));
            toastBgGO.transform.SetParent(toastGO.transform, false);
            toastBgGO.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.15f, 0.85f);
            StretchFull(toastBgGO.GetComponent<RectTransform>());

            var toastTextGO = new GameObject("Text", typeof(Text));
            toastTextGO.transform.SetParent(toastGO.transform, false);
            var toastText = toastTextGO.GetComponent<Text>();
            toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.color = Color.white;
            toastText.fontSize = FontCuerpo;
            toastText.fontStyle = FontStyle.Bold;
            StretchFull(toastTextGO.GetComponent<RectTransform>());

            var modeToast = toastGO.GetComponent<ModeToastView>();
            modeToast.Bind(toastText, toastGO.GetComponent<CanvasGroup>());
            modeToastRef = modeToast;

            // Menu de ordenes ([Q] sostenido). Se construye con la misma
            // fabrica que usa GameplaySceneBootstrap para la escena real,
            // asi la suite prueba EXACTAMENTE el panel que ve el jugador.
            ordenesMenuRef = SP.UI.MenuDeOrdenes.Construir(canvasGO.transform);

            var rosterGO = new GameObject("Roster", typeof(RectTransform), typeof(SelectedSoldierUI));
            rosterGO.transform.SetParent(canvasGO.transform, false);
            var rosterRt = rosterGO.GetComponent<RectTransform>();
            rosterRt.anchorMin = new Vector2(0f, 1f);
            rosterRt.anchorMax = new Vector2(0f, 1f);
            rosterRt.pivot = new Vector2(0f, 1f);
            // Margen unificado a 16px con el resto del HUD (antes 20).
            rosterRt.anchoredPosition = new Vector2(16f, -16f);
            // Las filas pasaron de 1 renglón (solo nombre) a 2 (nombre +
            // vida/arma) y suman una barra de vida abajo: hay que darles
            // el alto real o el segundo renglón queda recortado.
            const float rowHeight = 46f;
            const float rowStride = 50f;
            rosterRt.sizeDelta = new Vector2(240f, squad.Count * rowStride);
            var roster = rosterGO.GetComponent<SelectedSoldierUI>();

            for (int i = 0; i < squad.Count; i++)
            {
                var rowGO = new GameObject($"Row_{squad[i].DisplayName}", typeof(Image));
                rowGO.transform.SetParent(rosterGO.transform, false);
                var rowRt = rowGO.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -i * rowStride);
                rowRt.sizeDelta = new Vector2(230f, rowHeight);
                var rowImg = rowGO.GetComponent<Image>();

                var labelGO = new GameObject("Label", typeof(Text));
                labelGO.transform.SetParent(rowGO.transform, false);
                var labelTxt = labelGO.GetComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.color = Color.white;
                labelTxt.fontSize = FontChico;
                labelTxt.alignment = TextAnchor.UpperLeft;
                var labelRt = labelGO.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(8f, 6f);
                labelRt.offsetMax = new Vector2(-6f, -3f);

                // Barra de vida al pie de la fila: el número solo obliga a
                // hacer la división mental, la barra se lee de un vistazo.
                var rowBarBgGO = new GameObject("BarBG", typeof(Image));
                rowBarBgGO.transform.SetParent(rowGO.transform, false);
                rowBarBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
                var rowBarBgRt = rowBarBgGO.GetComponent<RectTransform>();
                rowBarBgRt.anchorMin = new Vector2(0f, 0f);
                rowBarBgRt.anchorMax = new Vector2(1f, 0f);
                rowBarBgRt.pivot = new Vector2(0f, 0f);
                rowBarBgRt.anchoredPosition = new Vector2(6f, 3f);
                rowBarBgRt.sizeDelta = new Vector2(-12f, 4f);

                var rowBarFillGO = new GameObject("BarFill", typeof(Image));
                rowBarFillGO.transform.SetParent(rowBarBgGO.transform, false);
                var rowBarFillImg = rowBarFillGO.GetComponent<Image>();
                rowBarFillImg.color = new Color(0.35f, 0.85f, 0.4f);
                rowBarFillImg.type = Image.Type.Filled;
                rowBarFillImg.fillMethod = Image.FillMethod.Horizontal;
                rowBarFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                rowBarFillImg.fillAmount = 1f;
                StretchFull(rowBarFillGO.GetComponent<RectTransform>());

                roster.AddRow(squad[i], rowImg, labelTxt, rowBarFillImg);
            }

            roster.Initialize();
            rosterUiRef = roster;

            BuildInstructionBanner(canvasGO.transform);
            BuildPhaseBanner(canvasGO.transform);
            BuildNearbySquadList(canvasGO.transform, squad);
            BuildSelectionBox(canvasGO.transform);
            BuildMinimap(canvasGO.transform, cam);
            BuildPauseUI(canvasGO.transform);
            BuildOutcomeUI(canvasGO.transform);
        }

        // HUD del artillero: reticulo de dos estados, marca de la brecha
        // de giro pendiente, barra de cooldown y anillo en el suelo con
        // el radio de explosion real del arma.
        // Flecha breve en el borde apuntando a una baja que quedo fuera
        // de encuadre: por el feed de texto esas bajas se perdian entre el
        // resto de la informacion.
        static void BuildOffscreenKillMarker(GameObject canvasGO)
        {
            var root = new GameObject("OffscreenKillMarker", typeof(RectTransform), typeof(OffscreenKillMarkerView));
            root.transform.SetParent(canvasGO.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            var arrowGO = new GameObject("Arrow", typeof(Image));
            arrowGO.transform.SetParent(root.transform, false);
            arrowGO.GetComponent<Image>().color = new Color(0.95f, 0.3f, 0.2f, 0.9f);
            var rt = arrowGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(14f, 26f);
            arrowGO.SetActive(false);

            var view = root.GetComponent<OffscreenKillMarkerView>();
            view.Bind(arrowGO.GetComponent<Image>());
            offscreenKillRef = view;
        }

        static void BuildTurretAimUI(GameObject canvasGO)
        {
            var root = new GameObject("TurretAim", typeof(RectTransform), typeof(TurretAimView));
            root.transform.SetParent(canvasGO.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            var reticleGO = new GameObject("Reticle", typeof(Image));
            reticleGO.transform.SetParent(root.transform, false);
            var reticleImg = reticleGO.GetComponent<Image>();
            reticleImg.color = new Color(0.35f, 1f, 0.45f);
            var reticleRt = reticleGO.GetComponent<RectTransform>();
            reticleRt.anchorMin = reticleRt.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRt.pivot = new Vector2(0.5f, 0.5f);
            reticleRt.anchoredPosition = Vector2.zero;
            reticleRt.sizeDelta = new Vector2(14f, 14f);

            var gapGO = new GameObject("GapMarker", typeof(Image));
            gapGO.transform.SetParent(root.transform, false);
            gapGO.GetComponent<Image>().color = new Color(1f, 0.75f, 0.25f, 0.85f);
            var gapRt = gapGO.GetComponent<RectTransform>();
            gapRt.anchorMin = gapRt.anchorMax = new Vector2(0.5f, 0.5f);
            gapRt.pivot = new Vector2(0.5f, 0.5f);
            gapRt.anchoredPosition = Vector2.zero;
            gapRt.sizeDelta = new Vector2(6f, 22f);
            gapGO.SetActive(false);

            var cdBgGO = new GameObject("CooldownBG", typeof(Image));
            cdBgGO.transform.SetParent(root.transform, false);
            cdBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var cdBgRt = cdBgGO.GetComponent<RectTransform>();
            cdBgRt.anchorMin = cdBgRt.anchorMax = new Vector2(0.5f, 0.5f);
            cdBgRt.pivot = new Vector2(0.5f, 1f);
            cdBgRt.anchoredPosition = new Vector2(0f, -28f);
            cdBgRt.sizeDelta = new Vector2(90f, 7f);

            var cdFillGO = new GameObject("CooldownFill", typeof(Image));
            cdFillGO.transform.SetParent(cdBgGO.transform, false);
            var cdFillImg = cdFillGO.GetComponent<Image>();
            cdFillImg.color = new Color(0.35f, 1f, 0.45f);
            cdFillImg.type = Image.Type.Filled;
            cdFillImg.fillMethod = Image.FillMethod.Horizontal;
            cdFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            cdFillImg.fillAmount = 1f;
            StretchFull(cdFillGO.GetComponent<RectTransform>());

            // El anillo vive en el mundo, no en el Canvas: marca un area
            // del terreno, no una posicion de pantalla.
            var ringGO = new GameObject("TurretRadiusRing", typeof(LineRenderer));
            var ring = ringGO.GetComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.widthMultiplier = 0.12f;
            ring.positionCount = 0;
            var ringMat = CreateFlatMaterial(new Color(0.95f, 0.55f, 0.1f)); transientRuntimeAssets.Add(ringMat); ring.sharedMaterial = ringMat;
            ring.enabled = false;

            var view = root.GetComponent<TurretAimView>();
            view.Bind(reticleImg, gapGO.GetComponent<Image>(), cdFillImg, ring);
            root.SetActive(false);
            turretAimRef = view;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Los textos que se ven directo sobre el mundo 3D (sin un panel
        // opaco atras) pierden contraste segun el cielo o el terreno de
        // fondo -- un contorno oscuro fino los mantiene legibles sin
        // importar que haya detras. Los textos que ya estan sobre un
        // panel de fondo solido no lo necesitan.
        static void AddOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        static void BuildInstructionBanner(Transform canvasParent)
        {
            var go = new GameObject("InstructionBanner", typeof(RectTransform), typeof(InstructionBannerView));
            go.transform.SetParent(canvasParent, false);
            StretchFull(go.GetComponent<RectTransform>());

            var textGO = new GameObject("Text", typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.1f, 0.12f);
            text.fontSize = FontSubtitulo;
            var rt = textGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 26f);
            rt.sizeDelta = new Vector2(900f, 30f);

            var bgGO = new GameObject("BG", typeof(Image));
            bgGO.transform.SetParent(go.transform, false);
            bgGO.transform.SetAsFirstSibling();
            bgGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.8f);
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0f);
            bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.pivot = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition = new Vector2(0f, 20f);
            bgRt.sizeDelta = new Vector2(920f, 40f);

            var view = go.GetComponent<InstructionBannerView>();
            view.Bind(text);
            view.SetText("Bienvenido. Movete con WASD.");
            instructionUiRef = view;
        }

        static void BuildPhaseBanner(Transform canvasParent)
        {
            var go = new GameObject("PhaseBanner", typeof(RectTransform), typeof(PhaseBannerView));
            go.transform.SetParent(canvasParent, false);
            StretchFull(go.GetComponent<RectTransform>());

            var textGO = new GameObject("Text", typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.35f, 0.15f);
            text.fontSize = FontTitulo;
            text.fontStyle = FontStyle.Bold;
            var rt = textGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 80f);
            rt.sizeDelta = new Vector2(900f, 120f);

            var view = go.GetComponent<PhaseBannerView>();
            view.Bind(text);
            phaseBannerRef = view;
        }

        static void BuildNearbySquadList(Transform canvasParent, List<Soldier> squad)
        {
            var panelGO = new GameObject("NearbySquadPanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(canvasParent, false);
            panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            var panelRt = panelGO.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            // Margen unificado a 16px con el resto del HUD (antes 20).
            panelRt.anchoredPosition = new Vector2(16f, 16f);
            panelRt.sizeDelta = new Vector2(260f, 120f);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(panelGO.transform, false);
            viewportGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;
            var viewportRt = viewportGO.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(4f, 4f);
            viewportRt.offsetMax = new Vector2(-4f, -4f);

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 1f);
            contentRt.sizeDelta = new Vector2(252f, squad.Count * 48f);
            contentRt.anchoredPosition = Vector2.zero;

            var scrollRect = panelGO.AddComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var listView = panelGO.AddComponent<NearbySquadListView>();

            for (int i = 0; i < squad.Count; i++)
            {
                var rowGO = new GameObject($"NearbyRow_{squad[i].DisplayName}", typeof(Image));
                rowGO.transform.SetParent(contentGO.transform, false);
                rowGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
                var rowRt = rowGO.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -i * 48f);
                rowRt.sizeDelta = new Vector2(0f, 44f);

                var labelGO = new GameObject("Label", typeof(Text));
                labelGO.transform.SetParent(rowGO.transform, false);
                var labelTxt = labelGO.GetComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.color = Color.white;
                labelTxt.fontSize = FontChico;
                labelTxt.alignment = TextAnchor.UpperLeft;
                var labelRt = labelGO.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(6f, 6f);
                labelRt.offsetMax = new Vector2(0f, 0f);

                // Barra de vida real (no solo el número), fina, abajo del todo.
                var healthBgGO = new GameObject("HealthBG", typeof(Image));
                healthBgGO.transform.SetParent(rowGO.transform, false);
                healthBgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
                var healthBgRt = healthBgGO.GetComponent<RectTransform>();
                healthBgRt.anchorMin = new Vector2(0f, 0f);
                healthBgRt.anchorMax = new Vector2(1f, 0f);
                healthBgRt.pivot = new Vector2(0f, 0f);
                healthBgRt.anchoredPosition = new Vector2(6f, 3f);
                healthBgRt.sizeDelta = new Vector2(-12f, 5f);

                var healthFillGO = new GameObject("HealthFill", typeof(Image));
                healthFillGO.transform.SetParent(healthBgGO.transform, false);
                var healthFillImg = healthFillGO.GetComponent<Image>();
                healthFillImg.color = new Color(0.35f, 0.85f, 0.4f);
                healthFillImg.type = Image.Type.Filled;
                healthFillImg.fillMethod = Image.FillMethod.Horizontal;
                healthFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthFillImg.fillAmount = 1f;
                StretchFull(healthFillGO.GetComponent<RectTransform>());

                listView.AddEntry(squad[i], rowGO, labelTxt, healthFillImg);
            }

            squadListRef = listView;
        }

        static Button BuildUIButton(Transform parent, string name, string label, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(260f, 56f);

            var textGO = new GameObject("Text", typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var txt = textGO.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = FontSubtitulo;
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;
            StretchFull(textGO.GetComponent<RectTransform>());

            return go.GetComponent<Button>();
        }

        static Slider BuildLabeledSlider(Transform parent, string label, Vector2 anchoredPos, float min, float max, float value)
        {
            var labelGO = new GameObject(label + "_Label", typeof(Text));
            labelGO.transform.SetParent(parent, false);
            var labelTxt = labelGO.GetComponent<Text>();
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = Color.white;
            labelTxt.fontSize = FontCuerpo;
            labelTxt.text = label;
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = anchoredPos + new Vector2(0f, 24f);
            labelRt.sizeDelta = new Vector2(400f, 24f);

            // Valor numérico a la derecha del título: antes la barra no
            // decía nada de cuánto era el valor real, solo un relleno
            // sin referencia -- imposible saber "qué tan sensible" o
            // "qué tan alto" quedó de verdad.
            var valueGO = new GameObject(label + "_Value", typeof(Text));
            valueGO.transform.SetParent(parent, false);
            var valueTxt = valueGO.GetComponent<Text>();
            valueTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueTxt.alignment = TextAnchor.MiddleRight;
            valueTxt.color = new Color(0.8f, 0.85f, 0.9f);
            valueTxt.fontSize = FontChico;
            valueTxt.text = value.ToString("0.00");
            var valueRt = valueGO.GetComponent<RectTransform>();
            valueRt.anchorMin = valueRt.anchorMax = new Vector2(0.5f, 0.5f);
            valueRt.anchoredPosition = anchoredPos + new Vector2(0f, 24f);
            valueRt.sizeDelta = new Vector2(400f, 24f);

            var sliderGO = new GameObject(label + "_Slider", typeof(Slider));
            sliderGO.transform.SetParent(parent, false);
            var sliderRt = sliderGO.GetComponent<RectTransform>();
            sliderRt.anchorMin = sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRt.anchoredPosition = anchoredPos;
            sliderRt.sizeDelta = new Vector2(400f, 20f);

            var bgGO = new GameObject("Background", typeof(Image));
            bgGO.transform.SetParent(sliderGO.transform, false);
            bgGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f);
            StretchFull(bgGO.GetComponent<RectTransform>());

            var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            StretchFull(fillAreaGO.GetComponent<RectTransform>());

            var fillGO = new GameObject("Fill", typeof(Image));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            fillGO.GetComponent<Image>().color = new Color(0.35f, 0.65f, 0.9f);
            StretchFull(fillGO.GetComponent<RectTransform>());

            var handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            StretchFull(handleAreaGO.GetComponent<RectTransform>());

            var handleGO = new GameObject("Handle", typeof(Image));
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            handleGO.GetComponent<Image>().color = Color.white;
            var handleRt = handleGO.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(16f, 24f);

            var slider = sliderGO.GetComponent<Slider>();
            slider.fillRect = fillGO.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.targetGraphic = handleGO.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return slider;
        }

        // Pausa ([ESC]) con sub-panel de configuraciones (sensibilidad de
        // mouse y volumen). El propio PauseController escucha [ESC], acá
        // solo se arma la UI y se la Bind()ea.
        static void BuildPauseUI(Transform canvasParent)
        {
            var pauseGO = new GameObject("PauseController", typeof(RectTransform), typeof(PauseController));
            pauseGO.transform.SetParent(canvasParent, false);
            StretchFull(pauseGO.GetComponent<RectTransform>());
            var pauseController = pauseGO.GetComponent<PauseController>();

            var pausePanelGO = new GameObject("PausePanel", typeof(Image));
            pausePanelGO.transform.SetParent(pauseGO.transform, false);
            pausePanelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            StretchFull(pausePanelGO.GetComponent<RectTransform>());

            var pauseTitleGO = new GameObject("Title", typeof(Text));
            pauseTitleGO.transform.SetParent(pausePanelGO.transform, false);
            var pauseTitleTxt = pauseTitleGO.GetComponent<Text>();
            pauseTitleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pauseTitleTxt.alignment = TextAnchor.MiddleCenter;
            pauseTitleTxt.color = Color.white;
            pauseTitleTxt.fontSize = FontTitulo;
            pauseTitleTxt.fontStyle = FontStyle.Bold;
            pauseTitleTxt.text = "PAUSA";
            var pauseTitleRt = pauseTitleGO.GetComponent<RectTransform>();
            pauseTitleRt.anchorMin = pauseTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
            pauseTitleRt.anchoredPosition = new Vector2(0f, 140f);
            pauseTitleRt.sizeDelta = new Vector2(500f, 80f);

            var continueBtn = BuildUIButton(pausePanelGO.transform, "ContinueButton", "CONTINUAR", new Vector2(0f, 90f), new Color(0.25f, 0.6f, 0.35f));
            continueBtn.onClick.AddListener(pauseController.OnContinueClicked);

            var settingsBtn = BuildUIButton(pausePanelGO.transform, "SettingsButton", "CONFIGURACIONES", new Vector2(0f, 30f), new Color(0.3f, 0.45f, 0.7f));
            settingsBtn.onClick.AddListener(pauseController.OnSettingsClicked);

            var controlsBtn = BuildUIButton(pausePanelGO.transform, "ControlsButton", "CONTROLES", new Vector2(0f, -30f), new Color(0.4f, 0.4f, 0.45f));
            controlsBtn.onClick.AddListener(pauseController.OnControlsClicked);

            var menuBtn = BuildUIButton(pausePanelGO.transform, "MenuButton", "VOLVER AL MENU", new Vector2(0f, -90f), new Color(0.55f, 0.3f, 0.25f));
            menuBtn.onClick.AddListener(pauseController.OnMenuClicked);

            var settingsPanelGO = new GameObject("SettingsPanel", typeof(Image));
            settingsPanelGO.transform.SetParent(pauseGO.transform, false);
            // Alfa 1 (no 0.95): con transparencia se veía "PAUSA" tenue
            // atravesando el panel de Configuraciones, un pisado de texto
            // que quedaba confuso, no un efecto buscado.
            settingsPanelGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var settingsRt = settingsPanelGO.GetComponent<RectTransform>();
            settingsRt.anchorMin = settingsRt.anchorMax = new Vector2(0.5f, 0.5f);
            // 620 y no 560: entra un segundo toggle (efectos de camara) sin
            // que el boton VOLVER se superponga con el.
            settingsRt.sizeDelta = new Vector2(480f, 620f);

            var settingsTitleGO = new GameObject("Title", typeof(Text));
            settingsTitleGO.transform.SetParent(settingsPanelGO.transform, false);
            var settingsTitleTxt = settingsTitleGO.GetComponent<Text>();
            settingsTitleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settingsTitleTxt.alignment = TextAnchor.MiddleCenter;
            settingsTitleTxt.color = Color.white;
            settingsTitleTxt.fontSize = FontEncabezado;
            settingsTitleTxt.fontStyle = FontStyle.Bold;
            settingsTitleTxt.text = "CONFIGURACIONES";
            var settingsTitleRt = settingsTitleGO.GetComponent<RectTransform>();
            settingsTitleRt.anchorMin = settingsTitleRt.anchorMax = new Vector2(0.5f, 1f);
            settingsTitleRt.pivot = new Vector2(0.5f, 1f);
            settingsTitleRt.anchoredPosition = new Vector2(0f, -20f);
            settingsTitleRt.sizeDelta = new Vector2(440f, 40f);

            BuildLabeledSlider(settingsPanelGO.transform, "Sensibilidad de mouse", new Vector2(0f, 130f), 0.05f, 0.5f, 0.15f);
            // Sensibilidad de torreta por separado: mirar a pie y girar
            // un cañon son gestos de escala muy distinta, ajustar uno no
            // deberia arruinar el otro.
            BuildLabeledSlider(settingsPanelGO.transform, "Sensibilidad de torreta", new Vector2(0f, 70f), 0.05f, 0.5f, 0.15f);
            var volumeSlider = BuildLabeledSlider(settingsPanelGO.transform, "Volumen", new Vector2(0f, 10f), 0f, 1f, 1f);
            volumeSlider.onValueChanged.AddListener(v => AudioListener.volume = v);
            BuildLabeledSlider(settingsPanelGO.transform, "Tamaño de HUD", new Vector2(0f, -50f), 0.7f, 1.4f, 1f);
            BuildLabeledSlider(settingsPanelGO.transform, "Tamaño de mirilla", new Vector2(0f, -110f), 0.5f, 2f, 1f);

            // Invertir eje Y: toggle simple, no un slider -- es una opcion
            // binaria (invertido o no), no un rango.
            var invertGO = new GameObject("InvertirEjeY_Toggle", typeof(Toggle));
            invertGO.transform.SetParent(settingsPanelGO.transform, false);
            var invertRt = invertGO.GetComponent<RectTransform>();
            invertRt.anchorMin = invertRt.anchorMax = new Vector2(0.5f, 0.5f);
            invertRt.anchoredPosition = new Vector2(-140f, -160f);
            invertRt.sizeDelta = new Vector2(24f, 24f);
            var invertBgGO = new GameObject("Background", typeof(Image));
            invertBgGO.transform.SetParent(invertGO.transform, false);
            invertBgGO.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f);
            StretchFull(invertBgGO.GetComponent<RectTransform>());
            var invertCheckGO = new GameObject("Checkmark", typeof(Image));
            invertCheckGO.transform.SetParent(invertBgGO.transform, false);
            invertCheckGO.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.45f);
            StretchFull(invertCheckGO.GetComponent<RectTransform>());
            var invertToggle = invertGO.GetComponent<Toggle>();
            invertToggle.targetGraphic = invertBgGO.GetComponent<Image>();
            invertToggle.graphic = invertCheckGO.GetComponent<Image>();
            invertToggle.isOn = false;

            var invertLabelGO = new GameObject("InvertirEjeY_Label", typeof(Text));
            invertLabelGO.transform.SetParent(settingsPanelGO.transform, false);
            var invertLabelTxt = invertLabelGO.GetComponent<Text>();
            invertLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            invertLabelTxt.alignment = TextAnchor.MiddleLeft;
            invertLabelTxt.color = Color.white;
            invertLabelTxt.fontSize = FontCuerpo;
            invertLabelTxt.text = "Invertir eje Y";
            var invertLabelRt = invertLabelGO.GetComponent<RectTransform>();
            invertLabelRt.anchorMin = invertLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
            invertLabelRt.anchoredPosition = new Vector2(20f, -160f);
            invertLabelRt.sizeDelta = new Vector2(260f, 24f);

            BuildLabeledToggle(settingsPanelGO.transform, "EfectosDeCamara", "Efectos de camara", -195f, true);

            var backBtn = BuildUIButton(settingsPanelGO.transform, "BackButton", "VOLVER", new Vector2(0f, -255f), new Color(0.5f, 0.5f, 0.5f));
            backBtn.onClick.AddListener(pauseController.OnSettingsBackClicked);

            // Panel de controles: antes no habia ningun lugar donde ver la
            // lista de atajos salvo el texto contextual, que solo muestra
            // unos pocos segun el estado actual. La pausa es el momento
            // natural para consultarlos todos juntos.
            var controlsPanelGO = new GameObject("ControlsPanel", typeof(Image));
            controlsPanelGO.transform.SetParent(pauseGO.transform, false);
            controlsPanelGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var controlsRt = controlsPanelGO.GetComponent<RectTransform>();
            controlsRt.anchorMin = controlsRt.anchorMax = new Vector2(0.5f, 0.5f);
            controlsRt.sizeDelta = new Vector2(560f, 420f);

            var controlsTitleGO = new GameObject("Title", typeof(Text));
            controlsTitleGO.transform.SetParent(controlsPanelGO.transform, false);
            var controlsTitleTxt = controlsTitleGO.GetComponent<Text>();
            controlsTitleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            controlsTitleTxt.alignment = TextAnchor.MiddleCenter;
            controlsTitleTxt.color = Color.white;
            controlsTitleTxt.fontSize = FontEncabezado;
            controlsTitleTxt.fontStyle = FontStyle.Bold;
            controlsTitleTxt.text = "CONTROLES";
            var controlsTitleRt = controlsTitleGO.GetComponent<RectTransform>();
            controlsTitleRt.anchorMin = controlsTitleRt.anchorMax = new Vector2(0.5f, 1f);
            controlsTitleRt.pivot = new Vector2(0.5f, 1f);
            controlsTitleRt.anchoredPosition = new Vector2(0f, -18f);
            controlsTitleRt.sizeDelta = new Vector2(500f, 36f);

            var controlsListGO = new GameObject("List", typeof(Text));
            controlsListGO.transform.SetParent(controlsPanelGO.transform, false);
            var controlsListTxt = controlsListGO.GetComponent<Text>();
            controlsListTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            controlsListTxt.alignment = TextAnchor.UpperLeft;
            controlsListTxt.color = new Color(0.9f, 0.9f, 0.92f);
            controlsListTxt.fontSize = FontChico;
            // Fuente UNICA: antes esto era un literal a mano que ya habia
            // divergido del cartel contextual y del codigo. Le faltaban ~20
            // atajos reales (Q, C, F1/F2/F3, H, Espacio, Ctrl+1..9, el zoom
            // con clic derecho, la R de municion del artillero, y todo el
            // contexto de vehiculo en vista tactica). Ahora las dos vistas
            // derivan de ControlsTable, asi que no pueden desincronizarse.
            controlsListTxt.text = SP.UI.ControlsTable.FullText();
            controlsListTxt.fontSize = FontMicro;
            var controlsListRt = controlsListGO.GetComponent<RectTransform>();
            controlsListRt.anchorMin = new Vector2(0f, 0f);
            controlsListRt.anchorMax = new Vector2(1f, 1f);
            controlsListRt.offsetMin = new Vector2(24f, 60f);
            controlsListRt.offsetMax = new Vector2(-24f, -60f);

            var rebindOpenBtn = BuildUIButton(controlsPanelGO.transform, "RebindButton", "REMAPEAR", new Vector2(-130f, -180f), new Color(0.35f, 0.45f, 0.6f));
            rebindOpenBtn.onClick.AddListener(pauseController.OnRebindClicked);

            BuildRebindPanel(pauseGO.transform, pauseController);

            var controlsBackBtn = BuildUIButton(controlsPanelGO.transform, "BackButton", "VOLVER", new Vector2(130f, -180f), new Color(0.5f, 0.5f, 0.5f));
            controlsBackBtn.onClick.AddListener(pauseController.OnControlsBackClicked);

            // Confirmacion antes de salir al menu: abandonar la partida es
            // irreversible, y un click accidental en "Volver al menu" no
            // deberia mandar directo a la escena de menu sin dar chance de
            // arrepentirse.
            var confirmExitGO = new GameObject("ConfirmExitPanel", typeof(Image));
            confirmExitGO.transform.SetParent(pauseGO.transform, false);
            confirmExitGO.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.05f, 0.97f);
            var confirmExitRt = confirmExitGO.GetComponent<RectTransform>();
            confirmExitRt.anchorMin = confirmExitRt.anchorMax = new Vector2(0.5f, 0.5f);
            confirmExitRt.sizeDelta = new Vector2(420f, 200f);

            var confirmExitTextGO = new GameObject("Text", typeof(Text));
            confirmExitTextGO.transform.SetParent(confirmExitGO.transform, false);
            var confirmExitTxt = confirmExitTextGO.GetComponent<Text>();
            confirmExitTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            confirmExitTxt.alignment = TextAnchor.MiddleCenter;
            confirmExitTxt.color = Color.white;
            confirmExitTxt.fontSize = FontCuerpo;
            confirmExitTxt.text = "¿Volver al menu?\nSe perdera el progreso de esta partida.";
            var confirmExitTextRt = confirmExitTextGO.GetComponent<RectTransform>();
            confirmExitTextRt.anchorMin = confirmExitTextRt.anchorMax = new Vector2(0.5f, 1f);
            confirmExitTextRt.pivot = new Vector2(0.5f, 1f);
            confirmExitTextRt.anchoredPosition = new Vector2(0f, -20f);
            confirmExitTextRt.sizeDelta = new Vector2(380f, 90f);

            var noBtn = BuildUIButton(confirmExitGO.transform, "NoButton", "CANCELAR", new Vector2(-90f, -70f), new Color(0.4f, 0.4f, 0.45f));
            noBtn.onClick.AddListener(pauseController.OnConfirmExitNo);
            var yesBtn = BuildUIButton(confirmExitGO.transform, "YesButton", "SALIR", new Vector2(90f, -70f), new Color(0.6f, 0.25f, 0.2f));
            yesBtn.onClick.AddListener(pauseController.OnConfirmExitYes);
            // Las posiciones de arriba son historicas y se solapaban. El
            // reparto real lo hace el diagramador, con las mismas cuentas
            // que corrigen la escena ya guardada: una sola definicion.
            SP.UI.Diagramador.AcomodarConfirmarSalida(confirmExitGO);

            controlsPanelGO.SetActive(false);
            confirmExitGO.SetActive(false);

            pauseController.Bind(pausePanelGO, settingsPanelGO);
            pauseController.HudScaler = hudScalerRef;
            pauseController.AimUiRef = aimUiRef;
            pauseControllerRef = pauseController;
        }

        // Victoria y derrota: mismo esqueleto (título grande + Reintentar
        // + Salir), distinto color/texto por panel.
        static void BuildOutcomeUI(Transform canvasParent)
        {
            var outcomeGO = new GameObject("GameOutcome", typeof(RectTransform), typeof(GameOutcomeController));
            outcomeGO.transform.SetParent(canvasParent, false);
            StretchFull(outcomeGO.GetComponent<RectTransform>());
            var outcomeController = outcomeGO.GetComponent<GameOutcomeController>();

            var victoryGO = BuildOutcomePanel(outcomeGO.transform, "VictoryPanel", "GANASTE", new Color(0.1f, 0.5f, 0.15f, 0.92f), out var victoryRetry, out var victoryExit);
            var defeatGO = BuildOutcomePanel(outcomeGO.transform, "DefeatPanel", "PERDISTE", new Color(0.55f, 0.1f, 0.1f, 0.92f), out var defeatRetry, out var defeatExit);

            victoryRetry.onClick.AddListener(outcomeController.OnRetryClicked);
            victoryExit.onClick.AddListener(outcomeController.OnExitClicked);
            defeatRetry.onClick.AddListener(outcomeController.OnRetryClicked);
            defeatExit.onClick.AddListener(outcomeController.OnExitClicked);

            outcomeController.Bind(victoryGO, defeatGO);
            outcomeControllerRef = outcomeController;
        }

        static GameObject BuildOutcomePanel(Transform parent, string name, string title, Color bg, out Button retryBtn, out Button exitBtn)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            StretchFull(go.GetComponent<RectTransform>());

            var titleGO = new GameObject("Title", typeof(Text));
            titleGO.transform.SetParent(go.transform, false);
            var titleTxt = titleGO.GetComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;
            titleTxt.fontSize = FontDisplay;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.text = title;
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.6f);
            titleRt.sizeDelta = new Vector2(900f, 120f);

            // Estadisticas de la partida: sin esto la pantalla de fin solo
            // decia si ganaste o perdiste, sin dato alguno de como fue.
            var statsGO = new GameObject("Stats", typeof(Text));
            statsGO.transform.SetParent(go.transform, false);
            var statsTxt = statsGO.GetComponent<Text>();
            statsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statsTxt.alignment = TextAnchor.MiddleCenter;
            statsTxt.color = new Color(0.9f, 0.9f, 0.9f);
            statsTxt.fontSize = FontSubtitulo;
            var statsRt = statsGO.GetComponent<RectTransform>();
            statsRt.anchorMin = statsRt.anchorMax = new Vector2(0.5f, 0.6f);
            statsRt.anchoredPosition = new Vector2(0f, -70f);
            statsRt.sizeDelta = new Vector2(700f, 40f);
            if (name == "VictoryPanel") victoryStatsRef = statsTxt; else defeatStatsRef = statsTxt;

            retryBtn = BuildUIButton(go.transform, "RetryButton", "REINTENTAR", new Vector2(0f, 0f), new Color(0.25f, 0.45f, 0.75f));
            exitBtn = BuildUIButton(go.transform, "ExitButton", "SALIR", new Vector2(0f, -70f), new Color(0.5f, 0.5f, 0.5f));

            // Navegacion explicita entre los dos botones: sin esto no se
            // puede pasar de uno a otro con el teclado ni con un mando.
            var retryNav = retryBtn.navigation;
            retryNav.mode = Navigation.Mode.Explicit;
            retryNav.selectOnDown = exitBtn;
            retryBtn.navigation = retryNav;
            var exitNav = exitBtn.navigation;
            exitNav.mode = Navigation.Mode.Explicit;
            exitNav.selectOnUp = retryBtn;
            exitBtn.navigation = exitNav;

            return go;
        }

        static void BuildSelectionBox(Transform canvasParent)
        {
            var go = new GameObject("SelectionBox", typeof(Image));
            go.transform.SetParent(canvasParent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.4f, 0.85f, 0.5f, 0.22f);
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = Vector2.zero;

            go.SetActive(false);
            selectionBoxRef = img;
        }

        static void BuildMinimap(Transform canvasParent, Camera mainCam)
        {
            var mmCamGO = new GameObject("MinimapCamera");
            var mmCam = mmCamGO.AddComponent<Camera>();
            mmCam.orthographic = true;
            mmCam.orthographicSize = 26f;
            mmCam.nearClipPlane = 1f;
            mmCam.farClipPlane = 200f;
            mmCam.clearFlags = CameraClearFlags.SolidColor;
            // Negro puro: el minimapa no muestra el terreno real, solo los
            // íconos de colores de su propia capa (filtro de capas).
            mmCam.backgroundColor = Color.black;
            mmCam.depth = mainCam.depth - 1f;
            int minimapLayer = GetOrCreateMinimapLayer();
            mmCam.cullingMask = 1 << minimapLayer;
            // La cámara principal no debe ver los íconos flotando en el
            // mundo real.
            mainCam.cullingMask &= ~(1 << minimapLayer);
            mmCamGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mmCamGO.transform.position = new Vector3(0f, 60f, 0f);

            var rt = new RenderTexture(384, 384, 16) { name = "RT_Minimap" }; transientRuntimeAssets.Add(rt);
            mmCam.targetTexture = rt;

            var follow = mmCamGO.AddComponent<MinimapFollow>();
            minimapFollowRef = follow;

            // Borde: un marco un poco más grande y claro detrás del panel
            // negro, para que el minimapa se distinga del fondo del juego.
            var borderGO = new GameObject("MinimapBorder", typeof(Image));
            borderGO.transform.SetParent(canvasParent, false);
            borderGO.GetComponent<Image>().color = new Color(0.75f, 0.78f, 0.82f, 0.9f);
            var borderRt = borderGO.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(1f, 1f);
            borderRt.anchorMax = new Vector2(1f, 1f);
            borderRt.pivot = new Vector2(1f, 1f);
            // Margen unificado a 16px con el resto del HUD (antes 14).
            borderRt.anchoredPosition = new Vector2(-16f, -16f);
            borderRt.sizeDelta = new Vector2(228f, 228f);

            var frameGO = new GameObject("MinimapFrame", typeof(Image));
            frameGO.transform.SetParent(borderGO.transform, false);
            frameGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            var frameRt = frameGO.GetComponent<RectTransform>();
            StretchFull(frameRt);
            frameRt.offsetMin = new Vector2(3f, 3f);
            frameRt.offsetMax = new Vector2(-3f, -3f);

            var nLabelGO = new GameObject("N", typeof(Text));
            nLabelGO.transform.SetParent(borderGO.transform, false);
            var nLabel = nLabelGO.GetComponent<Text>();
            nLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nLabel.alignment = TextAnchor.MiddleCenter;
            nLabel.color = new Color(0.85f, 0.9f, 0.95f);
            nLabel.fontSize = FontChico;
            nLabel.fontStyle = FontStyle.Bold;
            nLabel.text = "N";
            var nRt = nLabelGO.GetComponent<RectTransform>();
            nRt.anchorMin = nRt.anchorMax = new Vector2(0.5f, 1f);
            nRt.pivot = new Vector2(0.5f, 1f);
            nRt.anchoredPosition = new Vector2(0f, -2f);
            nRt.sizeDelta = new Vector2(24f, 18f);

            var imgGO = new GameObject("MinimapImage", typeof(RawImage));
            imgGO.transform.SetParent(frameGO.transform, false);
            var rawImg = imgGO.GetComponent<RawImage>();
            rawImg.texture = rt;
            var imgRt = imgGO.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = new Vector2(4f, 4f);
            imgRt.offsetMax = new Vector2(-4f, -4f);

            BuildMinimapLegend(canvasParent);
        }

        // Leyenda de colores: sin esto, un punto azul y uno rojo en el
        // minimapa se interpretan por convencion o por prueba y error. Se
        // ancla debajo del minimapa, con exactamente los mismos colores
        // que usa MinimapIcon -- si alguno cambia, esta leyenda tiene que
        // cambiar con el (comparten las mismas constantes).
        static void BuildMinimapLegend(Transform canvasParent)
        {
            var legendGO = new GameObject("MinimapLegend", typeof(Image));
            legendGO.transform.SetParent(canvasParent, false);
            legendGO.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.75f);
            var legendRt = legendGO.GetComponent<RectTransform>();
            legendRt.anchorMin = new Vector2(1f, 1f);
            legendRt.anchorMax = new Vector2(1f, 1f);
            legendRt.pivot = new Vector2(1f, 1f);
            // Margen unificado a 16px (antes 14) y recalculado para
            // mantener el mismo hueco de 6px debajo del minimapa: con
            // el borde ahora en -16 (alto 228), su borde inferior queda
            // en y=-244; la leyenda arranca 6px mas abajo, en -250.
            legendRt.anchoredPosition = new Vector2(-16f, -250f);
            legendRt.sizeDelta = new Vector2(150f, 66f);

            (string label, Color color)[] entries =
            {
                ("Aliado", PlayerMinimapColor),
                ("Enemigo", EnemyMinimapColor),
                ("Vehículo", new Color(0.98f, 0.65f, 0.15f)),
            };

            for (int i = 0; i < entries.Length; i++)
            {
                var swatchGO = new GameObject($"Swatch_{entries[i].label}", typeof(Image));
                swatchGO.transform.SetParent(legendGO.transform, false);
                swatchGO.GetComponent<Image>().color = entries[i].color;
                var swRt = swatchGO.GetComponent<RectTransform>();
                swRt.anchorMin = swRt.anchorMax = new Vector2(0f, 1f);
                swRt.pivot = new Vector2(0f, 1f);
                swRt.anchoredPosition = new Vector2(8f, -8f - i * 20f);
                swRt.sizeDelta = new Vector2(12f, 12f);

                var labelGO = new GameObject($"Label_{entries[i].label}", typeof(Text));
                labelGO.transform.SetParent(legendGO.transform, false);
                var label = labelGO.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.text = entries[i].label;
                label.color = Color.white;
                label.fontSize = FontMicro;
                label.alignment = TextAnchor.MiddleLeft;
                var labelRt = labelGO.GetComponent<RectTransform>();
                labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot = new Vector2(0f, 1f);
                labelRt.anchoredPosition = new Vector2(26f, -6f - i * 20f);
                labelRt.sizeDelta = new Vector2(110f, 16f);
            }
        }
    }
}
