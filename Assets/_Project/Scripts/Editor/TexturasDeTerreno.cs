using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Capas extra del terreno del nivel, generadas por codigo (mismo estilo de manchas suaves de 128 px que las de pasto y
    // tierra de siempre, sin bajar assets de afuera):
    //   Grava ....... el camino de grava (carretera central, camino de servicio, patios).
    //   Hojarasca ... el piso oscuro del bosque (mezcla de tierra humeda, hojas y agujas de pino).
    // Ambas son "tileables" (el ruido envuelve en los bordes), asi que repiten sin costura.
    public static class TexturasDeTerreno
    {
        const string Carpeta = "Assets/_Project/Terrains";
        public const string RutaGrava = Carpeta + "/GravelLayer.terrainlayer";
        public const string RutaHojarasca = Carpeta + "/LitterLayer.terrainlayer";

        public static TerrainLayer Grava() => Asegurar("Gravel", RutaGrava, 7f, (u, v) =>
        {
            // Base gris calida con manchas, mas granitos claros y oscuros sueltos.
            float m = Ruido(u, v, 6, 11) * 0.5f + Ruido(u, v, 14, 23) * 0.3f + Ruido(u, v, 40, 37) * 0.2f;
            float granoClaro = Ruido(u, v, 64, 5) > 0.83f ? 0.22f : 0f;
            float granoOscuro = Ruido(u, v, 64, 91) > 0.86f ? -0.2f : 0f;
            float k = 0.46f + (m - 0.5f) * 0.32f + granoClaro + granoOscuro;
            return new Color(k * 1.02f, k * 0.97f, k * 0.9f, 1f);
        });

        public static TerrainLayer Hojarasca() => Asegurar("Litter", RutaHojarasca, 8f, (u, v) =>
        {
            // Marron verdoso oscuro; algunas hojas secas mas claras y agujas mas oscuras.
            float m = Ruido(u, v, 5, 3) * 0.55f + Ruido(u, v, 16, 17) * 0.3f + Ruido(u, v, 48, 29) * 0.15f;
            float hoja = Ruido(u, v, 32, 71) > 0.8f ? 0.1f : 0f;
            float aguja = Ruido(u, v, 96, 13) > 0.78f ? -0.07f : 0f;
            float k = 0.22f + (m - 0.5f) * 0.2f + hoja + aguja;
            return new Color(k * 1.0f, k * 0.86f, k * 0.5f, 1f);
        });

        static TerrainLayer Asegurar(string nombre, string rutaLayer, float tile, System.Func<float, float, Color> color)
        {
            var existente = AssetDatabase.LoadAssetAtPath<TerrainLayer>(rutaLayer);
            if (existente != null && existente.diffuseTexture != null) { existente.tileSize = new Vector2(tile, tile); return existente; }

            string rutaPng = Carpeta + "/" + nombre + "_Diffuse.png";
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var pix = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    pix[y * N + x] = color(x / (float)N, y / (float)N);
            tex.SetPixels(pix);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), rutaPng), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(rutaPng, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(rutaPng);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();

            var capa = existente != null ? existente : new TerrainLayer();
            capa.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaPng);
            capa.tileSize = new Vector2(tile, tile);
            capa.smoothness = 0f;
            capa.metallic = 0f;
            if (existente == null) AssetDatabase.CreateAsset(capa, rutaLayer);
            else EditorUtility.SetDirty(capa);
            AssetDatabase.SaveAssets();
            return capa;
        }

        // Ruido de valor periodico (celdas x celdas por tile) con interpolacion suave; devuelve 0..1.
        static float Ruido(float u, float v, int celdas, int semilla)
        {
            float fx = u * celdas, fy = v * celdas;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0, celdas, semilla), b = Hash(x0 + 1, y0, celdas, semilla);
            float c = Hash(x0, y0 + 1, celdas, semilla), d = Hash(x0 + 1, y0 + 1, celdas, semilla);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Hash(int x, int y, int celdas, int semilla)
        {
            x = ((x % celdas) + celdas) % celdas;
            y = ((y % celdas) + celdas) % celdas;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + semilla * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
