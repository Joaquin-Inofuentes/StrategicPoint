using UnityEngine;

namespace SP.Presentation
{
    // Impacto de un proyectil, en cubitos (pedido: "si es soldado cubitos de
    // distintos tamaños rojos, si es piso verde y si es obstaculo amarillo").
    // Reusa DebrisPool: presupuesto fijo, gravedad, un rebote y encogido.
    public enum ImpactSurface { Soldier, Ground, Obstacle }

    public static class ImpactCubes
    {
        public static readonly Color Red = new Color(1f, 0.10f, 0.08f);
        public static readonly Color Green = new Color(0.20f, 0.95f, 0.25f);
        public static readonly Color Yellow = new Color(1f, 0.88f, 0.10f);

        public static Color ColorOf(ImpactSurface s)
        {
            switch (s)
            {
                case ImpactSurface.Soldier: return Red;
                case ImpactSurface.Ground: return Green;
                default: return Yellow;
            }
        }

        // Cuantos cubitos salen por impacto (un balazo chico da menos que uno
        // de cañon). Publico para poder medirlo en tests.
        public static int CountFor(ImpactSurface s, int damage)
        {
            int extra = Mathf.Clamp(damage / 12, 0, 8);
            return (s == ImpactSurface.Soldier ? 8 : 6) + extra;
        }

        public static int Spawn(Vector3 point, Vector3 normal, ImpactSurface surface, int damage)
        {
            if (!Application.isPlaying) return 0;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            normal.Normalize();

            var color = ColorOf(surface);
            int n = CountFor(surface, damage);
            for (int i = 0; i < n; i++)
            {
                // Cono alrededor de la normal + una componente hacia arriba,
                // para que salten y caigan en vez de deslizarse.
                var dir = (normal * 1.1f + Random.insideUnitSphere).normalized;
                dir.y = Mathf.Abs(dir.y) * 0.6f + 0.35f;
                float speed = Random.Range(2.2f, 6.5f);
                // Tamaños bien distintos entre si: unos granos y unos cubos.
                float size = Random.value < 0.25f ? Random.Range(0.11f, 0.17f) : Random.Range(0.04f, 0.10f);
                DebrisPool.Spawn(point + normal * 0.05f, dir * speed, color, size, Random.Range(0.55f, 1.1f));
            }
            return n;
        }
    }
}
