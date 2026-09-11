using UnityEngine;
using UnityEditor;

namespace SP.EditorTools
{
    // Arma los prefabs de arma REAL (rifle/pistola/pesada/metralleta) a partir
    // de los FBX multi-parte de Assets/ARTS/SP_Arte/_FBX_Export/05_Armas --
    // que estaban en el proyecto sin usar: los soldados (jugador, aliados y
    // enemigos) mostraban un cubo de color en la mano en vez del arma real.
    //
    // Van a Resources/Weapons porque WeaponHolder/WeaponBackRack los cargan
    // en RUNTIME con Resources.Load (funciona igual en Play mode que en un
    // build final), no con AssetDatabase (solo Editor).
    public static class WeaponPrefabBuilder
    {
        const string FbxDir = "Assets/ARTS/SP_Arte/_FBX_Export/05_Armas";
        const string PrefabDir = "Assets/_Project/Resources/Weapons";

        [MenuItem("SP/Art/Build Weapon Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/_Project", "Resources");
            EnsureFolder("Assets/_Project/Resources", "Weapons");

            Build("SM_Wpn_Fusil", "P_Wpn_Fusil");
            Build("SM_Wpn_Pistola", "P_Wpn_Pistola");
            Build("SM_Wpn_LMG", "P_Wpn_Heavy");
            Build("SM_Wpn_Metralleta", "P_Wpn_Metralleta");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[WeaponPrefabBuilder] Prefabs de arma listos en " + PrefabDir);
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static void Build(string fbxName, string prefabName)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + "/" + fbxName + ".fbx");
            if (fbx == null) { Debug.LogWarning("[WeaponPrefabBuilder] Falta el FBX " + fbxName); return; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            inst.name = prefabName;

            // BUG REAL que esto corrige: los 6 FBX de armas traen la MISMA
            // raiz corrida (-0.279, 0.876, 0.419) -- resto de donde estaba
            // parada el arma en la escena de origen al exportar. Sin anular
            // esto, instanciar el prefab en la mano de un soldado la
            // aparece a casi un metro de distancia, no en el punto de
            // agarre.
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var path = PrefabDir + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
        }
    }
}
