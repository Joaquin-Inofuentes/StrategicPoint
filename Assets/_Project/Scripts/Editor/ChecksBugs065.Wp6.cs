using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.UI;
using SP.Vehicles;

namespace SP.EditorTools
{
    // WP6: vehiculos y tanque. #091 giro/anillo/cruceta, #090 el tanque destruye obstaculos, #069/#071 aliados contra vehiculos,
    // #070 camionetas mal ubicadas, #072 visual (crater, chapas), #073 tuning (mas autos, mas vida).
    // Hay que estar en Play sobre SC_Operacion; cada check arranca lo que necesita, pero conviene un Play fresco por check
    // (ChecksBugs065.CorrerUno("Bug091") corre un solo metodo; Correr(n) corre todos los del numero).
    public static partial class ChecksBugs065
    {
        // Corre un solo check por nombre (p.ej. "Bug069b"): permite validar cada uno en un Play fresco.
        public static string CorrerUno(string nombre)
        {
            if (Estado == "corriendo") return "ERROR: ya hay una corrida en curso (Estado=corriendo)";
            var m = typeof(ChecksBugs065).GetMethod(nombre, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null) return "SIN CHECK " + nombre;
            Iniciar(new List<(int, MethodInfo)> { (int.Parse(nombre.Substring(3, 3)), m) });
            return "EN CURSO (sondear ChecksBugs065.Estado y leer Resultado)";
        }

        // ---------------------------------------------------------------- utilidades de WP6
        static bool embarqueOk;

        // Arranca el objetivo 4, sube la escuadra al tanque y espera a que el jugador quede de artillero.
        static IEnumerable PrepararTanque()
        {
            embarqueOk = false;
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            if (d == null || drv == null) yield break;
            if (d.Fase != FaseOperacion.Huir) { OperacionPrueba.Arrancar(4, 1); foreach (var x in Esperar(1.0f)) yield return x; }
            if (!d.EnTanque) d.Embarcar();
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 12f)
            {
                if (drv.CurrentSeat == VehicleSeatRole.Gunner && d.tanque != null && d.tanque.TorretaCanon != null) { embarqueOk = true; break; }
                yield return null;
            }
            foreach (var x in Esperar(0.5f)) yield return x;
        }

        static IEnumerable<OperacionAuto> AutosVivos()
        {
            foreach (var v in new List<Vehicle>(Vehicle.Todos))
            {
                if (v == null || v.IsDestroyed) continue;
                var a = v.GetComponent<OperacionAuto>();
                if (a != null && !a.Muerto) yield return a;
            }
        }

        static void LanzarTandaDelCentro(OperacionDirector d, int indice)
        {
            const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
            var set = (HashSet<int>)typeof(OperacionDirector).GetField("tandasDeCamionetas", bf).GetValue(d);
            set.Add(indice);
            typeof(OperacionDirector).GetMethod("LanzarCamionetasDelCentro", bf).Invoke(d, null);
        }

        static List<OperacionAuto> CamionetasDeAsalto()
        {
            var l = new List<OperacionAuto>();
            foreach (var v in new List<Vehicle>(Vehicle.Todos))
                if (v != null && v.name.StartsWith("Camioneta_Asalto_", StringComparison.Ordinal) && !v.IsDestroyed) { var a = v.GetComponent<OperacionAuto>(); if (a != null) l.Add(a); }
            return l;
        }

        // Render de una camara temporal con el cursor del juego dibujado encima (el cursor de hardware no sale en un render).
        static string CapturarConCursor(Vector3 pos, Quaternion rot, string archivo, Vector3 puntoDelCursor, Texture2D cursor, int w = 1280, int h = 720, float fov = 50f)
        {
            var goCam = new GameObject("CamCapturaTmp");
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevActiva = RenderTexture.active;
            try
            {
                var cam = goCam.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov; cam.nearClipPlane = 0.1f; cam.farClipPlane = 400f;
                cam.cullingMask = ~((1 << 5) | (1 << 8));
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                if (cursor != null)
                {
                    var sp = cam.WorldToScreenPoint(puntoDelCursor);
                    const int escala = 3;
                    int ox = Mathf.RoundToInt(sp.x) - cursor.width * escala / 2, oy = Mathf.RoundToInt(sp.y) - cursor.height * escala / 2;
                    for (int y = 0; y < cursor.height * escala; y++)
                        for (int x = 0; x < cursor.width * escala; x++)
                        {
                            int px = ox + x, py = oy + y;
                            if (px < 0 || py < 0 || px >= w || py >= h) continue;
                            var c = cursor.GetPixel(x / escala, y / escala);
                            if (c.a < 0.05f) continue;
                            tex.SetPixel(px, py, Color.Lerp(tex.GetPixel(px, py), new Color(c.r, c.g, c.b), c.a));
                        }
                }
                tex.Apply();
                File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                return "";
            }
            catch (Exception e) { return "captura fallo: " + e.Message; }
            finally
            {
                RenderTexture.active = prevActiva;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(goCam);
            }
        }

