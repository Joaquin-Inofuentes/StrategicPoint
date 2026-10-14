using System;
using UnityEngine;
using UnityEngine.AI;
using SP.Core;
using SP.Actors;
using SP.Vehicles;

namespace SP.Ai
{
    // AiBrain (parte): navegacion. Simple a proposito: el NavMeshAgent calcula la ruta y el cuerpo la sigue (SoldierMotor aplica el paso,
    // la colision y la animacion). Sin A* propio, sin vigilantes de atasco, sin teletransportes ni pasos laterales.
    public partial class AiBrain
    {
        // Hasta donde acercarse cuando NO hay linea de tiro: practicamente "hasta tocarlo".
        const float DistanciaDeContacto = 1.5f;

        // El destino se vuelve a pedir al agent solo si se corrio mas que esto (un pedido nuevo cada frame lo dejaba "calculando" para siempre).
        const float CorrimientoParaRepedir = 0.5f;
        // Sin avanzar esta distancia en esta ventana (con la ruta ya calculada) = no puede llegar: se da la orden por cumplida donde llego.
        const float VentanaSinAvance = 2.5f;
        const float AvanceMinimoEnVentana = 0.3f;
        float ventanaT, pendienteSinRuta;
        Vector3 ventanaAncla;

        public int RemainingPathPoints => AgentActivo && agent.hasPath ? 1 : 0;

        // Pide la ruta al agent. Si el agent no esta (sin NavMesh bajo el soldado) no hay ruta: se camina derecho.
        void PlanPathTo(Vector3 destination)
        {
            ResetStuckWatch();
            EnsureAgent();
            if (!AgentActivo) { agentTieneDestino = false; return; }
            SincronizarAgente();
            if (agentTieneDestino && agent.pathPending && (agentDestino - destination).sqrMagnitude < 9f) return;
            agentTieneDestino = agent.SetDestination(destination);
            if (agentTieneDestino) agentDestino = destination;
        }

        void ClearPath()
        {
            agentTieneDestino = false;
            if (AgentActivo) agent.ResetPath();
            ResetStuckWatch();
        }

        void ResetStuckWatch()
        {
            ventanaT = 0f;
            ventanaAncla = self != null ? self.transform.position : Vector3.zero;
        }

        // Los llamadores replanifican cada tanto (formacion, seguir, patrulla); el agent solo recalcula si el destino cambio de verdad.
        bool AvanzarConRodeo(Vector3 objetivo, float umbral, float dt, ref Vector3 destino, ref bool tieneDestino, ref float reloj)
        {
            destino = objetivo;
            tieneDestino = true;
            return AdvanceTo(objetivo, umbral, dt);
        }

        void RodearHasta(Vector3 objetivo, float dt) => AdvanceTo(objetivo, DistanciaDeContacto, dt);
        void SeguirHasta(Vector3 objetivo, float umbral, float dt) => AdvanceTo(objetivo, umbral, dt);

        Vector3 destinoRodeo, destinoSeguimiento, destinoPatrulla, destinoRegresoAPuesto;
        bool tieneRodeo, tieneSeguimiento, tienePatrulla, tieneRegresoAPuesto;
        float relojDeRodeo, relojDeSeguimiento, relojDePatrulla, relojDeRegresoAPuesto;

        // --- Cobertura ---
        public const float RadioDeBusquedaDeCobertura = 25f;
        const float RefrescoDeCobertura = 0.5f;
        const float DistanciaEnLaCobertura = 0.6f;
        Vector3 coberturaElegida;
        bool tieneCobertura;
        float relojDeCobertura;

        public bool VaHaciaUnaCobertura => tieneCobertura;
        public Vector3 CoberturaElegida => coberturaElegida;

