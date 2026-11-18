using System.Collections;
using System.Text;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T17: lo que agrega la Ronda 13 al nivel NO agrega choque: ningun renderer Detalle_R13 ni R13_* tiene collider.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 986a ningun Detalle_R13 / R13_* con collider
        static IEnumerator Bug986a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            int total = 0, conCollider = 0; var lista = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r == null) continue;
                bool r13 = r.name.StartsWith("Detalle_R13") || r.name.StartsWith("R13_");
                if (!r13) for (var t = r.transform.parent; t != null && !r13; t = t.parent) r13 = t.name == "Detalle_R13" || t.name.StartsWith("R13_Solado") || t.name == "R13_Calles" || t.name == "R13_PatioSolado";
                if (!r13) continue;
                total++;
                var c = r.GetComponent<Collider>();
                if (c != null && c.enabled) { conCollider++; if (lista.Length < 300) lista.Append(r.name + " "); }
            }
            W8Ok(ref ok, total > 0, sb, $"{total} renderers Detalle_R13/R13_* en las escenas (si es 0 la Ola 2 no corrio)");
            W8Ok(ref ok, conCollider == 0, sb, $"{conCollider} de ellos con collider ({lista})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
