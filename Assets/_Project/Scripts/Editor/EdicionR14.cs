using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Ronda 14 (reportes del 17/11): ediciones de escena que no se pueden hacer a mano. Todo trabaja sobre las 4 escenas abiertas (EscenasR13.Asegurar).
    // Cada metodo devuelve un informe y, con aplicar=true, cambia las escenas (queda sin guardar: lo guarda EscenasR13.Guardar al final de la tanda).
    public static class EdicionR14
    {
        // ---------------------------------------------------------------- quitar objetos por ruta
        // Apaga (SetActive(false)) cada transform cuyo nombre completo (padre/hijo) cumple el filtro. Los apagados quedan como en rondas anteriores (checks los cuentan).
        public static string Apagar(string nombre, System.Func<Transform, bool> filtro, bool aplicar)
        {
            var sb = new StringBuilder(); int n = 0;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.gameObject.activeSelf || !filtro(t)) continue;
                n++;
                if (aplicar) { t.gameObject.SetActive(false); EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); }
            }
            sb.AppendLine($"{nombre}: {n} objetos {(aplicar ? "apagados" : "encontrados")}");
            return sb.ToString();
        }

        static bool Cerca(Transform t, float x, float z, float r) { var p = t.position; return (new Vector2(p.x, p.z) - new Vector2(x, z)).magnitude <= r; }
        static string Padre(Transform t) => t.parent != null ? t.parent.name : "";

        // Tanda 1 de la ronda 14: quitar lo que el autor pidio sacar (#14 #15 #17 #19 #27 #28 #29 #31). aplicar=false solo cuenta.
        public static string AplicarTanda1(bool aplicar)
        {
            var sb = new StringBuilder();
            sb.Append(Apagar("casitas (#27 #29)", t => Padre(t) == "Terreno_Ambiente" && t.name.StartsWith("Casita"), aplicar));
            sb.Append(Apagar("columna (#17)", t => Padre(t) == "Interior" && t.name.StartsWith("P2_Columna") && Cerca(t, 320f, 266f, 1.5f), aplicar));
            sb.Append(Apagar("cabinas de calle (#14)", t => Padre(t) == "P_Op_Pres_Calles" && t.name.StartsWith("Cabina"), aplicar));
            sb.Append(Apagar("decoracion de entrada (#15)", t => Padre(t) == "P_Op_Pres_Mansion" && (t.name.StartsWith("Alfombra_Gala") || t.name.StartsWith("Poste_Cordon") || t.name == "Cordon" || t.name.StartsWith("Arco_Flor")), aplicar));
            sb.Append(Apagar("luces hada / esferas que levitan (#31)", t => t.name == "Luz_Hada", aplicar));
            sb.Append(Apagar("piezas flotantes del edificio (#28)", t => Padre(t) == "Edificio_2" && (t.name.StartsWith("Equipo_Aire") || t.name.StartsWith("Antena")) || (Padre(t) == "Estructura" && t.name.StartsWith("Hormigon_x-160_z160")), aplicar));
            sb.Append(QuitarFlechasDelPiso(aplicar, -400f, 400f));
            sb.Append(Apagar("tramo norte de la cruz -> T (#1)", t => Padre(t) == "R13_T" && (t.name == "R13_Calle_Acceso_Patio" || t.name.StartsWith("R13_Vereda_Acceso") || t.name.StartsWith("R13_Cordon_Acceso") || t.name.StartsWith("R13_Isleta") || t.name.StartsWith("P_Env_Arbusto")) && t.position.z > -13f, aplicar));
            return sb.ToString();
        }

        // #20 "que se vea bosque, no bloqueo de muros": el muro del Valle Sur se oculta (su collider sigue como limite) y delante crece una doble fila de arboles.
        public static string BosqueEnValleSur(bool aplicar)
        {
            Transform muro = null;
            foreach (var t in EscenasR13.Todos()) if (t != null && t.name == "Valle_Sur") { muro = t; break; }
            if (muro == null) return "Valle_Sur: no encontrado\n";
            GameObject Prefab(string n) { foreach (var g in AssetDatabase.FindAssets(n + " t:Prefab")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p) == n) return AssetDatabase.LoadAssetAtPath<GameObject>(p); } return null; }
            var tipos = new[] { Prefab("P_Env_ArbolA"), Prefab("P_Env_ArbolB"), Prefab("P_Env_Arbusto_Grande"), Prefab("P_Env_Arbusto_Medio") };
            foreach (var p in tipos) if (p == null) return "faltan prefabs de arbol/arbusto\n";
            var rs = muro.GetComponentsInChildren<Renderer>(true); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(muro.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            int n = 0; var rnd = new System.Random(1411);
            if (!aplicar) return $"bosque Valle_Sur: {rs.Length} renderers a ocultar; muro x[{b.min.x:0},{b.max.x:0}] z[{b.min.z:0},{b.max.z:0}]\n";
            foreach (var r in rs) { r.enabled = false; EscenasR13.Tocar(r); }
            var raiz = new GameObject("R14_BosqueSur"); raiz.transform.SetParent(muro.parent, true);
            for (float x = b.min.x - 6f; x <= b.max.x + 6f; x += 3.4f)
                for (int fila = 0; fila < 2; fila++)
                {
                    int ti = rnd.Next(10) < 6 ? rnd.Next(2) : 2 + rnd.Next(2);
                    float zc = b.center.z + (fila == 0 ? 2f : 6f) + (float)(rnd.NextDouble() - 0.5) * 2.2f;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(tipos[ti], raiz.transform);
                    go.transform.position = new Vector3(x + (float)(rnd.NextDouble() - 0.5) * 2f, 0f, zc);
                    go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    go.transform.localScale *= 0.85f + (float)rnd.NextDouble() * 0.5f; n++;
                }
            EscenasR13.Tocar(raiz); EscenasR13.Sucia(raiz.scene);
            return $"bosque Valle_Sur: {n} plantas, {rs.Length} renderers del muro ocultos\n";
        }

        // #13/#24: las piezas estaticas del escenario con hueco (erizo, poste, muro caido, auto quemado, cartel, farol, concreto) tenian un BoxCollider que
        // frenaba balas donde no hay material. Se cambia por MeshCollider no convexo con la malla visual (solo estaticos, sin Rigidbody).
        static readonly string[] PiezasConHueco = { "P_Env_Barricada_Erizo", "P_Env_Poste_Caido", "P_Env_Muro_Caido", "P_Env_Auto_Quemado", "P_Env_Cartel", "P_Env_Farol", "P_Env_Barricada_Concreto" };
        public static string ColliderDeMalla(bool aplicar)
        {
            int n = 0, sinMalla = 0; var sb = new StringBuilder();
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                string nom = t.name.Replace("(Clone)", "");
                bool es = false; foreach (var p in PiezasConHueco) if (nom == p || nom.StartsWith(p + "_")) { es = true; break; }
                if (!es || t.GetComponent<Rigidbody>() != null) continue;
                var box = t.GetComponent<BoxCollider>(); if (box == null || box.isTrigger) continue;
                var mf = t.GetComponentInChildren<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable && false) { sinMalla++; continue; }
                n++;
                if (!aplicar) continue;
                // el MeshCollider va en el hijo que lleva la malla (sus transforms/escala son los de la malla)
                var go = mf.gameObject; var mc = go.GetComponent<MeshCollider>(); if (mc == null) mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = false; mc.isTrigger = false;
                Object.DestroyImmediate(box);
                EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene);
            }
            sb.AppendLine($"colliders de malla: {n} piezas {(aplicar ? "cambiadas" : "a cambiar")}, {sinMalla} sin malla");
            return sb.ToString();
        }

        // Test de balas: por cada pieza con hueco, un rayo por el centro de la caja envolvente debe pegar la pieza (macizo) y los rayos por las esquinas
        // vacias deben cruzar. Devuelve cuantas piezas dan paso por algun hueco (el BoxCollider anterior nunca dejaba pasar).
        public static string TestBalasPorHuecos()
        {
            Physics.SyncTransforms(); int piezas = 0, conHueco = 0, macizoOk = 0; var sb = new StringBuilder();
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                string nom = t.name.Replace("(Clone)", ""); bool es = false; foreach (var p in PiezasConHueco) if (nom == p || nom.StartsWith(p + "_")) { es = true; break; }
                if (!es) continue; var mc = t.GetComponentInChildren<MeshCollider>(); if (mc == null) continue;
                piezas++; var b = mc.bounds; var c = b.center; bool hueco = false, macizo = false;
                // rayos horizontales a 3 alturas y 5 posiciones laterales: de afuera hacia adentro
                for (int h = 0; h < 3 && !hueco; h++) for (int i = -2; i <= 2; i++)
                {
                    float y = b.min.y + b.size.y * (h + 0.5f) / 3f; var o = new Vector3(b.min.x - 2f, y, c.z + i * b.extents.z * 0.45f);
                    bool pega = mc.Raycast(new Ray(o, Vector3.right), out _, b.size.x + 4f);
                    if (!pega) { hueco = true; break; }
                }
                var oc = new Vector3(c.x, c.y, b.min.z - 2f); for (int k = 0; k < 3 && !macizo; k++) { var oo = oc + Vector3.up * (k - 1) * b.extents.y * 0.5f; if (mc.Raycast(new Ray(oo, Vector3.forward), out _, b.size.z + 4f)) macizo = true; }
                if (hueco) conHueco++; if (macizo) macizoOk++;
            }
            sb.AppendLine($"balas: {piezas} piezas con MeshCollider, {conHueco} dejan pasar un rayo por algun hueco, {macizoOk} frenan un rayo por el centro");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- flechas pintadas en el asfalto (mallas fusionadas M_Op_Amarillo)
        // Componente conexo = trozo de pintura. Un guion es un rectangulo (2 triangulos, 4 vertices soldados). Una flecha tiene mas triangulos o una forma que no es rectangulo.
        public static string QuitarFlechasDelPiso(bool aplicar, float zMin, float zMax)
        {
            var sb = new StringBuilder(); int total = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.name != "M_Op_Amarillo") continue;
                var m = mf.sharedMesh; if (m == null || !m.isReadable || m.subMeshCount != 1 || m.vertexCount < 100) continue;
                string ruta = AssetDatabase.GetAssetPath(m);
                if (!ruta.Contains("FusionOperacion") && !ruta.Contains("/Generated/R13/")) continue;
                var vs = m.vertices; var tr = m.triangles; var mat = mf.transform.localToWorldMatrix;
                int nT = tr.Length / 3;
                // soldadura por posicion (cm)
                var id = new Dictionary<(int, int, int), int>(); int Weld(Vector3 w) { var k = (Mathf.RoundToInt(w.x * 50f), Mathf.RoundToInt(w.y * 50f), Mathf.RoundToInt(w.z * 50f)); if (!id.TryGetValue(k, out int v)) { v = id.Count; id[k] = v; } return v; }
                var wv = new int[vs.Length]; for (int i = 0; i < vs.Length; i++) wv[i] = Weld(mat.MultiplyPoint3x4(vs[i]));
                var padre = new int[id.Count]; for (int i = 0; i < padre.Length; i++) padre[i] = i;
                int Raiz(int a) { while (padre[a] != a) { padre[a] = padre[padre[a]]; a = padre[a]; } return a; }
                for (int t = 0; t < nT; t++) { int a = Raiz(wv[tr[t * 3]]), b = Raiz(wv[tr[t * 3 + 1]]), c = Raiz(wv[tr[t * 3 + 2]]); padre[b] = a; padre[Raiz(c)] = a; }
                var porComp = new Dictionary<int, List<int>>();
                for (int t = 0; t < nT; t++) { int r = Raiz(wv[tr[t * 3]]); if (!porComp.TryGetValue(r, out var l)) { l = new List<int>(); porComp[r] = l; } l.Add(t); }
                var quitar = new HashSet<int>(); int flechas = 0; var centrosFlecha = new List<Vector3>();
                for (int pasada = 0; pasada < 2; pasada++)
                foreach (var kv in porComp)
                {
                    var tris = kv.Value; var vset = new HashSet<int>(); Vector3 mn = Vector3.one * 1e9f, mx = Vector3.one * -1e9f; Vector3 cen = Vector3.zero;
                    foreach (var t in tris) for (int k = 0; k < 3; k++) { int vi = tr[t * 3 + k]; vset.Add(wv[vi]); var w = mat.MultiplyPoint3x4(vs[vi]); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); cen += w; }
                    cen /= tris.Count * 3f;
                    bool rect = tris.Count == 2 && vset.Count == 4;
                    if (pasada == 0 && rect) continue;
                    if (pasada == 1) { if (!rect || quitar.Contains(tris[0])) continue; bool cerca = false; foreach (var cf in centrosFlecha) if ((new Vector2(cf.x, cf.z) - new Vector2(cen.x, cen.z)).magnitude < 3.2f) { cerca = true; break; } if (!cerca) continue; }
                    else if (quitar.Contains(tris[0])) continue;
                    var tam = mx - mn;
                    if (tam.x > 6f || tam.z > 6f) continue;    // lineas largas continuas (bordes): no son flechas
                    if (cen.z < zMin || cen.z > zMax) continue;
                    flechas++; foreach (var t in tris) quitar.Add(t); if (pasada == 0) centrosFlecha.Add(cen);
                    sb.AppendLine($"  flecha en ({cen.x:0.0},{cen.z:0.0}) tri={tris.Count} v={vset.Count} tam=({tam.x:0.0},{tam.z:0.0}) en {mf.name}");
                }
                if (flechas == 0) continue;
                total += flechas;
                if (!aplicar) continue;
                var keep = new List<int>(tr.Length);
                for (int t = 0; t < nT; t++) if (!quitar.Contains(t)) { keep.Add(tr[t * 3]); keep.Add(tr[t * 3 + 1]); keep.Add(tr[t * 3 + 2]); }
                Mesh destino;
                if (ruta.Contains("/Generated/R13/")) destino = m;
                else { EscenasR13.AsegurarCarpetaGenerada(); destino = Object.Instantiate(m); destino.name = "R14_" + m.name; var nueva = EscenasR13.CarpetaGenerada + "/R14_" + m.name + ".asset"; AssetDatabase.CreateAsset(destino, nueva); }
                destino.SetTriangles(keep, 0); destino.RecalculateBounds();
                if (destino != m) { mf.sharedMesh = destino; }
                EditorUtility.SetDirty(destino); EscenasR13.Tocar(mf); EscenasR13.Sucia(mf.gameObject.scene);
            }
            sb.Insert(0, $"flechas del piso: {total} {(aplicar ? "quitadas" : "encontradas")} (z {zMin}..{zMax})\n");
            return sb.ToString();
        }
    }
}
