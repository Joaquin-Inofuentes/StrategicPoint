using UnityEngine;
using SP.Actors;
using SP.Ai;

namespace SP.Player
{
    // La consciencia que salta de cuerpo en cuerpo. Traduce intención en
    // llamadas al soldado que ocupa. Es único en la escena.
    public class PlayerBrain : MonoBehaviour
    {
        public Soldier Current { get; private set; }

        public void Possess(Soldier soldier)
        {
            if (Current != null)
            {
                var previousBrain = Current.GetComponent<AiBrain>();
                if (previousBrain != null) previousBrain.IsPossessedByPlayer = false;
            }

            Current = soldier;

            var brain = soldier.GetComponent<AiBrain>();
            if (brain != null) brain.IsPossessedByPlayer = true;
        }

        public void Move(Vector3 worldDirection, float dt) => Current?.Motor.Move(worldDirection, dt);

        public void RotateYaw(float yawDeltaDegrees) => Current?.Motor.RotateYaw(yawDeltaDegrees);

        public bool Fire()
        {
            if (Current == null) return false;
            return Current.Weapon.TryFire(Current.transform.position, Current.transform.forward);
        }
    }
}
