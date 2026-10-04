using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;
using SP.Presentation;

namespace SP.Ai
{
    // Parte "tactica" del cerebro: coberturas (ordenadas por el jugador o
    // buscadas por el propio enemigo en combate), seguir al jugador cuando se
    // aleja, y eleccion DINAMICA del objetivo por distancia y vision.
    public partial class AiBrain
    {
        // ------------------------------------------------------------------
        // Cobertura
        // ------------------------------------------------------------------
        bool enCobertura;          // parado en su cobertura, agachado
        bool yendoACobertura;      // caminando hacia ella
        bool coberturaPorOrden;    // la pidio el jugador (el enemigo la decide solo)
        Vector3 coberturaPunto;
        Collider coberturaDueno;
        float relojVigilancia;
        float relojTactico;

        // El vehiculo al que este soldado esta yendo a subir (o null).
        public SP.Vehicles.Vehicle MountTargetVehicle => mountTarget;

        public bool EnCobertura => enCobertura;
        public bool YendoACobertura => yendoACobertura;
        public bool CoberturaPorOrden => coberturaPorOrden;
        public Vector3 CoberturaPunto => coberturaPunto;

        // Bug #061: "revisa que los aliados y enemigos usen coberturas". Antes solo los enemigos se cubrian solos (los aliados
        // unicamente con la dificultad humanizada y heridos: en la practica nunca). Ahora tambien los aliados que no maneja el
        // jugador; la decision de si conviene cubrirse sigue siendo la ponderada (CoberturasPuntuadas.Presion).
        // Bug #061: radio de la cobertura "a mano" que se toma aun sin presion (ver TickCoberturaTactica).
        public const float RadioDeCoberturaAMano = 8f;
        bool BuscaCoberturaSolo => self != null && !SinCobertura && (CoberturaPropia || self.Team == TeamId.Enemy
            || (self.Team == TeamId.Player && self.Role != RoleType.Civilian && !IsPossessedByPlayer && !Pasivo));

        // Version de la eleccion de cobertura: 0 = la vieja (la mas cercana con linea de tiro), 1..5 = puntuacion ponderada
        // (ver CoberturasPuntuadas). El banco de duelos (CoberturaBench) la cambia por soldado para comparar iteraciones.
        public static int DefaultVersionCobertura = 5;
        public int VersionCobertura = -1;
        public int VersionDeCobertura => VersionCobertura >= 0 ? VersionCobertura : DefaultVersionCobertura;
        public bool SinCobertura;       // banco de pruebas: el rival "sin IA de cobertura"
        public bool CoberturaPropia;    // fuerza la busqueda autonoma de cobertura (banco de pruebas)
        public float UltimaPresion { get; private set; }
        public int CoberturasElegidas { get; private set; }
        public int CambiosDeCobertura { get; private set; }
        float relojReevaluar;

        float Vida01 => self != null && self.Health != null ? (float)self.Health.Current / Mathf.Max(1, self.Health.MaxHealth) : 1f;
        bool Recargando => self != null && self.Weapon != null && (self.Weapon.IsReloading || self.Weapon.CurrentAmmo <= 0);

        // El jugador manda a este soldado a cubrirse en 'punto': camina, se
        // agacha y se queda ahi hasta recibir otra orden.
        public void IssueCoverOrder(Vector3 punto, Collider dueno)
        {
            if (!bootstrapped) Bootstrap();
            // Una orden de cobertura corta el combate en curso: el soldado
            // obedece (IssueMoveOrder, en combate, solo redirige el camino).
            target = null;
            IssueMoveOrder(punto);   // libera cualquier cobertura previa
            coberturaPunto = punto;
            coberturaDueno = dueno;
            coberturaPorOrden = true;
            yendoACobertura = true;
            enCobertura = false;
        }

