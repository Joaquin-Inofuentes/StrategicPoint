using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace SP.EditorTools
{
    public static class NavMeshSetupPipeline
    {
        [MenuItem("Tools/NavMesh/Setup Prefabs and Bake Scenes")]
        public static void RunFullSetup()
        {
            SetupSoldierPrefabs();
            BakeScene("Assets/_Project/Scenes/SC_Gameplay.unity");
            BakeScene("Assets/_Project/Scenes/SC_TestLevel.unity");
            if (File.Exists("Assets/_Recovery/0.unity"))
            {
                BakeScene("Assets/_Recovery/0.unity");
            }

            // Dejar activa la escena principal
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Gameplay.unity", OpenSceneMode.Single);
            Debug.Log("[NavMeshSetupPipeline] Setup completo de prefabs y horneado de NavMesh en escenas finalizado con exito.");
        }

        public static void SetupSoldierPrefabs()
        {
            string[] prefabPaths = {
                "Assets/_Project/Prefabs/P_Soldier_Base.prefab",
                "Assets/_Project/Prefabs/P_Soldier_Ally.prefab",
                "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab",
                "Assets/_Project/Prefabs/Soldados/P_Soldado_1_Vega.prefab",
                "Assets/_Project/Prefabs/Soldados/P_Soldado_2_Kes.prefab",
                "Assets/_Project/Prefabs/Soldados/P_Soldado_3_Doc.prefab"
            };

            int updated = 0;
            foreach (var path in prefabPaths)
            {
                if (!File.Exists(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                var agent = root.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    agent = root.AddComponent<NavMeshAgent>();
                }

                agent.radius = 0.45f;
                agent.height = 1.8f;
                agent.speed = 5f;
                agent.acceleration = 12f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 0.1f;
                agent.autoBraking = true;
                agent.updateRotation = true;
                agent.enabled = false; // Se activa según IsPossessedByPlayer en runtime

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                updated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[NavMeshSetupPipeline] {updated} prefabs de soldados actualizados con NavMeshAgent.");
        }

        public static void BakeScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[NavMeshSetupPipeline] No se pudo abrir la escena: {scenePath}");
                return;
            }

            // Buscar o crear NavMeshSurface
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                var go = new GameObject("NavMeshSurface");
                surface = go.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[NavMeshSetupPipeline] NavMesh horneado y guardado para: {scenePath}");
        }
    }
}
