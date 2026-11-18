using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Actors;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    /// <summary>
    /// Ronda 13 · T17, pulido general del nivel 2. A diferencia de PulidoNivel2Nov11 (mismo patron de lista de nombres) esto GUARDA en la escena. Hace, en este orden:
    ///  1) da cuerpo (BoxCollider) a los props macizos de la lista de nombres (la de Nov11 mas Docs/RONDA_13/pulido_nombres.txt, un prefijo por linea) que no lo tienen;
    ///  2) colliders desalineados del arte (n1 de Bug501g): con corregirColliders re-ajusta el BoxCollider a los bounds del arte (ArteOp);
    ///  3) flotantes (informe F de T11): con corregirFlotantes baja el objeto hasta apoyarlo (solo si el hueco es de 0,3..1,5 m y no es un elemento elevado por diseno).
    /// Por defecto soloInforme = true: solo lista. Las falsos positivos conocidos de 501g (sacos, Reflector_Baranda_*, remaches del porton) se documentan en la lista blanca del check.
    /// Firma: PulidoNivel2R13.EscenaAbierta(bool soloInforme = true, bool corregirColliders = false, bool corregirFlotantes = false)
    /// </summary>
    public static class PulidoNivel2R13
    {
        public const string ListaNombres = "Docs/RONDA_13/pulido_nombres.txt";
        static readonly string[] NombresBase =
        {
            "Roca", "Casita", "TanqueDeAgua", "Arco_Pilar", "Arco_Base", "Obelisco", "Obelisco_Base", "Pedestal", "Fuentecita_Cuenco",
            "Cabina", "Saco_Anillo", "Saco_Anillo_Alto", "Cantero", "Topiario", "Cactus",
        };

        [MenuItem("Strategic Point/Operacion/R13 T17 · Pulido del nivel 2 (informe)")]
        static void MenuInforme() => Debug.Log(EscenaAbierta(true));

        [MenuItem("Strategic Point/Operacion/R13 T17 · Pulido del nivel 2 (APLICAR colliders nuevos)")]
        static void MenuAplicar() { if (EditorUtility.DisplayDialog("R13 T17", "Agregar los colliders de la lista y guardar las escenas?", "Aplicar", "Cancelar")) Debug.Log(EscenaAbierta(false)); }

        public static string EscenaAbierta(bool soloInforme = true, bool corregirColliders = false, bool corregirFlotantes = false)
        {
            var sb = new StringBuilder("== R13 T17 pulido nivel 2" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t17.txt");

            var nombres = new List<string>(NombresBase);
            if (File.Exists(ListaNombres)) foreach (var l in File.ReadAllLines(ListaNombres)) if (!string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")) nombres.Add(l.Trim());

            // ---- 1) props macizos sin collider
            int nuevos = 0, candidatos = 0;
            foreach (var t in EscenasR13.Todos())
            {
                var mr = t != null ? t.GetComponent<MeshRenderer>() : null;
                if (mr == null) continue;
                bool coincide = false; foreach (var n in nombres) if (t.name == n || (n.EndsWith("*") && t.name.StartsWith(n.TrimEnd('*')))) { coincide = true; break; }
                if (!coincide && t.name.StartsWith("Auto_Quemado_") && t.name.EndsWith("_Cabina")) coincide = true;
                if (!coincide || t.GetComponent<Collider>() != null) continue;
                if (mr.GetComponentInParent<Soldier>() != null || mr.GetComponentInParent<Vehicle>() != null) continue;
                var mf = t.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                candidatos++;
                sb.AppendLine($"SIN COLLIDER {EscenasR13.Ruta(t)} en {EscenasR13.P(t.position)}");
                if (soloInforme) continue;
                var b = mf.sharedMesh.bounds; var bc = t.gameObject.AddComponent<BoxCollider>();
                float fx = t.name == "Cactus" ? 0.3f : t.name == "Roca" ? 0.85f : 1f;
                bc.center = b.center; bc.size = new Vector3(b.size.x * fx, b.size.y, b.size.z * fx);
                EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); nuevos++;
            }
            sb.AppendLine($"1) props de la lista sin collider: {candidatos}; colliders agregados: {nuevos}");

            // ---- 2) colliders desalineados del arte (n1 de 501g)
            int desalineados = 0, ajustados = 0;
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
                if (!(desfase > 0.7f || exceso > 1.6f || falta < 0.55f)) continue;
                desalineados++;
                sb.AppendLine($"COLLIDER DESALINEADO {EscenasR13.Ruta(om.transform)}: desfase {desfase:0.0} m, collider/arte {falta:0.00}..{exceso:0.00}");
                if (soloInforme || !corregirColliders || !(col is BoxCollider box)) continue;
                var ls = om.transform.lossyScale;
                box.center = om.transform.InverseTransformPoint(ab.center);
                box.size = new Vector3(ab.size.x / Mathf.Max(0.001f, Mathf.Abs(ls.x)), ab.size.y / Mathf.Max(0.001f, Mathf.Abs(ls.y)), ab.size.z / Mathf.Max(0.001f, Mathf.Abs(ls.z)));
                EscenasR13.Tocar(box); EscenasR13.Sucia(om.gameObject.scene); ajustados++;
            }
            sb.AppendLine($"2) colliders desalineados: {desalineados}; ajustados: {ajustados}" + (soloInforme || corregirColliders ? "" : " (corregirColliders = false: solo informe)"));

            // ---- 3) flotantes
            int flot = 0, bajados = 0;
            foreach (var h in AuditoriaDeSolapesR13.FlotantesYEnterradosF(400))
            {
                flot++;
                sb.AppendLine($"F {h.escena} {h.ruta} {h.detalle}");
                if (soloInforme || !corregirFlotantes || h.renderer == null || !h.detalle.StartsWith("flotante: ")) continue;
                string num = h.detalle.Substring("flotante: ".Length).Split(' ')[0];
                if (!float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gap) || gap < 0.3f || gap > 1.5f) continue;
                var t = h.renderer.transform; t.position += Vector3.down * gap;
                EscenasR13.Tocar(t); EscenasR13.Sucia(t.gameObject.scene); bajados++;
            }
            sb.AppendLine($"3) flotantes/enterrados: {flot}; bajados: {bajados}" + (soloInforme || corregirFlotantes ? "" : " (corregirFlotantes = false: solo informe)"));

            if (!soloInforme) { AssetDatabase.SaveAssets(); EscenasR13.Guardar(sb); }
            return EscenasR13.Cerrar(sb, "r13_t17.txt");
        }
    }
}
