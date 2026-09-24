using System.Collections;
using UnityEngine;
using SP.Actors;
using SP.Presentation;

namespace SP.CameraSystem
{
    public enum ControlMode { Fps, Rts }

    // Posee la cámara y delega su posición en el modo activo (FPS u RTS).
    public class CameraRig : MonoBehaviour
    {
        // Acceso directo para los sistemas que necesitan sacudir la camara
        // muy seguido (cada disparo del cañon, cada explosion). Antes
        // TurretWeapon hacia un FindAnyObjectByType POR DISPARO, que es un
        // barrido de escena en el peor momento posible.
        public static CameraRig Instance { get; private set; }

        void OnEnable() { Instance = this; AplicarRutasEnCamara(); }
        void OnDisable() { if (Instance == this) Instance = null; }

        // Solo para HeadlessTestRunner: ese harness corre en Edit Mode, y
        // Unity no manda OnEnable a un MonoBehaviour comun (sin
        // ExecuteAlways) fuera de Play -- medido: ni siquiera forzando un
        // ciclo enabled=false/true en el rig real de la escena, Instance
        // se queda en null. Sin este hook, cualquier sistema que dependa
        // de CameraRig.Instance (WorldUiDirector, AttackLineManager) es
        // imposible de probar en ese harness.
        public static void EnsureInstanceForTests(CameraRig rig)
        {
            if (rig != null) Instance = rig;
        }

        [SerializeField] Camera cam;
        [SerializeField] float rtsHeight = 30f;
        [SerializeField] float rtsMinHeight = 12f;
        [SerializeField] float rtsMaxHeight = 70f;
        // Pedido explicito: "no quiero que sea ortogonal, quiero que sea
        // perspectiva". 90° puros (mirando derecho hacia abajo) en una
        // camara en perspectiva se ve IGUAL que ortografico -- sin angulo no
        // hay profundidad que mostrar. 55° es un picado real (estilo
        // Age of Empires/Company of Heroes): se sigue leyendo el mapa desde
        // arriba pero los soldados y el terreno muestran volumen de verdad.
        [SerializeField] Vector3 rtsLookEuler = new Vector3(90f, 0f, 0f);

        // Punto del SUELO que la camara de RTS esta mirando. En ortografico
        // alcanzaba con la posicion XZ de la camara (miraba derecho hacia
        // abajo, asi que lo que habia debajo YA era el foco); en perspectiva
        // con picado la camara tiene que vivir detras-y-arriba de ese punto,
        // no exactamente encima, o el encuadre queda corrido. Ver
        // RtsCameraPositionFor.
        Vector3 rtsFocusPoint;
        float rtsCurrentHeight;
        // El zoom con la rueda anima hacia esta altura (y ancla el punto bajo el cursor mientras dura).
        float rtsTargetHeight = -1f;
        Vector2 zoomPixel;
        bool zoomAnclado;

        // Pitch (mirar arriba/abajo) es propio de la cámara, no del cuerpo
        // del soldado: el cuerpo solo gira en yaw (RotateYaw), y acá se le
        // suma un pitch local para que el mouse en Y también haga algo.
        float pitch;
        const float MaxPitch = 80f;

        // Zoom de mirilla: mantener click derecho angosta el FOV, como
        // apuntar con la mira. Un lerp simple, no un corte seco.
        [SerializeField] float normalFov = 60f;
        [SerializeField] float zoomFov = 25f;
        [SerializeField] float zoomLerpSpeed = 12f;
        bool zoomed;

        public ControlMode Mode { get; private set; } = ControlMode.Fps;

        public void SetCamera(Camera c)
        {
            cam = c;
            // La camara es SIEMPRE en perspectiva ahora (RTS incluido):
            // pedido explicito de que no sea ortogonal.
            if (cam != null) { cam.orthographic = false; normalFov = cam.fieldOfView; }
            AplicarRutasEnCamara();
        }

        // Pedido explicito: "el enemigo esta muy lejos y no veo las
        // esferas amarillas del waypoints" -- esta capa se habia sacado
        // del culling mask a proposito en una sesion anterior (era
        // referencia de diseño, no algo pensado para el jugador). Ese
        // criterio cambio: ahora el jugador SI tiene que poder verlas
        // (para poder confirmar visualmente que un enemigo sigue su
        // ronda), asi que en vez de excluir la capa, se fuerza a que este
        // incluida. FPS y RTS comparten la unica Camera del juego, asi
        // que esto alcanza para los dos modos.
        // Las rutas y esferas de patrulla ya NO estan siempre a la vista: aparecen solo mientras se
        // sostiene la tecla de vista tactica ([C]). Antes ensuciaban toda la pantalla.
        bool rutasVisibles;
        public bool RutasVisibles => rutasVisibles;
        public void MostrarRutas(bool visibles)
        {
            if (rutasVisibles == visibles) return;
            rutasVisibles = visibles;
            AplicarRutasEnCamara();
        }

        void AplicarRutasEnCamara()
        {
            if (cam == null) return;
            int layer = LayerMask.NameToLayer(PatrolRouteLine.LayerName);
            if (layer < 0) return;
            if (rutasVisibles) cam.cullingMask |= (1 << layer);
            else cam.cullingMask &= ~(1 << layer);
        }

        public void SetZoomed(bool value) => zoomed = value;

