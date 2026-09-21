using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.Core
{
    // Busca un objeto que es RAIZ de escena por su nombre. Recorre solo las raices de las escenas cargadas
    // (decenas de objetos), no toda la jerarquia como GameObject.Find. Sirve para los objetos que el propio
    // codigo crea en la raiz (raices de pools, iconos de obstaculos) y quiere reencontrar tras un reinicio.
    public static class RaicesDeEscena
    {
        static readonly System.Collections.Generic.List<GameObject> raices = new System.Collections.Generic.List<GameObject>();

        public static GameObject Buscar(string nombre)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var escena = SceneManager.GetSceneAt(i);
                if (!escena.isLoaded) continue;
                raices.Clear();
                escena.GetRootGameObjects(raices);
                foreach (var go in raices)
                    if (go != null && go.name == nombre) { raices.Clear(); return go; }
            }
            raices.Clear();
            return null;
        }
    }
}
