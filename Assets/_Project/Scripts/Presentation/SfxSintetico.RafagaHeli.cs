using UnityEngine;

namespace SP.Presentation
{
    // Bug #297 ("el sonido de disparos del helicoptero es molesto, reemplazalo por uno mejor"): antes cada bala de las dos ametralladoras de
    // puerta reproducia Shot_Heavy (una escopeta distorsionada, 16 veces por segundo). Ahora cada rafaga suena UNA vez, como un clip de
    // 1,2 s: golpes graves y secos (10 por segundo, con un poco de jitter) sin chirrido ni distorsion, y una cola corta.
    public static partial class SfxSintetico
    {
        public const float DuracionRafagaHeli = 1.2f;
        static AudioClip rafagaHeli;

        public static AudioClip RafagaDeHeli()
        {
            if (rafagaHeli != null) return rafagaHeli;
            var b = Buffer(DuracionRafagaHeli + 0.15f);
            var rng = new System.Random(2977);
            const int golpes = 12;
            for (int i = 0; i < golpes; i++)
            {
                float t = i * (DuracionRafagaHeli / golpes) + ((float)rng.NextDouble() - 0.5f) * 0.012f;
                if (t < 0f) t = 0f;
                float g = 0.8f + (float)rng.NextDouble() * 0.2f;
                Tono(b, t, 0.10f, 150f, 62f, 26f, g, 0.08f, 0.0015f);                         // el "thump" grave
                Ruido(b, t, 0.028f, 0.38f, 0.2f, 0.97f, 70f, g * 0.35f, 2980 + i, 0.0004f);     // el cuerpo del disparo, sin agudos
            }
            // Cola grave: la rafaga se apaga sola.
            Ruido(b, DuracionRafagaHeli - 0.05f, 0.2f, 0.12f, 0.08f, 0.99f, 14f, 0.18f, 2999, 0.01f);
            rafagaHeli = Cerrar(b, "RafagaHeli", 0.7f);
            return rafagaHeli;
        }
    }
}
