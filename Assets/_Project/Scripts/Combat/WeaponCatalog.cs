using UnityEngine;

namespace SP.Combat
{
    // Estadísticas y color de cada arma, en un solo lugar. Las armas
    // recogibles del piso (WeaponPickup) y las teclas rápidas 1/2/3 del
    // jugador usan los mismos valores, para que sean intercambiables.
    public static class WeaponCatalog
    {
        public struct Spec
        {
            public int Damage;
            public float Cooldown;
            public Color Color;
        }

        public static Spec Get(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                    return new Spec { Damage = 14, Cooldown = 0.15f, Color = new Color(0.95f, 0.88f, 0.20f) };
                case WeaponKind.Heavy:
                    return new Spec { Damage = 50, Cooldown = 0.80f, Color = new Color(0.80f, 0.20f, 0.55f) };
                case WeaponKind.Rifle:
                default:
                    return new Spec { Damage = 26, Cooldown = 0.30f, Color = new Color(0.55f, 0.68f, 0.78f) };
            }
        }
    }
}
