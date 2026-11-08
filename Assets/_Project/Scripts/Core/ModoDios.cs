using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Vehicles;

namespace SP.Core
{
    // MODO DIOS ([F4]): nadie del bando del jugador recibe dano. Ni el soldado que manejas,
    // ni la escuadra ni el civil. El tanque propio pierde vida pero es indestructible: no baja de 1 (bugs #347/#359). Los enemigos siguen siendo normales.
    // Sirve para probar, para mirar el mapa sin morir y para practicar ordenes.
    public static class ModoDios
    {
        public static bool Activo { get; private set; }

        // Aviso para la interfaz y el tutorial.
        public static event System.Action<bool> Cambio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activo = false; Cambio = null; VelocidadActiva = true; }

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
        // Bug #354 (2026-11-06): la escena corre a x2 (antes x4) y el soldado que manejas corre x2 mas rapido; en la camara RTS (dar ordenes) todo va normal.
        public const float EscalaDeTiempo = 2f;
        // Bug #393 (2026-11-08: "quiero que sea el doble de la de ahora"): 2 -> 4.
        public const float FactorDeCarreraDelModoDios = 4f;
        // Bug #368: con el modo dios las cargas explosivas tardan 3 s (reales) en explotar.
        public const float SegundosDeMechaEnModoDios = 3f;
        // Lo lee SoldierMotor: el modo dios solo acelera al soldado que maneja el jugador.
        public static bool EnRts { get { var r = SP.CameraSystem.CameraRig.Instance; return r != null && r.Mode == SP.CameraSystem.ControlMode.Rts; } }

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
            bool gestionada = Mathf.Abs(t - 1f) < 0.001f || Mathf.Abs(t - EscalaDeTiempo) < 0.001f;   // no pisa la pausa (0) ni la camara lenta (<1)
            if (!gestionada) return;
            float objetivo = Activo && !EnRts ? EscalaDeTiempo : 1f;
            if (Mathf.Abs(t - objetivo) > 0.001f) Time.timeScale = objetivo;
        }

        // true si a esta vida no se le puede quitar nada ahora mismo.
        public static bool Protege(Health h)
        {
            if (!Activo || h == null) return false;
            if (h.TryGetComponent<Soldier>(out var s)) return s.Team == TeamId.Player;
            // Bug #004 (build): con el modo dios el tanque propio no mostraba ningun dano ("no me quitan vida"). Ahora los vehiculos del
            // jugador SI pierden vida (se ve la barra), pero no pasan de 1 punto: los golpes se topan en TopeDeDano y el guion puede destruirlos igual.
            // Bug #347 (2026-11-06: "en modo dios deberia ser indestructible"): el tanque propio ya no pierde NADA de vida con el modo dios (antes bajaba hasta 1).
            // Bug #359 (2026-11-06: "no me quitan nada de vida, quiero que cada disparo me saque al menos 15-20 %"): el tanque SI pierde vida con el modo dios
            // (se ve la barra) y no se destruye nunca: baja hasta 1 (ver TopeDeDano).
            return false;
        }

        // Dano que puede recibir de verdad esta vida con el modo dios activo: un vehiculo del jugador baja hasta 1 y ahi se frena.
        public static int TopeDeDano(Health h, int amount)
        {
            if (!Activo || h == null || amount <= 0) return amount;
            if (h.TryGetComponent<Vehicle>(out var v) && v.Bando == TeamId.Player) return Mathf.Max(0, Mathf.Min(amount, h.Current - 1));
            return amount;
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
