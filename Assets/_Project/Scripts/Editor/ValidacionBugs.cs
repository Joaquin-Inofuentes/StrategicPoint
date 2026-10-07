using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Validacion automatica de los bugs 8-21 + el nivel completo (pedido: "itera 20 veces si o si todo el nivel y sobre los bugs
    // para validar que todo este resuelto y aplicado"). Se corre en Play sobre SC_Operacion; cada iteracion:
    //   - recorre los 6 objetivos (salto + unos segundos de simulacion) contando errores de consola,
    //   - prueba cada bug con datos reales del juego (y teclado VIRTUAL para W / Espacio / U, sin tocar el teclado del usuario),
    //   - escribe Registros/Validacion/iter_NN.txt y una fila en Registros/Validacion/resumen.md, con capturas en las iteraciones pedidas.
    //
    //   unity cmd eval -- "SP.EditorTools.ValidacionBugs.Iniciar(1, true)"   y luego   "SP.EditorTools.ValidacionBugs.Estado"
    public static class ValidacionBugs
    {
        public static string Estado { get; private set; } = "inactivo";
        public static string UltimoInforme { get; private set; } = "";
        static IEnumerator rutina;
        static int iteracion;
        static bool conCapturas;
        static readonly List<string> lineas = new List<string>();
        static readonly List<(string nombre, bool ok, string detalle)> resultados = new List<(string, bool, string)>();
        static readonly List<string> errores = new List<string>();
        static Keyboard vkb;
        static Keyboard kbReal;
        static InputSettings.EditorInputBehaviorInPlayMode comportamientoPrevio;
        static InputSettings.BackgroundBehavior fondoPrevio;

        static string Carpeta => Path.Combine(SesionLog.Carpeta, "Validacion");
        static string Inv(FormattableString f) => f.ToString(CultureInfo.InvariantCulture);

        public static string Iniciar(int numero, bool capturas = false)
        {
            if (!Application.isPlaying) return "ERROR: no esta en Play";
            if (OperacionDirector.Instancia == null) return "ERROR: abri SC_Operacion";
            if (rutina != null) return "ERROR: ya corriendo";
            iteracion = numero; conCapturas = capturas;
            lineas.Clear(); resultados.Clear(); errores.Clear();
            Directory.CreateDirectory(Carpeta);
            Application.logMessageReceived += AlLog;
            rutina = Ejecutar();
            Estado = "corriendo";
            EditorApplication.update += Paso;
            return "iteracion " + numero + " iniciada";
        }

        static void AlLog(string msg, string stack, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception && t != LogType.Assert) return;
            if (msg.Contains("capture screen shot")) return;   // ScreenCapture del propio arnes
            if (errores.Count < 30) errores.Add(t + ": " + msg.Split('\n')[0]);
        }

        static void Paso()
        {
            bool sigue = false;
            try { sigue = rutina != null && rutina.MoveNext(); }
            catch (Exception e) { Log("EXCEPCION DEL ARNES: " + e); Debug.LogException(e); }
            if (sigue && Application.isPlaying) return;
            EditorApplication.update -= Paso;
            Application.logMessageReceived -= AlLog;
            QuitarTecladoVirtual();
            rutina = null;
            Escribir();
            Estado = "listo";
        }

        static void Log(string s) { lineas.Add(Inv($"[{Time.realtimeSinceStartup:0.0}] ") + s); }

        static void Resultado(string nombre, bool ok, string detalle)
        {
            resultados.Add((nombre, ok, detalle));
            Log((ok ? "OK    " : "FALLA ") + nombre + " | " + detalle);
        }

        static IEnumerable Esperar(float segundosReales)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < segundosReales) yield return null;
        }

        static Soldier Yo() => PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;

        static List<Soldier> Aliados(bool incluirYo = false)
        {
            var yo = Yo();
            return ActorRegistry.All.Where(s => s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.gameObject.activeInHierarchy
                && s.Health != null && s.Health.IsAlive && (incluirYo || s != yo)).ToList();
        }

        static float Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        static void Teletransportar(Soldier s, Vector3 p)
        {
            s.transform.position = new Vector3(p.x, s.transform.position.y, p.z);
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
        }

        // Saca de juego a los enemigos activos cerca (para las pruebas de curacion/rescate en calma) y los devuelve despues.
        static List<Soldier> ApartarEnemigos(Vector3 centro, float radio)
        {
            var l = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Enemy && s.gameObject.activeInHierarchy && Plano(s.transform.position, centro) < radio) { s.gameObject.SetActive(false); l.Add(s); }
            return l;
        }

        static void DevolverEnemigos(List<Soldier> l) { foreach (var s in l) if (s != null) s.gameObject.SetActive(true); }

        // ---- teclado virtual ----
        static void PonerTecladoVirtual()
        {
            if (vkb != null) return;
            kbReal = Keyboard.current;
            comportamientoPrevio = InputSystem.settings.editorInputBehaviorInPlayMode;
            fondoPrevio = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            vkb = InputSystem.AddDevice<Keyboard>("ValidacionKB");
            vkb.MakeCurrent();
        }

        static void Teclas(params Key[] k)
        {
            PonerTecladoVirtual();
            vkb.MakeCurrent();
            InputSystem.QueueStateEvent(vkb, new KeyboardState(k));
        }

        static void QuitarTecladoVirtual()
        {
            if (vkb == null) return;
            try { InputSystem.QueueStateEvent(vkb, new KeyboardState()); InputSystem.RemoveDevice(vkb); } catch { }
            vkb = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = comportamientoPrevio;
            InputSystem.settings.backgroundBehavior = fondoPrevio;
            if (kbReal != null && kbReal.added) kbReal.MakeCurrent();
        }

        static IEnumerable Pulsar(Key k, int cuadros = 3)
        {
            Teclas(k);
            for (int i = 0; i < cuadros; i++) yield return null;
            Teclas();
            yield return null;
        }

        static string Captura(string nombre)
        {
            if (!conCapturas) return "";
            string f = Path.Combine(Carpeta, Inv($"it{iteracion:00}_{nombre}.png"));
            try { ScreenCapture.CaptureScreenshot(f); } catch { }
            return Path.GetFileName(f);
        }

        // ---------------------------------------------------------------
        static IEnumerator Ejecutar()
        {
            var d = OperacionDirector.Instancia;
            Log(Inv($"=== ITERACION {iteracion} · {DateTime.Now:yyyy-MM-dd HH:mm:ss} · escena {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} ==="));
            foreach (var x in Esperar(2f)) yield return x;

            // 1) Nivel completo: los 6 objetivos.
            for (int o = 1; o <= OperacionDirector.TotalObjetivos; o++)
            {
                int errAntes = errores.Count;
                string r = OperacionPrueba.Arrancar(o);
                foreach (var x in Esperar(o == 4 || o == 5 ? 6f : 3.5f)) yield return x;
                bool reservaApuntada = false;
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Brain != null && s.Brain.CurrentTarget != null && !s.Brain.CurrentTarget.gameObject.activeInHierarchy && !s.Brain.CurrentTarget.Brain.MontadoEnVehiculo) reservaApuntada = true;
                bool faseOk = OperacionDirector.Indice(d.Fase) == o - 1 || (o == OperacionDirector.TotalObjetivos && d.Fase == FaseOperacion.Extraer);
                Resultado($"NIVEL objetivo {o}", faseOk && !reservaApuntada && errores.Count == errAntes,
                    Inv($"fase={d.Fase} reservaApuntada={reservaApuntada} erroresNuevos={errores.Count - errAntes} cazadores={CazaDeEnemigos.Cazadores} · {OperacionPrueba.Resumen()}"));
                if (o == 4 || o == 5) Captura("nivel_obj" + o);
            }

            // 2) Bugs 17/20/21: los enemigos se mueven y vienen a buscar a la escuadra.
            OperacionDirector.ResistirSinMando = true;   // WP10: bugs 17/20/21 miden el flujo viejo de Resistir (los enemigos vienen solos)
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            {
                var antes = new Dictionary<Soldier, (Vector3 pos, float dist)>();
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Team != TeamId.Enemy || !s.gameObject.activeInHierarchy || !s.Health.IsAlive) continue;
                    var al = ActorRegistry.FindNearestEnemyInRange(s.transform.position, TeamId.Enemy, 999f);
                    antes[s] = (s.transform.position, al != null ? Plano(al.transform.position, s.transform.position) : 999f);
                }
                foreach (var x in Esperar(8f)) yield return x;
                int movidos = 0, acercados = 0, peleando = 0, total = 0;
                foreach (var kv in antes)
                {
                    var s = kv.Key;
                    if (s == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    total++;
                    if (Plano(s.transform.position, kv.Value.pos) > 3f) movidos++;
                    var al = ActorRegistry.FindNearestEnemyInRange(s.transform.position, TeamId.Enemy, 999f);
                    float dd = al != null ? Plano(al.transform.position, s.transform.position) : 999f;
                    if (dd < kv.Value.dist - 3f) acercados++;
                    if (s.Brain != null && s.Brain.CurrentTarget != null) peleando++;
                }
                Resultado("BUG 17/20/21 enemigos vienen a atacar", total > 0 && CazaDeEnemigos.Cazadores > 0 && movidos + peleando >= Mathf.CeilToInt(total * 0.5f),
                    Inv($"enemigos={total} movidos={movidos} acercados={acercados} peleando={peleando} cazadores={CazaDeEnemigos.Cazadores} rumbos={CazaDeEnemigos.RumbosDados}"));
                Captura("bug21_cazadores");
            }

            // Para las pruebas de escuadra: zona de partida (objetivo 1), sin enemigos alrededor.
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var yo0 = Yo();
            var apartados = ApartarEnemigos(yo0 != null ? yo0.transform.position : Vector3.zero, 70f);
            foreach (var x in Esperar(1f)) yield return x;

            // 3) Minimapa: se ven los aliados.
            {
                var mm = SP.UI.MinimapFollow.Activo;
                var cam = mm != null ? mm.MinimapCamera : null;
                int con = 0, total = 0; float escalaMin = 999f;
                foreach (var s in Aliados(true))
                {
                    total++;
                    var ic = MinimapIcon.BuscarPorTarget(s.transform);
                    var r = ic != null ? ic.GetComponent<MeshRenderer>() : null;
                    if (ic != null && r != null && r.enabled && ic.gameObject.activeInHierarchy) { con++; escalaMin = Mathf.Min(escalaMin, ic.transform.localScale.x); }
                }
                float esperado = cam != null ? cam.orthographicSize * MinimapIcon.FraccionDelRadio * 2f * 0.9f : 0f;
                Resultado("BUG 15/minimapa aliados visibles", mm != null && cam != null && cam.enabled && total > 0 && con == total && escalaMin >= esperado,
                    Inv($"camara={(cam != null && cam.enabled)} ortho={(cam != null ? cam.orthographicSize : 0f):0} aliadosConIcono={con}/{total} escalaMin={escalaMin:0.0} esperado>={esperado:0.0}"));
            }

            // 4) Bug 8: curacion del jugador (te sigue, te cura, animacion, bloqueo con cartel, ESPACIO detiene).
            {
                var yo = Yo();
                var medico = PedidoDeCuracion.MedicoDisponible(yo);
                if (yo == null || medico == null) Resultado("BUG 8 curacion", false, "sin jugador o sin medico");
                else
                {
                    Teletransportar(medico, yo.transform.position + yo.transform.forward * 14f);
                    yo.Health.RestaurarVida(Mathf.RoundToInt(yo.Health.MaxHealth * 0.35f));
                    int vida0 = yo.Health.Current;
                    bool pedido = PedidoDeCuracion.Solicitar(yo);
                    float t0 = Time.realtimeSinceStartup;
                    bool curando = false, animo = false, cartel = false, quieto = true, cruces = false;
                    float distMin = 999f;
                    while (Time.realtimeSinceStartup - t0 < 30f && yo.Health.Current < yo.Health.MaxHealth)
                    {
                        distMin = Mathf.Min(distMin, Plano(medico.transform.position, yo.transform.position));
                        if (PedidoDeCuracion.CurandoAlJugador(yo))
                        {
                            if (!curando)
                            {
                                curando = true;
                                // intenta moverse: W durante unos cuadros -> no se mueve y aparece el cartel
                                var p0 = yo.transform.position;
                                Teclas(Key.W);
                                for (int i = 0; i < 12; i++) yield return null;
                                string aviso = AvisoCentral.TextoActual ?? "";
                                cartel = aviso.Contains("AGUARDE") && aviso.Contains("ESPACIO");
                                quieto = Plano(p0, yo.transform.position) < 0.25f;
                                Teclas();
                                Captura("bug08_curando_cartel");
                            }
                            animo |= CurandoAnimacion.Animando(medico);
                            cruces |= CurandoAnimacion.CrucesVivas(medico) > 0;
                        }
                        yield return null;
                    }
                    bool curado = yo.Health.Current >= yo.Health.MaxHealth * 0.97f;
                    Resultado("BUG 8 medico viene y cura al jugador", pedido && curando && curado && distMin < 3.5f,
                        Inv($"pedido={pedido} curando={curando} vida {vida0}->{yo.Health.Current}/{yo.Health.MaxHealth} distMin={distMin:0.0} t={Time.realtimeSinceStartup - t0:0.0}s"));
                    Resultado("BUG 8 animacion de curar", animo && cruces, Inv($"animando={animo} cruces={cruces}"));
                    Resultado("BUG 8 bloqueo + cartel AGUARDE", cartel && quieto, Inv($"cartel={cartel} noSeMovio={quieto}"));

                    // ESPACIO detiene
                    foreach (var x in Esperar(0.5f)) yield return x;
                    yo.Health.RestaurarVida(Mathf.RoundToInt(yo.Health.MaxHealth * 0.4f));
                    PedidoDeCuracion.Solicitar(yo);
                    float t1 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t1 < 15f && !PedidoDeCuracion.CurandoAlJugador(yo)) yield return null;
                    bool llego = PedidoDeCuracion.CurandoAlJugador(yo);
                    foreach (var x in Pulsar(Key.Space)) yield return x;
                    yield return null;
                    string av = AvisoCentral.TextoActual ?? "";
                    bool cortada = !PedidoDeCuracion.Activo;
                    foreach (var x in Esperar(2.5f)) yield return x;
                    bool sigueCortada = !(PedidoDeCuracion.Activo && PedidoDeCuracion.Herido == yo);
                    Resultado("BUG 8 ESPACIO detiene la curacion", llego && cortada && sigueCortada && av.Contains("DETENIDA"), Inv($"llego={llego} cortada={cortada} sigueCortada2.5s={sigueCortada} aviso=\"{av}\""));

                    // Orden SEGUIR al medico lo libera (no queda clavado curando).
                    PedidoDeCuracion.Solicitar(yo);
                    foreach (var x in Esperar(1f)) yield return x;
                    bool activoAntes = PedidoDeCuracion.Activo;
                    var enf = PedidoDeCuracion.Enfermero;
                    if (enf != null && enf.Brain != null) enf.Brain.IssueFollowOrder(yo);
                    yield return null;
                    Resultado("BUG 8 una orden nueva libera al medico", activoAntes && !PedidoDeCuracion.Activo, Inv($"activoAntes={activoAntes} activoDespues={PedidoDeCuracion.Activo}"));
                    yo.Health.RestaurarVida(yo.Health.MaxHealth);
                }
            }

            // 5) Bugs 10/14: aliado caido -> indicador + minimapa parpadeando; orden de reanimar lo revive.
            {
                var yo = Yo();
                var medico = PedidoDeCuracion.MedicoDisponible(yo);
                var victima = Aliados().FirstOrDefault(s => s != medico && s.Role != RoleType.Medic);
                if (victima == null || medico == null) Resultado("BUG 10/14 reanimar", false, "sin victima o sin medico");
                else
                {
                    Teletransportar(victima, yo.transform.position + yo.transform.right * 12f);
                    foreach (var x in Esperar(0.5f)) yield return x;
                    PedidoDeCuracion.AtencionAutomatica = false;   // que no lo levante solo antes de mirar el indicador
                    SP.Core.ComandosDeDepuracion.Matar(victima.Id);
                    foreach (var x in Esperar(1.2f)) yield return x;
                    var ind = IndicadorDeCaidos.Instancia;
                    bool marca = ind != null && ind.TieneMarca(victima);
                    var ic = MinimapIcon.BuscarPorTarget(victima.transform);
                    bool parpadea = ic != null && ic.CaidoParpadeando;
                    Captura("bug10_caido_indicador");
                    Resultado("BUG 10 indicador de caido + minimapa parpadea", marca && parpadea, Inv($"marca={marca} texto=\"{(ind != null ? ind.TextoDe(victima) : "")}\" iconoParpadea={parpadea}"));
                    PedidoDeCuracion.AtencionAutomatica = true;
                    bool orden = PedidoDeCuracion.SolicitarReanimar(victima);
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 35f && !victima.Health.IsAlive) yield return null;
                    yield return null; yield return null;
                    ic = MinimapIcon.BuscarPorTarget(victima.transform);
                    bool iconoNormal = ic != null && !ic.CaidoParpadeando && ic.GetComponent<MeshRenderer>().enabled;
                    Resultado("BUG 14 la orden de reanimar lo revive", orden && victima.Health.IsAlive, Inv($"orden={orden} vivo={victima.Health.IsAlive} t={Time.realtimeSinceStartup - t0:0.0}s"));
                    Resultado("BUG 15 icono vuelve al revivir", iconoNormal, Inv($"icono={(ic != null)} parpadea={(ic != null && ic.CaidoParpadeando)}"));
                }
            }

            // 6) Bug 12: el jugador cae y lo vienen a rescatar solos (sin enemigos cerca).
            {
                var yo = Yo();
                foreach (var a in Aliados()) Teletransportar(a, yo.transform.position + new Vector3(UnityEngine.Random.Range(-14f, 14f), 0f, UnityEngine.Random.Range(-14f, 14f)));
                foreach (var x in Esperar(1f)) yield return x;
                SP.Core.ComandosDeDepuracion.Matar(yo.Id);
                float t0 = Time.realtimeSinceStartup;
                bool alguien = false; string quien = "";
                while (Time.realtimeSinceStartup - t0 < 40f && !yo.Health.IsAlive)
                {
                    if (!alguien && (PedidoDeCuracion.Activo && PedidoDeCuracion.Herido == yo || RescateAutomatico.Activo && RescateAutomatico.Caido == yo))
                    { alguien = true; quien = PedidoDeCuracion.Enfermero != null ? PedidoDeCuracion.Enfermero.DisplayName : (RescateAutomatico.Rescatista != null ? RescateAutomatico.Rescatista.DisplayName : "?"); }
                    yield return null;
                }
                float tr = Time.realtimeSinceStartup - t0;
                foreach (var x in Esperar(2.5f)) yield return x;
                var drv = PlayerInputDriver.Activo;
                bool fps = drv != null && drv.Rig != null && drv.Rig.Mode == SP.CameraSystem.ControlMode.Fps && Yo() == yo;
                Captura("bug12_rescatado");
                Resultado("BUG 12 vienen a rescatar al jugador caido", alguien && yo.Health.IsAlive, Inv($"rescatista={quien} revivido={yo.Health.IsAlive} t={tr:0.0}s"));
                Resultado("BUG 12 al revivir vuelve a primera persona", fps, Inv($"modo={(drv != null && drv.Rig != null ? drv.Rig.Mode.ToString() : "?")} poseido={(Yo() != null ? Yo().DisplayName : "-")}"));
            }

            // 7) Bug 16: si te alejas (fuera del minimapa) los aliados vuelven si o si.
            {
                var yo = Yo();
                var aliados = Aliados();
                var destino = d.entradas != null && d.entradas.Length > 2 && d.entradas[2] != null ? d.entradas[2].position : yo.transform.position + yo.transform.forward * 70f;
                Teletransportar(yo, destino);
                foreach (var x in Esperar(0.5f)) yield return x;
                float dist0 = aliados.Count > 0 ? aliados.Average(a => Plano(a.transform.position, yo.transform.position)) : 0f;
                int regresos0 = aliados.Sum(a => a.Brain != null ? a.Brain.RegresosForzados : 0);
                foreach (var x in Esperar(22f)) yield return x;
                float dist1 = aliados.Count > 0 ? aliados.Average(a => Plano(a.transform.position, yo.transform.position)) : 0f;
                int regresos1 = aliados.Sum(a => a.Brain != null ? a.Brain.RegresosForzados : 0);
                Resultado("BUG 16 aliados lejanos vuelven si o si", dist0 > AiBrainRadio() && regresos1 > regresos0 && dist1 < dist0 - 25f,
                    Inv($"distancia media {dist0:0}m -> {dist1:0}m en 22s, regresos forzados +{regresos1 - regresos0}"));
                Captura("bug16_regreso");
            }
            DevolverEnemigos(apartados);

            // 8) Bug 11 + cursor interactivo / dialogo de reporte con pausa, miniatura y json completo ([U] real por teclado virtual).
            {
                int proximo = SesionLog.ProximoNumeroDeBug;
                string indicador = SesionLog.TextoIndicadorDeBug;
                Resultado("BUG 11 indicador de bug actual", indicador.Contains(Inv($"#{proximo:000}")), "indicador=\"" + indicador + "\"");
                foreach (var x in Pulsar(Key.U)) yield return x;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f && !(ReporteDeBug.Abierto && Time.timeScale == 0f)) yield return null;
                foreach (var x in Esperar(0.4f)) yield return x;
                bool abierto = ReporteDeBug.Abierto, pausa = Time.timeScale == 0f, mini = ReporteDeBug.CapturaPendiente != null && ReporteDeBug.CapturaPendiente.width > 0;
                // escribir "w" no tiene que mover al soldado ni abrir nada
                var yo = Yo(); var p0 = yo != null ? yo.transform.position : Vector3.zero;
                Teclas(Key.W); for (int i = 0; i < 6; i++) yield return null; Teclas();
                bool noSeMovio = yo == null || Plano(p0, yo.transform.position) < 0.05f;
                Captura("bug_dialogo");
                foreach (var x in Esperar(0.4f)) yield return x;
                int n = ReporteDeBug.Confirmar(Inv($"validacion automatica iteracion {iteracion}: dialogo + miniatura + diagnostico"));
                foreach (var x in Esperar(1f)) yield return x;
                string json = SesionLog.ArchivoDeBug(n);
                string txt = json != null && File.Exists(json) ? File.ReadAllText(json) : "";
                string png = json != null ? Path.ChangeExtension(json, ".png") : null;
                bool pngOk = png != null && File.Exists(png) && new FileInfo(png).Length > 5000;
                var est = txt.Length > 0 ? JsonUtility.FromJson<EstadoDeSesion>(txt) : null;
                bool completo = est != null && est.diagnostico.Count >= 12 && est.ultimasLineas.Count > 0 && est.nota.Contains("validacion")
                                && est.diagnostico.Any(l => l.StartsWith("CURACION")) && est.diagnostico.Any(l => l.StartsWith("MINIMAPA")) && est.diagnostico.Any(l => l.StartsWith("ENEMIGOS"));
                Resultado("BUG U dialogo pausa + miniatura", abierto && pausa && mini && noSeMovio, Inv($"abierto={abierto} timeScale0={pausa} miniatura={mini} teclaWnoMueve={noSeMovio}"));
                Resultado("BUG U guarda json completo + captura", n == proximo && completo && pngOk && Time.timeScale > 0f,
                    Inv($"bug #{n} (esperado #{proximo}) diagnostico={(est != null ? est.diagnostico.Count : 0)} lineas ultimasLineas={(est != null ? est.ultimasLineas.Count : 0)} png={pngOk} timeScaleDespues={Time.timeScale}"));
                // Cancelar no gasta numero
                ReporteDeBug.Abrir();
                foreach (var x in Esperar(0.5f)) yield return x;
                ReporteDeBug.Cancelar();
                yield return null;
                Resultado("BUG U cancelar no gasta numero", SesionLog.ProximoNumeroDeBug == proximo + 1 && Time.timeScale > 0f && !ReporteDeBug.Abierto, Inv($"proximo={SesionLog.ProximoNumeroDeBug}"));
            }

            // 9) Tanque: torreta sube/baja, cuerpo nivelado, metralleta real, sonidos reales.
            {
                Vehicle tanque = Vehicle.Todos.FirstOrDefault(v => v != null && (v.name == "Tanque_Fuga" || v.name == "Vehiculo_Blindado"));
                TurretWeapon canon = tanque != null ? tanque.GetComponentsInChildren<TurretWeapon>().FirstOrDefault(t => t.transform.parent != null && t.transform.parent.name == "TurretMount") : null;
                if (canon == null) Resultado("TANQUE torreta", false, "sin tanque/cañon");
                else
                {
                    var ai = canon.GetComponent<TurretAI>();
                    bool aiPrev = ai != null && ai.enabled; if (ai != null) ai.enabled = false;
                    var alto = canon.transform.position + tanque.transform.forward * 30f + Vector3.up * 14f;
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 2.5f) { canon.AimAt(alto, Time.deltaTime); yield return null; }
                    float pitchArriba = canon.PitchActual;
                    var cuerpo = canon.CuerpoNivelado;
                    float inclCuerpo = cuerpo != null ? Mathf.Abs(Mathf.DeltaAngle(0f, cuerpo.eulerAngles.x)) : 99f;
                    Captura("tanque_canon_arriba");
                    var bajo = canon.transform.position + tanque.transform.forward * 8f - Vector3.up * 3f;
                    t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 2.5f) { canon.AimAt(bajo, Time.deltaTime); yield return null; }
                    float pitchAbajo = canon.PitchActual;
                    canon.AddDesiredPitch(-200f);
                    float limite = canon.DesiredPitch;
                    if (ai != null) ai.enabled = aiPrev;
                    Resultado("TANQUE cañon sube y baja (torreta nivelada)", pitchArriba <= -10f && pitchAbajo >= 3f && inclCuerpo < 1f && Mathf.Approximately(limite, canon.MinPitch),
                        Inv($"pitchArriba={pitchArriba:0.0} pitchAbajo={pitchAbajo:0.0} cuerpoInclinado={inclCuerpo:0.0}° limiteArriba={limite:0}"));
                    var mg = tanque.transform.Find("MetralletaMount/MetralletaPivot/VisualMundo");
                    bool mgReal = mg != null && mg.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled && r.GetComponent<MeshFilter>().sharedMesh != null && r.GetComponent<MeshFilter>().sharedMesh.name.Contains("Metralleta"));
                    bool arte = tanque.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null && r.GetComponent<MeshFilter>().sharedMesh.name.Contains("Cuerpo"));
                    Resultado("TANQUE modelo real (orugas/torreta/metralleta)", mgReal && arte, Inv($"cuerpoReal={arte} metralletaReal={mgReal}"));
                    var vaf = tanque.GetComponent<VehicleAudioFeedback>();
                    var body = GenericSfx.Get(SfxKind.CannonBody); var orugas = GenericSfx.Get(SfxKind.Orugas);
                    vaf = tanque.GetComponentInChildren<VehicleAudioFeedback>();
                    bool sonidos = body != null && body.name.StartsWith("Canon_") && orugas != null && orugas.name.Contains("T26") && vaf != null && vaf.UsaGrabacionReal;
                    Resultado("TANQUE sonidos reales (orugas T-26 + cañonazo)", sonidos, Inv($"cañon={(body != null ? body.name : "-")} orugas={(orugas != null ? orugas.name : "-")} chirrido={(vaf != null && vaf.UsaGrabacionReal)}"));
                }
            }

            // 10) Final: subir al heli -> fundido que se retira, sonidos, victoria visible.
            {
                OperacionPrueba.Arrancar(6);
                foreach (var x in Esperar(1.5f)) yield return x;
                d.SubirAlHeli();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 16f && !(d.Fase == FaseOperacion.Victoria && !d.FundidoActivo)) yield return null;
                foreach (var x in Esperar(0.6f)) yield return x;
                bool sono = AudioDirector.SonoRecien("SeatChange", 60) || AudioDirector.Historial.Any(h => h.Contains("SeatChange"));
                bool fundidoFuera = GameObject.Find("OperacionFundido") == null;
                string cap = Captura("final_victoria");
                Resultado("FINAL fundido se retira (pantalla no queda apagada)", d.Fase == FaseOperacion.Victoria && !d.FundidoActivo && fundidoFuera,
                    Inv($"fase={d.Fase} fundidoActivo={d.FundidoActivo} canvasFundido={(fundidoFuera ? "destruido" : "SIGUE")} subidos={OperacionDirector.SubidosAlHeli} t={Time.realtimeSinceStartup - t0:0.0}s"));
                Resultado("FINAL suena la subida", sono || OperacionDirector.SubidosAlHeli > 0, Inv($"historialAudio={string.Join(",", AudioDirector.Historial.Skip(Math.Max(0, AudioDirector.Historial.Count - 8)))}"));
            }

            Log(Inv($"errores de consola en la iteracion: {errores.Count}"));
            foreach (var e in errores) Log("   " + e);
        }

        static float AiBrainRadio() => SP.Ai.AiBrain.RadioDeRegresoForzado;

        static void Escribir()
        {
            int ok = resultados.Count(r => r.ok);
            var sb = new StringBuilder();
            foreach (var l in lineas) sb.AppendLine(l);
            sb.AppendLine(Inv($"RESULTADO: {ok}/{resultados.Count} OK · errores de consola={errores.Count}"));
            UltimoInforme = sb.ToString();
            try
            {
                File.WriteAllText(Path.Combine(Carpeta, Inv($"iter_{iteracion:00}.txt")), UltimoInforme, new UTF8Encoding(false));
                string res = Path.Combine(Carpeta, "resumen.md");
                if (!File.Exists(res)) File.WriteAllText(res, "| Iteracion | Fecha | OK | Fallas | Errores consola |\n|---|---|---|---|---|\n", new UTF8Encoding(false));
                var fallas = string.Join("; ", resultados.Where(r => !r.ok).Select(r => r.nombre));
                File.AppendAllText(res, Inv($"| {iteracion} | {DateTime.Now:HH:mm:ss} | {ok}/{resultados.Count} | {(fallas.Length > 0 ? fallas : "-")} | {errores.Count} |\n"), new UTF8Encoding(false));
            }
            catch (Exception e) { Debug.LogWarning("[ValidacionBugs] " + e.Message); }
        }
    }
}
