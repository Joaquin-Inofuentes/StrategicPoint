using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Core;
using SP.Operacion;
using SP.Presentation;
using G = SP.Operacion.MansionGeometria;

namespace SP.EditorTools
{
    // Ronda 20, punto 4 de Docs/COLA_PENDIENTES.md ("emprolijar totalmente la zona final": la azotea de la mansion, objetivo 6). Ambos con el
    // Editor en Play y la mansion PRENDIDA (la regex de PrepararModoDelMando la apaga para todo lo que no es de la azotea: aca se vuelve a prender,
    // porque sin mansion el heli se posa en la plaza y la zona final real es el techo).
    //   Bug998a: lo roto limpio (remate flotante, cadenas que atraviesan la losa, apliques/cuadros en el aire, bases de reflector), cobertura visual de
    //            la azotea por celdas (ANTES = kit apagado / DESPUES), cero piezas R20 sin apoyo (sin contar destructibles), cero colliders R20 en la
    //            zona reservada (heli, pasillos salida->heli, huellas de las torres, llegada) o metidos en otro collider, NavMesh de cada llegada
    //            al heli (contra la linea base sin el kit), heli nivelado, presupuesto y reaplicar sin duplicar.
    //   Bug998b: inventario de la azotea (Temp/r20_inventario_antes|despues.txt) y tomas Assets/Validacion~/r20_zonafinal_<angulo>_antes|despues.png
    //            (mas r20_zonafinal_antes.png / _despues.png = vista general).
    // Correr:  return SP.EditorTools.ChecksBugs065.CorrerLista("Bug998a,Bug998b", true);
    public static partial class ChecksBugs065
    {
        static readonly Bounds R20dAzotea = new Bounds(new Vector3((G.X0 + G.X1) * 0.5f, G.Nivel(3) + 6f, (G.Z0 + G.Z1) * 0.5f), new Vector3(G.X1 - G.X0 + 1f, 13f, G.Z1 - G.Z0 + 1f));
        const float R20dCelda = 4f, R20dCoberturaMin = 0.6f, R20dGananciaMin = 0.2f;

        static IEnumerable R20dArrancar(StringBuilder sb)
        {
            OperacionDirector.ApagarMansion = false;   // la zona final real es la azotea
            OperacionPrueba.Arrancar(6);
            foreach (var x in Esperar(3f)) yield return x;
            AjustesDeNivelR20.Aplicar();
            R20dQuietos();
            foreach (var x in Esperar(1f)) yield return x;   // carving de lo nuevo
            Physics.SyncTransforms();
            var d = OperacionDirector.Instancia;
            if (d != null && d.heli != null) sb.Append($"heli en ({d.heli.position.x:0.0},{d.heli.position.y:0.0},{d.heli.position.z:0.0}); ");
        }

        // Sin enemigos (las tandas de la extraccion le tiran a las coberturas) y con todos quietos: se mide el escenario.
        static void R20dQuietos()
        {
            R19zCongelarTodos(W9Yo());
            foreach (var s in ActorRegistry.All) if (s != null && s.Team == SP.Combat.TeamId.Enemy && s.gameObject.activeSelf) s.gameObject.SetActive(false);
        }

        static string R20dRuta(Transform t)
        {
            var s = t.name; int n = 0;
            for (var p = t.parent; p != null && n < 4; p = p.parent, n++) s = p.name + "/" + s;
            return s;
        }

        static bool R20dEsNuestro(Transform t) => AjustesDeNivelR20.GrupoActual != null && t.IsChildOf(AjustesDeNivelR20.GrupoActual);

