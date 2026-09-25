using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SP.Core;

namespace SP.Vehicles
{
    // Conduce el vehículo solo cuando nadie lo está manejando a mano.
    // Recibe una orden de ir a un punto (clic derecho) y gira hacia allá
    // acelerando, hasta llegar.
    public class VehicleBrain : MonoBehaviour
    {
        [SerializeField] float arriveThreshold = 1.2f;
        [SerializeField] float turnDegPerSec = 130f;

        // A partir de que angulo el destino cuenta como "detras". 100 y no
        // 90 para que un destino apenas al costado no dispare marcha atras.
        public const float AnguloDeMarchaAtras = 100f;

        // Hasta donde conviene retroceder en vez de dar la vuelta. 15 m
        // cubre con margen toda la franja donde el arco no entraba (medido:
        // fallaba de 2 a 6 m) sin volver la marcha atras el modo normal de
        // recorrer el mapa, que a media velocidad seria tedioso.
        public const float DistanciaDeMarchaAtras = 15f;

        VehicleMotor motor;
        Vehicle vehicle;
        TurretAI turretAi;
        Vector3? destination;
        bool bootstrapped;

        public bool HasOrder => destination.HasValue;
        public Vector3? CurrentDestination => destination;
        public bool IsPlayerDriving { get; set; }

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            motor = GetComponent<VehicleMotor>();
            vehicle = GetComponent<Vehicle>();
            turretAi = GetComponentInChildren<TurretAI>();
            WorldSystemsRegistry.Register(this);
        }

        void OnDestroy() => WorldSystemsRegistry.Unregister(this);

        // Ruta por el NavMesh hacia el destino. VACIA = linea recta (Edit
        // mode, sin NavMesh horneado, o destino en el mismo tramo libre).
        // Antes el vehiculo apuntaba directo al punto: una orden [T] al otro
        // lado de una muralla lo dejaba empujando el cubo para siempre.
        readonly List<Vector3> route = new List<Vector3>();
        int routeIndex;
        public IReadOnlyList<Vector3> Route => route;
        public int RouteIndex => routeIndex;

        // El NavMesh se horneo para un soldado (radio 0,5); el blindado mide
        // 2,2 de ancho. Cada esquina de la ruta se corre esta distancia
        // hacia el interior de la zona caminable, lejos del borde mas
        // cercano, para que el casco no raspe las paredes.
        const float HolguraDelBlindado = 1.4f;
        const float LlegadaIntermedia = 3f;

        public void IssueMoveOrder(Vector3 point)
        {
            destination = point;
            ReiniciarAtasco();
            BuildRoute(point);
        }

        // --- Patrulla (tanques enemigos) ---
        // Con tripulacion y sin orden del jugador, da vueltas por estos puntos
        // (con una pausa en cada uno) y frena cuando su torreta tiene blanco.
        [SerializeField] Vector3[] patrullaPuntos;
        [SerializeField] bool frenarAlCombatir;
        [SerializeField] float gasMaximo = 1f;
        [SerializeField] float pausaEnPunto = 2.5f;
        int patrullaIdx;
        float pausaRestante;
        public bool TienePatrulla => patrullaPuntos != null && patrullaPuntos.Length > 0;
        public int IndiceDePatrulla => patrullaIdx;

        public void ConfigurarPatrulla(Vector3[] puntos, bool frenarSiCombate, float gasTope)
        {
            patrullaPuntos = puntos;
            frenarAlCombatir = frenarSiCombate;
            gasMaximo = Mathf.Clamp(gasTope, 0.1f, 1f);
            patrullaIdx = 0;
        }

        // Aviso al jugador: un tanque enemigo entra en su radio de vision.
        float proximoAvisoTanque;
        void AvisarTanqueEnemigo()
        {
            if (vehicle == null || vehicle.Bando != SP.Combat.TeamId.Enemy || vehicle.IsDestroyed) return;
            var lider = SP.Ai.AjustesDeEscuadra.Lider;
            if (lider == null || Time.time < proximoAvisoTanque) return;
            if ((lider.transform.position - transform.position).sqrMagnitude > 70f * 70f) return;
            proximoAvisoTanque = Time.time + 25f;
            SP.Presentation.Feedback.Accion(SP.Presentation.SfxKind.EnemySpotted, "¡TANQUE ENEMIGO A LA VISTA!",
                transform.position, SP.Presentation.Feedback.Bad, aviso: true, pulso: true, volumen: 0.7f);
        }

