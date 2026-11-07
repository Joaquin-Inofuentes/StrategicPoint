using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Pasadas de lujo 11 a 17: salon de baile, celdas, azotea y fachada (todo visual, sin colliders).
    public static partial class OperacionBuilder
    {
        static void LuzDeLujoIter11a17(Transform interior, Transform deco)
        {
            float y2 = G.Nivel(2), y3 = G.Nivel(3);
            // ---- Iter 11: salon de baile: lamparas de pie, mesas altas de coctel y plantas ----
            foreach (var (x, z) in new[] { (309f, 254f), (335f, 254f), (309f, 268f), (335f, 270f), (322f, 250f), (322f, 270f) })
            {
                if (EnTorre(x, z, 2, 1.6f)) continue;
                Cilindro(deco, "Lampara_Pie_Poste", x, y2, y2 + 2.2f, z, 0.1f, MDorado);
                Caja(deco, "Lampara_Pie_Pantalla", x - 0.4f, x + 0.4f, y2 + 2.2f, y2 + 2.8f, z - 0.4f, z + 0.4f, MCristal, false);
                LuzPuntual(deco, "Luz_Lampara_Pie", new Vector3(x, y2 + 2.5f, z), 10f, 0.9f, new Color(1f, 0.88f, 0.66f));
            }
            foreach (var (x, z) in new[] { (314f, 276f), (326f, 276f), (338f, 262f), (306f, 266f), (316f, 256f) })
            {
                if (EnTorre(x, z, 2, 1.6f)) continue;
                Cilindro(deco, "Mesa_Alta_Pie", x, y2, y2 + 1.1f, z, 0.18f, MDorado);
                Cilindro(deco, "Mesa_Alta_Tapa", x, y2 + 1.1f, y2 + 1.18f, z, 1.1f, MMarmol);
                Esfera(deco, "Copa", new Vector3(x + 0.2f, y2 + 1.3f, z), 0.18f, MCristal);
            }
            foreach (var (x, z) in new[] { (300f, 276f), (344f, 252f), (344f, 266f), (330f, 249.5f) }) if (!EnTorre(x, z, 2, 1.5f)) Planta(deco, x, y2, z, 1.4f);

            // ---- Iter 12: celdas: camastro, inodoro y luz de emergencia roja ----
            foreach (float c in new[] { 299.3f, 303.9f, 308.5f, 313.1f })
            {
                Caja(deco, "Camastro", c - 1.7f, c - 0.5f, y2 + 0.35f, y2 + 0.5f, 283.2f, 285.3f, MRampa, false);
                Caja(deco, "Camastro_Manta", c - 1.65f, c - 0.55f, y2 + 0.5f, y2 + 0.58f, 283.3f, 285.0f, MLibroB, false);
                Caja(deco, "Camastro_Patas", c - 1.6f, c - 0.6f, y2, y2 + 0.35f, 283.3f, 285.2f, MBarrote, false);
                Cilindro(deco, "Inodoro", c + 1.4f, y2, y2 + 0.45f, 284.7f, 0.5f, MMarmol);
                Caja(deco, "Cadena_Pared", c + 0.2f, c + 0.28f, y2 + 1.2f, y2 + 2.4f, 285.3f, 285.4f, MBarrote, false);
                LuzPuntual(deco, "Luz_Emergencia", new Vector3(c, y2 + 3.2f, 282.5f), 5.5f, 0.7f, new Color(1f, 0.25f, 0.2f));
            }

            // ---- Iter 13: azotea: helipuerto con balizas y la H, jardineras y reflectores ----
            Caja(deco, "H_Vertical_I", 320.6f, 321.3f, y3 + 0.04f, y3 + 0.07f, 263.8f, 268.2f, MBarrote, false);
            Caja(deco, "H_Vertical_D", 322.7f, 323.4f, y3 + 0.04f, y3 + 0.07f, 263.8f, 268.2f, MBarrote, false);
            Caja(deco, "H_Travesano", 321.3f, 322.7f, y3 + 0.04f, y3 + 0.07f, 265.6f, 266.4f, MBarrote, false);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                float bx = 322f + Mathf.Cos(a) * 5.8f, bz = 266f + Mathf.Sin(a) * 5.8f;
                Caja(deco, "Baliza_Helipuerto", bx - 0.12f, bx + 0.12f, y3, y3 + 0.12f, bz - 0.12f, bz + 0.12f, MCristal, false);
            }
            for (float x = 304f; x < 342f; x += 6f)
            {
                if (EnTorre(x, 248f, 3, 1.5f) || (x > 318f && x < 326f)) continue;
                Caja(deco, "Jardinera_Azotea", x - 1.2f, x + 1.2f, y3, y3 + 0.7f, 247.6f, 248.8f, MMarmolOsc, false);
                Esfera(deco, "Seto_Azotea", new Vector3(x, y3 + 1.0f, 248.2f), 1.3f, MHoja);
            }
            foreach (var (x, z) in new[] { (297.5f, 247.5f), (346.5f, 247.5f), (297.5f, 284.5f), (346.5f, 284.5f) })
                LuzPuntual(deco, "Reflector_Azotea", new Vector3(x, y3 + 2.6f, z), 22f, 1.6f, new Color(1f, 0.95f, 0.85f));

            // ---- Iter 14: azotea: tanque de agua, antena con baliza y tapa dorada en el parapeto ----
            Cilindro(deco, "Tanque", 344f, y3 + 1.6f, y3 + 4.2f, 262f, 3f, MPilar);
            foreach (float dx in new[] { -1.1f, 1.1f })
                foreach (float dz in new[] { -1.1f, 1.1f })
                    Caja(deco, "Tanque_Pata", 344f + dx - 0.1f, 344f + dx + 0.1f, y3, y3 + 1.6f, 262f + dz - 0.1f, 262f + dz + 0.1f, MBarrote, false);
            Caja(deco, "Antena_1", 300.9f, 301.1f, y3, y3 + 7f, 267.9f, 268.1f, MBarrote, false);
            Caja(deco, "Antena_1_Travesano", 300.2f, 301.8f, y3 + 5.5f, y3 + 5.6f, 267.95f, 268.05f, MBarrote, false);
            Esfera(deco, "Antena_Baliza", new Vector3(301f, y3 + 7.1f, 268f), 0.3f, Mat("Mansion_Baliza", new Color(1f, 0.1f, 0.1f), 2f));
            float pt = y3 + G.Parapeto;
            Caja(deco, "Parapeto_Tapa_S", G.X0, G.X1, pt, pt + 0.1f, G.Z0 - 0.1f, G.Z0 + G.Muro + 0.1f, MDorado, false);
            Caja(deco, "Parapeto_Tapa_N", G.X0, G.X1, pt, pt + 0.1f, G.Z1 - G.Muro - 0.1f, G.Z1 + 0.1f, MDorado, false);
            Caja(deco, "Parapeto_Tapa_O", G.X0 - 0.1f, G.X0 + G.Muro + 0.1f, pt, pt + 0.1f, G.Z0, G.Z1, MDorado, false);
            Caja(deco, "Parapeto_Tapa_E", G.X1 - G.Muro - 0.1f, G.X1 + 0.1f, pt, pt + 0.1f, G.Z0, G.Z1, MDorado, false);

            // ---- Iter 15: fachada: pilastras, balcones con balaustrada y denticulos ----
            for (float x = 302.7f; x < 346f; x += 5.4f)
            {
                if (x > 309f && x < 335f) continue;
                Caja(deco, "Pilastra", x - 0.4f, x + 0.4f, 0f, G.Nivel(3), G.Z0 - 0.3f, G.Z0, MMarmol, false);
                Caja(deco, "Pilastra_Base", x - 0.5f, x + 0.5f, 0f, 0.5f, G.Z0 - 0.38f, G.Z0, MMarmolOsc, false);
                Caja(deco, "Pilastra_Capitel", x - 0.5f, x + 0.5f, G.Nivel(3) - 0.6f, G.Nivel(3), G.Z0 - 0.38f, G.Z0, MDorado, false);
            }
            for (int n = 1; n <= 2; n++)
                for (float x = 300f; x < 346f; x += 5.4f)
                {
                    if (n == 1 && x > 311f && x < 333f) continue;
                    float yb = G.Nivel(n);
                    Caja(deco, "Balcon_Losa", x - 1.7f, x + 1.7f, yb + 0.55f, yb + 0.75f, G.Z0 - 1.0f, G.Z0, MMarmol, false);
                    Caja(deco, "Balcon_Baranda", x - 1.7f, x + 1.7f, yb + 1.55f, yb + 1.65f, G.Z0 - 1.0f, G.Z0 - 0.9f, MDorado, false);
                    for (float b = -1.6f; b <= 1.65f; b += 0.4f) Caja(deco, "Balcon_Baluste", x + b - 0.06f, x + b + 0.06f, yb + 0.75f, yb + 1.55f, G.Z0 - 0.98f, G.Z0 - 0.92f, MMarmol, false);
                }
            for (float x = G.X0 + 0.3f; x < G.X1 - 0.5f; x += 0.8f)
            {
                Caja(deco, "Denticulo_S", x, x + 0.35f, G.Nivel(3) - 0.75f, G.Nivel(3) - 0.3f, G.Z0 - 0.3f, G.Z0, MMarmol, false);
                Caja(deco, "Denticulo_N", x, x + 0.35f, G.Nivel(3) - 0.75f, G.Nivel(3) - 0.3f, G.Z1, G.Z1 + 0.3f, MMarmol, false);
            }

            // ---- Iter 16: mastiles con banderas, farolas de hierro y reflectores sobre la fachada ----
            foreach (float x in new[] { 307.5f, 336.5f })
            {
                Cilindro(deco, "Mastil", x, 0.23f, 10f, G.Z0 - 3.5f, 0.14f, MBarrote);
                Caja(deco, "Bandera", x + 0.07f, x + 2.6f, 7.6f, 9.4f, G.Z0 - 3.55f, G.Z0 - 3.45f, MTela, false);
                Caja(deco, "Bandera_Franja", x + 0.07f, x + 2.6f, 8.35f, 8.65f, G.Z0 - 3.56f, G.Z0 - 3.44f, MDorado, false);
                Esfera(deco, "Mastil_Remate", new Vector3(x, 10.1f, G.Z0 - 3.5f), 0.3f, MDorado);
            }
            foreach (var (x, z) in new[] { (304f, 238f), (340f, 238f), (304f, 243f), (340f, 243f) })
            {
                Cilindro(deco, "Farola_Poste", x, 0.23f, 4.6f, z, 0.16f, MBarrote);
                Caja(deco, "Farola_Farol", x - 0.3f, x + 0.3f, 4.6f, 5.2f, z - 0.3f, z + 0.3f, MCristal, false);
                Caja(deco, "Farola_Tapa", x - 0.4f, x + 0.4f, 5.2f, 5.35f, z - 0.4f, z + 0.4f, MBarrote, false);
                LuzPuntual(deco, "Luz_Farola", new Vector3(x, 4.9f, z), 16f, 1.8f, new Color(1f, 0.88f, 0.64f));
            }
            foreach (float x in new[] { 301f, 317f, 327f, 343f }) LuzPuntual(deco, "Reflector_Fachada", new Vector3(x, 1.2f, G.Z0 - 4f), 20f, 2.0f, new Color(1f, 0.93f, 0.78f));

            // ---- Iter 17: jardin frontal: setos, topiarios y leones junto a la escalinata ----
            foreach (var (x0, x1) in new[] { (298f, 308f), (336f, 346f) })
            {
                Caja(deco, "Seto_Frontal", x0, x1, 0.23f, 1.2f, 241.2f, 242.4f, MHoja, false);
                Caja(deco, "Seto_Frontal_Borde", x0 - 0.1f, x1 + 0.1f, 0.23f, 0.35f, 240.9f, 242.7f, MMarmolOsc, false);
            }
            foreach (var (x, z) in new[] { (300f, 239f), (306f, 239f), (338f, 239f), (344f, 239f) })
            {
                Cilindro(deco, "Topiario_Maceta", x, 0.23f, 0.9f, z, 0.9f, MMarmolOsc);
                Esfera(deco, "Topiario", new Vector3(x, 1.5f, z), 1.2f, MHoja);
                Esfera(deco, "Topiario_Copa", new Vector3(x, 2.3f, z), 0.7f, MHoja);
            }
            foreach (float x in new[] { 309.3f, 334.7f })
            {
                Caja(deco, "Leon_Pedestal", x - 0.7f, x + 0.7f, 0.23f, 1.3f, 240.3f, 241.7f, MMarmol, false);
                Caja(deco, "Leon_Cuerpo", x - 0.45f, x + 0.45f, 1.3f, 2.1f, 240.4f, 241.6f, MDorado, false);
                Esfera(deco, "Leon_Cabeza", new Vector3(x, 2.4f, 240.5f), 0.7f, MDorado);
            }
        }
    }
}
