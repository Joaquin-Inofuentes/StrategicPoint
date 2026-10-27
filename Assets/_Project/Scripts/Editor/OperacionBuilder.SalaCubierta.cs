using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Presentation;

namespace SP.EditorTools
{
    public static partial class OperacionBuilder
    {
        // Bugs #289/#290 ("falta el techo, debe estar cubierto, baja el brillo a las barras de los muros" / "dale mas altura"): la sala del centro
        // de datos (x -26..26, z -80..-40) estaba a cielo abierto con muros de 5 m. Retoque EN SITIO e idempotente de SC_Operacion (nunca reconstruye):
        //   * los 8 muros CD_* pasan de 5 a 8 m; lo que cuelga arriba (pilares, losas y vallas del arte de operacion) sube con ellos;
        //   * rellenos sobre la entrada sur y las dos puertas laterales (las aberturas llegaban a 5 m) y un dintel sobre la salida norte;
        //   * Techo_CD cerrado (con collider, fuera de la malla de navegacion) con TechoOcultable para que la vista tactica no lo vea;
        //   * remates (mastiles, antenas, equipos de aire) y rotulo suben con el techo; luces interiores a 7,2 m y dos mas;
        //   * barras y LEDs con la emision bajada (materiales "Tenue" nuevos: Mat() cachea por nombre y LedCian se usa fuera de la sala).
        // El arte de Servidores (bandejas, ductos, paneles, cables) sale de ServidoresEscenaAbierta(), que ya lo genera a 7,2-7,6 m.
        public const float AlturaDeLaSala = 8f;
        const string GrupoSalaCubierta = "9_SalaCubierta";

        static readonly string[] MurosDeLaSala = { "CD_Sur_O", "CD_Sur_E", "CD_Norte_O", "CD_Norte_E", "CD_Oeste_S", "CD_Oeste_N", "CD_Este_S", "CD_Este_N" };

        static GameObject CuboSolido(Transform padre, string nombre, Vector3 centro, Vector3 tam, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre; go.transform.SetParent(padre, false);
            go.transform.position = centro; go.transform.localScale = tam;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            go.isStatic = true;
            return go;
        }

        public static string SalaCubiertaEscenaAbierta()
        {
            var sb = new StringBuilder();
            var raizCd = GameObject.Find("3_CentroDeDatos");
            if (raizCd == null) return "Falta 3_CentroDeDatos";
            const float H = AlturaDeLaSala;

            // 1. muros de la sala a 8 m
            int muros = 0;
            foreach (var t in raizCd.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(MurosDeLaSala, t.name) < 0) continue;
                var s = t.localScale; var p = t.position;
                t.localScale = new Vector3(s.x, H, s.z);
                t.position = new Vector3(p.x, H * 0.5f, p.z);
                EditorUtility.SetDirty(t); muros++;
            }
            Physics.SyncTransforms();
            sb.Append("muros=").Append(muros).Append(' ');

            // 2. arte de operacion sobre el perimetro de la sala: pilares a 8 m, losas y vallas +3 m (absoluto: no se vuelve a mover si ya estan arriba)
            int pilares = 0, tapas = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                var b = r.bounds; var c = b.center;
                if (Mathf.Abs(c.x) > 27f || c.z < -81f || c.z > -39f) continue;
                bool perimetro = Mathf.Abs(c.x) > 25f || c.z < -79f || c.z > -41f;
                if (!perimetro) continue;
                string n = r.name;
                if (n == "SM_Mod_Muro_Pilar" && b.max.y < H - 0.1f && b.size.y > 4.5f)
                {
                    float f = (H - b.min.y) / b.size.y;
                    var t = r.transform; var sc = t.localScale; t.localScale = new Vector3(sc.x, sc.y * f, sc.z);
                    float dy = b.min.y - r.bounds.min.y; t.position += Vector3.up * dy;
                    EditorUtility.SetDirty(t); pilares++;
                }
                else if (n == "SM_Mod_Losa" && c.y < 6f) { r.transform.position += Vector3.up * 3f; EditorUtility.SetDirty(r.transform); tapas++; }
                else if (n == "SM_Mod_Valla_Puas" && c.y < 6.5f) { r.transform.position += Vector3.up * 3f; EditorUtility.SetDirty(r.transform); tapas++; }
            }
            sb.Append("pilares=").Append(pilares).Append(" tapas=").Append(tapas).Append(' ');

            // 3. grupo propio: rellenos sobre las aberturas, techo y luces
            var viejo = GameObject.Find(GrupoSalaCubierta);
            if (viejo != null) Object.DestroyImmediate(viejo);
            var g = new GameObject(GrupoSalaCubierta).transform;
            g.SetParent(raizCd.transform, false);
            var pared = Datos;
            CuboSolido(g, "Relleno_Entrada_Sur", new Vector3(0f, 6.4f, -80f), new Vector3(8.8f, H - 4.8f, 1.5f), pared, true);
            CuboSolido(g, "Relleno_Lateral_O", new Vector3(-26f, 6.4f, -59f), new Vector3(1.5f, H - 4.8f, 6.1f), pared, true);
            CuboSolido(g, "Relleno_Lateral_E", new Vector3(26f, 6.4f, -59f), new Vector3(1.5f, H - 4.8f, 6.1f), pared, true);
            CuboSolido(g, "Dintel_Salida_Norte", new Vector3(0f, 6.5f, -40f), new Vector3(8f, H - 5f, 1.5f), pared, true);

