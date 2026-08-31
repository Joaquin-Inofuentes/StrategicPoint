using System;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // AiStateChangedEvent se publicaba en cada cambio pero nadie lo consumia
    // visualmente del lado propio: el jugador no sabia si un soldado estaba
    // patrullando, persiguiendo o cumpliendo una orden sin ir a leerlo al
    // roster. Hermano de EnemyAlertIndicatorView, pero para el equipo
    // propio y con la paleta completa de estados, no solo alerta/no.
    public class SquadStateIndicatorView : MonoBehaviour
    {
        Soldier soldier;
        // Serializado a proposito: el hijo SquadStateIndicator es un
        // GameObject real que sobrevive un domain reload (recompilar un
        // script durante Play mode), pero un campo privado sin serializar
        // volvia a null y OnEnable construia un SEGUNDO cubo encima del que
        // ya estaba. Con cincuenta soldados eso son cincuenta cubos y
        // cincuenta materiales de mas por cada recarga.
        [SerializeField] Renderer markerRenderer;
        // El material lo creamos nosotros en runtime: hay que guardarlo
        // para poder liberarlo en OnDestroy (ver abajo).
        [SerializeField] Material ownedMaterial;
        IDisposable sub;

        const string MarkerName = "SquadStateIndicator";

        static readonly Color IdleColor = new Color(0.55f, 0.58f, 0.62f);
        static readonly Color OrderColor = new Color(0.35f, 0.75f, 0.95f);
        static readonly Color CombatColor = new Color(0.95f, 0.35f, 0.2f);
        static readonly Color DeadColor = new Color(0.2f, 0.2f, 0.2f);

        // A partir de esta distancia de camara el indicador se apaga: con
        // cincuenta soldados en pantalla, cincuenta esferas lejanas son
        // ruido visual y coste de dibujado sin informacion legible.
        const float VisibleDistance = 55f;
        const float LodCheckInterval = 0.25f;
        float lodTimer;

        void OnEnable()
        {
            if (soldier == null) soldier = GetComponent<Soldier>();
            if (soldier == null || soldier.Team != TeamId.Player) { enabled = false; return; }

            // Doble red: el campo serializado cubre el domain reload, y la
            // busqueda por nombre cubre cualquier via en la que el campo se
            // pierda pero el hijo siga colgado (o quedarian dos cubos).
            if (markerRenderer == null)
            {
                var existing = transform.Find(MarkerName);
                if (existing != null)
                {
                    markerRenderer = existing.GetComponent<Renderer>();
                    // Ese hijo lo creo BuildMarker, asi que su sharedMaterial
                    // tambien es nuestro: readoptarlo evita que quede huerfano.
                    if (markerRenderer != null && ownedMaterial == null)
                        ownedMaterial = markerRenderer.sharedMaterial;
                }
            }
            if (markerRenderer == null) BuildMarker();

            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<AiStateChangedEvent>(OnStateChanged);
        }

        void OnDisable() => sub?.Dispose();

        // Destruir el GameObject NO libera el Material creado en runtime:
        // queda huerfano hasta cambiar de escena. Mismo criterio que ya
        // aplica KillFeedbackDirector.SilhouetteFlash con su silueta.
        void OnDestroy()
        {
            if (ownedMaterial == null) return;
            // El constructor de escena agrega este componente en Edit mode,
            // donde Destroy no esta permitido (mismo patron que SelectionRingFx).
            if (Application.isPlaying) Destroy(ownedMaterial);
            else DestroyImmediate(ownedMaterial);
            ownedMaterial = null;
        }

        void BuildMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = MarkerName;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.SetParent(transform, false);
            // Mas alto que el de enemigo (1.4) para que, cuando un aliado y
            // un enemigo se cruzan, no queden los dos a la misma altura.
            go.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            go.transform.localScale = Vector3.one * 0.16f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            markerRenderer = go.GetComponent<MeshRenderer>();
            ownedMaterial = new Material(shader) { color = IdleColor };
            markerRenderer.sharedMaterial = ownedMaterial;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (markerRenderer == null) return;
            lodTimer -= Time.deltaTime;
            if (lodTimer > 0f) return;
            lodTimer = LodCheckInterval;

            var cam = Camera.main;
            if (cam == null) return;
            bool visible = Vector3.Distance(cam.transform.position, transform.position) <= VisibleDistance;
            if (markerRenderer.enabled != visible) markerRenderer.enabled = visible;
        }

        void OnStateChanged(AiStateChangedEvent evt)
        {
            if (soldier == null || evt.ActorId != soldier.Id || markerRenderer == null) return;
            markerRenderer.sharedMaterial.color = evt.NewState switch
            {
                "Chase" or "Attack" or "MovingToAttackOrder" => CombatColor,
                "MovingToOrder" => OrderColor,
                "Dead" => DeadColor,
                _ => IdleColor,
            };
        }
    }
}
