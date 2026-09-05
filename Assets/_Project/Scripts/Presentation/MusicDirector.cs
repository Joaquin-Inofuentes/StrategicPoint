using UnityEngine;
using SP.Core;
using SP.Ai;

namespace SP.Presentation
{
    // G4: dos temas que se cruzan solos segun si hay combate cerca de la
    // camara. Este prototipo no tiene pistas de musica importadas -- todo
    // el audio del proyecto es procedural (ver GenericSfx) -- asi que las
    // dos "canciones" son dos lechos ambiente generados por codigo con
    // caracter distinto (Estrategia: grave y con tremolo lento. Lucha: mas
    // agudo y con tremolo rapido) en vez de silencio con una etiqueta.
    //
    // La GANANCIA es estatica y pura a proposito (igual que
    // AudioDirector.Attenuation/SelectVictim): asi la suite headless, que
    // corre en Edit mode sin audio real, puede verificar el CRUCE sin
    // depender de Application.isPlaying. Solo la reproduccion de verdad
    // (AplicarAAudioFuentes) esta gateada por eso.
    public static class MusicDirector
    {
        // Hasta que distancia de la camara cuenta un soldado en combate.
        public const float AlcanceDeCombate = 15f;

        // Cuanto sube/baja la ganancia por segundo. A esta tasa, de 0 a
        // 0.8 tarda 0.8/1.5 = 0.53 s -- bien debajo del "menos de 2 s"
        // que pide el test, y el cruce sigue siendo una rampa, no un corte.
        const float TasaDeCruce = 1.5f;

        const float VolumenBase = 0.35f;

        public static float GananciaLucha { get; private set; }

        static AudioSource estrategiaSource;
        static AudioSource luchaSource;
        static bool fuentesListas;

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload:
        // sin este reset, GananciaLucha arrastraria el valor de la sesion
        // de Play ANTERIOR (mismo patron que Projectile.ResetActiveInstancesOnLoad).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            GananciaLucha = 0f;
            fuentesListas = false;
            estrategiaSource = null;
            luchaSource = null;
        }

        // Se llama desde WorldSimulationDriver.Step, el mismo unico camino
        // de simulacion que ya usan PedidoDeCuracion/RescateAutomatico/
        // ObstacleMarker: asi la suite headless (que avanza el tiempo a
        // mano) ejercita exactamente esto.
        public static void Tick(float dt)
        {
            float objetivo = HayCombateCerca() ? 1f : 0f;
            GananciaLucha = Mathf.MoveTowards(GananciaLucha, objetivo, TasaDeCruce * dt);

            if (Application.isPlaying) AplicarAAudioFuentes();
        }

        // "Hay combate" = algun soldado vivo en Attack o Chase a menos de
        // AlcanceDeCombate metros de la camara (que es el mejor proxy de
        // "donde esta el jugador" sin acoplarse a FPS/RTS/vehiculo).
        static bool HayCombateCerca()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var pos = cam.transform.position;

            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.Health.IsAlive || s.Brain == null) continue;
                if (s.Brain.State != AiState.Attack && s.Brain.State != AiState.Chase) continue;
                if (Vector3.Distance(s.transform.position, pos) <= AlcanceDeCombate) return true;
            }
            return false;
        }

        static void AplicarAAudioFuentes()
        {
            AsegurarFuentes();
            // Reusa el canal Ambient de AudioDirector como volumen maestro
            // de la musica: si el dia de mañana hay un slider de ambiente,
            // la musica lo respeta gratis, sin que este director tenga que
            // saber nada de PlayerPrefs.
            float maestro = AudioDirector.GainFor(SfxChannel.Ambient) * VolumenBase;
            if (estrategiaSource != null) estrategiaSource.volume = (1f - GananciaLucha) * maestro;
            if (luchaSource != null) luchaSource.volume = GananciaLucha * maestro;
        }

        static void AsegurarFuentes()
        {
            if (fuentesListas && estrategiaSource != null && luchaSource != null) return;
            fuentesListas = true;

            var root = new GameObject("MusicDirector");
            estrategiaSource = CrearFuenteLoop(root.transform, "Estrategia", GenerarLoopEstrategia());
            luchaSource = CrearFuenteLoop(root.transform, "Lucha", GenerarLoopLucha());
        }

        static AudioSource CrearFuenteLoop(Transform padre, string nombre, AudioClip clip)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f; // musica de fondo: 2D, no posicional
            src.playOnAwake = false;
            src.volume = 0f;
            src.Play();
            return src;
        }

        // Acorde sostenido con tremolo (amplitud modulada senoidalmente),
        // en loop de 2 segundos exactos. Todas las frecuencias (parciales
        // Y tremolo) son multiplos enteros de 0.5 Hz, asi que la onda
        // vuelve a fase cero justo al final del buffer y AudioSource.loop
        // no mete un click en el empalme.
        static AudioClip GenerarLoopEstrategia()
            => GenerarLoop(new[] { 110f, 165f, 220f }, new[] { 0.5f, 0.3f, 0.2f }, 4f, "MusicaEstrategia");

        // Lucha: parciales mas agudos y tremolo mas rapido -- el mismo
        // truco que distingue calma de tension en cualquier partitura de
        // videojuego, sin componer una melodia de verdad.
        static AudioClip GenerarLoopLucha()
            => GenerarLoop(new[] { 220f, 330f, 440f }, new[] { 0.45f, 0.3f, 0.25f }, 10f, "MusicaLucha");

        static AudioClip GenerarLoop(float[] partials, float[] weights, float tremoloHz, string name)
        {
            const int sampleRate = 44100;
            const float duration = 2f;
            int sampleCount = (int)(duration * sampleRate);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float v = 0f;
                for (int p = 0; p < partials.Length; p++)
                    v += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * weights[p];
                float tremolo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * tremoloHz * t);
                samples[i] = Mathf.Clamp(v * tremolo, -1f, 1f);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
