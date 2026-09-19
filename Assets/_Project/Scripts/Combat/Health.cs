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

        // Pedido explicito del usuario: "pasado 3 segundos despues de
        // accion se empieza a curar automaticamente". "Accion" = el ultimo
        // golpe recibido -- el mismo gatillo que ya usa LastAttackerId.
        // Curarse a full de un tiro no es el objetivo (eso ya lo hace
        // Heal() para revivir o para un botiquin): esto es una regeneracion
        // LENTA que premia salir del tiroteo, no un boton de vida infinita
        // en medio del combate.
        //
        // En SEGUNDOS de dt acumulado, no Time.time: los mismos timers de
        // WeaponHolder (cooldownTimer, reloadTimer) usan este patron para
        // poder simularse a mano en la suite headless (SimStep/SimulateSeconds),
        // que corre en Edit mode con Time.time congelado.
        const float SegundosSinDanoParaRegenerar = 3f;
        const float VidaPorSegundoRegenerando = 12f;

        // Arranca ya "afuera de combate": un soldado recien creado o recien
        // revivido no tiene por que esperar 3 s de gracia si por algun
        // motivo nace sin la vida llena.
        float segundosSinDano = SegundosSinDanoParaRegenerar;
        float regenAcumulada;

        // Verificable desde afuera (tests, UI) sin depender de leer el
        // Current dos frames seguidos para inferirlo.
        public bool IsRegenerating { get; private set; }

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
            segundosSinDano = SegundosSinDanoParaRegenerar;
            regenAcumulada = 0f;
            IsRegenerating = false;
        }

        // Llamado una vez por frame desde WorldSimulationDriver.Step, igual
        // que WeaponHolder.Tick -- el mismo camino de simulacion que corre
        // por igual el juego real y la suite headless (SimStep).
        public void Tick(float dt)
        {
            if (!IsAlive) return;

            segundosSinDano += dt;

            if (Current >= maxHealth || segundosSinDano < SegundosSinDanoParaRegenerar)
            {
                IsRegenerating = false;
                regenAcumulada = 0f;
                return;
            }

            IsRegenerating = true;
            regenAcumulada += VidaPorSegundoRegenerando * dt;

            // Cura de a puntos enteros (Heal() ya descarta amount<=0, y un
            // HealedEvent por fraccion de punto no dice nada util). El
            // resto se guarda para el proximo tick en vez de perderse.
            int puntos = Mathf.FloorToInt(regenAcumulada);
            if (puntos <= 0) return;
            regenAcumulada -= puntos;
            Heal(puntos);

            // BUG REAL medido con la propia suite: si esta cura llega
            // justo al maximo, IsRegenerating se quedaba en true un tick
            // de mas -- el que curo, no el siguiente -- porque arriba se
            // pone en true ANTES de curar y nada lo baja despues en el
            // mismo Tick. SimulateUntil corta apenas ve el maximo, sin dar
            // un tick extra, y ahi quedaba en evidencia.
            if (Current >= maxHealth) IsRegenerating = false;
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

            // Dificultad: potenciadores de dano segun quien pega y a quien (solo partida principal).
            if (Dificultad.Activa) amount = Mathf.Max(1, Mathf.RoundToInt(amount * Dificultad.MultiplicadorDeDano(attackerId, this)));

            // Cualquier golpe de verdad reinicia la cuenta de "sin accion":
            // la regeneracion se corta del todo, no sigue de donde iba.
            segundosSinDano = 0f;
            regenAcumulada = 0f;
            IsRegenerating = false;

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
