using UnityEngine;

namespace SP.Presentation
{
    public static partial class SfxSintetico
    {
        // R17 (pendiente 2.8): murmullo de agua de las fuentes. Ruido filtrado en dos pasadas (un "shhh" grave y otro mas suave) con un
        // vaiven lento de volumen, para que se oiga corriente y no estatica. Dura 2 s y se cierra en lazo (sin salto al repetir).
        static AudioClip aguaDeFuente;
        public static AudioClip AguaDeFuente()
        {
            if (aguaDeFuente != null) return aguaDeFuente;
            const float dur = 2.0f, f = 0.25f;
            int n = (int)(dur * SR);
            var b = Buffer(dur + f);
            var rng = new System.Random(901);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR;
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.10f * (w - lp);
                lp2 += 0.025f * (lp - lp2);
                float burbuja = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 3.1f * t) * Mathf.Sin(2f * Mathf.PI * 1.7f * t);
                b[i] = (0.8f * lp + 0.9f * lp2) * burbuja;
            }
            aguaDeFuente = Lazo(b, n, "AguaDeFuente", 0.5f);
            return aguaDeFuente;
        }
    }
}
