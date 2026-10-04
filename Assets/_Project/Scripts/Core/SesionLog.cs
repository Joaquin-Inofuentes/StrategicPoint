using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Combat;
using SP.Player;
using SP.Vehicles;

namespace SP.Core
{
    // Una mision (o cualquier sistema) con estado propio que entra en el registro de sesion y se puede restaurar.
    public interface IEstadoGuardable
    {
        string ClaveEstado { get; }
        void CapturarEstado(Dictionary<string, string> kv);
        void RestaurarEstado(Dictionary<string, string> kv);
    }

    [Serializable]
    public class SnapSoldado
    {
        public string nombre, equipo, rol, estadoIa, objetivo, vehiculo, asiento, arma;
        public bool activo, vivo, poseido, agachado, enCobertura, quieto, pasivo;
        public int vida, vidaMax, municion;
        public Vector3 pos;
        public float yaw;
    }

    [Serializable]
    public class SnapVehiculo
    {
        public string nombre, bando;
        public bool activo, destruido;
        public int vida, vidaMax;
        public Vector3 pos;
        public Vector3 rot;
        public List<string> ocupantes = new List<string>();
    }

    [Serializable]
    public class SnapClave { public string k, v; }

    [Serializable]
    public class SnapJugador
    {
        public string soldado, modo, asiento;
        public Vector3 pos, camPos, camRot;
        public float yaw, pitch, fov;
    }

    // Todo lo necesario para "volver a ese momento": se escribe como JSON en cada bug marcado y sirve de base para guardar partida.
    [Serializable]
    public class EstadoDeSesion
    {
        public int version = 1;
        public int numero;
        public string nota, escena, fecha, resumen;
        public float tiempoSesion, tiempoJuego;
        public SnapJugador jugador = new SnapJugador();
        public List<SnapSoldado> soldados = new List<SnapSoldado>();
        public List<SnapVehiculo> vehiculos = new List<SnapVehiculo>();
        public List<SnapClave> mision = new List<SnapClave>();
        // Pedido: "que el reporte sea mas completo, fijate que datos te faltaron y agregalos". Todo lo que hizo falta para
        // diagnosticar los bugs 8-21: orden/camino/blanco de cada aliado, cazadores, curacion, minimapa, mira, seleccion RTS,
        // fps/timeScale, indicadores y las ultimas lineas del registro.
        public string captura;
        public List<string> diagnostico = new List<string>();
        public List<string> ultimasLineas = new List<string>();
    }

    // REGISTRO DE SESION. Pedido explicito: "un log txt de los pasos que hice (movimiento del soldado, disparos y en que direccion,
    // ordenes dadas a quien y que, posicion, soldado elegido) cada 5 segundos, y el estado de TODO, para poder recrear los momentos
    // de bug y para guardar partida. Al apretar [8] o [U] se registra ese punto como un bug con un numero".
    //
    //  - Registros/sesion_<escena>_<fecha>.txt : bitacora. Un bloque cada 5 s con jugador, acciones del lapso, aliados, enemigos,
    //    vehiculos y mision, mas lineas sueltas de eventos (ordenes, posesiones, bajas, fases) cuando ocurren.
    //  - [U] / [8]: foto del estado (JSON) + captura de la camara + linea en bugs.txt + cartel con el NUMERO de bug.
    //  - SesionLog.RestaurarBug(n) deja la partida como estaba en ese bug (soldados, vida, mision, vehiculos).
    public class SesionLog : MonoBehaviour
    {
        public const float Intervalo = 5f;
        public static SesionLog Instancia { get; private set; }

        // Costura de pruebas: la suite / el CLI no tienen teclado real.
        public static bool PruebaMarcarBug;
        // El dialogo de reporte ([U]/[8]) tiene el juego en pausa y el teclado es suyo (PlayerInputDriver/PauseController no leen nada).
        public static bool DialogoAbierto => ReporteDeBug.Abierto;
        // Numero que va a tener el proximo bug (sin consumirlo): lo muestra el indicador y el titulo del dialogo.
        public static int ProximoNumeroDeBug
        {
            get
            {
                int n = 0;
                try { string f = Path.Combine(Carpeta, "contador_bugs.txt"); if (File.Exists(f)) int.TryParse(File.ReadAllText(f).Trim(), out n); } catch { }
                return n + 1;
            }
        }
        public static int UltimoBug { get; private set; }
        public static string UltimoArchivoBug { get; private set; }

