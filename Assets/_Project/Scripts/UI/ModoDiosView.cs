using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Cartel fijo "MODO DIOS [F4]" mientras nadie de tu bando recibe dano: se ve siempre para que
    // nadie juegue una partida entera invencible sin darse cuenta. Late en dorado.
    public class ModoDiosView : MonoBehaviour
    {
        Text texto;
        CanvasGroup grupo;

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
            rt.anchoredPosition = new Vector2(0f, 96f);
            rt.sizeDelta = new Vector2(360f, 34f);

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(go.transform, false);
            var img = fondo.GetComponent<Image>();
            img.color = new Color(0.25f, 0.19f, 0.02f, 0.85f);
            img.raycastTarget = false;
            var frt = (RectTransform)fondo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

            var tGO = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            tGO.transform.SetParent(go.transform, false);
            var tx = tGO.GetComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = 16; tx.fontStyle = FontStyle.Bold;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = new Color(1f, 0.85f, 0.25f);
            tx.raycastTarget = false;
            tx.text = "★  MODO DIOS  ·  [F4] apagar";
            var trt = (RectTransform)tGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

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
