using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // Tanda #104-#132, P5: motor, efectos y lineas. #126 el soldado que queda apoyado mas alto que su piso baja (no levita a y=1,30),
    // #113 casquillos de las ametralladoras del heli (visibles de noche, chicos, efimeros), #125 la linea roja de ataque con tope de distancia.
    public static partial class ChecksBugs065
    {
        // Altura del piso bajo un punto (ignora soldados y vehiculos).
        static float PisoBajo(Vector3 p)
        {
            float mejor = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<Soldier>() != null || h.collider.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (h.point.y > mejor) mejor = h.point.y;
            }
            return mejor;
        }

        // Pone a 'doc' 0,5 m mas alto que su piso, lo camina 2 m y devuelve por res: [0]=y inicial, [1]=y final, [2]=segundos de simulacion.
        public static string Traza126 = "";
        static IEnumerable Caminar126(Soldier doc, Vector3 inicio, Vector3 dir, float[] res)
        {
            // 0,49 y no 0,50: con 0,50 exacto el redondeo de float decide si cae o levita; el reporte (y=1,30) era el borde de abajo.
            doc.transform.position = inicio + Vector3.up * (SoldierMotor.PivoteSobrePiso + SoldierMotor.HuecoParaCaer - 0.01f);
            doc.transform.rotation = Quaternion.LookRotation(dir);
            yield return null;
            res[0] = doc.transform.position.y;
            float s0 = Time.time, r0 = Time.realtimeSinceStartup; float movido = 0f; var ant = doc.transform.position;
            Traza126 = "ini(" + inicio.x.ToString("0.0") + "," + inicio.z.ToString("0.0") + ") dir(" + dir.x.ToString("0.0") + "," + dir.z.ToString("0.0") + ") "; float yAnt = doc.transform.position.y;
            while (movido < 2f && Time.realtimeSinceStartup - r0 < 10f)
            {
                doc.Motor.Move(dir, Time.deltaTime);
                if (Mathf.Abs(doc.transform.position.y - yAnt) > 0.01f && Traza126.Length < 600) { Traza126 += doc.transform.position.y.ToString("0.00") + (doc.Motor.IsJumping ? "j" : "") + "@" + movido.ToString("0.0") + "(" + doc.transform.position.x.ToString("0.0") + "," + doc.transform.position.z.ToString("0.0") + ") "; }
                yAnt = doc.transform.position.y;
                movido += Vector3.Distance(new Vector3(doc.transform.position.x, 0f, doc.transform.position.z), new Vector3(ant.x, 0f, ant.z));
                ant = doc.transform.position;
                yield return null;
            }
            res[2] = Time.time - s0;
            res[1] = doc.transform.position.y;
        }

        // ---------------------------------------------------------------- #126 Doc no levita a y=1,30
        static IEnumerator Bug126()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            Soldier doc = null;
            foreach (var s in Soldados(TeamId.Player)) if (s.Role == RoleType.Medic && !MandoTactico.EsMiliciano(s)) { doc = s; break; }
            if (doc == null) { Fin("FALLO no esta Doc"); yield break; }
            // El cerebro lo mueve WorldSimulationDriver aunque se apague el componente: se lo deja "poseido" para que no haga Tick (si no, un aliado lejos del jugador se reubica solo).
            var br = doc.Brain; bool poseidoAntes = br != null && br.IsPossessedByPlayer; if (br != null) br.IsPossessedByPlayer = true;
            var sb = new StringBuilder(); bool ok = true;
            try
            {
                // Suelo plano de la zona de la plaza (verificado por rayo en el punto y a 2 m).
                var centro = d.plaza != null ? d.plaza.position : doc.transform.position;
                Vector3 inicio = default; bool hay = false; Vector3 dir = Vector3.forward; float piso = 0f;
                foreach (var ang in new float[] { 0, 90, 180, 270, 45, 135, 225, 315 })
                {
                    foreach (var rad in new float[] { 6f, 10f, 14f })
                    {
                        var p0 = centro + Quaternion.Euler(0, ang, 0) * Vector3.forward * rad;
                        var dd = Quaternion.Euler(0, ang + 90f, 0) * Vector3.forward;
                        float f0 = PisoBajo(p0), f1 = PisoBajo(p0 + dd * 2.5f);
                        if (float.IsNegativeInfinity(f0) || float.IsNegativeInfinity(f1) || Mathf.Abs(f0 - f1) > 0.05f) continue;
                        if (Physics.CheckCapsule(p0 + Vector3.up * (f0 + 0.6f - p0.y), p0 + Vector3.up * (f0 + 1.4f - p0.y), 0.45f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Raycast(new Vector3(p0.x, f0 + 0.6f, p0.z), dd, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                        inicio = new Vector3(p0.x, f0, p0.z); dir = dd; piso = f0; hay = true; break;
                    }
                    if (hay) break;
                }
                if (!hay) { Fin("FALLO no se encontro un tramo plano libre de 2,5 m cerca de la plaza"); yield break; }
                // Doc apoyado 0,5 m mas alto que su piso (y = piso + 0,8 + 0,5 = 1,30 con piso 0): la situacion del reporte.
                // ANTES (bug reproducido, con el descenso de escalon apagado): levita a 1,30 aunque camine 2 m.
                var res = new float[3];
                SoldierMotor.DescensoDeEscalonActivo = false;
                try { foreach (var x in Caminar126(doc, inicio, dir, res)) yield return x; }
                finally { SoldierMotor.DescensoDeEscalonActivo = true; }
                float yAntes = res[1];
                RenderTemporal("v3_126_antes", doc.transform.position + new Vector3(-3f, 0.6f, -3f), doc.transform.position, 40f);
                bool bugReproducido = yAntes > piso + SoldierMotor.PivoteSobrePiso + 0.4f;
                if (!bugReproducido) ok = false;
                sb.Append($"ANTES (sin la correccion): Doc y {res[0]:0.00} -> {yAntes:0.00} tras caminar 2 m: levita={bugReproducido}{(bugReproducido ? "" : " (NO SE REPRODUJO)")} traza[{Traza126}]; ");
                // DESPUES: baja al piso.
                foreach (var x in Caminar126(doc, inicio, dir, res)) yield return x;
                float y0 = res[0], y1 = res[1], t1 = res[2];
                RenderTemporal("v3_126_despues", doc.transform.position + new Vector3(-3f, 0.6f, -3f), doc.transform.position, 40f);
                bool ok1 = Mathf.Abs(y1 - (piso + SoldierMotor.PivoteSobrePiso)) <= 0.05f && t1 <= 1.0f;
                if (!ok1) ok = false;
                sb.Append($"piso={piso:0.00}; DESPUES: Doc y {y0:0.00} -> {y1:0.00} tras 2 m en {t1:0.00} s (se pide {piso + 0.8f:0.00}+-0,05 en <= 1 s){(ok1 ? "" : " MAL")}; ");
                // Un escalon real (gap > 0,5 m) sigue siendo caida (no baja de golpe): se lo pone 1,5 m mas alto y se mira que baje con gravedad.
                doc.transform.position = new Vector3(doc.transform.position.x, piso + SoldierMotor.PivoteSobrePiso + 1.5f, doc.transform.position.z);
                doc.Motor.Move(dir, 0.02f);
                bool enCaida = doc.Motor.IsJumping;
                if (!enCaida) ok = false;
                sb.Append($"con 1,5 m de hueco sigue la caida normal: IsJumping={enCaida}{(enCaida ? "" : " MAL")}; ");
                float rr = Time.realtimeSinceStartup;
                while (doc.Motor.IsJumping && Time.realtimeSinceStartup - rr < 6f) yield return null;
                float y2 = doc.transform.position.y;
                bool ok2 = Mathf.Abs(y2 - (piso + SoldierMotor.PivoteSobrePiso)) <= 0.05f;
                if (!ok2) ok = false;
                sb.Append($"aterriza en y={y2:0.00}{(ok2 ? "" : " MAL")}");
            }
            finally { if (br != null) br.IsPossessedByPlayer = poseidoAntes; }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #113 casquillos del heli
        static IEnumerator Bug113()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(1.0f)) yield return x;
            var cam = CamaraPrincipal.Actual;
            if (cam == null) { Fin("FALLO sin camara"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            CasquillosDeMetralleta.PisoY = cam.transform.position.y - 6f;
            var origen = cam.transform.position + cam.transform.forward * 6f;
            // Lejos (> 40 m de la camara): no se expulsa nada.
            int e0 = CasquillosDeMetralleta.Expulsados;
            CasquillosDeMetralleta.Expulsar(cam.transform.position + cam.transform.forward * 60f, Vector3.right);
            bool lejosNo = CasquillosDeMetralleta.Expulsados == e0;
            if (!lejosNo) ok = false;
            sb.Append($"a 60 m no se expulsa={lejosNo}{(lejosNo ? "" : " MAL")}; ");
            // 20 casquillos cerca.
            e0 = CasquillosDeMetralleta.Expulsados;
            for (int i = 0; i < 20; i++) CasquillosDeMetralleta.Expulsar(origen + Random.insideUnitSphere * 0.3f, Vector3.right);
            int exp = CasquillosDeMetralleta.Expulsados - e0;
            yield return null;
            int activosInicio = CasquillosDeMetralleta.Activos;
            if (exp != 20 || activosInicio < 20) ok = false;
            sb.Append($"expulsados {exp}/20, activos {activosInicio}; ");
            // Material: emision > 0 y piezas del tamano nuevo.
            var mat = CasquillosDeMetralleta.MaterialDeLaton;
            Color em = mat != null && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            float emMax = Mathf.Max(em.r, Mathf.Max(em.g, em.b));
            if (emMax <= 0f || !mat.IsKeywordEnabled("_EMISSION")) ok = false;
            var raiz = GameObject.Find("CasquillosDeMetralleta");
            Vector3 esc = raiz != null && raiz.transform.childCount > 0 ? raiz.transform.GetChild(0).localScale : Vector3.zero;
            bool tamOk = (esc - CasquillosDeMetralleta.Tamano).magnitude < 0.001f && esc.z <= 0.07f + 1e-4f;
            if (!tamOk) ok = false;
            sb.Append($"emision max={emMax:0.00} (> 0), tamano {esc.x:0.000}x{esc.y:0.000}x{esc.z:0.000}{(emMax > 0f && tamOk ? "" : " MAL")}; ");
            RenderTemporal("v3_113_casquillos", cam.transform.position, origen, 40f);
            // Todos inactivos pasado 1,2 s de simulacion (vida 1,0 s).
            float s0 = Time.time, r0 = Time.realtimeSinceStartup;
            while (Time.time - s0 < 1.2f && Time.realtimeSinceStartup - r0 < 30f) yield return null;
            yield return null;
            int activosFin = CasquillosDeMetralleta.Activos;
            if (activosFin != 0) ok = false;
            sb.Append($"activos tras {Time.time - s0:0.0} s de simulacion={activosFin}{(activosFin == 0 ? "" : " MAL")}; ");
            // El dialogo de reporte no deja el juego congelado: timeScale > 0 y sin dialogo abierto.
            bool sinCongelar = Time.timeScale > 0f && !ReporteDeBug.Abierto;
            if (!sinCongelar) ok = false;
            sb.Append($"timeScale={Time.timeScale:0.0} dialogo abierto={ReporteDeBug.Abierto}{(sinCongelar ? "" : " MAL")}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #125 la linea roja de ataque tiene tope de distancia y visibilidad
        static IEnumerator Bug125()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            ArrancarEn(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var mgr = Object.FindAnyObjectByType<AttackLineManager>();
            var drv = PlayerInputDriver.Activo; var rig = CameraRig.Instance;
            var yo = Poseido();
            if (mgr == null || rig == null || yo == null || drv == null) { Fin($"FALLO faltan piezas (manager={mgr != null} rig={rig != null} poseido={yo != null})"); yield break; }
            // Decision pura.
            bool a80 = AttackLineManager.DebeDibujar(80f, 100f, true, out _);
            bool a20 = AttackLineManager.DebeDibujar(20f, 100f, true, out float op20);
            bool a40 = AttackLineManager.DebeDibujar(40f, 100f, true, out float op40);
            bool oculto = AttackLineManager.DebeDibujar(20f, 100f, false, out _);
            if (a80 || !a20 || !a40 || oculto || !(op40 < op20) || op20 < 0.99f) ok = false;
            sb.Append($"decision: 80 m -> {a80}, 20 m -> {a20} (opacidad {op20:0.00}), 40 m -> {a40} (opacidad {op40:0.00}), enemigo no revelado -> {oculto}{(!a80 && a20 && a40 && !oculto && op40 < op20 ? "" : " MAL")}; ");

            // Integracion en RTS con un aliado y un enemigo trabado.
            var apagados = ApagarIaAjena(yo);
            var modoPrevio = rig.Mode;
            Soldier aliado = null, enemigo = null;
            foreach (var s in Soldados(TeamId.Player)) if (s != yo && s.Brain != null) { aliado = s; break; }
            foreach (var s in Soldados(TeamId.Enemy)) { enemigo = s; break; }
            if (aliado == null || enemigo == null) { foreach (var b in apagados) if (b != null) b.enabled = true; Fin("FALLO sin aliado o sin enemigo"); yield break; }
            var campoTarget = typeof(AiBrain).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var setState = typeof(AiBrain).GetMethod("SetState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try
            {
                rig.SetMode(ControlMode.Rts);
                var br = aliado.Brain; br.enabled = false;
                var eb = enemigo.Brain; if (eb != null) eb.enabled = false;
                campoTarget.SetValue(br, enemigo);
                setState.Invoke(br, new object[] { AiState.Attack });
                InteligenciaDeEnemigos.Revelar(enemigo.Id);
                var basePos = aliado.transform.position;
                bool Linea() => mgr.TieneLinea(aliado.Id);
                // 80 m: ninguna linea.
                enemigo.transform.position = basePos + new Vector3(80f, 0f, 0f);
                yield return null; yield return null;
                bool l80 = Linea();
                // ANTES (bug reproducido, sin tope): a 80 m la linea se dibuja.
                AttackLineManager.TopeActivo = false;
                yield return null; yield return null;
                bool l80antes = Linea();
                RenderTemporal("v3_125_linea_80m_antes", basePos + new Vector3(40f, 90f, -30f), basePos + new Vector3(40f, 0f, 0f), 60f);
                AttackLineManager.TopeActivo = true;
                yield return null; yield return null;
                sb.Append($"ANTES (sin tope): a 80 m linea={l80antes}{(l80antes ? "" : " (NO SE REPRODUJO)")}; ");
                if (!l80antes) ok = false;
                // 20 m: una linea.
                enemigo.transform.position = basePos + new Vector3(20f, 0f, 0f);
                yield return null; yield return null;
                bool l20 = Linea(); int total20 = AttackLineManager.LineasActivas;
                RenderTemporal("v3_125_linea_20m", basePos + new Vector3(10f, 40f, -12f), basePos + new Vector3(10f, 0f, 0f), 50f);
                // 20 m pero enemigo no revelado: sin linea (no se le regala su posicion al jugador). Se usa otro enemigo al que nadie vio.
                Soldier oculto2 = null;
                foreach (var s in Soldados(TeamId.Enemy)) if (s != enemigo && !InteligenciaDeEnemigos.EstaRevelado(s.Id)) { oculto2 = s; break; }
                bool l20oculto = false;
                if (oculto2 != null)
                {
                    var o2b = oculto2.Brain; if (o2b != null) o2b.enabled = false;
                    campoTarget.SetValue(br, oculto2);
                    oculto2.transform.position = basePos + new Vector3(20f, 0f, 3f);
                    yield return null; yield return null;
                    l20oculto = Linea();
                    if (l20oculto) ok = false;
                }
                if (l80 || !l20) ok = false;
                sb.Append($"RTS: enemigo trabado a 80 m -> linea={l80} (se pide no); a 20 m -> linea={l20} (lineas activas en total={total20}, incluye las de otros soldados); a 20 m sin revelar -> {(oculto2 == null ? "n/d" : l20oculto.ToString())}{(!l80 && l20 && !l20oculto ? "" : " MAL")}");
            }
            finally
            {
                AttackLineManager.TopeActivo = true;
                rig.SetMode(modoPrevio);
                campoTarget.SetValue(aliado.Brain, null);
                foreach (var b in apagados) if (b != null) b.enabled = true;
                aliado.Brain.enabled = true;
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
