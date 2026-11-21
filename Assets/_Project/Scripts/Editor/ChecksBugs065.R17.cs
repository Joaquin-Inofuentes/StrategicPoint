using System.Collections;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 17: evidencia medida de la reaccion al dano (pedidos 2/15) y del trigger contra el blindado (pedido 14).
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- 990a un enemigo herido por el jugador responde (ReaccionAlDano)
        static IEnumerator Bug990a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = W9Yo();
            W8Ok(ref ok, ReaccionAlDano.Activa, sb, "la reaccion esta activa en la Operacion");
            // victima: un enemigo vivo, sin puesto fijo por rol, lo mas cerca del jugador
            Soldier v = null; float mejor = float.MaxValue;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.Brain == null || !s.gameObject.activeInHierarchy) continue;
                if (s.Brain.Pasivo || s.Brain.PuestoFijoPorRol || s.Brain.MontadoEnVehiculo) continue;
                float d = (s.transform.position - yo.transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; v = s; }
            }
            if (v == null) { Fin("FALLO no hay un enemigo apto para herir"); yield break; }
            v.Brain.Quieto = true; v.Brain.Atrincherado = true;   // peor caso: atado a su puesto
            int antes = ReaccionAlDano.Aplicadas;
            v.Health.TakeDamage(5, yo.Id);
            float t0 = Time.realtimeSinceStartup; bool respondio = false;
            while (Time.realtimeSinceStartup - t0 < 3f && !respondio) { respondio = ReaccionAlDano.Aplicadas > antes; yield return null; }
            float seg = Time.realtimeSinceStartup - t0;
            W8Ok(ref ok, respondio, sb, $"la reaccion se aplico a los {seg:0.00} s (esperado 0,25-0,6 s; aplicadas {antes} -> {ReaccionAlDano.Aplicadas})");
            W8Ok(ref ok, !v.Brain.Quieto && !v.Brain.Atrincherado, sb, $"el herido sale de Quieto/Atrincherado (Quieto={v.Brain.Quieto}, Atrincherado={v.Brain.Atrincherado})");
            foreach (var x in Esperar(1f)) yield return x;
            W8Ok(ref ok, v.Brain.CurrentTarget != null || v.Brain.State == SP.Ai.AiState.Chase || v.Brain.State == SP.Ai.AiState.Attack, sb, $"el herido va por el tirador (estado={v.Brain.State}, blanco={(v.Brain.CurrentTarget != null ? v.Brain.CurrentTarget.name : "ninguno")})");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990b la toma del jefe arranca al cruzar el umbral, al danar el blindado o por salvaguarda
        static IEnumerator Bug990b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            d.AbrirSalidaNortePorPrueba(false);
            foreach (var x in Esperar(3f)) yield return x;
            W8Ok(ref ok, !d.TomaDelJefeActiva && d.Subfase == (int)SubfaseHuida.Acercamiento, sb, "tras 3 s con el muro caido y el jugador en z<=-30 la toma no arranca");
            // cruzar el umbral
            var p = yo.transform.position; p.z = OperacionDirector.ZParaLaToma + 4f;
            yo.transform.position = p; Physics.SyncTransforms();
            float t0 = Time.realtimeSinceStartup; bool toma = false;
            while (Time.realtimeSinceStartup - t0 < 5f && !toma) { toma = d.TomaDelJefeActiva || d.Subfase != (int)SubfaseHuida.Acercamiento; yield return null; }
            W8Ok(ref ok, toma, sb, $"al cruzar z={OperacionDirector.ZParaLaToma} (puesto en z={yo.transform.position.z:0.0}) la toma arranca en {Time.realtimeSinceStartup - t0:0.0} s");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990c clip de muerte por direccion del golpe (tabla + muerte real)
        static IEnumerator Bug990c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            W8Ok(ref ok, SP.Presentation.CubeFxReactor.ElegirMuertePorAngulo(0f, false, false) == 0 && SP.Presentation.CubeFxReactor.ElegirMuertePorAngulo(180f, false, false) == 1
                && SP.Presentation.CubeFxReactor.ElegirMuertePorAngulo(90f, false, false) == 2 && SP.Presentation.CubeFxReactor.ElegirMuertePorAngulo(0f, true, false) == 3
                && SP.Presentation.CubeFxReactor.ElegirMuertePorAngulo(180f, true, false) == 4 && SP.Presentation.CubeFxReactor.ElegirMuertePorAngulo(0f, true, true) == 5,
                sb, "tabla: frente=0, espalda=1, derecha=2, headshot frente=3, headshot espalda=4, headshot agachado=5");
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var yo = W9Yo();
            // dos victimas: una que mira al lado contrario del tirador (golpe por la espalda) y una de frente con headshot
            Soldier espalda = null, frente = null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy || s.Brain == null || s.Brain.Pasivo) continue;
                if (espalda == null) espalda = s; else if (frente == null) { frente = s; break; }
            }
            if (espalda == null || frente == null) { Fin("FALLO no hay dos enemigos para la prueba"); yield break; }
            var haciaYo = yo.transform.position - espalda.transform.position; haciaYo.y = 0f;
            espalda.transform.rotation = Quaternion.LookRotation(-haciaYo.normalized, Vector3.up);   // mira para el otro lado
            haciaYo = yo.transform.position - frente.transform.position; haciaYo.y = 0f;
            frente.transform.rotation = Quaternion.LookRotation(haciaYo.normalized, Vector3.up);     // mira al tirador
            espalda.Health.TakeDamage(99999, yo.Id, false);
            frente.Health.TakeDamage(99999, yo.Id, true);
            foreach (var x in Esperar(0.6f)) yield return x;
            int ve = LeerVarianteDeMuerte(espalda), vf = LeerVarianteDeMuerte(frente);
            W8Ok(ref ok, ve == 1, sb, $"golpe por la espalda: variante {ve} (esperada 1)");
            W8Ok(ref ok, vf == 3 || vf == 5, sb, $"headshot de frente: variante {vf} (esperada 3, o 5 si estaba agachado) y MurioPorLaCabeza={frente.Health.MurioPorLaCabeza}");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static int LeerVarianteDeMuerte(Soldier s)
        {
            foreach (var a in s.GetComponentsInChildren<Animator>(true))
                if (a != null && a.runtimeAnimatorController != null)
                    foreach (var p in a.parameters)
                        if (p.name == SP.Presentation.SoldierAnimatorDriver.ParamMuerteVariante) return a.GetInteger(p.name);
            return -1;
        }

        // ---------------------------------------------------------------- 990d el aro de uso del tanque: fuera del radio no esta "dentro", a 3 m si
        static IEnumerator Bug990d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            ArrancarEn(4, 1, (int)SubfaseHuida.Abordaje);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo(); var a = AnilloDeSubida.Instancia;
            if (a == null || d == null || d.tanque == null) { Fin("FALLO no hay anillo o tanque"); yield break; }
            var t = d.tanque.transform.position;
            yo.transform.position = t + new Vector3(0f, 0f, 14f); Physics.SyncTransforms();
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, !a.DentroDelRadio, sb, $"a {a.DistanciaAlJugador:0.0} m: fuera del radio (radio de uso {a.RadioDeUso:0.0} m)");
            yo.transform.position = t + new Vector3(0f, 0f, 3f); Physics.SyncTransforms();
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, a.DentroDelRadio, sb, $"a {a.DistanciaAlJugador:0.0} m: dentro del radio (radio de uso {a.RadioDeUso:0.0} m), cartel='{a.TextoDelCartel}'");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990e balizas del hackeo: apagadas sin operador, 2 encendidas con operador, apagadas al soltarlo
        static IEnumerator Bug990e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo(); var pc = d != null ? d.computadora : null;
            if (pc == null) { Fin("FALLO no hay computadora"); yield break; }
            pc.ForzarDesbloqueo();
            W8Ok(ref ok, SP.Presentation.ServidoresVivosR17.BalizasEncendidas == 0 && !SP.Presentation.ServidoresVivosR17.HackeoActivo, sb, "sin operador: 0 balizas encendidas");
            Soldier op = null;
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s != yo && s.Health != null && s.Health.IsAlive) { op = s; break; }
            if (op == null) { Fin("FALLO no hay un aliado para operar la computadora"); yield break; }
            op.transform.position = pc.transform.position + pc.transform.forward * 1.2f; Physics.SyncTransforms();
            bool enviado = pc.EnviarA(op, out string motivo);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 25f && (pc.Operador == null || SP.Presentation.ServidoresVivosR17.BalizasEncendidas == 0)) yield return null;
            W8Ok(ref ok, pc.Operador != null, sb, $"EnviarA={enviado} ('{motivo}'): operador={(pc.Operador != null ? pc.Operador.name : "ninguno")} a los {Time.realtimeSinceStartup - t0:0.0} s");
            W8Ok(ref ok, SP.Presentation.ServidoresVivosR17.BalizasEncendidas == 2 && SP.Presentation.ServidoresVivosR17.HackeoActivo, sb, $"hackeando: {SP.Presentation.ServidoresVivosR17.BalizasEncendidas} balizas encendidas (esperadas 2)");
            int visibles = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) if (r.enabled && r.transform.root != null && r.name == "Cuerpo" && r.transform.parent != null && r.transform.parent.name == "Pivote" && r.transform.parent.parent != null && r.transform.parent.parent.name.StartsWith("Baliza_Roja")) visibles++;
            W8Ok(ref ok, visibles == 2, sb, $"mallas de baliza visibles en la escena: {visibles}");
            pc.ResetearProgreso();
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, SP.Presentation.ServidoresVivosR17.BalizasEncendidas == 0, sb, $"al soltar el operador: {SP.Presentation.ServidoresVivosR17.BalizasEncendidas} balizas (esperadas 0)");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990f la toma del jefe arranca al danar el blindado desde la sala (sin cruzar el umbral)
        static IEnumerator Bug990f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            d.AbrirSalidaNortePorPrueba(false);
            foreach (var x in Esperar(4f)) yield return x;
            W8Ok(ref ok, !d.TomaDelJefeActiva && d.Subfase == (int)SubfaseHuida.Acercamiento && yo.transform.position.z <= OperacionDirector.ZParaLaToma, sb, $"antes del golpe: sin toma, subfase={d.Subfase}, z={yo.transform.position.z:0.0}");
            if (d.tanque == null || d.tanque.Health == null) { Fin("FALLO no hay vehiculo del jefe"); yield break; }
            d.tanque.Health.TakeDamage(40, yo.Id);
            float t0 = Time.realtimeSinceStartup; bool toma = false;
            while (Time.realtimeSinceStartup - t0 < 5f && !toma) { toma = d.TomaDelJefeActiva || d.Subfase != (int)SubfaseHuida.Acercamiento; yield return null; }
            W8Ok(ref ok, toma && yo.transform.position.z <= OperacionDirector.ZParaLaToma, sb, $"al danar el blindado la toma arranca en {Time.realtimeSinceStartup - t0:0.0} s con el jugador aun en z={yo.transform.position.z:0.0}");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990g salvaguarda: a los 45 s de caer el muro la toma arranca aunque el jugador no haga nada
        static IEnumerator Bug990g()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.AbrirSalidaNortePorPrueba(false);
            float t0 = Time.realtimeSinceStartup; bool toma = false; float tMitad = -1f;
            while (Time.realtimeSinceStartup - t0 < OperacionDirector.SegundosDeSalvaguardaDeLaToma + 15f && !toma)
            {
                toma = d.TomaDelJefeActiva || d.Subfase != (int)SubfaseHuida.Acercamiento;
                if (tMitad < 0f && Time.realtimeSinceStartup - t0 > 30f) { tMitad = 1f; W8Ok(ref ok, !toma, sb, "a los 30 s todavia no arranco"); }
                yield return null;
            }
            float seg = Time.realtimeSinceStartup - t0;
            W8Ok(ref ok, toma && seg >= OperacionDirector.SegundosDeSalvaguardaDeLaToma - 4f && seg <= OperacionDirector.SegundosDeSalvaguardaDeLaToma + 12f, sb, $"la toma arranco por salvaguarda a los {seg:0.0} s (esperado ~{OperacionDirector.SegundosDeSalvaguardaDeLaToma:0} s)");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990h pesado: mascara (1er headshot no mata), furia, y telegrafo (laser) mientras estaciona
        static IEnumerator Bug990h()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            for (int intento = 0; intento < 1 && molde == null; intento++)
            {
                foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado") && a.GetComponent<PerroEnemigo>() == null) { molde = a; break; }
                if (molde == null) yield return new WaitForSeconds(0.25f);
            }
            //            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado") && a.GetComponent<PerroEnemigo>() == null) { molde = a; break; }
            if (molde == null) { int tot = 0, vivos = 0, act = 0; foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy) { tot++; if (a.Health != null && a.Health.IsAlive) vivos++; if (a.gameObject.activeInHierarchy) act++; } Fin($"FALLO no hay enemigo molde (total={tot} vivos={vivos} activos={act} fase={OperacionDirector.Instancia?.Fase} yo={(yo != null ? yo.name : "null")})"); yield break; }
            var go = Object.Instantiate(molde.gameObject, yo.transform.position + yo.transform.forward * 22f + Vector3.up * 0.1f, Quaternion.LookRotation(-yo.transform.forward));
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            var p = EnemigoPesado.Aplicar(s);
            foreach (var x in Esperar(0.8f)) yield return x;
            W8Ok(ref ok, p != null && p.MascaraPuesta && s.Health.CapasDeCabeza == 1, sb, $"con mascara puesta (capas de cabeza {s.Health.CapasDeCabeza}, piezas armadas)");
            if (s.Brain != null) s.Brain.SetPatrolRoute(new[] { yo.transform.position, yo.transform.position });
            float t0 = Time.realtimeSinceStartup; bool laser = false; float ancho0 = 0f, ancho1 = 0f; bool vioFrenesi = false;
            while (Time.realtimeSinceStartup - t0 < 14f && p != null && !vioFrenesi)
            {
                yield return null;
                if (p.Estado == EnemigoPesado.Fase.Estacionando && p.TelegrafoActivo) { if (!laser) { laser = true; ancho0 = p.AnchoDelTelegrafo; } ancho1 = p.AnchoDelTelegrafo; }
                if (p.Estado == EnemigoPesado.Fase.Frenesi) vioFrenesi = true;
            }
            W8Ok(ref ok, laser, sb, "mientras estaciona sale el laser de aviso");
            W8Ok(ref ok, ancho1 > ancho0, sb, $"el laser se engrosa hasta abrir fuego ({ancho0:0.000} -> {ancho1:0.000})");
            W8Ok(ref ok, vioFrenesi && !p.TelegrafoActivo, sb, "al abrir fuego el laser se apaga");
            // 1er headshot: rompe la mascara y entra en furia; el 2o mata
            var h = HitboxCabeza.De(s);
            s.Health.TakeDamage(60, yo.Id, true);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, s.Health.IsAlive && p.EnFuria && p.PiezasDeMascaraSueltas == 3, sb, $"1er headshot: vivo={s.Health.IsAlive}, furia={p.EnFuria}, piezas sueltas={p.PiezasDeMascaraSueltas}");
            s.Health.TakeDamage(60, yo.Id, true);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, !s.Health.IsAlive, sb, $"2o headshot lo mata (vivo={s.Health.IsAlive})");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990i caminos de NavMesh con los objetos nuevos de la sala de servidores, la fachada y la guarnicion
        static IEnumerator Bug990i()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(2.5f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo(); var pc = d.computadora;
            var desde = yo.transform.position;
            // puntos de la sala: la computadora, el centro, las cuatro esquinas utiles y las posiciones de la guarnicion
            var puntos = new System.Collections.Generic.List<(string, Vector3)>
            {
                ("computadora", pc.transform.position), ("centro de la sala", new Vector3(0f, 0.1f, -60f)),
                ("esquina NO", new Vector3(-20f, 0.1f, -45f)), ("esquina NE", new Vector3(20f, 0.1f, -45f)),
                ("esquina SO", new Vector3(-20f, 0.1f, -75f)), ("esquina SE", new Vector3(20f, 0.1f, -75f)),

            };
            int malos = 0;
            foreach (var (nombre, p) in puntos)
            {
                string r = W9Nav(desde, p, out var estado);
                bool bien = estado == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                if (!bien) malos++;
                sb.Append($"[{nombre}: {r}{(bien ? "" : " MAL")}] ");
            }
            ok &= malos == 0;
            // dos puntos sobre la sala: de la computadora a cada esquina (camino entre objetos nuevos)
            foreach (var (nombre, p) in puntos)
            {
                if (nombre == "computadora") continue;
                var origenPc = UnityEngine.AI.NavMesh.SamplePosition(pc.transform.position, out var hp, 3f, UnityEngine.AI.NavMesh.AllAreas) ? hp.position : pc.transform.position;
                string r = W9Nav(origenPc, p, out var estado);
                if (estado != UnityEngine.AI.NavMeshPathStatus.PathComplete) { ok = false; sb.Append($"[PC->{nombre}: {r} MAL] "); }
            }
            // ningun obstaculo nuevo debe cerrar los pasillos: los NavMeshObstacle de R17_* no pueden cubrir el eje central x=0
            int obst = 0, sobreElEje = 0;
            foreach (var o in Object.FindObjectsByType<UnityEngine.AI.NavMeshObstacle>(FindObjectsInactive.Exclude))
            {
                if (o == null || !o.enabled) continue;
                bool r17 = false; for (var t = o.transform; t != null; t = t.parent) if (t.name.StartsWith("R17_")) { r17 = true; break; }
                if (!r17) continue;
                obst++;
                var b = new Bounds(o.transform.position + o.center, o.size);
                if (b.min.x < 1.5f && b.max.x > -1.5f && b.center.z > -80f && b.center.z < -39f) sobreElEje++;
            }
            W8Ok(ref ok, sobreElEje == 0, sb, $"obstaculos R17 en el NavMesh: {obst}, sobre el eje central de la sala: {sobreElEje}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990j rendimiento: tiempo de cuadro en la sala de servidores con y sin los objetos R17 (A/B en la misma corrida)
        static IEnumerator Bug990j()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(3f)) yield return x;
            var yo = W9Yo();
            var p = yo.transform.position; p.x = 0f; p.z = -62f; yo.transform.position = p; Physics.SyncTransforms();
            foreach (var x in Esperar(1.5f)) yield return x;
            var grupos = new System.Collections.Generic.List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.name.StartsWith("R17_") && t.parent != null && !t.parent.name.StartsWith("R17_")) grupos.Add(t.gameObject);
            int renderersR17 = 0, lucesR17 = 0, sistemasR17 = 0;
            foreach (var g in grupos) { renderersR17 += g.GetComponentsInChildren<Renderer>(false).Length; lucesR17 += g.GetComponentsInChildren<Light>(false).Length; sistemasR17 += g.GetComponentsInChildren<ParticleSystem>(false).Length; }
            // 5 s con los objetos
            var con = new System.Collections.Generic.List<float>();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 5f) { yield return null; con.Add(Time.unscaledDeltaTime); }
            foreach (var g in grupos) if (g != null) g.SetActive(false);
            foreach (var x in Esperar(1f)) yield return x;
            var sin = new System.Collections.Generic.List<float>();
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 5f) { yield return null; sin.Add(Time.unscaledDeltaTime); }
            foreach (var g in grupos) if (g != null) g.SetActive(true);
            float mc = Media(con) * 1000f, ms = Media(sin) * 1000f, pc = Percentil(con, 0.95f) * 1000f, ps = Percentil(sin, 0.95f) * 1000f;
            sb.Append($"grupos R17={grupos.Count} renderers={renderersR17} luces={lucesR17} sistemas de particulas={sistemasR17}; ");
            W8Ok(ref ok, mc <= ms * 1.25f + 1.0f, sb, $"cuadro medio con R17 {mc:0.00} ms ({con.Count} cuadros), sin R17 {ms:0.00} ms ({sin.Count} cuadros); p95 {pc:0.00} vs {ps:0.00} ms");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static float Media(System.Collections.Generic.List<float> l) { if (l.Count == 0) return 0f; float s = 0f; foreach (var v in l) s += v; return s / l.Count; }
        static float Percentil(System.Collections.Generic.List<float> l, float q) { if (l.Count == 0) return 0f; var c = new System.Collections.Generic.List<float>(l); c.Sort(); return c[Mathf.Clamp((int)(q * (c.Count - 1)), 0, c.Count - 1)]; }

        // ---------------------------------------------------------------- 990k dificultad: la escuadra (sin que el jugador dispare) contra la guarnicion nueva del Centro de datos
        static IEnumerator Bug990k()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionDirector.ProbarBloqueoDelCentro = true;
            OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = W9Yo();
            int garr0 = W9Vivos(d.GuarnicionDelCentro);
            W8Ok(ref ok, garr0 >= 4, sb, $"guarnicion armada: {garr0} (4 a 6 esperados), PC bloqueada={d.computadora.Bloqueada}");
            int aliados0 = 0; foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.Health != null && s.Health.IsAlive) aliados0++;
            float t0 = Time.realtimeSinceStartup; float tLimpia = -1f;
            while (Time.realtimeSinceStartup - t0 < 75f)
            {
                yield return null;
                if (W9Vivos(d.GuarnicionDelCentro) == 0) { tLimpia = Time.realtimeSinceStartup - t0; break; }
                if (yo == null || yo.Health == null || !yo.Health.IsAlive) break;
            }
            int aliados1 = 0; foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.Health != null && s.Health.IsAlive) aliados1++;
            sb.Append($"sin disparar el jugador: guarnicion eliminada={(tLimpia >= 0f ? tLimpia.ToString("0.0") + " s" : "NO en 75 s")}, aliados {aliados0} -> {aliados1}, restantes={W9Vivos(d.GuarnicionDelCentro)}, PC bloqueada={d.computadora.Bloqueada}, reacciones aplicadas={ReaccionAlDano.Aplicadas}; ");
            W8Ok(ref ok, aliados1 >= 1, sb, "queda al menos un aliado en pie");
            float tDes = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - tDes < 4f && d.computadora.Bloqueada) yield return null;
            W8Ok(ref ok, !d.computadora.Bloqueada, sb, $"la PC se desbloquea {Time.realtimeSinceStartup - tDes:0.0} s despues de eliminar la guarnicion (restantes en el recuento={d.computadora.Restantes})");
            OperacionDirector.ProbarBloqueoDelCentro = false;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 990l tomas de la muerte: golpe por la espalda y headshot de frente, con el cuerpo cayendo a la vista
        static IEnumerator Bug990l()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var yo = W9Yo();
            Soldier a = null, b = null;
            foreach (var s in ActorRegistry.All) { if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || s.GetComponent<EnemigoPesado>() != null) continue; if (a == null) a = s; else { b = s; break; } }
            if (a == null || b == null) { Fin("FALLO no hay dos enemigos"); yield break; }
            foreach (var s in new[] { a, b })
            {
                var ag = s.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.enabled = false;
                if (s.Brain != null) s.Brain.enabled = false;
                s.gameObject.SetActive(true);
            }
            var fwd = yo.transform.forward; fwd.y = 0f; fwd.Normalize(); var der = Vector3.Cross(Vector3.up, fwd);
            a.transform.position = yo.transform.position + fwd * 6f - der * 1.4f; a.transform.rotation = Quaternion.LookRotation(fwd);      // de espaldas al tirador
            b.transform.position = yo.transform.position + fwd * 7.5f + der * 1.4f; b.transform.rotation = Quaternion.LookRotation(-fwd);   // de frente
            Physics.SyncTransforms();
            foreach (var x in Esperar(0.5f)) yield return x;
            a.transform.rotation = Quaternion.LookRotation(fwd); b.transform.rotation = Quaternion.LookRotation(-fwd);   // la IA pudo girarlos mientras esperaban
            a.Health.TakeDamage(99999, yo.Id, false);
            b.Health.TakeDamage(99999, yo.Id, true);
            foreach (var x in Esperar(0.35f)) yield return x;
            foreach (var x in CapturarPantalla("r17_muerte_cayendo.png")) yield return x;
            foreach (var x in Esperar(1.2f)) yield return x;
            foreach (var x in CapturarPantalla("r17_muerte_en_el_piso.png")) yield return x;
            int va = LeerVarianteDeMuerte(a), vb = LeerVarianteDeMuerte(b);
            W8Ok(ref ok, va == 1 && (vb == 3 || vb == 5), sb, $"variantes: espalda={va} (1), headshot de frente={vb} (3 o 5)");
            W8Ok(ref ok, !a.Health.IsAlive && !b.Health.IsAlive, sb, "los dos murieron");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