        // APUNTAR (mantener click derecho) pasa la camara a PRIMERA PERSONA de verdad: del
        // encuadre por encima del hombro se desliza al ojo del soldado (o a la mira del
        // canon en el tanque). 0 = encuadre normal, 1 = camara en el ojo / mira.
        public const float VelocidadDeApuntado = 5f;
        float adsBlend;
        public float AdsBlend => adsBlend;
        public float AdsBlendSuave => SmoothStep01(adsBlend);

        // Al apuntar la mira NO esta quieta: respira. Un balanceo lento (ruido suave) que crece con el
        // aumento; agachado se reduce y quieto baja. Mueve la camara de verdad, o sea tambien el tiro.
        public float EscalaDeRespiracion = 1f;
        public Vector2 Respiracion { get; private set; }

        void AplicarRespiracion()
        {
            float e = SmoothStep01(adsBlend);
            if (!zoomed || e < 0.6f) { Respiracion = Vector2.zero; return; }
            float amp = 0.11f * Mathf.Sqrt(Mathf.Max(1f, ZoomFactor)) * EscalaDeRespiracion * (e - 0.6f) / 0.4f;
            float t = Time.time;
            var r = new Vector2((Mathf.PerlinNoise(t * 0.85f, 0.13f) - 0.5f) * 2f * amp, (Mathf.PerlinNoise(0.71f, t * 0.7f) - 0.5f) * 2f * amp);
            Respiracion = r;
            transform.rotation = transform.rotation * Quaternion.Euler(r.y, r.x, 0f);
        }

        // Zoom REAL: el aumento (x2, x6...) se traduce a FOV con la tangente,
        // para que "x6" sea de verdad seis veces mas cerca y no una resta de grados.
        public float ZoomFactor { get; private set; } = 2f;
        public void SetZoomFactor(float factor)
        {
            ZoomFactor = Mathf.Max(1.05f, factor);
            zoomFov = 2f * Mathf.Atan(Mathf.Tan(normalFov * 0.5f * Mathf.Deg2Rad) / ZoomFactor) * Mathf.Rad2Deg;
        }

        // Lo consulta la optica del arma para saber si mostrarse. Estaba
        // guardado en un bool privado que solo leia LateUpdate.
        public bool EstaConZoom => zoomed;
        public float FovDeZoom => zoomFov;
        // Adonde VA el FOV, no donde esta: cam.fieldOfView pasa varios
        // frames lerpeando entre 60 y 25, y quien necesite compararse
        // contra el zoom tiene que mirar el destino o se queda con un
        // valor a mitad de camino.
        public float FovObjetivo => zoomed ? zoomFov : normalFov;

        void LateUpdate()
        {
            // El offset continuo se captura y se limpia SIEMPRE, incluso si
            // salimos temprano: LateUpdate tiene varias salidas, y si el
            // reseteo viviera solo en el camino normal, el offset se
            // acumularia sin limite cada vez que el rig sale antes.
            Vector3 frame = frameOffset;
            frameOffset = Vector3.zero;

            if (cam == null) return;

            recoilPitch = Mathf.MoveTowards(recoilPitch, 0f, Time.deltaTime * recoilRecoverySpeed);

            UpdateBob(Mode == ControlMode.Fps);

            if (Mode == ControlMode.Fps)
            {
                adsBlend = Mathf.MoveTowards(adsBlend, zoomed ? 1f : 0f, Time.unscaledDeltaTime * VelocidadDeApuntado);
                float goal = zoomed ? zoomFov : normalFov;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, goal, Time.deltaTime * zoomLerpSpeed);
                ApplyCameraOffsets(frame);
                AplicarRespiracion();
                return;
            }

            // En RTS: converge el PUNTO DE FOCO hacia el objetivo de paneo
            // (ya acotado a los bordes del mapa en Pan()), y la posicion de
            // la camara se deriva de ese foco + altura + el angulo de
            // picado (RtsCameraPositionFor) -- no se mueve directo, porque
            // en perspectiva la camara no vive exactamente encima de lo que
            // mira. No corre durante una transicion de camara
            // (BeginTransition), que ya tiene su propio control total de la
            // posicion.
            if (!IsTransitioning)
            {
                if (panTargetInitialized)
                {
                    var focusXZ = new Vector3(rtsFocusPoint.x, 0f, rtsFocusPoint.z);
                    var targetXZ = new Vector3(panTarget.x, 0f, panTarget.z);
                    var lerped = Vector3.Lerp(focusXZ, targetXZ, Time.deltaTime * panSmoothSpeed);
                    rtsFocusPoint = new Vector3(lerped.x, rtsFocusPoint.y, lerped.z);
                }
                transform.rotation = Quaternion.Euler(rtsLookEuler);
                transform.position = RtsCameraPositionFor(rtsFocusPoint, rtsCurrentHeight);
                AnimarZoom(Time.unscaledDeltaTime);
            }

            ApplyCameraOffsets(frame);
        }

        // Deriva la posicion de la camara a partir de DONDE mira (foco en
        // el suelo) y de QUE TAN ALTO esta -- inverso de "la camara esta en
        // (x,height,z) mirando con rtsLookEuler, ¿que punto del suelo cae
        // en el centro de pantalla?". Con picado puro (90°) la camara vive
        // justo encima del foco; con picado angulado (el caso real ahora)
        // tiene que vivir ademas retrasada en Z, si no el foco queda
        // arriba/abajo del centro de la pantalla en vez de en el medio.
        Vector3 RtsCameraPositionFor(Vector3 focus, float height)
        {
            Vector3 forward = Quaternion.Euler(rtsLookEuler) * Vector3.forward;
            float descenso = -forward.y; // positivo: cuanto mira hacia abajo
            if (descenso < 0.01f) return focus + Vector3.up * height; // picado casi nulo: evita dividir por ~0
            float t = height / descenso;
            return focus - forward * t;
        }

