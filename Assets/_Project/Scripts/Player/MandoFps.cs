using UnityEngine;
using UnityEngine.InputSystem;

namespace SP.Player
{
    // Lectura del gamepad para la vista en primera persona. El juego lee el teclado directo (no usa InputActions),
    // asi que el mando se suma como una segunda fuente en los mismos puntos: no reemplaza nada.
    //   Stick izq. = mover · Stick der. = mirar · RT = disparar · A = saltar · B (mantener) = agacharse
    //   Click stick izq. = correr · X = recargar · RB / LB = arma siguiente / anterior · Start = pausa
    // Fuera de alcance por ahora: vista tactica (RTS), radial de ordenes y vehiculos.
    public static class MandoFps
    {
        public const float ZonaMuerta = 0.18f;
        public const float GradosPorSegundo = 190f;

        public static Gamepad Pad => Gamepad.current;
        public static bool Conectado => Gamepad.current != null;

        // Aplica zona muerta radial y reescala para que el movimiento fino sea posible.
        public static Vector2 Zona(Vector2 v)
        {
            float m = v.magnitude;
            if (m < ZonaMuerta) return Vector2.zero;
            return v.normalized * Mathf.Clamp01((m - ZonaMuerta) / (1f - ZonaMuerta));
        }

        public static Vector2 Mover => Pad != null ? Zona(Pad.leftStick.ReadValue()) : Vector2.zero;
        public static Vector2 Mirar => Pad != null ? Zona(Pad.rightStick.ReadValue()) : Vector2.zero;
        public static bool Disparar => Pad != null && Pad.rightTrigger.ReadValue() > 0.5f;
        public static bool Saltar => Pad != null && Pad.buttonSouth.wasPressedThisFrame;
        public static bool Agachar => Pad != null && Pad.buttonEast.isPressed;
        public static bool Correr => Pad != null && Pad.leftStickButton.isPressed;
        public static bool Recargar => Pad != null && Pad.buttonWest.wasPressedThisFrame;
        public static bool ArmaSiguiente => Pad != null && Pad.rightShoulder.wasPressedThisFrame;
        public static bool ArmaAnterior => Pad != null && Pad.leftShoulder.wasPressedThisFrame;
        public static bool Pausa => Pad != null && Pad.startButton.wasPressedThisFrame;
    }
}
