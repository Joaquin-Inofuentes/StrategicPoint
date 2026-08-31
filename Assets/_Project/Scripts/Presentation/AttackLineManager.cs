using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Ai;

namespace SP.Presentation
{
    // Línea roja entre un soldado y el enemigo al que le está disparando
    // mientras está en estado Attack. Revisa a todo el mundo cada frame
    // (son pocos soldados) y crea/reposiciona/borra las líneas solas.
    public class AttackLineManager : MonoBehaviour
    {
        static readonly Color LineColor = new Color(0.9f, 0.15f, 0.12f);

        readonly Dictionary<int, LineRenderer> lines = new Dictionary<int, LineRenderer>();

        void Update()
        {
            foreach (var soldier in ActorRegistry.All)
            {
                if (soldier == null) continue;
                // soldier.Brain en vez de GetComponent<AiBrain>(): esto
                // corre en Update() para cada soldado, cada frame -- con
                // el registro ya cacheado en Soldier no hace falta pagar
                // GetComponent otra vez para lo mismo.
                var brain = soldier.Brain;
                bool attacking = brain != null && brain.State == AiState.Attack && brain.CurrentTarget != null
                    && soldier.gameObject.activeInHierarchy;

                if (!attacking)
                {
                    RemoveLine(soldier.Id);
                    continue;
                }

                if (!lines.TryGetValue(soldier.Id, out var lr) || lr == null)
                {
                    lr = CreateLine();
                    lines[soldier.Id] = lr;
                }

                lr.SetPosition(0, soldier.transform.position + Vector3.up * 0.5f);
                lr.SetPosition(1, brain.CurrentTarget.transform.position + Vector3.up * 0.5f);
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
            var go = new GameObject("AttackLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.05f;
            lr.useWorldSpace = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = LineColor };
            lr.material = mat;
            lr.startColor = LineColor;
            lr.endColor = LineColor;
            return lr;
        }

        // Si esta línea se crea por primera vez recién en medio del combate
        // (Play mode), Unity compila esa variante de shader ahí mismo y el
        // frame se traba (a veces sale una captura negra). Se precalienta
        // una, lejos y chiquita, al armar el nivel en el editor.
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
