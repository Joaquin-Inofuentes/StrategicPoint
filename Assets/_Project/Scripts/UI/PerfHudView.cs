using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Item 235: panel de diagnostico en vivo.
    //
    // El backlog lo ponia ULTIMO, con dependencias en 229 y 234. La
    // dependencia esta al reves: esta es la herramienta que permite
    // VERIFICAR al resto del bloque de performance, asi que no depende de
    // ellos -- ellos dependen de ella. Sin esto, cualquier afirmacion sobre
    // rendimiento es una opinion.
    //
    // Es de SOLO LECTURA: no escribe ningun estado del juego. Apagado por
    // defecto y sin ningun Update activo mientras esta apagado, para que
    // medir no altere lo medido.
    public class PerfHudView : MonoBehaviour
    {
        // [SerializeField] y no privado a secas: se asigna al construir la
        // escena y un campo sin serializar no sobrevive el domain reload.
        [SerializeField] Text label;

        public bool Visible { get; private set; }

        // Media movil + percentil 95. El promedio simple no sirve: un solo
        // pico de GC lo arruina y esconde justamente lo que interesa.
        const int SampleCount = 120;
        readonly float[] samples = new float[SampleCount];
        int sampleIndex;
        int samplesFilled;

        readonly StringBuilder sb = new StringBuilder(256);

        // Refresco del TEXTO, no del muestreo. Reconstruir el string cada
        // frame seria basura para el GC dentro del propio medidor.
        const float TextRefreshInterval = 0.25f;
        float textTimer;

        void OnEnable()
        {
            if (label == null)
            {
                var t = transform.Find("Text");
                if (t != null) label = t.GetComponent<Text>();
            }
            ApplyVisibility();
        }

        public void Bind(Text text)
        {
            label = text;
            ApplyVisibility();
        }

        public void Toggle()
        {
            Visible = !Visible;
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            // Se apaga el HIJO y no este GameObject: apagar el propio GO
            // mataria el componente y no habria forma de volver a prenderlo.
            if (label != null) label.gameObject.SetActive(Visible);
        }

        void Update()
        {
            if (!Visible || label == null) return;

            samples[sampleIndex] = Time.unscaledDeltaTime * 1000f;
            sampleIndex = (sampleIndex + 1) % SampleCount;
            if (samplesFilled < SampleCount) samplesFilled++;

            textTimer -= Time.unscaledDeltaTime;
            if (textTimer > 0f) return;
            textTimer = TextRefreshInterval;

            label.text = BuildReport();
        }

        string BuildReport()
        {
            float median, p95;
            ComputeStats(out median, out p95);

            sb.Length = 0;
            sb.Append("ms/frame  mediana ").Append(median.ToString("0.00"))
              .Append("   p95 ").Append(p95.ToString("0.00")).Append('\n');
            sb.Append("actores ").Append(SP.Core.ActorRegistry.All.Count);
            sb.Append("   vehiculos ").Append(SP.Core.WorldSystemsRegistry.Vehicles.Count);
            sb.Append("   obstaculos ").Append(SP.Core.WorldSystemsRegistry.Obstacles.Count).Append('\n');
            sb.Append("proyectiles en vuelo ").Append(SP.Combat.Projectile.ActiveInstances.Count).Append('\n');

            var audio = SP.Presentation.AudioDirector.Instance;
            if (audio != null)
            {
                sb.Append("voces ").Append(audio.ActiveVoiceCount)
                  .Append(" activas / ").Append(audio.FreeVoiceCount).Append(" libres")
                  .Append("   descartadas ").Append(audio.DroppedCount).Append('\n');
            }

            return sb.ToString();
        }

        // Mediana y p95 sobre una copia ordenada de la ventana. Es O(n log n)
        // sobre 120 elementos y solo 4 veces por segundo: despreciable, y
        // vale la pena frente a un promedio que miente.
        void ComputeStats(out float median, out float p95)
        {
            median = 0f;
            p95 = 0f;
            if (samplesFilled == 0) return;

            var copy = new float[samplesFilled];
            System.Array.Copy(samples, copy, samplesFilled);
            System.Array.Sort(copy);

            median = copy[samplesFilled / 2];
            int idx95 = Mathf.Clamp(Mathf.CeilToInt(samplesFilled * 0.95f) - 1, 0, samplesFilled - 1);
            p95 = copy[idx95];
        }

        // Para verificar sin depender de leer la pantalla.
        public float MedianMs { get { float m, p; ComputeStats(out m, out p); return m; } }
        public float P95Ms { get { float m, p; ComputeStats(out m, out p); return p; } }
    }
}