        void TickPatrulla(float dt)
        {
            AvisarTanqueEnemigo();
            if (!TienePatrulla || destination.HasValue || IsPlayerDriving) return;
            if (vehicle == null || vehicle.Driver == null || vehicle.IsDestroyed) return;
            if (turretAi != null && turretAi.IsEngaging) { motor.Brake(dt); return; }
            pausaRestante -= dt;
            if (pausaRestante > 0f) { motor.Brake(dt); return; }
            pausaRestante = pausaEnPunto;
            IssueMoveOrder(patrullaPuntos[patrullaIdx]);
            patrullaIdx = (patrullaIdx + 1) % patrullaPuntos.Length;
        }

        public void Stop()
        {
            destination = null;
            route.Clear();
            routeIndex = 0;
        }

        // Anti-atasco: un vehiculo que empuja un cubo (por ejemplo, un destino
        // que cae ADENTRO de un obstaculo) se quedaba con la orden viva para
        // siempre. Si en SegundosDeAtasco no avanza ni MetrosDeAtasco: cerca
        // del destino se da por cumplida; lejos, se recalcula la ruta una
        // vez y, si sigue igual, se cancela.
        const float SegundosDeAtasco = 2f;
        const float MetrosDeAtasco = 0.5f;
        const float CercaDelDestino = 8f;
        float relojDeAtasco;
        Vector3 anclaDeAtasco;
        bool replanificado;

        void ReiniciarAtasco()
        {
            relojDeAtasco = 0f;
            anclaDeAtasco = transform.position;
            replanificado = false;
        }

        // true si la orden se termino por atasco.
        bool VigilarAtasco(float dt, float distanciaAlDestino)
        {
            relojDeAtasco += dt;
            if (relojDeAtasco < SegundosDeAtasco) return false;

            var avance = transform.position - anclaDeAtasco;
            avance.y = 0f;
            relojDeAtasco = 0f;
            anclaDeAtasco = transform.position;
            if (avance.magnitude >= MetrosDeAtasco) return false;

            if (distanciaAlDestino <= CercaDelDestino || replanificado)
            {
                Stop();
                return true;
            }
            replanificado = true;
            BuildRoute(destination.Value);
            return false;
        }

        void BuildRoute(Vector3 goal)
        {
            route.Clear();
            routeIndex = 0;
            if (!Application.isPlaying) return;

            if (!NavMesh.SamplePosition(transform.position, out var desde, 6f, NavMesh.AllAreas)) return;
            if (!NavMesh.SamplePosition(goal, out var hasta, 6f, NavMesh.AllAreas)) return;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(desde.position, hasta.position, NavMesh.AllAreas, path)) return;
            if (path.status != NavMeshPathStatus.PathComplete || path.corners.Length <= 2) return;

