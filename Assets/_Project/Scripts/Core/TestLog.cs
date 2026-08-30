using System.Diagnostics;
using UnityEngine;

namespace SP.Core
{
    // Logueo con timer para el test de integración. [t=0.00s] mensaje.
    public static class TestLog
    {
        static Stopwatch watch;

        public static void Begin() => watch = Stopwatch.StartNew();

        static float Elapsed => watch != null ? (float)watch.Elapsed.TotalSeconds : 0f;

        public static void Phase(string title) =>
            UnityEngine.Debug.Log($"[t={Elapsed:0.00}s] ===== {title} =====");

        public static void Step(string message) =>
            UnityEngine.Debug.Log($"[t={Elapsed:0.00}s] {message}");

        public static void Warn(string message) =>
            UnityEngine.Debug.LogWarning($"[t={Elapsed:0.00}s] {message}");
    }
}
