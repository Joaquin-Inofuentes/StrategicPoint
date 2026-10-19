using System.Collections;
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
