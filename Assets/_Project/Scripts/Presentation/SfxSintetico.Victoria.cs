using UnityEngine;

namespace SP.Presentation
{
    // WP2 (#093): himno de victoria y salvataje, sintetizado por codigo (nada descargado). 26,5 s en Re mayor a 92 bpm,
    // progresion I-V-vi-IV (un compas por acorde, dos vueltas) y un compas final de resolucion:
    //   compases 0-3  pad calido + cuerno suave + timbal en los tiempos 1 y 3
    //   compases 4-7  melodia de bronces saturados a plena fuerza + timbal en los 4 tiempos y redoble corto al final
    //   compases 8-9  acorde de Re sostenido, platillo, redoble final con crescendo y golpe de cierre
    // Los datos se generan sin tocar la API de Unity (solo System.Math), asi se calculan en un hilo y no traban el juego;
    // el AudioClip se crea despues en el hilo principal.
    public static partial class SfxSintetico
    {
        public const int HimnoSR = 44100;
        public const double HimnoBpm = 92.0;
        public const int HimnoCompases = 10;

        const int TablaN = 2048;

        // Tabla de onda: diente de sierra de banda limitada (suma de armonicos) o seno.
        static float[] TablaSierra(int armonicos)
        {
            var t = new float[TablaN + 1];
            double pico = 0;
            for (int i = 0; i < TablaN; i++)
            {
                double v = 0;
                for (int h = 1; h <= armonicos; h++) v += System.Math.Sin(2.0 * System.Math.PI * h * i / TablaN) / h;
                t[i] = (float)v;
                if (System.Math.Abs(v) > pico) pico = System.Math.Abs(v);
            }
            for (int i = 0; i < TablaN; i++) t[i] = (float)(t[i] / pico);
            t[TablaN] = t[0];
            return t;
        }

        static float Leer(float[] tabla, double fase)
        {
            double x = fase * TablaN;
            int i = (int)x;
            float fr = (float)(x - i);
            return tabla[i] + (tabla[i + 1] - tabla[i]) * fr;
        }

        // Una voz sostenida con ataque lento / release exponencial, dos osciladores desafinados y vibrato opcional.
        static void Voz(float[] b, float[] tabla, int sr, double inicio, double duracion, double release, double f, double ataque, double ganancia, double desafine, double vibrato, double octavaAbajo)
        {
            int i0 = (int)(inicio * sr);
            int n = (int)((duracion + release) * sr);
            double fa = 0, fb = 0, fc = 0;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                double t = (double)i / sr;
                double env = System.Math.Min(1.0, t / ataque);
                if (t > duracion) env *= System.Math.Exp(-(t - duracion) * (5.0 / release));
                double vib = 1.0 + vibrato * System.Math.Sin(2.0 * System.Math.PI * 5.2 * t) * System.Math.Min(1.0, t / 0.4);
                double ff = f * vib;
                fa += ff * (1.0 + desafine) / sr; fb += ff * (1.0 - desafine) / sr; fc += ff * 0.5 / sr;
                fa -= System.Math.Floor(fa); fb -= System.Math.Floor(fb); fc -= System.Math.Floor(fc);
                double v = Leer(tabla, fa) + Leer(tabla, fb);
                if (octavaAbajo > 0) v += octavaAbajo * 2.0 * Leer(tabla, fc);
                b[i0 + i] += (float)(v * 0.5 * env * ganancia);
            }
        }

