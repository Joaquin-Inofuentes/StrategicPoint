using System;
using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Presentation
{
    // En vista RTS el roster marcaba al poseido pero el mundo no: con la
    // tropa dispersa habia que cruzar el nombre del roster con el mapa para
    // ubicarlo. Un solo marcador que salta de cabeza en cabeza siguiendo al
    // poseido, en vez de uno por soldado que hubiera que apagar y prender.
    public class PossessedMarkerView : MonoBehaviour
    {
        Transform marker;
        Soldier current;
        IDisposable sub;

        static readonly Color MarkerColor = new Color(1f, 0.9f, 0.3f);

        // Altura a la que flota el rombo sobre la cabeza. Constante y no
        // magica suelta: el director la necesita para preguntar por el
        // punto real del marcador al evaluar el nivel de detalle.
        const float MarkerHeight = 2.1f;

        // Puerta de nivel de detalle que escribe WorldUiDirector. Arranca
        // en true a proposito: si el director nunca corre (Edit mode, o
        // una escena sin el), el marcador se comporta exactamente como
        // antes, solo que sin LOD.
        bool lodAllowed = true;
        public bool LodAllowed => lodAllowed;
        public void SetLodAllowed(bool value) => lodAllowed = value;

        void OnEnable()
        {
            if (marker == null) BuildMarker();
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<PossessionChangedEvent>(OnPossessionChanged);
            // Alta y baja en el unico recorrido de UI de mundo. Mismo
            // patron que SP.Core.WorldSystemsRegistry.
            WorldUiDirector.Register(this);
        }

        void OnDisable()
        {
            sub?.Dispose();
            WorldUiDirector.Unregister(this);
        }

        // Punto que el director proyecta para decidir el LOD: el del
        // marcador en si, no el de los pies del soldado.
        public bool TryGetLodProbe(out Vector3 position)
        {
            if (current == null) { position = Vector3.zero; return false; }
            position = current.transform.position + Vector3.up * MarkerHeight;
            return true;
        }

        void BuildMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PossessedMarker";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            // Rombo apuntando hacia abajo: se distingue de las esferas de
            // alerta enemiga y del cubo de estado del escuadron.
            go.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            go.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = new Material(shader) { color = MarkerColor };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            marker = go.transform;
            marker.gameObject.SetActive(false);
        }

        // El marcador se posiciona por codigo (no se emparenta al
        // soldado): emparentarlo lo heredaria la rotacion del cuerpo y el
        // rombo giraria con el, y ademas quedaria colgando de un objeto
        // que se desactiva al subir a un vehiculo.
        //
        // Ya no es un LateUpdate propio: lo llama WorldUiDirector en el
        // unico recorrido de UI de mundo. Devuelve true si el marcador
        // quedo visible, para que el director pueda contarlo.
        public bool Tick()
        {
            if (marker == null) return false;
            // El LOD se COMPONE con la regla de siempre (hay poseido, esta
            // vivo y su objeto esta activo); solo puede restar.
            bool show = lodAllowed && current != null && current.Health.IsAlive && current.gameObject.activeInHierarchy;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return false;

            float bob = Mathf.Sin(Time.time * 3f) * 0.08f;
            marker.position = current.transform.position + new Vector3(0f, MarkerHeight + bob, 0f);
            return true;
        }

        void OnPossessionChanged(PossessionChangedEvent evt)
        {
            current = ActorRegistry.FindById(evt.ToId);
        }

        // El primer poseido no genera un PossessionChangedEvent (ya lo esta
        // al arrancar la partida), asi que hace falta poder fijarlo a mano.
        public void SetInitial(Soldier soldier) => current = soldier;
    }
}
