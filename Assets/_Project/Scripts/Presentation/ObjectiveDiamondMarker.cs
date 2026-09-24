using UnityEngine;

namespace SP.Presentation
{
    // Pedido explicito: el mismo gizmo de rombo (fondo blanco + interior de
    // color) que usan enemigos/aliados, pero AMARILLO y sobre el objetivo
    // actual de la mision -- y "quiero que el rombo del objetivo destaque
    // mucho": tamano mayor que el de enemigo/aliado y siempre visible
    // mientras haya mision en curso (no se apaga por angulo de mira como el
    // de las unidades: el objetivo es justo lo que el jugador tiene que
    // poder encontrar desde lejos).
    public class ObjectiveDiamondMarker : MonoBehaviour
    {
        GameObject marcador;
        Material materialInterior;

        const string MarkerName = "LocatorRomboObjetivo";
        // BUG REAL: a 3.2 m quedaba pegado a la etiqueta de texto del
        // TutorialBeacon ("CENTRO"/"HELICOPTERO", ver MisionDirector) que
        // vive casi en la misma altura (3.4 m) y en el MISMO punto -- las
        // dos cosas se superponian y el rombo se perdia detras/al lado del
        // texto y la columna traslucida del beacon. Ahora flota bien por
        // encima de esa etiqueta, como un marcador propio y separado.
        const float Altura = 6f;
        const float TamanoBorde = 1.05f;
        const float TamanoInterior = 0.72f;
        const float DistanciaVisible = 220f; // bien mas lejos que enemigo/aliado: es el faro del nivel

        static readonly Color ColorObjetivo = DiamondGizmo.ColorObjetivo;
        static readonly Color ColorBorde = Color.white;

        System.Func<Vector3> obtenerPunto;

        public static ObjectiveDiamondMarker Crear(System.Func<Vector3> obtenerPunto)
        {
            var go = new GameObject("ObjectiveDiamondMarker");
            var m = go.AddComponent<ObjectiveDiamondMarker>();
            m.obtenerPunto = obtenerPunto;
            return m;
        }

        void OnEnable() { if (marcador == null) Construir(); }
        void OnDisable() { if (marcador != null) marcador.SetActive(false); }

        void OnDestroy()
        {
            if (materialInterior == null) return;
            if (Application.isPlaying) Destroy(materialInterior);
            else DestroyImmediate(materialInterior);
            materialInterior = null;
        }

        void Construir()
        {
            marcador = new GameObject(MarkerName);
            marcador.transform.SetParent(transform, false);

            DiamondGizmo.CrearCara("Borde", marcador.transform, TamanoBorde, DiamondGizmo.NuevoMaterial(ColorBorde));

            materialInterior = DiamondGizmo.NuevoMaterial(ColorObjetivo);
            var interior = DiamondGizmo.CrearCara("Interior", marcador.transform, TamanoInterior, materialInterior);
            interior.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            // Pedido explicito: "el minimapa no se ven los triangulos... y
            // objetivo" -- el objetivo actual tampoco tenia ninguna marca en
            // el minimapa, solo en el mundo. Sigue este mismo transform
            // (Update ya lo reposiciona en XZ sobre el punto objetivo cada
            // frame), asi que basta con colgarle un icono mas.
            if (Application.isPlaying)
            {
                int layerMinimapa = LayerMask.NameToLayer("Minimap");
                if (layerMinimapa < 0) layerMinimapa = 8;
                var iconoMinimapa = MinimapIcon.Spawn(transform, ColorObjetivo, layerMinimapa, 2.4f);
                iconoMinimapa.ConvertirEnDobleTriangulo();
            }
        }

        void Update()
        {
            if (marcador == null || obtenerPunto == null) return;

            if (RomboVisibilidad.Suprimidos) { marcador.SetActive(false); return; }

            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) { marcador.SetActive(false); return; }

            var punto = obtenerPunto();
            transform.position = punto + Vector3.up * Altura;

            var haciaMarcador = transform.position - cam.transform.position;
            bool visible = haciaMarcador.magnitude <= DistanciaVisible;
            marcador.SetActive(visible);
            if (visible) marcador.transform.rotation = cam.transform.rotation;
        }
    }
}
