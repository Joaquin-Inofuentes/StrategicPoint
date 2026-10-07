using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // WP11: medidor de rendimiento por fase que separa lo que cuesta el JUEGO de lo que cuesta el editor. Se usa por CLI en Play:
    //   Iniciar("Resistir") ... esperar ... Cerrar()  ->  una linea con fps, p95, PlayerLoop (juego), EditorLoop (editor), hilo principal y GPU
    //   segun FrameTimingManager, ms del WorldSimulationDriver, basura por frame y materiales.
    // No usa el Profiler (no engorda la ventana): solo ProfilerRecorder (contadores) y FrameTimingManager.
    public static class MedidorDeFases
    {
        static bool activo;
        static string etiqueta = "";
        static int ultimoFrame = -1, n;
        static int gpuInvalidos;
        static double sumaReb, sumaAi, sumaVeh;
        static double sumaDt, sumaPlayer, sumaEditor, sumaMain, sumaGpu, sumaSim, maxSim, sumaGc, sumaMainFT, sumaRender;
        static readonly List<float> dts = new List<float>();
        static ProfilerRecorder recPlayer, recEditor, recSim, recGc, recRender;
        static readonly FrameTiming[] ft = new FrameTiming[1];
        static string materialesAlIniciar = "";

        public static string Materiales()
        {
            var todos = Resources.FindObjectsOfTypeAll<Material>(); int inst = 0, enEscena = 0;
            foreach (var m in todos) { if (m == null) continue; if (m.name.EndsWith(" (Instance)", StringComparison.Ordinal)) inst++; if (m.hideFlags == HideFlags.None) enEscena++; }
            return todos.Length + " (instancias " + inst + ", hideFlags=None " + enEscena + ")";
        }

        static string F(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

        static ProfilerRecorder Crear(ProfilerCategory cat, string nombre)
        {
            try { return ProfilerRecorder.StartNew(cat, nombre, 1); } catch { return default; }
        }

        // Diagnostico de fugas de materiales: Origenes(true) antes de entrar en Play (el recuento se reinicia con el domain reload, por eso se
        // guarda en SessionState y se reengancha al cargar). InformeOrigenes() lista quien pidio mas materiales a las fabricas.
        [InitializeOnLoadMethod]
        static void ReengancharOrigenes()
        {
            if (SessionState.GetBool("SPMed.Origenes", false)) SP.Presentation.SafeMaterial.Origenes = new Dictionary<string, int>();
        }

        public static string Origenes(bool encender)
        {
            SessionState.SetBool("SPMed.Origenes", encender);
            SP.Presentation.SafeMaterial.Origenes = encender ? new Dictionary<string, int>() : null;
            return encender ? "recuento de origenes encendido" : "apagado";
        }

        public static string InformeOrigenes(int top = 25)
        {
            var d = SP.Presentation.SafeMaterial.Origenes;
            if (d == null) return "apagado";
            var l = new List<KeyValuePair<string, int>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder(); int total = 0; foreach (var kv in l) total += kv.Value;
            sb.AppendLine("materiales creados por las fabricas: " + total);
            for (int i = 0; i < l.Count && i < top; i++) sb.AppendLine("  " + l[i].Value + "  " + l[i].Key);
            return sb.ToString();
        }

        public static string Iniciar(string nombre)
        {
            if (activo) Soltar();
            etiqueta = nombre; n = 0; ultimoFrame = -1; dts.Clear();
            gpuInvalidos = 0; sumaReb = sumaAi = sumaVeh = 0; sumaDt = sumaPlayer = sumaEditor = sumaMain = sumaGpu = sumaSim = maxSim = sumaGc = sumaMainFT = sumaRender = 0;
            recPlayer = Crear(ProfilerCategory.Internal, "PlayerLoop");
            recEditor = Crear(ProfilerCategory.Internal, "EditorLoop");
            recSim = Crear(ProfilerCategory.Scripts, "Assembly-CSharp.dll!SP.Ai::WorldSimulationDriver.Update() [Invoke]");
            recGc = Crear(ProfilerCategory.Memory, "GC Allocated In Frame");
            recRender = Crear(ProfilerCategory.Render, "CPU Render Thread Frame Time");
            materialesAlIniciar = Materiales();
            activo = true;
            EditorApplication.update += Muestrear;
            return "midiendo " + nombre;
        }

        static void Soltar()
        {
            EditorApplication.update -= Muestrear;
            if (recPlayer.Valid) recPlayer.Dispose();
            if (recEditor.Valid) recEditor.Dispose();
            if (recSim.Valid) recSim.Dispose();
            if (recGc.Valid) recGc.Dispose();
            if (recRender.Valid) recRender.Dispose();
            activo = false;
        }

        static void Muestrear()
        {
            if (!Application.isPlaying) return;
            int f = Time.frameCount;
            if (f == ultimoFrame) return;
            bool primero = ultimoFrame < 0;
            ultimoFrame = f;
            if (primero) return;
            n++;
            float dt = Time.unscaledDeltaTime; dts.Add(dt); sumaDt += dt;
            if (recPlayer.Valid) sumaPlayer += recPlayer.LastValue / 1e6;
            if (recEditor.Valid) sumaEditor += recEditor.LastValue / 1e6;
            { double r = SP.Ai.WorldSimulationDriver.LastRebuildMs, a = SP.Ai.WorldSimulationDriver.LastAiWeaponMs, v = SP.Ai.WorldSimulationDriver.LastVehicleMs; sumaReb += r; sumaAi += a; sumaVeh += v; double s = r + a + v; sumaSim += s; if (s > maxSim) maxSim = s; }
            if (recGc.Valid) sumaGc += recGc.LastValue;
            if (recRender.Valid) sumaRender += recRender.LastValue / 1e6;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, ft) > 0) { sumaMainFT += ft[0].cpuMainThreadFrameTime; if (ft[0].gpuFrameTime < 1000.0) sumaGpu += ft[0].gpuFrameTime; else gpuInvalidos++; }
        }

        public static string Cerrar()
        {
            if (!activo) return "no se estaba midiendo";
            Soltar();
            if (n < 5) return etiqueta + ": muy pocos frames (" + n + ")";
            var ci = CultureInfo.InvariantCulture;
            dts.Sort();
            float p95 = dts[Mathf.Min(dts.Count - 1, Mathf.CeilToInt(dts.Count * 0.95f) - 1)] * 1000f;
            float peor = dts[dts.Count - 1] * 1000f;
            string materiales = Materiales();
            var sb = new StringBuilder();
            sb.Append(etiqueta).Append(": ")
              .Append(F($"frames={n} fps={n / sumaDt:0.0} frame={sumaDt / n * 1000:0.0}ms p95={p95:0.0}ms peor={peor:0}ms | "))
              .Append(F($"JUEGO(PlayerLoop)={sumaPlayer / n:0.0}ms EDITOR(frame-PlayerLoop)={sumaDt / n * 1000 - sumaPlayer / n:0.0}ms hiloPrincipalFT={sumaMainFT / n:0.0}ms GPU={sumaGpu / Math.Max(1, n - gpuInvalidos):0.0}ms renderThread={sumaRender / n:0.0}ms | "))
              .Append(F($"WorldSim={sumaSim / n:0.00}ms (grilla {sumaReb / n:0.00} + IA/armas {sumaAi / n:0.00} + vehiculos/torretas {sumaVeh / n:0.00}; max {maxSim:0.0}) GC={sumaGc / n / 1024.0:0.0}KB/frame | materiales {materialesAlIniciar}->{materiales}"));
            return sb.ToString();
        }

        // Estado de la escena que importa para el costo (no mide nada: solo cuenta).
        public static string Entorno()
        {
            int activos = 0, fragmentos = 0, renderers = 0, luces = 0, luzActiva = 0, soldados = 0, enemigos = 0, particulas = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r != null && r.enabled && r.gameObject.activeInHierarchy) renderers++;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) { luces++; if (l.enabled && l.gameObject.activeInHierarchy) luzActiva++; }
            foreach (var s in SP.Core.ActorRegistry.All) if (s != null && s.isActiveAndEnabled && s.Health != null && s.Health.IsAlive) { soldados++; if (s.Team == SP.Combat.TeamId.Enemy) enemigos++; }
            foreach (var p in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (p != null && p.isPlaying) particulas++;
            foreach (var fr in UnityEngine.Object.FindObjectsByType<SP.Presentation.Fragmento>(FindObjectsSortMode.None)) if (fr != null && fr.gameObject.activeInHierarchy) fragmentos++;
            activos = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            return $"transforms={activos} renderers={renderers} luces={luzActiva}/{luces} soldadosVivos={soldados} (enemigos {enemigos}) particulasActivas={particulas} fragmentosActivos={fragmentos} gameView={Screen.width}x{Screen.height}";
        }
    }
}
