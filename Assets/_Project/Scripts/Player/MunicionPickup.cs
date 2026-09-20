using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Player
{
    // Municion suelta al morir un enemigo (item nuevo). 100% por codigo, sin
    // prefab ni asset externo -- mismo espiritu que CajaDeSuministros.Crear,
    // pero mas chica y sin reposicion (se recoge una vez y desaparece).
    //
    // Deteccion por DISTANCIA en Update, no OnTriggerEnter: los soldados de
    // este proyecto no llevan Rigidbody (ver Rehen.cs para el mismo
    // razonamiento), asi que un trigger normal de Unity nunca dispararia
    // contra ellos. El Collider que lleva el pickup esta en isTrigger solo
    // para no bloquear el paso de nadie mientras esta en el piso.
    public class MunicionPickup : MonoBehaviour
    {
        public const float Radio = 1.6f;
        // Se cae sola si nadie la recoge en un rato: si no, el piso de una
        // mision larga terminaria lleno de cajitas de balas de cada baja.
        public const float VidaMaxima = 45f;

        Renderer rend;
        PlayerInputDriver driver;
        float edad;

        public static MunicionPickup Crear(Vector3 pos)
        {
            var raiz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            raiz.name = "MunicionPickup";
            raiz.transform.position = pos + Vector3.up * 0.3f;
            raiz.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            var colViejo = raiz.GetComponent<Collider>();
            if (colViejo != null) Destroy(colViejo);
            var col = raiz.AddComponent<BoxCollider>();
            col.isTrigger = true;

            var color = new Color(0.85f, 0.65f, 0.15f); // color "municion", distinto del verde de CajaDeSuministros
            raiz.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(color);

            return raiz.AddComponent<MunicionPickup>();
        }

        void Awake()
        {
            rend = GetComponent<Renderer>();
        }

        void Update()
        {
            edad += Time.deltaTime;
            if (edad >= VidaMaxima) { Destroy(gameObject); return; }

            // Gira despacio para que se note en el piso, igual que la caja de suministros.
            transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);

            if (driver == null) driver = FindAnyObjectByType<PlayerInputDriver>();
            var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            if (yo == null || yo.Weapon == null || !yo.Health.IsAlive) return;

            var d = yo.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > Radio * Radio) return;

            yo.Weapon.AgregarMunicion(1);
            Feedback.Accion(SfxKind.HealDone, "+MUNICION", transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.5f);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------
        // Enganche con la muerte de un enemigo: un solo listener global (no
        // uno por soldado) que crea el pickup en la posicion de cualquier
        // Soldier del equipo Enemy que muera durante la partida.
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribirse()
        {
            EventBus.Instance.Subscribe<EntityDiedEvent>(OnEnemigoMuerto);
        }

        static void OnEnemigoMuerto(EntityDiedEvent evt)
        {
            if (!Application.isPlaying) return;
            var soldado = ActorRegistry.FindById(evt.ActorId);
            if (soldado == null || soldado.Team != TeamId.Enemy) return;
            Crear(soldado.transform.position);
        }
    }
}
