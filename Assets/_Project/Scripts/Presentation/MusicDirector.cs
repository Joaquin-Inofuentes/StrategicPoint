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

        // Cruce ASIMETRICO: entrar en combate pega un golpe casi inmediato
        // (calma -> accion en bien menos de 0.5 s, para que se sienta el
        // impacto), pero salir de combate relaja el ritmo con un crossfade
        // largo de varios segundos (accion -> calma, 2-4 s). GananciaLucha
        // sigue siendo una rampa LINEAL en el tiempo (0..1) -- eso es lo que
        // valida la suite headless (sube >0.8 en <2s, baja <0.05 en <3s) --
        // la curva de easing no-lineal se aplica solo al mapear esa rampa a
        // VOLUMEN real en AplicarAAudioFuentes, mas abajo.
        //
        // Subida: 1/TasaDeSubida = 0.8/3.5 = 0.229 s hasta el umbral 0.8 (<0.5s).
        const float TasaDeSubida = 3.5f;
        // Bajada: 0.95/TasaDeBajada = 0.95/0.35 = 2.71 s hasta el umbral 0.05
        // (dentro de la ventana 2-4 s pedida, y por debajo del limite de 3 s
        // que exige el test de Fases8a12).
        const float TasaDeBajada = 0.35f;

        // Curvas de easing (no lineales) para el VOLUMEN, evaluadas con el
        // progreso lineal de GananciaLucha como parametro 0..1:
        //  - CurvaImpacto: ease-out agresivo (arranca con pendiente fuerte y
        //    se aplana cerca de 1) -- la accion "pega" de entrada.
        //  - CurvaSuave: ease-in-out clasico (smoothstep) -- transicion pareja
        //    y natural para relajar hacia la calma.
        static readonly AnimationCurve CurvaImpacto = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f),
            new Keyframe(1f, 1f, 0.2f, 0f));
        static readonly AnimationCurve CurvaSuave = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        const float VolumenBase = 0.35f;

        public static float GananciaLucha { get; private set; }

        // 1 = normal; la mision la baja para dejar sola a la musica tensa del final.
        public static float Atenuacion { get; set; } = 1f;

        static AudioSource estrategiaSource;
        static AudioSource luchaSource;
        static bool fuentesListas;
        static bool combateUltimo;

        // ------------------------------------------------------------------
        // WP2 (#093): musica de victoria y salvataje con fade largo.
        // El himno se sintetiza en un hilo (PrecargarVictoria) y suena por una fuente 2D propia con ignoreListenerPause. Los
        // fades corren con Time.unscaledDeltaTime desde un runner propio: la victoria pone timeScale 0, y ahi ni este Tick (lo
        // llama WorldSimulationDriver.Step) ni nada escalado avanza.
        // ------------------------------------------------------------------
        public static bool VictoriaSonando { get; private set; }
        public static bool VictoriaLista => himnoClip != null;
        public static bool VictoriaGenerando => himnoClip == null && himnoTarea != null && !himnoTarea.IsCompleted;
        // Volumen real de la fuente del himno (ya multiplicado por el canal Ambient del usuario).
        public static float VolumenVictoria => victoriaSource != null ? victoriaSource.volume : 0f;
        public static bool VictoriaReproduciendo => victoriaSource != null && victoriaSource.isPlaying;
        // Mayor volumen entre los dos loops de combate/calma (para auditar que quedan en silencio).
        public static float VolumenDeLoops => Mathf.Max(estrategiaSource != null ? estrategiaSource.volume : 0f, luchaSource != null ? luchaSource.volume : 0f);
        // 1 = normal; baja a 0 durante la victoria (loops de combate; el ambiente baja a 0.25).
        public static float FactorOtros { get; private set; } = 1f;
        public const float VolumenDeVictoria = 0.95f;

        static AudioClip himnoClip;
        static System.Threading.Tasks.Task<float[]> himnoTarea;
        static AudioSource victoriaSource;
        static MusicaVictoriaRunner runnerVictoria;
        static float victoriaT, victoriaFadeIn = 6f, otrosT, otrosFade = 4f;

        // Empieza a calcular el himno en un hilo de fondo (idempotente). Llamarlo unos segundos antes de TocarVictoria.
        public static void PrecargarVictoria()
        {
            if (himnoClip != null || himnoTarea != null) return;
            himnoTarea = System.Threading.Tasks.Task.Run(() => SfxSintetico.HimnoEpicoDatos());   // #129: himno v2 (estereo, ~38 s)
        }

        public static void TocarVictoria(float fadeIn = 6f, float fadeOtros = 4f)
        {
            if (!Application.isPlaying) return;
            PrecargarVictoria();
            victoriaFadeIn = Mathf.Max(0.01f, fadeIn);
            otrosFade = Mathf.Max(0.01f, fadeOtros);
            victoriaT = 0f; otrosT = 0f;
            VictoriaSonando = true;
            if (victoriaSource != null) { victoriaSource.Stop(); victoriaSource.volume = 0f; victoriaSource.Play(); }
            if (runnerVictoria == null)
            {
                var go = new GameObject("MusicaVictoria");
                runnerVictoria = go.AddComponent<MusicaVictoriaRunner>();
            }
        }

        // Corta el himno y devuelve el resto de la musica a la normalidad (reintentar, tests, cambio de escena).
        public static void DetenerVictoria()
        {
            VictoriaSonando = false;
            FactorOtros = 1f;
            if (victoriaSource != null) { victoriaSource.Stop(); victoriaSource.volume = 0f; }
        }

        // Un paso del fade con tiempo NO escalado (lo llama MusicaVictoriaRunner.Update).
        internal static void PasoVictoria(float dtReal)
        {
            if (!VictoriaSonando) return;
            if (victoriaSource == null)
            {
                if (himnoClip == null)
                {
                    if (himnoTarea == null || !himnoTarea.IsCompleted) { otrosT += dtReal; AplicarOtros(); return; }
                    if (himnoTarea.IsFaulted) { Debug.LogError("[MusicDirector] fallo el himno de victoria: " + himnoTarea.Exception?.GetBaseException().Message); VictoriaSonando = false; return; }
                    himnoClip = SfxSintetico.ClipDelHimnoEpico(himnoTarea.Result);
                }
                var go = new GameObject("Victoria");
                go.transform.SetParent(runnerVictoria != null ? runnerVictoria.transform : null, false);
                victoriaSource = go.AddComponent<AudioSource>();
                victoriaSource.clip = himnoClip;
                victoriaSource.loop = false;
                victoriaSource.spatialBlend = 0f;   // musica: 2D, no posicional
                victoriaSource.playOnAwake = false;
                victoriaSource.ignoreListenerPause = true;
                victoriaSource.volume = 0f;
                victoriaSource.Play();
            }
            victoriaT += dtReal;
            otrosT += dtReal;
            float p = Mathf.Clamp01(victoriaT / victoriaFadeIn);
            float suave = p * p * (3f - 2f * p);
            victoriaSource.volume = suave * VolumenDeVictoria * AudioDirector.GainFor(SfxChannel.Ambient);
            AplicarOtros();
        }

        static void AplicarOtros()
        {
            FactorOtros = 1f - Mathf.Clamp01(otrosT / otrosFade);
            if (Application.isPlaying) AplicarAAudioFuentes(combateUltimo);
        }

        internal static void AlDestruirRunner()
        {
            VictoriaSonando = false;
            FactorOtros = 1f;
            victoriaSource = null;
            runnerVictoria = null;
        }

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload:
        // sin este reset, GananciaLucha arrastraria el valor de la sesion
        // de Play ANTERIOR (mismo patron que Projectile.ResetActiveInstancesOnLoad).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            GananciaLucha = 0f;
            CombateForzado = false;
            Atenuacion = 1f;
            fuentesListas = false;
            estrategiaSource = null;
            luchaSource = null;
            VictoriaSonando = false;
            FactorOtros = 1f;
            himnoClip = null;
            himnoTarea = null;
            victoriaSource = null;
            runnerVictoria = null;
        }

        // Se llama desde WorldSimulationDriver.Step, el mismo unico camino
        // de simulacion que ya usan PedidoDeCuracion/RescateAutomatico/
        // ObstacleMarker: asi la suite headless (que avanza el tiempo a
        // mano) ejercita exactamente esto.
        public static void Tick(float dt)
        {
            bool combate = HayCombateCerca();
            float objetivo = combate ? 1f : 0f;
            float tasa = combate ? TasaDeSubida : TasaDeBajada;
            GananciaLucha = Mathf.MoveTowards(GananciaLucha, objetivo, tasa * dt);

            if (Application.isPlaying) AplicarAAudioFuentes(combate);
        }

        // "Hay combate" = algun soldado vivo en Attack o Chase a menos de
        // AlcanceDeCombate metros de la camara (que es el mejor proxy de
        // "donde esta el jugador" sin acoplarse a FPS/RTS/vehiculo).
        // #127: la cobertura de la extraccion pide la musica de combate al maximo aunque no haya nadie en Attack/Chase cerca de la camara.
        public static bool CombateForzado;

        static bool HayCombateCerca()
        {
            if (CombateForzado) return true;
            var cam = SP.Core.CamaraPrincipal.Actual;
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

        static void AplicarAAudioFuentes(bool combate)
        {
            AsegurarFuentes();
            combateUltimo = combate;
            // Reusa el canal Ambient de AudioDirector como volumen maestro
            // de la musica: si el dia de mañana hay un slider de ambiente,
            // la musica lo respeta gratis, sin que este director tenga que
            // saber nada de PlayerPrefs.
            float maestro = AudioDirector.GainFor(SfxChannel.Ambient) * VolumenBase * Atenuacion * FactorOtros;

            // El progreso lineal (GananciaLucha) solo decide LA RAMPA en el
            // tiempo (por eso el test de Fases8a12 lo puede medir en
            // segundos). El VOLUMEN real que sale por los AudioSource pasa
            // por una curva de easing -- impacto rapido al entrar en
            // combate, suave al salir -- para que el fade no se sienta
            // lineal/brusco. Sin AudioMixer en el proyecto (no hay .mixer
            // bajo Assets/_Project todavia), se modula AudioSource.volume
            // directamente con el peso ya curveado.
            var curva = combate ? CurvaImpacto : CurvaSuave;
            float pesoLucha = Mathf.Clamp01(curva.Evaluate(GananciaLucha));
            float pesoEstrategia = 1f - pesoLucha;

            if (estrategiaSource != null) estrategiaSource.volume = pesoEstrategia * maestro;
            if (luchaSource != null) luchaSource.volume = pesoLucha * maestro;
        }

        static void AsegurarFuentes()
        {
            if (fuentesListas && estrategiaSource != null && luchaSource != null) return;
            fuentesListas = true;

            var root = new GameObject("MusicDirector");
            estrategiaSource = CrearFuenteLoop(root.transform, "Estrategia", CargarORespaldo(GenerarLoopEstrategia, "SA_Calma", "Calm"));
            luchaSource = CrearFuenteLoop(root.transform, "Lucha", CargarORespaldo(GenerarLoopLucha, "SA_Accion", "Action"));
        }

        // Pedido explicito: musica real de fondo en vez de los lechos
        // procedurales. Prueba cada nombre de archivo en orden (la pista
        // definitiva primero, "SA_Calma"/"SA_Accion"; el placeholder viejo
        // "Calm"/"Action" como segunda red de seguridad) y recien si
        // Resources.Load no encuentra ninguno (p.ej. en el editor de tests
        // headless, que no lo necesita) cae al generador procedural -- asi
        // la suite headless sigue viendo el cruce de ganancia funcionar
        // igual, tenga o no clip real cargado.
        static AudioClip CargarORespaldo(System.Func<AudioClip> respaldo, params string[] nombresArchivo)
        {
            foreach (var nombre in nombresArchivo)
            {
                var clip = SP.Core.RecursosCache.Cargar<AudioClip>("Audio/Music/" + nombre);
                if (clip != null) return clip;
            }
            return respaldo();
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

    // Avanza el fade del himno de victoria con tiempo no escalado (ver MusicDirector.TocarVictoria).
    public class MusicaVictoriaRunner : MonoBehaviour
    {
        void Update() { MusicDirector.PasoVictoria(Time.unscaledDeltaTime); }
        void OnDestroy() { MusicDirector.AlDestruirRunner(); }
    }
}
