using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SP.Core;

namespace SP.Presentation
{
    // Menú de inicio: [Jugar] carga la escena de gameplay, [Salir] cierra
    // el juego (o sale de Play mode si esto corre en el Editor).
    public class MainMenuController : MonoBehaviour
    {
        // button.onClick.AddListener(...) hecho en un script de Editor
        // (al armar la escena, fuera de Play mode) NO sobrevive a Play
        // mode: UnityEvent solo conserva los listeners "persistentes"
        // (los cargados a mano en el Inspector), no los agregados por
        // código estando en Edit mode. Por eso la conexión real pasa acá,
        // en Awake, que sí corre durante Play mode.
        void Awake()
        {
            var canvasRoot = transform.parent;
            if (canvasRoot == null) return;
            var playBtn = canvasRoot.Find("PlayButton")?.GetComponent<Button>();
            var tutorialBtn = canvasRoot.Find("TutorialButton")?.GetComponent<Button>();
            var exitBtn = canvasRoot.Find("ExitButton")?.GetComponent<Button>();
            if (tutorialBtn != null) { tutorialBtn.onClick.AddListener(OnTutorialClicked); SP.UI.ButtonSfx.Attach(tutorialBtn); }
            if (playBtn != null) { playBtn.onClick.AddListener(OnPlayClicked); SP.UI.ButtonSfx.Attach(playBtn); }
            if (exitBtn != null) { exitBtn.onClick.AddListener(OnExitClicked); SP.UI.ButtonSfx.Attach(exitBtn); }
        }

        void Start() => GameLog.Line("Pantalla de menu cargada");

        // Un doble click en Jugar (pasa seguido: el segundo click del
        // mouse cae antes de que la escena termine de cambiar) disparaba
        // "Se selecciono iniciar partida" dos veces en el log por una
        // sola intención del jugador.
        bool actionTaken;

        public void OnPlayClicked()
        {
            if (actionTaken) return;
            // JUGAR no arranca directo: primero se elige la dificultad.
            MostrarDificultad();
        }

        GameObject panelDificultad;

        public void MostrarDificultad()
        {
            var raiz = transform.parent;
            if (raiz == null) { IniciarPartida(NivelDificultad.Medio); return; }
            if (panelDificultad != null) { panelDificultad.SetActive(true); return; }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelDificultad = new GameObject("PanelDificultad", typeof(RectTransform), typeof(Image));
            panelDificultad.transform.SetParent(raiz, false);
            var fondo = panelDificultad.GetComponent<Image>();
            fondo.color = new Color(0.04f, 0.05f, 0.07f, 0.96f);
            var frt = panelDificultad.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

            Texto(panelDificultad.transform, font, "ELEGI LA DIFICULTAD", 54, new Vector2(0.5f, 0.86f), new Vector2(1200f, 90f), Color.white);
            Texto(panelDificultad.transform, font, "Cambia la vida y el daño de los enemigos, de tu escuadra y el tuyo.", 22, new Vector2(0.5f, 0.78f), new Vector2(1200f, 40f), new Color(0.75f, 0.8f, 0.88f));

            var niveles = new[] { NivelDificultad.Facil, NivelDificultad.Medio, NivelDificultad.Dificil };
            var colores = new[] { new Color(0.2f, 0.55f, 0.35f), new Color(0.2f, 0.42f, 0.7f), new Color(0.65f, 0.22f, 0.2f) };
            for (int i = 0; i < 3; i++)
            {
                var nivel = niveles[i];
                var d = Dificultad.Datos(nivel);
                var go = new GameObject("Dificultad_" + d.Nombre, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panelDificultad.transform, false);
                go.GetComponent<Image>().color = colores[i];
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.45f);
                rt.anchoredPosition = new Vector2((i - 1) * 470f, 0f);
                rt.sizeDelta = new Vector2(440f, 360f);
                Texto(go.transform, font, d.Nombre, 40, new Vector2(0.5f, 0.86f), new Vector2(420f, 60f), Color.white);
                Texto(go.transform, font, Dificultad.Resumen(nivel), 20, new Vector2(0.5f, 0.52f), new Vector2(420f, 130f), Color.white, TextAnchor.MiddleLeft);
                Texto(go.transform, font, d.Frase, 18, new Vector2(0.5f, 0.15f), new Vector2(400f, 70f), new Color(1f, 1f, 1f, 0.85f));
                var b = go.GetComponent<Button>();
                b.onClick.AddListener(() => IniciarPartida(nivel));
                SP.UI.ButtonSfx.Attach(b);
            }

            var volver = new GameObject("Volver", typeof(RectTransform), typeof(Image), typeof(Button));
            volver.transform.SetParent(panelDificultad.transform, false);
            volver.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.34f);
            var vrt = volver.GetComponent<RectTransform>();
            vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.12f);
            vrt.sizeDelta = new Vector2(260f, 56f);
            Texto(volver.transform, font, "VOLVER", 26, new Vector2(0.5f, 0.5f), new Vector2(240f, 50f), Color.white);
            var vb = volver.GetComponent<Button>();
            vb.onClick.AddListener(() => panelDificultad.SetActive(false));
            SP.UI.ButtonSfx.Attach(vb);
        }

        static void Texto(Transform padre, Font font, string texto, int tam, Vector2 ancla, Vector2 caja, Color color, TextAnchor alinear = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.text = texto; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.color = color;
            t.alignment = alinear; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = ancla;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = caja;
        }

        public void IniciarPartida(NivelDificultad nivel)
        {
            if (actionTaken) return;
            actionTaken = true;
            Dificultad.Actual = nivel;
            GameLog.Line($"Se selecciono iniciar partida (dificultad {Dificultad.Datos(nivel).Nombre})");
            SceneManager.LoadScene("SC_Gameplay");
        }

        public void OnTutorialClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono el tutorial");
            SceneManager.LoadScene("SC_Tutorial");
        }

        public void OnExitClicked()
        {
            if (actionTaken) return;
            actionTaken = true;
            GameLog.Line("Se selecciono salir del juego");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
