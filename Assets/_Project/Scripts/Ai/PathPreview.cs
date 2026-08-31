using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Ai
{
    // Item 218: vista previa de la ruta.
    //
    // Antes de este item el jugador no tenia forma de saber POR DONDE iba a
    // ir la escuadra: solo veia el marcador del destino, y si habia un
    // obstaculo en el medio se enteraba recien cuando los soldados se
    // encajaban contra el. Esto dibuja la ruta calculada mientras se
    // mantiene el clic, antes de confirmar la orden.
    //
    // Un LineRenderer unico y reusado, no uno por segmento: la ruta cambia
    // cada frame mientras se arrastra el cursor, y crear/destruir por frame
    // seria basura constante en el peor momento.
    public class PathPreview : MonoBehaviour
    {
        public static PathPreview Instance { get; private set; }

        static readonly Color PathColor = new Color(0.4f, 0.9f, 1f, 0.9f);

        LineRenderer line;
        readonly List<Vector3> buffer = new List<Vector3>(64);

        WaypointGraph graph;

        void OnEnable()
        {
            Instance = this;
            EnsureLine();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            Hide();
        }

        public void Attach(WaypointGraph waypointGraph) => graph = waypointGraph;

        // Idempotente y por nombre, como el resto del proyecto: un campo
        // privado no sobrevive el domain reload, pero el hijo si.
        void EnsureLine()
        {
            if (line != null) return;
            var existing = transform.Find("PathPreviewLine");
            if (existing != null) line = existing.GetComponent<LineRenderer>();
            if (line != null) return;

            var go = new GameObject("PathPreviewLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.16f;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            line.material = new Material(shader) { color = PathColor };
            line.startColor = PathColor;
            line.endColor = PathColor;
            line.positionCount = 0;
        }

        // Devuelve false si no hay ruta: el llamador puede usar eso para
        // avisar "destino inalcanzable" en vez de dejar al jugador dar una
        // orden que nadie va a poder cumplir.
        public bool Show(Vector3 from, Vector3 to)
        {
            EnsureLine();
            if (line == null) return false;

            if (graph == null || !graph.IsBuilt)
            {
                // Sin grafo, la "ruta" es la linea recta: es exactamente lo
                // que van a hacer los soldados, asi que la vista previa
                // sigue siendo honesta.
                DrawStraight(from, to);
                return true;
            }

            buffer.Clear();
            if (!graph.TryFindPath(from, to, buffer) || buffer.Count < 2)
            {
                Hide();
                return false;
            }

            line.positionCount = buffer.Count;
            for (int i = 0; i < buffer.Count; i++)
            {
                var p = buffer[i];
                // Un poco por encima del suelo: a ras se pelea en z-fight
                // con el plano y la linea aparece cortada a parches.
                line.SetPosition(i, new Vector3(p.x, p.y + 0.12f, p.z));
            }
            line.enabled = true;
            return true;
        }

        void DrawStraight(Vector3 from, Vector3 to)
        {
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(from.x, from.y + 0.12f, from.z));
            line.SetPosition(1, new Vector3(to.x, to.y + 0.12f, to.z));
            line.enabled = true;
        }

        public void Hide()
        {
            if (line == null) return;
            line.positionCount = 0;
            line.enabled = false;
        }

        public bool IsShowing => line != null && line.enabled && line.positionCount > 0;
        public int PointCount => line != null ? line.positionCount : 0;
    }
}
