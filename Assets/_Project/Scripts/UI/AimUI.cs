using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;
using SP.Presentation;
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
        float crosshairUserScale = 1f;
        float crosshairSpreadFraction;

        // La mirilla era un punto blanco fijo, sin forma de ajustar
        // tamaño ni color -- sobre fondos claros practicamente
        // desaparecia y no habia ninguna opcion para compensarlo.
        public void SetCrosshairScale(float scale)
        {
            crosshairUserScale = scale;
            RecomputeCrosshairSize();
        }

        // La mirilla antes no decia nada de la precision real del arma:
        // se veia igual de chica disparando en rafaga sostenida que
        // recien equipada. Ahora se abre con la dispersion real que
        // WeaponHolder aplica al proyectil (no es un efecto cosmetico
        // aparte), y se cierra sola al dejar de disparar.
        public void SetSpread01(float fraction)
        {
            crosshairSpreadFraction = Mathf.Clamp01(fraction);
            RecomputeCrosshairSize();
        }

        void RecomputeCrosshairSize()
        {
            crosshairBaseSize = new Vector2(6f, 6f) * crosshairUserScale + Vector2.one * (crosshairSpreadFraction * 9f);
        }

        // El tinte real de cada frame lo recalcula UpdateFromAimResult a
        // partir de crosshairBaseColor (llamado cada frame en FPS), asi
        // que no hace falta reaplicar el color a mano aca: el proximo
        // frame ya lo toma.
        public void SetCrosshairColor(Color color) => crosshairBaseColor = color;
        int watchedShooterId = -1;
        IDisposable damageSub;
        IDisposable environmentHitSub;

        public string CurrentPrompt { get; private set; } = "";
        public bool IsVisible => promptText != null && promptText.gameObject.activeSelf;

        // Aviso bajo la mirilla cuando el arma no puede disparar por
        // municion: antes el disparo simplemente no salia y no habia
        // ninguna señal de por que -- el jugador podia pensar que el
        // juego no registro el click.
        Text ammoWarningText;
        public void BindAmmoWarning(Text text) => ammoWarningText = text;

        public void UpdateAmmoWarning(SP.Combat.WeaponHolder weapon)
        {
            if (ammoWarningText == null || weapon == null) return;
            if (weapon.IsReloading)
            {
                ammoWarningText.text = "RECARGANDO";
                ammoWarningText.gameObject.SetActive(true);
            }
            else if (weapon.CurrentAmmo <= 0)
            {
                ammoWarningText.text = "SIN MUNICION";
                ammoWarningText.gameObject.SetActive(true);
            }
            else
            {
                ammoWarningText.gameObject.SetActive(false);
            }
        }

        // La mirilla/cartel/paneles de info son puramente de puntería a
        // pie (FPS). En RTS o manejando un vehículo, nadie los actualiza
        // (nada llama UpdateFromAimResult ahí) y quedaban congelados con
        // lo último que se apuntó a pie -- un punto blanco fijo en medio
        // de la vista táctica, a veces con un cartel tipo "[F] Poseer a
        // X" de un aliado que ya ni está en pantalla.
        public void SetVisible(bool visible)
        {
            if (crosshair != null) crosshair.gameObject.SetActive(visible);
            if (!visible)
            {
                if (promptText != null) promptText.gameObject.SetActive(false);
                if (soldierInfoPanel != null) soldierInfoPanel.SetActive(false);
                if (vehicleInfoPanel != null) vehicleInfoPanel.SetActive(false);
                if (ammoWarningText != null) ammoWarningText.gameObject.SetActive(false);
                CurrentPrompt = "";
            }
        }

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
            if (ammoWarningText == null)
            {
                var t = canvasRoot.Find("AmmoWarningText");
                if (t != null) ammoWarningText = t.GetComponent<Text>();
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

        static readonly Color KillMarkerColor = new Color(1f, 0.85f, 0.15f);

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || crosshair == null || evt.AttackerId != watchedShooterId) return;
            StopAllCoroutines();

            // Herir y matar producian exactamente la misma señal -- el
            // jugador no podia distinguir "le pegue" de "lo mate" sin
            // mirar aparte. RemainingHealth ya viaja en el mismo evento
            // de daño que produjo la baja, no hace falta esperar un
            // EntityDiedEvent separado (que ademas no lleva quien mato).
            bool isKill = evt.RemainingHealth <= 0;
            StartCoroutine(FlashHitMarker(isKill ? KillMarkerColor : EnemyHitColor, isKill));
            if (isKill)
            {
                var clip = GenericSfx.Get(SfxKind.Swap); // tono agudo distintivo, ya existente en la paleta de sonidos
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 0.6f);
            }
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
            StartCoroutine(FlashHitMarker(color, false));
        }

        bool flashing;

        // isKill agranda mas el flash y lo sostiene mas tiempo: la misma
        // logica de "impacto" pero con mas peso, para que una baja se
        // LEA como algo mas importante que un impacto cualquiera, no
        // solo un color distinto.
        IEnumerator FlashHitMarker(Color flashColor, bool isKill)
        {
            flashing = true;
            float peakMultiplier = isKill ? 3.6f : 2.4f;
            float duration = isKill ? 0.4f : 0.22f;

            crosshair.color = flashColor;
            crosshair.rectTransform.sizeDelta = crosshairBaseSize * peakMultiplier;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                crosshair.rectTransform.sizeDelta = Vector2.Lerp(crosshairBaseSize * peakMultiplier, crosshairBaseSize, k);
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
                    // Antes ofrecía "[G] subir" igual sobre una carcasa
                    // destruida -- Vehicle.Mount() ya lo rechaza en
                    // silencio, pero el cartel no avisaba nada, como si
                    // sí fuera a funcionar.
                    CurrentPrompt = result.Vehicle.IsDestroyed ? "Vehículo destruido" : "[G] Ordenar subir al vehiculo";
                    currentAimTint = result.Vehicle.IsDestroyed ? ObstacleTint : VehicleTint;
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
            // El estado de IA es justamente lo que decide si conviene tomar
            // el control ahora o dejarlo pelear -- el panel mostraba vida,
            // arma y rol, pero no eso.
            string state = s.Brain != null ? StateLabel(s.Brain.State) : "-";
            soldierInfoText.text = $"{tag}{s.DisplayName}   ·   Vida {s.Health.Current}/{s.Health.MaxHealth}   ·   Arma {s.Weapon.CurrentWeaponKind}   ·   {s.Role}   ·   {state}";
        }

        static string StateLabel(SP.Ai.AiState state) => state switch
        {
            SP.Ai.AiState.Patrol => "Patrullando",
            SP.Ai.AiState.Idle => "En reposo",
            SP.Ai.AiState.MovingToOrder => "Cumpliendo orden",
            SP.Ai.AiState.MovingToAttackOrder => "Yendo a atacar",
            SP.Ai.AiState.Chase => "Persiguiendo",
            SP.Ai.AiState.Attack => "En combate",
            SP.Ai.AiState.Dead => "Caido",
            _ => state.ToString(),
        };

        void UpdateVehicleInfo(AimResult result)
        {
            if (vehicleInfoPanel == null) return;

            bool show = result.Type == AimTargetType.Vehicle && result.Vehicle != null;
            vehicleInfoPanel.SetActive(show);
            if (!show || seatSquares == null) return;

            var vehicle = result.Vehicle;
            // Una carcasa destruida no tiene "asientos libres" que
            // mostrar en verde -- confundía, parecía que todavía se
            // podía subir. Todos los cuadros en rojo apagado en vez.
            for (int i = 0; i < seatSquares.Length && i < SeatOrder.Length; i++)
            {
                if (vehicle.IsDestroyed) { seatSquares[i].color = new Color(0.45f, 0.15f, 0.15f); continue; }
                bool free = vehicle.IsSeatFree(SeatOrder[i]);
                seatSquares[i].color = free ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.15f, 0.15f, 0.16f);
            }
        }
    }
}
