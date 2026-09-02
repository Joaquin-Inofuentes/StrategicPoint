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
    }
}
