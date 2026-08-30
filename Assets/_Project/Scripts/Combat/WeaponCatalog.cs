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
            // Escala local del cubo visible en la mano: cada arma se ve
            // como una forma distinta (no solo un color distinto), para que
            // cambiar de arma con 1/2/3 se note de un vistazo.
            public Vector3 VisualScale;
        }

        public static Spec Get(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                    // Chica y corta.
                    return new Spec { Damage = 14, Cooldown = 0.15f, Color = new Color(0.95f, 0.88f, 0.20f), VisualScale = new Vector3(0.13f, 0.13f, 0.28f) };
                case WeaponKind.Heavy:
                    // Grande y gruesa.
                    return new Spec { Damage = 50, Cooldown = 0.80f, Color = new Color(0.80f, 0.20f, 0.55f), VisualScale = new Vector3(0.26f, 0.26f, 0.65f) };
                case WeaponKind.Rifle:
                default:
                    // Larga y angosta.
                    return new Spec { Damage = 26, Cooldown = 0.30f, Color = new Color(0.55f, 0.68f, 0.78f), VisualScale = new Vector3(0.15f, 0.15f, 0.55f) };
            }
        }
    }
}
