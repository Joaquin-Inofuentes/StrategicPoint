using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Cartel chico abajo al centro: dice si el soldado que manejas esta a la LUZ (los enemigos te ven de lejos) o en la
    // SOMBRA (solo te ven de cerca). Es la cara visible de IluminacionTactica: sin esto el jugador no sabria por que
    // unos caminos son "sigilosos" y otros no. Solo se muestra en la mision, y se apaga en las fases sin sigilo.
    public class IndicadorDeExposicion : MonoBehaviour
    {
        Text texto;
        CanvasGroup grupo;
        float proximoChequeo;
        bool iluminado;
        float alfaObjetivo = 0f;

        public static IndicadorDeExposicion Crear(Transform raiz)
        {
            if (raiz == null) return null;
            var go = new GameObject("IndicadorDeExposicion", typeof(RectTransform), typeof(CanvasGroup), typeof(IndicadorDeExposicion));
            go.transform.SetParent(raiz, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 168f);
            rt.sizeDelta = new Vector2(420f, 26f);

            var t = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Outline));
            t.transform.SetParent(go.transform, false);
            var txt = t.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 15; txt.fontStyle = FontStyle.Bold; txt.alignment = TextAnchor.MiddleCenter; txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow;
            var o = t.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(1.5f, -1.5f);
            var trt = txt.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

            var ind = go.GetComponent<IndicadorDeExposicion>();
            ind.texto = txt;
            ind.grupo = go.GetComponent<CanvasGroup>();
            ind.grupo.alpha = 0f; ind.grupo.interactable = false; ind.grupo.blocksRaycasts = false;
            return ind;
        }

        void Update()
        {
            if (grupo == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= proximoChequeo)
            {
                proximoChequeo = Time.unscaledTime + 0.25f;
                var driver = SP.Player.PlayerInputDriver.Activo;
                var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
                var m = SP.Mision.MisionDirector.Instancia;
                bool hay = IluminacionTactica.Activa && IluminacionTactica.CantidadDeLuces > 0 && yo != null
                           && yo.Health != null && yo.Health.IsAlive && m != null
                           && m.Fase != SP.Mision.FaseDeMision.Victoria && m.Fase != SP.Mision.FaseDeMision.Derrota;
                alfaObjetivo = hay ? 1f : 0f;
                if (hay)
                {
                    iluminado = IluminacionTactica.EstaIluminado(yo.transform.position);
                    texto.text = iluminado ? "A LA LUZ · TE VEN DE LEJOS" : "EN LA SOMBRA · SOLO TE VEN DE CERCA";
                    texto.color = iluminado ? new Color(1f, 0.66f, 0.28f) : new Color(0.55f, 0.82f, 1f);
                }
            }
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo * 0.9f, dt * 3f);
        }
    }
}
