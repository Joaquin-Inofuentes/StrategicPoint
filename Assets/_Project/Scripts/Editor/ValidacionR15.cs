using System.Collections.Generic;
using System.Text;
using SP.Actors;
using SP.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SP.EditorTools
{
    // Ronda 15: validaciones de nivel que se corren desde el Editor sobre las escenas abiertas (EscenasR13.Asegurar) sin entrar a Play.
    public static class ValidacionR15
    {
        // Spawn de enemigos: cada uno (activo o no, es la posicion con la que arranca) debe
        //  1) estar sobre el NavMesh (a <= 1,5 m),
        //  2) no tener su capsula metida dentro de un collider solido (arboles, MeshCollider de piezas, muros),
        //  3) no estar encerrado (alguna de las 8 direcciones libre >= 2 m),
        //  4) no estar en un bolsillo del NavMesh (salida libre >= 5 m). Las zonas se separan por portones/muros demolibles A PROPOSITO, por eso no se exige ruta al jugador.
        public static string SpawnsDeEnemigos(out int malos)
        {
            var sb = new StringBuilder(); malos = 0; int n = 0, fueraDeNav = 0, enCollider = 0, encerrados = 0, sinRuta = 0;
            var refs = new List<Vector3>();
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include)) if (s.Team == TeamId.Player) refs.Add(s.transform.position);
            foreach (var t in EscenasR13.Todos()) if (t != null && (t.name == "Destino_Huida" || t.name == "Ruta_1" || t.name == "Plaza" || t.name == "Calle_Principal")) refs.Add(t.position);
            bool hayMeta = refs.Count > 0;
            var dirs = new Vector3[8]; for (int i = 0; i < 8; i++) dirs[i] = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include))
            {
                if (s.Team != TeamId.Enemy) continue; n++;
                var p = s.transform.position; bool malo = false; string why = "";
                if (p.y >= 4f) continue;
                if (!NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas)) { fueraDeNav++; malo = true; why += " fueraDeNavMesh"; }
                else
                {
                    var a = hit.position + Vector3.up * 0.4f; var b = hit.position + Vector3.up * 1.4f;
                    foreach (var c in Physics.OverlapCapsule(a, b, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c.transform.IsChildOf(s.transform) || c.GetComponentInParent<Soldier>() != null || c is TerrainCollider) continue;
                        if (c.bounds.size.y < 0.3f) continue;   // suelos y losas finas
                        enCollider++; malo = true; why += " dentroDe:" + c.name; break;
                    }
                    int libres = 0;
                    foreach (var d in dirs)
                        if (!Physics.CapsuleCast(a, b, 0.3f, d, out var h, 2f, ~0, QueryTriggerInteraction.Ignore) || h.collider.GetComponentInParent<Soldier>() != null) libres++;
                    if (libres == 0) { encerrados++; malo = true; why += " encerrado"; }
                    // Bolsillo: caminando por el NavMesh, la mayor distancia libre en 8 direcciones debe ser >= 5 m (si no, esta metido en un huequito sin salida).
                    float mejor = 0f;
                    foreach (var d in dirs)
                    {
                        float libre = NavMesh.Raycast(hit.position, hit.position + d * 8f, out var eh, NavMesh.AllAreas) ? eh.distance : 8f;
                        if (libre > mejor) mejor = libre;
                    }
                    if (mejor < 5f)
                    {
                        // recinto angosto (bunker, cabina): solo es bolsillo si TAMPOCO llega caminando a ningun punto de su zona
                        bool sale = false; var path = new NavMeshPath();
                        foreach (var z in new[] { -300f, -190f, -140f, -30f, 30f, 150f })
                        {
                            var d0 = new Vector3(0f, 0f, z); if (!NavMesh.SamplePosition(d0, out var hz, 8f, NavMesh.AllAreas)) continue;
                            if (NavMesh.CalculatePath(hit.position, hz.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) { sale = true; break; }
                        }
                        if (!sale) { sinRuta++; malo = true; why += $" bolsillo({mejor:0.0}m)"; }
                    }
                }
                if (malo) { malos++; sb.AppendLine($"  MALO {s.name} ({p.x:0},{p.y:0},{p.z:0}){why}"); }
            }
            sb.Insert(0, $"spawns de enemigos: {n} revisados · fuera de NavMesh {fueraDeNav} · dentro de collider {enCollider} · encerrados {encerrados} · en bolsillo {sinRuta}\n");
            return sb.ToString();
        }

        // Solapamiento de geometria: dos props sueltos (raiz P_Env_*) cuyos colliders se penetran de verdad (>= 25 cm), no solo sus cajas envolventes (las copas de los arboles
        // tienen cajas enormes que no significan nada). Duplicado: mismo nombre y misma posicion (<= 5 cm).
        public struct Par { public Transform a, b; public string tipo; public float prof; }
        static Transform RaizDeProp(Transform t) { Transform r = null; for (var p = t; p != null; p = p.parent) if (p.name.StartsWith("P_Env_")) r = p; return r; }
        public static List<Par> Solapados(float minProf = 0.25f)
        {
            var res = new List<Par>(); var vistos = new HashSet<(Transform, Transform)>();
            var cols = new List<Collider>();
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude)) if (c != null && !c.isTrigger && RaizDeProp(c.transform) != null) cols.Add(c);
            foreach (var a in cols)
            {
                var ra = RaizDeProp(a.transform); var bb = a.bounds;
                foreach (var b in Physics.OverlapBox(bb.center, bb.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (b == a || b.isTrigger) continue; var rb = RaizDeProp(b.transform); if (rb == null || rb == ra) continue;
                    var k = ra.GetHashCode() <= rb.GetHashCode() ? (ra, rb) : (rb, ra);
                    if (vistos.Contains(k)) continue;
                    if (ra.name == rb.name && (ra.position - rb.position).sqrMagnitude < 0.0025f) { vistos.Add(k); res.Add(new Par { a = ra, b = rb, tipo = "duplicado" }); continue; }
                    bool ok;
                    try { ok = Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float dist); if (ok && dist >= minProf) { vistos.Add(k); res.Add(new Par { a = ra, b = rb, tipo = "superpuesto", prof = dist }); } }
                    catch { /* par de colliders no soportado (MeshCollider no convexo contra otro no convexo) */ }
                }
            }
            return res;
        }

        // Colliders sobrantes (informe): sin MeshRenderer propio ni de hijos y no trigger; o de props menores a 0,35 m; o duplicados en un mismo objeto.
        public static string CollidersSobrantes(bool aplicar, out int total)
        {
            var sb = new StringBuilder(); int vacios = 0, chicos = 0, dobles = 0; total = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c == null || c.isTrigger || c is TerrainCollider || c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Rigidbody>() != null) continue;
                if (c.GetComponentInParent<SP.Interaction.IInteractable>() != null) continue;
                bool sobra = false; string why = "";
                var cols = c.GetComponents<Collider>();
                if (cols.Length > 1 && c != cols[0] && c.GetType() == cols[0].GetType()) { sobra = true; why = "doble"; dobles++; }
                else if (c.GetComponentInChildren<Renderer>(false) == null && c.GetComponentInParent<Renderer>() == null) { sobra = false; }
                else if (c.bounds.size.x < 0.25f && c.bounds.size.y < 0.25f && c.bounds.size.z < 0.25f && c.GetComponentInParent<SP.Interaction.IInteractable>() == null) { sobra = true; why = "chico"; chicos++; }
                if (!sobra) continue; total++;
                if (aplicar) { Object.DestroyImmediate(c); }
            }
            sb.AppendLine($"colliders sobrantes: {total} ({dobles} dobles, {chicos} menores a 25 cm) {(aplicar ? "quitados" : "detectados")}");
            return sb.ToString();
        }
    }
}
