using System;
using UnityEngine;
using UnityEngine.AI;
using SP.Core;
using SP.Actors;
using SP.Vehicles;

namespace SP.Ai
{
    // AiBrain (parte): rutas, rodeo de obstaculos, deteccion de trabas y avance hacia un punto.
    public partial class AiBrain
    {
        // ------------------------------------------------------------------
        // Rodeo de obstaculos
        // ------------------------------------------------------------------
        // Se llama UNA vez por orden, no por frame. Si la linea recta al
        // destino esta libre (el caso comun) NavService devuelve false sin
        // tocar el A*, la ruta queda vacia y todo se mueve como siempre.
        void PlanPathTo(Vector3 destination)
        {
            path.Clear();
            pathIndex = 0;
            repathed = false;
            ResetStuckWatch();
            // Sella la ruta con la Version actual de la grilla: mientras
            // nadie demuela ni levante un obstaculo de por medio, no hace
            // falta volver a correr A* para este mismo destino.
            pathVersion = SP.Core.NavService.Version;

            EnsureAgent();
            if (AgentActivo)
            {
                SincronizarAgente();
                // Bugs #043/#046 ("los enemigos no vienen / se quedaron inmoviles"): el refresco de rodeo (0,5 s) volvia a pedir
                // la ruta aunque la anterior siguiera calculandose. Con 40+ agentes la cola asincrona del NavMesh tarda mas que
                // eso: cada pedido nuevo pisaba al anterior, la ruta quedaba "pendiente" para siempre y el soldado clavado
                // (medido: pathPending=true, hasPath=false, 0 m en 30 s). Si ya hay un pedido en curso al mismo destino, se espera.
                if (agentTieneDestino && agent.pathPending && (agentDestino - destination).sqrMagnitude < 9f) return;
                if (agent.SetDestination(destination))
                {
                    agentDestino = destination;
                    agentTieneDestino = true;
                    return;
                }
                agentTieneDestino = false;
            }

            if (!SP.Core.NavService.TryFindDetour(self.transform.position, destination, path))
            {
                path.Clear();
                return;
            }

            // El primer punto de la ruta ES la posicion actual: arrancar
            // ahi seria "llegar" al instante y perder un tramo.
            pathIndex = 1;
            GameLog.Line($"{self.DisplayName} rodea un obstaculo: {path.Count - 1} tramos hasta {destination}");
        }

        void ClearPath()
        {
            path.Clear();
            pathIndex = 0;
            repathed = false;
            agentTieneDestino = false;
            if (AgentActivo) agent.ResetPath();
        }

        void ResetStuckWatch()
        {
            stuckTimer = 0f;
            stuckAnchor = self != null ? self.transform.position : Vector3.zero;
        }

        // Igual que Motor.MoveTowards, pero pasando por los waypoints de la
        // ruta si hay una. Devuelve true al llegar al destino FINAL.
        // Hasta donde acercarse cuando NO hay linea de tiro. No es el
        // alcance del arma: es practicamente "hasta tocarlo", porque el
        // problema era justamente frenar antes de tener angulo.
        const float DistanciaDeContacto = 1.5f;

        // Cada cuanto se rehace la ruta de rodeo. El usuario lo pidio con
        // este numero: "con tasa de refresco cada 0.5 segundos para validar
        // antes de disparar para saber si se debe mover o disparar".
        //
        // Y no es solo economia de A*: la primera version replanificaba
        // cuando la ruta estaba vacia, o sea practicamente por tick, y cada
        // PlanPathTo vuelve el indice a 0. El punto 0 de una ruta es la
        // posicion ACTUAL del soldado, asi que llegaba a el, avanzaba el
        // indice, y al tick siguiente volvia a empezar: la ruta se
        // recorria entera sin moverse un centimetro. Medido: 0,0 m en 15
        // segundos, peor que no hacer nada.
        const float RefrescoDeRodeo = 0.5f;

        // Si el enemigo se corrio mas que esto, la ruta vieja lleva a donde
        // ya no esta y se rehace sin esperar al refresco.
        const float RehacerRutaSiSeMovio = 3f;

        Vector3 destinoRodeo;
        bool tieneRodeo;
        float relojDeRodeo;

