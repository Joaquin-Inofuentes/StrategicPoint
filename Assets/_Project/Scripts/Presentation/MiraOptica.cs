using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // La optica del arma en primera persona.
    //
    // El zoom que ya existia angosta el FOV de la camara principal (60 -> 25):
    // eso acerca TODA la pantalla, que es lo contrario de mirar por una
    // mira. Una optica de verdad amplia SOLO lo que se ve por el tubo, y
    // el resto de la pantalla se queda como esta.
    //
    // Por eso hay una segunda camara: mira hacia adelante desde el mismo
    // lugar, con la mitad del FOV de la principal, y lo que ve se dibuja
    // adentro del tubo a traves de una RenderTexture.
    //
    // El rifle y el pesado llevan tubo (amplian). La pistola lleva un cubo
    // que marca el objetivo y no amplia nada: una mira telescopica en una
    // pistola no tiene sentido, y el plan pide justamente esa diferencia.
    public class MiraOptica : MonoBehaviour
    {
        // Cuanto amplia respecto de la camara principal. La principal ya
        // esta en 25 grados al hacer zoom, asi que la optica queda en 11:
        // se nota que es otra imagen y no la misma un poco mas cerca.
        public const float FactorDeAumento = 0.45f;
        public const int LadoDeLaTextura = 256;
        // El visor del arma cuelga a 0,65 m de la camara. Recortando por
        // delante de eso, la optica no se filma a si misma.
        public const float RecorteCercano = 1f;

        public Camera Optica { get; private set; }
        public RenderTexture Textura { get; private set; }
        public GameObject Tubo { get; private set; }
        public WeaponKind Forma { get; private set; }
        public bool Amplia { get; private set; }

        MeshRenderer tuboRenderer;
        Material materialDelTubo;

        public static MiraOptica Asegurar(Transform camaraPrincipal, Transform visorDelArma)
        {
            if (camaraPrincipal == null || visorDelArma == null) return null;
            var ya = camaraPrincipal.GetComponentInChildren<MiraOptica>(true);
            if (ya != null) return ya;

            var go = new GameObject("MiraOptica");
            go.transform.SetParent(camaraPrincipal, false);
            var mira = go.AddComponent<MiraOptica>();
            mira.Construir(visorDelArma);
            return mira;
        }

        void Construir(Transform visorDelArma)
        {
            Textura = new RenderTexture(LadoDeLaTextura, LadoDeLaTextura, 16)
            {
                name = "TexturaDeLaMira",
                hideFlags = HideFlags.HideAndDontSave,
            };
            Textura.Create();

            var camGO = new GameObject("CamaraDeLaMira");
            camGO.transform.SetParent(transform, false);
            Optica = camGO.AddComponent<Camera>();
            Optica.targetTexture = Textura;
            // Detras de la principal en el orden de dibujado: la optica es
            // una textura, no lo que se ve en pantalla.
            Optica.depth = -10;
            Optica.nearClipPlane = RecorteCercano;
            Optica.fieldOfView = 25f * FactorDeAumento;

            // El tubo cuelga del VISOR, no de la camara: asi acompaña el
            // retroceso del arma en vez de quedarse flotando quieto
            // mientras el arma se sacude.
            Tubo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Tubo.name = "TuboDeLaMira";
            var col = Tubo.GetComponent<Collider>();
            if (col != null) DestroyInmediatoOTardio(col);
            Tubo.transform.SetParent(visorDelArma, false);
            tuboRenderer = Tubo.GetComponent<MeshRenderer>();

            // Shader propio y no el del arma: ese declara solo _BaseColor
            // y su vertex ni lee UVs, asi que asignarle mainTexture no hace
            // nada y el tubo salia blanco.
            var shader = Shader.Find("SP/MiraOptica");
            materialDelTubo = shader != null ? new Material(shader) : SafeMaterial.Create(Color.white);
            materialDelTubo.hideFlags = HideFlags.HideAndDontSave;
            // Mismo criterio que el visor del arma: despues de la geometria
            // opaca, pero por delante en profundidad. Una mira que se mete
            // adentro de la pared no sirve de nada.
            materialDelTubo.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            tuboRenderer.sharedMaterial = materialDelTubo;

            Configurar(WeaponKind.Rifle);
            Mostrar(false);
        }

        static void DestruirloYa(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        static void DestroyInmediatoOTardio(Object o) => DestruirloYa(o);

        // La forma del arma decide si hay aumento o no.
        public void Configurar(WeaponKind arma)
        {
            if (Tubo == null) return;
            Forma = arma;
            Amplia = arma != WeaponKind.Pistol;

            // Un cilindro tiene su eje en Y, asi que para que sea un tubo
            // apuntando hacia adelante hay que acostarlo 90 grados.
            var malla = Tubo.GetComponent<MeshFilter>();
            var primitivo = GameObject.CreatePrimitive(Amplia ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            malla.sharedMesh = primitivo.GetComponent<MeshFilter>().sharedMesh;
            DestruirloYa(primitivo);

            Tubo.transform.localPosition = new Vector3(0f, 0.9f, 0.1f);
            Tubo.transform.localRotation = Amplia ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            // La escala es LOCAL al visor, que ya viene achatado en X/Y y
            // largo en Z: sin dividir por la escala del padre, el tubo sale
            // deformado siguiendo la forma del arma.
            var padre = Tubo.transform.parent.localScale;
            var deseada = Amplia ? new Vector3(0.05f, 0.04f, 0.05f) : new Vector3(0.04f, 0.04f, 0.04f);
            Tubo.transform.localScale = new Vector3(
                deseada.x / Mathf.Max(0.0001f, padre.x),
                deseada.y / Mathf.Max(0.0001f, padre.y),
                deseada.z / Mathf.Max(0.0001f, padre.z));

            // La pistola no amplia: su cubo es una marca de color, no una
            // ventana a otra imagen.
            materialDelTubo.mainTexture = Amplia ? Textura : null;
            if (!Amplia) materialDelTubo.color = new Color(0.95f, 0.35f, 0.2f);
            else materialDelTubo.color = Color.white;
        }

        // Solo se ve mientras se apunta: un tubo permanente encima del arma
        // tapa pantalla todo el tiempo a cambio de nada.
        public void Mostrar(bool apuntando)
        {
            if (Tubo != null) Tubo.SetActive(apuntando);
            // La camara de la optica se apaga cuando no se usa: es un
            // render completo de la escena por frame, y pagarlo mientras
            // nadie mira por la mira no tiene sentido.
            if (Optica != null) Optica.enabled = apuntando && Amplia;
        }

        // MEDIDO en Play: con el FOV actual de la camara principal la
        // optica quedaba en 27 grados contra 25 de la principal, o sea MAS
        // ABIERTA -- una "mira" que aleja. Pasa porque el FOV principal
        // tarda varios frames en bajar de 60 a 25 y esto corre en Update
        // mientras el lerp vive en LateUpdate: siempre se lee el valor del
        // frame anterior. Tomando el MENOR entre el actual y el destino
        // del zoom, la optica nunca puede ser mas abierta que la pantalla.
        public void Seguir(Camera principal, float fovObjetivo)
        {
            if (Optica == null || principal == null) return;
            Optica.fieldOfView = Mathf.Min(principal.fieldOfView, fovObjetivo) * FactorDeAumento;
            Optica.transform.position = principal.transform.position;
            Optica.transform.rotation = principal.transform.rotation;
        }

        void OnDestroy()
        {
            if (Textura != null) { Textura.Release(); DestruirloYa(Textura); }
            if (materialDelTubo != null) DestruirloYa(materialDelTubo);
        }
    }
}
