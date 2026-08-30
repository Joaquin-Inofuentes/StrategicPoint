using UnityEngine;
using SP.Actors;

namespace SP.Presentation
{
    // Anillo (cilindro chato) que sigue a un soldado seleccionado y pulsa
    // -- se achica y se agranda en loop con un lerp simple -- para marcar
    // a simple vista quién está elegido en la vista RTS.
    public class SelectionRingFx : MonoBehaviour
    {
        public Transform Target;
        [SerializeField] float baseRadius = 0.75f;
        [SerializeField] float pulseAmount = 0.18f;
        [SerializeField] float pulseSpeed = 2.2f;
        [SerializeField] float groundHeight = 0.03f;

        // Si se trackea un soldado (no un vehiculo), el anillo deja de
        // ser todo del mismo amarillo fijo: se tiñe segun su vida, los
        // mismos umbrales que el resto del HUD. Antes, seleccionar a un
        // herido y a uno sano se veia identico -- habia que abrir el
        // roster para saber a cual convenia retirar.
        [SerializeField] Soldier trackedSoldier;
        MeshRenderer ringRenderer;

        public void TrackHealth(Soldier soldier)
        {
            trackedSoldier = soldier;
            if (ringRenderer == null) ringRenderer = GetComponent<MeshRenderer>();
        }

        public static SelectionRingFx Spawn(Transform target, Color color, float radius = 0.75f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SelectionRing";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            rend.sharedMaterial = mat;

            var fx = go.AddComponent<SelectionRingFx>();
            fx.Target = target;
            fx.baseRadius = radius;
            return fx;
        }

        void LateUpdate()
        {
            if (Target == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            // A nivel del piso (no a la altura del centro del soldado, que
            // lo dejaba envolviendo la mitad del cubo en vez de estar abajo).
            transform.position = new Vector3(Target.position.x, groundHeight, Target.position.z);

            float k = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float radius = (baseRadius + pulseAmount * k) * 2f; // cylinder scale = diámetro
            transform.localScale = new Vector3(radius, 0.03f, radius);

            if (trackedSoldier != null && trackedSoldier.Health != null)
            {
                if (ringRenderer == null) ringRenderer = GetComponent<MeshRenderer>();
                if (ringRenderer != null && trackedSoldier.Health.MaxHealth > 0)
                {
                    float frac = (float)trackedSoldier.Health.Current / trackedSoldier.Health.MaxHealth;
                    Color c = frac > 0.6f ? new Color(0.35f, 0.85f, 0.4f)
                        : frac > 0.25f ? new Color(0.95f, 0.8f, 0.25f)
                        : new Color(0.95f, 0.25f, 0.2f);
                    ringRenderer.sharedMaterial.color = c;
                }
            }
        }
    }
}
