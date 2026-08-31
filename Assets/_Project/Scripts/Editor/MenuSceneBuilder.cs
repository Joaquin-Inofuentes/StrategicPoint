using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using SP.Presentation;

namespace SP.EditorTools
{
    // Arma la escena de menú principal: fondo, título, botón Jugar y
    // botón Salir. Mismo patrón que HeadlessTestRunner: todo por código,
    // nada de arrastrar objetos a mano en el Editor.
    public static class MenuSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_MainMenu.unity";

        [MenuItem("Strategic Point/Construir menu principal")]
        public static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("MainCamera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camGO.AddComponent<AudioListener>();

            // InputSystemUIInputModule, no StandaloneInputModule: el
            // proyecto tiene Active Input Handling = "Input System
            // Package (New)" exclusivo (activeInputHandler=1), y el
            // módulo viejo no lee ratón/teclado de ese sistema -- sin
            // esto los botones se ven pero nunca reciben el click real.
            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var titleGO = new GameObject("Title", typeof(Text));
            titleGO.transform.SetParent(canvasGO.transform, false);
            var titleTxt = titleGO.GetComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;
            titleTxt.fontSize = 64;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.text = "STRATEGIC POINT";
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.65f);
            titleRt.anchorMax = new Vector2(0.5f, 0.65f);
            titleRt.sizeDelta = new Vector2(900f, 100f);

            var menuGO = new GameObject("MainMenu", typeof(RectTransform), typeof(MainMenuController));
            menuGO.transform.SetParent(canvasGO.transform, false);
            var menuController = menuGO.GetComponent<MainMenuController>();

            var playBtn = BuildButton(canvasGO.transform, "PlayButton", "JUGAR", new Vector2(0f, 20f), new Color(0.25f, 0.6f, 0.35f));
            playBtn.onClick.AddListener(menuController.OnPlayClicked);

            var exitBtn = BuildButton(canvasGO.transform, "ExitButton", "SALIR", new Vector2(0f, -60f), new Color(0.6f, 0.25f, 0.25f));
            exitBtn.onClick.AddListener(menuController.OnExitClicked);

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScenesInBuildSettings();

            Debug.Log("[MenuSceneBuilder] Escena de menu principal construida en " + ScenePath);
        }

        static Button BuildButton(Transform canvasParent, string name, string label, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(canvasParent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(260f, 56f);

            var textGO = new GameObject("Text", typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var txt = textGO.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        // SceneManager.LoadScene(name) necesita que la escena esté en Build
        // Settings -- si no, tira "Scene couldn't be loaded" en Play mode.
        public static void RegisterScenesInBuildSettings()
        {
            var menu = new EditorBuildSettingsScene(ScenePath, true);
            var gameplay = new EditorBuildSettingsScene("Assets/_Project/Scenes/SC_Gameplay.unity", true);
            var testLevel = new EditorBuildSettingsScene("Assets/_Project/Scenes/SC_TestLevel.unity", true);
            EditorBuildSettings.scenes = new[] { menu, gameplay, testLevel };
        }
    }
}
