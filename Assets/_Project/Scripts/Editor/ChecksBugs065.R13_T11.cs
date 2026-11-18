using System.Collections;
using System.Text;
using UnityEngine;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Ronda 13, T11: una vez limpio en ORIGEN, las redes de seguridad en juego no apagan nada.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 980a LimpiezaDeDuplicados y TitulosDeObjetosOcultos cuentan 0
        static IEnumerator Bug980a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(5f)) yield return x;
            W8Ok(ref ok, LimpiezaDeDuplicados.Apagados == 0, sb, $"LimpiezaDeDuplicados.Apagados = {LimpiezaDeDuplicados.Apagados} (pide 0)");
            W8Ok(ref ok, LimpiezaDeDuplicados.CollidersApagados == 0, sb, $"LimpiezaDeDuplicados.CollidersApagados = {LimpiezaDeDuplicados.CollidersApagados} (pide 0)");
            W8Ok(ref ok, TitulosDeObjetosOcultos.Ocultos == 0, sb, $"TitulosDeObjetosOcultos.Ocultos = {TitulosDeObjetosOcultos.Ocultos} (pide 0)");
            if (LimpiezaDeDuplicados.Detalle.Count > 0)
            {
                try { System.IO.File.WriteAllLines("Temp/r13_duplicados_en_juego.txt", LimpiezaDeDuplicados.Detalle); } catch { }
                sb.Append("[primeros: " + string.Join(" | ", LimpiezaDeDuplicados.Detalle.GetRange(0, System.Math.Min(5, LimpiezaDeDuplicados.Detalle.Count))) + "; lista en Temp/r13_duplicados_en_juego.txt] ");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
