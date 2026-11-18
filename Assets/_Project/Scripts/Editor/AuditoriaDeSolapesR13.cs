using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Vehicles;

namespace SP.EditorTools
{
    /// <summary>
    /// Ronda 13 · T11. Auditoria de SOLO LECTURA de solapes de geometria y objetos duplicados en las 4 escenas del nivel (SC_Operacion + Cuarteles, Ruta y Ciudad).
    /// Categorias: A duplicados exactos (incluidos los ya apagados), B casi duplicados a 15 cm, C coplanares (DetectorDeSolapes por sector, area &gt;= 0,5 m2, distinto
    /// material o malla), D interpenetracion de colliders (ComputePenetration &gt; 0,1 m, raices de prefab distintas), E prop sin collider con &gt;= 40 % de su AABB dentro de
    /// un collider de edificio, F flotantes o enterrados (raycast desde bounds.min.y).
    /// Firma: AuditoriaDeSolapesR13.Informe("antes"|"despues") -> escribe Temp/solapes_&lt;etiqueta&gt;.txt (copiar a Docs/RONDA_13/). No modifica nada.
    /// </summary>
    public static class AuditoriaDeSolapesR13
    {
        public sealed class Hallazgo
        {
            public char cat; public string escena, ruta, detalle; public Vector3 pos;
            public Renderer renderer; public Collider collider; public GameObject go;   // referencias vivas (solo en esta sesion del Editor)
            public override string ToString() => $"{cat}\t{escena}\t{ruta}\t({pos.x:0.0},{pos.y:0.0},{pos.z:0.0})\t{detalle}";
        }

        static readonly string[] Pisos = { "Ground", "Losa", "Calle", "Piso", "Asfalto", "Vereda", "R13_Solado", "Terreno", "Suelo", "Camino", "Plaza_Cemento", "R13_PatioSolado" };
        static readonly (string nombre, Vector3 centro, Vector3 tam)[] Sectores =
        {
            ("Cuarteles", new Vector3(0f, 5f, -170f), new Vector3(130f, 30f, 440f)),
            ("Ruta", new Vector3(0f, 5f, 213f), new Vector3(780f, 30f, 340f)),
            ("Ciudad", new Vector3(320f, 10f, 230f), new Vector3(260f, 60f, 260f)),
        };

