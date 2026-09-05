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

        // Devuelve false si no se pudo poseer. Antes esto no tenia ninguna
        // guarda: Possess(null) reventaba en la linea del GetComponent, y
        // PossessionService.Swap volvia a reventar leyendo target.Id. La
        // llamada de arranque (PlayerInputDriver hace Possess(Squad[0]))
        // depende de que la lista este bien cableada en la escena: si esa
        // primera posicion quedaba vacia, el juego moria en el Start con
        // un NullReferenceException que no dice cual es la lista.
        //
        // Tampoco se puede poseer un cadaver: varias rutas de posesion ya
        // lo verificaban por su cuenta y otras no, asi que la regla vive
        // aca, donde no se puede saltear.
        public bool Possess(Soldier soldier)
        {
            if (soldier == null)
            {
                Debug.LogError("[PlayerBrain] Possess(null): revisa que la escuadra este cableada en la escena.");
                return false;
            }
            if (soldier.Health != null && !soldier.Health.IsAlive)
            {
                Debug.LogWarning($"[PlayerBrain] No se puede poseer a {soldier.DisplayName}: esta muerto.");
                return false;
            }

            if (Current != null)
            {
                var previousBrain = Current.GetComponent<AiBrain>();
                if (previousBrain != null) previousBrain.IsPossessedByPlayer = false;
            }

            Current = soldier;

            var brain = soldier.GetComponent<AiBrain>();
            if (brain != null) brain.IsPossessedByPlayer = true;
            return true;
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
