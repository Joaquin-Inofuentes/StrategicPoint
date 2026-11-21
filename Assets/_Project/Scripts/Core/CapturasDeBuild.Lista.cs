using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.Core
{
    // Ronda 13, T0 (2026-11-16): lista ordenada de tomas del arnes "-capturas" y ayudantes comunes. Las tomas viven en CapturasDeBuild.<Grupo>.cs.
    // Todo lo que pertenece al juego NUEVO (SangreFx, AnilloDeImpactos, EngranajeDeRevivir, CartelVolver, R13_*...) se busca por NOMBRE o por reflexion:
    // este mismo arnes compila y corre sobre 95b80a95 (ANTES: lo que falta se reporta como 0) y sobre el codigo final (DESPUES).
    // Uso: StrategicPoint.exe -capturas <dir> [-tomas id,id,...] -screen-width 1600 -screen-height 900 -screen-fullscreen 0
    public sealed partial class CapturasDeBuild
    {
        string tomaActual = "global";
        HashSet<string> filtro;                         // null = todas
        readonly List<Action> finalizadores = new List<Action>();

        // ---------------------------------------------------------------- lista ordenada (plan 7.2). Cada entrada: nombre de grupo, ids de fotos que produce, limite de segundos.
        struct Grupo { public string nombre; public string[] ids; public float limite; public Func<IEnumerator> corre; public bool reiniciaEscena; }

        // Orden pensado por FASE del juego: saltar de Huir a Resistir deja al jugador sin control (la cinematica de la huida se corta con StopAllCoroutines),
        // asi que primero las tomas de las fases 1-2, despues las de la 5 (Resistir), despues las de la 4 (Huir) y la ciudad al final con la escena recargada.
        List<Grupo> Lista()
        {
            return new List<Grupo>
            {
                // fases 1 y 2
                new Grupo { nombre = "inicio",   ids = new[] { "inicio_cenital", "inicio_ojo", "inicio_hud", "porton1" }, limite = 60f, corre = Inicio },
                new Grupo { nombre = "reserva",  ids = new[] { "reserva_O", "reserva_E" }, limite = 60f, corre = Reservas },
                new Grupo { nombre = "revivir_40",    ids = new[] { "revivir_40" }, limite = 60f, corre = Revivir40 },
                new Grupo { nombre = "revivir_medico", ids = new[] { "revivir_medico" }, limite = 60f, corre = RevivirMedico },
                new Grupo { nombre = "cuchillo_soldado", ids = new[] { "cuchillo_soldado_0", "cuchillo_soldado_1" }, limite = 60f, corre = CuchilloSoldado },
                new Grupo { nombre = "cuchillo_pesado", ids = new[] { "cuchillo_pesado" }, limite = 60f, corre = CuchilloPesado },
                new Grupo { nombre = "fuentes",  ids = new[] { "fuentes_popups" }, limite = 60f, corre = FuentesPopups },
                new Grupo { nombre = "alejamiento", ids = new[] { "alejamiento" }, limite = 60f, corre = Alejamiento },
                new Grupo { nombre = "ajustes_dano", ids = new[] { "ajustes_dano_1280", "ajustes_dano_1600", "ajustes_dano_1920" }, limite = 60f, corre = AjustesDano },
                new Grupo { nombre = "sala",     ids = new[] { "sala_rts_juego", "sala_rts_cenital", "sala_rts_diagonal" }, limite = 60f, corre = Sala },
                // fase 5 (Resistir)
                new Grupo { nombre = "celdas",   ids = new[] { "celdas_0", "celdas_1", "celdas_2", "celdas_3" }, limite = 60f, corre = Celdas },
                new Grupo { nombre = "terminal", ids = new[] { "terminal_frente", "terminal_amplia", "terminal" }, limite = 60f, corre = Terminal },
                new Grupo { nombre = "compuerta", ids = new[] { "compuerta_bajada" }, limite = 60f, corre = CompuertaBajada },
                new Grupo { nombre = "plaza",    ids = new[] { "plaza_cenital", "plaza_ojo", "plaza_mobiliario", "ciudad_entrada" }, limite = 60f, corre = PlazaYCiudad },
                new Grupo { nombre = "criaturas", ids = new[] { "pesado_comparacion", "pesado_frente", "escudero_frente", "escudero_impacto", "escudero_impacto_lejos", "escudero_giro_0", "escudero_muerto" }, limite = 120f, corre = Criaturas },
                new Grupo { nombre = "escalera", ids = new[] { "escalera_bala_0", "escalera_bala_1", "escalera_bala_2", "escalera_explosion_0", "escalera_explosion_1", "escalera_explosion_2" }, limite = 90f, corre = Escalera },
                new Grupo { nombre = "heli",     ids = new[] { "heli_cohetes" }, limite = 260f, corre = HeliCohetes },
                // rendimiento: plaza (5), inicio (1) y la ruta (4) en ese orden
                new Grupo { nombre = "perf",     ids = new[] { "perf_plaza", "perf_inicio", "perf_ruta" }, limite = 90f, corre = PerfTodas },
                // fase 4 (Huir)
                new Grupo { nombre = "salida",   ids = new[] { "salida_norte_cenital", "salida_norte_ojo", "patio_tanque" }, limite = 60f, corre = SalidaNorteYPatio },
                new Grupo { nombre = "camion",   ids = new[] { "camion_patio_cerca" }, limite = 60f, corre = CamionPatio },
                new Grupo { nombre = "desierto", ids = new[] { "desierto_1", "desierto_2", "desierto_3", "desierto_rodante" }, limite = 60f, corre = Desierto },
                new Grupo { nombre = "huida",    ids = new[] { "retroceso_huida" }, limite = 60f, corre = RetrocesoHuida },
                new Grupo { nombre = "r15", ids = IdsR15, limite = 150f, corre = R15 },
                new Grupo { nombre = "subida_real", ids = IdsSubida, limite = 200f, corre = SubidaReal },
                new Grupo { nombre = "r17_combate", ids = IdsR17Combate, limite = 150f, corre = R17Combate },
                new Grupo { nombre = "r17_centro", ids = IdsR17Centro, limite = 150f, corre = R17Centro },
                new Grupo { nombre = "r17_mundo", ids = IdsR17Mundo, limite = 180f, corre = R17Mundo },
                new Grupo { nombre = "reportes17", ids = IdsReportes17(), limite = 120f, corre = Reportes17 },
                new Grupo { nombre = "tanque",   ids = new[] { "tanque_mira_inicial", "tanque_hud_barra", "tanque_impacto_izq", "tanque_impacto_atras" }, limite = 60f, corre = Tanque },
                // la ciudad a x4 deja el juego en cualquier estado: escena recargada antes y de ultima
                new Grupo { nombre = "ciudad",   ids = new[] { "perros_manada", "ciudad_vista", "ciudad_perro" }, limite = 120f, corre = Ciudad, reiniciaEscena = true },
            };
        }

        bool Quiero(string id)
        {
            if (filtro == null) return true;
            return filtro.Contains(id) || filtro.Contains(tomaActual);
        }

        bool QuieroGrupo(Grupo g)
        {
            if (filtro == null) return true;
            if (filtro.Contains(g.nombre)) return true;
            foreach (var i in g.ids) if (filtro.Contains(i)) return true;
            return false;
        }

        IEnumerator Todo()
        {
            float inicio = Time.realtimeSinceStartup;
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            var a = Environment.GetCommandLineArgs(); int it = Array.IndexOf(a, "-tomas");
            if (it >= 0 && it + 1 < a.Length) { filtro = new HashSet<string>(a[it + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); }
            UnityEngine.Random.InitState(1313);
            Application.runInBackground = true;
            try { UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus; } catch (Exception) { }
            var difPrevia = Dificultad.Actual;                                   // la dificultad vive en PlayerPrefs: se restaura al final
            Dificultad.Actual = NivelDificultad.Medio;
            Dato("version=" + Application.version + " resolucion=" + Screen.width + "x" + Screen.height + " dificultad=" + Dificultad.Actual + " semilla=1313 filtro=" + (filtro == null ? "(todas)" : string.Join(",", filtro)));
            yield return Espera(2f);
            SceneLoader.Cargar("SC_Operacion");
            while (OperacionDirector.Instancia == null && Time.realtimeSinceStartup - inicio < 90f) yield return null;
            yield return Espera(4f);
            PartidaGuardada.ModoPrueba = true; ModoDios.Poner(true);
            CinematicaDeOperacion.Saltar(); CinematicaDeRapel.Saltar();
            PrepararMolde(); Dato("dato global.molde_fijo=" + (moldeFijo != null));
            Dato("dato global.escena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            Dato("dato global.duplicados_apagados=" + LimpiezaDeDuplicados.Apagados);
            Dato("dato global.colliders_apagados=" + LimpiezaDeDuplicados.CollidersApagados);
            Dato("dato global.titulos_ocultos=" + TitulosDeObjetosOcultos.Ocultos);
            Volcar();
            foreach (var g in Lista())
            {
                if (!QuieroGrupo(g)) continue;
                tomaActual = g.nombre;
                if (g.reiniciaEscena) yield return ReiniciarEscena();
                yield return Seguro(g.nombre, g.corre(), g.limite);
                tomaActual = "global";
            }
            Dificultad.Actual = difPrevia;
            Dato("dato global.duplicados_apagados_final=" + LimpiezaDeDuplicados.Apagados + " colliders_apagados_final=" + LimpiezaDeDuplicados.CollidersApagados + " titulos_ocultos_final=" + TitulosDeObjetosOcultos.Ocultos);
            Dato("FIN " + F(Time.realtimeSinceStartup - inicio) + " s");
            Volcar();
            Application.Quit();
        }

        static string EstadoDelJuego()
        {
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo; var yo = Yo();
            int vivosAliados = 0, enem = 0; foreach (var a in ActorRegistry.All) if (a != null && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy) { if (a.Team == TeamId.Player) vivosAliados++; else enem++; }
            return "director=" + (d != null) + " fase=" + (d != null ? d.Fase.ToString() : "-") + " sub=" + (d != null ? d.Subfase.ToString() : "-") + " driver=" + (drv != null) + " yo=" + (yo != null ? yo.name + (yo.Health.IsAlive ? "(vivo)" : "(muerto)") : "null") + " aliados_vivos=" + vivosAliados + " enemigos_vivos=" + enem + " escala=" + Time.timeScale.ToString("0.0", Ci) + " dios=" + ModoDios.Activo;
        }

        IEnumerator ReiniciarEscena()
        {
            Dato("-- escena recargada antes de " + tomaActual);
            moldeFijo = null;
            SceneLoader.Cargar("SC_Operacion");
            yield return Espera(2f);
            float t0 = Time.realtimeSinceStartup;
            while (OperacionDirector.Instancia == null && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            yield return Espera(4f);
            PartidaGuardada.ModoPrueba = true; ModoDios.Poner(true);
            CinematicaDeOperacion.Saltar(); CinematicaDeRapel.Saltar();
            PrepararMolde(); Volcar();
        }

        void Volcar() { try { File.WriteAllText(Path.Combine(dir, "captura_info.txt"), info.ToString()); } catch (Exception) { } }
        // Ejecuta una toma sin que un error corte las demas: lo anota y sigue. Limite de tiempo por toma (R13 T0) y finalizadores (SinDios, ConDificultad...).
        IEnumerator Seguro(string nombre, IEnumerator it, float limite = 60f)
        {
            tomaActual = nombre; Dato("== " + nombre); Dato("estado al empezar: " + EstadoDelJuego());
            float t0 = Time.realtimeSinceStartup; bool error = false;
            while (true)
            {
                bool hay = false;
                try { hay = it.MoveNext(); }
                catch (Exception e) { Dato("ERROR en " + nombre + ": " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace); error = true; }
                if (error || !hay) break;
                if (Time.realtimeSinceStartup - t0 > limite) { Dato("ERROR en " + nombre + ": TIEMPO (mas de " + F(limite) + " s)"); break; }
                yield return it.Current;
            }
            Finalizar();
            { var dd = OperacionDirector.Instancia; float tc = Time.realtimeSinceStartup; while (dd != null && dd.CineDeMansionEnCurso && Time.realtimeSinceStartup - tc < 12f) yield return null; }   // no dejar una cinematica a medias: el siguiente salto la corta
            Dato("estado al terminar: " + EstadoDelJuego());
            Dato("dato " + nombre + ".segundos=" + F(Time.realtimeSinceStartup - t0));
            Volcar();
        }


        void Finalizar()
        {
            for (int i = finalizadores.Count - 1; i >= 0; i--) { try { finalizadores[i](); } catch (Exception e) { Dato("aviso al finalizar: " + e.Message); } }
            finalizadores.Clear();
            Time.timeScale = 1f;
        }

        // ---------------------------------------------------------------- datos parseables
        void D(string clave, object valor)
        {
            string s = valor is float f ? F(f) : valor is double dd ? F((float)dd) : valor is bool b ? (b ? "true" : "false") : Convert.ToString(valor, Ci);
            Dato("dato " + tomaActual + "." + clave + "=" + s);
        }
        void D(string toma, string clave, object valor) { string t = tomaActual; tomaActual = toma; D(clave, valor); tomaActual = t; }

        // ---------------------------------------------------------------- SinDios / ConDificultad
        // Modo dios apagado (y su aceleracion de tiempo) con el jugador invulnerable: lo que se mide de dano no queda triplicado.
        void SinDios()
        {
            bool dios = ModoDios.Activo, vel = ModoDios.VelocidadActiva;
            var yo = Yo(); bool inv = yo != null && yo.Health != null && yo.Health.Invulnerable;
            ModoDios.VelocidadActiva = false; ModoDios.Poner(false); Time.timeScale = 1f;
            if (yo != null && yo.Health != null) yo.Health.Invulnerable = true;
            finalizadores.Add(() =>
            {
                var y2 = Yo(); if (y2 != null && y2.Health != null) y2.Health.Invulnerable = inv && y2 == yo;
                if (yo != null && yo.Health != null) yo.Health.Invulnerable = inv;
                ModoDios.VelocidadActiva = vel; ModoDios.Poner(dios); Time.timeScale = 1f;
            });
        }

        void ConDificultad(NivelDificultad n)
        {
            var prev = Dificultad.Actual; Dificultad.Actual = n;
            finalizadores.Add(() => { Dificultad.Actual = prev; });
        }

        // ---------------------------------------------------------------- contar / buscar por nombre
        static List<Transform> TodosLosTransforms(bool incluirInactivos)
        {
            var l = new List<Transform>();
            foreach (var t in FindObjectsByType<Transform>(incluirInactivos ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t != null) l.Add(t);
            return l;
        }

        // Cuantos objetos ACTIVOS tienen un nombre que empieza con alguno de los prefijos (o lo contiene, si el prefijo empieza con '*').
        static int ContarPorNombre(params string[] prefijos) => ContarPorNombre(null, 0f, prefijos);
        static int ContarPorNombre(Vector3? centro, float radio, params string[] prefijos)
        {
            int n = 0;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                if (centro.HasValue) { var d = t.position - centro.Value; d.y = 0f; if (d.magnitude > radio) continue; }
                foreach (var p in prefijos)
                {
                    bool ok = p.Length > 0 && p[0] == '*' ? t.name.IndexOf(p.Substring(1), StringComparison.OrdinalIgnoreCase) >= 0 : t.name.StartsWith(p, StringComparison.OrdinalIgnoreCase);
                    if (ok) { n++; break; }
                }
            }
            return n;
        }

        static bool ExisteActivo(string nombre)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t != null && t.name == nombre && t.gameObject.activeInHierarchy) return true;
            return false;
        }

        static Transform BuscarPorNombre(string nombre, bool inactivos = false)
        {
            foreach (var t in FindObjectsByType<Transform>(inactivos ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (t != null && t.name == nombre) return t;
            return null;
        }

        // Textos activos (UI legacy y TextMesh) cuyo contenido contiene alguno de los fragmentos.
        static List<string> TextosActivos(params string[] fragmentos)
        {
            var l = new List<string>();
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                foreach (var f in fragmentos) if (t.text.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) { l.Add(t.text.Replace("\n", " / ")); break; }
            }
            foreach (var t in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                foreach (var f in fragmentos) if (t.text.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) { l.Add(t.text.Replace("\n", " / ")); break; }
            }
            return l;
        }

        // Textos activos que NO usan una fuente belica (BlackOpsOne / StardosStencil): lo que el plan T22 deja en 0.
        static int FuentesNoBelicas()
        {
            int n = 0;
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text) || t.color.a < 0.05f) continue;
                if (!EsFuenteBelica(t.font)) n++;
            }
            foreach (var t in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
                if (!EsFuenteBelica(t.font)) n++;
            }
            return n;
        }
        static bool EsFuenteBelica(Font f) => f != null && (f.name.IndexOf("BlackOps", StringComparison.OrdinalIgnoreCase) >= 0 || f.name.IndexOf("Stardos", StringComparison.OrdinalIgnoreCase) >= 0);

        // ---------------------------------------------------------------- reflexion (todo lo nuevo se busca asi)
        const BindingFlags Todos = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        static readonly Dictionary<string, Type> cacheTipos = new Dictionary<string, Type>();
        static Type Tipo(string nombre)
        {
            if (cacheTipos.TryGetValue(nombre, out var t)) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] ts; try { ts = asm.GetTypes(); } catch (Exception) { continue; }
                foreach (var x in ts) if (x.Name == nombre || x.FullName == nombre) { cacheTipos[nombre] = x; return x; }
            }
            cacheTipos[nombre] = null; return null;
        }

        // Campo o propiedad (instancia o estatica) por nombre. 'o' puede ser una instancia o un Type.
        static object Leer(object o, string nombre)
        {
            if (o == null) return null;
            var tipo = o as Type ?? o.GetType(); object inst = o is Type ? null : o;
            for (var t = tipo; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(nombre, Todos | BindingFlags.DeclaredOnly); if (p != null && p.GetIndexParameters().Length == 0) { try { var gm = p.GetGetMethod(true); if (gm == null) return null; return p.GetValue(gm.IsStatic ? null : inst); } catch (Exception) { return null; } }
                var f = t.GetField(nombre, Todos | BindingFlags.DeclaredOnly); if (f != null) { try { return f.GetValue(f.IsStatic ? null : inst); } catch (Exception) { return null; } }
            }
            return null;
        }
        static bool Escribir(object o, string nombre, object valor)
        {
            if (o == null) return false;
            var tipo = o as Type ?? o.GetType(); object inst = o is Type ? null : o;
            for (var t = tipo; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(nombre, Todos | BindingFlags.DeclaredOnly); if (p != null && p.CanWrite) { try { p.SetValue(inst, valor); return true; } catch (Exception) { return false; } }
                var f = t.GetField(nombre, Todos | BindingFlags.DeclaredOnly); if (f != null) { try { f.SetValue(inst, valor); return true; } catch (Exception) { return false; } }
            }
            return false;
        }
        static bool Existe(object o, string nombre)
        {
            if (o == null) return false;
            var tipo = o as Type ?? o.GetType();
            for (var t = tipo; t != null; t = t.BaseType) if (t.GetProperty(nombre, Todos | BindingFlags.DeclaredOnly) != null || t.GetField(nombre, Todos | BindingFlags.DeclaredOnly) != null) return true;
            return false;
        }
        // Llama a un metodo por nombre (el primero con esa cantidad de argumentos). Devuelve true si lo encontro; 'resultado' trae el valor.
        static bool Llamar(object o, string metodo, out object resultado, params object[] args)
        {
            resultado = null; if (o == null) return false;
            var tipo = o as Type ?? o.GetType(); object inst = o is Type ? null : o;
            for (var t = tipo; t != null; t = t.BaseType)
                foreach (var m in t.GetMethods(Todos | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != metodo) continue;
                    var ps = m.GetParameters(); if (ps.Length < args.Length) continue;
                    var real = new object[ps.Length]; bool ok = true;
                    for (int i = 0; i < ps.Length; i++) { if (i < args.Length) real[i] = args[i]; else if (ps[i].HasDefaultValue) real[i] = ps[i].DefaultValue; else { ok = false; break; } }
                    if (!ok) continue;
                    try { resultado = m.Invoke(m.IsStatic ? null : inst, real); return true; } catch (Exception) { return false; }
                }
            return false;
        }
        static bool Llamar(object o, string metodo, params object[] args) => Llamar(o, metodo, out _, args);
        static UnityEngine.Object BuscarUno(string tipo, bool inactivos = false)
        {
            var t = Tipo(tipo); if (t == null) return null;
            return FindFirstObjectByType(t, inactivos ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
        }

        // ---------------------------------------------------------------- Perf: promedio y percentiles de unscaledDeltaTime
        struct ResPerf { public int n; public float fps, p95ms, p99ms, maxms; }
        ResPerf ultimoPerf;

        IEnumerator Perf(float segundos, string id = null)
        {
            var dts = new List<float>(); float t0 = Time.realtimeSinceStartup;
            yield return null; yield return null;   // el primer cuadro tras mover la camara no vale
            while (Time.realtimeSinceStartup - t0 < segundos) { dts.Add(Time.unscaledDeltaTime); yield return null; }
            ultimoPerf = CalcPerf(dts);
            DatosPerf(id ?? tomaActual);
        }
        static ResPerf CalcPerf(List<float> dts)
        {
            var r = new ResPerf(); if (dts.Count == 0) return r;
            var s = new List<float>(dts); s.Sort(); double suma = 0; foreach (var d in dts) suma += d;
            r.n = dts.Count; r.fps = (float)(dts.Count / suma);
            r.p95ms = s[Mathf.Clamp((int)(s.Count * 0.95f), 0, s.Count - 1)] * 1000f; r.p99ms = s[Mathf.Clamp((int)(s.Count * 0.99f), 0, s.Count - 1)] * 1000f; r.maxms = s[s.Count - 1] * 1000f;
            return r;
        }
        void DatosPerf(string id)
        {
            D(id, "fps_prom", ultimoPerf.fps); D(id, "p95_ms", ultimoPerf.p95ms); D(id, "p99_ms", ultimoPerf.p99ms); D(id, "max_ms", ultimoPerf.maxms); D(id, "cuadros", ultimoPerf.n);
        }

        // ---------------------------------------------------------------- Resolucion
        IEnumerator Resolucion(int w, int h)
        {
            Screen.SetResolution(w, h, false);
            yield return Espera(1f);
        }

        // Camara propia que dibuja a pantalla (depth 100) con las demas camaras de pantalla apagadas: devuelve la funcion que lo deshace.
        Camera CamaraDePantalla(string nombre)
        {
            var apag = new List<Camera>();
            foreach (var c in Camera.allCameras) if (c != null && c.enabled && c.targetTexture == null) { c.enabled = false; apag.Add(c); }
            var go = new GameObject(nombre); var cam = go.AddComponent<Camera>(); cam.depth = 100f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 900f; cam.clearFlags = CameraClearFlags.Skybox;
            finalizadores.Add(() => { foreach (var c in apag) if (c != null) c.enabled = true; if (go != null) Destroy(go); });
            return cam;
        }

        // ---------------------------------------------------------------- varios
        static Soldier AliadoNoMedico(Soldier excepto)
        {
            foreach (var a in ActorRegistry.All)
                if (a != null && a != excepto && a.Team == TeamId.Player && a.Role != RoleType.Civilian && a.Role != RoleType.Medic && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy) return a;
            return null;
        }

        // El resto de la escuadra queda quieta: sin IA que revive, cura o dispara durante la medicion.
        static void CongelarCerebros(params Soldier[] salvo)
        {
            foreach (var a in ActorRegistry.All)
            {
                if (a == null || a.Brain == null) continue;
                bool exc = false; foreach (var s in salvo) if (a == s) exc = true;
                if (!exc) a.Brain.Anulado = true;
            }
        }

        // Teclado: mantener una tecla (Interactuar = E) con un dispositivo sintetico. Sin teclado real en la build (o con foco perdido) no hay otra forma.
        static UnityEngine.InputSystem.Keyboard tecladoVirtual;
        static UnityEngine.InputSystem.Keyboard TecladoParaProbar()
        {
            // un teclado virtual propio: no depende de que la ventana tenga el foco (se pone IgnoreFocus al arrancar)
            if (tecladoVirtual == null || !tecladoVirtual.added) tecladoVirtual = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("R13Teclado");
            return tecladoVirtual;
        }
        static void TeclaE(bool apretada)
        {
            var kb = TecladoParaProbar(); if (kb == null) return;
            if (apretada) UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
            else UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        }

        // Espera hasta que 'cond' sea cierta o pasen 'max' segundos (tiempo real). Devuelve por 'ok'.
        bool esperaOk;
        IEnumerator EsperarHasta(Func<bool> cond, float max)
        {
            esperaOk = false; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < max) { bool c = false; try { c = cond(); } catch (Exception) { } if (c) { esperaOk = true; yield break; } yield return null; }
        }
    }
}
