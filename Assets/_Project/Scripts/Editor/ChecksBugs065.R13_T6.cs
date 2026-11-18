using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.UI;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Ronda 13, T6: liberar milicianos con una cinematica de 2 s (reja que baja, camara corta) y el aviso DESPUES.
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 965a celdas: cinematica de 1,8-2,2 s, control quitado y devuelto, reja abierta, 2 liberados
        static IEnumerator Bug965a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            ModoDios.Poner(true);
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO sin director"); yield break; }
            d.SaltarAlSegundoPisoPorPrueba();
            if (CinematicaDeRapel.Activa) CinematicaDeRapel.Saltar();   // SaltarA(Resistir) arranca el rapel, que apaga el driver; las pruebas lo cortan
            // la cinematica de LLEGADA al 2.o piso (6,5 s) no es la de liberacion: se espera a que termine
            foreach (var x in Esperar(1.0f)) yield return x;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f && (d.CineDeMansionEnCurso || d.TerminalDeCeldas == null)) yield return null;
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, d.TerminalDeCeldas != null && !d.CineDeMansionEnCurso, sb, "la terminal existe y la cinematica de llegada ya termino");
            var yo = Poseido();
            { var dv = PlayerInputDriver.Activo; sb.Append($"[diag: drivers={Object.FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length} habilitados={System.Array.FindAll(Object.FindObjectsByType<PlayerInputDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None), q => q.enabled).Length} vivosJugador={System.Linq.Enumerable.Count(ActorRegistry.All, q => q != null && q.Team == TeamId.Player && q.Health != null && q.Health.IsAlive)} driver={(dv != null)} brain={(dv != null && dv.Brain != null)} current={(dv != null && dv.Brain != null && dv.Brain.Current != null)} cine={d.CineDeMansionEnCurso} etapa={d.EtapaMansion} t={Time.time:0.0}] "); }
            if (yo == null) foreach (var aa in ActorRegistry.All) if (aa != null && aa.Team == TeamId.Player && aa.Health != null && aa.Health.IsAlive && aa.Brain != null && aa.Brain.IsPossessedByPlayer) { yo = aa; break; }
            if (yo == null || d.TerminalDeCeldas == null) { var dv = PlayerInputDriver.Activo; Fin($"FALLO sin jugador/terminal [driver={(dv != null)} brain={(dv != null && dv.Brain != null)} current={(dv != null && dv.Brain != null && dv.Brain.Current != null)} vivos={ActorRegistry.All.Count} fase={d.Fase} etapa={d.EtapaMansion}] " + sb); yield break; }
            // sin guardia a la vista (si no, pelean): se apagan los enemigos de la mansion
            ((IList)typeof(OperacionDirector).GetField("pendientesMansion", NoPublico).GetValue(d)).Clear();
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.name.StartsWith("Mansion_Enemigo")) a.gameObject.SetActive(false);
            ActorRegistry.Invalidate();
            yo.transform.position = d.PosDeUsoDeLaTerminal + Vector3.up * 0.9f; Physics.SyncTransforms();
            foreach (var x in Esperar(0.6f)) yield return x;
            W8Ok(ref ok, !d.MilicianosLiberados && !d.RejaAbierta, sb, "antes de usar la terminal: celdas cerradas");
            var drv = PlayerInputDriver.Activo;
            AlertQueue.Historial.Clear();
            OperacionTerminal.PruebaToqueE = true;
            // mide la duracion de la cinematica cuadro a cuadro
            float inicio = -1f, fin = -1f; bool driverOffDurante = false, driverOnDespues = false, rejaA15 = false, alertaDurante = false;
            int barrotesActivosA15 = -1;
            float tEspera = Time.time;
            while (Time.time - tEspera < 8f)
            {
                yield return null;
                if (d.CineDeMansionEnCurso && inicio < 0f) inicio = Time.time;
                if (inicio >= 0f && d.CineDeMansionEnCurso)
                {
                    if (drv != null && !drv.enabled) driverOffDurante = true;
                    if (!rejaA15 && Time.time - inicio >= 1.5f) { rejaA15 = d.RejaAbierta; barrotesActivosA15 = BarrotesActivos(); }
                    if (AlertQueue.Historial.Exists(m => m.Contains("MILICIANOS LIBERADOS"))) alertaDurante = true;
                }
                if (inicio >= 0f && !d.CineDeMansionEnCurso) { fin = Time.time; break; }
            }
            float dur = fin >= 0f ? fin - inicio : -1f;
            W8Ok(ref ok, dur >= 1.8f && dur <= 2.2f, sb, $"la cinematica dura {dur:0.00} s (1,8 a 2,2)");
            W8Ok(ref ok, driverOffDurante, sb, "el control del jugador se quita durante la cinematica");
            foreach (var x in Esperar(0.3f)) yield return x;
            driverOnDespues = drv != null && drv.enabled;
            W8Ok(ref ok, driverOnDespues, sb, "el control se devuelve al terminar");
            W8Ok(ref ok, rejaA15 && barrotesActivosA15 <= 0, sb, $"a los 1,5 s la reja esta abierta ({rejaA15}) y quedan {barrotesActivosA15} barrotes activos");
            W8Ok(ref ok, d.MilicianosLiberados && MandoTactico.Milicianos.Count == MandoTactico.CantidadDeMilicianos, sb, $"{MandoTactico.Milicianos.Count} milicianos liberados (de {MandoTactico.CantidadDeMilicianos})");
            W8Ok(ref ok, !alertaDurante, sb, "el aviso no sale durante la cinematica (la cola se vacia al empezar)");
            string alerta = AlertQueue.Historial.Find(m => m.Contains("MILICIANOS LIBERADOS")) ?? "ninguno";
            W8Ok(ref ok, alerta.Contains("MILICIANOS LIBERADOS"), sb, $"despues aparece «{alerta.Replace('\n', ' ')}»");
            // siguen al lider: la distancia media baja entre los 2 s y los 8 s
            var mil = MandoTactico.Milicianos;
            float d0 = DistanciaMedia(mil, yo);
            yo.transform.position += yo.transform.forward * 4f; Physics.SyncTransforms();
            foreach (var x in Esperar(6f)) yield return x;
            float d1 = DistanciaMedia(mil, yo);
            W8Ok(ref ok, d1 < d0 + 4f && d1 < 12f, sb, $"los milicianos siguen al lider: distancia media {d0:0.0} -> {d1:0.0} m");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static float DistanciaMedia(System.Collections.Generic.IEnumerable<Soldier> l, Soldier yo)
        {
            float s = 0f; int n = 0;
            if (l != null) foreach (var m in l) if (m != null && m.Health != null && m.Health.IsAlive) { s += Vector3.Distance(m.transform.position, yo.transform.position); n++; }
            return n > 0 ? s / n : 999f;
        }

        static int BarrotesActivos()
        {
            var reja = GameObject.Find("P2_Reja"); if (reja == null) return -1;
            int n = 0; foreach (Transform c in reja.transform) if (c.gameObject.activeSelf) n++;
            return n;
        }
    }
}
