using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // Tanda de bugs #231-#255 (pedidos de juego del 2026-10-15). Cada check pide un Play FRESCO sobre SC_Operacion:
    //   ChecksBugs065.CorrerUno("Bug231a")  (o runchk.sh Bug231a)
    //   231a revivido no enterrado    233a camara no atraviesa un muro   234a la bala no nace dentro de una cobertura baja
    //   235a el enemigo oye y va      240a dificultad adaptativa         243a toma del jefe al caer el muro
    //   244a fusil x2 cadencia, x3 cargador                              248a sin Halcon en la carrera
    //   250a Resistir en RTS, ambos equipos a sus puntos, minimapa       253a caidos aliados piden ayuda
    //   254a camara de muerte mira al asesino 3 s                         255a cuenta de caidos al extraer
    public static partial class ChecksBugs065
    {
        static Soldier B231Aliado(string nombre)
        {
            foreach (var s in ActorRegistry.All) if (s != null && s.DisplayName == nombre) return s;
            return null;
        }

        static float B231BajoElPie(Soldier s)
        {
            var col = s.GetComponent<Collider>();
            bool estaba = col.enabled; if (!estaba) col.enabled = true;
            Physics.SyncTransforms();
            float y = col.bounds.min.y;
            if (!estaba) col.enabled = false;
            return y;
        }

        // ---------------------------------------------------------------- 231a revivido sin enterrar (y el caido tampoco)
        static IEnumerator Bug231a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var doc = B231Aliado("Soldado_3_Doc");
            if (doc == null) { Fin("FALLO no hay Doc"); yield break; }
            float yVivo = doc.transform.position.y;
            ComandosDeDepuracion.Matar(doc.Id);
            foreach (var x in Esperar(1.6f)) yield return x;   // el apoyo de cadaveres corre cada 0,5 s
            W8Ok(ref ok, !doc.Health.IsAlive, sb, "Doc murio");
            W8Ok(ref ok, Mathf.Abs(doc.transform.position.y - yVivo) <= 0.06f, sb, $"el cadaver se queda a la altura del pivote ({doc.transform.position.y:0.00} vs {yVivo:0.00} vivo), antes bajaba a 0,00");
            var smr = doc.GetComponentInChildren<SkinnedMeshRenderer>();
            W8Ok(ref ok, smr != null && smr.bounds.min.y >= -0.05f, sb, $"la malla del caido no esta bajo el piso (min y={(smr != null ? smr.bounds.min.y : 0f):0.00})");
            W8Ok(ref ok, Reanimacion.Ejecutar(doc, 0.5f), sb, "se lo revive");
            foreach (var x in Esperar(0.6f)) yield return x;
            float pie = B231BajoElPie(doc);
            W8Ok(ref ok, doc.Health.IsAlive && Mathf.Abs(pie) <= 0.06f, sb, $"revivido con la base del collider en el piso (y={pie:0.000})");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 233a la camara no atraviesa un muro
        static IEnumerator Bug233a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var yo = W9Yo(); var drv = PlayerInputDriver.Activo; var cam = CamaraPrincipal.Actual;
            var muro = GameObject.CreatePrimitive(PrimitiveType.Cube); muro.name = "B233_Muro";
            var back = -yo.transform.forward; back.y = 0f; back.Normalize();
            muro.transform.position = yo.transform.position + back * 1.1f + Vector3.up * 1.5f;
            muro.transform.rotation = Quaternion.LookRotation(back, Vector3.up);
            muro.transform.localScale = new Vector3(14f, 6f, 0.4f);
            Physics.SyncTransforms();
            foreach (var x in Esperar(1.0f)) yield return x;
            float dCam = Vector3.Dot(cam.transform.position - yo.transform.position, back);   // cuanto atras del soldado esta la camara
            float dMuro = 1.1f - 0.2f;                                                       // la cara del muro mira al soldado a 0,9 m
            W8Ok(ref ok, dCam < dMuro + 0.05f, sb, $"con un muro a 0,9 m detras, la camara queda a {dCam:0.00} m (no pasa de {dMuro:0.00})");
            // El soldado sigue a la vista: el rayo camara -> soldado no choca con nada solido.
            var o = cam.transform.position; var dest = yo.transform.position + Vector3.up * 1.2f;
            bool tapado = Physics.Raycast(o, (dest - o).normalized, out var h, Vector3.Distance(o, dest) - 0.3f, ~0, QueryTriggerInteraction.Ignore) && h.collider != null && h.collider.GetComponentInParent<Soldier>() == null;
            W8Ok(ref ok, !tapado, sb, "nada tapa la linea camara -> soldado");
            UnityEngine.Object.Destroy(muro);
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 234a la bala sale de la camara si la boca queda tapada
        static IEnumerator Bug234a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var yo = W9Yo(); var drv = PlayerInputDriver.Activo;
            var fwd = yo.transform.forward; fwd.y = 0f; fwd.Normalize();
            // Cobertura baja (1,05 m) pegada a la boca del arma: la camara ve por encima, la boca no.
            var muro = GameObject.CreatePrimitive(PrimitiveType.Cube); muro.name = "B234_Saco";
            var boca = yo.Weapon.Muzzle != null ? yo.Weapon.Muzzle.position : yo.transform.position;
            muro.transform.position = new Vector3(boca.x, 0.525f, boca.z) + fwd * 0.55f;
            muro.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            muro.transform.localScale = new Vector3(4f, 1.05f, 0.4f);
            Physics.SyncTransforms();
            var cam = CamaraPrincipal.Actual;
            var objetivo = cam.transform.position + cam.transform.forward * 35f;
            int antes = 0; foreach (var p in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude)) antes++;
            bool disparo = drv.Brain.Fire(objetivo);
            yield return null;
            Projectile nuevo = null; float mejor = 1e9f;
            foreach (var p in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsInactive.Exclude))
            { float d = (p.transform.position - cam.transform.position).sqrMagnitude; if (d < mejor) { mejor = d; nuevo = p; } }
            W8Ok(ref ok, disparo && nuevo != null, sb, $"el disparo salio (proyectil visible={(nuevo != null)})");
            if (nuevo != null)
            {
                float yBala = nuevo.transform.position.y;
                W8Ok(ref ok, yBala >= 1.05f - 0.02f, sb, $"la bala nace por encima de la cobertura de 1,05 m (y={yBala:0.00}), no dentro");
            }
            UnityEngine.Object.Destroy(muro);
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 235a el enemigo oye el disparo y va hacia el
        static IEnumerator Bug235a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var yo = W9Yo();
            Soldier e = null; AiState antes = AiState.Idle;
            var candidatos = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Enemy && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Brain != null && s.Brain.State == AiState.Patrol && Vector3.Distance(s.transform.position, yo.transform.position) > 55f) candidatos.Add(s);
            foreach (var c in candidatos)
            {
                var dir = c.transform.position - yo.transform.position; dir.y = 0f; dir.Normalize();
                var p0 = yo.transform.position + dir * 40f;
                c.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(p0.x, 0.8f, p0.z));
                c.transform.position = new Vector3(p0.x, 0.8f, p0.z);
                foreach (var x in Esperar(0.25f)) yield return x;
                if (c.Brain.State == AiState.Patrol) { e = c; antes = c.Brain.State; break; }
            }
            if (e == null) { Fin("FALLO no hay un enemigo que siga en patrulla a 40 m"); yield break; }
            EventBus.Instance.Publish(new ShotFiredEvent(yo.Id));
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, antes == AiState.Patrol && (e.Brain.State == AiState.Chase || e.Brain.State == AiState.Attack), sb, $"enemigo a 40 m (mas alla de los 30 m de antes) pasa de {antes} a {e.Brain.State} al oir el tiro");
            W8Ok(ref ok, AiBrain.RadioDeOidoEnemigo > 30f, sb, $"radio de oido enemigo {AiBrain.RadioDeOidoEnemigo} m");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 240a dificultad adaptativa
        static IEnumerator Bug240a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            DificultadAdaptativa.Resetear(); DificultadAdaptativa.Forzado = null;
            W8Ok(ref ok, Dificultad.Activa, sb, "la dificultad adaptativa rige en la partida principal");
            // escuadra sana, sin dano reciente: el reto sube
            float t = Time.time + 100f;
            for (int i = 0; i < 12; i++) { t += 4f; DificultadAdaptativa.Evaluar(t); }
            float alto = DificultadAdaptativa.Factor;
            W8Ok(ref ok, alto > 1.15f, sb, $"escuadra sana y sin dano: el dano enemigo sube a x{alto:0.00}");
            // hay caidos y poca vida: el reto baja
            foreach (var n in new[] { "Soldado_1_Vega", "Soldado_3_Doc" }) { var s = B231Aliado(n); if (s != null) { ModoDios.Poner(false); ComandosDeDepuracion.Matar(s.Id); } }
            foreach (var x in Esperar(0.5f)) yield return x;
            for (int i = 0; i < 20; i++) { t += 4f; DificultadAdaptativa.Evaluar(t); }
            float bajo = DificultadAdaptativa.Factor;
            W8Ok(ref ok, bajo < alto - 0.3f && bajo >= DificultadAdaptativa.Minimo - 0.001f, sb, $"con 2 caidos el dano baja a x{bajo:0.00} (minimo {DificultadAdaptativa.Minimo})");
            // los enemigos no pegan todos igual
            float mn = 9f, mx = 0f; for (int id = 1; id <= 60; id++) { float r = DificultadAdaptativa.RasgoDelEnemigo(id); mn = Mathf.Min(mn, r); mx = Mathf.Max(mx, r); }
            W8Ok(ref ok, mx - mn > 0.15f && mn >= 0.84f && mx <= 1.16f, sb, $"rasgos por enemigo entre x{mn:0.00} y x{mx:0.00}");
            DificultadAdaptativa.Resetear();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 243a la toma del jefe arranca apenas cae el muro
        static IEnumerator Bug243a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            W8Ok(ref ok, d.Subfase == (int)SubfaseHuida.Acercamiento && yo.transform.position.z <= OperacionDirector.ZParaLaToma, sb, $"antes: subfase={d.Subfase}, z={yo.transform.position.z:0.0} (<= {OperacionDirector.ZParaLaToma})");
            ObstacleMarker muro = null;
            foreach (var o in UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include)) if (o.name == "Muro_Salida_Norte") { muro = o; break; }
            if (muro == null) { Fin("FALLO no hay Muro_Salida_Norte"); yield break; }
            muro.Demoler(null, 9f);
            float t0 = Time.realtimeSinceStartup; bool toma = false;
            while (Time.realtimeSinceStartup - t0 < 3f && !toma) { toma = d.TomaDelJefeActiva || d.Subfase != (int)SubfaseHuida.Acercamiento; yield return null; }
            W8Ok(ref ok, toma, sb, $"la presentacion del jefe arranca al caer el muro ({Time.realtimeSinceStartup - t0:0.0} s) sin pasar z={OperacionDirector.ZParaLaToma}");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 244a fusil de asalto: el doble de cadencia, el triple de cargador
        static IEnumerator Bug244a()
        {
            var sb = new StringBuilder(); bool ok = true;
            var e = WeaponCatalog.Get(WeaponKind.Rifle);
            W8Ok(ref ok, Mathf.Abs(e.Cooldown - 0.15f) < 0.001f, sb, $"cadencia: {e.Cooldown:0.00} s entre tiros (antes 0,30 = el doble)");
            W8Ok(ref ok, e.MagazineSize == 24, sb, $"cargador de {e.MagazineSize} (antes 8 = el triple)");
            Fin((ok ? "OK " : "FALLO ") + sb);
            yield break;
        }

        // ---------------------------------------------------------------- 248a el Halcon no entra a la carrera
        static IEnumerator Bug248a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionDirector.HalconEnLaCarrera = false;
            var d = OperacionDirector.Instancia;
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1.0f)) yield return x;
            d.Embarcar();
            float t0 = Time.realtimeSinceStartup; bool visto = false; float reloj = 0f;
            while (Time.realtimeSinceStartup - t0 < 42f && d.Fase == FaseOperacion.Huir && !d.FinalActiva)
            {
                if (d.Halcon != null && d.Halcon.gameObject.activeInHierarchy) visto = true;
                reloj = d.Reloj;
                yield return null;
            }
            W8Ok(ref ok, reloj >= 30f, sb, $"la carrera corrio {reloj:0.0} s (el Halcon entraba a los ~25 s)");
            W8Ok(ref ok, !visto && d.HalconesCreados == 0, sb, $"sin Halcon en la carrera (visto={visto}, creados={d.HalconesCreados})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 250a Resistir arranca en RTS, los dos equipos van a los dos puntos, el minimapa sigue
        static IEnumerator Bug250a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5, 1);
            foreach (var x in Esperar(0.8f)) yield return x;
            var d = OperacionDirector.Instancia; var rig = PlayerInputDriver.Activo.Rig;
            W8Ok(ref ok, d.Fase == FaseOperacion.Resistir && rig.Mode == ControlMode.Rts, sb, $"Resistir arranca en RTS (modo={rig.Mode})");
            var sec = (SectorDeDefensa[])typeof(OperacionDirector).GetField("sectoresDeDefensa", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(d);
            var foco = rig.RtsFoco; var medio = (sec[0].Centro + sec[1].Centro) * 0.5f;
            W8Ok(ref ok, new Vector2(foco.x - medio.x, foco.z - medio.z).magnitude < 30f, sb, $"la camara tactica mira entre los dos sectores (foco {foco}, medio {medio})");
            // en hasta 30 s reales todos (escuadra y milicianos) estan a <= 12 m de uno de los dos puntos, y los dos puntos tienen gente
            int enA = 0, enB = 0, total = 0; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 30f)
            {
                enA = enB = total = 0;
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue;
                    total++;
                    var p = s.transform.position; p.y = 0f;
                    if (Vector3.Distance(p, new Vector3(sec[0].Centro.x, 0f, sec[0].Centro.z)) <= 12f) enA++;
                    else if (Vector3.Distance(p, new Vector3(sec[1].Centro.x, 0f, sec[1].Centro.z)) <= 12f) enB++;
                }
                if (enA + enB == total && enA > 0 && enB > 0) break;
                yield return null;
            }
            W8Ok(ref ok, total >= 6 && enA > 0 && enB > 0 && enA + enB == total, sb, $"los dos equipos llegaron a sus puntos: A={enA} B={enB} de {total}");
            // el minimapa se centra en lo que mira la camara tactica y se actualiza al panear
            var mm = SP.UI.MinimapFollow.Activo;
            yield return null;
            W8Ok(ref ok, mm != null && new Vector2(mm.transform.position.x - rig.RtsFoco.x, mm.transform.position.z - rig.RtsFoco.z).magnitude < 0.5f, sb, "el minimapa esta centrado en el foco de la camara RTS");
            rig.CentrarRtsEn(rig.RtsFoco + new Vector3(-40f, 0f, 30f), 0f);
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, mm != null && new Vector2(mm.transform.position.x - rig.RtsFoco.x, mm.transform.position.z - rig.RtsFoco.z).magnitude < 0.5f, sb, $"al panear 50 m el minimapa lo sigue (foco {rig.RtsFoco})");
            var rec = GameObject.Find("RecuadroRts");
            W8Ok(ref ok, rec != null && rec.activeInHierarchy, sb, "se dibuja el recuadro de la region visible sobre el minimapa");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 253a los aliados caidos se ven y piden ayuda
        static IEnumerator Bug253a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var kes = B231Aliado("Soldado_3_Doc");
            Soldier enemigo = null; foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health.IsAlive && s.gameObject.activeInHierarchy) { enemigo = s; break; }
            ComandosDeDepuracion.Matar(kes.Id);
            if (enemigo != null) ComandosDeDepuracion.Matar(enemigo.Id);
            foreach (var x in Esperar(0.8f)) yield return x;
            var ayuda = kes.GetComponent<AyudaDeCaido>();
            var brazo = kes.transform.Find("AyudaDeCaido_Brazo"); var cartel = kes.transform.Find("AyudaDeCaido_Cartel");
            W8Ok(ref ok, ayuda != null && brazo != null && brazo.gameObject.activeInHierarchy && cartel != null && cartel.gameObject.activeInHierarchy, sb, "el aliado caido levanta el brazo y muestra el cartel de ayuda");
            var r0 = brazo != null ? brazo.localRotation : Quaternion.identity;
            foreach (var x in Esperar(0.25f)) yield return x;
            W8Ok(ref ok, brazo != null && Quaternion.Angle(r0, brazo.localRotation) > 2f, sb, "el brazo SALUDA (se mueve)");
            bool cuerpoVisible = false; foreach (var r in kes.GetComponentsInChildren<SkinnedMeshRenderer>()) if (r.enabled) cuerpoVisible = true;
            W8Ok(ref ok, cuerpoVisible, sb, "el cuerpo del caido sigue visible");
            if (enemigo != null) W8Ok(ref ok, enemigo.transform.Find("AyudaDeCaido_Brazo") == null, sb, "un enemigo muerto NO pide ayuda");
            Reanimacion.Ejecutar(kes);
            foreach (var x in Esperar(0.5f)) yield return x;
            var b2 = kes.transform.Find("AyudaDeCaido_Brazo");
            W8Ok(ref ok, b2 == null || !b2.gameObject.activeInHierarchy, sb, "al revivirlo deja de pedir ayuda");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 254a la camara de muerte mira al asesino
        static IEnumerator Bug254a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(false);
            foreach (var x in Esperar(1f)) yield return x;
            var yo = W9Yo(); var cam = CamaraPrincipal.Actual;
            Soldier asesino = null;
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.Brain != null) { asesino = s; break; }
            if (asesino == null) { Fin("FALLO no hay enemigo activo"); yield break; }
            var p = yo.transform.position + yo.transform.right * 9f + yo.transform.forward * 6f;
            asesino.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(p.x, 0.8f, p.z)); asesino.transform.position = new Vector3(p.x, 0.8f, p.z);
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s != yo && s.Brain != null) s.Brain.Pasivo = true;   // que nadie lo reviva ni lo cambie antes de los 3 s
            asesino.Brain.Pasivo = true;
            yo.Health.TakeDamage(99999, asesino.Id);
            foreach (var x in Esperar(1.5f)) yield return x;
            var haciaElAsesino = (asesino.transform.position - cam.transform.position); haciaElAsesino.y = 0f;
            var fwd = cam.transform.forward; fwd.y = 0f;
            float ang = Vector3.Angle(fwd, haciaElAsesino);
            W8Ok(ref ok, !yo.Health.IsAlive && W9Yo() == yo, sb, "a 1,5 s el jugador sigue en su cuerpo caido (no cambio todavia)");
            W8Ok(ref ok, ang < 25f, sb, $"la camara mira al asesino (desvio {ang:0.0} grados, a {haciaElAsesino.magnitude:0.0} m)");
            float t0m = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0m < 9f && W9Yo() == yo) yield return null;   // 3 s de juego (la camara de muerte puede ir lenta)
            var nuevo = W9Yo();
            W8Ok(ref ok, nuevo != null && nuevo != yo && nuevo.Health.IsAlive, sb, $"pasados los {PlayerInputDriver.TiempoMirandoAlAsesino:0} s pasa al aliado mas cercano ({(nuevo != null ? nuevo.DisplayName : "-")})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 255a con companeros caidos no se sube al helicoptero
        static IEnumerator Bug255a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(6, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s != yo && s.Health != null && !s.Health.IsAlive) Reanimacion.Ejecutar(s);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, OperacionDirector.AliadosCaidos() == 0, sb, "sin caidos la cuenta es 0");
            Soldier otro = null; foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s != yo && s.Role != RoleType.Civilian && s.Health.IsAlive) { otro = s; break; }
            if (otro != null) { ModoDios.Poner(false); ComandosDeDepuracion.Matar(otro.Id); ModoDios.Poner(true); }
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, OperacionDirector.AliadosCaidos() >= 1, sb, $"con un companero muerto la cuenta es {OperacionDirector.AliadosCaidos()}");
            var f = typeof(OperacionDirector).GetField("heli", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var heli = (Transform)f.GetValue(d);
            yo.transform.position = new Vector3(heli.position.x + 7f, 0.8f, heli.position.z); Physics.SyncTransforms();
            foreach (var x in Esperar(0.6f)) yield return x;
            W8Ok(ref ok, !d.SubiendoAlHeli, sb, "con un muerto a bordo NO se sube (el [E] no embarca)");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 236a / 239a / 252a coberturas a escala, sin tablones ni colliders de escombro de piso
        static IEnumerator Bug236a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(0.5f)) yield return x;
            int tablones = 0, craterConCollider = 0, escombroConCollider = 0, sacos = 0, sacosBajos = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name.StartsWith("P_Env_Barricada_Tablones")) tablones++;
                if ((t.name.StartsWith("P_Env_Crater") || t.name.StartsWith("P_Env_Escombro_Pila")) && t.GetComponentInChildren<Collider>() != null) { if (t.name.Contains("Crater")) craterConCollider++; else escombroConCollider++; }
                if (t.name.StartsWith("P_Env_Barricada_Sacos") && t.parent != null && t.parent.name == "PropsPorZonas")
                {
                    sacos++;
                    var r = t.GetComponentInChildren<Renderer>(); if (r != null && r.bounds.size.y < 0.95f) sacosBajos++;
                }
            }
            W8Ok(ref ok, tablones == 0, sb, $"sin vallas de tablones que obstaculicen ({tablones})");
            W8Ok(ref ok, craterConCollider == 0 && escombroConCollider == 0, sb, $"crateres y escombros de piso sin collider (crateres con collider={craterConCollider}, escombro={escombroConCollider})");
            W8Ok(ref ok, sacos == 0 || sacosBajos == 0, sb, $"sacos sueltos a escala (>= 0,95 m de alto): {sacos - sacosBajos}/{sacos}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
