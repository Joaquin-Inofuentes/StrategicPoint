using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;
using SP.Vehicles;

namespace SP.UI
{
    // Retículo + prompt contextual: "F: Poseer a X" o "T: Ir aquí".
    // También resalta el retículo cuando el disparo del jugador impacta.
    public class AimUI : MonoBehaviour
    {
        Text promptText;
        Image crosshair;
        Color crosshairBaseColor = Color.white;
        Vector2 crosshairBaseSize = new Vector2(6f, 6f);
        int watchedShooterId = -1;
        IDisposable damageSub;
        IDisposable environmentHitSub;

        public string CurrentPrompt { get; private set; } = "";
        public bool IsVisible => promptText != null && promptText.gameObject.activeSelf;

        public void Bind(Text prompt, Image cross)
        {
            promptText = prompt;
            crosshair = cross;
            if (cross != null)
            {
                crosshairBaseColor = cross.color;
                crosshairBaseSize = cross.rectTransform.sizeDelta;
            }
        }

        // Panel de info al apuntar a un aliado: vida, arma y especialidad.
        Text soldierInfoText;
        GameObject soldierInfoPanel;
        public void BindSoldierInfo(GameObject panel, Text info)
        {
            soldierInfoPanel = panel;
            soldierInfoText = info;
        }

        // Panel de info al apuntar a un vehículo: 4 cuadrados de asiento
        // (verde = libre, gris oscuro = ocupado), en el mismo orden que
        // VehicleSeatRole: Driver, Passenger1, Passenger2, Gunner.
        static readonly VehicleSeatRole[] SeatOrder =
        {
            VehicleSeatRole.Driver, VehicleSeatRole.Passenger1, VehicleSeatRole.Passenger2, VehicleSeatRole.Gunner
        };
        GameObject vehicleInfoPanel;
        Image[] seatSquares;
        public void BindVehicleInfo(GameObject panel, Image[] squares)
        {
            vehicleInfoPanel = panel;
            seatSquares = squares;
        }

        // Se llama explícitamente al construir la UI, no depende de OnEnable
        // (Bind/Initialize se llaman a mano en editor al armar la escena).
        public void Initialize()
        {
            damageSub?.Dispose();
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
            environmentHitSub?.Dispose();
            environmentHitSub = EventBus.Instance.Subscribe<EnvironmentHitEvent>(OnEnvironmentHit);
        }

        // `damageSub` y todas las referencias de abajo no sobreviven al
        // domain reload al entrar en Play mode (la escena se construyó en
        // editor): la suscripción a EventBus se pierde y los campos quedan
        // null. Como OnDamage nunca se dispararía para re-suscribirse solo,
        // hace falta un punto de entrada garantizado tras el reload:
        // OnEnable. Ojo: Crosshair/PromptText/SoldierInfoPanel/
        // VehicleInfoPanel son HERMANOS de este objeto bajo el Canvas, no
        // hijos — GetComponentInChildren no los encuentra, hay que buscarlos
        // por nombre desde el padre.
        void OnEnable()
        {
            if (damageSub == null) Initialize();

            var canvasRoot = transform.parent;
            if (canvasRoot == null) return;

            if (promptText == null)
            {
                var t = canvasRoot.Find("PromptText");
                if (t != null) promptText = t.GetComponent<Text>();
            }
            if (crosshair == null)
            {
                var t = canvasRoot.Find("Crosshair");
                if (t != null)
                {
                    crosshair = t.GetComponent<Image>();
                    if (crosshair != null)
                    {
                        crosshairBaseColor = crosshair.color;
                        crosshairBaseSize = crosshair.rectTransform.sizeDelta;
                    }
                }
            }
            if (soldierInfoPanel == null)
            {
                var t = canvasRoot.Find("SoldierInfoPanel");
                if (t != null)
                {
                    soldierInfoPanel = t.gameObject;
                    soldierInfoText = t.GetComponentInChildren<Text>(true);
                }
            }
            if (vehicleInfoPanel == null)
            {
                var t = canvasRoot.Find("VehicleInfoPanel");
                if (t != null)
                {
                    vehicleInfoPanel = t.gameObject;
                    // El primer Image es el fondo del panel; los siguientes 4,
                    // los cuadrados de asiento en el mismo orden en que se
                    // crearon (Driver, Passenger1, Passenger2, Gunner).
                    seatSquares = t.GetComponentsInChildren<Image>(true).Skip(1).Take(4).ToArray();
                }
            }
        }

        void OnDestroy()
        {
            damageSub?.Dispose();
            environmentHitSub?.Dispose();
        }

        // A quién mirar para saber si "mi" disparo impactó.
        public void SetWatchedShooter(int soldierId) => watchedShooterId = soldierId;

        static readonly Color EnemyHitColor = new Color(0.95f, 0.2f, 0.15f);
        static readonly Color VehicleHitColor = new Color(0.3f, 0.55f, 0.95f);
        static readonly Color ObstacleHitColor = new Color(0.75f, 0.75f, 0.78f);
        static readonly Color GroundHitColor = new Color(0.55f, 0.42f, 0.28f);

