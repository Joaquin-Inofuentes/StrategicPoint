using UnityEngine;

namespace SP.Presentation
{
    // Canales de mezcla (item 186). Van a nivel de namespace y no anidados
    // en AudioDirector por la misma razon que SfxKind vive fuera de
    // GenericSfx: es la convencion del proyecto y evita que cada punto de
    // llamada tenga que escribir AudioDirector.SfxChannel.Sfx.
    //
    // Los miembros nuevos van SIEMPRE al final: el valor entero de cada uno
    // indexa el cache de ganancias y quedaria guardado si se serializara.
    public enum SfxChannel { Sfx, Ui, Ambient }

    // Estado de una voz del pool, separado del AudioSource A PROPOSITO: asi
    // la decision de a quien robarle la voz (AudioDirector.SelectVictim) es
    // una funcion pura sobre datos planos y se puede verificar en Edit mode,
    // sin escena, sin Play mode y sin hardware de audio -- que es como corre
    // la suite headless de este proyecto.
    public struct VoiceState
    {
        public bool Free;
        public float Audibility;
        public float ExpiresAt;
    }

    // Punto central de reproduccion de sonido (items 186, 187, 189, 191 y 193).
    //
    // POR QUE UNA CLASE Y NO UN AudioMixer: el backlog pide "mezclador con
    // canales separados", pero un AudioMixer es un ASSET que solo se puede
    // crear desde el Editor (no hay API de creacion en runtime, ni para el
    // mixer ni para sus AudioMixerGroup) y en este proyecto TODA la escena
    // se construye por codigo desde Editor/HeadlessTestRunner.cs. Asi que
    // los "grupos" se implementan como ganancias por canal aplicadas al
    // volumen de cada AudioSource antes de reproducir. El resultado
    // observable es el mismo: bajar efectos sin tocar interfaz.
    //
    // AudioListener.volume sigue siendo el MAESTRO y es propiedad de
    // PauseController (clave sp_volume) y de AudioDucking. Esta clase no lo
    // toca nunca: si lo tocara, las dos cosas se pelearian por el mismo
    // valor y el ultimo en escribir ganaria.
    //
    // POR QUE UN POOL FIJO (item 189): con cincuenta soldados disparando,
    // cada sistema reproduciendo por su cuenta genera decenas de voces por
    // segundo. El resultado no es "mas fuerte", es ruido blanco -- y ademas
    // cuesta carisimo. Aca hay 24 voces 3D y 6 voces 2D, y punto: cuando no
    // queda ninguna, el sonido nuevo pelea contra el menos audible que este
    // sonando y, si pierde, se descarta EN SILENCIO. Descartar es la
    // funcionalidad, no una falla.
    public class AudioDirector : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Item 186: canales
        // ------------------------------------------------------------------

        const string PrefSfx = "sp_volume_sfx";
        const string PrefUi = "sp_volume_ui";
        // El backlog solo nombra efectos e interfaz; ambiente sigue la misma
        // convencion para que el dia que haya un slider no haya que migrar
        // ninguna clave ya persistida.
        const string PrefAmbient = "sp_volume_ambient";

        // Cache perezoso: PlayerPrefs.GetFloat es una lectura nativa y esto
        // se consulta en CADA sonido reproducido. Es float? y no float para
        // poder distinguir "todavia no lo lei" de "el usuario lo dejo en 0".
        // Un estatico con respaldo en PlayerPrefs, ademas, es el unico
        // patron de este proyecto que sobrevive al domain reload sin que
        // nadie tenga que recablear una referencia (ver CameraFxSettings).
        static readonly float?[] gainCache = new float?[3];

        static string PrefKeyFor(SfxChannel c)
        {
            switch (c)
            {
                case SfxChannel.Ui: return PrefUi;
                case SfxChannel.Ambient: return PrefAmbient;
                default: return PrefSfx;
            }
        }

