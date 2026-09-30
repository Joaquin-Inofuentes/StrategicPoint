using System.Collections.Generic;
using UnityEngine;

namespace SP.EditorTools
{
    // Los CAMINOS del nivel de SC_Gameplay como datos (poligonales en XZ): una sola fuente de verdad para
    //   - pintar el terreno (LevelBlockoutBuilder),
    //   - dejar libre el paso y llenar de arboles el resto (BlockoutArtDresser),
    //   - poner las luces que marcan cada camino (AmbientacionDelNivel).
    //
    // El nivel es un corredor de sur a norte (base -> rehen). Hay TRES caminos hasta el rehen, cada uno con su caracter:
    //   Carretera ... camino central de grava, ancho, muy iluminado (sodio) y con los puestos enemigos y los tanques.
    //   Bosque ...... sendero de tierra por el flanco OESTE entre arboles: oscuro, lamparas frias y mas escondites.
    //   Servicio .... camino de grava por el flanco ESTE con alambrados y focos industriales: abierto pero vigilado.
    // Mas los conectores (calles de la aldea, trochas entre carriles y las brechas del fortin) para cambiar de camino.
    public enum TipoDeRuta { Carretera, Bosque, Servicio, Conector }

    public struct RutaDelNivel
    {
        public string Nombre;
        public TipoDeRuta Tipo;
        public float Ancho;
        public Vector2[] Puntos;

        public float Largo
        {
            get
            {
                float l = 0f;
                for (int i = 0; i < Puntos.Length - 1; i++) l += Vector2.Distance(Puntos[i], Puntos[i + 1]);
                return l;
            }
        }

        // Punto, tangente (unitaria) y normal (a la izquierda) a `d` metros del inicio.
        public void Muestrear(float d, out Vector2 punto, out Vector2 tangente)
        {
            d = Mathf.Clamp(d, 0f, Largo);
            for (int i = 0; i < Puntos.Length - 1; i++)
            {
                float seg = Vector2.Distance(Puntos[i], Puntos[i + 1]);
                if (d <= seg || i == Puntos.Length - 2)
                {
                    tangente = (Puntos[i + 1] - Puntos[i]).normalized;
                    punto = Puntos[i] + tangente * Mathf.Min(d, seg);
                    return;
                }
                d -= seg;
            }
            punto = Puntos[Puntos.Length - 1];
            tangente = Vector2.up;
        }
    }

    public static class RutasDelNivel
    {
        // Posiciones clave (las mismas que los marcadores de la mision en la escena).
        public static readonly Vector3 Helipuerto = new Vector3(-26f, 0f, -8f);
        public static readonly Vector3 Plaza = new Vector3(4f, 0f, 119f);
        public static readonly Vector3 RefugioDelCivil = new Vector3(4f, 0f, 283f);

        static RutaDelNivel R(string nombre, TipoDeRuta tipo, float ancho, params float[] xz)
        {
            var pts = new Vector2[xz.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
            return new RutaDelNivel { Nombre = nombre, Tipo = tipo, Ancho = ancho, Puntos = pts };
        }

        public static readonly RutaDelNivel[] Todas =
        {
            // Carretera central: base -> campo de tiro -> cañon -> aldea (plaza) -> puesto -> chicane en S -> porton del fortin -> refugio.
            R("Carretera", TipoDeRuta.Carretera, 9f,
                4f, -12f,  4f, 18f,  -4f, 42f,  2f, 62f,  4f, 74f,  4f, 100f,  3f, 119f,  3f, 140f,  10f, 166f,
                26f, 186f,  34f, 204f,  -26f, 204f,  -30f, 214f,  -10f, 220f,  4f, 226f,  4f, 250f,  4f, 272f,  4f, 284f),

            // Sendero del bosque (oeste): sale del helipuerto, atraviesa la puerta oeste del cañon y de la chicane, rodea el fortin.
            R("Sendero del bosque", TipoDeRuta.Bosque, 5f,
                -26f, -8f,  -36f, -6f,  -43f, 4f,  -45f, 22f,  -43f, 45f,  -41f, 60f,  -43f, 72f,  -42f, 90f,  -43f, 106f,
                -44f, 124f,  -42f, 150f,  -43f, 175f,  -43f, 197f,  -42f, 205f,  -38f, 214f,  -41f, 236f,  -42f, 262f,  -38f, 276f),

            // Camino de servicio (este): sale de la base por el costado, puerta este del cañon y de la chicane, rodea el fortin.
            R("Camino de servicio", TipoDeRuta.Servicio, 6f,
                34f, -6f,  46f, 8f,  52f, 24f,  53f, 50f,  51f, 72f,  52f, 100f,  52f, 130f,  50f, 160f,  52f, 190f,
                52f, 205f,  52f, 216f,  52f, 240f,  51f, 262f,  46f, 277f),

            // Conectores: se puede cambiar de camino en varios puntos.
            R("Trocha del campo de tiro", TipoDeRuta.Conector, 3.5f, -43f, 44f,  -4f, 42f,  51f, 31f),
            R("Calle de la aldea sur", TipoDeRuta.Conector, 3.5f, -43f, 106f,  3f, 106f,  52f, 104f),
            R("Calle de la aldea norte", TipoDeRuta.Conector, 3.5f, -44f, 124f,  3f, 124f,  52f, 126f),
            R("Brecha oeste del fortin", TipoDeRuta.Conector, 3.5f, -42f, 244f,  -30f, 244f,  4f, 244f),
            R("Brecha este del fortin", TipoDeRuta.Conector, 3.5f, 52f, 244f,  46f, 244f,  4f, 244f),
            R("Entrada oeste al refugio", TipoDeRuta.Conector, 4f, -38f, 276f,  -34f, 280f,  -12f, 279f,  -4f, 282f,  4f, 284f),
            R("Entrada este al refugio", TipoDeRuta.Conector, 4f, 46f, 277f,  44f, 280f,  28f, 279f,  14f, 282f,  4f, 284f),
        };

        // Distancia horizontal de (x,z) al camino mas cercano, ya descontado su semiancho: <0 = sobre el camino.
        public static float DistanciaABorde(float x, float z, out int indice)
        {
            float mejor = float.MaxValue;
            indice = -1;
            var p = new Vector2(x, z);
            for (int r = 0; r < Todas.Length; r++)
            {
                var ruta = Todas[r];
                for (int i = 0; i < ruta.Puntos.Length - 1; i++)
                {
                    float d = DistSegmento(p, ruta.Puntos[i], ruta.Puntos[i + 1]) - ruta.Ancho * 0.5f;
                    if (d < mejor) { mejor = d; indice = r; }
                }
            }
            return mejor;
        }

        public static float DistanciaABorde(float x, float z) => DistanciaABorde(x, z, out _);

        // Igual pero solo contra las rutas de un tipo.
        public static float DistanciaABorde(float x, float z, TipoDeRuta tipo)
        {
            float mejor = float.MaxValue;
            var p = new Vector2(x, z);
            foreach (var ruta in Todas)
            {
                if (ruta.Tipo != tipo) continue;
                for (int i = 0; i < ruta.Puntos.Length - 1; i++)
                    mejor = Mathf.Min(mejor, DistSegmento(p, ruta.Puntos[i], ruta.Puntos[i + 1]) - ruta.Ancho * 0.5f);
            }
            return mejor;
        }

        public static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
