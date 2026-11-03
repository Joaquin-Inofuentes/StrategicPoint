using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SP.Operacion;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    public static partial class OperacionBuilder
    {
        // Pasadas 81-100: fachada y entorno de la mansion (norte = +z; la fachada principal mira al sur, z = Z0).
        static void PrestigioMansion(StringBuilderLite res, Transform t)
        {
            float z0 = G.Z0, top = G.Nivel(3) + G.Parapeto;
            // 81: faroles de gala flanqueando el porton.
            foreach (float x in new[] { 316.2f, 327.8f }) { Caja(t, "Farol_Gala", x - 0.3f, x + 0.3f, 3.6f, 4.3f, z0 - 0.7f, z0 - 0.1f, MCristal, false); Caja(t, "Farol_Gala_Marco", x - 0.4f, x + 0.4f, 4.3f, 4.5f, z0 - 0.8f, z0, MDorado, false); LuzPuntual(t, "Luz_Farol_Gala", new Vector3(x, 3.9f, z0 - 1.2f), 10f, 2.0f, new Color(1f, 0.85f, 0.55f)); }
            // 82: lamparas colgantes entre las columnas del portico.
            foreach (float x in new[] { 315.75f, 322f, 328.25f })
            { Caja(t, "Portico_Cadena", x - 0.03f, x + 0.03f, 6.6f, 8.4f, z0 - 1.6f - 0.03f, z0 - 1.6f + 0.03f, MDorado, false); Esfera(t, "Portico_Lampara", new Vector3(x, 6.3f, z0 - 1.6f), 0.9f, MCristal); LuzPuntual(t, "Luz_Portico", new Vector3(x, 6.3f, z0 - 1.9f), 12f, 2.2f, new Color(1f, 0.86f, 0.6f)); }
            // 83: fila de focos de piso que lavan la fachada con luz calida.
            for (float x = 300f; x <= 344f; x += 5.5f) { if (x > 312f && x < 332f) continue; FocoSpot(t, "Foco_Fachada_Piso", new Vector3(x, 0.4f, z0 - 3.2f), new Vector3(x, 12f, z0), 35f, 28f, 7f, new Color(1f, 0.84f, 0.55f)); }
            // 84: corona dorada sobre el frontis.
            Caja(t, "Corona_Base", 319f, 325f, 11.0f, 11.5f, z0 - 1.4f, z0 - 0.4f, MDorado, false); Caja(t, "Corona_Cuerpo", 320f, 324f, 11.5f, 13.0f, z0 - 1.2f, z0 - 0.6f, MDorado, false);
            for (int i = 0; i < 5; i++) Esfera(t, "Corona_Punta", new Vector3(320.2f + i * 0.9f, 13.3f + (i == 2 ? 0.5f : 0f), z0 - 0.9f), 0.45f, MDorado);
            LuzPuntual(t, "Luz_Corona", new Vector3(322f, 12f, z0 - 2.5f), 9f, 2.4f, new Color(1f, 0.85f, 0.45f));
            // 85: urnas en las esquinas del parapeto.
            foreach (var (x, z) in new[] { (G.X0 + 0.6f, z0 + 0.6f), (G.X1 - 0.6f, z0 + 0.6f), (G.X0 + 0.6f, G.Z1 - 0.6f), (G.X1 - 0.6f, G.Z1 - 0.6f) })
            { Cilindro(t, "Urna_Pie", x, top + 0.1f, top + 0.5f, z, 0.5f, MMarmol); Esfera(t, "Urna", new Vector3(x, top + 1.0f, z), 0.9f, MMarmol); Cilindro(t, "Urna_Boca", x, top + 1.4f, top + 1.55f, z, 0.7f, MDorado); }
            // 86: gran araña exterior en el centro del portico.
            for (int i = 0; i < 3; i++) Cilindro(t, "Aranita_Aro", 322f, 8.0f - i * 0.5f, 8.12f - i * 0.5f, z0 - 1.6f, 2.6f - i * 0.7f, MDorado);
            // 87-88: alfombra roja hasta la plaza con postes y cordones.
            Caja(t, "Alfombra_Gala", 320.2f, 323.8f, 0.24f, 0.27f, 232f, 243f, MAlfombra, false);
            for (float z = 233f; z <= 243f; z += 2.5f) foreach (float x in new[] { 319.4f, 324.6f })
            { Cilindro(t, "Poste_Cordon", x, 0.23f, 1.1f, z, 0.14f, MDorado); Esfera(t, "Poste_Cordon_Bola", new Vector3(x, 1.15f, z), 0.22f, MDorado); if (z < 243f) Caja(t, "Cordon", x - 0.03f, x + 0.03f, 0.85f, 0.95f, z, z + 2.5f, MTela, false); }
            // 89: antorchas con llama a los lados de la escalinata.
            foreach (var (x, z) in new[] { (310.2f, 243f), (333.8f, 243f), (304f, 241f), (340f, 241f) })
            {
                Cilindro(t, "Antorcha_Poste", x, 0.23f, 2.6f, z, 0.14f, MBarrote); Cilindro(t, "Antorcha_Copa", x, 2.6f, 2.9f, z, 0.5f, MDorado); Esfera(t, "Antorcha_Llama", new Vector3(x, 3.2f, z), 0.55f, MFlama);
                var g = new GameObject("Luz_Antorcha"); g.transform.SetParent(t, false); g.transform.position = new Vector3(x, 3.4f, z);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 12f; l.intensity = 2.8f; l.color = new Color(1f, 0.58f, 0.22f); l.shadows = LightShadows.None;
                Animar(g, JuegoDeLuces.Modo.Llama, 1.3f, 1f);
            }
            // 90: arcos de flores sobre la alfombra.
            foreach (float z in new[] { 235f, 239f })
            {
                foreach (float x in new[] { 319.2f, 324.8f }) Cilindro(t, "Arco_Flor_Poste", x, 0.23f, 3.4f, z, 0.12f, MBlanco);
                for (int i = 0; i <= 10; i++) { float u = i / 10f, x = Mathf.Lerp(319.2f, 324.8f, u), y = 3.4f + 0.9f * Mathf.Sin(u * Mathf.PI); Esfera(t, "Arco_Flor", new Vector3(x, y, z), 0.4f, i % 2 == 0 ? MHoja : MFlor(i)); }
            }
            // 91: laberinto de setos y jardines laterales (alas oeste y este).
            foreach (float xs in new[] { 288f, 356f })
            {
                for (int i = 0; i < 4; i++) Caja(t, "Seto_Ala", xs - 3.5f, xs + 3.5f, 0.1f, 1.4f, 247f + i * 8f, 248f + i * 8f, MHoja, false);
                Cilindro(t, "Fuentecita_Ala", xs, 0.1f, 0.6f, 268f, 2.6f, MMarmol); Cilindro(t, "Fuentecita_Ala_Agua", xs, 0.6f, 0.66f, 268f, 2.2f, MAguaViva);
                LuzPuntual(t, "Luz_Ala", new Vector3(xs, 1.0f, 268f), 8f, 1.4f, new Color(0.4f, 0.8f, 1f));
            }
            // 92: cupula de vidrio sobre el centro del tejado (claraboya luminosa).
            Esfera(t, "Claraboya", new Vector3(322f, G.Nivel(3) + 0.3f, 252f), 3.2f, MatTrans("Prestigio_Vidrio", new Color(0.7f, 0.9f, 1f, 0.35f), 0.8f, false));
            // 93: emblema solar dorado en el frontis.
            Cilindro(t, "Emblema_Sol", 322f, 9.6f, 9.7f, z0 - 2.25f, 1.6f, MDorado); for (int i = 0; i < 12; i++) { var r = Caja(t, "Emblema_Rayo", -0.06f, 0.06f, 9.6f, 9.7f, 0.9f, 1.6f, MDorado, false); r.transform.position = new Vector3(322f, 9.65f, z0 - 2.25f); r.transform.rotation = Quaternion.Euler(90f, i * 30f, 0f) * Quaternion.Euler(-90f, 0f, 0f); }
            // 94: parpadeo suave de las luces de las ventanas de planta alta (vida dentro de la casa).
            for (int n = 1; n <= 2; n++) for (float x = 304f; x < 345f; x += 10.8f)
            { var g = new GameObject("Luz_Ventana"); g.transform.SetParent(t, false); g.transform.position = new Vector3(x, G.Nivel(n) + 3.4f, z0 - 1.2f); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 8f; l.intensity = 1.0f; l.color = new Color(1f, 0.82f, 0.5f); l.shadows = LightShadows.None; Animar(g, JuegoDeLuces.Modo.Pulso, 0.35f, 0.25f); }
            // 95: lavado de color que cicla sobre la cornisa superior (juego de luces de la fachada).
            for (float x = 302f; x <= 342f; x += 10f)
            { var l = FocoSpot(t, "Foco_Color_Cornisa", new Vector3(x, top + 0.5f, z0 + 0.1f), new Vector3(x, G.Nivel(2) + 1f, z0 - 3f), 60f, 18f, 5f, Color.white); Animar(l.gameObject, JuegoDeLuces.Modo.Ciclo, 0.8f, 1f); l.GetComponent<JuegoDeLuces>().fase = x * 0.05f; }
            // 96: mastil de gala en el centro del tejado con bandera grande y baliza.
            Cilindro(t, "Mastil_Gala", 322f, G.Nivel(3), G.Nivel(3) + 11f, 262f, 0.2f, MBarrote); Caja(t, "Bandera_Gala", 322.1f, 326.6f, G.Nivel(3) + 7.6f, G.Nivel(3) + 10.6f, 261.95f, 262.05f, MTela, false); Caja(t, "Bandera_Gala_Franja", 322.1f, 326.6f, G.Nivel(3) + 8.7f, G.Nivel(3) + 9.4f, 261.93f, 262.07f, MDorado, false);
            Esfera(t, "Mastil_Gala_Remate", new Vector3(322f, G.Nivel(3) + 11.2f, 262f), 0.5f, MDorado);
            { var g = new GameObject("Luz_Mastil"); g.transform.SetParent(t, false); g.transform.position = new Vector3(322f, G.Nivel(3) + 11.6f, 262f); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 14f; l.intensity = 2.5f; l.color = new Color(1f, 0.15f, 0.1f); l.shadows = LightShadows.None; Animar(g, JuegoDeLuces.Modo.Parpadeo, 1f, 1f); }
            // 97: dos reflectores de busqueda en el tejado girando sobre la ciudad.
            Reflector(t, "Reflector_Techo_O", new Vector3(G.X0 + 2f, G.Nivel(3) + 1.5f, G.Z1 - 2f), 0f, 75f, new Color(0.85f, 0.92f, 1f), JuegoDeLuces.Modo.Giro, 1.2f);
            Reflector(t, "Reflector_Techo_E", new Vector3(G.X1 - 2f, G.Nivel(3) + 1.5f, G.Z1 - 2f), 90f, 75f, new Color(1f, 0.88f, 0.65f), JuegoDeLuces.Modo.Giro, -1.0f);
            // 98: aureola luminosa suave alrededor del tejado (halo de gala).
            Esfera(t, "Halo_Mansion", new Vector3(322f, G.Nivel(3) + 4f, 266f), 70f, MatTrans("Prestigio_HaloMansion", new Color(1f, 0.85f, 0.5f, 0.05f), 0.6f, true));
            // 99: escudo dorado sobre el porton con luz propia.
            Caja(t, "Escudo_Fondo", 320.5f, 323.5f, 5.5f, 7.4f, z0 - 0.25f, z0 - 0.05f, MMarmolOsc, false); Caja(t, "Escudo_Marco", 320.3f, 323.7f, 5.3f, 7.6f, z0 - 0.15f, z0 - 0.02f, MDorado, false); Esfera(t, "Escudo_Gema", new Vector3(322f, 6.45f, z0 - 0.35f), 0.7f, MNeonRosa);
            LuzPuntual(t, "Luz_Escudo", new Vector3(322f, 6.5f, z0 - 1.4f), 7f, 1.6f, new Color(1f, 0.5f, 0.75f));
            // 100: fanal de proa: luz blanca potente que corona toda la composicion.
            FocoSpot(t, "Foco_Corona_Final", new Vector3(322f, 0.6f, 236f), new Vector3(322f, 14f, 246f), 55f, 40f, 9f, new Color(1f, 0.92f, 0.75f));
            res.Add("81-100 mansion: faroles de gala, lamparas del portico, lavado de fachada, corona, urnas, alfombra con cordones, antorchas, arcos de flores, alas ajardinadas, emblema, color ciclante, mastil, reflectores giratorios, halo");
        }
    }
}
