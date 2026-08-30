using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Solo marca en el log de flujo que la escena de gameplay terminó de
    // cargar (Start corre después de que todo el resto ya se construyó).
    public class GameplaySceneBootstrap : MonoBehaviour
    {
        void Start()
        {
            GameLog.Line("Inicio partida");
            GameLog.Line("Cargo la escena");
        }
    }
}