        // Ir a la cobertura mas cercana DESDE LA QUE SE LE PUEDE DISPARAR al blanco. Devuelve false si no hay ninguna que sirva.
        bool CubrirseDe(Soldier objetivo, float dt, float radio = RadioDeBusquedaDeCobertura, float maxDistAlBlanco = float.MaxValue, float factorRango = 1f)
        {
            relojDeCobertura += dt;
            bool toca = !tieneCobertura || relojDeCobertura >= RefrescoDeCobertura;
            if (toca && !SP.Core.CoberturasPuntuadas.PuedeBuscar()) { if (!tieneCobertura) return false; toca = false; }   // presupuesto de busquedas por frame
            if (toca)
            {
                relojDeCobertura = 0f;
                Vector3 elegida = Vector3.zero;
                if (VersionDeCobertura >= 1)
                {
                    tieneCobertura = SP.Core.CoberturasPuntuadas.TryElegir(self.transform.position, objetivo, self, radio,
                        EffectiveAttackRange * factorRango, Vida01, VersionDeCobertura, Recargando, tieneCobertura ? coberturaElegida : (Vector3?)null, out var el, maxDistAlBlanco);
                    if (tieneCobertura) elegida = el.punto;
                }
                else
                    tieneCobertura = SP.Core.Coberturas.TryMejorCobertura(
                        self.transform.position, objetivo, self, RadioDeBusquedaDeCobertura,
                        EffectiveAttackRange * 0.85f, out elegida);
                if (!tieneCobertura) return false;
                coberturaElegida = elegida;
            }
            if (!tieneCobertura) return false;
            AdvanceTo(coberturaElegida, DistanciaEnLaCobertura, dt);
            return true;
        }

        void SoltarCobertura()
        {
            tieneCobertura = false;
            relojDeCobertura = 0f;
        }

        // ------------------------------------------------------------------
        // Blancos INALCANZABLES: un aliado no persigue para siempre a un enemigo del otro lado de algo cerrado.
        // ------------------------------------------------------------------
        readonly System.Collections.Generic.Dictionary<int, float> ignorados = new System.Collections.Generic.Dictionary<int, float>();
        float chaseSinLineaT, chaseDistRef;
        int chaseObjetivoId = -1;
        public int ObjetivosDescartados { get; private set; }

        public bool Ignorando(Soldier s)
        {
            if (s == null || ignorados.Count == 0) return false;
            if (!ignorados.TryGetValue(s.Id, out var hasta)) return false;
            if (Time.time < hasta) return true;
            ignorados.Remove(s.Id);
            return false;
        }

        float chaseUltimaLlamada = -99f;

        bool VigilarInalcanzable(float distanciaAlBlanco, float dt)
        {
            if (target == null || self.Team != SP.Combat.TeamId.Player) return false;
            if (Time.time - chaseUltimaLlamada > 1f) { chaseSinLineaT = 0f; chaseDistRef = distanciaAlBlanco; }
            chaseUltimaLlamada = Time.time;
            chaseSinLineaT += dt;
            if (distanciaAlBlanco < chaseDistRef - 2f) { chaseDistRef = distanciaAlBlanco; chaseSinLineaT = Mathf.Min(chaseSinLineaT, 4f); }
            bool parcial = AgentActivo && !agent.pathPending && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial;
            if (!((parcial && chaseSinLineaT > 1.5f) || chaseSinLineaT > 8f)) return false;

            string motivo = parcial ? "ruta parcial" : "sin acercarse";
            var vistos = target;
            float hasta = Time.time + 10f;
            int cantidad = 0;
            foreach (var s in SP.Core.ActorRegistry.All)
            {
                if (s == null || s.Team == self.Team || s.Health == null || !s.Health.IsAlive) continue;
                if (s != vistos && ((s.transform.position - vistos.transform.position).sqrMagnitude > 144f || TieneLineaDeTiro(s))) continue;
                ignorados[s.Id] = hasta;
                cantidad++;
            }
            ObjetivosDescartados += cantidad;
            GameLog.Line($"{self.DisplayName} descarta a {cantidad} enemigo(s) cerca de {vistos.DisplayName}: del otro lado de un obstaculo ({motivo}) · vuelve con el grupo");
            target = null;
            sensedTarget = null;
            chaseSinLineaT = 0f; chaseObjetivoId = -1;
            ClearPath();
            if (followTarget != null && followTarget.Health != null && followTarget.Health.IsAlive && followTarget.gameObject.activeInHierarchy)
            {
                SetState(AiState.Follow);
                return true;
            }
            var lider = AjustesDeEscuadra.Lider;
            if (lider == null || lider == self)
            {
                var drv = SP.Player.PlayerInputDriver.Activo;
                lider = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            }
            if (lider != null && lider != self && lider.Health != null && lider.Health.IsAlive && lider.gameObject.activeInHierarchy) ComenzarSeguirAlJugador(lider);
            else { hasOrder = false; SetState(AiState.Patrol); }
            return true;
        }

