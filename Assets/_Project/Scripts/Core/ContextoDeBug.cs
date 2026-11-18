using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using SP.Actors;
using SP.Vehicles;

namespace SP.Core
{
    // Columnas nuevas del reporte de bugs (plan de Opus 5.5: Docs/PLAN_columnas_bugs_opus.md, 2026-11-08). Un solo armador para las tres
    // salidas: el Sheet (SubidaDeBug.Armar), el CSV local (Registros/bugs_columnas.csv) y bugs.txt.
    //  - Buffers baratos (FPS de los ultimos 5 s, peor frame, recorrido de los ultimos 10 s, errores de consola) cargados por un vigilante.
    //  - Contexto de cada bug: build, estado de control, objeto mirado, entorno (NavMesh y colliders cercanos), ajustes (modo dios, timeScale).
    //  - Clasificacion automatica de la nota (Clase / Area / Prioridad) para seguimiento.
    public static class ContextoDeBug
    {
        // ------------------------------------------------------------------ buffers
        const int MuestrasDeFrame = 300, MuestrasDePos = 20;
        static readonly float[] dts = new float[MuestrasDeFrame]; static int nDts, iDts;
        static readonly Vector3[] posiciones = new Vector3[MuestrasDePos]; static int nPos, iPos; static float proximaPos;
        static int errores; static string ultimoError = ""; static readonly object cerrojo = new object();
        public static string OrigenDePartida = "nueva";
        static int bugsEnLaSesion;

        public static float Fps5s { get { int n = Mathf.Min(nDts, MuestrasDeFrame); if (n == 0) return 0f; float s = 0f; for (int i = 0; i < n; i++) s += dts[i]; return s > 0f ? n / s : 0f; } }
        public static float FrameMsMax5s { get { int n = Mathf.Min(nDts, MuestrasDeFrame); float m = 0f; for (int i = 0; i < n; i++) m = Mathf.Max(m, dts[i]); return m * 1000f; } }
        public static int Errores { get { lock (cerrojo) return errores; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { nDts = iDts = nPos = iPos = 0; errores = 0; ultimoError = ""; bugsEnLaSesion = 0; proximaPos = 0f; OrigenDePartida = "nueva"; sello = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            Application.logMessageReceivedThreaded -= AlLog; Application.logMessageReceivedThreaded += AlLog;
            if (UnityEngine.Object.FindFirstObjectByType<Vigilante>() != null) return;
            var go = new GameObject("ContextoDeBug") { hideFlags = HideFlags.DontSave }; UnityEngine.Object.DontDestroyOnLoad(go); go.AddComponent<Vigilante>();
        }

        static void AlLog(string texto, string pila, LogType tipo)
        {
            if (tipo != LogType.Error && tipo != LogType.Exception && tipo != LogType.Assert) return;
            lock (cerrojo)
            {
                errores++;
                string linea = (texto ?? "").Split('\n')[0]; if (linea.Length > 160) linea = linea.Substring(0, 160);
                ultimoError = linea;
            }
        }

        sealed class Vigilante : MonoBehaviour
        {
            void Update()
            {
                float dt = Time.unscaledDeltaTime;
                if (Time.timeScale > 0f && dt > 0f && dt < 5f) { dts[iDts] = dt; iDts = (iDts + 1) % MuestrasDeFrame; if (nDts < MuestrasDeFrame) nDts++; }
                if (Time.unscaledTime < proximaPos || Time.timeScale <= 0f) return;
                proximaPos = Time.unscaledTime + 0.5f;
                var p = PosicionDeReferencia(); if (p == null) return;
                posiciones[iPos] = p.Value; iPos = (iPos + 1) % MuestrasDePos; if (nPos < MuestrasDePos) nPos++;
            }
        }

        static Soldier Poseido()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            if (d != null && d.Brain != null && d.Brain.Current != null) return d.Brain.Current;
            foreach (var s in ActorRegistry.All) if (s != null && s.Brain != null && s.Brain.IsPossessedByPlayer) return s;
            return null;
        }

        static Vector3? PosicionDeReferencia()
        {
            var yo = Poseido(); if (yo == null) return null;
            var v = VehiculoDe(yo);
            return v != null ? v.transform.position : yo.transform.position;
        }

        static Vehicle VehiculoDe(Soldier yo)
        {
            if (yo == null) return null;
            foreach (var v in WorldSystemsRegistry.Vehicles) if (v != null && v.isActiveAndEnabled && !v.IsDestroyed && v.RoleOf(yo) != null) return v;
            return null;
        }

        public static float Desplazamiento10s()
        {
            int n = Mathf.Min(nPos, MuestrasDePos); if (n < 2) return 0f;
            float total = 0f; Vector3? ant = null;
            for (int k = 0; k < n; k++)
            {
                var p = posiciones[(iPos - n + k + MuestrasDePos * 2) % MuestrasDePos];
                if (ant.HasValue) { var d = new Vector3(p.x - ant.Value.x, 0f, p.z - ant.Value.z).magnitude; if (d < 6f) total += d; }   // saltos > 6 m = teletransporte, no cuentan
                ant = p;
            }
            return total;
        }

        // ------------------------------------------------------------------ sello del build
        static string sello;
        public static string Build
        {
            get
            {
                if (sello != null) return sello;
                string ver = Application.version;
                var ta = RecursosCache.Cargar<TextAsset>("build_sello");
                if (ta != null && !string.IsNullOrWhiteSpace(ta.text)) return sello = ta.text.Trim();
                try
                {
                    string git = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".git"));
                    string head = File.Exists(Path.Combine(git, "HEAD")) ? File.ReadAllText(Path.Combine(git, "HEAD")).Trim() : "";
                    string sha = head;
                    if (head.StartsWith("ref:")) { string r = Path.Combine(git, head.Substring(5).Trim().Replace('/', Path.DirectorySeparatorChar)); sha = File.Exists(r) ? File.ReadAllText(r).Trim() : ""; }
                    if (sha.Length >= 7) return sello = $"{ver}+{sha.Substring(0, 7)}·editor";
                }
                catch { }
                return sello = ver + "+sin-git";
            }
        }

