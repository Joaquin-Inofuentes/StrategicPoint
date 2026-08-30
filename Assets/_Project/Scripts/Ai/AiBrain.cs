using System;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Vehicles;

namespace SP.Ai
{
    // Máquina de estados de un soldado no poseído. Sensa, persigue, ataca,
    // reacciona a que le disparen y a que le disparen a un aliado cercano,
    // y ejecuta órdenes explícitas del jugador (T). Cuando el soldado pasa
    // a ser poseído por el jugador, se suspende (IsPossessedByPlayer).
    public class AiBrain : MonoBehaviour
    {
        [SerializeField] float visionRange = 10f;
        [SerializeField] float attackRange = 6f;
        [SerializeField] float alertRadius = 30f;
        [SerializeField] float arriveThreshold = 0.6f;

        Soldier self;
        Soldier target;
        Vector3 orderDestination;
        bool hasOrder;
        bool bootstrapped;
        Vehicle mountTarget;
        IDisposable damageSub;

        Vector3[] patrolRoute;
        int patrolIndex;

        // Ronda de patrulla: mientras no haya nada más que hacer (Patrol),
        // camina de waypoint en waypoint en loop. Se corta solo si el
        // sensado detecta un enemigo (como cualquier otra cosa en Patrol).
        public void SetPatrolRoute(Vector3[] points)
        {
            patrolRoute = points;
            patrolIndex = 0;
        }

        public AiState State { get; private set; } = AiState.Patrol;
        public bool IsPossessedByPlayer { get; set; }
        public Soldier CurrentTarget => target;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            self = GetComponent<Soldier>();
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnAnyDamage);
        }

        void OnDestroy() => damageSub?.Dispose();

        void SetState(AiState next)
        {
            if (State == next) return;
            State = next;
            EventBus.Instance.Publish(new AiStateChangedEvent(self.Id, next.ToString()));
        }

        void OnAnyDamage(DamageTakenEvent evt)
        {
            if (self == null || !self.Health.IsAlive) return;

            var attacker = ActorRegistry.FindById(evt.AttackerId);
            if (attacker == null || !attacker.Health.IsAlive) return;

            // Me dispararon a mí: reacciono aunque esté fuera de mi rango de visión normal.
            if (evt.TargetId == self.Id)
            {
                if (State == AiState.Idle || State == AiState.Patrol || State == AiState.MovingToOrder)
                {
                    target = attacker;
                    hasOrder = false;
                    SetState(AiState.Chase);
                }
                return;
            }

            // Le dispararon a un aliado cerca de mí: me sumo a la pelea.
            var victim = ActorRegistry.FindById(evt.TargetId);
            if (victim == null || victim == self || victim.Team != self.Team) return;
            if (State != AiState.Idle && State != AiState.Patrol) return;

            float dist = Vector3.Distance(self.transform.position, victim.transform.position);
            if (dist > alertRadius) return;

            target = attacker;
            hasOrder = false;
            SetState(AiState.Chase);
        }

        public void IssueMoveOrder(Vector3 point)
        {
            if (!bootstrapped) Bootstrap();
            target = null;
            hasOrder = true;
            mountTarget = null;
            orderDestination = point;
            SetState(AiState.MovingToOrder);
        }

        public void IssueMountOrder(Vehicle vehicle)
        {
            if (vehicle == null) return;
            if (!bootstrapped) Bootstrap();
            target = null;
            hasOrder = true;
            mountTarget = vehicle;
            orderDestination = vehicle.transform.position;
            SetState(AiState.MovingToOrder);
        }

        public void IssueAttackOrder(Soldier enemy)
        {
            if (!bootstrapped) Bootstrap();
            target = enemy;
            hasOrder = true;
            SetState(AiState.MovingToAttackOrder);
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (IsPossessedByPlayer || self == null) return;
            if (!self.gameObject.activeInHierarchy) return;

            if (!self.Health.IsAlive)
            {
                SetState(AiState.Dead);
                return;
            }

            if (target != null && !target.Health.IsAlive)
                target = null;

            if (target == null && (State == AiState.Chase || State == AiState.Attack))
                SetState(hasOrder ? AiState.MovingToOrder : AiState.Patrol);

            // El sensado puede interrumpir una patrulla u orden de movimiento
            // simple, pero no una orden de ataque ni una de subir a un
            // vehículo ya en curso: esas son deliberadas.
            bool onProtectedOrder = State == AiState.MovingToAttackOrder ||
                (State == AiState.MovingToOrder && mountTarget != null);
            if (State != AiState.Chase && State != AiState.Attack && !onProtectedOrder)
            {
                var sensed = ActorRegistry.FindNearestEnemyInRange(self.transform.position, self.Team, visionRange);
                if (sensed != null)
                {
                    target = sensed;
                    hasOrder = false;
                    SetState(AiState.Chase);
                }
            }

            switch (State)
            {
                case AiState.Patrol:
                    if (patrolRoute != null && patrolRoute.Length > 0)
                    {
                        if (self.Motor.MoveTowards(patrolRoute[patrolIndex], 1f, dt))
                            patrolIndex = (patrolIndex + 1) % patrolRoute.Length;
                    }
                    break;

                case AiState.Idle:
                    break;

                case AiState.MovingToOrder:
                    if (self.Motor.MoveTowards(orderDestination, arriveThreshold, dt))
                    {
                        hasOrder = false;
                        if (mountTarget != null)
                        {
                            mountTarget.Mount(self);
                            mountTarget = null;
                            return; // el GameObject quedó inactivo: no tocar más estado.
                        }
                        EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));
                        SetState(AiState.Patrol);
                    }
                    break;

                case AiState.Chase:
                case AiState.MovingToAttackOrder:
                    if (target == null) { SetState(AiState.Patrol); break; }
                    float d = Vector3.Distance(self.transform.position, target.transform.position);
                    if (d <= attackRange) SetState(AiState.Attack);
                    else self.Motor.MoveTowards(target.transform.position, attackRange * 0.85f, dt);
                    break;

                case AiState.Attack:
                    if (target == null || !target.Health.IsAlive) { SetState(AiState.Patrol); break; }
                    float dd = Vector3.Distance(self.transform.position, target.transform.position);
                    if (dd > attackRange) { SetState(hasOrder ? AiState.MovingToAttackOrder : AiState.Chase); break; }

                    self.Motor.LookTowards(target.transform.position, dt);
                    self.Weapon.Tick(dt);
                    self.Weapon.TryFire(self.transform.position, (target.transform.position - self.transform.position).normalized);
                    break;
            }
        }
    }
}
