using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Aviso breve del modo al que se acaba de pasar ("VISTA RTS" / "VISTA
    // FPS"). Antes, el cambio de modo era un corte de proyeccion seco sin
    // ningun texto que confirmara que paso ni a cual de los dos.
    public class ModeToastView : MonoBehaviour
    {
        Text label;
        CanvasGroup group;
        Coroutine routine;

        // WP11: el aviso del modo ("VISTA TACTICA · DIRIGI A TU EQUIPO", "TORRETA EN AUTOMATICO"...) cae justo donde el HUD de la operacion pone el
        // temporizador grande (arriba al centro): se encimaban ("90" bajo el aviso). Mientras el temporizador esta a la vista, el HUD pide subir el aviso
        // por encima de el. Es un pedido por cuadro (no guarda nada): sin el HUD de la operacion el aviso queda donde lo puso la escena.
        public const float SubidaPorTemporizador = 80f;
        static bool subirPedido;
        public static void SubirSobreElTemporizador(bool subir) { subirPedido = subir; }
        RectTransform rt; Vector2 posBase; bool conBase, subido;

        void Update()
        {
            if (rt == null) { rt = transform as RectTransform; if (rt == null) return; }
            if (!conBase) { posBase = rt.anchoredPosition; conBase = true; }
            if (subirPedido == subido) return;
            subido = subirPedido;
            rt.anchoredPosition = subido ? posBase + new Vector2(0f, SubidaPorTemporizador) : posBase;
        }

        public void Bind(Text text, CanvasGroup canvasGroup)
        {
            label = text;
            group = canvasGroup;
            if (group == null) return;
            group.alpha = 0f;
        }

        void OnEnable()
        {
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (group == null) group = GetComponent<CanvasGroup>();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            routine = null;
            if (group != null) group.alpha = 0f;
        }

        public void Show(string text, float fadeSeconds = 1f)
        {
            if (label == null || group == null) return;
            // Avisos largos ("¡2 KES CAIDO! NECESITA REANIMACION") se cortaban en una linea: parte en dos y achica la letra si hace falta.
            if (!label.resizeTextForBestFit)
            {
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextMaxSize = Mathf.Max(14, label.fontSize);
                label.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(label.fontSize * 0.55f));
                label.resizeTextForBestFit = true;
            }
            label.text = text;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FadeOut(fadeSeconds));
        }

        IEnumerator FadeOut(float duration)
        {
            group.alpha = 1f;
            float t = 0f;
            const float hold = 0.3f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

            t = 0f;
            float fade = Mathf.Max(0.01f, duration - hold);
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - (t / fade);
                yield return null;
            }
            group.alpha = 0f;
        }
    }
}
