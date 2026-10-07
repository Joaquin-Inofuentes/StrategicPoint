using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.UI;
using SP.Vehicles;

namespace SP.EditorTools
{
    // WP10 (#101): RESISTIR dependiente del RTS, "DIRIGIR LA DEFENSA DESDE LA RADIO". Play FRESCO por check (ChecksBugs065.CorrerUno("Bug101a")).
    // En RTS no se puede inyectar un clic real desde el CLI: se prueba con las MISMAS funciones a las que llama el mouse (SelectionController,
    // OrderService.IssueFormationOrderForSelection, IssueAttackVehicleOrderForSelection) y [E] con OperacionTerminal.PruebaToqueE. No se probo con mouse real.
    //   101a  la radio: llegada sin enemigos, [E] toma la radio, la camara pasa SOLA a RTS (P7 #120: solo la primera vez, ya NO es obligatoria: se
    //         puede volver a FPS), el operador no se mueve, recibe la mitad del dano, salen 4 milicianos (#122: 6 aliados seleccionables), el panel y el
    //         anillo guia marcan el paso 1; el boton del panel suelta la radio y vuelve a FPS con el reloj detenido; se retoma con [E] (sin forzar RTS).
    //   101b  pasos y reloj: 10 s con los pasos sin cumplir y el reloj efectivo no se mueve; las ordenes por API avanzan los pasos del panel y arranca el
    //         reloj; las unidades llegan a sus sectores; flecha de oleada anunciada.
    //   101c  caida y recuperacion: sector amenazado y vacio cae a los 6 s, el reloj se pausa, 2 aliados 4 s lo recuperan y el reloj sigue.
    //   101d  los aliados que llegan a un sector se cubren solos en <= 6 s (#074).
    //   101e  la cadena entera: 90 s efectivos (5 oleadas, camionetas, orden de ataque a vehiculo) -> Extraer -> Victoria; fps y milicianos que no suben.
    //   101f  reglas puras de MandoTactico (sin escena).
    //   101g  si el operador muere la radio queda libre y la vista deja de ser obligatoria.
    //   101h  EstadoDeSesion: capturar y restaurar la fase (relojEfectivo, sectoresCaidos, radioTomada).
    public static partial class ChecksBugs065
    {
        const string W10Prefijo = "v2_101_";

        static Soldier W10Aliado(string fragmento)
        {
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.DisplayName != null && s.DisplayName.Contains(fragmento)) return s;
            return null;
        }

        static void W10Elegir(params Soldier[] l)
        {
            var sel = PlayerInputDriver.Activo.Selection;
            sel.Clear();
            foreach (var s in l) if (s != null) sel.AddToSelection(s);
        }

        static void W10Ordenar(Vector3 punto, params Soldier[] l)
        {
            W10Elegir(l);
            OrderService.IssueFormationOrderForSelection(PlayerInputDriver.Activo.Selection.Selected, punto, Vector3.forward, FormationKind.Cuadricula, false);
        }

