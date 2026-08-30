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
