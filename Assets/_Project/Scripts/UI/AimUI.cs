using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;

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

        // Se llama explícitamente al construir la UI, no depende de OnEnable
        // (Bind/Initialize se llaman a mano en editor al armar la escena).
        public void Initialize()
        {
            damageSub?.Dispose();
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        // `damageSub` y `promptText`/`crosshair` no sobreviven al domain
        // reload al entrar en Play mode (la escena se construyó en editor):
        // la suscripción a EventBus se pierde y los campos quedan null. Como
        // OnDamage nunca se dispararía para re-suscribirse solo, hace falta
        // un punto de entrada garantizado tras el reload: OnEnable.
        void OnEnable()
        {
            if (damageSub == null) Initialize();
            if (promptText == null) promptText = GetComponentInChildren<Text>(true);
            if (crosshair == null)
            {
                crosshair = GetComponentInChildren<Image>(true);
                if (crosshair != null)
                {
                    crosshairBaseColor = crosshair.color;
                    crosshairBaseSize = crosshair.rectTransform.sizeDelta;
                }
            }
        }

        void OnDestroy() => damageSub?.Dispose();

        // A quién mirar para saber si "mi" disparo impactó.
        public void SetWatchedShooter(int soldierId) => watchedShooterId = soldierId;

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || crosshair == null || evt.AttackerId != watchedShooterId) return;
            StopAllCoroutines();
            StartCoroutine(FlashHitMarker());
        }

        IEnumerator FlashHitMarker()
        {
            crosshair.color = new Color(0.95f, 0.2f, 0.15f);
            crosshair.rectTransform.sizeDelta = crosshairBaseSize * 2.4f;

            float t = 0f;
            const float duration = 0.22f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                crosshair.rectTransform.sizeDelta = Vector2.Lerp(crosshairBaseSize * 2.4f, crosshairBaseSize, k);
                crosshair.color = Color.Lerp(new Color(0.95f, 0.2f, 0.15f), crosshairBaseColor, k);
                yield return null;
            }

            crosshair.rectTransform.sizeDelta = crosshairBaseSize;
            crosshair.color = crosshairBaseColor;
        }

        public void UpdateFromAimResult(AimResult result)
        {
            switch (result.Type)
            {
                case AimTargetType.Ally:
                    CurrentPrompt = $"[F] Poseer a {result.Soldier.DisplayName}";
                    break;
                case AimTargetType.Vehicle:
                    CurrentPrompt = "[G] Ordenar subir al vehiculo";
                    break;
                case AimTargetType.Ground:
                    CurrentPrompt = "[T] Ir aquí";
                    break;
                default:
                    CurrentPrompt = "";
                    break;
            }

            if (promptText != null)
            {
                promptText.text = CurrentPrompt;
                promptText.gameObject.SetActive(!string.IsNullOrEmpty(CurrentPrompt));
            }
        }
    }
}
