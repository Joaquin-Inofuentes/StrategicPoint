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
            // Misma referencia que el HUD y el menu de pausa (960x540): asi un boton de 260x56 mide lo mismo en las tres pantallas.
            scaler.referenceResolution = new Vector2(960f, 540f);
            scaler.matchWidthOrHeight = 0.5f;

            var titleGO = new GameObject("Title", typeof(Text));
            titleGO.transform.SetParent(canvasGO.transform, false);
            var titleTxt = titleGO.GetComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = Color.white;
            titleTxt.fontSize = 40;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.text = "STRATEGIC POINT";
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 172f);
            titleRt.sizeDelta = new Vector2(700f, 70f);

            // Panel oscuro detras de los botones y textos de apoyo: el menu deja de ser botones sobre fondo liso.
            var panelGO = new GameObject("PanelBotones", typeof(Image));
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelGO.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.2f, 0.9f);
            var panelRt = panelGO.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = new Vector2(0f, -20f);
            panelRt.sizeDelta = new Vector2(320f, 218f);
            var lineaGO = new GameObject("LineaTitulo", typeof(Image));
            lineaGO.transform.SetParent(canvasGO.transform, false);
            lineaGO.GetComponent<Image>().color = new Color(0.98f, 0.82f, 0.25f, 0.95f);
            var lineaRt = lineaGO.GetComponent<RectTransform>();
            lineaRt.anchorMin = lineaRt.anchorMax = new Vector2(0.5f, 0.5f);
            lineaRt.anchoredPosition = new Vector2(0f, 142f);
            lineaRt.sizeDelta = new Vector2(360f, 3f);
            CrearTextoMenu(canvasGO.transform, "Subtitulo", "COMANDO TACTICO EN PRIMERA PERSONA", 16, FontStyle.Bold, new Color(0.75f, 0.82f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(600f, 26f));
            CrearTextoMenu(canvasGO.transform, "Consejo", "Primera vez? Empieza por el TUTORIAL. En partida: [Esc] pausa, con la lista de CONTROLES", 13, FontStyle.Normal, new Color(0.65f, 0.7f, 0.8f), new Vector2(0.5f, 0.06f), Vector2.zero, new Vector2(760f, 24f));
            CrearTextoMenu(canvasGO.transform, "Version", "v" + Application.version, 12, FontStyle.Normal, new Color(0.5f, 0.55f, 0.65f), new Vector2(0.98f, 0.03f), Vector2.zero, new Vector2(160f, 20f));

            var menuGO = new GameObject("MainMenu", typeof(RectTransform), typeof(MainMenuController));
            menuGO.transform.SetParent(canvasGO.transform, false);
            var menuController = menuGO.GetComponent<MainMenuController>();

            var playBtn = BuildButton(canvasGO.transform, "PlayButton", "JUGAR", new Vector2(0f, 50f), new Color(0.25f, 0.6f, 0.35f));
            playBtn.onClick.AddListener(menuController.OnPlayClicked);

            var tutorialBtn = BuildButton(canvasGO.transform, "TutorialButton", "TUTORIAL", new Vector2(0f, -20f), new Color(0.2f, 0.5f, 0.8f));
            tutorialBtn.onClick.AddListener(menuController.OnTutorialClicked);

            var exitBtn = BuildButton(canvasGO.transform, "ExitButton", "SALIR", new Vector2(0f, -90f), new Color(0.6f, 0.25f, 0.25f));
            exitBtn.onClick.AddListener(menuController.OnExitClicked);

            Directory.CreateDirectory("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScenesInBuildSettings();

            Debug.Log("[MenuSceneBuilder] Escena de menu principal construida en " + ScenePath);
        }

        static void CrearTextoMenu(Transform padre, string nombre, string texto, int tam, FontStyle estilo, Color color, Vector2 ancla, Vector2 pos, Vector2 caja)
        {
            var go = new GameObject(nombre, typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.alignment = nombre == "Version" ? TextAnchor.LowerRight : TextAnchor.MiddleCenter;
            t.color = color; t.fontSize = tam; t.fontStyle = estilo; t.text = texto;
            t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = ancla;
            if (nombre == "Version") rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = pos; rt.sizeDelta = caja;
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
            var tutorial = new EditorBuildSettingsScene("Assets/_Project/Scenes/SC_Tutorial.unity", true);
            EditorBuildSettings.scenes = new[] { menu, gameplay, testLevel, tutorial };
        }
    }
}