        public static float GainFor(SfxChannel c)
        {
            int i = (int)c;
            if (i < 0 || i >= gainCache.Length) return 1f;
            if (gainCache[i] == null) gainCache[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyFor(c), 1f));
            return gainCache[i].Value;
        }

        public static void SetGain(SfxChannel c, float v)
        {
            int i = (int)c;
            if (i < 0 || i >= gainCache.Length) return;
            v = Mathf.Clamp01(v);
            gainCache[i] = v;
            PlayerPrefs.SetFloat(PrefKeyFor(c), v);
        }

        // Solo para tests: obliga a releer PlayerPrefs en la proxima
        // consulta, sin arrastrar el valor cacheado de una corrida previa
        // (mismo servicio que CameraFxSettings.InvalidateCache).
        public static void InvalidateGainCache()
        {
            for (int i = 0; i < gainCache.Length; i++) gainCache[i] = null;
        }

        // ------------------------------------------------------------------
        // Items 187 y 193: atenuacion por distancia y audio posicional
        // ------------------------------------------------------------------

        public const float MinDistance = 5f;    // adentro de esto no atenua
        public const float MaxDistance = 90f;   // afuera de esto es inaudible

        public const float CutoffNear = 22000f; // tope del filtro = filtro apagado
        public const float CutoffFar = 900f;    // lejos solo llegan los graves

        // Misma curva que AudioRolloffMode.Linear con esos min/max, pero
        // como funcion PURA: el motor atenua el sonido REAL por su cuenta,
        // y esto se usa para decidir a quien vale la pena darle una voz.
        // Sin esto habria que preguntarle el volumen al motor, que no lo
        // expone, o medir el audio, que exige hardware y Play mode.
        public static float Attenuation(float distance)
        {
            if (distance <= MinDistance) return 1f;
            if (distance >= MaxDistance) return 0f;
            return 1f - (distance - MinDistance) / (MaxDistance - MinDistance);
        }

        // El aire se come los agudos antes que los graves: un disparo lejano
        // no es "el mismo disparo mas bajito", es un sonido mas opaco. Sin
        // esto la distancia se lee mal aunque el volumen sea correcto.
        //
        // Estrictamente decreciente en todo [0, MaxDistance] a proposito: una
        // meseta plana cerca del oyente haria que un test de monotonia con
        // dos muestras vecinas viera dos valores iguales. La raiz da mas
        // resolucion cerca, que es donde el oido nota la diferencia.
        public static float CutoffFor(float distance)
        {
            float k = Mathf.Clamp01(distance / MaxDistance);
            return Mathf.Lerp(CutoffNear, CutoffFar, Mathf.Sqrt(k));
        }

        // ------------------------------------------------------------------
        // Item 191: variacion de tono por instancia
        // ------------------------------------------------------------------

        public const float MinPitch = 0.92f;
        public const float MaxPitch = 1.08f;

        // El mismo clip repetido identico cincuenta veces por segundo suena
        // a artefacto digital, no a cincuenta fusiles. CUIDADO: el pitch se
        // aplica al AudioSource ANTES de Play() y NUNCA alrededor de
        // PlayOneShot -- PlayOneShot no captura el pitch, lo lee en vivo
        // cada frame, asi que devolverlo a 1 en la linea siguiente borra el
        // efecto antes de que suene una sola muestra. Ese bug ya existio en
        // este proyecto (ver GenericSfx.PlayOneShot2D y CubeFxReactor).
        public static float NextPitch() => Random.Range(MinPitch, MaxPitch);

        // ------------------------------------------------------------------
        // Item 189: limite de voces
        // ------------------------------------------------------------------

        public const int Voice3DBudget = 24;
        public const int Voice2DBudget = 6;

        // Una voz esta disponible si nunca se uso o si su clip ya termino.
        // La comparacion es ESTRICTA a proposito: con <= una voz recien
        // lanzada en el instante cero (ExpiresAt sin fijar todavia, o un
        // "ahora" de cero) se leeria como vencida y se la podria pisar en el
        // mismo frame en que empezo a sonar.
        static bool IsIdle(VoiceState v, float now) => v.Free || v.ExpiresAt < now;

        // Devuelve el indice de voz a usar, o -1 si el sonido hay que
        // descartarlo. Reglas, en orden:
        //   1) cualquier voz libre -- o ya vencida, porque Update puede no
        //      haber pasado a cosecharla todavia y una voz que ya termino de
        //      sonar no tiene por que hacerle perder el turno a nadie;
        //   2) si no hay ninguna, la de MENOR audibilidad, y solo si la
        //      nueva le gana de verdad;
        //   3) empate = se descarta. Robar en el empate no mejora la mezcla
        //      y ademas corta un sonido a mitad, que se escucha peor que no
        //      haberlo empezado.
        public static int SelectVictim(VoiceState[] voices, float newAudibility, float now)
        {
            if (voices == null || voices.Length == 0) return -1;

            for (int i = 0; i < voices.Length; i++)
                if (IsIdle(voices[i], now)) return i;

            int worst = -1;
            float worstAudibility = float.MaxValue;
            for (int i = 0; i < voices.Length; i++)
            {
                // Estricto: en empate gana el indice mas bajo, para que la
                // eleccion sea determinista y reproducible en un test.
                if (voices[i].Audibility < worstAudibility)
                {
                    worstAudibility = voices[i].Audibility;
                    worst = i;
                }
            }

            if (worst < 0) return -1;
            return newAudibility > worstAudibility ? worst : -1;
        }

        // ------------------------------------------------------------------
        // Instancia y pool
        // ------------------------------------------------------------------

        public static AudioDirector Instance { get; private set; }

        const string Voice3DPrefix = "Voice_";
        const string Voice2DPrefix = "UiVoice_";

        AudioSource[] sources3D;
        AudioLowPassFilter[] filters3D;
        VoiceState[] voices3D;

        AudioSource[] sources2D;
        VoiceState[] voices2D;

        Transform listenerTf;
        bool poolReady;

        public int DroppedCount { get; private set; }

        void OnEnable()
        {
            Instance = this;
            // Re-adquirir SIEMPRE al habilitarse: los arrays son campos
            // privados sin [SerializeField], asi que quedan en null despues
            // de un domain reload aunque los hijos sigan existiendo en la
            // escena. Ese es el bug recurrente de este proyecto.
            EnsureVoices();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            StopAllVoices();
        }

        // Idempotente: busca cada hijo por nombre antes de crearlo, asi
        // llamarlo diez veces deja exactamente 24 + 6 hijos. Es publico para
        // que el constructor de escena pueda forzar el armado en Edit mode,
        // donde OnEnable no corre (esta clase no es [ExecuteAlways]).
        public void EnsureVoices()
        {
            // Camino rapido: esto se llama en CADA sonido reproducido. El
            // flag es un campo privado, o sea que vuelve a false solo tras
            // un domain reload -- justo cuando hay que rearmar. Igual se
            // revisa la ultima fuente por si alguien destruyo un hijo.
            if (poolReady && sources3D != null && sources3D.Length == Voice3DBudget
                && sources3D[Voice3DBudget - 1] != null) return;
            poolReady = false;

            voices3D = EnsureStates(voices3D, Voice3DBudget);
            voices2D = EnsureStates(voices2D, Voice2DBudget);
            if (sources3D == null || sources3D.Length != Voice3DBudget) sources3D = new AudioSource[Voice3DBudget];
            if (filters3D == null || filters3D.Length != Voice3DBudget) filters3D = new AudioLowPassFilter[Voice3DBudget];
            if (sources2D == null || sources2D.Length != Voice2DBudget) sources2D = new AudioSource[Voice2DBudget];

            for (int i = 0; i < Voice3DBudget; i++)
            {
                if (sources3D[i] != null && filters3D[i] != null) continue;

                var tf = EnsureChild(Voice3DPrefix + i);
                var src = tf.GetComponent<AudioSource>();
                if (src == null) src = tf.gameObject.AddComponent<AudioSource>();

                src.playOnAwake = false;
                src.loop = false;
                // Item 193: panorama real. Sin spatialBlend en 1 el sonido
                // sale centrado y no se puede saber de donde te disparan,
                // que es la mitad de la informacion de un tiroteo.
                src.spatialBlend = 1f;
                // Item 187: la curva la aplica el motor, no nosotros.
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = MinDistance;
                src.maxDistance = MaxDistance;
                // Doppler en cero: desafina el clip segun la velocidad
                // relativa y nos pisaria el pitch del item 191, que es
                // justamente el que queremos controlar nosotros.
                src.dopplerLevel = 0f;

                // El filtro tiene que vivir en el MISMO GameObject que el
                // AudioSource: los filtros de audio de Unity procesan la
                // cadena de su propio objeto.
                var lp = tf.GetComponent<AudioLowPassFilter>();
                if (lp == null) lp = tf.gameObject.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = CutoffNear;

                sources3D[i] = src;
                filters3D[i] = lp;
            }

            for (int i = 0; i < Voice2DBudget; i++)
            {
                if (sources2D[i] != null) continue;

                var tf = EnsureChild(Voice2DPrefix + i);
                var src = tf.GetComponent<AudioSource>();
                if (src == null) src = tf.gameObject.AddComponent<AudioSource>();

                src.playOnAwake = false;
                src.loop = false;
                // Interfaz: 2D puro y SIN filtro. Un clic de menu que se
                // escucha mas opaco porque el jugador camino diez metros
                // seria un error, no un efecto.
                src.spatialBlend = 0f;
                src.dopplerLevel = 0f;

                sources2D[i] = src;
            }

            poolReady = true;
        }

        static VoiceState[] EnsureStates(VoiceState[] existing, int count)
        {
            if (existing != null && existing.Length == count) return existing;
            var states = new VoiceState[count];
            // default(VoiceState).Free es FALSE: sin este bucle un array
            // recien creado se leeria como "24 voces ocupadas" y
            // ActiveVoiceCount mentiria desde el primer frame.
            for (int i = 0; i < count; i++) states[i].Free = true;
            return states;
        }

        Transform EnsureChild(string childName)
        {
            var tf = transform.Find(childName);
            if (tf != null) return tf;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // ------------------------------------------------------------------
        // Reloj
        // ------------------------------------------------------------------

        // unscaledTime y NO Time.time: la pausa pone Time.timeScale en 0 y
        // la camara lenta de la ultima baja (KillFeedbackDirector) lo pone
        // en 0.25, pero el audio NO sigue al timeScale -- un clip de 0.2 s
        // sigue durando 0.2 s reales. Con Time.time una voz lanzada antes de
        // pausar no vencia NUNCA y el pool se quedaba sin voces para siempre.
        static float Now => Time.unscaledTime;

        // ------------------------------------------------------------------
        // Reproduccion
        // ------------------------------------------------------------------

        public void Play(SfxKind kind, Vector3 position, float volume, float priority)
        {
            PlayClip(GenericSfx.Get(kind), position, volume, priority);
        }

        public void Play(SfxKind kind, Vector3 position, float volume)
        {
            PlayClip(GenericSfx.Get(kind), position, volume, 1f);
        }

        // Overload por clip para los sonidos que no son un SfxKind, como el
        // timbre por arma de GenericSfx.GetWeaponShot(WeaponKind).
        public bool PlayClip(AudioClip clip, Vector3 position, float volume, float priority)
            => PlayClip(clip, position, volume, priority, SfxChannel.Sfx);

        // Un sonido posicional casi siempre es del canal de efectos, pero el
        // ambiente tambien tiene posicion (una fogata, un generador), asi
        // que el canal es un parametro y no una constante.
        public bool PlayClip(AudioClip clip, Vector3 position, float volume, float priority, SfxChannel channel)
        {
            // La suite headless corre en Edit mode: nada que reproduzca
            // audio puede ejecutarse ahi.
            if (!Application.isPlaying) return false;
            if (clip == null) return false;

            EnsureVoices();

            float finalVolume = Mathf.Clamp01(volume * GainFor(channel));
            // Canal en silencio: se sale ANTES de tocar el pool. Ocupar una
            // voz con algo que nadie va a oir seria robarsela a un sonido
            // audible. No cuenta como descarte por falta de voz.
            if (finalVolume <= 0f) return false;

            float distance = DistanceToListener(position);
            float audibility = volume * Attenuation(distance) * Mathf.Max(0f, priority);
            if (audibility <= 0f) return false;   // ya esta fuera de rango

            float now = Now;
            int slot = SelectVictim(voices3D, audibility, now);
            if (slot < 0)
            {
                DroppedCount++;
                return false;
            }

            var src = sources3D[slot];
            if (src == null) { DroppedCount++; return false; }

            var lp = filters3D[slot];
            if (lp != null) lp.cutoffFrequency = CutoffFor(distance);

            src.transform.position = position;
            src.clip = clip;
            src.volume = finalVolume;
            // Pista para la virtualizacion propia del motor, que es
            // independiente de la nuestra: en Unity 0 es la MAXIMA prioridad
            // y 255 la minima, por eso va invertido.
            src.priority = Mathf.Clamp(255 - Mathf.RoundToInt(Mathf.Clamp01(priority) * 255f), 0, 255);

            float pitch = NextPitch();
            src.pitch = pitch;   // ANTES de Play(), y con Play(), no PlayOneShot
            src.Play();

            // A pitch alto el clip dura MENOS: se divide. El Max evita
            // dividir por cero si alguna vez llega un pitch degenerado.
            voices3D[slot].Free = false;
            voices3D[slot].Audibility = audibility;
            voices3D[slot].ExpiresAt = now + clip.length / Mathf.Max(0.01f, pitch);
            return true;
        }

        // Voz 2D: interfaz y ambiente. Sin posicion, sin filtro y sin
        // atenuacion, asi que la audibilidad es solo volumen por prioridad.
        public bool PlayFlat(AudioClip clip, SfxChannel channel, float volume, float priority)
        {
            if (!Application.isPlaying) return false;
            if (clip == null) return false;

            EnsureVoices();

            float finalVolume = Mathf.Clamp01(volume * GainFor(channel));
            if (finalVolume <= 0f) return false;

            float audibility = volume * Mathf.Max(0f, priority);
            if (audibility <= 0f) return false;

            float now = Now;
            int slot = SelectVictim(voices2D, audibility, now);
            if (slot < 0)
            {
                DroppedCount++;
                return false;
            }

            var src = sources2D[slot];
            if (src == null) { DroppedCount++; return false; }

            src.clip = clip;
            src.volume = finalVolume;
            src.priority = Mathf.Clamp(255 - Mathf.RoundToInt(Mathf.Clamp01(priority) * 255f), 0, 255);

            float pitch = NextPitch();
            src.pitch = pitch;
            src.Play();

            voices2D[slot].Free = false;
            voices2D[slot].Audibility = audibility;
            voices2D[slot].ExpiresAt = now + clip.length / Mathf.Max(0.01f, pitch);
            return true;
        }

        public bool PlayUi(SfxKind kind, float volume, float priority)
            => PlayFlat(GenericSfx.Get(kind), SfxChannel.Ui, volume, priority);

        // Atajos estaticos para los puntos de llamada, que hoy resuelven el
        // audio con AudioSource.PlayClipAtPoint o con un AudioSource por
        // entidad. Devuelven false si no hay director en la escena, para que
        // quien llama pueda decidir si le importa; nunca tiran.
        public static bool PlayAt(SfxKind kind, Vector3 position, float volume, float priority = 1f)
            => Instance != null && Instance.PlayClip(GenericSfx.Get(kind), position, volume, priority);

        public static bool PlayClipAt(AudioClip clip, Vector3 position, float volume, float priority = 1f)
            => Instance != null && Instance.PlayClip(clip, position, volume, priority);

        public static bool PlayUi2D(SfxKind kind, float volume, float priority = 1f)
            => Instance != null && Instance.PlayUi(kind, volume, priority);

        public static float DistanceOrUnknown(Transform listener, Vector3 position) =>
            listener != null ? Vector3.Distance(listener.position, position) : float.MaxValue;

        float DistanceToListener(Vector3 position) => DistanceOrUnknown(ResolveListener(), position);

        Transform ResolveListener()
        {
            if (listenerTf != null) return listenerTf;
            // Camara primero porque el AudioListener vive ahi y Camera.main
            // ya esta cacheado por el motor. El barrido de escena es el plan
            // B y solo corre cuando la referencia se perdio, NUNCA por
            // frame ni por sonido: un FindAnyObjectByType por disparo con
            // cincuenta soldados es exactamente lo que no queremos.
            //
            // FindAnyObjectByType y no FindFirstObjectByType: el segundo esta
            // marcado obsoleto en esta version de Unity, justamente porque
            // depende del orden de instance ID.
            var cam = Camera.main;
            if (cam != null) { listenerTf = cam.transform; return listenerTf; }
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener != null) listenerTf = listener.transform;
            return listenerTf;
        }

        // ------------------------------------------------------------------
        // Un solo Update para todo el audio del juego
        // ------------------------------------------------------------------

        // 24 + 6 comparaciones de float por frame, fijas, sin importar
        // cuantos soldados haya en la escena. Preguntarle isPlaying a cada
        // AudioSource seria una llamada nativa por voz y por frame, y nada
        // por entidad puede escalar a cincuenta soldados.
        void Update()
        {
            float now = Now;
            ReapExpired(voices3D, now);
            ReapExpired(voices2D, now);
        }

        static void ReapExpired(VoiceState[] voices, float now)
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i].Free || !IsIdle(voices[i], now)) continue;
                voices[i].Free = true;
                voices[i].Audibility = 0f;
            }
        }

        void StopAllVoices()
        {
            SilenceAll(sources3D, voices3D);
            SilenceAll(sources2D, voices2D);
        }

        static void SilenceAll(AudioSource[] sources, VoiceState[] voices)
        {
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null) sources[i].Stop();
                if (voices != null && i < voices.Length)
                {
                    voices[i].Free = true;
                    voices[i].Audibility = 0f;
                    voices[i].ExpiresAt = 0f;
                }
            }
        }

        // ------------------------------------------------------------------
        // Verificacion
        // ------------------------------------------------------------------

        // Cuentan las dos reservas juntas (30 voces): FreeVoiceCount +
        // ActiveVoiceCount es siempre VoiceCount.
        public int VoiceCount => Voice3DBudget + Voice2DBudget;

        public int FreeVoiceCount => CountIdle(voices3D, Voice3DBudget, Now) + CountIdle(voices2D, Voice2DBudget, Now);
        public int ActiveVoiceCount => VoiceCount - FreeVoiceCount;

        public int Active3DVoiceCount => Voice3DBudget - CountIdle(voices3D, Voice3DBudget, Now);
        public int Active2DVoiceCount => Voice2DBudget - CountIdle(voices2D, Voice2DBudget, Now);

        static int CountIdle(VoiceState[] voices, int budget, float now)
        {
            // Pool todavia sin armar (Edit mode, o antes del primer
            // OnEnable): estan todas libres. Devolver cero haria que
            // ActiveVoiceCount informara 30 voces sonando sin que exista una
            // sola fuente.
            if (voices == null) return budget;
            int n = 0;
            // Una voz vencida cuenta como libre aunque Update todavia no la
            // haya cosechado, o el conteo dependeria del orden de ejecucion.
            for (int i = 0; i < voices.Length; i++)
                if (IsIdle(voices[i], now)) n++;
            return n;
        }

        public void ResetStats() => DroppedCount = 0;
    }
}
