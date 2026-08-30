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
        IDisposable sub;
        Coroutine routine;

        public void Bind(Text text)
        {
            label = text;
            label.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (label == null) label = GetComponentInChildren<Text>(true);
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnEntityDied);
        }

        void OnDisable() => sub?.Dispose();

        void OnEntityDied(EntityDiedEvent evt)
        {
            if (!Application.isPlaying || label == null) return;

            var actor = ActorRegistry.FindById(evt.ActorId);
            // Solo se anuncian bajas enemigas: si a un aliado propio le
            // avisáramos lo mismo, "SOLDADO ABATIDO" leería como un festejo
            // por perder a los nuestros.
            if (actor == null || actor.Team != TeamId.Enemy) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PunchAndFade());
        }

        IEnumerator PunchAndFade()
        {
            label.text = "SOLDADO ABATIDO";
            label.gameObject.SetActive(true);
            var rt = label.rectTransform;
            var baseColor = new Color(0.95f, 0.25f, 0.15f);

            // Overshoot bien grande (2x) y una leve sacudida de rotación:
            // "explosivo y volátil" pedía algo más brusco que el punch
            // parejo de PhaseBannerView.
            const float punchTime = 0.18f;
            float t = 0f;
            while (t < punchTime)
            {
                t += Time.deltaTime;
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
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(2f, 1f, t / settleTime);
                rt.localRotation = Quaternion.Slerp(rt.localRotation, Quaternion.identity, t / settleTime);
                yield return null;
            }
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            const float holdTime = 0.7f;
            yield return new WaitForSeconds(holdTime);

            const float fadeTime = 1f; // punch + settle + hold + fade ~= 2s en total
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float a = 1f - (t / fadeTime);
                label.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }

            label.gameObject.SetActive(false);
        }
    }
}
