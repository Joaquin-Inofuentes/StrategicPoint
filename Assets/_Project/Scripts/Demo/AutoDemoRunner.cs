using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using SP.Core;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.CameraSystem;
using SP.Vehicles;
using SP.Player;

namespace SP.Presentation
{
    // Corre las 4 fases del guion de prueba en Play mode real (no en Edit
    // mode como HeadlessTestRunner): usa las mismas APIs públicas que
    // dispararía el jugador con teclado/mouse, esperando en tiempo real
    // entre pasos, sacando una captura de pantalla por paso y logueando
    // todo con TestLog. Pensado para verificar que el flujo entero anda
    // en una corrida de Play mode de verdad, no solo en la simulación.
    //
    // Desactivable: destildar autoPlayOnStart en el inspector antes de
    // darle Play, o apretar F9 durante el juego (arranca/corta la demo).
    public class AutoDemoRunner : MonoBehaviour
    {
        // Por defecto NO arranca sola: el usuario le da Play para probar el
        // juego a mano tranquilo, sin que la demo le agarre el control. Para
        // correrla, un comando desde afuera llama a StartDemo() (o el propio
        // usuario aprieta F9 si quiere verla).
        [SerializeField] bool autoPlayOnStart = false;

        public PlayerBrain Brain;
        public CameraRig Rig;
        public SelectionController Selection;
        public AimTargeting Aim;
        public PlayerInputDriver InputDriver;
        public List<Soldier> Squad;
        public Vehicle DemoVehicle;
        public List<WeaponPickup> WeaponPickups;
        public Soldier DemoEnemy;
        public List<Soldier> PatrolEnemies;

        public bool IsRunning { get; private set; }

        int stepCounter;
        Coroutine running;

        void Start()
        {
            if (autoPlayOnStart) StartDemo();
        }

