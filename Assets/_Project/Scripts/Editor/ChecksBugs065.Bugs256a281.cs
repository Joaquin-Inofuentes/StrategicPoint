using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.EditorTools;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // Tanda de bugs #256-#281 (pedidos de juego del 2026-10-19). Cada check pide un Play FRESCO sobre SC_Operacion:
    //   ChecksBugs065.CorrerLista("Bug276a,Bug258a", true)
    //   258a los contenedores no se vuelven transparentes con el soldado detras     276a modo dios: velocidad x2 y dano x3
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 272a torretas aliadas y helicopteros de asalto en el hackeo
        static IEnumerator Bug272a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia;
            var def = d != null ? d.DefensaDelCentroActual : null;
            if (def == null) { Fin("FALLO no hay DefensaDelCentro al entrar a la fase"); yield break; }
            W8Ok(ref ok, def.Torretas.Count == DefensaDelCentro.TorretasAliadas, sb, $"hay {def.Torretas.Count} torretas aliadas alrededor de la computadora");
            def.Comenzar();
            foreach (var x in Esperar(14f)) yield return x;
            W8Ok(ref ok, def.HelicopterosLanzados >= 1, sb, $"entro al menos un helicoptero de asalto ({def.HelicopterosLanzados})");
            int disparos = 0; foreach (var t in def.Torretas) if (t != null) disparos += t.Disparos;
            W8Ok(ref ok, disparos > 0, sb, $"las torretas dispararon solas ({disparos} balas)");
            HelicopteroEnemigo heli = null; foreach (var h in def.Helicopteros) if (h != null) { heli = h; break; }
            if (heli == null) { Fin("FALLO " + sb + " sin helicoptero"); yield break; }
            W8Ok(ref ok, heli.Vehiculo.Health.MaxHealth == HelicopteroEnemigo.VidaDeAsalto, sb, $"vida de asalto {heli.Vehiculo.Health.MaxHealth}");
            W8Ok(ref ok, heli.Altura > 10f, sb, $"vuela sobre la computadora (altura {heli.Altura:0.0})");
            heli.Vehiculo.TakeDamage(HelicopteroEnemigo.VidaDeAsalto + 10, -4);
            foreach (var x in Esperar(1.5f)) yield return x;
            W8Ok(ref ok, heli.Derribado && def.HelicopterosDerribados >= 1, sb, "se lo derriba a tiros y cuenta como derribado");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 282a el modo dios (x3) no destruye al tanque jefe: el piso de vida se respeta
        static IEnumerator Bug282a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            ModoDios.VelocidadActiva = false;
            ModoDios.Poner(true);
            OperacionPrueba.Arrancar(4, 1, 1);   // subfase Jefe: el Tanque_Fuga lleva el piso de vida del jefe
            foreach (var x in Esperar(2f)) yield return x;
            var yo = PlayerInputDriver.Activo.Brain.Current;
            SP.Vehicles.Vehicle tq = null;
            foreach (var v in Object.FindObjectsByType<SP.Vehicles.Vehicle>()) if (v != null && v.name == "Tanque_Fuga") { tq = v; break; }
            if (tq == null) { Fin("FALLO no hay Tanque_Fuga"); yield break; }
            int piso = tq.PisoDeVida;
            W8Ok(ref ok, piso > 0, sb, $"el tanque lleva piso de vida ({piso} de {tq.Health.MaxHealth})");
            for (int i = 0; i < 60 && !tq.IsDestroyed; i++) tq.TakeDamage(300, yo.Id);
            W8Ok(ref ok, !tq.IsDestroyed && tq.Health.IsAlive, sb, $"con modo dios (x3) tras 60 golpes de 300 sigue vivo ({tq.Health.Current}/{tq.Health.MaxHealth})");
            W8Ok(ref ok, tq.Health.Current >= piso, sb, $"no baja del piso ({tq.Health.Current} >= {piso})");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 285a rebote de zoom del francotirador + torso del cuchillo
        static IEnumerator Bug285a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1, 0);
            foreach (var x in Esperar(2f)) yield return x;
            var rig = PlayerInputDriver.Activo.Rig;
            W8Ok(ref ok, rig.FactorDeReboteDeZoom() == 1f, sb, "sin disparo el factor es 1");
            float fov0 = Camera.main.fieldOfView; rig.RebotarZoom(); sb.Append($"[zoom={rig.EstaConZoom}] ");
            float min = 9f, max = 0f, acum = 0f; int frames = 0; float t0 = Time.unscaledTime;
            while (rig.ReboteDeZoomActivo && frames < 400)
            {
                float f = Camera.main.fieldOfView / fov0;   // solo lo mueve la camara (llamar al factor desde aca lo adelantaria el doble)
                if (f < min) min = f; if (f > max) max = f; frames++; acum += Time.unscaledDeltaTime;
                yield return null;
            }
            float dur = Time.unscaledTime - t0;
            W8Ok(ref ok, min < 0.9f, sb, $"acerca el zoom (FOV x{min:0.00})");
            W8Ok(ref ok, max > 1.02f, sb, $"rebota hacia atras pasando el zoom normal (FOV x{max:0.00})");
            W8Ok(ref ok, acum > 0.35f && acum < 0.75f, sb, $"dura medio segundo ({acum:0.00} s de dt, {dur:0.00} s de reloj, {frames} cuadros)");
            W8Ok(ref ok, rig.FactorDeReboteDeZoom() == 1f, sb, "termina exacto en el zoom normal");
            W8Ok(ref ok, SoldierMeleeAnim.EscalaDelTorsoDelJugador < 0.5f, sb, $"el torso del jugador se mueve poco con el cuchillo ({SoldierMeleeAnim.EscalaDelTorsoDelJugador})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 284a la extraccion recibe un flujo secuencial de enemigos
        static IEnumerator Bug284a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            ModoDios.VelocidadActiva = false;
            ModoDios.Poner(true);
            OperacionPrueba.Arrancar(6, 1, 0);
            foreach (var x in Esperar(3f)) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, d.Fase == FaseOperacion.Extraer, sb, $"estamos en Extraer ({d.Fase})");
            foreach (var x in Esperar(20f)) yield return x;
            W8Ok(ref ok, d.TandasDeExtraccion >= 5, sb, $"llegaron {d.TandasDeExtraccion} tandas en 20 s (una cada {OperacionDirector.SegundosEntreTandasDeExtraccion} s)");
            W8Ok(ref ok, d.EnemigosDeExtraccionCreados >= 12, sb, $"{d.EnemigosDeExtraccionCreados} enemigos creados, {d.EnemigosDeExtraccionVivos} vivos (tope {OperacionDirector.TopeDeVivosEnExtraccion})");
            W8Ok(ref ok, d.EnemigosDeExtraccionVivos <= OperacionDirector.TopeDeVivosEnExtraccion, sb, "no pasa el tope de vivos");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 283a los aliados no se traban al seguir al jugador que camina
        static IEnumerator Bug283a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            ModoDios.VelocidadActiva = false;
            ModoDios.Poner(true);
            OperacionPrueba.Arrancar(4, 1, 0);
            foreach (var x in Esperar(2f)) yield return x;
            var d = PlayerInputDriver.Activo; var yo = d.Brain.Current;
            var ag = yo.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var aliados = new List<Soldier>();
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s != yo && s.Health.IsAlive && s.Role != RoleType.Civilian) aliados.Add(s);
            var hist = new Dictionary<Soldier, List<Vector3>>(); foreach (var a in aliados) hist[a] = new List<Vector3>();
            float t = 0f, atrapadoMax = 0f, distMax = 0f; string peor = "";
            var atrapadoDesde = new Dictionary<Soldier, float>();
            var rumbo = new Vector3(0f, 0f, 1f);
            while (t < 40f)
            {
                // El jugador camina a ~5 m/s hacia el norte (si el camino esta libre) y gira 90 grados cada 8 s para recorrer el patio.
                if (((int)(t / 8f)) % 2 == 1) rumbo = new Vector3(1f, 0f, 0f); else rumbo = new Vector3(0f, 0f, 1f);
                if (ag != null && ag.enabled) ag.Move(rumbo * 5f * Time.deltaTime); else yo.transform.position += rumbo * 5f * Time.deltaTime;
                t += Time.deltaTime;
                if (Time.frameCount % 15 == 0)
                    foreach (var a in aliados)
                    {
                        var l = hist[a]; l.Add(a.transform.position); if (l.Count > 12) l.RemoveAt(0);
                        float dist = Vector3.Distance(a.transform.position, yo.transform.position);
                        if (dist > distMax) { distMax = dist; peor = a.DisplayName; }
                        bool quieto = l.Count >= 12 && Vector3.Distance(l[0], l[l.Count - 1]) < 0.8f;
                        if (quieto && dist > 8f && a.Brain.State == SP.Ai.AiState.Follow)
                        {
                            if (!atrapadoDesde.ContainsKey(a)) atrapadoDesde[a] = t;
                            atrapadoMax = Mathf.Max(atrapadoMax, t - atrapadoDesde[a]);
                        }
                        else atrapadoDesde.Remove(a);
                    }
                yield return null;
            }
            W8Ok(ref ok, atrapadoMax < 4f, sb, $"ningun aliado queda parado y lejos mas de 4 s (peor {atrapadoMax:0.0} s)");
            W8Ok(ref ok, distMax < 70f, sb, $"el aliado mas lejano llego a {distMax:0} m ({peor}); corre para alcanzar al lider");
            foreach (var a in aliados) { var na = a.GetComponent<UnityEngine.AI.NavMeshAgent>(); string nav = na != null ? (na.pathStatus + " vel=" + na.velocity.magnitude.ToString("0.0") + " spd=" + na.speed.ToString("0.0") + " enNav=" + na.isOnNavMesh) : "-"; sb.Append($"[{a.DisplayName} a {Vector3.Distance(a.transform.position, yo.transform.position):0.0} m, {a.Brain.State}, regresos={a.Brain.RegresosForzados}, nav={nav}] "); }
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 277a el canon baja lo que baja la camara y vertical + horizontal se suman
        static IEnumerator Bug277a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            ModoDios.Poner(true);
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(2f)) yield return x;
            var d = PlayerInputDriver.Activo;
            SP.Vehicles.Vehicle tq = null;
            foreach (var v in Object.FindObjectsByType<SP.Vehicles.Vehicle>()) if (v != null && v.name == "Tanque_Fuga") { tq = v; break; }
            if (d == null || tq == null) { Fin("FALLO no hay jugador o tanque"); yield break; }
            var yo = d.Brain.Current;
            var ag = yo.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var pos = tq.transform.position + tq.transform.right * 3f;
            if (ag != null && ag.enabled) ag.Warp(pos);
            yo.transform.position = pos;
            d.EnterVehicle(tq);
            foreach (var x in Esperar(3.5f)) yield return x;   // la animacion de subida bloquea el cambio de asiento
            d.SwitchSeat(SP.Vehicles.VehicleSeatRole.Gunner);
            foreach (var x in Esperar(0.5f)) yield return x;
            SP.Vehicles.TurretWeapon canon = null;
            foreach (var t in tq.GetComponentsInChildren<SP.Vehicles.TurretWeapon>()) { var ai = t.GetComponent<SP.Vehicles.TurretAI>(); if (ai != null && ai.Asiento == SP.Vehicles.VehicleSeatRole.Gunner) { canon = t; break; } }
            if (canon == null) { Fin("FALLO no hay canon"); yield break; }
            W8Ok(ref ok, d.CurrentSeat == SP.Vehicles.VehicleSeatRole.Gunner, sb, $"el jugador va de artillero ({d.CurrentSeat})");
            W8Ok(ref ok, canon.MaxPitch >= 38f, sb, $"el tubo baja hasta {canon.MaxPitch:0} grados (la camara baja hasta {PlayerInputDriver.InclinacionMaxArtillero:0}; antes 10/15)");
            var campo = typeof(PlayerInputDriver).GetField("inclinacionArtillero", BindingFlags.NonPublic | BindingFlags.Instance);
            campo.SetValue(d, 35f);   // mira casi al pie del tanque
            float yaw0 = canon.YawMundo;
            canon.AddDesiredYaw(30f);
            foreach (var x in Esperar(2.5f)) yield return x;
            W8Ok(ref ok, canon.PitchActual > 16f, sb, $"mirando al suelo cercano el tubo baja a {canon.PitchActual:0.0} grados (mas que el viejo limite de 15)");
            float dy = Mathf.Abs(Mathf.DeltaAngle(yaw0, canon.YawMundo));
            W8Ok(ref ok, dy > 20f, sb, $"el giro horizontal se suma al vertical a la vez (yaw +{dy:0} grados con el tubo a {canon.PitchActual:0.0})");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 276a modo dios: velocidad doble y dano triple
        static IEnumerator Bug276a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1f)) yield return x;
            var yo = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Brain.Current : null;
            Soldier enemigo = null;
            foreach (var s in ActorRegistry.All) { if (s != null && s.Team == TeamId.Enemy && s.Health.IsAlive) { enemigo = s; break; } }
            if (yo == null || enemigo == null) { Fin("FALLO no hay soldado propio o enemigo"); yield break; }

            ModoDios.VelocidadActiva = true;
            ModoDios.Poner(true);
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, Mathf.Abs(Time.timeScale - ModoDios.EscalaDeTiempo) < 0.01f, sb, $"con modo dios la escena corre a x{Time.timeScale:0.0}");
            int antes = enemigo.Health.Current;
            enemigo.Health.TakeDamage(10, yo.Id);
            int conDios = antes - enemigo.Health.Current;
            ModoDios.Poner(false);
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, Mathf.Abs(Time.timeScale - 1f) < 0.01f, sb, $"al apagarlo vuelve a x{Time.timeScale:0.0}");
            antes = enemigo.Health.Current;
            enemigo.Health.TakeDamage(10, yo.Id);
            int sinDios = antes - enemigo.Health.Current;
            W8Ok(ref ok, sinDios > 0 && conDios == sinDios * 3, sb, $"el dano del jugador es x3 con modo dios ({conDios} contra {sinDios})");
            ModoDios.VelocidadActiva = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 258a los contenedores no se desvanecen
        static IEnumerator Bug258a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, 3);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null ? drv.Brain.Current : null;
            if (yo == null) { Fin("FALLO no hay soldado propio"); yield break; }
            var puntos = new[] { new Vector4(35.7f, -306.9f, 36.7f, 0f), new Vector4(-37.1f, -226.1f, 358.7f, 0f) };
            foreach (var p in puntos)
            {
                var pos = new Vector3(p.x, 0.8f, p.y);
                var ag = yo.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag != null && ag.enabled) ag.Warp(pos);
                yo.transform.position = pos;
                yo.transform.rotation = Quaternion.Euler(0f, p.z, 0f);
                drv.Rig.ResetPitch();
                foreach (var x in Esperar(1.2f)) yield return x;
                var t = TransparenciaDeOclusores.Instancia;
                int transparentes = 0; string cuales = "";
                if (t != null)
                {
                    foreach (var r in Object.FindObjectsByType<MeshRenderer>())
                    {
                        if (r == null || !t.EstaTransparente(r)) continue;
                        transparentes++; cuales += r.name + " ";
                    }
                }
                W8Ok(ref ok, transparentes == 0, sb, $"en ({p.x:0},{p.y:0}) no se desvanece ningun muro ({transparentes}: {cuales})");
            }
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
