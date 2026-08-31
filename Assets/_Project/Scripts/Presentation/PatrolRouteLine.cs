using UnityEngine;

namespace SP.Presentation
{
    // Dibuja con un LineRenderer de verdad (no un Gizmo, que solo se ve en
    // el editor) el circuito de patrulla de un enemigo, cerrando el loop
    // entre el último punto y el primero.
    public class PatrolRouteLine : MonoBehaviour
    {
        public static PatrolRouteLine Spawn(Vector3[] points, Color color, float height = 0.05f)
        {
            var go = new GameObject("PatrolRoute");
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.positionCount = points.Length;
            lr.widthMultiplier = 0.12f;
            lr.useWorldSpace = true;

            lr.material = SafeMaterial.Create(color);
            lr.startColor = color;
            lr.endColor = color;

            for (int i = 0; i < points.Length; i++)
                lr.SetPosition(i, points[i] + Vector3.up * height);

            return go.AddComponent<PatrolRouteLine>();
        }
    }
}
