using UnityEngine;

namespace SP.Presentation
{
    // Baliza que late: la luz (y la malla emisiva, si tiene) suben y bajan de forma pareja. Sirve para el refugio del
    // rehen (cian, lento), las luces de aterrizaje del helipuerto (azul) y las luces de aviso rojas de las torres.
    [DisallowMultipleComponent]
    public class BalizaPulsante : MonoBehaviour
    {
        [SerializeField] Light luz;
        [SerializeField] Renderer malla;
        [SerializeField, Min(0.05f)] float ciclosPorSegundo = 0.6f;
        [SerializeField, Range(0f, 1f)] float minimo = 0.25f;
        [SerializeField] float fase;
        [SerializeField, ColorUsage(false, true)] Color emision = Color.white;

        float intensidadBase;
        MaterialPropertyBlock bloque;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public void Configurar(Light l, Renderer r, Color emisionHdr, float ciclos, float min, float faseInicial)
        {
            luz = l; malla = r; emision = emisionHdr; ciclosPorSegundo = ciclos; minimo = min; fase = faseInicial;
            if (luz != null) intensidadBase = luz.intensity;
        }

        void Awake()
        {
            if (luz == null) luz = GetComponent<Light>();
            if (luz != null) intensidadBase = luz.intensity;
            bloque = new MaterialPropertyBlock();
        }

        void Update()
        {
            float s = 0.5f + 0.5f * Mathf.Sin((Time.time * ciclosPorSegundo + fase) * Mathf.PI * 2f);
            float k = Mathf.Lerp(minimo, 1f, s * s);
            if (luz != null && luz.enabled) luz.intensity = intensidadBase * k;
            if (malla != null)
            {
                if (bloque == null) bloque = new MaterialPropertyBlock();
                malla.GetPropertyBlock(bloque);
                bloque.SetColor(EmissionColor, emision * k);
                malla.SetPropertyBlock(bloque);
            }
        }
    }
}
