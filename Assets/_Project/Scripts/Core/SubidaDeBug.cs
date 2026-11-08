using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SP.Core
{
    // Todo lo que viaja al Sheet en un reporte. imagen: JPG ya comprimido; datosJson: objeto JSON {"Columna":valor,...}.
    public sealed class ReporteSubida
    {
        public int numero;
        public string descripcion;   // columna "Descripcion": resumen automatico del estado
        public string comentarios;   // columna "Comentarios": lo que escribio el jugador
        public string datosJson;     // una columna por clave (fase, posicion, tiempos...)
        public string estadoJson;    // el .json completo del estado; el script lo guarda en Drive y deja el link en "Estado_JSON"
        public string estadoNombre;
        public byte[] imagen;
    }

    // Pedido: "ademas de generar el registro local tras apretar [U], que tambien lo suba" (foto + datos a un Sheet de Google
    // via Apps Script; la foto va a Drive y se ve en la celda), con los comentarios del jugador en su propia columna y el estado
    // (fase, posicion, tiempos...) separado en columnas para poder recrear la escena.
    //
    //  - Se llama DESPUES de guardar el registro local (SesionLog.MarcarBugInterno): lo local nunca depende de la red.
    //  - Corre en un hilo de red: el juego sigue (la subida tarda de 5 a 120 s segun el peso y el estado de Apps Script).
    //  - Optimizado: la captura se baja a JPG 1920 px de ancho (unos 150-800 KB) en vez de subir el PNG crudo (3-10 MB), y el
    //    JSON se arma con StringBuilder una sola vez.
    //  - Si falla (sin internet, token malo, Apps Script caido) el reporte queda en Registros/pendientes y se reintenta en la
    //    proxima partida. Un reintento puede duplicar la fila si la primera vez Google guardo pero la respuesta se perdio.
    //  - Las continuaciones vuelven al hilo principal (UnitySynchronizationContext): ahi se puede tocar SesionLog y la UI.
    public static class SubidaDeBug
    {
        // Web app de Apps Script ("Ejecutar como: yo", "Acceso: cualquier usuario"). El token es un secreto compartido con el script.
        const string Url = "https://script.google.com/macros/s/AKfycbwMCYhUJB1SnUEr0HQMH1blhq6W4eoBcsL750EEyw7zpzXa10TPBnMXuUU49Sif3hu3ww/exec";
        const string Token = "0f7f4acca98639e0bd01e3079924b1195d58918e864ed932";

        const int MaxTexto = 4000;           // limite del script para descripcion y comentarios
        const int AnchoMaximo = 1920;
        const int CalidadJpg = 85;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Las pruebas automaticas lo apagan para no llenar el Sheet del usuario con filas de prueba.
        public static bool Habilitada = true;
        public static int Enviando { get; private set; }
        public static string UltimoResultado { get; private set; } = "";
        public static int UltimaFila { get; private set; }
        public static int UltimoBugSubido { get; private set; }
        // (numero de bug, ok, detalle)
        public static event Action<int, bool, string> Terminado;

        static HttpClient http;
        // Un solo cliente para todo el juego; AllowAutoRedirect sigue el 302 con el que responde Apps Script.
        static HttpClient Http => http ??= new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(4) };

        [Serializable] class Respuesta { public bool ok; public int fila; public string error; }
        [Serializable]
        class Pendiente
        {
            public int numero; public string descripcion, comentarios, datosJson, estadoNombre;
            public string imagen, estado;   // nombres de los archivos hermanos
        }

        // 2026-11-06 (pedido del dueno): un bug marcado desde el EDITOR queda solo en local (Registros/); al Sheet suben unicamente los
        // marcados desde el .exe (build). Los registros locales se guardan igual en los dos casos.
        public static bool SubeAlSheet => !Application.isEditor;
        public static bool Disponible => Habilitada && SubeAlSheet && Application.isPlaying && !Application.isBatchMode;

        static string CarpetaPendientes => Path.Combine(SesionLog.Carpeta, "pendientes");

        // --------------------------------------------------------------- API
        // El reporte debe traer imagen (JPG/PNG/GIF/WEBP, hasta 15 MB).
        public static void Subir(ReporteSubida r)
        {
            if (!Disponible || r == null) return;
            if (r.imagen == null || r.imagen.Length == 0) { Terminar(r.numero, false, "sin imagen para subir", 0); return; }
            r.descripcion = Recortar(r.descripcion, MaxTexto);
            r.comentarios = Recortar(r.comentarios, MaxTexto);
            _ = Ejecutar(r, true);
        }

        // Arma las columnas del Sheet a partir de la foto del estado. Todo en formato invariante (punto decimal).
        public static ReporteSubida Armar(EstadoDeSesion e, string nota, string rutaJson, string rutaPng, string jsonTexto, List<KeyValuePair<string, string>> extra = null)
        {
            var c = new Columnas();
            // Columnas nuevas (Docs/PLAN_columnas_bugs_opus.md): el script del Sheet las escribe por nombre de columna.
            if (extra != null) foreach (var kv in extra) c.S(kv.Key, kv.Value);
            string fase = Mision(e, "Operacion.fase") ?? Mision(e, "MisionDirector.fase") ?? "?";
            c.I("Bug", e.numero);
            c.S("Escena", e.escena);
            c.S("Fase", fase);
            c.N("Tiempo_sesion_s", e.tiempoSesion, "0.0");
            c.N("Tiempo_juego_s", e.tiempoJuego, "0.0");
            if (float.TryParse(Mision(e, "Operacion.reloj"), NumberStyles.Float, Inv, out var reloj)) c.N("Reloj_operacion_s", reloj, "0.0");

            var j = e.jugador;
            SnapSoldado yo = null;
            foreach (var s in e.soldados) if (s.poseido) { yo = s; break; }
            if (!string.IsNullOrEmpty(j.soldado))
            {
                c.S("Jugador", j.soldado); c.S("Modo", j.modo); c.S("Asiento", j.asiento);
                c.N("Jugador_X", j.pos.x); c.N("Jugador_Y", j.pos.y); c.N("Jugador_Z", j.pos.z); c.N("Jugador_Yaw", j.yaw, "0.0");
                if (yo != null) { c.I("Jugador_Vida", yo.vida); c.I("Jugador_VidaMax", yo.vidaMax); }
            }
            c.N("Camara_X", j.camPos.x); c.N("Camara_Y", j.camPos.y); c.N("Camara_Z", j.camPos.z);
            c.N("Camara_RotX", j.camRot.x, "0.0"); c.N("Camara_RotY", j.camRot.y, "0.0"); c.N("Camara_RotZ", j.camRot.z, "0.0");
            c.N("Camara_FOV", j.fov, "0");

            int vivosA = 0, vivosE = 0, totalE = 0;
            var aliados = new StringBuilder();
            foreach (var s in e.soldados)
            {
                bool enemigo = s.equipo == "Enemy";
                if (enemigo) { if (s.vivo) totalE++; if (s.vivo && s.activo) vivosE++; continue; }
                if (s.rol == "Civilian") continue;
                if (s.vivo && s.activo) vivosA++;
                if (aliados.Length > 0) aliados.Append('\n');
                aliados.Append(s.nombre).Append('[').Append(s.rol).Append("] hp=").Append(s.vida).Append('/').Append(s.vidaMax)
                       .Append(s.vivo ? "" : " CAIDO").Append(s.activo ? "" : " INACTIVO")
                       .Append(" pos=(").Append(F(s.pos.x)).Append(',').Append(F(s.pos.y)).Append(',').Append(F(s.pos.z)).Append(") yaw=").Append(F(s.yaw))
                       .Append(" ia=").Append(s.estadoIa).Append(" arma=").Append(s.arma).Append(" mun=").Append(s.municion)
                       .Append(s.poseido ? " POSEIDO" : "").Append(string.IsNullOrEmpty(s.vehiculo) ? "" : " vehiculo=" + s.vehiculo + "/" + s.asiento);
            }
            c.I("Aliados_vivos", vivosA);
            c.I("Enemigos_vivos", vivosE);
            c.I("Enemigos_vivos_total", totalE);
            int vehiculosReales = 0; foreach (var v0 in e.vehiculos) if (!(!v0.activo && v0.nombre != null && v0.nombre.Contains("Plantilla"))) vehiculosReales++;   // H7: las plantillas inactivas no cuentan
            c.I("Vehiculos", vehiculosReales);
            c.S("Aliados", aliados.ToString());

            var veh = new StringBuilder();
            foreach (var v in e.vehiculos)
            {
                if (!v.activo && v.nombre != null && v.nombre.Contains("Plantilla")) continue;
                if (veh.Length > 0) veh.Append('\n');
                veh.Append(v.nombre).Append(' ').Append(v.bando).Append(" hp=").Append(v.vida).Append('/').Append(v.vidaMax)
                   .Append(v.destruido ? " DESTRUIDO" : "").Append(v.activo ? "" : " INACTIVO")
                   .Append(" pos=(").Append(F(v.pos.x)).Append(',').Append(F(v.pos.y)).Append(',').Append(F(v.pos.z)).Append(") rot=(")
                   .Append(F(v.rot.x)).Append(',').Append(F(v.rot.y)).Append(',').Append(F(v.rot.z)).Append(')');
                if (v.ocupantes != null && v.ocupantes.Count > 0) veh.Append(" ocupantes=").Append(string.Join(",", v.ocupantes));
            }
            c.S("Vehiculos_detalle", veh.ToString());

            var mision = new StringBuilder();
            foreach (var k in e.mision) { if (mision.Length > 0) mision.Append("; "); mision.Append(k.k).Append('=').Append(k.v); }
            c.S("Mision", mision.ToString());

            c.S("Diag_Tiempo", Diag(e, "TIEMPO:"));
            c.S("Diag_Jugador", Diag(e, "JUGADOR:"));
            c.S("Diag_Enemigos", Diag(e, "ENEMIGOS:"));
            c.S("Diag_Curacion", Diag(e, "CURACION:"));
            c.S("Archivo_local", Path.GetFileName(rutaJson));
            c.S("Captura_local", Path.GetFileName(rutaPng));

            // Descripcion legible (cada dato en su linea; los numeros exactos estan en sus propias columnas).
            var sb = new StringBuilder();
            sb.Append("BUG #").Append(e.numero.ToString("000")).Append(" · ").Append(e.escena).Append(" · ").Append(e.fecha)
              .Append("\nFase: ").Append(fase).Append(" · sesión T+").Append(e.tiempoSesion.ToString("0.0", Inv)).Append(" s");
            if (!string.IsNullOrEmpty(j.soldado))
                sb.Append("\nJugador: ").Append(j.soldado).Append(" · X ").Append(F(j.pos.x)).Append(" · Y ").Append(F(j.pos.y)).Append(" · Z ").Append(F(j.pos.z));
            sb.Append("\nAliados vivos: ").Append(vivosA).Append(" · Enemigos vivos: ").Append(vivosE).Append(" · Vehículos: ").Append(e.vehiculos.Count);
            return new ReporteSubida
            {
                numero = e.numero,
                descripcion = sb.ToString(),
                comentarios = nota ?? "",
                datosJson = c.ToString(),
                estadoJson = jsonTexto,
                estadoNombre = Path.GetFileNameWithoutExtension(rutaJson),
            };
        }

        // Baja la captura a JPG de hasta 1920 px de ancho. Solo lee la textura (no la destruye).
        public static byte[] Comprimir(Texture2D tex)
        {
            if (tex == null) return null;
            Texture2D copia = null; RenderTexture rt = null; var previo = RenderTexture.active;
            try
            {
                Texture2D fuente = tex;
                if (tex.width > AnchoMaximo)
                {
                    int alto = Mathf.Max(1, Mathf.RoundToInt(tex.height * (float)AnchoMaximo / tex.width));
                    rt = RenderTexture.GetTemporary(AnchoMaximo, alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    copia = new Texture2D(AnchoMaximo, alto, TextureFormat.RGB24, false);
                    copia.ReadPixels(new Rect(0, 0, AnchoMaximo, alto), 0, 0);
                    copia.Apply(false);
                    fuente = copia;
                }
                return fuente.EncodeToJPG(CalidadJpg);
            }
            catch (Exception ex) { Debug.LogWarning("[SubidaDeBug] No se pudo comprimir la captura: " + ex.Message); return null; }
            finally
            {
                RenderTexture.active = previo;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (copia != null) UnityEngine.Object.Destroy(copia);
            }
        }

        // Para cuando la captura quedo solo en disco (ScreenCapture.CaptureScreenshot asincrono).
        public static byte[] ComprimirArchivo(string rutaPng)
        {
            Texture2D t = null;
            try
            {
                if (!File.Exists(rutaPng)) return null;
                t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!t.LoadImage(File.ReadAllBytes(rutaPng))) return null;
                return Comprimir(t);
            }
            catch { return null; }
            finally { if (t != null) UnityEngine.Object.Destroy(t); }
        }

        // Reintenta, de a uno, lo que no se pudo subir en partidas anteriores.
        public static void ReintentarPendientes()
        {
            if (!Disponible) return;
            _ = ReintentarAsync();
        }

        // ------------------------------------------------------------ interno
        static string Mision(EstadoDeSesion e, string clave)
        {
            foreach (var k in e.mision) if (k.k == clave) return k.v;
            return null;
        }

        static string Diag(EstadoDeSesion e, string prefijo)
        {
            foreach (var l in e.diagnostico) if (l != null && l.StartsWith(prefijo, StringComparison.Ordinal)) return l.Substring(prefijo.Length).Trim();
            return "";
        }

        static string F(float v) => v.ToString("0.##", Inv);

        // Objeto JSON de columnas: los numeros viajan como numeros (se pueden filtrar y graficar en el Sheet).
        sealed class Columnas
        {
            readonly StringBuilder sb = new StringBuilder("{");
            bool primero = true;
            void Clave(string k) { if (!primero) sb.Append(','); primero = false; sb.Append('"'); EscaparJson(sb, k); sb.Append("\":"); }
            public void S(string k, string v) { Clave(k); sb.Append('"'); EscaparJson(sb, v ?? ""); sb.Append('"'); }
            public void I(string k, int v) { Clave(k); sb.Append(v.ToString(Inv)); }
            public void N(string k, float v, string fmt = "0.##")
            {
                Clave(k);
                if (float.IsNaN(v) || float.IsInfinity(v)) sb.Append("null"); else sb.Append(v.ToString(fmt, Inv));
            }
            public override string ToString() => sb.ToString() + "}";
        }

        static async Task ReintentarAsync()
        {
            string[] jsons;
            try { if (!Directory.Exists(CarpetaPendientes)) return; jsons = Directory.GetFiles(CarpetaPendientes, "*.json"); }
            catch { return; }
            foreach (var f in jsons)
            {
                if (f.EndsWith("_estado.json", StringComparison.Ordinal)) continue;
                try
                {
                    var p = JsonUtility.FromJson<Pendiente>(File.ReadAllText(f, Encoding.UTF8));
                    string img = p == null || string.IsNullOrEmpty(p.imagen) ? null : Path.Combine(CarpetaPendientes, p.imagen);
                    if (img == null || !File.Exists(img)) { File.Delete(f); continue; }
                    string est = string.IsNullOrEmpty(p.estado) ? null : Path.Combine(CarpetaPendientes, p.estado);
                    var r = new ReporteSubida
                    {
                        numero = p.numero, descripcion = p.descripcion, comentarios = p.comentarios, datosJson = p.datosJson,
                        estadoNombre = p.estadoNombre, imagen = File.ReadAllBytes(img),
                        estadoJson = est != null && File.Exists(est) ? File.ReadAllText(est, Encoding.UTF8) : null,
                    };
                    await Ejecutar(r, false);
                }
                catch (Exception ex) { Debug.LogWarning("[SubidaDeBug] Pendiente ilegible " + Path.GetFileName(f) + ": " + ex.Message); }
            }
        }

        static async Task Ejecutar(ReporteSubida r, bool guardarSiFalla)
        {
            Enviando++;
            string error = null; int fila = 0;
            try
            {
                string crudo = await Enviar(r);        // vuelve al hilo principal
                var resp = JsonUtility.FromJson<Respuesta>(crudo);
                if (resp != null && resp.ok) fila = resp.fila;
                else error = resp != null && !string.IsNullOrEmpty(resp.error) ? resp.error : "respuesta inesperada: " + Recortar(crudo, 120);
            }
            catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
            Enviando--;

            if (error == null) { BorrarPendiente(r.numero); Terminar(r.numero, true, "fila " + fila, fila); }
            else
            {
                if (guardarSiFalla) GuardarPendiente(r);
                Terminar(r.numero, false, error, 0);
            }
        }

        // El POST en si. Todo el armado del JSON ocurre antes del primer await (hilo principal, pocos ms);
        // la red usa ConfigureAwait(false) para no tocar el hilo del juego.
        static async Task<string> Enviar(ReporteSubida r)
        {
            string b64 = Convert.ToBase64String(r.imagen);
            var sb = new StringBuilder(b64.Length + (r.estadoJson != null ? r.estadoJson.Length * 2 : 0) + 4096);
            sb.Append("{\"token\":\"").Append(Token).Append("\",\"descripcion\":\"");
            EscaparJson(sb, r.descripcion ?? "");
            sb.Append("\",\"comentarios\":\"");
            EscaparJson(sb, r.comentarios ?? "");
            sb.Append('"');
            if (!string.IsNullOrEmpty(r.datosJson)) sb.Append(",\"datos\":").Append(r.datosJson);
            if (!string.IsNullOrEmpty(r.estadoJson))
            {
                sb.Append(",\"estado_nombre\":\""); EscaparJson(sb, r.estadoNombre ?? "estado");
                sb.Append("\",\"estado_json\":\""); EscaparJson(sb, r.estadoJson); sb.Append('"');
            }
            sb.Append(",\"imagen_base64\":\"").Append(b64).Append("\"}");
            b64 = null;

            using (var contenido = new StringContent(sb.ToString(), new UTF8Encoding(false), "application/json"))
            using (var resp = await Http.PostAsync(Url, contenido).ConfigureAwait(false))
            {
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        static void EscaparJson(StringBuilder sb, string s)
        {
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
        }

        static void Terminar(int n, bool ok, string detalle, int fila)
        {
            UltimoResultado = (ok ? "OK " : "FALLO ") + $"bug #{n:000}: {detalle}";
            if (ok) { UltimaFila = fila; UltimoBugSubido = n; }
            SesionLog.Evento(ok ? $"Bug #{n:000} SUBIDO al Sheet ({detalle})"
                                : $"Bug #{n:000} NO se pudo subir: {detalle} (queda en Registros/pendientes)");
            if (!ok) Debug.LogWarning($"[SubidaDeBug] Bug #{n:000} no se subio: {detalle}");
            try
            {
                SP.UI.AlertQueue.Push(ok ? $"BUG #{n:000} SUBIDO AL SHEET" : $"BUG #{n:000} NO SE SUBIO (QUEDA PENDIENTE)",
                                      ok ? SP.UI.AlertPriority.Media : SP.UI.AlertPriority.Alta, 2.5f);
            }
            catch { }
            Terminado?.Invoke(n, ok, detalle);
        }

        static void GuardarPendiente(ReporteSubida r)
        {
            try
            {
                Directory.CreateDirectory(CarpetaPendientes);
                string baseN = $"{r.numero:000}";
                string nombreImg = $"{baseN}_{DateTime.Now:HHmmss}.jpg";
                File.WriteAllBytes(Path.Combine(CarpetaPendientes, nombreImg), r.imagen);
                string nombreEst = null;
                if (!string.IsNullOrEmpty(r.estadoJson))
                {
                    nombreEst = baseN + "_estado.json";
                    File.WriteAllText(Path.Combine(CarpetaPendientes, nombreEst), r.estadoJson, new UTF8Encoding(false));
                }
                var p = new Pendiente
                {
                    numero = r.numero, descripcion = r.descripcion, comentarios = r.comentarios, datosJson = r.datosJson,
                    estadoNombre = r.estadoNombre, imagen = nombreImg, estado = nombreEst,
                };
                File.WriteAllText(Path.Combine(CarpetaPendientes, baseN + ".json"), JsonUtility.ToJson(p), new UTF8Encoding(false));
            }
            catch (Exception ex) { Debug.LogWarning("[SubidaDeBug] No se pudo dejar el pendiente: " + ex.Message); }
        }

        static void BorrarPendiente(int n)
        {
            try
            {
                string j = Path.Combine(CarpetaPendientes, $"{n:000}.json");
                if (!File.Exists(j)) return;
                var p = JsonUtility.FromJson<Pendiente>(File.ReadAllText(j, Encoding.UTF8));
                if (p != null && !string.IsNullOrEmpty(p.imagen)) File.Delete(Path.Combine(CarpetaPendientes, p.imagen));
                if (p != null && !string.IsNullOrEmpty(p.estado)) File.Delete(Path.Combine(CarpetaPendientes, p.estado));
                File.Delete(j);
            }
            catch { }
        }

        static string Recortar(string s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
    }
}
