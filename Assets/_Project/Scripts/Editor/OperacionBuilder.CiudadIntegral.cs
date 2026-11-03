using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

namespace SP.EditorTools
{
    // Rediseno integral de la ciudad: las cuatro manzanas entre el anillo de calles y la cruz estaban vacias (arena). Se llenan de edificios de
    // distinta altura con calles de servicio entre ellos, veredas, arboles, autos y mobiliario; la plaza deja de ser un helipuerto y pasa a ser una
    // plaza civica (fuente central, bancos, arboles, monumento). Cada edificio y cada conjunto es UN prefab (Prefabs/Escena/Elementos/P_Op_Ciudad_*).
    public static partial class OperacionBuilder
    {
        const string GrupoCiudadIntegral = "10_CiudadIntegral";

        [MenuItem("Strategic Point/Arte/Ciudad/Rediseno integral de la ciudad")]
        static void CiudadIntegralMenu() => Debug.Log(ConstruirCiudadIntegral());

        static readonly Color[] PaletaEdificios =
        {
            new Color(0.72f, 0.62f, 0.50f), new Color(0.58f, 0.60f, 0.64f), new Color(0.68f, 0.50f, 0.42f),
            new Color(0.50f, 0.56f, 0.52f), new Color(0.78f, 0.74f, 0.66f), new Color(0.44f, 0.47f, 0.55f),
        };

        static Material MVereda => Mat("Ciudad_Vereda", new Color(0.55f, 0.54f, 0.52f));
        static Material MAdoquinA => Mat("Ciudad_AdoquinA", new Color(0.62f, 0.60f, 0.56f));
        static Material MAdoquinB => Mat("Ciudad_AdoquinB", new Color(0.50f, 0.48f, 0.45f));
        static Material MAgua => Mat("Ciudad_Agua", new Color(0.18f, 0.38f, 0.55f), 0.4f);
        static Material MCopa => Mat("Ciudad_Copa", new Color(0.20f, 0.38f, 0.16f));
        static Material MTronco => Mat("Ciudad_Tronco", new Color(0.30f, 0.20f, 0.12f));
        static Material MVidrioOscuro => Mat("Ciudad_VidrioOscuro", new Color(0.10f, 0.14f, 0.18f));
        static Material MChapa(int i) => Mat("Ciudad_Chapa" + i, i == 0 ? new Color(0.55f, 0.16f, 0.14f) : i == 1 ? new Color(0.20f, 0.30f, 0.45f) : new Color(0.62f, 0.60f, 0.52f));

        // Manzanas (x0, x1, z0, z1): la cruz y el anillo de calles quedan libres; la mansion ocupa x 300..344, z 248..272.
        static readonly (float x0, float x1, float z0, float z1)[] Manzanas =
        {
            (272f, 291f, 238f, 290f), (353f, 374f, 238f, 290f), (272f, 302f, 172f, 220f), (342f, 374f, 172f, 220f),
        };

