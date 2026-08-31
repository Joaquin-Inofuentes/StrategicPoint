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
        static CanvasScaler hudScalerRef;
        static Text victoryStatsRef;
        static Text defeatStatsRef;
        static VehicleStatusView vehicleStatusRef;
        static TurretAimView turretAimRef;
        static OffscreenKillMarkerView offscreenKillRef;
        static DamageVignetteView damageVignetteRef;
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
            inputDriver.TurretAim = turretAimRef;
            inputDriver.Outcome = outcomeControllerRef;
            inputDriver.PauseRef = pauseControllerRef;
            inputDriver.PlayerHealth = playerHealthRef;
            inputDriver.SelectionCount = selectionCountRef;
            inputDriver.ModeToast = modeToastRef;
            servicesGO.AddComponent<WorldSimulationDriver>();
            servicesGO.AddComponent<SelectionRingManager>();
            var possessedMarker = servicesGO.AddComponent<PossessedMarkerView>();
            possessedMarker.SetInitial(playerBrain.Current);
            var killDirector = servicesGO.AddComponent<KillFeedbackDirector>();
            killDirector.Brain = playerBrain;
            killDirector.OffscreenMarker = offscreenKillRef;
            // El feed ya no se suscribe solo al bus: lo dispara el director
            // despues de actualizar su estado, para que el texto no salga
            // corrido una baja (ver comentario en KillFeedView.ShowKill).
            killDirector.Feed = killFeedRef;
            servicesGO.AddComponent<AttackLineManager>();
            servicesGO.AddComponent<OrderLineManager>();
            servicesGO.AddComponent<FloatingDamageTextManager>();
            servicesGO.AddComponent<PostFxDirector>();
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

            TestLog.Step("Entorno de prueba construido: 3 soldados, vehiculo, armas, minimapa, camara y UI listos");

            if (runPhases)
            {
                RunPhase1(vega, kes, doc, pool, soldierPrefab, colorEnemy);
                RunPhase2(playerBrain, rig, aimTargeting, selection, vega, kes, doc, soldierPrefab, colorEnemy, pool);
                RunPhase3(playerBrain, rig, selection, aimTargeting, vega, kes, doc, soldierPrefab, colorEnemy, pool, vehicle);
                RunPhase4(playerBrain, rig, vehicle, weaponPickups, vega, kes, doc);

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

                BuildTestButton(canvasRootRef, runner);

                TestLog.Step("Demo lista: Vega junto al vehiculo, Kes y Doc cerca. AutoDemoRunner armado (F9 o el boton 'Test' para arrancar/cortar a mano).");
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

            bool fired = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
            Check($"Disparo: se creo proyectil de {vega.DisplayName} con exito (click)", fired);

            int freeBefore = pool.FreeCount;
            SimulateSeconds(3.2f);
            Check("El proyectil volvio al pool", pool.FreeCount >= freeBefore);

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
        // Simulación manual (independiente del Update de Unity)
        // ---------------------------------------------------------------
        static void SimStep(float dt)
        {
            SP.Core.SpatialGrid.Rebuild();
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
            if (barrelRend != null) barrelRend.sharedMaterial = CreateFlatMaterial(new Color(0.12f, 0.12f, 0.13f));

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
            labelTxt.fontSize = 18;
            labelTxt.text = label;
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = new Vector2(20f, y);
            labelRt.sizeDelta = new Vector2(260f, 24f);

            return toggle;
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
                o.GetComponent<MeshRenderer>().sharedMaterial = CreateFlatMaterial(new Color(0.93f, 0.78f, 0.55f));
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
                prop.GetComponent<MeshRenderer>().sharedMaterial = CreateFlatMaterial(new Color(0.45f, 0.55f, 0.35f));
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
            promptTxt.fontSize = 24;
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
            ammoWarnTxt.fontSize = 22;
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
            siText.fontSize = 20;
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
                seatLabelTxt.fontSize = 13;
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
            wsText.fontSize = 16;
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
            phText.fontSize = 16;
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
            msText.fontSize = 16;
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
            selCountText.fontSize = 15;
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
            vsSpeedTxt.fontSize = 20;
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
            vsSeatTxt.fontSize = 13;
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
            vsGunnerTxt.fontSize = 13;
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
            killFeedTxt.fontSize = 40;
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
            deadText.fontSize = 22;
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
            toastText.fontSize = 18;
            toastText.fontStyle = FontStyle.Bold;
            StretchFull(toastTextGO.GetComponent<RectTransform>());

            var modeToast = toastGO.GetComponent<ModeToastView>();
            modeToast.Bind(toastText, toastGO.GetComponent<CanvasGroup>());
            modeToastRef = modeToast;

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
                labelTxt.fontSize = 14;
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
            ring.sharedMaterial = CreateFlatMaterial(new Color(0.95f, 0.55f, 0.1f));
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
            text.fontSize = 22;
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
            text.fontSize = 44;
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
                labelTxt.fontSize = 14;
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

        // Botón real (no solo la tecla F9) para arrancar/cortar la demo
        // automática a mano, sin que el jugador tenga que saber el atajo.
        static void BuildTestButton(Transform canvasParent, SP.Presentation.AutoDemoRunner runner)
        {
            var go = new GameObject("TestButton", typeof(Image), typeof(Button));
            go.transform.SetParent(canvasParent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.16f, 0.45f, 0.85f, 0.9f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 16f);
            rt.sizeDelta = new Vector2(150f, 44f);

            var labelGO = new GameObject("Label", typeof(Text));
            labelGO.transform.SetParent(go.transform, false);
            var label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 18;
            label.text = "Test (F9)";
            StretchFull(labelGO.GetComponent<RectTransform>());

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                if (runner.IsRunning) runner.StopDemo();
                else runner.StartDemo();
            });
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
            txt.fontSize = 24;
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
            labelTxt.fontSize = 18;
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
            valueTxt.fontSize = 16;
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
            pauseTitleTxt.fontSize = 48;
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
            settingsTitleTxt.fontSize = 30;
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
            invertLabelTxt.fontSize = 18;
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
            controlsTitleTxt.fontSize = 28;
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
            controlsListTxt.fontSize = 16;
            controlsListTxt.text =
                "A pie: [WASD] moverse · [Click] disparar · [R] recargar\n" +
                "[1][2][3] cambiar de arma · [F] poseer aliado · [E] interactuar\n" +
                "[TAB] alternar vista RTS/FPS\n\n" +
                "Vista RTS: [WASD] panear · [Rueda] zoom · [Arrastrar] seleccionar\n" +
                "[Shift+Click] sumar a seleccion · [Ctrl+A] seleccionar escuadra\n" +
                "[T]/[Click der.] mover selección · [X] cancelar orden\n" +
                "[G] subir/bajar del vehículo · [F] poseer\n\n" +
                "Vehículo: [WASD] conducir · [G] frenar · [1][2] cambiar asiento\n" +
                "[V] cámara · [E] bajar · [Click] disparar torreta (artillero)\n\n" +
                "[ESC] pausa/volver atrás un paso";
            var controlsListRt = controlsListGO.GetComponent<RectTransform>();
            controlsListRt.anchorMin = new Vector2(0f, 0f);
            controlsListRt.anchorMax = new Vector2(1f, 1f);
            controlsListRt.offsetMin = new Vector2(24f, 60f);
            controlsListRt.offsetMax = new Vector2(-24f, -60f);

            var controlsBackBtn = BuildUIButton(controlsPanelGO.transform, "BackButton", "VOLVER", new Vector2(0f, -180f), new Color(0.5f, 0.5f, 0.5f));
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
            confirmExitTxt.fontSize = 20;
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
            titleTxt.fontSize = 72;
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
            statsTxt.fontSize = 22;
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

            var rt = new RenderTexture(384, 384, 16) { name = "RT_Minimap" };
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
            nLabel.fontSize = 14;
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
                var swatchGO = new GameObject("Swatch", typeof(Image));
                swatchGO.transform.SetParent(legendGO.transform, false);
                swatchGO.GetComponent<Image>().color = entries[i].color;
                var swRt = swatchGO.GetComponent<RectTransform>();
                swRt.anchorMin = swRt.anchorMax = new Vector2(0f, 1f);
                swRt.pivot = new Vector2(0f, 1f);
                swRt.anchoredPosition = new Vector2(8f, -8f - i * 20f);
                swRt.sizeDelta = new Vector2(12f, 12f);

                var labelGO = new GameObject("Label", typeof(Text));
                labelGO.transform.SetParent(legendGO.transform, false);
                var label = labelGO.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.text = entries[i].label;
                label.color = Color.white;
                label.fontSize = 12;
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
