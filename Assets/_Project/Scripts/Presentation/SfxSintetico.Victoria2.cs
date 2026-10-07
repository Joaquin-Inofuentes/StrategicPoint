using UnityEngine;

namespace SP.Presentation
{
    // #129: himno de victoria v2, sintetizado por codigo (nada descargado). ESTEREO, 44,1 kHz, ~38 s en Re mayor a 100 bpm, 15 compases:
    //   0-1    intro: pad de cuerdas que crece, arpegios de arpa y redoble de timbales en crescendo
    //   2-5    tema A (cuerno suave) sobre I-V-vi-IV con cuerdas, violonchelos, arpegios y timbal en 1 y 3
    //   6-9    tema B (bronces) una octava arriba, caja de marcha, timbal en los 4 tiempos y platillo invertido de entrada
    //   10-13  climax vi-IV-I-V: bronces a plena fuerza con coro (osciladores desafinados con vibrato), redobles y platillo invertido
    //   14     acorde final de Re con todo el conjunto, golpe de timbal grave y platillo largo
    // Pad de cuerdas = cinco osciladores diente de sierra desafinados repartidos en el panorama + filtro paso bajo; bronces = sierra de
    // banda limitada saturada (tanh) con ataque franco y vibrato tardio; percusion sintetizada (timbal, caja, platillo); reverb de sala por
    // lineas de retardo (peines + pasatodo, un juego por canal); normalizacion por RMS y limitador suave. Solo System.Math: se calcula en un hilo.
    public static partial class SfxSintetico
    {
        public const int HimnoEpicoSR = 44100;
        public const double HimnoEpicoBpm = 100.0;
        public const int HimnoEpicoCompases = 15;
        public const double HimnoEpicoColaSegundos = 2.4;

        // ---- voces ----
        // Voz sostenida estereo: 'voces' osciladores desafinados (+-cents) repartidos en el panorama (pan +- spread); ataque lineal y release exponencial.
        static void VozE(float[] L, float[] R, float[] tabla, float[] seno, int sr, double ini, double dur, double rel, double f, double atk, double gan,
                         int voces, double cents, double vib, double pan, double spread)
        {
            int i0 = (int)(ini * sr), nSost = (int)(dur * sr), nRel = (int)(rel * sr), n = nSost + nRel;
            int nAtk = System.Math.Max(1, (int)(atk * sr));
            double decay = System.Math.Exp(-5.0 / System.Math.Max(1.0, rel * sr));
            double escala = gan / System.Math.Sqrt(voces);
            for (int v = 0; v < voces; v++)
            {
                double pos = voces > 1 ? (double)v / (voces - 1) * 2.0 - 1.0 : 0.0;
                double ratio = System.Math.Pow(2.0, pos * cents / 1200.0);
                double p = System.Math.Max(-1.0, System.Math.Min(1.0, pan + spread * pos));
                double ang = (p + 1.0) * System.Math.PI / 4.0;
                float gl = (float)(System.Math.Cos(ang) * escala), gr = (float)(System.Math.Sin(ang) * escala);
                double fase = (v * 0.37) % 1.0, lfo = v * 0.21, relEnv = 1.0;
                double paso = f * ratio / sr;
                int vibN = (int)(0.45 * sr);
                for (int i = 0; i < n; i++)
                {
                    int idx = i0 + i;
                    if (idx >= L.Length) break;
                    double env = i < nAtk ? (double)i / nAtk : 1.0;
                    if (i >= nSost) { relEnv *= decay; env *= relEnv; }
                    double vm = 1.0;
                    if (vib > 0.0)
                    {
                        lfo += 5.3 / sr; if (lfo >= 1.0) lfo -= 1.0;
                        vm = 1.0 + vib * Leer(seno, lfo) * (i < vibN ? (double)i / vibN : 1.0);
                    }
                    fase += paso * vm; if (fase >= 1.0) fase -= 1.0;
                    float s = Leer(tabla, fase) * (float)env;
                    L[idx] += s * gl; R[idx] += s * gr;
                }
            }
        }

