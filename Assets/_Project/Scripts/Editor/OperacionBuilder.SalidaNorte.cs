using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Portón de la SALIDA NORTE del Centro de Datos (el primero del nivel): entre los pilares de franjas amarillas y negras el muro era un
    // paño liso con un hueco azul arriba (cielo) y sin detalle. Aca se le arma una hoja de acero a juego con el portón blindado:
    // dos hojas con costillas, cruces, remaches, banda de peligro, cerradura y pistones, por las dos caras. Cuelga de
    // Muro_Salida_Norte/Adjuntos (sin colliders), asi que se rompe y cae con el muro al volar la carga. Idempotente.
    public static partial class OperacionBuilder
    {
        const string NombreArteSalidaNorte = "Porton_Salida_Arte";

        [MenuItem("Strategic Point/Arte/Portón salida norte (SC_Operacion)")]
        static void PortonSalidaNorteMenu() => Debug.Log(PortonSalidaNorteEscenaAbierta());

        public static string PortonSalidaNorteEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            Transform muro = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Muro_Salida_Norte" && t.gameObject.scene == escena) { muro = t; break; }
            if (muro == null) return "no esta Muro_Salida_Norte";
            var adj = muro.Find("Adjuntos");
            if (adj == null) return "Muro_Salida_Norte no tiene Adjuntos";
            var viejo = adj.Find(NombreArteSalidaNorte);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

            var acero = Mat("PortonAcero", new Color(0.20f, 0.22f, 0.25f));
            var acero2 = Mat("PortonCostilla", new Color(0.30f, 0.32f, 0.35f));
            var amarillo = Mat("PortonPeligroAmarillo", new Color(0.95f, 0.72f, 0.08f));
            var luzRoja = Mat("PortonLuzRoja", new Color(1f, 0.12f, 0.08f), 2.5f);
            var negro = Negro;

            var arte = new GameObject(NombreArteSalidaNorte).transform;
            arte.SetParent(adj, false);
            // El hueco real: pilares en x=+-6,6 (1,5 de ancho) -> libre de -5,85 a 5,85; dintel por debajo de y=6,95.
            const float ancho = 11.7f, alto = 6.9f, espesor = 1.46f;   // el muro mide 1,5 de grosor: la losa lo rellena entre los pilares, sin quedar al ras de sus caras
            float yBase = -muro.position.y;             // local de Adjuntos: el piso queda en y = -2,5
            float yCentro = yBase + alto * 0.5f;
            float mitadHoja = ancho * 0.25f;            // centro de cada hoja

            // cuerpo: una losa y una ranura oscura al medio (juntura de las dos hojas)
            CajaPB(arte, "Panel", new Vector3(0f, yCentro, 0f), new Vector3(ancho, alto, espesor), acero);
            CajaPB(arte, "Juntura", new Vector3(0f, yCentro, -espesor * 0.5f - 0.02f), new Vector3(0.16f, alto - 0.2f, 0.05f), negro, 0f, false);
            CajaPB(arte, "Juntura", new Vector3(0f, yCentro, espesor * 0.5f + 0.02f), new Vector3(0.16f, alto - 0.2f, 0.05f), negro, 0f, false);

            float hoja = ancho * 0.5f - 0.3f;           // ancho util de cada hoja
            float altoU = alto - 1.0f;                  // sobre la banda de peligro
            float yU = yBase + 0.6f + altoU * 0.5f;
            for (int cara = -1; cara <= 1; cara += 2)
            {
                float z = cara * (espesor * 0.5f + 0.04f);   // el detalle va sobre cada cara del muro
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    float cx = lado * mitadHoja;
                    // marco hundido de la hoja y tablero interior
                    CajaPB(arte, "Marco", new Vector3(cx, yU, z), new Vector3(hoja, altoU, 0.08f), acero2, 0f, false);
                    CajaPB(arte, "Tablero", new Vector3(cx, yU, z + cara * 0.02f), new Vector3(hoja - 0.4f, altoU - 0.4f, 0.08f), acero, 0f, false);
                    // costillas horizontales (se evita la altura de las barreras rojo/blanco, y=1,2 local)
                    foreach (float yr in new[] { -1.9f, -0.5f, 2.9f, 4.2f })
                        CajaPB(arte, "Costilla", new Vector3(cx, yr, z + cara * 0.06f), new Vector3(hoja - 0.2f, 0.22f, 0.12f), acero2);
                    // cruz de refuerzo
                    float dx = hoja - 0.6f, dy = altoU - 0.6f;
                    float diag = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float largo = Mathf.Sqrt(dx * dx + dy * dy);
                    CajaPB(arte, "Cruz_A", new Vector3(cx, yU, z + cara * 0.14f), new Vector3(largo, 0.2f, 0.08f), acero2, diag, false);
                    CajaPB(arte, "Cruz_B", new Vector3(cx, yU, z + cara * 0.14f), new Vector3(largo, 0.2f, 0.08f), acero2, -diag, false);
                    // remaches en los cuatro bordes
                    for (int r = 0; r < Mathf.FloorToInt(hoja / 0.7f); r++)
                    {
                        float x = cx - hoja * 0.5f + 0.35f + r * 0.7f;
                        CajaPB(arte, "Remache", new Vector3(x, yU + altoU * 0.5f - 0.15f, z + cara * 0.07f), new Vector3(0.1f, 0.1f, 0.07f), negro, 0f, false);
                        CajaPB(arte, "Remache", new Vector3(x, yU - altoU * 0.5f + 0.15f, z + cara * 0.07f), new Vector3(0.1f, 0.1f, 0.07f), negro, 0f, false);
                    }
                    // banda de peligro al pie, en bloques de 0,5 m amarillo/negro
                    int bloques = Mathf.FloorToInt(hoja / 0.5f);
                    for (int b = 0; b < bloques; b++)
                        CajaPB(arte, "Peligro", new Vector3(cx - hoja * 0.5f + 0.25f + b * 0.5f, yBase + 0.3f, z + cara * 0.03f), new Vector3(0.5f, 0.5f, 0.06f), (b + (lado > 0 ? 1 : 0)) % 2 == 0 ? amarillo : negro, 0f, false);
                    // pistones hidraulicos en el borde exterior de cada hoja
                    CajaPB(arte, "Piston", new Vector3(lado * (ancho * 0.5f - 0.25f), yU, z + cara * 0.12f), new Vector3(0.22f, altoU - 0.6f, 0.2f), negro, 0f, false);
                    CajaPB(arte, "Piston_Brazo", new Vector3(lado * (ancho * 0.5f - 0.25f), yU - altoU * 0.2f, z + cara * 0.12f), new Vector3(0.34f, altoU * 0.35f, 0.26f), acero2, 0f, false);
                }
                // cerradura central con luz roja
                CajaPB(arte, "Cerradura_Placa", new Vector3(0f, yU, z + cara * 0.14f), new Vector3(0.9f, 1.5f, 0.14f), negro, 0f, false);
                CajaPB(arte, "Cerradura_Luz", new Vector3(0f, yU + 0.45f, z + cara * 0.24f), new Vector3(0.3f, 0.3f, 0.08f), luzRoja, 0f, false);
                CajaPB(arte, "Cerradura_Tecla", new Vector3(0f, yU - 0.15f, z + cara * 0.22f), new Vector3(0.5f, 0.3f, 0.06f), acero2, 0f, false);
            }

            // Las 4 barreras rojo/blanco (bug #320) quedan montadas por delante de la hoja, no metidas dentro de ella.
            foreach (Transform b in adj) if (b.name == "Barrera") { var p = b.localPosition; p.z = -espesor * 0.5f - 0.27f; b.localPosition = p; }
            foreach (Transform r in adj) if (r.name.StartsWith("Rotulo_SALIDA")) { var p = r.localPosition; p.z = -espesor * 0.5f - 0.6f; r.localPosition = p; }

            EditorUtility.SetDirty(arte.gameObject);
            EditorSceneManager.MarkSceneDirty(escena);
            return $"{NombreArteSalidaNorte}: {arte.GetComponentsInChildren<Renderer>(true).Length} piezas";
        }
    }
}
