using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SP.UI;

namespace SP.EditorTools
{
    // Foto de la tarjeta DANO de Configuraciones sin entrar en Play (los objetos son temporales y no se guardan en ninguna escena).
    public static class CapturasDeUi
    {
        public static string FotoPanelDeDano(string ruta)
        {
            var raiz = new GameObject("TMP_FotoUi") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camGo = new GameObject("Cam", typeof(Camera)); camGo.transform.SetParent(raiz.transform, false); camGo.hideFlags = HideFlags.HideAndDontSave;
                var cam = camGo.GetComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f); cam.orthographic = true; cam.cullingMask = ~0;
                var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); cgo.transform.SetParent(raiz.transform, false);
                var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
                var crt = (RectTransform)cgo.transform;
                // tarjeta de configuraciones de mentira (solo para dar contexto de posicion) + la tarjeta real de DANO a su derecha
                var panel = new GameObject("PanelFalso", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(cgo.transform, false);
                var prt = (RectTransform)panel.transform; prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(1f, 1f); prt.anchoredPosition = new Vector2(-30f, 190f); prt.sizeDelta = new Vector2(540f, 380f);
                panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.96f);
                var t = new GameObject("T", typeof(RectTransform), typeof(Text)); t.transform.SetParent(prt, false);
                var tx = t.GetComponent<Text>(); tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tx.text = "CONFIGURACIONES\n(sonido, interfaz, mira y control, pantalla...)"; tx.alignment = TextAnchor.MiddleCenter; tx.fontSize = 24; tx.color = new Color(0.7f, 0.72f, 0.8f);
                var trt = (RectTransform)t.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                var vista = PanelDeDano.Asegurar(panel);
                // el panel falso es pivot (1,1) y la tarjeta se ancla a su esquina (1,1): queda a la derecha
                Canvas.ForceUpdateCanvases();
                int w = 1280, h = 560;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt; cam.orthographicSize = h / 2f;
                crt.sizeDelta = new Vector2(w, h);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                RenderTexture.active = prev; cam.targetTexture = null;
                File.WriteAllBytes(ruta, tex.EncodeToJPG(92));
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                return "OK " + ruta + (vista != null ? " (tarjeta creada)" : " (sin tarjeta)");
            }
            finally { Object.DestroyImmediate(raiz); }
        }
    }
}