        static GameObject ArmarEdificio(string nombre, float cx, float cz, float w, float d, float h, Color color, int semilla)
        {
            var rnd = new System.Random(semilla);
            var raiz = new GameObject(nombre);
            var t = raiz.transform;
            var muro = Mat("Ciudad_Edif_" + Mathf.RoundToInt(color.r * 100) + "_" + Mathf.RoundToInt(color.g * 100) + "_" + Mathf.RoundToInt(color.b * 100), color);
            var trim = Mat("Ciudad_EdifTrim", new Color(0.82f, 0.80f, 0.74f));
            float x0 = cx - w / 2, x1 = cx + w / 2, z0 = cz - d / 2, z1 = cz + d / 2;
            Caja(t, "Cuerpo", x0, x1, 0f, h, z0, z1, muro);
            // Zocalo y cornisa.
            Caja(t, "Zocalo", x0 - 0.15f, x1 + 0.15f, 0f, 0.9f, z0 - 0.15f, z1 + 0.15f, trim, false);
            Caja(t, "Cornisa", x0 - 0.35f, x1 + 0.35f, h - 0.5f, h + 0.25f, z0 - 0.35f, z1 + 0.35f, trim, false);
            // Parapeto del techo.
            Caja(t, "Parapeto_S", x0, x1, h + 0.25f, h + 1.0f, z0, z0 + 0.25f, trim, false);
            Caja(t, "Parapeto_N", x0, x1, h + 0.25f, h + 1.0f, z1 - 0.25f, z1, trim, false);
            Caja(t, "Parapeto_O", x0, x0 + 0.25f, h + 0.25f, h + 1.0f, z0, z1, trim, false);
            Caja(t, "Parapeto_E", x1 - 0.25f, x1, h + 0.25f, h + 1.0f, z0, z1, trim, false);
            int pisos = Mathf.Max(2, Mathf.FloorToInt(h / 3.6f));
            for (int f = 0; f < pisos; f++)
            {
                float yc = f == 0 ? 1.7f : 1.7f + f * 3.6f;
                if (yc + 0.8f > h - 0.7f) break;
                bool tienda = f == 0;
                for (int cara = 0; cara < 4; cara++)
                {
                    bool encendida = rnd.NextDouble() < 0.35;
                    var mv = tienda ? MVidrioOscuro : encendida ? Ventana : VentanaApagada;
                    float alto = tienda ? 2.3f : 1.5f;
                    if (cara == 0) Caja(t, "Ventanal_S", x0 + 1.2f, x1 - 1.2f, yc - alto / 2, yc + alto / 2, z0 - 0.1f, z0 + 0.02f, mv, false);
                    else if (cara == 1) Caja(t, "Ventanal_N", x0 + 1.2f, x1 - 1.2f, yc - alto / 2, yc + alto / 2, z1 - 0.02f, z1 + 0.1f, mv, false);
                    else if (cara == 2) Caja(t, "Ventanal_O", x0 - 0.1f, x0 + 0.02f, yc - alto / 2, yc + alto / 2, z0 + 1.2f, z1 - 1.2f, mv, false);
                    else Caja(t, "Ventanal_E", x1 - 0.02f, x1 + 0.1f, yc - alto / 2, yc + alto / 2, z0 + 1.2f, z1 - 1.2f, mv, false);
                }
                // Pilastras que cortan el ventanal en tramos.
                if (!tienda) for (float px = x0 + 3.2f; px < x1 - 2f; px += 3.4f) { Caja(t, "Pilastra_S", px - 0.18f, px + 0.18f, yc - 0.9f, yc + 0.9f, z0 - 0.14f, z0 + 0.02f, trim, false); Caja(t, "Pilastra_N", px - 0.18f, px + 0.18f, yc - 0.9f, yc + 0.9f, z1 - 0.02f, z1 + 0.14f, trim, false); }
            }
            // Toldo de la tienda y equipos del techo.
            Caja(t, "Toldo", x0 + 1.5f, x1 - 1.5f, 2.9f, 3.15f, z0 - 1.1f, z0, MChapa(rnd.Next(0, 3)), false);
            // Iter 18: letrero de neon sobre el toldo, segundo toldo al norte y tanque de agua en varios techos.
            {
                var neon = Mat("Ciudad_Neon" + (semilla % 4), semilla % 4 == 0 ? new Color(1f, 0.25f, 0.35f) : semilla % 4 == 1 ? new Color(0.25f, 0.85f, 1f) : semilla % 4 == 2 ? new Color(1f, 0.8f, 0.2f) : new Color(0.5f, 1f, 0.5f), 2.5f);
                float nx = cx + (float)(rnd.NextDouble() - 0.5) * w * 0.3f;
                Caja(t, "Letrero_Neon", nx - w * 0.18f, nx + w * 0.18f, 3.45f, 4.15f, z0 - 0.3f, z0 - 0.15f, neon, false);
                Caja(t, "Letrero_Marco", nx - w * 0.18f - 0.1f, nx + w * 0.18f + 0.1f, 3.35f, 4.25f, z0 - 0.15f, z0 - 0.05f, MetalOsc, false);
                Caja(t, "Toldo_N", x0 + 1.5f, x1 - 1.5f, 2.9f, 3.15f, z1, z1 + 1.1f, MChapa(rnd.Next(0, 3)), false);
                if (rnd.NextDouble() < 0.5)
                {
                    float tx = cx - w * 0.3f, tz = cz + d * 0.2f;
                    var cil = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cil.name = "Techo_Tanque"; cil.transform.SetParent(t, false);
                    cil.transform.position = new Vector3(tx, h + 1.9f, tz); cil.transform.localScale = new Vector3(2.2f, 0.9f, 2.2f);
                    cil.GetComponent<MeshRenderer>().sharedMaterial = trim; Object.DestroyImmediate(cil.GetComponent<Collider>()); cil.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                    Caja(t, "Techo_Tanque_Patas", tx - 0.9f, tx + 0.9f, h + 0.25f, h + 1.0f, tz - 0.9f, tz + 0.9f, MetalOsc, false);
                }
            }
            Caja(t, "Techo_Equipo_A", cx - w * 0.25f - 1.2f, cx - w * 0.25f + 1.2f, h + 0.25f, h + 1.6f, cz - 1f, cz + 1f, MetalOsc, false);
            if (rnd.NextDouble() < 0.6) Caja(t, "Techo_Caseta", cx + w * 0.2f - 1.5f, cx + w * 0.2f + 1.5f, h + 0.25f, h + 2.6f, cz - 1.5f, cz + 1.5f, trim, false);
            return raiz;
        }