        void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (IsRunning) StopDemo();
                else StartDemo();
            }
        }

        public void StartDemo()
        {
            if (IsRunning) return;
            running = StartCoroutine(DemoSequence());
        }

        public void StopDemo()
        {
            if (running != null) StopCoroutine(running);
            IsRunning = false;
            TestLog.Warn("Demo automatico detenido a mano (F9).");
        }

        // Holgura entre pasos: a propósito lenta, para que se pueda ver
        // (a modo de benchmark) cómo se prueba cada funcionalidad en vez de
        // que pase todo en un parpadeo. Ajustable en el inspector.
        [SerializeField] float stepGap = 1.2f;

        static void FullHeal(params Soldier[] soldiers)
        {
            foreach (var s in soldiers) s.Health.Initialize(s.Id, s.Health.MaxHealth);
        }

        IEnumerator CaptureStep(string stepName)
        {
            // Ojo: WaitForSecondsRealtime (no WaitForSeconds), porque los
            // pasos de disparo de armas congelan Time.timeScale=0 para que
            // el proyectil se vea quieto en la foto — con tiempo escalado
            // esta espera nunca terminaría.
            //
            // Un par de frames de margen antes de sacar la foto: si algo
            // (un shader nuevo, un cambio de camara) recién se disparó este
            // mismo frame, evita capturar el frame trabado/negro de la
            // transición.
            yield return new WaitForSecondsRealtime(0.2f);
            yield return null;

            // A pesar del margen de arriba, una fracción de los pasos
            // salía DIRECTAMENTE EN NEGRO (mismo tamaño de archivo exacto,
            // el mismo frame vacío) — pasa cuando el Editor no está
            // enfocado y el Game View no repinta a tiempo entre ticks. No
            // es determinístico (le pasa a distintos pasos en distintas
            // corridas), así que en vez de adivinar cuánto esperar, se
            // captura a una textura, se mide si de verdad quedó negra, y
            // si es así se repinta a mano y se reintenta antes de escribir
            // el archivo — mucho más confiable que una espera fija.
            string dir = Path.Combine(Application.dataPath, "..", "DemoCaptures");
            Directory.CreateDirectory(dir);
            string fileName = $"{stepCounter:00}_{stepName}.png";
            string path = Path.Combine(dir, fileName);

            Texture2D shot = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                yield return new WaitForEndOfFrame();
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (!IsBlack(shot)) break;

                UnityEngine.Object.Destroy(shot);
                shot = null;
#if UNITY_EDITOR
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
                yield return new WaitForSecondsRealtime(0.25f);
                yield return null;
            }

            if (shot != null)
            {
                File.WriteAllBytes(path, shot.EncodeToPNG());
                UnityEngine.Object.Destroy(shot);
            }

            stepCounter++;
            TestLog.Step($"Captura: {fileName}");
            yield return new WaitForSecondsRealtime(stepGap);
        }

        // Frame realmente negro (pantalla sin repintar): revisa una
        // muestra de píxeles en vez de todos, es una foto de 1920x1080 y
        // esto corre en medio del demo a cada paso.
        static bool IsBlack(Texture2D tex)
        {
            if (tex == null) return true;
            for (int i = 0; i < 9; i++)
            {
                int x = (tex.width / 3) * (i % 3) + tex.width / 6;
                int y = (tex.height / 3) * (i / 3) + tex.height / 6;
                if (tex.GetPixel(x, y).maxColorComponent > 0.02f) return false;
            }
            return true;
        }

        // Mensaje de tutorial + log + espera corta, todo junto: así cada
        // paso queda marcado en el texto de pantalla (bottom-center) y en
        // la consola con el mismo contenido. La espera es la holgura para
        // poder leer el mensaje antes de que pase al siguiente paso.
        // Realtime por la misma razón que CaptureStep.
        IEnumerator Tutorial(string message, float hold = 2.2f)
        {
            InputDriver.ShowTutorialMessage(message, hold);
            TestLog.Step(message);
            yield return new WaitForSecondsRealtime(stepGap);
        }

        IEnumerator DemoSequence()
        {
            IsRunning = true;
            stepCounter = 0;
            TestLog.Begin();
            TestLog.Phase("DEMO AUTOMATICO EN PLAY MODE - INICIO");

            var vega = Squad[0];
            var kes = Squad[1];
            var doc = Squad[2];

            // ============================================================
            // FASE 1 - Combate FPS básico
            // ============================================================
            TestLog.Phase("FASE 1 - Combate FPS basico");

            Brain.Possess(vega);
            Rig.SetMode(ControlMode.Fps);
            Rig.FollowFps(vega);
            yield return Tutorial("FASE 1: Movete con [WASD] y dispara con [Click] para eliminar al enemigo.");
            yield return CaptureStep("fase1_inicio");

            Vector3 startPos = vega.transform.position;
            float t = 0f;
            while (t < 0.6f)
            {
                Brain.Move(vega.transform.forward, Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }
            TestLog.Step($"{vega.DisplayName} se movio {Vector3.Distance(startPos, vega.transform.position):0.00} m con WASD");
            yield return CaptureStep("fase1_movimiento");

            bool fired = Brain.Fire();
            TestLog.Step($"Disparo de {vega.DisplayName}: {(fired ? "proyectil creado" : "fallo")}");
            yield return CaptureStep("fase1_disparo");

            if (DemoEnemy != null)
            {
                DemoEnemy.gameObject.SetActive(true);
                DemoEnemy.transform.position = vega.transform.position + vega.transform.forward * 12f + Vector3.up * 0.8f;
                DemoEnemy.transform.rotation = Quaternion.LookRotation(-vega.transform.forward);
                vega.transform.rotation = Quaternion.LookRotation((DemoEnemy.transform.position - vega.transform.position).normalized);
                Rig.FollowFps(vega);
                yield return Tutorial($"Aparecio {DemoEnemy.DisplayName}: seguí disparando hasta eliminarlo.");
                yield return CaptureStep("fase1_aparece_enemigo");

                int enemyHpBefore = DemoEnemy.Health.Current;
                Brain.Fire();
                float waitT = 0f;
                while (DemoEnemy.Health.Current == enemyHpBefore && DemoEnemy.Health.IsAlive && waitT < 2f)
                {
                    waitT += Time.deltaTime;
                    yield return null;
                }
                TestLog.Step($"{DemoEnemy.DisplayName} recibio dano ({DemoEnemy.Health.Current}/{DemoEnemy.Health.MaxHealth} vida)");
                yield return CaptureStep("fase1_impacto");

                float combatTimeout = 10f;
                while (DemoEnemy.Health.IsAlive && vega.Health.IsAlive && combatTimeout > 0f)
                {
                    // El jugador sigue disparando mientras la IA enemiga se acerca y contraataca sola.
                    vega.transform.rotation = Quaternion.LookRotation((DemoEnemy.transform.position - vega.transform.position).normalized);
                    Rig.FollowFps(vega);
                    Brain.Fire();
                    combatTimeout -= 0.4f;
                    yield return new WaitForSeconds(0.4f);
                }
                TestLog.Step(!DemoEnemy.Health.IsAlive
                    ? $"{vega.DisplayName} elimino a {DemoEnemy.DisplayName}"
                    : $"Combate cortado por tiempo (vida enemigo {DemoEnemy.Health.Current})");
                yield return CaptureStep("fase1_resultado_combate");
            }

            // ============================================================
            // FASE 2 - Posesion, camara y ordenes
            // ============================================================
            TestLog.Phase("FASE 2 - Posesion, camara y ordenes");
            yield return Tutorial($"FASE 2: Apuntale a {kes.DisplayName} y apreta [F] para poseerla (la camara se desliza sola hacia ella).");

            // Apuntale primero (para que resalte de color, como pide el
            // resaltado de apuntado) y recién ahí "aprieta F": la cámara no
            // salta de golpe, hace un lerp corto hacia el nuevo cuerpo.
            vega.transform.rotation = Quaternion.LookRotation((kes.transform.position - vega.transform.position).normalized);
            Rig.FollowFps(vega);
            yield return null; // deja que Update() calcule el resaltado de apuntado
            TestLog.Step($"Apuntando a {kes.DisplayName}: deberia resaltar de color mas claro");
            yield return CaptureStep("fase2_resaltado_antes_de_poseer");

            PossessionService.Swap(Brain, kes);
            Rig.BeginTransition(kes.EyeAnchor != null ? kes.EyeAnchor : kes.transform);
            yield return new WaitForSeconds(0.15f);
            TestLog.Step("Camara a mitad de camino (lerp en curso, todavia no llego)");
            yield return CaptureStep("fase2_camara_lerp_a_mitad");

            while (Rig.IsTransitioning) yield return null;
            TestLog.Step($"Se cambio de soldado poseido a {kes.DisplayName}: la camara ya llego");
            yield return CaptureStep("fase2_posesion");

            Vector3 kesStart = kes.transform.position;
            t = 0f;
            while (t < 0.4f) { Brain.Move(kes.transform.forward, Time.deltaTime); t += Time.deltaTime; yield return null; }
            TestLog.Step($"{kes.DisplayName} se movio {Vector3.Distance(kesStart, kes.transform.position):0.00} m");
            yield return CaptureStep("fase2_movimiento");

            yield return Tutorial("Se le da a Vega la orden de ir a un punto: aparece un cilindro verde que se achica en el destino.");
            OrderService.IssueMoveOrder(vega, kes.transform.position + new Vector3(3f, 0f, 3f));
            yield return CaptureStep("fase2_orden_marcador_verde");

            // ============================================================
            // FASE 3 - Vista RTS y seleccion multiple
            // ============================================================
            TestLog.Phase("FASE 3 - Vista RTS y seleccion multiple");
            yield return Tutorial("FASE 3: [TAB] cambia a vista RTS. Arrastra el mouse para seleccionar varios soldados.");

            Rig.SetMode(ControlMode.Rts);
            Rig.SetRtsView(kes.transform.position);
            yield return CaptureStep("fase3_vista_rts");

            Selection.SelectSingle(vega);
            Selection.AddToSelection(kes);
            Selection.AddToSelection(doc);
            TestLog.Step($"Se seleccionaron {Selection.Selected.Count} soldados (arrastre tipo Age of Empires)");
            yield return CaptureStep("fase3_seleccion_multiple");

            yield return Tutorial("Orden grupal de movimiento: cilindro verde (mover) en el punto de destino.");
            Vector3 rallyPoint = kes.transform.position + new Vector3(6f, 0f, -6f);
            OrderService.IssueMoveOrderForSelection(Selection.Selected, rallyPoint);
            yield return CaptureStep("fase3_orden_grupal");
            yield return new WaitForSeconds(1.2f);

            Rig.SetMode(ControlMode.Fps);
            PossessionService.Swap(Brain, vega);
            Rig.FollowFps(vega);
            TestLog.Step("Volvio a vista FPS, poseyendo de nuevo a Vega");
            yield return CaptureStep("fase3_vuelta_fps");

            // ============================================================
            // FASE 4 - Vehiculo, torreta y armas
            // ============================================================
            TestLog.Phase("FASE 4 - Vehiculo, torreta y armas");

            foreach (var occupant in new List<Soldier>(DemoVehicle.Occupants)) DemoVehicle.Dismount(occupant);

            DemoVehicle.transform.position = new Vector3(6f, 0.6f, -14f);
            DemoVehicle.transform.rotation = Quaternion.identity;
            vega.transform.position = DemoVehicle.transform.position + new Vector3(-2.6f, 0f, 0f);
            vega.transform.rotation = Quaternion.LookRotation(Vector3.right);
            kes.transform.position = DemoVehicle.transform.position + new Vector3(-2.6f, 0f, 1.5f);
            doc.transform.position = DemoVehicle.transform.position + new Vector3(30f, 0f, 30f); // lejos: no debe subir sola
            Rig.FollowFps(vega);

            // Con la camioneta vacia, la orden de ir sola a un punto debe
            // ser RECHAZADA: no hay ningun aliado tuyo manejando adentro.
            bool deniedNoDriver = InputDriver.TryIssueVehicleMoveOrder(DemoVehicle.transform.position + Vector3.right * 10f);
            TestLog.Step($"Orden a camioneta VACIA: {(deniedNoDriver ? "ACEPTADA (mal, no deberia)" : "rechazada correctamente, no hay conductor")}");

            yield return Tutorial("FASE 4: Acercate a la camioneta y apreta [E] para subir (se suben los aliados cercanos solos).");
            yield return CaptureStep("fase4_acercandose");

            InputDriver.EnterVehicle(DemoVehicle);
            TestLog.Step($"Vega subio de conductor. Ocupantes: {DemoVehicle.OccupantCount}/{DemoVehicle.Capacity} (Kes subio sola: {(DemoVehicle.RoleOf(kes) != null)}, Doc lejos no subio: {DemoVehicle.RoleOf(doc) == null})");
            yield return CaptureStep("fase4_montado_camara_lerp");
            while (Rig.IsTransitioning) yield return null;
            yield return CaptureStep("fase4_montado");

            var motor = DemoVehicle.GetComponent<VehicleMotor>();
            var vBrain = DemoVehicle.GetComponent<VehicleBrain>();
            var turret = DemoVehicle.GetComponentInChildren<TurretWeapon>();

            vBrain.IsPlayerDriving = true;
            InputDriver.ToggleVehicleCameraView(); // 3ra persona, para que se vea el vehiculo andando
            yield return Tutorial("Mantene [W] para acelerar. [G] frena. [2] pasa a la torreta.");
            float driveT = 0f;
            while (driveT < 1.2f) { motor.Drive(1f, 0f, Time.deltaTime); driveT += Time.deltaTime; yield return null; }
            TestLog.Step($"La camioneta acelero a {motor.CurrentSpeed:0.0} u/s");
            yield return CaptureStep("fase4_conduciendo");

            float brakeT = 0f;
            while (brakeT < 1.5f && !motor.IsStopped) { motor.Brake(Time.deltaTime); brakeT += Time.deltaTime; yield return null; }
            TestLog.Step($"Freno con G hasta velocidad {motor.CurrentSpeed:0.00}");
            yield return CaptureStep("fase4_frenado");

            // Con Vega todavia de conductor, la orden debe ser ACEPTADA: usa
            // la misma puerta pública que usaria el click derecho real, para
            // probar de verdad el arreglo pedido ("solo si hay alguien tuyo adentro").
            yield return Tutorial("Click derecho: se manda la camioneta sola a un punto (cilindro verde) -- hay conductor adentro.");
            Vector3 dest = DemoVehicle.transform.position + new Vector3(14f, 0f, 4f);
            bool orderAccepted = InputDriver.TryIssueVehicleMoveOrder(dest);
            TestLog.Step($"Orden con conductor presente ({DemoVehicle.Driver?.DisplayName}): {(orderAccepted ? "aceptada" : "RECHAZADA (mal, deberia aceptarse)")}");
            yield return CaptureStep("fase4_orden_camioneta_marcador");

            float orderTimeout = 15f;
            while (vBrain.HasOrder && orderTimeout > 0f) { orderTimeout -= Time.deltaTime; yield return null; }
            TestLog.Step($"La camioneta {(vBrain.HasOrder ? "no llego a tiempo" : "llego sola")} (distancia final {Vector3.Distance(DemoVehicle.transform.position, dest):0.00} m)");
            yield return CaptureStep("fase4_orden_camioneta_llegada");

            InputDriver.SwitchSeat(VehicleSeatRole.Gunner);
            TestLog.Step($"Vega aprieta 2: paso a artillero (asiento actual: {DemoVehicle.RoleOf(vega)})");
            yield return CaptureStep("fase4_torreta");

            bool turretFired = turret.TryFire();
            TestLog.Step($"Disparo de torreta: {(turretFired ? "proyectil creado" : "fallo")}");
            yield return CaptureStep("fase4_torreta_disparo");

            InputDriver.ExitVehicle();
            Rig.FollowFps(vega);
            TestLog.Step("Vega bajo de la camioneta (E)");
            yield return CaptureStep("fase4_bajado");

            // --- Armas recogibles ---
            yield return Tutorial("Acercate a cada arma tirada y apreta [E] para equiparla.");
            foreach (var pickup in WeaponPickups)
            {
                vega.transform.position = pickup.transform.position + new Vector3(0f, 0.4f, 1.8f);
                vega.transform.rotation = Quaternion.LookRotation(Vector3.back);
                Rig.FollowFps(vega);
                yield return new WaitForSeconds(0.15f);
                TestLog.Step($"Vega se acerca al arma {pickup.Kind} (color {pickup.Color})");
                yield return CaptureStep($"fase4_arma_{pickup.Kind}_prompt");

                pickup.EquipOn(vega.Weapon, vega.Id);
                Time.timeScale = 0f;
                bool weaponFired = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
                if (weaponFired && Projectile.ActiveInstances.Count > 0)
                {
                    var proj = Projectile.ActiveInstances[Projectile.ActiveInstances.Count - 1];
                    for (int i = 0; i < 5; i++) proj.Tick(0.03f);
                }
                TestLog.Step($"Arma {pickup.Kind} (color {pickup.Color}) equipada y disparada por {vega.DisplayName}: {weaponFired}");
                yield return CaptureStep($"fase4_arma_{pickup.Kind}_disparo");
                Time.timeScale = 1f;
            }

            // ============================================================
            // FASE 5 - Resaltado de apuntado, camara suave y armas 1/2/3
            // ============================================================
            TestLog.Phase("FASE 5 - Resaltado de apuntado y cambio de arma con 1/2/3");
            yield return Tutorial("FASE 5: al apuntar a un aliado o vehiculo, resalta de color. Probando teclas 1/2/3 para cambiar de arma.");

            vega.transform.position = DemoVehicle.transform.position + new Vector3(-4f, 0f, 0f);
            vega.transform.rotation = Quaternion.LookRotation((DemoVehicle.transform.position - vega.transform.position).normalized);
            Rig.FollowFps(vega);
            yield return null; // deja correr Update() para que calcule el resaltado
            TestLog.Step("Apuntando al vehiculo: deberia resaltar de color mas claro");
            yield return CaptureStep("fase5_resaltado_vehiculo");

            vega.transform.rotation = Quaternion.LookRotation((kes.transform.position - vega.transform.position).normalized);
            Rig.FollowFps(vega);
            yield return null;
            TestLog.Step($"Apuntando a {kes.DisplayName}: deberia resaltar de color mas claro");
            yield return CaptureStep("fase5_resaltado_aliado");

            var kinds = new[] { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Heavy };
            for (int i = 0; i < kinds.Length; i++)
            {
                InputDriver.EquipWeaponHotkey(kinds[i]);
                Time.timeScale = 0f;
                bool fired3 = vega.Weapon.TryFire(vega.transform.position, vega.transform.forward);
                if (fired3 && Projectile.ActiveInstances.Count > 0)
                {
                    var proj = Projectile.ActiveInstances[Projectile.ActiveInstances.Count - 1];
                    for (int k = 0; k < 5; k++) proj.Tick(0.03f);
                }
                TestLog.Step($"Tecla [{i + 1}]: arma {kinds[i]} equipada, color {vega.Weapon.CurrentWeaponKind}, disparo={fired3}");
                yield return CaptureStep($"fase5_tecla_{i + 1}_{kinds[i]}");
                Time.timeScale = 1f;
            }

            // Marcadores de orden de ataque y de subida (rojo y azul).
            if (DemoEnemy != null)
            {
                DemoEnemy.Health.Initialize(DemoEnemy.Id, DemoEnemy.Health.MaxHealth);
                DemoEnemy.transform.position = vega.transform.position + vega.transform.forward * 8f;
                DemoEnemy.gameObject.SetActive(true);
                yield return Tutorial("Orden de ataque: cilindro rojo sobre el enemigo senalado.");
                OrderService.IssueAttackOrder(doc, DemoEnemy);
                yield return CaptureStep("fase5_marcador_ataque_rojo");
                DemoEnemy.gameObject.SetActive(false);
            }

            yield return Tutorial("Orden de subir al vehiculo: cilindro azul sobre la camioneta.");
            OrderService.IssueMountOrder(doc, DemoVehicle);
            yield return CaptureStep("fase5_marcador_subir_azul");

            // ============================================================
            // FASE 6 - Patrulla en waypoints y vista RTS top-down
            // ============================================================
            TestLog.Phase("FASE 6 - Patrulla en waypoints y vista RTS");
            TestLog.Step("Para cambiar de soldado: apuntale a un aliado y apreta [F] (funciona igual en FPS y en RTS).");

            if (PatrolEnemies != null && PatrolEnemies.Count > 0)
            {
                Rig.SetMode(ControlMode.Rts);
                Rig.SetRtsView(PatrolEnemies[0].transform.position);
                yield return Tutorial("FASE 6: 4 enemigos patrullan en loop por waypoints fijos (lineas naranjas = su ronda).");
                yield return CaptureStep("fase6_patrulla_waypoints");

                Rig.SetMode(ControlMode.Fps);
                Rig.FollowFps(vega);
            }

            // ============================================================
            // FASE 7 - Paneles de info al apuntar, y flecha+lineas al vehiculo
            // ============================================================
            TestLog.Phase("FASE 7 - Info al apuntar (soldado/vehiculo) y flecha de montaje");

            // Kes seguia adentro (Passenger1) desde la Fase 4 -- hay que
            // bajarla antes de decir "vehiculo vacio", si no el panel de
            // asientos correctamente la muestra ocupada y desentona con el
            // tutorial (esto es lo que se veia "desfasado" en la Fase 7).
            foreach (var occupant in new List<Soldier>(DemoVehicle.Occupants)) DemoVehicle.Dismount(occupant);

            vega.transform.position = kes.transform.position + new Vector3(0f, 0f, -3f);
            vega.transform.rotation = Quaternion.LookRotation((kes.transform.position - vega.transform.position).normalized);
            Rig.FollowFps(vega);
            yield return null;
            yield return Tutorial($"Apuntando a {kes.DisplayName}: abajo se ve su vida, arma y especialidad.");
            yield return CaptureStep("fase7_info_soldado");

            DemoVehicle.transform.position = vega.transform.position + vega.transform.forward * 10f;
            kes.transform.position = DemoVehicle.transform.position + new Vector3(-3f, 0f, 2f);
            doc.transform.position = DemoVehicle.transform.position + new Vector3(3f, 0f, 2f);
            vega.transform.rotation = Quaternion.LookRotation((DemoVehicle.transform.position - vega.transform.position).normalized);
            Rig.FollowFps(vega);
            yield return null;
            yield return Tutorial("Apuntando al vehiculo vacio: flecha azul arriba + lineas a los aliados que subirian, y cuadrados de asiento (todos verdes = libres).");
            yield return CaptureStep("fase7_flecha_montaje_y_asientos_libres");

            // ============================================================
            // FASE 8 - [G] hace bajar a todos y cambia el color del vehiculo
            // ============================================================
            TestLog.Phase("FASE 8 - Bajar a todos con [G] y color del vehiculo segun ocupacion");

            InputDriver.EnterVehicle(DemoVehicle);
            while (Rig.IsTransitioning) yield return null;
            TestLog.Step($"Vega subio, {kes.DisplayName} y {doc.DisplayName} subieron solos. Ocupantes: {DemoVehicle.OccupantCount}/{DemoVehicle.Capacity}");
            yield return Tutorial("Camioneta ocupada: deberia verse mas oscura que antes.");
            yield return CaptureStep("fase8_vehiculo_ocupado_color");

            InputDriver.ExitVehicle();
            Rig.FollowFps(vega);
            yield return null;
            vega.transform.rotation = Quaternion.LookRotation((DemoVehicle.transform.position - vega.transform.position).normalized);
            Rig.FollowFps(vega);
            yield return null;
            TestLog.Step($"Vega bajo y le apunta a la camioneta con {DemoVehicle.OccupantCount} adentro; aprieta [G] para bajarlos a todos");
            InputDriver.GOrderOnVehicle(DemoVehicle);
            yield return Tutorial("[G] apuntando a un vehiculo con gente: todos bajan y el color vuelve al original.");
            yield return CaptureStep("fase8_vehiculo_vacio_color_original");

            // ============================================================
            // FASE 9 - Batalla final de prueba contra 3 enemigos
            // ============================================================
            TestLog.Phase("FASE 9 - Batalla final contra 3 enemigos (test de todo junto)");

            if (PatrolEnemies != null && PatrolEnemies.Count >= 3)
            {
                FullHeal(vega, kes, doc);
                Vector3 battleground = new Vector3(0f, 0.8f, 0f);
                vega.transform.position = battleground;
                kes.transform.position = battleground + new Vector3(2f, 0f, 0f);
                doc.transform.position = battleground + new Vector3(-2f, 0f, 0f);

                for (int i = 0; i < 3; i++)
                {
                    var enemy = PatrolEnemies[i];
                    enemy.Health.Initialize(enemy.Id, enemy.Health.MaxHealth);
                    enemy.gameObject.SetActive(true);
                    enemy.transform.position = battleground + new Vector3(Mathf.Cos(i * 2.1f) * 9f, 0f, Mathf.Sin(i * 2.1f) * 9f);
                }

                Rig.SetMode(ControlMode.Rts);
                Rig.SetRtsView(battleground);
                yield return Tutorial("FASE 9: se sueltan 3 enemigos de la patrulla contra el escuadron completo. Se prueba todo junto.");
                yield return CaptureStep("fase9_batalla_inicio");

                float battleTimeout = 14f;
                while (battleTimeout > 0f)
                {
                    bool anyEnemyAlive = false;
                    for (int i = 0; i < 3; i++) if (PatrolEnemies[i].Health.IsAlive) anyEnemyAlive = true;
                    bool anySquadAlive = vega.Health.IsAlive || kes.Health.IsAlive || doc.Health.IsAlive;
                    if (!anyEnemyAlive || !anySquadAlive) break;
                    battleTimeout -= Time.deltaTime;
                    yield return null;
                }

                yield return CaptureStep("fase9_batalla_mitad");
                yield return new WaitForSecondsRealtime(2f);
                yield return CaptureStep("fase9_batalla_resultado");

                int enemiesDown = 0;
                for (int i = 0; i < 3; i++) if (!PatrolEnemies[i].Health.IsAlive) enemiesDown++;
                TestLog.Step($"Resultado: {enemiesDown}/3 enemigos caidos. Vega {vega.Health.Current}/{vega.Health.MaxHealth} vida, {kes.DisplayName} {kes.Health.Current}/{kes.Health.MaxHealth}, {doc.DisplayName} {doc.Health.Current}/{doc.Health.MaxHealth}");
            }

            TestLog.Phase("DEMO AUTOMATICO EN PLAY MODE - COMPLETADO");
            IsRunning = false;
        }
    }
}
