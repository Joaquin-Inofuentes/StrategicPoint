using UnityEngine;

namespace SP.Presentation
{
    // Circulo chato que representa a un soldado/vehiculo en el minimapa.
    // Vive en su propia capa (Minimap), que la cámara principal no ve y la
    // cámara del minimapa sí — así el minimapa no muestra el terreno ni la
    // geometría real, solo estos íconos de colores sobre fondo negro.
    public class MinimapIcon : MonoBehaviour
    {
        public Transform Target;
        [SerializeField] float height = 55f;

        void LateUpdate()
        {
            if (Target == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }
            transform.position = new Vector3(Target.position.x, height, Target.position.z);
        }

        public static MinimapIcon Spawn(Transform target, Color color, int layer, float radius = 1.6f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "MinimapIcon";
            go.layer = layer;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.localScale = new Vector3(radius, 0.2f, radius);

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = color };
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var icon = go.AddComponent<MinimapIcon>();
            icon.Target = target;
            return icon;
        }
    }
}
