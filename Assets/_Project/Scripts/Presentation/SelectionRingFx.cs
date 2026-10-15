using UnityEngine;
using SP.Actors;

namespace SP.Presentation
{
    // Anillo (cilindro chato) que sigue a un soldado seleccionado y pulsa
    // -- se achica y se agranda en loop con un lerp simple -- para marcar
    // a simple vista quién está elegido en la vista RTS.
    public class SelectionRingFx : MonoBehaviour
    {
        public Transform Target;
        [SerializeField] float baseRadius = 0.75f;
        [SerializeField] float pulseAmount = 0.06f;
        [SerializeField] float pulseSpeed = 2.2f;
        [SerializeField] float groundHeight = 0.03f;

        // Si se trackea un soldado (no un vehiculo), el anillo deja de
        // ser todo del mismo amarillo fijo: se tiñe segun su vida, los
        // mismos umbrales que el resto del HUD. Antes, seleccionar a un
        // herido y a uno sano se veia identico -- habia que abrir el
        // roster para saber a cual convenia retirar.
        [SerializeField] Soldier trackedSoldier;
        MeshRenderer ringRenderer;

        public void TrackHealth(Soldier soldier)
        {
            trackedSoldier = soldier;
            if (ringRenderer == null) ringRenderer = GetComponent<MeshRenderer>();
        }

        static Material sharedMaterial;
        static MaterialPropertyBlock propertyBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                    sharedMaterial = SafeMaterial.CreateShared();
                return sharedMaterial;
            }
        }

        // Se escriben las dos propiedades porque el shader puede ser el de
        // URP (_BaseColor) o el Standard de fallback (_Color).
        public void SetColor(Color c)
        {
            if (ringRenderer == null) ringRenderer = GetComponent<MeshRenderer>();
            if (ringRenderer == null) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            ringRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            propertyBlock.SetColor(ColorId, c);
            ringRenderer.SetPropertyBlock(propertyBlock);
        }

        // Contador de solo lectura para verificacion (item 232/5-companion):
        // solo sube cuando el pool del manager esta vacio y hay que crear
        // un anillo de verdad. Sirve para confirmar que, tras el llenado
        // inicial, cambiar de seleccion reusa en vez de instanciar.
        public static int SpawnCount { get; private set; }
        public static void ResetSpawnCount() => SpawnCount = 0;

        public static SelectionRingFx Spawn(Transform target, Color color, float radius = 0.75f)
        {
            SpawnCount++;
            // Bug #178 ("quiero que los seleccionados tengan un circulo en su base"): el cilindro chato con material Lit quedaba negro de noche y no se
            // veia. Ahora es el aro plano sin iluminacion de AnilloDeAviso (transparente, no lo afecta la luz), tenido por vida del soldado.
            var go = new GameObject("SelectionRing");
            go.AddComponent<MeshFilter>().sharedMesh = SP.Operacion.AnilloDeAviso.MallaPlana();
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = SP.Operacion.AnilloDeAviso.MaterialDeAro();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var fx = go.AddComponent<SelectionRingFx>();
            fx.SetColor(color);
            fx.Target = target;
            fx.baseRadius = radius;
            return fx;
        }

        // Destello de "orden recibida": el anillo se agranda y se aclara
        // un instante. Se implementa como una ventana de tiempo leida en
        // LateUpdate (no una corrutina) para que conviva con el pulso y
        // el tinte por vida que ese mismo LateUpdate ya recalcula cada
        // frame -- una corrutina que pintara el color seria pisada.
        // Bug #178: por encima de los decals y el asfalto que el piso visible lleva sobre el collider (a 0,03 el aro quedaba tapado).
        public float ElevacionExtra;
        float proximaMedidaDePiso, pisoY = 0.03f;
        float ackFlashUntil;
        const float AckFlashDuration = 0.45f;
        public void FlashAcknowledge() => ackFlashUntil = Time.time + AckFlashDuration;

        void LateUpdate()
        {
            if (Target == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            // A nivel del piso (no a la altura del centro del soldado, que
            // lo dejaba envolviendo la mitad del cubo en vez de estar abajo).
            // Bug #178: el anillo a y fija (0,03) quedaba tapado por pisos elevados (patio del centro de datos, plataformas): se apoya en el piso real.
            if (Time.time >= proximaMedidaDePiso)
            {
                proximaMedidaDePiso = Time.time + 0.25f + (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this) & 7) * 0.01f;
                pisoY = SP.Operacion.AnilloDeAviso.AlturaDelPisoEn(Target.position) + groundHeight + ElevacionExtra;
            }
            transform.position = new Vector3(Target.position.x, pisoY, Target.position.z);

            float k = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float radius = (baseRadius + pulseAmount * k) * 2f; // cylinder scale = diámetro

            float ackRemaining = ackFlashUntil - Time.time;
            float ack = ackRemaining > 0f ? ackRemaining / AckFlashDuration : 0f;
            radius *= 1f + ack * 0.3f;
            transform.localScale = new Vector3(radius, 1f, radius);

            if (trackedSoldier != null && trackedSoldier.Health != null)
            {
                if (ringRenderer == null) ringRenderer = GetComponent<MeshRenderer>();
                if (ringRenderer != null && trackedSoldier.Health.MaxHealth > 0)
                {
                    float frac = (float)trackedSoldier.Health.Current / trackedSoldier.Health.MaxHealth;
                    Color c = frac > 0.6f ? new Color(0.35f, 0.85f, 0.4f)
                        : frac > 0.25f ? new Color(0.95f, 0.8f, 0.25f)
                        : new Color(0.95f, 0.25f, 0.2f);
                    // MaterialPropertyBlock y no sharedMaterial.color: el
                    // material ahora es compartido entre todos los anillos,
                    // asi que escribirle el color pintaria a TODOS igual.
                    SetColor(ack > 0f ? Color.Lerp(c, Color.white, ack) : c);
                }
            }
        }
    }
}
