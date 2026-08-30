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
        }

        public void UpdateFrom(Vehicle vehicle, VehicleMotor motor)
        {
            if (vehicle == null || motor == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            if (speedLabel != null)
                speedLabel.text = $"{Mathf.Abs(motor.CurrentSpeed):0.0} u/s";

            if (healthFill != null)
            {
                var hp = vehicle.Health;
                healthFill.fillAmount = hp.MaxHealth > 0 ? (float)hp.Current / hp.MaxHealth : 0f;
                healthFill.color = Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.4f, 0.85f, 0.45f), healthFill.fillAmount);
            }

            if (gunnerLabel != null)
                gunnerLabel.text = vehicle.Gunner != null ? $"Artillero: {vehicle.Gunner.DisplayName}" : "Artillero: -";
        }
    }
}
