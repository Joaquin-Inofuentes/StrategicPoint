using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.EditorTools
{
    // Tanda #104-#132, P7: Resistir rediseñado (#120 revivir / radio como rol / cambio con C, #122 cuentas, #114 pasos y guia).
    // Cada check reproduce el "ANTES" con un interruptor estatico de pruebas (que apaga la correccion) y mide el "DESPUES".
    // Teclas: [E] se inyecta con el teclado virtual (isPressed llega aunque el editor no tenga foco) y con OperacionTerminal.PruebaToqueE/PruebaMantenerE;
    // [C] con CambioDeAliado.PruebaApretada. No se probo con teclado/mouse reales.
    public static partial class ChecksBugs065
    {
        const string P7Prefijo = "v3_";

        // Radio tomada con la escuadra ubicada (RTS), modo dios. Restaura los interruptores de P7 al terminar cada check con P7Limpiar().
        static IEnumerable P7RadioTomada(System.Action<bool> listo)
        {
            foreach (var x in P6RadioTomada(listo)) yield return x;
        }

        static void P7Limpiar()
        {
            PlayerInputDriver.SoloEscuadraParaRevivir = false;
            MandoTactico.RadioObligaARts = false; MandoTactico.SoltarConToque = false; MandoTactico.MilicianosAActivar = MandoTactico.CantidadDeMilicianos;
            OperacionDirector.PasosSecuenciales = true;
            CameraRig.ProveedorDeRtsForzado = null;
            CambioDeAliado.PruebaApretada = false;
            OperacionTerminal.PruebaMantenerE = false; OperacionTerminal.PruebaToqueE = false;
            PedidoDeCuracion.AtencionAutomatica = true; PedidoDeCuracion.ReanimarEnCalma = true;
            var kb = Keyboard.current;
            if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState());
        }

        // Pasa a FPS con [Tab] (el camino real: AlternarVista) y espera a que se asiente.
        static IEnumerable P7AFps()
        {
            var drv = PlayerInputDriver.Activo;
            drv.AlternarVista();
            for (int i = 0; i < 6; i++) yield return null;
        }

        // ---------------------------------------------------------------- #120 (a) revivir milicianos
        static IEnumerator Bug120()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (Keyboard.current == null) { Fin("FALLO no hay Keyboard.current"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                bool listo = false;
                foreach (var x in P7RadioTomada(v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "radio tomada (Arrancar(5,1,1))");
                var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
                PedidoDeCuracion.AtencionAutomatica = false; PedidoDeCuracion.ReanimarEnCalma = false;   // que ningun medico de IA lo reviva por su cuenta
                var m = d.Milicianos.Count > 1 ? d.Milicianos[1] : null;   // MILICIANO 2
                P6Ok(ref ok, m != null && m.DisplayName.Contains("MILICIANO 2") && MandoTactico.EsMiliciano(m), sb, "existe MILICIANO 2");
                if (m == null) { Fin("FALLO " + sb); yield break; }
                foreach (var x in P7AFps()) yield return x;
                var yo = drv.Brain.Current;
                P6Ok(ref ok, drv.Rig.Mode == ControlMode.Fps && yo != null && !MandoTactico.EsOperadorDeRadio(yo), sb, $"FPS controlando a {yo.DisplayName} (el operador sigue en la radio)");
                ComandosDeDepuracion.Matar(m.Id);
                for (int i = 0; i < 4; i++) yield return null;
                Junto(yo, m.transform.position, 1.5f);
                for (int i = 0; i < 4; i++) yield return null;
                P6Ok(ref ok, !m.Health.IsAlive && !MandoTactico.Milicianos.Contains(null) && MandoTactico.EsMiliciano(m), sb, "MILICIANO 2 caido (sigue en MandoTactico.Milicianos)");

                // ANTES: solo la escuadra era candidata: el cartel salia (la mira detecta Caido) pero [E] no hacia nada.
                PlayerInputDriver.SoloEscuadraParaRevivir = true;
                bool candidatoAntes = drv.FindNearestDownedAlly() != null;
                Key kE = KeyBindings.Get(KeyBindings.Interactuar);
                KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 0f);
                Teclas(kE);
                float t0 = Time.realtimeSinceStartup; bool revivioAntes = false;
                while (Time.realtimeSinceStartup - t0 < 2.5f)
                {
                    yield return null;
                    KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, Time.realtimeSinceStartup - t0);
                    if (m.Health.IsAlive) { revivioAntes = true; break; }
                }
                Teclas();
                sb.Append($"ANTES (solo escuadra): candidato a revivir={candidatoAntes} (esperado False), [E] 2,5 s -> miliciano vivo={revivioAntes} (esperado False); ");
                if (candidatoAntes || revivioAntes) ok = false;

                // DESPUES: todo soldado del bando es candidato; [E] mantenido 5 s lo revive.
                PlayerInputDriver.SoloEscuadraParaRevivir = false;
                Junto(yo, m.transform.position, 1.5f);
                for (int i = 0; i < 3; i++) yield return null;
                var cand = drv.FindNearestDownedAlly();
                P6Ok(ref ok, cand == m, sb, $"DESPUES: candidato a revivir = {(cand != null ? cand.DisplayName : "ninguno")}");
                KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, 0f);
                Teclas(kE);
                t0 = Time.realtimeSinceStartup; float tRevivio = -1f; bool capturado = false;
                while (Time.realtimeSinceStartup - t0 < 7.5f)
                {
                    yield return null;
                    float t = Time.realtimeSinceStartup - t0;
                    KeyBindings.ForzarInicioDePulsacion(KeyBindings.Interactuar, t);
                    if (t > 2.5f && !capturado) { capturado = true; foreach (var x in CapturarPantalla(P7Prefijo + "120_revivir_miliciano.png")) yield return x; }
                    if (m.Health.IsAlive) { tRevivio = t; break; }
                }
                Teclas();
                P6Ok(ref ok, tRevivio >= 4.3f && tRevivio <= 7.2f, sb, $"[E] mantenido: el miliciano revive a los {tRevivio:0.0} s (se espera ~5)");
                foreach (var x in Esperar(0.5f)) yield return x;
                P6Ok(ref ok, m.Health.IsAlive && MandoTactico.EsMiliciano(m) && d.Milicianos.Count == MandoTactico.CantidadDeMilicianos && m.Brain != null && !m.Brain.IsPossessedByPlayer, sb, "vivo, sigue siendo miliciano y con su cerebro de IA");
            }
            finally { P7Limpiar(); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #120 (b) la radio es un rol, no un modo de camara
        static IEnumerator Bug120b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                bool listo = false;
                foreach (var x in P7RadioTomada(v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "radio tomada por el jugador, RTS (primer ingreso)");
                var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
                var op = MandoTactico.Operador;
                P6Ok(ref ok, op != null && op.Brain.IsPossessedByPlayer && MandoTactico.OperadorEsElJugador && d.PasoHecho[3], sb, $"operador = {op.DisplayName} (el jugador), pasos 1-4 hechos");

                // ANTES: la radio obligaba a RTS (Tab y posesion bloqueados, SetMode(Fps) rechazado).
                MandoTactico.RadioObligaARts = true;
                CameraRig.ProveedorDeRtsForzado = () => MandoTactico.EnRadio;
                rig.SetMode(ControlMode.Fps);
                yield return null; yield return null;
                bool atrapado = rig.Mode == ControlMode.Rts && MandoTactico.BloqueaElCambioDeVista() && MandoTactico.BloqueaLaPosesion();
                MandoTactico.RadioObligaARts = false; CameraRig.ProveedorDeRtsForzado = null;
                sb.Append($"ANTES (radio obliga a RTS): SetMode(Fps) rechazado y Tab/posesion bloqueados={atrapado} (esperado True); ");
                if (!atrapado) ok = false;

                // DESPUES: Tab -> FPS permitido; el jugador pasa a otro aliado y el operador sigue como IA "OPERANDO LA RADIO".
                var pos0 = op.transform.position;
                foreach (var x in P7AFps()) yield return x;
                var yo = drv.Brain.Current;
                P6Ok(ref ok, rig.Mode == ControlMode.Fps && yo != null && yo != op, sb, $"Tab -> FPS permitido, ahora controlas a {(yo != null ? yo.DisplayName : "?")}");
                P6Ok(ref ok, MandoTactico.EnRadio && ReferenceEquals(MandoTactico.Operador, op) && !op.Brain.IsPossessedByPlayer && !MandoTactico.OperadorEsElJugador, sb, "el operador sigue en la radio como IA");
                foreach (var x in Esperar(3.0f)) yield return x;
                bool anim = AnimacionDeAccion.TipoActivo(op) == TipoAccion.Operar;
                P6Ok(ref ok, anim, sb, $"el operador IA anima Operar (tipo activo={AnimacionDeAccion.TipoActivo(op)})");
                P6Ok(ref ok, op.Brain.Quieto && op.Motor.IsCrouching && (op.transform.position - pos0).magnitude < 3.2f, sb, $"quieto, de rodillas y en su puesto ({(op.transform.position - pos0).magnitude:0.0} m)");
                float r0 = d.RelojEfectivo;
                foreach (var x in Esperar(3.0f)) yield return x;
                P6Ok(ref ok, d.RelojDelMandoCorre && d.RelojEfectivo > r0 + 0.5f, sb, $"el reloj del mando corre con el jugador en FPS ({r0:0.0} -> {d.RelojEfectivo:0.0}), pasos>=4");
                // el jugador puede volver a RTS y a FPS sin que se suelte nada
                drv.AlternarVista();
                for (int i = 0; i < 5; i++) yield return null;
                var panel = PanelDeMando.Instancia;
                P6Ok(ref ok, rig.Mode == ControlMode.Rts && MandoTactico.EnRadio && panel != null && panel.BotonSoltarVisible, sb, "Tab otra vez -> RTS, el operador IA sigue y el panel ofrece SOLTAR LA RADIO");
                // el operador IA caido: se pierde la radio
                var vista = RenderTemporal(P7Prefijo + "120b_operador_ia.png", d.radioDeCampana.position + new Vector3(4.6f, 2.3f, -5.4f), d.radioDeCampana.position + new Vector3(0f, 0.5f, -1.2f), 42f);
                sb.Append("captura " + System.IO.Path.GetFileName(vista) + "; ");
                ModoDios.Poner(false);
                op.Health.TakeDamage(100000, -1);
                foreach (var x in Esperar(1.2f)) yield return x;
                ModoDios.Poner(true);
                P6Ok(ref ok, !op.Health.IsAlive && !MandoTactico.RadioTomada && !d.RelojDelMandoCorre, sb, $"operador caido: radio libre, el reloj se detiene ('{d.MotivoDeLaPausa}')");
            }
            finally { P7Limpiar(); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #120 (c) cambio de aliado con C
        static IEnumerator Bug120c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                bool listo = false;
                foreach (var x in P7RadioTomada(v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "radio tomada (RTS)");
                var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
                foreach (var x in P7AFps()) yield return x;
                foreach (var x in Esperar(0.6f)) yield return x;
                var ca = CambioDeAliado.Instancia;
                P6Ok(ref ok, ca != null && rig.Mode == ControlMode.Fps && KeyBindings.Get(KeyBindings.CambiarAliado) == Key.C, sb, "FPS y [C] = cambiar de aliado");

                // --- toque: el aliado vivo mas cercano
                var antes = drv.Brain.Current;
                var esperado = ca.MasCercano();
                ca.Actualizar(true, 0.05f); ca.Actualizar(false, 0.016f);
                for (int i = 0; i < 3; i++) yield return null;
                P6Ok(ref ok, esperado != null && drv.Brain.Current == esperado && esperado != antes, sb, $"toque de C: de {antes.DisplayName} a {(drv.Brain.Current != null ? drv.Brain.Current.DisplayName : "?")} (el mas cercano era {(esperado != null ? esperado.DisplayName : "?")})");

                // --- mantener: todos los rombos resaltados, candidato = el mas centrado aunque este ocluido
                var cam = CamaraPrincipal.Actual;
                var yo = drv.Brain.Current;
                var oculto = Soldados(TeamId.Player).Find(s => s != yo && !MandoTactico.EsOperadorDeRadio(s));
                bool ocluido = false;
                if (cam != null && oculto != null)
                {
                    // Se pone a un aliado DETRAS de una pared, justo en el centro de la pantalla.
                    var f = cam.transform.forward;
                    var pared = GameObject.CreatePrimitive(PrimitiveType.Cube);   // muro de prueba: 16 x 10 x 1 m a 8 m delante de la camara
                    pared.name = "ParedDePruebaP7";
                    pared.transform.position = cam.transform.position + f * 8f;
                    pared.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
                    pared.transform.localScale = new Vector3(16f, 10f, 1f);
                    Physics.SyncTransforms();
                    oculto.Brain.IsPossessedByPlayer = true;   // congelado: la IA no lo mueve
                    oculto.transform.position = cam.transform.position + f * 15f - Vector3.up * 0.4f;
                    Physics.SyncTransforms();
                    ocluido = Physics.Linecast(cam.transform.position, oculto.transform.position + Vector3.up * 1.4f, out var h2, ~0, QueryTriggerInteraction.Ignore) && h2.collider.gameObject == pared;
                    var elegidoOculto = ca.MasCentrado(cam);
                    Object.Destroy(pared);
                    P6Ok(ref ok, ocluido && elegidoOculto == oculto, sb, $"candidato OCLUIDO: aliado detras de una pared al centro de la pantalla (ocluido={ocluido}) -> candidato {(elegidoOculto != null ? elegidoOculto.DisplayName : "ninguno")}");
                    oculto.Brain.IsPossessedByPlayer = false;
                }
                // Camino real: [C] mantenida (CambioDeAliado.PruebaApretada) por el Update del driver.
                CambioDeAliado.PruebaApretada = true;
                foreach (var x in Esperar(0.8f)) yield return x;
                int posibles = 0; foreach (var s in Soldados(TeamId.Player)) if (s != drv.Brain.Current) posibles++;
                P6Ok(ref ok, ca.Resaltando && ca.Candidato != null && ca.RombosVisibles == posibles, sb, $"mantener C: resaltando={ca.Resaltando}, rombos={ca.RombosVisibles} de {posibles} aliados (incluye milicianos), candidato={(ca.Candidato != null ? ca.Candidato.DisplayName : "-")}");
                P6Ok(ref ok, ca.Pista.StartsWith("SOLTÁ [C] PARA CONTROLAR A "), sb, $"pista '{ca.Pista}'");
                // El candidato es el mas centrado (misma cuenta aparte, con tolerancia por el movimiento entre cuadros).
                float mejor = 9f, dCand = 9f; cam = CamaraPrincipal.Actual;
                foreach (var s in Soldados(TeamId.Player))
                {
                    if (s == drv.Brain.Current) continue;
                    var vp = cam.WorldToViewportPoint(s.transform.position + Vector3.up * 1.4f);
                    if (vp.z <= 0f) continue;
                    float dd = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f));
                    mejor = Mathf.Min(mejor, dd);
                    if (s == ca.Candidato) dCand = dd;
                }
                P6Ok(ref ok, dCand <= mejor + 0.03f, sb, $"el candidato esta a {dCand:0.000} del centro (el mejor: {mejor:0.000})");
                foreach (var x in CapturarPantalla(P7Prefijo + "120_rombos_resaltados.png")) yield return x;
                var previo = drv.Brain.Current;
                CambioDeAliado.PruebaApretada = false;
                for (int i = 0; i < 5; i++) yield return null;
                P6Ok(ref ok, !ca.Resaltando && ca.UltimoElegido != null && drv.Brain.Current == ca.UltimoElegido && drv.Brain.Current != previo, sb, $"soltar C: ahora controlas a {(drv.Brain.Current != null ? drv.Brain.Current.DisplayName : "?")} (el candidato era {(ca.UltimoElegido != null ? ca.UltimoElegido.DisplayName : "-")})");
            }
            finally { P7Limpiar(); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #120 (d) la radio se suelta con [E] mantenida 1 s, no con un toque
        static IEnumerator Bug120d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                bool listo = false;
                foreach (var x in P7RadioTomada(v => listo = v)) yield return x;
                P6Ok(ref ok, listo, sb, "radio tomada");
                var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var rig = drv.Rig;
                PedidoDeCuracion.AtencionAutomatica = false; PedidoDeCuracion.ReanimarEnCalma = false;
                var op = MandoTactico.Operador;
                // FPS controlando al operador (SetMode directo: sin relevo).
                rig.SetMode(ControlMode.Fps);
                for (int i = 0; i < 6; i++) yield return null;
                P6Ok(ref ok, rig.Mode == ControlMode.Fps && drv.Brain.Current == op && MandoTactico.OperadorEsElJugador, sb, "FPS poseyendo al operador");

                // ANTES: un toque de [E] soltaba la radio.
                MandoTactico.SoltarConToque = true;
                OperacionTerminal.PruebaToqueE = true;
                foreach (var x in Esperar(0.6f)) yield return x;
                bool soltoAntes = !MandoTactico.RadioTomada;
                OperacionTerminal.PruebaToqueE = false; MandoTactico.SoltarConToque = false;
                sb.Append($"ANTES (toque suelta): radio soltada por un toque={soltoAntes} (esperado True); ");
                if (!soltoAntes) ok = false;
                // retomar con [E] (junto a la radio)
                foreach (var x in Esperar(0.6f)) yield return x;
                OperacionTerminal.PruebaToqueE = true;
                foreach (var x in W9bHasta(() => MandoTactico.EnRadio, 3f)) yield return x;
                OperacionTerminal.PruebaToqueE = false;
                foreach (var x in Esperar(0.5f)) yield return x;
                P6Ok(ref ok, MandoTactico.EnRadio && rig.Mode == ControlMode.Fps, sb, "retomada con [E] sin forzar RTS");

                // DESPUES 1: toque de [E] NO suelta.
                OperacionTerminal.PruebaToqueE = true;
                foreach (var x in Esperar(1.0f)) yield return x;
                OperacionTerminal.PruebaToqueE = false;
                P6Ok(ref ok, MandoTactico.EnRadio, sb, "DESPUES: un toque de [E] no suelta la radio");

                // DESPUES 2: mantener < 1 s no suelta (progreso); revivir tiene prioridad sobre soltar.
                var m = d.Milicianos.Count > 0 ? d.Milicianos[0] : null;
                Junto(m, op.transform.position, 1.2f);
                for (int i = 0; i < 3; i++) yield return null;
                ComandosDeDepuracion.Matar(m.Id);
                for (int i = 0; i < 4; i++) yield return null;
                Junto(m, op.transform.position, 1.2f);
                P6Ok(ref ok, drv.FindNearestDownedAlly() == m, sb, "hay un caido al alcance del operador");
                OperacionTerminal.PruebaMantenerE = true;
                foreach (var x in Esperar(2.5f)) yield return x;
                bool seguia = MandoTactico.EnRadio;
                OperacionTerminal.PruebaMantenerE = false;
                P6Ok(ref ok, seguia, sb, "con un caido al alcance, [E] mantenida 2,5 s no suelta la radio (revivir tiene prioridad)");
                Reanimacion.Ejecutar(m);
                for (int i = 0; i < 4; i++) yield return null;

                // DESPUES 3: mantener 1 s suelta (y el progreso arranca antes).
                OperacionTerminal.PruebaMantenerE = true;
                float t0 = Time.realtimeSinceStartup; float progresoMax = 0f; bool soltada = false; float tSoltada = -1f;
                while (Time.realtimeSinceStartup - t0 < 8f)
                {
                    yield return null;
                    if (MandoTactico.RadioTomada) progresoMax = Mathf.Max(progresoMax, d.ESostenidaParaSoltar);
                    else { soltada = true; tSoltada = Time.realtimeSinceStartup - t0; break; }
                }
                OperacionTerminal.PruebaMantenerE = false;
                P6Ok(ref ok, soltada && progresoMax > 0.1f, sb, $"[E] mantenida: progreso hasta {progresoMax:0.00} y la radio se soltó a los {tSoltada:0.0} s");
                foreach (var x in Esperar(0.5f)) yield return x;
                P6Ok(ref ok, !MandoTactico.RadioTomada && !d.RelojDelMandoCorre && d.MotivoDeLaPausa.StartsWith("RADIO ABANDONADA"), sb, $"radio libre, el reloj se detiene ('{d.MotivoDeLaPausa}')");
            }
            finally { P7Limpiar(); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #122 las cuentas cierran: 4 milicianos
        static IEnumerator Bug122()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                // ANTES: 2 milicianos (lo de antes) -> 3 + 2 - 1 = 4 aliados para 3 sectores x 2.
                MandoTactico.MilicianosAActivar = 2;
                ArrancarEn(5, 1, 1);
                foreach (var x in Esperar(1.5f)) yield return x;
                ModoDios.Poner(true);
                var d = OperacionDirector.Instancia;
                int vivosAntes = 0; foreach (var s in Soldados(TeamId.Player)) if (!MandoTactico.EsOperadorDeRadio(s)) vivosAntes++;
                int necesarios = MandoTactico.CantidadDeSectores * MandoTactico.AliadosParaRecuperar;
                sb.Append($"ANTES (2 milicianos): aliados disponibles sin el operador={vivosAntes}, necesarios={necesarios} (no alcanza={vivosAntes < necesarios}); ");
                if (vivosAntes >= necesarios) ok = false;

                // DESPUES: 4 milicianos.
                MandoTactico.MilicianosAActivar = MandoTactico.CantidadDeMilicianos;
                ArrancarEn(5, 1, 1);
                foreach (var x in Esperar(1.5f)) yield return x;
                d = OperacionDirector.Instancia;
                var todos = Soldados(TeamId.Player);
                var libres = new List<Soldier>();
                foreach (var s in todos) if (!MandoTactico.EsOperadorDeRadio(s)) libres.Add(s);
                int escuadra = 0; foreach (var s in todos) if (!MandoTactico.EsMiliciano(s)) escuadra++;
                P6Ok(ref ok, d.Milicianos.Count == MandoTactico.CantidadDeMilicianos && MandoTactico.Milicianos.Count == 4, sb, $"DESPUES: milicianos activos={d.Milicianos.Count}");
                P6Ok(ref ok, escuadra + d.Milicianos.Count - 1 >= MandoTactico.CantidadDeSectores * MandoTactico.AliadosParaRecuperar && libres.Count >= 6, sb, $"escuadra {escuadra} + milicianos {d.Milicianos.Count} - operador 1 = {libres.Count} >= {MandoTactico.CantidadDeSectores} x {MandoTactico.AliadosParaRecuperar}");
                var roles = ""; foreach (var m in d.Milicianos) roles += m.Role + " ";
                sb.Append("roles " + roles + "; ");
                // Se cubren los tres sectores con 2 aliados cada uno.
                for (int i = 0; i < 3; i++)
                    for (int k = 0; k < 2 && i * 2 + k < libres.Count; k++)
                    {
                        var s = libres[i * 2 + k];
                        var c = d.sectoresDeDefensa[i].Centro + new Vector3(k == 0 ? -1.5f : 1.5f, 0f, 0f);
                        if (UnityEngine.AI.NavMesh.SamplePosition(c, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) c = h.position;
                        s.transform.position = new Vector3(c.x, s.transform.position.y, c.z);
                        ApoyoEnElPiso.Apoyar(s.transform);
                        if (s.Brain != null) { s.Brain.CancelOrder(); if (!s.Brain.IsPossessedByPlayer) s.Brain.Quieto = true; }
                    }
                foreach (var x in Esperar(1.0f)) yield return x;
                bool todosConDos = true; string det = "";
                for (int i = 0; i < 3; i++) { det += d.sectoresDeDefensa[i].letra + "=" + d.sectoresDeDefensa[i].AliadosDentro + " "; if (d.sectoresDeDefensa[i].AliadosDentro < MandoTactico.AliadosParaRecuperar) todosConDos = false; }
                P6Ok(ref ok, todosConDos, sb, "los 3 sectores con 2 aliados a la vez: " + det);
                // El texto del sector caido sale de la constante y, con las cuentas nuevas, se recupera.
                var A = d.sectoresDeDefensa[0];
                A.Caido = true; A.SegundosRecuperando = 0f;
                foreach (var x in Esperar(0.8f)) yield return x;
                string aviso = PanelDeMando.Instancia != null ? PanelDeMando.Instancia.TextoAviso : "(sin panel)";
                string num = MandoTactico.AliadosParaRecuperar.ToString();
                P6Ok(ref ok, aviso.Contains($"CON {num} ALIADOS") && d.MotivoDeLaPausa.Contains($"CON {num} ALIADOS"), sb, $"aviso='{aviso}' motivo='{d.MotivoDeLaPausa}' (contienen la constante {num})");
                foreach (var x in W9bHasta(() => !A.Caido, 12f)) yield return x;
                P6Ok(ref ok, !A.Caido, sb, "el sector caido se recupera con 2 aliados adentro (hay alcance para cubrirlo)");
                foreach (var x in CapturarPantalla(P7Prefijo + "122_sectores_cubiertos.png")) yield return x;
            }
            finally { P7Limpiar(); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #114 pasos de a uno, punteado y flecha, sin marcador del helicoptero
        static IEnumerator Bug114()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                // ---- ANTES: sin la secuencia, seleccionar a los milicianos tacha el paso 3 con el paso 1 pendiente (la captura del reporte).
                OperacionDirector.PasosSecuenciales = false;
                ArrancarEn(5);
                foreach (var x in Esperar(1.2f)) yield return x;
                ModoDios.Poner(true);
                var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
                d.TomarLaRadioPorPrueba(false);
                foreach (var x in Esperar(1.0f)) yield return x;
                var ms = new List<Soldier>(d.Milicianos);
                W10Elegir(ms.ToArray());
                foreach (var x in Esperar(0.5f)) yield return x;
                bool rotoAntes = !d.PasoHecho[0] && d.PasoHecho[2];
                sb.Append($"ANTES (sin secuencia): paso 1 pendiente={!d.PasoHecho[0]} y paso 3 tachado={d.PasoHecho[2]} (el bug: {(rotoAntes ? "reproducido" : "NO reproducido")}); ");
                if (!rotoAntes) ok = false;

                // ---- DESPUES
                OperacionDirector.PasosSecuenciales = true;
                ArrancarEn(5);
                foreach (var x in Esperar(1.2f)) yield return x;
                d = OperacionDirector.Instancia; drv = PlayerInputDriver.Activo;
                d.TomarLaRadioPorPrueba(false);
                foreach (var x in Esperar(1.0f)) yield return x;
                var panel = PanelDeMando.Instancia; var guia = GuiaDeMando.Instancia;
                P6Ok(ref ok, MandoTactico.EnRadio && panel != null && guia != null, sb, "DESPUES: radio tomada, panel y guia presentes");
                bool InvarianteOk(out string det)
                {
                    int actual = d.PasoActualDelMando; det = $"actual={actual} hechos=";
                    bool bien = true; for (int i = 0; i < d.PasoHecho.Length; i++) { det += d.PasoHecho[i] ? "1" : "0"; if (actual >= 0 && i > actual && d.PasoHecho[i]) bien = false; if (actual >= 0 && i == actual && d.PasoHecho[i]) bien = false; }
                    return bien;
                }
                ms = new List<Soldier>(d.Milicianos);
                W10Elegir(ms.ToArray());   // el mismo gesto que rompia el panel
                foreach (var x in Esperar(0.5f)) yield return x;
                P6Ok(ref ok, !d.PasoHecho[2] && d.PasoActualDelMando == 0 && panel.Actual == 0, sb, $"seleccionar a los milicianos primero NO tacha el paso 3 (actual={d.PasoActualDelMando}, panel.Actual={panel.Actual})");
                P6Ok(ref ok, guia.Activa && guia.PasoGuiado == 0 && guia.FlechaVisible && !guia.RutaActiva, sb, $"paso 1: flecha 3D sobre {d.UnidadA.DisplayName} (visible={guia.FlechaVisible}) y todavia sin ruta");
                P6Ok(ref ok, panel.TextoActual.StartsWith("SELECCIONÁ A ") && panel.TextoActual.Contains("ROMBO") && panel.TextoActual.Length < 60, sb, $"texto corto con verbo+destino: '{panel.TextoActual}'");
                W10Elegir(d.UnidadA);
                foreach (var x in Esperar(1.0f)) yield return x;
                bool inv = InvarianteOk(out var det1);
                P6Ok(ref ok, d.PasoHecho[0] && d.PasoActualDelMando == 1 && panel.Actual == 1 && inv, sb, $"seleccionar a {d.UnidadA.DisplayName} avanza al paso 2 ({det1})");
                // el punteado: del soldado al centro del sector A
                var A = d.sectoresDeDefensa[0].Centro;
                foreach (var x in W9bHasta(() => guia.RutaActiva, 2f)) yield return x;
                float dIni = guia.RutaActiva ? Vector3.Distance(guia.InicioDeLaRuta, d.UnidadA.transform.position) : 999f;
                float dFin = guia.RutaActiva ? Vector3.Distance(new Vector3(guia.FinDeLaRuta.x, 0f, guia.FinDeLaRuta.z), new Vector3(A.x, 0f, A.z)) : 999f;
                P6Ok(ref ok, guia.RutaActiva && guia.PuntosDeRuta >= 2 && dIni < 6f && dFin < 11f && guia.PasoGuiado == 1, sb, $"paso 2: punteado activo ({guia.PuntosDeRuta} puntos) desde {d.UnidadA.DisplayName} (a {dIni:0.0} m) hasta el sector A (a {dFin:0.0} m), ancho {guia.AnchoDeLaRuta:0.00} m");
                P6Ok(ref ok, guia.FlechaVisible && Vector3.Distance(new Vector3(guia.PosicionDeLaFlecha.x, 0f, guia.PosicionDeLaFlecha.z), new Vector3(A.x, 0f, A.z)) < 3f, sb, "flecha 3D sobre el circulo del sector A");
                P6Ok(ref ok, panel.TextoActual.Contains("SECTOR A") && panel.TextoActual.Contains("CLIC DERECHO"), sb, $"texto: '{panel.TextoActual}'");
                foreach (var x in CapturarPantalla(P7Prefijo + "114_paso2_punteado.png")) yield return x;
                // el resto de los pasos de a uno
                W10Ordenar(A, d.UnidadA);
                foreach (var x in Esperar(0.8f)) yield return x;
                bool inv2 = InvarianteOk(out var det2);
                P6Ok(ref ok, d.PasoHecho[1] && d.PasoActualDelMando == 2 && inv2, sb, $"orden a A: paso 3 activo ({det2})");
                P6Ok(ref ok, panel.TextoActual.Contains(MandoTactico.CantidadDeMilicianos.ToString()), sb, $"el paso 3 dice cuantos milicianos hay: '{panel.TextoActual}'");
                W10Elegir(ms.ToArray());
                foreach (var x in Esperar(0.8f)) yield return x;
                bool inv3 = InvarianteOk(out var det3);
                P6Ok(ref ok, d.PasoHecho[2] && d.PasoActualDelMando == 3 && inv3, sb, $"milicianos seleccionados: paso 4 activo ({det3})");
                P6Ok(ref ok, d.FlechaHeli == null || !d.FlechaHeli.Visible, sb, $"marcador HELICOPTERO oculto en Resistir (existe={d.FlechaHeli != null})");

                // ---- el marcador aparece en Extraer y nunca a > 600 m
                ArrancarEn(6);
                foreach (var x in Esperar(2.0f)) yield return x;
                d = OperacionDirector.Instancia;
                string txt = d.FlechaHeli != null ? d.FlechaHeli.TextoActual : "";
                int metros = -1;
                var partes = txt.Split(new[] { '·', 'm' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length >= 2) int.TryParse(partes[1].Trim(), out metros);
                P6Ok(ref ok, d.FlechaHeli != null && d.FlechaHeli.Visible && metros >= 0 && metros <= OperacionDirector.DistanciaMaximaDelMarcadorDelHeli, sb, $"Extraer: marcador '{txt}' (<= {OperacionDirector.DistanciaMaximaDelMarcadorDelHeli:0} m)");
            }
            finally { P7Limpiar(); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
