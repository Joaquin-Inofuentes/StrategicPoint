using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Core;
using SP.Operacion;
using SP.Presentation;

namespace SP.EditorTools
{
    // Ronda 19, punto 4 "Vestir exterior servidores" (Docs/PLAN_R19_v2.md, 3.4; carril F, parte F de G3). Checks deterministas y cortos:
    //   Bug994g (Play, objetivo 3): cobertura de fachada por cara (celdas de 4 m x 3 bandas, ANTES = solo R17 / DESPUES = R17 + R19), zonas libres
    //           sin piezas R19, caminos de las oleadas por NavMesh (contra la linea base con el kit R19 apagado), ruta del tanque, reaplicar sin
    //           duplicar, regla de apoyo (#501b: nada R19 apoyado solo en un destructible; conductos R17 hasta el piso), prefijos, presupuesto.
    //   Bug994h (Play, objetivo 3): mediana del costo de render mirando la cara norte desde (0, 1,7, -20), con el kit R19 apagado y prendido.
    // Correr:  return SP.EditorTools.ChecksBugs065.CorrerLista("Bug994g,Bug994h", true);
    public static partial class ChecksBugs065
    {
        const float R19eCelda = 4f, R19eAfuera = 1.5f, R19eMinimo = 0.70f, R19eTramoVacioMax = 8f;
        static readonly float[] R19eBandas = { 0f, 2.7f, 5.3f, AjustesDeNivelR19.AlturaDeLosMuros };

        static bool R19eSolapan(Bounds a, Bounds b, float m = 0.005f)
            => a.min.x < b.max.x - m && a.max.x > b.min.x + m && a.min.y < b.max.y - m && a.max.y > b.min.y + m && a.min.z < b.max.z - m && a.max.z > b.min.z + m;

        // Arma (o confirma) los parches de la fachada y devuelve los renderers de R17 y R19 por separado.
        static bool R19ePreparar(out OperacionDirector d, out Transform r17, out Transform r19, StringBuilder sb)
        {
            d = OperacionDirector.Instancia; r17 = null; r19 = null;
            if (d == null) { sb.Append("sin director; "); return false; }
            AjustesDeNivelR17.Aplicar(d);
            var g17 = GameObject.Find(AjustesDeNivelR17.GrupoFachada);
            r17 = g17 != null ? g17.transform : null;
            r19 = AjustesDeNivelR19.GrupoActual;
            if (r19 == null) { sb.Append("el grupo R19 no se armo (raiz 3_CentroDeDatos no encontrada?); "); return false; }
            return true;
        }

        static List<Renderer> R19eRenderers(Transform t)
        {
            var l = new List<Renderer>();
            if (t == null) return l;
            foreach (var r in t.GetComponentsInChildren<Renderer>(false)) if (r != null && r.enabled && !(r is ParticleSystemRenderer)) l.Add(r);
            return l;
        }

        // Cobertura de una cara: fraccion de celdas vestidas (sin contar las que caen sobre una zona libre) y el tramo mas largo con las 3 bandas vacias.
        static float R19eCobertura(int ic, List<Bounds> arte, out int vestidas, out int contadas, out float peorTramo, out string vaciasTxt)
        {
            var c = AjustesDeNivelR19.Caras[ic];
            vestidas = 0; contadas = 0; peorTramo = 0f; float tramo = 0f;
            var vacias = new List<string>();
            for (float s0 = c.A0; s0 < c.A1 - 0.01f; s0 += R19eCelda)
            {
                float s1 = Mathf.Min(s0 + R19eCelda, c.A1), ancho = s1 - s0;
                int vaciasEnColumna = 0, excluidas = 0;
                for (int b = 0; b < 3; b++)
                {
                    var celda = AjustesDeNivelR19.CajaEnCara(c, s0, s1, R19eBandas[b], R19eBandas[b + 1], -0.02f, R19eAfuera);
                    bool excluida = false;
                    foreach (var z in AjustesDeNivelR19.ZonasLibres)
                    {
                        if (z.Cara != ic || R19eBandas[b] >= z.YMax) continue;
                        float solape = Mathf.Min(s1, z.S1) - Mathf.Max(s0, z.S0);
                        if (solape >= ancho * 0.5f) { excluida = true; break; }
                    }
                    if (excluida) { excluidas++; continue; }
                    contadas++;
                    bool vestida = false;
                    foreach (var a in arte) if (R19eSolapan(celda, a)) { vestida = true; break; }
                    if (vestida) vestidas++;
                    else { vaciasEnColumna++; if (vacias.Count < 8) vacias.Add($"{s0:0}..{s1:0}/b{b + 1}"); }
                }
                if (excluidas > 0) { tramo = 0f; continue; }   // la zona libre corta el tramo (ahi no se puede vestir por regla)
                if (vaciasEnColumna == 3) { tramo += ancho; peorTramo = Mathf.Max(peorTramo, tramo); }
                else tramo = 0f;
            }
            vaciasTxt = string.Join(" ", vacias);
            return contadas > 0 ? (float)vestidas / contadas : 0f;
        }

