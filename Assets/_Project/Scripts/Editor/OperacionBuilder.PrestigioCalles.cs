using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SP.Operacion;

namespace SP.EditorTools
{
    public static partial class OperacionBuilder
    {
        // Farola ornamental: poste, brazo, farol de cristal y luz (opcionalmente con maceta colgante y estandarte).
        static void FarolaPrestigio(Transform p, float x, float z, int sentido, bool doble, bool flores, bool estandarte, int k)
        {
            Cilindro(p, "Farola_Base", x, 0.12f, 0.9f, z, 0.5f, MMarmolOsc);
            Cilindro(p, "Farola_Poste", x, 0.9f, 6.2f, z, 0.17f, MBarrote);
            Caja(p, "Farola_Anillo", x - 0.2f, x + 0.2f, 1.6f, 1.75f, z - 0.2f, z + 0.2f, MDorado, false);
            Caja(p, "Farola_Brazo", x - 0.06f, x + 0.06f + sentido * 0.9f * 0f, 6.1f, 6.22f, z - 0.06f, z + 0.06f + sentido * 0.9f, MBarrote, false);
            Caja(p, "Farola_Farol", x - 0.32f, x + 0.32f, 5.5f, 6.1f, z - 0.32f + sentido * 0.9f, z + 0.32f + sentido * 0.9f, MCristal, false);
            Caja(p, "Farola_Tapa", x - 0.42f, x + 0.42f, 6.1f, 6.28f, z - 0.42f + sentido * 0.9f, z + 0.42f + sentido * 0.9f, MDorado, false);
            Esfera(p, "Farola_Remate", new Vector3(x, 6.45f, z + sentido * 0.9f), 0.28f, MDorado);
            LuzPuntual(p, "Luz_Farola", new Vector3(x, 5.8f, z + sentido * 1.2f), 15f, 2.0f, new Color(1f, 0.85f, 0.58f));
            if (doble)
            {
                Caja(p, "Farola_Brazo_2", x - 0.06f, x + 0.06f, 5.4f, 5.52f, z - 0.9f * 0f - 0.06f - sentido * 0.9f, z + 0.06f, MBarrote, false);
                Caja(p, "Farola_Farol_2", x - 0.28f, x + 0.28f, 4.9f, 5.4f, z - 0.28f - sentido * 0.9f, z + 0.28f - sentido * 0.9f, MCristal, false);
                LuzPuntual(p, "Luz_Farola_2", new Vector3(x, 5.1f, z - sentido * 1.1f), 11f, 1.4f, new Color(1f, 0.85f, 0.58f));
            }
            if (flores)
            {
                Cilindro(p, "Colgante_Maceta", x + 0.5f, 3.2f, 3.5f, z, 0.55f, MMarmolOsc);
                for (int i = 0; i < 4; i++) Esfera(p, "Colgante_Flor", new Vector3(x + 0.5f + (i % 2 - 0.5f) * 0.3f, 3.65f, z + (i / 2 - 0.5f) * 0.3f), 0.3f, MFlor(k + i));
                Caja(p, "Colgante_Brazo", x, x + 0.5f, 3.2f, 3.26f, z - 0.03f, z + 0.03f, MBarrote, false);
            }
            if (estandarte)
            {
                Caja(p, "Estandarte", x - 0.55f, x - 0.15f, 3.0f, 5.0f, z - 0.02f, z + 0.02f, MTela, false);
                Caja(p, "Estandarte_Franja", x - 0.55f, x - 0.15f, 4.5f, 4.65f, z - 0.03f, z + 0.03f, MDorado, false);
                Caja(p, "Estandarte_Barra", x - 0.6f, x - 0.1f, 5.0f, 5.06f, z - 0.03f, z + 0.03f, MDorado, false);
            }
        }

        static void Bolardo(Transform p, float x, float z)
        {
            Cilindro(p, "Bolardo", x, 0.12f, 0.75f, z, 0.22f, MBarrote);
            Esfera(p, "Bolardo_Luz", new Vector3(x, 0.8f, z), 0.2f, MBombilla);
        }

