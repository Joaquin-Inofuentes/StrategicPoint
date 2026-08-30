using UnityEngine;
using SP.Core;

namespace SP.Combat
{
    // Única responsabilidad: llevar la cuenta de puntos de vida y avisar
    // cuando cambian o llegan a cero. No sabe que existe una barra de vida,
    // ni un sonido, ni una animación.
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour
    {
        [SerializeField] int maxHealth = 100;

        public int MaxHealth => maxHealth;
        public int Current { get; private set; }
        public bool IsAlive => Current > 0;
        public int ActorId { get; private set; }

        public void Initialize(int actorId, int max)
        {
            ActorId = actorId;
            maxHealth = max;
            Current = max;
        }

        public void TakeDamage(int amount, int attackerId)
        {
            if (!IsAlive) return;

            Current = Mathf.Max(0, Current - amount);
            EventBus.Instance.Publish(new DamageTakenEvent(ActorId, attackerId, amount, Current));

            if (Current <= 0)
                EventBus.Instance.Publish(new EntityDiedEvent(ActorId));
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }
    }
}
