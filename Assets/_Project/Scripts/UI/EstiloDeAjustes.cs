using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.UI
{
    // Aspecto y respuesta de la pantalla de Configuraciones. Pedido explicito: "la UI de scrolls corrigela, que sea mejor;
    // toda interaccion tiene su sonido de feedback y colores; mejora la estetica y la diagramacion".
    //
    // Los objetos (sliders, toggles, botones) son los de la escena: PauseController los busca por nombre y les conecta el
    // valor. Aca solo se los VISTE en tiempo de ejecucion (sprites redondeados generados por codigo, pista y relleno finos,
    // perilla circular, interruptor tipo pildora) y se les agrega su respuesta: resalte al pasar el mouse, sonido al entrar,
    // al arrastrar (un "tic" por tramo), al soltar y al conmutar. Nada de esto se serializa en la escena.
    public static class EstiloDeAjustes
    {
        public static readonly Color FondoTarjeta = new Color(0.055f, 0.07f, 0.105f, 1f);
        public static readonly Color Borde = new Color(1f, 0.82f, 0.3f, 0.55f);
        public static readonly Color Pista = new Color(0.13f, 0.16f, 0.23f, 1f);
        public static readonly Color PistaResaltada = new Color(0.19f, 0.23f, 0.32f, 1f);
        public static readonly Color Etiqueta = new Color(0.9f, 0.93f, 0.98f, 1f);
        public static readonly Color Dorado = new Color(1f, 0.82f, 0.3f, 1f);
        public static readonly Color Atenuado = new Color(0.66f, 0.72f, 0.82f, 1f);
        public static readonly Color FondoBoton = new Color(0.15f, 0.21f, 0.31f, 1f);
        public static readonly Color FondoAplicar = new Color(0.17f, 0.42f, 0.28f, 1f);
        public static readonly Color AcentoSonido = new Color(0.25f, 0.76f, 0.96f, 1f);
        public static readonly Color AcentoControl = new Color(0.78f, 0.45f, 0.95f, 1f);
        public static readonly Color AcentoInterfaz = new Color(0.36f, 0.86f, 0.52f, 1f);
        public static readonly Color ChipFondo = new Color(0.03f, 0.04f, 0.07f, 0.9f);

        static Sprite redondeado, circulo, contorno;

        public static Sprite Redondeado()
        {
            if (redondeado == null) redondeado = Sliced(20, 8, false);
            return redondeado;
        }
        public static Sprite Contorno()
        {
            if (contorno == null) contorno = Sliced(24, 3, true);
            return contorno;
        }
        public static Sprite Circulo()
        {
            if (circulo == null)
            {
                const int n = 64;
                var tex = Nueva(n);
                var pix = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                        pix[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n * 0.5f - d) * 255f));
                    }
                tex.SetPixels32(pix); tex.Apply(false, true);
                circulo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                circulo.hideFlags = HideFlags.HideAndDontSave;
            }
            return circulo;
        }

        static Texture2D Nueva(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        // Rectangulo de esquinas redondeadas para Image.Type.Sliced; "anillo" deja solo el borde (grosor en px).
        static Sprite Sliced(int radio, int grosor, bool anillo)
        {
            int n = radio * 2 + 8;
            var tex = Nueva(n);
            var pix = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radio, n - radio), cy = Mathf.Clamp(py, radio, n - radio);
                    // Distancia firmada al borde del rectangulo redondeado: negativa adentro.
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) - radio;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (anillo) a *= Mathf.Clamp01(grosor + d + 0.5f);
                    pix[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pix); tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radio, radio, radio, radio));
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        public static Color AcentoDe(string nombre)
        {
            if (nombre.StartsWith("Sensibilidad")) return AcentoControl;
            if (nombre == "Volumen" || nombre == "General" || nombre == "VFX" || nombre == "Voces") return AcentoSonido;
            return AcentoInterfaz;
        }

        // ------------------------------------------------------------------ vestido (idempotente)
        public static void Vestir(RectTransform panel)
        {
            if (panel == null) return;
            var fondo = panel.GetComponent<Image>();
            if (fondo != null)
            {
                fondo.sprite = Redondeado(); fondo.type = Image.Type.Sliced; fondo.color = FondoTarjeta; fondo.raycastTarget = true;
            }
            if (SP.Core.BuscarHijo.Ruta(panel, "Borde") == null)
            {
                var b = new GameObject("Borde", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)b.transform;
                rt.SetParent(panel, false);
                rt.SetAsFirstSibling();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                var im = b.GetComponent<Image>();
                im.sprite = Contorno(); im.type = Image.Type.Sliced; im.color = Borde; im.raycastTarget = false;
            }

            foreach (Transform h in panel)
            {
                if (h.name.EndsWith("_Slider")) VestirSlider(h.GetComponent<Slider>(), h.name.Substring(0, h.name.Length - 7), panel);
                else if (h.name.EndsWith("_Toggle")) VestirInterruptor(h.GetComponent<Toggle>(), h.name.Substring(0, h.name.Length - 7), panel);
                else if (h.name.EndsWith("_Label") || h.name.EndsWith("_Value")) EstiloDeTexto(h.GetComponent<Text>(), h.name.EndsWith("_Value"));
            }
            var titulo = SP.Core.BuscarHijo.Ruta(panel, "Title");
            var tt = titulo != null ? titulo.GetComponent<Text>() : null;
            if (tt != null) { tt.color = Color.white; tt.fontSize = 30; tt.fontStyle = FontStyle.Bold; tt.alignment = TextAnchor.MiddleCenter; }
            VestirBoton(SP.Core.BuscarHijo.Ruta(panel, "BackButton"), true);
        }

        static void EstiloDeTexto(Text t, bool valor)
        {
            if (t == null) return;
            t.fontStyle = FontStyle.Bold;
            t.fontSize = valor ? 16 : 18;
            t.color = valor ? Dorado : Etiqueta;
            t.raycastTarget = false;
        }

        static void VestirSlider(Slider s, string nombre, RectTransform panel)
        {
            if (s == null) return;
            s.transition = Selectable.Transition.None;
            var acento = AcentoDe(nombre);
            var srt = (RectTransform)s.transform;

            var pista = SP.Core.BuscarHijo.Ruta(srt, "Background") as RectTransform;
            if (pista != null)
            {
                pista.anchorMin = new Vector2(0f, 0.5f); pista.anchorMax = new Vector2(1f, 0.5f);
                pista.offsetMin = new Vector2(0f, -5f); pista.offsetMax = new Vector2(0f, 5f);
                var im = pista.GetComponent<Image>();
                if (im != null) { im.sprite = Redondeado(); im.type = Image.Type.Sliced; im.color = Pista; im.raycastTarget = false; }
            }
            var zonaRelleno = SP.Core.BuscarHijo.Ruta(srt, "Fill Area") as RectTransform;
            if (zonaRelleno != null)
            {
                zonaRelleno.anchorMin = new Vector2(0f, 0.5f); zonaRelleno.anchorMax = new Vector2(1f, 0.5f);
                zonaRelleno.offsetMin = new Vector2(0f, -5f); zonaRelleno.offsetMax = new Vector2(0f, 5f);
            }
            var relleno = SP.Core.BuscarHijo.Ruta(srt, "Fill Area/Fill");
            var rim = relleno != null ? relleno.GetComponent<Image>() : null;
            if (rim != null) { rim.sprite = Redondeado(); rim.type = Image.Type.Sliced; rim.color = acento; rim.raycastTarget = false; }

            var zonaPerilla = SP.Core.BuscarHijo.Ruta(srt, "Handle Slide Area") as RectTransform;
            if (zonaPerilla != null)
            {
                // Alto CERO centrado: Slider.UpdateVisuals le pone al Handle anclas Y 0..1 y, con la zona estirada a todo el
                // alto del slider, la perilla se deformaba (22x46). Con la zona de alto cero mide siempre sizeDelta (22x22).
                zonaPerilla.anchorMin = new Vector2(0f, 0.5f); zonaPerilla.anchorMax = new Vector2(1f, 0.5f);
                zonaPerilla.offsetMin = new Vector2(11f, 0f); zonaPerilla.offsetMax = new Vector2(-11f, 0f);
            }
            var perilla = SP.Core.BuscarHijo.Ruta(srt, "Handle Slide Area/Handle") as RectTransform;
            Image perillaIm = null, nucleo = null;
            if (perilla != null)
            {
                perilla.anchorMin = new Vector2(perilla.anchorMin.x, 0.5f); perilla.anchorMax = new Vector2(perilla.anchorMax.x, 0.5f);
                perilla.sizeDelta = new Vector2(22f, 22f);
                perillaIm = perilla.GetComponent<Image>();
                if (perillaIm != null) { perillaIm.sprite = Circulo(); perillaIm.type = Image.Type.Simple; perillaIm.preserveAspect = true; perillaIm.color = Color.white; perillaIm.raycastTarget = true; }
                var n = SP.Core.BuscarHijo.Ruta(perilla, "Nucleo");
                if (n == null)
                {
                    var g = new GameObject("Nucleo", typeof(RectTransform), typeof(Image));
                    var r = (RectTransform)g.transform;
                    r.SetParent(perilla, false);
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(10f, 10f);
                    nucleo = g.GetComponent<Image>();
                    nucleo.sprite = Circulo(); nucleo.raycastTarget = false;
                }
                else nucleo = n.GetComponent<Image>();
                nucleo.color = acento;
            }

            var comp = s.GetComponent<SliderDeAjuste>();
            if (comp == null) comp = s.gameObject.AddComponent<SliderDeAjuste>();
            var chip = SP.Core.BuscarHijo.Ruta(panel, nombre + "_Chip");
            var valor = SP.Core.BuscarHijo.Ruta(panel, nombre + "_Value");
            if (chip == null && valor != null)
            {
                var g = new GameObject(nombre + "_Chip", typeof(RectTransform), typeof(Image));
                var r = (RectTransform)g.transform;
                r.SetParent(panel, false);
                r.SetSiblingIndex(valor.GetSiblingIndex());
                var im = g.GetComponent<Image>();
                im.sprite = Redondeado(); im.type = Image.Type.Sliced; im.color = ChipFondo; im.raycastTarget = false;
                chip = g.transform;
            }
            // Reubica la perilla y el relleno con las anclas nuevas (SetValue no redibuja si el valor no cambia: se lo mueve y se lo devuelve).
            float v0 = s.value;
            s.SetValueWithoutNotify(Mathf.Approximately(v0, s.minValue) ? s.maxValue : s.minValue);
            s.SetValueWithoutNotify(v0);
            comp.Configurar(s, pista != null ? pista.GetComponent<Image>() : null, perilla, nucleo, chip != null ? chip.GetComponent<Image>() : null, valor != null ? valor.GetComponent<Text>() : null, acento);
        }

        static void VestirInterruptor(Toggle t, string nombre, RectTransform panel)
        {
            if (t == null) return;
            t.transition = Selectable.Transition.None;
            var raiz = (RectTransform)t.transform;
            var fondo = SP.Core.BuscarHijo.Ruta(raiz, "Background") as RectTransform;
            if (fondo == null) return;
            // La casilla original (Background + Checkmark) pasa a ser una pildora con perilla; el rotulo va DENTRO del toggle, asi
            // toda la fila se puede clicar.
            var marca = SP.Core.BuscarHijo.Ruta(fondo, "Checkmark");
            if (marca != null) { t.graphic = null; marca.gameObject.SetActive(false); }
            fondo.anchorMin = fondo.anchorMax = new Vector2(0f, 0.5f);
            fondo.pivot = new Vector2(0f, 0.5f);
            fondo.sizeDelta = new Vector2(48f, 24f);
            fondo.anchoredPosition = new Vector2(2f, 0f);
            var fim = fondo.GetComponent<Image>();
            if (fim != null) { fim.sprite = Redondeado(); fim.type = Image.Type.Sliced; fim.raycastTarget = true; t.targetGraphic = fim; }

            var perilla = SP.Core.BuscarHijo.Ruta(fondo, "Perilla") as RectTransform;
            if (perilla == null)
            {
                var g = new GameObject("Perilla", typeof(RectTransform), typeof(Image));
                perilla = (RectTransform)g.transform;
                perilla.SetParent(fondo, false);
                perilla.anchorMin = perilla.anchorMax = new Vector2(0f, 0.5f);
                perilla.pivot = new Vector2(0.5f, 0.5f);
                perilla.sizeDelta = new Vector2(18f, 18f);
                var pim = g.GetComponent<Image>();
                pim.sprite = Circulo(); pim.color = Color.white; pim.raycastTarget = false;
            }

            var etiqueta = SP.Core.BuscarHijo.Ruta(raiz, nombre + "_Label") ?? SP.Core.BuscarHijo.Ruta(panel, nombre + "_Label");
            if (etiqueta != null)
            {
                etiqueta.SetParent(raiz, false);
                var er = (RectTransform)etiqueta;
                er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one;
                er.offsetMin = new Vector2(62f, 0f); er.offsetMax = new Vector2(0f, 0f);
                var tx = etiqueta.GetComponent<Text>();
                if (tx != null) { EstiloDeTexto(tx, false); tx.alignment = TextAnchor.MiddleLeft; }
            }
            var comp = t.GetComponent<InterruptorDeAjuste>();
            if (comp == null) comp = t.gameObject.AddComponent<InterruptorDeAjuste>();
            comp.Configurar(t, fim, perilla);
        }

        // Boton de la pantalla: mismo skin del juego, resalte por tinte y sonido (ButtonSfx ya da hover + click).
        public static void VestirBoton(Transform boton, bool principal = false)
        {
            if (boton == null) return;
            var b = boton.GetComponent<Button>();
            if (b == null) return;
            var c = b.colors;
            c.normalColor = Color.white; c.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); c.selectedColor = Color.white; c.fadeDuration = 0.08f;
            b.colors = c;
            b.transition = Selectable.Transition.ColorTint;
            if (b.targetGraphic == null) b.targetGraphic = b.GetComponent<Image>();
            var im = b.GetComponent<Image>();
            if (im != null) im.color = principal ? new Color(0.24f, 0.36f, 0.56f, 1f) : FondoBoton;
            SP.UI.ButtonSfx.Attach(b);
        }
    }

    // ---------------------------------------------------------------------------------------------- slider
    // Resalte al pasar el mouse (pista mas clara, perilla mas grande), tic de sonido cada tramo mientras se arrastra, click al
    // tomar y al soltar, y un destello dorado en el numero cuando cambia.
    public class SliderDeAjuste : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Slider slider;
        Image pista, chip, nucleo;
        RectTransform perilla;
        Text valor;
        Color acento;
        bool encima, apretado;
        float ultimoTic, destello;
        const float TramoDeTic = 0.05f;

        // Para quien necesite aplicar el valor SOLO al soltar (tamano de HUD): Apretado dice si hay un puntero sobre la perilla y
        // Soltado avisa al levantarlo (o si el slider se apaga a mitad del arrastre).
        public bool Apretado => apretado;
        public event System.Action<Slider> Soltado;

        public void Configurar(Slider s, Image pistaIm, RectTransform perillaRt, Image nucleoIm, Image chipIm, Text valorTx, Color acentoColor)
        {
            if (slider != null) slider.onValueChanged.RemoveListener(AlCambiar);
            slider = s; pista = pistaIm; perilla = perillaRt; nucleo = nucleoIm; chip = chipIm; valor = valorTx; acento = acentoColor;
            ultimoTic = slider != null ? slider.normalizedValue : 0f;
            if (slider != null) slider.onValueChanged.AddListener(AlCambiar);
        }

        void OnDisable()
        {
            bool estabaApretado = apretado;
            encima = apretado = false; destello = 0f; Pintar(0f);
            if (estabaApretado) Soltado?.Invoke(slider);
        }
        void OnDestroy() { if (slider != null) slider.onValueChanged.RemoveListener(AlCambiar); }

        void AlCambiar(float _)
        {
            destello = 1f;
            if (slider == null) return;
            float n = slider.normalizedValue;
            if (Mathf.Abs(n - ultimoTic) >= TramoDeTic || n <= 0.001f && ultimoTic > 0.001f || n >= 0.999f && ultimoTic < 0.999f)
            {
                ultimoTic = n;
                AudioDirector.PlayUi2D(SfxKind.RadialTick, 0.3f, 0.25f);
            }
        }

        public void OnPointerEnter(PointerEventData e) { encima = true; AudioDirector.PlayUi2D(SfxKind.UiHover, 0.3f, 0.3f); }
        public void OnPointerExit(PointerEventData e) { encima = false; }
        public void OnPointerDown(PointerEventData e) { apretado = true; AudioDirector.PlayUi2D(SfxKind.UiClick, 0.45f, 0.8f); }
        public void OnPointerUp(PointerEventData e) { apretado = false; AudioDirector.PlayUi2D(SfxKind.UiClick, 0.55f, 0.85f); Soltado?.Invoke(slider); }

        void Update() => Pintar(Time.unscaledDeltaTime);

        void Pintar(float dt)
        {
            if (destello > 0f) destello = Mathf.Max(0f, destello - dt * 4f);
            float objetivo = apretado ? 1.32f : encima ? 1.16f : 1f;
            if (perilla != null)
            {
                float k = Mathf.MoveTowards(perilla.localScale.x, objetivo, dt * 5f);
                perilla.localScale = new Vector3(k, k, 1f);
            }
            if (pista != null) pista.color = encima || apretado ? EstiloDeAjustes.PistaResaltada : EstiloDeAjustes.Pista;
            if (nucleo != null) nucleo.color = apretado ? Color.Lerp(acento, Color.white, 0.35f) : acento;
            if (valor != null) valor.color = Color.Lerp(EstiloDeAjustes.Dorado, Color.white, destello);
            if (chip != null) chip.color = Color.Lerp(EstiloDeAjustes.ChipFondo, new Color(acento.r * 0.35f, acento.g * 0.35f, acento.b * 0.35f, 0.95f), Mathf.Max(destello, encima || apretado ? 0.6f : 0f));
        }
    }

    // ---------------------------------------------------------------------------------------------- interruptor
    // Pildora con perilla que se desliza: verde y perilla a la derecha = activo. Suena al pasar el mouse y al conmutar (un
    // sonido distinto para encender y para apagar).
    public class InterruptorDeAjuste : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Toggle toggle;
        Image fondo;
        RectTransform perilla;
        float k;
        bool encima;
        static readonly Color Apagado = new Color(0.16f, 0.19f, 0.27f, 1f);
        static readonly Color Encendido = new Color(0.22f, 0.62f, 0.38f, 1f);

        public void Configurar(Toggle t, Image fondoIm, RectTransform perillaRt)
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(AlConmutar);
            toggle = t; fondo = fondoIm; perilla = perillaRt;
            if (toggle != null) { toggle.onValueChanged.AddListener(AlConmutar); k = toggle.isOn ? 1f : 0f; }
            Pintar();
        }

        void OnEnable() { if (toggle != null) k = toggle.isOn ? 1f : 0f; Pintar(); }
        void OnDestroy() { if (toggle != null) toggle.onValueChanged.RemoveListener(AlConmutar); }

        void AlConmutar(bool encendido)
        {
            AudioDirector.PlayUi2D(encendido ? SfxKind.RadialConfirm : SfxKind.UiClick, encendido ? 0.4f : 0.55f, 0.85f);
        }

        public void OnPointerEnter(PointerEventData e) { encima = true; AudioDirector.PlayUi2D(SfxKind.UiHover, 0.3f, 0.3f); }
        public void OnPointerExit(PointerEventData e) { encima = false; }

        void Update()
        {
            if (toggle == null) return;
            k = Mathf.MoveTowards(k, toggle.isOn ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            Pintar();
        }

        void Pintar()
        {
            if (fondo == null || perilla == null || toggle == null) return;
            var c = Color.Lerp(Apagado, Encendido, k);
            fondo.color = encima ? Color.Lerp(c, Color.white, 0.12f) : c;
            var rt = (RectTransform)fondo.transform;
            float x = Mathf.Lerp(12f, rt.sizeDelta.x - 12f, k);
            perilla.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
