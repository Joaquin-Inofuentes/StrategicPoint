using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 19, carril E (PLAN_R19_v2 3.7 y 3.8; G3 parte E). Los de fachada (994g/h) van en R19e.cs (carril F).
    //   #994k  anillo de subida con el radio REAL (RadioDeAbordaje, en planta), onda de 3 aros al subir, plaza sin cartel y radio 5,
    //          cambiar de fase estando adentro no dispara onda.
    //   #994m  umbral del blindado visible en z = ZParaLaToma (-8): sin toma con el jugador en z -20, recordatorio a los ~25 s, toma al cruzar
    //          (el poseido o un aliado), la marca se apaga, ZFuegoDelJefe == -30.
    //   #994n  al embarcar, los enemigos de a pie cercanos reciben la orden de atacar al tanque (tope 8; perros y puestos fijos no).
    // Play sobre SC_Operacion; conviene un Play fresco por check (CorrerLista con aislar = true).
    public static partial class ChecksBugs065
    {
        static void R19cPoner(Soldier s, Vector3 p)
        {
            if (s == null) return;
            p.y = s.transform.position.y;
            s.transform.position = p;
            ApoyoEnElPiso.Apoyar(s.transform);
            Physics.SyncTransforms();
        }

        static Vector3 R19cPlano(Vector3 v) { v.y = 0f; return v; }

        // ---------------------------------------------------------------- 994k anillo de subida: radio real, onda, plaza, cambio de fase
        static IEnumerator Bug994k()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, (int)SubfaseHuida.Abordaje);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo(); var a = AnilloDeSubida.Instancia;
            if (d == null || d.tanque == null || yo == null || a == null) { ModoDios.Poner(false); Fin("FALLO no hay director, tanque, jugador o anillo"); yield break; }
            var t = d.tanque.transform;
            W8Ok(ref ok, AnilloDeSubida.EstaActivo && a.Objetivo == t, sb, $"Abordaje: anillo activo={AnilloDeSubida.EstaActivo} sobre el tanque={a.Objetivo == t}");
            W8Ok(ref ok, Mathf.Abs(a.RadioDeUso - OperacionDirector.RadioDeAbordaje) <= 0.05f, sb, $"radio de uso {a.RadioDeUso:0.00} m (RadioDeAbordaje {OperacionDirector.RadioDeAbordaje:0.00})");
            W8Ok(ref ok, a.TextoDelCartel == AnilloDeSubida.CartelDelTanque, sb, $"cartel '{a.TextoDelCartel}'");
            W8Ok(ref ok, Mathf.Approximately(a.EscalaLejosEfectiva, AnilloDeSubida.EscalaLejosRadioGrande), sb, $"escala lejana efectiva {a.EscalaLejosEfectiva:0.00} con radio >= 5 m");
            var lado = R19cPlano(t.right).normalized; if (lado.sqrMagnitude < 0.01f) lado = Vector3.right;
            R19cPoner(yo, t.position + lado * 5f);
            foreach (var x in Esperar(0.4f)) yield return x;
            var cu = a.ColorDeUso;
            W8Ok(ref ok, a.DentroDelRadio && cu.g > 0.9f && cu.r < 0.4f, sb, $"a {a.DistanciaAlJugador:0.0} m (planta): dentro={a.DentroDelRadio}, color de uso ({cu.r:0.00},{cu.g:0.00},{cu.b:0.00})");
            R19cPoner(yo, t.position + lado * 7.5f);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, !a.DentroDelRadio && a.LlamadaActiva, sb, $"a {a.DistanciaAlJugador:0.0} m: dentro={a.DentroDelRadio}, aros de llamada={a.LlamadaActiva}");
            R19cPoner(yo, t.position + lado * 5f);
            foreach (var x in Esperar(0.3f)) yield return x;
            int ondas0 = AnilloDeSubida.OndasLanzadas;
            d.Embarcar();
            // la onda se mide desde el primer cuadro en que se ve (el arbitro la lanza en el Update siguiente a Embarcar), en tiempo real y en
            // el reloj interno del anillo (SegundosDeOnda, dt sin escala), y se cuentan los cuadros del juego
            float tLim = Time.realtimeSinceStartup; float t0 = -1f; bool vioOnda = false; float maxInterno = 0f; int f0 = Time.frameCount;
            while (Time.realtimeSinceStartup - tLim < 4f && AnilloDeSubida.EstaActivo)
            {
                if (a.EnOnda) { if (!vioOnda) { vioOnda = true; t0 = Time.realtimeSinceStartup; f0 = Time.frameCount; } maxInterno = Mathf.Max(maxInterno, a.SegundosDeOnda); }
                yield return null;
            }
            float dur = t0 >= 0f ? Time.realtimeSinceStartup - t0 : -1f; int cuadros = Time.frameCount - f0;
            W8Ok(ref ok, d.EnTanque && vioOnda && AnilloDeSubida.OndasLanzadas == ondas0 + 1, sb, $"Embarcar: EnTanque={d.EnTanque}, onda vista={vioOnda}, ondas {ondas0}->{AnilloDeSubida.OndasLanzadas}");
            W8Ok(ref ok, !AnilloDeSubida.EstaActivo && maxInterno >= AnilloDeSubida.DuracionDeOnda - 0.15f && maxInterno <= AnilloDeSubida.DuracionDeOnda + 0.15f, sb, $"la onda duro {dur:0.00} s reales (el Editor corre a {cuadros} cuadros: se mide el reloj del anillo {maxInterno:0.00} s; esperado {AnilloDeSubida.DuracionDeOnda:0.00} +- 0,15) y despues apagado");
            // Resistir sin mando: anillo en la plaza, sin cartel, radio 5. Primero se baja del tanque (montado, el soldado lo arrastra el asiento)
            var drvK = SP.Player.PlayerInputDriver.Activo;
            if (drvK != null && drvK.CurrentSeat.HasValue) drvK.ExitVehicle();
            yield return null;
            OperacionDirector.ResistirSinMando = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            d = OperacionDirector.Instancia; yo = W9Yo(); a = AnilloDeSubida.Instancia;
            if (d == null || d.plaza == null || yo == null || a == null) { OperacionDirector.ResistirSinMando = false; ModoDios.Poner(false); Fin("FALLO " + sb + "; Resistir: no hay plaza/jugador/anillo"); yield break; }
            W8Ok(ref ok, AnilloDeSubida.EstaActivo && a.Objetivo == d.plaza && a.TextoDelCartel == "" && Mathf.Abs(a.RadioDeUso - OperacionDirector.RadioDeInteraccionDeBase) <= 0.05f, sb,
                $"Resistir sin mando: activo={AnilloDeSubida.EstaActivo} en la plaza={a.Objetivo == d.plaza} cartel='{a.TextoDelCartel}' radio {a.RadioDeUso:0.00} (esperado {OperacionDirector.RadioDeInteraccionDeBase:0.00})");
            R19cPoner(yo, d.plaza.position + Vector3.right * 2f);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, a.DentroDelRadio, sb, $"en la plaza a {a.DistanciaAlJugador:0.0} m: dentro={a.DentroDelRadio} (yo={yo.name} montado={(yo.Brain != null && yo.Brain.MontadoEnVehiculo)})");
            // cambiar de fase estando adentro: apagado seco, sin onda ni sonido
            int ondas1 = AnilloDeSubida.OndasLanzadas, secos1 = AnilloDeSubida.OcultadosSinOnda;
            OperacionDirector.ResistirSinMando = false;
            OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, !AnilloDeSubida.EstaActivo && AnilloDeSubida.OndasLanzadas == ondas1 && AnilloDeSubida.OcultadosSinOnda > secos1, sb,
                $"cambio de fase con el jugador adentro: activo={AnilloDeSubida.EstaActivo}, ondas {ondas1}->{AnilloDeSubida.OndasLanzadas} (sin cambio), apagados secos {secos1}->{AnilloDeSubida.OcultadosSinOnda}");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 994m umbral visible del blindado
        static List<Soldier> R19cAliados(Soldier yo)
        {
            var r = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s != yo && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) r.Add(s);
            return r;
        }

        static IEnumerator Bug994m()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            W8Ok(ref ok, OperacionDirector.ZFuegoDelJefe == -30f && OperacionDirector.ZParaLaToma == -8f, sb, $"ZParaLaToma={OperacionDirector.ZParaLaToma} ZFuegoDelJefe={OperacionDirector.ZFuegoDelJefe}");
            W8Ok(ref ok, UmbralDelBlindado.Cruzo(-7.9f) && !UmbralDelBlindado.Cruzo(-8.1f) && !UmbralDelBlindado.Cruzo(-20f), sb, "Cruzo(z) puro");
            OperacionPrueba.Arrancar(4, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            if (d == null || yo == null) { ModoDios.Poner(false); Fin("FALLO no hay director o jugador"); yield break; }
            var pasivos = new List<Soldier>();
            try
            {
                d.AbrirSalidaNortePorPrueba(false);
                float tAbierta = Time.realtimeSinceStartup;
                // la escuadra en z -20 (antes de la linea) y quieta: nadie cruza solo
                foreach (var s in R19cAliados(yo)) { R19cPoner(s, new Vector3(Random.Range(-4f, 4f), 0f, -21f)); if (s.Brain != null && !s.Brain.Pasivo) { s.Brain.Pasivo = true; pasivos.Add(s); } }
                R19cPoner(yo, new Vector3(0f, 0f, -20f));
                foreach (var x in Esperar(6f)) yield return x;
                var u = UmbralDelBlindado.Instancia;
                W8Ok(ref ok, !d.TomaDelJefeActiva && d.Subfase == (int)SubfaseHuida.Acercamiento, sb, $"6 s en z={yo.transform.position.z:0.0}: sin toma (subfase {d.Subfase})");
                W8Ok(ref ok, UmbralDelBlindado.Activa && u != null && Mathf.Abs(u.Z - OperacionDirector.ZParaLaToma) < 0.05f, sb, $"marca activa={UmbralDelBlindado.Activa} en z={(u != null ? u.Z : float.NaN):0.0}");
                int rend = u != null ? u.RenderersEncendidos : 0;
                W8Ok(ref ok, rend >= UmbralDelBlindado.Tramos + 4 && u != null && u.TextoMostrado == UmbralDelBlindado.TextoDelCartel, sb, $"marca visible: {rend} renderers encendidos (>= {UmbralDelBlindado.Tramos + 4}), cartel '{(u != null ? u.TextoMostrado : "")}'");
                // recordatorio a los 25 +- 3 s de abierta la salida
                int rec0 = d.RecordatoriosDeLaToma;
                while (Time.realtimeSinceStartup - tAbierta < OperacionDirector.SegundosDeRecordatorioDeLaToma + 4f && d.RecordatoriosDeLaToma == rec0) yield return null;
                float tRec = Time.realtimeSinceStartup - tAbierta;
                W8Ok(ref ok, d.RecordatoriosDeLaToma == rec0 + 1 && Mathf.Abs(tRec - OperacionDirector.SegundosDeRecordatorioDeLaToma) <= 3f && !d.TomaDelJefeActiva, sb, $"recordatorio a los {tRec:0.0} s (esperado {OperacionDirector.SegundosDeRecordatorioDeLaToma:0} +- 3), sin toma");
                // cruzar la linea: toma en <= 1 s y la marca se apaga en <= 1 s
                R19cPoner(yo, new Vector3(0f, 0f, -6f));
                float t0 = Time.realtimeSinceStartup; bool toma = false;
                while (Time.realtimeSinceStartup - t0 < 1.2f && !toma) { toma = d.TomaDelJefeActiva || d.Subfase != (int)SubfaseHuida.Acercamiento; yield return null; }
                float tToma = Time.realtimeSinceStartup - t0;
                W8Ok(ref ok, toma && tToma <= 1f && d.MotivoDeLaToma == "cruce", sb, $"al cruzar a z={yo.transform.position.z:0.0}: toma en {tToma:0.00} s (motivo '{d.MotivoDeLaToma}')");
                while (Time.realtimeSinceStartup - t0 < 1.2f && UmbralDelBlindado.Instancia != null && UmbralDelBlindado.Instancia.gameObject.activeSelf) yield return null;
                bool apagada = UmbralDelBlindado.Instancia == null || !UmbralDelBlindado.Instancia.gameObject.activeSelf;
                W8Ok(ref ok, apagada, sb, $"la marca se apaga en {Time.realtimeSinceStartup - t0:0.00} s");
            }
            finally { foreach (var s in pasivos) if (s != null && s.Brain != null) s.Brain.Pasivo = false; pasivos.Clear(); }
            // variante: el poseido atras (z -45) y un aliado cruza (z -6)
            OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(1f)) yield return x;
            d = OperacionDirector.Instancia; yo = W9Yo();
            try
            {
                d.AbrirSalidaNortePorPrueba(false);
                var aliados = R19cAliados(yo);
                foreach (var s in aliados) { R19cPoner(s, new Vector3(Random.Range(-4f, 4f), 0f, -46f)); if (s.Brain != null && !s.Brain.Pasivo) { s.Brain.Pasivo = true; pasivos.Add(s); } }
                R19cPoner(yo, new Vector3(0f, 0f, -45f));
                foreach (var x in Esperar(3f)) yield return x;
                W8Ok(ref ok, !d.TomaDelJefeActiva && d.Subfase == (int)SubfaseHuida.Acercamiento, sb, "variante: con todos en z -45, sin toma");
                if (aliados.Count == 0) { ok = false; sb.Append("; variante: no hay aliados MAL"); }
                else
                {
                    R19cPoner(aliados[0], new Vector3(0f, 0f, -6f));
                    float t0 = Time.realtimeSinceStartup; bool toma = false;
                    while (Time.realtimeSinceStartup - t0 < 1.2f && !toma) { toma = d.TomaDelJefeActiva || d.Subfase != (int)SubfaseHuida.Acercamiento; yield return null; }
                    W8Ok(ref ok, toma && d.MotivoDeLaToma == "cruce aliado" && yo.transform.position.z < -40f, sb, $"variante: {aliados[0].name} cruza a z -6 con el poseido en z={yo.transform.position.z:0.0} -> toma={toma} (motivo '{d.MotivoDeLaToma}')");
                }
            }
            finally { foreach (var s in pasivos) if (s != null && s.Brain != null) s.Brain.Pasivo = false; }
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 994n orden de atacar al tanque al embarcar
        static IEnumerator Bug994n()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, (int)SubfaseHuida.Abordaje);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            if (d == null || d.tanque == null || yo == null) { ModoDios.Poner(false); Fin("FALLO no hay director, tanque o jugador"); yield break; }
            var t = d.tanque.transform;
            // 4 enemigos de a pie a 30 m del tanque (+ un perro y un puesto fijo si hay, a 25 m: no deben recibir la orden)
            var comunes = new List<Soldier>(); Soldier perro = null, fijo = null;
            var tripulacion = new HashSet<Soldier>(d.tripulacionDelJefe ?? new Soldier[0]);
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.Brain == null || tripulacion.Contains(s) || s.Brain.MontadoEnVehiculo) continue;
                bool esPerro = s.GetComponent<PerroEnemigo>() != null;
                if (esPerro) { if (perro == null) perro = s; continue; }
                if (s.Brain.PuestoFijoPorRol) { if (fijo == null) fijo = s; continue; }
                if (comunes.Count < 4) comunes.Add(s);
            }
            if (comunes.Count < 4) { ModoDios.Poner(false); Fin($"FALLO solo hay {comunes.Count} enemigos comunes para la prueba"); yield break; }
            var adelante = R19cPlano(t.forward).normalized; if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
            // la escuadra quieta durante la prueba: si no, mata a los enemigos de la prueba antes de que reciban la orden
            var aliadosQuietos = new List<Soldier>();
            foreach (var s in R19cAliados(yo)) if (s.Brain != null && !s.Brain.Pasivo) { s.Brain.Pasivo = true; aliadosQuietos.Add(s); }
            for (int i = 0; i < comunes.Count; i++)
            {
                var s = comunes[i];
                // muchos enemigos de reserva viven bajo un padre INACTIVO (oleadas): activarlos a ellos solos no los pone en la jerarquia activa
                // y el filtro del director (activeInHierarchy) los descarta con razon. Se los saca del padre para la prueba.
                if (s.transform.parent != null && !s.transform.parent.gameObject.activeInHierarchy) s.transform.SetParent(null, true);
                if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
                s.Brain.Pasivo = false;
                R19cPoner(s, t.position + Quaternion.Euler(0f, -45f + 30f * i, 0f) * adelante * 30f);
            }
            if (perro != null) { if (!perro.gameObject.activeSelf) perro.gameObject.SetActive(true); R19cPoner(perro, t.position - adelante * 25f); }
            if (fijo != null) { if (!fijo.gameObject.activeSelf) fijo.gameObject.SetActive(true); R19cPoner(fijo, t.position + Quaternion.Euler(0f, 90f, 0f) * adelante * 25f); }
            R19cPoner(yo, t.position + R19cPlano(t.right).normalized * 4f);
            foreach (var x in Esperar(0.4f)) yield return x;
            int ordenes0 = d.OrdenesContraElTanque;
            var candidatos = d.CandidatosContraElTanque();
            int enLista = 0; foreach (var s in comunes) if (candidatos.Contains(s)) enLista++;
            sb.Append($"candidatos antes de subir: {candidatos.Count} ({enLista}/4 de la prueba)");
            d.Embarcar();
            foreach (var x in Esperar(2.5f)) yield return x;
            int conOrden = 0;
            foreach (var s in comunes) if (s != null && s.Brain != null && s.Brain.VehiculoOrdenado == d.tanque) conOrden++;
            foreach (var s in comunes)
                if (s != null) sb.Append($"; {s.name}: vivo={s.Health.IsAlive} jerarquia={s.gameObject.activeInHierarchy} pasivo={s.Brain.Pasivo} fijo={s.Brain.PuestoFijoPorRol} a {R19cPlano(s.transform.position - t.position).magnitude:0} m orden={(s.Brain.VehiculoOrdenado != null ? s.Brain.VehiculoOrdenado.name : "-")} estado={s.Brain.State}");
            int nuevas = d.OrdenesContraElTanque - ordenes0;
            W8Ok(ref ok, d.EnTanque, sb, $"EnTanque={d.EnTanque}");
            W8Ok(ref ok, conOrden >= 3, sb, $"{conOrden}/4 enemigos a 30 m con VehiculoOrdenado == tanque en 2,5 s (se piden >= 3)");
            W8Ok(ref ok, nuevas >= conOrden && nuevas <= OperacionDirector.MaximoContraElTanque, sb, $"ordenes dadas {nuevas} (tope {OperacionDirector.MaximoContraElTanque})");
            if (perro != null) W8Ok(ref ok, perro.Brain == null || perro.Brain.VehiculoOrdenado != d.tanque, sb, "el perro no recibe la orden"); else sb.Append("; perro: (n/a, no hay en la escena)");
            if (fijo != null) W8Ok(ref ok, fijo.Brain.VehiculoOrdenado != d.tanque, sb, $"el puesto fijo ({fijo.name}) no recibe la orden"); else sb.Append("; puesto fijo: (n/a)");
            foreach (var s in aliadosQuietos) if (s != null && s.Brain != null) s.Brain.Pasivo = false;
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