        // Golpe de timbal: tono que cae de ~125 a 70 Hz + golpe seco de baqueta (ruido grave muy corto).
        static void Timbal(float[] b, int sr, double inicio, double vel, System.Random rng)
        {
            int i0 = (int)(inicio * sr);
            int n = (int)(1.15 * sr);
            double fase = 0, low = 0;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                double t = (double)i / sr;
                fase += 2.0 * System.Math.PI * (70.0 + 55.0 * System.Math.Exp(-t * 14.0)) / sr;
                double cuerpo = System.Math.Sin(fase) * System.Math.Exp(-t * 5.2) * System.Math.Min(1.0, t / 0.002);
                double blanco = rng.NextDouble() * 2.0 - 1.0;
                low += 0.25 * (blanco - low);
                double baqueta = low * System.Math.Exp(-t * 90.0) * 1.4;
                b[i0 + i] += (float)((cuerpo + baqueta) * vel);
            }
        }

        // Platillo: ruido agudo (blanco menos su media lenta) con caida larga.
        static void Platillo(float[] b, int sr, double inicio, double dur, double ganancia, System.Random rng)
        {
            int i0 = (int)(inicio * sr);
            int n = (int)(dur * sr);
            double low = 0;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                double t = (double)i / sr;
                double blanco = rng.NextDouble() * 2.0 - 1.0;
                low += 0.35 * (blanco - low);
                b[i0 + i] += (float)((blanco - low) * System.Math.Exp(-t * 2.2) * System.Math.Min(1.0, t / 0.003) * ganancia);
            }
        }

        static void PasaBajos(float[] b, int sr, double corte)
        {
            double a = 1.0 - System.Math.Exp(-2.0 * System.Math.PI * corte / sr);
            double y = 0;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = (float)y; }
        }

        // Datos del himno (mono). Seguro para llamar desde un hilo de fondo.
        public static float[] HimnoDeVictoriaDatos(int sr = HimnoSR)
        {
            double beat = 60.0 / HimnoBpm;
            int n = (int)((HimnoCompases * 4 * beat + 0.45) * sr);
            var pad = new float[n];
            var bronce = new float[n];
            var perc = new float[n];
            var rng = new System.Random(970);
            var sierraPad = TablaSierra(7);
            var sierraBronce = TablaSierra(16);

            // Acordes por compas: D A Bm G D A Bm G y Re final (dos compases).
            double[][] acordes =
            {
                new[] { 146.83, 185.00, 220.00, 293.66 },   // D
                new[] { 220.00, 277.18, 329.63, 440.00 },   // A
                new[] { 246.94, 293.66, 369.99, 493.88 },   // Bm
                new[] { 196.00, 246.94, 293.66, 392.00 },   // G
            };
            double[] bajos = { 73.42, 110.00, 123.47, 98.00 };
            for (int c = 0; c < 8; c++)
            {
                int k = c % 4;
                double ini = c * 4 * beat;
                double dur = 4 * beat;
                foreach (double f in acordes[k]) Voz(pad, sierraPad, sr, ini, dur, 0.55, f, 0.55, 0.20, 0.0035, 0.0, 0.0);
                Voz(pad, sierraPad, sr, ini, dur, 0.4, bajos[k], 0.15, 0.55, 0.002, 0.0, 0.0);
            }
            // Final: Re mayor sostenido dos compases con release largo hasta el fin del buffer.
            {
                double ini = 8 * 4 * beat, dur = 8 * beat;
                foreach (double f in acordes[0]) Voz(pad, sierraPad, sr, ini, dur, 1.2, f, 0.35, 0.26, 0.0035, 0.0, 0.0);
                Voz(pad, sierraPad, sr, ini, dur, 1.0, bajos[0], 0.1, 0.65, 0.002, 0.0, 0.0);
                Voz(pad, sierraPad, sr, ini, dur, 1.0, bajos[0] * 2.0, 0.1, 0.30, 0.002, 0.0, 0.0);
            }
            PasaBajos(pad, sr, 1500.0);

            // Melodia (inicioEnTiempos, duracionEnTiempos, Hz, velocidad). Notas del acorde en los tiempos fuertes.
            double[][] melodia =
            {
                // vuelta 1: cuerno suave, una octava abajo
                new[] { 0, 2, 369.99, 0.45 }, new[] { 2, 2, 440.00, 0.45 },
                new[] { 4, 2, 329.63, 0.45 }, new[] { 6, 2, 440.00, 0.45 },
                new[] { 8, 2, 293.66, 0.45 }, new[] { 10, 2, 369.99, 0.45 },
                new[] { 12, 2, 246.94, 0.45 }, new[] { 14, 2, 293.66, 0.45 },
                // vuelta 2: bronces a plena fuerza
                new[] { 16, 2, 440.00, 0.9 }, new[] { 18, 1, 587.33, 0.9 }, new[] { 19, 1, 739.99, 0.9 },
                new[] { 20, 2, 659.25, 0.9 }, new[] { 22, 1, 554.37, 0.9 }, new[] { 23, 1, 659.25, 0.9 },
                new[] { 24, 2, 739.99, 0.9 }, new[] { 26, 1, 587.33, 0.9 }, new[] { 27, 1, 493.88, 0.9 },
                new[] { 28, 2, 587.33, 0.9 }, new[] { 30, 1, 493.88, 0.9 }, new[] { 31, 1, 440.00, 0.9 },
                // cierre
                new[] { 32, 2, 739.99, 1.0 }, new[] { 34, 2, 880.00, 1.0 },
            };
            foreach (var m in melodia)
            {
                bool suave = m[3] < 0.6;
                // Los bronces entran con ataque mas franco; el cuerno suave, redondo.
                Voz(bronce, sierraBronce, sr, m[0] * beat, m[1] * beat * 0.97, 0.16, m[2], suave ? 0.14 : 0.06, m[3], 0.004, suave ? 0.003 : 0.0045, suave ? 0.0 : 0.35);
            }
            // Nota final larga (Re5) hasta el fin del clip.
            Voz(bronce, sierraBronce, sr, 36 * beat, 3.7 * beat, 1.6, 587.33, 0.05, 1.0, 0.004, 0.005, 0.5);
            Voz(bronce, sierraBronce, sr, 36 * beat, 3.7 * beat, 1.6, 293.66, 0.07, 0.8, 0.004, 0.003, 0.0);
            // Bronces saturados (tanh) y un poco de aire en los agudos.
            for (int i = 0; i < n; i++) bronce[i] = (float)System.Math.Tanh(2.2 * bronce[i]);
            PasaBajos(bronce, sr, 5200.0);

            // Percusion.
            for (int c = 0; c < 4; c++) { Timbal(perc, sr, c * 4 * beat, 0.55, rng); Timbal(perc, sr, (c * 4 + 2) * beat, 0.5, rng); }
            for (int c = 4; c < 8; c++)
            {
                Timbal(perc, sr, c * 4 * beat, 0.85, rng);
                Timbal(perc, sr, (c * 4 + 1) * beat, 0.42, rng);
                Timbal(perc, sr, (c * 4 + 2) * beat, 0.8, rng);
                if (c < 7) Timbal(perc, sr, (c * 4 + 3) * beat, 0.42, rng);
            }
            // Redoble corto al final de la vuelta 2 (tiempos 4 a 4 del compas 7, semicorcheas con crescendo).
            for (int k = 0; k < 7; k++) Timbal(perc, sr, (31.0 + k * 0.125) * beat, 0.35 + 0.08 * k, rng);
            // Compas 8: golpe grande con platillo y un segundo golpe.
            Timbal(perc, sr, 32 * beat, 1.0, rng);
            Platillo(perc, sr, 32 * beat, 2.4, 0.38, rng);
            Timbal(perc, sr, 34 * beat, 0.7, rng);
            // Compas 9: redoble final con crescendo y golpe de cierre con platillo largo.
            int golpes = 14;
            for (int k = 0; k < golpes; k++) Timbal(perc, sr, (36.0 + k * 0.25) * beat, 0.3 + 0.7 * k / (golpes - 1.0), rng);
            Timbal(perc, sr, 39.5 * beat, 1.0, rng);
            Platillo(perc, sr, 39.5 * beat, 1.0, 0.42, rng);

            // Mezcla, compresion suave (tanh), normalizacion y fundidos de entrada/salida.
            var o = new float[n];
            double pico = 0, energia = 0;
            for (int i = 0; i < n; i++)
            {
                double v = pad[i] * 0.62 + bronce[i] * 0.62 + perc[i] * 0.85;
                v = System.Math.Tanh(1.25 * v);
                o[i] = (float)v;
                double a = v < 0 ? -v : v;
                if (a > pico) pico = a;
                energia += v * v;
            }
            // Nivel por RMS y no por pico: saturado como esta, normalizado al pico quedaba ~5x mas fuerte que la musica del juego
            // (SA_Accion rms 0.20 a volumen 0.35). Con rms 0.14 y volumen 0.95 queda ~2x: un climax, no un golpe al oido.
            double rms = System.Math.Sqrt(energia / n);
            double k2 = rms > 0.0001 ? 0.14 / rms : 1.0;
            if (pico * k2 > 0.9) k2 = 0.9 / pico;
            int fadeIn = (int)(0.05 * sr), fadeOut = (int)(0.4 * sr);
            for (int i = 0; i < n; i++)
            {
                double g = k2;
                if (i < fadeIn) g *= (double)i / fadeIn;
                if (i >= n - fadeOut) g *= (double)(n - i) / fadeOut;
                o[i] = (float)(o[i] * g);
            }
            return o;
        }

        // Clip del himno (hilo principal). Para generarlo en un hilo se usa HimnoDeVictoriaDatos y se crea el clip despues.
        public static AudioClip ClipDelHimno(float[] datos, int sr = HimnoSR)
        {
            var clip = AudioClip.Create("HimnoVictoria", datos.Length, 1, sr, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }
}
