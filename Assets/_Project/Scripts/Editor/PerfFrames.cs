using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace SP.EditorTools
{
    // Captura el profiler del Editor sin abrir la ventana y vuelca, para los frames mas lentos, el camino mas caro del arbol de la hebra principal.
    //   return SP.EditorTools.PerfFrames.Capturar();   // empieza a grabar (Play)
    //   return SP.EditorTools.PerfFrames.Analizar(300f);   // frames de mas de 300 ms -> Temp/perf_frames.txt
    public static class PerfFrames
    {
        public static string Capturar()
        {
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
            Profiler_Enable(true);
            return "grabando";
        }

        static void Profiler_Enable(bool on) { UnityEngine.Profiling.Profiler.enabled = on; }

        public static string Analizar(float umbralMs = 300f, int maxFrames = 3)
        {
            Profiler_Enable(false); ProfilerDriver.enabled = false;
            int a = ProfilerDriver.firstFrameIndex, b = ProfilerDriver.lastFrameIndex;
            var sb = new StringBuilder();
            var lentos = new List<KeyValuePair<float, int>>();
            for (int f = a; f <= b; f++)
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
                    if (v != null && v.valid && v.frameTimeMs >= umbralMs) lentos.Add(new KeyValuePair<float, int>(v.frameTimeMs, f));
            sb.AppendLine($"frames grabados={b - a + 1} (indices {a}..{b}), lentos>={umbralMs}ms: {lentos.Count}");
            lentos.Sort((x, y) => y.Key.CompareTo(x.Key));
            for (int i = 0; i < lentos.Count && i < maxFrames; i++)
            {
                int f = lentos[i].Value;
                sb.AppendLine($"=== frame {f}: {lentos[i].Key:F0} ms ===");
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, true))
                    Bajar(v, v.GetRootItemID(), 0, sb);
            }
            System.IO.File.WriteAllText("Temp/perf_frames.txt", sb.ToString());
            return sb.ToString().Split('\n')[0];
        }

        // Agrega por nombre de marcador (hasta profundidad 9, solo hebra principal) el tiempo total, el maximo en un frame y en cuantos frames paso de 3 ms.
        public static string Resumen(float minMs = 1.5f, string filtro = null)
        {
            Profiler_Enable(false); ProfilerDriver.enabled = false;
            int a = ProfilerDriver.firstFrameIndex, b = ProfilerDriver.lastFrameIndex;
            var tot = new Dictionary<string, double>(); var max = new Dictionary<string, float>(); var altos = new Dictionary<string, int>();
            var frameMs = new List<float>();
            for (int f = a; f <= b; f++)
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, true))
                {
                    if (v == null || !v.valid) continue;
                    frameMs.Add(v.frameTimeMs);
                    Acumular(v, v.GetRootItemID(), 0, minMs, tot, max, altos);
                }
            frameMs.Sort();
            var sb = new StringBuilder();
            sb.AppendLine($"frames={frameMs.Count} mediana={frameMs[frameMs.Count / 2]:F1}ms p99={frameMs[(int)(frameMs.Count * 0.99f)]:F1}ms max={frameMs[frameMs.Count - 1]:F0}ms");
            var porMax = new List<KeyValuePair<string, float>>(max); porMax.Sort((x, y) => y.Value.CompareTo(x.Value));
            sb.AppendLine("--- por MAXIMO en un frame ---");
            for (int i = 0; i < porMax.Count && i < 25; i++) sb.AppendLine($"{porMax[i].Key}  max={porMax[i].Value:F1}ms  total={tot[porMax[i].Key]:F0}ms  frames>3ms={altos[porMax[i].Key]}");
            var porTot = new List<KeyValuePair<string, double>>(tot); porTot.Sort((x, y) => y.Value.CompareTo(x.Value));
            if (filtro != null) porTot = porTot.FindAll(kv => kv.Key.Contains(filtro));
            sb.AppendLine("--- por TOTAL ---");
            for (int i = 0; i < porTot.Count && i < 45; i++) sb.AppendLine($"{porTot[i].Key}  total={porTot[i].Value:F0}ms  max={max[porTot[i].Key]:F1}ms");
            System.IO.File.WriteAllText("Temp/perf_resumen.txt", sb.ToString());
            return sb.ToString().Split('\n')[0];
        }

        static void Acumular(HierarchyFrameDataView v, int id, int prof, float minMs, Dictionary<string, double> tot, Dictionary<string, float> max, Dictionary<string, int> altos)
        {
            var hijos = new List<int>(); v.GetItemChildren(id, hijos);
            foreach (var h in hijos)
            {
                float t = v.GetItemColumnDataAsFloat(h, HierarchyFrameDataView.columnTotalTime);
                if (t < minMs) continue;
                string n = v.GetItemName(h);
                tot.TryGetValue(n, out var x); tot[n] = x + t;
                max.TryGetValue(n, out var m); if (t > m) max[n] = t;
                if (t >= 3f) { altos.TryGetValue(n, out var c); altos[n] = c + 1; }
                if (prof < 9) Acumular(v, h, prof + 1, minMs, tot, max, altos);
            }
        }

        static void Bajar(HierarchyFrameDataView v, int id, int prof, StringBuilder sb)
        {
            var hijos = new List<int>(); v.GetItemChildren(id, hijos);
            hijos.Sort((x, y) => v.GetItemColumnDataAsFloat(y, HierarchyFrameDataView.columnTotalTime).CompareTo(v.GetItemColumnDataAsFloat(x, HierarchyFrameDataView.columnTotalTime)));
            for (int i = 0; i < hijos.Count && i < 3; i++)
            {
                float t = v.GetItemColumnDataAsFloat(hijos[i], HierarchyFrameDataView.columnTotalTime);
                if (t < 20f) break;
                sb.Append(' ', prof * 2).Append(v.GetItemName(hijos[i])).Append("  ").Append(t.ToString("F0")).Append(" ms  self=")
                  .Append(v.GetItemColumnDataAsFloat(hijos[i], HierarchyFrameDataView.columnSelfTime).ToString("F0")).AppendLine();
                if (i == 0 && prof < 14) Bajar(v, hijos[i], prof + 1, sb);
                else if (prof < 14) Bajar(v, hijos[i], prof + 1, sb);
            }
        }
    }
}
