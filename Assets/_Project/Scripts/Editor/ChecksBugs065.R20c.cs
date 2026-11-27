using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Ronda 20, carril C (Docs/COLA_PENDIENTES.md punto 3): "balancear parte blindado: casi imposible de ganar, con 5 bombazos ya exploto".
    //   #997a  aguante del tanque del jugador (sintetico, sin modo dios): cuantos golpes de cada fuente lo destruyen con la curva de dano de la
    //          carrera puesta (cañonazo del BLINDADO GOLIAT, cohete de un enemigo de a pie, obus de camioneta). Exige >= GolpesMinimos de explosion.
    //   #997b  el BLINDADO sigue ganable con juego razonable (sin modo dios): el poseido (Vega, Asalto) tira un cohete cada 5 s con 80 % de punteria
    //          y esquiva el 70 % de los circulos rojos; la escuadra IA pelea sola. Mide tiempo hasta la rendicion, cañonazos, golpes y bajas.
    //   #997c  la carrera con juego razonable (sin modo dios): el canon le tira cada 1,6 s al blanco hostil mas cercano (75 % de punteria).
    //          Mide golpes grandes (>= 5 % de la vida), vida minima y si el tanque llega.
    // Play sobre SC_Operacion; un Play fresco por check (CorrerLista con aislar = true). Las tasas de derrota salen de correr 997b/997c 3 veces.
    public static partial class ChecksBugs065
    {
        public const int R20cGolpesMinimos = 8;          // pedido: que el tanque aguante bastante mas que 5 "bombazos"
        public const float R20cTiempoMaximoDelJefe = 150f; // s reales con juego razonable

        static Soldier R20cEnemigoCualquiera()
        {
            Soldier vivo = null, cualquiera = null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy) continue;
                if (cualquiera == null) cualquiera = s;
                if (s.Health != null && s.Health.IsAlive) { vivo = s; break; }
            }
            return vivo != null ? vivo : cualquiera;
        }

        // Golpes de una fuente hasta llevar al tanque a 0 (con un piso de 2 HP para poder medir varias fuentes seguidas). Entre golpe y golpe
        // se devuelve lo que le saco cualquier otra cosa (camionetas, tiradores): solo cuentan nuestros golpes. Separados una ventana de presupuesto
        // de la curva (TanqueJefe.TanqueDelJugadorVentanaDePresupuesto + 0,1 s): cada golpe llega con la ventana libre.
        static IEnumerable R20cGolpesHastaCaer(Vehicle t, Action golpe, Action<int, string> resultado, int tope = 60)
        {
            t.PonerVida01(1f);
            float pisoPrevio = t.PisoDeVida01;
            t.PisoDeVida01 = 1.5f / t.Health.MaxHealth;   // 2 HP
            // Se cuentan los golpes hasta que lo acumulado llega al 96 % de la vida maxima: cerca del piso de medicion los golpes salen
            // recortados y contarlos inflaria el numero (el golpe que cruza el 96 % es, en el juego, el que lo destruye).
            int n = 0; var tr = new StringBuilder();
            int esperado = t.Health.Current, acumulado = 0, max = t.Health.MaxHealth;
            while (n < tope && acumulado < max * 0.96f && t.Health.Current > t.PisoDeVida && !t.IsDestroyed)
            {
                if (t.Health.Current < esperado) t.Health.Heal(esperado - t.Health.Current);
                int h0 = t.Health.Current;
                golpe();
                n++;
                int dd = h0 - t.Health.Current;
                acumulado += dd;
                if (n <= 14) tr.Append(Mathf.RoundToInt(dd * 100f / max)).Append("% ");
                esperado = t.Health.Current;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < TanqueJefe.TanqueDelJugadorVentanaDePresupuesto + 0.1f)
                {
                    if (t.Health.Current < esperado && !t.IsDestroyed) t.Health.Heal(esperado - t.Health.Current);
                    yield return null;
                }
            }
            t.PisoDeVida01 = pisoPrevio;
            t.PonerVida01(1f);
            resultado(n, tr.ToString().Trim());
        }

        static void R20cExplosionContra(Vehicle t, int dano, int atacante)
        {
            Vehicle.DanoDeExplosionEnCurso = true;
            try { t.TakeDamage(dano, atacante); }
            finally { Vehicle.DanoDeExplosionEnCurso = false; }
        }

        // ---------------------------------------------------------------- 997a aguante del tanque del jugador
        static IEnumerator Bug997a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, (int)SubfaseHuida.Abordaje);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            if (d == null || d.tanque == null) { Fin("FALLO sin director o tanque"); yield break; }
            ModoDios.Poner(false);
            d.Embarcar();
            foreach (var x in Esperar(7f)) yield return x;   // pasan los 6 s de gracia de la curva
            var t = d.tanque;
            var en = R20cEnemigoCualquiera(); int idE = en != null ? en.Id : 9999;
            W8Ok(ref ok, d.EnTanque && t.Bando == TeamId.Player && t.Health.MaxHealth == TanqueJefe.VidaMaxima && t.AjusteDeDano != null, sb, $"a bordo: vida {t.Health.Current}/{t.Health.MaxHealth}, curva de dano puesta");
            int nCanon = 0, nCohete = 0; string trC = "", trR = "";
            // Cohete de un enemigo de a pie (lanzacohetes: 95 x 2,5 contra vehiculos) - el "bombazo" de la orden de ataque al tanque (R19 E4).
            foreach (var x in R20cGolpesHastaCaer(t, () => R20cExplosionContra(t, Mathf.RoundToInt(95 * 2.5f), idE), (n, s) => { nCohete = n; trR = s; })) yield return x;
            // Cañonazo del BLINDADO GOLIAT (el obus del jefe, vehiculo enemigo -3) en el casco, en una carrera NUEVA (la primera ya va por la
            // mitad). Tope de 12 golpes: la carrera dura ~76 s y a ~4 % por golpe no llega a 0 (12 golpes ya prueban "aguanta >= 8").
            var drv0 = SP.Player.PlayerInputDriver.Activo;
            if (drv0 != null && drv0.CurrentSeat.HasValue) drv0.ExitVehicle();
            yield return null;
            OperacionPrueba.Arrancar(4, 1, (int)SubfaseHuida.Abordaje);
            foreach (var x in Esperar(1.5f)) yield return x;
            d = OperacionDirector.Instancia; t = d.tanque;
            d.Embarcar();
            foreach (var x in Esperar(7f)) yield return x;
            foreach (var x in R20cGolpesHastaCaer(t, () => R20cExplosionContra(t, TanqueJefe.DanoDelCanon, -3), (n, s) => { nCanon = n; trC = s; }, 12)) yield return x;
            // (los obuses de camioneta, -3, pesan igual que el cañonazo: los topa la misma ventana; las rafagas de balas tambien)
            sb.Append($"[medicion] canonazos del Goliat={nCanon} ({trC}) · cohetes de a pie={nCohete} ({trR}); ");
            W8Ok(ref ok, nCanon >= R20cGolpesMinimos, sb, $"aguanta {nCanon} cañonazos del BLINDADO (>= {R20cGolpesMinimos})");
            W8Ok(ref ok, nCohete >= R20cGolpesMinimos, sb, $"aguanta {nCohete} cohetes de enemigos de a pie (>= {R20cGolpesMinimos})");
            W8Ok(ref ok, nCohete <= 16, sb, "y no es invulnerable (<= 16 cohetes)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 997b el BLINDADO es ganable con juego razonable
        static IEnumerator Bug997b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var vega = Escuadrista(RoleType.Assault);
            if (d == null || d.tanque == null || vega == null) { Fin("FALLO sin director, tanque o Asalto"); yield break; }
            Poseer(vega); yield return null;
            ModoDios.Poner(false); SP.Combat.WeaponHolder.MunicionInfinitaConModoDios = false;
            var t = d.tanque;
            int vidaJefeEscena = t.Health.MaxHealth;
            d.TeletransportarEscuadra(new Vector3(0f, 0f, -4f), Quaternion.identity);
            float tt = Time.realtimeSinceStartup;
            while (d.SubfaseDeHuida != SubfaseHuida.Jefe && Time.realtimeSinceStartup - tt < 10f) yield return null;
            var j = d.Jefe;
            if (j == null || d.SubfaseDeHuida != SubfaseHuida.Jefe) { Fin("FALLO no empezo la pelea (subfase " + d.Subfase + ")"); yield break; }
            var rnd = new System.Random(Environment.TickCount);
            var escuadra = Escuadra3();
            var vivoAntes = new Dictionary<Soldier, bool>(); var vidaAntes = new Dictionary<Soldier, int>();
            foreach (var s in escuadra) { vivoAntes[s] = s.Health.IsAlive; vidaAntes[s] = s.Health.Current; }
            int bajas = 0, golpeados = 0, impactosConVictima = 0, cohetes = 0, aciertos = 0, esquivas = 0, avisosVistos = 0, canonPrevio = j.Canonazos, danoMaxPorCanon = 0;
            bool esquivarEste = false; int avisoPrevio = j.InicioDeAvisos.Count;
            float t0 = Time.realtimeSinceStartup, proximoCohete = t0 + 3f;
            bool derrota = false;
            while (j.Situacion != TanqueJefe.Estado.Rendido && Time.realtimeSinceStartup - t0 < 240f)
            {
                if (d.Fase == FaseOperacion.Derrota || SP.Mision.EstadoDePartida.Perdio) { derrota = true; break; }
                var yo = W9Yo();
                // esquiva: el 70 % de los avisos (con reaccion de ~0,5 s) el poseido sale del circulo
                if (j.InicioDeAvisos.Count != avisoPrevio) { avisoPrevio = j.InicioDeAvisos.Count; avisosVistos++; esquivarEste = rnd.NextDouble() < 0.7; }
                var an = j.AnilloActual;
                if (esquivarEste && an != null && an.Activo && an.Progreso01 >= 0.35f && yo != null && yo.Health.IsAlive)
                {
                    var dd = R19cPlano(yo.transform.position - an.Centro);
                    if (dd.magnitude < an.Radio + 0.5f)
                    {
                        if (dd.sqrMagnitude < 0.01f) dd = Vector3.right;
                        R19cPoner(yo, an.Centro + dd.normalized * (an.Radio + 1.5f));
                        esquivas++;
                    }
                    esquivarEste = false;
                }
                // cohetes: uno cada 5 s (disparo 1,2 + recarga 2,8 + apuntar), 80 % de aciertos, a <= 45 m
                if (Time.realtimeSinceStartup >= proximoCohete && yo != null && yo.Health.IsAlive && j.FuegoHabilitado)
                {
                    proximoCohete += 5f;
                    if (R19cPlano(yo.transform.position - t.transform.position).magnitude <= 45f)
                    {
                        cohetes++;
                        if (rnd.NextDouble() < 0.8) { aciertos++; Projectile.ExplodeAt(t.transform.position + Vector3.up * 1.2f, 5f, 95, yo.Id, TeamId.Player, null, 2.5f); }
                    }
                }
                yield return null;
                // efecto del cañonazo sobre la escuadra (en el cuadro del impacto)
                bool hubo = j.Canonazos != canonPrevio; canonPrevio = j.Canonazos;
                bool alguien = false;
                foreach (var s in escuadra)
                {
                    if (s == null) continue;
                    int dv = vidaAntes[s] - s.Health.Current;
                    if (hubo && dv >= 15) { golpeados++; alguien = true; danoMaxPorCanon = Mathf.Max(danoMaxPorCanon, dv); }
                    if (vivoAntes[s] && !s.Health.IsAlive) bajas++;
                    vivoAntes[s] = s.Health.IsAlive; vidaAntes[s] = s.Health.Current;
                }
                if (alguien) impactosConVictima++;
            }
            float dur = Time.realtimeSinceStartup - t0;
            bool rendido = j.Situacion == TanqueJefe.Estado.Rendido;
            int vivos = 0; foreach (var s in escuadra) if (s != null && s.Health.IsAlive) vivos++;
            sb.Append($"[medicion] vida del blindado {vidaJefeEscena}->{t.Health.MaxHealth} · rendido={rendido} en {dur:0} s · cañonazos={j.Canonazos} (avisos {avisosVistos}, esquivas {esquivas}) · con victima={impactosConVictima} golpes a soldados={golpeados} (max {danoMaxPorCanon}) · bajas={bajas} vivos {vivos}/{escuadra.Count} · cohetes {aciertos}/{cohetes} · cargas={j.CargasAdosadas} · derrota={derrota}; ");
            W8Ok(ref ok, !derrota, sb, "sin derrota");
            W8Ok(ref ok, rendido && dur <= R20cTiempoMaximoDelJefe, sb, $"el blindado se rinde en {dur:0} s (<= {R20cTiempoMaximoDelJefe:0})");
            W8Ok(ref ok, j.Canonazos >= 4, sb, $"y hubo pelea: {j.Canonazos} cañonazos (>= 4: no es trivial)");
            ModoDios.Poner(false); SP.Combat.WeaponHolder.MunicionInfinitaConModoDios = true;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 997c la carrera con juego razonable
        static IEnumerator Bug997c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, (int)SubfaseHuida.Abordaje);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            if (d == null || d.tanque == null) { Fin("FALLO sin director o tanque"); yield break; }
            ModoDios.Poner(false);
            var t = d.tanque;
            d.Embarcar();
            var rnd = new System.Random(Environment.TickCount);
            int max = t.Health.MaxHealth, ultimo = t.Health.Current, grandes = 0, mayor = 0, golpes = 0, disparos = 0;
            float minimo = 1f; var tr = new StringBuilder();
            var sub = EventBus.Instance.Subscribe<VehicleDamagedEvent>(e =>
            {
                if (e.Vehicle != t) return;
                int dv = ultimo - e.RemainingHealth; ultimo = e.RemainingHealth;
                if (dv <= 0) return;
                golpes++; mayor = Mathf.Max(mayor, dv);
                if (dv >= max * 0.05f) { grandes++; if (grandes <= 12) tr.Append(Mathf.RoundToInt(dv * 100f / max)).Append("% "); }
            });
            float t0 = Time.realtimeSinceStartup, proximo = t0 + 2f;
            try
            {
                while (!d.FinalActiva && d.Fase == FaseOperacion.Huir && Time.realtimeSinceStartup - t0 < 150f && !t.IsDestroyed)
                {
                    if (ultimo < t.Health.Current) ultimo = t.Health.Current;
                    minimo = Mathf.Min(minimo, t.Health.Current / (float)max);
                    if (Time.realtimeSinceStartup >= proximo)
                    {
                        proximo += 1.6f;
                        // blanco: camioneta viva o enemigo de a pie a <= 55 m
                        Vector3? blanco = null; float mejor = 55f;
                        foreach (var a in UnityEngine.Object.FindObjectsByType<OperacionAuto>(FindObjectsSortMode.None))
                        {
                            if (a == null || a.Muerto) continue;
                            float dd = R19cPlano(a.transform.position - t.transform.position).magnitude;
                            if (dd < mejor) { mejor = dd; blanco = a.transform.position; }
                        }
                        if (blanco == null)
                            foreach (var s in ActorRegistry.All)
                            {
                                if (s == null || s.Team != TeamId.Enemy || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                                float dd = R19cPlano(s.transform.position - t.transform.position).magnitude;
                                if (dd < mejor) { mejor = dd; blanco = s.transform.position; }
                            }
                        var yo = W9Yo();
                        if (blanco.HasValue && rnd.NextDouble() < 0.75)
                        {
                            disparos++;
                            Projectile.ExplodeAt(blanco.Value + Vector3.up * 0.8f, OperacionDirector.RadioDelCanon, OperacionDirector.DanoDelCanon, yo != null ? yo.Id : -1, TeamId.Player, t, 1f);
                        }
                    }
                    yield return null;
                }
            }
            finally { sub.Dispose(); }
            bool llego = d.FinalActiva || d.Fase == FaseOperacion.Resistir;
            bool destruido = t.IsDestroyed || d.Fase == FaseOperacion.Derrota;
            sb.Append($"[medicion] llego={llego} destruido={destruido} motivo='{(destruido ? d.Motivo : "")}' a los {d.Reloj:0} s · vida minima {minimo * 100f:0}% · golpes={golpes} grandes(>=5%)={grandes} ({tr.ToString().Trim()}) mayor={mayor * 100f / max:0}% · disparos del canon={disparos} · reparado en ventanas={(d.Jefe != null ? d.Jefe.ReparadoEnLaCarrera * 100f / max : 0f):0}%; ");
            W8Ok(ref ok, llego && !destruido, sb, "el tanque llega al final con juego razonable");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
