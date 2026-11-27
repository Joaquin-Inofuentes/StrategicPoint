using System.Collections;
using System.Text;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // R19, pasada 2 (integracion sin atajos de los checks): recorre los 6 objetivos de la Operacion con OperacionPrueba.Recorrer y verifica que
    // todos los saltos arrancan, que no aparecen excepciones nuevas y que el director sigue vivo. Los errores de NavMeshViva ("does not allow read access") son previos.
    public static partial class ChecksBugs065
    {
        static IEnumerator Bug994w()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; int excepciones = 0;
            Application.LogCallback oyente = (msg, pila, tipo) =>
            {
                if (tipo == LogType.Exception || (tipo == LogType.Error && !msg.Contains("does not allow read access") && !msg.Contains("NavMeshViva"))) excepciones++;
            };
            Application.logMessageReceived += oyente;
            OperacionPrueba.Recorrer(8f);
            float t0 = Time.realtimeSinceStartup;
            while (OperacionPrueba.Corriendo && Time.realtimeSinceStartup - t0 < 240f) yield return null;
            Application.logMessageReceived -= oyente;
            W8Ok(ref ok, !OperacionPrueba.Corriendo, sb, $"el recorrido de los 6 objetivos termino ({Time.realtimeSinceStartup - t0:0} s)");
            W8Ok(ref ok, OperacionDirector.Instancia != null, sb, "el director sigue vivo");
            W8Ok(ref ok, excepciones == 0, sb, $"{excepciones} excepciones o errores nuevos durante el recorrido");
            int filas = 0; foreach (var l in OperacionPrueba.Informe.Split('\n')) if (l.StartsWith("| ") && l.Contains("#")) filas++;
            W8Ok(ref ok, filas >= OperacionDirector.TotalObjetivos, sb, $"{filas} objetivos marcados de {OperacionDirector.TotalObjetivos}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
