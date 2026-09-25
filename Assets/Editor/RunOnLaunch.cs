using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class RunOnLaunch
{
    static RunOnLaunch()
    {
        // Solo correr si estamos en batchmode y pasamos un argumento especial
        if (System.Environment.CommandLine.Contains("-runTutorialTest"))
        {
            EditorApplication.update += StartTest;
        }
    }

    static void StartTest()
    {
        EditorApplication.update -= StartTest;
        Application.logMessageReceived += Log;
        EditorApplication.isPlaying = true;
        EditorApplication.delayCall += () => {
            SP.EditorTools.TutorialAutoPlayerMenu.ReproducirTutorialAutomatico();
        };
    }

    static void Log(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            File.AppendAllText("dump_exception.txt", condition + "\n" + stackTrace + "\n");
        }
    }
}