        static void Banco(Transform p, float x, float z, bool eje)
        {
            float a = eje ? 1.1f : 0.35f, b = eje ? 0.35f : 1.1f;
            Caja(p, "Banco_Asiento", x - a, x + a, 0.45f, 0.55f, z - b, z + b, MMueble, false);
            Caja(p, "Banco_Patas", x - a + 0.1f, x + a - 0.1f, 0.12f, 0.45f, z - b + 0.1f, z + b - 0.1f, MBarrote, false);
            if (eje) Caja(p, "Banco_Respaldo", x - a, x + a, 0.55f, 1.0f, z + b - 0.06f, z + b, MMueble, false);
            else Caja(p, "Banco_Respaldo", x + a - 0.06f, x + a, 0.55f, 1.0f, z - b, z + b, MMueble, false);
        }

        static void Cebra(Transform p, float x, float z, bool horizontal)
        {
            for (int i = 0; i < 9; i++)
            {
                float o = -4f + i * 1.0f;
                if (horizontal) Caja(p, "Cebra", x + o - 0.28f, x + o + 0.28f, 0.115f, 0.135f, z - 1.6f, z + 1.6f, MBlanco, false);
                else Caja(p, "Cebra", x - 1.6f, x + 1.6f, 0.115f, 0.135f, z + o - 0.28f, z + o + 0.28f, MBlanco, false);
            }
        }

