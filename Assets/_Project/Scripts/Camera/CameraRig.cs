using System.Collections;
using UnityEngine;
using SP.Actors;

namespace SP.CameraSystem
{
    public enum ControlMode { Fps, Rts }

    // Posee la cámara y delega su posición en el modo activo (FPS u RTS).
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float rtsHeight = 30f;
        [SerializeField] float rtsOrthoSize = 20f;
        // Pura vista de pájaro: 90° en X (mirando derecho hacia abajo), sin
        // inclinación en Y/Z. Antes eran 55° (una vista en ángulo, no un
        // top-down real).
        [SerializeField] Vector3 rtsLookEuler = new Vector3(90f, 0f, 0f);

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
            if (cam != null && !cam.orthographic) normalFov = cam.fieldOfView;
        }

        public void SetZoomed(bool value) => zoomed = value;

        void LateUpdate()
        {
            if (cam == null) return;

            recoilPitch = Mathf.MoveTowards(recoilPitch, 0f, Time.deltaTime * recoilRecoverySpeed);

            if (!cam.orthographic)
            {
                float goal = zoomed ? zoomFov : normalFov;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, goal, Time.deltaTime * zoomLerpSpeed);
                ApplyDirectionalShake();
                return;
            }

            // En RTS: converge hacia el objetivo de paneo (ya acotado a
            // los bordes del mapa en Pan()). No corre durante una
            // transicion de camara (BeginTransition), que ya tiene su
            // propio control total de la posicion.
            if (panTargetInitialized && !IsTransitioning)
            {
                var target = new Vector3(panTarget.x, transform.position.y, panTarget.z);
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * panSmoothSpeed);
            }

            ApplyDirectionalShake();
        }

        public void AddPitch(float delta) => pitch = Mathf.Clamp(pitch + delta, -MaxPitch, MaxPitch);
        public void ResetPitch() => pitch = 0f;

        // Culatazo de camara: un canal SEPARADO del pitch que controla el
        // mouse, para que decaiga solo sin que el jugador tenga que
        // compensarlo bajando el mouse el mismo tanto que subio (eso
        // seria acumular error de puntería en cada disparo). Sube de
        // golpe con cada tiro y se recupera con un MoveTowards.
        float recoilPitch;
        [SerializeField] float recoilRecoverySpeed = 40f; // grados/seg
        public void KickRecoil(float degrees) => recoilPitch += degrees;
        public float RecoilPitch => recoilPitch;

        // Sacudida DIRECCIONAL: una vibracion aleatoria no comunica de
        // donde vino la fuerza. El culatazo del cañon tiene que empujar la
        // camara en un sentido concreto -- el opuesto al eje de disparo --
        // para que se lea como retroceso y no como ruido.
        Vector3 shakeOffset;
        [SerializeField] float shakeRecoverySpeed = 9f;
        public Vector3 ShakeOffset => shakeOffset;

        public void KickDirectional(Vector3 worldDirection, float magnitude)
        {
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            shakeOffset += worldDirection.normalized * magnitude;
        }

        // Se aplica DESPUES de posicionar la camara (por eso vive en el
        // final de LateUpdate) y decae solo.
        void ApplyDirectionalShake()
        {
            if (shakeOffset.sqrMagnitude < 0.000001f) return;
            transform.position += shakeOffset;
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, Mathf.Clamp01(Time.deltaTime * shakeRecoverySpeed));
        }

        // Vista RTS guardada al salir, para no perder el encuadre que el
        // jugador armo (paneo + zoom) cada vez que vuelve. Sin esto, cada
        // regreso a RTS recentraba en el poseido, tirando cualquier
        // observacion de otra zona del mapa.
        Vector3? savedRtsPosition;
        float savedRtsOrthoSize = -1f;

        public void SetMode(ControlMode mode)
        {
            bool wasRts = Mode == ControlMode.Rts;
            bool goingToRts = mode == ControlMode.Rts;

            // Guardar la vista RTS justo antes de dejarla, no al entrar:
            // es la unica forma de capturar el ultimo estado real (paneo,
            // zoom) que el jugador dejo, en vez de un valor viejo.
            if (wasRts && !goingToRts && cam != null)
            {
                savedRtsPosition = transform.position;
                savedRtsOrthoSize = cam.orthographicSize;
            }

            Mode = mode;
            if (cam != null) cam.orthographic = mode == ControlMode.Rts;
        }

        // Si hay una vista RTS guardada, la restaura en vez de recentrar
        // en `center`. Se usa en vez de llamar a SetRtsView directo desde
        // los puntos que alternan modo, para que "volver a RTS" y
        // "entrar a RTS por primera vez o tras la muerte" puedan pedir
        // explicitamente cual de las dos quieren.
        public void RestoreOrSetRtsView(Vector3 fallbackCenter)
        {
            if (savedRtsPosition.HasValue && savedRtsOrthoSize > 0f)
            {
                transform.position = savedRtsPosition.Value;
                transform.rotation = Quaternion.Euler(rtsLookEuler);
                if (cam != null) cam.orthographicSize = savedRtsOrthoSize;
                // El objetivo de paneo suavizado debe re-sincronizarse con
                // la posicion recien restaurada -- si no, el primer Pan()
                // arrancaria el lerp desde donde haya quedado el objetivo
                // de la sesion RTS anterior (o de FPS), un salto visible.
                panTargetInitialized = false;
            }
            else
            {
                SetRtsView(fallbackCenter);
            }
        }

        public void ToggleMode() => SetMode(Mode == ControlMode.Fps ? ControlMode.Rts : ControlMode.Fps);

        // Mientras hay una transición en curso (BeginTransition), el resto
        // de los métodos Follow* no deben pisarla escribiendo la transform
        // de golpe cada frame; por eso todos arrancan chequeando esto.
        public bool IsTransitioning { get; private set; }
        Coroutine transitionRoutine;

        // Lerp corto de posición/rotación hacia un ancla (usado al poseer un
        // aliado con F o al subir a un vehículo con E), en vez del salto
        // instantáneo de antes.
        public void BeginTransition(Transform target, float duration = 0.35f)
        {
            if (target == null) return;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
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
                t += Time.deltaTime;
                float k = t / duration;
                transform.position = Vector3.Lerp(fromPos, target.position, k);
                transform.rotation = Quaternion.Slerp(fromRot, target.rotation, k);
                yield return null;
            }
            transform.position = target.position;
            transform.rotation = target.rotation;
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

        // Primera persona genérica: sirve para el ojo de un soldado o el
        // asiento de un vehículo, cualquier ancla con posición y rotación.
        public void FollowAnchor(Transform anchor)
        {
            if (anchor == null || IsTransitioning) return;
            transform.position = anchor.position;
            transform.rotation = anchor.rotation * Quaternion.Euler(-pitch, 0f, 0f);
        }

        // Tercera persona: orbita detrás y arriba del objetivo, mirándolo.
        public void FollowThirdPerson(Transform target, float distance = 7f, float height = 3f)
        {
            if (target == null || IsTransitioning) return;
            Vector3 desired = target.position - target.forward * distance + Vector3.up * height;
            transform.position = desired;
            transform.rotation = Quaternion.LookRotation((target.position + Vector3.up * 1.2f - transform.position).normalized);
        }

        public void SetRtsView(Vector3 center)
        {
            if (cam != null) cam.orthographicSize = rtsOrthoSize;
            transform.position = center + Vector3.up * rtsHeight;
            transform.rotation = Quaternion.Euler(rtsLookEuler);
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

        public void Pan(Vector3 worldDelta)
        {
            if (!panTargetInitialized) { panTarget = transform.position; panTargetInitialized = true; }
            panTarget += worldDelta;
            panTarget.x = Mathf.Clamp(panTarget.x, -mapHalfExtent, mapHalfExtent);
            panTarget.z = Mathf.Clamp(panTarget.z, -mapHalfExtent, mapHalfExtent);
        }

        // Si la camara se pierde en RTS no habia forma rapida de volver a
        // la accion salvo panear a ciegas hasta encontrar a la tropa.
        // Manda el objetivo de paneo directo al punto pedido (acotado
        // igual que Pan) y deja que el mismo lerp de LateUpdate lo lleve
        // ahi con suavidad, en vez de teletransportar.
        public void RecenterOn(Vector3 point)
        {
            panTargetInitialized = true;
            panTarget = new Vector3(
                Mathf.Clamp(point.x, -mapHalfExtent, mapHalfExtent),
                transform.position.y,
                Mathf.Clamp(point.z, -mapHalfExtent, mapHalfExtent));
        }

        // Zoom ya hacia clamp, pero no decia nada al llegar al limite: el
        // jugador seguia girando la rueda sin entender por que no pasaba
        // nada mas. Expone si el ultimo Zoom se topo con un extremo.
        public bool ZoomAtLimit { get; private set; }

        public void Zoom(float delta)
        {
            if (cam == null) return;
            // OJO: comparar "antes vs despues del clamp" esta mal si ya
            // se estaba justo en el limite (antes==despues sin que este
            // Zoom haya pedido nada extra). Lo que importa es si el
            // valor SIN acotar se hubiera ido afuera del rango.
            float raw = cam.orthographicSize - delta;
            float clamped = Mathf.Clamp(raw, 6f, 60f);
            ZoomAtLimit = Mathf.Abs(raw - clamped) > 0.001f;
            cam.orthographicSize = clamped;
        }
    }
}
