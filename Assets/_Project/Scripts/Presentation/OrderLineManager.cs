using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Combat;
using SP.CameraSystem;

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
        // Blanca (pedido explicito: "quiero que sea blanca no gris o negra"), sin luz: ver SafeMaterial.CreateLinea.
        static readonly Color LineColor = new Color(1f, 1f, 1f, 0.9f);

        readonly Dictionary<int, LineRenderer> lines = new Dictionary<int, LineRenderer>();

        void Update()
        {
            foreach (var soldier in ActorRegistry.All)
            {
                if (soldier == null) continue;
                var brain = soldier.Brain;
                var destination = brain != null ? brain.CurrentOrderDestination : null;

                // Destino de un ENEMIGO: pedido explicito "en RTS no deberian verse las lineas de destino de los
                // enemigos". Solo se ve con el radial abierto y en primera persona (dar una orden mirando el terreno).
                bool enRts = CameraRig.Instance != null && CameraRig.Instance.Mode == ControlMode.Rts;
                bool radialAbierto = SP.UI.MenuDeOrdenes.Activo != null && SP.UI.MenuDeOrdenes.Activo.Abierto;
                bool ocultoPorSerEnemigo = soldier.Team == TeamId.Enemy && (enRts || !radialAbierto);

                if (!destination.HasValue || !soldier.gameObject.activeInHierarchy || ocultoPorSerEnemigo)
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
            if (lr == null) return;

            var mat = Application.isPlaying ? lr.material : lr.sharedMaterial;   // .material en Edit instancia y filtra un material
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
            var go = new GameObject("OrderLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.1f;   // 0.04 era invisible a la altura de la camara RTS
            lr.useWorldSpace = true;
            lr.material = SafeMaterial.CreateLinea(Color.white);
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
            var mat = Application.isPlaying ? lr.material : lr.sharedMaterial;   // .material en Edit instancia y filtra un material
            if (Application.isPlaying) { if (mat != null) Destroy(mat); Object.Destroy(lr.gameObject); }
            else { if (mat != null) DestroyImmediate(mat); Object.DestroyImmediate(lr.gameObject); }
        }
    }
}
