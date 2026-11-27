using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 19, T0.2 (Docs/PLAN_R19_v2.md, seccion 6.2): tomas ANTES/DESPUES con camara propia a RenderTexture (patron R18bFoto) y posiciones fijas.
    // Salida: Assets/Validacion~/r19_<toma>_<etiqueta>.png, con la etiqueta de SessionState "R19Etiqueta" ("antes" por defecto).
    //   Bug994z (SC_Operacion): reaccion_senal, escudero_frente_dia/sala, pesado_marcador(_34), pesado_recarga(_34), muerte_tira_<ms>,
    //                           fachada_norte/sur/este/diagonal, umbral_blindado, anillo_5m
    //   Bug994y (SC_MainMenu):  menu_niveles
    //   Bug994x (SC_Tutorial):  tutorial_tanque_anillo, nivel1_panel
    // Repetir:  UnityEditor.SessionState.SetString("R19Etiqueta", "despues"); return SP.EditorTools.ChecksBugs065.CorrerLista("Bug994z,Bug994y,Bug994x", true);
    // Cada check termina en OK con la lista de tomas hechas y las que faltan (con el motivo). Lo que todavia no existe se anota "(n/a: <etiqueta>)".
    public static partial class ChecksBugs065
    {
        const string R19zClave = "R19Etiqueta";
        // "Estudio" de las tomas de enemigos: el patio sur del Centro de datos, lejos del combate del objetivo 2 (humo, granadas, aliados tirando).
        static readonly Vector3 R19zEstudio = new Vector3(0f, 0f, -100f);
        static string R19zEtiqueta => SessionState.GetString(R19zClave, "antes");
        static readonly List<string> r19zHechas = new List<string>();
        static readonly List<string> r19zFaltan = new List<string>();
        static readonly List<string> r19zNotas = new List<string>();

        static string r19zPrimerError;
        static void R19zAlLog(string msg, string stack, LogType t)
        {
            if (r19zPrimerError == null && (t == LogType.Error || t == LogType.Exception)) r19zPrimerError = msg.Length > 160 ? msg.Substring(0, 160) : msg;
        }
        static void R19zReiniciar()
        {
            r19zHechas.Clear(); r19zFaltan.Clear(); r19zNotas.Clear(); r19zPrimerError = null;
            Application.logMessageReceived -= R19zAlLog; Application.logMessageReceived += R19zAlLog;
        }
        static void R19zFalta(string toma, string motivo) { r19zFaltan.Add(toma + " (" + motivo + ")"); }
        static string R19zResumen()
        {
            string s = $"etiqueta={R19zEtiqueta}; {r19zHechas.Count} tomas [{string.Join(", ", r19zHechas)}]";
            s += r19zFaltan.Count > 0 ? $"; faltan {r19zFaltan.Count}: {string.Join("; ", r19zFaltan)}" : "; no falta ninguna";
            if (r19zNotas.Count > 0) s += "; notas: " + string.Join("; ", r19zNotas);
            Application.logMessageReceived -= R19zAlLog;
            if (r19zPrimerError != null) s += "; primer error de consola: " + r19zPrimerError.Replace('\n', ' ');
            return s;
        }

        // Camara propia a una RenderTexture de 1280x720 (como R18bFoto, pero con posicion y mira fijas).
        static bool R19zFoto(string toma, Vector3 camPos, Vector3 mira, float fov = 55f)
        {
            string ruta = RutaValidacion("r19_" + toma + "_" + R19zEtiqueta + ".png");
            GameObject go = null; RenderTexture rt = null; Texture2D tex = null; var prev = RenderTexture.active;
            try
            {
                go = new GameObject("R19zCamara"); var cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov; cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.05f; cam.farClipPlane = 800f;
                // las mismas capas que la camara del juego (sin esto salen los iconos del minimapa flotando en el cielo)
                var principal = CamaraPrincipal.Actual;
                cam.cullingMask = principal != null ? principal.cullingMask : ~(1 << 8);
                go.transform.position = camPos; go.transform.LookAt(mira);
                rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                File.WriteAllBytes(ruta, tex.EncodeToPNG());
                cam.targetTexture = null;
                r19zHechas.Add(toma);
                return true;
            }
            catch (System.Exception e) { R19zFalta(toma, "la foto fallo: " + e.Message); return false; }
            finally
            {
                RenderTexture.active = prev;
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // Piso bajo p (el mas alto que no sea un soldado), lanzando desde p.y + desde.
        static bool R19zPiso(Vector3 p, float desde, out Vector3 piso)
        {
            piso = p;
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + desde, p.z), Vector3.down, desde + 25f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.NegativeInfinity; Vector3 n = Vector3.up;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Soldier>() != null || h.collider.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (h.point.y > mejor) { mejor = h.point.y; n = h.normal; }
            }
            if (float.IsNegativeInfinity(mejor) || n.y < 0.8f) return false;
            piso = new Vector3(p.x, mejor, p.z);
            return true;
        }

        // Busca un lugar libre cerca de 'centro' (espiral) con una direccion libre de 'distCam' para la camara.
        // techo: 0 da igual, 1 a cielo abierto, 2 bajo techo.
        static bool R19zLugar(Vector3 centro, float rMax, float distCam, float radio, float alto, int techo, System.Func<Vector3, bool> dentro, out Vector3 pie, out Vector3 dir)
        {
            pie = centro; dir = Vector3.forward;
            for (float r = 0f; r <= rMax; r += 2.5f)
            {
                int n = r < 0.1f ? 1 : 12;
                for (int k = 0; k < n; k++)
                {
                    var p = centro + Quaternion.Euler(0f, k * 30f + r * 7f, 0f) * Vector3.forward * r;
                    if (dentro != null && !dentro(p)) continue;
                    if (!R19zPiso(p, techo == 2 ? 4f : 40f, out var piso)) continue;
                    bool hayTecho = Physics.Raycast(piso + Vector3.up * (alto + 0.3f), Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
                    if (techo == 1 && hayTecho) continue;
                    if (techo == 2 && !hayTecho) continue;
                    if (Physics.CheckCapsule(piso + Vector3.up * (radio + 0.15f), piso + Vector3.up * Mathf.Max(radio + 0.2f, alto - radio), radio, ~0, QueryTriggerInteraction.Ignore)) continue;
                    for (int j = 0; j < 12; j++)
                    {
                        var d = Quaternion.Euler(0f, j * 30f, 0f) * Vector3.forward;
                        if (Physics.SphereCast(piso + Vector3.up * 1.3f, 0.35f, d, out _, distCam + 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (!R19zPiso(piso + d * distCam, 4f, out var pisoCam) || Mathf.Abs(pisoCam.y - piso.y) > 0.5f) continue;   // la camara tiene piso debajo, al mismo nivel (no sobre un muro ni en el aire)
                        pie = piso; dir = d;
                        return true;
                    }
                }
            }
            return false;
        }

        // Direccion libre para fotografiar algo ya puesto (no se lo mueve).
        static Vector3 R19zCamaraLibre(Vector3 foco, float dist, float alto, Vector3 preferida)
        {
            Vector3 mejor = foco + preferida.normalized * dist + Vector3.up * alto; float mejorLibre = -1f;
            var objetivo = foco + Vector3.up * 1.1f;
            for (int k = 0; k < 16; k++)
            {
                var d = Quaternion.Euler(0f, k * 22.5f, 0f) * preferida.normalized;
                var pos = foco + d * dist + Vector3.up * alto;
                float libre = Physics.Raycast(objetivo, (pos - objetivo).normalized, out var imp, (pos - objetivo).magnitude, ~0, QueryTriggerInteraction.Ignore) && imp.collider.GetComponentInParent<Soldier>() == null ? imp.distance : 999f;
                if (libre > mejorLibre + 0.01f) { mejorLibre = libre; mejor = pos; }
                if (libre >= 999f) break;
            }
            return mejor;
        }

        static void R19zColocar(Soldier s, Vector3 pie, Vector3 mirarHacia)
        {
            var ag = s.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.enabled = false;
            if (s.Brain != null) { s.Brain.Pasivo = true; s.Brain.enabled = false; }
            s.transform.position = pie + Vector3.up * SoldierMotor.PivoteSobrePiso;
            var f = mirarHacia - pie; f.y = 0f;
            if (f.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            Physics.SyncTransforms();
        }

        static void R19zMoverJugador(Soldier yo, Vector3 p)
        {
            if (yo == null) return;
            if (R19zPiso(p, 30f, out var piso)) p = piso;
            yo.transform.position = p + Vector3.up * SoldierMotor.PivoteSobrePiso;
            Physics.SyncTransforms();
        }

        static List<Soldier> R19zFusileros(Soldier yo)
        {
            var l = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy || s.Brain == null) continue;
                if (s.Brain.MontadoEnVehiculo || s.Brain.PuestoFijoPorRol) continue;
                if (s.GetComponent<EnemigoPesado>() != null || s.GetComponent<EnemigoEscudero>() != null || s.GetComponent<EnemigoFrancotirador>() != null || s.GetComponent<PerroEnemigo>() != null) continue;
                l.Add(s);
            }
            if (yo != null) l.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
            return l;
        }

        static void R19zCongelarTodos(Soldier yo)
        {
            foreach (var s in ActorRegistry.All)
                if (s != null && s != yo && s.Brain != null && !s.Brain.IsPossessedByPlayer) { s.Brain.Pasivo = true; s.Brain.enabled = false; }   // sin cerebro tampoco disparan
        }

        static void R19zNotaTipo(string nombre, string que)
        {
            bool existe = System.Type.GetType(nombre + ", Assembly-CSharp") != null;
            r19zNotas.Add(que + (existe ? " existe" : " (n/a: " + R19zEtiqueta + ")"));
        }

        static void R19zNotaMiembro(System.Type t, string miembro, string que)
        {
            var f = t.GetField(miembro, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var p = f == null ? t.GetProperty(miembro, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
            object v = f != null ? f.GetValue(null) : p != null ? p.GetValue(null) : null;
            r19zNotas.Add(que + (f != null || p != null ? "=" + v : " (n/a: " + R19zEtiqueta + ")"));
        }

        static IEnumerable R19zCuadros(int n) { for (int i = 0; i < n; i++) yield return null; }

        static IEnumerable R19zEsperarJuego(float seg)
        {
            float t0 = Time.time, r0 = Time.realtimeSinceStartup;
            while (Time.time - t0 < seg && Time.realtimeSinceStartup - r0 < seg * 4f + 5f) yield return null;
        }

        // ================================================================ 994z: tomas de la Operacion
        static IEnumerator Bug994z()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            R19zReiniciar();
            R19zNotaTipo("SP.Operacion.UmbralDelBlindado", "UmbralDelBlindado");
            R19zNotaTipo("SP.Presentation.AjustesDeNivelR19", "AjustesDeNivelR19");
            R19zNotaMiembro(typeof(OperacionDirector), "RadioDeAbordaje", "RadioDeAbordaje");
            R19zNotaMiembro(typeof(OperacionDirector), "ZFuegoDelJefe", "ZFuegoDelJefe");
            r19zNotas.Add($"ZParaLaToma={OperacionDirector.ZParaLaToma}");
            r19zNotas.Add($"retiro de caidos a los {SP.Presentation.RetiroDeEnemigosCaidos.SegundosAntesDeRetirar} s");

            // ---------------------------------------------------------------- A) objetivo 2: enemigos (reaccion, escudero, pesado, muerte)
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var yo = W9Yo();
            if (yo == null) { foreach (var t in new[] { "reaccion_senal", "escudero_frente_dia", "escudero_frente_sala", "pesado_marcador", "pesado_recarga", "muerte_tira" }) R19zFalta(t, "sin jugador"); }
            else
            {
                var fus = R19zFusileros(yo);
                r19zNotas.Add($"{fus.Count} fusileros en el objetivo 2");

                // reaccion_senal: un enemigo herido por un ALIADO (no por el jugador)
                Soldier aliado = null;
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Player && s != yo && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) { aliado = s; break; }
                Soldier herido = null;
                foreach (var s in fus) if (!s.Brain.Pasivo) { herido = s; break; }
                if (aliado == null) R19zFalta("reaccion_senal", "no hay aliado vivo");
                else if (herido == null) R19zFalta("reaccion_senal", "no hay enemigo apto");
                else
                {
                    int antes = ReaccionAlDano.Aplicadas;
                    herido.Health.TakeDamage(5, aliado.Id);
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 1.5f && ReaccionAlDano.Aplicadas == antes) yield return null;
                    foreach (var x in Esperar(0.3f)) yield return x;
                    var pref = aliado.transform.position - herido.transform.position; pref.y = 0f; if (pref.sqrMagnitude < 0.01f) pref = Vector3.back;
                    pref = Quaternion.Euler(0f, 35f, 0f) * pref;
                    R19zFoto("reaccion_senal", R19zCamaraLibre(herido.transform.position, 5f, 1.8f, pref), herido.transform.position + Vector3.up * 1.4f);
                    r19zNotas.Add($"reaccion: aplicadas {antes}->{ReaccionAlDano.Aplicadas} ({herido.name} herido por {aliado.name}, estado {herido.Brain.State})");
                    fus.Remove(herido);
                }

                // A partir de aca, nadie se mueve ni dispara (si no, los aliados matan a los modelos y los enemigos tapan las tomas).
                R19zCongelarTodos(yo);
                foreach (var x in Esperar(1.5f)) yield return x;   // que se asienten el humo y las granadas que ya estaban en el aire

                // escudero_frente_dia / escudero_frente_sala
                Soldier esc = fus.Count > 0 ? fus[0] : null;
                if (esc == null) { R19zFalta("escudero_frente_dia", "sin fusilero para convertir"); R19zFalta("escudero_frente_sala", "sin fusilero para convertir"); }
                else
                {
                    fus.RemoveAt(0);
                    if (R19zLugar(R19zEstudio, 30f, 6f, 0.6f, 2.0f, 1, null, out var pie, out var dir))
                    {
                        R19zColocar(esc, pie, pie + dir);
                        EnemigoEscudero.Aplicar(esc);
                        foreach (var x in R19zCuadros(6)) yield return x;
                        foreach (var x in Esperar(0.4f)) yield return x;
                        R19zColocar(esc, pie, pie + dir);
                        R19zFoto("escudero_frente_dia", pie + dir * 6f + Vector3.up * 1.6f, pie + Vector3.up * 1.2f);
                    }
                    else { EnemigoEscudero.Aplicar(esc); R19zFalta("escudero_frente_dia", "no hubo lugar libre a cielo abierto"); }
                    if (R19zLugar(new Vector3(0f, 0f, -60f), 18f, 4.0f, 0.6f, 2.0f, 2, p => Mathf.Abs(p.x) < 24f && p.z > -78f && p.z < -42f, out var pieS, out var dirS))
                    {
                        R19zColocar(esc, pieS, pieS + dirS);
                        foreach (var x in R19zCuadros(4)) yield return x;
                        R19zColocar(esc, pieS, pieS + dirS);
                        R19zFoto("escudero_frente_sala", pieS + dirS * 4.0f + Vector3.up * 1.5f, pieS + Vector3.up * 1.15f);
                    }
                    else R19zFalta("escudero_frente_sala", "no hubo lugar libre dentro de la sala");
                }

                // pesado_marcador / pesado_recarga (Brain.Pasivo: si no, lo tapan sus explosiones)
                Soldier pes = fus.Count > 0 ? fus[0] : null;
                if (pes == null) { R19zFalta("pesado_marcador", "sin fusilero para convertir"); R19zFalta("pesado_recarga", "sin fusilero para convertir"); }
                else
                {
                    fus.RemoveAt(0);
                    pes.Brain.Pasivo = true;
                    if (!R19zLugar(R19zEstudio + new Vector3(10f, 0f, -4f), 30f, 8f, 1.1f, 4.0f, 1, null, out var pie, out var dir))
                    { R19zFalta("pesado_marcador", "no hubo lugar libre"); R19zFalta("pesado_recarga", "no hubo lugar libre"); }
                    else
                    {
                        R19zColocar(pes, pie, pie + dir);
                        var ep = EnemigoPesado.Aplicar(pes);
                        foreach (var x in R19zCuadros(6)) yield return x;
                        foreach (var x in Esperar(0.4f)) yield return x;
                        R19zColocar(pes, pie, pie + dir);
                        R19zMoverJugador(yo, pie + dir * 14f);   // la camara del juego cerca: el marcador se apaga a mas de 55 m
                        pes.Health.TakeDamage(10, yo.Id);          // la barra de vida solo se ve un rato despues de un golpe
                        foreach (var x in Esperar(0.5f)) yield return x;
                        R19zColocar(pes, pie, pie + dir);
                        var d34 = Quaternion.Euler(0f, 45f, 0f) * dir;
                        if (Physics.SphereCast(pie + Vector3.up * 1.5f, 0.3f, d34, out _, 8f, ~0, QueryTriggerInteraction.Ignore)) d34 = Quaternion.Euler(0f, -45f, 0f) * dir;
                        R19zFoto("pesado_marcador", pie + dir * 8f + Vector3.up * 2.4f, pie + Vector3.up * 2.0f);
                        R19zFoto("pesado_marcador_34", pie + d34 * 8f + Vector3.up * 2.4f, pie + Vector3.up * 2.0f);
                        var arma = pes.Weapon;
                        var rec = arma != null ? typeof(WeaponHolder).GetMethod("StartReload", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
                        if (rec == null) { R19zFalta("pesado_recarga", "no se pudo forzar la recarga (sin StartReload)"); }
                        else
                        {
                            try { if (rec.GetParameters().Length == 0) rec.Invoke(arma, null); } catch (System.Exception e) { r19zNotas.Add("StartReload: " + e.Message); }
                            pes.Health.TakeDamage(5, yo.Id);
                            foreach (var x in Esperar(0.8f)) yield return x;
                            R19zColocar(pes, pie, pie + dir);
                            r19zNotas.Add($"pesado: recargando={arma.IsReloading} anim propia={(ep != null && ep.AnimacionPropia)} recargando(anim)={(ep != null && ep.Recargando)} vida {pes.Health.Current}/{pes.Health.MaxHealth}");
                            R19zFoto("pesado_recarga", pie + dir * 8f + Vector3.up * 2.4f, pie + Vector3.up * 2.0f);
                            R19zFoto("pesado_recarga_34", pie + d34 * 8f + Vector3.up * 2.4f, pie + Vector3.up * 2.0f);
                        }
                    }
                }

                // muerte_tira: un fusilero muerto, fotografiado a los 0,3 / 2 / 4,1 / 4,5 / 13 / 15 s (hoy el retiro empieza a los 14 s)
                Soldier mue = fus.Count > 0 ? fus[0] : null;
                if (mue == null) R19zFalta("muerte_tira", "sin fusilero");
                else if (!R19zLugar(R19zEstudio + new Vector3(-10f, 0f, 2f), 30f, 5f, 0.6f, 2.0f, 1, null, out var pie, out var dir)) R19zFalta("muerte_tira", "no hubo lugar libre");
                else
                {
                    fus.RemoveAt(0);
                    R19zColocar(mue, pie, pie + dir);
                    foreach (var x in R19zCuadros(3)) yield return x;
                    R19zColocar(mue, pie, pie + dir);
                    var cam = pie + dir * 5f + Vector3.up * 1.8f; var mira = pie + Vector3.up * 0.4f;
                    float tMuerte = Time.time;
                    mue.Health.TakeDamage(99999, yo.Id);
                    foreach (float seg in new[] { 0.3f, 2f, 4.1f, 4.5f, 13f, 15f })
                    {
                        while (Time.time - tMuerte < seg) yield return null;
                        R19zFoto("muerte_tira_" + Mathf.RoundToInt(seg * 1000f).ToString("00000") + "ms", cam, mira);
                    }
                    int vis = 0; foreach (var r in mue.GetComponentsInChildren<Renderer>(true)) if (r.enabled) vis++;
                    r19zNotas.Add($"muerte: a los 15 s quedan {vis} renderers encendidos (retirados {SP.Presentation.RetiroDeEnemigosCaidos.Retirados})");
                }
            }

            // ---------------------------------------------------------------- B) objetivo 3: las cuatro fachadas del Centro de datos
            OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(2.5f)) yield return x;
            R19zCongelarTodos(W9Yo());
            R19zFoto("fachada_norte", new Vector3(0f, 3.5f, -17f), new Vector3(0f, 4f, -39.25f), 60f);
            R19zFoto("fachada_sur", new Vector3(0f, 3.5f, -103f), new Vector3(0f, 4f, -80.75f), 60f);
            R19zFoto("fachada_este", new Vector3(49f, 3.5f, -60f), new Vector3(26.75f, 4f, -60f), 60f);
            R19zFoto("fachada_diagonal", new Vector3(42f, 15f, -16f), new Vector3(16f, 2f, -50f), 60f);   // desde arriba del muro del patio (a nivel de piso lo tapa)

            // ---------------------------------------------------------------- C) objetivo 4, acercamiento: el blindado desde z -20 (el jugador queda en la sala)
            OperacionPrueba.Arrancar(4, 1);
            foreach (var x in Esperar(1f)) yield return x;
            var d = OperacionDirector.Instancia;
            if (d == null) { R19zFalta("umbral_blindado", "sin director"); R19zFalta("anillo_5m", "sin director"); }
            else
            {
                d.AbrirSalidaNortePorPrueba(false);
                foreach (var x in Esperar(3f)) yield return x;
                if (d.tanque == null) R19zFalta("umbral_blindado", "sin blindado");
                else
                {
                    R19zFoto("umbral_blindado", new Vector3(0f, 1.7f, -20f), d.tanque.transform.position + Vector3.up * 1.5f, 60f);
                    r19zNotas.Add($"umbral: toma activa={d.TomaDelJefeActiva} subfase={d.Subfase} blindado en z={d.tanque.transform.position.z:0.0}");
                }

                // ---------------------------------------------------------------- D) objetivo 4, abordaje: el anillo con el jugador a 5 m
                OperacionPrueba.Arrancar(4, 1, (int)SubfaseHuida.Abordaje);
                foreach (var x in Esperar(1.5f)) yield return x;
                var yo2 = W9Yo(); d = OperacionDirector.Instancia;
                if (yo2 == null || d == null || d.tanque == null) R19zFalta("anillo_5m", "sin jugador o tanque");
                else
                {
                    var t = d.tanque.transform.position;
                    var haciaYo = yo2.transform.position - t; haciaYo.y = 0f;
                    if (haciaYo.sqrMagnitude < 1f) haciaYo = Vector3.back;
                    haciaYo.Normalize();
                    R19zMoverJugador(yo2, t + haciaYo * 5f);
                    foreach (var x in Esperar(0.6f)) yield return x;
                    var a = AnilloDeSubida.Instancia;
                    var lado = Quaternion.Euler(0f, 30f, 0f) * haciaYo;
                    R19zFoto("anillo_5m", t + lado * 13f + Vector3.up * 7.5f, t + haciaYo * 2.5f, 60f);
                    r19zNotas.Add(a != null ? $"anillo: activo={AnilloDeSubida.EstaActivo} radio de uso {a.RadioDeUso:0.0} m, jugador a {a.DistanciaAlJugador:0.0} m, dentro={a.DentroDelRadio}, cartel='{a.TextoDelCartel}'" : "anillo: no hay instancia");
                }
            }
            R19zFalta("tutorial_canon", "n/a: " + R19zEtiqueta + " (el paso canon_simple no existe todavia)");
            ModoDios.Poner(false);
            Fin("OK " + R19zResumen());
        }

        // CapturarCanvas pone una camara en el ORIGEN del mundo que dibuja la capa del canvas hasta 10 m: cualquier renderer de esa capa que no
        // sea parte de un Canvas y quede cerca del origen sale "detras" del panel. Solo se ocultan (durante la toma) los que NO son de la escena:
        // restos de Plays anteriores con HideFlags.DontSave* (asi fue la mancha roja: anillos de AnilloDeAviso filtrados de checks de la Operacion).
        // Lo que si es de la escena (crater, escombros) se deja, para que 'antes' y 'despues' tengan el mismo fondo; se anota igual.
        static string R19zCapturarCanvasLimpio(Canvas cv, string archivo)
        {
            int capa = cv.gameObject.layer;
            var apagados = new List<Renderer>();
            var nombres = new List<string>();
            var caja = new Bounds(new Vector3(0f, 0f, 5f), new Vector3(30f, 30f, 14f));
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || r.gameObject.layer != capa || r.GetComponentInParent<Canvas>() != null) continue;
                if (!caja.Intersects(r.bounds)) continue;
                var raiz = r.transform.root.gameObject;
                bool resto = (raiz.hideFlags & HideFlags.DontSaveInEditor) != 0 || !raiz.scene.IsValid() || string.IsNullOrEmpty(raiz.scene.name);
                if (!resto) continue;
                r.enabled = false; apagados.Add(r);
                if (nombres.Count < 8) nombres.Add(r.name + "(" + r.GetType().Name + " en " + raiz.name + ")");
            }
            if (apagados.Count > 0) r19zNotas.Add($"{archivo}: {apagados.Count} renderers ajenos a la escena (restos de Plays anteriores) ocultos para la toma: {string.Join(", ", nombres)}");
            try { return CapturarCanvas(cv, 1600, 900, archivo); }
            finally { foreach (var r in apagados) if (r != null) r.enabled = true; }
        }

        // ================================================================ 994y: selector de niveles del menu
        [EscenaDelCheck("SC_MainMenu")]
        static IEnumerator Bug994y()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            R19zReiniciar();
            foreach (var x in Esperar(0.8f)) yield return x;
            var mm = Object.FindAnyObjectByType<SP.Presentation.MainMenuController>(FindObjectsInactive.Include);
            if (mm == null) { R19zFalta("menu_niveles", "no hay MainMenuController"); Fin("OK " + R19zResumen()); yield break; }
            // La toma muestra el estado "Nivel 1 hecho (01:23), Nivel 2 sin hacer": COMPLETADO en la tarjeta 1 y SIGUIENTE en la 2.
            // Se guardan y devuelven las claves de progreso (con la etiqueta 'antes' esto no cambia nada visible).
            var prefsY = R19aGuardarPrefs();
            SP.Core.ProgresoDeNiveles.Borrar(1); SP.Core.ProgresoDeNiveles.Borrar(2);
            SP.Core.ProgresoDeNiveles.Completar(1, 83f);
            mm.MostrarNiveles();
            mm.RefrescarProgresoDeNiveles();
            foreach (var x in Esperar(0.6f)) yield return x;
            r19zNotas.Add($"tarjetas: 1='{mm.TextoDeEstadoDelNivel(1)}' 2='{mm.TextoDeEstadoDelNivel(2)}'");
            var cv = mm.PanelNiveles != null ? mm.PanelNiveles.GetComponentInParent<Canvas>() : null;
            if (cv == null) { R19aRestaurarPrefs(prefsY); R19zFalta("menu_niveles", "el panel de niveles no tiene Canvas"); Fin("OK " + R19zResumen()); yield break; }
            cv = cv.rootCanvas;
            string err = R19zCapturarCanvasLimpio(cv, "r19_menu_niveles_" + R19zEtiqueta + ".png");
            if (err.Length == 0) r19zHechas.Add("menu_niveles"); else R19zFalta("menu_niveles", err);
            R19aRestaurarPrefs(prefsY);
            Fin("OK " + R19zResumen());
        }

        // ================================================================ 994x: tutorial (anillo del tanque y panel de victoria)
        [EscenaDelCheck("SC_Tutorial")]
        static IEnumerator Bug994x()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            R19zReiniciar();
            // El tutorial guarda el paso en PlayerPrefs y Finalizar() lo borra: se deja todo como estaba.
            // R19: Finalizar() tambien graba el progreso del Nivel 1 (ProgresoDeNiveles): se guarda y se devuelve todo con R19aGuardarPrefs.
            var prefsR19 = R19aGuardarPrefs();
            ModoDios.Poner(true);
            float t0 = Time.realtimeSinceStartup;
            while (SP.Tutorial.TutorialManager.Instance == null && Time.realtimeSinceStartup - t0 < 15f) yield return null;
            var tm = SP.Tutorial.TutorialManager.Instance;
            if (tm == null) { R19zFalta("tutorial_tanque_anillo", "no hay TutorialManager"); R19zFalta("nivel1_panel", "no hay TutorialManager"); }
            else
            {
                foreach (var x in Esperar(2f)) yield return x;
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 120f && (tm.PasoActual == null || tm.PasoActual.Id != "entrar_tanque") && !tm.Terminado)
                {
                    tm.SaltarPaso();
                    foreach (var x in Esperar(0.35f)) yield return x;
                }
                var drv = SP.Player.PlayerInputDriver.Activo; var yo = W9Yo();
                if (tm.PasoActual == null || tm.PasoActual.Id != "entrar_tanque") R19zFalta("tutorial_tanque_anillo", "no se llego al paso entrar_tanque (paso " + (tm.PasoActual != null ? tm.PasoActual.Id : "-") + ")");
                else if (drv == null || drv.Vehicle == null || yo == null) R19zFalta("tutorial_tanque_anillo", "sin jugador o tanque");
                else
                {
                    foreach (var x in Esperar(1f)) yield return x;
                    var t = drv.Vehicle.transform.position;
                    var haciaYo = yo.transform.position - t; haciaYo.y = 0f;
                    if (haciaYo.sqrMagnitude < 1f) haciaYo = Vector3.back;
                    haciaYo.Normalize();
                    R19zMoverJugador(yo, t + haciaYo * 5f);
                    foreach (var x in Esperar(0.6f)) yield return x;
                    var lado = Quaternion.Euler(0f, 30f, 0f) * haciaYo;
                    R19zFoto("tutorial_tanque_anillo", t + lado * 13f + Vector3.up * 7.5f, t + haciaYo * 2.5f, 60f);
                    var a = AnilloDeSubida.Instancia;
                    r19zNotas.Add($"tutorial: anillo activo={AnilloDeSubida.EstaActivo}" + (a != null ? $" radio {a.RadioDeUso:0.0}" : "") + $", radio de interaccion {drv.RadioDeInteraccion:0.0}");
                }

                // nivel1_panel: el panel de VictoriaTutorial, instanciado sin jugar el resto
                var centro = drv != null && drv.Vehicle != null ? drv.Vehicle.transform.position : Vector3.zero;
                var v = SP.Tutorial.VictoriaTutorial.Iniciar(tm, centro);
                foreach (var x in Esperar(2.4f)) yield return x;
                if (v == null || !v.PanelVisible || v.BotonMenu == null) R19zFalta("nivel1_panel", "el panel no aparecio");
                else
                {
                    var cv = v.BotonMenu.GetComponentInParent<Canvas>();
                    if (cv == null) R19zFalta("nivel1_panel", "el panel no tiene Canvas");
                    else
                    {
                        string err = R19zCapturarCanvasLimpio(cv.rootCanvas, "r19_nivel1_panel_" + R19zEtiqueta + ".png");
                        if (err.Length == 0) r19zHechas.Add("nivel1_panel"); else R19zFalta("nivel1_panel", err);
                        var textos = new List<string>();
                        foreach (var b in new[] { v.BotonRepetir, v.BotonSiguiente, v.BotonMenu }) if (b != null) { var tx = b.GetComponentInChildren<UnityEngine.UI.Text>(true); if (tx != null) textos.Add(tx.text); }
                        r19zNotas.Add("panel '" + v.TituloDelPanel + "': " + textos.Count + " botones [" + string.Join(" | ", textos) + "]");
                    }
                }
            }
            R19aRestaurarPrefs(prefsR19);
            ModoDios.Poner(false);
            Fin("OK " + R19zResumen());
        }
    }
}
