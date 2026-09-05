using SP.Actors;
using SP.Core;

namespace SP.Player
{
    // Ejecuta la transferencia de control y la anuncia. El cuerpo abandonado
    // no se destruye ni se congela: su AiBrain se reactiva solo.
    public static class PossessionService
    {
        // Devuelve false si la posesion no se pudo hacer. El evento solo se
        // publica cuando de verdad cambio el control: antes se publicaba
        // siempre, asi que un intento fallido igual movia toda la UI que
        // escucha PossessionChangedEvent (marcador de poseido, HUD, camara)
        // hacia un soldado que nadie estaba controlando.
        public static bool Swap(PlayerBrain brain, Soldier target)
        {
            if (brain == null || target == null) return false;

            int fromId = brain.Current != null ? brain.Current.Id : -1;
            if (!brain.Possess(target)) return false;

            EventBus.Instance.Publish(new PossessionChangedEvent(fromId, target.Id));
            return true;
        }
    }
}
