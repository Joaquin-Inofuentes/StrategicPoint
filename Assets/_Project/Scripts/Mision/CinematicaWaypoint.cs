using UnityEngine;

namespace SP.Mision
{
    // Un punto de camara de la cinematica de apertura (ver CinematicaIntroPath/CinematicaDeIntro).
    // Pedido explicito: "cada empty tiene sus variable de segundos de cuanto tiempo demora en
    // llegar y cuanto tiempo se queda en esa toma" -- estos dos numeros son de ESTE punto, no
    // globales, para poder darle mas tiempo a una toma que a otra desde el Inspector sin tocar
    // codigo. "mirarPunto" es hacia donde apunta la camara mientras viaja hacia aca y mientras se
    // queda esperando (en vez de una Transform, un punto de mundo: mas facil de ubicar a mano en
    // el Inspector/Scene view que tener que arrastrar otro objeto).
    public class CinematicaWaypoint : MonoBehaviour
    {
        [Tooltip("Segundos que tarda la camara en LLEGAR desde el punto anterior hasta este.")]
        public float segundosParaLlegar = 2.5f;

        [Tooltip("Segundos que la camara se queda esperando en este punto antes de seguir al siguiente.")]
        public float segundosDeEspera = 1.5f;

        [Tooltip("Punto de mundo hacia el que mira la camara mientras viaja hacia aca y mientras espera.")]
        public Vector3 mirarPunto;

        [Tooltip("Texto opcional a mostrar (subtitulo, estilo cine) mientras la camara espera en este punto. Vacio = no muestra nada.")]
        [TextArea] public string subtitulo = "";
    }
}
