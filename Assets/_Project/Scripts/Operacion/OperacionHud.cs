using UnityEngine;
using UnityEngine.UI;

namespace SP.Operacion
{
    // Cartel de la Operacion Cuartel: capitulo + objetivo (arriba a la izquierda), temporizador grande al centro,
    // barra de progreso y la linea de "que tecla apretar" abajo. Se arma en runtime sobre el canvas del HUD, igual que MisionHud.
    public class OperacionHud : MonoBehaviour
    {
        public static OperacionHud Instancia { get; private set; }

        Text titulo, detalle, prompt, timerNumero, timerEtiqueta, aviso;
        RectTransform relleno, barra, barraPromptFondo, barraPromptRelleno;
        CanvasGroup timerGrupo, avisoGrupo;
        float avisoHasta;
        int promptFrame = -1;
        public string TextoTitulo => titulo != null ? titulo.text : "";
        public string TextoDetalle => detalle != null ? detalle.text : "";
        public string TextoPrompt => prompt != null && promptFrame >= Time.frameCount - 2 ? prompt.text : "";
        public string TextoTimer => timerGrupo != null && timerGrupo.alpha > 0.01f ? timerEtiqueta.text + " " + timerNumero.text : "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Instancia = null;

        public static OperacionHud Crear()
        {
            if (Instancia != null) return Instancia;
            var driver = SP.Player.PlayerInputDriver.Activo;
            Transform raiz = driver != null && driver.AimUiRef != null ? driver.AimUiRef.transform.parent : null;
            if (raiz == null) return null;

            var go = new GameObject("OperacionHud", typeof(RectTransform), typeof(Image), typeof(OperacionHud));
            go.transform.SetParent(raiz, false);
            var h = go.GetComponent<OperacionHud>();
            Instancia = h;
            var panel = go.GetComponent<Image>();
            panel.color = new Color(0.04f, 0.06f, 0.09f, 0.66f);
            panel.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -20f);
            rt.sizeDelta = new Vector2(360f, 118f);
            rt.localScale = Vector3.one * 0.8f;

            var filete = new GameObject("Filete", typeof(RectTransform), typeof(Image));
            filete.transform.SetParent(go.transform, false);
            filete.GetComponent<Image>().color = new Color(1f, 0.82f, 0.3f, 0.95f);
            filete.GetComponent<Image>().raycastTarget = false;
            var frt = filete.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(4f, 0f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            h.titulo = Texto(go.transform, font, 14, new Vector2(0f, 43f), new Vector2(344f, 20f), new Color(1f, 0.82f, 0.3f));
            h.detalle = Texto(go.transform, font, 16, new Vector2(0f, 8f), new Vector2(344f, 52f), Color.white);
            h.detalle.horizontalOverflow = HorizontalWrapMode.Wrap;

            var fondoBarra = new GameObject("Barra", typeof(RectTransform), typeof(Image));
            fondoBarra.transform.SetParent(go.transform, false);
            fondoBarra.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            h.barra = fondoBarra.GetComponent<RectTransform>();
            h.barra.anchorMin = h.barra.anchorMax = new Vector2(0.5f, 0.5f);
            h.barra.anchoredPosition = new Vector2(0f, -45f);
            h.barra.sizeDelta = new Vector2(324f, 7f);
            h.relleno = Relleno(fondoBarra.transform, new Color(1f, 0.82f, 0.3f));

            // Temporizador grande al centro (resistir / trayecto).
            var tg = new GameObject("OperacionTimer", typeof(RectTransform), typeof(CanvasGroup));
            tg.transform.SetParent(raiz, false);
            var trt = tg.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(0f, -110f);
            trt.sizeDelta = new Vector2(620f, 110f);
            h.timerGrupo = tg.GetComponent<CanvasGroup>();
            h.timerGrupo.alpha = 0f; h.timerGrupo.interactable = false; h.timerGrupo.blocksRaycasts = false;
            h.timerEtiqueta = Texto(tg.transform, font, 26, new Vector2(0f, 36f), new Vector2(620f, 40f), new Color(1f, 0.82f, 0.3f));
            h.timerNumero = Texto(tg.transform, font, 60, new Vector2(0f, -20f), new Vector2(620f, 80f), Color.white);
            var borde = h.timerNumero.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(0f, 0f, 0f, 0.85f); borde.effectDistance = new Vector2(2f, -2f);

            // Linea de "tecla" abajo al centro + barra de progreso de interaccion.
            var pg = new GameObject("OperacionPrompt", typeof(RectTransform), typeof(Image));
            pg.transform.SetParent(raiz, false);
            var pimg = pg.GetComponent<Image>(); pimg.color = new Color(0f, 0f, 0f, 0.55f); pimg.raycastTarget = false;
            var prt = pg.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, 170f);
            prt.sizeDelta = new Vector2(760f, 64f);
            h.prompt = Texto(pg.transform, font, 22, new Vector2(0f, 8f), new Vector2(740f, 36f), Color.white);
            var pb = new GameObject("BarraPrompt", typeof(RectTransform), typeof(Image));
            pb.transform.SetParent(pg.transform, false);
            pb.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            h.barraPromptFondo = pb.GetComponent<RectTransform>();
            h.barraPromptFondo.anchorMin = h.barraPromptFondo.anchorMax = new Vector2(0.5f, 0.5f);
            h.barraPromptFondo.anchoredPosition = new Vector2(0f, -20f);
            h.barraPromptFondo.sizeDelta = new Vector2(700f, 9f);
            h.barraPromptRelleno = Relleno(pb.transform, new Color(0.35f, 1f, 0.5f));
            h.promptRaiz = pg;
            pg.SetActive(false);

