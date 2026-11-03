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
    // Rediseno de caminos (2026-11-03, 50 pasadas): la ciudad y la mansion ganan rutas alternas y opciones tacticas.
    //   callejones   cada pasaje entre edificios tiene arco con color de ruta, luces, coberturas en chicana y (los largos) una pasarela elevada
    //   calles       barreras en chicana en la calle este-oeste, islas de cobertura en la avenida y muretes de entrada a la plaza
    //   mansion      puertas de servicio laterales (planta baja y 1.er piso) y dos escaleras exteriores de servicio hasta el 1.er piso
    // Todo lo que se pisa en altura es RampaCaminable o losa con collider; lo que decora no tiene collider.
    public static partial class OperacionBuilder
    {
        const string GrupoCaminos = "12_Caminos";
        static Material MNeonAmbar => Mat("Caminos_NeonAmbar", new Color(1f, 0.62f, 0.12f), 3f);
        static Material MNeonVerde => Mat("Caminos_NeonVerde", new Color(0.3f, 1f, 0.45f), 3f);
        static Material MHormigon => Mat("Caminos_Hormigon", new Color(0.52f, 0.52f, 0.50f));
        static Material MCajon => Mat("Caminos_Cajon", new Color(0.45f, 0.33f, 0.2f));
        static Material MAndamio => Mat("Caminos_Andamio", new Color(0.30f, 0.32f, 0.34f));
        static Material NeonRuta(int i) => (i % 4) == 0 ? MNeonRosa : (i % 4) == 1 ? MNeonCian : (i % 4) == 2 ? MNeonAmbar : MNeonVerde;
        static Color ColorRuta(int i) => (i % 4) == 0 ? new Color(1f, 0.4f, 0.7f) : (i % 4) == 1 ? new Color(0.4f, 0.9f, 1f) : (i % 4) == 2 ? new Color(1f, 0.7f, 0.25f) : new Color(0.45f, 1f, 0.55f);

        struct Callejon { public float x0, x1, z0, z1; public bool ew; public int id; }

        // Los pasajes de 4 m que dejan los edificios de cada manzana (misma cuenta que ConstruirCiudadIntegral).
        static List<Callejon> Callejones()
        {
            var l = new List<Callejon>(); int id = 0;
            foreach (var m in Manzanas)
            {
                float ancho = m.x1 - m.x0, largo = m.z1 - m.z0, alto = (largo - 8f) / 3f;
                for (int i = 0; i < 2; i++) { float z0 = m.z0 + i * (alto + 4f) + alto; l.Add(new Callejon { x0 = m.x0, x1 = m.x1, z0 = z0, z1 = z0 + 4f, ew = true, id = id++ }); }
                if (ancho >= 30f) { float anc = (ancho - 4f) / 2f; l.Add(new Callejon { x0 = m.x0 + anc, x1 = m.x0 + anc + 4f, z0 = m.z0, z1 = m.z1, ew = false, id = id++ }); }
            }
            return l;
        }

        static bool Libre(float x0, float x1, float z0, float z1, float y0 = 0.5f, float y1 = 3f)
        {
            Physics.SyncTransforms();
            return !Physics.CheckBox(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3((x1 - x0) * 0.5f, (y1 - y0) * 0.5f, (z1 - z0) * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        // Losa elevada con collider que no entra al NavMesh (se camina por encima, el piso de abajo sigue libre).
        static GameObject Elevado(Transform p, string n, float x0, float x1, float y0, float y1, float z0, float z1, Material m)
        {
            var g = Caja(p, n, x0, x1, y0, y1, z0, z1, m, true);
            g.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return g;
        }

        // Rampa caminable con su malla fuera del NavMesh y barandas a los dos lados.
        static void Escalera(Transform p, string n, Vector3 a, Vector3 b, float ancho)
        {
            var r = Tramo(p, n, a, b, ancho, MAndamio);
            r.GetComponent<RampaCaminable>().anchoMedio = ancho * 0.5f;
            r.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            var dir = (b - a).normalized; var lado = Vector3.Cross(Vector3.up, dir).normalized;
            foreach (int s in new[] { -1, 1 })
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n + "_Baranda"; g.transform.SetParent(p, false);
                var pa = a + lado * (s * (ancho * 0.5f + 0.04f)) + Vector3.up * 1.0f; var pb = b + lado * (s * (ancho * 0.5f + 0.04f)) + Vector3.up * 1.0f;
                g.transform.SetPositionAndRotation((pa + pb) * 0.5f, Quaternion.LookRotation((pb - pa).normalized, Vector3.up));
                g.transform.localScale = new Vector3(0.07f, 0.07f, (pb - pa).magnitude);
                g.GetComponent<MeshRenderer>().sharedMaterial = MBarrote; Object.DestroyImmediate(g.GetComponent<Collider>());
                g.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                for (float t = 0f; t <= 1.001f; t += 0.25f)
                {
                    var q = Vector3.Lerp(pa, pb, t);
                    Caja(p, n + "_Montante", q.x - 0.03f, q.x + 0.03f, q.y - 1.0f, q.y, q.z - 0.03f, q.z + 0.03f, MBarrote, false);
                }
            }
        }

        // 1-18: callejones.
        static void CaminosCallejones(StringBuilderLite res, Transform t)
        {
            var lista = Callejones(); int arcos = 0, luces = 0, cob = 0, pas = 0, rotulos = 0;
            foreach (var c in lista)
            {
                var color = ColorRuta(c.id); var neon = NeonRuta(c.id);
                // 1-3: arco de entrada en cada boca (dos pilastras, dintel, tira de neon con el color de la ruta y luz).
                for (int e = 0; e < 2; e++)
                {
                    if (c.ew)
                    {
                        float x = e == 0 ? c.x0 : c.x1;
                        float xa = e == 0 ? x : x - 0.6f, xb = e == 0 ? x + 0.6f : x;
                        Caja(t, "Arco_Pilar", xa, xb, 0.1f, 4.8f, c.z0, c.z0 + 0.35f, MHormigon, false); Caja(t, "Arco_Pilar", xa, xb, 0.1f, 4.8f, c.z1 - 0.35f, c.z1, MHormigon, false);
                        Caja(t, "Arco_Dintel", xa, xb, 4.8f, 5.3f, c.z0, c.z1, MMarmolOsc, false);
                        Caja(t, "Arco_Neon", xa + (e == 0 ? 0.55f : -0.02f), xb - (e == 0 ? 0.02f : 0.55f), 4.5f, 4.7f, c.z0 + 0.4f, c.z1 - 0.4f, neon, false);
                        LuzPuntual(t, "Luz_Arco_Callejon", new Vector3(x + (e == 0 ? 1.2f : -1.2f), 4.2f, (c.z0 + c.z1) * 0.5f), 7f, 1.3f, color);
                    }
                    else
                    {
                        float z = e == 0 ? c.z0 : c.z1; float za = e == 0 ? z : z - 0.6f, zb = e == 0 ? z + 0.6f : z;
                        Caja(t, "Arco_Pilar", c.x0, c.x0 + 0.35f, 0.1f, 4.8f, za, zb, MHormigon, false); Caja(t, "Arco_Pilar", c.x1 - 0.35f, c.x1, 0.1f, 4.8f, za, zb, MHormigon, false);
                        Caja(t, "Arco_Dintel", c.x0, c.x1, 4.8f, 5.3f, za, zb, MMarmolOsc, false);
                        Caja(t, "Arco_Neon", c.x0 + 0.4f, c.x1 - 0.4f, 4.5f, 4.7f, za + (e == 0 ? 0.55f : -0.02f), zb - (e == 0 ? 0.02f : 0.55f), neon, false);
                        LuzPuntual(t, "Luz_Arco_Callejon", new Vector3((c.x0 + c.x1) * 0.5f, 4.2f, z + (e == 0 ? 1.2f : -1.2f)), 7f, 1.3f, color);
                    }
                    arcos++;
                }
                float largo = c.ew ? c.x1 - c.x0 : c.z1 - c.z0;
                // 4-6: guirnalda de bombillas a lo largo del callejon y farolillo de pared cada 8 m con el color de la ruta.
                for (float s = 1.5f; s < largo - 1f; s += 2.2f)
                {
                    var pos = c.ew ? new Vector3(c.x0 + s, 4.4f - 0.35f * Mathf.Sin(s * 1.2f), (c.z0 + c.z1) * 0.5f) : new Vector3((c.x0 + c.x1) * 0.5f, 4.4f - 0.35f * Mathf.Sin(s * 1.2f), c.z0 + s);
                    Esfera(t, "Callejon_Bombilla", pos, 0.2f, MBombilla);
                }
                for (float s = 4f; s < largo - 2f; s += 8f)
                {
                    var pos = c.ew ? new Vector3(c.x0 + s, 3.4f, (c.z0 + c.z1) * 0.5f) : new Vector3((c.x0 + c.x1) * 0.5f, 3.4f, c.z0 + s);
                    LuzPuntual(t, "Luz_Callejon", pos, 8f, 1.5f, Color.Lerp(color, new Color(1f, 0.85f, 0.6f), 0.5f)); luces++;
                }
                // 7-9: coberturas en chicana (cajones, contenedor y barriles) que dejan 1,6 m de paso y obligan a elegir lado.
                int k = 0;
                for (float s = 5f; s < largo - 4f; s += 6.5f)
                {
                    bool lado = (k++ % 2) == 0;
                    float ancho = 1.6f, fondo = 1.1f, mid = c.ew ? (c.z0 + c.z1) * 0.5f : (c.x0 + c.x1) * 0.5f;
                    float off = lado ? -1.1f : 1.1f;
                    float cx = c.ew ? c.x0 + s : mid + off, cz = c.ew ? mid + off : c.z0 + s;
                    float ex = c.ew ? ancho : fondo, ez = c.ew ? fondo : ancho;
                    if (!Libre(cx - ex * 0.5f, cx + ex * 0.5f, cz - ez * 0.5f, cz + ez * 0.5f)) continue;
                    Caja(t, "Callejon_Cajon", cx - ex * 0.5f, cx + ex * 0.5f, 0.1f, 1.15f, cz - ez * 0.5f, cz + ez * 0.5f, k % 3 == 0 ? MChapa(0) : MCajon);
                    Caja(t, "Callejon_Cajon_Tapa", cx - ex * 0.5f - 0.04f, cx + ex * 0.5f + 0.04f, 1.15f, 1.22f, cz - ez * 0.5f - 0.04f, cz + ez * 0.5f + 0.04f, MBarrote, false);
                    cob++;
                }
                // 10-12: puertas de servicio con farol (solo decoracion) a ambos lados del callejon, para que cada pasaje se lea como ruta de servicio.
                for (float s = 6f; s < largo - 3f; s += 9f)
                {
                    if (c.ew)
                    {
                        Caja(t, "Servicio_Puerta", c.x0 + s - 0.6f, c.x0 + s + 0.6f, 0.1f, 2.4f, c.z0 - 0.02f, c.z0 + 0.06f, MChapa(1), false);
                        Caja(t, "Servicio_Puerta", c.x0 + s + 2.5f - 0.6f, c.x0 + s + 2.5f + 0.6f, 0.1f, 2.4f, c.z1 - 0.06f, c.z1 + 0.02f, MChapa(2), false);
                        Esfera(t, "Servicio_Farol", new Vector3(c.x0 + s, 2.7f, c.z0 + 0.25f), 0.28f, MBombilla);
                    }
                    else
                    {
                        Caja(t, "Servicio_Puerta", c.x0 - 0.02f, c.x0 + 0.06f, 0.1f, 2.4f, c.z0 + s - 0.6f, c.z0 + s + 0.6f, MChapa(1), false);
                        Caja(t, "Servicio_Puerta", c.x1 - 0.06f, c.x1 + 0.02f, 0.1f, 2.4f, c.z0 + s + 2.5f - 0.6f, c.z0 + s + 2.5f + 0.6f, MChapa(2), false);
                        Esfera(t, "Servicio_Farol", new Vector3(c.x0 + 0.25f, 2.7f, c.z0 + s), 0.28f, MBombilla);
                    }
                }
                // 13-15: tubos de ventilacion y cableado sobre el pasaje, con carteles de la ruta en el medio.
                for (float s = 3f; s < largo - 2f; s += 5.5f)
                {
                    if (c.ew) { Cilindro(t, "Ducto", c.x0 + s, 4.0f, 4.15f, c.z0 + 0.3f, 0.35f, MetalOsc); Caja(t, "Cable", c.x0 + s - 2.7f, c.x0 + s, 4.2f, 4.24f, c.z1 - 0.3f, c.z1 - 0.26f, MBarrote, false); }
                    else { Cilindro(t, "Ducto", c.x0 + 0.3f, 4.0f, 4.15f, c.z0 + s, 0.35f, MetalOsc); Caja(t, "Cable", c.x1 - 0.3f, c.x1 - 0.26f, 4.2f, 4.24f, c.z0 + s - 2.7f, c.z0 + s, MBarrote, false); }
                }
                {
                    float mx = (c.x0 + c.x1) * 0.5f, mz = (c.z0 + c.z1) * 0.5f;
                    if (c.ew) Caja(t, "Rotulo_Ruta", mx - 1.4f, mx + 1.4f, 3.6f, 4.05f, c.z0 + 0.05f, c.z0 + 0.12f, neon, false);
                    else Caja(t, "Rotulo_Ruta", c.x0 + 0.05f, c.x0 + 0.12f, 3.6f, 4.05f, mz - 1.4f, mz + 1.4f, neon, false);
                    rotulos++;
                }
                // 16-18: pasarela elevada (solo en los callejones largos): rampa de subida, tramo recto con baranda y rampa de bajada.
                if (c.ew && largo >= 29f)
                {
                    float zc = c.z0 + 0.8f, yP = 4.4f, rampa = 10.5f;
                    float xa = c.x0 + 1.0f, xb = c.x1 - 1.0f;
                    Escalera(t, "Pasarela_Subida", new Vector3(xa, 0.05f, zc), new Vector3(xa + rampa, yP, zc), 1.4f);
                    Elevado(t, "Pasarela_Tramo", xa + rampa, xb - rampa, yP - 0.2f, yP, c.z0 + 0.1f, c.z0 + 1.5f, MAndamio);
                    Escalera(t, "Pasarela_Bajada", new Vector3(xb - rampa, yP, zc), new Vector3(xb, 0.05f, zc), 1.4f);
                    Caja(t, "Pasarela_Baranda", xa + rampa, xb - rampa, yP, yP + 1.0f, c.z0 + 1.46f, c.z0 + 1.5f, MBarrote, false);
                    for (float x = xa + rampa; x < xb - rampa; x += 2f) Caja(t, "Pasarela_Soporte", x - 0.04f, x + 0.04f, 0f, yP - 0.2f, c.z0 + 1.4f, c.z0 + 1.48f, MBarrote, false);
                    LuzPuntual(t, "Luz_Pasarela", new Vector3((xa + xb) * 0.5f, yP + 1.2f, zc), 9f, 1.4f, color);
                    pas++;
                }
            }
            res.Add($"1-18 callejones ({lista.Count}): {arcos} arcos con neon por ruta, guirnaldas y {luces} farolillos, {cob} coberturas en chicana, puertas de servicio, ductos, {rotulos} rotulos, {pas} pasarelas elevadas");
        }

        // 19-32: calles y plaza.
        static void CaminosCalles(StringBuilderLite res, Transform t)
        {
            int n = 0;
            // 19-22: barreras de hormigon en chicana sobre la calle este-oeste (afuera del anillo): dos carriles estrechos y cobertura a cada lado.
            foreach (float x in new[] { 282f, 292f, 352f, 362f })
            {
                foreach (var (z0, z1) in new[] { (223.6f, 226.6f), (231.6f, 234.6f) })
                {
                    if (!Libre(x - 0.6f, x + 0.6f, z0, z1)) continue;
                    Caja(t, "Barrera_Jersey", x - 0.5f, x + 0.5f, 0.1f, 0.95f, z0, z1, MHormigon);
                    Caja(t, "Barrera_Franja", x - 0.51f, x + 0.51f, 0.55f, 0.7f, z0, z1, MChapa(0), false);
                    n++;
                }
                LuzPuntual(t, "Luz_Barrera", new Vector3(x, 1.4f, 229f), 8f, 1.2f, new Color(1f, 0.45f, 0.2f));
            }
            // 23-24: autos abandonados atravesados formando un bloqueo parcial en la boca norte y sur de la avenida del anillo.
            foreach (var (x, z, yaw) in new[] { (299f, 295.5f, 12f), (345f, 295.5f, -10f), (299f, 168.5f, -8f), (345f, 168.5f, 14f) })
            {
                if (!Libre(x - 2.4f, x + 2.4f, z - 1.2f, z + 1.2f)) continue;
                ArmarAuto(t, "Auto_Bloqueo", x, z, 90f + yaw, MChapa(n % 3)); n++;
            }
            // 25-27: islas de cobertura en el eje de la avenida (jardinera larga con arbolito y farol) para cruzar por tramos.
            foreach (float z in new[] { 187f, 199f })
            {
                if (!Libre(320.6f, 323.4f, z - 3f, z + 3f)) continue;
                Caja(t, "Isla_Cantero", 320.8f, 323.2f, 0.1f, 0.9f, z - 3f, z + 3f, MMarmolOsc);
                Caja(t, "Isla_Borde", 320.7f, 323.3f, 0.9f, 1.0f, z - 3.1f, z + 3.1f, MDorado, false);
                for (int i = 0; i < 5; i++) Esfera(t, "Isla_Arbusto", new Vector3(322f, 1.35f, z - 2.4f + i * 1.2f), 0.9f, MCopa);
                LuzPuntual(t, "Luz_Isla", new Vector3(322f, 1.8f, z), 8f, 1.3f, new Color(0.6f, 1f, 0.7f)); n++;
            }
            // 28-29: muretes de cobertura en la boca sur de la plaza (dos tramos con paso al centro y a los costados).
            foreach (var (x0, x1) in new[] { (310.5f, 315.5f), (328.5f, 333.5f) })
            {
                if (!Libre(x0, x1, 208.8f, 209.6f)) continue;
                Caja(t, "Murete_Plaza", x0, x1, 0.1f, 1.05f, 208.8f, 209.6f, MMarmol);
                Caja(t, "Murete_Plaza_Tapa", x0 - 0.06f, x1 + 0.06f, 1.05f, 1.13f, 208.7f, 209.7f, MDorado, false); n++;
            }
            // 30-31: puestos de feria dobles frente a la plaza (cobertura y bloqueo parcial del tiro desde la calle).
            foreach (var (x, z) in new[] { (298f, 215f), (346f, 215f) })
            {
                if (!Libre(x - 1.6f, x + 1.6f, z - 1.1f, z + 1.1f)) continue;
                Caja(t, "Feria_Puesto", x - 1.5f, x + 1.5f, 0.1f, 1.1f, z - 0.7f, z + 0.7f, MMueble);
                Caja(t, "Feria_Toldo", x - 1.8f, x + 1.8f, 2.35f, 2.5f, z - 1.2f, z + 1.0f, MChapa((int)x % 3), false);
                for (int i = 0; i < 6; i++) Esfera(t, "Feria_Bombilla", new Vector3(x - 1.5f + i * 0.6f, 2.25f, z - 1.15f), 0.16f, MBombilla);
                LuzPuntual(t, "Luz_Feria", new Vector3(x, 2.1f, z - 1.2f), 7f, 1.2f, new Color(1f, 0.8f, 0.5f)); n++;
            }
            // 32: postes con flechas de neon que indican cada ruta en las cuatro esquinas del cruce (rosa/cian/ambar/verde).
            int r = 0;
            foreach (var (x, z) in new[] { (302.5f, 224.5f), (341.5f, 224.5f), (302.5f, 233.5f), (341.5f, 233.5f) })
            {
                Caja(t, "Ruta_Poste", x - 0.07f, x + 0.07f, 0.1f, 3.6f, z - 0.07f, z + 0.07f, MBarrote, false);
                Caja(t, "Ruta_Flecha", x - 0.6f, x + 0.6f, 3.0f, 3.4f, z - 0.05f, z + 0.05f, NeonRuta(r), false);
                LuzPuntual(t, "Luz_Ruta", new Vector3(x, 3.2f, z - 0.6f), 6f, 1.1f, ColorRuta(r)); r++;
            }
            res.Add($"19-32 calles: barreras en chicana, autos de bloqueo, islas en la avenida, muretes y puestos frente a la plaza, flechas de ruta ({n} piezas + 4 postes)");
        }

        // 33-50: la mansion.
        static void CaminosMansion(StringBuilderLite res, Transform t)
        {
            // 33-36: marcos dorados y luces en las cuatro puertas de servicio (planta baja y 1.er piso, oeste y este).
            foreach (bool oeste in new[] { true, false })
            {
                float xp = oeste ? G.X0 : G.X1; int s = oeste ? -1 : 1; var color = oeste ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 0.7f, 0.25f);
                foreach (var pu in PuertasLaterales(oeste))
                {
                    float xa = Mathf.Min(xp, xp + s * 0.15f), xb = Mathf.Max(xp, xp + s * 0.15f);
                    Caja(t, "Servicio_Marco_Dintel", xa - 0.1f, xb + 0.1f, pu.y1, pu.y1 + 0.25f, pu.z0 - 0.25f, pu.z1 + 0.25f, MDorado, false);
                    Caja(t, "Servicio_Marco_Jamba", xa - 0.1f, xb + 0.1f, pu.y0, pu.y1, pu.z0 - 0.25f, pu.z0, MDorado, false);
                    Caja(t, "Servicio_Marco_Jamba", xa - 0.1f, xb + 0.1f, pu.y0, pu.y1, pu.z1, pu.z1 + 0.25f, MDorado, false);
                    Caja(t, "Servicio_Neon", xa - 0.12f, xb + 0.12f, pu.y1 + 0.3f, pu.y1 + 0.42f, pu.z0, pu.z1, oeste ? MNeonCian : MNeonAmbar, false);
                    LuzPuntual(t, "Luz_Servicio", new Vector3(xp + s * 1.5f, pu.y0 + 3.4f, (pu.z0 + pu.z1) * 0.5f), 9f, 1.8f, color);
                }
            }
            // 37-42: escaleras exteriores de servicio (oeste cian, este ambar): rampa de 15 m hasta el rellano del 1.er piso, con apoyos, baranda y luz.
            int idx = 0;
            foreach (bool oeste in new[] { true, false })
            {
                float xc = oeste ? 294.6f : 349.4f, s = oeste ? -1f : 1f; float yP = G.Nivel(1);
                float zIni = 247.2f, zFin = 262f;
                var color = oeste ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 0.7f, 0.25f);
                Escalera(t, oeste ? "Servicio_Escalera_O" : "Servicio_Escalera_E", new Vector3(xc, 0.05f, zIni), new Vector3(xc, yP, zFin), 2.0f);
                float xa = oeste ? xc - 1.2f : G.X1, xb = oeste ? G.X0 : xc + 1.2f;
                Elevado(t, "Servicio_Rellano", xa, xb, yP - 0.3f, yP, 262f, 266f, MAndamio);
                Caja(t, "Servicio_Rellano_Borde", xa, xb, yP, yP + 1.0f, 266f - 0.04f, 266f, MBarrote, false);
                Caja(t, "Servicio_Rellano_Borde", oeste ? xa : xb - 0.04f, oeste ? xa + 0.04f : xb, yP, yP + 1.0f, 262f, 266f, MBarrote, false);
                for (float z = zIni + 3f; z < zFin; z += 4f) Caja(t, "Servicio_Apoyo", xc - 0.06f, xc + 0.06f, 0f, Mathf.Lerp(0.05f, yP, (z - zIni) / (zFin - zIni)) - 0.3f, z - 0.06f, z + 0.06f, MBarrote, false);
                foreach (float z in new[] { 262.5f, 265.5f }) Caja(t, "Servicio_Apoyo", xc - 0.08f, xc + 0.08f, 0f, yP - 0.3f, z - 0.08f, z + 0.08f, MBarrote, false);
                LuzPuntual(t, "Luz_Servicio_Escalera", new Vector3(xc, 3.6f, 254f), 12f, 1.8f, color);
                LuzPuntual(t, "Luz_Servicio_Rellano", new Vector3(xc, yP + 2.2f, 264f), 9f, 1.8f, color);
                Caja(t, "Servicio_Cartel", xc - 0.05f, xc + 0.05f, 3.0f, 3.5f, 246.9f, 249.4f, oeste ? MNeonCian : MNeonAmbar, false); idx++;
            }
            // 43-46: recibidores interiores de las puertas de PB: alfombra, consola cobertura a un lado y arco bajo con luces, dejando un pasillo.
            foreach (bool oeste in new[] { true, false })
            {
                float xi = oeste ? G.X0 + G.Muro : G.X1 - G.Muro; int s = oeste ? 1 : -1; float zc = oeste ? 264f : 268f;
                Caja(t, "Recibidor_Alfombra", Mathf.Min(xi, xi + s * 5f), Mathf.Max(xi, xi + s * 5f), 0.17f, 0.2f, zc - 1.3f, zc + 1.3f, MAlfombra, false);
                float xa = xi + s * 3f;
                if (Libre(xa - 0.5f, xa + 0.5f, zc + 2.2f, zc + 4.2f, 0.3f, 2f))
                {
                    Caja(t, "Recibidor_Consola", xa - 0.4f, xa + 0.4f, 0.1f, 1.0f, zc + 2.4f, zc + 4.0f, MMueble);
                    Caja(t, "Recibidor_Consola_Tapa", xa - 0.45f, xa + 0.45f, 1.0f, 1.08f, zc + 2.35f, zc + 4.05f, MMarmol, false);
                }
                Esfera(t, "Recibidor_Farol", new Vector3(xi + s * 1.0f, 3.6f, zc - 1.9f), 0.35f, MBombilla); Esfera(t, "Recibidor_Farol", new Vector3(xi + s * 1.0f, 3.6f, zc + 1.9f), 0.35f, MBombilla);
                LuzPuntual(t, "Luz_Recibidor", new Vector3(xi + s * 2.5f, 4.6f, zc), 10f, 1.6f, new Color(1f, 0.85f, 0.6f));
            }
            // 47-48: planta baja con dos carriles laterales: jardineras bajas que separan la alfombra central de los pasillos de las columnas.
            foreach (var (x, z0, z1) in new[] { (312.5f, 252f, 258f), (331.5f, 252f, 258f), (312.5f, 272f, 278f), (331.5f, 272f, 278f) })
            {
                if (!Libre(x - 0.7f, x + 0.7f, z0, z1, 0.3f, 2f)) continue;
                Caja(t, "PB_Jardinera", x - 0.6f, x + 0.6f, 0.1f, 0.95f, z0, z1, MMarmolOsc);
                for (int i = 0; i < 3; i++) Esfera(t, "PB_Jardinera_Flor", new Vector3(x, 1.2f, z0 + 1f + i * 2f), 0.7f, MFlor(i + (int)z0));
            }
            // 49: balizas de ruta en el suelo del vestibulo: tira cian al oeste y ambar al este que llevan a cada puerta de servicio.
            Caja(t, "PB_Baliza_O", 297.2f, 318.5f, 0.2f, 0.23f, 263.9f, 264.1f, MNeonCian, false);
            Caja(t, "PB_Baliza_E", 325.5f, 346.8f, 0.2f, 0.23f, 267.9f, 268.1f, MNeonAmbar, false);
            // 50: cartel de ruta sobre cada rellano exterior y luz de aviso en el borde de la azotea hacia las escaleras.
            Caja(t, "Rellano_Cartel_O", 293.2f, 296f, G.Nivel(1) + 4.7f, G.Nivel(1) + 5.2f, 261.9f, 262.0f, MNeonCian, false);
            Caja(t, "Rellano_Cartel_E", 348f, 350.8f, G.Nivel(1) + 4.7f, G.Nivel(1) + 5.2f, 261.9f, 262.0f, MNeonAmbar, false);
            res.Add("33-50 mansion: 4 puertas de servicio con marco y luz, 2 escaleras exteriores a 1.er piso, recibidores, jardineras que parten el vestibulo en carriles, balizas de ruta");
        }

        // Quita arboles, autos y mobiliario de vereda que estorban a las escaleras de servicio (lados oeste y este de la mansion).
        static int DespejarServicio(Transform ciudad)
        {
            int n = 0;
            var zonas = new[] { new Bounds(new Vector3(294.5f, 4f, 256f), new Vector3(7f, 12f, 24f)), new Bounds(new Vector3(350f, 4f, 256f), new Vector3(7f, 12f, 24f)) };
            foreach (var tr in ciudad.GetComponentsInChildren<Transform>(false))
            {
                if (tr == null || !tr.gameObject.activeSelf) continue;
                string nm = tr.name;
                if (!(nm.StartsWith("Arbol_Vereda") || nm.StartsWith("Auto_") || nm.StartsWith("Hidrante") || nm == "Buzon")) continue;
                foreach (var z in zonas) if (z.Contains(new Vector3(tr.position.x, 4f, tr.position.z))) { tr.gameObject.SetActive(false); n++; break; }
            }
            return n;
        }

        public static string ConstruirCaminos()
        {
            var res = new StringBuilderLite();
            var esc = SceneManager.GetSceneByPath(RutaEscenaDeSector("Ciudad"));
            if (!esc.IsValid() || !esc.isLoaded) return "La escena Ciudad no esta abierta (aisl.sh Completa).";
            Transform ciudad = null; foreach (var r in esc.GetRootGameObjects()) if (r.name == "Ciudad") ciudad = r.transform;
            if (ciudad == null) return "Sin raiz Ciudad.";
            var viejo = ciudad.Find(GrupoCaminos); if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var grupo = new GameObject(GrupoCaminos).transform; grupo.SetParent(ciudad, false);
            AsegurarCarpeta(CarpetaElementos);
            res.Add("despejados para las escaleras de servicio: " + DespejarServicio(ciudad));

            void Instanciar(GameObject escenaria, string nombrePrefab)
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(escenaria, CarpetaElementos + "/" + nombrePrefab + ".prefab");
                Object.DestroyImmediate(escenaria);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, esc);
                inst.transform.SetParent(grupo, false); inst.transform.position = Vector3.zero;
            }
            var a = new GameObject("P_Op_Cam_Callejones"); CaminosCallejones(res, a.transform); Instanciar(a, "P_Op_Cam_Callejones");
            var b = new GameObject("P_Op_Cam_Calles"); CaminosCalles(res, b.transform); Instanciar(b, "P_Op_Cam_Calles");
            var c = new GameObject("P_Op_Cam_Mansion"); CaminosMansion(res, c.transform); Instanciar(c, "P_Op_Cam_Mansion");
            EditorSceneManager.MarkSceneDirty(esc);
            AssetDatabase.SaveAssets();
            return res.ToString();
        }
    }
}
