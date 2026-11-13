using UnityEngine;

namespace SP.Presentation
{
    // Sonidos del perro enemigo y de los enemigos pesados/escudero (2026-11-13), sintetizados con un modelo fuente-filtro como una voz de verdad:
    // la glotis (tren de pulsos con vibrato, jitter y ruido de turbulencia) pasa por cuatro resonancias de garganta/boca que se mueven durante el
    // ladrido (la boca se abre: F1 sube, F2 baja). Sale un "guau" con cuerpo, aspereza y cola de eco corto. Soltar un .wav bajo
    // Resources/Audio/Sfx/Ladrido/ lo reemplaza (GenericSfx.Get busca primero ahi).
    public static partial class SfxSintetico
    {
        // Resonador de banda (RBJ, ganancia de pico constante) con coeficientes que se recalculan al mover la frecuencia.
        sealed class Resonador
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void Poner(float f, float q)
            {
                float w0 = 2f * Mathf.PI * Mathf.Clamp(f, 60f, SR * 0.45f) / SR, al = Mathf.Sin(w0) / (2f * q), c = Mathf.Cos(w0), a0 = 1f + al;
                b0 = al / a0; b1 = 0f; b2 = -al / a0; a1 = -2f * c / a0; a2 = (1f - al) / a0;
            }
            public float Paso(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y; return y;
            }
        }