        static bool R20dSobreHueco(Vector3 p)
        {
            foreach (int i in AjustesDeNivelR20.TorresDeLaAzotea) if (AjustesDeNivelR20.HuellaDeTorre(i).Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        // Collider que sostiene (no trigger, no soldado/vehiculo/proyectil, no destructible, no el propio).
        static bool R20dSostiene(Collider c, Transform propio)
        {
            if (c == null || !c.enabled || c.isTrigger || c.attachedRigidbody != null) return false;
            if (propio != null && (c.transform == propio || c.transform.IsChildOf(propio))) return false;
            if (c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) return false;
            var om = c.GetComponentInParent<ObstacleMarker>();
            return om == null || !om.EsDestruible;
        }

        // ================================================================ 998a
        static IEnumerator Bug998a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            foreach (var x in R20dArrancar(sb)) yield return x;
            var d = OperacionDirector.Instancia;
            var g = AjustesDeNivelR20.GrupoActual;
            if (d == null || d.heli == null || g == null) { Fin("FALLO " + sb + (g == null ? "el grupo R20 no se armo (no se encontro la losa de la azotea)" : "sin director/heli")); yield break; }
            float Y = AjustesDeNivelR20.Y; var c = G.CentroDeLaAzotea;
            bool enLaAzotea = Mathf.Abs(d.heli.position.y - Y) < 0.5f && G.Dentro(d.heli.position);
            W8Ok(ref ok, enLaAzotea, sb, $"heli posado en la azotea ({d.heli.position.x:0.0},{d.heli.position.y:0.0},{d.heli.position.z:0.0})");
            W8Ok(ref ok, d.heli.up.y > 0.9995f, sb, $"heli nivelado (inclinacion {Vector3.Angle(d.heli.up, Vector3.up):0.0} grados)");

            var rend = new List<Renderer>();
            foreach (var r in g.GetComponentsInChildren<Renderer>(false)) if (r != null && r.enabled) rend.Add(r);
            var todos = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);

            // ---- 1. lo roto
            int remates = 0, cadenas = 0, enElAire = 0, basesSueltas = 0; var quien = new List<string>();
            foreach (var r in todos)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; string n = r.name;
                if (!R20dAzotea.Intersects(b) && !(G.Dentro(b.center, 1f) && n.Contains("Cadena"))) continue;
                if (n == "Mastil_Gala_Remate") { remates++; quien.Add(n); }
                else if (n == "Cadena" || n == "R20_Cadena")
                {
                    if (Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.02f, b.center.z), Vector3.up, out var h, 2f, ~0, QueryTriggerInteraction.Ignore) && h.point.y < b.max.y - 0.02f)
                    { cadenas++; if (quien.Count < 8) quien.Add($"{n}@{b.center.x:0},{b.center.y:0.0},{b.center.z:0}"); }
                }
                else if ((n.StartsWith("Aplique_") || n.Contains("_Lujo_")) && b.max.y > Y + 0.15f && R20dSobreHueco(b.center)) { enElAire++; if (quien.Count < 8) quien.Add($"{n}@{b.center.x:0},{b.center.y:0.0},{b.center.z:0}"); }
                else if (n.StartsWith("Reflector_Techo") && n.EndsWith("_Base"))
                {
                    var abajo = Physics.OverlapBox(new Vector3(b.center.x, b.min.y - 0.06f, b.center.z), new Vector3(b.extents.x * 0.5f, 0.08f, b.extents.z * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    bool apoyada = false; foreach (var col in abajo) if (R20dSostiene(col, r.transform)) apoyada = true;
                    // el pedestal R20 es arte sin collider: se acepta si hay un R20_Pedestal debajo tocando el piso
                    if (!apoyada) foreach (var p in rend) if (p.name == "R20_Pedestal_Reflector" && p.bounds.max.y >= b.min.y - 0.05f && p.bounds.Contains(new Vector3(b.center.x, p.bounds.center.y, b.center.z)) && p.bounds.min.y <= Y + 0.03f) apoyada = true;
                    if (!apoyada) { basesSueltas++; quien.Add(n); }
                }
            }
            W8Ok(ref ok, remates == 0 && cadenas == 0 && enElAire == 0 && basesSueltas == 0, sb,
                $"lo roto: remate flotante {remates}, cadenas que atraviesan la losa {cadenas}, apliques/cuadros en el aire {enElAire}, bases de reflector sin apoyo {basesSueltas} (arreglos: remate {AjustesDeNivelR20.RemateApagado}, cadenas {AjustesDeNivelR20.CadenasRecortadas}, flotantes {AjustesDeNivelR20.FlotantesApagados}, pedestales {AjustesDeNivelR20.Pedestales})" + (quien.Count > 0 ? ": " + string.Join(", ", quien) : ""));

            // ---- 2. piezas R20 sin apoyo (sin contar destructibles; las hijas de una cobertura se van con ella)
            var sueltas = new List<Renderer>(); var sostenidas = new HashSet<Renderer>();
            foreach (var r in rend)
            {
                if (r.GetComponentInParent<ObstacleMarker>() != null) { sostenidas.Add(r); continue; }
                var b = r.bounds; b.Expand(0.12f);
                foreach (var col in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (R20dSostiene(col, r.transform)) { sostenidas.Add(r); break; }
                if (!sostenidas.Contains(r)) sueltas.Add(r);
            }
            for (bool cambio = true; cambio;)   // lo que toca algo sostenido (la lente al cabezal, la manga al aro) tambien queda sostenido
            {
                cambio = false;
                for (int i = sueltas.Count - 1; i >= 0; i--)
                {
                    var b = sueltas[i].bounds; b.Expand(0.06f);
                    foreach (var s in sostenidas) if (s.bounds.Intersects(b)) { sostenidas.Add(sueltas[i]); sueltas.RemoveAt(i); cambio = true; break; }
                }
            }
            var ls = new List<string>(); foreach (var r in sueltas) if (ls.Count < 8) ls.Add($"{r.name}@{r.bounds.center.x:0.0},{r.bounds.min.y:0.00},{r.bounds.center.z:0.0}");
            W8Ok(ref ok, sueltas.Count == 0, sb, $"{sueltas.Count} piezas R20 flotantes (de {rend.Count})" + (ls.Count > 0 ? ": " + string.Join(", ", ls) : ""));

            // ---- 3. colliders R20: fuera de la zona reservada y sin meterse en otro collider; nada alto (> 0,12 m) dentro del circulo del heli
            int reservados = 0, solapados = 0, altosEnElHeli = 0; var lc = new List<string>();
            var cols = g.GetComponentsInChildren<Collider>(false);
            foreach (var col in cols)
            {
                if (!col.enabled || col.isTrigger) continue;
                var b = col.bounds;
                if (AjustesDeNivelR20.EnZonaReservada(b)) { reservados++; if (lc.Count < 6) lc.Add(col.name + "(reservada)"); }
                var mitad = new Vector3(Mathf.Max(0.03f, b.extents.x - 0.05f), Mathf.Max(0.03f, b.extents.y - 0.08f), Mathf.Max(0.03f, b.extents.z - 0.05f));
                foreach (var o in Physics.OverlapBox(b.center + Vector3.up * 0.05f, mitad, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (o == null || o == col || o.isTrigger || R20dEsNuestro(o.transform) && o.transform.IsChildOf(col.transform)) continue;
                    if (o.GetComponentInParent<Soldier>() != null || o.GetComponentInParent<SP.Vehicles.Vehicle>() != null || o.attachedRigidbody != null) continue;
                    solapados++; if (lc.Count < 6) lc.Add($"{col.name}x{o.name}");
                }
            }
            foreach (var r in rend)
            {
                var b = r.bounds;
                float dx = Mathf.Max(b.min.x - c.x, 0f, c.x - b.max.x), dz = Mathf.Max(b.min.z - c.z, 0f, c.z - b.max.z);
                if (Mathf.Sqrt(dx * dx + dz * dz) < AjustesDeNivelR20.RadioReservadoHeli && b.max.y > Y + 0.12f) { altosEnElHeli++; if (lc.Count < 6) lc.Add(r.name + "(heli)"); }
            }
            W8Ok(ref ok, reservados == 0 && solapados == 0 && altosEnElHeli == 0, sb, $"colliders R20 ({cols.Length}): {reservados} en la zona reservada, {solapados} solapes con otros colliders; {altosEnElHeli} piezas altas en el circulo del heli" + (lc.Count > 0 ? ": " + string.Join(", ", lc) : ""));

            // ---- 4. cobertura visual de la azotea (celdas de 4 m con losa, fuera del helipuerto y de los huecos de las torres)
            float antes, despues; int vAntes, vDespues, contadas; string vacias;
            AjustesDeNivelR20.Mostrar(false); Physics.SyncTransforms();
            antes = R20dCobertura(out vAntes, out contadas, out _);
            AjustesDeNivelR20.Mostrar(true); Physics.SyncTransforms();
            despues = R20dCobertura(out vDespues, out contadas, out vacias);
            W8Ok(ref ok, despues >= R20dCoberturaMin && despues - antes >= R20dGananciaMin, sb, $"cobertura visual de la azotea: {vDespues}/{contadas} = {despues:P0} (antes {vAntes}/{contadas} = {antes:P0})" + (vacias.Length > 0 ? $" vacias [{vacias}]" : ""));

            // ---- 5. NavMesh: de cada llegada al heli (puerta y centro), con el kit y contra la linea base sin el kit
            var origenes = new List<(string, Vector3)>();
            foreach (int i in AjustesDeNivelR20.TorresDeLaAzotea) { origenes.Add(("salida " + G.Torres[i].nombre, G.SalidaDeTorre(i))); origenes.Add(("2.o piso " + G.Torres[i].nombre, G.EntradaDeTorre(i))); }
            origenes.Add(("llegada", AjustesDeNivelR20.Llegada));
            var puerta = d.heli.position + d.heli.right * 1.6f;
            var largosCon = new float[origenes.Count]; var largosSin = new float[origenes.Count];
            for (int i = 0; i < origenes.Count; i++) largosCon[i] = R20dLargo(origenes[i].Item2, puerta);
            AjustesDeNivelR20.Mostrar(false);
            foreach (var x in Esperar(1.0f)) yield return x;   // el carving se retira
            for (int i = 0; i < origenes.Count; i++) largosSin[i] = R20dLargo(origenes[i].Item2, puerta);
            AjustesDeNivelR20.Mostrar(true);
            foreach (var x in Esperar(1.0f)) yield return x;
            int cortados = 0, alargados = 0; var ln = new List<string>();
            for (int i = 0; i < origenes.Count; i++)
            {
                bool corta = largosCon[i] < 0f && largosSin[i] >= 0f;
                bool larga = largosCon[i] >= 0f && largosSin[i] >= 0f && largosCon[i] > largosSin[i] * 1.1f + 1f;
                if (corta) cortados++; if (larga) alargados++;
                ln.Add($"{origenes[i].Item1} {(largosCon[i] < 0 ? "NO" : largosCon[i].ToString("0.0"))}/{(largosSin[i] < 0 ? "NO" : largosSin[i].ToString("0.0"))}");
            }
            int sinCamino = 0; foreach (var l in largosCon) if (l < 0f) sinCamino++;
            W8Ok(ref ok, cortados == 0 && alargados == 0 && sinCamino == 0, sb, $"NavMesh hasta la puerta del heli (con kit/sin kit, m): {cortados} cortados, {alargados} alargados > 10 %, {sinCamino} sin camino [{string.Join("; ", ln)}]");

            // ---- 6. presupuesto, nombres, luces
            var mats = new HashSet<string>(); int malos = 0;
            foreach (var r in rend) { foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("R20_")) mats.Add(m.name); if (!r.name.StartsWith("R20_")) malos++; }
            int luces = 0, conSombra = 0; foreach (var l in g.GetComponentsInChildren<Light>(false)) { luces++; if (l.shadows != LightShadows.None) conSombra++; }
            W8Ok(ref ok, AjustesDeNivelR20.ObjetosCreados <= 220 && mats.Count <= 8 && malos == 0, sb, $"presupuesto: {AjustesDeNivelR20.ObjetosCreados} objetos (<= 220), {rend.Count} renderers, {mats.Count} materiales R20 (<= 8), {malos} nombres sin prefijo R20_, coberturas {AjustesDeNivelR20.CoberturasCreadas}, solidos {AjustesDeNivelR20.SolidosCreados}, omitidas {AjustesDeNivelR20.PiezasOmitidas}");
            W8Ok(ref ok, luces <= 2 && conSombra == 0, sb, $"{luces} luces reales R20 (<= 2), {conSombra} con sombra");

            // ---- 7. reaplicar no duplica
            int obj = AjustesDeNivelR20.ObjetosCreados, cons = AjustesDeNivelR20.Construcciones;
            AjustesDeNivelR17.Aplicar(d); AjustesDeNivelR20.Aplicar(); AjustesDeNivelR20.Aplicar();
            int grupos = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) foreach (var raiz in SceneManager.GetSceneAt(i).GetRootGameObjects()) if (raiz.name == AjustesDeNivelR20.GrupoRaiz) grupos++;
            W8Ok(ref ok, grupos == 1 && AjustesDeNivelR20.Construcciones == cons && AjustesDeNivelR20.ObjetosCreados == obj, sb, $"reaplicar: {grupos} grupo(s) {AjustesDeNivelR20.GrupoRaiz}, construcciones {cons}->{AjustesDeNivelR20.Construcciones}, objetos {obj}->{AjustesDeNivelR20.ObjetosCreados}");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        static float R20dLargo(Vector3 desde, Vector3 hasta)
        {
            if (!NavMesh.SamplePosition(desde, out var a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(hasta, out var b, 3f, NavMesh.AllAreas)) return -1f;
            var p = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, p) || p.status != NavMeshPathStatus.PathComplete) return -1f;
            float l = 0f; for (int k = 1; k < p.corners.Length; k++) l += (p.corners[k] - p.corners[k - 1]).magnitude;
            return l;
        }

