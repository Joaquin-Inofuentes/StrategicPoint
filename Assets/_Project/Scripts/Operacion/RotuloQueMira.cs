using UnityEngine;
using SP.Core;

namespace SP.Operacion
{
    // Rotulo de texto en el mundo (TextMesh) que gira solo sobre Y para leerse siempre del lado de la camara: el del generador se veia
    // espejado al acercarse por el lado contrario al que se habia orientado.
    [ExecuteAlways]
    public class RotuloQueMira : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return;
            var d = transform.position - cam.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
    }
}
