using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SP.Core;

namespace SP.Tutorial
{
    // Log claro del tutorial: una linea por INICIO de paso, por SUB-PASO
    // cumplido, por PISTA mostrada y por PASO completado. Sale por tres
    // lados: la consola (prefijo [TUTORIAL]), el log de flujo del juego
    // (GameLog) y un archivo de texto legible (Logs/Tutorial_Log.txt en la
    // carpeta del proyecto) para poder revisar la corrida completa.
    public static class TutorialLog
    {
        static readonly List<string> lineas = new List<string>();
        public static IReadOnlyList<string> Lineas => lineas;
        static float t0;

        static string RutaArchivo => Path.Combine(Application.dataPath, "..", "Logs", "Tutorial_Log.txt");

        public static void Reiniciar()
        {
            lineas.Clear();
            t0 = Time.realtimeSinceStartup;
            Guardar();
        }

        static string Tiempo() => (Time.realtimeSinceStartup - t0).ToString("000.0") + "s";

        public static void Escribir(string prefijo, string mensaje)
        {
            string linea = $"[TUTORIAL] {Tiempo()} {prefijo} {mensaje}";
            lineas.Add(linea);
            Debug.Log(linea);
            GameLog.Line(linea);
            Guardar();
        }

        public static void Paso(int indice, int total, string titulo, string mensaje)
            => Escribir($"[PASO {indice + 1:00}/{total:00} {titulo}]", mensaje);

        public static void Sub(int indice, int total, string titulo, int sub, int subTotal, string texto)
            => Escribir($"[PASO {indice + 1:00}/{total:00} {titulo}]", $"   sub-paso {sub}/{subTotal} CUMPLIDO: {texto}");

        static void Guardar()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo));
                File.WriteAllText(RutaArchivo, string.Join("\n", lineas), Encoding.UTF8);
            }
            catch { /* el log de archivo es solo una comodidad */ }
        }
    }
}
