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
            // Aumento REAL de la mira al mantener el clic derecho (1 = sin zoom).
            // El FOV sale de esto: fov = 2*atan(tan(fov0/2)/ZoomFactor).
            public float ZoomFactor;
            public ReticleStyle Reticle;
            // Perdigones por disparo (escopeta) y explosion (lanzacohetes).
            public int Pellets;
            public float PelletSpreadDeg;
            public float ExplosionRadius;
            public float ProjectileSpeed;   // multiplicador de VelocidadBase
            public float ProjectileGravity;
            public string DisplayName;
        }

        public static Spec Get(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Pistol:
                    // Chica y corta. BALANCE REAL medido: con Damage=14 el
                    // dps SOSTENIDO (cargador completo + recarga, no solo
                    // el tiro suelto) daba Pistola 60,0 > Rifle 53,3 >
                    // Pesada 37,1 -- el arma pensada como respaldo debil
                    // superaba a las otras dos en el numero que de verdad
                    // importa en un tiroteo largo. Bajado a 8: 34,3 < 37,1
                    // < 53,3, o sea Rifle (el arma principal) > Pesada
                    // (pega fuerte pero recarga eterna) > Pistola
                    // (respaldo rapido, el mas debil de los tres). Cadencia
                    // y cargador intactos: sigue siendo la mas rapida de
                    // sacar y recargar, solo pega menos por tiro.
                    return new Spec { Damage = 8, Cooldown = 0.15f, Color = new Color(0.95f, 0.88f, 0.20f), VisualScale = new Vector3(0.13f, 0.13f, 0.28f), MagazineSize = 12, ReloadDuration = 1.0f, ZoomFactor = 1.4f, Reticle = ReticleStyle.Cruz, Pellets = 1, ProjectileSpeed = 1f, DisplayName = "Pistola" };
                case WeaponKind.Heavy:
                    // Grande y gruesa. Naranja quemado a proposito: el rosa/
                    // magenta anterior (0.80, 0.20, 0.55) se confundia a
                    // simple vista con el fucsia de un material roto.
                    return new Spec { Damage = 50, Cooldown = 0.80f, Color = new Color(0.85f, 0.35f, 0.10f), VisualScale = new Vector3(0.26f, 0.26f, 0.65f), MagazineSize = 4, ReloadDuration = 2.2f, ZoomFactor = 1.7f, Reticle = ReticleStyle.Anillo, Pellets = 1, ProjectileSpeed = 1f, DisplayName = "Ametralladora" };
                case WeaponKind.Rifle:
                    // Larga y angosta.
                    return new Spec { Damage = 26, Cooldown = 0.30f, Color = new Color(0.55f, 0.68f, 0.78f), VisualScale = new Vector3(0.15f, 0.15f, 0.55f), MagazineSize = 8, ReloadDuration = 1.5f, ZoomFactor = 2.2f, Reticle = ReticleStyle.Punto, Pellets = 1, ProjectileSpeed = 1f, DisplayName = "Fusil" };
                case WeaponKind.Smg:
                    // Rafaga corta y suelta: dps sostenido parecido al fusil (9 x 24 / 4,2 s).
                    return new Spec { Damage = 9, Cooldown = 0.11f, Color = new Color(0.62f, 0.9f, 0.45f), VisualScale = new Vector3(0.13f, 0.17f, 0.42f), MagazineSize = 24, ReloadDuration = 1.6f, ZoomFactor = 1.6f, Reticle = ReticleStyle.Chevron, Pellets = 1, ProjectileSpeed = 1f, DisplayName = "Metralleta" };
                case WeaponKind.Rocket:
                    // Lanzacohetes: 1 tiro, explosion de 5 m, vuela lento y cae un poco.
                    return new Spec { Damage = 95, Cooldown = 1.2f, Color = new Color(1f, 0.5f, 0.15f), VisualScale = new Vector3(0.18f, 0.2f, 0.7f), MagazineSize = 1, ReloadDuration = 2.8f, ZoomFactor = 2.4f, Reticle = ReticleStyle.Mildot, Pellets = 1, ExplosionRadius = 5f, ProjectileSpeed = 0.4f, ProjectileGravity = 3f, DisplayName = "Lanzacohetes" };
                case WeaponKind.Shotgun:
                    return new Spec { Damage = 9, Cooldown = 0.9f, Color = new Color(0.85f, 0.6f, 0.3f), VisualScale = new Vector3(0.14f, 0.16f, 0.6f), MagazineSize = 6, ReloadDuration = 2.4f, ZoomFactor = 1.25f, Reticle = ReticleStyle.Circulo, Pellets = 7, PelletSpreadDeg = 4.5f, ProjectileSpeed = 1f, DisplayName = "Escopeta" };
                case WeaponKind.Sniper:
                    return new Spec { Damage = 70, Cooldown = 1.4f, Color = new Color(0.7f, 0.8f, 0.95f), VisualScale = new Vector3(0.14f, 0.18f, 0.75f), MagazineSize = 5, ReloadDuration = 2.4f, ZoomFactor = 6f, Reticle = ReticleStyle.Telescopica, Pellets = 1, ProjectileSpeed = 1f, DisplayName = "Francotirador" };
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
