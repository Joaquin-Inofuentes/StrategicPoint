using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SP.UI;
using SP.Combat;
using SP.Presentation;

namespace SP.EditorTools
{
    // Configura e inicializa el panel de arma y municion (WeaponStatus) en modo edicion
    // para que se vea IDENTICO a como se ve en modo Play.
    //
    // Ubicacion: esquina inferior DERECHA (anchors 1,0; pivot 1,0; x=-11.5, y=10.2).
    // De este modo las unidades (Roster) quedan a la izquierda y el arma a la derecha.
    //
    // Dimensiones del panel: 136 x 112 px (angosto y alto).
    // - Columna izquierda: icono de arma grande (52x104 px).
    // - Columna derecha (apilada):
    //   * Texto municion cargador / reserva (ej. "30 / 90").
    //   * Cuchillo: atajo [F] + icono de daga.
    //   * Granadas: atajo [G] + icono de granada + contador (ej. "3").
    //   * Barra de recarga/enfriamiento al pie (BarBG + BarFill en verde).
    public static class WeaponStatusUiPipeline
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";

        [MenuItem("Strategic Point/UI/3. Simplificar panel de arma (angosto, apilado)")]
        public static void Simplificar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Gameplay"))
            {
                EditorSceneManager.OpenScene(ScenePath);
                scene = EditorSceneManager.GetActiveScene();
            }

            var panel = GameObject.Find("WeaponStatus");
            if (panel == null)
            {
                Debug.LogError("[WeaponStatusUiPipeline] No se encontro 'WeaponStatus' en la escena.");
                return;
            }

