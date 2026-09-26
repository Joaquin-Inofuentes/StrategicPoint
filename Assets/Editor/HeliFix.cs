using UnityEngine;
using UnityEditor;
public static class HeliFix {
    public static void Fix() {
        string[] paths = { "Assets/_Project/Prefabs/ArteMundo/P_Veh_Heli.prefab", "Assets/_Project/Prefabs/ArteMundo/P_Veh_Heli_Fuselaje.prefab" };
        foreach (var path in paths) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            bool changed = false;
            foreach (var rend in prefab.GetComponentsInChildren<MeshRenderer>(true)) {
                if (rend.gameObject.name.ToLower().Contains("sphere") || rend.gameObject.name.ToLower().Contains("esfera") || rend.GetComponent("HitboxDeImpacto") != null || rend.gameObject.GetComponent<SphereCollider>() != null) {
                    Debug.Log("Removing renderer from: " + rend.gameObject.name + " in " + path);
                    Object.DestroyImmediate(rend, true);
                    var mf = rend.gameObject.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf, true);
                    changed = true;
                }
            }
            if (changed) PrefabUtility.SavePrefabAsset(prefab);
        }
        Debug.Log("Done HeliFix");
    }
}
