using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido explicito: "Crea un prefab para cada soldado y limpia un
    // poco". Los 3 aliados jugables (Soldado_1_Vega/2_Kes/3_Doc) vivian
    // sueltos en SC_Gameplay -- a diferencia de props/armas/vehiculo, que
    // ya tienen su .prefab reutilizable desde WorldArtPipeline, nunca se
    // armo uno para los soldados.
    //
    // SaveAsPrefabAssetAndConnect hace en un solo paso lo mismo que
    // "arrastrar el GameObject de la escena a la carpeta de Project": crea
    // el .prefab a partir de la jerarquia actual (visual, colliders,
    // AiBrain/Soldier/Health/etc.) y conecta la instancia de la escena a
    // ese prefab, sin mover, renombrar ni resetear nada. Correrlo de nuevo
    // solo re-sincroniza el prefab -- no duplica ni rompe la conexion.
    public static class SoldierPrefabPipeline
    {
        const string PrefabDir = "Assets/_Project/Prefabs/Soldados";
        static readonly string[] Soldados = { "Soldado_1_Vega", "Soldado_2_Kes", "Soldado_3_Doc" };

        [MenuItem("Strategic Point/Arte/7. Crear prefabs de soldados")]
        public static void CrearPrefabs()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay")
            {
                scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Gameplay.unity", OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError("[SoldierPrefabPipeline] No se pudo abrir SC_Gameplay.");
                    return;
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Soldados");

            int ok = 0;
            foreach (var nombre in Soldados)
            {
                var go = GameObject.Find(nombre);
                if (go == null)
                {
                    Debug.LogWarning($"[SoldierPrefabPipeline] No se encontro '{nombre}' en la escena -- se omite.");
                    continue;
                }

                string ruta = $"{PrefabDir}/P_{nombre}.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, ruta, InteractionMode.AutomatedAction);
                ok++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SoldierPrefabPipeline] {ok}/{Soldados.Length} prefabs de soldado creados/actualizados en {PrefabDir}. Escena guardada.");
        }
    }
}
