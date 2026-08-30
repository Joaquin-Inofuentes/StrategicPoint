using System;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Cuantos hay seleccionados, en un numero grande y propio -- antes
    // solo aparecia al final del texto de ayuda de RTS, mezclado con la
    // lista de atajos, donde en pleno combate nadie lo lee.
    public class SelectionCountView : MonoBehaviour
    {
        Text label;
        Image background;
        public void Bind(Text text) => label = text;

        IDisposable sub;
        int lastCount;
        // Solo tiene sentido en RTS -- PlayerInputDriver avisa el modo
        // por este metodo en vez de apagar el GameObject entero: apagar
        // la raiz (que es la que escucha el EventBus) mataria la
        // suscripcion cada vez que se pasa a FPS, con el mismo efecto que
        // ya paso una vez al desactivarse a si misma desde OnEnable.
        bool modeAllowsVisible = true;

        void OnEnable()
        {
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (background == null) background = GetComponent<Image>();
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
            Refresh();
        }

        void OnDisable() => sub?.Dispose();

        public void SetModeVisible(bool allowed)
        {
            modeAllowsVisible = allowed;
            Refresh();
        }

        void Refresh()
        {
            bool visible = modeAllowsVisible && lastCount > 0;
            if (background != null) background.enabled = visible;
            if (label != null) label.gameObject.SetActive(visible);
        }

        void OnSelectionChanged(SelectionChangedEvent evt)
        {
            lastCount = evt.SelectedIds != null ? evt.SelectedIds.Count : 0;
            if (label != null && lastCount > 0)
                label.text = lastCount == 1 ? "1 seleccionado" : $"{lastCount} seleccionados";
            Refresh();
        }
    }
}
