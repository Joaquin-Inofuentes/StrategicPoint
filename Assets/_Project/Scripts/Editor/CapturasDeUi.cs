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
                // Ronda 13 (T3): el panel REAL de Configuraciones (PanelAjustesExtra.Preparar arma las secciones, incluida DAÑO, y LayoutDeAjustes las acomoda).
                int w = 1280, h = 720;
                crt.sizeDelta = new Vector2(w, h);
                var panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(cgo.transform, false);
                var prt = (RectTransform)panel.transform; prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                var tgo = new GameObject("Title", typeof(RectTransform), typeof(Text)); tgo.transform.SetParent(prt, false);
                var tt = tgo.GetComponent<Text>(); tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tt.text = "CONFIGURACIONES";
                PanelAjustesExtra.Preparar(panel);
                var vista = panel.GetComponent<PanelDeDano>();
                Canvas.ForceUpdateCanvases();
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
