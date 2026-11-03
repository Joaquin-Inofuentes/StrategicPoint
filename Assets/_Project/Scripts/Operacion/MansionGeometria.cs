using UnityEngine;

namespace SP.Operacion
{
    // La mansion de la ciudad (rediseno de Resistir): una sola cascara de 44 x 24 m con tres pisos y azotea. Las medidas viven aca para que
    // las comparta el constructor del editor (OperacionBuilder.Mansion) y el director (OperacionDirector.Mansion).
    //
    //   nivel 0 PB       plaza interior con pilares y mostrador; el porton esta al sur
    //   nivel 1 1.er     pasillo central con seis cuartos de emboscada
    //   nivel 2 2.o      gran salon abierto; las celdas de los milicianos al noroeste
    //   nivel 3 azotea   abierta, con parapeto; el helicoptero enemigo ataca desde aca arriba
    //
    // Las escaleras son torres de dos tramos en zigzag (sube por un carril, rellano, vuelve por el otro): cada torre une dos pisos y esta en una
    // esquina distinta, asi que para subir de piso hay que cruzar el salon entero.
    public static class MansionGeometria
    {
        public const float X0 = 300f, X1 = 344f, Z0 = 248f, Z1 = 272f;
        public const float Muro = 0.6f, Piso = 5.5f, Losa = 0.3f, Parapeto = 1.2f;
        public const float TorreAncho = 5.6f, TorreFondo = 8.4f, TramoLargo = 6.5f, CarrilA = 1.45f, CarrilB = 4.15f, MedioCarril = 1.15f;
        public const float PortonX = 322f, PortonAncho = 5f, PortonAlto = 4.4f;

        public static float Nivel(int n) => n * Piso;
        public static Vector3 Centro(int nivel) => new Vector3((X0 + X1) * 0.5f, Nivel(nivel), (Z0 + Z1) * 0.5f);
        public static bool Dentro(Vector3 p, float margen = 0f) => p.x > X0 - margen && p.x < X1 + margen && p.z > Z0 - margen && p.z < Z1 + margen;

        // Nivel (0..3) segun la altura de los pies; -1 si esta fuera de la huella.
        public static int NivelDe(Vector3 p)
        {
            if (!Dentro(p, 0.5f)) return -1;
            return Mathf.Clamp(Mathf.FloorToInt((p.y + 1.2f) / Piso), 0, 3);
        }

        public struct Torre
        {
            public string nombre; public Vector2 origen; public Vector2 u, v; public int nivelBase;
            public Vector2 Mundo(float cu, float cv) => origen + u * cu + v * cv;
        }

        // Cuatro torres de escalera. u: del muro de la torre hacia adentro; v: de la puerta (extremo cercano) hacia el fondo.
        public static readonly Torre[] Torres =
        {
            new Torre { nombre = "A", origen = new Vector2(300.6f, 263f), u = new Vector2(1, 0), v = new Vector2(0, 1), nivelBase = 0 },   // PB -> 1.er piso, noroeste
            new Torre { nombre = "B", origen = new Vector2(343.4f, 257f), u = new Vector2(-1, 0), v = new Vector2(0, -1), nivelBase = 1 }, // 1.er -> 2.o, sureste
            new Torre { nombre = "C", origen = new Vector2(343.4f, 263f), u = new Vector2(-1, 0), v = new Vector2(0, 1), nivelBase = 2 },  // 2.o -> azotea, noreste
            new Torre { nombre = "D", origen = new Vector2(300.6f, 257f), u = new Vector2(1, 0), v = new Vector2(0, -1), nivelBase = 2 }, // 2.o -> azotea, suroeste
            new Torre { nombre = "E", origen = new Vector2(319f, 263f), u = new Vector2(1, 0), v = new Vector2(0, 1), nivelBase = 2 },    // 2.o -> azotea, norte-centro
        };

        // Donde sale la escalera al piso de arriba (final del carril B, lado de la puerta), a la altura del piso nivelBase+1.
        public static Vector3 SalidaDeTorre(int i)
        {
            var t = Torres[i];
            var p = t.Mundo(CarrilB, -1.4f);
            return new Vector3(p.x, Nivel(t.nivelBase + 1), p.y);
        }

        // Donde entra la escalera desde su piso (puerta del carril A).
        public static Vector3 EntradaDeTorre(int i)
        {
            var t = Torres[i];
            var p = t.Mundo(CarrilA, -1.4f);
            return new Vector3(p.x, Nivel(t.nivelBase), p.y);
        }

        // Puntos de aparicion por piso (mundo; y = altura del piso).
        public static readonly Vector3[] EnemigosPB =
        {
            new Vector3(309f, 0f, 252f), new Vector3(335f, 0f, 252f), new Vector3(316f, 0f, 260f), new Vector3(329f, 0f, 260f),
            new Vector3(322f, 0f, 266f), new Vector3(309f, 0f, 267f), new Vector3(336f, 0f, 267f), new Vector3(322f, 0f, 255f),
        };
        public static readonly Vector3[] EnemigosPrimero =
        {
            new Vector3(312.5f, 5.5f, 267.5f), new Vector3(324.5f, 5.5f, 267.5f), new Vector3(337f, 5.5f, 267.5f),
            new Vector3(306.5f, 5.5f, 252.5f), new Vector3(319.5f, 5.5f, 252.5f), new Vector3(331.5f, 5.5f, 252.5f),
            new Vector3(331f, 5.5f, 260f), new Vector3(314f, 5.5f, 260f),
        };
        public static readonly Vector3[] EnemigosSegundo =
        {
            new Vector3(312f, 11f, 253f), new Vector3(330f, 11f, 253f), new Vector3(314f, 11f, 261f), new Vector3(329f, 11f, 261f),
            new Vector3(322f, 11f, 257f), new Vector3(334f, 11f, 268f), new Vector3(309f, 11f, 261f), new Vector3(322f, 11f, 250.5f),
        };
        public static readonly Vector3[] CeldasDeMilicianos =
        {
            new Vector3(309.2f, 11f, 269.4f), new Vector3(312.9f, 11f, 269.4f), new Vector3(316.6f, 11f, 269.4f), new Vector3(310.6f, 11f, 266.4f),
        };
        public static readonly Vector3 PuertaDeCeldas = new Vector3(313f, 11f, 265.2f);
        public static readonly Vector3 CentroDeLaAzotea = new Vector3(322f, 16.5f, 260f);
    }
}
