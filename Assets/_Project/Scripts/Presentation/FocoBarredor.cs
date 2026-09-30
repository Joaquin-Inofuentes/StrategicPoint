using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Reflector de una torre: el haz barre de un lado a otro (yaw oscilante alrededor de `yawCentro`, con el cabeceo
    // fijo hacia el suelo). El haz cuenta como luz para IluminacionTactica: quien lo cruza es visto de lejos.
    [DisallowMultipleComponent]
    public class FocoBarredor : MonoBehaviour
    {
        [SerializeField] float yawCentro;
        [SerializeField] float cabeceo = 24f;
        [SerializeField, Min(0f)] float barrido = 55f;
        [SerializeField, Min(0.01f)] float velocidad = 0.22f;   // ciclos por segundo
        [SerializeField] float fase;

        public void Configurar(float yaw, float cabeceoGrados, float barridoGrados, float ciclosPorSegundo, float faseInicial)
        {
            yawCentro = yaw; cabeceo = cabeceoGrados; barrido = barridoGrados; velocidad = ciclosPorSegundo; fase = faseInicial;
            Aplicar(0f);
        }

        void OnEnable()
        {
            Aplicar(Time.time);
            IluminacionTactica.Registrar(GetComponentInChildren<Light>());   // el haz tambien te delata
        }
        void OnDisable() => IluminacionTactica.Quitar(GetComponentInChildren<Light>());
        void Update() => Aplicar(Time.time);

        void Aplicar(float t)
        {
            float yaw = yawCentro + Mathf.Sin((t * velocidad + fase) * Mathf.PI * 2f) * barrido;
            transform.rotation = Quaternion.Euler(cabeceo, yaw, 0f);
        }
    }
}