        static bool R19eCaminoCompleto(Vector3 desde, Vector3 hasta)
        {
            if (!NavMesh.SamplePosition(desde, out var a, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(hasta, out var b, 4f, NavMesh.AllAreas)) return false;
            var p = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, p) && p.status == NavMeshPathStatus.PathComplete;
        }

        // ================================================================ 994g
        static IEnumerator Bug994g()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(2.5f)) yield return x;
            if (!R19ePreparar(out var d, out var r17, out var r19, sb)) { Fin("FALLO " + sb); yield break; }
            foreach (var x in Esperar(1.0f)) yield return x;   // carving de las coberturas nuevas
            Physics.SyncTransforms();

            var arte17 = new List<Bounds>(); foreach (var r in R19eRenderers(r17)) arte17.Add(r.bounds);
            var rend19 = R19eRenderers(r19);
            var arteAmbos = new List<Bounds>(arte17); foreach (var r in rend19) arteAmbos.Add(r.bounds);

            // ---- 1. cobertura por cara (antes = solo R17, despues = R17 + R19)
            for (int ic = 0; ic < 4; ic++)
            {
                var c = AjustesDeNivelR19.Caras[ic];
                float antes = R19eCobertura(ic, arte17, out int va, out int ca, out float ta, out _);
                float despues = R19eCobertura(ic, arteAmbos, out int vd, out int cd, out float td, out string vacias);
                W8Ok(ref ok, despues >= R19eMinimo && td <= R19eTramoVacioMax, sb,
                    $"cara {c.Nombre}: vestidas {vd}/{cd} = {despues:P0} (antes {va}/{ca} = {antes:P0}); tramo vacio mas largo {td:0.0} m (antes {ta:0.0})" + (vacias.Length > 0 ? $" vacias [{vacias}]" : ""));
            }

            // ---- 2. zonas libres: ni renderers ni colliders R19 dentro de las 4 aberturas
            int enZona = 0; var quien = new List<string>();
            foreach (var z in AjustesDeNivelR19.ZonasLibres)
            {
                var caja = AjustesDeNivelR19.CajaDeZona(z);
                foreach (var r in rend19) if (R19eSolapan(caja, r.bounds, 0.01f)) { enZona++; if (quien.Count < 6) quien.Add(r.name); }
                foreach (var col in r19.GetComponentsInChildren<Collider>(false)) if (col.enabled && R19eSolapan(caja, col.bounds, 0.01f)) { enZona++; if (quien.Count < 6) quien.Add(col.name + "(collider)"); }
            }
            W8Ok(ref ok, enZona == 0, sb, $"{enZona} piezas R19 dentro de las zonas libres (puertas sur/laterales, salida norte)" + (quien.Count > 0 ? ": " + string.Join(", ", quien) : ""));

