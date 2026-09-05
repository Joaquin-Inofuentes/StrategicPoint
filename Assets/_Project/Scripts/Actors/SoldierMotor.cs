using UnityEngine;
using SP.Core;

namespace SP.Actors
{
    // Mueve el transform. Lo usan por igual el jugador y la IA detrás de la
    // misma llamada, sin que a ninguno le importe quién conduce.
    //
    // BUG DE FONDO CORREGIDO ACA: esto hacia literalmente
    // "transform.position += dir * speed * dt" y nada mas. El cubo "Muro"
    // de la escena tiene su BoxCollider bien puesto (activo, no trigger,
    // capa Default) y aun asi TODO lo atravesaba: mover un transform a
    // mano no consulta fisica -- la fisica reacciona a un Rigidbody, y aca
    // no hay ninguno. El collider del Muro servia para los raycasts (mira,
    // proyectiles) y para nada mas. La solucion no es agregar Rigidbodies
    // (eso cambiaria el modelo de movimiento de todo el juego) sino
    // resolver la colision en el UNICO cuello de botella por donde ya
    // pasaban el jugador y la IA: este metodo.
    public class SoldierMotor : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float turnSpeedDegPerSec = 220f;

        Collider body;
        float bodyRadius = 0.4f;
        bool bodyResolved;

        void EnsureBody()
        {
            if (bodyResolved) return;
            bodyResolved = true;

            body = GetComponent<Collider>();
            bodyRadius = Deslizador.RadioDe(body, transform, 0.4f);
        }

        public void Move(Vector3 worldDirection, float dt)
        {
            if (worldDirection.sqrMagnitude > 1f) worldDirection.Normalize();
            transform.position += Resolve(worldDirection * moveSpeed * dt);
        }

        public void RotateYaw(float yawDeltaDegrees)
        {
            transform.Rotate(Vector3.up, yawDeltaDegrees, Space.World);
        }

        public void LookTowards(Vector3 worldPoint, float dt)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDegPerSec * dt);
        }

        // Avanza hacia el punto y gira para mirarlo. Devuelve true al llegar.
        public bool MoveTowards(Vector3 worldPoint, float arriveThreshold, float dt)
        {
            Vector3 delta = worldPoint - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist <= arriveThreshold) return true;

            LookTowards(worldPoint, dt);
            Move(delta.normalized, dt);
            return false;
        }

        // ------------------------------------------------------------------
        // Colision
        // ------------------------------------------------------------------
        // La resolucion vive en SP.Core.Deslizador: la necesitan este motor
        // Y el del vehiculo, y dos copias se habrian ido separando.
        Vector3 Resolve(Vector3 delta)
        {
            EnsureBody();
            return Deslizador.Resolver(transform, body, delta, bodyRadius);
        }
    }
}
