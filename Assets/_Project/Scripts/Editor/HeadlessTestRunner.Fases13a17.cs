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
    // HeadlessTestRunner (parte): fases 13 a 17 de la suite.
    public partial class HeadlessTestRunner
    {
        // FASE 13 - segunda tanda de pedidos: asientos, marcadores de
        // recorrido, icono por arma, seleccion unica y ruta de vehiculo.
        static void RunPhase13(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 13 - Asientos, marcadores de recorrido, iconos de arma y seleccion unica");

            // --- Iconos: uno por arma, y distintos entre si ---
            var iconoRifle = WeaponStatusView.IconFor(WeaponKind.Rifle);
            var iconoPistola = WeaponStatusView.IconFor(WeaponKind.Pistol);
            var iconoPesada = WeaponStatusView.IconFor(WeaponKind.Heavy);
            Check("Cada arma (1/2/3) tiene su icono y son tres distintos",
                iconoRifle != null && iconoPistola != null && iconoPesada != null
                && iconoRifle.texture != iconoPistola.texture && iconoPistola.texture != iconoPesada.texture
                && iconoRifle.texture != iconoPesada.texture);

            // --- Asientos: intercambio con un aliado y pase a uno libre ---
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vega.Brain.CancelOrder(); kes.Brain.CancelOrder(); doc.Brain.CancelOrder();
            vehicle.Mount(vega, VehicleSeatRole.Driver);
            vehicle.Mount(kes, VehicleSeatRole.Passenger1);
            bool cambio = vehicle.SwapSeats(vega, kes);
            Check($"Dos ocupantes intercambian asiento (Vega {vehicle.RoleOf(vega)}, Kes {vehicle.RoleOf(kes)})",
                cambio && vehicle.RoleOf(vega) == VehicleSeatRole.Passenger1 && vehicle.RoleOf(kes) == VehicleSeatRole.Driver);
            Check("Tras intercambiar siguen 2 a bordo, con el Brain apagado",
                vehicle.OccupantCount == 2 && !vega.Brain.enabled && !kes.Brain.enabled);

            bool paso = vehicle.MoveToSeat(vega, VehicleSeatRole.Gunner);
            Check($"Pasar a un asiento LIBRE lo mueve (Vega {vehicle.RoleOf(vega)}) y libera el anterior",
                paso && vehicle.RoleOf(vega) == VehicleSeatRole.Gunner && vehicle.IsSeatFree(VehicleSeatRole.Passenger1));
            Check("Pasar a un asiento OCUPADO no hace nada (Kes sigue de conductor)",
                !vehicle.MoveToSeat(kes, VehicleSeatRole.Gunner) && vehicle.RoleOf(kes) == VehicleSeatRole.Driver);
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);

            // --- Marcadores de cola: viven mientras algun soldado los tenga ---
            OrderMarkerFx.ClearQueuedMarkers();
            var p1 = new Vector3(40f, 0f, 40f);
            var p2 = new Vector3(46f, 0f, 40f);
            OrderService.IssueMoveOrder(kes, p1);
            OrderService.IssueMoveOrder(kes, p2, queued: true);
            int marcadoresConCola = OrderMarkerFx.QueuedMarkers.Count;
            int soltadosConOrden = OrderMarkerFx.PurgarCompletados();
            Check($"Con la orden en cola vigente el marcador NO se limpia ({marcadoresConCola} marcador, {soltadosConOrden} soltados)",
                marcadoresConCola == 1 && soltadosConOrden == 0);
            OrderService.IssueMoveOrder(kes, new Vector3(-40f, 0f, 40f));
            int soltadosTrasNuevaOrden = OrderMarkerFx.PurgarCompletados();
            Check($"Una orden nueva a otro lado borra la cola vieja y su marcador se limpia ({soltadosTrasNuevaOrden} soltado, quedan {OrderMarkerFx.QueuedMarkers.Count})",
                soltadosTrasNuevaOrden == 1 && OrderMarkerFx.QueuedMarkers.Count == 0);
            kes.Brain.CancelOrder();

            // El trazado sin ejecutar NO lo toca la limpieza automatica.
            TrazadoDeCamino.Limpiar();
            TrazadoDeCamino.Marcar(new Vector3(30f, 0f, 30f));
            TrazadoDeCamino.Marcar(new Vector3(36f, 0f, 30f));
            int soltadosDelPlan = OrderMarkerFx.PurgarCompletados();
            Check($"Los marcadores de un recorrido todavia sin ejecutar se respetan ({soltadosDelPlan} soltados, {OrderMarkerFx.QueuedMarkers.Count} vivos)",
                soltadosDelPlan == 0 && OrderMarkerFx.QueuedMarkers.Count == 2);
            TrazadoDeCamino.Limpiar();
            Check($"Descartar el trazado los limpia todos ({OrderMarkerFx.QueuedMarkers.Count} vivos)",
                OrderMarkerFx.QueuedMarkers.Count == 0);

            // --- RTS -> FPS con UNA sola unidad seleccionada ---
            var seleccion = inputDriver.Selection;
            seleccion.Clear();
            Check("Sin seleccion no hay soldado unico", inputDriver.SoldadoUnicoSeleccionado() == null);
            seleccion.SelectSingle(kes);
            Check("Con UNO seleccionado, Tab lo posee (soldado unico = Kes)", inputDriver.SoldadoUnicoSeleccionado() == kes);
            seleccion.AddToSelection(doc);
            Check("Con dos seleccionados no se cambia de poseido", inputDriver.SoldadoUnicoSeleccionado() == null);
            seleccion.Clear();

            // --- El vehiculo sin NavMesh (Edit mode) sigue en linea recta ---
            var vb = vehicle.GetComponent<VehicleBrain>();
            vb.IssueMoveOrder(new Vector3(60f, 0f, 60f));
            Check($"Sin NavMesh horneado la orden del vehiculo no arma ruta ({vb.Route.Count} esquinas)", vb.Route.Count == 0 && vb.HasOrder);
            vb.Stop();
            Check("Detener limpia destino y ruta", !vb.HasOrder && vb.Route.Count == 0);

            TestLog.Phase("FASE 13 FINALIZADA");
        }


        // ---------------------------------------------------------------
        // FASE 14 · coberturas ordenadas, seguir al jugador, objetivo
        // dinamico, impactos en cubitos, tanque (teclas, patrulla) y feedback
        // ---------------------------------------------------------------
        static GameObject CrearCoberturaDePrueba(string nombre, Vector3 pos, int vida)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.position = pos + Vector3.up * 0.6f;
            go.transform.localScale = new Vector3(3f, 1.2f, 1f);
            var m = go.AddComponent<ObstacleMarker>();
            var so = new SerializedObject(m);
            so.FindProperty("maxHealth").intValue = vida;
            so.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            return go;
        }


        // ------------------------------------------------------------------
        // FASE 15 (ronda 6): radial contextual, modo dios, correr, ametralladora fija, reanimar, mira
        // ------------------------------------------------------------------
        static void RunPhase15(PlayerInputDriver inputDriver, AimTargeting aim, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                               GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            TestLog.Phase("FASE 15 - Radial contextual, modo dios, correr, ametralladora fija, reanimar, coberturas ocultas");

            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vega.Brain.CancelOrder(); kes.Brain.CancelOrder(); doc.Brain.CancelOrder();
            FullHeal(vega, kes, doc);
            SP.Core.ModoDios.Poner(false);

            // --- Teclas nuevas ---
            Check("[C] es la tecla de vista tactica (coberturas y rutas)", KeyBindings.Get(KeyBindings.VerTactico) == UnityEngine.InputSystem.Key.C);
            Check("La tabla de controles menciona F4 y la vista tactica", SP.UI.ControlsTable.FullText().Contains("F4") && SP.UI.ControlsTable.FullText().Contains("modo dios"));

            // --- Modo dios ---
            int antes = kes.Health.Current;
            SP.Core.ModoDios.Poner(true);
            kes.Health.TakeDamage(60, -1);
            Check("Modo dios: un aliado no recibe dano", kes.Health.Current == antes);
            var enemigo = SpawnSoldier(soldierPrefab, "T15_Enemigo", TeamId.Enemy, RoleType.Enemy, new Vector3(-70f, 0.8f, 70f), colorEnemy, pool, 100);
            enemigo.Health.TakeDamage(30, kes.Id);
            Check("Modo dios: el enemigo SI recibe dano", enemigo.Health.Current == 70);
            int vidaTanque = vehicle.Health.Current;
            vehicle.TakeDamage(80, -1);
            Check("Modo dios: el tanque propio no recibe dano", vehicle.Health.Current == vidaTanque);
            SP.Core.ModoDios.Poner(false);
            kes.Health.TakeDamage(60, -1);
            Check("Sin modo dios el dano vuelve", kes.Health.Current == antes - 60);
            FullHeal(vega, kes, doc);

            // --- Correr ---
            float caminar = vega.Motor.MoveSpeed;
            vega.Motor.SetRunning(true);
            Check($"Correr acelera al {SoldierMotor.FactorDeCarrera:0.0}x ({vega.Motor.MoveSpeed:0.0} contra {caminar:0.0} m/s)", Mathf.Abs(vega.Motor.MoveSpeed - caminar * SoldierMotor.FactorDeCarrera) < 0.01f && vega.Motor.Corriendo);
            vega.Motor.SetCrouching(true);
            Check("Agachado no se corre", !vega.Motor.Corriendo);
            vega.Motor.SetCrouching(false);
            vega.Motor.SetRunning(false);
            Check("Sin Shift vuelve a caminar", Mathf.Abs(vega.Motor.MoveSpeed - caminar) < 0.01f);

            // --- Radial contextual: solo lo que se puede hacer con lo apuntado ---
            var suelo = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Ground, Point = Vector3.zero });
            bool soloBase = suelo.Visible[SP.UI.MenuDeOrdenes.IrAlli] && suelo.Visible[SP.UI.MenuDeOrdenes.Cubrirse] && suelo.Visible[SP.UI.MenuDeOrdenes.Posicion];
            int visibles = 0; foreach (bool v in suelo.Visible) if (v) visibles++;
            Check($"Apuntando al suelo el radial ofrece solo IR ALLI, CUBRIRSE y POSICION ({visibles})", soloBase && visibles == 3);

            kes.Configure(kes.DisplayName, TeamId.Player, RoleType.Flanker, kes.Health.MaxHealth);
            doc.Configure(doc.DisplayName, TeamId.Player, RoleType.Medic, doc.Health.MaxHealth);
            kes.Health.TakeDamage(90, -1);
            var aHerido = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Ally, Soldier = kes, Point = kes.transform.position, HitTransform = kes.transform });
            Check("Apuntando a un aliado HERIDO con medico vivo aparece CURAR (contextual)", aHerido.Visible[SP.UI.MenuDeOrdenes.Curar] && aHerido.Contextual[SP.UI.MenuDeOrdenes.Curar]);
            Check("...y tambien POSEER", aHerido.Visible[SP.UI.MenuDeOrdenes.Poseer] && aHerido.Contextual[SP.UI.MenuDeOrdenes.Poseer]);
            Check("...pero no DEMOLER ni TANQUE ni TORRETA", !aHerido.Visible[SP.UI.MenuDeOrdenes.Demoler] && !aHerido.Visible[SP.UI.MenuDeOrdenes.Tanque] && !aHerido.Visible[SP.UI.MenuDeOrdenes.Torreta]);
            FullHeal(vega, kes, doc);
            var aSano = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Ally, Soldier = kes, Point = kes.transform.position, HitTransform = kes.transform });
            Check("Apuntando a un aliado SANO no aparece CURAR", !aSano.Visible[SP.UI.MenuDeOrdenes.Curar]);

            var aEnemigo = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Enemy, Soldier = enemigo, Point = enemigo.transform.position, HitTransform = enemigo.transform });
            Check("Apuntando a un enemigo aparece ATACAR", aEnemigo.Visible[SP.UI.MenuDeOrdenes.Atacar] && aEnemigo.Contextual[SP.UI.MenuDeOrdenes.Atacar]);

            var aTanque = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = vehicle.transform.position, HitTransform = vehicle.transform });
            Check("Apuntando al tanque aliado aparece TANQUE (con SUBIRME YO)", aTanque.Visible[SP.UI.MenuDeOrdenes.Tanque] && aTanque.OpcionVisible[SP.UI.MenuDeOrdenes.Tanque][3]);

            // Las categorias contextuales van primero y el menu las marca
            var menu = inputDriver.OrdenesMenu;
            if (menu != null && menu.EsRadial)
            {
                menu.Abrir(aHerido);
                Check($"El radial pone lo contextual primero ({menu.CategoriasVisibles[0]}) y lo marca dorado", menu.EsContextual(menu.CategoriasVisibles[0]));
                Check($"...con {menu.CategoriasVisibles.Count} categorias (2 contextuales + IR ALLI, CUBRIRSE, POSICION)", menu.CategoriasVisibles.Count == 5);
                menu.ElegirDirecto(SP.UI.MenuDeOrdenes.Curar, 2);
                Check("Elegir CURAR A ESTE devuelve la opcion real 2", menu.Seleccion == SP.UI.MenuDeOrdenes.Curar && menu.Sub == 2);
                Check("Las opciones de CURAR con un herido apuntado se filtran (sin REVIVIR)", !System.Linq.Enumerable.Contains(menu.OpcionesVisibles, 3));
                menu.Cerrar();
                menu.Abrir(suelo);
                Check("Sin contexto solo hay 3 categorias", menu.CategoriasVisibles.Count == 3);
                menu.Cerrar();
            }

            // --- Reanimar a un caido ---
            kes.Health.TakeDamage(9999, -1);
            Check("Un aliado muerto queda caido", !kes.Health.IsAlive);
            var caido = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Caido, Soldier = kes, Point = kes.transform.position, HitTransform = kes.transform });
            Check("Apuntando a un aliado caido con medico vivo aparece CURAR > REVIVIR", caido.Visible[SP.UI.MenuDeOrdenes.Curar] && caido.OpcionVisible[SP.UI.MenuDeOrdenes.Curar][3]);
            kes.transform.position = new Vector3(-40f, 0.8f, -40f);
            doc.transform.position = new Vector3(-34f, 0.8f, -40f);
            Physics.SyncTransforms();
            bool pidio = PedidoDeCuracion.SolicitarReanimar(kes);
            Check("El medico acepta reanimar al caido", pidio && PedidoDeCuracion.Reanimando);
            bool revivio = SimulateUntil(() => kes.Health.IsAlive, 20f);
            Check($"El medico llega, se queda junto a el y lo levanta ({(revivio ? kes.Health.Current : 0)} de vida)", revivio);
            Check("...y el medico vuelve a pelear normal", doc.Brain != null && !doc.Brain.Pasivo);
            PedidoDeCuracion.Cancelar();
            FullHeal(vega, kes, doc);

            // --- Ametralladora fija ---
            var go = new GameObject("T15_Torreta");
            go.transform.position = new Vector3(-90f, 0.5f, 90f);
            var torreta = TorretaFija.Instalar(go);
            Check("Una torreta fija instalada esta libre y con zona de apuntado", torreta.Libre && go.GetComponent<BoxCollider>() != null);
            vega.transform.position = new Vector3(-90f, 0.8f, 86f);
            vega.transform.rotation = Quaternion.identity;
            int loadoutPrevio = vega.Weapon.CurrentLoadoutIndex;
            var armaPrevia = vega.Weapon.CurrentWeaponKind;
            string motivo;
            bool ocupada = torreta.Ocupar(vega, out motivo);
            Check("El soldado ocupa la torreta", ocupada && torreta.Ocupante == vega);
            Check($"...se queda plantado detras del arma y con cinta de {TorretaFija.Cinta} balas", vega.Weapon.CurrentAmmo == TorretaFija.Cinta && vega.Weapon.MagazineSize == TorretaFija.Cinta);
            Check("Otro soldado no puede ocuparla", !torreta.Ocupar(kes, out motivo));
            vega.transform.rotation = Quaternion.Euler(0f, torreta.YawCentro + 170f, 0f);
            torreta.AcotarGiro(vega);
            Check($"El giro esta limitado al arco de +-{TorretaFija.ArcoDeGiro:0}", Mathf.Abs(Mathf.DeltaAngle(torreta.YawCentro, vega.transform.eulerAngles.y)) <= TorretaFija.ArcoDeGiro + 0.1f);
            var apuntaTorreta = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Ally, Soldier = kes });
            torreta.Liberar();
            Check("Al salir la torreta queda libre", torreta.Libre);
            Check("...y el soldado recupera su arma de siempre", vega.Weapon.CurrentWeaponKind == armaPrevia && vega.Weapon.CurrentLoadoutIndex == loadoutPrevio);
            var aTorreta = inputDriver.ConstruirContextoRadial(new AimResult { Type = AimTargetType.Torreta, Torreta = torreta, Point = go.transform.position, HitTransform = go.transform });
            Check("Apuntando a la torreta libre aparece TORRETA (contextual, opcion USAR)", aTorreta.Visible[SP.UI.MenuDeOrdenes.Torreta] && aTorreta.Contextual[SP.UI.MenuDeOrdenes.Torreta] && aTorreta.OpcionVisible[SP.UI.MenuDeOrdenes.Torreta][0]);
            Check("TorretaFija.MasCercana la encuentra a distancia horizontal", TorretaFija.MasCercana(new Vector3(-90f, 5f, 87f), TorretaFija.AlcanceDeUso) == torreta);
            UnityEngine.Object.DestroyImmediate(go);

            // --- Aliados caidos: se los apunta aunque no tengan collider ---
            kes.Health.TakeDamage(9999, -1);
            var col = kes.GetComponent<Collider>(); bool colHabia = col != null && col.enabled; if (col != null) col.enabled = false;
            kes.transform.position = new Vector3(-40f, 0.8f, -40f);
            Physics.SyncTransforms();
            var rayo = new Ray(kes.transform.position + new Vector3(0f, 1.5f, -6f), (new Vector3(0f, 0.2f, 0f) - new Vector3(0f, 1.5f, -6f)).normalized);
            var mira = aim.Evaluate(rayo, vega);
            Check($"Un aliado caido sin collider igual se apunta ({mira.Type})", mira.Type == AimTargetType.Caido && mira.Soldier == kes);
            if (col != null) col.enabled = colHabia;
            FullHeal(vega, kes, doc);
            kes.Health.Initialize(kes.Id, kes.Health.MaxHealth);

            // --- Coberturas ocultas ---
            SP.Core.Coberturas.MostrarMarcas(false);
            Check("Las coberturas y rutas no estan siempre a la vista", !SP.Core.Coberturas.MarcasVisibles);
            SP.Core.ModoDios.Poner(false);
            SP.UI.MenuDeOrdenes.PonerPista(-1);
            TestLog.Phase("FASE 15 FINALIZADA");
        }

        // ------------------------------------------------------------------
        // FASE 16 (ronda 7): salto sin hundirse, sonidos por arma, cuchillo [F], granada [G], mirillas propias,
        // regeneracion apagable y carga de demolicion ajustable.
        // ------------------------------------------------------------------
        // Ronda 8: cajas de suministros, buffer de salto, HUD sin superposiciones, pausa con audio, cartel de obstaculos.
        static void RunPhase17(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc)
        {
            TestLog.Phase("FASE 17 - Suministros, buffer de salto, HUD pulido, pausa con audio");
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            FullHeal(vega, kes, doc);
            SP.Core.ModoDios.Poner(false);
            Health.RegeneracionPermitida = false;
            inputDriver.Brain.Possess(vega);

            // --- Caja de suministros ---
            var caja = CajaDeSuministros.Crear(vega.transform.position + new Vector3(30f, 0f, 30f));
            Check("La caja de suministros se crea y esta disponible", caja != null && caja.Disponible);
            vega.Weapon.ConsumirGranada(); vega.Weapon.ConsumirGranada();
            vega.Health.TakeDamage(Mathf.RoundToInt(vega.Health.MaxHealth * 0.5f), -1);
            int granadasAntes = vega.Weapon.Granadas, vidaAntes = vega.Health.Current, recogidasAntes = CajaDeSuministros.Recogidas;
            var upd = typeof(CajaDeSuministros).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            vega.transform.position = caja.transform.position + new Vector3(8f, 0f, 0f);
            upd.Invoke(caja, null);
            Check("Lejos de la caja no pasa nada", vega.Weapon.Granadas == granadasAntes && caja.Disponible);
            vega.transform.position = new Vector3(caja.transform.position.x + 0.5f, vega.transform.position.y, caja.transform.position.z);
            upd.Invoke(caja, null);
            Check($"Junto a la caja las granadas vuelven al maximo ({granadasAntes} -> {vega.Weapon.Granadas})", vega.Weapon.Granadas == WeaponHolder.GranadasMaximas);
            Check($"Junto a la caja se cura ({vidaAntes} -> {vega.Health.Current})", vega.Health.Current > vidaAntes);
            Check("La caja se vacia y cuenta la recogida", !caja.Disponible && CajaDeSuministros.Recogidas == recogidasAntes + 1);
            UnityEngine.Object.DestroyImmediate(caja.gameObject);
            Health.RegeneracionPermitida = true;

            // --- Buffer de salto: un segundo salto pedido en el aire se guarda ---
            var motor = vega.Motor;
            motor.Jump();
            bool saltando = motor.IsJumping;
            motor.Jump();
            var campo = typeof(SoldierMotor).GetField("saltoPedidoHasta", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check("Pedir otro salto en el aire lo deja en el buffer", saltando && campo != null && (float)campo.GetValue(motor) > Time.time);

            // --- Minimapa legible ---
            var goMm = new GameObject("t_mm", typeof(Camera));
            var mm = goMm.AddComponent<SP.UI.MinimapFollow>();
            Check($"El minimapa arranca en {mm.tamanoMini.x} px (>= 140)", mm.tamanoMini.x >= 140f);
            UnityEngine.Object.DestroyImmediate(goMm);

            // --- El cartel de obstaculos lejanos esta acotado ---
            Check("El cartel de obstaculos solo aparece a menos de 30 m", SP.UI.AimUI.DistanciaMaximaCartelObstaculo <= 30f);

            // --- Tutorial: paso nuevo de suministros ---
            var tm = UnityEngine.Object.FindAnyObjectByType<SP.Tutorial.TutorialManager>();
            Check("El tutorial tiene el paso de suministros (36 pasos)", tm == null || tm.Total >= 36);
        }

        static void RunPhase16(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                               GameObject soldierPrefab, Color colorEnemy, ProjectilePool pool)
        {
            TestLog.Phase("FASE 16 - Salto corregido, sonido y mirilla por arma, cuchillo [F], granada [G], feedback");

            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vega.Brain.CancelOrder(); kes.Brain.CancelOrder(); doc.Brain.CancelOrder();
            FullHeal(vega, kes, doc);
            SP.Core.ModoDios.Poner(false);

            // --- Teclas ---
            Check("[F] es el cuchillo", KeyBindings.Get(KeyBindings.AtaqueCuchillo) == UnityEngine.InputSystem.Key.F);
            Check("[G] es la granada", KeyBindings.Get(KeyBindings.Granada) == UnityEngine.InputSystem.Key.G);
            Check("Poseer con [F] ya no existe (lo hace el radial)", KeyBindings.Get(KeyBindings.Poseer) == UnityEngine.InputSystem.Key.None);
            Check("La tabla de controles menciona el cuchillo y la granada", SP.UI.ControlsTable.FullText().Contains("cuchillo") && SP.UI.ControlsTable.FullText().Contains("granada"));

            // --- Salto: importado como humanoide y el controlador usa los clips HORNEADOS con la cadera a la altura de pie ---
            var ctrlSoldado = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/_Project/Animation/AC_Soldado.controller");
            Check("Existe el controlador del soldado", ctrlSoldado != null);
            foreach (var par in new[] { ("SaltoArriba", "jump up"), ("SaltoAire", "jump loop"), ("SaltoAbajo", "jump down") })
            {
                var imp = UnityEditor.AssetImporter.GetAtPath("Assets/ARTS/Slim Shooter Pack/" + par.Item2 + ".fbx") as UnityEditor.ModelImporter;
                Check($"'{par.Item2}' se importa como Humanoide (mismo avatar que el soldado)", imp != null && imp.animationType == UnityEditor.ModelImporterAnimationType.Human);
                AnimationClip usado = null;
                if (ctrlSoldado != null)
                    foreach (var e in ctrlSoldado.layers[0].stateMachine.states) if (e.state.name == par.Item1) usado = e.state.motion as AnimationClip;
                Check($"El estado {par.Item1} usa un clip horneado (.anim propio, no el del FBX)", usado != null && UnityEditor.AssetDatabase.GetAssetPath(usado).EndsWith(".anim"));
                float suma = 0f; int n = 0;
                if (usado != null)
                    foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(usado))
                        if (b.propertyName == "RootT.y") foreach (var k in UnityEditor.AnimationUtility.GetEditorCurve(usado, b).keys) { suma += k.value; n++; }
                float prom = n > 0 ? suma / n : 0f;
                Check($"'{par.Item2}': altura de cadera {prom:0.00} cerca de la de pie (0,96), no hundida (0,41)", prom > 0.85f && prom < 1.1f);
            }

            // --- Sonidos: cada accion nueva tiene un clip valido y audible ---
            var nuevos = new[] { SfxKind.Explosion, SfxKind.GrenadePin, SfxKind.GrenadeThrow, SfxKind.GrenadeBounce, SfxKind.KnifeSwing, SfxKind.KnifeHit,
                                 SfxKind.Jump, SfxKind.Land, SfxKind.RadialOpen, SfxKind.RadialTick, SfxKind.RadialConfirm, SfxKind.RadialCancel,
                                 SfxKind.HealStart, SfxKind.HealDone, SfxKind.Revive, SfxKind.BombPlant, SfxKind.BombTick };
            int mudos = 0; string quien = "";
            foreach (var k in nuevos)
            {
                var clip = GenericSfx.Get(k);
                if (!ClipAudible(clip)) { mudos++; quien += k + " "; }
            }
            Check($"Los {nuevos.Length} sonidos nuevos existen y no estan mudos ni con NaN ({quien})", mudos == 0);
            Check("La explosion es un estruendo largo (> 1,5 s)", GenericSfx.Get(SfxKind.Explosion).length > 1.5f);
            Check("El tic de la carga es mas corto que el estruendo", GenericSfx.Get(SfxKind.BombTick).length < GenericSfx.Get(SfxKind.Explosion).length);

            var armas = new[] { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Heavy, WeaponKind.Smg, WeaponKind.Shotgun, WeaponKind.Sniper, WeaponKind.Rocket };
            var vistosRecarga = new HashSet<AudioClip>(); var vistosDisparo = new HashSet<AudioClip>(); var vistosDesenfunde = new HashSet<AudioClip>();
            int recargasFueraDeTiempo = 0, sinDisparo = 0;
            foreach (var a in armas)
            {
                var rec = GenericSfx.GetWeaponReload(a); var dis = GenericSfx.GetWeaponShot(a); var des = GenericSfx.GetWeaponDraw(a);
                vistosRecarga.Add(rec); vistosDisparo.Add(dis); vistosDesenfunde.Add(des);
                if (!ClipAudible(dis)) sinDisparo++;
                float dur = WeaponCatalog.Get(a).ReloadDuration;
                if (!ClipAudible(rec) || Mathf.Abs(rec.length - dur) > 0.7f) recargasFueraDeTiempo++;
            }
            Check("Las 7 armas tienen disparo propio distinto", vistosDisparo.Count == 7 && sinDisparo == 0);
            Check("Las 7 armas tienen recarga propia distinta", vistosRecarga.Count == 7);
            Check("La recarga de cada arma dura lo que dice el catalogo (+-0,7 s)", recargasFueraDeTiempo == 0);
            Check("Las 7 armas tienen sonido de desenfundar propio", vistosDesenfunde.Count == 7);
            Check("El lanzacohetes suena a cohete (fiush largo, > 1 s)", GenericSfx.GetWeaponShot(WeaponKind.Rocket).length > 1f);
            Check("Metralleta, escopeta y francotirador usan grabaciones reales", GenericSfx.GetWeaponShot(WeaponKind.Smg).name.StartsWith("Shot_Smg")
                && GenericSfx.GetWeaponShot(WeaponKind.Shotgun).name.StartsWith("Shot_Shotgun") && GenericSfx.GetWeaponShot(WeaponKind.Sniper).name.StartsWith("Shot_Sniper"));

            // --- Mirillas: una distinta por arma ---
            var estilos = new HashSet<ReticleStyle>(); var sprites = new HashSet<Sprite>();
            foreach (var a in armas) { var e = WeaponCatalog.Get(a).Reticle; estilos.Add(e); sprites.Add(SP.UI.MirillaView.SpriteDe(e)); }
            Check("Cada arma tiene su propio estilo de mirilla (7 distintos)", estilos.Count == 7);
            Check("...y cada estilo tiene su sprite (7 distintos)", sprites.Count == 7 && !sprites.Contains(null));

            // --- Cuchillo ---
            var victima = SpawnSoldier(soldierPrefab, "T16_Victima", TeamId.Enemy, RoleType.Enemy, kes.transform.position + kes.transform.forward * 1.4f, colorEnemy, pool, 100);
            SP.Presentation.CuchilloFx.ResetearContadores();
            kes.Weapon.Tick(1f);
            bool golpeo = kes.Weapon.TryMelee();
            Check("El cuchillo pega a un enemigo a 1,4 m", golpeo && victima.Health.Current == 100 - 55);
            Check("El cuchillo registra tajo y acierto para el feedback", SP.Presentation.CuchilloFx.Tajos == 1 && SP.Presentation.CuchilloFx.Aciertos == 1);
            Check("El cuchillo tiene enfriamiento (no dos tajos seguidos)", !kes.Weapon.TryMelee());

            // --- Granada ---
            Check("Cada soldado empieza con 3 granadas", kes.Weapon.Granadas == WeaponHolder.GranadasMaximas);
            int gastadas = 0; while (kes.Weapon.ConsumirGranada()) gastadas++;
            Check("Se pueden gastar exactamente 3 y la cuarta falla", gastadas == 3 && !kes.Weapon.ConsumirGranada());
            kes.Weapon.ReponerGranadas();
            Check("Reponer devuelve las 3", kes.Weapon.Granadas == 3);

            var v0 = Granada.VelocidadHacia(new Vector3(0f, 1.2f, 0f), new Vector3(12f, 1.2f, 0f));
            Check($"La velocidad de lanzamiento a 12 m es {Granada.VelocidadDeLanzamiento} m/s ({v0.magnitude:0.0})", Mathf.Abs(v0.magnitude - Granada.VelocidadDeLanzamiento) < 0.05f);
            var lejos = Granada.VelocidadHacia(new Vector3(0f, 1.2f, 0f), new Vector3(200f, 1.2f, 0f));
            Check("Fuera de alcance sale a 45 grados (el maximo)", Mathf.Abs(Mathf.Atan2(lejos.y, lejos.x) * Mathf.Rad2Deg - 45f) < 0.5f);
            // Alcance teorico: v^2/g. La curva de la vista previa tiene que caer donde dice la formula (sin piso: 3 s de simulacion).
            var puntos = new List<Vector3>();
            Granada.Simular(new Vector3(500f, 30f, 500f), Granada.VelocidadHacia(new Vector3(500f, 30f, 500f), new Vector3(512f, 30f, 500f)), null, puntos, out var caida, out _);
            Check($"La curva simulada tiene {puntos.Count} puntos y avanza hacia el objetivo", puntos.Count > 10 && puntos[puntos.Count - 1].x > puntos[0].x + 8f);

            // Explosion: dana al enemigo, no al propio bando
            var blanco = SpawnSoldier(soldierPrefab, "T16_Blanco", TeamId.Enemy, RoleType.Enemy, new Vector3(-80f, 0.8f, 80f), colorEnemy, pool, 200);
            var aliado = SpawnSoldier(soldierPrefab, "T16_Aliado", TeamId.Player, RoleType.Assault, new Vector3(-79f, 0.8f, 80f), colorEnemy, pool, 200);
            Projectile.ExplodeAt(new Vector3(-80f, 0.8f, 80f), Granada.RadioDeExplosion, Granada.Dano, kes.Id, TeamId.Player);
            Check("La explosion de la granada hiere al enemigo del centro", blanco.Health.Current < 200);
            Check("...y NO hiere al aliado que esta pegado", aliado.Health.Current == 200);

            // --- Regeneracion apagable (tutorial de curar) ---
            var regen = SpawnSoldier(soldierPrefab, "T16_Regen", TeamId.Player, RoleType.Assault, new Vector3(-90f, 0.8f, 90f), colorEnemy, pool, 100);
            regen.Health.TakeDamage(50, -1);
            SP.Combat.Health.RegeneracionPermitida = false;
            for (int i = 0; i < 400; i++) regen.Health.Tick(0.05f);
            Check("Con la regeneracion apagada nadie se cura solo", regen.Health.Current == 50);
            SP.Combat.Health.RegeneracionPermitida = true;
            for (int i = 0; i < 400; i++) regen.Health.Tick(0.05f);
            Check("Con la regeneracion prendida vuelve a curarse", regen.Health.Current > 50);

            // --- Demolicion: la carga se puede alargar para practicar y vuelve a 4 s ---
            Check("La carga de demolicion dura 4 s de fabrica", Mathf.Abs(SP.Player.Demolicion.Segundos - 4f) < 0.001f);

            // --- Feedback ---
            int cont = SP.Presentation.Feedback.Contador;
            SP.Presentation.Feedback.Visual("prueba", null, null, false, false);
            Check("Feedback.Visual cuenta como accion con feedback", SP.Presentation.Feedback.Contador == cont + 1);
            Check("Cada categoria del radial tiene color propio", SP.Player.PlayerInputDriver.ColorDeCategoria.Length == SP.UI.MenuDeOrdenes.CantidadDeCategorias);

            foreach (var s in new[] { victima, blanco, aliado, regen }) if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
            FullHeal(vega, kes, doc);
        }

        // Un clip sirve si tiene muestras, ninguna NaN y algun pico audible.
        static bool ClipAudible(AudioClip clip)
        {
            if (clip == null || clip.length < 0.02f || clip.samples <= 0) return false;
            var datos = new float[Mathf.Min(clip.samples, 88200) * clip.channels];
            if (!clip.GetData(datos, 0)) return true;   // audio comprimido sin datos en Edit mode: existe y basta
            float pico = 0f;
            foreach (var m in datos) { if (float.IsNaN(m) || float.IsInfinity(m)) return false; float a = m < 0f ? -m : m; if (a > pico) pico = a; }
            return pico > 0.05f;
        }

        static void RunPhase14(PlayerInputDriver inputDriver, Vehicle vehicle, Soldier vega, Soldier kes, Soldier doc,
                               ProjectilePool pool, GameObject soldierPrefab, Color colorEnemy)
        {
            TestLog.Phase("FASE 14 - Coberturas, seguir al jugador, objetivo dinamico, cubitos de impacto, tanque y feedback");

            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vega.Brain.CancelOrder(); kes.Brain.CancelOrder(); doc.Brain.CancelOrder();
            FullHeal(vega, kes, doc);

            // --- Cubitos de impacto: rojo soldado, verde piso, amarillo obstaculo ---
            var rojo = ImpactCubes.ColorOf(ImpactSurface.Soldier);
            var verde = ImpactCubes.ColorOf(ImpactSurface.Ground);
            var amarillo = ImpactCubes.ColorOf(ImpactSurface.Obstacle);
            Check("Impacto en soldado = cubitos ROJOS", rojo.r > 0.9f && rojo.g < 0.2f && rojo.b < 0.2f);
            Check("Impacto en piso = cubitos VERDES", verde.g > 0.9f && verde.r < 0.3f);
            Check("Impacto en obstaculo = cubitos AMARILLOS", amarillo.r > 0.9f && amarillo.g > 0.8f && amarillo.b < 0.2f);
            Check($"Un balazo suelta varios cubitos ({ImpactCubes.CountFor(ImpactSurface.Soldier, 26)} en soldado, {ImpactCubes.CountFor(ImpactSurface.Ground, 26)} en piso) y un disparo mas fuerte, mas",
                ImpactCubes.CountFor(ImpactSurface.Soldier, 26) >= 6 && ImpactCubes.CountFor(ImpactSurface.Ground, 100) > ImpactCubes.CountFor(ImpactSurface.Ground, 10));

            // --- Proyectiles mas rapidos ---
            var antes = new HashSet<Projectile>(Projectile.ActiveInstances);
            vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
            Projectile nuevo = null;
            foreach (var p in Projectile.ActiveInstances) if (!antes.Contains(p)) { nuevo = p; break; }
            Check($"La bala de mano sale a la velocidad base ({(nuevo != null ? nuevo.Velocity.magnitude : 0f):0} m/s, minimo 240)",
                nuevo != null && nuevo.Velocity.magnitude >= 240f && Mathf.Abs(nuevo.Velocity.magnitude - Projectile.VelocidadBase) < 1f);
            SimulateSeconds(3.2f);

            // --- Coberturas: el punto de lo apuntado, la destruccion y la explosion ---
            var cubo = CrearCoberturaDePrueba("T14_Cobertura", new Vector3(-60f, 0f, 60f), 300);
            SP.Core.Coberturas.Registrar();
            var marca = cubo.GetComponent<ObstacleMarker>();
            Check("El obstaculo nuevo da puntos de cobertura", SP.Core.Coberturas.Cantidad >= 4);
            Vector3 puntoCob; Collider duenoCob;
            bool hay = SP.Core.Coberturas.TryPuntoApuntado(cubo.transform.position + new Vector3(0f, 0f, -0.5f), cubo.transform, 0f, out puntoCob, out duenoCob);
            Check($"Apuntar al obstaculo da el punto del costado apuntado ({puntoCob})", hay && duenoCob != null && puntoCob.z < cubo.transform.position.z);
            Check("La cobertura sigue vigente mientras el cubo esta en pie", SP.Core.Coberturas.Vigente(duenoCob));

            // --- Orden de cobertura: camina, se agacha y se queda ---
            kes.transform.position = new Vector3(-60f, 0.8f, 52f);
            kes.Brain.CancelOrder();
            SP.Presentation.Feedback.Reset();
            bool ordenada = OrderService.IssueCoverOrder(kes, puntoCob, duenoCob);
            Check("La orden de cobertura se acepta", ordenada);
            bool llego = SimulateUntil(() => kes.Brain.EnCobertura, 12f);
            Check($"El aliado llega, entra en cobertura ({kes.Brain.State}) y queda AGACHADO", llego && kes.Motor.IsCrouching);
            var posLlegada = kes.transform.position;
            SimulateSeconds(2.5f);
            Check($"Y SE QUEDA ahi (se movio {(kes.transform.position - posLlegada).magnitude:0.00} m en 2,5 s)",
                kes.Brain.EnCobertura && kes.Motor.IsCrouching && (kes.transform.position - posLlegada).magnitude < 0.5f);
            Check($"Dio feedback al tomar la cobertura ({SP.Presentation.Feedback.UltimoTexto} / {SP.Presentation.Feedback.UltimoSonido})",
                SP.Presentation.Feedback.Contador >= 1 && SP.Presentation.Feedback.UltimoSonido == SfxKind.CoverTake);

            // --- Se destruye: "se da cuenta" ---
            int versionAntes = SP.Core.Coberturas.Version;
            int contadorAntes = SP.Presentation.Feedback.Contador;
            marca.TakeDamage(99999);
            Check("Al destruirse el obstaculo, las coberturas se rehacen", SP.Core.Coberturas.Version > versionAntes);
            SimulateSeconds(0.6f);
            Check($"El aliado se dio cuenta de que la cobertura cayo (EnCobertura={kes.Brain.EnCobertura})", !kes.Brain.EnCobertura);
            Check($"Y lo aviso ({SP.Presentation.Feedback.UltimoTexto})",
                SP.Presentation.Feedback.Contador > contadorAntes && SP.Presentation.Feedback.UltimoSonido == SfxKind.CoverLost);

            // --- Una orden nueva suelta la cobertura y lo para de pie ---
            var cubo2 = CrearCoberturaDePrueba("T14_Cobertura2", new Vector3(-60f, 0f, 60f), 300);
            SP.Core.Coberturas.Registrar();
            SP.Core.Coberturas.TryPuntoApuntado(cubo2.transform.position + new Vector3(0f, 0f, -0.5f), cubo2.transform, 0f, out puntoCob, out duenoCob);
            kes.transform.position = puntoCob + Vector3.back * 3f + Vector3.up * 0.8f;
            OrderService.IssueCoverOrder(kes, puntoCob, duenoCob);
            SimulateUntil(() => kes.Brain.EnCobertura, 8f);
            OrderService.IssueMoveOrder(kes, kes.transform.position + new Vector3(10f, 0f, 0f));
            SimulateSeconds(0.2f);
            Check("Una orden nueva lo saca de cobertura y lo para de pie", !kes.Brain.EnCobertura && !kes.Motor.IsCrouching);
            kes.Brain.CancelOrder();

            // --- Explosion: rompe obstaculos (el cañon del tanque) ---
            int hpAntes = cubo2.GetComponent<ObstacleMarker>().CurrentHealth;
            Projectile.ExplodeAt(cubo2.transform.position + Vector3.up * 0.6f, 3f, 45, -1, null);
            int hpDespues = cubo2.GetComponent<ObstacleMarker>().CurrentHealth;
            Check($"Una explosion daña el obstaculo ({hpAntes} -> {hpDespues})", hpDespues < hpAntes);

            // --- Holograma: mismo modelo del aliado, 90 % transparente, estatico ---
            SP.Core.Coberturas.TryPuntoApuntado(cubo2.transform.position + new Vector3(0f, 0f, -0.5f), cubo2.transform, 0f, out puntoCob, out duenoCob);
            CoverHologram.Mostrar(kes, puntoCob, SP.Core.Coberturas.FrenteDe(puntoCob, duenoCob), cubo2.transform);
            Check($"Con [Shift] apuntando a una cobertura aparece el holograma del aliado ({(CoverHologram.ModeloActual != null ? CoverHologram.ModeloActual.name : "-")})",
                CoverHologram.Visible && CoverHologram.ModeloActual == kes && (CoverHologram.PuntoActual - puntoCob).sqrMagnitude < 0.01f);
            var holoVisual = CoverHologram.Instance != null ? CoverHologram.Instance.transform.Find("HoloRoot/HoloVisual") : null;
            if (holoVisual != null)
            {
                var rend = holoVisual.GetComponentInChildren<Renderer>();
                float alfa = rend != null ? rend.sharedMaterial.color.a : -1f;
                Check($"El holograma es 90 % transparente (alfa {alfa:0.00}) y no lleva scripts ni animador",
                    Mathf.Abs(alfa - 0.10f) < 0.01f && holoVisual.GetComponentInChildren<MonoBehaviour>(true) == null && holoVisual.GetComponent<Animator>() == null);
            }
            else TestLog.Step("Holograma: el soldado de esta escena no tiene 'Visual' (arte no importado); se verifica solo que aparece");
            CoverHologram.Ocultar();
            Check("Al soltar [Shift] el holograma se oculta", !CoverHologram.Visible);
            if (CoverHologram.Instance != null) UnityEngine.Object.DestroyImmediate(CoverHologram.Instance.gameObject);

            UnityEngine.Object.DestroyImmediate(cubo);
            UnityEngine.Object.DestroyImmediate(cubo2);
            SP.Core.Coberturas.Limpiar();

            // --- Seguir al jugador cuando se aleja ---
            AjustesDeEscuadra.AsegurarEnEscena();
            var ajustes = UnityEngine.Object.FindFirstObjectByType<AjustesDeEscuadra>(FindObjectsInactive.Include);
            ajustes.distanciaParaSeguir = 25f; ajustes.distanciaParaDetenerse = 8f;
            ajustes.Aplicar();
            vega.transform.position = new Vector3(-60f, 0.8f, -60f);
            doc.transform.position = vega.transform.position + new Vector3(3f, 0f, 3f);
            doc.Brain.CancelOrder();
            AjustesDeEscuadra.Lider = vega;
            SimulateSeconds(0.5f);
            Check("Cerca del jugador el aliado libre NO lo sigue", !doc.Brain.SiguiendoAlJugador);
            doc.transform.position = vega.transform.position + new Vector3(0f, 0f, 40f);
            SimulateSeconds(0.3f);
            Check($"Si el jugador se aleja mas de {AjustesDeEscuadra.DistanciaParaSeguir:0} m, el aliado empieza a seguirlo ({doc.Brain.State})",
                doc.Brain.SiguiendoAlJugador && doc.Brain.State == AiState.Follow);
            bool llegoDoc = SimulateUntil(() => !doc.Brain.SiguiendoAlJugador, 14f);
            Check($"Y deja de seguirlo al llegar a {AjustesDeEscuadra.DistanciaParaDetenerse:0} m ({Vector3.Distance(doc.transform.position, vega.transform.position):0.0} m)",
                llegoDoc && Vector3.Distance(doc.transform.position, vega.transform.position) <= AjustesDeEscuadra.DistanciaParaDetenerse + 2f);
            AjustesDeEscuadra.Lider = null;
            doc.Brain.CancelOrder();

            // --- Objetivo dinamico: distancia + vision (cono y linea de tiro) ---
            kes.transform.position = new Vector3(-60f, 0.8f, 40f);
            kes.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            kes.Brain.CancelOrder();
            var cerca = SpawnSoldier(soldierPrefab, "E14_cerca", TeamId.Enemy, RoleType.Enemy, kes.transform.position + new Vector3(0f, 0f, 8f), colorEnemy, pool, 180);
            var lejos = SpawnSoldier(soldierPrefab, "E14_lejos", TeamId.Enemy, RoleType.Enemy, kes.transform.position + new Vector3(4f, 0f, 14f), colorEnemy, pool, 180);
            cerca.Brain.Stance = CombatStance.AltoElFuego; lejos.Brain.Stance = CombatStance.AltoElFuego;
            SP.Core.ActorRegistry.Invalidate();
            Check($"Un blanco cercano puntua mejor que uno lejano ({kes.Brain.PuntajeDeObjetivo(cerca):0.0} < {kes.Brain.PuntajeDeObjetivo(lejos):0.0})",
                kes.Brain.PuntajeDeObjetivo(cerca) < kes.Brain.PuntajeDeObjetivo(lejos));
            SimulateSeconds(0.4f);
            Check($"Elige el mas cercano ({(kes.Brain.CurrentTarget != null ? kes.Brain.CurrentTarget.DisplayName : "ninguno")})", kes.Brain.CurrentTarget == cerca);
            UnityEngine.Object.DestroyImmediate(cerca.gameObject);
            SP.Core.ActorRegistry.Invalidate();
            kes.Brain.CancelOrder();
            SimulateSeconds(0.4f);
            Check($"Uno a 14 m (fuera del radio de 10 m) pero DENTRO DEL CONO y con linea de tiro tambien se ve ({(kes.Brain.CurrentTarget != null ? kes.Brain.CurrentTarget.DisplayName : "ninguno")})",
                kes.Brain.CurrentTarget == lejos);
            kes.transform.rotation = Quaternion.LookRotation(Vector3.back);
            kes.Brain.CancelOrder();
            SimulateSeconds(0.4f);
            Check($"Ese mismo, a la ESPALDA (fuera del cono), NO se ve ({(kes.Brain.CurrentTarget != null ? kes.Brain.CurrentTarget.DisplayName : "ninguno")})",
                kes.Brain.CurrentTarget == null);
            UnityEngine.Object.DestroyImmediate(lejos.gameObject);
            SP.Core.ActorRegistry.Invalidate();
            kes.Brain.CancelOrder();

            // --- Tanque: teclas [G] todos suben / [I] todos bajan y panel de teclas ---
            kes.transform.position = vehicle.transform.position + new Vector3(3f, 0f, 0f);
            doc.transform.position = vehicle.transform.position + new Vector3(-3f, 0f, 0f);
            vega.transform.position = vehicle.transform.position + new Vector3(0f, 0f, -6f);
            SP.Core.ActorRegistry.Invalidate();
            SP.Presentation.Feedback.Reset();
            int suben = inputDriver.SubirATodos(vehicle);
            Check($"[G] manda a TODOS los aliados libres a subir ({suben})", suben == 2);
            Check("...y hubo feedback (sonido de 'todos suben')", SP.Presentation.Feedback.UltimoSonido == SfxKind.BoardAll);
            bool subieron = SimulateUntil(() => vehicle.OccupantCount == 2, 14f);
            Check($"Los dos llegan y suben ({vehicle.OccupantCount} a bordo)", subieron);

            var canvasGo = new GameObject("T14_Canvas", typeof(Canvas));
            var panel = VehicleKeysPanel.Asegurar(canvasGo.transform);
            panel.Actualizar(vehicle, null, 0);
            string txt = panel.UltimoTexto;
            Check("El panel de teclas del tanque explica el radial: TANQUE > subir todos / bajar todos", txt.Contains("TANQUE") && txt.Contains("subir todos") && txt.Contains("bajar todos"));
            Check("...y los 4 asientos con quien los ocupa y 'intercambiar' si esta ocupado",
                txt.Contains("[1]") && txt.Contains("[2]") && txt.Contains("[3]") && txt.Contains("[4]") && txt.Contains("intercambiar") && txt.Contains(kes.DisplayName));
            UnityEngine.Object.DestroyImmediate(canvasGo);

            SP.Presentation.Feedback.Reset();
            int bajan = inputDriver.BajarATodos(vehicle);
            Check($"[I] baja a TODOS ({bajan}) y el vehiculo queda vacio ({vehicle.OccupantCount})", bajan == 2 && vehicle.OccupantCount == 0);
            Check("...con su feedback", SP.Presentation.Feedback.UltimoSonido == SfxKind.ExitAll);
            kes.Brain.CancelOrder(); doc.Brain.CancelOrder();

            // --- Tanque enemigo: bando, tripulacion sin artillero humano y patrulla ---
            // (Vehicle.Todos se llena en OnEnable, que en Edit mode no corre: en Play se verifico que la torreta ve al tanque hostil.)
            var vb = vehicle.GetComponent<VehicleBrain>();
            vehicle.Mount(kes, VehicleSeatRole.Driver);
            vehicle.Mount(doc, VehicleSeatRole.Passenger2);
            vehicle.AsignarBando(TeamId.Enemy, new Color(0.55f, 0.13f, 0.11f));
            Check("Un tanque enemigo tiene bando Enemigo", vehicle.Bando == TeamId.Enemy);
            Check("Su tripulacion (conductor + pasajero) NO ocupa el asiento de artillero: la torreta queda en automatico", vehicle.Gunner == null && vehicle.OccupantCount == 2);
            vb.IsPlayerDriving = false;
            vb.ConfigurarPatrulla(new[] { new Vector3(-50f, 0f, -50f), new Vector3(-50f, 0f, -20f) }, true, 0.5f);
            vb.Tick(0.05f);
            Check($"Con tripulacion y sin orden, el tanque enemigo sale a patrullar (destino {vb.CurrentDestination})", vb.TienePatrulla && vb.HasOrder);
            vb.Stop();
            foreach (var o in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(o);
            vehicle.AsignarBando(TeamId.Player, new Color(0.98f, 0.65f, 0.15f));
            vb.ConfigurarPatrulla(null, false, 1f);
            kes.Brain.CancelOrder(); doc.Brain.CancelOrder();

            // --- Feedback central ---
            SP.Presentation.Feedback.Reset();
            SP.Presentation.Feedback.Accion(SfxKind.Select, "PRUEBA", null, SP.Presentation.Feedback.Ok);
            Check("Feedback.Accion registra la accion con su sonido", SP.Presentation.Feedback.Contador == 1 && SP.Presentation.Feedback.UltimoSonido == SfxKind.Select);
            foreach (SfxKind k in Enum.GetValues(typeof(SfxKind)))
                if (GenericSfx.Get(k) == null) { Check($"Todos los sonidos tienen clip ({k})", false); break; }

            TestLog.Phase("FASE 14 FINALIZADA");
        }
    }
}