            // ---- 3. caminos de las oleadas hasta (0, -54): con el kit R19 prendido, contra la linea base con el kit apagado
            var origenes = new[] { new Vector3(0f, 0f, -104f), new Vector3(-49f, 0f, -60f), new Vector3(49f, 0f, -60f), new Vector3(0f, 0f, -90f) };
            var destino = new Vector3(0f, 0f, -54f);
            var con = new bool[origenes.Length];
            for (int i = 0; i < origenes.Length; i++) con[i] = R19eCaminoCompleto(origenes[i], destino);
            r19.gameObject.SetActive(false);
            foreach (var x in Esperar(0.6f)) yield return x;
            var sin = new bool[origenes.Length];
            for (int i = 0; i < origenes.Length; i++) sin[i] = R19eCaminoCompleto(origenes[i], destino);
            r19.gameObject.SetActive(true);
            foreach (var x in Esperar(0.6f)) yield return x;
            int rotos = 0, previos = 0; var det = new List<string>();
            for (int i = 0; i < origenes.Length; i++)
            {
                det.Add($"({origenes[i].x:0},{origenes[i].z:0})={(con[i] ? "ok" : "NO")}{(sin[i] ? "" : "/previo NO")}");
                if (!con[i] && sin[i]) rotos++;
                if (!con[i] && !sin[i]) previos++;
            }
            W8Ok(ref ok, rotos == 0, sb, $"caminos al destino de las oleadas: {rotos} cortados por el kit R19, {previos} ya cortados sin el kit [{string.Join(" ", det)}]");

            // ---- 4. ruta del tanque: ningun collider R19 en el camino
            int enRuta = 0;
            foreach (var col in AjustesDeNivelOpus.BloqueosDeLaRuta(d)) if (col != null && col.transform.IsChildOf(r19)) enRuta++;
            W8Ok(ref ok, enRuta == 0, sb, $"{enRuta} colliders R19 en la ruta del tanque");

            // ---- 5. reaplicar no duplica
            int obj = AjustesDeNivelR19.ObjetosCreados, cons = AjustesDeNivelR19.Construcciones;
            AjustesDeNivelR17.Aplicar(d); AjustesDeNivelR19.Aplicar();
            int grupos = 0;
            if (r19.parent != null) foreach (Transform h in r19.parent) if (h.name == AjustesDeNivelR19.GrupoExterior) grupos++;
            W8Ok(ref ok, grupos == 1 && AjustesDeNivelR19.Construcciones == cons && AjustesDeNivelR19.ObjetosCreados == obj, sb,
                $"reaplicar: {grupos} grupo(s) {AjustesDeNivelR19.GrupoExterior}, construcciones {cons}->{AjustesDeNivelR19.Construcciones}, objetos {obj}->{AjustesDeNivelR19.ObjetosCreados}");

            // ---- 6. apoyo (#501b): ninguna pieza R19 suelta (fuera de una cobertura) apoyada SOLO en destructibles; conductos R17 hasta el piso
            int dependientes = 0; var dep = new List<string>();
            foreach (var r in rend19)
            {
                if (r.GetComponentInParent<ObstacleMarker>() != null) continue;   // hija de su cobertura: se va con ella
                var b = r.bounds;
                if (b.min.y < 0.35f) continue;
                var centro = new Vector3(b.center.x, b.min.y - 0.12f, b.center.z);
                var mitad = new Vector3(Mathf.Max(b.extents.x * 0.9f, 0.05f), 0.2f, Mathf.Max(b.extents.z * 0.9f, 0.05f));
                int soportes = 0, destructibles = 0;
                foreach (var col in Physics.OverlapBox(centro, mitad, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col == null || !col.enabled || col.transform == r.transform || col.transform.IsChildOf(r.transform)) continue;
                    soportes++;
                    var om = col.GetComponentInParent<ObstacleMarker>();
                    if (om != null && om.EsDestruible) destructibles++;
                }
                if (soportes > 0 && soportes == destructibles) { dependientes++; if (dep.Count < 6) dep.Add($"{r.name} y={b.min.y:0.0}"); }
            }
            int conductos = 0, conductosAlPiso = 0;
            foreach (var r in R19eRenderers(r17)) if (r.name == "Conducto_Vertical") { conductos++; if (r.bounds.min.y < 0.35f) conductosAlPiso++; }
            W8Ok(ref ok, dependientes == 0, sb, $"{dependientes} piezas R19 apoyadas solo en destructibles" + (dep.Count > 0 ? ": " + string.Join(", ", dep) : ""));
            W8Ok(ref ok, conductos == conductosAlPiso, sb, $"conductos R17 hasta el piso: {conductosAlPiso}/{conductos}");

