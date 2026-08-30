using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.CameraSystem;
using SP.UI;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    // Traduce teclado/ratón reales a los mismos métodos que usa el test
    // automático. No decide nada nuevo: es el "pegamento" de Play mode.
    // Solo corre cuando el juego está en Play (Application.isPlaying).
    public class PlayerInputDriver : MonoBehaviour
    {
        public PlayerBrain Brain;
        public AimTargeting Aim;
        public CameraRig Rig;
        public SelectionController Selection;
        public List<Soldier> Squad;
        public AimUI AimUiRef;
        public InstructionBannerView Instructions;
        public Image SelectionBox;
        public Vehicle Vehicle;
        public List<WeaponPickup> WeaponPickups;
        public MinimapFollow MinimapRef;

        [SerializeField] float lookSensitivity = 0.15f;
        [SerializeField] float rtsPanSpeed = 14f;
        [SerializeField] float rtsZoomSpeed = 20f;
        [SerializeField] float dragThresholdPixels = 6f;
        [SerializeField] float interactRadius = 3.5f;
        [SerializeField] float autoMountRadius = 6f;

        bool dragging;
        Vector2 dragStart;

        // Resaltado de a qué le estoy apuntando (aliado o vehículo): se
        // guarda el renderer y su color original para poder devolvérselo
        // apenas dejo de apuntarle.
        Renderer highlightedRenderer;
        Color highlightedOriginalColor;
        VehicleMountIndicator mountIndicator;

        // Estado de "estoy adentro de un vehículo".
        VehicleSeatRole? currentSeat;
        bool vehicleFirstPerson = true;

        public void ToggleVehicleCameraView() => vehicleFirstPerson = !vehicleFirstPerson;

        // Mensaje de tutorial que pisa temporalmente el texto contextual
        // normal (usado por el nivel tutorial / demo automática para narrar
        // paso a paso qué está pasando, en vez del prompt de "qué apretar").
        float tutorialUntil = -1f;
        string tutorialText = "";
        bool TutorialActive => Time.time < tutorialUntil;

        public void ShowTutorialMessage(string text, float holdSeconds = 1.4f)
        {
            tutorialText = text;
            tutorialUntil = Time.time + holdSeconds;
            if (Instructions != null) Instructions.SetText(text);
        }

        void SetInstructionText(string contextual)
        {
            if (Instructions == null) return;
            Instructions.SetText(TutorialActive ? tutorialText : contextual);
        }

        // PlayerBrain.Current no se serializa con la escena (es estado de
        // runtime, no de diseño). Al entrar en Play desde cero hay que
        // poseer al primer soldado de la escuadra a mano.
        void Start()
        {
            if (Brain.Current == null && Squad != null && Squad.Count > 0)
            {
                Brain.Possess(Squad[0]);
                Rig.FollowFps(Squad[0]);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            UpdateCursorLock(kb, Mouse.current);

            if (MinimapRef != null)
                MinimapRef.Target = currentSeat.HasValue ? Vehicle.transform : (Brain.Current != null ? Brain.Current.transform : null);

            if (currentSeat.HasValue)
            {
                UpdateInVehicle(kb, Mouse.current);
                return;
            }

            if (kb.tabKey.wasPressedThisFrame)
            {
                Rig.ToggleMode();
                // Al pasar a RTS, la vista se centra en la última posición
                // del soldado que estabas controlando (no donde quedó la
                // cámara en FPS).
                if (Rig.Mode == ControlMode.Rts && Brain.Current != null)
                    Rig.SetRtsView(Brain.Current.transform.position);
            }

            if (Rig.Mode == ControlMode.Fps) UpdateFps(kb, Mouse.current);
            else UpdateRts(kb, Mouse.current);
        }

        // El cursor arranca libre (para poder clickear la UI/el juego). Al
        // primer click adentro se bloquea y esconde, como cualquier FPS; con
        // Escape se libera de nuevo. En vista RTS lo dejamos libre siempre,
        // porque ahí el mouse selecciona y arrastra en vez de mirar.
        void UpdateCursorLock(Keyboard kb, Mouse mouse)
        {
            bool wantsLock = currentSeat.HasValue || Rig.Mode == ControlMode.Fps;

            if (wantsLock)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // -----------------------------------------------------------
        // A pie (FPS)
        // -----------------------------------------------------------
        void UpdateFps(Keyboard kb, Mouse mouse)
        {
            if (Brain.Current == null) return;
            if (AimUiRef != null) AimUiRef.SetWatchedShooter(Brain.Current.Id);

            Vector3 f = Brain.Current.transform.forward;
            Vector3 r = Brain.Current.transform.right;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += f;
            if (kb.sKey.isPressed) move -= f;
            if (kb.dKey.isPressed) move += r;
            if (kb.aKey.isPressed) move -= r;
            if (move.sqrMagnitude > 0.0001f) Brain.Move(move.normalized, Time.deltaTime);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = mouse.delta.ReadValue();
                Brain.RotateYaw(delta.x * lookSensitivity);
                Rig.AddPitch(delta.y * lookSensitivity);
            }

            Rig.FollowFps(Brain.Current);

            var ray = Rig.GetForwardRay();
            var result = Aim.Evaluate(ray, Brain.Current);
            UpdateAimHighlight(result);
            UpdateVehicleMountIndicator(result);
            if (AimUiRef != null) AimUiRef.UpdateFromAimResult(result);

            if (mouse != null && mouse.leftButton.wasPressedThisFrame) Brain.Fire();

            if (kb.digit1Key.wasPressedThisFrame) EquipFromCatalog(WeaponKind.Rifle);
            if (kb.digit2Key.wasPressedThisFrame) EquipFromCatalog(WeaponKind.Pistol);
            if (kb.digit3Key.wasPressedThisFrame) EquipFromCatalog(WeaponKind.Heavy);

            if (kb.fKey.wasPressedThisFrame && result.Type == AimTargetType.Ally)
            {
                var target = result.Soldier;
                PossessionService.Swap(Brain, target);
                Rig.BeginTransition(target.EyeAnchor != null ? target.EyeAnchor : target.transform);
            }

            if (kb.tKey.wasPressedThisFrame && result.Type == AimTargetType.Ground)
            {
                var nearest = OrderService.FindNearestFreeAlly(result.Point, TeamId.Player, Brain.Current);
                if (nearest != null) OrderService.IssueMoveOrder(nearest, result.Point);
            }

            if (kb.gKey.wasPressedThisFrame && result.Type == AimTargetType.Vehicle)
                GOrderOnVehicle(result.Vehicle);

            // Orden a la camioneta: clic derecho sobre el suelo, viajando sola.
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && result.Type == AimTargetType.Ground)
                TryIssueVehicleMoveOrder(result.Point);

            // Interacción por cercanía (no por puntería): subir al vehículo
            // o equipar un arma tirada en el piso.
            var nearVehicle = Vehicle != null && Vector3.Distance(Brain.Current.transform.position, Vehicle.transform.position) <= interactRadius
                ? Vehicle : null;
            var nearPickup = FindNearestPickup(Brain.Current.transform.position);

            if (kb.eKey.wasPressedThisFrame)
            {
                if (nearVehicle != null) EnterVehicle(nearVehicle);
                else if (nearPickup != null) nearPickup.EquipOn(Brain.Current.Weapon, Brain.Current.Id);
            }

            SetInstructionText(nearVehicle != null ? "[E] Subir al vehiculo (se suben los aliados cercanos)"
                : nearPickup != null ? $"[E] Equipar {nearPickup.Kind}"
                : BuildFpsInstruction(result));
        }

        // Resalta (aclara el color) el aliado o vehículo al que se le está
        // apuntando, y le devuelve su color original apenas se deja de
        // apuntarle o se apunta a otra cosa.
        void UpdateAimHighlight(AimResult result)
        {
            Renderer target = null;
            if (result.Type == AimTargetType.Ally && result.Soldier != null)
                target = result.Soldier.GetComponentInChildren<Renderer>();
            else if (result.Type == AimTargetType.Vehicle && result.Vehicle != null)
                target = result.Vehicle.GetComponentInChildren<Renderer>();

            if (target == highlightedRenderer) return;

            if (highlightedRenderer != null)
                highlightedRenderer.sharedMaterial.color = highlightedOriginalColor;

            highlightedRenderer = target;
            if (target != null)
            {
                highlightedOriginalColor = target.sharedMaterial.color;
                target.sharedMaterial.color = Color.Lerp(highlightedOriginalColor, Color.white, 0.65f);
            }
        }

        // [G] apuntando a un vehículo: si tiene gente adentro, todos bajan
        // (orden inversa a subir); si está vacío, se manda al aliado libre
        // más cercano a que suba, como antes.
        public void GOrderOnVehicle(Vehicle vehicle)
        {
            if (vehicle.OccupantCount > 0)
            {
                DismountAll(vehicle);
                return;
            }

            var nearest = OrderService.FindNearestFreeAlly(vehicle.transform.position, TeamId.Player, Brain.Current);
            if (nearest != null) OrderService.IssueMountOrder(nearest, vehicle);
        }

        void DismountAll(Vehicle vehicle)
        {
            foreach (var occupant in new List<Soldier>(vehicle.Occupants)) vehicle.Dismount(occupant);
        }

        // Flecha (cilindro+cono) sobre el vehículo apuntado, más una línea
        // por cada aliado libre y cercano que subiría solo si se le ordena.
        void UpdateVehicleMountIndicator(AimResult result)
        {
            if (result.Type != AimTargetType.Vehicle)
            {
                if (mountIndicator != null) mountIndicator.Hide();
                return;
            }

            if (mountIndicator == null) mountIndicator = VehicleMountIndicator.Create();

            var incoming = new List<Soldier>();
            if (Squad != null)
            {
                foreach (var s in Squad)
                {
                    if (s == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    if (result.Vehicle.RoleOf(s) != null) continue; // ya está adentro
                    if (Vector3.Distance(s.transform.position, result.Vehicle.transform.position) <= autoMountRadius)
                        incoming.Add(s);
                }
            }
            mountIndicator.Show(result.Vehicle, incoming);
        }

        void EquipFromCatalog(WeaponKind kind) => EquipWeaponHotkey(kind);

        // Público para que la demo/tutorial automáticos puedan probar los
        // atajos 1/2/3 sin depender de que haya un teclado físico.
        public void EquipWeaponHotkey(WeaponKind kind)
        {
            if (Brain.Current == null) return;
            var spec = WeaponCatalog.Get(kind);
            Brain.Current.Weapon.EquipWeapon(kind, spec.Damage, spec.Cooldown, spec.Color);
        }

        // Única puerta de entrada para "mandar la camioneta sola a un
        // punto": solo funciona si hay un aliado tuyo sentado de conductor
        // (si no hay nadie manejando, no tiene sentido que se mueva sola).
        // Pública y reusada por el FPS, por el artillero y por los tests.
        // Debajo de esta distancia, un click nuevo se considera "el mismo
        // pedido de siempre" y se ignora: sin esto, clickear cerca del
        // destino (o de donde ya está la camioneta) dispara una orden nueva
        // por frame y termina tirando marcadores repetidos sin parar.
        const float RedundantOrderDistance = 2f;

        public bool TryIssueVehicleMoveOrder(Vector3 point)
        {
            if (Vehicle == null || Vehicle.Driver == null) return false;

            var vb = Vehicle.GetComponent<VehicleBrain>();

            if (Vector3.Distance(point, Vehicle.transform.position) < RedundantOrderDistance) return false;
            if (vb.HasOrder && vb.CurrentDestination.HasValue &&
                Vector3.Distance(point, vb.CurrentDestination.Value) < RedundantOrderDistance) return false;

            vb.IsPlayerDriving = false;
            vb.IssueMoveOrder(point);
            OrderMarkerFx.Spawn(point, OrderMarkerFx.MoveColor);
            return true;
        }

        WeaponPickup FindNearestPickup(Vector3 from)
        {
            if (WeaponPickups == null) return null;
            WeaponPickup best = null;
            float bestDist = interactRadius;
            foreach (var p in WeaponPickups)
            {
                if (p == null) continue;
                float d = Vector3.Distance(from, p.transform.position);
                if (d <= bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        string BuildFpsInstruction(AimResult result)
        {
            switch (result.Type)
            {
                case AimTargetType.Ally:
                    return $"[F] poseer a {result.Soldier.DisplayName}   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Vehicle:
                    return "[G] ordenar al aliado mas cercano que suba   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Ground:
                    return "[T] ordenar ir aquí   ·   [Click der.] mandar la camioneta aquí (si hay alguien manejando)   ·   [Click] disparar   ·   [TAB] vista RTS";
                default:
                    return "[WASD] moverse   ·   [Click] disparar   ·   [1][2][3] cambiar de arma   ·   [TAB] vista RTS";
            }
        }

        // -----------------------------------------------------------
        // Adentro del vehículo (conductor o artillero)
        // -----------------------------------------------------------
        // Público para que el runner de demo automático (AutoDemoRunner)
        // pueda ejecutar los mismos pasos que dispararía una tecla real.
        public VehicleSeatRole? CurrentSeat => currentSeat;

        public void EnterVehicle(Vehicle vehicle)
        {
            var role = vehicle.IsSeatFree(VehicleSeatRole.Driver) ? VehicleSeatRole.Driver : vehicle.FirstFreeSeat();
            if (role == null) return;

            var driverSoldier = Brain.Current;
            if (!vehicle.Mount(driverSoldier, role)) return;

            currentSeat = role;
            var vb = vehicle.GetComponent<VehicleBrain>();
            if (role == VehicleSeatRole.Driver) vb.IsPlayerDriving = true;

            Transform seatAnchor = role == VehicleSeatRole.Driver ? vehicle.transform.Find("DriverEye") : vehicle.transform;
            if (seatAnchor != null) Rig.BeginTransition(seatAnchor);

            // Los aliados libres cerca también suben, en cualquier asiento libre.
            foreach (var s in Squad)
            {
                if (s == null || s == driverSoldier || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(s.transform.position, vehicle.transform.position) <= autoMountRadius)
                    vehicle.Mount(s);
            }
        }

        public void ExitVehicle()
        {
            if (Brain.Current == null) return;
            Vehicle.Dismount(Brain.Current);
            var vb = Vehicle.GetComponent<VehicleBrain>();
            if (currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
            currentSeat = null;
            Rig.FollowFps(Brain.Current);
        }

        void UpdateInVehicle(Keyboard kb, Mouse mouse)
        {
            if (Vehicle == null || Brain.Current == null) { currentSeat = null; return; }

            if (kb.vKey.wasPressedThisFrame) vehicleFirstPerson = !vehicleFirstPerson;
            if (kb.eKey.wasPressedThisFrame) { ExitVehicle(); return; }

            var motor = Vehicle.GetComponent<VehicleMotor>();
            var vb = Vehicle.GetComponent<VehicleBrain>();
            var turret = Vehicle.GetComponentInChildren<TurretWeapon>();

            if (currentSeat == VehicleSeatRole.Driver)
            {
                if (kb.digit2Key.wasPressedThisFrame && Vehicle.IsSeatFree(VehicleSeatRole.Gunner))
                {
                    SwitchSeat(VehicleSeatRole.Gunner);
                    return;
                }

                vb.IsPlayerDriving = true;
                if (kb.gKey.isPressed)
                {
                    motor.Brake(Time.deltaTime);
                }
                else
                {
                    float throttle = (kb.wKey.isPressed ? 1f : 0f) + (kb.sKey.isPressed ? -1f : 0f);
                    float steer = (kb.dKey.isPressed ? 1f : 0f) + (kb.aKey.isPressed ? -1f : 0f);
                    motor.Drive(throttle, steer, Time.deltaTime);
                }

                UpdateVehicleCamera(Vehicle.transform.Find("DriverEye"));
            }
            else if (currentSeat == VehicleSeatRole.Gunner)
            {
                if (kb.digit1Key.wasPressedThisFrame && Vehicle.IsSeatFree(VehicleSeatRole.Driver))
                {
                    SwitchSeat(VehicleSeatRole.Driver);
                    return;
                }

                if (mouse != null && turret != null)
                {
                    var delta = mouse.delta.ReadValue();
                    turret.RotateYaw(delta.x * lookSensitivity);
                    if (mouse.leftButton.wasPressedThisFrame) turret.TryFire();
                }

                // Clic derecho: ordenarle a la camioneta ir a un punto (la maneja la IA).
                if (mouse != null && mouse.rightButton.wasPressedThisFrame && Rig.Cam != null)
                {
                    var groundRay = Rig.Cam.ScreenPointToRay(mouse.position.ReadValue());
                    var res = Aim.Evaluate(groundRay, null);
                    if (res.Type == AimTargetType.Ground) TryIssueVehicleMoveOrder(res.Point);
                }

                var gunnerEye = turret != null ? turret.transform.Find("GunnerEye") : null;
                UpdateVehicleCamera(gunnerEye != null ? gunnerEye : Vehicle.transform);
            }
            else
            {
                UpdateVehicleCamera(Vehicle.transform);
            }

            string role = currentSeat == VehicleSeatRole.Driver
                ? "[WASD] conducir · [G] frenar · [2] ir a la torreta · [V] camara · [E] bajar"
                : currentSeat == VehicleSeatRole.Gunner
                    ? "[Mouse] apuntar torreta · [Click] disparar · [Click der.] mandar la camioneta ahi (si hay conductor) · [1] volver a conducir · [V] camara · [E] bajar"
                    : "[E] bajar · [V] camara";
            SetInstructionText(role);
        }

        public void SwitchSeat(VehicleSeatRole newRole)
        {
            var soldier = Brain.Current;
            var vb = Vehicle.GetComponent<VehicleBrain>();

            // Libera el asiento actual sin reaparecer al soldado afuera.
            Vehicle.Dismount(soldier);
            soldier.gameObject.SetActive(false);
            Vehicle.Mount(soldier, newRole);

            if (currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
            currentSeat = newRole;
            if (newRole == VehicleSeatRole.Driver) vb.IsPlayerDriving = true;
        }

        void UpdateVehicleCamera(Transform anchor)
        {
            if (vehicleFirstPerson && anchor != null) Rig.FollowAnchor(anchor);
            else Rig.FollowThirdPerson(Vehicle.transform, 8f, 3.5f);
        }

        // -----------------------------------------------------------
        // RTS
        // -----------------------------------------------------------
        void UpdateRts(Keyboard kb, Mouse mouse)
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
                if (Mathf.Abs(scroll) > 0.01f) Rig.Zoom(scroll * rtsZoomSpeed * Time.deltaTime);
            }

            SetInstructionText($"[Arrastrar] seleccionar varios · [Shift+Click] sumar · [T] mover selección · [G] subir al vehículo · [F] poseer · [TAB] vista FPS · {Selection.Selected.Count} seleccionados");

            if (mouse == null || Rig.Cam == null) return;

            UpdateDragSelection(kb, mouse);

            var screenRay = Rig.Cam.ScreenPointToRay(mouse.position.ReadValue());

            if (kb.tKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Ground)
                    OrderService.IssueMoveOrderForSelection(Selection.Selected, result.Point);
            }

            if (kb.gKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Vehicle)
                {
                    if (result.Vehicle.OccupantCount > 0) DismountAll(result.Vehicle);
                    else OrderService.IssueMountOrderForSelection(Selection.Selected, result.Vehicle);
                }
            }

            if (kb.fKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Ally)
                {
                    PossessionService.Swap(Brain, result.Soldier);
                    Rig.SetMode(ControlMode.Fps);
                }
            }
        }

        // Clic simple = seleccionar uno (o sumar con Shift). Arrastrar dibuja
        // un cuadro y selecciona a todos los aliados que caen adentro, como
        // en cualquier RTS estilo Age of Empires.
        void UpdateDragSelection(Keyboard kb, Mouse mouse)
        {
            Vector2 mousePos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                dragStart = mousePos;
                if (SelectionBox != null) SelectionBox.gameObject.SetActive(true);
            }

            if (dragging && mouse.leftButton.isPressed)
            {
                UpdateSelectionBoxVisual(dragStart, mousePos);
            }

            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                if (SelectionBox != null) SelectionBox.gameObject.SetActive(false);

                float dist = Vector2.Distance(dragStart, mousePos);
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

                if (dist < dragThresholdPixels)
                {
                    var ray = Rig.Cam.ScreenPointToRay(mousePos);
                    var result = Aim.Evaluate(ray, null);
                    if (result.Type == AimTargetType.Ally)
                    {
                        if (shift) Selection.AddToSelection(result.Soldier);
                        else Selection.SelectSingle(result.Soldier);
                    }
                    else if (!shift)
                    {
                        Selection.Clear();
                    }
                }
                else
                {
                    SelectAlliesInScreenRect(dragStart, mousePos, shift);
                }
            }
        }

        void UpdateSelectionBoxVisual(Vector2 a, Vector2 b)
        {
            if (SelectionBox == null) return;
            var rt = SelectionBox.rectTransform;
            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
            rt.anchoredPosition = new Vector2(minX, minY);
            rt.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        }

        void SelectAlliesInScreenRect(Vector2 a, Vector2 b, bool addToExisting)
        {
            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);

            bool first = !addToExisting;
            bool any = false;

            foreach (var s in Squad)
            {
                if (s == null || s.Team != TeamId.Player || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;

                var sp = Rig.Cam.WorldToScreenPoint(s.transform.position);
                if (sp.z < 0f) continue;
                if (sp.x < minX || sp.x > maxX || sp.y < minY || sp.y > maxY) continue;

                any = true;
                if (first) { Selection.SelectSingle(s); first = false; }
                else Selection.AddToSelection(s);
            }

            if (!any && !addToExisting) Selection.Clear();
        }
    }
}