        static GameObject ArmarArbol(Transform p, string n, float x, float z, float escala)
        {
            var r = new GameObject(n); r.transform.SetParent(p, false); r.transform.position = new Vector3(x, 0f, z);
            var tr = GameObject.CreatePrimitive(PrimitiveType.Cylinder); tr.name = "Tronco"; tr.transform.SetParent(r.transform, false);
            tr.transform.localPosition = new Vector3(0f, 1.6f * escala, 0f); tr.transform.localScale = new Vector3(0.4f * escala, 1.6f * escala, 0.4f * escala);
            tr.GetComponent<MeshRenderer>().sharedMaterial = MTronco;
            var co = GameObject.CreatePrimitive(PrimitiveType.Sphere); co.name = "Copa"; co.transform.SetParent(r.transform, false);
            co.transform.localPosition = new Vector3(0f, 4.2f * escala, 0f); co.transform.localScale = new Vector3(3.2f * escala, 2.8f * escala, 3.2f * escala);
            co.GetComponent<MeshRenderer>().sharedMaterial = MCopa;
            Object.DestroyImmediate(co.GetComponent<Collider>());
            co.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return r;
        }

        static GameObject ArmarAuto(Transform p, string n, float x, float z, float yaw, Material chapa)
        {
            var r = new GameObject(n); r.transform.SetParent(p, false); r.transform.position = new Vector3(x, 0f, z); r.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            void Pieza2(string nn, Vector3 c, Vector3 s, Material m, bool col)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = nn; g.transform.SetParent(r.transform, false);
                g.transform.localPosition = c; g.transform.localScale = s; g.GetComponent<MeshRenderer>().sharedMaterial = m;
                if (!col) { Object.DestroyImmediate(g.GetComponent<BoxCollider>()); g.AddComponent<NavMeshModifier>().ignoreFromBuild = true; }
            }
            Pieza2("Carroceria", new Vector3(0f, 0.7f, 0f), new Vector3(1.9f, 0.9f, 4.4f), chapa, true);
            Pieza2("Cabina", new Vector3(0f, 1.45f, -0.2f), new Vector3(1.7f, 0.65f, 2.2f), MVidrioOscuro, false);
            return r;
        }