        // Toda orden nueva del jugador suelta la cobertura y el "seguir solo".
        // "Todos quietos": no sigue al jugador ni se mueve hasta recibir otra orden.
        bool quieto;
        CombatStance posturaAntesDeQuieto = CombatStance.Libre;
        public bool Quieto
        {
            get => quieto;
            set
            {
                if (value == quieto) return;
                quieto = value;
                if (value)
                {
                    // Plantado: en Libre pasa a Defensiva (no persigue); en otra postura (p. ej. el
                    // alto el fuego del tutorial) se respeta.
                    posturaAntesDeQuieto = Stance;
                    if (Stance == CombatStance.Libre) Stance = CombatStance.Defensiva;
                }
                else Stance = posturaAntesDeQuieto;
            }
        }

        // Civil: no reacciona al combate (ni ve enemigos ni devuelve fuego), solo sigue ordenes.
        public bool Pasivo { get; set; }

        // Freeze global de IA (ambos equipos): pedido explicito "mientras este reproduciendose la
        // cinematica inicial no se muevan ni enemigos ni aliados ni dispare". A diferencia de
        // Pasivo (por-instancia, solo bloquea reaccion a combate), esto corta el Tick() entero
        // -- ni sensado, ni movimiento, ni disparo -- para TODOS los AiBrain a la vez. Lo
        // prende/apaga CinematicaDeIntro.
        public static bool IAPausada;

        void NuevaOrden()
        {
            // Bug #8: una orden del jugador (seguir, ir, atacar...) al medico que estaba con un pedido de curacion lo dejaba
            // atado al pedido viejo (y Pasivo: no peleaba) hasta 30 s. Cualquier orden que NO venga del propio pedido lo suelta.
            SP.Player.PedidoDeCuracion.SoltarSiEsElEnfermero(self);
            LiberarCobertura();
            seguirAuto = false;
            if (Quieto) Quieto = false;
        }

        // Feedback de deteccion: el enemigo que ve a la escuadra marca un "!"
        // rojo sobre su cabeza y suena un aviso (limitado para no saturar).
        static float ultimoAvisoDeteccion = -99f;
        float proximoAvisoPropio;

        void AvisarDeteccion(Soldier detectado)
        {
            if (self.Team != TeamId.Enemy) return;
            if (State == AiState.Chase || State == AiState.Attack) return;
            if (Time.time < proximoAvisoPropio || Time.unscaledTime - ultimoAvisoDeteccion < 0.5f) return;
            proximoAvisoPropio = Time.time + 6f;
            ultimoAvisoDeteccion = Time.unscaledTime;
            Feedback.Accion(SfxKind.EnemySpotted, "!", self.transform.position, Feedback.Bad, aviso: false, pulso: false, volumen: 0.45f);
        }

        void LiberarCobertura()
        {
            bool estaba = enCobertura || yendoACobertura;
            enCobertura = false;
            yendoACobertura = false;
            coberturaPorOrden = false;
            coberturaDueno = null;
            if (estaba && self != null && self.Motor != null) self.Motor.SetCrouching(false);
        }

        public enum CoverSubState { Hidden, Peeking }
        CoverSubState subStateCobertura;
        float relojSubStateCobertura;
        bool esCoberturaDeBorde;
        Vector3 lateralOffset;

        void EntrarEnCoberturaBase()
        {
            subStateCobertura = CoverSubState.Hidden;
            relojSubStateCobertura = DuracionOculto();
            esCoberturaDeBorde = false;
            lateralOffset = Vector3.zero;

            if (coberturaDueno != null)
            {
                if (coberturaDueno.bounds.size.y >= 1.4f)
                {
                    esCoberturaDeBorde = true;
                }
            }
        }

        // Ciclo oculto/asoma. Antes fijo (oculto 1.2-2.0 s, asoma 0.8-1.4 s: dispara ~40% del tiempo). Con F_CICLO depende de la vida:
        // sano asoma casi todo el tiempo, herido se esconde mas.
        float DuracionOculto()
        {
            if (!CoberturasPuntuadas.Usa(VersionDeCobertura, CoberturasPuntuadas.F_CICLO)) return UnityEngine.Random.Range(1.2f, 2.0f);
            return Mathf.Lerp(0.35f, 1.8f, 1f - Vida01) * UnityEngine.Random.Range(0.85f, 1.15f);
        }

