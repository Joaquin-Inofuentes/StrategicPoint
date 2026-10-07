using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;
using SP.Presentation;

namespace SP.EditorTools
{
    // WP8: rediseno del cuartel (#078). Hay que estar en Play sobre SC_Operacion y conviene un Play fresco por check
    // (ChecksBugs065.CorrerUno("Bug078b")); Correr(78) los corre todos en orden a-e (el ultimo, la cadena, mata a todo el cuartel).
    //   078a  estructura: 2 torres de francotirador, 2 de torreta, 4 reflectores y 4 sectores con cartel dentro del cuartel; el portón
    //         y los 4 sectores son alcanzables (flood-fill con capsulas y rutas del NavMesh); 28 enemigos.
    //   078b  reflector: sigue al poseido, alerta, HUD, dispersion x0,75, se rompe a tiros / a explosion, queda fijo sin operador.
    //   078c  francotirador (laser 1,0 s antes de cada tiro, no baja de la torre) y torreta (arco de 120 grados).
    //   078d  torre: las balas no la tocan, 2 cohetes la tiran, mata al tirador, el pivote vuelca.
    //   078e  cadena: matar a los 28 abre el porton y la fase pasa a Puestos; la linea de subobjetivos se muestra y se limpia.
    public static partial class ChecksBugs065
    {
        static Soldier W8Yo()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        static bool W8Ok(ref bool ok, bool cond, StringBuilder sb, string texto)
        {
            if (!cond) ok = false;
            sb.Append(texto).Append(cond ? " OK; " : " MAL; ");
            return cond;
        }

        static bool W8Libre(Vector3 p)
        {
            return !Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore);
        }

        // Un punto de suelo libre y alcanzable (sobre el NavMesh), a la vista de 'desde' (la boca de un tirador o una lampara).
        // yawCentro/arco acotan la direccion; ignorar = raiz que no tapa la vista (la torre propia).
        static bool W8PuntoVisto(Vector3 desde, float yawCentro, float arco, float dmin, float dmax, Transform ignorar, out Vector3 punto, float pasoDist = 2.5f, float pasoAng = 5f)
        {
            punto = default;
            for (float dist = dmin; dist <= dmax; dist += pasoDist)
                for (float a = 0f; a <= arco; a += pasoAng)
                    foreach (var sgn in new[] { 1f, -1f })
                    {
                        float yaw = yawCentro + a * sgn;
                        var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        var c = new Vector3(desde.x, 0f, desde.z) + dir * dist;
                        if (!NavMesh.SamplePosition(c, out var h, 1.0f, NavMesh.AllAreas) || (new Vector3(h.position.x, 0f, h.position.z) - c).magnitude > 0.6f) continue;
                        var p = h.position; p.y = 0f;
                        if (!W8Libre(p)) continue;
                        if (!NavService.HayLineaDeTiro(desde, p + Vector3.up * 1.05f, ignorar, null)) continue;
                        punto = p; return true;
                    }
            return false;
        }

        static void W8Teletransportar(Vector3 p, Vector3 mirandoA)
        {
            var d = OperacionDirector.Instancia;
            var v = mirandoA - p; v.y = 0f;
            d.TeletransportarEscuadra(p, v.sqrMagnitude > 0.01f ? Quaternion.LookRotation(v.normalized) : Quaternion.identity);
        }

        static TiradorDeTorre W8Tirador(TorreDestruible.Tipo tipo, int indice = 0)
        {
            var d = OperacionDirector.Instancia;
            int n = 0;
            foreach (var t in d.torres)
                if (t != null && t.tipo == tipo)
                {
                    if (n++ == indice) return t.GetComponent<TiradorDeTorre>();
                }
            return null;
        }