        static GameObject ArmarPlazaCivica()
        {
            var raiz = new GameObject("P_Op_Ciudad_PlazaCivica"); var t = raiz.transform;
            const float cx = 322f, cz = 228f;
            // Adoquines: tablero de losas claras y oscuras, apoyado sobre la pintura vieja de la plaza (H y anillo amarillo, ya fusionados en las calles) para taparla; sin collider.
            for (int i = -5; i < 5; i++)
                for (int j = -5; j < 5; j++)
                    Caja(t, "Adoquin", cx + i * 3.2f, cx + i * 3.2f + 3.2f, 0.17f, 0.23f, cz + j * 3.2f, cz + j * 3.2f + 3.2f, ((i + j) & 1) == 0 ? MAdoquinA : MAdoquinB, false);
            // El centro de la plaza (322, 228) es el puesto de la defensa: queda libre, con un rosetón de piedra al ras. La fuente va al sur, lejos del camino al porton.
            Caja(t, "Roseton_Exterior", cx - 3.4f, cx + 3.4f, 0.23f, 0.27f, cz - 3.4f, cz + 3.4f, MPilar, false);
            Caja(t, "Roseton_Interior", cx - 2.2f, cx + 2.2f, 0.27f, 0.30f, cz - 2.2f, cz + 2.2f, MAdoquinB, false);
            const float fz = cz - 12.5f;
            var cuenco = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cuenco.name = "Fuente_Cuenco"; cuenco.transform.SetParent(t, false);
            cuenco.transform.position = new Vector3(cx, 0.45f, fz); cuenco.transform.localScale = new Vector3(6f, 0.45f, 6f); cuenco.GetComponent<MeshRenderer>().sharedMaterial = MPilar;
            var agua = GameObject.CreatePrimitive(PrimitiveType.Cylinder); agua.name = "Fuente_Agua"; agua.transform.SetParent(t, false);
            agua.transform.position = new Vector3(cx, 0.88f, fz); agua.transform.localScale = new Vector3(5.2f, 0.02f, 5.2f); agua.GetComponent<MeshRenderer>().sharedMaterial = MAgua;
            Object.DestroyImmediate(agua.GetComponent<Collider>()); agua.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            Caja(t, "Fuente_Columna", cx - 0.5f, cx + 0.5f, 0.9f, 3.4f, fz - 0.5f, fz + 0.5f, MPilar);
            Caja(t, "Fuente_Copa", cx - 1.2f, cx + 1.2f, 3.2f, 3.5f, fz - 1.2f, fz + 1.2f, MPilar, false);
            // Bancos a los lados de la fuente (el punto de la plaza que usa la defensa queda libre) y jardineras en las esquinas.
            foreach (var (bx, bz, giro) in new[] { (cx - 5.5f, fz, true), (cx + 5.5f, fz, true), (cx - 9.5f, cz - 9.5f, false), (cx + 9.5f, cz - 9.5f, false) })
            {
                if (giro) Caja(t, "Banco", bx - 0.4f, bx + 0.4f, 0f, 0.5f, bz - 1.6f, bz + 1.6f, MMueble);
                else Caja(t, "Banco", bx - 1.6f, bx + 1.6f, 0f, 0.5f, bz - 0.4f, bz + 0.4f, MMueble);
            }
            foreach (var (jx, jz) in new[] { (cx - 12.5f, cz - 12.5f), (cx + 12.5f, cz - 12.5f), (cx - 12.5f, cz + 12.5f), (cx + 12.5f, cz + 12.5f) })
            {
                Caja(t, "Jardinera", jx - 1.6f, jx + 1.6f, 0f, 0.8f, jz - 1.6f, jz + 1.6f, MPilar);
                ArmarArbol(t, "Arbol_Jardinera", jx, jz, 0.8f);
            }
            // Arboles en las esquinas exteriores de la plaza.
            foreach (var (ax, az) in new[] { (cx - 15f, cz - 6f), (cx - 15f, cz + 6f), (cx + 15f, cz - 6f), (cx + 15f, cz + 6f), (cx - 6f, cz - 15f), (cx + 6f, cz - 15f) })
                ArmarArbol(t, "Arbol_Plaza", ax, az, 1f);
            return raiz;
        }

