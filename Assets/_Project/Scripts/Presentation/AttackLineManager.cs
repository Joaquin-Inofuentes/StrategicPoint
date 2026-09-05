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
            // H2: en FPS esta linea nace a centimetros de la camara (el
            // soldado que la dispara puede ser el propio poseido, o uno
            // pegado a el) y el ancho del LineRenderer se orienta DE CARA
            // A LA CAMARA en cada punto (alignment View, el default) --
            // de cerca y en un angulo rasante eso proyecta como un
            // triangulo enorme y oscuro tapando media pantalla, no como
            // la lineita fina que se ve bien desde arriba en RTS. Reusa
            // la misma señal que ya separa FPS de RTS en todo el
            // proyecto (CameraRig.SetMode pone cam.orthographic=true
            // solo en RTS) para no dibujarla fuera de ahi.
            var cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                if (lines.Count > 0) RemoveAllLines();
                return;
            }

            foreach (var soldier in ActorRegistry.All)
            {
                if (soldier == null) continue;
                // soldier.Brain en vez de GetComponent<AiBrain>(): esto
                // corre en Update() para cada soldado, cada frame -- con
                // el registro ya cacheado en Soldier no hace falta pagar
                // GetComponent otra vez para lo mismo.
                var brain = soldier.Brain;
                // Pedido explicito: la linea roja tiene que verse en cuanto
                // el soldado TIENE un enemigo trabado (Chase/MovingToAttackOrder
                // ya persiguen a un target concreto, no solo Attack cuando ya
                // esta disparando) -- antes solo se dibujaba con el gatillo
                // apretado, y para entonces el jugador ya no llegaba a ver
                // "a quien" estaba mirando el soldado un instante antes.
                bool hasEnemyLocked = brain != null && brain.CurrentTarget != null &&
                    (brain.State == AiState.Attack || brain.State == AiState.Chase || brain.State == AiState.MovingToAttackOrder) &&
                    soldier.gameObject.activeInHierarchy;

                if (!hasEnemyLocked)
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

        void RemoveAllLines()
        {
            foreach (var actorId in new List<int>(lines.Keys)) RemoveLine(actorId);
        }

        void RemoveLine(int actorId)
        {
            if (!lines.TryGetValue(actorId, out var lr)) return;
            lines.Remove(actorId);
            if (lr == null) return;
            
            var mat = lr.material;
            if (Application.isPlaying)
            {
                if (mat != null) Destroy(mat);
                Destroy(lr.gameObject);
            }
            else
            {
                if (mat != null) DestroyImmediate(mat);
                DestroyImmediate(lr.gameObject);
            }
        }

        static LineRenderer CreateLine()
        {
            var go = new GameObject("AttackLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.05f;
            lr.useWorldSpace = true;
            lr.material = SafeMaterial.Create(LineColor);
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
            var mat = lr.material;
            if (Application.isPlaying) { if (mat != null) Destroy(mat); Object.Destroy(lr.gameObject); }
            else { if (mat != null) DestroyImmediate(mat); Object.DestroyImmediate(lr.gameObject); }
        }
    }
}
