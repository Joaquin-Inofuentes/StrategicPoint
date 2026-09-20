using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Tutorial;

namespace SP.EditorTools
{
    // Menu de Editor para el pedido "quiero un script con timers para el tutorial que de
    // manera mecanica haga todo el tutorial para testear todas las funciones": entra en
    // Play sobre SC_Tutorial (si hace falta) y lanza TutorialAutoPlayer, que recorre los
    // 36 pasos con gestos reales cronometrados (ver Assets/_Project/Scripts/Tutorial/TutorialAutoPlayer.cs).
    public static class TutorialAutoPlayerMenu
    {
        const string RutaEscenaTutorial = "Assets/_Project/Scenes/SC_Tutorial.unity";

        [MenuItem("Strategic Point/Tutorial/Reproducir Tutorial Automatico (con timers)")]
        public static void ReproducirTutorialAutomatico()
        {
            if (!EditorApplication.isPlaying)
            {
                if (SceneManager.GetActiveScene().path != RutaEscenaTutorial)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    EditorSceneManager.OpenScene(RutaEscenaTutorial);
                }
                EditorApplication.isPlaying = true;
                EditorApplication.delayCall += EsperarYLanzar;
            }
            else
            {
                TutorialAutoPlayer.Lanzar();
            }
        }

        static void EsperarYLanzar()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.delayCall += EsperarYLanzar; return; }
            if (SceneManager.GetActiveScene().path != RutaEscenaTutorial || TutorialManager.Instance == null)
            {
                EditorApplication.delayCall += EsperarYLanzar;
                return;
            }
            TutorialAutoPlayer.Lanzar();
        }
    }
}
