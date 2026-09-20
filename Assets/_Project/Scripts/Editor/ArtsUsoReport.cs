using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Item 89: dice cuanto de Assets/ARTS usa de verdad el juego. Raices = todo lo que vive fuera de ARTS (escenas,
    // prefabs, materiales, Resources del proyecto) + las escenas del build; se siguen las dependencias y lo que queda
    // sin tocar se lista con su peso. NO borra nada: borrar es una decision de arte. Salida: Docs/ARTS_USO.csv
    public static class ArtsUsoReport
    {
        [MenuItem("Strategic Point/Reportes/Uso de ARTS (CSV)")]
        public static string Generar()
        {
            var raices = AssetDatabase.FindAssets("", new[] { "Assets/_Project" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p) && !p.EndsWith(".cs") && !p.EndsWith(".meta"))
                .Concat(EditorBuildSettings.scenes.Select(s => s.path)).Distinct().ToArray();

            var usados = new HashSet<string>(AssetDatabase.GetDependencies(raices, true));

            var todos = AssetDatabase.FindAssets("", new[] { "Assets/ARTS" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToList();

            var sb = new StringBuilder("ruta,carpeta,bytes,usado\n");
            var porCarpeta = new SortedDictionary<string, long[]>();   // [usados, sin usar]
            long totalUsado = 0, totalSin = 0;
            foreach (var p in todos)
            {
                long bytes = File.Exists(p) ? new FileInfo(p).Length : 0;
                bool usa = usados.Contains(p);
                var partes = p.Split('/');
                string carpeta = string.Join("/", partes.Take(Mathf.Min(4, partes.Length - 1)));
                if (!porCarpeta.TryGetValue(carpeta, out var acc)) porCarpeta[carpeta] = acc = new long[2];
                acc[usa ? 0 : 1] += bytes;
                if (usa) totalUsado += bytes; else totalSin += bytes;
                sb.Append('"').Append(p).Append("\",\"").Append(carpeta).Append("\",").Append(bytes).Append(',').Append(usa ? "si" : "no").Append('\n');
            }
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/ARTS_USO.csv", sb.ToString(), new UTF8Encoding(false));

            var res = new StringBuilder();
            res.Append("ARTS: ").Append(todos.Count).Append(" archivos, ").Append((totalUsado + totalSin) / 1048576).Append(" MB; usados ")
               .Append(totalUsado / 1048576).Append(" MB, sin usar ").Append(totalSin / 1048576).Append(" MB.\n");
            foreach (var kv in porCarpeta.OrderByDescending(k => k.Value[1]).Take(12))
                res.Append("  ").Append(kv.Key).Append(": sin usar ").Append(kv.Value[1] / 1048576).Append(" MB, usado ").Append(kv.Value[0] / 1048576).Append(" MB\n");
            Debug.Log(res.ToString());
            return res.ToString();
        }
    }
}
