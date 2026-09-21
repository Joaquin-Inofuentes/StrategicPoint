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
    // Traduce teclado/ratón reales a los mismos métodos que usa el test
    // automático. No decide nada nuevo: es el "pegamento" de Play mode.
    // Solo corre cuando el juego está en Play (Application.isPlaying).
    public partial class PlayerInputDriver : MonoBehaviour
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
        public SP.UI.PerfHudView PerfHud;
        public SP.UI.GroupCardsView GroupCards;
        // Grafo de navegacion para la vista previa de ruta (218). Se arma
        // al construir la escena, no lo calcula el driver.
        [System.NonSerialized] public SP.Core.WaypointGraph NavGraph;
        // Formacion con la que se emiten las ordenes de movimiento. Cuadricula
        // es la de siempre, asi que el comportamiento por defecto no cambia.
        FormationKind currentFormation = FormationKind.Cuadricula;
        public PlayerHealthView PlayerHealth;
        public UI.SelectionCountView SelectionCount;
        public UI.ModeToastView ModeToast;
        public UI.MenuDeOrdenes OrdenesMenu;
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
        [SerializeField] float rtsPanSpeed = 28f;   // x2 (antes 14)
        // Ronda 11: 80 (era 40, y antes 20) y sin suavizado (ver CameraRig.AnimarZoom). Const y no [SerializeField]: un valor serializado en la escena pisaria el nuevo.
        public const float rtsZoomSpeed = 80f;
        // [E]: menos de esto = interactuar (toque); esto o mas = accion especial de la clase.
        public const float SostenerParaEspecial = 0.5f;
        bool eToque;

        // Las ordenes de escuadra viven en el radial de [Q]; las teclas sueltas
        // heredadas (F1-F3, G, T, Y, U, I, Z, C, F, B, K) quedan apagadas. Solo
        // TAB (cambio de vista) y el manejo directo siguen siendo teclas.
        public bool AtajosDeTecladoHeredados = false;
        [SerializeField] float dragThresholdPixels = 6f;
        [SerializeField] float interactRadius = 3.5f;
        [SerializeField] float autoMountRadius = 6f;

        // Pedido explicito: 2 segundos de lerp al cambiar a otro soldado
        // apuntado (poseer), contra 1 segundo al entrar a un vehiculo o
        // asiento (ver EnterPossessedVehicleSeat/SwitchSeat) -- cambiar de
        // cuerpo es un salto de punto de vista mas grande que cambiar de
        // asiento en el mismo vehiculo, y se pidio que se note mas.
        const float PossessBlendSeconds = 2f;
        const float VehicleBlendSeconds = 1f;

        bool dragging;
        Vector2 dragStart;

        // Si la pulsacion de click derecho actual empezo sobre un boton de
        // UI: en ese caso soltar no debe emitir una orden al mundo.
        bool rightPressStartedOverUi;

        // Resaltado de a qué le estoy apuntando (aliado o vehículo): se
        // guarda el renderer y su color original para poder devolvérselo
        // apenas dejo de apuntarle.
        Renderer highlightedRenderer;
        Color highlightedOriginalColor;
        VehicleMountIndicator mountIndicator;

        // B4: anillo en la base de lo que se este apuntando (enemigo,
        // aliado, vehiculo montable, obstaculo interactuable), en FPS y
        // en RTS. Reusa SelectionRingFx tal cual -- el mismo que ya
        // dibuja el anillo de seleccion -- en vez de armar un anillo
        // nuevo desde cero.
        SelectionRingFx aimRing;
        static readonly Color AimRingColor = new Color(0.85f, 0.88f, 0.95f);

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
                if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
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
                weaponViewmodelRenderer.sharedMaterial = SP.Presentation.SafeMaterial.Create(Color.white);

                // Del plan del usuario: "Al hacer zoom debe poner el arma
                // adelante de todo".
                //
                // El viewmodel cuelga de la camara a 0,65 m y mide 0,22 de
                // largo, o sea que su cara delantera queda a ~0,76 m. El
                // jugador puede pegarse a una pared hasta ~0,5 m de la
                // camara, asi que el arma queda literalmente DENTRO de la
                // pared: mismo buffer de profundidad que el mundo, la
                // pared gana, y el arma desaparece. Comprobado con una
                // captura contra el Muro: la pantalla entera es pared y no
                // se ve nada del arma.
                //
                // Un arma en primera persona no es geometria del mundo: es
                // parte de la interfaz. Se dibuja SIEMPRE por delante
                // (ZTest Always) y despues de toda la geometria opaca, que
                // es lo que hace cualquier shooter y lo que pide el plan.
                // El shader propio es la parte que de verdad lo arregla.
                // Probe primero con renderQueue = Overlay sobre URP/Lit y NO
                // alcanza: la cola cambia el orden de dibujado, no el test
                // de profundidad, asi que la pared se dibuja antes, escribe
                // su profundidad y el arma se descarta igual. Y URP/Lit ni
                // siquiera expone _ZTest (verificado con HasProperty), asi
                // que forzarlo por material era una llamada que no hacia
                // nada. Ver Assets/_Project/Shaders/ArmaEnPrimeraPersona.
                var shaderArma = Shader.Find("SP/ArmaEnPrimeraPersona");
                if (shaderArma != null)
                {
                    var matArma = new Material(shaderArma);
                    matArma.hideFlags = HideFlags.HideAndDontSave;
                    // Transparent y no Overlay: con Overlay el arma se
                    // dibujaba TAMBIEN por encima del HUD y tapaba la barra
                    // de vida. Alcanza con quedar despues de la geometria
                    // opaca; el ZTest Always del shader es lo que la saca
                    // de adentro de la pared.
                    matArma.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    weaponViewmodelRenderer.sharedMaterial = matArma;
                }
            }

            weaponViewmodel.SetActive(true);
            // Pedido explicito: "hay un cubito a la derecha, quitalo, ya
            // no debe verse -- ahora el arma esta sobre el soldado". Este
            // cubo nacio para representar el arma en primera persona
            // cuando el cuerpo no tenia un arma real colgada de la mano.
            // Ahora que ArmaEnLaMano cuelga el modelo real del hueso (ver
            // ArmaEnLaMano.Colgar) y la camara sigue por encima del
            // hombro (FollowOverShoulder, no un ojo en primera persona
            // pura), el jugador ya ve el arma real sobre el cuerpo del
            // soldado -- este segundo cubo de color solido pegado a la
            // camara quedo como un duplicado sin textura, siempre visible
            // en la esquina inferior derecha. Se sigue actualizando su
            // transform (retroceso, encuadre al apuntar) porque
            // MiraOptica.Asegurar cuelga el tubo de la mira de este mismo
            // transform -- solo se apaga lo que se DIBUJA de el.
            if (weaponViewmodelRenderer != null) weaponViewmodelRenderer.enabled = false;
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

            // BUG REAL encontrado al medir la optica: apuntando, el arma se
            // iba FUERA DE PANTALLA. Cuelga en x=0,28 con z=0,65, o sea a
            // 22 grados del eje; con el FOV de cadera (60 vertical) eso cae
            // adentro, pero el zoom lo baja a 25 y el medio ancho horizontal
            // pasa a ~22 grados. Medido: viewport x = 1,88, casi el doble
            // del borde derecho. El arreglo anterior la puso por delante de
            // las paredes, pero seguia saliendose del encuadre justo al
            // apuntar -- y una mira invisible cuando se usa no sirve.
            //
            // Apuntando, el arma se centra, que es ademas lo que hace
            // cualquier shooter: la optica queda sobre el eje de la camara.
            apuntadoVisual = Mathf.MoveTowards(apuntadoVisual,
                Rig.EstaConZoom ? 1f : 0f, Time.deltaTime * 8f);
            var desdeLaCadera = new Vector3(0.28f, -0.22f, 0.65f);
            // La y de apuntado sube el arma hasta que el tubo de la optica
            // queda en el centro exacto de la pantalla.
            var apuntando = new Vector3(0f, -0.135f, 0.62f);
            var baseDelArma = Vector3.Lerp(desdeLaCadera, apuntando, apuntadoVisual);

            weaponViewmodel.transform.localPosition = new Vector3(
                baseDelArma.x,
                baseDelArma.y + maxKickY * recoilKick,
                baseDelArma.z - maxKickZ * recoilKick);

            // La optica (H1). El zoom que ya existia angosta el FOV de la
            // camara principal, o sea que acerca TODA la pantalla; una mira
            // de verdad amplia solo lo que se ve por el tubo. Ver
            // SP.Presentation.MiraOptica.
            if (Mira == null) Mira = MiraOptica.Asegurar(Rig.Cam.transform, weaponViewmodel.transform);
            if (Mira != null)
            {
                if (Mira.Forma != weapon.CurrentWeaponKind) Mira.Configurar(weapon.CurrentWeaponKind);
                Mira.Seguir(Rig.Cam, Rig.FovObjetivo);
                Mira.Mostrar(false);   // reemplazada por MirillaView (zoom real + reticula nitida)
            }
        }

        AimResult ultimoResultadoDeMira;
        public AimResult UltimaMira => ultimoResultadoDeMira;
        // Semilla de prueba: la suite y las pruebas de juego fijan lo que 'hay en la mira' sin tener que alinear la camara
        // con un soldado que se mueve. En juego normal es siempre null.
        public AimResult? MiraForzada;

        // La carga de demolicion del asalto usa el mismo anillo que el revivir.
        public bool DemolicionEnCurso { get; set; }
        public void MostrarProgresoDemolicion(float f01) => MostrarCirculoRevivir(Mathf.Clamp01(f01));
        public void OcultarProgresoDemolicion() { if (circuloRevivir != null) circuloRevivir.SetVisible(false); }

        // Zoom real y reticula del arma equipada (ver UI/MirillaView).
        bool saltandoAntes;

        // Tutorial: mientras esta en true, subir/bajar del tanque y usar la ametralladora fija SOLO se hacen desde
        // el radial [Q] (el atajo [E] avisa y no hace nada), para que el jugador practique el comando.
        public bool SoloRadial;
        float ultimoAvisoSoloRadial = -9f;
        bool BloqueaAtajo(string pista)
        {
            if (!SoloRadial) return false;
            if (Time.time - ultimoAvisoSoloRadial > 1.5f) { ultimoAvisoSoloRadial = Time.time; RejectOrder(pista); }
            return true;
        }

        // ------------------------------------------------------------------
        // Granada de mano [G]: se MANTIENE para ver la curva y el radio de la explosion, se SUELTA
        // para lanzarla. El clic derecho o [Esc] la guarda de nuevo sin gastarla.
        // ------------------------------------------------------------------
        bool granadaApuntando;
        public bool GranadaApuntando => granadaApuntando;
        public int GranadasLanzadasPorElJugador { get; private set; }

        Vector3 OrigenDeGranada(Soldier yo) => yo.transform.position + Vector3.up * 0.55f + yo.transform.forward * 0.55f + yo.transform.right * 0.2f;

        Vector3 PuntoDeGranada(Ray ray, AimResult result, Vector3 origen)
        {
            Vector3 punto = result.Type != AimTargetType.None ? result.Point : ray.origin + ray.direction * Granada.AlcanceMaximo;
            var plano = new Vector3(punto.x - origen.x, 0f, punto.z - origen.z);
            if (plano.magnitude > Granada.AlcanceMaximo)
            {
                plano = plano.normalized * Granada.AlcanceMaximo;
                punto = new Vector3(origen.x + plano.x, punto.y, origen.z + plano.z);
            }
            return punto;
        }

        void CancelarGranada()
        {
            if (!granadaApuntando) return;
            granadaApuntando = false;
            TrayectoriaGranadaView.Asegurar().Ocultar();
        }

        void ActualizarGranada(Keyboard kb, Ray ray, AimResult result)
        {
            // Solo en Play: la vista previa crea objetos y en Edit mode (la suite headless) quedarian sueltos.
            if (!Application.isPlaying) return;
            var yo = Brain.Current;
            var vista = TrayectoriaGranadaView.Asegurar();
            bool bloqueado = TorretaFijaActiva || (OrdenesMenu != null && OrdenesMenu.Abierto) || yo == null || yo.Weapon == null;
            if (bloqueado) { CancelarGranada(); return; }

            var tecla = KeyBindings.Get(KeyBindings.Granada);
            bool apretada = tecla != Key.None && kb[tecla].isPressed;

            if (!granadaApuntando && KeyBindings.WasPressed(KeyBindings.Granada))
            {
                if (yo.Weapon.Granadas <= 0)
                {
                    Feedback.Accion(SfxKind.EmptyClick, "SIN GRANADAS", yo.transform.position, Feedback.Warn, aviso: true, pulso: false, volumen: 0.6f);
                    return;
                }
                granadaApuntando = true;
                Feedback.Accion(SfxKind.GrenadePin, "GRANADA LISTA · SOLTA [G] PARA LANZAR", yo.transform.position, Feedback.Warn, aviso: true, pulso: true, volumen: 0.8f);
            }
            if (!granadaApuntando) return;

            var mouse = Mouse.current;
            if (kb.escapeKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                CancelarGranada();
                Feedback.Accion(SfxKind.RadialCancel, "GRANADA GUARDADA", yo.transform.position, Feedback.Info, aviso: true, pulso: false, volumen: 0.5f);
                return;
            }

            var origen = OrigenDeGranada(yo);
            var v = Granada.VelocidadHacia(origen, PuntoDeGranada(ray, result, origen));
            vista.Mostrar(origen, v, yo.transform);
            if (apretada) return;

            // Soltada: se lanza.
            CancelarGranada();
            if (!yo.Weapon.ConsumirGranada()) return;
            Granada.Lanzar(origen, v, yo);
            GranadasLanzadasPorElJugador++;
            Feedback.Visual($"GRANADA · QUEDAN {yo.Weapon.Granadas}", yo.transform.position, Feedback.Warn, aviso: true, pulso: false);
            Rig.KickDirectional(-yo.transform.forward, 0.06f);
        }

        void ActualizarMirilla(AimResult result)
        {
            if (Brain.Current == null || Rig == null) return;
            var spec = WeaponCatalog.Get(Brain.Current.Weapon.CurrentWeaponKind);
            Rig.SetZoomFactor(spec.ZoomFactor);
            var canvasRoot = AimUiRef != null ? AimUiRef.transform.parent : null;
            var mirilla = MirillaView.Asegurar(canvasRoot);
            if (mirilla == null) return;
            var tinte = result.Type == AimTargetType.Enemy ? new Color(1f, 0.32f, 0.26f)
                      : result.Type == AimTargetType.Ally ? new Color(0.45f, 1f, 0.55f)
                      : new Color(1f, 1f, 1f, 0.95f);
            mirilla.Actualizar(Rig.EstaConZoom && Rig.AdsBlend > 0.55f, spec.Reticle, tinte);
            if (AimUiRef != null)
            {
                AimUiRef.SetCrosshairStyle(MirillaView.SpriteDe(spec.Reticle), spec.Reticle == ReticleStyle.Punto ? 46f : 54f);
                AimUiRef.SetBaseCrosshairHidden(mirilla.Alfa > 0.5f);
            }
        }

        // La optica del arma. Publica para que la suite la pueda mirar sin
        // reflexion: es estado observable del arma, no un detalle interno.
        public MiraOptica Mira { get; private set; }

        // 0 = arma en la cadera, 1 = arma centrada apuntando. Se mueve
        // gradual para que centrarla no sea un salto.
        float apuntadoVisual;
        public float ApuntadoVisual => apuntadoVisual;

        // Estado de "estoy adentro de un vehículo".
        VehicleSeatRole? currentSeat;

        // Vestigial: la vista de vehiculo es siempre 3ra persona ahora (ver
        // UpdateVehicleCamera). Se deja el metodo -- sin efecto -- para que
        // AutoDemoRunner, que lo llama, siga compilando sin tocarlo.
        // ---------------------------------------------------------------
        // SetDestination: mueve al jugador a un punto del mundo SIN cursor ni
        // camara. A pie, el soldado camina solo hasta ahi (WASD lo corta); en un
        // vehiculo, se manda el vehiculo (con un aliado al volante si hace falta).
        // Es publico y tambien sirve por mensaje:
        //   driver.SendMessage("SetDestination", new Vector3(x, 0, z));
        // ---------------------------------------------------------------
        Vector3? destinoAuto;
        public bool TieneDestino => destinoAuto.HasValue;
        public Vector3? DestinoActual => destinoAuto;

        public void SetDestination(Vector3 punto)
        {
            if (currentSeat.HasValue && Vehicle != null)
            {
                if (Vehicle.Driver == null)
                {
                    foreach (var ocupante in Vehicle.Occupants)
                    {
                        if (ocupante == null || ocupante == Brain.Current) continue;
                        if (ocupante.Health == null || !ocupante.Health.IsAlive || Vehicle.IsMountAnimating(ocupante)) continue;
                        Vehicle.MoveToSeat(ocupante, VehicleSeatRole.Driver);
                        break;
                    }
                }
                if (TryIssueVehicleMoveOrder(punto) && currentSeat == VehicleSeatRole.Driver) autoConduccion = true;
                GameLog.Line($"SetDestination (vehiculo) -> {punto}");
                return;
            }
            destinoAuto = punto;
            GameLog.Line($"SetDestination (a pie) -> {punto}");
        }

        public void CancelDestination() => destinoAuto = null;

        public void ToggleVehicleCameraView() { }

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
            // El menu radial de ordenes ([Q] sostenido): si la escena no lo trae, se arma.
            if (OrdenesMenu == null || !OrdenesMenu.EsRadial) OrdenesMenu = SP.UI.MenuDeOrdenes.AsegurarEnEscena();
            if (Brain.Current == null && Squad != null && Squad.Count > 0)
            {
                Brain.Possess(Squad[0]);
                Rig.FollowOverShoulder(Squad[0].transform);

                // La posesion inicial no publica PossessionChangedEvent (ver
                // el comentario en RosterView.Rebuild): sin este empujon, la
                // fila del roster que arranca poseida podia quedar sin
                // resaltar si su OnEnable corrio antes que este Start().
                var roster = FindAnyObjectByType<SP.UI.RosterView>();
                if (roster != null) roster.Rebuild();
            }

            // Bug 14: PathPreview.Attach solo lo llamaba HeadlessTestRunner
            // (herramienta de Editor). En una escena de juego real construida
            // de otra forma, PathPreview.Instance.graph quedaba null para
            // siempre y la vista previa de ruta nunca rodeaba obstaculos.
            // Conectarlo aca, donde el driver SI corre en Play mode real,
            // garantiza que la vista previa quede armada sin importar como
            // se construyo la escena.
            //
            // ...pero seguia sin dibujar nada, porque NavGraph tampoco lo
            // asignaba nadie fuera del HeadlessTestRunner: la unica linea
            // que lo escribia vivia en la suite. Conectarlo a null es
            // conectarlo a nada. NavService es el que ahora construye la
            // grilla del mapa real; esto la comparte con la vista previa
            // para que la linea azul dibuje el MISMO rodeo que van a hacer
            // los soldados.
            if (NavGraph == null) NavGraph = SP.Core.NavService.Graph;
            if (SP.Ai.PathPreview.Instance != null && NavGraph != null)
                SP.Ai.PathPreview.Instance.Attach(NavGraph);
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
            // Es un estatico: si se queda apuntando al soldado de la
            // partida anterior, la siguiente arranca con un soldado
            // fantasma al que nadie le puede dar ordenes.
            OrderService.ManejadoAMano = null;
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

            // Quien esta manejando el jugador CON LAS MANOS. BUG REAL: el
            // comentario original de esta linea decia que corria "en el
            // unico punto por donde pasa todo frame", pero en los hechos
            // vivia mas abajo, despues de CUATRO returns tempranos (pausa,
            // overlay de controles, handlingDeath, currentSeat.HasValue).
            // Durante la secuencia de muerte (hasta 5s) y mientras el
            // jugador esta sentado en un vehiculo, este valor quedaba
            // pisado con lo que fuera que tenia ANTES de entrar a esa
            // ventana -- si en el medio se reposeia a otro soldado (ej.
            // [Espacio] durante la muerte), OrderService.LoManejaElJugador
            // seguia devolviendo false para el soldado nuevo (o true para
            // el viejo) hasta el primer frame que lograra llegar hasta
            // aca sin cortarse antes. Ahora se recalcula ANTES de
            // cualquier return, asi que ninguna transicion se lo pierde.
            OrderService.ManejadoAMano = Rig.Mode == ControlMode.Fps ? Brain.Current : null;
            // Los aliados libres te siguen si te alejas mas de X (ver
            // AjustesDeEscuadra): solo hay lider a pie y en primera persona.
            AjustesDeEscuadra.Lider = Rig.Mode == ControlMode.Fps && !currentSeat.HasValue ? Brain.Current : null;
            if (!currentSeat.HasValue && panelDeTeclas != null && panelDeTeclas.Visible) panelDeTeclas.SetVisible(false);

            // Pausa/menú de victoria-derrota tienen Time.timeScale=0, pero
            // Update() no se frena solo por eso: sin este corte, mientras
            // el panel de pausa está en pantalla el jugador podía seguir
            // moviéndose, disparando y girando la cámara por detrás.
            if (PauseRef != null && PauseRef.IsPaused) return;

            // [H] consulta los controles sin pausar el juego -- sigue
            // corriendo la simulacion (a diferencia de abrirlo desde
            // pausa), pero congela la entrada del jugador mientras esta
            // abierto para no mover ni disparar por error mientras lee.
            if (KeyBindings.WasPressed(KeyBindings.Controles) && PauseRef != null) PauseRef.ToggleControlsOverlay();
            // [P] panel de diagnostico: fps (mediana y p95), conteos de
            // actores, proyectiles y voces de audio. Solo lectura.
            if (kb.pKey.wasPressedThisFrame && PerfHud != null) PerfHud.Toggle();

            // D2/D3: tamaño del minimapa. [M] alterna grande/original,
            // [L] cicla entre 3 tamaños fijos que se recuerdan entre
            // partidas -- las dos formas de agrandarlo, pedidas por
            // separado en el plan, conviven sobre el mismo RectTransform.
            if (MinimapRef != null)
            {
                if (KeyBindings.WasPressed(KeyBindings.MinimapAgrandar)) MinimapRef.AlternarTamano();
                if (KeyBindings.WasPressed(KeyBindings.MinimapCiclarTamano)) MinimapRef.CiclarTamanoFijo();
            }

            // 216: un solo consumidor de la cola de alertas. Antes cada
            // vista emitia su aviso por su cuenta y con varios a la vez
            // ninguno quedaba legible. La cola decide cual se muestra.
            if (ModeToast != null && !AlertQueue.IsBusy)
            {
                string alerta; float segs;
                if (AlertQueue.TryDequeue(out alerta, out segs)) ModeToast.Show(alerta, segs);
            }
            if (PauseRef != null && PauseRef.IsControlsOverlayOpen) return;

            UpdateCursorLock(kb, Mouse.current);
            if (Rig.Mode != ControlMode.Rts) CursorContextual.Restaurar();

            // [F4] modo dios y [C] (mantener) vista tactica: se atienden en cualquier modo de camara.
            if (kb.f4Key.wasPressedThisFrame) AlternarModoDios();
            // Ronda 11: [F9] mata al soldado que manejas y [F10] revive a la escuadra caida (para probar si el medico viene).
            if (kb.f9Key.wasPressedThisFrame) ModeToast?.Show(ComandosDeDepuracion.Matar().ToUpperInvariant(), 1.5f);
            if (kb.f10Key.wasPressedThisFrame) ModeToast?.Show(ComandosDeDepuracion.Revivir().ToUpperInvariant(), 1.5f);
            ActualizarVistaTactica();

            if (MinimapRef != null)
                MinimapRef.Target = currentSeat.HasValue ? Vehicle.transform : (Brain.Current != null ? Brain.Current.transform : null);

            // El [TAB] se procesa ANTES del corte por "estoy adentro de un
            // vehículo": antes, estando adentro, Tab no hacía nada (el
            // return de UpdateInVehicle lo comía entero) -- ahora alterna
            // entre manejar en primera persona y ver el auto desde arriba
            // en RTS, sin bajarse ni perder el asiento.
            if (KeyBindings.WasPressed(KeyBindings.AlternarVista) && !handlingDeath)
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
                // Pedido explicito: sonido de transicion ("fiush") al
                // cambiar entre FPS y RTS. 2D y canal Sfx: no es un sonido
                // que ocurra en ningun punto del mundo, es feedback de UI
                // de camara.
                AudioDirector.PlayUi2D(SfxKind.CameraSwoosh, 0.6f, 0.7f);
                // Marca que el jugador ya descubrio el cambio de modo, para
                // que el recordatorio de GameplaySceneBootstrap no vuelva a
                // aparecer nunca mas en ninguna partida futura.
                PlayerPrefs.SetInt("sp_used_tab", 1);
                PlayerPrefs.Save();

                // RTS -> FPS con UNA sola unidad seleccionada: el jugador
                // pasa a manejar a ESA, no vuelve al soldado que tenia antes.
                // Con cero o varias seleccionadas no hay a quien elegir y se
                // conserva el poseido de siempre. Va ANTES del chequeo de
                // asiento de abajo: si la elegida va montada, TryPossess ya
                // toma su asiento.
                if (Rig.Mode == ControlMode.Fps && !currentSeat.HasValue)
                {
                    var elegido = SoldadoUnicoSeleccionado();
                    if (elegido != null && elegido != Brain.Current) TryPossess(elegido);
                }

                // BUG REAL ("se maneja solo"): en RTS una orden a la escuadra
                // incluye al soldado que venias manejando, y OrderService apaga su
                // IsPossessedByPlayer y le deja un destino. Al volver a FPS nadie
                // le devolvia el control: el cuerpo seguia caminando solo (o se
                // bajaba del tanque). Ahora se reclama al cambiar a FPS.
                if (Rig.Mode == ControlMode.Fps && Brain.Current != null) ReclamarControl(Brain.Current);

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
                // El radial tambien se abre desde el asiento (bajar, mandar el tanque, poseer...).
                ActualizarMenuDeOrdenes();
                if (currentSeat.HasValue) UpdateInVehicle(kb, Mouse.current);
                return;
            }

            // [F1]/[F2]/[F3]: posee directamente al soldado 1/2/3 del
            // escuadrón, sin tener que apuntarle primero.
            if (AtajosDeTecladoHeredados)
            {
                if (kb.f1Key.wasPressedThisFrame) PossessSquadIndex(0);
                if (kb.f2Key.wasPressedThisFrame) PossessSquadIndex(1);
                if (kb.f3Key.wasPressedThisFrame) PossessSquadIndex(2);
            }
            // [Q] cicla entre vivos y [C] posee al mas cercano: ambas caen
            // bajo la mano izquierda sin soltar WASD, a diferencia de F1/F2/F3.
            // El ciclado ya NO se dispara al apretar sino al SOLTAR rapido:
            // apretar y soltar son el mismo gesto hasta que pasa el umbral,
            // y decidir al apretar haria que mantener [Q] ciclara ademas de
            // abrir el menu.
            ActualizarMenuDeOrdenes();
            // 199: solo se podia ciclar hacia ADELANTE. Con una escuadra de
            // tres eso ya obliga a dar la vuelta entera para volver uno.
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.CiclarPosesionAtras)) CycleLivingAlly(-1);
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.PoseerMasCercano)) PossessNearestAlly();

            if (Rig.Mode == ControlMode.Fps) UpdateFps(kb, Mouse.current);
            else UpdateRts(kb, Mouse.current);
        }

        // Devuelve el cuerpo al jugador: sin orden pendiente, sin IA y con el estado
        // de asiento coherente con lo que de verdad paso mientras miraba desde RTS.
        public void ReclamarControl(Soldier s)
        {
            if (s == null) return;
            var ai = s.Brain;
            bool vaMontado = Vehicle != null && Vehicle.RoleOf(s) != null;
            if (ai != null)
            {
                bool teniaOrden = ai.CurrentOrderDestination.HasValue || ai.State == SP.Ai.AiState.MovingToOrder || ai.State == SP.Ai.AiState.Follow;
                if (!vaMontado && (teniaOrden || !ai.IsPossessedByPlayer)) ai.CancelOrder();
                if (!ai.IsPossessedByPlayer)
                {
                    ai.IsPossessedByPlayer = true;
                    GameLog.Line($"{s.DisplayName}: el jugador recupera el control al volver a FPS");
                }
            }
            // Una orden de RTS pudo bajarlo del tanque: el asiento recordado ya no vale.
            if (currentSeat.HasValue && !vaMontado) ClearVehicleSeatState();
        }

        // -----------------------------------------------------------
        // Muerte del soldado poseído: la cámara se aleja mirando el
        // cadáver, espera un momento, y pasa sola al aliado vivo más
        // cercano -- o a vista RTS si no queda ninguno.
        // -----------------------------------------------------------
        bool handlingDeath;
        // A3: segundos que se espera un [Espacio] (A2) antes de pasar solo
        // a vista tactica. Publica y const para que la suite pueda medir
        // "a los 4 s sigue en FPS, a los 6 s ya paso a RTS" sin adivinar
        // el numero.
        public const float EsperaMaximaTrasMorir = 5f;
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
                // Murio en la ametralladora fija: la torreta se libera sola (TorretaFija.LateUpdate).
                torretaActual = null; torretaPendiente = null;
                // A5: se pide ANTES de arrancar la corrutina -- si hay un
                // aliado libre, ya viene en camino desde el primer frame de
                // la camara de muerte, no recien despues de la espera.
                SP.Player.RescateAutomatico.Solicitar(Brain.Current);
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
                if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false); MirillaView.Instancia?.Ocultar();
                deadSoldier.SetBodyVisible(true);
                deadSoldier.Motor.SetCrouching(false);
                bodyHiddenFor = null;
                ClearVehicleSeatState();

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

                // A1: antes, a los 3 s la camara pasaba SOLA al aliado vivo
                // mas cercano -- el jugador nunca decidia nada. Ahora se
                // queda orbitando el cadaver, esperando: A2 deja pedir el
                // cambio con [Espacio] en cualquier momento, y A3 lo fuerza
                // a los 5 s si nadie lo pidio. Sin NINGUN aliado vivo no hay
                // nada que esperar: derrota en el acto, igual que siempre.
                var candidatoAlMorir = OrderService.FindNearestFreeAlly(deadSoldier.transform.position, TeamId.Player, deadSoldier);
                if (candidatoAlMorir == null)
                {
                    // Ronda 11: la derrota (o el revivir en calma) la decide EstadoDePartida, que mira a toda la escuadra en cada tick.
                    GameLog.Line("Sin aliados libres: EstadoDePartida decide derrota o recuperacion");
                    Rig.SetMode(ControlMode.Rts);
                    Rig.SetRtsView(deadSoldier.transform.position);
                }
                else
                {
                    // Orbita despacio alrededor del cadaver mientras se
                    // espera -- mismo radio/altura que el punto de partida,
                    // solo gira el angulo alrededor del soldado.
                    const float orbitDegPerSec = 12f;
                    Vector3 toCam = deathPullBackGO.transform.position - deadSoldier.transform.position;
                    float radius = new Vector2(toCam.x, toCam.z).magnitude;
                    float height = toCam.y;
                    float angle = Mathf.Atan2(toCam.z, toCam.x) * Mathf.Rad2Deg;

                    float espera = 0f;
                    while (true)
                    {
                        // A5: un aliado libre puede estar de camino a
                        // revivirte (RescateAutomatico, pedido apenas
                        // moriste). Mientras te esta reviviendo, el timer
                        // de A3 NO avanza -- si avanzara igual, un rescate
                        // que tarda en llegar caeria a RTS de todos modos y
                        // el esfuerzo del aliado no serviria de nada.
                        bool teEstanReviviendo = SP.Player.RescateAutomatico.Activo && SP.Player.RescateAutomatico.Caido == deadSoldier;
                        if (!teEstanReviviendo) espera += Time.unscaledDeltaTime;

                        angle += orbitDegPerSec * Time.unscaledDeltaTime;
                        float rad = angle * Mathf.Deg2Rad;
                        Vector3 offset = new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);
                        deathPullBackGO.transform.position = deadSoldier.transform.position + offset;
                        deathPullBackGO.transform.rotation = Quaternion.LookRotation((deadSoldier.transform.position + Vector3.up * 0.8f - deathPullBackGO.transform.position).normalized);
                        Rig.FollowAnchor(deathPullBackGO.transform);

                        // A5: el rescate llego a buen puerto -- volves a
                        // ser vos mismo, no otro soldado ni RTS.
                        if (deadSoldier.Health.IsAlive)
                        {
                            GameLog.Line($"{deadSoldier.DisplayName} fue revivido por un aliado");
                            Rig.SetMode(ControlMode.Fps);
                            Rig.BeginTransition(deadSoldier.EyeAnchor != null ? deadSoldier.EyeAnchor : deadSoldier.transform);
                            break;
                        }

                        // A2: el jugador PIDE el cambio, no le llega solo.
                        if (KeyBindings.WasPressed(KeyBindings.Recentrar))
                        {
                            var elegido = OrderService.FindNearestFreeAlly(deadSoldier.transform.position, TeamId.Player, deadSoldier);
                            if (elegido != null)
                            {
                                GameLog.Line($"Camara cambio de {deadSoldier.DisplayName} a {elegido.DisplayName} (pedido con [Espacio])");
                                PossessionService.Swap(Brain, elegido);
                                Rig.SetMode(ControlMode.Fps);
                                Rig.BeginTransition(elegido.EyeAnchor != null ? elegido.EyeAnchor : elegido.transform);
                                break;
                            }
                        }

                        // A3: nadie pidio el cambio a tiempo -- se pasa solo
                        // a vista tactica, sin dar la partida por perdida
                        // (todavia hay aliados vivos que podrian revivirte).
                        if (espera >= EsperaMaximaTrasMorir)
                        {
                            GameLog.Line("Nadie pidio el cambio a tiempo: la vista pasa sola a RTS");
                            Rig.SetMode(ControlMode.Rts);
                            Rig.SetRtsView(deadSoldier.transform.position);
                            break;
                        }

                        yield return null;
                    }
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
            // BUG REAL: el primer click del jugador sobre "Reintentar"/
            // "Salir" (o sobre "Continuar" en el menu de pausa) volvia a
            // bloquear y esconder el cursor ANTES de que le sirviera de
            // algo al boton -- el modo seguia siendo Fps (nada lo cambia al
            // ganar/perder/pausar), asi que wantsLock daba true igual con
            // un panel modal tapando toda la pantalla. El click quedaba
            // "comido" por este re-bloqueo en vez de llegarle al boton: se
            // veia como que Reintentar/Salir no respondian a nada.
            bool modalShowing = (Outcome != null && Outcome.IsShowing) || (PauseRef != null && PauseRef.IsPaused);
            bool wantsLock = Rig.Mode == ControlMode.Fps && !modalShowing;

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
        // Ametralladora fija: en cuanto se implementa se conecta (ver TorretaFija).
        bool TorretaFijaActiva => torretaActual != null;
        SP.Vehicles.TorretaFija torretaActual;

        void UpdateFps(Keyboard kb, Mouse mouse)
        {
            if (Brain.Current == null) return;
            ActualizarTorretaFija();
            if (VehicleStatus != null) VehicleStatus.gameObject.SetActive(false);
            if (TurretAim != null) TurretAim.SetVisible(false);
            if (AimUiRef != null)
            {
                AimUiRef.SetVisible(true);
                AimUiRef.SetWatchedShooter(Brain.Current.Id);
                AimUiRef.SetSpread01(Brain.Current.Weapon.SpreadFraction01);
                // Pedido explicito (segunda vuelta): "el tamaño de la lupa
                // que sea 4 veces mas tamaño que el actual" -- el doble
                // pedido antes se quedaba corto. El blur de fondo mientras
                // se apunta (PostFxDirector) ya lee Rig.EstaConZoom por su
                // cuenta.
                AimUiRef.SetCrosshairZoomScale(1f);
            }
            if (SelectionCount != null) SelectionCount.SetModeVisible(false);

            // Pedido explicito: "quiero poder ver el soldado q manejo y sus
            // armas en su espalda". Antes esto ocultaba el cuerpo entero a
            // pie (Brain.Current.SetBodyVisible(false)) -- necesario con
            // una camara en el ojo, pero la vista a pie ahora es por
            // encima del hombro (Rig.FollowOverShoulder mas abajo), asi
            // que el cuerpo se deja visible a proposito.
            // Apuntando (click derecho) la camara esta en el ojo: se oculta el cuerpo pero no el arma.
            bool enOjo = Rig.AdsBlendSuave > 0.85f;
            if (enOjo)
            {
                if (bodyHiddenFor != Brain.Current)
                {
                    if (bodyHiddenFor != null) bodyHiddenFor.SetBodyVisible(true);
                    Brain.Current.SetBodyVisible(false, true);
                    bodyHiddenFor = Brain.Current;
                }
            }
            else if (bodyHiddenFor != null) { bodyHiddenFor.SetBodyVisible(true); bodyHiddenFor = null; }

            Vector3 f = Brain.Current.transform.forward;
            Vector3 r = Brain.Current.transform.right;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += f;
            if (kb.sKey.isPressed) move -= f;
            if (kb.dKey.isPressed) move += r;
            if (kb.aKey.isPressed) move -= r;
            var mandoMover = MandoFps.Mover;
            if (mandoMover.sqrMagnitude > 0f) move += f * mandoMover.y + r * mandoMover.x;
            if (TorretaFijaActiva) { move = Vector3.zero; destinoAuto = null; }
            bool moving = move.sqrMagnitude > 0.0001f;
            // Destino automatico (SetDestination): camina solo hasta el punto,
            // sin cursor ni camara. Las teclas WASD siguen mandando.
            if (!moving && destinoAuto.HasValue)
            {
                var haciaDestino = destinoAuto.Value - Brain.Current.transform.position;
                haciaDestino.y = 0f;
                if (haciaDestino.magnitude < 0.8f) destinoAuto = null;
                else
                {
                    Brain.RotateYaw(Mathf.DeltaAngle(Brain.Current.transform.eulerAngles.y, Mathf.Atan2(haciaDestino.x, haciaDestino.z) * Mathf.Rad2Deg));
                    move = haciaDestino.normalized;
                    moving = true;
                }
            }
            // [Shift]: correr (solo hacia adelante, de pie y sin apuntar). Los aliados libres que van
            // con vos tambien corren (AjustesDeEscuadra.Correr) y las piernas aceleran.
            bool shiftCorrer = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed || MandoFps.Correr;
            bool correr = shiftCorrer && moving && !Brain.Current.Motor.IsCrouching && Vector3.Dot(move.normalized, f) > 0.2f
                && !Rig.EstaConZoom && !TorretaFijaActiva;
            Brain.Current.Motor.SetRunning(correr);
            Rig.EscalaDeRespiracion = Brain.Current.Motor.IsCrouching || TorretaFijaActiva ? 0.3f : moving ? 1.6f : 1f;
            AjustesDeEscuadra.Correr = correr;
            if (moving && !TorretaFijaActiva) Brain.Move(move.normalized, Time.deltaTime);
            // Balanceo al caminar: caminar y estar quieto se veian
            // exactamente igual, sin ninguna sensacion de pisada.
            Rig.SetWalking(moving);
            // Pedido explicito: pisadas reales segun superficie. Cadencia
            // fija: no hay Animator de piernas expuesto aca para engancharse
            // a un evento de clip, pero alcanza para que se sienta ritmico.
            UpdateFootsteps(moving);

            // G2: mismo Ctrl que en RTS usan Ctrl+A y Ctrl+Click (trazar
            // recorrido) -- no colisiona porque son ramas mutuamente
            // excluyentes (UpdateFps vs UpdateRts).
            bool agacharHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || MandoFps.Agachar;
            Brain.Current.Motor.SetCrouching(agacharHeld);
            if (agacharHeld != agachadoAntes)
            {
                agachadoAntes = agacharHeld;
                Feedback.Accion(SfxKind.Crouch, agacharHeld ? "AGACHADO" : "DE PIE", Brain.Current.transform.position,
                    Feedback.Info, aviso: false, pulso: false, volumen: 0.3f);
            }

            // G3: [Espacio] salta. No choca con el mismo [Espacio] de RTS
            // (recentrar camara) ni con el de la camara de muerte (pedir
            // cambio de cuerpo): son ramas mutuamente excluyentes, esta
            // vive solo adentro de UpdateFps.
            if ((kb.spaceKey.wasPressedThisFrame || MandoFps.Saltar) && !TorretaFijaActiva)
            {
                bool yaSaltaba = Brain.Current.Motor.IsJumping;
                Brain.Current.Motor.Jump();
                if (!yaSaltaba && Brain.Current.Motor.IsJumping)
                    Feedback.Accion(SfxKind.Jump, null, Brain.Current.transform.position, Feedback.Info, aviso: false, pulso: false, volumen: 0.6f);
            }
            // Aterrizaje: golpe sordo y un sacudon leve de camara al volver a apoyar los pies.
            bool enElAire = Brain.Current.Motor.IsJumping;
            if (saltandoAntes && !enElAire)
            {
                Feedback.Accion(SfxKind.Land, null, Brain.Current.transform.position, Feedback.Info, aviso: false, pulso: false, volumen: 0.7f);
                Rig.KickDirectional(Vector3.down, 0.08f);
            }
            saltandoAntes = enElAire;

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = mouse.delta.ReadValue();
                if (OrdenesMenu != null && OrdenesMenu.Abierto)
                    OrdenesMenu.MoverSeleccion(delta);   // con el radial abierto el mouse elige, no gira
                else
                {
                    // La sensibilidad baja con el zoom para que apuntar con x6 no sea nervioso.
                    float k = Rig.EstaConZoom ? 1f / Mathf.Pow(Mathf.Max(1f, Rig.ZoomFactor), 0.75f) : 1f;
                    Brain.RotateYaw(delta.x * lookSensitivity * k);
                    if (TorretaFijaActiva) torretaActual.AcotarGiro(Brain.Current);
                    Rig.AddPitch(delta.y * lookSensitivity * k * (InvertLookY ? -1f : 1f));
                }
            }

            // Mando: el stick derecho gira igual que el mouse (velocidad angular, no delta), sin necesitar cursor capturado.
            var mandoMirar = MandoFps.Mirar;
            if (mandoMirar.sqrMagnitude > 0f && !(OrdenesMenu != null && OrdenesMenu.Abierto))
            {
                float kMando = Rig.EstaConZoom ? 1f / Mathf.Pow(Mathf.Max(1f, Rig.ZoomFactor), 0.75f) : 1f;
                float grados = MandoFps.GradosPorSegundo * (lookSensitivity / 0.15f) * kMando * Time.deltaTime;
                Brain.RotateYaw(mandoMirar.x * grados);
                if (TorretaFijaActiva) torretaActual.AcotarGiro(Brain.Current);
                Rig.AddPitch(mandoMirar.y * grados * (InvertLookY ? -1f : 1f));
            }

            // En la torreta la camara va casi al ojo (los techos de las torres no dejan lugar a 4 m detras).
            Rig.FollowOverShoulder(Brain.Current.transform, distance: TorretaFijaActiva ? 1.4f : 4f, heightOffset: Brain.Current.Motor.EyeHeightDrop,
                ojo: Brain.Current.EyeAnchor != null ? Brain.Current.EyeAnchor.position : (Vector3?)null);
            UpdateNearestAllyHighlight();

            var ray = Rig.GetForwardRay();
            var result = MiraForzada ?? Aim.Evaluate(ray, Brain.Current);
            UpdateAimHighlight(result);
            UpdateAimRing(result);
            UpdateCoverPreview(kb, result);
            UpdateVehicleMountIndicator(result);
            if (AimUiRef != null) AimUiRef.UpdateFromAimResult(result);
            ultimoResultadoDeMira = result;
            ActualizarPromptContextual(result);
            ActualizarMirilla(result);
            // [Ctrl]: habilidad especial de la clase que se maneja -- medico
            // cura/reanima, asalto/explosivos/francotirador afinan la
            // punteria. Mismo Ctrl que ya agacha (agacharHeld arriba): no es
            // una tecla nueva, se lee la tecla fisica de nuevo aca porque
            // agacharHeld es una variable local de mas arriba en este mismo
            // metodo -- se agachan Y afinan la punteria a la vez, algo
            // coherente (mismo pedido que ya reduce el spread al agachar).
            // Ronda 11 (punto 14): la habilidad especial pasa a [E] MANTENIDO (>= SostenerParaEspecial); un toque corto de [E] interactua.
            // [Ctrl] queda solo para agacharse. eToque se calcula UNA vez por cuadro: WasTapped consume la pulsacion.
            eToque = KeyBindings.WasTapped(KeyBindings.Interactuar, SostenerParaEspecial);
            bool ctrlSostenido = KeyBindings.IsHeld(KeyBindings.Interactuar, SostenerParaEspecial) || MandoFps.Agachar;
            ActualizarHabilidadDeClase(result, ctrlSostenido, moving);
            if (WeaponStatus != null) WeaponStatus.UpdateFrom(Brain.Current.Weapon);
            if (AimUiRef != null) AimUiRef.UpdateAmmoWarning(Brain.Current.Weapon);
            if (AimUiRef != null) AimUiRef.UpdateReloadCircle(Brain.Current.Weapon);
            if (KeyBindings.WasPressed(KeyBindings.Recargar) || MandoFps.Recargar)
            {
                bool yaRecargaba = Brain.Current.Weapon.IsReloading;
                Brain.Current.Weapon.Reload();
                if (!yaRecargaba && Brain.Current.Weapon.IsReloading)
                    Feedback.Visual("RECARGANDO", Brain.Current.transform.position, Feedback.Warn, aviso: false, pulso: false);   // el sonido lo pone WeaponHolder.StartReload
            }
            // La vida del poseido vive SOLO en el roster (abajo a la izquierda):
            // el panel "VIDA" de la esquina derecha repetia el mismo dato.
            if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false);
            UpdateWeaponViewmodel(Brain.Current.Weapon);

            // isPressed (no wasPressedThisFrame): antes habia que
            // clickear una vez por bala incluso con un rifle. Ahora
            // mantener el boton dispara a la cadencia real del arma
            // (fireCooldown), que ya es distinta por WeaponKind.
            if ((mouse != null && mouse.leftButton.isPressed) || MandoFps.Disparar)
            {
                bool emptyBeforeFire = Brain.Current.Weapon.CurrentAmmo <= 0 && !Brain.Current.Weapon.IsReloading;
                // El mismo punto que ya muestra la mira (result.Point): si
                // no golpeo nada (apuntando al cielo) se usa un punto lejano
                // sobre el mismo rayo de camara, nunca Vector3.zero.
                Vector3 aimPoint = result.Type != AimTargetType.None ? result.Point : ray.origin + ray.direction * Aim.MaxDistance;
                bool fired = Brain.Fire(aimPoint);
                // Clic seco de gatillo vacio: solo si de verdad no
                // disparo por falta de municion (no por estar en
                // cooldown normal entre tiros, que no deberia sonar
                // como un fallo).
                if (!fired && emptyBeforeFire && emptyClickCooldown <= 0f)
                {
                    emptyClickCooldown = 0.3f;
                    Brain.Current.Weapon.SonarGatilloVacio();

                    // Sin municion de verdad (ni cargador ni reservas): aviso
                    // en pantalla + log, y cambio solo a la primera arma del
                    // loadout que si tenga con que disparar.
                    if (Brain.Current.Weapon.SinMunicionTotal)
                    {
                        Debug.Log("SIN MUNICION");
                        Feedback.Visual("SIN MUNICION", Brain.Current.transform.position, Feedback.Bad, aviso: true, pulso: false);
                        Brain.Current.Weapon.CambiarASiguienteConMunicion();
                    }
                }
            }
            emptyClickCooldown = Mathf.Max(0f, emptyClickCooldown - Time.deltaTime);

            // BUG REAL: wasPressedThisFrame no "consume" la tecla -- el
            // mismo apretar de [1]/[2]/[3] para elegir una orden del menu
            // ([Q] sostenido, ver ResolverGestoDeQ) tambien llegaba ACA en
            // el mismo frame y re-equipaba el arma correspondiente sin que
            // el jugador lo pidiera. Mientras el menu esta abierto, estas
            // teclas son suyas.
            bool menuDeOrdenesAbierto = OrdenesMenu != null && OrdenesMenu.Abierto;
            if (!menuDeOrdenesAbierto)
            {
                // Las teclas 1/2/3 son las RANURAS del loadout de la clase que se maneja
                // (asalto: fusil/pistola/lanzacohetes; flanqueador: metralleta/pistola/escopeta...).
                TickReordenarEscuadra(kb);
                TickArsenal(kb);
                if (kb.digit1Key.wasPressedThisFrame) EquipSlot(0);
                if (kb.digit2Key.wasPressedThisFrame) EquipSlot(1);
                if (kb.digit3Key.wasPressedThisFrame) EquipSlot(2);
            }

            // 206: cambiar de arma con la rueda, la convencion del genero.
            // No colisiona con el zoom RTS por construccion: esta rama solo
            // se alcanza con Rig.Mode == Fps y sin asiento de vehiculo, y
            // los dos lectores de rueda para zoom viven en ramas de RTS.
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) CycleWeapon(wheel > 0f ? +1 : -1);
            }
            if (MandoFps.ArmaSiguiente) CycleWeapon(+1);
            if (MandoFps.ArmaAnterior) CycleWeapon(-1);

            // Pedido explicito: "con V quiero q sea el ataque de cuchillo
            // rapido". No pasa por Brain.Fire() ni depende del arma a
            // distancia equipada -- es una accion aparte con su propio
            // enfriamiento (ver WeaponHolder.TryMelee), asi que funciona
            // igual sin importar si llevas rifle, pistola o pesada.
            if (KeyBindings.WasPressed(KeyBindings.AtaqueCuchillo) && !(OrdenesMenu != null && OrdenesMenu.Abierto))
                Brain.Current.Weapon.TryMelee();

            ActualizarGranada(kb, ray, result);

            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.Poseer) && result.Type == AimTargetType.Ally)
                TryPossess(result.Soldier);

            // E1: [F] sobre un enemigo manda la orden de atacar a lo
            // seleccionado -- misma tecla que "poseer" sobre un aliado,
            // pero AimTargetType ya distingue cual es cual, asi que no hay
            // ambiguedad en que rama entra.
            // BUG REAL: si nunca se entro a vista RTS a seleccionar (caso
            // normal del jugador que arranca poseyendo en FPS), Selection
            // esta vacia y la orden se perdia en silencio. Sin seleccion
            // manual, [F] ataca con el soldado que estas manejando.
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.Poseer) && result.Type == AimTargetType.Enemy)
            {
                var attackers = Selection.Selected.Count > 0 ? Selection.Selected : SoloBrainCurrente();
                OrderService.IssueAttackOrderForSelection(attackers, result.Soldier);
            }

            if (AtajosDeTecladoHeredados && kb.tKey.wasPressedThisFrame)
            {
                bool shiftHeld = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                // [T] sobre una COBERTURA (el obstaculo o el disco celeste del
                // piso): el aliado mas cercano va, se agacha y se queda.
                if (TryResolverCobertura(result, out var puntoCobertura, out var duenoCobertura))
                    IssueCoverOrderT(puntoCobertura, duenoCobertura);
                else if (result.Type == AimTargetType.Ground)
                    IssueGroundOrderT(result.Point, shiftHeld);
            }

            if (AtajosDeTecladoHeredados && kb.gKey.wasPressedThisFrame && result.Type == AimTargetType.Vehicle)
                GOrderOnVehicle(result.Vehicle);

            // Pedido explicito: "que le pueda decir a mis aliados que me
            // sigan" -- no necesita apuntar a nada, es sobre TODA la
            // escuadra viva y activa (no montada en un vehiculo), igual
            // que el resto de las ordenes de escuadra completa.
            if (AtajosDeTecladoHeredados && kb.yKey.wasPressedThisFrame && Squad != null)
                OrderService.IssueFollowOrderForSelection(Squad, Brain.Current);

            // Pedido explicito: teclas dedicadas para subir/bajar del
            // vehiculo, sin depender de apuntarle (eso ya lo cubre [G]).
            // [U] sube a UN aliado por apretada -- al mas cercano que
            // todavia no este ya en camino a montar -- para poder llenar
            // los asientos de a uno en vez de mandar a toda la escuadra
            // de un tiron. [I] baja a todos los que esten adentro.
            if (AtajosDeTecladoHeredados && kb.uKey.wasPressedThisFrame)
            {
                var vehicle = FindTheVehicle();
                if (vehicle != null && !vehicle.IsDestroyed && vehicle.HasAnyRoom)
                {
                    var next = FindNextSquadmateToBoard(vehicle);
                    if (next != null) OrderService.IssueMountOrder(next, vehicle);
                    else RejectOrder("NO HAY MAS ALIADOS PARA SUBIR");
                }
            }
            if (AtajosDeTecladoHeredados && kb.iKey.wasPressedThisFrame)
            {
                var vehicle = FindTheVehicle();
                if (vehicle != null && vehicle.OccupantCount > 0) DismountAll(vehicle);
            }

            // Mantener click derecho apretado: zoom de mirilla (no manda la
            // camioneta hasta que se suelta, eso sigue siendo un click).
            if (mouse != null) Rig.SetZoomed(mouse.rightButton.isPressed);

            // Pedido explicito: click derecho sobre un aliado lo
            // selecciona (igual que arrastrar el mouse en RTS, pero
            // apuntando desde primera persona) y un click derecho
            // posterior sobre el piso lo manda ahi. No pisa el zoom de
            // mirilla de arriba (ese reacciona a isPressed cada frame,
            // este es un evento de UN frame) ni la orden a la camioneta:
            // esa sigue siendo el resultado por defecto si no hay nadie
            // seleccionado.
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                if (result.Type == AimTargetType.Ally)
                {
                    bool shiftPressed = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    if (shiftPressed)
                    {
                        Selection.SelectSingle(result.Soldier);
                        if (ModeToast != null) ModeToast.Show($"{result.Soldier.DisplayName.ToUpperInvariant()} SELECCIONADO", 1f);
                    }
                }
                else if (result.Type == AimTargetType.Ground)
                {
                    if (Selection.Selected.Count > 0)
                    {
                        OrderService.IssueMoveOrderForSelection(Selection.Selected, result.Point);
                        Selection.Clear();
                    }
                    else
                        TryIssueVehicleMoveOrder(result.Point);
                }
            }

            // Ametralladora fija: [E] la ocupa (apuntandole o parado junto a ella) y [E] la deja.
            if (TorretaFijaActiva)
            {
                if (eToque || KeyBindings.WasPressed(KeyBindings.SubirBajarVehiculo))
                { if (!BloqueaAtajo("USA EL RADIAL: [Q] → TORRETA FIJA → SALIR")) SalirDeTorreta(); }
                else SetInstructionText("[Mouse] apuntar (arco limitado) · [Click] disparar · [Click der.] mirar por la mira · [R] recargar · [E] salir de la torreta · [TAB] vista RTS");
                if (TorretaFijaActiva) return;
            }
            else if (eToque)
            {
                var t = result.Type == AimTargetType.Torreta ? result.Torreta : TorretaFija.MasCercana(Brain.Current.transform.position, TorretaFija.AlcanceDeUso);
                if (t != null && FindNearestPickup(Brain.Current.transform.position) == null && !(Vehicle != null && Vector3.Distance(Brain.Current.transform.position, Vehicle.transform.position) <= interactRadius))
                {
                    if (BloqueaAtajo("USA EL RADIAL: [Q] → TORRETA FIJA → USAR")) return;
                    UsarTorreta(t); return;
                }
            }

            // A4: revivir a un caido tiene prioridad sobre subir al vehiculo
            // o equipar un arma -- si hay un compañero caido al alcance,
            // sostener [E] es lo unico que [E] hace ese frame.
            var caidoCercano = FindNearestDownedAlly();
            if (caidoCercano != null)
            {
                UpdateRevivalHold(caidoCercano);
                return;
            }
            if (circuloRevivir != null && !DemolicionEnCurso) circuloRevivir.SetVisible(false);

            // Interacción por cercanía (no por puntería): subir al vehículo
            // o equipar un arma tirada en el piso.
            var nearVehicle = Vehicle != null && !Vehicle.IsDestroyed && Vector3.Distance(Brain.Current.transform.position, Vehicle.transform.position) <= interactRadius
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
                if (!BloqueaAtajo("USA EL RADIAL: [Q] → TANQUE → SUBIRME YO")) EnterVehicle(nearVehicle);
            }
            else if (eToque)
            {
                if (nearPickup != null) nearPickup.EquipOn(Brain.Current.Weapon, Brain.Current.Id);
                else if (nearVehicle != null && !BloqueaAtajo("USA EL RADIAL: [Q] → TANQUE → SUBIRME YO")) EnterVehicle(nearVehicle);
            }

            var torretaCerca = TorretaFija.MasCercana(Brain.Current.transform.position, TorretaFija.AlcanceDeUso);
            // Un IInteractable en la mira o pegado al jugador manda sobre el resto del
            // cartel: es lo que un TAP de [Q] va a disparar (ver TryInteractuarConMira),
            // asi que el jugador tiene que poder leer ANTES de tocar la tecla que hay
            // algo interactuable y que hace.
            var promptInteraccion = PromptDeInteraccion();
            SetInstructionText(promptInteraccion != null ? promptInteraccion + "  ·  [Q] mantener: radial de ordenes"
                : nearVehicle != null ? "[E] Subir al vehiculo  ·  [Q] mantener: radial de ordenes"
                : nearPickup != null ? $"[E] Equipar {nearPickup.Kind}"
                : torretaCerca != null && torretaCerca.Libre ? "[E] Usar la ametralladora fija  ·  [Q] mantener: radial de ordenes"
                : BuildFpsInstruction(result));
        }

        // -----------------------------------------------------------
        // A4: mantener [E] 5 s revive a un caido
        // -----------------------------------------------------------
        public const float TiempoDeRevivir = 5f;
        CirculoDeProgreso circuloRevivir;

        Soldier FindNearestDownedAlly()
        {
            if (Squad == null || Brain.Current == null) return null;
            Soldier best = null;
            float bestDist = interactRadius;
            foreach (var s in Squad)
            {
                if (s == null || s.Health == null || s.Health.IsAlive) continue;
                float d = Vector3.Distance(Brain.Current.transform.position, s.transform.position);
                if (d <= bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        // La decision de "ya se sostuvo lo suficiente" entra como parametro
        // en vez de leerse aca adentro -- mismo motivo que ResolverGestoDeQ
        // en E2: asi la suite headless puede probar el revivir en si
        // (KeyBindings.ForzarInicioDePulsacion + HayPulsacionRegistrada) sin
        // depender de Keyboard.current, que no existe en Edit mode.
        public bool TryRevivir(Soldier caido, bool sostenidoLoSuficiente)
        {
            if (caido == null || caido.Health == null || caido.Health.IsAlive || !sostenidoLoSuficiente) return false;
            caido.Health.Initialize(caido.Id, caido.Health.MaxHealth);
            caido.Motor.ResetMotionState();
            GameLog.Line($"{caido.DisplayName} fue revivido");
            return true;
        }

        void UpdateRevivalHold(Soldier caido)
        {
            SetInstructionText($"Mantener [E] para revivir a {caido.DisplayName}");
            if (!KeyBindings.IsPressed(KeyBindings.Interactuar))
            {
                if (circuloRevivir != null) circuloRevivir.SetVisible(false);
                return;
            }

            float progreso = KeyBindings.HeldSeconds(KeyBindings.Interactuar) / TiempoDeRevivir;
            MostrarCirculoRevivir(Mathf.Clamp01(progreso));

            if (TryRevivir(caido, KeyBindings.IsHeld(KeyBindings.Interactuar, TiempoDeRevivir)))
            {
                if (circuloRevivir != null) circuloRevivir.SetVisible(false);
            }
        }

        static readonly Color RevivirFondo = new Color(0f, 0f, 0f, 0.4f);
        static readonly Color RevivirRelleno = new Color(0.35f, 0.9f, 0.45f);

        void MostrarCirculoRevivir(float progreso01)
        {
            if (circuloRevivir == null)
            {
                var canvasRoot = AimUiRef != null ? AimUiRef.transform.parent : null;
                if (canvasRoot == null) return;
                circuloRevivir = CirculoDeProgreso.Construir(canvasRoot, 46f, RevivirFondo, RevivirRelleno);
                circuloRevivir.gameObject.name = "CirculoRevivir";
                var rt = (RectTransform)circuloRevivir.transform;
                // Debajo del centro de la pantalla: no tapa la mira.
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.32f);
                rt.anchoredPosition = Vector2.zero;
            }
            circuloRevivir.SetVisible(true);
            circuloRevivir.SetProgreso(progreso01);
        }

        // -----------------------------------------------------------
        // [Ctrl]: habilidad especial de la clase que se maneja.
        //   - Medico: cura a un herido o reanima a un caido (cercano o en
        //     la mira). Independiente del [E] sostenido de arriba (A4) --
        //     ese sigue andando para CUALQUIER clase; esto es la version
        //     "especialista" del medico, mas rapida, atada a otra tecla.
        //   - Asalto / Explosivos (mismo RoleType.Assault: no hay una clase
        //     "Explosivos" separada, ver Demolicion.cs) y Francotirador:
        //     mientras se sostiene [Ctrl] apuntando (click derecho), el
        //     spread del arma baja progresivamente hasta un piso, con un
        //     tope de tiempo. Soltar [Ctrl], dejar de apuntar o moverse
        //     mucho reinicia el progreso a cero.
        // -----------------------------------------------------------
        public const float TiempoDeReanimarMedico = 2.5f;   // el especialista reanima el doble de rapido que el [E] generico (TiempoDeRevivir = 5f)
        public const float CuracionMedicoPorSegundo = 25f;  // mas rapido que el enfermero de IA (PedidoDeCuracion.CuracionPorSegundo = 12)
        public const float TiempoDeEnfoqueMax = 2.5f;       // segundos sosteniendo [Ctrl]+mira hasta la precision maxima

        float medicoAccionSegundos;
        float enfoqueSostenidoSegundos;
        float curacionAcumulada;
        CirculoDeProgreso circuloEnfoque;

        static readonly Color EnfoqueFondo = new Color(0f, 0f, 0f, 0.4f);
        static readonly Color EnfoqueRelleno = new Color(0.95f, 0.85f, 0.3f);

        void ActualizarHabilidadDeClase(AimResult result, bool ctrlSostenido, bool moviendoseMucho)
        {
            if (Brain.Current == null) return;

            switch (Brain.Current.Role)
            {
                case RoleType.Medic:
                    ActualizarHabilidadMedico(result, ctrlSostenido);
                    break;
                case RoleType.Assault:
                case RoleType.Sniper:
                    ActualizarEnfoquePrecision(result, ctrlSostenido, moviendoseMucho);
                    break;
                default:
                    // Cualquier otra clase (Flanker, etc.): sin habilidad de
                    // [Ctrl] todavia -- se asegura que no quede un enfoque
                    // viejo pegado si el jugador cambio de soldado.
                    Brain.Current.Weapon.SetEnfoque(0f);
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    break;
            }
        }

        // --- Medico: curar / reanimar con [Ctrl] ---------------------
        void ActualizarHabilidadMedico(AimResult result, bool ctrlSostenido)
        {
            // Prioridad al caido, igual que el [E] generico de arriba: un
            // companero muerto importa mas que uno solo herido.
            var caido = result.Type == AimTargetType.Caido ? result.Soldier : FindNearestDownedAlly();
            if (caido != null)
            {
                if (!ctrlSostenido)
                {
                    medicoAccionSegundos = 0f;
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    return;
                }
                medicoAccionSegundos += Time.deltaTime;
                MostrarCirculoEnfoque(Mathf.Clamp01(medicoAccionSegundos / TiempoDeReanimarMedico));
                SetInstructionText($"[E mantenido] Reanimando a {caido.DisplayName}...");
                if (medicoAccionSegundos >= TiempoDeReanimarMedico && TryRevivir(caido, true))
                {
                    medicoAccionSegundos = 0f;
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    Feedback.Accion(SfxKind.Revive, $"{caido.DisplayName} REANIMADO", caido.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.8f);
                }
                return;
            }

            // Sin caido a mano: si hay un herido (con vida pero no al
            // maximo) cerca o en la mira, se lo cura mientras se sostiene
            // [Ctrl]. En la mira tiene prioridad sobre el mas cercano --
            // apuntarle a proposito a un herido especifico entre varios.
            Soldier herido = (result.Type == AimTargetType.Ally && Herido(result.Soldier)) ? result.Soldier : FindNearestWoundedAlly();
            if (herido != null && ctrlSostenido)
            {
                medicoAccionSegundos = 0f;
                curacionAcumulada += CuracionMedicoPorSegundo * Time.deltaTime;
                int entero = Mathf.FloorToInt(curacionAcumulada);
                if (entero > 0)
                {
                    herido.Health.Heal(entero);
                    curacionAcumulada -= entero;
                }
                float frac = herido.Health.MaxHealth > 0 ? (float)herido.Health.Current / herido.Health.MaxHealth : 1f;
                MostrarCirculoEnfoque(Mathf.Clamp01(frac));
                SetInstructionText($"[E mantenido] Curando a {herido.DisplayName}...");
                if (herido.Health.Current >= herido.Health.MaxHealth)
                {
                    if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
                    Feedback.Accion(SfxKind.HealDone, $"{herido.DisplayName} CURADO", herido.transform.position, Feedback.Ok, aviso: true, pulso: false, volumen: 0.7f);
                }
                return;
            }

            medicoAccionSegundos = 0f;
            curacionAcumulada = 0f;
            if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
        }

        // Aliado vivo pero no al maximo de vida, mas cercano -- mismo patron que FindNearestDownedAlly.
        Soldier FindNearestWoundedAlly()
        {
            if (Squad == null || Brain.Current == null) return null;
            Soldier best = null;
            float bestDist = interactRadius;
            foreach (var s in Squad)
            {
                if (!Herido(s)) continue;
                float d = Vector3.Distance(Brain.Current.transform.position, s.transform.position);
                if (d <= bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        // --- Asalto / Explosivos / Francotirador: punteria fina con [Ctrl] ---
        void ActualizarEnfoquePrecision(AimResult result, bool ctrlSostenido, bool moviendoseMucho)
        {
            // "apuntando" = el mismo ADS de siempre (click derecho sostenido, Rig.EstaConZoom).
            bool apuntando = Rig != null && Rig.EstaConZoom;
            bool activo = ctrlSostenido && apuntando && !moviendoseMucho;

            enfoqueSostenidoSegundos = activo
                ? Mathf.Min(TiempoDeEnfoqueMax, enfoqueSostenidoSegundos + Time.deltaTime)
                : 0f;

            float progreso01 = Mathf.Clamp01(enfoqueSostenidoSegundos / TiempoDeEnfoqueMax);
            Brain.Current.Weapon.SetEnfoque(progreso01);

            if (activo && progreso01 > 0.01f)
            {
                MostrarCirculoEnfoque(progreso01);
                SetInstructionText(progreso01 >= 0.999f ? "[E mantenido] Punteria maxima" : "[E mantenido] Afinando la punteria...");
            }
            else if (circuloEnfoque != null) circuloEnfoque.SetVisible(false);
        }

        void MostrarCirculoEnfoque(float progreso01)
        {
            if (circuloEnfoque == null)
            {
                var canvasRoot = AimUiRef != null ? AimUiRef.transform.parent : null;
                if (canvasRoot == null) return;
                circuloEnfoque = CirculoDeProgreso.Construir(canvasRoot, 46f, EnfoqueFondo, EnfoqueRelleno);
                circuloEnfoque.gameObject.name = "CirculoEnfoque";
                var rt = (RectTransform)circuloEnfoque.transform;
                // Mismo anclaje vertical que el circulo de revivir pero del
                // otro lado del centro, para no superponerse si algun dia
                // coinciden en pantalla (dos jugadores distintos, replay, etc.).
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.24f);
                rt.anchoredPosition = Vector2.zero;
            }
            circuloEnfoque.SetVisible(true);
            circuloEnfoque.SetProgreso(progreso01);
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

        void HideFpsOnlyIndicators()
        {
            CoverHologram.Ocultar();
            ClearNearestAllyHighlight();
            if (mountIndicator != null) mountIndicator.Hide();
            if (highlightedRenderer != null)
            {
                SP.Presentation.CubeFxReactor.WriteTint(highlightedRenderer, highlightedOriginalColor);
                highlightedRenderer = null;
            }
        }

        // -----------------------------------------------------------
        // Coberturas: [Shift] muestra el holograma, [T] manda a cubrirse
        // -----------------------------------------------------------
        const float RadioCoberturaPiso = 1.6f;
        bool agachadoAntes;
        float ultimoTCoberturaTiempo = -999f;
        Soldier ultimoAliadoCobertura;
        Vector3 ultimoPuntoCobertura;
        VehicleKeysPanel panelDeTeclas;
        int ultimaSeleccion;

        public bool TryResolverCobertura(AimResult r, out Vector3 punto, out Collider dueno)
        {
            punto = default;
            dueno = null;
            if (r.Type == AimTargetType.Obstacle)
                return Coberturas.TryPuntoApuntado(r.Point, r.HitTransform, 0f, out punto, out dueno);
            if (r.Type == AimTargetType.Ground)
                return Coberturas.TryPuntoApuntado(r.Point, null, RadioCoberturaPiso, out punto, out dueno);
            return false;
        }

        Soldier AliadoLibreMasCercano(Vector3 punto, Soldier excluir)
        {
            return ActorRegistry.FindNearest(punto, s =>
                s != Brain.Current && s != excluir && s.Team == TeamId.Player
                && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy
                && !OrderService.LoManejaElJugador(s));
        }

        // Vista previa: con [Shift] apretado y apuntando a una cobertura,
        // holograma (90 % transparente) del aliado que la tomaria.
        void UpdateCoverPreview(Keyboard kb, AimResult result)
        {
            bool ver = KeyBindings.IsPressed(KeyBindings.VerTactico) || (OrdenesMenu != null && OrdenesMenu.Abierto && OrdenesMenu.Seleccion == MenuDeOrdenes.Cubrirse);
            if (!ver || !TryResolverCobertura(result, out var punto, out var dueno))
            {
                CoverHologram.Ocultar();
                return;
            }
            var aliado = AliadoLibreMasCercano(punto, null) ?? Brain.Current;
            var transformObstaculo = result.Type == AimTargetType.Obstacle ? result.HitTransform : (dueno != null ? dueno.transform : null);
            CoverHologram.Mostrar(aliado, punto, Coberturas.FrenteDe(punto, dueno), transformObstaculo);
        }

        // [T] sobre una cobertura: el aliado libre mas cercano va, se agacha
        // y se queda. Dos [T] seguidos reparten al SIGUIENTE aliado a la
        // SIGUIENTE cobertura. Publico para poder probarlo sin teclado.
        public bool IssueCoverOrderT(Vector3 punto, Collider dueno)
        {
            bool repique = Time.unscaledTime - ultimoTCoberturaTiempo < VentanaDobleT;
            ultimoTCoberturaTiempo = Time.unscaledTime;

            var elegido = AliadoLibreMasCercano(punto, repique ? ultimoAliadoCobertura : null);
            if (elegido == null) { RejectOrder("NO HAY ALIADOS LIBRES PARA CUBRIRSE"); return false; }

            if (repique)
            {
                var indices = Coberturas.IndicesCercanos(ultimoPuntoCobertura, 2, 8f);
                if (indices.Count > 1)
                {
                    punto = Coberturas.Puntos[indices[1]];
                    dueno = Coberturas.Duenos[indices[1]];
                }
            }

            bool ok = OrderService.IssueCoverOrder(elegido, punto, dueno);
            if (ok) { ultimoAliadoCobertura = elegido; ultimoPuntoCobertura = punto; }
            return ok;
        }

        // RTS: toda la seleccion a cubrirse, un punto distinto para cada uno.
        public int IssueCoverOrderForSelection(IReadOnlyList<Soldier> seleccion, Vector3 puntoApuntado, Transform obstaculo)
        {
            if (seleccion == null || seleccion.Count == 0) return 0;
            if (!Coberturas.TryPuntoApuntado(puntoApuntado, obstaculo, obstaculo != null ? 0f : RadioCoberturaPiso, out var basePunto, out _)) return 0;
            var indices = Coberturas.IndicesCercanos(basePunto, seleccion.Count, 9f);
            int n = 0;
            for (int i = 0; i < seleccion.Count; i++)
            {
                int idx = indices.Count == 0 ? -1 : indices[Mathf.Min(i, indices.Count - 1)];
                if (idx < 0) break;
                if (OrderService.IssueCoverOrder(seleccion[i], Coberturas.Puntos[idx], Coberturas.Duenos[idx])) n++;
            }
            return n;
        }

        // -----------------------------------------------------------
        // Tanque: todos suben / todos bajan
        // -----------------------------------------------------------
        int AliadosEnCaminoAlVehiculo(Vehicle v)
        {
            int n = 0;
            if (Squad == null) return 0;
            foreach (var s in Squad)
                if (s != null && s.Brain != null && s.Brain.MountTargetVehicle == v && s.Health.IsAlive) n++;
            return n;
        }

        // [G] dentro del tanque: manda a TODOS los aliados libres a subir,
        // hasta llenar los asientos.
        public int SubirATodos(Vehicle v)
        {
            if (v == null || v.IsDestroyed) return 0;
            int libres = v.Capacity - v.OccupantCount - AliadosEnCaminoAlVehiculo(v);
            int n = 0;
            while (libres > 0)
            {
                var next = FindNextSquadmateToBoard(v);
                if (next == null) break;
                OrderService.IssueMountOrder(next, v);
                n++; libres--;
            }
            if (panelDeTeclas != null) panelDeTeclas.Destellar("G");
            if (n > 0)
                Feedback.Accion(SfxKind.BoardAll, $"TODOS SUBEN ({n})", v.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.6f);
            else
                RejectOrder(v.HasAnyRoom ? "NO HAY MAS ALIADOS PARA SUBIR" : "VEHICULO LLENO");
            return n;
        }

        // [I] dentro del tanque: bajan todos MENOS el jugador.
        public int BajarATodos(Vehicle v)
        {
            if (v == null) return 0;
            int n = 0;
            foreach (var o in new List<Soldier>(v.Occupants))
            {
                if (o == Brain.Current) continue;
                if (v.Dismount(o)) n++;
            }
            if (panelDeTeclas != null) panelDeTeclas.Destellar("I");
            if (n > 0)
                Feedback.Accion(SfxKind.ExitAll, $"TODOS BAJAN ({n})", v.transform.position, Feedback.Warn, aviso: true, pulso: true, volumen: 0.6f);
            else
                RejectOrder("NO HAY NADIE MAS ADENTRO");
            return n;
        }

        void ActualizarPanelDeTeclas()
        {
            if (Vehicle == null) return;
            if (panelDeTeclas == null)
            {
                var canvas = ModeToast != null ? ModeToast.GetComponentInParent<Canvas>() : null;
                if (canvas != null) panelDeTeclas = VehicleKeysPanel.Asegurar(canvas.rootCanvas.transform);
            }
            if (panelDeTeclas == null) return;
            // Mirando por la mira del canon / la metralleta el panel tapa la optica: se esconde.
            panelDeTeclas.SetVisible(!(Rig != null && Rig.AdsBlend > 0.3f));
            panelDeTeclas.Actualizar(Vehicle, currentSeat, AliadosEnCaminoAlVehiculo(Vehicle));
        }

        // -----------------------------------------------------------
        // E3: doble [T] reparte, Shift+[T] distribuye
        // -----------------------------------------------------------
        // Antes [T] siempre mandaba al mismo (el vivo libre mas cercano al
        // punto): apretarlo dos veces seguidas al mismo lugar repetia la
        // orden sobre el MISMO soldado en vez de sumar al segundo.
        public const float VentanaDobleT = 0.5f;
        float ultimoTUnscaledTime = -999f;
        Soldier ultimoTSoldado;

        // Separado de la lectura de teclado (shiftHeld entra como
        // parametro) para poder probarlo desde la suite sin depender de
        // Keyboard.current, que no existe en Edit mode -- mismo criterio
        // que TryRevivir en A4.
        public void IssueGroundOrderT(Vector3 punto, bool shiftHeld)
        {
            if (shiftHeld)
            {
                // Shift+[T]: TODA la escuadra libre, repartida en formacion
                // -- no uno por uno, de una.
                var libres = new List<Soldier>();
                if (Squad != null)
                {
                    foreach (var s in Squad)
                        if (s != null && s != Brain.Current && s.Health != null && s.Health.IsAlive && !OrderService.LoManejaElJugador(s))
                            libres.Add(s);
                }
                if (libres.Count == 0) return;
                var puntos = OrderService.FormationPoints(punto, libres.Count);
                for (int i = 0; i < libres.Count; i++) OrderService.IssueMoveOrder(libres[i], puntos[i]);
                GameLog.Line($"Shift+[T]: se repartieron {libres.Count} aliados en formacion");
                return;
            }

            // Dos [T] rapidos (dentro de la ventana) al candidato de
            // siempre reparten al SIGUIENTE mas cercano en vez de repetirle
            // la orden al primero.
            bool esRepique = Time.unscaledTime - ultimoTUnscaledTime < VentanaDobleT;
            ultimoTUnscaledTime = Time.unscaledTime;

            var candidato = OrderService.FindNearestFreeAlly(punto, TeamId.Player, Brain.Current);
            if (esRepique && ultimoTSoldado != null && candidato == ultimoTSoldado)
            {
                candidato = ActorRegistry.FindNearest(punto, s =>
                    s != Brain.Current && s != ultimoTSoldado && s.Team == TeamId.Player
                    && s.Health != null && s.Health.IsAlive && !OrderService.LoManejaElJugador(s));
            }
            if (candidato == null) return;

            OrderService.IssueMoveOrder(candidato, punto);
            ultimoTSoldado = candidato;
        }

        void UpdateAimHighlight(AimResult result)
        {
            Renderer target = null;
            if ((result.Type == AimTargetType.Ally || result.Type == AimTargetType.Enemy) && result.Soldier != null)
                target = result.Soldier.GetComponentInChildren<Renderer>();
            else if (result.Type == AimTargetType.Vehicle && result.Vehicle != null)
                target = result.Vehicle.GetComponentInChildren<Renderer>();

            if (target == highlightedRenderer) return;

            // MaterialPropertyBlock y NO sharedMaterial.color: desde el item
            // 230 los soldados de un mismo equipo COMPARTEN material, asi
            // que escribirle el color aca teñiria a todo el equipo de
            // blanco al apuntarle a uno solo.
            if (highlightedRenderer != null)
                SP.Presentation.CubeFxReactor.WriteTint(highlightedRenderer, highlightedOriginalColor);

            highlightedRenderer = target;
            if (target != null)
            {
                highlightedOriginalColor = SP.Presentation.CubeFxReactor.ReadTint(target);
                SP.Presentation.CubeFxReactor.WriteTint(target, Color.Lerp(highlightedOriginalColor, Color.white, 0.65f));
            }
        }

        // B4: circulo en la base de lo apuntado. Se llama tanto desde el
        // rayo de la mira FPS como desde el rayo del mouse en RTS -- el
        // mismo AimResult, la misma logica, sin duplicar nada por modo.
        void UpdateAimRing(AimResult result)
        {
            // El cubito de estado de un aliado solo se ve mientras se le apunta.
            SP.Presentation.SquadStateIndicatorView.Apuntar(
                result.Type == AimTargetType.Ally ? result.Soldier : null);

            // Unificado: TODO lo interactuable (aliado, enemigo, vehiculo, muro
            // demolible, caido reanimable, torreta) se resalta igual, para que
            // cualquier cosa con la que el radial de [Q] pueda hacer algo se
            // reconozca de un vistazo. Antes Caido y Torreta quedaban afuera:
            // se ofrecia [Q] REANIMAR o la torreta salia en el radial, pero
            // nada en pantalla marcaba que ahi habia algo interactuable.
            bool show = result.HitTransform != null && (
                result.Type == AimTargetType.Ally || result.Type == AimTargetType.Enemy ||
                result.Type == AimTargetType.Vehicle || result.Type == AimTargetType.Obstacle ||
                result.Type == AimTargetType.Caido || result.Type == AimTargetType.Torreta);

            if (!show)
            {
                if (aimRing != null) aimRing.gameObject.SetActive(false);
                return;
            }

            if (aimRing == null) aimRing = SelectionRingFx.Spawn(result.HitTransform, AimRingColor, 0.9f);
            aimRing.gameObject.SetActive(true);
            aimRing.Target = result.HitTransform;
        }

        // [G] apuntando a un vehículo: sube UN aliado por apretada, al
        // mismo criterio que [U] (el más cercano que todavía no esté
        // adentro ni ya en camino a subir) -- para poder llenar los
        // asientos de a uno, apretando varias veces seguidas, sin mandar
        // dos veces al mismo. [I] es la tecla dedicada para bajarlos a
        // todos; [G] no hace doble función.
        //
        // BUG REAL: antes, con el vehículo ya ocupado, [G] los bajaba a
        // TODOS en vez de sumar uno más -- y encima usaba
        // FindNearestFreeAlly, que no descarta a quien ya está adentro
        // (Mount() solo lo desactiva, no lo saca del registro), así que
        // apretar [G] dos veces con alguien ya montado le repetía la
        // orden a ESE MISMO en vez de sumar al siguiente. FindNextSquadmateToBoard
        // (la función de [U]) sí filtra correctamente a los ya montados y
        // a los que ya están en camino.
        public void GOrderOnVehicle(Vehicle vehicle)
        {
            // Ordenar subir a una carcasa destruida antes mandaba al
            // aliado a caminar hasta ahí para nada: Vehicle.Mount() ya
            // rechaza el intento al llegar, pero eso no se sabe hasta
            // que llega -- una caminata entera sin ningún resultado ni
            // aviso.
            if (vehicle.IsDestroyed) return;
            if (!vehicle.HasAnyRoom) { RejectOrder("VEHICULO LLENO"); return; }

            var next = FindNextSquadmateToBoard(vehicle);
            if (next != null) OrderService.IssueMountOrder(next, vehicle);
            else RejectOrder("NO HAY MAS ALIADOS PARA SUBIR");
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
            mountIndicator.Show(result.Vehicle, incoming, !result.Vehicle.IsDestroyed && result.Vehicle.HasAnyRoom);
        }

        // C3: "con una unidad seleccionada, apuntar a algo montable
        // muestra si se puede o no" -- version RTS del mismo indicador,
        // con la seleccion actual en vez de la heuristica de cercania de
        // la version FPS (ahi no existe "seleccion").
        void UpdateVehicleMountIndicatorRts(AimResult result)
        {
            if (result.Type != AimTargetType.Vehicle || Selection.Selected.Count == 0)
            {
                if (mountIndicator != null) mountIndicator.Hide();
                return;
            }

            if (mountIndicator == null) mountIndicator = VehicleMountIndicator.Create();

            var incoming = new List<Soldier>();
            foreach (var s in Selection.Selected)
                if (s != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && result.Vehicle.RoleOf(s) == null)
                    incoming.Add(s);

            mountIndicator.Show(result.Vehicle, incoming, !result.Vehicle.IsDestroyed && result.Vehicle.HasAnyRoom);
        }

        void PossessSquadIndex(int index)
        {
            if (Squad == null || index < 0 || index >= Squad.Count) return;
            TryPossess(Squad[index]);
        }

        // Buffer reutilizable para la orden de ataque con [F] cuando no
        // hay seleccion RTS activa: el jugador posee un solo cuerpo a la
        // vez, asi que un array de un elemento alcanza y evita generar
        // basura por frame.
        readonly Soldier[] soloBrainCurrenteBuffer = new Soldier[1];
        IEnumerable<Soldier> SoloBrainCurrente()
        {
            soloBrainCurrenteBuffer[0] = Brain.Current;
            return soloBrainCurrenteBuffer;
        }

        // Pisadas del jugador: cadencia fija mientras camina, con la
        // superficie detectada por raycast hacia abajo. El piso de todo el
        // nivel hoy es concreto (WorldArtPipeline.ReemplazarGround), asi que
        // "concreto" es el default real y "pasto" queda listo para
        // cualquier terreno futuro que no matchee ese nombre.
        const float FootstepInterval = 0.42f;
        float footstepTimer;
        void UpdateFootsteps(bool moving)
        {
            if (!moving) { footstepTimer = 0f; return; }
            footstepTimer -= Time.deltaTime;
            if (footstepTimer > 0f) return;
            footstepTimer = FootstepInterval;

            var pos = Brain.Current.transform.position;
            var kind = SfxKind.FootstepConcrete;
            if (Physics.Raycast(pos + Vector3.up * 0.4f, Vector3.down, out var hit, 1.5f))
            {
                var n = hit.collider.name;
                bool esConcreto = n.IndexOf("Concreto", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Piso", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!esConcreto) kind = SfxKind.FootstepGrass;
            }
            AudioDirector.PlayAt(kind, pos, 0.55f);
        }

        // El soldado propio y vivo que esta seleccionado SI Y SOLO SI es
        // el unico. Null en cualquier otro caso.
        public Soldier SoldadoUnicoSeleccionado()
        {
            if (Selection == null || Selection.Selected.Count != 1) return null;
            var s = Selection.Selected[0];
            if (s == null || s.Health == null || !s.Health.IsAlive || s.Team != TeamId.Player) return null;
            return s;
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
            if (Brain.Current == target) return false;
            if (torretaActual != null) SalirDeTorreta();

            // Pedido explicito: antes esto se rechazaba de plano ("esta
            // dentro de un vehiculo"). Ahora, si esta montado, se toma
            // control de ESE asiento -- lo mismo que ya hacia
            // EnterVehicleViewFromRts al apuntarle al vehiculo entero
            // (que toma "el primer ocupante"), pero entrando por el
            // soldado puntual que se quiso poseer.
            if (!target.gameObject.activeInHierarchy)
            {
                var vehicle = FindVehicleContaining(target);
                var role = vehicle != null ? vehicle.RoleOf(target) : null;
                if (vehicle == null || role == null)
                {
                    if (DeadNotice != null) DeadNotice.Show($"{target.DisplayName} no esta disponible");
                    OrderService.PlayRejectSound();
                    return false;
                }

                var previousInVehicle = Brain.Current;
                PossessionService.Swap(Brain, target);
                Vehicle = vehicle;
                Rig.SetMode(ControlMode.Fps);
                EnterPossessedVehicleSeat(role.Value);

                if (ModeToast != null) ModeToast.Show($"CONTROLAS A {target.DisplayName.ToUpperInvariant()}", 1.2f);
                if (previousInVehicle != null && previousInVehicle.Brain != null && !previousInVehicle.Brain.IsPossessedByPlayer)
                    GameLog.Line($"{previousInVehicle.DisplayName} vuelve al control de la IA");

                return true;
            }

            var previous = Brain.Current;
            PossessionService.Swap(Brain, target);

            // El pitch es estado del rig, no del soldado: sin esto heredas
            // el angulo vertical del anterior y podes aparecer mirando al
            // piso sin ningun motivo.
            Rig.ResetPitch();
            Rig.BeginFollowBlend(PossessBlendSeconds);
            if (Rig.Mode == ControlMode.Rts) Rig.SetMode(ControlMode.Fps);

            if (ModeToast != null) ModeToast.Show($"CONTROLAS A {target.DisplayName.ToUpperInvariant()}", 1.2f);

            // El anterior recupera su AiBrain y empieza a actuar solo. Sin
            // aviso, el jugador ve moverse a un soldado que creia suyo.
            if (previous != null && previous.Brain != null && !previous.Brain.IsPossessedByPlayer)
                GameLog.Line($"{previous.DisplayName} vuelve al control de la IA");

            return true;
        }

        // Vehiculo actual (el unico del mapa hoy) o, si en el futuro hay
        // mas de uno, el que de verdad tiene a este soldado entre sus
        // ocupantes -- no asumido por el campo Vehicle, que es fijo por
        // Inspector.
        static Vehicle FindVehicleContaining(Soldier soldier)
        {
            var vehicles = SP.Core.WorldSystemsRegistry.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v == null) continue;
                var occupants = v.Occupants;
                for (int j = 0; j < occupants.Count; j++)
                    if (occupants[j] == soldier) return v;
            }
            return null;
        }

        // [U]/[I] no apuntan a nada, asi que necesitan resolver "el
        // vehiculo" solos. Con un solo vehiculo en el mapa (hoy) esto
        // alcanza; el campo Vehicle queda como respaldo si el registro
        // todavia no se poblo.
        Vehicle FindTheVehicle()
        {
            // El tanque ALIADO: en el nivel 4x hay tanques enemigos registrados tambien,
            // y "subir/bajar todos" jamas debe tocar a uno de ellos.
            if (Vehicle != null && !Vehicle.IsDestroyed && Vehicle.Bando == TeamId.Player) return Vehicle;
            var vehicles = SP.Core.WorldSystemsRegistry.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
                if (vehicles[i] != null && vehicles[i].Bando == TeamId.Player) return vehicles[i];
            return vehicles.Count > 0 && vehicles[0] != null && vehicles[0].Bando == TeamId.Player ? vehicles[0] : Vehicle;
        }

        // El mas cercano de la escuadra que todavia puede subir: vivo,
        // activo (no ya montado) y sin una orden de movimiento en curso
        // que ya lo lleve a ESTE vehiculo -- sin este ultimo chequeo,
        // apretar [U] dos veces seguido mandaba al mismo de nuevo en vez
        // de sumar al siguiente.
        Soldier FindNextSquadmateToBoard(Vehicle vehicle)
        {
            if (Squad == null) return null;
            Soldier best = null;
            float bestDist = float.MaxValue;
            foreach (var s in Squad)
            {
                if (s == null || s == Brain.Current) continue;
                if (!s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                // BUG REAL: el de la metralleta queda de pie sobre el chasis,
                // o sea ACTIVO en la escena, asi que el filtro de arriba no
                // lo descartaba: con uno ya montado ahi, cada [U]/[G] siguiente
                // le repetia la orden a ESE MISMO en vez de sumar al proximo
                // aliado, y el resto de la escuadra nunca subia.
                if (vehicle.RoleOf(s) != null) continue;

                var brain = s.GetComponent<AiBrain>();
                if (brain != null && brain.CurrentOrderDestination.HasValue &&
                    Vector3.Distance(brain.CurrentOrderDestination.Value, vehicle.transform.position) < 1f)
                    continue;

                float d = Vector3.Distance(s.transform.position, vehicle.transform.position);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        // Poseer exigia recordar el numero de cada soldado o apuntarle con
        // precision -- ninguna de las dos cosas es viable bajo fuego.
        void PossessNearestAlly()
        {
            if (Brain.Current == null) return;
            var nearest = ActorRegistry.FindNearest(Brain.Current.transform.position, s =>
                s.Health.IsAlive && s.Team == Brain.Current.Team && s != Brain.Current && s.gameObject.activeInHierarchy && s.Role != RoleType.Civilian);
            if (nearest == null) { RejectOrder("NO HAY ALIADO CERCA"); return; }
            TryPossess(nearest);
        }

        // Los atajos por indice fallan cuando ese soldado murio. El ciclo
        // salta a los caidos y recorre a los vivos en orden estable (el del
        // escuadron), asi que siempre da un resultado util.
        void CycleToNextLivingAlly() => CycleLivingAlly(+1);

        // direction +1 avanza y -1 retrocede sobre el mismo orden estable.

        void EquipFromCatalog(WeaponKind kind) => EquipWeaponHotkey(kind);

        public void EquipSlot(int slot)
        {
            var w = Brain.Current != null ? Brain.Current.Weapon : null;
            if (w == null || slot < 0 || slot >= w.Loadout.Count) return;
            EquipWeaponHotkey(w.Loadout[slot]);
        }

        // La rueda del mouse cicla la MISMA lista pública que expone
        // WeaponHolder.Loadout -- antes esto tenía su propio array fijo en
        // paralelo (WeaponCycle) que por construcción no podía divergir del
        // catálogo 1/2/3, pero eran dos fuentes de la "verdad" separadas.
        // Ahora hay una sola.
        void CycleWeapon(int direction)
        {
            if (Brain.Current == null || Brain.Current.Weapon == null) return;
            if (direction >= 0) Brain.Current.Weapon.CycleNext();
            else Brain.Current.Weapon.CyclePrevious();
        }

        // Público para que la demo/tutorial automáticos puedan probar los
        // atajos 1/2/3 sin depender de que haya un teclado físico. Pasa por
        // EquipFromLoadout (no EquipWeapon directo) para que CurrentLoadoutIndex
        // quede sincronizado -- si no, elegir "2" con la tecla y después
        // seguir con la rueda arrancaría el ciclo desde el índice viejo.
        public void EquipWeaponHotkey(WeaponKind kind)
        {
            if (Brain.Current == null || Brain.Current.Weapon == null) return;
            int idx = Brain.Current.Weapon.Loadout.IndexOf(kind);
            if (idx >= 0)
            {
                bool cambio = Brain.Current.Weapon.CurrentWeaponKind != kind;
                Brain.Current.Weapon.EquipFromLoadout(idx);
                if (cambio) Feedback.Visual($"[{idx + 1}] {WeaponCatalog.Get(kind).DisplayName.ToUpperInvariant()}", null, Feedback.Info, aviso: true, pulso: false);   // suena el desenfunde propio del arma
                return;
            }

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

        public bool TryIssueVehicleMoveOrder(Vector3 point, Vehicle vehicle = null)
        {
            // El vehiculo objetivo es un parametro opcional (no siempre el
            // campo Vehicle fijo por Inspector): en RTS la orden tiene que
            // ir al vehiculo REALMENTE seleccionado (Selection.SelectedVehicle),
            // no al que el jugador ocupo alguna vez. Sin argumento se cae al
            // campo de siempre, para no romper los llamadores de FPS/artillero
            // que ya pasan por "el vehiculo en el que estoy".
            if (vehicle == null) vehicle = Vehicle;
            if (vehicle == null || vehicle.Driver == null) return false;

            var vb = vehicle.GetComponent<VehicleBrain>();

            if (Vector3.Distance(point, vehicle.transform.position) < RedundantOrderDistance) return false;
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
                    return $"{result.Soldier.DisplayName}   ·   [Q] mantener: radial (poseer, ordenes)   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Enemy:
                    return $"Enemigo: {result.Soldier.DisplayName}   ·   [Q] mantener: radial → ATACAR   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Vehicle:
                    return "[Q] mantener: radial → TANQUE (subir / bajar / ir)   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Obstacle:
                    return "Obstáculo   ·   [Q] mantener: radial → CUBRIRSE / DEMOLER   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Ground:
                    return "[Q] mantener: radial → IR ALLI   ·   [Click der.] mandar el tanque aquí (si hay conductor)   ·   [Click] disparar   ·   [TAB] vista RTS";
                case AimTargetType.Torreta:
                    return "[E] usar la ametralladora fija   ·   [Q] mantener: radial   ·   [TAB] vista RTS";
                case AimTargetType.Caido:
                    return "Aliado caido   ·   [Q] mantener: radial → REANIMAR (si queda un medico)   ·   [E] mantener 5 s: reanimarlo vos   ·   [TAB] vista RTS";
                default:
                    return "[WASD] moverse   ·   [Shift] correr   ·   [Ctrl] agacharse   ·   [E] tocar: interactuar · mantener: habilidad de clase   ·   [F] cuchillo   ·   [G] granada   ·   [Click] disparar   ·   [Click der.] mantener: mirar por la mira   ·   [Q] mantener: radial   ·   [C] mantener: coberturas   ·   [TAB] vista RTS   ·   [F4] modo dios";
            }
        }



        // ------------------------------------------------------------------
        // UI: solo bloquea lo que se puede clickear
        // ------------------------------------------------------------------
        static readonly List<RaycastResult> resultadosUi = new List<RaycastResult>();

        bool PunteroSobreUiInteractiva(Vector2 pantalla)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var datos = new PointerEventData(es) { position = pantalla };
            resultadosUi.Clear();
            es.RaycastAll(datos, resultadosUi);
            foreach (var r in resultadosUi)
            {
                if (r.gameObject == null) continue;
                if (r.gameObject.GetComponentInParent<Selectable>() != null) return true;
                if (r.gameObject.GetComponentInParent<SP.UI.MinimapFollow>() != null) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // [F4] MODO DIOS y [C] VISTA TACTICA
        // ------------------------------------------------------------------
        void AlternarModoDios()
        {
            bool on = ModoDios.Alternar();
            Feedback.Accion(on ? SfxKind.Swap : SfxKind.EmptyClick, null, null, on ? Feedback.Ok : Feedback.Warn, aviso: false, pulso: false, volumen: 0.5f);
            if (ModeToast != null)
                ModeToast.Show(on ? "MODO DIOS: NADIE DE TU BANDO RECIBE DAÑO  ·  [F4] PARA APAGAR" : "MODO DIOS APAGADO", 2.2f);
        }

        // Coberturas del piso y rutas de patrulla enemigas: NO estan siempre a la vista. Se ven mientras
        // se mantiene [C], y las coberturas ademas cuando el radial esta parado sobre CUBRIRSE.
        void ActualizarVistaTactica()
        {
            bool tecla = KeyBindings.IsPressed(KeyBindings.VerTactico);
            bool radialCubrirse = OrdenesMenu != null && OrdenesMenu.Abierto && OrdenesMenu.Seleccion == MenuDeOrdenes.Cubrirse;
            Coberturas.MostrarMarcas(tecla || radialCubrirse);
            if (Rig != null) Rig.MostrarRutas(tecla);
        }

        // ------------------------------------------------------------------
        // AMETRALLADORA FIJA
        // ------------------------------------------------------------------
        public bool EnTorretaFija => torretaActual != null;
        public TorretaFija TorretaActual => torretaActual;
        TorretaFija torretaPendiente;

        public bool UsarTorreta(TorretaFija t)
        {
            if (t == null || Brain == null || Brain.Current == null) return false;
            if (currentSeat.HasValue) { RejectOrder("BAJATE DEL TANQUE PRIMERO"); return false; }
            if (torretaActual == t) return true;
            if (!t.Libre) { RejectOrder("LA TORRETA YA ESTA OCUPADA"); return false; }
            var yo = Brain.Current;
            var horizontal = t.transform.position - yo.transform.position; horizontal.y = 0f;
            float d = horizontal.magnitude;
            if (d > TorretaFija.AlcanceDeUso)
            {
                // Lejos: camina solo hasta ella y la ocupa al llegar.
                torretaPendiente = t;
                destinoAuto = t.transform.position;
                Avisar("VOY A LA TORRETA...");
                return true;
            }
            string motivo;
            if (!t.Ocupar(yo, out motivo)) { RejectOrder(motivo ?? "NO SE PUEDE"); return false; }
            torretaActual = t;
            torretaPendiente = null;
            Rig.ResetPitch();
            Feedback.Accion(SfxKind.SeatChange, "EN LA AMETRALLADORA FIJA", t.transform.position, Feedback.Ok, aviso: false, pulso: true, volumen: 0.5f);
            if (ModeToast != null) ModeToast.Show("AMETRALLADORA FIJA: apunta, dispara · [E] salir", 2.5f);
            return true;
        }

        public void SalirDeTorreta()
        {
            torretaPendiente = null;
            var t = torretaActual;
            torretaActual = null;
            if (t == null) return;
            t.Liberar();
            Avisar("SALISTE DE LA TORRETA");
        }

        static float HorizontalA(Vector3 a, Vector3 b) { var v = b - a; v.y = 0f; return v.magnitude; }

        bool OrdenDeTorreta(int sub, AimResult aim)
        {
            if (sub == 1)
            {
                if (torretaActual == null) { RejectOrder("NO ESTAS EN UNA TORRETA"); return false; }
                SalirDeTorreta();
                return true;
            }
            var t = aim.Type == AimTargetType.Torreta ? aim.Torreta : TorretaFija.MasCercana(Brain.Current.transform.position, TorretaFija.AlcanceDeUso * 3f);
            if (t == null) { RejectOrder("APUNTA A UNA TORRETA FIJA"); return false; }
            return UsarTorreta(t);
        }

        // Cada frame en FPS: sincroniza el estado con la torreta (murio, la sacaron, llego a ella).
        void ActualizarTorretaFija()
        {
            if (torretaActual != null && (Brain.Current == null || torretaActual.Ocupante != Brain.Current)) { torretaActual = null; }
            if (torretaPendiente != null)
            {
                if (!destinoAuto.HasValue) torretaPendiente = null;   // WASD lo cancelo
                else if (Brain.Current != null && HorizontalA(Brain.Current.transform.position, torretaPendiente.transform.position) <= TorretaFija.AlcanceDeUso * 0.6f)
                {
                    var t = torretaPendiente;
                    destinoAuto = null;
                    torretaPendiente = null;
                    UsarTorreta(t);
                }
            }
        }

        // ------------------------------------------------------------------
        // Contexto del radial: solo lo que se puede hacer con lo que se apunta
        // ------------------------------------------------------------------
        int PrimeraOpcionVisible(int categoria)
        {
            if (OrdenesMenu != null && OrdenesMenu.Abierto && OrdenesMenu.Seleccion == categoria && OrdenesMenu.OpcionesVisibles.Count > 0)
                return OrdenesMenu.OpcionesVisibles[0];
            return 0;
        }

        static bool Herido(Soldier s) => s != null && s.Health != null && s.Health.IsAlive && s.Health.Current < s.Health.MaxHealth;

        public ContextoRadial ConstruirContextoRadial(AimResult aim)
        {
            var c = new ContextoRadial();
            c.Mostrar(MenuDeOrdenes.IrAlli, false);
            c.Mostrar(MenuDeOrdenes.Cubrirse, false);
            c.Mostrar(MenuDeOrdenes.Posicion, false);

            var yo = Brain != null ? Brain.Current : null;
            var curar = new List<int>();
            var tanque = new List<int>();
            string apunta = "";

            switch (aim.Type)
            {
                case AimTargetType.Enemy:
                    if (aim.Soldier != null)
                    {
                        c.Mostrar(MenuDeOrdenes.Atacar, true);
                        apunta = $"Enemigo: {aim.Soldier.DisplayName}";
                    }
                    break;

                case AimTargetType.Ally:
                    if (aim.Soldier != null && aim.Soldier != yo)
                    {
                        var a = aim.Soldier;
                        apunta = $"Aliado: {a.DisplayName} ({a.Health.Current}/{a.Health.MaxHealth})";
                        if (a.Role != RoleType.Civilian)
                        {
                            if (Herido(a) && PedidoDeCuracion.MedicoDisponible(a) != null) curar.Add(2);
                            c.Mostrar(MenuDeOrdenes.Poseer, true, 4);
                        }
                        else if (Herido(a) && PedidoDeCuracion.MedicoDisponible(a) != null) curar.Add(2);
                    }
                    break;

                case AimTargetType.Caido:
                    if (aim.Soldier != null)
                    {
                        apunta = $"Caido: {aim.Soldier.DisplayName}";
                        if (PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null) curar.Add(3);
                    }
                    break;

                case AimTargetType.Obstacle:
                {
                    var m = Demolicion.MarcadorApuntado(aim.HitTransform, aim.Point);
                    string motivo;
                    if (m != null && Demolicion.EsDemolible(m, out motivo))
                    {
                        var opciones = new List<int>();
                        bool yoAsalto = yo != null && yo.Role == RoleType.Assault && yo.Health.IsAlive;
                        bool aliadoAsalto = false;
                        foreach (var d in DestinatariosDeOrden()) if (d.Role == RoleType.Assault) { aliadoAsalto = true; break; }
                        if (aliadoAsalto) opciones.Add(0);
                        if (yoAsalto) opciones.Add(1);
                        if (DemoledorAsalto.HayEnCurso) opciones.Add(2);
                        apunta = "Muro destructible" + (opciones.Count == 0 ? " (necesitas un soldado de ASALTO)" : "");
                        if (opciones.Count > 0) c.Mostrar(MenuDeOrdenes.Demoler, true, opciones.ToArray());
                    }
                    break;
                }

                case AimTargetType.Vehicle:
                    if (aim.Vehicle != null && !aim.Vehicle.IsDestroyed && aim.Vehicle.Bando == TeamId.Player)
                    {
                        apunta = "Tanque aliado";
                        if (!currentSeat.HasValue) tanque.Add(3);
                        if (aim.Vehicle.HasAnyRoom) tanque.Add(0);
                        if (aim.Vehicle.OccupantCount > 0) tanque.Add(1);
                        if (aim.Vehicle.Driver != null) tanque.Add(2);
                    }
                    break;

                case AimTargetType.Torreta:
                    if (aim.Torreta != null)
                    {
                        apunta = aim.Torreta.Libre ? "Ametralladora fija" : "Ametralladora fija (ocupada)";
                        if (aim.Torreta.Libre) c.Mostrar(MenuDeOrdenes.Torreta, true, 0);
                    }
                    break;
            }

            // Estados propios: no dependen de la mira.
            if (yo != null && yo.Health != null && yo.Health.Current < yo.Health.MaxHealth * 0.7f)
            {
                bool hayQuien = yo.Role == RoleType.Medic ? PedidoDeCuracion.BotiquinListoEn <= 0f : PedidoDeCuracion.MedicoDisponible(yo) != null;
                if (hayQuien && !curar.Contains(0)) curar.Insert(0, 0);
            }
            if (currentSeat.HasValue && Vehicle != null && !Vehicle.IsDestroyed)
            {
                tanque.Clear();
                tanque.Add(4);
                if (Vehicle.OccupantCount > 1) tanque.Add(1);
                if (Vehicle.Driver != null) tanque.Add(2);
                if (Vehicle.HasAnyRoom) tanque.Add(0);
                if (apunta.Length == 0) apunta = "Vas en el tanque";
            }
            if (torretaActual != null)
            {
                c.Mostrar(MenuDeOrdenes.Torreta, true, 1);
                if (apunta.Length == 0) apunta = "En la ametralladora fija";
            }

            if (curar.Count > 0) c.Mostrar(MenuDeOrdenes.Curar, true, curar.ToArray());
            if (tanque.Count > 0) c.Mostrar(MenuDeOrdenes.Tanque, true, tanque.ToArray());
            c.Apuntando = apunta;
            return c;
        }

        // Texto sobre la mira: dice QUE ofrece el radial para lo apuntado (dorado = accion contextual).
        void ActualizarPromptContextual(AimResult aim)
        {
            if (AimUiRef == null) return;
            if (OrdenesMenu != null && OrdenesMenu.Abierto) return;
            var yo = Brain != null ? Brain.Current : null;
            string texto = null;
            bool destacado = true;
            switch (aim.Type)
            {
                case AimTargetType.Enemy:
                    texto = aim.Soldier != null ? $"[Q] toque: ATACAR a {aim.Soldier.DisplayName}  ·  [Q] mantener: menu" : null; break;
                case AimTargetType.Ally:
                    if (aim.Soldier == null) break;
                    if (aim.Soldier.Role == RoleType.Civilian) { texto = Herido(aim.Soldier) && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null ? $"[Q] toque: CURAR a {aim.Soldier.DisplayName} ({aim.Soldier.Health.Current}/{aim.Soldier.Health.MaxHealth})" : $"Civil: {aim.Soldier.DisplayName}"; destacado = Herido(aim.Soldier); }
                    else if (Herido(aim.Soldier) && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null) texto = $"[Q] toque: CURAR a {aim.Soldier.DisplayName} ({aim.Soldier.Health.Current}/{aim.Soldier.Health.MaxHealth})  ·  [Q] mantener: POSEER / menu";
                    else { texto = $"[Q] toque: {aim.Soldier.DisplayName} TE SIGUE  ·  [Q] mantener: POSEER / menu"; destacado = false; }
                    break;
                case AimTargetType.Caido:
                    texto = aim.Soldier != null && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null ? $"[Q] toque: REANIMAR a {aim.Soldier.DisplayName}  ·  [Q] mantener: menu" : aim.Soldier != null ? $"{aim.Soldier.DisplayName} esta caido (no queda medico)" : null;
                    destacado = aim.Soldier != null && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null;
                    break;
                case AimTargetType.Obstacle:
                {
                    var m = Demolicion.MarcadorApuntado(aim.HitTransform, aim.Point);
                    string motivo;
                    if (m != null && Demolicion.EsDemolible(m, out motivo))
                    {
                        bool asalto = (yo != null && yo.Role == RoleType.Assault) || DestinatariosDeOrden().Exists(x => x.Role == RoleType.Assault);
                        texto = asalto ? "[Q] DEMOLER este muro (carga de 4 s)" : "Muro destructible: hace falta un soldado de ASALTO";
                        destacado = asalto;
                    }
                    break;
                }
                case AimTargetType.Vehicle:
                    if (aim.Vehicle != null && !aim.Vehicle.IsDestroyed && aim.Vehicle.Bando == TeamId.Player) texto = "[E] SUBIR AL TANQUE  ·  [Q] toque: subir a todos  ·  [Q] mantener: menu del tanque";
                    else if (aim.Vehicle != null && aim.Vehicle.IsDestroyed) { texto = "Vehiculo destruido"; destacado = false; }
                    break;
                case AimTargetType.Torreta:
                    if (aim.Torreta != null) texto = aim.Torreta.Libre ? "[E] USAR LA AMETRALLADORA FIJA  ·  [Q] radial" : "Ametralladora fija (ocupada)";
                    destacado = aim.Torreta != null && aim.Torreta.Libre;
                    break;
            }
            if (texto != null) AimUiRef.PonerPromptContextual(texto, destacado);
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
            if (PlayerHealth != null) PlayerHealth.gameObject.SetActive(false); MirillaView.Instancia?.Ocultar();
            if (SelectionCount != null) SelectionCount.SetModeVisible(true);
            HideFpsOnlyIndicators();
            if (bodyHiddenFor != null) { bodyHiddenFor.SetBodyVisible(true); bodyHiddenFor.Motor.SetCrouching(false); bodyHiddenFor = null; }
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
                if (Mathf.Abs(scroll) > 0.01f) Rig.ZoomHaciaCursor(scroll * rtsZoomSpeed * Time.deltaTime, mouse.position.ReadValue());
            }

            string selectionLabel = Selection.SelectedVehicle != null ? "vehiculo seleccionado" : $"{Selection.Selected.Count} seleccionados";
            SetInstructionText($"[Arrastrar] seleccionar · [Shift+Click] sumar · [Click der.] mover la selección · [Ctrl+Click der.] trazar recorrido · [Q] mantener: radial · [C] mantener: coberturas y rutas · [WASD] panear · [Rueda] zoom al cursor · [TAB] vista FPS · {selectionLabel}");

            if (mouse == null || Rig.Cam == null) return;

            UpdateDragSelection(kb, mouse);

            var screenRay = Rig.Cam.ScreenPointToRay(mouse.position.ReadValue());

            // B4 y C3 en RTS: mismo raycast que ya se calculaba para
            // resolver el click, reusado para el anillo bajo el cursor y
            // la marca de montable sin pagar un segundo Physics.Raycast
            // por frame.
            var resultRts = Aim.Evaluate(screenRay, null);
            if (OrdenesMenu != null && OrdenesMenu.Abierto) OrdenesMenu.MoverSeleccion(mouse.delta.ReadValue());
            else ultimoResultadoDeMira = resultRts;
            UpdateAimRing(resultRts);
            UpdateVehicleMountIndicatorRts(resultRts);
            ActualizarCursorRts(resultRts);

            // Pedido explicito: mantener click derecho apretado NO mueve
            // la camara. Antes, sostenerlo y mover la mano de mas (aunque
            // fuera solo para terminar de apuntar la orden, no para
            // panear) corria la vista -- y si ademas se sostenia mas de
            // 180ms, ArrastreDerecho lo clasificaba como paneo y la orden
            // ni se emitia al soltar. El paneo de camara sigue existiendo
            // (WASD, mas abajo), simplemente click derecho ya no es una
            // de sus formas de dispararlo.
            if (mouse.rightButton.wasPressedThisFrame)
                rightPressStartedOverUi = PunteroSobreUiInteractiva(mouse.position.ReadValue());

            bool rightClickOrder = mouse.rightButton.wasReleasedThisFrame && !rightPressStartedOverUi;

            // Feedback de seleccion (sonido + aviso) cuando cambia la cantidad.
            int cantSel = Selection.Selected.Count;
            if (cantSel != ultimaSeleccion)
            {
                if (cantSel > 0)
                    Feedback.Accion(SfxKind.Select, cantSel == 1 ? "1 SELECCIONADO" : $"{cantSel} SELECCIONADOS", null, Feedback.Info, aviso: true, pulso: false, volumen: 0.35f);
                ultimaSeleccion = cantSel;
            }
            UpdateCoverPreviewRts(kb, screenRay);

            // [T] o click derecho: mover a todos los seleccionados ahí --
            // o al vehículo, si es él quien está seleccionado (requiere
            // conductor propio adentro, como en FPS). "!dragging" es el
            // recuadro de selección por click IZQUIERDO, no el derecho.
            bool pidioOrden = (AtajosDeTecladoHeredados && kb.tKey.wasPressedThisFrame) || (rightClickOrder && !dragging);

            // Con Ctrl apretado el mismo gesto NO ordena: marca un punto
            // del recorrido, que no arranca hasta [Espacio]. Se resuelve
            // antes y apaga el pedido de orden -- si cayera adentro del
            // bloque de abajo, el mismo click emitiria la orden ademas de
            // marcar el punto.
            if (pidioOrden && ctrlHeld)
            {
                pidioOrden = false;
                var marca = Aim.Evaluate(screenRay, null);
                if (marca.Type != AimTargetType.Ground) RejectOrder("MARCA EN EL PISO");
                else if (!TrazadoDeCamino.Marcar(marca.Point))
                    RejectOrder(TrazadoDeCamino.Cantidad >= TrazadoDeCamino.MaximoDePuntos
                        ? "RECORRIDO LLENO" : "PUNTO NO VALIDO");
                else if (ModeToast != null)
                    ModeToast.Show($"PUNTO {TrazadoDeCamino.Cantidad}  ·  [ESPACIO] ARRANCA", 1.0f);
            }

            if (pidioOrden)
            {
                var result = Aim.Evaluate(screenRay, null);
                // Con Shift la orden se ENCOLA detras de lo ya planificado
                // en vez de reemplazarlo: es lo que permite trazar una ruta
                // de varios tramos.
                bool queued = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

                // Sobre una COBERTURA (el obstaculo o su disco celeste): la
                // seleccion va a cubrirse, un punto para cada uno.
                bool esCobertura = Selection.SelectedVehicle == null && Selection.Selected.Count > 0
                    && TryResolverCobertura(result, out _, out _);

                // BUG REAL ("en RTS a veces no toma la orden de moverse"): la orden de mover exigia que el
                // cursor cayera justo sobre el PISO. Con el cursor sobre un aliado, un cuerpo caido, una torreta,
                // un techo o un muro (o sobre nada), no pasaba absolutamente nada y no se avisaba. Ahora el
                // destino es el piso que queda bajo el cursor, salvo enemigos y vehiculos (que tienen su propia orden).
                if (!esCobertura && result.Type != AimTargetType.Ground && result.Type != AimTargetType.Enemy && result.Type != AimTargetType.Vehicle
                    && AimTargeting.PisoBajoRayo(screenRay, out var pisoBajoCursor))
                    result = new AimResult { Type = AimTargetType.Ground, Point = pisoBajoCursor };

                if (Selection.SelectedVehicle == null && Selection.Selected.Count == 0)
                {
                    RejectOrder("NADIE SELECCIONADO: ARRASTRA UN CUADRO O CLICK SOBRE UN ALIADO");
                    pidioOrden = false;
                }

                if (esCobertura)
                {
                    int cubiertos = IssueCoverOrderForSelection(Selection.Selected, result.Point,
                        result.Type == AimTargetType.Obstacle ? result.HitTransform : null);
                    if (cubiertos == 0) RejectOrder("NO HAY COBERTURA LIBRE AHI");
                }
                else if (result.Type == AimTargetType.Ground)
                {
                    if (Selection.SelectedVehicle != null)
                    {
                        // Antes esto fallaba en silencio si nadie manejaba
                        // el vehiculo (TryIssueVehicleMoveOrder devuelve
                        // false sin avisar por que): el jugador seleccionaba
                        // el tanque, le daba la orden de moverse, y no
                        // pasaba nada -- parecia que la seleccion O el
                        // movimiento estaban rotos cuando en realidad hacia
                        // falta un conductor adentro primero.
                        if (Selection.SelectedVehicle.Driver == null)
                            RejectOrder("EL VEHICULO NECESITA UN CONDUCTOR");
                        else
                            TryIssueVehicleMoveOrder(result.Point, Selection.SelectedVehicle);
                    }
                    else if (Selection.Selected.Count > 0)
                    {
                        // Ordenar sobre un obstaculo no hacia nada y no
                        // avisaba: el soldado se trababa contra el borde y
                        // el jugador creia que la orden se habia dado.
                        if (!OrderService.IsValidDestination(result.Point)) RejectOrder("DESTINO BLOQUEADO");
                        else OrderService.IssueFormationOrderForSelection(Selection.Selected, result.Point, Vector3.forward, currentFormation, queued);
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

            // Mismo criterio que la version FPS de [G] (ver GOrderOnVehicle):
            // suma a la seleccion que todavia no este adentro, no expulsa a
            // nadie -- [I] es la unica tecla que baja gente.
            if (AtajosDeTecladoHeredados && kb.gKey.wasPressedThisFrame)
            {
                var result = Aim.Evaluate(screenRay, null);
                if (result.Type == AimTargetType.Vehicle && !result.Vehicle.IsDestroyed)
                {
                    if (!result.Vehicle.HasAnyRoom) { RejectOrder("VEHICULO LLENO"); }
                    else
                    {
                        var boardable = new List<Soldier>();
                        foreach (var s in Selection.Selected)
                            if (s != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && result.Vehicle.RoleOf(s) == null)
                                boardable.Add(s);
                        if (boardable.Count > 0) OrderService.IssueMountOrderForSelection(boardable, result.Vehicle);
                        else RejectOrder("NO HAY MAS ALIADOS PARA SUBIR");
                    }
                }
            }

            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.Poseer))
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
            // 213: clic sobre el minimapa emite la orden en el punto del
            // MUNDO correspondiente, sin tener que panear la camara hasta
            // ahi. Se chequea antes que el clic normal para que el clic
            // sobre el minimapa no sea interpretado como clic en el mundo.
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && MinimapRef != null && Selection.Selected.Count > 0)
            {
                var minimapRt = MinimapRef.transform as RectTransform;
                if (minimapRt != null)
                {
                    Vector2 local;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            minimapRt, mouse.position.ReadValue(), null, out local))
                    {
                        Vector3 destino;
                        if (MinimapRef.TryMinimapPointToWorld(local, out destino))
                        {
                            OrderService.IssueFormationOrderForSelection(
                                Selection.Selected, destino, Vector3.forward, currentFormation);
                            AlertQueue.Push("ORDEN DESDE EL MINIMAPA", AlertPriority.Media, 1.2f);
                            return;
                        }
                    }
                }
            }

            if (KeyBindings.WasPressed(KeyBindings.Reagrupar) && Selection.Selected.Count > 0)
            {
                OrderService.RegroupSelection(Selection.Selected, currentFormation);
                if (ModeToast != null) ModeToast.Show("REAGRUPANDO");
            }

            // [B] retirada: alejarse del enemigo mas cercano (217)
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.Retirada) && Selection.Selected.Count > 0)
            {
                OrderService.IssueRetreatOrderForSelection(Selection.Selected);
                if (ModeToast != null) ModeToast.Show("RETIRADA");
            }

            // [K] cicla la formacion con la que se emiten las ordenes (210)
            if (AtajosDeTecladoHeredados && KeyBindings.WasPressed(KeyBindings.CiclarFormacion))
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

            // Descartar un recorrido todavia sin arrancar es lo mismo que
            // cancelar una orden, y no necesita seleccion: los puntos son
            // del jugador, no de nadie en particular.
            if (KeyBindings.WasPressed(KeyBindings.CancelarOrden) && TrazadoDeCamino.HayTrazado)
            {
                TrazadoDeCamino.Limpiar();
                if (ModeToast != null) ModeToast.Show("RECORRIDO DESCARTADO");
            }

            if (KeyBindings.WasPressed(KeyBindings.CancelarOrden) && Selection.Selected.Count > 0)
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
                Feedback.Accion(SfxKind.EmptyClick, "ORDEN CANCELADA", null, Feedback.Warn, aviso: true, pulso: false, volumen: 0.5f);
            }

            UpdateControlGroups(kb);
            SeleccionarConFuncion(kb);
            UpdateFormationPreview(mouse, screenRay);

            // [Espacio] recentra la camara en el centroide de la escuadra
            // viva -- la tecla mas grande y accesible para la accion mas
            // repetida en vista tactica, para cuando la camara se pierde
            // paneando por el mapa.
            // [Espacio] tiene dos trabajos y el recorrido gana: si hay uno
            // trazado, recentrar la camara es lo ultimo que el jugador
            // quiere en ese momento. Sin trazado se comporta igual que
            // siempre.
            if (KeyBindings.WasPressed(KeyBindings.Recentrar) && TrazadoDeCamino.HayTrazado)
            {
                int tramos = TrazadoDeCamino.Ejecutar(Selection.Selected);
                if (tramos > 0) { if (ModeToast != null) ModeToast.Show($"RECORRIDO DE {tramos} TRAMOS"); }
                else { RejectOrder("NADIE SELECCIONADO"); TrazadoDeCamino.Limpiar(); }
            }
            else if (KeyBindings.WasPressed(KeyBindings.Recentrar) && Squad != null)
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
                // Item 218: la linea de ruta tiene que desaparecer en el
                // mismo instante que los fantasmas de destino, no un frame
                // despues -- si no, quedaba una linea vieja pegada en
                // pantalla apuntando a un destino que ya no se estaba
                // previsualizando.
                if (SP.Ai.PathPreview.Instance != null) SP.Ai.PathPreview.Instance.Hide();
                return;
            }

            var result = Aim.Evaluate(screenRay, null);
            if (result.Type != AimTargetType.Ground) { ClearFormationGhosts(); if (SP.Ai.PathPreview.Instance != null) SP.Ai.PathPreview.Instance.Hide(); return; }

            var spots = OrderService.FormationPoints(result.Point, Vector3.forward, Selection.Selected.Count, currentFormation);
            EnsureGhostCount(spots.Length);
            for (int i = 0; i < spots.Length; i++)
                formationGhosts[i].transform.position = new Vector3(spots[i].x, 0.06f, spots[i].z);

            // 218: vista previa de la ruta, no solo del destino. Antes el
            // jugador veia DONDE iban a terminar los fantasmas pero no
            // POR DONDE iba a ir la escuadra -- si habia un obstaculo en
            // el medio, se enteraba recien cuando los soldados chocaban
            // contra el. El origen es el centroide de la seleccion, que
            // es de donde parte la orden en conjunto.
            if (SP.Ai.PathPreview.Instance != null)
            {
                Vector3 centroide = Vector3.zero;
                int vivos = 0;
                foreach (var s in Selection.Selected)
                {
                    if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                    centroide += s.transform.position;
                    vivos++;
                }
                if (vivos > 0) SP.Ai.PathPreview.Instance.Show(centroide / vivos, result.Point);
            }
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
                go.GetComponent<MeshRenderer>().sharedMaterial = SP.Presentation.SafeMaterial.Create(new Color(0.35f, 0.85f, 0.35f));
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

        // Traduce los ids guardados a soldados vivos para las tarjetas.
        // Se llama al GUARDAR un grupo y a intervalo desde la vista, no por
        // frame desde aca.
        void RefreshGroupCards()
        {
            if (GroupCards == null) return;
            var listas = new System.Collections.Generic.List<System.Collections.Generic.List<Soldier>>();
            for (int g = 1; g <= SP.UI.GroupCardsView.SlotCount; g++)
            {
                var miembros = new System.Collections.Generic.List<Soldier>();
                if (controlGroups.TryGetValue(g, out var ids))
                    foreach (var id in ids)
                    {
                        var s = SP.Core.ActorRegistry.FindById(id);
                        if (s != null) miembros.Add(s);
                    }
                listas.Add(miembros);
            }
            GroupCards.SetGroups(listas);
        }

        void UpdateControlGroups(Keyboard kb)
        {
            // BUG REAL: wasPressedThisFrame no "consume" la tecla -- el
            // mismo [1]-[5] usado para elegir una orden del menu ([Q]
            // sostenido) tambien llegaba aca en el mismo frame y
            // guardaba/recuperaba un grupo de control sin que el jugador
            // lo pidiera (y con [Ctrl] sostenido, hasta PISABA un grupo
            // guardado). Mientras el menu esta abierto, estas teclas son
            // suyas.
            if (OrdenesMenu != null && OrdenesMenu.Abierto) return;

            var digitKeys = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key,
                                    kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

            for (int i = 0; i < digitKeys.Length; i++)
            {
                if (!digitKeys[i].wasPressedThisFrame) continue;
                int group = i + 1;

                // Ronda 11 (punto 18): en RTS [1] [2] [3] seleccionan al soldado 1/2/3 de la escuadra (Shift suma; doble toque centra).
                // Los grupos de control pasan a [4]-[9] (Ctrl + numero guarda, numero recupera).
                if (i < 3)
                {
                    if (!ctrl) SeleccionarSoldadoDeEscuadra(i, kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    continue;
                }

                if (ctrl)
                {
                    if (Selection.Selected.Count == 0) continue;
                    var ids = new List<int>();
                    foreach (var s in Selection.Selected) ids.Add(s.Id);
                    controlGroups[group] = ids;
                    RefreshGroupCards();
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
            // Del plan del usuario, dos renglones que resultaron ser EL
            // MISMO defecto:
            //
            //   "El cuadro de seleccion aveces se rompe. Testear 20 veces
            //    cuando se rompe y porq y solucionar"
            //   "Aveces en RTS cuando selecciono un soldado y aprieto click
            //    derecho en varios destinos algunos los omite"
            //
            // `dragging` solo se apagaba en wasReleasedThisFrame. Si la
            // soltada no llega -- porque el juego se pauso a mitad del
            // arrastre, porque el soldado murio y arranco la camara de
            // muerte, porque se recargo la escena, o simplemente porque se
            // fue el foco de la ventana con el boton apretado -- queda
            // encendido PARA SIEMPRE. Medido: con dragging en true y el
            // boton izquierdo suelto, un frame entero de este metodo lo
            // deja igual, encendido y con el cuadro pintado.
            //
            // Y lo que lo vuelve grave es lo otro: la orden de click
            // derecho en RTS se emite con "rightClickOrder && !dragging".
            // O sea que mientras el cuadro esta colgado, TODAS las ordenes
            // de click derecho se descartan en silencio. El jugador ve un
            // rectangulo pegado en pantalla y clicks que no hacen nada, y
            // parecen dos fallas distintas.
            //
            // La regla: si creo estar arrastrando pero el boton no esta
            // apretado, no estoy arrastrando. Cierra todas las salidas de
            // una vez, incluidas las que todavia no existen.
            if (dragging && !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                if (SelectionBox != null) SelectionBox.gameObject.SetActive(false);
            }

            // Solo cuenta la UI con la que se puede INTERACTUAR (botones, minimapa). Antes cualquier grafico con
            // raycast (un aviso, un panel decorativo) bloqueaba la seleccion y las ordenes en toda la pantalla.
            if (PunteroSobreUiInteractiva(mouse.position.ReadValue()))
            {
                if (!dragging) return;
            }

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
            // BUG REAL encontrado sacando capturas de RTS: el Canvas de
            // UI_Canvas es Screen Space - Overlay (verificado en runtime),
            // no Screen Space - Camera. Para Overlay, RectTransformUtility
            // exige pasar cam=null -- pasarle Rig.Cam (como hacia esto
            // antes) le pide a Unity que haga un raycast de camara y
            // proyecte sobre el plano del RectTransform en 3D, un calculo
            // que no tiene nada que ver con como el Overlay en realidad se
            // dibuja (mapeo directo pixel-a-local). Con la camara de FPS
            // (cerca del origen) el resultado quedaba, por coincidencia,
            // parecido al correcto; con la camara RTS (lejos y en angulo)
            // el cuadro de seleccion salia consistentemente minusculo --
            // medido: pedir un rectangulo de 1400x650 px en pantalla daba
            // un tamano de canvas de apenas 16x15, en vez de los 700x325
            // esperados (con el factor 2 del CanvasScaler).
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var local);
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
            if (Squad == null) return;

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

        void OnApplicationQuit() => PlayerPrefs.Save();
        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PlayerPrefs.Save();
        }
    }
}
