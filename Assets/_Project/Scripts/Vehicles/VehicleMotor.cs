using UnityEngine;

namespace SP.Vehicles
{
    // Motor del vehículo: acelera y frena de forma progresiva (no es
    // instantáneo como el motor de un soldado), y gira más rápido cuanto
    // más rápido va. Sin física real: pura matemática de transform.
    public class VehicleMotor : MonoBehaviour
    {
        [SerializeField] float maxSpeed = 12f;
        [SerializeField] float acceleration = 8f;
        [SerializeField] float brakeDeceleration = 14f;
        [SerializeField] float dragWhenIdle = 4f;
        [SerializeField] float turnDegPerSec = 70f;

        public float CurrentSpeed { get; private set; }
        public float MaxSpeed => maxSpeed;

        // throttle: -1..1 (atrás/adelante). steer: -1..1 (izq/der).
        public void Drive(float throttle, float steer, float dt)
        {
            if (Mathf.Abs(throttle) > 0.01f)
            {
                CurrentSpeed += throttle * acceleration * dt;
            }
            else
            {
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, dragWhenIdle * dt);
            }

            CurrentSpeed = Mathf.Clamp(CurrentSpeed, -maxSpeed * 0.5f, maxSpeed);

            float speedFactor = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / maxSpeed);
            if (Mathf.Abs(CurrentSpeed) > 0.05f)
                transform.Rotate(Vector3.up, steer * turnDegPerSec * speedFactor * Mathf.Sign(CurrentSpeed) * dt, Space.World);

            transform.position += transform.forward * CurrentSpeed * dt;
            KnockNearbyProps();
        }

        public void Brake(float dt)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, brakeDeceleration * dt);
            transform.position += transform.forward * CurrentSpeed * dt;
            KnockNearbyProps();
        }

        // Antes el vehiculo atravesaba el escenario sin alterar nada, lo
        // que reforzaba la sensacion de que flota en vez de pesar. Solo
        // cuenta si va con algo de velocidad: estar apoyado contra un
        // bidon quieto no deberia voltearlo.
        void KnockNearbyProps()
        {
            if (Mathf.Abs(CurrentSpeed) < 1f) return;
            var props = SP.Presentation.LightProp.All;
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (prop == null || prop.IsKnocked) continue;
                var d = prop.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= prop.KnockRadius + 1.4f) prop.Knock(transform.forward * Mathf.Sign(CurrentSpeed));
            }
        }

        public bool IsStopped => Mathf.Abs(CurrentSpeed) < 0.05f;
    }
}
