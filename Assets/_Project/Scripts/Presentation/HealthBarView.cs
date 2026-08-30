using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;

namespace SP.Presentation
{
    // Barra de vida flotante sobre un soldado. Sube y baja con la vida y
    // siempre mira a la cámara activa (billboard) — sirve igual en FPS y RTS.
    // Se auto-inicializa buscando a su propio Soldier: no depende de que
    // alguien la configure a mano (eso no sobrevive a un domain reload).
    public class HealthBarView : MonoBehaviour
    {
        Health health;
        Image fill;
        bool bootstrapped;

        void Start() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;

            var soldier = GetComponentInParent<Soldier>();
            if (soldier == null || soldier.Health == null) return;

            bootstrapped = true;
            health = soldier.Health;

            var fillTransform = transform.Find("Fill");
            fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (fill != null)
                fill.color = soldier.Team == TeamId.Player ? new Color(0.35f, 0.9f, 0.4f) : new Color(0.95f, 0.3f, 0.25f);
        }

        void LateUpdate()
        {
            if (!bootstrapped) Bootstrap();
            if (health == null) return;

            float pct = health.MaxHealth > 0 ? (float)health.Current / health.MaxHealth : 0f;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(pct);

            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
