using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.CameraSystem;
using SP.Core;
using SP.UI;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    // PlayerInputDriver (parte): manejo de vehiculos (entrar, asientos, camara, ordenes con [T]).
    public partial class PlayerInputDriver
    {
        // -----------------------------------------------------------
        // Adentro del vehículo (conductor o artillero)
        // -----------------------------------------------------------
        // Público para que el runner de demo automático (AutoDemoRunner)
        // pueda ejecutar los mismos pasos que dispararía una tecla real.
        public VehicleSeatRole? CurrentSeat => currentSeat;

        public void EnterVehicle(Vehicle vehicle)
        {
            if (torretaActual != null) SalirDeTorreta();
            var role = vehicle.IsSeatFree(VehicleSeatRole.Driver) ? VehicleSeatRole.Driver : vehicle.FirstFreeSeat();
            if (role == null)
            {
                // Antes esto fallaba en silencio: se apretaba [E] y no
                // pasaba nada, sin ninguna pista de si era porque el
                // vehiculo estaba lleno o porque algo mas fallo.
                if (ModeToast != null) ModeToast.Show("VEHICULO LLENO", 1.2f);
                return;
            }

            var driverSoldier = Brain.Current;
            if (!vehicle.Mount(driverSoldier, role)) return;

            EnterPossessedVehicleSeat(role.Value);
            Feedback.Accion(SfxKind.SeatChange, "SUBISTE AL TANQUE", vehicle.transform.position, Feedback.Ok, aviso: false, pulso: true, volumen: 0.5f);

            // Antes los aliados cercanos subian SOLOS con vos. Pedido
            // explicito: "al subirme al auto los aliados se suben, deberian
            // esperar la orden". Subir a la camioneta es una decision de
            // cada uno, no un efecto secundario de la tuya -- si querias
            // dejar a dos cubriendo una posicion, acercarte al vehiculo te
            // los levantaba sin avisar y no habia forma de evitarlo.
            //
            // Ahora suben con orden: [U] uno por vez (el mas cercano que
            // todavia no va en camino) o la orden de montaje apuntandole al
            // vehiculo en RTS. [I] los baja a todos.
            int esperando = 0;
            if (Squad != null)
            {
                foreach (var s in Squad)
                {
                    if (s == null || s == driverSoldier || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    if (vehicle.RoleOf(s) != null) continue;
                    if (Vector3.Distance(s.transform.position, vehicle.transform.position) <= autoMountRadius)
                        esperando++;
                }
            }
            // El cambio se avisa: sin esto, el que ya tenia la costumbre
            // arranca creyendo que los lleva atras y los deja tirados.
            if (esperando > 0 && ModeToast != null)
                ModeToast.Show(esperando == 1 ? "1 ALIADO ESPERA - [Q] RADIAL > TANQUE > SUBIR TODOS" : $"{esperando} ALIADOS ESPERAN - [Q] RADIAL > TANQUE > SUBIR TODOS", 2.5f);
        }

        // Toma control de un asiento en el que el soldado poseído YA está
        // montado -- sea porque acaba de subir (EnterVehicle) o porque ya
        // estaba adentro y el jugador recién ahora vuelve a esa vista con
        // [TAB] o [F] desde RTS. No llama a Vehicle.Mount: eso ya pasó.
        void EnterPossessedVehicleSeat(VehicleSeatRole role)
        {
            currentSeat = role;
            Vehicle.PlayerAboard = true;
            var vb = Vehicle.GetComponent<VehicleBrain>();
            if (role == VehicleSeatRole.Driver) { vb.Stop(); autoConduccion = false; vb.IsPlayerDriving = true; }

            // La vista de vehiculo es siempre en 3ra persona, y
            // CameraRig.FollowThirdPerson ya converge sola cada frame hacia
            // la pose del vehiculo (ver UpdateVehicleCamera). BeginFollowBlend
            // le pide que esta vez tarde 1s en llegar en vez de su
            // seguimiento ajustado de siempre -- pedido explicito.
            Rig.BeginFollowBlend(VehicleBlendSeconds);
        }

        // Aim, en RTS, apuntando a un vehículo con gente adentro: toma
        // control del conductor (o del primer ocupante si no hay
        // conductor) y pasa a la vista de manejo en primera persona, con
        // su propia UI (velocímetro, vida del vehículo, artillero).
        void EnterVehicleViewFromRts(Vehicle vehicle)
        {
            if (vehicle == null || vehicle.OccupantCount == 0) return;
            var occupant = vehicle.Driver ?? vehicle.Occupants[0];
            if (occupant == null) return;

            if (!occupant.Health.IsAlive)
            {
                if (DeadNotice != null) DeadNotice.Show($"{occupant.DisplayName} esta muerto: no se puede poseer");
                OrderService.PlayRejectSound();
                return;
            }

            PossessionService.Swap(Brain, occupant);
            var role = vehicle.RoleOf(occupant);
            if (role == null) return;

            Rig.SetMode(ControlMode.Fps);
            EnterPossessedVehicleSeat(role.Value);
        }

        void ClearVehicleSeatState()
        {
            if (currentSeat.HasValue && Vehicle != null)
            {
                Vehicle.PlayerAboard = false;
                var vb = Vehicle.GetComponent<VehicleBrain>();
                if (vb != null && currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
            }
            currentSeat = null;
        }

        public void ExitVehicle()
        {
            if (Brain.Current == null) return;
            Feedback.Accion(SfxKind.SeatChange, "BAJASTE DEL TANQUE", Vehicle.transform.position, Feedback.Info, aviso: true, pulso: false, volumen: 0.5f);
            Vehicle.Dismount(Brain.Current);
            var vb = Vehicle.GetComponent<VehicleBrain>();
            if (currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
            Vehicle.PlayerAboard = false;
            currentSeat = null;

            // Si venías viendo el vehículo desde arriba (RTS), bajarte no
            // debe dejar la cámara con una posición/rotación de FPS
            // colgada mientras el modo sigue en ortográfico: hay que
            // recentrar la vista RTS en vez de FollowFps.
            if (Rig.Mode == ControlMode.Rts) Rig.SetRtsView(Brain.Current.transform.position);
            else Rig.FollowOverShoulder(Brain.Current.transform, heightOffset: Brain.Current.Motor.EyeHeightDrop);
        }

        void UpdateInVehicle(Keyboard kb, Mouse mouse)
        {
            // El balanceo es solo de caminar en primera persona: sin
            // apagarlo aca seguiria oscilando la camara en RTS y dentro
            // del vehiculo, donde no hay pisadas que representar.
            Rig.SetWalking(false);
            // El zoom arranca apagado cada frame; solo la rama de
            // artillero lo vuelve a prender (ver mas abajo). Conducir o ir
            // de pasajero no tiene mira que acercar.
            Rig.SetZoomed(false);
            if (WeaponStatus != null) WeaponStatus.gameObject.SetActive(false);
            if (weaponViewmodel != null) weaponViewmodel.SetActive(false);
            if (AimUiRef != null) AimUiRef.SetVisible(false);
            if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false);
            bool puestoDeTiro = currentSeat == VehicleSeatRole.Gunner || currentSeat == VehicleSeatRole.Passenger1;
            if (!puestoDeTiro || Rig.Mode != ControlMode.Fps) MirillaView.Instancia?.Ocultar();
            if (SelectionCount != null) SelectionCount.SetModeVisible(false);
            HideFpsOnlyIndicators();
            if (bodyHiddenFor != null) { bodyHiddenFor.SetBodyVisible(true); bodyHiddenFor.Motor.SetCrouching(false); bodyHiddenFor = null; }
            if (Vehicle == null || Brain.Current == null) { currentSeat = null; return; }
            if (Vehicle.RoleOf(Brain.Current) == null)
            {
                // Una orden lo bajo del tanque (RTS): el estado de asiento quedo colgado.
                ClearVehicleSeatState();
                if (Rig.Mode == ControlMode.Fps) Rig.FollowOverShoulder(Brain.Current.transform, heightOffset: Brain.Current.Motor.EyeHeightDrop);
                return;
            }

            // El tanque se destruye y Vehicle.OnDestroyed() ya expulsa a
            // todo el mundo (Dismount reactiva el GameObject y lo
            // reposiciona) -- pero currentSeat es estado propio de este
            // componente, Vehicle no tiene forma de avisarle que ya no
            // hay que seguir tratando esto como "estoy adentro". Sin este
            // corte, la cámara se queda pegada a una carcasa quemada.
            if (Vehicle.IsDestroyed)
            {
                ClearVehicleSeatState();
                if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
                if (TurretAim != null) TurretAim.SetVisible(false);
                Rig.FollowOverShoulder(Brain.Current.transform, heightOffset: Brain.Current.Motor.EyeHeightDrop);
                return;
            }

            bool radialAbierto = OrdenesMenu != null && OrdenesMenu.Abierto;
            if (radialAbierto && mouse != null) OrdenesMenu.MoverSeleccion(mouse.delta.ReadValue());

            var motor = Vehicle.GetComponent<VehicleMotor>();
            // El freno solo es una accion real cuando quien maneja es el
            // jugador (currentSeat==Driver) y esta apretando [G] -- para
            // un pasajero o el conductor IA esto no aplica.
            bool isBraking = currentSeat == VehicleSeatRole.Driver && KeyBindings.IsPressed(KeyBindings.Frenar);
            if (VehicleStatus != null)
            {
                VehicleStatus.UpdateFrom(Vehicle, motor, isBraking);
                VehicleStatus.SetSeat(currentSeat);
            }

            if (KeyBindings.WasPressed(KeyBindings.SubirBajarVehiculo)
                || KeyBindings.WasPressed(KeyBindings.Interactuar))
            {
                if (!BloqueaAtajo("USA EL RADIAL: [Q] → TANQUE → BAJARME YO")) { ExitVehicle(); return; }
            }

            // Pedido explicito: "si estoy adentro, como digo que entren
            // los que esten cerca?" -- [U] no vivia aca, solo en
            // UpdateFps (a pie), asi que manejando o de artillero no
            // hacia nada. Mismo camino de a uno por apretada.
            if (AtajosDeTecladoHeredados && kb.uKey.wasPressedThisFrame)
            {
                var vehicleToFill = FindTheVehicle();
                if (vehicleToFill != null && !vehicleToFill.IsDestroyed && vehicleToFill.HasAnyRoom)
                {
                    var next = FindNextSquadmateToBoard(vehicleToFill);
                    if (next != null) OrderService.IssueMountOrder(next, vehicleToFill);
                    else RejectOrder("NO HAY MAS ALIADOS PARA SUBIR");
                }
            }

            // [G] todos suben, [I] todos bajan (menos vos): funcionan desde
            // cualquier asiento y desde la vista RTS del tanque. El panel de
            // teclas los muestra junto a los asientos.
            ActualizarPanelDeTeclas();
            if (AtajosDeTecladoHeredados && kb.gKey.wasPressedThisFrame) SubirATodos(Vehicle);
            if (AtajosDeTecladoHeredados && kb.iKey.wasPressedThisFrame) BajarATodos(Vehicle);
            if (panelDeTeclas != null)
            {
                if (kb.uKey.wasPressedThisFrame) panelDeTeclas.Destellar("U");
                if (kb.tKey.wasPressedThisFrame) panelDeTeclas.Destellar("T");
                if (kb.digit1Key.wasPressedThisFrame) panelDeTeclas.Destellar("1");
                if (kb.digit2Key.wasPressedThisFrame) panelDeTeclas.Destellar("2");
                if (kb.digit3Key.wasPressedThisFrame) panelDeTeclas.Destellar("3");
                if (kb.digit4Key.wasPressedThisFrame) panelDeTeclas.Destellar("4");
                if (kb.spaceKey.wasPressedThisFrame) panelDeTeclas.Destellar("Espacio");
            }

            // En RTS, adentro del vehículo: solo cámara top-down + la UI
            // del tanque, nada de manejar/artillar (eso es de la vista
            // FPS). [TAB] -- ya manejado en Update() -- es la puerta para
            // volver a manejar sin tener que bajarse y volver a subir.
            if (Rig.Mode == ControlMode.Rts)
            {
                Vector3 pan = Vector3.zero;
                if (kb.wKey.isPressed) pan += Vector3.forward;
                if (kb.sKey.isPressed) pan += Vector3.back;
                if (kb.dKey.isPressed) pan += Vector3.right;
                if (kb.aKey.isPressed) pan += Vector3.left;
                if (pan.sqrMagnitude > 0.0001f) Rig.Pan(pan.normalized * rtsPanSpeed * Time.deltaTime);

                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f) Rig.ZoomHaciaCursor(scroll * rtsZoomSpeed * Time.deltaTime, mouse.position.ReadValue());
                }

                if (TurretAim != null) TurretAim.SetVisible(false);
                SetInstructionText("[TAB] volver a manejar en primera persona   ·   [Rueda] zoom hacia el cursor   ·   [Q] radial   ·   [E] bajar");
                return;
            }

            var vb = Vehicle.GetComponent<VehicleBrain>();
            // BUG REAL que esto corrige: GetComponentInChildren<TurretWeapon>()
            // devolvia CUALQUIERA de los dos TurretWeapon del tanque (el del
            // cañon en "TurretPivot" y, desde que existe, el de la
            // metralleta en "MetralletaPivot") segun el orden de la
            // jerarquia -- ambiguo apenas se agrego un segundo. Cada asiento
            // busca el suyo por nombre, sin adivinar.
            var turretPivotT = Vehicle.transform.Find("TurretMount/TurretPivot");
            var turret = turretPivotT != null ? turretPivotT.GetComponent<TurretWeapon>() : null;
            var mgPivotT = Vehicle.transform.Find("MetralletaMount/MetralletaPivot");
            var mgTurret = mgPivotT != null ? mgPivotT.GetComponent<TurretWeapon>() : null;
            // El HUD de torreta es solo de quien esta apuntando un arma
            // montada: conduciendo no aporta nada y taparia la vista.
            if (TurretAim != null && currentSeat != VehicleSeatRole.Gunner && currentSeat != VehicleSeatRole.Passenger1) TurretAim.SetVisible(false);

            // Cambio de asiento con [1] conducir, [2] cañon, [3] metralleta,
            // [4] pasajero -- desde CUALQUIER asiento y hacia cualquiera:
            // libre (te mueves) u ocupado por un aliado (intercambian).
            // Antes solo funcionaba hacia un asiento LIBRE y con uno
            // ocupado la tecla no hacia nada, ni un aviso.
            VehicleSeatRole? asientoPedido = null;
            if (kb.digit1Key.wasPressedThisFrame) asientoPedido = VehicleSeatRole.Driver;
            else if (kb.digit2Key.wasPressedThisFrame) asientoPedido = VehicleSeatRole.Gunner;
            else if (kb.digit3Key.wasPressedThisFrame && mgTurret != null) asientoPedido = VehicleSeatRole.Passenger1;
            else if (kb.digit4Key.wasPressedThisFrame) asientoPedido = VehicleSeatRole.Passenger2;
            if (asientoPedido.HasValue && asientoPedido != currentSeat)
            {
                SwitchSeat(asientoPedido.Value);
                return;
            }

            // [T] manda el vehiculo adonde apunta la camara, sea cual sea
            // el asiento (conductor, cañon, metralleta o pasajero).
            OrdenDeVehiculoConT(kb);

            if (currentSeat == VehicleSeatRole.Driver)
            {
                float throttle = (kb.wKey.isPressed ? 1f : 0f) + (kb.sKey.isPressed ? -1f : 0f);
                float steer = (kb.dKey.isPressed ? 1f : 0f) + (kb.aKey.isPressed ? -1f : 0f);
                bool frenando = KeyBindings.IsPressed(KeyBindings.Frenar);

                // Conduccion automatica: se activa con [T] (ver
                // OrdenDeVehiculoConT). Mientras la orden siga viva el
                // VehicleBrain maneja solo, y el jugador recupera el volante
                // apenas toca WASD o el freno -- ahi se cancela la orden.
                if (autoConduccion)
                {
                    if (!vb.HasOrder) autoConduccion = false;
                    else if (Mathf.Abs(throttle) > 0.01f || Mathf.Abs(steer) > 0.01f || frenando)
                    {
                        vb.Stop();
                        autoConduccion = false;
                    }
                }

                if (autoConduccion)
                {
                    vb.IsPlayerDriving = false;
                }
                else
                {
                    vb.IsPlayerDriving = true;
                    if (frenando) motor.Brake(Time.deltaTime);
                    else motor.Drive(throttle, steer, Time.deltaTime);
                }

                UpdateVehicleCamera();
            }
            else if (currentSeat == VehicleSeatRole.Gunner)
            {
                if (mouse != null && turret != null)
                {
                    // El mouse ya no gira el cañon directo: mueve el
                    // angulo OBJETIVO, y el cañon lo persigue a velocidad
                    // limitada. Es lo que le da peso a la torreta -- y lo
                    // que hace que el reticulo de "ya llegue / todavia
                    // girando" tenga algo que informar.
                    var delta = radialAbierto ? Vector2.zero : mouse.delta.ReadValue();
                    turret.AddDesiredYaw(delta.x * turretSensitivity);
                    // BUG REAL: delta.y (arriba/abajo del mouse) se leia
                    // completo mas arriba pero nunca se usaba -- el cañon
                    // solo podia girar en el plano horizontal. Signo
                    // invertido a proposito: mouse hacia arriba (delta.y
                    // positivo) tiene que INCLINAR el cañon hacia arriba,
                    // que en este rig es pitch NEGATIVO (ver
                    // TurretWeapon.minPitchDeg/maxPitchDeg).
                    turret.AddDesiredPitch(-delta.y * turretSensitivity);
                    turret.TickPlayerAim(Time.deltaTime);
                    if (mouse.leftButton.wasPressedThisFrame) turret.TryFire();

                    // El artillero usaba el mismo FOV que caminando, asi
                    // que apuntar a distancia era adivinar. El zoom de
                    // mirilla existia a pie pero se desactivaba adrede en
                    // vehiculo; con el arco balistico hace mas falta aca.
                    Rig.SetZoomFactor(3.5f);
                    Rig.SetZoomed(mouse.rightButton.isPressed);

                    // [R] alterna municion: explosiva de area o
                    // perforante de daño concentrado.
                    if (KeyBindings.WasPressed(KeyBindings.Recargar))
                    {
                        turret.CycleAmmo();
                        if (ModeToast != null)
                            ModeToast.Show(turret.Ammo == TurretWeapon.AmmoType.Explosive ? "MUNICION EXPLOSIVA" : "MUNICION PERFORANTE", 1.1f);
                    }
                }
                if (TurretAim != null) TurretAim.UpdateFrom(turret);

                UpdateVehicleCameraAimed(turret != null ? turret.transform : null, turret, ReticleStyle.Artillero);
            }
            // Pedido explicito: "ahora es cañon y metralleta y conductor" --
            // un tercer puesto operable de verdad, no un pasajero mudo.
            // Mismo patron que el artillero del cañon (mouse apunta, click
            // dispara) pero sobre mgTurret -- su propio TurretWeapon,
            // independiente del cañon (ver MetralletaPivot).
            else if (currentSeat == VehicleSeatRole.Passenger1)
            {
                if (mouse != null && mgTurret != null)
                {
                    var delta = radialAbierto ? Vector2.zero : mouse.delta.ReadValue();
                    mgTurret.AddDesiredYaw(delta.x * turretSensitivity);
                    mgTurret.AddDesiredPitch(-delta.y * turretSensitivity);
                    mgTurret.TickPlayerAim(Time.deltaTime);
                    if (mouse.leftButton.isPressed) mgTurret.TryFire();
                    // Ronda 12: la metralleta del tanque se apunta con la mirilla NORMAL (la misma que la torreta fija):
                    // sin zoom ni optica, el clic derecho no hace nada aca.
                    Rig.SetZoomed(false);
                }
                if (TurretAim != null) TurretAim.UpdateFrom(mgTurret);

                UpdateVehicleCameraAimed(mgTurret != null ? mgTurret.transform : null, mgTurret, ReticleStyle.Anillo, conMira: false);
            }
            else
            {
                UpdateVehicleCamera();
            }

            const string asientos = "[1] conducir · [2] cañón · [3] metralleta · [4] pasajero (si esta ocupado, intercambian)";
            string role = currentSeat == VehicleSeatRole.Driver
                ? "[WASD] conducir · [Espacio] frenar · [Q] radial (tanque allí, subir/bajar) · " + asientos + " · [TAB] vista RTS · [E] bajar"
                : currentSeat == VehicleSeatRole.Gunner
                    ? "[Mouse] apuntar · [Click] disparar · [Click der.] zoom · [R] munición · [Q] radial · " + asientos + " · [E] bajar"
                    : currentSeat == VehicleSeatRole.Passenger1
                        ? "[Mouse] apuntar · [Click] disparar (mantener = rafaga) · [Q] radial · " + asientos + " · [E] bajar"
                        : "[Q] radial (tanque allí, subir/bajar) · " + asientos + " · [E] bajar · [TAB] vista RTS";
            SetInstructionText(role);
        }

        public void SwitchSeat(VehicleSeatRole newRole)
        {
            var soldier = Brain.Current;
            if (soldier == null || Vehicle == null) return;
            if (Vehicle.IsMountAnimating(soldier)) return;
            if (Vehicle.RoleOf(soldier) == newRole) return;

            var vb = Vehicle.GetComponent<VehicleBrain>();

            var ocupante = Vehicle.SoldierInSeat(newRole);
            if (ocupante != null)
            {
                // Asiento ocupado por un aliado: intercambian. Antes esto se
                // rechazaba ("ASIENTO OCUPADO") o, mas abajo, Mount caia a
                // OTRO asiento distinto al pedido.
                if (ocupante.Health == null || !ocupante.Health.IsAlive || Vehicle.IsMountAnimating(ocupante)
                    || !Vehicle.SwapSeats(soldier, ocupante))
                {
                    RejectOrder("ASIENTO OCUPADO");
                    return;
                }
                if (ModeToast != null) ModeToast.Show($"CAMBIAS DE ASIENTO CON {ocupante.DisplayName.ToUpperInvariant()}", 1.2f);
            }
            else
            {
                // Libera el asiento actual sin reaparecer al soldado afuera.
                Vehicle.MoveToSeat(soldier, newRole);
            }

            autoConduccion = false;
            if (currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
            // Se lee el asiento REAL tras montar, no se asume newRole: si
            // alguna vez Mount vuelve a caer a un asiento distinto (otra
            // carrera, otro llamador que no valido antes), currentSeat
            // sigue reflejando la verdad en vez de mentir.
            currentSeat = Vehicle.RoleOf(soldier) ?? newRole;
            if (currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = true;
            // BUG REAL que esto corrige: decia "se monto en la metralleta"
            // para el asiento del CAÑON (el que dispara obuses explosivos/
            // perforantes) -- confundia las dos armas del tanque entre si.
            // Ahora cada asiento nombra la suya.
            if (newRole == VehicleSeatRole.Gunner) GameLog.Line("Se montó en el cañón");
            if (newRole == VehicleSeatRole.Passenger1) GameLog.Line("Se montó en la metralleta");

            Feedback.Accion(SfxKind.SeatChange, ocupante != null ? "INTERCAMBIO DE ASIENTO" : "CAMBIO DE ASIENTO", Vehicle.transform.position,
                Feedback.Info, aviso: false, pulso: false, volumen: 0.5f);

            // La vista de vehiculo es siempre en 3ra persona orbitando el
            // chasis (Vehicle.transform): cambiar de asiento no mueve el
            // punto de origen de la camara, pero igual se pide 1s de lerp
            // -- es el mismo gesto que entrar por primera vez.
            Rig.BeginFollowBlend(VehicleBlendSeconds);
        }

        // Conduccion automatica pedida por el CONDUCTOR con [T]: la orden
        // vive en el VehicleBrain y esto solo recuerda que el jugador la
        // pidio para no pisarla con Drive(0,0) cada frame.
        bool autoConduccion;

        // Primer punto de SUELO que toca el rayo de la camara, atravesando el
        // propio vehiculo y a los soldados. La camara de 3ra persona mira por
        // encima del chasis, asi que un raycast comun (AimTargeting) devuelve
        // "Vehiculo" y nunca "Suelo": el conductor no podia dar la orden.
        // Suelo = superficie casi horizontal; una pared o un edificio no vale.
        bool TryGroundPointBehindVehicle(Ray ray, out Vector3 punto)
        {
            punto = default;
            var hits = Physics.RaycastAll(ray, Aim != null ? Aim.MaxDistance : 200f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.GetComponentInParent<Vehicle>() != null) continue;
                if (h.collider.GetComponentInParent<Soldier>() != null) continue;
                if (h.normal.y < 0.7f) return false; // pared / lateral de un cubo
                punto = h.point;
                return true;
            }
            return false;
        }

        // [T] dentro del vehiculo: manda el vehiculo al punto de suelo al
        // que apunta la camara (la de 3ra persona, o la del cañon/metralleta
        // cuando se esta apuntando). Vale desde cualquier asiento.
        void OrdenDeVehiculoConT(Keyboard kb)
        {
            if (!AtajosDeTecladoHeredados || !kb.tKey.wasPressedThisFrame || Vehicle == null || Rig.Cam == null) return;

            // Sin conductor, un aliado que vaya a bordo toma el volante (el
            // jugador en el cañon o la metralleta no puede manejar a la vez).
            if (Vehicle.Driver == null)
            {
                Soldier relevo = null;
                foreach (var ocupante in Vehicle.Occupants)
                {
                    if (ocupante == null || ocupante == Brain.Current) continue;
                    if (ocupante.Health == null || !ocupante.Health.IsAlive || Vehicle.IsMountAnimating(ocupante)) continue;
                    relevo = ocupante;
                    break;
                }
                if (relevo == null || !Vehicle.MoveToSeat(relevo, VehicleSeatRole.Driver))
                {
                    RejectOrder("EL VEHICULO NECESITA UN CONDUCTOR");
                    return;
                }
                if (ModeToast != null) ModeToast.Show($"{relevo.DisplayName.ToUpperInvariant()} TOMA EL VOLANTE", 1.2f);
            }

            if (!TryGroundPointBehindVehicle(Rig.GetForwardRay(), out var destino))
            {
                RejectOrder("APUNTA AL SUELO PARA MANDAR EL VEHICULO");
                return;
            }

            if (!TryIssueVehicleMoveOrder(destino)) return;

            // Si el conductor es el propio jugador, el volante pasa al
            // VehicleBrain hasta que toque WASD.
            if (currentSeat == VehicleSeatRole.Driver) autoConduccion = true;
            if (ModeToast != null) ModeToast.Show("VEHICULO EN CAMINO", 1.0f);
        }

        // La vista de vehiculo -- manejando o de artillero -- es siempre en
        // 3ra persona. Pedido explicito: antes el conductor veia en
        // primera persona (el ancla "DriverEye") y solo el artillero (o
        // con [V]) pasaba a 3ra, una inconsistencia entre asientos que
        // ademas hacia mas dificil ver el vehiculo entero al manejar.
        // BUG REAL (cosmetico, no de gameplay): el parametro "anchor" no se
        // usaba para nada -- FollowThirdPerson siempre orbita
        // Vehicle.transform sin importar que ancla se le pase. Los tres
        // llamadores (conductor, cañon, metralleta) calculaban
        // DriverEye/GunnerEye/MetralletaEye y los pasaban para nada, lo que
        // hacia parecer que "faltaba" un ancla propia por asiento cuando en
        // realidad NINGUNO se usaba jamas: la vista de vehiculo es a
        // proposito la misma orbita de 3ra persona sobre el chasis para
        // cualquier asiento (pedido explicito de una sesion anterior). Se
        // saca el parametro muerto en vez de dejarlo prometiendo algo que
        // no hace.
        void UpdateVehicleCamera()
        {
            Rig.FollowThirdPerson(Vehicle.transform, 8f, 3.5f);
            ApplyVehicleCameraFeel();
            ApplyVehicleSpeedFx();
        }

        // Pedido explicito: "quiero q cuando este usando la torreta la
        // camara rote mirando hacia donde apunto". Cañon y metralleta
        // llaman a esta en vez de UpdateVehicleCamera: la camara orbita
        // el forward del ARMA (gira con el mouse), no el del casco.
        // Mantener click derecho: la camara pasa a la MIRA del arma (primera persona sobre el canon
        // o la metralleta), con zoom y una reticula de optica. Sin apuntar, sigue en tercera persona.
        void UpdateVehicleCameraAimed(Transform aimSource, TurretWeapon arma = null, ReticleStyle reticula = ReticleStyle.Artillero, bool conMira = true)
        {
            if (aimSource != null)
            {
                // El canon mira por un periscopio sobre el techo de la torreta (0,7 m sobre el pivote, un poco
                // adelantado: se ve el tubo abajo); la metralleta, desde detras y arriba del arma.
                bool esCanon = conMira;
                // Con el cañon inclinado, 'up' de la torreta se tumba y el periscopio quedaba enterrado en el casco (pantalla marron):
                // la altura se mide en vertical de mundo y el adelanto en horizontal.
                Vector3 planoFwd = Vector3.ProjectOnPlane(aimSource.forward, Vector3.up);
                planoFwd = planoFwd.sqrMagnitude > 1e-4f ? planoFwd.normalized : Vehicle.transform.forward;
                Vector3 miraPos = aimSource.position + Vector3.up * (esCanon ? 0.85f : 0.28f) + (esCanon ? planoFwd * 0.15f : -aimSource.forward * 0.4f);
                Rig.FollowThirdPersonAimed(Vehicle.transform.position + Vector3.up * 1f, aimSource.forward, 8f, 3.5f,
                    miraPos, Quaternion.LookRotation(aimSource.forward, Vector3.up));
            }
            else
                Rig.FollowThirdPerson(Vehicle.transform, 8f, 3.5f);

            var canvasRoot = AimUiRef != null ? AimUiRef.transform.parent : null;
            var mirilla = MirillaView.Asegurar(canvasRoot);
            if (mirilla != null)
                mirilla.Actualizar(conMira && Rig.EstaConZoom && Rig.AdsBlend > 0.55f, reticula,
                    arma != null && arma.IsOnTarget() ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.9f, 0.5f));
            if (TurretAim != null && conMira && Rig.AdsBlend > 0.55f) TurretAim.SetVisible(false);
            ApplyVehicleCameraFeel();
            ApplyVehicleSpeedFx();
        }

        // Sin esto la camara del vehiculo esta rigidamente pegada al
        // ancla frame a frame: un tanque de varias toneladas se sentia
        // igual de liviano que una camara flotando. Se suma DESPUES de
        // posicionar la camara (Rig.FollowAnchor/FollowThirdPerson ya
        // corrieron), como un empujon extra, sin que CameraRig tenga que
        // saber nada de vehiculos.
        float vehiclePrevSpeed;

        // 178 desenfoque de movimiento y 182 viñeta de velocidad: manejar
        // a fondo se veia igual que estar detenido salvo por el numerito
        // del velocimetro. Los dos leen la MISMA fraccion de velocidad,
        // asi que se calculan una sola vez y en un solo lugar.
        void ApplyVehicleSpeedFx()
        {
            if (Vehicle == null) return;
            var motor = Vehicle.GetComponent<VehicleMotor>();
            if (motor == null) return;

            float speedFrac = Mathf.Clamp01(Mathf.Abs(motor.CurrentSpeed) / Mathf.Max(0.01f, motor.MaxSpeed));
            var postFx = SP.Presentation.PostFxDirector.Instance;
            if (postFx != null) postFx.SetSpeedBlur(speedFrac);
            if (DamageVignette != null) DamageVignette.SetSpeedFraction(speedFrac);
        }

        void ApplyVehicleCameraFeel()
        {
            if (Rig.IsTransitioning || Vehicle == null) return;
            var motor = Vehicle.GetComponent<VehicleMotor>();
            if (motor == null) return;

            // Inercia: empuje en sentido contrario a como cambio la
            // velocidad este frame (acelerar empuja hacia atras, frenar
            // empuja hacia adelante), no a la velocidad en si.
            float speedDelta = motor.CurrentSpeed - vehiclePrevSpeed;
            vehiclePrevSpeed = motor.CurrentSpeed;
            Vector3 inertiaOffset = -Vehicle.transform.forward * Mathf.Clamp(speedDelta * 0.12f, -0.25f, 0.25f);

            // Sacudida proporcional a la velocidad actual: sin fisica
            // real, el desplazamiento del VehicleMotor es perfectamente
            // liso, como deslizarse sobre hielo. Ruido Perlin en vez de
            // Random puro para que no tiemble a los saltos entre frames.
            float speedFrac = Mathf.Abs(motor.CurrentSpeed) / Mathf.Max(0.01f, motor.MaxSpeed);
            float shakeAmount = speedFrac * 0.035f;
            Vector3 shakeOffset = new Vector3(
                (Mathf.PerlinNoise(Time.time * 18f, 0.37f) - 0.5f) * shakeAmount,
                (Mathf.PerlinNoise(0.71f, Time.time * 18f) - 0.5f) * shakeAmount,
                0f);

            // Antes esto escribia transform.position directo, saltandose el
            // presupuesto de sacudida del rig: la inercia del vehiculo se
            // sumaba encima de cualquier otra sacudida sin tope alguno.
            // AddFrameOffset la mete por el canal continuo, que si esta
            // acotado y respeta el interruptor de efectos de camara.
            Rig.AddFrameOffset(inertiaOffset + shakeOffset);
        }
    }
}
