using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Actors;
using SP.Combat;

namespace SP.UI
{
    // "SOLDADO ABATIDO": texto grande, rojo/naranja, que explota (overshoot
    // de escala + sacudida) y se desvanece en 2 segundos, cada vez que
    // muere un enemigo. Se auto-suscribe a EntityDiedEvent -- no necesita
    // que nadie lo llame a mano, igual que SelectionRingManager.
    public class KillFeedView : MonoBehaviour
    {
        Text label;
        Coroutine routine;

        public void Bind(Text text)
        {
            label = text;
            label.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (label == null) label = GetComponentInChildren<Text>(true);
        }

        // Antes esta vista se suscribia sola a EntityDiedEvent. El EventBus
        // corre los handlers en orden de registro, y esta se registraba al
        // armar la UI mientras que KillFeedbackDirector se agrega despues:
        // el feed leia FeedText()/LastKillWasPlayer ANTES de que el director
        // procesara la baja, o sea que mostraba siempre los datos de la baja
        // ANTERIOR. En la primera baja propia salia "ABATIDO POR TU
        // ESCUADRA" en azul, y el "x2" y la racha llegaban siempre un kill
        // tarde. Ahora lo llama el director cuando ya actualizo su estado,
        // asi el orden es correcto por construccion y no por suerte.
        public void ShowKill()
        {
            if (!Application.isPlaying || label == null) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PunchAndFade());
        }

        IEnumerator PunchAndFade()
        {
            // El texto lo arma KillFeedbackDirector, que es quien sabe si
            // fue el jugador o la escuadra y cuantas bajas se agruparon.
            // Apilar una linea por baja cubria la pantalla justo cuando
            // mas informacion hay.
            var director = SP.Presentation.KillFeedbackDirector.Instance;
            label.text = director != null ? director.FeedText() : "SOLDADO ABATIDO";
            label.gameObject.SetActive(true);
            var rt = label.rectTransform;
            // Las bajas propias van en naranja fuerte y las de la
            // escuadra en un tono mas frio: sin esto el jugador no podia
            // evaluar su aporte contra el de sus soldados.
            bool mine = director != null && director.LastKillWasPlayer;
            var baseColor = mine ? new Color(0.95f, 0.25f, 0.15f) : new Color(0.45f, 0.7f, 0.95f);

            // Overshoot bien grande (2x) y una leve sacudida de rotación:
            // "explosivo y volátil" pedía algo más brusco que el punch
            // parejo de PhaseBannerView.
            const float punchTime = 0.18f;
            float t = 0f;
            while (t < punchTime)
            {
                t += Time.unscaledDeltaTime;
                float k = t / punchTime;
                rt.localScale = Vector3.one * Mathf.Lerp(0.2f, 2f, k);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * 40f) * (1f - k) * 10f);
                label.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                yield return null;
            }

            const float settleTime = 0.12f;
            t = 0f;
            while (t < settleTime)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(2f, 1f, t / settleTime);
                rt.localRotation = Quaternion.Slerp(rt.localRotation, Quaternion.identity, t / settleTime);
                yield return null;
            }
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            const float holdTime = 0.7f;
            yield return new WaitForSecondsRealtime(holdTime);

            const float fadeTime = 1f; // punch + settle + hold + fade ~= 2s en total
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - (t / fadeTime);
                label.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }

            label.gameObject.SetActive(false);
        }
    }
}
