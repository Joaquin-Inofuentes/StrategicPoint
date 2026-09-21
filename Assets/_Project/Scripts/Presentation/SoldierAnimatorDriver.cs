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
        // Componentes de la velocidad en el espacio del propio soldado, no
        // del mundo: alimentan los blend tree 2D de caminar/correr para que
        // el jugador (que puede strafear con WASD sin mirar hacia donde
        // camina) vea caminar de costado o hacia atras, y no una animacion
        // de avance deslizandose de lado. La IA, que siempre mira hacia
        // donde se mueve (MoveTowards), cae siempre en Adelante=1/Lateral=0
        // sin saber que estos parametros existen.
        public const string ParamAdelante = "Adelante";
        public const string ParamLateral = "Lateral";
        // Mismo booleano que ya lee WeaponHolder.MultiplicadorPostura: la
        // pose del Animator y la dispersion del arma comparten una sola
        // fuente de verdad (SoldierMotor.IsCrouching), asi que agacharse
        // siempre se ve Y se siente igual.
        public const string ParamAgachado = "Agachado";
        // Salto (G3): igual que Agachado, un bool atado a SoldierMotor.
        // IsJumping -- la presentacion no decide saltar, solo muestra lo
        // que el motor ya esta haciendo.
        public const string ParamSalto = "Salto";
        // Muerte: un bool (no un Trigger) para que revivir sea la misma
        // moneda al reves -- CubeFxReactor lo pone en true al morir, y en
        // false al reactivarse ya viva (ver CubeFxReactor.OnEnable), y el
        // Animator vuelve solo a "DePie" con la transicion de salida que
        // arma ArtBuilder. Un Trigger no tiene "estado actual" que
        // consultar para saber si conviene, o no, reforzarlo al revivir.
        public const string ParamMuerto = "Muerto";
        public const string ParamMuerteVariante = "MuerteVariante";
        // Cuantas variantes de "morir" arma ArtBuilder (una por clip de
        // muerte del pack). CubeFxReactor sortea un numero en este rango;
        // vive aca y no en el Editor porque el runtime tambien lo necesita.
        public const int CantidadDeMuertes = 6;
        public const int CapaDisparo = 1;

        Soldier soldier;
        Vector3 posicionPrevia;
        Vector3 ultimaDireccion = Vector3.forward;
        float velocidadSuavizada;
        float restanteDeDisparo;
        float pesoDisparo;
        float mezclaAgachado;
        public float PesoCapaDisparoActual => animator != null && animator.layerCount > CapaDisparo ? animator.GetLayerWeight(CapaDisparo) : 0f;
        IDisposable shotSub;
        bool arrancado;
        // Ronda 11 (punto 6): los clips de costado bajan la cadera y los pies quedan hasta 4 cm bajo el piso (medido: 0,089 contra 0,129
        // en reposo). Se mide la altura del pie mas bajo respecto de la raiz mientras esta quieto y en el suelo, y despues se sube el
        // modelo lo que falte, con suavizado.
        Transform[] pies;
        float pieEnReposo = float.NaN;
        float elevacionDePies;
        Vector3 modeloBase;
        bool modeloBaseTomada;

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
            // Mismo criterio: las armas que no lleva en la mano, colgadas
            // de la espalda (ver WeaponBackRack) -- pedido explicito de
            // poder verle el resto del loadout a quien manejas.
            if (GetComponent<SP.Presentation.WeaponBackRack>() == null) gameObject.AddComponent<SP.Presentation.WeaponBackRack>();
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
            ultimaDireccion = transform.forward;
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
            Vector3 direccion = arrancado && dt > 0f ? delta / dt : Vector3.zero;
            arrancado = true;

            velocidadSuavizada = Mathf.MoveTowards(velocidadSuavizada, velocidad, 20f * dt);
            // Ronda 13 (punto 7): agachado se camina a la mitad (SoldierMotor.FactorDeVelocidadAgachado). El ciclo "walk crouching"
            // se alcanza a la velocidad de caminata AGACHADA: sin reescalar, el blend quedaba a medio camino entre el idle y el
            // paso (pose a medias y pies patinando). Se normaliza contra la velocidad tope de la postura actual.
            bool agachadoAhora = soldier != null && soldier.Motor != null && soldier.Motor.IsCrouching;
            float topeDePostura = velocidadDeCarrera * (agachadoAhora ? SP.Actors.SoldierMotor.FactorDeVelocidadAgachado : 1f);
            float normalizada = topeDePostura > 0.01f
                ? Mathf.Clamp01(velocidadSuavizada / topeDePostura)
                : 0f;
            animator.SetFloat(ParamVelocidad, normalizada);

            // Correr: el ciclo de piernas llega a su tope a velocidad de caminata, asi que se
            // ACELERA la animacion (hasta x1.7) en proporcion a lo que se corre de mas.
            float ritmo = velocidadDeCarrera > 0.01f && !agachadoAhora ? Mathf.Clamp(velocidadSuavizada / velocidadDeCarrera, 1f, SP.Actors.SoldierMotor.FactorDeCarrera) : 1f;
            animator.speed = Mathf.MoveTowards(animator.speed, normalizada > 0.15f ? ritmo : 1f, 6f * dt);

            // Se proyecta la velocidad SUAVIZADA (no la cruda de este
            // frame) sobre los ejes propios del soldado: asi el blend 2D
            // recibe la misma curva de arranque/frenado que ya tenia el
            // parametro "Velocidad", solo que repartida en adelante/atras
            // y derecha/izquierda en vez de un solo numero de magnitud.
            // Ronda 11 (punto 5): al frenar, la direccion medida pasa a 0 de golpe pero la velocidad suavizada tarda ~0,25 s en bajar;
            // con direccion 0 Adelante/Lateral caian a 0 en un solo cuadro y el blend tree 2D mezclaba las 8 caminatas (el "traba" al
            // terminar de caminar, medido en Docs/RONDA_11). Se conserva la ULTIMA direccion y se la escala por la velocidad que decae.
            if (direccion.sqrMagnitude > 0.0001f) ultimaDireccion = direccion.normalized;
            Vector3 direccionSuavizada = ultimaDireccion * velocidadSuavizada;
            float escala = topeDePostura > 0.01f ? topeDePostura : 1f;
            // El blend 2D espera un vector de largo <= 1: correr a 1,09 (velocidad real / 5) sobrepasaba el borde y alternaba clips.
            var ad = new Vector2(Vector3.Dot(direccionSuavizada, transform.forward), Vector3.Dot(direccionSuavizada, transform.right)) / escala;
            if (ad.sqrMagnitude > 1f) ad.Normalize();
            animator.SetFloat(ParamAdelante, ad.x);
            animator.SetFloat(ParamLateral, ad.y);

            if (soldier != null && soldier.Motor != null)
            {
                animator.SetBool(ParamAgachado, soldier.Motor.IsCrouching);
                animator.SetBool(ParamSalto, soldier.Motor.IsJumping);
            }

            restanteDeDisparo = Mathf.Max(0f, restanteDeDisparo - dt);
            float objetivo = restanteDeDisparo > 0f ? 1f : 0f;
            pesoDisparo = Mathf.MoveTowards(pesoDisparo, objetivo, velocidadDeMezcla * dt);
            // Ronda 13 (punto 7): la capa de disparo es la pose de "firing rifle" DE PIE sobre la mitad de arriba: agachado
            // dejaba el torso parado sobre unas piernas en cuclillas. "idle crouching aiming" ya apunta el arma, asi que agachado
            // la capa se apaga (con la misma mezcla suave) y la pose queda entera.
            mezclaAgachado = Mathf.MoveTowards(mezclaAgachado, agachadoAhora ? 1f : 0f, velocidadDeMezcla * dt);
            if (animator.layerCount > CapaDisparo)
                animator.SetLayerWeight(CapaDisparo, pesoDisparo * (1f - mezclaAgachado));
        }

        float AlturaDelPieMasBajo()
        {
            if (pies == null)
            {
                var lista = new System.Collections.Generic.List<Transform>();
                foreach (var tr in animator.GetComponentsInChildren<Transform>(true))
                {
                    var n = tr.name.ToLowerInvariant();
                    if (n.Contains("foot") || n.Contains("toe")) lista.Add(tr);
                }
                pies = lista.ToArray();
            }
            float min = float.MaxValue;
            for (int i = 0; i < pies.Length; i++) min = Mathf.Min(min, pies[i].position.y);
            return min == float.MaxValue ? float.NaN : min - transform.position.y - elevacionDePies;
        }

        void LateUpdate()
        {
            if (animator == null || soldier == null || soldier.Motor == null) return;
            var modelo = animator.transform;
            if (modelo == transform) return;
            if (!modeloBaseTomada) { modeloBase = modelo.localPosition; modeloBaseTomada = true; }
            float alturaPie = AlturaDelPieMasBajo();
            if (float.IsNaN(alturaPie)) return;
            bool quieto = velocidadSuavizada < 0.05f;
            if (quieto && !soldier.Motor.IsJumping && !soldier.Motor.IsCrouching) pieEnReposo = alturaPie;
            float objetivo = 0f;
            if (!float.IsNaN(pieEnReposo) && !soldier.Motor.IsJumping && !soldier.Motor.IsCrouching && !quieto)
                objetivo = Mathf.Clamp(pieEnReposo - alturaPie, 0f, 0.1f);
            elevacionDePies = Mathf.MoveTowards(elevacionDePies, objetivo, 0.6f * Time.deltaTime);
            var lp = modeloBase; lp.y += elevacionDePies;
            modelo.localPosition = lp;
        }
    }
}
