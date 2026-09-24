using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Presentation;

namespace SP.UI
{
    // Cartel fijo "MODO DIOS [F4]" mientras nadie de tu bando recibe dano: se ve siempre para que
    // nadie juegue una partida entera invencible sin darse cuenta. Late en dorado.
    public class ModoDiosView : MonoBehaviour
    {
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static ModoDiosView Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable() => RegistrarActivo();
        Text texto;
        CanvasGroup grupo;

        // Pedido explicito: "q este mas abajo, mas alineado al filo
        // inferior, mas delgado y mas corto, y un icono q lo distinga".
        // Antes: 360x34 flotando a 96px del piso con "★ MODO DIOS · [F4]
        // apagar". Ahora: caja angosta de 190x22 pegada casi al borde
        // (8px), texto recortado a lo esencial, y un escudo (HudIconFactory)
        // en vez de la estrella de texto para que se lea sin depender del
        // color dorado.
        public static ModoDiosView Asegurar(Transform canvasRoot)
        {
            if (canvasRoot == null) return null;
            var t = canvasRoot.Find("ModoDiosView");
            if (t != null) return t.GetComponent<ModoDiosView>();

            var go = new GameObject("ModoDiosView", typeof(RectTransform), typeof(CanvasGroup), typeof(ModoDiosView));
            go.transform.SetParent(canvasRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            rt.sizeDelta = new Vector2(190f, 22f);

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(go.transform, false);
            var img = fondo.GetComponent<Image>();
            img.color = new Color(0.25f, 0.19f, 0.02f, 0.85f);
            img.raycastTarget = false;
            var frt = (RectTransform)fondo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

            var iconGO = new GameObject("Icono", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var irt = (RectTransform)iconGO.transform;
            irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(16f, 16f);
            irt.anchoredPosition = new Vector2(4f, 0f);
            var iimg = iconGO.GetComponent<Image>();
            iimg.sprite = HudIconFactory.Escudo();
            iimg.color = new Color(1f, 0.85f, 0.25f);
            iimg.raycastTarget = false;
            iimg.preserveAspect = true;

            var tGO = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            tGO.transform.SetParent(go.transform, false);
            var tx = tGO.GetComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = 13; tx.fontStyle = FontStyle.Bold;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = new Color(1f, 0.85f, 0.25f);
            tx.raycastTarget = false;
            tx.text = "MODO DIOS · F4";
            var trt = (RectTransform)tGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(22f, 0f); trt.offsetMax = Vector2.zero;

            var v = go.GetComponent<ModoDiosView>();
            v.texto = tx;
            v.grupo = go.GetComponent<CanvasGroup>();
            v.grupo.interactable = false; v.grupo.blocksRaycasts = false;
            v.grupo.alpha = ModoDios.Activo ? 1f : 0f;
            return v;
        }

        void Update()
        {
            if (grupo == null) return;
            float objetivo = ModoDios.Activo ? 1f : 0f;
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, Time.unscaledDeltaTime * 6f);
            if (ModoDios.Activo && texto != null)
                texto.color = Color.Lerp(new Color(1f, 0.7f, 0.15f), new Color(1f, 0.95f, 0.5f), (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f);
        }
    }
}