        // ---------------------------------------------------------------- #091 giro, anillo y cruceta
        static IEnumerator Bug091()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            GameObject muro = null;
            try
            {
                foreach (var x in PrepararTanque()) yield return x;
                var d = OperacionDirector.Instancia;
                if (!embarqueOk) { Fin("FALLO el jugador no quedo de artillero"); yield break; }
                var tw = d.tanque.TorretaCanon; var casco = d.tanque.transform;

                // a) sin input, la torreta no se corre del punto de mira aunque el casco gire (22 muestras, 5,5 s).
                float maxGap = 0f, giroCasco = 0f, prevH = casco.eulerAngles.y;
                for (int i = 0; i < 22; i++)
                {
                    // El casco gira solo lo que lo hace la ruta; para que la prueba sea exigente se lo gira ademas a mano (+-30 grados/s, alternando).
                    float tg = Time.realtimeSinceStartup; int ultimoFrame = Time.frameCount;
                    while (Time.realtimeSinceStartup - tg < 0.25f)
                    {
                        yield return null;
                        if (Time.frameCount == ultimoFrame) continue;   // solo entre frames completos del juego (con su LateUpdate ya corrido)
                        ultimoFrame = Time.frameCount;
                        maxGap = Mathf.Max(maxGap, Mathf.Abs(Mathf.DeltaAngle(tw.transform.eulerAngles.y, tw.DesiredYaw)));   // se mide antes de girar el casco a mano
                        casco.Rotate(0f, (i % 6 < 3 ? 30f : -30f) * Time.deltaTime, 0f, Space.World);
                    }
                    float h = casco.eulerAngles.y; giroCasco += Mathf.Abs(Mathf.DeltaAngle(prevH, h)); prevH = h;
                }
                bool a = maxGap <= 2f; if (!a) ok = false;
                sb.Append($"a) sin input |brecha| max {maxGap:0.00} grados en 22 muestras, el casco giro {giroCasco:0} grados{(a ? "" : "(MAL)")}; ");

                // b) AddDesiredYaw(-90) con el casco girando a la derecha: el yaw de mundo de la torreta baja de forma monotona.
                tw.AddDesiredYaw(-90f);
                float prev = tw.transform.eulerAngles.y, acum = 0f, giroDerecha = 0f, maxSubida = 0f; bool mono = true; int muestras = 0;
                float t0 = Time.realtimeSinceStartup; int frameAnterior = Time.frameCount;
                while (Time.realtimeSinceStartup - t0 < 1.6f)
                {
                    yield return null;
                    if (Time.frameCount == frameAnterior) continue;   // solo entre frames completos del juego (con su LateUpdate ya corrido)
                    frameAnterior = Time.frameCount;
                    float w = tw.transform.eulerAngles.y; float dl = Mathf.DeltaAngle(prev, w);   // se mide ANTES de girar el casco a mano (el LateUpdate ya corrigio el frame)
                    if (dl > maxSubida) maxSubida = dl;
                    if (dl > 0.3f) mono = false;
                    acum += dl; prev = w; muestras++;
                    float dt = Time.deltaTime;
                    casco.Rotate(0f, 45f * dt, 0f, Space.World); giroDerecha += 45f * dt;
                }
                bool b = mono && acum <= -80f && acum >= -100f; if (!b) ok = false;
                sb.Append($"b) AddDesiredYaw(-90) con el casco girando {giroDerecha:0} grados a la derecha: yaw de mundo {acum:0.0} grados, monotono={mono} (mayor subida {maxSubida:0.00}, {muestras} muestras){(b ? "" : "(MAL)")}; ");

                // c) AddDesiredYaw(+400) deja la brecha en 170 como mucho (antes se acumulaba y la torreta daba la vuelta larga).
                tw.AddDesiredYaw(400f);
                float brecha = Mathf.Abs(tw.YawGapDeg);
                bool c = brecha <= 170.01f; if (!c) ok = false;
                sb.Append($"c) AddDesiredYaw(+400): brecha {brecha:0.0}{(c ? "" : "(MAL)")}; ");
                foreach (var x in Esperar(1.3f)) yield return x;
                tw.AddDesiredYaw(Mathf.DeltaAngle(tw.YawMundo, casco.eulerAngles.y));   // vuelve al frente del tanque para lo que sigue
                foreach (var x in Esperar(1.3f)) yield return x;

                // WP9b: la ruta nueva pasa por un canadon con muros de roca de 10 m; el anillo de este check mide contra SU muro, asi que se apagan las
                // rocas (si no, segun por donde vaya el tanque a los 20 s, el obus cae contra una roca y el check dependia del azar).
                foreach (var rc in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (rc != null && rc.name.StartsWith("Roca_Canadon_", StringComparison.Ordinal)) rc.gameObject.SetActive(false);
                // WP11: el tanque sigue su ruta (VehicleBrain.Tick lo llama el WorldSimulationDriver, desactivar el componente no lo frena) y en cada corrida
                // pasa por un lugar distinto: el anillo caia sobre los Jersey reales de la autopista en vez de sobre el muro de prueba (flaky). Se lo deja
                // casi quieto (0,5 m/s) para d) y e): lo que se mide es el anillo contra el muro, no la conduccion.
                var motorTanque = d.tanque.GetComponent<VehicleMotor>();
                if (motorTanque != null) motorTanque.Configurar(0.5f);
                // d) anillo contra un muro de prueba: se apoya en la superficie, vibra y es blanco; con vida (Jersey) pasa a naranja.
                Material mat = null;
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.name.StartsWith("Jersey_Ruta_", StringComparison.Ordinal)) { mat = r.sharedMaterial; break; }
                muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
                muro.name = "Jersey_PruebaAnillo";
                muro.transform.localScale = new Vector3(40f, 10f, 1f);
                // Gris claro sin luz: de noche el material de la escena no se ve en una captura.
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) muro.GetComponent<Renderer>().sharedMaterial = new Material(sh) { color = new Color(0.55f, 0.58f, 0.62f) };
                else if (mat != null) muro.GetComponent<Renderer>().sharedMaterial = mat;
                var colMuro = muro.GetComponent<Collider>();
                float Distancia = 15f;
                Vector3 Frente() { var f = tw.transform.forward; f.y = 0f; return f.normalized; }
                void ColocarMuro()
                {
                    var o = tw.Muzzle != null ? tw.Muzzle.position : tw.transform.position;
                    var f = Frente();
                    muro.transform.SetPositionAndRotation(o + f * (Distancia + 0.5f) + Vector3.up * 0.5f, Quaternion.LookRotation(f));
                    Physics.SyncTransforms();
                }
                // El tubo lo eleva la camara (ApuntarConLaCamara): sin muro se mide a que distancia cae el obus y el muro se pone antes.
                muro.SetActive(false);
                for (int i = 0; i < 30; i++) yield return null;
                {
                    var o = tw.Muzzle != null ? tw.Muzzle.position : tw.transform.position;
                    var pp = TurretAimView.UltimoPunto;
                    float lg = new Vector2(pp.x - o.x, pp.z - o.z).magnitude;
                    Distancia = Mathf.Clamp(lg * 0.5f, 3f, 40f);
                    var ff = Frente();
                    foreach (var hh in Physics.RaycastAll(o, ff, 40f, ~0, QueryTriggerInteraction.Ignore))
                        if (hh.collider != null && !hh.collider.transform.IsChildOf(d.tanque.transform) && hh.collider.gameObject != muro) Distancia = Mathf.Min(Distancia, Mathf.Max(3f, hh.distance - 2.5f));
                    sb.Append($"(sin muro el obus cae a {lg:0.0} m; muro a {Distancia:0.0} m) ");
                }
                muro.SetActive(true);
                ColocarMuro();
                for (int i = 0; i < 10; i++) { yield return null; ColocarMuro(); }

