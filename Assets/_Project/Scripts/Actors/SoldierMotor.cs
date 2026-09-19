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
        float saltoPedidoHasta;

        // Lo lee AiBrain para acotar el paso cuando sigue la ruta del
        // NavMeshAgent (que solo planifica; este motor es quien camina).
        public float MoveSpeed => moveSpeed * (Corriendo ? FactorDeCarrera : 1f);

        // [Shift]: correr. El jugador lo pide a pie y los aliados libres corren con el
        // (AjustesDeEscuadra.Correr). No se corre agachado ni en el aire.
        public const float FactorDeCarrera = 1.7f;
        bool pideCorrer;
        public bool Corriendo => pideCorrer && !IsCrouching && !IsJumping;
        public void SetRunning(bool correr) => pideCorrer = correr;

        // Se guarda la altura de PISO al saltar (no 0 fijo): el terreno de
        // la escena no es perfectamente plano en todos lados (ver
        // ApoyoEnElPiso), asi que aterrizar tiene que volver a donde el
        // soldado estaba parado, no a un y=0 que podria quedar hundido o
        // flotando segun el lugar.
        public void Jump()
        {
            if (Vaulting) return;
            if (!IsJumping && TryVault()) return;   // item 53: contra un obstaculo bajo, saltar lo trepa
            if (IsJumping) { saltoPedidoHasta = Time.time + 0.12f; return; }   // buffer de salto
            if (IsCrouching) SetCrouching(false);   // item 52: saltar agachado primero te levanta, en vez de ignorar la tecla
            IsJumping = true;
            groundY = transform.position.y;
            // Cuanto sobra el pivote sobre el piso (0,8 en los soldados), para volver a apoyar los pies
            // en el terreno de DEBAJO, no en la altura a la que se despego.
            alturaDePivote = BuscarPiso(transform.position, out float piso) ? transform.position.y - piso : -1f;
            verticalVelocity = jumpSpeed;
        }

        // ---- Trepar obstaculos bajos (item 53): cajones, muretes, cobertura baja de 0,5 a 1,3 m de alto.
        public const float AlturaMinimaTrepable = 0.5f, AlturaMaximaTrepable = 1.3f, AlcanceDeTrepa = 1.1f, SegundosDeTrepa = 0.5f;
        public bool Vaulting { get; private set; }
        public string UltimoMotivoDeTrepa { get; private set; } = "";
        Vector3 vaultDesde, vaultHasta;
        float vaultT;

        // Mide el obstaculo de enfrente (rodilla libre = nada que trepar, cabeza libre = hay espacio arriba) y, si su
        // borde cae entre las alturas trepables, arranca el movimiento. Devuelve si trepo.
        public bool TryVault()
        {
            var adelante = transform.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.01f) { UltimoMotivoDeTrepa = "1"; return false; }
            adelante.Normalize();
            // Sin piso detectable se asume el pivote a 0,8 m de los pies (como todos los soldados).
            float pies = BuscarPiso(transform.position, out float piso) ? piso : transform.position.y - 0.8f;
            var origenRodilla = new Vector3(transform.position.x, pies + 0.3f, transform.position.z);
            if (!SondearObstaculo(origenRodilla, adelante, AlcanceDeTrepa, out var golpe)) { UltimoMotivoDeTrepa = "3"; return false; }
            // Borde superior: rayo hacia abajo un poco mas alla de la pared, desde 1,6 m.
            var sobre = golpe.point + adelante * 0.45f; sobre.y = pies + 1.6f;
            if (!Physics.Raycast(sobre, Vector3.down, out var arriba, 1.7f, ~0, QueryTriggerInteraction.Ignore)) { UltimoMotivoDeTrepa = "4"; return false; }
            if (arriba.collider.GetComponentInParent<Soldier>() != null) { UltimoMotivoDeTrepa = "5"; return false; }
            float alto = arriba.point.y - pies;
            if (alto < AlturaMinimaTrepable || alto > AlturaMaximaTrepable) { UltimoMotivoDeTrepa = "6"; return false; }
            // Cabeza libre: nada a 1,5 m de altura entre el soldado y el borde.
            var cabeza = new Vector3(transform.position.x, pies + 1.5f, transform.position.z);
            if (SondearObstaculo(cabeza, adelante, AlcanceDeTrepa + 0.5f, out _)) { UltimoMotivoDeTrepa = "7"; return false; }
            Vaulting = true; vaultT = 0f;
            vaultDesde = transform.position;
            vaultHasta = new Vector3(sobre.x, arriba.point.y + (transform.position.y - pies), sobre.z);
            return true;
        }

        bool SondearObstaculo(Vector3 origen, Vector3 dir, float largo, out RaycastHit mejor)
        {
            mejor = default;
            int n = Physics.RaycastNonAlloc(origen, dir, SondeoPiso, largo, ~0, QueryTriggerInteraction.Ignore);
            float d = float.MaxValue; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var c = SondeoPiso[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<Soldier>() != null) continue;
                if (SondeoPiso[i].distance < d) { d = SondeoPiso[i].distance; mejor = SondeoPiso[i]; hay = true; }
            }
            return hay;
        }

        void TickTrepa()
        {
            vaultT += Time.deltaTime / SegundosDeTrepa;
            float t = Mathf.Clamp01(vaultT);
            var p = Vector3.Lerp(vaultDesde, vaultHasta, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 0.35f;
            transform.position = p;
            if (t >= 1f) Vaulting = false;
        }

        float alturaDePivote = -1f;
        static readonly RaycastHit[] SondeoPiso = new RaycastHit[12];

        // Piso justo debajo de p: el punto mas alto que no sea el propio cuerpo ni un trigger. Un cuerpo
        // agachado o apilado sobre otro no cuenta como piso.
        bool BuscarPiso(Vector3 p, out float piso)
        {
            piso = 0f;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.3f, Vector3.down, SondeoPiso, 6f, ~0, QueryTriggerInteraction.Ignore);
            bool hay = false;
            float mejor = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = SondeoPiso[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<Soldier>() != null) continue;
                if (SondeoPiso[i].point.y > mejor) { mejor = SondeoPiso[i].point.y; hay = true; }
            }
            piso = mejor;
            return hay;
        }

        // BUG REAL: a diferencia de IsCrouching (que todo llamador pone en
        // false explicitamente al morir/cambiar de estado/perder la
        // posesion), nada reseteaba IsJumping/verticalVelocity al revivir
        // a un soldado caido. Update() sigue integrando la parabola de
        // salto aunque Health.Current este en 0 (este motor no mira
        // Health), asi que si alguien moria EN el aire y lo revivian en el
        // mismo lugar (RescateAutomatico, el rescate manual), el salto
        // seguia su curso usando groundY de la vida ANTERIOR -- el
        // soldado podia aparecer cayendo o flotando en vez de parado.
        public void ResetMotionState()
        {
            IsJumping = false;
            Vaulting = false;
            verticalVelocity = 0f;
            IsCrouching = false;
            pideCorrer = false;
        }

        void Update()
        {
            if (Vaulting) { TickTrepa(); return; }
            if (!IsJumping) return;
            verticalVelocity -= gravity * Time.deltaTime;
            var pos = transform.position;
            pos.y += verticalVelocity * Time.deltaTime;
            // El suelo puede subir o bajar mientras se esta en el aire (cuesta, escalon, cajon): se aterriza
            // sobre el de ahora, no sobre el del despegue.
            float suelo = groundY;
            if (alturaDePivote >= 0f && verticalVelocity <= 0f && BuscarPiso(pos, out float pisoActual))
                suelo = pisoActual + alturaDePivote;
            if (pos.y <= suelo && verticalVelocity <= 0f)
            {
                pos.y = suelo;
                IsJumping = false;
                verticalVelocity = 0f;
                transform.position = pos;
                if (Time.time <= saltoPedidoHasta) { saltoPedidoHasta = 0f; Jump(); }
                return;
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
            if (Vaulting) return;
            if (worldDirection.sqrMagnitude > 1f) worldDirection.Normalize();
            transform.position += Resolve(worldDirection * MoveSpeed * dt);
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
