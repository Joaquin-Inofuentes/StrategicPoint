using UnityEngine;

namespace SP.Presentation
{
    // #104: sonido del faro (Luminaria) al romperse: estallido de vidrio (crack seco + cascada de tintineos agudos) y el chisporroteo
    // electrico de la lampara muriendo. Sintetizado, deterministico.
    public static partial class SfxSintetico
    {
        public static AudioClip VidrioRoto()
        {
            var b = Buffer(1.3f);
            // Crack del estallido + golpe de la carcasa
            Ruido(b, 0f, 0.035f, 0.25f, 0.25f, 0.96f, 80f, 1.0f, 801, 0.0002f);
            Tono(b, 0f, 0.12f, 420f, 160f, 30f, 0.45f, 0.1f, 0.001f);
            // Cascada de tintineos de vidrio: trozos que caen y rebotan (parciales agudos con decaimiento rapido)
            var rng = new System.Random(802);
            for (int i = 0; i < 22; i++)
            {
                float t = 0.015f + (float)rng.NextDouble() * 0.75f * (0.35f + 0.65f * (float)rng.NextDouble());
                float f = 2800f + (float)rng.NextDouble() * 4200f;
                float g = (0.5f + (float)rng.NextDouble() * 0.5f) * (1f - t / 1.0f) * 0.5f;
                Metal(b, t, 0.18f, new[] { f, f * 1.52f, f * 2.31f }, 24f + (float)rng.NextDouble() * 20f, Mathf.Max(0.03f, g), 810 + i, 0.015f);
            }
            // Chisporroteo electrico: ruido en rafagas irregulares que se espacian
            float tt = 0.08f;
            for (int i = 0; i < 12; i++)
            {
                Ruido(b, tt, 0.012f + (float)rng.NextDouble() * 0.02f, 0.3f, 0.3f, 0.95f, 60f, 0.35f * (1f - i / 14f), 840 + i, 0.0003f);
                tt += 0.03f + (float)rng.NextDouble() * 0.05f * (1f + i * 0.35f);
            }
            // Zumbido que cae (el balasto muere)
            Tono(b, 0.05f, 0.45f, 220f, 55f, 7f, 0.2f, 0.4f, 0.004f);
            return Cerrar(b, "VidrioRoto", 0.9f);
        }
    }
}
