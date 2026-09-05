using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    public enum AimTargetType { None, Ally, Enemy, Vehicle, Ground, Obstacle }

    public struct AimResult
    {
        public AimTargetType Type;
        public Soldier Soldier;
        public Vehicle Vehicle;
        public Vector3 Point;
        // B4: raiz del objeto golpeado, para el anillo generico de apuntado
        // (SelectionRingFx necesita un Transform a quien seguir, y Point es
        // solo el punto de impacto -- no sirve para seguir a algo que se
        // mueve). Null en Ground/None: ahi no hay "algo" a marcar en su base.
        public Transform HitTransform;
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
                if (soldier != null && soldier != excludeSelf && soldier.Health.IsAlive)
                {
                    if (soldier.Team == TeamId.Player)
                    {
                        Highlight(soldier.Id);
                        return new AimResult { Type = AimTargetType.Ally, Soldier = soldier, Point = hit.point, HitTransform = soldier.transform };
                    }

                    Highlight(soldier.Id);
                    return new AimResult { Type = AimTargetType.Enemy, Soldier = soldier, Point = hit.point, HitTransform = soldier.transform };
                }

                ClearHighlight();

                var vehicle = hit.collider.GetComponentInParent<Vehicle>();
                if (vehicle != null)
                    return new AimResult { Type = AimTargetType.Vehicle, Vehicle = vehicle, Point = hit.point, HitTransform = vehicle.transform };

                var obstaculo = hit.collider.GetComponentInParent<ObstacleMarker>();
                if (obstaculo != null)
                    return new AimResult { Type = AimTargetType.Obstacle, Point = hit.point, HitTransform = obstaculo.transform };

                if (hit.collider.gameObject.name.StartsWith("Ground"))
                    return new AimResult { Type = AimTargetType.Ground, Point = hit.point };

                // CUALQUIER OTRO SOLIDO cuenta como suelo para el mando, con
                // el punto proyectado sobre el piso. Antes esto devolvia
                // None, y como todas las ordenes de RTS (mover, marcador,
                // fantasmas de formacion, vista previa de ruta) exigen
                // AimTargetType.Ground, un click derecho sobre un arbol,
                // una barricada o un barril no hacia NADA: ni orden, ni
                // marcador, ni aviso. El jugador clickea al lado de una
                // cobertura para reposicionarse y la escuadra lo ignora.
                //
                // Se ordena sobre el piso debajo del click y no sobre el
                // punto de impacto: si no, el destino quedaria a un metro
                // de altura arriba de la barricada.
                if (TryPuntoEnElPiso(ray, out var puntoPiso))
                    return new AimResult { Type = AimTargetType.Ground, Point = puntoPiso };

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

        // Altura del piso jugable. El suelo de la escena tiene su cara
        // superior en y=0; se cruza el rayo contra ese plano.
        const float AlturaDelPiso = 0f;

        static bool TryPuntoEnElPiso(Ray ray, out Vector3 punto)
        {
            punto = default;
            // Rayo paralelo al piso (o apuntando hacia arriba): no lo cruza.
            if (ray.direction.y >= -0.0001f) return false;
            float t = (AlturaDelPiso - ray.origin.y) / ray.direction.y;
            if (t < 0f) return false;
            punto = ray.origin + ray.direction * t;
            return true;
        }
    }
}
