using UnityEngine;

namespace SP.Operacion
{
    // R15 ("apagar UI segun corresponda para interacciones: revivir, explotar cosas"): mientras el jugador mantiene [E] para revivir, plantar una carga o
    // hackear una computadora, el panel de objetivo baja a casi transparente y queda solo el circulo de progreso de la mira. Quien interactua llama a
    // Marcar() cada frame que sostiene; deja de estar activo 0,25 s despues del ultimo llamado.
    public static class UiDeInteraccion
    {
        static float hasta = -1f;
        public static bool Activa => Time.unscaledTime < hasta;
        public static void Marcar() { hasta = Time.unscaledTime + 0.25f; }
        public const float AlfaDelPanel = 0.12f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { hasta = -1f; }
    }
}
