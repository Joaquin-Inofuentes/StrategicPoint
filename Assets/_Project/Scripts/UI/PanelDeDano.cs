using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Logica de la seccion "DAÑO" de Configuraciones (pedido 2026-11-13; Ronda 13 T3: ya NO es una tarjeta suelta pegada al costado, que se veia al 25 %
    // porque quedaba fuera de la pantalla). Las filas las arma PanelAjustesExtra.ConstruirDano ("Daño propio_Slider/_Chip/_Value/_Label" y lo mismo para
    // "Daño enemigo") y las acomoda LayoutDeAjustes como una seccion mas de la columna derecha. Aca solo se ENLAZA: el slider y el numero editable (el chip)
    // escriben en AjustesDeDano (coeficientes globales, se guardan solos) y se refrescan al abrir la pantalla o al restablecer.
    [DisallowMultipleComponent]
    public sealed class PanelDeDano : MonoBehaviour
    {
        public const string Nombre = "AjusteDeDano";   // la tarjeta suelta de versiones anteriores: si existe, se descarta
        Slider sPropio, sEnemigo; InputField iPropio, iEnemigo; Text vPropio, vEnemigo;
        bool enlazado;
        UnityEngine.Events.UnityAction<float> alSliderPropio, alSliderEnemigo;

        public static PanelDeDano Asegurar(GameObject settingsPanel)
        {
            if (settingsPanel == null) return null;
            var viejo = settingsPanel.transform.Find(Nombre);
            if (viejo != null) { if (Application.isPlaying) Destroy(viejo.gameObject); else DestroyImmediate(viejo.gameObject); }
            var v = settingsPanel.GetComponent<PanelDeDano>();
            if (v == null) v = settingsPanel.AddComponent<PanelDeDano>();
            v.Enlazar();
            v.Refrescar();
            return v;
        }

        public static void RefrescarEn(Transform panel)
        {
            var v = panel != null ? panel.GetComponent<PanelDeDano>() : null;
            if (v != null) v.Refrescar();
        }

        void OnEnable() { if (enlazado) Refrescar(); }

        void Enlazar()
        {
            if (enlazado && sPropio != null && sEnemigo != null) return;
            var t = (RectTransform)transform;
            sPropio = Buscar<Slider>(t, PanelAjustesExtra.FilaPropio + "_Slider"); sEnemigo = Buscar<Slider>(t, PanelAjustesExtra.FilaEnemigo + "_Slider");
            iPropio = Buscar<InputField>(t, PanelAjustesExtra.FilaPropio + "_Chip"); iEnemigo = Buscar<InputField>(t, PanelAjustesExtra.FilaEnemigo + "_Chip");
            vPropio = Buscar<Text>(t, PanelAjustesExtra.FilaPropio + "_Value"); vEnemigo = Buscar<Text>(t, PanelAjustesExtra.FilaEnemigo + "_Value");
            if (sPropio == null || sEnemigo == null) return;
            Cablear(sPropio, iPropio, vPropio, true);
            Cablear(sEnemigo, iEnemigo, vEnemigo, false);
            enlazado = true;
        }

        static T Buscar<T>(Transform raiz, string nombre) where T : Component
        {
            var h = BuscarHijo.Ruta(raiz, nombre);
            return h != null ? h.GetComponent<T>() : null;
        }

        void Cablear(Slider s, InputField campo, Text valor, bool propio)
        {
            // Ronda 16: RemoveAllListeners tambien sacaba el AlCambiar de SliderDeAjuste (tic sonoro y destello del chip). Se quita solo el propio.
            if (propio) { if (alSliderPropio != null) s.onValueChanged.RemoveListener(alSliderPropio); alSliderPropio = v => Aplicar(true, v); s.onValueChanged.AddListener(alSliderPropio); }
            else { if (alSliderEnemigo != null) s.onValueChanged.RemoveListener(alSliderEnemigo); alSliderEnemigo = v => Aplicar(false, v); s.onValueChanged.AddListener(alSliderEnemigo); }
            if (campo != null)
            {
                campo.textComponent = valor; campo.contentType = InputField.ContentType.DecimalNumber; campo.characterLimit = 5;
                campo.targetGraphic = campo.GetComponent<Image>(); campo.lineType = InputField.LineType.SingleLine;
                campo.onEndEdit.RemoveAllListeners();
                campo.onEndEdit.AddListener(t =>
                {
                    if (float.TryParse((t ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) Aplicar(propio, f);
                    else Refrescar();
                });
            }
        }

        // Un cambio del slider o del numero: se limpia (0,1 a 3), se guarda en AjustesDeDano y se repinta todo.
        void Aplicar(bool propio, float v)
        {
            if (propio) AjustesDeDano.Propio = v; else AjustesDeDano.Enemigo = v;
            Refrescar();
        }

        public void Refrescar()
        {
            if (sPropio == null || sEnemigo == null) return;
            Poner(sPropio, iPropio, vPropio, AjustesDeDano.Propio);
            Poner(sEnemigo, iEnemigo, vEnemigo, AjustesDeDano.Enemigo);
        }

        static void Poner(Slider s, InputField i, Text valor, float v)
        {
            s.SetValueWithoutNotify(v);
            string txt = v.ToString("0.00", CultureInfo.InvariantCulture);
            if (i != null && !i.isFocused) i.SetTextWithoutNotify(txt);
            else if (valor != null && (i == null || !i.isFocused)) valor.text = txt;
        }
    }
}
