using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Prueba por CLI de la Operacion Cuartel: "arranca desde el objetivo X".
    //
    //   Desde la terminal (editor abierto, SC_Operacion en Play):
    //     unity cmd eval --project-path <proyecto> -- "SP.EditorTools.OperacionPrueba.Arrancar(4)"
    //   o el script Tools/operacion_desde_objetivo.sh <objetivo> [puesto]   (abre la escena, entra en Play, salta y deja la foto)
    //
    // Objetivos (los mismos numeros que el HUD): 1 Infiltrar · 2 Muralla: volar los puestos (puesto 1 Oeste | 2 Este) · 3 Centro de datos
    //   4 Huir en el tanque · 5 Resistir en la ciudad · 6 Subir al helicoptero.
    //
    // Arrancar() deja hecho todo lo anterior (enemigos del cuartel fuera, puestos abiertos, etc.) y teletransporta a la escuadra.
    // Recorrer() hace los seis saltos seguidos y en cada uno MARCA UN ESTADO (el mismo mecanismo de la tecla U / 8: json con posicion de
    // enemigos, aliados y jugador, vida, camara y mision + captura) y escribe un informe con lo que encontro.
    public static class OperacionPrueba
    {
        public static bool Corriendo { get; private set; }
        public static string Informe { get; private set; } = "";
        public static string Progreso { get; private set; } = "";
        static IEnumerator rutina;
        static int ultimoObjetivo;

        static readonly string[] Nombres = { "", "INFILTRAR EL CUARTEL", "PUESTOS DE CONTROL", "CENTRO DE DATOS", "HUIR EN EL TANQUE", "RESISTIR EN LA CIUDAD", "SUBIR AL HELICOPTERO" };

        public static string Arrancar(int objetivo, int puesto = 1, int subfase = 0)
        {
            if (!Application.isPlaying) return "ERROR: el editor no esta en Play (SC_Operacion).";
            var d = OperacionDirector.Instancia;
            if (d == null) return "ERROR: no hay OperacionDirector (abri SC_Operacion).";
            if (objetivo < 1 || objetivo > OperacionDirector.TotalObjetivos) return "ERROR: objetivo 1.." + OperacionDirector.TotalObjetivos;
            PartidaGuardada.ModoPrueba = true;   // P10: las pruebas por CLI nunca tocan la partida guardada real del jugador
            // WP9a (#097): la cinematica inicial bloquea todo el input; las pruebas la saltan siempre.
            CinematicaDeOperacion.Saltar();
            ultimoObjetivo = objetivo;
            var fase = OperacionDirector.Orden[objetivo - 1];
            d.SaltarA(fase, true, subfase);
            if (objetivo == 2) d.SaltarAPuesto(Mathf.Clamp(puesto, 1, OperacionDirector.TotalDePuestos) - 1);   // puesto 1 = Oeste, 2 = Este (con el Oeste ya volado)
            return $"Arrancado en objetivo {objetivo}: {Nombres[objetivo]}" + (objetivo == 2 ? $" (puesto {(puesto == 2 ? "Este" : "Oeste")})" : "") + (subfase > 0 ? $" (subfase {subfase})" : "") + " · " + Resumen();
        }

        static string Inv(FormattableString f) => f.ToString(CultureInfo.InvariantCulture);

        public static string Resumen()
        {
            var d = OperacionDirector.Instancia;
            if (d == null) return "sin director";
            var yo = SP.Player.PlayerInputDriver.Activo != null && SP.Player.PlayerInputDriver.Activo.Brain != null ? SP.Player.PlayerInputDriver.Activo.Brain.Current : null;
            var sb = new StringBuilder();
            sb.Append(Inv($"fase={d.Fase} volados={d.PuestoActual} reloj={d.Reloj:0.0}"));
            if (yo != null) sb.Append(Inv($" yo=({yo.transform.position.x:0.0},{yo.transform.position.y:0.0},{yo.transform.position.z:0.0}) vida={yo.Health.Current}"));
            int en = 0, al = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue;
                if (s.Team == SP.Combat.TeamId.Enemy) en++; else al++;
            }
            sb.Append($" enemigos={en} aliados={al}");
            return sb.ToString();
        }

        // Los seis saltos seguidos. Cada uno espera 'segundosPorObjetivo' (tiempo real) antes de marcar el estado.
        public static void Recorrer(float segundosPorObjetivo = 4f)
        {
            if (Corriendo || !Application.isPlaying) return;
            Corriendo = true; Informe = "";
            rutina = Ejecutar(segundosPorObjetivo);
            EditorApplication.update += Paso;
        }

        static void Paso()
        {
            bool sigue = false;
            try { sigue = rutina != null && rutina.MoveNext(); }
            catch (System.Exception e) { Informe += "\nERROR: " + e.Message; Debug.LogError(e); }
            if (sigue) return;
            EditorApplication.update -= Paso;
            Corriendo = false; rutina = null;
        }

        static IEnumerator Ejecutar(float seg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Objetivo | Resultado del salto | Bug/estado marcado | Estado a los " + seg.ToString("0") + " s |");
            sb.AppendLine("|---|---|---|---|");
            for (int o = 1; o <= OperacionDirector.TotalObjetivos; o++)
            {
                Progreso = "objetivo " + o;
                string r = Arrancar(o);
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < seg) yield return null;
                int bug = SesionLog.MarcarBug("prueba CLI objetivo " + o + " " + Nombres[o]);
                yield return null; yield return null; yield return null;
                sb.AppendLine($"| {o} · {Nombres[o]} | {r} | #{bug:000} | {Resumen()} |");
                Informe = sb.ToString();
                // Deja pasar el cartel del bug antes de saltar al siguiente.
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 1.2f) yield return null;
            }
            Progreso = "listo";
            Informe = sb.ToString();
        }
    }
}
