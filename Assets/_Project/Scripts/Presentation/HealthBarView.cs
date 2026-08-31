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
        // muestra la del que acaba de recibir daño o curacion, que es justo
        // cuando la informacion importa, y se apaga sola despues de unos
        // segundos sin nuevos impactos.
        // El poseido NO es una excepcion: su vida ya vive en el HUD fijo
        // (UI.PlayerHealthView, que PlayerInputDriver refresca cada frame
        // mientras se esta a pie), y en primera persona su barra flotante
        // queda arriba y detras de la camara -- que vive en su EyeAnchor --
        // asi que nunca entraria en cuadro aunque se dejara prendida.
        const float VisibleSeconds = 3.5f;
        float hideAt = -1f;
        IDisposable damageSub;
        // La curacion tiene su propia suscripcion en vez de refrescar hideAt
        // desde LateUpdate: alli la barra oculta corta antes por el early
        // return, con lo que curar a alguien apagado no se veia nunca.
        IDisposable healSub;
        Soldier owner;

        void OnEnable()
        {
            Bootstrap();
            damageSub?.Dispose();
            damageSub = SP.Core.EventBus.Instance.Subscribe<SP.Core.DamageTakenEvent>(OnAnyDamage);
            healSub?.Dispose();
            healSub = SP.Core.EventBus.Instance.Subscribe<SP.Core.HealedEvent>(OnAnyHeal);
        }

        void OnDisable()
        {
            damageSub?.Dispose();
            healSub?.Dispose();
        }

        // La guarda de Application.isPlaying es la misma que tiene
        // CubeFxReactor.OnDamage: HeadlessTestRunner corre en Edit mode y
        // publica daño y curacion de verdad, y nada de esto debe encenderse
        // ahi (no hay frame que dibujar y Time.time no significa nada).
        void OnAnyDamage(SP.Core.DamageTakenEvent evt)
        {
            if (!Application.isPlaying) return;
            if (owner == null || evt.TargetId != owner.Id) return;
            hideAt = Time.time + VisibleSeconds;
        }

        void OnAnyHeal(SP.Core.HealedEvent evt)
        {
            if (!Application.isPlaying) return;
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