        // ------------------------------------------------------------------ id y orden
        public static string BugId(int n) => $"{(Application.isEditor ? "ED" : "EX")}-{Environment.MachineName}-{n:000}";
        public static int SiguienteOrdenEnSesion() => ++bugsEnLaSesion;

        // ------------------------------------------------------------------ estado de control
        static string EstadoDeControl(EstadoDeSesion e)
        {
            var yo = Poseido();
            if (yo == null) return string.IsNullOrEmpty(e.jugador.soldado) ? "Sin_control" : "Sin_soldado";
            if (yo.Health != null && !yo.Health.IsAlive) return "Muerto";
            var v = VehiculoDe(yo);
            if (v != null) { var rol = v.RoleOf(yo); return $"Vehiculo:{rol}@{v.name} {(v.Health != null ? v.Health.Current + "/" + v.Health.MaxHealth : "")}".Trim(); }
            var rig = SP.CameraSystem.CameraRig.Instance;
            if (rig != null && rig.Mode == SP.CameraSystem.ControlMode.Rts) return "RTS";
            return yo.Motor != null && yo.Motor.IsCrouching ? "FPS_agachado" : "FPS_a_pie";
        }

        // ------------------------------------------------------------------ objeto mirado y entorno
        static string ObjetoMirado()
        {
            try
            {
                var cam = CamaraPrincipal.Actual; if (cam == null) return "";
                if (!Physics.Raycast(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var h, 500f, ~0, QueryTriggerInteraction.Ignore)) return "(nada a 500 m)";
                var partes = new List<string>(); for (var t = h.transform; t != null && partes.Count < 3; t = t.parent) partes.Insert(0, t.name);
                return $"{string.Join("/", partes)} ({LayerMask.LayerToName(h.collider.gameObject.layer)}) d={h.distance:0.0}m p=({h.point.x:0.0},{h.point.y:0.0},{h.point.z:0.0})";
            }
            catch (Exception ex) { return "(error mirada: " + ex.Message + ")"; }
        }

        static readonly Collider[] bufCol = new Collider[48];
        static string Entorno()
        {
            try
            {
                var yo = Poseido(); if (yo == null) return "";
                var v = VehiculoDe(yo);
                Vector3 centro = (v != null ? v.transform.position : yo.transform.position) + Vector3.up;
                string nav = UnityEngine.AI.NavMesh.SamplePosition(centro, out var nh, 5f, UnityEngine.AI.NavMesh.AllAreas) ? $"nav={Vector3.Distance(centro, nh.position):0.0}m" : "nav=NO";
                float radio = v != null ? 5f : 1.6f;
                int n = Physics.OverlapSphereNonAlloc(centro, radio, bufCol, ~0, QueryTriggerInteraction.Ignore);
                var cerca = new List<(float d, string s)>();
                for (int i = 0; i < n; i++)
                {
                    var c = bufCol[i]; if (c == null) continue;
                    var raiz = c.transform; if (raiz.IsChildOf(yo.transform) || (v != null && raiz.IsChildOf(v.transform))) continue;
                    if (c is TerrainCollider) continue;
                    float d = Vector3.Distance(c.ClosestPoint(centro), centro);
                    cerca.Add((d, $"{c.name}({LayerMask.LayerToName(c.gameObject.layer)}) {d:0.0}m"));
                }
                cerca.Sort((a, b) => a.d.CompareTo(b.d));
                var sb = new StringBuilder(nav);
                for (int i = 0; i < Mathf.Min(3, cerca.Count); i++) sb.Append(" · ").Append(cerca[i].s);
                if (yo.Motor != null) sb.Append($" · velDeseada={yo.Motor.MoveSpeed:0.0}");
                return sb.ToString();
            }
            catch (Exception ex) { return "(error entorno: " + ex.Message + ")"; }
        }