        float DuracionAsomado()
        {
            if (!CoberturasPuntuadas.Usa(VersionDeCobertura, CoberturasPuntuadas.F_CICLO)) return UnityEngine.Random.Range(0.8f, 1.4f);
            return Mathf.Lerp(2.2f, 0.7f, 1f - Vida01) * UnityEngine.Random.Range(0.85f, 1.15f);
        }

        void TickCicloCobertura(float dt)
        {
            if (!enCobertura) return;

            bool isReloading = self.Weapon != null && self.Weapon.IsReloading;
            bool isMagazineEmpty = self.Weapon != null && self.Weapon.CurrentAmmo <= 0;

            relojSubStateCobertura -= dt;

            if (subStateCobertura == CoverSubState.Peeking && isMagazineEmpty)
            {
                subStateCobertura = CoverSubState.Hidden;
                if (self.Weapon != null) self.Weapon.Reload();
                relojSubStateCobertura = (self.Weapon != null && self.Weapon.ReloadRemaining > 0f) ? self.Weapon.ReloadRemaining : 1.5f;
            }
            else if (relojSubStateCobertura <= 0f)
            {
                if (subStateCobertura == CoverSubState.Hidden)
                {
                    if (isReloading || isMagazineEmpty)
                    {
                        relojSubStateCobertura = 0.1f;
                    }
                    else
                    {
                        subStateCobertura = CoverSubState.Peeking;
                        relojSubStateCobertura = DuracionAsomado();
                        
                        if (esCoberturaDeBorde)
                        {
                            Vector3 frente = Coberturas.FrenteDe(coberturaPunto, coberturaDueno);
                            Vector3 derecha = Vector3.Cross(Vector3.up, frente).normalized;
                            if (target != null)
                            {
                                Vector3 toTarget = target.transform.position - coberturaPunto;
                                if (Vector3.Dot(toTarget, derecha) < 0) derecha = -derecha;
                            }
                            lateralOffset = derecha * 0.6f;
                        }
                    }
                }
                else
                {
                    subStateCobertura = CoverSubState.Hidden;
                    relojSubStateCobertura = DuracionOculto();
                }
            }

            Vector3 targetPos = coberturaPunto;
            if (subStateCobertura == CoverSubState.Peeking && esCoberturaDeBorde)
            {
                targetPos = coberturaPunto + lateralOffset;
            }

            Vector3 disp = targetPos - self.transform.position;
            disp.y = 0f;
            if (disp.sqrMagnitude > 0.01f)
            {
                self.Motor.Move(disp.normalized, dt);
            }

            if (State != AiState.Attack)
            {
                if (subStateCobertura == CoverSubState.Hidden)
                    self.Motor.SetCrouching(true);
                else
                    self.Motor.SetCrouching(esCoberturaDeBorde);
            }
        }

        // Llegada a la cobertura ordenada.
        void EntrarEnCobertura()
        {
            enCobertura = true;
            yendoACobertura = false;
            hasOrder = false;
            ClearPath();
            SetState(AiState.Idle);
            EntrarEnCoberturaBase();
            self.Motor.SetCrouching(true);
            // Mira hacia afuera del obstaculo (hacia donde vendria el enemigo).
            if (coberturaDueno != null)
            {
                var c = coberturaDueno.bounds.center;
                var f = coberturaPunto - new Vector3(c.x, coberturaPunto.y, c.z);
                f.y = 0f;
                if (f.sqrMagnitude > 0.01f) self.Motor.LookTowards(coberturaPunto + f.normalized * 5f, 10f);
            }
            Feedback.Accion(SfxKind.CoverTake, null,
                self.transform.position, Feedback.Cover, aviso: false, pulso: true, volumen: 0.5f);
        }