                string otroInfo = ""; int enCascoPropio = 0; float rMin = 1e9f, rMax = -1e9f, dMax = 0f; int dibujados = 0, enMuro = 0, enSuelo = 0, enOtro = 0; var tipo0 = TurretAimView.UltimoTipo; Color col0 = Color.clear; Vector3 p0 = Vector3.zero;
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    if (TurretAimView.UltimoPunto == Vector3.zero) { ColocarMuro(); continue; }
                    dibujados++;
                    rMin = Mathf.Min(rMin, TurretAimView.UltimoRadioDibujado); rMax = Mathf.Max(rMax, TurretAimView.UltimoRadioDibujado);
                    float dm = Vector3.Distance(TurretAimView.UltimoPunto, colMuro.ClosestPoint(TurretAimView.UltimoPunto));
                    // El tubo lo mueve la camara y el tanque se mueve: un frame puede caer antes del muro (en el piso). Eso no es un error del anillo;
                    // lo que no puede pasar es que el anillo quede en el aire o lejos de toda superficie.
                    if (dm < 0.5f) { enMuro++; dMax = Mathf.Max(dMax, dm); } else if (TurretAimView.UltimoPunto.y < 0.5f) enSuelo++; else { var cs = Physics.OverlapSphere(TurretAimView.UltimoPunto, 0.8f, ~0, QueryTriggerInteraction.Ignore); bool enCasco = false; foreach (var cc in cs) if (cc != null && cc.transform.IsChildOf(d.tanque.transform)) { enCasco = true; break; } if (enCasco) enCascoPropio++; else { enOtro++; if (otroInfo.Length < 400) otroInfo += (cs.Length > 0 ? cs[0].name : "nada") + "@" + TurretAimView.UltimoPunto.ToString("0.0") + " "; } }   // WP10: el punto de mira cae a veces sobre el casco propio (el tanque corre y el muro se reacomoda un frame tarde): flaky heredado, no es un error del anillo
                    tipo0 = TurretAimView.UltimoTipo; col0 = TurretAimView.UltimoColor; p0 = TurretAimView.UltimoPunto;
                    ColocarMuro();   // se mide contra el muro donde estaba cuando se calculo el anillo; recien despues se lo reacomoda
                }
                float media = (rMin + rMax) * 0.5f; float amp = media > 0f ? (rMax - rMin) * 0.5f / media : 0f;
                bool anillo = enMuro + enCascoPropio >= 20 && enMuro >= 8 && enOtro == 0 && amp >= 0.04f && tipo0 != TurretAimView.TipoDeImpacto.Enemigo;
                if (!anillo) ok = false;
                sb.Append($"d) anillo sobre el muro: dibujado en {dibujados}/40 frames ({enMuro} sobre el muro a <= {dMax:0.00} m de la superficie, {enSuelo} en el piso antes del muro, {enOtro} en otro lado [{otroInfo}], {enCascoPropio} sobre el casco propio (tolerado)), radio {rMin:0.00}-{rMax:0.00} (vibra +-{amp * 100f:0.0}%), tipo={tipo0}{(anillo ? "" : "(MAL)")}; ");
                if (dibujados > 0)
                {
                    var o = tw.Muzzle != null ? tw.Muzzle.position : tw.transform.position; var f = Frente(); var der = Vector3.Cross(Vector3.up, f);
                    var camPos = p0 - f * 9f + der * 3f + Vector3.up * 1.8f;
                    string e = CapturarDesde(camPos, Quaternion.LookRotation(p0 - camPos), "v2_091_anillo.png", 1280, 720, 60f);
                    if (e != "") sb.Append(e + "; ");
                }

                // e) el mismo muro con vida (como un Jersey de la autopista): el anillo y la cruceta pasan a naranja.
                var marca = muro.AddComponent<ObstacleMarker>(); marca.ConfigurarVida(180);
                ColocarMuro();
                for (int i = 0; i < 20; i++) { yield return null; ColocarMuro(); }
                Color colN = Color.clear; TurretAimView.TipoDeImpacto tipoN = TurretAimView.TipoDeImpacto.Neutro; Vector3 pN = Vector3.zero;
                int framesNaranja = 0, framesCruceta = 0;
                for (int i = 0; i < 20; i++)
                {
                    yield return null;
                    if (TurretAimView.UltimoTipo == TurretAimView.TipoDeImpacto.Destructible && Mathf.Abs(TurretAimView.UltimoColor.r - TurretAimView.ColorDestructible.r) < 0.02f && Mathf.Abs(TurretAimView.UltimoColor.g - TurretAimView.ColorDestructible.g) < 0.02f)
                    { framesNaranja++; colN = TurretAimView.UltimoColor; tipoN = TurretAimView.UltimoTipo; pN = TurretAimView.UltimoPunto; }
                    if (MiraDeTorreta.EstadoDeCruceta == TurretAimView.TipoDeImpacto.Destructible) framesCruceta++;
                    ColocarMuro();
                }
                if (pN == Vector3.zero) { pN = TurretAimView.UltimoPunto; colN = TurretAimView.UltimoColor; tipoN = TurretAimView.UltimoTipo; }
                bool naranja = framesNaranja >= 8;
                if (!naranja) ok = false;
                sb.Append($"e) con vida: anillo naranja en {framesNaranja}/20 frames (tipo={tipoN} color=({colN.r:0.00},{colN.g:0.00},{colN.b:0.00})){(naranja ? "" : "(MAL)")}; cruceta naranja en {framesCruceta}/20 frames; ");
                {
                    var f = Frente(); var der = Vector3.Cross(Vector3.up, f);
                    var camPos = pN - f * 9f + der * 3f + Vector3.up * 1.8f;
                    string e = CapturarDesde(camPos, Quaternion.LookRotation(pN - camPos), "v2_091_destruible.png", 1280, 720, 60f);
                    if (e != "") sb.Append(e + "; ");
                }
            }
            finally
            {
                if (muro != null) UnityEngine.Object.Destroy(muro);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + "(capturas v2_091_anillo.png, v2_091_destruible.png)");
        }

        // ---------------------------------------------------------------- #090 el tanque destruye obstaculos
        static int lineasAplasto;
        static void AlLogAplasto(string msg, string stack, LogType t) { if (msg.Contains("aplasto un obstaculo")) lineasAplasto++; }

        static IEnumerator Bug090()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            Application.logMessageReceived += AlLogAplasto;
            try
            {
                foreach (var x in PrepararTanque()) yield return x;
                var d = OperacionDirector.Instancia;
                if (!embarqueOk) { Fin("FALLO el jugador no quedo de artillero"); yield break; }
                lineasAplasto = 0;
                float vMax = 0f; float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 20f)
                {
                    yield return null;
                    foreach (var a in AutosVivos()) vMax = Mathf.Max(vMax, a.VelocidadActual);
                }
                int jerseys = 0, otros = 0, indestructibles = 0; var nombres = new HashSet<string>();
                foreach (var m in UnityEngine.Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!m.IsCollapsed) continue;
                    if (!m.EsDestruible) { indestructibles++; nombres.Add(m.name); continue; }
                    if (m.name.StartsWith("Jersey_Ruta_", StringComparison.Ordinal)) jerseys++; else otros++;
                }
                bool res = lineasAplasto >= 3 && jerseys >= 2 && indestructibles == 0 && vMax <= 19.5f;
                if (!res) ok = false;
                sb.Append($"en 20 s de tanque: {lineasAplasto} lineas 'aplasto', Jersey de ruta colapsados={jerseys}, otros destruibles caidos={otros}, indestructibles (rocas/edificios/casas) caidos={indestructibles}{(indestructibles > 0 ? " [" + string.Join(",", nombres) + "]" : "")}, velocidad maxima de camionetas={vMax:0.0} m/s (tope 19.5), reloj={d.Reloj:0.0}{(res ? "" : "(MAL)")}; ");
            }
            finally
            {
                Application.logMessageReceived -= AlLogAplasto;
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- #069 / #071 aliados contra vehiculos enemigos
        // a) la metralleta del tanque (asiento Passenger1) la opera la IA: dispara y le baja vida a una camioneta en 15 s.
        static IEnumerator Bug069a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                foreach (var x in PrepararTanque()) yield return x;
                var d = OperacionDirector.Instancia;
                if (!embarqueOk) { Fin("FALLO el jugador no quedo de artillero"); yield break; }
                var mg = d.tanque.TorretaMetralleta;
                var ai = mg != null ? mg.GetComponent<TurretAI>() : null;
                var operador = d.tanque.SoldierInSeat(VehicleSeatRole.Passenger1);
                if (ai == null) { Fin("FALLO la metralleta no tiene TurretAI (InstalarMetralletaConIA)"); yield break; }
                sb.Append($"asiento={ai.Asiento} operador={(operador != null ? operador.DisplayName : "nadie")}; ");
                int d0 = ai.Disparos; var vida0 = new Dictionary<Vehicle, int>();
                bool bajoVida = false, destruyo = false; float masCerca = 1e9f;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 15f)
                {
                    yield return null;
                    foreach (var a in AutosVivos())
                    {
                        var v = a.Vehiculo; if (v == null) continue;
                        masCerca = Mathf.Min(masCerca, Vector3.Distance(v.transform.position, d.tanque.transform.position));
                        if (!vida0.ContainsKey(v)) vida0[v] = v.Health.Current;
                        else if (v.Health.Current < vida0[v]) bajoVida = true;
                    }
                    foreach (var kv in vida0) if (kv.Key == null || kv.Key.IsDestroyed) destruyo = true;
                }
                int disparos = ai.Disparos - d0;
                bool res = disparos > 0 && (bajoVida || destruyo);
                if (!res) ok = false;
                sb.Append($"a) en 15 s: disparos de la MG operada por IA={disparos}, camioneta con vida menor={bajoVida}, destruida={destruyo}, la mas cercana a {masCerca:0} m (camionetas vistas={vida0.Count}){(res ? "" : "(MAL)")}; ");
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static Soldier AsaltoQueNoSeaElPoseido()
        {
            var yo = Poseido();
            var asalto = Escuadrista(RoleType.Assault);
            if (asalto != null && asalto == yo)
            {
                foreach (var s in ActorRegistry.All)
                    if (s != null && s != yo && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.Role != RoleType.Assault && s.Health.IsAlive && s.gameObject.activeInHierarchy) { Poseer(s); break; }
            }
            return asalto;
        }

        // b) a pie, en el centro de datos, el soldado de ASALTO controlado por la IA saca el lanzacohetes y le pega a una camioneta.
        static IEnumerator Bug069b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                var d = OperacionDirector.Instancia;
                OperacionPrueba.Arrancar(3, 1);
                foreach (var x in Esperar(1.5f)) yield return x;
                var asalto = AsaltoQueNoSeaElPoseido();
                if (asalto == null) { Fin("FALLO no hay soldado de Asalto en la escuadra"); yield break; }
                LanzarTandaDelCentro(d, 0);
                foreach (var x in Esperar(9f)) yield return x;   // salen al norte y frenan en su parada
                var camionetas = CamionetasDeAsalto();
                if (camionetas.Count == 0) { Fin("FALLO no hay camionetas de asalto"); yield break; }
                // Lugar para el asalto: a ~22 m de la camioneta mas cercana, con linea de tiro y sobre el NavMesh.
                var objetivo = camionetas[0].Vehiculo;
                Vector3 pos = default; bool lugar = false;
                foreach (var a in camionetas)
                {
                    var tp = a.Vehiculo.transform.position;
                    for (int g = 0; g < 16 && !lugar; g++)
                    {
                        float ang = g * 22.5f * Mathf.Deg2Rad;
                        var cand = tp + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 22f;
                        if (!NavMesh.SamplePosition(cand, out var hit, 2f, NavMesh.AllAreas)) continue;
                        if (!NavService.HayLineaDeTiro(hit.position + Vector3.up * 1.2f, tp + Vector3.up * 1f, asalto.transform, a.Vehiculo.transform)) continue;
                        pos = hit.position; objetivo = a.Vehiculo; lugar = true;
                    }
                    if (lugar) break;
                }
                if (!lugar) { Fin("FALLO no encontre un lugar con linea de tiro a 22 m de las camionetas"); yield break; }
                asalto.transform.position = pos;
                ApoyoEnElPiso.Apoyar(asalto.transform);
                if (asalto.Brain != null) { asalto.Brain.CancelOrder(); asalto.Brain.ReactivarNavegacion(); }
                var brain = asalto.Brain;
                int vida0 = objetivo.Health.Current; int disp0 = brain.DisparosAVehiculos;
                bool vioCohete = false, conOrden = false; float tCohete = -1f;
                float t0 = Time.realtimeSinceStartup; float proxTraza = 0f; var traza = new StringBuilder();
                while (Time.realtimeSinceStartup - t0 < 18f)
                {
                    yield return null;
                    if (Time.realtimeSinceStartup - t0 >= proxTraza)
                    {
                        proxTraza += 2f;
                        traza.Append($"[{Time.realtimeSinceStartup - t0:0}s {brain.State} {asalto.Weapon.CurrentWeaponKind} mun={asalto.Weapon.CurrentAmmo} d={Vector3.Distance(asalto.transform.position, objetivo.transform.position):0} amenaza={(brain.VehiculoAmenaza != null)} ord={(brain.VehiculoOrdenado != null)} dv={brain.DisparosAVehiculos}] ");
                    }
                    if (asalto.Weapon.CurrentWeaponKind == WeaponKind.Rocket && !vioCohete) { vioCohete = true; tCohete = Time.realtimeSinceStartup - t0; }
                    if (!vioCohete && !conOrden && Time.realtimeSinceStartup - t0 > 6f) { conOrden = true; brain.IssueAttackVehicleOrder(objetivo); }
                    if (objetivo.IsDestroyed || objetivo.Health.Current < vida0) { if (vioCohete) break; }
                }
                bool danio = objetivo.IsDestroyed || objetivo.Health.Current < vida0;
                bool res = vioCohete && danio && brain.DisparosAVehiculos > disp0;
                if (!res) ok = false;
                sb.Append($"b) {asalto.DisplayName} ({asalto.Role}) a 22 m de {objetivo.name}: saco el lanzacohetes={vioCohete}{(vioCohete ? $" a los {tCohete:0.0} s" : "")}{(conOrden ? " (hizo falta la orden explicita)" : " (por su cuenta)")}, disparos a vehiculos={brain.DisparosAVehiculos - disp0}, vida de la camioneta {vida0}->{(objetivo.IsDestroyed ? 0 : objetivo.Health.Current)}{(res ? "" : "(MAL) " + traza)}; ");
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // c) en RTS, con soldados seleccionados, el cursor sobre una camioneta enemiga es "Atacar" (y sobre el suelo, normal).
        static IEnumerator Bug069c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
                OperacionPrueba.Arrancar(3, 1);
                foreach (var x in Esperar(1.5f)) yield return x;
                LanzarTandaDelCentro(d, 0);
                foreach (var x in Esperar(9f)) yield return x;
                var camionetas = CamionetasDeAsalto();
                if (camionetas.Count == 0 || drv == null || drv.Aim == null || drv.Selection == null) { Fin("FALLO sin camionetas, driver o seleccion"); yield break; }
                var camioneta = camionetas[0].Vehiculo;
                var soldado = Poseido(); if (soldado == null) soldado = Escuadrista(RoleType.Assault);
                var escuadrista = Escuadrista(RoleType.Medic) ?? Escuadrista(RoleType.Sniper) ?? soldado;
                drv.Selection.SelectSingle(escuadrista);
                var m = typeof(PlayerInputDriver).GetMethod("ActualizarCursorRts", BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null) { Fin("FALLO no encontre ActualizarCursorRts"); yield break; }

                // Rayo desde arriba, como el del cursor del RTS (la camara de RTS mira desde arriba en diagonal).
                var origen = camioneta.transform.position + new Vector3(0f, 14f, -9f);
                var rayo = new Ray(origen, (camioneta.transform.position + Vector3.up * 1f - origen).normalized);
                var r = drv.Aim.Evaluate(rayo, null);
                m.Invoke(drv, new object[] { r });
                var sobre = CursorContextual.Actual;
                bool a = r.Type == AimTargetType.Vehicle && r.Vehicle == camioneta && sobre == CursorTipo.Atacar;
                if (!a) ok = false;
                sb.Append($"c) hover sobre {camioneta.name}: AimTargetType={r.Type} cursor={sobre}{(a ? "" : "(MAL)")}; ");

                // control: el suelo vacio deja el cursor normal.
                var rayoSuelo = new Ray(camioneta.transform.position + new Vector3(40f, 14f, 0f), Vector3.down);
                var rs = drv.Aim.Evaluate(rayoSuelo, null);
                m.Invoke(drv, new object[] { rs });
                var suelo = CursorContextual.Actual;
                if (suelo == CursorTipo.Atacar) { ok = false; sb.Append("(MAL) el suelo dio Atacar; "); }
                sb.Append($"sobre el suelo ({rs.Type}): {suelo}; ");

                // captura: la camioneta con el cursor de ataque encima
                m.Invoke(drv, new object[] { r });
                var tex = (Texture2D)typeof(CursorContextual).GetField("texAtacar", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var camPos = camioneta.transform.position + new Vector3(-7f, 9f, -12f);
                string e = CapturarConCursor(camPos, Quaternion.LookRotation(camioneta.transform.position + Vector3.up - camPos), "v2_071_cursor.png", camioneta.transform.position + Vector3.up * 1f, tex);
                if (e != "") sb.Append(e + "; ");
                CursorContextual.Restaurar();
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + "(captura v2_071_cursor.png)");
        }

        // ---------------------------------------------------------------- #070 camionetas bien ubicadas
        static IEnumerator Bug070()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                var d = OperacionDirector.Instancia;
                OperacionPrueba.Arrancar(3, 1);
                foreach (var x in Esperar(1.5f)) yield return x;
                int malSalida = 0, malParada = 0, malFinal = 0; var detalle = new StringBuilder();
                for (int tanda = 0; tanda < 2; tanda++)
                {
                    int antes = d.CamionetasDelCentroCreadas;
                    LanzarTandaDelCentro(d, tanda);
                    foreach (var a in CamionetasDeAsalto())
                    {
                        if (!a.name.StartsWith("Camioneta_Asalto_", StringComparison.Ordinal)) continue;
                        int n = int.Parse(a.name.Substring("Camioneta_Asalto_".Length));
                        if (n <= antes) continue;
                        var t = a.transform;
                        if (!OperacionDirector.LugarLibreParaCamioneta(t.position, t.rotation, t)) { malSalida++; detalle.Append(a.name + " salida; "); }
                        if (!OperacionDirector.LugarLibreParaCamioneta(a.puntoDeAsalto, t.rotation, t)) { malParada++; detalle.Append(a.name + " parada; "); }
                    }
                    foreach (var x in Esperar(1.0f)) yield return x;
                }
                foreach (var x in Esperar(8f)) yield return x;   // llegan a su parada
                var vivas = CamionetasDeAsalto();
                Vector3 centro = Vector3.zero;
                foreach (var a in vivas)
                {
                    var t = a.transform; centro += t.position;
                    if (!OperacionDirector.LugarLibreParaCamioneta(t.position, t.rotation, t)) { malFinal++; detalle.Append(a.name + " final; "); }
                }
                bool res = vivas.Count >= 4 && malSalida == 0 && malParada == 0 && malFinal == 0;
                if (!res) ok = false;
                sb.Append($"2 tandas: camionetas={vivas.Count}, salidas encimadas={malSalida}, paradas encimadas={malParada}, posiciones finales con solidos={malFinal}, paradas que hubo que correr={d.ParadasCorridas}{(res ? "" : " (MAL: " + detalle + ")")}; ");
                if (vivas.Count > 0)
                {
                    centro /= vivas.Count;
                    var camPos = new Vector3(-6.4f, 0.8f, -24.7f);
                    string e = CapturarDesde(camPos, Quaternion.LookRotation(centro + Vector3.up * 1f - camPos), "v2_070_camionetas.png", 1280, 720, 60f);
                    if (e != "") sb.Append(e + "; ");
                }
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + "(captura v2_070_camionetas.png)");
        }

        // ---------------------------------------------------------------- #072 visual: crater, chapas, explosiones
        static IEnumerator Bug072v()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            float escalaAntes = Time.timeScale;
            try
            {
                foreach (var x in PrepararTanque()) yield return x;
                var d = OperacionDirector.Instancia;
                if (!embarqueOk) { Fin("FALLO el jugador no quedo de artillero"); yield break; }
                var tw = d.tanque.TorretaCanon;

                // 1) un obus real contra el asfalto, a ~22 m por delante
                var f = tw.transform.forward; f.y = 0f; f.Normalize();
                var o = tw.Muzzle != null ? tw.Muzzle.position : tw.transform.position;
                tw.ElevarHacia(o + f * 22f - Vector3.up * (o.y - 0.1f));
                foreach (var x in Esperar(1.5f)) yield return x;
                int cr0 = CraterPool.Lanzados; bool disparo = false;
                float t0 = Time.realtimeSinceStartup;
                while (!disparo && Time.realtimeSinceStartup - t0 < 4f) { disparo = tw.TryFire(); yield return null; }
                foreach (var x in Esperar(2.5f)) yield return x;
                int activos = CraterPool.Activos; float yMax = -1f;
                foreach (var p in CraterPool.Piezas) if (p != null && p.gameObject.activeSelf) yMax = Mathf.Max(yMax, p.transform.position.y);
                bool crater = disparo && activos > 0 && yMax > 0.08f && yMax < 0.5f; if (!crater) ok = false;   // sobre el asfalto (no hundido) y apoyado (no flotando)
                sb.Append($"obus: disparo={disparo}, crateres activos={activos} (lanzados +{CraterPool.Lanzados - cr0}), y maxima={yMax:0.000}{(crater ? "" : "(MAL)")}; ");

                // 2) una camioneta destruida suelta chapas (en camara lenta para la captura)
                OperacionAuto objetivo = null; t0 = Time.realtimeSinceStartup;
                while (objetivo == null && Time.realtimeSinceStartup - t0 < 12f)
                {
                    foreach (var a in AutosVivos()) { if (Vector3.Distance(a.transform.position, d.tanque.transform.position) < 40f) { objetivo = a; break; } }
                    if (objetivo == null) yield return null;
                }
                if (objetivo == null) { ok = false; sb.Append("no aparecio ninguna camioneta; "); }
                else
                {
                    int ch0 = ChapaVolante.Lanzadas; int frag0 = Fragmentador.Activos;
                    var v = objetivo.Vehiculo; var pos = v.transform.position;
                    Time.timeScale = 0.1f;
                    v.TakeDamage(99999, d.tanque != null ? 0 : -1);
                    int maxChapas = 0, maxFrag = 0;
                    float t1 = Time.realtimeSinceStartup; bool captura = false;
                    while (Time.realtimeSinceStartup - t1 < 2.2f)
                    {
                        yield return null;
                        maxChapas = Mathf.Max(maxChapas, ChapaVolante.Activas); maxFrag = Mathf.Max(maxFrag, Fragmentador.Activos);
                        if (!captura && Time.realtimeSinceStartup - t1 > 1.1f)
                        {
                            captura = true;
                            var camPos = v.transform.position + new Vector3(-9f, 5f, -10f);
                            string e = CapturarDesde(camPos, Quaternion.LookRotation(v.transform.position + Vector3.up * 1.2f - camPos), "v2_072_explosion.png", 1280, 720, 50f);
                            if (e != "") sb.Append(e + "; ");
                        }
                    }
                    Time.timeScale = escalaAntes;
                    bool chapas = ChapaVolante.Lanzadas > ch0 && maxChapas > 0; if (!chapas) ok = false;
                    sb.Append($"camioneta destruida: chapas lanzadas +{ChapaVolante.Lanzadas - ch0} (activas max {maxChapas}), fragmentos max {maxFrag}{(chapas ? "" : "(MAL)")}; ");
                }
            }
            finally { Time.timeScale = escalaAntes; ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + "(captura v2_072_explosion.png)");
        }

        // ---------------------------------------------------------------- #073 tuning: mas autos, mas vida
        static IEnumerator Bug073()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            try
            {
                foreach (var x in PrepararTanque()) yield return x;
                var d = OperacionDirector.Instancia;
                if (!embarqueOk) { Fin("FALLO el jugador no quedo de artillero"); yield break; }
                int vidaMax = 0; float pisoTanque = 1f; float t0 = Time.realtimeSinceStartup; var visto = new HashSet<Vehicle>();
                float primeraAparicion = -1f;
                while (Time.realtimeSinceStartup - t0 < 30f)
                {
                    yield return null;
                    foreach (var a in AutosVivos())
                    {
                        var v = a.Vehiculo;
                        if (v == null) continue;
                        if (visto.Add(v) && primeraAparicion < 0f) primeraAparicion = Time.realtimeSinceStartup - t0;
                        vidaMax = Mathf.Max(vidaMax, v.Health.MaxHealth);
                    }
                    var th = d.tanque.Health; if (th != null) pisoTanque = Mathf.Min(pisoTanque, th.Current / (float)th.MaxHealth);
                    if (d.MaximoDeAutosVivos >= d.AutosSimultaneosEfectivos && vidaMax > 0 && Time.realtimeSinceStartup - t0 > 12f) break;
                }
                bool res = d.MaximoDeAutosVivos >= 4 && vidaMax == 260;
                if (!res) ok = false;
                sb.Append($"autos simultaneos max={d.MaximoDeAutosVivos} (objetivo {d.AutosSimultaneosEfectivos}), vida maxima de camioneta={vidaMax}, camionetas distintas vistas={visto.Count}, por delante={d.AutosPorDelante}, primera aparicion a los {primeraAparicion:0.0} s, vida minima del tanque={pisoTanque * 100f:0}% (piso {OperacionDirector.PisoDeVidaDelTanque * 100f:0}%){(res ? "" : "(MAL)")}; ");
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
