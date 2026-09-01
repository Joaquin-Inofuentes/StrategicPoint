using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Cartel "Está muerto" que aparece al intentar poseer a un aliado
    // caído (F1/F2/F3 o apuntando), y se desvanece solo en 3 segundos.
    public class DeadNoticeView : MonoBehaviour
    {
        Text label;
        CanvasGroup group;
        Coroutine routine;

        public void Bind(Text text, CanvasGroup canvasGroup)
        {
            label = text;
            group = canvasGroup;
            group.alpha = 0f;
            gameObject.SetActive(true);
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

        // Antes solo servia para "X esta muerto" (el sufijo iba fijo
        // adentro). Ahora recibe el mensaje completo, para poder
        // reusarlo tambien en "X esta bajo ataque" / "X tiene poca
        // vida" sin construir un componente nuevo para cada aviso de
        // escuadra parecido.
        public void Show(string message, float fadeSeconds = 3f)
        {
            if (string.IsNullOrEmpty(message)) return;
            AlertQueue.Push(message, AlertPriority.Media, fadeSeconds);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (routine != null) return; // ya hay un aviso en pantalla, que termine su ciclo
            if (AlertQueue.TryDequeue(out string message, out float seconds))
                BeginShow(message, seconds);
        }

        void BeginShow(string message, float fadeSeconds)
        {
            if (label == null || group == null) return;
            label.text = message;
            routine = StartCoroutine(FadeOut(fadeSeconds));
        }

        IEnumerator FadeOut(float duration)
        {
            group.alpha = 1f;
            float t = 0f;
            const float hold = 0.4f;
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
            routine = null;
            AlertQueue.NotifyFinished();
        }
    }
}
