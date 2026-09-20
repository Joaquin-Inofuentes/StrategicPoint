using UnityEngine;

namespace SP.Core
{
    // Con "Enter Play Mode sin recarga de dominio" los estaticos sobreviven entre una corrida y la otra
    // (modo dios, regeneracion apagada por el tutorial, segundos de demolicion...) y ya causaron falsos fallos.
    // Se restablecen todos juntos al registrar el subsistema, antes de que cargue ninguna escena.
    public static class ReinicioDeEstaticos
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Restablecer();

        public static void Restablecer()
        {
            ModoDios.Poner(false);
            SP.Combat.Health.RegeneracionPermitida = true;
            SP.Player.Demolicion.Segundos = SP.Player.Demolicion.SegundosNormales;
            SP.Combat.WeaponHolder.ReservasActivas = false;
            SP.Mision.EstadoDePartida.Reiniciar();
        }
    }
}
