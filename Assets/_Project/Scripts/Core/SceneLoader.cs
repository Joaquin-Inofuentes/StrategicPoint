using UnityEngine.SceneManagement;

namespace SP.Core
{
    // Unico punto de entrada para cambiar de escena en el juego real. Antes cada
    // pantalla (menu, victoria/derrota, pausa, tutorial) llamaba a
    // SceneManager.LoadScene directo: un corte seco, sin ningun aviso si la carga
    // tarda. Ahora todas pasan primero por SC_Loading, que carga la escena destino
    // de forma asincronica (SceneManager.LoadSceneAsync) y muestra una barra de
    // progreso -- ver LoadingScreenController, que es quien la lee.
    public static class SceneLoader
    {
        public const string EscenaDeCarga = "SC_Loading";

        public static string Destino { get; private set; }

        public static void Cargar(string escenaDestino)
        {
            Destino = escenaDestino;
            SceneManager.LoadScene(EscenaDeCarga);
        }
    }
}
