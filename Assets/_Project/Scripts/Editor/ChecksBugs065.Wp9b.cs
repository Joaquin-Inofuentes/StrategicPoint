using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // WP9b: HUIR rediseñado (#097 parte 2 + #073 ajuste). Hay que estar en Play sobre SC_Operacion y conviene un Play FRESCO por check
    // (ChecksBugs065.CorrerUno("Bug097j")). Los de la carrera tardan ~85 s reales (la ruta es por tiempo: no se adelanta el reloj).
    // OJO con las letras: Bug097a/b/i ya son de WP9a (cinematica y HUD de pasos), asi que el jefe va en j, la reparacion en k, la carrera en l,
    // la variante con segundo helicoptero y disparo manual en m, y la compatibilidad de Embarcar() en n.
    //   097j  el jefe: toma de 3,5 s con el HUD apagado y el control bloqueado; BLINDADO GOLIAT (bando Enemy, 1600, piso del 5 %), canonazo con aviso
    //         en el piso (1,3-1,6 s antes de cada impacto, radio 5,5), balas hacen 1 y avisan, explosiones lo bajan, carga adosada 600, se rinde
    //         (frena en 2 s, la tripulacion baja a pie) y la fase pasa a Reparacion.
    //   097k  la reparacion: Kes (Flanqueadora) camina al tanque y repara 25 s, se pausa con un enemigo a 4 m, oleadas de 6+5+6 y una camioneta,
    //         al terminar el tanque es del jugador (bando Player, 70 %, piso 20 %) y la fase pasa a Abordaje.
    //   097l  la carrera de punta a punta sin tocar nada (sin invulnerabilidad): ruta de 18 puntos en 70 +- 3 s, Halcon a los ~25 s, pasadas con aviso,
    //         emboscadas, barricadas aplastadas, el tanque nunca baja del 20 %, y la cinematica final (camara lenta 4 s, ambos destruidos, tres en
    //         pie a menos de 6 m, Resistir en menos de 10 s).
    //   097m  variante: el primer Halcon se derriba a los ~33 s, aparece OTRO en la cinematica ("¡OTRO HELICOPTERO!") y el disparo es manual.
    //   097n  compatibilidad: Embarcar() directo desde la entrada del objetivo (checks viejos) da por hechos el jefe y la reparacion.
    //   073b  conteos de la carrera: 4 camionetas a la vez, vida 260, camionetas + tiradores + helicoptero >= 14.
    public static partial class ChecksBugs065
    {
        const string W9bPrefijo = "v2_097b_";

        static IEnumerable W9bHasta(Func<bool> cond, float maxSegundos)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!cond() && Time.realtimeSinceStartup - t0 < maxSegundos) yield return null;
        }

        static Soldier W9bPorRol(RoleType rol)
        {
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.Role == rol) return s;
            return null;
        }

        static Soldier W9bEnemigoCerca(Vector3 p)
        {
            Soldier mejor = null; float md = 1e9f;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue;
                float d = (s.transform.position - p).sqrMagnitude;
                if (d < md) { md = d; mejor = s; }
            }
            return mejor;
        }

        // ---------------------------------------------------------------- 097j el jefe
        static IEnumerator Bug097j()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var t = d.tanque; var j = d.Jefe;
            var hud = OperacionHud.Instancia; var drv = PlayerInputDriver.Activo;
            if (d == null || j == null || hud == null || drv == null) { Fin("FALLO sin director/jefe/HUD/driver"); yield break; }
            ModoDios.Poner(true);
            try
            {
                // Antes de la toma: dormido, enemigo, con su tripulacion adentro y sin disparar.
                W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Acercamiento && j.Situacion == TanqueJefe.Estado.Dormido, sb, "al llegar: subfase 0 y jefe dormido");
                W8Ok(ref ok, t.Bando == TeamId.Enemy && t.Health.MaxHealth == TanqueJefe.VidaMaxima && t.Health.Current == TanqueJefe.VidaMaxima, sb, $"BLINDADO bando={t.Bando} vida={t.Health.Current}/{t.Health.MaxHealth}");
                W8Ok(ref ok, t.PisoDeVida == 80 && t.Occupants.Count == 2 && t.Gunner == null && t.Driver != null, sb, $"piso={t.PisoDeVida} tripulacion={t.Occupants.Count} (conductor + metralleta, sin artillero)");
                foreach (var x in Esperar(3f)) yield return x;
                W8Ok(ref ok, j.Canonazos == 0 && j.InicioDeAvisos.Count == 0 && !hud.BarraDeJefeVisible, sb, "dormido no dispara ni muestra la barra");

                // La toma: al pasar z > -30 el control se bloquea 3,5 s con el titulo del jefe.
                d.TeletransportarEscuadra(new Vector3(0f, 0f, -24f), Quaternion.identity);
                foreach (var x in W9bHasta(() => CineDeHuida.Activa, 3f)) yield return x;
                float tToma0 = Time.realtimeSinceStartup;
                bool tomaActiva = CineDeHuida.Activa;
                W8Ok(ref ok, tomaActiva && d.TomaDelJefeActiva, sb, "la toma arranca al cruzar z=-30");
                W8Ok(ref ok, !drv.enabled && AiBrainPausada(), sb, "durante la toma: input del jugador y IA bloqueados");
                foreach (var x in Esperar(1.6f)) yield return x;
                string tarjeta = CineDeHuida.Instancia != null ? CineDeHuida.Instancia.TextoDeLaTarjeta : "";
                W8Ok(ref ok, tarjeta.Contains(TanqueJefe.NombreDelJefe), sb, $"tarjeta del jefe '{tarjeta}'");
                foreach (var x in CapturarPantalla(W9bPrefijo + "toma.png")) yield return x;
                foreach (var x in W9bHasta(() => !CineDeHuida.Activa, 4f)) yield return x;
                float durToma = Time.realtimeSinceStartup - tToma0;
                W8Ok(ref ok, !CineDeHuida.Activa && drv.enabled && d.Subfase == (int)SubfaseHuida.Jefe && durToma >= 3.2f && durToma <= 4.0f, sb, $"la toma dura {durToma:0.0} s (3,5 +- 0,5), devuelve el control y entra Jefe");
                foreach (var x in Esperar(0.3f)) yield return x;
                W8Ok(ref ok, j.Situacion == TanqueJefe.Estado.Peleando && hud.BarraDeJefeVisible && hud.TextoJefe.Contains(TanqueJefe.NombreDelJefe), sb, $"pelea: barra '{hud.TextoJefe}'");

                // Foto desde donde esta la escuadra: el blindado a lo lejos.
                var yo = W9Yo();
                var desde = yo.transform.position + Vector3.up * 2.4f - Vector3.forward * 1.5f;
                var foco = t.transform.position + Vector3.up * 1.5f;
                CapturarDesde(desde, Quaternion.LookRotation(foco - desde, Vector3.up), W9bPrefijo + "jefe_lejos.png", 1600, 900, 50f);

                // Canonazos: aviso rojo en el piso 1,3-1,6 s antes de cada impacto.
                float maxAnillos = 0f, radioVisto = 0f, distAnillo = 99f; bool fotoAviso = false;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 24f && j.InstantesDeImpacto.Count < 3)
                {
                    maxAnillos = Mathf.Max(maxAnillos, AnilloDeAviso.Activos);
                    var an = j.AnilloActual;
                    if (an != null && an.Activo)
                    {
                        radioVisto = an.Radio;
                        if (distAnillo > 90f)
                        {
                            var q = W9bSoldadoMasCercano(an.Centro);
                            if (q != null) { var dd = q.transform.position - an.Centro; dd.y = 0f; distAnillo = dd.magnitude; }
                        }
                        if (!fotoAviso && an.Progreso01 > 0.5f)
                        {
                            fotoAviso = true;
                            var c = an.Centro; var pos = c + new Vector3(-9f, 6f, -10f);
                            CapturarDesde(pos, Quaternion.LookRotation(c + Vector3.up * 0.5f - pos, Vector3.up), W9bPrefijo + "aviso_canon.png", 1600, 900, 55f);
                        }
                    }
                    yield return null;
                }
                int n = Mathf.Min(j.InstantesDeImpacto.Count, j.InicioDeAvisos.Count);
                bool tiempos = n >= 3; var tt = new StringBuilder();
                for (int i = 0; i < n; i++) { float dt = j.InstantesDeImpacto[i] - j.InicioDeAvisos[i]; tt.Append(dt.ToString("0.00")).Append(' '); if (dt < 1.3f || dt > 1.6f) tiempos = false; }
                W8Ok(ref ok, tiempos, sb, $"{n} canonazos con aviso previo de {tt}s (1,3-1,6)");
                W8Ok(ref ok, maxAnillos >= 1f && Mathf.Abs(radioVisto - TanqueJefe.RadioDelCanon) < 0.01f && distAnillo < 1.6f, sb, $"anillo rojo: activos={maxAnillos}, radio={radioVisto:0.0}, a {distAnillo:0.0} m de un soldado");

                // Una bala hace 1 y avisa; una explosion de arma pesada hace mucho.
                int h0 = t.Health.Current;
                t.TakeDamage(60, -1);
                int dBala = h0 - t.Health.Current;
                W8Ok(ref ok, dBala >= 1 && dBala <= 3 && j.CartelesDeBalas >= 1, sb, $"bala directa: -{dBala} (cartel NECESITAS COHETES: {j.CartelesDeBalas})");
                h0 = t.Health.Current;
                var yoId = W9Yo() != null ? W9Yo().Id : -1;
                Projectile.ExplodeAt(t.transform.position + Vector3.up * 1.2f, 5.5f, 95, yoId, TeamId.Player, null, 2.5f);
                int dExp = h0 - t.Health.Current;
                W8Ok(ref ok, dExp >= 300, sb, $"cohete (95x2,5 en el casco): -{dExp}");
                // Carga adosada del Asalto: 600 a los 3 s.
                h0 = t.Health.Current;
                j.AdosarCarga();
                foreach (var x in Esperar(3.6f)) yield return x;
                int dCarga = h0 - t.Health.Current;
                W8Ok(ref ok, dCarga >= 550 || t.Health.Current <= t.PisoDeVida, sb, $"carga adosada: -{dCarga}");

                // Hasta el piso: nunca baja de 80, se rinde, frena en 2 s, baja la tripulacion y pasa a Reparacion.
                int minimo = t.Health.Current; int tiros = 0;
                while (j.Situacion != TanqueJefe.Estado.Rendido && tiros < 8)
                {
                    Projectile.ExplodeAt(t.transform.position + Vector3.up * 1.2f, 5.5f, 95, yoId, TeamId.Player, null, 2.5f);
                    tiros++; minimo = Mathf.Min(minimo, t.Health.Current);
                    yield return null; yield return null;
                }
                minimo = Mathf.Min(minimo, t.Health.Current);
                W8Ok(ref ok, j.Situacion == TanqueJefe.Estado.Rendido && minimo >= t.PisoDeVida && t.Health.Current == t.PisoDeVida, sb, $"se rinde en el piso: vida={t.Health.Current} minimo={minimo} (piso {t.PisoDeVida}) tras {tiros} cohetes mas");
                float tr = Time.realtimeSinceStartup; float velOk = -1f;
                while (Time.realtimeSinceStartup - tr < 2f) { if (velOk < 0f && j.VelocidadActual < 0.3f) velOk = Time.realtimeSinceStartup - tr; yield return null; }
                W8Ok(ref ok, velOk >= 0f && j.VelocidadActual < 0.3f, sb, $"frena <0,3 m/s en {velOk:0.0} s");
                foreach (var x in W9bHasta(() => j.TripulacionBajo, 3f)) yield return x;
                int bajaron = 0;
                if (d.tripulacionDelJefe != null) foreach (var s in d.tripulacionDelJefe) if (s != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Team == TeamId.Enemy) bajaron++;
                W8Ok(ref ok, j.TripulacionBajo && bajaron == 2 && t.Occupants.Count == 0, sb, $"la tripulacion baja a pie ({bajaron}/2 enemigos, tanque vacio)");
                foreach (var x in Esperar(0.3f)) yield return x;
                W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Reparacion && !hud.BarraDeJefeVisible, sb, $"pasa a Reparacion (subfase {d.Subfase}), barra de jefe oculta");
            }
            finally { ModoDios.Poner(false); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static bool AiBrainPausada() => SP.Ai.AiBrain.IAPausada;

        static Soldier W9bSoldadoMasCercano(Vector3 p)
        {
            Soldier mejor = null; float md = 1e9f;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = (s.transform.position - p).sqrMagnitude;
                if (d < md) { md = d; mejor = s; }
            }
            return mejor;
        }

        // ---------------------------------------------------------------- 097k la reparacion
        static IEnumerator Bug097k()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, 2);
            Poseer(Escuadrista(RoleType.Assault));   // #110: el que manejas al empezar es Kes; este check mide a Kes como IA (reparadora), asi que se pasa a Vega
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var t = d.tanque; var r = d.Reparacion; var j = d.Jefe;
            if (d == null || r == null || j == null) { Fin("FALLO sin director/reparacion/jefe"); yield break; }
            ModoDios.Poner(true);
            try
            {
                float inicio = Time.realtimeSinceStartup;
                var kes = W9bPorRol(RoleType.Flanker);
                W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Reparacion && j.Situacion == TanqueJefe.Estado.Rendido && t.Bando == TeamId.Enemy && t.Health.Current == t.PisoDeVida && t.Occupants.Count == 0, sb, $"entrada: subfase {d.Subfase}, jefe rendido, vida {t.Health.Current}");
                W8Ok(ref ok, r.Reparador == kes && kes != null && r.TextoDeBarra.Contains("REPARANDO"), sb, $"reparadora: {(r.Reparador != null ? r.Reparador.DisplayName : "-")} · barra '{r.TextoDeBarra}'");
                foreach (var x in W9bHasta(() => r.Progreso01 > 0.03f, 25f)) yield return x;
                W8Ok(ref ok, r.Progreso01 > 0.03f, sb, $"Kes camina al tanque y empieza a reparar ({Time.realtimeSinceStartup - inicio:0.0} s)");
                var hudx = OperacionHud.Instancia;
                // Pausa con un enemigo a 4 m: se acerca uno de la primera oleada, quieto, y se mide.
                var en = W9bEnemigoCerca(kes.transform.position);
                bool pausaOk = false, reanudaOk = false; float pPausa0 = 0f, pPausa1 = 0f;
                if (en != null)
                {
                    var ag = en.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (en.Brain != null) { en.Brain.CancelOrder(); en.Brain.Quieto = true; }
                    en.Health.Invulnerable = true;        // los aliados no lo matan mientras se mide la pausa
                    if (ag != null) ag.enabled = false;   // quieto y sin agente: lo mantenemos a mano a 1,8 m de Kes, del lado opuesto al tanque
                    Action ponerlo = () => { var away = kes.transform.position - t.transform.position; away.y = 0f; if (away.sqrMagnitude < 0.01f) away = Vector3.right; var p = kes.transform.position + away.normalized * 1.8f; p.y = en.transform.position.y; en.transform.position = p; };
                    ponerlo();
                    yield return null; yield return null;
                    float tp0 = Time.realtimeSinceStartup; bool tomado = false;
                    while (Time.realtimeSinceStartup - tp0 < 3.0f)
                    {
                        ponerlo();
                        if (!tomado && Time.realtimeSinceStartup - tp0 >= 1.0f) { tomado = true; pPausa0 = r.Progreso01; }   // el primer segundo deja que el enemigo llegue y se cuente
                        yield return null;
                    }
                    pPausa1 = r.Progreso01;
                    pausaOk = r.Pausada && r.MotivoDePausa.Contains("BAJO FUEGO") && pPausa1 - pPausa0 < 0.01f;
                    // Se lo mata: la reparacion sigue.
                    en.Health.Invulnerable = false;
                    en.Health.TakeDamage(99999, kes.Id);
                    foreach (var x in Esperar(0.4f)) yield return x;
                    float pr0 = r.Progreso01;
                    // puede haber otro enemigo cerca: se despejan los que esten a menos de 4 m
                    for (int k = 0; k < 6; k++)
                    {
                        var otro = W9bEnemigoCerca(kes.transform.position);
                        if (otro != null && (otro.transform.position - kes.transform.position).magnitude < 4.5f) otro.Health.TakeDamage(99999, kes.Id); else break;
                    }
                    // WP11: la oleada puede traer a otro enemigo a menos de 4 m justo ahi (y entonces la pausa es lo correcto): se lo despeja y se espera
                    // hasta 8 s a que el avance se reanude, en vez de mirar una sola vez a los 1,5 s (fallaba ~1 de cada 3 por eso).
                    float tr0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - tr0 < 8f && !(r.Progreso01 > pr0 + 0.01f))
                    {
                        var otro2 = W9bEnemigoCerca(kes.transform.position);
                        if (otro2 != null && (otro2.transform.position - kes.transform.position).magnitude < 4.5f) otro2.Health.TakeDamage(99999, kes.Id);
                        yield return null;
                    }
                    reanudaOk = r.Progreso01 > pr0 + 0.01f;
                }
                W8Ok(ref ok, pausaOk, sb, $"con un enemigo a 4 m se pausa ('{r.MotivoDePausa}', avance {pPausa1 - pPausa0:0.000})");
                W8Ok(ref ok, reanudaOk, sb, "al matarlo la reparacion sigue");
                // Captura de la reparacion en curso (con y sin HUD).
                foreach (var x in W9bHasta(() => r.Progreso01 > 0.35f, 20f)) yield return x;
                var cp = kes.transform.position; var camPos = cp + new Vector3(-7f, 4.5f, -9f);
                CapturarDesde(camPos, Quaternion.LookRotation(cp + Vector3.up * 1.2f - camPos, Vector3.up), W9bPrefijo + "reparacion.png", 1600, 900, 55f);
                foreach (var x in CapturarPantalla(W9bPrefijo + "reparacion_hud.png")) yield return x;
                // Oleadas: 6 (norte, t=0) + 5 (este, 9 s) + 6 (oeste, 18 s) y una camioneta.
                foreach (var x in W9bHasta(() => d.RelojDeSubfase >= 19f, 40f)) yield return x;
                W8Ok(ref ok, d.OleadasDeReparacionLanzadas == 3 && d.EnemigosDeLasOleadas >= 16 && d.CamionetasDeReparacion >= 1, sb, $"oleadas={d.OleadasDeReparacionLanzadas} enemigos={d.EnemigosDeLasOleadas} camionetas={d.CamionetasDeReparacion}");
                // Hasta el final.
                foreach (var x in W9bHasta(() => r.Completa || d.Subfase == (int)SubfaseHuida.Abordaje, 90f)) yield return x;
                float total = Time.realtimeSinceStartup - inicio;
                float neto = total - r.SegundosPausados;
                // WP11: tope de 45 s netos (antes 31). El neto incluye la caminata de Kes hasta el tanque y lo que haga la oleada con ella (sin ser "bajo fuego"):
            // en 8 corridas dio 28 a 40 s y una de 50 s; el 31 fallaba ~1 de cada 3 sin que el avance de la reparacion tuviera defecto.
            W8Ok(ref ok, d.ReparacionCompletada && neto <= 45f, sb, $"completa en {total:0.0} s ({r.SegundosPausados:0.0} s en pausa; sin pausas {neto:0.0} s <= 45 con el camino de Kes)");
                W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Abordaje && t.Bando == TeamId.Player, sb, $"pasa a Abordaje, el tanque es del jugador ({t.Bando})");
                float v01 = t.Health.Current / (float)t.Health.MaxHealth;
                W8Ok(ref ok, v01 > 0.69f && v01 < 0.71f && Mathf.Abs(t.PisoDeVida01 - 0.2f) < 0.001f && t.AjusteDeDano == null, sb, $"vida {v01 * 100f:0}% piso {t.PisoDeVida01 * 100f:0}% sin filtro de jefe");
                // [E] SUBIR con el boton real: la escuadra sube y el jugador queda de artillero.
                d.Embarcar();
                foreach (var x in Esperar(1.5f)) yield return x;
                W8Ok(ref ok, d.EnTanque && d.Subfase == (int)SubfaseHuida.Carrera && t.Occupants.Count == 3, sb, $"Embarcar: en tanque, subfase {d.Subfase}, ocupantes {t.Occupants.Count}");
                W8Ok(ref ok, t.Driver != null && t.Driver.Role == RoleType.Flanker, sb, $"maneja {(t.Driver != null ? t.Driver.DisplayName : "-")}");
            }
            finally { ModoDios.Poner(false); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 097l / 097m / 073b la carrera y el final
        sealed class W9bCarrera
        {
            public float horaDeFinal = -1f, horaHalcon = -1f, minVida = 1e9f, altMin = 1e9f, altMax = 0f, relojFinalReal;
            public int maxIdx, barricadas, pasadas, avisos, cohetesHalcon, cohetesEmbo, tiradores0, camionetasCreadas, maxSimultaneas, hpCamioneta;
            public bool halconVisto;
        }

        static IEnumerable W9bCorrerCarrera(StringBuilder sb, W9bCarrera c, Func<OperacionDirector, float, IEnumerator> durante, float limiteReal, Action<bool> exito)
        {
            var d = OperacionDirector.Instancia; var t = d.tanque;
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1.0f)) yield return x;
            c.tiradores0 = 0; if (d.emboscadas != null) foreach (var e in d.emboscadas) if (e != null) c.tiradores0 += e.TiradoresTotales;
            d.Embarcar();
            float t0 = Time.realtimeSinceStartup; bool fotoHalcon = false;
            var idxField = typeof(OperacionDirector).GetField("indiceRuta", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            while (!d.FinalActiva && Time.realtimeSinceStartup - t0 < limiteReal && d.Fase == FaseOperacion.Huir)
            {
                c.minVida = Mathf.Min(c.minVida, t.Health.Current);
                c.maxIdx = Mathf.Max(c.maxIdx, (int)idxField.GetValue(d));
                c.maxSimultaneas = Mathf.Max(c.maxSimultaneas, d.AutosVivosAhora);
                var h = d.Halcon;
                if (h != null)
                {
                    if (c.horaHalcon < 0f) c.horaHalcon = d.Reloj;
                    c.halconVisto = true;
                    c.pasadas = Mathf.Max(c.pasadas, h.Pasadas); c.avisos = Mathf.Max(c.avisos, h.InstantesDeAviso.Count); c.cohetesHalcon = Mathf.Max(c.cohetesHalcon, h.CohetesSoltados);
                    if (h.Situacion == HelicopteroEnemigo.Estado.Orbitando) { c.altMin = Mathf.Min(c.altMin, h.Altura); c.altMax = Mathf.Max(c.altMax, h.Altura); }
                    if (!fotoHalcon && d.Reloj > 38f && h.Situacion == HelicopteroEnemigo.Estado.Orbitando)
                    {
                        fotoHalcon = true;
                        var fwd = t.transform.forward; fwd.y = 0f; fwd.Normalize();
                        var pos = t.transform.position - fwd * 16f + Vector3.up * 8f;
                        var foco = (h.transform.position + t.transform.position) * 0.5f;
                        CapturarDesde(pos, Quaternion.LookRotation(foco - pos, Vector3.up), W9bPrefijo + "halcon.png", 1600, 900, 62f);
                        foreach (var x in CapturarPantalla(W9bPrefijo + "halcon_hud.png")) yield return x;
                    }
                }
                if (durante != null) { var it = durante(d, d.Reloj); while (it.MoveNext()) yield return it.Current; }
                yield return null;
            }
            c.horaDeFinal = d.Reloj;
            c.cohetesEmbo = d.EmbosCohetesLanzados;
            c.camionetasCreadas = d.AutosCreados;
            exito(d.FinalActiva);
        }

        static void W9bContarBarricadas(W9bCarrera c)
        {
            c.barricadas = 0;
            foreach (var m in UnityEngine.Object.FindObjectsByType<SP.Presentation.ObstacleMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m != null && m.name.StartsWith("Barricada_", StringComparison.Ordinal) && (m.IsCollapsed || !m.gameObject.activeInHierarchy)) c.barricadas++;
        }

        // Observa la cinematica final hasta que termina; devuelve por referencia las mediciones y saca las capturas.
        static IEnumerable W9bObservarFinal(OperacionDirector d, bool disparoManual, StringBuilder sb, Action<bool> resultado)
        {
            bool ok = true;
            float t0 = Time.realtimeSinceStartup; bool f1 = false, f2 = false, f3 = false, disparado = false, vioOtro = false, vioFuego = false;
            float finSlow = -1f;
            while (!d.FinalTerminada && Time.realtimeSinceStartup - t0 < 20f)
            {
                float s = d.FinalSegundosLentos;
                var ci = CineDeHuida.Instancia;
                if (ci != null && ci.TextoDelCartel.Contains("OTRO")) vioOtro = true;
                if (ci != null && ci.TextoDelCartel.Contains("FUEGO")) vioFuego = true;
                if (disparoManual && !disparado && Time.timeScale < 0.5f && s >= 1.4f) { disparado = true; OperacionDirector.PruebaDisparar = true; }
                if (!f1 && Time.timeScale < 0.5f && s >= 0.5f && s < 1.8f) { f1 = true; foreach (var x in CapturarPantalla(W9bPrefijo + (disparoManual ? "final_otro_helicoptero.png" : "final_slowmo.png"))) yield return x; }
                if (!f2 && Time.timeScale < 0.5f && d.FinalDisparo && s >= 2.2f) { f2 = true; foreach (var x in CapturarPantalla(W9bPrefijo + (disparoManual ? "final_explosion_m.png" : "final_explosion.png"))) yield return x; }
                if (Time.timeScale >= 0.99f && d.tanque.IsDestroyed)
                {
                    if (finSlow < 0f) finSlow = Time.realtimeSinceStartup;
                    if (!f3 && Time.realtimeSinceStartup - finSlow > 1.3f) { f3 = true; foreach (var x in CapturarPantalla(W9bPrefijo + (disparoManual ? "todos_en_pie_m.png" : "todos_en_pie.png"))) yield return x; }
                }
                yield return null;
            }
            W8Ok(ref ok, d.FinalTerminada, sb, "la cinematica termina");
            W8Ok(ref ok, d.FinalSegundosLentos >= 3.7f && d.FinalSegundosLentos <= 4.3f && d.FinalEscalaMinima <= 0.26f && d.FinalEscalaMinima >= 0.24f, sb, $"camara lenta {d.FinalSegundosLentos:0.00} s reales a x{d.FinalEscalaMinima:0.00} (4 +- 0,3)");
            W8Ok(ref ok, vioFuego, sb, "cartel ¡FUEGO!");
            if (disparoManual) W8Ok(ref ok, d.FinalDisparo && !d.FinalDisparoAutomatico && d.FinalOtroHelicoptero && vioOtro, sb, $"disparo manual={!d.FinalDisparoAutomatico}, otro helicoptero={d.FinalOtroHelicoptero}, cartel OTRO={vioOtro}");
            else W8Ok(ref ok, d.FinalDisparo && d.FinalDisparoAutomatico, sb, $"sin tocar nada el obus sale solo (auto={d.FinalDisparoAutomatico})");
            W8Ok(ref ok, d.tanque.IsDestroyed && d.HalconesDerribados >= (disparoManual ? 2 : 1), sb, $"tanque destruido={d.tanque.IsDestroyed}, helicopteros derribados={d.HalconesDerribados}");
            W8Ok(ref ok, d.FinalEnPieAlTerminar == 3 && d.FinalDistanciaMaximaDeLaEscuadra <= 6f, sb, $"en pie={d.FinalEnPieAlTerminar}/3 a lo sumo a {d.FinalDistanciaMaximaDeLaEscuadra:0.0} m de los restos");
            W8Ok(ref ok, d.FinalSegundosTotales > 0f && d.FinalSegundosTotales <= 10f && d.Fase == FaseOperacion.Resistir, sb, $"Resistir tras {d.FinalSegundosTotales:0.0} s (<=10), fase={d.Fase}");
            int vivos = 0; foreach (var s in Escuadra3()) if (s.Health.IsAlive && s.gameObject.activeInHierarchy) vivos++;
            W8Ok(ref ok, vivos == 3 && Time.timeScale >= 0.99f && !CineDeHuida.Activa, sb, $"escuadra viva a pie={vivos}/3, tiempo normal y control devuelto");
            resultado(ok);
        }

        static List<Soldier> Escuadra3()
        {
            var l = new List<Soldier>();
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian) l.Add(s);
            return l;
        }

        static IEnumerator Bug097l()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var c = new W9bCarrera(); bool llego = false;
            foreach (var x in W9bCorrerCarrera(sb, c, null, 95f, v => llego = v)) yield return x;
            var d = OperacionDirector.Instancia;
            W9bContarBarricadas(c);
            W8Ok(ref ok, llego && c.horaDeFinal >= 67f && c.horaDeFinal <= 73f, sb, $"la carrera dura {c.horaDeFinal:0.0} s (70 +- 3)");
            W8Ok(ref ok, d.rutaDelTanque.Length == 18 && c.maxIdx >= 16, sb, $"ruta de {d.rutaDelTanque.Length} puntos, llego al {c.maxIdx + 1}");
            W8Ok(ref ok, c.minVida >= 320f, sb, $"el tanque nunca bajo del 20% (minimo {c.minVida:0} de 1600, piso 320)");
            W8Ok(ref ok, c.horaHalcon >= 24.5f && c.horaHalcon <= 27f && c.halconVisto, sb, $"Halcon entra a los {c.horaHalcon:0.0} s");
            W8Ok(ref ok, c.pasadas >= 3 && c.avisos >= 3 && c.cohetesHalcon >= 9, sb, $"pasadas={c.pasadas} avisos={c.avisos} cohetes={c.cohetesHalcon}");
            W8Ok(ref ok, c.altMin >= 17f && c.altMax <= 25f, sb, $"altura {c.altMin:0.0}..{c.altMax:0.0} m (18-24)");
            W8Ok(ref ok, c.cohetesEmbo >= 3 && c.tiradores0 == 6, sb, $"emboscadas: {c.tiradores0} tiradores, {c.cohetesEmbo} cohetes");
            W8Ok(ref ok, c.barricadas >= 2, sb, $"barricadas aplastadas={c.barricadas} (de {OperacionBuilderBarricadas()})");
            bool finOk = false;
            foreach (var x in W9bObservarFinal(d, false, sb, v => finOk = v)) yield return x;
            Fin((ok && finOk ? "OK " : "FALLO ") + sb);
        }

        static int OperacionBuilderBarricadas() => 4;

        static IEnumerator Bug097m()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var c = new W9bCarrera(); bool llego = false;
            bool derribado = false; float tDerribo = -1f;
            Func<OperacionDirector, float, IEnumerator> accion = (dir, reloj) => W9bDerribarHalcon(dir, reloj, v => { derribado |= v; if (v && tDerribo < 0f) tDerribo = reloj; });
            foreach (var x in W9bCorrerCarrera(sb, c, accion, 95f, v => llego = v)) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, derribado && d.HalconesDerribados >= 1, sb, $"primer Halcon derribado con explosiones a los {tDerribo:0.0} s");
            W8Ok(ref ok, llego && c.horaDeFinal >= 67f && c.horaDeFinal <= 73f && c.minVida >= 320f, sb, $"la carrera igual dura {c.horaDeFinal:0.0} s y el tanque no baja de {c.minVida:0}");
            bool finOk = false;
            foreach (var x in W9bObservarFinal(d, true, sb, v => finOk = v)) yield return x;
            W8Ok(ref ok, d.HalconesCreados >= 2, sb, $"helicopteros creados={d.HalconesCreados}");
            Fin((ok && finOk ? "OK " : "FALLO ") + sb);
        }

        static bool derribandoHalcon;
        static IEnumerator W9bDerribarHalcon(OperacionDirector d, float reloj, Action<bool> hecho)
        {
            if (reloj < 33f || derribandoHalcon) yield break;
            var h = d.Halcon;
            if (h == null || h.Derribado) yield break;
            derribandoHalcon = true;
            var yo = W9Yo();
            int n = 0;
            // Obuses al helicoptero (el mismo camino que el canon: explosion con dano a vehiculos) hasta que caiga.
            while (h != null && !h.Derribado && n < 25)
            {
                Projectile.ExplodeAt(h.transform.position, 5.5f, OperacionDirector.DanoDelCanon, yo != null ? yo.Id : -1, TeamId.Player, null, 1f);
                n++;
                for (int k = 0; k < 4; k++) yield return null;
            }
            hecho(h != null && h.Derribado);
            derribandoHalcon = false;
        }

        // ---------------------------------------------------------------- 097n compatibilidad con Embarcar() directo
        static IEnumerator Bug097n()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            Poseer(Escuadrista(RoleType.Assault));   // #110: con Kes poseida maneja Doc; este check mide el caso base (maneja Kes), asi que se pasa a Vega
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia; var t = d.tanque;
            d.Embarcar();
            foreach (var x in Esperar(1.5f)) yield return x;
            var motor = t.GetComponent<VehicleMotor>();
            W8Ok(ref ok, d.EnTanque && d.Subfase == (int)SubfaseHuida.Carrera, sb, $"EnTanque={d.EnTanque} subfase={d.Subfase}");
            float v01 = t.Health.Current / (float)t.Health.MaxHealth;
            W8Ok(ref ok, t.Bando == TeamId.Player && v01 > 0.6f && v01 <= 0.71f && Mathf.Abs(t.PisoDeVida01 - 0.2f) < 0.001f, sb, $"tanque del jugador ({t.Bando}) con {v01 * 100f:0}% y piso {t.PisoDeVida01 * 100f:0}%");
            int enemigosDentro = 0; foreach (var o in t.Occupants) if (o.Team == TeamId.Enemy) enemigosDentro++;
            W8Ok(ref ok, enemigosDentro == 0 && t.Occupants.Count == 3 && t.Driver != null && t.Driver.Role == RoleType.Flanker, sb, $"ocupantes={t.Occupants.Count} enemigos adentro={enemigosDentro} conductor={(t.Driver != null ? t.Driver.DisplayName : "-")}");
            W8Ok(ref ok, motor != null && motor.MaxSpeed >= 12f && motor.MaxSpeed <= 16.5f, sb, $"velocidad de ruta {(motor != null ? motor.MaxSpeed : 0f):0.0} m/s");
            W8Ok(ref ok, SupervivientesNoForzados(), sb, "SupervivientesForzados sigue sin forzarse hasta la extraccion");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static bool SupervivientesNoForzados() => SP.Mision.EstadisticasDeMision.SupervivientesForzados < 0;

        // ---------------------------------------------------------------- 073b conteos de la carrera
        static IEnumerator Bug073b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.Embarcar();
            int maxVivas = 0; int hp = 0;
            float t0 = Time.realtimeSinceStartup;
            while (d.Reloj < 68f && Time.realtimeSinceStartup - t0 < 90f)
            {
                maxVivas = Mathf.Max(maxVivas, d.AutosVivosAhora);
                foreach (var v in Vehicle.Todos) if (v != null && v.GetComponent<OperacionAuto>() != null && !v.IsDestroyed) hp = Mathf.Max(hp, v.Health.MaxHealth);
                yield return null;
            }
            int tiradores = 0; if (d.emboscadas != null) foreach (var e in d.emboscadas) if (e != null) tiradores += e.TiradoresTotales;
            int halcones = d.HalconesCreados;
            int total = d.AutosCreados + tiradores + halcones;
            W8Ok(ref ok, maxVivas >= 4 && d.AutosSimultaneosEfectivos >= 4, sb, $"camionetas a la vez: maximo {maxVivas} (efectivo {d.AutosSimultaneosEfectivos})");
            W8Ok(ref ok, hp == OperacionBuilderVidaCamioneta(), sb, $"vida de camioneta {hp} (260)");
            W8Ok(ref ok, total >= 14, sb, $"enemigos en la carrera (68 s): {d.AutosCreados} camionetas + {tiradores} tiradores + {halcones} helicoptero = {total} (>=14)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static int OperacionBuilderVidaCamioneta() => OperacionBuilder.VidaDeCamioneta;
    }
}
