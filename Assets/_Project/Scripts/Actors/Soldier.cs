using UnityEngine;
using SP.Core;
using SP.Combat;

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
        bool bootstrapped;

        public int Id { get; private set; }
        public string DisplayName => displayName;
        public TeamId Team => team;
        public RoleType Role => role;

        Health health;
        SoldierMotor motor;
        WeaponHolder weapon;

        // Estas propiedades se leen desde muchos lugares (PlayerBrain,
        // tests, IA) sin pasar por Awake primero cuando el objeto se creó
        // por script de editor fuera de Play mode, así que cada una se
        // asegura de haber corrido Bootstrap antes de responder.
        public Health Health { get { if (!bootstrapped) Bootstrap(); return health; } }
        public SoldierMotor Motor { get { if (!bootstrapped) Bootstrap(); return motor; } }
        public WeaponHolder Weapon { get { if (!bootstrapped) Bootstrap(); return weapon; } }

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
        public void Configure(string name, TeamId t, RoleType r, int max)
        {
            displayName = name;
            team = t;
            role = r;
            maxHealth = max;
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

            health.Initialize(Id, maxHealth);
            ActorRegistry.Register(this);
        }

        void OnDestroy() => ActorRegistry.Unregister(this);
    }
}
