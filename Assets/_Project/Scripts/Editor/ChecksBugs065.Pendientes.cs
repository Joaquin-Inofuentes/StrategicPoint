using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Vehicles;
using SP.Player;
using SP.UI;
using SP.Presentation;

namespace SP.EditorTools
{
    // Checks de los bugs que habian quedado sin prueba propia (#242 y #251). Un Play FRESCO sobre SC_Operacion cada uno.
    //   242a el que hackea la computadora no se distrae con enemigos y vuelve a su postura al terminar
    //   251a un defensor por cobertura y, sin blanco, se asoma cada tanto
    public static partial class ChecksBugs065
    {
        static T PrivadoDe<T>(object o, string campo)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(campo, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null) return (T)f.GetValue(o);
            }
            return default;
        }

        // ---------------------------------------------------------------- 242a el hacker no se distrae
        static IEnumerator Bug242a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;   // pasado el seguimiento automatico del arranque (1,2 s)
            OperacionTerminal term = null;
            foreach (var t in Object.FindObjectsByType<OperacionTerminal>(FindObjectsInactive.Exclude)) if (!t.modoSostener) { term = t; break; }
            if (term == null) { Fin("FALLO no hay computadora (terminal en modo hacker)"); yield break; }
            var yo = W9Yo(); Soldier op = null;
            foreach (var s in ActorRegistry.All) if (s != null && s != yo && term.PuedeOperar(s) && s.Brain != null) { op = s; break; }
            if (op == null) { Fin("FALLO no hay un aliado que pueda operar"); yield break; }
            var pos = term.transform.position + new Vector3(2f, 0f, 0f); pos.y = op.transform.position.y;
            var ag = op.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) ag.Warp(pos);
            op.transform.position = pos;
            bool pasivoAntes = op.Brain.Pasivo;
            term.Enganchar(op);
            // un enemigo aparece a 14 m, de frente, y le tira
            Soldier en = null;
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == TeamId.Enemy && s.Health.IsAlive) { en = s; break; }
            if (en != null)
            {
                en.gameObject.SetActive(true);
                var pe = pos + new Vector3(0f, 0f, 14f); pe.y = en.transform.position.y;
                var age = en.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (age != null) age.Warp(pe);
                en.transform.position = pe;
            }
            var traza = new List<string>(); var p0 = op.transform.position; bool reacciono = false; float t0 = Time.realtimeSinceStartup;
            bool fotoHack = false;
            while (Time.realtimeSinceStartup - t0 < 4f)
            {
                if (!fotoHack && Time.realtimeSinceStartup - t0 > 2f)
                {
                    fotoHack = true;
                    var dirv = (op.transform.position - term.transform.position); dirv.y = 0f; var lado = Vector3.Cross(Vector3.up, dirv.normalized);
                    var cp = op.transform.position + lado * 6f + Vector3.up * 2.6f + dirv.normalized * 4f;
                    CapturaConHud("v242_hacker_concentrado", cp, Quaternion.LookRotation(op.transform.position + Vector3.up * 1.0f - cp), 55f);
                }
                var st = op.Brain.State;
                if (st == AiState.Attack || st == AiState.Chase || st == AiState.MovingToAttackOrder) reacciono = true;
                if (Time.realtimeSinceStartup - t0 > traza.Count * 0.5f) traza.Add($"{st}/q{(op.Brain.Quieto ? 1 : 0)}/d{Vector3.Distance(p0, op.transform.position):0.0}/poseido{(op.Brain.IsPossessedByPlayer ? 1 : 0)}");
                yield return null;
            }
            W8Ok(ref ok, en != null, sb, "hay un enemigo cerca de la computadora");
            W8Ok(ref ok, op.Brain.Pasivo && op.Brain.Quieto, sb, $"mientras hackea queda concentrado (Pasivo={op.Brain.Pasivo}, Quieto={op.Brain.Quieto})");
            W8Ok(ref ok, !reacciono, sb, $"no pasa a Attack/Chase aunque haya un enemigo a 14 m (estado={op.Brain.State})");
            W8Ok(ref ok, Vector3.Distance(p0, op.transform.position) < 0.6f, sb, $"se queda junto a la computadora (se movio {Vector3.Distance(p0, op.transform.position):0.00} m)");
            sb.Append("[traza " + string.Join(" ", traza) + "] ");
            term.Avanzar(1000f, op);
            foreach (var x in Esperar(0.3f)) yield return x;
            W8Ok(ref ok, term.Completo && op.Brain.Pasivo == pasivoAntes && !op.Brain.Quieto, sb, $"al terminar vuelve a su postura (Pasivo={op.Brain.Pasivo}, Quieto={op.Brain.Quieto})");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static void Cap251(Soldier s, string nombre)
        {
            var dir = s.transform.forward; dir.y = 0f;
            var cp = s.transform.position - dir.normalized * 5.5f + Vector3.up * 1.7f + Vector3.Cross(Vector3.up, dir.normalized) * 2.5f;
            CapturaConHud(nombre, cp, Quaternion.LookRotation(s.transform.position + Vector3.up * 0.9f - cp), 45f);
        }

        // ---------------------------------------------------------------- 251a una cobertura por defensor y asomarse cada tanto
        static IEnumerator Bug251a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5, 1);
            ModoDios.Poner(true);
            // en 40 s reales los defensores llegan a sus coberturas; se anota quien se asoma sin blanco
            var asomo = new HashSet<int>(); var conCobertura = new Dictionary<int, Collider>();
            var puntos = new Dictionary<int, Vector3>();
            float t0 = Time.realtimeSinceStartup; int cuadros = 0, fotos = 0; Soldier quien = null; float fotoT = 0f;
            while (Time.realtimeSinceStartup - t0 < 40f)
            {
                cuadros++;
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Brain == null || !s.Health.IsAlive) continue;
                    if (!s.Brain.EnCobertura || s.Brain.IsPossessedByPlayer) continue;
                    var due = PrivadoDe<Collider>(s.Brain, "coberturaDueno");
                    conCobertura[s.Id] = due; puntos[s.Id] = PrivadoDe<Vector3>(s.Brain, "coberturaPunto");
                    var sub = PrivadoDe<object>(s.Brain, "subStateCobertura");
                    bool sinBlanco = PrivadoDe<Soldier>(s.Brain, "target") == null;
                    if (sinBlanco && sub != null && sub.ToString() == "Peeking")
                    {
                        asomo.Add(s.Id);
                        if (fotos == 0 && Time.realtimeSinceStartup - t0 > 8f) { fotos = 1; quien = s; fotoT = Time.realtimeSinceStartup; Cap251(s, "v251_defensor_asomado"); }
                    }
                    else if (quien == s && fotos == 1 && sub != null && sub.ToString() == "Hidden" && Time.realtimeSinceStartup - fotoT > 0.3f) { fotos = 2; Cap251(s, "v251_defensor_oculto"); }
                }
                yield return null;
            }
            int defensores = conCobertura.Count;
            W8Ok(ref ok, defensores >= 2, sb, $"{defensores} defensores en cobertura");
            // una cobertura por defensor: ningun par comparte el mismo punto de cobertura (a menos de 0,9 m)
            int compartidas = 0; var ids = new List<int>(puntos.Keys);
            for (int i = 0; i < ids.Count; i++) for (int j = i + 1; j < ids.Count; j++)
                if (Vector3.Distance(puntos[ids[i]], puntos[ids[j]]) < 0.9f) compartidas++;
            W8Ok(ref ok, compartidas == 0, sb, $"coberturas compartidas por dos defensores: {compartidas}");
            W8Ok(ref ok, asomo.Count >= 1, sb, $"{asomo.Count} de {defensores} defensores se asomaron a vigilar sin blanco (en 40 s)");
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Render con HUD (los Canvas overlay pasan un instante a ScreenSpaceCamera de la camara temporal): sin foco y sin tocar la ventana.
        static string CapturaConHud(string archivo, Vector3 pos, Quaternion rot, float fov = 60f, int w = 1600, int h = 900)
        {
            var goCam = new GameObject("CamCapturaHud");
            var cam = goCam.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.2f; cam.farClipPlane = 1500f; cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = ~((1 << 5) | (1 << 8));
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var cambiados = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (c.name == "SesionLog_Indicador" || c.name == "CanvasPausa") continue;
                c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; cambiados.Add(c);
            }
            Canvas.ForceUpdateCanvases();
            // los marcadores 3D de objetivo (rombos) se agrandan al acercar la camara de prueba: se apagan en la captura
            var mundoUi = GameObject.Find("UI_World"); bool mundoEstaba = mundoUi != null && mundoUi.activeSelf; if (mundoEstaba) mundoUi.SetActive(false);
            var apagados = new List<Renderer>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>())
            {
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn != "DiamondGizmo" && tn != "PossessedMarkerView" && tn != "InteractableDiamond" && tn != "ObjectiveDiamondMarker" && tn != "UnitLocatorCylinder") continue;
                foreach (var r in mb.GetComponentsInChildren<Renderer>()) if (r != null && r.enabled && !(r is SkinnedMeshRenderer)) { r.enabled = false; apagados.Add(r); }
            }
            cam.Render();
            foreach (var r in apagados) if (r != null) r.enabled = true;
            if (mundoEstaba) mundoUi.SetActive(true);
            foreach (var c in cambiados) if (c != null) c.renderMode = RenderMode.ScreenSpaceOverlay;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes("C:/_Proyectos privados/DV_C6_StrategicPoint/Informes/BUGS_231_255/despues/" + archivo + ".png", tex.EncodeToPNG());
            RenderTexture.active = prev; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(goCam);
            return archivo;
        }

        // ---------------------------------------------------------------- 245/246/247 capturas del artillero: anillo fijo, cruceta, explosion
        static IEnumerator Bug246cap()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            ModoDios.Poner(true);
            foreach (var x in PrepararTanque()) yield return x;
            var d = OperacionDirector.Instancia;
            if (!embarqueOk) { Fin("FALLO el jugador no quedo de artillero"); yield break; }
            var tw = d.tanque.TorretaCanon;
            var motor = d.tanque.GetComponent<VehicleMotor>(); if (motor != null) motor.Configurar(0.5f);
            foreach (var rc in Object.FindObjectsByType<Transform>()) if (rc != null && rc.name.StartsWith("Roca_Canadon_", System.StringComparison.Ordinal)) rc.gameObject.SetActive(false);
            var f = tw.transform.forward; f.y = 0f; f.Normalize();
            var o = tw.Muzzle != null ? tw.Muzzle.position : tw.transform.position;
            var muro = GameObject.CreatePrimitive(PrimitiveType.Cube); muro.name = "Muro_PruebaTorreta";
            muro.transform.localScale = new Vector3(40f, 10f, 1f);
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh != null) muro.GetComponent<Renderer>().sharedMaterial = new Material(sh) { color = new Color(0.55f, 0.58f, 0.62f) };
            for (int i = 0; i < 30; i++)
            {
                muro.transform.SetPositionAndRotation(o + f * 22f + Vector3.up * 0.5f, Quaternion.LookRotation(f));
                Physics.SyncTransforms(); yield return null;
            }
            var cam = CamaraPrincipal.Actual;
            // anillo fijo: se mide la amplitud del radio en 40 cuadros (no debe vibrar) y que haya anillo
            float rMin = 1e9f, rMax = -1e9f; int dib = 0;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if (TurretAimView.UltimoRadioDibujado > 0f) { rMin = Mathf.Min(rMin, TurretAimView.UltimoRadioDibujado); rMax = Mathf.Max(rMax, TurretAimView.UltimoRadioDibujado); dib++; }
            }
            W8Ok(ref ok, dib >= 30, sb, $"anillo de zona dibujado en {dib}/40 cuadros");
            W8Ok(ref ok, rMax - rMin < 0.02f, sb, $"el anillo no vibra (radio {rMin:0.00}..{rMax:0.00} m)");
            W8Ok(ref ok, !MiraDeTorreta.CrucetaVisible && !MiraDeTorreta.RomboVisible, sb, $"sin cruceta ni rombo en pantalla (cruceta={MiraDeTorreta.CrucetaVisible}, rombo={MiraDeTorreta.RomboVisible})");
            CapturaConHud("v245_anillo_fijo_y_cruceta", cam.transform.position, cam.transform.rotation, cam.fieldOfView);
            // disparo: explosion a distintos instantes
            bool disparo = tw.TryFire();
            W8Ok(ref ok, disparo, sb, "el canon dispara");
            float t0 = Time.realtimeSinceStartup; int n = 0;
            while (Time.realtimeSinceStartup - t0 < 0.9f)
            {
                yield return null;
                float t = Time.realtimeSinceStartup - t0;
                if (n == 0 && t > 0.35f) { CapturaConHud("v246_explosion_a", cam.transform.position, cam.transform.rotation, cam.fieldOfView); n++; }
                if (n == 1 && t > 0.6f) { CapturaConHud("v246_explosion_b", cam.transform.position, cam.transform.rotation, cam.fieldOfView); n++; }
            }
            Object.Destroy(muro);
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // Costo del arte agregado (grupo ArteExtra) en las escenas guardadas del Nivel 1 y el Tutorial: abre cada una en aditivo, cuenta y cierra sin guardar.
        public static string MedirArteNiveles()
        {
            var sb = new StringBuilder();
            foreach (var ruta in new[] { "Assets/_Project/Scenes/SC_Gameplay.unity", "Assets/_Project/Scenes/SC_Tutorial.unity" })
            {
                var esc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ruta, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                GameObject arte = null; int renderersEscena = 0; long trisEscena = 0;
                foreach (var r in esc.GetRootGameObjects())
                {
                    if (r.name == "ArteExtra") arte = r;
                    foreach (var mf in r.GetComponentsInChildren<MeshFilter>(true)) { renderersEscena++; if (mf.sharedMesh != null) trisEscena += mf.sharedMesh.triangles.Length / 3; }
                }
                if (arte == null) sb.Append(esc.name + ": SIN ArteExtra | ");
                else
                {
                    int objs = arte.GetComponentsInChildren<Transform>(true).Length, rend = 0, sombra = 0, cols = 0, luces = 0, lucesSombra = 0; long tris = 0;
                    var mats = new HashSet<Material>();
                    foreach (var r in arte.GetComponentsInChildren<Renderer>(true))
                    {
                        rend++; if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) sombra++;
                        foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
                        var mf = r.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                    }
                    cols = arte.GetComponentsInChildren<Collider>(true).Length;
                    foreach (var l in arte.GetComponentsInChildren<Light>(true)) { luces++; if (l.shadows != LightShadows.None) lucesSombra++; }
                    sb.Append($"{esc.name}: ArteExtra {objs} objetos, {rend} renderers ({sombra} con sombra), {tris} tris, {mats.Count} materiales, {cols} colliders, {luces} luces ({lucesSombra} con sombra) | escena entera: {renderersEscena} mallas, {trisEscena} tris | ");
                }
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(esc, true);
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 049rts rombos de aliados en la vista RTS (bug #049 "rombos para aliados y yo")
        static IEnumerator Bug049rts()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5, 1);
            foreach (var x in Esperar(3f)) yield return x;
            var rig = PlayerInputDriver.Activo.Rig;
            W8Ok(ref ok, rig.Mode == SP.CameraSystem.ControlMode.Rts, sb, $"la vista es RTS (modo={rig.Mode})");
            int aliados = 0, conRombo = 0, yoVisible = 0; float escMin = 1e9f, escMax = 0f;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                var loc = s.GetComponent<UnitLocatorCylinder>(); if (loc == null) continue;
                aliados++;
                if (loc.MarcadorVisible)
                {
                    conRombo++; escMin = Mathf.Min(escMin, loc.EscalaActual); escMax = Mathf.Max(escMax, loc.EscalaActual);
                }
            }
            var yo = W9Yo();
            var locYo = yo != null ? yo.GetComponent<UnitLocatorCylinder>() : null;
            yoVisible = locYo != null && locYo.MarcadorVisible ? 1 : 0;
            sb.Append($"[yo={(yo != null ? yo.DisplayName : "ninguno")} poseido={(yo != null && yo.Brain != null && yo.Brain.IsPossessedByPlayer)}] ");
            W8Ok(ref ok, aliados >= 3, sb, $"hay {aliados} aliados vivos");
            W8Ok(ref ok, conRombo == aliados, sb, $"rombo visible en {conRombo} de {aliados} aliados en RTS");
            W8Ok(ref ok, yoVisible == 1, sb, $"el poseido tambien tiene rombo en RTS ({yoVisible})");
            W8Ok(ref ok, escMax > 0.5f, sb, $"escala de los rombos {escMin:0.00}..{escMax:0.00}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 241 captura del destello del francotirador
        static IEnumerator Bug241cap()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1.2f)) yield return x;
            Soldier f = B231Aliado("Soldado_2_Kes");
            if (f == null) { Fin("FALLO no hay Kes"); yield break; }
            var dir = f.transform.forward; dir.y = 0f; dir.Normalize();
            var boca = f.transform.position + Vector3.up * 1.4f + dir * 0.8f;
            var der = Vector3.Cross(Vector3.up, dir);
            var camPos = boca + der * 3.2f - dir * 2.2f + Vector3.up * 0.4f;
            var rot = Quaternion.LookRotation((boca + dir * 4f) - camPos);
            int antes = SniperFx.Disparos;
            SniperFx.Disparo(boca, dir, false, true);
            W8Ok(ref ok, SniperFx.Disparos == antes + 1, sb, "el disparo de francotirador se registra");
            yield return null; yield return null;
            CapturaConHud("v241_destello_a", camPos, rot, 55f);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.12f) yield return null;
            CapturaConHud("v241_destello_b", camPos, rot, 55f);
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 232a el cuerpo no se deforma entre los escombros de la torreta
        static IEnumerator Bug232a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(1.2f)) yield return x;
            var cargas = Object.FindObjectsByType<CargaExplosiva>(FindObjectsInactive.Exclude);
            var kes = B231Aliado("Soldado_2_Kes");
            if (kes == null || cargas.Length == 0) { Fin("FALLO no hay Kes o cargas"); yield break; }
            // se vuela la artilleria y el aliado se para ENTRE los escombros, donde se vio el cuerpo roto
            foreach (var c in cargas) c.VolarYa();
            foreach (var x in Esperar(2.5f)) yield return x;
            var centro = cargas[0].transform.position;
            var ag = kes.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var destino = centro + new Vector3(2.5f, 0f, 2.5f);
            if (UnityEngine.AI.NavMesh.SamplePosition(destino, out var nh, 8f, UnityEngine.AI.NavMesh.AllAreas)) destino = nh.position;
            if (ag != null) ag.Warp(destino); kes.transform.position = destino;
            float peorInclinacion = 0f, minY = 99f, minAltura = 99f, maxEscala = 0f;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 6f)
            {
                yield return null;
                peorInclinacion = Mathf.Max(peorInclinacion, Vector3.Angle(kes.transform.up, Vector3.up));
                maxEscala = Mathf.Max(maxEscala, Mathf.Max(kes.transform.lossyScale.x, Mathf.Max(kes.transform.lossyScale.y, kes.transform.lossyScale.z)));
                var smr = kes.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) { minY = Mathf.Min(minY, MallaMinima(kes, out _)); minAltura = Mathf.Min(minAltura, smr.bounds.size.y); }
            }
            W8Ok(ref ok, peorInclinacion < 6f, sb, $"el cuerpo no se inclina entre los escombros (peor {peorInclinacion:0.0} grados)");
            W8Ok(ref ok, maxEscala < 1.15f, sb, $"la escala no se deforma (max {maxEscala:0.00})");
            W8Ok(ref ok, minY > -0.12f, sb, $"la malla no se hunde (min {minY:0.00} m bajo el piso)");
            W8Ok(ref ok, minAltura > 1.2f, sb, $"el cuerpo conserva su altura (min {minAltura:0.00} m)");
            var cam = CamaraPrincipal.Actual;
            var pos = kes.transform.position + new Vector3(0f, 2.2f, -3.2f);
            CapturaConHud("v232_cuerpo_entre_escombros", pos, Quaternion.LookRotation(kes.transform.position + Vector3.up * 1.0f - pos), 55f);
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static float PisoBajo(Soldier s)
        {
            var o = s.transform.position + Vector3.up * 2f; float mejor = float.NaN;
            foreach (var h in Physics.RaycastAll(o, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(s.transform)) continue;
                if (float.IsNaN(mejor) || h.point.y > mejor) mejor = h.point.y;
            }
            return mejor;
        }

        // ---------------------------------------------------------------- 232b los pies no se hunden ni de pie ni agachado ni caminando
        static IEnumerator Bug232b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || !s.Health.IsAlive || s.Motor == null) continue;
                var smr = s.GetComponentInChildren<SkinnedMeshRenderer>(); float piso = PisoBajo(s);
                if (smr == null || float.IsNaN(piso)) continue;
                s.Motor.SetCrouching(false); foreach (var x in Esperar(0.7f)) yield return x;
                float depie = smr.bounds.min.y - PisoBajo(s);
                s.Motor.SetCrouching(true); foreach (var x in Esperar(0.7f)) yield return x;
                float agachado = smr.bounds.min.y - PisoBajo(s);
                s.Motor.SetCrouching(false);
                W8Ok(ref ok, depie > -0.08f && agachado > -0.08f, sb, $"{s.DisplayName}: pies a {depie:0.00} m (de pie) y {agachado:0.00} m (agachado) del piso");
            }
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- rombos de aliados vistos con la camara REAL del juego (RTS y FPS)
        static IEnumerator Bug251d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(4f)) yield return x;
            var cam = CamaraPrincipal.Actual;
            CapturaConHud("rombos_rts_camara_real", cam.transform.position, cam.transform.rotation, cam.fieldOfView);
            var rig = PlayerInputDriver.Activo.Rig;
            rig.ToggleMode();
            foreach (var x in Esperar(2.5f)) yield return x;
            cam = CamaraPrincipal.Actual;
            CapturaConHud("rombos_fps_camara_real", cam.transform.position, cam.transform.rotation, cam.fieldOfView);
            ModoDios.Poner(false);
            Fin("OK modo=" + rig.Mode);
        }
    }
}
