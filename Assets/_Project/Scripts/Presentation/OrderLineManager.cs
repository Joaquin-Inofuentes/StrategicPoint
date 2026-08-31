using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Linea del soldado a su destino mientras dure una orden de
    // movimiento simple en RTS. El marcador (OrderMarkerFx) ya dice
    // DONDE hay un destino; esta linea dice DE QUIEN es, algo que con
    // varios soldados en movimiento simultaneo el marcador solo no
    // puede responder. Mismo patron que AttackLineManager: revisa a
    // todos cada frame y crea/reposiciona/borra las lineas solo.
    public class OrderLineManager : MonoBehaviour
    {
        static readonly Color LineColor = new Color(0.35f, 0.85f, 0.35f, 0.6f);

        readonly Dictionary<int, LineRenderer> lines = new Dictionary<int, LineRenderer>();

        void Update()
        {
            foreach (var soldier in ActorRegistry.All)
            {
                if (soldier == null) continue;
                var brain = soldier.Brain;
                var destination = brain != null ? brain.CurrentOrderDestination : null;

                if (!destination.HasValue || !soldier.gameObject.activeInHierarchy)
                {
                    RemoveLine(soldier.Id);
                    continue;
                }

                if (!lines.TryGetValue(soldier.Id, out var lr) || lr == null)
                {
                    lr = CreateLine();
                    lines[soldier.Id] = lr;
                }

                lr.SetPosition(0, soldier.transform.position + Vector3.up * 0.3f);
                lr.SetPosition(1, destination.Value + Vector3.up * 0.05f);
            }
        }

        void RemoveLine(int actorId)
        {
            if (!lines.TryGetValue(actorId, out var lr)) return;
            lines.Remove(actorId);
            if (lr != null) Destroy(lr.gameObject);
        }

        static LineRenderer CreateLine()
        {
            var go = new GameObject("OrderLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.04f;
            lr.useWorldSpace = true;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { color = LineColor };
            lr.material = mat;
            lr.startColor = LineColor;
            lr.endColor = LineColor;
            return lr;
        }

        // Mismo motivo que AttackLineManager.Prewarm: compilar la
        // variante del shader la primera vez en medio del juego real
        // trababa el frame.
        public static void Prewarm()
        {
            var lr = CreateLine();
            lr.transform.position = new Vector3(0f, -500f, 0f);
            lr.SetPosition(0, lr.transform.position);
            lr.SetPosition(1, lr.transform.position + Vector3.right * 0.01f);
            if (Application.isPlaying) Object.Destroy(lr.gameObject);
            else Object.DestroyImmediate(lr.gameObject);
        }
    }
}
