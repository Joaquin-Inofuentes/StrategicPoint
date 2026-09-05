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
        Vector2 crosshairSpriteSize = new Vector2(6f, 6f); // sizeDelta real de la Image, sin escala de usuario ni spread
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
            crosshairBaseSize = crosshairSpriteSize * crosshairUserScale + Vector2.one * (crosshairSpreadFraction * 9f);
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

        // B2: la recarga como circulo sobre la mira. WeaponStatusView ya
        // muestra una barra recta en la esquina del HUD -- esto es la
        // MISMA fraccion (ReadinessFraction01), pero donde el jugador ya
        // tiene puesta la vista mientras apunta, no en un panel aparte.
        const string ReloadCircleName = "CirculoRecarga";
        const float ReloadCircleDiameter = 34f;
        static readonly Color ReloadCircleFondo = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color ReloadCircleRelleno = new Color(0.95f, 0.6f, 0.2f);

        CirculoDeProgreso circuloRecarga;

        // Comun a B2 (recarga) y B3 (vida del enemigo): un circulo
        // concentrico con la mirilla, auto-construido la primera vez que
        // se necesita. Diametros distintos para que, si algun dia
        // coinciden encendidos a la vez (recargando mientras se le sigue
        // apuntando a un enemigo), no queden exactamente superpuestos.
        CirculoDeProgreso EnsureCentralCircle(ref CirculoDeProgreso campo, Transform canvasRoot, string nombre, float diametro, Color colorFondo, Color colorRelleno)
        {
            if (campo != null) return campo;
            var t = canvasRoot.Find(nombre);
            if (t != null)
            {
                campo = t.GetComponent<CirculoDeProgreso>();
                return campo;
            }
            campo = CirculoDeProgreso.Construir(canvasRoot, diametro, colorFondo, colorRelleno);
            campo.gameObject.name = nombre;
            var rt = (RectTransform)campo.transform;
            if (crosshair != null)
            {
                // Mismo anclaje y centro que la mirilla: el circulo tiene
                // que quedar concentrico con ella, no en otro lugar de la
                // pantalla.
                rt.anchorMin = crosshair.rectTransform.anchorMin;
                rt.anchorMax = crosshair.rectTransform.anchorMax;
                rt.pivot = crosshair.rectTransform.pivot;
                rt.anchoredPosition = crosshair.rectTransform.anchoredPosition;
            }
            campo.SetVisible(false);
            return campo;
        }

        // Llamado cada frame en FPS junto a UpdateAmmoWarning. Solo se ve
        // mientras se esta recargando -- fuera de eso no aporta nada que
        // el jugador necesite mirar en medio de la mira.
        public void UpdateReloadCircle(SP.Combat.WeaponHolder weapon)
        {
            if (weapon == null) return;
            var canvasRoot = transform.parent;
            if (canvasRoot == null) return;
            EnsureCentralCircle(ref circuloRecarga, canvasRoot, ReloadCircleName, ReloadCircleDiameter, ReloadCircleFondo, ReloadCircleRelleno);
            if (circuloRecarga == null) return;
            circuloRecarga.SetVisible(weapon.IsReloading);
            if (weapon.IsReloading) circuloRecarga.SetProgreso(weapon.ReadinessFraction01);
        }

        // B3: la vida del enemigo bajo la mira, como circulo. Solo
        // mientras se le apunta -- UpdateFromAimResult es quien decide
        // eso y llama a este metodo con el resultado.
        const string EnemyHealthCircleName = "CirculoVidaEnemigo";
        const float EnemyHealthCircleDiameter = 50f;
        static readonly Color EnemyHealthCircleFondo = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color EnemyHealthCircleRelleno = new Color(0.9f, 0.25f, 0.2f);

        CirculoDeProgreso circuloVidaEnemigo;

        void UpdateEnemyHealthCircle(AimResult result)
        {
            var canvasRoot = transform.parent;
            if (canvasRoot == null) return;
            EnsureCentralCircle(ref circuloVidaEnemigo, canvasRoot, EnemyHealthCircleName, EnemyHealthCircleDiameter, EnemyHealthCircleFondo, EnemyHealthCircleRelleno);
            if (circuloVidaEnemigo == null) return;

            bool show = result.Type == AimTargetType.Enemy && result.Soldier != null && result.Soldier.Health != null;
            circuloVidaEnemigo.SetVisible(show);
            if (show) circuloVidaEnemigo.SetProgreso((float)result.Soldier.Health.Current / result.Soldier.Health.MaxHealth);
        }

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
                crosshairSpriteSize = cross.rectTransform.sizeDelta;
                RecomputeCrosshairSize();
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
            // El cartel de [F] Poseer era texto pelado sobre el terreno:
            // claro sobre claro, justo donde aparece. Ver SP.UI.FondoOpaco,
            // que es el MISMO fondo que usan los demas carteles.
            FondoOpaco.Poner(promptText);
            if (crosshair == null)
            {
                var t = canvasRoot.Find("Crosshair");
                if (t != null)
                {
                    crosshair = t.GetComponent<Image>();
                    if (crosshair != null)
                    {
                        crosshairBaseColor = crosshair.color;
                        crosshairSpriteSize = crosshair.rectTransform.sizeDelta;
                        RecomputeCrosshairSize();
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
                    var squares = t.GetComponentsInChildren<Image>(true).Skip(1).Take(4).ToArray();
                    if (squares.Length == SeatOrder.Length)
                    {
                        seatSquares = squares;
                    }
                    else
                    {
                        seatSquares = null;
                        Debug.LogWarning($"[AimUI] VehicleInfoPanel tiene {squares.Length} Image hijas tras el fondo (se esperaban {SeatOrder.Length}); el panel de asientos de vehiculo no se puede armar de forma confiable y queda desactivado.");
                    }
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
        // B5: un obstaculo con ObstacleMarker aguanta disparos y se puede
        // derrumbar (F1-F3, G1) -- uno sin ese componente (un Muro, por
        // ejemplo) es pared fija. Antes los dos se veian identicos bajo
        // la mira, sin ninguna forma de saber cual convenia tirotear.
        static readonly Color DestructibleTint = new Color(0.9f, 0.15f, 0.1f);

        // B5: "cada tipo late a una frecuencia distinta" -- un pulso
        // continuo e independiente del flash de impacto, para que se
        // note a simple vista que clase de cosa hay bajo la mira incluso
        // sin leer el cartel. 0 = no pulsa (piso, nada).
        public float CurrentPulseFrequency { get; private set; }

        static float PulseFrequencyFor(AimTargetType type) => type switch
        {
            AimTargetType.Ally => 1.2f,
            AimTargetType.Enemy => 2.4f,
            AimTargetType.Vehicle => 1.8f,
            AimTargetType.Obstacle => 3.2f,
            _ => 0f,
        };

        static readonly Color KillMarkerColor = new Color(1f, 0.85f, 0.15f);
        static readonly Color CriticalMarkerColor = new Color(1f, 0.45f, 0.05f);
        // Que fraccion de la vida maxima hace de un impacto un "critico".
        // Impacto, critico y baja son tres sucesos con consecuencias muy
        // distintas para la decision del jugador (seguir tirandole o pasar
        // al siguiente) y producian la misma señal exacta.
        const float CriticalDamageFraction = 0.3f;

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
            var victim = SP.Core.ActorRegistry.FindById(evt.TargetId);
            int maxHp = victim != null && victim.Health != null ? victim.Health.MaxHealth : 100;
            bool isCritical = !isKill && maxHp > 0 && evt.Amount >= maxHp * CriticalDamageFraction;

            var markerColor = isKill ? KillMarkerColor : isCritical ? CriticalMarkerColor : EnemyHitColor;
            StartCoroutine(FlashHitMarker(markerColor, isKill, isCritical));

            // Cada nivel tambien suena distinto: en pleno combate la señal
            // sonora llega antes que la visual.
            //
            // ESTE SE QUEDA EN PlayOneShot2D, no migra a AudioDirector.
            // El tono critico ES el pitch: es SfxKind.Hit a 1.7, o sea el
            // mismo clip del impacto normal subido de tono, y esa es toda
            // la diferencia entre "le pegue" y "le pegue fuerte". El
            // director fija el pitch el mismo (NextPitch, la variacion por
            // instancia del item 191) y no expone ningun parametro para
            // pedirlo: pasar por PlayUi haria sonar el critico igual que un
            // impacto comun y la senal desapareceria. Ojo, ademas, con
            // PlayOneShot: no captura el pitch, lo lee en vivo cada frame
            // -- por eso hace falta un AudioSource propio y no basta con
            // subirle el pitch a uno compartido.
            if (isCritical) GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Hit), 0.5f, 1.7f, "CritTone");
            if (isKill)
            {
                // Este si migra: no depende del pitch. Antes era un
                // PlayClipAtPoint posicional plantado en la camara, o sea
                // un 2D mal hecho y ademas un GameObject nuevo por baja.
                // Canal Ui y prioridad casi maxima: confirmar una baja es
                // la informacion que mas cambia la decision siguiente del
                // jugador (dejar de tirarle y buscar otro blanco).
                AudioDirector.PlayUi2D(SfxKind.Swap, 0.6f, 0.95f);
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

        // El pulso continuo de B5: un latido suave de tamaño, aparte del
        // flash de impacto (que ya maneja su propio tamaño mientras dura).
        void Update()
        {
            if (crosshair == null || flashing || CurrentPulseFrequency <= 0f) return;
            float k = (Mathf.Sin(Time.time * CurrentPulseFrequency) + 1f) * 0.5f;
            crosshair.rectTransform.sizeDelta = crosshairBaseSize * (1f + k * 0.15f);
        }

        // isKill agranda mas el flash y lo sostiene mas tiempo: la misma
        // logica de "impacto" pero con mas peso, para que una baja se
        // LEA como algo mas importante que un impacto cualquiera, no
        // solo un color distinto.
        IEnumerator FlashHitMarker(Color flashColor, bool isKill, bool isCritical = false)
        {
            flashing = true;
            // Tres tamaños y tres duraciones, no dos: el critico tiene que
            // leerse como algo intermedio y no confundirse con ninguno de
            // los extremos.
            float peakMultiplier = isKill ? 3.6f : isCritical ? 3.0f : 2.4f;
            float duration = isKill ? 0.4f : isCritical ? 0.3f : 0.22f;

            crosshair.color = flashColor;
            crosshair.rectTransform.sizeDelta = crosshairBaseSize * peakMultiplier;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
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
                    // E1: antes solo decia el nombre -- no invitaba a
                    // ninguna accion, aunque [F] ya la ejecutara.
                    CurrentPrompt = $"[F] Atacar a {result.Soldier.DisplayName}";
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
                    // Destructible = tiene ObstacleMarker (F1-F3, G1 ya lo
                    // usan como "esto se puede derrumbar"). Sin ese
                    // componente es pared fija: no vale la pena gastar
                    // municion contra ella.
                    bool esDestructible = result.HitTransform != null
                        && result.HitTransform.GetComponent<SP.Presentation.ObstacleMarker>() != null;
                    CurrentPrompt = esDestructible ? "Obstáculo destructible" : "Obstáculo";
                    currentAimTint = esDestructible ? DestructibleTint : ObstacleTint;
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

            CurrentPulseFrequency = PulseFrequencyFor(result.Type);

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
            UpdateEnemyHealthCircle(result);
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
            SP.Ai.AiState.Follow => "Siguiendo",
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
