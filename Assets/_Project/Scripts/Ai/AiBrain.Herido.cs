using UnityEngine;
using UnityEngine.AI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;

namespace SP.Ai
{
    // Bug #087 ("un aliado herido se manda al muere"): antes un aliado con poca vida seguia cargando contra el enemigo (Herido solo
    // servia para disparar en marcha) y, si pedia curacion, PedidoDeCuracion lo plantaba al descubierto a esperar al medico.
    //
    // Ahora, un aliado de IA con menos del 40 % de vida que esta en combate (con blanco o bajo fuego) se REPLIEGA:
    //   - si hay un medico vivo a 45 m o menos que no este del lado del enemigo, camina hacia el (encuentro a 2 m) y le pide curacion;
    //   - si no, elige una cobertura que no lo acerque al blanco (la eleccion ponderada, con "sentido" herido = retroceder) y va ahi;
    //   - mientras camina va mirando al blanco y le devuelve rafagas (retrocede disparando); en la cobertura se queda agachado y pelea
    //     desde ahi; no persigue ni ataca a campo abierto hasta recuperarse (55 %) o hasta 4 s sin combate.
    // Respeta lo que el jugador fijo a proposito: ALTO, cobertura ordenada, orden de atacar o de ir, subir a un vehiculo, el "siganme" forzado.
    public partial class AiBrain
    {
        public const float VidaDeRepliegue = 0.40f;
        public const float VidaParaTerminarRepliegue = 0.55f;
        public const float VidaCritica = 0.25f;
        public const float RadioDelMedico = 45f;
        public const float DistanciaDeEncuentro = 2f;   // menos que PedidoDeCuracion.AlcanceDeCuracion (2,5): si el medico no llega (cobertura en el NavMesh) el herido cierra la distancia
        const float SegundosDeCalmaParaTerminar = 4f;
        const float RefrescoDeDestinoDeRepliegue = 1.5f;
        const float RadioDeCoberturaDeRepliegue = 15f;
        const float SegundosEntreAnunciosDeHerido = 15f;

        bool enRepliegue, yendoAlMedico, repliegueEsCobertura;
        Vector3 repliegueDestino;
        Collider repliegueDueno;
        float relojDestinoRepliegue, ultimaAmenazaT, proximoPedidoAlMedico, proximoAnuncioDeHerido;
        static float ultimoAnuncioDeHerido = -99f;

        public bool EnRepliegue => enRepliegue;
        public bool YendoAlMedico => enRepliegue && yendoAlMedico;
        public int Repliegues { get; private set; }
        public Vector3 DestinoDeRepliegue => repliegueDestino;

        // Esto solo aplica a aliados de IA en una situacion "libre" (sin una orden explicita del jugador en curso).
        bool PuedeReplegarse =>
            self != null && self.Team == TeamId.Player && self.Role != RoleType.Civilian
            && !Pasivo && !Quieto && !IsPossessedByPlayer && !MontadoEnVehiculo
            && !coberturaPorOrden && !orderIsAttack && mountTarget == null && !SiguiendoForzado
            && !(hasOrder && State == AiState.MovingToOrder);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarAnuncioDeHerido() { ultimoAnuncioDeHerido = -99f; }

