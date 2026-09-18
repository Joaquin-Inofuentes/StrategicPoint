using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class HierarchyPowerTools
{
    private static List<EntityId> selectionHistory = new List<EntityId>();
    private const int MaxHistory = 7;
    private static int tabIndex = -1;

    // --- Variables de Estado (Preview) ---
    private static EntityId lastHoveredID = EntityId.None;
    private static bool lastStateWasActive;
    private static GameObject lastHoveredObj;

    // --- Variables de Animación (Focus) ---
    private static Vector3 targetPivot;
    private static Vector3 startPivot;
    private static bool isAnimatingPivot = false;
    private static float animationStartTime;
    private const float AnimationDuration = 0.12f;

    // --- Variables para el Bounding Box y Crosshair ---
    private static GameObject objectToHighlight;
    private static bool isMiddleDragging = false;
    private static bool isMouseInHierarchy = false;
    private static List<EntityId> draggedObjectsSession = new List<EntityId>();

    // --- Tab-select en Scene View (objetos solapados bajo el cursor) ---
    private static List<GameObject> sceneViewPickIgnore = new List<GameObject>();
    private static Vector2 sceneViewPickMousePos;
    private const float SceneViewPickMoveTolerance = 4f; // px: si el mouse se movio mas que esto, reiniciar el ciclo

    // --- Caches y Optimización ---
    private static Dictionary<EntityId, int> hierarchyIndexCache = new Dictionary<EntityId, int>();
    private static double lastCacheTime = -100;
    private static bool cacheDirty = true;
    private static double lastMouseCheckTime = -1;
    private static bool cachedIsMouseInHierarchy = false;

    // Caches de GUI y GC Alloc
    private static GUIStyle numberStyle;
    private static GUIStyle toggleStyle;
    private static GUIStyle historyStyle;
    private static string[] indexStrings = new string[1000];

    private static void InitializeStyles()
    {
        if (numberStyle == null)
        {
            numberStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 9,
                fontStyle = FontStyle.Normal
            };
        }
        if (toggleStyle == null)
        {
            toggleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }
        if (historyStyle == null)
        {
            historyStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
    }

    private static string GetIndexString(int index)
    {
        if (index < 0) return "";
        if (index < indexStrings.Length)
        {
            if (indexStrings[index] == null)
            {
                indexStrings[index] = index.ToString("D3");
            }
            return indexStrings[index];
        }
        return index.ToString("D3");
    }

    private const string EnabledPrefKey = "MIP.HierarchyPowerTools.Enabled";

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledPrefKey, true);
        set => EditorPrefs.SetBool(EnabledPrefKey, value);
    }

    [MenuItem("MIP/Escena/Hierarchy Power Tools activo", false, 100)]
    private static void ToggleEnabled()
    {
        Enabled = !Enabled;
        if (!Enabled) RestoreAllTemporaryState();
    }

    [MenuItem("MIP/Escena/Hierarchy Power Tools activo", true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked("MIP/Escena/Hierarchy Power Tools activo", Enabled);
        return true;
    }

    static HierarchyPowerTools()
    {
        EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnGUI;
        Selection.selectionChanged += UpdateHistory;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyChanged += MarkCacheDirty;

        // Si se pierde el evento MouseUp (Alt-Tab, recarga de assembly, cierre del
        // Editor) los objetos previsualizados/barridos con Ctrl quedaban con su
        // activeSelf alterado para siempre. Restaurar en cualquiera de esos casos.
        AssemblyReloadEvents.beforeAssemblyReload += RestoreAllTemporaryState;
        EditorApplication.quitting += RestoreAllTemporaryState;
    }

    private static void RestoreAllTemporaryState()
    {
        RestorePreview();
        isMiddleDragging = false;

        foreach (EntityId id in draggedObjectsSession)
        {
            GameObject dragObj = EditorUtility.EntityIdToObject(id) as GameObject;
            if (dragObj != null) dragObj.SetActive(false);
        }
        draggedObjectsSession.Clear();
    }

    private static void MarkCacheDirty()
    {
        cacheDirty = true;
    }

    private static void UpdateHistory()
    {
        EntityId currentID = Selection.activeEntityId;
        if (!currentID.IsValid()) return;
        if (selectionHistory.Contains(currentID)) selectionHistory.Remove(currentID);
        selectionHistory.Insert(0, currentID);
        if (selectionHistory.Count > MaxHistory) selectionHistory.RemoveAt(selectionHistory.Count - 1);
        tabIndex = -1;
    }

    private static bool CheckIsMouseInHierarchy()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - lastMouseCheckTime > 0.05)
        {
            cachedIsMouseInHierarchy = EditorWindow.mouseOverWindow != null &&
                                       EditorWindow.mouseOverWindow.GetType().Name == "SceneHierarchyWindow";
            lastMouseCheckTime = now;
        }
        return cachedIsMouseInHierarchy;
    }

    private static void OnGUI(EntityId instanceID, Rect selectionRect)
    {
        if (!Enabled) return;

        GameObject obj = EditorUtility.EntityIdToObject(instanceID) as GameObject;
        if (obj == null) return;

        InitializeStyles();

        Event e = Event.current;

        isMouseInHierarchy = CheckIsMouseInHierarchy();

        bool isHovering = selectionRect.Contains(e.mousePosition);

        // --- GESTIÓN DE HIGHLIGHT ---
        if (!isMouseInHierarchy) objectToHighlight = null;
        else if (isHovering) objectToHighlight = obj;

        // --- LÓGICA DE BARRIDO CON CONTROL ---
        if (isMouseInHierarchy)
        {
            if (e.control)
            {
                if (isHovering && !obj.activeSelf && !draggedObjectsSession.Contains(instanceID))
                {
                    Undo.RecordObject(obj, "Quick Toggle");
                    obj.SetActive(true);
                    draggedObjectsSession.Add(instanceID);
                }
            }
            else if (draggedObjectsSession.Count > 0)
            {
                foreach (EntityId id in draggedObjectsSession)
                {
                    GameObject dragObj = EditorUtility.EntityIdToObject(id) as GameObject;
                    if (dragObj != null) dragObj.SetActive(false);
                }
                draggedObjectsSession.Clear();
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        // --- LÓGICA TECLA F ---
        if (isHovering && isMouseInHierarchy && e.type == EventType.KeyDown && e.keyCode == KeyCode.F)
        {
            FocusObject(obj);
            e.Use();
        }

        // --- LÓGICA TAB ---
        if (isMouseInHierarchy && e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
        {
            CycleSelection();
            e.Use();
        }

        // --- PREVIEW CON BOTÓN CENTRAL ---
        if (e.type == EventType.Repaint)
        {
            HandleControlPreview(instanceID, obj, isHovering && isMouseInHierarchy, isMiddleDragging);
        }

        if (isHovering && e.type == EventType.MouseDown && e.button == 2)
        {
            isMiddleDragging = true;
            e.Use();
        }
        if (e.type == EventType.MouseUp && e.button == 2)
        {
            isMiddleDragging = false;
            RestorePreview();
            e.Use();
        }

        DrawUI(instanceID, obj, selectionRect);
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        HandleSceneViewTabCycle(sceneView);

        if (!isMouseInHierarchy || objectToHighlight == null) return;

        Bounds b = GetBounds(objectToHighlight);
        Vector3 c = b.center;

        // --- 1. LÍNEAS INFINITAS (CROSSHAIR) ---
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = new Color(1f, 1f, 0f, 0.5f); // Amarillo con algo de transparencia para no molestar tanto

        float infinite = 100000f;
        // Línea Horizontal (Eje X)
        Handles.DrawLine(c + Vector3.left * infinite, c + Vector3.right * infinite);
        // Línea Vertical (Eje Y)
        Handles.DrawLine(c + Vector3.up * infinite, c + Vector3.down * infinite);
        // Línea de Profundidad (Eje Z) - Opcional, pero útil para 3D
        Handles.DrawLine(c + Vector3.forward * infinite, c + Vector3.back * infinite);

        // --- 2. RECUADRO RELLENO ---
        Vector3 ext = b.extents;
        Vector3[] v = new Vector3[] {
            c + new Vector3(-ext.x, -ext.y, -ext.z), c + new Vector3(ext.x, -ext.y, -ext.z),
            c + new Vector3(ext.x, ext.y, -ext.z), c + new Vector3(-ext.x, ext.y, -ext.z),
            c + new Vector3(-ext.x, -ext.y, ext.z), c + new Vector3(ext.x, -ext.y, ext.z),
            c + new Vector3(ext.x, ext.y, ext.z), c + new Vector3(-ext.x, ext.y, ext.z)
        };

        Handles.color = new Color(1f, 1f, 0f, 0.2f);
        Handles.DrawAAConvexPolygon(v[0], v[1], v[2], v[3]);
        Handles.DrawAAConvexPolygon(v[4], v[5], v[6], v[7]);
        Handles.DrawAAConvexPolygon(v[0], v[1], v[5], v[4]);
        Handles.DrawAAConvexPolygon(v[2], v[3], v[7], v[6]);
        Handles.DrawAAConvexPolygon(v[0], v[4], v[7], v[3]);
        Handles.DrawAAConvexPolygon(v[1], v[5], v[6], v[2]);

        // --- 3. BORDE WIREFRAME ---
        Handles.color = new Color(1f, 1f, 0f, 1f);
        Handles.DrawWireCube(b.center, b.size);

        sceneView.Repaint();
    }

    // Tab con el mouse sobre la Scene View: cicla los objetos apilados bajo el cursor
    // (uno por cada Tab), igual que Alt+click repetido pero sin soltar el mouse.
    // Distinto del Tab de la ventana Hierarchy (CycleSelection): ese cicla el HISTORIAL
    // de seleccion reciente; este cicla lo que hay FISICAMENTE bajo el cursor en 3D/UI.
    private static void HandleSceneViewTabCycle(SceneView sceneView)
    {
        if (!Enabled) return;

        // NOTA: isMouseInHierarchy (usado por el Tab del Hierarchy) se actualiza solo
        // cuando esa ventana repinta, asi que puede quedar pegado en "true" si el
        // Hierarchy no repinto despues de que el mouse se fue - bloqueando esto para
        // siempre. No lo usamos aca: focusedWindow ya es mutuamente excluyente con el
        // Hierarchy, asi que no hace falta.
        Event e = Event.current;
        if (e == null) return;
        if (e.type != EventType.KeyDown || e.keyCode != KeyCode.Tab) return;

        // Los eventos de teclado de IMGUI solo llegan a la ventana con foco de teclado
        // (a diferencia de Q/W/E/R/T, que son shortcuts de herramienta especiales que
        // Unity activa solo con mouse-over). Por eso hace falta un click previo dentro
        // de la Scene View para que Tab funcione aca.
        if (!(EditorWindow.focusedWindow is SceneView)) return;

        Vector2 mousePos = e.mousePosition;

        // Si el mouse se movio a otro punto, es un pick nuevo: reiniciar el ciclo.
        if (sceneViewPickIgnore.Count > 0 && Vector2.Distance(mousePos, sceneViewPickMousePos) > SceneViewPickMoveTolerance)
        {
            sceneViewPickIgnore.Clear();
        }
        sceneViewPickIgnore.RemoveAll(go => go == null);

        GameObject picked = HandleUtility.PickGameObject(mousePos, false, sceneViewPickIgnore.ToArray());
        if (picked == null && sceneViewPickIgnore.Count > 0)
        {
            // Se agoto la pila de objetos solapados en este punto: reiniciar y volver
            // a empezar desde el que esta mas arriba.
            sceneViewPickIgnore.Clear();
            picked = HandleUtility.PickGameObject(mousePos, false, null);
        }

        if (picked == null) return;

        sceneViewPickIgnore.Add(picked);
        sceneViewPickMousePos = mousePos;

        Selection.activeGameObject = picked;
        EditorGUIUtility.PingObject(picked);
        e.Use();
    }

    private static Bounds GetBounds(GameObject obj)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Bounds bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) bounds.Encapsulate(corners[i]);
            return bounds;
        }
        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds;
        return new Bounds(obj.transform.position, Vector3.one * 0.5f);
    }

    private static void FocusObject(GameObject obj)
    {
        if (SceneView.lastActiveSceneView == null) return;
        targetPivot = GetBounds(obj).center;
        startPivot = SceneView.lastActiveSceneView.pivot;
        animationStartTime = (float)EditorApplication.timeSinceStartup;
        if (!isAnimatingPivot) { isAnimatingPivot = true; EditorApplication.update += AnimateCamera; }
    }

    private static void AnimateCamera()
    {
        if (SceneView.lastActiveSceneView == null || !isAnimatingPivot) { StopAnimation(); return; }
        float t = Mathf.Clamp01(((float)EditorApplication.timeSinceStartup - animationStartTime) / AnimationDuration);
        float easedT = t * t * (3f - 2f * t);
        SceneView.lastActiveSceneView.pivot = Vector3.Lerp(startPivot, targetPivot, easedT);
        SceneView.lastActiveSceneView.Repaint();
        if (t >= 1f) StopAnimation();
    }

    private static void StopAnimation() { isAnimatingPivot = false; EditorApplication.update -= AnimateCamera; }

    private static void HandleControlPreview(EntityId instanceID, GameObject obj, bool isHovering, bool isTriggered)
    {
        if (isHovering && isTriggered)
        {
            if (!lastHoveredID.Equals(instanceID))
            {
                RestorePreview();
                lastHoveredID = instanceID;
                lastHoveredObj = obj;
                lastStateWasActive = obj.activeSelf;
                if (!lastStateWasActive) obj.SetActive(true);
            }
        }
        else if (lastHoveredID.Equals(instanceID) && (!isHovering || !isTriggered)) { RestorePreview(); }
    }

    private static void RestorePreview()
    {
        if (lastHoveredObj != null)
        {
            if (lastHoveredObj.activeSelf != lastStateWasActive) lastHoveredObj.SetActive(lastStateWasActive);
            lastHoveredObj = null; lastHoveredID = EntityId.None;
        }
    }

    private static void CycleSelection()
    {
        if (selectionHistory.Count == 0) return;
        tabIndex = (tabIndex + 1) % selectionHistory.Count;
        Selection.activeEntityId = selectionHistory[tabIndex];
        EditorGUIUtility.PingObject(Selection.activeEntityId);
    }

    private static void DrawUI(EntityId instanceID, GameObject obj, Rect rect)
    {
        rect = EnumerarElementos(obj, rect);

        // --- 1. COLUMNA DE HISTORIAL (DERECHA) ---
        int hIndex = selectionHistory.IndexOf(instanceID);
        if (hIndex != -1)
        {
            if (hIndex == 0)
            {
                GUI.color = new Color(1f, 1f, 0f, 1f); // Amarillo Patito Chillón
                historyStyle.fontSize = 17;
            }
            else
            {
                float t = (float)(hIndex - 1) / (MaxHistory - 1);
                // Azul eléctrico a azul muy lavado
                GUI.color = Color.Lerp(new Color(0f, 0.4f, 1f, 0.7f), new Color(0f, 0.2f, 0.6f, 0.1f), t);
                historyStyle.fontSize = 12;
            }

            GUI.Label(new Rect(rect.xMax - 18, rect.y, 18, rect.height), "●", historyStyle);
        }

        // --- 2. TOGGLE DE ACTIVIDAD (IZQUIERDA) ---
        Rect toggleRect = new Rect(rect.x - 28, rect.y, 20, rect.height);

        // Cursor de manito para feedback de click
        EditorGUIUtility.AddCursorRect(toggleRect, MouseCursor.Link);

        bool selfActive = obj.activeSelf;
        bool inHierarchy = obj.activeInHierarchy;
        bool isChild = obj.transform.parent != null;

        // --- CONFIGURACIÓN DE COLOR LAVADO ---
        // En lugar de negro (0,0,0), usamos un gris carbón (0.15) para que no sea tan "duro"
        Color charcoal = new Color(0.15f, 0.15f, 0.15f);
        Color greyBase = new Color(0.4f, 0.4f, 0.4f);

        float finalAlpha = 1f;
        Color finalRGB = charcoal;

        if (selfActive)
        {
            finalRGB = charcoal;
            // Reducimos el alpha para que no sea tan fuerte (0.7 para raíz, 0.4 para hijos)
            finalAlpha = isChild ? 0.4f : 0.7f;
        }
        else
        {
            finalRGB = greyBase;
            // Apagado es mucho más sutil
            finalAlpha = isChild ? 0.15f : 0.3f;
        }

        GUI.color = new Color(finalRGB.r, finalRGB.g, finalRGB.b, finalAlpha);

        // ● = Estado propio / ○ = Estado heredado (padre apagado)
        string icon = (selfActive == inHierarchy) ? "●" : "○";

        if (GUI.Button(toggleRect, icon, toggleStyle))
        {
            Undo.RecordObject(obj, "Toggle Active State");
            obj.SetActive(!selfActive);
            EditorApplication.RepaintHierarchyWindow();
        }

        GUI.color = Color.white; // Limpieza de estado de color
    }

    private static Rect EnumerarElementos(GameObject obj, Rect rect)
    {
        // --- 0. ENUMERADO DE ITEMS (MÁS A LA IZQUIERDA) ---
        int itemIndex = GetHierarchyVisibleIndex(obj);
        if (itemIndex != -1)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x - 60, rect.y, 30, rect.height), GetIndexString(itemIndex), numberStyle);
        }
        return rect;
    }

    private static int GetHierarchyVisibleIndex(GameObject obj)
    {
        double now = EditorApplication.timeSinceStartup;
        if (cacheDirty || now - lastCacheTime > 0.5)
        {
            RebuildHierarchyCache();
        }

        if (hierarchyIndexCache.TryGetValue(obj.GetEntityId(), out int index))
        {
            return index;
        }

        return -1;
    }

    private static void RebuildHierarchyCache()
    {
        hierarchyIndexCache.Clear();
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        int index = 0;
        foreach (GameObject root in rootObjects)
        {
            BuildCacheRecursive(root, ref index);
        }
        lastCacheTime = EditorApplication.timeSinceStartup;
        cacheDirty = false;
    }

    private static void BuildCacheRecursive(GameObject current, ref int index)
    {
        hierarchyIndexCache[current.GetEntityId()] = index;
        index++;
        foreach (Transform child in current.transform)
        {
            BuildCacheRecursive(child.gameObject, ref index);
        }
    }
}