using UnityEngine;

namespace SP.Presentation
{
    // WP2 (#072): graves de explosion/cañon y chapa de la destruccion de un vehiculo. Todo sintetizado por codigo
    // (deterministico, semilla fija), sin descargar nada.
    public static partial class SfxSintetico
    {
        // Datos crudos (a la frecuencia de muestreo pedida) de un golpe de graves, saturado. Dos capas:
        //   sub     seno que cae de 60 a 28 Hz con ataque de 3 ms y caida de ~1.1 s (lo que se SIENTE);
        //   golpe   seno que cae de 125 a 45 Hz en ~0.4 s (lo que se OYE en parlantes chicos; la saturacion le agrega armonicos).
        // Sin normalizar: GenericSfx.ConGraves lo mezcla y normaliza.
        public static float[] SubGolpeDatos(int sr)
        {
            int n = (int)(1.25f * sr);
            var d = new float[n];
            double fase = 0.0, faseG = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / sr;
                double f = 28.0 + 32.0 * System.Math.Exp(-t * 4.0);
                fase += 2.0 * System.Math.PI * f / sr;
                faseG += 2.0 * System.Math.PI * (45.0 + 80.0 * System.Math.Exp(-t * 7.0)) / sr;
                double atq = System.Math.Min(1.0, t / 0.003);
                double sub = System.Math.Sin(fase) * atq * System.Math.Exp(-t * 3.9);
                double golpe = System.Math.Sin(faseG) * atq * System.Math.Exp(-t * 6.5);
                d[i] = (float)System.Math.Tanh(1.8 * (sub + 0.9 * golpe));
            }
            return d;
        }

        // Retumbo grave de la explosion sintetica: ruido pasado por un filtro de un polo a ~240 Hz con caida lenta + barrido grave.
        public static float[] RetumboDeExplosionDatos(int sr)
        {
            int n = (int)(2.2f * sr);
            var d = new float[n];
            var rng = new System.Random(905);
            double a = System.Math.Exp(-2.0 * System.Math.PI * 240.0 / sr);
            double low = 0.0, fase = 0.0, pico = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / sr;
                double blanco = rng.NextDouble() * 2.0 - 1.0;
                low = a * low + (1.0 - a) * blanco;
                fase += 2.0 * System.Math.PI * (26.0 + 89.0 * System.Math.Exp(-t * 3.0)) / sr;
                double env = System.Math.Min(1.0, t / 0.005) * System.Math.Exp(-t * 2.1);
                double v = (low * 5.0 + 0.8 * System.Math.Sin(fase)) * env;
                d[i] = (float)v;
                double av = v < 0 ? -v : v;
                if (av > pico) pico = av;
            }
            if (pico > 0.0001) for (int i = 0; i < n; i++) d[i] = (float)(d[i] / pico);
            return d;
        }

        // Clip suelto del golpe de graves (para auditar o reusar).
        public static AudioClip SubGolpe()
        {
            var d = SubGolpeDatos(SR);
            return Cerrar(d, "SubGolpe", 0.9f);
        }

        // Fin de un vehiculo: golpe seco + parciales inarmonicos de chapa gruesa (180/410/730/1100 Hz) que se retuerce, 6 a 9 golpes
        // de chapas que caen entre 0.3 y 2.5 s (Clac/Tunc a tonos graves) y un roce largo de metal arrastrado.
        public static AudioClip DestruccionVehiculo()
        {
            var b = Buffer(3.4f);
            Ruido(b, 0f, 0.1f, 0.45f, 0.45f, 0.98f, 45f, 1.0f, 902, 0.0004f);
            Tono(b, 0f, 0.7f, 95f, 38f, 5f, 0.8f, 0.15f, 0.003f);
            Metal(b, 0f, 1.8f, new[] { 180f, 410f, 730f, 1100f }, 2.6f, 0.9f, 901, 0.10f);
            var rng = new System.Random(903);
            int golpes = 6 + rng.Next(4);
            for (int i = 0; i < golpes; i++)
            {
                float t = 0.3f + (float)rng.NextDouble() * 2.2f;
                float g = 0.12f + 0.5f * (1f - t / 3.4f);
                if (i % 2 == 0) Clac(b, t, g, 910 + i, 0.35f + (float)rng.NextDouble() * 0.5f);
                else Tunc(b, t, g * 1.2f, 930 + i, 0.9f + (float)rng.NextDouble() * 0.5f);
            }
            Roce(b, 0.25f, 2.6f, 0.4f, 950);
            return Cerrar(b, "DestruccionVehiculo", 0.9f);
        }
    }
}
