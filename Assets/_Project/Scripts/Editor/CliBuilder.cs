using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Build de Windows x64 invocable por linea de comandos
    // (-batchmode -executeMethod SP.EditorTools.CliBuilder.BuildWindows64),
    // para probar el juego real como .exe sin pasar por Play mode del
    // Editor. Usa las escenas ya configuradas en Build Settings, en el
    // mismo orden.
    public static class CliBuilder
    {
        public static void BuildWindows64()
        {
            string outputDir = "Builds/Windows64";
            System.IO.Directory.CreateDirectory(outputDir);

            var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputDir + "/StrategicPoint.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"[CliBuilder] Resultado: {summary.result} | Errores: {summary.totalErrors} | Warnings: {summary.totalWarnings} | Tamano: {summary.totalSize} bytes | Salida: {summary.outputPath}");

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // Build WebGL ajustada para fluidez y tamano (invocable por eval o -executeMethod).
        public static void BuildWebGL()
        {
            const string outputDir = "Builds/WebGL";
            System.IO.Directory.CreateDirectory(outputDir);

            var t = UnityEditor.Build.NamedBuildTarget.WebGL;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;      // hosting sin cabeceras Content-Encoding (GitHub Pages, itch.io)
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(t, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            EditorUserBuildSettings.development = false;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[CliBuilder] WebGL Resultado: {summary.result} | Errores: {summary.totalErrors} | Tamano: {summary.totalSize} bytes | Salida: {summary.outputPath}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
