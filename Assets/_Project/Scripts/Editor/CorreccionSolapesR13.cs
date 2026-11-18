using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.EditorTools
{
    /// <summary>
    /// Ronda 13 · T11, correccion. MODO PRUEBA por defecto (aplicar = false): solo informa que haria. Con aplicar = true:
    ///  1) borra los duplicados A (exactos) y B (a 15 cm) en la escena o, si viven dentro de un prefab, EN EL PREFAB (se informa cuantas instancias afecta);
    ///  2) borra EN ORIGEN los carteles de nombre de objeto (Rotulo_PUESTO*, ANTIAEREA, GENERADOR, SECTOR, RADIO, AYUNTAMIENTO y, por D9, PLAZA &gt;&gt; y CIUDAD &gt;&gt;;
    ///     queda BIENVENIDOS A; los CARGA_* solo se listan porque la carga referencia su texto);
    ///  3) aplica la lista revisada Docs/RONDA_13/solapes_lista_correccion.tsv: una linea por hallazgo D/E/F, "ACCION&lt;TAB&gt;escena:ruta[&lt;TAB&gt;dx&lt;TAB&gt;dy&lt;TAB&gt;dz]" con
    ///     ACCION = MOVER | BORRAR | APAGAR (las rutas salen tal cual de Temp/solapes_antes.txt; # = comentario).
    /// Firma: CorreccionSolapesR13.CorregirEscenaAbierta(bool aplicar = false). Va DESPUES de T9 y T12-T16 para no duplicar trabajo.
    /// </summary>
    public static class CorreccionSolapesR13
    {
        public const string ListaRevisada = "Docs/RONDA_13/solapes_lista_correccion.tsv";
        static readonly string[] RotulosFuera = { "Rotulo_PUESTO", "Rotulo_ANTIAEREA", "Rotulo_GENERADOR", "Rotulo_SECTOR", "Rotulo_AYUNTAMIENTO", "Rotulo_RADIO", "Rotulo_PLAZA", "Rotulo_CIUDAD" };

        [MenuItem("Strategic Point/Operacion/R13 T11 · Correccion de solapes (prueba)")]
        static void MenuPrueba() => Debug.Log(CorregirEscenaAbierta(false));

        [MenuItem("Strategic Point/Operacion/R13 T11 · Correccion de solapes (APLICAR)")]
        static void MenuAplicar() { if (EditorUtility.DisplayDialog("R13 T11", "Aplicar la correccion de solapes y guardar las escenas y los prefabs?", "Aplicar", "Cancelar")) Debug.Log(CorregirEscenaAbierta(true)); }

        static bool EsRotuloFuera(string n)
        {
            string m = n.StartsWith("P_Esc_Rotulo_") ? "Rotulo_" + n.Substring("P_Esc_Rotulo_".Length) : n;
            foreach (var p in RotulosFuera) if (m.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        sealed class Plan
        {
            public GameObject go; public string motivo; public string assetPath, relPath; public bool enPrefab;
        }

        // Un hijo de una instancia de prefab NO se edita en el asset: borrar ahi la pieza la sacaria de TODAS las instancias (incluida la que es el "original" del par: un pilar de
        // esquina compartido por dos muros se perderia en las dos). Se apaga SOLO esa copia en la escena (override de la instancia). Una instancia raiz o un objeto suelto se borra.
        static Plan Planear(GameObject go, string motivo)
        {
            return new Plan { go = go, motivo = motivo, enPrefab = false };
        }

        static void QuitarEnEscena(GameObject go)
        {
            if (go == null) return;
            var sc = go.scene;
            if (PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetNearestPrefabInstanceRoot(go) != go) { go.SetActive(false); EscenasR13.Tocar(go); EscenasR13.Sucia(sc); return; }
            bool soloPieza = go.transform.childCount == 0;
            if (soloPieza) foreach (var c in go.GetComponents<Component>()) if (!(c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider || c is UnityEngine.AI.NavMeshObstacle)) soloPieza = false;
            if (soloPieza) Object.DestroyImmediate(go);
            else
            {
                foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
                var mr = go.GetComponent<MeshRenderer>(); if (mr != null) Object.DestroyImmediate(mr);
                var mf = go.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf);
            }
            EscenasR13.Sucia(sc);
        }

        public static string CorregirEscenaAbierta(bool aplicar = false)
        {
            var sb = new StringBuilder("== R13 T11 correccion de solapes (" + (aplicar ? "APLICAR" : "PRUEBA") + ")\n");
            if (!EscenasR13.Asegurar(sb, false)) return EscenasR13.Cerrar(sb, "r13_t11_correccion.txt");

            // ---- 1) A y B
            var planes = new List<Plan>(); var vistosGo = new HashSet<GameObject>();
            int nA = 0, nB = 0;
            foreach (var h in AuditoriaDeSolapesR13.DuplicadosAyB())
            {
                if (h.go == null || !vistosGo.Add(h.go)) continue;
                if (h.cat == 'A') nA++; else nB++;
                planes.Add(Planear(h.go, h.cat + " " + h.detalle));
            }
            // ---- 2) carteles con nombre de objeto
            int nRot = 0; var cargas = new List<string>();
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null) continue;
                if (t.name.StartsWith("Rotulo_CARGA") || t.name.StartsWith("P_Esc_Rotulo_CARGA")) { cargas.Add(EscenasR13.Ruta(t)); continue; }
                if (!EsRotuloFuera(t.name) || !vistosGo.Add(t.gameObject)) continue;
                nRot++; planes.Add(Planear(t.gameObject, "ROTULO " + t.name));
            }
            sb.AppendLine($"duplicados A={nA} B={nB}; carteles a borrar en origen={nRot}; carteles CARGA_* solo listados={cargas.Count}");
            foreach (var c in cargas) sb.AppendLine("  CARGA (no se toca; la carga referencia su texto) " + c);

            // instancias afectadas por cada prefab
            var instancias = new Dictionary<string, int>();
            foreach (var p in planes) if (p.enPrefab && !instancias.ContainsKey(p.assetPath)) instancias[p.assetPath] = 0;
            if (instancias.Count > 0)
                foreach (var t in EscenasR13.Todos())
                {
                    if (t == null || !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    var s = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                    string ap = s != null ? AssetDatabase.GetAssetPath(s) : null;
                    if (ap != null && instancias.ContainsKey(ap)) instancias[ap]++;
                }
            foreach (var p in planes)
                sb.AppendLine($"{(p.enPrefab ? "PREFAB " + p.assetPath + "#" + p.relPath + $" ({instancias[p.assetPath]} instancias)" : "ESCENA " + EscenasR13.Ruta(p.go.transform))}  <- {p.motivo}");

            // ---- 3) lista revisada
            var acciones = new List<(string accion, string ruta, Vector3 d)>();
            if (File.Exists(ListaRevisada))
                foreach (var linea in File.ReadAllLines(ListaRevisada))
                {
                    if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith("#")) continue;
                    var c = linea.Split('\t');
                    if (c.Length < 2) { sb.AppendLine("LINEA IGNORADA (faltan columnas): " + linea); continue; }
                    var d = Vector3.zero;
                    if (c.Length >= 5) float.TryParse(c[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d.x);
                    if (c.Length >= 5) float.TryParse(c[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d.y);
                    if (c.Length >= 5) float.TryParse(c[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d.z);
                    acciones.Add((c[0].Trim().ToUpperInvariant(), c[1].Trim(), d));
                }
            else sb.AppendLine($"(no existe {ListaRevisada}: D/E/F sin tocar; se revisan a mano con Temp/solapes_antes.txt)");
            var porRuta = new Dictionary<string, Transform>();
            if (acciones.Count > 0) foreach (var t in EscenasR13.Todos()) if (t != null) { string r = EscenasR13.Ruta(t); if (!porRuta.ContainsKey(r)) porRuta[r] = t; }
            foreach (var a in acciones)
                sb.AppendLine($"LISTA {a.accion} {a.ruta} {(porRuta.ContainsKey(a.ruta) ? (a.accion == "MOVER" ? a.d.ToString() : "") : "NO ENCONTRADO")}");

            if (!aplicar) { sb.AppendLine("PRUEBA: no se modifico nada."); return EscenasR13.Cerrar(sb, "r13_t11_correccion.txt"); }

            // ---- aplicar: primero los prefabs (una pasada por asset), despues la escena
            var porAsset = new Dictionary<string, List<string>>();
            foreach (var p in planes) if (p.enPrefab) { if (!porAsset.TryGetValue(p.assetPath, out var l)) porAsset[p.assetPath] = l = new List<string>(); if (!l.Contains(p.relPath)) l.Add(p.relPath); }
            int borradosPrefab = 0;
            foreach (var kv in porAsset)
                EscenasR13.EditarPrefab(kv.Key, raiz => { foreach (var rel in kv.Value) { var t = raiz.transform.Find(rel); if (t != null) { Object.DestroyImmediate(t.gameObject); borradosPrefab++; } } }, sb);
            int borradosEscena = 0;
            foreach (var p in planes) if (!p.enPrefab && p.go != null) { QuitarEnEscena(p.go); borradosEscena++; }
            int movidos = 0, apagados = 0, quitados = 0;
            foreach (var a in acciones)
            {
                if (!porRuta.TryGetValue(a.ruta, out var t) || t == null) continue;
                var go = t.gameObject;
                if (a.accion == "MOVER") { t.position += a.d; EscenasR13.Tocar(t); EscenasR13.Sucia(go.scene); movidos++; }
                else if (a.accion == "APAGAR") { go.SetActive(false); EscenasR13.Tocar(go); EscenasR13.Sucia(go.scene); apagados++; }
                else if (a.accion == "BORRAR")
                {
                    var p = Planear(go, "lista");
                    if (p.enPrefab) sb.AppendLine("AVISO BORRAR dentro de un prefab (" + p.assetPath + "#" + p.relPath + "): agregarlo como ACCION de prefab a mano; no se borra");
                    else { QuitarEnEscena(go); quitados++; }
                }
            }
            AssetDatabase.SaveAssets();
            EscenasR13.Guardar(sb);
            sb.Insert(0, $"APLICADO: borrados en prefab={borradosPrefab} en escena={borradosEscena}; lista: movidos={movidos} apagados={apagados} borrados={quitados}. Rehornear el NavMesh y volver a correr AuditoriaDeSolapesR13.Informe(\"despues\").\n");
            return EscenasR13.Cerrar(sb, "r13_t11_correccion.txt");
        }
    }
}
