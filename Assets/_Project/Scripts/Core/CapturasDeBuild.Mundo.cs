using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.Core
{
    // R13 T0: tomas del mundo (inicio, reservas, salida norte, patio del tanque, plaza, camion, desierto, huida, compuerta, alejamiento).
    public sealed partial class CapturasDeBuild
    {
        static readonly Vector3 CentroInicio = new Vector3(0f, 0f, -345f);

        // ---------------------------------------------------------------- 1-4. inicio y porton
        IEnumerator Inicio()
        {
            Saltar(1); yield return Espera(3f);
            D("inicio_cenital", "camion_tambor_60m", ContarPorNombre(CentroInicio, 60f, "*camion", "*tambor", "*barril"));
            D("inicio_cenital", "camion_tambor_90m", ContarPorNombre(CentroInicio, 90f, "*camion", "*tambor", "*barril"));
            int rend = 0; foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (r != null && r.enabled && !(r is ParticleSystemRenderer) && (r.bounds.center - CentroInicio).magnitude < 120f) rend++;
            D("inicio_cenital", "render_aprox", rend);
            yield return Foto("inicio_cenital", new Vector3(0f, 95f, -345f), new Vector3(0f, 0f, -345f), 60f, true, Vector3.forward);
            yield return Foto("inicio_ojo", new Vector3(0f, 1.7f, -378f), new Vector3(0f, 1.5f, -340f), 60f);
            yield return Espera(1f);
            var tx = TextosActivos("OBJETIVO 1", "PORT", "GENERADOR");
            D("inicio_hud", "objetivo1", tx.Count > 0 ? tx[0] : "n/a");
            D("inicio_hud", "textos_objetivo", tx.Count);
            var cargas = FindObjectsByType<CargaExplosiva>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool hab = false; foreach (var c in cargas) if (c != null && c.Habilitado) hab = true;
            D("inicio_hud", "cargas", cargas.Length); D("inicio_hud", "carga_habilitada", hab);
            yield return FotoJuego("inicio_hud");
            var gens = FindObjectsByType<GeneradorDelCuartel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            D("porton1", "generador_activo", gens.Length);
            D("porton1", "camiones_activos", ContarPorNombre("Camion_"));
            yield return Foto("porton1", new Vector3(14.5f, 5f, -238f), new Vector3(0f, 2f, -222f), 55f);
        }

        // ---------------------------------------------------------------- 5-6. reservas: puerta del cubiculo
        IEnumerator Reservas()
        {
            Saltar(2); yield return Espera(3f);
            D("reserva_O", "barreras", ContarPorNombre("Barrera_Reserva_SoloEnemigos"));
            // "Reservas" agrupa por tramo (Muralla, CentroDeDatos...): las de esta fase son las hojas de Reservas/Muralla
            Transform raiz = null; foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t != null && t.name == "Muralla" && t.parent != null && t.parent.name == "Reservas") { raiz = t; break; }
            int total = 0, sin = 0; var sb = new StringBuilder();
            var yo = Yo(); var destino = yo != null ? yo.transform.position : Vector3.zero;
            if (raiz != null)
                foreach (Transform r in raiz)
                {
                    total++;
                    var path = new NavMeshPath();
                    bool hay = NavMesh.SamplePosition(r.position, out var h, 4f, NavMesh.AllAreas) && NavMesh.CalculatePath(h.position, EnMalla(destino), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                    if (!hay) { sin++; sb.Append(r.name).Append(' '); }
                }
            D("reserva_O", "reservas", total); D("reserva_O", "sin_ruta", sin); D("reserva_O", "sin_ruta_lista", sb.Length > 0 ? sb.ToString().Trim() : "-");
            D("reserva_E", "barreras", ContarPorNombre("Barrera_Reserva_SoloEnemigos")); D("reserva_E", "sin_ruta", sin);
            yield return Foto("reserva_O", new Vector3(-45.6f, 5f, -186f), new Vector3(-45.6f, 1.2f, -175f), 55f);
            yield return Foto("reserva_E", new Vector3(45.6f, 5f, -186f), new Vector3(45.6f, 1.2f, -175f), 55f);
        }

        // ---------------------------------------------------------------- 7-9. salida norte y patio del tanque
        IEnumerator SalidaNorteYPatio()
        {
            Saltar(4); yield return Espera(3f);
            D("salida_norte_cenital", "calle_t", ExisteActivo("Calle_T_Transversal") || ExisteActivo("R13_Calles"));
            D("salida_norte_cenital", "cordones", ContarPorNombre("R13_Cordon_"));
            yield return Foto("salida_norte_cenital", new Vector3(0f, 80f, -12f), new Vector3(0f, 0f, -12f), 60f, true, Vector3.forward);
            yield return Foto("salida_norte_ojo", new Vector3(0f, 1.7f, -37f), new Vector3(0f, 1.2f, 0f), 65f);
            // patio del tanque: cubos grises grandes (losa) cerca de (0,22)
            int grises = 0; var lista = new StringBuilder();
            foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                var b = mr.bounds; if (b.size.x < 10f || b.size.z < 10f || b.size.x > 80f || b.size.z > 80f || b.size.y > 6f) continue;
                if (Mathf.Abs(b.center.x) > 40f || Mathf.Abs(b.center.z - 22f) > 40f) continue;
                var mat = mr.sharedMaterial; if (mat == null) continue;
                Color c = Color.magenta; if (mat.HasProperty("_BaseColor")) c = mat.GetColor("_BaseColor"); else if (mat.HasProperty("_Color")) c = mat.color;
                float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if ((mx - mn < 0.2f && mx > 0.1f && mx < 0.9f) || mr.name == "Plaza_Cemento") { grises++; lista.Append(mr.name).Append('(').Append(F(b.size.x)).Append('x').Append(F(b.size.z)).Append(") "); }
            }
            D("patio_tanque", "cubos_grises_grandes", grises); D("patio_tanque", "lista", lista.Length > 0 ? lista.ToString().Trim() : "-");
            D("patio_tanque", "plaza_cemento", ExisteActivo("Plaza_Cemento"));
            yield return Foto("patio_tanque", new Vector3(0f, 16f, -6f), new Vector3(0f, 0f, 22f), 60f);
        }

        // ---------------------------------------------------------------- 31-35. plaza, mobiliario y entrada de la ciudad
        IEnumerator PlazaYCiudad()
        {
            Saltar(5); yield return Espera(2f);
            int adoq = ContarPorNombre("Adoquin"), losa = ContarPorNombre("Patio_Losa");
            int rendCaja = 0; var caja = new Bounds(new Vector3(322f, 0f, 228f), new Vector3(60f, 20f, 60f));
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (r != null && r.enabled && !(r is ParticleSystemRenderer) && caja.Intersects(r.bounds)) rendCaja++;
            D("plaza_cenital", "adoquines", adoq); D("plaza_cenital", "patio_losa", losa); D("plaza_cenital", "renderers_caja", rendCaja);
            D("plaza_cenital", "solado_r13", ExisteActivo("R13_Solado"));
            yield return Foto("plaza_cenital", new Vector3(322f, 60f, 200f), new Vector3(322f, 0f, 228f), 55f);
            yield return Foto("plaza_ojo", new Vector3(322f, 1.7f, 205f), new Vector3(322f, 1.2f, 228f), 60f);
            // el primer auto de la ciudad a menos de 40 m de la plaza
            Transform auto = null; float md = 40f;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.name.StartsWith("Auto_") || !t.gameObject.activeInHierarchy) continue;
                float d = new Vector2(t.position.x - 322f, t.position.z - 228f).magnitude; if (d < md) { md = d; auto = t; }
            }
            if (auto != null)
            {
                int piezas = 0, tris = 0; foreach (var mf in auto.GetComponentsInChildren<MeshFilter>()) { piezas++; if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3; }
                D("plaza_mobiliario", "piezas_auto", piezas); D("plaza_mobiliario", "triangulos_auto", tris); D("plaza_mobiliario", "detalle_r13", auto.Find("Detalle_R13") != null);
                yield return Foto("plaza_mobiliario", auto.position + new Vector3(3.5f, 1.6f, 3.5f), auto.position + Vector3.up * 0.8f, 50f);
            }
            else D("plaza_mobiliario", "piezas_auto", "sin auto a menos de 40 m");
            int rendCiudad = 0; foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (r != null && r.enabled && !(r is ParticleSystemRenderer) && r.bounds.center.x > 230f) rendCiudad++;
            D("plaza_mobiliario", "renderers_ciudad", rendCiudad);
            yield return Foto("ciudad_entrada", new Vector3(238f, 10f, 214f), new Vector3(272f, 0f, 228f), 60f);
        }

        // ---------------------------------------------------------------- 34. camion del patio
        IEnumerator CamionPatio()
        {
            Saltar(4); yield return Espera(3f);
            Transform camion = null; float md = 1e9f; var objetivo = new Vector3(-16f, 1.4f, 40f);
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeInHierarchy || t.name.IndexOf("camion", StringComparison.OrdinalIgnoreCase) < 0) continue;
                float d = Vector3.Distance(t.position, objetivo); if (d < md && d < 40f) { md = d; camion = t; }
            }
            int piezas = 0; if (camion != null) piezas = camion.GetComponentsInChildren<MeshFilter>().Length;
            D("camion_patio_cerca", "camion", camion != null ? camion.name : "ninguno"); D("camion_patio_cerca", "piezas_camion", piezas);
            D("camion_patio_cerca", "detalle_r13", camion != null && camion.Find("Detalle_R13") != null);
            yield return Foto("camion_patio_cerca", new Vector3(-10f, 2.2f, 34f), objetivo, 55f);
        }

        // ---------------------------------------------------------------- 36-38. desierto
        IEnumerator Desierto()
        {
            Saltar(4); yield return Espera(3f);
            int arboles = 0; foreach (var te in Terrain.activeTerrains) if (te != null && te.terrainData != null) arboles += te.terrainData.treeInstanceCount;
            D("desierto_1", "arboles_terreno", arboles);
            D("desierto_1", "rodantes", ContarPorNombre("ArbustoRodante_"));
            D("desierto_1", "polvo", ExisteActivo("PolvoDelDesierto") ? 1 : 0); D("desierto_1", "remolinos", ContarPorNombre("RemolinoDePolvo_"));
            D("desierto_1", "terreno_arboles_go", ExisteActivo("Terreno_ArbolesDesierto"));
            yield return Foto("desierto_1", new Vector3(40f, 2.2f, 70f), new Vector3(90f, 1f, 100f), 65f, false);
            yield return Foto("desierto_2", new Vector3(130f, 2.2f, 140f), new Vector3(180f, 1f, 170f), 65f, false);
            yield return Foto("desierto_3", new Vector3(220f, 2.2f, 200f), new Vector3(250f, 1f, 220f), 65f, false);
            GameObject rod = null; foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.name.StartsWith("ArbustoRodante_")) { rod = t.gameObject; break; }
            if (rod != null) { var rp = rod.transform.position; yield return Foto("desierto_rodante", rp + new Vector3(-5f, 1.6f, -5f), rp + Vector3.up * 0.5f, 55f, false); }
        }

        // ---------------------------------------------------------------- 42. retroceso en la huida
        IEnumerator RetrocesoHuida()
        {
            Saltar(4); yield return Espera(3f);
            var yo = Yo(); if (yo == null) { D("retroceso_huida", "error", "sin jugador"); yield break; }
            var ant = yo.transform.position; yield return Espera(0.6f);        // el director anota el z maximo
            var p = EnMalla(yo.transform.position + Vector3.back * 30f); p.y = yo.transform.position.y;
            yo.transform.position = EnMalla(p); Physics.SyncTransforms();
            var colocado = yo.transform.position;
            yield return Espera(1f);
            float tele = Vector3.Distance(new Vector3(yo.transform.position.x, 0f, yo.transform.position.z), new Vector3(colocado.x, 0f, colocado.z));
            D("retroceso_huida", "teletransporte_m", tele);
            var tx = TextosActivos("DEBES");
            D("retroceso_huida", "texto_cartel", tx.Count > 0 ? string.Join(" | ", tx) : "ninguno");
            D("retroceso_huida", "cartel_volver", ExisteActivo("CartelVolver"));
            yield return FotoJuego("retroceso_huida");
        }

        // ---------------------------------------------------------------- 43. compuerta de bajada de la mansion
        IEnumerator CompuertaBajada()
        {
            Saltar(5); yield return Espera(2f);
            var d = OperacionDirector.Instancia; if (d != null) d.SaltarAlSegundoPisoPorPrueba();
            yield return Espera(1f);
            yield return EsperarHasta(() => !d.CineDeMansionEnCurso, 10f);     // si se sale con la cinematica de llegada en curso, el siguiente salto deja al jugador sin control
            Transform comp = null; var nombres = new StringBuilder();
            for (int intento = 0; intento < 40 && comp == null; intento++)
            {
                nombres.Clear();
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t != null && t.name.StartsWith("Mansion_Compuerta_")) { nombres.Append(t.name).Append(t.gameObject.activeInHierarchy ? "" : "(inactiva)").Append(' '); if (comp == null && t.name.EndsWith("_bajada")) comp = t; }
                if (comp == null) yield return Espera(0.25f);
            }
            D("compuerta_bajada", "compuertas_existentes", nombres.Length > 0 ? nombres.ToString().Trim() : "ninguna");
            if (comp == null) { D("compuerta_bajada", "collider_on", "sin compuerta de bajada"); yield break; }
            int cols = 0, obs = 0;
            foreach (var c in comp.GetComponentsInChildren<Collider>(false)) if (c != null && c.enabled && !c.isTrigger) cols++;
            foreach (var o in comp.GetComponentsInChildren<NavMeshObstacle>(false)) if (o != null && o.enabled) obs++;
            D("compuerta_bajada", "compuerta", comp.name); D("compuerta_bajada", "collider_on", cols > 0); D("compuerta_bajada", "colliders", cols); D("compuerta_bajada", "obstacle_on", obs > 0);
            var yo = Yo();
            if (yo != null)
            {
                var b = new Bounds(comp.position, Vector3.zero); foreach (var r in comp.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var dir = new Vector3(comp.forward.x, 0f, comp.forward.z).normalized; if (dir.sqrMagnitude < 0.1f) dir = Vector3.forward;
                var pos = b.center + dir * 1.5f; pos.y = yo.transform.position.y;
                yo.transform.position = pos; yo.transform.rotation = Quaternion.LookRotation(-dir); Physics.SyncTransforms();
                yield return Espera(1f);
            }
            D("compuerta_bajada", "texto_cartel", TextosActivos("VOLVER", "ATRAS", "ATRÁS", "PISO").Count > 0 ? string.Join(" | ", TextosActivos("VOLVER", "ATRAS", "ATRÁS", "PISO")) : "ninguno");
            yield return FotoJuego("compuerta_bajada");
        }

        // ---------------------------------------------------------------- 44. alejamiento del objetivo
        IEnumerator Alejamiento()
        {
            Saltar(2); yield return Espera(3f);
            var d = OperacionDirector.Instancia; var yo = Yo(); if (d == null || yo == null) { D("alejamiento", "error", "sin director o jugador"); yield break; }
            Vector3 obj = new Vector3(0f, 0f, -150f); bool hayObj = d.GuiaPermitida(out var o2); if (hayObj) obj = o2;
            D("alejamiento", "objetivo", V(obj)); D("alejamiento", "guia_permitida", hayObj);
            // movimiento continuo (la regla cuenta segundos en los que la distancia CRECE): 18 m/s durante 6 s
            float t0 = Time.realtimeSinceStartup; var anclaY = yo.transform.position.y;
            while (Time.realtimeSinceStartup - t0 < 6f)
            {
                var dir = yo.transform.position - obj; dir.y = 0f; if (dir.sqrMagnitude < 1f) dir = Vector3.back; dir.Normalize();
                var p = yo.transform.position + dir * (18f * Time.unscaledDeltaTime); yo.transform.position = p; Physics.SyncTransforms();
                yield return null;
            }
            yield return Espera(0.5f);
            var tx = TextosActivos("VOLVE", "VOLVÉ", "OBJETIVO ·");
            D("alejamiento", "texto_cartel", tx.Count > 0 ? string.Join(" | ", tx) : "ninguno");
            D("alejamiento", "distancia_m", Vector3.Distance(new Vector3(yo.transform.position.x, 0f, yo.transform.position.z), new Vector3(obj.x, 0f, obj.z)));
            yield return FotoJuego("alejamiento");
        }
    }
}
