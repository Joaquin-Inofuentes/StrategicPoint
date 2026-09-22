using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SP.Presentation
{
    // Unico contenido de SC_Loading: una barra que se llena mientras la escena
    // destino (SP.Core.SceneLoader.Destino) se carga en segundo plano, y despues
    // la activa. La referencia a la barra la deja puesta LoadingSceneBuilder al
    // armar la escena (misma convencion que el resto del proyecto: todo por
    // codigo, nada de arrastrar a mano en el Inspector).
    public class LoadingScreenController : MonoBehaviour
    {
        public Image barra;

        // SceneManager tapa el progreso real en 0.9 mientras allowSceneActivation
        // esta en false -- sin este mapeo la barra se queda pegada en 90% varios
        // cuadros y despues salta de golpe a 100 al activarse.
        const float TopeProgresoReal = 0.9f;

        // Minimo de tiempo que se ve esta pantalla. Si la escena destino ya esta
        // en cache (recargar SC_Gameplay al Reintentar, por ejemplo) la carga
        // tarda menos de un cuadro y la pantalla de carga parpadearia sin que se
        // le pueda leer nada.
        const float MinimoVisibleSegundos = 0.35f;

        void Start() => StartCoroutine(CargarEscenaDestino());

        IEnumerator CargarEscenaDestino()
        {
            string destino = SP.Core.SceneLoader.Destino;
            // Nadie deberia entrar a SC_Loading sin pasar por SceneLoader.Cargar
            // (F5 directo sobre esta escena en el Editor, por ejemplo), pero si
            // pasa, mejor caer al menu que quedarse trabado en una barra que
            // nunca arranca.
            if (string.IsNullOrEmpty(destino)) destino = "SC_MainMenu";

            float inicio = Time.unscaledTime;
            SetProgreso(0f);

            var op = SceneManager.LoadSceneAsync(destino);
            op.allowSceneActivation = false;

            while (op.progress < TopeProgresoReal)
            {
                SetProgreso(op.progress / TopeProgresoReal);
                yield return null;
            }
            SetProgreso(1f);

            float faltante = MinimoVisibleSegundos - (Time.unscaledTime - inicio);
            if (faltante > 0f) yield return new WaitForSecondsRealtime(faltante);

            op.allowSceneActivation = true;
        }

        void SetProgreso(float t)
        {
            if (barra != null) barra.fillAmount = Mathf.Clamp01(t);
        }
    }
}
