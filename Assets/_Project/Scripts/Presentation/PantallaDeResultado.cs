using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Mision;

namespace SP.Presentation
{
    // Bug #064: "agrega una animacion, que no sea una pantalla de golpe, que tenga su fade o efecto". La pantalla de
    // victoria/derrota aparecia entera en un frame (y debajo seguian asomando el HUD, el minimapa y la flecha del
    // helicoptero). Ahora:
    //   - va en su propio canvas por encima de todo el HUD
    //   - el fondo se funde desde transparente, el titulo entra grande y se asienta
    //   - las estadisticas entran fila por fila (deslizando) y los numeros cuentan desde 0
    //   - los botones aparecen al final y recien ahi se pueden usar
    // Corre con tiempo NO escalado: la pantalla pone timeScale en 0.
    public static class PantallaDeResultado
    {
        public const float DuracionFondo = 0.7f, EntreFilas = 0.17f, DuracionFila = 0.28f, DuracionConteo = 0.6f;
        const float AltoFila = 26f;
        static readonly Color ColorDestacado = new Color(1f, 0.86f, 0.38f, 1f);

        public static bool Animando { get; private set; }
        public static int FilasMostradas { get; private set; }

        // alfaFondo < 1 (bug #077: victoria de la Operacion, con la escena viva y en camara lenta detras): el fondo queda
        // translucido y se agrega un degrade oscuro en el centro para que la tabla se lea sobre la escena.
        public static float AlfaDeFondoUltimo { get; private set; } = 1f;

        public static IEnumerator Animar(GameObject panel, List<EstadisticasDeMision.Fila> filas, Text statsViejo, float yInicio, float alfaFondo = 1f)
        {
            if (panel == null) yield break;
            Animando = true;
            FilasMostradas = 0;

            // Canvas propio por encima del HUD (los botones necesitan su propio GraphicRaycaster).
            var canvas = panel.GetComponent<Canvas>();
            if (canvas == null) { canvas = panel.AddComponent<Canvas>(); panel.AddComponent<GraphicRaycaster>(); }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 800;

            var fondo = panel.GetComponent<Image>();
            var colorFondo = fondo != null ? fondo.color : Color.clear;
            colorFondo.a = Mathf.Clamp01(alfaFondo);   // por defecto 1: tapa del todo el HUD (antes asomaban el objetivo y el minimapa)
            AlfaDeFondoUltimo = colorFondo.a;
            Image degrade = null;
            if (colorFondo.a < 0.99f) degrade = CrearDegrade(panel.transform);
            var titulo = panel.transform.Find("Title") as RectTransform;
            var grupoTitulo = Grupo(titulo);
            var reintentar = panel.transform.Find("RetryButton") as RectTransform;
            var salir = panel.transform.Find("ExitButton") as RectTransform;
            var razon = panel.transform.Find("Reason") as RectTransform;
            var grupoRazon = Grupo(razon);

            // Botones lado a lado abajo: deja lugar a la tabla de estadisticas.
            var botones = new List<CanvasGroup>();
            if (reintentar != null) { reintentar.sizeDelta = new Vector2(340f, 78f); reintentar.anchoredPosition = new Vector2(-185f, -205f); botones.Add(Grupo(reintentar)); }
            if (salir != null) { salir.sizeDelta = new Vector2(340f, 78f); salir.anchoredPosition = new Vector2(185f, -205f); botones.Add(Grupo(salir)); }
            foreach (var b in botones) { b.alpha = 0f; b.interactable = false; b.blocksRaycasts = false; }

            // Tabla de estadisticas (reemplaza la linea suelta "Stats").
            var viejo = panel.transform.Find("Resumen");
            if (viejo != null) Object.Destroy(viejo.gameObject);
            if (statsViejo != null) statsViejo.text = "";
            var tabla = new GameObject("Resumen", typeof(RectTransform)).GetComponent<RectTransform>();
            tabla.SetParent(panel.transform, false);
            tabla.anchorMin = tabla.anchorMax = new Vector2(0.5f, 0.6f);
            tabla.pivot = new Vector2(0.5f, 1f);
            tabla.anchoredPosition = new Vector2(0f, yInicio);
            tabla.sizeDelta = new Vector2(820f, AltoFila * filas.Count);
            var fuente = statsViejo != null ? statsViejo.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var filasUi = new List<(CanvasGroup g, RectTransform rt, Text valor, EstadisticasDeMision.Fila f)>();
            for (int i = 0; i < filas.Count; i++)
            {
                var f = filas[i];
                var fila = new GameObject("Fila_" + i, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
                fila.SetParent(tabla, false);
                fila.anchorMin = fila.anchorMax = new Vector2(0.5f, 1f);
                fila.pivot = new Vector2(0.5f, 1f);
                fila.sizeDelta = new Vector2(820f, AltoFila);
                fila.anchoredPosition = new Vector2(0f, -i * AltoFila);
                var color = f.Destacada ? ColorDestacado : new Color(1f, 1f, 1f, 0.92f);
                Texto(fila, "Etiqueta", f.Etiqueta, fuente, TextAnchor.MiddleRight, new Vector2(-14f, 0f), new Vector2(1f, 0.5f), color, f.Destacada);
                var valor = Texto(fila, "Valor", f.Numero >= 0 ? string.Format(f.Valor, 0) : f.Valor, fuente, TextAnchor.MiddleLeft, new Vector2(14f, 0f), new Vector2(0f, 0.5f), color, true);
                var g = fila.GetComponent<CanvasGroup>(); g.alpha = 0f; g.interactable = false; g.blocksRaycasts = false;
                filasUi.Add((g, fila, valor, f));
            }

            // 1) fondo + titulo
            float t = 0f;
            if (fondo != null) fondo.color = new Color(colorFondo.r, colorFondo.g, colorFondo.b, 0f);
            if (grupoTitulo != null) grupoTitulo.alpha = 0f;
            if (grupoRazon != null) grupoRazon.alpha = 0f;
            while (t < DuracionFondo + 0.25f)
            {
                t += Time.unscaledDeltaTime;
                float kf = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / DuracionFondo));
                if (fondo != null) fondo.color = new Color(colorFondo.r, colorFondo.g, colorFondo.b, colorFondo.a * kf);
                if (degrade != null) degrade.color = new Color(0f, 0f, 0f, DegradeAlfa * kf);
                float kt = Mathf.Clamp01((t - 0.2f) / 0.55f);
                if (titulo != null)
                {
                    titulo.localScale = Vector3.one * Mathf.LerpUnclamped(1.45f, 1f, SalidaConRebote(kt));
                    grupoTitulo.alpha = Mathf.Clamp01(kt * 2f);
                }
                if (grupoRazon != null) grupoRazon.alpha = Mathf.Clamp01((t - 0.55f) / 0.3f);
                yield return null;
            }
            if (titulo != null) titulo.localScale = Vector3.one;

