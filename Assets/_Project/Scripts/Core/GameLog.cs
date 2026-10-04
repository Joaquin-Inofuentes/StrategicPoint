using System.IO;
using System.Text;
using UnityEngine;

namespace SP.Core
{
    // Log de flujo de juego (menú, pausa, victoria/derrota, ordenes de alto
    // nivel) separado a propósito de TestLog: TestLog es para el test de
    // integración fase por fase, este es la narración de "qué hizo el
    // jugador" pedida aparte. Mismo mecanismo (Debug.Log) pero con su
    // propio prefijo y su propio archivo en disco, para poder mirarlo sin
    // mezclarse con el otro.
    public static class GameLog
    {
        static readonly StringBuilder buffer = new StringBuilder();

        // Bug #052: cada linea hacia Debug.Log CON pila (en el editor, ~1 ms por linea: ExtractStackTrace) y reescribia el
        // archivo entero. Ahora sin pila y el archivo se escribe como mucho una vez por segundo.
        static float ultimaEscritura = -10f;
        static bool pendiente;

        public static void Line(string message)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[FLUJO] {0}", message);
            buffer.AppendLine(message);
            pendiente = true;
            if (Time.realtimeSinceStartup - ultimaEscritura >= 1f) TryFlush();
        }

        public static void Clear()
        {
            buffer.Clear();
            TryFlush();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void EngancharSalida() => Application.quitting += () => { if (pendiente) TryFlush(); };

        static string FilePath => Path.Combine(Application.dataPath, "..", "GameFlowLog.txt");

        static void TryFlush()
        {
            ultimaEscritura = Time.realtimeSinceStartup;
            pendiente = false;
            try { File.WriteAllText(FilePath, buffer.ToString()); }
            catch { /* solo un log de conveniencia, no debe romper nada si falla */ }
        }
    }
}
