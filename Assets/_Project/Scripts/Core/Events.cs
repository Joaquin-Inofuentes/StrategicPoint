using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // Eventos: structs inmutables. Nunca llevan una referencia a un MonoBehaviour,
    // solo ids, posiciones y valores. Es la única vía por la que algo "se entera"
    // de lo que pasó, sin conocer a quién se lo publicó.

    public readonly struct DamageTakenEvent
    {
        public readonly int TargetId;
        public readonly int AttackerId;
        public readonly int Amount;
        public readonly int RemainingHealth;

        public DamageTakenEvent(int targetId, int attackerId, int amount, int remainingHealth)
        {
            TargetId = targetId;
            AttackerId = attackerId;
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }

    public readonly struct EntityDiedEvent
    {
        public readonly int ActorId;
        public EntityDiedEvent(int actorId) => ActorId = actorId;
    }

    public readonly struct ShotFiredEvent
    {
        public readonly int ShooterId;
        public ShotFiredEvent(int shooterId) => ShooterId = shooterId;
    }

    public readonly struct ProjectileReturnedEvent
    {
        public readonly int ProjectileInstanceId;
        public ProjectileReturnedEvent(int id) => ProjectileInstanceId = id;
    }

    public readonly struct AiStateChangedEvent
    {
        public readonly int ActorId;
        public readonly string NewState;
        public AiStateChangedEvent(int actorId, string newState)
        {
            ActorId = actorId;
            NewState = newState;
        }
    }

    public readonly struct PossessionChangedEvent
    {
        public readonly int FromId;
        public readonly int ToId;
        public PossessionChangedEvent(int fromId, int toId)
        {
            FromId = fromId;
            ToId = toId;
        }
    }

    public readonly struct SwapTargetHighlightedEvent
    {
        public readonly int ActorId;
        public SwapTargetHighlightedEvent(int actorId) => ActorId = actorId;
    }

    public readonly struct SwapTargetClearedEvent
    {
    }

    public readonly struct MoveOrderIssuedEvent
    {
        public readonly int ActorId;
        public readonly Vector3 Destination;
        public MoveOrderIssuedEvent(int actorId, Vector3 destination)
        {
            ActorId = actorId;
            Destination = destination;
        }
    }

    public readonly struct OrderCompletedEvent
    {
        public readonly int ActorId;
        public OrderCompletedEvent(int actorId) => ActorId = actorId;
    }

    public readonly struct SelectionChangedEvent
    {
        public readonly List<int> SelectedIds;
        public SelectionChangedEvent(List<int> selectedIds) => SelectedIds = selectedIds;
    }
}
