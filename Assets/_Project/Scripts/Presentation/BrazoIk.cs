using UnityEngine;

namespace SP.Presentation
{
    // IK analitico de dos huesos para el brazo derecho, en el espacio del soldado (derecha, arriba, adelante). No depende de los
    // ejes propios de cada hueso, asi que vale igual para las tres variantes de cuerpo. Lo usan las animaciones procedurales
    // del golpe de cuchillo (SoldierMeleeAnim) y del lanzamiento de granada (SoldierGrenadeAnim); ambas corren en LateUpdate,
    // o sea despues de que el Animator poso el cuerpo, y solo modifican la pose de ese frame.
    public static class BrazoIk
    {
        // punto: posicion deseada de la mano en unidades del alcance del brazo, medida desde el hombro (derecha, arriba,
        // adelante). hint: hacia donde apunta el codo. peso: mezcla con la pose del Animator (0 = nada, 1 = IK completo).
        public static void Resolver(Transform raiz, Transform brazo, Transform antebrazo, Transform mano, Vector3 punto, Vector3 hint, float peso)
        {
            Vector3 hombro = brazo.position;
            float l1 = Vector3.Distance(brazo.position, antebrazo.position);
            float l2 = Vector3.Distance(antebrazo.position, mano.position);
            float alcance = l1 + l2;
            if (alcance < 0.05f) return;

            Vector3 d = (raiz.right * punto.x + raiz.up * punto.y + raiz.forward * punto.z) * alcance;
            float dist = Mathf.Clamp(d.magnitude, 0.3f * alcance, 0.98f * alcance);
            Vector3 dirN = d.sqrMagnitude > 1e-6f ? d.normalized : raiz.forward;
            Vector3 objetivo = hombro + dirN * dist;

            // Codo: interseccion de las dos esferas (largo del brazo y del antebrazo), del lado que marca el hint.
            float a = (l1 * l1 - l2 * l2 + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 hintMundo = raiz.right * hint.x + raiz.up * hint.y + raiz.forward * hint.z;
            Vector3 lado = Vector3.ProjectOnPlane(hintMundo, dirN);
            if (lado.sqrMagnitude < 1e-6f) lado = raiz.right;
            Vector3 codo = hombro + dirN * a + lado.normalized * h;

            Vector3 viejaDirBrazo = (antebrazo.position - brazo.position).normalized;
            Vector3 nuevaDirBrazo = (codo - hombro).normalized;
            var rotBrazo = Quaternion.FromToRotation(viejaDirBrazo, nuevaDirBrazo) * brazo.rotation;
            brazo.rotation = Quaternion.Slerp(brazo.rotation, rotBrazo, peso);

            Vector3 viejaDirAntebrazo = (mano.position - antebrazo.position).normalized;
            Vector3 nuevaDirAntebrazo = (objetivo - antebrazo.position).normalized;
            var rotAntebrazo = Quaternion.FromToRotation(viejaDirAntebrazo, nuevaDirAntebrazo) * antebrazo.rotation;
            antebrazo.rotation = Quaternion.Slerp(antebrazo.rotation, rotAntebrazo, peso);
        }

        public static float Suave(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
    }
}
