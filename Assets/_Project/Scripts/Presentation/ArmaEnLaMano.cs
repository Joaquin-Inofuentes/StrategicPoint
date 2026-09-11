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
        // Cuanto se adelanta el CENTRO del arma respecto de la mano, para
        // el arma con la que se calibro este numero originalmente (el
        // cubo viejo, de 0,55 de largo): con 0,15 la culata quedaba 12 cm
        // por detras del puño, se leia como agarrada, no como pegada.
        //
        // BUG REAL: esto era una CONSTANTE, la misma para las tres armas.
        // El cubo median 0,55 sin importar cual arma representara, pero el
        // modelo real de WeaponPrefabBuilder no -- va de 0,21 m (pistola) a
        // 1,12 m (pesada), con el rifle en 0,76 m. Contra un rifle real
        // (mucho mas largo que el cubo con el que se afino este numero) la
        // culata quedaba enterrada 20+ cm dentro del torso/brazo en vez de
        // 12 cm detras del puño. Ahora el adelanto se calcula por arma, a
        // partir de su largo real (WeaponModels.NaturalLength), con el
        // mismo criterio de siempre: culata ~12 cm detras del puño.
        public const float MargenCulataDetrasDelPuno = 0.12f;

        // Un pelo por encima del hueso: el hueso de la mano cae en el
        // centro de la palma y el arma se apoya arriba de ella.
        public const float AlturaSobreLaMano = 0.02f;

        // Donde queda la boca del canio dentro del arma, en unidades del
        // cubo (que va de -0,5 a 0,5 en cada eje): la cara de adelante.
        public const float PuntaDelCanio = 0.5f;

        bool colgada;
        Transform arma;

        // Direcciones "adelante" y "arriba" del cuerpo en el momento de
        // colgar el arma, expresadas en el espacio LOCAL de la mano. Al
        // ser locales sobreviven a que la mano rote despues por la
        // animacion (el espacio local de un hijo no cambia aunque el
        // padre gire), asi que sirven para reajustar cuanto se adelanta
        // el arma cada vez que se cambia de arma, sin tener que volver a
        // colgarla desde cero.
        Vector3 dirAdelanteLocal = Vector3.forward;
        Vector3 dirArribaLocal = Vector3.up;

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

            arma = transform.Find("WeaponVisual");
            if (arma == null) return false;

            // Direcciones del cuerpo, congeladas en espacio local de la
            // mano ANTES de tocar nada -- este es el mismo "adelante"/
            // "arriba" que antes se usaba una sola vez para calcular la
            // posicion en mundo, ahora guardado para poder reaplicarlo
            // con un adelanto distinto cada vez que cambie el arma.
            dirAdelanteLocal = mano.InverseTransformDirection(transform.forward);
            dirArribaLocal = mano.InverseTransformDirection(transform.up);

            // Se coloca en MUNDO -- con el cuerpo ya posado -- y recien
            // despues se cuelga del hueso conservando esa pose. Es
            // exactamente lo que haria alguien ubicandola a mano en el
            // editor, y por eso el offset local que queda es el correcto
            // para el resto de las poses y no solo para esta.
            arma.rotation = Quaternion.LookRotation(transform.forward, transform.up);
            arma.position = mano.position;
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
            Reposicionar(holder != null ? holder.CurrentWeaponKind : WeaponKind.Rifle);
            return true;
        }

        // Ajusta cuanto se adelanta el arma respecto de la mano segun cual
        // arma sea -- una pistola chica se centra casi sobre el puño, un
        // rifle o una pesada (mucho mas largos) necesitan adelantarse mas
        // para que la culata no termine adentro del torso. Si todavia no
        // se colgo el arma (Colgar() no corrio) no hace nada: Colgar()
        // llama esto solo por su cuenta apenas cuelga, con el arma que
        // este equipada en ese momento.
        public void Reposicionar(WeaponKind kind)
        {
            if (!colgada || arma == null) return;
            float largoNatural = WeaponModels.NaturalLength(kind);
            float adelanto = Mathf.Max(0f, largoNatural * 0.5f - MargenCulataDetrasDelPuno);
            arma.localPosition = dirAdelanteLocal * adelanto + dirArribaLocal * AlturaSobreLaMano;
        }
    }
}
