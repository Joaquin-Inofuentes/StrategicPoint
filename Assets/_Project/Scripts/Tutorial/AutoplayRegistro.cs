using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.Tutorial
{
    // Registro mecanico del reproductor automatico: todo lo que pasa en una corrida queda en disco, en formato pensado para que lo
    // lea una maquina (JSON por linea / JSON), no una persona. Carpeta: <proyecto>/Logs/Autoplay/<runId>/ (Logs/ esta en .gitignore).
    //   log.jsonl     una linea por mensaje de consola + eventos propios: tiempo real, tiempo de juego, cuadro, paso, fase, tipo, texto.
    //   resumen.json  la corrida entera: por paso duracion real y de juego, FPS, memoria, posicion, vida, municion, banderas que
    //                 cambiaron, subpasos, errores/avisos, capturas, intentos y resultado. Se reescribe despues de cada paso.
    //   resumen.txt   lo mismo en una tabla compacta.
    //   estado.json   el estado vivo (se reescribe cada segundo): sirve para vigilar la corrida sin hablar con el editor.
    //   cap/          capturas JPG reducidas: antes, durante (periodicas) y despues de cada paso.
    // Logs/Autoplay/historial.jsonl acumula una linea por corrida (duracion total y por paso) para estimar cuanto tarda cada test.
    // Con fallos de disco o de captura NO se corta la corrida: se prueba otro camino y se anota en 'avisos'.
    [Serializable] public class LineaLog
    {
        public int n; public float tReal; public float tJuego; public int frame;
        public int paso; public string id; public string fase; public string tipo; public string msg; public string stack;
    }

    [Serializable] public class MuestraAutoplay
    {
        public float tReal, tJuego, fps, vida, memoriaMB, memoriaUnityMB;
        public Vector3 pos; public int municion, aliadosVivos, enemigosVivos, timeScale100;
    }

    [Serializable] public class CapturaAutoplay { public string fase; public string archivo; public string metodo; public float tReal; }

    [Serializable] public class PasoAutoplay
    {
        public int indice; public string id, titulo, resultado;
        public int intentos, subpasosHechos, subpasosTotal;
        public float topeS, durRealS, durJuegoS, fpsMedio, fpsMin, fpsP5, cuadroP95Ms, distanciaM;
        public int cuadros, errores, avisos, excepciones;
        public MuestraAutoplay inicio, fin;
        public List<string> banderasNuevas = new List<string>();
        public List<CapturaAutoplay> capturas = new List<CapturaAutoplay>();
    }

    [Serializable] public class CorridaAutoplay
    {
        public string runId, inicioUtc, unityVersion, escena, carpeta, resultado, entradaHumana;
        public float durRealS, durJuegoS, fpsMedio, fpsMin;
        public int pasosTotal, ok, okReintento, fallidos, saltados, errores, avisosConsola, excepciones, capturasOk, capturasFallidas;
        public List<PasoAutoplay> pasos = new List<PasoAutoplay>();
        public List<string> avisos = new List<string>();   // fallos propios (captura, disco...) y como se resolvieron
    }

    [Serializable] class EstadoVivoAutoplay
    {
        public string runId, fase, id, titulo, ultimoMensaje, ultimaCaptura;
        public bool corriendo; public int indice, total, ok, fallidos, errores, capturas;
        public float tRealS, pasoRealS, fps;
    }

    public class AutoplayRegistro : MonoBehaviour
    {
        public static AutoplayRegistro Actual { get; private set; }
        public const int AnchoCaptura = 960;
        public const float PeriodoDuranteS = 6f;

        public string RunId { get; private set; }
        public string Carpeta { get; private set; }
        public bool ConCapturas = true;
        public CorridaAutoplay Corrida { get; private set; }

        StreamWriter log;
        int seq;
        float t0Real, t0Juego, proximoEstado;
        string faseActual = "-", ultimoMensaje = "", ultimaCaptura = "";
        PasoAutoplay paso; TutorialManager.Paso pasoRef; HashSet<string> banderasAlInicio;
        readonly List<float> cuadros = new List<float>();
        readonly List<float> cuadrosTotal = new List<float>();
        Coroutine periodica; int nDurante;
        PlayerInputDriver driver; TutorialManager tm;
        Soldier ultimoSoldado; Vector3 ultimaPos;
        int erroresGlobales;

        public static AutoplayRegistro Iniciar(GameObject donde, PlayerInputDriver driver, TutorialManager tm, bool capturas, bool entradaHumanaIgnorada)
        {
            var r = donde.AddComponent<AutoplayRegistro>();
            r.driver = driver; r.tm = tm; r.ConCapturas = capturas;
            r.RunId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            r.t0Real = Time.realtimeSinceStartup; r.t0Juego = Time.time;
            r.Corrida = new CorridaAutoplay
            {
                runId = r.RunId, inicioUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, pasosTotal = tm != null ? tm.Total : 0,
                entradaHumana = entradaHumanaIgnorada ? "ignorada" : "activa"
            };
            r.AbrirCarpeta();
            Application.logMessageReceived += r.AlLoguear;
            Actual = r;
            r.Evento("CORRIDA_INICIO", $"runId={r.RunId} escena={r.Corrida.escena} unity={Application.unityVersion} capturas={capturas} entradaHumana={r.Corrida.entradaHumana}");
            r.EscribirEstado(true);
            return r;
        }

        // ---------------- disco (con alternativa si falla) ----------------
        void AbrirCarpeta()
        {
            string[] bases =
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Autoplay")),
                Path.Combine(Application.persistentDataPath, "Autoplay")
            };
            foreach (var b in bases)
            {
                try
                {
                    Carpeta = Path.Combine(b, RunId);
                    Directory.CreateDirectory(Path.Combine(Carpeta, "cap"));
                    log = new StreamWriter(Path.Combine(Carpeta, "log.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    Corrida.carpeta = Carpeta.Replace('\\', '/');
                    if (b != bases[0]) Aviso("No se pudo escribir en Logs/Autoplay; se usa persistentDataPath: " + Carpeta);
                    return;
                }
                catch (Exception e) { Debug.LogWarning("[AUTOPLAY] Carpeta de registro no disponible (" + b + "): " + e.Message); }
            }
            Carpeta = null;   // sin disco: la corrida sigue, solo con la consola
        }

        void Escribir(LineaLog l)
        {
            if (log == null) return;
            try { log.WriteLine(JsonUtility.ToJson(l)); }
            catch (Exception) { try { log.Dispose(); } catch { } log = null; if (Corrida != null) Corrida.avisos.Add("Fallo la escritura de log.jsonl a las " + l.tReal.ToString("0.0") + " s; se sigue solo con resumen.json"); }
        }

        void AlLoguear(string texto, string pila, LogType tipo)
        {
            if (Corrida == null) return;
            string t = tipo == LogType.Log ? "Log" : tipo == LogType.Warning ? "Warning" : tipo == LogType.Assert ? "Assert" : tipo == LogType.Exception ? "Exception" : "Error";
            if (paso != null)
            {
                if (tipo == LogType.Warning) paso.avisos++;
                else if (tipo == LogType.Exception) { paso.excepciones++; paso.errores++; }
                else if (tipo == LogType.Error || tipo == LogType.Assert) paso.errores++;
            }
            if (tipo == LogType.Warning) Corrida.avisosConsola++;
            else if (tipo == LogType.Exception) { Corrida.excepciones++; Corrida.errores++; erroresGlobales++; }
            else if (tipo == LogType.Error || tipo == LogType.Assert) { Corrida.errores++; erroresGlobales++; }
            ultimoMensaje = texto != null && texto.Length > 160 ? texto.Substring(0, 160) : texto;
            Escribir(NuevaLinea(t, texto, tipo == LogType.Log || tipo == LogType.Warning ? null : Recortar(pila, 700)));
        }

        LineaLog NuevaLinea(string tipo, string msg, string pila)
        {
            return new LineaLog
            {
                n = ++seq, tReal = Time.realtimeSinceStartup - t0Real, tJuego = Time.time - t0Juego, frame = Time.frameCount,
                paso = paso != null ? paso.indice : -1, id = paso != null ? paso.id : "", fase = faseActual, tipo = tipo,
                msg = Recortar(msg, 1200), stack = pila
            };
        }

        static string Recortar(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s.Substring(0, max) + "..." : s);

        // Evento propio (entrada virtual, inicio/fin de paso...). Va al mismo log.jsonl con tipo "EVENTO:<tipo>".
        public void Evento(string tipo, string detalle) { if (Corrida != null) Escribir(NuevaLinea("EVENTO:" + tipo, detalle, null)); }

        void Aviso(string s) { if (Corrida != null) Corrida.avisos.Add(s); Evento("AVISO", s); }

        // ---------------- muestras ----------------
        MuestraAutoplay Muestra()
        {
            var m = new MuestraAutoplay { tReal = Time.realtimeSinceStartup - t0Real, tJuego = Time.time - t0Juego, timeScale100 = Mathf.RoundToInt(Time.timeScale * 100f) };
            try
            {
                var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
                if (yo != null)
                {
                    m.pos = yo.transform.position;
                    if (yo.Health != null) m.vida = yo.Health.Current;
                    if (yo.Weapon != null) m.municion = yo.Weapon.CurrentAmmo;
                }
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    if (s.Team == TeamId.Player) m.aliadosVivos++; else if (s.Team == TeamId.Enemy) m.enemigosVivos++;
                }
                m.memoriaMB = GC.GetTotalMemory(false) / 1048576f;
                m.memoriaUnityMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
                m.fps = cuadros.Count > 0 ? cuadros.Count / Mathf.Max(0.0001f, Suma(cuadros)) : 0f;
            }
            catch (Exception e) { Aviso("Muestra incompleta: " + e.Message); }
            return m;
        }

        static float Suma(List<float> l) { float s = 0f; for (int i = 0; i < l.Count; i++) s += l[i]; return s; }

        HashSet<string> BanderasVerdaderas()
        {
            var set = new HashSet<string>();
            if (tm == null || tm.Flags == null) return set;
            foreach (var f in tm.Flags.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(bool) && (bool)f.GetValue(tm.Flags)) set.Add(f.Name);
            return set;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) { cuadros.Add(dt); cuadrosTotal.Add(dt); }
            var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            if (yo != null)
            {
                if (yo == ultimoSoldado && paso != null) paso.distanciaM += Vector3.Distance(yo.transform.position, ultimaPos);
                ultimoSoldado = yo; ultimaPos = yo.transform.position;
            }
            if (Corrida.resultado == null && Time.realtimeSinceStartup >= proximoEstado) { proximoEstado = Time.realtimeSinceStartup + 1f; EscribirEstado(true); }
        }

        // ---------------- pasos ----------------
        public void PasoInicio(int indice, TutorialManager.Paso p)
        {
            pasoRef = p; nDurante = 0; cuadros.Clear(); faseActual = "gesto";
            paso = new PasoAutoplay { indice = indice, id = p != null ? p.Id : "?", titulo = p != null ? p.Titulo : "?", intentos = 1 };
            paso.inicio = Muestra();
            banderasAlInicio = BanderasVerdaderas();
            Corrida.pasos.Add(paso);
            Evento("PASO_INICIO", $"{paso.indice + 1}/{Corrida.pasosTotal} {paso.id} '{paso.titulo}'");
        }

        public void Reintento(string motivo) { if (paso == null) return; paso.intentos++; Evento("REINTENTO", $"intento {paso.intentos}: {motivo}"); }
        public void ActualizarIntentos(int n) { if (paso != null) paso.intentos = n; }
        public void Tope(float s) { if (paso != null) paso.topeS = s; }

        public void IniciarDurante()
        {
            DetenerDurante();
            if (ConCapturas) periodica = StartCoroutine(CapturasPeriodicas());
        }

        void DetenerDurante() { if (periodica != null) { StopCoroutine(periodica); periodica = null; } }

        IEnumerator CapturasPeriodicas()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(PeriodoDuranteS);
                faseActual = "durante";
                yield return Capturar("durante");
                faseActual = "gesto";
            }
        }

        // resultado: OK | OK_REINTENTO | FALLIDO_FORZADO | SALTADO
        public IEnumerator PasoFin(string resultado)
        {
            if (paso == null) yield break;
            DetenerDurante();
            paso.fin = Muestra();
            paso.resultado = resultado;
            paso.durRealS = paso.fin.tReal - paso.inicio.tReal;
            paso.durJuegoS = paso.fin.tJuego - paso.inicio.tJuego;
            paso.cuadros = cuadros.Count;
            if (cuadros.Count > 0)
            {
                var orden = new List<float>(cuadros); orden.Sort();
                float tot = Suma(cuadros);
                paso.fpsMedio = cuadros.Count / Mathf.Max(0.0001f, tot);
                paso.fpsMin = 1f / Mathf.Max(0.0001f, orden[orden.Count - 1]);
                paso.fpsP5 = 1f / Mathf.Max(0.0001f, orden[Mathf.Min(orden.Count - 1, Mathf.CeilToInt(orden.Count * 0.95f) - 1)]);
                paso.cuadroP95Ms = orden[Mathf.Min(orden.Count - 1, Mathf.CeilToInt(orden.Count * 0.95f) - 1)] * 1000f;
            }
            var ahora = BanderasVerdaderas();
            foreach (var b in ahora) if (banderasAlInicio == null || !banderasAlInicio.Contains(b)) paso.banderasNuevas.Add(b);
            if (pasoRef != null && pasoRef.Subs != null)
            {
                paso.subpasosTotal = pasoRef.Subs.Length;
                foreach (var s in pasoRef.Subs) if (s.Hecha) paso.subpasosHechos++;
            }
            switch (resultado)
            {
                case "OK": Corrida.ok++; break;
                case "OK_REINTENTO": Corrida.okReintento++; break;
                case "FALLIDO_FORZADO": Corrida.fallidos++; break;
                default: Corrida.saltados++; break;
            }
            Evento("PASO_FIN", $"{paso.id} {resultado} real={paso.durRealS:0.0}s juego={paso.durJuegoS:0.0}s fps={paso.fpsMedio:0} min={paso.fpsMin:0} sub={paso.subpasosHechos}/{paso.subpasosTotal} banderas=[{string.Join(",", paso.banderasNuevas)}]");
            faseActual = "despues";
            yield return Capturar("despues");
            faseActual = "-";
            EscribirResumen();
            EscribirEstado(true);
            paso = null;
        }

        // ---------------- capturas (3 caminos) ----------------
        public IEnumerator Capturar(string fase)
        {
            if (!ConCapturas || Carpeta == null || paso == null) yield break;
            string nombre = $"{paso.indice:00}_{paso.id}_{fase}{(fase == "durante" ? "_" + (++nDurante) : "")}.jpg";
            string ruta = Path.Combine(Carpeta, "cap", nombre);
            yield return new WaitForEndOfFrame();
            byte[] bytes = null; string metodo = null;
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex != null) { bytes = Reducir(tex); metodo = "pantalla"; Destroy(tex); }
            }
            catch (Exception e) { Aviso("Captura por pantalla fallo (" + e.Message + "); se prueba con la camara"); }
            if (bytes == null)
            {
                try { bytes = DesdeCamara(); if (bytes != null) metodo = "camara"; }
                catch (Exception e) { Aviso("Captura por camara fallo (" + e.Message + ")"); }
            }
            if (bytes != null)
            {
                try { File.WriteAllBytes(ruta, bytes); }
                catch (Exception e) { Aviso("No se pudo guardar " + nombre + ": " + e.Message); bytes = null; }
            }
            if (bytes == null)
            {
                try { ScreenCapture.CaptureScreenshot(ruta); metodo = "archivo-async"; bytes = new byte[0]; }
                catch (Exception e) { Aviso("Captura " + nombre + " imposible: " + e.Message); }
            }
            if (bytes != null)
            {
                Corrida.capturasOk++;
                paso.capturas.Add(new CapturaAutoplay { fase = fase, archivo = "cap/" + nombre, metodo = metodo, tReal = Time.realtimeSinceStartup - t0Real });
                ultimaCaptura = "cap/" + nombre;
            }
            else Corrida.capturasFallidas++;
        }

        static byte[] Reducir(Texture2D src)
        {
            if (src.width <= AnchoCaptura) return src.EncodeToJPG(75);
            int alto = Mathf.Max(2, Mathf.RoundToInt(AnchoCaptura * (float)src.height / src.width));
            var rt = RenderTexture.GetTemporary(AnchoCaptura, alto, 0, RenderTextureFormat.ARGB32);
            var previa = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var t2 = new Texture2D(AnchoCaptura, alto, TextureFormat.RGB24, false);
                t2.ReadPixels(new Rect(0, 0, AnchoCaptura, alto), 0, 0);
                t2.Apply();
                var b = t2.EncodeToJPG(75);
                Destroy(t2);
                return b;
            }
            finally { RenderTexture.active = previa; RenderTexture.ReleaseTemporary(rt); }
        }

        static byte[] DesdeCamara()
        {
            Camera cam = null;
            foreach (var c in Camera.allCameras)
            {
                if (c == null || !c.enabled) continue;
                if (cam == null || c.CompareTag("MainCamera")) cam = c;
            }
            if (cam == null) return null;
            int alto = AnchoCaptura * 9 / 16;
            var rt = RenderTexture.GetTemporary(AnchoCaptura, alto, 24, RenderTextureFormat.ARGB32);
            var previaTarget = cam.targetTexture; var previaActiva = RenderTexture.active;
            try
            {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var t = new Texture2D(AnchoCaptura, alto, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, AnchoCaptura, alto), 0, 0); t.Apply();
                var b = t.EncodeToJPG(75); Destroy(t); return b;
            }
            finally { cam.targetTexture = previaTarget; RenderTexture.active = previaActiva; RenderTexture.ReleaseTemporary(rt); }
        }

        // ---------------- salidas ----------------
        void EscribirEstado(bool corriendo)
        {
            if (Carpeta == null || Corrida == null) return;
            try
            {
                var e = new EstadoVivoAutoplay
                {
                    runId = RunId, corriendo = corriendo, fase = faseActual, indice = paso != null ? paso.indice : Corrida.pasos.Count, total = Corrida.pasosTotal,
                    id = paso != null ? paso.id : "", titulo = paso != null ? paso.titulo : "", ok = Corrida.ok + Corrida.okReintento, fallidos = Corrida.fallidos,
                    errores = Corrida.errores, capturas = Corrida.capturasOk, tRealS = Time.realtimeSinceStartup - t0Real,
                    pasoRealS = paso != null ? (Time.realtimeSinceStartup - t0Real) - paso.inicio.tReal : 0f,
                    fps = cuadros.Count > 0 ? cuadros.Count / Mathf.Max(0.0001f, Suma(cuadros)) : 0f, ultimoMensaje = ultimoMensaje, ultimaCaptura = ultimaCaptura
                };
                string json = JsonUtility.ToJson(e);
                File.WriteAllText(Path.Combine(Carpeta, "estado.json"), json);
                string raiz = Path.GetDirectoryName(Carpeta);
                File.WriteAllText(Path.Combine(raiz, "estado_actual.json"), json);
            }
            catch (Exception) { /* el estado vivo es opcional: si falla, el bash cae a leer resumen.json o a consultar al editor */ }
        }

        void EscribirResumen()
        {
            if (Carpeta == null) return;
            try
            {
                Corrida.durRealS = Time.realtimeSinceStartup - t0Real;
                Corrida.durJuegoS = Time.time - t0Juego;
                if (cuadrosTotal.Count > 0)
                {
                    float tot = Suma(cuadrosTotal); float mx = 0f; foreach (var c in cuadrosTotal) if (c > mx) mx = c;
                    Corrida.fpsMedio = cuadrosTotal.Count / Mathf.Max(0.0001f, tot); Corrida.fpsMin = 1f / Mathf.Max(0.0001f, mx);
                }
                File.WriteAllText(Path.Combine(Carpeta, "resumen.json"), JsonUtility.ToJson(Corrida, true));
                var sb = new StringBuilder();
                sb.AppendLine($"run={RunId} resultado={Corrida.resultado ?? "en-curso"} real={Corrida.durRealS:0.0}s juego={Corrida.durJuegoS:0.0}s fps={Corrida.fpsMedio:0}/min{Corrida.fpsMin:0} " +
                              $"ok={Corrida.ok} okReintento={Corrida.okReintento} fallidos={Corrida.fallidos} saltados={Corrida.saltados} errores={Corrida.errores} excepciones={Corrida.excepciones} avisos={Corrida.avisosConsola} capturas={Corrida.capturasOk}/{Corrida.capturasOk + Corrida.capturasFallidas} entradaHumana={Corrida.entradaHumana}");
                sb.AppendLine("idx id resultado intentos real_s juego_s fps fpsMin sub dist_m vida municion aliados enemigos memMB err banderasNuevas");
                foreach (var p in Corrida.pasos)
                    sb.AppendLine($"{p.indice + 1} {p.id} {p.resultado} {p.intentos} {p.durRealS:0.0} {p.durJuegoS:0.0} {p.fpsMedio:0} {p.fpsMin:0} {p.subpasosHechos}/{p.subpasosTotal} {p.distanciaM:0} " +
                                  $"{(p.fin != null ? p.fin.vida : 0):0} {(p.fin != null ? p.fin.municion : 0)} {(p.fin != null ? p.fin.aliadosVivos : 0)} {(p.fin != null ? p.fin.enemigosVivos : 0)} {(p.fin != null ? p.fin.memoriaMB : 0):0} {p.errores} [{string.Join(",", p.banderasNuevas)}]");
                foreach (var a in Corrida.avisos) sb.AppendLine("AVISO " + a);
                File.WriteAllText(Path.Combine(Carpeta, "resumen.txt"), sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[AUTOPLAY] No se pudo escribir resumen.json: " + e.Message); }
        }

        public void Finalizar(string resultado)
        {
            if (Corrida == null || Corrida.resultado != null) return;
            DetenerDurante();
            Corrida.resultado = resultado;
            Evento("CORRIDA_FIN", $"resultado={resultado} ok={Corrida.ok} reintento={Corrida.okReintento} fallidos={Corrida.fallidos} saltados={Corrida.saltados}");
            EscribirResumen();
            EscribirEstado(false);
            try
            {
                var h = new StringBuilder();
                h.Append("{\"runId\":\"").Append(RunId).Append("\",\"resultado\":\"").Append(resultado).Append("\",\"durRealS\":").Append(Corrida.durRealS.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                 .Append(",\"pasos\":{");
                for (int i = 0; i < Corrida.pasos.Count; i++)
                    h.Append(i > 0 ? "," : "").Append("\"").Append(Corrida.pasos[i].id).Append("\":").Append(Corrida.pasos[i].durRealS.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                h.Append("}}");
                if (Carpeta != null) File.AppendAllText(Path.Combine(Path.GetDirectoryName(Carpeta), "historial.jsonl"), h + "\n");
            }
            catch (Exception) { }
            Application.logMessageReceived -= AlLoguear;
            try { log?.Dispose(); } catch { }
            log = null;
        }

        void OnDestroy()
        {
            if (Corrida != null && Corrida.resultado == null) Finalizar("INTERRUMPIDA");
            Application.logMessageReceived -= AlLoguear;
            if (Actual == this) Actual = null;
        }
    }
}
