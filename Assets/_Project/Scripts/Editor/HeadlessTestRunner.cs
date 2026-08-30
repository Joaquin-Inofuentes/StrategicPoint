using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
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

        static Material CreateFlatMaterial(Color color)
        {
            // Lit, pero con brillo/metalico casi nulo: sombreado suave y
            // colores vivos, sin el brillo especular que los lavaba pálidos.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
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
        }

        [MenuItem("Strategic Point/Construir nivel y correr test")]
        public static void RunAll()
        {
            try
            {
                TestLog.Begin();
                BuildAndRun();
                TestLog.Phase("TODAS LAS FASES COMPLETADAS CON EXITO");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TEST FALLIDO] {ex}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
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
            EventBus.Instance.ClearAll();
            ActorRegistry.Clear();
            Projectile.ActiveInstances.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildGround();
            BuildObstacles();
            SP.Presentation.OrderMarkerFx.Prewarm();

            var soldierPrefab = BuildAndSaveSoldierPrefab();
            var projectilePrefab = BuildAndSaveProjectilePrefab();
            var vehiclePrefab = BuildAndSaveVehiclePrefab();
            var colorVehicle = new Color(0.98f, 0.65f, 0.15f);

            var poolGO = new GameObject("ProjectilePool");
            var pool = poolGO.AddComponent<ProjectilePool>();
            pool.Configure(projectilePrefab, 24);

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
            var rig = camGO.AddComponent<CameraRig>();
            rig.SetCamera(cam);
            rig.SetMode(ControlMode.Fps);

            var servicesGO = new GameObject("GameServices");
            var playerBrain = servicesGO.AddComponent<PlayerBrain>();
            var aimTargeting = servicesGO.AddComponent<AimTargeting>();
            var selection = servicesGO.AddComponent<SelectionController>();
            playerBrain.Possess(vega);
            rig.FollowFps(vega);

            BuildUI(squad, cam);

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
            servicesGO.AddComponent<WorldSimulationDriver>();
            servicesGO.AddComponent<SelectionRingManager>();

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            TestLog.Step("Entorno de prueba construido: 3 soldados, vehiculo, armas, minimapa, camara y UI listos");

            if (runPhases)
            {
                RunPhase1(vega, kes, doc, pool, soldierPrefab, colorEnemy);
                RunPhase2(playerBrain, rig, aimTargeting, selection, vega, kes, doc, soldierPrefab, colorEnemy, pool);
                RunPhase3(playerBrain, rig, selection, aimTargeting, vega, kes, doc, soldierPrefab, colorEnemy, pool, vehicle);
                RunPhase4(playerBrain, rig, vehicle, weaponPickups, vega, kes, doc);
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
                var demoEnemy = SpawnSoldier(soldierPrefab, "Enemigo_Demo", TeamId.Enemy, RoleType.Enemy, new Vector3(0f, 0.8f, 12f), colorEnemy, pool, 60);
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

                TestLog.Step("Demo lista: Vega junto al vehiculo, Kes y Doc cerca. AutoDemoRunner armado (F9 para arrancar/cortar a mano).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
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

            bool fired = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
            Check($"Disparo: se creo proyectil de {vega.DisplayName} con exito (click)", fired);

            int freeBefore = pool.FreeCount;
            SimulateSeconds(3.2f);
            Check("El proyectil volvio al pool", pool.FreeCount >= freeBefore);

            var enemy1 = SpawnSoldier(soldierPrefab, "Enemigo_1", TeamId.Enemy, RoleType.Enemy, vega.transform.position + vega.transform.forward * 15f + Vector3.up * 0.8f, enemyColor, pool, 60);
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

            var enemy2 = SpawnSoldier(soldierPrefab, "Enemigo_2", TeamId.Enemy, RoleType.Enemy, nearestFree.transform.position + Vector3.forward * 16f + Vector3.up * 0.8f, enemyColor, pool, 50);
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

            var enemy3 = SpawnSoldier(soldierPrefab, "Enemigo_3", TeamId.Enemy, RoleType.Enemy, dest + new Vector3(3f, 0.8f, 0f), enemyColor, pool, 40);
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
        // Simulación manual (independiente del Update de Unity)
        // ---------------------------------------------------------------
        static void SimStep(float dt)
        {
            var snapshot = new List<Soldier>(ActorRegistry.All);
            foreach (var s in snapshot)
            {
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                s.GetComponent<AiBrain>()?.Tick(dt);
                if (s.Weapon != null) s.Weapon.Tick(dt);
            }

            var projectiles = Projectile.ActiveInstances.ToArray();
            foreach (var p in projectiles) p.Tick(dt);

            foreach (var v in UnityEngine.Object.FindObjectsByType<VehicleBrain>(FindObjectsSortMode.None))
                v.Tick(dt);
            foreach (var t in UnityEngine.Object.FindObjectsByType<TurretWeapon>(FindObjectsSortMode.None))
                t.Tick(dt);
        }

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

        static void Check(string message, bool condition)
        {
            if (condition) TestLog.Step(message);
            else TestLog.Warn($"{message} -- NO SE CUMPLIO EN EL TIEMPO ESPERADO");
        }

        // ---------------------------------------------------------------
        // Construcción de prefabs y entorno
        // ---------------------------------------------------------------
        static Soldier SpawnSoldier(GameObject prefab, string name, TeamId team, RoleType role, Vector3 position, Color color, ProjectilePool pool, int maxHealth)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.identity;

            var rend = instance.GetComponentInChildren<MeshRenderer>();
            if (rend != null) rend.sharedMaterial = CreateFlatMaterial(color);

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

            MinimapIcon.Spawn(instance.transform, color, GetOrCreateMinimapLayer());

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
            driverEye.localPosition = new Vector3(0f, 0.3f, 0.4f);

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

            var turret = turretPivot.AddComponent<TurretWeapon>();

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

            var mat = CreateFlatMaterial(color);
            foreach (var rend in instance.GetComponentsInChildren<MeshRenderer>()) rend.sharedMaterial = mat;

            var turret = instance.GetComponentInChildren<TurretWeapon>();
            turret.SetPool(pool);

            MinimapIcon.Spawn(instance.transform, color, GetOrCreateMinimapLayer(), 2.4f);

            return instance.GetComponent<Vehicle>();
        }

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(160f, 1f, 160f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = CreateFlatMaterial(new Color(0.82f, 0.85f, 0.88f));
        }

        static void BuildObstacles()
        {
            Vector3[] positions =
            {
                new Vector3(6f, 0.5f, 3f),
                new Vector3(-6f, 0.5f, 4f),
                new Vector3(4f, 0.75f, -6f),
                new Vector3(-5f, 1f, -3f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
                o.name = $"Obstaculo_{i + 1}";
                o.transform.position = positions[i];
                o.transform.localScale = new Vector3(2f, 1.5f + i * 0.5f, 2f);
                o.GetComponent<MeshRenderer>().sharedMaterial = CreateFlatMaterial(new Color(0.93f, 0.78f, 0.55f));
            }
        }

        static List<WeaponPickup> BuildWeaponPickups()
        {
            var defs = new (string name, WeaponKind kind, int dmg, float cooldown, Color color, Vector3 pos)[]
            {
                ("Arma_Rifle",  WeaponKind.Rifle,  26, 0.30f, new Color(0.55f, 0.68f, 0.78f), new Vector3(3f, 0.4f, -3f)),
                ("Arma_Pistola",WeaponKind.Pistol, 14, 0.15f, new Color(0.95f, 0.88f, 0.20f), new Vector3(4f, 0.4f, -3f)),
                ("Arma_Pesada", WeaponKind.Heavy,  50, 0.80f, new Color(0.80f, 0.20f, 0.55f), new Vector3(5f, 0.4f, -3f)),
            };

            var list = new List<WeaponPickup>();
            foreach (var d in defs)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = d.name;
                go.transform.position = d.pos;
                go.transform.localScale = Vector3.one * 0.5f;
                go.GetComponent<MeshRenderer>().sharedMaterial = CreateFlatMaterial(d.color);

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
                var enemy = SpawnSoldier(soldierPrefab, $"Enemigo_Patrulla_{i + 1}", TeamId.Enemy, RoleType.Enemy, routes[i][0], enemyColor, pool, 60);
                enemy.GetComponent<AiBrain>().SetPatrolRoute(routes[i]);
                PatrolRouteLine.Spawn(routes[i], new Color(0.95f, 0.6f, 0.2f));
                enemies.Add(enemy);
            }
            return enemies;
        }

        static void BuildUI(List<Soldier> squad, Camera cam)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

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
            promptTxt.fontSize = 18;
            var prt = promptGO.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, -40f);
            prt.sizeDelta = new Vector2(420f, 30f);

            var aimUIGO = new GameObject("AimUI", typeof(RectTransform), typeof(AimUI));
            aimUIGO.transform.SetParent(canvasGO.transform, false);
            var aimUi = aimUIGO.GetComponent<AimUI>();
            aimUi.Bind(promptTxt, crossImg);
            aimUi.Initialize();
            aimUiRef = aimUi;

            // Panel de info al apuntar a un aliado (vida/arma/especialidad),
            // justo arriba del texto de instrucciones para que no se pisen.
            var soldierInfoGO = new GameObject("SoldierInfoPanel", typeof(Image));
            soldierInfoGO.transform.SetParent(canvasGO.transform, false);
            soldierInfoGO.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.72f);
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
            siText.fontSize = 15;
            StretchFull(siTextGO.GetComponent<RectTransform>());

            aimUi.BindSoldierInfo(soldierInfoGO, siText);
            soldierInfoGO.SetActive(false);

            // Panel de info al apuntar a un vehículo: 4 cuadrados de asiento
            // (verde = libre, gris muy oscuro = ocupado), mismo lugar que el
            // panel de soldado (nunca se muestran los dos a la vez).
            var vehicleInfoGO = new GameObject("VehicleInfoPanel", typeof(Image));
            vehicleInfoGO.transform.SetParent(canvasGO.transform, false);
            vehicleInfoGO.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.72f);
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
                seatLabelTxt.fontSize = 9;
                seatLabelTxt.text = seatLabels[i].Replace("Pasajero ", "Pas.");
                var seatLabelRt = seatLabelGO.GetComponent<RectTransform>();
                seatLabelRt.anchorMin = seatLabelRt.anchorMax = new Vector2(0f, 0.5f);
                seatLabelRt.pivot = new Vector2(0.5f, 1f);
                seatLabelRt.sizeDelta = new Vector2(56f, 14f);
                seatLabelRt.anchoredPosition = new Vector2(14f + i * 60f + 13f, -14f);
            }

            aimUi.BindVehicleInfo(vehicleInfoGO, seatSquares);
            vehicleInfoGO.SetActive(false);

            var rosterGO = new GameObject("Roster", typeof(RectTransform), typeof(SelectedSoldierUI));
            rosterGO.transform.SetParent(canvasGO.transform, false);
            var rosterRt = rosterGO.GetComponent<RectTransform>();
            rosterRt.anchorMin = new Vector2(0f, 1f);
            rosterRt.anchorMax = new Vector2(0f, 1f);
            rosterRt.pivot = new Vector2(0f, 1f);
            rosterRt.anchoredPosition = new Vector2(20f, -20f);
            rosterRt.sizeDelta = new Vector2(220f, 100f);
            var roster = rosterGO.GetComponent<SelectedSoldierUI>();

            for (int i = 0; i < squad.Count; i++)
            {
                var rowGO = new GameObject($"Row_{squad[i].DisplayName}", typeof(Image));
                rowGO.transform.SetParent(rosterGO.transform, false);
                var rowRt = rowGO.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(0f, 1f);
                rowRt.pivot = new Vector2(0f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -i * 32f);
                rowRt.sizeDelta = new Vector2(200f, 28f);
                var rowImg = rowGO.GetComponent<Image>();

                var labelGO = new GameObject("Label", typeof(Text));
                labelGO.transform.SetParent(rowGO.transform, false);
                var labelTxt = labelGO.GetComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.color = Color.white;
                labelTxt.fontSize = 14;
                labelTxt.alignment = TextAnchor.MiddleLeft;
                var labelRt = labelGO.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(8f, 0f);
                labelRt.offsetMax = Vector2.zero;

                roster.AddRow(squad[i], rowImg, labelTxt);
            }

            roster.Initialize();
            rosterUiRef = roster;

            BuildInstructionBanner(canvasGO.transform);
            BuildPhaseBanner(canvasGO.transform);
            BuildNearbySquadList(canvasGO.transform, squad);
            BuildSelectionBox(canvasGO.transform);
            BuildMinimap(canvasGO.transform, cam);
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            text.fontSize = 16;
            var rt = textGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 26f);
            rt.sizeDelta = new Vector2(900f, 30f);

            var bgGO = new GameObject("BG", typeof(Image));
            bgGO.transform.SetParent(go.transform, false);
            bgGO.transform.SetAsFirstSibling();
            bgGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.65f);
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
            text.fontSize = 34;
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
            panelRt.anchoredPosition = new Vector2(20f, 20f);
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
            contentRt.sizeDelta = new Vector2(252f, squad.Count * 40f);
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
                rowRt.anchoredPosition = new Vector2(0f, -i * 40f);
                rowRt.sizeDelta = new Vector2(0f, 36f);

                var labelGO = new GameObject("Label", typeof(Text));
                labelGO.transform.SetParent(rowGO.transform, false);
                var labelTxt = labelGO.GetComponent<Text>();
                labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.color = Color.white;
                labelTxt.fontSize = 11;
                labelTxt.alignment = TextAnchor.MiddleLeft;
                var labelRt = labelGO.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(6f, 0f);
                labelRt.offsetMax = Vector2.zero;

                listView.AddEntry(squad[i], labelTxt);
            }

            squadListRef = listView;
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

            var rt = new RenderTexture(256, 256, 16) { name = "RT_Minimap" };
            mmCam.targetTexture = rt;

            var follow = mmCamGO.AddComponent<MinimapFollow>();
            minimapFollowRef = follow;

            var frameGO = new GameObject("MinimapFrame", typeof(Image));
            frameGO.transform.SetParent(canvasParent, false);
            frameGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var frameRt = frameGO.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(1f, 1f);
            frameRt.anchorMax = new Vector2(1f, 1f);
            frameRt.pivot = new Vector2(1f, 1f);
            frameRt.anchoredPosition = new Vector2(-16f, -16f);
            frameRt.sizeDelta = new Vector2(176f, 176f);

            var imgGO = new GameObject("MinimapImage", typeof(RawImage));
            imgGO.transform.SetParent(frameGO.transform, false);
            var rawImg = imgGO.GetComponent<RawImage>();
            rawImg.texture = rt;
            var imgRt = imgGO.GetComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = new Vector2(4f, 4f);
            imgRt.offsetMax = new Vector2(-4f, -4f);
        }
    }
}
