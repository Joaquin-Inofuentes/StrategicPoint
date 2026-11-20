using System.Collections.Generic;
using System.IO;
using System.Text;
using SP.Core;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Sube al Sheet los reportes que quedaron solo en Registros/ (los marcados desde el Editor antes de 2026-11-20).
    // Uso: SubirBacklogBugs.Iniciar(desde, hasta) -> uno por vez (sin saturar Apps Script); el avance queda en Temp/backlog_bugs.txt.
    public static class SubirBacklogBugs
    {
        static readonly Queue<string> cola = new Queue<string>();
        static System.Threading.Tasks.Task actual; static string actualJson; static int ok, mal, orden;
        static string Log => Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp", "backlog_bugs.txt");

        public static string Iniciar(int desde, int hasta)
        {
            if (cola.Count > 0 || actual != null) return "ya hay una subida en curso";
            var files = new List<(int n, string f)>();
            foreach (var f in Directory.GetFiles(SesionLog.Carpeta, "bug_*.json"))
            {
                string nombre = Path.GetFileName(f);
                if (nombre.EndsWith("_estado.json")) continue;
                if (!int.TryParse(nombre.Substring(4, 3), out int n) || n < desde || n > hasta) continue;
                if (!File.Exists(Path.ChangeExtension(f, ".png"))) continue;
                files.Add((n, f));
            }
            files.Sort((a, b) => a.n.CompareTo(b.n));
            foreach (var x in files) cola.Enqueue(x.f);
            ok = mal = 0; orden = 0;
            File.WriteAllText(Log, $"inicio: {cola.Count} reportes ({desde}..{hasta})\n");
            EditorApplication.update -= Pump; EditorApplication.update += Pump;
            return $"{cola.Count} reportes en cola";
        }

        static void Pump()
        {
            if (actual != null) { if (!actual.IsCompleted) return; Anotar(); actual = null; }
            if (cola.Count == 0) { File.AppendAllText(Log, $"fin: ok={ok} mal={mal}\n"); EditorApplication.update -= Pump; return; }
            string f = cola.Dequeue(); actualJson = f;
            try
            {
                string texto = File.ReadAllText(f, Encoding.UTF8);
                var e = JsonUtility.FromJson<EstadoDeSesion>(texto);
                List<KeyValuePair<string, string>> extra = null;
                try { extra = ContextoDeBug.Columnas(e, ++orden); } catch { }
                string png = Path.ChangeExtension(f, ".png");
                var r = SubidaDeBug.Armar(e, e.nota, f, png, texto, extra);
                r.imagen = SubidaDeBug.ComprimirArchivo(png);
                if (r.imagen == null) { mal++; File.AppendAllText(Log, $"#{e.numero:000} sin imagen legible\n"); return; }
                actual = SubidaDeBug.SubirDeDisco(r);
            }
            catch (System.Exception ex) { mal++; File.AppendAllText(Log, Path.GetFileName(f) + " error: " + ex.Message + "\n"); }
        }

        static void Anotar()
        {
            bool bien = SubidaDeBug.UltimoResultado.StartsWith("OK");
            if (bien) ok++; else mal++;
            File.AppendAllText(Log, SubidaDeBug.UltimoResultado + "\n");
        }
    }
}
