using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SP.EditorTools
{
    // Pulido de rendimiento de SC_Operacion (parte 2): la ciudad usa ~6.000 piezas del kit SOLO VISUALES (losetas de piso, asfalto, lineas,
    // bordes, ventanas...) que se dibujaban una por una (9.300 draw calls en el Profiler, solo 127 mallas distintas).
    // Esta herramienta las FUSIONA por (material, sombra, capa, zona de 40 m) en mallas combinadas guardadas como assets.
    //
    // Seguro porque: el NavMesh se hornea con PhysicsColliders (las mallas visuales no cuentan), y solo se toca lo que no tiene ningun
    // Collider arriba en la jerarquia, ningun componente propio del juego, Rigidbody, Animator ni luz. Lo demolible (ObstacleMarker),
    // las torretas, las luces y los textos quedan intactos. Las piezas chatas (< 0,2 m de alto: asfalto, lineas, decals) dejan de
    // proyectar sombra, que no se nota y saca miles de casters del pase de sombras.
    public static partial class OperacionBuilder
    {
        const string CarpetaFusion = "Assets/_Project/Generated/FusionOperacion";
        const string NombreRaizFusion = "Fusionados";

        struct ClaveFusion
        {
            public Material mat; public int sombra, capa, cx, cz;
        }

        public static string FusionarMallas(Transform raiz, float celda = 40f)
        {
            if (raiz == null) return "sin raiz";
            var viejo = raiz.Find(NombreRaizFusion);
            if (viejo != null) return "ya fusionado (" + viejo.childCount + " mallas): reconstrui el nivel o borra '" + NombreRaizFusion + "' para repetir";

            // 1) candidatos
            var grupos = new Dictionary<ClaveFusion, List<MeshRenderer>>();
            int candidatos = 0, planos = 0;
            foreach (var mr in raiz.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (mr == null || !mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount != 1) continue;
                if (mr.sharedMaterials.Length != 1 || mr.sharedMaterial == null) continue;
                if (mr.GetComponent<TextMesh>() != null || mr.HasPropertyBlock()) continue;
                if (mr.GetComponentInParent<Collider>() != null) continue;
                if (EsDinamico(mr.transform, raiz)) continue;
                if (mr.transform.childCount > 0) continue;

                var modo = mr.shadowCastingMode;
                if (modo != ShadowCastingMode.Off && mr.bounds.size.y < 0.2f) { modo = ShadowCastingMode.Off; planos++; }
                var c = mr.bounds.center;
                var k = new ClaveFusion { mat = mr.sharedMaterial, sombra = (int)modo, capa = mr.gameObject.layer, cx = Mathf.FloorToInt(c.x / celda), cz = Mathf.FloorToInt(c.z / celda) };
                if (!grupos.TryGetValue(k, out var l)) grupos[k] = l = new List<MeshRenderer>();
                l.Add(mr);
                candidatos++;
            }
            if (candidatos == 0) return "no hay piezas fusionables";

            // 2) las mallas importadas no son legibles: se habilita Read/Write un rato y se restaura al final
            var importadores = new Dictionary<string, ModelImporter>();
            foreach (var l in grupos.Values)
                foreach (var mr in l)
                {
                    var m = mr.GetComponent<MeshFilter>().sharedMesh;
                    if (m.isReadable) continue;
                    string ruta = AssetDatabase.GetAssetPath(m);
                    if (string.IsNullOrEmpty(ruta) || importadores.ContainsKey(ruta)) continue;
                    if (AssetImporter.GetAtPath(ruta) is ModelImporter imp && !imp.isReadable) importadores[ruta] = imp;
                }
            AssetDatabase.StartAssetEditing();
            try { foreach (var imp in importadores.Values) { imp.isReadable = true; imp.SaveAndReimport(); } }
            finally { AssetDatabase.StopAssetEditing(); }

            // 3) fusion
            if (Directory.Exists(CarpetaFusion)) AssetDatabase.DeleteAsset(CarpetaFusion);
            Directory.CreateDirectory(CarpetaFusion);
            var padre = new GameObject(NombreRaizFusion).transform;
            padre.SetParent(raiz, false);
            int creadas = 0, sinFusionar = 0, eliminados = 0, vertices = 0;
            var aBorrar = new List<GameObject>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in grupos)
                {
                    var lista = kv.Value;
                    if (lista.Count < 2) { sinFusionar += lista.Count; continue; }
                    var combos = new List<CombineInstance>(lista.Count);
                    long verts = 0;
                    foreach (var mr in lista)
                    {
                        var mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                        if (!mesh.isReadable) continue;
                        combos.Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = padre.worldToLocalMatrix * mr.transform.localToWorldMatrix });
                        verts += mesh.vertexCount;
                    }
                    if (combos.Count < 2) { sinFusionar += lista.Count; continue; }

                    var fus = new Mesh { name = $"Fus_{kv.Key.mat.name}_{kv.Key.cx}_{kv.Key.cz}_{creadas}" };
                    if (verts > 65000) fus.indexFormat = IndexFormat.UInt32;
                    fus.CombineMeshes(combos.ToArray(), true, true);
                    fus.RecalculateBounds();
                    string ruta = $"{CarpetaFusion}/{creadas:0000}.asset";
                    AssetDatabase.CreateAsset(fus, ruta);

                    var go = new GameObject(fus.name);
                    go.transform.SetParent(padre, false);
                    go.layer = kv.Key.capa;
                    go.AddComponent<MeshFilter>().sharedMesh = fus;
                    var rend = go.AddComponent<MeshRenderer>();
                    rend.sharedMaterial = kv.Key.mat;
                    rend.shadowCastingMode = (ShadowCastingMode)kv.Key.sombra;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    var navMod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();   // por si alguien hornea desde mallas: la geometria visual no cuenta
                    navMod.ignoreFromBuild = true;
                    creadas++; vertices += (int)verts;
                    foreach (var mr in lista) if (mr != null) aBorrar.Add(mr.gameObject);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            foreach (var go in aBorrar) { if (go != null) { Object.DestroyImmediate(go); eliminados++; } }

            // 4) restaurar la legibilidad original de las mallas importadas
            AssetDatabase.StartAssetEditing();
            try { foreach (var imp in importadores.Values) { imp.isReadable = false; imp.SaveAndReimport(); } }
            finally { AssetDatabase.StopAssetEditing(); }

            return $"fusionadas {eliminados} piezas en {creadas} mallas ({vertices} vertices); {sinFusionar} piezas sueltas; {planos} piezas chatas sin sombra";
        }

        // Para la escena abierta (sin reconstruir): fusiona y guarda.
        public static string FusionarMallasEscenaAbierta()
        {
            var activa = EditorSceneManager.GetActiveScene();
            var o = GameObject.Find("Operacion");
            if (o == null) return "no hay un objeto 'Operacion' en " + activa.name;
            string r = FusionarMallas(o.transform);
            EditorSceneManager.MarkSceneDirty(activa);
            EditorSceneManager.SaveScene(activa);
            AssetDatabase.SaveAssets();
            return r;
        }
    }
}
