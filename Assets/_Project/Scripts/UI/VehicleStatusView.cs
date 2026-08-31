using UnityEngine;
using UnityEngine.UI;
using SP.Vehicles;

namespace SP.UI
{
    // HUD que reemplaza al de arma en cuanto se sube al vehículo:
    // velocímetro, barra de vida del vehículo y quién está de artillero.
    // Un tanque no se conduce igual que se camina, así que su UI tampoco
    // debería ser la misma (antes, adentro del vehículo, no había ningún
    // HUD propio -- solo el texto de instrucciones).
    public class VehicleStatusView : MonoBehaviour
    {
        Text speedLabel;
        Image healthFill;
        Text gunnerLabel;
        Text seatLabel;

        public void Bind(Text speed, Image health, Text gunner)
        {
            speedLabel = speed;
            healthFill = health;
            gunnerLabel = gunner;
        }

        void OnEnable()
        {
            if (speedLabel == null)
            {
                var t = transform.Find("SpeedText");
                if (t != null) speedLabel = t.GetComponent<Text>();
            }
            if (healthFill == null)
            {
                var t = transform.Find("HealthBarBG/HealthBarFill");
                if (t != null) healthFill = t.GetComponent<Image>();
            }
            if (gunnerLabel == null)
            {
                var t = transform.Find("GunnerText");
                if (t != null) gunnerLabel = t.GetComponent<Text>();
            }
            if (seatLabel == null)
            {
                var t = transform.Find("SeatText");
                if (t != null) seatLabel = t.GetComponent<Text>();
            }
        }

        // El rol se deducia leyendo el texto largo de controles (que
        // cambia todo el tiempo). No habia un indicador fijo y estable de
        // en que asiento estabas.
        static string RoleLabel(SP.Vehicles.VehicleSeatRole? role) => role switch
        {
            SP.Vehicles.VehicleSeatRole.Driver => "Conductor",
            SP.Vehicles.VehicleSeatRole.Gunner => "Artillero",
            SP.Vehicles.VehicleSeatRole.Passenger1 => "Pasajero",
            SP.Vehicles.VehicleSeatRole.Passenger2 => "Pasajero",
            _ => "-",
        };

        public void SetSeat(SP.Vehicles.VehicleSeatRole? role)
        {
            if (seatLabel != null) seatLabel.text = RoleLabel(role);
        }

        public void UpdateFrom(Vehicle vehicle, VehicleMotor motor, bool braking = false)
        {
            if (vehicle == null || motor == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            if (speedLabel != null)
            {
                // Marcha atras: antes CurrentSpeed negativo se mostraba
                // identico a positivo (con Abs), sin ningun indicio de
                // que ibas para atras salvo mirar el paisaje afuera.
                bool reversing = motor.CurrentSpeed < -0.1f;
                string prefix = reversing ? "R " : "";
                string suffix = braking ? "  FRENANDO" : "";
                speedLabel.text = $"{prefix}{Mathf.Abs(motor.CurrentSpeed):0.0} u/s{suffix}";
                speedLabel.color = braking ? new Color(0.95f, 0.6f, 0.2f)
                    : reversing ? new Color(0.95f, 0.85f, 0.3f)
                    : Color.white;
            }

            if (healthFill != null)
            {
                var hp = vehicle.Health;
                healthFill.fillAmount = hp.MaxHealth > 0 ? (float)hp.Current / hp.MaxHealth : 0f;
                healthFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.4f, 0.85f, 0.45f), healthFill.fillAmount);
            }

            if (gunnerLabel != null)
            {
                // Pedido explicito: si hay UN solo tripulante tiene que
                // notarse a simple vista -- esa persona maneja O dispara,
                // nunca las dos cosas (TurretAI/VehicleBrain), y sin este
                // rotulo no habia forma de saber por que el cañon dejaba
                // de responder solo al arrancar a andar.
                bool solo = vehicle.OccupantCount == 1;
                string crew = solo ? $"Tripulación: 1/{vehicle.Capacity} (SOLO)" : $"Tripulación: {vehicle.OccupantCount}/{vehicle.Capacity}";
                string gunnerPart = vehicle.Gunner != null ? $" · Artillero: {vehicle.Gunner.DisplayName}" : "";
                gunnerLabel.text = crew + gunnerPart;
                gunnerLabel.color = solo ? new Color(0.95f, 0.65f, 0.2f) : new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
