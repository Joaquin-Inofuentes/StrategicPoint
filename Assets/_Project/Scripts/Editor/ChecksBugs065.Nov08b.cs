using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Checks de los bugs #396-#414 (2026-11-08, segunda tanda). Plan: Docs/PLAN_bugs_396_414_opus.md.
    public static partial class ChecksBugs065
    {
        static string RutaCompleta(Transform t) { var s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s; }

        static Color ColorDe(Renderer r)
        {
            if (r is LineRenderer lr) return lr.startColor;
            var m = r.sharedMaterial; if (m == null) return Color.clear;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.clear;
        }

        // Renderers activos que tocan una caja, con tipo, color y textura (para identificar "esto" de un reporte).
        static string RenderersEnCaja(Bounds caja, bool soloAmarillos, int max = 25)
        {
            var sb = new StringBuilder(); int n = 0, total = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || !r.bounds.Intersects(caja)) continue;
                var c = ColorDe(r);
                bool amarillo = c.r > 0.7f && c.g > 0.6f && c.b < 0.45f;
                string tex = r.sharedMaterial != null && r.sharedMaterial.mainTexture != null ? r.sharedMaterial.mainTexture.name : "";
                if (soloAmarillos && !amarillo && !tex.ToLowerInvariant().Contains("punte")) continue;
                total++;
                if (n++ < max) sb.Append(RutaCompleta(r.transform)).Append(" [").Append(r.GetType().Name).Append("] c=").Append(c).Append(" tex=").Append(tex).Append(" sz=").Append(r.bounds.size).Append(" | ");
            }
            return $"{total} renderers: " + sb;
        }

        // ---------------------------------------------------------------- 404 la linea punteada amarilla (GuiaDeMando/RutaDelPaso)
        static IEnumerator Bug404a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            ModoDios.Poner(true);
            GuiaDeMando.Asegurar();
            foreach (var x in Esperar(3f)) yield return x;
            int dibujadas = 0; var nombres = new StringBuilder();
            foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude))
                if (lr != null && lr.enabled && lr.gameObject.activeInHierarchy && lr.transform.parent != null && lr.transform.parent.name == "GuiaDeMando") { dibujadas++; nombres.Append(lr.name).Append("#").Append(lr.transform.parent.GetHashCode()).Append(" n=").Append(lr.positionCount).Append(" mat=").Append(lr.sharedMaterial != null ? lr.sharedMaterial.name : "-").Append(" guiaInst=").Append(GuiaDeMando.Instancia != null ? GuiaDeMando.Instancia.GetHashCode() : 0).Append(" "); }
            W8Ok(ref ok, !GuiaDeMando.DibujarRuta && dibujadas == 0, sb, $"la ruta punteada de la guia del mando no se dibuja ({dibujadas} lineas activas: {nombres})");
            var caja = new Bounds(new Vector3(322f, 0.4f, 255f), new Vector3(8f, 1.6f, 70f));
            sb.Append("[amarillos en la caja: " + RenderersEnCaja(caja, true, 6) + "] ");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 403 particulas trabadas (diagnostico + criterio)
        static IEnumerator Bug403a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            // 30 destrucciones y unos disparos contra el entorno de la plaza, despues 25 s de calma
            var yo = Poseido();
            for (int i = 0; i < 30; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Prueba403_" + i;
                go.transform.position = (yo != null ? yo.transform.position : new Vector3(322f, 0.8f, 226f)) + new Vector3(Random.Range(-6f, 6f), 1f, Random.Range(4f, 12f));
                Fragmentador.Romper(go.transform, go.transform.position + Vector3.up, 6f);
                Object.Destroy(go);
                yield return null;
            }
            // Se espera tiempo DE JUEGO (los fragmentos envejecen con Time.deltaTime y el editor corre lento): 10 s > la vida maxima (8 s + hundido)
            float t0 = Time.time, r0 = Time.realtimeSinceStartup;
            while (Time.time - t0 < 10f && Time.realtimeSinceStartup - r0 < 90f) yield return null;
            var viejos = new StringBuilder(); int nFrag = 0;
            foreach (var f in Fragmentador.Piezas) if (f != null && f.EnUso) { nFrag++; if (viejos.Length < 400) viejos.Append(f.name).Append(" edad=").Append(f.Edad.ToString("0.0")).Append(" vida=").Append(f.Vida.ToString("0.0")).Append(" asentado=").Append(f.Asentado).Append(" | "); }
            W8Ok(ref ok, nFrag == 0, sb, $"pasados 10 s de juego sin fuego no queda ningun fragmento en uso ({nFrag}: {viejos})");
            int trabadas = 0; var det = new StringBuilder();
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
            {
                if (ps == null || !ps.gameObject.activeInHierarchy) continue;
                var m = ps.main;
                if (m.loop) continue;   // los ambientales (llamas, brasas, luciernagas) son de bucle a proposito
                if (ps.particleCount > 0 && (ps.isPaused || ps.time > m.duration + m.startLifetime.constantMax + 4f)) { trabadas++; if (det.Length < 500) det.Append(RutaCompleta(ps.transform)).Append(" n=").Append(ps.particleCount).Append(" t=").Append(ps.time.ToString("0.0")).Append(" dur=").Append(m.duration).Append(" pausado=").Append(ps.isPaused).Append(" | "); }
            }
            W8Ok(ref ok, trabadas == 0, sb, $"ninguna particula (no ambiental) queda trabada ({trabadas}: {det})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- tanque propio para los checks del lote 1
        static SP.Vehicles.Vehicle TanqueDelJugador()
        {
            SP.Vehicles.Vehicle v = null;
            foreach (var t in SP.Vehicles.Vehicle.Todos) if (t != null && t.Health != null && !t.IsDestroyed && t.TorretaCanon != null && t.Bando != TeamId.Enemy) { v = t; if (t.isActiveAndEnabled) break; }
            if (v == null) foreach (var t in SP.Vehicles.Vehicle.Todos) if (t != null && t.Health != null && !t.IsDestroyed && t.TorretaCanon != null) { t.MarcarBando(TeamId.Player); v = t; break; }
            return v;
        }

        // ---------------------------------------------------------------- 396 HUD del tanque: barras anchas, sin pisarse, sin el HUD viejo
        static IEnumerator Bug396b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var v = TanqueDelJugador();
            if (v == null) { Fin("FALLO no hay tanque propio"); yield break; }
            SP.UI.CapasDeHud.ModoTanque(true);
            SP.UI.HudDeTanqueSimple.Mostrar(v);
            foreach (var x in Esperar(0.6f)) yield return x;
            var h = SP.UI.HudDeTanqueSimple.Instancia;
            W8Ok(ref ok, h != null && SP.UI.HudDeTanqueSimple.Visible, sb, "el HUD del tanque se ve");
            if (h == null) { Fin("FALLO " + sb); yield break; }
            int viejos = 0;
            foreach (var vs in Object.FindObjectsByType<SP.UI.VehicleStatusView>(FindObjectsInactive.Exclude)) if (vs != null && vs.gameObject.activeInHierarchy) { var g = vs.GetComponentInParent<CanvasGroup>(); if (g == null || g.alpha > 0.01f) viejos++; }
            W8Ok(ref ok, viejos == 0, sb, $"el panel viejo de vehiculo no se muestra ({viejos})");
            W8Ok(ref ok, h.AnchoDeLaBarraDeVida >= 700f, sb, $"barra de vida ancha ({h.AnchoDeLaBarraDeVida:0} px)");
            W8Ok(ref ok, h.InsigniaDeDiosVisible, sb, "la insignia de dios se ve con dios activo");
            var rv = h.RectDeVida; var rr = h.RectDeRecarga; var ri = h.RectDeInsignia;
            W8Ok(ref ok, !rv.Overlaps(rr) && !rv.Overlaps(ri) && !rr.Overlaps(ri), sb, $"no se pisan vida {rv} recarga {rr} insignia {ri}");
            try
            {
                var dir = System.IO.Path.Combine(Application.dataPath, "Validacion~"); System.IO.Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "v9_396_hud_tanque.png"));
            }
            catch { }
            foreach (var x in Esperar(0.5f)) yield return x;
            SP.UI.CapasDeHud.ModoTanque(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 399 la barra de recarga sigue al canon
        static IEnumerator Bug399a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var v = TanqueDelJugador();
            if (v == null) { Fin("FALLO no hay tanque propio"); yield break; }
            SP.UI.CapasDeHud.ModoTanque(true); SP.UI.HudDeTanqueSimple.Mostrar(v);
            foreach (var x in Esperar(0.5f)) yield return x;
            var h = SP.UI.HudDeTanqueSimple.Instancia;
            var canon = v.TorretaCanon;
            bool disparo = canon.TryFire();
            float minimo = 1f, t0 = Time.time;
            while (Time.time - t0 < 6f) { minimo = Mathf.Min(minimo, h.FraccionDeRecarga); if (minimo < 0.5f) break; yield return null; }
            W8Ok(ref ok, disparo, sb, "el canon dispara");
            W8Ok(ref ok, minimo < 0.5f, sb, $"la barra de recarga baja al disparar el canon (minimo {minimo:0.00})");
            t0 = Time.time; while (Time.time - t0 < 30f && h.FraccionDeRecarga < 0.99f) yield return null;
            W8Ok(ref ok, h.FraccionDeRecarga >= 0.99f, sb, $"y vuelve a llenarse ({h.FraccionDeRecarga:0.00})");
            SP.UI.CapasDeHud.ModoTanque(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 400 el proyectil del canon va al triple (la ametralladora no)
        static IEnumerator Bug400a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var v = TanqueDelJugador();
            if (v == null) { Fin("FALLO no hay tanque propio"); yield break; }
            var canon = v.TorretaCanon; float vCanon = SP.Vehicles.TurretWeapon.ProjectileSpeed * canon.MultiplicadorDeVelocidad;
            W8Ok(ref ok, Mathf.Abs(vCanon - 390f) < 20f, sb, $"el canon del jugador va a ~390 m/s ({vCanon:0})");
            string mg = "sin ametralladora";
            foreach (var tw in v.GetComponentsInChildren<SP.Vehicles.TurretWeapon>(true))
                if (tw != canon) { float vm = SP.Vehicles.TurretWeapon.ProjectileSpeed * tw.MultiplicadorDeVelocidad; mg = vm.ToString("0"); W8Ok(ref ok, vm < vCanon * 0.5f, sb, $"la ametralladora no cambia ({vm:0} m/s)"); break; }
            var real = typeof(SP.Combat.Projectile).GetField("effectiveSpeed", NoPublico);
            canon.TryFire(); yield return null; yield return null;
            float leida = 0f; foreach (var p in SP.Combat.Projectile.ActiveInstances) if (p != null && real != null) leida = Mathf.Max(leida, (float)real.GetValue(p));
            W8Ok(ref ok, real == null || Mathf.Abs(leida - vCanon) < 25f, sb, $"el proyectil creado sale con esa velocidad ({leida:0})");
            Fin((ok ? "OK " : "FALLO ") + sb + " mg=" + mg);
        }

        // ---------------------------------------------------------------- 401 los rombos enemigos se ven detras de los obstaculos
        static IEnumerator Bug401a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(3f)) yield return x;
            int n = 0, siempre = 0;
            foreach (var u in Object.FindObjectsByType<UnitLocatorCylinder>(FindObjectsInactive.Include)) { if (u == null) continue; n++; if (u.SiempreEncima) siempre++; }
            W8Ok(ref ok, n > 0 && siempre == n, sb, $"todos los rombos (incluidos enemigos) se dibujan por encima de lo que tapa ({siempre}/{n})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 398 tanques enemigos garantizados, feedback de impacto, dano x2
        static IEnumerator Bug398c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            var crear = typeof(OperacionDirector).GetMethod("CrearAuto", NoPublico);
            for (int i = 0; i < 12; i++) { crear?.Invoke(d, null); yield return null; }
            W8Ok(ref ok, d.TanquesEnemigosCreados >= OperacionDirector.TanquesMinimos && d.TanquesEnemigosCreados <= OperacionDirector.TanquesMaximos, sb, $"tanques enemigos entre {OperacionDirector.TanquesMinimos} y {OperacionDirector.TanquesMaximos} ({d.TanquesEnemigosCreados} de {d.AutosCreados} autos)");
            W8Ok(ref ok, OperacionDirector.MultiplicadorDeDanoDelTanque >= 8f, sb, $"el dano que recibe el tanque es el doble que antes ({OperacionDirector.MultiplicadorDeDanoDelTanque})");
            var v = TanqueDelJugador();
            if (v != null)
            {
                SP.UI.CapasDeHud.ModoTanque(true); SP.UI.HudDeTanqueSimple.Mostrar(v);
                foreach (var x in Esperar(0.5f)) yield return x;
                int mostradosAntes = SP.UI.ImpactoEnVehiculoView.Mostrados;
                v.TakeDamage(70, 0);
                for (int i = 0; i < 6; i++) yield return null;
                var iv = SP.UI.ImpactoEnVehiculoView.Instancia;
                W8Ok(ref ok, SP.UI.ImpactoEnVehiculoView.Mostrados > mostradosAntes, sb, $"un golpe al tanque dispara el feedback ({mostradosAntes} -> {SP.UI.ImpactoEnVehiculoView.Mostrados}, alpha {(iv != null ? iv.AlphaActual : -1f):0.00})");
                SP.UI.CapasDeHud.ModoTanque(false);
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ lote 2: mansion (#406-#414)
        static IEnumerable ArrancarMansion(OperacionDirector[] salida)
        {
            OperacionPrueba.Arrancar(5); ModoDios.Poner(true);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia;
            typeof(OperacionDirector).GetMethod("AbrirElPorton", NoPublico).Invoke(d, null);
            salida[0] = d;
            foreach (var x in Esperar(2f)) yield return x;
        }

        static int ContarActivos(System.Func<Transform, bool> pred, out string nombres)
        {
            int n = 0; var sb = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.gameObject.activeInHierarchy && pred(t)) { n++; if (sb.Length < 300) sb.Append(RutaCompleta(t)).Append(" | "); }
            nombres = sb.ToString(); return n;
        }

        // 407 lamparas de pie del piso 2
        static IEnumerator Bug407a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            foreach (var x in Esperar(4f)) yield return x;   // el vigilante del parche corre cada 3 s
            int n = ContarActivos(t => (t.name.StartsWith("Lampara_Pie") || t.name == "Luz_Lampara_Pie") && G.Dentro(t.position), out var nombres);
            W8Ok(ref ok, n == 0, sb, $"no quedan lamparas de pie activas dentro de la mansion ({n}: {nombres})");
            foreach (var x in FotoCamara("v9_407_despues.jpg", new Vector3(341.5f, 16f, 268f), new Vector3(309f, 14f, 268f), 70f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 408 escenario del salon (sin colision) pero NO el de la Huida
        static IEnumerator Bug408a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            foreach (var x in Esperar(4f)) yield return x;
            int n = ContarActivos(t => (t.name == "Escenario" || t.name == "Escenario_Borde" || t.name == "Escenario_Telon") && G.Dentro(t.position) && Mathf.Abs(t.position.y - G.Nivel(2)) < 2f, out var nombres);
            W8Ok(ref ok, n == 0, sb, $"el escenario del piso 2 esta apagado ({n}: {nombres})");
            foreach (var x in FotoCamara("v9_408_despues.jpg", new Vector3(302.8f, 17.3f, 275.3f), new Vector3(299f, 14.5f, 267f), 70f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 411 bloque negro: ningun Azotea_Equipo sobre el hueco de una torre
        static IEnumerator Bug411a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            foreach (var x in Esperar(4f)) yield return x;
            int sobre = 0; var det = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t == null || t.name != "Azotea_Equipo" || !t.gameObject.activeInHierarchy) continue;
                var r = t.GetComponentInChildren<Renderer>(); var b = r != null ? r.bounds : new Bounds(t.position, Vector3.one);
                bool toca = false;
                foreach (var sx in new[] { -1f, 0f, 1f }) foreach (var sz in new[] { -1f, 0f, 1f })
                    if (G.EnTorre(new Vector3(b.center.x + sx * b.extents.x, G.Nivel(3) - 0.5f, b.center.z + sz * b.extents.z), out _, 0.3f)) toca = true;
                if (toca) { sobre++; det.Append(t.position).Append(" | "); }
            }
            W8Ok(ref ok, sobre == 0, sb, $"ningun Azotea_Equipo tapa el hueco de una torre ({sobre}: {det})");
            foreach (var x in FotoCamara("v9_411_despues.jpg", new Vector3(339.2f, 18.2f, 283.2f), new Vector3(338.6f, 17.2f, 278f), 70f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 409/410 las compuertas no frenan las balas ni el apuntado, pero siguen frenando el movimiento
        static IEnumerator Bug409a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            int total = 0, sin = 0, frenaMov = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || !t.name.StartsWith("Mansion_Compuerta_")) continue;
                var c = t.GetComponent<Collider>(); if (c == null) continue;
                total++;
                if (t.GetComponent<NoFrenaBalas>() != null) sin++;
                if (c.enabled && NavService.BlocksMovement(c)) frenaMov++;
            }
            W8Ok(ref ok, total >= 8 && sin == total, sb, $"todas las compuertas ({total}) llevan NoFrenaBalas ({sin})");
            W8Ok(ref ok, frenaMov > 0, sb, $"las cerradas siguen frenando el movimiento ({frenaMov})");
            // una bala real contra la compuerta cerrada: el rayo de la bala no se frena en ella
            Collider cerrada = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) { if (t != null && t.name.StartsWith("Mansion_Compuerta_") && t.GetComponent<Collider>() != null && t.GetComponent<Collider>().enabled) { cerrada = t.GetComponent<Collider>(); break; } }
            if (cerrada != null)
            {
                var origen = cerrada.bounds.center - cerrada.transform.forward * 3f;
                var hits = Physics.RaycastAll(origen, cerrada.transform.forward, 8f, ~0, QueryTriggerInteraction.Ignore);
                bool algunoFrena = false;
                foreach (var h in hits) if (h.collider.GetComponent<NoFrenaBalas>() == null && h.collider == cerrada) algunoFrena = true;
                W8Ok(ref ok, !algunoFrena, sb, "el collider de la compuerta no cuenta como obstaculo de disparos");
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 414 solo 2 milicianos
        static IEnumerator Bug414a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            var d = dd[0];
            PlayerInputDriver.Activo.Brain.Current.transform.position = new Vector3(322f, G.Nivel(2) + 0.9f, 262f);
            typeof(OperacionDirector).GetMethod("EntrarSegundoPiso", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(1.5f)) yield return x;
            int n = ContarActivos(t => t.name.StartsWith("Prisionero_") && t.GetComponent<Soldier>() != null, out var nombres);
            W8Ok(ref ok, n == MandoTactico.CantidadDeMilicianos, sb, $"existen exactamente {MandoTactico.CantidadDeMilicianos} prisioneros ({n}: {nombres})");
            typeof(OperacionDirector).GetMethod("LiberarMilicianos", NoPublico).Invoke(d, null);
            foreach (var x in Esperar(1f)) yield return x;
            W8Ok(ref ok, MandoTactico.Milicianos.Count == MandoTactico.CantidadDeMilicianos, sb, $"liberados: {MandoTactico.Milicianos.Count} milicianos");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 413 + 406 aliados atrapados en la torre / entre pisos
        static IEnumerator Bug413a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            var d = dd[0]; var yo = PlayerInputDriver.Activo.Brain.Current;
            typeof(OperacionDirector).GetMethod("EntrarAzotea", NoPublico).Invoke(d, null);
            yo.transform.position = new Vector3(334.9f, 20.3f, 265.3f);
            var pos = new[] { new Vector3(338.4f, 20.2f, 273.5f), new Vector3(339.6f, 20.3f, 273.1f), new Vector3(340.8f, 20.3f, 273.0f) };
            var aliados = new List<Soldier>(); int k = 0;
            foreach (var a in OperacionDirector.AliadosAEvacuar(true)) if (a != null && a != yo && k < 3) { a.transform.position = pos[k++]; aliados.Add(a); }
            Physics.SyncTransforms();
            W8Ok(ref ok, aliados.Count >= 2, sb, $"aliados disponibles ({aliados.Count})");
            float t0 = Time.time; bool todos = false;
            while (Time.time - t0 < 25f && !todos)
            {
                todos = true;
                foreach (var a in aliados) if (a == null || G.EnTorre(a.transform.position, out _, 0.8f) || G.NivelDe(a.transform.position) < 3) todos = false;
                if (!todos) yield return null;
            }
            W8Ok(ref ok, todos, sb, $"los aliados salen de la torre C y quedan en la azotea ({Time.time - t0:0} s; liberados={d.AliadosLiberadosDeCompuerta} traidos={d.AliadosTraidosAlCambiarDePiso})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug406a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            var d = dd[0]; var yo = PlayerInputDriver.Activo.Brain.Current;
            var aliados = new List<Soldier>();
            foreach (var a in OperacionDirector.AliadosAEvacuar(true)) if (a != null && a != yo && aliados.Count < 2) aliados.Add(a);
            if (aliados.Count < 2) { Fin("FALLO faltan aliados"); yield break; }
            aliados[0].transform.position = new Vector3(322f, 1.5f, 258f);
            aliados[1].transform.position = Vector3.Lerp(G.EntradaDeTorre(0), G.SalidaDeTorre(0), 0.5f) + Vector3.up * 0.3f;
            foreach (var x in FotoCamara("v9_406_antes.jpg", new Vector3(322f, 5f, 248f), new Vector3(322f, 1.5f, 262f), 70f)) yield return x;
            yo.transform.position = new Vector3(322f, G.Nivel(1) + 0.9f, 262f);
            typeof(OperacionDirector).GetMethod("EntrarPrimerPiso", NoPublico).Invoke(d, null);
            Physics.SyncTransforms();
            float t0 = Time.time; bool listos = false;
            while (Time.time - t0 < 40f && !listos)
            {
                listos = true;
                foreach (var a in aliados) if (a == null || G.NivelDe(a.transform.position) < 1 || G.EnTorre(a.transform.position, out _, 0.8f)) listos = false;
                if (!listos) yield return null;
            }
            W8Ok(ref ok, listos, sb, $"los dos aliados pasan al 1.er piso y salen de la torre ({Time.time - t0:0} s; traidos={d.AliadosTraidosAlCambiarDePiso} liberados={d.AliadosLiberadosDeCompuerta})");
            foreach (var x in FotoCamara("v9_406_aliados_pasan.jpg", new Vector3(322f, G.Nivel(1) + 4f, 250f), new Vector3(322f, G.Nivel(1) + 1f, 262f), 70f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 412 la orden en RTS cae sobre el piso mostrado, no sobre la losa oculta de arriba
        static IEnumerator Bug412a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            var d = dd[0]; var yo = PlayerInputDriver.Activo.Brain.Current;
            typeof(OperacionDirector).GetMethod("EntrarPrimerPiso", NoPublico).Invoke(d, null);
            yo.transform.position = new Vector3(322f, G.Nivel(1) + 0.9f, 262f);
            foreach (var a in OperacionDirector.AliadosAEvacuar(true)) if (a != null && a != yo) a.transform.position = new Vector3(318f, G.Nivel(1) + 0.9f, 262f);
            Physics.SyncTransforms();
            var rig = SP.CameraSystem.CameraRig.Instance;
            rig.SetMode(SP.CameraSystem.ControlMode.Rts, new Vector3(322f, 0f, 262f));
            foreach (var x in Esperar(1.5f)) yield return x;
            var pl = PlantasDeLaMansion.Activa;
            W8Ok(ref ok, pl != null && pl.NivelMostrado == 1, sb, $"el recorte muestra el piso 1 ({(pl != null ? pl.NivelMostrado : -9)})");
            var desde = new Vector3(322f, G.Nivel(3) + 25f, 246f); var hacia = new Vector3(322f, G.Nivel(1), 262f);
            var aim = Object.FindFirstObjectByType<AimTargeting>();
            if (aim == null) { W8Ok(ref ok, false, sb, "no hay AimTargeting en la escena"); Fin("FALLO " + sb); yield break; }
            var res = aim.Evaluate(new Ray(desde, (hacia - desde).normalized), yo);
            W8Ok(ref ok, G.NivelDe(res.Point) <= 1 && res.Point.y < G.Nivel(2) - 0.3f, sb, $"el punto de la orden NO cae sobre la losa del piso 2 ({res.Point}, tipo {res.Type})");
            rig.SetMode(SP.CameraSystem.ControlMode.Fps);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ lote 3: #397 apuntado, #405 solapes
        static IEnumerator Bug397f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            d.Embarcar();
            float t0 = Time.time; while (Time.time - t0 < 12f && (PlayerInputDriver.Activo == null || PlayerInputDriver.Activo.Vehicle == null)) yield return null;
            var pid = PlayerInputDriver.Activo; var v = pid != null ? pid.Vehicle : null;
            var rig = SP.CameraSystem.CameraRig.Instance;
            if (v == null || rig == null || rig.Cam == null) { Fin($"FALLO no se pudo montar el tanque (vehiculo={(v != null)} rig={(rig != null)})"); yield break; }
            var barrera = GameObject.CreatePrimitive(PrimitiveType.Cube); barrera.name = "Prueba397_Barrera";
            Vector3 fw = v.transform.forward; fw.y = 0f; fw.Normalize();
            barrera.transform.position = v.transform.position + fw * 6f + Vector3.up * 0.7f;
            barrera.transform.rotation = Quaternion.LookRotation(fw); barrera.transform.localScale = new Vector3(4f, 1.4f, 0.6f);
            Physics.SyncTransforms();
            var colBarrera = barrera.GetComponent<Collider>();
            float anterior = -1f; int sobreBarrera = 0, retrocesos = 0; var det = new StringBuilder();
            for (int i = 0; i <= 20; i++)
            {
                float pitch = -10f + i;   // grados hacia arriba
                var dir = Quaternion.AngleAxis(-pitch, v.transform.right) * fw;
                rig.Cam.transform.position = v.transform.position - fw * 8f + Vector3.up * 3.5f;
                rig.Cam.transform.rotation = Quaternion.LookRotation(dir);
                Physics.SyncTransforms();
                if (!pid.PuntoBajoLaMira(out var punto)) { det.Append("sin punto en ").Append(pitch).Append(" | "); continue; }
                bool enBarrera = false;
                foreach (var c in Physics.OverlapSphere(punto, 0.25f, ~0, QueryTriggerInteraction.Ignore)) if (c == colBarrera) enBarrera = true;
                if (enBarrera) { sobreBarrera++; det.Append("sobre la barrera a ").Append(pitch).Append(" | "); }
                float dist = Vector3.Distance(rig.Cam.transform.position, punto);
                if (dist < anterior - 0.5f) { retrocesos++; det.Append("retrocede a ").Append(pitch).Append(" (").Append(dist.ToString("0")).Append(") | "); }
                anterior = dist;
            }
            W8Ok(ref ok, sobreBarrera == 0, sb, $"la mira nunca se apoya en la barrera pegada al tanque ({sobreBarrera}: {det})");
            W8Ok(ref ok, retrocesos == 0, sb, $"al subir la mira el punto apuntado se aleja de forma continua ({retrocesos} retrocesos)");
            // atlas: el canon a distintas elevaciones, de costado
            var canon = v.TorretaCanon; var lado = v.transform.right;
            foreach (var pitch in new[] { -10f, -5f, 0f, 3f, 6f, 10f })
            {
                var dir = Quaternion.AngleAxis(-pitch, v.transform.right) * fw;
                rig.Cam.transform.position = v.transform.position - fw * 8f + Vector3.up * 3.5f; rig.Cam.transform.rotation = Quaternion.LookRotation(dir); Physics.SyncTransforms();
                if (pid.PuntoBajoLaMira(out var p2)) for (int k = 0; k < 40; k++) { canon.ElevarHacia(p2); yield return null; }
                foreach (var x in FotoCamara($"v9_397_pitch_{(pitch < 0 ? "m" : "p")}{Mathf.Abs(pitch):0}.jpg", v.transform.position + lado * 9f + Vector3.up * 3f, v.transform.position + fw * 2f + Vector3.up * 1.5f, 55f, 640, 360)) yield return x;
            }
            Object.Destroy(barrera);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug405a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            foreach (var x in Esperar(4f)) yield return x;   // el vigilante del parche corre cada 3 s
            var centro = new Vector3((G.X0 + G.X1) * 0.5f, G.Nivel(1), (G.Z0 + G.Z1) * 0.5f);
            var zona = new Bounds(centro, new Vector3(G.X1 - G.X0 + 6f, G.Nivel(3) + 8f, G.Z1 - G.Z0 + 6f));
            var l = SP.EditorTools.DetectorDeSolapes.Escanear(zona, 3000, 0.004f, 0.02f, true);   // solo caras verticales: las de piso/techo de dos piezas apiladas quedan tapadas por la losa
            int malos = 0; var det = new StringBuilder();
            var unicos = new Dictionary<string, int>();
            foreach (var s in l) if (!s.MismoMaterial && !s.mismaMalla && s.area >= 1f)
            {
                malos++;
                string k = "eje " + "XYZ"[s.eje] + (s.positivo ? "+" : "-") + " " + s.a.Substring(s.a.LastIndexOf('/') + 1) + " <> " + s.b.Substring(s.b.LastIndexOf('/') + 1) + " @" + s.plano.ToString("0.0") + " a=" + s.area.ToString("0.0");
                unicos[k] = unicos.TryGetValue(k, out var q) ? q + 1 : 1;
            }
            foreach (var kv in unicos) if (det.Length < 6000) det.Append(kv.Key).Append(" x").Append(kv.Value).Append(" || ");
            W8Ok(ref ok, malos == 0, sb, $"sin caras coplanares de materiales distintos en la mansion ({malos} de {l.Count}): {det}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 402 auditoria: todo lo que tiene collider cerca de la plaza o en la mansion esta clasificado
        static readonly string[] PrefijosIndestructibles =
        {
            "Ground", "Suelo", "Losa", "Muro", "Torre_", "Mansion_", "Edificio", "Calle", "Acera", "Cordon", "Vereda", "Pared", "Techo", "Piso", "Escalon", "Rampa",
            "Fusionados", "Zocalo", "Cornisa", "Medianera", "Tabique", "Pilastra", "Columna_Mansion", "Portico", "Azotea", "Jamba", "Hoja_", "Reja", "Baranda", "Parapeto", "Claraboya",
            "Estante", "Terreno", "Cielo", "Limite", "Borde", "P1_", "P2_", "PB_", "P3_", "Entrada", "Vidriera", "Escalera"
        };

        static IEnumerator Bug402a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true; var dd = new OperacionDirector[1];
            foreach (var x in ArrancarMansion(dd)) yield return x;
            foreach (var x in Esperar(4f)) yield return x;   // el vigilante del parche corre cada 3 s
            int a = 0, c = 0, d = 0, sinClasificar = 0, total = 0, sinMalla = 0;
            var det = new StringBuilder(); var vistos = new HashSet<string>();
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (col == null || col.isTrigger || !col.enabled || !DestructibilidadDelNivel.EnAlcance(col.bounds)) continue;
                if (col.GetComponent<MeshRenderer>() == null) { sinMalla++; continue; }
                total++;
                if (DestructibilidadDelNivel.YaEsDestructible(col)) { a++; continue; }
                if (DestructibilidadDelNivel.EsEstructural(col)) { c++; continue; }
                if (DestructibilidadDelNivel.TieneLogicaPropia(col)) { d++; continue; }
                sinClasificar++;
                string n = col.transform.name; string clave = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_');
                if (vistos.Add(clave) && vistos.Count <= 30) det.Append(RutaCompleta(col.transform)).Append(" | ");
            }
            W8Ok(ref ok, sinClasificar == 0, sb, $"colliders con malla cerca de la plaza o en la mansion: {total} (destructibles {a} [{DestructibilidadDelNivel.Convertidos} convertidos en esta corrida], estructura indestructible por diseno {c}, con logica propia {d}, sin clasificar {sinClasificar}; sin malla {sinMalla}): {det}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 398d foto: al menos 3 tanques enemigos a la vista detras del tanque propio, con la vineta de impacto
        static IEnumerator Bug398d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4); ModoDios.Poner(true);
            foreach (var x in Esperar(2f)) yield return x;
            var d = Object.FindFirstObjectByType<OperacionDirector>();
            d.Embarcar();
            float t0 = Time.time; while (Time.time - t0 < 12f && (PlayerInputDriver.Activo == null || PlayerInputDriver.Activo.Vehicle == null)) yield return null;
            var v = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Vehicle : null;
            if (v == null) { Fin("FALLO no se pudo montar el tanque"); yield break; }
            var crear = typeof(OperacionDirector).GetMethod("CrearAuto", NoPublico);
            for (int i = 0; i < 12; i++) { crear?.Invoke(d, null); yield return null; }
            foreach (var x in Esperar(1f)) yield return x;
            var tanques = new List<OperacionAuto>();
            foreach (var a in Object.FindObjectsByType<OperacionAuto>(FindObjectsInactive.Exclude)) if (a != null && !a.Muerto && a.EsTanque) tanques.Add(a);
            W8Ok(ref ok, tanques.Count >= 3, sb, $"hay al menos 3 tanques enemigos vivos ({tanques.Count})");
            Vector3 fw = v.transform.forward; fw.y = 0f; fw.Normalize(); var der = Vector3.Cross(Vector3.up, fw);
            for (int i = 0; i < tanques.Count && i < 4; i++)
            {
                var pos = v.transform.position + fw * (28f + 9f * i) + der * ((i % 2 == 0 ? -1f : 1f) * (3f + 2f * i));
                var ag = tanques[i].GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ag != null && ag.enabled) ag.Warp(pos); else tanques[i].transform.position = pos;
            }
            Physics.SyncTransforms();
            v.TakeDamage(60, 0);   // para que se vea la vineta de impacto
            var camPos = v.transform.position - fw * 9f + Vector3.up * 6f; var mira = v.transform.position + fw * 40f;
            var tmp = new GameObject("Prueba398d_Cam").AddComponent<Camera>(); tmp.fieldOfView = 70f; tmp.aspect = 16f / 9f; tmp.transform.position = camPos; tmp.transform.LookAt(mira);
            var planos = GeometryUtility.CalculateFrustumPlanes(tmp); int enCuadro = 0;
            foreach (var a in tanques) { var r = a.GetComponentInChildren<Renderer>(); if (r != null && GeometryUtility.TestPlanesAABB(planos, r.bounds)) enCuadro++; }
            Object.Destroy(tmp.gameObject);
            W8Ok(ref ok, enCuadro >= 3, sb, $"{enCuadro} tanques enemigos dentro del cuadro de la foto");
            foreach (var x in FotoCamara("v9_398_tanques.jpg", camPos, mira, 70f)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 700 recorrido completo instrumentado: los 6 objetivos con modo dios, mirando errores y anomalias
        static readonly Dictionary<string, int> logsDelRecorrido = new Dictionary<string, int>();
        static void CapturarLog(string msg, string stack, LogType tipo)
        {
            if (tipo != LogType.Error && tipo != LogType.Exception && tipo != LogType.Assert) return;
            string k = (tipo == LogType.Exception ? "EXC " : "ERR ") + (msg.Length > 140 ? msg.Substring(0, 140) : msg);
            int n; logsDelRecorrido.TryGetValue(k, out n); if (logsDelRecorrido.Count < 60) logsDelRecorrido[k] = n + 1;
        }

        static IEnumerator Bug700a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            logsDelRecorrido.Clear(); Application.logMessageReceived += CapturarLog;
            ModoDios.Poner(true);
            for (int o = 1; o <= OperacionDirector.TotalObjetivos; o++)
            {
                string r = OperacionPrueba.Arrancar(o);
                if (o == 5) { var dd = OperacionDirector.Instancia; }
                float t0 = Time.time, r0 = Time.realtimeSinceStartup, fpsMin = 999f; int frames = 0; float ult = Time.realtimeSinceStartup;
                var yo0 = Poseido(); Vector3 pos0 = yo0 != null ? yo0.transform.position : Vector3.zero;
                while (Time.time - t0 < 20f && Time.realtimeSinceStartup - r0 < 90f)
                {
                    yield return null; frames++;
                    float dt = Time.realtimeSinceStartup - ult; ult = Time.realtimeSinceStartup; if (dt > 0f && frames > 10) fpsMin = Mathf.Min(fpsMin, 1f / dt);
                }
                int bajoElMapa = 0, nan = 0, vivos = 0; var det = new StringBuilder();
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || !s.gameObject.activeInHierarchy) continue;
                    var p = s.transform.position;
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.y)) { nan++; continue; }
                    if (s.Health != null && s.Health.IsAlive) vivos++;
                    if (p.y < -3f && s.Health != null && s.Health.IsAlive) { bajoElMapa++; if (det.Length < 300) det.Append(s.name).Append(p).Append(' '); }
                }
                var yo = Poseido(); string mov = yo != null ? $" mov={Vector3.Distance(pos0, yo.transform.position):0}m" : " sin-poseido";
                var d = OperacionDirector.Instancia;
                sb.Append($"[obj{o} {d.Fase} vivos={vivos} fpsMin={fpsMin:0} bajoMapa={bajoElMapa}{(det.Length > 0 ? "(" + det + ")" : "")} nan={nan}{mov}] ");
                if (bajoElMapa > 0 || nan > 0 || (yo == null && d.Fase != FaseOperacion.Victoria)) ok = false;   // en el objetivo 6 el jugador ya subio al helicoptero y la partida termina en Victoria
            }
            Application.logMessageReceived -= CapturarLog;
            var errores = new StringBuilder(); int totalErr = 0;
            foreach (var kv in logsDelRecorrido) { totalErr += kv.Value; if (errores.Length < 1800) errores.Append("x").Append(kv.Value).Append(' ').Append(kv.Key.Replace('\n', ' ')).Append(" || "); }
            W8Ok(ref ok, totalErr == 0, sb, $"sin errores ni excepciones en consola durante los 6 objetivos ({totalErr}): {errores}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
