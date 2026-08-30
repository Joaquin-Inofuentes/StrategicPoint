using UnityEngine;

namespace SP.Vehicles
{
    // Conduce el vehículo solo cuando nadie lo está manejando a mano.
    // Recibe una orden de ir a un punto (clic derecho) y gira hacia allá
    // acelerando, hasta llegar.
    public class VehicleBrain : MonoBehaviour
    {
        [SerializeField] float arriveThreshold = 1.2f;
        [SerializeField] float turnDegPerSec = 130f;

        VehicleMotor motor;
        Vehicle vehicle;
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
        }

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
            if (IsPlayerDriving || !destination.HasValue) return;

            Vector3 delta = destination.Value - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (dist <= arriveThreshold)
            {
                destination = null;
                motor.Brake(dt);
                return;
            }

            // La IA gira el chasis directamente (no depende de la velocidad,
            // a diferencia del volante del jugador) y el motor solo empuja
            // hacia adelante: así reorienta con fiabilidad sin importar
            // desde qué ángulo llegó la orden.
            var targetRot = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnDegPerSec * dt);

            motor.Drive(1f, 0f, dt);
        }
    }
}