        // Devuelve true si este tick lo uso el repliegue (el llamador no corre el switch de estados).
        bool TickHerido(float dt)
        {
            if (self.Team != TeamId.Player) return false;
            float v = Vida01;
            bool bajoFuego = Time.time - tiempoUltimoAtaque < 4f;
            bool enCombate = (target != null && (State == AiState.Chase || State == AiState.Attack)) || bajoFuego;

            if (!enRepliegue)
            {
                if (v >= VidaDeRepliegue || !enCombate || !PuedeReplegarse) return false;
                EmpezarRepliegue();
            }
            if (!PuedeReplegarse || v >= VidaParaTerminarRepliegue) { TerminarRepliegue(); return false; }
            if (enCombate) ultimaAmenazaT = Time.time;
            else if (Time.time - ultimaAmenazaT > SegundosDeCalmaParaTerminar) { TerminarRepliegue(); return false; }

            var amenaza = AmenazaParaElRepliegue();
            relojDestinoRepliegue -= dt;
            if (relojDestinoRepliegue <= 0f)
            {
                relojDestinoRepliegue = RefrescoDeDestinoDeRepliegue;
                ElegirDestinoDeRepliegue(amenaza);
            }

            // Ya cubierto y peleando desde ahi: la logica normal de Attack dispara con el ciclo oculto/asoma.
            if (enCobertura && State == AiState.Attack && target != null) return false;

            self.Weapon.Tick(dt);
            Vector3? mira = amenaza != null ? amenaza.transform.position : (Vector3?)null;
            var aDestino = repliegueDestino - self.transform.position; aDestino.y = 0f;
            float umbral = yendoAlMedico ? 0.4f : 0.6f;
            bool llego = aDestino.magnitude <= umbral;
            if (!llego)
            {
                self.Motor.SetCrouching(false);
                self.Motor.SetRunning(false);
                llego = MoverMirando(repliegueDestino, mira, umbral, dt);
                VigilarProgreso(repliegueDestino, umbral, dt);
            }
            if (llego && repliegueEsCobertura && !enCobertura)
            {
                // Llego a la cobertura elegida: la ocupa (agachado, con el ciclo oculto/asoma).
                coberturaPunto = repliegueDestino; coberturaDueno = repliegueDueno; coberturaPorOrden = false; yendoACobertura = false;
                EntrarEnCoberturaTactica();
            }
            if (llego)
            {
                // En su puesto (cobertura o al lado del medico): se queda mirando al enemigo y le tira desde ahi.
                if (!enCobertura) self.Motor.SetCrouching(true);
                if (mira.HasValue) self.Motor.LookTowards(mira.Value, dt);
                if (enCobertura && amenaza != null && State != AiState.Attack
                    && Vector3.Distance(self.transform.position, amenaza.transform.position) <= EffectiveAttackRange && TieneLineaDeTiro(amenaza))
                {
                    target = amenaza;
                    SetState(AiState.Attack);
                }
            }
            DispararDesdeElRepliegue(amenaza, dt);
            return true;
        }

        void EmpezarRepliegue()
        {
            enRepliegue = true;
            Repliegues++;
            relojDestinoRepliegue = 0f;
            ultimaAmenazaT = Time.time;
            repliegueEsCobertura = false;
            yendoAlMedico = false;
            repliegueDestino = self.transform.position;
            if (Time.time >= proximoAnuncioDeHerido && Time.unscaledTime - ultimoAnuncioDeHerido > 3f)
            {
                proximoAnuncioDeHerido = Time.time + SegundosEntreAnunciosDeHerido;
                ultimoAnuncioDeHerido = Time.unscaledTime;
                Feedback.Accion(SfxKind.Wounded, $"{self.DisplayName.ToUpperInvariant()}: ¡ESTOY HERIDO, ME REPLIEGO!", self.transform.position,
                    Feedback.Warn, aviso: true, pulso: false, volumen: 0.5f);
            }
            GameLog.Line($"{self.DisplayName} esta herido ({self.Health.Current}/{self.Health.MaxHealth}): se repliega");
        }

        void TerminarRepliegue()
        {
            enRepliegue = false;
            yendoAlMedico = false;
            repliegueEsCobertura = false;
        }

        Soldier AmenazaParaElRepliegue()
        {
            if (target != null && target.Health != null && target.Health.IsAlive && target.gameObject.activeInHierarchy) return target;
            var a = ActorRegistry.FindById(ultimoAtacanteId);
            if (a != null && a.Health != null && a.Health.IsAlive && a.gameObject.activeInHierarchy && a.Team != self.Team) return a;
            return ActorRegistry.FindNearestEnemyInRange(self.transform.position, self.Team, EffectiveVisionRange * 1.3f);
        }

