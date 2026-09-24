using UnityEngine;

namespace SP.Presentation
{
    // Pedido explicito: "que no aparezca texto sino arriba un rombo pero con
    // imagen de un engranaje para saber que es interactuable -- enemigos,
    // aliados, todo -- y ese rombo resalte al apuntarle". Reemplaza a TODOS
    // los carteles de texto que aparecian sobre la mira al apuntar algo con
    // lo que se puede interactuar (PlayerInputDriver.ActualizarPromptContextual
    // ya no escribe texto: llama a Mostrar/Ocultar de aca).
    //
    // Es UNA sola instancia reciclada (no una por objetivo): en un momento
    // dado el jugador apunta a lo sumo una cosa, asi que no hace falta un
    // marcador por soldado/obstaculo/vehiculo como si tienen los locators de
    // equipo -- alcanza con reposicionar el mismo rombo cada frame sobre lo
    // que este apuntado ahora.
    public class InteractGearMarker : MonoBehaviour
    {
        static InteractGearMarker instancia;

        GameObject marcador;
        Material materialInterior;
        float pulso;

        const float TamanoBorde = 0.5f;
        const float TamanoInterior = 0.34f;

        // Dorado = "el radial tiene una accion lista ahora" (demoler, curar,
        // reanimar, subir, atacar...). Gris claro = solo informativo (ej. un
        // aliado sano que simplemente te sigue): sigue marcando "esto es
        // interactuable" sin prometer una accion inmediata.
        static readonly Color ColorDestacado = new Color(1f, 0.82f, 0.15f, 1f);
        static readonly Color ColorInformativo = new Color(0.82f, 0.85f, 0.9f, 1f);
        static readonly Color ColorBorde = Color.white;

        static InteractGearMarker EnsureInstancia()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("InteractGearMarker");
            instancia = go.AddComponent<InteractGearMarker>();
            instancia.Construir();
            return instancia;
        }

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload: sin
        // este reset, la instancia de una sesion de Play anterior quedaria
        // referenciada como "fake null" si la escena la destruyo entre medio.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearInstancia() => instancia = null;

        void Construir()
        {
            marcador = new GameObject("Rombo");
            marcador.transform.SetParent(transform, false);

            DiamondGizmo.CrearCara("Borde", marcador.transform, TamanoBorde, DiamondGizmo.NuevoMaterial(ColorBorde));

            materialInterior = DiamondGizmo.NuevoMaterialConEngranaje(ColorInformativo);
            var interior = DiamondGizmo.CrearCara("Interior", marcador.transform, TamanoInterior, materialInterior);
            interior.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            marcador.SetActive(false);
        }

        // Llamado cada frame que hay algo interactuable bajo la mira.
        // ancla = punto del mundo ARRIBA del objetivo (ya con el offset de
        // altura resuelto por el llamador, que es quien sabe si es un
        // soldado, un vehiculo o un punto de impacto en un muro).
        public static void Mostrar(Vector3 ancla, bool destacado)
        {
            var m = EnsureInstancia();
            if (RomboVisibilidad.Suprimidos) { m.marcador.SetActive(false); return; }

            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) { m.marcador.SetActive(false); return; }

            m.transform.position = ancla;
            m.marcador.transform.rotation = cam.transform.rotation;

            // Un pulso de escala suave (no de color: el color ya distingue
            // destacado/informativo) para que "resalte al apuntarle" se lea
            // como algo vivo, no como un icono estatico mas del HUD.
            m.pulso += Time.unscaledDeltaTime * (destacado ? 6f : 3f);
            float k = 1f + Mathf.Sin(m.pulso) * (destacado ? 0.08f : 0.04f);
            m.marcador.transform.localScale = Vector3.one * k;

            var color = destacado ? ColorDestacado : ColorInformativo;
            if (m.materialInterior.color != color)
            {
                m.materialInterior.color = color;
                if (m.materialInterior.HasProperty("_BaseColor")) m.materialInterior.SetColor("_BaseColor", color);
            }

            m.marcador.SetActive(true);
        }

        public static void Ocultar()
        {
            if (instancia != null && instancia.marcador != null) instancia.marcador.SetActive(false);
        }

        void OnDestroy()
        {
            if (materialInterior != null)
            {
                if (Application.isPlaying) Destroy(materialInterior);
                else DestroyImmediate(materialInterior);
                materialInterior = null;
            }
            if (instancia == this) instancia = null;
        }
    }
}
