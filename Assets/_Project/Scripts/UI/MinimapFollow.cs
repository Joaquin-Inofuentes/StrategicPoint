using UnityEngine;

namespace SP.UI
{
    // Cámara cenital del minimapa: sigue al objetivo actual (soldado o
    // vehículo poseído) desde arriba, mirando siempre hacia abajo.
    public class MinimapFollow : MonoBehaviour
    {
        public Transform Target;
        [SerializeField] float height = 60f;

        void LateUpdate()
        {
            if (Target == null) return;
            transform.position = new Vector3(Target.position.x, height, Target.position.z);
        }
    }
}
