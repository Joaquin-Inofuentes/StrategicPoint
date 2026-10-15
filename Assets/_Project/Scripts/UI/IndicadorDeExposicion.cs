using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Icono chico abajo al centro (antes un cartel de texto): dice si el soldado que manejas esta a la LUZ (los enemigos te ven de lejos) o en la
    // SOMBRA (solo te ven de cerca). Es la cara visible de IluminacionTactica: sin esto el jugador no sabria por que
    // unos caminos son "sigilosos" y otros no. Solo se muestra en la mision, y se apaga en las fases sin sigilo.
    public class IndicadorDeExposicion : MonoBehaviour
    {
        Image icono;
        static Sprite spSol, spSombra;
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

            // Pulido (feedback visual, no texto): un icono chico, sol anaranjado = a la luz (te ven de lejos), luna azul = en la sombra.
            rt.sizeDelta = new Vector2(44f, 44f);
            var t = new GameObject("Icono", typeof(RectTransform), typeof(Image));
            t.transform.SetParent(go.transform, false);
            var img = t.GetComponent<Image>();
            img.raycastTarget = false; img.preserveAspect = true;
            var trt = img.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

            var ind = go.GetComponent<IndicadorDeExposicion>();
            ind.icono = img;
            ind.grupo = go.GetComponent<CanvasGroup>();
            ind.grupo.alpha = 0f; ind.grupo.interactable = false; ind.grupo.blocksRaycasts = false;
            return ind;
        }

        // Sprites procedurales (en runtime, no se guardan en escena): disco con rayos y media luna.
        static Sprite Sol()
        {
            if (spSol != null) return spSol;
            const int N = 64; var px = new Color32[N * N]; float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                bool disco = r < 13f;
                bool rayo = r > 18f && r < 30f && Mathf.Abs(Mathf.Sin(ang * 4f)) > 0.8f;
                px[y * N + x] = new Color32(255, 255, 255, (byte)((disco || rayo) ? 255 : 0));
            }
            return spSol = Hacer(px, N);
        }

        static Sprite Sombra()
        {
            if (spSombra != null) return spSombra;
            const int N = 64; var px = new Color32[N * N]; float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                float dx2 = x - (c + 11f), dy2 = y - (c + 5f), r2 = Mathf.Sqrt(dx2 * dx2 + dy2 * dy2);
                bool luna = r < 26f && r2 > 22f;
                px[y * N + x] = new Color32(255, 255, 255, (byte)(luna ? 255 : 0));
            }
            return spSombra = Hacer(px, N);
        }

        static Sprite Hacer(Color32[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
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
                    icono.sprite = iluminado ? Sol() : Sombra();
                    icono.color = iluminado ? new Color(1f, 0.66f, 0.28f) : new Color(0.55f, 0.82f, 1f);
                }
            }
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo * 0.9f, dt * 3f);
        }
    }
}
