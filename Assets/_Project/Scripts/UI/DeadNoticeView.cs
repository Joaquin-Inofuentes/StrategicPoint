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

        public void Show(string soldierName, float fadeSeconds = 3f)
        {
            if (label == null || group == null) return;
            label.text = $"{soldierName} está muerto";
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FadeOut(fadeSeconds));
        }

        IEnumerator FadeOut(float duration)
        {
            group.alpha = 1f;
            float t = 0f;
            const float hold = 0.4f;
            while (t < hold) { t += Time.deltaTime; yield return null; }

            t = 0f;
            float fade = Mathf.Max(0.01f, duration - hold);
            while (t < fade)
            {
                t += Time.deltaTime;
                group.alpha = 1f - (t / fade);
                yield return null;
            }
            group.alpha = 0f;
        }
    }
}