        // Solo para escenas SIN NavMesh (la suite headless): rodeo con la grilla de NavService. En el juego real siempre hay agent.
        readonly System.Collections.Generic.List<Vector3> rodeoSinNavMesh = new System.Collections.Generic.List<Vector3>();
        Vector3 rodeoDestino; int rodeoIndice; float rodeoReloj;
        bool AvanzarSinNavMesh(Vector3 destination, float threshold, float dt)
        {
            rodeoReloj += dt;
            if (rodeoReloj >= 0.5f || (rodeoDestino - destination).sqrMagnitude > 9f)
            {
                rodeoReloj = 0f; rodeoDestino = destination;
                rodeoIndice = SP.Core.NavService.TryFindDetour(self.transform.position, destination, rodeoSinNavMesh) ? 1 : 0;
            }
            if (rodeoSinNavMesh.Count > 0 && rodeoIndice > 0 && rodeoIndice < rodeoSinNavMesh.Count - 1)
            {
                if (self.Motor.MoveTowards(rodeoSinNavMesh[rodeoIndice], 1.2f, dt)) rodeoIndice++;
                return false;
            }
            return self.Motor.MoveTowards(destination, threshold, dt);
        }

        // ------------------------------------------------------------------
        // Avance
        // ------------------------------------------------------------------
        // Camina hacia el destino por la ruta del NavMeshAgent. Devuelve true al llegar (o cuando ya no puede acercarse mas).
        // girar=false: el cuerpo no se reorienta (attack-move: el torso ya apunta al blanco).
        bool AdvanceTo(Vector3 destination, float threshold, float dt, bool girar = true)
        {
            EnsureAgent();
            if (!AgentActivo) return AvanzarSinNavMesh(destination, threshold, dt);

            SincronizarAgente();

            float corrimiento = agentTieneDestino ? (agentDestino - destination).sqrMagnitude : float.MaxValue;
            if (!agentTieneDestino || corrimiento > CorrimientoParaRepedir * CorrimientoParaRepedir)
            {
                if (!(agentTieneDestino && agent.pathPending && corrimiento < 9f))
                {
                    if (!agent.SetDestination(destination)) { agentTieneDestino = false; return self.Motor.MoveTowards(destination, threshold, dt); }
                    agentDestino = destination;
                    agentTieneDestino = true;
                    ResetStuckWatch();
                }
            }

            // El NavMesh se rehizo (cayo un muro): la ruta vieja ya no vale.
            if (agentNavVersion != SP.Core.NavMeshViva.Version)
            {
                agentNavVersion = SP.Core.NavMeshViva.Version;
                agent.SetDestination(destination);
            }

            // Esperando la primera ruta: unos instantes quieto; si la cola del NavMesh tarda (muchos agentes, o una suite que simula sin frames
            // reales) se camina derecho al destino hasta que llegue la ruta.
            if (agent.pathPending)
            {
                pendienteSinRuta += dt;
                if (pendienteSinRuta > 0.4f) self.Motor.MoveTowards(destination, threshold, dt);
                return false;
            }
            pendienteSinRuta = 0f;
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                return self.Motor.MoveTowards(destination, threshold, dt);

            Vector3 alDestino = destination - self.transform.position;
            alDestino.y = 0f;
            if (alDestino.magnitude <= threshold || agent.remainingDistance <= threshold)
            {
                ClearPath();
                return true;
            }

            // Sin avanzar nada en la ventana: no hay forma de llegar; se da por cumplido donde se llego.
            ventanaT += dt;
            if (ventanaT >= VentanaSinAvance)
            {
                var movido = self.transform.position - ventanaAncla; movido.y = 0f;
                ResetStuckWatch();
                if (movido.magnitude < AvanceMinimoEnVentana && alDestino.magnitude > threshold + 0.6f)
                {
                    GameLog.Line($"{self.DisplayName} no puede acercarse mas: el destino esta bloqueado");
                    ClearPath();
                    return true;
                }
            }

            // El agent decide POR DONDE (la esquina siguiente); el motor, COMO (colision, giro, animacion).
            Vector3 esquina = agent.steeringTarget;
            Vector3 delta = esquina - self.transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0.001f)
            {
                if (girar) self.Motor.LookTowards(esquina, dt);
                float paso = Mathf.Max(self.Motor.MoveSpeed * dt, 0.0001f);
                self.Motor.Move(delta / dist * Mathf.Min(1f, dist / paso), dt);
            }
            return false;
        }
    }
}
