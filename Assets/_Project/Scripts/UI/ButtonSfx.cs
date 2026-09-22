using System.Collections;
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
    //
    // Sumado despues: el hover solo tenia sonido, sin nada en pantalla -- un
    // jugador navegando con el mouse (o con el teclado, que tambien dispara
    // OnPointerEnter via el EventSystem al mover la seleccion) no tenia forma
    // VISUAL de saber que boton iba a apretar antes de apretarlo. Un
    // agrandado leve (escala, no color) porque funciona igual sin importar
    // el skin de cada boton, y no pisa el Selected/Highlighted del propio
    // Button si ese componente ya trae uno.
    public class ButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float EscalaHover = 1.08f;
        const float SegundosDeTransicion = 0.08f;

        Vector3 escalaBase;
        Coroutine animacionEnCurso;
        bool escalaBaseCapturada;

        void CapturarEscalaBase()
        {
            if (escalaBaseCapturada) return;
            escalaBaseCapturada = true;
            escalaBase = transform.localScale;
        }

        // Un boton puede desactivarse (cambio de panel) a mitad del lerp de
        // hover: sin esto quedaria con la escala agrandada la proxima vez
        // que el panel se reactiva, porque OnPointerExit nunca llega a
        // dispararse para un objeto ya inactivo.
        void OnDisable()
        {
            if (!escalaBaseCapturada) return;
            if (animacionEnCurso != null) StopCoroutine(animacionEnCurso);
            animacionEnCurso = null;
            transform.localScale = escalaBase;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AudioDirector.PlayUi2D(SfxKind.UiHover, 0.4f, 0.4f);
            CapturarEscalaBase();
            AnimarA(escalaBase * EscalaHover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CapturarEscalaBase();
            AnimarA(escalaBase);
        }

        void AnimarA(Vector3 destino)
        {
            if (animacionEnCurso != null) StopCoroutine(animacionEnCurso);
            animacionEnCurso = StartCoroutine(LerpEscala(destino));
        }

        IEnumerator LerpEscala(Vector3 destino)
        {
            var inicio = transform.localScale;
            float t = 0f;
            while (t < SegundosDeTransicion)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(inicio, destino, Mathf.Clamp01(t / SegundosDeTransicion));
                yield return null;
            }
            transform.localScale = destino;
            animacionEnCurso = null;
        }

        // Prioridad del click mas alta que la del hover: perder el acuse
        // de un click (por limite de voces del canal Ui) confunde al
        // jugador sobre si la accion se registro; perder un hover no.
        public static void Attach(Button button)
        {
            if (button == null) return;
            var go = button.gameObject;
            if (go.GetComponent<ButtonSfx>() == null) go.AddComponent<ButtonSfx>();
            button.onClick.AddListener(() => AudioDirector.PlayUi2D(SfxKind.UiClick, 0.55f, 0.85f));

            // Pedido explicito: arte "low-poly futurista belico" para los
            // botones en vez del rectangulo plano de siempre. Se aplica aca
            // (en vez de en cada BuildButton de cada pantalla) porque TODO
            // boton real del juego ya pasa por Attach -- un solo lugar
            // cubre menu principal, pausa, ajustes y game over de una vez.
            // Solo si el boton no trae ya un sprite propio, para no pisar
            // un skin especifico que se le haya puesto a mano.
            var img = button.GetComponent<Image>();
            if (img != null && img.sprite == null)
            {
                img.sprite = MilitaryButtonSkin.Shared;
                img.type = Image.Type.Sliced;
            }
        }
    }
}