        // Un ladrido: ataque de 6 ms, cuerpo y caida. f0 sube de golpe y baja (como el "gua-u"); las formantes siguen la apertura de la boca.
        static void UnLadrido(float[] b, float ini, float dur, float f0Pico, float fuerza, int seed)
        {
            int i0 = Mathf.Max(0, (int)(ini * SR));
            int n = Mathf.Min(b.Length - i0, (int)(dur * SR));
            var rng = new System.Random(seed);
            var r1 = new Resonador(); var r2 = new Resonador(); var r3 = new Resonador(); var r4 = new Resonador();
            double fase = 0.0; float jit = 0f, aire = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR, k = (float)i / n;
                // tono: ataque rapido hasta el pico y caida (curva de ladrido) + vibrato rapido + jitter lento
                float curva = k < 0.12f ? Mathf.Lerp(0.78f, 1f, k / 0.12f) : Mathf.Lerp(1f, 0.58f, Mathf.Pow((k - 0.12f) / 0.88f, 0.8f));
                jit = Mathf.Lerp(jit, ((float)rng.NextDouble() - 0.5f) * 0.05f, 0.002f);
                float f0 = f0Pico * curva * (1f + jit + 0.012f * Mathf.Sin(2f * Mathf.PI * 38f * t));
                fase += f0 / SR; if (fase >= 1.0) fase -= 1.0;
                float p = (float)fase;
                // fuente glotica: diente de sierra suavizado (muchos armonicos) + parte de pulso estrecho
                float saw = 1f - 2f * p;
                float pulso = p < 0.18f ? Mathf.Sin(Mathf.PI * p / 0.18f) : 0f;
                float fuente = saw * 0.55f + pulso * 0.9f;
                // aire de la turbulencia (mas fuerte al principio: el "gua")
                aire = Mathf.Lerp(aire, (float)rng.NextDouble() * 2f - 1f, 0.55f);
                fuente += aire * (0.42f - 0.22f * k) * fuerza;
                // formantes: la boca se abre hasta la mitad y se cierra
                float ap = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k * 1.15f));
                if ((i & 31) == 0)
                {
                    r1.Poner(Mathf.Lerp(520f, 880f, ap), 5.5f);
                    r2.Poner(Mathf.Lerp(1350f, 1050f, ap), 6f);
                    r3.Poner(2650f + 200f * ap, 7f);
                    r4.Poner(3900f, 8f);
                }
                float v = r1.Paso(fuente) * 1.6f + r2.Paso(fuente) * 1.15f + r3.Paso(fuente) * 0.7f + r4.Paso(fuente) * 0.35f + fuente * 0.12f;
                float env = Mathf.Min(1f, t / 0.006f) * Mathf.Pow(1f - k, 0.9f) * (k < 0.7f ? 1f : Mathf.Lerp(1f, 0.55f, (k - 0.7f) / 0.3f));
                b[i0 + i] += v * env * fuerza;
            }
        }

        // Ladrido(corto=false): "guau guau" (dos ladridos); corto=true: uno solo, seco, para el momento del salto.
        public static AudioClip Ladrido(bool corto)
        {
            var b = Buffer(corto ? 0.42f : 0.95f);
            if (corto) UnLadrido(b, 0.02f, 0.22f, 520f, 1f, 1101);
            else
            {
                UnLadrido(b, 0.03f, 0.25f, 500f, 1f, 1111);
                UnLadrido(b, 0.40f, 0.27f, 540f, 1.1f, 1112);
            }
            // eco de cuarto: dos repeticiones apagadas
            int d1 = (int)(0.075f * SR), d2 = (int)(0.16f * SR);
            for (int i = b.Length - 1; i >= 0; i--) { float e = 0f; if (i >= d1) e += b[i - d1] * 0.22f; if (i >= d2) e += b[i - d2] * 0.1f; b[i] += e; }
            return Cerrar(b, corto ? "LadridoCorto" : "Ladrido", 0.9f);
        }

        // Gruñido: f0 grave (~80 Hz) con temblor de amplitud a 26 Hz (la vibracion de la garganta) y ruido; las mismas resonancias, cerradas.
        public static AudioClip GruniidoDePerro()
        {
            var b = Buffer(1.0f);
            var r1 = new Resonador(); var r2 = new Resonador(); var r3 = new Resonador();
            var rng = new System.Random(1131); double fase = 0.0; float aire = 0f;
            r1.Poner(430f, 4f); r2.Poner(880f, 5f); r3.Poner(1900f, 6f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / SR, k = t / 1.0f;
                float f0 = 74f + 10f * Mathf.Sin(2f * Mathf.PI * 2.2f * t) + 6f * Mathf.Sin(2f * Mathf.PI * 0.9f * t);
                fase += f0 / SR; if (fase >= 1.0) fase -= 1.0;
                float p = (float)fase;
                float fuente = (1f - 2f * p) * 0.6f + (p < 0.12f ? Mathf.Sin(Mathf.PI * p / 0.12f) : 0f);
                aire = Mathf.Lerp(aire, (float)rng.NextDouble() * 2f - 1f, 0.35f);
                fuente += aire * 0.35f;
                float tiembla = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 26f * t);
                float v = r1.Paso(fuente) * 1.4f + r2.Paso(fuente) * 0.9f + r3.Paso(fuente) * 0.4f;
                float env = Mathf.Min(1f, t / 0.08f) * (k < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.75f) / 0.25f));
                b[i] += v * tiembla * env;
            }
            return Cerrar(b, "GruniidoDePerro", 0.7f);
        }

        // Pata sobre el piso: un toque seco y corto (ruido grave filtrado + un golpecito).
        public static AudioClip PasoDePerro()
        {
            var b = Buffer(0.09f);
            Ruido(b, 0f, 0.07f, 0.35f, 0.15f, 0.9f, 55f, 0.8f, 1141, 0.0008f);
            Tono(b, 0f, 0.05f, 210f, 120f, 70f, 0.5f, 0.1f, 0.0005f);
            return Cerrar(b, "PasoDePerro", 0.55f);
        }

        // Paso del pesado: el suelo retumba (golpe grave que cae de 95 a 38 Hz) con un crujido de botas.
        public static AudioClip PasoPesado()
        {
            var b = Buffer(0.4f);
            Tono(b, 0f, 0.32f, 95f, 38f, 9f, 1f, 0.35f, 0.002f);
            Ruido(b, 0f, 0.12f, 0.25f, 0.1f, 0.9f, 28f, 0.55f, 1151, 0.001f);
            Ruido(b, 0.01f, 0.05f, 0.6f, 0.4f, 0.8f, 60f, 0.25f, 1152, 0.0005f);
            return Cerrar(b, "PasoPesado", 0.85f);
        }

        // La bala contra el escudo: tañido metalico seco con un golpe de chapa.
        public static AudioClip ImpactoEscudo()
        {
            var b = Buffer(0.5f);
            Metal(b, 0f, 0.45f, new[] { 520f, 1210f, 1980f, 2860f, 4100f }, 11f, 0.7f, 1161, 0.05f);
            Ruido(b, 0f, 0.025f, 0.8f, 0.5f, 0.7f, 90f, 0.9f, 1162, 0.0003f);
            Tono(b, 0f, 0.06f, 330f, 180f, 40f, 0.5f, 0.2f, 0.0005f);
            return Cerrar(b, "ImpactoEscudo", 0.8f);
        }
    }
}
