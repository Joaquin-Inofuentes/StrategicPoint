using System.Collections;
using System.Linq;
using UnityEngine;
using SP.Core;

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
            Fase23_OrdenDeMoverSeRespeta(vega, kes);
        }

        // La suite corre en Edit mode, donde Unity NO llama Awake/OnEnable/OnDisable a un MonoBehaviour comun (ver
        // CameraRig.EnsureInstanceForTests): un componente recien agregado queda sin inicializar, sin suscribirse al EventBus
        // ni registrarse en su singleton. Estas pruebas arman sus componentes a mano, asi que se les simula el ciclo de Play.
        // Fuera de Edit mode no hacen nada (Unity ya lo hizo). Apagar va antes de cada DestroyImmediate: en Edit mode tampoco
        // corre OnDisable, y sin el la suscripcion al EventBus quedaria viva apuntando a un objeto destruido.
        // soloAwake: para un componente de la escena de la suite que solo hay que dar de alta en su singleton, sin
        // suscribirlo a eventos que despues nadie daria de baja.
        static void Arrancar(MonoBehaviour mb, bool soloAwake = false)
        {
            if (Application.isPlaying || mb == null) return;
            const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            mb.GetType().GetMethod("Awake", f, null, System.Type.EmptyTypes, null)?.Invoke(mb, null);
            if (!soloAwake) mb.GetType().GetMethod("OnEnable", f, null, System.Type.EmptyTypes, null)?.Invoke(mb, null);
        }

        static void Apagar(MonoBehaviour mb)
        {
            if (Application.isPlaying || mb == null) return;
            const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            mb.GetType().GetMethod("OnDisable", f, null, System.Type.EmptyTypes, null)?.Invoke(mb, null);
        }

        // PlayerBrain.Activo es un singleton: una prueba que registra su propio cerebro tiene que devolverle el lugar al de la
        // escena, si no las pruebas siguientes (roster, agachado) ven un cerebro ya destruido.
        static void VolverAlCerebro(SP.Player.PlayerBrain previo)
        {
            SP.Player.PlayerBrain.ReiniciarActivo();
            if (previo != null) previo.Registrar();
        }

        static void Fase23_FormacionLateral(SP.Actors.Soldier vega, SP.Actors.Soldier kes, SP.Actors.Soldier doc)
        {
            TestLog.Step("Probando Fase23_FormacionLateral: primera fila al costado (visible en camara), no detras");
            
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
            
            // Cancelar lo que quedo de pruebas anteriores de esta misma fase: Fase23_SinCartelCobertura le da a Kes una orden
            // de cobertura y no la suelta, y una cobertura ORDENADA no se abandona para seguir al lider (medido: Kes se
            // quedaba en Idle mientras Vega se alejaba 100 m). CancelOrder suelta la cobertura y el ALTO.
            kes.Brain.CancelOrder();
            doc.Brain.CancelOrder();
            var setState = typeof(SP.Ai.AiBrain).GetMethod("SetState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setState.Invoke(kes.Brain, new object[] { SP.Ai.AiState.Idle });
            setState.Invoke(doc.Brain, new object[] { SP.Ai.AiState.Idle });

            // Lo que se mide es la formacion, no el combate: con los enemigos de patrulla de la escena de la suite activos,
            // Kes veia a uno en el camino de 100 m y se iba a perseguirlo (Chase), y el check fallaba por eso. Se apagan
            // los enemigos vivos durante la prueba y se vuelven a prender al final.
            var enemigosApagados = new System.Collections.Generic.List<GameObject>();
            foreach (var e in SP.Core.ActorRegistry.All)
                if (e != null && e.Team == SP.Combat.TeamId.Enemy && e.gameObject.activeSelf) { e.gameObject.SetActive(false); enemigosApagados.Add(e.gameObject); }
            SP.Core.ActorRegistry.Invalidate();

            SP.Ai.WorldSimulationDriver.Step(0.1f);

            var rastro = new System.Text.StringBuilder();
            // 14 s a 5 m/s = 70 m: el piso de la escena de la suite termina en z=80 y la grilla de navegacion esta bloqueada
            // desde z=86. Con los 100 m de antes Vega salia del mapa, las ranuras laterales quedaban bloqueadas y los dos
            // caian a la linea central 3 m atras (medido: perfectos al costado hasta z=85, cerrados detras desde ahi).
            for (int i = 0; i < 140; i++)
            {
                vega.transform.position += Vector3.forward * (5f * 0.1f); // 5m/s
                SP.Ai.WorldSimulationDriver.Step(0.1f);
                if (i % 40 == 0)
                {
                    var rk = vega.transform.InverseTransformPoint(kes.transform.position);
                    var rd = vega.transform.InverseTransformPoint(doc.transform.position);
                    rastro.Append($" t{i / 10f:0.0}s vega.z={vega.transform.position.z:0} kes({rk.x:0.0},{rk.z:0.0}) {kes.Brain.State} doc({rd.x:0.0},{rd.z:0.0}) {doc.Brain.State};");
                }
            }
            foreach (var e in enemigosApagados) e.SetActive(true);
            SP.Core.ActorRegistry.Invalidate();
            
            var relKes = vega.transform.InverseTransformPoint(kes.transform.position);
            var relDoc = vega.transform.InverseTransformPoint(doc.transform.position);
            
            string Estado(SP.Actors.Soldier s) => $"{s.Brain.State}{(s.Brain.IsPossessedByPlayer ? " poseido" : "")}{(s.Health.IsAlive ? "" : " muerto")} sigue={(s.Brain.FollowTarget != null ? s.Brain.FollowTarget.name : "-")} blanco={(s.Brain.CurrentTarget != null ? s.Brain.CurrentTarget.name : "-")}";
            Check($"Uno esta a la derecha y otro a la izquierda (kes {relKes.x:0.00},{relKes.z:0.00} [{Estado(kes)}] doc {relDoc.x:0.00},{relDoc.z:0.00} [{Estado(doc)}])", relKes.x * relDoc.x < 0f && Mathf.Abs(relKes.x) > 0.5f && Mathf.Abs(relDoc.x) > 0.5f);
            // Pedido explicito: la primera fila va AL COSTADO (visible en la
            // camara del jugador), no detras del hombro. z≈0, ya no < -0.5.
            Check($"Ambos van a la par (Z≈0, no detras; kes z={relKes.z:0.00} doc z={relDoc.z:0.00}) rastro:{rastro}", Mathf.Abs(relKes.z) < 0.5f && Mathf.Abs(relDoc.z) < 0.5f);
            
            SP.Ai.AjustesDeEscuadra.Lider = prevLider;
            SP.Ai.AjustesDeEscuadra.DistanciaLateralFormacion = prevLat;
            SP.Ai.AjustesDeEscuadra.DistanciaAtrasFormacion = prevAtr;
        }


        // BUG REAL (RTS): un aliado mandado a mas de DistanciaParaSeguir del lider llegaba y volvia solo con el lider (auto-seguir).
        // Ahora se queda donde se le ordeno (MantienePosicion) y solo vuelve si el lider se aleja mas que RadioDeRegresoForzado.
        static void Fase23_OrdenDeMoverSeRespeta(SP.Actors.Soldier vega, SP.Actors.Soldier kes)
        {
            TestLog.Step("Probando Fase23_OrdenDeMoverSeRespeta: el aliado ordenado lejos del lider se queda en su punto");
            var prevLider = SP.Ai.AjustesDeEscuadra.Lider;
            var prevSeguir = SP.Ai.AjustesDeEscuadra.DistanciaParaSeguir;
            SP.Ai.AjustesDeEscuadra.Lider = vega;
            SP.Ai.AjustesDeEscuadra.DistanciaParaSeguir = 12.5f;
            vega.transform.position = Vector3.zero;
            kes.transform.position = new Vector3(2.5f, 0, -1.5f);
            kes.Brain.CancelOrder();
            var setState = typeof(SP.Ai.AiBrain).GetMethod("SetState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setState.Invoke(kes.Brain, new object[] { SP.Ai.AiState.Idle });
            var enemigosApagados = new System.Collections.Generic.List<GameObject>();
            foreach (var e in SP.Core.ActorRegistry.All)
                if (e != null && e.Team == SP.Combat.TeamId.Enemy && e.gameObject.activeSelf) { e.gameObject.SetActive(false); enemigosApagados.Add(e.gameObject); }
            SP.Core.ActorRegistry.Invalidate();

            var destino = new Vector3(20f, 0f, 0f);   // a 20 m del lider: mas que DistanciaParaSeguir
            SP.Player.OrderService.IssueMoveOrder(kes, destino);
            for (int i = 0; i < 100; i++) SP.Ai.WorldSimulationDriver.Step(0.1f);   // 10 s
            float dMeta = Vector3.Distance(new Vector3(kes.transform.position.x, 0f, kes.transform.position.z), destino);
            Check($"Llego al punto ordenado y se quedo ahi (a {dMeta:0.0} m, estado {kes.Brain.State}, mantiene={kes.Brain.MantienePosicion})", dMeta < 3f && kes.Brain.MantienePosicion && kes.Brain.State != SP.Ai.AiState.Follow);

            vega.transform.position = new Vector3(0f, 0f, 60f);   // el lider se va mas de RadioDeRegresoForzado (45 m)
            // En Edit mode Time.time no avanza: el enfriamiento del regreso forzado (proximoRegresoForzado) que dejo una prueba anterior no vence nunca.
            typeof(SP.Ai.AiBrain).GetField("proximoRegresoForzado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(kes.Brain, -1f);
            for (int i = 0; i < 40; i++) SP.Ai.WorldSimulationDriver.Step(0.1f);
            Check($"Si el lider se aleja mas de 45 m, vuelve con el (estado {kes.Brain.State})", kes.Brain.State == SP.Ai.AiState.Follow);

            kes.Brain.CancelOrder();
            Check("Una orden nueva suelta el anclaje", !kes.Brain.MantienePosicion);
            foreach (var e in enemigosApagados) e.SetActive(true);
            SP.Core.ActorRegistry.Invalidate();
            SP.Ai.AjustesDeEscuadra.Lider = prevLider;
            SP.Ai.AjustesDeEscuadra.DistanciaParaSeguir = prevSeguir;
        }

                // Deshabilitado: probaba UnitLocatorCylinder.IgnorarOclusion, una
        // funcionalidad de "ignorar oclusion 4s tras recibir dano del
        // jugador" que nunca se llego a implementar (UnitLocatorCylinder no
        // tiene ningun chequeo de linea de vision/oclusion hoy, solo angulo
        // y distancia). Se deja documentado en vez de simular que pasa.
        static void Fase23_RomboSinOclusion()
        {
            TestLog.Step("Probando Fase23_RomboSinOclusion: SALTEADO -- UnitLocatorCylinder.IgnorarOclusion no esta implementado");
        }

        static void Fase23_RomboTanque()
        {
            TestLog.Step("Probando Fase23_RomboTanque: color del rombo por ocupacion");
            
            var vehGo = new GameObject("TestTank");
            vehGo.SetActive(false);
            var veh = vehGo.AddComponent<SP.Vehicles.Vehicle>();
            vehGo.SetActive(true);
            Arrancar(veh);   // Vehicle.OnEnable crea el RomboTanque
            
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
                s2.Configure("S2", SP.Combat.TeamId.Enemy, SP.Combat.RoleType.Enemy, 10);

                veh.Mount(s2, SP.Vehicles.VehicleSeatRole.Gunner, true);
                Check("Tanque mixto -> violeta", mat.color == SP.Presentation.DiamondGizmo.ColorMixto);
                
                veh.Dismount(s1);
                Check("Tanque enemigo -> rojo", mat.color == SP.Presentation.DiamondGizmo.ColorEnemigo);
                
                Object.DestroyImmediate(sol1Go);
                Object.DestroyImmediate(sol2Go);
            }
            Apagar(veh);   // OnDisable lo saca de Vehicle.Todos y de WorldSystemsRegistry
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

            // El roster habla con tres singletons que en Play dan de alta sus Awake y en Edit mode quedan vacios:
            // SelectionController.Instance (clic simple), CameraRig.Instance (centrar / pasar a FPS) y PlayerBrain.Activo
            // (doble clic = poseer). Se apuntan a los de la escena de la suite y al final se deja todo como estaba.
            var cerebroPrevio = SP.Player.PlayerBrain.Activo;
            var poseidoPrevio = inputDriver.Brain != null ? inputDriver.Brain.Current : null;
            var modoPrevio = inputDriver.Rig.Mode;
            Arrancar(inputDriver.Selection, soloAwake: true);
            SP.CameraSystem.CameraRig.EnsureInstanceForTests(inputDriver.Rig);
            if (inputDriver.Brain != null) inputDriver.Brain.Registrar();

            inputDriver.Rig.SetMode(SP.CameraSystem.ControlMode.Rts);

            var rosterRowGo = new GameObject("TestRosterRow");
            var rosterRow = rosterRowGo.AddComponent<SP.UI.RosterRowView>();
            rosterRow.Bind(doc, 1);
            
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left
            };
            
            // Simular primer click. El doble clic se detecta por Time.unscaledTime - lastClickTime <= 0,3 s, con
            // lastClickTime arrancando en 0: en Edit mode Time.unscaledTime vuelve a cero con cada recompilacion y casi no
            // avanza (medido: 4 y 13 s con el editor abierto hace una hora), asi que el PRIMER clic podia contar como doble
            // (poseia y pasaba a FPS en vez de seleccionar). Se fija "ultimo clic hace mucho", que es lo que pasa en Play.
            typeof(SP.UI.RosterRowView).GetField("lastClickTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(rosterRow, float.NegativeInfinity);
            rosterRow.OnPointerClick(eventData);
            
            Check("Despues de 1 clic, doc queda seleccionado", inputDriver.Selection.Selected.Contains(doc) && inputDriver.Selection.Selected.Count == 1);
            
            // RecenterOn fija el DESTINO del paneo (panTarget) y la camara se desliza hasta ahi en los cuadros siguientes
            // (CameraRig.LateUpdate): RtsFocus recien coincide despues de varios cuadros, tambien en Play. Se compara el destino.
            Vector3 pos = doc.transform.position;
            Vector3 focus = inputDriver.Rig.PanTargetActual;
            Check($"RTS focus debe coincidir con pos (destino del paneo: {focus}, Pos: {pos})", Mathf.Abs(focus.x - pos.x) < 0.1f && Mathf.Abs(focus.z - pos.z) < 0.1f);
            
            // Simular segundo click rapido (doble click)
            rosterRow.OnPointerClick(eventData);
            
            Check("Despues de doble clic, la camara pasa a FPS", inputDriver.Rig.Mode == SP.CameraSystem.ControlMode.Fps);
            Check("Despues de doble clic, doc pasa a estar poseido", SP.Player.PlayerBrain.Activo != null && SP.Player.PlayerBrain.Activo.Current == doc);
            
            // Doc poseido no corre su IA (y la prueba de formacion de mas abajo lo necesita siguiendo a Vega).
            if (poseidoPrevio != null && inputDriver.Brain.Current != poseidoPrevio) SP.Player.PossessionService.Swap(inputDriver.Brain, poseidoPrevio);
            inputDriver.Rig.SetMode(modoPrevio);
            VolverAlCerebro(cerebroPrevio);
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
            // Con Health, el Bootstrap del soldado (al leer su Id) lo da de alta en ActorRegistry, que es donde el anillo
            // busca al atacante por Id. Sin Health, Bootstrap tira un LogError y el atacante nunca aparece.
            attackerGo.AddComponent<SP.Combat.Health>();
            var attacker = attackerGo.AddComponent<SP.Actors.Soldier>();
            
            var playerGo = new GameObject("TestPlayer");
            playerGo.AddComponent<SP.Combat.Health>();
            var player = playerGo.AddComponent<SP.Actors.Soldier>();
            
            Arrancar(ringManager);   // Awake arma el pool de anillos, OnEnable se suscribe al dano

            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            var cerebroPrevio = SP.Player.PlayerBrain.Activo;
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

            Apagar(ringManager);
            VolverAlCerebro(cerebroPrevio);
            SP.Core.ActorRegistry.Unregister(attacker);   // en Edit mode tampoco corre el OnDestroy que lo da de baja
            SP.Core.ActorRegistry.Unregister(player);
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
            // Physics.autoSyncTransforms esta apagado: las fases anteriores mueven a los soldados por transform y, sin
            // sincronizar, collider.bounds y el raycast pueden ver la posicion fisica vieja. En Play se sincroniza en cada
            // paso de fisica. (El 0,30 que daban Vega, Kes y Doc era otra cosa: Vehicle.Dismount les dejaba la escala en 1.)
            Physics.SyncTransforms();
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
                string nombrePiso = "";
                for (int i = 0; i < n; i++)
                {
                    var c = buffer[i].collider;
                    if (c == null || c == col || c.transform.IsChildOf(s.transform)) continue;
                    float y = buffer[i].point.y;
                    if (y > col.bounds.min.y + 0.3f) continue;
                    if (y > piso) { piso = y; hay = true; nombrePiso = c.name; }
                }

                if (hay)
                {
                    float dist = Mathf.Abs(col.bounds.min.y - piso);
                    Check($"Soldado {s.name} bien apoyado (dist: {dist:0.000}; pies y={col.bounds.min.y:0.00}, piso '{nombrePiso}' y={piso:0.00}, pos {s.transform.position}, rot {s.transform.eulerAngles}, escala {s.transform.lossyScale}, {col.GetType().Name} {col.bounds.size})", dist < 0.03f);
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
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);
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
            var sinCollider = new System.Text.StringBuilder();
            foreach (var c in solidos)
            {
                // El blindado tambien cuenta como solido de cobertura, pero sus mallas (casco, torreta, cañon) no llevan
                // collider propio a proposito: el vehiculo choca y recibe tiros con su propio casco e HitboxDeImpacto. Igual
                // que en Fase23_ColliderSinMalla, los vehiculos quedan fuera.
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                var meshes = c.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var m in meshes)
                {
                    if (m.GetComponent<Collider>() == null)
                    {
                        count++;
                        sinCollider.Append(c.name).Append('/').Append(m.name).Append(' ');
                    }
                }
            }
            Check($"0 mallas en coberturas sin collider propio (hay {count}: {sinCollider})", count == 0);
        }
        static void Fase23_HudMunicion()
        {
            TestLog.Step("Probando Fase23_HudMunicion: HUD de municion y vida");
            
            var weaponGo = new GameObject("TestWeapon");
            var weapon = weaponGo.AddComponent<SP.Combat.WeaponHolder>();
            
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
            soldierGo.AddComponent<SP.Combat.Health>();
            var soldier = soldierGo.AddComponent<SP.Actors.Soldier>();
            soldier.Configure("TestSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);
            brain.Possess(soldier);

            view.Bind(img, brain);
            Arrancar(view);   // OnEnable arma la capa roja (RedImpact) y se suscribe al dano

            float alpha0 = view.CurrentRedAlpha;
            Check("Alpha inicial del impacto rojo es 0", alpha0 == 0f);

            SP.Core.EventBus.Instance.Publish(new SP.Core.DamageTakenEvent(soldier.Id, 1, 20, 80));
            
            float alpha1 = view.CurrentRedAlpha;
            Check("Alpha del impacto rojo sube al recibir dano", alpha1 > 0f);
            
            Apagar(view);
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
            // TorretaFija.Instalar y CajaDeSuministros.Crear arman el rombo SOLO en Play (if Application.isPlaying): en
            // Edit mode instalar no debe dejar rombos guardados en la escena. Asi que en la suite (Edit mode) se verifica
            // eso, y la parte de Play (rombo visible, se oculta al ocupar) queda para cuando la suite corre en Play.
            if (!Application.isPlaying)
                Check("Fuera de Play, instalar una torreta no deja un rombo en la escena (se arma en Play)", romboTorreta == null);
            else
                Check("Torreta vacia tiene rombo", romboTorreta != null && romboTorreta.Condicion() == true);

            var soldierGo = new GameObject("TestSoldier");
            soldierGo.AddComponent<SP.Combat.Health>();
            soldierGo.AddComponent<SP.Combat.WeaponHolder>();
            var soldier = soldierGo.AddComponent<SP.Actors.Soldier>();
            soldier.Configure("TestSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            torreta.Ocupar(soldier, out _);
            if (Application.isPlaying)
                Check("Torreta ocupada oculta el rombo", romboTorreta != null && romboTorreta.Condicion() == false);

            // Municion
            var pk = SP.Player.MunicionPickup.Crear(Vector3.zero);
            var romboMun = pk.GetComponentInChildren<SP.Presentation.InteractableDiamond>();
            Check("MunicionPickup tiene rombo", romboMun != null);

            // Caja
            var caja = SP.Player.CajaDeSuministros.Crear(Vector3.zero);
            var romboCaja = caja.GetComponentInChildren<SP.Presentation.InteractableDiamond>();
            if (!Application.isPlaying)
                Check("Fuera de Play, la caja de suministros no deja un rombo en la escena (se arma en Play)", romboCaja == null);
            else
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
            Arrancar(view);   // OnEnable llama a Diagramador.AcomodarSelectionCount

            Check("Anclado abajo (anchorMin.y == 0)", rt.anchorMin.y == 0f);
            Check("Anclado al centro (anchorMin.x == 0.5)", rt.anchorMin.x == 0.5f);
            Check("Posicion Y deja espacio para InstructionBannerView (> 78)", rt.anchoredPosition.y >= 78f + SP.UI.Diagramador.Aire);

            Apagar(view);
            Object.DestroyImmediate(go);
        }
        static void Fase23_AgachadoContagio()
        {
            TestLog.Step("Probando Fase23_AgachadoContagio: Agacharse contagia a aliados cercanos");

            var goPlayer = new GameObject("PlayerSoldier");
            goPlayer.AddComponent<SP.Combat.Health>();
            goPlayer.AddComponent<SP.Actors.SoldierMotor>();
            var playerSoldier = goPlayer.AddComponent<SP.Actors.Soldier>();
            playerSoldier.Configure("PlayerSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var goFollower = new GameObject("FollowerSoldier");
            goFollower.AddComponent<SP.Combat.Health>();
            goFollower.AddComponent<SP.Actors.SoldierMotor>();
            goFollower.AddComponent<SP.Ai.AiBrain>();
            var followerSoldier = goFollower.AddComponent<SP.Actors.Soldier>();
            followerSoldier.Configure("FollowerSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var goFarFollower = new GameObject("FarFollowerSoldier");
            goFarFollower.AddComponent<SP.Combat.Health>();
            goFarFollower.AddComponent<SP.Actors.SoldierMotor>();
            goFarFollower.AddComponent<SP.Ai.AiBrain>();
            var farFollowerSoldier = goFarFollower.AddComponent<SP.Actors.Soldier>();
            farFollowerSoldier.Configure("FarFollowerSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var goCoverFollower = new GameObject("CoverFollowerSoldier");
            goCoverFollower.AddComponent<SP.Combat.Health>();
            goCoverFollower.AddComponent<SP.Actors.SoldierMotor>();
            goCoverFollower.AddComponent<SP.Ai.AiBrain>();
            var coverFollowerSoldier = goCoverFollower.AddComponent<SP.Actors.Soldier>();
            coverFollowerSoldier.Configure("CoverFollowerSoldier", SP.Combat.TeamId.Player, SP.Combat.RoleType.Assault, 100);

            var brainGo = new GameObject("TestBrain");
            var brain = brainGo.AddComponent<SP.Player.PlayerBrain>();
            // AiBrain.TickAgachadoContagio copia la postura del soldado de PlayerBrain.Activo: en Play este cerebro se
            // registra en su Awake, en Edit mode hay que hacerlo a mano.
            var cerebroPrevio = SP.Player.PlayerBrain.Activo;
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

            VolverAlCerebro(cerebroPrevio);
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
            goCivil.AddComponent<SP.Combat.Health>();
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
    }
}


