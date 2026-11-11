using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;
using SP.Presentation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Revision Opus 5.5 (2026-11-11): auditoria de la destructibilidad automatica (#402), de los desplazamientos de 1 cm (#405) y mejoras de diseno del nivel.
    // Los checks de esta tanda son Bug71x (auditoria) y Bug72x (mejoras). Hay que matchear reMansion en ChecksBugs065.cs.
    public static partial class ChecksBugs065
    {
        static IEnumerable EsperarAjustes()
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f && (AjustesDeNivelNov08.PasadasPorLotes < 2 || DestructibilidadDelNivel.Convertidos == 0)) yield return null;
            foreach (var x in Esperar(1f)) yield return x;
        }

        // Todo GameObject al que apunta algun campo (publico o privado) del Director: son los objetos que el juego "conoce por referencia".
        static HashSet<GameObject> ReferenciasDelDirector(OperacionDirector d)
        {
            var set = new HashSet<GameObject>();
            void Anotar(object o)
            {
                if (o == null) return;
                if (o is GameObject g) { if (g != null) set.Add(g); }
                else if (o is Component c) { if (c != null) set.Add(c.gameObject); }
                else if (o is IEnumerable e && !(o is string)) foreach (var x in e) if (x is GameObject || x is Component) Anotar(x);
            }
            for (var t = d.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                { try { Anotar(f.GetValue(d)); } catch { } }
            return set;
        }

        // ---------------------------------------------------------------- 710a auditoria de lo que #402 convirtio en destructible
        // Vuelca cada objeto convertido (ruta, tamano, vida, referenciado por el Director, nombre sospechoso, NavMeshObstacle) a Assets/Validacion~/v10_710a_convertidos.txt.
        static IEnumerator Bug710a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in EsperarAjustes()) yield return x;
            var d = OperacionDirector.Instancia;
            var refs = ReferenciasDelDirector(d);
            string[] sospechosos = { "terminal", "baliza", "heli", "cobertura", "cover", "spawn", "ancla", "compuerta", "puerta", "servidor", "generador", "reflector", "torreta", "porton", "escudo", "radio", "mando", "antena", "pista", "helipuerto", "objetivo", "punto", "marca", "luz", "lampara", "cartel", "panel", "mesa_mando", "computadora" };
            var salida = new StringBuilder();
            int conRef = 0, conNombre = 0, conObs = 0, conRb = 0, total = 0;
            var porNombre = new SortedDictionary<string, int>();
            foreach (var go in DestructibilidadDelNivel.ListaDeConvertidos)
            {
                if (go == null) continue; total++;
                var m = go.GetComponent<ObstacleMarker>();
                bool referenciado = false;
                foreach (var r in refs) if (r != null && (r == go || r.transform.IsChildOf(go.transform) || go.transform.IsChildOf(r.transform) && go.transform.parent == r.transform)) { referenciado = true; break; }
                string n = go.name.ToLowerInvariant(); string cual = null;
                foreach (var s in sospechosos) if (n.Contains(s)) { cual = s; break; }
                bool obs = go.GetComponentInParent<UnityEngine.AI.NavMeshObstacle>() != null;
                bool rb = go.GetComponentInParent<Rigidbody>() != null;
                if (referenciado) conRef++; if (cual != null) conNombre++; if (obs) conObs++; if (rb) conRb++;
                porNombre.TryGetValue(go.name, out int k); porNombre[go.name] = k + 1;
                var b = go.GetComponent<Collider>().bounds;
                salida.Append(Inv($"{RutaCompleta(go.transform)} | tam {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0} | pos {b.center.x:0},{b.center.y:0.0},{b.center.z:0}"))
                      .Append(referenciado ? " | REF-DIRECTOR" : "").Append(cual != null ? " | NOMBRE~" + cual : "").Append(obs ? " | NAVMESHOBSTACLE" : "").Append(rb ? " | RIGIDBODY" : "").Append('\n');
            }
            salida.Append("\n--- por nombre ---\n"); foreach (var kv in porNombre) salida.Append(kv.Key).Append(" x").Append(kv.Value).Append('\n');
            System.IO.File.WriteAllText(RutaValidacion("v10_710a_convertidos.txt"), salida.ToString());
            W8Ok(ref ok, total > 0, sb, $"convertidos listados: {total}");
            W8Ok(ref ok, conRef == 0, sb, $"referenciados por el Director: {conRef}");
            W8Ok(ref ok, conNombre == 0, sb, $"con nombre de logica de juego: {conNombre}");
            sb.Append($"[con NavMeshObstacle {conObs}, con Rigidbody {conRb}] ");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 711a cuantos objetos con nombre de la tabla de #405 hay y donde
        static IEnumerator Bug711a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in EsperarAjustes()) yield return x;
            string[] nombres = { "Torre_E_Lado_Interior", "Torre_A_Lado_Interior", "Torre_D_Lado_Interior", "Torre_B_Lado_Interior", "Torre_C_Lado_Interior", "Torre_B_Divisoria", "Torre_C_Divisoria", "Torre_E_Divisoria", "Torre_D_Divisoria", "Torre_A_Divisoria", "Marco_S", "PB_Chimenea", "Cornisa_O_1", "Cornisa_E_1", "Estante" };
            var salida = new StringBuilder(); int fuera = 0, dentro = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                bool esDeLaTabla = System.Array.IndexOf(nombres, t.name) >= 0 || t.name.EndsWith("Puerta_D2_Relleno") || t.name.EndsWith("Lujo_Cuadro_A") || t.name.EndsWith("Lujo_Cuadro_B");
                if (!esDeLaTabla) continue;
                bool en = G.Dentro(t.position, 1.5f);
                if (en) dentro++; else fuera++;
                salida.Append(Inv($"{(en ? "DENTRO" : "FUERA ")} {RutaCompleta(t)} pos {t.position.x:0.00},{t.position.y:0.00},{t.position.z:0.00}\n"));
            }
            System.IO.File.WriteAllText(RutaValidacion("v10_711a_desplazados.txt"), salida.ToString());
            W8Ok(ref ok, dentro > 0, sb, $"dentro de la mansion: {dentro}");
            W8Ok(ref ok, fuera == 0, sb, $"fuera de la mansion (se hubieran movido sin razon): {fuera}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 712a censo del mobiliario de la mansion: cuantas piezas llevan ObstacleMarker (#402 "todo destructible")
        static readonly string[] MobiliarioDeLaMansion = { "PB_Sofa", "PB_Mostrador", "PB_Mampara_O", "PB_Mampara_E", "P1_Mesa", "P1_Cajon_1", "P1_Cajon_2", "P1_Aparador", "P2_Sofa", "P2_Piano", "P2_Barra", "Azotea_Equipo" };
        static IEnumerator Bug712a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in EsperarAjustes()) yield return x;
            int total = 0, conMarca = 0; var sin = new StringBuilder(); var porNombre = new Dictionary<string, int>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (System.Array.IndexOf(MobiliarioDeLaMansion, t.name) < 0 || !G.Dentro(t.position, 1f)) continue;
                total++;
                if (t.GetComponent<ObstacleMarker>() != null) conMarca++;
                else { porNombre.TryGetValue(t.name, out int k); porNombre[t.name] = k + 1; }
            }
            foreach (var kv in porNombre) sin.Append(kv.Key).Append(" x").Append(kv.Value).Append(' ');
            W8Ok(ref ok, total >= 20, sb, $"piezas de mobiliario encontradas en la mansion: {total}");
            W8Ok(ref ok, conMarca == total, sb, $"con ObstacleMarker (destructibles): {conMarca}/{total}; sin: {sin}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        // ---------------------------------------------------------------- 713a fotos de auditoria: un vistazo por piso y la azotea (sin asserts de diseno, deja las imagenes)
        static IEnumerator Bug713a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(5);
            foreach (var x in EsperarAjustes()) yield return x;
            float cx = (G.X0 + G.X1) * 0.5f;
            foreach (var x in FotoCamara("v10_713_pb.jpg", new Vector3(cx, 4.5f, 249f), new Vector3(cx, 1f, 270f), 70f, 960, 540)) yield return x;
            foreach (var x in FotoCamara("v10_713_p1.jpg", new Vector3(cx, 10.5f, 249f), new Vector3(cx, 7.5f, 270f), 70f, 960, 540)) yield return x;
            foreach (var x in FotoCamara("v10_713_p2.jpg", new Vector3(cx, 17.5f, 249f), new Vector3(cx, 14f, 270f), 70f, 960, 540)) yield return x;
            foreach (var x in FotoCamara("v10_713_azotea.jpg", new Vector3(cx, 24f, 247f), new Vector3(cx, 19.5f, 268f), 70f, 960, 540)) yield return x;
            foreach (var x in FotoCamara("v10_713_plaza.jpg", new Vector3(322f, 14f, 195f), new Vector3(322f, 3f, 240f), 60f, 960, 540)) yield return x;
            Fin("OK fotos de auditoria (v10_713_*.jpg)");
        }
        // ---------------------------------------------------------------- 714a la ruta del tanque esta despejada: una caja del tamano del tanque recorrida a lo largo de la ruta no choca con nada solido
        static IEnumerator Bug714a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(4);
            foreach (var x in EsperarAjustes()) yield return x;
            var d = OperacionDirector.Instancia;
            var bloqueos = new Dictionary<string, int>(); var donde = new Dictionary<string, Vector3>();
            float largo = 0f; int muestras = 0;
            for (int i = 0; i + 1 < d.rutaDelTanque.Length; i++) { largo += Vector3.Distance(d.rutaDelTanque[i].position, d.rutaDelTanque[i + 1].position); }
            muestras = Mathf.RoundToInt(largo / 3f);
            foreach (var c in AjustesDeNivelOpus.BloqueosDeLaRuta(d)) { string clave = c.name; bloqueos.TryGetValue(clave, out int k); bloqueos[clave] = k + 1; if (!donde.ContainsKey(clave)) donde[clave] = c.bounds.center; }
            var det = new StringBuilder(); foreach (var kv in bloqueos) det.Append(Inv($"{kv.Key} x{kv.Value} en {donde[kv.Key].x:0},{donde[kv.Key].z:0}; "));
            W8Ok(ref ok, muestras > 100, sb, $"ruta de {largo:0} m (apagados por el parche: {AjustesDeNivelOpus.ObjetosDeRutaApagados})");
            W8Ok(ref ok, bloqueos.Count == 0, sb, $"objetos solidos sobre la ruta del tanque: {det}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
        // ---------------------------------------------------------------- 715a galeria del 1.er piso con cobertura: 6 bancos destructibles, carril libre, puertas y apariciones libres, camino entre torres sigue corto
        static IEnumerator Bug715a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in EsperarAjustes()) yield return x;
            foreach (var x in Esperar(2.5f)) yield return x;   // el parche de la galeria corre cada 2 s
            var bancos = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t.name == AjustesDeNivelOpus.NombreBanco) bancos.Add(t.gameObject);
            W8Ok(ref ok, bancos.Count == AjustesDeNivelOpus.BancosDeGaleria.Length, sb, $"bancos en la galeria: {bancos.Count}/{AjustesDeNivelOpus.BancosDeGaleria.Length}");
            int destructibles = 0, conCollider = 0; float yPiso = G.Nivel(1);
            bool carrilLibre = true, enElPiso = true; float minSpawn = 99f;
            foreach (var b in bancos)
            {
                var m = b.GetComponent<ObstacleMarker>(); if (m != null) destructibles++;
                var col = b.GetComponent<Collider>(); if (col != null) conCollider++;
                if (Mathf.Abs(b.transform.position.y - (yPiso + 0.5f)) > 0.05f) enElPiso = false;
                if (col != null) { var bb = col.bounds; if (bb.max.z > 265.4f && bb.min.z < 266.6f) carrilLibre = false; }
                foreach (var sp in G.EnemigosPrimero) { var q = b.GetComponent<Collider>().ClosestPoint(new Vector3(sp.x, b.transform.position.y, sp.z)); minSpawn = Mathf.Min(minSpawn, Vector2.Distance(new Vector2(q.x, q.z), new Vector2(sp.x, sp.z))); }
            }
            W8Ok(ref ok, destructibles == bancos.Count && conCollider == bancos.Count && bancos.Count > 0, sb, $"todos destructibles y solidos ({destructibles}/{conCollider})");
            W8Ok(ref ok, enElPiso, sb, "apoyados sobre el piso del 1.er piso");
            W8Ok(ref ok, carrilLibre, sb, "la franja central (z 265,4-266,6, la alfombra) queda libre");
            W8Ok(ref ok, minSpawn >= 1.5f, sb, $"distancia minima a un punto de aparicion de la guardia: {minSpawn:0.0} m");
            // las salidas/entradas de las torres de este piso quedan libres (una primera version del banco norte tapaba la salida de la torre A: lo cazo Bug409u)
            float minTorres = 99f;
            foreach (var b in bancos) { var col = b.GetComponent<Collider>(); foreach (var pt in new[] { G.SalidaDeTorre(0), G.EntradaDeTorre(1) }) { var q = col.ClosestPoint(new Vector3(pt.x, b.transform.position.y, pt.z)); minTorres = Mathf.Min(minTorres, Vector2.Distance(new Vector2(q.x, q.z), new Vector2(pt.x, pt.z))); } }
            W8Ok(ref ok, minTorres >= 3f, sb, $"distancia minima de un banco a la salida de la torre A o la entrada de la B: {minTorres:0.0} m");
            // el camino a pie entre la salida de la torre A y la puerta de la torre B (en este piso) sigue siendo casi recto
            var a = G.SalidaDeTorre(0); var bpt = G.EntradaDeTorre(1);
            bool ma = UnityEngine.AI.NavMesh.SamplePosition(a, out var ha, 0.4f, UnityEngine.AI.NavMesh.AllAreas);
            bool mb = UnityEngine.AI.NavMesh.SamplePosition(bpt, out var hb, 0.4f, UnityEngine.AI.NavMesh.AllAreas);
            var camino = new UnityEngine.AI.NavMeshPath(); bool hay = ma && mb && UnityEngine.AI.NavMesh.CalculatePath(ha.position, hb.position, UnityEngine.AI.NavMesh.AllAreas, camino);
            float largo = 0f; if (hay) for (int i = 0; i + 1 < camino.corners.Length; i++) largo += Vector3.Distance(camino.corners[i], camino.corners[i + 1]);
            float recta = hay ? Vector3.Distance(ha.position, hb.position) : 0f;
            W8Ok(ref ok, hay && camino.status == UnityEngine.AI.NavMeshPathStatus.PathComplete && largo < recta * 1.35f, sb, Inv($"camino torre A -> torre B en el 1.er piso: {largo:0.0} m para {recta:0.0} m en linea recta ({(hay ? camino.status.ToString() : "sin camino")})"));
            foreach (var x in FotoCamara("v10_715_galeria.jpg", new Vector3(299f, yPiso + 4f, 266f), new Vector3(330f, yPiso + 0.5f, 266f), 75f, 960, 540)) yield return x;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
