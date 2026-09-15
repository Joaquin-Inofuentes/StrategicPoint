using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.UI
{
    // Pedido explicito: "todos los botones deben tener un sonido al
    // apuntar al boton y otro sonido al hacerle click". Un solo componente
    // reutilizable en vez de repetir la logica en cada controlador de
    // pantalla (MainMenuController, PauseController, GameOutcomeController,
    // KeyRebindView): Attach() se llama junto a cada onClick.AddListener ya
    // existente y no hace falta tocar nada mas.
    public class ButtonSfx : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
            => AudioDirector.PlayUi2D(SfxKind.UiHover, 0.4f, 0.4f);

        // Prioridad del click mas alta que la del hover: perder el acuse
        // de un click (por limite de voces del canal Ui) confunde al
        // jugador sobre si la accion se registro; perder un hover no.
        public static void Attach(Button button)
        {
            if (button == null) return;
            var go = button.gameObject;
            if (go.GetComponent<ButtonSfx>() == null) go.AddComponent<ButtonSfx>();
            button.onClick.AddListener(() => AudioDirector.PlayUi2D(SfxKind.UiClick, 0.55f, 0.85f));
        }
    }
}
