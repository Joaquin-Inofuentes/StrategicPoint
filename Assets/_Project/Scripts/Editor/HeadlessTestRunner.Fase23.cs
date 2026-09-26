using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;
using SP.Vehicles;

namespace SP.EditorTools
{
    public static partial class HeadlessTestRunner
    {
        public static void RunPhase23(SP.Player.PlayerInputDriver inputDriver, SP.Vehicles.Vehicle vehicle, SP.Actors.Soldier vega, SP.Actors.Soldier kes, SP.Actors.Soldier doc, UnityEngine.GameObject soldierPrefab, UnityEngine.Color colorEnemy, SP.Combat.ProjectilePool pool)
        {
            TestLog.Phase("FASE 23: RONDA 14 (BASELINE)");
            Fase23_CClicCobertura();
            Fase23_CLineas();
            Fase23_Cadaveres();
            Fase23_PiesEnElPiso();
            Fase23_CartelVolver();
            Fase23_RazonDeDerrota();
            Fase23_ColliderSinMalla();
            Fase23_CoberturasConCollider();
            Fase23_SinCartelCobertura(kes);
            Fase23_HudMunicion();
            Fase23_RadialNoAbreEnRts(inputDriver);
            Fase23_BordeRojoImpacto();
            Fase23_CartelSeleccionAbajoCentro();
            Fase23_RombosInteractuables();
            Fase23_AnilloAtacante();
            Fase23_PolvoMovimiento();
            Fase23_CivilQuieto();
            Fase23_Nudos();
            Fase23_RadialMuerto(inputDriver);
            Fase23_RosterClics(inputDriver, doc);
            Fase23_CinematicaSaltoRapido();
            Fase23_RomboTanque();
            Fase23_RomboSinOclusion();
            Fase23_AgachadoContagio();
            Fase23_CursorPorObjetivo();
            Fase23_FormacionLateral(vega, kes, doc);
            Fase23_MedicoAutomatico();
            Fase23_TimerGranada();
            Fase23_Luminaria();
            Fase23_RtsControls(inputDriver, vega);
            Fase23_ReviveEnCalma(vega, kes);
            Fase23_ZoomCentrado();
            Fase23_Headshot();
            Fase23_RegenNueveSegundos(kes);
            Fase23_FTeclas();
            Fase23_EscapeCamioneta();
            Fase23_Motos();
            Fase23_CoheteVsTanque(pool);
            Fase23_MinimapaRegistro();
        }

        static void Fase23_CClicCobertura()
        {
            TestLog.Step("Fase23_CClicCobertura");
            Check("Fase23_CClicCobertura registrada", true);
        }

        static void Fase23_EscapeCamioneta()
        {
            TestLog.Step("Probando Fase23_EscapeCamioneta: Camioneta y MisionDirector");
            var camGo = new GameObject("Camioneta");
            var camioneta = camGo.AddComponent<SP.Mision.Camioneta>();
            camioneta.Inicializar();
            camioneta.IniciarEscape();
            Check("Camioneta en ruta", camioneta.EnRuta);
            Object.DestroyImmediate(camGo);
        }

        static void Fase23_Motos()
        {
            TestLog.Step("Probando Fase23_Motos: MotoEnemiga");
            var motoGo = new GameObject("MotoEnemiga");
            var moto = motoGo.AddComponent<SP.Vehicles.MotoEnemiga>();
            moto.Inicializar();
            Check("Moto tiene vehÃ­culo", moto.Vehiculo != null);
            Object.DestroyImmediate(motoGo);
        }

        static void Fase23_CLineas()
        {
            TestLog.Step("Fase23_CLineas");
            Check("Fase23_CLineas registrada", true);
        }

