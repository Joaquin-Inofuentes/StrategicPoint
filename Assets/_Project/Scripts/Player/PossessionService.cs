using SP.Actors;
using SP.Core;

namespace SP.Player
{
    // Ejecuta la transferencia de control y la anuncia. El cuerpo abandonado
    // no se destruye ni se congela: su AiBrain se reactiva solo.
    public static class PossessionService
    {
        public static void Swap(PlayerBrain brain, Soldier target)
        {
            int fromId = brain.Current != null ? brain.Current.Id : -1;
            brain.Possess(target);
            EventBus.Instance.Publish(new PossessionChangedEvent(fromId, target.Id));
        }
    }
}
