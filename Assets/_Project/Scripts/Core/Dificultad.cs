using UnityEngine;
using SP.Actors;
using SP.Combat;

namespace SP.Core
{
    public enum NivelDificultad { Facil, Medio, Dificil }

    // Dificultad de la partida: se elige al tocar JUGAR y reparte "potenciadores" de vida y
    // dano a tres grupos: los ENEMIGOS, los ALIADOS (la escuadra que no manejas) y el
    // JUGADOR (el soldado que tenes en las manos).
    //
    //   vida enemigos / aliados : se multiplica la vida maxima al empezar la partida.
    //   vida del jugador        : equivale a "mas vida" pero sin tocar los numeros del HUD:
    //                             el dano que recibe el soldado que manejas se divide por ese factor.
    //   dano                    : se multiplica el dano de cada golpe segun QUIEN lo da.
    //
    // MEDIO es el balance pensado (la escuadra tiene una ventaja chica); DIFICIL es el equilibrio parejo de las armas. Solo corre en la partida principal
    // (Activa); el tutorial y la suite de pruebas quedan siempre en x1.
    public static class Dificultad
    {
        public struct Perfil
        {
            public string Nombre;
            public float VidaEnemigos, DanoEnemigos, VidaAliados, DanoAliados, VidaJugador, DanoJugador;
            public float CantidadDeOleadas;      // multiplica el tamano de las oleadas de la mision
            public string Frase;
        }

        const string Pref = "sp_dificultad";
        static NivelDificultad? actual;

        public static bool Activa { get; set; }

        // En DIFICIL las explosiones de tu propio bando (tus granadas, tus cohetes) tambien te dañan, al 50 %.
        public static bool FuegoAmigoExplosivo => Activa && Actual == NivelDificultad.Dificil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activa = false; actual = null; }

        public static NivelDificultad Actual
        {
            get
            {
                if (!actual.HasValue) actual = (NivelDificultad)Mathf.Clamp(PlayerPrefs.GetInt(Pref, (int)NivelDificultad.Medio), 0, 2);
                return actual.Value;
            }
            set
            {
                actual = value;
                PlayerPrefs.SetInt(Pref, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static Perfil Datos(NivelDificultad n)
        {
            switch (n)
            {
                case NivelDificultad.Facil:
                    return new Perfil { Nombre = "FACIL", VidaEnemigos = 0.75f, DanoEnemigos = 0.65f, VidaAliados = 1.35f, DanoAliados = 1.25f, VidaJugador = 1.5f, DanoJugador = 1.3f, CantidadDeOleadas = 0.75f, Frase = "Para aprender: enemigos debiles y tu escuadra aguanta mucho mas." };
                case NivelDificultad.Dificil:
                    return new Perfil { Nombre = "DIFICIL", VidaEnemigos = 1.05f, DanoEnemigos = 1.05f, VidaAliados = 1f, DanoAliados = 1f, VidaJugador = 1f, DanoJugador = 1f, CantidadDeOleadas = 1.4f, Frase = "Enemigos potenciados, oleadas 40% mas grandes y cero ayuda." };
                default:
                    return new Perfil { Nombre = "MEDIO", VidaEnemigos = 0.95f, DanoEnemigos = 0.9f, VidaAliados = 1.15f, DanoAliados = 1.05f, VidaJugador = 1.25f, DanoJugador = 1.1f, CantidadDeOleadas = 1f, Frase = "El balance pensado: tu escuadra tiene una pequeña ventaja." };
            }
        }

        public static Perfil PerfilActual => Datos(Actual);

        static string Pct(float f)
        {
            int p = Mathf.RoundToInt((f - 1f) * 100f);
            return p == 0 ? "x1" : (p > 0 ? "+" : "") + p + "%";
        }

        // Texto de una linea por grupo, para el menu y el cartel de inicio.
        public static string Resumen(NivelDificultad n)
        {
            var d = Datos(n);
            return $"ENEMIGOS  vida {Pct(d.VidaEnemigos)} · daño {Pct(d.DanoEnemigos)}\n" +
                   $"ALIADOS   vida {Pct(d.VidaAliados)} · daño {Pct(d.DanoAliados)}\n" +
                   $"VOS       vida {Pct(d.VidaJugador)} · daño {Pct(d.DanoJugador)}";
        }

        // ---------------- vida ----------------
        // Se llama una vez al empezar la partida principal. Devuelve cuantos soldados se ajustaron.
        public static int AplicarVida()
        {
            int n = 0;
            foreach (var s in ActorRegistry.All) if (AjustarVida(s)) n++;
            return n;
        }

        public static bool AjustarVida(Soldier s)
        {
            if (s == null || s.Health == null) return false;
            var d = Datos(Actual);
            float k = s.Team == TeamId.Enemy ? d.VidaEnemigos : (s.Role == RoleType.Civilian ? 1f : d.VidaAliados);
            if (Mathf.Approximately(k, 1f)) return false;
            s.Health.Initialize(s.Id, Mathf.Max(1, Mathf.RoundToInt(s.Health.MaxHealth * k)));
            return true;
        }

        // ---------------- dano ----------------
        // Multiplicador total de un golpe: quien lo da y a quien le llega.
        public static float MultiplicadorDeDano(int atacanteId, Health victima)
        {
            if (!Activa) return 1f;
            var d = Datos(Actual);
            float m = 1f;

            var atacante = atacanteId >= 0 ? ActorRegistry.FindById(atacanteId) : null;
            if (atacante != null)
            {
                if (atacante.Team == TeamId.Enemy) m *= d.DanoEnemigos;
                else if (atacante.Brain != null && atacante.Brain.IsPossessedByPlayer) m *= d.DanoJugador;
                else if (atacante.Role != RoleType.Civilian) m *= d.DanoAliados;
            }

            if (victima != null)
            {
                var v = victima.GetComponent<Soldier>();
                if (v != null && v.Brain != null && v.Brain.IsPossessedByPlayer && d.VidaJugador > 0f) m /= d.VidaJugador;
            }
            return m;
        }
    }
}