        // Nota de arpa: seno + armonicos con caida exponencial.
        static void Arpa(float[] L, float[] R, int sr, double ini, double f, double vel, double pan)
        {
            int i0 = (int)(ini * sr), n = (int)(1.1 * sr);
            double ang = (System.Math.Max(-1.0, System.Math.Min(1.0, pan)) + 1.0) * System.Math.PI / 4.0;
            float gl = (float)(System.Math.Cos(ang) * vel), gr = (float)(System.Math.Sin(ang) * vel);
            double f1 = 0, f2 = 0, f3 = 0, e = 1.0, d = System.Math.Exp(-4.2 / sr), d2 = System.Math.Exp(-9.0 / sr);
            double e2 = 1.0;
            for (int i = 0; i < n && i0 + i < L.Length; i++)
            {
                f1 += f / sr; f2 += 2.0 * f / sr; f3 += 3.0 * f / sr;
                f1 -= System.Math.Floor(f1); f2 -= System.Math.Floor(f2); f3 -= System.Math.Floor(f3);
                e *= d; e2 *= d2;
                double ata = i < 80 ? i / 80.0 : 1.0;
                double s = (System.Math.Sin(2.0 * System.Math.PI * f1) + 0.45 * e2 * System.Math.Sin(2.0 * System.Math.PI * f2) + 0.2 * e2 * System.Math.Sin(2.0 * System.Math.PI * f3)) * e * ata;
                L[i0 + i] += (float)(s * gl); R[i0 + i] += (float)(s * gr);
            }
        }

        // Caja de marcha: ruido agudo corto + tono seco.
        static void Caja(float[] b, int sr, double ini, double vel, System.Random rng)
        {
            int i0 = (int)(ini * sr), n = (int)(0.28 * sr);
            double low = 0, fase = 0;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                double t = (double)i / sr;
                double blanco = rng.NextDouble() * 2.0 - 1.0;
                low += 0.18 * (blanco - low);
                fase += 2.0 * System.Math.PI * 195.0 / sr;
                double v = (blanco - low) * System.Math.Exp(-t * 26.0) + 0.35 * System.Math.Sin(fase) * System.Math.Exp(-t * 32.0);
                b[i0 + i] += (float)(v * vel * System.Math.Min(1.0, t / 0.001));
            }
        }

        // Platillo invertido: ruido agudo que crece hasta el golpe.
        static void PlatilloInvertido(float[] b, int sr, double ini, double dur, double gan, System.Random rng)
        {
            int i0 = (int)(ini * sr), n = (int)(dur * sr);
            double low = 0;
            for (int i = 0; i < n && i0 + i < b.Length; i++)
            {
                double u = (double)i / n;
                double blanco = rng.NextDouble() * 2.0 - 1.0;
                low += 0.3 * (blanco - low);
                b[i0 + i] += (float)((blanco - low) * u * u * u * gan);
            }
        }

        static void PasaBajosDoble(float[] b, int sr, double corte) { PasaBajos(b, sr, corte); PasaBajos(b, sr, corte); }

