using UnityEngine;
using SP.Combat;

namespace SP.Actors
{
    // Las clases del juego, en un solo lugar: como se llama, que modelo lleva,
    // que armas trae (las teclas 1/2/3 son sus ranuras) y de que color es su
    // marca sobre la cabeza.
    //
    //   ASALTO       fusil + pistola + LANZACOHETES (mira de mildots, x2,4)
    //   FLANQUEADOR  metralleta + pistola + escopeta (rapido, corto alcance)
    //   MEDICO       fusil + pistola, y cura a los aliados heridos
    //
    // Los enemigos no tienen clase propia: se les reparte una variante segun su
    // nombre (fusilero, comando, francotirador, explorador) con el mismo modelo
    // teñido de rojo.
    public static class SoldierClasses
    {
        public struct Def
        {
            public string Nombre;
            public string Malla;          // Resources/Soldados/Mesh_<Malla>
            public WeaponKind[] Loadout;
            public Color Color;
            public bool Enemigo;
        }

        static readonly Def Asalto = new Def { Nombre = "ASALTO", Malla = "Asalto", Loadout = new[] { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Rocket }, Color = new Color(1f, 0.55f, 0.2f) };
        static readonly Def Flanqueador = new Def { Nombre = "FLANQUEADOR", Malla = "Flanqueador", Loadout = new[] { WeaponKind.Smg, WeaponKind.Pistol, WeaponKind.Shotgun }, Color = new Color(0.3f, 0.9f, 0.85f) };
        static readonly Def Medico = new Def { Nombre = "MEDICO", Malla = "Medico", Loadout = new[] { WeaponKind.Rifle, WeaponKind.Pistol }, Color = new Color(0.45f, 1f, 0.5f) };
        static readonly Def Francotirador = new Def { Nombre = "FRANCOTIRADOR", Malla = "Francotirador", Loadout = new[] { WeaponKind.Sniper, WeaponKind.Pistol }, Color = new Color(0.7f, 0.8f, 1f) };

        static readonly Def Civil = new Def { Nombre = "CIVIL", Malla = "Medico", Loadout = new WeaponKind[0], Color = new Color(1f, 0.9f, 0.4f) };

        static readonly Def[] Enemigos =
        {
            new Def { Nombre = "FUSILERO", Malla = "Fusilero", Loadout = new[] { WeaponKind.Rifle, WeaponKind.Pistol }, Color = new Color(1f, 0.35f, 0.3f), Enemigo = true },
            new Def { Nombre = "COMANDO", Malla = "Asalto", Loadout = new[] { WeaponKind.Heavy, WeaponKind.Pistol }, Color = new Color(1f, 0.35f, 0.3f), Enemigo = true },
            new Def { Nombre = "FUSILERO", Malla = "Fusilero", Loadout = new[] { WeaponKind.Rifle, WeaponKind.Pistol }, Color = new Color(1f, 0.35f, 0.3f), Enemigo = true },
            new Def { Nombre = "EXPLORADOR", Malla = "Flanqueador", Loadout = new[] { WeaponKind.Smg, WeaponKind.Pistol }, Color = new Color(1f, 0.35f, 0.3f), Enemigo = true },
            new Def { Nombre = "FRANCOTIRADOR", Malla = "Francotirador", Loadout = new[] { WeaponKind.Sniper, WeaponKind.Pistol }, Color = new Color(1f, 0.35f, 0.3f), Enemigo = true },
            new Def { Nombre = "FUSILERO", Malla = "Fusilero", Loadout = new[] { WeaponKind.Rifle, WeaponKind.Pistol }, Color = new Color(1f, 0.35f, 0.3f), Enemigo = true },
        };

        public static Def Para(Soldier s)
        {
            if (s.Team == TeamId.Enemy)
            {
                int h = 0;
                foreach (char c in s.name) h = h * 31 + c;
                return Enemigos[Mathf.Abs(h) % Enemigos.Length];
            }
            switch (s.Role)
            {
                case RoleType.Assault: return Asalto;
                case RoleType.Flanker: return Flanqueador;
                case RoleType.Sniper: return Francotirador;
                case RoleType.Civilian: return Civil;
                default: return Medico;
            }
        }

        public static string NombreDe(Soldier s) => Para(s).Nombre;
    }
}