        static bool EsPiso(Transform t)
        {
            for (var q = t; q != null; q = q.parent)
                foreach (var p in Pisos) if (q.name.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        static bool Dinamico(Component c) => c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Vehicle>() != null || c.GetComponentInParent<Rigidbody>() != null || c.GetComponentInParent<Animator>() != null;

        static int H(object o) => o == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        static Vector3Int Q(Vector3 v, float p) => new Vector3Int(Mathf.RoundToInt(v.x / p), Mathf.RoundToInt(v.y / p), Mathf.RoundToInt(v.z / p));

        struct Clave : System.IEquatable<Clave>
        {
            public int malla, material; public Vector3Int pos, esc, rot;
            public bool Equals(Clave o) => malla == o.malla && material == o.material && pos == o.pos && esc == o.esc && rot == o.rot;
            public override bool Equals(object o) => o is Clave k && Equals(k);
            public override int GetHashCode() { unchecked { return (((malla * 397 ^ material) * 397 ^ pos.GetHashCode()) * 397 ^ esc.GetHashCode()) * 397 ^ rot.GetHashCode(); } }
        }

        static Hallazgo Nuevo(char cat, Renderer r, string detalle) => new Hallazgo { cat = cat, escena = r.gameObject.scene.name, ruta = EscenasR13.Ruta(r.transform), pos = r.transform.position, detalle = detalle, renderer = r, go = r.gameObject };

        /// <summary>A y B. Incluye renderers apagados y objetos inactivos (los que LimpiezaDeDuplicados ya apago o apagaria en juego). El original (el primero) se conserva; se lista la COPIA.</summary>
        public static List<Hallazgo> DuplicadosAyB()
        {
            var res = new List<Hallazgo>();
            var fino = new Dictionary<Clave, MeshRenderer>(); var grueso = new Dictionary<Clave, MeshRenderer>();
            var esA = new HashSet<MeshRenderer>();
            var todos = new List<MeshRenderer>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i); if (!sc.isLoaded) continue;
                foreach (var raiz in sc.GetRootGameObjects()) todos.AddRange(raiz.GetComponentsInChildren<MeshRenderer>(true));
            }
            foreach (var mr in todos)
            {
                if (mr == null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null || Dinamico(mr)) continue;
                var t = mr.transform;
                if (t.position.sqrMagnitude < 0.0001f && mf.sharedMesh.vertexCount > 200) continue;   // mallas fusionadas / suelos en el origen
                var k = new Clave { malla = H(mf.sharedMesh), material = H(mr.sharedMaterial), pos = Q(t.position, 0.01f), esc = Q(t.lossyScale, 0.01f), rot = Q(t.eulerAngles, 0.5f) };
                if (!fino.TryGetValue(k, out var primero)) fino[k] = mr;
                else { esA.Add(mr); res.Add(Nuevo('A', mr, $"copia exacta de {EscenasR13.Ruta(primero.transform)}{(mr.enabled && mr.gameObject.activeInHierarchy ? "" : " [ya apagada]")}")); }
            }
            foreach (var mr in todos)
            {
                if (mr == null || esA.Contains(mr) || !mr.gameObject.activeInHierarchy) continue;   // apagado en origen = ya resuelto
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null || Dinamico(mr)) continue;
                var t = mr.transform;
                if (t.position.sqrMagnitude < 0.0001f && mf.sharedMesh.vertexCount > 200) continue;
                var k = new Clave { malla = H(mf.sharedMesh), material = H(mr.sharedMaterial), pos = Q(t.position, 0.15f), esc = Q(t.lossyScale, 0.1f), rot = Q(t.eulerAngles, 12f) };   // la rotacion cuenta: Cruz_A/Cruz_B del porton (una X) y piezas giradas no son duplicados
                if (!grueso.TryGetValue(k, out var primero)) grueso[k] = mr;
                else res.Add(Nuevo('B', mr, $"casi duplicado (a 15 cm) de {EscenasR13.Ruta(primero.transform)}"));
            }
            return res;
        }

        static string NombreDeEdificio(Transform t)
        {
            for (var q = t; q != null; q = q.parent)
                if (q.name.StartsWith("Edificio") || q.name.StartsWith("Casa") || q.name.StartsWith("P_Op_Ciudad_Edificio") || q.name.StartsWith("P_Esc_Edificio") || q.name.StartsWith("P_Esc_Casa")) return q.name;
            return null;
        }

        static GameObject RaizDePrefab(GameObject go) => PrefabUtility.GetNearestPrefabInstanceRoot(go) ?? go.transform.root.gameObject;