        public void AddPitch(float delta) => pitch = Mathf.Clamp(pitch + delta, -MaxPitch, MaxPitch);
        public void ResetPitch() => pitch = 0f;

        // Expuesto para quien tenga que apuntar de verdad con este pitch
        // (no solo mostrarlo): el disparo del jugador y el arma en la
        // mano necesitan el mismo angulo que ya mueve la camara, o mirar
        // arriba/abajo giraria la vista sin mover ni la mira ni la bala.
        public float Pitch => pitch;

        // Culatazo de camara: un canal SEPARADO del pitch que controla el
        // mouse, para que decaiga solo sin que el jugador tenga que
        // compensarlo bajando el mouse el mismo tanto que subio (eso
        // seria acumular error de puntería en cada disparo). Sube de
        // golpe con cada tiro y se recupera con un MoveTowards.
        float recoilPitch;
        [SerializeField] float recoilRecoverySpeed = 40f; // grados/seg
        [SerializeField] float maxRecoilPitch = 25f; // tope duro: varias fuentes sumando no deben mandar la mira al cielo
        public void KickRecoil(float degrees)
        {
            if (!CameraFxSettings.Enabled) return;
            recoilPitch = Mathf.Clamp(recoilPitch + degrees, 0f, maxRecoilPitch);
        }
        public float RecoilPitch => recoilPitch;

        // Sacudida DIRECCIONAL: una vibracion aleatoria no comunica de
        // donde vino la fuerza. El culatazo del cañon tiene que empujar la
        // camara en un sentido concreto -- el opuesto al eje de disparo --
        // para que se lea como retroceso y no como ruido.
        Vector3 shakeOffset;
        [SerializeField] float shakeRecoverySpeed = 9f;
        public Vector3 ShakeOffset => shakeOffset;

        // Sin tope, varias fuentes sumando a la vez (disparo del cañon +
        // explosion cercana + impacto recibido) podian mandar la camara
        // lejos del personaje. El presupuesto unico es lo que garantiza
        // que la sacudida sea legible y no un temblor infinito.
        [SerializeField] float maxShakeMagnitude = 0.45f;
        public float MaxShakeMagnitude => maxShakeMagnitude;

        public void KickDirectional(Vector3 worldDirection, float magnitude)
        {
            if (!CameraFxSettings.Enabled) return;
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            shakeOffset += worldDirection.normalized * magnitude;
            shakeOffset = Vector3.ClampMagnitude(shakeOffset, maxShakeMagnitude);
        }

        // Canal aparte para fuentes CONTINUAS (inercia del vehiculo,
        // balanceo al caminar): si entraran por shakeOffset pelearian
        // contra su propio decaimiento cada frame. Se consume y se limpia
        // una vez por LateUpdate.
        Vector3 frameOffset;

        public void AddFrameOffset(Vector3 offset)
        {
            if (!CameraFxSettings.Enabled) return;
            frameOffset += offset;
        }

        // Balanceo al caminar (183). Solo en primera persona: en RTS la
        // camara mira el mapa desde arriba con picado fijo, balancearla
        // ahi solo marea.
        // BUG REAL (pedido: "no quiero q vibre la pantalla al caminar"):
        // amp=0.035 con bobFrequency=9 rad/s hacia un rebote vertical en
        // Abs(sin) -- a esa frecuencia el "paso" se sentia como un
        // temblor constante, no una caminata. Se expone como campos
        // serializados (antes eran literales fijos en el codigo) y el
        // default baja a 0: sigue disponible para quien quiera un bob
        // sutil, pero no vibra por default.
        [SerializeField] float bobAmplitude = 0f;
        [SerializeField] float bobFrequency = 9f;
        bool walking;
        float bobPhase;
        public Vector3 BobOffset { get; private set; }

        public void SetWalking(bool value) => walking = value;

        void UpdateBob(bool firstPerson)
        {
            bool active = firstPerson && walking && CameraFxSettings.Enabled && bobAmplitude > 0f;
            if (active) bobPhase += Time.deltaTime * bobFrequency;
            float amp = active ? bobAmplitude : 0f;
            // El eje Y usa Abs(sin) para que el paso sea un rebote hacia
            // arriba y nunca hunda la camara por debajo de su altura.
            BobOffset = new Vector3(Mathf.Cos(bobPhase) * amp * 0.6f,
                                    Mathf.Abs(Mathf.Sin(bobPhase)) * amp, 0f);
        }

