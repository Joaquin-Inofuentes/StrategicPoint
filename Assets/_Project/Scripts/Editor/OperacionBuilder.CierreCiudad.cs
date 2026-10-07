using UnityEngine;

namespace SP.EditorTools
{
    // #116: "en el mapa hay sectores no terminados". Vista cenital de los 4 bordes de la ciudad (Assets/Validacion/v3_116_*): al norte, sur
    // y oeste las ultimas casas daban a campo abierto (suelo pelado hasta el horizonte) y al este, pasado el muro del borde, el vacio. Aca
    // se cierra con una muralla de edificios de relleno (macizos, altos, sin ventanas utiles) pegada a cada lado, dejando libre la entrada
    // de la autopista por el oeste (z 208-248). Es solo decorado: nada de esto toca el recorrido jugable.
    public static partial class OperacionBuilder
    {
        public static int EdificiosDeCierre { get; private set; }

        static void CierreDeLaCiudad(Transform ciudad)
        {
            var vieja = ciudad.Find("Cierre");
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
            var g = Grupo(ciudad, "Cierre");
            EdificiosDeCierre = 0;
            // Piso de horizonte: sin collider ni NavMesh, bajo el piso real. Cubre lo que queda mas alla del borde del mapa (antes: vacio) para que
            // la camara tactica no vea el fondo del cielo bajo el horizonte.
            var fondo = Cubo(g, "Fondo_Horizonte", CiudadX, CiudadZ, 1600f, 0.1f, 1600f, Mat("Fondo_Horizonte", new Color(0.13f, 0.17f, 0.14f)), 0f, -0.13f, false);
            fondo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rnd = new System.Random(116);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var relleno = new[] { Mat("Relleno_A", new Color(0.30f, 0.32f, 0.37f)), Mat("Relleno_B", new Color(0.36f, 0.34f, 0.33f)), Mat("Relleno_C", new Color(0.27f, 0.30f, 0.33f)) };

            void Pieza(float x, float z, float w, float d)
            {
                float h = R(13f, 22f);
                var e = Solido(g, "Relleno_" + (++EdificiosDeCierre), x, z, w, h, d, relleno[rnd.Next(relleno.Length)]);
                CopiarModificadorDeNavegacion(e);
            }
            // Fila a lo largo de X (norte y sur) y a lo largo de Z (oeste y este).
            void FilaX(float x0, float x1, float zc, float prof)
            {
                float x = x0;
                while (x < x1) { float w = R(11f, 16f); Pieza(x + w * 0.5f, zc, w, prof); x += w + 0.3f; }
            }
            void FilaZ(float z0, float z1, float xc, float prof)
            {
                float z = z0;
                while (z < z1) { float d = R(11f, 16f); Pieza(xc, z + d * 0.5f, prof, d); z += d + 0.3f; }
            }
            FilaX(232f, 406f, 320f, 14f);      // norte: z 313-327 (las casas del norte llegan a 312)
            FilaX(232f, 406f, 136f, 14f);      // sur: z 129-143 (las del sur llegan a 145)
            FilaZ(129f, 207f, 240f, 14f);      // oeste, al sur de la entrada
            FilaZ(249f, 327f, 240f, 14f);      // oeste, al norte de la entrada
            FilaZ(143f, 313f, 397.5f, 14f);    // este, pasado el muro del borde (x 388-390)
        }
    }
}
