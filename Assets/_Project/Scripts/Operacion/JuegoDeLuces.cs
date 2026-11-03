using UnityEngine;

namespace SP.Operacion
{
    // Animacion de luces y de piezas luminosas de la ciudad (reflectores que barren el cielo, fuente con colores, antorchas, balizas, farolillos).
    // Solo mueve el transform y la Light de su propio objeto; no toca colliders ni NavMesh.
    public class JuegoDeLuces : MonoBehaviour
    {
        public enum Modo { Pulso, Ciclo, Giro, Parpadeo, Llama, Flotar, Barrido }

        public Modo modo = Modo.Pulso;
        public float velocidad = 1f, amplitud = 0.4f, fase;
        public Color colorA = Color.white, colorB = Color.white;
        public Vector3 eje = Vector3.up;

        Light luz; float intensidadBase; Vector3 posBase; Quaternion rotBase;

        void Awake()
        {
            luz = GetComponent<Light>();
            if (luz != null) intensidadBase = luz.intensity;
            posBase = transform.localPosition; rotBase = transform.localRotation;
            fase += (Mathf.Abs(posBase.x * 7.31f + posBase.z * 3.17f)) % 6.28f;
        }

        void Update()
        {
            float t = Time.time * velocidad + fase;
            switch (modo)
            {
                case Modo.Pulso:
                    if (luz != null) luz.intensity = intensidadBase * (1f + amplitud * Mathf.Sin(t * 2f));
                    break;
                case Modo.Ciclo:
                    if (luz != null)
                        luz.color = colorA == colorB ? Color.HSVToRGB((t * 0.12f) % 1f, 0.75f, 1f) : Color.Lerp(colorA, colorB, 0.5f + 0.5f * Mathf.Sin(t));
                    break;
                case Modo.Giro:
                    transform.localRotation = rotBase * Quaternion.AngleAxis(t * 35f, eje);
                    break;
                case Modo.Barrido:
                    transform.localRotation = rotBase * Quaternion.AngleAxis(Mathf.Sin(t * 0.6f) * 40f, eje);
                    break;
                case Modo.Parpadeo:
                    if (luz != null) luz.enabled = (t % 1.6f) < 0.18f;
                    break;
                case Modo.Llama:
                    if (luz != null) luz.intensity = intensidadBase * (0.75f + 0.5f * Mathf.PerlinNoise(t * 3.1f, fase));
                    break;
                case Modo.Flotar:
                    transform.localPosition = posBase + Vector3.up * Mathf.Sin(t) * amplitud;
                    break;
            }
        }
    }
}
