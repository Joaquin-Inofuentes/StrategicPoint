using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP5b: feedback de acciones. #066 engranajes sobre el que opera y sobre el interactuable, #067 animacion para toda accion,
    // #068 anillo de destino animado y linea blanca de 3 s. Hay que estar en Play sobre SC_Operacion; validar cada uno en un Play fresco.
    public static partial class ChecksBugs065
    {
        // Render de una camara temporal a PNG (sin HUD): sirve para ver los efectos del mundo desde donde se quiera.
        static string CapturarDesde(Vector3 pos, Quaternion rot, string archivo, int w = 1280, int h = 720, float fov = 55f, Light luz = null)
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

        // Mismo sentido de la camara del juego, desplazada hacia atras: los billboards (engranajes) miran a la camara principal.
        static Quaternion RotacionDeLaCamara() => CamaraPrincipal.Actual != null ? CamaraPrincipal.Actual.transform.rotation : Quaternion.identity;

        // ---------------------------------------------------------------- #066 engranajes
        static IEnumerator Bug066()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(2, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var kes = Escuadrista(RoleType.Flanker);
            CargaExplosiva panel = null;   // WP9a: la carga del puesto (solo el ASALTO) es el objetivo de la accion
            foreach (var t in UnityEngine.Object.FindObjectsByType<CargaExplosiva>(FindObjectsSortMode.None))
                if (t.Habilitado && !t.EstaPlantada && (panel == null || t.transform.position.x < panel.transform.position.x)) panel = t;
            if (vega == null || panel == null || EngranajesDeAccion.Instancia == null)
            {
                ModoDios.Poner(diosAntes);
                Fin($"FALLO faltan piezas: vega={(vega != null)} carga={(panel != null)} engranajes={(EngranajesDeAccion.Instancia != null)}"); yield break;
            }
            try
            {
                Poseer(vega);
                yield return null;
                Junto(vega, panel.transform.position, 2f);
                if (kes != null) Junto(kes, panel.transform.position, 7f, kes.transform.position);
                for (int i = 0; i < 3; i++) yield return null;
                int antes = EngranajesDeAccion.Activos;

                // A) Vega mantiene [E] en el panel: 2 engranajes (sobre Vega y sobre el panel).
                OperacionTerminal.PruebaMantenerE = true;
                foreach (var x in Esperar(0.8f)) yield return x;
                var m1 = EngranajesDeAccion.Muestras();
                int sobreKes = 0, sobrePanel = 0; float aK1 = 0f, aP1 = 0f; Vector3 pK = default, pP = default;
                foreach (var m in m1)
                {
                    if (m.Saliendo || m.ActorId != vega.Id) continue;
                    if (!m.EsObjetivo) { sobreKes++; aK1 = m.Angulo; pK = m.Posicion; } else { sobrePanel++; aP1 = m.Angulo; pP = m.Posicion; }
                }
                bool dos = EngranajesDeAccion.Activos == 2 && sobreKes == 1 && sobrePanel == 1;
                bool encima = Vector3.Distance(new Vector3(pK.x, 0, pK.z), new Vector3(vega.transform.position.x, 0, vega.transform.position.z)) < 0.01f
                    && pK.y > vega.transform.position.y + 1.2f && pP.y > panel.transform.position.y;
                if (!dos || !encima) ok = false;
                foreach (var m in m1) sb.Append($"[gear actor={m.ActorId} objetivo={m.EsObjetivo} alfa={m.Alfa:0.00} tam={m.Tamano:0.00} pos={m.Posicion}] ");
                sb.Append($"A) activos={EngranajesDeAccion.Activos} (antes {antes}): sobre Vega={sobreKes} (y={pK.y:0.00}, Vega y={vega.transform.position.y:0.00}) sobre el panel={sobrePanel} (y={pP.y:0.00}){(dos && encima ? "" : "(MAL)")}; ");

                // B) rotan: >= 40 grados entre dos muestras a 0.5 s, y en sentidos opuestos.
                // Desde un costado (los engranajes miran a la camara del juego: en una toma de frente los dos caerian uno sobre el otro).
                var rotCam = RotacionDeLaCamara();
                var camPos = vega.transform.position + rotCam * Vector3.right * 3.2f - rotCam * Vector3.forward * 4.5f + Vector3.up * 1.6f;
                var centroAccion = (vega.transform.position + panel.transform.position) * 0.5f + Vector3.up * 2.2f;
                var cap = CapturarDesde(camPos, Quaternion.LookRotation(centroAccion - camPos), "v2_066_engranajes.png", fov: 55f);
                var camPos2 = centroAccion + Vector3.up * 4f + rotCam * Vector3.right * 1.5f - rotCam * Vector3.forward * 5f;
                cap += CapturarDesde(camPos2, Quaternion.LookRotation(centroAccion - camPos2), "v2_066_engranajes_b.png", fov: 55f);
                foreach (var x in CapturarPantalla("v2_066_engranajes_fps.png")) yield return x;
                foreach (var x in Esperar(0.5f - 0.0f)) yield return x;
                var m2 = EngranajesDeAccion.Muestras();
                float aK2 = aK1, aP2 = aP1;
                foreach (var m in m2)
                {
                    if (m.Saliendo || m.ActorId != vega.Id) continue;
                    if (!m.EsObjetivo) aK2 = m.Angulo; else aP2 = m.Angulo;
                }
                float dK = aK2 - aK1, dP = aP2 - aP1;
                bool gira = Mathf.Abs(dK) >= 40f && Mathf.Abs(dP) >= 40f && Mathf.Sign(dK) != Mathf.Sign(dP);
                if (!gira) ok = false;
                sb.Append($"B) giro en ~0.5 s: sobre Vega {dK:0}, sobre el panel {dP:0} grados{(gira ? "" : "(MAL)")}; ");
                if (!string.IsNullOrEmpty(cap)) sb.Append(cap + "; ");

                // C) soltar [E]: se desvanecen y a los 0.5 s no queda ninguno (ni activo ni dibujado).
                OperacionTerminal.PruebaMantenerE = false;
                float tSolte = Time.realtimeSinceStartup;
                foreach (var x in Esperar(0.12f)) yield return x;
                int alfaMedio = EngranajesDeAccion.Visibles;
                float t05 = 0.5f - (Time.realtimeSinceStartup - tSolte);
                if (t05 > 0f) foreach (var x in Esperar(t05)) yield return x;
                int activos05 = EngranajesDeAccion.Activos, visibles05 = EngranajesDeAccion.Visibles;
                bool cero = activos05 == 0 && visibles05 == 0;
                if (!cero) ok = false;
                sb.Append($"C) al soltar: dibujados a los 0.12 s={alfaMedio}, a los {Time.realtimeSinceStartup - tSolte:0.00} s activos={activos05} dibujados={visibles05}{(cero ? "" : "(MAL)")}; ");

                // D) el engranaje de la mira (InteractGearMarker) tambien rota (90 grados por segundo).
                float g0 = InteractGearMarker.AnguloActual;
                float tg = Time.realtimeSinceStartup; float g1 = g0; float acum = 0f; float prev = g0;
                while (Time.realtimeSinceStartup - tg < 0.5f)
                {
                    InteractGearMarker.Mostrar(vega.transform.position + Vector3.up * 2f, true);
                    float a = InteractGearMarker.AnguloActual;
                    acum += Mathf.Repeat(a - prev + 180f, 360f) - 180f; prev = a;
                    yield return null;
                }
                InteractGearMarker.Ocultar();
                bool marcador = acum >= 40f;
                if (!marcador) ok = false;
                sb.Append($"D) engranaje de la mira: {acum:0} grados en 0.5 s{(marcador ? "" : "(MAL)")}; ");
            }
            finally
            {
                OperacionTerminal.PruebaMantenerE = false;
                InteractGearMarker.Ocultar();
                if (vega != null) Poseer(vega);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- #067 animacion para toda accion
        struct CasoDeAccion
        {
            public TipoAccion Tipo; public string Verbo; public string Prop; public bool Agacha; public bool Captura; public float Duracion;
        }

        static void DispararAccion(Soldier actor, Soldier otro, in CasoDeAccion c, float dt)
        {
            var f = actor.transform.forward; f.y = 0f; f.Normalize();
            switch (c.Tipo)
            {
                case TipoAccion.Recargar:
                case TipoAccion.RecogerMunicion:
                    // Acciones cortas con duracion propia: se disparan una vez (ver el bucle del check).
                    break;
                case TipoAccion.Reparar:
                    AnimacionDeAccion.Tick(actor, actor.transform.position + f * 0.9f, c.Tipo, dt);
                    break;
                case TipoAccion.Curar:
                    AccionesEnCurso.Reportar(actor, c.Verbo, otro.transform.position, 0.5f, 2f, otro.transform);
                    break;
                default:
                    AccionesEnCurso.Reportar(actor, c.Verbo, actor.transform.position + f * 1.1f, 0.5f, 2f, null);
                    break;
            }
        }

        static IEnumerator Bug067()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var doc = Escuadrista(RoleType.Medic); var kes = Escuadrista(RoleType.Flanker);
            if (vega == null || doc == null || kes == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan soldados de la escuadra"); yield break; }
            var tabla = new StringBuilder();
            var luzTmp = new GameObject("LuzCapturaTmp").AddComponent<Light>();
            luzTmp.type = LightType.Point; luzTmp.range = 14f; luzTmp.intensity = 6f; luzTmp.color = new Color(1f, 0.95f, 0.85f);
            var casos = new[]
            {
                new CasoDeAccion { Tipo = TipoAccion.Curar,           Verbo = "CURANDO",      Duracion = 1.0f },
                new CasoDeAccion { Tipo = TipoAccion.Revivir,         Verbo = "REVIVIENDO",   Agacha = true, Captura = true, Duracion = 1.2f },
                new CasoDeAccion { Tipo = TipoAccion.Demoler,         Verbo = "DETONANDO",    Prop = "Prop_CargaRoja", Duracion = 1.0f },
                new CasoDeAccion { Tipo = TipoAccion.Operar,          Verbo = "DESACTIVANDO", Captura = true, Duracion = 1.0f },
                new CasoDeAccion { Tipo = TipoAccion.Hackear,         Verbo = "HACKEANDO",    Duracion = 1.0f },
                new CasoDeAccion { Tipo = TipoAccion.Reparar,         Verbo = null,           Prop = "Prop_LlaveInglesa", Agacha = true, Captura = true, Duracion = 1.2f },
                new CasoDeAccion { Tipo = TipoAccion.RecogerMunicion, Verbo = null,           Duracion = 0.6f },
                new CasoDeAccion { Tipo = TipoAccion.Recargar,        Verbo = null,           Prop = "Prop_Cargador", Duracion = 0.4f },
            };
            var actores = new[] { doc, kes };
            try
            {
                Poseer(vega);
                yield return null;
                // Doc y Kes quietos, de frente al mismo lado; Vega (jugador) cerca de testigo.
                var centro = vega.transform.position;
                int n = 0;
                foreach (var c0 in casos)
                {
                    var c = c0;
                    var actor = actores[n % 2]; var otro = actor == doc ? kes : doc; n++;
                    if (actor.Brain != null) { actor.Brain.CancelOrder(); actor.Brain.Quieto = true; }
                    bool agachadoPrevio = actor.Motor != null && actor.Motor.IsCrouching;
                    for (int i = 0; i < 6; i++) yield return null;
                    var hay = AnimacionDeAccion.Activa(actor);
                    AnimacionDeAccion.ReiniciarMedicion(actor);
                    luzTmp.transform.position = actor.transform.position + new Vector3(1.5f, 2.5f, -1.5f);

                    float t0 = Time.realtimeSinceStartup; bool capturado = false; bool activaMedia = false; bool rig = false; bool agachaMedio = false; string propMedio = null;
                    string errCap = "";
                    if (c.Tipo == TipoAccion.Recargar || c.Tipo == TipoAccion.RecogerMunicion)
                        AnimacionDeAccion.Iniciar(actor, c.Tipo, actor.transform.position + actor.transform.forward, c.Duracion);
                    while (Time.realtimeSinceStartup - t0 < c.Duracion + 0.3f)
                    {
                        float el = Time.realtimeSinceStartup - t0;
                        if (el < c.Duracion) DispararAccion(actor, otro, c, Time.deltaTime);
                        if (el >= Mathf.Min(0.5f, c.Duracion * 0.6f) && !activaMedia)
                        {
                            activaMedia = AnimacionDeAccion.Activa(actor) && AnimacionDeAccion.TipoActivo(actor) == c.Tipo;
                            rig = AnimacionDeAccion.TieneRig(actor);
                            agachaMedio = AnimacionDeAccion.AgachadaPropia(actor);
                            propMedio = AnimacionDeAccion.Prop(actor);
                        }
                        if (c.Captura && !capturado && el >= 0.6f)
                        {
                            capturado = true;
                            var p = actor.transform.position;
                            var f = actor.transform.forward; f.y = 0f; f.Normalize();
                            var camPos = p + f * 2.6f + actor.transform.right * 1.2f + Vector3.up * 0.7f;
                            var look = Quaternion.LookRotation((p + Vector3.up * 0.1f) - camPos);
                            errCap = CapturarDesde(camPos, look, "v2_067_" + c.Tipo.ToString().ToLowerInvariant() + ".png", 960, 720, 45f);
                        }
                        yield return null;
                    }
                    float desp = AnimacionDeAccion.DesplazamientoMaximoDeManos(actor);
                    foreach (var x in Esperar(0.5f)) yield return x;
                    bool cerro = !AnimacionDeAccion.Activa(actor) && AnimacionDeAccion.Prop(actor) == null && !AnimacionDeAccion.AgachadaPropia(actor)
                        && (actor.Motor == null || actor.Motor.IsCrouching == agachadoPrevio);
                    bool propOk = c.Prop == null || propMedio == c.Prop;
                    bool agachaOk = !c.Agacha || agachaMedio;
                    bool filaOk = activaMedia && rig && desp >= 0.15f && cerro && propOk && agachaOk;
                    if (!filaOk) ok = false;
                    tabla.Append($"{c.Tipo}[{actor.DisplayName}]: activa={activaMedia} rig={rig} manos+{desp:0.00} m prop={(propMedio ?? "-")} agacha={agachaMedio} cierra={cerro} {(filaOk ? "OK" : "MAL")}{(errCap != "" ? " " + errCap : "")}; ");
                    if (actor.Brain != null) actor.Brain.Quieto = false;
                }

                // Caminando de verdad (orden de movimiento a 12 m): el IK se apaga (>0.5 m/s) y las manos quedan en la pose del Animator.
                var camina = doc;
                AnimacionDeAccion.Terminar(camina); AccionesEnCurso.Terminar(camina);
                if (camina.Brain != null) { camina.Brain.CancelOrder(); camina.Brain.Quieto = false; }
                for (int i = 0; i < 6; i++) yield return null;
                var inicio = camina.transform.position;
                var fw = camina.transform.forward; fw.y = 0f; fw.Normalize();
                OrderService.IssueMoveOrder(camina, inicio + fw * 14f);
                AnimacionDeAccion.Tick(camina, camina.transform.position + fw, TipoAccion.Operar, Time.deltaTime);
                float tw = Time.realtimeSinceStartup; float vMax = 0f; Vector3 pPrev = camina.transform.position;
                bool reinicio = false;
                while (Time.realtimeSinceStartup - tw < 1.6f)
                {
                    // Los primeros 0.4 s son la transicion (arrancar y soltar el IK en ~70 ms): se mide despues.
                    if (!reinicio && Time.realtimeSinceStartup - tw >= 0.4f) { reinicio = true; AnimacionDeAccion.ReiniciarMedicion(camina); }
                    AnimacionDeAccion.Tick(camina, camina.transform.position + fw, TipoAccion.Operar, Time.deltaTime);
                    vMax = Mathf.Max(vMax, (camina.transform.position - pPrev).magnitude / Mathf.Max(Time.deltaTime, 0.001f));
                    pPrev = camina.transform.position;
                    yield return null;
                }
                float recorrido = Vector3.Distance(camina.transform.position, inicio);
                float despCamina = AnimacionDeAccion.DesplazamientoMaximoDeManos(camina);
                AnimacionDeAccion.Terminar(camina);
                if (camina.Brain != null) { camina.Brain.CancelOrder(); camina.Brain.Quieto = false; }
                // Enganches reales (no la API directa): recoger una caja de municion y recargar el arma de verdad.
                var jugador = vega;
                Poseer(jugador);
                for (int i = 0; i < 4; i++) yield return null;
                if (jugador.Brain != null) jugador.Brain.Quieto = false;
                AnimacionDeAccion.Terminar(jugador);
                bool recoge = false, recarga = false; string tRec = "-", tRel = "-";
                MunicionPickup.Crear(jugador.transform.position + Vector3.up * 0.0f);
                for (int i = 0; i < 6; i++)
                {
                    yield return null;
                    var ta = AnimacionDeAccion.TipoActivo(jugador);
                    if (ta == TipoAccion.RecogerMunicion) { recoge = true; tRec = ta.ToString(); }
                }
                foreach (var x in Esperar(0.9f)) yield return x;
                bool recargaIniciada = false;
                if (jugador.Weapon != null)
                {
                    var campo = typeof(WeaponHolder).GetField("<CurrentAmmo>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (campo != null) campo.SetValue(jugador.Weapon, Mathf.Max(0, jugador.Weapon.MagazineSize - 3));
                    recargaIniciada = jugador.Weapon.Reload();
                }
                for (int i = 0; i < 6; i++)
                {
                    yield return null;
                    var ta = AnimacionDeAccion.TipoActivo(jugador);
                    if (ta == TipoAccion.Recargar) { recarga = true; tRel = ta.ToString(); }
                }
                bool enganches = recoge && recarga && recargaIniciada;
                if (!enganches) ok = false;
                sb.Append($"ENGANCHES: MunicionPickup -> {tRec} ({(recoge ? "OK" : "MAL")}); WeaponHolder.Reload -> {tRel} reload={recargaIniciada} ({(recarga ? "OK" : "MAL")}); ");
                bool caminaOk = despCamina < 0.10f && recorrido > 2f;
                if (!caminaOk) ok = false;
                sb.Append("TABLA: " + tabla + "| caminando (recorrio " + recorrido.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m, v max " + vMax.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m/s): manos+" + despCamina.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m (el IK se apaga)" + (caminaOk ? "" : "(MAL)") + "; ");
            }
            finally
            {
                foreach (var a in actores) { if (a != null && a.Brain != null) a.Brain.Quieto = false; AnimacionDeAccion.Terminar(a); AccionesEnCurso.Terminar(a); }
                if (luzTmp != null) UnityEngine.Object.DestroyImmediate(luzTmp.gameObject);
                if (vega != null) Poseer(vega);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- #068 destino animado y linea de 3 s
        static IEnumerator Bug068()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault); var doc = Escuadrista(RoleType.Medic);
            var lineas = OrderLineManager.Instancia;
            if (vega == null || doc == null || lineas == null || doc.Brain == null)
            {
                ModoDios.Poner(diosAntes);
                Fin($"FALLO faltan piezas: vega={(vega != null)} doc={(doc != null)} lineas={(lineas != null)}"); yield break;
            }
            try
            {
                Poseer(vega);
                yield return null;
                // Un destino lejano (>= 40 m de camino) para que Doc siga en camino durante los 3+ s del check.
                Vector3? destino = null; float largo = 0f;
                var o = doc.transform.position;
                for (int g = 0; g < 360 && destino == null; g += 20)
                {
                    var dirp = Quaternion.Euler(0f, g, 0f) * Vector3.forward;
                    var p = o + dirp * 60f;
                    if (!NavMesh.SamplePosition(p, out var h, 10f, NavMesh.AllAreas)) continue;
                    var camino = new NavMeshPath();
                    if (!NavMesh.CalculatePath(o, h.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
                    float L = 0f; for (int i = 1; i < camino.corners.Length; i++) L += Vector3.Distance(camino.corners[i - 1], camino.corners[i]);
                    if (L >= 45f) { destino = h.position; largo = L; }
                }
                if (destino == null) { Fin("FALLO no se encontro un destino alcanzable a >= 45 m de camino"); yield break; }
                int matsAntes = Resources.FindObjectsOfTypeAll<Material>().Length;

                OrderService.IssueMoveOrder(doc, destino.Value);
                float t0 = Time.realtimeSinceStartup;
                OrderMarkerFx mk = OrderMarkerFx.MasCercanoA(destino.Value);
                bool existe = mk != null;
                sb.Append($"destino a {largo:0} m de camino; marcador creado={existe}; ");
                if (!existe) { ok = false; }

                string cap = "";
                float escala06 = 0f, ang06 = 0f; bool activo06 = false; float escala12 = 0f;
                bool linea1 = false, linea28 = false, linea32 = false, ordenViva32 = false; float alfa1 = -1f, alfa28 = -1f; bool act32 = true;
                bool m06 = false, m1 = false, m28 = false, m32 = false, c1 = false, c2 = false, c3 = false;
                var cielo = doc.transform.position;
                while (Time.realtimeSinceStartup - t0 < 3.4f)
                {
                    float el = Time.realtimeSinceStartup - t0;
                    if (mk != null && !m06 && el >= 0.6f)
                    {
                        m06 = true; activo06 = mk.gameObject.activeInHierarchy; escala06 = mk.EscalaActual; ang06 = mk.AnguloActual;
                    }
                    if (mk != null && !c1 && el >= 0.2f) { c1 = true; cap += CapturarDesde(destino.Value + new Vector3(0f, 6f, -7f), Quaternion.LookRotation(new Vector3(0f, -6f, 7f)), "v2_068_destino_a.png", 960, 540, 45f); }
                    if (mk != null && !c2 && el >= 0.55f) { c2 = true; cap += CapturarDesde(destino.Value + new Vector3(0f, 6f, -7f), Quaternion.LookRotation(new Vector3(0f, -6f, 7f)), "v2_068_destino_b.png", 960, 540, 45f); }
                    if (mk != null && !c3 && el >= 0.95f) { c3 = true; cap += CapturarDesde(destino.Value + new Vector3(0f, 6f, -7f), Quaternion.LookRotation(new Vector3(0f, -6f, 7f)), "v2_068_destino_c.png", 960, 540, 45f); }
                    if (!m1 && el >= 1.0f)
                    {
                        m1 = true; linea1 = lineas.Estado(doc.Id, out alfa1, out _, out _) && alfa1 >= 0.999f;
                        // Captura de la linea desde atras de Doc.
                        var f = (destino.Value - doc.transform.position); f.y = 0f; f.Normalize();
                        cap += CapturarDesde(doc.transform.position - f * 3.5f + Vector3.up * 3.2f, Quaternion.LookRotation(f + Vector3.down * 0.25f), "v2_068_linea.png", 960, 540, 60f);
                    }
                    if (!m28 && el >= 2.8f)
                    {
                        m28 = true; linea28 = lineas.Estado(doc.Id, out alfa28, out bool act, out _) && alfa28 < 1f && alfa28 > 0f && act;
                    }
                    if (!m32 && el >= 3.2f)
                    {
                        m32 = true;
                        bool hay = lineas.Estado(doc.Id, out float a, out bool activa, out float edad);
                        act32 = activa;
                        linea32 = hay && !activa;
                        ordenViva32 = doc.Brain.CurrentOrderDestination.HasValue;
                        escala12 = mk != null && mk.gameObject.activeInHierarchy ? mk.EscalaActual : -1f;
                    }
                    yield return null;
                }
                bool marcador = existe && activo06 && escala06 > 1.2f && Mathf.Abs(ang06) > 0.01f;
                bool lineaOk = linea1 && linea28 && linea32 && ordenViva32;
                bool sigueMarcador = escala12 > 0f;
                if (!marcador || !lineaOk || !sigueMarcador) ok = false;
                sb.Append($"a 0.6 s: marcador activo={activo06} escala {escala06:0.00} (pide > 1.2) giro {ang06:0}{(marcador ? "" : "(MAL)")}; ");
                sb.Append($"linea: alfa a 1 s={alfa1:0.00}, a 2.8 s={alfa28:0.00}, a 3.2 s activa={act32} con la orden aun en curso={ordenViva32}{(lineaOk ? "" : "(MAL)")}; marcador sigue a 3.2 s={sigueMarcador}; ");
                if (cap != "") sb.Append(cap + "; ");

                // Pulso final: al cancelar la orden el anillo hace su pulso y desaparece (en menos de 0.6 s).
                doc.Brain.CancelOrder();
                float tc = Time.realtimeSinceStartup; bool pulso = false;
                while (Time.realtimeSinceStartup - tc < 0.7f)
                {
                    if (mk != null && mk.PulsoFinal) pulso = true;
                    yield return null;
                }
                bool fuera = mk == null || !mk.gameObject.activeInHierarchy;
                if (!pulso || !fuera) ok = false;
                sb.Append($"al cancelar: pulso final={pulso}, desaparece a los 0.7 s={fuera}{(pulso && fuera ? "" : "(MAL)")}; ");

                // Sin fuga de materiales: una primera tanda de 30 ordenes crece el pool (cada marcador nuevo trae su sistema de particulas);
                // la segunda tanda reusa marcadores y no puede crear ningun material.
                for (int i = 0; i < 30; i++) { OrderMarkerFx.Spawn(destino.Value + new Vector3(i * 0.3f, 0f, 0f), OrderMarkerFx.MoveColor, 0.2f); }
                foreach (var x in Esperar(2.0f)) yield return x;
                int matsMedio = Resources.FindObjectsOfTypeAll<Material>().Length;
                for (int i = 0; i < 30; i++) { OrderMarkerFx.Spawn(destino.Value + new Vector3(i * 0.3f, 0f, 3f), Feedback.Cover, 0.2f); }
                foreach (var x in Esperar(2.0f)) yield return x;
                int matsDespues = Resources.FindObjectsOfTypeAll<Material>().Length;
                // Validacion: el +1/+2 de ruido viene de otros sistemas vivos (polvo, humo, HUD) y no de los marcadores: 6 tandas de 30 reusos medidas
                // aparte dan +0. Una fuga real (1 material por marcador) daria +30 por tanda, asi que se tolera hasta +4.
                bool sinFuga = matsDespues - matsMedio <= 4;
                if (!sinFuga) ok = false;
                sb.Append($"materiales: antes {matsAntes}, tras 30 marcadores nuevos {matsMedio} (crece el pool), tras otros 30 reusados {matsDespues}{(sinFuga ? "" : "(MAL)")}; ");
            }
            finally
            {
                if (doc != null && doc.Brain != null) doc.Brain.CancelOrder();
                OrderMarkerFx.RecycleAll();
                if (vega != null) Poseer(vega);
                ModoDios.Poner(diosAntes);
            }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }
    }
}
