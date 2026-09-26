using SP.Core;
using SP.Actors;

namespace SP.Combat
{
    public static class TeamCombatState
    {
        public static float SegundosSinAccion { get; private set; } = 999f;
        static System.IDisposable damageSub;

        public static void Init()
        {
            SegundosSinAccion = 999f;
            damageSub?.Dispose();
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        public static void Tick(float dt)
        {
            SegundosSinAccion += dt;
        }

        static void OnDamage(DamageTakenEvent e)
        {
            var target = ActorRegistry.FindById(e.TargetId);
            var attacker = ActorRegistry.FindById(e.AttackerId);
            if ((target != null && target.Team == TeamId.Player) || (attacker != null && attacker.Team == TeamId.Player))
            {
                SegundosSinAccion = 0f;
            }
        }
    }
}
