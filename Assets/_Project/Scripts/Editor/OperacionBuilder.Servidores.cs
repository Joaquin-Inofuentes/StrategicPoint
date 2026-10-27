using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido: "la parte de servidores mejorala, que sea mas estilo servidores y que tenga puertas de acceso y computadoras y mas
    // escenario". Sala de datos del Nivel 2 (x -25..25, z -79..-41): filas de gabinetes con puertas perforadas y LEDs, piso elevado
    // con juntas de baldosa, corredores frio/caliente marcados, bandejas de cable y ductos de aire en el techo, tres puertas de
    // acceso (sur, oeste, este) con lector de tarjeta y luces de estado, puestos de operador con PC en la pared norte y paredes de
    // video en los costados. Todo es arte SIN colision (igual que 9_PulidoFinal): no toca el NavMesh ni el combate.
    // Se corre sobre la escena ABIERTA y es idempotente: reemplaza el grupo "9_Servidores".
    public static partial class OperacionBuilder
    {
        [MenuItem("Strategic Point/Arte/Sala de servidores (SC_Operacion)")]
        public static void ServidoresMenu() => Debug.Log(ServidoresEscenaAbierta());

        public static string ServidoresEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            var viejo = GameObject.Find("9_Servidores");
            if (viejo != null) Object.DestroyImmediate(viejo);
            int n0 = PulidoFinalObjetos;
            var raiz = new GameObject("9_Servidores");
            var g = raiz.transform;
            var rnd = new System.Random(20261025);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var gab = Mat("GabineteNegro", new Color(0.12f, 0.14f, 0.18f));
            var gabFrente = Mat("GabineteFrente", new Color(0.30f, 0.34f, 0.42f));
            var gabRejilla = Mat("GabineteRejilla", new Color(0.03f, 0.035f, 0.045f));
            var metal = Mat("MetalSala", new Color(0.34f, 0.38f, 0.45f));
            var metalOsc = Mat("RackMetal", new Color(0.10f, 0.12f, 0.16f));
            var tile = Mat("JuntaPisoElevado", new Color(0.30f, 0.34f, 0.42f));
            var frio = Mat("PasilloFrio", new Color(0.35f, 0.62f, 0.85f));
            var amarillo = Mat("LineaAmarilla", new Color(0.95f, 0.78f, 0.06f));
            var negro = Negro;
            var mesa = Mat("MesaOficina", new Color(0.30f, 0.32f, 0.36f));
            var silla = Mat("SillaOficina", new Color(0.12f, 0.13f, 0.15f));
            var cableR = Mat("CableRojo", new Color(0.55f, 0.08f, 0.08f));
            var cableA = Mat("CableAzul", new Color(0.10f, 0.25f, 0.55f));
            var ducto = Mat("DuctoAire", new Color(0.50f, 0.54f, 0.58f));
            var extintor = Mat("Extintor", new Color(0.75f, 0.08f, 0.06f));
            var leds = new[]
            {
                Mat("LedVerdeTenue", new Color(0.2f, 1f, 0.4f), 1.1f),
                Mat("LedCianTenue", new Color(0.2f, 0.8f, 1f), 1.1f),
                Mat("LedAmbarTenue", new Color(1f, 0.65f, 0.1f), 1.1f),
            };
            var ledRojo = Mat("LedRojoAcceso", new Color(1f, 0.1f, 0.08f), 2.6f);
            var ledVerde = Mat("LedVerdeAcceso", new Color(0.15f, 1f, 0.35f), 2.6f);
            var pantalla = Mat("PantallaAzul", new Color(0.35f, 0.75f, 1f), 1.8f);
            var pantallaB = Mat("PantallaBlanca", new Color(0.85f, 0.95f, 1f), 1.5f);
            var pantallaV = Mat("PantallaVerde", new Color(0.25f, 0.95f, 0.5f), 1.6f);
            var holo = Mat("HoloCian", new Color(0.2f, 0.75f, 1f), 1.4f);

            // ------------------------------------------------ a) piso elevado: juntas de baldosa cada 2 m, y corredores marcados
            var gPiso = Grupo(g, "Piso");
            for (float x = -24f; x <= 24f; x += 2f)
                CajaPF(gPiso, "Junta_X", new Vector3(x, 0.125f, -60f), new Vector3(0.05f, 0.012f, 37.6f), tile);
            for (float z = -78f; z <= -42f; z += 2f)
                CajaPF(gPiso, "Junta_Z", new Vector3(0f, 0.126f, z), new Vector3(49.6f, 0.012f, 0.05f), tile);
            // Pasillo frio: franja celeste entre las dos filas de racks centrales; flechas amarillas hacia la terminal.
            CajaPF(gPiso, "Pasillo_Frio_Centro", new Vector3(0f, 0.13f, -62f), new Vector3(1.6f, 0.012f, 32f), frio);
            for (int i = 0; i < 6; i++)
                CajaPF(gPiso, "Flecha_Terminal", new Vector3(0f, 0.135f, -74f + i * 4.5f), new Vector3(0.9f, 0.012f, 0.35f), amarillo);
            // Cinta amarilla de seguridad alrededor de cada rack-cobertura.
            for (int i = 1; i <= 6; i++)
            {
                var o = GameObject.Find("Rack_" + i);
                if (o == null) continue;
                var b = LimitesDe(o);
                float m = 0.55f, t = 0.08f;
                CajaPF(gPiso, "Cinta_N", new Vector3(b.center.x, 0.135f, b.max.z + m), new Vector3(b.size.x + 2 * m, 0.012f, t), amarillo);
                CajaPF(gPiso, "Cinta_S", new Vector3(b.center.x, 0.135f, b.min.z - m), new Vector3(b.size.x + 2 * m, 0.012f, t), amarillo);
                CajaPF(gPiso, "Cinta_E", new Vector3(b.max.x + m, 0.135f, b.center.z), new Vector3(t, 0.012f, b.size.z + 2 * m), amarillo);
                CajaPF(gPiso, "Cinta_O", new Vector3(b.min.x - m, 0.135f, b.center.z), new Vector3(t, 0.012f, b.size.z + 2 * m), amarillo);
            }

            // ------------------------------------------------ b) filas de gabinetes pegadas al muro sur (puertas perforadas + LEDs)
            var gGab = Grupo(g, "Gabinetes");
            int idx = 0;
            for (int lado = -1; lado <= 1; lado += 2)
                for (int k = 0; k < 19; k++)
                {
                    float x = lado * (5.6f + k * 0.98f);
                    float z = -78.45f;
                    CajaPF(gGab, "Gabinete", new Vector3(x, 1.2f, z), new Vector3(0.92f, 2.2f, 1.0f), gab, 0f, true);
                    // puerta frontal perforada (hacia +z) con rejilla oscura y marco claro
                    CajaPF(gGab, "Puerta", new Vector3(x, 1.2f, z + 0.52f), new Vector3(0.82f, 2.0f, 0.04f), gabFrente);
                    // unidades de servidor apiladas (bandejas 1U) con un LED cada una; alguna en ambar o rojo (unidad con falla)
                    for (int u = 0; u < 4; u++)
                    {
                        float yu = 0.55f + u * 0.42f;
                        CajaPF(gGab, "Unidad", new Vector3(x - 0.05f, yu, z + 0.545f), new Vector3(0.66f, 0.3f, 0.03f), u % 2 == 0 ? gabRejilla : metal);
                        var led = leds[(idx + u + k) % leds.Length];
                        if (rnd.Next(16) == 0) led = ledRojo;
                        CajaPF(gGab, "Led", new Vector3(x + 0.3f, yu + 0.06f, z + 0.565f), new Vector3(0.07f, 0.07f, 0.03f), led);
                    }
                    CajaPF(gGab, "Ventilacion", new Vector3(x, 2.1f, z + 0.545f), new Vector3(0.66f, 0.1f, 0.03f), gabRejilla);
                    if (k % 3 == 1) CajaPF(gGab, "Cable_Subida", new Vector3(x, 4.55f, z + 0.1f), new Vector3(0.12f, 4.9f, 0.12f), (k + lado) % 2 == 0 ? cableR : cableA);
                    idx++;
                }

            // ------------------------------------------------ c) puestos de operador con PC en el muro norte
            var gPc = Grupo(g, "PuestosNorte");
            foreach (int lado in new[] { -1, 1 })
                for (int k = 0; k < 4; k++)
                {
                    float x = lado * (14f + k * 2.6f);
                    float z = -42.5f;
                    CajaPF(gPc, "Escritorio", new Vector3(x, 0.4f, z), new Vector3(2.0f, 0.8f, 1.0f), mesa, 0f, true);
                    var pant = (k + (lado > 0 ? 1 : 0)) % 3 == 0 ? pantallaV : (k % 2 == 0 ? pantalla : pantallaB);
                    CajaPF(gPc, "Monitor", new Vector3(x - 0.45f, 1.2f, z - 0.2f), new Vector3(0.8f, 0.5f, 0.05f), pant);
                    CajaPF(gPc, "Monitor_B", new Vector3(x + 0.45f, 1.2f, z - 0.2f), new Vector3(0.8f, 0.5f, 0.05f), pantalla);
                    CajaPF(gPc, "Monitor_Pie", new Vector3(x, 0.9f, z - 0.2f), new Vector3(0.14f, 0.2f, 0.14f), metalOsc);
                    CajaPF(gPc, "Teclado", new Vector3(x, 0.82f, z + 0.18f), new Vector3(0.6f, 0.03f, 0.22f), negro);
                    CajaPF(gPc, "Torre_PC", new Vector3(x + 0.85f, 0.25f, z - 0.2f), new Vector3(0.26f, 0.5f, 0.5f), metalOsc);
                    CajaPF(gPc, "Torre_Led", new Vector3(x + 0.85f, 0.4f, z + 0.06f), new Vector3(0.05f, 0.05f, 0.02f), leds[rnd.Next(leds.Length)]);
                    CajaPF(gPc, "Silla_Asiento", new Vector3(x, 0.5f, z + 1.15f), new Vector3(0.5f, 0.08f, 0.5f), silla);
                    CajaPF(gPc, "Silla_Respaldo", new Vector3(x, 0.82f, z + 1.4f), new Vector3(0.5f, 0.55f, 0.07f), silla);
                    FormaPF(PrimitiveType.Cylinder, gPc, "Silla_Pie", new Vector3(x, 0.25f, z + 1.15f), new Vector3(0.1f, 0.25f, 0.1f), silla);
                }

            // ------------------------------------------------ d) paredes de video (2 x 3 pantallas) en los muros oeste y este
            var gVid = Grupo(g, "ParedesDeVideo");
            foreach (int lado in new[] { -1, 1 })
            {
                float xp = lado * 25.0f;
                CajaPF(gVid, "Marco", new Vector3(xp, 3.0f, -47.5f), new Vector3(0.1f, 2.5f, 10.4f), metalOsc);
                for (int fila = 0; fila < 2; fila++)
                    for (int col = 0; col < 3; col++)
                    {
                        var pm = (fila + col + (lado > 0 ? 1 : 0)) % 3 == 0 ? holo : ((fila + col) % 2 == 0 ? pantalla : pantallaB);
                        CajaPF(gVid, "Pantalla", new Vector3(xp - lado * 0.07f, 2.4f + fila * 1.2f, -51.4f + col * 3.4f), new Vector3(0.05f, 1.05f, 3.2f), pm);
                    }
                CajaPF(gVid, "Consola", new Vector3(xp - lado * 0.9f, 0.55f, -47.5f), new Vector3(0.9f, 1.1f, 9.6f), mesa, 0f, true);
                for (int j = 0; j < 4; j++)
                    CajaPF(gVid, "Consola_Pantalla", new Vector3(xp - lado * 0.72f, 1.2f, -51f + j * 2.8f), new Vector3(0.05f, 0.45f, 1.2f), j % 2 == 0 ? pantallaV : pantalla);
            }

            // ------------------------------------------------ e) techo: bandejas de cable y ductos de aire
            var gTecho = Grupo(g, "Techo");
            foreach (float x in new[] { -17f, -4.5f, 4.5f, 17f })
            {
                CajaPF(gTecho, "Bandeja", new Vector3(x, 7.25f, -60f), new Vector3(0.55f, 0.12f, 36f), negro);
                CajaPF(gTecho, "Cable_R", new Vector3(x - 0.14f, 7.33f, -60f), new Vector3(0.08f, 0.05f, 35.6f), cableR);
                CajaPF(gTecho, "Cable_A", new Vector3(x + 0.14f, 7.33f, -60f), new Vector3(0.08f, 0.05f, 35.6f), cableA);
            }
            foreach (float z in new[] { -69f, -52f })
            {
                CajaPF(gTecho, "Ducto", new Vector3(0f, 7.55f, z), new Vector3(46f, 0.6f, 0.9f), ducto, 0f, true);
                for (int k = -3; k <= 3; k++)
                    CajaPF(gTecho, "Rejilla_Aire", new Vector3(k * 6.5f, 7.22f, z), new Vector3(1.4f, 0.04f, 0.7f), gabRejilla);
            }

            // paneles de luz de techo (emisivos): 3 filas a lo largo de la sala
            var panelLuz = Mat("PanelLuzTecho", new Color(0.8f, 0.92f, 1f), 1.1f);
            foreach (float x in new[] { -11f, 0f, 11f })
                for (int k = 0; k < 7; k++)
                    CajaPF(gTecho, "Panel_Luz", new Vector3(x, 7.18f, -76f + k * 5.2f), new Vector3(1.3f, 0.05f, 2.4f), panelLuz);

            // ------------------------------------------------ f) puertas de acceso: sur (x -4..4), oeste y este (z -62..-56)
            var gPu = Grupo(g, "PuertasDeAcceso");
            void PuertaSur()
            {
                float z = -80f;
                CajaPF(gPu, "Jamba_O", new Vector3(-4.2f, 2.3f, z), new Vector3(0.45f, 4.6f, 1.7f), metal, 0f, true);
                CajaPF(gPu, "Jamba_E", new Vector3(4.2f, 2.3f, z), new Vector3(0.45f, 4.6f, 1.7f), metal, 0f, true);
                CajaPF(gPu, "Dintel", new Vector3(0f, 4.55f, z), new Vector3(8.85f, 0.55f, 1.7f), metal, 0f, true);
                CajaPF(gPu, "Dintel_Luz", new Vector3(0f, 4.2f, z + 0.86f), new Vector3(7.6f, 0.08f, 0.04f), leds[2]);
                CajaPF(gPu, "Hoja_O", new Vector3(-3.0f, 1.9f, z), new Vector3(1.6f, 3.8f, 0.16f), metalOsc, 0f, true);
                CajaPF(gPu, "Hoja_E", new Vector3(3.0f, 1.9f, z), new Vector3(1.6f, 3.8f, 0.16f), metalOsc, 0f, true);
                CajaPF(gPu, "Hoja_O_Franja", new Vector3(-3.0f, 1.9f, z + 0.09f), new Vector3(1.2f, 0.12f, 0.02f), amarillo);
                CajaPF(gPu, "Hoja_E_Franja", new Vector3(3.0f, 1.9f, z + 0.09f), new Vector3(1.2f, 0.12f, 0.02f), amarillo);
                for (int k = 0; k < 8; k++)
                {
                    var m = k % 2 == 0 ? amarillo : negro;
                    CajaPF(gPu, "Franja_Aviso_O", new Vector3(-4.2f, 0.3f + k * 0.55f, z + 0.86f), new Vector3(0.45f, 0.28f, 0.03f), m);
                    CajaPF(gPu, "Franja_Aviso_E", new Vector3(4.2f, 0.3f + k * 0.55f, z + 0.86f), new Vector3(0.45f, 0.28f, 0.03f), m);
                }
                // lectores de tarjeta en el interior del muro, con luz roja/verde
                foreach (int s in new[] { -1, 1 })
                {
                    CajaPF(gPu, "Lector", new Vector3(s * 5.2f, 1.35f, -79.2f), new Vector3(0.28f, 0.42f, 0.08f), metalOsc);
                    CajaPF(gPu, "Lector_Led", new Vector3(s * 5.2f, 1.5f, -79.15f), new Vector3(0.08f, 0.08f, 0.03f), s < 0 ? ledVerde : ledRojo);
                }
                FormaPF(PrimitiveType.Sphere, gPu, "Baliza_Roja", new Vector3(0f, 5.05f, z + 0.5f), new Vector3(0.45f, 0.45f, 0.45f), ledRojo);
            }
            void PuertaLateral(int lado)
            {
                float x = lado * 26f;
                CajaPF(gPu, "Jamba_N", new Vector3(x, 2.3f, -55.8f), new Vector3(1.7f, 4.6f, 0.45f), metal, 0f, true);
                CajaPF(gPu, "Jamba_S", new Vector3(x, 2.3f, -62.2f), new Vector3(1.7f, 4.6f, 0.45f), metal, 0f, true);
                CajaPF(gPu, "Dintel", new Vector3(x, 4.55f, -59f), new Vector3(1.7f, 0.55f, 6.85f), metal, 0f, true);
                CajaPF(gPu, "Dintel_Luz", new Vector3(x - lado * 0.86f, 4.2f, -59f), new Vector3(0.04f, 0.08f, 5.6f), leds[2]);
                CajaPF(gPu, "Hoja_N", new Vector3(x, 1.9f, -57.1f), new Vector3(0.16f, 3.8f, 1.6f), metalOsc, 0f, true);
                CajaPF(gPu, "Hoja_S", new Vector3(x, 1.9f, -60.9f), new Vector3(0.16f, 3.8f, 1.6f), metalOsc, 0f, true);
                CajaPF(gPu, "Hoja_N_Franja", new Vector3(x - lado * 0.09f, 1.9f, -57.1f), new Vector3(0.02f, 0.12f, 1.2f), amarillo);
                CajaPF(gPu, "Hoja_S_Franja", new Vector3(x - lado * 0.09f, 1.9f, -60.9f), new Vector3(0.02f, 0.12f, 1.2f), amarillo);
                for (int k = 0; k < 8; k++)
                {
                    var m = k % 2 == 0 ? amarillo : negro;
                    CajaPF(gPu, "Franja_Aviso_N", new Vector3(x - lado * 0.86f, 0.3f + k * 0.55f, -55.8f), new Vector3(0.03f, 0.28f, 0.45f), m);
                    CajaPF(gPu, "Franja_Aviso_S", new Vector3(x - lado * 0.86f, 0.3f + k * 0.55f, -62.2f), new Vector3(0.03f, 0.28f, 0.45f), m);
                }
                CajaPF(gPu, "Lector", new Vector3(x - lado * 0.8f, 1.35f, -63.1f), new Vector3(0.08f, 0.42f, 0.28f), metalOsc);
                CajaPF(gPu, "Lector_Led", new Vector3(x - lado * 0.76f, 1.5f, -63.1f), new Vector3(0.03f, 0.08f, 0.08f), ledVerde);
                FormaPF(PrimitiveType.Sphere, gPu, "Baliza_Roja", new Vector3(x - lado * 0.5f, 5.05f, -59f), new Vector3(0.45f, 0.45f, 0.45f), ledRojo);
            }
            PuertaSur(); PuertaLateral(-1); PuertaLateral(1);

            // ------------------------------------------------ g) equipamiento: extintores, UPS y cables de piso hacia las puertas
            var gEq = Grupo(g, "Equipamiento");
            foreach (var p in new[] { new Vector2(-24.4f, -78.2f), new Vector2(24.4f, -78.2f), new Vector2(-24.4f, -42.2f), new Vector2(24.4f, -42.2f), new Vector2(-6.4f, -78.4f), new Vector2(6.4f, -78.4f) })
            {
                FormaPF(PrimitiveType.Cylinder, gEq, "Extintor", new Vector3(p.x, 0.4f, p.y), new Vector3(0.22f, 0.4f, 0.22f), extintor);
                CajaPF(gEq, "Extintor_Soporte", new Vector3(p.x, 0.75f, p.y + (p.y < -60f ? -0.14f : 0.14f)), new Vector3(0.3f, 0.1f, 0.06f), metalOsc);
            }
            foreach (int lado in new[] { -1, 1 })
            {
                CajaPF(gEq, "UPS", new Vector3(lado * 10.2f, 0.7f, -42.2f), new Vector3(1.6f, 1.4f, 0.9f), gab, 0f, true);
                CajaPF(gEq, "UPS_Panel", new Vector3(lado * 10.2f, 1.0f, -41.73f), new Vector3(1.2f, 0.5f, 0.03f), gabFrente);
                CajaPF(gEq, "UPS_Led", new Vector3(lado * 10.2f, 1.25f, -41.7f), new Vector3(0.9f, 0.05f, 0.03f), leds[0]);
                CajaPF(gEq, "Cable_Piso_Lateral", new Vector3(lado * 13f, 0.14f, -59f), new Vector3(26f, 0.03f, 0.16f), lado < 0 ? cableR : cableA);
            }

            EditorSceneManager.MarkSceneDirty(escena);
            return $"Servidores: objetos={PulidoFinalObjetos - n0}";
        }
    }
}
