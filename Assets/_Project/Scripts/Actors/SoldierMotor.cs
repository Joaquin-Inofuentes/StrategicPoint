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

        // Margen que se deja SIEMPRE entre el cuerpo y lo que choca. Sin
        // el, el soldado queda apoyado a distancia exactamente 0 de la
        // pared y el barrido del frame siguiente devuelve un impacto a
        // distancia 0: se traba en vez de deslizar.
        const float SkinWidth = 0.03f;

        // Cuantas veces se reproyecta el movimiento restante al chocar.
        // 1 alcanza para una pared, 2 para una esquina; la tercera es el
        // caso raro de quedar encajado entre tres caras. Mas que eso es
        // gastar barridos para no moverse igual.
        const int MaxSlides = 3;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        static readonly Collider[] OverlapBuffer = new Collider[16];

        Collider body;
        float bodyRadius = 0.4f;
        bool bodyResolved;

        void EnsureBody()
        {
            if (bodyResolved) return;
            bodyResolved = true;

            body = GetComponent<Collider>();
            // El radio se saca de la escala, no de bounds: bounds es una
            // caja alineada al mundo y CRECE al girar el cuerpo, asi que
            // un soldado a 45 grados se creeria 40% mas gordo y frenaria
            // antes de tocar nada.
            if (body is BoxCollider box)
            {
                var s = transform.lossyScale;
                float side = Mathf.Min(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.z * s.z));
                bodyRadius = Mathf.Max(0.05f, side * 0.5f - SkinWidth);
            }
            else if (body != null)
            {
                var e = body.bounds.extents;
                bodyRadius = Mathf.Max(0.05f, Mathf.Min(e.x, e.z) - SkinWidth);
            }
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
        // Colision: barrer y deslizar
        // ------------------------------------------------------------------
        // Toma el desplazamiento pedido y devuelve el que de verdad se
        // puede hacer. La componente vertical pasa intacta: este es un
        // juego sobre un piso unico y no hay salto ni gravedad que
        // resolver; meter la Y aca solo agregaria casos sin dueño.
        Vector3 Resolve(Vector3 delta)
        {
            EnsureBody();

            float vertical = delta.y;
            Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);
            float remaining = horizontal.magnitude;
            if (remaining < 0.00001f) return delta;

            Vector3 dir = horizontal / remaining;
            Vector3 start = transform.position;

            // Primero salir de adentro de algo, si ya estaba metido: un
            // cuerpo superpuesto devuelve impactos a distancia 0 con una
            // normal que no sirve, y sin esto quedaria clavado para
            // siempre justo donde mas se nota (dentro del Muro).
            Vector3 pos = start + Depenetrate(start);

            for (int i = 0; i < MaxSlides && remaining > 0.00001f; i++)
            {
                if (!TryHit(pos, dir, remaining + SkinWidth, out float hitDistance, out Vector3 normal))
                {
                    pos += dir * remaining;
                    remaining = 0f;
                    break;
                }

                float advance = Mathf.Max(0f, hitDistance - SkinWidth);
                pos += dir * advance;
                remaining -= advance;

                Vector3 n = new Vector3(normal.x, 0f, normal.z);
                if (n.sqrMagnitude < 0.000001f) break; // cara horizontal: no hay por donde deslizar

                n.Normalize();

                // Se le saca al movimiento restante la componente que
                // entra en la pared. Lo que queda es lo que corre PARALELO
                // a ella: por eso el soldado se desliza en vez de frenar
                // en seco contra el Muro.
                Vector3 slide = Vector3.ProjectOnPlane(dir * remaining, n);
                slide.y = 0f;
                remaining = slide.magnitude;
                if (remaining < 0.00001f) break;
                dir = slide / remaining;
            }

            Vector3 result = pos - start;
            result.y = vertical;
            return result;
        }

        bool TryHit(Vector3 pos, Vector3 dir, float distance, out float hitDistance, out Vector3 normal)
        {
            hitDistance = 0f;
            normal = Vector3.zero;

            int n = Physics.SphereCastNonAlloc(pos, bodyRadius, dir, HitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < n; i++)
            {
                var h = HitBuffer[i];
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(transform)) continue;
                // distance 0 significa que la esfera ya arrancaba
                // superpuesta: la normal que devuelve Unity ahi no es la
                // de la cara. Eso lo arregla Depenetrate, no este barrido.
                if (h.distance <= 0f) continue;
                if (h.distance >= best) continue;
                if (!NavService.BlocksMovement(h.collider)) continue;

                best = h.distance;
                normal = h.normal;
                found = true;
            }

            hitDistance = best;
            return found;
        }

        Vector3 Depenetrate(Vector3 pos)
        {
            if (body == null) return Vector3.zero;

            int n = Physics.OverlapSphereNonAlloc(pos, bodyRadius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;

            for (int i = 0; i < n; i++)
            {
                var other = OverlapBuffer[i];
                if (other == null || other == body) continue;
                if (other.transform.IsChildOf(transform)) continue;
                if (!NavService.BlocksMovement(other)) continue;

                if (!Physics.ComputePenetration(body, pos, transform.rotation,
                                                other, other.transform.position, other.transform.rotation,
                                                out Vector3 pushDir, out float pushDist))
                    continue;

                pushDir.y = 0f;
                if (pushDir.sqrMagnitude < 0.000001f) continue;
                push += pushDir.normalized * (pushDist + SkinWidth);
            }

            return push;
        }
    }
}
