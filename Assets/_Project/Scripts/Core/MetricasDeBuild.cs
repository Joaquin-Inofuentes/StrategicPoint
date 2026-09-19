using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SP.Core
{
    // Modo de medicion para builds: `Juego.exe -spmetrics` carga SC_Gameplay desde el menu (o donde este), espera 6 s de
    // calentamiento, mide 20 s de partida y escribe el resultado en spmetrics.txt (carpeta de datos persistentes) y en el log.
    // Sirve para confirmar en un build real lo que en el Editor solo se puede estimar: costo por frame, basura del GC, memoria.
    public static class MetricasDeBuild
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            if (Application.isEditor) return;
            bool pedido = false;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-spmetrics") pedido = true;
                if (args[i] == "-spshot" && i + 1 < args.Length) { pedido = true; rutaCaptura = args[i + 1]; }
                if (args[i] == "-spsuper" && i + 1 < args.Length) int.TryParse(args[i + 1], out superCaptura);
            }
            if (!pedido) return;
            var go = new GameObject("MetricasDeBuild");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Corredor>().StartCoroutine(go.GetComponent<Corredor>().Correr());
        }

        static string rutaCaptura;
        static int superCaptura = 1;

        class Corredor : MonoBehaviour
        {
            public IEnumerator Correr()
            {
                var sb = new StringBuilder();
                var ci = CultureInfo.InvariantCulture;
                if (SceneManager.GetActiveScene().name != "SC_Gameplay")
                {
                    sb.AppendLine("escena_inicial=" + SceneManager.GetActiveScene().name + " -> cargando SC_Gameplay");
                    SceneManager.LoadScene("SC_Gameplay");
                    yield return null; yield return null;
                }
                yield return new WaitForSecondsRealtime(6f);
                if (!string.IsNullOrEmpty(rutaCaptura))
                {
                    // Modo captura (`-spshot ruta.png`): una imagen del HUD a la resolucion pedida, para revisar otras proporciones.
                    ScreenCapture.CaptureScreenshot(rutaCaptura, Mathf.Clamp(superCaptura, 1, 4));
                    yield return new WaitForSecondsRealtime(1.5f);
                    Application.Quit();
                    yield break;
                }
                // Sin tope de cuadros ni vsync: si no, el promedio solo dice "144 fps" y esconde el costo real por frame.
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                yield return new WaitForSecondsRealtime(1f);
                int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);

                using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
                {
                    var tiempos = new System.Collections.Generic.List<float>(4096);
                    long gcSuma = 0; long gcMax = 0; int n = 0;
                    float fin = Time.realtimeSinceStartup + 20f;
                    while (Time.realtimeSinceStartup < fin)
                    {
                        yield return null;
                        tiempos.Add(Time.unscaledDeltaTime * 1000f);
                        long b = gc.Valid ? gc.LastValue : 0;
                        gcSuma += b; if (b > gcMax) gcMax = b; n++;
                    }
                    tiempos.Sort();
                    float prom = 0f; foreach (var t in tiempos) prom += t; prom /= Mathf.Max(1, tiempos.Count);
                    float p95 = tiempos[Mathf.Clamp((int)(tiempos.Count * 0.95f), 0, tiempos.Count - 1)];
                    float p99 = tiempos[Mathf.Clamp((int)(tiempos.Count * 0.99f), 0, tiempos.Count - 1)];
                    sb.AppendLine($"frames={n} promedio_ms={prom.ToString("0.00", ci)} fps_prom={(1000f / prom).ToString("0", ci)} p95_ms={p95.ToString("0.00", ci)} p99_ms={p99.ToString("0.00", ci)} max_ms={tiempos[tiempos.Count - 1].ToString("0.00", ci)}");
                    sb.AppendLine($"gc_por_frame_prom_bytes={(gcSuma / Mathf.Max(1, n))} gc_por_frame_max_bytes={gcMax}");
                }
                sb.AppendLine($"recolecciones_GC_en_20s: gen0={GC.CollectionCount(0) - gc0} gen1={GC.CollectionCount(1) - gc1} gen2={GC.CollectionCount(2) - gc2}");
                sb.AppendLine($"heap_gestionado_MB={(GC.GetTotalMemory(false) / 1048576f).ToString("0.0", ci)}");
                sb.AppendLine($"memoria_total_asignada_MB={(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("0.0", ci)} mono_usado_MB={(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576f).ToString("0.0", ci)}");
                int canvases = 0, worldCanvases = 0, graficos = 0;
                foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) { canvases++; if (c.renderMode == RenderMode.WorldSpace) worldCanvases++; }
                graficos = UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None).Length;
                sb.AppendLine($"escena={SceneManager.GetActiveScene().name} canvases={canvases} (world={worldCanvases}) graficos_ui={graficos} audiosources={UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}");
                sb.AppendLine($"cajas_de_suministros={SP.Player.CajaDeSuministros.Todas.Count} reservas_activas={SP.Combat.WeaponHolder.ReservasActivas} hud_pulido={(UnityEngine.Object.FindAnyObjectByType<SP.UI.HudPulido>() != null)} soldados={ActorRegistry.All.Count}");
                sb.AppendLine($"gpu={SystemInfo.graphicsDeviceName} cpu={SystemInfo.processorType} ram_MB={SystemInfo.systemMemorySize} resolucion={Screen.width}x{Screen.height}");
                try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "spmetrics.txt"), sb.ToString()); } catch { }
                Debug.Log("SPMETRICS\n" + sb);
                Application.Quit();
            }
        }
    }
}