        static GameObject ArmarMobiliario()
        {
            var raiz = new GameObject("P_Op_Ciudad_Mobiliario"); var t = raiz.transform;
            var rnd = new System.Random(77);
            // Autos estacionados en las veredas de las cuatro manzanas (frente a la calle de servicio).
            int k = 0;
            foreach (var m in Manzanas)
            {
                for (float z = m.z0 + 6f; z < m.z1 - 4f; z += 14f)
                {
                    float x = (m.x0 + m.x1) * 0.5f < 322f ? m.x1 + 2.6f : m.x0 - 2.6f;
                    if (Mathf.Abs(z - 230f) < 10f) continue;
                    ArmarAuto(t, "Auto_" + (++k), x, z, 90f + (rnd.Next(0, 2) == 0 ? 0f : 180f), MChapa(rnd.Next(0, 3)));
                }
            }
            // Arboles de vereda a lo largo del anillo, contra las manzanas.
            foreach (var m in Manzanas)
                for (float z = m.z0 + 2f; z < m.z1; z += 9f)
                {
                    bool oeste = (m.x0 + m.x1) * 0.5f < 322f;
                    ArmarArbol(t, "Arbol_Vereda", oeste ? m.x0 - 2.5f : m.x1 + 2.5f, z, 0.9f + (float)rnd.NextDouble() * 0.3f);
                }
            // Refugios de colectivo y cestos de basura en la cruz.
            foreach (var (bx, bz) in new[] { (296f, 241f), (348f, 217f) })
            {
                Caja(t, "Refugio_Techo", bx - 1.6f, bx + 1.6f, 2.4f, 2.55f, bz - 0.6f, bz + 0.6f, MChapa(1), false);
                Caja(t, "Refugio_Fondo", bx - 1.6f, bx + 1.6f, 0f, 2.4f, bz + 0.5f, bz + 0.6f, MVidrioOscuro, true);
                Caja(t, "Refugio_Banco", bx - 1.0f, bx + 1.0f, 0f, 0.5f, bz - 0.1f, bz + 0.3f, MMueble);
            }
            // Iter 19: hidrantes y buzones sobre las veredas de las manzanas, y semaforos en las esquinas de la cruz.
            foreach (var m in Manzanas)
            {
                bool oeste = (m.x0 + m.x1) * 0.5f < 322f;
                float bx = oeste ? m.x1 + 1.2f : m.x0 - 1.2f;
                foreach (float bz in new[] { m.z0 + 3f, (m.z0 + m.z1) * 0.5f + 5f, m.z1 - 3f })
                {
                    var h = GameObject.CreatePrimitive(PrimitiveType.Cylinder); h.name = "Hidrante"; h.transform.SetParent(t, false);
                    h.transform.position = new Vector3(bx, 0.45f, bz); h.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
                    h.GetComponent<MeshRenderer>().sharedMaterial = MChapa(0); Object.DestroyImmediate(h.GetComponent<Collider>()); h.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                }
                Caja(t, "Buzon", bx - 0.3f, bx + 0.3f, 0f, 1.1f, m.z0 + 7f - 0.3f, m.z0 + 7f + 0.3f, MChapa(1), false);
            }
            foreach (var (sx, sz) in new[] { (303f, 233f), (341f, 233f), (303f, 223f), (341f, 223f) })
            {
                Caja(t, "Semaforo_Poste", sx - 0.08f, sx + 0.08f, 0f, 4.2f, sz - 0.08f, sz + 0.08f, MetalOsc, false);
                Caja(t, "Semaforo_Caja", sx - 0.22f, sx + 0.22f, 3.4f, 4.5f, sz - 0.2f, sz + 0.2f, MBarrote, false);
                Caja(t, "Semaforo_Rojo", sx - 0.12f, sx + 0.12f, 4.05f, 4.3f, sz - 0.22f, sz - 0.2f, Mat("Ciudad_SemRojo", new Color(1f, 0.1f, 0.1f), 2.5f), false);
                Caja(t, "Semaforo_Verde", sx - 0.12f, sx + 0.12f, 3.5f, 3.75f, sz - 0.22f, sz - 0.2f, Mat("Ciudad_SemVerde", new Color(0.1f, 1f, 0.3f), 2.5f), false);
            }
            foreach (var (cx2, cz2) in new[] { (300f, 232f), (344f, 224f), (300f, 190f), (346f, 205f) })
                Caja(t, "Contenedor", cx2 - 1.2f, cx2 + 1.2f, 0f, 1.3f, cz2 - 0.7f, cz2 + 0.7f, MChapa(0));
            return raiz;
        }

