using UnityEngine;

namespace SP.Presentation
{
    // Sintesis por codigo de los sonidos de la Operacion Cuartel (todo CC0 por construccion: se genera con matematica, no se descargo
    // nada). Un .wav bajo Resources/Audio/Sfx/<Clave>/ sigue teniendo prioridad (ver GenericSfx.Get).
    //   Logro             muerte de un enemigo: arpegio de campanitas, agradable y corto (sensacion de "logro")
    //   ObjetivoCumplido  fin de un objetivo: el "flash" de un tajo (como cortar un zapallo) + campana
    //   ImpactoPesado     obus de tanque: golpe seco + trueno grave + escombros
    //   BalaImpacto       bala contra metal/vehiculo: crack agudo + ping de rebote
    //   Orugas            lazo de orugas de tanque a toda maquina (para AudioSource 3D)
    //   MotorCamioneta    lazo de motor de camioneta forzado
    //   AmbienteDesierto  lazo de viento seco del desierto
    public static partial class SfxSintetico
    {
        // Campana: parciales inarmonicos (1 : 2.76 : 5.4 : 8.93) con decaimiento distinto, mas un golpe corto de ruido.
        static void Campana(float[] b, float ini, float f, float decay, float gain)
        {
            int i0 = Mathf.Max(0, (int)(ini * SR));
            int n = b.Length - i0;
            double p1 = 0, p2 = 0, p3 = 0, p4 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                p1 += 2.0 * System.Math.PI * f / SR;
                p2 += 2.0 * System.Math.PI * f * 2.76 / SR;
                p3 += 2.0 * System.Math.PI * f * 5.4 / SR;
                p4 += 2.0 * System.Math.PI * f * 8.93 / SR;
                float ataque = Mathf.Min(1f, t / 0.002f);
                float v = (float)System.Math.Sin(p1) * Mathf.Exp(-decay * t)
                        + 0.45f * (float)System.Math.Sin(p2) * Mathf.Exp(-decay * 1.7f * t)
                        + 0.22f * (float)System.Math.Sin(p3) * Mathf.Exp(-decay * 2.6f * t)
                        + 0.10f * (float)System.Math.Sin(p4) * Mathf.Exp(-decay * 4.2f * t);
                b[i0 + i] += v * ataque * gain;
            }
        }

