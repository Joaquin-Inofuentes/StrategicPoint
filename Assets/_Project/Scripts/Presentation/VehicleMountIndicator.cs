using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Vehicles;

namespace SP.Presentation
{
    // Cuando le apuntás a un vehículo, muestra una flecha (cilindro +
    // cono, apuntando de arriba hacia abajo) sobre el vehículo, y una
    // línea por cada aliado libre y cercano que subiría solo si se le da
    // la orden — así se ve de antemano quién se va a subir.
    public class VehicleMountIndicator : MonoBehaviour
    {
        static readonly Color ArrowColor = new Color(0.25f, 0.55f, 0.95f);

        GameObject arrowRoot;
        readonly List<LineRenderer> lines = new List<LineRenderer>();

        public static VehicleMountIndicator Create()
        {
            var go = new GameObject("VehicleMountIndicator");
            return go.AddComponent<VehicleMountIndicator>();
        }

        void EnsureArrow()
        {
            if (arrowRoot != null) return;

            arrowRoot = new GameObject("MountArrow");
            arrowRoot.transform.SetParent(transform, false);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(arrowRoot.transform, false);
            DestroyCollider(shaft);
            shaft.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            shaft.transform.localScale = new Vector3(0.12f, 0.5f, 0.12f);
            ApplyColor(shaft, ArrowColor);

            var head = new GameObject("Head");
            head.transform.SetParent(arrowRoot.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var mf = head.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildConeMesh(0.3f, 0.55f, 14);
            var mr = head.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SafeMaterial.Create(ArrowColor);
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        static void ApplyColor(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = SafeMaterial.Create(color);
        }

        // Cono simple con la punta hacia abajo (apex en y=0, base en y=height).
        static Mesh BuildConeMesh(float radius, float height, int segments)
        {
            var mesh = new Mesh { name = "ConeMesh" };
            var vertices = new Vector3[segments + 2];
            var triangles = new List<int>();

            vertices[0] = Vector3.zero; // apex, abajo
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(t) * radius, height, Mathf.Sin(t) * radius);
            }
            vertices[segments + 1] = new Vector3(0f, height, 0f); // centro de la base

            for (int i = 0; i < segments; i++)
            {
                int a = i + 1;
                int b = (i + 1) % segments + 1;
                triangles.Add(0); triangles.Add(b); triangles.Add(a); // pared lateral
                triangles.Add(segments + 1); triangles.Add(a); triangles.Add(b); // tapa de la base
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Show(Vehicle vehicle, IEnumerable<Soldier> incomingAllies)
        {
            EnsureArrow();
            arrowRoot.SetActive(true);
            arrowRoot.transform.position = vehicle.transform.position + Vector3.up * 3.2f;

            int i = 0;
            foreach (var ally in incomingAllies)
            {
                var lr = GetOrCreateLine(i);
                lr.gameObject.SetActive(true);
                lr.SetPosition(0, ally.transform.position + Vector3.up * 0.6f);
                lr.SetPosition(1, vehicle.transform.position);
                i++;
            }
            for (int j = i; j < lines.Count; j++) lines[j].gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (arrowRoot != null) arrowRoot.SetActive(false);
            foreach (var lr in lines) lr.gameObject.SetActive(false);
        }

        LineRenderer GetOrCreateLine(int index)
        {
            if (index < lines.Count) return lines[index];

            var go = new GameObject($"MountLine_{index}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.08f;
            lr.useWorldSpace = true;
            lr.material = SafeMaterial.Create(ArrowColor);
            lr.startColor = ArrowColor;
            lr.endColor = ArrowColor;
            lines.Add(lr);
            return lr;
        }
    }
}
