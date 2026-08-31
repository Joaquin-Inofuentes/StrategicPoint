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

        void OnEnable()
        {
            if (marker == null) BuildMarker();
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<PossessionChangedEvent>(OnPossessionChanged);
        }

        void OnDisable() => sub?.Dispose();

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

        // El marcador se posiciona en LateUpdate (no se emparenta al
        // soldado): emparentarlo lo heredaria la rotacion del cuerpo y el
        // rombo giraria con el, y ademas quedaria colgando de un objeto
        // que se desactiva al subir a un vehiculo.
        void LateUpdate()
        {
            if (marker == null) return;
            bool show = current != null && current.Health.IsAlive && current.gameObject.activeInHierarchy;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;

            float bob = Mathf.Sin(Time.time * 3f) * 0.08f;
            marker.position = current.transform.position + new Vector3(0f, 2.1f + bob, 0f);
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