        // Se aplica DESPUES de posicionar la camara (por eso vive en el
        // final de LateUpdate) y decae solo. Recibe el frameOffset ya
        // capturado por LateUpdate, que lo limpia siempre.
        void ApplyCameraOffsets(Vector3 frame)
        {
            Vector3 total = shakeOffset + frame + BobOffset;
            total = Vector3.ClampMagnitude(total, maxShakeMagnitude);
            offsetAplicado = Vector3.zero;
            if (total.sqrMagnitude > 0.000001f) { transform.position += total; offsetAplicado = total; }
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, Mathf.Clamp01(Time.deltaTime * shakeRecoverySpeed));
        }

        // Ronda 12 ("el tanque tambalea la camara"): la sacudida/inercia/balanceo se SUMABA a transform.position y el frame
        // siguiente el seguimiento (Lerp desde la posicion actual) partia de esa posicion ya movida. Como el Lerp solo
        // recupera una fraccion por frame, un empujon continuo se acumulaba ~1/k veces (con k=0,17, casi 6 veces el
        // offset pedido) y la camara oscilaba de un lado a otro. Ahora se guarda lo aplicado y el seguimiento lo quita
        // antes de interpolar: el offset es un adorno de UN frame, no parte de la posicion base.
        Vector3 offsetAplicado;
        void QuitarOffsetPrevio()
        {
            if (offsetAplicado == Vector3.zero) return;
            transform.position -= offsetAplicado;
            offsetAplicado = Vector3.zero;
        }

        // Vista RTS guardada al salir, para no perder el encuadre que el
        // jugador armo (paneo + zoom) cada vez que vuelve. Sin esto, cada
        // regreso a RTS recentraba en el poseido, tirando cualquier
        // observacion de otra zona del mapa.
        Vector3? savedRtsFocus;
        float savedRtsHeight = -1f;

        public void SetMode(ControlMode mode, Vector3? rtsFallbackCenter = null)
        {
            adsBlend = 0f;   // al cambiar de vista se pierde el encuadre de mira: no se arrastra un apuntado colgado
            bool wasRts = Mode == ControlMode.Rts;
            bool goingToRts = mode == ControlMode.Rts;

            // Guardar la vista RTS justo antes de dejarla, no al entrar:
            // es la unica forma de capturar el ultimo estado real (paneo,
            // zoom) que el jugador dejo, en vez de un valor viejo.
            if (wasRts && !goingToRts)
            {
                savedRtsFocus = rtsFocusPoint;
                savedRtsHeight = rtsCurrentHeight;
            }

            Mode = mode;
            // Volver a FPS con el FOV que habia quedado a mitad de zoom de
            // RTS (o viceversa) se veria como un salto -- cada modo arranca
            // con su FOV de reposo, y el lerp de LateUpdate (solo en FPS)
            // se encarga de la mirilla desde ahi.
            if (cam != null) cam.fieldOfView = normalFov;

            // BUG REAL encontrado jugando: el [TAB] normal (el 99% de las
            // veces que se entra a RTS) llama ToggleMode()/SetMode(mode) SIN
            // fallback -- antes esta condicion exigia rtsFallbackCenter.HasValue
            // para hacer CUALQUIER cosa, asi que en ese camino la camara nunca
            // tocaba posicion NI rotacion: se quedaba con rtsFocusPoint=(0,0,0)
            // y rtsCurrentHeight=0 (los defaults de C#, nunca inicializados en
            // Awake/OnEnable), y LateUpdate la mandaba a esa altura 0 en el
            // origen del mundo mirando para donde la habia dejado FPS -- la
            // camara terminaba enterrada en el piso mirando al cielo (el
            // "tunel de estrellas" que se ve en RTS). Ahora SIEMPRE se llama
            // a RestoreOrSetRtsView, con el fallback explicito si lo hay o la
            // posicion actual de la camara si no -- y esa funcion ya sabe
            // preferir la vista RTS guardada de la sesion si existe.
            if (goingToRts && !wasRts)
                RestoreOrSetRtsView(rtsFallbackCenter ?? transform.position);
        }

        // Si hay una vista RTS guardada, la restaura en vez de recentrar
        // en `center`. Se usa en vez de llamar a SetRtsView directo desde
        // los puntos que alternan modo, para que "volver a RTS" y
        // "entrar a RTS por primera vez o tras la muerte" puedan pedir
        // explicitamente cual de las dos quieren.
        public void RestoreOrSetRtsView(Vector3 fallbackCenter)
        {
            CancelTransition();
            if (savedRtsFocus.HasValue && savedRtsHeight > 0f)
            {
                rtsFocusPoint = savedRtsFocus.Value;
                rtsCurrentHeight = savedRtsHeight;
                rtsTargetHeight = savedRtsHeight;
                transform.rotation = Quaternion.Euler(rtsLookEuler);
                transform.position = RtsCameraPositionFor(rtsFocusPoint, rtsCurrentHeight);
                // El objetivo de paneo suavizado debe re-sincronizarse con
                // el foco recien restaurado -- si no, el primer Pan()
                // arrancaria el lerp desde donde haya quedado el objetivo
                // de la sesion RTS anterior (o de FPS), un salto visible.
                panTargetInitialized = false;
            }
            else
            {
                SetRtsView(fallbackCenter);
            }
        }

        public void ToggleMode(Vector3? rtsFallbackCenter = null) => SetMode(Mode == ControlMode.Fps ? ControlMode.Rts : ControlMode.Fps, rtsFallbackCenter);

        // Mientras hay una transición en curso (BeginTransition), el resto
        // de los métodos Follow* no deben pisarla escribiendo la transform
        // de golpe cada frame; por eso todos arrancan chequeando esto.
        public bool IsTransitioning { get; private set; }
        Coroutine transitionRoutine;

        void CancelTransition()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
            IsTransitioning = false;
        }

        // Lerp corto de posición/rotación hacia un ancla (usado al poseer un
        // aliado con F o al subir a un vehículo con E), en vez del salto
        // instantáneo de antes.
        public void BeginTransition(Transform target, float duration = 0.35f)
        {
            if (target == null) return;
            CancelTransition();
            transitionRoutine = StartCoroutine(TransitionRoutine(target, duration));
        }

        IEnumerator TransitionRoutine(Transform target, float duration)
        {
            IsTransitioning = true;
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            float t = 0f;
            while (t < duration)
            {
                if (target == null)
                {
                    transform.position = fromPos;
                    transform.rotation = fromRot;
                    IsTransitioning = false;
                    transitionRoutine = null;
                    yield break;
                }
                t += Time.deltaTime;
                float k = t / duration;
                transform.position = Vector3.Lerp(fromPos, target.position, k);
                transform.rotation = Quaternion.Slerp(fromRot, target.rotation, k);
                yield return null;
            }
            if (target != null)
            {
                transform.position = target.position;
                transform.rotation = target.rotation;
            }
            IsTransitioning = false;
            transitionRoutine = null;
        }

        public void FollowFps(Soldier soldier)
        {
            if (soldier == null || IsTransitioning) return;
            Transform eye = soldier.EyeAnchor != null ? soldier.EyeAnchor : soldier.transform;
            transform.position = eye.position;
            transform.rotation = eye.rotation * Quaternion.Euler(-(pitch + recoilPitch), 0f, 0f);
        }

        // Pedido explicito: "que cambiar de camara (poseer otro soldado,
        // subir a un vehiculo/asiento) sea con un lerp de 1 o 2 segundos".
        // FollowThirdPerson/FollowOverShoulder ya convergen solas hacia el
        // objetivo cada frame (no son un salto instantaneo), pero antes lo
        // hacian siempre a la MISMA velocidad fija -- no habia forma de
        // pedir "esta vez que tarde mas". BeginFollowBlend marca una
        // ventana de tiempo real durante la cual esas dos usan una
        // velocidad de convergencia mas lenta (derivada de la duracion
        // pedida); pasada la ventana, vuelven solas a su seguimiento
        // ajustado de siempre. Sigue trackeando un objetivo que se mueve
        // durante la transicion (a diferencia de BeginTransition, que
        // lerpea hacia la posicion FIJA que un Transform tenia al arrancar).
        [SerializeField] float normalFollowSpeed = 10f;

        // BUG REAL corregido aca: la ventana de blend antes solo cambiaba
        // la VELOCIDAD de un lerp exponencial de toda la vida (nunca llega
        // del todo al objetivo, solo se acerca). Al cumplirse la duracion
        // pedida, la velocidad saltaba de golpe a normalFollowSpeed (mucho
        // mas rapida) para terminar de cerrar el resto que habia quedado
        // sin converger -- ese salto de velocidad, justo en el ultimo
        // tramo de la transicion, se leia como un tiron/glitch. Pedido
        // explicito: "en el ultimo segundo se ve raro, quiero que sea
        // fluido". Ahora la ventana interpola por FRACCION DE TIEMPO
        // (0 a 1, suavizada), asi que al llegar a `seconds` la camara ya
        // esta EXACTAMENTE en la pose deseada -- no queda ningun resto que
        // la velocidad normal tenga que compensar de un salto.
        float blendStartRealtime = -1f;
        float blendDurationActual = 1f;
        Vector3 blendStartPos;
        Quaternion blendStartRot;
        bool blendActive;

        public void BeginFollowBlend(float seconds)
        {
            blendDurationActual = Mathf.Max(0.05f, seconds);
            blendStartRealtime = Time.unscaledTime;
            blendStartPos = transform.position;
            blendStartRot = transform.rotation;
            blendActive = true;
        }

        static float SmoothStep01(float f) => f * f * (3f - 2f * f);

        // Fraccion 0..1 de la ventana de blend actual, ya suavizada. Marca
        // la ventana como terminada al llegar a 1 (una sola vez, el mismo
        // frame en que se aplica la pose final exacta) para que el
        // proximo Follow* vuelva solo a su convergencia de siempre sin
        // ningun resto pendiente.
        float BlendEased()
        {
            float f = Mathf.Clamp01((Time.unscaledTime - blendStartRealtime) / blendDurationActual);
            if (f >= 1f) blendActive = false;
            return SmoothStep01(f);
        }

        // Pedido explicito: "al comienzo que mire hacia donde esta el
        // soldado de manera disimulada hasta que encuadre bien". Un solo
        // salto de rotacion (la de siempre -> la del encuadre final) se
        // sentia brusco cuando el nuevo soldado no estaba ya casi de
        // frente a la camara vieja. Se parte en dos tramos: el primer 40%
        // de la ventana gira SUAVEMENTE desde la rotacion de arranque
        // hacia un simple mirar-al-soldado (orientacion, no encuadre); el
        // 60% restante converge desde ese mirar-al-soldado hacia la
        // rotacion final de encuadre (hombro/vehiculo). El punto de mira
        // se calcula una sola vez con la posicion de arranque, no cada
        // frame, para no perseguir un objetivo que ya se esta moviendo.
        const float LookAtPortion = 0.4f;

        Quaternion BlendLookThenFrame(Vector3 lookAtPoint, Quaternion finalFrameRot, float eased)
        {
            Quaternion lookAtObjetivo = Quaternion.LookRotation((lookAtPoint - blendStartPos).normalized, Vector3.up);
            float haciaMirada = SmoothStep01(Mathf.Clamp01(eased / LookAtPortion));
            float haciaEncuadre = SmoothStep01(Mathf.Clamp01((eased - LookAtPortion) / (1f - LookAtPortion)));
            Quaternion trasMirada = Quaternion.Slerp(blendStartRot, lookAtObjetivo, haciaMirada);
            return Quaternion.Slerp(trasMirada, finalFrameRot, haciaEncuadre);
        }

        // Pedido explicito: "quiero poder ver el soldado q manejo y sus
        // armas en su espalda" -- una camara en el ojo (FollowFps) nunca
        // puede mostrar ni el cuerpo ni la espalda de quien la lleva. Por
        // encima del hombro, a diferencia de FollowThirdPerson (pensado
        // para vehiculos, que no cabecean): acá el pitch del mouse SI
        // mueve la camara -- si no, apuntar arriba/abajo a pie dejaria de
        // funcionar apenas se dejo de mirar desde el ojo.
        // Altura 10% mas baja que antes (1.7 -> 1.53): pedido explicito
        // ("que la camara este un 10% abajo"), medido sobre esta vista
        // por encima del hombro, que es la que el jugador tiene puesta la
        // mayor parte del tiempo a pie.
        // heightOffset: cuanto restarle a la altura fija del pivote -- lo
        // usa el agachado (ver SoldierMotor.EyeHeightDrop) para que la
        // camara baje junto con el cuerpo en vez de quedarse flotando a la
        // altura de pie de siempre. 0 = sin cambios, comportamiento previo.
        //
        // Pedido explicito: "la camara estara con el soldado actual
        // encuadrado a la izquierda". Antes la camara vivia justo detras
        // del pivote mirando derecho adelante: el soldado quedaba
        // perfectamente centrado (tapando su propio punto de mira). El
        // desplazamiento lateral mueve la CAMARA hacia la derecha del
        // cuerpo sin reapuntarla hacia el (misma rotacion "look" de
        // siempre) -- eso es justo lo que hace que el soldado se vea
        // correrse al lado izquierdo de la pantalla, como en cualquier
        // shooter en tercera persona por encima del hombro.
        // Pedido de seguimiento: "mas a la derecha, como un Resident
        // Evil" -- 0.55 dejaba el cuerpo mas cerca del centro que el
        // encuadre clasico RE4/RE6 (personaje pegado al borde izquierdo).
        // Subido a 1.0 sobre la misma distancia (4 m) para un angulo
        // notoriamente mayor sin exagerar tanto que el punto de mira
        // quede desalineado del centro de pantalla.
        [SerializeField] float shoulderSideOffset = 1.0f;

        public void FollowOverShoulder(Transform target, float distance = 4f, float height = 1.53f, float heightOffset = 0f, Vector3? ojo = null)
        {
            if (target == null || IsTransitioning) return;
            Vector3 pivot = target.position + Vector3.up * (height - heightOffset);
            Quaternion look = target.rotation * Quaternion.Euler(-(pitch + recoilPitch), 0f, 0f);
            Vector3 desired = pivot - (look * Vector3.forward) * distance + (look * Vector3.right) * shoulderSideOffset;

            // Apuntando: la camara viaja al ojo (misma rotacion, sin lag) y desde ahi se ve
            // por la mira del arma, no por encima del hombro.
            if (ojo.HasValue && adsBlend > 0.001f && !blendActive)
            {
                float e = SmoothStep01(adsBlend);
                Vector3 ojoPos = ojo.Value + (look * Vector3.forward) * 0.12f;
                float kf = Mathf.Clamp01(Time.deltaTime * normalFollowSpeed);
                Vector3 basePos = Vector3.Lerp(transform.position, desired, kf);
                transform.position = Vector3.Lerp(basePos, ojoPos, e);
                transform.rotation = Quaternion.Slerp(Quaternion.Slerp(transform.rotation, look, kf), look, e);
                return;
            }

            if (blendActive)
            {
                float eased = BlendEased();
                transform.position = Vector3.Lerp(blendStartPos, desired, eased);
                transform.rotation = BlendLookThenFrame(pivot, look, eased);
                return;
            }

            float k = Mathf.Clamp01(Time.deltaTime * normalFollowSpeed);
            transform.position = Vector3.Lerp(transform.position, desired, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, k);
        }

        // Primera persona genérica: sirve para el ojo de un soldado o el
        // asiento de un vehículo, cualquier ancla con posición y rotación.
        public void FollowAnchor(Transform anchor)
        {
            if (anchor == null || IsTransitioning) return;
            transform.position = anchor.position;
            transform.rotation = anchor.rotation * Quaternion.Euler(-(pitch + recoilPitch), 0f, 0f);
        }

        // Tercera persona: orbita detrás y arriba del objetivo, mirándolo.
        // Antes esto pisaba transform.position/rotation de golpe cada
        // frame: entrar a un vehiculo (viniendo de la vista a pie) o
        // cambiar de asiento se sentia como un corte de camara, no una
        // transicion. Ahora converge con un Lerp/Slerp hacia la pose
        // deseada -- la primera vez que se llama (recien subido al
        // vehiculo) la camara arranca lejos de "desired" y se desliza
        // hasta ahi sola, sin que nadie tenga que orquestar un
        // BeginTransition aparte para este caso.
        // Ronda 12: la posicion ya no es un Lerp hacia un punto que se mueve con el vehiculo (a 12 m/s dejaba la camara
        // 1 m atras y la balanceaba en cada giro y cada frenada). Ahora la camara va RIGIDA a la distancia pedida sobre el
        // vehiculo, y lo unico que se suaviza es el YAW con el que lo mira (SmoothDampAngle): al girar el tanque la camara
        // lo sigue con un retraso corto y sin oscilar, y al chocar o deslizar contra un muro la camara no tiembla.
        Transform yawObjetivo;
        float yawSuave, yawVel;
        public const float TiempoDeYawDeVehiculo = 0.16f;

        public void FollowThirdPerson(Transform target, float distance = 7f, float height = 3f)
        {
            if (target == null || IsTransitioning) return;
            QuitarOffsetPrevio();
            float yawReal = target.eulerAngles.y;
            if (yawObjetivo != target) { yawObjetivo = target; yawSuave = yawReal; yawVel = 0f; }
            yawSuave = Mathf.SmoothDampAngle(yawSuave, yawReal, ref yawVel, TiempoDeYawDeVehiculo);
            Vector3 atras = Quaternion.Euler(0f, yawSuave, 0f) * Vector3.forward;
            Vector3 desired = target.position - atras * distance + Vector3.up * height;
            Quaternion desiredRot = Quaternion.LookRotation((target.position + Vector3.up * 1.2f - desired).normalized);

            if (blendActive)
            {
                float eased = BlendEased();
                transform.position = Vector3.Lerp(blendStartPos, desired, eased);
                transform.rotation = Quaternion.Slerp(blendStartRot, desiredRot, eased);
                return;
            }

            transform.position = desired;
            transform.rotation = desiredRot;
        }

        // Tercera persona QUE SIGUE LA PUNTERIA -- pedido explicito:
        // "cuando este usando la torreta la camara rote mirando hacia
        // donde apunto". FollowThirdPerson (arriba) orbita el FORWARD DEL
        // CASCO, que solo cambia si el conductor gira el vehiculo entero
        // -- apuntar con el cañon o la metralleta no lo movia un grado.
        // Esta en cambio orbita el forward del ARMA (el que el jugador
        // gira con el mouse), asi que la camara barre junto con la
        // punteria, como estar mirando por encima del hombro de quien
        // apunta.
        //
        // Solo yaw: se aplana aimForward a horizontal para que la camara
        // no cabecee con la elevacion del cañon (apuntar al cielo o al
        // piso giraria la camara entera, mareador). Velocidad propia
        // (mas rapida que CurrentFollowSpeed, pensada para transiciones
        // de 1-2s entre asientos) porque esto responde al mouse cuadro a
        // cuadro -- con la misma velocidad lenta de siempre la camara se
        // quedaria visiblemente atras de hacia donde ya esta apuntando.
        const float AimFollowSpeed = 6f;

        public void FollowThirdPersonAimed(Vector3 pivotPos, Vector3 aimForward, float distance = 7f, float height = 3f,
            Vector3? miraPos = null, Quaternion? miraRot = null)
        {
            if (IsTransitioning) return;
            QuitarOffsetPrevio();
            var flat = new Vector3(aimForward.x, 0f, aimForward.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 desired = pivotPos - flat * distance + Vector3.up * height;
            Quaternion desiredRot = Quaternion.LookRotation((pivotPos + Vector3.up * 1.2f - desired).normalized);
            float k = Mathf.Clamp01(Time.deltaTime * AimFollowSpeed);
            Vector3 basePos = Vector3.Lerp(transform.position, desired, k);
            Quaternion baseRot = Quaternion.Slerp(transform.rotation, desiredRot, k);

            // Mirando por la mira del canon / la metralleta: primera persona sobre el arma.
            if (miraPos.HasValue && miraRot.HasValue && adsBlend > 0.001f)
            {
                float e = SmoothStep01(adsBlend);
                transform.position = Vector3.Lerp(basePos, miraPos.Value, e);
                transform.rotation = Quaternion.Slerp(baseRot, miraRot.Value, e);
                return;
            }
            transform.position = basePos;
            transform.rotation = baseRot;
        }

        public void SetRtsView(Vector3 center)
        {
            CancelTransition();
            rtsFocusPoint = new Vector3(center.x, 0f, center.z);
            rtsCurrentHeight = rtsHeight;
            rtsTargetHeight = rtsHeight;
            transform.rotation = Quaternion.Euler(rtsLookEuler);
            transform.position = RtsCameraPositionFor(rtsFocusPoint, rtsCurrentHeight);
            panTargetInitialized = false;
        }

        public Ray GetForwardRay() => new Ray(transform.position, transform.forward);

        public Camera Cam => cam;

        // Antes Pan sumaba directo a la posicion sin ningun limite: se
        // podia alejar infinitamente del mapa y quedar mirando el vacio
        // sin saber como volver. Ahora acumula en un objetivo acotado a
        // los bordes del mapa, y LateUpdate interpola la posicion real
        // hacia ese objetivo -- el paneo deja de ser un salto duro
        // dependiente del framerate y pasa a converger suave.
        [SerializeField] float mapHalfExtent = 90f;
        [SerializeField] float panSmoothSpeed = 10f;
        Vector3 panTarget;
        bool panTargetInitialized;

        // BUG REAL: el limite era un cuadrado fijo de +-mapHalfExtent (90)
        // centrado en el ORIGEN, y el terreno de esta escena no es ni
        // cuadrado ni esta centrado ahi: mide 58 x 160 y su centro cae en
        // (4.4, 57.7). O sea que la camara podia irse 60 metros fuera del
        // piso por el oeste -- pantalla vacia, sin nada con que orientarse
        // para volver -- y en cambio se frenaba 47 metros ANTES del borde
        // norte, que si es terreno jugable.
        //
        // Ahora se acota al terreno de verdad, medido por NavService desde
        // los colliders de la escena. El campo serializado queda como
        // respaldo para una escena sin colliders.
        void AcotarAlMapa(ref Vector3 punto)
        {
            if (SP.Core.NavService.TryArea(out var limites))
            {
                punto.x = Mathf.Clamp(punto.x, limites.min.x, limites.max.x);
                punto.z = Mathf.Clamp(punto.z, limites.min.z, limites.max.z);
                return;
            }
            punto.x = Mathf.Clamp(punto.x, -mapHalfExtent, mapHalfExtent);
            punto.z = Mathf.Clamp(punto.z, -mapHalfExtent, mapHalfExtent);
        }

        public void Pan(Vector3 worldDelta)
        {
            if (!panTargetInitialized) { panTarget = rtsFocusPoint; panTargetInitialized = true; }
            panTarget += worldDelta;
            AcotarAlMapa(ref panTarget);
        }

        // Si la camara se pierde en RTS no habia forma rapida de volver a
        // la accion salvo panear a ciegas hasta encontrar a la tropa.
        // Manda el objetivo de paneo directo al punto pedido (acotado
        // igual que Pan) y deja que el mismo lerp de LateUpdate lo lleve
        // ahi con suavidad, en vez de teletransportar.
        public void RecenterOn(Vector3 point)
        {
            panTargetInitialized = true;
            var destino = new Vector3(point.x, 0f, point.z);
            AcotarAlMapa(ref destino);
            panTarget = destino;
        }

        // Zoom ya hacia clamp, pero no decia nada al llegar al limite: el
        // jugador seguia girando la rueda sin entender por que no pasaba
        // nada mas. Expone si el ultimo Zoom se topo con un extremo.
        public bool ZoomAtLimit { get; private set; }

        // En perspectiva no hay "orthographicSize" que acercar/alejar: el
        // equivalente es la ALTURA de la camara sobre su foco (mas cerca
        // del suelo = mas zoom). Mismo signo que antes (delta positivo
        // acerca) para que la rueda del mouse se siga sintiendo igual.
        public void Zoom(float delta)
        {
            float raw = rtsCurrentHeight - delta;
            float clamped = Mathf.Clamp(raw, rtsMinHeight, rtsMaxHeight);
            ZoomAtLimit = Mathf.Abs(raw - clamped) > 0.001f;
            rtsCurrentHeight = clamped;
            rtsTargetHeight = clamped;
            zoomAnclado = false;
        }

        // Punto del suelo (y = 0) que cae bajo un pixel de la pantalla.
        bool SueloBajoPixel(Vector2 pantalla, out Vector3 punto)
        {
            punto = default;
            if (cam == null) return false;
            var rayo = cam.ScreenPointToRay(pantalla);
            if (Mathf.Abs(rayo.direction.y) < 0.0001f) return false;
            float t = -rayo.origin.y / rayo.direction.y;
            if (t <= 0f) return false;
            punto = rayo.origin + rayo.direction * t;
            return true;
        }

        // Zoom HACIA EL CURSOR: el punto del mapa que esta bajo el mouse se queda bajo el mouse
        // mientras se acerca o se aleja (como Google Maps o cualquier RTS). La altura no salta:
        // se anima hacia el objetivo y, en cada frame, se mide el suelo bajo el pixel antes y
        // despues del cambio de altura y la diferencia se le suma al foco de la camara.
        public void ZoomHaciaCursor(float delta, Vector2 pantalla)
        {
            if (cam == null || Mode != ControlMode.Rts || IsTransitioning) { Zoom(delta); return; }
            if (rtsTargetHeight < 0f) rtsTargetHeight = rtsCurrentHeight;
            float raw = rtsTargetHeight - delta;
            float clamped = Mathf.Clamp(raw, rtsMinHeight, rtsMaxHeight);
            ZoomAtLimit = Mathf.Abs(raw - clamped) > 0.001f;
            rtsTargetHeight = clamped;
            zoomPixel = pantalla;
            zoomAnclado = true;
        }

        // Alturas a las que converge el zoom (para las pruebas).
        public float RtsTargetHeight => rtsTargetHeight < 0f ? rtsCurrentHeight : rtsTargetHeight;
        public const float VelocidadDeZoomAnimado = 14f;

        void AnimarZoom(float dt)
        {
            if (rtsTargetHeight < 0f || Mathf.Abs(rtsCurrentHeight - rtsTargetHeight) < 0.002f)
            {
                if (rtsTargetHeight >= 0f) rtsCurrentHeight = rtsTargetHeight;
                return;
            }
            Vector3 antes = default;
            bool habia = zoomAnclado && SueloBajoPixel(zoomPixel, out antes);
            rtsCurrentHeight = rtsTargetHeight;   // Ronda 11: zoom sin lerp (pedido); VelocidadDeZoomAnimado queda por compatibilidad
            transform.position = RtsCameraPositionFor(rtsFocusPoint, rtsCurrentHeight);
            if (!habia || !SueloBajoPixel(zoomPixel, out var despues)) return;

            var corrimiento = new Vector3(antes.x - despues.x, 0f, antes.z - despues.z);
            if (!panTargetInitialized) { panTarget = rtsFocusPoint; panTargetInitialized = true; }
            var foco = rtsFocusPoint + corrimiento;
            var objetivo = panTarget + corrimiento;
            AcotarAlMapa(ref foco);
            AcotarAlMapa(ref objetivo);
            rtsFocusPoint = new Vector3(foco.x, rtsFocusPoint.y, foco.z);
            panTarget = objetivo;
            transform.position = RtsCameraPositionFor(rtsFocusPoint, rtsCurrentHeight);
        }

        // Alturas de la vista RTS (para las pruebas y para mostrar el zoom).
        public float RtsHeight => rtsCurrentHeight;
        public Vector3 RtsFocus => rtsFocusPoint;
    }
}
