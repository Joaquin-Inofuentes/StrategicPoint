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

        // Pedido explicito: "cada uno con su icono quiero ver vida y
        // funcion abajo a la izquierda para saber q hace cada uno y yo
        // abajo en el centro icono para saber si estoy conduciendo o
        // metralleta o cañon". Una fila fija por asiento (Driver, Gunner,
        // Passenger1, Passenger2 -- ese orden, ver CrewRoleOrder): se
        // prende o apaga segun este ocupado, nunca se crea/destruye en
        // runtime.
        static readonly VehicleSeatRole[] CrewRoleOrder =
        {
            VehicleSeatRole.Driver, VehicleSeatRole.Gunner, VehicleSeatRole.Passenger1, VehicleSeatRole.Passenger2
        };

        GameObject crewPanelRoot;
        GameObject[] crewRows;
        Image[] crewIcons;
        Text[] crewIconLabels;
        Text[] crewNameLabels;
        Image[] crewHealthFills;

        GameObject seatBadgeRoot;
        Image seatBadgeIcon;
        Text seatBadgeIconLabel;
        Text seatBadgeLabel;

        public void Bind(Text speed, Image health, Text gunner)
        {
            speedLabel = speed;
            healthFill = health;
            gunnerLabel = gunner;
        }

        // Cablea las 4 filas de tripulación (mismo orden que CrewRoleOrder)
        // y la insignia de asiento propio. Separado de Bind (arriba) porque
        // ese ya lo llaman caminos viejos con la firma de 3 argumentos --
        // no hacia falta tocarlo, solo sumar esto.
        public void BindCrew(GameObject panelRoot, GameObject[] rows, Image[] icons, Text[] iconLabels, Text[] nameLabels, Image[] healthFills,
            GameObject badgeRoot, Image badgeIcon, Text badgeIconLabel, Text badgeLabel)
        {
            crewPanelRoot = panelRoot;
            crewRows = rows;
            crewIcons = icons;
            crewIconLabels = iconLabels;
            crewNameLabels = nameLabels;
            crewHealthFills = healthFills;
            seatBadgeRoot = badgeRoot;
            seatBadgeIcon = badgeIcon;
            seatBadgeIconLabel = badgeIconLabel;
            seatBadgeLabel = badgeLabel;
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

            // BUG REAL que esto corrige: BindCrew (llamado una sola vez al
            // construir la escena) pasa referencias por campos privados SIN
            // [SerializeField] -- Unity no los guarda en el .unity, asi que
            // sobrevivian mientras el Editor seguia con el mismo dominio
            // pero se perdian (quedaban null) apenas se volvia a entrar a
            // Play desde una escena recargada de disco. Mismo remedio que ya
            // usan speedLabel/healthFill/etc. arriba: redescubrir por
            // nombre en la jerarquia, no depender de que la referencia haya
            // sobrevivido.
            if (crewPanelRoot == null && transform.parent != null)
            {
                var panel = transform.parent.Find("VehicleCrewPanel");
                if (panel != null)
                {
                    crewPanelRoot = panel.gameObject;
                    crewRows = new GameObject[4];
                    crewIcons = new Image[4];
                    crewIconLabels = new Text[4];
                    crewNameLabels = new Text[4];
                    crewHealthFills = new Image[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var row = panel.Find("Row_" + i);
                        if (row == null) continue;
                        crewRows[i] = row.gameObject;
                        var icon = row.Find("Icon");
                        if (icon != null)
                        {
                            crewIcons[i] = icon.GetComponent<Image>();
                            var iconLabel = icon.Find("IconLabel");
                            if (iconLabel != null) crewIconLabels[i] = iconLabel.GetComponent<Text>();
                        }
                        var nameT = row.Find("NameLabel");
                        if (nameT != null) crewNameLabels[i] = nameT.GetComponent<Text>();
                        var hpFill = row.Find("HealthBG/HealthFill");
                        if (hpFill != null) crewHealthFills[i] = hpFill.GetComponent<Image>();
                    }
                }
            }
            if (seatBadgeRoot == null && transform.parent != null)
            {
                var badge = transform.parent.Find("VehicleSeatBadge");
                if (badge != null)
                {
                    seatBadgeRoot = badge.gameObject;
                    var icon = badge.Find("Icon");
                    if (icon != null)
                    {
                        seatBadgeIcon = icon.GetComponent<Image>();
                        var iconLabel = icon.Find("IconLabel");
                        if (iconLabel != null) seatBadgeIconLabel = iconLabel.GetComponent<Text>();
                    }
                    var label = badge.Find("Label");
                    if (label != null) seatBadgeLabel = label.GetComponent<Text>();
                }
            }
        }

        // El rol se deducia leyendo el texto largo de controles (que
        // cambia todo el tiempo). No habia un indicador fijo y estable de
        // en que asiento estabas.
        //
        // Gunner ahora es "cañón" (dispara los obuses del TurretWeapon
        // principal) y Passenger1 es "metralleta" (TurretWeapon propio en
        // MetralletaPivot) -- antes "torreta"/"Pasajero 1" mezclaba las
        // dos armas del tanque bajo el mismo nombre generico.
        static string RoleLabel(SP.Vehicles.VehicleSeatRole? role) => role switch
        {
            SP.Vehicles.VehicleSeatRole.Driver => "Conductor",
            SP.Vehicles.VehicleSeatRole.Gunner => "Cañón",
            SP.Vehicles.VehicleSeatRole.Passenger1 => "Metralleta",
            SP.Vehicles.VehicleSeatRole.Passenger2 => "Pasajero",
            _ => "-",
        };

        // Abreviatura de 2 letras para el "icono" (este proyecto no usa
        // sprites, todo es geometria/color procedural -- un cuadrado de
        // color con estas letras es el mismo lenguaje visual que ya usa el
        // resto del HUD). Color propio por rol: mismo criterio que ya
        // distingue arma por color en WeaponCatalog/WeaponModels.
        public static string RoleIconText(SP.Vehicles.VehicleSeatRole role) => role switch
        {
            SP.Vehicles.VehicleSeatRole.Driver => "CO",
            SP.Vehicles.VehicleSeatRole.Gunner => "CA",
            SP.Vehicles.VehicleSeatRole.Passenger1 => "ME",
            _ => "PA",
        };

        public static Color RoleIconColor(SP.Vehicles.VehicleSeatRole role) => role switch
        {
            SP.Vehicles.VehicleSeatRole.Driver => new Color(0.3f, 0.55f, 0.95f),
            SP.Vehicles.VehicleSeatRole.Gunner => new Color(0.85f, 0.35f, 0.10f),
            SP.Vehicles.VehicleSeatRole.Passenger1 => new Color(0.95f, 0.85f, 0.25f),
            _ => new Color(0.55f, 0.55f, 0.55f),
        };

        public void SetSeat(SP.Vehicles.VehicleSeatRole? role)
        {
            if (seatLabel != null) seatLabel.text = RoleLabel(role);

            if (seatBadgeRoot == null) return;
            bool tieneAsiento = role.HasValue;
            if (seatBadgeRoot.activeSelf != tieneAsiento) seatBadgeRoot.SetActive(tieneAsiento);
            if (!tieneAsiento) return;
            if (seatBadgeIcon != null) seatBadgeIcon.color = RoleIconColor(role.Value);
            if (seatBadgeIconLabel != null) seatBadgeIconLabel.text = RoleIconText(role.Value);
            if (seatBadgeLabel != null) seatBadgeLabel.text = RoleLabel(role);
        }

        public void UpdateFrom(Vehicle vehicle, VehicleMotor motor, bool braking = false)
        {
            if (vehicle == null || motor == null)
            {
                gameObject.SetActive(false);
                if (crewPanelRoot != null) crewPanelRoot.SetActive(false);
                if (seatBadgeRoot != null) seatBadgeRoot.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            if (crewPanelRoot != null && !crewPanelRoot.activeSelf) crewPanelRoot.SetActive(true);

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
                gunnerLabel.text = crew;
                gunnerLabel.color = solo ? new Color(0.95f, 0.65f, 0.2f) : new Color(0.85f, 0.85f, 0.85f);
            }

            UpdateCrewRows(vehicle);
        }

        // Fila por asiento: icono de color + letras de rol, nombre del
        // ocupante y SU PROPIA vida (Health del soldado, no la del
        // vehiculo -- son dos barras de vida distintas, la del tanque ya
        // la muestra healthFill). Vacio = fila apagada, no un nombre
        // generico tipo "-": asi se ve de un vistazo cuantos asientos
        // quedan libres sin tener que contar texto.
        void UpdateCrewRows(Vehicle vehicle)
        {
            if (crewRows == null) return;
            for (int i = 0; i < CrewRoleOrder.Length && i < crewRows.Length; i++)
            {
                var role = CrewRoleOrder[i];
                var occupante = vehicle.SoldierInSeat(role);
                bool ocupado = occupante != null;
                if (crewRows[i] != null && crewRows[i].activeSelf != ocupado) crewRows[i].SetActive(ocupado);
                if (!ocupado) continue;

                if (crewIcons != null && crewIcons[i] != null) crewIcons[i].color = RoleIconColor(role);
                if (crewIconLabels != null && crewIconLabels[i] != null) crewIconLabels[i].text = RoleIconText(role);
                if (crewNameLabels != null && crewNameLabels[i] != null)
                {
                    var hp = occupante.Health;
                    crewNameLabels[i].text = $"{occupante.DisplayName} · {RoleLabel(role)}\n{hp.Current}/{hp.MaxHealth} vida";
                }
                if (crewHealthFills != null && crewHealthFills[i] != null)
                {
                    var hp = occupante.Health;
                    float frac = hp.MaxHealth > 0 ? (float)hp.Current / hp.MaxHealth : 0f;
                    crewHealthFills[i].fillAmount = frac;
                    crewHealthFills[i].color = Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.4f, 0.85f, 0.45f), frac);
                }
            }
        }
    }
}