        // --- Cobertura (F3) ---
        // A que distancia como maximo se acepta ir a buscar una cobertura.
        // Mas lejos que esto, caminar hasta ahi cuesta mas que el rodeo
        // directo y el soldado se aleja del combate.
        public const float RadioDeBusquedaDeCobertura = 25f;
        // Mismo criterio que el rodeo: la eleccion se rehace cada medio
        // segundo, no cada tick. Sin el reloj, PlanPathTo resetea el indice
        // de la ruta todos los frames y el soldado consume el camino sin
        // moverse (bug ya medido en el rodeo: 0,0 m en 15 s).
        const float RefrescoDeCobertura = 0.5f;
        const float DistanciaEnLaCobertura = 0.6f;
        Vector3 coberturaElegida;
        bool tieneCobertura;
        float relojDeCobertura;

        public bool VaHaciaUnaCobertura => tieneCobertura;
        public Vector3 CoberturaElegida => coberturaElegida;

        // BUG REAL reportado por el usuario: "aprieto [Y] (Siganme) dos
        // veces y el primero rehace bien el camino pero el segundo queda
        // trabado". Causa: Follow llamaba a self.Motor.MoveTowards DIRECTO
        // contra la ranura de formacion, sin pasar por AdvanceTo ni
        // PlanPathTo -- no usaba el rodeo de NavService ni TickStuckWatch.
        // Si la ranura de un aliado quedaba del otro lado de un obstaculo
        // respecto de su posicion actual, empujaba contra el para siempre.
        // Generalizado a un solo helper: Patrol y el regreso al puesto en
        // Defensiva (HoldStancePosition) tenian el MISMO problema --
        // MoveTowards directo, sin A* ni deteccion de atasco -- asi que
        // quedarse trabado contra un obstaculo en plena ronda de patrulla
        // era igual de posible que en Follow. Cada llamador pasa su propio
        // trio destino/tiene/reloj por ref para no mezclar el estado de
        // una orden de seguimiento con el de una ronda de patrulla.
        bool AvanzarConRodeo(Vector3 objetivo, float umbral, float dt, ref Vector3 destino, ref bool tieneDestino, ref float reloj)
        {
            reloj += dt;
            bool seMovioMucho = tieneDestino && Vector3.Distance(destino, objetivo) > RehacerRutaSiSeMovio;
            if (!tieneDestino || seMovioMucho || reloj >= RefrescoDeRodeo)
            {
                destino = objetivo;
                tieneDestino = true;
                reloj = 0f;
                // Se replanifica DESDE LA POSICION ACTUAL, asi que empezar
                // de nuevo por el punto 0 no pierde el avance: cada ruta
                // nueva arranca donde el soldado esta parado ahora.
                PlanPathTo(objetivo);
            }
            return AdvanceTo(objetivo, umbral, dt);
        }

        void RodearHasta(Vector3 objetivo, float dt) =>
            AvanzarConRodeo(objetivo, DistanciaDeContacto, dt, ref destinoRodeo, ref tieneRodeo, ref relojDeRodeo);

        Vector3 destinoSeguimiento;
        bool tieneSeguimiento;
        float relojDeSeguimiento;

        void SeguirHasta(Vector3 objetivo, float umbral, float dt) =>
            AvanzarConRodeo(objetivo, umbral, dt, ref destinoSeguimiento, ref tieneSeguimiento, ref relojDeSeguimiento);

        Vector3 destinoPatrulla;
        bool tienePatrulla;
        float relojDePatrulla;

        Vector3 destinoRegresoAPuesto;
        bool tieneRegresoAPuesto;
        float relojDeRegresoAPuesto;

