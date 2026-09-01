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
            public int MagazineSize;
            public float ReloadDuration;
        }

        public static Spec Get(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                    // Chica y corta.
                    return new Spec { Damage = 14, Cooldown = 0.15f, Color = new Color(0.95f, 0.88f, 0.20f), VisualScale = new Vector3(0.13f, 0.13f, 0.28f), MagazineSize = 12, ReloadDuration = 1.0f };
                case WeaponKind.Heavy:
                    // Grande y gruesa. Naranja quemado a proposito: el rosa/
                    // magenta anterior (0.80, 0.20, 0.55) se confundia a
                    // simple vista con el fucsia de un material roto.
                    return new Spec { Damage = 50, Cooldown = 0.80f, Color = new Color(0.85f, 0.35f, 0.10f), VisualScale = new Vector3(0.26f, 0.26f, 0.65f), MagazineSize = 4, ReloadDuration = 2.2f };
                case WeaponKind.Rifle:
                    // Larga y angosta.
                    return new Spec { Damage = 26, Cooldown = 0.30f, Color = new Color(0.55f, 0.68f, 0.78f), VisualScale = new Vector3(0.15f, 0.15f, 0.55f), MagazineSize = 8, ReloadDuration = 1.5f };
                default:
                    // WeaponKind sin Spec definido en el catalogo: no debe
                    // pasar desapercibido como si fuera un Rifle elegido a
                    // proposito. Avisa fuerte y cae a Rifle solo como ultimo
                    // recurso, para no tirar el combate abajo por un dato
                    // faltante.
                    Debug.LogWarning($"[WeaponCatalog] WeaponKind.{kind} no tiene Spec definido -- usando stats de Rifle como resguardo.");
                    goto case WeaponKind.Rifle;
            }
        }
    }
}
