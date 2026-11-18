using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Ronda 13 · T14: ambientacion de desierto con presupuesto de rendimiento (referencia: el nivel 1, que usa arboles de Terrain).
    //   - Terreno_ArbolesDesierto: un Terrain plano SOLO de arboles (drawHeightmap = false, sin TerrainCollider, con NavMeshModifier.ignoreFromBuild),
    //     entre 600 y 1500 instancias de P_Env_ArbolA/B y arbustos, fuera de 24 m de la polilinea RutaTanque, de la ciudad (x >= 190, z >= 100) y del rio.
    //   - R13_Desierto: rocas y cactus en los huecos del bucle de la ruta (LugarLibre, a 24..70 m de la ruta).
    //   - Los ArbustoRodante (pool de 10) y el PolvoDelDesierto son SCRIPTS DE JUEGO del Carril D, creados por OperacionDirector.AmbientarLaRuta: aqui
    //     solo se informa si existen. El presupuesto (triangulos visibles estimados) recorta la cantidad de arboles si hace falta.
    // Va en la escena Ruta. NO volver a correr Ambiente(): Terreno_Ambiente esta en las 4 escenas.
    public static partial class OperacionBuilder
    {
        const float TerrenoR13X0 = -390f, TerrenoR13Z0 = 43f, TerrenoR13Ancho = 780f, TerrenoR13Largo = 340f;

        [MenuItem("Strategic Point/Operacion/R13 T14 · Ambientacion de desierto")]
        static void MenuDesiertoR13() => Debug.Log(DesiertoR13EscenaAbierta());

        static int TriangulosDeR13(GameObject go)
        {
            int n = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) n += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
            return n;
        }

        static bool ExcluidoDelDesiertoR13(float x, float z, Vector2[] ruta, float distRuta)
        {
            if (x >= 190f && z >= 100f) return true;                                  // ciudad
            if (x > -160f && x < 30f && z > 296f) return true;                        // rio
            if (ruta != null && DistanciaAPolilinea2(ruta, new Vector2(x, z)) < distRuta) return true;
            return false;
        }

        static GameObject EnvolturaDeArbolR13(GameObject origen, string nombre)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(origen);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); inst.transform.localScale = Vector3.one;
            var cis = new List<CombineInstance>(); var mats = new List<Material>();
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || mf.sharedMesh == null || !mr.enabled) continue;
                var m = mr.sharedMaterials;
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                {
                    cis.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sm, transform = inst.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix });
                    mats.Add(m[Mathf.Min(sm, m.Length - 1)]);
                }
            }
            if (cis.Count == 0) { Object.DestroyImmediate(inst); return null; }
            var malla = new Mesh { name = nombre + "_Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            malla.CombineMeshes(cis.ToArray(), false, true);
            string rutaMalla = EscenasR13.CarpetaGenerada + "/" + nombre + "_Mesh.asset";
            AssetDatabase.DeleteAsset(rutaMalla); AssetDatabase.CreateAsset(malla, rutaMalla);
            var raiz = new GameObject(nombre);
            raiz.AddComponent<MeshFilter>().sharedMesh = malla; raiz.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
            string rutaPrefab = EscenasR13.CarpetaGenerada + "/" + nombre + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            Object.DestroyImmediate(raiz); Object.DestroyImmediate(inst);
            return prefab;
        }

        /// <summary>
        /// T14. Firma: OperacionBuilder.DesiertoR13EscenaAbierta(bool soloInforme = false, int arboles = 650, int arbustos = 250, int presupuestoTris = 700000)
        /// arboles + arbustos debe quedar entre 600 y 1500 (Bug983a). presupuestoTris = triangulos visibles estimados como maximo (treeDistance 140 m).
        /// </summary>
        public static string DesiertoR13EscenaAbierta(bool soloInforme = false, int arboles = 650, int arbustos = 250, int presupuestoTris = 700000)
        {
            var sb = new StringBuilder("== R13 T14 desierto" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t14.txt");
            var esc = EscenasR13.Sector("Ruta");
            if (!esc.isLoaded) { sb.AppendLine("FALLO la escena Ruta no esta abierta"); return EscenasR13.Cerrar(sb, "r13_t14.txt"); }

            // polilinea de la ruta del tanque
            Vector2[] ruta = null;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || t.name != "RutaTanque" || t.childCount < 2) continue;
                ruta = new Vector2[t.childCount];
                for (int i = 0; i < t.childCount; i++) ruta[i] = new Vector2(t.GetChild(i).position.x, t.GetChild(i).position.z);
                break;
            }
            if (ruta == null) { sb.AppendLine("FALLO no se encontro RutaTanque (5_Autopista)"); return EscenasR13.Cerrar(sb, "r13_t14.txt"); }
            sb.AppendLine($"RutaTanque: {ruta.Length} puntos");

            var pA = AssetDatabase.LoadAssetAtPath<GameObject>(DirArte + "P_Env_ArbolA.prefab");
            var pB = AssetDatabase.LoadAssetAtPath<GameObject>(DirArte + "P_Env_ArbolB.prefab");
            var pArb = AssetDatabase.LoadAssetAtPath<GameObject>(DirArte + "P_Env_Arbusto_Grande.prefab");
            if (pA == null || pB == null || pArb == null) { sb.AppendLine("FALLO faltan P_Env_ArbolA/B o P_Env_Arbusto_Grande en ArteMundo"); return EscenasR13.Cerrar(sb, "r13_t14.txt"); }
            // Un TreePrototype exige un MeshRenderer en la RAIZ del prefab ("couldn't be instanced because the prefab contains no valid mesh renderer" en el player):
            // los P_Env_* tienen las piezas en hijos, asi que se arma una envoltura de una sola malla (un submesh por pieza) y esa es la que usa el Terrain.
            EscenasR13.AsegurarCarpetaGenerada();
            var tA = EnvolturaDeArbolR13(pA, "R13_Tree_ArbolA"); var tB = EnvolturaDeArbolR13(pB, "R13_Tree_ArbolB"); var tArb = EnvolturaDeArbolR13(pArb, "R13_Tree_Arbusto");
            if (tA == null || tB == null || tArb == null) { sb.AppendLine("FALLO no se pudo armar la envoltura de malla unica de los arboles"); return EscenasR13.Cerrar(sb, "r13_t14.txt"); }

            // ---- presupuesto de rendimiento
            const float treeDistance = 140f;
            float fraccionVisible = Mathf.PI * treeDistance * treeDistance / (TerrenoR13Ancho * TerrenoR13Largo);
            int trisA = TriangulosDeR13(pA), trisB = TriangulosDeR13(pB), trisArb = TriangulosDeR13(pArb);
            float EstimadoVisible(int nArb, int nArbus) => ((nArb * 0.5f * trisA) + (nArb * 0.5f * trisB) + nArbus * (float)trisArb) * fraccionVisible;
            sb.AppendLine($"triangulos por prototipo: ArbolA={trisA} ArbolB={trisB} Arbusto={trisArb}; fraccion visible a {treeDistance:0} m = {fraccionVisible:0.00}");
            int na = Mathf.Max(0, arboles), nb = Mathf.Max(0, arbustos);
            float est = EstimadoVisible(na, nb);
            while (est > presupuestoTris && na + nb > 600) { na = Mathf.Max(0, Mathf.RoundToInt(na * 0.93f)); nb = Mathf.Max(0, Mathf.RoundToInt(nb * 0.93f)); est = EstimadoVisible(na, nb); }
            if (na + nb < 600) { nb += 600 - (na + nb); est = EstimadoVisible(na, nb); }
            if (na + nb > 1500) { na = Mathf.Min(na, 1000); nb = Mathf.Min(nb, 500); }
            sb.AppendLine($"PRESUPUESTO: {na} arboles + {nb} arbustos = {na + nb}; triangulos visibles estimados {est:0} (tope {presupuestoTris}) {(est <= presupuestoTris ? "OK" : "SOBRE EL TOPE: bajar arboles/arbustos o treeDistance")}");

            // ---- scripts de juego del Carril D
            foreach (var nombre in new[] { "SP.Operacion.ArbustoRodante", "SP.Operacion.PolvoDelDesierto" })
            {
                bool hay = false;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) if (asm.GetType(nombre) != null) { hay = true; break; }
                sb.AppendLine($"script de juego {nombre}: {(hay ? "presente" : "NO existe todavia (lo escribe el Carril D)")}");
            }

            // ---- lo que ya existe
            foreach (var t in EscenasR13.DeEscena(esc)) if (t != null && (t.name == "Terreno_ArbolesDesierto" || t.name == "R13_Desierto")) sb.AppendLine("YA EXISTE (se recrea) " + EscenasR13.Ruta(t));
            if (soloInforme) { sb.AppendLine("se crearia: Terreno_ArbolesDesierto + R13_Desierto (rocas/cactus)"); return EscenasR13.Cerrar(sb, "r13_t14.txt"); }

            Physics.SyncTransforms();
            var rnd = new System.Random(1313);
            float R() => (float)rnd.NextDouble();

            // ---- posiciones de arboles/arbustos
            var puntos = new List<Vector2>(); var tipos = new List<int>();
            int intentos = 0, pedidos = na + nb;
            while (puntos.Count < pedidos && intentos++ < pedidos * 60)
            {
                float x = TerrenoR13X0 + 5f + R() * (TerrenoR13Ancho - 10f), z = TerrenoR13Z0 + 5f + R() * (TerrenoR13Largo - 10f);
                if (ExcluidoDelDesiertoR13(x, z, ruta, 14f)) continue;
                if (Physics.CheckSphere(new Vector3(x, 2.4f, z), 1.8f, ~0, QueryTriggerInteraction.Ignore)   /* centro a 2,4 m: el fondo de la esfera queda a 0,6 m y no roza el Ground (techo en y=0) */) continue;
                bool cerca = false;
                for (int i = puntos.Count - 1; i >= 0 && !cerca; i--) if ((puntos[i] - new Vector2(x, z)).sqrMagnitude < 9f) cerca = true;
                if (cerca) continue;
                puntos.Add(new Vector2(x, z));
                tipos.Add(puntos.Count <= na ? (puntos.Count % 2) : 2);
            }
            sb.AppendLine($"posiciones validas: {puntos.Count} de {pedidos} pedidas ({intentos} intentos)");

            // ---- Terrain solo de arboles
            EscenasR13.AsegurarCarpetaGenerada();
            string rutaTd = EscenasR13.CarpetaGenerada + "/TerrainData_ArbolesDesierto.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(rutaTd);
            if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, rutaTd); }
            td.heightmapResolution = 33; td.size = new Vector3(TerrenoR13Ancho, 10f, TerrenoR13Largo);
            td.treeInstances = new TreeInstance[0];
            td.treePrototypes = new[] { new TreePrototype { prefab = tA }, new TreePrototype { prefab = tB }, new TreePrototype { prefab = tArb } };
            var inst = new TreeInstance[puntos.Count];
            for (int i = 0; i < puntos.Count; i++)
            {
                float esc0 = tipos[i] == 2 ? 0.8f + R() * 0.7f : 0.85f + R() * 0.5f;
                inst[i] = new TreeInstance
                {
                    position = new Vector3((puntos[i].x - TerrenoR13X0) / TerrenoR13Ancho, 0f, (puntos[i].y - TerrenoR13Z0) / TerrenoR13Largo),
                    prototypeIndex = tipos[i], widthScale = esc0, heightScale = esc0 * (0.9f + R() * 0.25f), rotation = R() * Mathf.PI * 2f,
                    color = Color.white, lightmapColor = Color.white,
                };
            }
            td.SetTreeInstances(inst, true);
            EditorUtility.SetDirty(td);

            foreach (var r in esc.GetRootGameObjects()) if (r.name == "Terreno_ArbolesDesierto" || r.name == "R13_Desierto") Object.DestroyImmediate(r);
            var go = new GameObject("Terreno_ArbolesDesierto");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, esc);
            go.transform.position = new Vector3(TerrenoR13X0, -0.12f, TerrenoR13Z0);
            var terreno = go.AddComponent<Terrain>();                       // a mano: Terrain.CreateTerrainGameObject agregaria un TerrainCollider
            terreno.terrainData = td;
            terreno.drawHeightmap = false; terreno.drawTreesAndFoliage = true; terreno.allowAutoConnect = false;
            terreno.treeDistance = treeDistance; terreno.treeBillboardDistance = 90f; terreno.treeCrossFadeLength = 10f; terreno.treeMaximumFullLODCount = 120;
            terreno.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mod = go.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true; mod.applyToChildren = true;
            sb.AppendLine($"Terreno_ArbolesDesierto creado: {inst.Length} instancias, sin TerrainCollider ({(go.GetComponent<TerrainCollider>() == null ? "ok" : "ERROR: tiene collider")})");

            // ---- rocas y cactus en los huecos del bucle de la ruta
            var grupo = EscenasR13.GrupoNuevo(esc, "R13_Desierto");
            var cactus = new[] { "P_Op_Cactus", "P_Op_Cactus_2", "P_Op_Cactus_3", "P_Op_Cactus_4" };
            var rocas = new[] { "P_Op_Roca", "P_Op_Roca_2", "P_Op_Roca_3", "P_Op_Roca_4" };
            int nCactus = 0, nRocas = 0; intentos = 0;
            while ((nCactus < 36 || nRocas < 30) && intentos++ < 4000)
            {
                float x = 50f + R() * 130f, z = 135f + R() * 245f;                              // el bucle de la ruta: x 55..170, z 140..372
                if (ExcluidoDelDesiertoR13(x, z, ruta, 14f) || DistanciaAPolilinea2(ruta, new Vector2(x, z)) > 70f) continue;
                bool roca = nCactus >= 36 || (nRocas < 30 && R() < 0.45f);
                float yaw = R() * 360f;
                if (!LugarLibre(new Vector3(x, 0f, z), 1.2f, 1.2f, yaw, 2f)) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Escena/Elementos/{(roca ? rocas[rnd.Next(4)] : cactus[rnd.Next(4)])}.prefab");
                if (pf == null) continue;
                var o = (GameObject)PrefabUtility.InstantiatePrefab(pf, esc);
                o.transform.SetParent(grupo, false);
                o.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                o.name = (roca ? "Desierto_Roca_" : "Desierto_Cactus_") + (roca ? ++nRocas : ++nCactus);
                // mismo criterio que PulidoNivel2Nov11: roca maciza (85 %) y cactus solo el tronco (30 %)
                foreach (var mr in o.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.GetComponent<Collider>() != null) continue;
                    var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds; var bc = mr.gameObject.AddComponent<BoxCollider>();
                    float f = roca ? 0.85f : 0.3f; bc.center = b.center; bc.size = new Vector3(b.size.x * f, b.size.y, b.size.z * f);
                }
            }
            sb.AppendLine($"R13_Desierto: {nCactus} cactus + {nRocas} rocas (con collider: hay que rehornear el NavMesh en el paso 10 de la Ola 2)");

            AssetDatabase.SaveAssets();
            EscenasR13.Guardar(sb);
            sb.Insert(0, $"Terreno de arboles ({inst.Length}) y R13_Desierto ({nCactus + nRocas}) creados en Ruta. Falta medir perf_ruta (arnes) DESPUES y comparar con ANTES.\n");
            return EscenasR13.Cerrar(sb, "r13_t14.txt");
        }
    }
}