        // F3: en vez de rodear al enemigo al descubierto, ir a la cobertura
        // mas cercana DESDE LA QUE SE LE PUEDE DISPARAR. La segunda mitad
        // es la que importa: esconderse donde no se puede tirar es peor que
        // quedarse afuera -- se deja de hacer daño y ademas no hay motivo
        // para volver a salir.
        //
        // Devuelve false si no hay ninguna que sirva, y ahi el llamador se
        // queda con el rodeo de siempre.
        bool CubrirseDe(Soldier objetivo, float dt)
        {
            relojDeCobertura += dt;
            if (!tieneCobertura || relojDeCobertura >= RefrescoDeCobertura)
            {
                relojDeCobertura = 0f;
                Vector3 elegida;
                // El 0,85 del alcance es el mismo margen con el que ya se
                // frena el acercamiento normal: una cobertura a esa
                // distancia del enemigo es una posicion de tiro de verdad,
                // no una que queda a un paso de quedarse corta.
                elegida = Vector3.zero;
                if (VersionDeCobertura >= 1)
                {
                    tieneCobertura = SP.Core.CoberturasPuntuadas.TryElegir(self.transform.position, objetivo, self, RadioDeBusquedaDeCobertura,
                        EffectiveAttackRange, Vida01, VersionDeCobertura, Recargando, tieneCobertura ? coberturaElegida : (Vector3?)null, out var el);
                    if (tieneCobertura) elegida = el.punto;
                }
                else
                    tieneCobertura = SP.Core.Coberturas.TryMejorCobertura(
                        self.transform.position, objetivo, self, RadioDeBusquedaDeCobertura,
                        EffectiveAttackRange * 0.85f, out elegida);
                if (!tieneCobertura) return false;
                // Solo se replanifica cuando la eleccion CAMBIA: repetir
                // PlanPathTo al mismo punto cada medio segundo tira el
                // avance de la ruta a la basura.
                if ((elegida - coberturaElegida).sqrMagnitude > 0.01f || RemainingPathPoints == 0)
                {
                    coberturaElegida = elegida;
                    PlanPathTo(elegida);
                }
            }
            if (!tieneCobertura) return false;

            // Se para ENCIMA de la cobertura, no a distancia de contacto:
            // el punto de una cobertura es ocuparla. Frenando a 1,5 m el
            // soldado queda al descubierto justo al lado del obstaculo.
            AdvanceTo(coberturaElegida, DistanciaEnLaCobertura, dt);
            return true;
        }

        void SoltarCobertura()
        {
            tieneCobertura = false;
            relojDeCobertura = 0f;
        }

        // ------------------------------------------------------------------
        // Blancos INALCANZABLES
        // ------------------------------------------------------------------
        // BUG REAL ("mis aliados se quedan trabados antes del puesto 3 aunque no haya pelea"): un guardia del puesto siguiente, del
        // otro lado de la barrera todavia cerrada, cuenta como "enemigo visto"; el aliado lo persigue, el NavMesh solo le da una
        // ruta PARCIAL (la barrera corta el paso) que hasta lo manda a dar vueltas por el mapa, y se queda yendo y viniendo sin
        // poder disparar ni volver con el jugador. Ahora, sin linea de tiro y con ruta parcial (o sin acercarse en 8 s), el blanco
        // se descarta 10 s: el aliado vuelve a seguir al jugador y el sensado ya no lo elige hasta que pase ese tiempo.
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
            // Solo los aliados: los enemigos que rodean a la escuadra se comportan como siempre.
            if (target == null || self.Team != SP.Combat.TeamId.Player) return false;
            // El reloj es de la PERSECUCION sin linea de tiro, no de un blanco puntual: con varios guardias detras de la misma
            // barrera el aliado iba cambiando de blanco y el reloj de cada uno arrancaba de cero (MEDIDO: 20 s pegado a la barrera).
            if (Time.time - chaseUltimaLlamada > 1f) { chaseSinLineaT = 0f; chaseDistRef = distanciaAlBlanco; }
            chaseUltimaLlamada = Time.time;
            chaseSinLineaT += dt;
            if (distanciaAlBlanco < chaseDistRef - 2f) { chaseDistRef = distanciaAlBlanco; chaseSinLineaT = Mathf.Min(chaseSinLineaT, 4f); }
            bool parcial = AgentActivo && !agent.pathPending && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathPartial;
            // Pegado a algo (golpes del vigilante de progreso) o sin acercarse: tambien cuenta.
            bool pegado = GolpesSinProgreso >= 1 && chaseSinLineaT > 2f;
            if (!((parcial && chaseSinLineaT > 1.5f) || pegado || chaseSinLineaT > 8f)) return false;

            // Se descarta al blanco y a todos los que estan con el del otro lado (a menos de 12 m de el, sin linea de tiro desde aca).
            string motivo = parcial ? "ruta parcial" : pegado ? "pegado a un obstaculo" : "sin acercarse";
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
            SP.Core.SesionLog.Evento($"{self.DisplayName} descarta {cantidad} objetivo(s) inalcanzable(s) cerca de {vistos.DisplayName} a {distanciaAlBlanco:0.0} m ({motivo})");
            target = null;
            sensedTarget = null;
            chaseSinLineaT = 0f; chaseObjetivoId = -1;
            ClearPath();
            // Vuelve con quien venia siguiendo; si ya no tiene a quien, con el soldado que maneja el jugador.
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

