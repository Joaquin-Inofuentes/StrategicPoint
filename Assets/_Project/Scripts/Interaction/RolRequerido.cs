using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Interaction
{
    // Bugs #083/#084: apretar [E] frente a algo que exige OTRO rol no hacia nada ("aprieto E y no pasa nada"). Este es el unico
    // lugar que dice, con el mismo formato, "DEBES SER {ROL} PARA {ACCION}" y a quien mandar con [Q]. Lo usan los paneles y la
    // computadora de la Operacion (OperacionTerminal), la demolicion (Demolicion) y, en WP9a, las cargas explosivas.
    public static class RolRequerido
    {
        public const float Enfriamiento = 1.5f;   // para que mantener [E] no repita el cartel y el clic cada frame

        static float proximo;
        public static string UltimoTexto { get; private set; }
        public static int Cantidad { get; private set; }

        static readonly Color FondoRojo = new Color(0.50f, 0.05f, 0.05f, 0.92f);

        // Para pruebas y para arrancar de cero tras un Play: borra el enfriamiento y los contadores.
        public static void Reiniciar() { proximo = 0f; UltimoTexto = null; Cantidad = 0; }

        public static string Nombre(RoleType rol)
        {
            switch (rol)
            {
                case RoleType.Assault: return "ASALTO";
                case RoleType.Flanker: return "FLANQUEADOR";
                case RoleType.Medic: return "MÉDICO";
                case RoleType.Sniper: return "FRANCOTIRADOR";
                case RoleType.Civilian: return "CIVIL";
                default: return "ENEMIGO";
            }
        }

        // El aliado vivo de la escuadra con ese rol (para sugerir a quien mandar con [Q]).
        public static Soldier AliadoCon(RoleType rol, Soldier excluir = null)
        {
            Soldier mejor = null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s == excluir || s.Team != TeamId.Player || s.Role != rol) continue;
                if (s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                mejor = s;
                break;
            }
            return mejor;
        }

        // "Soldado_2_Kes" -> "KES" (los nombres de la escena traen el prefijo del objeto).
        public static string NombreCorto(Soldier s)
        {
            string n = s != null ? s.DisplayName : null;
            if (string.IsNullOrEmpty(n)) return "ALIADO";
            int i = n.LastIndexOf('_');
            if (i >= 0 && i < n.Length - 1) n = n.Substring(i + 1);
            return n.ToUpperInvariant();
        }

        public static string Texto(RoleType rol, string accion, Soldier yo)
        {
            var t = "DEBES SER " + Nombre(rol) + " PARA " + accion;
            var aliado = AliadoCon(rol, yo);
            if (aliado != null) t += "  ·  [Q] ENVIAR A " + NombreCorto(aliado);
            return t;
        }

        // Muestra el cartel rojo y el clic seco. Devuelve true si lo mostro (false = estaba en enfriamiento).
        public static bool Avisar(Soldier yo, RoleType rol, string accion = "INTERACTUAR CON ESTO")
        {
            if (Time.unscaledTime < proximo) return false;
            proximo = Time.unscaledTime + Enfriamiento;
            string texto = Texto(rol, accion, yo);
            UltimoTexto = texto;
            Cantidad++;
            AvisoCentral.Mostrar(texto, 2.4f, FondoRojo);
            Feedback.Accion(SfxKind.EmptyClick, texto, null, Feedback.Bad, aviso: false, pulso: false, volumen: 0.6f);
            return true;
        }
    }
}
