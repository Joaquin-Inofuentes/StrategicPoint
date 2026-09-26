using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Linq;

public static class BuildAndPack
{
    [MenuItem("Tools/Build and Pack INOFUENTES")]
    public static void Run()
    {
        string buildDir = "C:/Users/PC_JOACO/Downloads/INOFUENTES/TP1_INOFUENTES";
        if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);

        foreach (string file in Directory.GetFiles(buildDir)) {
            if (!file.EndsWith(".rar")) File.Delete(file);
        }
        foreach (string dir in Directory.GetDirectories(buildDir)) {
            Directory.Delete(dir, true);
        }

        var buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        buildPlayerOptions.locationPathName = Path.Combine(buildDir, "StrategicPoint.exe");
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        UnityEngine.Debug.Log("Building to " + buildDir);
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log("Build succeeded! Compressing to rar...");
            string rarPath = "C:/Program Files/WinRAR/Rar.exe"; 
            if (File.Exists(rarPath))
            {
                string archive = Path.Combine(buildDir, "TP1_INOFUENTES.rar");
                if (File.Exists(archive)) File.Delete(archive);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = rarPath;
                psi.Arguments = "a -ep1 -r "" + archive + "" "" + buildDir + "\*"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                
                var process = Process.Start(psi);
                process.WaitForExit();
                UnityEngine.Debug.Log("Rar compression complete!");
            }
            else
            {
                UnityEngine.Debug.LogError("WinRAR not found at " + rarPath + ". Please compress manually.");
            }
        }
        else
        {
            UnityEngine.Debug.LogError("Build failed!");
        }
    }
}
