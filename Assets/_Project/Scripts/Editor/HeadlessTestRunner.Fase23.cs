using System.Collections;
using UnityEngine;

namespace SP.EditorTools
{
    public static partial class HeadlessTestRunner
    {
        public static void RunPhase23(SP.Player.PlayerInputDriver inputDriver, SP.Vehicles.Vehicle vehicle, SP.Actors.Soldier vega, SP.Actors.Soldier kes, SP.Actors.Soldier doc, UnityEngine.GameObject soldierPrefab, UnityEngine.Color colorEnemy, SP.Combat.ProjectilePool pool)
        {
            TestLog.Phase("FASE 23: RONDA 14 (BASELINE)");
            Fase23_PiesEnElPiso();
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
            Fase23_RadialMuerto(inputDriver);
            Fase23_RosterClics(inputDriver, doc);
            Fase23_CinematicaSaltoRapido();
            Fase23_RomboTanque();
            Fase23_RomboSinOclusion();
            Fase23_AgachadoContagio();
            Fase23_FormacionLateral(vega, kes, doc);
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
            
            kes.Brain.SetStateForTests(SP.Ai.AiState.Idle);
            doc.Brain.SetStateForTests(SP.Ai.AiState.Idle);

            kes.Brain.Step(0.1f);
            doc.Brain.Step(0.1f);
            
            for (int i = 0; i < 200; i++)
            {
                vega.transform.position += Vector3.forward * (5f * 0.1f); // 5m/s
                kes.Brain.Step(0.1f);
                doc.Brain.Step(0.1f);
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
            enemyHealth.MaxHealth = 100; enemyHealth.SetInitialAmount(100);
            enemy.Configure("Enemy", SP.Actors.TeamId.Enemy, SP.Actors.RoleType.Assault, 100);
            var locator = enemyGo.AddComponent<SP.Presentation.UnitLocatorCylinder>();
            SP.Core.ActorRegistry.Register(enemy);

            var playerGo = new GameObject("TestPlayer");
            var player = playerGo.AddComponent<SP.Actors.Soldier>();
            var playerHealth = playerGo.AddComponent<SP.Combat.Health>();
            playerHealth.MaxHealth = 100; playerHealth.SetInitialAmount(100);
            player.Configure("Player", SP.Actors.TeamId.Allied, SP.Actors.RoleType.Assault, 100);
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
            
            var mr = vehGo.transform.Find("RomboTanque");
            Material mat = null;
            if (mr != null) mat = mr.GetComponent<MeshRenderer>().sharedMaterial;

            Check("RomboTanque existe", mat != null);
            if (mat != null)
            {
                Check("Tanque vacio -> gris", mat.color == SP.Presentation.DiamondGizmo.ColorVacio);
                
                var sol1Go = new GameObject("S1"); var s1 = sol1Go.AddComponent<SP.Actors.Soldier>();
                s1.Id = 991;
                var h1 = sol1Go.AddComponent<SP.Actors.Health>(); h1.MaxHealth = 10; h1.SetInitialAmount(10); s1.Health = h1;
                s1.Team = SP.Combat.TeamId.Player;
                
                veh.Mount(s1, SP.Vehicles.VehicleSeatRole.Driver, true);
                Check("Tanque aliado -> azul", mat.color == SP.Presentation.DiamondGizmo.ColorAliado);
                
                var sol2Go = new GameObject("S2"); var s2 = sol2Go.AddComponent<SP.Actors.Soldier>();
                s2.Id = 992;
                var h2 = sol2Go.AddComponent<SP.Actors.Health>(); h2.MaxHealth = 10; h2.SetInitialAmount(10); s2.Health = h2;
                s2.Team = SP.Combat.TeamId.Enemy;
                
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
            TestLog.Step("Probando Fase23_AnilloAtacante: el daño al jugador dispara un anillo en el minimapa");
            
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
            SP.Core.EventBus.Publish(new SP.Core.DamageTakenEvent(player.Id, attacker.Id, 10, 90));
            
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
            Check("Sonido fue CoverTake", SP.Presentation.Feedback.UltimoSonido == SP.Core.SfxKind.CoverTake);
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
            
            var weaponGo = new GameObject("TestWeapon");
            var weapon = weaponGo.AddComponent<SP.Combat.WeaponHolder>();
            
            bool oldReservas = SP.Combat.WeaponHolder.ReservasActivas;
            SP.Combat.WeaponHolder.ReservasActivas = true;
            weapon.LimitaMunicion = true;
            weapon.Bootstrap(new SP.Actors.ActorInfo { Id = 1, Team = 1 });
            
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
            
            Object.DestroyImmediate(weaponGo);
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
            var soldier = soldierGo.AddComponent<SP.Actors.Soldier>();
            soldier.Id = 999;
            var health = soldierGo.AddComponent<SP.Actors.Health>();
            health.MaxHealth = 100;
            health.SetInitialAmount(100);
            soldier.Health = health;
            brain.Possess(soldier);
            
            SP.Player.PlayerBrain.Activo = brain;
            
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
            var soldier = soldierGo.AddComponent<SP.Actors.Soldier>();
            var health = soldierGo.AddComponent<SP.Actors.Health>();
            health.MaxHealth = 100; health.SetInitialAmount(100);
            soldier.Health = health;
            var wGo = new GameObject("Weapon");
            wGo.transform.SetParent(soldierGo.transform);
            var w = wGo.AddComponent<SP.Combat.WeaponHolder>();
            w.Bootstrap(new SP.Actors.ActorInfo { Id = 1, Team = 1 });
            soldier.Weapon = w;

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
            var playerSoldier = goPlayer.AddComponent<SP.Actors.Soldier>();
            playerSoldier.Id = 1;
            playerSoldier.Team = SP.Combat.TeamId.Player;
            playerSoldier.Health = goPlayer.AddComponent<SP.Actors.Health>();
            playerSoldier.Health.MaxHealth = 100;
            playerSoldier.Health.SetInitialAmount(100);
            
            var goFollower = new GameObject("FollowerSoldier");
            var followerSoldier = goFollower.AddComponent<SP.Actors.Soldier>();
            followerSoldier.Id = 2;
            followerSoldier.Team = SP.Combat.TeamId.Player;
            followerSoldier.Health = goFollower.AddComponent<SP.Actors.Health>();
            followerSoldier.Health.MaxHealth = 100;
            followerSoldier.Health.SetInitialAmount(100);
            
            var goFarFollower = new GameObject("FarFollowerSoldier");
            var farFollowerSoldier = goFarFollower.AddComponent<SP.Actors.Soldier>();
            farFollowerSoldier.Id = 3;
            farFollowerSoldier.Team = SP.Combat.TeamId.Player;
            farFollowerSoldier.Health = goFarFollower.AddComponent<SP.Actors.Health>();
            farFollowerSoldier.Health.MaxHealth = 100;
            farFollowerSoldier.Health.SetInitialAmount(100);

            var goCoverFollower = new GameObject("CoverFollowerSoldier");
            var coverFollowerSoldier = goCoverFollower.AddComponent<SP.Actors.Soldier>();
            coverFollowerSoldier.Id = 4;
            coverFollowerSoldier.Team = SP.Combat.TeamId.Player;
            coverFollowerSoldier.Health = goCoverFollower.AddComponent<SP.Actors.Health>();
            coverFollowerSoldier.Health.MaxHealth = 100;
            coverFollowerSoldier.Health.SetInitialAmount(100);

            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            brain.Possess(playerSoldier);
            SP.Player.PlayerBrain.Activo = brain;

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
            civil.Configure("CivilTest", SP.Actors.TeamId.Player, SP.Actors.RoleType.Civilian, 100);
            
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
    }
}


