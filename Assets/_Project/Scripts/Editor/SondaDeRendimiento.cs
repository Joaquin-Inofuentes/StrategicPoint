using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace SP.EditorTools
{
    // Sonda de rendimiento por CLI (en Play):
    //   eval "return SP.EditorTools.SondaDeRendimiento.Iniciar();"   -> prende el Profiler del editor
    //   (jugar/esperar unos segundos)
    //   eval "return SP.EditorTools.SondaDeRendimiento.Informe(30);" -> top de marcadores por tiempo PROPIO promedio (ms/frame)
    //                                                                     + top por tiempo TOTAL, sobre los frames grabados
    // Sirve para encontrar que script se come el frame sin abrir la ventana del Profiler.
    public static class SondaDeRendimiento
    {
        public static string Iniciar()
        {
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.enabled = true;
            UnityEngine.Profiling.Profiler.enabled = true;
            return "profiler grabando";
        }

        public static string Detener()
        {
            ProfilerDriver.enabled = false;
            return "profiler detenido";
        }

        // Los "tirones": los peores frames grabados con sus marcadores de mas tiempo propio (sin los del profiler).
        public static string Picos(int cuantos = 6, int porFrame = 10, int maxFrames = 400)
        {
            int primero = ProfilerDriver.firstFrameIndex, ultimo = ProfilerDriver.lastFrameIndex;
            if (ultimo < 0) return "sin frames";
            primero = Mathf.Max(primero, ultimo - maxFrames + 1);
            var tiempos = new List<(int f, double ms)>();
            for (int f = primero; f <= ultimo; f++)
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
                    if (v != null && v.valid) tiempos.Add((f, v.frameTimeMs));
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            int lentos = tiempos.Count(t => t.ms > 50);
            sb.AppendLine($"frames={tiempos.Count} >50ms={lentos} >100ms={tiempos.Count(t => t.ms > 100)}");
            var hijos = new List<int>();
            foreach (var (f, ms) in tiempos.OrderByDescending(t => t.ms).Take(cuantos))
            {
                var propio = new Dictionary<string, double>();
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
                {
                    var pila = new Stack<int>(); pila.Push(v.GetRootItemID());
                    while (pila.Count > 0)
                    {
                        int id = pila.Pop();
                        if (id != v.GetRootItemID())
                        {
                            string n = v.GetItemName(id);
                            if (!n.StartsWith("Profiler.")) { propio.TryGetValue(n, out var a); propio[n] = a + v.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnSelfTime); }
                        }
                        hijos.Clear(); v.GetItemChildren(id, hijos); foreach (var h in hijos) pila.Push(h);
                    }
                }
                sb.AppendLine($"== frame {f}: {ms.ToString("0.0", ci)} ms");
                foreach (var kv in propio.OrderByDescending(k => k.Value).Take(porFrame)) sb.AppendLine($"   {kv.Value.ToString("0.0", ci)}  {kv.Key}");
            }
            return sb.ToString();
        }

        public static string Informe(int top = 30, int maxFrames = 300)
        {
            int primero = ProfilerDriver.firstFrameIndex, ultimo = ProfilerDriver.lastFrameIndex;
            if (ultimo < 0) return "sin frames (llamar Iniciar() en Play)";
            primero = Mathf.Max(primero, ultimo - maxFrames + 1);
            var propio = new Dictionary<string, double>();
            var total = new Dictionary<string, double>();
            double frameMs = 0, peor = 0; int frames = 0;
            var hijos = new List<int>();
            for (int f = primero; f <= ultimo; f++)
            {
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
                {
                    if (v == null || !v.valid) continue;
                    frames++;
                    double ms = v.frameTimeMs; frameMs += ms; if (ms > peor) peor = ms;
                    var pila = new Stack<(int id, int prof)>();
                    pila.Push((v.GetRootItemID(), 0));
                    while (pila.Count > 0)
                    {
                        var (id, prof) = pila.Pop();
                        if (id != v.GetRootItemID())
                        {
                            string n = v.GetItemName(id);
                            double s = v.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnSelfTime);
                            double t = v.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnTotalTime);
                            propio.TryGetValue(n, out var a); propio[n] = a + s;
                            if (prof <= 6) { total.TryGetValue(n, out var b); total[n] = System.Math.Max(b, 0) + t; }
                        }
                        hijos.Clear(); v.GetItemChildren(id, hijos);
                        foreach (var h in hijos) pila.Push((h, prof + 1));
                    }
                }
            }
            if (frames == 0) return "sin datos validos";
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"frames={frames} ms_prom={(frameMs / frames).ToString("0.0", ci)} fps~{(1000.0 / (frameMs / frames)).ToString("0", ci)} peor_ms={peor.ToString("0.0", ci)}");
            sb.AppendLine("-- PROPIO (ms/frame) --");
            foreach (var kv in propio.OrderByDescending(k => k.Value).Take(top)) sb.AppendLine($"{(kv.Value / frames).ToString("0.00", ci)}  {kv.Key}");
            sb.AppendLine("-- TOTAL (ms/frame, prof<=6) --");
            foreach (var kv in total.OrderByDescending(k => k.Value).Take(top)) sb.AppendLine($"{(kv.Value / frames).ToString("0.00", ci)}  {kv.Key}");
            return sb.ToString();
        }

        // WP11: arbol (de arriba hacia abajo) de los frames mas lentos que NO son del editor (sin contar los frames donde EditorLoop es el grueso),
        // mostrando solo los nodos de al menos minMs. Sirve para ver QUIEN llama a AddComponent / Instantiate en un tiron de 100 ms.
        public static string Arbol(double minMs = 3, int cuantos = 2, int maxFrames = 400, int maxLineas = 60)
        {
            int primero = ProfilerDriver.firstFrameIndex, ultimo = ProfilerDriver.lastFrameIndex;
            if (ultimo < 0) return "sin frames";
            primero = Mathf.Max(primero, ultimo - maxFrames + 1);
            var tiempos = new List<(int f, double ms)>();
            for (int f = primero; f <= ultimo; f++)
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false))
                {
                    if (v == null || !v.valid) continue;
                    double editor = 0;
                    var hs = new List<int>(); v.GetItemChildren(v.GetRootItemID(), hs);
                    foreach (var h in hs) if (v.GetItemName(h) == "EditorLoop") editor = v.GetItemColumnDataAsDouble(h, HierarchyFrameDataView.columnTotalTime);
                    tiempos.Add((f, v.frameTimeMs - editor));
                }
            var ci = CultureInfo.InvariantCulture; var sb = new StringBuilder();
            foreach (var (f, ms) in tiempos.OrderByDescending(t => t.ms).Take(cuantos))
            {
                sb.AppendLine($"== frame {f}: {ms.ToString("0.0", ci)} ms sin EditorLoop");
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnTotalTime, false))
                {
                    int lineas = 0;
                    void Rec(int id, int nivel)
                    {
                        if (lineas >= maxLineas) return;
                        var hs = new List<int>(); v.GetItemChildren(id, hs);
                        hs.Sort((a, b) => v.GetItemColumnDataAsDouble(b, HierarchyFrameDataView.columnTotalTime).CompareTo(v.GetItemColumnDataAsDouble(a, HierarchyFrameDataView.columnTotalTime)));
                        foreach (var h in hs)
                        {
                            double t = v.GetItemColumnDataAsDouble(h, HierarchyFrameDataView.columnTotalTime);
                            if (t < minMs) continue;
                            string n = v.GetItemName(h);
                            if (n == "EditorLoop") continue;
                            sb.AppendLine(new string(' ', nivel * 2) + t.ToString("0.0", ci) + "  " + n + "  (propio " + v.GetItemColumnDataAsDouble(h, HierarchyFrameDataView.columnSelfTime).ToString("0.0", ci) + ")");
                            lineas++;
                            Rec(h, nivel + 1);
                        }
                    }
                    Rec(v.GetRootItemID(), 0);
                }
            }
            return sb.ToString();
        }
    }
}
