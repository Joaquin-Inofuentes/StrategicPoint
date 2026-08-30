using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;

namespace SP.Vehicles
{
    // Apuntado automático de la torreta cuando no hay un artillero humano
    // adentro: busca al enemigo vivo más cercano en rango, gira el cañón
    // hacia él (con el mismo giro lento de TurretWeapon.AimAt) y dispara
    // apenas queda bien apuntado. Así el tanque también participa de la
    // batalla sin que el jugador tenga que estar manejando la torreta.
    public class TurretAI : MonoBehaviour
    {
        [SerializeField] float range = 40f;
        [SerializeField] float retargetInterval = 0.4f;

        TurretWeapon turret;
        Vehicle vehicle;
        Soldier target;
        float retargetTimer;

        void Awake()
        {
            turret = GetComponent<TurretWeapon>();
            vehicle = GetComponentInParent<Vehicle>();
        }

        public void Tick(float dt)
        {
            if (turret == null) return;
            // Si hay un artillero de carne y hueso adentro, la IA se
            // aparta: el mouse del jugador manda.
            if (vehicle != null && vehicle.Gunner != null) return;

            retargetTimer -= dt;
            if (retargetTimer <= 0f || target == null || !target.Health.IsAlive)
            {
                retargetTimer = retargetInterval;
                target = ActorRegistry.FindNearestEnemyInRange(transform.position, TeamId.Player, range);
            }

            if (target == null || !target.Health.IsAlive) return;

            var aimPoint = target.transform.position;
            turret.AimAt(aimPoint, dt);
            if (turret.IsAimedAt(aimPoint)) turret.TryFire();
        }
    }
}