            // Aviso grande central (cambio de fase).
            var ag = new GameObject("OperacionAviso", typeof(RectTransform), typeof(CanvasGroup));
            ag.transform.SetParent(raiz, false);
            var art = ag.GetComponent<RectTransform>();
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = new Vector2(0f, 150f);
            art.sizeDelta = new Vector2(1200f, 120f);
            h.avisoGrupo = ag.GetComponent<CanvasGroup>(); h.avisoGrupo.alpha = 0f;
            h.avisoGrupo.interactable = false; h.avisoGrupo.blocksRaycasts = false;
            h.aviso = Texto(ag.transform, font, 40, Vector2.zero, new Vector2(1200f, 120f), new Color(1f, 0.9f, 0.4f));
            h.aviso.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Bug #042: 1200 de ancho fijo * la escala del canvas (1,79 medido) era mas que la pantalla y
            // "UN SOLDADO INTERACTUA 30 s · LOS DEMAS DEFIENDEN" salia cortado por los dos lados. Ahora ocupa el 84% del
            // ancho real (con la fuente belica, mas ancha) y achica la letra si no entra.
            art.anchorMin = new Vector2(0.08f, 0.5f); art.anchorMax = new Vector2(0.92f, 0.5f);
            art.sizeDelta = new Vector2(0f, 150f);
            var avisoRt = h.aviso.rectTransform;
            avisoRt.anchorMin = Vector2.zero; avisoRt.anchorMax = Vector2.one; avisoRt.sizeDelta = Vector2.zero; avisoRt.anchoredPosition = Vector2.zero;
            h.aviso.verticalOverflow = VerticalWrapMode.Truncate;
            h.aviso.resizeTextForBestFit = true; h.aviso.resizeTextMinSize = 18; h.aviso.resizeTextMaxSize = 40;
            var bordeA = h.aviso.gameObject.AddComponent<Outline>();
            bordeA.effectColor = new Color(0f, 0f, 0f, 0.9f); bordeA.effectDistance = new Vector2(2f, -2f);
            return h;
        }

        GameObject promptRaiz;

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (timerGrupo != null) Destroy(timerGrupo.gameObject);
            if (promptRaiz != null) Destroy(promptRaiz);
            if (avisoGrupo != null) Destroy(avisoGrupo.gameObject);
        }

        static RectTransform Relleno(Transform padre, Color color)
        {
            var fill = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(padre, false);
            fill.GetComponent<Image>().color = color;
            var r = fill.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.sizeDelta = Vector2.zero;
            return r;
        }

        static Text Texto(Transform padre, Font font, int tam, Vector2 pos, Vector2 caja, Color color)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.color = color;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = caja;
            return t;
        }

        // ---- API que usa el director ----
        public void Objetivo(string tit, string det, float progreso01, Color color)
        {
            if (titulo == null) return;
            titulo.text = tit; detalle.text = det;
            relleno.sizeDelta = new Vector2(barra.sizeDelta.x * Mathf.Clamp01(progreso01), 0f);
            relleno.GetComponent<Image>().color = color;
        }

        public void Timer(string etiqueta, int segundos, Color color)
        {
            if (timerGrupo == null) return;
            timerVisible = true;
            timerEtiqueta.text = etiqueta; timerNumero.text = segundos.ToString(); timerNumero.color = color;
        }
        public void OcultarTimer() { timerVisible = false; if (timerGrupo != null) timerGrupo.alpha = 0f; }
        // Bug #060: el cartel grande de fase y el temporizador se encimaban ("TANQUE DESTRUIDO / RESISTE 40 s" sobre "RESISTI 40").
        // Mientras hay cartel, el temporizador se aparta (se desvanece) y vuelve cuando el cartel se va.
        bool timerVisible;

        // Se debe llamar CADA frame mientras haya que mostrarlo; deja de verse solo.
        public void Prompt(string texto, float progreso01 = -1f)
        {
            if (prompt == null || string.IsNullOrEmpty(texto)) return;
            promptFrame = Time.frameCount;
            prompt.text = texto;
            if (!promptRaiz.activeSelf) promptRaiz.SetActive(true);
            bool conBarra = progreso01 >= 0f;
            if (barraPromptFondo.gameObject.activeSelf != conBarra) barraPromptFondo.gameObject.SetActive(conBarra);
            if (conBarra) barraPromptRelleno.sizeDelta = new Vector2(barraPromptFondo.sizeDelta.x * Mathf.Clamp01(progreso01), 0f);
        }

        public void Aviso(string texto, float segundos = 3.5f)
        {
            if (aviso == null) return;
            aviso.text = texto; avisoHasta = Time.unscaledTime + segundos;
        }

        void Update()
        {
            if (promptRaiz != null && promptRaiz.activeSelf && promptFrame < Time.frameCount - 1) promptRaiz.SetActive(false);
            if (avisoGrupo != null)
            {
                float objetivoAlfa = Time.unscaledTime < avisoHasta ? 1f : 0f;
                avisoGrupo.alpha = Mathf.MoveTowards(avisoGrupo.alpha, objetivoAlfa, Time.unscaledDeltaTime * 2.5f);
            }
            if (timerGrupo != null) timerGrupo.alpha = timerVisible ? Mathf.Clamp01(1f - (avisoGrupo != null ? avisoGrupo.alpha : 0f) * 1.5f) : 0f;
        }
    }
}
