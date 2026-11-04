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

        static Material MMarmol => Mat("Mansion_Marmol", new Color(0.86f, 0.84f, 0.80f));
        static Material MMarmolOsc => Mat("Mansion_MarmolOsc", new Color(0.22f, 0.22f, 0.25f));
        static Material MAlfombra => Mat("Mansion_Alfombra", new Color(0.46f, 0.08f, 0.10f));
        static Material MDorado => Mat("Mansion_Dorado", new Color(0.78f, 0.60f, 0.22f), 0.25f);
        static Material MCuadroA => Mat("Mansion_CuadroA", new Color(0.18f, 0.32f, 0.45f));
        static Material MCuadroB => Mat("Mansion_CuadroB", new Color(0.55f, 0.36f, 0.18f));
        static Material MCristal => Mat("Mansion_Cristal", new Color(1f, 0.90f, 0.70f), 3f);

        static GameObject Columna(Transform p, string n, float x, float z, float y0, float y1, float radio = 0.55f)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = n; c.transform.SetParent(p, false);
            c.transform.position = new Vector3(x, (y0 + y1) * 0.5f, z); c.transform.localScale = new Vector3(radio * 2f, (y1 - y0) * 0.5f, radio * 2f);
            c.GetComponent<MeshRenderer>().sharedMaterial = MMarmol;
            Caja(p, n + "_Base", x - radio * 1.4f, x + radio * 1.4f, y0, y0 + 0.4f, z - radio * 1.4f, z + radio * 1.4f, MMarmolOsc, false);
            Caja(p, n + "_Capitel", x - radio * 1.5f, x + radio * 1.5f, y1 - 0.45f, y1, z - radio * 1.5f, z + radio * 1.5f, MDorado, false);
            return c;
        }

        static void Arana(Transform p, float x, float y, float z, float escala = 1f)
        {
            Caja(p, "Cadena", x - 0.04f, x + 0.04f, y, y + 1.2f, z - 0.04f, z + 0.04f, MDorado, false);
            Caja(p, "Lampara_Aro", x - 0.9f * escala, x + 0.9f * escala, y - 0.15f, y, z - 0.9f * escala, z + 0.9f * escala, MDorado, false);
            Caja(p, "Lampara_Cristal", x - 0.55f * escala, x + 0.55f * escala, y - 0.55f, y - 0.15f, z - 0.55f * escala, z + 0.55f * escala, MCristal, false);
            var g = new GameObject("Luz_Arana"); g.transform.SetParent(p, false); g.transform.position = new Vector3(x, y - 0.8f, z);
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 15f; l.intensity = 1.2f; l.color = new Color(1f, 0.88f, 0.68f); l.shadows = LightShadows.None;
        }

        // Una torre de escalera: devuelve el hueco que hay que dejar en la losa del piso de arriba.
        static Rect ArmarTorre(Transform p, G.Torre t)
        {
            float y0 = G.Nivel(t.nivelBase), H = G.Piso, yTop = y0 + H;
            float W = G.TorreAncho, F = G.TorreFondo, run = G.TramoLargo;
            float u0 = G.PuertaU0, uA = G.DivisoriaU0, uB = G.DivisoriaU1, uS = W - 0.4f;
            const float ini = 0.3f;
            string k = "Torre_" + t.nombre + "_";
            // Pared de la puerta (v 0..0.3) con la puerta del carril A (4,6 m de ancho, 4,4 m de alto).
            CajaTorre(p, t, k + "Puerta_I", 0f, u0, 0f, ini, y0, yTop, MMuro);
            CajaTorre(p, t, k + "Puerta_Dintel", u0, uA, 0f, ini, y0 + G.PuertaAlto, yTop, MMuro);
            // Bajo el final del carril B la pared queda 0,9 m mas baja que el piso de arriba: si llegara al piso, el NavMesh dejaria una franja
            // muerta justo donde se sale de la escalera. El hueco se tapa con una pieza solo visual.
            CajaTorre(p, t, k + "Puerta_D1", uA, uB, 0f, ini, y0, yTop, MMuro);
            CajaTorre(p, t, k + "Puerta_D2", uB, W, 0f, ini, y0, yTop - 0.9f, MMuro);
            CajaTorre(p, t, k + "Puerta_D2_Relleno", uB, W, 0f, ini, yTop - 0.9f, yTop - 0.05f, MMuro, false);
            if (t.nombre == "E") CajaTorre(p, t, k + "Lado_Exterior", -0.4f, 0f, 0f, F, y0, yTop + 1.1f, MMuro);
            CajaTorre(p, t, k + "Lado_Interior", uS, W, 0f, F, y0, yTop, MMuro);
            CajaTorre(p, t, k + "Baranda_Lado", uS, W, 0f, run + ini, yTop, yTop + 1.1f, MMuro);
            CajaTorre(p, t, k + "Divisoria", uA, uB, ini, run + ini, y0, yTop + 1.1f, MPilar);
            CajaTorre(p, t, k + "Baranda_Fondo", uB, uS, run + ini, run + ini + 0.3f, yTop, yTop + 1.1f, MMuro);
            // Pasamanos dorado sobre la baranda del hueco (solo visual).
            CajaTorre(p, t, k + "Pasamanos", uS - 0.05f, uS + 0.1f, ini, run + ini, yTop + 1.1f, yTop + 1.25f, MDorado, false);
            float yMed = y0 + H * 0.5f;
            // Bug #331 ("hay algo q solapa alli"): el rellano atravesaba todo el muro lateral (hasta u = W) y su sombra dejaba una franja oscura sobre la
            // cara del muro que da al salon; ahora termina en la cara interior del muro (u = uS) y no entra en el.
            CajaTorre(p, t, k + "Rellano", 0f, uS, run + ini, F, yMed - G.Losa, yMed, MRampa);
            Vector3 P(float cu, float cv, float y) { var w = t.Mundo(cu, cv); return new Vector3(w.x, y, w.y); }
            float ancho = G.MedioCarril * 2f;
            Tramo(p, k + "Tramo_A", P(G.CarrilA, ini, y0), P(G.CarrilA, run + ini, yMed), ancho, MRampa);
            Tramo(p, k + "Tramo_B", P(G.CarrilB, run + ini, yMed), P(G.CarrilB, ini, yTop), ancho, MRampa);
            // Alfombra roja sobre los dos tramos (solo visual, apenas por encima de la superficie).
            for (int c = 0; c < 2; c++)
            {
                var a = c == 0 ? P(G.CarrilA, ini, y0) : P(G.CarrilB, run + ini, yMed);
                var b = c == 0 ? P(G.CarrilA, run + ini, yMed) : P(G.CarrilB, ini, yTop);
                var alf = Tramo(p, k + "Alfombra_" + c, a + Vector3.up * 0.17f, b + Vector3.up * 0.17f, 1.6f, MAlfombra);
                alf.transform.localScale = new Vector3(1.6f, 0.04f, alf.transform.localScale.z);
                Object.DestroyImmediate(alf.GetComponent<RampaCaminable>()); Object.DestroyImmediate(alf.GetComponent<BoxCollider>());
                alf.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            var luz = new GameObject(k + "Luz"); luz.transform.SetParent(p, false);
            luz.transform.position = P(W * 0.5f, run * 0.5f, y0 + H - 1.5f);
            var l = luz.AddComponent<Light>(); l.type = LightType.Point; l.range = 13f; l.intensity = 1.3f; l.color = new Color(1f, 0.9f, 0.7f); l.shadows = LightShadows.None;
            var a2 = t.Mundo(uB, ini); var b2 = t.Mundo(uS, run + ini);
            return Rect.MinMaxRect(Mathf.Min(a2.x, b2.x), Mathf.Min(a2.y, b2.y), Mathf.Max(a2.x, b2.x), Mathf.Max(a2.y, b2.y));
        }

        static bool SobreTorre(float x0, float x1, float z0, float z1, int nivel)
        {
            foreach (var t in G.Torres)
            {
                if (t.nivelBase != nivel && t.nivelBase + 1 != nivel) continue;   // solo las torres que tocan este piso
                var a = t.Mundo(0f, 0f); var b = t.Mundo(G.TorreAncho, G.TorreFondo);
                if (x1 > Mathf.Min(a.x, b.x) - 0.1f && x0 < Mathf.Max(a.x, b.x) + 0.1f && z1 > Mathf.Min(a.y, b.y) - 0.1f && z0 < Mathf.Max(a.y, b.y) + 0.1f) return true;
            }
            return false;
        }

        // Piso de marmol en damero (solo visual).
        static void PisoDeLujo(Transform p, int nivel, float xa, float xb, float za, float zb)
        {
            float y = G.Nivel(nivel) + (nivel == 0 ? 0.13f : 0.005f);   // la calle pasa por debajo de la PB: el marmol va por encima del asfalto
            // Bug #329 ("quitar ese obstaculo"): la base de marmol era una sola caja sobre todo el piso y tapaba como una tapa el hueco de cada escalera
            // (se subia contra ella). Desde el 1.er piso se parte restando la huella de las torres que tocan el nivel.
            var huecosTorres = new List<Rect>();
            if (nivel >= 1)
                foreach (var t in G.Torres)
                {
                    if (t.nivelBase != nivel && t.nivelBase + 1 != nivel) continue;
                    var ta = t.Mundo(0f, 0f); var tb = t.Mundo(G.TorreAncho, G.TorreFondo);
                    huecosTorres.Add(Rect.MinMaxRect(Mathf.Min(ta.x, tb.x), Mathf.Min(ta.y, tb.y), Mathf.Max(ta.x, tb.x), Mathf.Max(ta.y, tb.y)));
                }
            int partes = 0;
            foreach (var r in RestarHuecos(Rect.MinMaxRect(xa, za, xb, zb), huecosTorres))
                Caja(p, "Marmol_Base_" + nivel + (partes++ == 0 ? "" : "_" + partes), r.xMin, r.xMax, y, y + 0.02f, r.yMin, r.yMax, MMarmol, false);
            const float L = 4f;
            for (float x = xa; x < xb - 0.1f; x += L)
                for (float z = za; z < zb - 0.1f; z += L)
                {
                    int ix = Mathf.FloorToInt((x - xa) / L), iz = Mathf.FloorToInt((z - za) / L);
                    if (((ix + iz) & 1) == 0) continue;
                    float x1 = Mathf.Min(x + L, xb), z1 = Mathf.Min(z + L, zb);
                    if (SobreTorre(x, x1, z, z1, nivel)) continue;
                    Caja(p, "Marmol_Oscuro", x, x1, y + 0.02f, y + 0.035f, z, z1, MMarmolOsc, false);
                }
        }

        // Puertas de servicio de los muros laterales (oeste: z 262..266; este: z 262..266 arriba y 266..270 abajo): planta baja y primer piso.
        public static List<(float z0, float z1, float y0, float y1)> PuertasLaterales(bool oeste) => new List<(float, float, float, float)>
        {
            oeste ? (262f, 266f, 0f, 4.4f) : (266f, 270f, 0f, 4.4f),
            (262f, 266f, G.Nivel(1), G.Nivel(1) + 4.4f),
        };

        static bool VentanaChocaConPuerta(bool oeste, float z, float yc)
        {
            foreach (var p in PuertasLaterales(oeste))
                if (z + 0.9f > p.z0 && z - 0.9f < p.z1 && yc + 1.5f > p.y0 && yc - 1.5f < p.y1) return true;
            return false;
        }

        static void MuroConPuertas(Transform casco, string n, float xa, float xb, float yMax, List<(float z0, float z1, float y0, float y1)> puertas)
        {
            var zs = new SortedSet<float> { G.Z0, G.Z1 };
            foreach (var p in puertas) { zs.Add(p.z0); zs.Add(p.z1); }
            var l = new List<float>(zs); int c = 0;
            for (int i = 0; i + 1 < l.Count; i++)
            {
                float za = l[i], zb = l[i + 1], zm = (za + zb) * 0.5f;
                var huecos = new List<(float, float)>();
                foreach (var p in puertas) if (zm > p.z0 && zm < p.z1) huecos.Add((p.y0, p.y1));
                huecos.Sort(); float y = 0f;
                foreach (var h in huecos) { if (h.Item1 > y + 0.01f) Caja(casco, $"{n}_{++c}", xa, xb, y, h.Item1, za, zb, MMuro); y = h.Item2; }
                if (yMax > y + 0.01f) Caja(casco, $"{n}_{++c}", xa, xb, y, yMax, za, zb, MMuro);
            }
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
            float xi0 = G.X0 + G.Muro, xi1 = G.X1 - G.Muro, zi0 = G.Z0 + G.Muro, zi1 = G.Z1 - G.Muro;
            // ---- casco: cuatro muros con el hueco del porton al sur ----
            Caja(casco, "Muro_S_Oeste", G.X0, gx0, 0f, yMax, G.Z0, G.Z0 + G.Muro, MMuro);
            Caja(casco, "Muro_S_Este", gx1, G.X1, 0f, yMax, G.Z0, G.Z0 + G.Muro, MMuro);
            Caja(casco, "Muro_S_Dintel", gx0, gx1, G.PortonAlto, yMax, G.Z0, G.Z0 + G.Muro, MMuro);
            Caja(casco, "Muro_N", G.X0, G.X1, 0f, yMax, G.Z1 - G.Muro, G.Z1, MMuro);
            MuroConPuertas(casco, "Muro_O", G.X0, G.X0 + G.Muro, yMax, PuertasLaterales(true));
            MuroConPuertas(casco, "Muro_E", G.X1 - G.Muro, G.X1, yMax, PuertasLaterales(false));
            float medio = (gx0 + gx1) * 0.5f;
            foreach (int lado in new[] { -1, 1 })
            {
                float xa = lado < 0 ? gx0 : medio, xb = lado < 0 ? medio : gx1;
                var hoja = Caja(casco, lado < 0 ? "Porton_Hoja_O" : "Porton_Hoja_E", xa, xb, 0f, G.PortonAlto, G.Z0 + 0.05f, G.Z0 + 0.55f, MBarrote);
                hoja.AddComponent<NavMeshModifier>().ignoreFromBuild = true;   // la hoja se abre: la malla del hueco queda caminable
                var pd = hoja.AddComponent<PortonDeMansion>();
                pd.cerrada = hoja.transform.position;
                pd.abierta = hoja.transform.position + new Vector3(lado * (G.PortonAncho * 0.5f + 0.2f), 0f, 1.0f);
                // Bug #326 ("corrije esas 2 franjas"): las franjas doradas del porton eran del Detalle y quedaban flotando al abrirse la hoja; ahora son de la hoja.
                var franja = Caja(deco, "Porton_Dorado", xa + 0.2f, xb - 0.2f, 2.3f, 2.5f, G.Z0 - 0.04f, G.Z0 + 0.05f, MDorado, false);
                franja.transform.SetParent(hoja.transform, true);
            }
            // Portico de entrada: cuatro columnas, frontis y escalinata (solo visual).
            foreach (float cx in new[] { 313.5f, 318f, 326f, 330.5f }) Columna(deco, "Portico_Columna", cx, G.Z0 - 1.6f, 0f, 8.4f, 0.6f);
            Caja(deco, "Portico_Arquitrabe", 311.5f, 332.5f, 8.4f, 9.4f, G.Z0 - 2.6f, G.Z0 + 0.1f, MPilar, false);
            Caja(deco, "Portico_Frontis", 312.5f, 331.5f, 9.4f, 10.3f, G.Z0 - 2.2f, G.Z0, MMuro, false);
            Caja(deco, "Portico_Frontis_Alto", 316f, 328f, 10.3f, 11.0f, G.Z0 - 1.8f, G.Z0, MMuro, false);
            for (int e = 0; e < 4; e++) Caja(deco, "Escalinata_" + e, 310f + e * 0.2f, 334f - e * 0.2f, 0f, 0.14f * (4 - e), G.Z0 - 3.4f + e * 0.55f, G.Z0 - 2.9f + e * 0.55f + 0.5f, MMarmol, false);
            Caja(deco, "Alfombra_Entrada", 319.6f, 324.4f, 0.09f, 0.12f, G.Z0 - 3.3f, G.Z0 + 0.6f, MAlfombra, false);
            for (int n = 1; n <= 3; n++)
            {
                float y = G.Nivel(n);
                Caja(deco, "Cornisa_S_" + n, G.X0 - 0.3f, G.X1 + 0.3f, y - 0.3f, y + 0.12f, G.Z0 - 0.3f, G.Z0, MDorado, false);
                Caja(deco, "Cornisa_N_" + n, G.X0 - 0.3f, G.X1 + 0.3f, y - 0.3f, y + 0.12f, G.Z1, G.Z1 + 0.3f, MDorado, false);
                Caja(deco, "Cornisa_O_" + n, G.X0 - 0.3f, G.X0, y - 0.3f, y + 0.12f, G.Z0 - 0.3f, G.Z1 + 0.3f, MDorado, false);
                Caja(deco, "Cornisa_E_" + n, G.X1, G.X1 + 0.3f, y - 0.3f, y + 0.12f, G.Z0 - 0.3f, G.Z1 + 0.3f, MDorado, false);
            }
            // Ventanales altos con marco dorado.
            for (int n = 0; n < 3; n++)
            {
                float yc = G.Nivel(n) + 3.4f;
                for (float x = 300f; x < 346f; x += 5.4f)
                {
                    if (n == 0 && x > gx0 - 3f && x < gx1 + 3f) continue;
                    Caja(deco, "Marco_S", x - 1.1f, x + 1.1f, yc - 1.7f, yc + 1.7f, G.Z0 - 0.12f, G.Z0, MDorado, false);
                    Caja(deco, "Ventana_S", x - 0.9f, x + 0.9f, yc - 1.5f, yc + 1.5f, G.Z0 - 0.16f, G.Z0 - 0.1f, Ventana, false);
                    Caja(deco, "Marco_N", x - 1.1f, x + 1.1f, yc - 1.7f, yc + 1.7f, G.Z1, G.Z1 + 0.12f, MDorado, false);
                    Caja(deco, "Ventana_N", x - 0.9f, x + 0.9f, yc - 1.5f, yc + 1.5f, G.Z1 + 0.1f, G.Z1 + 0.16f, Ventana, false);
                }
                for (float z = 252f; z < 284f; z += 6f)
                {
                    // Bug #335 ("quita eso q esta levitando"): la ventana luminosa de las puertas de servicio laterales quedaba flotando en el hueco.
                    if (!VentanaChocaConPuerta(true, z, yc)) Caja(deco, "Ventana_O", G.X0 - 0.16f, G.X0 - 0.1f, yc - 1.5f, yc + 1.5f, z - 0.9f, z + 0.9f, Ventana, false);
                    if (!VentanaChocaConPuerta(false, z, yc)) Caja(deco, "Ventana_E", G.X1 + 0.1f, G.X1 + 0.16f, yc - 1.5f, yc + 1.5f, z - 0.9f, z + 0.9f, Ventana, false);
                }
            }

            // ---- losas (con el hueco de cada torre que sube a ese piso) ----
            var huecos = new Dictionary<int, List<Rect>> { { 1, new List<Rect>() }, { 2, new List<Rect>() }, { 3, new List<Rect>() } };
            for (int i = 0; i < G.Torres.Length; i++)
            {
                var torre = G.Torres[i];
                huecos[torre.nivelBase + 1].Add(ArmarTorre(escaleras, torre));
            }
            var total = Rect.MinMaxRect(xi0, zi0, xi1, zi1);
            for (int n = 1; n <= 3; n++)
            {
                int c = 0;
                foreach (var r in RestarHuecos(total, huecos[n]))
                    Caja(pisos, $"Losa_{n}_{++c}", r.xMin, r.xMax, G.Nivel(n) - G.Losa, G.Nivel(n), r.yMin, r.yMax, MLosa);
            }
            for (int n = 0; n <= 2; n++) PisoDeLujo(deco, n, xi0, xi1, zi0, zi1);

            // ---- planta baja: vestibulo con columnas, mostrador, chimenea, alfombra y sofas ----
            float hPB = G.Piso - G.Losa;
            foreach (float x in new[] { 308f, 317f, 327f, 336f })
                foreach (float z in new[] { 254f, 268f })
                    Columna(interior, "PB_Columna", x, z, 0f, hPB, 0.6f);
            Caja(deco, "PB_Alfombra", 319f, 325f, 0.17f, 0.2f, zi0, 272f, MAlfombra, false);
            Caja(interior, "PB_Mostrador", 316f, 328f, 0f, 1.2f, 262.6f, 263.8f, MMueble);
            Caja(deco, "PB_Mostrador_Tapa", 315.8f, 328.2f, 1.2f, 1.3f, 262.5f, 263.9f, MMarmol, false);
            Caja(interior, "PB_Mampara_O", 316f, 319f, 0f, 1.4f, 251f, 251.4f, MBarrote);
            Caja(interior, "PB_Mampara_E", 325f, 328f, 0f, 1.4f, 251f, 251.4f, MBarrote);
            foreach (var (x, z, w) in new[] { (308f, 262f, 3f), (336f, 262f, 3f), (308f, 274f, 3f), (336f, 274f, 3f), (322f, 280f, 4f) })
                Caja(interior, "PB_Sofa", x - w * 0.5f, x + w * 0.5f, 0f, 1f, z - 0.55f, z + 0.55f, MMueble);
            // Chimenea monumental en el muro oeste y cuadros en el muro norte.
            Caja(deco, "PB_Chimenea", xi0, xi0 + 0.9f, 0f, 3.2f, 280f, 285f, MMarmolOsc, false);
            Caja(deco, "PB_Chimenea_Fuego", xi0 + 0.9f, xi0 + 1.0f, 0.4f, 1.4f, 281.6f, 283.4f, Mat("Mansion_Fuego", new Color(1f, 0.5f, 0.15f), 3f), false);
            foreach (float x in new[] { 305f, 313f, 331f, 339f })
                Caja(deco, "PB_Cuadro", x - 1.4f, x + 1.4f, 2.0f, 4.4f, zi1 - 0.06f, zi1, x < 322f ? MCuadroA : MCuadroB, false);
            foreach (float z in new[] { 254f, 268f }) Arana(deco, 322f, hPB - 0.2f, z, 1.6f);
            Arana(deco, 322f, hPB - 0.2f, 278f, 1.2f);

            // ---- primer piso: galeria ancha y seis salones ----
            float y1 = G.Nivel(1), y1t = G.Nivel(2) - G.Losa;
            void Tabique(string n, float x0, float x1, float z0, float z1, float[] puertas, float yb, float yt)
            {
                float cur = x0; int i = 0;
                var ord = new List<float>(puertas); ord.Sort();
                foreach (float px in ord)
                {
                    Caja(interior, $"{n}_{++i}", cur, px - 1.8f, yb, yt, z0, z1, MTabique);
                    Caja(interior, $"{n}_Dintel_{i}", px - 1.8f, px + 1.8f, yb + 3.8f, yt, z0, z1, MTabique);
                    Caja(deco, $"{n}_Marco_{i}", px - 1.9f, px + 1.9f, yb + 3.8f, yb + 4.0f, z0 - 0.05f, z1 + 0.05f, MDorado, false);
                    cur = px + 1.8f;
                }
                Caja(interior, $"{n}_{++i}", cur, x1, yb, yt, z0, z1, MTabique);
            }
            Tabique("P1_TabiqueS", xi0, 336.2f, 258.9f, 259.2f, new[] { 303f, 316f, 329f }, y1, y1t);
            Tabique("P1_TabiqueN", 307.8f, xi1, 272.8f, 273.1f, new[] { 313f, 326f, 340f }, y1, y1t);
            foreach (float x in new[] { 309.5f, 322.5f })
                Caja(interior, "P1_Medianera_S", x - 0.15f, x + 0.15f, y1, y1t, zi0, 258.9f, MTabique);
            foreach (float x in new[] { 320f, 333.5f })
                Caja(interior, "P1_Medianera_N", x - 0.15f, x + 0.15f, y1, y1t, 273.1f, zi1, MTabique);
            Caja(deco, "P1_Alfombra", xi0, xi1, y1 + 0.04f, y1 + 0.07f, 264.4f, 267.6f, MAlfombra, false);
            Caja(interior, "P1_Cajon_1", 311f, 315f, y1, y1 + 1.2f, 262.4f, 263.6f, MMueble);
            Caja(interior, "P1_Cajon_2", 330f, 334f, y1, y1 + 1.2f, 268.4f, 269.6f, MMueble);
            Caja(interior, "P1_Aparador", 322f, 326f, y1, y1 + 1.0f, 259.4f, 260.2f, MMueble);
            foreach (var (x, z) in new[] { (303f, 252.5f), (316f, 252.5f), (329f, 252.5f), (313.5f, 279f), (327f, 279f), (340.5f, 279f) })
                Caja(interior, "P1_Mesa", x - 1.4f, x + 1.4f, y1, y1 + 0.95f, z - 0.9f, z + 0.9f, MMueble);
            for (float x = 305f; x < 345f; x += 13f) Arana(deco, x, y1t - 0.2f, 266f, 1.3f);

            // ---- segundo piso: salon de baile con columnas, barra y las celdas ----
            float y2 = G.Nivel(2), y2t = G.Nivel(3) - G.Losa;
            foreach (float x in new[] { 308f, 320f, 332f, 342f })
                foreach (float z in new[] { 252f, 266f })
                    Columna(interior, "P2_Columna", x, z, y2, y2t, 0.6f);
            Caja(deco, "P2_Alfombra", 306f, 338f, y2 + 0.04f, y2 + 0.07f, 258.4f, 261.6f, MAlfombra, false);
            Caja(interior, "P2_Barra", 332f, 345f, y2, y2 + 1.2f, 280f, 281.2f, MMueble);
            Caja(deco, "P2_Barra_Tapa", 331.8f, 345.2f, y2 + 1.2f, y2 + 1.3f, 279.8f, 281.4f, MMarmolOsc, false);
            foreach (var (x, z) in new[] { (312f, 256f), (330f, 256f), (314f, 262f), (326f, 262f), (338f, 264f) })
                Caja(interior, "P2_Sofa", x - 1.6f, x + 1.6f, y2, y2 + 1.1f, z - 0.7f, z + 0.7f, MMueble);
            Caja(interior, "P2_Piano", 320f, 324f, y2, y2 + 1.4f, 252.5f, 254.5f, MBarrote);
            // Celdas contra el muro norte (x 297..315,4, z 279,5..285,4), frente de barrotes.
            Caja(interior, "P2_Celdas_Fondo_Piso", 297f, 315.4f, y2 + 0.005f, y2 + 0.05f, 279.5f, zi1, MPiso, false);
            foreach (float x in new[] { 297f, 301.6f, 306.2f, 310.8f, 315.4f })
                Caja(interior, "P2_Celda_Division", x - 0.15f, x + 0.15f, y2, y2t, 279.5f, zi1, MTabique);
            for (float x = 297.4f; x < 315.3f; x += 0.45f)
                Caja(interior, "P2_Barrote", x - 0.04f, x + 0.04f, y2, y2 + 3.6f, 279.4f, 279.5f, MBarrote, true);
            Caja(interior, "P2_Celdas_Dintel", 297f, 315.4f, y2 + 3.6f, y2t, 279.3f, 279.6f, MTabique);
            foreach (float x in new[] { 310f, 322f, 334f }) Arana(deco, x, y2t - 0.2f, 258f, 1.6f);

            // ---- azotea: coberturas y helipuerto ----
            float y3 = G.Nivel(3);
            foreach (var (x, z, w, d, h) in new[] { (308f, 254f, 3.6f, 1.8f, 1.3f), (336f, 254f, 3.6f, 1.8f, 1.3f), (306f, 278f, 3.6f, 1.8f, 1.3f), (338f, 278f, 3.6f, 1.8f, 1.3f), (322f, 252f, 4.4f, 1.5f, 1.2f), (322f, 281f, 4.4f, 1.5f, 1.2f), (312f, 266f, 3f, 1.6f, 1.2f), (332f, 266f, 3f, 1.6f, 1.2f) })
                Caja(interior, "Azotea_Equipo", x - w * 0.5f, x + w * 0.5f, y3, y3 + h, z - d * 0.5f, z + d * 0.5f, MBarrote);
            var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disco.name = "Azotea_Helipuerto";
            Object.DestroyImmediate(disco.GetComponent<Collider>());
            disco.transform.SetParent(deco, false);
            disco.transform.position = new Vector3(322f, y3 + 0.02f, 266f); disco.transform.localScale = new Vector3(11f, 0.01f, 11f);
            disco.GetComponent<MeshRenderer>().sharedMaterial = Mat("Mansion_Helipuerto", new Color(0.85f, 0.75f, 0.2f));
            disco.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            PasadasDeLujo(interior, deco);
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
                        bool candidato = n.StartsWith("Ayuntamiento") || n.StartsWith("Rotulo_AYUNTAMIENTO") || n.StartsWith("Radio_") || n.StartsWith("Rotulo_RADIO") || n.StartsWith("Campanario") || n.StartsWith("Farola") || n.StartsWith("Auto_Viejo") || n.StartsWith("Lampara_x") || n.StartsWith("Pantalla_x") || n.StartsWith("Op_EquipoAire") || n == "Plaza_Banco";
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
