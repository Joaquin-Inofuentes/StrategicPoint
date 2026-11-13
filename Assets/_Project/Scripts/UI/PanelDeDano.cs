using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Tarjeta "DANO" de Configuraciones (pedido 2026-11-13): un slider y un numero editable para el dano PROPIO (lo que hacen vos y tu escuadra) y otro para el
    // dano ENEMIGO (lo que recibe tu bando). Los dos son coeficientes globales (AjustesDeDano) y se guardan solos. Va pegada al costado derecho de la tarjeta de
    // Configuraciones, como hija suya (se muestra y se oculta con ella) y fuera de su layout.
    public sealed class PanelDeDano : MonoBehaviour
    {
        public const string Nombre = "AjusteDeDano";
        Slider sPropio, sEnemigo; InputField iPropio, iEnemigo; Text infoPropio, infoEnemigo;

        public static PanelDeDano Asegurar(GameObject settingsPanel)
        {
            if (settingsPanel == null) return null;
            var viejo = settingsPanel.transform.Find(Nombre);
            if (viejo != null) { var v = viejo.GetComponent<PanelDeDano>(); if (v != null) { v.Refrescar(); return v; } Destroy(viejo.gameObject); }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject(Nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(settingsPanel.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = new Vector2(24f, 0f); rt.sizeDelta = new Vector2(470f, 372f);
            go.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.96f);
            var vista = go.AddComponent<PanelDeDano>();
            vista.Construir(rt, font);
            vista.Refrescar();
            return vista;
        }

        void OnEnable() { Refrescar(); }

        public void Refrescar()
        {
            if (sPropio == null) return;
            Poner(sPropio, iPropio, infoPropio, AjustesDeDano.Propio, true);
            Poner(sEnemigo, iEnemigo, infoEnemigo, AjustesDeDano.Enemigo, false);
        }

        static void Poner(Slider s, InputField i, Text info, float v, bool propio)
        {
            s.SetValueWithoutNotify(v); i.SetTextWithoutNotify(v.ToString("0.00", CultureInfo.InvariantCulture));
            info.text = propio ? $"Tu bando le hace {v * 100f:0} % del dano normal a los enemigos." : $"Los enemigos le hacen {v * 100f:0} % del dano normal a tu bando.";
        }

        void Construir(RectTransform card, Font font)
        {
            var dorado = new Color(0.95f, 0.78f, 0.3f); var claro = new Color(0.92f, 0.93f, 0.96f); var tenue = new Color(0.65f, 0.68f, 0.75f);
            Texto(card, font, "Titulo", "DAÑO", 26, TextAnchor.MiddleLeft, dorado, new Vector2(24f, -14f), new Vector2(300f, 36f));
            Texto(card, font, "Sub", "Coeficientes globales, valen en todos los niveles", 14, TextAnchor.MiddleLeft, tenue, new Vector2(24f, -48f), new Vector2(420f, 22f));
            Bloque(card, font, "DAÑO PROPIO", -86f, out sPropio, out iPropio, out infoPropio, v => { AjustesDeDano.Propio = v; Poner(sPropio, iPropio, infoPropio, AjustesDeDano.Propio, true); }, claro, tenue, dorado);
            Bloque(card, font, "DAÑO ENEMIGO", -208f, out sEnemigo, out iEnemigo, out infoEnemigo, v => { AjustesDeDano.Enemigo = v; Poner(sEnemigo, iEnemigo, infoEnemigo, AjustesDeDano.Enemigo, false); }, claro, tenue, dorado);
            // restablecer
            var b = new GameObject("Restablecer", typeof(RectTransform), typeof(Image), typeof(Button));
            b.transform.SetParent(card, false);
            var brt = (RectTransform)b.transform; brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f); brt.anchoredPosition = new Vector2(24f, -326f); brt.sizeDelta = new Vector2(250f, 32f);
            b.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);
            var bt = Texto(brt, font, "Label", "RESTABLECER (PROPIO 1,00 · ENEMIGO 0,50)", 13, TextAnchor.MiddleCenter, claro, Vector2.zero, Vector2.zero, true);
            b.GetComponent<Button>().onClick.AddListener(() => { AjustesDeDano.Restablecer(); Refrescar(); });
        }

        void Bloque(RectTransform card, Font font, string titulo, float y, out Slider slider, out InputField campo, out Text info, System.Action<float> alCambiar, Color claro, Color tenue, Color dorado)
        {
            Texto(card, font, "L_" + titulo, titulo, 18, TextAnchor.MiddleLeft, claro, new Vector2(24f, y), new Vector2(300f, 26f));
            // slider
            var sgo = new GameObject(titulo + "_Slider", typeof(RectTransform), typeof(Slider));
            sgo.transform.SetParent(card, false);
            var srt = (RectTransform)sgo.transform; srt.anchorMin = srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 1f); srt.anchoredPosition = new Vector2(24f, y - 38f); srt.sizeDelta = new Vector2(310f, 26f);
            var fondo = Imagen(srt, "Fondo", new Color(0.2f, 0.22f, 0.28f, 1f)); Estirar(fondo.rectTransform, 0f, 0.3f, 1f, 0.7f, 0f, 0f, 0f, 0f);
            var areaRelleno = new GameObject("AreaRelleno", typeof(RectTransform)); areaRelleno.transform.SetParent(srt, false);
            var arrt = (RectTransform)areaRelleno.transform; Estirar(arrt, 0f, 0.3f, 1f, 0.7f, 0f, 0f, 0f, 0f);
            var relleno = Imagen(arrt, "Relleno", dorado); relleno.rectTransform.anchorMin = Vector2.zero; relleno.rectTransform.anchorMax = Vector2.one; relleno.rectTransform.offsetMin = relleno.rectTransform.offsetMax = Vector2.zero;
            var areaPerilla = new GameObject("AreaPerilla", typeof(RectTransform)); areaPerilla.transform.SetParent(srt, false);
            var aprt = (RectTransform)areaPerilla.transform; Estirar(aprt, 0f, 0f, 1f, 1f, 11f, 0f, -11f, 0f);
            var perilla = Imagen(aprt, "Perilla", Color.white); perilla.rectTransform.sizeDelta = new Vector2(22f, 26f);
            slider = sgo.GetComponent<Slider>();
            slider.fillRect = relleno.rectTransform; slider.handleRect = perilla.rectTransform; slider.targetGraphic = perilla; slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = AjustesDeDano.Minimo; slider.maxValue = AjustesDeDano.Maximo; slider.wholeNumbers = false;
            // relleno acotado por el ancla (el Slider lo hace con fillRect; la Image es lisa)
            // numero editable
            var igo = new GameObject(titulo + "_Numero", typeof(RectTransform), typeof(Image), typeof(InputField));
            igo.transform.SetParent(card, false);
            var irt = (RectTransform)igo.transform; irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f); irt.pivot = new Vector2(0f, 1f); irt.anchoredPosition = new Vector2(350f, y - 34f); irt.sizeDelta = new Vector2(96f, 34f);
            igo.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 1f);
            var txt = Texto(irt, font, "Texto", "1.00", 20, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.zero, true);
            campo = igo.GetComponent<InputField>(); campo.textComponent = txt; campo.contentType = InputField.ContentType.DecimalNumber; campo.characterLimit = 5; campo.targetGraphic = igo.GetComponent<Image>();
            info = Texto(card, font, "I_" + titulo, "", 13, TextAnchor.UpperLeft, tenue, new Vector2(24f, y - 72f), new Vector2(420f, 36f));
            var sl = slider; var ca = campo;
            sl.onValueChanged.AddListener(v => alCambiar(v));
            ca.onEndEdit.AddListener(t =>
            {
                if (float.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) alCambiar(f); else alCambiar(sl.value);
            });
        }

        static Image Imagen(Transform padre, string nombre, Color c)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image)); go.transform.SetParent(padre, false);
            var im = go.GetComponent<Image>(); im.color = c; return im;
        }

        static void Estirar(RectTransform r, float ax0, float ay0, float ax1, float ay1, float ox0, float oy0, float ox1, float oy1)
        {
            r.anchorMin = new Vector2(ax0, ay0); r.anchorMax = new Vector2(ax1, ay1); r.offsetMin = new Vector2(ox0, oy0); r.offsetMax = new Vector2(ox1, oy1);
        }

        static Text Texto(Transform padre, Font font, string nombre, string s, int size, TextAnchor ancla, Color color, Vector2 pos, Vector2 tam, bool estirado = false)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text)); go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = ancla; t.fontStyle = FontStyle.Bold; t.text = s; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = (RectTransform)go.transform;
            if (estirado) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(6f, 2f); r.offsetMax = new Vector2(-6f, -2f); }
            else { r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f); r.anchoredPosition = pos; r.sizeDelta = tam; }
            return t;
        }
    }
}
