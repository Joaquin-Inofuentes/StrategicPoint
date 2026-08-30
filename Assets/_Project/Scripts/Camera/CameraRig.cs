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

        public ControlMode Mode { get; private set; } = ControlMode.Fps;

        public void SetCamera(Camera c) => cam = c;

        public void AddPitch(float delta) => pitch = Mathf.Clamp(pitch + delta, -MaxPitch, MaxPitch);
        public void ResetPitch() => pitch = 0f;

        public void SetMode(ControlMode mode)
        {
            Mode = mode;
            if (cam != null) cam.orthographic = mode == ControlMode.Rts;
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
            transform.rotation = eye.rotation * Quaternion.Euler(-pitch, 0f, 0f);
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
        }

        public Ray GetForwardRay() => new Ray(transform.position, transform.forward);

        public Camera Cam => cam;

        public void Pan(Vector3 worldDelta) => transform.position += worldDelta;

        public void Zoom(float delta)
        {
            if (cam == null) return;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - delta, 6f, 60f);
        }
    }
}
