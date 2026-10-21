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

        // Bugs #276/#279: con el modo dios el bando del jugador pega x3 y la escena corre a x4 de velocidad (pedido 2026-10-21: antes x2, luego x3).
        public const float MultiplicadorDeDanoPropio = 3f;
        public const float EscalaDeTiempo = 4f;

        // Multiplicador de un golpe segun quien lo da (solo el bando del jugador, y solo con el modo dios activo).
        public static float MultiplicadorDeDano(int atacanteId)
        {
            if (!Activo || atacanteId < 0) return 1f;
            var a = ActorRegistry.FindById(atacanteId);
            return a != null && a.Team == TeamId.Player && a.Role != RoleType.Civilian ? MultiplicadorDeDanoPropio : 1f;
        }

        // Lo llama el vigilante de velocidad: pone o saca el x4 sin pisar la pausa (0) ni la camara lenta (<1).
        public static bool VelocidadActiva = true;   // las corridas de checks lo apagan para no doblar sus tiempos
        public static void AjustarVelocidad()
        {
            if (!VelocidadActiva) return;
            float t = Time.timeScale;
            if (Activo && Mathf.Abs(t - 1f) < 0.001f) Time.timeScale = EscalaDeTiempo;
            else if (!Activo && Mathf.Abs(t - EscalaDeTiempo) < 0.001f) Time.timeScale = 1f;
        }

        // true si a esta vida no se le puede quitar nada ahora mismo.
        public static bool Protege(Health h)
        {
            if (!Activo || h == null) return false;
            if (h.TryGetComponent<Soldier>(out var s)) return s.Team == TeamId.Player;
            if (h.TryGetComponent<Vehicle>(out var v)) return v.Bando == TeamId.Player;
            return false;
        }
    }

    // Vigilante de la velocidad del modo dios: se reaplica sola al volver de pausas, camaras lentas y pantallas de resultado.
    public sealed class VigilanteDeVelocidadDios : MonoBehaviour
    {
        static VigilanteDeVelocidadDios instancia;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("VigilanteDeVelocidadDios") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<VigilanteDeVelocidadDios>();
        }
        void Update() { ModoDios.AjustarVelocidad(); }
        void OnDestroy() { if (Mathf.Abs(Time.timeScale - ModoDios.EscalaDeTiempo) < 0.001f) Time.timeScale = 1f; }
    }
}
