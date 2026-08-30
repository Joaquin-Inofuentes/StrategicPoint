using UnityEngine;
using SP.Core;

namespace SP.Combat
{
    public readonly struct WeaponPickedUpEvent
    {
        public readonly int SoldierId;
        public readonly WeaponKind Kind;
        public WeaponPickedUpEvent(int soldierId, WeaponKind kind)
        {
            SoldierId = soldierId;
            Kind = kind;
        }
    }

    // Cubo en el mundo que representa un arma. Al equiparla (E cerca), cambia
    // el arma del soldado: daño, cadencia y el color de lo que dispara.
    public class WeaponPickup : MonoBehaviour
    {
        [SerializeField] WeaponKind kind = WeaponKind.Rifle;
        [SerializeField] int damage = 34;
        [SerializeField] float cooldown = 0.35f;
        [SerializeField] Color color = Color.white;

        public WeaponKind Kind => kind;
        public Color Color => color;

        public void Configure(WeaponKind weaponKind, int weaponDamage, float weaponCooldown, Color weaponColor)
        {
            kind = weaponKind;
            damage = weaponDamage;
            cooldown = weaponCooldown;
            color = weaponColor;
        }

        public void EquipOn(WeaponHolder holder, int soldierId)
        {
            if (holder == null) return;
            holder.EquipWeapon(kind, damage, cooldown, color);
            EventBus.Instance.Publish(new WeaponPickedUpEvent(soldierId, kind));
        }
    }
}
