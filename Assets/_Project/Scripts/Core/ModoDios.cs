using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Vehicles;

namespace SP.Core
{
    // MODO DIOS ([F4]): nadie del bando del jugador recibe dano. Ni el soldado que manejas,
    // ni la escuadra, ni el civil, ni el tanque propio. Los enemigos siguen siendo normales.
    // Sirve para probar, para mirar el mapa sin morir y para practicar ordenes.
    public static class ModoDios
    {
        public static bool Activo { get; private set; }

        // Aviso para la interfaz y el tutorial.
        public static event System.Action<bool> Cambio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activo = false; Cambio = null; }

        public static void Poner(bool activo)
        {
            if (Activo == activo) return;
            Activo = activo;
            GameLog.Line("Modo dios " + (activo ? "ACTIVADO" : "desactivado"));
            Cambio?.Invoke(activo);
        }

        public static bool Alternar() { Poner(!Activo); return Activo; }

        // true si a esta vida no se le puede quitar nada ahora mismo.
        public static bool Protege(Health h)
        {
            if (!Activo || h == null) return false;
            if (h.TryGetComponent<Soldier>(out var s)) return s.Team == TeamId.Player;
            if (h.TryGetComponent<Vehicle>(out var v)) return v.Bando == TeamId.Player;
            return false;
        }
    }
}
