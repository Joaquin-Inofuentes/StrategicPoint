using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using SP.Operacion;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // La mansion de la ciudad (ver MansionGeometria): se arma como UN prefab entero (P_Op_8_Mansion) y se instancia en la escena del sector Ciudad.
    public static partial class OperacionBuilder
    {
        const string RutaPrefabMansion = "Assets/_Project/Prefabs/Escena/Elementos/P_Op_8_Mansion.prefab";

        [MenuItem("Strategic Point/Arte/Mansion/Construir la mansion de la ciudad")]
        static void ConstruirMansionMenu() => Debug.Log(ConstruirMansion());

        static Material MMuro => Mat("Mansion_Muro", new Color(0.80f, 0.74f, 0.64f));
        static Material MLosa => Mat("Mansion_Losa", new Color(0.46f, 0.46f, 0.49f));
        static Material MTabique => Mat("Mansion_Tabique", new Color(0.88f, 0.85f, 0.78f));
        static Material MPilar => Mat("Mansion_Pilar", new Color(0.62f, 0.58f, 0.52f));
        static Material MMueble => Mat("Mansion_Mueble", new Color(0.36f, 0.23f, 0.15f));
        static Material MPiso => Mat("Mansion_Piso", new Color(0.30f, 0.29f, 0.31f));
        static Material MBarrote => Mat("Mansion_Barrote", new Color(0.12f, 0.12f, 0.13f));
        static Material MRampa => Mat("Mansion_Rampa", new Color(0.55f, 0.52f, 0.47f));
        static Material MLuz => Mat("Mansion_Luz", new Color(1f, 0.93f, 0.78f), 2.2f);

        // Caja alineada a los ejes en coordenadas de mundo.
        static GameObject Caja(Transform p, string n, float x0, float x1, float y0, float y1, float z0, float z1, Material m, bool colision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(p, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            go.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!colision) { Object.DestroyImmediate(go.GetComponent<BoxCollider>()); go.AddComponent<NavMeshModifier>().ignoreFromBuild = true; }
            return go;
        }

        // Pieza en coordenadas de una torre (u: hacia adentro, v: hacia el fondo).
        static GameObject CajaTorre(Transform p, G.Torre t, string n, float u0, float u1, float v0, float v1, float y0, float y1, Material m, bool colision = true)
        {
            var a = t.Mundo(u0, v0); var b = t.Mundo(u1, v1);
            return Caja(p, n, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x), y0, y1, Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y), m, colision);
        }

        static GameObject Tramo(Transform p, string n, Vector3 a, Vector3 b, float ancho, Material m)
        {
            var dir = b - a;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation((a + b) * 0.5f - rot * Vector3.up * 0.15f, rot);
            go.transform.localScale = new Vector3(ancho, 0.3f, dir.magnitude);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            var r = go.AddComponent<RampaCaminable>();
            r.abajo = new Vector3(a.x, a.y, a.z); r.arriba = new Vector3(b.x, b.y, b.z);
            r.alturaInicial = a.y; r.alturaDelFinal = b.y; r.anchoMedio = G.MedioCarril;
            return go;
        }

        static List<Rect> RestarHuecos(Rect total, List<Rect> huecos)
        {
            var partes = new List<Rect> { total };
            foreach (var h in huecos)
            {
                var nuevas = new List<Rect>();
                foreach (var r in partes)
                {
                    if (!r.Overlaps(h)) { nuevas.Add(r); continue; }
                    void Agregar(float x0, float x1, float z0, float z1) { if (x1 - x0 > 0.01f && z1 - z0 > 0.01f) nuevas.Add(Rect.MinMaxRect(x0, z0, x1, z1)); }
                    Agregar(r.xMin, h.xMin, r.yMin, r.yMax);
                    Agregar(h.xMax, r.xMax, r.yMin, r.yMax);
                    float ix0 = Mathf.Max(r.xMin, h.xMin), ix1 = Mathf.Min(r.xMax, h.xMax);
                    Agregar(ix0, ix1, r.yMin, h.yMin);
                    Agregar(ix0, ix1, h.yMax, r.yMax);
                }
                partes = nuevas;
            }
            return partes;
        }

        // Una torre de escalera: devuelve el hueco que hay que dejar en la losa del piso de arriba.
        static Rect ArmarTorre(Transform p, G.Torre t)
        {
            float y0 = G.Nivel(t.nivelBase), H = G.Piso, yTop = y0 + H;
            float W = G.TorreAncho, F = G.TorreFondo, run = G.TramoLargo;
            const float ini = 0.3f;
            string k = "Torre_" + t.nombre + "_";
            // Pared de la puerta (v 0..0.3) con la puerta del carril A abierta (u 0.3..2.6, 3 m de alto).
            CajaTorre(p, t, k + "Puerta_I", 0f, 0.3f, 0f, ini, y0, yTop, MMuro);
            // Bajo el final del carril B la pared queda 0,9 m mas baja que el piso de arriba: si llegara al piso, el NavMesh dejaria una franja
            // muerta justo donde se sale de la escalera. El hueco se tapa con una pieza solo visual.
            CajaTorre(p, t, k + "Puerta_D1", 2.6f, 3.0f, 0f, ini, y0, yTop, MMuro);
            CajaTorre(p, t, k + "Puerta_D2", 3.0f, W, 0f, ini, y0, yTop - 0.9f, MMuro);
            CajaTorre(p, t, k + "Puerta_D2_Relleno", 3.0f, W, 0f, ini, yTop - 0.9f, yTop - 0.05f, MMuro, false);
            CajaTorre(p, t, k + "Puerta_Dintel", 0.3f, 2.6f, 0f, ini, y0 + 3f, yTop, MMuro);
            if (t.nombre == "E") CajaTorre(p, t, k + "Lado_Exterior", -0.3f, 0f, 0f, F, y0, yTop + 1.1f, MMuro);
            // Pared del lado de adentro: llega al techo en el rellano y funciona de baranda arriba del hueco.
            CajaTorre(p, t, k + "Lado_Interior", W - 0.3f, W, 0f, F, y0, yTop, MMuro);
            CajaTorre(p, t, k + "Baranda_Lado", W - 0.3f, W, 0f, run + ini, yTop, yTop + 1.1f, MMuro);
            // Divisoria entre los dos carriles.
            CajaTorre(p, t, k + "Divisoria", 2.6f, 3.0f, ini, run + ini, y0, yTop + 1.1f, MPilar);
            CajaTorre(p, t, k + "Baranda_Fondo", 3.0f, W - 0.3f, run + ini, run + ini + 0.3f, yTop, yTop + 1.1f, MMuro);
            // Rellano a media altura.
            float yMed = y0 + H * 0.5f;
            CajaTorre(p, t, k + "Rellano", 0f, W, run + ini, F, yMed - G.Losa, yMed, MRampa);
            // Tramos.
            Vector3 P(float cu, float cv, float y) { var w = t.Mundo(cu, cv); return new Vector3(w.x, y, w.y); }
            Tramo(p, k + "Tramo_A", P(G.CarrilA, ini, y0), P(G.CarrilA, run + ini, yMed), 2.3f, MRampa);
            Tramo(p, k + "Tramo_B", P(G.CarrilB, run + ini, yMed), P(G.CarrilB, ini, yTop), 2.3f, MRampa);
            // Luz del hueco de la escalera.
            var luz = new GameObject(k + "Luz"); luz.transform.SetParent(p, false);
            luz.transform.position = P(W * 0.5f, run * 0.5f, y0 + 4.2f);
            var l = luz.AddComponent<Light>(); l.type = LightType.Point; l.range = 9f; l.intensity = 1.1f; l.color = new Color(1f, 0.9f, 0.7f); l.shadows = LightShadows.None;
            var a = t.Mundo(3.0f, ini); var b = t.Mundo(W - 0.3f, run + ini);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static GameObject ArmarMansion()
        {
            var raiz = new GameObject("P_Op_8_Mansion");
            var t = raiz.transform;
            var casco = new GameObject("Casco").transform; casco.SetParent(t, false);
            var pisos = new GameObject("Losas").transform; pisos.SetParent(t, false);
            var escaleras = new GameObject("Escaleras").transform; escaleras.SetParent(t, false);
            var interior = new GameObject("Interior").transform; interior.SetParent(t, false);
            var deco = new GameObject("Detalle").transform; deco.SetParent(t, false);

            float yMax = G.Nivel(3) + G.Parapeto;
            float gx0 = G.PortonX - G.PortonAncho * 0.5f, gx1 = G.PortonX + G.PortonAncho * 0.5f;
            // ---- casco: cuatro muros con el hueco del porton al sur ----
            Caja(casco, "Muro_S_Oeste", G.X0, gx0, 0f, yMax, G.Z0, G.Z0 + G.Muro, MMuro);
            Caja(casco, "Muro_S_Este", gx1, G.X1, 0f, yMax, G.Z0, G.Z0 + G.Muro, MMuro);
            Caja(casco, "Muro_S_Dintel", gx0, gx1, G.PortonAlto, yMax, G.Z0, G.Z0 + G.Muro, MMuro);
            Caja(casco, "Muro_N", G.X0, G.X1, 0f, yMax, G.Z1 - G.Muro, G.Z1, MMuro);
            Caja(casco, "Muro_O", G.X0, G.X0 + G.Muro, 0f, yMax, G.Z0, G.Z1, MMuro);
            Caja(casco, "Muro_E", G.X1 - G.Muro, G.X1, 0f, yMax, G.Z0, G.Z1, MMuro);
            // Porton: dos hojas que se deslizan hacia adentro y a los costados.
            float medio = (gx0 + gx1) * 0.5f;
            foreach (int lado in new[] { -1, 1 })
            {
                float xa = lado < 0 ? gx0 : medio, xb = lado < 0 ? medio : gx1;
                var hoja = Caja(casco, lado < 0 ? "Porton_Hoja_O" : "Porton_Hoja_E", xa, xb, 0f, G.PortonAlto, G.Z0 + 0.05f, G.Z0 + 0.55f, MBarrote);
                hoja.AddComponent<NavMeshModifier>().ignoreFromBuild = true;   // la hoja se abre: la malla del hueco queda caminable
                var pd = hoja.AddComponent<PortonDeMansion>();
                pd.cerrada = hoja.transform.position;
                pd.abierta = hoja.transform.position + new Vector3(lado * (G.PortonAncho * 0.5f + 0.2f), 0f, 1.0f);
            }
            // Pilastras del porton y cornisas de cada piso (solo visuales).
            Caja(deco, "Pilastra_Porton_O", gx0 - 0.9f, gx0, 0f, G.PortonAlto + 1.2f, G.Z0 - 0.35f, G.Z0 + 0.1f, MPilar, false);
            Caja(deco, "Pilastra_Porton_E", gx1, gx1 + 0.9f, 0f, G.PortonAlto + 1.2f, G.Z0 - 0.35f, G.Z0 + 0.1f, MPilar, false);
            Caja(deco, "Frontis_Porton", gx0 - 0.9f, gx1 + 0.9f, G.PortonAlto + 0.2f, G.PortonAlto + 1.4f, G.Z0 - 0.45f, G.Z0 + 0.1f, MPilar, false);
            for (int n = 1; n <= 3; n++)
            {
                float y = G.Nivel(n);
                Caja(deco, "Cornisa_S_" + n, G.X0 - 0.25f, G.X1 + 0.25f, y - 0.25f, y + 0.1f, G.Z0 - 0.25f, G.Z0, MPilar, false);
                Caja(deco, "Cornisa_N_" + n, G.X0 - 0.25f, G.X1 + 0.25f, y - 0.25f, y + 0.1f, G.Z1, G.Z1 + 0.25f, MPilar, false);
                Caja(deco, "Cornisa_O_" + n, G.X0 - 0.25f, G.X0, y - 0.25f, y + 0.1f, G.Z0 - 0.25f, G.Z1 + 0.25f, MPilar, false);
                Caja(deco, "Cornisa_E_" + n, G.X1, G.X1 + 0.25f, y - 0.25f, y + 0.1f, G.Z0 - 0.25f, G.Z1 + 0.25f, MPilar, false);
            }
            // Ventanas encendidas en las fachadas sur y norte, con marco oscuro.
            for (int n = 0; n < 3; n++)
            {
                float yc = G.Nivel(n) + 2.9f;
                foreach (float x in new[] { 305f, 311f, 316f, 328f, 333f, 339f })
                {
                    Caja(deco, "Marco_S", x - 0.9f, x + 0.9f, yc - 1.2f, yc + 1.2f, G.Z0 - 0.12f, G.Z0, MBarrote, false);
                    Caja(deco, "Ventana_S", x - 0.7f, x + 0.7f, yc - 1.0f, yc + 1.0f, G.Z0 - 0.16f, G.Z0 - 0.1f, Ventana, false);
                }
                foreach (float x in new[] { 305f, 311f, 316f, 322f, 328f, 333f, 339f })
                {
                    Caja(deco, "Marco_N", x - 0.9f, x + 0.9f, yc - 1.2f, yc + 1.2f, G.Z1, G.Z1 + 0.12f, MBarrote, false);
                    Caja(deco, "Ventana_N", x - 0.7f, x + 0.7f, yc - 1.0f, yc + 1.0f, G.Z1 + 0.1f, G.Z1 + 0.16f, Ventana, false);
                }
                foreach (float z in new[] { 254f, 260f, 266f })
                {
                    Caja(deco, "Ventana_O", G.X0 - 0.16f, G.X0 - 0.1f, yc - 1.0f, yc + 1.0f, z - 0.7f, z + 0.7f, Ventana, false);
                    Caja(deco, "Ventana_E", G.X1 + 0.1f, G.X1 + 0.16f, yc - 1.0f, yc + 1.0f, z - 0.7f, z + 0.7f, Ventana, false);
                }
            }

            // ---- losas (con el hueco de cada torre que sube a ese piso) ----
            var huecos = new Dictionary<int, List<Rect>> { { 1, new List<Rect>() }, { 2, new List<Rect>() }, { 3, new List<Rect>() } };
            for (int i = 0; i < G.Torres.Length; i++)
            {
                var torre = G.Torres[i];
                var h = ArmarTorre(escaleras, torre);
                huecos[torre.nivelBase + 1].Add(h);
            }
            var total = Rect.MinMaxRect(G.X0 + G.Muro, G.Z0 + G.Muro, G.X1 - G.Muro, G.Z1 - G.Muro);
            for (int n = 1; n <= 3; n++)
            {
                int c = 0;
                foreach (var r in RestarHuecos(total, huecos[n]))
                    Caja(pisos, $"Losa_{n}_{++c}", r.xMin, r.xMax, G.Nivel(n) - G.Losa, G.Nivel(n), r.yMin, r.yMax, n == 3 ? MLosa : MLosa);
            }
            // Piso de la planta baja (visual).
            Caja(deco, "Piso_PB", G.X0 + G.Muro, G.X1 - G.Muro, 0.005f, 0.04f, G.Z0 + G.Muro, G.Z1 - G.Muro, MPiso, false);

            // ---- planta baja: pilares, mostrador, mamparas y muebles de cobertura ----
            foreach (float x in new[] { 308f, 316f, 328f, 336f })
                foreach (float z in new[] { 253f, 266f })
                    Caja(interior, "PB_Pilar", x - 0.5f, x + 0.5f, 0f, G.Piso - G.Losa, z - 0.5f, z + 0.5f, MPilar);
            Caja(interior, "PB_Mostrador", 317.5f, 326.5f, 0f, 1.15f, 258.6f, 259.5f, MMueble);
            Caja(interior, "PB_Mampara_O", 316.8f, 319.4f, 0f, 1.3f, 250.4f, 250.8f, MBarrote);
            Caja(interior, "PB_Mampara_E", 324.6f, 327.2f, 0f, 1.3f, 250.4f, 250.8f, MBarrote);
            foreach (var (x, z, w) in new[] { (311f, 258f, 2.4f), (333f, 258f, 2.4f), (311f, 262f, 2.4f), (333f, 262f, 2.4f), (322f, 268f, 3.2f) })
                Caja(interior, "PB_Sofa", x - w * 0.5f, x + w * 0.5f, 0f, 0.95f, z - 0.5f, z + 0.5f, MMueble);

            // ---- primer piso: pasillo central y seis cuartos (tres al norte, tres al sur) ----
            float y1 = G.Nivel(1), y1t = G.Nivel(2) - G.Losa;
            // Tabique sur (z 256.7..257) con tres puertas; tabique norte (z 263..263.3) con tres puertas.
            void Tabique(string n, float x0, float x1, float z0, float z1, float[] puertas, float yb, float yt)
            {
                float cur = x0; int i = 0;
                var ord = new List<float>(puertas); ord.Sort();
                foreach (float px in ord)
                {
                    Caja(interior, $"{n}_{++i}", cur, px - 1.6f, yb, yt, z0, z1, MTabique);
                    Caja(interior, $"{n}_Dintel_{i}", px - 1.6f, px + 1.6f, yb + 3.2f, yt, z0, z1, MTabique);
                    cur = px + 1.6f;
                }
                Caja(interior, $"{n}_{++i}", cur, x1, yb, yt, z0, z1, MTabique);
            }
            Tabique("P1_TabiqueS", G.X0 + G.Muro, 337.8f, 256.7f, 257.0f, new[] { 309f, 322f, 331f }, y1, y1t);
            Tabique("P1_TabiqueN", 306.2f, G.X1 - G.Muro, 263.0f, 263.3f, new[] { 312f, 324.5f, 337f }, y1, y1t);
            // Medianeras entre cuartos.
            foreach (float x in new[] { 315.5f, 328.5f })
            {
                Caja(interior, "P1_Medianera_S", x - 0.15f, x + 0.15f, y1, y1t, G.Z0 + G.Muro, 256.7f, MTabique);
                Caja(interior, "P1_Medianera_N", x - 0.15f, x + 0.15f, y1, y1t, 263.3f, G.Z1 - G.Muro, MTabique);
            }
            // Cajones de cobertura en el pasillo.
            Caja(interior, "P1_Cajon_1", 313.5f, 316.5f, y1, y1 + 1.1f, 258.6f, 259.6f, MMueble);
            Caja(interior, "P1_Cajon_2", 328.5f, 331.5f, y1, y1 + 1.1f, 260.6f, 261.6f, MMueble);

            // ---- segundo piso: salon abierto, islas de cobertura y las celdas de los milicianos ----
            float y2 = G.Nivel(2), y2t = G.Nivel(3) - G.Losa;
            foreach (var (x, z) in new[] { (312f, 255f), (330f, 255f), (316f, 259f), (328f, 262f), (334f, 266f) })
                Caja(interior, "P2_Isla", x - 1.2f, x + 1.2f, y2, y2 + 1.1f, z - 1.0f, z + 1.0f, MMueble);
            Caja(interior, "P2_Mesa_Central", 319f, 325f, y2, y2 + 1.0f, 256.5f, 258.0f, MMueble);
            // Tres celdas contra el muro norte (x 307.4..318.6, z 267.4..271.4), frente de barrotes.
            Caja(interior, "P2_Celdas_Fondo_Piso", 307.4f, 318.6f, y2 + 0.005f, y2 + 0.05f, 267.4f, G.Z1 - G.Muro, MPiso, false);
            foreach (float x in new[] { 307.4f, 311.1f, 314.9f, 318.6f })
                Caja(interior, "P2_Celda_Division", x - 0.15f, x + 0.15f, y2, y2t, 267.4f, G.Z1 - G.Muro, MTabique);
            for (float x = 307.7f; x < 318.5f; x += 0.45f)
                Caja(interior, "P2_Barrote", x - 0.04f, x + 0.04f, y2, y2 + 3.2f, 267.3f, 267.4f, MBarrote, true);
            Caja(interior, "P2_Celdas_Dintel", 307.4f, 318.6f, y2 + 3.2f, y2t, 267.25f, 267.45f, MTabique);

            // ---- azotea: coberturas sobre el techo, rotulo del helipuerto y luces ----
            float y3 = G.Nivel(3);
            foreach (var (x, z, w, d, h) in new[] { (311f, 255f, 3f, 1.6f, 1.3f), (333f, 256f, 3f, 1.6f, 1.3f), (312f, 266f, 3.4f, 1.8f, 1.4f), (333f, 266f, 3.4f, 1.8f, 1.4f), (322f, 254f, 4f, 1.4f, 1.2f), (322f, 267f, 4f, 1.4f, 1.2f) })
                Caja(interior, "Azotea_Equipo", x - w * 0.5f, x + w * 0.5f, y3, y3 + h, z - d * 0.5f, z + d * 0.5f, MBarrote);
            var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disco.name = "Azotea_Helipuerto";
            Object.DestroyImmediate(disco.GetComponent<Collider>());
            disco.transform.SetParent(deco, false);
            disco.transform.position = new Vector3(322f, y3 + 0.02f, 260f); disco.transform.localScale = new Vector3(9f, 0.01f, 9f);
            disco.GetComponent<MeshRenderer>().sharedMaterial = Mat("Mansion_Helipuerto", new Color(0.85f, 0.75f, 0.2f));
            disco.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            for (int n = 0; n <= 2; n++)
                foreach (var (x, z) in new[] { (309f, 254f), (322f, 254f), (335f, 254f), (309f, 266f), (322f, 266f), (335f, 266f) })
                {
                    var g = new GameObject($"Luz_{n}"); g.transform.SetParent(deco, false);
                    g.transform.position = new Vector3(x, G.Nivel(n) + 4.6f, z);
                    var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 11f; l.intensity = 0.9f; l.color = new Color(1f, 0.92f, 0.78f); l.shadows = LightShadows.None;
                    Caja(deco, "Panel_Luz", x - 0.6f, x + 0.6f, G.Nivel(n) + 4.85f, G.Nivel(n) + 4.95f, z - 0.3f, z + 0.3f, MLuz, false);
                }
            return raiz;
        }

        public static string ConstruirMansion()
        {
            var res = new System.Text.StringBuilder();
            var esc = SceneManager.GetSceneByPath(RutaEscenaDeSector("Ciudad"));
            if (!esc.IsValid() || !esc.isLoaded) return "La escena Ciudad no esta abierta (aisl.sh Completa).";
            // Quita una mansion anterior de la escena.
            foreach (var r in esc.GetRootGameObjects())
                foreach (var m in r.GetComponentsInChildren<Transform>(true)) if (m != null && m.name.StartsWith("P_Op_8_Mansion")) { Object.DestroyImmediate(m.gameObject); break; }

            // Apaga lo que queda dentro de la huella (radio, campanario, farolas, autos): estorba y el NavMesh lo hornearia adentro.
            var huella = new Bounds(new Vector3((G.X0 + G.X1) * 0.5f, 8f, (G.Z0 + G.Z1) * 0.5f), new Vector3(G.X1 - G.X0 + 1f, 30f, G.Z1 - G.Z0 + 1f));
            int apagados = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var e = SceneManager.GetSceneAt(i);
                foreach (var raiz in e.GetRootGameObjects())
                    foreach (var tr in raiz.GetComponentsInChildren<Transform>(true))
                    {
                        string n = tr.name;
                        bool candidato = n.StartsWith("Radio_") || n.StartsWith("Rotulo_RADIO") || n.StartsWith("Campanario") || n.StartsWith("Farola") || n.StartsWith("Auto_Viejo") || n.StartsWith("Lampara_x") || n.StartsWith("Pantalla_x") || n.StartsWith("Op_EquipoAire") || n == "Plaza_Banco";
                        if (!candidato || !tr.gameObject.activeSelf) continue;
                        var rr = tr.GetComponent<Renderer>(); var cc = tr.GetComponent<Collider>();
                        Vector3 c = rr != null ? rr.bounds.center : cc != null ? cc.bounds.center : tr.position;
                        if (!huella.Contains(c)) continue;
                        tr.gameObject.SetActive(false); apagados++;
                        EditorSceneManager.MarkSceneDirty(e);
                    }
            }
            // Segunda pasada: todo lo que tenga malla con el centro adentro (sacos, barricadas, lineas de calle...), por su grupo GRP_ si lo tiene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var e = SceneManager.GetSceneAt(i);
                foreach (var raiz in e.GetRootGameObjects())
                    foreach (var rr in raiz.GetComponentsInChildren<Renderer>(false))
                    {
                        if (rr == null || !rr.gameObject.activeInHierarchy || !huella.Contains(rr.bounds.center)) continue;
                        bool propia = false; Transform grp = null;
                        for (var q = rr.transform; q != null; q = q.parent)
                        {
                            if (q.name.StartsWith("P_Op_8_Mansion")) propia = true;
                            if (grp == null && q.name.StartsWith("GRP_")) grp = q;
                        }
                        if (propia) continue;
                        (grp != null ? grp : rr.transform).gameObject.SetActive(false); apagados++;
                        EditorSceneManager.MarkSceneDirty(e);
                    }
            }
            res.AppendLine("apagados en la huella: " + apagados);

            var scratch = ArmarMansion();
            AsegurarCarpeta(CarpetaElementos);
            var prefab = PrefabUtility.SaveAsPrefabAsset(scratch, RutaPrefabMansion);
            Object.DestroyImmediate(scratch);
            Transform grupo = null; Transform ciudadRaiz = null;
            foreach (var r in esc.GetRootGameObjects()) if (r.name == "Ciudad") ciudadRaiz = r.transform;
            if (ciudadRaiz == null) return "No hay raiz Ciudad en la escena.";
            grupo = ciudadRaiz.Find("8_Mansion");
            if (grupo == null) { var g = new GameObject("8_Mansion"); g.transform.SetParent(ciudadRaiz, false); grupo = g.transform; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, esc);
            inst.transform.SetParent(grupo, false);
            inst.transform.position = Vector3.zero;
            EditorSceneManager.MarkSceneDirty(esc);
            res.AppendLine("mansion instanciada: " + inst.name + " hijos=" + inst.transform.childCount);
            return res.ToString();
        }
    }
}
