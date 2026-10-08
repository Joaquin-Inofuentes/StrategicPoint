using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Operacion;
using SP.Player;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Validador de jugabilidad de la Operacion Cuartel (Informes/11_Validacion_jugabilidad.md).
    // Un "jugador automatico" que recorre las 6 fases usando las MISMAS APIs publicas que el input real: SetDestination (caminar),
    // PlayerBrain.Fire (disparar), TryPossess (cambiar de soldado), EquipWeaponHotkey (arma), EnviarA (el "INTERACTUAR" del radial),
    // PruebaToqueE / PruebaMantenerE (la tecla [E]), Selection + OrderService (vista tactica) y TurretWeapon.AimAt/TryFire (el canon).
    // No toca la escena ni el balance: solo mira y juega. Log en Temp/jugabilidad.log, resultados en Temp/jugabilidad_result.txt.
    //
    //   eval:  return SP.EditorTools.ValidadorJugabilidad.Iniciar(1, 6, true, false);   // desde, hasta, encadenado (sin saltos), modo dios
    //   poll:  return SP.EditorTools.ValidadorJugabilidad.Estado;
    public static class ValidadorJugabilidad
    {
        public static bool Corriendo { get; private set; }
        public static string Estado { get; private set; } = "inactivo";
        public static string Resultado => resultado.ToString();

        static readonly Stack<IEnumerator> pila = new Stack<IEnumerator>();
        static bool Avanzar()
        {
            while (pila.Count > 0)
            {
                var top = pila.Peek();
                if (top.MoveNext()) { if (top.Current is IEnumerator sub) { pila.Push(sub); continue; } return true; }
                pila.Pop();
            }
            return false;
        }
        static readonly StringBuilder resultado = new StringBuilder();
        static string rutaLog, rutaRes;
        static bool dios, encadenado; static int diosFase;
        static float ultimoLogHud;

        // ---- estadisticas por fase ----
        class Fs
        {
            public int n; public string nombre;
            public float t0, r0; public int disparos0, impactos0, misMuertes0, caidos0, reanim0, bajas0;
            public int muertes, casiMuertes, cambiosDeCuerpo;
            public float quietoMax, quietoAct, quietoTotal, minVidaYo = 1f;
            public int ammoIni, ammoMin = int.MaxValue, ammoFin; public string armaIni = "";
            public int disparosBot, recargas, sinMunicion;
            public bool ok; public string nota = "";
            public float tFin, rFin;
        }
        static Fs fs;
        static readonly Dictionary<int, bool> vivoAntes = new Dictionary<int, bool>();
        static readonly Dictionary<int, bool> heridoGrave = new Dictionary<int, bool>();
        static readonly HashSet<int> yaMarcadas = new HashSet<int>();
        static string fraseDeMuertes = "";

        // ----------------------------------------------------------------- entrada
        public static string Iniciar(int desde = 1, int hasta = 6, bool encadenar = true, bool modoDios = false, int diosSoloFase = 0)
        {
            if (!Application.isPlaying) return "ERROR: hace falta Play en SC_Operacion";
            if (Corriendo) return "ya corriendo";
            if (OperacionDirector.Instancia == null) return "ERROR: sin director";
            var raiz = Path.GetDirectoryName(Application.dataPath);
            rutaLog = Path.Combine(raiz, "Temp", "jugabilidad.log");
            rutaRes = Path.Combine(raiz, "Temp", "jugabilidad_result.txt");
            File.WriteAllText(rutaLog, "");
            File.WriteAllText(rutaRes, "");
            resultado.Clear();
            dios = modoDios; encadenado = encadenar; diosFase = diosSoloFase; ModoDios.Poner(false); revivirT = 0f;
            vivoAntes.Clear(); heridoGrave.Clear(); yaMarcadas.Clear();
            Corriendo = true; Estado = "arrancando";
            pila.Clear(); pila.Push(Principal(desde, hasta, encadenar));
            EditorApplication.update -= Paso;
            EditorApplication.update += Paso;
            return "iniciado " + desde + ".." + hasta + (encadenar ? " encadenado" : " con saltos") + (modoDios ? " DIOS" : "");
        }

        public static void Detener()
        {
            EditorApplication.update -= Paso;
            Corriendo = false; Estado = "detenido";
            var d = PlayerInputDriver.Activo; if (d != null) d.CancelDestination();
            OperacionTerminal.PruebaToqueE = false; OperacionTerminal.PruebaMantenerE = false;
            ModoDios.Poner(false);
            Time.timeScale = 1f;
        }

        static void Paso()
        {
            if (!Application.isPlaying) { Detener(); Estado = "fuera de Play"; return; }
            bool sigue = false;
            try
            {
                Vigilar();
                sigue = Avanzar();
            }
            catch (Exception e) { Log("EXCEPCION: " + e); Estado = "excepcion: " + e.Message; sigue = false; }
            if (sigue) return;
            EditorApplication.update -= Paso;
            Corriendo = false;
            if (!Estado.StartsWith("excepcion")) Estado = "listo";
            File.WriteAllText(rutaRes, resultado.ToString());
            ModoDios.Poner(false);
            OperacionTerminal.PruebaToqueE = false; OperacionTerminal.PruebaMantenerE = false;
        }

        // ----------------------------------------------------------------- utilidades
        static string Inv(FormattableString f) => f.ToString(CultureInfo.InvariantCulture);
        static void Log(string s)
        {
            try { File.AppendAllText(rutaLog, Inv($"[{Time.time:0.0}|{Time.realtimeSinceStartup:0.0}] ") + s + "\n"); } catch { }
        }
        static PlayerInputDriver Drv => PlayerInputDriver.Activo;
        static Soldier Yo { get { var d = Drv; return d != null && d.Brain != null ? d.Brain.Current : null; } }
        static OperacionDirector Dir => OperacionDirector.Instancia;
        static float Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }
        static bool Vivo(Soldier s) => s != null && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy;

        static List<Soldier> Aliados(bool incluirMilicia = false)
        {
            var r = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || !Vivo(s)) continue;
                if (!incluirMilicia && MandoTactico.EsMiliciano(s)) continue;
                r.Add(s);
            }
            return r;
        }
        static Soldier Escuadrista(RoleType rol)
        {
            foreach (var s in Aliados()) if (s.Role == rol) return s;
            return null;
        }

        static string Pos(Vector3 p) => Inv($"{p.x:0},{p.y:0},{p.z:0}");

        static string Hud()
        {
            var h = OperacionHud.Instancia;
            if (h == null) return "(sin hud)";
            return $"[{h.TextoTitulo}] {h.TextoDetalle} | prompt='{h.TextoPrompt}' timer='{h.TextoTimer}'";
        }

        // ----------------------------------------------------------------- vigilancia (cada cuadro)
        static float ultimoDanoYo;
        static void Vigilar()
        {
            if (Time.timeScale < 0.99f && Dir != null && Dir.Fase != FaseOperacion.Victoria && Dir.Fase != FaseOperacion.Derrota) Time.timeScale = 1f;
            bool quiereDios = dios || (diosFase > 0 && fs != null && fs.n == diosFase);
            if (quiereDios != ModoDios.Activo) { ModoDios.Poner(quiereDios); Log("modo dios " + quiereDios); }
            var f = fs;
            // muertes y casi-muertes de toda la escuadra (con milicias)
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null || !s.gameObject.activeInHierarchy) continue;
                int id = s.Id; bool vivo = s.Health.IsAlive;
                if (vivoAntes.TryGetValue(id, out bool antes))
                {
                    if (antes && !vivo) { if (f != null) f.muertes++; Log($"MUERTE de {s.DisplayName} ({s.Role}) en {Pos(s.transform.position)} fase={Dir.Fase}"); }
                    else if (!antes && vivo) Log($"REVIVIDO {s.DisplayName}");
                }
                vivoAntes[id] = vivo;
                float fr = s.Health.Current / (float)Mathf.Max(1, s.Health.MaxHealth);
                heridoGrave.TryGetValue(id, out bool grave);
                if (vivo && fr < 0.25f && !grave) { heridoGrave[id] = true; if (f != null) f.casiMuertes++; Log($"CASI MUERTE {s.DisplayName} hp={s.Health.Current}"); }
                else if (grave && (fr > 0.6f || !vivo)) heridoGrave[id] = false;
            }
            var yo = Yo;
            if (f != null && yo != null && yo.Health != null)
            {
                f.minVidaYo = Mathf.Min(f.minVidaYo, yo.Health.Current / (float)Mathf.Max(1, yo.Health.MaxHealth));
                if (yo.Weapon != null)
                {
                    int a = yo.Weapon.CurrentAmmo + Mathf.Max(0, yo.Weapon.ReservaActual);
                    f.ammoMin = Mathf.Min(f.ammoMin, a); f.ammoFin = a;
                }
                // tramo "quieto": ningun enemigo vivo a menos de 40 m de la escuadra ni de mi
                bool enemigoCerca = false;
                foreach (var e in ActorRegistry.All)
                {
                    if (e == null || e.Team != TeamId.Enemy || !Vivo(e)) continue;
                    if (Plano(e.transform.position, yo.transform.position) < 40f) { enemigoCerca = true; break; }
                }
                if (!enemigoCerca) { f.quietoAct += Time.deltaTime; f.quietoTotal += Time.deltaTime; f.quietoMax = Mathf.Max(f.quietoMax, f.quietoAct); }
                else f.quietoAct = 0f;
            }
            if (Time.time - ultimoLogHud > 10f && Dir != null)
            {
                ultimoLogHud = Time.time;
                var sb = new StringBuilder();
                sb.Append($"HUD fase={Dir.Fase}/{Dir.Subfase} reloj={Dir.Reloj:0.0} yo={(yo != null ? yo.DisplayName + "@" + Pos(yo.transform.position) + " hp=" + yo.Health.Current : "-")}");
                if (yo != null && yo.Weapon != null) sb.Append($" arma={yo.Weapon.CurrentWeaponKind} {yo.Weapon.CurrentAmmo}+{yo.Weapon.ReservaActual} corre={(yo.Motor != null && yo.Motor.Corriendo)}");
                sb.Append(" aliados=" + Aliados(true).Count + " | " + Hud());
                Log(sb.ToString());
            }
        }

        // ----------------------------------------------------------------- control del jugador
        static void MirarA(Soldier yo, Vector3 p)
        {
            var d = p - yo.transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f || PlayerBrain.Activo == null) return;
            float dy = Mathf.DeltaAngle(yo.transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            PlayerBrain.Activo.RotateYaw(dy);
        }

        static readonly NavMeshPath camino = new NavMeshPath();
        static Vector3 caminoMeta = new Vector3(1e9f, 0, 0); static float caminoT = -99f;
        public static bool Correr = true;

        static void PulsarCorrer()
        {
            if (!Correr) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.LeftShift));
        }

        // Camina hacia 'meta' por el NavMesh (SetDestination solo camina en linea recta). Devuelve la distancia plana que falta.
        static float IrA(Vector3 meta)
        {
            var yo = Yo; var drv = Drv;
            if (yo == null || drv == null) return 999f;
            var pos = yo.transform.position;
            float dist = Plano(pos, meta);
            if ((meta - caminoMeta).sqrMagnitude > 4f || Time.time - caminoT > 0.8f)
            {
                caminoMeta = meta; caminoT = Time.time;
                if (NavMesh.SamplePosition(pos, out var hp, 4f, NavMesh.AllAreas) && NavMesh.SamplePosition(meta, out var hm, 8f, NavMesh.AllAreas))
                    NavMesh.CalculatePath(hp.position, hm.position, NavMesh.AllAreas, camino);
                else camino.ClearCorners();
            }
            Vector3 sig = meta;
            var c = camino.corners;
            if (c != null && c.Length >= 2)
            {
                int i = 1;
                while (i < c.Length - 1 && Plano(pos, c[i]) < 1.6f) i++;
                sig = c[i];
            }
            drv.SetDestination(sig);
            PulsarCorrer();
            return dist;
        }

        static float AlcanceDe(Soldier yo)
        {
            if (yo.Weapon == null) return 30f;
            switch (yo.Weapon.CurrentWeaponKind)
            {
                case WeaponKind.Sniper: return 70f;
                case WeaponKind.Rocket: return 45f;
                case WeaponKind.Pistol: return 18f;
                case WeaponKind.Shotgun: return 14f;
                case WeaponKind.Smg: return 30f;
                default: return 40f;
            }
        }

        // Linea de tiro libre desde mis ojos hasta el pecho del blanco (los aliados en medio no cuentan; todo lo demas tapa).
        static bool Linea(Soldier yo, Vector3 destino, Soldier blanco)
        {
            var o = yo.transform.position + Vector3.up * 0.7f;
            var d = destino - o; float dist = d.magnitude;
            if (dist < 0.5f) return true;
            var hits = Physics.RaycastAll(o, d / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var s = h.collider.GetComponentInParent<Soldier>();
                if (s == yo) continue;
                if (s != null && blanco != null && s == blanco) return true;
                if (s != null && s.Team == yo.Team) continue;
                if (blanco == null)
                {
                    var v = h.collider.GetComponentInParent<Vehicle>();
                    if (v != null && Plano(v.transform.position, destino) < 6f) return true;
                }
                return false;
            }
            return true;
        }

        static Soldier ElegirBlanco(Soldier yo, float alcance, Func<Soldier, bool> filtro, out Vector3 punto)
        {
            punto = Vector3.zero;
            Soldier mejor = null; float md = float.MaxValue;
            foreach (var e in ActorRegistry.All)
            {
                if (e == null || e.Team != TeamId.Enemy || !Vivo(e)) continue;
                if (filtro != null && !filtro(e)) continue;
                float d = Plano(e.transform.position, yo.transform.position);
                if (d > alcance || d >= md) continue;
                var p = e.transform.position + Vector3.up * 0.45f;
                if (!Linea(yo, p, e)) continue;
                md = d; mejor = e; punto = p;
            }
            return mejor;
        }

        // Un cuadro de combate: si hay blanco a tiro frena, apunta y dispara; devuelve true si estuvo combatiendo.
        static bool Combatir(Func<Soldier, bool> filtro = null, float alcanceMax = 0f)
        {
            var yo = Yo; var drv = Drv;
            if (yo == null || !yo.Health.IsAlive || drv == null || yo.Weapon == null) return false;
            var w = yo.Weapon;
            if (w.CurrentAmmo <= 0 && !w.IsReloading)
            {
                if (w.SinMunicionTotal)
                {
                    if (fs != null) fs.sinMunicion++;
                    if (!w.CambiarASiguienteConMunicion()) return false;
                }
                else { w.Reload(); if (fs != null) fs.recargas++; }
            }
            float alcance = alcanceMax > 0f ? alcanceMax : AlcanceDe(yo);
            var b = ElegirBlanco(yo, alcance, filtro, out var punto);
            if (b == null) return false;
            drv.CancelDestination();
            MirarA(yo, punto);
            if (w.IsReloading) return true;
            if (PlayerBrain.Activo.Fire(punto) && fs != null) fs.disparosBot++;
            return true;
        }

        // Si el que manejo cayo: espera la camara de muerte y pasa al aliado vivo mas cercano (lo que hace el jugador con [Espacio]).
        static IEnumerator SiMori()
        {
            var yo = Yo; var drv = Drv;
            if (drv == null) yield break;
            if (yo != null && yo.Health.IsAlive && Vivo(yo)) yield break;
            Log("EL JUGADOR CAYO: " + (yo != null ? yo.DisplayName : "?"));
            float t0 = Time.time;
            while (drv.IsHandlingDeath && Time.time - t0 < 8f) yield return null;
            var cand = Aliados(true);
            Soldier mejor = null; float md = 1e9f;
            foreach (var s in cand)
            {
                if (s == yo || s.Brain == null || s.Brain.MontadoEnVehiculo) continue;
                float d = yo != null ? Plano(s.transform.position, yo.transform.position) : 0f;
                if (d < md) { md = d; mejor = s; }
            }
            if (mejor != null)
            {
                drv.TryPossess(mejor);
                if (drv.Rig != null && drv.Rig.Mode != ControlMode.Fps && !(Dir.Fase == FaseOperacion.Resistir && Dir.Subfase >= 1)) drv.Rig.SetMode(ControlMode.Fps);
                if (fs != null) fs.cambiosDeCuerpo++;
                Log("posee a " + mejor.DisplayName);
            }
            yield return null;
        }

        // Mantener [E] 5 s sobre un caido (A4): el jugador camina hasta el y lo revive. false = no hay nadie a quien revivir (o hay peligro).
        static float revivirT;
        static bool Revivir()
        {
            var yo = Yo; var drv = Drv;
            if (yo == null || !Vivo(yo) || drv == null) { revivirT = 0f; return false; }
            Soldier c = null; float md = 35f;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null || s.Health.IsAlive || !s.gameObject.activeInHierarchy || s == yo) continue;
                float d = Plano(s.transform.position, yo.transform.position);
                if (d < md) { md = d; c = s; }
            }
            if (c == null) { revivirT = 0f; return false; }
            if (ElegirBlanco(yo, 22f, null, out _) != null) { revivirT = 0f; return false; }
            if (md > 2.2f) { IrA(c.transform.position); revivirT = 0f; return true; }
            drv.CancelDestination();
            revivirT += Time.deltaTime;
            if (revivirT >= PlayerInputDriver.TiempoDeRevivir) { drv.TryRevivir(c, true); Log("revive a " + c.DisplayName); revivirT = 0f; }
            return true;
        }

        static IEnumerator Esperar(float s) { float t = Time.time; while (Time.time - t < s) yield return null; }

        static bool Poseer(Soldier s)
        {
            var drv = Drv; if (drv == null || s == null || !Vivo(s)) return false;
            if (Yo == s) return true;
            bool ok = drv.TryPossess(s);
            if (ok && drv.Rig != null && drv.Rig.Mode != ControlMode.Fps && !(Dir.Fase == FaseOperacion.Resistir && Dir.Subfase >= 1)) drv.Rig.SetMode(ControlMode.Fps);
            return ok;
        }

        // ----------------------------------------------------------------- estadisticas de fase
        static void IniciarFase(int n, string nombre)
        {
            var e = EstadisticasDeMision.Instancia;
            fs = new Fs { n = n, nombre = nombre, t0 = Time.time, r0 = Time.realtimeSinceStartup };
            if (e != null) { fs.disparos0 = e.Disparos; fs.impactos0 = e.Impactos; fs.misMuertes0 = e.MisMuertes; fs.caidos0 = e.CaidosDeLaEscuadra; fs.reanim0 = e.Reanimaciones; fs.bajas0 = e.BajasEnemigas; }
            var yo = Yo;
            if (yo != null && yo.Weapon != null && yo.Weapon.Loadout.Count > 0 && yo.Weapon.CurrentWeaponKind != yo.Weapon.Loadout[0]) yo.Weapon.EquipFromLoadout(0);   // arranca cada objetivo con el arma principal
            if (yo != null && yo.Weapon != null)
            {
                fs.ammoIni = yo.Weapon.CurrentAmmo + Mathf.Max(0, yo.Weapon.ReservaActual);
                fs.armaIni = yo.Weapon.CurrentWeaponKind.ToString();
            }
            fs.ammoMin = fs.ammoIni;
            Log($"==== FASE {n} {nombre} INICIO · {Hud()}");
        }

        static void CerrarFase(bool ok, string nota = "")
        {
            if (fs == null) return;
            fs.ok = ok; fs.nota = nota; fs.tFin = Time.time; fs.rFin = Time.realtimeSinceStartup;
            var e = EstadisticasDeMision.Instancia;
            int disp = e != null ? e.Disparos - fs.disparos0 : 0, imp = e != null ? e.Impactos - fs.impactos0 : 0;
            int misMuertes = e != null ? e.MisMuertes - fs.misMuertes0 : 0, caidos = e != null ? e.CaidosDeLaEscuadra - fs.caidos0 : 0, rean = e != null ? e.Reanimaciones - fs.reanim0 : 0, bajas = e != null ? e.BajasEnemigas - fs.bajas0 : 0;
            string linea = Inv($"FASE {fs.n} | {fs.nombre} | {(ok ? "COMPLETADA" : "NO COMPLETADA")} | juego {fs.tFin - fs.t0:0.0} s | real {fs.rFin - fs.r0:0.0} s | muertes(mias de escuadra) {fs.muertes} | caidos(estad.) {caidos} | misMuertes {misMuertes} | reanim {rean} | casi-muertes {fs.casiMuertes} | cambios cuerpo {fs.cambiosDeCuerpo} | minVidaYo {fs.minVidaYo:0.00} | bajas {bajas} | disparos(escuadra) {disp} impactos {imp} | disparosBot {fs.disparosBot} recargas {fs.recargas} sinMun {fs.sinMunicion} | arma {fs.armaIni} municion ini {fs.ammoIni} min {fs.ammoMin} fin {fs.ammoFin} | quietoMax {fs.quietoMax:0.0} s quietoTotal {fs.quietoTotal:0.0} s | {fs.nota}");
            resultado.AppendLine(linea);
            Log("==== " + linea);
            File.WriteAllText(rutaRes, resultado.ToString());
            fs = null;
        }

        // ----------------------------------------------------------------- principal
        static IEnumerator Principal(int desde, int hasta, bool encadenar)
        {
            var d = Dir;
            float tInicio = Time.time, rInicio = Time.realtimeSinceStartup;
            if (encadenar && desde == 1)
            {
                // Arranque normal: sin salto. Solo se salta la cinematica inicial (el jugador puede mantener [Espacio] 1 s).
                CinematicaDeRapel.Saltar();
                CinematicaDeOperacion.Saltar();
                PartidaGuardada.ModoPrueba = true;
                yield return Esperar(1.5f);
            }
            for (int n = desde; n <= hasta; n++)
            {
                Estado = "fase " + n;
                if (!encadenar || (n == desde && desde > 1))
                {
                    Log(OperacionPrueba.Arrancar(n, 1));
                    yield return Esperar(2.5f);
                }
                Poseer(Escuadrista(RoleType.Flanker));
                IEnumerator f = null;
                switch (n)
                {
                    case 1: f = Fase1(); break;
                    case 2: f = Fase2(); break;
                    case 3: f = Fase3(); break;
                    case 4: f = Fase4(); break;
                    case 5: f = Fase5(); break;
                    case 6: f = Fase6(); break;
                }
                yield return f;
                if (d.Fase == FaseOperacion.Derrota) { Log("DERROTA: " + d.Motivo); resultado.AppendLine("DERROTA en la fase " + n + ": " + d.Motivo); break; }
                if (fs != null) { CerrarFase(false, "abortada"); break; }
                if (resultado.ToString().Contains("NO COMPLETADA")) break;
            }
            resultado.AppendLine(Inv($"TOTAL de la corrida {(encadenar ? "encadenada" : "con saltos")}: juego {Time.time - tInicio:0.0} s, real {Time.realtimeSinceStartup - rInicio:0.0} s"));
        }

        // Espera con combate hasta que 'listo' sea true o pase el tope; 'meta' (opcional) devuelve a donde ir cuando no hay blanco.
        static IEnumerator Jugar(Func<bool> listo, Func<Vector3?> meta, float tope, float cerca = 2f, Func<Soldier, bool> filtro = null, Action cadaCuadro = null, float alcance = 0f)
        {
            float t0 = Time.time;
            while (!listo())
            {
                if (Dir.Fase == FaseOperacion.Derrota || Dir.Fase == FaseOperacion.Victoria) yield break;
                if (Time.time - t0 > tope) { Log("TOPE de tiempo (" + tope + " s)"); yield break; }
                yield return SiMori();
                cadaCuadro?.Invoke();
                bool lucha = Combatir(filtro, alcance);
                if (!lucha && Revivir()) { yield return null; continue; }
                if (!lucha && meta != null)
                {
                    var m = meta();
                    if (m.HasValue) { if (IrA(m.Value) < cerca) Drv.CancelDestination(); }
                }
                yield return null;
            }
        }

        // ----------------------------------------------------------------- FASE 1: infiltrar
        static IEnumerator Fase1()
        {
            var d = Dir;
            IniciarFase(1, "Infiltrar");
            Vector3? Meta()
            {
                var yo = Yo; if (yo == null) return null;
                Soldier mejor = null; float md = 1e9f;
                foreach (var e in d.enemigosCuartel)
                {
                    if (!Vivo(e)) continue;
                    float dd = Plano(e.transform.position, yo.transform.position);
                    if (dd < md) { md = dd; mejor = e; }
                }
                if (mejor == null) return null;
                // acercarse hasta ~22 m del enemigo mas cercano
                var dir = (yo.transform.position - mejor.transform.position); dir.y = 0f;
                if (dir.magnitude < 20f) return Linea(yo, mejor.transform.position + Vector3.up * 0.45f, mejor) ? (Vector3?)null : mejor.transform.position;   // cerca y sin linea: acercarse
                return mejor.transform.position + dir.normalized * 18f;
            }
            yield return Jugar(() => d.Fase != FaseOperacion.Infiltrar, Meta, 900f);
            CerrarFase(d.Fase != FaseOperacion.Infiltrar && d.Fase != FaseOperacion.Derrota, d.Fase == FaseOperacion.Infiltrar ? "tope: quedaban " + CuartelVivos() : "");
        }
        static int CuartelVivos() { int n = 0; foreach (var e in Dir.enemigosCuartel) if (Vivo(e)) n++; return n; }

        // ----------------------------------------------------------------- FASE 2: puestos
        static IEnumerator Fase2()
        {
            var d = Dir;
            IniciarFase(2, "Puestos");
            float proxOrden = 0f;
            Vector3? Meta()
            {
                var yo = Yo; if (yo == null) return null;
                // el puesto sin volar mas cercano
                PuestoDeVoladura p = null; float md = 1e9f;
                foreach (var x in d.puestos) { if (x.carga == null || x.carga.EstaVolada) continue; float dd = Plano(x.carga.transform.position, yo.transform.position); if (dd < md) { md = dd; p = x; } }
                if (p == null) return null;
                var c = p.carga.transform.position;
                // armada: alejarse >= 20 m
                if (p.carga.EstaPlantada)
                {
                    if (md < 20f) { var dir = yo.transform.position - c; dir.y = 0f; return c + dir.normalized * 24f; }
                    return null;
                }
                // lugar de espera 22 m al sur de la carga (la escuadra viene del sur)
                return c + new Vector3(0f, 0f, -22f);
            }
            void Cuadro()
            {
                if (Time.time < proxOrden) return;
                proxOrden = Time.time + 2f;
                var yo = Yo; if (yo == null) return;
                foreach (var x in d.puestos)
                {
                    if (x.carga == null || x.carga.EstaPlantada) continue;
                    if (Plano(x.carga.transform.position, yo.transform.position) > 40f) continue;
                    if (x.carga.Asignado != null && Vivo(x.carga.Asignado)) continue;
                    var vega = Escuadrista(RoleType.Assault);
                    if (vega == null) { Log("No queda ASALTO para plantar"); continue; }
                    if (Yo == vega) { Poseer(Escuadrista(RoleType.Flanker) ?? Escuadrista(RoleType.Medic)); }
                    bool ok = x.carga.EnviarA(vega, out var motivo);
                    Log($"EnviarA(Vega) {x.carga.Titulo}: {ok} {motivo}");
                }
            }
            yield return Jugar(() => d.Fase != FaseOperacion.Puestos, Meta, 900f, 3f, null, Cuadro);
            CerrarFase(d.Fase != FaseOperacion.Puestos && d.Fase != FaseOperacion.Derrota, d.Fase == FaseOperacion.Puestos ? "tope" : "");
        }

        // ----------------------------------------------------------------- FASE 3: centro de datos
        static IEnumerator Fase3()
        {
            var d = Dir;
            IniciarFase(3, "Centro de datos");
            float proxOrden = 0f;
            Vector3? Meta()
            {
                var yo = Yo; if (yo == null) return null;
                var c = d.computadora.transform.position;
                if (Plano(yo.transform.position, c) > 9f) return c + new Vector3(0f, 0f, -6f);
                return null;
            }
            void Cuadro()
            {
                if (d.computadora.Operador != null && Vivo(d.computadora.Operador)) return;
                if (Time.time < proxOrden) return;
                proxOrden = Time.time + 2f;
                var yo = Yo; if (yo == null || Plano(yo.transform.position, d.computadora.transform.position) > 30f) return;
                // el operador: la asaltante (Vega) si esta; si no, cualquiera que no sea yo
                Soldier op = Escuadrista(RoleType.Assault);
                if (op == null || op == yo) op = Escuadrista(RoleType.Medic);
                if (op == null || op == yo) return;
                bool ok = d.computadora.EnviarA(op, out var motivo);
                Log($"computadora.EnviarA({op.DisplayName}): {ok} {motivo}");
            }
            yield return Jugar(() => d.Fase != FaseOperacion.CentroDeDatos, Meta, 600f, 3f, null, Cuadro);
            CerrarFase(d.Fase != FaseOperacion.CentroDeDatos && d.Fase != FaseOperacion.Derrota, d.Fase == FaseOperacion.CentroDeDatos ? "tope" : "");
        }

        // ----------------------------------------------------------------- FASE 4: huir en el tanque
        static IEnumerator Fase4()
        {
            var d = Dir;
            IniciarFase(4, "Huir en el tanque");
            var drv = Drv;
            // 4a. Acercamiento: caminar hasta pasar z > -30
            yield return Jugar(() => d.Fase != FaseOperacion.Huir || d.Subfase >= (int)SubfaseHuida.Jefe || d.TomaDelJefeActiva,
                () => new Vector3(d.tanque.transform.position.x - 12f, 0f, -22f), 300f);
            Log("4a listo: subfase=" + d.Subfase + " toma=" + d.TomaDelJefeActiva);
            float t0 = Time.time;
            while (d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Acercamiento && Time.time - t0 < 30f) yield return null;
            // 4b. Jefe: poseer a Vega (cohetes) y bajarlo
            if (d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Jefe)
            {
                float tJefe = Time.time;
                var vega = Escuadrista(RoleType.Assault);
                if (vega != null) Poseer(vega);
                yield return null;
                var yo = Yo;
                if (yo != null && yo.Weapon != null && yo.Weapon.Loadout.Contains(WeaponKind.Rocket)) drv.EquipWeaponHotkey(WeaponKind.Rocket);
                Log("Jefe: arma=" + (Yo != null && Yo.Weapon != null ? Yo.Weapon.CurrentWeaponKind + " " + Yo.Weapon.CurrentAmmo + "+" + Yo.Weapon.ReservaActual : "?"));
                int golpesEsquivados = 0;
                float proxArma = 0f;
                while (d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Jefe && Time.time - tJefe < 420f)
                {
                    yield return SiMori();
                    yo = Yo; if (yo == null) { yield return null; continue; }
                    if (Dir.Fase == FaseOperacion.Derrota) break;
                    // esquivar el circulo rojo
                    bool esquivando = false;
                    var jefe = d.Jefe;
                    var anillo = jefe != null ? jefe.AnilloActual : null;
                    if (anillo != null && anillo.Activo && Plano(anillo.Centro, yo.transform.position) < anillo.Radio + 3f)
                    {
                        var dir = yo.transform.position - anillo.Centro; dir.y = 0f; if (dir.sqrMagnitude < 0.1f) dir = Vector3.back;
                        var destino = anillo.Centro + dir.normalized * (anillo.Radio + 6f);
                        drv.SetDestination(destino); PulsarCorrer(); esquivando = true; golpesEsquivados++;
                    }
                    if (!esquivando)
                    {
                        // sin cohetes: ir a la caja
                        var w = yo.Weapon;
                        bool tieneCohete = w != null && w.Loadout.Contains(WeaponKind.Rocket) && (w.CurrentWeaponKind != WeaponKind.Rocket ? w.ReservaDe(WeaponKind.Rocket) > 0 : (w.CurrentAmmo + Mathf.Max(0, w.ReservaActual)) > 0);
                        if (!tieneCohete)
                        {
                            CajaDeSuministros caja = null; float md = 1e9f;
                            foreach (var c in CajaDeSuministros.Todas) { if (c == null || !c.Disponible) continue; float dd = Plano(c.transform.position, yo.transform.position); if (dd < md) { md = dd; caja = c; } }
                            if (caja != null) { Log("Sin cohetes: voy a la caja (" + (int)md + " m)"); IrA(caja.transform.position); }
                            else Combatir(null, 0f);
                        }
                        else
                        {
                            if (w.CurrentWeaponKind != WeaponKind.Rocket && Time.time >= proxArma) { proxArma = Time.time + 1f; drv.EquipWeaponHotkey(WeaponKind.Rocket); }
                            var tp = d.tanque.transform.position + Vector3.up * 1.2f;
                            float dist = Plano(tp, yo.transform.position);
                            if (w.CurrentWeaponKind == WeaponKind.Rocket && dist < 38f && dist > 12f && Linea(yo, tp, null))
                            {
                                drv.CancelDestination();
                                MirarA(yo, tp);
                                if (w.CurrentAmmo <= 0 && !w.IsReloading) { w.Reload(); fs.recargas++; }
                                else if (!w.IsReloading && PlayerBrain.Activo.Fire(tp)) fs.disparosBot++;
                            }
                            else if (dist >= 38f || !Linea(yo, tp, null)) IrA(tp + (yo.transform.position - tp).normalized * 25f);
                            else if (dist <= 12f) { var dir = yo.transform.position - tp; dir.y = 0f; drv.SetDestination(tp + dir.normalized * 26f); }
                        }
                    }
                    yield return null;
                }
                Log($"Jefe fin: subfase={d.Subfase} tanque%={(d.Jefe != null ? d.Jefe.Fraccion01 : -1):0.00} esquivadas={golpesEsquivados} t={Time.time - tJefe:0.0}");
            }
            // 4c. Reparacion: defender con Vega a pie junto al tanque
            if (d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Reparacion)
            {
                float tRep = Time.time;
                yield return Jugar(() => d.Fase != FaseOperacion.Huir || d.Subfase != (int)SubfaseHuida.Reparacion,
                    () => { var tp = d.tanque.transform.position; return Plano(Yo.transform.position, tp) > 14f ? tp + new Vector3(0f, 0f, -8f) : (Vector3?)null; }, 180f, 3f);
                Log($"Reparacion fin: t={Time.time - tRep:0.0}");
            }
            // 4d. Abordaje: caminar al tanque y [E]
            if (d.Fase == FaseOperacion.Huir && d.Subfase == (int)SubfaseHuida.Abordaje)
            {
                float tAb = Time.time;
                while (d.Fase == FaseOperacion.Huir && !d.EnTanque && Time.time - tAb < 90f)
                {
                    yield return SiMori();
                    var yo = Yo; if (yo == null) { yield return null; continue; }
                    if (!Combatir(null, 0f))
                    {
                        float dist = IrA(d.tanque.transform.position);
                        if (dist <= 6f) { drv.CancelDestination(); OperacionTerminal.PruebaToqueE = true; }
                    }
                    yield return null;
                }
                OperacionTerminal.PruebaToqueE = false;
                Log($"Abordaje fin: enTanque={d.EnTanque} t={Time.time - tAb:0.0}");
            }
            // 4e. Carrera: artillero con el canon
            if (d.Fase == FaseOperacion.Huir && d.EnTanque)
            {
                float tCar = Time.time; int disparosCanon = 0;
                float vidaMinTanque = 1f;
                while (d.Fase == FaseOperacion.Huir && (d.EnTanque || d.EnLlegada) && Time.time - tCar < 200f)
                {
                    var canon = d.tanque != null ? d.tanque.TorretaCanon : null;
                    if (d.tanque != null && d.tanque.Health != null && d.EnTanque && !d.EnLlegada) vidaMinTanque = Mathf.Min(vidaMinTanque, d.tanque.Health.Current / (float)d.tanque.Health.MaxHealth);
                    if (canon != null && d.EnTanque && drv.CurrentSeat == VehicleSeatRole.Gunner)
                    {
                        Vehicle mejor = null; float md = 1e9f;
                        foreach (var v in WorldSystemsRegistry.Vehicles)
                        {
                            if (v == null || !v.isActiveAndEnabled || v.IsDestroyed || v.Bando != TeamId.Enemy || v == d.tanque) continue;
                            float dd = Plano(v.transform.position, d.tanque.transform.position);
                            if (dd < md && dd < 75f) { md = dd; mejor = v; }
                        }
                        if (mejor != null)
                        {
                            var p = mejor.transform.position + Vector3.up * 0.8f;
                            canon.AimAt(p, Time.deltaTime);
                            if (canon.IsAimedAt(p, 3f) && canon.TryFire()) disparosCanon++;
                        }
                    }
                    yield return null;
                }
                Log($"Carrera fin: autosDestruidos={d.AutosDestruidos}/{d.AutosCreados} disparosCanon={disparosCanon} vidaMinTanque={vidaMinTanque:0.00} t={Time.time - tCar:0.0} fase={d.Fase}");
                fs.nota += $"carrera: {d.AutosDestruidos}/{d.AutosCreados} autos, {disparosCanon} obuses, tanque min {vidaMinTanque * 100f:0}%; ";
            }
            // esperar el cierre (llegada y destruccion del tanque -> Resistir)
            float tw = Time.time;
            while (d.Fase == FaseOperacion.Huir && Time.time - tw < 40f) { yield return SiMori(); yield return null; }
            CerrarFase(d.Fase == FaseOperacion.Resistir, d.Fase == FaseOperacion.Huir ? "tope (subfase " + d.Subfase + ")" : fs.nota);
        }

        // ----------------------------------------------------------------- FASE 5: resistir (radio + sectores)
        static readonly Dictionary<int, int> destinoSector = new Dictionary<int, int>();
        // Jugador de campo: terminado el tutorial de la vista tactica, el jugador posee a la asaltante y pelea en el sector mas caliente
        // (la radio sigue operada por la IA: es un rol). false = el jugador solo comanda desde la radio.
        public static bool modoCampo = true;
        static bool campoIniciado;
        static float proxEquipar;
        static Vehicle vehActual; static float vehDesde; static readonly HashSet<int> vehiculosIgnorados = new HashSet<int>();
        static bool PasosListos(OperacionDirector d) { var p = d.PasoHecho; for (int i = 0; i < 5; i++) if (!p[i]) return false; return true; }
        static void IniciarCampo(OperacionDirector d)
        {
            var drv = Drv; if (drv == null) return;
            var vega = Escuadrista(RoleType.Assault);
            if (vega == null || MandoTactico.EsOperadorDeRadio(vega)) return;
            if (drv.Rig != null && drv.Rig.Mode != ControlMode.Fps) drv.Rig.SetMode(ControlMode.Fps);
            Poseer(vega);
            Log("MODO CAMPO: el jugador posee a " + vega.DisplayName);
        }
        static void Campo(OperacionDirector d)
        {
            var yo = Yo; var drv = Drv;
            if (!campoIniciado || yo == null || !yo.Health.IsAlive || drv == null || yo.Weapon == null) return;
            if (drv.Rig != null && drv.Rig.Mode != ControlMode.Fps) drv.Rig.SetMode(ControlMode.Fps);
            var w = yo.Weapon;
            // camioneta enemiga: cohete
            Vehicle veh = null; float mv = 1e9f;
            foreach (var v in WorldSystemsRegistry.Vehicles)
            {
                if (v == null || !v.isActiveAndEnabled || v.IsDestroyed || v.Bando != TeamId.Enemy) continue;
                float dd = Plano(v.transform.position, yo.transform.position);
                if (dd < mv && dd < 90f) { mv = dd; veh = v; }
            }
            bool cohete = w.Loadout.Contains(WeaponKind.Rocket) && (w.CurrentWeaponKind == WeaponKind.Rocket ? w.CurrentAmmo + Mathf.Max(0, w.ReservaActual) > 0 : w.ReservaDe(WeaponKind.Rocket) > 0);
            if (veh != null && !vehiculosIgnorados.Contains(veh.GetHashCode()))
            {
                if (vehActual != veh) { vehActual = veh; vehDesde = Time.time; }
                if (Time.time - vehDesde > 30f) { vehiculosIgnorados.Add(veh.GetHashCode()); Log("camioneta ignorada tras 30 s"); }
                else if (cohete)
                {
                    if (w.CurrentWeaponKind != WeaponKind.Rocket && !w.IsReloading && Time.time >= proxEquipar) { proxEquipar = Time.time + 1.5f; drv.EquipWeaponHotkey(WeaponKind.Rocket); }
                    var tp = veh.transform.position + Vector3.up * 1f;
                    float dist = Plano(tp, yo.transform.position);
                    bool linea = Linea(yo, tp, null);
                    if (dist < 34f && dist > 9f && linea)
                    {
                        drv.CancelDestination(); MirarA(yo, tp);
                        if (w.CurrentAmmo <= 0 && !w.IsReloading) w.Reload();
                        else if (!w.IsReloading && w.CurrentWeaponKind == WeaponKind.Rocket && PlayerBrain.Activo.Fire(tp) && fs != null) fs.disparosBot++;
                    }
                    else IrA(tp + (yo.transform.position - tp).normalized * (linea ? 24f : 12f));
                    return;
                }
            }
            if (w.CurrentWeaponKind == WeaponKind.Rocket && w.Loadout.Count > 0 && !w.IsReloading && Time.time >= proxEquipar) { proxEquipar = Time.time + 1.5f; drv.EquipWeaponHotkey(w.Loadout[0]); }
            if (Combatir(null, 0f)) return;
            if (Revivir()) return;
            // sin blanco a la vista: al sector mas caliente (caido > amenazado), si no a la plaza
            Vector3 meta = d.plaza.position; float mejor = 1e9f;
            foreach (var sc in d.sectoresDeDefensa)
            {
                if (sc == null || !(sc.Caido || sc.Amenazado)) continue;
                float dd = Plano(sc.transform.position, yo.transform.position) - (sc.Caido ? 1000f : 0f);
                if (dd < mejor) { mejor = dd; meta = sc.transform.position; }
            }
            if (Plano(meta, yo.transform.position) > 8f) IrA(meta); else drv.CancelDestination();
        }
        static IEnumerator Fase5()
        {
            var d = Dir;
            IniciarFase(5, "Resistir en la ciudad");
            var drv = Drv;
            destinoSector.Clear(); vehiculosOrdenados.Clear(); vehiculosIgnorados.Clear(); vehActual = null; campoIniciado = false;
            // 5a. A pie hasta la radio
            if (d.Fase == FaseOperacion.Resistir && d.Subfase == 0)
            {
                float t0 = Time.time;
                while (d.Fase == FaseOperacion.Resistir && d.Subfase == 0 && Time.time - t0 < 240f)
                {
                    yield return SiMori();
                    var yo = Yo; if (yo == null) { yield return null; continue; }
                    if (!Combatir(null, 0f))
                    {
                        float dist = IrA(d.radioDeCampana.position);
                        if (dist <= MandoTactico.AlcanceDeLaRadio - 0.4f) { drv.CancelDestination(); OperacionTerminal.PruebaToqueE = true; }
                    }
                    yield return null;
                }
                OperacionTerminal.PruebaToqueE = false;
                Log($"5a radio: t={Time.time - t0:0.0} subfase={d.Subfase}");
            }
            // 5b. Dirigir desde la radio
            if (d.Fase == FaseOperacion.Resistir && d.Subfase >= 1)
            {
                float t0 = Time.time; float proxAcc = 0f, proxLogMando = 0f; int paso = -1; float ultRel = 0f; float tRelojQuieto = 0f; float maxPausa = 0f;
                while (d.Fase == FaseOperacion.Resistir && Time.time - t0 < 420f)
                {
                    if (Dir.Fase == FaseOperacion.Derrota) break;
                    if (!MandoTactico.EnRadio && Vivo(Yo) && d.PasoActualDelMando >= -1)
                    {
                        // el operador cayo o solto la radio: volver a tomarla
                        float dist = IrA(d.radioDeCampana.position);
                        if (dist <= MandoTactico.AlcanceDeLaRadio - 0.4f) OperacionTerminal.PruebaToqueE = true;
                    }
                    else OperacionTerminal.PruebaToqueE = false;
                    if (Time.time >= proxAcc) { proxAcc = Time.time + 1.2f; Dirigir(d); }
                    if (modoCampo) Campo(d); else if (MandoTactico.EnRadio && Vivo(Yo)) Combatir(null, 0f);
                    if (modoCampo && !campoIniciado && PasosListos(d)) { campoIniciado = true; IniciarCampo(d); }
                    if (Time.time >= proxLogMando) { proxLogMando = Time.time + 6f; LogMando(d); }
                    if (d.PasoActualDelMando != paso) { paso = d.PasoActualDelMando; Log($"MANDO paso={paso} reloj={d.RelojEfectivo:0.0} corre={d.RelojDelMandoCorre} motivo='{d.MotivoDeLaPausa}' | {Hud()}"); }
                    if (d.RelojEfectivo > ultRel + 0.001f) { ultRel = d.RelojEfectivo; tRelojQuieto = 0f; } else { tRelojQuieto += Time.deltaTime; maxPausa = Mathf.Max(maxPausa, tRelojQuieto); }
                    yield return SiMori();
                    yield return null;
                }
                OperacionTerminal.PruebaToqueE = false;
                fs.nota += $"mando: oleadas lanzadas {d.OleadasDelMandoLanzadas}/5, sectores caidos {d.SectoresCaidosTotales}, pausa maxima del reloj {maxPausa:0.0} s; ";
                Log($"5b fin: t={Time.time - t0:0.0} reloj={d.RelojEfectivo:0.0}");
            }
            CerrarFase(d.Fase == FaseOperacion.Extraer || d.Fase == FaseOperacion.Victoria, d.Fase == FaseOperacion.Resistir ? "tope" : fs.nota);
        }

        // Las acciones de la vista tactica de un jugador: elegir, ordenar al sector, repartir de a dos.
        static void Dirigir(OperacionDirector d)
        {
            var drv = Drv; if (drv == null || drv.Selection == null || d.sectoresDeDefensa == null) return;
            int paso = d.PasoActualDelMando;
            var sec = d.sectoresDeDefensa;
            var mil = new List<Soldier>(); foreach (var m in d.Milicianos) if (Vivo(m)) mil.Add(m);
            switch (paso)
            {
                case 0: if (Vivo(d.UnidadA)) drv.Selection.SelectSingle(d.UnidadA); break;
                case 1: if (Vivo(d.UnidadA)) Mandar(d.UnidadA, 0, d); break;
                case 2: if (mil.Count > 0) { drv.Selection.SelectSingle(mil[0]); for (int i = 1; i < mil.Count; i++) drv.Selection.AddToSelection(mil[i]); } break;
                case 3: foreach (var m in mil) Mandar(m, 1, d); break;
                case 4:
                    if (Vivo(d.UnidadC)) { drv.Selection.SelectSingle(d.UnidadC); Mandar(d.UnidadC, 2, d); }
                    break;
                case 5: break;   // el ataque a la camioneta lo da el mantenimiento (con los asaltos)
            }
            if (paso >= 0 && paso <= 4) return;   // primero los pasos guiados
            // mantenimiento: todos los libres a los sectores amenazados/caidos (los demas pueden quedar vacios), asaltos contra las camionetas
            var libres = new List<Soldier>();
            foreach (var a in Aliados(true)) if (!MandoTactico.EsOperadorDeRadio(a) && !(a.Brain != null && a.Brain.IsPossessedByPlayer)) libres.Add(a);
            if (libres.Count == 0) return;
            var atacando = new HashSet<int>();   // las camionetas las baja el jugador de campo con cohetes (los milicianos solo tienen subfusil)
            // reparto por necesidad: caidos primero, luego amenazados (2 aliados cada uno); el resto, lo que sobre
            var defensores = libres.FindAll(x => !atacando.Contains(x.Id));
            int n = defensores.Count; if (n == 0) return;
            int[] actual = new int[defensores.Count];
            int[] eff = new int[sec.Length];
            for (int k = 0; k < defensores.Count; k++)
            {
                var a = defensores[k]; actual[k] = -1;
                for (int i = 0; i < sec.Length; i++) if (sec[i] != null && sec[i].Contiene(a.transform.position)) { actual[k] = i; break; }
                if (actual[k] < 0 && a.Brain != null && a.Brain.TieneOrden && destinoSector.TryGetValue(a.Id, out int dst)) actual[k] = dst;
                if (actual[k] >= 0) eff[actual[k]]++;
            }
            var prio = new List<int>();
            for (int i = 0; i < sec.Length; i++) if (sec[i] != null && sec[i].Caido) prio.Add(i);
            for (int i = 0; i < sec.Length; i++) if (sec[i] != null && !sec[i].Caido && sec[i].Amenazado) prio.Add(i);
            var want = new int[sec.Length];
            int resto = n;
            foreach (int i in prio) { want[i] = Mathf.Min(2, resto); resto -= want[i]; }
            int libresDeAmenaza = 0; for (int i = 0; i < sec.Length; i++) if (sec[i] != null && !prio.Contains(i)) libresDeAmenaza++;
            for (int i = 0; i < sec.Length && resto > 0 && libresDeAmenaza > 0; i++)
                if (sec[i] != null && !prio.Contains(i)) { int w = Mathf.Min(2, Mathf.CeilToInt(resto / (float)libresDeAmenaza)); want[i] = w; resto -= w; libresDeAmenaza--; }
            var ordenados = new List<int>(prio);
            for (int i = 0; i < sec.Length; i++) if (sec[i] != null && !ordenados.Contains(i)) ordenados.Add(i);
            foreach (int i in ordenados)
            {
                while (eff[i] < want[i])
                {
                    int mejor = -1; float md = 1e9f;
                    for (int k = 0; k < defensores.Count; k++)
                    {
                        int cur = actual[k];
                        if (cur == i) continue;
                        if (cur >= 0 && eff[cur] <= want[cur]) continue;   // no se desviste otro sector que lo necesita
                        float dd = Plano(defensores[k].transform.position, sec[i].transform.position);
                        if (dd < md) { md = dd; mejor = k; }
                    }
                    if (mejor < 0) break;
                    int de = actual[mejor];
                    Mandar(defensores[mejor], i, d);
                    if (de >= 0) eff[de]--;
                    actual[mejor] = i; eff[i]++;
                }
            }
        }
        static readonly HashSet<int> vehiculosOrdenados = new HashSet<int>();

        static void LogMando(OperacionDirector d)
        {
            var sb = new StringBuilder($"MANDO t={d.RelojEfectivo:0.0} corre={d.RelojDelMandoCorre} '{d.MotivoDeLaPausa}' radio={MandoTactico.EnRadio} oleadas={d.OleadasDelMandoLanzadas}/{d.OleadasDelMandoAnunciadas} sectores:");
            foreach (var sc in d.sectoresDeDefensa) if (sc != null) sb.Append($" {sc.letra}[{(sc.Amenazado ? "AMEN" : "-")}{(sc.Caido ? " CAIDO" : "")} dentro={sc.AliadosDentro}]");
            int en = 0; foreach (var e in ActorRegistry.All) if (e != null && e.Team == TeamId.Enemy && Vivo(e)) en++;
            sb.Append($" enemigos={en} aliados={Aliados(true).Count} hpOperador={(Yo != null ? Yo.Health.Current : -1)}");
            Log(sb.ToString());
        }
        static void Mandar(Soldier s, int sector, OperacionDirector d)
        {
            var sc = d.sectoresDeDefensa[sector];
            if (destinoSector.TryGetValue(s.Id, out int ya) && ya == sector && s.Brain != null && (s.Brain.TieneOrden || sc.Contiene(s.transform.position))) return;
            var p = sc.transform.position + new Vector3(((s.Id * 37) % 5 - 2) * 1.5f, 0f, ((s.Id * 53) % 5 - 2) * 1.5f);
            if (NavMesh.SamplePosition(p, out var h, 4f, NavMesh.AllAreas)) p = h.position;
            destinoSector[s.Id] = sector;
            OrderService.IssueMoveOrder(s, p);
        }

        // ----------------------------------------------------------------- FASE 6: extraer
        static IEnumerator Fase6()
        {
            var d = Dir;
            IniciarFase(6, "Extraer");
            var drv = Drv;
            // volver a FPS si quedo en la vista tactica
            if (drv.Rig != null && drv.Rig.Mode != ControlMode.Fps) { drv.Rig.SetMode(ControlMode.Fps); yield return null; }
            float t0 = Time.time; bool subi = false;
            while (d.Fase == FaseOperacion.Extraer && Time.time - t0 < 300f)
            {
                yield return SiMori();
                var yo = Yo; if (yo == null) { yield return null; continue; }
                if (!d.SubiendoAlHeli)
                {
                    if (!Combatir(null, 0f))
                    {
                        var meta = d.heliAterrizaje.position;
                        float dist = IrA(meta);
                        if (d.HeliAterrizo && !d.CoberturaActiva && Plano(yo.transform.position, d.heli.position) <= 10f) { drv.CancelDestination(); OperacionTerminal.PruebaToqueE = true; subi = true; }
                    }
                }
                else OperacionTerminal.PruebaToqueE = false;
                yield return null;
            }
            OperacionTerminal.PruebaToqueE = false;
            // la secuencia final (despegue ~14 s) y victoria
            float tv = Time.time; float rv = Time.realtimeSinceStartup;
            while (d.Fase != FaseOperacion.Victoria && d.Fase != FaseOperacion.Derrota && Time.realtimeSinceStartup - rv < 60f) yield return null;
            fs.nota += $"subidos al heli {OperacionDirector.SubidosAlHeli}, caidos rescatados {d.HeridosRescatados}; [E] a los {(subi ? "ok" : "no")}";
            CerrarFase(d.Fase == FaseOperacion.Victoria, d.Fase == FaseOperacion.Victoria ? fs.nota : "tope: " + fs.nota);
        }
    }
}