        public static string Carpeta
        {
            get
            {
                string baseDir = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..")) : Application.persistentDataPath;
                return Path.Combine(baseDir, "Registros");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto()
        {
            if (Instancia != null) return;
            var go = new GameObject("SesionLog");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<SesionLog>();
            ReporteDeBug.Asegurar();
        }

        // --- API estatica ---
        // Todo lo que se escribe usa formato numerico invariante ("12.5", no "12,5"): una coma decimal volvia ilegible la posicion
        // "(-2,5,0,8,-372,0)" y el archivo no se podia volver a leer para recrear un bug.
        sealed class CulturaInvariante : System.IDisposable
        {
            readonly System.Globalization.CultureInfo previa;
            public CulturaInvariante()
            {
                previa = System.Globalization.CultureInfo.CurrentCulture;
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            }
            public void Dispose() { System.Globalization.CultureInfo.CurrentCulture = previa; }
        }

        public static void Evento(string texto)
        {
            if (Instancia != null) Instancia.EscribirLinea("EVENTO", texto);
        }

        // Una orden dada por el jugador (o por un sistema en su nombre): "quien" recibe "que".
        public static void Orden(string que, IEnumerable<Soldier> destinatarios, string detalle = null)
        {
            if (Instancia == null) return;
            var sb = new StringBuilder();
            sb.Append(que).Append(" -> ");
            bool primero = true;
            if (destinatarios != null)
                foreach (var s in destinatarios) { if (s == null) continue; if (!primero) sb.Append(", "); sb.Append(s.DisplayName); primero = false; }
            if (primero) sb.Append("(nadie)");
            if (!string.IsNullOrEmpty(detalle)) sb.Append(" | ").Append(detalle);
            Instancia.RegistrarOrden(sb.ToString());
        }

        public static void Orden(string que, Soldier destinatario, string detalle = null)
            => Orden(que, new[] { destinatario }, detalle);

        // --- estado ---
        string archivo;
        StreamWriter escritor;
        float inicioSesion;
        float proximoBloque;
        int bloques;
        readonly List<string> ordenesDelLapso = new List<string>();
        readonly List<string> disparosDelLapso = new List<string>();
        int disparosTotalesLapso;
        float metrosLapso;
        Soldier ultimoPoseido;
        Vector3 ultimaPos;
        readonly List<string> posesionesDelLapso = new List<string>();
        readonly List<string> bajasDelLapso = new List<string>();
        IDisposable subShot, subDeath, subPoss;
        string escenaActual = "";

        void OnEnable()
        {
            subShot = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShot);
            subDeath = EventBus.Instance.Subscribe<EntityDiedEvent>(OnDeath);
            subPoss = EventBus.Instance.Subscribe<PossessionChangedEvent>(OnPossession);
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDisable()
        {
            subShot?.Dispose(); subDeath?.Dispose(); subPoss?.Dispose();
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Cerrar();
        }

        void OnApplicationQuit() => Cerrar();

        void Cerrar()
        {
            try { escritor?.Flush(); escritor?.Dispose(); } catch { }
            escritor = null;
        }

        void OnSceneChanged(Scene a, Scene b)
        {
            Cerrar();
            archivo = null;
        }

        bool Abrir()
        {
            if (escritor != null) return true;
            try
            {
                Directory.CreateDirectory(Carpeta);
                escenaActual = SceneManager.GetActiveScene().name;
                archivo = Path.Combine(Carpeta, $"sesion_{escenaActual}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                escritor = new StreamWriter(archivo, false, new UTF8Encoding(false)) { AutoFlush = true };
                inicioSesion = Time.unscaledTime;
                proximoBloque = Time.unscaledTime + 1f;
                escritor.WriteLine("# REGISTRO DE SESION - Strategic Point");
                escritor.WriteLine($"# escena={escenaActual} fecha={DateTime.Now:yyyy-MM-dd HH:mm:ss} unity={Application.unityVersion} intervalo={Intervalo}s");
                escritor.WriteLine("# Formato: bloque cada 5 s (JUGADOR / ACCIONES / ALIADOS / ENEMIGOS / VEHICULOS / MISION) + lineas sueltas [EVENTO|ORDEN|BAJA|POSESION|BUG].");
                escritor.WriteLine("# Para volver a un momento: SesionLog.RestaurarBug(n) (n = numero de bug marcado con [U] o [8]).");
                escritor.WriteLine();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SesionLog] No se pudo abrir el registro: " + e.Message);
                return false;
            }
        }

        string Reloj => $"T+{Time.unscaledTime - inicioSesion:0000.0}s juego={Time.time:0000.0}";

        // Las ultimas lineas sueltas (eventos, ordenes, bajas...): van completas en cada reporte de bug.
        public static readonly List<string> UltimasLineas = new List<string>();
        const int MaximoDeUltimasLineas = 60;

        void EscribirLinea(string tipo, string texto)
        {
            string linea;
            using (new CulturaInvariante()) linea = $"[{Reloj}] {tipo}: {texto}";
            UltimasLineas.Add(linea);
            if (UltimasLineas.Count > MaximoDeUltimasLineas) UltimasLineas.RemoveAt(0);
            if (!Abrir()) return;
            try { escritor.WriteLine(linea); } catch { }
        }

        void RegistrarOrden(string texto)
        {
            ordenesDelLapso.Add($"[{Time.unscaledTime - inicioSesion:0000.0}s] {texto}");
            EscribirLinea("ORDEN", texto);
        }

        // ---- eventos ----
        void OnShot(ShotFiredEvent e)
        {
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (yo == null || e.ShooterId != yo.Id) return;
            disparosTotalesLapso++;
            if (disparosDelLapso.Count >= 12) return;
            using var _c = new CulturaInvariante();
            var muzzle = yo.Weapon != null ? yo.Weapon.Muzzle : null;
            Vector3 dir = muzzle != null ? muzzle.forward : yo.transform.forward;
            string a = drv.UltimaMira.Type.ToString();
            if (drv.UltimaMira.Soldier != null) a += ":" + drv.UltimaMira.Soldier.DisplayName;
            disparosDelLapso.Add($"dir=({dir.x:0.00},{dir.y:0.00},{dir.z:0.00}) yaw={Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg:0}° apunta={a}");
        }

        void OnDeath(EntityDiedEvent e)
        {
            var s = ActorRegistry.FindById(e.ActorId);
            if (s == null) return;
            using var _c = new CulturaInvariante();
            string quien = s.Health != null && s.Health.LastAttackerId >= 0 ? (ActorRegistry.FindById(s.Health.LastAttackerId)?.DisplayName ?? "?") : "?";
            string txt = $"{s.DisplayName} ({s.Team}/{s.Role}) en ({s.transform.position.x:0.0},{s.transform.position.z:0.0}) por {quien}";
            bajasDelLapso.Add(txt);
            EscribirLinea("BAJA", txt);
        }

        void OnPossession(PossessionChangedEvent e)
        {
            var a = ActorRegistry.FindById(e.FromId);
            var b = ActorRegistry.FindById(e.ToId);
            string txt = $"{(a != null ? a.DisplayName : "-")} -> {(b != null ? b.DisplayName : "-")}";
            posesionesDelLapso.Add(txt);
            EscribirLinea("POSESION", "soldado elegido: " + txt);
        }

        // ---- frame ----
        void Update()
        {
            var kb = Keyboard.current;
            if (PruebaMarcarBug) { PruebaMarcarBug = false; MarcarBug(); }
            if (!DialogoAbierto && kb != null && (KeyBindings.WasPressed(KeyBindings.MarcarBug) || kb.digit8Key.wasPressedThisFrame))
            {
                // Con el radial abierto los numeros eligen categoria: no es un pedido de bug.
                var radial = SP.UI.MenuDeOrdenes.Activo;
                if (!(radial != null && radial.Abierto)) { ReporteDeBug.Asegurar(); ReporteDeBug.Abrir(); }
            }
            ActualizarIndicadorDeBug();

            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (yo != null)
            {
                if (yo == ultimoPoseido) { var d = yo.transform.position - ultimaPos; d.y = 0f; if (d.magnitude < 6f) metrosLapso += d.magnitude; }
                ultimoPoseido = yo; ultimaPos = yo.transform.position;
            }

            if (ActorRegistry.All.Count == 0) return;
            if (!Abrir()) return;
            if (Time.timeScale > 0f && Time.unscaledTime >= proximoBloque)
            {
                proximoBloque = Time.unscaledTime + Intervalo;
                EscribirBloque();
            }
        }

        // ---------------------------------------------------------------
        // Bloque de 5 s
        // ---------------------------------------------------------------
        static string V(Vector3 p) => $"({p.x:0.0},{p.y:0.0},{p.z:0.0})";

        string LineaSoldado(Soldier s)
        {
            var b = s.Brain;
            string tgt = b != null && b.CurrentTarget != null ? b.CurrentTarget.DisplayName : "-";
            string st = b != null ? b.State.ToString() : "-";
            var extra = new StringBuilder();
            if (b != null)
            {
                if (b.EnCobertura) extra.Append(" cobertura");
                else if (b.YendoACobertura) extra.Append(" yendoACobertura");
                if (b.Quieto) extra.Append(" quieto");
                if (b.Pasivo) extra.Append(" pasivo");
                if (b.SiguiendoAlJugador) extra.Append(" siguiendoAuto");
                if (b.FollowTarget != null) extra.Append(" sigueA=" + b.FollowTarget.DisplayName);
                var dest = b.CurrentOrderDestination;
                if (dest.HasValue) extra.Append(" ordenIr=" + V(dest.Value));
                if (b.MontadoEnVehiculo) extra.Append(" enVehiculo");
            }
            if (s.Motor != null && s.Motor.IsCrouching) extra.Append(" agachado");
            return $"  {s.DisplayName + (s.Team == TeamId.Enemy ? "#" + s.Id : ""),-24} {s.Role,-9} hp={s.Health.Current}/{s.Health.MaxHealth} pos={V(s.transform.position)} yaw={s.transform.eulerAngles.y:0} ia={st} obj={tgt}{extra}";
        }

        void EscribirBloque()
        {
            using var _c = new CulturaInvariante();
            bloques++;
            var sb = new StringBuilder(4096);
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            sb.AppendLine($"=== BLOQUE {bloques} [{Reloj} | escena {SceneManager.GetActiveScene().name}] ===");

            // JUGADOR
            if (yo != null)
            {
                var rig = drv.Rig;
                string modo = rig != null ? rig.Mode.ToString() : "?";
                string asiento = drv.CurrentSeat.HasValue ? drv.CurrentSeat.Value.ToString() : "-";
                float pitch = rig != null && rig.Cam != null ? NormalizarAngulo(rig.Cam.transform.eulerAngles.x) : 0f;
                sb.AppendLine($"JUGADOR: {yo.DisplayName} ({yo.Role}) hp={yo.Health.Current}/{yo.Health.MaxHealth} pos={V(yo.transform.position)} yaw={yo.transform.eulerAngles.y:0} pitch={pitch:0} modo={modo} asiento={asiento} municion={(yo.Weapon != null ? yo.Weapon.CurrentAmmo : 0)} apunta={drv.UltimaMira.Type}{(drv.UltimaMira.Soldier != null ? ":" + drv.UltimaMira.Soldier.DisplayName : "")}");
            }
            else sb.AppendLine("JUGADOR: (ninguno poseido)");

            // ACCIONES del lapso
            sb.AppendLine($"ACCIONES (ultimos {Intervalo:0}s): camino {metrosLapso:0.0} m | disparos={disparosTotalesLapso} | ordenes={ordenesDelLapso.Count} | cambios de soldado={posesionesDelLapso.Count} | bajas={bajasDelLapso.Count}");
            foreach (var d in disparosDelLapso) sb.AppendLine("   disparo: " + d);
            foreach (var o in ordenesDelLapso) sb.AppendLine("   orden: " + o);
            foreach (var p in posesionesDelLapso) sb.AppendLine("   eligio: " + p);
            foreach (var m in bajasDelLapso) sb.AppendLine("   baja: " + m);
            metrosLapso = 0f; disparosTotalesLapso = 0;
            disparosDelLapso.Clear(); ordenesDelLapso.Clear(); posesionesDelLapso.Clear(); bajasDelLapso.Clear();

            // ALIADOS / ENEMIGOS
            var aliados = new List<Soldier>(); var enemigos = new List<Soldier>(); int reservas = 0; int civiles = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null) continue;
                if (!s.gameObject.activeInHierarchy && s.Health.IsAlive && s.Team == TeamId.Enemy) { reservas++; continue; }
                if (s.Role == RoleType.Civilian) { civiles++; }
                if (s.Team == TeamId.Player) aliados.Add(s); else if (s.Health.IsAlive) enemigos.Add(s);
            }
            sb.AppendLine($"ALIADOS ({aliados.Count}):");
            foreach (var s in aliados) sb.AppendLine(LineaSoldado(s) + (s.Health.IsAlive ? "" : " MUERTO"));
            sb.AppendLine($"ENEMIGOS ({enemigos.Count} vivos, {reservas} en reserva):");
            foreach (var s in enemigos) sb.AppendLine(LineaSoldado(s));

            // VEHICULOS
            var vs = FindObjectsByType<Vehicle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine($"VEHICULOS ({vs.Length}):");
            foreach (var v in vs)
            {
                var occ = new List<string>();
                foreach (var o in v.Occupants) if (o != null) occ.Add(o.DisplayName + ":" + v.RoleOf(o));
                sb.AppendLine($"  {v.name,-20} bando={v.Bando} hp={(v.Health != null ? v.Health.Current : 0)}/{(v.Health != null ? v.Health.MaxHealth : 0)} pos={V(v.transform.position)} yaw={v.transform.eulerAngles.y:0}{(v.IsDestroyed ? " DESTRUIDO" : "")} ocupantes=[{string.Join(",", occ)}]");
            }

            // MISION
            var kv = new Dictionary<string, string>();
            CapturarMision(kv);
            sb.AppendLine("MISION:");
            foreach (var par in kv) sb.AppendLine($"  {par.Key} = {par.Value}");
            sb.AppendLine();

            try { escritor.Write(sb.ToString()); } catch { }
        }

