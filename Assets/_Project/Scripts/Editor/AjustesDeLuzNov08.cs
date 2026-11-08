using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Bugs #382 y #387 (2026-11-08): "baja el brillo de las ventanas, es mucho" y "el resto de luces deberian tener menos intensidad".
    // Escala la emision de los materiales de ventanas y cristales. NO es idempotente (cada corrida multiplica otra vez): se corre una sola vez
    // y los .mat quedan guardados; el registro de lo aplicado esta en el commit.
    public static class AjustesDeLuzNov08
    {
        public const float Factor = 0.55f;
        static readonly string[] Patrones = { "VentanaCalida", "VentanaFria", "VentanaLuz", "M_Op_Mansion_Cristal" };

        [MenuItem("Strategic Point/Arte/11. Bajar brillo de ventanas (una sola vez)")]
        public static string Aplicar()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (!ruta.StartsWith("Assets/_Project/")) continue;
                string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
                bool coincide = false; foreach (var p in Patrones) if (nombre.Contains(p)) { coincide = true; break; }
                if (!coincide) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (m == null || !m.HasProperty("_EmissionColor")) continue;
                var antes = m.GetColor("_EmissionColor");
                if (antes.maxColorComponent <= 0.001f) continue;
                m.SetColor("_EmissionColor", new Color(antes.r * Factor, antes.g * Factor, antes.b * Factor, antes.a));
                EditorUtility.SetDirty(m);
                sb.AppendLine($"{nombre}: {antes.maxColorComponent:0.00} -> {antes.maxColorComponent * Factor:0.00}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[AjustesDeLuzNov08]\n" + sb);
            return sb.ToString();
        }
    }
}
