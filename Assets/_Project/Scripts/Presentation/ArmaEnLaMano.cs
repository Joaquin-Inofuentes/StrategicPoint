using System.Collections;
using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // Del plan del usuario: "Los soldados no tienen armas. Deberian tener
    // armas".
    //
    // Armas tenian: WeaponVisual existe en todos desde siempre. Lo que no
    // tenian era el arma EN LA MANO. Ese cubo se colgo de la RAIZ del
    // soldado en la epoca en que el cuerpo tambien era un cubo, y ahi
    // quedo cuando el cuerpo paso a ser el rig animado. Medido en
    // SC_Gameplay: el rifle a y=0,80 (altura de cadera) y la mano derecha
    // a y=1,34 -- 54 cm mas arriba -- y ademas fijo, sin seguir jamas al
    // brazo que lo animaba. Visto desde el juego eso no es un arma: es un
    // cubo que flota al costado.
    //
    // Esto lo cuelga del hueso de la mano UNA sola vez, cuando el Animator
    // ya poso el cuerpo, y despues no hace nada mas: lo lleva la
    // animacion. El canio (Muzzle) se va con el, asi que el fogonazo y las
    // balas salen de la punta del arma y no del aire.
    //
    // Si el soldado no tiene rig humano -- los cubos de la suite headless,
    // que corre sin Animator -- no toca nada. La presentacion no puede
    // cambiar lo que la simulacion ve, y esta clase se toma esa regla en
    // serio: no mueve colliders, no cambia el Muzzle de altura respecto
    // del cuerpo mas alla de lo que la mano ya hace, y no decide nada.
    [RequireComponent(typeof(WeaponHolder))]
    public class ArmaEnLaMano : MonoBehaviour
    {
        // Cuanto se adelanta el CENTRO del arma respecto de la mano. El
        // cubo mide 0,55 de largo, asi que con 0,15 la culata queda 12 cm
        // por detras del puño: se lee como agarrada, no como pegada.
        public const float AdelantoDeLaMano = 0.15f;

        // Un pelo por encima del hueso: el hueso de la mano cae en el
        // centro de la palma y el arma se apoya arriba de ella.
        public const float AlturaSobreLaMano = 0.02f;

        // Donde queda la boca del canio dentro del arma, en unidades del
        // cubo (que va de -0,5 a 0,5 en cada eje): la cara de adelante.
        public const float PuntaDelCanio = 0.5f;

        bool colgada;

        void Start()
        {
            StartCoroutine(ColgarCuandoElCuerpoEsteEnPose());
        }

        // Un frame de espera, no por prolijidad: en el primer frame el
        // Animator todavia no evaluo y el esqueleto esta en pose de bind,
        // con los brazos en cruz. Congelar el offset contra ESA pose deja
        // el arma flotando a un metro del cuerpo para siempre.
        IEnumerator ColgarCuandoElCuerpoEsteEnPose()
        {
            yield return null;
            Colgar();
        }

        public bool Colgar()
        {
            if (colgada) return true;

            var anim = GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman) return false;

            var mano = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (mano == null) return false;

            var arma = transform.Find("WeaponVisual");
            if (arma == null) return false;

            // Se coloca en MUNDO -- con el cuerpo ya posado -- y recien
            // despues se cuelga del hueso conservando esa pose. Es
            // exactamente lo que haria alguien ubicandola a mano en el
            // editor, y por eso el offset local que queda es el correcto
            // para el resto de las poses y no solo para esta.
            arma.rotation = Quaternion.LookRotation(transform.forward, transform.up);
            arma.position = mano.position
                          + transform.forward * AdelantoDeLaMano
                          + transform.up * AlturaSobreLaMano;
            arma.SetParent(mano, true);

            // El canio pasa a ser hijo del arma, no de la raiz: si se
            // queda arriba, el fogonazo sale de la cadera mientras el
            // arma dispara desde la mano, que es peor que antes porque
            // ahora se ve la diferencia.
            var holder = GetComponent<WeaponHolder>();
            if (holder != null && holder.Muzzle != null)
            {
                holder.Muzzle.SetParent(arma, false);
                holder.Muzzle.localPosition = new Vector3(0f, 0f, PuntaDelCanio);
                holder.Muzzle.localRotation = Quaternion.identity;
                holder.Muzzle.localScale = Vector3.one;
            }

            colgada = true;
            return true;
        }
    }
}
