using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SP.Core
{
    // Pedido: "cuando apriete U para reportar, que me abra un cuadro de dialogo para escribir, que se ponga en PAUSA hasta que
    // escriba y de a confirmar, que guarde una captura para validar y quiero ver una MINIATURA de la captura del bug".
    //
    //  1. [U]/[8]: se saca la captura y la foto del estado ANTES de mostrar nada (el dialogo no sale en la imagen).
    //  2. Pausa total (timeScale 0) + cursor libre; el juego no recibe teclas mientras se escribe (SesionLog.DialogoAbierto).
    //  3. Dialogo: "REPORTAR BUG #N", miniatura de la captura, campo de texto, CONFIRMAR (Ctrl+Enter) / CANCELAR (Esc).
    //  4. CONFIRMAR guarda json (estado + diagnostico completo + nota) y png; CANCELAR descarta y no gasta numero.
    public class ReporteDeBug : MonoBehaviour
    {
        public static ReporteDeBug Instancia { get; private set; }
        public static bool Abierto { get; private set; }
        public static int NumeroPendiente { get; private set; }
        public static Texture2D CapturaPendiente { get; private set; }
        // Costura de pruebas (CLI sin teclado real).
        public static bool PruebaAbrir;

        EstadoDeSesion estadoPendiente;
        float escalaPrevia = 1f;
        CursorLockMode cursorPrevio; bool cursorVisiblePrevio;
        bool audioPausadoPrevio;

        GameObject raiz;
        RawImage miniatura;
        InputField campo;
        Text titulo, ayuda;

        public static ReporteDeBug Asegurar()
        {
            if (Instancia != null) return Instancia;
            if (SesionLog.Instancia == null) return null;
            Instancia = SesionLog.Instancia.gameObject.AddComponent<ReporteDeBug>();
            return Instancia;
        }

        void OnDestroy()
        {
            if (Instancia != this) return;
            // #113: si la escena se descarga con el dialogo abierto, no dejar el juego congelado (casquillos y escombros "flotando").
            if (Abierto) { Time.timeScale = escalaPrevia > 0f ? escalaPrevia : 1f; AudioListener.pause = audioPausadoPrevio; }
            Instancia = null; Abierto = false;
        }

        public static void Abrir()
        {
            var r = Asegurar();
            if (r == null || Abierto) return;
            r.StartCoroutine(r.RutinaAbrir());
        }

        IEnumerator RutinaAbrir()
        {
            Abierto = true;   // ya bloquea el input del juego mientras se saca la captura
            // La captura del frame que el jugador estaba viendo, sin el dialogo encima.
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); } catch { }
            CapturaPendiente = tex;
            estadoPendiente = SesionLog.Instancia != null ? SesionLog.Instancia.Capturar("") : null;
            NumeroPendiente = SesionLog.ProximoNumeroDeBug;

            escalaPrevia = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            audioPausadoPrevio = AudioListener.pause;
            AudioListener.pause = true;
            cursorPrevio = Cursor.lockState; cursorVisiblePrevio = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

            Construir();
            titulo.text = $"REPORTAR BUG #{NumeroPendiente:000}";
            miniatura.texture = tex;
            if (tex != null)
            {
                var fit = miniatura.GetComponent<AspectRatioFitter>();
                if (fit != null) fit.aspectRatio = (float)tex.width / Mathf.Max(1, tex.height);
            }
            campo.text = "";
            textoPrevio = ""; caretPrevio = 0;
            raiz.SetActive(true);
            yield return null;
            AsegurarEventSystem();
            campo.Select();
            campo.ActivateInputField();
            SesionLog.Evento($"Dialogo de bug abierto (#{NumeroPendiente:000}), juego en pausa");
        }

        void Update()
        {
            if (PruebaAbrir) { PruebaAbrir = false; Abrir(); }
            if (!Abierto || raiz == null || !raiz.activeSelf) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Cancelar(); return; }
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Confirmar(campo.text);
        }

        // Bug #048: Ctrl+Borrar tiene que borrar la palabra anterior, como en cualquier editor. El InputField de uGUI no lo
        // hace (borra un caracter). Se trabaja sobre el texto/cursor que habia ANTES de que el campo procesara la tecla
        // (guardados al final del frame anterior), asi da igual si el campo ya borro un caracter este frame.
        string textoPrevio = "";
        int caretPrevio;

        void LateUpdate()
        {
            if (!Abierto || campo == null || raiz == null || !raiz.activeSelf) return;
            var kb = Keyboard.current;
            bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
            if (ctrl && kb.backspaceKey.wasPressedThisFrame && campo.isFocused)
            {
                int fin = Mathf.Clamp(caretPrevio, 0, textoPrevio.Length);
                int ini = InicioDePalabraAnterior(textoPrevio, fin);
                campo.text = textoPrevio.Substring(0, ini) + textoPrevio.Substring(fin);
                campo.caretPosition = campo.selectionAnchorPosition = campo.selectionFocusPosition = ini;
            }
            textoPrevio = campo.text ?? "";
            caretPrevio = campo.caretPosition;
        }

        // Retrocede los espacios y despues la palabra (letras/numeros); si lo que hay es puntuacion, borra ese bloque.
        public static int InicioDePalabraAnterior(string s, int fin)
        {
            int i = fin;
            while (i > 0 && char.IsWhiteSpace(s[i - 1])) i--;
            if (i > 0 && char.IsLetterOrDigit(s[i - 1])) { while (i > 0 && char.IsLetterOrDigit(s[i - 1])) i--; }
            else { while (i > 0 && !char.IsWhiteSpace(s[i - 1]) && !char.IsLetterOrDigit(s[i - 1])) i--; }
            return i;
        }

        // Tambien la usa el CLI para validar sin teclado.
        public static int Confirmar(string nota)
        {
            var r = Instancia;
            if (r == null || !Abierto) return -1;
            int n = SesionLog.MarcarBugConCaptura(nota ?? "", CapturaPendiente, r.estadoPendiente);
            r.Cerrar(false);
            return n;
        }

        public static void Cancelar()
        {
            var r = Instancia;
            if (r == null || !Abierto) return;
            SesionLog.Evento($"Reporte de bug #{NumeroPendiente:000} CANCELADO");
            r.Cerrar(true);
        }

        void Cerrar(bool descartarCaptura)
        {
            if (raiz != null) raiz.SetActive(false);
            if (miniatura != null) miniatura.texture = null;
            if (CapturaPendiente != null) Destroy(CapturaPendiente);
            CapturaPendiente = null;
            estadoPendiente = null;
            Time.timeScale = escalaPrevia;
            AudioListener.pause = audioPausadoPrevio;
            Cursor.lockState = cursorPrevio; Cursor.visible = cursorVisiblePrevio;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Abierto = false;
        }

        static void AsegurarEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem_ReporteDeBug", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        // ---- UI ----
        void Construir()
        {
            if (raiz != null) return;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            raiz = new GameObject("ReporteDeBug_Dialogo", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            raiz.transform.SetParent(transform, false);
            var canvas = raiz.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6500;
            var scaler = raiz.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var velo = Panel(raiz.transform, "Velo", new Color(0f, 0f, 0f, 0.55f));
            Estirar(velo.rectTransform);

            var caja = Panel(raiz.transform, "Caja", new Color(0.07f, 0.08f, 0.1f, 0.97f));
            caja.rectTransform.sizeDelta = new Vector2(820f, 560f);
            var borde = caja.gameObject.AddComponent<Outline>(); borde.effectColor = new Color(1f, 0.78f, 0.25f, 0.9f); borde.effectDistance = new Vector2(2f, -2f);

            titulo = Texto(caja.transform, "Titulo", font, 28, FontStyle.Bold, new Color(1f, 0.82f, 0.25f), new Vector2(0f, 248f), new Vector2(780f, 40f), TextAnchor.MiddleCenter);
            Texto(caja.transform, "Pausa", font, 14, FontStyle.Italic, new Color(0.75f, 0.8f, 0.85f), new Vector2(0f, 220f), new Vector2(780f, 22f), TextAnchor.MiddleCenter)
                .text = "JUEGO EN PAUSA · la captura y el estado ya se tomaron en el instante en que apretaste [U]";

            // Miniatura de la captura
            var marco = Panel(caja.transform, "Marco", new Color(0f, 0f, 0f, 1f));
            marco.rectTransform.anchoredPosition = new Vector2(0f, 75f);
            marco.rectTransform.sizeDelta = new Vector2(480f, 270f);
            var mgo = new GameObject("Miniatura", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            mgo.transform.SetParent(marco.transform, false);
            miniatura = mgo.GetComponent<RawImage>();
            Estirar(miniatura.rectTransform);
            var fit = mgo.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9f;
            Texto(caja.transform, "EtiquetaCaptura", font, 12, FontStyle.Normal, new Color(0.6f, 0.65f, 0.7f), new Vector2(0f, -68f), new Vector2(780f, 18f), TextAnchor.MiddleCenter)
                .text = "miniatura de la captura que se va a guardar junto al reporte";

            // Campo de texto
            Texto(caja.transform, "EtiquetaNota", font, 15, FontStyle.Bold, Color.white, new Vector2(0f, -92f), new Vector2(740f, 22f), TextAnchor.MiddleLeft)
                .text = "¿Qué pasó? (describí el bug)";
            var fondoCampo = Panel(caja.transform, "Campo", new Color(1f, 1f, 1f, 0.95f));
            fondoCampo.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            fondoCampo.rectTransform.sizeDelta = new Vector2(740f, 90f);
            var txtCampo = Texto(fondoCampo.transform, "Texto", font, 16, FontStyle.Normal, new Color(0.08f, 0.08f, 0.1f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Estirar(txtCampo.rectTransform, 8f);
            txtCampo.supportRichText = false;
            var ph = Texto(fondoCampo.transform, "Placeholder", font, 16, FontStyle.Italic, new Color(0.45f, 0.45f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            Estirar(ph.rectTransform, 8f);
            ph.text = "Ej: el medico no vino a curarme aunque se lo pedi...";
            campo = fondoCampo.gameObject.AddComponent<InputField>();
            campo.textComponent = txtCampo;
            campo.placeholder = ph;
            campo.lineType = InputField.LineType.MultiLineNewline;
            campo.characterLimit = 1000;

            // Botones
            Boton(caja.transform, "CONFIRMAR  (Ctrl+Enter)", new Vector2(-150f, -237f), new Color(0.2f, 0.62f, 0.3f), font, () => Confirmar(campo.text));
            Boton(caja.transform, "CANCELAR  (Esc)", new Vector2(150f, -237f), new Color(0.55f, 0.2f, 0.2f), font, Cancelar);
            ayuda = Texto(caja.transform, "Ayuda", font, 11, FontStyle.Normal, new Color(0.6f, 0.65f, 0.7f), new Vector2(0f, -268f), new Vector2(780f, 18f), TextAnchor.MiddleCenter);
            ayuda.text = "se guarda: captura · posicion/vida/orden de cada soldado · enemigos y cazadores · curacion · minimapa · ultimos eventos · tu nota";
            raiz.SetActive(false);
        }

        static Image Panel(Transform padre, string nombre, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var im = go.GetComponent<Image>(); im.color = color;
            return im;
        }

        static void Estirar(RectTransform rt, float margen = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen); rt.offsetMax = new Vector2(-margen, -margen);
        }

        static Text Texto(Transform padre, string nombre, Font font, int size, FontStyle style, Color color, Vector2 pos, Vector2 dim, TextAnchor anchor)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            var rt = t.rectTransform; rt.anchoredPosition = pos; rt.sizeDelta = dim;
            return t;
        }

        static void Boton(Transform padre, string texto, Vector2 pos, Color color, Font font, UnityEngine.Events.UnityAction accion)
        {
            var im = Panel(padre, "Boton_" + texto.Split(' ')[0], color);
            im.rectTransform.anchoredPosition = pos;
            im.rectTransform.sizeDelta = new Vector2(260f, 44f);
            var b = im.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            b.onClick.AddListener(accion);
            var t = Texto(im.transform, "Texto", font, 17, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            Estirar(t.rectTransform);
            t.text = texto;
            SP.UI.ButtonSfx.Attach(b);
        }
    }
}
