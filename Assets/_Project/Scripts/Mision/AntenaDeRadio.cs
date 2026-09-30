using UnityEngine;
using SP.Presentation;

namespace SP.Mision
{
    // La antena de radio del fortin: objetivo OPCIONAL que cambia el final de la partida. Mientras siga en pie, el enemigo
    // pide refuerzos apenas rescatas al civil (una segunda oleada por los flancos); si la derrumbas antes -- una carga del
    // Asalto o un cohete -- esos refuerzos no llegan. Vive en un cubo con ObstacleMarker: cualquier forma de derribarlo vale.
    [DisallowMultipleComponent]
    public class AntenaDeRadio : MonoBehaviour
    {
        public static bool Destruida { get; private set; }
        public static AntenaDeRadio Activa { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Destruida = false; Activa = null; }

        ObstacleMarker marca;

        void OnEnable()
        {
            Activa = this;
            marca = GetComponent<ObstacleMarker>();
            ObstacleMarker.Derrumbado += AlDerrumbarse;
        }

        void OnDisable()
        {
            ObstacleMarker.Derrumbado -= AlDerrumbarse;
            if (Activa == this) Activa = null;
        }

        void AlDerrumbarse(ObstacleMarker m)
        {
            if (m != marca || Destruida) return;
            Destruida = true;
            // Las luces rojas se apagan: se nota desde lejos que la antena ya no transmite.
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var b in GetComponentsInChildren<BalizaPulsante>(true)) b.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Baliza")) continue;
                var bloque = new MaterialPropertyBlock();
                r.GetPropertyBlock(bloque);
                bloque.SetColor("_EmissionColor", Color.black);
                r.SetPropertyBlock(bloque);
            }
            SP.UI.AlertQueue.Push("¡ANTENA DE RADIO DESTRUIDA! EL ENEMIGO YA NO PUEDE PEDIR REFUERZOS", SP.UI.AlertPriority.Alta, 3.5f);
        }
    }
}