        // Vigila que el obstaculo que lo cubre siga en pie. Barato: cuatro
        // veces por segundo.
        void TickCobertura(float dt)
        {
            if (!enCobertura && !yendoACobertura) return;
            relojVigilancia += dt;
            if (relojVigilancia < 0.25f) return;
            relojVigilancia = 0f;
            if (coberturaDueno == null || Coberturas.Vigente(coberturaDueno)) return;
            PerderCobertura();
        }

        // "Se dio cuenta": la cobertura se destruyo. Busca otra cerca; si no
        // hay, pelea de pie.
        void PerderCobertura()
        {
            bool ordenada = coberturaPorOrden;
            var donde = self.transform.position;
            enCobertura = false;
            yendoACobertura = false;
            coberturaDueno = null;
            Feedback.Accion(SfxKind.CoverLost,
                ordenada ? $"¡{self.DisplayName.ToUpperInvariant()}: COBERTURA DESTRUIDA!" : "¡COBERTURA DESTRUIDA!",
                donde, Feedback.Warn, aviso: ordenada, pulso: false, volumen: 0.6f);
            self.Motor.SetCrouching(false);

            if (Coberturas.TryCercano(donde, 14f, out var nuevo, out var dueno) && (nuevo - donde).sqrMagnitude > 0.5f)
            {
                if (ordenada) IssueCoverOrder(nuevo, dueno);
                else
                {
                    coberturaPunto = nuevo;
                    coberturaDueno = dueno;
                    yendoACobertura = true;
                    PlanPathTo(nuevo);
                }
            }
            else coberturaPorOrden = false;
        }

