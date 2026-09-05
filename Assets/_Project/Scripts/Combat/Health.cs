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

        // Quien pego el ultimo golpe. Sirve para atribuir la baja (¿la
        // hice yo o mi escuadra?) y para señalar a quien te mato durante
        // la camara de muerte -- que es justo lo que el jugador mas quiere
        // saber en ese momento.
        public int LastAttackerId { get; private set; } = -1;

        public void Initialize(int actorId, int max)
        {
            ActorId = actorId;
            maxHealth = max;
            Current = max;
            // Revivir (HeadlessTestRunner y AutoDemoRunner llaman Initialize()
            // para esto) tiene que borrar tambien quien te mato la vez
            // anterior: si no, un soldado recien revivido queda con
            // LastAttackerId apuntando al verdugo de su muerte ANTERIOR hasta
            // que alguien le pegue de nuevo en esta vida.
            LastAttackerId = -1;
        }

        public void TakeDamage(int amount, int attackerId)
        {
            if (!IsAlive) return;

            // BUG REAL medido: con amount negativo esto CURABA. Con 70 de
            // vida, TakeDamage(-50) dejaba al soldado en 100 y ademas
            // publicaba un DamageTakenEvent de -50, que es lo que mueve el
            // numero flotante de daño, la viñeta roja y la flecha de
            // direccion: en pantalla se leia como un golpe mientras el
            // soldado se curaba. Curar tiene su propio metodo (Heal) y su
            // propio evento.
            //
            // El caso amount == 0 tambien se corta: cualquier calculo de
            // daño que redondee a cero (una caida por distancia, un
            // multiplicador chico) encendia todo el feedback de impacto
            // -- numero flotante, viñeta, flecha -- sin quitar un solo
            // punto de vida.
            if (amount <= 0) return;

            Current = Mathf.Clamp(Current - amount, 0, maxHealth);
            LastAttackerId = attackerId;
            EventBus.Instance.Publish(new DamageTakenEvent(ActorId, attackerId, amount, Current));

            if (Current <= 0)
                EventBus.Instance.Publish(new EntityDiedEvent(ActorId));
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            int before = Current;
            Current = Mathf.Min(maxHealth, Current + amount);

            // Solo se avisa si la vida cambio DE VERDAD: curar a alguien que
            // ya esta lleno no es un evento, y publicarlo igual encenderia
            // las cincuenta barras de vida a la vez sin que pasara nada.
            if (Current == before) return;
            EventBus.Instance.Publish(new HealedEvent(ActorId, Current - before, Current));
        }
    }
}
