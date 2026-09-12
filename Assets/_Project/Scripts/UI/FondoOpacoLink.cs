using UnityEngine;

namespace SP.UI
{
    // BUG REAL ("la UI que solapa feo" -- cajas negras vacias que se
    // quedaban pegadas en pantalla): FondoOpaco.Poner cuelga el fondo
    // como HERMANO del texto (no como hijo -- ver el comentario de
    // Poner sobre por que un hijo queda SIEMPRE dibujado encima de su
    // padre). El costo de dejar de ser hijo es que Unity ya no apaga el
    // fondo solo cuando algo apaga el texto (SetActive en un hijo
    // arrastra a sus hijos; en un hermano, no). PhaseBannerView,
    // InstructionBannerView y AimUI apagan su Text con
    // gameObject.SetActive(false) al terminar de mostrarlo, y el fondo
    // se quedaba prendido para siempre -- un rectangulo negro vacio
    // tapando media pantalla.
    //
    // Este componente vive en el TEXTO (no en el fondo): OnEnable/
    // OnDisable de Unity se disparan sobre CUALQUIER componente del
    // GameObject que cambia de activo, sin importar quien haya llamado
    // a SetActive ni desde donde. Asi el fondo sigue al texto sin que
    // cada vista tenga que acordarse de apagarlo a mano.
    [DisallowMultipleComponent]
    public class FondoOpacoLink : MonoBehaviour
    {
        public GameObject Fondo;

        void OnEnable()
        {
            if (Fondo != null) Fondo.SetActive(true);
        }

        void OnDisable()
        {
            if (Fondo != null) Fondo.SetActive(false);
        }
    }
}
