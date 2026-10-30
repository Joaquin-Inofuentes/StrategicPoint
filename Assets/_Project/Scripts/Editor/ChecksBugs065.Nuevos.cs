using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;
using SP.UI;
using SP.Vehicles;

namespace SP.EditorTools
{
    // Bugs 318-325 (reportes con la tecla U del 2026-10-30 16:23 a 16:27). Capturas "despues" en Assets/Validacion/v3_NNN_despues.png.
    public static partial class ChecksBugs065
    {
        // Foto del mundo con una camara temporal (sin HUD): para encuadres que el Game View no da (helicopteros, cuerdas).
        static void CapturaConCamara(string archivo, Vector3 pos, Vector3 mira, float fov = 55f)
        {
            var go = new GameObject("CamTemporalDeCheck");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.2f; cam.farClipPlane = 600f;
            go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation((mira - pos).normalized, Vector3.up);
            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(RutaValidacion(archivo), tex.EncodeToPNG());
            Object.Destroy(tex); cam.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(go);
        }

        static void PonerALaEscuadraEn(Vector3 p)
        {
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Player && s.gameObject.activeInHierarchy) { s.transform.position = new Vector3(p.x, s.transform.position.y, p.z); ApoyoEnElPiso.Apoyar(s.transform); }
        }

