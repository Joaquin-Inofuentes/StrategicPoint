// SP_Arte — puesta a punto automatica de los assets de arte.
//
// Que resuelve:
//   1. El trimsheet es un atlas de color plano. Si Unity lo importa con filtro bilineal o
//      compresion DXT, las celdas se mezclan entre si y aparecen colores que no existen.
//      Hay que forzar Point + sin compresion. Eso lo hace el AssetPostprocessor de abajo,
//      automaticamente, sin que nadie tenga que acordarse.
//   2. Los FBX no traen material propio a proposito: se les asigna MAT_Trimsheet, uno solo
//      para todo el arte. Antes habia un material por objeto.
//
// Uso: menu Strategic Point > Arte > Trimsheet > Hacer todo.
// Convive con ArtSetup.cs y ArtBuilder.cs del proyecto: distinto namespace,
// distinta clase y submenu propio, no se pisan.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace SPArte
{
    /// <summary>Ajustes de importacion. Corren solos cada vez que Unity (re)importa el asset.</summary>
    public class SP_ArteImportSettings : AssetPostprocessor
    {
        const string CARPETA = "Assets/ARTS/SP_Arte";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(CARPETA + "/Trimsheet_1024")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType        = TextureImporterType.Default;
            ti.filterMode         = FilterMode.Point;          // sin esto las celdas se mezclan
            ti.wrapMode           = TextureWrapMode.Clamp;
            ti.mipmapEnabled      = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.sRGBTexture        = true;
            ti.maxTextureSize     = 1024;
            ti.alphaSource        = TextureImporterAlphaSource.None;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').Contains(CARPETA + "/_FBX_Export/")) return;

            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode  = ModelImporterMaterialImportMode.None; // el material se asigna aparte
            mi.importCameras       = false;
            mi.importLights        = false;
            mi.importVisibility    = false;
            mi.importAnimation     = false;
            mi.importBlendShapes   = false;
            mi.weldVertices        = false;                    // conservar las UV por cara
            mi.importNormals       = ModelImporterNormals.Import;
            mi.importTangents      = ModelImporterTangents.None;
            mi.meshOptimizationFlags = MeshOptimizationFlags.Everything;
        }
    }

    public static class SP_ArteSetup
    {
        const string ARTE    = "Assets/ARTS/SP_Arte";
        const string FBX     = ARTE + "/_FBX_Export";
        const string TEX     = ARTE + "/Trimsheet_1024.png";
        const string MATDIR  = "Assets/_Project/Materials/Arte";
        const string MATPATH = MATDIR + "/MAT_Trimsheet.mat";
        const string PREFDIR = "Assets/_Project/Prefabs/SP_Arte";

        [MenuItem("Strategic Point/Arte/Trimsheet/1. Crear material del trimsheet")]
        public static void MenuMaterial()
        {
            var m = CrearMaterial();
            Debug.Log(m != null
                ? "SP_Arte: material listo en " + MATPATH
                : "SP_Arte: no se pudo crear el material.");
        }

        [MenuItem("Strategic Point/Arte/Trimsheet/2. Generar prefabs desde _FBX_Export")]
        public static void MenuPrefabs()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MATPATH) ?? CrearMaterial();
            if (m == null) { Debug.LogError("SP_Arte: falta el material, corre el paso 1."); return; }
            Debug.Log("SP_Arte: " + GenerarPrefabs(m) + " prefabs en " + PREFDIR);
        }

        [MenuItem("Strategic Point/Arte/Trimsheet/Hacer todo")]
        public static void MenuTodo()
        {
            var m = CrearMaterial();
            if (m == null) { Debug.LogError("SP_Arte: no se pudo crear el material."); return; }
            int n = GenerarPrefabs(m);
            Debug.Log("SP_Arte: listo. Material " + MATPATH + " y " + n + " prefabs en " + PREFDIR);
        }

        // ------------------------------------------------------------------

        static Material CrearMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX);
            if (tex == null) { Debug.LogError("SP_Arte: no encuentro " + TEX); return null; }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) { Debug.LogError("SP_Arte: no encuentro el shader de URP."); return null; }

            AsegurarCarpeta(MATDIR);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MATPATH);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MATPATH);
            }
            mat.shader = shader;

            // URP/Lit usa _BaseMap; Standard usa _MainTex. Se setean los dos que existan.
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.12f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static int GenerarPrefabs(Material mat)
        {
            AsegurarCarpeta(PREFDIR);
            if (!AssetDatabase.IsValidFolder(FBX))
            {
                Debug.LogError("SP_Arte: no encuentro la carpeta " + FBX);
                return 0;
            }

            var guids = AssetDatabase.FindAssets("t:Model", new[] { FBX });
            int hechos = 0;

            foreach (var guid in guids)
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (modelo == null) continue;

                var inst = Object.Instantiate(modelo);
                inst.name = Path.GetFileNameWithoutExtension(ruta);

                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }

                PrefabUtility.SaveAsPrefabAsset(inst, PREFDIR + "/" + inst.name + ".prefab");
                Object.DestroyImmediate(inst);
                hechos++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return hechos;
        }

        static void AsegurarCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            var partes = ruta.Split('/');
            var acum = partes[0];                       // "Assets"
            for (int i = 1; i < partes.Length; i++)
            {
                var sig = acum + "/" + partes[i];
                if (!AssetDatabase.IsValidFolder(sig)) AssetDatabase.CreateFolder(acum, partes[i]);
                acum = sig;
            }
        }
    }
}
