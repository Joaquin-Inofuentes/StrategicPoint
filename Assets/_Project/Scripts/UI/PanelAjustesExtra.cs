using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Arma y viste la pantalla de Configuraciones. Los sliders y toggles ya estan en la escena (PauseController les conecta el
    // valor por nombre); aca se le AGREGAN las secciones (SONIDO, INTERFAZ, MIRA Y CONTROL, PANTALLA), el modo de pantalla, la
    // resolucion con su boton APLICAR y el idioma, se los viste (EstiloDeAjustes) y se los acomoda (LayoutDeAjustes).
    // Se prepara solo la primera vez que se abre y es idempotente.
    //
    // Quitado a pedido: tamano de interfaz, daltonismo, HUD minimo y subtitulos ya no tienen boton aca (las funciones siguen
    // existiendo en AjustesDeJuego, pero no se muestran).
    public static class PanelAjustesExtra
    {
        static readonly string[] Ocultos = { "MisionHud", "MinimapBorder", "GroupCards", "SelectionCount", "MissionStatus", "PerfHud" };

        public static void Preparar(GameObject settingsPanel)
        {
            if (settingsPanel == null) return;
            var rt = (RectTransform)settingsPanel.transform;

            // Restos de versiones anteriores del panel (bloque de la derecha con accesibilidad): se descartan.
            var viejo = SP.Core.BuscarHijo.Ruta(rt, "AjustesExtra");
            if (viejo != null)
            {
                if (Application.isPlaying) Object.Destroy(viejo.gameObject); else Object.DestroyImmediate(viejo.gameObject);
            }

            if (SP.Core.BuscarHijo.Ruta(rt, "Pantalla") == null) Construir(rt);
            EstiloDeAjustes.Vestir(rt);
            Refrescar(rt);
            var layout = LayoutDeAjustes.Asegurar(settingsPanel);
            layout.Aplicar();
        }

        static void Construir(RectTransform panel)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Seccion(panel, font, "Sonido", "SONIDO");
            Seccion(panel, font, "Interfaz", "INTERFAZ");
            Seccion(panel, font, "Control", "MIRA Y CONTROL");
            Seccion(panel, font, "Pantalla", "PANTALLA");
            Separador(panel, "TituloSep", 0.3f);

            Boton(panel, font, "Pantalla", "MODO", () => { AjustesDeJuego.AlternarPantallaCompleta(); Refrescar(panel); });
            Texto(panel, font, "ResolucionLabel", "RESOLUCION", 14, TextAnchor.MiddleLeft, EstiloDeAjustes.Atenuado);
            CrearDropdown(panel, font, "Resolucion");
            Boton(panel, font, "Idioma", "IDIOMA / LANGUAGE", () => { SP.Core.Loc.Alternar(); Refrescar(panel); });
            Texto(panel, font, "Nota", "Mando: stick izq. mover, stick der. mirar, RT disparar, A saltar, B agacharse, X recargar, RB/LB cambiar arma, Start pausa.", 12, TextAnchor.MiddleCenter, EstiloDeAjustes.Atenuado);
        }

        // Titulo de seccion + linea dorada debajo ("Sec_<n>" y "SecSep_<n>"; el boton "Pantalla" se llama igual que la seccion, por eso el prefijo).
        static void Seccion(Transform padre, Font font, string id, string titulo)
        {
            Texto(padre, font, "Sec_" + id, titulo, 15, TextAnchor.MiddleLeft, EstiloDeAjustes.Dorado, FontStyle.Bold);
            Separador(padre, "SecSep_" + id, 0.45f);
        }

        static void Separador(Transform padre, string nombre, float alfa)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var im = go.GetComponent<Image>();
            var d = EstiloDeAjustes.Dorado;
            im.color = new Color(d.r, d.g, d.b, alfa);
            im.raycastTarget = false;
        }

        static void Texto(Transform padre, Font font, string nombre, string s, int size, TextAnchor anchor, Color color, FontStyle estilo = FontStyle.Bold)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = anchor; t.fontStyle = estilo; t.text = s; t.color = color; t.raycastTarget = false;
        }

        static void Boton(Transform padre, Font font, string nombre, string titulo, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            go.GetComponent<Image>().color = EstiloDeAjustes.FondoBoton;
            var b = go.GetComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(accion);
            EstiloDeAjustes.VestirBoton(go.transform);

            var t = new GameObject("Label", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(go.transform, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(0.55f, 1f); trt.offsetMin = new Vector2(16f, 2f); trt.offsetMax = new Vector2(-4f, -2f);
            var tx = t.GetComponent<Text>();
            tx.font = font; tx.fontSize = 18; tx.fontStyle = FontStyle.Bold; tx.alignment = TextAnchor.MiddleLeft; tx.color = EstiloDeAjustes.Etiqueta; tx.raycastTarget = false; tx.text = titulo;
            tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 11; tx.resizeTextMaxSize = 18;

            var v = new GameObject("Value", typeof(RectTransform), typeof(Text));
            v.transform.SetParent(go.transform, false);
            var vrt = (RectTransform)v.transform;
            vrt.anchorMin = new Vector2(0.55f, 0f); vrt.anchorMax = new Vector2(1f, 1f); vrt.offsetMin = new Vector2(4f, 2f); vrt.offsetMax = new Vector2(-16f, -2f);
            var vx = v.GetComponent<Text>();
            vx.font = font; vx.fontSize = 18; vx.fontStyle = FontStyle.Bold; vx.alignment = TextAnchor.MiddleRight; vx.color = EstiloDeAjustes.Dorado; vx.raycastTarget = false; vx.text = "";
            vx.resizeTextForBestFit = true; vx.resizeTextMinSize = 11; vx.resizeTextMaxSize = 18;
        }

        static Image ImagenDe(Transform raiz, string ruta)
        {
            var t = SP.Core.BuscarHijo.Ruta(raiz, ruta);
            return t != null ? t.GetComponent<Image>() : null;
        }

        static void CrearDropdown(Transform padre, Font font, string nombre)
        {
            var go = UnityEngine.UI.DefaultControls.CreateDropdown(new UnityEngine.UI.DefaultControls.Resources());
            go.name = nombre;
            go.transform.SetParent(padre, false);
            var d = go.GetComponent<Dropdown>();
            d.ClearOptions();
            var opts = new List<string>();
            foreach (var r in AjustesDeJuego.Resoluciones()) opts.Add($"{r.width}x{r.height}");
            d.AddOptions(opts);
            d.value = AjustesDeJuego.IndiceResolucion();
            d.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetInt("sp_resolucion", v); PlayerPrefs.Save();
                AjustesDeJuego.AplicarPantalla();
                SP.Presentation.AudioDirector.PlayUi2D(SP.Presentation.SfxKind.UiClick, 0.55f, 0.85f);
            });

            // Estilo oscuro (el desplegable por defecto es blanco con letra negra y desentona con todo lo demas).
            var fondo = go.GetComponent<Image>(); if (fondo != null) { fondo.sprite = EstiloDeAjustes.Redondeado(); fondo.type = Image.Type.Sliced; fondo.color = EstiloDeAjustes.FondoBoton; }
            foreach (var t in go.GetComponentsInChildren<Text>(true)) { t.font = font; t.fontSize = 18; t.color = Color.white; t.fontStyle = FontStyle.Bold; }
            var flecha = ImagenDe(go.transform, "Arrow"); if (flecha != null) flecha.color = EstiloDeAjustes.Dorado;
            var plantilla = ImagenDe(go.transform, "Template"); if (plantilla != null) { plantilla.sprite = EstiloDeAjustes.Redondeado(); plantilla.type = Image.Type.Sliced; plantilla.color = new Color(0.07f, 0.09f, 0.14f, 1f); }
            var itemBg = ImagenDe(go.transform, "Template/Viewport/Content/Item/Item Background"); if (itemBg != null) itemBg.color = EstiloDeAjustes.FondoBoton;
            var marca = ImagenDe(go.transform, "Template/Viewport/Content/Item/Item Checkmark"); if (marca != null) marca.color = EstiloDeAjustes.Dorado;
            var barra = ImagenDe(go.transform, "Template/Scrollbar"); if (barra != null) barra.color = EstiloDeAjustes.Pista;
            var mango = ImagenDe(go.transform, "Template/Scrollbar/Sliding Area/Handle"); if (mango != null) { mango.sprite = EstiloDeAjustes.Redondeado(); mango.type = Image.Type.Sliced; mango.color = EstiloDeAjustes.Dorado; }
            d.transition = Selectable.Transition.ColorTint;
            var c = d.colors; c.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f); c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); d.colors = c;
            go.AddComponent<SelectorSfx>();

            // Boton APLICAR a la derecha de la misma fila (aplica la resolucion elegida sin depender del cambio de valor).
            var btnGo = new GameObject("Aplicar", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(padre, false);
            btnGo.GetComponent<Image>().color = EstiloDeAjustes.FondoAplicar;
            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = btnGo.GetComponent<Image>();
            btn.onClick.AddListener(() => AjustesDeJuego.AplicarPantalla());
            EstiloDeAjustes.VestirBoton(btnGo.transform);
            btnGo.GetComponent<Image>().color = EstiloDeAjustes.FondoAplicar;
            var btnTxt = new GameObject("Label", typeof(RectTransform), typeof(Text));
            btnTxt.transform.SetParent(btnGo.transform, false);
            var btrt = (RectTransform)btnTxt.transform;
            btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = btrt.offsetMax = Vector2.zero;
            var btx = btnTxt.GetComponent<Text>();
            btx.font = font; btx.fontSize = 18; btx.fontStyle = FontStyle.Bold; btx.alignment = TextAnchor.MiddleCenter; btx.color = Color.white; btx.text = "APLICAR"; btx.raycastTarget = false;
        }

        public static void Refrescar(Transform panel)
        {
            if (panel == null) return;
            Poner(panel, "Pantalla", SP.Core.Loc.T(AjustesDeJuego.PantallaCompleta ? "COMPLETA" : "VENTANA"));
            Poner(panel, "Idioma", SP.Core.Loc.T(SP.Core.Loc.Actual == SP.Core.Idioma.Es ? "ESPANOL" : "ENGLISH"));
            var dd = SP.Core.BuscarHijo.Ruta(panel, "Resolucion");
            var drop = dd != null ? dd.GetComponent<Dropdown>() : null;
            if (drop != null && drop.value != AjustesDeJuego.IndiceResolucion()) drop.SetValueWithoutNotify(AjustesDeJuego.IndiceResolucion());
        }

        static void Poner(Transform panel, string boton, string texto)
        {
            var t = SP.Core.BuscarHijo.Ruta(panel, boton);
            var vt = SP.Core.BuscarHijo.Ruta(t, "Value");
            var tx = vt != null ? vt.GetComponent<Text>() : null;
            if (tx != null) tx.text = texto;
        }

        // HUD minimo: oculta lo secundario (F10). No tiene boton en Configuraciones pero la tecla sigue.
        public static void AplicarHudMinimo(Transform canvas)
        {
            if (canvas == null) return;
            foreach (var n in Ocultos)
            {
                var t = canvas.Find(n);
                if (t == null) continue;
                // Escala 0 (y no solo alfa): varios de estos paneles tienen su propio Canvas o reescriben su alfa cada frame.
                t.localScale = AjustesDeJuego.HudMinimo ? Vector3.zero : Vector3.one;
            }
        }
    }

    // Sonido del desplegable de resolucion: al pasar el mouse (el clic lo cubre el cambio de valor y el propio despliegue).
    public class SelectorSfx : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerClickHandler
    {
        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { SP.Presentation.AudioDirector.PlayUi2D(SP.Presentation.SfxKind.UiHover, 0.3f, 0.3f); }
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { SP.Presentation.AudioDirector.PlayUi2D(SP.Presentation.SfxKind.UiClick, 0.45f, 0.8f); }
    }
}
