using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Cartel grande centrado al completar una fase ("Felicidades, terminaste
    // la Fase 1..."), con una animación de tamaño (lerp de escala).
    public class PhaseBannerView : MonoBehaviour
    {
        Text label;
        RectTransform rt;

        public void Bind(Text text)
        {
            if (text == null) return;
            label = text;
            rt = text.rectTransform;
            text.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (rt != null) rt.localScale = Vector3.one;
            if (label != null) label.gameObject.SetActive(false);
        }

        public void Show(string message, float holdSeconds = 2.2f)
        {
            // `label`/`rt` no se serializan: tras el domain reload al entrar
            // en Play mode (escena construida en editor) quedan null, así
            // que se re-buscan solos en vez de quedar mudos para siempre.
            if (label == null) label = GetComponentInChildren<Text>(true);
            if (label == null) return;
            if (rt == null) rt = label.rectTransform;

            label.text = message;
            label.gameObject.SetActive(true);

            if (Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(PunchAndHide(holdSeconds));
            }
        }

        IEnumerator PunchAndHide(float holdSeconds)
        {
            yield return ScaleOver(0.2f, 1.15f, 0.25f);
            yield return ScaleOver(1.15f, 1f, 0.12f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return ScaleOver(1f, 0.2f, 0.3f);
            label.gameObject.SetActive(false);
        }

        IEnumerator ScaleOver(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            rt.localScale = Vector3.one * to;
        }
    }
}
