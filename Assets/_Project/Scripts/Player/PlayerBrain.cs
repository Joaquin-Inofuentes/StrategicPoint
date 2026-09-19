using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;

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
                if (Current.Weapon != null) Current.Weapon.LimitaMunicion = false;
            }

            Current = soldier;

            var brain = soldier.GetComponent<AiBrain>();
            if (brain != null) brain.IsPossessedByPlayer = true;
            if (soldier.Weapon != null) soldier.Weapon.LimitaMunicion = true;
            return true;
        }

        public void Move(Vector3 worldDirection, float dt) => Current?.Motor.Move(worldDirection, dt);

        public void RotateYaw(float yawDeltaDegrees) => Current?.Motor.RotateYaw(yawDeltaDegrees);

        // aimPoint: el punto de mundo que el cursor/mira esta apuntando
        // DE VERDAD (el resultado del mismo rayo de camara que ya usa
        // AimTargeting para resolver a que le apunta el jugador).
        //
        // BUG REAL encontrado al revisar la punteria: sin esto, la
        // direccion del disparo salia de transform.forward del soldado mas
        // el pitch de la camara -- una direccion calculada desde el CUERPO,
        // mientras que la bala en si sale del Muzzle (la punta del arma,
        // colgada de la mano, a una posicion distinta del cuerpo) y el
        // reticulo en pantalla apunta con el rayo de la CAMARA (que ahora
        // ademas esta corrida al costado por el encuadre a hombro). Tres
        // puntos de origen distintos (cuerpo, camara, boca del arma) nunca
        // van a alinearse por casualidad: de cerca, o apuntando al costado,
        // la bala salia notoriamente desviada de donde mostraba la mira.
        // Ahora la bala apunta desde la boca del arma HACIA el mismo punto
        // que el reticulo esta marcando -- sea el impacto real (result.Point)
        // o, si no hay nada que golpear, un punto lejano sobre el mismo
        // rayo de camara -- que es justo lo que cualquier shooter hace.
        public bool Fire(Vector3? aimPoint = null)
        {
            if (Current == null) return false;

            // Sin aimPoint (IA, demo automatica): se mantiene el
            // comportamiento de siempre, forward del cuerpo + pitch de la
            // camara del jugador si la hay.
            Vector3 direction = Current.transform.forward;
            var rig = CameraRig.Instance;
            if (rig != null)
                direction = (Current.transform.rotation * Quaternion.Euler(-rig.Pitch, 0f, 0f)) * Vector3.forward;

            if (aimPoint.HasValue)
            {
                var muzzle = Current.Weapon.Muzzle;
                Vector3 origen = muzzle != null ? muzzle.position : Current.transform.position;
                Vector3 haciaElPunto = aimPoint.Value - origen;
                if (haciaElPunto.sqrMagnitude > 0.0001f) direction = haciaElPunto.normalized;
            }

            return Current.Weapon.TryFire(Current.transform.position, direction);
        }
    }
}
