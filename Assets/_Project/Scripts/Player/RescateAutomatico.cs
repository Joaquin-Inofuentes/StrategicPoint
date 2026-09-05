using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Player
{
    // A5: "un aliado libre va a revivirte y frena el timer". Mismo patron
    // que PedidoDeCuracion (curarse por pedido del jugador), pero este lo
    // dispara el juego solo cuando MUERE EL JUGADOR, no un pedido de menu.
    //
    // Estatico y con UN solo rescate a la vez a proposito: el jugador es
    // uno, no hace falta cola. El Tick vive en WorldSimulationDriver.Step,
    // el unico camino de simulacion que corren por igual el juego y la
    // suite -- un Update propio quedaria afuera del arnes.
    public static class RescateAutomatico
    {
        public const float AlcanceDeRevivir = 2f;
        // Mismo tiempo que PlayerInputDriver.TiempoDeRevivir (A4): revivir
        // tarda lo mismo lo pida el jugador con [E] o lo haga la IA sola.
        public const float TiempoDeCanal = 5f;
        // El rescatista no puede tener un enemigo mas cerca que esto -- "un
        // aliado SIN ENEMIGOS CERCA" es la condicion que pide la tarea.
        public const float RadioDeSeguridad = 15f;
        // Si en medio minuto no llego (atascado contra algo), el rescate se
        // cae solo: sin esto un rescatista trabado deja el timer de A3
        // congelado para siempre.
        public const float EsperaMaxima = 30f;

        public static Soldier Caido { get; private set; }
        public static Soldier Rescatista { get; private set; }
        public static bool Activo => Caido != null && Rescatista != null;

        static float restante;
        static float canalizado;

        // Se llama al morir el jugador (OnEntityDied). Devuelve false (sin
        // dejar nada pendiente) si no hay nadie libre para mandar.
        public static bool Solicitar(Soldier caido)
        {
            Cancelar();
            if (caido == null || caido.Health == null || caido.Health.IsAlive) return false;

            var rescatista = BuscarRescatistaLibre(caido);
            if (rescatista == null) return false;

            Caido = caido;
            Rescatista = rescatista;
            restante = EsperaMaxima;
            canalizado = 0f;
            OrderService.IssueFollowOrder(rescatista, caido);
            GameLog.Line($"{rescatista.DisplayName} va a revivir a {caido.DisplayName}");
            return true;
        }

        // El aliado vivo mas cercano al caido que NO tenga un enemigo
        // dentro de RadioDeSeguridad -- uno que ya esta peleando no
        // abandona el combate para venir a revivir.
        static Soldier BuscarRescatistaLibre(Soldier caido)
        {
            return ActorRegistry.FindNearest(caido.transform.position, s =>
                s != caido && s.Team == caido.Team && s.Health != null && s.Health.IsAlive
                && !OrderService.LoManejaElJugador(s)
                && ActorRegistry.FindNearestEnemyInRange(s.transform.position, s.Team, RadioDeSeguridad) == null);
        }

        public static void Cancelar()
        {
            Caido = null;
            Rescatista = null;
            restante = 0f;
            canalizado = 0f;
        }

        public static void Tick(float dt)
        {
            if (!Activo) return;

            // Ya lo revivieron por otro lado (por ejemplo, [Espacio] lo
            // cambio de soldado y el nuevo poseido lo revivio con [E] de A4)
            // o el propio rescatista murio en el camino.
            if (Caido.Health.IsAlive || !Rescatista.Health.IsAlive)
            {
                Cancelar();
                return;
            }

            restante -= dt;
            if (restante <= 0f) { Cancelar(); return; }

            float d = Vector3.Distance(Caido.transform.position, Rescatista.transform.position);
            if (d > AlcanceDeRevivir) { canalizado = 0f; return; } // todavia caminando

            canalizado += dt;
            if (canalizado < TiempoDeCanal) return;

            Caido.Health.Initialize(Caido.Id, Caido.Health.MaxHealth);
            GameLog.Line($"{Rescatista.DisplayName} revivio a {Caido.DisplayName}");
            Cancelar();
        }
    }
}
