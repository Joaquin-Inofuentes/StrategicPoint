using UnityEngine;
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

        public void IssueMoveOrder(Vector3 point)
        {
            destination = point;
        }

        public void Stop()
        {
            destination = null;
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (motor == null) return;
            // Igual que TurretWeapon: Tick() se llama directo desde
            // WorldSimulationDriver, "enabled=false" no alcanza para
            // frenar un vehículo destruido -- una carcasa quemada no
            // debería poder seguir manejando sola hacia un destino viejo.
            if ((vehicle != null && vehicle.IsDestroyed) || IsPlayerDriving || !destination.HasValue) return;

            // Misma regla que TurretAI.IsEngaging, del otro lado: con un
            // solo tripulante trabado disparandole a algo, esa persona no
            // puede ADEMAS estar manejando -- la orden de movimiento
            // queda pendiente (no se pierde, solo espera) hasta que
            // suelte el blanco o suba alguien mas.
            if (vehicle != null && vehicle.OccupantCount == 1 && turretAi != null && turretAi.IsEngaging) return;

            Vector3 delta = destination.Value - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (dist <= arriveThreshold)
            {
                destination = null;
                motor.Brake(dt);
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

            float gas = Mathf.Clamp01(Mathf.Cos(anguloAlDestino * Mathf.Deg2Rad));
            motor.Drive(gas, 0f, dt);
        }
    }
}
