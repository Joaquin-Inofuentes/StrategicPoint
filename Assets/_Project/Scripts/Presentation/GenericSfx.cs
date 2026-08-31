using System.Collections.Generic;
using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    public enum SfxKind { Shoot, Hit, Death, Order, Swap, EmptyClick }

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

        static AudioClip Generate(SfxKind kind)
        {
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
                default: freq = 500f; duration = 0.08f; decay = 14f; break;
            }
            return GenerateTone(freq, duration, decay, kind.ToString());
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