        // Enemigo en combate: va a una cobertura desde la que pueda seguir
        // disparando, se agacha y dispara. Devuelve true mientras esta
        // CORRIENDO hacia ella (ese tick no dispara).
        bool TickCoberturaTactica(float dt)
        {
            if (!BuscaCoberturaSolo || target == null) return false;

            if (yendoACobertura)
            {
                self.Motor.SetCrouching(false);
                if (FuegoDeCoberturaHerido(dt)) return true;
                if (AdvanceTo(coberturaPunto, 0.6f, dt)) EntrarEnCoberturaTactica();
                else return true;
            }

            int version = VersionDeCobertura;
            if (enCobertura)
            {
                // v5: ya cubierto, pero si cambio la situacion (se hirio, recargo, hay otra claramente mejor) se muda.
                if (CoberturasPuntuadas.Usa(version, CoberturasPuntuadas.F_MUDARSE) && !coberturaPorOrden)
                {
                    relojReevaluar -= dt;
                    if (relojReevaluar <= 0f)
                    {
                        relojReevaluar = 2.5f;
                        if (CoberturasPuntuadas.TryElegir(self.transform.position, target, self, 12f, EffectiveAttackRange, Vida01, version, Recargando, coberturaPunto, out var otra)
                            && (otra.punto - coberturaPunto).sqrMagnitude > 2f * 2f)
                        {
                            // Puntaje de la actual: se re-evalua restringido a ella con la histeresis ya sumada.
                            if (CoberturasPuntuadas.TryElegir(coberturaPunto, target, self, 1.2f, EffectiveAttackRange, Vida01, version, Recargando, coberturaPunto, out var actualE)
                                ? otra.puntaje > actualE.puntaje + 0.5f : true)
                            {
                                CambiosDeCobertura++;
                                coberturaPunto = otra.punto; coberturaDueno = otra.dueno;
                                enCobertura = false; yendoACobertura = true;
                                PlanPathTo(otra.punto);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            relojTactico -= dt;
            if (relojTactico > 0f) return false;
            relojTactico = 1.2f + (self.Id % 5) * 0.15f;

            float dActual = Vector3.Distance(self.transform.position, target.transform.position);
            // Decision ponderada: sano y sin presion NO se cubre (cubrirse cuesta fuego: solo dispara mientras asoma)...
            bool sinPresion = false;
            if (CoberturasPuntuadas.Usa(version, CoberturasPuntuadas.F_DECIDIR))
            {
                int amenazas = CoberturasPuntuadas.AmenazasSobre(self);
                UltimaPresion = CoberturasPuntuadas.Presion(Vida01, Recargando, amenazas, dActual, EffectiveAttackRange);
                sinPresion = UltimaPresion <= CoberturasPuntuadas.UmbralDeCubrirse;
            }
            Vector3 punto; Collider dueno;
            if (sinPresion)
            {
                // ...salvo (bug #061) una cobertura A MANO desde la que sigue disparando: cuesta poco y es lo que haria
                // cualquiera. Medido en la ciudad: con 26 enemigos peleando se cubria 1, porque la escuadra (3) no alcanza
                // para "presionar" a un enemigo sano.
                if (!CoberturasPuntuadas.TryElegir(self.transform.position, target, self, RadioDeCoberturaAMano, EffectiveAttackRange, Vida01, version, Recargando, null, out var cerca)
                    || !cerca.puedeDisparar) return false;
                punto = cerca.punto; dueno = cerca.dueno;
            }
            else if (version >= 1)
            {
                if (!CoberturasPuntuadas.TryElegir(self.transform.position, target, self, 12f, EffectiveAttackRange, Vida01, version, Recargando, null, out var el)) return false;
                punto = el.punto; dueno = el.dueno;
            }
            else if (!Coberturas.TryCoberturaDeTiro(self.transform.position, target, self, 12f,
                    EffectiveAttackRange * 0.45f, EffectiveAttackRange * 0.9f, out punto, out dueno)) return false;
            CoberturasElegidas++;
            // Solo si de verdad cambia algo: no correr 1 m, ni alejarse.
            if ((punto - self.transform.position).sqrMagnitude < 1.5f * 1.5f) return false;
            if (Vector3.Distance(punto, target.transform.position) > dActual + 2f) return false;

            coberturaPunto = punto;
            coberturaDueno = dueno;
            coberturaPorOrden = false;
            yendoACobertura = true;
            PlanPathTo(punto);
            Feedback.Accion(SfxKind.EnemySpotted, "¡A CUBIERTO!", self.transform.position, Feedback.Bad, aviso: false, pulso: false, volumen: 0.3f);
            return true;
        }

        void EntrarEnCoberturaTactica()
        {
            enCobertura = true;
            yendoACobertura = false;
            EntrarEnCoberturaBase();
            self.Motor.SetCrouching(true);
        }

        // ------------------------------------------------------------------
        // Seguir al jugador cuando se aleja
        // ------------------------------------------------------------------
        bool seguirAuto;
        static float ultimoAvisoDeSeguir = -99f;
        // Mas alla de esto el aliado "se fue del minimapa" y vuelve si o si (bug #16).
        public static float RadioDeRegresoForzado = 45f;
        float proximoRegresoForzado, ultimoAvisoRegreso = -99f;
        public int RegresosForzados { get; private set; }

        public bool SiguiendoAlJugador => seguirAuto;

        void TickSeguirAlJugador()
        {
            // BUG REAL: el civil (Pasivo=true, "no reacciona a nada, solo
            // sigue ordenes") quedaba afuera de este chequeo -- este
            // llamado auto-sigue-al-lider corre para CUALQUIER aliado
            // libre que este lejos, sin mirar Pasivo. Resultado: el civil
            // arrancaba a caminar hacia el jugador desde su escondite ni
            // bien aparecia, en vez de esperar a que el jugador se acerque
            // (la mecanica real de rescate en MisionDirector.TickRescatar).
            // La orden explicita de seguir que llega despues del rescate
            // (MisionDirector.SeguirAlJugador -> OrderService.IssueFollowOrder)
            // no pasa por aca, asi que este guard no la afecta.
            if (self.Team != TeamId.Player || Quieto || Pasivo) return;
            var lider = AjustesDeEscuadra.Lider;
            if (lider == null || lider == self || lider.Health == null || !lider.Health.IsAlive || !lider.gameObject.activeInHierarchy)
            {
                if (seguirAuto) DejarDeSeguirAuto();
                return;
            }

            var d = lider.transform.position - self.transform.position;
            d.y = 0f;
            float dist = d.magnitude;

            // Bug #16: "si los aliados desaparecen de la visibilidad del minimapa, si o si vienen hacia mi; les costo mucho venir y
            // se distraen con un enemigo". Mas lejos que RadioDeRegresoForzado vuelven SI O SI: seguimiento forzado (no se
            // enganchan con ningun enemigo durante el lapso), renovado mientras sigan lejos. Solo se respeta lo que el jugador
            // fijo a proposito: ALTO (Quieto), montar un vehiculo, una cobertura ordenada, una orden explicita de IR/ATACAR, o que
            // este atendiendo un pedido de curacion.
            if (dist > RadioDeRegresoForzado && mountTarget == null && !coberturaPorOrden && !MontadoEnVehiculo
                && !(hasOrder && State != AiState.Follow) && SP.Player.PedidoDeCuracion.Enfermero != self)
            {
                if (Time.time >= proximoRegresoForzado)
                {
                    proximoRegresoForzado = Time.time + 3.5f;
                    IssueFollowOrder(lider, default, 4f);
                    seguirAuto = true;
                    RegresosForzados++;
                    if (Time.time - ultimoAvisoRegreso > 8f)
                    {
                        ultimoAvisoRegreso = Time.time;
                        SP.Core.SesionLog.Evento(System.FormattableString.Invariant($"{self.DisplayName} estaba a {dist:0} m (fuera del minimapa): vuelve SI O SI con {lider.DisplayName}"));
                    }
                }
                return;
            }

            if (seguirAuto)
            {
                bool enCombate = State == AiState.Chase || State == AiState.Attack || State == AiState.MovingToAttackOrder;
                if (State != AiState.Follow && !enCombate) { seguirAuto = false; return; }
                if (State == AiState.Follow && dist <= AjustesDeEscuadra.DistanciaParaDetenerse) DejarDeSeguirAuto();
                return;
            }

            // Pedido explicito: "si me alejo mucho no importa si estan
            // atacando o siendo atacados, vuelven a seguirme si no estan
            // fijados o sobre algo". Antes "libre" exigia Idle/Patrol Y
            // target==null: un soldado que estaba peleando (o que se fue
            // solo a una cobertura de tiro lejana persiguiendo a un enemigo
            // que ve de lejos, como el francotirador) nunca calificaba para
            // el llamado automatico, sin importar cuanto se alejara. Ahora
            // Chase/Attack SIN orden explicita de ataque tambien cuentan
            // como "libre": si el jugador se aleja lo suficiente el soldado
            // corta lo que esta haciendo y vuelve. Lo que SI lo "fija" y
            // sigue respetandose: una orden explicita (hasOrder/orderIsAttack),
            // estar montando un vehiculo, o una cobertura pedida a mano
            // (coberturaPorOrden) -- eso solo se suelta con otra orden.
            bool enCombateSinOrden = (State == AiState.Chase || State == AiState.Attack) && !orderIsAttack;
            bool libre = (State == AiState.Idle || State == AiState.Patrol || enCombateSinOrden) && !hasOrder && mountTarget == null;
            if (!libre || yendoACobertura) return;
            if (enCobertura && (coberturaPorOrden || !AjustesDeEscuadra.SeguirDesdeCobertura)) return;
            if (dist <= AjustesDeEscuadra.DistanciaParaSeguir) return;

            ComenzarSeguirAlJugador(lider);
        }

        void TickAgachadoContagio()
        {
            if (self.Team != TeamId.Player) return;
            var playerBrain = SP.Player.PlayerBrain.Activo;
            if (playerBrain == null || playerBrain.Current == null) return;
            
            var possessed = playerBrain.Current;
            if (possessed == self) return;

            if (State != AiState.Follow || followTarget != possessed) return;

            float dist = Vector3.Distance(transform.position, possessed.transform.position);
            if (dist <= 8f)
            {
                bool crouch = possessed.Motor.IsCrouching;
                if (!crouch && enCobertura) return;
                self.Motor.SetCrouching(crouch);
            }
        }

        void ComenzarSeguirAlJugador(Soldier lider)
        {
            LiberarCobertura();
            target = null;
            hasOrder = true;
            orderIsAttack = false;
            mountTarget = null;
            orderQueue.Clear();
            orderDestination = self.transform.position;   // si el combate lo desvia, no vuelve a un punto viejo
            ClearPath();
            followTarget = lider;

            int idx = 0;
            foreach (var s in SP.Core.ActorRegistry.All)
            {
                if (s == self) continue;
                if (s.Brain != null && s.Brain.FollowTarget == lider) idx++;
            }
            int row = (idx / 2) + 1;
            float sign = (idx % 2 == 0) ? 1f : -1f;
            float px = sign * row * AjustesDeEscuadra.DistanciaLateralFormacion;
            // BUG REAL reportado jugando: la primer fila (los dos aliados
            // mas comunes, escuadra chica) iba en cuña BEHIND -- en la
            // camara en primera persona del jugador quedaban fuera de
            // cuadro, detras del hombro, invisibles mientras caminabas.
            // Pedido explicito: "que vayan a mis lados, visibles". La
            // primera fila ahora va EXACTO al costado (pz=0); solo si hay
            // mas de dos aliados siguiendote, las filas siguientes se caen
            // un poco hacia atras para no pisar a la fila de adelante.
            float pz = -(row - 1) * AjustesDeEscuadra.DistanciaAtrasFormacion;
            followOffsetLocal = new Vector3(px, 0f, pz);

            seguirAuto = true;
            SetState(AiState.Follow);
            forceSense = true;

            if (Time.unscaledTime - ultimoAvisoDeSeguir > 4f)
            {
                ultimoAvisoDeSeguir = Time.unscaledTime;
                // Pedido explicito: quitar el cartel "ALIADOS TE SIGUEN" -- molesta.
                // Se deja solo el sonido (sin texto no hay WorldTag ni AlertQueue,
                // ver Feedback.Accion) como confirmacion de que la orden llego.
                Feedback.Accion(SfxKind.FollowCall, null, self.transform.position, Feedback.Ok, aviso: true, pulso: true, volumen: 0.5f);
            }
        }

        void DejarDeSeguirAuto()
        {
            seguirAuto = false;
            if (State != AiState.Follow) return;
            hasOrder = false;
            followTarget = null;
            followOffsetLocal = Vector3.zero;
            ClearPath();
            SetState(AiState.Idle);
        }

        // ------------------------------------------------------------------
        // Objetivo dinamico: distancia + vision
        // ------------------------------------------------------------------
        // Antes: "el enemigo mas cercano en el radio de vision", y una vez
        // elegido se lo perseguia hasta perderlo. Ahora se pondera:
        //   - distancia (mas cerca = mejor)
        //   - vision: dentro del radio de vision se lo "siente" (como
        //     siempre); mas alla, hasta 1,6x, solo si esta DENTRO DEL CONO
        //     frontal y con LINEA DE TIRO
        //   - sin linea de tiro pesa mas (mejor uno que se ve que uno
        //     detras de una pared)
        //   - quien acaba de dispararme pesa mas; el herido, un poco mas
        //   - el objetivo actual tiene ventaja (histeresis): no salta de uno
        //     a otro por diferencias minimas
        public const float AlcanceExtendido = 1.6f;
        public const float SemiconoDeVision = 100f;
        readonly List<Soldier> candidatos = new List<Soldier>(16);
        int ultimoAtacanteId = -1;
        float tiempoUltimoAtaque = -99f;
        float relojRetarget;

        public float PuntajeDeObjetivo(Soldier s)
        {
            if (s == null || self == null) return float.MaxValue;
            var pos = self.transform.position;
            float dist = Vector3.Distance(pos, s.transform.position);
            bool linea = TieneLineaDeTiro(s);
            float p = dist;
            if (!linea) p += 12f;
            if (s.Id == ultimoAtacanteId && Time.time - tiempoUltimoAtaque < 4f) p -= 10f;
            if (s.Health != null && s.Health.MaxHealth > 0 && s.Health.Current < s.Health.MaxHealth * 0.4f) p -= 3f;
            if (s == target) p -= 5f;
            return p;
        }

        bool EnElCono(Soldier s)
        {
            var d = s.transform.position - self.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(self.transform.forward, d) <= SemiconoDeVision;
        }

        Soldier MejorObjetivoVisible()
        {
            float vision = EffectiveVisionRange;
            var equipo = self.Team;
            // BUG REAL ("perdes porque murio el rehen sin haber hecho nada raro"): el civil (Role ==
            // Civilian) es un no-combatiente Pasivo -- no ve enemigos ni devuelve fuego (ver
            // Pasivo mas arriba) -- pero antes de este chequeo SI calificaba como blanco valido para
            // cualquier enemigo que lo viera, igual que un soldado mas. Como queda visible desde el
            // arranque de la mision (SpawnCivilOculto en MisionDirector.Start), bastaba que un
            // enemigo de patrulla le tuviera linea de vision en cualquier momento de Infiltrar o
            // Resistir para matarlo sin que el jugador pudiera hacer nada -- y TickRescatar/
            // TickEscapar pierden la mision apenas Civil.Health.IsAlive da false. Un rehen indefenso
            // no deberia ser un objetivo militar prioritario: se lo excluye del sensado de enemigos.
            SpatialGrid.QueryInRange(self.transform.position, vision * AlcanceExtendido, candidatos,
                s => s.Health != null && s.Health.IsAlive && s.Team != equipo && s.Role != RoleType.Civilian);

            Soldier mejor = null;
            float mejorPuntaje = float.MaxValue;
            for (int i = 0; i < candidatos.Count; i++)
            {
                var s = candidatos[i];
                if (Ignorando(s)) continue;   // descartado por inalcanzable (ver VigilarInalcanzable)
                float dist = Vector3.Distance(self.transform.position, s.transform.position);
                // De noche un enemigo ve a distancia completa solo a quien esta iluminado (ver IluminacionTactica).
                if (equipo == TeamId.Enemy && dist > IluminacionTactica.VisionContra(s.transform.position, vision)) continue;
                if (dist > vision)
                {
                    // Vision extendida: cono + linea de tiro.
                    if (!EnElCono(s) || !TieneLineaDeTiro(s)) continue;
                }
                float p = PuntajeDeObjetivo(s);
                if (p < mejorPuntaje) { mejorPuntaje = p; mejor = s; }
            }
            return mejor;
        }

        // En pleno combate: cada 0.5 s se revisa si aparecio un blanco
        // claramente mejor que el actual (una orden de atacar explicita no se
        // cambia).
        void TickRetarget(float dt)
        {
            if (orderIsAttack || target == null) return;
            if (State != AiState.Chase && State != AiState.Attack) return;
            relojRetarget += dt;
            if (relojRetarget < 0.5f) return;
            relojRetarget = 0f;

            var mejor = MejorObjetivoVisible();
            if (mejor == null || mejor == target) return;
            if (PuntajeDeObjetivo(mejor) + 4f >= PuntajeDeObjetivo(target)) return;
            target = mejor;
            segundosSinLineaDeTiro = 0f;
        }
    }
}