        public static string ConstruirCiudadIntegral()
        {
            var res = new System.Text.StringBuilder();
            var esc = SceneManager.GetSceneByPath(RutaEscenaDeSector("Ciudad"));
            if (!esc.IsValid() || !esc.isLoaded) return "La escena Ciudad no esta abierta (aisl.sh Completa).";
            Transform ciudad = null; foreach (var r in esc.GetRootGameObjects()) if (r.name == "Ciudad") ciudad = r.transform;
            if (ciudad == null) return "Sin raiz Ciudad.";
            var viejo = ciudad.Find(GrupoCiudadIntegral); if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var grupo = new GameObject(GrupoCiudadIntegral).transform; grupo.SetParent(ciudad, false);
            AsegurarCarpeta(CarpetaElementos);

            // 1) fuera el helipuerto de la plaza: la losa con la H y las luces. El Transform "Plaza" se queda (el director lo usa de centro).
            int apagados = 0;
            // La losa y las luces viven en SC_Operacion (6_Ciudad), no en la escena Ciudad: se barren todas las escenas abiertas.
            for (int k = 0; k < SceneManager.sceneCount; k++)
            {
                var sc = SceneManager.GetSceneAt(k);
                foreach (var raiz in sc.GetRootGameObjects())
                    foreach (var tr in raiz.GetComponentsInChildren<Transform>(true))
                    {
                        if ((tr.name == "LucesDelHelipuerto" || tr.name == "Helipad") && tr.gameObject.activeSelf) { tr.gameObject.SetActive(false); apagados++; EditorSceneManager.MarkSceneDirty(sc); }
                        if (tr.name == "Plaza" && tr.parent != null && tr.parent.name == "6_Ciudad")
                            foreach (var rr in tr.GetComponentsInChildren<Renderer>(true)) if (rr.enabled) { rr.enabled = false; apagados++; EditorSceneManager.MarkSceneDirty(sc); }
                    }
            }
            res.AppendLine("helipuerto de la plaza quitado (" + apagados + ")");

            void Instanciar(GameObject escenaria, string nombrePrefab)
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(escenaria, CarpetaElementos + "/" + nombrePrefab + ".prefab");
                Object.DestroyImmediate(escenaria);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, esc);
                inst.transform.SetParent(grupo, false); inst.transform.position = Vector3.zero;
            }

            // Despeja lo suelto que quedaba dentro de las manzanas (sacos, escombros, postes).
            int despejados = 0;
            foreach (var m in Manzanas)
            {
                var caja = new Bounds(new Vector3((m.x0 + m.x1) * 0.5f, 5f, (m.z0 + m.z1) * 0.5f), new Vector3(m.x1 - m.x0 + 7f, 30f, m.z1 - m.z0 + 6f));
                foreach (var rr in ciudad.GetComponentsInChildren<Renderer>(false))
                {
                    if (rr == null || !rr.gameObject.activeInHierarchy || !caja.Contains(rr.bounds.center)) continue;
                    bool excluir = false; Transform grp = null;
                    for (var q = rr.transform; q != null && q != ciudad; q = q.parent)
                    {
                        if (q.name == GrupoCiudadIntegral || q.name == "Calles" || q.name == "Luces" || q.name == "Terreno_Ambiente" || q.name == "Ground" || q.name == "0_Perimetro" || q.name == "8_Mansion" || q.name == "Helicoptero_Extraccion") excluir = true;
                        if (grp == null && q.name.StartsWith("GRP_")) grp = q;
                    }
                    if (excluir) continue;
                    (grp != null ? grp : rr.transform).gameObject.SetActive(false); despejados++;
                }
            }
            res.AppendLine("objetos despejados en manzanas: " + despejados);

            // 2) veredas de las manzanas + edificios.
            int n = 0; var rnd = new System.Random(2026);
            foreach (var m in Manzanas)
            {
                var v = new GameObject("Vereda"); Caja(v.transform, "Vereda", m.x0 - 3.5f, m.x1 + 3.5f, 0.005f, 0.12f, m.z0 - 3f, m.z1 + 3f, MVereda, false);
                Instanciar(v, "P_Op_Ciudad_Vereda_" + (++n));
            }
            int ed = 0;
            foreach (var m in Manzanas)
            {
                float ancho = m.x1 - m.x0, largo = m.z1 - m.z0;
                int filas = largo > 50f ? 3 : 3; float alto = (largo - 4f * (filas - 1)) / filas;
                for (int i = 0; i < filas; i++)
                {
                    float z0 = m.z0 + i * (alto + 4f);
                    // En manzanas anchas (>= 30 m) los edificios se parten en dos columnas con callejon de 4 m.
                    int cols = ancho >= 30f ? 2 : 1; float anc = (ancho - 4f * (cols - 1)) / cols;
                    for (int c = 0; c < cols; c++)
                    {
                        float x0 = m.x0 + c * (anc + 4f);
                        float h = 10f + (float)rnd.NextDouble() * 18f;
                        var e = ArmarEdificio("P_Op_Ciudad_Edificio_" + (++ed), x0 + anc / 2, z0 + alto / 2, anc, alto, h, PaletaEdificios[rnd.Next(PaletaEdificios.Length)], 100 + ed);
                        Instanciar(e, "P_Op_Ciudad_Edificio_" + ed);
                    }
                }
            }
            res.AppendLine("edificios: " + ed);
            Instanciar(ArmarPlazaCivica(), "P_Op_Ciudad_PlazaCivica");
            Instanciar(ArmarMobiliario(), "P_Op_Ciudad_Mobiliario");
            EditorSceneManager.MarkSceneDirty(esc);
            return res.ToString();
        }
    }
}
