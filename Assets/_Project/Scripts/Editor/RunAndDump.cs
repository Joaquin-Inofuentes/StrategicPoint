using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class RunAndDump
{
    static RunAndDump()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void Run()
    {
        Application.logMessageReceived += Log;
        EditorApplication.isPlaying = true;
    }

    static void Log(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            File.AppendAllText("dump_exception.txt", condition + "\n" + stackTrace + "\n");
            EditorApplication.Exit(1);
        }
    }
    
    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SP.EditorTools.TutorialAutoPlayerMenu.ReproducirTutorialAutomatico();
        }
    }
}