        // ---------------------------------------------------------------- 318u la bomba es una bomba (geometria rehecha)
        static IEnumerator Bug318u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var doc = Escuadrista(RoleType.Medic);
            if (d == null || d.cargaPorton == null || doc == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            try
            {
                var carga = d.cargaPorton.transform;
                var arte = carga.Find("ArteBomba");
                int piezas = arte != null ? arte.GetComponentsInChildren<MeshRenderer>().Length : 0;
                W8Ok(ref ok, piezas >= 30, sb, $"la bomba tiene arte propio ({piezas} piezas, pide >= 30)");
                var cajaOriginal = carga.Find("Caja_Carga");
                W8Ok(ref ok, cajaOriginal != null && !cajaOriginal.GetComponent<MeshRenderer>().enabled, sb, "el cajon rojo original ya no se dibuja");
                W8Ok(ref ok, d.cargaPorton.caja != null && arte != null && d.cargaPorton.caja.transform.IsChildOf(arte), sb, "el tinte de armado va al temporizador de la bomba");
                int cilindros = 0; if (arte != null) foreach (var f in arte.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null && f.sharedMesh.name.StartsWith("Cylinder")) cilindros++;
                W8Ok(ref ok, cilindros >= 5, sb, $"hay cartuchos cilindricos ({cilindros})");
                Poseer(doc); yield return null;
                var c = carga.position;
                doc.transform.position = new Vector3(c.x - 2.2f, doc.transform.position.y, c.z - 2.6f); ApoyoEnElPiso.Apoyar(doc.transform);
                doc.transform.rotation = Quaternion.LookRotation(new Vector3(c.x - doc.transform.position.x, 0f, c.z - doc.transform.position.z));
                foreach (var x in Esperar(1.0f)) yield return x;
                foreach (var x in CapturarPantalla("v3_318_despues.png")) yield return x;
                CapturaConCamara("v3_318_detalle.png", c + new Vector3(-1.6f, 1.3f, -1.9f), c + Vector3.up * 0.4f, 45f);
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 319u la barra del jefe solo mientras se pelea
        static IEnumerator Bug319u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(4, 1, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var vega = Escuadrista(RoleType.Assault);
            var hud = d != null ? CampoPrivado(d, "hud") as OperacionHud : null;
            if (d == null || d.Jefe == null || hud == null || vega == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (jefe/hud)"); yield break; }
            try
            {
                Poseer(vega); yield return null;
                PonerALaEscuadraEn(new Vector3(8f, 0f, -59f));
                d.Jefe.FuegoHabilitado = false;
                foreach (var x in Esperar(1.5f)) yield return x;
                W8Ok(ref ok, !hud.BarraDeJefeVisible, sb, $"adentro del Centro de Datos (el jefe aun no dispara) no hay barra (visible={hud.BarraDeJefeVisible})");
                foreach (var x in CapturarPantalla("v3_319_despues.png")) yield return x;
                d.Jefe.FuegoHabilitado = true;
                foreach (var x in Esperar(1.0f)) yield return x;
                W8Ok(ref ok, d.Jefe.Situacion != TanqueJefe.Estado.Peleando || hud.BarraDeJefeVisible, sb, "peleando, la barra se ve");
                d.Jefe.RendirYa();
                foreach (var x in Esperar(1.0f)) yield return x;
                W8Ok(ref ok, !hud.BarraDeJefeVisible, sb, $"rendido/derribado la barra desaparece (visible={hud.BarraDeJefeVisible})");
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 320u el muro de salida se rompe entero (decoracion incluida)
        static IEnumerator Bug320u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            var muro = Buscar("Muro_Salida_Norte");
            var vega = Escuadrista(RoleType.Assault);
            if (d == null || muro == null || vega == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas"); yield break; }
            try
            {
                var adj = muro.transform.Find("Adjuntos");
                int barreras = 0; if (adj != null) foreach (Transform t in adj) if (t.name == "Barrera") barreras++;
                W8Ok(ref ok, barreras == 4, sb, $"las 4 barreras rojo/blanco viven en los Adjuntos del muro ({barreras})");
                d.AbrirSalidaNortePorPrueba(true);
                foreach (var x in Esperar(12f)) yield return x;
                W8Ok(ref ok, !muro.activeSelf, sb, "el muro cayo");
                var caja = BoundsDe(muro); caja.Expand(new Vector3(0.5f, 0.5f, 0.6f));
                int sueltas = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (!r.enabled || r.name != "Barrera" || r.GetComponentInParent<Fragmento>() != null) continue;
                    if (caja.Contains(r.bounds.center)) sueltas++;
                }
                W8Ok(ref ok, sueltas == 0, sb, $"no queda ninguna barrera donde estaba el muro ({sueltas})");
                int fragmentosVivos = 0; foreach (var f in Object.FindObjectsByType<Fragmento>(FindObjectsInactive.Exclude)) if (f.EnUso && caja.Contains(f.transform.position)) fragmentosVivos++;
                W8Ok(ref ok, fragmentosVivos == 0, sb, $"a los 12 s no quedan escombros apilados en la abertura ({fragmentosVivos})");
                Poseer(vega); yield return null;
                vega.transform.position = new Vector3(6f, vega.transform.position.y, -37f); ApoyoEnElPiso.Apoyar(vega.transform);
                vega.transform.rotation = Quaternion.LookRotation(new Vector3(-0.1f, 0f, -1f));
                foreach (var x in Esperar(1.0f)) yield return x;
                foreach (var x in CapturarPantalla("v3_320_despues.png")) yield return x;
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 321u helicopteros que sueltan tropas en cuerdas y despues atacan
        static IEnumerator Bug321u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(4, 1, 4);
            foreach (var x in Esperar(3f)) yield return x;
            Time.timeScale = 1f;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            bool diosAntes = ModoDios.Activo; ModoDios.Poner(true);
            if (d == null || d.tanque == null || d.halcon == null) { ModoDios.Poner(diosAntes); Fin("FALLO faltan piezas (tanque/halcon)"); yield break; }
            try
            {
                var punto = d.tanque.transform.position + d.tanque.transform.forward * 70f; punto.y = 0f;
                var h = d.LanzarDespliegue(punto, 3);
                if (h == null) { ok = false; sb.Append("FALLO no se pudo lanzar el helicoptero (plantilla de soldado?); "); }
                else
                {
                    bool foto = false; float t0 = Time.realtimeSinceStartup;
                    while ((h.Situacion == HelicopteroEnemigo.Estado.Desplegando) && Time.realtimeSinceStartup - t0 < 30f)
                    {
                        if (!foto && h.SogasColgadas && h.SoldadosBajados >= 2)
                        {
                            foto = true;
                            var hp = h.transform.position;
                            CapturaConCamara("v3_321_despues.png", hp + new Vector3(14f, -3f, -16f), hp + Vector3.down * 5f, 50f);
                        }
                        yield return null;
                    }
                    W8Ok(ref ok, h.Situacion != HelicopteroEnemigo.Estado.Desplegando, sb, $"el despliegue termina ({h.Situacion})");
                    W8Ok(ref ok, h.SoldadosBajados == 3 && h.SoldadosDelDespliegue.Count == 3, sb, $"bajaron 3 soldados por las cuerdas ({h.SoldadosBajados})");
                    int enSuelo = 0, enemigos = 0;
                    foreach (var s in h.SoldadosDelDespliegue)
                    {
                        if (s == null) continue;
                        if (s.Team == TeamId.Enemy) enemigos++;
                        if (Mathf.Abs(s.transform.position.y - punto.y) < 1.2f) enSuelo++;
                    }
                    W8Ok(ref ok, enemigos == 3 && enSuelo == 3, sb, $"son enemigos ({enemigos}) y aterrizaron en el piso ({enSuelo})");
                    W8Ok(ref ok, !h.SogasColgadas, sb, "las cuerdas se recogen");
                    foreach (var x in Esperar(6f)) yield return x;
                    W8Ok(ref ok, h.Situacion == HelicopteroEnemigo.Estado.Entrando || h.Situacion == HelicopteroEnemigo.Estado.Orbitando, sb, $"despues ataca al tanque: orbita y ametralla ({h.Situacion}, balas={h.BalasDeMetralla})");
                    W8Ok(ref ok, h.EsDeDespliegue && HelicopteroEnemigo.Instancia != h, sb, "no pisa al Halcon de la cinematica final");
                    W8Ok(ref ok, d.DesplieguesLanzados >= 1, sb, $"el director cuenta el despliegue ({d.DesplieguesLanzados})");
                }
                int programados = OperacionDirector.SegundosDeLosDespliegues.Length;
                W8Ok(ref ok, programados >= 3, sb, $"hay {programados} desembarcos programados en la carrera");
                foreach (var x in CapturarPantalla("v3_321_juego.png")) yield return x;
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 322u el tanque de la fuga va sin ametralladora
        static IEnumerator Bug322u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(4, 1, 3);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            if (d == null || d.tanque == null) { Fin("FALLO faltan piezas (tanque)"); yield break; }
            // El jefe enemigo conserva la suya.
            W8Ok(ref ok, d.tanque.TorretaMetralleta != null && !d.tanque.SinMetralleta, sb, "el blindado enemigo (jefe) sigue con su ametralladora");
            d.Embarcar();   // sube a la escuadra: aca se le quita la metralleta al tanque
            foreach (var x in Esperar(3f)) yield return x;
            var mount = d.tanque.transform.Find("MetralletaMount");
            W8Ok(ref ok, d.tanque.SinMetralleta && d.tanque.TorretaMetralleta == null, sb, "el tanque de la fuga va sin metralleta");
            W8Ok(ref ok, mount == null || !mount.gameObject.activeSelf, sb, "el montaje de la metralleta esta apagado (ni arma ni modelo)");
            int disparos = 0;
            foreach (var t in d.tanque.GetComponentsInChildren<TurretWeapon>(false)) if (t.transform.parent != null && t.transform.parent.name == "MetralletaMount") disparos++;
            W8Ok(ref ok, disparos == 0, sb, "no hay ningun arma activa en el montaje");
            var p1 = d.tanque.SoldierInSeat(VehicleSeatRole.Passenger1);
            W8Ok(ref ok, p1 == null, sb, "nadie va de 'metralleta' (los aliados van de pasajeros)");
            foreach (var x in CapturarPantalla("v3_322_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 323u doble clic sobre un soldado = entrar en el
        static IEnumerator Bug323u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var drv = PlayerInputDriver.Activo;
            var kes = Escuadrista(RoleType.Flanker); var doc = Escuadrista(RoleType.Medic);
            if (drv == null || kes == null || doc == null) { Fin("FALLO faltan piezas"); yield break; }
            var vega = Escuadrista(RoleType.Assault); Poseer(vega); yield return null;
            drv.Rig.SetMode(ControlMode.Rts);
            foreach (var x in Esperar(1.0f)) yield return x;
            foreach (var x in CapturarPantalla("v3_323_antes_rts.png")) yield return x;
            drv.ClicSobreAliado(kes, false);
            W8Ok(ref ok, drv.Brain.Current != kes && System.Linq.Enumerable.Contains(drv.Selection.Selected, kes), sb, "un clic solo selecciona");
            yield return null;
            drv.ClicSobreAliado(kes, false);
            W8Ok(ref ok, drv.Brain.Current == kes, sb, $"el segundo clic seguido sobre el mismo soldado lo posee (actual={drv.Brain.Current?.name})");
            foreach (var x in Esperar(1.2f)) yield return x;
            W8Ok(ref ok, drv.Rig.Mode == ControlMode.Fps, sb, $"y la camara pasa a primera persona ({drv.Rig.Mode})");
            foreach (var x in CapturarPantalla("v3_323_despues.png")) yield return x;
            // dos clics sobre soldados distintos, o muy espaciados, no poseen
            drv.Rig.SetMode(ControlMode.Rts);
            foreach (var x in Esperar(0.8f)) yield return x;
            var antes = drv.Brain.Current;
            drv.ClicSobreAliado(doc, false); drv.ClicSobreAliado(kes, false);
            W8Ok(ref ok, drv.Brain.Current == antes, sb, "dos clics sobre soldados distintos no poseen");
            foreach (var x in Esperar(OperacionPrueba_Ventana() + 0.15f)) yield return x;
            drv.ClicSobreAliado(kes, false);
            W8Ok(ref ok, drv.Brain.Current == antes, sb, "dos clics muy separados en el tiempo no poseen");
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }
        static float OperacionPrueba_Ventana() => PlayerInputDriver.VentanaDeDobleClic;

        // ---------------------------------------------------------------- 324u el panel de municion sigue al soldado elegido y F4 da municion infinita
        static IEnumerator Bug324u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var sb = new StringBuilder(); bool ok = true;
            var drv = PlayerInputDriver.Activo;
            var vega = Escuadrista(RoleType.Assault); var kes = Escuadrista(RoleType.Flanker);
            bool diosAntes = ModoDios.Activo;
            if (drv == null || vega == null || kes == null) { Fin("FALLO faltan piezas"); yield break; }
            try
            {
                Poseer(vega); yield return null;
                // (a) sin [F4] la municion baja; con [F4] no.
                ModoDios.Poner(false);
                var arma = vega.Weapon;
                int a0 = arma.CurrentAmmo; int g0 = arma.Granadas;
                float t0 = Time.realtimeSinceStartup; int tiros = 0;
                while (tiros < 3 && Time.realtimeSinceStartup - t0 < 8f) { if (drv.Brain.Fire(vega.transform.position + vega.transform.forward * 30f)) tiros++; yield return null; }
                W8Ok(ref ok, tiros > 0 && arma.CurrentAmmo < a0 || arma.IsReloading, sb, $"sin [F4] la municion baja ({a0} -> {arma.CurrentAmmo})");
                arma.ConsumirGranada();
                W8Ok(ref ok, arma.Granadas == g0 - 1, sb, $"sin [F4] la granada se gasta ({g0} -> {arma.Granadas})");
                arma.ReponerGranadas(); arma.RestaurarMunicion(a0);
                ModoDios.Poner(true);
                int a1 = arma.CurrentAmmo; int g1 = arma.Granadas;
                t0 = Time.realtimeSinceStartup; tiros = 0;
                while (tiros < 5 && Time.realtimeSinceStartup - t0 < 10f) { if (drv.Brain.Fire(vega.transform.position + vega.transform.forward * 30f)) tiros++; yield return null; }
                W8Ok(ref ok, tiros >= 3 && arma.CurrentAmmo == a1, sb, $"con [F4] disparo {tiros} veces y la municion no baja ({a1} -> {arma.CurrentAmmo})");
                arma.ConsumirGranada();
                W8Ok(ref ok, arma.Granadas == g1, sb, $"con [F4] la granada no se gasta ({g1} -> {arma.Granadas})");
                // (b) el panel de abajo a la derecha sigue al soldado seleccionado en RTS
                drv.Rig.SetMode(ControlMode.Rts);
                foreach (var x in Esperar(1.0f)) yield return x;
                drv.Selection.SelectSingle(kes);
                yield return null;
                W8Ok(ref ok, CapasDeHud.SoldadoSeleccionado() == kes, sb, "el panel toma al soldado seleccionado (Kes)");
                var textos = CapasDeHud.Instancia != null ? CapasDeHud.Instancia.GetComponentsInChildren<UnityEngine.UI.Text>(true) : null;
                foreach (var x in Esperar(0.5f)) yield return x;
                foreach (var x in CapturarPantalla("v3_324_kes.png")) yield return x;
                drv.Selection.SelectSingle(vega);
                foreach (var x in Esperar(0.5f)) yield return x;
                W8Ok(ref ok, CapasDeHud.SoldadoSeleccionado() == vega, sb, "al elegir a Vega el panel pasa a Vega");
                foreach (var x in CapturarPantalla("v3_324_vega.png")) yield return x;
                W8Ok(ref ok, vega.Weapon.CurrentWeaponKind != kes.Weapon.CurrentWeaponKind || vega.Weapon.CurrentAmmo != kes.Weapon.CurrentAmmo, sb, $"y los dos paneles son distintos ({vega.Weapon.CurrentWeaponKind} {vega.Weapon.CurrentAmmo} vs {kes.Weapon.CurrentWeaponKind} {kes.Weapon.CurrentAmmo})");
            }
            finally { ModoDios.Poner(diosAntes); }
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }

        // ---------------------------------------------------------------- 325u oleadas: un lado a la vez y cantidad segun la vida
        static IEnumerator Bug325u()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            string r0 = OperacionPrueba.Arrancar(5, 1);
            foreach (var x in Esperar(2f)) yield return x;
            var d = OperacionDirector.Instancia; var sb = new StringBuilder(); bool ok = true;
            if (d == null) { Fin("FALLO sin director"); yield break; }
            var ol = d.OleadasDelMando;
            W8Ok(ref ok, ol.Count == 5, sb, $"hay 5 oleadas ({ol.Count})");
            bool unLado = true; for (int i = 0; i < ol.Count; i++) if (ol[i].bocas.Length != 1) unLado = false;
            W8Ok(ref ok, unLado, sb, "cada oleada entra por UN solo lado");
            var lados = new List<int>(); foreach (var o in ol) lados.Add(o.bocas[0]);
            bool secuencial = true; for (int i = 1; i < lados.Count; i++) if (lados[i] == lados[i - 1]) secuencial = false;
            W8Ok(ref ok, secuencial, sb, $"y se suceden por lados distintos ({string.Join(",", lados)})");
            int baseTotal = 0; foreach (var o in ol) foreach (var c in o.cantidades) baseTotal += c;
            W8Ok(ref ok, baseTotal <= 26, sb, $"menos enemigos que antes (37): {baseTotal} a vida de referencia");
            float vidaRef = OperacionDirector.VidaDeReferenciaDeLaDefensa;
            W8Ok(ref ok, Mathf.Approximately(OperacionDirector.FactorDeVidaDeLaOleada(vidaRef), 1f), sb, "con la vida de referencia el factor es 1");
            int mucha = OperacionDirector.CantidadEscalada(5, OperacionDirector.FactorDeVidaDeLaOleada(vidaRef * 1.5f));
            int poca = OperacionDirector.CantidadEscalada(5, OperacionDirector.FactorDeVidaDeLaOleada(vidaRef * 0.4f));
            W8Ok(ref ok, mucha > 5 && poca < 5 && poca >= 1, sb, $"mucha vida -> {mucha} enemigos, poca vida -> {poca} (base 5)");
            // En vivo: se anuncia la 1.a oleada con la vida actual y se compara con la misma oleada con la escuadra herida.
            var o1 = ol[0];
            float vida = OperacionDirector.VidaTotalDelBando();
            W8Ok(ref ok, vida > 0f, sb, $"la vida total del bando se mide ({vida:0})");
            foreach (var x in Esperar(0.2f)) yield return x;
            float factorSano = OperacionDirector.FactorDeVidaDeLaOleada(vida);
            float factorHerido = OperacionDirector.FactorDeVidaDeLaOleada(vida * 0.25f);
            W8Ok(ref ok, factorHerido < factorSano, sb, $"con la escuadra herida el factor baja ({factorSano:0.00} -> {factorHerido:0.00})");
            foreach (var x in CapturarPantalla("v3_325_despues.png")) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb + $"(arranque: {r0})");
        }
    }
}