        // Pasadas 13-40.
        static void PrestigioCalles(StringBuilderLite res, Transform t)
        {
            int k = 0;
            // 13-14: farolas ornamentales en las dos veredas de la avenida sur; 15-16: sobre la calle este-oeste (norte y sur).
            for (float z = 176f; z <= 218f; z += 10f) { FarolaPrestigio(t, 304.2f, z, 1, false, k % 2 == 0, k % 3 == 0, k); k++; FarolaPrestigio(t, 339.8f, z, -1, false, k % 2 == 1, k % 3 == 1, k); k++; }
            foreach (float x in new[] { 276f, 284f, 292f, 352f, 360f, 368f })
            {
                FarolaPrestigio(t, x, 222.4f, 1, false, k % 2 == 0, false, k); k++;
                FarolaPrestigio(t, x, 235.6f, -1, false, k % 2 == 1, k % 3 == 0, k); k++;
            }
            // 17: farolas dobles doradas junto al borde de la plaza.
            foreach (var (x, z) in new[] { (305f, 213f), (339f, 213f), (305f, 243f), (339f, 243f) }) FarolaPrestigio(t, x, z, z > 228f ? -1 : 1, true, true, false, k++);
            // 18: reflectores de piso al pie de cada arbol de vereda (luz de acento verde-calida).
            foreach (var m in Manzanas)
            {
                bool oeste = (m.x0 + m.x1) * 0.5f < 322f;
                for (float z = m.z0 + 2f; z < m.z1; z += 9f)
                {
                    float x = oeste ? m.x0 - 2.5f : m.x1 + 2.5f;
                    LuzPuntual(t, "Luz_Arbol", new Vector3(x, 0.5f, z), 6f, 0.9f, new Color(0.55f, 1f, 0.55f));
                }
            }
            // 19-20: bolardos luminosos a lo largo de las veredas de la avenida y de la plaza.
            for (float z = 174f; z <= 220f; z += 4f) { Bolardo(t, 306.2f, z); Bolardo(t, 337.8f, z); }
            for (float x = 307f; x <= 337f; x += 5f) { Bolardo(t, x, 211.2f); }
            // 21-22: bancos y cestos sobre las veredas de la avenida.
            for (float z = 182f; z <= 214f; z += 16f) { Banco(t, 307.6f, z, false); Banco(t, 336.4f, z, false); }
            foreach (float z in new[] { 190f, 206f }) { Caja(t, "Cesto", 308.4f, 308.9f, 0.1f, 0.95f, z - 0.25f, z + 0.25f, MBarrote, false); Caja(t, "Cesto", 335.1f, 335.6f, 0.1f, 0.95f, z - 0.25f, z + 0.25f, MBarrote, false); }
            // 23: pasos de cebra en la avenida (sur de la plaza) y en la calle este-oeste.
            Cebra(t, 322f, 210f, true); Cebra(t, 322f, 178f, true); Cebra(t, 297f, 229f, false); Cebra(t, 347f, 229f, false);
            // 24: lineas de pare.
            Caja(t, "Pare_Avenida", 308f, 322f, 0.115f, 0.135f, 207f, 207.5f, MBlanco, false); Caja(t, "Pare_Avenida", 322f, 336f, 0.115f, 0.135f, 213f, 213.5f, MBlanco, false);
            // 25: tira LED calida en el cordon de la avenida.
            Caja(t, "Cordon_Led_O", 305.4f, 305.55f, 0.1f, 0.18f, 172f, 223f, MBombilla, false);
            Caja(t, "Cordon_Led_E", 338.45f, 338.6f, 0.1f, 0.18f, 172f, 223f, MBombilla, false);
            // 26: alcantarillas.
            foreach (var (x, z) in new[] { (314f, 196f), (330f, 186f), (326f, 205f), (284f, 229f), (360f, 228f) }) Cilindro(t, "Alcantarilla", x, 0.105f, 0.13f, z, 1.1f, MBarrote);
            // 27: flechas blancas pintadas en el asfalto.
            foreach (float z in new[] { 184f, 198f }) { Caja(t, "Flecha_Cuerpo", 315.5f, 316.1f, 0.115f, 0.135f, z, z + 3.2f, MBlanco, false); Caja(t, "Flecha_Punta", 315.1f, 316.5f, 0.115f, 0.135f, z + 3.2f, z + 3.7f, MBlanco, false); }
            // 28: guirnaldas de bombillas cruzando la avenida (curvas entre farolas).
            foreach (float z in new[] { 180f, 192f, 204f })
            {
                for (int i = 0; i <= 14; i++)
                {
                    float u = i / 14f, x = Mathf.Lerp(304.4f, 339.6f, u), y = 6.0f - 1.4f * Mathf.Sin(u * Mathf.PI);
                    Esfera(t, "Guirnalda_Bombilla", new Vector3(x, y, z), 0.28f, MBombilla);
                    if (i % 5 == 2) LuzPuntual(t, "Luz_Guirnalda", new Vector3(x, y - 0.2f, z), 6f, 0.8f, new Color(1f, 0.85f, 0.55f));
                }
            }
            // 29: arco monumental de entrada en el extremo sur de la avenida, con cartel dorado luminoso.
            foreach (float x in new[] { 306f, 338f }) { Caja(t, "Arco_Pilar", x - 1.5f, x + 1.5f, 0.1f, 11f, 164f, 167f, MMarmol, false); Caja(t, "Arco_Base", x - 1.9f, x + 1.9f, 0.1f, 1.2f, 163.6f, 167.4f, MMarmolOsc, false); Caja(t, "Arco_Capitel", x - 1.8f, x + 1.8f, 10.4f, 11.2f, 163.7f, 167.3f, MDorado, false); }
            Caja(t, "Arco_Dintel", 304f, 340f, 11f, 13f, 163.8f, 167.2f, MMarmol, false);
            Caja(t, "Arco_Cartel", 311f, 333f, 11.4f, 12.6f, 163.6f, 163.8f, MBombilla, false);
            Caja(t, "Arco_Cornisa", 303.5f, 340.5f, 13f, 13.5f, 163.5f, 167.5f, MDorado, false);
            foreach (float x in new[] { 306f, 338f }) { LuzPuntual(t, "Luz_Arco", new Vector3(x, 8f, 162f), 14f, 2.2f, new Color(1f, 0.85f, 0.55f)); Esfera(t, "Arco_Remate", new Vector3(x, 11.9f, 165.5f), 1.2f, MDorado); }
            // 30: obeliscos gemelos flanqueando el arco.
            foreach (float x in new[] { 300f, 344f }) { Caja(t, "Obelisco_Base", x - 1.2f, x + 1.2f, 0.1f, 0.9f, 165f - 1.2f, 165f + 1.2f, MMarmolOsc, false); Caja(t, "Obelisco", x - 0.55f, x + 0.55f, 0.9f, 9f, 164.5f, 165.5f, MMarmol, false); Esfera(t, "Obelisco_Punta", new Vector3(x, 9.2f, 165f), 0.8f, MDorado); LuzPuntual(t, "Luz_Obelisco", new Vector3(x, 1.2f, 167f), 12f, 1.6f, new Color(1f, 0.9f, 0.7f)); }
            // 31: torre del reloj en la esquina de la avenida con la calle este-oeste.
            { float x = 303.5f, z = 221.5f; Caja(t, "Reloj_Torre", x - 0.8f, x + 0.8f, 0.1f, 8f, z - 0.8f, z + 0.8f, MMarmol, false); Cilindro(t, "Reloj_Esfera", x, 6.2f, 6.28f, z - 0.82f, 1.8f, MCristal); Caja(t, "Reloj_Tejado", x - 1.1f, x + 1.1f, 8f, 8.3f, z - 1.1f, z + 1.1f, MDorado, false); Esfera(t, "Reloj_Remate", new Vector3(x, 8.7f, z), 0.7f, MDorado); LuzPuntual(t, "Luz_Reloj", new Vector3(x, 6.2f, z - 1.8f), 9f, 1.4f, new Color(1f, 0.9f, 0.6f)); }
            // 32: cabinas telefonicas.
            foreach (var (x, z) in new[] { (309f, 222f), (335f, 236f) }) { Caja(t, "Cabina", x - 0.5f, x + 0.5f, 0.1f, 2.4f, z - 0.5f, z + 0.5f, MChapa(0), false); Caja(t, "Cabina_Vidrio", x - 0.4f, x + 0.4f, 0.4f, 2.1f, z - 0.52f, z - 0.48f, MCristal, false); }
            // 33: quioscos con letrero luminoso.
            foreach (var (x, z, mn) in new[] { (310f, 236.5f, 0), (334f, 222f, 1) })
            { Caja(t, "Quiosco", x - 1.4f, x + 1.4f, 0.1f, 2.5f, z - 1.0f, z + 1.0f, MChapa(1), false); Caja(t, "Quiosco_Techo", x - 1.7f, x + 1.7f, 2.5f, 2.7f, z - 1.3f, z + 1.3f, MMueble, false); Caja(t, "Quiosco_Letrero", x - 1.0f, x + 1.0f, 2.8f, 3.4f, z - 0.05f, z + 0.05f, mn == 0 ? MNeonRosa : MNeonCian, false); LuzPuntual(t, "Luz_Quiosco", new Vector3(x, 2.2f, z - 1.4f), 7f, 1.2f, mn == 0 ? new Color(1f, 0.4f, 0.7f) : new Color(0.4f, 0.9f, 1f)); }
            // 34: terrazas con sombrillas de colores sobre la vereda de la calle este-oeste.
            for (int i = 0; i < 6; i++)
            {
                float x = 278f + i * 4.6f, z = 233.2f;
                Cilindro(t, "Terraza_Mesa", x, 0.1f, 0.8f, z, 0.9f, MBlanco); Cilindro(t, "Sombrilla_Mastil", x, 0.8f, 2.8f, z, 0.07f, MBarrote);
                var sc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); sc.name = "Sombrilla"; sc.transform.SetParent(t, false); sc.transform.position = new Vector3(x, 2.9f, z); sc.transform.localScale = new Vector3(2.6f, 0.08f, 2.6f);
                sc.GetComponent<MeshRenderer>().sharedMaterial = MChapa(i % 3); Object.DestroyImmediate(sc.GetComponent<Collider>());
                Silla(t, x - 0.9f, 0.1f, z, 3); Silla(t, x + 0.9f, 0.1f, z, 2);
            }
            // 35: puestos de comida con luces de feria.
            foreach (var (x, z) in new[] { (356f, 233f), (366f, 233f) })
            {
                Caja(t, "Puesto", x - 1.2f, x + 1.2f, 0.1f, 1.1f, z - 0.7f, z + 0.7f, MMueble, false); Caja(t, "Puesto_Toldo", x - 1.5f, x + 1.5f, 2.3f, 2.45f, z - 1.2f, z + 0.9f, MChapa(0), false);
                for (int i = 0; i < 6; i++) Esfera(t, "Puesto_Bombilla", new Vector3(x - 1.2f + i * 0.48f, 2.2f, z - 1.15f), 0.16f, MBombilla);
            }
            // 36: fuentes pequenas en las esquinas de la avenida.
            foreach (var (x, z) in new[] { (308.5f, 175f), (335.5f, 175f) }) { Cilindro(t, "Fuentecita_Cuenco", x, 0.1f, 0.7f, z, 2.4f, MMarmol); Cilindro(t, "Fuentecita_Agua", x, 0.7f, 0.74f, z, 2.0f, MAguaViva); Cilindro(t, "Fuentecita_Chorro", x, 0.7f, 2.0f, z, 0.12f, MAguaViva); LuzPuntual(t, "Luz_Fuentecita", new Vector3(x, 0.9f, z), 7f, 1.3f, new Color(0.4f, 0.8f, 1f)); }
            // 37: estatuas en las cuatro esquinas del cruce.
            foreach (var (x, z) in new[] { (301f, 226f), (343f, 226f), (301f, 233f), (343f, 233f) }) Estatua(t, x, 0.1f, z);
            // 38: canteros de flores a lo largo de las veredas.
            for (float z = 177f; z <= 217f; z += 8f) foreach (float x in new[] { 309.6f, 334.4f }) { Caja(t, "Cantero", x - 0.7f, x + 0.7f, 0.1f, 0.45f, z - 1.6f, z + 1.6f, MMarmolOsc, false); for (int i = 0; i < 5; i++) Esfera(t, "Flor", new Vector3(x + (i % 2 - 0.5f) * 0.6f, 0.6f, z - 1.2f + i * 0.6f), 0.36f, MFlor(i + (int)z)); }
            // 39: reflectores de busqueda barriendo el cielo desde los extremos de la avenida (efecto de gran estreno).
            Reflector(t, "Reflector_SO", new Vector3(300f, 0.6f, 170f), 20f, 62f, new Color(0.7f, 0.85f, 1f), JuegoDeLuces.Modo.Barrido, 0.9f);
            Reflector(t, "Reflector_SE", new Vector3(344f, 0.6f, 170f), -20f, 62f, new Color(1f, 0.85f, 0.6f), JuegoDeLuces.Modo.Barrido, 0.75f);
            // 40: letreros verticales luminosos sobre postes (barrio comercial).
            foreach (var (x, z, mt) in new[] { (276f, 240f, 0), (360f, 240f, 1), (276f, 218f, 1), (360f, 218f, 0) })
            { Caja(t, "Rotulo_Poste", x - 0.08f, x + 0.08f, 0.1f, 8f, z - 0.08f, z + 0.08f, MBarrote, false); Caja(t, "Rotulo_Neon", x - 0.5f, x + 0.5f, 4.2f, 7.8f, z - 0.15f, z + 0.15f, mt == 0 ? MNeonRosa : MNeonCian, false); LuzPuntual(t, "Luz_Rotulo", new Vector3(x, 6f, z - 1f), 9f, 1.2f, mt == 0 ? new Color(1f, 0.4f, 0.7f) : new Color(0.4f, 0.9f, 1f)); }
            res.Add("13-40 calles: farolas, bolardos, bancos, cebras, guirnaldas, arco, obeliscos, reloj, quioscos, terrazas, fuentecitas, reflectores");
        }
    }
}