            var techo = CuboSolido(g, "Techo_CD", new Vector3(0f, H + 0.15f, -60f), new Vector3(53.5f, 0.3f, 41.5f), Mat("TechoSala", new Color(0.2f, 0.22f, 0.26f)), true);
            techo.isStatic = false;
            var mod = techo.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true;
            techo.AddComponent<TechoOcultable>();
            techo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // 4. luces interiores bajo el techo (las dos de antes suben a 7,2 m y se agregan dos mas)
            var est = raizCd.transform.Find("Estructuras");
            int luces = 0;
            foreach (var l in raizCd.GetComponentsInChildren<Light>(true))
            {
                if (l.name != "Luz_CD_1" && l.name != "Luz_CD_2") continue;
                l.transform.position = new Vector3(l.transform.position.x, 7.2f, l.transform.position.z);
                l.range = 26f; l.intensity = 4.2f; EditorUtility.SetDirty(l); luces++;
            }
            if (est != null)
            {
                LuzPuntual(g, "Luz_CD_3", 0f, 7.2f, -70f, new Color(0.7f, 0.9f, 1f), 4.2f, 26f);
                LuzPuntual(g, "Luz_CD_4", 0f, 7.2f, -46f, new Color(0.7f, 0.9f, 1f), 4.2f, 26f);
            }
            sb.Append("luces=").Append(luces + 2).Append(' ');

            // 5. pulido final de la sala: remates arriba del techo, barras de los muros mas suaves, LEDs de los racks mas suaves
            var pf = GameObject.Find("9_PulidoFinal");
            int remates = 0, barras = 0, leds = 0;
            if (pf != null)
            {
                var cd = pf.transform.Find("CentroDeDatos");
                if (cd != null)
                {
                    var tenue = Mat("LineaLuzTenue", new Color(0.2f, 0.8f, 1f), 0.6f);
                    var ledV = Mat("LedVerdeTenue", new Color(0.2f, 1f, 0.4f), 1.1f); var ledC = Mat("LedCianTenue", new Color(0.2f, 0.8f, 1f), 1.1f); var ledA = Mat("LedAmbarTenue", new Color(1f, 0.65f, 0.1f), 1.1f);
                    foreach (var t in cd.GetComponentsInChildren<Transform>(true))
                    {
                        var p = t.position;
                        if (t.parent != null && t.parent.name == "Remates")
                        {
                            float y = t.name == "Mastil" ? 11.2f : t.name == "Mastil_Luz" ? 13.5f : t.name == "Antena_Plato" ? 10.4f : t.name == "Equipo_Aire" ? 8.75f : p.y;
                            if (!Mathf.Approximately(y, p.y)) { t.position = new Vector3(p.x, y, p.z); EditorUtility.SetDirty(t); remates++; }
                        }
                        else if (t.name == "LineaLuz")
                        {
                            t.position = new Vector3(p.x, 3.2f, p.z);
                            var mr = t.GetComponent<MeshRenderer>(); if (mr != null) mr.sharedMaterial = tenue;
                            EditorUtility.SetDirty(t); barras++;
                        }
                        else if (t.name == "Led" && t.parent != null && t.parent.name == "Racks")
                        {
                            var mr = t.GetComponent<MeshRenderer>();
                            if (mr == null || mr.sharedMaterial == null) continue;
                            string mn = mr.sharedMaterial.name;
                            if (mn.EndsWith("LedVerde")) mr.sharedMaterial = ledV; else if (mn.EndsWith("LedCian")) mr.sharedMaterial = ledC; else if (mn.EndsWith("LedAmbar")) mr.sharedMaterial = ledA; else continue;
                            EditorUtility.SetDirty(mr); leds++;
                        }
                    }
                }
            }
            sb.Append("remates=").Append(remates).Append(" barras=").Append(barras).Append(" ledsRack=").Append(leds).Append(' ');

            // 6. rotulo del frente a la altura nueva
            foreach (var tm in raizCd.GetComponentsInChildren<TextMesh>(true))
                if (tm.transform.parent != null && tm.transform.parent.GetComponent<TextMesh>() != null) continue;
                else if (tm.text == "CENTRO DE DATOS") { var p = tm.transform.position; tm.transform.position = new Vector3(p.x, 6.6f, p.z); EditorUtility.SetDirty(tm.transform); }
                else if (tm.text == "SALIDA NORTE · DEMOLER") { tm.text = "SALIDA NORTE"; foreach (var h in tm.GetComponentsInChildren<TextMesh>(true)) h.text = "SALIDA NORTE"; EditorUtility.SetDirty(tm); }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return sb.ToString();
        }
    }
}
