using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

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

        // REANIMAR: el medico revive a un aliado caido (muerto) parado junto a el unos segundos.
        public const float SegundosDeReanimar = 4f;
        public static bool Reanimando { get; private set; }
        static Soldier medicoPasivo;
        public static float ProgresoDeReanimar => Reanimando ? Mathf.Clamp01(acumulado / SegundosDeReanimar) : 0f;

        public static Soldier MedicoDisponible(Soldier excluir = null)
        {
            Soldier mejor = null;
            foreach (var a in ActorRegistry.All)
            {
                if (a == null || a == excluir || a.Team != TeamId.Player || a.Role != RoleType.Medic) continue;
                if (a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                mejor = a; break;
            }
            return mejor;
        }

        // medicoManual: el medico es el propio jugador (tiene que acercarse el mismo).
        public static bool SolicitarReanimar(Soldier caido, Soldier medicoManual = null)
        {
            Cancelar();
            if (caido == null || caido.Health == null || caido.Health.IsAlive || caido.Team != TeamId.Player) return false;
            var medico = medicoManual != null && medicoManual.Health != null && medicoManual.Health.IsAlive
                ? medicoManual : MedicoDisponible(caido);
            if (medico == null || medico == caido) return false;
            if (medico != medicoManual && OrderService.LoManejaElJugador(medico)) return false;

            Herido = caido;
            Enfermero = medico;
            Reanimando = true;
            restante = EsperaMaxima;
            acumulado = 0f;
            // Un caido no se "sigue" (el seguimiento se cae al morir el objetivo): se va al lugar donde yace.
            if (medico != medicoManual)
            {
                OrderService.IssueMoveOrder(medico, caido.transform.position + (medico.transform.position - caido.transform.position).normalized * 1.2f);
                // Mientras reanima no se distrae peleando: si no, se va y el caido queda sin levantar.
                if (medico.Brain != null) { medico.Brain.Pasivo = true; medicoPasivo = medico; }
            }
            GameLog.Line($"{medico.DisplayName} va a reanimar a {caido.DisplayName}");
            return true;
        }

        static float restante;
        static float acumulado;
        // Feedback: el medico ya llego y esta atendiendo (para sonar UNA vez al empezar) y cuando se
        // mostro el ultimo "+N" flotante.
        static bool atendiendo;
        static float proximoNumero;

        static void EmpezarAtencion(Vector3 donde, string texto)
        {
            if (atendiendo) return;
            atendiendo = true;
            proximoNumero = 0f;
            Feedback.Accion(SfxKind.HealStart, texto, donde, Feedback.Ok, aviso: false, pulso: true, volumen: 0.7f);
        }

        // Devuelve false (y no deja pedido abierto) si no hay a quien
        // mandar. El llamador usa eso para avisar por pantalla.
        // medicoManual: el medico es el propio jugador (no se le ordena caminar: tiene que
        // acercarse el mismo a AlcanceDeCuracion del herido).
        public static bool Solicitar(Soldier herido, Soldier medicoManual = null)
        {
            Cancelar();
            if (herido == null || herido.Health == null || !herido.Health.IsAlive) return false;
            if (herido.Health.Current >= herido.Health.MaxHealth) return false;

            var medico = medicoManual != null && medicoManual.Health != null && medicoManual.Health.IsAlive
                ? medicoManual : BuscarEnfermero(herido);
            if (medico == null || medico == herido) return false;

            Herido = herido;
            Enfermero = medico;
            restante = EsperaMaxima;
            acumulado = 0f;
            if (medico != medicoManual) OrderService.IssueFollowOrder(medico, herido);
            GameLog.Line($"{medico.DisplayName} va a atender a {herido.DisplayName}");
            return true;
        }

        // BOTIQUIN del medico que maneja el jugador: se cura solo BotiquinVida puntos en
        // BotiquinSegundos, y tarda BotiquinEspera s en volver a estar listo.
        public const int BotiquinVida = 60;
        public const float BotiquinSegundos = 4f;
        public const float BotiquinEspera = 25f;
        public static Soldier BotiquinDe { get; private set; }
        public static float BotiquinListoEn { get; private set; }
        static float botiquinRestante, botiquinAcum;

        public static bool Botiquin(Soldier medico)
        {
            if (medico == null || medico.Health == null || !medico.Health.IsAlive) return false;
            if (medico.Health.Current >= medico.Health.MaxHealth || BotiquinListoEn > 0f || botiquinRestante > 0f) return false;
            BotiquinDe = medico;
            botiquinRestante = BotiquinSegundos;
            botiquinAcum = 0f;
            BotiquinListoEn = BotiquinEspera;
            GameLog.Line($"{medico.DisplayName} usa el botiquin");
            Feedback.Accion(SfxKind.HealStart, "BOTIQUIN", medico.transform.position, Feedback.Ok, aviso: false, pulso: true, volumen: 0.7f);
            return true;
        }

        public static bool BotiquinActivo => botiquinRestante > 0f;

        static void TickBotiquin(float dt)
        {
            if (BotiquinListoEn > 0f) BotiquinListoEn = Mathf.Max(0f, BotiquinListoEn - dt);
            if (botiquinRestante <= 0f) return;
            if (BotiquinDe == null || BotiquinDe.Health == null || !BotiquinDe.Health.IsAlive) { botiquinRestante = 0f; return; }
            botiquinRestante -= dt;
            if (botiquinRestante <= 0f)
                Feedback.Accion(SfxKind.HealDone, "¡BOTIQUIN LISTO!", BotiquinDe.transform.position, Feedback.Ok, aviso: false, pulso: true, volumen: 0.7f);
            botiquinAcum += BotiquinVida / BotiquinSegundos * dt;
            int puntos = Mathf.FloorToInt(botiquinAcum);
            if (puntos <= 0) return;
            botiquinAcum -= puntos;
            BotiquinDe.Health.Heal(puntos);
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
                s => s != herido && s.Team == herido.Team && s.Health != null && s.Health.IsAlive && s.Role != RoleType.Civilian
                     && !OrderService.LoManejaElJugador(s));
        }

        public static void Cancelar()
        {
            // El medico que iba a reanimar vuelve a pelear normal.
            if (medicoPasivo != null && medicoPasivo.Brain != null) medicoPasivo.Brain.Pasivo = false;
            medicoPasivo = null;
            Herido = null;
            Enfermero = null;
            Reanimando = false;
            restante = 0f;
            acumulado = 0f;
            atendiendo = false;
        }

        // El medico atiende SOLO a un aliado herido cercano (sin que el jugador lo
        // pida), siempre que no este peleando ni lo maneje el jugador. Reusa el
        // mismo pedido de arriba: va hasta el herido (orden de seguir) y lo cura
        // CuracionPorSegundo mientras este a AlcanceDeCuracion.
        public const float FraccionHerido = 0.75f;
        public const float RadioDeAtencionAutomatica = 16f;
        static float proximoEscaneo;

        // El tutorial la apaga para que el jugador mande a curar el mismo.
        public static bool AtencionAutomatica = true;

        static void AtenderSolo(float dt)
        {
            if (!AtencionAutomatica) return;
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
            TickBotiquin(dt);
            if (!Activo) { AtenderSolo(dt); return; }

            if (Reanimando)
            {
                if (Herido.Health.IsAlive || !Enfermero.Health.IsAlive) { Cancelar(); return; }
                restante -= dt;
                if (restante <= 0f) { Cancelar(); return; }
                if (Vector3.Distance(Herido.transform.position, Enfermero.transform.position) > AlcanceDeCuracion) return;
                EmpezarAtencion(Herido.transform.position, "REANIMANDO…");
                acumulado += dt;
                if (acumulado < SegundosDeReanimar) return;
                var revivido = Herido;
                revivido.Health.Initialize(revivido.Id, revivido.Health.MaxHealth);
                revivido.Motor.ResetMotionState();
                GameLog.Line($"{Enfermero.DisplayName} reanimo a {revivido.DisplayName}");
                Feedback.Accion(SfxKind.Revive, "¡" + revivido.DisplayName.ToUpperInvariant() + " DE VUELTA!", revivido.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.9f);
                Cancelar();
                return;
            }

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
            EmpezarAtencion(Herido.transform.position, "CURANDO…");

            // Se acumula en float y se gasta en enteros: con dt de 1/60 y
            // 12 de vida por segundo, redondear cada frame daria 0 siempre
            // y no curaria nunca.
            acumulado += CuracionPorSegundo * dt;
            int puntos = Mathf.FloorToInt(acumulado);
            if (puntos <= 0) return;
            acumulado -= puntos;
            Herido.Health.Heal(puntos);

            // Numero verde flotante cada segundo mientras cura, y campanita al quedar sano.
            proximoNumero -= dt;
            if (proximoNumero <= 0f)
            {
                proximoNumero = 1f;
                Feedback.Visual("+" + CuracionPorSegundo, Herido.transform.position, Feedback.Ok, aviso: false, pulso: false);
            }
            if (Herido.Health.Current >= Herido.Health.MaxHealth)
                Feedback.Accion(SfxKind.HealDone, "¡" + Herido.DisplayName.ToUpperInvariant() + " CURADO!", Herido.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.8f);
        }
    }
}
