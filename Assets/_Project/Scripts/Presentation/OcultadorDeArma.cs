using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Apaga los renderers del arma que el soldado lleva en la mano durante una animacion procedural (golpe de cuchillo,
    // lanzamiento de granada) y los vuelve a prender igual que estaban al terminar.
    public class OcultadorDeArma
    {
        Renderer[] renderers;
        bool[] estabaVisible;

        public bool Oculta { get; private set; }

        public void Ocultar(Transform mano, Transform raiz)
        {
            if (Oculta) return;
            var arma = (mano != null ? BuscarHijo.Ruta(mano, "WeaponVisual") : null) ?? BuscarHijo.Ruta(raiz, "WeaponVisual");
            if (arma == null) return;
            renderers = arma.GetComponentsInChildren<Renderer>(true);
            estabaVisible = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                estabaVisible[i] = renderers[i] != null && renderers[i].enabled;
                if (renderers[i] != null) renderers[i].enabled = false;
            }
            Oculta = true;
        }

        public void Restaurar()
        {
            if (!Oculta) return;
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null && estabaVisible[i]) renderers[i].enabled = true;
            renderers = null;
            Oculta = false;
        }
    }
}
