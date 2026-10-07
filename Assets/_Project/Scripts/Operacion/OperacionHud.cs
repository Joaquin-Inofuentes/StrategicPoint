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
        Transform raizHud;
        Font fuenteHud;
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
            h.raizHud = raiz;
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
            h.fuenteHud = font;
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
            SP.UI.ModeToastView.SubirSobreElTemporizador(false);
            if (timerGrupo != null) Destroy(timerGrupo.gameObject);
            if (promptRaiz != null) Destroy(promptRaiz);
            if (avisoGrupo != null) Destroy(avisoGrupo.gameObject);
            if (jefeRaiz != null) Destroy(jefeRaiz);
            if (ilumRaiz != null) Destroy(ilumRaiz);
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

        // ---- Columna de la Operacion (WP0): APIs para los objetivos rediseñados (todavia sin uso) ----

        // Segunda linea chica del objetivo (debajo del panel). Vacia = se oculta.
        GameObject subRaiz, pasosRaiz;
        Text subTexto, pasosTexto;
        public string TextoSubobjetivo => subRaiz != null && subRaiz.activeSelf ? subTexto.text : "";
        public string TextoPasos => pasosRaiz != null && pasosRaiz.activeSelf ? pasosTexto.text : "";

        // Hijo del panel anclado al borde de abajo (las coordenadas locales ya llevan la escala 0.8 del panel).
        GameObject CrearLineaInferior(string nombre, int tam, float alto, TextAnchor ancla, out Text texto)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(transform, false);
            var img = g.GetComponent<Image>(); img.color = new Color(0.04f, 0.06f, 0.09f, 0.55f); img.raycastTarget = false;
            var rt = g.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, alto);
            var tx = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Shadow));
            tx.transform.SetParent(g.transform, false);
            texto = tx.GetComponent<Text>();
            texto.font = fuenteHud != null ? fuenteHud : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = tam; texto.fontStyle = FontStyle.Bold; texto.color = Color.white;
            texto.alignment = ancla; texto.raycastTarget = false; texto.supportRichText = true;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap; texto.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = texto.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 2f); trt.offsetMax = new Vector2(-8f, -2f);
            g.SetActive(false);
            return g;
        }

        public void Subobjetivos(string linea)
        {
            if (titulo == null) return;
            if (string.IsNullOrEmpty(linea))
            {
                if (subRaiz != null && subRaiz.activeSelf) { subRaiz.SetActive(false); AcomodarLineasInferiores(); }
                return;
            }
            if (subRaiz == null) subRaiz = CrearLineaInferior("Subobjetivo", 13, 24f, TextAnchor.MiddleLeft, out subTexto);
            subTexto.color = new Color(0.85f, 0.92f, 1f);
            subTexto.text = linea;
            // Una linea larga (la del cuartel) ocupa dos renglones: la caja crece para que el fondo la cubra.
            float alto = linea.Length > 40 ? 40f : 24f;
            var srt = subRaiz.GetComponent<RectTransform>();
            if (!Mathf.Approximately(srt.sizeDelta.y, alto)) { srt.sizeDelta = new Vector2(0f, alto); AcomodarLineasInferiores(); }
            if (!subRaiz.activeSelf) { subRaiz.SetActive(true); AcomodarLineasInferiores(); }
        }

        // Lista compacta de pasos: hechos en verde con tilde, el actual en amarillo con flecha, el resto en gris.
        // pasos == null o vacio la oculta. "actual" fuera de rango (>= pasos.Length) marca todos como hechos.
        // WP9a (#097): se puede llamar cada frame (no reconstruye si no cambio nada) y [V] la pliega a una sola linea (el paso actual).
        string[] pasosUltimos; int pasoUltimo = -99; bool pasosPlegados, pasosSucios;
        public bool PasosPlegados => pasosPlegados;
        public void AlternarPasos() { pasosPlegados = !pasosPlegados; pasosSucios = true; }

        public void Pasos(string[] pasos, int actual)
        {
            if (titulo == null) return;
            if (pasos == null || pasos.Length == 0)
            {
                pasosUltimos = null; pasoUltimo = -99;
                if (pasosRaiz != null && pasosRaiz.activeSelf) { pasosRaiz.SetActive(false); AcomodarLineasInferiores(); }
                return;
            }
            if (pasosRaiz != null && pasosRaiz.activeSelf && !pasosSucios && ReferenceEquals(pasos, pasosUltimos) && actual == pasoUltimo) return;
            pasosUltimos = pasos; pasoUltimo = actual; pasosSucios = false;
            if (pasosRaiz == null) pasosRaiz = CrearLineaInferior("Pasos", 13, 24f, TextAnchor.UpperLeft, out pasosTexto);
            var sb = new System.Text.StringBuilder();
            int lineas = pasos.Length;
            if (pasosPlegado())
            {
                int i = Mathf.Clamp(actual, 0, pasos.Length - 1);
                sb.Append(actual >= pasos.Length ? "<color=#5BE37A>✓ " : "<color=#FFD24D>▶ ").Append(Mathf.Min(actual + 1, pasos.Length)).Append('/').Append(pasos.Length).Append(" · ").Append(pasos[i]).Append("</color>  <color=#8E98A3>[V]</color>");
                lineas = 1;
            }
            else
            {
                for (int i = 0; i < pasos.Length; i++)
                {
                    if (i > 0) sb.Append('\n');
                    if (i < actual) sb.Append("<color=#5BE37A>✓ ").Append(pasos[i]).Append("</color>");
                    else if (i == actual) sb.Append("<color=#FFD24D>▶ ").Append(pasos[i]).Append("</color>");
                    else sb.Append("<color=#8E98A3>• ").Append(pasos[i]).Append("</color>");
                }
            }
            pasosTexto.text = sb.ToString();
            var rt = pasosRaiz.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 8f + 17f * lineas);
            if (!pasosRaiz.activeSelf) pasosRaiz.SetActive(true);
            AcomodarLineasInferiores();
        }

        bool pasosPlegado() => pasosPlegados;

        void AcomodarLineasInferiores()
        {
            float y = -3f;
            if (subRaiz != null && subRaiz.activeSelf) { var srt = subRaiz.GetComponent<RectTransform>(); srt.anchoredPosition = new Vector2(0f, y); y -= srt.sizeDelta.y + 3f; }
            if (pasosRaiz != null && pasosRaiz.activeSelf) pasosRaiz.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, y);
        }

        // Barra de jefe: ancha, roja, arriba al centro (520x18) con el nombre encima.
        GameObject jefeRaiz;
        Text jefeNombre;
        RectTransform jefeRelleno;
        const float AnchoBarraJefe = 520f;
        public bool BarraDeJefeVisible => jefeRaiz != null && jefeRaiz.activeSelf;
        public string TextoJefe => BarraDeJefeVisible ? jefeNombre.text : "";
        public float FraccionDeJefe { get; private set; }

        public void BarraDeJefe(string nombre, float frac)
        {
            if (raizHud == null) return;
            if (jefeRaiz == null)
            {
                jefeRaiz = new GameObject("OperacionBarraDeJefe", typeof(RectTransform));
                jefeRaiz.transform.SetParent(raizHud, false);
                var rt = jefeRaiz.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -14f);
                rt.sizeDelta = new Vector2(AnchoBarraJefe + 8f, 50f);
                var font = fuenteHud != null ? fuenteHud : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                jefeNombre = Texto(jefeRaiz.transform, font, 20, Vector2.zero, new Vector2(AnchoBarraJefe + 8f, 26f), new Color(1f, 0.82f, 0.78f));
                jefeNombre.rectTransform.anchorMin = jefeNombre.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                jefeNombre.rectTransform.anchoredPosition = new Vector2(0f, -13f);
                var borde = jefeNombre.gameObject.AddComponent<Outline>();
                borde.effectColor = new Color(0f, 0f, 0f, 0.9f); borde.effectDistance = new Vector2(1.5f, -1.5f);
                var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
                fondo.transform.SetParent(jefeRaiz.transform, false);
                var fi = fondo.GetComponent<Image>(); fi.color = new Color(0f, 0f, 0f, 0.7f); fi.raycastTarget = false;
                var frt = fondo.GetComponent<RectTransform>();
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f);
                frt.pivot = new Vector2(0.5f, 0f);
                frt.anchoredPosition = new Vector2(0f, 2f);
                frt.sizeDelta = new Vector2(AnchoBarraJefe + 4f, 22f);
                var inner = new GameObject("Hueco", typeof(RectTransform), typeof(Image));
                inner.transform.SetParent(fondo.transform, false);
                var ii = inner.GetComponent<Image>(); ii.color = new Color(0.25f, 0.04f, 0.04f, 0.9f); ii.raycastTarget = false;
                var irt = inner.GetComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(AnchoBarraJefe, 18f);
                jefeRelleno = Relleno(inner.transform, new Color(0.88f, 0.12f, 0.1f));
                // Relleno ancla en el borde izquierdo del hueco: se alinea al x minimo del padre.
                jefeRelleno.anchorMin = new Vector2(0f, 0f); jefeRelleno.anchorMax = new Vector2(0f, 1f);
            }
            if (!jefeRaiz.activeSelf) jefeRaiz.SetActive(true);
            jefeNombre.text = nombre;
            FraccionDeJefe = Mathf.Clamp01(frac);
            jefeRelleno.sizeDelta = new Vector2(AnchoBarraJefe * FraccionDeJefe, 0f);
        }

        public void OcultarBarraDeJefe()
        {
            if (jefeRaiz != null && jefeRaiz.activeSelf) jefeRaiz.SetActive(false);
        }

        // ---- WP8 (#078): "TE ESTAN ILUMINANDO". Vineta blanca en el borde de la pantalla + cartel mientras un reflector del cuartel te sigue. ----
        GameObject ilumRaiz;
        CanvasGroup ilumGrupo;
        Text ilumTexto;
        bool ilumPedido;
        public const string TextoDeIluminacion = "TE ESTAN ILUMINANDO\nDISPARA AL REFLECTOR";
        public bool IluminadoVisible => ilumGrupo != null && ilumGrupo.alpha > 0.05f;
        public string TextoIluminado => IluminadoVisible && ilumTexto != null ? ilumTexto.text : "";
        public float AlfaDeIluminacion => ilumGrupo != null ? ilumGrupo.alpha : 0f;

        // Se llama cada frame con el estado actual; el fundido de entrada y salida es propio (tiempo sin escalar).
        public void Iluminado(bool si)
        {
            ilumPedido = si;
            if (!si || ilumRaiz != null || raizHud == null) return;
            ilumRaiz = new GameObject("OperacionIluminado", typeof(RectTransform), typeof(CanvasGroup));
            ilumRaiz.transform.SetParent(raizHud, false);
            ilumGrupo = ilumRaiz.GetComponent<CanvasGroup>();
            ilumGrupo.alpha = 0f; ilumGrupo.interactable = false; ilumGrupo.blocksRaycasts = false;
            var rt = ilumRaiz.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            // Vineta: un degradado radial transparente al centro y blanco hacia los bordes.
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VinetaDeReflector" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 1.15f, r)) * 0.36f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            var vineta = new GameObject("Vineta", typeof(RectTransform), typeof(Image));
            vineta.transform.SetParent(ilumRaiz.transform, false);
            var img = vineta.GetComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            img.raycastTarget = false;
            var vrt = vineta.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.offsetMin = vrt.offsetMax = Vector2.zero;
            var font = fuenteHud != null ? fuenteHud : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ilumTexto = Texto(ilumRaiz.transform, font, 30, Vector2.zero, new Vector2(900f, 90f), new Color(1f, 0.97f, 0.8f));
            ilumTexto.rectTransform.anchorMin = ilumTexto.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            ilumTexto.rectTransform.anchoredPosition = new Vector2(0f, -95f);
            ilumTexto.text = TextoDeIluminacion;
            var borde = ilumTexto.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(0f, 0f, 0f, 0.95f); borde.effectDistance = new Vector2(2f, -2f);
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
            if (!CinematicaDeOperacion.IntroActiva && SP.Player.KeyBindings.WasPressed(SP.Player.KeyBindings.PlegarPasos)) AlternarPasos();
            if (promptRaiz != null && promptRaiz.activeSelf && promptFrame < Time.frameCount - 1) promptRaiz.SetActive(false);
            if (avisoGrupo != null)
            {
                float objetivoAlfa = Time.unscaledTime < avisoHasta ? 1f : 0f;
                avisoGrupo.alpha = Mathf.MoveTowards(avisoGrupo.alpha, objetivoAlfa, Time.unscaledDeltaTime * 2.5f);
            }
            if (ilumGrupo != null)
            {
                float objetivoIlum = ilumPedido ? 1f : 0f;
                ilumGrupo.alpha = Mathf.MoveTowards(ilumGrupo.alpha, objetivoIlum, Time.unscaledDeltaTime * (ilumPedido ? 5f : 2.5f));
                if (ilumTexto != null) ilumTexto.color = Color.Lerp(new Color(1f, 0.97f, 0.8f), new Color(1f, 0.55f, 0.35f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
            }
            bool alumbrado = SP.Operacion.ReflectorVigia.PoseidoIluminado;
            if (alumbrado || ilumRaiz != null) Iluminado(alumbrado);
            if (timerGrupo != null) timerGrupo.alpha = timerVisible ? Mathf.Clamp01(1f - (avisoGrupo != null ? avisoGrupo.alpha : 0f) * 1.5f) : 0f;
            SP.UI.ModeToastView.SubirSobreElTemporizador(timerVisible);   // WP11: el aviso del modo no se encima con el temporizador
        }
    }
}
