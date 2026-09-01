using UnityEngine;
using SP.Core;
using SP.Combat;
using SP.Ai;

namespace SP.Actors
{
    // Reúne las piezas del GameObject y las expone. No decide nada por sí mismo.
    // Se auto-inicializa en Awake a partir de sus propios campos serializados,
    // para que funcione igual recién construido en el editor o recargado
    // desde una escena guardada (Play mode hace un domain reload).
    public class Soldier : MonoBehaviour
    {
        [SerializeField] string displayName;
        [SerializeField] TeamId team;
        [SerializeField] RoleType role;
        [SerializeField] int maxHealth = 100;

        static int nextId = 1;

        // Solo para tests/reinicios de escena en Edit mode, donde no hay
        // domain reload entre corridas: sin esto, correr la suite dos
        // veces en la misma sesion de Editor da Id's que no arrancan en 1,
        // rompiendo cualquier comparacion "antes/despues" entre corridas
        // (ej. RunEquivalenceCheck) o cualquier aserción que asuma IDs bajos.
        public static void ResetIdCounterForTests() => nextId = 1;

        bool bootstrapped;

        public int Id { get; private set; }
        public string DisplayName => displayName;
        public TeamId Team => team;
        public RoleType Role => role;

        Health health;
        SoldierMotor motor;
        WeaponHolder weapon;
        // WorldSimulationDriver hacia GetComponent<AiBrain>() por soldado
        // en cada frame de Update -- con cincuenta soldados son tres mil
        // llamadas por segundo a una operacion que siempre devuelve lo
        // mismo. Mismo patron que Health/Motor/Weapon: se cachea una sola
        // vez en Bootstrap.
        AiBrain aiBrain;

        // Estas propiedades se leen desde muchos lugares (PlayerBrain,
        // tests, IA) sin pasar por Awake primero cuando el objeto se creó
        // por script de editor fuera de Play mode, así que cada una se
        // asegura de haber corrido Bootstrap antes de responder.
        public Health Health { get { if (!bootstrapped) Bootstrap(); return health; } }
        public SoldierMotor Motor { get { if (!bootstrapped) Bootstrap(); return motor; } }
        public WeaponHolder Weapon { get { if (!bootstrapped) Bootstrap(); return weapon; } }
        public AiBrain Brain { get { if (!bootstrapped) Bootstrap(); return aiBrain; } }

        public Transform EyeAnchor;

        Renderer[] bodyRenderers;

        // Oculta la propia malla en primera persona: la cámara vive a
        // ~0.5m del centro del cuerpo (EyeAnchor), muy dentro del near
        // clip plane, así que sin esto se ve un triángulo gigante
        // recortado tapando la pantalla apenas alguien se posee a sí
        // mismo. No toca colliders ni el GameObject: solo el renderizado.
        public void SetBodyVisible(bool visible)
        {
            if (bodyRenderers == null) bodyRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in bodyRenderers) if (r != null) r.enabled = visible;
        }

        // Fija identidad y equipo. Se llama una vez al construir la escena.
        //
        // BUG CRITICO CORREGIDO: Awake() ya corre Bootstrap() (que a su vez
        // llama Health.Initialize(Id, maxHealth)) en el mismo instante en que
        // Unity instancia el prefab -- ANTES de que este metodo tenga chance
        // de correr. El "max" que se pasa aca antes se guardaba en el campo
        // maxHealth pero jamas volvia a llegar a Health, que ya habia quedado
        // inicializado con el default del prefab (100). Un enemigo "creado con
        // 180 de vida" en realidad nacia con 100, sin ningun error ni log.
        // Ahora, si el bootstrap ya corrio, se vuelve a sincronizar Health con
        // el valor real pedido.
        public void Configure(string name, TeamId t, RoleType r, int max)
        {
            displayName = name;
            team = t;
            role = r;
            maxHealth = max;

            if (bootstrapped && health != null)
                health.Initialize(Id, maxHealth);
        }

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            Id = nextId++;
            health = GetComponent<Health>();
            motor = GetComponent<SoldierMotor>();
            weapon = GetComponent<WeaponHolder>();
            aiBrain = GetComponent<AiBrain>();

            if (health == null)
            {
                Debug.LogError($"Soldier '{name}' no tiene un componente Health adjunto: no se puede inicializar.", this);
                return; // deja bootstrapped=true para no reintentar en bucle, pero sin tocar un health nulo
            }

            health.Initialize(Id, maxHealth);
            ActorRegistry.Register(this);
        }

        void OnDestroy() => ActorRegistry.Unregister(this);
    }
}
