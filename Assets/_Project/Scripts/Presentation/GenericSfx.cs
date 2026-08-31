using System.Collections.Generic;
using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // Los miembros nuevos van SIEMPRE al final: el valor entero de cada
    // uno es lo que quedaria guardado si alguna vez se serializara, y
    // meter uno en el medio correria todos los que siguen.
    //
    // ImpactMetal / ImpactDirt / ImpactStone (item 192) son las colas de
    // impacto por MATERIAL: hasta ahora EnvironmentHitEvent solo pintaba
    // particulas, asi que pegarle a un tanque, a una pared o al piso
    // sonaba exactamente igual -- es decir, no sonaba. BulletWhizz (item
    // 194) es el silbido de la bala que pasa cerca.
    public enum SfxKind { Shoot, Hit, Death, Order, Swap, EmptyClick, VehicleHit, CannonBody, CannonCrack, TurretReloaded, Wounded, Heartbeat, ImpactMetal, ImpactDirt, ImpactStone, BulletWhizz }

    // Sonidos genéricos generados por código (tonos con envolvente),
    // para no depender de clips de audio importados en el prototipo.
    public static class GenericSfx
    {
        static readonly Dictionary<SfxKind, AudioClip> cache = new Dictionary<SfxKind, AudioClip>();
        static readonly Dictionary<WeaponKind, AudioClip> weaponShotCache = new Dictionary<WeaponKind, AudioClip>();

        public static AudioClip Get(SfxKind kind)
        {
            if (cache.TryGetValue(kind, out var clip) && clip != null) return clip;
            clip = Generate(kind);
            cache[kind] = clip;
            return clip;
        }

        // Antes las tres armas sonaban exactamente igual al disparar: el
        // unico indicio de que arma tenias era mirar el HUD. Cada una
        // ahora tiene su propio timbre, en vez de compartir SfxKind.Shoot.
        public static AudioClip GetWeaponShot(WeaponKind kind)
        {
            if (weaponShotCache.TryGetValue(kind, out var clip) && clip != null) return clip;
            float freq, duration, decay;
            switch (kind)
            {
                // Rifle: medio-agudo y corto, cadencia rapida.
                case WeaponKind.Rifle: freq = 950f; duration = 0.07f; decay = 20f; break;
                // Pistola: mas agudo todavia pero mas corto y seco, un "pop".
                case WeaponKind.Pistol: freq = 1300f; duration = 0.05f; decay = 26f; break;
                // Heavy: grave y sostenido, se siente mas pesado.
                case WeaponKind.Heavy: freq = 220f; duration = 0.16f; decay = 8f; break;
                default: freq = 900f; duration = 0.08f; decay = 18f; break;
            }
            clip = GenerateTone(freq, duration, decay, "Shot_" + kind);
            weaponShotCache[kind] = clip;
            return clip;
        }

        // Un tono con pitch propio necesita un AudioSource: PlayClipAtPoint no
        // admite pitch y PlayOneShot no lo captura (lo lee en vivo cada frame).
        // Un solo lugar para los usos que necesitan pitch propio.
        //
        // POR QUE SIGUE EXISTIENDO DESPUES DE AudioDirector: el director
        // fija el pitch el mismo (AudioDirector.NextPitch, la variacion
        // por instancia del item 191) y NO expone ningun parametro de
        // pitch, ni en PlayUi ni en PlayFlat ni en PlayClip. Los dos unicos
        // sonidos del juego cuyo pitch es INFORMACION -- el tono critico de
        // la mirilla (1.7 fijo) y el tono de racha de KillFeedbackDirector
        // (sube con la racha) -- perderian justamente lo que comunican si
        // pasaran por ahi. Todo el resto del audio ya migro al director:
        // estos dos quedan aca a proposito, no por olvido.
        public static void PlayOneShot2D(AudioClip clip, float volume, float pitch, string name = "OneShotTone")
        {
            if (clip == null || !Application.isPlaying) return;
            var go = new GameObject(name);
            var cam = Camera.main;
            if (cam != null) go.transform.SetParent(cam.transform, false);   // no cuelga de la raiz
            var src = go.AddComponent<AudioSource>();
            src.clip = clip; src.volume = volume; src.pitch = pitch; src.spatialBlend = 0f;
            src.Play();
            // A pitch alto el clip dura MENOS, no mas: se divide, no se
            // multiplica. El Max evita dividir por cero si llega un pitch 0.
            Object.Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        static AudioClip Generate(SfxKind kind)
        {
            // Choque metalico: antes un impacto en el vehiculo sonaba
            // (SfxKind.Hit) identico a un balazo en un soldado -- un tono
            // puro no suena a metal, hace falta sumar un par de parciales
            // desafinados entre si (asi suena una campana o una chapa).
            if (kind == SfxKind.VehicleHit) return GenerateMetalClang();

            // Item 192: las tres colas por material NO son tonos. Un
            // impacto contra tierra o piedra no tiene altura musical
            // reconocible, y GenerateTone (una senoidal pura) siempre
            // suena a nota -- por eso salen por generadores propios y se
            // devuelven antes del switch de frecuencia de abajo.
            if (kind == SfxKind.ImpactMetal) return GenerateBulletMetal();
            if (kind == SfxKind.ImpactDirt) return GenerateDirtThud();
            if (kind == SfxKind.ImpactStone) return GenerateStoneCrack();
            // Item 194: idem, pero ademas necesita barrido de frecuencia.
            if (kind == SfxKind.BulletWhizz) return GenerateWhizz();

            float freq, duration, decay;
            switch (kind)
            {
                case SfxKind.Shoot: freq = 900f; duration = 0.08f; decay = 18f; break;
                case SfxKind.Hit: freq = 220f; duration = 0.12f; decay = 10f; break;
                case SfxKind.Death: freq = 110f; duration = 0.5f; decay = 3f; break;
                case SfxKind.Order: freq = 660f; duration = 0.1f; decay = 12f; break;
                case SfxKind.Swap: freq = 1200f; duration = 0.15f; decay = 8f; break;
                // Clic seco de gatillo vacio: muy corto, sin tono musical
                // reconocible, para que se lea como un "no" mecanico y no
                // como una nota mas del resto de la paleta de sonidos.
                case SfxKind.EmptyClick: freq = 2400f; duration = 0.035f; decay = 60f; break;
                // Un cañon necesita DOS capas para sonar potente, no un
                // tono unico: el cuerpo grave que da el peso y el crack
                // agudo que da el golpe. Se reproducen juntas.
                case SfxKind.CannonBody: freq = 55f; duration = 0.55f; decay = 4f; break;
                case SfxKind.CannonCrack: freq = 1700f; duration = 0.07f; decay = 32f; break;
                // Mecanismo de recarga: se dispara al COMPLETARSE el
                // cooldown, no al iniciarlo, para que el artillero pueda
                // mirar el campo en vez del HUD.
                case SfxKind.TurretReloaded: freq = 480f; duration = 0.09f; decay = 26f; break;
                // Quejido del herido: es SfxKind.Hit a pitch 0.75 pero como clip
                // propio. Bajarle el pitch al AudioSource compartido no funciona:
                // PlayOneShot no captura el pitch, lo lee en vivo cada frame.
                case SfxKind.Wounded: freq = 165f; duration = 0.16f; decay = 7.5f; break;
                // Latido: un golpe grave y corto, no una nota. Con una frecuencia
                // musical reconocible sonaria como parte de la paleta de avisos en
                // vez de como una senal corporal.
                case SfxKind.Heartbeat: freq = 55f; duration = 0.22f; decay = 9f; break;
                default: freq = 500f; duration = 0.08f; decay = 14f; break;
            }
            return GenerateTone(freq, duration, decay, kind.ToString());
        }

        // Golpe de chasis: los parciales de siempre, movidos tal cual al
        // helper parametrizado. Mismos numeros y misma semilla, asi que el
        // clip que sale es EXACTAMENTE el de antes -- generalizar no puede
        // cambiar como suena un sonido que ya estaba aprobado.
        static AudioClip GenerateMetalClang()
            => GenerateMetallic(MetalClangPartials, MetalClangWeights, 0.22f, 14f, 0.08f, 7, "VehicleHit");

        static readonly float[] MetalClangPartials = { 180f, 410f, 730f };
        static readonly float[] MetalClangWeights = { 0.5f, 0.32f, 0.22f };

        // Item 192, metal: bala contra blindaje. Mismo enfoque
        // multi-parcial que el golpe de chasis, pero con parciales MAS
        // AGUDOS, decaimiento mucho mas rapido y mas ruido: un balazo
        // puntual sobre chapa es un "tink" que no hace resonar el casco
        // entero, y si sonara igual que GenerateMetalClang el jugador no
        // podria distinguir "le pegue al tanque" de "el tanque comio un
        // golpe grande".
        static readonly float[] BulletMetalPartials = { 620f, 1370f, 2480f };
        static readonly float[] BulletMetalWeights = { 0.44f, 0.30f, 0.20f };

        static AudioClip GenerateBulletMetal()
            => GenerateMetallic(BulletMetalPartials, BulletMetalWeights, 0.15f, 27f, 0.14f, 11, "ImpactMetal");

        // Suma de parciales inarmonicos + una pizca de ruido. Es esa
        // "desafinacion" entre parciales la que se lee como metal en vez
        // de como una nota musical limpia.
        //
        // La semilla del ruido es FIJA y explicita: el mismo SfxKind tiene
        // que sonar identico entre corridas, si no, dos partidas no serian
        // comparables y un problema de audio no se podria reproducir.
        static AudioClip GenerateMetallic(float[] partials, float[] weights, float duration, float decay, float noise, int seed, string name)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, (int)(duration * sampleRate));
            var samples = new float[sampleCount];
            var rng = new System.Random(seed);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-decay * t);
                float v = 0f;
                for (int p = 0; p < partials.Length; p++)
                    v += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * weights[p];
                v += ((float)rng.NextDouble() - 0.5f) * noise;
                samples[i] = Mathf.Clamp(v * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Item 192, tierra: rafaga de ruido de BAJA frecuencia con
        // envolvente rapida. Un impacto en el piso es un golpe sordo sin
        // altura: todo lo que aporta informacion esta en los graves y en
        // lo rapido que se apaga.
        static AudioClip GenerateDirtThud()
            => GenerateNoiseBurst(0.985f, 0.9995f, 0.13f, 32f, 0.002f, 31, "ImpactDirt");

        // Item 192, piedra: media frecuencia y cola bastante mas larga que
        // la tierra. La piedra devuelve energia (rebota, astilla) donde la
        // tierra la absorbe -- esa diferencia de COLA es justamente la que
        // le dice al jugador contra que material esta disparando.
        static AudioClip GenerateStoneCrack()
            => GenerateNoiseBurst(0.90f, 0.995f, 0.30f, 11f, 0.001f, 53, "ImpactStone");

        // Ruido blanco pasado por dos filtros de UN POLO por muestra:
        //   lowAmount  acerca cada muestra a la anterior -> se come los
        //              agudos (cuanto mas cerca de 1, mas grave queda);
        //   highAmount sigue una media todavia mas lenta que se RESTA ->
        //              se come los graves y el continuo.
        // Los dos juntos dan una banda, que es lo unico que hace falta
        // para diferenciar materiales. Un filtro biquad real seria mas
        // limpio y aca no aportaria nada audible.
        //
        // No se usa AudioLowPassFilter (el componente de Unity) a
        // proposito: ese filtra la voz EN REPRODUCCION y ya esta ocupado
        // por la atenuacion por distancia de AudioDirector (item 187).
        // Aca hace falta hornear la banda DENTRO del clip.
        static AudioClip GenerateNoiseBurst(float lowAmount, float highAmount, float duration, float decay, float attack, int seed, string name)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, (int)(duration * sampleRate));
            var samples = new float[sampleCount];
            var rng = new System.Random(seed);

            float low = 0f, sub = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(white, low, lowAmount);
                sub = Mathf.Lerp(low, sub, highAmount);
                // Ataque corto pero NO instantaneo: saltar de cero a la
                // amplitud maxima en una sola muestra mete un click digital
                // que se escucha por encima del propio impacto.
                float envelope = Mathf.Exp(-decay * t) * (attack <= 0f ? 1f : Mathf.Min(1f, t / attack));
                samples[i] = (low - sub) * envelope;
            }

            // Normalizacion al pico: filtrar ruido blanco fuerte le baja
            // muchisimo la energia (cuanto mas angosta la banda, mas), y
            // cuanto sale depende de los coeficientes. Sin esto, ajustar
            // la banda cambiaria tambien el volumen y habria que
            // recalibrar cada punto de llamada a mano.
            NormalizePeak(samples, 0.8f);

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Item 194: silbido de la bala que pasa cerca.
        //
        // Barrido descendente y no una frecuencia fija: lo que el oido
        // reconoce como "me paso al lado" es el Doppler, o sea la caida de
        // tono mientras se aleja. Con un tono fijo suena a un "chsss"
        // cualquiera y no comunica nada. La envolvente es una campana
        // (entra y sale) y no la exponencial del resto de la paleta: una
        // exponencial arranca en el maximo, o sea la bala ya al lado tuyo,
        // sin la parte de "viene".
        static AudioClip GenerateWhizz()
        {
            const int sampleRate = 44100;
            const float duration = 0.20f;
            int sampleCount = (int)(duration * sampleRate);
            var samples = new float[sampleCount];
            var rng = new System.Random(23);

            float low = 0f, sub = 0f;
            double phase = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                float k = (float)i / sampleCount;
                float freq = Mathf.Lerp(2600f, 850f, k);
                // La fase se ACUMULA en vez de calcularse como 2*pi*f*t:
                // con la frecuencia variando, esa formula produce saltos de
                // fase (clicks) en cada muestra.
                phase += 2.0 * System.Math.PI * freq / sampleRate;

                float white = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(white, low, 0.62f);
                sub = Mathf.Lerp(low, sub, 0.995f);

                float envelope = Mathf.Sin(Mathf.PI * k);
                samples[i] = ((float)System.Math.Sin(phase) * 0.45f + (low - sub) * 0.55f) * envelope;
            }

            NormalizePeak(samples, 0.7f);

            var clip = AudioClip.Create("BulletWhizz", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static void NormalizePeak(float[] samples, float target)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = samples[i] < 0f ? -samples[i] : samples[i];
                if (a > peak) peak = a;
            }
            // Un buffer entero en silencio existe (duracion redondeada a
            // una sola muestra): dividir por cero devolveria NaN y
            // AudioClip.SetData con NaN es un chirrido, no un silencio.
            if (peak <= 0.0001f) return;
            float gain = target / peak;
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
        }

        static AudioClip GenerateTone(float freq, float duration, float decay, string name)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, (int)(duration * sampleRate));
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-decay * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
