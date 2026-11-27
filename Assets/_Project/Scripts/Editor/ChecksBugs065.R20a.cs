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

namespace SP.EditorTools
{
    // Ronda 20, punto 1 de COLA_PENDIENTES ("disparo a enemigos y la bala se frena por un collider invisible; queda el decal flotando").
    //   #995a  disparos reales (Projectile del pool) desde lineas de tiro tipicas a enemigos quietos en los objetivos 1, 2, 3 y 5: ninguna bala se
    //          frena en un collider sin superficie visible, todo decal queda a <= 0,05 m de una malla dibujada, las lineas libres le pegan al enemigo
    //          y las paredes visibles siguen frenando. Informe de los colliders invisibles que el fisico crudo cruzaba (nombre/posicion).
    //   #995b  el decal se va con la pieza: tiro a una cobertura, se apaga la pieza -> el decal se apaga; tiro a otra, se derrumba -> idem.
    //   #995c  informe (no falla): solidos sin superficie dibujada y solidos que sobresalen > 0,3 m de su malla.
    //   #995d  tiros a la parte sin malla de los colliders sobredimensionados: la bala no se frena en el aire ni deja decal flotando.
    // Correr:  return SP.EditorTools.ChecksBugs065.CorrerLista("Bug995a,Bug995b,Bug995c,Bug995d", true);
    public static partial class ChecksBugs065
    {
        // ---------------------------------------------------------------- medida independiente de "superficie dibujada"
        // Cajas orientadas de TODAS las mallas dibujables de las escenas (sin soldados, vehiculos, proyectiles ni decals). Medida independiente de
        // la que usa el runtime (SuperficieVisible solo mira la familia del collider): aca vale cualquier malla del mundo.
        struct R20Caja { public Transform t; public Bounds lb; public bool mundo; public Renderer r; }
        static List<R20Caja> R20Cajas()
        {
            var l = new List<R20Caja>(4096);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (r.GetComponentInParent<Soldier>() != null || r.GetComponentInParent<Projectile>() != null || r.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh == null) continue;
                if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; }
                // las mallas gigantes (montanas, horizonte) no cuentan: su caja envuelve medio nivel y "respaldaria" cualquier punto
                var wb = r.bounds; float mx = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z)), mn = Mathf.Min(wb.size.x, Mathf.Min(wb.size.y, wb.size.z));
                if (mx > 60f && mn > 1.5f) continue;
                // static batching: localBounds no describe la pieza (malla combinada); se usa la caja del mundo
                bool mundo = r is SkinnedMeshRenderer || r.isPartOfStaticBatch;
                l.Add(new R20Caja { t = r.transform, lb = mundo ? wb : r.localBounds, mundo = mundo, r = r });
            }
            return l;
        }

        static float R20DistCaja(in R20Caja c, Vector3 p)
        {
            if (c.mundo) return Mathf.Sqrt(c.lb.SqrDistance(p));
            var q = c.t.InverseTransformPoint(p); var lb = c.lb;
            var k = new Vector3(Mathf.Clamp(q.x, lb.min.x, lb.max.x), Mathf.Clamp(q.y, lb.min.y, lb.max.y), Mathf.Clamp(q.z, lb.min.z, lb.max.z));
            return (c.t.TransformPoint(k) - p).magnitude;
        }

        static float R20DistSuperficie(List<R20Caja> cajas, Vector3 p, out Renderer cual)
        {
            float mejor = float.MaxValue; cual = null;
            for (int i = 0; i < cajas.Count; i++)
            {
                var c = cajas[i]; if (c.r == null || !c.r.gameObject.activeInHierarchy) continue;
                if (!c.mundo && (c.t.position - p).sqrMagnitude > 400f * 400f) continue;
                float d = R20DistCaja(c, p); if (d < mejor) { mejor = d; cual = c.r; }
            }
            return mejor;
        }

        static string R20Ruta(Transform t)
        {
            var sb = new StringBuilder(t.name); int n = 0;
            for (var p = t.parent; p != null && n < 3; p = p.parent, n++) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        static string R20V(Vector3 v) => Inv($"({v.x:0.0},{v.y:0.0},{v.z:0.0})");

        // Primer solido del fisico crudo entre a y b (sin soldados ni vehiculos), como lo veia la bala antes de la R20.
        static readonly RaycastHit[] bufR20 = new RaycastHit[32];
        static bool R20PrimerSolido(Vector3 a, Vector3 b, out RaycastHit primero)
        {
            primero = default; var d = b - a; float L = d.magnitude; if (L < 0.01f) return false;
            int n = Physics.RaycastNonAlloc(a, d / L, bufR20, L, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var h = bufR20[i]; var c = h.collider;
                if (c == null || h.distance >= mejor) continue;
                if (!NavService.BlocksMovement(c) || c.GetComponentInParent<NoFrenaBalas>() != null) continue;
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null || c is TerrainCollider) continue;
                mejor = h.distance; primero = h; hay = true;
            }
            return hay;
        }

        struct R20Linea { public Soldier e; public Vector3 ojo, blanco; public int tipo; public Collider col; public float distCol; }   // tipo 0 libre, 1 invisible, 2 pared visible

        // ---------------------------------------------------------------- 995a
        static IEnumerator Bug995a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool hsAntes = Projectile.HeadshotMataAlInstante; Projectile.HeadshotMataAlInstante = false;
            var invisibles = new Dictionary<Collider, string>();
            int lineas = 0, libres = 0, libresPegan = 0, invis = 0, invisFrenadas = 0, paredes = 0, paredesAtravesadas = 0, mundo = 0, decalesFlotantes = 0, sinDecal = 0, disparos = 0;
            float peorDecal = 0f, peorImpacto = 0f; string peorDetalle = "";
            var fallos = new StringBuilder(); var diagLibres = new StringBuilder();
            var errores = new List<string>();
            Application.LogCallback alLog = (msg, st, t) => { if ((t == LogType.Error || t == LogType.Exception) && errores.Count < 3 && !errores.Contains(msg)) errores.Add(msg.Length > 160 ? msg.Substring(0, 160) : msg); };
            Application.logMessageReceived += alLog;
            foreach (int objetivo in new[] { 1, 2, 3, 5 })
            {
                OperacionPrueba.Arrancar(objetivo, 1);
                ModoDios.Poner(true);
                foreach (var x in Esperar(2.5f)) yield return x;
                var yo = W9Yo(); var pool = ProjectilePool.Activo;
                if (yo == null || pool == null) { sb.Append($"obj{objetivo}: sin jugador/pool; "); ok = false; continue; }
                var apagados = ApagarIaAjena(yo);
                var cajas = R20Cajas();
                var enemigos = new List<Soldier>();
                foreach (var s in ActorRegistry.All)
                    if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy && s.GetComponent<PerroEnemigo>() == null) enemigos.Add(s);
                if (enemigos.Count < 6)
                {
                    // objetivos con la guardia en reserva (inactiva): se activan los mas cercanos
                    var reserva = new List<Soldier>();
                    foreach (var s in ActorRegistry.All)
                        if (s != null && s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && !s.gameObject.activeSelf && s.GetComponent<PerroEnemigo>() == null) reserva.Add(s);
                    reserva.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
                    foreach (var s in reserva) { if (enemigos.Count >= 10) break; s.gameObject.SetActive(true); if (s.gameObject.activeInHierarchy) enemigos.Add(s); }
                    for (int fr = 0; fr < 4; fr++) yield return null;
                }
                enemigos.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
                if (enemigos.Count > 14) enemigos.RemoveRange(14, enemigos.Count - 14);
                foreach (var e in enemigos) { var ag = e.GetComponent<NavMeshAgent>(); if (ag != null) ag.enabled = false; }
                yield return null;

                // lineas de tiro: desde el jugador y desde 12 puestos de pie (navmesh) alrededor de cada enemigo, a 9 y 18 m
                var elegidas = new List<R20Linea>();
                int nL = 0, nI = 0, nP = 0;
                foreach (var e in enemigos)
                {
                    var cuerpo = e.GetComponent<Collider>(); var blanco = cuerpo != null ? cuerpo.bounds.center : e.transform.position + Vector3.up * 1.1f;   // agachado o de pie: al centro del cuerpo
                    var puestos = new List<Vector3> { yo.transform.position };
                    foreach (float dist in new[] { 9f, 18f })
                        for (int a = 0; a < 6; a++)
                        {
                            float ang = (a * 60f + (dist > 10f ? 30f : 0f)) * Mathf.Deg2Rad;
                            var p = e.transform.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * dist;
                            if (!NavMesh.SamplePosition(p, out var nh, 1.2f, NavMesh.AllAreas) || Mathf.Abs(nh.position.y - e.transform.position.y) > 2f) continue;
                            puestos.Add(nh.position);
                        }
                    foreach (var pu in puestos)
                    {
                        var ojo = pu + Vector3.up * 1.55f;
                        if (Physics.CheckSphere(ojo, 0.15f, ~0, QueryTriggerInteraction.Ignore)) continue;   // el tirador no puede estar adentro de algo
                        lineas++;
                        var l = new R20Linea { e = e, ojo = ojo, blanco = blanco };
                        if (R20PrimerSolido(ojo, blanco, out var h))
                        {
                            float dSup = R20DistSuperficie(cajas, h.point, out _);
                            bool invisible = dSup > SuperficieVisible.Tolerancia;
                            l.col = h.collider; l.distCol = (blanco - h.point).magnitude;
                            if (invisible)
                            {
                                l.tipo = 1;
                                if (!invisibles.ContainsKey(h.collider))
                                {
                                    var rs = h.collider.GetComponentsInChildren<Renderer>(true);
                                    var b = h.collider.bounds;
                                    invisibles[h.collider] = $"{R20Ruta(h.collider.transform)} [{h.collider.GetType().Name}] centro {R20V(b.center)} tam {R20V(b.size)}, renderers propios {rs.Length}, impacto a {dSup:0.00} m de una malla";
                                }
                                if (nI < 16) { elegidas.Add(l); nI++; }
                            }
                            else { l.tipo = 2; if (nP < 6 && l.distCol > 5f) { elegidas.Add(l); nP++; } }
                        }
                        else if (nL < 12) { l.tipo = 0; elegidas.Add(l); nL++; }
                    }
                }

                foreach (var l in elegidas)
                {
                    var e = l.e; if (e == null || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) continue;
                    // se reclasifica justo antes del tiro (un barril que revento o un empujon cambian la linea)
                    Physics.SyncTransforms();
                    var cu = e.GetComponent<Collider>(); var l2 = l; if (cu != null) l2.blanco = cu.bounds.center;
                    if (R20PrimerSolido(l2.ojo, l2.blanco, out var h2))
                    {
                        l2.col = h2.collider; l2.distCol = (l2.blanco - h2.point).magnitude;
                        l2.tipo = R20DistSuperficie(cajas, h2.point, out _) > SuperficieVisible.Tolerancia ? 1 : 2;
                        if (l2.tipo == 2 && l2.distCol <= 5f) continue;
                        var om = h2.collider.GetComponentInParent<ObstacleMarker>(); if (om != null && om.EsExplosivo) continue;   // un barril revienta y empuja a todos
                    }
                    else l2.tipo = 0;
                    var lc = l2;
                    e.Health.Heal(e.Health.MaxHealth);
                    int serie0 = Projectile.SerieDeImpactosDelMundo, serieS0 = Projectile.SerieDeImpactosEnSoldado; int inv0 = Projectile.InvisiblesAtravesados;
                    var dir = (lc.blanco - lc.ojo).normalized;
                    var pr = pool.Spawn(lc.ojo, dir, yo.Id, TeamId.Player, 1, Color.cyan);
                    disparos++;
                    for (int f = 0; f < 40 && pr != null && Projectile.ActiveInstances.Contains(pr); f++) yield return null;
                    bool pego = Projectile.SerieDeImpactosEnSoldado != serieS0 && Projectile.UltimoSoldadoImpactado == e;
                    bool enMundo = Projectile.SerieDeImpactosDelMundo != serie0;
                    if (enMundo)
                    {
                        mundo++;
                        var punto = Projectile.UltimoPuntoDelMundo; var col = Projectile.UltimoColliderDelMundo;
                        float dImp = R20DistSuperficie(cajas, punto, out var rImp);
                        if (dImp > peorImpacto) { peorImpacto = dImp; peorDetalle = col != null ? R20Ruta(col.transform) + " " + R20V(punto) : R20V(punto); }
                        if (dImp > SuperficieVisible.Tolerancia) { invisFrenadas++; if (fallos.Length < 1800) fallos.Append($"[frenada en {(col != null ? R20Ruta(col.transform) : "?")} {R20V(punto)} a {dImp:0.00} m de malla] "); }
                        var dec = Projectile.UltimoDecal;
                        if (dec == null || !dec.activeInHierarchy) sinDecal++;
                        else
                        {
                            float dDec = R20DistSuperficie(cajas, dec.transform.position, out _);
                            if (dDec > peorDecal) peorDecal = dDec;
                            if (dDec > 0.05f) { decalesFlotantes++; if (fallos.Length < 1800) fallos.Append($"[decal a {dDec:0.00} m en {R20V(dec.transform.position)}] "); }
                        }
                    }
                    if (lc.tipo == 0) { libres++; if (pego) libresPegan++; else if (diagLibres.Length < 700) diagLibres.Append($"[{e.name} d={(lc.blanco - lc.ojo).magnitude:0} {(enMundo ? "mundo " + (Projectile.UltimoColliderDelMundo != null ? R20Ruta(Projectile.UltimoColliderDelMundo.transform) : "?") + " " + R20V(Projectile.UltimoPuntoDelMundo) : "nada")} vivo={e.Health.IsAlive} act={e.gameObject.activeInHierarchy}] "); }
                    else if (lc.tipo == 1) { invis++; }
                    else { paredes++; if (pego) { paredesAtravesadas++; if (fallos.Length < 1800) { Physics.Raycast(lc.ojo, dir, out var hh, (lc.blanco - lc.ojo).magnitude, ~0, QueryTriggerInteraction.Ignore); R20DistSuperficie(cajas, hh.point, out var rr); fallos.Append($"[atraveso {R20Ruta(lc.col.transform)} {lc.col.GetType().Name} tam {R20V(lc.col.bounds.size)} en {R20V(hh.point)}: a {SuperficieVisible.DistanciaALaSuperficie(lc.col, hh.point):0.00} m de su malla propia, malla mas cercana {(rr != null ? R20Ruta(rr.transform) : "-")}, renderers propios {SuperficieVisible.De(lc.col).Length}, invisiblesAtravesados {Projectile.InvisiblesAtravesados - inv0}] "); } } }
                }
                sb.Append($"obj{objetivo}: {enemigos.Count} enemigos, {elegidas.Count} tiros; ");
                foreach (var m in apagados) if (m != null) m.enabled = true;
                ModoDios.Poner(false);
                yield return null;
            }
            Projectile.HeadshotMataAlInstante = hsAntes;
            Application.logMessageReceived -= alLog;
            sb.Append($"lineas medidas {lineas}, disparos {disparos}; ");
            W8Ok(ref ok, disparos >= 40, sb, $"{disparos} disparos reales (>= 40)");
            W8Ok(ref ok, invisFrenadas == 0, sb, $"balas frenadas lejos de una malla {invisFrenadas} de {mundo} impactos en el escenario (peor {peorImpacto:0.00} m {peorDetalle})");
            W8Ok(ref ok, decalesFlotantes == 0, sb, $"decals a > 0,05 m de una malla {decalesFlotantes} (peor {peorDecal:0.000} m; {sinDecal} impactos sin decal)");
            W8Ok(ref ok, libres == 0 || libresPegan >= Mathf.CeilToInt(libres * 0.8f), sb, $"lineas libres que pegan {libresPegan}/{libres} (>= 80 %)");
            W8Ok(ref ok, paredesAtravesadas == 0, sb, $"paredes visibles atravesadas {paredesAtravesadas}/{paredes}");
            sb.Append($"lineas cruzadas por collider invisible (fisico crudo) {invis}; colliders invisibles distintos {invisibles.Count}: ");
            int k = 0; foreach (var kv in invisibles) { if (k++ >= 25) { sb.Append("..."); break; } sb.Append(kv.Value).Append(" | "); }
            if (fallos.Length > 0) sb.Append(" FALLOS: ").Append(fallos);
            if (diagLibres.Length > 0) sb.Append(" LIBRES SIN PEGAR: ").Append(diagLibres);
            if (errores.Count > 0) sb.Append(" ERRORES: ").Append(string.Join(" || ", errores));
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 995b
        static IEnumerator Bug995b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var yo = W9Yo(); var pool = ProjectilePool.Activo;
            if (yo == null || pool == null) { Fin("FALLO sin jugador/pool"); yield break; }
            var apagados = ApagarIaAjena(yo);
            // coberturas destructibles cerca del jugador con superficie dibujada
            var marcas = new List<ObstacleMarker>();
            foreach (var m in Object.FindObjectsByType<ObstacleMarker>(FindObjectsInactive.Exclude))
            {
                if (m == null || m.IsCollapsed || !m.EsDestruible || m.EsExplosivo || m.SoloExplosiones) continue;
                var c = m.GetComponentInChildren<Collider>(); if (c == null || c.isTrigger || SuperficieVisible.SinSuperficie(c)) continue;
                marcas.Add(m);
            }
            marcas.Sort((p, q) => (p.transform.position - yo.transform.position).sqrMagnitude.CompareTo((q.transform.position - yo.transform.position).sqrMagnitude));
            sb.Append($"{marcas.Count} coberturas destructibles; ");
            int probadas = 0;
            for (int i = 0; i < marcas.Count && probadas < 2; i++)
            {
                var m = marcas[i]; var c = m.GetComponentInChildren<Collider>();
                var b = c.bounds; var centro = b.center;
                // un tiro horizontal desde 6 m, del lado sin otros solidos en el medio
                bool disparado = false;
                for (int a = 0; a < 8 && !disparado; a++)
                {
                    float ang = a * 45f * Mathf.Deg2Rad;
                    var ojo = centro + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (6f + b.extents.magnitude);
                    if (Physics.CheckSphere(ojo, 0.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!R20PrimerSolido(ojo, centro, out var h) || h.collider != c) continue;
                    // el tiro no tiene que derrumbarla: se le sube la vida
                    m.ConfigurarVida(100000);
                    int serie0 = Projectile.SerieDeImpactosDelMundo;
                    var pr = pool.Spawn(ojo, (centro - ojo).normalized, yo.Id, TeamId.Player, 1, Color.cyan);
                    for (int f = 0; f < 30 && pr != null && Projectile.ActiveInstances.Contains(pr); f++) yield return null;
                    disparado = true;
                    var dec = Projectile.UltimoDecal;
                    bool marco = Projectile.SerieDeImpactosDelMundo != serie0 && dec != null && dec.activeInHierarchy;
                    W8Ok(ref ok, marco, sb, $"{m.name}: decal creado ({(dec != null ? R20V(dec.transform.position) : "-")})");
                    if (!marco) break;
                    var an = dec.GetComponent<DecalAnclado>();
                    W8Ok(ref ok, an != null && an.Ancla == Projectile.UltimoColliderDelMundo, sb, $"{m.name}: anclado al collider tocado");
                    foreach (var x in Esperar(0.6f)) yield return x;
                    W8Ok(ref ok, dec.activeInHierarchy, sb, $"{m.name}: sigue visible con la pieza en pie");
                    if (probadas == 0) { m.gameObject.SetActive(false); sb.Append("(pieza apagada) "); }
                    else { m.Demoler(); sb.Append("(pieza demolida) "); }
                    foreach (var x in Esperar(0.6f)) yield return x;
                    W8Ok(ref ok, !dec.activeInHierarchy, sb, $"{m.name}: el decal se fue con la pieza");
                    if (probadas == 0) m.gameObject.SetActive(true);
                    probadas++;
                }
            }
            W8Ok(ref ok, probadas == 2, sb, $"{probadas} coberturas probadas (2)");
            foreach (var a in apagados) if (a != null) a.enabled = true;
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 995c: informe de colliders solidos sin superficie o mayores que su malla
        // Lista (no falla): solidos que frenan el paso y que el runtime R20 deja pasar a las balas (SinSuperficie) o que sobresalen > 0,3 m de
        // la caja de sus mallas (la bala sigue hasta la malla). Sirve para ubicar los "colliders invisibles" del pedido.
        static IEnumerator Bug995c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(1, 1);
            foreach (var x in Esperar(2.5f)) yield return x;
            var sin = new List<string>(); var grandes = new List<(float, string)>(); int total = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c == null || !NavService.BlocksMovement(c) || SuperficieVisible.Exento(c) || c.GetComponentInParent<NoFrenaBalas>() != null) continue;
                total++;
                var b = c.bounds;
                if (SuperficieVisible.SinSuperficie(c)) { sin.Add($"{R20Ruta(c.transform)} [{c.GetType().Name}] centro {R20V(b.center)} tam {R20V(b.size)}"); continue; }
                var rs = SuperficieVisible.De(c); bool hay = false; Bounds u = default;
                foreach (var r in rs) { if (r == null || !r.gameObject.activeInHierarchy) continue; if (!hay) { u = r.bounds; hay = true; } else u.Encapsulate(r.bounds); }
                if (!hay) continue;
                float sobra = Mathf.Max(Mathf.Max(u.min.x - b.min.x, b.max.x - u.max.x), Mathf.Max(Mathf.Max(u.min.y - b.min.y, b.max.y - u.max.y), Mathf.Max(u.min.z - b.min.z, b.max.z - u.max.z)));
                if (sobra > 0.3f) grandes.Add((sobra, $"{R20Ruta(c.transform)} centro {R20V(b.center)} tam {R20V(b.size)} malla {R20V(u.size)} sobra {sobra:0.00} m"));
            }
            grandes.Sort((p, q) => q.Item1.CompareTo(p.Item1));
            var sb = new StringBuilder($"{total} solidos; SIN SUPERFICIE {sin.Count}: ");
            for (int i = 0; i < sin.Count && i < 40; i++) sb.Append(sin[i]).Append(" | ");
            sb.Append($" MAYORES QUE SU MALLA (> 0,3 m) {grandes.Count}: ");
            for (int i = 0; i < grandes.Count && i < 30; i++) sb.Append(grandes[i].Item2).Append(" | ");
            Fin("OK " + sb);
        }
        // ---------------------------------------------------------------- 995d: tiros a la parte SIN malla de los colliders mas grandes que su pieza
        // (puertas de acceso de los servidores: jambas/hojas de 14-21 m de alto con malla de 4 m; cuenco de la fuente de 6 m con malla de 0,9;
        // pilas de escombro). Antes la bala se frenaba en el aire y dejaba el decal flotando; ahora sigue de largo o marca sobre la malla.
        static IEnumerator Bug995d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var yo = W9Yo(); var pool = ProjectilePool.Activo;
            if (yo == null || pool == null) { Fin("FALLO sin jugador/pool"); yield break; }
            var apagados = ApagarIaAjena(yo);
            var cajas = R20Cajas();
            var lista = new List<(float, Collider, Vector3)>();
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c == null || !NavService.BlocksMovement(c) || SuperficieVisible.Exento(c) || c.GetComponentInParent<NoFrenaBalas>() != null) continue;
                var rs = SuperficieVisible.De(c); bool hay = false; Bounds u = default;
                foreach (var r in rs) { if (r == null || !r.gameObject.activeInHierarchy) continue; if (!hay) { u = r.bounds; hay = true; } else u.Encapsulate(r.bounds); }
                if (!hay) continue;
                var b = c.bounds;
                // punto adentro del collider y a >= 0,4 m de toda malla: arriba de la malla (lo comun: el collider sube de mas)
                if (b.max.y - u.max.y < 0.8f) continue;
                var p = new Vector3(b.center.x, Mathf.Min(b.max.y - 0.2f, u.max.y + 0.6f), b.center.z);
                if (R20DistSuperficie(cajas, p, out _) < 0.4f) continue;
                lista.Add((b.max.y - u.max.y, c, p));
            }
            lista.Sort((p, q) => q.Item1.CompareTo(p.Item1));
            int probados = 0, frenadas = 0, flotantes = 0, crudos = 0;
            var det = new StringBuilder();
            foreach (var (sobra, c, p) in lista)
            {
                if (probados >= 14) break;
                bool tirado = false;
                for (int a = 0; a < 8 && !tirado; a++)
                {
                    float ang = a * 45f * Mathf.Deg2Rad;
                    var ojo = p + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (8f + c.bounds.extents.magnitude);
                    if (Physics.CheckSphere(ojo, 0.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!R20PrimerSolido(ojo, p + (p - ojo).normalized * 0.5f, out var h) || h.collider != c) continue;
                    crudos++;
                    int serie0 = Projectile.SerieDeImpactosDelMundo;
                    var pr = pool.Spawn(ojo, (p - ojo).normalized, yo.Id, TeamId.Player, 1, Color.cyan);
                    for (int f = 0; f < 40 && pr != null && Projectile.ActiveInstances.Contains(pr); f++) yield return null;
                    tirado = true; probados++;
                    bool freno = Projectile.SerieDeImpactosDelMundo != serie0 && Projectile.UltimoColliderDelMundo == c;
                    float dSup = freno ? R20DistSuperficie(cajas, Projectile.UltimoPuntoDelMundo, out _) : 0f;
                    var dec = freno ? Projectile.UltimoDecal : null;
                    float dDec = dec != null && dec.activeInHierarchy ? R20DistSuperficie(cajas, dec.transform.position, out _) : 0f;
                    if (freno && dSup > SuperficieVisible.Tolerancia) frenadas++;
                    if (dDec > 0.05f) flotantes++;
                    if (det.Length < 1500) det.Append($"[{R20Ruta(c.transform)} sobra {sobra:0.0} m en {R20V(p)}: {(freno ? $"freno a {dSup:0.00} m de malla" : "siguio")}] ");
                }
            }
            W8Ok(ref ok, probados >= 8, sb, $"{probados} colliders sobredimensionados tiroteados ({lista.Count} candidatos; el fisico crudo los tocaba {crudos})");
            W8Ok(ref ok, frenadas == 0, sb, $"balas frenadas en el aire {frenadas}");
            W8Ok(ref ok, flotantes == 0, sb, $"decals flotantes {flotantes}");
            sb.Append(det);
            foreach (var m in apagados) if (m != null) m.enabled = true;
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
