using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // Sintesis por codigo de los sonidos de combate que no tienen grabacion real (ronda 7):
    // explosion, cohete, granada, cuchillo, recargas y desenfundes POR ARMA, radial, curacion y
    // carga de demolicion. Todo es deterministico (semilla fija por sonido) para que dos
    // corridas suenen igual, y cada clip se normaliza al pico para que el volumen se calibre en
    // el punto de llamada y no aqui. GenericSfx cae aca cuando no hay un clip real bajo
    // Resources/Audio/Sfx/<Clave>/ (asi el usuario puede reemplazar cualquiera soltando un .wav).
    public static class SfxSintetico
    {
        const int SR = 44100;

        // ------------------------------------------------------------------
        // Herramientas: capas que se SUMAN en un mismo buffer
        // ------------------------------------------------------------------
        static float[] Buffer(float duracion) => new float[Mathf.Max(1, (int)(duracion * SR))];

        // Ruido pasado por dos filtros de un polo (banda). lowIni->lowFin mueve el corte durante la
        // capa (un "fiush" abre el filtro), bell = envolvente de campana en vez de exponencial.
        static void Ruido(float[] b, float ini, float dur, float lowIni, float lowFin, float hi, float decay, float gain, int seed, float ataque = 0.002f, bool campana = false)
        {
            int i0 = Mathf.Max(0, (int)(ini * SR));
            int n = Mathf.Min(b.Length - i0, (int)(dur * SR));
            var rng = new System.Random(seed);
            float low = 0f, sub = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float k = (float)i / Mathf.Max(1, n);
                float lowAmt = Mathf.Lerp(lowIni, lowFin, k);
                float blanco = (float)rng.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(blanco, low, lowAmt);
                sub = Mathf.Lerp(low, sub, hi);
                float env = campana ? Mathf.Sin(Mathf.PI * k) : Mathf.Exp(-decay * t) * (ataque <= 0f ? 1f : Mathf.Min(1f, t / ataque));
                b[i0 + i] += (low - sub) * env * gain;
            }
        }

        // Tono con barrido de frecuencia (fase acumulada: sin clicks) y un armonico.
        static void Tono(float[] b, float ini, float dur, float f0, float f1, float decay, float gain, float armonico = 0.15f, float ataque = 0.003f)
        {
            int i0 = Mathf.Max(0, (int)(ini * SR));
            int n = Mathf.Min(b.Length - i0, (int)(dur * SR));
            double fase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float f = Mathf.Lerp(f0, f1, (float)i / Mathf.Max(1, n));
                fase += 2.0 * System.Math.PI * f / SR;
                float env = Mathf.Exp(-decay * t) * Mathf.Min(1f, t / Mathf.Max(0.0005f, ataque));
                b[i0 + i] += ((float)System.Math.Sin(fase) + armonico * (float)System.Math.Sin(fase * 2.0)) * env * gain;
            }
        }

        // Suma de parciales inarmonicos = metal. Con un poco de ruido para el "tsss" del golpe.
        static void Metal(float[] b, float ini, float dur, float[] parciales, float decay, float gain, int seed, float ruido = 0.08f)
        {
            int i0 = Mathf.Max(0, (int)(ini * SR));
            int n = Mathf.Min(b.Length - i0, (int)(dur * SR));
            var rng = new System.Random(seed);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float v = 0f;
                for (int p = 0; p < parciales.Length; p++) v += Mathf.Sin(2f * Mathf.PI * parciales[p] * t) / (1f + p * 0.7f);
                v += ((float)rng.NextDouble() - 0.5f) * ruido * 2f;
                b[i0 + i] += v * Mathf.Exp(-decay * t) * gain;
            }
        }

        static readonly float[] ParcialesSeco = { 2300f, 3700f, 5200f };
        static readonly float[] ParcialesGrave = { 620f, 1370f, 2480f };

        // "Clac" mecanico: impacto seco + un ping metalico corto.
        static void Clac(float[] b, float t, float gain, int seed, float tono = 1f)
        {
            Ruido(b, t, 0.012f, 0.35f, 0.35f, 0.98f, 90f, gain * 1.1f, seed, 0.0004f);
            Metal(b, t, 0.05f, new[] { 2300f * tono, 3700f * tono, 5200f * tono }, 70f, gain * 0.6f, seed + 1, 0.02f);
        }

        // "Tunc": golpe sordo (cargador que entra, tapa que cierra).
        static void Tunc(float[] b, float t, float gain, int seed, float grave = 1f)
        {
            Ruido(b, t, 0.07f, 0.97f, 0.97f, 0.999f, 42f, gain * 1.4f, seed, 0.001f);
            Tono(b, t, 0.09f, 150f * grave, 80f * grave, 34f, gain * 0.7f);
        }

        // Roce/deslizamiento (corredera, cerrojo, tubo).
        static void Roce(float[] b, float t, float dur, float gain, int seed)
        {
            Ruido(b, t, dur, 0.8f, 0.55f, 0.99f, 0f, gain, seed, 0f, true);
        }

        static void Traqueteo(float[] b, float t, float dur, int golpes, float gain, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < golpes; i++)
            {
                float ti = t + (float)rng.NextDouble() * dur;
                Clac(b, ti, gain * (0.5f + (float)rng.NextDouble() * 0.5f), seed + 10 + i, 0.7f + (float)rng.NextDouble() * 0.6f);
            }
        }

        static AudioClip Cerrar(float[] b, string nombre, float pico)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++) { float a = b[i] < 0f ? -b[i] : b[i]; if (a > max) max = a; }
            float g = max > 0.0001f ? pico / max : 1f;
            // Fade-out de 6 ms: si el ultimo sample no es cero, el fin del clip hace "click".
            int fade = Mathf.Min(b.Length, (int)(0.006f * SR));
            for (int i = 0; i < b.Length; i++)
            {
                float v = b[i] * g;
                if (i >= b.Length - fade) v *= (float)(b.Length - i) / fade;
                b[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create(nombre, b.Length, 1, SR, false);
            clip.SetData(b, 0);
            return clip;
        }

        // ------------------------------------------------------------------
        // Explosion y cohete
        // ------------------------------------------------------------------
        // Estruendo: trueno grave largo + cuerpo medio + crack de ataque + cola de escombros que caen.
        public static AudioClip Explosion()
        {
            var b = Buffer(2.4f);
            Ruido(b, 0f, 2.2f, 0.992f, 0.992f, 0.9993f, 2.1f, 1.1f, 301, 0.004f);
            Ruido(b, 0f, 1.0f, 0.93f, 0.93f, 0.995f, 4.2f, 0.9f, 302, 0.001f);
            Ruido(b, 0f, 0.14f, 0.55f, 0.55f, 0.985f, 28f, 1.0f, 303, 0.0005f);
            Tono(b, 0f, 1.5f, 115f, 26f, 2.6f, 1.0f, 0.1f, 0.004f);
            var rng = new System.Random(304);
            for (int i = 0; i < 34; i++)
            {
                float t = 0.28f + (float)rng.NextDouble() * 1.5f;
                Clac(b, t, 0.16f * (1f - t / 2.1f), 320 + i, 0.4f + (float)rng.NextDouble() * 0.8f);
                Tunc(b, t + 0.01f, 0.1f * (1f - t / 2.1f), 360 + i, 1.2f);
            }
            return Cerrar(b, "Explosion", 0.95f);
        }

        // Disparo del lanzacohetes: golpe de la carga, ignicion y el "fiushhh" del cohete que se aleja.
        public static AudioClip LanzamientoDeCohete()
        {
            var b = Buffer(1.9f);
            Tono(b, 0f, 0.3f, 95f, 42f, 11f, 1.0f, 0.1f);
            Ruido(b, 0f, 0.09f, 0.6f, 0.6f, 0.98f, 34f, 0.8f, 331, 0.0005f);
            // El "fiush": ruido cuyo filtro se ABRE (mas agudo) mientras se aleja y se apaga de a poco.
            Ruido(b, 0.03f, 1.8f, 0.95f, 0.5f, 0.99f, 2.4f, 1.1f, 332, 0.14f);
            Ruido(b, 0.1f, 1.3f, 0.75f, 0.35f, 0.99f, 3.4f, 0.35f, 333, 0.2f);
            Tono(b, 0.05f, 1.2f, 420f, 1300f, 2.6f, 0.13f, 0.4f, 0.15f);
            return Cerrar(b, "Shot_Rocket", 0.95f);
        }

        // ------------------------------------------------------------------
        // Granada y cuchillo
        // ------------------------------------------------------------------
        public static AudioClip GranadaSeguro()
        {
            var b = Buffer(0.34f);
            Clac(b, 0f, 0.9f, 401, 0.8f);
            Metal(b, 0.02f, 0.3f, ParcialesSeco, 16f, 0.7f, 402, 0.04f);
            Metal(b, 0.13f, 0.2f, new[] { 3100f, 4400f }, 26f, 0.35f, 403, 0.02f);
            return Cerrar(b, "GrenadePin", 0.85f);
        }

        public static AudioClip GranadaLanzada()
        {
            var b = Buffer(0.4f);
            Ruido(b, 0f, 0.36f, 0.9f, 0.5f, 0.99f, 0f, 1f, 411, 0f, true);
            Tono(b, 0.02f, 0.2f, 240f, 130f, 14f, 0.25f);
            return Cerrar(b, "GrenadeThrow", 0.75f);
        }

        public static AudioClip GranadaRebote()
        {
            var b = Buffer(0.3f);
            Metal(b, 0f, 0.28f, new[] { 720f, 1310f, 2150f, 3300f }, 24f, 0.9f, 421, 0.12f);
            Tunc(b, 0f, 0.7f, 422, 1.1f);
            return Cerrar(b, "GrenadeBounce", 0.85f);
        }

        public static AudioClip CuchilloTajo()
        {
            var b = Buffer(0.26f);
            Ruido(b, 0f, 0.24f, 0.6f, 0.15f, 0.97f, 0f, 1f, 431, 0f, true);
            Metal(b, 0.05f, 0.15f, new[] { 3800f, 5400f }, 30f, 0.12f, 432, 0.02f);
            return Cerrar(b, "KnifeSwing", 0.7f);
        }

        // Golpe que acierta: hoja que entra (roce hmedo), golpe sordo y un zumbido metalico corto.
        public static AudioClip CuchilloImpacto()
        {
            var b = Buffer(0.34f);
            Ruido(b, 0f, 0.1f, 0.5f, 0.3f, 0.97f, 22f, 0.9f, 441, 0.0008f);
            Tunc(b, 0.005f, 1.1f, 442, 1.25f);
            Metal(b, 0.01f, 0.28f, new[] { 2600f, 4100f }, 34f, 0.3f, 443, 0.03f);
            return Cerrar(b, "KnifeHit", 0.9f);
        }

        // ------------------------------------------------------------------
        // Movimiento
        // ------------------------------------------------------------------
        public static AudioClip Salto()
        {
            var b = Buffer(0.22f);
            Ruido(b, 0f, 0.2f, 0.85f, 0.65f, 0.99f, 0f, 0.9f, 451, 0f, true);
            Tono(b, 0f, 0.1f, 210f, 340f, 24f, 0.35f);
            return Cerrar(b, "Jump", 0.55f);
        }

        public static AudioClip Aterrizaje()
        {
            var b = Buffer(0.3f);
            Tunc(b, 0f, 1f, 461, 1f);
            Ruido(b, 0f, 0.16f, 0.965f, 0.999f, 0.999f, 24f, 0.9f, 462, 0.002f);
            Ruido(b, 0.01f, 0.1f, 0.8f, 0.8f, 0.99f, 30f, 0.25f, 463, 0.001f);
            return Cerrar(b, "Land", 0.8f);
        }

        // ------------------------------------------------------------------
        // Radial y acciones tacticas
        // ------------------------------------------------------------------
        public static AudioClip RadialAbrir()
        {
            var b = Buffer(0.22f);
            Tono(b, 0f, 0.2f, 360f, 760f, 9f, 0.6f, 0.2f, 0.01f);
            Ruido(b, 0f, 0.18f, 0.85f, 0.4f, 0.99f, 0f, 0.25f, 471, 0f, true);
            return Cerrar(b, "RadialOpen", 0.6f);
        }

        public static AudioClip RadialTick()
        {
            var b = Buffer(0.04f);
            Tono(b, 0f, 0.035f, 1900f, 1600f, 80f, 1f, 0f, 0.0008f);
            return Cerrar(b, "RadialTick", 0.55f);
        }

        public static AudioClip RadialConfirmar()
        {
            var b = Buffer(0.3f);
            Tono(b, 0f, 0.14f, 784f, 784f, 12f, 0.8f, 0.2f, 0.004f);
            Tono(b, 0.075f, 0.22f, 1175f, 1175f, 9f, 0.8f, 0.2f, 0.004f);
            return Cerrar(b, "RadialConfirm", 0.7f);
        }

        public static AudioClip RadialCancelar()
        {
            var b = Buffer(0.18f);
            Tono(b, 0f, 0.16f, 640f, 300f, 14f, 1f, 0.1f, 0.004f);
            return Cerrar(b, "RadialCancel", 0.55f);
        }

        // Curacion: tres notas suaves que suben ("ya voy") y, al terminar, un destello agudo.
        public static AudioClip CuracionInicio()
        {
            var b = Buffer(0.5f);
            float[] n = { 523f, 659f, 784f };
            for (int i = 0; i < n.Length; i++) Tono(b, i * 0.09f, 0.22f, n[i], n[i], 10f, 0.8f, 0.12f, 0.006f);
            return Cerrar(b, "HealStart", 0.6f);
        }

        public static AudioClip CuracionFin()
        {
            var b = Buffer(0.7f);
            float[] n = { 1047f, 1319f, 1568f, 2093f };
            for (int i = 0; i < n.Length; i++) Tono(b, i * 0.06f, 0.3f, n[i], n[i], 8f, 0.7f, 0.25f, 0.004f);
            return Cerrar(b, "HealDone", 0.6f);
        }

        // Reanimacion: descarga electrica, golpe de pecho y campanita de vuelta a la vida.
        public static AudioClip Reanimar()
        {
            var b = Buffer(0.95f);
            Ruido(b, 0f, 0.28f, 0.5f, 0.3f, 0.96f, 12f, 0.7f, 481, 0.002f);
            Tono(b, 0.02f, 0.3f, 300f, 1300f, 5f, 0.4f, 0.5f, 0.02f);
            Tunc(b, 0.3f, 1.2f, 482, 0.6f);
            Tono(b, 0.45f, 0.45f, 880f, 880f, 6f, 0.5f, 0.3f, 0.005f);
            Tono(b, 0.55f, 0.4f, 1320f, 1320f, 7f, 0.4f, 0.3f, 0.005f);
            return Cerrar(b, "Revive", 0.7f);
        }

        // Carga plantada: tres pitidos agudos seguidos.
        public static AudioClip CargaPlantada()
        {
            var b = Buffer(0.45f);
            for (int i = 0; i < 3; i++) Tono(b, i * 0.12f, 0.07f, 1800f, 1800f, 20f, 0.8f, 0.05f, 0.002f);
            return Cerrar(b, "BombPlant", 0.6f);
        }

        public static AudioClip CargaTic()
        {
            var b = Buffer(0.09f);
            Tono(b, 0f, 0.07f, 1500f, 1500f, 26f, 1f, 0.05f, 0.002f);
            return Cerrar(b, "BombTick", 0.5f);
        }

        // ------------------------------------------------------------------
        // Recarga y desenfunde por arma. La duracion sigue a la recarga del catalogo para que el
        // ultimo "clac" (cerrojo/corredera) caiga justo cuando el arma queda lista.
        // ------------------------------------------------------------------
        public static AudioClip Recarga(WeaponKind arma)
        {
            switch (arma)
            {
                case WeaponKind.Pistol:
                {
                    var b = Buffer(1.0f);
                    Clac(b, 0.00f, 0.8f, 501, 0.9f);       // suelta el cargador
                    Roce(b, 0.10f, 0.2f, 0.12f, 502);
                    Tunc(b, 0.40f, 0.9f, 503, 1.2f);       // entra el cargador nuevo
                    Clac(b, 0.44f, 0.6f, 504, 1.2f);
                    Roce(b, 0.62f, 0.14f, 0.3f, 505);      // corredera atras
                    Clac(b, 0.78f, 1.0f, 506, 1.1f);       // corredera adelante
                    return Cerrar(b, "Reload_Pistol", 0.8f);
                }
                case WeaponKind.Smg:
                {
                    var b = Buffer(1.6f);
                    Clac(b, 0.00f, 0.8f, 511, 0.8f);
                    Traqueteo(b, 0.12f, 0.35f, 3, 0.15f, 512);
                    Tunc(b, 0.62f, 1.0f, 513, 1f);
                    Clac(b, 0.66f, 0.6f, 514, 1f);
                    Roce(b, 1.05f, 0.18f, 0.4f, 515);      // cerrojo atras
                    Clac(b, 1.30f, 1.0f, 516, 0.9f);       // cerrojo adelante
                    return Cerrar(b, "Reload_Smg", 0.8f);
                }
                case WeaponKind.Heavy:
                {
                    var b = Buffer(2.2f);
                    Tunc(b, 0.00f, 1.0f, 521, 0.7f);       // abre la tapa
                    Clac(b, 0.05f, 0.7f, 522, 0.6f);
                    Traqueteo(b, 0.35f, 0.9f, 9, 0.22f, 523); // cinta de balas
                    Tunc(b, 1.35f, 1.1f, 524, 0.8f);       // acomoda la caja
                    Tunc(b, 1.6f, 1.0f, 525, 0.7f);        // cierra la tapa
                    Clac(b, 1.65f, 0.8f, 526, 0.7f);
                    Roce(b, 1.85f, 0.14f, 0.4f, 527);
                    Clac(b, 2.02f, 1.0f, 528, 0.8f);       // arma
                    return Cerrar(b, "Reload_Heavy", 0.85f);
                }
                case WeaponKind.Shotgun:
                {
                    var b = Buffer(2.4f);
                    for (int i = 0; i < 4; i++)            // cartucho por cartucho
                    {
                        float t = 0.1f + i * 0.34f;
                        Clac(b, t, 0.55f, 531 + i * 3, 1.3f);
                        Tunc(b, t + 0.03f, 0.35f, 532 + i * 3, 1.4f);
                    }
                    Roce(b, 1.55f, 0.16f, 0.5f, 545);      // corredera atras
                    Clac(b, 1.72f, 0.9f, 546, 0.7f);
                    Roce(b, 1.9f, 0.14f, 0.4f, 547);       // corredera adelante
                    Clac(b, 2.08f, 1.0f, 548, 0.6f);
                    return Cerrar(b, "Reload_Shotgun", 0.85f);
                }
                case WeaponKind.Sniper:
                {
                    var b = Buffer(2.4f);
                    Clac(b, 0.05f, 0.7f, 551, 0.8f);       // levanta la manija
                    Roce(b, 0.30f, 0.22f, 0.45f, 552);     // cerrojo atras
                    Clac(b, 0.55f, 0.8f, 553, 0.6f);
                    Clac(b, 1.05f, 0.6f, 554, 1.3f);       // entra la bala
                    Tunc(b, 1.08f, 0.5f, 555, 1.3f);
                    Roce(b, 1.40f, 0.2f, 0.45f, 556);      // cerrojo adelante
                    Clac(b, 1.66f, 1.0f, 557, 0.7f);
                    Clac(b, 2.0f, 0.8f, 558, 0.9f);        // baja la manija
                    return Cerrar(b, "Reload_Sniper", 0.85f);
                }
                case WeaponKind.Rocket:
                {
                    var b = Buffer(2.8f);
                    Tunc(b, 0.00f, 1.1f, 561, 0.6f);       // abre la culata
                    Clac(b, 0.05f, 0.8f, 562, 0.5f);
                    Roce(b, 0.45f, 0.7f, 0.5f, 563);       // el tubo se desliza
                    Traqueteo(b, 0.5f, 0.6f, 4, 0.15f, 564);
                    Tunc(b, 1.55f, 1.3f, 565, 0.55f);      // entra la ojiva
                    Clac(b, 1.6f, 0.6f, 566, 0.5f);
                    Tunc(b, 2.05f, 1.0f, 567, 0.7f);       // cierra la culata
                    Clac(b, 2.35f, 1.0f, 568, 0.7f);       // traba
                    return Cerrar(b, "Reload_Rocket", 0.85f);
                }
                default: // Rifle
                {
                    var b = Buffer(1.5f);
                    Clac(b, 0.00f, 0.8f, 571, 0.85f);      // suelta el cargador
                    Roce(b, 0.12f, 0.22f, 0.1f, 572);
                    Tunc(b, 0.58f, 1.0f, 573, 1.1f);       // entra el cargador
                    Clac(b, 0.62f, 0.7f, 574, 1.1f);
                    Roce(b, 0.95f, 0.16f, 0.4f, 575);      // manija de carga atras
                    Clac(b, 1.18f, 1.0f, 576, 0.9f);       // manija adelante
                    return Cerrar(b, "Reload_Rifle", 0.8f);
                }
            }
        }

        // Sacar el arma: roce de la correa y el "clac" propio de cada una.
        public static AudioClip Desenfundar(WeaponKind arma)
        {
            var b = Buffer(0.4f);
            Roce(b, 0f, 0.16f, 0.35f, 601 + (int)arma);
            switch (arma)
            {
                case WeaponKind.Pistol: Clac(b, 0.14f, 0.7f, 611, 1.4f); break;
                case WeaponKind.Smg: Clac(b, 0.14f, 0.7f, 612, 1.1f); Traqueteo(b, 0.05f, 0.1f, 2, 0.15f, 613); break;
                case WeaponKind.Heavy: Tunc(b, 0.14f, 1.0f, 614, 0.6f); Traqueteo(b, 0.1f, 0.2f, 3, 0.2f, 615); break;
                case WeaponKind.Shotgun: Clac(b, 0.12f, 0.8f, 616, 0.8f); Clac(b, 0.22f, 0.9f, 617, 0.7f); break;   // bombeo "chk-chak"
                case WeaponKind.Sniper: Clac(b, 0.14f, 0.9f, 618, 0.7f); break;
                case WeaponKind.Rocket: Tunc(b, 0.14f, 1.2f, 619, 0.5f); Tono(b, 0.14f, 0.2f, 90f, 60f, 16f, 0.25f); break;
                default: Clac(b, 0.14f, 0.8f, 620, 0.9f); break;
            }
            return Cerrar(b, "Draw_" + arma, 0.7f);
        }

        // Disparo sin municion (gatillo en seco): mas grave para el arma pesada, seco para la chica.
        public static AudioClip GatilloVacio(WeaponKind arma)
        {
            var b = Buffer(0.12f);
            float tono = arma == WeaponKind.Pistol || arma == WeaponKind.Smg ? 1.4f : arma == WeaponKind.Heavy || arma == WeaponKind.Rocket ? 0.6f : 1f;
            Clac(b, 0f, 1f, 640 + (int)arma, tono);
            return Cerrar(b, "Dry_" + arma, 0.75f);
        }
    }
}
