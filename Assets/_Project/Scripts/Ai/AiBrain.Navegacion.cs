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

            EnsureAgent();
            if (AgentActivo)
            {
                SincronizarAgente();
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

        bool AdvanceTo(Vector3 destination, float threshold, float dt)
        {
            EnsureAgent();
            if (TryAdvanceWithAgent(destination, threshold, dt, out bool llegoConAgent))
                return llegoConAgent;

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
        bool TryAdvanceWithAgent(Vector3 destination, float threshold, float dt, out bool llego)
        {
            llego = false;
            if (!AgentActivo) return false;

            SincronizarAgente();

            if (!agentTieneDestino || (agentDestino - destination).sqrMagnitude > 0.25f)
            {
                if (!agent.SetDestination(destination))
                {
                    agentTieneDestino = false;
                    return false;
                }
                agentDestino = destination;
                agentTieneDestino = true;
            }

            TickStuckWatchAgent(destination, dt);
            // El vigilante pudo dar la orden por cumplida (ClearPath).
            if (!agentTieneDestino || !AgentActivo) return true;

            // Sin primera ruta todavia: quieto este frame. Con una ruta
            // vieja (replanificacion periodica) se sigue caminando por ella
            // para no frenar cada medio segundo.
            if (agent.pathPending && !agent.hasPath) return true;
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
            if (SP.Core.NavService.TryFindDetour(self.transform.position, destination, path))
            {
                pathIndex = 1;
                GameLog.Line($"{self.DisplayName} estaba trabado: recalcula la ruta ({path.Count - 1} tramos)");
            }
        }
    }
}
