using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using SP.Operacion;

namespace SP.EditorTools
{
    public static partial class OperacionBuilder
    {
        // Pasadas 41-60: la plaza civica (centro 322, 228; fuente al sur en 322, 215.5).
        static void PrestigioPlaza(StringBuilderLite res, Transform t)
        {
            const float cx = 322f, cz = 228f, fz = 215.5f;
            // 41: aros de luz empotrados alrededor del roseton central.
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f, x = cx + Mathf.Cos(a) * 4.2f, z = cz + Mathf.Sin(a) * 4.2f;
                Cilindro(t, "Aro_Luz", x, 0.3f, 0.34f, z, 0.45f, MBombilla);
            }
            LuzPuntual(t, "Luz_Roseton", new Vector3(cx, 1.0f, cz), 12f, 1.2f, new Color(1f, 0.85f, 0.55f));
            // 42: segunda taza de la fuente, chorros y corona dorada.
            Cilindro(t, "Fuente_Taza_Alta", cx, 3.0f, 3.25f, fz, 3.2f, MMarmol);
            Cilindro(t, "Fuente_Agua_Alta", cx, 3.25f, 3.3f, fz, 2.8f, MAguaViva);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f, x = cx + Mathf.Cos(a) * 2.0f, z = fz + Mathf.Sin(a) * 2.0f;
                Cilindro(t, "Fuente_Chorro", x, 0.9f, 2.0f, z, 0.1f, MAguaViva);
                Esfera(t, "Fuente_Gota", new Vector3(x, 2.05f, z), 0.22f, MAguaViva);
            }
            Esfera(t, "Fuente_Corona", new Vector3(cx, 4.0f, fz), 0.9f, MDorado);
            // 43-44: luz bajo el agua que pulsa y seis luces de colores que ciclan alrededor (juego de luces de la fuente).
            var lu = new GameObject("Luz_Agua"); lu.transform.SetParent(t, false); lu.transform.position = new Vector3(cx, 1.1f, fz);
            var lz = lu.AddComponent<Light>(); lz.type = LightType.Point; lz.range = 14f; lz.intensity = 2.6f; lz.color = new Color(0.35f, 0.75f, 1f); lz.shadows = LightShadows.None;
            Animar(lu, JuegoDeLuces.Modo.Pulso, 1.1f, 0.35f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f, x = cx + Mathf.Cos(a) * 5.2f, z = fz + Mathf.Sin(a) * 5.2f;
                var g = new GameObject("Luz_Fuente_Color"); g.transform.SetParent(t, false); g.transform.position = new Vector3(x, 0.8f, z);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 2.2f; l.shadows = LightShadows.None; l.color = Color.HSVToRGB(i / 6f, 0.75f, 1f);
                var j = Animar(g, JuegoDeLuces.Modo.Ciclo, 1f, 1f); j.fase = i * 1.05f;
                Cilindro(t, "Foco_Fuente", x, 0.23f, 0.5f, z, 0.35f, MBarrote);
            }
            // 45: glorietas (pergolas) a los costados con guirnaldas.
            foreach (float xs in new[] { cx - 14f, cx + 14f })
            {
                foreach (float zz in new[] { cz - 4f, cz + 4f }) { Caja(t, "Glorieta_Poste", xs - 0.25f, xs + 0.25f, 0.23f, 4.2f, zz - 0.25f, zz + 0.25f, MMarmol, false); Caja(t, "Glorieta_Capitel", xs - 0.4f, xs + 0.4f, 4.0f, 4.3f, zz - 0.4f, zz + 0.4f, MDorado, false); }
                for (int i = 0; i < 6; i++) Caja(t, "Glorieta_Viga", xs - 0.9f, xs + 0.9f, 4.3f, 4.45f, cz - 4.2f + i * 1.68f, cz - 3.9f + i * 1.68f, MMueble, false);
                for (int i = 0; i < 10; i++) Esfera(t, "Glorieta_Bombilla", new Vector3(xs + (i % 2 == 0 ? -0.7f : 0.7f), 4.15f, cz - 4f + i * 0.9f), 0.2f, MBombilla);
                LuzPuntual(t, "Luz_Glorieta", new Vector3(xs, 3.8f, cz), 10f, 1.6f, new Color(1f, 0.82f, 0.52f));
            }
            // 46: anillo de ocho farolas ornamentales alrededor de la plaza.
            int k = 0;
            foreach (var (x, z, s) in new[] { (cx - 15.5f, cz - 15.5f, 1), (cx + 15.5f, cz - 15.5f, 1), (cx - 15.5f, cz + 15.5f, -1), (cx + 15.5f, cz + 15.5f, -1), (cx, cz - 15.8f, 1), (cx - 15.8f, cz, 1), (cx + 15.8f, cz, 1), (cx, cz + 15.8f, -1) })
                FarolaPrestigio(t, x, z, s, true, false, false, k++);
            // 47: canteros florales en el borde de las jardineras y cerca de los bancos.
            foreach (var (jx, jz) in new[] { (cx - 12.5f, cz - 12.5f), (cx + 12.5f, cz - 12.5f), (cx - 12.5f, cz + 12.5f), (cx + 12.5f, cz + 12.5f) })
                for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Esfera(t, "Flor_Jardinera", new Vector3(jx + Mathf.Cos(a) * 1.9f, 0.55f, jz + Mathf.Sin(a) * 1.9f), 0.34f, MFlor(i + (int)jx)); }
            // 48: lineas doradas incrustadas que irradian del roseton en cruz y en diagonal.
            Caja(t, "Incrustacion_NS", cx - 0.08f, cx + 0.08f, 0.23f, 0.26f, cz - 16f, cz + 16f, MDorado, false);
            Caja(t, "Incrustacion_EO", cx - 16f, cx + 16f, 0.23f, 0.26f, cz - 0.08f, cz + 0.08f, MDorado, false);
            for (int i = 0; i < 4; i++) { float a = 45f + i * 90f; var d = Caja(t, "Incrustacion_Diag", -0.08f, 0.08f, 0.23f, 0.26f, -11f, 11f, MDorado, false); d.transform.position = new Vector3(cx, 0.245f, cz); d.transform.rotation = Quaternion.Euler(0f, a, 0f); }
            // 49: figura dorada sobre la fuente.
            Cilindro(t, "Fuente_Figura", cx, 4.4f, 5.4f, fz, 0.5f, MDorado); Esfera(t, "Fuente_Figura_Cabeza", new Vector3(cx, 5.65f, fz), 0.5f, MDorado);
            // 50: mastiles con banderines alrededor de la plaza.
            foreach (var (x, z) in new[] { (cx - 15.8f, cz - 8f), (cx - 15.8f, cz + 8f), (cx + 15.8f, cz - 8f), (cx + 15.8f, cz + 8f) })
            { Cilindro(t, "Mastil_Plaza", x, 0.23f, 7f, z, 0.12f, MBarrote); Caja(t, "Banderin", x + 0.06f, x + 1.8f, 5.3f, 6.7f, z - 0.03f, z + 0.03f, MTela, false); Caja(t, "Banderin_Franja", x + 0.06f, x + 1.8f, 5.85f, 6.1f, z - 0.04f, z + 0.04f, MDorado, false); }
            // 51: reflectores dorados desde la plaza hacia la fachada de la mansion.
            foreach (float x in new[] { cx - 9f, cx - 3f, cx + 3f, cx + 9f }) FocoSpot(t, "Foco_Fachada", new Vector3(x, 0.5f, 241f), new Vector3(x, 12f, 246f), 40f, 30f, 6f, new Color(1f, 0.82f, 0.5f));
            // 52: tiras LED perimetrales (calidas al norte, cian al sur).
            Caja(t, "Led_Plaza_N", cx - 16f, cx + 16f, 0.23f, 0.3f, cz + 15.9f, cz + 16f, MBombilla, false);
            Caja(t, "Led_Plaza_S", cx - 16f, cx + 16f, 0.23f, 0.3f, cz - 16f, cz - 15.9f, MNeonCian, false);
            Caja(t, "Led_Plaza_O", cx - 16f, cx - 15.9f, 0.23f, 0.3f, cz - 16f, cz + 16f, MNeonCian, false);
            Caja(t, "Led_Plaza_E", cx + 15.9f, cx + 16f, 0.23f, 0.3f, cz - 16f, cz + 16f, MNeonCian, false);
            // 53: luces de hadas en la copa de los arboles de la plaza.
            var rnd = new System.Random(9);
            foreach (var (ax, az) in new[] { (cx - 15f, cz - 6f), (cx - 15f, cz + 6f), (cx + 15f, cz - 6f), (cx + 15f, cz + 6f), (cx - 6f, cz - 15f), (cx + 6f, cz - 15f) })
                for (int i = 0; i < 14; i++) Esfera(t, "Luz_Hada", new Vector3(ax + (float)(rnd.NextDouble() - 0.5) * 3.2f, 3.0f + (float)rnd.NextDouble() * 2.2f, az + (float)(rnd.NextDouble() - 0.5) * 3.2f), 0.14f, MBombilla);
            // 54: pabellon circular en la esquina sureste.
            { float px = cx + 12f, pz = cz - 5f; for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Cilindro(t, "Pabellon_Columna", px + Mathf.Cos(a) * 2.6f, 0.23f, 3.6f, pz + Mathf.Sin(a) * 2.6f, 0.3f, MMarmol); }
              Cilindro(t, "Pabellon_Piso", px, 0.23f, 0.4f, pz, 6.4f, MMarmol); Cilindro(t, "Pabellon_Techo", px, 3.6f, 3.9f, pz, 6.8f, MDorado); Esfera(t, "Pabellon_Cupula", new Vector3(px, 4.0f, pz), 3.6f, MPilar); Esfera(t, "Pabellon_Remate", new Vector3(px, 5.9f, pz), 0.4f, MDorado);
              LuzPuntual(t, "Luz_Pabellon", new Vector3(px, 3.0f, pz), 9f, 1.5f, new Color(1f, 0.85f, 0.55f)); }
            // 55: macetones con plantas a lo largo de la escalinata.
            for (int i = 0; i < 6; i++) { float x = 311f + i * 4.4f; Cilindro(t, "Macetero", x, 0.23f, 0.9f, 244.2f, 0.9f, MMarmolOsc); Esfera(t, "Macetero_Planta", new Vector3(x, 1.4f, 244.2f), 1.0f, MHoja); }
            // 56: dos reflectores de busqueda en la plaza que se cruzan sobre la mansion.
            Reflector(t, "Reflector_Plaza_O", new Vector3(cx - 15f, 0.6f, cz + 15f), 20f, 70f, new Color(0.8f, 0.9f, 1f), JuegoDeLuces.Modo.Barrido, 0.8f);
            Reflector(t, "Reflector_Plaza_E", new Vector3(cx + 15f, 0.6f, cz + 15f), -20f, 70f, new Color(1f, 0.85f, 0.6f), JuegoDeLuces.Modo.Barrido, 0.7f);
            // 57: farolillos flotantes sobre la plaza (suben y bajan lentamente).
            for (int i = 0; i < 12; i++)
            {
                float x = cx - 12f + (float)rnd.NextDouble() * 24f, z = cz - 12f + (float)rnd.NextDouble() * 24f, y = 7f + (float)rnd.NextDouble() * 5f;
                var g = new GameObject("Farolillo"); g.transform.SetParent(t, false); g.transform.position = new Vector3(x, y, z);
                Esfera(g.transform, "Farolillo_Cuerpo", g.transform.position, 0.8f, MBombilla);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 8f; l.intensity = 1.1f; l.color = new Color(1f, 0.75f, 0.4f); l.shadows = LightShadows.None;
                Animar(g, JuegoDeLuces.Modo.Flotar, 0.5f, 0.6f);
            }
            // 58: braseros con llama en las cuatro esquinas.
            foreach (var (x, z) in new[] { (cx - 15f, cz - 15f), (cx + 15f, cz - 15f), (cx - 15f, cz + 15f), (cx + 15f, cz + 15f) })
            {
                Cilindro(t, "Brasero_Pie", x, 0.23f, 1.0f, z, 0.35f, MBarrote); Cilindro(t, "Brasero_Cuenco", x, 1.0f, 1.35f, z, 0.9f, MBarrote); Esfera(t, "Brasero_Llama", new Vector3(x, 1.6f, z), 0.7f, MFlama);
                var g = new GameObject("Luz_Brasero"); g.transform.SetParent(t, false); g.transform.position = new Vector3(x, 1.8f, z);
                var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 11f; l.intensity = 2.4f; l.color = new Color(1f, 0.55f, 0.2f); l.shadows = LightShadows.None;
                Animar(g, JuegoDeLuces.Modo.Llama, 1f, 1f);
            }
            // 59: neblina de la fuente (esfera translucida).
            Esfera(t, "Neblina_Fuente", new Vector3(cx, 1.6f, fz), 9f, MNeblina);
            // 60: reloj de sol y brujula incrustada en el centro.
            Cilindro(t, "Brujula_Disco", cx, 0.3f, 0.33f, cz, 2.2f, MDorado); Caja(t, "Brujula_N", cx - 0.1f, cx + 0.1f, 0.33f, 0.37f, cz, cz + 1.6f, MMarmolOsc, false); Caja(t, "Brujula_E", cx, cx + 1.6f, 0.33f, 0.37f, cz - 0.1f, cz + 0.1f, MMarmolOsc, false);
            res.Add("41-60 plaza: aros de luz, fuente en dos tazas con luces de colores que ciclan, glorietas, farolas, incrustaciones doradas, pabellon, reflectores, farolillos, braseros, neblina");
        }
    }
}
