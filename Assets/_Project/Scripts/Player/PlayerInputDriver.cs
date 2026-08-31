using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.CameraSystem;
using SP.Core;
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
        // Publico (no privado) a proposito: asi Unity lo serializa y la
        // referencia sobrevive el domain reload al entrar a Play.
        public DamageVignetteView DamageVignette;
        // Formacion con la que se emiten las ordenes de movimiento. Cuadricula
        // es la de siempre, asi que el comportamiento por defecto no cambia.
        FormationKind currentFormation = FormationKind.Cuadricula;
        public PlayerHealthView PlayerHealth;
        public UI.SelectionCountView SelectionCount;
        public UI.ModeToastView ModeToast;
        public InstructionBannerView Instructions;
        public Image SelectionBox;
        public Vehicle Vehicle;
        public List<WeaponPickup> WeaponPickups;
        public MinimapFollow MinimapRef;
        public DeadNoticeView DeadNotice;
        public WeaponStatusView WeaponStatus;
        public VehicleStatusView VehicleStatus;
        public TurretAimView TurretAim;
        public GameOutcomeController Outcome;
        public PauseController PauseRef;

        [SerializeField] float lookSensitivity = 0.15f;
        // El slider de "Sensibilidad de mouse" en Configuraciones antes
        // no hacía nada de verdad (solo se veía, no afectaba el juego) --
        // esta propiedad es lo que lo conecta a algo real.
        public float LookSensitivity { get => lookSensitivity; set => lookSensitivity = value; }

        // Antes la torreta usaba la misma sensibilidad que mirar a pie:
        // son dos gestos de escala muy distinta (mirar con el cuerpo vs.
        // girar un cañon), ajustar uno arruinaba el otro.
        [SerializeField] float turretSensitivity = 0.15f;
        public float TurretSensitivity { get => turretSensitivity; set => turretSensitivity = value; }

        // Requisito de accesibilidad basico y preferencia muy comun en
        // shooters: sin esto no habia forma de invertir el eje vertical.
        public bool InvertLookY { get; set; }
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

        // Cubo pegado a la cámara (no al cuerpo): así se ve en primera
        // persona el arma equipada apuntando siempre hacia donde mirás,
        // con su propia forma/color según qué arma tenés en mano.
        GameObject weaponViewmodel;
        Renderer weaponViewmodelRenderer;

        // Retroceso visual del arma al disparar: sin esto el viewmodel
        // queda perfectamente inmovil pase lo que pase, y disparar se
        // siente como apretar un boton en vez de usar un arma. recoilKick
        // arranca en 1 en cada disparo y decae hacia 0; el desplazamiento
        // real se aplica en Z (hacia la camara) en UpdateWeaponViewmodel.
        float recoilKick;
        float emptyClickCooldown;
        IDisposable shotSub;

        void OnShotFiredForRecoil(ShotFiredEvent evt)
        {
            if (Brain == null || Brain.Current == null || evt.ShooterId != Brain.Current.Id) return;
            recoilKick = 1f;
            // Culatazo real de camara, no solo del cubo del arma: sube la
            // mira un poco con cada disparo y se recupera sola. La
            // magnitud depende del arma (Heavy patea mas que Pistol),
            // igual que ya varia el retroceso del viewmodel via
            // WeaponCatalog.
            var spec = WeaponCatalog.Get(Brain.Current.Weapon.CurrentWeaponKind);
            // El daño ya varia por arma en el catalogo (Heavy pega mas
            // fuerte que Pistol): un proxy razonable de "cuanto empuja"
            // sin sumar un campo de recoil nuevo al catalogo.
            float kickDeg = Mathf.Clamp(spec.Damage * 0.025f, 0.6f, 3f);
            Rig.KickRecoil(kickDeg);
        }

        void UpdateWeaponViewmodel(WeaponHolder weapon)
        {
            if (Rig == null || Rig.Cam == null || weapon == null) return;

            if (weaponViewmodel == null)
            {
                weaponViewmodel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                weaponViewmodel.name = "WeaponViewmodel";
                var col = weaponViewmodel.GetComponent<Collider>();
                if (col != null) Destroy(col);
                weaponViewmodel.transform.SetParent(Rig.Cam.transform, false);
                // Un poco más lejos y más grande que el cubo del cuerpo: tan
                // cerca de la cámara y tan fino, casi no se veía (se perdía
                // contra el cielo, muy parecido de color). Corrido del
                // rincón inferior derecho, que es donde vive el HUD del
                // arma (le tapaba el viewmodel por encima).
                weaponViewmodel.transform.localPosition = new Vector3(0.28f, -0.22f, 0.65f);
                weaponViewmodel.transform.localRotation = Quaternion.identity;
                weaponViewmodelRenderer = weaponViewmodel.GetComponent<MeshRenderer>();
                // Unlit a propósito: con el shader Lit, bajo la luz plana de
                // la escena, un color como el del Rifle (celeste grisáceo)
                // queda casi idéntico al cielo de fondo y el cubo desaparece
                // a simple vista aunque esté perfectamente ubicado y activo.
                // Unlit + oscurecido garantiza contraste sin depender de la
                // iluminación de la escena.
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                weaponViewmodelRenderer.sharedMaterial = new Material(shader);
            }

            weaponViewmodel.SetActive(true);
            var spec = WeaponCatalog.Get(weapon.CurrentWeaponKind);
            // OJO: escalar spec.VisualScale (pensado para el cuerpo, con
            // el largo del cañón en Z) de golpe x2/x4 y ubicarlo a solo
            // 0.55-0.7 de la cámara hacía que la mitad del cubo en Z
            // quedara DETRÁS del punto focal de la cámara (near clip
            // 0.3), y ese cruce lo dejaba totalmente fuera del frustum:
            // por eso no se veía pese a estar activo, bien coloreado y
            // "dentro de cámara" según todo diagnóstico salvo la
            // profundidad real. Ancho/alto escalan con el arma pero la
            // profundidad se cablea fija y chica, y la distancia a la
            // cámara se aleja lo suficiente como para dejar margen real
            // delante del near clip.
            // Primer intento (0.18-0.35 de ancho) resultó gigante: tapaba
            // media pantalla en las capturas reales del demo. Un arma en
            // primera persona debe leerse como un detalle en la esquina,
            // no como una pared — bajado a un rango bien chico.
            float widthHeight = Mathf.Clamp(spec.VisualScale.x * 1.1f, 0.08f, 0.15f);
            const float depth = 0.22f;
            weaponViewmodel.transform.localScale = new Vector3(widthHeight, widthHeight, depth);
            weaponViewmodelRenderer.sharedMaterial.color = Color.Lerp(spec.Color, Color.black, 0.4f);

            // El retroceso decae rapido (recupera en ~0.12s) y empuja el
            // arma hacia la camara (Z local mas chico) y un poco hacia
            // arriba, volviendo sola a su lugar -- un punch, no un lerp
            // parejo, para que se note el golpe del disparo.
            recoilKick = Mathf.MoveTowards(recoilKick, 0f, Time.deltaTime * 8f);
            const float maxKickZ = 0.09f;
            const float maxKickY = 0.03f;
            weaponViewmodel.transform.localPosition = new Vector3(
                0.28f,
                -0.22f + maxKickY * recoilKick,
                0.65f - maxKickZ * recoilKick);
        }

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

        IDisposable deathSub;
        IDisposable vehicleDestroyedSub;
        IDisposable turretControlSub;
        IDisposable squadDamageSub;
        void OnEnable()
        {
            deathSub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnEntityDied);
            vehicleDestroyedSub = EventBus.Instance.Subscribe<VehicleDestroyedEvent>(OnVehicleDestroyed);
            turretControlSub = EventBus.Instance.Subscribe<TurretControlChangedEvent>(OnTurretControlChanged);
            shotSub = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFiredForRecoil);
            squadDamageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnSquadDamage);
        }
        void OnDisable()
        {
            deathSub?.Dispose();
            vehicleDestroyedSub?.Dispose();
            turretControlSub?.Dispose();
            shotSub?.Dispose();
            squadDamageSub?.Dispose();
            // Si el objeto se apaga a mitad de la camara de muerte, los
            // objetos temporales de esa escena no tienen quien los borre.
            CleanupDeathSequence();
        }

        // Si atacaban a un aliado que no estabas controlando, no te
        // enterabas hasta que ya habia muerto (el aviso de DeadNotice
        // solo dispara con la muerte). Ahora avisa apenas empieza el
        // ataque, con una ventana minima entre avisos por soldado para
        // no saturar con una notificacion por bala.
        readonly System.Collections.Generic.Dictionary<int, float> lastAttackAlert = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.HashSet<int> lowHealthWarned = new System.Collections.Generic.HashSet<int>();
        const float LowHealthThreshold = 0.3f;
        const float AttackAlertCooldown = 4f;

        void OnSquadDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || Squad == null || DeadNotice == null) return;
            if (Brain.Current != null && evt.TargetId == Brain.Current.Id) return; // el propio ya tiene su vignette

            Soldier victim = null;
            foreach (var s in Squad) if (s != null && s.Id == evt.TargetId) { victim = s; break; }
            if (victim == null || !victim.Health.IsAlive) return;

            if (!lastAttackAlert.TryGetValue(victim.Id, out var last) || Time.time - last > AttackAlertCooldown)
            {
                lastAttackAlert[victim.Id] = Time.time;
                DeadNotice.Show($"{victim.DisplayName} esta bajo ataque", 2f);
            }

            // Aviso de vida critica: una sola vez por caida por debajo del
            // umbral, no una vez por bala mientras siga por debajo.
            float frac = victim.Health.MaxHealth > 0 ? (float)victim.Health.Current / victim.Health.MaxHealth : 1f;
            if (frac <= LowHealthThreshold)
            {
                if (!lowHealthWarned.Contains(victim.Id))
                {
                    lowHealthWarned.Add(victim.Id);
                    DeadNotice.Show($"{victim.DisplayName} tiene poca vida", 2f);
                }
            }
            else
            {
                lowHealthWarned.Remove(victim.Id);
            }
        }

        // Solo avisa si el que reventó es el vehículo donde está el
        // jugador ahora mismo -- un tanque enemigo o aliado destruido en
        // otra punta del mapa no debería interrumpir con un aviso.
        void OnVehicleDestroyed(VehicleDestroyedEvent evt)
        {
            if (currentSeat.HasValue && Vehicle != null && Vehicle == evt.Vehicle)
                if (ModeToast != null) ModeToast.Show("VEHICULO DESTRUIDO", 1.6f);
        }

        // TurretAI cede el control al haber artillero humano y lo retoma
        // al bajarse, pero ese traspaso era invisible: el jugador veia la
        // torreta moverse sola sin saber por que. Igual que el aviso de
        // vehiculo destruido, solo interesa el vehiculo donde esta ahora.
        void OnTurretControlChanged(TurretControlChangedEvent evt)
        {
            if (!currentSeat.HasValue || Vehicle == null || Vehicle != evt.Vehicle) return;
            if (ModeToast == null) return;
            ModeToast.Show(evt.AiInControl ? "TORRETA EN AUTOMATICO" : "TORRETA BAJO TU CONTROL", 1.4f);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Pausa/menú de victoria-derrota tienen Time.timeScale=0, pero
            // Update() no se frena solo por eso: sin este corte, mientras
            // el panel de pausa está en pantalla el jugador podía seguir
            // moviéndose, disparando y girando la cámara por detrás.
            if (PauseRef != null && PauseRef.IsPaused) return;

            // [H] consulta los controles sin pausar el juego -- sigue
            // corriendo la simulacion (a diferencia de abrirlo desde
            // pausa), pero congela la entrada del jugador mientras esta
            // abierto para no mover ni disparar por error mientras lee.
            if (kb.hKey.wasPressedThisFrame && PauseRef != null) PauseRef.ToggleControlsOverlay();
            if (PauseRef != null && PauseRef.IsControlsOverlayOpen) return;

            UpdateCursorLock(kb, Mouse.current);

            if (MinimapRef != null)
                MinimapRef.Target = currentSeat.HasValue ? Vehicle.transform : (Brain.Current != null ? Brain.Current.transform : null);

            // El [TAB] se procesa ANTES del corte por "estoy adentro de un
            // vehículo": antes, estando adentro, Tab no hacía nada (el
            // return de UpdateInVehicle lo comía entero) -- ahora alterna
            // entre manejar en primera persona y ver el auto desde arriba
            // en RTS, sin bajarse ni perder el asiento.
            if (kb.tabKey.wasPressedThisFrame && !handlingDeath)
            {
                // Si Tab te saca de RTS a mitad de un arrastre de
                // selección, el cuadrito quedaba prendido en pantalla
                // para siempre (nada lo apagaba hasta el próximo drag
                // completo en RTS, y para entonces ya no tenía sentido
                // dónde estaba dibujado).
                if (dragging)
                {
                    dragging = false;
                    if (SelectionBox != null) SelectionBox.gameObject.SetActive(false);
                }

                Rig.ToggleMode();
                // Marca que el jugador ya descubrio el cambio de modo, para
                // que el recordatorio de GameplaySceneBootstrap no vuelva a
                // aparecer nunca mas en ninguna partida futura.
                PlayerPrefs.SetInt("sp_used_tab", 1);

                if (Rig.Mode == ControlMode.Fps && !currentSeat.HasValue && Brain.Current != null && Vehicle != null)
                {
                    var role = Vehicle.RoleOf(Brain.Current);
                    if (role != null) EnterPossessedVehicleSeat(role.Value);
                }

                if (Rig.Mode == ControlMode.Rts)
                {
                    Vector3 focus = currentSeat.HasValue ? Vehicle.transform.position
                        : Brain.Current != null ? Brain.Current.transform.position : Vector3.zero;
                    // Restaura el paneo/zoom que el jugador dejo la ultima
                    // vez que estuvo en RTS, en vez de recentrar siempre
                    // en el poseido -- si no hay vista guardada (primera
                    // vez), cae a centrar en foco como antes.
                    Rig.RestoreOrSetRtsView(focus);
                }

                if (ModeToast != null) ModeToast.Show(Rig.Mode == ControlMode.Rts ? "VISTA RTS" : "VISTA FPS");
                // 184: el salto entre vista FPS y RTS era un corte
                // seco. Un destello gris muy corto lo lee como una
                // transicion. No va dentro de CameraRig.SetMode: eso
                // tambien lo llama la secuencia de muerte, donde un
                // flash encima de la camara de muerte seria un
                // accidente visual.
                SP.UI.ScreenFlashView.ModeChange();
            }

            if (handlingDeath) return;

            if (currentSeat.HasValue)
            {
                UpdateInVehicle(kb, Mouse.current);
                return;
            }

            // [F1]/[F2]/[F3]: posee directamente al soldado 1/2/3 del
            // escuadrón, sin tener que apuntarle primero.
            if (kb.f1Key.wasPressedThisFrame) PossessSquadIndex(0);
            if (kb.f2Key.wasPressedThisFrame) PossessSquadIndex(1);
            if (kb.f3Key.wasPressedThisFrame) PossessSquadIndex(2);
            // [Q] cicla entre vivos y [C] posee al mas cercano: ambas caen
            // bajo la mano izquierda sin soltar WASD, a diferencia de F1/F2/F3.
            if (KeyBindings.WasPressed(KeyBindings.CiclarPosesion)) CycleLivingAlly(+1);
            // 199: solo se podia ciclar hacia ADELANTE. Con una escuadra de
            // tres eso ya obliga a dar la vuelta entera para volver uno.
            if (KeyBindings.WasPressed(KeyBindings.CiclarPosesionAtras)) CycleLivingAlly(-1);
            if (KeyBindings.WasPressed(KeyBindings.PoseerMasCercano)) PossessNearestAlly();

            if (Rig.Mode == ControlMode.Fps) UpdateFps(kb, Mouse.current);
            else UpdateRts(kb, Mouse.current);
        }

        // -----------------------------------------------------------
        // Muerte del soldado poseído: la cámara se aleja mirando el
        // cadáver, espera un momento, y pasa sola al aliado vivo más
        // cercano -- o a vista RTS si no queda ninguno.
        // -----------------------------------------------------------
        bool handlingDeath;
        // Para que PauseController no abra la pausa a mitad de la
        // cámara de muerte -- técnicamente no rompía nada (se congela
        // bien y sigue al continuar), pero pausar en medio de esa
        // escena breve se siente como una interrupción rara, no
        // intencional.
        public bool IsHandlingDeath => handlingDeath;

        // El anillo del asesino y el punto de camara de la muerte eran
        // locales de la corrutina: si esta se cortaba a mitad, nadie los
        // destruia nunca y quedaban clavados en escena. Como campos del
        // componente siempre hay quien los limpie (CleanupDeathSequence).
        SelectionRingFx deathKillerRing;
        GameObject deathPullBackGO;

        void OnEntityDied(EntityDiedEvent evt)
        {
            if (!Application.isPlaying || Brain.Current == null) return;

            if (evt.ActorId == Brain.Current.Id)
            {
                if (handlingDeath) return;
                StartCoroutine(DeathSequence(Brain.Current));
                return;
            }

            // Antes, si moría un aliado que NO estabas manejando, no te
            // enterabas de nada hasta que intentabas poseerlo con
            // F1/F2/F3 (ahí sí salía "está muerto"). Ahora también avisa
            // en el momento, aunque estés mirando para otro lado.
            if (Squad == null || DeadNotice == null) return;
            foreach (var s in Squad)
            {
                if (s != null && s.Id == evt.ActorId)
                {
                    DeadNotice.Show($"{s.DisplayName} esta muerto");
                    GameLog.Line($"{s.DisplayName} murio");
                    break;
                }
            }
        }

        IEnumerator DeathSequence(Soldier deadSoldier)
        {
            handlingDeath = true;
            // try/finally porque esta corrutina no siempre llega al final:
            // si la escuadra remata al ultimo enemigo mientras corre la
            // camara de muerte, BattleManager llama Outcome.ShowVictory()
            // y eso pone Time.timeScale = 0, con lo cual el bucle de
            // orbita (que avanza con Time.deltaTime) no termina nunca.
            // Antes eso dejaba el anillo rojo del asesino clavado sobre el
            // cadaver encima de la pantalla de victoria -- los soldados no
            // se destruyen al morir, asi que el autodestruirse por
            // Target == null de SelectionRingFx tampoco lo limpiaba -- y
            // handlingDeath en true para siempre, lo que bloqueaba toda
            // futura DeathSequence y dejaba a PauseController creyendo que
            // seguia la camara de muerte. Unity descarta el iterador al
            // frenar la corrutina o desactivar el objeto, asi que el
            // finally corre igual. (yield adentro de try/finally es legal
            // en C#; lo prohibido es yield adentro de catch.)
            try
            {
                if (WeaponStatus != null) WeaponStatus.gameObject.SetActive(false);
                if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
                if (TurretAim != null) TurretAim.SetVisible(false);
                if (weaponViewmodel != null) weaponViewmodel.SetActive(false);
                if (AimUiRef != null) AimUiRef.SetVisible(false);
                if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false);
                deadSoldier.SetBodyVisible(true);
                bodyHiddenFor = null;

                // La camara de muerte mostraba el cadaver propio pero no decia
                // QUIEN te mato, que es lo que el jugador mas quiere saber en
                // ese momento. El ultimo atacante ya queda registrado en el
                // Health del caido.
                deathKillerRing = null;
                var killer = ActorRegistry.FindById(deadSoldier.Health.LastAttackerId);
                if (killer != null && killer.Health.IsAlive)
                {
                    deathKillerRing = SelectionRingFx.Spawn(killer.transform, new Color(1f, 0.3f, 0.2f), 1.1f);
                    if (DeadNotice != null) DeadNotice.Show($"Te mato {killer.DisplayName}");
                }

                // Punto de cámara "detrás y arriba" del cadáver, mirándolo --
                // un GameObject temporal porque BeginTransition necesita un
                // Transform de destino, no una posición suelta.
                deathPullBackGO = new GameObject("DeathCamPullback");
                Vector3 back = -deadSoldier.transform.forward * 4f + Vector3.up * 2.2f;
                deathPullBackGO.transform.position = deadSoldier.transform.position + back;
                deathPullBackGO.transform.rotation = Quaternion.LookRotation((deadSoldier.transform.position + Vector3.up * 0.8f - deathPullBackGO.transform.position).normalized);

                Rig.SetMode(ControlMode.Fps);
                Rig.BeginTransition(deathPullBackGO.transform, 0.9f);
                while (Rig.IsTransitioning) yield return null;

                // 3 segundos mirando al cadáver, orbitando despacio alrededor
                // (no una cámara congelada): "rotando mirándolo por 3
                // segundos". Mismo radio/altura que el punto de partida, solo
                // gira el ángulo alrededor del soldado.
                const float holdSeconds = 3f;
                const float orbitDegPerSec = 12f;
                Vector3 toCam = deathPullBackGO.transform.position - deadSoldier.transform.position;
                float radius = new Vector2(toCam.x, toCam.z).magnitude;
                float height = toCam.y;
                float angle = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;

                float t = 0f;
                while (t < holdSeconds)
                {
                    t += Time.deltaTime;
                    angle += orbitDegPerSec * Time.deltaTime;
                    float rad = angle * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);
                    deathPullBackGO.transform.position = deadSoldier.transform.position + offset;
                    deathPullBackGO.transform.rotation = Quaternion.LookRotation((deadSoldier.transform.position + Vector3.up * 0.8f - deathPullBackGO.transform.position).normalized);
                    Rig.FollowAnchor(deathPullBackGO.transform);
                    yield return null;
                }

                Soldier nearest = null;
                float bestDist = float.MaxValue;
                if (Squad != null)
                {
                    foreach (var s in Squad)
                    {
                        if (s == null || s == deadSoldier || !s.Health.IsAlive) continue;
                        float d = Vector3.Distance(deadSoldier.transform.position, s.transform.position);
                        if (d < bestDist) { bestDist = d; nearest = s; }
                    }
                }

                if (nearest != null)
                {
                    GameLog.Line($"Camara cambio de {deadSoldier.DisplayName} a {nearest.DisplayName} (aliado vivo mas cercano)");
                    PossessionService.Swap(Brain, nearest);
                    Rig.SetMode(ControlMode.Fps);
                    Rig.BeginTransition(nearest.EyeAnchor != null ? nearest.EyeAnchor : nearest.transform);
                }
                else
                {
                    GameLog.Line("Perdiste");
                    Rig.SetMode(ControlMode.Rts);
                    Rig.SetRtsView(deadSoldier.transform.position);
                    if (Outcome != null) Outcome.ShowDefeat();
                }
            }
            finally
            {
                // El resalte del asesino y el punto de camara duran solo lo
                // que dura la camara de muerte: dejarlos puestos confundiria
                // al anillo con una seleccion.
                CleanupDeathSequence();
            }
        }

        // Un solo lugar que deja la camara de muerte sin residuos: lo llama
        // el finally de la corrutina (camino feliz o corte a mitad) y
        // tambien OnDisable, por si el objeto se apaga antes de que Unity
        // llegue a descartar el iterador. Es idempotente a proposito, asi
        // que correr las dos veces no rompe nada.
        void CleanupDeathSequence()
        {
            if (deathKillerRing != null)
            {
                // Destruir el GameObject NO libera el Material creado en
                // runtime -- quedaria huerfano hasta cambiar de escena.
                // Mismo criterio que KillFeedbackDirector.SilhouetteFlash.
                var mr = deathKillerRing.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
                Destroy(deathKillerRing.gameObject);
                deathKillerRing = null;
            }

            if (deathPullBackGO != null)
            {
                Destroy(deathPullBackGO);
                deathPullBackGO = null;
            }

            handlingDeath = false;
        }

        // El cursor arranca libre (para poder clickear la UI/el juego). Al
        // primer click adentro se bloquea y esconde, como cualquier FPS; con
        // Escape se libera de nuevo. En vista RTS lo dejamos libre siempre,
        // porque ahí el mouse selecciona y arrastra en vez de mirar.
        void UpdateCursorLock(Keyboard kb, Mouse mouse)
        {
            // Antes también se bloqueaba con solo currentSeat.HasValue,
            // sin mirar el modo -- si estabas manejando un vehiculo y
            // pasabas a vista RTS con [TAB] (sin bajarte), el cursor
            // seguia preso e invisible, aunque esa vista es igual de
            // "arriba mirando el mapa" que la RTS de a pie, donde el
            // mouse siempre queda libre para clickear.
            bool wantsLock = Rig.Mode == ControlMode.Fps;

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
        // A quién se le ocultó el cuerpo por estar poseído en FPS (la
        // cámara vive a centímetros de su propio EyeAnchor, y sin esto
        // su propia malla tapa la pantalla). Se restaura apenas deja de
        // ser el poseído o se sale de FPS.
        Soldier bodyHiddenFor;

        void UpdateFps(Keyboard kb, Mouse mouse)
        {
            if (Brain.Current == null) return;
            if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
            if (TurretAim != null) TurretAim.SetVisible(false);
            if (AimUiRef != null)
            {
                AimUiRef.SetVisible(true);
                AimUiRef.SetWatchedShooter(Brain.Current.Id);
                AimUiRef.SetSpread01(Brain.Current.Weapon.SpreadFraction01);
            }
            if (SelectionCount != null) SelectionCount.SetModeVisible(false);

            if (bodyHiddenFor != Brain.Current)
            {
                if (bodyHiddenFor != null) bodyHiddenFor.SetBodyVisible(true);
                Brain.Current.SetBodyVisible(false);
                bodyHiddenFor = Brain.Current;
            }

            Vector3 f = Brain.Current.transform.forward;
            Vector3 r = Brain.Current.transform.right;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += f;
            if (kb.sKey.isPressed) move -= f;
            if (kb.dKey.isPressed) move += r;
            if (kb.aKey.isPressed) move -= r;
            bool moving = move.sqrMagnitude > 0.0001f;
            if (moving) Brain.Move(move.normalized, Time.deltaTime);
            // Balanceo al caminar: caminar y estar quieto se veian
            // exactamente igual, sin ninguna sensacion de pisada.
            Rig.SetWalking(moving);

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = mouse.delta.ReadValue();
                Brain.RotateYaw(delta.x * lookSensitivity);
                Rig.AddPitch(delta.y * lookSensitivity * (InvertLookY ? -1f : 1f));
            }

            Rig.FollowFps(Brain.Current);
            UpdateNearestAllyHighlight();

            var ray = Rig.GetForwardRay();
            var result = Aim.Evaluate(ray, Brain.Current);
            UpdateAimHighlight(result);
            UpdateVehicleMountIndicator(result);
            if (AimUiRef != null) AimUiRef.UpdateFromAimResult(result);
            if (WeaponStatus != null) WeaponStatus.UpdateFrom(Brain.Current.Weapon);
            if (AimUiRef != null) AimUiRef.UpdateAmmoWarning(Brain.Current.Weapon);
            if (kb.rKey.wasPressedThisFrame) Brain.Current.Weapon.Reload();
            if (PlayerHealth != null)
            {
                PlayerHealth.gameObject.SetActive(true);
                PlayerHealth.UpdateFrom(Brain.Current);
            }
            UpdateWeaponViewmodel(Brain.Current.Weapon);

            // isPressed (no wasPressedThisFrame): antes habia que
            // clickear una vez por bala incluso con un rifle. Ahora
            // mantener el boton dispara a la cadencia real del arma
            // (fireCooldown), que ya es distinta por WeaponKind.
            if (mouse != null && mouse.leftButton.isPressed)
            {
                bool emptyBeforeFire = Brain.Current.Weapon.CurrentAmmo <= 0 && !Brain.Current.Weapon.IsReloading;
                bool fired = Brain.Fire();
                // Clic seco de gatillo vacio: solo si de verdad no
                // disparo por falta de municion (no por estar en
                // cooldown normal entre tiros, que no deberia sonar
                // como un fallo).
                if (!fired && emptyBeforeFire && emptyClickCooldown <= 0f)
                {
                    emptyClickCooldown = 0.3f;
                    AudioSource.PlayClipAtPoint(GenericSfx.Get(SfxKind.EmptyClick), Rig.transform.position, 0.5f);
                }
            }
            emptyClickCooldown = Mathf.Max(0f, emptyClickCooldown - Time.deltaTime);

            if (kb.digit1Key.wasPressedThisFrame) EquipFromCatalog(WeaponKind.Rifle);
            if (kb.digit2Key.wasPressedThisFrame) EquipFromCatalog(WeaponKind.Pistol);
            if (kb.digit3Key.wasPressedThisFrame) EquipFromCatalog(WeaponKind.Heavy);

            // 206: cambiar de arma con la rueda, la convencion del genero.
            // No colisiona con el zoom RTS por construccion: esta rama solo
            // se alcanza con Rig.Mode == Fps y sin asiento de vehiculo, y
            // los dos lectores de rueda para zoom viven en ramas de RTS.
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) CycleWeapon(wheel > 0f ? +1 : -1);
            }

            if (kb.fKey.wasPressedThisFrame && result.Type == AimTargetType.Ally)
                TryPossess(result.Soldier);

            if (kb.tKey.wasPressedThisFrame && result.Type == AimTargetType.Ground)
            {
                var nearest = OrderService.FindNearestFreeAlly(result.Point, TeamId.Player, Brain.Current);
                if (nearest != null) OrderService.IssueMoveOrder(nearest, result.Point);
            }

            if (kb.gKey.wasPressedThisFrame && result.Type == AimTargetType.Vehicle)
                GOrderOnVehicle(result.Vehicle);

            // Mantener click derecho apretado: zoom de mirilla (no manda la
            // camioneta hasta que se suelta, eso sigue siendo un click).
            if (mouse != null) Rig.SetZoomed(mouse.rightButton.isPressed);

            // Orden a la camioneta: clic derecho sobre el suelo, viajando sola.
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && result.Type == AimTargetType.Ground)
                TryIssueVehicleMoveOrder(result.Point);

            // Interacción por cercanía (no por puntería): subir al vehículo
            // o equipar un arma tirada en el piso.
            var nearVehicle = Vehicle != null && Vector3.Distance(Brain.Current.transform.position, Vehicle.transform.position) <= interactRadius
                ? Vehicle : null;
            var nearPickup = FindNearestPickup(Brain.Current.transform.position);

            // 201: antes [E] hacia las dos cosas y el vehiculo ganaba
            // siempre, asi que parado al lado de un vehiculo Y de un arma
            // tirada, el arma era INALCANZABLE. Ahora [X] es subir/bajar y
            // [E] queda como interactuar puro, prefiriendo el pickup. [E]
            // sobre el vehiculo se conserva como alias heredado para no
            // romper la memoria muscular de golpe.
            if (KeyBindings.WasPressed(KeyBindings.SubirBajarVehiculo) && nearVehicle != null)
            {
                EnterVehicle(nearVehicle);
            }
            else if (KeyBindings.WasPressed(KeyBindings.Interactuar))
            {
                if (nearPickup != null) nearPickup.EquipOn(Brain.Current.Weapon, Brain.Current.Id);
                else if (nearVehicle != null) EnterVehicle(nearVehicle);
            }

            SetInstructionText(nearVehicle != null ? "[E] Subir al vehiculo (se suben los aliados cercanos)"
                : nearPickup != null ? $"[E] Equipar {nearPickup.Kind}"
                : BuildFpsInstruction(result));
        }

        // Resalta (aclara el color) el aliado o vehículo al que se le está
        // apuntando, y le devuelve su color original apenas se deja de
        // apuntarle o se apunta a otra cosa.
        // Para poseer a un aliado hay que apuntarle con precision, sin
        // ninguna pista de cual esta en rango util -- este anillo marca
        // al vivo mas cercano (excluyendo al propio poseido) para que el
        // jugador sepa a quien puede cambiar sin tener que girar la
        // camara buscando. Se recalcula por intervalo, no por frame: no
        // hace falta la precision de un frame para "quien esta mas cerca".
        const float NearestAllyRange = 15f;
        const float NearestAllyCheckInterval = 0.35f;
        float nextNearestAllyCheck;
        Soldier nearestAllyHighlighted;
        SelectionRingFx nearestAllyRing;
        static readonly Color NearestAllyRingColor = new Color(0.4f, 0.85f, 1f, 0.8f);

        void UpdateNearestAllyHighlight()
        {
            if (Squad == null || Time.time < nextNearestAllyCheck) return;
            nextNearestAllyCheck = Time.time + NearestAllyCheckInterval;

            Soldier nearest = null;
            float bestDistSqr = NearestAllyRange * NearestAllyRange;
            foreach (var s in Squad)
            {
                if (s == null || s == Brain.Current || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = (s.transform.position - Brain.Current.transform.position).sqrMagnitude;
                if (d <= bestDistSqr) { bestDistSqr = d; nearest = s; }
            }

            if (nearest == nearestAllyHighlighted) return;
            nearestAllyHighlighted = nearest;

            if (nearestAllyRing != null) { Destroy(nearestAllyRing.gameObject); nearestAllyRing = null; }
            if (nearest != null) nearestAllyRing = SelectionRingFx.Spawn(nearest.transform, NearestAllyRingColor, 0.85f);
        }

        void ClearNearestAllyHighlight()
        {
            if (nearestAllyRing != null) { Destroy(nearestAllyRing.gameObject); nearestAllyRing = null; }
            nearestAllyHighlighted = null;
        }

        void UpdateAimHighlight(AimResult result)
        {
            Renderer target = null;
            if ((result.Type == AimTargetType.Ally || result.Type == AimTargetType.Enemy) && result.Soldier != null)
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
            // Ordenar subir a una carcasa destruida antes mandaba al
            // aliado a caminar hasta ahí para nada: Vehicle.Mount() ya
            // rechaza el intento al llegar, pero eso no se sabe hasta
            // que llega -- una caminata entera sin ningún resultado ni
            // aviso.
            if (vehicle.IsDestroyed) return;

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
            GameLog.Line("Se dio la orden de que salgan del auto");
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

        void PossessSquadIndex(int index)
        {
            if (Squad == null || index < 0 || index >= Squad.Count) return;
            TryPossess(Squad[index]);
        }

        // Unico camino de posesion del jugador. Antes cada sitio hacia lo
        // suyo: el [F] desde RTS cambiaba de camara de golpe (sin la
        // transicion que si tenian los atajos F1/F2/F3 y la secuencia de
        // muerte), ninguno avisaba a quien pasaste, ninguno devolvia el
        // pitch a cero, y el rechazo por soldado caido solo existia en un
        // sitio -- en el resto la tecla parecia no funcionar.
        public bool TryPossess(Soldier target)
        {
            if (target == null) return false;

            if (!target.Health.IsAlive)
            {
                if (DeadNotice != null) DeadNotice.Show($"{target.DisplayName} esta muerto: no se puede poseer");
                OrderService.PlayRejectSound();
                return false;
            }
            if (!target.gameObject.activeInHierarchy)
            {
                if (DeadNotice != null) DeadNotice.Show($"{target.DisplayName} esta dentro de un vehiculo");
                OrderService.PlayRejectSound();
                return false;
            }
            if (Brain.Current == target) return false;

            var previous = Brain.Current;
            PossessionService.Swap(Brain, target);

            // El pitch es estado del rig, no del soldado: sin esto heredas
            // el angulo vertical del anterior y podes aparecer mirando al
            // piso sin ningun motivo.
            Rig.ResetPitch();
            Rig.BeginTransition(target.EyeAnchor != null ? target.EyeAnchor : target.transform);
            if (Rig.Mode == ControlMode.Rts) Rig.SetMode(ControlMode.Fps);

            if (ModeToast != null) ModeToast.Show($"CONTROLAS A {target.DisplayName.ToUpperInvariant()}", 1.2f);

            // El anterior recupera su AiBrain y empieza a actuar solo. Sin
            // aviso, el jugador ve moverse a un soldado que creia suyo.
            if (previous != null && previous.Brain != null && !previous.Brain.IsPossessedByPlayer)
                GameLog.Line($"{previous.DisplayName} vuelve al control de la IA");

            return true;
        }

        // Poseer exigia recordar el numero de cada soldado o apuntarle con
        // precision -- ninguna de las dos cosas es viable bajo fuego.
        void PossessNearestAlly()
        {
            if (Brain.Current == null) return;
            var nearest = ActorRegistry.FindNearest(Brain.Current.transform.position, s =>
                s.Health.IsAlive && s.Team == Brain.Current.Team && s != Brain.Current && s.gameObject.activeInHierarchy);
            if (nearest == null) { RejectOrder("NO HAY ALIADO CERCA"); return; }
            TryPossess(nearest);
        }

        // Los atajos por indice fallan cuando ese soldado murio. El ciclo
        // salta a los caidos y recorre a los vivos en orden estable (el del
        // escuadron), asi que siempre da un resultado util.
        void CycleToNextLivingAlly() => CycleLivingAlly(+1);

        // direction +1 avanza y -1 retrocede sobre el mismo orden estable.
        void CycleLivingAlly(int direction)
        {
            if (Squad == null || Squad.Count == 0) return;
            if (direction == 0) direction = 1;
            int start = Squad.IndexOf(Brain.Current);
            for (int step = 1; step <= Squad.Count; step++)
            {
                var candidate = Squad[((start + step * direction) % Squad.Count + Squad.Count) % Squad.Count];
                if (candidate == null || candidate == Brain.Current) continue;
                if (!candidate.Health.IsAlive || !candidate.gameObject.activeInHierarchy) continue;
                TryPossess(candidate);
                return;
            }
            RejectOrder("NO QUEDAN ALIADOS VIVOS");
        }

        void EquipFromCatalog(WeaponKind kind) => EquipWeaponHotkey(kind);

        // Orden fijo del ciclo, el mismo que las teclas 1/2/3.
        static readonly WeaponKind[] WeaponCycle = { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Heavy };

        void CycleWeapon(int direction)
        {
            if (Brain.Current == null || Brain.Current.Weapon == null) return;
            var actual = Brain.Current.Weapon.CurrentWeaponKind;
            int idx = System.Array.IndexOf(WeaponCycle, actual);
            if (idx < 0) idx = 0;
            int next = ((idx + direction) % WeaponCycle.Length + WeaponCycle.Length) % WeaponCycle.Length;
            EquipFromCatalog(WeaponCycle[next]);
        }

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
                case AimTargetType.Enemy:
                    return $"Enemigo: {result.Soldier.DisplayName}   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Vehicle:
                    return "[G] ordenar al aliado mas cercano que suba   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Obstacle:
                    return "Obstáculo   ·   [Click] disparar   ·   [TAB] vista RTS";
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

            // Los aliados libres cerca también suben, en cualquier asiento libre.
            foreach (var s in Squad)
            {
                if (s == null || s == driverSoldier || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(s.transform.position, vehicle.transform.position) <= autoMountRadius)
                    vehicle.Mount(s);
            }
        }

        // Toma control de un asiento en el que el soldado poseído YA está
        // montado -- sea porque acaba de subir (EnterVehicle) o porque ya
        // estaba adentro y el jugador recién ahora vuelve a esa vista con
        // [TAB] o [F] desde RTS. No llama a Vehicle.Mount: eso ya pasó.
        void EnterPossessedVehicleSeat(VehicleSeatRole role)
        {
            currentSeat = role;
            var vb = Vehicle.GetComponent<VehicleBrain>();
            if (role == VehicleSeatRole.Driver) vb.IsPlayerDriving = true;

            Transform seatAnchor = role == VehicleSeatRole.Driver ? Vehicle.transform.Find("DriverEye") : Vehicle.transform;
            if (seatAnchor != null) Rig.BeginTransition(seatAnchor);
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

            PossessionService.Swap(Brain, occupant);
            var role = vehicle.RoleOf(occupant);
            if (role == null) return;

            Rig.SetMode(ControlMode.Fps);
            EnterPossessedVehicleSeat(role.Value);
        }

        public void ExitVehicle()
        {
            if (Brain.Current == null) return;
            Vehicle.Dismount(Brain.Current);
            var vb = Vehicle.GetComponent<VehicleBrain>();
            if (currentSeat == VehicleSeatRole.Driver) vb.IsPlayerDriving = false;
            currentSeat = null;

            // Si venías viendo el vehículo desde arriba (RTS), bajarte no
            // debe dejar la cámara con una posición/rotación de FPS
            // colgada mientras el modo sigue en ortográfico: hay que
            // recentrar la vista RTS en vez de FollowFps.
            if (Rig.Mode == ControlMode.Rts) Rig.SetRtsView(Brain.Current.transform.position);
            else Rig.FollowFps(Brain.Current);
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
            if (SelectionCount != null) SelectionCount.SetModeVisible(false);
            ClearNearestAllyHighlight();
            if (bodyHiddenFor != null) { bodyHiddenFor.SetBodyVisible(true); bodyHiddenFor = null; }
            if (Vehicle == null || Brain.Current == null) { currentSeat = null; return; }

            // El tanque se destruye y Vehicle.OnDestroyed() ya expulsa a
            // todo el mundo (Dismount reactiva el GameObject y lo
            // reposiciona) -- pero currentSeat es estado propio de este
            // componente, Vehicle no tiene forma de avisarle que ya no
            // hay que seguir tratando esto como "estoy adentro". Sin este
            // corte, la cámara se queda pegada a una carcasa quemada.
            if (Vehicle.IsDestroyed)
            {
                currentSeat = null;
                if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
                if (TurretAim != null) TurretAim.SetVisible(false);
                Rig.FollowFps(Brain.Current);
                return;
            }

            var motor = Vehicle.GetComponent<VehicleMotor>();
            // El freno solo es una accion real cuando quien maneja es el
            // jugador (currentSeat==Driver) y esta apretando [G] -- para
            // un pasajero o el conductor IA esto no aplica.
            bool isBraking = currentSeat == VehicleSeatRole.Driver && kb.gKey.isPressed;
            if (VehicleStatus != null)
            {
                VehicleStatus.UpdateFrom(Vehicle, motor, isBraking);
                VehicleStatus.SetSeat(currentSeat);
            }

            if (KeyBindings.WasPressed(KeyBindings.SubirBajarVehiculo)
                || KeyBindings.WasPressed(KeyBindings.Interactuar)) { ExitVehicle(); return; }

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
                    if (Mathf.Abs(scroll) > 0.01f) Rig.Zoom(scroll * rtsZoomSpeed * Time.deltaTime);
                }

                if (TurretAim != null) TurretAim.SetVisible(false);
                SetInstructionText("[TAB] volver a manejar en primera persona   ·   [E] bajar");
                return;
            }

            if (kb.vKey.wasPressedThisFrame) vehicleFirstPerson = !vehicleFirstPerson;

            var vb = Vehicle.GetComponent<VehicleBrain>();
            var turret = Vehicle.GetComponentInChildren<TurretWeapon>();
            // El HUD de torreta es solo del artillero: conduciendo o de
            // pasajero no aporta nada y taparia la vista.
            if (TurretAim != null && currentSeat != VehicleSeatRole.Gunner) TurretAim.SetVisible(false);

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
                    // El mouse ya no gira el cañon directo: mueve el
                    // angulo OBJETIVO, y el cañon lo persigue a velocidad
                    // limitada. Es lo que le da peso a la torreta -- y lo
                    // que hace que el reticulo de "ya llegue / todavia
                    // girando" tenga algo que informar.
                    var delta = mouse.delta.ReadValue();
                    turret.AddDesiredYaw(delta.x * turretSensitivity);
                    turret.TickPlayerAim(Time.deltaTime);
                    if (mouse.leftButton.wasPressedThisFrame) turret.TryFire();

                    // El artillero usaba el mismo FOV que caminando, asi
                    // que apuntar a distancia era adivinar. El zoom de
                    // mirilla existia a pie pero se desactivaba adrede en
                    // vehiculo; con el arco balistico hace mas falta aca.
                    Rig.SetZoomed(mouse.rightButton.isPressed);

                    // [R] alterna municion: explosiva de area o
                    // perforante de daño concentrado.
                    if (kb.rKey.wasPressedThisFrame)
                    {
                        turret.CycleAmmo();
                        if (ModeToast != null)
                            ModeToast.Show(turret.Ammo == TurretWeapon.AmmoType.Explosive ? "MUNICION EXPLOSIVA" : "MUNICION PERFORANTE", 1.1f);
                    }
                }
                if (TurretAim != null) TurretAim.UpdateFrom(turret);

                // Antes esto era clic derecho, pero ese boton pasa a ser
                // el zoom de mira del artillero (que es lo que mas se usa
                // desde ese asiento). [T] es ademas la misma tecla que da
                // la orden de movimiento en RTS.
                if (kb.tKey.wasPressedThisFrame && mouse != null && Rig.Cam != null)
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
                ? "[WASD] conducir · [G] frenar · [2] ir a la torreta · [V] cámara · [TAB] vista RTS · [E] bajar"
                : currentSeat == VehicleSeatRole.Gunner
                    ? "[Mouse] apuntar · [Click] disparar · [Click der.] zoom de mira · [R] munición · [T] mandar la camioneta ahí · [1] conducir · [V] cámara · [TAB] vista RTS · [E] bajar"
                    : "[E] bajar · [V] cámara · [TAB] vista RTS";
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
            if (newRole == VehicleSeatRole.Gunner) GameLog.Line("Se monto en la metralleta");

            // Antes la camara saltaba de golpe al cambiar de asiento --
            // de conductor a artillero es un cambio de punto de vista
            // igual de brusco que subir al vehiculo por primera vez, que
            // ya usa esta misma transicion.
            if (vehicleFirstPerson)
            {
                Transform newAnchor = newRole == VehicleSeatRole.Driver
                    ? Vehicle.transform.Find("DriverEye")
                    : newRole == VehicleSeatRole.Gunner
                        ? Vehicle.GetComponentInChildren<TurretWeapon>()?.transform.Find("GunnerEye")
                        : null;
                if (newAnchor != null) Rig.BeginTransition(newAnchor);
            }
        }

        void UpdateVehicleCamera(Transform anchor)
        {
            if (vehicleFirstPerson && anchor != null) Rig.FollowAnchor(anchor);
            else Rig.FollowThirdPerson(Vehicle.transform, 8f, 3.5f);
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

        // -----------------------------------------------------------
        // RTS
        // -----------------------------------------------------------
        void UpdateRts(Keyboard kb, Mouse mouse)
        {
            // El balanceo es solo de caminar en primera persona: sin
            // apagarlo aca seguiria oscilando la camara en RTS y dentro
            // del vehiculo, donde no hay pisadas que representar.
            Rig.SetWalking(false);
            Rig.SetZoomed(false); // el zoom de mirilla es solo a pie
            if (WeaponStatus != null) WeaponStatus.gameObject.SetActive(false);
            if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
            if (TurretAim != null) TurretAim.SetVisible(false);
            if (weaponViewmodel != null) weaponViewmodel.SetActive(false);
            if (AimUiRef != null) AimUiRef.SetVisible(false);
            if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false);
            if (SelectionCount != null) SelectionCount.SetModeVisible(true);
            ClearNearestAllyHighlight();
            if (bodyHiddenFor != null) { bodyHiddenFor.SetBodyVisible(true); bodyHiddenFor = null; }
            UpdateVehicleSelectionRing();
            bool ctrlHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            // [Ctrl+A] selecciona a toda la escuadra viva, el estandar de
            // cualquier RTS -- antes solo se podia arrastrar un cuadro
            // que los abarcara a todos, lo que obligaba a alejar la
            // camara primero.
            if (ctrlHeld && kb.aKey.wasPressedThisFrame && Squad != null)
            {
                Selection.SelectAll(Squad);
                GameLog.Line("Se selecciono toda la escuadra");
            }

            Vector3 pan = Vector3.zero;
            if (kb.wKey.isPressed) pan += Vector3.forward;
            if (kb.sKey.isPressed) pan += Vector3.back;
            if (kb.dKey.isPressed) pan += Vector3.right;
            // Con Ctrl apretado, A es el atajo de "seleccionar todo", no
            // panear -- sin este corte, Ctrl+A tambien empujaria la
            // camara a la izquierda en el mismo instante.
            if (kb.aKey.isPressed && !ctrlHeld) pan += Vector3.left;
            if (pan.sqrMagnitude > 0.0001f) Rig.Pan(pan.normalized * rtsPanSpeed * Time.deltaTime);

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Rig.Zoom(scroll * rtsZoomSpeed * Time.deltaTime);
            }

            string selectionLabel = Selection.SelectedVehicle != null ? "vehiculo seleccionado" : $"{Selection.Selected.Count} seleccionados";
            SetInstructionText($"[Arrastrar] seleccionar varios · [Shift+Click] sumar · [T]/[Click der.] mover selección · [X] cancelar orden · [G] subir al vehículo · [F] poseer · [Q] ciclar · [C] mas cercano · [TAB] vista FPS · {selectionLabel}");

            if (mouse == null || Rig.Cam == null) return;

            UpdateDragSelection(kb, mouse);

            var screenRay = Rig.Cam.ScreenPointToRay(mouse.position.ReadValue());

            // [T] o click derecho: mover a todos los seleccionados ahí --
            // o al vehículo, si es él quien está seleccionado (requiere
            // conductor propio adentro, como en FPS).
            if (kb.tKey.wasPressedThisFrame || (mouse.rightButton.wasPressedThisFrame && !dragging))
            {
                var result = Aim.Evaluate(screenRay, null);
                // Con Shift la orden se ENCOLA detras de lo ya planificado
                // en vez de reemplazarlo: es lo que permite trazar una ruta
                // de varios tramos.
                bool queued = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

                if (result.Type == AimTargetType.Ground)
                {
                    if (Selection.SelectedVehicle != null) TryIssueVehicleMoveOrder(result.Point);
                    else if (Selection.Selected.Count > 0)
                    {
                        // Ordenar sobre un obstaculo no hacia nada y no
                        // avisaba: el soldado se trababa contra el borde y
                        // el jugador creia que la orden se habia dado.
                        if (!OrderService.IsValidDestination(result.Point)) RejectOrder("DESTINO BLOQUEADO");
                        else OrderService.IssueMoveOrderForSelection(Selection.Selected, result.Point, queued);
                    }
                }
                // IssueAttackOrder existia y funcionaba pero no estaba
                // cableada a ninguna entrada en RTS: una capacidad ya
                // implementada que el jugador no podia usar.
                else if (result.Type == AimTargetType.Enemy && Selection.Selected.Count > 0)
                {
                    // Lote: la confirmacion sonora y el log van UNA vez, no
                    // una por soldado (con 50 seleccionados eran 50 tonos
                    // superpuestos). El log ya lo emite el metodo de lote.
                    OrderService.IssueAttackOrderForSelection(Selection.Selected, result.Soldier);
                }
                else if (result.Type == AimTargetType.Vehicle && result.Vehicle.IsDestroyed)
                {
                    RejectOrder("VEHICULO DESTRUIDO");
                }
            }

            if (kb.gKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Vehicle && !result.Vehicle.IsDestroyed)
                {
                    if (result.Vehicle.OccupantCount > 0) DismountAll(result.Vehicle);
                    else OrderService.IssueMountOrderForSelection(Selection.Selected, result.Vehicle);
                }
            }

            if (kb.fKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Ally) TryPossess(result.Soldier);
                // Apuntando al vehículo con la escuadra (o parte de ella)
                // ya adentro: [F] toma control de manejo en vez de
                // requerir que primero le apuntes a un soldado -- los
                // ocupantes están inactivos/ocultos, no se les puede
                // apuntar directamente.
                else if (result.Type == AimTargetType.Vehicle && result.Vehicle.OccupantCount > 0)
                {
                    EnterVehicleViewFromRts(result.Vehicle);
                }
            }

            // Una orden dada por error obligaba a esperar a que el
            // soldado llegara a destino para recien ahi poder
            // redirigirlo. [X] la cancela y devuelve a la seleccion
            // actual a Patrol sin tener que darle una orden nueva encima.
            // --- Ordenes de escuadra nuevas ---
            // [Z] reagrupar dispersos (219)
            if (KeyBindings.WasPressed(KeyBindings.Reagrupar) && Selection.Selected.Count > 0)
            {
                OrderService.RegroupSelection(Selection.Selected, currentFormation);
                if (ModeToast != null) ModeToast.Show("REAGRUPANDO");
            }

            // [B] retirada: alejarse del enemigo mas cercano (217)
            if (KeyBindings.WasPressed(KeyBindings.Retirada) && Selection.Selected.Count > 0)
            {
                OrderService.IssueRetreatOrderForSelection(Selection.Selected);
                if (ModeToast != null) ModeToast.Show("RETIRADA");
            }

            // [K] cicla la formacion con la que se emiten las ordenes (210)
            if (KeyBindings.WasPressed(KeyBindings.CiclarFormacion))
            {
                currentFormation = (FormationKind)(((int)currentFormation + 1) % 4);
                if (ModeToast != null) ModeToast.Show("FORMACION: " + currentFormation.ToString().ToUpper());
            }

            // [J] seleccionar solo los heridos (220)
            if (KeyBindings.WasPressed(KeyBindings.SeleccionarHeridos))
            {
                if (!Selection.SelectWoundedOnly() && ModeToast != null) ModeToast.Show("NADIE HERIDO");
            }

            // [N] seleccionar a todos los del mismo tipo en pantalla (214)
            if (KeyBindings.WasPressed(KeyBindings.SeleccionarMismoTipo) && Selection.Selected.Count > 0)
            {
                Selection.SelectSameTypeOnScreen(Selection.Selected[0], Rig.Cam);
            }

            if (kb.xKey.wasPressedThisFrame && Selection.Selected.Count > 0)
            {
                foreach (var s in Selection.Selected)
                {
                    var b = s.Brain;
                    if (b != null) b.CancelOrder();
                }
                // Los marcadores de cola son permanentes (representan un
                // plan pendiente): cancelar la orden tiene que borrarlos,
                // si no queda un plan dibujado que ya nadie va a cumplir.
                OrderMarkerFx.ClearQueuedMarkers();
                GameLog.Line("Se cancelo la orden de la seleccion");
            }

            UpdateControlGroups(kb);
            UpdateFormationPreview(mouse, screenRay);

            // [Espacio] recentra la camara en el centroide de la escuadra
            // viva -- la tecla mas grande y accesible para la accion mas
            // repetida en vista tactica, para cuando la camara se pierde
            // paneando por el mapa.
            if (kb.spaceKey.wasPressedThisFrame && Squad != null)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (var s in Squad)
                {
                    if (s == null || !s.Health.IsAlive) continue;
                    sum += s.transform.position;
                    count++;
                }
                if (count > 0) Rig.RecenterOn(sum / count);
            }
        }

        // Anillo de selección para el vehículo (mismo look que el de los
        // soldados, SelectionRingFx, solo que esto no pasa por
        // SelectionRingManager porque ese escucha SelectionChangedEvent,
        // que es pura selección de soldados).
        Vehicle ringedVehicle;
        SelectionRingFx vehicleSelectionRing;
        static readonly Color VehicleSelectionRingColor = new Color(0.3f, 0.75f, 0.95f);

        // El jugador no veia donde iba a quedar cada soldado hasta DESPUES
        // de dar la orden, cuando ya no podia corregirla. Mientras se
        // mantiene el boton derecho apretado se dibujan los puestos
        // fantasma; se descartan al soltar (la orden real, que se emite en
        // wasPressedThisFrame, dibuja sus propios marcadores).
        readonly List<GameObject> formationGhosts = new List<GameObject>();

        void UpdateFormationPreview(Mouse mouse, Ray screenRay)
        {
            bool showing = mouse.rightButton.isPressed && !dragging
                && Selection.Selected.Count > 1 && Selection.SelectedVehicle == null;

            if (!showing)
            {
                if (formationGhosts.Count > 0) ClearFormationGhosts();
                return;
            }

            var result = Aim.Evaluate(screenRay, null);
            if (result.Type != AimTargetType.Ground) { ClearFormationGhosts(); return; }

            var spots = OrderService.FormationPoints(result.Point, Selection.Selected.Count);
            EnsureGhostCount(spots.Length);
            for (int i = 0; i < spots.Length; i++)
                formationGhosts[i].transform.position = new Vector3(spots[i].x, 0.06f, spots[i].z);
        }

        void EnsureGhostCount(int count)
        {
            while (formationGhosts.Count < count)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "FormationGhost";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.localScale = new Vector3(0.9f, 0.03f, 0.9f);
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                go.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = new Color(0.35f, 0.85f, 0.35f) };
                formationGhosts.Add(go);
            }
            while (formationGhosts.Count > count)
            {
                var last = formationGhosts[formationGhosts.Count - 1];
                formationGhosts.RemoveAt(formationGhosts.Count - 1);
                if (last != null) Destroy(last);
            }
        }

        void ClearFormationGhosts()
        {
            foreach (var g in formationGhosts) if (g != null) Destroy(g);
            formationGhosts.Clear();
        }

        void RejectOrder(string reason)
        {
            if (ModeToast != null) ModeToast.Show(reason, 1.2f);
            OrderService.PlayRejectSound();
            GameLog.Line($"Orden rechazada: {reason}");
        }

        // Se guardan IDS y no referencias a Soldier: si un miembro del
        // grupo cae, su id simplemente no resuelve al recuperarlo, en vez
        // de arrastrar una referencia a un objeto muerto para siempre.
        readonly Dictionary<int, List<int>> controlGroups = new Dictionary<int, List<int>>();
        int lastRecalledGroup = -1;
        float lastRecallTime = -99f;
        const float GroupDoubleTapSeconds = 0.4f;

        void UpdateControlGroups(Keyboard kb)
        {
            var digitKeys = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key,
                                    kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (!digitKeys[i].wasPressedThisFrame) continue;
                int group = i + 1;

                if (ctrl)
                {
                    if (Selection.Selected.Count == 0) continue;
                    var ids = new List<int>();
                    foreach (var s in Selection.Selected) ids.Add(s.Id);
                    controlGroups[group] = ids;
                    if (ModeToast != null) ModeToast.Show($"GRUPO {group} GUARDADO ({ids.Count})", 1.0f);
                    GameLog.Line($"Se guardo el grupo de control {group} con {ids.Count} soldados");
                    continue;
                }

                if (!controlGroups.TryGetValue(group, out var savedIds)) continue;

                var alive = new List<Soldier>();
                foreach (var id in savedIds)
                {
                    var s = ActorRegistry.FindById(id);
                    if (s != null && s.Health.IsAlive) alive.Add(s);
                }
                if (alive.Count == 0) { RejectOrder($"GRUPO {group} SIN SOBREVIVIENTES"); continue; }

                Selection.SelectAll(alive);

                // Doble pulsacion de la MISMA tecla: ademas de seleccionar,
                // lleva la vista hasta el grupo. La primera solo selecciona
                // -- recuperar un grupo no deberia mover la camara sin que
                // el jugador lo pida.
                bool doubleTap = lastRecalledGroup == group && Time.time - lastRecallTime <= GroupDoubleTapSeconds;
                lastRecalledGroup = group;
                lastRecallTime = Time.time;

                if (doubleTap)
                {
                    Vector3 sum = Vector3.zero;
                    foreach (var s in alive) sum += s.transform.position;
                    Rig.RecenterOn(sum / alive.Count);
                }
            }
        }

        void UpdateVehicleSelectionRing()
        {
            if (Selection.SelectedVehicle == ringedVehicle) return;
            ringedVehicle = Selection.SelectedVehicle;
            if (vehicleSelectionRing != null) Destroy(vehicleSelectionRing.gameObject);
            // Radio bien más grande que el de un soldado: el anillo por
            // defecto (pensado para una cápsula chica) quedaba adentro de
            // la sombra del propio chasis del tanque -- invisible, tapado
            // por el mismo vehículo.
            vehicleSelectionRing = ringedVehicle != null ? SelectionRingFx.Spawn(ringedVehicle.transform, VehicleSelectionRingColor, 2.6f) : null;
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
                    else if (result.Type == AimTargetType.Vehicle)
                    {
                        // El tanque se selecciona solo (no se combina con
                        // tropa vía Shift+Click: son dos tipos de
                        // selección mutuamente excluyentes).
                        Selection.SelectVehicle(result.Vehicle);
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

        // El cuadro de selección vive en un Canvas con CanvasScaler
        // ScaleWithScreenSize: 1 unidad de Canvas ya NO es 1 pixel de
        // pantalla, así que asignar coordenadas de mouse (pixeles reales)
        // directo a anchoredPosition queda desfasado de la posición real
        // apenas la resolución no es exactamente la de referencia. Hay que
        // convertir pixel de pantalla -> espacio local del Canvas.
        Vector2 ScreenToCanvasLocal(Vector2 screenPoint)
        {
            var canvasRect = SelectionBox.rectTransform.parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, Rig.Cam, out var local);
            // El SelectionBox tiene pivot/anchors en (0,0): sus coordenadas
            // son relativas a la esquina inferior-izquierda del Canvas, no a
            // su centro (que es de donde sale "local").
            return local + new Vector2(canvasRect.rect.width * canvasRect.pivot.x, canvasRect.rect.height * canvasRect.pivot.y);
        }

        void UpdateSelectionBoxVisual(Vector2 a, Vector2 b)
        {
            if (SelectionBox == null) return;
            Vector2 la = ScreenToCanvasLocal(a);
            Vector2 lb = ScreenToCanvasLocal(b);
            var rt = SelectionBox.rectTransform;
            float minX = Mathf.Min(la.x, lb.x), maxX = Mathf.Max(la.x, lb.x);
            float minY = Mathf.Min(la.y, lb.y), maxY = Mathf.Max(la.y, lb.y);
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