        public static List<Hallazgo> InterpenetracionesD(int maximo)
        {
            var res = new List<Hallazgo>();
            Physics.SyncTransforms();
            var cols = new List<Collider>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i); if (!sc.isLoaded) continue;
                foreach (var raiz in sc.GetRootGameObjects()) cols.AddRange(raiz.GetComponentsInChildren<Collider>(false));
            }
            var vistos = new HashSet<long>();
            var buf = new Collider[64];
            foreach (var a in cols)
            {
                if (a == null || !a.enabled || a.isTrigger || a is TerrainCollider || Dinamico(a) || EsPiso(a.transform)) continue;
                if (a is MeshCollider ma && !ma.convex) continue;
                var ba = a.bounds; if (ba.size.magnitude > 80f || ba.size.magnitude < 0.2f) continue;
                int n = Physics.OverlapBoxNonAlloc(ba.center, ba.extents, buf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var b = buf[i];
                    if (b == null || b == a || !b.enabled || b is TerrainCollider || Dinamico(b) || EsPiso(b.transform)) continue;
                    if (b is MeshCollider mb && !mb.convex) continue;
                    long id = ((long)Mathf.Min(H(a), H(b)) << 32) ^ (uint)Mathf.Max(H(a), H(b));
                    if (!vistos.Add(id)) continue;
                    if (RaizDePrefab(a.gameObject) == RaizDePrefab(b.gameObject)) continue;
                    var ma2 = a.GetComponentInParent<SP.Presentation.ObstacleMarker>(); var mb2 = b.GetComponentInParent<SP.Presentation.ObstacleMarker>();
                    if (ma2 != null && ma2 == mb2) continue;
                    if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out var dir, out float dist) && dist > 0.1f)
                    {
                        var h = new Hallazgo { cat = 'D', escena = a.gameObject.scene.name, ruta = EscenasR13.Ruta(a.transform), pos = ba.center, collider = a, go = a.gameObject, detalle = $"penetra {dist:0.00} m en {EscenasR13.Ruta(b.transform)}" };
                        res.Add(h);
                        if (res.Count >= maximo) return res;
                    }
                }
            }
            return res;
        }

        public static List<Hallazgo> PropsEnEdificiosE(int maximo)
        {
            var res = new List<Hallazgo>();
            Physics.SyncTransforms();
            var buf = new Collider[64];
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var sc = SceneManager.GetSceneAt(s); if (!sc.isLoaded) continue;
                foreach (var raiz in sc.GetRootGameObjects())
                    foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        if (r == null || !r.enabled || Dinamico(r) || EsPiso(r.transform)) continue;
                        var b = r.bounds; float vol = b.size.x * b.size.y * b.size.z;
                        if (vol < 0.01f || b.size.magnitude > 15f || b.size.y < 0.05f) continue;
                        if (r.GetComponentInParent<Collider>() != null) continue;                      // solo props SIN collider
                        string miEdificio = NombreDeEdificio(r.transform);
                        int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, buf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < n; i++)
                        {
                            var c = buf[i]; if (c == null || !c.enabled || c is TerrainCollider) continue;
                            string eb = NombreDeEdificio(c.transform);
                            if (eb == null || (miEdificio != null && eb == miEdificio) || r.transform.IsChildOf(c.transform.root)) continue;
                            var cb = c.bounds; var min = Vector3.Max(b.min, cb.min); var max = Vector3.Min(b.max, cb.max);
                            if (max.x <= min.x || max.y <= min.y || max.z <= min.z) continue;
                            float dentro = (max.x - min.x) * (max.y - min.y) * (max.z - min.z) / vol;
                            if (dentro >= 0.4f) { res.Add(Nuevo('E', r, $"{dentro * 100f:0} % de su AABB dentro de {EscenasR13.Ruta(c.transform)}")); break; }
                        }
                        if (res.Count >= maximo) return res;
                    }
            }
            return res;
        }

        static readonly string[] ElevadosPorDiseno = { "Toldo", "Techo", "Cornisa", "Parapeto", "Ventan", "Balcon", "Barand", "Letrero", "Cartel", "Rotulo", "Farol", "Luz", "Pasarela", "Arco", "Dintel", "Antena", "Baliza", "Guirnalda", "Bombilla", "Neon", "Copa", "Cabeza", "Cartelera", "Reloj", "Escalera", "Chimenea", "Aire", "Flor", "Remate", "Led", "Pilastra", "Marco", "Placa", "Lampara", "Cielo", "Horizonte", "Fus" };

        public static List<Hallazgo> FlotantesYEnterradosF(int maximo)
        {
            var res = new List<Hallazgo>();
            Physics.SyncTransforms();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var sc = SceneManager.GetSceneAt(s); if (!sc.isLoaded) continue;
                foreach (var raiz in sc.GetRootGameObjects())
                    foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        if (r == null || !r.enabled || Dinamico(r)) continue;
                        var b = r.bounds;
                        if (b.size.magnitude > 20f || b.size.y < 0.3f) continue;
                        string n = r.name; bool elevado = false;
                        for (var q = r.transform; q != null && !elevado; q = q.parent) foreach (var e in ElevadosPorDiseno) if (q.name.Contains(e)) { elevado = true; break; }
                        if (elevado) continue;
                        if (b.max.y < 0.1f) { res.Add(Nuevo('F', r, $"enterrado: techo en y={b.max.y:0.00}")); }
                        else if (b.min.y > 0.3f && b.min.y < 3f)
                        {
                            var origen = new Vector3(b.center.x, b.min.y + 0.02f, b.center.z);
                            float gap;
                            if (Physics.Raycast(origen, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != null && !hit.collider.transform.IsChildOf(r.transform)) gap = origen.y - hit.point.y;
                            else gap = 3f;
                            if (gap > 0.3f) res.Add(Nuevo('F', r, $"flotante: {gap:0.00} m sobre {(gap >= 3f ? "nada" : "el piso")}"));
                        }
                        if (res.Count >= maximo) return res;
                    }
            }
            return res;
        }

        [MenuItem("Strategic Point/Operacion/R13 T11 · Auditoria de solapes (informe antes)")]
        static void MenuInforme() => Debug.Log(Informe("antes"));

        /// <summary>Firma: AuditoriaDeSolapesR13.Informe(string etiqueta, int maxPorCategoria = 400, bool incluirC = true). Solo lectura (puede abrir las escenas si no estan abiertas).</summary>
        public static string Informe(string etiqueta, int maxPorCategoria = 400, bool incluirC = true)
        {
            var sb = new StringBuilder($"== Solapes R13 ({etiqueta}) {System.DateTime.Now:yyyy-MM-dd HH:mm}\n");
            if (!EscenasR13.Asegurar(sb, false)) return EscenasR13.Cerrar(sb, $"solapes_{etiqueta}.txt");
            var A = new List<Hallazgo>(); var B = new List<Hallazgo>();
            foreach (var h in DuplicadosAyB()) (h.cat == 'A' ? A : B).Add(h);
            var C = new List<Hallazgo>();
            if (incluirC)
                foreach (var sec in Sectores)
                {
                    var l = DetectorDeSolapes.Escanear(new Bounds(sec.centro, sec.tam), 1500, 0.004f, 0.5f);
                    foreach (var s in l)
                    {
                        if (s.mismaMalla && s.MismoMaterial) continue;
                        C.Add(new Hallazgo { cat = 'C', escena = sec.nombre, ruta = s.a + " <> " + s.b, pos = new Vector3(s.desde.x, s.plano, s.desde.y), detalle = $"eje {"XYZ"[s.eje]}{(s.positivo ? "+" : "-")} area={s.area:0.00} [{s.matA}/{s.matB}]" });
                    }
                }
            var D = InterpenetracionesD(maxPorCategoria * 3);
            var E = PropsEnEdificiosE(maxPorCategoria * 3);
            var F = FlotantesYEnterradosF(maxPorCategoria * 3);

            sb.AppendLine("RESUMEN por categoria y escena (escena = donde vive el objeto; C por sector)");
            foreach (var (cat, lista, desc) in new[] { ('A', A, "duplicados exactos"), ('B', B, "casi duplicados 15 cm"), ('C', C, "coplanares >= 0,5 m2"), ('D', D, "interpenetracion"), ('E', E, "props sin collider dentro de edificios"), ('F', F, "flotantes/enterrados") })
            {
                var porEscena = new SortedDictionary<string, int>();
                foreach (var h in lista) porEscena[h.escena] = (porEscena.TryGetValue(h.escena, out var c) ? c : 0) + 1;
                var det = new StringBuilder(); foreach (var kv in porEscena) det.Append($"{kv.Key}={kv.Value} ");
                sb.AppendLine($"{cat} {desc}: {lista.Count}  [{det}]");
            }
            sb.AppendLine("\nHALLAZGOS (cat, escena, ruta, posicion, detalle)");
            foreach (var lista in new[] { A, B, C, D, E, F })
            {
                int k = 0;
                foreach (var h in lista) { if (k++ >= maxPorCategoria) { sb.AppendLine($"... {lista.Count - maxPorCategoria} mas de la categoria {lista[0].cat}"); break; } sb.AppendLine(h.ToString()); }
            }
            return EscenasR13.Cerrar(sb, $"solapes_{etiqueta}.txt");
        }
    }
}
