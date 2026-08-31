using UnityEngine;

namespace SP.Vehicles
{
    // El cañon desprendido en la explosion final: sale hacia arriba con
    // algo de giro y cae. Gravedad a mano, no Rigidbody -- el proyecto no
    // usa fisica en ningun otro lado y meter un cuerpo rigido solo para
    // esto traeria colisiones que nadie mas maneja.
    public class DetachedTurretFlight : MonoBehaviour
    {
        Vector3 velocity;
        Vector3 spin;
        bool landed;

        const float Gravity = -18f;

        public void Launch()
        {
            velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(9f, 13f), Random.Range(-2.5f, 2.5f));
            spin = new Vector3(Random.Range(-260f, 260f), Random.Range(-180f, 180f), Random.Range(-260f, 260f));
        }

        void Update()
        {
            if (landed) return;
            float dt = Time.deltaTime;
            velocity.y += Gravity * dt;
            transform.position += velocity * dt;
            transform.Rotate(spin * dt, Space.Self);

            if (transform.position.y <= 0.25f)
            {
                var p = transform.position;
                p.y = 0.25f;
                transform.position = p;
                landed = true;
            }
        }
    }
}