            // corners[0] es donde esta parado, y el ultimo es el destino
            // (que se sigue usando tal cual, sin correrlo).
            for (int i = 1; i < path.corners.Length - 1; i++)
                route.Add(Inflar(path.corners[i]));
        }

        static Vector3 Inflar(Vector3 esquina)
        {
            if (NavMesh.FindClosestEdge(esquina, out var borde, NavMesh.AllAreas) && borde.distance < HolguraDelBlindado)
            {
                var corrida = esquina + borde.normal * (HolguraDelBlindado - borde.distance);
                if (NavMesh.SamplePosition(corrida, out var valida, 1f, NavMesh.AllAreas)) return valida.position;
            }
            return esquina;
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (SP.Ai.AiBrain.IAPausada) return; // cinematica de apertura en curso: ver AiBrain.Tick()
            if (motor == null) return;
            // Igual que TurretWeapon: Tick() se llama directo desde
            // WorldSimulationDriver, "enabled=false" no alcanza para
            // frenar un vehículo destruido -- una carcasa quemada no
            // debería poder seguir manejando sola hacia un destino viejo.
            if (vehicle != null && vehicle.IsDestroyed) return;
            TickPatrulla(dt);
            if (IsPlayerDriving || !destination.HasValue) return;

            // Tanque enemigo con blanco a la vista: para y dispara.
            if (frenarAlCombatir && turretAi != null && turretAi.IsEngaging) { motor.Brake(dt); return; }

            // Misma regla que TurretAI.IsEngaging, del otro lado: con un
            // solo tripulante trabado disparandole a algo, esa persona no
            // puede ADEMAS estar manejando -- la orden de movimiento
            // queda pendiente (no se pierde, solo espera) hasta que
            // suelte el blanco o suba alguien mas.
            if (vehicle != null && vehicle.OccupantCount == 1 && turretAi != null && turretAi.IsEngaging) return;

            if (VigilarAtasco(dt, (destination.Value - transform.position).magnitude)) return;

            // Tramo actual: la siguiente esquina de la ruta, o el destino
            // final cuando ya no quedan.
            Vector3 objetivo = destination.Value;
            bool esIntermedio = routeIndex < route.Count;
            if (esIntermedio) objetivo = route[routeIndex];

            Vector3 delta = objetivo - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (esIntermedio)
            {
                if (dist <= LlegadaIntermedia)
                {
                    routeIndex++;
                    return; // el proximo tick apunta a la esquina siguiente
                }
            }
            else if (dist <= arriveThreshold)
            {
                // La orden termina cuando el vehiculo YA paro, no al llegar:
                // antes se frenaba un solo tick y la velocidad quedaba
                // guardada en el motor (3 u/s de un vehiculo detenido).
                motor.Brake(dt);
                if (motor.IsStopped)
                {
                    destination = null;
                    route.Clear();
                    routeIndex = 0;
                }
                return;
            }

            // Del plan del usuario: "En tanque si selecciono una posicion
            // atras de el. Empieza a dar circulos sin sentido. Deberia solo
            // retroceder".
            //
            // Antes esto giraba el chasis hacia el destino Y ADEMAS pisaba
            // el acelerador a fondo todo el tiempo. Con el destino atras,
            // el vehiculo sale para adelante mientras gira: describe un
            // arco cuyo radio, a maxima velocidad, es mas grande que la
            // distancia al destino, asi que el destino queda ADENTRO del
            // circulo y no se alcanza nunca. Medido, con el destino a 2 m
            // atras: 231 metros recorridos en 20 segundos sin acercarse ni
            // un centimetro. De 8 m para atras si llegaba, porque ahi el
            // arco entra.
            //
            // Dos reglas, y la segunda sola ya rompe el circulo:
            float anguloAlDestino = Vector3.Angle(transform.forward, delta.normalized);

            // 1) Detras y cerca: se retrocede. Es lo que haria cualquiera
            //    con el auto: no se da la vuelta para ir tres metros atras.
            //    Se apunta la COLA al destino, no el morro.
            if (anguloAlDestino > AnguloDeMarchaAtras && dist <= DistanciaDeMarchaAtras)
            {
                var rotAtras = Quaternion.LookRotation(-delta.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rotAtras, turnDegPerSec * dt);
                motor.Drive(-1f, 0f, dt);
                return;
            }

            // 2) Para adelante, pero el acelerador sigue al alineamiento:
            //    mientras esta cruzado no avanza (gira casi en el lugar) y
            //    recien pisa a fondo cuando ya mira al destino. Avanzar de
            //    costado es exactamente lo que convertia el giro en arco.
            var targetRot = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnDegPerSec * dt);

            // Llegada: se empieza a frenar con la distancia que hace falta para
            // parar (v^2 / 2a, con margen sobre la deceleracion real del
            // motor). Antes solo se frenaba AL llegar: con una orden a 6 m y
            // el vehiculo a 12 m/s se pasaba de largo unos 8 m.
            if (!esIntermedio)
            {
                float v = Mathf.Abs(motor.CurrentSpeed);
                float distanciaDeFrenado = v * v / (2f * 11f);
                if (dist - arriveThreshold <= distanciaDeFrenado)
                {
                    motor.Brake(dt);
                    return;
                }
            }

            float gas = Mathf.Clamp01(Mathf.Cos(anguloAlDestino * Mathf.Deg2Rad)) * gasMaximo;
            motor.Drive(gas, 0f, dt);
        }
    }
}
