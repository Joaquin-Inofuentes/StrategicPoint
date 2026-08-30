using UnityEngine;

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

        public static SelectionRingFx Spawn(Transform target, Color color)
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

            transform.position = Target.position + Vector3.up * 0.03f;

            float k = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float radius = (baseRadius + pulseAmount * k) * 2f; // cylinder scale = diámetro
            transform.localScale = new Vector3(radius, 0.03f, radius);
        }
    }
}