            // ---- 7. nombres, presupuesto, materiales y luces
            int prohibidos = 0;
            foreach (var r in rend19) foreach (var p in AjustesDeNivelR19.PrefijosProhibidos) if (r.name.StartsWith(p, System.StringComparison.Ordinal)) { prohibidos++; break; }
            var mats = new HashSet<string>();
            foreach (var r in rend19) foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("R19_", System.StringComparison.Ordinal)) mats.Add(m.name);
            int luces = 0, lucesConSombra = 0;
            foreach (var l in r19.GetComponentsInChildren<Light>(false)) { luces++; if (l.shadows != LightShadows.None) lucesConSombra++; }
            W8Ok(ref ok, prohibidos == 0, sb, $"{prohibidos} piezas R19 con prefijos que SalaDespejadaEnRts trata aparte");
            W8Ok(ref ok, AjustesDeNivelR19.ObjetosCreados <= 240 && mats.Count <= 6, sb, $"presupuesto: {AjustesDeNivelR19.ObjetosCreados} objetos (<= 240), {mats.Count} materiales R19 nuevos (<= 6): {string.Join(",", mats)}");
            W8Ok(ref ok, luces <= 2 && lucesConSombra == 0, sb, $"{luces} luces reales R19 (<= 2), {lucesConSombra} con sombra");
            sb.Append($"\n  kit: pilastras {AjustesDeNivelR19.Pilastras} (pilares del kit modular {AjustesDeNivelR19.PilaresDelKit}), rejas {AjustesDeNivelR19.Rejas}, luminarias {AjustesDeNivelR19.Luminarias}, coberturas {AjustesDeNivelR19.CoberturasCreadas}, solidos {AjustesDeNivelR19.SolidosCreados}, omitidas {AjustesDeNivelR19.PiezasOmitidas}, obstaculos recogidos {AjustesDeNivelR19.ObstaculosRecogidos}, renderers {rend19.Count}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ================================================================ 994h
        static IEnumerator Bug994h()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(3, 1);
            foreach (var x in Esperar(2.5f)) yield return x;
            if (!R19ePreparar(out _, out _, out var r19, sb)) { Fin("FALLO " + sb); yield break; }
            foreach (var x in Esperar(0.5f)) yield return x;

            var rend = R19eRenderers(r19);
            var lights = new List<Light>(r19.GetComponentsInChildren<Light>(false));
            void Kit(bool on) { foreach (var r in rend) if (r != null) r.enabled = on; foreach (var l in lights) if (l != null) l.enabled = on; }

            var go = new GameObject("R19eCamara"); var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f; cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.05f; cam.farClipPlane = 800f;
            var principal = CamaraPrincipal.Actual; cam.cullingMask = principal != null ? principal.cullingMask : ~(1 << 8);
            go.transform.position = new Vector3(0f, 1.7f, -20f); go.transform.LookAt(new Vector3(0f, 4f, AjustesDeNivelR19.CaraNorte));
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt;
            var sinKit = new List<double>(); var conKit = new List<double>();
            var sw = new Stopwatch();
            try
            {
                for (int i = 0; i < 10; i++) cam.Render();   // calentar
                for (int ronda = 0; ronda < 6; ronda++)
                {
                    bool on = ronda % 2 == 1;
                    Kit(on);
                    cam.Render();
                    for (int i = 0; i < 30; i++) { sw.Restart(); cam.Render(); sw.Stop(); (on ? conKit : sinKit).Add(sw.Elapsed.TotalMilliseconds); }
                }
            }
            finally
            {
                Kit(true);
                cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            }
            sinKit.Sort(); conKit.Sort();
            double mSin = sinKit[sinKit.Count / 2], mCon = conKit[conKit.Count / 2];
            double ratio = mSin > 0 ? mCon / mSin : 1;
            // tolerancia del plan: +3 %; con piso absoluto de 0,10 ms (el render de una camara en el Editor tiene ruido de ese orden)
            W8Ok(ref ok, ratio <= 1.03 || mCon - mSin <= 0.10, sb, $"render de la cara norte desde (0, 1,7, -20): mediana sin kit {mSin:0.000} ms, con kit {mCon:0.000} ms ({(ratio - 1) * 100:+0.0;-0.0} %), {rend.Count} renderers R19, {lights.Count} luces");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