        // Fraccion de celdas de 4 x 4 m con algo (arte o equipo, sin contar la losa, las ondas del rotor, soldados ni vehiculos) entre el piso y 4 m.
        static float R20dCobertura(out int vestidas, out int contadas, out string vaciasTxt)
        {
            vestidas = 0; contadas = 0; var vacias = new List<string>();
            float Y = AjustesDeNivelR20.Y; var c = G.CentroDeLaAzotea;
            var cajas = new List<Bounds>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || EsRuido(r)) continue;
                var b = r.bounds;
                if (!R20dAzotea.Intersects(b) || b.max.y < Y - 0.01f || r.name.StartsWith("Losa_") || r.name == "Onda" || Mathf.Max(b.size.x, b.size.z) > 30f) continue;
                cajas.Add(b);
            }
            for (float x = G.X0 + G.Muro; x < G.X1 - G.Muro - 1f; x += R20dCelda)
                for (float z = G.Z0 + G.Muro; z < G.Z1 - G.Muro - 1f; z += R20dCelda)
                {
                    float cx = x + R20dCelda * 0.5f, cz = z + R20dCelda * 0.5f;
                    if (new Vector2(cx - c.x, cz - c.z).magnitude < 6.5f || R20dSobreHueco(new Vector3(cx, 0f, cz)) || !AjustesDeNivelR20.PisoEn(cx, cz, out _)) continue;
                    contadas++;
                    var celda = new Bounds(new Vector3(cx, Y + 2f, cz), new Vector3(R20dCelda, 4.02f, R20dCelda));
                    bool v = false; foreach (var b in cajas) if (b.Intersects(celda)) { v = true; break; }
                    if (v) vestidas++; else if (vacias.Count < 12) vacias.Add($"{cx:0},{cz:0}");
                }
            vaciasTxt = string.Join(" ", vacias);
            return contadas > 0 ? (float)vestidas / contadas : 0f;
        }

        static void R20dInventario(string archivo)
        {
            var sb = new StringBuilder();
            var d = OperacionDirector.Instancia;
            Bounds heliB = new Bounds();
            if (d != null && d.heli != null)
            {
                heliB = new Bounds(d.heli.position, Vector3.one);
                foreach (var r in d.heli.GetComponentsInChildren<Renderer>(false)) if ((r.bounds.center - d.heli.position).magnitude < 12f) heliB.Encapsulate(r.bounds);
                sb.AppendLine($"HELI pos={d.heli.position} bounds={heliB.min}..{heliB.max} forward={d.heli.forward} up={d.heli.up}");
            }
            sb.AppendLine("== RENDERERS en la azotea (FLOTA = sin collider debajo; ENTERRADO = bajo la cara de la losa)");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || !R20dAzotea.Contains(r.bounds.center) || EsRuido(r)) continue;
                var b = r.bounds; string marca = "";
                if (b.min.y > G.Nivel(3) + 0.1f && !Apoyado(r)) marca += " FLOTA";
                if (b.max.y < G.Nivel(3) + 0.02f && b.min.y > G.Nivel(3) - 0.6f) marca += " ENTERRADO";
                if (d != null && d.heli != null && b.Intersects(heliB) && !r.transform.IsChildOf(d.heli) && b.max.y > G.Nivel(3) + 0.12f) marca += " TOCA_HELI";
                sb.AppendLine($"{R20dRuta(r.transform)} c=({b.center.x:0.0},{b.center.y:0.00},{b.center.z:0.0}) t=({b.size.x:0.00},{b.size.y:0.00},{b.size.z:0.00}){(r.GetComponent<Collider>() != null ? " COL" : "")}{marca}");
            }
            sb.AppendLine("== LUCES a < 30 m del centro de la azotea");
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l != null && l.enabled && (l.transform.position - G.CentroDeLaAzotea).magnitude < 30f) sb.AppendLine($"{R20dRuta(l.transform)} {l.type} pos={l.transform.position} rango={l.range} int={l.intensity}");
            sb.AppendLine("== NAVMESH de la azotea (# = piso navegable cada 4 m)");
            for (float x = G.X0 + 2f; x < G.X1; x += 4f)
            {
                var fila = new StringBuilder();
                for (float z = G.Z1 - 2f; z > G.Z0; z -= 4f)
                    fila.Append(NavMesh.SamplePosition(new Vector3(x, G.Nivel(3), z), out var h, 0.6f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - G.Nivel(3)) < 0.5f ? '#' : '.');
                sb.AppendLine($"x={x:0} {fila}");
            }
            File.WriteAllText(archivo, sb.ToString());
        }

        static void R20dFotos(string etiqueta, Vector3 heli)
        {
            var c = G.CentroDeLaAzotea; float y = G.Nivel(3);
            R20dFoto(etiqueta, "general", new Vector3(G.X0 - 6f, y + 14f, G.Z0 - 8f), c, 60f);
            R20dFoto(etiqueta, "helipuerto", new Vector3(c.x - 4f, y + 4.5f, G.Z0 + 3f), heli + Vector3.up * 0.5f, 60f);
            R20dFoto(etiqueta, "llegada_D", G.SalidaDeTorre(3) + Vector3.up * 1.7f, heli + Vector3.up * 1f, 60f);
            R20dFoto(etiqueta, "llegada_C", G.SalidaDeTorre(2) + Vector3.up * 1.7f, heli + Vector3.up * 1f, 60f);
            R20dFoto(etiqueta, "norte", new Vector3(c.x + 6f, y + 3f, G.Z1 - 2f), new Vector3(c.x, y + 1f, G.Z0 + 4f), 65f);
            R20dFoto(etiqueta, "oeste", new Vector3(G.X0 + 2f, y + 2.5f, c.z - 6f), new Vector3(G.X1, y + 1f, c.z + 4f), 65f);
            R20dFoto(etiqueta, "este", new Vector3(G.X1 - 2f, y + 2.5f, G.Z0 + 3f), new Vector3(G.X0 + 6f, y + 0.5f, G.Z1 - 6f), 65f);
            R20dFoto(etiqueta, "cenital", new Vector3(c.x, y + 45f, c.z - 18f), c, 55f);
            R20dFoto(etiqueta, "ruta_2piso", new Vector3(327f, G.Nivel(2) + 3.2f, 249.5f), new Vector3(322f, G.Nivel(2) + 4.5f, 262f), 70f);   // las aranas del 2.o piso (cadenas)
        }

        static void R20dFoto(string etiqueta, string toma, Vector3 pos, Vector3 mira, float fov)
        {
            var rutas = new List<string> { RutaValidacion($"r20_zonafinal_{toma}_{etiqueta}.png") };
            if (toma == "general") rutas.Add(RutaValidacion($"r20_zonafinal_{etiqueta}.png"));
            GameObject go = null; RenderTexture rt = null; Texture2D tex = null; var prev = RenderTexture.active;
            try
            {
                go = new GameObject("R20dCamara"); var cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov; cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.05f; cam.farClipPlane = 800f;
                var principal = CamaraPrincipal.Actual; cam.cullingMask = principal != null ? principal.cullingMask : ~(1 << 8);
                go.transform.position = pos; go.transform.LookAt(mira);
                rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                var png = tex.EncodeToPNG();
                foreach (var ruta in rutas) { Directory.CreateDirectory(Path.GetDirectoryName(ruta)); File.WriteAllBytes(ruta, png); }
                cam.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = prev;
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        // ================================================================ 998b
        static IEnumerator Bug998b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder();
            foreach (var x in R20dArrancar(sb)) yield return x;
            var d = OperacionDirector.Instancia;
            var heli = d != null && d.heli != null ? d.heli.position : G.CentroDeLaAzotea;
            bool hay = AjustesDeNivelR20.GrupoActual != null;
            AjustesDeNivelR20.Mostrar(false);
            R20dQuietos();
            foreach (var x in Esperar(0.4f)) yield return x;
            R20dInventario("Temp/r20_inventario_antes.txt");
            R20dFotos("antes", heli);
            if (hay)
            {
                AjustesDeNivelR20.Mostrar(true);
                R20dQuietos();
                foreach (var x in Esperar(0.4f)) yield return x;
                R20dInventario("Temp/r20_inventario_despues.txt");
                R20dFotos("despues", heli);
            }
            sb.Append(hay ? "9 tomas antes y despues (Assets/Validacion~/r20_zonafinal_*), inventarios en Temp/r20_inventario_*.txt" : "solo tomas antes (sin grupo R20)");
            Fin("OK " + sb);
        }
    }
}
