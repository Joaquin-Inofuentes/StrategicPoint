using UnityEngine;

namespace SP.Operacion
{
    // La mansion de la ciudad (rediseno de Resistir): una sola cascara de 52 x 40 m con tres pisos de 6,5 m de alto y azotea. Las medidas viven aca
    // para que las comparta el constructor del editor (OperacionBuilder.Mansion) y el director (OperacionDirector.Mansion).
    //
    //   nivel 0 PB       gran vestibulo con columnas, mostrador de recepcion y alfombra; el porton esta al sur
    //   nivel 1 1.er     galeria central ancha con seis salones de emboscada
    //   nivel 2 2.o      salon de baile abierto; las celdas de los milicianos al noroeste
    //   nivel 3 azotea   abierta, con parapeto; el helicoptero enemigo ataca desde aca arriba
    //
    // Las escaleras son torres de dos tramos en zigzag (sube por un carril, rellano, vuelve por el otro): cada torre une dos pisos y esta en una
    // esquina distinta, asi que para subir de piso hay que cruzar el salon entero. Cada carril mide 4,6 m de ancho.
    public static class MansionGeometria
    {
        public const float X0 = 296f, X1 = 348f, Z0 = 246f, Z1 = 286f;
        public const float Muro = 0.6f, Piso = 6.5f, Losa = 0.3f, Parapeto = 1.2f;
        // Torre: ancho 11,2 (carril A 0,4..5,0 · divisoria 5,0..6,2 · carril B 6,2..10,8 · muro 10,8..11,2); fondo 12,6 (tramo de 9 m + rellano).
        public const float TorreAncho = 11.2f, TorreFondo = 12.6f, TramoLargo = 9f, CarrilA = 2.7f, CarrilB = 8.5f, MedioCarril = 2.3f;
        public const float DivisoriaU0 = 5.0f, DivisoriaU1 = 6.2f, PuertaU0 = 0.4f, PuertaAlto = 4.4f;
        public const float PortonX = 322f, PortonAncho = 7f, PortonAlto = 5.2f;

        public static float Nivel(int n) => n * Piso;
        public static Vector3 Centro(int nivel) => new Vector3((X0 + X1) * 0.5f, Nivel(nivel), (Z0 + Z1) * 0.5f);
        public static bool Dentro(Vector3 p, float margen = 0f) => p.x > X0 - margen && p.x < X1 + margen && p.z > Z0 - margen && p.z < Z1 + margen;

        // Nivel (0..3) segun la altura de los pies; -1 si esta fuera de la huella.
        public static int NivelDe(Vector3 p)
        {
            if (!Dentro(p, 0.5f)) return -1;
            return Mathf.Clamp(Mathf.FloorToInt((p.y + 1.4f) / Piso), 0, 3);
        }

        public struct Torre
        {
            public string nombre; public Vector2 origen; public Vector2 u, v; public int nivelBase;
            public Vector2 Mundo(float cu, float cv) => origen + u * cu + v * cv;
        }

        // Cinco torres de escalera. u: del muro de la torre hacia adentro; v: de la puerta (extremo cercano) hacia el fondo.
        public static readonly Torre[] Torres =
        {
            new Torre { nombre = "A", origen = new Vector2(296.6f, 272.8f), u = new Vector2(1, 0), v = new Vector2(0, 1), nivelBase = 0 },   // PB -> 1.er piso, noroeste
            new Torre { nombre = "B", origen = new Vector2(347.4f, 259.2f), u = new Vector2(-1, 0), v = new Vector2(0, -1), nivelBase = 1 }, // 1.er -> 2.o, sureste
            new Torre { nombre = "C", origen = new Vector2(347.4f, 272.8f), u = new Vector2(-1, 0), v = new Vector2(0, 1), nivelBase = 2 },  // 2.o -> azotea, noreste
            new Torre { nombre = "D", origen = new Vector2(296.6f, 259.2f), u = new Vector2(1, 0), v = new Vector2(0, -1), nivelBase = 2 }, // 2.o -> azotea, suroeste
            new Torre { nombre = "E", origen = new Vector2(316.4f, 272.8f), u = new Vector2(1, 0), v = new Vector2(0, 1), nivelBase = 2 },   // 2.o -> azotea, norte-centro
        };

        // Donde sale la escalera al piso de arriba (final del carril B, lado de la puerta), a la altura del piso nivelBase+1.
        public static Vector3 SalidaDeTorre(int i)
        {
            var t = Torres[i];
            var p = t.Mundo(CarrilB, -2f);
            return new Vector3(p.x, Nivel(t.nivelBase + 1), p.y);
        }

        // Donde entra la escalera desde su piso (puerta del carril A).
        public static Vector3 EntradaDeTorre(int i)
        {
            var t = Torres[i];
            var p = t.Mundo(CarrilA, -2f);
            return new Vector3(p.x, Nivel(t.nivelBase), p.y);
        }

        // Rellano de la torre (donde asoman los refuerzos de la azotea).
        public static Vector3 RellanoDeTorre(int i)
        {
            var t = Torres[i];
            var p = t.Mundo(TorreAncho * 0.5f, TramoLargo + 0.3f + (TorreFondo - TramoLargo - 0.3f) * 0.5f);
            return new Vector3(p.x, Nivel(t.nivelBase) + Piso * 0.5f, p.y);
        }

        // Puntos de aparicion por piso (mundo; y = altura del piso).
        public static readonly Vector3[] EnemigosPB =
        {
            new Vector3(306f, 0f, 251f), new Vector3(340f, 0f, 251f), new Vector3(312f, 0f, 262f), new Vector3(332f, 0f, 262f),
            new Vector3(322f, 0f, 270f), new Vector3(308f, 0f, 279f), new Vector3(338f, 0f, 281f), new Vector3(322f, 0f, 256f),
            new Vector3(312f, 0f, 276f), new Vector3(334f, 0f, 270f),
        };
        public static readonly Vector3[] EnemigosPrimero =
        {
            new Vector3(314f, 6.5f, 279f), new Vector3(327f, 6.5f, 279f), new Vector3(341f, 6.5f, 279f),
            new Vector3(303f, 6.5f, 252f), new Vector3(316f, 6.5f, 252f), new Vector3(329f, 6.5f, 252f),
            new Vector3(334f, 6.5f, 266f), new Vector3(312f, 6.5f, 266f), new Vector3(322f, 6.5f, 266f),
        };
        public static readonly Vector3[] EnemigosSegundo =
        {
            new Vector3(308f, 13f, 252f), new Vector3(330f, 13f, 252f), new Vector3(312f, 13f, 264f), new Vector3(332f, 13f, 264f),
            new Vector3(322f, 13f, 258f), new Vector3(340f, 13f, 278f), new Vector3(306f, 13f, 268f), new Vector3(322f, 13f, 250f),
            new Vector3(344f, 13f, 262f), new Vector3(300f, 13f, 262f),
        };
        // Celdas contra el muro norte (x 297..315,4, z 279,5..285,4), puerta al sur.
        public static readonly Vector3[] CeldasDeMilicianos =
        {
            new Vector3(299.3f, 13f, 283f), new Vector3(303.9f, 13f, 283f), new Vector3(308.5f, 13f, 283f), new Vector3(313.1f, 13f, 283f),
        };
        public static readonly Vector3 PuertaDeCeldas = new Vector3(306.4f, 13f, 276.6f);
        public static readonly Vector3 CentroDeLaAzotea = new Vector3(322f, 19.5f, 266f);
    }
}
