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

        // Pedido explicito (ronda 1): "la UI de abajo a la izq q sea mas
        // simplificado con iconos simples". Pedido explicito (ronda 2):
        // "re-diagramalo mejor... q los indicadores sean cuadrados
        // simplificados. Solo icono, numero de vida y barra. Simple."
        // Deja cada fila como un cuadrado fijo (TamanoCuadrado) con el
        // icono de rol arriba, el numero de vida al medio y la barra de
        // vida abajo -- nada mas. Tambien fuerza al HorizontalLayoutGroup
        // del "Roster" (el contenedor de las filas en la escena) a respetar
        // ese tamaño fijo en vez de estirar cada fila a lo ancho.
        // Idempotente: si "Icon" ya existe no lo duplica, solo reacomoda.
        const float TamanoCuadrado = 56f;

        [MenuItem("Strategic Point/UI/2. Simplificar fila del Roster (cuadrado con icono)")]
        public static void SimplificarConIconos()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Gameplay"))
            {
                EditorSceneManager.OpenScene(ScenePath);
                scene = EditorSceneManager.GetActiveScene();
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[RosterUiPipeline] No se pudo abrir " + PrefabPath + " (correr primero \"1. Migrar Roster a prefab por soldado\").");
                return;
            }

            var labelT = root.transform.Find("Label");
            var barT = root.transform.Find("BarBG");
            if (labelT == null || barT == null)
            {
                Debug.LogError("[RosterUiPipeline] El prefab no tiene Label/BarBG como se esperaba.");
                PrefabUtility.UnloadPrefabContents(root);
                return;
            }
            var labelRt = labelT.GetComponent<RectTransform>();
            var barRt = barT.GetComponent<RectTransform>();

            var iconoExistente = root.transform.Find("Icon");
            var iconGO = iconoExistente != null ? iconoExistente.gameObject
                : new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            iconGO.transform.SetParent(root.transform, false);
            iconGO.transform.SetAsFirstSibling();

            // Icono: bloque superior, casi todo el ancho del cuadrado.
            var iconRt = iconGO.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0f);
            iconRt.anchorMax = new Vector2(1f, 1f);
            iconRt.offsetMin = new Vector2(4f, 27f);
            iconRt.offsetMax = new Vector2(-4f, -4f);

            var iconImg = iconGO.GetComponent<UnityEngine.UI.Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;

            // Numero de vida: franja angosta entre el icono y la barra.
            // OJO: con anchorMax.y == anchorMin.y == 0 (pineado abajo, sin
            // stretch vertical) offsetMax.y NO es un inset desde arriba --
            // es, igual que offsetMin.y, una posicion absoluta medida desde
            // el borde inferior del padre. Restarle TamanoCuadrado (formula
            // que si vale para un anchor estirado 0..1) daba un rect
            // invertido (offsetMax.y menor que offsetMin.y) y la fila se
            // veia sin numero ni barra.
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.offsetMin = new Vector2(2f, 8f);
            labelRt.offsetMax = new Vector2(-2f, 24f);
            var labelText = labelT.GetComponent<UnityEngine.UI.Text>();
            if (labelText != null)
            {
                labelText.alignment = TextAnchor.MiddleCenter;
                // BUG REAL: la caja del numero mide poco de alto (16px) y el
                // Text por default trunca vertical -- un tamaño de fuente
                // igual o mayor a la caja (12/14 contra 11 de antes) hacia
                // que Unity no dibujara NADA, ni siquiera recortado. Overflow
                // en vez de Truncate es la red de seguridad (nunca desaparece
                // aunque algun estado use un numero mas ancho/alto).
                labelText.verticalOverflow = VerticalWrapMode.Overflow;
                labelText.fontSize = 13;
            }

            // Barra de vida: tira fina pegada abajo del todo.
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.offsetMin = new Vector2(3f, 2f);
            barRt.offsetMax = new Vector2(-3f, 7f);

            // El cuadrado en si: LayoutElement fijo (el HorizontalLayoutGroup
            // del Roster, ajustado abajo, respeta este tamaño en vez de
            // estirar la fila a lo ancho como antes).
            var layoutEl = root.GetComponent<UnityEngine.UI.LayoutElement>();
            if (layoutEl == null) layoutEl = root.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutEl.preferredWidth = layoutEl.minWidth = TamanoCuadrado;
            layoutEl.preferredHeight = layoutEl.minHeight = TamanoCuadrado;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            var roster = GameObject.Find("Roster");
            if (roster != null)
            {
                var hlg = roster.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                if (hlg != null)
                {
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;
                }
                var rosterRt = roster.GetComponent<RectTransform>();
                if (rosterRt != null) rosterRt.sizeDelta = new Vector2(rosterRt.sizeDelta.x, TamanoCuadrado);
                EditorUtility.SetDirty(roster);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[RosterUiPipeline] Fila del roster pasada a cuadrado simple (icono + numero + barra). Prefab y escena guardados.");
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