            // 2) filas de a una, deslizando, y los numeros contando
            float inicioFilas = Time.unscaledTime;
            float fin = inicioFilas + EntreFilas * filasUi.Count + DuracionConteo;
            while (Time.unscaledTime < fin)
            {
                float ahora = Time.unscaledTime - inicioFilas;
                for (int i = 0; i < filasUi.Count; i++)
                {
                    var (g, rt, valor, f) = filasUi[i];
                    float k = Mathf.Clamp01((ahora - i * EntreFilas) / DuracionFila);
                    if (k <= 0f) continue;
                    if (g.alpha <= 0f) FilasMostradas++;
                    g.alpha = k;
                    rt.anchoredPosition = new Vector2(Mathf.Lerp(36f, 0f, 1f - (1f - k) * (1f - k)), -i * AltoFila);
                    if (f.Numero > 0)
                    {
                        float kc = Mathf.Clamp01((ahora - i * EntreFilas) / DuracionConteo);
                        valor.text = string.Format(f.Valor, Mathf.RoundToInt(f.Numero * (1f - (1f - kc) * (1f - kc))));
                    }
                }
                yield return null;
            }
            foreach (var (g, rt, valor, f) in filasUi)
            {
                if (g.alpha <= 0f) FilasMostradas++;
                g.alpha = 1f;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
                if (f.Numero >= 0) valor.text = string.Format(f.Valor, f.Numero);
            }

            // 3) botones
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                foreach (var b in botones) b.alpha = Mathf.Clamp01(t / 0.35f);
                yield return null;
            }
            foreach (var b in botones) { b.alpha = 1f; b.interactable = true; b.blocksRaycasts = true; }
            Animando = false;
        }

        const float DegradeAlfa = 0.62f;
        static Texture2D texturaDegrade;

        // Manchon oscuro elíptico y suave detras de la tabla (primer hijo: queda debajo de todo el texto y los botones).
        static Image CrearDegrade(Transform panel)
        {
            var viejo = panel.Find("DegradeDeLegibilidad");
            if (viejo != null) Object.Destroy(viejo.gameObject);
            if (texturaDegrade == null)
            {
                const int w = 128, h = 96;
                texturaDegrade = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float nx = ((x + 0.5f) / w) * 2f - 1f, ny = ((y + 0.5f) / h) * 2f - 1f;
                        float d = Mathf.Sqrt(nx * nx + ny * ny);
                        float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.35f, 1f, d));
                        px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                texturaDegrade.SetPixels32(px); texturaDegrade.Apply(false, true);
            }
            var go = new GameObject("DegradeDeLegibilidad", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1500f, 900f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            var img = go.GetComponent<Image>();
            img.sprite = Sprite.Create(texturaDegrade, new Rect(0, 0, texturaDegrade.width, texturaDegrade.height), new Vector2(0.5f, 0.5f), 100f);
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            return img;
        }

        static CanvasGroup Grupo(RectTransform rt)
        {
            if (rt == null) return null;
            var g = rt.GetComponent<CanvasGroup>();
            if (g == null) g = rt.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        static Text Texto(RectTransform padre, string nombre, string texto, Font fuente, TextAnchor alineacion, Vector2 pos, Vector2 pivote, Color color, bool negrita)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(padre, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivote;
            rt.sizeDelta = new Vector2(400f, AltoFila);
            rt.anchoredPosition = pos;
            var tx = go.GetComponent<Text>();
            tx.font = fuente; tx.text = texto; tx.fontSize = 20; tx.alignment = alineacion; tx.color = color;
            tx.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
            tx.raycastTarget = false; tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.55f); o.effectDistance = new Vector2(1f, -1f);
            return tx;
        }

        // Ease-out con un poquito de rebote al final (el titulo "cae" y se asienta).
        static float SalidaConRebote(float x)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