        // Deja el haz del reflector (en barrido) cruzando el punto: se fija su yaw/pitch internos para que el cono ya lo contenga (el barrido sigue desde ahi).
        static void W8ApuntarHaz(ReflectorVigia r, Vector3 punto)
        {
            var dir = (punto - r.cabeza.position).normalized;
            var e = Quaternion.LookRotation(dir, Vector3.up).eulerAngles;
            var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ReflectorVigia).GetField("yaw", fl).SetValue(r, e.y + 4f);   // 4 grados al costado: el barrido lo cruza y lo engancha
            typeof(ReflectorVigia).GetField("pitch", fl).SetValue(r, Mathf.DeltaAngle(0f, e.x));
            r.cabeza.rotation = Quaternion.Euler(Mathf.DeltaAngle(0f, e.x), e.y + 4f, 0f);
        }

        static bool W8EsTirador(OperacionDirector d, Soldier s)
        {
            foreach (var t in d.torres) if (t != null && t.ocupante == s) return true;
            return false;
        }

        static bool W8EnLimitesDelCuartel(Vector3 p) => p.x > -61f && p.x < 61f && p.z > -341f && p.z < -219f;

        // ---------------------------------------------------------------- 078a estructura y alcanzabilidad
        static IEnumerator Bug078a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;

            // 1) componentes dentro de los limites del cuartel
            int tf = 0, tt = 0, rf = 0, fuera = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<TorreDestruible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!W8EnLimitesDelCuartel(t.transform.position)) fuera++;
                if (t.tipo == TorreDestruible.Tipo.Francotirador) tf++; else tt++;
                var mk = t.GetComponent<ObstacleMarker>();
                if (mk == null || !mk.SoloExplosiones || mk.MaxHealth != (t.tipo == TorreDestruible.Tipo.Francotirador ? TorreDestruible.VidaFrancotirador : TorreDestruible.VidaTorreta)) { ok = false; sb.Append("marca de " + t.name + " mal; "); }
            }
            foreach (var r in UnityEngine.Object.FindObjectsByType<ReflectorVigia>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!W8EnLimitesDelCuartel(r.transform.position)) fuera++;
                rf++;
                var l = r.cabeza != null ? r.cabeza.GetComponent<Light>() : null;
                var lum = r.cabeza != null ? r.cabeza.GetComponent<Luminaria>() : null;
                var bc = r.cabeza != null ? r.cabeza.GetComponent<BoxCollider>() : null;
                bool lamp = l != null && l.type == LightType.Spot && Mathf.Approximately(l.range, 45f) && Mathf.Approximately(l.spotAngle, 18f) && l.shadows == LightShadows.None && lum != null && lum.Health == 1 && bc != null && Mathf.Abs(bc.size.x * r.cabeza.localScale.x - 1.4f) < 0.01f;
                if (!lamp) { ok = false; sb.Append("lampara de " + r.name + " mal; "); }
            }
            W8Ok(ref ok, tf == 2 && tt == 2 && rf == 4 && fuera == 0, sb, $"torres francotirador={tf} torreta={tt} reflectores={rf} fuera={fuera}");
            int sectores = 0; var vistos = new HashSet<string>();
            var raiz = RaicesDeEscena.Buscar("Operacion");
            foreach (var tr in raiz.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("Rotulo_SECTOR ", StringComparison.Ordinal) && W8EnLimitesDelCuartel(tr.position))
                    vistos.Add(tr.name.Substring(14, 1));
            sectores = vistos.Count;
            W8Ok(ref ok, sectores == 4 && vistos.Contains("A") && vistos.Contains("B") && vistos.Contains("C") && vistos.Contains("D"), sb, $"sectores con cartel={sectores}");

            // 2) 28 enemigos: 20 guardias + 2 francotiradores + 2 artilleros + 4 operadores
            int guardias = 0, arriba = 0, ops = 0;
            foreach (var s in d.enemigosCuartel)
            {
                if (s == null) continue;
                if (W8EsTirador(d, s)) arriba++;
                else { bool op = false; foreach (var r in d.reflectores) if (r != null && r.operador == s) op = true; if (op) ops++; else guardias++; }
            }
            W8Ok(ref ok, d.enemigosCuartel.Length == 28 && guardias == 20 && arriba == 4 && ops == 4, sb, $"enemigos={d.enemigosCuartel.Length} guardias={guardias} tiradores={arriba} operadores={ops}");
            int enCobertura = 0; foreach (var s in d.enemigosCuartel) if (s != null && s.Brain != null && s.Brain.Atrincherado) enCobertura++;
            W8Ok(ref ok, enCobertura == 20, sb, $"guardias atrincherados={enCobertura}/20");

            // 3) alcanzabilidad por flood-fill con capsulas (celdas de 1 m) desde la brecha, sobre todo el cuartel
            const float x0 = -60f, z0 = -339f; const int nx = 120, nz = 118;
            var libre = new bool[nx, nz]; var visto = new bool[nx, nz];
            int celdasLibres = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    libre[i, j] = W8Libre(new Vector3(x0 + i + 0.5f, 0f, z0 + j + 0.5f));
                    if (libre[i, j]) celdasLibres++;
                }
            var cola = new Queue<(int, int)>();
            int si = (int)(0f - x0), sj = 0;
            if (libre[si, sj]) { visto[si, sj] = true; cola.Enqueue((si, sj)); }
            int alcanzadas = 0;
            while (cola.Count > 0)
            {
                var (ci, cj) = cola.Dequeue(); alcanzadas++;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = ci + di, nj = cj + dj;
                        if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || !libre[ni, nj] || visto[ni, nj]) continue;
                        if (di != 0 && dj != 0 && (!libre[ci + di, cj] || !libre[ci, cj + dj])) continue;   // sin cortar esquinas
                        visto[ni, nj] = true; cola.Enqueue((ni, nj));
                    }
            }
            Func<float, float, bool> alc = (px, pz) => { int i = Mathf.Clamp((int)(px - x0), 0, nx - 1), j = Mathf.Clamp((int)(pz - z0), 0, nz - 1); return visto[i, j]; };
            // Muestras por sector (suelo libre) y el frente del porton (cerrado: se llega hasta tocarlo).
            (string n, float x, float z)[] muestras =
            {
                ("A", 0f, -337f), ("A este", 40f, -330f), ("A oeste", -50f, -322f), ("B", -22f, -292f), ("B callejon", -48f, -295.5f), ("B trasera", -58f, -280f),
                ("patio", 0f, -290f), ("C", 40f, -295f), ("C fondo", 55f, -270f), ("D oeste", -30f, -255f), ("D este", 30f, -255f), ("D patio norte", -35f, -236f),
                ("D patio este", 45f, -236f), ("frente al porton", 0f, -222.5f), ("Comando oeste", -15f, -246f), ("Comando este", 15f, -246f),
            };
            var noAlc = new List<string>();
            foreach (var m in muestras) if (!alc(m.x, m.z)) noAlc.Add(m.n);
            W8Ok(ref ok, noAlc.Count == 0, sb, $"flood-fill: {alcanzadas}/{celdasLibres} celdas libres alcanzadas desde la brecha, muestras sin alcanzar=[{string.Join(", ", noAlc)}]");
            // Bolsillos grandes inalcanzables (mas de 40 m2 de suelo libre sin acceso) = sectores cerrados.
            int bolsillos = 0;
            var marcado = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!libre[i, j] || visto[i, j] || marcado[i, j]) continue;
                    int area = 0; var q = new Queue<(int, int)>(); q.Enqueue((i, j)); marcado[i, j] = true;
                    while (q.Count > 0) { var (ci, cj) = q.Dequeue(); area++; for (int di = -1; di <= 1; di++) for (int dj = -1; dj <= 1; dj++) { int ni = ci + di, nj = cj + dj; if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || !libre[ni, nj] || visto[ni, nj] || marcado[ni, nj]) continue; marcado[ni, nj] = true; q.Enqueue((ni, nj)); } }
                    if (area >= 40) bolsillos++;
                }
            W8Ok(ref ok, bolsillos == 0, sb, $"bolsillos cerrados de >=40 m2={bolsillos}");

            // 4) rutas del NavMesh desde la entrada de la fase hasta cada guardia, la base de cada torre y el frente del porton
            var origen = d.entradas[0].position;
            int sinRuta = 0; var malos = new List<string>();
            var path = new NavMeshPath();
            foreach (var s in d.enemigosCuartel)
            {
                if (s == null || W8EsTirador(d, s)) continue;
                bool op = false; foreach (var r in d.reflectores) if (r != null && r.operador == s) op = true;
                if (op) continue;
                if (!NavMesh.SamplePosition(s.transform.position, out var h, 2f, NavMesh.AllAreas) || !NavMesh.CalculatePath(origen, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) { sinRuta++; malos.Add(s.name); }
            }
            foreach (var t in d.torres)
            {
                var b = t.transform.position;
                bool alguno = false;
                foreach (var of in new[] { new Vector3(4.5f, 0, 0), new Vector3(-4.5f, 0, 0), new Vector3(0, 0, 4.5f), new Vector3(0, 0, -4.5f) })
                    if (NavMesh.SamplePosition(b + of, out var h, 1.5f, NavMesh.AllAreas) && NavMesh.CalculatePath(origen, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) { alguno = true; break; }
                if (!alguno) { sinRuta++; malos.Add("base " + t.name); }
            }
            if (NavMesh.SamplePosition(new Vector3(0f, 0f, -223f), out var hp, 2f, NavMesh.AllAreas) && NavMesh.CalculatePath(origen, hp.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) { }
            else { sinRuta++; malos.Add("frente del porton"); }
            W8Ok(ref ok, sinRuta == 0, sb, $"rutas NavMesh desde la entrada: sin ruta={sinRuta} [{string.Join(", ", malos)}]");

            Fin((ok ? "OK " : "FALLO ") + "estructura del cuartel: " + sb);
        }

        // ---------------------------------------------------------------- 078b reflector
        static IEnumerator Bug078b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            bool iaAntes = AiBrain.IAPausada; AiBrain.IAPausada = true;   // el cuartel quieto: solo se mide el reflector
            var inv = Wp7HacerInvulnerable();
            var yo = W8Yo();
            if (yo == null) { Fin("FALLO no hay soldado poseido"); yield break; }
            var hud = OperacionHud.Instancia;
            try
            {
                // --- A) R1: poseido a ~30 m dentro del cono, sigue en 2 s a menos de 6 grados (y despues de moverse) ---
                var r1 = d.reflectores[0];
                if (!W8PuntoVisto(r1.cabeza.position, r1.yawBase, 40f, 26f, 34f, r1.transform, out var p1)) { Fin("FALLO no encontre un punto libre a 30 m de R1 con vision"); yield break; }
                W8Teletransportar(p1, r1.cabeza.position);
                yield return null;
                var chest = yo.transform.position + Vector3.up * 0.25f;
                W8ApuntarHaz(r1, chest);   // el haz lo cruza (como en el barrido)
                foreach (var x in Esperar(2.0f)) yield return x;
                chest = yo.transform.position + Vector3.up * 0.25f;
                float ang1 = Vector3.Angle(r1.cabeza.forward, chest - r1.cabeza.position);
                float dist1 = Vector3.Distance(r1.cabeza.position, chest);
                W8Ok(ref ok, r1.Siguiendo && r1.Blanco == yo && ang1 < 6f && r1.OperadorVivo, sb, $"R1 sigue al poseido a {dist1:0.0} m: angulo {ang1:0.0} grados a los 2 s (blanco={(r1.Blanco != null ? r1.Blanco.name : "-")}, poseido={yo.name}, operadorVivo={r1.OperadorVivo}, roto={r1.EstaRoto}, luz={r1.Luz.enabled}, estaAlumbrando={r1.EstaAlumbrando(yo)})");
                bool hudVisible = hud != null && hud.IluminadoVisible && hud.TextoIluminado.Contains("ILUMINANDO") && hud.TextoIluminado.Contains("REFLECTOR");
                W8Ok(ref ok, ReflectorVigia.PoseidoIluminado && hudVisible, sb, $"HUD: vineta y cartel visibles ('{(hud != null ? hud.TextoIluminado.Replace("\n", " / ") : "-")}', alfa {(hud != null ? hud.AlfaDeIluminacion : 0f):0.00})");

                // se mueve 5 m de costado y lo sigue
                var lado = Vector3.Cross(Vector3.up, (p1 - r1.cabeza.position)).normalized;
                Vector3 p1b = p1 + lado * 5f; bool movio = false;
                if (NavMesh.SamplePosition(p1b, out var hb, 1f, NavMesh.AllAreas) && W8Libre(hb.position) && NavService.HayLineaDeTiro(r1.cabeza.position, hb.position + Vector3.up * 1.05f, r1.transform, null))
                {
                    yo.transform.position = new Vector3(hb.position.x, yo.transform.position.y, hb.position.z); movio = true;
                }
                foreach (var x in Esperar(2.0f)) yield return x;
                chest = yo.transform.position + Vector3.up * 0.25f;
                float ang1b = Vector3.Angle(r1.cabeza.forward, chest - r1.cabeza.position);
                W8Ok(ref ok, !movio || (r1.Siguiendo && ang1b < 6f), sb, $"R1 tras moverse 5 m{(movio ? "" : " (no se pudo mover)")}: angulo {ang1b:0.0}");

                // alerta: enemigos libres a 50 m o menos reciben la orden (hay guardias del sector A y B cerca)
                W8Ok(ref ok, r1.Alertas >= 1, sb, $"R1 alerto a {r1.Alertas} enemigos");

                // dispersion: contra el iluminado vale x0,75; en otra direccion, x1
                var origen = r1.cabeza.position + new Vector3(8f, -5f, 0f);
                float f1 = ReflectorVigia.FactorDeDispersionContraIluminado(origen, chest - origen);
                float f0 = ReflectorVigia.FactorDeDispersionContraIluminado(origen, -(chest - origen));
                W8Ok(ref ok, Mathf.Abs(f1 - 0.75f) < 0.001f && Mathf.Abs(f0 - 1f) < 0.001f, sb, $"dispersion contra el iluminado x{f1:0.00} (contra otro x{f0:0.00})");

                // --- B) un disparo a la lampara la rompe: luz apagada, historial de rotos, haz y viñeta fuera ---
                var lum = r1.cabeza.GetComponent<Luminaria>();
                lum.TakeDamage(1, r1.cabeza.position);
                yield return null; yield return null;
                bool roto = !r1.Luz.enabled && r1.EstaRoto && ReflectorVigia.Rotos.Contains(r1) && !r1.HazVisible && r1.Blanco == null;
                W8Ok(ref ok, roto, sb, $"R1 tras 1 disparo: luz={(r1.Luz.enabled ? "ON" : "apagada")}, en rotos={ReflectorVigia.Rotos.Contains(r1)}, haz visible={r1.HazVisible}");
                foreach (var x in Esperar(1.0f)) yield return x;
                W8Ok(ref ok, !ReflectorVigia.PoseidoIluminado && (hud == null || !hud.IluminadoVisible), sb, "viñeta apagada tras romperse");

                // --- C) R2: al morir el operador el haz queda fijo ---
                var r2 = d.reflectores[1];
                if (!W8PuntoVisto(r2.cabeza.position, r2.yawBase, 50f, 22f, 32f, r2.transform, out var p2)) { Fin("FALLO no encontre un punto libre a 25 m de R2 con vision: " + sb); yield break; }
                W8Teletransportar(p2, r2.cabeza.position);
                yield return null;
                chest = yo.transform.position + Vector3.up * 0.25f;
                W8ApuntarHaz(r2, chest);
                foreach (var x in Esperar(1.5f)) yield return x;
                bool seguiaAntes = r2.Siguiendo;
                ComandosDeDepuracion.Matar(r2.operador.Id);
                yield return null; yield return null;
                var rotFija = r2.cabeza.rotation;
                // el poseido se va a otro lado dentro de su vision: no lo sigue
                var otro = p2 + Vector3.Cross(Vector3.up, (p2 - r2.cabeza.position)).normalized * 6f;
                if (NavMesh.SamplePosition(otro, out var ho, 1f, NavMesh.AllAreas)) yo.transform.position = new Vector3(ho.position.x, yo.transform.position.y, ho.position.z);
                foreach (var x in Esperar(1.5f)) yield return x;
                float giro = Quaternion.Angle(rotFija, r2.cabeza.rotation);
                W8Ok(ref ok, seguiaAntes && !r2.OperadorVivo && !r2.Siguiendo && giro < 0.5f && r2.Luz.enabled, sb, $"R2 sin operador: seguia antes={seguiaAntes}, ahora siguiendo={r2.Siguiendo}, giro {giro:0.00} grados en 1,5 s, luz encendida={r2.Luz.enabled}");

                // --- D) R3: una explosion a 3 m de la lampara la rompe ---
                var r3 = d.reflectores[2];
                float antesR3 = Vector3.Distance(Vector3.zero, Vector3.zero);
                Projectile.ExplodeAt(r3.cabeza.position + Vector3.right * 2.0f, 5f, 95, -1, null);
                yield return null; yield return null;
                W8Ok(ref ok, r3.EstaRoto && !r3.Luz.enabled, sb, $"R3 tras explosion a 2 m: roto={r3.EstaRoto}");
                var r4 = d.reflectores[3];
                Projectile.ExplodeAt(r4.cabeza.position + Vector3.right * 6f, 5f, 95, -1, null);
                yield return null; yield return null;
                W8Ok(ref ok, !r4.EstaRoto, sb, $"R4 tras explosion a 6 m: intacto={!r4.EstaRoto}");
                W8Ok(ref ok, d.ReflectoresRotos == 2, sb, $"director cuenta {d.ReflectoresRotos} reflectores rotos (esperado 2)");
            }
            finally { AiBrain.IAPausada = iaAntes; foreach (var h in inv) if (h != null) h.Invulnerable = false; }
            Fin((ok ? "OK " : "FALLO ") + "reflector: " + sb);
        }

        // ---------------------------------------------------------------- 078c francotirador y torreta
        static IEnumerator Bug078c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            bool iaAntes = AiBrain.IAPausada; AiBrain.IAPausada = true;
            var inv = Wp7HacerInvulnerable();
            var yo = W8Yo();
            var disparos = new List<float>(); var laserAntes = new List<float>(); float laserOn = -1f; bool prevLaser = false;
            IDisposable sub = null;
            try
            {
                // --- A) francotirador TF2: laser 1,0 s antes de cada tiro, no baja de la torre, 95 de dano, cada 2,8 s ---
                var sniper = W8Tirador(TorreDestruible.Tipo.Francotirador, 1) as FrancotiradorEnTorre;
                if (sniper == null) { Fin("FALLO no hay FrancotiradorEnTorre"); yield break; }
                var s = sniper.soldado;
                if (!W8PuntoVisto(sniper.Boca, -140f, 70f, 30f, 60f, s.transform, out var pv)) { Fin("FALLO no encontre un punto a la vista del francotirador TF2"); yield break; }
                int id = s.Id;
                sub = EventBus.Instance.Subscribe<ShotFiredEvent>(e =>
                {
                    if (e.ShooterId != id) return;
                    disparos.Add(Time.time);
                    laserAntes.Add(laserOn >= 0f ? Time.time - laserOn : -1f);
                });
                float alturaInicial = s.transform.position.y;
                W8Teletransportar(pv, sniper.Boca);
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 12.5f)
                {
                    bool l = sniper.LaserActivo;
                    if (l && !prevLaser) laserOn = Time.time;
                    prevLaser = l;
                    yield return null;
                }
                // los intervalos entre tiros: ~2,8 s
                bool todosLosLaser = laserAntes.Count >= 3;
                foreach (var a in laserAntes) if (a < 0.9f || a > 1.1f) todosLosLaser = false;
                bool cadencia = disparos.Count >= 3;
                for (int i = 1; i < disparos.Count; i++) if (Mathf.Abs(disparos[i] - disparos[i - 1] - 2.8f) > 0.25f) cadencia = false;
                W8Ok(ref ok, todosLosLaser, sb, $"TF2 laser antes de cada tiro [{string.Join(", ", laserAntes.ConvertAll(a => a.ToString("0.00")))}] s");
                W8Ok(ref ok, cadencia, sb, $"TF2 {disparos.Count} tiros en 12,5 s, cadencia 2,8 s");
                W8Ok(ref ok, s.transform.position.y > alturaInicial - 0.1f && s.transform.position.y > 12f, sb, $"TF2 no baja de la torre (y={s.transform.position.y:0.0})");
                var w = s.Weapon;
                W8Ok(ref ok, w.CurrentWeaponKind == WeaponKind.Sniper, sb, $"arma={w.CurrentWeaponKind} dano={FrancotiradorEnTorre.Dano}");
                sub.Dispose(); sub = null;

                // --- B) torreta TV1: dispara dentro del arco de 120 grados y NO fuera ---
                var art = W8Tirador(TorreDestruible.Tipo.Torreta, 0) as ArtilleroDeTorreta;
                if (art == null) { Fin("FALLO no hay ArtilleroDeTorreta: " + sb); yield break; }
                // fuera del arco primero: detras de la torreta
                int antes = art.Disparos;
                bool hayAtras = W8PuntoVisto(art.Boca, art.yawBase + 180f, 40f, 8f, 14f, art.soldado.transform, out var pa);
                if (hayAtras)
                {
                    W8Teletransportar(pa, art.Boca);
                    foreach (var x in Esperar(5f)) yield return x;
                    W8Ok(ref ok, art.Disparos == antes, sb, $"TV1 no dispara fuera del arco (detras, a {Vector3.Distance(art.Boca, pa):0} m): {art.Disparos - antes} tiros");
                }
                else sb.Append("(sin punto libre detras de TV1: arco trasero no medido); ");
                bool hayFrente = W8PuntoVisto(art.Boca, art.yawBase, 40f, 20f, 45f, art.soldado.transform, out var pf);
                if (!hayFrente) { Fin("FALLO no encontre un punto dentro del arco de TV1: " + sb); yield break; }
                antes = art.Disparos; int rafagasAntes = art.Rafagas;
                W8Teletransportar(pf, art.Boca);
                foreach (var x in Esperar(7f)) yield return x;
                W8Ok(ref ok, art.Disparos - antes >= 8 && art.Rafagas - rafagasAntes >= 1, sb, $"TV1 dispara dentro del arco ({Vector3.Distance(art.Boca, pf):0} m): {art.Disparos - antes} tiros en {art.Rafagas - rafagasAntes} rafagas");
                W8Ok(ref ok, art.soldado.Weapon.CurrentWeaponKind == WeaponKind.Smg && art.soldado.transform.position.y > 5.5f, sb, $"TV1 arma={art.soldado.Weapon.CurrentWeaponKind} y={art.soldado.transform.position.y:0.0}");
            }
            finally { if (sub != null) sub.Dispose(); AiBrain.IAPausada = iaAntes; foreach (var h in inv) if (h != null) h.Invulnerable = false; }
            Fin((ok ? "OK " : "FALLO ") + "francotirador y torreta: " + sb);
        }

        // ---------------------------------------------------------------- 078d torre destruible
        static IEnumerator Bug078d()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.2f)) yield return x;
            bool iaAntes = AiBrain.IAPausada; AiBrain.IAPausada = true;
            var inv = Wp7HacerInvulnerable();
            var yo = W8Yo();
            var tf1 = d.torres[0];
            try
            {
                if (tf1.tipo != TorreDestruible.Tipo.Francotirador) { Fin("FALLO d.torres[0] no es de francotirador"); yield break; }
                // El soldado dispara desde el callejon de las barracas, mirando a la torre.
                var baseT = tf1.transform.position;
                var puesto = new Vector3(baseT.x + 13f, 0f, baseT.z);
                if (!NavMesh.SamplePosition(puesto, out var hp, 1.5f, NavMesh.AllAreas)) { Fin("FALLO no hay suelo a 13 m de TF1"); yield break; }
                W8Teletransportar(hp.position, baseT);
                yield return null;
                yo.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, 0f, 0f));
                var w = yo.Weapon;
                var origen = yo.transform.position + Vector3.up * 0.4f;
                var objetivo = new Vector3(baseT.x + 1.6f, 1.5f, baseT.z);
                int vida0 = tf1.Vida;
                var s = tf1.ocupante;
                // 1) las balas no le bajan vida
                w.EquipWeapon(WeaponKind.Rifle, 26, 0.05f, Color.white); w.ConfigurarCargador(100, 0.5f);
                for (int i = 0; i < 40; i++)
                {
                    w.TryFire(origen, (objetivo - origen).normalized);
                    yield return new WaitForSeconds(0.06f);
                }
                foreach (var x in Esperar(0.6f)) yield return x;
                W8Ok(ref ok, tf1.Vida == vida0 && !tf1.EstaCaida && tf1.Marca.SoloExplosiones, sb, $"40 balas: vida {vida0} -> {tf1.Vida}");
                // 2) un cohete no alcanza (450 de vida), dos si
                w.EquipWeapon(WeaponKind.Rocket, 95, 0.3f, Color.white); w.ConfigurarCargador(10, 0.5f);
                yield return new WaitForSeconds(0.4f);
                bool tiro1 = w.TryFire(origen, (objetivo - origen).normalized);
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 2.5f && tf1.Vida == vida0) yield return null;
                int vida1 = tf1.Vida;
                W8Ok(ref ok, tiro1 && vida1 < vida0 && vida1 > 0 && !tf1.EstaCaida, sb, $"cohete 1: vida {vida0} -> {vida1} (dano {vida0 - vida1})");
                w.EquipWeapon(WeaponKind.Rocket, 95, 0.3f, Color.white);   // cargador lleno: el dano del cohete no depende de la recarga
                yield return new WaitForSeconds(0.4f);
                bool tiro2 = w.TryFire(origen, (objetivo - origen).normalized);
                t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f && !tf1.EstaCaida) yield return null;
                W8Ok(ref ok, tiro2 && tf1.EstaCaida, sb, $"cohete 2: disparo={tiro2} torre caida={tf1.EstaCaida} vida={tf1.Vida} (dano acumulado {vida0 - tf1.Vida}+)");
                W8Ok(ref ok, s != null && !s.Health.IsAlive, sb, "el francotirador muere con la torre");
                // 3) el vuelco: el pivote se inclina hasta ~80 grados en 1,2 s
                float inclinacion = 0f;
                t0 = Time.realtimeSinceStartup;
                bool pivoteActivo = tf1.pivote != null && tf1.pivote.gameObject.activeSelf;
                while (Time.realtimeSinceStartup - t0 < 2.4f && tf1.pivote != null && tf1.pivote.gameObject.activeSelf)
                {
                    inclinacion = Mathf.Max(inclinacion, Vector3.Angle(Vector3.up, tf1.pivote.up));
                    yield return null;
                }
                W8Ok(ref ok, inclinacion > 70f && inclinacion < 95f, sb, $"el pivote volco {inclinacion:0} grados (esperado ~80)");
                foreach (var x in Esperar(0.5f)) yield return x;
                W8Ok(ref ok, tf1.pivote == null || !tf1.pivote.gameObject.activeSelf, sb, "la torre se desarma al tocar el piso");
                W8Ok(ref ok, Fragmentador.Lanzados > 0, sb, $"fragmentos lanzados={Fragmentador.Lanzados}");
                // la malla ya no tiene la torre: la base es transitable en el collider
                W8Ok(ref ok, d.TorresCaidas(TorreDestruible.Tipo.Francotirador) == 1 && d.TorresCaidas(TorreDestruible.Tipo.Torreta) == 0, sb, $"director: torres caidas {d.TorresCaidas(TorreDestruible.Tipo.Francotirador)}/2");
                if (yo != null) { w.EquipFromLoadout(0); }
            }
            finally { AiBrain.IAPausada = iaAntes; foreach (var h in inv) if (h != null) h.Invulnerable = false; }
            Fin((ok ? "OK " : "FALLO ") + "torre destruible: " + sb);
        }

        // ---------------------------------------------------------------- 078e cadena del objetivo 1
        static IEnumerator Bug078e()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play (SC_Operacion)"); yield break; }
            var d = OperacionDirector.Instancia;
            if (d == null) { Fin("FALLO no hay OperacionDirector"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(1);
            foreach (var x in Esperar(1.5f)) yield return x;
            var hud = OperacionHud.Instancia;
            var inv = Wp7HacerInvulnerable();
            try
            {
                string sub = hud != null ? hud.TextoSubobjetivo : "";
                W8Ok(ref ok, sub.Contains("Torres 0/2") && sub.Contains("Torretas 0/2") && sub.Contains("Reflectores 0/4") && sub.Contains("Soldados restantes: 28"), sb, $"HUD subobjetivos '{sub}'");
                // el objetivo se cumple con TODOS los soldados muertos (guardias, tiradores y operadores)
                int muertos = 0;
                foreach (var s in d.enemigosCuartel) { if (s == null) continue; ComandosDeDepuracion.Matar(s.Id); muertos++; }
                var porton = d.portonCuartel;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 4f && d.Fase == FaseOperacion.Infiltrar) yield return null;
                W8Ok(ref ok, d.Fase == FaseOperacion.Puestos, sb, $"matados {muertos}/28 -> fase {d.Fase}");
                foreach (var x in Esperar(2.5f)) yield return x;
                W8Ok(ref ok, porton != null && !porton.activeSelf, sb, $"porton hundido={(porton != null && !porton.activeSelf)}");
                W8Ok(ref ok, hud == null || hud.TextoSubobjetivo == "" || !hud.TextoSubobjetivo.Contains("Torres"), sb, "la linea de subobjetivos del cuartel se limpia al cambiar de fase");
                W8Ok(ref ok, d.Fase == FaseOperacion.Puestos && hud != null && hud.TextoTitulo.StartsWith("OBJETIVO 2/6", StringComparison.Ordinal), sb, $"titulo '{(hud != null ? hud.TextoTitulo : "-")}'");

                // Recorrido real: con el porton hundido y el NavMesh rehecho, un agente cruza el cuartel de la brecha a la salida norte.
                foreach (var x in Esperar(2.0f)) yield return x;
                var a = d.entradas[0].position; var b = d.entradas[1].position;
                var camino = new NavMeshPath();
                NavMeshHit ha, hb;
                bool haySalida = NavMesh.SamplePosition(a, out ha, 3f, NavMesh.AllAreas) & NavMesh.SamplePosition(b, out hb, 3f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, camino) && camino.status == NavMeshPathStatus.PathComplete;
                W8Ok(ref ok, haySalida, sb, $"ruta brecha -> salida norte del cuartel: {(haySalida ? "completa" : "SIN RUTA")}");
                if (haySalida)
                {
                    var go = new GameObject("W8_Caminante");
                    var ag = go.AddComponent<NavMeshAgent>();
                    ag.radius = 0.4f; ag.height = 1.8f; ag.speed = 9f; ag.acceleration = 30f; ag.angularSpeed = 720f;
                    ag.Warp(ha.position);
                    ag.SetDestination(hb.position);
                    float tw = Time.realtimeSinceStartup; bool llego = false;
                    while (Time.realtimeSinceStartup - tw < 70f)
                    {
                        if (!ag.pathPending && ag.remainingDistance < 1.5f) { llego = true; break; }
                        yield return null;
                    }
                    W8Ok(ref ok, llego, sb, $"un agente real recorrio el cuartel en {Time.realtimeSinceStartup - tw:0.0} s (distancia {camino.corners.Length} tramos)");
                    UnityEngine.Object.Destroy(go);
                }
            }
            finally { foreach (var h in inv) if (h != null) h.Invulnerable = false; }
            Fin((ok ? "OK " : "FALLO ") + "cadena del cuartel: " + sb);
        }
    }
}
