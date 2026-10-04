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
        // Bug #8: 12 por segundo sobre 486 de vida eran 40 s de curacion (y el jugador seguia recibiendo tiros): no se notaba.
        // Ahora se cura el 9% de la vida maxima por segundo (44/s para 486), nunca menos de CuracionPorSegundo.
        public static int CuracionPorSegundoDe(Soldier herido) =>
            herido != null && herido.Health != null ? Mathf.Max(CuracionPorSegundo, Mathf.RoundToInt(herido.Health.MaxHealth * 0.09f)) : CuracionPorSegundo;

        // El pedido emite sus propias ordenes (seguir al herido, ir hasta el caido): esas NO deben soltar al enfermero.
        static bool emitiendoOrden;

        // Llamado por AiBrain al recibir CUALQUIER orden: si el que la recibe es el enfermero del pedido en curso y la orden no la
        // dio el pedido mismo, el pedido se cae (y el siguiente de la cola se atiende enseguida).
        // Validacion (bug #8): la orden SEGUIR soltaba al medico, pero en el mismo instante la atencion automatica lo volvia a tomar.
        // Despues de una orden del jugador ese soldado queda fuera de la atencion automatica unos segundos: se respeta la orden.
        public const float SegundosRespetandoOrden = 8f;
        static readonly System.Collections.Generic.Dictionary<Soldier, float> ordenManualHasta = new System.Collections.Generic.Dictionary<Soldier, float>();
        public static bool RespetandoOrden(Soldier s) => s != null && ordenManualHasta.TryGetValue(s, out var t) && Time.time < t;

        public static void SoltarSiEsElEnfermero(Soldier s)
        {
            if (emitiendoOrden || s == null) return;
            if (s.Role == RoleType.Medic) ordenManualHasta[s] = Time.time + SegundosRespetandoOrden;
            if (Enfermero != s) return;
            GameLog.Line($"{s.DisplayName} recibio otra orden: deja el pedido de {(Herido != null ? Herido.DisplayName : "-")}");
            Cancelar();
        }

        static void OrdenarSeguir(Soldier medico, Soldier herido, float forzar)
        {
            emitiendoOrden = true;
            try { OrderService.IssueFollowOrder(medico, herido, default, forzar, silencioso: true); }
            finally { emitiendoOrden = false; }
            // Que llegue hasta el alcance de curacion (el seguimiento comun frena varios metros antes).
            if (medico != null && medico.Brain != null) medico.Brain.UmbralDeSeguimientoForzado = AlcanceDeCuracion * 0.5f;
        }

        // Un solo aviso al salir hacia el herido (la orden de seguir del pedido es silenciosa).
        static void AnunciarIda(Soldier medico, Soldier herido)
        {
            if (medico == null || herido == null) return;
            Feedback.Accion(SfxKind.FollowCall, $"{medico.DisplayName.ToUpperInvariant()} VA A CURAR A {herido.DisplayName.ToUpperInvariant()}",
                medico.transform.position, Feedback.Ok, aviso: false, pulso: true, volumen: 0.35f);
        }

        static void OrdenarIr(Soldier medico, Vector3 punto)
        {
            emitiendoOrden = true;
            try { OrderService.IssueMoveOrder(medico, punto); }
            finally { emitiendoOrden = false; }
        }

        // El soldado del jugador esta siendo curado AHORA (el medico llego y atiende): PlayerInputDriver lo frena y muestra el aviso.
        public static bool CurandoAlJugador(Soldier poseido) => Activo && !Reanimando && atendiendo && Herido == poseido && poseido != null;
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
        // Sin medico vivo, al JUGADOR caido lo levanta el aliado libre mas cercano (bug #12: "me mori y no vinieron a rescatarme").
        public static Soldier RescatistaCercano(Soldier caido) => caido == null ? null : ActorRegistry.FindNearest(caido.transform.position,
            s => s != caido && s.Team == caido.Team && s.Role != RoleType.Civilian && s.Health != null && s.Health.IsAlive
                 && s.gameObject.activeInHierarchy && !OrderService.LoManejaElJugador(s) && !(s.Brain != null && s.Brain.MontadoEnVehiculo));

        public static bool SolicitarReanimar(Soldier caido, Soldier medicoManual = null)
        {
            Cancelar();
            if (caido == null || caido.Health == null || caido.Health.IsAlive || caido.Team != TeamId.Player) return false;
            var poseido = PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;
            var medico = medicoManual != null && medicoManual.Health != null && medicoManual.Health.IsAlive
                ? medicoManual : (MedicoDisponible(caido) ?? (caido == poseido ? RescatistaCercano(caido) : null));
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
                OrdenarIr(medico, caido.transform.position + (medico.transform.position - caido.transform.position).normalized * 1.2f);
                // Mientras reanima no se distrae peleando: si no, se va y el caido queda sin levantar.
                if (medico.Brain != null) { medico.Brain.Pasivo = true; medicoPasivo = medico; }
            }
            GameLog.Line($"{medico.DisplayName} va a reanimar a {caido.DisplayName}");
            SesionLog.Orden("REANIMAR", medico, "a " + caido.DisplayName);
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
        // Pedido explicito: "el medico siempre debe responder a esas alertas". Antes un pedido nuevo PISABA al que estaba atendiendo
        // y el herido anterior quedaba sin curar. Ahora, si el medico (de IA) ya esta con otro herido, el nuevo se encola y se atiende
        // apenas termine.
        static readonly System.Collections.Generic.List<Soldier> cola = new System.Collections.Generic.List<Soldier>();
        public static int EnCola => cola.Count;
        public static void LimpiarCola() => cola.Clear();

        static void AtenderSiguienteDeLaCola()
        {
            while (cola.Count > 0)
            {
                var h = cola[0];
                cola.RemoveAt(0);
                if (h == null || h.Health == null || !h.Health.IsAlive || h.Health.Current >= h.Health.MaxHealth) continue;
                if (Solicitar(h)) return;
            }
        }

        public static bool Solicitar(Soldier herido, Soldier medicoManual = null)
        {
            // Bug #8: el pedido del JUGADOR ("CURARME") se quedaba en la cola detras de otro herido. El jugador tiene prioridad:
            // el herido que se estaba atendiendo pasa a la cola y el medico viene enseguida.
            if (herido != null && herido == rechazado) rechazado = null;   // lo pidio el: se levanta el "no me cures"
            var poseido = PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;
            if (herido != null && herido == poseido && medicoManual == null && Activo && !Reanimando && Herido != herido
                && Herido != null && Herido.Health != null && Herido.Health.IsAlive && !cola.Contains(Herido))
                cola.Insert(0, Herido);
            else if (herido != null && herido.Health != null && herido.Health.IsAlive && herido.Health.Current < herido.Health.MaxHealth
                && medicoManual == null && Activo && !Reanimando && Herido != herido && Enfermero != null && Enfermero.Health != null && Enfermero.Health.IsAlive)
            {
                if (!cola.Contains(herido)) cola.Add(herido);
                GameLog.Line($"{Enfermero.DisplayName} anota el pedido de {herido.DisplayName} (en cola: {cola.Count})");
                return true;
            }
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
            // El medico acude aunque este peleando: durante unos segundos ignora el combate (ver AiBrain.IssueFollowOrder).
            if (medico != medicoManual) { OrdenarSeguir(medico, herido, SegundosDeAtencionUrgente); AnunciarIda(medico, herido); }
            GameLog.Line($"{medico.DisplayName} va a atender a {herido.DisplayName}");
            SesionLog.Orden("CURAR", medico, $"a {herido.DisplayName} ({herido.Health.Current}/{herido.Health.MaxHealth})");
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
            AccionesEnCurso.Reportar(BotiquinDe, "USANDO BOTIQUIN", BotiquinDe.transform.position + BotiquinDe.transform.forward * 0.6f,
                1f - Mathf.Clamp01(botiquinRestante / BotiquinSegundos), Mathf.Max(0f, botiquinRestante));   // ronda 13 (puntos 2 y 3)
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

        // Bug #8 (validacion): "apreta ESPACIO para detenerlo" detenia la curacion, pero en el siguiente escaneo la atencion
        // automatica volvia a mandar al medico (el jugador seguia herido). Si el jugador la corta, ese herido queda fuera de la
        // atencion automatica un rato (salvo que lo vuelvan a herir o que la pida el mismo).
        public const float SegundosSinAtencionTrasCortar = 20f;
        static Soldier rechazado; static float rechazadoHasta; static int vidaAlRechazar;
        public static bool RechazoActivo(Soldier s) => s != null && s == rechazado && Time.time < rechazadoHasta && s.Health != null && s.Health.Current >= vidaAlRechazar;

        public static void CancelarPorElJugador()
        {
            var h = Herido;
            Cancelar();
            if (h == null) return;
            rechazado = h; rechazadoHasta = Time.time + SegundosSinAtencionTrasCortar; vidaAlRechazar = h.Health != null ? h.Health.Current : 0;
        }

        // Bug #039/#040: el herido de IA queda plantado mientras el medico se acerca y lo cura (al jugador lo frena PlayerInputDriver).
        static Soldier retenido;
        public static Soldier Retenido => retenido;

        static void Retener(Soldier s)
        {
            if (retenido == s) return;
            Soltar();
            if (s == null || s.Motor == null) return;
            retenido = s;
            s.Motor.Retenido = true;
            GameLog.Line($"{s.DisplayName} se queda quieto: lo estan curando");
        }

        static Soldier medicoRetenido;
        static float proximoReSeguir;

        static void Soltar()
        {
            if (retenido != null && retenido.Motor != null) retenido.Motor.Retenido = false;
            retenido = null;
            if (medicoRetenido != null && medicoRetenido.Motor != null) medicoRetenido.Motor.Retenido = false;
            medicoRetenido = null;
        }

        public static void Cancelar()
        {
            Soltar();
            if (Enfermero != null && Enfermero.Brain != null) Enfermero.Brain.UmbralDeSeguimientoForzado = -1f;
            if (Enfermero != null) CurandoAnimacion.Terminar(Enfermero);
            // El medico que iba a reanimar vuelve a pelear normal.
            if (medicoPasivo != null && medicoPasivo.Brain != null) medicoPasivo.Brain.Pasivo = false;
            medicoPasivo = null;
            Herido = null;
            Enfermero = null;
            Reanimando = false;
            reanimacionAutomatica = false;
            restante = 0f;
            acumulado = 0f;
            atendiendo = false;
        }

        // El medico atiende SOLO a un aliado herido cercano (sin que el jugador lo
        // pida), siempre que no este peleando ni lo maneje el jugador. Reusa el
        // mismo pedido de arriba: va hasta el herido (orden de seguir) y lo cura
        // CuracionPorSegundo mientras este a AlcanceDeCuracion.
        public const float FraccionHerido = 0.75f;
        public const float RadioDeAtencionAutomatica = 24f;
        // Un aliado por debajo de esta fraccion es URGENTE: el medico lo atiende aunque este peleando y desde mas lejos.
        public const float FraccionUrgente = 0.45f;
        public const float RadioUrgente = 45f;
        public const float SegundosDeAtencionUrgente = 3f;
        static float proximoEscaneo;

        // El tutorial la apaga para que el jugador mande a curar el mismo.
        public static bool AtencionAutomatica = true;

        // Ronda 13 (punto 8): en calma el medico revive por su cuenta a los aliados caidos (antes solo curaba heridos y la
        // escuadra solo revivia si caian TODOS). "Calma" = ningun enemigo a RadioDeCalma del medico ni del caido.
        // Tiene prioridad sobre curar heridos. El tutorial tambien la apaga con AtencionAutomatica.
        public static bool ReanimarEnCalma = true;
        public const float RadioDeCalma = 25f;
        public const float RadioParaReanimarEnCalma = 45f;
        static bool reanimacionAutomatica;
        public static bool ReanimacionEsAutomatica => Reanimando && reanimacionAutomatica;

        public static bool HayCalma(Vector3 punto) =>
            ActorRegistry.FindNearestEnemyInRange(punto, TeamId.Player, RadioDeCalma) == null;

        // Si el medico ya esta al lado del caido (lo mandaste ahi, bug #14) lo levanta aunque haya enemigos lejos.
        public const float RadioDeReanimarAlLado = 5f;

        static Soldier CaidoParaReanimar(Soldier medico)
        {
            Soldier mejor = null; float mejorD = RadioParaReanimarEnCalma;
            foreach (var a in ActorRegistry.All)
            {
                if (a == null || a == medico || a.Team != medico.Team || a.Role == RoleType.Civilian || a.Health == null || a.Health.IsAlive) continue;
                if (!a.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(a.transform.position, medico.transform.position);
                if (d >= mejorD || (d > RadioDeReanimarAlLado && !HayCalma(a.transform.position))) continue;
                mejor = a; mejorD = d;
            }
            return mejor;
        }

        // Bug #12: "me atacaron y mis soldados no vinieron a rescatarme; me mori y no vinieron, aun sin enemigos". Si el soldado
        // que maneja el jugador cae, el rescate tiene PRIORIDAD: el medico (o, sin medico, el aliado libre mas cercano) va a
        // levantarlo apenas no haya enemigos a RadioDePeligroDelRescate del cuerpo, sin esperar la "calma" total.
        public const float RadioDePeligroDelRescate = 9f;

        static bool RescatarAlJugador()
        {
            var yo = PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;
            if (yo == null || yo.Health == null || yo.Health.IsAlive || yo.Team != TeamId.Player || !yo.gameObject.activeInHierarchy) return false;
            if (ActorRegistry.FindNearestEnemyInRange(yo.transform.position, TeamId.Player, RadioDePeligroDelRescate) != null) return false;
            // RescateAutomatico (pedido al morir) ya tiene a alguien en camino y canalizando: no se le pisa la orden.
            if (RescateAutomatico.Activo && RescateAutomatico.Caido == yo) return false;
            if (!SolicitarReanimar(yo)) return false;
            GameLog.Line($"{Enfermero.DisplayName} va a RESCATAR al jugador ({yo.DisplayName})");
            SesionLog.Orden("RESCATAR AL JUGADOR", Enfermero, "a " + yo.DisplayName);
            return true;
        }

        static void AtenderSolo(float dt)
        {
            if (!AtencionAutomatica) return;
            proximoEscaneo -= dt;
            if (proximoEscaneo > 0f) return;
            proximoEscaneo = 0.75f;
            if (ReanimarEnCalma && RescatarAlJugador()) return;

            foreach (var medico in ActorRegistry.All)
            {
                if (medico == null || medico.Role != RoleType.Medic || medico.Team != TeamId.Player) continue;
                if (medico.Health == null || !medico.Health.IsAlive || OrderService.LoManejaElJugador(medico) || RespetandoOrden(medico)) continue;
                bool peleando = medico.Brain != null && medico.Brain.CurrentTarget != null;
                // El propio medico herido se cura solo con su botiquin (le toca a el, no hay quien lo atienda).
                if (medico.Health.Current < medico.Health.MaxHealth * 0.55f) Botiquin(medico);

                if (!peleando && ReanimarEnCalma)
                {
                    var caido = CaidoParaReanimar(medico);
                    if (caido != null && SolicitarReanimar(caido) && Enfermero == medico)
                    {
                        reanimacionAutomatica = true;
                        GameLog.Line($"{medico.DisplayName} reanima solo a {caido.DisplayName} (calma)");
                        return;
                    }
                }

                Soldier peor = null; float peorFrac = peleando ? FraccionUrgente : FraccionHerido;
                foreach (var a in ActorRegistry.All)
                {
                    if (a == null || a == medico || a.Team != medico.Team || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                    if (a.Role == RoleType.Civilian || RechazoActivo(a)) continue;
                    float f = (float)a.Health.Current / Mathf.Max(1, a.Health.MaxHealth);
                    if (f >= peorFrac) continue;
                    float dist = Vector3.Distance(a.transform.position, medico.transform.position);
                    if (dist > (f < FraccionUrgente ? RadioUrgente : RadioDeAtencionAutomatica)) continue;
                    peor = a; peorFrac = f;
                }
                if (peor == null) continue;

                Cancelar();
                Herido = peor; Enfermero = medico; restante = EsperaMaxima; acumulado = 0f;
                OrdenarSeguir(medico, peor, SegundosDeAtencionUrgente);
                AnunciarIda(medico, peor);
                GameLog.Line($"{medico.DisplayName} atiende solo a {peor.DisplayName} ({peor.Health.Current}/{peor.Health.MaxHealth})");
                SesionLog.Orden("CURAR (automatico)", medico, $"a {peor.DisplayName} ({peor.Health.Current}/{peor.Health.MaxHealth}){(peleando ? " · urgente, estaba peleando" : "")}");
                return;
            }
        }

        public static void Tick(float dt)
        {
            TickBotiquin(dt);
            if (!Activo)
            {
                if (cola.Count > 0) AtenderSiguienteDeLaCola();
                if (!Activo) AtenderSolo(dt);
                return;
            }

            if (Reanimando)
            {
                if (Herido.Health.IsAlive || !Enfermero.Health.IsAlive) { Cancelar(); return; }
                // Reanimacion automatica: si vuelve la accion se corta (el medico vuelve a pelear normal).
                if (reanimacionAutomatica && !HayCalma(Enfermero.transform.position)) { Cancelar(); return; }
                restante -= dt;
                if (restante <= 0f) { Cancelar(); return; }
                if (Vector3.Distance(Herido.transform.position, Enfermero.transform.position) > AlcanceDeCuracion)
                {
                    // Bug #14: si algo lo desvio (otra orden, un empujon) se le vuelve a mandar al cuerpo.
                    if (Enfermero.Brain != null && Enfermero.Brain.State != SP.Ai.AiState.MovingToOrder && !OrderService.LoManejaElJugador(Enfermero))
                        OrdenarIr(Enfermero, Herido.transform.position + (Enfermero.transform.position - Herido.transform.position).normalized * 1.2f);
                    return;
                }
                EmpezarAtencion(Herido.transform.position, "REANIMANDO…");
                CurandoAnimacion.Tick(Enfermero, Herido, dt);
                acumulado += dt;
                AccionesEnCurso.Reportar(Enfermero, "REVIVIENDO", Herido.transform.position, acumulado / SegundosDeReanimar, SegundosDeReanimar - acumulado, Herido.transform);   // ronda 13 (puntos 2 y 3)
                if (acumulado < SegundosDeReanimar) return;
                var revivido = Herido;
                Reanimacion.Ejecutar(revivido);   // ronda 13: camino unico
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
            // El herido de IA se planta apenas hay pedido (el poseido lo maneja el jugador: lo frena PlayerInputDriver). Medido:
            // si solo se plantaba con el medico cerca, el herido seguia cargando contra el enemigo y moria antes de que llegara.
            var poseidoAhora = PlayerInputDriver.Activo != null && PlayerInputDriver.Activo.Brain != null ? PlayerInputDriver.Activo.Brain.Current : null;
            if (Herido != poseidoAhora) Retener(Herido);
            else if (Herido == poseidoAhora) Soltar();
            // Histeresis: una vez atendiendo, recien se corta si se separan bastante (antes cada paso cruzaba el borde del
            // alcance y el "CURANDO…" con su sonido se repetia en bucle: bug #039).
            if (d > (atendiendo ? AlcanceDeCuracion + 1.5f : AlcanceDeCuracion))
            {
                // Lejos del herido no hay por que estar Pasivo (antes quedaba "pasivo" sin curar ni pelear).
                if (medicoPasivo == Enfermero && Enfermero.Brain != null) { Enfermero.Brain.Pasivo = false; medicoPasivo = null; }
                if (medicoRetenido != null && medicoRetenido.Motor != null) { medicoRetenido.Motor.Retenido = false; medicoRetenido = null; }
                atendiendo = false;
                // Si el medico perdio el seguimiento (el sensado lo mando a perseguir a un enemigo), se le vuelve a ordenar,
                // FORZADO (asi el sensado no lo saca de nuevo al frame siguiente) y como mucho una vez por segundo. Bug #039/#040:
                // se re-emitia sin forzar cada frame -> Follow/Chase alternando, el medico clavado a 7 m y "TE SIGUE" en bucle.
                proximoReSeguir -= dt;
                if (proximoReSeguir <= 0f && Enfermero.Brain != null && !OrderService.LoManejaElJugador(Enfermero)
                    && (Enfermero.Brain.FollowTarget != Herido || !Enfermero.Brain.SiguiendoForzado))
                {
                    proximoReSeguir = 1f;
                    OrdenarSeguir(Enfermero, Herido, SegundosDeAtencionUrgente);
                }
                return;
            }
            EmpezarAtencion(Herido.transform.position, "CURANDO…");
            // El seguimiento llevaba al medico hasta pararse ENCIMA del herido (medido: 0,0 m). Se abre a ~1 m y ahi se planta.
            if (Enfermero != poseidoAhora && Enfermero.Motor != null)
            {
                var abrir = Enfermero.transform.position - Herido.transform.position; abrir.y = 0f;
                float separacion = abrir.magnitude;
                if (separacion < 0.01f) abrir = -Herido.transform.right;
                Enfermero.Motor.Retenido = false;
                if (separacion < 0.95f) Enfermero.Motor.Move(abrir.normalized, dt);
                else { Enfermero.Motor.Retenido = true; medicoRetenido = Enfermero; }
            }
            CurandoAnimacion.Tick(Enfermero, Herido, dt);
            // Mientras cura no se distrae: si lo ataca no sale corriendo a pelear y deja al herido a medias.
            if (Enfermero.Brain != null && !Enfermero.Brain.Pasivo && !OrderService.LoManejaElJugador(Enfermero)) { Enfermero.Brain.Pasivo = true; medicoPasivo = Enfermero; }
            int porSegundo = CuracionPorSegundoDe(Herido);
            AccionesEnCurso.Reportar(Enfermero, "CURANDO", Herido.transform.position, (float)Herido.Health.Current / Mathf.Max(1, Herido.Health.MaxHealth),
                (Herido.Health.MaxHealth - Herido.Health.Current) / (float)porSegundo, Herido.transform);   // ronda 13 (puntos 2 y 3)

            // Se acumula en float y se gasta en enteros: con dt de 1/60 y
            // 12 de vida por segundo, redondear cada frame daria 0 siempre
            // y no curaria nunca.
            acumulado += porSegundo * dt;
            int puntos = Mathf.FloorToInt(acumulado);
            if (puntos <= 0) return;
            acumulado -= puntos;
            Herido.Health.Heal(puntos);

            // Numero verde flotante cada segundo mientras cura, y campanita al quedar sano.
            proximoNumero -= dt;
            if (proximoNumero <= 0f)
            {
                proximoNumero = 1f;
                Feedback.Visual("+" + porSegundo, Herido.transform.position, Feedback.Ok, aviso: false, pulso: false);
            }
            if (Herido.Health.Current >= Herido.Health.MaxHealth)
                Feedback.Accion(SfxKind.HealDone, "¡" + Herido.DisplayName.ToUpperInvariant() + " CURADO!", Herido.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.8f);
        }
    }
}