        static int W10EnemigosEnLaCiudad()
        {
            var d = OperacionDirector.Instancia; int n = 0;
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Enemy && s.gameObject.activeInHierarchy && s.Health.IsAlive
                    && (s.transform.position - d.plaza.position).sqrMagnitude < 110f * 110f) n++;
            return n;
        }

        // Arranca el objetivo 5 (llegada), pone modo dios, acerca la escuadra a la radio y la toma con [E].
        static IEnumerable W10TomarLaRadio(StringBuilder sb, Action<bool> listo)
        {
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia;
            ModoDios.Poner(true);
            d.TeletransportarEscuadra(new Vector3(324.2f, 0f, 265.2f), Quaternion.identity);
            foreach (var x in Esperar(0.6f)) yield return x;
            OperacionTerminal.PruebaToqueE = true;
            foreach (var x in W9bHasta(() => MandoTactico.EnRadio, 3f)) yield return x;
            foreach (var x in Esperar(0.5f)) yield return x;
            listo(MandoTactico.EnRadio && d.Subfase == 1);
        }

        // ---------------------------------------------------------------- 101a la radio
        static IEnumerator Bug101a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var rig = drv.Rig; var hud = OperacionHud.Instancia;
            if (d == null || drv == null || rig == null || hud == null) { Fin("FALLO sin director/driver/camara/HUD"); yield break; }
            ModoDios.Poner(true);
            var yo = drv.Brain.Current;
            // 1) llegada: sin radio, sin sectores pintados, sin enemigos, reloj quieto, FPS.
            foreach (var x in Esperar(3f)) yield return x;
            W8Ok(ref ok, d.Fase == FaseOperacion.Resistir && d.Subfase == 0 && d.MandoActivo && !MandoTactico.RadioTomada && rig.Mode == ControlMode.Fps, sb, $"llegada: fase={d.Fase} subfase={d.Subfase} FPS={rig.Mode == ControlMode.Fps}");
            W8Ok(ref ok, d.segundosDeResistencia == 90f && d.RelojEfectivo == 0f && d.OleadasDelMandoAnunciadas == 0 && W10EnemigosEnLaCiudad() == 0, sb, $"90 s efectivos, reloj={d.RelojEfectivo:0.0}, enemigos en la ciudad={W10EnemigosEnLaCiudad()} (esperaba 0 hasta tomar la radio)");
            W8Ok(ref ok, hud.TextoDetalle.ToLowerInvariant().Contains("radio") && hud.TextoTitulo.Contains("DIRIGIR LA DEFENSA"), sb, $"HUD llegada='{hud.TextoTitulo}' / '{hud.TextoDetalle}'");
            // 2) a menos de 3,6 m de la radio: prompt y [E].
            d.TeletransportarEscuadra(new Vector3(324.2f, 0f, 265.2f), Quaternion.identity);
            foreach (var x in Esperar(0.7f)) yield return x;
            W8Ok(ref ok, hud.TextoPrompt.Contains("[E] TOMAR LA RADIO") && hud.TextoPrompt.Contains("coordin"), sb, $"prompt='{hud.TextoPrompt}'");
            OperacionTerminal.PruebaToqueE = true;
            foreach (var x in W9bHasta(() => MandoTactico.EnRadio, 3f)) yield return x;
            foreach (var x in Esperar(1.0f)) yield return x;
            yo = drv.Brain.Current;
            var radio = d.radioDeCampana.position;
            W8Ok(ref ok, MandoTactico.EnRadio && d.Subfase == 1 && rig.Mode == ControlMode.Rts, sb, $"radio tomada: subfase={d.Subfase}, camara={rig.Mode}");
            foreach (var x in CapturarPantalla(W10Prefijo + "mando_paso1.png")) yield return x;
            float altura = rig.transform.position.y;
            W8Ok(ref ok, altura >= 50f && Mathf.Abs(rig.rtsYaw) < 1f && (rig.RtsFoco - new Vector3(324.2f, 0f, 265.2f)).magnitude < 6f, sb, $"vista tactica (P6/#121: el foco nace donde estaba el soldado, antes en el centro de los sectores 322,246): altura={altura:0.0} yaw={rig.rtsYaw:0} foco={rig.RtsFoco}");
            W8Ok(ref ok, (yo.transform.position - radio).magnitude < 3.2f && yo.Motor != null && yo.Motor.IsCrouching, sb, $"el operador esta en la radio a {(yo.transform.position - radio).magnitude:0.0} m, arrodillado={(yo.Motor != null && yo.Motor.IsCrouching)}");
            // 3) P7 (#120b): la vista tactica ya NO es obligatoria (antes: no se salia de RTS ni con SetMode/Toggle/Tab). Se puede pasar a FPS sin soltar
            // la radio; se vuelve a RTS para seguir con el resto del check.
            rig.SetMode(ControlMode.Fps);
            foreach (var x in Esperar(0.2f)) yield return x;
            bool pasoAFps = rig.Mode == ControlMode.Fps && MandoTactico.EnRadio;
            rig.SetMode(ControlMode.Rts, yo.transform.position);
            foreach (var x in Esperar(0.2f)) yield return x;
            W8Ok(ref ok, pasoAFps && rig.Mode == ControlMode.Rts && !CameraRig.RtsForzado && !MandoTactico.BloqueaElCambioDeVista() && !MandoTactico.BloqueaLaPosesion(), sb, $"P7: la radio no obliga a RTS: paso a FPS={pasoAFps}, forzado={CameraRig.RtsForzado}, Tab bloqueado={MandoTactico.BloqueaElCambioDeVista()}, posesion bloqueada={MandoTactico.BloqueaLaPosesion()}");
            // pista de la primera vez
            W8Ok(ref ok, MandoTactico.TextoDeTabLibre.Contains("[TAB]") && MandoTactico.TextoDeTabLibre.Contains("OPERADOR SIGUE EN LA RADIO") && MandoTactico.PrimerIngresoHecho, sb, "la pista de la primera vez dice como volver a FPS y que el operador sigue en la radio");
            // 4) el operador no se mueve ni con una orden de mover.
            var p0 = yo.transform.position;
            OrderService.IssueMoveOrder(yo, p0 + new Vector3(0f, 0f, 25f));
            foreach (var x in Esperar(2.0f)) yield return x;
            W8Ok(ref ok, (yo.transform.position - p0).magnitude < 0.3f && OrderService.LoManejaElJugador(yo), sb, $"el operador no se mueve en 2 s ({(yo.transform.position - p0).magnitude:0.00} m) ni obedece ordenes");
            // 5) dano x0,5: el mismo soldado recibe 200 con el factor del mando y 200 sin el (el resto de la cadena, como la dificultad, es igual).
            ModoDios.Poner(false);
            float factor = yo.Health.FactorDeDano;
            int a0 = yo.Health.Current;
            yo.Health.TakeDamage(200, -1);
            int dConMando = a0 - yo.Health.Current;
            yo.Health.Heal(dConMando);
            yo.Health.FactorDeDano = 1f;
            a0 = yo.Health.Current;
            yo.Health.TakeDamage(200, -1);
            int dSinMando = a0 - yo.Health.Current;
            yo.Health.Heal(dSinMando);
            yo.Health.FactorDeDano = factor;
            ModoDios.Poner(true);
            W8Ok(ref ok, factor == 0.5f && dSinMando > 0 && Mathf.Abs(dConMando * 2 - dSinMando) <= 2, sb, $"dano del operador con la radio {dConMando} vs {dSinMando} sin el factor (x0,5: factor={factor})");
            // 6) milicianos y aliados seleccionables.
            int milicianos = 0; bool rolesOk = true;
            foreach (var m in d.Milicianos) { if (m != null && m.Team == TeamId.Player && m.gameObject.activeInHierarchy && m.Health.IsAlive && MandoTactico.EsMiliciano(m)) milicianos++; }
            rolesOk = d.Milicianos.Count == MandoTactico.CantidadDeMilicianos && d.Milicianos[0].Role == RoleType.Assault && d.Milicianos[1].Role == RoleType.Medic && d.Milicianos[2].Role == RoleType.Flanker && d.Milicianos[3].Role == RoleType.Assault;
            var elegibles = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.Health.IsAlive && s.gameObject.activeInHierarchy && !MandoTactico.EsOperadorDeRadio(s)) elegibles.Add(s);
            drv.Selection.SelectAll(elegibles);
            int seleccionados = drv.Selection.Selected.Count;
            drv.Selection.Clear();
            W8Ok(ref ok, milicianos == 4 && rolesOk && elegibles.Count == 6 && seleccionados == 6, sb, $"P7/#122: 4 milicianos (Assault, Medic, Flanker, Assault), aliados seleccionables={elegibles.Count}, seleccion de todos={seleccionados} (pide 6 = 3 + 4 - el operador)");
            // 7) panel guiado y anillo guia en el paso 1.
            var panel = PanelDeMando.Instancia;
            W8Ok(ref ok, panel != null && panel.Filas == OperacionDirector.PasosDelMando && panel.Actual == 0 && panel.TextoActual.Contains("SELECCION") && panel.TextoActual.Contains(RolRequerido.NombreCorto(d.UnidadA)), sb, $"panel: filas={(panel != null ? panel.Filas : -1)} actual={(panel != null ? panel.Actual : -9)} '{(panel != null ? panel.TextoActual : "-")}'");
            W8Ok(ref ok, d.AnillosDeGuia == 1, sb, $"anillo guia sobre {d.UnidadA.DisplayName}: {d.AnillosDeGuia}");
            // el paso 1 se cumple al seleccionar a Kes y el anillo se apaga.
            W10Elegir(d.UnidadA);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, d.PasoHecho[0] && panel.Actual == 1 && d.AnillosDeGuia == 0, sb, $"seleccionar a {d.UnidadA.DisplayName} cumple el paso 1 (panel.Actual={panel.Actual}, anillos={d.AnillosDeGuia})");
            drv.Selection.Clear();
            // 8) soltar la radio: FPS, reloj detenido con su motivo; retomarla.
            // P6/#124: en RTS la [E] sube la camara y ya no suelta la radio: se suelta con el boton SOLTAR LA RADIO del panel.
            OperacionTerminal.PruebaToqueE = true;
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, MandoTactico.EnRadio && PanelDeMando.Instancia != null && PanelDeMando.Instancia.BotonSoltarVisible, sb, "P6: [E] en RTS ya no suelta la radio y el panel muestra el boton SOLTAR LA RADIO");
            OperacionTerminal.PruebaToqueE = false;
            PanelDeMando.Instancia.PulsarBotonSoltar();
            foreach (var x in Esperar(0.8f)) yield return x;
            W8Ok(ref ok, !MandoTactico.RadioTomada && rig.Mode == ControlMode.Fps && !d.RelojDelMandoCorre && d.MotivoDeLaPausa.StartsWith("RADIO ABANDONADA"), sb, $"boton suelta la radio: FPS={rig.Mode == ControlMode.Fps}, reloj corre={d.RelojDelMandoCorre}, motivo='{d.MotivoDeLaPausa}'");
            float r0 = d.RelojEfectivo;
            foreach (var x in Esperar(2f)) yield return x;
            W8Ok(ref ok, Mathf.Abs(d.RelojEfectivo - r0) < 0.05f, sb, $"con la radio suelta el reloj no avanza ({r0:0.00} -> {d.RelojEfectivo:0.00})");
            foreach (var x in Esperar(0.5f)) yield return x;
            OperacionTerminal.PruebaToqueE = true;
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, MandoTactico.EnRadio && rig.Mode == ControlMode.Fps, sb, $"[E] la retoma: EnRadio={MandoTactico.EnRadio}, camara={rig.Mode} (P7: retomar NO fuerza RTS, solo la primera vez)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Prepara la escena del mando y manda a todos a sus sectores por API (Kes -> A, milicianos -> B, el otro -> C). Llena 'llegada' con los
        // segundos (desde la orden) a los que cada sector tuvo aliados adentro.
        static bool w10Listo;
        static readonly float[] w10Cubierta = new float[3];   // segundos (desde la orden) en que cada sector tuvo a alguien en cobertura
        static IEnumerable W10Ubicar(StringBuilder sb, float[] llegada)
        {
            bool listo = false;
            foreach (var x in W10TomarLaRadio(sb, v => listo = v)) yield return x;
            var d = OperacionDirector.Instancia;
            w10Listo = listo;
            if (!listo) yield break;
            var a = d.UnidadA; var c = d.UnidadC; var ms = new List<Soldier>(d.Milicianos);
            float t0 = Time.realtimeSinceStartup;
            W10Elegir(a); foreach (var x in Esperar(0.25f)) yield return x;
            W10Ordenar(d.sectoresDeDefensa[0].Centro, a); foreach (var x in Esperar(0.25f)) yield return x;
            W10Elegir(ms.ToArray()); foreach (var x in Esperar(0.25f)) yield return x;
            W10Ordenar(d.sectoresDeDefensa[1].Centro, ms.ToArray()); foreach (var x in Esperar(0.25f)) yield return x;
            W10Elegir(c); foreach (var x in Esperar(0.25f)) yield return x;
            W10Ordenar(d.sectoresDeDefensa[2].Centro, c); foreach (var x in Esperar(0.25f)) yield return x;
            for (int i = 0; i < 3; i++) { llegada[i] = -1f; w10Cubierta[i] = -1f; }
            while (Time.realtimeSinceStartup - t0 < 45f)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (llegada[i] < 0f && d.sectoresDeDefensa[i].AliadosDentro > 0) llegada[i] = Time.realtimeSinceStartup - t0;
                    if (w10Cubierta[i] < 0f && llegada[i] >= 0f)
                        foreach (var s in ActorRegistry.All)
                        {
                            if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || !s.Health.IsAlive || !s.gameObject.activeInHierarchy || MandoTactico.EsOperadorDeRadio(s)) continue;
                            if (d.sectoresDeDefensa[i].Contiene(s.transform.position) && s.Brain.EnCobertura) { w10Cubierta[i] = Time.realtimeSinceStartup - t0; break; }
                        }
                }
                if (llegada[0] >= 0f && llegada[1] >= 0f && llegada[2] >= 0f && w10Cubierta[0] >= 0f && w10Cubierta[1] >= 0f && w10Cubierta[2] >= 0f) break;
                yield return null;
            }
        }

        // ---------------------------------------------------------------- 101b pasos y reloj
        static IEnumerator Bug101b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool listo = false;
            foreach (var x in W10TomarLaRadio(sb, v => listo = v)) yield return x;
            var d = OperacionDirector.Instancia; var panel = PanelDeMando.Instancia; var drv = PlayerInputDriver.Activo;
            if (!listo) { Fin("FALLO no se pudo tomar la radio"); yield break; }
            // 1) 10 s sin cumplir los pasos: el reloj efectivo no se mueve y nadie ataca.
            foreach (var x in Esperar(10f)) yield return x;
            W8Ok(ref ok, d.RelojEfectivo == 0f && !d.RelojDelMandoCorre && d.MotivoDeLaPausa.StartsWith("UBICA"), sb, $"10 s sin ubicar: reloj efectivo={d.RelojEfectivo:0.00} (constante), motivo='{d.MotivoDeLaPausa}'");
            W8Ok(ref ok, d.OleadasDelMandoAnunciadas == 0 && W10EnemigosEnLaCiudad() == 0 && panel.TextoAviso.Contains("UBICA"), sb, $"sin oleadas ni enemigos, aviso del panel='{panel.TextoAviso}'");
            // 2) las ordenes por API cumplen los pasos del panel en orden.
            var a = d.UnidadA; var c = d.UnidadC; var ms = new List<Soldier>(d.Milicianos);
            W10Elegir(a); foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, d.PasoHecho[0] && panel.Actual == 1, sb, $"paso 1 (seleccionar) hecho, panel.Actual={panel.Actual}");
            W10Ordenar(d.sectoresDeDefensa[0].Centro, a); foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, d.PasoHecho[1] && panel.Actual == 2, sb, $"paso 2 (orden al sector A) hecho, panel.Actual={panel.Actual}");
            W10Elegir(ms.ToArray()); foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, d.PasoHecho[2] && panel.Actual == 3, sb, $"paso 3 (los dos milicianos) hecho, panel.Actual={panel.Actual}");
            float rAntes = d.RelojEfectivo;
            W10Ordenar(d.sectoresDeDefensa[1].Centro, ms.ToArray()); foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, d.PasoHecho[3] && panel.Actual == 4 && d.RelojDelMandoCorre, sb, $"paso 4 (milicianos al sector B) hecho, panel.Actual={panel.Actual}, el reloj corre={d.RelojDelMandoCorre}");
            W10Ordenar(d.sectoresDeDefensa[2].Centro, c); foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, d.PasoHecho[4] && panel.Hechos >= 5, sb, $"paso 5 ({RolRequerido.NombreCorto(c)} al sector C) hecho, pasos hechos={panel.Hechos}");
            // 3) llegan a sus sectores; sale la primera flecha de aviso.
            var llegada = new float[] { -1f, -1f, -1f };
            float t0 = Time.realtimeSinceStartup; bool capSectores = false; bool flecha = false; int anunciadas = 0;
            while (Time.realtimeSinceStartup - t0 < 40f)
            {
                for (int i = 0; i < 3; i++) if (llegada[i] < 0f && d.sectoresDeDefensa[i].AliadosDentro > 0) llegada[i] = Time.realtimeSinceStartup - t0;
                if (FlechaDeOleada.Activas > 0) flecha = true;
                anunciadas = d.OleadasDelMandoAnunciadas;
                if (!capSectores && FlechaDeOleada.Activas > 0 && llegada[0] >= 0f)
                {
                    capSectores = true;
                    foreach (var x in Esperar(5.0f)) yield return x;
                    foreach (var x in CapturarPantalla(W10Prefijo + "sectores.png")) yield return x;
                }
                if (llegada[0] >= 0f && llegada[1] >= 0f && llegada[2] >= 0f && capSectores) break;
                yield return null;
            }
            W8Ok(ref ok, llegada[0] >= 0f && llegada[1] >= 0f && llegada[2] >= 0f, sb, $"llegadas a A/B/C: {llegada[0]:0.0}/{llegada[1]:0.0}/{llegada[2]:0.0} s");
            W8Ok(ref ok, flecha && anunciadas >= 1 && capSectores, sb, $"flecha de oleada visible={flecha} (pool {FlechaDeOleada.Todas.Count}), oleadas anunciadas={anunciadas}");
            W8Ok(ref ok, d.RelojEfectivo > rAntes + 3f, sb, $"el reloj efectivo avanza: {rAntes:0.0} -> {d.RelojEfectivo:0.0} s");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101c caida y recuperacion
        static IEnumerator Bug101c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var llegada = new float[3];
            w10Listo = false; foreach (var x in W10Ubicar(sb, llegada)) yield return x;
            ok = w10Listo;
            var d = OperacionDirector.Instancia; if (!ok) { Fin("FALLO " + sb); yield break; }
            var A = d.sectoresDeDefensa[0]; var B = d.sectoresDeDefensa[1];
            // Esperar a que A este amenazado (oleada 1 anunciada) con Kes adentro, y retirar a Kes.
            foreach (var x in W9bHasta(() => A.Amenazado && A.AliadosDentro > 0, 40f)) yield return x;
            W8Ok(ref ok, A.Amenazado && !A.Caido, sb, $"sector A amenazado (oleada {d.OleadasDelMandoAnunciadas} anunciada) y cubierto por {A.AliadosDentro}");
            foreach (var x in Esperar(1.0f)) yield return x;
            float corriaAntes = d.RelojDelMandoCorre ? 1f : 0f;
            var kes = d.UnidadA;
            W10Ordenar(B.Centro, kes);
            float tRetiro = Time.realtimeSinceStartup;
            foreach (var x in W9bHasta(() => A.AliadosDentro == 0, 6f)) yield return x;
            float tVacio = Time.realtimeSinceStartup;
            foreach (var x in W9bHasta(() => A.Caido, 15f)) yield return x;
            float segVacio = Time.realtimeSinceStartup - tVacio;
            W8Ok(ref ok, A.Caido && segVacio >= 5.0f && segVacio <= 7.5f, sb, $"A cae {segVacio:0.0} s despues de quedar vacio (6 +- 1,5)");
            float r1 = d.RelojEfectivo;
            foreach (var x in Esperar(3.0f)) yield return x;
            W8Ok(ref ok, Mathf.Abs(d.RelojEfectivo - r1) < 0.06f && !d.RelojDelMandoCorre && d.MotivoDeLaPausa.Contains("SECTOR CAIDO"), sb, $"con A caido el reloj se pausa ({r1:0.00} -> {d.RelojEfectivo:0.00}), motivo='{d.MotivoDeLaPausa}'");
            W8Ok(ref ok, PanelDeMando.Instancia.TextoAviso.Contains("CAÍDO") && OperacionHud.Instancia.TextoDetalle.Contains("A ✗"), sb, $"panel='{PanelDeMando.Instancia.TextoAviso}' HUD='{OperacionHud.Instancia.TextoDetalle}'");
            foreach (var x in CapturarPantalla(W10Prefijo + "sector_caido.png")) yield return x;
            // Recuperar: Kes y el segundo (C) vuelven a A, 2 aliados adentro 4 s.
            var otro = d.UnidadC;
            W10Ordenar(A.Centro, kes, otro);
            foreach (var x in W9bHasta(() => A.AliadosDentro >= 2, 40f)) yield return x;
            float tDentro = Time.realtimeSinceStartup;
            foreach (var x in W9bHasta(() => !A.Caido, 15f)) yield return x;
            float segRec = Time.realtimeSinceStartup - tDentro;
            W8Ok(ref ok, !A.Caido && segRec >= 3.0f && segRec <= 6.5f, sb, $"A se recupera {segRec:0.0} s despues de tener 2 aliados adentro (4 +- 1,5)");
            float r2 = d.RelojEfectivo;
            foreach (var x in Esperar(3.0f)) yield return x;
            W8Ok(ref ok, d.RelojEfectivo > r2 + 1.5f && d.SectoresCaidosTotales >= 1, sb, $"el reloj sigue ({r2:0.0} -> {d.RelojEfectivo:0.0}), sectores caidos en la partida={d.SectoresCaidosTotales}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101d coberturas automaticas (#074)
        static IEnumerator Bug101d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var llegada = new float[3];
            w10Listo = false; foreach (var x in W10Ubicar(sb, llegada)) yield return x;
            ok = w10Listo;
            var d = OperacionDirector.Instancia; if (!ok) { Fin("FALLO " + sb); yield break; }
            // Puntos de cobertura que ofrece cada sector (>= 3 de los 4 pedidos).
            for (int i = 0; i < 3; i++)
            {
                int n = d.PuntosDeCoberturaDelSector(i).Count;
                W8Ok(ref ok, n >= 3, sb, $"sector {d.sectoresDeDefensa[i].letra}: {n} puntos de cobertura");
            }
            // Desde que cada sector tiene aliados adentro, alguno queda en cobertura en <= 6 s.
            for (int i = 0; i < 3; i++)
            {
                float dt = w10Cubierta[i] - llegada[i];
                W8Ok(ref ok, llegada[i] >= 0f && w10Cubierta[i] >= 0f && dt <= 6f, sb, $"sector {d.sectoresDeDefensa[i].letra}: en cobertura {dt:0.0} s despues de llegar (llegada a los {llegada[i]:0.0} s)");
            }
            W8Ok(ref ok, d.CoberturasAutomaticas >= 3, sb, $"ordenes de cobertura automaticas={d.CoberturasAutomaticas}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101e la cadena entera + rendimiento
        static IEnumerator Bug101e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5, 1, 1);   // radio tomada y la escuadra ya ubicada en A/B/C
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            ModoDios.Poner(true);
            W8Ok(ref ok, d.Fase == FaseOperacion.Resistir && d.Subfase == 1 && MandoTactico.EnRadio && d.PasoHecho[3], sb, $"Arrancar(5,_,1): subfase={d.Subfase}, radio tomada={MandoTactico.EnRadio}, pasos 1-4 hechos");
            W8Ok(ref ok, SP.Mision.EstadisticasDeMision.SupervivientesForzados < 0, sb, "SupervivientesForzados sin forzar durante Resistir");
            float t0 = Time.realtimeSinceStartup; float acum = 0f; int frames = 0; float peor = 0f; bool ordenAtaque = false; int maxVivos = 0, maxCamionetas = 0; int caidos = 0;
            var vistas = new HashSet<int>(); Soldier soldadoQueVuelve = null; float volverA = -1f;
            while (d.Fase == FaseOperacion.Resistir && Time.realtimeSinceStartup - t0 < 160f)
            {
                float dt = Time.unscaledDeltaTime; acum += dt; frames++; peor = Mathf.Max(peor, dt);
                if (volverA > 0f && Time.realtimeSinceStartup > volverA) { W10Ordenar(d.sectoresDeDefensa[1].Centro, soldadoQueVuelve); volverA = -1f; }
                int vivos = 0; foreach (var o in d.OleadasDelMando) vivos += o.Vivos;
                maxVivos = Mathf.Max(maxVivos, vivos);
                for (int i = 0; i < d.OleadasDelMando.Count; i++) if (d.OleadasDelMando[i].lanzada) vistas.Add(i);
                foreach (var o in d.OleadasDelMando)
                {
                    if (o.VehiculosVivos > 0 && !ordenAtaque)
                    {
                        var veh = o.vehiculos.Find(v => v != null && !v.Muerto);
                        if (veh != null && veh.Vehiculo != null)
                        {
                            maxCamionetas++;
                            var atacante = d.Milicianos[0];
                            W10Elegir(atacante);
                            int n = OrderService.IssueAttackVehicleOrderForSelection(drv.Selection.Selected, veh.Vehiculo);
                            ordenAtaque = n > 0;
                            // La orden saca al miliciano del sector B: vuelve enseguida (el otro lo cubre) para no pausar el reloj a proposito.
                            soldadoQueVuelve = atacante; volverA = Time.realtimeSinceStartup + 0.6f;
                        }
                    }
                }
                foreach (var s in d.sectoresDeDefensa) if (s.Caido) caidos++;
                yield return null;
            }
            float dur = Time.realtimeSinceStartup - t0;
            float fps = frames / Mathf.Max(0.01f, acum);
            W8Ok(ref ok, d.Fase == FaseOperacion.Extraer && d.HeliEnLaZona, sb, $"a los {d.RelojEfectivo:0.0} s efectivos ({dur:0} s reales) la fase pasa a {d.Fase}, el helicoptero esta en la zona (P8 #127: estacionario cubriendo)={d.HeliEnLaZona}");
            W8Ok(ref ok, vistas.Count == 5 && d.OleadasDelMandoLanzadas == 5, sb, $"oleadas lanzadas {d.OleadasDelMandoLanzadas}/5, maximo de atacantes vivos a la vez={maxVivos}");
            W8Ok(ref ok, !MandoTactico.RadioTomada && drv.Rig.Mode == ControlMode.Fps && PanelDeMando.Instancia == null && FlechaDeOleada.Activas == 0, sb, $"al final se suelta la radio, vuelve FPS ({drv.Rig.Mode}), sin panel ni flechas");
            W8Ok(ref ok, ordenAtaque && d.PasoHecho[5], sb, $"orden de ataque contra la camioneta emitida={ordenAtaque} y paso 6 hecho={d.PasoHecho[5]}");
            W8Ok(ref ok, fps >= 20f, sb, $"rendimiento en el editor: {fps:0.0} fps medios, peor frame {peor * 1000f:0} ms (informativo: la comparacion contra el flujo viejo es 101pa/101pb; sectores caidos en la partida={d.SectoresCaidosTotales})");
            // Extraer -> Victoria: P8 (#128) los milicianos SUBEN con la escuadra (antes se quedaban cubriendo).
            var ms = new List<Soldier>(d.Milicianos);
            foreach (var x in Esperar(1.0f)) yield return x;
            d.SubirAlHeli();
            foreach (var x in W9bHasta(() => d.Fase == FaseOperacion.Victoria && !d.FundidoActivo, 90f)) yield return x;
            int milicianosEnElHeli = 0; foreach (var m in ms) if (m != null && !m.gameObject.activeInHierarchy) milicianosEnElHeli++;
            W8Ok(ref ok, d.Fase == FaseOperacion.Victoria && OperacionDirector.SubidosAlHeli >= 1, sb, $"cadena completa: fase={d.Fase}, subidos al helicoptero={OperacionDirector.SubidosAlHeli}");
            W8Ok(ref ok, SP.Mision.EstadisticasDeMision.SupervivientesForzados == OperacionDirector.SubidosAlHeli && OperacionDirector.SubidosAlHeli >= 1, sb, $"SupervivientesForzados={SP.Mision.EstadisticasDeMision.SupervivientesForzados} coincide con los subidos ({OperacionDirector.SubidosAlHeli}), milicianos incluidos");
            W8Ok(ref ok, milicianosEnElHeli == ms.Count && ms.Count == MandoTactico.CantidadDeMilicianos, sb, $"los milicianos suben con la escuadra (a bordo={milicianosEnElHeli}/{ms.Count}; P8 #128, antes se quedaban)");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101i campanario
        static int W10EnemigosLejanosVisibles(out int lejanos)
        {
            lejanos = 0; int visibles = 0;
            var aliados = new List<Vector3>();
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Player && s.Health.IsAlive && s.gameObject.activeInHierarchy) aliados.Add(s.transform.position);
            foreach (var ic in UnityEngine.Object.FindObjectsByType<MinimapIcon>(FindObjectsSortMode.None))
            {
                if (!ic.FogEnabled) continue;
                var p = ic.TargetPosition; bool lejos = true;
                foreach (var a in aliados) if ((a - p).sqrMagnitude < 14f * 14f) { lejos = false; break; }
                if (!lejos) continue;
                lejanos++;
                if (ic.IsRendered) visibles++;
            }
            return visibles;
        }

        static IEnumerator Bug101i()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5, 1, 1);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia; ModoDios.Poner(true);
            var kes = d.UnidadA; var campanario = d.campanario;
            if (kes == null || campanario == null) { Fin("FALLO sin unidad A o sin campanario"); yield break; }
            foreach (var x in W9bHasta(() => d.OleadasDelMandoLanzadas >= 1, 40f)) yield return x;
            foreach (var x in Esperar(5f)) yield return x;
            int lej0, vis0 = W10EnemigosLejanosVisibles(out lej0);
            W8Ok(ref ok, !d.CampanarioOcupado && !WorldUiDirector.RevelarEnemigos, sb, $"sin el campanario: ocupado={d.CampanarioOcupado}, revelar={WorldUiDirector.RevelarEnemigos} (la niebla de guerra del minimapa NO esta activa en esta escena: iconos con niebla={lej0}/{vis0}, todos los enemigos se ven siempre)");
            // Kes sube por la rampa (orden de mover al centro de la plataforma).
            float h0 = Time.realtimeSinceStartup;
            OrderService.IssueMoveOrder(kes, campanario.position);
            foreach (var x in W9bHasta(() => d.CampanarioOcupado, 45f)) yield return x;
            float subio = Time.realtimeSinceStartup - h0;
            W8Ok(ref ok, d.CampanarioOcupado && kes.transform.position.y > 5.5f, sb, $"{kes.DisplayName} llego arriba en {subio:0.0} s (y={kes.transform.position.y:0.0}, ocupado={d.CampanarioOcupado})");
            foreach (var x in Esperar(1.0f)) yield return x;
            int lej1, vis1 = W10EnemigosLejanosVisibles(out lej1);
            W8Ok(ref ok, WorldUiDirector.RevelarEnemigos && d.CampanarioOcupado, sb, $"con el campanario ocupado WorldUiDirector.RevelarEnemigos={WorldUiDirector.RevelarEnemigos} (efecto visible solo si hay niebla: iconos con niebla={lej1}/{vis1})");
            W8Ok(ref ok, Mathf.Abs(kes.Brain.BonusDeAlcance - OperacionDirector.AlcanceExtraDelCampanario) < 0.01f, sb, $"bonus de alcance x{kes.Brain.BonusDeAlcance:0.00}");
            foreach (var x in CapturarPantalla(W10Prefijo + "campanario.png")) yield return x;
            // Baja: vuelve a A.
            OrderService.IssueMoveOrder(kes, d.sectoresDeDefensa[0].Centro);
            foreach (var x in W9bHasta(() => !d.CampanarioOcupado, 45f)) yield return x;
            foreach (var x in Esperar(0.6f)) yield return x;
            W8Ok(ref ok, !d.CampanarioOcupado && !WorldUiDirector.RevelarEnemigos && Mathf.Abs(kes.Brain.BonusDeAlcance - 1f) < 0.01f, sb, $"al bajar se apaga: ocupado={d.CampanarioOcupado}, revelar={WorldUiDirector.RevelarEnemigos}, bonus x{kes.Brain.BonusDeAlcance:0.00}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101j los nombres del tutorial se adaptan al operador
        static IEnumerator Bug101j()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            ModoDios.Poner(true);
            var doc = W10Aliado("Doc");
            drv.Brain.Possess(doc);
            foreach (var x in Esperar(0.5f)) yield return x;
            d.TeletransportarEscuadra(new Vector3(324.2f, 0f, 265.2f), Quaternion.identity);
            foreach (var x in Esperar(0.7f)) yield return x;
            OperacionTerminal.PruebaToqueE = true;
            foreach (var x in W9bHasta(() => MandoTactico.EnRadio, 3f)) yield return x;
            foreach (var x in Esperar(0.6f)) yield return x;
            var op = MandoTactico.Operador; var panel = PanelDeMando.Instancia;
            W8Ok(ref ok, op == doc && d.UnidadA != null && d.UnidadC != null && d.UnidadA != op && d.UnidadC != op && d.UnidadA != d.UnidadC, sb, $"operador={op.DisplayName}, unidades del tutorial A={d.UnidadA.DisplayName} C={d.UnidadC.DisplayName} (ninguna es el operador)");
            W8Ok(ref ok, panel.TextoActual.Contains(RolRequerido.NombreCorto(d.UnidadA)) && d.AnillosDeGuia == 1, sb, $"paso 1 nombra a {RolRequerido.NombreCorto(d.UnidadA)}: '{panel.TextoActual}'");
            W8Ok(ref ok, d.Milicianos.Count == MandoTactico.CantidadDeMilicianos && d.MedicoDeApoyo != null && d.MedicoDeApoyo != op, sb, $"medico de apoyo={(d.MedicoDeApoyo != null ? d.MedicoDeApoyo.DisplayName : "-")}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101pa / 101pb rendimiento: flujo viejo vs mando (Play fresco cada uno)
        // Mismo escenario: Resistir con modo dios, 70 s de pelea, se miden los frames desde los 10 s (cuando ya llegaron los enemigos). El editor domina el
        // costo (EditorLoop): lo que importa es la comparacion entre los dos flujos en las mismas condiciones, no el numero absoluto.
        static float w10Fps, w10Peor, w10P95; static int w10Vivos;
        static IEnumerable W10MedirFps(float desdeSeg, float hastaSeg)
        {
            var dts = new List<float>(); float t0 = Time.realtimeSinceStartup; w10Peor = 0f; w10Vivos = 0;
            while (Time.realtimeSinceStartup - t0 < hastaSeg)
            {
                if (Time.realtimeSinceStartup - t0 >= desdeSeg) { float dt = Time.unscaledDeltaTime; dts.Add(dt); w10Peor = Mathf.Max(w10Peor, dt); }
                int en = 0; foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.gameObject.activeInHierarchy && s.Health.IsAlive) en++;
                w10Vivos = Mathf.Max(w10Vivos, en);
                yield return null;
            }
            float suma = 0f; foreach (var v in dts) suma += v;
            w10Fps = dts.Count / Mathf.Max(0.001f, suma);
            dts.Sort(); w10P95 = dts.Count > 0 ? dts[Mathf.Min(dts.Count - 1, Mathf.FloorToInt(dts.Count * 0.95f))] : 0f;
        }

        static IEnumerator Bug101pa()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionDirector.ResistirSinMando = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            foreach (var x in W10MedirFps(10f, 70f)) yield return x;
            Fin(Inv($"OK flujo VIEJO (sin mando): {w10Fps:0.0} fps medios, p95 de frame {w10P95 * 1000f:0} ms, peor {w10Peor * 1000f:0} ms, maximo de enemigos vivos {w10Vivos}"));
        }

        static IEnumerator Bug101pb()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5, 1, 1);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            foreach (var x in W10MedirFps(10f, 70f)) yield return x;
            Fin(Inv($"OK flujo MANDO (WP10): {w10Fps:0.0} fps medios, p95 de frame {w10P95 * 1000f:0} ms, peor {w10Peor * 1000f:0} ms, maximo de enemigos vivos {w10Vivos}"));
        }

        // ---------------------------------------------------------------- 101f reglas puras
        static string Bug101f()
        {
            var sb = new StringBuilder(); bool ok = true;
            W8Ok(ref ok, MandoTactico.SegundosDeAnuncio == 8f && MandoTactico.SegundosParaCaer == 6f && MandoTactico.SegundosParaRecuperar == 4f && MandoTactico.AliadosParaRecuperar == 2 && MandoTactico.FactorDeDanoDelOperador == 0.5f, sb, "constantes: 8 s de aviso, 6 s para caer, 4 s y 2 aliados para recuperar, dano x0,5");
            W8Ok(ref ok, MandoTactico.RelojCorre(4, true, false, 0) && !MandoTactico.RelojCorre(3, true, false, 0) && !MandoTactico.RelojCorre(4, false, false, 0) && !MandoTactico.RelojCorre(4, true, true, 0) && !MandoTactico.RelojCorre(4, true, false, 1), sb, "reloj: solo con pasos 1-4, radio, ningun sector caido y ningun amenazado vacio");
            W8Ok(ref ok, MandoTactico.MotivoDePausa(4, true, false, 0) == "" && MandoTactico.MotivoDePausa(2, true, false, 0).StartsWith("UBICA") && MandoTactico.MotivoDePausa(4, false, false, 0).StartsWith("RADIO") && MandoTactico.MotivoDePausa(4, true, true, 0).StartsWith("SECTOR CAIDO") && MandoTactico.MotivoDePausa(4, true, false, 2).StartsWith("SECTOR AMENAZADO"), sb, "motivos de pausa");
            W8Ok(ref ok, !MandoTactico.DebeCaer(5.9f) && MandoTactico.DebeCaer(6f), sb, "cae a los 6 s");
            W8Ok(ref ok, MandoTactico.AliadosNecesarios(5) == 2 && MandoTactico.AliadosNecesarios(1) == 1 && MandoTactico.AliadosNecesarios(0) == 1, sb, "aliados necesarios: 2 (o los que queden)");
            W8Ok(ref ok, MandoTactico.SeRecupera(2, 4, 4f) && !MandoTactico.SeRecupera(1, 4, 9f) && !MandoTactico.SeRecupera(2, 4, 3.9f) && MandoTactico.SeRecupera(1, 1, 4f), sb, "se recupera con 2 aliados 4 s (con 1 vivo, basta 1)");
            W8Ok(ref ok, MandoTactico.TextoDeRecuperar().Contains(MandoTactico.AliadosParaRecuperar.ToString()) && MandoTactico.MotivoDePausa(4, true, true, 0).Contains(MandoTactico.AliadosParaRecuperar.ToString()), sb, "P7/#122: los textos de recuperar salen de la constante");
            W8Ok(ref ok, !MandoTactico.EnRadio && !MandoTactico.BloqueaElCambioDeVista() && !MandoTactico.BloqueaLaPosesion() && !CameraRig.RtsForzado, sb, "sin radio nada bloquea: Tab, posesion ni camara");
            return (ok ? "OK " : "FALLO ") + sb;
        }

        // ---------------------------------------------------------------- 101g el operador muere
        static IEnumerator Bug101g()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; bool listo = false;
            foreach (var x in W10TomarLaRadio(sb, v => listo = v)) yield return x;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            if (!listo) { Fin("FALLO no se pudo tomar la radio"); yield break; }
            var op = MandoTactico.Operador;
            W8Ok(ref ok, MandoTactico.EnRadio && !CameraRig.RtsForzado, sb, "P7: con la radio la vista tactica ya no es obligatoria");
            ModoDios.Poner(false);
            op.Health.TakeDamage(100000, -1);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, !op.Health.IsAlive && !MandoTactico.RadioTomada && !CameraRig.RtsForzado && !MandoTactico.BloqueaElCambioDeVista(), sb, $"operador muerto: radio tomada={MandoTactico.RadioTomada}, vista forzada={CameraRig.RtsForzado}, Tab bloqueado={MandoTactico.BloqueaElCambioDeVista()}");
            W8Ok(ref ok, !d.RelojDelMandoCorre, sb, $"el reloj queda detenido ({d.MotivoDeLaPausa})");
            ModoDios.Poner(true);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 101h sesion: capturar y restaurar
        static IEnumerator Bug101h()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var llegada = new float[3];
            w10Listo = false; foreach (var x in W10Ubicar(sb, llegada)) yield return x;
            ok = w10Listo;
            var d = OperacionDirector.Instancia; if (!ok) { Fin("FALLO " + sb); yield break; }
            foreach (var x in Esperar(6f)) yield return x;
            d.sectoresDeDefensa[1].Caido = true;
            var kv = new Dictionary<string, string>();
            d.CapturarEstado(kv);
            string rel = kv.ContainsKey("relojEfectivo") ? kv["relojEfectivo"] : "-";
            W8Ok(ref ok, kv.ContainsKey("relojEfectivo") && kv["radioTomada"] == "True" && kv["sectoresCaidos"] == "1" && kv.ContainsKey("pasosMando"), sb, $"capturado: relojEfectivo={rel} radioTomada={kv["radioTomada"]} sectoresCaidos={kv["sectoresCaidos"]} pasos={kv["pasosMando"]}");
            // Se vuelve a la llegada y se restaura.
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, !MandoTactico.RadioTomada && d.RelojEfectivo == 0f, sb, "antes de restaurar: sin radio, reloj 0");
            d.RestaurarEstado(kv);
            foreach (var x in Esperar(1.0f)) yield return x;
            float esperado = float.Parse(rel, System.Globalization.CultureInfo.InvariantCulture);
            // P10 (#120): si hay una oleada en curso, el reloj retrocede hasta antes de su aviso (se anuncia y sale de nuevo).
            if (kv.TryGetValue("relojOleada", out var ro) && float.TryParse(ro, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rw) && rw >= 0f) esperado = Mathf.Min(esperado, rw);
            W8Ok(ref ok, d.Fase == FaseOperacion.Resistir && MandoTactico.RadioTomada && d.Subfase == 1 && Mathf.Abs(d.RelojEfectivo - esperado) < 2f && d.sectoresDeDefensa[1].Caido, sb, $"restaurado: radio={MandoTactico.RadioTomada}, subfase={d.Subfase}, reloj efectivo={d.RelojEfectivo:0.0} (esperaba ~{esperado:0.0}), B caido={d.sectoresDeDefensa[1].Caido}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
