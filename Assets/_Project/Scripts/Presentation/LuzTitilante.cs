using UnityEngine;

using SP.Core;

namespace SP.Presentation
{
    // Luz de fuego / farol viejo: la intensidad oscila con ruido suave (nunca se apaga del todo) y, si tiene `Llama`,
    // esa malla respira en altura al ritmo de la luz. Es puro adorno de ambientacion: no toca gameplay.
    // Solo se mueve cuando la luz esta cerca de la camara (a mas de RadioVisible ni se calcula).
    [DisallowMultipleComponent]
    public class LuzTitilante : MonoBehaviour
    {
        [SerializeField] Light luz;
        [SerializeField] Transform llama;
        [SerializeField, Min(0f)] float amplitud = 0.28f;
        [SerializeField, Min(0.1f)] float velocidad = 7f;
        [SerializeField, Min(1f)] float radioVisible = 70f;

        float intensidadBase, semilla, escalaBaseY;
        Vector3 escalaBase;
        Transform camara;

        public void Configurar(Light l, Transform malla, float amp = 0.28f, float vel = 7f)
        {
            luz = l; llama = malla; amplitud = amp; velocidad = vel;
        }

        void Awake()
        {
            if (luz == null) luz = GetComponent<Light>();
            if (luz != null) intensidadBase = luz.intensity;
            semilla = Random.value * 100f;
            if (llama != null) { escalaBase = llama.localScale; escalaBaseY = escalaBase.y; }
        }

        void OnEnable() { if (luz == null) luz = GetComponent<Light>(); IluminacionTactica.Registrar(luz); }
        void OnDisable() => IluminacionTactica.Quitar(luz);

        void Update()
        {
            if (luz == null || !luz.enabled) return;
            if (camara == null) { var c = SP.Core.CamaraPrincipal.Actual; if (c != null) camara = c.transform; }
            if (camara != null && (camara.position - transform.position).sqrMagnitude > radioVisible * radioVisible) return;

            float t = Time.time * velocidad + semilla;
            // Dos octavas: el vaiven lento y el parpadeo rapido del fuego.
            float n = (Mathf.PerlinNoise(t, semilla) - 0.5f) * 2f + (Mathf.PerlinNoise(t * 3.1f, semilla + 7f) - 0.5f) * 0.6f;
            float k = 1f + n * amplitud;
            luz.intensity = intensidadBase * Mathf.Max(0.35f, k);
            if (llama != null)
            {
                float alto = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(k * 0.5f + 0.25f));
                llama.localScale = new Vector3(escalaBase.x * (2f - alto * 0.5f) * 0.75f, escalaBaseY * alto, escalaBase.z * (2f - alto * 0.5f) * 0.75f);
            }
        }
    }
}
