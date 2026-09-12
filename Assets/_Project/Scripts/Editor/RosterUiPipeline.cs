using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.UI;

namespace SP.EditorTools
{
    // Pedido explicito: "en Roster los bloques... quiero un prefab para
    // cada caso y q tenga puntero al soldado... y se sincronize en tiempo
    // real por eventos". Antes las 3 filas (Row_Soldado_1_Vega/2_Kes/3_Doc)
    // vivian pre-armadas a mano en SC_Gameplay y SelectedSoldierUI las
    // reencontraba por nombre en OnEnable, refrescando TODO por LateUpdate
    // cada frame. Ahora:
    //   - Un solo prefab reutilizable (P_RosterRow, RosterRowView) define
    //     como es "una fila".
    //   - RosterView instancia una por soldado del squad real, cada una
    //     con su propio Soldier ya atado (Bind).
    //   - Cada fila se sincroniza sola via EventBus (Damage/Healed/Died/
    //     AiState/Weapon/Possession/Selection) -- cero polling.
    //
    // Idempotente: correrlo de nuevo reconstruye el prefab y vuelve a
    // limpiar la escena sin duplicar nada.
    public static class RosterUiPipeline
    {
        const string PrefabDir = "Assets/_Project/Prefabs/UI";
        const string PrefabPath = PrefabDir + "/P_RosterRow.prefab";
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";

        [MenuItem("Strategic Point/UI/1. Migrar Roster a prefab por soldado")]
        public static void Migrar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SC_Gameplay")
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError("[RosterUiPipeline] No se pudo abrir SC_Gameplay.");
                    return;
                }
            }

            var roster = GameObject.Find("Roster");
            if (roster == null)
            {
                Debug.LogError("[RosterUiPipeline] No se encontro el GameObject 'Roster' en la escena.");
                return;
            }

            // La primera fila existente es el molde visual (mismo fondo,
            // Label y BarBG/BarFill que ya se veian bien) -- se clona para
            // no tocar el original hasta no tener el prefab guardado.
            Transform filaMolde = null;
            foreach (Transform child in roster.transform)
                if (child.name.StartsWith("Row_")) { filaMolde = child; break; }

            if (filaMolde == null)
            {
                Debug.LogError("[RosterUiPipeline] No se encontro ninguna fila 'Row_*' para usar de molde.");
                return;
            }

            var prefabRoot = CrearPrefab(filaMolde);
            if (prefabRoot == null) return;

            // Limpia las filas viejas (pre-armadas a mano, una por soldado)
            // -- RosterView las va a reemplazar por instancias reales del
            // prefab en runtime.
            for (int i = roster.transform.childCount - 1; i >= 0; i--)
            {
                var child = roster.transform.GetChild(i);
                if (child.name.StartsWith("Row_")) Object.DestroyImmediate(child.gameObject);
            }

            var viejo = roster.GetComponent<SelectedSoldierUI>();
            if (viejo != null) Object.DestroyImmediate(viejo);

            var rosterView = roster.GetComponent<RosterView>();
            if (rosterView == null) rosterView = roster.AddComponent<RosterView>();
            var prefabAsset = AssetDatabase.LoadAssetAtPath<RosterRowView>(PrefabPath);
            rosterView.SetRowPrefab(prefabAsset);

            EditorUtility.SetDirty(roster);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[RosterUiPipeline] Roster migrado a RosterView + " + PrefabPath + ". Escena guardada.");
        }

        static RosterRowView CrearPrefab(Transform filaMolde)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");

            var clon = Object.Instantiate(filaMolde.gameObject);
            clon.name = "P_RosterRow";

            // Texto horneado de una fila puntual ("Soldado_1_Vega
            // (Assault)") no tiene sentido en un prefab generico: Bind()
            // lo pisa apenas se instancia, pero dejarlo vacio evita
            // confundir a quien abra el prefab en el Editor.
            var label = clon.transform.Find("Label")?.GetComponent<UnityEngine.UI.Text>();
            if (label != null) label.text = "";

            var view = clon.GetComponent<RosterRowView>();
            if (view == null) view = clon.AddComponent<RosterRowView>();

            PrefabUtility.SaveAsPrefabAsset(clon, PrefabPath);
            Object.DestroyImmediate(clon);

            return AssetDatabase.LoadAssetAtPath<RosterRowView>(PrefabPath);
        }
    }
}