        static float NormalizarAngulo(float a) => a > 180f ? a - 360f : a;

        // Bug #052: FindObjectsByType<MonoBehaviour>(Include) cada 5 s era un tiron de ~8 ms. Los guardables se buscan cada 30 s.
        static readonly List<IEstadoGuardable> guardables = new List<IEstadoGuardable>();
        static float proximaBusquedaDeGuardables = -1f;

        static void CapturarMision(Dictionary<string, string> kv)
        {
            if (Time.unscaledTime >= proximaBusquedaDeGuardables || guardables.Exists(x => x == null || (x is UnityEngine.Object o && o == null)))
            {
                proximaBusquedaDeGuardables = Time.unscaledTime + 30f;
                guardables.Clear();
                foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (m is IEstadoGuardable gg) guardables.Add(gg);
            }
            foreach (var g in guardables)
            {
                {
                    var tmp = new Dictionary<string, string>();
                    g.CapturarEstado(tmp);
                    foreach (var p in tmp) kv[g.ClaveEstado + "." + p.Key] = p.Value;
                }
            }
            if (SP.Mision.MisionDirector.Activo) kv["MisionDirector.fase"] = SP.Mision.MisionDirector.Instancia.Fase.ToString();
        }

        // ---------------------------------------------------------------
        // Foto del estado (guardar partida / bug)
        // ---------------------------------------------------------------
        public EstadoDeSesion Capturar(string nota = "")
        {
            var e = new EstadoDeSesion
            {
                nota = nota ?? "",
                escena = SceneManager.GetActiveScene().name,
                fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                tiempoSesion = Time.unscaledTime - inicioSesion,
                tiempoJuego = Time.time,
            };
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (yo != null)
            {
                e.jugador.soldado = yo.DisplayName;
                e.jugador.pos = yo.transform.position;
                e.jugador.yaw = yo.transform.eulerAngles.y;
                e.jugador.modo = drv.Rig != null ? drv.Rig.Mode.ToString() : "?";
                e.jugador.asiento = drv.CurrentSeat.HasValue ? drv.CurrentSeat.Value.ToString() : "";
            }
            var cam = CamaraPrincipal.Actual;
            if (cam != null)
            {
                e.jugador.camPos = cam.transform.position;
                e.jugador.camRot = cam.transform.eulerAngles;
                e.jugador.fov = cam.fieldOfView;
                e.jugador.pitch = NormalizarAngulo(cam.transform.eulerAngles.x);
            }
            foreach (var s in ActorRegistry.All)
            {
                if (s == null) continue;
                var b = s.Brain;
                var ss = new SnapSoldado
                {
                    nombre = s.name,
                    equipo = s.Team.ToString(),
                    rol = s.Role.ToString(),
                    activo = s.gameObject.activeSelf,
                    vivo = s.Health.IsAlive,
                    vida = s.Health.Current,
                    vidaMax = s.Health.MaxHealth,
                    pos = s.transform.position,
                    yaw = s.transform.eulerAngles.y,
                    poseido = s == yo,
                    agachado = s.Motor != null && s.Motor.IsCrouching,
                    municion = s.Weapon != null ? s.Weapon.CurrentAmmo : 0,
                    arma = s.Weapon != null ? s.Weapon.CurrentWeaponKind.ToString() : "",
                };
                if (b != null)
                {
                    ss.estadoIa = b.State.ToString();
                    ss.objetivo = b.CurrentTarget != null ? b.CurrentTarget.name : "";
                    ss.enCobertura = b.EnCobertura; ss.quieto = b.Quieto; ss.pasivo = b.Pasivo;
                }
                e.soldados.Add(ss);
            }
            foreach (var v in FindObjectsByType<Vehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var sv = new SnapVehiculo
                {
                    nombre = v.name, bando = v.Bando.ToString(), activo = v.gameObject.activeSelf, destruido = v.IsDestroyed,
                    vida = v.Health != null ? v.Health.Current : 0, vidaMax = v.Health != null ? v.Health.MaxHealth : 0,
                    pos = v.transform.position, rot = v.transform.eulerAngles,
                };
                foreach (var o in v.Occupants) if (o != null) sv.ocupantes.Add(o.name + ":" + v.RoleOf(o));
                e.vehiculos.Add(sv);
            }
            var kv = new Dictionary<string, string>();
            CapturarMision(kv);
            foreach (var p in kv) e.mision.Add(new SnapClave { k = p.Key, v = p.Value });

            int vivosE = 0, vivosA = 0;
            foreach (var s in e.soldados) { if (!s.vivo || !s.activo) continue; if (s.equipo == "Enemy") vivosE++; else vivosA++; }
            string fase = kv.TryGetValue("Operacion.fase", out var f) ? f : (kv.TryGetValue("MisionDirector.fase", out var f2) ? f2 : "?");
            e.resumen = $"fase={fase} | {e.jugador.soldado} en {V(e.jugador.pos)} | aliados vivos={vivosA} enemigos vivos={vivosE} | vehiculos={e.vehiculos.Count}";
            using (new CulturaInvariante())
            {
                try { Diagnosticar(e.diagnostico); } catch (Exception ex) { e.diagnostico.Add("(error armando diagnostico: " + ex.Message + ")"); }
            }
            e.ultimasLineas.AddRange(UltimasLineas);
            return e;
        }

