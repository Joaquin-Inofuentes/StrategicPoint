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
    // puede responder. Mismo patron que AttackLineManager: revisa
    // todos cada frame y crea/reposiciona/borra las lineas solo.
    //
    // Bug #068: la linea blanca dura 3 s (no toda la orden): alfa 1 hasta los 2.5 s, se desvanece hasta 0 a los 3.0 s y despues se
    // oculta aunque el soldado siga en camino. El reloj es por soldado y arranca de nuevo cuando cambia su destino.
    public class OrderLineManager : MonoBehaviour
    {
        // Blanca (pedido explicito: "quiero que sea blanca no gris o negra"), sin luz: ver SafeMaterial.CreateLinea.
        static readonly Color LineColor = new Color(1f, 1f, 1f, 0.9f);

        public const float SegundosCompleta = 2.5f;   // alfa 1
        public const float SegundosTotales = 3.0f;    // desde aca esta oculta

        sealed class Linea
        {
            public LineRenderer lr;
            public Vector3 destino;
            public float t0;
            public float alfa = 1f;
            public bool oculta;
        }

        readonly Dictionary<int, Linea> lines = new Dictionary<int, Linea>();
        static OrderLineManager instancia;
        public static OrderLineManager Instancia => instancia;

        void OnEnable() { instancia = this; }
        void OnDisable() { if (instancia == this) instancia = null; }

        // Para la suite: estado de la linea de un soldado (false = no hay linea registrada).
        public bool Estado(int soldadoId, out float alfa, out bool activa, out float edad)
        {
            alfa = 0f; activa = false; edad = 0f;
            if (!lines.TryGetValue(soldadoId, out var l) || l == null) return false;
            alfa = l.alfa;
            activa = l.lr != null && l.lr.gameObject.activeSelf;
            edad = Time.time - l.t0;
            return true;
        }

        public static float AlfaSegunEdad(float edad)
        {
            if (edad <= SegundosCompleta) return 1f;
            if (edad >= SegundosTotales) return 0f;
            return 1f - (edad - SegundosCompleta) / (SegundosTotales - SegundosCompleta);
        }

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

                if (!lines.TryGetValue(soldier.Id, out var l) || l == null || l.lr == null)
                {
                    l = new Linea { lr = CreateLine(), t0 = Time.time, destino = destination.Value };
                    lines[soldier.Id] = l;
                }

                // Destino nuevo: el reloj de los 3 s vuelve a empezar y la linea reaparece.
                var dd = destination.Value - l.destino; dd.y = 0f;
                if (dd.sqrMagnitude > 0.05f * 0.05f)
                {
                    l.destino = destination.Value;
                    l.t0 = Time.time;
                    l.oculta = false;
                }

                float edad = Time.time - l.t0;
                l.alfa = AlfaSegunEdad(edad);
                if (l.alfa <= 0f)
                {
                    if (!l.oculta) { l.oculta = true; l.lr.gameObject.SetActive(false); }
                    continue;
                }
                if (l.oculta || !l.lr.gameObject.activeSelf) { l.oculta = false; l.lr.gameObject.SetActive(true); }

                var c = LineColor; c.a = LineColor.a * l.alfa;
                l.lr.startColor = c;
                l.lr.endColor = c;
                l.lr.SetPosition(0, soldier.transform.position + Vector3.up * 0.3f);
                l.lr.SetPosition(1, destination.Value + Vector3.up * 0.05f);
            }
        }

        void RemoveLine(int actorId)
        {
            if (!lines.TryGetValue(actorId, out var l)) return;
            lines.Remove(actorId);
            var lr = l != null ? l.lr : null;
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
