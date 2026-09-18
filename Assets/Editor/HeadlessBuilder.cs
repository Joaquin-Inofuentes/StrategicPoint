using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.Linq;
using UnityEngine;

public class HeadlessBuilder
{
    private const string BuildPathPrefKey = "MIP.HeadlessBuilder.BuildPath";
    private const string DefaultBuildPath = "Builds/HeadlessWebGL";

    // Resuelve el path de salida: variable de entorno (para CI) > EditorPrefs
    // (configurable por herramienta) > default relativo al proyecto. Antes era
    // un path absoluto fijo a la máquina del autor original, que rompía en
    // cualquier otra máquina o runner.
    public static string ResolveBuildPath()
    {
        string envPath = Environment.GetEnvironmentVariable("MIP_BUILD_OUTPUT");
        if (!string.IsNullOrEmpty(envPath)) return envPath;
        return EditorPrefs.GetString(BuildPathPrefKey, DefaultBuildPath);
    }

    public static void SetBuildPath(string path) => EditorPrefs.SetString(BuildPathPrefKey, path);

    /// <summary>
    /// Punto de entrada de batchmode: construye y CIERRA el Editor con el codigo de salida que
    /// espera CI. No llamar con un Editor abierto -- se lleva puesta la sesion del que este
    /// trabajando; para eso esta BuildWebGLDesdeEditor().
    /// </summary>
    public static void BuildWebGL()
    {
        BuildResult resultado = EjecutarBuild();
        if (resultado == BuildResult.Succeeded)
        {
            Debug.Log("[HeadlessBuilder] WebGL Build Succeeded!");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("[HeadlessBuilder] WebGL Build Failed!");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// El mismo build, pero sin cerrar el Editor: sirve para construir desde una sesion abierta
    /// (o desde el CLI contra ella) y seguir trabajando. Devuelve el resultado como texto.
    /// </summary>
    [MenuItem("MIP/Build WebGL (sin cerrar el Editor)")]
    public static string BuildWebGLDesdeEditor()
    {
        BuildResult resultado = EjecutarBuild();
        string msg = $"[HeadlessBuilder] Build WebGL: {resultado} -> {ResolveBuildPath()}";
        Debug.Log(msg);
        return msg;
    }

    private static BuildResult EjecutarBuild()
    {
        string buildPath = ResolveBuildPath();
        Debug.Log("[HeadlessBuilder] Build output: " + buildPath);

        // Programmatically populate scenes if EditorBuildSettings is empty
        var buildScenes = EditorBuildSettings.scenes;
        if (buildScenes == null || buildScenes.Length == 0)
        {
            Debug.Log("[HeadlessBuilder] EditorBuildSettings.scenes is empty. Setting default scene list...");
            var defaultScenes = new[]
            {
                new EditorBuildSettingsScene("Assets/_Project/Scenes/MIP Version Final.unity", true)
            };
            EditorBuildSettings.scenes = defaultScenes;
            buildScenes = defaultScenes;
        }

        string[] scenes = buildScenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
            
        if (scenes.Length == 0)
        {
            Debug.LogError("[HeadlessBuilder] No enabled scenes found to build!");
            return BuildResult.Failed;
        }
            
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.None; // release: sin profiler ni simbolos de debug

        // Perfil por variable de entorno: MIP_BUILD_PROFILE = release (default) | diag
        //   release -> sin MIP_DIAG ni MIP_LOGS. Es lo que va a produccion.
        //   diag    -> con los dos, para reproducir un bug en un telefono real.
        string perfil = System.Environment.GetEnvironmentVariable("MIP_BUILD_PROFILE") ?? "release";
        buildPlayerOptions.extraScriptingDefines = perfil == "diag"
            ? new[] { "MIP_DIAG", "MIP_LOGS" }
            : new string[0];
        Debug.Log("[HeadlessBuilder] Perfil: " + perfil);

        // extraScriptingDefines SUMA a los simbolos del Player Settings, no los reemplaza.
        // Como para trabajar con tests conviene tener MIP_DIAG/MIP_LOGS puestos en el Editor,
        // sin esta limpieza un Editor configurado para debug contaminaria el build de release
        // (los tests entrarian al build y los logs volverian). Se restauran en el finally para
        // no cambiarle la configuracion al que este trabajando en el Editor.
        var targetNombrado = UnityEditor.Build.NamedBuildTarget.WebGL;
        string defsOriginales = PlayerSettings.GetScriptingDefineSymbols(targetNombrado);
        if (perfil != "diag")
        {
            var limpios = defsOriginales.Split(';')
                .Where(d => d != "MIP_DIAG" && d != "MIP_LOGS")
                .ToArray();
            PlayerSettings.SetScriptingDefineSymbols(targetNombrado, string.Join(";", limpios));
        }

        // [T4.1 / 2026-09-03] Movil primero: ASTC lo decodifica el GPU de cualquier Android
        // o iOS moderno. El default era DXTC, que los GPU Mali y Adreno NO exponen en WebGL:
        // Unity los descomprime en CPU al cargar y sube RGBA sin comprimir, o sea que la
        // compresion no ahorra ni memoria ni tiempo de subida, solo tamano de descarga.
        // PERF-ARRANQUE ya midio que el build es 86 % texturas.
        // En escritorio ASTC se descomprime por software: mas lento ahi, mas rapido en el
        // telefono, que es el target. Si esto midiera peor en un telefono real, se revierte
        // este commit entero -- por eso va solo.
        UnityEditor.EditorUserBuildSettings.webGLBuildSubtarget = UnityEditor.WebGLTextureSubtarget.ASTC;
        buildPlayerOptions.subtarget = (int)UnityEditor.WebGLTextureSubtarget.ASTC;
        Debug.Log("[HeadlessBuilder] Compresion de texturas: ASTC");

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        }
        finally
        {
            PlayerSettings.SetScriptingDefineSymbols(targetNombrado, defsOriginales);
        }
        return report.summary.result;
    }
}
