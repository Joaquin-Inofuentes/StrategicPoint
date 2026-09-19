using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Player
{
    // "Necesito curarme" del menu de ordenes ([Q] sostenido).
    //
    // De las cinco ordenes del menu, cuatro ya existian en OrderService
    // (linea, cuña, seguirme, alto). Esta es la unica que no tenia nada
    // detras: RoleType.Medic estaba declarado y no lo miraba nadie.
    //
    // Es estatico y con UN solo pedido vivo a la vez a proposito. El
    // pedido lo hace el jugador desde su menu, y el jugador es uno: una
    // cola de pedidos seria maquinaria para un caso que no existe.
    //
    // El Tick vive en WorldSimulationDriver.Step, que es el unico camino
    // de simulacion que corren por igual el juego y la suite. Ponerlo en
    // un Update propio lo dejaria afuera del arnes.
    public static class PedidoDeCuracion
    {
        // A que distancia el enfermero puede atender. Es algo mas que el
        // radio de llegada de la orden de seguir para que no se quede
        // oscilando un paso afuera sin curar nunca.
        public const float AlcanceDeCuracion = 2.5f;
        public const int CuracionPorSegundo = 12;
        // Si en medio minuto no llego, el pedido se cae solo: sin esto un
        // enfermero trabado detras de un muro deja al herido esperando
        // para siempre y bloquea cualquier pedido posterior.
        public const float EsperaMaxima = 30f;

        public static Soldier Herido { get; private set; }
        public static Soldier Enfermero { get; private set; }
        public static bool Activo => Herido != null && Enfermero != null;

        static float restante;
        static float acumulado;

        // Devuelve false (y no deja pedido abierto) si no hay a quien
        // mandar. El llamador usa eso para avisar por pantalla.
        public static bool Solicitar(Soldier herido)
        {
            Cancelar();
            if (herido == null || herido.Health == null || !herido.Health.IsAlive) return false;
            if (herido.Health.Current >= herido.Health.MaxHealth) return false;

            var medico = BuscarEnfermero(herido);
            if (medico == null) return false;

            Herido = herido;
            Enfermero = medico;
            restante = EsperaMaxima;
            acumulado = 0f;
            OrderService.IssueFollowOrder(medico, herido);
            GameLog.Line($"{medico.DisplayName} va a atender a {herido.DisplayName}");
            return true;
        }

        // El enfermero del equipo del herido mas cercano a el. Si no hay
        // nadie con el rol, cae al aliado vivo mas cercano: en una
        // escuadra de tres, negarse a mandar a alguien porque nadie tiene
        // el rol seria una orden que nunca hace nada.
        public static Soldier BuscarEnfermero(Soldier herido)
        {
            var conRol = ActorRegistry.FindNearest(herido.transform.position,
                s => s != herido && s.Team == herido.Team && s.Health != null && s.Health.IsAlive
                     && s.Role == RoleType.Medic && !OrderService.LoManejaElJugador(s));
            if (conRol != null) return conRol;

            return ActorRegistry.FindNearest(herido.transform.position,
                s => s != herido && s.Team == herido.Team && s.Health != null && s.Health.IsAlive
                     && !OrderService.LoManejaElJugador(s));
        }

        public static void Cancelar()
        {
            Herido = null;
            Enfermero = null;
            restante = 0f;
            acumulado = 0f;
        }

        // El medico atiende SOLO a un aliado herido cercano (sin que el jugador lo
        // pida), siempre que no este peleando ni lo maneje el jugador. Reusa el
        // mismo pedido de arriba: va hasta el herido (orden de seguir) y lo cura
        // CuracionPorSegundo mientras este a AlcanceDeCuracion.
        public const float FraccionHerido = 0.75f;
        public const float RadioDeAtencionAutomatica = 16f;
        static float proximoEscaneo;

        static void AtenderSolo(float dt)
        {
            proximoEscaneo -= dt;
            if (proximoEscaneo > 0f) return;
            proximoEscaneo = 1.5f;

            foreach (var medico in ActorRegistry.All)
            {
                if (medico == null || medico.Role != RoleType.Medic || medico.Team != TeamId.Player) continue;
                if (medico.Health == null || !medico.Health.IsAlive || OrderService.LoManejaElJugador(medico)) continue;
                if (medico.Brain != null && medico.Brain.CurrentTarget != null) continue;

                Soldier peor = null; float peorFrac = FraccionHerido;
                foreach (var a in ActorRegistry.All)
                {
                    if (a == null || a == medico || a.Team != medico.Team || a.Health == null || !a.Health.IsAlive) continue;
                    float f = (float)a.Health.Current / Mathf.Max(1, a.Health.MaxHealth);
                    if (f >= peorFrac) continue;
                    if (Vector3.Distance(a.transform.position, medico.transform.position) > RadioDeAtencionAutomatica) continue;
                    peor = a; peorFrac = f;
                }
                if (peor == null) continue;

                Cancelar();
                Herido = peor; Enfermero = medico; restante = EsperaMaxima; acumulado = 0f;
                OrderService.IssueFollowOrder(medico, peor);
                GameLog.Line($"{medico.DisplayName} atiende solo a {peor.DisplayName} ({peor.Health.Current}/{peor.Health.MaxHealth})");
                return;
            }
        }

        public static void Tick(float dt)
        {
            if (!Activo) { AtenderSolo(dt); return; }

            if (!Herido.Health.IsAlive || !Enfermero.Health.IsAlive
                || Herido.Health.Current >= Herido.Health.MaxHealth)
            {
                Cancelar();
                return;
            }

            restante -= dt;
            if (restante <= 0f) { Cancelar(); return; }

            float d = Vector3.Distance(Herido.transform.position, Enfermero.transform.position);
            if (d > AlcanceDeCuracion) return;

            // Se acumula en float y se gasta en enteros: con dt de 1/60 y
            // 12 de vida por segundo, redondear cada frame daria 0 siempre
            // y no curaria nunca.
            acumulado += CuracionPorSegundo * dt;
            int puntos = Mathf.FloorToInt(acumulado);
            if (puntos <= 0) return;
            acumulado -= puntos;
            Herido.Health.Heal(puntos);
        }
    }
}