        // ------------------------------------------------------------------
        // Vigilante de PROGRESO (independiente de PlanPathTo)
        // ------------------------------------------------------------------
        // BUG REAL ("se quedan trabados mis aliados al pasar del puesto 2 al 3 sin pelea"): el vigilante de atascos de arriba
        // (TickStuckWatch*) se reinicia en CADA PlanPathTo, y AvanzarConRodeo (Follow, patrulla, rodeo) replanifica cada 0,5 s,
        // asi que su temporizador de ~1 s jamas llegaba a disparar: un aliado empujando contra un muro (o metido en el margen
        // del NavMesh pegado a uno) quedaba trabado para siempre. Este vigilante mide solo el avance real del cuerpo:
        //   1er aviso (2 s sin avanzar 35 cm):  se apoya el cuerpo sobre el NavMesh mas cercano y se rehace la ruta.
        //   2do aviso (aliados):               se lo reubica junto a su destino (si esta lejos) -- mejor un salto que un soldado clavado.
        const float VentanaSinProgreso = 2f;
        const float AvanceMinimo = 0.35f;
        Vector3 sinProgresoAncla;
        float sinProgresoT;
        int sinProgresoGolpes;
        public int GolpesSinProgreso => sinProgresoGolpes;
        public int DesatascosHechos { get; private set; }

        void VigilarProgreso(Vector3 meta, float umbral, float dt)
        {
            if (self == null) return;
            if (sinProgresoT <= 0f && sinProgresoAncla == Vector3.zero) sinProgresoAncla = self.transform.position;
            sinProgresoT += dt;
            if (sinProgresoT < VentanaSinProgreso) return;
            sinProgresoT = 0f;
            var movido = self.transform.position - sinProgresoAncla; movido.y = 0f;
            sinProgresoAncla = self.transform.position;
            var aMeta = meta - self.transform.position; aMeta.y = 0f;
            if (movido.magnitude > AvanceMinimo || aMeta.magnitude <= umbral + 0.6f) { sinProgresoGolpes = 0; return; }
            if (Pasivo || Quieto) return;

            sinProgresoGolpes++;
            DesatascosHechos++;
            if (sinProgresoGolpes == 1)
            {
                if (ApoyarEnNavMesh()) GameLog.Line($"{self.DisplayName} estaba trabado: se apoya en el NavMesh y rehace la ruta");
                else GameLog.Line($"{self.DisplayName} estaba trabado: rehace la ruta");
                PlanPathTo(meta);
                return;
            }
            // Segundo aviso en adelante: solo los aliados se reubican (un enemigo atascado no importa tanto y un salto seria visible).
            // Y solo siguiendo o yendo a un punto: en Chase/cobertura la meta es el enemigo y saltar junto a el seria absurdo.
            if (self.Team != SP.Combat.TeamId.Player || (State != AiState.Follow && State != AiState.MovingToOrder)) { PlanPathTo(meta); return; }
            if (aMeta.magnitude < 5f) { sinProgresoGolpes = 0; return; }   // ya esta cerca: no hace falta saltar
            // A metro y medio de la meta, del lado de donde venia: si la meta es otro soldado (seguir, curar) antes quedaba
            // parado ENCIMA de el (medido en el bug #039: 0,0 m entre el medico y el herido).
            var junto = meta + (aMeta.sqrMagnitude > 0.01f ? -aMeta.normalized : Vector3.back) * 1.5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(junto, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas))
            {
                var p = new Vector3(h.position.x, self.transform.position.y, h.position.z);
                self.transform.position = p;
                SP.Core.ApoyoEnElPiso.Apoyar(self.transform);
                sinProgresoAncla = self.transform.position;
                sinProgresoGolpes = 0;
                ClearPath();
                SincronizarAgente();
                GameLog.Line($"{self.DisplayName} seguia trabado: se reubica junto a su destino {p}");
            }
        }

        // Si el cuerpo quedo en el margen del NavMesh pegado a un muro (o adentro de un solido), lo apoya sobre el punto caminable
        // mas cercano. Devuelve true si lo movio.
        bool ApoyarEnNavMesh()
        {
            if (!UnityEngine.AI.NavMesh.SamplePosition(self.transform.position, out var h, 2.5f, UnityEngine.AI.NavMesh.AllAreas)) return false;
            var d = h.position - self.transform.position; d.y = 0f;
            if (d.magnitude < 0.25f) return false;
            // Un poco hacia adentro del tramo caminable para no volver a quedar rozando el muro.
            var p = new Vector3(h.position.x, self.transform.position.y, h.position.z);
            self.transform.position = p;
            sinProgresoAncla = p;
            SincronizarAgente();
            return true;
        }

