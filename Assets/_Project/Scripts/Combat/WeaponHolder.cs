using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Combat
{
    // Arma en mano de un soldado: pide proyectiles al pool, nunca instancia.
    // Lee dueño y equipo de su propio Soldier — nunca los cachea por su cuenta,
    // así sobrevive a un domain reload sin que nadie tenga que recablearlo.
    public class WeaponHolder : MonoBehaviour, IWeapon
    {
        [SerializeField] float fireCooldown = 0.35f;
        [SerializeField] int damage = 34;
        [SerializeField] ProjectilePool pool;

        public Transform Muzzle;

        // Cubo chico pegado a la mano/arma del soldado: se ve tanto en FPS
        // (el jugador lo ve colgando adelante suyo) como en RTS (parte del
        // cuerpo), y se tiñe del color del arma equipada — así se nota a
        // simple vista con qué arma anda cada uno, sin abrir ningún menú.
        public Renderer WeaponVisualRenderer;

        Soldier owner;
        float cooldownTimer;
        bool bootstrapped;
        Color projectileColor = new Color(1f, 0.92f, 0.35f);

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public WeaponKind CurrentWeaponKind { get; private set; } = WeaponKind.Rifle;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            owner = GetComponent<Soldier>();
        }

        public void SetPool(ProjectilePool projectilePool) => pool = projectilePool;

        public void SetTuning(int weaponDamage, float cooldown)
        {
            damage = weaponDamage;
            fireCooldown = cooldown;
        }

        // Cambiar de arma (recogida en el mundo) es cambiar estos tres
        // números y el color de lo que dispara. Nada más.
        public void EquipWeapon(WeaponKind kind, int weaponDamage, float cooldown, Color color)
        {
            CurrentWeaponKind = kind;
            damage = weaponDamage;
            fireCooldown = cooldown;
            projectileColor = color;
            // Cambiar de arma no debería dejarte esperando el enfriamiento
            // del arma anterior: se puede disparar de una con la nueva.
            cooldownTimer = 0f;

            ApplyWeaponVisualColor(color);
            // Cada arma tiene su propia forma (chica/larga/gruesa), no solo
            // color: así se distingue de un vistazo cuál está equipada. El
            // cuerpo del soldado tiene escala no uniforme (0.9/1.6/0.9): hay
            // que compensarla para que el cubo del arma no salga deformado.
            if (WeaponVisualRenderer != null)
            {
                var rootScale = transform.lossyScale;
                var wanted = WeaponCatalog.Get(kind).VisualScale;
                WeaponVisualRenderer.transform.localScale = new Vector3(wanted.x / rootScale.x, wanted.y / rootScale.y, wanted.z / rootScale.z);
            }
        }

        // Igual que con el material de Projectile: un Material creado en
        // runtime y guardado dentro de un prefab (PrefabUtility.SaveAsPrefabAsset)
        // puede quedar null en la instancia — se recrea sola si hace falta.
        void ApplyWeaponVisualColor(Color color)
        {
            if (WeaponVisualRenderer == null) return;
            if (WeaponVisualRenderer.sharedMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                WeaponVisualRenderer.sharedMaterial = new Material(shader);
            }
            WeaponVisualRenderer.sharedMaterial.color = color;
        }

        public void Tick(float dt)
        {
            if (cooldownTimer > 0f) cooldownTimer -= dt;
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (owner == null) Bootstrap();
            if (cooldownTimer > 0f || pool == null || owner == null) return false;

            var spawnPos = Muzzle != null ? Muzzle.position : origin;
            pool.Spawn(spawnPos, direction, owner.Id, owner.Team, damage, projectileColor);
            cooldownTimer = fireCooldown;
            EventBus.Instance.Publish(new ShotFiredEvent(owner.Id));
            return true;
        }
    }
}
