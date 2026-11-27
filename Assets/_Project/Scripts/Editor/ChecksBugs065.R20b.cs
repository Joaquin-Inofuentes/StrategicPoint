using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Cola de pendientes #2 (2026-11-27): "que mi ultimo guardado guarde tambien el progreso del nivel, no solo aparecer en esa zona (mori en el
    // blindado y al cargar mi ultimo punto estaba la computadora sin explotar pero la pude traspasar igual)".
    //   Bug996a  Huir tras EXPLOTAR la computadora: guardar en el acercamiento, continuar; pasar a la pelea con el BLINDADO, guardar, continuar.
    //            En las dos cargas: terminal sin dibujo ni collider, salida norte caida y sin collider, subfase y toma del jefe como estaban.
    //   Bug996b  lo NO explotado es solido: guardar en el centro de datos con el hackeo a medias, continuar: la terminal se ve y bloquea el paso
    //            (collider encendido, no trigger, choca con la capa del jugador, un rayo la toca) y el muro de la salida norte sigue en pie y solido.
    //   Bug996c  compatibilidad: una partida v1 (sin las claves nuevas) de la huida se sigue cargando y deduce el progreso por la fase.
    //   Bug996d  ciclo guardar -> continuar en cada checkpoint de autoguardado (con su motivo real): el resumen del progreso queda igual.
    // Todos en ModoPrueba (partida_SC_Operacion_prueba.json en Registros/Partidas). Correr:
    //   return SP.EditorTools.ChecksBugs065.CorrerLista("Bug996a,Bug996b,Bug996c,Bug996d", true);
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- utilidades
        static IEnumerable R20bGuardarYContinuar(string motivo, StringBuilder sb, Action<bool> resultado)
        {
            PartidaGuardada.ModoPrueba = true;
            if (!PartidaGuardada.Guardar(motivo)) { sb.Append("no se pudo guardar (" + (PartidaGuardada.MotivoDeBloqueo() ?? PartidaGuardada.UltimoAviso) + "); "); resultado(false); yield break; }
            foreach (var x in R20bContinuar(sb, resultado)) yield return x;
        }

        static IEnumerable R20bContinuar(StringBuilder sb, Action<bool> resultado)
        {
            var viejo = OperacionDirector.Instancia;
            if (!PartidaGuardada.ContinuarSinPantallaDeCarga()) { sb.Append("Continuar fallo (" + PartidaGuardada.UltimoAviso + "); "); resultado(false); yield break; }
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                yield return null;
                if (Time.realtimeSinceStartup - t0 > 90f) { sb.Append("la escena no volvio a cargar en 90 s; "); resultado(false); yield break; }
                var nuevo = OperacionDirector.Instancia;
                if (nuevo != null && nuevo != viejo && PartidaGuardada.PendienteDeRestaurar == null && !PartidaGuardada.Restaurando) break;
            }
            foreach (var x in Esperar(1.5f)) yield return x;
            resultado(true);
        }

        static IEnumerable R20bEscenaLimpia()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("SC_Operacion");
            yield return null; yield return null;
            foreach (var x in Esperar(1.0f)) yield return x;
        }

        static int R20bCapaDelJugador()
        {
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            return yo != null ? yo.gameObject.layer : 0;
        }

        // El collider esta encendido, no es trigger, choca con la capa del jugador y un rayo horizontal desde 3 m (por cada lado) lo toca.
        static bool R20bBloqueaElPaso(Collider c, out string det)
        {
            if (c == null) { det = "sin collider"; return false; }
            int capa = R20bCapaDelJugador();
            bool encendido = c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger;
            bool choca = !Physics.GetIgnoreLayerCollision(capa, c.gameObject.layer);
            int toques = 0;
            if (encendido)
            {
                var b = c.bounds;
                float y = Mathf.Min(b.center.y, b.min.y + 0.6f);
                foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                {
                    float ext = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z))));
                    var desde = new Vector3(b.center.x, y, b.center.z) + dir * (ext + 3f);
                    foreach (var h in Physics.RaycastAll(desde, -dir, ext + 3.5f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider == c) { toques++; break; }
                }
            }
            det = $"{c.name}: encendido={encendido} chocaConJugador={choca} rayos que lo tocan={toques}/4";
            return encendido && choca && toques >= 2;
        }

        // Colliders solidos encendidos del grupo de la terminal (Computadora) dentro de la caja de la mesa: tiene que dar 0 si la terminal volo.
        static int R20bSolidosEnLaTerminal(OperacionDirector d, out string nombres)
        {
            nombres = "";
            if (d.computadora == null) return 0;
            var t = d.computadora.transform;
            var grupo = t.parent != null ? t.parent : t;
            int n = 0;
            foreach (var c in Physics.OverlapBox(t.position, Vector3.Scale(t.lossyScale, new Vector3(0.5f, 0.5f, 0.5f)) + Vector3.one * 0.6f, t.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == null || !c.transform.IsChildOf(grupo)) continue;
                n++; nombres += c.name + " ";
            }
            return n;
        }

        static bool R20bMuroCaidoYSinChoque(OperacionDirector d, out string det)
        {
            var c = d.ColliderDelMuroDeSalida;
            bool sinChoque = c == null || !c.enabled || !c.gameObject.activeInHierarchy;
            det = $"muro en pie={d.MuroDeSalidaSigueEnPie} collider={(c == null ? "-" : c.enabled && c.gameObject.activeInHierarchy ? "ENCENDIDO" : "apagado")}";
            return !d.MuroDeSalidaSigueEnPie && sinChoque;
        }

        static void R20bVolada(ref bool ok, StringBuilder sb, OperacionDirector d, string cuando)
        {
            int solidos = R20bSolidosEnLaTerminal(d, out var nombres);
            W8Ok(ref ok, d.TerminalDestruida && d.TerminalSinRastro && solidos == 0, sb, $"[{cuando}] terminal volada (destruida={d.TerminalDestruida} sinRastro={d.TerminalSinRastro} solidos en su caja={solidos} {nombres})");
            W8Ok(ref ok, R20bMuroCaidoYSinChoque(d, out var dm), sb, $"[{cuando}] salida norte abierta ({dm})");
        }

        // Hackeo completo de verdad: la computadora explota (VoladuraDeLaTerminal) y el director pasa a Huir.
        static IEnumerable R20bExplotarLaComputadora(StringBuilder sb, Action<bool> resultado)
        {
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(2.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.computadora.ForzarDesbloqueo();
            d.computadora.Avanzar(d.computadora.DuracionEfectiva + 1f);
            float t0 = Time.realtimeSinceStartup;
            while (d.Fase != FaseOperacion.Huir && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            if (d.Fase != FaseOperacion.Huir) { sb.Append($"el hackeo completo no paso a Huir (fase={d.Fase} completo={d.computadora.Completo}); "); resultado(false); yield break; }
            foreach (var x in Esperar(2.0f)) yield return x;   // la cadena de explosiones y el muro (0,35 s)
            resultado(true);
        }

        // ---------------------------------------------------------------- 996a
        static IEnumerator Bug996a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            bool listo = false;
            foreach (var x in R20bExplotarLaComputadora(sb, v => listo = v)) yield return x;
            if (!listo) { Fin("FALLO " + sb); yield break; }
            var d = OperacionDirector.Instancia;
            R20bVolada(ref ok, sb, d, "antes de guardar");
            string antes = d.ResumenDeProgreso();

            // 1) Guardar en el acercamiento de la huida y continuar.
            bool cargo = false;
            foreach (var x in R20bGuardarYContinuar("auto: inicio objetivo 4", sb, v => cargo = v)) yield return x;
            if (!cargo) { Dificultad.Actual = dificultadPrevia; Fin("FALLO " + sb); yield break; }
            d = OperacionDirector.Instancia;
            var datos = PartidaGuardada.Cargar();
            W8Ok(ref ok, datos != null && datos.version == PartidaGuardada.VersionActual && !string.IsNullOrEmpty(datos.progreso), sb, $"archivo v{(datos != null ? datos.version : -1)} con progreso [{(datos != null ? datos.progreso : "")}]");
            string mision = "";
            if (datos != null && datos.estado != null) foreach (var p in datos.estado.mision) if (p.k == "Operacion.terminalVolada" || p.k == "Operacion.muroSalidaCaido" || p.k == "Operacion.tomaJefeHecha") mision += p.k.Substring(10) + "=" + p.v + " ";
            W8Ok(ref ok, mision.Contains("terminalVolada=True") && mision.Contains("muroSalidaCaido=True"), sb, "claves guardadas: " + mision);
            W8Ok(ref ok, d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Acercamiento, sb, $"fase {d.Fase}/{d.Subfase}");
            R20bVolada(ref ok, sb, d, "continuar en el acercamiento");
            string despues = d.ResumenDeProgreso();
            W8Ok(ref ok, antes == despues, sb, $"progreso [{antes}] -> [{despues}]");

            // 2) La pelea con el BLINDADO (el caso del pedido: mori en el blindado y cargue el ultimo punto).
            d.SaltarA(FaseOperacion.Huir, false, (int)SubfaseHuida.Jefe);
            foreach (var x in Esperar(1.0f)) yield return x;
            if (d.tanque != null) d.tanque.PonerVida01(0.55f);
            yield return null;
            W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Jefe, sb, $"en la pelea con el blindado (subfase {d.Subfase})");
            float vidaAntes = d.tanque != null && d.tanque.Health != null ? d.tanque.Health.Current / (float)d.tanque.Health.MaxHealth : -1f;
            cargo = false;
            foreach (var x in R20bGuardarYContinuar("auto: fin CineDeHuida (toma del jefe)", sb, v => cargo = v)) yield return x;
            if (!cargo) { Dificultad.Actual = dificultadPrevia; Fin("FALLO " + sb); yield break; }
            d = OperacionDirector.Instancia;
            float vidaDesp = d.tanque != null && d.tanque.Health != null ? d.tanque.Health.Current / (float)d.tanque.Health.MaxHealth : -1f;
            W8Ok(ref ok, d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Jefe, sb, $"fase {d.Fase}/{d.Subfase} (esperaba Huir/1)");
            W8Ok(ref ok, d.TomaDelJefeHecha && !d.TomaDelJefeActiva, sb, $"la toma del jefe no se repite (hecha={d.TomaDelJefeHecha} activa={d.TomaDelJefeActiva})");
            W8Ok(ref ok, Mathf.Abs(vidaDesp - vidaAntes) <= 0.08f, sb, $"vida del blindado {vidaAntes:0.00} -> {vidaDesp:0.00}");
            R20bVolada(ref ok, sb, d, "continuar en el blindado");
            // Un rato despues sigue igual (nada la vuelve a encender).
            foreach (var x in Esperar(2.0f)) yield return x;
            W8Ok(ref ok, d.TerminalSinRastro && !d.MuroDeSalidaSigueEnPie, sb, "2 s despues: terminal y salida siguen voladas");
            Dificultad.Actual = dificultadPrevia;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 996b
        static IEnumerator Bug996b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(2.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.computadora.ForzarDesbloqueo();
            d.computadora.Avanzar(d.computadora.DuracionEfectiva * 0.35f);
            yield return null;
            float progAntes = d.computadora.Progreso01;
            W8Ok(ref ok, d.TerminalEnPieYSolida && d.MuroDeSalidaSigueEnPie, sb, $"antes: terminal en pie={d.TerminalEnPieYSolida} muro en pie={d.MuroDeSalidaSigueEnPie} progreso={progAntes:0.00}");
            string antes = d.ResumenDeProgreso();
            bool cargo = false;
            foreach (var x in R20bGuardarYContinuar("manual", sb, v => cargo = v)) yield return x;
            if (!cargo) { Dificultad.Actual = dificultadPrevia; Fin("FALLO " + sb); yield break; }
            d = OperacionDirector.Instancia;
            W8Ok(ref ok, d.Fase == FaseOperacion.CentroDeDatos && !d.computadora.Completo && Mathf.Abs(d.computadora.Progreso01 - progAntes) <= 0.06f, sb, $"fase {d.Fase}, progreso {d.computadora.Progreso01:0.00} (guardado {progAntes:0.00}), completo={d.computadora.Completo}");
            W8Ok(ref ok, d.TerminalEnPieYSolida && !d.TerminalDestruida, sb, $"la terminal sin explotar se dibuja y es solida (enPie={d.TerminalEnPieYSolida} destruida={d.TerminalDestruida})");
            Collider mesa = null;
            foreach (var c in d.computadora.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) { mesa = c; break; }
            W8Ok(ref ok, R20bBloqueaElPaso(mesa, out var dmesa), sb, "no se traspasa la terminal: " + dmesa);
            W8Ok(ref ok, d.MuroDeSalidaSigueEnPie && R20bBloqueaElPaso(d.ColliderDelMuroDeSalida, out var dmuro), sb, $"la salida norte sigue cerrada y solida: {(d.ColliderDelMuroDeSalida != null ? R20bDet(d.ColliderDelMuroDeSalida) : "sin collider")}");
            string despues = d.ResumenDeProgreso();
            W8Ok(ref ok, antes == despues, sb, $"progreso [{antes}] -> [{despues}]");
            Dificultad.Actual = dificultadPrevia;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static string R20bDet(Collider c) { R20bBloqueaElPaso(c, out var det); return det; }

        // ---------------------------------------------------------------- 996c
        static IEnumerator Bug996c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            bool listo = false;
            foreach (var x in R20bExplotarLaComputadora(sb, v => listo = v)) yield return x;
            if (!listo) { Fin("FALLO " + sb); yield break; }
            if (!PartidaGuardada.Guardar("auto: inicio objetivo 4")) { Fin("FALLO no se pudo guardar: " + PartidaGuardada.UltimoAviso); yield break; }
            // Se reescribe como una partida v1: sin version 2, sin resumen y sin las claves nuevas del estado de la Operacion.
            string ruta = PartidaGuardada.RutaDe(PartidaGuardada.EscenaDeLaOperacion);
            var j = JsonUtility.FromJson<PartidaGuardadaDatos>(File.ReadAllText(ruta));
            j.version = 1; j.progreso = null;
            int quitadas = j.estado.mision.RemoveAll(p => p.k == "Operacion.terminalVolada" || p.k == "Operacion.muroSalidaCaido" || p.k == "Operacion.tomaJefeHecha" || p.k == "Operacion.cargaPorton");
            File.WriteAllText(ruta, JsonUtility.ToJson(j, false), new UTF8Encoding(false));
            W8Ok(ref ok, quitadas >= 3 && PartidaGuardada.HayPartida(), sb, $"partida v1 ({quitadas} claves nuevas quitadas) reconocida={PartidaGuardada.HayPartida()}");
            bool cargo = false;
            foreach (var x in R20bContinuar(sb, v => cargo = v)) yield return x;
            if (!cargo) { Dificultad.Actual = dificultadPrevia; Fin("FALLO " + sb); yield break; }
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, d.Fase == FaseOperacion.Huir, sb, $"fase {d.Fase}/{d.Subfase}");
            R20bVolada(ref ok, sb, d, "v1 continuada");
            // Otra version (999) se sigue rechazando.
            j.version = 999; File.WriteAllText(ruta, JsonUtility.ToJson(j, false), new UTF8Encoding(false));
            W8Ok(ref ok, !PartidaGuardada.HayPartida(), sb, "una version 999 se ignora");
            Dificultad.Actual = dificultadPrevia;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 996d
        struct R20bCheckpoint { public string motivo; public int objetivo, subfase; public bool embarcar; }

        static IEnumerator Bug996d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var informe = new StringBuilder(); bool ok = true;
            var dificultadPrevia = Dificultad.Actual;
            LimpiarPartidasDePrueba(); PartidaGuardada.AutoguardadoActivo = false;
            // Los autoguardados del juego: EntrarFase (inicio objetivo N), fin de la cinematica de apertura (objetivo 1), fin de la toma del jefe
            // (Huir/Jefe), subir al tanque (Huir en el tanque) y fin de la cinematica final (inicio del objetivo 5).
            var casos = new List<R20bCheckpoint>
            {
                new R20bCheckpoint { motivo = "auto: inicio objetivo 1", objetivo = 1 },
                new R20bCheckpoint { motivo = "auto: fin cinemática de apertura", objetivo = 1 },
                new R20bCheckpoint { motivo = "auto: inicio objetivo 2", objetivo = 2 },
                new R20bCheckpoint { motivo = "auto: inicio objetivo 3", objetivo = 3 },
                new R20bCheckpoint { motivo = "auto: inicio objetivo 4", objetivo = 4 },
                new R20bCheckpoint { motivo = "auto: fin CineDeHuida (toma del jefe)", objetivo = 4, subfase = (int)SubfaseHuida.Jefe },
                new R20bCheckpoint { motivo = "auto: subir al tanque", objetivo = 4, subfase = (int)SubfaseHuida.Abordaje, embarcar = true },
                new R20bCheckpoint { motivo = "auto: fin CineDeHuida (cinemática final) + inicio objetivo 5", objetivo = 5 },
                new R20bCheckpoint { motivo = "auto: inicio objetivo 6", objetivo = 6 },
            };
            foreach (var caso in casos)
            {
                var sb = new StringBuilder(); bool casoOk = true;
                foreach (var x in R20bEscenaLimpia()) yield return x;
                OperacionPrueba.Arrancar(caso.objetivo, 1, caso.subfase);
                foreach (var x in Esperar(2.5f)) yield return x;
                var d = OperacionDirector.Instancia;
                // En el juego real, llegar al objetivo 4 o mas implica el hackeo terminado: terminal volada y salida abierta (los atajos de prueba la dejan en pie).
                if (caso.objetivo >= 4) d.DejarTerminalVoladaYa();
                if (caso.embarcar) d.Embarcar();
                foreach (var x in Esperar(0.6f)) yield return x;
                string antes = d.ResumenDeProgreso();
                // En el juego la toma del jefe ya se vio en cualquier subfase posterior al acercamiento (el atajo de prueba no la marca).
                if (d.Fase == FaseOperacion.Huir && (d.Subfase >= (int)SubfaseHuida.Jefe || d.EnTanque)) antes = antes.Replace("toma=False", "toma=True");
                bool cargo = false;
                foreach (var x in R20bGuardarYContinuar(caso.motivo, sb, v => cargo = v)) yield return x;
                if (!cargo) casoOk = false;
                else
                {
                    d = OperacionDirector.Instancia;
                    string despues = d.ResumenDeProgreso();
                    var datos = PartidaGuardada.Cargar();
                    bool motivoOk = datos != null && datos.motivo == caso.motivo;
                    bool igual = antes == despues;
                    bool coherente = !despues.Contains("INCOHERENTE");
                    casoOk = igual && coherente && motivoOk;
                    sb.Append(igual ? $"[{despues}]" : $"[{antes}] -> [{despues}] (DISTINTO)");
                    if (!coherente) sb.Append(" terminal INCOHERENTE (se ve sin chocar o al reves)");
                    if (!motivoOk) sb.Append(" motivo MAL (" + (datos != null ? datos.motivo : "-") + ")");
                    if (caso.objetivo == 3)
                    {
                        Collider mesa = null;
                        foreach (var c in d.computadora.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) { mesa = c; break; }
                        bool solida = R20bBloqueaElPaso(mesa, out var dm);
                        if (!solida) { casoOk = false; sb.Append(" terminal traspasable: " + dm); }
                    }
                }
                if (!casoOk) ok = false;
                informe.Append($"[{(casoOk ? "OK" : "FALLO")}] {caso.motivo}: {sb} | ");
                yield return null;
            }
            Dificultad.Actual = dificultadPrevia;
            PartidaGuardada.Borrar();
            Fin((ok ? "OK " : "FALLO ") + informe);
        }
    }
}
