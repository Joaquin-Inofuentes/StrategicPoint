using System.Collections.Generic;
using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // Los miembros nuevos van SIEMPRE al final: el valor entero de cada
    // uno es lo que quedaria guardado si alguna vez se serializara, y
    // meter uno en el medio correria todos los que siguen.
    public enum SfxKind { Shoot, Hit, Death, Order, Swap, EmptyClick, VehicleHit, CannonBody, CannonCrack, TurretReloaded, Wounded, Heartbeat }

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

        static AudioClip GenerateMetalClang()
        {
            const int sampleRate = 44100;
            const float duration = 0.22f;
            const float decay = 14f;
            // Frecuencias sin relacion armonica simple entre si: es
            // justamente esa "desafinacion" la que se lee como metal en
            // vez de como una nota musical limpia.
            float[] partials = { 180f, 410f, 730f };
            float[] weights = { 0.5f, 0.32f, 0.22f };

            int sampleCount = (int)(duration * sampleRate);
            var samples = new float[sampleCount];
            var rng = new System.Random(7);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-decay * t);
                float v = 0f;
                for (int p = 0; p < partials.Length; p++)
                    v += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * weights[p];
                v += ((float)rng.NextDouble() - 0.5f) * 0.08f;
                samples[i] = Mathf.Clamp(v * envelope, -1f, 1f);
            }

            var clip = AudioClip.Create("VehicleHit", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
