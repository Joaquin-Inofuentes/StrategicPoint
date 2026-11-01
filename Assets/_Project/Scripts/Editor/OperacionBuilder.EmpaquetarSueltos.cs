using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.EditorTools
{
    // Lo que el reparto dejo sin prefab (contenedores con NavMesh o luces propias, o de menos de 60 m): cada uno pasa a ser un prefab
    // entero en Prefabs/Escena/Elementos, en su mismo lugar y con su misma jerarquia. Los identicos (misma firma) comparten prefab.
    public static partial class OperacionBuilder
    {
        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Empaquetar elementos sueltos de los sectores")]
        static void EmpaquetarSueltosMenu() => Debug.Log(EmpaquetarSueltosDeSectores());

        // Un prefab no puede apuntar a un material que solo existe en la escena: se guarda como asset (compartido si varias piezas usan el mismo).
        static int PersistirMateriales(Transform raiz)
        {
            const string carpeta = "Assets/_Project/Materials/Generated";
            AsegurarCarpeta(carpeta);
            int n = 0;
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials; bool cambio = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i]; if (m == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))) continue;
                    string ruta = AssetDatabase.GenerateUniqueAssetPath($"{carpeta}/Op_{m.name.Replace(" ", "_").Replace("(Instance)", "")}.mat");
                    AssetDatabase.CreateAsset(m, ruta); n++; cambio = true;
                }
                if (cambio) r.sharedMaterials = ms;
            }
            return n;
        }

        public static string EmpaquetarSueltosDeSectores()
        {
            AsegurarCarpeta(CarpetaElementos);
            var res = new System.Text.StringBuilder();
            var porFirma = new Dictionary<string, GameObject>();
            int nuevos = 0, reutilizados = 0, omitidos = 0, materialesNuevos = 0;
            foreach (var sector in SectoresDelEntorno)
            {
                var esc = SceneManager.GetSceneByPath(RutaEscenaDeSector(sector));
                if (!esc.IsValid() || !esc.isLoaded) { res.AppendLine(sector + ": escena no abierta"); continue; }
                Transform raiz = null; foreach (var r in esc.GetRootGameObjects()) if (r.name == sector) raiz = r.transform;
                if (raiz == null) continue;
                var lista = new List<Transform>();
                foreach (Transform grupo in raiz) foreach (Transform e in grupo)
                {
                    if (PrefabUtility.IsPartOfAnyPrefab(e.gameObject)) continue;
                    if (e.GetComponentInChildren<Renderer>(true) == null && e.GetComponentInChildren<Light>(true) == null) continue;
                    lista.Add(e);
                }
                foreach (var t in lista)
                {
                    bool juego = false;
                    foreach (var c in t.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null && EsComponenteDeJuegoQueSeQueda(c)) { juego = true; break; }
                    if (juego) { omitidos++; res.AppendLine("  omitido (componente de juego): " + t.parent.name + "/" + t.name); continue; }
                    materialesNuevos += PersistirMateriales(t);
                    string firma = FirmaDeElemento(t);
                    var padre = t.parent; int idx = t.GetSiblingIndex(); string nombre = t.name;
                    if (porFirma.TryGetValue(firma, out var existente))
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(existente, esc);
                        inst.transform.SetParent(padre, false);
                        inst.transform.localPosition = t.localPosition; inst.transform.localRotation = t.localRotation; inst.transform.localScale = t.localScale;
                        inst.name = nombre; inst.layer = t.gameObject.layer; inst.SetActive(t.gameObject.activeSelf);
                        inst.transform.SetSiblingIndex(idx);
                        Object.DestroyImmediate(t.gameObject);
                        reutilizados++;
                    }
                    else
                    {
                        string baseNombre = "P_Op_" + padre.name.Replace(" ", "_") + "_" + nombre.Replace(" ", "_");
                        string ruta = $"{CarpetaElementos}/{baseNombre}.prefab"; int n = 1;
                        while (File.Exists(ruta)) { n++; ruta = $"{CarpetaElementos}/{baseNombre}_{n}.prefab"; }
                        var asset = PrefabUtility.SaveAsPrefabAssetAndConnect(t.gameObject, ruta, InteractionMode.AutomatedAction);
                        if (asset == null) { omitidos++; res.AppendLine("  no se pudo crear prefab de " + padre.name + "/" + nombre); continue; }
                        t.name = nombre;
                        porFirma[firma] = asset; nuevos++;
                    }
                }
                EditorSceneManager.MarkSceneDirty(esc);
            }
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            res.Insert(0, $"Sueltos empaquetados: prefabs nuevos={nuevos}, instancias de uno ya creado={reutilizados}, omitidos={omitidos}\n");
            return res.ToString();
        }
    }
}
