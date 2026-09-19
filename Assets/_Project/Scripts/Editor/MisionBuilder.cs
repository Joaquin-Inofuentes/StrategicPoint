using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Mision;

namespace SP.EditorTools
{
    // Arma la mision de rescate en SC_Gameplay: un objeto "Mision" con el MisionDirector y las
    // referencias a los prefabs (enemigo, civil, helicoptero del arte). Es idempotente.
    public static class MisionBuilder
    {
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";
        const string PrefabCivil = "Assets/_Project/Prefabs/P_Soldier_Ally.prefab";
        const string PrefabHeli = "Assets/_Project/Prefabs/ArteMundo/P_Veh_Heli.prefab";

        [MenuItem("Strategic Point/Mision/Construir mision de rescate (SC_Gameplay)")]
        public static void Construir()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay") { Debug.LogWarning("[Mision] Abri SC_Gameplay antes (escena activa: " + scene.name + ")."); return; }

            var previo = GameObject.Find("Mision");
            if (previo != null) Object.DestroyImmediate(previo);

            var go = new GameObject("Mision");
            var d = go.AddComponent<MisionDirector>();
            var so = new SerializedObject(d);
            so.FindProperty("enemigoPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            so.FindProperty("civilPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabCivil);
            so.FindProperty("heliPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabHeli);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Mision] MisionDirector armado en SC_Gameplay.");
        }
    }
}
