using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace SP.EditorTools
{
    public class AuditoriaDeColliders : EditorWindow
    {
        [MenuItem("Strategic Point/Auditar colliders sin malla")]
        public static void Auditar()
        {
            var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include);
            
            var invisibleColliders = new List<Collider>();
            foreach (var c in colliders)
            {
                if (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                if (c.GetComponentInParent<SP.Vehicles.HitboxDeImpacto>() != null) continue;
                if (c.GetComponentInParent<SP.Actors.Soldier>() != null) continue;
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                
                if (EnListaBlanca(c.transform)) continue;
                // R13 T10: cuenta solo los renderers ENCENDIDOS (antes contaba tambien los apagados y no detectaba portones, sacos ni compuertas).
                bool visible = false;
                foreach (var r in c.GetComponentsInChildren<Renderer>(false))
                    if (r != null && r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)) { visible = true; break; }
                if (!visible) invisibleColliders.Add(c);
            }

            var coberturasSolidos = SP.Core.Coberturas.Solidos();
            var coberturasMeshSinCollider = new List<MeshRenderer>();
            
            foreach (var c in coberturasSolidos)
            {
                var meshes = c.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var m in meshes)
                {
                    if (m.GetComponent<Collider>() == null)
                    {
                        coberturasMeshSinCollider.Add(m);
                    }
                }
            }

            string path = "Docs/RONDA_14_COLLIDERS.csv";
            Directory.CreateDirectory("Docs");
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("scene,hierarchy path,center,size,builder name,issue");
                
                foreach (var c in invisibleColliders)
                {
                    writer.WriteLine($"{c.gameObject.scene.name},{GetPath(c.transform)},{c.bounds.center},{c.bounds.size},{GetBuilderName(c.gameObject)},InvisibleCollider");
                }
                
                foreach (var m in coberturasMeshSinCollider)
                {
                    writer.WriteLine($"{m.gameObject.scene.name},{GetPath(m.transform)},{m.bounds.center},{m.bounds.size},{GetBuilderName(m.gameObject)},CoberturaMeshSinCollider");
                }
            }
            
            EscribirInforme(invisibleColliders);
            Debug.Log($"Auditoría completada. {invisibleColliders.Count} colliders invisibles, {coberturasMeshSinCollider.Count} mallas de cobertura sin collider. Guardado en {path}");
        }
        
        // Limites fisicos y piezas con collider por diseno (R13 T10).
        public static readonly string[] ListaBlanca =
            { "Borde", "Valle", "Cierre", "Ground", "Saco", "Porton", "ArbolCollider", "Mesa_Colision", "Muro_Salida_Norte" };

        static bool EnListaBlanca(Transform t)
        {
            for (var x = t; x != null; x = x.parent)
                foreach (var n in ListaBlanca) if (x.name.Contains(n)) return true;
            return false;
        }

        // Escribe Docs/RONDA_13/colliders_<etiqueta>.txt con los colliders solidos y sin pintar fuera de la lista blanca (salida esperada: 0).
        public static string Informe(string etiqueta)
        {
            var lista = new List<Collider>();
            foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c.isTrigger || !c.enabled) continue;
                if (c.GetComponentInParent<SP.Vehicles.HitboxDeImpacto>() != null || c.GetComponentInParent<SP.Actors.Soldier>() != null || c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (EnListaBlanca(c.transform)) continue;
                bool visible = false;
                foreach (var r in c.GetComponentsInChildren<Renderer>(false))
                    if (r != null && r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)) { visible = true; break; }
                if (!visible) lista.Add(c);
            }
            return EscribirInforme(lista, etiqueta);
        }

        static string EscribirInforme(List<Collider> lista, string etiqueta = "auditoria")
        {
            Directory.CreateDirectory("Docs/RONDA_13");
            string ruta = $"Docs/RONDA_13/colliders_{etiqueta}.txt";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# colliders solidos activos sin renderer encendido, fuera de la lista blanca ({string.Join(", ", ListaBlanca)})");
            sb.AppendLine($"invisibles_fuera_de_lista={lista.Count}");
            foreach (var c in lista) sb.AppendLine($"{c.gameObject.scene.name}	{GetPath(c.transform)}	{c.bounds.center}	{c.bounds.size}");
            File.WriteAllText(ruta, sb.ToString());
            return ruta + " (" + lista.Count + ")";
        }

        static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path.Replace(",", "");
        }
        
        static string GetBuilderName(GameObject go)
        {
            return go.name.Replace(",", "");
        }
    }
}