        // Arpegio ascendente C6-E6-G6-C7 de campanitas con brillo: "logro" de videojuego, agradable y breve.
        public static AudioClip Logro()
        {
            var b = Buffer(0.95f);
            float[] notas = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int i = 0; i < notas.Length; i++)
                Campana(b, i * 0.06f, notas[i], 7.5f - i * 0.6f, 0.55f + i * 0.12f);
            // Brillo: un soplido agudo al arranque y una octava suave debajo para que tenga cuerpo.
            Ruido(b, 0f, 0.09f, 0.3f, 0.12f, 0.9f, 40f, 0.18f, 701, 0.001f);
            Tono(b, 0f, 0.5f, 523.25f, 523.25f, 5f, 0.28f, 0.25f, 0.004f);
            return Cerrar(b, "Logro", 0.8f);
        }

        // "Flash" de un tajo (cortar un zapallo: barrido agudo seco + golpe humedo y crujiente) y despues una campana de ping, doble.
        public static AudioClip ObjetivoCumplido()
        {
            var b = Buffer(2.1f);
            // Tajo: el filtro se abre rapido (fiuuu) y corta de golpe.
            Ruido(b, 0f, 0.13f, 0.78f, 0.12f, 0.992f, 12f, 0.85f, 711, 0.006f);
            Tono(b, 0f, 0.12f, 2600f, 5200f, 20f, 0.12f, 0.1f, 0.004f);
            // Golpe en el zapallo: thud grave + crujido de pulpa.
            Tono(b, 0.115f, 0.16f, 170f, 62f, 24f, 1.0f, 0.12f, 0.001f);
            Ruido(b, 0.115f, 0.11f, 0.55f, 0.35f, 0.985f, 30f, 0.8f, 712, 0.0005f);
            var rng = new System.Random(713);
            for (int i = 0; i < 7; i++) Clac(b, 0.13f + (float)rng.NextDouble() * 0.12f, 0.13f, 720 + i, 0.3f + (float)rng.NextDouble() * 0.5f);
            // Campana: ping y su quinta, un poco despues.
            Campana(b, 0.2f, 1318.5f, 3.4f, 0.8f);
            Campana(b, 0.42f, 1975.5f, 3.0f, 0.6f);
            return Cerrar(b, "ObjetivoCumplido", 0.9f);
        }

        // Obus de tanque: crack de ataque, golpe seco, trueno grave y escombros. Mas corto y "de adentro" que Explosion.
        public static AudioClip ImpactoPesado()
        {
            var b = Buffer(1.5f);
            Ruido(b, 0f, 0.05f, 0.5f, 0.5f, 0.97f, 60f, 1.0f, 741, 0.0003f);
            Tono(b, 0f, 0.6f, 95f, 30f, 6f, 1.0f, 0.12f, 0.002f);
            Ruido(b, 0f, 0.9f, 0.96f, 0.93f, 0.9985f, 3.4f, 0.8f, 742, 0.002f);
            var rng = new System.Random(743);
            for (int i = 0; i < 16; i++)
            {
                float t = 0.12f + (float)rng.NextDouble() * 0.9f;
                Clac(b, t, 0.2f * (1f - t / 1.2f), 750 + i, 0.4f + (float)rng.NextDouble() * 0.9f);
            }
            return Cerrar(b, "ImpactoPesado", 0.9f);
        }

        // Bala contra metal: "tac" agudo seco + ping metalico corto que rebota.
        public static AudioClip BalaImpacto()
        {
            var b = Buffer(0.42f);
            Ruido(b, 0f, 0.02f, 0.3f, 0.3f, 0.97f, 90f, 1.0f, 761, 0.0002f);
            Metal(b, 0f, 0.35f, new[] { 1850f, 2900f, 4300f }, 16f, 0.5f, 762, 0.03f);
            Tono(b, 0.02f, 0.25f, 3400f, 1500f, 14f, 0.22f, 0.1f, 0.001f);   // el "piuu" del rebote
            return Cerrar(b, "BalaImpacto", 0.85f);
        }

        // ------------------------------------------------------------------
        // Lazos (se cierran con un fundido cruzado para que no haya salto al repetir)
        // ------------------------------------------------------------------
        static AudioClip Lazo(float[] largo, int muestras, string nombre, float pico)
        {
            int fundido = largo.Length - muestras;
            var r = new float[muestras];
            for (int i = 0; i < muestras; i++)
            {
                if (i >= fundido) r[i] = largo[i];
                else { float k = (float)i / Mathf.Max(1, fundido); r[i] = Mathf.Lerp(largo[muestras + i], largo[i], k); }
            }
            float max = 0f;
            for (int i = 0; i < r.Length; i++) { float a = r[i] < 0f ? -r[i] : r[i]; if (a > max) max = a; }
            float g = max > 0.0001f ? pico / max : 1f;
            for (int i = 0; i < r.Length; i++) r[i] = Mathf.Clamp(r[i] * g, -1f, 1f);
            var clip = AudioClip.Create(nombre, r.Length, 1, SR, false);
            clip.SetData(r, 0);
            return clip;
        }

        // Orugas a toda maquina: motor diesel grave + el "clac-clac-clac" de los eslabones golpeando las ruedas (16 por segundo).
        public static AudioClip Orugas()
        {
            const float dur = 2.0f, f = 0.2f;
            int n = (int)(dur * SR);
            var b = Buffer(dur + f);
            // Motor: dos senos graves con modulacion de ralenti + ruido grave.
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR;
                float mod = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                b[i] += 0.55f * Mathf.Sin(2f * Mathf.PI * 46f * mod * t) + 0.30f * Mathf.Sin(2f * Mathf.PI * 92f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 138f * t);
            }
            Ruido(b, 0f, dur + f, 0.985f, 0.985f, 0.9995f, 0f, 0.9f, 771, 0f);
            // Eslabones: 32 golpes en 2 s (16 Hz), cada uno un clac metalico con jitter.
            var rng = new System.Random(772);
            for (int i = 0; i < 34; i++)
            {
                float t = i * (dur / 32f) + ((float)rng.NextDouble() - 0.5f) * 0.012f;
                Clac(b, Mathf.Max(0f, t), 0.30f + 0.12f * (float)rng.NextDouble(), 780 + i, 0.5f + (float)rng.NextDouble() * 0.6f);
                if (i % 2 == 0) Tunc(b, Mathf.Max(0f, t) + 0.004f, 0.14f, 820 + i, 1.4f);
            }
            return Lazo(b, n, "Orugas", 0.9f);
        }

        // Camioneta forzada: motor de 4 cilindros alto de vueltas + ruido de viento/rodado.
        public static AudioClip MotorCamioneta()
        {
            const float dur = 1.0f, f = 0.15f;
            int n = (int)(dur * SR);
            var b = Buffer(dur + f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR;
                float fase = 2f * Mathf.PI * 118f * t;
                // Onda diente de sierra suave (suma de armonicos) = rugido de motor.
                float v = 0f;
                for (int h = 1; h <= 6; h++) v += Mathf.Sin(fase * h) / h;
                float am = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 236f * t);
                b[i] += v * am * 0.35f;
            }
            Ruido(b, 0f, dur + f, 0.7f, 0.7f, 0.97f, 0f, 0.12f, 791, 0f);
            Ruido(b, 0f, dur + f, 0.96f, 0.96f, 0.999f, 0f, 0.5f, 792, 0f);
            return Lazo(b, n, "MotorCamioneta", 0.85f);
        }

        // Bug #058 ("quiero escuchar el sonido de las ruedas de los enemigos"): rodado de neumaticos sobre asfalto con tierra.
        // Retumbo grave del caucho + siseo del dibujo de la cubierta + piedritas que saltan; el volumen ondula con el giro de
        // la rueda. Se sube de volumen y de tono con la velocidad (OperacionAuto.TickRastro).
        public static AudioClip RuedasCamioneta()
        {
            const float dur = 1.6f, f = 0.2f;
            int n = (int)(dur * SR);
            var b = Buffer(dur + f);
            Ruido(b, 0f, dur + f, 0.975f, 0.975f, 0.999f, 0f, 1.0f, 811, 0f);    // retumbo
            Ruido(b, 0f, dur + f, 0.55f, 0.55f, 0.94f, 0f, 0.18f, 812, 0f);      // siseo del dibujo
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR;
                b[i] *= 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 9f * t);       // vuelta de rueda
            }
            var rng = new System.Random(813);
            for (int i = 0; i < 26; i++)
                Clac(b, (float)rng.NextDouble() * dur, 0.08f + 0.1f * (float)rng.NextDouble(), 830 + i, 1.3f + (float)rng.NextDouble() * 0.8f);
            return Lazo(b, n, "RuedasCamioneta", 0.8f);
        }

        // Viento seco del desierto: ruido pasa-banda cuyo corte y volumen ondulan despacio (rafagas), mas un zumbido grave lejano.
        public static AudioClip AmbienteDesierto()
        {
            const float dur = 10f, f = 1.2f;
            int n = (int)(dur * SR);
            var b = Buffer(dur + f);
            var rng = new System.Random(801);
            float low = 0f, sub = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR;
                float blanco = (float)rng.NextDouble() * 2f - 1f;
                float corte = 0.93f + 0.05f * Mathf.Sin(2f * Mathf.PI * 0.11f * t) + 0.02f * Mathf.Sin(2f * Mathf.PI * 0.37f * t + 1.3f);
                low = Mathf.Lerp(blanco, low, corte);
                sub = Mathf.Lerp(low, sub, 0.998f);
                float rafaga = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.13f * t + 0.6f) * Mathf.Sin(2f * Mathf.PI * 0.053f * t + 2f);
                b[i] += (low - sub) * rafaga * 3.2f;
                b[i] += 0.05f * Mathf.Sin(2f * Mathf.PI * 52f * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.07f * t));
            }
            return Lazo(b, n, "AmbienteDesierto", 0.55f);
        }
    }
}
