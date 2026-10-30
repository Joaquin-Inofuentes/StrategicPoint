using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Portón norte del cuartel (el primero del nivel, el de la bomba "CARGA PORTON NORTE"): era una barrera lisa roja y blanca de 3,4 m con el
    // cielo abierto hasta el dintel. Aca se le arma una hoja blindada de piso a dintel (dos hojas con juntura, tableros, costillas, vigas, remaches,
    // banda de peligro rojo/blanco, cerradura y pistones) por las dos caras. Cuelga del cubo `Porton_Cuartel` (que conserva el collider y se hunde
    // al volar la carga), en un hijo con escala inversa para trabajar en metros, sin rotaciones (la escala no uniforme las deformaria).
    public static partial class OperacionBuilder
    {
        public const float AltoDelArteDelPortonCuartel = 6.1f;

        [MenuItem("Strategic Point/Arte/Portón cuartel (SC_Operacion)")]
        static void PortonCuartelMenu() => Debug.Log(PortonCuartelEscenaAbierta());

        public static string PortonCuartelEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            Transform cubo = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Porton_Cuartel" && t.gameObject.scene == escena) { cubo = t; break; }
            if (cubo == null) return "no esta Porton_Cuartel";
            var viejo = cubo.Find("Arte");
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

            // el dibujo liso (cubo y franjas) se apaga; queda el collider
            foreach (var r in cubo.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var ls = cubo.lossyScale;
            var arte = new GameObject("Arte").transform;
            arte.SetParent(cubo, false);
            arte.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            arte.localPosition = Vector3.zero;

            var acero = Mat("PortonAcero", new Color(0.20f, 0.22f, 0.25f));
            var acero2 = Mat("PortonCostilla", new Color(0.30f, 0.32f, 0.35f));
            var rojo = Mat("PortonCuartelRojo", new Color(0.62f, 0.07f, 0.06f));
            var blanco = Mat("PortonCuartelBlanco", new Color(0.88f, 0.87f, 0.84f));
            var luzRoja = Mat("PortonLuzRoja", new Color(1f, 0.12f, 0.08f), 2.5f);
            var negro = Negro;

            const float ancho = 13.3f, alto = AltoDelArteDelPortonCuartel, prof = 1.16f;
            float suelo = -ls.y * 0.5f;                  // piso en coordenadas locales del arte (el cubo esta apoyado en y=0)
            float yC = suelo + alto * 0.5f;
            float mitad = ancho * 0.25f;

            CajaPB(arte, "Cuerpo", new Vector3(0f, yC, 0f), new Vector3(ancho, alto, prof), acero);
            for (int cara = -1; cara <= 1; cara += 2)
            {
                float z = cara * (prof * 0.5f + 0.04f);
                CajaPB(arte, "Juntura", new Vector3(0f, yC, z), new Vector3(0.18f, alto - 0.2f, 0.06f), negro, 0f, false);
                float hoja = ancho * 0.5f - 0.4f, altoU = alto - 1.3f, yU = suelo + 0.9f + altoU * 0.5f;
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    float cx = lado * mitad;
                    CajaPB(arte, "Marco", new Vector3(cx, yU, z), new Vector3(hoja, altoU, 0.08f), acero2, 0f, false);
                    CajaPB(arte, "Tablero_Alto", new Vector3(cx, yU + altoU * 0.22f, z + cara * 0.05f), new Vector3(hoja - 0.5f, altoU * 0.5f, 0.08f), acero, 0f, false);
                    CajaPB(arte, "Tablero_Bajo", new Vector3(cx, yU - altoU * 0.26f, z + cara * 0.05f), new Vector3(hoja - 0.5f, altoU * 0.4f, 0.08f), acero, 0f, false);
                    foreach (float f in new[] { 0.0f, 0.5f, -0.5f })       // costillas horizontales
                        CajaPB(arte, "Costilla", new Vector3(cx, yU + f * altoU * 0.9f, z + cara * 0.1f), new Vector3(hoja - 0.2f, 0.22f, 0.12f), acero2);
                    for (int v = -2; v <= 2; v++)                            // vigas verticales
                        CajaPB(arte, "Viga", new Vector3(cx + v * hoja * 0.2f, yU, z + cara * 0.1f), new Vector3(0.2f, altoU, 0.1f), acero2, 0f, false);
                    for (int r = 0; r < Mathf.FloorToInt(hoja / 0.7f); r++)
                    {
                        float x = cx - hoja * 0.5f + 0.35f + r * 0.7f;
                        CajaPB(arte, "Remache", new Vector3(x, yU + altoU * 0.5f - 0.15f, z + cara * 0.1f), new Vector3(0.1f, 0.1f, 0.07f), negro, 0f, false);
                        CajaPB(arte, "Remache", new Vector3(x, yU - altoU * 0.5f + 0.15f, z + cara * 0.1f), new Vector3(0.1f, 0.1f, 0.07f), negro, 0f, false);
                    }
                    int bloques = Mathf.FloorToInt(hoja / 0.5f);             // banda de peligro rojo/blanco al pie
                    for (int b = 0; b < bloques; b++)
                        CajaPB(arte, "Peligro", new Vector3(cx - hoja * 0.5f + 0.25f + b * 0.5f, suelo + 0.45f, z + cara * 0.03f), new Vector3(0.5f, 0.7f, 0.06f), (b + (lado > 0 ? 1 : 0)) % 2 == 0 ? rojo : blanco, 0f, false);
                    CajaPB(arte, "Piston", new Vector3(lado * (ancho * 0.5f - 0.25f), yU, z + cara * 0.14f), new Vector3(0.22f, altoU - 0.6f, 0.2f), negro, 0f, false);
                    CajaPB(arte, "Piston_Brazo", new Vector3(lado * (ancho * 0.5f - 0.25f), yU - altoU * 0.2f, z + cara * 0.14f), new Vector3(0.36f, altoU * 0.35f, 0.28f), acero2, 0f, false);
                }
                // franja alta rojo/blanco bajo el dintel (identidad del porton) y cerradura central
                int arriba = Mathf.FloorToInt(ancho / 0.8f);
                for (int b = 0; b < arriba; b++)
                    CajaPB(arte, "Franja_Alta", new Vector3(-ancho * 0.5f + 0.4f + b * 0.8f, suelo + alto - 0.28f, z + cara * 0.03f), new Vector3(0.8f, 0.5f, 0.06f), b % 2 == 0 ? rojo : blanco, 0f, false);
                CajaPB(arte, "Cerradura_Placa", new Vector3(0f, YCerradura(suelo, alto), z + cara * 0.16f), new Vector3(0.9f, 1.5f, 0.14f), negro, 0f, false);
                CajaPB(arte, "Cerradura_Luz", new Vector3(0f, YCerradura(suelo, alto) + 0.45f, z + cara * 0.26f), new Vector3(0.3f, 0.3f, 0.08f), luzRoja, 0f, false);
            }
            EditorUtility.SetDirty(cubo.gameObject);
            EditorSceneManager.MarkSceneDirty(escena);
            return $"Porton_Cuartel/Arte: {arte.GetComponentsInChildren<Renderer>(true).Length} piezas";
        }

        static float YCerradura(float suelo, float alto) => suelo + 0.9f + (alto - 1.3f) * 0.5f;
    }
}
