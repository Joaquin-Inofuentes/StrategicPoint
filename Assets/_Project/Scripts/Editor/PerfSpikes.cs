using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Cazador de picos ("hipo cada X segundos al caminar"). En Play, cada frame de juego mide el tiempo real y, si pasa del umbral (3x la mediana
    // reciente y al menos 35 ms), anota que marcadores del profiler (los "Clase.Update()", "LateUpdate()", FindObjects, GC) fueron los mas caros de ESE
    // frame. Al final escribe Temp/perf_spikes.txt con la lista de picos, su cadencia y un ranking de culpables.
    //   return SP.EditorTools.PerfSpikes.Iniciar();     // en Play
    //   return SP.EditorTools.PerfSpikes.Detener();     // escribe el informe y devuelve el resumen
    public static class PerfSpikes
    {
        sealed class Marcador { public string nombre; public ProfilerRecorder rec; public double acumulado; public int veces; }
        static readonly List<Marcador> marcadores = new List<Marcador>();
        static ProfilerRecorder gcAlloc, gcAllocBytes;
        static readonly List<float> ventana = new List<float>();
        static readonly StringBuilder picos = new StringBuilder();
        static readonly Dictionary<string, double> culpables = new Dictionary<string, double>();
        static readonly Dictionary<string, int> culpablesVeces = new Dictionary<string, int>();
        static readonly List<float> instantes = new List<float>();
        static float t0, ultimo; static int frameUlt = -1, frames, nPicos, gcUlt;
        static double peorMs;
        public static bool Corriendo { get; private set; }

        public static string Iniciar()
        {
            if (!Application.isPlaying) return "ERROR: no esta en Play";
            Detener();
            marcadores.Clear(); picos.Clear(); culpables.Clear(); culpablesVeces.Clear(); instantes.Clear(); ventana.Clear();
            frames = 0; nPicos = 0; peorMs = 0; frameUlt = -1;
            var lista = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(lista);
            foreach (var h in lista)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                string n = d.Name;
                bool util = n.EndsWith(".Update()") || n.EndsWith(".LateUpdate()") || n.EndsWith(".FixedUpdate()") || n.StartsWith("FindObjects") || n.Contains("FindObjectsOfType")
                    || n == "GC.Collect" || n.StartsWith("Physics.Processing") || n.StartsWith("Physics.Simulate") || n == "NavMesh.Update" || n.StartsWith("Animators.Update") || n.StartsWith("Camera.Render");
                if (!util) continue;
                if (marcadores.Count > 900) break;
                try { marcadores.Add(new Marcador { nombre = n, rec = ProfilerRecorder.StartNew(d.Category, n, 1) }); } catch { }
            }
            // Los "Clase.Update()" no figuran en GetAvailable: se arman por nombre a partir de los MonoBehaviour vivos en la escena.
            var tipos = new HashSet<System.Type>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)) if (mb != null) tipos.Add(mb.GetType());
            foreach (var t in tipos)
                foreach (var metodo in new[] { "Update", "LateUpdate", "FixedUpdate" })
                {
                    if (t.GetMethod(metodo, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) == null) continue;
                    string nombre = t.Name + "." + metodo + "()";
                    try { marcadores.Add(new Marcador { nombre = nombre, rec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, nombre, 1) }); } catch { }
                }
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count", 1);
            gcAllocBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            t0 = Time.realtimeSinceStartup; ultimo = t0; gcUlt = System.GC.CollectionCount(0);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Corriendo = true;
            return "PerfSpikes: " + marcadores.Count + " marcadores vigilados";
        }

        static void Tick()
        {
            if (!Application.isPlaying) return;
            if (Time.frameCount == frameUlt) return;
            frameUlt = Time.frameCount;
            float ahora = Time.realtimeSinceStartup, dt = ahora - ultimo; ultimo = ahora;
            if (frames++ < 5) return;
            ventana.Add(dt); if (ventana.Count > 90) ventana.RemoveAt(0);
            var orden = new List<float>(ventana); orden.Sort(); float mediana = orden[orden.Count / 2];
            double ms = dt * 1000.0; if (ms > peorMs) peorMs = ms;
            int gc = System.GC.CollectionCount(0);
            if (ms >= 35.0 && dt > mediana * 3f && ventana.Count > 20)
            {
                nPicos++; instantes.Add(ahora - t0);
                var top = new List<KeyValuePair<string, double>>();
                foreach (var m in marcadores) { if (!m.rec.Valid) continue; double v = m.rec.LastValue / 1e6; if (v >= 0.4) top.Add(new KeyValuePair<string, double>(m.nombre, v)); }
                top.Sort((a, b) => b.Value.CompareTo(a.Value));
                picos.Append("t=").Append((ahora - t0).ToString("F1")).Append("s frame=").Append(Time.frameCount).Append(' ').Append(ms.ToString("F0")).Append("ms (mediana ").Append((mediana * 1000f).ToString("F0")).Append(") gcGen0+").Append(gc - gcUlt)
                    .Append(" alloc=").Append((gcAllocBytes.Valid ? gcAllocBytes.LastValue / 1024 : 0)).Append("KB :: ");
                for (int i = 0; i < top.Count && i < 6; i++)
                {
                    picos.Append(top[i].Key).Append('=').Append(top[i].Value.ToString("F1")).Append("ms ");
                    culpables.TryGetValue(top[i].Key, out var a); culpables[top[i].Key] = a + top[i].Value;
                    culpablesVeces.TryGetValue(top[i].Key, out var c); culpablesVeces[top[i].Key] = c + 1;
                }
                picos.AppendLine();
            }
            gcUlt = gc;
        }

        public static string Detener()
        {
            EditorApplication.update -= Tick;
            string resumen = "";
            if (Corriendo)
            {
                float dur = Time.realtimeSinceStartup - t0;
                var sb = new StringBuilder();
                sb.AppendLine($"duracion={dur:F0}s frames={frames} fps~{frames / Mathf.Max(1f, dur):F0} picos={nPicos} peor={peorMs:F0}ms");
                if (instantes.Count > 2)
                {
                    var gaps = new List<float>(); for (int i = 1; i < instantes.Count; i++) gaps.Add(instantes[i] - instantes[i - 1]);
                    gaps.Sort(); sb.AppendLine($"separacion entre picos: mediana={gaps[gaps.Count / 2]:F1}s min={gaps[0]:F1}s max={gaps[gaps.Count - 1]:F1}s");
                }
                var rank = new List<KeyValuePair<string, double>>(culpables);
                rank.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.AppendLine("--- culpables (ms acumulados en frames pico / veces) ---");
                for (int i = 0; i < rank.Count && i < 15; i++) sb.AppendLine($"{rank[i].Key}  {rank[i].Value:F0}ms  x{culpablesVeces[rank[i].Key]}");
                sb.AppendLine("--- picos ---"); sb.Append(picos);
                System.IO.File.WriteAllText("Temp/perf_spikes.txt", sb.ToString());
                resumen = sb.ToString().Split('\n')[0] + " | top: " + (rank.Count > 0 ? rank[0].Key : "-");
            }
            foreach (var m in marcadores) if (m.rec.Valid) m.rec.Dispose();
            marcadores.Clear();
            if (gcAlloc.Valid) gcAlloc.Dispose(); if (gcAllocBytes.Valid) gcAllocBytes.Dispose();
            Corriendo = false;
            return resumen;
        }
    }
}
