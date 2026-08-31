using System;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Marca sobre la cabeza del enemigo que dice si ya te detecto o no.
    // Sin esto no habia forma de saber, antes de que empiece a disparar,
    // si un enemigo estaba patrullando tranquilo o ya venia directo hacia
    // vos -- la decision de atacar o rodear depende de esa diferencia.
    public class EnemyAlertIndicatorView : MonoBehaviour
    {
        Soldier soldier;
        // Serializado a proposito: el hijo AlertIndicator es un GameObject
        // real que sobrevive un domain reload (recompilar un script durante
        // Play mode), pero un campo privado sin serializar volvia a null y
        // OnEnable construia una SEGUNDA esfera encima de la que ya estaba.
        // Con cincuenta enemigos eso son cincuenta esferas y cincuenta
        // materiales de mas por cada recarga.
        [SerializeField] Renderer markerRenderer;
        // El material lo creamos nosotros en runtime: hay que guardarlo
        // para poder liberarlo en OnDestroy (ver abajo).
        [SerializeField] Material ownedMaterial;
        IDisposable sub;

        const string MarkerName = "AlertIndicator";

        static readonly Color AlertColor = new Color(0.95f, 0.2f, 0.15f);
        static readonly Color UnawareColor = new Color(0.6f, 0.6f, 0.65f, 0.5f);

        // A partir de esta distancia de camara el indicador se apaga: con
        // cincuenta enemigos en pantalla, cincuenta esferas lejanas son
        // ruido visual y coste de dibujado sin informacion legible. El
        // chequeo de distancia es lo caro, asi que se throttlea; el
        // parpadeo, que si tiene que verse fluido, sigue por frame.
        const float VisibleDistance = 55f;
        const float LodCheckInterval = 0.25f;
        float lodTimer;

        void OnEnable()
        {
            if (soldier == null) soldier = GetComponent<Soldier>();
            if (soldier == null || soldier.Team != TeamId.Enemy) { enabled = false; return; }

            // Doble red: el campo serializado cubre el domain reload, y la
            // busqueda por nombre cubre cualquier via en la que el campo se
            // pierda pero el hijo siga colgado (o quedarian dos esferas).
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = MarkerName;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            go.transform.localScale = Vector3.one * 0.18f;

            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit");
            markerRenderer = go.GetComponent<MeshRenderer>();
            ownedMaterial = new Material(shader) { color = UnawareColor };
            markerRenderer.sharedMaterial = ownedMaterial;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        bool alerted;

        void OnStateChanged(AiStateChangedEvent evt)
        {
            if (soldier == null || evt.ActorId != soldier.Id || markerRenderer == null) return;
            alerted = evt.NewState == "Chase" || evt.NewState == "Attack" || evt.NewState == "MovingToAttackOrder";
            markerRenderer.sharedMaterial.color = alerted ? AlertColor : UnawareColor;
        }

        // Por debajo de este umbral la marca parpadea: es la respuesta a la
        // decision mas frecuente del combate -- gastar otra bala en este o
        // pasar al siguiente. Antes no habia forma de saberlo sin apuntarle
        // y leer el panel.
        public const float LowHealthFraction = 0.3f;
        static readonly Color DyingColor = new Color(1f, 0.95f, 0.3f);
        public bool IsBlinkingLowHealth { get; private set; }

        void Update()
        {
            if (markerRenderer == null || soldier == null || soldier.Health == null) return;

            // Parte cara (distancia a camara + lectura de vida): a intervalo
            // fijo, no por frame. Con cincuenta enemigos esto era el grueso
            // del coste y la vida no cambia lo bastante rapido como para
            // que se note el retardo de un cuarto de segundo.
            lodTimer -= Time.deltaTime;
            if (lodTimer <= 0f)
            {
                lodTimer = LodCheckInterval;

                var cam = Camera.main;
                if (cam != null)
                {
                    bool visible = Vector3.Distance(cam.transform.position, transform.position) <= VisibleDistance;
                    if (markerRenderer.enabled != visible) markerRenderer.enabled = visible;
                }

                float frac = soldier.Health.MaxHealth > 0 ? (float)soldier.Health.Current / soldier.Health.MaxHealth : 1f;
                bool dying = soldier.Health.IsAlive && frac <= LowHealthFraction;
                if (IsBlinkingLowHealth != dying)
                {
                    IsBlinkingLowHealth = dying;
                    // Al salir del parpadeo hay que devolver el color del
                    // estado, o queda pegado en el ultimo tono del parpadeo.
                    if (!dying) markerRenderer.sharedMaterial.color = alerted ? AlertColor : UnawareColor;
                }
            }

            // El parpadeo si es por frame: throttlearlo a 4 Hz lo convertiria
            // en un titileo a saltos y se pierde el efecto. Los que no
            // parpadean o estan apagados por LOD no escriben color nunca.
            if (!IsBlinkingLowHealth || !markerRenderer.enabled) return;

            float k = (Mathf.Sin(Time.time * 12f) + 1f) * 0.5f;
            markerRenderer.sharedMaterial.color = Color.Lerp(alerted ? AlertColor : UnawareColor, DyingColor, k);
        }
    }
}
