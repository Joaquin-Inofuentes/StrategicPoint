using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Escena en la que corre un check (por defecto SC_Operacion). Lo lee la maquina de fases de ChecksBugs065.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class EscenaDelCheckAttribute : Attribute
    {
        public readonly string Escena;
        public EscenaDelCheckAttribute(string escena) { Escena = escena; }
    }

    // Checks de las tandas de bugs #065-#101 y #104-#132 (Operacion Cuartel y SC_Gameplay/SC_Loading). La escena de cada check es SC_Operacion
    // salvo que el metodo lleve [EscenaDelCheck("SC_Gameplay")] (P0 de la tanda #104-#132). Cada paquete agrega los suyos como metodos estaticos con el nombre
    // Bug0NN (tres digitos), SIN parametros, en cualquier parte de esta clase (puede ser un archivo partial nuevo):
    //
    //   static string Bug0NN()        sincrono: devuelve "OK ..." o "FALLO ..."
    //   static IEnumerator Bug0NN()   con esperas (frames/segundos): al terminar llama a Fin("OK ...") o Fin("FALLO ...")
    // Un bug puede tener varios checks con sufijo de una letra (Bug065m, Bug072a...): Correr(65) los corre todos, en orden alfabetico.
    //
    // Se registran solos por reflexion. Uso desde la terminal (editor en Play sobre SC_Operacion cuando el check lo necesite):
    //   return SP.EditorTools.ChecksBugs065.Correr(80);        // un bug en el Play ACTUAL (si es asincrono devuelve "EN CURSO": sondear Estado/Resultado)
    //   return SP.EditorTools.ChecksBugs065.CorrerTodos();     // 0..132, CADA CHECK EN UN PLAY FRESCO (ver abajo); asincrono: sondear Estado, leer Informe
    //   return SP.EditorTools.ChecksBugs065.CorrerLista("Bug079,Bug085");   // esos checks, cada uno en un Play fresco
    //   return SP.EditorTools.ChecksBugs065.Estado;            // "inactivo" | "corriendo" | "listo"
    //   return SP.EditorTools.ChecksBugs065.Informe;           // una linea por check (tambien Temp/checks_progreso.txt, que sobrevive a un cuelgue)
    // El numero 0 es el smoke test de la columna de la Operacion (WP0).
    //
    // AISLAMIENTO (WP11): la pasada encadenada fallaba por contaminacion de estado (muertos, escuadra en el helicoptero, objetivo 2 sin armar...).
    // Ahora CorrerTodos/CorrerLista salen de Play y vuelven a entrar entre un check y el siguiente (escena recargada desde disco, estaticos del
    // juego reiniciados). Como entrar en Play recarga el dominio, el estado de la pasada vive en SessionState y una maquina de fases (Paso) se
    // reengancha sola despues de cada recarga ([InitializeOnLoadMethod]).
    public static partial class ChecksBugs065
    {
        const string KP = "SPChecks.";

        // Persistentes (sobreviven a la recarga de dominio dentro de la sesion del editor).
        public static string Estado { get => SessionState.GetString(KP + "estado", "inactivo"); private set => SessionState.SetString(KP + "estado", value); }
        public static string Informe { get => SessionState.GetString(KP + "informe", ""); private set => SessionState.SetString(KP + "informe", value); }
        public static string Resultado { get => SessionState.GetString(KP + "resultado", ""); private set => SessionState.SetString(KP + "resultado", value); }
        static string Fase { get => SessionState.GetString(KP + "fase", ""); set => SessionState.SetString(KP + "fase", value); }
        static int Indice { get => SessionState.GetInt(KP + "idx", 0); set => SessionState.SetInt(KP + "idx", value); }
        static bool Aislar { get => SessionState.GetBool(KP + "aislar", false); set => SessionState.SetBool(KP + "aislar", value); }
        static long Hasta { get => long.Parse(SessionState.GetString(KP + "hasta", "0"), CultureInfo.InvariantCulture); set => SessionState.SetString(KP + "hasta", value.ToString(CultureInfo.InvariantCulture)); }
        static string[] ColaGuardada { get => SessionState.GetString(KP + "cola", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries); set => SessionState.SetString(KP + "cola", string.Join("\n", value)); }
        const string ArchivoProgreso = "Temp/checks_progreso.txt";

        static Dictionary<int, List<MethodInfo>> checks;
        static readonly Regex Patron = new Regex(@"^Bug(\d{3})([a-z]?)$", RegexOptions.Compiled);
        static IEnumerator actual;          // check asincrono en curso (no sobrevive a una recarga: la recarga solo ocurre ENTRE checks)
        static int bugActual;
        static MethodInfo metodoActual;
        static string salida;
        static int erroresDeConsola;
        static bool escuchando;

        static void Registrar()
        {
            checks = new Dictionary<int, List<MethodInfo>>();
            foreach (var m in typeof(ChecksBugs065).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var mt = Patron.Match(m.Name);
                if (!mt.Success || m.GetParameters().Length != 0) continue;
                if (m.ReturnType != typeof(string) && m.ReturnType != typeof(IEnumerator)) continue;
                int n = int.Parse(mt.Groups[1].Value, CultureInfo.InvariantCulture);
                if (!checks.TryGetValue(n, out var lista)) checks[n] = lista = new List<MethodInfo>();
                lista.Add(m);
            }
            foreach (var l in checks.Values) l.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        // Numeros de bug con check registrado (para que los paquetes verifiquen que su metodo se encontro).
        public static int[] Registrados()
        {
            Registrar();
            var l = new List<int>(checks.Keys); l.Sort();
            return l.ToArray();
        }

        // Los checks asincronos cierran con esto.
        public static void Fin(string texto) { salida = texto; }

        static string Inv(FormattableString f) => f.ToString(CultureInfo.InvariantCulture);

        public static IEnumerable Esperar(float segundosReales)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < segundosReales) yield return null;
        }

        static void AlLog(string msg, string stack, LogType t)
        {
            if ((t == LogType.Error || t == LogType.Exception || t == LogType.Assert) && !msg.Contains("capture screen shot")) erroresDeConsola++;
        }

        static void Escuchar(bool si)
        {
            if (si && !escuchando) { Application.logMessageReceived += AlLog; escuchando = true; }
            else if (!si && escuchando) { Application.logMessageReceived -= AlLog; escuchando = false; }
        }

        // Despues de una recarga de dominio (entrar en Play, compilar) la pasada en curso se reengancha sola.
        [InitializeOnLoadMethod]
        static void Reanudar()
        {
            if (SessionState.GetString(KP + "estado", "inactivo") != "corriendo") return;
            escuchando = false;
            EditorApplication.update -= Paso;
            EditorApplication.update += Paso;
        }

        public static string Correr(int bug)
        {
            if (Estado == "corriendo") return "ERROR: ya hay una corrida en curso (Estado=corriendo)";
            Registrar();
            if (!checks.TryGetValue(bug, out var ms)) return $"SIN CHECK para el bug {bug:000}";
            SP.Core.PartidaGuardada.ModoPrueba = true;   // P10
            bool todoSincrono = true;
            foreach (var m in ms) if (m.ReturnType != typeof(string)) todoSincrono = false;
            if (todoSincrono)
            {
                Escuchar(true);
                var sb = new StringBuilder();
                foreach (var m in ms)
                {
                    erroresDeConsola = 0;
                    string r;
                    try { r = (string)m.Invoke(null, null); }
                    catch (Exception e) { r = "FALLO excepcion: " + (e.InnerException ?? e).Message; }
                    if (sb.Length > 0) sb.Append(Environment.NewLine);
                    sb.Append(Linea(bug, r, m));
                }
                Escuchar(false);
                Resultado = sb.ToString();
                Informe = Resultado;
                return ms.Count == 1 ? Resultado.Substring(Resultado.IndexOf(' ') + 1) : Resultado;
            }
            var lista = new List<(int, MethodInfo)>();
            foreach (var m in ms) lista.Add((bug, m));
            Iniciar(lista);
            return "EN CURSO (sondear ChecksBugs065.Estado y leer Resultado)";
        }

        // Pasada completa AISLADA: cada check corre en un Play fresco (se sale de Play y se vuelve a entrar entre un check y el siguiente, con
        // la escena recargada desde disco y el dominio recargado). Asi ninguno hereda muertos, la escuadra en el helicoptero, el modo dios,
        // la IA pausada ni el objetivo armado del anterior. Cuesta ~15 s extra por check.
        // Con aislar=false se usa el Play actual como antes (los checks se contaminan entre si: solo para depurar).
        public static string CorrerTodos(int desde = 0, int hasta = 132, bool aislar = true)
        {
            if (Estado == "corriendo") return "ERROR: ya hay una corrida en curso (Estado=corriendo)";
            Registrar();
            var lista = new List<(int, MethodInfo)>();
            int cuantos = 0;
            for (int b = desde; b <= hasta; b++)
            {
                if (checks.TryGetValue(b, out var ms)) { cuantos++; foreach (var m in ms) lista.Add((b, m)); }
                else if (b >= 65) { cuantos++; lista.Add((b, null)); }   // los numeros de la tanda sin check se anotan como "SIN CHECK"
            }
            Iniciar(lista, aislar);
            return $"EN CURSO ({cuantos} bugs, {lista.Count} checks, aislar={aislar}): sondear ChecksBugs065.Estado y leer Informe (tambien Temp/checks_progreso.txt)";
        }

        // Corre una lista de checks por nombre ("Bug079,Bug085,Bug079"), cada uno en un Play fresco. Sirve para repetir los dudosos N veces.
        public static string CorrerLista(string nombres, bool aislar = true)
        {
            if (Estado == "corriendo") return "ERROR: ya hay una corrida en curso (Estado=corriendo)";
            Registrar();
            var lista = new List<(int, MethodInfo)>();
            foreach (var nombre in nombres.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var m = typeof(ChecksBugs065).GetMethod(nombre, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null || !Patron.IsMatch(m.Name)) return "SIN CHECK " + nombre;
                lista.Add((int.Parse(nombre.Substring(3, 3), CultureInfo.InvariantCulture), m));
            }
            Iniciar(lista, aislar);
            return $"EN CURSO ({lista.Count} checks, aislar={aislar})";
        }

        static string Linea(int bug, string r, MethodInfo m = null)
        {
            string sufijo = m != null && m.Name.Length > 6 ? m.Name.Substring(6) : "";
            return Inv($"#{bug:000}{sufijo} {r}") + (erroresDeConsola > 0 ? $" [errores de consola: {erroresDeConsola}]" : "");
        }

        static void Iniciar(List<(int, MethodInfo)> lista, bool aislar = false)
        {
            var nombres = new List<string>();
            foreach (var (b, m) in lista) nombres.Add(b.ToString(CultureInfo.InvariantCulture) + "|" + (m != null ? m.Name : ""));
            ColaGuardada = nombres.ToArray();
            Indice = 0; Aislar = aislar;
            actual = null; salida = null;
            Resultado = ""; Informe = "";
            Fase = aislar ? "reiniciar" : "check";
            Estado = "corriendo";
            Escuchar(true);
            try { File.WriteAllText(ArchivoProgreso, $"# corrida {DateTime.Now:yyyy-MM-dd HH:mm:ss} aislar={aislar} checks={lista.Count}{Environment.NewLine}"); } catch (Exception) { }
            EditorApplication.update -= Paso;
            EditorApplication.update += Paso;
        }

        // Escena del check que toca correr (cola[Indice]); SC_Operacion por defecto.
        static string EscenaDelIndice()
        {
            var cola = ColaGuardada; int idx = Indice;
            if (idx >= cola.Length) return "SC_Operacion";
            var p = cola[idx].Split('|');
            if (p.Length < 2 || p[1].Length == 0) return "SC_Operacion";
            var m = typeof(ChecksBugs065).GetMethod(p[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var at = m != null ? (EscenaDelCheckAttribute)Attribute.GetCustomAttribute(m, typeof(EscenaDelCheckAttribute)) : null;
            return at != null ? at.Escena : "SC_Operacion";
        }

        static string RutaDeEscena(string nombre) => "Assets/_Project/Scenes/" + nombre + ".unity";

        static long Ahora() => DateTime.UtcNow.Ticks;
        static long EnSegundos(double s) => Ahora() + (long)(s * TimeSpan.TicksPerSecond);

        // Maquina de fases (cada llamada la hace avanzar un paso; sobrevive a las recargas de dominio):
        //   reiniciar -> saliendo -> entrando -> asentando -> check -> corriendo -> (reiniciar | fin)
        static void Paso()
        {
            try
            {
                if (Estado != "corriendo") { EditorApplication.update -= Paso; return; }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (!escuchando) Escuchar(true);
                for (int guardia = 0; guardia < 50; guardia++)
                {
                    string fase = Fase;
                    MarcarFase(fase);
                    if (fase == "reiniciar")
                    {
                        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
                        Hasta = EnSegundos(1.5); Fase = "saliendo"; return;
                    }
                    if (fase == "saliendo")
                    {
                        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                        if (Ahora() < Hasta) return;   // deja asentar el editor tras salir de Play
                        var esc = EditorSceneManager.GetActiveScene();
                        string nombreEsc = EscenaDelIndice();
                        if (!esc.path.EndsWith("/" + nombreEsc + ".unity", StringComparison.Ordinal))
                        {
                            if (esc.isDirty) { FalloDeArranque("la escena activa (" + esc.path + ") tiene cambios sin guardar y no es " + nombreEsc); continue; }
                            EditorSceneManager.OpenScene(RutaDeEscena(nombreEsc));
                        }
                        Hasta = EnSegundos(120); Fase = "entrando";
                        EditorApplication.isPlaying = true;
                        return;
                    }
                    if (fase == "entrando")
                    {
                        bool esOperacion = EscenaDelIndice() == "SC_Operacion";
                        bool listo = Application.isPlaying && (esOperacion ? OperacionDirector.Instancia != null : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == EscenaDelIndice());
                        if (listo) { Hasta = EnSegundos(4.0); Fase = "asentando"; return; }   // Start de todo (cinematica, oleadas, IA) y primeros cuadros
                        if (Ahora() > Hasta) { FalloDeArranque("no entro en Play con director en 120 s"); continue; }
                        return;
                    }
                    if (fase == "asentando")
                    {
                        if (!Application.isPlaying) { Fase = "reiniciar"; return; }   // algo lo saco de Play: se repite
                        if (Ahora() < Hasta) return;
                        Fase = "check"; continue;
                    }
                    if (fase == "check")
                    {
                        var cola = ColaGuardada; int idx = Indice;
                        if (idx >= cola.Length) { Terminar(); return; }
                        var partes = cola[idx].Split('|');
                        bugActual = int.Parse(partes[0], CultureInfo.InvariantCulture);
                        var nombre = partes.Length > 1 ? partes[1] : "";
                        metodoActual = nombre.Length > 0 ? typeof(ChecksBugs065).GetMethod(nombre, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
                        erroresDeConsola = 0; salida = null; actual = null;
                        SP.Core.PartidaGuardada.ModoPrueba = true;   // P10: los checks nunca escriben la partida guardada real
                        if (metodoActual == null) { Anotar(bugActual, "SIN CHECK"); SiguienteCheck(); continue; }
                        // WP9a (#097): la cinematica inicial bloquea todo el input; salvo los checks de la propia cinematica (Bug097*), se la corta antes de cada check.
                        if (EscenaDelIndice() == "SC_Operacion" && !metodoActual.Name.StartsWith("Bug097", StringComparison.Ordinal)) CinematicaDeOperacion.Saltar();
                        if (metodoActual.ReturnType == typeof(string))
                        {
                            string r;
                            try { r = (string)metodoActual.Invoke(null, null); }
                            catch (Exception e) { r = "FALLO excepcion: " + (e.InnerException ?? e).Message; }
                            Anotar(bugActual, r);
                            SiguienteCheck(); continue;
                        }
                        try { actual = (IEnumerator)metodoActual.Invoke(null, null); }
                        catch (Exception e) { Anotar(bugActual, "FALLO excepcion: " + (e.InnerException ?? e).Message); actual = null; SiguienteCheck(); continue; }
                        Fase = "corriendo"; continue;
                    }
                    if (fase == "corriendo")
                    {
                        if (actual == null)
                        {
                            // Una recarga de dominio a mitad de un check lo dejo sin rutina: se lo da por fallado y se sigue.
                            var cola = ColaGuardada; int idx = Indice;
                            if (idx < cola.Length) { var p = cola[idx].Split('|'); bugActual = int.Parse(p[0], CultureInfo.InvariantCulture); metodoActual = p.Length > 1 && p[1].Length > 0 ? typeof(ChecksBugs065).GetMethod(p[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null; }
                            Anotar(bugActual, "FALLO el dominio se recargo a mitad del check (compilacion o cambio de modo ajeno)");
                            SiguienteCheck(); continue;
                        }
                        bool sigue;
                        try { sigue = actual.MoveNext(); }
                        catch (Exception e) { Anotar(bugActual, "FALLO excepcion: " + (e.InnerException ?? e).Message); actual = null; SiguienteCheck(); continue; }
                        if (salida != null || !sigue)
                        {
                            Anotar(bugActual, salida ?? "FALLO el check asincrono termino sin llamar a Fin()");
                            actual = null;
                            SiguienteCheck(); continue;
                        }
                        return;   // espera al proximo frame
                    }
                    // fase desconocida: se corta.
                    Terminar(); return;
                }
            }
            catch (Exception e) { Informe = Informe + "ERROR DEL ARNES: " + e.Message + Environment.NewLine; Terminar(); }
        }

        // P12: tiempos de cada fase de la maquina (Temp/checks_fases.txt) para saber donde se va el tiempo de una pasada aislada.
        static void MarcarFase(string fase)
        {
            if (SessionState.GetString(KP + "faseLog", "") == fase + "#" + Indice) return;
            SessionState.SetString(KP + "faseLog", fase + "#" + Indice);
            try { File.AppendAllText("Temp/checks_fases.txt", $"{DateTime.Now:HH:mm:ss} idx={Indice} fase={fase} play={EditorApplication.isPlaying}{Environment.NewLine}"); } catch (Exception) { }
        }

        static void FalloDeArranque(string motivo)
        {
            var cola = ColaGuardada; int idx = Indice;
            if (idx < cola.Length)
            {
                var p = cola[idx].Split('|');
                bugActual = int.Parse(p[0], CultureInfo.InvariantCulture);
                metodoActual = p.Length > 1 && p[1].Length > 0 ? typeof(ChecksBugs065).GetMethod(p[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
                erroresDeConsola = 0;
                Anotar(bugActual, "FALLO " + motivo);
            }
            SiguienteCheck();
        }

        // Avanza al proximo check: con aislamiento reinicia Play primero; sin aislamiento, lo corre directo.
        static void SiguienteCheck()
        {
            Indice = Indice + 1;
            Fase = Aislar ? "reiniciar" : "check";
            if (Indice >= ColaGuardada.Length) Fase = "check";   // el proximo Paso detecta el fin y llama a Terminar
        }

        static void Anotar(int bug, string r)
        {
            string l = Linea(bug, r, metodoActual);
            Informe = Informe + l + Environment.NewLine;
            Resultado = l;
            try { File.AppendAllText(ArchivoProgreso, $"{DateTime.Now:HH:mm:ss} {l}{Environment.NewLine}"); } catch (Exception) { }
        }

        static void Terminar()
        {
            EditorApplication.update -= Paso;
            Escuchar(false);
            Estado = "listo";
            Fase = "";
            try { File.AppendAllText(ArchivoProgreso, "# FIN" + Environment.NewLine); } catch (Exception) { }
            // Con aislamiento, la pasada deja el editor FUERA de Play (el estado final de un check cualquiera no sirve de referencia).
            if (Aislar && (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)) EditorApplication.isPlaying = false;
        }

        // ---------------------------------------------------------------
        // Bug000: smoke de la columna de la Operacion (WP0). Hay que estar en Play sobre SC_Operacion.
        // ---------------------------------------------------------------
        static IEnumerator Bug000()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector (abri SC_Operacion)"); yield break; }
            var sb = new StringBuilder();
            bool ok = true;
            int errores0 = erroresDeConsola;

            // 1) Constantes y orden de fases.
            if (OperacionDirector.TotalObjetivos != 6 || OperacionDirector.Orden.Length != OperacionDirector.TotalObjetivos) { ok = false; sb.Append("Orden/TotalObjetivos incoherentes; "); }
            for (int i = 0; i < OperacionDirector.Orden.Length; i++)
                if (OperacionDirector.Indice(OperacionDirector.Orden[i]) != i) { ok = false; sb.Append("Indice(" + OperacionDirector.Orden[i] + ")!=" + i + "; "); }
            if (OperacionDirector.Indice(FaseOperacion.Victoria) != OperacionDirector.Orden.Length) { ok = false; sb.Append("Indice(Victoria); "); }

            // 2) Los seis objetivos en una sola sesion de Play: la fase entra y el titulo dice "OBJETIVO n/6".
            for (int o = 1; o <= OperacionDirector.TotalObjetivos; o++)
            {
                string r = OperacionPrueba.Arrancar(o);
                if (r.StartsWith("ERROR")) { ok = false; sb.Append($"obj{o}: {r}; "); continue; }
                foreach (var x in Esperar(1.6f)) yield return x;
                var hud = OperacionHud.Instancia;
                string titulo = hud != null ? hud.TextoTitulo : "(sin hud)";
                string esperado = $"OBJETIVO {o}/{OperacionDirector.TotalObjetivos}";
                bool faseOk = d.Fase == OperacionDirector.Orden[o - 1];
                bool tituloOk = titulo.StartsWith(esperado, StringComparison.Ordinal);
                if (!faseOk || !tituloOk) ok = false;
                sb.Append($"obj{o}: fase={d.Fase}{(faseOk ? "" : "(MAL)")} titulo='{titulo}'{(tituloOk ? "" : "(MAL)")}; ");
            }

            // 3) Subfase: arrancar con subfase la deja puesta; cambiar de fase la reinicia.
            OperacionPrueba.Arrancar(3, 1, 2);
            yield return null;
            bool subOk = d.Subfase == 2;
            OperacionPrueba.Arrancar(4);
            yield return null;
            subOk &= d.Subfase == 0;
            if (!subOk) { ok = false; sb.Append("Subfase no se fija/reinicia; "); }

            // 4) APIs nuevas del HUD (sin uso todavia): se ven y se ocultan.
            var h = OperacionHud.Instancia;
            if (h == null) { ok = false; sb.Append("sin OperacionHud; "); }
            else
            {
                bool dirEstaba = d.enabled; d.enabled = false;   // WP9a: el director pinta la lista de pasos de la Operacion cada frame: se lo apaga mientras se prueba la API
                h.BarraDeJefe("TANQUE JEFE", 0.5f);
                h.Pasos(new[] { "uno", "dos", "tres" }, 1);
                h.Subobjetivos("subobjetivo de prueba");
                yield return null;
                bool jefe = h.BarraDeJefeVisible && h.TextoJefe == "TANQUE JEFE" && Mathf.Abs(h.FraccionDeJefe - 0.5f) < 0.01f;
                bool pasos = h.TextoPasos.Contains("uno") && h.TextoPasos.Contains("dos") && h.TextoPasos.Contains("tres");
                bool sub = h.TextoSubobjetivo == "subobjetivo de prueba";
                h.OcultarBarraDeJefe(); h.Pasos(null, 0); h.Subobjetivos(null);
                yield return null;
                bool oculto = !h.BarraDeJefeVisible && h.TextoPasos == "" && h.TextoSubobjetivo == "";
                d.enabled = dirEstaba;
                if (!(jefe && pasos && sub && oculto)) { ok = false; sb.Append($"HUD jefe={jefe} pasos={pasos} sub={sub} oculto={oculto}; "); }
            }

            if (erroresDeConsola > errores0) { ok = false; sb.Append($"errores de consola nuevos={erroresDeConsola - errores0}; "); }
            OperacionPrueba.Arrancar(1);
            Fin((ok ? "OK " : "FALLO ") + "smoke WP0: " + sb);
        }
    }
}