        // ---- reverb de sala: 4 peines (con amortiguacion) + 2 pasatodo por canal ----
        static float[] ReverbCanal(float[] entrada, int sr, double[] retardosMs, double feedback, double amort)
        {
            int n = entrada.Length;
            var salida = new float[n];
            for (int c = 0; c < retardosMs.Length; c++)
            {
                int d = (int)(retardosMs[c] * sr / 1000.0);
                var buf = new float[d];
                int pos = 0; float lp = 0f;
                for (int i = 0; i < n; i++)
                {
                    float y = buf[pos];
                    lp += (float)(amort * (y - lp));   // amortiguacion: cada vuelta pierde agudos
                    float v = entrada[i] + (float)feedback * lp;
                    buf[pos] = v;
                    if (++pos >= d) pos = 0;
                    salida[i] += y * 0.25f;
                }
            }
            // Dos pasatodo en serie (difusion).
            double[] ap = { 5.0, 1.7 };
            for (int a = 0; a < ap.Length; a++)
            {
                int d = (int)(ap[a] * sr / 1000.0);
                var buf = new float[d];
                int pos = 0; const float g = 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float x = salida[i];
                    float y = buf[pos];
                    float v = x + g * y;
                    buf[pos] = v;
                    salida[i] = y - g * v;
                    if (++pos >= d) pos = 0;
                }
            }
            return salida;
        }

        // La reverb corre a media frecuencia (la cola es oscura): envio mono de media tasa -> un juego de peines por canal -> se suma subiendo la tasa.
        static void AgregarReverb(float[] dstL, float[] dstR, float[] sendMonoHalf, int srHalf, double wet)
        {
            var rl = ReverbCanal(sendMonoHalf, srHalf, new[] { 29.7, 37.1, 41.1, 43.7 }, 0.87, 0.45);
            var rr = ReverbCanal(sendMonoHalf, srHalf, new[] { 30.6, 37.9, 42.0, 44.9 }, 0.87, 0.45);
            for (int i = 0; i < dstL.Length; i++) { dstL[i] += (float)(SubirMuestra(rl, i) * wet); dstR[i] += (float)(SubirMuestra(rr, i) * wet); }
        }

        // Muestra 'i' (a la tasa completa) de un buffer a media tasa, por interpolacion lineal.
        static float SubirMuestra(float[] mitad, int i)
        {
            int j = i >> 1;
            return (i & 1) == 0 ? mitad[j] : 0.5f * (mitad[j] + mitad[j + 1]);
        }

        // ---- el himno ----
        // Datos ESTEREO intercalados (L, R, L, R...). Seguro para llamar desde un hilo de fondo.
        public static float[] HimnoEpicoDatos(int sr = HimnoEpicoSR)
        {
            double beat = 60.0 / HimnoEpicoBpm;
            double bar = 4.0 * beat;
            int n = (int)((HimnoEpicoCompases * bar + HimnoEpicoColaSegundos) * sr);
            var rng = new System.Random(1290);
            var sierra = TablaSierra(9);
            var sierraB = TablaSierra(18);
            var seno = new float[TablaN + 1];
            for (int i = 0; i <= TablaN; i++) seno[i] = (float)System.Math.Sin(2.0 * System.Math.PI * i / TablaN);

            // Buses (estereo) por familia.
            // Cuerdas, bajos, arpa y coro (todo grave y filtrado por debajo de 2,6 kHz) se calculan a MEDIA frecuencia de muestreo y se
            // suben a 44,1 kHz al mezclar: la mitad de trabajo para el hilo. Los bronces (mas brillantes) y la percusion van a la completa.
            int sH = sr / 2, nH = n / 2 + 4;
            var cuerdasL = new float[nH]; var cuerdasR = new float[nH];
            var bronceL = new float[n]; var bronceR = new float[n];
            var bajoL = new float[nH]; var bajoR = new float[nH];
            var arpaL = new float[nH]; var arpaR = new float[nH];
            var coroL = new float[nH]; var coroR = new float[nH];
            var perc = new float[n];

            // Acordes por compas (D D | D A Bm G | D A Bm G | Bm G D A | D).
            int[] acordePorCompas = { 0, 0, 0, 1, 2, 3, 0, 1, 2, 3, 2, 3, 0, 1, 0 };
            double[][] acordes =
            {
                new[] { 146.83, 220.00, 293.66, 369.99 },    // D
                new[] { 164.81, 220.00, 277.18, 329.63 },    // A
                new[] { 185.00, 246.94, 293.66, 369.99 },    // Bm
                new[] { 196.00, 246.94, 293.66, 392.00 },    // G
            };
            double[] raices = { 73.42, 110.00, 123.47, 98.00 };

            for (int c = 0; c < HimnoEpicoCompases; c++)
            {
                int k = acordePorCompas[c];
                double ini = c * bar;
                bool ultimo = c == HimnoEpicoCompases - 1;
                double dur = ultimo ? bar : bar * 0.995;
                double rel = ultimo ? HimnoEpicoColaSegundos : 0.7;
                // intensidad por seccion
                double inten = c < 2 ? 0.45 + 0.25 * c : c < 6 ? 0.8 : c < 10 ? 0.95 : 1.15;
                // cuerdas: las cuatro notas del acorde, cinco voces desafinadas
                foreach (double f in acordes[k]) VozE(cuerdasL, cuerdasR, sierra, seno, sH, ini, dur, rel, f, c < 2 ? 0.9 : 0.5, 0.17 * inten, 4, 12.0, 0.0018, 0.0, 0.85);
                // octava arriba de cuerdas desde la entrada del tema B (brillo)
                if (c >= 6) foreach (double f in acordes[k]) VozE(cuerdasL, cuerdasR, sierra, seno, sH, ini, dur, rel, f * 2.0, 0.4, 0.07 * inten, 2, 9.0, 0.002, 0.0, 0.9);
                // violonchelos / contrabajo (raiz y quinta grave)
                VozE(bajoL, bajoR, sierra, seno, sH, ini, dur, rel, raices[k], c < 2 ? 0.5 : 0.12, 0.5 * (c < 2 ? 0.6 : 1.0), 2, 6.0, 0.0, 0.0, 0.2);
                if (c >= 2) VozE(bajoL, bajoR, sierra, seno, sH, ini, dur, rel, raices[k] * 2.0, 0.15, 0.22, 2, 6.0, 0.0, 0.0, 0.3);
                // arpegios de arpa (corcheas), mas suaves antes del climax
                if (c <= 9 || ultimo)
                {
                    int[] patron = { 0, 2, 1, 3, 2, 3, 1, 2 };
                    for (int j = 0; j < 8 && !ultimo; j++)
                    {
                        double f = acordes[k][patron[j]] * 2.0;
                        double vel = (c < 2 ? 0.14 : 0.1) * (0.8 + 0.2 * (j % 2 == 0 ? 1 : 0));
                        Arpa(arpaL, arpaR, sH, ini + j * beat * 0.5, f, vel, (j % 2 == 0 ? -0.45 : 0.45));
                    }
                    if (ultimo) for (int j = 0; j < 4; j++) Arpa(arpaL, arpaR, sH, ini + j * beat * 0.25, acordes[0][j] * 4.0, 0.12, (j % 2 == 0 ? -0.5 : 0.5));
                }
                // coro: del climax al final (sinusoides desafinadas con vibrato, octava arriba)
                if (c >= 10)
                    foreach (double f in acordes[k])
                    {
                        VozE(coroL, coroR, seno, seno, sH, ini, dur, rel, f * 2.0, 0.45, 0.12 * (ultimo ? 1.4 : 1.0), 3, 14.0, 0.0035, 0.0, 0.9);
                        VozE(coroL, coroR, seno, seno, sH, ini, dur, rel, f * 4.0, 0.45, 0.05, 2, 14.0, 0.0035, 0.0, 0.9);
                    }
            }

            // Melodia (compas absoluto, inicio en tiempos dentro del compas, duracion en tiempos, Hz). Tema A (2-5), B (6-9), C (10-13), final (14).
            // D4 293.66 E4 329.63 F#4 369.99 G4 392.00 A4 440.00 B4 493.88 C#5 554.37 D5 587.33 E5 659.25 F#5 739.99 G5 783.99 A5 880.00 B5 987.77
            double[][] tema =
            {
                // A: cuerno suave
                new[] { 2, 0, 2, 369.99 }, new[] { 2, 2, 1, 440.00 }, new[] { 2, 3, 1, 587.33 },
                new[] { 3, 0, 1.5, 554.37 }, new[] { 3, 1.5, 0.5, 493.88 }, new[] { 3, 2, 1, 440.00 }, new[] { 3, 3, 1, 659.25 },
                new[] { 4, 0, 2, 587.33 }, new[] { 4, 2, 1, 739.99 }, new[] { 4, 3, 0.5, 659.25 }, new[] { 4, 3.5, 0.5, 587.33 },
                new[] { 5, 0, 1.5, 493.88 }, new[] { 5, 1.5, 0.5, 587.33 }, new[] { 5, 2, 2, 783.99 },
                // B: bronces, una octava arriba en el cierre de cada frase
                new[] { 6, 0, 1, 587.33 }, new[] { 6, 1, 1, 739.99 }, new[] { 6, 2, 2, 880.00 },
                new[] { 7, 0, 2, 880.00 }, new[] { 7, 2, 1, 659.25 }, new[] { 7, 3, 1, 554.37 },
                new[] { 8, 0, 1, 987.77 }, new[] { 8, 1, 1, 880.00 }, new[] { 8, 2, 1, 739.99 }, new[] { 8, 3, 1, 587.33 },
                new[] { 9, 0, 1, 587.33 }, new[] { 9, 1, 1, 783.99 }, new[] { 9, 2, 2, 987.77 },
                // C: climax
                new[] { 10, 0, 3, 739.99 }, new[] { 10, 3, 1, 659.25 },
                new[] { 11, 0, 1, 587.33 }, new[] { 11, 1, 1, 783.99 }, new[] { 11, 2, 2, 987.77 },
                new[] { 12, 0, 3, 880.00 }, new[] { 12, 3, 1, 739.99 },
                new[] { 13, 0, 1, 659.25 }, new[] { 13, 1, 1, 554.37 }, new[] { 13, 2, 1, 659.25 }, new[] { 13, 3, 1, 880.00 },
            };
            foreach (var m in tema)
            {
                int c = (int)m[0];
                double ini = c * bar + m[1] * beat;
                double dur = m[2] * beat * 0.96;
                bool suave = c < 6;
                bool clim = c >= 10;
                double gan = suave ? 0.5 : clim ? 0.9 : 0.75;
                double f = m[3];
                // cuerno/trompeta principal
                VozE(bronceL, bronceR, sierraB, seno, sr, ini, dur, 0.2, f * (suave ? 0.5 : 1.0), suave ? 0.12 : 0.05, gan * 0.55, 3, 7.0, suave ? 0.003 : 0.0045, 0.0, 0.35);
                // trombon / segunda voz una octava abajo desde el tema B
                if (!suave) VozE(bronceL, bronceR, sierraB, seno, sr, ini, dur, 0.2, f * 0.5, 0.06, gan * 0.45, 3, 6.0, 0.003, 0.0, 0.3);
                if (clim) VozE(bronceL, bronceR, sierraB, seno, sr, ini, dur, 0.2, f * 0.75, 0.06, gan * 0.25, 2, 6.0, 0.003, 0.0, 0.3);   // quinta debajo: poder
            }
            // Nota final larga: Re5 y Re4 con todo el conjunto, mas Fa# para el acorde mayor.
            {
                double ini = 14 * bar;
                VozE(bronceL, bronceR, sierraB, seno, sr, ini, bar, HimnoEpicoColaSegundos, 587.33, 0.05, 0.62, 3, 7.0, 0.004, 0.0, 0.35);
                VozE(bronceL, bronceR, sierraB, seno, sr, ini, bar, HimnoEpicoColaSegundos, 293.66, 0.05, 0.5, 3, 6.0, 0.003, 0.0, 0.3);
                VozE(bronceL, bronceR, sierraB, seno, sr, ini, bar, HimnoEpicoColaSegundos, 739.99, 0.07, 0.3, 3, 7.0, 0.004, 0.0, 0.35);
                VozE(bronceL, bronceR, sierraB, seno, sr, ini, bar, HimnoEpicoColaSegundos, 1174.66, 0.08, 0.16, 2, 7.0, 0.004, 0.0, 0.35);
            }
            // Filtros por familia (suavizan el diente de sierra; los bronces ademas se saturan).
            PasaBajosDoble(cuerdasL, sH, 2600.0); PasaBajosDoble(cuerdasR, sH, 2600.0);
            PasaBajosDoble(bajoL, sH, 420.0); PasaBajosDoble(bajoR, sH, 420.0);
            PasaBajosDoble(coroL, sH, 2200.0); PasaBajosDoble(coroR, sH, 2200.0);
            for (int i = 0; i < n; i++)
            {
                bronceL[i] = (float)System.Math.Tanh(1.9 * bronceL[i]);
                bronceR[i] = (float)System.Math.Tanh(1.9 * bronceR[i]);
            }
            PasaBajos(bronceL, sr, 4800.0); PasaBajos(bronceR, sr, 4800.0);

            // Percusion (mono, al centro).
            for (int k = 0; k < 8; k++) Timbal(perc, sr, (1.0 * bar) + (2.0 + k * 0.25) * beat * 1.0, 0.25 + 0.09 * k, rng);   // redoble de la intro (compas 1, tiempos 3-4)
            Timbal(perc, sr, 2 * bar, 0.8, rng);
            for (int c = 2; c < 6; c++) { if (c > 2) Timbal(perc, sr, c * bar, 0.55, rng); Timbal(perc, sr, c * bar + 2 * beat, 0.5, rng); }
            for (int c = 6; c < 10; c++)
            {
                Timbal(perc, sr, c * bar, 0.9, rng); Timbal(perc, sr, c * bar + beat, 0.45, rng);
                Timbal(perc, sr, c * bar + 2 * beat, 0.8, rng); Timbal(perc, sr, c * bar + 3 * beat, 0.45, rng);
                Caja(perc, sr, c * bar + beat, 0.55, rng); Caja(perc, sr, c * bar + 3 * beat, 0.6, rng);
                Caja(perc, sr, c * bar + 3.5 * beat, 0.3, rng);
            }
            for (int c = 10; c < 14; c++)
            {
                Timbal(perc, sr, c * bar, 1.0, rng); Timbal(perc, sr, c * bar + beat, 0.6, rng);
                Timbal(perc, sr, c * bar + 2 * beat, 0.9, rng); Timbal(perc, sr, c * bar + 2.5 * beat, 0.5, rng); Timbal(perc, sr, c * bar + 3 * beat, 0.7, rng);
                Caja(perc, sr, c * bar + beat, 0.6, rng); Caja(perc, sr, c * bar + 3 * beat, 0.65, rng);
                if (c == 11 || c == 13) for (int k = 0; k < 8; k++) Caja(perc, sr, c * bar + (3.0 + k * 0.125) * beat, 0.25 + 0.05 * k, rng);   // redoble de caja de cierre de frase
            }
            // Final: golpe grave doble, platillo largo y redoble previo.
            Timbal(perc, sr, 14 * bar, 1.0, rng); Timbal(perc, sr, 14 * bar + 0.02, 0.85, rng);
            Timbal(perc, sr, 14 * bar + 2 * beat, 0.7, rng);
            Platillo(perc, sr, 14 * bar, 3.2, 0.42, rng);
            Platillo(perc, sr, 10 * bar, 2.2, 0.32, rng);
            Platillo(perc, sr, 6 * bar, 1.8, 0.24, rng);
            PlatilloInvertido(perc, sr, 6 * bar - 2.4, 2.4, 0.2, rng);
            PlatilloInvertido(perc, sr, 10 * bar - 2.4, 2.4, 0.22, rng);
            PlatilloInvertido(perc, sr, 14 * bar - 1.2, 1.2, 0.26, rng);

            // Mezcla: seco + reverb de sala (el envio a la reverb se calcula a media frecuencia).
            var L = new float[n]; var R = new float[n];
            var sendH = new float[nH];
            for (int i = 0; i < n; i++)
            {
                float cl = SubirMuestra(cuerdasL, i) * 0.80f + bronceL[i] * 0.78f + SubirMuestra(bajoL, i) * 0.70f + SubirMuestra(arpaL, i) * 0.55f + SubirMuestra(coroL, i) * 0.55f;
                float cr = SubirMuestra(cuerdasR, i) * 0.80f + bronceR[i] * 0.78f + SubirMuestra(bajoR, i) * 0.70f + SubirMuestra(arpaR, i) * 0.55f + SubirMuestra(coroR, i) * 0.55f;
                float p = perc[i] * 0.9f;
                L[i] = cl + p; R[i] = cr + p;
                sendH[i >> 1] += 0.5f * (0.9f * 0.5f * (cl + cr) + 0.35f * p);
            }
            AgregarReverb(L, R, sendH, sH, 0.42);

            // Normalizacion por RMS (como el himno anterior: un climax, no un golpe al oido) y limitador suave por encima de 0,6.
            double energia = 0;
            for (int i = 0; i < n; i++) { energia += (double)L[i] * L[i] + (double)R[i] * R[i]; }
            double rms = System.Math.Sqrt(energia / (2.0 * n));
            double k2 = rms > 0.0001 ? 0.15 / rms : 1.0;
            int fadeIn = (int)(0.04 * sr), fadeOut = (int)(0.6 * sr);
            var o = new float[2 * n];
            for (int i = 0; i < n; i++)
            {
                double g = k2;
                if (i < fadeIn) g *= (double)i / fadeIn;
                if (i >= n - fadeOut) g *= (double)(n - i) / fadeOut;
                o[2 * i] = LimitadorSuave((float)(L[i] * g));
                o[2 * i + 1] = LimitadorSuave((float)(R[i] * g));
            }
            return o;
        }

        // Lineal hasta 0,6; por encima se aplana suavemente hacia 0,95 (nunca pasa de ahi).
        static float LimitadorSuave(float x)
        {
            float a = x < 0f ? -x : x;
            if (a <= 0.6f) return x;
            float y = 0.6f + 0.35f * (float)System.Math.Tanh((a - 0.6f) / 0.35f);
            return x < 0f ? -y : y;
        }

        // Clip estereo del himno (hilo principal). 'datos' = intercalado L,R.
        public static AudioClip ClipDelHimnoEpico(float[] datos, int sr = HimnoEpicoSR)
        {
            var clip = AudioClip.Create("HimnoVictoria2", datos.Length / 2, 2, sr, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }
}