        bool AdvanceTo(Vector3 destination, float threshold, float dt)
        {
            EnsureAgent();
            VigilarProgreso(destination, threshold, dt);
            if (TryAdvanceWithAgent(destination, threshold, dt, out bool llegoConAgent))
                return llegoConAgent;

            // La grilla cambio desde que se planeo esta ruta (un obstaculo
            // se demolio o aparecio uno nuevo de por medio): rehacerla YA,
            // no esperar a que el atasco la detecte 1 s+ despues. Sin esto,
            // un soldado en camino seguia bordeando un muro ya derrumbado
            // hasta chocar contra la nada y recien ahi replanificar.
            if (pathVersion != SP.Core.NavService.Version)
                PlanPathTo(destination);

            TickStuckWatch(destination, dt);

            if (pathIndex >= path.Count)
                return self.Motor.MoveTowards(destination, threshold, dt);

            bool last = pathIndex == path.Count - 1;
            // El ultimo punto de la ruta se reemplaza por el destino real:
            // el A* devuelve el centro de un nodo de la grilla, y frenar
            // ahi dejaria al soldado hasta a un nodo del punto pedido.
            Vector3 waypoint = last ? destination : path[pathIndex];

            if (!self.Motor.MoveTowards(waypoint, last ? threshold : WaypointArriveThreshold, dt))
                return false;

            pathIndex++;
            if (pathIndex < path.Count) return false;

            ClearPath();
            return true;
        }

        // Devuelve false si el agent no puede conducir este tramo (no hay
        // NavMesh bajo el soldado, o no hay ruta valida): el llamador sigue
        // con el A* casero de siempre, asi la suite headless (sin NavMesh
        // bakeado) y cualquier escena sin bakear se comportan como antes.
        // Devuelve true si el agent se hizo cargo; "llego" dice si ya llego.
        float pendienteSinRuta;

        bool TryAdvanceWithAgent(Vector3 destination, float threshold, float dt, out bool llego)
        {
            llego = false;
            if (!AgentActivo) return false;

            SincronizarAgente();

            // Bugs #043/#046: no se pisa un pedido que todavia se esta calculando si el destino casi no cambio (ver PlanPathTo).
            float corrimiento = agentTieneDestino ? (agentDestino - destination).sqrMagnitude : float.MaxValue;
            if (!agentTieneDestino || (corrimiento > 0.25f && !(agent.pathPending && corrimiento < 9f)))
            {
                if (!agent.SetDestination(destination))
                {
                    agentTieneDestino = false;
                    return false;
                }
                agentDestino = destination;
                agentTieneDestino = true;
            }

            // Se rehizo el NavMesh (un muro cayo o se cerro un carril): la ruta que el agent traia se planeo con la malla vieja.
            if (agentNavVersion != SP.Core.NavMeshViva.Version)
            {
                agentNavVersion = SP.Core.NavMeshViva.Version;
                agent.SetDestination(destination);
            }

            TickStuckWatchAgent(destination, dt);
            // El vigilante pudo dar la orden por cumplida (ClearPath).
            if (!agentTieneDestino || !AgentActivo) return true;

            // Sin primera ruta todavia: quieto este frame. Con una ruta
            // vieja (replanificacion periodica) se sigue caminando por ella
            // para no frenar cada medio segundo.
            // Bugs #043/#046: si la primera ruta tarda (cola del NavMesh cargada), en vez de quedarse plantado se avanza derecho
            // hacia el destino mientras tanto (el motor ya frena contra paredes); apenas llega la ruta, manda ella.
            if (agent.pathPending && !agent.hasPath)
            {
                pendienteSinRuta += dt;
                if (pendienteSinRuta > 0.4f) self.Motor.MoveTowards(destination, threshold, dt);
                return true;
            }
            pendienteSinRuta = 0f;
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                agentTieneDestino = false;
                return false;
            }

            Vector3 alDestino = destination - self.transform.position;
            alDestino.y = 0f;
            if (alDestino.magnitude <= threshold ||
                (!agent.pathPending && agent.remainingDistance <= threshold))
            {
                ClearPath();
                llego = true;
                return true;
            }

