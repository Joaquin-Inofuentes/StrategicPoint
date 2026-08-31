using System;
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

        // Mostrar TODAS las barras siempre es inviable con cincuenta
        // unidades: satura la pantalla y ademas cuesta dibujarlas. Solo se
        // muestra la del que acaba de recibir daño, que es justo cuando la
        // informacion importa, y se apaga sola despues de unos segundos
        // sin nuevos impactos. La del poseido no se oculta nunca: esa es
        // la del propio jugador.
        const float VisibleSeconds = 3.5f;
        float hideAt = -1f;
        IDisposable damageSub;
        Soldier owner;

        void OnEnable()
        {
            Bootstrap();
            damageSub?.Dispose();
            damageSub = SP.Core.EventBus.Instance.Subscribe<SP.Core.DamageTakenEvent>(OnAnyDamage);
        }

        void OnDisable() => damageSub?.Dispose();

        void OnAnyDamage(SP.Core.DamageTakenEvent evt)
        {
            if (owner == null || evt.TargetId != owner.Id) return;
            hideAt = Time.time + VisibleSeconds;
        }

        void Start() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;

            var soldier = GetComponentInParent<Soldier>();
            if (soldier == null || soldier.Health == null) return;

            bootstrapped = true;
            owner = soldier;
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

            bool shouldShow = Time.time <= hideAt;
            // Los hijos, no este objeto: apagarse a si mismo desde aca
            // dispararia OnDisable y mataria la suscripcion al bus, con lo
            // que la barra no volveria a aparecer nunca mas.
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf != shouldShow) child.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            float pct = health.MaxHealth > 0 ? (float)health.Current / health.MaxHealth : 0f;
            if (fill != null) fill.fillAmount = Mathf.Clamp01(pct);

            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