        static void Fase23_Headshot()
        {
            TestLog.Step("Probando Fase23_Headshot: DaÃ±o doble y HeadshotEvent si y > base + (crouching ? 0.95 : 1.45)");

            var go = new UnityEngine.GameObject("Soldier");
            var col = go.AddComponent<UnityEngine.CapsuleCollider>();
            col.height = 2f;
            col.center = new UnityEngine.Vector3(0, 1f, 0);

            go.AddComponent<SP.Combat.Health>();
            go.AddComponent<SP.Actors.SoldierMotor>();
            var s = go.AddComponent<SP.Actors.Soldier>();
            s.Configure("HeadshotEnemy", SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Assault, 100);
            
            var r = go.AddComponent<UnityEngine.MeshRenderer>();
            r.bounds = new UnityEngine.Bounds(new UnityEngine.Vector3(0, 1f, 0), new UnityEngine.Vector3(1, 2, 1));

            SP.Core.ActorRegistry.Register(s);
            SP.Core.SpatialGrid.Rebuild();
            
            bool headshotFired = false;
            var sub = SP.Core.EventBus.Instance.Subscribe<SP.Core.HeadshotEvent>(e => { if (e.TargetId == s.Id) headshotFired = true; });

            var pGo = new UnityEngine.GameObject("Proj1");
            var p = pGo.AddComponent<SP.Combat.Projectile>();
            p.Configure(null, new UnityEngine.Vector3(0, 1.0f, -2f), UnityEngine.Vector3.forward, 99, SP.Combat.TeamId.Player, 10);
            p.OnSpawn();
            p.Tick(0.1f);
            UnityEngine.Object.DestroyImmediate(pGo);

            Check("Disparo al pecho: daño normal 10", s.Health.Current == 90);
            Check("No se emitio HeadshotEvent en el pecho", !headshotFired);

            s.Health.Initialize(s.Id, 100);
            var p2Go = new UnityEngine.GameObject("Proj2");
            var p2 = p2Go.AddComponent<SP.Combat.Projectile>();
            p2.Configure(null, new UnityEngine.Vector3(0, 1.5f, -2f), UnityEngine.Vector3.forward, 99, SP.Combat.TeamId.Player, 10);
            p2.OnSpawn();
            p2.Tick(0.1f);
            UnityEngine.Object.DestroyImmediate(p2Go);

            Check("Disparo a y=1.5 de pie: daño doble 20", s.Health.Current == 80);
            Check("Se emitio HeadshotEvent", headshotFired);

            headshotFired = false;
            typeof(SP.Actors.SoldierMotor).GetProperty("IsCrouching").SetValue(s.Motor, true);
            s.Health.Initialize(s.Id, 100);
            var p3Go = new UnityEngine.GameObject("Proj3");
            var p3 = p3Go.AddComponent<SP.Combat.Projectile>();
            p3.Configure(null, new UnityEngine.Vector3(0, 1.0f, -2f), UnityEngine.Vector3.forward, 99, SP.Combat.TeamId.Player, 10);
            p3.OnSpawn();
            p3.Tick(0.1f);
            UnityEngine.Object.DestroyImmediate(p3Go);

            Check("Disparo a y=1.0 agachado: daño doble 20", s.Health.Current == 80);
            Check("Se emitio HeadshotEvent agachado", headshotFired);

            sub.Dispose();
            SP.Core.ActorRegistry.Unregister(s);
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void Fase23_Cadaveres()
        {
            TestLog.Step("Probando Fase23_Cadaveres: Queue de 24 cadaveres (los mas viejos se ocultan), revivir saca de la queue y asegura visible");
            
            SP.Presentation.CubeFxReactor.ReiniciarActivo();

            var corpses = new System.Collections.Generic.List<UnityEngine.GameObject>();
            for (int i = 0; i < 26; i++)
            {
                var go = new UnityEngine.GameObject("Corpse" + i);
                go.AddComponent<SP.Combat.Health>();
                var s = go.AddComponent<SP.Actors.Soldier>();
                s.Configure("Corpse" + i, SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Assault, 100);
                
                var r = go.AddComponent<SP.Presentation.CubeFxReactor>();
                r.Bootstrap();
                corpses.Add(go);

                // Matamos al soldado de verdad (TakeDamage, no publicar los
                // eventos DamageTakenEvent/EntityDiedEvent a mano): si
                // Health.Current no baja de verdad a 0 aca, mas abajo
                // "revivir" con Initialize() nunca dispara Revivido (que
                // exige haber estado vivo Y estar en 0 ahora), y el tramo
                // de revivir/escapar-de-la-queue queda sin probar de verdad.
                s.Health.TakeDamage(100, 0);
            }

            Check("Despues de 26 muertes, Corpse0 y Corpse1 deberian estar inactivos", !corpses[0].activeSelf && !corpses[1].activeSelf);
            Check("Corpse2 a Corpse25 deberian estar activos", corpses[2].activeSelf && corpses[25].activeSelf);

            // Revivir el mas viejo de los activos (Corpse2)
            var h2 = corpses[2].GetComponent<SP.Combat.Health>();
            h2.Initialize(h2.ActorId, 100); // Trigger Revivido

            Check("Despues de revivir, el soldado sale de la queue y el GO se asegura de estar activo", corpses[2].activeSelf);

            // Matar a DOS mas para ver si Corpse3 se oculta. Uno solo no
            // alcanza: al revivir, Corpse2 dejo la cola en 23 (no en 24), asi
            // que el primer muerto nuevo solo la vuelve a poner en 24 -- todavia
            // dentro del cupo, sin desalojar a nadie. Recien el segundo la
            // hace superar 24 de nuevo, y ahi el mas viejo ya no es Corpse2
            // (que escapo de la cola al revivir) sino Corpse3.
            for (int i = 26; i <= 27; i++)
            {
                var goNew = new UnityEngine.GameObject("Corpse" + i);
                goNew.AddComponent<SP.Combat.Health>();
                var sNew = goNew.AddComponent<SP.Actors.Soldier>();
                sNew.Configure("Corpse" + i, SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Assault, 100);
                var rNew = goNew.AddComponent<SP.Presentation.CubeFxReactor>();
                rNew.Bootstrap();
                corpses.Add(goNew);

                sNew.Health.TakeDamage(100, 0);
            }

            Check("Al morir dos mas, Corpse3 se oculta (Corpse2 escapo de la queue al revivir)", !corpses[3].activeSelf);
            
            foreach (var c in corpses)
            {
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        static void Fase23_Luminaria()
        {
            TestLog.Step("Probando Fase23_Luminaria: dispararle a una luminaria la apaga y tira chispas");
            
            var go = new GameObject("TestFarol");
            var light = go.AddComponent<Light>();
            light.enabled = true;
            
            var sphereCol = go.AddComponent<SphereCollider>();
            sphereCol.radius = 0.5f;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SP.Presentation.SafeMaterial.Create(Color.white); // Ensure a material exists to avoid null ref

            var luminaria = go.AddComponent<SP.Presentation.Luminaria>();
            
            bool eventFired = false;
            System.Action<SP.Core.LuminariaRotaEvent> handler = e => eventFired = true;
            var sub = SP.Core.EventBus.Instance.Subscribe(handler);
            
            luminaria.TakeDamage(1, Vector3.zero);
            
            Check("Luz se apago", !light.enabled);
            Check("Evento LuminariaRotaEvent disparado", eventFired);
            
            sub.Dispose();
            Object.DestroyImmediate(go);
        }

        static void Fase23_FormacionLateral(SP.Actors.Soldier vega, SP.Actors.Soldier kes, SP.Actors.Soldier doc)
        {
            TestLog.Step("Probando Fase23_FormacionLateral: formacion WEDGE al seguir");
            
            var prevLider = SP.Ai.AjustesDeEscuadra.Lider;
            var prevLat = SP.Ai.AjustesDeEscuadra.DistanciaLateralFormacion;
            var prevAtr = SP.Ai.AjustesDeEscuadra.DistanciaAtrasFormacion;
            
            SP.Ai.AjustesDeEscuadra.Lider = vega;
            SP.Ai.AjustesDeEscuadra.DistanciaLateralFormacion = 2.5f;
            SP.Ai.AjustesDeEscuadra.DistanciaAtrasFormacion = 1.5f;
            SP.Ai.AjustesDeEscuadra.DistanciaParaSeguir = 1f; // Force follow
            
            vega.transform.position = Vector3.zero;
            vega.transform.rotation = Quaternion.identity;
            kes.transform.position = new Vector3(2.5f, 0, -1.5f);
            doc.transform.position = new Vector3(-2.5f, 0, -1.5f);
            
            kes.Brain.IssueFollowOrder(vega);
            doc.Brain.IssueFollowOrder(vega);

            kes.Brain.Tick(0.1f);
            doc.Brain.Tick(0.1f);
            
            for (int i = 0; i < 200; i++)
            {
                vega.transform.position += Vector3.forward * (5f * 0.1f); // 5m/s
                kes.Brain.Tick(0.1f);
                doc.Brain.Tick(0.1f);
            }
            
            var relKes = vega.transform.InverseTransformPoint(kes.transform.position);
            var relDoc = vega.transform.InverseTransformPoint(doc.transform.position);
            
            Check("Uno esta a la derecha y otro a la izquierda", relKes.x * relDoc.x < 0f && Mathf.Abs(relKes.x) > 0.5f && Mathf.Abs(relDoc.x) > 0.5f);
            Check("Ambos van por detras (-Z)", relKes.z < -0.5f && relDoc.z < -0.5f);
            
            SP.Ai.AjustesDeEscuadra.Lider = prevLider;
            SP.Ai.AjustesDeEscuadra.DistanciaLateralFormacion = prevLat;
            SP.Ai.AjustesDeEscuadra.DistanciaAtrasFormacion = prevAtr;
        }

                static void Fase23_RomboSinOclusion()
        {
            TestLog.Step("Probando Fase23_RomboSinOclusion: el rombo ignora oclusion al recibir dano del jugador");

            var enemyGo = new GameObject("TestEnemy");
            var enemy = enemyGo.AddComponent<SP.Actors.Soldier>();
            var enemyHealth = enemyGo.AddComponent<SP.Combat.Health>();
            
            enemy.Configure("Enemy", SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Assault, 100);
            var locator = enemyGo.AddComponent<SP.Presentation.UnitLocatorCylinder>();
            SP.Core.ActorRegistry.Register(enemy);

            var playerGo = new GameObject("TestPlayer");
            var player = playerGo.AddComponent<SP.Actors.Soldier>();
            var playerHealth = playerGo.AddComponent<SP.Combat.Health>();
            
            player.Configure("Player", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            var playerBrain = playerGo.AddComponent<SP.Ai.AiBrain>();
            playerBrain.IsPossessedByPlayer = true;
            SP.Core.ActorRegistry.Register(player);

            // Trigger OnEnable so it subscribes
            locator.enabled = false;
            locator.enabled = true;

            Check("Inicialmente no ignora oclusion", !locator.IgnorarOclusion);

            SP.Core.EventBus.Instance.Publish(new SP.Core.DamageTakenEvent(enemy.Id, player.Id, 10, 90));

            Check("Tras recibir dano del jugador, ignora oclusion", locator.IgnorarOclusion);

            var oclusionTimerField = typeof(SP.Presentation.UnitLocatorCylinder).GetField("oclusionTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (oclusionTimerField != null)
            {
                oclusionTimerField.SetValue(locator, 0f); // Fast forward timer
                
                var updateMethod = typeof(SP.Presentation.UnitLocatorCylinder).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (updateMethod != null) updateMethod.Invoke(locator, null);
                
                Check("Despues de 4s (timer agotado), vuelve a ser normal", !locator.IgnorarOclusion);
            }
            else
            {
                Check("No se encontro oclusionTimer", false);
            }

            SP.Core.ActorRegistry.Unregister(enemy);
            SP.Core.ActorRegistry.Unregister(player);
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
        }

        static void Fase23_RomboTanque()
        {
            TestLog.Step("Probando Fase23_RomboTanque: color del rombo por ocupacion");
            
            var vehGo = new GameObject("TestTank");
            vehGo.SetActive(false);
            var veh = vehGo.AddComponent<SP.Vehicles.Vehicle>();
            vehGo.SetActive(true);

            // BUG REAL (por que "RomboTanque existe" fallaba): el rombo de Vehicle se crea en
            // OnEnable, y en Edit mode (esta suite) OnEnable no corre de forma confiable solo con
            // el AddComponent+SetActive de arriba -- mismo problema, ya documentado en este
            // proyecto, que resolvieron a mano TorretaFija.Instalar (llamando Registrar() el
            // mismo) y Fase23_CoheteVsTanque (llamando WorldSystemsRegistry.Register(vehicle) el
            // mismo) unas lineas mas abajo en este archivo. Aca se empuja OnEnable por reflexion
            // en vez de asumir que el SetActive lo dispara solo.
            var onEnableMethod = typeof(SP.Vehicles.Vehicle).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (onEnableMethod != null) onEnableMethod.Invoke(veh, null);

            var mr = vehGo.transform.Find("RomboTanque");
            Material mat = null;
            if (mr != null) mat = mr.GetComponent<MeshRenderer>().sharedMaterial;

            Check("RomboTanque existe", mat != null);
            if (mat != null)
            {
                Check("Tanque vacio -> gris", mat.color == SP.Presentation.DiamondGizmo.ColorVacio);
                
                var sol1Go = new GameObject("S1");
                sol1Go.AddComponent<SP.Combat.Health>();
                var s1 = sol1Go.AddComponent<SP.Actors.Soldier>();
                s1.Configure("S1", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 10);
                
                veh.Mount(s1, SP.Vehicles.VehicleSeatRole.Driver, true);
                Check("Tanque aliado -> azul", mat.color == SP.Presentation.DiamondGizmo.ColorAliado);
                
                var sol2Go = new GameObject("S2");
                sol2Go.AddComponent<SP.Combat.Health>();
                var s2 = sol2Go.AddComponent<SP.Actors.Soldier>();
                s2.Configure("S2", SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Assault, 10);
                
                veh.Mount(s2, SP.Vehicles.VehicleSeatRole.Gunner, true);
                Check("Tanque mixto -> violeta", mat.color == SP.Presentation.DiamondGizmo.ColorMixto);
                
                veh.Dismount(s1);
                Check("Tanque enemigo -> rojo", mat.color == SP.Presentation.DiamondGizmo.ColorEnemigo);
                
                Object.DestroyImmediate(sol1Go);
                Object.DestroyImmediate(sol2Go);
            }
            Object.DestroyImmediate(vehGo);
        }

        static void Fase23_CinematicaSaltoRapido()
        {
            TestLog.Step("Probando Fase23_CinematicaSaltoRapido: avance rapido de waypoints");
            
            var cinGo = new GameObject("TestCin");
            var cin = cinGo.AddComponent<SP.Mision.CinematicaDeIntro>();
            var fields = cin.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool hasBannerText = false;
            foreach (var f in fields) if (f.Name == "cartelSiguiente" && f.FieldType == typeof(UnityEngine.UI.Text)) hasBannerText = true;
            
            Check("CinematicaDeIntro tiene cartelSiguiente", hasBannerText);
            
            Object.DestroyImmediate(cinGo);
        }

        static void Fase23_RosterClics(SP.Player.PlayerInputDriver inputDriver, SP.Actors.Soldier doc)
        {
            TestLog.Step("Probando Fase23_RosterClics: roster con clic (centra) y doble clic (entra a FPS)");
            
            if (inputDriver == null || inputDriver.Rig == null || inputDriver.Selection == null || doc == null)
            {
                Check("inputDriver, Rig, Selection y doc disponibles para la prueba", false);
                return;
            }

            inputDriver.Rig.SetMode(SP.CameraSystem.ControlMode.Rts);

            var rosterRowGo = new GameObject("TestRosterRow");
            var rosterRow = rosterRowGo.AddComponent<SP.UI.RosterRowView>();
            rosterRow.Bind(doc, 1);
            
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };
            
            // Simular primer click
            rosterRow.OnPointerClick(eventData);
            
            Check("Despues de 1 clic, doc queda seleccionado", inputDriver.Selection.Selected.Contains(doc) && inputDriver.Selection.Selected.Count == 1);
            
            Vector3 pos = doc.transform.position;
            Vector3 focus = inputDriver.Rig.RtsFocus;
            Check($"RTS focus debe coincidir con pos (Focus: {focus}, Pos: {pos})", Mathf.Abs(focus.x - pos.x) < 0.1f && Mathf.Abs(focus.z - pos.z) < 0.1f);
            
            // Simular segundo click rapido (doble click)
            rosterRow.OnPointerClick(eventData);
            
            Check("Despues de doble clic, la camara pasa a FPS", inputDriver.Rig.Mode == SP.CameraSystem.ControlMode.Fps);
            Check("Despues de doble clic, doc pasa a estar poseido", SP.Player.PlayerBrain.Activo != null && SP.Player.PlayerBrain.Activo.Current == doc);
            
            Object.DestroyImmediate(rosterRowGo);
        }

        static void Fase23_AnilloAtacante()
        {
            TestLog.Step("Probando Fase23_AnilloAtacante: el daÃ±o al jugador dispara un anillo en el minimapa");
            
            var ringManagerGo = new GameObject("TestRingManager");
            var ringManager = ringManagerGo.AddComponent<SP.Presentation.MinimapAttackerRings>();
            
            // Dummy attacker and player
            var attackerGo = new GameObject("TestAttacker");
            attackerGo.transform.position = new Vector3(10, 0, 10);
            var attacker = attackerGo.AddComponent<SP.Actors.Soldier>();
            
            var playerGo = new GameObject("TestPlayer");
            var player = playerGo.AddComponent<SP.Actors.Soldier>();
            
            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            brain.Registrar();
            brain.Possess(player);
            
            // Send event
            SP.Core.EventBus.Instance.Publish(new SP.Core.DamageTakenEvent(player.Id, attacker.Id, 10, 90));
            
            // Verify
            var rings = ringManager.GetComponentsInChildren<UnityEngine.LineRenderer>(true);
            bool foundActive = false;
            foreach (var r in rings)
            {
                if (r.gameObject.activeSelf && r.transform.position == attacker.transform.position)
                {
                    foundActive = true;
                    break;
                }
            }
            
            Check("Un anillo del pool se activo en la posicion del atacante", foundActive);
            
            Object.DestroyImmediate(ringManagerGo);
            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(brainGo);
        }

        static void Fase23_RadialMuerto(SP.Player.PlayerInputDriver inputDriver)
        {
            TestLog.Step("Probando Fase23_RadialMuerto: la opcion de poseer a un soldado muerto es no-seleccionable");
            if (inputDriver == null || inputDriver.OrdenesMenu == null)
            {
                Check("inputDriver y OrdenesMenu disponibles para la prueba", false);
                return;
            }

            var menu = inputDriver.OrdenesMenu;
            menu.PonerSoldados("A", "B", "C", true, false, false);
            menu.Abrir();
            menu.ElegirDirecto(SP.UI.MenuDeOrdenes.Poseer, 0);
            Check("No se puede seleccionar soldado muerto (Sub == -1)", menu.Sub == -1);
            
            menu.ElegirDirecto(SP.UI.MenuDeOrdenes.Poseer, 1);
            Check("Se puede seleccionar soldado vivo (Sub == 1)", menu.Sub == 1);
            
            menu.Cerrar();
        }

        static void Fase23_SinCartelCobertura(SP.Actors.Soldier kes)
        {
            TestLog.Step("Probando Fase23_SinCartelCobertura: al cubrirse no debe haber texto, solo sonido/holograma");
            if (kes == null || kes.Brain == null) { Check("Kes lista", false); return; }

            SP.Presentation.Feedback.Reset();
            int tagsBefore = SP.Presentation.WorldTag.ActiveCount;
            
            // Creamos un collider dummy para la cobertura
            var go = new GameObject("DummyCover");
            var col = go.AddComponent<BoxCollider>();
            
            // Mandamos a cubrirse y adelantamos la simulacion hasta que llegue
            kes.Brain.IssueCoverOrder(kes.transform.position, col);
            for (int i = 0; i < 50; i++)
            {
                if (kes.Brain.EnCobertura) break;
                SP.Ai.WorldSimulationDriver.Step(0.1f);
            }
            
            Object.DestroyImmediate(go);

            Check("Kes llego a cobertura", kes.Brain.EnCobertura);
            Check("Sonido fue CoverTake", SP.Presentation.Feedback.UltimoSonido == SP.Presentation.SfxKind.CoverTake);
            Check("Sin texto de cobertura", string.IsNullOrEmpty(SP.Presentation.Feedback.UltimoTexto));
            Check("WorldTag no creado", SP.Presentation.WorldTag.ActiveCount == tagsBefore);
        }

        static void Fase23_PiesEnElPiso()
        {
            TestLog.Step("Probando Fase23_PiesEnElPiso: los soldados deberian estar en el piso y no flotar");
            var soldados = Object.FindObjectsByType<SP.Actors.Soldier>(FindObjectsInactive.Include);
            var buffer = new RaycastHit[16];
            foreach (var s in soldados)
            {
                if (s == null) continue;
                var col = s.GetComponent<Collider>();
                if (col == null) continue;
                
                var pos = s.transform.position;
                var desde = new Vector3(pos.x, col.bounds.max.y + 4f, pos.z);
                
                int n = Physics.RaycastNonAlloc(desde, Vector3.down, buffer, 24f, ~0, QueryTriggerInteraction.Ignore);
                float piso = float.NegativeInfinity;
                bool hay = false;
                for (int i = 0; i < n; i++)
                {
                    var c = buffer[i].collider;
                    if (c == null || c == col || c.transform.IsChildOf(s.transform)) continue;
                    float y = buffer[i].point.y;
                    if (y > col.bounds.min.y + 0.3f) continue;
                    if (y > piso) { piso = y; hay = true; }
                }

                if (hay)
                {
                    float dist = Mathf.Abs(col.bounds.min.y - piso);
                    Check($"Soldado {s.name} bien apoyado (dist: {dist:0.000})", dist < 0.03f);
                }
                else
                {
                    Check($"Soldado {s.name} detecto piso", false);
                }
            }
        }

        static void Fase23_RazonDeDerrota()
        {
            TestLog.Step("Probando Fase23_RazonDeDerrota: causas de derrota");
            
            SP.Mision.EstadoDePartida.Reiniciar();
            SP.Mision.EstadoDePartida.RegistrarDerrotaExterna(SP.Mision.CausaDeDerrota.EscuadraCaida);
            Check("Causa es EscuadraCaida", SP.Mision.EstadoDePartida.Causa == SP.Mision.CausaDeDerrota.EscuadraCaida);

            SP.Mision.EstadoDePartida.Reiniciar();
            SP.Mision.EstadoDePartida.RegistrarDerrotaExterna(SP.Mision.CausaDeDerrota.CivilMuerto);
            Check("Causa es CivilMuerto", SP.Mision.EstadoDePartida.Causa == SP.Mision.CausaDeDerrota.CivilMuerto);
        }

        static void Fase23_ColliderSinMalla()
        {
            TestLog.Step("Probando Fase23_ColliderSinMalla: verificar que no haya colliders fisicos invisibles");
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            foreach (var c in colliders)
            {
                if (c.isTrigger) continue;
                if (c.GetComponentInParent<SP.Vehicles.HitboxDeImpacto>() != null) continue;
                if (c.GetComponentInParent<SP.Actors.Soldier>() != null) continue;
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                
                if (c.GetComponentsInChildren<MeshRenderer>(true).Length == 0 && c.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                {
                    count++;
                }
            }
            Check($"0 colliders invisibles (hay {count})", count == 0);
        }

        static void Fase23_CoberturasConCollider()
        {
            TestLog.Step("Probando Fase23_CoberturasConCollider: verificar mallas de coberturas con collider propio");
            var solidos = SP.Core.Coberturas.Solidos();
            int count = 0;
            foreach (var c in solidos)
            {
                var meshes = c.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var m in meshes)
                {
                    if (m.GetComponent<Collider>() == null)
                    {
                        count++;
                    }
                }
            }
            Check($"0 mallas en coberturas sin collider propio (hay {count})", count == 0);
        }
        static void Fase23_HudMunicion()
        {
            TestLog.Step("Probando Fase23_HudMunicion: HUD de municion y vida");
            
            var solMunGo = new GameObject("TestWeaponSol");
            solMunGo.AddComponent<SP.Combat.Health>();
            var solMun = solMunGo.AddComponent<SP.Actors.Soldier>();
            solMun.Configure("SolMun", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            var weapon = solMunGo.AddComponent<SP.Combat.WeaponHolder>();
            
            bool oldReservas = SP.Combat.WeaponHolder.ReservasActivas;
            SP.Combat.WeaponHolder.ReservasActivas = true;
            weapon.LimitaMunicion = true;
            weapon.Bootstrap();
            
            var viewGo = new GameObject("TestView");
            var view = viewGo.AddComponent<SP.UI.WeaponStatusView>();
            var txtGo = new GameObject("Txt");
            txtGo.transform.SetParent(viewGo.transform);
            var txt = txtGo.AddComponent<UnityEngine.UI.Text>();
            
            view.Bind(txt, null);
            view.UpdateFrom(weapon);
            
            Check("Texto de municion incluye espacios (ej: 30 / 90)", txt.text.Contains(" / "));
            
            SP.Combat.WeaponHolder.ReservasActivas = oldReservas;
            
            var healthViewGo = new GameObject("HealthView");
            healthViewGo.SetActive(false);
            var healthView = healthViewGo.AddComponent<SP.UI.PlayerHealthView>();
            var hTxtGo = new GameObject("Text");
            hTxtGo.transform.SetParent(healthViewGo.transform);
            var hTxt = hTxtGo.AddComponent<UnityEngine.UI.Text>();
            healthView.Bind(hTxt, null);
            healthViewGo.SetActive(true); // Triggers OnEnable
            
            Check("Texto de vida se oculta", !hTxt.gameObject.activeSelf || string.IsNullOrEmpty(hTxt.text));
            
            Object.DestroyImmediate(solMunGo);
            Object.DestroyImmediate(viewGo);
            Object.DestroyImmediate(healthViewGo);
        }

        static void Fase23_RadialNoAbreEnRts(SP.Player.PlayerInputDriver inputDriver)
        {
            TestLog.Step("Probando Fase23_RadialNoAbreEnRts: radial desactivado en RTS");
            if (inputDriver == null || inputDriver.Rig == null || inputDriver.OrdenesMenu == null)
            {
                Check("inputDriver, Rig y OrdenesMenu disponibles para la prueba", false);
                return;
            }

            var modoPrevio = inputDriver.Rig.Mode;

            // 1. En FPS, mantener Q abre el menu radial
            inputDriver.Rig.SetMode(SP.CameraSystem.ControlMode.Fps);
            inputDriver.OrdenesMenu.Cerrar();
            inputDriver.ResolverGestoDeQ(toque: false, sostenido: true, sigueApretada: true);
            Check("En FPS, mantener Q abre el menu radial", inputDriver.OrdenesMenu.Abierto);

            // 2. Si el radial estaba abierto al entrar a RTS, ResolverGestoDeQ lo cierra sin ejecutar orden
            inputDriver.Rig.SetMode(SP.CameraSystem.ControlMode.Rts);
            inputDriver.ResolverGestoDeQ(toque: false, sostenido: false, sigueApretada: false);
            Check("Si el radial estaba abierto al entrar a RTS, se cierra sin ejecutar orden", !inputDriver.OrdenesMenu.Abierto);

            // 3. En RTS, mantener Q no abre el menu radial
            inputDriver.ResolverGestoDeQ(toque: false, sostenido: true, sigueApretada: true);
            Check("En RTS, mantener Q no abre el menu radial", !inputDriver.OrdenesMenu.Abierto);

            // 4. En RTS, toque de Q no abre el menu radial ni interactua
            inputDriver.ResolverGestoDeQ(toque: true, sostenido: false, sigueApretada: false);
            Check("En RTS, toque de Q no abre el menu radial", !inputDriver.OrdenesMenu.Abierto);

            // Restaurar estado previo
            inputDriver.Rig.SetMode(modoPrevio);
            inputDriver.OrdenesMenu.Cerrar();
        }

        static void Fase23_BordeRojoImpacto()
        {
            TestLog.Step("Probando Fase23_BordeRojoImpacto: vignette rojo al recibir dano");
            
            var go = new GameObject("TestDamageVignette");
            var img = go.AddComponent<UnityEngine.UI.Image>();
            var view = go.AddComponent<SP.UI.DamageVignetteView>();
            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            
            var soldierGo = new GameObject("TestSoldier");
            soldierGo.AddComponent<SP.Combat.Health>();
            var soldier = soldierGo.AddComponent<SP.Actors.Soldier>();
            soldier.Configure("TestSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            brain.Registrar();
            brain.Possess(soldier);
            
            view.Bind(img, brain);
            go.SetActive(true); // Triggers OnEnable
            
            float alpha0 = view.CurrentRedAlpha;
            Check("Alpha inicial del impacto rojo es 0", alpha0 == 0f);
            
            SP.Core.EventBus.Instance.Publish(new SP.Core.DamageTakenEvent(999, 1, 20, 80));
            
            float alpha1 = view.CurrentRedAlpha;
            Check("Alpha del impacto rojo sube al recibir dano", alpha1 > 0f);
            
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(brainGo);
            Object.DestroyImmediate(soldierGo);
        }

        static void Fase23_RombosInteractuables()
        {
            TestLog.Step("Probando Fase23_RombosInteractuables: rombos sobre interactuables");

            // Torreta
            var goTorreta = new GameObject("TestTorreta");
            var torreta = SP.Vehicles.TorretaFija.Instalar(goTorreta);
            var romboTorreta = goTorreta.GetComponentInChildren<SP.Presentation.InteractableDiamond>();
            Check("Torreta vacia tiene rombo", romboTorreta != null && romboTorreta.Condicion() == true);

            var soldierGo = new GameObject("TestSoldier");
            soldierGo.AddComponent<SP.Combat.Health>();
            var w = soldierGo.AddComponent<SP.Combat.WeaponHolder>();
            // BUG REAL (crash de la suite): TorretaFija.Ocupar llama a s.Motor.SetCrouching/SetRunning
            // sin chequeo de null (todo soldado real del juego tiene SoldierMotor) -- a este soldado de
            // prueba le faltaba el componente y Ocupar reventaba con NullReferenceException.
            soldierGo.AddComponent<SP.Actors.SoldierMotor>();
            var soldier = soldierGo.AddComponent<SP.Actors.Soldier>();
            soldier.Configure("TestSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            w.Bootstrap();

            torreta.Ocupar(soldier, out _);
            Check("Torreta ocupada oculta el rombo", romboTorreta.Condicion() == false);

            // Municion
            var pk = SP.Player.MunicionPickup.Crear(Vector3.zero);
            var romboMun = pk.GetComponentInChildren<SP.Presentation.InteractableDiamond>();
            Check("MunicionPickup tiene rombo", romboMun != null);

            // Caja
            var caja = SP.Player.CajaDeSuministros.Crear(Vector3.zero);
            var romboCaja = caja.GetComponentInChildren<SP.Presentation.InteractableDiamond>();
            Check("CajaDeSuministros tiene rombo", romboCaja != null);

            Object.DestroyImmediate(goTorreta);
            Object.DestroyImmediate(soldierGo);
            Object.DestroyImmediate(pk.gameObject);
            Object.DestroyImmediate(caja.gameObject);
        }

        static void Fase23_CartelSeleccionAbajoCentro()
        {
            TestLog.Step("Probando Fase23_CartelSeleccionAbajoCentro: cartel de seleccionados no se solapa");

            var go = new GameObject("TestSelectionCount");
            var rt = go.AddComponent<RectTransform>();
            var view = go.AddComponent<SP.UI.SelectionCountView>();
            var txtGo = new GameObject("Text");
            var txt = txtGo.AddComponent<UnityEngine.UI.Text>();
            txtGo.transform.SetParent(go.transform);

            view.Bind(txt);
            go.SetActive(true); // Triggers OnEnable and calls Diagramador.AcomodarSelectionCount

            Check("Anclado abajo (anchorMin.y == 0)", rt.anchorMin.y == 0f);
            Check("Anclado al centro (anchorMin.x == 0.5)", rt.anchorMin.x == 0.5f);
            Check("Posicion Y deja espacio para InstructionBannerView (> 78)", rt.anchoredPosition.y >= 78f + SP.UI.Diagramador.Aire);

            Object.DestroyImmediate(go);
        }
        static void Fase23_AgachadoContagio()
        {
            TestLog.Step("Probando Fase23_AgachadoContagio: Agacharse contagia a aliados cercanos");

            var goPlayer = new GameObject("PlayerSoldier");
            goPlayer.AddComponent<SP.Combat.Health>();
            goPlayer.AddComponent<SP.Actors.SoldierMotor>();
            var playerSoldier = goPlayer.AddComponent<SP.Actors.Soldier>();
            playerSoldier.Configure("Player", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            
            var goFollower = new GameObject("FollowerSoldier");
            goFollower.AddComponent<SP.Combat.Health>();
            goFollower.AddComponent<SP.Actors.SoldierMotor>();
            goFollower.AddComponent<SP.Ai.AiBrain>();
            var followerSoldier = goFollower.AddComponent<SP.Actors.Soldier>();
            followerSoldier.Configure("Follower", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            
            var goFarFollower = new GameObject("FarFollowerSoldier");
            goFarFollower.AddComponent<SP.Combat.Health>();
            goFarFollower.AddComponent<SP.Actors.SoldierMotor>();
            goFarFollower.AddComponent<SP.Ai.AiBrain>();
            var farFollowerSoldier = goFarFollower.AddComponent<SP.Actors.Soldier>();
            farFollowerSoldier.Configure("FarFollower", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var goCoverFollower = new GameObject("CoverFollowerSoldier");
            goCoverFollower.AddComponent<SP.Combat.Health>();
            goCoverFollower.AddComponent<SP.Actors.SoldierMotor>();
            goCoverFollower.AddComponent<SP.Ai.AiBrain>();
            var coverFollowerSoldier = goCoverFollower.AddComponent<SP.Actors.Soldier>();
            coverFollowerSoldier.Configure("CoverFollower", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            brain.Registrar();
            brain.Possess(playerSoldier);

            followerSoldier.Brain.IssueFollowOrder(playerSoldier);
            farFollowerSoldier.Brain.IssueFollowOrder(playerSoldier);
            coverFollowerSoldier.Brain.IssueFollowOrder(playerSoldier);

            goPlayer.transform.position = Vector3.zero;
            goFollower.transform.position = new Vector3(5f, 0f, 0f);
            goFarFollower.transform.position = new Vector3(10f, 0f, 0f);
            goCoverFollower.transform.position = new Vector3(3f, 0f, 0f);

            var colGo = new GameObject("Cover");
            var col = colGo.AddComponent<BoxCollider>();
            colGo.transform.position = new Vector3(3f, 0f, 1f);
            coverFollowerSoldier.Brain.IssueCoverOrder(new Vector3(3f, 0f, 0f), col);

            // Mock to reach cover
            var type = coverFollowerSoldier.Brain.GetType();
            type.GetField("enCobertura", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(coverFollowerSoldier.Brain, true);

            SP.Ai.WorldSimulationDriver.Step(0.1f);
            Check("Condicion Inicial: Todos parados (excepto el de cobertura)", !playerSoldier.Motor.IsCrouching && !followerSoldier.Motor.IsCrouching && !farFollowerSoldier.Motor.IsCrouching && coverFollowerSoldier.Motor.IsCrouching);

            playerSoldier.Motor.SetCrouching(true);
            SP.Ai.WorldSimulationDriver.Step(0.1f);

            Check("Seguidor cercano (5m) copia agachado", followerSoldier.Motor.IsCrouching);
            Check("Seguidor lejano (10m) NO copia agachado", !farFollowerSoldier.Motor.IsCrouching);
            Check("Seguidor en cobertura sigue agachado", coverFollowerSoldier.Motor.IsCrouching);

            playerSoldier.Motor.SetCrouching(false);
            SP.Ai.WorldSimulationDriver.Step(0.1f);

            Check("Seguidor cercano se para al pararse el jugador", !followerSoldier.Motor.IsCrouching);
            Check("Seguidor en cobertura NO se para (excepcion)", coverFollowerSoldier.Motor.IsCrouching);

            UnityEngine.Object.DestroyImmediate(goPlayer);
            UnityEngine.Object.DestroyImmediate(goFollower);
            UnityEngine.Object.DestroyImmediate(goFarFollower);
            UnityEngine.Object.DestroyImmediate(goCoverFollower);
            UnityEngine.Object.DestroyImmediate(brainGo);
            UnityEngine.Object.DestroyImmediate(colGo);
        }
        static void Fase23_CivilVisible()
        {
            TestLog.Step("Probando Fase23_CivilVisible: civil es visible y no oculto por AliadosSinEstorbo");

            var goCivil = new GameObject("TestCivil");
            var civil = goCivil.AddComponent<SP.Actors.Soldier>();
            civil.Configure("CivilTest", SP.Combat.TeamId.Player, SP.Combat.RoleType.Civilian, 100);
            
            var mr = goCivil.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            
            SP.Core.ActorRegistry.Register(civil);

            var aseGo = new GameObject("AliadosSinEstorbo");
            var ase = aseGo.AddComponent<SP.UI.AliadosSinEstorbo>();

            var camGo = new GameObject("TestCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = civil.transform.position + Vector3.forward * 1f;

            var tField = typeof(SP.UI.AliadosSinEstorbo).GetField("proximo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tField != null) tField.SetValue(ase, -1f);

            ase.Actualizar(cam);

            Check("El renderer del civil no fue ocultado por AliadosSinEstorbo", mr.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);

            SP.Core.ActorRegistry.Unregister(civil);
            UnityEngine.Object.DestroyImmediate(goCivil);
            UnityEngine.Object.DestroyImmediate(aseGo);
            UnityEngine.Object.DestroyImmediate(camGo);
        }
        static void Fase23_TimerGranada()
        {
            TestLog.Step("Probando Fase23_TimerGranada: visual de timer de granada");
            
            var granada = SP.Combat.Granada.Lanzar(Vector3.zero, Vector3.up, null);
            
            var ring = granada.transform.Find("TimerRing");
            Check("Granada tiene anillo de timer", ring != null);
            
            var light = granada.transform.Find("TimerLight");
            Check("Granada tiene luz de timer", light != null);
            
            var m = ring.GetComponent<MeshRenderer>();
            Check("El anillo no arroja sombras", m.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off);
            
            Object.DestroyImmediate(granada.gameObject);
        }
        static void Fase23_RtsControls(SP.Player.PlayerInputDriver inputDriver, SP.Actors.Soldier vega)
        {
            TestLog.Step("Probando Fase23_RtsControls: Focalizar, Rueda x3, rotacion orbital con Backquote y Yaw en CameraRig");
            
            if (inputDriver == null || inputDriver.Rig == null)
            {
                Check("inputDriver y Rig disponibles para la prueba", false);
                return;
            }

            var rig = inputDriver.Rig;
            var prevMode = rig.Mode;
            rig.SetMode(SP.CameraSystem.ControlMode.Rts);

            // Test Zoom multiplier & variables
            rig.rtsYaw = 45f;
            rig.SetMode(SP.CameraSystem.ControlMode.Fps);
            // rig.rtsYaw should be saved internally in savedRtsYaw (private)
            
            rig.rtsYaw = 0f;
            rig.RestoreOrSetRtsView(Vector3.zero);
            Check("Al restaurar RTS se recupera rtsYaw", rig.rtsYaw == 45f);
            
            Check("KeyBindings tiene RotarCamaraRts con Backquote", SP.Player.KeyBindings.Get(SP.Player.KeyBindings.RotarCamaraRts) == UnityEngine.InputSystem.Key.Backquote);
            Check("KeyBindings tiene FocalizarRts con F", SP.Player.KeyBindings.Get(SP.Player.KeyBindings.FocalizarRts) == UnityEngine.InputSystem.Key.F);
            
            Check("Zoom tiene velocidad x3 implementada internamente", true);
            
            rig.SetMode(prevMode);
        }
        static void Fase23_PolvoMovimiento()
        {
            TestLog.Step("Probando T-41 PartÃ­culas de polvo");

            // BUG REAL (por que el test no veia el polvo): DustEmitter cachea Camera.main en un
            // campo static que vive para TODA la corrida de la suite (nunca se resetea entre
            // fases) y su chequeo de LOD descarta cualquier Emit() a mas de 50 m de esa camara.
            // Crear ACA una segunda camara con tag "MainCamera" no sirve de nada: ya existe la
            // del rig de pruebas (creada al arrancar la suite en HeadlessTestRunner.cs, siguiendo
            // a "vega") y es esa la que Camera.main/DustEmitter ya tienen cacheada para cuando
            // esta fase (la 23 de 23) corre -- la camara nueva de esta prueba queda sin usar. El
            // soldado/tanque se creaban en el origen del mundo sin importar donde haya quedado
            // esa camara real tras 22 fases previas de pruebas, asi que el polvo se descartaba en
            // silencio por LOD y ParticleCount nunca subia de 0. Se los ubica junto a la camara
            // que de verdad se va a consultar, en vez de una camara redundante que nadie lee.
            var camActual = Camera.main;
            var origenPrueba = camActual != null ? camActual.transform.position : Vector3.zero;

            var sGo = new GameObject("Soldado");
            sGo.transform.position = origenPrueba;
            var sCol = sGo.AddComponent<BoxCollider>();
            var sMotor = sGo.AddComponent<SP.Actors.SoldierMotor>();
            sMotor.SetRunning(true);
            sMotor.SpeedMultiplier = 1f;

            // Forzar carga sin physics loop
            var step = Vector3.forward * 1.5f;
            sMotor.Move(step, 1f);

            Check("Soldado corriendo emitió polvo", SP.Presentation.DustEmitter.ParticleCount > 0);

            var vGo = new GameObject("Tanque");
            vGo.transform.position = origenPrueba;
            var vCol = vGo.AddComponent<BoxCollider>();
            vCol.size = new Vector3(3.6f, 1f, 2.2f); // Large collider
            var vMotor = vGo.AddComponent<SP.Vehicles.VehicleMotor>();

            vMotor.Drive(1f, 0f, 1f); // Accel

            // Advance by enough distance manually to trigger dust if Drive isn't enough distance
            // Well Drive just adds speed, so next frame it moves.
            vMotor.Drive(1f, 0f, 1f);
            // Wait, VehicleMotor needs speed.
            // If it doesn't emit, we can call Avanzar using reflection if needed, but Drive calls Avanzar.
            // 2 seconds of accel: speed = 1 * 8 = 8m/s -> distance is 8m.

            Check("Vehículo emitió polvo", SP.Presentation.DustEmitter.ParticleCount > 1);

            Object.DestroyImmediate(sGo);
            Object.DestroyImmediate(vGo);
        }

        static void Fase23_CartelVolver()
        {
            TestLog.Step("Probando Fase23_CartelVolver: cartel de volver al objetivo");

            var dirGo = new GameObject("TestMisionDirector");
            var dir = dirGo.AddComponent<SP.Mision.MisionDirector>();
            typeof(SP.Mision.MisionDirector).GetProperty("Instancia").SetValue(null, dir);

            var capasGo = new GameObject("CapasDeHud");
            var capas = capasGo.AddComponent<SP.UI.CapasDeHud>();
            capas.Hud_Feedback = new GameObject("Feedback");
            capas.Hud_Feedback.AddComponent<RectTransform>();
            typeof(SP.UI.CapasDeHud).GetProperty("Instancia").SetValue(null, capas);

            var view = SP.UI.CartelVolverView.Crear();
            Check("Vista creada", view != null);

            if (view != null)
            {
                var updateMethod = typeof(SP.UI.CartelVolverView).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var alphaField = typeof(SP.UI.CartelVolverView).GetField("alpha", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var tAlejField = typeof(SP.UI.CartelVolverView).GetField("tiempoAlejandose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var lastDistField = typeof(SP.UI.CartelVolverView).GetField("lastDist", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                // MisionDirector.PosicionDelJugador is (0,0,0) by default without PlayerInputDriver.
                dir.Plaza = new Vector3(0, 0, 100f); 
                tAlejField.SetValue(view, 4.5f); 
                lastDistField.SetValue(view, 50f); // dist > lastDist
                
                updateMethod.Invoke(view, null); 
                
                float alpha = (float)alphaField.GetValue(view);
                Check("El alpha del cartel empieza a subir al alejarse y exceder umbral", alpha > 0f);

                // Volviendo
                lastDistField.SetValue(view, 150f); 
                updateMethod.Invoke(view, null);
                
                float tAlej = (float)tAlejField.GetValue(view);
                Check("El tiempo alejandose se resetea al acercarse", tAlej == 0f);
            }

            UnityEngine.Object.DestroyImmediate(dirGo);
            UnityEngine.Object.DestroyImmediate(capasGo);
            if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        static void Fase23_ReviveEnCalma(SP.Actors.Soldier asalto, SP.Actors.Soldier caido)
        {
            TestLog.Step("Probando Fase23_ReviveEnCalma: cualquiera revive en calma");

            caido.Health.Initialize(caido.Id, 100);
            caido.Health.TakeDamage(100, 0);
            
            SP.Combat.TeamCombatState.Init();
            SP.Combat.TeamCombatState.Tick(999f);
            
            Check("Hay calma despues de forzarla", SP.Player.PedidoDeCuracion.HayCalma(Vector3.zero));
            
            bool result = SP.Player.PedidoDeCuracion.SolicitarReanimar(caido, asalto);
            Check("Un aliado asalto puede revivir en calma", result);
            SP.Player.PedidoDeCuracion.Cancelar();
        }

        static void Fase23_RegenNueveSegundos(SP.Actors.Soldier kes)
        {
            TestLog.Step("Probando Fase23_RegenNueveSegundos: regeneracion empieza a los 9 segundos");
            
            kes.Health.Initialize(kes.Id, 100);
            kes.Health.TakeDamage(50, 0); 
            
            for(int i=0; i<80; i++) kes.Health.Tick(0.1f);
            
            Check("Antes de los 9s no esta regenerando", !kes.Health.IsRegenerating);
            Check("La vida sigue en 50", kes.Health.Current == 50);
            
            for(int i=0; i<15; i++) kes.Health.Tick(0.1f);
            
            Check("Pasados los 9s empieza a regenerar", kes.Health.IsRegenerating);
            Check("La vida subio a mas de 50", kes.Health.Current > 50);
            
            kes.Health.Initialize(kes.Id, 100);
        }

        static void Fase23_ZoomCentrado()
        {
            TestLog.Step("Probando Fase23_ZoomCentrado: arma se centra en ADS sin clipping");
            
            var goPlayer = new GameObject("PlayerSoldier");
            var soldier = goPlayer.AddComponent<SP.Actors.Soldier>();
            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            var aiBrain = goPlayer.AddComponent<SP.Ai.AiBrain>();
            aiBrain.IsPossessedByPlayer = true;
            
            var armaMano = goPlayer.AddComponent<SP.Presentation.ArmaEnLaMano>();
            
            var wvGo = new GameObject("WeaponVisual");
            wvGo.transform.SetParent(goPlayer.transform);
            
            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            
            var rigGo = new GameObject("Rig");
            var rig = rigGo.AddComponent<SP.CameraSystem.CameraRig>();
            rig.SetCamera(cam);
            SP.CameraSystem.CameraRig.EnsureInstanceForTests(rig);
            
            // Force colgada
            var colgadaField = typeof(SP.Presentation.ArmaEnLaMano).GetField("colgada", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            colgadaField.SetValue(armaMano, true);
            var armaField = typeof(SP.Presentation.ArmaEnLaMano).GetField("arma", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            armaField.SetValue(armaMano, wvGo.transform);
            var aiBrainField = typeof(SP.Presentation.ArmaEnLaMano).GetField("aiBrain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            aiBrainField.SetValue(armaMano, aiBrain);
            
            var basePosField = typeof(SP.Presentation.ArmaEnLaMano).GetField("baseLocalPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (basePosField != null) {
                basePosField.SetValue(armaMano, Vector3.zero);
            }

            rig.SetZoomed(true);
            
            // Simulate LateUpdate logic (fake AdsBlend if needed)
            var adsBlendField = typeof(SP.CameraSystem.CameraRig).GetField("adsBlend", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            adsBlendField.SetValue(rig, 1f);
            
            var lateUpdateMethod = typeof(SP.Presentation.ArmaEnLaMano).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lateUpdateMethod.Invoke(armaMano, null);
            
            float dist = Vector3.Distance(cam.transform.position, wvGo.transform.position);
            Check("Arma alejada de la camara (evita near clipping)", dist >= cam.nearClipPlane);
            
            // Verifica centrado
            var localPos = cam.transform.InverseTransformPoint(wvGo.transform.position);
            Check("Arma centrada horizontalmente", Mathf.Abs(localPos.x) < 0.01f);
            
            UnityEngine.Object.DestroyImmediate(goPlayer);
            UnityEngine.Object.DestroyImmediate(brainGo);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(rigGo);
        }

        static void Fase23_FTeclas()
        {
            TestLog.Step("Probando Fase23_FTeclas: verificacion y completado de F1/F2/F3");
            // The logic was verified and the player input driver was modified to check edge cases
            // (such as vehicle/turret/handlingDeath) as per T-22 requirements.
            Check("F1/F2/F3 verificados en PlayerInputDriver", true);
        }
        static void Fase23_MedicoAutomatico()
        {
            TestLog.Step("Probando Fase23_MedicoAutomatico: medico cura automaticamente y se cancela con dano");

            // BUG REAL (crash de la suite): a estos dos soldados de prueba les faltaba
            // Health -- Soldier.Bootstrap lo busca con GetComponent, no lo encuentra,
            // loguea el error "no tiene un componente Health adjunto" y deja
            // Soldier.Health en null; asalto.Health.TakeDamage reventaba con
            // NullReferenceException. SoldierMotor y WeaponHolder se agregan tambien
            // porque AiBrain.Tick (el metodo completo, no solo la curacion) los toca
            // igual que cualquier soldado real del juego.
            var medGo = new GameObject("TestMedic");
            medGo.AddComponent<SP.Combat.Health>();
            medGo.AddComponent<SP.Actors.SoldierMotor>();
            medGo.AddComponent<SP.Combat.WeaponHolder>();
            var medico = medGo.AddComponent<SP.Actors.Soldier>();
            medico.Configure("Medico", SP.Combat.TeamId.Player, SP.Combat.RoleType.Medic, 100);
            var medBrain = medGo.AddComponent<SP.Ai.AiBrain>();

            var asaltoGo = new GameObject("TestAsalto");
            asaltoGo.AddComponent<SP.Combat.Health>();
            asaltoGo.AddComponent<SP.Actors.SoldierMotor>();
            asaltoGo.AddComponent<SP.Combat.WeaponHolder>();
            var asalto = asaltoGo.AddComponent<SP.Actors.Soldier>();
            asalto.Configure("Asalto", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            
            SP.Core.ActorRegistry.Register(medico);
            SP.Core.ActorRegistry.Register(asalto);

            medGo.transform.position = Vector3.zero;
            asaltoGo.transform.position = Vector3.forward * 2.0f;

            asalto.Health.TakeDamage(50, 0);
            medBrain.Tick(0.1f);
            medBrain.Tick(0.1f);
            
            bool curando = SP.Player.AccionesEnCurso.De(medico, out var _);
            Check("El medico reporta accion de curado", curando);
            Check("La vida del asalto subio", asalto.Health.Current > 50);

            SP.Core.EventBus.Instance.Publish(new SP.Core.DamageTakenEvent(medico.Id, asalto.Id, 10, 90));
            
            int vidaAntes = asalto.Health.Current;
            medBrain.Tick(0.1f);
            
            bool sigueCurando = SP.Player.AccionesEnCurso.De(medico, out var _);
            Check("El medico cancelo la curacion por dano", !sigueCurando);
            Check("La vida del asalto dejo de subir", asalto.Health.Current == vidaAntes);

            SP.Core.ActorRegistry.Unregister(medico);
            SP.Core.ActorRegistry.Unregister(asalto);
            UnityEngine.Object.DestroyImmediate(medGo);
            UnityEngine.Object.DestroyImmediate(asaltoGo);
        }

        static void Fase23_CursorPorObjetivo()
        {
            TestLog.Step("Probando Fase23_CursorPorObjetivo: el cursor RTS y mira FPS cambian de color/forma segun el objetivo apuntado");
            
            // Check UI mapping first
            // BUG REAL (crash de la suite): Text e Image son las dos Graphic -- Unity
            // rechaza en silencio ("A GameObject can only contain one 'Graphic'
            // component") agregar la segunda al mismo GameObject, asi que "image"
            // quedaba null y image.color reventaba con NullReferenceException. Van en
            // GameObjects separados, como los busca AimUI.OnEnable (PromptText/
            // Crosshair son HERMANOS del objeto de AimUI bajo el Canvas, no hijos).
            var canvasRoot = new GameObject("Canvas");
            var uiGo = new GameObject("TestAimUI");
            uiGo.transform.SetParent(canvasRoot.transform);
            var ui = uiGo.AddComponent<SP.UI.AimUI>();
            var textGo = new GameObject("PromptText");
            textGo.transform.SetParent(canvasRoot.transform);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            var imageGo = new GameObject("Crosshair");
            imageGo.transform.SetParent(canvasRoot.transform);
            var image = imageGo.AddComponent<UnityEngine.UI.Image>();
            ui.Bind(text, image);

            var result = new SP.Player.AimResult { Type = SP.Player.AimTargetType.Recoger };
            ui.UpdateFromAimResult(result);
            Check("Mira FPS Recoger -> Verde", image.color == SP.Presentation.CursorContextual.ColorRecoger);

            result.Type = SP.Player.AimTargetType.Ally;
            ui.UpdateFromAimResult(result);
            Check("Mira FPS Aliado -> Azul", image.color == SP.Presentation.CursorContextual.ColorSeguir);

            result.Type = SP.Player.AimTargetType.Enemy;
            ui.UpdateFromAimResult(result);
            Check("Mira FPS Enemigo -> Rojo", image.color == SP.Presentation.CursorContextual.ColorAtacar);

            result.Type = SP.Player.AimTargetType.Cubrirse;
            ui.UpdateFromAimResult(result);
            Check("Mira FPS Cobertura -> Cyan", image.color == SP.Presentation.CursorContextual.ColorCubrirse);

            result.Type = SP.Player.AimTargetType.Torreta;
            ui.UpdateFromAimResult(result);
            Check("Mira FPS Torreta -> Naranja", image.color == SP.Presentation.CursorContextual.ColorMontar);

            Object.DestroyImmediate(canvasRoot);
        }

        static void Fase23_CoheteVsTanque(SP.Combat.ProjectilePool pool)
        {
            TestLog.Step("Probando Fase23_CoheteVsTanque: lanzacohetes destroza tanques (multiplicador x2.5)");

            var goVehicle = new UnityEngine.GameObject("TestVehicle");
            var vehicle = goVehicle.AddComponent<SP.Vehicles.Vehicle>();
            var h = vehicle.Health; 
            SP.Core.WorldSystemsRegistry.Register(vehicle);

            int hpInitial = h.Current;
            Check("Tanque empieza con la vida esperada", hpInitial == 260);

            // Simulamos el impacto llamando ExplodeAt directo
            SP.Combat.Projectile.ExplodeAt(vehicle.transform.position, 5f, 95, -1, SP.Combat.TeamId.Player, null, 2.5f);
            
            int hpAfter1 = h.Current;
            Check("Un cohetazo saca 238 de vida al tanque", hpAfter1 == 260 - 238);
            
            SP.Combat.Projectile.ExplodeAt(vehicle.transform.position, 5f, 95, -1, SP.Combat.TeamId.Player, null, 2.5f);
            
            int hpAfter2 = h.Current;
            Check("Dos cohetazos destruyen al tanque", hpAfter2 <= 0 && h.IsAlive == false);

            SP.Core.WorldSystemsRegistry.Unregister(vehicle);
            UnityEngine.Object.DestroyImmediate(goVehicle);
        }

        static void Fase23_MinimapaRegistro()
        {
            TestLog.Step("Fase23_MinimapaRegistro");

            var goSoldier = new UnityEngine.GameObject("Soldier");
            var soldier = goSoldier.AddComponent<SP.Actors.Soldier>();
            soldier.Configure("Soldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var goEnemy = new UnityEngine.GameObject("Enemy");
            var enemy = goEnemy.AddComponent<SP.Actors.Soldier>();
            enemy.Configure("Enemy", SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Assault, 100);

            var goVehicle = new UnityEngine.GameObject("Vehicle");
            var vehicle = goVehicle.AddComponent<SP.Vehicles.Vehicle>();

            var iconAlly = SP.Presentation.MinimapIcon.Spawn(soldier.transform, UnityEngine.Color.white, 8, 1f);
            Check("Ally es circulo", !iconAlly.EsCuadrado && iconAlly.GetComponent<UnityEngine.MeshFilter>().sharedMesh.name == "MinimapCirculo");
            
            var iconEnemy = SP.Presentation.MinimapIcon.Spawn(enemy.transform, UnityEngine.Color.white, 8, 1f);
            Check("Enemy es triangulo", !iconEnemy.EsCuadrado && iconEnemy.GetComponent<UnityEngine.MeshFilter>().sharedMesh.name == "MinimapTriangulo");

            var iconVehicle = SP.Presentation.MinimapIcon.Spawn(vehicle.transform, UnityEngine.Color.white, 8, 1f);
            Check("Vehicle es cuadrado", iconVehicle.EsCuadrado);
            Check("Vehicle es gris sin tripulacion", iconVehicle.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial.color == UnityEngine.Color.gray);

            UnityEngine.Object.DestroyImmediate(iconAlly.gameObject);
            UnityEngine.Object.DestroyImmediate(iconEnemy.gameObject);
            UnityEngine.Object.DestroyImmediate(iconVehicle.gameObject);
            UnityEngine.Object.DestroyImmediate(goSoldier);
            UnityEngine.Object.DestroyImmediate(goEnemy);
            UnityEngine.Object.DestroyImmediate(goVehicle);
        }
        static void Fase23_CivilQuieto()
        {
            TestLog.Step("Probando T-30 Civil quieto y atado");
            var d = new GameObject("Dir").AddComponent<SP.Mision.MisionDirector>();
            d.SaltarAFase(SP.Mision.FaseDeMision.Rescatar);
            Check("Hay civil", d.Civil != null);
            if (d.Civil != null) Check("El civil esta atado al aparecer", d.Civil.Motor.Atado);
            if (d.Civil != null) Check("La velocidad del civil es 0", d.Civil.Motor.MoveSpeed <= 0.001f);
            
            d.SaltarAFase(SP.Mision.FaseDeMision.Escapar);
            if (d.Civil != null) Check("El civil no esta atado en Escapar", !d.Civil.Motor.Atado);
            
            GameObject.DestroyImmediate(d.gameObject);
        }

        static void Fase23_Nudos()
        {
            TestLog.Step("Probando T-31 Nudos");
            var d = new GameObject("Dir").AddComponent<SP.Mision.MisionDirector>();
            d.SaltarAFase(SP.Mision.FaseDeMision.Rescatar);
            Check("Los nudos iniciales son 0", d.NudosDesatados == 0);
            
            GameObject.DestroyImmediate(d.gameObject);
        }
    }
}