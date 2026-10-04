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
        bool chirridoReal;
        public bool UsaGrabacionReal => chirridoReal;

        void Awake()
        {
            motor = GetComponentInParent<VehicleMotor>();
            engineSource = GetComponent<AudioSource>();
            // Pedido: "sonido real de movimiento de tanque". Si hay grabacion real de orugas en Resources/Audio/Sfx/
            // OrugasChirrido/ (CC0, ver CREDITS.txt) reemplaza al diente de sierra sintetico.
            var reales = Resources.LoadAll<AudioClip>("Audio/Sfx/OrugasChirrido");
            chirridoReal = reales != null && reales.Length > 0 && motor != null;
            engineSource.clip = chirridoReal ? reales[0] : GetOrBuildLoopClip();
            engineSource.loop = true;
            engineSource.rolloffMode = AudioRolloffMode.Logarithmic;
            engineSource.minDistance = 6f;
            engineSource.maxDistance = 120f;
            engineSource.playOnAwake = false;
            engineSource.spatialBlend = 1f;
            engineSource.volume = 0.25f; // en ralenti, con el vehiculo quieto
            engineSource.pitch = 0.7f;
        }

        void OnEnable()
        {
            if (motor == null) motor = GetComponentInParent<VehicleMotor>();
            if (engineSource == null) engineSource = GetComponent<AudioSource>();
            if (engineSource != null && !engineSource.isPlaying) engineSource.Play();
        }

        Vehicle vehicle;

        void Update()
        {
            if (motor == null || engineSource == null) return;

            // Vehicle.OnDestroyed apaga motor, torreta e IA pero no sabe de
            // este componente: CurrentSpeed quedaba congelado en su ultimo
            // valor, asi que un tanque destruido a plena marcha se quedaba
            // con el motor sonando agudo en loop eterno sobre la carcasa.
            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();
            if (vehicle != null && vehicle.IsDestroyed)
            {
                if (engineSource.isPlaying) engineSource.Stop();
                return;
            }

            float speedFrac = Mathf.Clamp01(Mathf.Abs(motor.CurrentSpeed) / Mathf.Max(0.01f, motor.MaxSpeed));
            if (chirridoReal)
            {
                // Con la grabacion real, esta capa es SOLO el chirrido metalico de las orugas: callada con el tanque quieto
                // y creciendo con la velocidad. El motor + orugas (T-26 real) lo pone SonidosDeOperacion.
                engineSource.pitch = Mathf.Lerp(0.8f, 1.25f, speedFrac) * (motor.CurrentSpeed < -0.1f ? 0.9f : 1f);
                engineSource.volume = speedFrac < 0.03f ? 0f : Mathf.Lerp(0.15f, 0.75f, speedFrac) * SP.Presentation.AudioDirector.GainFor(SP.Presentation.SfxChannel.Sfx);
                return;
            }
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
