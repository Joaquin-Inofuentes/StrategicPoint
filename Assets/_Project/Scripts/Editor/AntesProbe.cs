using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Actors;
using SP.Core;

namespace SP.EditorTools
{
    // Ronda 11: instrumento SOLO de medicion (no toca ninguna regla del juego). Sirve para dejar registrado el "antes"
    // de cada punto del pedido y, con la misma sonda, el "despues". Escribe en Logs/Antes/<corrida>/:
    //   log.jsonl  una linea por evento del juego (disparos, dano, muertes, curas, posesion, estados de IA) y por marca propia,
    //              con reloj real, reloj de juego y cuadro, para poder leerlo como datos.
    //   fotos/     capturas de pantalla numeradas.
    // Se maneja desde eval / el CLI de Unity: AntesProbe.Iniciar("nombre"), .Marca(...), .Foto(...), .Cerrar().
    public static class AntesProbe
    {
        public static string Carpeta { get; private set; }
        static StreamWriter log;
        static readonly List<IDisposable> subs = new List<IDisposable>();
        static int nFoto;
        static double t0;

        public static string Iniciar(string nombre)
        {
            Cerrar();
            Carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Antes", nombre));
            Directory.CreateDirectory(Path.Combine(Carpeta, "fotos"));
            log = new StreamWriter(Path.Combine(Carpeta, "log.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
            t0 = EditorApplication.timeSinceStartup;
            nFoto = 0;
            var bus = EventBus.Instance;
            subs.Add(bus.Subscribe<ShotFiredEvent>(e => Evento("disparo", "tirador", e.ShooterId, "pos", Pos(e.ShooterId))));
            subs.Add(bus.Subscribe<DamageTakenEvent>(e => Evento("dano", "victima", e.TargetId, "atacante", e.AttackerId, "cant", e.Amount, "resta", e.RemainingHealth)));
            subs.Add(bus.Subscribe<EntityDiedEvent>(e => Evento("muerte", "id", e.ActorId)));
            subs.Add(bus.Subscribe<HealedEvent>(e => Evento("cura", "id", e.TargetId, "cant", e.Amount, "resta", e.RemainingHealth)));
            subs.Add(bus.Subscribe<PossessionChangedEvent>(e => Evento("posesion", "de", e.FromId, "a", e.ToId)));
            subs.Add(bus.Subscribe<AiStateChangedEvent>(e => Evento("estado_ia", "id", e.ActorId, "a", e.NewState)));
            Marca("inicio", nombre);
            return Carpeta;
        }

        static string Pos(int id)
        {
            var s = ActorRegistry.FindById(id);
            if (s == null) return "";
            var p = s.transform.position;
            return p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1");
        }

        static void Evento(string tipo, params object[] pares)
        {
            if (log == null) return;
            var sb = new StringBuilder("{");
            sb.Append("\"t\":").Append((EditorApplication.timeSinceStartup - t0).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"juego\":").Append(Time.time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"cuadro\":").Append(Time.frameCount);
            sb.Append(",\"tipo\":\"").Append(tipo).Append('"');
            for (int i = 0; i + 1 < pares.Length; i += 2)
            {
                sb.Append(",\"").Append(pares[i]).Append("\":");
                var v = pares[i + 1];
                if (v is int || v is float || v is double || v is long) sb.Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                else sb.Append('"').Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture).Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
            sb.Append('}');
            log.WriteLine(sb.ToString());
        }

        public static void Marca(string que, string detalle = "") => Evento("marca", "que", que, "detalle", detalle);

        public static string Foto(string nombre)
        {
            if (Carpeta == null) return null;
            nFoto++;
            var ruta = Path.Combine(Carpeta, "fotos", nFoto.ToString("00") + "_" + nombre + ".png");
            ScreenCapture.CaptureScreenshot(ruta);
            Evento("foto", "archivo", Path.GetFileName(ruta));
            return ruta;
        }


        // ---------- guion de movimiento + muestreo por cuadro de la animacion ----------
        // plan: "t,adelante,derecha,correr,saltar;..." (t en segundos desde el inicio del guion; adelante/derecha en -1..1 respecto
        // del cuerpo, que no gira; correr y saltar 0/1). Cada cuadro aplica la ultima fila cuyo t ya paso y registra un evento "anim"
        // con los parametros del Animator, el estado, el clip, la altura de la raiz y el punto mas bajo del cuerpo y de los pies.
        struct Paso { public float t, fwd, right; public bool run, jump; }
        static readonly List<Paso> plan = new List<Paso>();
        static Soldier guionSoldado;
        static float guionT0;
        static float guionFin;
        static bool guionActivo;
        static int guionPasoSaltado = -1;
        static string guionNombre;
        static Vector3 guionPosPrev;

        public static string Guion(int id, string nombre, string texto, float duracion)
        {
            var s = ActorRegistry.FindById(id);
            if (s == null) return "sin soldado " + id;
            plan.Clear();
            foreach (var fila in texto.Split(';'))
            {
                var c = fila.Split(',');
                if (c.Length < 5) continue;
                plan.Add(new Paso { t = F(c[0]), fwd = F(c[1]), right = F(c[2]), run = c[3].Trim() == "1", jump = c[4].Trim() == "1" });
            }
            guionSoldado = s; guionNombre = nombre; guionT0 = Time.time; guionFin = Time.time + duracion; guionPasoSaltado = -1; guionPosPrev = s.transform.position;
            if (!guionActivo) { EditorApplication.update += GuionTick; guionActivo = true; }
            Marca("guion_inicio", nombre + " id=" + id + " dur=" + duracion);
            return "guion " + nombre + " con " + plan.Count + " filas";
        }

        static float F(string s) => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);

        static void GuionTick()
        {
            if (!EditorApplication.isPlaying || guionSoldado == null || log == null) { Detener(); return; }
            float t = Time.time - guionT0;
            if (Time.time >= guionFin) { Marca("guion_fin", guionNombre); Detener(); return; }
            Paso p = default; int idx = -1;
            for (int i = 0; i < plan.Count; i++) if (plan[i].t <= t) { p = plan[i]; idx = i; }
            var m = guionSoldado.Motor;
            if (idx >= 0)
            {
                var d = guionSoldado.transform.forward * p.fwd + guionSoldado.transform.right * p.right;
                m.SetRunning(p.run);
                if (d.sqrMagnitude > 0.0001f) m.Move(d.normalized, Time.deltaTime);
                if (p.jump && idx != guionPasoSaltado) { m.Jump(); guionPasoSaltado = idx; }
            }
            var an = guionSoldado.GetComponentInChildren<Animator>(true);
            float minCuerpo = float.MaxValue, minPie = float.MaxValue;
            foreach (var r in guionSoldado.GetComponentsInChildren<Renderer>(true))
                if (r.enabled && r.bounds.size.sqrMagnitude > 0f) minCuerpo = Mathf.Min(minCuerpo, r.bounds.min.y);
            foreach (var tr in guionSoldado.GetComponentsInChildren<Transform>(true))
            {
                var n = tr.name.ToLowerInvariant();
                if (n.Contains("foot") || n.Contains("toe") || n.Contains("pie")) minPie = Mathf.Min(minPie, tr.position.y);
            }
            string clip = "", estado = ""; float norm = 0f;
            if (an != null && an.runtimeAnimatorController != null)
            {
                var ci = an.GetCurrentAnimatorClipInfo(0);
                if (ci.Length > 0) { clip = ci[0].clip.name; for (int i = 1; i < ci.Length; i++) clip += "+" + ci[i].clip.name; }
                var si = an.GetCurrentAnimatorStateInfo(0);
                norm = si.normalizedTime; estado = an.IsInTransition(0) ? "transicion" : "estable";
            }
            float raizY = guionSoldado.transform.position.y;
            Evento("anim", "n", guionNombre, "tj", t, "paso", idx,
                "vel", an != null ? an.GetFloat("Velocidad") : 0f, "adel", an != null ? an.GetFloat("Adelante") : 0f, "lat", an != null ? an.GetFloat("Lateral") : 0f,
                "salto", guionSoldado.Motor.IsJumping ? 1 : 0, "agach", guionSoldado.Motor.IsCrouching ? 1 : 0,
                "raizY", raizY, "cuerpoMinY", minCuerpo == float.MaxValue ? 0f : minCuerpo, "pieMinY", minPie == float.MaxValue ? 0f : minPie,
                "clip", clip, "estado", estado, "normT", norm,
                "desplaz", (guionSoldado.transform.position - guionPosPrev).magnitude / Mathf.Max(0.0001f, Time.deltaTime));
            guionPosPrev = guionSoldado.transform.position;
        }

        static void Detener()
        {
            if (guionActivo) { EditorApplication.update -= GuionTick; guionActivo = false; }
            guionSoldado = null;
        }

        public static void Cerrar()
        {
            Detener();
            foreach (var s in subs) s.Dispose();
            subs.Clear();
            if (log != null) { log.Flush(); log.Dispose(); log = null; }
        }
    }
}
