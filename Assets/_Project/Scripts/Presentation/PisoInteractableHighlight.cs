using UnityEngine;
using UnityEngine.Rendering;

namespace SP.Presentation
{
    // Pedido explicito: "los interactuables en el piso resalten con un
    // cilindro para saber que es interactuable, solamente al apuntarle".
    // Un anillo chato (cilindro aplastado) bajo el objeto, que solo se
    // enciende mientras la mira del jugador cae ADENTRO de un cono angosto
    // apuntando hacia el (no por simple cercania: eso ya lo resuelve la
    // recogida automatica de cada pickup). Lo usan WeaponPickup y
    // CajaDeSuministros -- los dos "interactuables de piso" del juego.
    public class PisoInteractableHighlight : MonoBehaviour
    {
        Transform objetivo;
        GameObject anillo;
        float yOffsetAlPiso;

        // Opcional: condicion extra ademas de "la mira lo tiene encima" --
        // CajaDeSuministros la usa para que el anillo no aparezca mientras la
        // caja esta vacia/reponiendose (Disponible == false), que es cuando
        // en realidad no hay nada con que interactuar.
        public System.Func<bool> CondicionExtra;

        const float DistanciaMaxima = 24f;
        const float AnguloMaximo = 3.5f;
        static readonly Color ColorResalte = new Color(1f, 0.85f, 0.2f, 1f);

        public static PisoInteractableHighlight Agregar(Transform objetivo, float radio, float yOffsetAlPiso = -0.4f)
        {
            var go = new GameObject("ResaltadoInteractuable");
            var h = go.AddComponent<PisoInteractableHighlight>();
            h.objetivo = objetivo;
            h.yOffsetAlPiso = yOffsetAlPiso;
            h.Construir(radio);
            return h;
        }

        void Construir(float radio)
        {
            anillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            anillo.name = "Anillo";
            var col = anillo.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            anillo.transform.SetParent(transform, false);
            anillo.transform.localScale = new Vector3(radio, 0.02f, radio);

            var rend = anillo.GetComponent<MeshRenderer>();
            rend.sharedMaterial = DiamondGizmo.NuevoMaterial(ColorResalte);
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            anillo.SetActive(false);
        }

        void Update()
        {
            if (objetivo == null) { Destroy(gameObject); return; }
            transform.position = new Vector3(objetivo.position.x, objetivo.position.y + yOffsetAlPiso, objetivo.position.z);

            var cam = SP.Core.CamaraPrincipal.Actual;
            bool visible = false;
            if (cam != null)
            {
                var hacia = objetivo.position - cam.transform.position;
                float dist = hacia.magnitude;
                visible = dist > 0.05f && dist <= DistanciaMaxima && Vector3.Angle(cam.transform.forward, hacia) <= AnguloMaximo;
            }
            if (visible && CondicionExtra != null) visible = CondicionExtra();
            if (anillo != null) anillo.SetActive(visible);
        }

        void OnDestroy() { if (anillo != null) { if (Application.isPlaying) Destroy(anillo); else DestroyImmediate(anillo); } }
    }
}
