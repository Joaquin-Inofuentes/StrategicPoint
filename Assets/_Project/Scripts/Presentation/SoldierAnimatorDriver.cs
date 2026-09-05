using System;
using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Presentation
{
    // Traduce lo que el soldado YA hace a parametros del Animator. No
    // decide nada: no mueve, no dispara, no cambia de estado. Es el mismo
    // contrato que CubeFxReactor, y por el mismo motivo -- si la
    // presentacion pudiera decidir, la simulacion dejaria de ser la unica
    // fuente de verdad y la suite headless (que corre sin Animator) veria
    // un juego distinto al que se ve en pantalla.
    //
    // Dos entradas, y ninguna es "el estado de la IA":
    //
    //   * VELOCIDAD: se mide del desplazamiento real del transform, no de
    //     AiBrain.State ni de moveSpeed. Asi el jugador poseido, la IA en
    //     Patrol, la IA con orden y el attack-move alimentan el MISMO
    //     parametro sin que este componente sepa que existe ninguno de los
    //     cuatro. Ademas, si algo frena al soldado (el Muro), las piernas
    //     se frenan solas: no hay forma de que camine en el aire.
    //
    //   * DISPARO: se engancha a ShotFiredEvent del bus. Un disparo es un
    //     instante y una animacion dura; por eso se guarda un tiempo de
    //     sostenido en vez de un booleano -- si no, la capa de disparo
    //     parpadearia una vez por bala.
    public class SoldierAnimatorDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;

        // Velocidad a la que la mezcla llega a "correr". Es la misma
        // moveSpeed del SoldierMotor; se deja serializada y no se lee del
        // motor para poder exagerar o suavizar el ciclo sin tocar gameplay.
        [SerializeField] float velocidadDeCarrera = 5f;

        // Cuanto se sostiene la pose de disparo despues del ultimo tiro.
        // Con la cadencia mas lenta del juego (0.9 s entre balas) esto deja
        // caer la capa entre tiro y tiro, que es lo que se quiere ver.
        [SerializeField] float sostenidoDeDisparo = 0.45f;

        // Cuanto tarda la capa de disparo en subir y bajar. Instantaneo se
        // ve como un tiron; mas lento y el soldado sigue apuntando despues
        // de que el enemigo ya cayo.
        [SerializeField] float velocidadDeMezcla = 8f;

        // Tinte de equipo. La textura de camuflaje es la misma para los dos
        // bandos -- es el mismo soldado -- asi que sin esto un aliado y un
        // enemigo serian identicos a diez metros. Se multiplica sobre el
        // color base, con lo cual el camuflaje se sigue viendo entero.
        static readonly Color TinteAliado = new Color(0.82f, 1f, 0.86f);
        static readonly Color TinteEnemigo = new Color(1f, 0.62f, 0.55f);

        public const string ParamVelocidad = "Velocidad";
        public const int CapaDisparo = 1;

        Soldier soldier;
        Vector3 posicionPrevia;
        float velocidadSuavizada;
        float restanteDeDisparo;
        float pesoDisparo;
        IDisposable shotSub;
        bool arrancado;

        void Awake()
        {
            soldier = GetComponent<Soldier>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            posicionPrevia = transform.position;

            // El tinte se escribe ANTES de que CubeFxReactor lea el suyo, y
            // despues se lo despierta a mano. Ese componente cachea el
            // color base en su Bootstrap para poder volver a el despues de
            // cada destello de daño; si corriera primero se guardaria el
            // blanco de fabrica y el soldado perderia su color de equipo en
            // el primer tiro que recibiera. El orden entre dos Awake no
            // esta definido, asi que no se puede confiar en el.
            PintarPorEquipo();
            var fx = GetComponent<CubeFxReactor>();
            if (fx != null) fx.Bootstrap();

            // Tener cuerpo animado y tener el arma en la mano son la misma
            // condicion: se agrega aca y no en el prefab para que valga
            // igual para los soldados de la escena, los de los prefabs y
            // los que aparezcan despues, sin tocar ninguna escena. Los
            // soldados-cubo, que no tienen este componente, siguen con el
            // arma al costado y la suite headless no ve ningun cambio.
            if (GetComponent<ArmaEnLaMano>() == null) gameObject.AddComponent<ArmaEnLaMano>();
        }

        void PintarPorEquipo()
        {
            if (soldier == null) return;
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;
            CubeFxReactor.WriteTint(rend, soldier.Team == SP.Combat.TeamId.Player ? TinteAliado : TinteEnemigo);
        }

        void OnEnable()
        {
            shotSub = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShot);
            posicionPrevia = transform.position;
            arrancado = false;
        }

        void OnDisable()
        {
            shotSub?.Dispose();
            shotSub = null;
        }

        void OnShot(ShotFiredEvent evt)
        {
            if (soldier == null || evt.ShooterId != soldier.Id) return;
            restanteDeDisparo = sostenidoDeDisparo;
        }

        void Update()
        {
            if (animator == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return; // pausa o timeScale 0: no hay nada que medir

            var pos = transform.position;
            Vector3 delta = pos - posicionPrevia;
            delta.y = 0f;
            posicionPrevia = pos;

            // El primer frame despues de habilitarse (o de un teleport de
            // spawn) tiene un delta que no es movimiento: sin esta guarda
            // el soldado arranca la partida en plena carrera.
            float velocidad = arrancado ? delta.magnitude / dt : 0f;
            arrancado = true;

            velocidadSuavizada = Mathf.MoveTowards(velocidadSuavizada, velocidad, 20f * dt);
            float normalizada = velocidadDeCarrera > 0.01f
                ? Mathf.Clamp01(velocidadSuavizada / velocidadDeCarrera)
                : 0f;
            animator.SetFloat(ParamVelocidad, normalizada);

            restanteDeDisparo = Mathf.Max(0f, restanteDeDisparo - dt);
            float objetivo = restanteDeDisparo > 0f ? 1f : 0f;
            pesoDisparo = Mathf.MoveTowards(pesoDisparo, objetivo, velocidadDeMezcla * dt);
            if (animator.layerCount > CapaDisparo)
                animator.SetLayerWeight(CapaDisparo, pesoDisparo);
        }
    }
}