        static string Ajustes()
        {
            return $"dios={(ModoDios.Activo ? 1 : 0)} ts={Time.timeScale:0.00} carreraDios={ModoDios.FactorDeCarreraDelModoDios:0.#} danoPropio=x{ModoDios.MultiplicadorDeDanoPropio:0.#}";
        }

        // ------------------------------------------------------------------ clasificacion automatica de la nota
        static string Sin(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD); var sb = new StringBuilder();
            foreach (var ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        public static string Clasificar(string nota, float desplazamiento10s = -1f, int erroresLog = 0)
        {
            string t = Sin(nota);
            bool atasco = Regex.IsMatch(t, @"snap|trab|atasc|atrap|no avanza|no pasa|bugue|bugeo|atravies|collider");
            bool balance = Regex.IsMatch(t, @"\bvida\b|dano|velocidad|doble|mitad|veces|cadencia");
            bool pedido = Regex.IsMatch(t, @"\bquiero\b|\bfalta|\bquita|deberia|\bagrega|\bpon\b|que haya|mejor|sea ");
            bool pregunta = t.Contains("?") && !atasco;
            string clase = atasco ? "Defecto" : balance ? "Balance" : pregunta ? "Pregunta" : pedido ? "Pedido" : "Defecto";
            string area = atasco ? "Colision/Nav"
                : Regex.IsMatch(t, @"helic|vehicul|tanque|camioneta") ? "Vehiculos/Helis"
                : Regex.IsMatch(t, @"minimapa|rombo|\bui\b|barra|cartel|hud") ? "UI/Minimapa"
                : Regex.IsMatch(t, @"luz|luces|brillo|ventana|sombra|fuente|bandera|textura") ? "Luces/Visual"
                : Regex.IsMatch(t, @"sonido|particula|explosion|efecto|humo") ? "Audio/FX"
                : Regex.IsMatch(t, @"aliado|miliciano") ? "IA aliados"
                : Regex.IsMatch(t, @"enemigo|francotirador|flanco|pesado") ? "IA enemigos"
                : balance ? "Balance" : Regex.IsMatch(t, @"lag|fps|traba mucho|lento") ? "Rendimiento" : "Otro";
            string prio = Regex.IsMatch(t, @"no avanza|se trabo|no pasa|bugeo|bugue") || (atasco && desplazamiento10s >= 0f && desplazamiento10s < 0.5f) ? "P0"
                : clase == "Defecto" ? "P1" : clase == "Balance" || area == "Luces/Visual" ? "P2" : "P3";
            return $"{clase}·{area}·{prio}";
        }

        // ------------------------------------------------------------------ columnas
        // Las columnas nuevas, en el orden en que viajan. Se arman UNA vez por bug (estado exacto del instante).
        public static List<KeyValuePair<string, string>> Columnas(EstadoDeSesion e, int ordenEnSesion)
        {
            var l = new List<KeyValuePair<string, string>>();
            void A(string k, string v) => l.Add(new KeyValuePair<string, string>(k, v ?? ""));
            string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            string clasif = Clasificar(e.nota, Desplazamiento10s(), Errores);
            var c = clasif.Split('·');
            A("Bug_id", BugId(e.numero));
            A("Build", Build);
            A("Estado_control", EstadoDeControl(e));
            string sub = null; foreach (var k in e.mision) if (k.k == "Operacion.subfase") { sub = k.v; break; }
            A("Subfase", sub ?? "");
            A("Objeto_mirado", ObjetoMirado());
            A("Entorno", Entorno());
            A("Desplaz_10s_m", F(Desplazamiento10s()));
            A("Ajustes", Ajustes());
            A("FPS_5s", F(Fps5s));
            A("Frame_ms_max_5s", F(FrameMsMax5s));
            lock (cerrojo) A("Errores_log", errores == 0 ? "0" : errores + " | último: " + ultimoError);
            A("Clasif_auto", clasif);
            A("Sesion", $"{e.escena} · #{ordenEnSesion}");
            A("Estado", "Nuevo");
            A("Clase", c.Length > 0 ? c[0] : ""); A("Area", c.Length > 1 ? c[1] : ""); A("Prioridad", c.Length > 2 ? c[2] : "");
            A("Duplicado_de", ""); A("Commit_fix", ""); A("Notas_dev", "");
            return l;
        }

        // Fila del CSV local: las columnas de siempre (las que ya viajan al Sheet) + las nuevas, en un orden fijo.
        public static List<KeyValuePair<string, string>> FilaLocal(EstadoDeSesion e, string nota, string rutaJson, string rutaPng, List<KeyValuePair<string, string>> extra)
        {
            var l = new List<KeyValuePair<string, string>>();
            string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            void A(string k, string v) => l.Add(new KeyValuePair<string, string>(k, v ?? ""));
            string Extra(string k) { foreach (var kv in extra) if (kv.Key == k) return kv.Value; return ""; }
            string fase = "?"; foreach (var k in e.mision) if (k.k == "Operacion.fase" || (fase == "?" && k.k == "MisionDirector.fase")) { fase = k.v; break; }
            int vivosA = 0, activosE = 0, totalE = 0;
            foreach (var s in e.soldados)
            {
                if (s.equipo == "Enemy") { if (s.vivo) totalE++; if (s.vivo && s.activo) activosE++; continue; }
                if (s.rol != "Civilian" && s.vivo && s.activo) vivosA++;
            }
            A("Bug", e.numero.ToString(CultureInfo.InvariantCulture)); A("Bug_id", Extra("Bug_id")); A("Fecha", e.fecha); A("Comentarios", (nota ?? "").Trim());
            A("Estado", Extra("Estado")); A("Prioridad", Extra("Prioridad")); A("Clase", Extra("Clase")); A("Area", Extra("Area"));
            A("Duplicado_de", ""); A("Commit_fix", ""); A("Notas_dev", ""); A("Clasif_auto", Extra("Clasif_auto"));
            A("Build", Extra("Build")); A("Escena", e.escena); A("Fase", fase); A("Subfase", Extra("Subfase"));
            A("Tiempo_sesion_s", F(e.tiempoSesion)); A("Estado_control", Extra("Estado_control"));
            A("Jugador", e.jugador.soldado); A("Jugador_X", F(e.jugador.pos.x)); A("Jugador_Y", F(e.jugador.pos.y)); A("Jugador_Z", F(e.jugador.pos.z));
            A("Camara", $"({F(e.jugador.camPos.x)},{F(e.jugador.camPos.y)},{F(e.jugador.camPos.z)}) rx={F(e.jugador.camRot.x)} ry={F(e.jugador.camRot.y)} fov={F(e.jugador.fov)}");
            A("Objeto_mirado", Extra("Objeto_mirado")); A("Entorno", Extra("Entorno")); A("Desplaz_10s_m", Extra("Desplaz_10s_m")); A("Ajustes", Extra("Ajustes"));
            A("FPS_5s", Extra("FPS_5s")); A("Frame_ms_max_5s", Extra("Frame_ms_max_5s")); A("Errores_log", Extra("Errores_log"));
            A("Aliados_vivos", vivosA.ToString(CultureInfo.InvariantCulture)); A("Enemigos_vivos", activosE.ToString(CultureInfo.InvariantCulture));
            A("Enemigos_reserva", Mathf.Max(0, totalE - activosE).ToString(CultureInfo.InvariantCulture)); A("Sesion", Extra("Sesion"));
            A("Archivo_local", Path.GetFileName(rutaJson)); A("Captura_local", Path.GetFileName(rutaPng));
            return l;
        }

        // ------------------------------------------------------------------ CSV local (RFC 4180, UTF-8 con BOM)
        static string Csv(string v)
        {
            v = v ?? "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        // Agrega una fila a Registros/bugs_columnas.csv. Si el archivo ya existe con otra cabecera, se lo archiva y se empieza uno nuevo.
        public static void AgregarFilaCsv(string carpeta, List<KeyValuePair<string, string>> columnas)
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                string ruta = Path.Combine(carpeta, "bugs_columnas.csv");
                string cab = string.Join(",", columnas.ConvertAll(k => Csv(k.Key)));
                if (File.Exists(ruta))
                {
                    string primera = ""; using (var r = new StreamReader(ruta, Encoding.UTF8)) primera = r.ReadLine() ?? "";
                    if (primera.TrimStart('﻿') != cab) File.Move(ruta, Path.Combine(carpeta, $"bugs_columnas_anterior_{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
                }
                bool nuevo = !File.Exists(ruta);
                using (var w = new StreamWriter(ruta, true, new UTF8Encoding(true)))
                {
                    if (nuevo) w.Write(cab + "\r\n");
                    w.Write(string.Join(",", columnas.ConvertAll(k => Csv(k.Value))) + "\r\n");
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ContextoDeBug] No se pudo escribir bugs_columnas.csv: " + ex.Message); }
        }
    }
}
