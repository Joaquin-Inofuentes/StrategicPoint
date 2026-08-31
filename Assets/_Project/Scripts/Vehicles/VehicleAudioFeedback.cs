using UnityEngine;

namespace SP.Vehicles
{
    // El vehiculo era completamente mudo: ninguna realimentacion sonora
    // de que estabas conduciendo ni de a que velocidad ibas. Un tono
    // continuo cuyo pitch y volumen siguen la velocidad real, generado
    // por codigo (mismo enfoque que GenericSfx, sin depender de un clip
    // importado).
    [RequireComponent(typeof(AudioSource))]
    public class VehicleAudioFeedback : MonoBehaviour
    {
        AudioSource engineSource;
        VehicleMotor motor;
        static AudioClip cachedLoopClip;

        void Awake()
        {
            motor = GetComponent<VehicleMotor>();
            engineSource = GetComponent<AudioSource>();
            engineSource.clip = GetOrBuildLoopClip();
            engineSource.loop = true;
            engineSource.playOnAwake = false;
            engineSource.spatialBlend = 1f;
            engineSource.volume = 0.25f; // en ralenti, con el vehiculo quieto
            engineSource.pitch = 0.7f;
        }

        void OnEnable()
        {
            if (motor == null) motor = GetComponent<VehicleMotor>();
            if (engineSource == null) engineSource = GetComponent<AudioSource>();
            if (engineSource != null && !engineSource.isPlaying) engineSource.Play();
        }

        void Update()
        {
            if (motor == null || engineSource == null) return;
            float speedFrac = Mathf.Clamp01(Mathf.Abs(motor.CurrentSpeed) / Mathf.Max(0.01f, motor.MaxSpeed));
            // Piso audible en ralenti (0.7) para que el motor nunca quede
            // en silencio total con el vehiculo detenido -- eso se leeria
            // como "motor apagado", no como "parado con el motor prendido".
            engineSource.pitch = Mathf.Lerp(0.7f, 1.6f, speedFrac);
            engineSource.volume = Mathf.Lerp(0.25f, 0.6f, speedFrac);

            // Marcha atras: tono mas grave que ir para adelante a la
            // misma velocidad absoluta, para que se note el cambio de
            // sentido sin tener que mirar el HUD.
            if (motor.CurrentSpeed < -0.1f) engineSource.pitch *= 0.75f;
        }

        // Onda de diente de sierra con un poco de ruido: un tono puro
        // (seno) sonaba demasiado "silbido", nada parecido a un motor.
        static AudioClip GetOrBuildLoopClip()
        {
            if (cachedLoopClip != null) return cachedLoopClip;
            const int sampleRate = 44100;
            const float duration = 0.5f; // se loopea, no hace falta mas
            const float freq = 90f;
            int sampleCount = (int)(duration * sampleRate);
            var samples = new float[sampleCount];
            var rng = new System.Random(99);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float phase = (t * freq) % 1f;
                float saw = phase * 2f - 1f;
                float noise = ((float)rng.NextDouble() - 0.5f) * 0.15f;
                samples[i] = Mathf.Clamp(saw * 0.5f + noise, -1f, 1f);
            }
            cachedLoopClip = AudioClip.Create("VehicleEngineLoop", sampleCount, 1, sampleRate, false);
            cachedLoopClip.SetData(samples, 0);
            return cachedLoopClip;
        }
    }
}
