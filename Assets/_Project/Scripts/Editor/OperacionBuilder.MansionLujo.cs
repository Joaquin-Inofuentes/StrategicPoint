using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Las veinte pasadas de lujo de la mansion (todo visual, sin colliders: el NavMesh no cambia). Cada Iter* es una pasada.
    public static partial class OperacionBuilder
    {
        static Material MHoja => Mat("Mansion_Hoja", new Color(0.16f, 0.38f, 0.17f));
        static Material MLibroA => Mat("Mansion_LibroA", new Color(0.45f, 0.10f, 0.10f));
        static Material MLibroB => Mat("Mansion_LibroB", new Color(0.12f, 0.25f, 0.40f));
        static Material MLibroC => Mat("Mansion_LibroC", new Color(0.62f, 0.50f, 0.20f));
        static Material MEspejo => Mat("Mansion_Espejo", new Color(0.62f, 0.78f, 0.85f), 0.35f);
        static Material MTela => Mat("Mansion_Tela", new Color(0.40f, 0.06f, 0.12f));

        const float ZI0 = 246.6f, ZI1 = 285.4f, XI0 = 296.6f, XI1 = 347.4f;

        // Lo que toca una torre de ese piso no se decora (las escaleras ya traen lo suyo).
        static bool EnTorre(float x, float z, int nivel, float m = 1.2f) => SobreTorre(x - m, x + m, z - m, z + m, nivel);

        static void LuzPuntual(Transform p, string n, Vector3 pos, float rango, float intensidad, Color c)
        {
            var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.position = pos;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = rango; l.intensity = intensidad; l.color = c; l.shadows = LightShadows.None;
        }

        static void Esfera(Transform p, string n, Vector3 c, float d, Material m)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = n; s.transform.SetParent(p, false);
            s.transform.position = c; s.transform.localScale = Vector3.one * d; s.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(s.GetComponent<Collider>()); s.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        static void Cilindro(Transform p, string n, float x, float y0, float y1, float z, float d, Material m)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = n; c.transform.SetParent(p, false);
            c.transform.position = new Vector3(x, (y0 + y1) * 0.5f, z); c.transform.localScale = new Vector3(d, (y1 - y0) * 0.5f, d);
            c.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(c.GetComponent<Collider>()); c.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        static void Aplique(Transform p, float x, float y, float z, int dx, int dz)
        {
            Caja(p, "Aplique_Base", x - 0.16f - Mathf.Abs(dx) * 0.05f, x + 0.16f + Mathf.Abs(dx) * 0.05f, y - 0.3f, y + 0.3f, z - 0.16f - Mathf.Abs(dz) * 0.05f, z + 0.16f + Mathf.Abs(dz) * 0.05f, MDorado, false);
            Caja(p, "Aplique_Luz", x + dx * 0.12f - 0.11f, x + dx * 0.12f + 0.11f, y - 0.2f, y + 0.25f, z + dz * 0.12f - 0.11f, z + dz * 0.12f + 0.11f, MCristal, false);
            LuzPuntual(p, "Luz_Aplique", new Vector3(x + dx * 0.9f, y, z + dz * 0.9f), 9f, 0.9f, new Color(1f, 0.86f, 0.62f));
        }

        static void Planta(Transform p, float x, float y, float z, float h = 1.6f)
        {
            Cilindro(p, "Maceta", x, y, y + 0.6f, z, 0.9f, MMarmolOsc);
            Cilindro(p, "Tallo", x, y + 0.6f, y + 0.6f + h * 0.5f, z, 0.12f, MMueble);
            Esfera(p, "Follaje_1", new Vector3(x, y + 0.6f + h * 0.7f, z), 1.5f, MHoja);
            Esfera(p, "Follaje_2", new Vector3(x + 0.35f, y + 0.6f + h * 0.4f, z - 0.2f), 1.0f, MHoja);
            Esfera(p, "Follaje_3", new Vector3(x - 0.35f, y + 0.6f + h * 0.5f, z + 0.25f), 1.0f, MHoja);
        }

        static void Estatua(Transform p, float x, float y, float z)
        {
            Caja(p, "Pedestal", x - 0.55f, x + 0.55f, y, y + 1.1f, z - 0.55f, z + 0.55f, MMarmol, false);
            Caja(p, "Pedestal_Cornisa", x - 0.65f, x + 0.65f, y + 1.1f, y + 1.2f, z - 0.65f, z + 0.65f, MDorado, false);
            Cilindro(p, "Estatua_Cuerpo", x, y + 1.2f, y + 2.5f, z, 0.7f, MDorado);
            Esfera(p, "Estatua_Cabeza", new Vector3(x, y + 2.75f, z), 0.55f, MDorado);
            Caja(p, "Estatua_Brazo", x - 0.55f, x - 0.35f, y + 1.7f, y + 2.6f, z - 0.1f, z + 0.1f, MDorado, false);
        }

        static void Silla(Transform p, float x, float y, float z, int mira)
        {
            Caja(p, "Silla_Asiento", x - 0.3f, x + 0.3f, y + 0.45f, y + 0.55f, z - 0.3f, z + 0.3f, MMueble, false);
            Caja(p, "Silla_Patas", x - 0.25f, x + 0.25f, y, y + 0.45f, z - 0.25f, z + 0.25f, MDorado, false);
            if (mira == 0) Caja(p, "Silla_Respaldo", x - 0.3f, x + 0.3f, y + 0.55f, y + 1.2f, z - 0.3f, z - 0.22f, MMueble, false);
            else if (mira == 1) Caja(p, "Silla_Respaldo", x - 0.3f, x + 0.3f, y + 0.55f, y + 1.2f, z + 0.22f, z + 0.3f, MMueble, false);
            else if (mira == 2) Caja(p, "Silla_Respaldo", x - 0.3f, x - 0.22f, y + 0.55f, y + 1.2f, z - 0.3f, z + 0.3f, MMueble, false);
            else Caja(p, "Silla_Respaldo", x + 0.22f, x + 0.3f, y + 0.55f, y + 1.2f, z - 0.3f, z + 0.3f, MMueble, false);
        }

        static void LuzDeLujoIter1a10(Transform interior, Transform deco)
        {
            // ---- Iter 1: apliques de pared con luz calida en los tres pisos ----
            for (int n = 0; n <= 2; n++)
            {
                float y = G.Nivel(n) + 3.6f;
                for (float x = 303f; x < 345f; x += 8.5f)
                {
                    if (!EnTorre(x, ZI1, n, 2f)) Aplique(deco, x, y, ZI1 - 0.2f, 0, -1);
                    if (!EnTorre(x, ZI0, n, 2f) && !(n == 0 && x > 316f && x < 328f)) Aplique(deco, x, y, ZI0 + 0.2f, 0, 1);
                }
                foreach (float z in new[] { 254f, 266f, 277f })
                {
                    if (!EnTorre(XI0, z, n, 2f)) Aplique(deco, XI0 + 0.2f, y, z, 1, 0);
                    if (!EnTorre(XI1, z, n, 2f)) Aplique(deco, XI1 - 0.2f, y, z, -1, 0);
                }
            }

            // ---- Iter 2: zocalo de madera y cornisa dorada interior ----
            for (int n = 0; n <= 2; n++)
            {
                float yb = G.Nivel(n), yt = G.Nivel(n + 1) - G.Losa;
                Caja(deco, "Zocalo_S", XI0, XI1, yb + 0.14f, yb + 0.55f, ZI0, ZI0 + 0.1f, MMueble, false);
                Caja(deco, "Zocalo_N", XI0, XI1, yb + 0.14f, yb + 0.55f, ZI1 - 0.1f, ZI1, MMueble, false);
                Caja(deco, "Zocalo_O", XI0, XI0 + 0.1f, yb + 0.14f, yb + 0.55f, ZI0, ZI1, MMueble, false);
                Caja(deco, "Zocalo_E", XI1 - 0.1f, XI1, yb + 0.14f, yb + 0.55f, ZI0, ZI1, MMueble, false);
                Caja(deco, "Cornisa_Int_S", XI0, XI1, yt - 0.4f, yt, ZI0, ZI0 + 0.25f, MDorado, false);
                Caja(deco, "Cornisa_Int_N", XI0, XI1, yt - 0.4f, yt, ZI1 - 0.25f, ZI1, MDorado, false);
                Caja(deco, "Cornisa_Int_O", XI0, XI0 + 0.25f, yt - 0.4f, yt, ZI0, ZI1, MDorado, false);
                Caja(deco, "Cornisa_Int_E", XI1 - 0.25f, XI1, yt - 0.4f, yt, ZI0, ZI1, MDorado, false);
            }

            // ---- Iter 3: boiserie (paneles de madera con marco dorado) en la planta baja ----
            void Panel(float x0, float x1, float z, int cara)
            {
                for (float x = x0; x + 2.6f <= x1; x += 3.2f)
                {
                    float zz = z + cara * 0.05f;
                    Caja(deco, "Boiserie", x, x + 2.6f, 0.7f, 3.3f, zz - 0.03f, zz + 0.03f, MMueble, false);
                    Caja(deco, "Boiserie_Marco", x + 0.08f, x + 2.52f, 3.2f, 3.3f, zz - 0.04f, zz + 0.04f, MDorado, false);
                    Caja(deco, "Boiserie_Marco", x + 0.08f, x + 2.52f, 0.7f, 0.8f, zz - 0.04f, zz + 0.04f, MDorado, false);
                }
            }
            Panel(309f, 345f, ZI1 - 0.2f, -1);
            Panel(298f, 316f, ZI0 + 0.2f, 1);
            Panel(329f, 345f, ZI0 + 0.2f, 1);

            // ---- Iter 4: iluminacion indirecta (tira emisiva en el cielorraso) en los tres pisos ----
            for (int n = 0; n <= 2; n++)
            {
                float yt = G.Nivel(n + 1) - G.Losa;
                Caja(deco, "Cove_S", XI0 + 0.3f, XI1 - 0.3f, yt - 0.48f, yt - 0.42f, ZI0 + 0.25f, ZI0 + 0.45f, MCristal, false);
                Caja(deco, "Cove_N", XI0 + 0.3f, XI1 - 0.3f, yt - 0.48f, yt - 0.42f, ZI1 - 0.45f, ZI1 - 0.25f, MCristal, false);
                Caja(deco, "Cove_O", XI0 + 0.25f, XI0 + 0.45f, yt - 0.48f, yt - 0.42f, ZI0 + 0.3f, ZI1 - 0.3f, MCristal, false);
                Caja(deco, "Cove_E", XI1 - 0.45f, XI1 - 0.25f, yt - 0.48f, yt - 0.42f, ZI0 + 0.3f, ZI1 - 0.3f, MCristal, false);
            }

            // ---- Iter 5: estatuas sobre pedestales en la planta baja ----
            foreach (var (x, z) in new[] { (312.5f, 258f), (331.5f, 258f), (312.5f, 270f), (331.5f, 270f) }) Estatua(deco, x, 0.13f, z);

            // ---- Iter 6: plantas grandes en macetas ----
            foreach (var (x, z) in new[] { (300f, 250f), (344f, 250f), (344f, 282f), (312f, 249.5f), (332f, 249.5f), (320f, 282f), (326f, 282f) }) Planta(deco, x, 0.13f, z);
            foreach (var (x, z) in new[] { (300f, 262f), (344f, 262f), (344f, 270f) }) if (!EnTorre(x, z, 1)) Planta(deco, x, G.Nivel(1), z, 1.3f);

            // ---- Iter 7: bibliotecas con libros en los salones del norte del 1.er piso ----
            float y1 = G.Nivel(1);
            for (float x = 309f; x + 2.8f <= 346f; x += 3.4f)
            {
                if (EnTorre(x + 1.4f, ZI1 - 0.3f, 1, 1.5f)) continue;
                Caja(deco, "Estante", x, x + 2.8f, y1 + 0.1f, y1 + 4.4f, ZI1 - 0.5f, ZI1, MMueble, false);
                for (int fila = 0; fila < 4; fila++)
                {
                    float yf = y1 + 0.25f + fila * 1.0f;
                    for (int k = 0; k < 4; k++)
                    {
                        var mat = ((fila + k) % 3 == 0) ? MLibroA : ((fila + k) % 3 == 1) ? MLibroB : MLibroC;
                        Caja(deco, "Libros", x + 0.1f + k * 0.65f, x + 0.1f + k * 0.65f + 0.6f, yf, yf + 0.75f, ZI1 - 0.46f, ZI1 - 0.12f, mat, false);
                    }
                }
            }

            // ---- Iter 8: sillas alrededor de las mesas del 1.er piso ----
            foreach (var (x, z) in new[] { (303f, 252.5f), (316f, 252.5f), (329f, 252.5f), (313.5f, 279f), (327f, 279f), (340.5f, 279f) })
            {
                Silla(deco, x - 0.8f, y1, z - 1.3f, 1); Silla(deco, x + 0.8f, y1, z - 1.3f, 1);
                Silla(deco, x - 0.8f, y1, z + 1.3f, 0); Silla(deco, x + 0.8f, y1, z + 1.3f, 0);
                Caja(deco, "Mantel", x - 1.5f, x + 1.5f, y1 + 0.95f, y1 + 0.99f, z - 1.0f, z + 1.0f, MTela, false);
                Cilindro(deco, "Florero", x, y1 + 0.99f, y1 + 1.4f, z, 0.3f, MCristal);
            }

            // ---- Iter 9: cortinas burdeos con baston dorado en los ventanales ----
            for (int n = 0; n < 3; n++)
            {
                float yc = G.Nivel(n) + 3.4f;
                for (float x = 300f; x < 346f; x += 5.4f)
                {
                    if (n == 0 && x > G.PortonX - G.PortonAncho * 0.5f - 3f && x < G.PortonX + G.PortonAncho * 0.5f + 3f) continue;
                    if (!EnTorre(x, ZI0, n, 1.5f))
                    {
                        Caja(deco, "Cortina_S", x - 1.5f, x - 0.95f, yc - 2.0f, yc + 2.1f, ZI0 + 0.06f, ZI0 + 0.3f, MTela, false);
                        Caja(deco, "Cortina_S", x + 0.95f, x + 1.5f, yc - 2.0f, yc + 2.1f, ZI0 + 0.06f, ZI0 + 0.3f, MTela, false);
                        Caja(deco, "Baston_S", x - 1.6f, x + 1.6f, yc + 2.1f, yc + 2.2f, ZI0 + 0.1f, ZI0 + 0.2f, MDorado, false);
                    }
                    if (!EnTorre(x, ZI1, n, 1.5f))
                    {
                        Caja(deco, "Cortina_N", x - 1.5f, x - 0.95f, yc - 2.0f, yc + 2.1f, ZI1 - 0.3f, ZI1 - 0.06f, MTela, false);
                        Caja(deco, "Cortina_N", x + 0.95f, x + 1.5f, yc - 2.0f, yc + 2.1f, ZI1 - 0.3f, ZI1 - 0.06f, MTela, false);
                        Caja(deco, "Baston_N", x - 1.6f, x + 1.6f, yc + 2.1f, yc + 2.2f, ZI1 - 0.2f, ZI1 - 0.1f, MDorado, false);
                    }
                }
            }

            // ---- Iter 10: salon de baile: pista con rosetón dorado, escenario y espejos ----
            float y2 = G.Nivel(2);
            Cilindro(deco, "Pista_Dorada", 322f, y2 + 0.07f, y2 + 0.09f, 260f, 12.4f, MDorado);
            Cilindro(deco, "Pista_Oscura", 322f, y2 + 0.09f, y2 + 0.11f, 260f, 11.4f, MMarmolOsc);
            Cilindro(deco, "Pista_Clara", 322f, y2 + 0.11f, y2 + 0.13f, 260f, 8f, MMarmol);
            Cilindro(deco, "Pista_Centro", 322f, y2 + 0.13f, y2 + 0.15f, 260f, 3.2f, MDorado);
            // #408: se quito Escenario (plataforma sin collider que no aportaba nada)
            // #408: se quito Escenario_Borde (plataforma sin collider que no aportaba nada)
            // #408: se quito Escenario_Telon (plataforma sin collider que no aportaba nada)
            for (float z = 250f; z < 258f; z += 3.7f) Caja(deco, "Espejo", XI1 - 0.12f, XI1 - 0.05f, y2 + 0.8f, y2 + 4.4f, z, z + 3.1f, MEspejo, false);
            for (float z = 262f; z < 272f; z += 3.7f) Caja(deco, "Espejo", XI1 - 0.12f, XI1 - 0.05f, y2 + 0.8f, y2 + 4.4f, z, z + 3.1f, MEspejo, false);
        }

        // Punto de entrada: se llama al final de ArmarMansion.
        static void PasadasDeLujo(Transform interior, Transform deco)
        {
            LuzDeLujoIter1a10(interior, deco);
            LuzDeLujoIter11a17(interior, deco);
            Iter20Torres(deco);
        }

        // ---- Iter 20: escaleras de honor: cuadros con marco dorado y apliques sobre la divisoria, siguiendo la pendiente de cada tramo ----
        static void Iter20Torres(Transform deco)
        {
            foreach (var t in G.Torres)
            {
                float y0 = G.Nivel(t.nivelBase), H = G.Piso, yMed = y0 + H * 0.5f, run = G.TramoLargo, ini = 0.3f;
                float uA = G.DivisoriaU0, uB = G.DivisoriaU1;
                string k = "Torre_" + t.nombre + "_Lujo_";
                foreach (float v in new[] { 2.5f, 6.5f })
                {
                    float yA = y0 + (v - ini) / run * (H * 0.5f), yB = yMed + (run + ini - v) / run * (H * 0.5f);
                    var pa = t.Mundo(uA - 0.2f, v); var pb = t.Mundo(uB + 0.2f, v);
                    Aplique(deco, pa.x, yA + 3.3f, pa.y, -(int)t.u.x, -(int)t.u.y);
                    Aplique(deco, pb.x, yB + 3.3f, pb.y, (int)t.u.x, (int)t.u.y);
                }
                float vc = 4.6f;
                CajaTorre(deco, t, k + "Cuadro_A", uA - 0.1f, uA, vc - 1.0f, vc + 1.0f, y0 + H * 0.25f + 1.8f, y0 + H * 0.25f + 3.2f, MCuadroA, false);
                CajaTorre(deco, t, k + "Marco_A", uA - 0.06f, uA, vc - 1.15f, vc + 1.15f, y0 + H * 0.25f + 1.65f, y0 + H * 0.25f + 3.35f, MDorado, false);
                CajaTorre(deco, t, k + "Cuadro_B", uB, uB + 0.1f, vc - 1.0f, vc + 1.0f, yMed + H * 0.25f + 1.8f, yMed + H * 0.25f + 3.2f, MCuadroB, false);
                CajaTorre(deco, t, k + "Marco_B", uB, uB + 0.06f, vc - 1.15f, vc + 1.15f, yMed + H * 0.25f + 1.65f, yMed + H * 0.25f + 3.35f, MDorado, false);
            }
        }
    }
}
