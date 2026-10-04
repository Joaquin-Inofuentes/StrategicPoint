using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Bug #044: el "HEADSHOT" salia como etiqueta en el mundo, pegada a la cabeza del enemigo: a 30 m eran 4 pixeles rojos
    // al lado de la mira y nadie la leia. Ahora es un texto de pantalla, justo debajo de la mira (donde el jugador ya esta
    // mirando), con un golpe de escala corto y fundido. Se arma solo la primera vez: no hay que tocar escenas ni prefabs.
    public class HeadshotPopup : MonoBehaviour
    {
        static HeadshotPopup instancia;
        Text etiqueta;
        Coroutine rutina;

        public static void Mostrar(bool mata)
        {
            if (!Application.isPlaying) return;
            if (instancia == null) instancia = Crear();
            if (instancia.rutina != null) instancia.StopCoroutine(instancia.rutina);
            instancia.rutina = instancia.StartCoroutine(instancia.Golpe(mata));
        }

        static HeadshotPopup Crear()
        {
            var go = new GameObject("HeadshotPopup");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var escalador = go.AddComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1280f, 720f);
            escalador.matchWidthOrHeight = 0.5f;
            var p = go.AddComponent<HeadshotPopup>();

            var t = new GameObject("Texto").AddComponent<Text>();
            t.transform.SetParent(go.transform, false);
            t.raycastTarget = false;
            // La misma tipografia que el resto del HUD si hay algun texto en escena; si no, la de Unity.
            var modelo = Object.FindAnyObjectByType<Text>();
            t.font = modelo != null && modelo.font != null ? modelo.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontStyle = FontStyle.Bold;
            t.fontSize = 30;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sombra = t.gameObject.AddComponent<Outline>();
            sombra.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sombra.effectDistance = new Vector2(2f, -2f);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -58f);
            rt.sizeDelta = new Vector2(600f, 50f);
            t.gameObject.SetActive(false);
            p.etiqueta = t;
            return p;
        }

        public static bool Visible => instancia != null && instancia.etiqueta != null && instancia.etiqueta.gameObject.activeSelf;
        public static string Texto => instancia != null && instancia.etiqueta != null ? instancia.etiqueta.text : "";

        IEnumerator Golpe(bool mata)
        {
            etiqueta.text = mata ? "¡HEADSHOT!" : "HEADSHOT";
            var color = mata ? new Color(1f, 0.22f, 0.15f) : new Color(1f, 0.6f, 0.2f);
            etiqueta.gameObject.SetActive(true);
            var rt = etiqueta.rectTransform;
            float t = 0f;
            while (t < 0.14f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, t / 0.14f);
                etiqueta.color = color;
                yield return null;
            }
            rt.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.6f);
            t = 0f;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                etiqueta.color = new Color(color.r, color.g, color.b, 1f - t / 0.45f);
                yield return null;
            }
            etiqueta.gameObject.SetActive(false);
            rutina = null;
        }
    }
}
