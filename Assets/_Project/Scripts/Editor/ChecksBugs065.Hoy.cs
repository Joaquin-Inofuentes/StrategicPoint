using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Tanda del 2026-11-06 (bugs #345-#353 + "distinguir asalto y francotirador"). Cada check deja capturas en Assets/Validacion~/ (jpg)
    // sacadas con una camara propia hacia un RenderTexture: no dependen del Game View ni de la UI.
    public static partial class ChecksBugs065
    {
        // Foto con una camara suelta. El archivo queda en Assets/Validacion~/<archivo>.
        static IEnumerable FotoCamara(string archivo, Vector3 pos, Vector3 mira, float fov = 50f, int w = 960, int h = 540)
        {
            var go = new GameObject("CamFoto_Prueba");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos; cam.transform.LookAt(mira);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 900f;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            yield return null;
            cam.Render();
            var previa = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previa;
            File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToJPG(90));
            cam.targetTexture = null;
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt); Object.Destroy(go);
        }

        // ---------------------------------------------------------------- 601a asalto vs francotirador: se distinguen de un vistazo
        static IEnumerator Bug601a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var asalto = Escuadrista(RoleType.Assault); var franco = Escuadrista(RoleType.Flanker) ?? Escuadrista(RoleType.Sniper); var medico = Escuadrista(RoleType.Medic);
            if (asalto == null || franco == null) { Fin("FALLO faltan soldados (asalto/francotirador)"); yield break; }
            var baseP = asalto.transform.position;
            // Los tres en fila, de frente a una camara a 4,5 m (distancia de juego en tercera persona), sobre el piso.
            var centro = baseP + new Vector3(0f, 0f, 6f);
            Soldier[] fila = { asalto, medico, franco };
            for (int i = 0; i < fila.Length; i++)
            {
                if (fila[i] == null) continue;
                fila[i].transform.position = centro + new Vector3((i - 1) * 1.6f, 0f, 0f);
                fila[i].transform.rotation = Quaternion.LookRotation(Vector3.back);
                ApoyoEnElPiso.Apoyar(fila[i].transform);
                if (fila[i].Brain != null) fila[i].Brain.CancelOrder();
            }
            foreach (var x in Esperar(0.6f)) yield return x;
            var look1 = asalto.GetComponent<SoldierLook>(); var look2 = franco.GetComponent<SoldierLook>();
            var r1 = asalto.GetComponentInChildren<SkinnedMeshRenderer>(true); var r2 = franco.GetComponentInChildren<SkinnedMeshRenderer>(true);
            W8Ok(ref ok, look1 != null && look1.Aplicado && look2 != null && look2.Aplicado, sb, "los dos tienen el look de su clase aplicado");
            W8Ok(ref ok, r1 != null && r2 != null && r1.sharedMesh != r2.sharedMesh, sb, "mallas distintas");
            W8Ok(ref ok, r1 != null && r2 != null && r1.sharedMaterial != r2.sharedMaterial, sb, "materiales distintos");
            // Color promedio de lo que se ve de cada uno (muestreo de la textura propia de cada material)
            Color ca = ColorMedio(r1), cf = ColorMedio(r2);
            float dist = Mathf.Abs(ca.r - cf.r) + Mathf.Abs(ca.g - cf.g) + Mathf.Abs(ca.b - cf.b);
            W8Ok(ref ok, dist > 0.40f, sb, $"el color medio difiere (asalto {ca.r:0.00},{ca.g:0.00},{ca.b:0.00} / franco {cf.r:0.00},{cf.g:0.00},{cf.b:0.00}; dif {dist:0.00} > 0.40; antes era 0.20)");
            var cam = centro + new Vector3(0f, 1.7f, 4.5f);
            foreach (var x in FotoCamara("v4_601_clases_despues.jpg", cam, centro + new Vector3(0f, 1.0f, 0f), 42f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + "(captura v4_601_clases_despues.jpg)");
        }

        // Foto desde la pose exacta de la camara que marco el bug en el Sheet (columnas Camara_X/Y/Z, Camara_RotX/Y/Z, Camara_FOV).
        static IEnumerable FotoDePose(string archivo, float x, float y, float z, float rx, float ry, float rz, float fov = 60f, int w = 960, int h = 540)
        {
            var go = new GameObject("CamPose_Prueba");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 900f;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            yield return null;
            cam.Render();
            var previa = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previa;
            File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToJPG(90));
            cam.targetTexture = null; Object.Destroy(tex); rt.Release(); Object.Destroy(rt); Object.Destroy(go);
        }

        // ---------------------------------------------------------------- 601f foto de la pose del bug #351 (colision invisible)
        static IEnumerator Bug601f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            foreach (var x in FotoDePose("v4_351_despues.jpg", 295.72f, 3.63f, 274.41f, 23.4f, 259.1f, 0f, 60f)) yield return x;
            foreach (var x in FotoCamara("v4_351_despues_b.jpg", new Vector3(299f, 3.2f, 268f), new Vector3(292.1f, 0.6f, 274.1f), 55f)) yield return x;
            foreach (var x in FotoCamara("v4_351_despues_c.jpg", new Vector3(285f, 3.2f, 268f), new Vector3(292.1f, 0.6f, 274.1f), 55f)) yield return x;
            Fin("OK fotos del 351 (v4_351_despues.jpg)");
        }

        // ---------------------------------------------------------------- 601r azotea: los enemigos del techo llegan hasta el jugador (bug #352)
        static IEnumerator Bug601r()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            ModoDios.Poner(true);
            var d = OperacionDirector.Instancia;
            d.SaltarALaAzotea();
            foreach (var x in Esperar(1.5f)) yield return x;
            var campo = typeof(OperacionDirector).GetField("tAzotea", NoPublico);
            campo.SetValue(d, OperacionDirector.CuandoCuelgan[0] - 0.2f);
            float limite = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < limite && (d.HelisDeLaAzotea.Count == 0 || d.HelisDeLaAzotea[0].SoldadosBajados < d.HelisDeLaAzotea[0].SoldadosPedidos)) { foreach (var hh in d.HelisDeLaAzotea) if (hh != null && hh.Vehiculo != null) hh.Vehiculo.Health.Invulnerable = true; yield return null; }
            foreach (var x in Esperar(2f)) yield return x;
            var yo = Poseido(); var objetivo = yo != null ? yo.transform.position : Vector3.zero;
            int enTecho = 0, llegan = 0; var detalle = new StringBuilder();
            var lista = new System.Collections.Generic.List<Soldier>();
            foreach (var hh in d.HelisDeLaAzotea) if (hh != null) foreach (var s0 in hh.SoldadosDelDespliegue) if (s0 != null) lista.Add(s0);
            detalle.Append($" (desplegados={lista.Count}, yo={objetivo:0.0})");
            foreach (var s in lista)
            {
                if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive) continue;
                if (s.transform.position.y < 18f) { detalle.Append($" [{s.name} abajo y={s.transform.position.y:0.0}]"); continue; }
                enTecho++;
                var camino = new UnityEngine.AI.NavMeshPath();
                bool hay = UnityEngine.AI.NavMesh.CalculatePath(s.transform.position, objetivo, UnityEngine.AI.NavMesh.AllAreas, camino);
                bool completo = hay && camino.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
                if (completo) llegan++;
                else
                {
                    var dir = objetivo - (s.transform.position + Vector3.up); string golpe = "-";
                    if (Physics.Raycast(s.transform.position + Vector3.up, dir.normalized, out var h, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)) golpe = h.collider.transform.parent != null ? h.collider.transform.parent.name + "/" + h.collider.name : h.collider.name;
                    detalle.Append($" [{s.name}@{s.transform.position:0} path={camino.status} golpe={golpe}]");
                }
            }
            W8Ok(ref ok, enTecho > 0, sb, $"hay enemigos en el techo ({enTecho})");
            W8Ok(ref ok, llegan == enTecho, sb, $"todos tienen camino completo hasta el jugador ({llegan}/{enTecho}){detalle}");
            // Con el modo dios se puede mover al jugador: a 14 m (otro punto del techo) tambien tienen que llegar
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 601s que es la tira roja del bug #352 (rayos desde la camara del reporte)
        static IEnumerator Bug601s()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia;
            d.SaltarALaAzotea();
            foreach (var x in Esperar(2.0f)) yield return x;
            var go = new GameObject("CamPose_Prueba"); var cam = go.AddComponent<Camera>(); cam.enabled = false;
            cam.transform.SetPositionAndRotation(new Vector3(318.53f, 24.46f, 271.07f), Quaternion.Euler(41.1f, 63.2f, 0f)); cam.fieldOfView = 60f; cam.aspect = 16f / 9f;
            var vistos = new System.Collections.Generic.HashSet<string>();
            for (float px = 0.50f; px <= 0.70f; px += 0.05f)
                for (float py = 0.0f; py <= 0.65f; py += 0.1f)
                {
                    var rayo = cam.ViewportPointToRay(new Vector3(px, 1f - py, 0f));
                    if (Physics.Raycast(rayo, out var h, 60f, ~0, QueryTriggerInteraction.Collide))
                    {
                        string ruta = h.collider.name; var t = h.collider.transform.parent; int n = 0; while (t != null && n++ < 4) { ruta = t.name + "/" + ruta; t = t.parent; }
                        var rd = h.collider.GetComponentInChildren<Renderer>(true);
                        string mat = rd != null && rd.sharedMaterial != null ? rd.sharedMaterial.name : "-";
                        string col = rd != null && rd.sharedMaterial != null && rd.sharedMaterial.HasProperty("_BaseColor") ? rd.sharedMaterial.GetColor("_BaseColor").ToString() : "";
                        vistos.Add($"{ruta} [{h.collider.GetType().Name} trig={h.collider.isTrigger} mat={mat} {col}] punto={h.point:0.0}");
                    }
                    var rend = Physics.Raycast(rayo, out var h2, 60f, ~0, QueryTriggerInteraction.Ignore);
                }
            foreach (var v in vistos) sb.AppendLine(v);
            // todos los renderers rojos grandes y finos cerca de la camara (la tira)
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; if ((b.center - new Vector3(322f, 21f, 274f)).sqrMagnitude > 20f * 20f) continue;
                var m = r.sharedMaterial; if (m == null || !m.HasProperty("_BaseColor")) continue;
                var c = m.GetColor("_BaseColor"); if (!(c.r > 0.35f && c.r > c.g * 2.2f && c.r > c.b * 2.2f)) continue;
                string ruta = r.name; var t = r.transform.parent; int n = 0; while (t != null && n++ < 3) { ruta = t.name + "/" + ruta; t = t.parent; }
                sb.AppendLine($"ROJO {ruta} centro={b.center:0.0} tam={b.size:0.0} col={r.GetComponentInParent<Collider>() != null}");
            }
            System.IO.File.WriteAllText("Temp/inspeccion_601s.txt", sb.ToString());
            Object.Destroy(go);
            Fin("OK volcado en Temp/inspeccion_601s.txt");
        }

        // ---------------------------------------------------------------- 601t #345 la terminal estalla y se lleva la maquina y el muro; #346 sin chevrones; #350 sin flecha roja
        static IEnumerator Bug601t()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            var arte = Buscar("ArteTerminal");
            W8Ok(ref ok, arte != null && arte.GetComponentsInChildren<Renderer>(true).Length >= 8, sb, "antes: la maquina esta en pie");
            bool muroEnPieAntes = !MuroSalidaCaido(d);
            d.ExplotarLaTerminalPorPrueba();   // el atajo de prueba deja el muro ya caido: se dispara la voladura directamente
            foreach (var x in Esperar(0.15f)) yield return x;
            foreach (var x in FotoDePose("v4_345_despues_a.jpg", -2.04f, 3.26f, -54.48f, 13.5f, 6f, 0f, 60f)) yield return x;
            foreach (var x in Esperar(6.0f)) yield return x;
            foreach (var x in FotoDePose("v4_345_despues.jpg", -2.04f, 3.26f, -54.48f, 13.5f, 6f, 0f, 60f)) yield return x;
            bool maquinaViva = false; if (arte != null) foreach (var r in arte.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy) maquinaViva = true;
            string nombreArte = "-";
            W8Ok(ref ok, d.TerminalDestruida && !maquinaViva, sb, $"la maquina ya no se dibuja (destruida={d.TerminalDestruida} viva={maquinaViva} arte={nombreArte})");
            W8Ok(ref ok, !muroEnPieAntes || MuroSalidaCaido(d), sb, $"el muro de la salida cae con el estallido (estaba en pie={muroEnPieAntes} timeScale={Time.timeScale:0.0})");
            // #355: el monitor emisivo del grupo "Computadora" ya no queda flotando
            bool monitorVivo = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.enabled && r.gameObject.activeInHierarchy && r.transform.parent != null && r.transform.parent.name == "Computadora") monitorVivo = true;
            W8Ok(ref ok, !monitorVivo, sb, "no queda ningun monitor flotando en el grupo Computadora (bug #355)");
            // #346: ni chevrones ambientales ni flechas pintadas en el piso
            int pintadas = 0; foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.gameObject.scene.IsValid() && (t.name == "FlechaPiso" || t.name == "Ruta_Flecha" || t.name == "Flecha_Ala")) pintadas++;
            W8Ok(ref ok, pintadas == 0, sb, $"no quedan flechas de ruta pintadas ({pintadas})");
            // #350: la flecha roja de oleada ya no se dibuja en el mundo (el minimapa conserva la suya)
            var f = SP.Presentation.FlechaDeOleada.Mostrar(new Vector3(325f, 0f, 298f), Vector3.left, 6f);
            foreach (var x in Esperar(0.3f)) yield return x;
            var rm = f != null ? f.transform.Find("Mundo")?.GetComponent<MeshRenderer>() : null;
            W8Ok(ref ok, f != null && f.Activa && rm != null && !rm.enabled, sb, "la flecha roja de oleada no se dibuja en la calle");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 601b/601q mediciones: cuelgue al entrar al tanque (#358) y fps en la mansion (#357/#361/#365)
        static IEnumerable Muestrear(float segundos, System.Action<float> porCuadro)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < segundos) { porCuadro(Time.unscaledDeltaTime); yield return null; }
        }

        // Vuelca a archivo las 25 muestras con mas tiempo propio del cuadro (hilo principal) del Profiler del Editor.
        static void VolcarCuadroDelProfiler(int frame, string archivo)
        {
            var sb = new StringBuilder();
            for (int fr = frame - 2; fr <= frame + 1; fr++)
            {
                try
                {
                    using (var v = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(fr, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime, false))
                    {
                        if (v == null || !v.valid) continue;
                        var filas = new System.Collections.Generic.List<(string, float, float)>();
                        var pila = new System.Collections.Generic.Stack<int>(); pila.Push(v.GetRootItemID());
                        var hijos = new System.Collections.Generic.List<int>();
                        while (pila.Count > 0)
                        {
                            int id = pila.Pop(); hijos.Clear(); v.GetItemChildren(id, hijos);
                            foreach (var h in hijos) { filas.Add((v.GetItemName(h), v.GetItemColumnDataAsFloat(h, UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime), v.GetItemColumnDataAsFloat(h, UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime))); pila.Push(h); }
                        }
                        filas.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                        sb.AppendLine($"== cuadro {fr} (ms de {v.frameTimeMs:0.0})");
                        for (int i = 0; i < Mathf.Min(25, filas.Count); i++) sb.AppendLine($"{filas[i].Item2,9:0.00} propio | {filas[i].Item3,9:0.00} total | {filas[i].Item1}");
                    }
                }
                catch (System.Exception e) { sb.AppendLine("error en cuadro " + fr + ": " + e.Message); }
            }
            File.WriteAllText(archivo, sb.ToString());
        }

        static IEnumerator Bug601b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionDirector.HalconEnLaCarrera = false;
            var d = OperacionDirector.Instancia;
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1.5f)) yield return x;
            var tanque = d.tanque;
            var p0 = tanque != null ? tanque.transform.position : Vector3.zero;
            float suma = 0f, max = 0f; int n = 0, golpes = 0, cuadroMax = 0; var sw = System.Diagnostics.Stopwatch.StartNew();
            UnityEditorInternal.ProfilerDriver.ClearAllFrames(); UnityEditorInternal.ProfilerDriver.enabled = true;
            int frameDelProfiler = -1;
            d.Embarcar();
            sw.Stop(); sb.Append($"(Embarcar() tardo {sw.ElapsedMilliseconds} ms sincronico) ");
            foreach (var x in Muestrear(14f, dt => { suma += dt; n++; if (dt > max) { max = dt; cuadroMax = n; if (dt > 0.5f) frameDelProfiler = UnityEditorInternal.ProfilerDriver.lastFrameIndex; } if (dt > 0.1f) golpes++; })) yield return x;
            UnityEditorInternal.ProfilerDriver.enabled = false;
            sb.Append($"(cuadro mas largo fue el n.{cuadroMax}) ");
            if (frameDelProfiler >= 0) VolcarCuadroDelProfiler(frameDelProfiler, "Temp/hitch_358.txt");
            float avanzo = tanque != null ? Vector3.Distance(p0, tanque.transform.position) : 0f;
            W8Ok(ref ok, tanque != null && avanzo > 15f, sb, $"el tanque avanza tras embarcar ({avanzo:0.0} m en 14 s)");
            W8Ok(ref ok, max < 0.5f, sb, $"ningun cuadro congelado (cuadro mas largo {max * 1000f:0} ms, {golpes} cuadros > 100 ms, fps medio {n / Mathf.Max(0.01f, suma):0})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug601q()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            OperacionPrueba.Arrancar(5, 1);
            ModoDios.Poner(false); Time.timeScale = 1f;
            foreach (var x in Esperar(3f)) yield return x;
            float suma = 0f, max = 0f; int n = 0;
            foreach (var x in Muestrear(8f, dt => { suma += dt; n++; if (dt > max) max = dt; })) yield return x;
            int luces = 0, sombras = 0, renderers = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled && l.gameObject.activeInHierarchy) { luces++; if (l.shadows != LightShadows.None) sombras++; }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.enabled && r.gameObject.activeInHierarchy) renderers++;
            Fin($"OK fps medio {n / Mathf.Max(0.01f, suma):0.0} | cuadro mas largo {max * 1000f:0} ms | luces activas {luces} (con sombra {sombras}) | renderers {renderers}");
        }

        // ---------------------------------------------------------------- 601g bugs #354/#355/#362/#364/#366: velocidad del modo dios, monitor flotante, fuente, zoom del minimapa, rejas invisibles
        static IEnumerator Bug601g()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var yo = Poseido(); var rig = SP.CameraSystem.CameraRig.Instance;
            // #354 velocidad del modo dios
            ModoDios.VelocidadActiva = true; ModoDios.Poner(true); rig.SetMode(SP.CameraSystem.ControlMode.Fps);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, Mathf.Abs(Time.timeScale - 2f) < 0.01f, sb, $"modo dios en primera persona: la escena corre a x{Time.timeScale:0.0} (pide x2)");
            yo.Motor.SetRunning(true); float conDios = yo.Motor.MoveSpeed; ModoDios.Poner(false); float sinDios = yo.Motor.MoveSpeed; ModoDios.Poner(true);
            W8Ok(ref ok, Mathf.Abs(conDios / Mathf.Max(0.01f, sinDios) - 2f) < 0.05f, sb, $"el soldado que manejas corre x2 con modo dios ({sinDios:0.0} -> {conDios:0.0} m/s)");
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, yo.transform.position);
            foreach (var x in Esperar(0.4f)) yield return x;
            W8Ok(ref ok, Mathf.Abs(Time.timeScale - 1f) < 0.01f, sb, $"en RTS todo va normal (x{Time.timeScale:0.0})");
            rig.SetMode(SP.CameraSystem.ControlMode.Fps); ModoDios.Poner(false); ModoDios.VelocidadActiva = false; Time.timeScale = 1f; yo.Motor.SetRunning(false);
            // #362 fuente sin solapes
            Renderer copa = null, cuenco = null, agua = null;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { if (r.name == "Fuente_Copa") copa = r; else if (r.name == "Fuente_Cuenco") cuenco = r; else if (r.name == "Fuente_Agua") agua = r; }
            W8Ok(ref ok, copa != null && !copa.enabled, sb, "la copa duplicada de la fuente esta apagada");
            W8Ok(ref ok, agua != null && cuenco != null && agua.bounds.center.y >= cuenco.bounds.max.y, sb, $"el agua de la fuente esta sobre el borde del cuenco (agua y={(agua != null ? agua.bounds.center.y : 0f):0.00}, borde y={(cuenco != null ? cuenco.bounds.max.y : 0f):0.00})");
            // #366 las compuertas no se dibujan
            int conRender = 0, compuertas = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Mansion_Compuerta_")) { compuertas++; conRender += t.GetComponentsInChildren<Renderer>(true).Length; }
            W8Ok(ref ok, compuertas > 0 && conRender == 0, sb, $"{compuertas} compuertas de torre, {conRender} con dibujo (solo collider invisible)");
            // #364 zoom del minimapa dentro de la mansion
            d.SaltarALaAzotea();
            foreach (var x in Esperar(2.5f)) yield return x;
            var cam = MinimapaCam();
            W8Ok(ref ok, cam != null && cam.orthographicSize < 60f, sb, $"dentro de la mansion el minimapa hace zoom (ortho {(cam != null ? cam.orthographicSize : 0f):0})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static Camera MinimapaCam() { var m = SP.UI.MinimapFollow.Activo; return m != null ? m.MinimapCamera : null; }

        // ---------------------------------------------------------------- 601x diagnostico #355 (cosas que flotan en la sala de la terminal) y #362 (piezas de la fuente)
        static string RutaDeObjeto601(Transform t) { string r = t.name; var p = t.parent; int n = 0; while (p != null && n++ < 4) { r = p.name + "/" + r; p = p.parent; } return r; }
        static IEnumerator Bug601x()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(1.5f)) yield return x;
            OperacionDirector.Instancia.ExplotarLaTerminalPorPrueba();
            foreach (var x in Esperar(4f)) yield return x;
            sb.AppendLine("== sala de la terminal (x -14..14, z -64..-40), activos, y>0.25");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; if (b.center.x < -14f || b.center.x > 14f || b.center.z < -64f || b.center.z > -40f || b.min.y < 0.25f || b.size.y > 8f) continue;
                var m = r.sharedMaterial; string mat = m != null ? m.name : "-";
                bool emi = m != null && m.IsKeywordEnabled("_EMISSION");
                sb.AppendLine($"{RutaDeObjeto601(r.transform)} centro={b.center:0.0} tam={b.size:0.0} mat={mat} emi={emi} baja={b.min.y:0.00}");
            }
            File.WriteAllText("Temp/diag_601x_terminal.txt", sb.ToString());
            sb.Clear();
            sb.AppendLine("== piezas de fuente");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null || !r.name.StartsWith("Fuente")) continue;
                var b = r.bounds; var m = r.sharedMaterial;
                sb.AppendLine($"{RutaDeObjeto601(r.transform)} act={r.gameObject.activeInHierarchy} centro={b.center:0.00} tam={b.size:0.00} mat={(m != null ? m.name : "-")} q={(m != null ? m.renderQueue : 0)}");
            }
            File.WriteAllText("Temp/diag_601x_fuente.txt", sb.ToString());
            Fin("OK volcado en Temp/diag_601x_*.txt");
        }

        // ---------------------------------------------------------------- 601p fotos y volcado de la Plaza Civica (mejora de la plaza, 2026-11-06)
        static IEnumerator Bug601p()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var c = new Vector3(322f, 0f, 215.5f);
            sb.AppendLine("== renderers a <= 30 m del centro de la plaza (activos), nombre | centro | tam | material");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; var d = b.center - c; d.y = 0f; if (d.magnitude > 30f || b.size.y > 40f) continue;
                var m = r.sharedMaterial;
                sb.AppendLine($"{RutaDeObjeto601(r.transform)} centro={b.center:0.0} tam={b.size:0.0} mat={(m != null ? m.name : "-")}");
            }
            sb.AppendLine("== luces a <= 40 m");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                var d = l.transform.position - c; d.y = 0f; if (d.magnitude > 40f) continue;
                sb.AppendLine($"{RutaDeObjeto601(l.transform)} tipo={l.type} int={l.intensity:0.00} rango={l.range:0.0} sombras={l.shadows} pos={l.transform.position:0.0}");
            }
            File.WriteAllText("Temp/diag_601p_plaza.txt", sb.ToString());
            foreach (var x in FotoCamara("v5_plaza_1_norte.jpg", c + new Vector3(0f, 9f, -22f), c + Vector3.up * 1.5f, 60f)) yield return x;
            foreach (var x in FotoCamara("v5_plaza_2_este.jpg", c + new Vector3(22f, 9f, 0f), c + Vector3.up * 1.5f, 60f)) yield return x;
            foreach (var x in FotoCamara("v5_plaza_3_sur.jpg", c + new Vector3(0f, 9f, 22f), c + Vector3.up * 1.5f, 60f)) yield return x;
            foreach (var x in FotoCamara("v5_plaza_4_peaton.jpg", c + new Vector3(-9f, 1.7f, -9f), c + Vector3.up * 1.5f, 70f)) yield return x;
            foreach (var x in FotoCamara("v5_plaza_5_fuente.jpg", c + new Vector3(4.5f, 2.6f, -4.5f), c + Vector3.up * 1.4f, 60f)) yield return x;
            foreach (var x in FotoCamara("v5_plaza_6_cenital.jpg", c + new Vector3(0f, 45f, -1f), c, 60f)) yield return x;
            Fin("OK fotos v5_plaza_* y Temp/diag_601p_plaza.txt");
        }

        // ---------------------------------------------------------------- 601c auditoria de coherencia de la ciudad (repasada 1: duplicados exactos y flotantes)
        static string GrupoDe601(Transform t) { var partes = RutaDeObjeto601(t).Split('/'); return partes.Length >= 3 ? partes[0] + "/" + partes[1] + "/" + partes[2] : RutaDeObjeto601(t); }
        static IEnumerator Bug601c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var todos = new System.Collections.Generic.List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && r.bounds.size.y < 60f) todos.Add(r);
            sb.AppendLine($"renderers activos: {todos.Count}");
            // 1) duplicados exactos: mismo mesh + mismo centro (<2 cm) + mismo tamano
            var celdas = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Renderer>>();
            foreach (var r in todos)
            {
                var mf = r.GetComponent<MeshFilter>(); string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : r.GetType().Name;
                var b = r.bounds; string k = $"{mesh}|{Mathf.Round(b.center.x * 50)}|{Mathf.Round(b.center.y * 50)}|{Mathf.Round(b.center.z * 50)}|{Mathf.Round(b.size.x * 20)}|{Mathf.Round(b.size.y * 20)}|{Mathf.Round(b.size.z * 20)}";
                if (!celdas.TryGetValue(k, out var l)) celdas[k] = l = new System.Collections.Generic.List<Renderer>();
                l.Add(r);
            }
            int dups = 0; var porGrupo = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var kv in celdas) if (kv.Value.Count > 1)
            {
                dups += kv.Value.Count - 1; string g = GrupoDe601(kv.Value[0].transform);
                porGrupo[g] = porGrupo.TryGetValue(g, out var n) ? n + 1 : 1;
                if (porGrupo[g] <= 3) sb.AppendLine($"  DUP x{kv.Value.Count}: {RutaDeObjeto601(kv.Value[0].transform)} (+ {RutaDeObjeto601(kv.Value[1].transform)}) en {kv.Value[0].bounds.center:0.0}");
            }
            sb.AppendLine($"== duplicados exactos: {dups} piezas sobrantes en {porGrupo.Count} grupos");
            foreach (var kv in porGrupo) sb.AppendLine($"   {kv.Key}: {kv.Value} pares");
            // 2) flotantes: base a > 0.4 m del piso y sin nada solido debajo (a 1.5 m) y no es luz/cartel/colgante
            int flot = 0; var flotGrupo = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var r in todos)
            {
                var b = r.bounds; if (b.min.y < 0.4f || b.min.y > 6f || b.size.y > 4f) continue;
                string n = r.name; if (n.Contains("Luz") || n.Contains("Colgante") || n.Contains("Bombilla") || n.Contains("Guirnalda") || n.Contains("Lampara") || n.Contains("Cartel") || n.Contains("Bandera") || n.Contains("Ventana") || n.Contains("Balcon") || n.Contains("Cuadro") || n.Contains("Techo") || n.Contains("Heli") || n.Contains("Aro")) continue;
                var desde = new Vector3(b.center.x, b.min.y - 0.05f, b.center.z);
                bool apoyo = false;
                foreach (var h in Physics.RaycastAll(desde, Vector3.down, b.min.y + 0.5f, ~0, QueryTriggerInteraction.Ignore)) { if (!h.collider.transform.IsChildOf(r.transform) && h.distance < 1.5f) { apoyo = true; break; } }
                if (apoyo) continue;
                if (Physics.CheckBox(b.center, b.extents + Vector3.one * 0.15f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) && Physics.OverlapBox(b.center, b.extents + Vector3.one * 0.15f).Length > 1) continue;
                flot++; string g = GrupoDe601(r.transform); flotGrupo[g] = flotGrupo.TryGetValue(g, out var m) ? m + 1 : 1;
                if (flotGrupo[g] <= 2) sb.AppendLine($"  FLOTA: {RutaDeObjeto601(r.transform)} base={b.min.y:0.00} centro={b.center:0.0} tam={b.size:0.0}");
            }
            sb.AppendLine($"== posibles flotantes: {flot} en {flotGrupo.Count} grupos");
            foreach (var kv in flotGrupo) sb.AppendLine($"   {kv.Key}: {kv.Value}");
            File.WriteAllText("Temp/diag_601c_ciudad.txt", sb.ToString());
            Fin("OK dup=" + dups + " flotantes=" + flot + " (Temp/diag_601c_ciudad.txt)");
        }

        // ---------------------------------------------------------------- 601k anillo de fotos a nivel de calle alrededor de la plaza (repasadas de coherencia de la ciudad)
        static IEnumerator Bug601k()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var c = new Vector3(322f, 0f, 215.5f);
            var sb = new StringBuilder();
            sb.AppendLine("== hijos de Ciudad: nombre | centro | tamano");
            var raiz = GameObject.Find("Ciudad");
            if (raiz != null) foreach (Transform h in raiz.transform)
            {
                var b = new Bounds(); bool ini = false;
                foreach (var r in h.GetComponentsInChildren<Renderer>(false)) { if (r is ParticleSystemRenderer) continue; if (!ini) { b = r.bounds; ini = true; } else b.Encapsulate(r.bounds); }
                sb.AppendLine($"{h.name} centro={(ini ? b.center.ToString("0") : "-")} tam={(ini ? b.size.ToString("0") : "-")}");
            }
            File.WriteAllText("Temp/diag_601k_ciudad.txt", sb.ToString());
            float[] radios = { 38f, 70f };
            for (int ri = 0; ri < radios.Length; ri++)
                for (int i = 0; i < 6; i++)
                {
                    float ang = (i * 60f + ri * 30f) * Mathf.Deg2Rad;
                    var pos = c + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * radios[ri];
                    var rayo = new Ray(pos + Vector3.up * 60f, Vector3.down); float y = 0f;
                    foreach (var h in Physics.RaycastAll(rayo, 120f, ~0, QueryTriggerInteraction.Ignore)) if (h.normal.y > 0.7f && h.point.y < 3f) { y = Mathf.Max(y, h.point.y); }
                    foreach (var x in FotoCamara($"v5_ciudad_r{ri}_{i}.jpg", new Vector3(pos.x, y + 3.2f, pos.z), c + Vector3.up * 3f, 65f)) yield return x;
                }
            Fin("OK 12 fotos v5_ciudad_r*_* y Temp/diag_601k_ciudad.txt");
        }

        // ---------------------------------------------------------------- 601m repasadas 2-5: emision, luces, colores, piezas hundidas
        static IEnumerator Bug601m()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var sb = new StringBuilder();
            var rend = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            // 2) materiales con emision alta: cuantas piezas los usan
            var emi = new System.Collections.Generic.Dictionary<string, (float v, int n)>();
            var blancos = new System.Collections.Generic.Dictionary<string, int>();
            int hundidas = 0; var hundGrupo = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var r in rend)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer) continue;
                var m = r.sharedMaterial; if (m == null) continue;
                if (m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor"))
                {
                    var e = m.GetColor("_EmissionColor"); float v = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
                    emi.TryGetValue(m.name, out var q); emi[m.name] = (v, q.n + 1);
                }
                if (m.HasProperty("_BaseColor")) { var bc = m.GetColor("_BaseColor"); if (bc.r > 0.93f && bc.g > 0.93f && bc.b > 0.93f) { blancos.TryGetValue(m.name, out var q); blancos[m.name] = q + 1; } }
                var b = r.bounds;
                if (b.max.y < 0.02f && b.size.y > 0.05f) { hundidas++; string g = GrupoDe601(r.transform); hundGrupo[g] = hundGrupo.TryGetValue(g, out var k) ? k + 1 : 1; }
            }
            sb.AppendLine("== emision (materiales con valor > 1.2), ordenados por uso");
            var lista = new System.Collections.Generic.List<(string n, float v, int c)>();
            foreach (var kv in emi) if (kv.Value.v > 1.2f) lista.Add((kv.Key, kv.Value.v, kv.Value.n));
            lista.Sort((a, b) => b.c.CompareTo(a.c));
            foreach (var t in lista) sb.AppendLine($"  {t.n} emision={t.v:0.00} piezas={t.c}");
            sb.AppendLine("== materiales casi blancos puros (base > 0.93) en uso");
            foreach (var kv in blancos) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            sb.AppendLine($"== piezas hundidas bajo el piso (techo < 2 cm): {hundidas}");
            foreach (var kv in hundGrupo) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            // 3) luces por zona: intensidad y rango
            sb.AppendLine("== luces activas por grupo (n, intensidad min/prom/max, rango prom, con sombras)");
            var gl = new System.Collections.Generic.SortedDictionary<string, System.Collections.Generic.List<Light>>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy || l.type == LightType.Directional) continue;
                string g = GrupoDe601(l.transform);
                if (!gl.TryGetValue(g, out var li)) gl[g] = li = new System.Collections.Generic.List<Light>();
                li.Add(l);
            }
            foreach (var kv in gl)
            {
                float mn = 1e9f, mx = 0f, sum = 0f, rg = 0f; int sh = 0;
                foreach (var l in kv.Value) { mn = Mathf.Min(mn, l.intensity); mx = Mathf.Max(mx, l.intensity); sum += l.intensity; rg += l.range; if (l.shadows != LightShadows.None) sh++; }
                sb.AppendLine($"  {kv.Key}: n={kv.Value.Count} int={mn:0.0}/{sum / kv.Value.Count:0.0}/{mx:0.0} rango={rg / kv.Value.Count:0.0} sombras={sh}");
            }
            File.WriteAllText("Temp/diag_601m_ciudad.txt", sb.ToString());
            Fin("OK Temp/diag_601m_ciudad.txt");
        }

        // ---------------------------------------------------------------- 601n repasadas 5-6: z-fighting y escala de props repetidos
        static IEnumerator Bug601n()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var sb = new StringBuilder();
            var planas = new System.Collections.Generic.List<Renderer>();
            var porNombre = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Vector3>>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.y < 0.06f && b.size.x > 0.4f && b.size.z > 0.4f && b.max.y < 3f) planas.Add(r);
                string n = r.name; int d = n.Length; while (d > 0 && (char.IsDigit(n[d - 1]) || n[d - 1] == '_')) d--; n = n.Substring(0, d);
                if (n.Length < 3) continue;
                if (!porNombre.TryGetValue(n, out var l)) porNombre[n] = l = new System.Collections.Generic.List<Vector3>();
                l.Add(b.size);
            }
            // z-fighting: dos laminas con la misma altura (< 4 mm) y solape de >30 % del area
            int zf = 0; var zfG = new System.Collections.Generic.SortedDictionary<string, int>();
            for (int i = 0; i < planas.Count; i++)
                for (int j = i + 1; j < planas.Count; j++)
                {
                    var a = planas[i].bounds; var c = planas[j].bounds;
                    if (Mathf.Abs(a.center.y - c.center.y) > 0.004f) continue;
                    float ox = Mathf.Min(a.max.x, c.max.x) - Mathf.Max(a.min.x, c.min.x), oz = Mathf.Min(a.max.z, c.max.z) - Mathf.Max(a.min.z, c.min.z);
                    if (ox <= 0.05f || oz <= 0.05f) continue;
                    float menor = Mathf.Min(a.size.x * a.size.z, c.size.x * c.size.z);
                    if (ox * oz < 0.3f * menor) continue;
                    if (planas[i].sharedMaterial == planas[j].sharedMaterial) continue;   // mismo material: no se nota
                    zf++; string g = GrupoDe601(planas[i].transform); zfG[g] = zfG.TryGetValue(g, out var k) ? k + 1 : 1;
                    if (zfG[g] <= 2) sb.AppendLine($"  ZF: {RutaDeObjeto601(planas[i].transform)} ({planas[i].sharedMaterial.name}) vs {RutaDeObjeto601(planas[j].transform)} ({planas[j].sharedMaterial.name}) y={a.center.y:0.000} en {a.center:0}");
                }
            sb.AppendLine($"== laminas coplanares con material distinto (z-fighting): {zf} (de {planas.Count} laminas)");
            foreach (var kv in zfG) sb.AppendLine($"   {kv.Key}: {kv.Value}");
            // escala: nombres repetidos >= 4 veces con tamano muy dispar (max/min de la altura > 1.6 o del lado mayor > 1.8)
            sb.AppendLine("== props repetidos con tamano dispar (n, alto min-max, lado min-max)");
            foreach (var kv in porNombre)
            {
                if (kv.Value.Count < 4) continue;
                float hMin = 1e9f, hMax = 0f, lMin = 1e9f, lMax = 0f;
                foreach (var v in kv.Value) { hMin = Mathf.Min(hMin, v.y); hMax = Mathf.Max(hMax, v.y); float l = Mathf.Max(v.x, v.z); lMin = Mathf.Min(lMin, l); lMax = Mathf.Max(lMax, l); }
                if (hMin < 0.15f && lMin < 0.15f) continue;
                if ((hMin > 0.15f && hMax / hMin > 1.6f) || (lMin > 0.15f && lMax / lMin > 1.8f))
                    sb.AppendLine($"  {kv.Key}: n={kv.Value.Count} alto {hMin:0.00}-{hMax:0.00} lado {lMin:0.00}-{lMax:0.00}");
            }
            File.WriteAllText("Temp/diag_601n_ciudad.txt", sb.ToString());
            Fin("OK zf=" + zf + " (Temp/diag_601n_ciudad.txt)");
        }

        // ---------------------------------------------------------------- 601q repasada 7: piezas grandes y visibles de la ciudad sin collider (se atraviesan caminando)
        static IEnumerator Bug601w()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var sb = new StringBuilder(); int sin = 0; var g = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer) continue;
                var b = r.bounds; if (b.size.y < 1.2f || Mathf.Max(b.size.x, b.size.z) < 1.0f || b.min.y > 1.5f || b.size.y > 60f) continue;
                string ruta = RutaDeObjeto601(r.transform); if (!ruta.StartsWith("Ciudad/")) continue;
                if (r.GetComponentInParent<Collider>() != null) continue;
                var c = b.center; bool cubierto = false;
                foreach (var h in Physics.OverlapBox(c, b.extents * 0.9f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) { cubierto = true; break; }
                if (cubierto) continue;
                string n = r.name; if (n.Contains("Ventana") || n.Contains("Cartel") || n.Contains("Rotulo") || n.Contains("Bandera") || n.Contains("Hoja") || n.Contains("Copa") || n.Contains("Colgante") || n.Contains("Flor") || n.Contains("Arbusto") || n.Contains("Cortina") || n.Contains("Holo")) continue;
                sin++; string gr = GrupoDe601(r.transform); g[gr] = g.TryGetValue(gr, out var k) ? k + 1 : 1;
                if (g[gr] <= 3) sb.AppendLine($"  SIN COLLIDER: {ruta} centro={b.center:0.0} tam={b.size:0.0}");
            }
            sb.AppendLine($"== piezas grandes de Ciudad sin collider: {sin}"); foreach (var kv in g) sb.AppendLine($"   {kv.Key}: {kv.Value}");
            File.WriteAllText("Temp/diag_601q_ciudad.txt", sb.ToString());
            Fin("OK sin collider=" + sin + " agregados=" + SP.Presentation.ColisionesDeLaCiudad.Agregados + " (Temp/diag_601q_ciudad.txt)");
        }

        // ---------------------------------------------------------------- 601z foto aerea de la ciudad (repasadas 9-10)
        static IEnumerator Bug601z()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var c = new Vector3(322f, 0f, 230f);
            foreach (var x in FotoCamara("v5_ciudad_aerea_1.jpg", c + new Vector3(0f, 150f, -120f), c, 55f, 1280, 720)) yield return x;
            foreach (var x in FotoCamara("v5_ciudad_aerea_2.jpg", c + new Vector3(120f, 110f, 60f), c, 55f, 1280, 720)) yield return x;
            Fin("OK fotos aereas v5_ciudad_aerea_*");
        }

        // ---------------------------------------------------------------- 602 fotos ANTES/DESPUES de la tanda del 07/11 (nueva etapa de pedidos)
        public static string Sufijo602 = "despues";
        static bool BoundsDe602(string nombreContiene, out Bounds b, bool exacto = false)
        {
            b = default; bool ini = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                string ruta = RutaDeObjeto601(r.transform);
                if (!(exacto ? ruta.Contains("/" + nombreContiene + "/") : ruta.Contains(nombreContiene))) continue;
                if (!ini) { b = r.bounds; ini = true; } else b.Encapsulate(r.bounds);
            }
            return ini;
        }
        static IEnumerator Bug602a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string suf = Sufijo602; var sb = new StringBuilder();
            // zona de servidores (objetivo 3): vista cenital tipo RTS
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(2f)) yield return x;
            if (BoundsDe602("9_Servidores", out var bs, true))
            {
                sb.Append($"servidores centro={bs.center:0} tam={bs.size:0}; ");
                var rigS = SP.CameraSystem.CameraRig.Instance;
                if (rigS != null) rigS.SetMode(SP.CameraSystem.ControlMode.Rts, bs.center);   // el techo y lo de arriba reaccionan al MODO del rig, no a mi camara
                foreach (var x in Esperar(1.2f)) yield return x;
                foreach (var x in FotoCamara($"v6_servidores_rts_{suf}.jpg", bs.center + new Vector3(0f, 38f, -9f), bs.center, 60f, 1280, 720)) yield return x;
                var nombres = new System.Collections.Generic.SortedDictionary<string, int>();
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    string ruta = RutaDeObjeto601(r.transform); if (!(ruta.Contains("9_Servidores") || ruta.Contains("3_CentroDeDatos"))) continue;
                    string n = r.name.TrimEnd('0','1','2','3','4','5','6','7','8','9','_'); nombres[n + (r.enabled ? "" : " [off]")] = nombres.TryGetValue(n + (r.enabled ? "" : " [off]"), out var q) ? q + 1 : 1;
                }
                foreach (var kv in nombres) sb.AppendLine($"  {kv.Key} x{kv.Value}");
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { string ruta = RutaDeObjeto601(l.transform); if (ruta.Contains("9_Servidores") || ruta.Contains("3_CentroDeDatos")) sb.AppendLine($"  LUZ {ruta} {l.type} int={l.intensity:0.0} on={l.enabled}"); }
            }
            // zona del tanque (objetivo 4)
            OperacionPrueba.Arrancar(4);
            foreach (var x in Esperar(3f)) yield return x;
            if (BoundsDe602("4_PatioDelTanque", out var bt, true))
            {
                sb.Append($"patio tanque centro={bt.center:0} tam={bt.size:0}; ");
                foreach (var x in FotoCamara($"v6_patio_tanque_{suf}.jpg", bt.center + new Vector3(0f, 30f, -40f), bt.center, 60f, 1280, 720)) yield return x;
            }
            // mansion: escaleras y celdas (objetivo 5, mansion activa)
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            foreach (var x in FotoCamara($"v6_mansion_escalera_{suf}.jpg", new Vector3(286f, 8f, 248f), new Vector3(294f, 4f, 254f), 65f, 1280, 720)) yield return x;
            var pc = SP.Operacion.MansionGeometria.PuertaDeCeldas;
            foreach (var x in FotoCamara($"v6_celdas_{suf}.jpg", pc + new Vector3(7f, 2.6f, -7f), pc, 65f, 1280, 720)) yield return x;
            File.WriteAllText("Temp/diag_602a_" + suf + ".txt", sb.ToString());
            Fin("OK fotos v6_*_" + suf);
        }

        // ---------------------------------------------------------------- 602b soldados de la mansion: levitacion (altura sobre el piso que tienen debajo)
        static IEnumerator Bug602b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            ModoDios.Poner(true);
            // la escuadra al pie de la escalera 1 y 40 s de juego con los enemigos de la mansion activos: se muestrea el hueco real (altura - piso - pivote)
            var entrada = SP.Operacion.MansionGeometria.EntradaDeTorre(0);
            var yo = Poseido(); var escuadra = new System.Collections.Generic.List<Soldier>();
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Player && a.Health != null && a.Health.IsAlive) escuadra.Add(a);
            for (int k = 0; k < escuadra.Count; k++) escuadra[k].transform.position = new Vector3(entrada.x + k * 1.2f, 0.9f, entrada.z - 3f);
            var donde = new System.Collections.Generic.Dictionary<string, string>(); var peores = new System.Collections.Generic.Dictionary<string, float>(); int muestras = 0, flotando = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 40f)
            {
                yield return null;
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    var p = s.transform.position; if (p.x < 270f || p.x > 375f || p.z < 235f || p.z > 295f) continue;
                    float suelo = float.NegativeInfinity;
                    foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                    { if (h.collider.GetComponentInParent<SP.Actors.Soldier>() != null || h.normal.y < 0.5f) continue; suelo = Mathf.Max(suelo, h.point.y); }
                    if (float.IsNegativeInfinity(suelo)) continue;
                    float gap = p.y - suelo - SP.Actors.SoldierMotor.PivoteSobrePiso; muestras++;
                    if (gap > 0.3f && !s.Motor.IsJumping) { flotando++; if (!peores.TryGetValue(s.name, out var q) || gap > q) { peores[s.name] = gap; donde[s.name] = $"{p:0.0} piso={suelo:0.00} " + (s.Brain != null ? "agente" : "-") + " saltando=" + s.Motor.IsJumping; } }
                }
            }
            W8Ok(ref ok, flotando == 0, sb, $"en 40 s: {muestras} muestras, flotando >30 cm={flotando} ({peores.Count} soldados)");
            int n = 0; foreach (var kv in peores) { if (n++ >= 6) break; sb.Append($"[{kv.Key} max hueco {kv.Value:0.00} m en {donde[kv.Key]}] "); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 602c enemigo PESADO: vida x4, minigun, ensanche, frena 2 s y abre fuego sostenido
        static IEnumerator Bug602c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado")) { molde = a; break; }
            if (molde == null) { Fin("FALLO no hay enemigo molde"); yield break; }
            var go = Object.Instantiate(molde.gameObject, yo.transform.position + yo.transform.forward * 22f + Vector3.up * 0.1f, Quaternion.LookRotation(-yo.transform.forward), molde.transform.parent);
            go.SetActive(true);
            var s = go.GetComponent<Soldier>(); int vidaBase = s.Health.MaxHealth;
            var p = EnemigoPesado.Aplicar(s);
            W8Ok(ref ok, p != null && s.name.StartsWith("Pesado"), sb, "se creo el pesado");
            foreach (var x in Esperar(0.6f)) yield return x;
            W8Ok(ref ok, s.Health.MaxHealth == vidaBase * EnemigoPesado.MultiplicadorDeVida, sb, $"vida x4 ({vidaBase} -> {s.Health.MaxHealth})");
            W8Ok(ref ok, s.Weapon != null && s.Weapon.CurrentWeaponKind == WeaponKind.Minigun, sb, $"lleva la minigun ({(s.Weapon != null ? s.Weapon.CurrentWeaponKind.ToString() : "-")})");
            var animP = s.GetComponentInChildren<Animator>(true);
            W8Ok(ref ok, p != null && p.AnimacionPropia && animP != null && animP.runtimeAnimatorController != null && animP.runtimeAnimatorController.name == "AC_Pesado", sb, $"usa su animacion de Mixamo (controlador {(animP != null && animP.runtimeAnimatorController != null ? animP.runtimeAnimatorController.name : "-")})");
            var spec = WeaponCatalog.Get(WeaponKind.Minigun);
            W8Ok(ref ok, spec.Cooldown <= 0.06f && spec.MagazineSize >= 150 && spec.ReloadDuration >= 4f, sb, $"minigun: cadencia {1f / spec.Cooldown:0} disp/s, cargador {spec.MagazineSize}, recarga {spec.ReloadDuration:0.0} s (pesada)");
            W8Ok(ref ok, s.transform.localScale.x > molde.transform.localScale.x * 1.05f, sb, $"cuerpo ensanchado x{s.transform.localScale.x / molde.transform.localScale.x:0.00}");
            var clipMin = SP.Presentation.GenericSfx.GetWeaponShot(WeaponKind.Minigun); var clipHeavy = SP.Presentation.GenericSfx.GetWeaponShot(WeaponKind.Heavy); var clipRif = SP.Presentation.GenericSfx.GetWeaponShot(WeaponKind.Rifle);
            W8Ok(ref ok, clipMin != null && clipMin != clipRif && clipMin != clipHeavy && clipMin.length >= 0.3f, sb, $"su disparo es propio (clip de {(clipMin != null ? clipMin.length : 0f):0.00} s, tono de 70 Hz) y CubeFxReactor le suma el golpe grave de la Heavy");
            // comportamiento: lo ve -> se estaciona -> a los 2 s abre fuego
            if (s.Brain != null) s.Brain.SetPatrolRoute(new[] { yo.transform.position, yo.transform.position });
            float t0 = Time.realtimeSinceStartup; float tEst = -1f, tFre = -1f;
            while (Time.realtimeSinceStartup - t0 < 14f && p != null)
            {
                yield return null;
                if (p.Estado == EnemigoPesado.Fase.Estacionando && tEst < 0f) tEst = Time.time;
                if (p.Estado == EnemigoPesado.Fase.Frenesi && tFre < 0f) { tFre = Time.time; break; }
            }
            W8Ok(ref ok, tEst >= 0f, sb, "al ver al enemigo se estaciona");
            float quieto = p != null && p.InicioDeFrenesi >= 0f && p.InicioDeEstacionar >= 0f ? p.InicioDeFrenesi - p.InicioDeEstacionar : -1f;
            W8Ok(ref ok, quieto >= EnemigoPesado.SegundosDeEstacionamiento - 0.05f && quieto <= EnemigoPesado.SegundosDeEstacionamiento + 0.4f, sb, $"se queda quieto 2 s de juego antes del frenesi ({quieto:0.00} s)");
            W8Ok(ref ok, s.Brain != null && s.Brain.Quieto, sb, "durante el frenesi no se mueve");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 602d helicoptero boss, tanque (cadencia x2, marcha variable, mira sin saltos), terminal de celdas
        static IEnumerator Bug602d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in PrepararTanque()) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, embarqueOk, sb, "el jugador quedo de artillero");
            // helicoptero boss: 5 veces la vida y resistente
            var h = HelicopteroEnemigo.IniciarSobre(d.halcon, d.plaza != null ? d.plaza : d.tanque.transform, 0f);
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, h != null && h.Vehiculo != null, sb, "se creo un helicoptero enemigo");
            if (h != null && h.Vehiculo != null)
            {
                W8Ok(ref ok, h.Vehiculo.Health.MaxHealth == HelicopteroEnemigo.VidaDeAsalto && HelicopteroEnemigo.VidaDeAsalto == 320 * 5, sb, $"vida x5 (320 -> {h.Vehiculo.Health.MaxHealth})");
                int antes = h.Vehiculo.Health.Current; h.Vehiculo.TakeDamage(100, -1);
                int perdio = antes - h.Vehiculo.Health.Current;
                W8Ok(ref ok, perdio > 0 && perdio <= 65, sb, $"es resistente: un golpe de 100 le saca {perdio} (factor {HelicopteroEnemigo.FactorDeResistencia:0.0})");
                Object.Destroy(h.gameObject);
            }
            // cadencia del canon del jugador x2
            var canon = d.tanque.TorretaCanon;
            W8Ok(ref ok, canon != null && Mathf.Approximately(canon.FactorDeCadencia, 2f), sb, $"el canon del tanque del jugador dispara x{(canon != null ? canon.FactorDeCadencia : 0f):0.0}");
            if (canon != null)
            {
                float baseSerializada = (float)typeof(SP.Vehicles.TurretWeapon).GetField("fireCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(canon);
                float esperado = baseSerializada * (1f + canon.Heat * 1.6f / 2f) / 2f;
                W8Ok(ref ok, Mathf.Abs(canon.EffectiveCooldown - esperado) < 0.01f && canon.EffectiveCooldown < baseSerializada * 0.6f, sb, $"cooldown efectivo {canon.EffectiveCooldown:0.00} s contra {baseSerializada * (1f + canon.Heat * 1.6f):0.00} s sin el x2 (mas rapido, base {baseSerializada:0.00})");
            }
            // marcha variable: el factor de ritmo recorre entre 1 y 1,6
            var motor = d.tanque.GetComponent<SP.Vehicles.VehicleMotor>();
            float mn = 9f, mx = 0f;
            if (motor != null)
            {
                W8Ok(ref ok, motor.VariarRitmo, sb, "la huida pide ritmo variable");
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 22f) { yield return null; mn = Mathf.Min(mn, motor.FactorDeRitmo); mx = Mathf.Max(mx, motor.FactorDeRitmo); }
                W8Ok(ref ok, mn < 1.15f && mx > 1.45f, sb, $"el ritmo de marcha varia entre x{mn:0.00} y x{mx:0.00}");
            }
            // terminal de celdas
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            ModoDios.Poner(true);
            d = OperacionDirector.Instancia;
            d.SaltarAlSegundoPisoPorPrueba();
            foreach (var x in Esperar(3f)) yield return x;
            W8Ok(ref ok, d.TerminalDeCeldas != null, sb, "la terminal de las celdas existe");
            if (d.TerminalDeCeldas != null)
            {
                var rends = d.TerminalDeCeldas.GetComponentsInChildren<Renderer>();
                bool emisiva = false; foreach (var r in rends) if (r.sharedMaterial != null && r.sharedMaterial.name == "M_Op_TerminalCeldas" && r.sharedMaterial.IsKeywordEnabled("_EMISSION")) emisiva = true;
                W8Ok(ref ok, emisiva, sb, "la pantalla de la terminal es emisiva");
                W8Ok(ref ok, d.LucesApagadasDelPiso >= 3, sb, $"las demas luces del piso estan atenuadas ({d.LucesApagadasDelPiso})");
                var yo = Poseido();
                if (yo == null) foreach (var aa in ActorRegistry.All) if (aa != null && aa.Team == TeamId.Player && aa.Health != null && aa.Health.IsAlive) { yo = aa; break; }
                Vector3 pos = d.TerminalDeCeldas.position;
                W8Ok(ref ok, !d.MilicianosLiberados, sb, "antes de usar la terminal las celdas siguen cerradas");
                yo.transform.position = pos + d.TerminalDeCeldas.forward * -1.5f + Vector3.up * 0.9f;
                float distTerminal = Vector3.Distance(new Vector3(yo.transform.position.x, 0f, yo.transform.position.z), new Vector3(pos.x, 0f, pos.z));
                W8Ok(ref ok, distTerminal < OperacionDirector.AlcanceDeLaTerminalDeCeldas, sb, $"a 1,5 m de la terminal se esta dentro del alcance ({distTerminal:0.0} m < {OperacionDirector.AlcanceDeLaTerminalDeCeldas:0.0})");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 602e RTS: [Q] posee al aliado apuntado (con resalte) y la sala de servidores se despeja solo en RTS
        static IEnumerator Bug602e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var drv = PlayerInputDriver.Activo; var rig = SP.CameraSystem.CameraRig.Instance;
            var yo = Poseido();
            Soldier otro = null;
            foreach (var a in ActorRegistry.All) if (a != null && a != yo && a.Team == TeamId.Player && a.Role != RoleType.Civilian && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy) { otro = a; break; }
            if (drv == null || rig == null || yo == null || otro == null) { Fin("FALLO faltan driver/rig/aliado"); yield break; }
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, otro.transform.position);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, rig.Mode == SP.CameraSystem.ControlMode.Rts, sb, "la camara esta en RTS");
            var mouse = UnityEngine.InputSystem.Mouse.current; var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse == null || kb == null) { Fin("FALLO no hay mouse/teclado virtual"); yield break; }
            Vector3 pant = Vector3.zero; float tEnc = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tEnc < 8f)
            {
                rig.RecenterOn(otro.transform.position);
                yield return null; yield return null;
                pant = rig.Cam.WorldToScreenPoint(otro.transform.position + Vector3.up * 1.0f);
                if (pant.z > 1f && pant.x > 50f && pant.x < Screen.width - 50f && pant.y > 50f && pant.y < Screen.height - 50f) break;
            }
            W8Ok(ref ok, pant.z > 1f && pant.x > 0f && pant.x < Screen.width && pant.y > 0f && pant.y < Screen.height, sb, $"el aliado entra en el encuadre RTS (pantalla {pant}, vista {Screen.width}x{Screen.height})");
            // se prueba apuntar a distintas alturas del cuerpo del aliado y se queda con la primera que lo detecta
            string barrido = "";
            foreach (float alto in new[] { 0.3f, 0.9f, 1.5f, 2.2f, 2.8f })
            {
                var pp = rig.Cam.WorldToScreenPoint(otro.transform.position + Vector3.up * alto);
                UnityEngine.InputSystem.LowLevel.InputState.Change(mouse.position, (Vector2)pp);
                yield return null; yield return null; yield return null;
                barrido += $"[+{alto:0.0}m -> {(drv.AliadoApuntadoRts != null ? drv.AliadoApuntadoRts.DisplayName : "nadie")} {drv.MotivoDelResalte}] ";
                if (drv.AliadoApuntadoRts == otro) { pant = pp; break; }
            }
            sb.Append(barrido);
            bool resaltado = drv.AliadoApuntadoRts == otro;   // se evalua en el mismo instante de la deteccion (la camara sigue en movimiento)
            W8Ok(ref ok, resaltado, sb, $"al apuntar a {otro.DisplayName} se lo resalta (apuntado={(drv.AliadoApuntadoRts != null ? drv.AliadoApuntadoRts.DisplayName : "nadie")}; motivo={drv.MotivoDelResalte}; pantalla={pant}; mouse={mouse.position.ReadValue()})");
            bool hayAro = false; foreach (var f in Object.FindObjectsByType<SP.Presentation.SelectionRingFx>(FindObjectsSortMode.None)) if (f.Target == otro.transform) hayAro = true;
            W8Ok(ref ok, hayAro, sb, "tiene el aro de resalte encima");
            // el flanco de pulsacion solo vale durante el update que procesa el evento: se encola SIN forzar InputSystem.Update().
            // La camara y el aliado se mueven, asi que se reintenta: se reapunta (mouse sobre el aliado) y se pulsa Q solo cuando el aliado esta apuntado.
            PonerEntradaSinFoco();
            string diagQ = "";
            for (int intento = 0; intento < 6 && Poseido() != otro; intento++)
            {
                rig.RecenterOn(otro.transform.position);
                for (int k = 0; k < 4; k++)
                {
                    UnityEngine.InputSystem.LowLevel.InputState.Change(mouse.position, (Vector2)rig.Cam.WorldToScreenPoint(otro.transform.position + Vector3.up * 1.5f));
                    yield return null;
                }
                UnityEngine.InputSystem.LowLevel.InputState.Change(mouse.position, (Vector2)rig.Cam.WorldToScreenPoint(otro.transform.position + Vector3.up * 1.5f));
                yield return null;
                if (drv.AliadoApuntadoRts != otro) { diagQ += $"[i{intento} sin apuntar] "; continue; }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Q));
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                diagQ += $"[i{intento} Q enviada modo={rig.Mode}] ";
                if (rig.Mode == SP.CameraSystem.ControlMode.Fps) break;
            }
            sb.Append(diagQ);
            foreach (var x in Esperar(0.8f)) yield return x;
            W8Ok(ref ok, Poseido() == otro, sb, $"[Q] apuntando a un aliado lo posee (ahora {(Poseido() != null ? Poseido().DisplayName : "nadie")})");
            W8Ok(ref ok, rig.Mode == SP.CameraSystem.ControlMode.Fps, sb, "pasa a primera persona");

            // sala de servidores: luces/vigas colgantes se ocultan solo en RTS
            OperacionPrueba.Arrancar(3);
            foreach (var x in Esperar(2f)) yield return x;
            rig = SP.CameraSystem.CameraRig.Instance;
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, new Vector3(0f, 0f, -61f));
            foreach (var x in Esperar(1.5f)) yield return x;
            int ocultasRts = SalaDespejadaEnRts_PiezasOcultas();
            W8Ok(ref ok, ocultasRts >= 10, sb, $"en RTS se ocultan {ocultasRts} piezas colgantes de la sala de servidores (paneles de luz, vigas)");
            rig.SetMode(SP.CameraSystem.ControlMode.Fps, new Vector3(0f, 0f, -61f));
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, SalaDespejadaEnRts_PiezasOcultas() == 0, sb, "al volver a FPS se dibujan de nuevo");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 602f fotos "despues" de lo nuevo: pesado junto a un soldado normal, helicoptero jefe y aro de posesion en RTS
        static IEnumerator Bug602f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado")) { molde = a; break; }
            if (yo == null || molde == null) { Fin("FALLO faltan jugador/molde"); yield break; }
            Vector3 f = yo.transform.forward; Vector3 baseP = yo.transform.position + f * 9f; Vector3 der = Vector3.Cross(Vector3.up, f);
            var normal = Object.Instantiate(molde.gameObject, baseP - der * 0.9f, Quaternion.LookRotation(-f), molde.transform.parent); normal.SetActive(true);
            var gordo = Object.Instantiate(molde.gameObject, baseP + der * 0.9f, Quaternion.LookRotation(-f), molde.transform.parent); gordo.SetActive(true);
            EnemigoPesado.Aplicar(gordo.GetComponent<Soldier>());
            foreach (var n in new[] { normal, gordo }) { var b = n.GetComponent<Soldier>().Brain; if (b != null) b.enabled = false; }
            foreach (var x in Esperar(0.8f)) yield return x;
            var luz = new GameObject("LuzFoto_Prueba").AddComponent<Light>(); luz.type = LightType.Point; luz.range = 14f; luz.intensity = 3.2f; luz.color = new Color(1f, 0.95f, 0.85f);
            float tsPrevio = Time.timeScale; Time.timeScale = 0f;   // congela al pesado para las fotos
            Vector3 cg = baseP + der * 0.9f;   // posicion fija: el soldado avanza si se lo deja (se lo reubica antes de cada foto)
            luz.transform.position = cg - f * 2.5f + Vector3.up * 2.6f;
            gordo.transform.position = cg; normal.transform.position = baseP - der * 0.9f;
            foreach (var x in FotoCamara("v6_pesado_despues.jpg", baseP - f * 4.4f + Vector3.up * 1.5f - der * 1.3f, baseP + Vector3.up * 1.0f, 40f, 2560, 1440)) yield return x;   // junto al soldado normal (a su izquierda)
            Object.Destroy(luz.gameObject); Time.timeScale = tsPrevio;
            Object.Destroy(normal); Object.Destroy(gordo);
            // helicoptero jefe
            var d = OperacionDirector.Instancia;
            var h = HelicopteroEnemigo.IniciarSobre(d.halcon, d.plaza != null ? d.plaza : d.tanque.transform, 0f);
            foreach (var x in Esperar(2.5f)) yield return x;
            if (h != null && h.Vehiculo != null)
            {
                var hp = h.Vehiculo.transform.position;
                foreach (var x in FotoCamara("v6_helicoptero_jefe_despues.jpg", hp - Vector3.forward * 22f + Vector3.up * 5f, hp, 50f, 1280, 720)) yield return x;
                Object.Destroy(h.gameObject);
            }
            W8Ok(ref ok, h != null, sb, "helicoptero jefe fotografiado");
            Fin((ok ? "OK " : "FALLO ") + sb + "fotos v6_pesado_despues / v6_helicoptero_jefe_despues");
        }
        // ---------------------------------------------------------------- 603a diagnostico: marcadores/etiquetas visibles en cada fase (cuadrados amarillo/azul, titulos de nombres)
        static IEnumerator Bug603a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var rep = new StringBuilder(); var vistos = new System.Collections.Generic.HashSet<string>();
            foreach (int fase in new[] { 1, 3, 5, 6 })
            {
                OperacionPrueba.Arrancar(fase);
                foreach (var x in Esperar(3f)) yield return x;
                foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                {
                    var r = tm.GetComponent<MeshRenderer>(); if (r == null || !r.enabled || !tm.gameObject.activeInHierarchy) continue;
                    string k = $"\"{tm.text.Replace(System.Environment.NewLine, " ").Replace((char)10, (char)32)}\" en {RutaDeObjeto601(tm.transform)} pos={tm.transform.position:0}";
                    if (vistos.Add(k)) rep.AppendLine($"f{fase} {k}");
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/diag_603a_marcadores.txt"), rep.ToString());
            Fin("OK diag Temp/diag_603a_marcadores.txt");
        }

        // ---------------------------------------------------------------- 603b fotos con la camara principal (FPS y RTS) en cada fase, para ver los marcadores
        static IEnumerable FotoPrincipal(string archivo)
        {
            var cam = Camera.main; if (cam == null) yield break;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var previoRt = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = previoRt;
            var previa = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            RenderTexture.active = previa;
            File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToJPG(88));
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
            yield return null;
        }
        static IEnumerator Bug603b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            foreach (int fase in new[] { 1, 3, 5 })
            {
                OperacionPrueba.Arrancar(fase);
                foreach (var x in Esperar(3.5f)) yield return x;
                foreach (var x in FotoPrincipal($"v7_fase{fase}_fps.jpg")) yield return x;
                var rig = SP.CameraSystem.CameraRig.Instance; var yo = Poseido();
                if (rig != null && yo != null)
                {
                    rig.SetMode(SP.CameraSystem.ControlMode.Rts, yo.transform.position);
                    foreach (var x in Esperar(1.5f)) yield return x;
                    foreach (var x in FotoPrincipal($"v7_fase{fase}_rts.jpg")) yield return x;
                    rig.SetMode(SP.CameraSystem.ControlMode.Fps, yo.transform.position);
                    foreach (var x in Esperar(0.5f)) yield return x;
                }
            }
            Fin("OK fotos v7_fase*_fps/rts");
        }
        // ---------------------------------------------------------------- 603c que objetos hay bajo ciertos puntos de pantalla (RTS, fase 5)
        static IEnumerator Bug603c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3.5f)) yield return x;
            var rig = SP.CameraSystem.CameraRig.Instance; var yo = Poseido();
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, yo.transform.position);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); var cuenta = new System.Collections.Generic.SortedDictionary<string, int>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is TrailRenderer || r is LineRenderer) continue;
                var bb = r.bounds; if (bb.size.y > 0.25f || bb.size.x < 0.7f || bb.size.z < 0.7f || bb.size.x > 12f || bb.size.z > 12f) continue;
                Color c = Color.black; var m = r.sharedMaterial; if (m == null) continue;
                if (m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION")) c = m.GetColor("_EmissionColor");
                if (c.maxColorComponent < 0.05f && m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
                bool amarillo = c.r > 0.6f && c.g > 0.5f && c.b < 0.45f, azul = c.b > 0.6f && c.r < 0.5f;
                if (!amarillo && !azul) continue;
                string k = (amarillo ? "AMARILLO " : "AZUL ") + RutaDeObjeto601(r.transform) + " mat=" + m.name; cuenta.TryGetValue(k, out int n); cuenta[k] = n + 1;
            }
            foreach (var kv in cuenta) sb.AppendLine($"{kv.Value} x {kv.Key}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/diag_603c.txt"), sb.ToString());
            Fin("OK diag_603c");
        }
        // ---------------------------------------------------------------- 603d titulos de zona/objeto ocultos; los carteles de ruta se mantienen
        static IEnumerator Bug603d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(3f)) yield return x;
            var sb = new StringBuilder(); bool ok = true; int visiblesRuta = 0; var malos = "";
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                var r = tm.GetComponent<MeshRenderer>(); if (r == null || !r.enabled || !tm.gameObject.activeInHierarchy) continue;
                string ruta = RutaDeObjeto601(tm.transform);
                if (ruta.Contains("Rotulo_PUESTO") || ruta.Contains("Rotulo_ANTIAEREA") || ruta.Contains("Rotulo_GENERADOR") || ruta.Contains("Rotulo_SECTOR")) malos += ruta + "; ";
                if (ruta.Contains("Rotulo_CIUDAD") || ruta.Contains("Rotulo_PLAZA") || ruta.Contains("Rotulo_BIENVENIDOS")) visiblesRuta++;
            }
            W8Ok(ref ok, malos == "", sb, "no queda ningun titulo de puesto/antiaerea/generador/sector dibujado " + malos);
            W8Ok(ref ok, SP.Presentation.TitulosDeObjetosOcultos.Ocultos >= 4, sb, $"se ocultaron {SP.Presentation.TitulosDeObjetosOcultos.Ocultos} titulos");
            W8Ok(ref ok, visiblesRuta >= 2, sb, $"los carteles de ruta siguen ({visiblesRuta})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 603e rombos de aliado: ocultos fuera de RTS, visibles en RTS; los de enemigo no cambian
        static IEnumerator Bug603e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3f)) yield return x;
            var rig = SP.CameraSystem.CameraRig.Instance; var yo = Poseido();
            var sb = new StringBuilder(); bool ok = true;
            int AliadosVisibles(out int enemigos)
            {
                int a = 0; enemigos = 0;
                foreach (var l in Object.FindObjectsByType<SP.Presentation.UnitLocatorCylinder>(FindObjectsSortMode.None))
                {
                    var so = l.GetComponent<Soldier>(); if (so == null || !l.MarcadorVisible) continue;
                    if (so.Team == TeamId.Enemy) enemigos++; else a++;
                }
                return a;
            }
            rig.SetMode(SP.CameraSystem.ControlMode.Fps, yo.transform.position);
            foreach (var x in Esperar(1.0f)) yield return x;
            int aFps = AliadosVisibles(out int eFps);
            W8Ok(ref ok, aFps == 0, sb, $"en primera persona no hay rombos de aliado ({aFps}); de enemigo {eFps}");
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, yo.transform.position);
            foreach (var x in Esperar(1.5f)) yield return x;
            int aRts = AliadosVisibles(out _);
            W8Ok(ref ok, aRts >= 1, sb, $"en RTS los rombos de aliado se ven ({aRts})");
            rig.SetMode(SP.CameraSystem.ControlMode.Fps, yo.transform.position);
            foreach (var x in Esperar(1.0f)) yield return x;
            W8Ok(ref ok, AliadosVisibles(out _) == 0, sb, "al volver a FPS se apagan");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 603f los refuerzos llegan en helicoptero y no por portones
        static IEnumerator Bug603f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var pos = new Vector3(327f, 0f, 192f);
            int antes = SP.Operacion.HeliDeDescarga.Lanzados;
            EntradaDeReserva.Marcar(pos);
            foreach (var x in Esperar(0.5f)) yield return x;
            W8Ok(ref ok, SP.Operacion.HeliDeDescarga.Lanzados == antes + 1, sb, "llega un helicoptero de descarga");
            int piezas = 0; foreach (var g in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (g.name == "Pieza" && g.parent != null && g.parent.name == "Compuerta_Reserva") piezas++;
            W8Ok(ref ok, piezas == 0, sb, $"no hay portones dibujados ({piezas} piezas)");
            GameObject heli = GameObject.Find("HeliDeDescarga");
            Vector3 pHeli = Vector3.zero; bool sogas = false, fotoHecha = false; float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 30f && heli != null)
            {
                yield return null;
                heli = GameObject.Find("HeliDeDescarga"); if (heli == null) break;
                foreach (var lr in heli.GetComponentsInChildren<LineRenderer>()) if (lr.enabled) { sogas = true; pHeli = heli.transform.position; }
                if (sogas && !fotoHecha) { fotoHecha = true; foreach (var x in FotoCamara("v6_heli_descarga_despues.jpg", pos + new Vector3(-16f, 7f, -16f), pos + Vector3.up * 9f, 55f, 1600, 900)) yield return x; }
            }
            W8Ok(ref ok, sogas && Mathf.Abs(pHeli.y - HeliDeDescarga.Altura) < 3f && Vector3.Distance(new Vector3(pHeli.x, 0, pHeli.z), new Vector3(pos.x, 0, pos.z)) < 3f, sb, $"queda suspendido sobre el punto con sogas ({pHeli})");
            W8Ok(ref ok, heli == null, sb, "despues se retira y desaparece");
            W8Ok(ref ok, SP.Operacion.HeliDeDescarga.Activos == 0, sb, "no quedan helicopteros activos");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 603g el pesado recarga con su animacion lenta y sus disparos sueltan particulas
        static IEnumerator Bug603g()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado")) { molde = a; break; }
            if (yo == null || molde == null) { Fin("FALLO faltan jugador/molde"); yield break; }
            var go = Object.Instantiate(molde.gameObject, yo.transform.position + yo.transform.forward * 12f + Vector3.up * 0.1f, Quaternion.LookRotation(-yo.transform.forward), molde.transform.parent); go.SetActive(true);
            var s = go.GetComponent<Soldier>(); var p = EnemigoPesado.Aplicar(s);
            foreach (var x in Esperar(0.8f)) yield return x;
            var anim = s.GetComponentInChildren<Animator>(true);
            int capa = -1; for (int i = 0; i < anim.layerCount; i++) if (anim.GetLayerName(i) == "Recarga") capa = i;
            W8Ok(ref ok, capa >= 0, sb, $"AC_Pesado tiene la capa de recarga (indice {capa})");
            float peso0 = capa >= 0 ? anim.GetLayerWeight(capa) : -1f;
            s.Weapon.RestaurarMunicion(s.Weapon.MagazineSize / 2);   // con el cargador lleno Reload() no hace nada
            s.Weapon.Reload();
            sb.Append($"[diag: recargando {s.Weapon.IsReloading}, propia {p.AnimacionPropia}, activo {p.isActiveAndEnabled}, ctrl {(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null")}] ");
            float t0 = Time.time, tTope = 0f, pesoMax = 0f;
            while (Time.time - t0 < 6f)
            {
                yield return null;
                if (capa >= 0) pesoMax = Mathf.Max(pesoMax, anim.GetLayerWeight(capa));
                if (p.Recargando && tTope <= 0f) tTope = Time.time - t0;
            }
            W8Ok(ref ok, peso0 == 0f && pesoMax > 0.9f, sb, $"al recargar la capa sube a {pesoMax:0.00} (antes {peso0:0.00}) y se nota a los {tTope:0.00} s");
            W8Ok(ref ok, s.Weapon.IsReloading == false && !p.Recargando, sb, "al terminar la recarga vuelve a la pose de disparo");
            int antes = SP.Presentation.MinigunFx.Disparos;
            SP.Presentation.MinigunFx.Disparo(s.transform.position + Vector3.up * 1.2f, s.transform.forward);
            W8Ok(ref ok, SP.Presentation.MinigunFx.Disparos == antes + 1, sb, "cada disparo de minigun lanza sus particulas");
            Object.Destroy(go);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 603h foto de las particulas de la minigun del pesado
        static IEnumerator Bug603h()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(2f)) yield return x;
            ModoDios.Poner(true);
            var yo = Poseido();
            Soldier molde = null;
            foreach (var a in ActorRegistry.All) if (a != null && a.Team == TeamId.Enemy && a.Health != null && a.Health.IsAlive && a.GetComponent<EnemigoPesado>() == null && !a.name.StartsWith("Pesado")) { molde = a; break; }
            if (yo == null || molde == null) { Fin("FALLO faltan jugador/molde"); yield break; }
            Vector3 f = yo.transform.forward; Vector3 baseP = yo.transform.position + f * 10f;
            var go = Object.Instantiate(molde.gameObject, baseP + Vector3.up * 0.1f, Quaternion.LookRotation(-f), molde.transform.parent); go.SetActive(true);
            var s = go.GetComponent<Soldier>(); EnemigoPesado.Aplicar(s);
            var b = s.Brain; if (b != null) b.enabled = false;
            foreach (var x in Esperar(0.8f)) yield return x;
            var luz = new GameObject("LuzFoto_Prueba").AddComponent<Light>(); luz.type = LightType.Point; luz.range = 16f; luz.intensity = 3.5f; luz.transform.position = baseP - f * 3f + Vector3.up * 3f;
            var boca = s.Weapon != null && s.Weapon.Muzzle != null ? s.Weapon.Muzzle : s.transform;
            for (int k = 0; k < 8; k++) { SP.Presentation.MinigunFx.Disparo(boca.position, boca.forward); yield return null; }
            var lado = Vector3.Cross(Vector3.up, f).normalized;
            foreach (var x in FotoCamara("v6_pesado_particulas_despues.jpg", boca.position + lado * 4.5f + Vector3.up * 1.2f - f * 1.0f, boca.position + boca.forward * 1.2f, 40f, 1600, 900)) yield return x;
            Object.Destroy(luz.gameObject); Object.Destroy(go);
            Fin("OK foto v6_pesado_particulas_despues");
        }
        static int SalaDespejadaEnRts_PiezasOcultas() => SP.Presentation.SalaDespejadaEnRts.PiezasOcultas;

        // ---------------------------------------------------------------- 601v fotos de #346 y #350 en las poses del Sheet + regla "el Editor no sube bugs al Sheet"
        static IEnumerator Bug601v()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            W8Ok(ref ok, SP.Core.SubidaDeBug.SubeAlSheet == !Application.isEditor && !SP.Core.SubidaDeBug.SubeAlSheet, sb, "en el Editor los reportes NO suben al Sheet (SubeAlSheet=false)");
            W8Ok(ref ok, !SP.Core.SubidaDeBug.Disponible, sb, "en el Editor la subida no esta disponible (solo guarda local)");
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            foreach (var x in FotoDePose("v4_346_despues.jpg", -47.03f, 5.2f, 255.2f, 28.4f, 126f, 0f, 60f)) yield return x;
            var f = SP.Presentation.FlechaDeOleada.Mostrar(new Vector3(325f, 0f, 298f), Vector3.left, 8f);
            foreach (var x in Esperar(0.5f)) yield return x;
            foreach (var x in FotoDePose("v4_350_despues.jpg", 331.22f, 4.43f, 298.21f, 31.6f, 267.9f, 0f, 60f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + "(fotos 346 y 350)");
        }

        // ---------------------------------------------------------------- 601d #347 en modo dios el tanque es indestructible; sin modo dios el danio pega fuerte
        static IEnumerator Bug601d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; d.Embarcar();
            foreach (var x in Esperar(8f)) yield return x;
            var t = d.tanque; int max = t.Health.MaxHealth;
            ModoDios.Poner(false);
            int a0 = t.Health.Current; for (int i = 0; i < 40; i++) { t.TakeDamage(7, -1); }
            int sinDios = a0 - t.Health.Current;
            W8Ok(ref ok, sinDios >= max * 0.04f, sb, $"sin modo dios 40 impactos le sacan {sinDios} HP ({sinDios * 100f / max:0.0}%)");
            ModoDios.Poner(true);
            int b0 = t.Health.Current;
            for (int i = 0; i < 400; i++) t.TakeDamage(70, -1);
            W8Ok(ref ok, t.Health.Current < b0 && t.Health.Current >= 1 && !t.IsDestroyed, sb, $"con modo dios 400 cohetes le bajan la vida pero no lo destruyen ({b0} -> {t.Health.Current}) (bug #359)");
            // bug #359: cada explosion enemiga le saca al menos el 15 %
            ModoDios.Poner(false); t.Health.RestaurarVida(max);
            SP.Vehicles.Vehicle.DanoDeExplosionEnCurso = true; t.TakeDamage(10, 9999); SP.Vehicles.Vehicle.DanoDeExplosionEnCurso = false;
            int porGolpe = max - t.Health.Current;
            W8Ok(ref ok, porGolpe >= Mathf.CeilToInt(max * 0.15f) - 1, sb, $"una explosion enemiga le saca {porGolpe} HP ({porGolpe * 100f / max:0.0}%, minimo 15 %)");
            t.Health.RestaurarVida(max); ModoDios.Poner(true);
            foreach (var x in Esperar(0.5f)) yield return x;
            foreach (var x in CapturarPantalla("v4_347_despues.png")) yield return x;
            Time.timeScale = 1f;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 601e #349 plaza con 16 enemigos visibles que vienen a mi; #352 techo
        static IEnumerator Bug601e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.0f)) yield return x;
            var d = OperacionDirector.Instancia;
            W8Ok(ref ok, OperacionDirector.EnemigosDeLaPlaza == 16, sb, "la plaza trae 16 enemigos (antes 8)");
            ModoDios.Poner(true);
            sb.Append($"(t=1s mansionActiva={d.MansionActiva}) ");
            for (int k = 0; k < 4; k++) { foreach (var x in Esperar(3f)) yield return x; sb.Append($"(t={4 + k * 3}s activa={d.MansionActiva} creados={d.MansionEnemigosCreados} etapa={d.EtapaMansion}) "); }
            int vivos = 0, reveladosOK = 0, cercaDeMi = 0; var yo = Poseido();
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive || !s.name.StartsWith("Mansion_Enemigo")) continue;
                vivos++;
                if (yo != null && Vector3.Distance(s.transform.position, yo.transform.position) < 45f) cercaDeMi++;
            }
            sb.Append($"(mansionActiva={d.MansionActiva} etapa={d.EtapaMansion} creados={d.MansionEnemigosCreados} fase={d.Fase}) ");
            W8Ok(ref ok, vivos >= 3, sb, $"ya hay enemigos en la plaza ({vivos}) y {cercaDeMi} a menos de 45 m del jugador");
            foreach (var x in CapturarPantalla("v4_349_despues.png")) yield return x;
            // #352: obstaculos del techo solo para aliados
            d.SaltarALaAzotea();
            foreach (var x in Esperar(2f)) yield return x;
            W8Ok(ref ok, d.ObstaculosDelTechoSoloParaAliados > 0, sb, $"{d.ObstaculosDelTechoSoloParaAliados} obstaculos del techo dejan pasar a los enemigos");
            foreach (var x in FotoDePose("v4_352_despues.jpg", 318.53f, 24.46f, 271.07f, 41.1f, 63.2f, 0f, 60f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static bool MuroSalidaCaido(OperacionDirector d) => d.MuroDeSalidaCaidoPorPrueba;

        // ---------------------------------------------------------------- 601i inspeccion: que colliders hay cerca de los puntos de los bugs #351 y #352
        static IEnumerator Bug601i()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            // #351: colision invisible junto a la mansion (el jugador estaba en 292.85, 0.8, 273.4 mirando al 259 de yaw)
            Volcar(sb, "351 planta baja", new Vector3(292.85f, 1.0f, 273.4f), 4.5f);
            d.SaltarALaAzotea();
            foreach (var x in Esperar(1.5f)) yield return x;
            // #352: azotea (320.98, 20.3, 272.93)
            Volcar(sb, "352 azotea", new Vector3(320.98f, 21.0f, 272.93f), 7f);
            System.IO.File.WriteAllText("Temp/inspeccion_601.txt", sb.ToString());
            Fin("OK inspeccion volcada en Temp/inspeccion_601.txt (" + sb.Length + " caracteres)");
        }

        static void Volcar(StringBuilder sb, string titulo, Vector3 c, float r)
        {
            sb.AppendLine("=== " + titulo + " @ " + c);
            var cols = Physics.OverlapSphere(c, r, ~0, QueryTriggerInteraction.Collide);
            foreach (var col in cols)
            {
                if (col == null) continue;
                var rend = col.GetComponentInChildren<Renderer>(true);
                bool visible = rend != null && rend.enabled && rend.gameObject.activeInHierarchy;
                var marker = col.GetComponentInParent<SP.Presentation.ObstacleMarker>();
                string ruta = col.name; var t = col.transform.parent; int n = 0; while (t != null && n++ < 4) { ruta = t.name + "/" + ruta; t = t.parent; }
                sb.AppendLine($"{(col.isTrigger ? "TRIG" : "SOLID")} {(visible ? "vis" : "INVISIBLE")} {col.GetType().Name} {ruta} centro={col.bounds.center:0.0} tam={col.bounds.size:0.0} layer={LayerMask.LayerToName(col.gameObject.layer)} marker={(marker != null ? marker.name : "-")}");
            }
        }

        // Color medio de lo que se VE del soldado: se muestrea la textura de su material en el UV de cada vertice de la malla.
        static Color ColorMedio(SkinnedMeshRenderer r)
        {
            if (r == null || r.sharedMaterial == null || r.sharedMesh == null) return Color.black;
            var m = r.sharedMaterial;
            var tex = (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null) as Texture2D;
            Color tinte = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            var uv = r.sharedMesh.uv;
            if (tex == null || !tex.isReadable || uv == null || uv.Length == 0) return tinte;
            float R = 0, G = 0, B = 0;
            foreach (var t in uv) { var c = tex.GetPixelBilinear(t.x, t.y); R += c.r; G += c.g; B += c.b; }
            return new Color(R / uv.Length * tinte.r, G / uv.Length * tinte.g, B / uv.Length * tinte.b);
        }
    }
}
