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
    //
    // BUG REAL que esto corrige: a diferencia de WorldArtPipeline (que
    // remapea el material de CADA FBX de 05_Armas al importarlo), este
    // builder instanciaba el FBX crudo y lo guardaba tal cual -- el
    // material que trae el propio FBX (o el "Lit" por defecto de Unity si
    // no trae ninguno), sin textura. El arma en la mano/espalda de
    // cualquier soldado terminaba con SafeMaterial.Create tapando ese
    // hueco con un color solido en vez del trimsheet real. Ahora el
    // material de cada renderer se fuerza al MISMO material compartido
    // que usa el resto del pack (WorldArtPipeline.CrearMaterialTrimsheet),
    // en vez de depender del remap del importer: bake explicito, no una
    // esperanza de orden de ejecucion entre dos herramientas separadas.
    public static class WeaponPrefabBuilder
    {
        const string FbxDir = "Assets/ARTS/SP_Arte/_FBX_Export/05_Armas";
        const string PrefabDir = "Assets/_Project/Resources/Weapons";
        const string TrimsheetMatPath = "Assets/_Project/Materials/ArteMundo/MAT_Trimsheet.mat";

        [MenuItem("SP/Art/Build Weapon Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/_Project", "Resources");
            EnsureFolder("Assets/_Project/Resources", "Weapons");

            Build("SM_Wpn_Fusil", "P_Wpn_Fusil");
            Build("SM_Wpn_Pistola", "P_Wpn_Pistola");
            Build("SM_Wpn_LMG", "P_Wpn_Heavy");
            Build("SM_Wpn_Metralleta", "P_Wpn_Metralleta");
            Build("SM_Wpn_Lanzacohetes", "P_Wpn_Lanzacohetes");
            Build("SM_Wpn_Escopeta", "P_Wpn_Escopeta");
            Build("SM_Wpn_Sniper", "P_Wpn_Sniper");
            Build("SM_Wpn_AK", "P_Wpn_AK");

            // Auditoria #44: el cuchillo (CuchilloFx.cs) y la granada
            // (Granada.cs) cargan "Weapons/P_Wpn_Cuchillo" y
            // "Weapons/P_Wpn_Granada" en runtime; hasta ahora esos dos
            // prefabs existian en el proyecto pero nadie los regeneraba
            // desde este builder (se habian armado a mano). Se agregan aca
            // para que "Build Weapon Prefabs" los reconstruya igual que al
            // resto si el FBX cambia.
            Build("SM_Wpn_Cuchillo", "P_Wpn_Cuchillo");
            Build("SM_Wpn_Granada", "P_Wpn_Granada");

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

            // BUG REAL que esto corrige: anular la raiz (arriba) deja el
            // GRUPO en el origen, pero no dice nada sobre DONDE, dentro de
            // ese grupo, quedo el origen respecto de la malla -- el FBX
            // trae ese punto donde le quedo comodo al artista en Maya, no
            // necesariamente en el centro del arma. WeaponHolder cuelga el
            // arma de la mano usando ESTE origen como punto de agarre, y
            // WeaponBackRack lo reescala hasta 2,4x (la pistola, la mas
            // chica, contra TargetBackLength) -- cualquier corrimiento
            // entre origen y malla se ve chico en la mano pero se EXAGERA
            // multiplicado por esa escala en la espalda: la pistola
            // colgada terminaba flotando ~30-50 cm lejos de su lugar, cerca
            // de la cabeza. Se recentra el contenido para que el origen del
            // prefab quede en el centro real de su propia geometria, mismo
            // patron que WorldArtPipeline.ArmarPrefabs usa para las props.
            var bounds = default(Bounds);
            bool first = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!first)
            {
                var center = bounds.center; // inst esta en el origen: bounds mundial == bounds local
                foreach (Transform child in inst.transform)
                    child.localPosition -= center;
            }

            var trimsheet = AssetDatabase.LoadAssetAtPath<Material>(TrimsheetMatPath);
            if (trimsheet != null)
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = trimsheet;
            }
            else
            {
                Debug.LogWarning("[WeaponPrefabBuilder] No se encontro " + TrimsheetMatPath +
                                  " -- corre antes 'Strategic Point/Arte/4. Importar arte de mundo nuevo'.");
            }

            var path = PrefabDir + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
        }
    }
}
