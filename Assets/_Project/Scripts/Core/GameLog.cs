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

        public static void Line(string message)
        {
            Debug.Log($"[FLUJO] {message}");
            buffer.AppendLine(message);
            TryFlush();
        }

        public static void Clear()
        {
            buffer.Clear();
            TryFlush();
        }

        static string FilePath => Path.Combine(Application.dataPath, "..", "GameFlowLog.txt");

        static void TryFlush()
        {
            try { File.WriteAllText(FilePath, buffer.ToString()); }
            catch { /* solo un log de conveniencia, no debe romper nada si falla */ }
        }
    }
}
