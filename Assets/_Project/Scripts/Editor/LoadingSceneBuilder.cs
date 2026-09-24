using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.EditorTools
{
    // Arma la escena intermedia de carga: nada mas que fondo oscuro, un texto
    // "CARGANDO" y una barra de progreso. Mismo patron que MenuSceneBuilder: todo
    // por codigo, nada de arrastrar objetos a mano en el Editor.
    public static class LoadingSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/SC_Loading.unity";

        // Oscuro a proposito (pedido explicito): esta pantalla aparece ENTRE
        // otras escenas -- el menu, la partida de dia, la de noche -- y un fondo
        // claro haria un flash de por medio en cada cambio. Con un fondo oscuro
        // parejo el salto se nota menos sea cual sea la escena de origen o destino.
        static readonly Color ColorFondo = new Color(0.03f, 0.035f, 0.045f);

        [MenuItem("Strategic Point/Construir escena de carga")]
        public static void BuildLoadingScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("MainCamera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColorFondo;
            camGO.AddComponent<AudioListener>();

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Misma referencia que el menu y el HUD (960x540): la barra mide lo mismo en cualquier resolucion.
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;

            var fondoGO = new GameObject("Fondo", typeof(Image));
            fondoGO.transform.SetParent(canvasGO.transform, false);
            fondoGO.GetComponent<Image>().color = ColorFondo;
            var fondoRt = fondoGO.GetComponent<RectTransform>();
            fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
            fondoRt.offsetMin = fondoRt.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var textoGO = new GameObject("Texto", typeof(Text));
            textoGO.transform.SetParent(canvasGO.transform, false);
            var texto = textoGO.GetComponent<Text>();
            texto.font = font;
            texto.text = "CARGANDO...";
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = new Color(0.85f, 0.88f, 0.95f);
            texto.fontSize = 22;
            texto.fontStyle = FontStyle.Bold;
            texto.raycastTarget = false;
            var textoRt = textoGO.GetComponent<RectTransform>();
            textoRt.anchorMin = textoRt.anchorMax = new Vector2(0.5f, 0.5f);
            textoRt.anchoredPosition = new Vector2(0f, 40f);
            textoRt.sizeDelta = new Vector2(400f, 40f);

            var pistaGO = new GameObject("BarraFondo", typeof(Image));
            pistaGO.transform.SetParent(canvasGO.transform, false);
            pistaGO.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.23f);
            var pistaRt = pistaGO.GetComponent<RectTransform>();
            pistaRt.anchorMin = pistaRt.anchorMax = new Vector2(0.5f, 0.5f);
            pistaRt.anchoredPosition = Vector2.zero;
            pistaRt.sizeDelta = new Vector2(360f, 18f);

            var rellenoGO = new GameObject("BarraRelleno", typeof(Image));
            rellenoGO.transform.SetParent(pistaGO.transform, false);
            var relleno = rellenoGO.GetComponent<Image>();
            // Mismo dorado que ya se usa de acento en el menu (LineaTitulo).
            relleno.color = new Color(0.98f, 0.82f, 0.25f, 0.95f);
            relleno.type = Image.Type.Filled;
            relleno.fillMethod = Image.FillMethod.Horizontal;
            relleno.fillOrigin = (int)Image.OriginHorizontal.Left;
            relleno.fillAmount = 0f;
            var rellenoRt = rellenoGO.GetComponent<RectTransform>();
            rellenoRt.anchorMin = Vector2.zero; rellenoRt.anchorMax = Vector2.one;
            rellenoRt.offsetMin = rellenoRt.offsetMax = Vector2.zero;

            // Pedido explicito: "que se vea el porcentaje de carga lentamente" --
            // numero debajo de la barra, mismo dorado que el relleno.
            var porcentajeGO = new GameObject("Porcentaje", typeof(Text));
            porcentajeGO.transform.SetParent(canvasGO.transform, false);
            var porcentaje = porcentajeGO.GetComponent<Text>();
            porcentaje.font = font;
            porcentaje.text = "0%";
            porcentaje.alignment = TextAnchor.MiddleCenter;
            porcentaje.color = new Color(0.98f, 0.82f, 0.25f, 0.95f);
            porcentaje.fontSize = 18;
            porcentaje.fontStyle = FontStyle.Bold;
            porcentaje.raycastTarget = false;
            var porcentajeRt = porcentajeGO.GetComponent<RectTransform>();
            porcentajeRt.anchorMin = porcentajeRt.anchorMax = new Vector2(0.5f, 0.5f);
            porcentajeRt.anchoredPosition = new Vector2(0f, -34f);
            porcentajeRt.sizeDelta = new Vector2(200f, 30f);

            var controllerGO = new GameObject("LoadingScreen", typeof(LoadingScreenController));
            var controller = controllerGO.GetComponent<LoadingScreenController>();
            controller.barra = relleno;
            controller.textoPorcentaje = porcentaje;

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log("[LoadingSceneBuilder] Escena de carga construida en " + ScenePath);
        }

        // Igual que TutorialSceneBuilder.AgregarAlBuild: suma la escena si falta,
        // sin pisar lo que ya haya en Build Settings (a diferencia de
        // MenuSceneBuilder, que reescribe la lista entera -- por eso esa lista
        // tambien incluye ahora SC_Loading, ver MenuSceneBuilder.RegisterScenesInBuildSettings).
        public static void RegisterInBuildSettings()
        {
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in lista) if (s.path == ScenePath) return;
            lista.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }
    }
}
