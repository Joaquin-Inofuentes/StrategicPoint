using System;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Marca sobre la cabeza del enemigo que dice si ya te detecto o no.
    // Sin esto no habia forma de saber, antes de que empiece a disparar,
    // si un enemigo estaba patrullando tranquilo o ya venia directo hacia
    // vos -- la decision de atacar o rodear depende de esa diferencia.
    public class EnemyAlertIndicatorView : MonoBehaviour
    {
        Soldier soldier;
        Renderer markerRenderer;
        IDisposable sub;

        static readonly Color AlertColor = new Color(0.95f, 0.2f, 0.15f);
        static readonly Color UnawareColor = new Color(0.6f, 0.6f, 0.65f, 0.5f);

        void OnEnable()
        {
            if (soldier == null) soldier = GetComponent<Soldier>();
            if (soldier == null || soldier.Team != TeamId.Enemy) { enabled = false; return; }

            if (markerRenderer == null) BuildMarker();

            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<AiStateChangedEvent>(OnStateChanged);
        }

        void OnDisable() => sub?.Dispose();

        void BuildMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AlertIndicator";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            go.transform.localScale = Vector3.one * 0.18f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            markerRenderer = go.GetComponent<MeshRenderer>();
            markerRenderer.sharedMaterial = new Material(shader) { color = UnawareColor };
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        bool alerted;

        void OnStateChanged(AiStateChangedEvent evt)
        {
            if (soldier == null || evt.ActorId != soldier.Id || markerRenderer == null) return;
            alerted = evt.NewState == "Chase" || evt.NewState == "Attack" || evt.NewState == "MovingToAttackOrder";
            markerRenderer.sharedMaterial.color = alerted ? AlertColor : UnawareColor;
        }

        // Por debajo de este umbral la marca parpadea: es la respuesta a la
        // decision mas frecuente del combate -- gastar otra bala en este o
        // pasar al siguiente. Antes no habia forma de saberlo sin apuntarle
        // y leer el panel.
        public const float LowHealthFraction = 0.3f;
        static readonly Color DyingColor = new Color(1f, 0.95f, 0.3f);
        public bool IsBlinkingLowHealth { get; private set; }

        void Update()
        {
            if (markerRenderer == null || soldier == null || soldier.Health == null) return;
            float frac = soldier.Health.MaxHealth > 0 ? (float)soldier.Health.Current / soldier.Health.MaxHealth : 1f;
            bool dying = soldier.Health.IsAlive && frac <= LowHealthFraction;
            if (IsBlinkingLowHealth != dying)
            {
                IsBlinkingLowHealth = dying;
                // Al salir del parpadeo hay que devolver el color del
                // estado, o queda pegado en el ultimo tono del parpadeo.
                if (!dying) markerRenderer.sharedMaterial.color = alerted ? AlertColor : UnawareColor;
            }
            if (!dying) return;

            float k = (Mathf.Sin(Time.time * 12f) + 1f) * 0.5f;
            markerRenderer.sharedMaterial.color = Color.Lerp(alerted ? AlertColor : UnawareColor, DyingColor, k);
        }
    }
}