            ConstruirLayoutCompleto(panel);

            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WeaponStatusUiPipeline] Panel de arma inicializado a la DERECHA e identico a modo Play. Escena guardada.");
        }

        public static void ConstruirLayoutCompleto(GameObject panel)
        {
            // 1. Configurar RectTransform del Panel a la DERECHA
            var panelRt = panel.GetComponent<RectTransform>();
            if (panelRt != null)
            {
                panelRt.anchorMin = new Vector2(1f, 0f);
                panelRt.anchorMax = new Vector2(1f, 0f);
                panelRt.pivot = new Vector2(1f, 0f);
                panelRt.sizeDelta = new Vector2(136f, 112f);
                panelRt.anchoredPosition = new Vector2(-11.5f, 10.2f);
            }

            // Fondo del panel
            var panelImg = panel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color(0.08f, 0.1f, 0.12f, 0.85f);
            }

            var view = panel.GetComponent<WeaponStatusView>();
            if (view == null)
            {
                view = panel.AddComponent<WeaponStatusView>();
            }

            // 2. Icono de arma (columna izquierda, 52x104 px)
            var iconT = panel.transform.Find("Icono");
            GameObject iconGo;
            if (iconT == null)
            {
                iconGo = new GameObject("Icono", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(panel.transform, false);
                iconT = iconGo.transform;
            }
            else
            {
                iconGo = iconT.gameObject;
            }
            var iconRt = (RectTransform)iconT;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0f);
            iconRt.pivot = new Vector2(0f, 0f);
            iconRt.sizeDelta = new Vector2(52f, 104f);
            iconRt.anchoredPosition = new Vector2(4f, 4f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;
            iconImg.sprite = WeaponStatusView.IconFor(WeaponKind.Rifle);
            iconImg.color = Color.white;

            // Conectar serialized field icon en WeaponStatusView
            var so = new SerializedObject(view);
            var iconProp = so.FindProperty("icon");
            if (iconProp != null)
            {
                iconProp.objectReferenceValue = iconImg;
                so.ApplyModifiedProperties();
            }

            // 3. Texto de munición (arriba derecha: "30 / 90")
            var textT = panel.transform.Find("Text");
            if (textT == null)
            {
                var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel.transform, false);
                textT = go.transform;
            }
            var textRt = (RectTransform)textT;
            textRt.anchorMin = textRt.anchorMax = new Vector2(0f, 0f);
            textRt.pivot = new Vector2(0f, 0f);
            textRt.sizeDelta = new Vector2(70f, 26f);
            textRt.anchoredPosition = new Vector2(60f, 84f);
            var textComp = textT.GetComponent<Text>();
            if (textComp != null)
            {
                textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textComp.alignment = TextAnchor.MiddleLeft;
                textComp.fontSize = 22;
                textComp.fontStyle = FontStyle.Bold;
                textComp.verticalOverflow = VerticalWrapMode.Overflow;
                textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
                textComp.color = Color.white;
                textComp.raycastTarget = false;
                textComp.text = "30 / 90";
            }

            // 4. Cuchillo [F] (fila media-alta)
            var cuchilloLabelT = panel.transform.Find("ExtrasCuchilloLabel");
            if (cuchilloLabelT == null)
            {
                var go = new GameObject("ExtrasCuchilloLabel", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel.transform, false);
                cuchilloLabelT = go.transform;
            }
            var cLabelRt = (RectTransform)cuchilloLabelT;
            cLabelRt.anchorMin = cLabelRt.anchorMax = new Vector2(0f, 0f);
            cLabelRt.pivot = new Vector2(0f, 0f);
            cLabelRt.sizeDelta = new Vector2(16f, 30f);
            cLabelRt.anchoredPosition = new Vector2(60f, 58f);
            var cLabelText = cuchilloLabelT.GetComponent<Text>();
            cLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cLabelText.fontSize = 15;
            cLabelText.fontStyle = FontStyle.Bold;
            cLabelText.alignment = TextAnchor.MiddleCenter;
            cLabelText.color = new Color(0.75f, 0.78f, 0.82f);
            cLabelText.raycastTarget = false;
            cLabelText.text = "F";

            var cuchilloT = panel.transform.Find("ExtrasCuchillo");
            if (cuchilloT == null)
            {
                var go = new GameObject("ExtrasCuchillo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(panel.transform, false);
                cuchilloT = go.transform;
            }
            var cuchilloRt = (RectTransform)cuchilloT;
            cuchilloRt.anchorMin = cuchilloRt.anchorMax = new Vector2(0f, 0f);
            cuchilloRt.pivot = new Vector2(0f, 0f);
            cuchilloRt.sizeDelta = new Vector2(30f, 30f);
            cuchilloRt.anchoredPosition = new Vector2(78f, 54f);
            var cuchilloImg = cuchilloT.GetComponent<Image>();
            cuchilloImg.raycastTarget = false;
            cuchilloImg.preserveAspect = true;
            cuchilloImg.sprite = HudIconFactory.Cuchillo();
            cuchilloImg.color = new Color(0.9f, 0.94f, 0.98f);

            // 5. Granada [G] (fila media-baja)
            var granadaLabelT = panel.transform.Find("ExtrasGranadaLabel");
            if (granadaLabelT == null)
            {
                var go = new GameObject("ExtrasGranadaLabel", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel.transform, false);
                granadaLabelT = go.transform;
            }
            var gLabelRt = (RectTransform)granadaLabelT;
            gLabelRt.anchorMin = gLabelRt.anchorMax = new Vector2(0f, 0f);
            gLabelRt.pivot = new Vector2(0f, 0f);
            gLabelRt.sizeDelta = new Vector2(16f, 30f);
            gLabelRt.anchoredPosition = new Vector2(60f, 22f);
            var gLabelText = granadaLabelT.GetComponent<Text>();
            gLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gLabelText.fontSize = 15;
            gLabelText.fontStyle = FontStyle.Bold;
            gLabelText.alignment = TextAnchor.MiddleCenter;
            gLabelText.color = new Color(0.75f, 0.78f, 0.82f);
            gLabelText.raycastTarget = false;
            gLabelText.text = "G";

            var granadaT = panel.transform.Find("ExtrasGranada");
            if (granadaT == null)
            {
                var go = new GameObject("ExtrasGranada", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(panel.transform, false);
                granadaT = go.transform;
            }
            var granadaRt = (RectTransform)granadaT;
            granadaRt.anchorMin = granadaRt.anchorMax = new Vector2(0f, 0f);
            granadaRt.pivot = new Vector2(0f, 0f);
            granadaRt.sizeDelta = new Vector2(30f, 30f);
            granadaRt.anchoredPosition = new Vector2(78f, 18f);
            var granadaImg = granadaT.GetComponent<Image>();
            granadaImg.raycastTarget = false;
            granadaImg.preserveAspect = true;
            granadaImg.sprite = HudIconFactory.Granada();
            granadaImg.color = new Color(1f, 0.79f, 0.29f);

            var granadaCountT = panel.transform.Find("ExtrasGranadaCount");
            if (granadaCountT == null)
            {
                var go = new GameObject("ExtrasGranadaCount", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(panel.transform, false);
                granadaCountT = go.transform;
            }
            var gCountRt = (RectTransform)granadaCountT;
            gCountRt.anchorMin = gCountRt.anchorMax = new Vector2(0f, 0f);
            gCountRt.pivot = new Vector2(0f, 0f);
            gCountRt.sizeDelta = new Vector2(28f, 30f);
            gCountRt.anchoredPosition = new Vector2(110f, 18f);
            var gCountText = granadaCountT.GetComponent<Text>();
            gCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gCountText.fontSize = 18;
            gCountText.fontStyle = FontStyle.Bold;
            gCountText.alignment = TextAnchor.MiddleLeft;
            gCountText.raycastTarget = false;
            gCountText.color = new Color(1f, 0.79f, 0.29f);
            gCountText.text = "3";

            // 6. Barra de recarga / preparación al pie (BarBG / BarFill)
            var barT = panel.transform.Find("BarBG");
            if (barT == null)
            {
                var go = new GameObject("BarBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(panel.transform, false);
                barT = go.transform;
            }
            var barRt = (RectTransform)barT;
            barRt.anchorMin = barRt.anchorMax = new Vector2(0f, 0f);
            barRt.pivot = new Vector2(0f, 0f);
            barRt.sizeDelta = new Vector2(72f, 10f);
            barRt.anchoredPosition = new Vector2(60f, 4f);
            var barImg = barT.GetComponent<Image>();
            if (barImg != null)
            {
                barImg.color = new Color(0.12f, 0.14f, 0.16f, 0.9f);
            }

            var rotulo = barT.Find("Rotulo");
            if (rotulo != null) Object.DestroyImmediate(rotulo.gameObject);

            var fillT = barT.Find("BarFill");
            if (fillT == null)
            {
                var go = new GameObject("BarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(barT, false);
                fillT = go.transform;
            }
            var fillRt = (RectTransform)fillT;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillT.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.color = new Color(0.4f, 0.85f, 0.45f);
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillAmount = 1f;
            }

            // 7. Reloj (inactivo cuando está lista)
            var relojT = panel.transform.Find("Reloj");
            if (relojT == null)
            {
                var go = new GameObject("Reloj", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(panel.transform, false);
                relojT = go.transform;
            }
            var relojRt = (RectTransform)relojT;
            relojRt.anchorMin = relojRt.anchorMax = new Vector2(0f, 0f);
            relojRt.pivot = new Vector2(0f, 0f);
            relojRt.sizeDelta = new Vector2(22f, 22f);
            relojRt.anchoredPosition = new Vector2(36f, 4f);
            var relojImg = relojT.GetComponent<Image>();
            relojImg.raycastTarget = false;
            relojImg.sprite = HudIconFactory.Reloj();
            relojImg.color = new Color(1f, 0.75f, 0.35f);
            relojT.gameObject.SetActive(false);

            // 8. Limpiar elementos obsoletos
            var extrasViejo = panel.transform.Find("Extras");
            if (extrasViejo != null) Object.DestroyImmediate(extrasViejo.gameObject);
        }
    }
}
