using System.IO;
using UnityEditor;
using UnityEngine;

public static class TerrainSetupHelper
{
    [MenuItem("Tools/Terrain/Fix and Setup Paintable URP Terrain")]
    public static void FixAndSetupTerrain()
    {
        // 1. Asegurar carpeta para assets de terreno
        string folder = "Assets/_Project/Terrains";
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
        {
            AssetDatabase.CreateFolder("Assets", "_Project");
        }
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project", "Terrains");
        }

        // 2. Crear dos texturas PNG reales si no existen para pintar
        string texGrassPath = folder + "/Grass_Diffuse.png";
        string texDirtPath = folder + "/Dirt_Diffuse.png";

        if (!File.Exists(texGrassPath))
        {
            Texture2D texGrass = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Color grassBase = new Color(0.22f, 0.45f, 0.15f);
            Color grassVar = new Color(0.28f, 0.52f, 0.18f);
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f);
                    texGrass.SetPixel(x, y, Color.Lerp(grassBase, grassVar, noise));
                }
            }
            texGrass.Apply();
            File.WriteAllBytes(texGrassPath, texGrass.EncodeToPNG());
            AssetDatabase.ImportAsset(texGrassPath);
        }

        if (!File.Exists(texDirtPath))
        {
            Texture2D texDirt = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Color dirtBase = new Color(0.55f, 0.35f, 0.18f);
            Color dirtVar = new Color(0.42f, 0.25f, 0.12f);
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    texDirt.SetPixel(x, y, Color.Lerp(dirtBase, dirtVar, noise));
                }
            }
            texDirt.Apply();
            File.WriteAllBytes(texDirtPath, texDirt.EncodeToPNG());
            AssetDatabase.ImportAsset(texDirtPath);
        }

        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texGrassPath);
        Texture2D dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDirtPath);

        // 3. Crear o cargar TerrainLayers como assets persistentes
        string layerGrassPath = folder + "/GrassLayer.terrainlayer";
        string layerDirtPath = folder + "/DirtLayer.terrainlayer";

        TerrainLayer layerGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerGrassPath);
        if (layerGrass == null)
        {
            layerGrass = new TerrainLayer
            {
                diffuseTexture = grassTex,
                tileSize = new Vector2(10, 10),
                name = "GrassLayer"
            };
            AssetDatabase.CreateAsset(layerGrass, layerGrassPath);
        }
        else
        {
            layerGrass.diffuseTexture = grassTex;
            EditorUtility.SetDirty(layerGrass);
        }

        TerrainLayer layerDirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerDirtPath);
        if (layerDirt == null)
        {
            layerDirt = new TerrainLayer
            {
                diffuseTexture = dirtTex,
                tileSize = new Vector2(10, 10),
                name = "DirtLayer"
            };
            AssetDatabase.CreateAsset(layerDirt, layerDirtPath);
        }
        else
        {
            layerDirt.diffuseTexture = dirtTex;
            EditorUtility.SetDirty(layerDirt);
        }

        // 4. Crear o cargar Material URP de Terreno
        string matPath = folder + "/M_Terrain_URP.mat";
        Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        Shader urpTerrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (urpTerrainShader == null)
        {
            urpTerrainShader = Shader.Find("Nature/Terrain/Standard");
        }
        if (terrainMat == null)
        {
            terrainMat = new Material(urpTerrainShader) { name = "M_Terrain_URP" };
            AssetDatabase.CreateAsset(terrainMat, matPath);
        }
        else
        {
            if (terrainMat.shader != urpTerrainShader && urpTerrainShader != null)
            {
                terrainMat.shader = urpTerrainShader;
                EditorUtility.SetDirty(terrainMat);
            }
        }

        // 5. Cargar o crear TerrainData como Asset persistente
        string dataPath = folder + "/TerrainData_Main.asset";
        TerrainData tData = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (tData == null)
        {
            tData = new TerrainData
            {
                heightmapResolution = 513,
                size = new Vector3(200, 30, 200)
            };
            AssetDatabase.CreateAsset(tData, dataPath);
        }

        tData.terrainLayers = new TerrainLayer[] { layerGrass, layerDirt };

        // 6. Buscar o instanciar el GameObject de Terrain en la escena
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        GameObject terrainGO;
        if (terrain == null)
        {
            terrainGO = new GameObject("Terrain");
            terrain = terrainGO.AddComponent<Terrain>();
            var col = terrainGO.AddComponent<TerrainCollider>();
            col.terrainData = tData;
        }
        else
        {
            terrainGO = terrain.gameObject;
            terrainGO.name = "Terrain";
            var col = terrainGO.GetComponent<TerrainCollider>();
            if (col == null) col = terrainGO.AddComponent<TerrainCollider>();
            col.terrainData = tData;
        }

        terrain.terrainData = tData;
        terrain.materialTemplate = terrainMat;
        terrainGO.transform.position = new Vector3(-100, 0, -100);

        // 7. Pintar una zona visible (camino/círculo central con la capa de tierra)
        int w = tData.alphamapWidth;
        int h = tData.alphamapHeight;
        float[,,] alphamaps = new float[w, h, 2];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = (x - w * 0.5f);
                float dy = (y - h * 0.5f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Crear un círculo central y un camino en diagonal
                bool isPath = Mathf.Abs(x - y) < (w * 0.05f) && dist < (w * 0.35f);
                bool isCenter = dist < (w * 0.15f);

                if (isCenter || isPath)
                {
                    float blend = Mathf.Clamp01(1f - (dist / (w * 0.2f)));
                    alphamaps[x, y, 0] = 1f - blend; // Capa base (Grass)
                    alphamaps[x, y, 1] = blend;      // Capa pintura (Dirt)
                }
                else
                {
                    alphamaps[x, y, 0] = 1f;
                    alphamaps[x, y, 1] = 0f;
                }
            }
        }

        tData.SetAlphamaps(0, 0, alphamaps);

        EditorUtility.SetDirty(tData);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log("[TerrainSetupHelper] Terreno URP configurado, capas serializadas en disco y zona pintada con exito.");
    }

    [MenuItem("Tools/Terrain/Capture Terrain Snapshot")]
    public static void CaptureSnapshot()
    {
        string screenshotPath = "C:/Users/PC_JOACO/.gemini/antigravity/brain/3fd48ba0-ab9b-487b-8724-0a2b500b10d0/terrain_painted_fixed.png";

        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();

        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 70, -60);
            cam.transform.rotation = Quaternion.Euler(50, 0, 0);

            RenderTexture rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            Texture2D screenShot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            screenShot.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(screenshotPath, bytes);
            Debug.Log("[TerrainSetupHelper] Captura guardada en: " + screenshotPath);
        }
    }

    [MenuItem("Tools/Inspection/PrintMinimapHierarchy")]
    public static void PrintMinimapHierarchy()
    {
        var border = GameObject.Find("MinimapBorder");
        if (border == null)
        {
            Debug.LogError("No se encontro MinimapBorder con GameObject.Find");
            return;
        }

        var rt = border.GetComponent<RectTransform>();
        Debug.Log($"[MINIMAP] Border: sizeDelta={rt.sizeDelta}, anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}, pivot={rt.pivot}, anchoredPos={rt.anchoredPosition}");
        for (int i = 0; i < rt.childCount; i++)
        {
            var child = rt.GetChild(i) as RectTransform;
            Debug.Log($"[MINIMAP] Child {i} ({child.name}): sizeDelta={child.sizeDelta}, anchorMin={child.anchorMin}, anchorMax={child.anchorMax}, pivot={child.pivot}, anchoredPos={child.anchoredPosition}");
            for (int j = 0; j < child.childCount; j++)
            {
                var grandChild = child.GetChild(j) as RectTransform;
                Debug.Log($"[MINIMAP]   GrandChild {j} ({grandChild.name}): sizeDelta={grandChild.sizeDelta}, anchorMin={grandChild.anchorMin}, anchorMax={grandChild.anchorMax}, pivot={grandChild.pivot}, anchoredPos={grandChild.anchoredPosition}");
            }
        }
    }

    [MenuItem("Tools/Minimap/Resize Minimap To One Third")]
    public static void ResizeMinimap()
    {
        var border = GameObject.Find("MinimapBorder");
        if (border == null)
        {
            Debug.LogError("[ResizeMinimap] No se encontro MinimapBorder en la escena activa.");
            return;
        }

        var rt = border.GetComponent<RectTransform>();
        Undo.RecordObject(rt, "Resize Minimap To One Third");
        Vector2 oldSize = rt.sizeDelta;
        rt.sizeDelta = new Vector2(oldSize.x / 3f, oldSize.y / 3f);
        EditorUtility.SetDirty(rt);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ResizeMinimap] MinimapBorder redimensionado exitosamente de {oldSize} a {rt.sizeDelta}. Escena guardada.");
    }

    [MenuItem("Tools/Verification/VerifyAndCapture")]
    public static void EnsureGameplaySceneAndCapture()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/SC_Gameplay.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        var border = GameObject.Find("MinimapBorder");
        var rt = border != null ? border.GetComponent<RectTransform>() : null;
        var agent = Object.FindFirstObjectByType<UnityEngine.AI.NavMeshAgent>();

        Debug.Log($"[VERIFICATION] Escena: {scene.name}");
        Debug.Log($"[VERIFICATION] NavMeshSurface presente: {surface != null}");
        Debug.Log($"[VERIFICATION] MinimapBorder sizeDelta: {(rt != null ? rt.sizeDelta.ToString() : "null")}");
        Debug.Log($"[VERIFICATION] NavMeshAgent encontrado en soldados: {agent != null}");

        CaptureSnapshot();
    }

    [MenuItem("Tools/Debug/DiagnoseTestFailure")]
    public static void DiagnoseTestFailure()
    {
        var vega = GameObject.Find("Soldado_1_Vega")?.GetComponent<SP.Actors.Soldier>();
        var kes = GameObject.Find("Soldado_2_Kes")?.GetComponent<SP.Actors.Soldier>();
        var doc = GameObject.Find("Soldado_3_Doc")?.GetComponent<SP.Actors.Soldier>();
        var inputDriver = Object.FindFirstObjectByType<SP.Player.PlayerInputDriver>();

        Debug.Log($"[DIAGNOSE] vega={vega != null}, kes={kes != null}, doc={doc != null}, inputDriver={inputDriver != null}");
        if (kes != null)
        {
            var agent = kes.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Debug.Log($"[DIAGNOSE] kes agent: {(agent != null ? "exists" : "null")}, enabled={(agent != null ? agent.enabled.ToString() : "N/A")}, isOnNavMesh={(agent != null ? agent.isOnNavMesh.ToString() : "N/A")}");
        }
        if (inputDriver != null)
        {
            var dests = inputDriver.DestinatariosDeOrden();
            Debug.Log($"[DIAGNOSE] Destinatarios: {dests.Count}");
            foreach (var d in dests) Debug.Log($"[DIAGNOSE] Destinatario: {d.name}, isAlive={d.Health?.IsAlive}, loManejaElJugador={SP.Player.OrderService.LoManejaElJugador(d)}");
            bool res = inputDriver.EjecutarOrdenDelMenu(1);
            Debug.Log($"[DIAGNOSE] EjecutarOrdenDelMenu(1) res={res}");
            if (kes != null) Debug.Log($"[DIAGNOSE] kes.Brain.State={kes.Brain?.State}, HasOrder={kes.Brain?.CurrentOrderDestination.HasValue}");
            if (doc != null) Debug.Log($"[DIAGNOSE] doc.Brain.State={doc.Brain?.State}, HasOrder={doc.Brain?.CurrentOrderDestination.HasValue}");
        }
    }
}

