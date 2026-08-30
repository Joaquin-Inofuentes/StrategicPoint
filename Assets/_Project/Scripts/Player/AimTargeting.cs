using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Vehicles;

namespace SP.Player
{
    public enum AimTargetType { None, Ally, Vehicle, Ground }

    public struct AimResult
    {
        public AimTargetType Type;
        public Soldier Soldier;
        public Vehicle Vehicle;
        public Vector3 Point;
    }

    // Qué hay bajo el retículo o el cursor: un aliado poseíble, un vehículo,
    // o el suelo. Publica el resaltado para que la capa de presentación lo pinte.
    public class AimTargeting : MonoBehaviour
    {
        [SerializeField] float maxDistance = 200f;

        int lastHighlightedId = -1;

        public AimResult Evaluate(Ray ray, Soldier excludeSelf)
        {
            Physics.SyncTransforms();

            if (Physics.Raycast(ray, out var hit, maxDistance))
            {
                var soldier = hit.collider.GetComponentInParent<Soldier>();
                if (soldier != null && soldier != excludeSelf && soldier.Team == TeamId.Player && soldier.Health.IsAlive)
                {
                    Highlight(soldier.Id);
                    return new AimResult { Type = AimTargetType.Ally, Soldier = soldier, Point = hit.point };
                }

                ClearHighlight();

                var vehicle = hit.collider.GetComponentInParent<Vehicle>();
                if (vehicle != null)
                    return new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = hit.point };

                if (hit.collider.gameObject.name.StartsWith("Ground"))
                    return new AimResult { Type = AimTargetType.Ground, Point = hit.point };

                return new AimResult { Type = AimTargetType.None };
            }

            ClearHighlight();
            return new AimResult { Type = AimTargetType.None };
        }

        void Highlight(int soldierId)
        {
            if (lastHighlightedId == soldierId) return;
            lastHighlightedId = soldierId;
            EventBus.Instance.Publish(new SwapTargetHighlightedEvent(soldierId));
        }

        void ClearHighlight()
        {
            if (lastHighlightedId == -1) return;
            lastHighlightedId = -1;
            EventBus.Instance.Publish(new SwapTargetClearedEvent());
        }
    }
}