        // Tinte suave y permanente de la mirilla mientras se sostiene la
        // puntería sobre algo (no el flash de "le pegué", que es aparte):
        // así el jugador sabe qué tiene bajo la mira sin tener que leer el
        // cartel de texto. Vuelve al color base apenas deja de apuntarle.
        static readonly Color AllyTint = new Color(0.4f, 0.85f, 1f);
        static readonly Color EnemyTint = new Color(1f, 0.45f, 0.4f);
        static readonly Color VehicleTint = new Color(0.5f, 0.7f, 1f);
        static readonly Color ObstacleTint = new Color(0.85f, 0.85f, 0.85f);

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || crosshair == null || evt.AttackerId != watchedShooterId) return;
            StopAllCoroutines();
            StartCoroutine(FlashHitMarker(EnemyHitColor));
        }

        void OnEnvironmentHit(EnvironmentHitEvent evt)
        {
            if (!Application.isPlaying || crosshair == null || evt.ShooterId != watchedShooterId) return;
            Color color = evt.Kind switch
            {
                EnvironmentHitKind.Vehicle => VehicleHitColor,
                EnvironmentHitKind.Ground => GroundHitColor,
                _ => ObstacleHitColor,
            };
            StopAllCoroutines();
            StartCoroutine(FlashHitMarker(color));
        }

        bool flashing;

        IEnumerator FlashHitMarker(Color flashColor)
        {
            flashing = true;
            crosshair.color = flashColor;
            crosshair.rectTransform.sizeDelta = crosshairBaseSize * 2.4f;

            float t = 0f;
            const float duration = 0.22f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                crosshair.rectTransform.sizeDelta = Vector2.Lerp(crosshairBaseSize * 2.4f, crosshairBaseSize, k);
                // Vuelve al tinte de lo que se esté apuntando ahora (no al
                // blanco base): si seguís apuntando al mismo enemigo apenas
                // le pegaste, el flash debe apagarse hacia el rojo tenue de
                // "hay un enemigo ahí", no a blanco neutro.
                crosshair.color = Color.Lerp(flashColor, currentAimTint, k);
                yield return null;
            }

            crosshair.rectTransform.sizeDelta = crosshairBaseSize;
            crosshair.color = currentAimTint;
            flashing = false;
        }

        Color currentAimTint;

        public void UpdateFromAimResult(AimResult result)
        {
            switch (result.Type)
            {
                case AimTargetType.Ally:
                    CurrentPrompt = $"[F] Poseer a {result.Soldier.DisplayName}";
                    currentAimTint = AllyTint;
                    break;
                case AimTargetType.Enemy:
                    CurrentPrompt = $"Enemigo: {result.Soldier.DisplayName}";
                    currentAimTint = EnemyTint;
                    break;
                case AimTargetType.Vehicle:
                    CurrentPrompt = "[G] Ordenar subir al vehiculo";
                    currentAimTint = VehicleTint;
                    break;
                case AimTargetType.Obstacle:
                    CurrentPrompt = "Obstáculo";
                    currentAimTint = ObstacleTint;
                    break;
                case AimTargetType.Ground:
                    CurrentPrompt = "[T] Ir aquí";
                    currentAimTint = crosshairBaseColor;
                    break;
                default:
                    CurrentPrompt = "";
                    currentAimTint = crosshairBaseColor;
                    break;
            }

            if (promptText != null)
            {
                promptText.text = CurrentPrompt;
                promptText.gameObject.SetActive(!string.IsNullOrEmpty(CurrentPrompt));
            }

            // La mirilla en sí misma lleva el tinte de qué hay debajo (no
            // solo el cartel de texto), salvo mientras un flash de impacto
            // está en curso -- ese ya la termina dejando en este mismo
            // tinte al apagarse.
            if (crosshair != null && !flashing) crosshair.color = currentAimTint;

            UpdateSoldierInfo(result);
            UpdateVehicleInfo(result);
        }

        void UpdateSoldierInfo(AimResult result)
        {
            if (soldierInfoPanel == null) return;

            bool show = (result.Type == AimTargetType.Ally || result.Type == AimTargetType.Enemy) && result.Soldier != null;
            soldierInfoPanel.SetActive(show);
            if (!show || soldierInfoText == null) return;

            var s = result.Soldier;
            string tag = result.Type == AimTargetType.Enemy ? "[Enemigo] " : "";
            soldierInfoText.text = $"{tag}{s.DisplayName}   ·   Vida {s.Health.Current}/{s.Health.MaxHealth}   ·   Arma {s.Weapon.CurrentWeaponKind}   ·   {s.Role}";
        }

        void UpdateVehicleInfo(AimResult result)
        {
            if (vehicleInfoPanel == null) return;

            bool show = result.Type == AimTargetType.Vehicle && result.Vehicle != null;
            vehicleInfoPanel.SetActive(show);
            if (!show || seatSquares == null) return;

            var vehicle = result.Vehicle;
            for (int i = 0; i < seatSquares.Length && i < SeatOrder.Length; i++)
            {
                bool free = vehicle.IsSeatFree(SeatOrder[i]);
                seatSquares[i].color = free ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.15f, 0.15f, 0.16f);
            }
        }
    }
}
