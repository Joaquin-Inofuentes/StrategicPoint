using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.Core
{
    // Capturas DESDE LA BUILD para validar un cambio de antes/despues (2026-11-13): "StrategicPoint.exe -capturas <carpeta>" entra solo a la Operacion,
    // arma cada situacion (sala de servidores en RTS, pesado junto a un enemigo comun, escudero recibiendo disparos y muriendo, cuerpo que cae en una
    // escalera, terminal de las celdas, perros de la ciudad), la fotografia con una camara propia y escribe captura_info.txt con lo medido.
    // Es la misma secuencia para la build "antes" y la "despues": solo cambia el juego. Un jugador nunca la dispara (necesita el argumento).
    public sealed class CapturasDeBuild : MonoBehaviour
    {
        const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        string dir; readonly StringBuilder info = new StringBuilder();
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-capturas");
            if (i < 0 || i + 1 >= a.Length || FindFirstObjectByType<CapturasDeBuild>() != null) return;
            var go = new GameObject("CapturasDeBuild"); DontDestroyOnLoad(go);
            var c = go.AddComponent<CapturasDeBuild>(); c.dir = a[i + 1];
            c.StartCoroutine(c.Todo());
        }

        void Dato(string s) { info.AppendLine(s); }
        static string F(float v) => v.ToString("0.00", Ci);
        static string V(Vector3 v) => $"({v.x.ToString("0.0", Ci)},{v.y.ToString("0.0", Ci)},{v.z.ToString("0.0", Ci)})";

        IEnumerator Espera(float s) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < s) yield return null; }

        // Ejecuta una toma sin que un error corte las demas: lo anota y sigue.
        IEnumerator Seguro(string nombre, IEnumerator it)
        {
            Dato("== " + nombre);
            while (true)
            {
                bool hay;
                try { hay = it.MoveNext(); }
                catch (Exception e) { Dato("ERROR en " + nombre + ": " + e.GetType().Name + " " + e.Message + "\n" + e.StackTrace); Time.timeScale = 1f; yield break; }
                if (!hay) yield break;
                yield return it.Current;
            }
        }

        IEnumerator Todo()
        {
            float inicio = Time.realtimeSinceStartup;
            try { Directory.CreateDirectory(dir); } catch (Exception) { }
            yield return Espera(2f);
            SceneLoader.Cargar("SC_Operacion");
            while (OperacionDirector.Instancia == null && Time.realtimeSinceStartup - inicio < 90f) yield return null;
            yield return Espera(4f);
            PartidaGuardada.ModoPrueba = true; ModoDios.Poner(true);
            CinematicaDeOperacion.Saltar(); CinematicaDeRapel.Saltar();
            Dato("version=" + Application.version + " escena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            yield return Seguro("sala", Sala());
            yield return Seguro("terminal", Terminal());
            yield return Seguro("criaturas", Criaturas());
            yield return Seguro("escalera", Escalera());
            yield return Seguro("ciudad", Ciudad());
            Dato("FIN " + F(Time.realtimeSinceStartup - inicio) + " s");
            try { File.WriteAllText(Path.Combine(dir, "captura_info.txt"), info.ToString()); } catch (Exception) { }
            Application.Quit();
        }

        static void Saltar(int objetivo)
        {
            var d = OperacionDirector.Instancia; if (d == null) return;
            CinematicaDeOperacion.Saltar();
            d.SaltarA(OperacionDirector.Orden[objetivo - 1], true, 0);
        }

        static Soldier Yo() { var d = PlayerInputDriver.Activo; return d != null && d.Brain != null ? d.Brain.Current : null; }
        static Vector3 EnMalla(Vector3 p) => UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 6f, UnityEngine.AI.NavMesh.AllAreas) ? h.position : p;

        static Soldier Clon(Vector3 pos, Vector3 haciaDir)
        {
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.GetComponent<EnemigoPesado>() == null && a.GetComponent<EnemigoEscudero>() == null && a.GetComponent<PerroEnemigo>() == null && a.name.StartsWith("Mansion_Enemigo_")) { molde = a; break; }
            if (molde == null) foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.GetComponent<EnemigoPesado>() == null && a.GetComponent<EnemigoEscudero>() == null && a.GetComponent<PerroEnemigo>() == null) { molde = a; break; }
            if (molde == null) return null;
            haciaDir.y = 0f; if (haciaDir.sqrMagnitude < 0.01f) haciaDir = Vector3.forward;
            var go = Instantiate(molde.gameObject, pos, Quaternion.LookRotation(haciaDir.normalized), molde.transform.parent);
            go.SetActive(true);
            var r = go.GetComponent<Soldier>(); if (r != null) ApoyoEnElPiso.Apoyar(r.transform);
            return r;
        }

        IEnumerator ConMolde()
        {
            var d = OperacionDirector.Instancia;
            if (d != null) typeof(OperacionDirector).GetField("relojMansion", Priv)?.SetValue(d, 100000f);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 14f)
            {
                foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.name.StartsWith("Mansion_Enemigo_")) yield break;
                yield return null;
            }
        }

        static void AlejarAliados(Vector3 desde)
        {
            int k = 0;
            foreach (var a in ActorRegistry.All)
                if (a != null && a.Team == TeamId.Player && a.gameObject.activeInHierarchy && a != Yo())
                { a.transform.position = EnMalla(desde + Quaternion.Euler(0f, 120f * (k++ % 3), 0f) * Vector3.forward * 110f); ApoyoEnElPiso.Apoyar(a.transform); }
        }

        // Primer punto de camara que no queda dentro de una pared y con linea libre hacia 'centro'.
        static Vector3 Despejada(Vector3 centro, params Vector3[] candidatos)
        {
            foreach (var c in candidatos) if (!Physics.CheckSphere(c, 0.4f, ~0, QueryTriggerInteraction.Ignore) && !Physics.Linecast(c, centro, ~0, QueryTriggerInteraction.Ignore)) return c;
            foreach (var c in candidatos) if (!Physics.Linecast(c, centro, ~0, QueryTriggerInteraction.Ignore)) return c;
            return candidatos[0];
        }

        // ---- fotos ----
        List<Behaviour> apagados = new List<Behaviour>(); List<Renderer> apagadosR = new List<Renderer>();
        void OcultarMarcadores()
        {
            apagados.Clear(); apagadosR.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) if (c != null && c.enabled && c.renderMode != RenderMode.ScreenSpaceOverlay) { c.enabled = false; apagados.Add(c); }
            var cara = typeof(DiamondGizmo).GetField("caraCompartida", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Mesh;
            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                if (mf == null) continue; var mr = mf.GetComponent<MeshRenderer>(); if (mr == null || !mr.enabled) continue;
                if ((cara != null && mf.sharedMesh == cara) || mf.name == "AlertIndicator" || mf.name.Contains("Marker")) { mr.enabled = false; apagadosR.Add(mr); }
            }
        }
        void MostrarMarcadores()
        {
            foreach (var c in apagados) if (c != null) c.enabled = true;
            foreach (var r in apagadosR) if (r != null) r.enabled = true;
        }

        // Camara propia a un RenderTexture (no depende del rig del juego). congelar: el tiempo se detiene durante la toma.
        IEnumerator Foto(string id, Vector3 pos, Vector3 mira, float fov = 50f, bool congelar = true, Vector3? arriba = null, int w = 1600, int h = 900)
        {
            float previo = Time.timeScale; if (congelar) Time.timeScale = 0f;
            OcultarMarcadores();
            var go = new GameObject("CamCaptura"); var cam = go.AddComponent<Camera>();
            cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(mira - pos, arriba ?? Vector3.up);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 900f;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            yield return null; yield return null;
            cam.Render();
            var previa = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previa;
            try { File.WriteAllBytes(Path.Combine(dir, id + ".png"), tex.EncodeToPNG()); } catch (Exception) { }
            cam.targetTexture = null; Destroy(tex); rt.Release(); Destroy(rt); Destroy(go);
            MostrarMarcadores();
            Time.timeScale = previo;
        }

        // Lo que ve el jugador de verdad: la camara del juego tal cual (con HUD).
        IEnumerator FotoJuego(string id)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(dir, id + ".png"), tex.EncodeToPNG()); } catch (Exception) { }
            Destroy(tex);
        }

        // ---------------------------------------------------------------- 1. sala de computadoras / servidores en RTS
        IEnumerator Sala()
        {
            Saltar(3); yield return Espera(3f);
            var rig = CameraRig.Instance; var centro = new Vector3(0f, 0f, -60f);
            var yoSala = Yo(); if (yoSala != null) { yoSala.transform.position = EnMalla(new Vector3(0f, 1f, -56f)); Physics.SyncTransforms(); }
            if (rig != null) rig.SetMode(ControlMode.Rts, centro);
            yield return Espera(5f);
            Dato("piezas ocultas en RTS=" + SalaDespejadaEnRts.PiezasOcultas + " modo=" + (rig != null ? rig.Mode.ToString() : "sin rig"));
            yield return FotoJuego("sala_rts_juego");
            yield return Foto("sala_rts_cenital", centro + new Vector3(0f, 38f, -9f), centro, 60f, false);
            yield return Foto("sala_rts_diagonal", new Vector3(0f, 26f, -93f), centro, 55f, false);
            // cuantas piezas por encima de 2,5 m siguen dibujandose dentro de la sala (vigas, luces, ductos)
            int visibles = 0; var nombres = new SortedDictionary<string, int>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || r is ParticleSystemRenderer || r is LineRenderer || !r.enabled) continue;
                var b = r.bounds; if (b.min.y < 2.5f || Mathf.Abs(b.center.x) > 26f || b.center.z < -80f || b.center.z > -40f) continue;
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                visibles++; string n = r.name; nombres[n] = nombres.TryGetValue(n, out var q) ? q + 1 : 1;
            }
            Dato("piezas visibles sobre 2,5 m en la sala=" + visibles);
            foreach (var kv in nombres) Dato("   " + kv.Key + " x" + kv.Value);
            if (rig != null) rig.SetMode(ControlMode.Fps);
        }

        // ---------------------------------------------------------------- 2. terminal de las celdas
        IEnumerator Terminal()
        {
            Saltar(5); yield return Espera(3f);
            var d = OperacionDirector.Instancia; var jug = Yo();
            if (jug != null) { jug.transform.position = new Vector3(308.7f, 13.9f, 270.2f); Physics.SyncTransforms(); }
            yield return Espera(2f);
            if (d.TerminalDeCeldas == null) typeof(OperacionDirector).GetMethod("CrearTerminalDeCeldas", Priv)?.Invoke(d, null);
            Physics.SyncTransforms(); yield return Espera(0.5f);
            var tt = d.TerminalDeCeldas; if (tt == null) { Dato("SIN TERMINAL"); yield break; }
            var piezas = new StringBuilder();
            foreach (var r in tt.GetComponentsInChildren<Renderer>(true)) piezas.Append(r.name).Append(' ');
            Dato("terminal en " + V(tt.position) + " mira " + V(tt.forward) + " piezas: " + piezas);
            yield return Foto("terminal_frente", tt.position + tt.forward * 3.2f + Vector3.up * 1.7f, tt.position + Vector3.up * 1.0f, 60f);
            yield return Foto("terminal_amplia", tt.position + tt.forward * 6.5f + tt.right * 2.5f + Vector3.up * 2.4f, tt.position + Vector3.up * 0.9f, 62f);
        }

        // ---------------------------------------------------------------- 3. pesado, escudero
        IEnumerator Criaturas()
        {
            Saltar(5); yield return Espera(3f);
            yield return ConMolde();
            var yo = Yo(); if (yo == null) { Dato("SIN JUGADOR"); yield break; }
            AlejarAliados(yo.transform.position);
            var baseP = EnMalla(yo.transform.position + yo.transform.forward * 14f);
            var fw = -yo.transform.forward; fw.y = 0f; fw.Normalize(); var der = Vector3.Cross(Vector3.up, fw).normalized;
            var normal = Clon(baseP + der * -1.6f, fw); var pesado = Clon(baseP + der * 0.4f, fw);
            if (normal == null || pesado == null) { Dato("SIN MOLDE"); yield break; }
            EnemigoPesado.Aplicar(pesado);
            Soldier aliado = null; foreach (var a in ActorRegistry.All) if (a != null && a != yo && a.Team == TeamId.Player && a.Role != RoleType.Civilian && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy) { aliado = a; break; }
            if (aliado != null) { aliado.transform.position = baseP + der * 2.4f; aliado.transform.rotation = Quaternion.LookRotation(fw); ApoyoEnElPiso.Apoyar(aliado.transform); }
            yield return Espera(1.5f);
            if (normal.Brain != null) normal.Brain.Anulado = true; if (pesado.Brain != null) pesado.Brain.Anulado = true; if (aliado != null && aliado.Brain != null) aliado.Brain.Anulado = true;
            normal.Motor.SetCrouching(false); pesado.Motor.SetCrouching(false);
            var smrP = pesado.GetComponentInChildren<SkinnedMeshRenderer>(true); var smrN = normal.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Dato($"pesado: escala raiz {V(pesado.transform.localScale)} alto {F(smrP != null ? smrP.bounds.size.y : 0f)} m ancho {F(smrP != null ? smrP.bounds.size.x : 0f)} m | normal: alto {F(smrN != null ? smrN.bounds.size.y : 0f)} m ancho {F(smrN != null ? smrN.bounds.size.x : 0f)} m | velocidad {F(pesado.Motor.MoveSpeed)} contra {F(normal.Motor.MoveSpeed)}");
            // el encuadre se calcula con el tamano real de los modelos (un pesado doble no entra en el encuadre de uno chico)
            var bnd = new Bounds(pesado.transform.position, Vector3.zero);
            foreach (var q in new[] { pesado, normal, aliado }) if (q != null) foreach (var rr in q.GetComponentsInChildren<Renderer>()) bnd.Encapsulate(rr.bounds);
            float distG = Mathf.Max(4.6f, bnd.extents.magnitude / Mathf.Tan(26f * Mathf.Deg2Rad) * 1.1f);
            yield return Foto("pesado_comparacion", bnd.center - fw * distG + Vector3.up * 0.4f, bnd.center, 52f);
            var bP = new Bounds(pesado.transform.position, Vector3.zero); foreach (var rr in pesado.GetComponentsInChildren<Renderer>()) bP.Encapsulate(rr.bounds);
            float distP = Mathf.Max(3.6f, bP.size.y * 0.5f / Mathf.Tan(21f * Mathf.Deg2Rad) * 1.3f);
            yield return Foto("pesado_frente", bP.center - fw * distP + der * (distP * 0.18f), bP.center, 42f);

            // ---- escudero: primero a un lado, mirando al jugador
            var posE = EnMalla(baseP + der * 5.5f);
            var esc = Clon(posE, yo.transform.position - posE); if (esc == null) { Dato("SIN MOLDE ESCUDERO"); yield break; }
            EnemigoEscudero.Aplicar(esc);
            yield return Espera(1.2f);
            if (esc.Brain != null) esc.Brain.Anulado = true;
            esc.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(yo.transform.position - esc.transform.position, Vector3.up));
            yield return Espera(0.3f);
            var comp = esc.GetComponent<EnemigoEscudero>();
            var escudo = esc.transform.Find("Escudo");
            Dato("escudero: vida " + esc.Health.Current + " escudo hijo=" + (escudo != null));
            yield return Foto("escudero_frente", esc.transform.position + esc.transform.forward * 3.4f + esc.transform.right * 1.4f + Vector3.up * 1.4f, esc.transform.position + Vector3.up * 1.0f, 50f);
            int v0 = esc.Health.Current;
            // disparos de frente: contra el escudo (chispas); medimos las particulas vivas un instante despues
            int antes = ContarParticulas();
            for (int i = 0; i < 4; i++) { esc.Health.TakeDamage(6, yo.Id, false); yield return null; }
            yield return Espera(0.06f);
            int durante = ContarParticulas();
            Dato($"disparo al escudo: vida {v0} -> {esc.Health.Current} bloqueados {(comp != null ? comp.Bloqueados : -1)} particulas {antes} -> {durante}");
            var objetivoImp = escudo != null ? escudo.position : esc.transform.position + Vector3.up;
            yield return Foto("escudero_impacto", objetivoImp + esc.transform.forward * 1.5f + esc.transform.right * 0.9f + Vector3.up * 0.2f, objetivoImp + esc.transform.forward * 0.15f, 45f);
            yield return Foto("escudero_impacto_lejos", esc.transform.position + esc.transform.forward * 4.2f + esc.transform.right * 2.0f + Vector3.up * 1.6f, objetivoImp, 50f);

            // ---- giro: el escudero mira para el lado contrario y el motor lo gira hacia un punto a su espalda (como cuando lo flanquean);
            // se mide a nivel del motor (sin IA): angulo que le falta cada 1,5 s de juego
            if (esc.Brain != null) esc.Brain.Anulado = true;
            var haciaYo = Vector3.ProjectOnPlane(yo.transform.position - esc.transform.position, Vector3.up).normalized;
            esc.transform.rotation = Quaternion.LookRotation(-haciaYo);
            var puntoFlanco = esc.transform.position + haciaYo * 8f;
            float t0 = Time.realtimeSinceStartup, tUlt = t0; int n = 0; var sbG = new StringBuilder(); float angFinal = 180f, tLlegada = -1f;
            while (Time.realtimeSinceStartup - t0 < 10.6f && n < 8)
            {
                float t = Time.realtimeSinceStartup - t0;
                esc.Motor.LookTowards(puntoFlanco, Time.realtimeSinceStartup - tUlt); tUlt = Time.realtimeSinceStartup;
                angFinal = Vector3.Angle(esc.transform.forward, haciaYo);
                if (tLlegada < 0f && angFinal < 5f) tLlegada = t;
                if (t >= n * 1.5f)
                {
                    sbG.Append($"t={F(t)} falta={angFinal.ToString("0", Ci)} ");
                    yield return Foto("escudero_giro_" + n, esc.transform.position + Vector3.up * 9f, esc.transform.position + haciaYo * 1.2f, 50f, false, haciaYo);
                    tUlt = Time.realtimeSinceStartup; n++;
                }
                yield return null;
            }
            Dato("giro de 180 grados del escudero: " + sbG + (tLlegada >= 0f ? "llego a 5 grados en " + F(tLlegada) + " s" : "no llego en 10 s (quedan " + angFinal.ToString("0", Ci) + ")"));

            // ---- muerte: el escudo debe soltarse y caer
            if (esc.Brain != null) esc.Brain.Anulado = true;
            var posMuerte = esc.transform.position;
            esc.Health.TakeDamage(100000, -1, false);
            yield return Espera(2.8f);
            var escudoMuerto = FindEscudo(esc);
            Dato($"escudero muerto: vivo={esc.Health.IsAlive} escudo={(escudoMuerto != null ? "existe" : "no")} hijo_del_soldado={(escudoMuerto != null && escudoMuerto.IsChildOf(esc.transform))} y_escudo={(escudoMuerto != null ? F(escudoMuerto.position.y) : "-")} y_soldado={F(esc.transform.position.y)} desplazamiento_del_escudo={(escudoMuerto != null ? F(Vector3.Distance(escudoMuerto.position, posMuerte)) : "-")}");
            var ctr = posMuerte + Vector3.up * 0.3f; var ex = esc.transform.right; var ef = esc.transform.forward;
            yield return Foto("escudero_muerto", Despejada(ctr, ctr + ef * 3.6f + ex * 1.6f + Vector3.up * 1.8f, ctr - ef * 3.6f + ex * 1.6f + Vector3.up * 1.8f, ctr + ex * 3.6f + Vector3.up * 1.8f, ctr - ex * 3.6f + Vector3.up * 1.8f, ctr + Vector3.up * 5f + ex * 1.5f), ctr, 52f);
        }

        static Transform FindEscudo(Soldier esc)
        {
            var h = esc.transform.Find("Escudo"); if (h != null) return h;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.name == "Escudo" && t.GetComponentInParent<Soldier>() == null) return t;
            return null;
        }

        static int ContarParticulas()
        {
            int n = 0; foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude)) if (ps != null && ps.isPlaying) n += ps.particleCount;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.name.StartsWith("ImpactCube", StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }

        // ---------------------------------------------------------------- 4. escalera: cuerpo que muere sobre los escalones
        IEnumerator Escalera()
        {
            Saltar(5); yield return Espera(3f);
            yield return ConMolde();
            var yo = Yo(); if (yo == null) { Dato("SIN JUGADOR"); yield break; }
            AlejarAliados(yo.transform.position);
            RampaCaminable rampa = null; float mejor = 1e9f;
            foreach (var r in RampaCaminable.Todas)
            {
                if (r == null || !r.isActiveAndEnabled) continue;
                float dh = Mathf.Abs(r.alturaDelFinal - r.alturaInicial); if (dh < 2f || dh > 7f) continue;
                float dist = Vector3.Distance(r.abajo, yo.transform.position);
                if (dist < mejor) { mejor = dist; rampa = r; }
            }
            if (rampa == null) { Dato("SIN RAMPA"); yield break; }
            var eje = rampa.arriba - rampa.abajo; eje.y = 0f; var ejeN = eje.normalized; var lado = Vector3.Cross(Vector3.up, ejeN);
            Dato($"rampa abajo={V(rampa.abajo)} arriba={V(rampa.arriba)} alt {F(rampa.alturaInicial)}->{F(rampa.alturaDelFinal)} ancho {F(rampa.anchoMedio * 2f)}");
            for (int caso = 0; caso < 2; caso++)
            {
                float k = caso == 0 ? 0.45f : 0.6f;
                var p = rampa.abajo + eje * k; rampa.Superficie(p, out float ys); p.y = ys + SoldierMotor.PivoteSobrePiso;
                var s = Clon(p, ejeN); if (s == null) { Dato("SIN MOLDE"); yield break; }
                s.transform.position = p; Physics.SyncTransforms();
                yield return Espera(0.6f);
                if (s.Brain != null) s.Brain.Anulado = true;
                s.transform.position = p;
                yield return null;
                var origen = s.transform.position - ejeN * 3f;
                if (caso == 0) s.Health.TakeDamage(100000, yo.Id, false);
                else { s.Health.TakeDamage(100000, -1, false); RagdollDeExplosion.Lanzar(s, origen, 9f); }
                string tag = caso == 0 ? "bala" : "explosion";
                for (int i = 0; i < 3; i++)
                {
                    yield return Espera(i == 0 ? 0.5f : i == 1 ? 1.0f : 3.0f);
                    float dy = 0f; bool sobre = rampa.Superficie(s.transform.position, out float ysup); if (sobre) dy = s.transform.position.y - SoldierMotor.PivoteSobrePiso - ysup;
                    var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    float minY = smr != null ? smr.bounds.min.y : s.transform.position.y;
                    Dato($"escalera {tag} +{i}: pos {V(s.transform.position)} sobre_rampa={sobre} y_pivote-superficie-0.8={F(dy)} malla_min_y={F(minY)} superficie_y={(sobre ? F(ysup) : "-")}");
                    var c = (sobre ? new Vector3(s.transform.position.x, ysup + 0.3f, s.transform.position.z) : s.transform.position) + Vector3.up * 0.2f;
                    var cam = Despejada(c, c + lado * 3.4f + Vector3.up * 1.4f, c - lado * 3.4f + Vector3.up * 1.4f, c + lado * 2.4f + Vector3.up * 2.4f, c - lado * 2.4f + Vector3.up * 2.4f, c - ejeN * 3.5f + Vector3.up * 2.2f + lado * 1.0f, c + ejeN * 3.5f + Vector3.up * 2.2f + lado * 1.0f, c + Vector3.up * 4.5f - ejeN * 1.5f);
                    yield return Foto($"escalera_{tag}_{i}", cam, c, 52f);
                }
            }
        }

        // ---------------------------------------------------------------- 5. perros de la ciudad
        IEnumerator Ciudad()
        {
            Saltar(5); yield return Espera(2f);
            var d = OperacionDirector.Instancia;
            // el reloj de la resistencia (y con el los refuerzos) solo corre con el jugador dentro de la ciudad: se lo lleva a la plaza
            if (d.plaza != null)
            {
                var yoC = Yo(); if (yoC != null) { yoC.transform.position = EnMalla(d.plaza.position + new Vector3(0f, 0.5f, -6f)); Physics.SyncTransforms(); }
                int kk = 0; foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Player && a != yoC && a.gameObject.activeInHierarchy) { a.transform.position = EnMalla(d.plaza.position + new Vector3(-3f + 2f * (kk++ % 4), 0.5f, -9f)); ApoyoEnElPiso.Apoyar(a.transform); }
            }
            Time.timeScale = 4f; yield return Espera(30f); Time.timeScale = 1f;
            int perros = 0, vivos = 0, escuderos = 0, pesados = 0, refuerzos = 0;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.name.StartsWith("Refuerzo_Ciudad_")) refuerzos++;
            foreach (var p in FindObjectsByType<PerroEnemigo>(FindObjectsInactive.Include)) { perros++; if (p.GetComponent<Soldier>() != null && p.GetComponent<Soldier>().Health.IsAlive) vivos++; }
            foreach (var e in FindObjectsByType<EnemigoEscudero>(FindObjectsInactive.Include)) escuderos++;
            foreach (var e in FindObjectsByType<EnemigoPesado>(FindObjectsInactive.Include)) pesados++;
            Dato($"ciudad tras 30 s a x4 (120 s de juego): refuerzos creados={d.RefuerzosCreados} (en escena {refuerzos}) perros={perros} (vivos {vivos}) escuderos={escuderos} pesados={pesados} reloj={F(d.Reloj)}");
            var yo = Yo(); var c = yo != null ? yo.transform.position : Vector3.zero;
            // el perro vivo mas cercano, de cerca
            PerroEnemigo cerca = null; float md = 1e9f;
            foreach (var p in FindObjectsByType<PerroEnemigo>(FindObjectsInactive.Exclude)) { var s = p.GetComponent<Soldier>(); if (s == null || !s.Health.IsAlive) continue; float dd = Vector3.Distance(p.transform.position, c); if (dd < md) { md = dd; cerca = p; } }
            yield return Foto("ciudad_vista", c + new Vector3(0f, 60f, -18f), c, 62f, false);
            if (cerca != null) yield return Foto("ciudad_perro", cerca.transform.position + Vector3.up * 1.4f + cerca.transform.right * 2.6f, cerca.transform.position + Vector3.up * 0.4f, 50f);
        }
    }
}
