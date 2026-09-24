using UnityEngine;

namespace SP.Presentation
{
    // Pedido explicito: "en la cinematica se desactivan todos los rombos,
    // todos". Un interruptor global unico en vez de que cada marcador (el
    // locator de enemigo/aliado, el del objetivo, el de interaccion) tenga
    // que enterarse por su cuenta de si hay una cinematica en curso -- lo
    // pone CinematicaDeVictoria al empezar y lo saca al terminar.
    public static class RomboVisibilidad
    {
        public static bool Suprimidos { get; set; }

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload
        // (mismo patron que el resto del proyecto): sin este reset, una
        // cinematica cortada a mitad de una sesion anterior dejaria los
        // rombos apagados para siempre en la siguiente.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Suprimidos = false;
    }
}
