using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ronda 13, T16: assets menos primitivos. Cada prop tocado tiene >= 5 piezas visuales o >= 200 triangulos, el collider raiz no cambia y la ciudad no
    // suma mas de 10 % de renderers activos respecto de ANTES (Docs/RONDA_13/antes/renderers_ciudad.txt, un entero; si no existe solo se informa).
    public static partial class ChecksBugs065
    {
        static int R13_T16_Triangulos(Transform t)
        {
            int n = 0;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(false))
            {
                var m = mf.sharedMesh; if (m == null) continue;
                var mr = mf.GetComponent<MeshRenderer>(); if (mr != null && !mr.enabled) continue;
                for (int s = 0; s < m.subMeshCount; s++) n += (int)(m.GetIndexCount(s) / 3);
            }
            return n;
        }

        // ---------------------------------------------------------------- 985a detalle suficiente, collider raiz igual, sin inflar los renderers de la ciudad
        static IEnumerator Bug985a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(5);
            foreach (var x in Esperar(3f)) yield return x;
            Physics.SyncTransforms();

            // 1) props con Detalle_R13: >= 5 piezas visuales o >= 200 triangulos; su collider raiz (BoxCollider/CapsuleCollider del propio prop o de sus hijos) sigue ahi
            int props = 0, pobres = 0, sinCollider = 0; var lista = new StringBuilder();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t == null || t.name != "Detalle_R13" || t.parent == null) continue;
                props++;
                var prop = t.parent;
                int piezas = 0; foreach (var r in t.GetComponentsInChildren<MeshRenderer>(false)) if (r.enabled) piezas++;
                int tris = R13_T16_Triangulos(t);
                if (piezas < 5 && tris < 200) { pobres++; if (lista.Length < 300) lista.Append($"{prop.name}({piezas} piezas, {tris} tris) "); }
                bool hayCollider = prop.GetComponent<Collider>() != null || prop.GetComponentInChildren<Collider>(false) != null;
                bool esArbol = prop.name.StartsWith("Arbol_") || prop.name == "Patio_Arbol";
                bool sinColliderPorDiseno = prop.name == "Hidrante" || prop.name == "Patio_Puesto" || prop.name == "Contenedor" || esArbol;   // algunos nacieron sin collider (Hidrante) o con el del Tronco
                if (!hayCollider && !sinColliderPorDiseno) { sinCollider++; if (lista.Length < 300) lista.Append(prop.name + "(sin collider) "); }
            }
            W8Ok(ref ok, props >= 20, sb, $"{props} props con Detalle_R13 en las escenas (si es 0 la Ola 2 no corrio)");
            W8Ok(ref ok, pobres == 0, sb, $"{pobres} props con menos de 5 piezas y menos de 200 triangulos ({lista})");
            W8Ok(ref ok, sinCollider == 0, sb, $"{sinCollider} props que perdieron su collider raiz");

            // 2) camiones: el GO raiz conserva nombre, collider y escala, y la malla nueva tiene detalle
            // (Camion_3 se apago en T12 por decision: Bug981a pide 0 camiones en z < -200; solo se exige a Camion_Patio y, si siguiera activo, a Camion_3)
            foreach (var nombre in new[] { "Camion_3", "Camion_Patio" })
            {
                Transform cam = null;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)) if (t != null && t.name == nombre) cam = t;
                if (cam == null && nombre == "Camion_3") { sb.Append("[Camion_3 apagado por T12] "); continue; }
                if (cam == null) { W8Ok(ref ok, false, sb, $"existe {nombre}"); continue; }
                var bc = cam.GetComponent<BoxCollider>(); var mf = cam.GetComponent<MeshFilter>();
                bool colOk = bc != null && Vector3.Distance(bc.size, Vector3.one) < 0.01f && bc.center.sqrMagnitude < 0.0001f;
                int tris = mf != null && mf.sharedMesh != null ? (int)(mf.sharedMesh.triangles.Length / 3) : 0;
                var ls = cam.lossyScale; bool escala = Mathf.Abs(Mathf.Max(ls.x, ls.z) - 7f) < 0.05f && Mathf.Abs(Mathf.Min(ls.x, ls.z) - 3f) < 0.05f && Mathf.Abs(ls.y - 2.8f) < 0.05f;
                W8Ok(ref ok, colOk && escala, sb, $"{nombre}: collider raiz sin cambios (size {(bc != null ? bc.size.ToString() : "-")}) y escala {ls}");
                W8Ok(ref ok, tris >= 200, sb, $"{nombre}: {tris} triangulos (pide >= 200)");
            }

            // 3) renderers activos en la caja de la ciudad (x 262..380, z 162..296)
            var caja = Rect.MinMaxRect(262f, 162f, 380f, 296f); int rends = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                if (r != null && r.enabled && caja.Contains(new Vector2(r.bounds.center.x, r.bounds.center.z))) rends++;
            string archivo = Path.Combine("Docs", "RONDA_13", "antes", "renderers_ciudad.txt");
            if (File.Exists(archivo) && int.TryParse(File.ReadAllText(archivo).Trim(), out int antes) && antes > 0)
                W8Ok(ref ok, rends <= antes * 1.10f, sb, $"renderers activos en la ciudad: {rends} (antes {antes}; tope +10 % = {antes * 1.10f:0})");
            else sb.Append($"[renderers activos en la ciudad: {rends}; sin referencia ANTES en {archivo}: guardar ese entero para que el check mida +10 %] ");
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
