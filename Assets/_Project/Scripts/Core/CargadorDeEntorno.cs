using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.Core
{
    // EL ENTORNO DE SC_Operacion VIVE EN TRES ESCENAS ADITIVAS (cuartel, ruta, ciudad).
    //
    // Cada sector es la raiz de su propia escena, asi que lo que esta dentro de un sector no puede mezclarse con otro: si una pieza
    // esta en la escena equivocada se nota apenas se aisla esa escena. SC_Operacion conserva todo lo dinamico (soldados, enemigos,
    // vehiculos, UI, sistemas) y el mundo comun (suelo, sol, perimetro, horizonte).
    //
    // Este componente carga las escenas que falten al arrancar. Si ya estan abiertas (en el editor, con "Abrir Operacion completa")
    // no hace nada. Las escenas tienen que estar en Build Settings: la herramienta del editor las agrega.
    [DefaultExecutionOrder(-32000)]
    public class CargadorDeEntorno : MonoBehaviour
    {
        public string[] escenas = { "SC_Operacion_Cuarteles", "SC_Operacion_Ruta", "SC_Operacion_Ciudad" };

        void Awake()
        {
            foreach (var nombre in escenas)
            {
                if (string.IsNullOrEmpty(nombre)) continue;
                var ya = SceneManager.GetSceneByName(nombre);
                if (ya.IsValid() && ya.isLoaded) continue;
                if (ya.IsValid() && !ya.isLoaded) continue;   // ya se esta cargando
                if (!Application.CanStreamedLevelBeLoaded(nombre)) { Debug.LogWarning("[CargadorDeEntorno] la escena '" + nombre + "' no esta en Build Settings"); continue; }
                SceneManager.LoadScene(nombre, LoadSceneMode.Additive);
            }
        }
    }
}