            // El agent decide POR DONDE (la esquina siguiente de su ruta);
            // el motor decide COMO (colision, giro, animacion). El paso se
            // acota a la distancia que falta para no pasarse de la esquina y
            // oscilar alrededor de ella.
            Vector3 esquina = agent.steeringTarget;
            Vector3 delta = esquina - self.transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0.001f)
            {
                self.Motor.LookTowards(esquina, dt);
                float paso = Mathf.Max(self.Motor.MoveSpeed * dt, 0.0001f);
                self.Motor.Move(delta / dist * Mathf.Min(1f, dist / paso), dt);
            }
            return true;
        }

        // Mismo contrato que TickStuckWatch (1 s sin avanzar 30 cm ->
        // replanifica una vez; segundo atasco en MovingToOrder -> orden
        // cumplida donde llego, drena la cola, OrderCompletedEvent), pero
        // la replanificacion es agent.SetDestination. Solo mide progreso
        // real: un PathPartial (destino fuera del NavMesh) NO cuenta como
        // atasco mientras el soldado siga avanzando hacia el borde.
        void TickStuckWatchAgent(Vector3 destination, float dt)
        {
            stuckTimer += dt;
            if (stuckTimer < StuckSeconds) return;

            Vector3 progress = self.transform.position - stuckAnchor;
            progress.y = 0f;
            bool stuck = progress.sqrMagnitude < StuckProgressSqr;
            ResetStuckWatch();

            if (!stuck) return;

            if (repathed && State == AiState.MovingToOrder)
            {
                GameLog.Line($"{self.DisplayName} no puede acercarse mas: el destino esta bloqueado");
                if (orderQueue.Count > 0)
                {
                    orderDestination = orderQueue.Dequeue();
                    PlanPathTo(orderDestination);
                    return;
                }
                EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));
                hasOrder = false;
                ClearPath();
                SetState(AiState.Patrol);
                forceSense = true;
                return;
            }

            if (repathed) return;
            repathed = true;
            agent.SetDestination(destination);
            GameLog.Line($"{self.DisplayName} estaba trabado: recalcula la ruta por NavMesh");
        }

        void TickStuckWatch(Vector3 destination, float dt)
        {
            stuckTimer += dt;
            if (stuckTimer < StuckSeconds) return;

            Vector3 progress = self.transform.position - stuckAnchor;
            progress.y = 0f;
            bool stuck = progress.sqrMagnitude < StuckProgressSqr;
            ResetStuckWatch();

            if (!stuck) return;

            // SEGUNDO ATASCO: la ruta ya se recalculo una vez y el soldado
            // sigue sin avanzar. Eso significa que el destino no se puede
            // alcanzar -- tipicamente porque cae DENTRO de un solido (un
            // arbol, el Muro, una barricada).
            //
            // BUG REAL medido: sin esto el soldado empujaba contra el
            // obstaculo indefinidamente. A los 20 segundos seguia en
            // MovingToOrder a 0,70 m del destino, y como la orden nunca
            // terminaba tampoco publicaba OrderCompletedEvent, ni sacaba
            // el siguiente punto de la cola, ni volvia a Patrol: el
            // soldado quedaba inutil por el resto de la partida.
            //
            // Se da la orden por cumplida donde se pudo llegar. Es lo
            // honesto: el jugador ve al soldado detenerse y volver a estar
            // disponible, en vez de un cuerpo empujando una pared.
            if (repathed && State == AiState.MovingToOrder)
            {
                GameLog.Line($"{self.DisplayName} no puede acercarse mas: el destino esta bloqueado");
                if (orderQueue.Count > 0)
                {
                    orderDestination = orderQueue.Dequeue();
                    PlanPathTo(orderDestination);
                    return;
                }
                EventBus.Instance.Publish(new OrderCompletedEvent(self.Id));
                hasOrder = false;
                ClearPath();
                SetState(AiState.Patrol);
                forceSense = true;
                return;
            }

            // Primer atasco: se recalcula la ruta UNA vez. Reintentar sin
            // limite seria correr un A* por segundo por cada soldado
            // trabado contra otro cuerpo.
            if (repathed) return;
            repathed = true;

            path.Clear();
            pathIndex = 0;
            pathVersion = SP.Core.NavService.Version;
            if (SP.Core.NavService.TryFindDetour(self.transform.position, destination, path))
            {
                pathIndex = 1;
                GameLog.Line($"{self.DisplayName} estaba trabado: recalcula la ruta ({path.Count - 1} tramos)");
            }
        }
    }
}
