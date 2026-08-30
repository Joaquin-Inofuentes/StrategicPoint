using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    public enum SfxKind { Shoot, Hit, Death, Order, Swap }

    // Sonidos genéricos generados por código (tonos con envolvente),
    // para no depender de clips de audio importados en el prototipo.
    public static class GenericSfx
    {
        static readonly Dictionary<SfxKind, AudioClip> cache = new Dictionary<SfxKind, AudioClip>();

        public static AudioClip Get(SfxKind kind)
        {
            if (cache.TryGetValue(kind, out var clip) && clip != null) return clip;
            clip = Generate(kind);
            cache[kind] = clip;
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
                default: freq = 500f; duration = 0.08f; decay = 14f; break;
            }

            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, (int)(duration * sampleRate));
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-decay * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
            }

            var clip = AudioClip.Create(kind.ToString(), sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
