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
        // G2, version cubo: cuanto quedaba de la altura de pie al agacharse.
        // ORIGINALMENTE esto encogia transform.localScale.y entero -- valia
        // porque el soldado ERA un cubo (mesh + collider + EyeAnchor, todo
        // bajo el mismo transform, sin animacion). Con el rig humanoide de
        // ArtBuilder eso ya NO es seguro: "Visual" cuelga del mismo
        // transform y hereda esa escala, y Unity no soporta un rig
        // humanoide bajo una cadena de escala no uniforme (el mismo bug que
        // ArtBuilder.NormalizarEscala documenta para el import -- ahi el
        // esqueleto explotaba, cabeza a metros de los pies). Escalar en
        // vivo cada vez que alguien se agacha habria reproducido ese
        // desastre en pleno Play mode. La pose de agachado ahora la da el
        // Animator (parametro "Agachado", ver SoldierAnimatorDriver); esto
        // solo baja la camara en primera persona.
        [SerializeField] float fraccionAlturaAgachado = 0.6f;

        Collider body;
        float bodyRadius = 0.4f;
        bool bodyResolved;

        // Salto (G3). Sin Rigidbody en este motor (ver el comentario de
        // arriba: mover a mano, no fisica de verdad), asi que la parabola
        // se integra a mano, igual de simple que cualquier otro
        // movimiento de este motor.
        // jumpSpeed/gravity elegidos para un salto corto y rapido (~0.45 m
        // de alto, ~0.4 s en el aire) -- esto es un shooter tactico, no un
        // plataformero, asi que un salto alto y lento (lo que da la
        // combinacion original, ~0.84 m) se sentia flotante y lento de
        // recuperar para volver a disparar.
        [SerializeField] float jumpSpeed = 4.5f;
        [SerializeField] float gravity = 22f;
        float verticalVelocity;
        float groundY;
        public bool IsJumping { get; private set; }

        // Se guarda la altura de PISO al saltar (no 0 fijo): el terreno de
        // la escena no es perfectamente plano en todos lados (ver
        // ApoyoEnElPiso), asi que aterrizar tiene que volver a donde el
        // soldado estaba parado, no a un y=0 que podria quedar hundido o
        // flotando segun el lugar.
        public void Jump()
        {
            if (IsJumping || IsCrouching) return;
            IsJumping = true;
            groundY = transform.position.y;
            verticalVelocity = jumpSpeed;
        }

        void Update()
        {
            if (!IsJumping) return;
            verticalVelocity -= gravity * Time.deltaTime;
            var pos = transform.position;
            pos.y += verticalVelocity * Time.deltaTime;
            if (pos.y <= groundY && verticalVelocity <= 0f)
            {
                pos.y = groundY;
                IsJumping = false;
                verticalVelocity = 0f;
            }
            transform.position = pos;
        }

        Soldier soldierCacheado;
        float alturaOjoDePie;
        bool ojoCacheado;

        public bool IsCrouching { get; private set; }

        // BUG REAL: la camara por-encima-del-hombro (CameraRig.
        // FollowOverShoulder, la que de verdad usa el jugador a pie) pivotea
        // sobre "target.position + altura FIJA" -- nunca lee EyeAnchor. Al
        // agacharse, el Animator SI baja la pose del cuerpo pero la camara
        // se quedaba clavada a la misma altura de siempre: cuanto mas
        // agachado, mas "flotaba" la camara por encima de la cabeza del
        // soldado. Este offset es cuanto bajo el ojo ahora mismo respecto de
        // pie (0 si no esta agachado); FollowOverShoulder lo resta de su
        // altura fija para que la vista baje junto con el cuerpo.
        public float EyeHeightDrop => IsCrouching ? alturaOjoDePie * (1f - fraccionAlturaAgachado) : 0f;

        void EnsureBody()
        {
            if (bodyResolved) return;
            bodyResolved = true;

            body = GetComponent<Collider>();
            bodyRadius = Deslizador.RadioDe(body, transform, 0.4f);
        }

        // Se resuelve recien al primer agachado (no en Awake): ArtBuilder
        // agrega/reubica EyeAnchor sobre el prefab en tiempo de edicion, y
        // cachear antes de tiempo se arriesgaria a guardar una referencia a
        // un GameObject que ArtBuilder todavia va a mover.
        void EnsureOjo()
        {
            if (ojoCacheado) return;
            ojoCacheado = true;
            soldierCacheado = GetComponent<Soldier>();
            if (soldierCacheado != null && soldierCacheado.EyeAnchor != null)
                alturaOjoDePie = soldierCacheado.EyeAnchor.localPosition.y;
        }

        // Ctrl (mantenido) agacha en FPS. Ya no toca transform.localScale
        // (ver el comentario de arriba): solo baja el ancla de la camara en
        // primera persona la misma fraccion que antes, y deja la pose
        // visible (piernas, silueta) enteramente al Animator.
        public void SetCrouching(bool agachado)
        {
            // No se puede agachar en el aire: Jump() ya rechaza saltar
            // estando agachado, y esto cierra el otro sentido -- si no, el
            // jugador podia mantener Ctrl a mitad de salto y el Animator
            // intentaria mezclar la pose de agachado con la de salto.
            if (agachado && IsJumping) return;
            EnsureOjo();
            if (IsCrouching == agachado) return;
            IsCrouching = agachado;

            if (soldierCacheado == null || soldierCacheado.EyeAnchor == null) return;
            float y = alturaOjoDePie * (agachado ? fraccionAlturaAgachado : 1f);
            var p = soldierCacheado.EyeAnchor.localPosition;
            soldierCacheado.EyeAnchor.localPosition = new Vector3(p.x, y, p.z);
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