        static float fpsSuave = 60f;

        static void Diagnosticar(List<string> d)
        {
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            d.Add($"TIEMPO: timeScale={Time.timeScale:0.00} fps~{fpsSuave:0} frame={Time.frameCount} juego={Time.time:0.0}s");
            if (drv != null)
            {
                var m = drv.UltimaMira;
                string mira = m.Type + (m.Soldier != null ? ":" + m.Soldier.DisplayName : "") + (m.Vehicle != null ? ":" + m.Vehicle.name : "");
                d.Add($"JUGADOR: soldado={(yo != null ? yo.DisplayName : "-")} vivo={(yo != null && yo.Health.IsAlive)} modo={(drv.Rig != null ? drv.Rig.Mode.ToString() : "?")} asiento={(drv.CurrentSeat.HasValue ? drv.CurrentSeat.Value.ToString() : "-")} mira={mira} punto={V(m.Point)} cursorInteractivo={drv.MiraInteractivaActual}");
                if (drv.Selection != null)
                {
                    var sel = new List<string>();
                    foreach (var s in drv.Selection.Selected) if (s != null) sel.Add(s.DisplayName);
                    d.Add($"SELECCION RTS ({sel.Count}): {string.Join(", ", sel)}");
                }
            }
            // Aliados: orden, camino, blanco, distancia a vos.
            d.Add("ALIADOS:");
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian) continue;
                float dist = yo != null ? Vector3.Distance(s.transform.position, yo.transform.position) : -1f;
                string orden = s.Brain != null ? s.Brain.DescribirOrden() : "-";
                string estado = s.Brain != null ? s.Brain.State.ToString() : "-";
                d.Add($"  {s.DisplayName} ({s.Role}) hp={s.Health.Current}/{s.Health.MaxHealth}{(s.Health.IsAlive ? "" : " CAIDO")}{(s.gameObject.activeInHierarchy ? "" : " INACTIVO")} pos={V(s.transform.position)} aTuLado={dist:0.0}m ia={estado} {orden}{(s.Brain != null ? " regresosForzados=" + s.Brain.RegresosForzados : "")}");
            }
            // Enemigos: activos/reserva, cazadores, distancia al aliado mas cercano.
            int activos = 0, reserva = 0, quietos = 0;
            var lineasE = new List<string>();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive) continue;
                if (!s.gameObject.activeInHierarchy) { reserva++; continue; }
                activos++;
                bool caza = SP.Operacion.CazaDeEnemigos.EsCazador(s);
                var cerca = ActorRegistry.FindNearestEnemyInRange(s.transform.position, TeamId.Enemy, 999f);
                float dc = cerca != null ? Vector3.Distance(cerca.transform.position, s.transform.position) : -1f;
                bool quieto = s.Brain != null && s.Brain.CurrentTarget == null && !s.Brain.TieneOrden && s.Brain.State != AiState.Chase;
                if (quieto && !caza) quietos++;
                if (lineasE.Count < 40)
                    lineasE.Add($"  {s.DisplayName}#{s.Id} pos={V(s.transform.position)} ia={(s.Brain != null ? s.Brain.State.ToString() : "-")} blanco={(s.Brain != null && s.Brain.CurrentTarget != null ? s.Brain.CurrentTarget.DisplayName : "-")} cazador={caza} aliadoMasCerca={dc:0}m{(s.Brain != null ? " patrulla=" + s.Brain.PatrolCount : "")}");
            }
            d.Add($"ENEMIGOS: activos={activos} reserva={reserva} cazadores={SP.Operacion.CazaDeEnemigos.Cazadores} rumbosDados={SP.Operacion.CazaDeEnemigos.RumbosDados} quietosSinCazar={quietos}");
            d.AddRange(lineasE);
            // Curacion / reanimacion.
            d.Add($"CURACION: activo={PedidoDeCuracion.Activo} herido={(PedidoDeCuracion.Herido != null ? PedidoDeCuracion.Herido.DisplayName : "-")} enfermero={(PedidoDeCuracion.Enfermero != null ? PedidoDeCuracion.Enfermero.DisplayName : "-")} reanimando={PedidoDeCuracion.Reanimando} progreso={PedidoDeCuracion.ProgresoDeReanimar:0.00} enCola={PedidoDeCuracion.EnCola} curandoAlJugador={PedidoDeCuracion.CurandoAlJugador(yo)} automatica={PedidoDeCuracion.AtencionAutomatica} aviso=\"{SP.Presentation.AvisoCentral.TextoActual}\"");
            var ind = SP.Presentation.IndicadorDeCaidos.Instancia;
            d.Add($"CAIDOS: marcasVisibles={(ind != null ? ind.MarcasVisibles : 0)}");
            // Minimapa.
            var mm = SP.UI.MinimapFollow.Activo;
            var iconos = FindObjectsByType<SP.Presentation.MinimapIcon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int parpadeando = 0;
            foreach (var ic in iconos) if (ic.CaidoParpadeando) parpadeando++;
            var mcam = mm != null ? mm.MinimapCamera : null;
            d.Add($"MINIMAPA: activo={(mm != null)} sigueA={(mm != null && mm.Target != null ? mm.Target.name : "-")} ortho={(mcam != null ? mcam.orthographicSize : 0f):0.0} camaraActiva={(mcam != null && mcam.enabled)} iconos={iconos.Length} parpadeando={parpadeando} agrandado={(mm != null && mm.Agrandado)}");
            // Vehiculos: torretas.
            foreach (var t in FindObjectsByType<TurretWeapon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                d.Add($"TORRETA {t.transform.root.name}/{t.transform.parent?.name}: pitch={t.PitchActual:0.0} (limites {t.MinPitch:0}/{t.MaxPitch:0}) yaw={t.transform.eulerAngles.y:0} calor={t.Heat:0.00}");
            NavDiag(d);
        }

        static void NavDiag(List<string> d)
        {
            int conAgente = 0, sinCamino = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive) continue;
                var ag = s.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag == null || !ag.enabled) continue;
                conAgente++;
                if (ag.isOnNavMesh && ag.hasPath == false && ag.pathPending == false && s.Brain != null && s.Brain.TieneOrden) sinCamino++;
            }
            d.Add($"NAV: agentes={conAgente} conOrdenSinCamino={sinCamino}");
        }

        // ---- indicador "proximo bug" (bug #11: "falta indicador de que bug voy") ----
        Text indicador; float proximoIndicador;

        void ActualizarIndicadorDeBug()
        {
            fpsSuave = Mathf.Lerp(fpsSuave, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.05f);
            if (Time.unscaledTime < proximoIndicador) return;
            proximoIndicador = Time.unscaledTime + 1f;
            bool hayJuego = PlayerInputDriver.Activo != null;
            if (indicador == null)
            {
                if (!hayJuego) return;
                var go = new GameObject("SesionLog_Indicador", typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(transform, false);
                var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 5900;
                var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280f, 720f);
                indicador = NuevoTexto(go.transform, "Texto", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 12, FontStyle.Bold, new Color(1f, 0.82f, 0.25f, 0.75f), Vector2.zero, new Vector2(260f, 18f));
                indicador.alignment = TextAnchor.LowerLeft;
                indicador.raycastTarget = false;
                var rt = indicador.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f); rt.anchoredPosition = new Vector2(8f, 4f);
                var sombra = indicador.gameObject.AddComponent<Shadow>(); sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
            }
            indicador.transform.parent.gameObject.SetActive(hayJuego);
            int ultimo = ProximoNumeroDeBug - 1;
            indicador.text = $"[U] reportar BUG #{ProximoNumeroDeBug:000}" + (ultimo > 0 ? $"  · ultimo: #{ultimo:000}" : "");
        }

        public static string TextoIndicadorDeBug => Instancia != null && Instancia.indicador != null ? Instancia.indicador.text : "";

        // ---------------------------------------------------------------
        // Marcar bug
        // ---------------------------------------------------------------
        static int SiguienteNumeroDeBug()
        {
            string f = Path.Combine(Carpeta, "contador_bugs.txt");
            int n = 0;
            try { if (File.Exists(f)) int.TryParse(File.ReadAllText(f).Trim(), out n); } catch { }
            n++;
            try { Directory.CreateDirectory(Carpeta); File.WriteAllText(f, n.ToString()); } catch { }
            return n;
        }

        public static int MarcarBug(string nota = "")
        {
            if (Instancia == null) Auto();
            return Instancia != null ? Instancia.MarcarBugInterno(nota, null, null) : -1;
        }

        // Desde el dialogo: la captura y el estado ya se tomaron al apretar [U] (antes de la pausa y del dialogo).
        public static int MarcarBugConCaptura(string nota, Texture2D captura, EstadoDeSesion estado)
        {
            if (Instancia == null) Auto();
            return Instancia != null ? Instancia.MarcarBugInterno(nota, captura, estado) : -1;
        }

        int MarcarBugInterno(string nota, Texture2D captura, EstadoDeSesion previo)
        {
            using var _c = new CulturaInvariante();
            Abrir();
            int n = SiguienteNumeroDeBug();
            UltimoBug = n;
            var e = previo ?? Capturar(nota);
            e.nota = nota ?? "";
            e.numero = n;
            string baseName = $"bug_{n:000}_{e.escena}_{DateTime.Now:HHmmss}";
            string json = Path.Combine(Carpeta, baseName + ".json");
            string png = Path.Combine(Carpeta, baseName + ".png");
            UltimoArchivoBug = json;
            e.captura = Path.GetFileName(png);
            bool capturaGuardada = false;
            if (captura != null)
            {
                try { Directory.CreateDirectory(Carpeta); File.WriteAllBytes(png, captura.EncodeToPNG()); capturaGuardada = true; }
                catch (Exception ex) { Debug.LogWarning("[SesionLog] No se pudo guardar la captura: " + ex.Message); }
            }
            try
            {
                Directory.CreateDirectory(Carpeta);
                File.WriteAllText(json, JsonUtility.ToJson(e, true), new UTF8Encoding(false));
                File.AppendAllText(Path.Combine(Carpeta, "bugs.txt"),
                    $"BUG #{n:000} | {e.fecha} | escena {e.escena} | sesion T+{e.tiempoSesion:0.0}s | {e.resumen} | nota: {nota} | estado: {Path.GetFileName(json)} | captura: {Path.GetFileName(png)} | log: {(archivo != null ? Path.GetFileName(archivo) : "-")}{Environment.NewLine}",
                    new UTF8Encoding(false));
            }
            catch (Exception ex) { Debug.LogWarning("[SesionLog] No se pudo guardar el bug: " + ex.Message); }
            EscribirLinea("BUG", $"*** BUG #{n:000} MARCADO *** {e.resumen} | json={Path.GetFileName(json)} | captura={Path.GetFileName(png)}");
            // Se vuelca tambien un bloque completo asi el txt queda con el estado exacto de ese instante.
            if (escritor != null) EscribirBloque();
            StartCoroutine(CapturaYCartel(n, capturaGuardada ? null : png, e));
            return n;
        }

        IEnumerator CapturaYCartel(int n, string png, EstadoDeSesion e)
        {
            // La captura sale ANTES del cartel: el cartel no tiene que aparecer en la imagen del bug.
            if (png != null) { try { ScreenCapture.CaptureScreenshot(png); } catch { } }
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            int enemigos = 0, aliados = 0;
            foreach (var s in e.soldados) { if (!s.vivo || !s.activo) continue; if (s.equipo == "Enemy") enemigos++; else aliados++; }
            string t = $"BUG #{n:000} REGISTRADO";
            string d = $"Se capturo: estado de la partida · posicion de {enemigos} enemigos y {aliados} aliados y la tuya · vida de los soldados · toma de camara · estado de la mision ({e.resumen.Split('|')[0].Trim()})\nDecime \"agarra el bug {n}\" para retomar desde aca";
            MostrarCartel(t, d);
            SP.UI.AlertQueue.Push($"BUG #{n:000} REGISTRADO", SP.UI.AlertPriority.Alta, 2.5f);
            SP.Presentation.AudioDirector.PlayUi2D(SP.Presentation.SfxKind.RadialConfirm, 0.8f, 0.7f);
        }

        // ---- cartel ----
        GameObject cartel; Text cartelTitulo, cartelDetalle; float cartelHasta;

        void MostrarCartel(string titulo, string detalle)
        {
            if (cartel == null)
            {
                cartel = new GameObject("SesionLog_Cartel");
                cartel.transform.SetParent(transform, false);
                var canvas = cartel.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 6000;
                var scaler = cartel.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                var panel = new GameObject("Panel"); panel.transform.SetParent(cartel.transform, false);
                var img = panel.AddComponent<Image>(); img.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);
                var rt = img.rectTransform; rt.anchorMin = new Vector2(0.5f, 0.82f); rt.anchorMax = new Vector2(0.5f, 0.82f); rt.sizeDelta = new Vector2(860f, 120f);
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                cartelTitulo = NuevoTexto(panel.transform, "Titulo", font, 28, FontStyle.Bold, new Color(1f, 0.82f, 0.25f), new Vector2(0f, 38f), new Vector2(840f, 40f));
                cartelDetalle = NuevoTexto(panel.transform, "Detalle", font, 15, FontStyle.Normal, Color.white, new Vector2(0f, -18f), new Vector2(840f, 70f));
            }
            cartelTitulo.text = titulo; cartelDetalle.text = detalle;
            cartel.SetActive(true);
            cartelHasta = Time.unscaledTime + 5f;
            StartCoroutine(OcultarCartel());
        }

        static Text NuevoTexto(Transform padre, string nombre, Font font, int size, FontStyle style, Color color, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(nombre); go.transform.SetParent(padre, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchoredPosition = pos; rt.sizeDelta = dim;
            return t;
        }

        IEnumerator OcultarCartel()
        {
            while (Time.unscaledTime < cartelHasta) yield return null;
            if (cartel != null) cartel.SetActive(false);
        }

        // ---------------------------------------------------------------
        // Restaurar
        // ---------------------------------------------------------------
        public static string ArchivoDeBug(int n)
        {
            if (!Directory.Exists(Carpeta)) return null;
            foreach (var f in Directory.GetFiles(Carpeta, $"bug_{n:000}_*.json")) return f;
            return null;
        }

        // Restaura el bug n: si es de otra escena la carga primero. Devuelve un texto con lo que hizo.
        public static string RestaurarBug(int n)
        {
            string f = ArchivoDeBug(n);
            if (f == null) return $"No hay archivo para el bug #{n} en {Carpeta}";
            return RestaurarArchivo(f);
        }

        public static string RestaurarArchivo(string ruta)
        {
            if (Instancia == null) Auto();
            EstadoDeSesion e;
            try { e = JsonUtility.FromJson<EstadoDeSesion>(File.ReadAllText(ruta)); }
            catch (Exception ex) { return "No se pudo leer " + ruta + ": " + ex.Message; }
            if (e == null) return "Archivo vacio: " + ruta;
            if (SceneManager.GetActiveScene().name != e.escena)
            {
                Instancia.StartCoroutine(Instancia.CargarEscenaYRestaurar(e));
                return $"Cargando escena {e.escena} para restaurar el bug #{e.numero}...";
            }
            return Restaurar(e);
        }

        IEnumerator CargarEscenaYRestaurar(EstadoDeSesion e)
        {
            var op = SceneManager.LoadSceneAsync(e.escena);
            while (op != null && !op.isDone) yield return null;
            for (int i = 0; i < 30; i++) yield return null;   // los directores arrancan en sus Start
            var r = Restaurar(e);
            Debug.Log("[SesionLog] " + r);
        }

        public static string Restaurar(EstadoDeSesion e)
        {
            var sb = new StringBuilder();
            sb.Append($"Restaurando bug #{e.numero} ({e.fecha}): {e.resumen}. ");

            // 1) mision primero (activa/desactiva grupos enteros), 2) soldados, 3) vehiculos.
            var kv = new Dictionary<string, string>();
            foreach (var p in e.mision) kv[p.k] = p.v;
            int misiones = 0;
            foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(m is IEstadoGuardable g)) continue;
                var mio = new Dictionary<string, string>();
                string pref = g.ClaveEstado + ".";
                foreach (var p in kv) if (p.Key.StartsWith(pref)) mio[p.Key.Substring(pref.Length)] = p.Value;
                g.RestaurarEstado(mio);
                misiones++;
            }
            sb.Append($"misiones={misiones} ");

            var porNombre = new Dictionary<string, Soldier>();
            foreach (var s in ActorRegistry.All) if (s != null && !porNombre.ContainsKey(s.name)) porNombre[s.name] = s;
            int ok = 0;
            foreach (var ss in e.soldados)
            {
                if (!porNombre.TryGetValue(ss.nombre, out var s)) continue;
                var occupied = s.Brain != null && s.Brain.MontadoEnVehiculo;
                s.gameObject.SetActive(ss.activo);
                if (!ss.activo) { ok++; continue; }
                if (!occupied)
                {
                    s.transform.position = ss.pos;
                    s.transform.rotation = Quaternion.Euler(0f, ss.yaw, 0f);
                }
                s.Health.RestaurarVida(ss.vivo ? ss.vida : 0);
                if (s.Weapon != null) s.Weapon.RestaurarMunicion(ss.municion);
                if (s.Brain != null && ss.vivo && s.Team == TeamId.Player && !ss.poseido) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); s.Brain.Quieto = ss.quieto; }
                else if (s.Brain != null && ss.vivo) { s.Brain.ReactivarNavegacion(); }
                if (s.Motor != null) s.Motor.SetCrouching(ss.agachado);
                ok++;
            }
            sb.Append($"soldados={ok}/{e.soldados.Count} ");

            var vs = new Dictionary<string, Vehicle>();
            foreach (var v in FindObjectsByType<Vehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!vs.ContainsKey(v.name)) vs[v.name] = v;
            int okv = 0;
            foreach (var sv in e.vehiculos)
            {
                if (!vs.TryGetValue(sv.nombre, out var v)) continue;
                v.gameObject.SetActive(sv.activo);
                if (!sv.destruido) { v.transform.position = sv.pos; v.transform.rotation = Quaternion.Euler(sv.rot); }
                if (v.Health != null && !sv.destruido && sv.vida > 0) v.Health.RestaurarVida(sv.vida);
                okv++;
            }
            sb.Append($"vehiculos={okv}/{e.vehiculos.Count} ");

            // jugador: poseer al mismo soldado y mirar para el mismo lado
            var drv = PlayerInputDriver.Activo;
            if (drv != null && !string.IsNullOrEmpty(e.jugador.soldado) && porNombre.TryGetValue(e.jugador.soldado, out var yo) && yo.Health.IsAlive)
            {
                if (drv.Brain.Current != yo) drv.TryPossess(yo);
                if (e.jugador.modo == "Fps" && drv.Rig != null && drv.Rig.Mode != ControlMode.Fps) drv.Rig.SetMode(ControlMode.Fps);
                sb.Append($"jugador={yo.DisplayName}");
            }
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            Coberturas.Registrar();
            string r = sb.ToString();
            Evento("RESTAURADO: " + r);
            return r;
        }
    }
}
