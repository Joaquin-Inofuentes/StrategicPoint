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
            var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            var invisibleColliders = new List<Collider>();
            foreach (var c in colliders)
            {
                if (c.isTrigger) continue;
                if (c.GetComponentInParent<SP.Vehicles.HitboxDeImpacto>() != null) continue;
                if (c.GetComponentInParent<SP.Actors.Soldier>() != null) continue;
                if (c.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                
                var meshes = c.GetComponentsInChildren<MeshRenderer>(true);
                var skinned = c.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (meshes.Length == 0 && skinned.Length == 0)
                {
                    invisibleColliders.Add(c);
                }
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
            
            Debug.Log($"Auditoría completada. {invisibleColliders.Count} colliders invisibles, {coberturasMeshSinCollider.Count} mallas de cobertura sin collider. Guardado en {path}");
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