        // Cada 1,5 s: al medico (si hay y no esta del lado del enemigo) o a la mejor cobertura que no acerque al blanco.
        void ElegirDestinoDeRepliegue(Soldier amenaza)
        {
            yendoAlMedico = false;
            repliegueEsCobertura = false;
            var yo = self.transform.position;

            var medico = self.Role != RoleType.Medic ? PedidoDeCuracion.MedicoDisponible(self) : null;
            if (medico != null)
            {
                var haciaMedico = medico.transform.position - yo; haciaMedico.y = 0f;
                float dm = haciaMedico.magnitude;
                bool detras = amenaza == null || Vector3.Distance(medico.transform.position, amenaza.transform.position) > Vector3.Distance(yo, amenaza.transform.position) - 2f;
                if (dm <= RadioDelMedico && detras)
                {
                    yendoAlMedico = true;
                    if (dm <= DistanciaDeEncuentro) repliegueDestino = yo;
                    else
                    {
                        var punto = medico.transform.position - haciaMedico / dm * DistanciaDeEncuentro;
                        repliegueDestino = NavMesh.SamplePosition(punto, out var h, 3f, NavMesh.AllAreas) ? new Vector3(h.position.x, yo.y, h.position.z) : punto;
                    }
                    if (Time.time >= proximoPedidoAlMedico && PedidoDeCuracion.Herido != self)
                    {
                        proximoPedidoAlMedico = Time.time + 5f;
                        PedidoDeCuracion.Solicitar(self);
                    }
                    return;
                }
            }

            if (amenaza != null)
            {
                float dActual = Vector3.Distance(yo, amenaza.transform.position);
                // No se acerca mas al blanco (min = distancia actual - 1) y puede alejarse cuanto haga falta.
                if (CoberturasPuntuadas.TryElegir(yo, amenaza, self, RadioDeCoberturaDeRepliegue, EffectiveAttackRange, Vida01, Mathf.Max(1, VersionDeCobertura),
                        Recargando, enCobertura ? coberturaPunto : (Vector3?)null, out var el, float.MaxValue, Mathf.Max(0f, dActual - 1f)))
                {
                    repliegueDestino = el.punto; repliegueDueno = el.dueno; repliegueEsCobertura = true;
                    return;
                }
                // Sin cobertura a mano: se aleja del blanco unos 10 m.
                var alejarse = yo - amenaza.transform.position; alejarse.y = 0f;
                if (alejarse.sqrMagnitude < 0.01f) alejarse = -self.transform.forward;
                var dest = yo + alejarse.normalized * 10f;
                repliegueDestino = NavMesh.SamplePosition(dest, out var hh, 6f, NavMesh.AllAreas) ? new Vector3(hh.position.x, yo.y, hh.position.z) : yo + alejarse.normalized * 3f;
                return;
            }
            repliegueDestino = yo;
        }

        // Camina hacia el destino (por la ruta del NavMeshAgent si hay) mirando a otro lado: retrocede sin darle la espalda al blanco.
        // Devuelve true si ya esta a 'umbral' del destino.
        bool MoverMirando(Vector3 destino, Vector3? mira, float umbral, float dt)
        {
            var plano = destino - self.transform.position; plano.y = 0f;
            float dist = plano.magnitude;
            if (dist <= umbral) return true;
            Vector3 hacia = plano / dist;
            EnsureAgent();
            if (AgentActivo)
            {
                SincronizarAgente();
                if (!agentTieneDestino || (agentDestino - destino).sqrMagnitude > 0.25f)
                {
                    if (agent.SetDestination(destino)) { agentDestino = destino; agentTieneDestino = true; }
                    else agentTieneDestino = false;
                }
                if (agentTieneDestino && agent.hasPath && !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathInvalid)
                {
                    var d = agent.steeringTarget - self.transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f) hacia = d.normalized;
                }
            }
            float paso = Mathf.Max(self.Motor.MoveSpeed * dt, 0.0001f);
            self.Motor.Move(hacia * Mathf.Min(1f, dist / paso), dt);
            self.Motor.LookTowards(mira.HasValue ? mira.Value : self.transform.position + hacia, dt);
            return false;
        }

        // Rafagas hacia el blanco mientras se repliega (mismo gate que Attack: linea de tiro, apuntado y alcance; oculto no dispara).
        void DispararDesdeElRepliegue(Soldier amenaza, float dt)
        {
            if (amenaza == null || !StanceAllowsFire) return;
            if (enCobertura && subStateCobertura == CoverSubState.Hidden) return;
            if (!TieneLineaDeTiro(amenaza)) return;
            var plano = amenaza.transform.position - self.transform.position; plano.y = 0f;
            if (plano.magnitude > EffectiveAttackRange * HisteresisDeAlcance) return;
            if (plano.sqrMagnitude > 0.0001f && Vector3.Angle(self.transform.forward, plano) > aimToleranceDeg) return;
            var muzzle = self.Weapon.Muzzle;
            Vector3 origen = muzzle != null ? muzzle.position : self.transform.position;
            Vector3 dir = amenaza.transform.position - origen;
            if (dir.sqrMagnitude < 0.0001f) return;
            DispararConRafagas(dir.normalized, dt);
        }
    }
}
