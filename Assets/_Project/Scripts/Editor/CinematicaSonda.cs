using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Sonda de la cinematica inicial (bug #136, "esta solapado"): con el juego en Play, mira cada frame si la camara de la cinematica queda DENTRO de un
    // colisionador (esfera de 0,45 m) y anota en que segundo. Uso: eval "SP.EditorTools.CinematicaSonda.Iniciar()" ... eval "SP.EditorTools.CinematicaSonda.Informe".
    public static class CinematicaSonda
    {
        public static string Informe { get; private set; } = "";
        static readonly List<string> golpes = new List<string>();
        static bool activa; static float ultimoT = -1f; static int muestras;

        public static string Iniciar()
        {
            golpes.Clear(); muestras = 0; ultimoT = -1f; activa = true; Informe = "midiendo...";
            EditorApplication.update -= Paso; EditorApplication.update += Paso;
            return "sonda activa";
        }

        static void Paso()
        {
            if (!activa) { EditorApplication.update -= Paso; return; }
            var c = CinematicaDeOperacion.Instancia;
            if (!Application.isPlaying || c == null || !CinematicaDeOperacion.Activa)
            {
                if (muestras > 20) Terminar();
                return;
            }
            muestras++;
            var p = c.PosicionDeCamara;
            var cols = Physics.OverlapSphere(p, 0.45f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                if (col == null) continue;
                var dentro = col.bounds.Contains(p);
                if (!dentro) continue;
                golpes.Add(string.Format(CultureInfo.InvariantCulture, "t={0:0.0}s pos=({1:0},{2:0},{3:0}) dentro de {4}", c.Tiempo, p.x, p.y, p.z, Ruta(col.transform)));
                break;
            }
        }

        static string Ruta(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        static void Terminar()
        {
            activa = false;
            var sb = new StringBuilder($"{muestras} muestras, {golpes.Count} con la camara dentro de un colisionador\n");
            string ultimo = "";
            foreach (var g in golpes) { string k = g.Substring(g.IndexOf("dentro")); if (k == ultimo) continue; ultimo = k; sb.AppendLine(g); }
            Informe = sb.ToString();
        }
    }
}
