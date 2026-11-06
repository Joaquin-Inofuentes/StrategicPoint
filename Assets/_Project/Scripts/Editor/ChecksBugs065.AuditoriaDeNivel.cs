using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Presentation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Auditoria de nivel (2026-11-06): (a) los puntos donde aparecen los enemigos (reservas de la escena y puntos de la mansion) tienen que estar
    // sobre el NavMesh, no empotrados en geometria y ser alcanzables a pie desde la escuadra; (b) tras detonar TODO lo demolible no puede
    // quedar ninguna pieza flotando (renderer sin nada debajo que no estuviera ya asi antes de las explosiones).
    //   return SP.EditorTools.ChecksBugs065.CorrerLista("Bug501a,Bug501b,Bug501c");
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- utilidades
        static bool PuntoEmpotrado(Vector3 p, out string con)
        {
            con = null;
            var hits = Physics.OverlapCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.35f, 0.28f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                if (c == null || BarreraSoloEnemigos.Contiene(c)) continue;
                if (c.GetComponentInParent<Soldier>() != null || c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                if (!NavService.BlocksMovement(c)) continue;
                con = RutaDe(c.transform); return true;
            }
            return false;
        }

        static string RutaDe(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        // Lo que se evalua del punto: sobre malla y no empotrado. La conectividad se mide aparte (Componentes).
        static void AuditarPunto(string nombre, Vector3 p, StringBuilder sb, ref int malos, ref int total)
        {
            total++;
            var fallas = new List<string>();
            if (!NavMesh.SamplePosition(p, out _, 2.5f, NavMesh.AllAreas)) fallas.Add("fuera del NavMesh");
            if (PuntoEmpotrado(p, out var con)) fallas.Add("empotrado en " + con);
            if (fallas.Count > 0) { malos++; if (malos <= 40) sb.Append($"[{nombre} @ {p.x:0.0},{p.y:0.0},{p.z:0.0}: {string.Join(" + ", fallas)}] "); }
        }

        static bool Conectados(Vector3 a, Vector3 b)
        {
            if (!NavMesh.SamplePosition(a, out var ha, 2.5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out var hb, 2.5f, NavMesh.AllAreas)) return false;
            var p = new NavMeshPath();
            return NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, p) && p.status == NavMeshPathStatus.PathComplete;
        }

        // Agrupa los puntos en islas del NavMesh (alcanzables entre si a pie). La isla 0 es la de "origen".
        static List<List<(string n, Vector3 p)>> Componentes(Vector3 origen, List<(string n, Vector3 p)> pts)
        {
            var islas = new List<List<(string n, Vector3 p)>> { new List<(string n, Vector3 p)> { ("ORIGEN", origen) } };
            foreach (var pt in pts)
            {
                bool puesto = false;
                foreach (var isla in islas) if (Conectados(isla[0].p, pt.p)) { isla.Add(pt); puesto = true; break; }
                if (!puesto) islas.Add(new List<(string n, Vector3 p)> { pt });
            }
            return islas;
        }

        static void InformarIslas(List<List<(string n, Vector3 p)>> islas, StringBuilder sb)
        {
            for (int i = 1; i < islas.Count; i++)
            {
                var isla = islas[i]; var nombres = new List<string>();
                for (int k = 0; k < isla.Count && k < 6; k++) nombres.Add($"{isla[k].n}@{isla[k].p.x:0},{isla[k].p.z:0}");
                sb.Append($"\n  isla {i} ({isla.Count} puntos, NO conecta con la escuadra): {string.Join(", ", nombres)}");
            }
        }

        // ---------------------------------------------------------------- 501a: reservas y enemigos de la escena
        static IEnumerator Bug501a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            Time.timeScale = 1f;
            var yo = Escuadrista(RoleType.Assault);
            if (yo == null) { Fin("FALLO sin escuadra"); yield break; }
            NavMesh.SamplePosition(yo.transform.position, out var o, 5f, NavMesh.AllAreas);
            int malos = 0, total = 0; var pts = new List<(string n, Vector3 p)>();
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s == null || s.Team != TeamId.Enemy) continue;
                if (s.transform.position.y > 5f) continue;   // los del interior de la mansion se auditan en 501c (la mansion nace en el objetivo 5)
                string nom = (s.gameObject.activeInHierarchy ? "enemigo " : "reserva ") + s.name;
                AuditarPunto(nom, s.transform.position, sb, ref malos, ref total);
                pts.Add((nom, s.transform.position));
            }
            W8Ok(ref ok, total > 0, sb, $"{total} puntos de aparicion auditados");
            W8Ok(ref ok, malos == 0, sb, $"{malos} puntos de aparicion con problemas");
            var islas = Componentes(o.position, pts);
            W8Ok(ref ok, islas.Count == 1, sb, $"{islas.Count - 1} islas de NavMesh sin conexion con la escuadra (con los portones como estan)");
            InformarIslas(islas, sb);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 501f: islas del NavMesh con el nivel avanzado (objetivo 5, portones ya abiertos)
        static IEnumerator Bug501f()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(4f)) yield return x;
            Time.timeScale = 1f;
            var yo = Escuadrista(RoleType.Assault);
            if (yo == null) { Fin("FALLO sin escuadra"); yield break; }
            NavMesh.SamplePosition(yo.transform.position, out var o, 5f, NavMesh.AllAreas);
            int malos = 0, total = 0; var pts = new List<(string n, Vector3 p)>();
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s == null || s.Team != TeamId.Enemy || s.transform.position.y > 5f || s.name.Contains("Artillero")) continue;
                string nom = (s.gameObject.activeInHierarchy ? "enemigo " : "reserva ") + s.name;
                AuditarPunto(nom, s.transform.position, sb, ref malos, ref total);
                pts.Add((nom, s.transform.position));
            }
            W8Ok(ref ok, malos == 0, sb, $"{malos} de {total} puntos con problemas (objetivo 5)");
            var islas = Componentes(o.position, pts);
            W8Ok(ref ok, islas.Count == 1, sb, $"{islas.Count - 1} islas sin conexion con la escuadra (objetivo 5)");
            InformarIslas(islas, sb);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 501c: puntos de la mansion (nace en el objetivo 5)
        static IEnumerator Bug501c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(4f)) yield return x;
            Time.timeScale = 1f;
            var yo = Escuadrista(RoleType.Assault);
            if (yo == null) { Fin("FALLO sin escuadra"); yield break; }
            NavMesh.SamplePosition(yo.transform.position, out var o, 5f, NavMesh.AllAreas);
            int malos = 0, total = 0;
            var grupos = new[] { ("PB", G.EnemigosPB), ("1.er piso", G.EnemigosPrimero), ("2.o piso", G.EnemigosSegundo) };
            foreach (var (n, arr) in grupos) for (int i = 0; i < arr.Length; i++) AuditarPunto($"mansion {n} #{i}", arr[i], sb, ref malos, ref total);
            W8Ok(ref ok, total > 0, sb, $"{total} puntos de la mansion auditados (origen {o.position:0})");
            W8Ok(ref ok, malos == 0, sb, $"{malos} puntos de la mansion con problemas");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 501b: nada flotando tras detonar todo
        static bool Apoyado(Renderer r)
        {
            var b = r.bounds;
            if (b.min.y < 0.35f) return true;
            var centro = new Vector3(b.center.x, b.min.y - 0.12f, b.center.z);
            var mitad = new Vector3(Mathf.Max(b.extents.x * 0.9f, 0.05f), 0.2f, Mathf.Max(b.extents.z * 0.9f, 0.05f));
            var cs = Physics.OverlapBox(centro, mitad, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cs)
            {
                if (c == null || !c.enabled) continue;
                var t = c.transform;
                if (t == r.transform || t.IsChildOf(r.transform) || r.transform.IsChildOf(t)) continue;   // su propio collider no cuenta
                return true;
            }
            return false;
        }

        static bool EsRuido(Renderer r)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return true;
            if (r.GetComponent<TextMesh>() != null) return true;
            var t = r.transform;
            if (t.GetComponentInParent<Soldier>() != null || t.GetComponentInParent<SP.Vehicles.Vehicle>() != null || t.GetComponentInParent<Projectile>() != null) return true;
            if (r.bounds.size.magnitude > 80f) return true;
            return false;
        }

        static HashSet<Renderer> Sueltos()
        {
            var r = new HashSet<Renderer>();
            foreach (var m in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (m == null || !m.enabled || !m.gameObject.activeInHierarchy || EsRuido(m)) continue;
                if (Apoyado(m)) continue;
                r.Add(m);
            }
            return r;
        }

        // Adornos que flotan por diseno (iconos de interaccion, minimapa, luces): no cuentan.
        static bool EsAdornoFlotante(Renderer r)
        {
            string n = r.name; var p = r.transform.parent != null ? r.transform.parent.name : "";
            return n.Contains("Rombo") || n.Contains("MinimapIcon") || p.Contains("Rombo") || n.Contains("Interior") || n.Contains("Borde")
                || r.GetComponentInParent<InteractableDiamond>() != null || (r.GetComponentInParent<Rigidbody>() is Rigidbody rb && !rb.isKinematic && !rb.IsSleeping());
        }

        static List<Renderer> SueltosEn(Bounds zona)
        {
            var l = new List<Renderer>();
            foreach (var m in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (m == null || !m.enabled || !m.gameObject.activeInHierarchy || !zona.Intersects(m.bounds)) continue;
                if (EsRuido(m) || EsAdornoFlotante(m) || Apoyado(m)) continue;
                l.Add(m);
            }
            return l;
        }

        static Bounds ZonaDe(ObstacleMarker m)
        {
            var b = new Bounds(m.transform.position, Vector3.one);
            var col = m.GetComponentInChildren<Collider>(true); if (col != null) b = col.bounds;
            foreach (var r in m.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            b.Expand(6f);
            return b;
        }

        static string DuenoDe(Renderer r, ObstacleMarker este)
        {
            var d = r.GetComponentInParent<ObstacleMarker>(true);
            if (d == null) return "-";
            if (d == este) return d.name + " (este)";
            return d.name + (d.IsCollapsed ? " (caido)" : " (en pie, etapa " + d.Stage + ")");
        }

        // 501d: cada obstaculo destruible, de a uno; lo que queda flotando cerca (y no flotaba antes) se atribuye a ese obstaculo.
        static IEnumerator Bug501d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            ModoDios.Poner(true);
            Time.timeScale = 2f;
            var marcas = new List<ObstacleMarker>();
            foreach (var m in Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Exclude)) if (m != null && m.EsDestruible && !m.IsCollapsed) marcas.Add(m);
            int culpables = 0, flotantes = 0, n = 0;
            var lineas = new List<string>();
            foreach (var m in marcas)
            {
                if (m == null || m.IsCollapsed) continue;
                n++;
                var zona = ZonaDe(m);
                var antes = new HashSet<Renderer>(SueltosEn(zona));
                string ruta = RutaDe(m.transform); var pos = m.transform.position;
                m.Demoler(m.transform.position + Vector3.up * 1.5f, 11f);
                foreach (var x in Esperar(1.6f)) yield return x;
                var nuevos = new List<Renderer>();
                foreach (var r in SueltosEn(zona)) if (!antes.Contains(r)) nuevos.Add(r);
                if (nuevos.Count == 0) continue;
                culpables++; flotantes += nuevos.Count;
                var ej = new List<string>();
                for (int i = 0; i < nuevos.Count && i < 3; i++) ej.Add($"{RutaDe(nuevos[i].transform)} y={nuevos[i].bounds.min.y:0.0} dueno={DuenoDe(nuevos[i], m)}");
                lineas.Add($"{ruta} @ {pos.x:0},{pos.y:0.0},{pos.z:0} -> {nuevos.Count}: {string.Join(" ; ", ej)}");
            }
            Time.timeScale = 1f;
            W8Ok(ref ok, culpables == 0, sb, $"{culpables} de {n} obstaculos dejan {flotantes} piezas flotando");
            if (lineas.Count > 0) sb.Append("\n  " + string.Join("\n  ", lineas));
            System.IO.File.WriteAllText("Temp/flotantes_501d.txt", sb.ToString());
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 501e: solo las explosiones "de verdad" del nivel (cargas de los puestos y del porton, generador): se escanea alrededor de cada una.
        static IEnumerator Bug501e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            ModoDios.Poner(true);
            var det = typeof(CargaExplosiva).GetMethod("Detonar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            int total = 0; var lineas = new List<string>();
            foreach (var c in Object.FindObjectsByType<CargaExplosiva>(FindObjectsInactive.Exclude))
            {
                if (c.Situacion == CargaExplosiva.Estado.Volada) continue;
                var zona = new Bounds(c.transform.position, new Vector3(60f, 40f, 60f));
                var antes = new HashSet<Renderer>(SueltosEn(zona));
                det.Invoke(c, null);
                foreach (var x in Esperar(9f)) yield return x;
                int k = 0;
                foreach (var r in SueltosEn(zona)) if (!antes.Contains(r)) { k++; total++; if (lineas.Count < 60) lineas.Add($"carga {c.lado}: {RutaDe(r.transform)} y={r.bounds.min.y:0.0} @ {r.bounds.center.x:0},{r.bounds.center.z:0}"); }
                sb.Append($"[{c.lado}: {k} flotando] ");
            }
            var gen = Object.FindFirstObjectByType<GeneradorDelCuartel>();
            if (gen != null && !gen.EstaCaido)
            {
                var zona = new Bounds(gen.transform.position, new Vector3(60f, 40f, 60f));
                var antes = new HashSet<Renderer>(SueltosEn(zona));
                gen.Caer();
                foreach (var x in Esperar(9f)) yield return x;
                int k = 0;
                foreach (var r in SueltosEn(zona)) if (!antes.Contains(r)) { k++; total++; if (lineas.Count < 60) lineas.Add($"generador: {RutaDe(r.transform)} y={r.bounds.min.y:0.0} @ {r.bounds.center.x:0},{r.bounds.center.z:0}"); }
                sb.Append($"[generador: {k} flotando] ");
            }
            W8Ok(ref ok, total == 0, sb, $"{total} piezas flotando tras las explosiones reales");
            if (lineas.Count > 0) sb.Append("\n  " + string.Join("\n  ", lineas));
            System.IO.File.WriteAllText("Temp/flotantes_501e.txt", sb.ToString());
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // 501g (item #003 de la hoja: "corregir colisiones de objetos, chequear el resto del nivel"):
        //  (1) colliders de obstaculos desalineados del arte que los viste; (2) colliders invisibles sueltos (sin ninguna malla a la vista);
        //  (3) geometria solida visible (alta y ancha) que se atraviesa porque no tiene collider.
        static IEnumerator Bug501g()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            Physics.SyncTransforms();
            var mallas = new List<MeshRenderer>();
            foreach (var m in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)) if (m != null && m.enabled && !EsRuido(m) && m.bounds.size.magnitude < 80f) mallas.Add(m);
            // (1) desalineados
            var l1 = new List<string>(); int n1 = 0;
            foreach (var om in Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Exclude))
            {
                var col = om.GetComponent<Collider>(); if (col == null) continue;
                var arte = om.transform.Find("ArteOp"); if (arte == null) continue;
                var rs = arte.GetComponentsInChildren<Renderer>(false); if (rs.Length == 0) continue;
                var ab = rs[0].bounds; foreach (var r in rs) ab.Encapsulate(r.bounds);
                var cb = col.bounds;
                float desfase = Vector3.Distance(new Vector3(cb.center.x, 0, cb.center.z), new Vector3(ab.center.x, 0, ab.center.z));
                float exceso = Mathf.Max(cb.size.x / Mathf.Max(ab.size.x, 0.1f), cb.size.z / Mathf.Max(ab.size.z, 0.1f));
                float falta = Mathf.Min(cb.size.x / Mathf.Max(ab.size.x, 0.1f), cb.size.z / Mathf.Max(ab.size.z, 0.1f));
                if (desfase > 0.7f || exceso > 1.6f || falta < 0.55f) { n1++; if (l1.Count < 25) l1.Add($"{RutaDe(om.transform)} @ {cb.center.x:0},{cb.center.z:0}: desfase {desfase:0.0} m, collider/arte {falta:0.00}..{exceso:0.00}"); }
            }
            W8Ok(ref ok, n1 == 0, sb, $"{n1} obstaculos con el collider desalineado del arte");
            // (2) colliders invisibles sueltos
            var l2 = new List<string>(); int n2 = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c == null || c.isTrigger || !c.enabled || c is TerrainCollider || c.GetComponentInParent<Soldier>() != null || c.attachedRigidbody != null) continue;
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null || BarreraSoloEnemigos.Contiene(c) || c.GetComponentInParent<ObstacleMarker>() != null) continue;
                var b = c.bounds; if (b.size.y < 1f || Mathf.Max(b.size.x, b.size.z) < 1f || b.size.magnitude > 120f) continue;
                bool conMalla = false;
                var zona = b; zona.Expand(0.6f);
                foreach (var m in mallas) if (zona.Intersects(m.bounds) && m.bounds.size.y > 0.25f) { conMalla = true; break; }
                if (!conMalla) { n2++; if (l2.Count < 25) l2.Add($"{RutaDe(c.transform)} @ {b.center.x:0},{b.center.y:0.0},{b.center.z:0} tam {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0}"); }
            }
            W8Ok(ref ok, n2 == 0, sb, $"{n2} colliders invisibles sin ninguna malla");
            // (3) solido sin collider
            var l3 = new List<string>(); int n3 = 0;
            foreach (var m in mallas)
            {
                var b = m.bounds;
                if (b.size.y < 1.4f || b.size.x < 1.0f && b.size.z < 1.0f || Mathf.Max(b.size.x, b.size.z) < 1.5f) continue;
                if (b.min.y > 3f || m.GetComponentInParent<Canvas>() != null) continue;
                if (m.GetComponentInParent<Soldier>() != null || m.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                string n = RutaDe(m.transform);
                if (n.Contains("Fusionados") || n.Contains("Calles") || n.Contains("Decal") || n.Contains("Terreno") || n.Contains("Luz") || n.Contains("Cielo") || n.Contains("Horizonte") || n.Contains("Mundo")) continue;
                var zona = b; zona.Expand(new Vector3(0.2f, -0.1f, 0.2f));
                var cs = Physics.OverlapBox(zona.center, zona.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool hay = false; foreach (var c in cs) if (c != null && !(c is TerrainCollider) && c.GetComponentInParent<Soldier>() == null && c.attachedRigidbody == null) { hay = true; break; }
                if (!hay) { n3++; if (l3.Count < 40) l3.Add($"{n} @ {b.center.x:0},{b.center.z:0} tam {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0}"); }
            }
            W8Ok(ref ok, n3 == 0, sb, $"{n3} piezas solidas visibles sin collider (se atraviesan)");
            sb.Append("\n(1) " + string.Join("\n  ", l1) + "\n(2) " + string.Join("\n  ", l2) + "\n(3) " + string.Join("\n  ", l3));
            System.IO.File.WriteAllText("Temp/colisiones_501g.txt", sb.ToString());
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static IEnumerator Bug501b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(3f)) yield return x;
            Time.timeScale = 1f;
            ModoDios.Poner(true);
            var antes = Sueltos();
            sb.Append($"antes de explotar: {antes.Count} ya sin apoyo; ");
            // Detona todo: cargas, generador, torres y cada obstaculo destruible.
            int cargas = 0, marcas = 0, torres = 0;
            var det = typeof(CargaExplosiva).GetMethod("Detonar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var c in Object.FindObjectsByType<CargaExplosiva>(FindObjectsInactive.Exclude)) { if (c.Situacion != CargaExplosiva.Estado.Volada) { det.Invoke(c, null); cargas++; } }
            foreach (var g in Object.FindObjectsByType<GeneradorDelCuartel>(FindObjectsInactive.Exclude)) if (!g.EstaCaido) g.Caer();
            foreach (var t in Object.FindObjectsByType<TorreDestruible>(FindObjectsInactive.Exclude)) if (!t.EstaCaida && t.Marca != null) { t.Marca.Demoler(t.transform.position, 11f); torres++; }
            foreach (var m in Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Exclude))
            {
                if (m == null || m.IsCollapsed || !m.EsDestruible) continue;
                m.Demoler(m.transform.position + Vector3.up * 1.5f, 11f); marcas++;
            }
            sb.Append($"detonados: {cargas} cargas, {torres} torres, {marcas} obstaculos; ");
            foreach (var x in Esperar(14f)) yield return x;   // cae, rueda y se duerme
            var despues = Sueltos();
            int nuevos = 0; var lineas = new List<string>();
            foreach (var m in despues)
            {
                if (m == null || antes.Contains(m)) continue;
                nuevos++;
                if (lineas.Count < 40) lineas.Add($"{RutaDe(m.transform)} y={m.bounds.min.y:0.0} tam={m.bounds.size.x:0.0}x{m.bounds.size.y:0.0}x{m.bounds.size.z:0.0} @ {m.bounds.center.x:0},{m.bounds.center.z:0}");
            }
            W8Ok(ref ok, nuevos == 0, sb, $"{nuevos} piezas flotando tras las explosiones");
            if (lineas.Count > 0) sb.Append("\n  " + string.Join("\n  ", lineas));
            System.IO.File.WriteAllText("Temp/flotantes_501b.txt", sb.ToString());
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
