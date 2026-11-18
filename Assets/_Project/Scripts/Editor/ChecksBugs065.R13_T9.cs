using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Core;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T9: calles con sentido. Sin calle de aproximacion, la T existe, los bordes cortos de cada calle nueva tocan otra calle / patio / muro / cordon,
    // las rutas a la T y al patio son completas y el destino de la huida y la patrulla del jefe quedan libres.
    public static partial class ChecksBugs065
    {
        static bool R13_T9_EsCalle(string n) => n.StartsWith("Calle") || n.StartsWith("R13_Calle") || n == "Patio_Tanque" || n.StartsWith("R13_Cordon") || n.StartsWith("Porton") || n.StartsWith("Muro_") || n.StartsWith("Valle_");

        // true si algun OTRO renderer calle/patio/cordon/muro, o algun collider de muro, queda a menos de 0,5 m del punto (en planta)
        static bool R13_T9_TocaAlgo(Vector3 punto, Renderer propio)
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || r == propio || !r.enabled || !R13_T9_EsCalle(r.name)) continue;
                var b = r.bounds; var p = new Vector3(punto.x, b.center.y, punto.z);
                if (b.SqrDistance(p) < 0.5f * 0.5f) return true;
            }
            foreach (var c in Physics.OverlapSphere(new Vector3(punto.x, 1f, punto.z), 0.5f, ~0, QueryTriggerInteraction.Ignore))
                if (c != null && (c.name.StartsWith("Muro_") || c.name.StartsWith("Porton") || c.name.StartsWith("Valle_") || c.name.StartsWith("Cierre"))) return true;
            return false;
        }

        static string R13_T9_Camino(Vector3 a, Vector3 b, out bool completo)
        {
            completo = false;
            if (!NavMesh.SamplePosition(a, out var ha, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out var hb, 4f, NavMesh.AllAreas)) return "sin NavMesh en un extremo";
            var path = new NavMeshPath();
            bool ok = NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
            completo = ok && path.status == NavMeshPathStatus.PathComplete;
            return path.status.ToString();
        }

        // ---------------------------------------------------------------- 978a calles: sin aproximacion, con T, bordes conectados, rutas completas, destino libre
        static IEnumerator Bug978a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4);
            foreach (var x in Esperar(2f)) yield return x;
            Time.timeScale = 1f;
            float t0 = Time.realtimeSinceStartup;
            while (NavMeshViva.Version <= 0 && Time.realtimeSinceStartup - t0 < 25f) yield return null;
            Physics.SyncTransforms();

            // 1) sin calle de aproximacion a menos de 40 m de (0,-360)
            var centro = new Vector3(0f, 0f, -360f); int vivas = 0; var lista = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!(r.name == "Camino_Aproximacion" || r.name.StartsWith("Linea_Aprox"))) continue;
                if (Vector2.Distance(new Vector2(r.bounds.center.x, r.bounds.center.z), new Vector2(centro.x, centro.z)) < 40f) { vivas++; lista.Append(r.name + " "); }
            }
            W8Ok(ref ok, vivas == 0, sb, $"{vivas} renderers Camino_Aproximacion/Linea_Aprox* activos a menos de 40 m del inicio ({lista})");
            // lineas amarillas fusionadas: ningun triangulo del eje x=0 entre z -381..-338,5 (las recorta CallesR13)
            int trisAprox = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mr.sharedMaterial == null || mr.sharedMaterial.name != "M_Op_Amarillo" || mf.sharedMesh == null || mf.sharedMesh.vertexCount < 120 || !mf.sharedMesh.isReadable) continue;
                var vs = mf.sharedMesh.vertices; var tr = mf.sharedMesh.triangles; var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < tr.Length; i += 3)
                {
                    var c = (m.MultiplyPoint3x4(vs[tr[i]]) + m.MultiplyPoint3x4(vs[tr[i + 1]]) + m.MultiplyPoint3x4(vs[tr[i + 2]])) / 3f;
                    if (Mathf.Abs(c.x) < 0.6f && c.z > -381f && c.z < -338.5f) trisAprox++;
                }
            }
            W8Ok(ref ok, trisAprox == 0, sb, $"{trisAprox} triangulos de lineas amarillas fusionadas de la aproximacion");

            // 2) la T existe
            Transform tT = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.name == "Calle_T_Transversal") tT = t;
            W8Ok(ref ok, tT != null, sb, "existe Calle_T_Transversal activa");

            // 3) bordes cortos conectados: Calle_Salida, el acceso al patio y la T
            foreach (var nombre in new[] { "Calle_Salida", "R13_Calle_Acceso_Patio", "Calle_T_Transversal" })
            {
                MeshRenderer calle = null;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)) if (r != null && r.name == nombre && r.enabled) calle = r;
                if (calle == null) { W8Ok(ref ok, false, sb, $"existe {nombre}"); continue; }
                var b = calle.bounds; bool largoX = b.size.x >= b.size.z;
                var e1 = largoX ? new Vector3(b.min.x, 0f, b.center.z) : new Vector3(b.center.x, 0f, b.min.z);
                var e2 = largoX ? new Vector3(b.max.x, 0f, b.center.z) : new Vector3(b.center.x, 0f, b.max.z);
                W8Ok(ref ok, R13_T9_TocaAlgo(e1, calle) && R13_T9_TocaAlgo(e2, calle), sb, $"{nombre}: sus dos bordes cortos tocan otra calle, el patio, un porton, un muro o un cordon ({e1:0.0} / {e2:0.0})");
            }

            // 4) rutas
            foreach (var (a, b2) in new[] { (new Vector3(0f, 0f, -30f), new Vector3(40f, 0f, -18f)), (new Vector3(0f, 0f, -30f), new Vector3(-40f, 0f, -18f)), (new Vector3(0f, 0f, -30f), new Vector3(0f, 0f, 22f)) })
            {
                string st = R13_T9_Camino(a, b2, out bool comp);
                W8Ok(ref ok, comp, sb, $"camino {a} -> {b2}: {st}");
            }

            // 5) Destino_Huida y la patrulla del jefe quedan libres de obstaculos
            var libres = new List<(string, Vector3)> { ("Destino_Huida", new Vector3(0f, 0f, 24f)) };
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && (t.name == "Destino_Huida" || t.name.StartsWith("Patrulla_Jefe_"))) libres.Add((t.name, t.position));
            int bloqueados = 0; var det = new StringBuilder();
            foreach (var (n, p) in libres)
            {
                bool choca = false;   // el propio tanque (Tanque_Fuga, extents z 22 +- 1,8) y los soldados no cuentan: se mueven; solo geometria fija
                foreach (var col in Physics.OverlapSphere(new Vector3(p.x, 1.2f, p.z), 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    if (col != null && col.GetComponentInParent<SP.Vehicles.Vehicle>() == null && col.GetComponentInParent<SP.Actors.Soldier>() == null) { choca = true; break; }
                bool enMalla = NavMesh.SamplePosition(p, out var h, 1.0f, NavMesh.AllAreas);
                if (choca || !enMalla) { bloqueados++; det.Append($"{n}{p:0.0}({(choca ? "obstaculo" : "fuera del NavMesh")}) "); }
            }
            W8Ok(ref ok, bloqueados == 0, sb, $"{bloqueados} de {libres.Count} puntos (Destino_Huida y patrulla del jefe) bloqueados ({det})");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
