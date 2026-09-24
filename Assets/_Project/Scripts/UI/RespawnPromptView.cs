using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Pedido explicito: "cuando muera quiero que aparezca que diga apreta
    // barra espaciadora para reaparecer y ese cuadro vibre, se agrande y
    // achique con lerp". El cambio con [Espacio] a otro aliado YA existia
    // (PlayerInputDriver.DeathSequence, comentario "A2"): lo unico que
    // faltaba era decirle al jugador que esa tecla estaba esperando su
    // respuesta mientras la camara orbita el cadaver. Vive aparte de
    // DeadNoticeView porque esa es una cola de avisos que se desvanecen
    // solos en unos segundos -- este cartel tiene que quedarse fijo (con su
    // pulso) todo el tiempo que dure la espera, no una duracion fija.
    public static class RespawnPromptView
    {
        static GameObject raiz;
        static RectTransform caja;
        static float pulso;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { raiz = null; caja = null; pulso = 0f; }

        static void Construir()
        {
            raiz = new GameObject("RespawnPrompt", typeof(Canvas), typeof(CanvasScaler));
            var c = raiz.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 480; // debajo del lienzo de cinematica (900), encima del HUD normal
            var scaler = raiz.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("Caja", typeof(Image));
            panelGo.transform.SetParent(raiz.transform, false);
            panelGo.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.78f);
            caja = panelGo.GetComponent<RectTransform>();
            caja.anchorMin = caja.anchorMax = new Vector2(0.5f, 0.4f);
            caja.sizeDelta = new Vector2(620f, 88f);

            var textoGo = new GameObject("Texto", typeof(Text));
            textoGo.transform.SetParent(panelGo.transform, false);
            var texto = textoGo.GetComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.text = "[ESPACIO] REAPARECER EN OTRO ALIADO";
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = new Color(1f, 0.85f, 0.25f);
            texto.fontSize = 30;
            texto.fontStyle = FontStyle.Bold;
            texto.raycastTarget = false;
            var rt = texto.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            raiz.SetActive(false);
        }

        public static void Mostrar()
        {
            if (raiz == null) Construir();
            pulso = 0f;
            if (caja != null) caja.localScale = Vector3.one;
            raiz.SetActive(true);
        }

        public static void Ocultar()
        {
            if (raiz != null) raiz.SetActive(false);
        }

        // Llamado cada frame mientras dura la espera (PlayerInputDriver.
        // DeathSequence, dentro del while de orbita): un lerp seno de
        // escala, no un solo lerp de ida -- "vibre, se agrande y achique"
        // pide un ciclo continuo, no una animacion que termina.
        public static void Tick()
        {
            if (raiz == null || !raiz.activeSelf || caja == null) return;
            pulso += Time.unscaledDeltaTime * 4.2f;
            float k = Mathf.Lerp(0.92f, 1.1f, (Mathf.Sin(pulso) + 1f) * 0.5f);
            caja.localScale = Vector3.one * k;
        }
    }
}
