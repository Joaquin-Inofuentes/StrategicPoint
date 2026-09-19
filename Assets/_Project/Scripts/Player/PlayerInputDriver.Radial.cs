using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.CameraSystem;
using SP.Core;
using SP.UI;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    // PlayerInputDriver (parte): menu radial de [Q] y ejecucion de las ordenes de escuadra.
    public partial class PlayerInputDriver
    {
        // -----------------------------------------------------------
        // Menu de ordenes: [Q] sostenido
        // -----------------------------------------------------------

        // Umbral que separa los dos gestos de la MISMA tecla. 0,3 s es el
        // valor por defecto de KeyBindings: bastante mas que un toque
        // deliberado y bastante menos que "lo dejo apretado".
        public const float SostenerParaMenu = 0.3f;

        // Un unico lugar que decide, cada frame, cual de los dos gestos de
        // [Q] esta ocurriendo. Estan juntos a proposito: separarlos en dos
        // ifs sueltos es como se cuelan los casos en que los dos disparan
        // en el mismo frame.
        void ActualizarMenuDeOrdenes()
        {
            ResolverGestoDeQ(
                KeyBindings.WasTapped(KeyBindings.CiclarPosesion, SostenerParaMenu),
                KeyBindings.IsHeld(KeyBindings.CiclarPosesion, SostenerParaMenu),
                KeyBindings.IsPressed(KeyBindings.CiclarPosesion));
        }

        // La decision, separada de la lectura del teclado. Los dos gestos de
        // [Q] se deciden mirando los mismos tres booleanos, asi que se
        // pueden ejercer sin un teclado: la suite corre sin ninguno
        // (Keyboard.current es null) y con la lectura pegada aca adentro la
        // rama del toque corto no tenia forma de probarse.
        public void ResolverGestoDeQ(bool toque, bool sostenido, bool sigueApretada)
        {
            if (OrdenesMenu == null)
            {
                if (toque) CycleLivingAlly(+1);
                return;
            }

            if (sostenido && !OrdenesMenu.Abierto) AbrirRadial();

            if (OrdenesMenu.Abierto)
            {
                int elegida = MenuDeOrdenes.LeerTecla();
                if (elegida > 0)
                {
                    int cat = OrdenesMenu.CategoriaDeTecla(elegida);
                    OrdenesMenu.Cerrar();
                    if (OrdenesMenu.EsRadial) { if (cat >= 0) EjecutarOrdenRadial(cat, PrimeraOpcionVisible(cat)); }
                    else EjecutarOrdenDelMenu(elegida);
                    aimCongelado = null;
                    return;
                }
                // Se cierra al soltar. El toque corto no puede llegar aca (para abrirse ya hubo
                // que pasar el umbral), asi que soltar despues de mantener nunca cicla de soldado.
                // Al soltar se ejecuta lo resaltado: la opcion del anillo exterior, o la primera
                // opcion de la categoria si el cursor no salio del anillo interior. Cursor en el
                // centro o en una zona vacia del anillo exterior = cancelar.
                if (!sigueApretada)
                {
                    int cat = OrdenesMenu.Seleccion, sub = OrdenesMenu.Sub;
                    bool afuera = OrdenesMenu.EnAnilloExterior;
                    OrdenesMenu.Cerrar();
                    bool intento = OrdenesMenu.EsRadial && cat >= 0 && (sub >= 0 || !afuera);
                    if (intento)
                    {
                        if (sub >= 0) EjecutarOrdenRadial(cat, sub);
                        else EjecutarOrdenRadial(cat, PrimeraOpcionVisible(cat));
                    }
                    else AudioDirector.PlayUi2D(SfxKind.RadialCancel, 0.5f, 0.8f);   // soltar sin elegir = cancelar (se oye)
                    aimCongelado = null;
                }
                return;
            }

            if (toque) CycleLivingAlly(+1);
        }

        // Punto al que apuntaba el jugador cuando abrio el radial: el mouse se usa para
        // elegir en el menu, asi que "alli" no puede ser donde termina el cursor.
        AimResult? aimCongelado;

        public bool RadialAbierto => OrdenesMenu != null && OrdenesMenu.Abierto;

        void AbrirRadial()
        {
            string Clase(int i) => Squad != null && i < Squad.Count && Squad[i] != null ? Squad[i].ClassNameTitulo : null;
            OrdenesMenu.PonerSoldados(Clase(0), Clase(1), Clase(2));
            aimCongelado = ultimoResultadoDeMira;
            OrdenesMenu.Abrir(ConstruirContextoRadial(aimCongelado.Value));
        }

        // A quien le hablan las ordenes del menu: a la seleccion de RTS si
        // hay alguien seleccionado, y si no a la escuadra viva entera. En
        // FPS la seleccion suele estar vacia y una orden que no le llega a
        // nadie se lee como que el menu no funciona.
        public List<Soldier> DestinatariosDeOrden()
        {
            var lista = new List<Soldier>();
            if (Selection != null && Selection.Selected.Count > 0)
            {
                foreach (var s in Selection.Selected)
                    if (s != null && s.Health != null && s.Health.IsAlive) lista.Add(s);
                if (lista.Count > 0) return lista;
            }
            if (Squad != null)
                foreach (var s in Squad)
                    if (s != null && s.Health != null && s.Health.IsAlive
                        && !OrderService.LoManejaElJugador(s) && s != Brain.Current) lista.Add(s);
            return lista;
        }

        // opcion es 1..5, en el mismo orden que MenuDeOrdenes.Opciones.
        // Publico para que la suite pueda ejercerlo sin teclado.
        public bool EjecutarOrdenDelMenu(int opcion)
        {
            var destinatarios = DestinatariosDeOrden();
            Vector3 centro = Brain != null && Brain.Current != null
                ? Brain.Current.transform.position : transform.position;
            Vector3 frente = Rig != null && Rig.Cam != null
                ? Vector3.ProjectOnPlane(Rig.Cam.transform.forward, Vector3.up).normalized
                : Vector3.forward;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;

            switch (opcion)
            {
                case 1:
                    if (destinatarios.Count == 0) break;
                    OrderService.IssueFormationOrderForSelection(destinatarios, centro + frente * 4f, frente, FormationKind.Linea);
                    Avisar("FORMACION EN LINEA");
                    return true;
                case 2:
                    if (destinatarios.Count == 0) break;
                    OrderService.IssueFormationOrderForSelection(destinatarios, centro + frente * 4f, frente, FormationKind.Cuna);
                    Avisar("FORMACION EN CUÑA");
                    return true;
                case 3:
                    if (destinatarios.Count == 0 || Brain == null || Brain.Current == null) break;
                    OrderService.IssueFollowOrderForSelection(destinatarios, Brain.Current);
                    Avisar("SIGANME");
                    return true;
                case 4:
                    if (destinatarios.Count == 0) break;
                    foreach (var s in destinatarios) s.Brain?.CancelOrder();
                    OrderMarkerFx.ClearQueuedMarkers();
                    Avisar("ALTO");
                    return true;
                case 5:
                    var herido = Brain != null ? Brain.Current : null;
                    if (herido == null) break;
                    if (PedidoDeCuracion.Solicitar(herido)) { Avisar("ENFERMERO EN CAMINO"); return true; }
                    Avisar(herido.Health != null && herido.Health.Current >= herido.Health.MaxHealth
                        ? "NO HACE FALTA" : "NO HAY QUIEN ATIENDA");
                    return false;
            }
            OrderService.PlayRejectSound();
            Avisar("NADIE A QUIEN ORDENAR");
            return false;
        }

        // Punto del mundo "alli": lo apuntado con la mira (piso, obstaculo, enemigo, aliado
        // o vehiculo) o, si no se apunta a nada, 14 m al frente del soldado.
        Vector3 PuntoApuntadoParaOrdenes(AimResult r)
        {
            if (r.Type == AimTargetType.Ground || r.Type == AimTargetType.Obstacle) return r.Point;
            if ((r.Type == AimTargetType.Enemy || r.Type == AimTargetType.Ally) && r.Soldier != null) return r.Soldier.transform.position;
            if (r.Type == AimTargetType.Vehicle && r.Vehicle != null) return r.Vehicle.transform.position;
            var yo = Brain != null && Brain.Current != null ? Brain.Current.transform.position : transform.position;
            var frente = Rig != null && Rig.Cam != null ? Vector3.ProjectOnPlane(Rig.Cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            return yo + frente * 14f;
        }

        // Radial de [Q] por capas: categoria (anillo interior) y opcion (anillo exterior).
        // Publico para que la suite y las pruebas de juego puedan ejercerlo sin teclado.
        //   0 IR ALLI          1 CUBRIRSE (segun donde miro)   2 ATACAR      3 POSICION
        //   4 CURAR            5 TANQUE                         6 POSEER      7 DEMOLER
        // Aviso de cada orden radial que salio bien (categoria, opcion): lo escucha el tutorial.
        public event System.Action<int, int> OrdenRadialEjecutada;

        public bool EjecutarOrdenRadial(int categoria, int sub = 0)
        {
            var aim = aimCongelado ?? ultimoResultadoDeMira;
            bool ok = EjecutarOrdenRadialInterno(categoria, sub);
            if (ok)
            {
                ConfirmarOrdenRadial(categoria, sub, aim);
                OrdenRadialEjecutada?.Invoke(categoria, sub);
            }
            return ok;
        }

        public static readonly Color[] ColorDeCategoria =
        {
            new Color(0.35f, 0.75f, 1f),   // 0 IR ALLI
            new Color(0.30f, 0.80f, 1f),   // 1 CUBRIRSE
            new Color(1f, 0.35f, 0.30f),   // 2 ATACAR
            new Color(0.85f, 0.85f, 0.95f),// 3 POSICION
            new Color(0.35f, 0.95f, 0.5f), // 4 CURAR
            new Color(1f, 0.82f, 0.25f),   // 5 TANQUE
            new Color(0.75f, 0.55f, 1f),   // 6 POSEER
            new Color(1f, 0.6f, 0.2f),     // 7 DEMOLER
            new Color(1f, 0.82f, 0.25f),   // 8 TORRETA
        };

        // Toda orden del radial confirma con un "ping" de interfaz, el nombre de la opcion flotando en el
        // mundo y un pulso de color de la categoria en el punto de la accion (lo apuntado, o el soldado).
        void ConfirmarOrdenRadial(int categoria, int sub, AimResult aim)
        {
            if (categoria < 0 || categoria >= ColorDeCategoria.Length) return;
            var color = ColorDeCategoria[categoria];
            var donde = (categoria == 6 || (categoria == 4 && sub == 0)) ? (Brain.Current != null ? Brain.Current.transform.position : transform.position)
                                                                       : PuntoApuntadoParaOrdenes(aim);
            string nombre = MenuDeOrdenes.NombreDeOpcion(categoria, sub);
            Feedback.Accion(SfxKind.RadialConfirm, string.IsNullOrEmpty(nombre) ? null : nombre, donde, color, aviso: false, pulso: true, volumen: 0.45f);
        }

        bool EjecutarOrdenRadialInterno(int categoria, int sub)
        {
            var aim = aimCongelado ?? ultimoResultadoDeMira;
            var yo = Brain != null ? Brain.Current : null;
            string quien;
            switch (categoria)
            {
                case 0: // IR ALLI
                {
                    var dest = DestinatariosPorSub(sub, out quien);
                    if (dest.Count == 0) { RejectOrder(sub <= 0 ? "NADIE A QUIEN ORDENAR" : quien); return false; }
                    var punto = PuntoApuntadoParaOrdenes(aim);
                    var puntos = OrderService.FormationPoints(punto, dest.Count);
                    for (int i = 0; i < dest.Count; i++) OrderService.IssueMoveOrder(dest[i], puntos[i]);
                    Avisar(sub <= 0 ? "TODOS ALLI" : quien + " VA");
                    GameLog.Line($"Radial: {dest.Count} aliados van a {punto}");
                    return true;
                }
                case 1: // CUBRIRSE segun hacia donde miro
                {
                    var dest = DestinatariosPorSub(sub, out quien);
                    if (dest.Count == 0) { RejectOrder(sub <= 0 ? "NADIE A QUIEN ORDENAR" : quien); return false; }
                    var origen = yo != null ? yo.transform.position : transform.position;
                    var dir = DireccionDeMirada(aim);
                    int n = OrdenesDeEscuadra.CubrirSegunMirada(dest, origen, dir, out var primero);
                    if (n == 0 && TryResolverCobertura(aim, out var cobertura, out var dueno))
                    {
                        var lateral = Vector3.Cross(Vector3.up, dir).normalized;
                        for (int i = 0; i < dest.Count; i++)
                            if (OrderService.IssueCoverOrder(dest[i], cobertura + lateral * ((i - (dest.Count - 1) * 0.5f) * 1.6f), dueno)) n++;
                        primero = cobertura;
                    }
                    if (n == 0) { RejectOrder("NO HAY COBERTURA HACIA AHI"); return false; }
                    Avisar(sub <= 0 ? "A CUBIERTO" : quien + " SE CUBRE");
                    GameLog.Line($"Radial: {n} aliados se cubren mirando hacia {dir} (cobertura en {primero})");
                    return true;
                }
                case 2: // ATACAR
                {
                    var dest = DestinatariosPorSub(sub, out quien);
                    Soldier objetivo = aim.Type == AimTargetType.Enemy ? aim.Soldier : null;
                    if (objetivo == null && yo != null)
                        objetivo = ActorRegistry.FindNearest(yo.transform.position, s => s.Team == TeamId.Enemy && s.Health != null && s.Health.IsAlive && (s.transform.position - yo.transform.position).sqrMagnitude < 100f * 100f);
                    if (objetivo == null || dest.Count == 0) { RejectOrder(dest.Count == 0 ? (sub <= 0 ? "NADIE A QUIEN ORDENAR" : quien) : "NO HAY A QUIEN ATACAR"); return false; }
                    OrderService.IssueAttackOrderForSelection(dest, objetivo);
                    Avisar((sub <= 0 ? "ATAQUEN A " : quien + " ATACA A ") + objetivo.DisplayName.ToUpperInvariant());
                    return true;
                }
                case 3: // POSICION
                {
                    var dest = DestinatariosDeOrden();
                    switch (sub)
                    {
                        case 0:
                        {
                            int n = OrdenesDeEscuadra.Quietos(dest);
                            if (n == 0) { RejectOrder("NADIE A QUIEN ORDENAR"); return false; }
                            Avisar("TODOS QUIETOS");
                            GameLog.Line($"Radial: {n} aliados quietos");
                            return true;
                        }
                        case 1: return EjecutarOrdenDelMenu(3);
                        case 2: return EjecutarOrdenDelMenu(1);
                        case 3: return EjecutarOrdenDelMenu(2);
                        default:
                            if (dest.Count == 0) { RejectOrder("NADIE A QUIEN ORDENAR"); return false; }
                            OrderService.IssueRetreatOrderForSelection(dest);
                            Avisar("RETIRADA");
                            return true;
                    }
                }
                case 4: // CURAR
                    return OrdenDeCuracion(sub, aim);
                case 5: // TANQUE
                    return OrdenDeTanque(sub, aim);
                case 8: // TORRETA FIJA
                    return OrdenDeTorreta(sub, aim);
                case 6: // POSEER
                {
                    if (sub == 4)
                    {
                        if (aim.Type != AimTargetType.Ally || aim.Soldier == null) { RejectOrder("APUNTA A UN ALIADO"); return false; }
                        return TryPossess(aim.Soldier);
                    }
                    if (sub >= 3) { CycleLivingAlly(+1); return true; }
                    var s = SoldadoDeEscuadra(sub);
                    if (s == null || s == yo) { RejectOrder(s == yo ? "YA SOS ESE SOLDADO" : "NO HAY SOLDADO " + (sub + 1)); return false; }
                    return TryPossess(s);
                }
                case 7: // DEMOLER
                    return OrdenDeDemolicion(sub, aim);
            }
            return false;
        }

        Soldier SoldadoDeEscuadra(int n) => Squad != null && n >= 0 && n < Squad.Count ? Squad[n] : null;

        // sub 0 = todos los aliados libres; 1..3 = solo ese soldado de la escuadra.
        List<Soldier> DestinatariosPorSub(int sub, out string aviso)
        {
            aviso = "TODOS";
            if (sub <= 0) return DestinatariosDeOrden();
            var lista = new List<Soldier>();
            var s = SoldadoDeEscuadra(sub - 1);
            var yo = Brain != null ? Brain.Current : null;
            if (s == null || s.Health == null || !s.Health.IsAlive) { aviso = "EL " + sub + " NO ESTA DISPONIBLE"; return lista; }
            if (s == yo) { aviso = "VOS SOS EL " + sub; return lista; }
            lista.Add(s);
            aviso = "SOLO " + s.DisplayName.ToUpperInvariant();
            return lista;
        }

        // Hacia donde "mira" el jugador: la camara en FPS; en RTS, del soldado hacia el punto
        // apuntado con el cursor.
        Vector3 DireccionDeMirada(AimResult aim)
        {
            var yo = Brain != null ? Brain.Current : null;
            Vector3 d = Vector3.zero;
            if (Rig != null && Rig.Mode == ControlMode.Fps && Rig.Cam != null)
                d = Vector3.ProjectOnPlane(Rig.Cam.transform.forward, Vector3.up);
            else if (yo != null && aim.Type != AimTargetType.None)
                d = Vector3.ProjectOnPlane(aim.Point - yo.transform.position, Vector3.up);
            if (d.sqrMagnitude < 0.01f && yo != null) d = yo.transform.forward;
            if (d.sqrMagnitude < 0.01f) d = Vector3.forward;
            return d.normalized;
        }

        bool OrdenDeCuracion(int sub, AimResult aim)
        {
            var yo = Brain != null ? Brain.Current : null;
            if (yo == null) return false;
            switch (sub)
            {
                case 0: // CURARME
                    if (yo.Role == RoleType.Medic)
                    {
                        if (PedidoDeCuracion.Botiquin(yo)) { Avisar("BOTIQUIN: TE CURAS"); return true; }
                        RejectOrder(yo.Health.Current >= yo.Health.MaxHealth ? "NO HACE FALTA" : "BOTIQUIN EN ESPERA");
                        return false;
                    }
                    if (PedidoDeCuracion.Solicitar(yo)) { Avisar("MEDICO EN CAMINO"); return true; }
                    RejectOrder(yo.Health.Current >= yo.Health.MaxHealth ? "NO HACE FALTA" : "NO HAY QUIEN ATIENDA");
                    return false;
                case 3: // REVIVIR al aliado caido que apunto
                {
                    if (aim.Type != AimTargetType.Caido || aim.Soldier == null) { RejectOrder("APUNTA A UN ALIADO CAIDO"); return false; }
                    bool okR = yo.Role == RoleType.Medic
                        ? PedidoDeCuracion.SolicitarReanimar(aim.Soldier, yo)
                        : PedidoDeCuracion.SolicitarReanimar(aim.Soldier);
                    if (!okR) { RejectOrder("NO HAY MEDICO VIVO PARA REANIMAR"); return false; }
                    Avisar(yo.Role == RoleType.Medic ? $"QUEDATE JUNTO A {aim.Soldier.DisplayName.ToUpperInvariant()} 4 s" : $"MEDICO VA A REANIMAR A {aim.Soldier.DisplayName.ToUpperInvariant()}");
                    return true;
                }
                case 1: // CURAR ALIADO (el mas herido)
                case 2: // CURAR AL APUNTADO
                {
                    Soldier herido = null;
                    if (sub == 2 && aim.Type == AimTargetType.Ally) herido = aim.Soldier;
                    if (herido == null)
                    {
                        float peor = 0.999f;
                        foreach (var a in ActorRegistry.All)
                        {
                            if (a == null || a == yo || a.Team != TeamId.Player || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                            if (a.Role == RoleType.Civilian) continue;
                            float f = (float)a.Health.Current / Mathf.Max(1, a.Health.MaxHealth);
                            if (f < peor) { peor = f; herido = a; }
                        }
                    }
                    if (herido == null) { RejectOrder("NADIE HERIDO"); return false; }
                    bool ok = yo.Role == RoleType.Medic
                        ? PedidoDeCuracion.Solicitar(herido, yo)
                        : PedidoDeCuracion.Solicitar(herido);
                    if (!ok) { RejectOrder(herido.Health.Current >= herido.Health.MaxHealth ? "NO HACE FALTA" : "NO HAY QUIEN ATIENDA"); return false; }
                    Avisar(yo.Role == RoleType.Medic ? $"ACERCATE A {herido.DisplayName.ToUpperInvariant()} PARA CURARLO" : $"MEDICO VA CON {herido.DisplayName.ToUpperInvariant()}");
                    return true;
                }
            }
            return false;
        }

        bool OrdenDeTanque(int sub, AimResult aim)
        {
            var yo = Brain != null ? Brain.Current : null;
            var v = FindTheVehicle();
            switch (sub)
            {
                case 0: // SUBIR TODOS
                {
                    if (v == null || v.IsDestroyed || !v.HasAnyRoom) { RejectOrder("NO HAY LUGAR EN EL TANQUE"); return false; }
                    var suben = DestinatariosDeOrden().FindAll(s => v.RoleOf(s) == null);
                    var civil = SP.Mision.MisionDirector.Activo ? SP.Mision.MisionDirector.Instancia.Civil : null;
                    if (civil != null && SP.Mision.MisionDirector.Instancia.CivilRescatado && civil.Health.IsAlive && civil.gameObject.activeInHierarchy && v.RoleOf(civil) == null) suben.Add(civil);
                    if (suben.Count == 0) { RejectOrder("NADIE PARA SUBIR"); return false; }
                    OrderService.IssueMountOrderForSelection(suben, v);
                    Avisar("TODOS AL TANQUE");
                    return true;
                }
                case 1: // BAJAR TODOS
                {
                    if (v == null || v.OccupantCount == 0) { RejectOrder("NADIE EN EL TANQUE"); return false; }
                    foreach (var o in new List<Soldier>(v.Occupants))
                        if (o != yo) v.Dismount(o);
                    Avisar("TODOS ABAJO");
                    GameLog.Line("Radial: bajar todos del tanque");
                    return true;
                }
                case 2: // TANQUE ALLI
                {
                    if (v == null) { RejectOrder("NO HAY TANQUE"); return false; }
                    var punto = PuntoApuntadoParaOrdenes(aim);
                    if (v.Driver == null) { RejectOrder("EL TANQUE NECESITA UN CONDUCTOR"); return false; }
                    if (!TryIssueVehicleMoveOrder(punto, v)) { RejectOrder("EL TANQUE YA VA / ESTA AHI"); return false; }
                    if (currentSeat == VehicleSeatRole.Driver) autoConduccion = true;
                    Avisar("TANQUE EN CAMINO");
                    return true;
                }
                case 3: // SUBIRME YO
                {
                    if (currentSeat.HasValue) { RejectOrder("YA ESTAS EN EL TANQUE"); return false; }
                    if (v == null || v.IsDestroyed || yo == null) { RejectOrder("NO HAY TANQUE"); return false; }
                    if (Vector3.Distance(yo.transform.position, v.transform.position) > interactRadius * 2f) { RejectOrder("ACERCATE AL TANQUE"); return false; }
                    EnterVehicle(v);
                    return currentSeat.HasValue;
                }
                default: // BAJARME YO
                    if (!currentSeat.HasValue) { RejectOrder("NO ESTAS EN EL TANQUE"); return false; }
                    ExitVehicle();
                    return true;
            }
        }

        bool OrdenDeDemolicion(int sub, AimResult aim)
        {
            var yo = Brain != null ? Brain.Current : null;
            if (sub == 2)
            {
                bool hubo = DemoledorAsalto.CancelarTodos();
                Avisar(hubo ? "DEMOLICION CANCELADA" : "NADA QUE CANCELAR");
                return hubo;
            }

            var marcador = Demolicion.MarcadorApuntado(aim.HitTransform, aim.Point);
            if (marcador == null) { RejectOrder("APUNTA A UN MURO O COBERTURA"); return false; }
            string motivo;
            if (!Demolicion.EsDemolible(marcador, out motivo)) { RejectOrder(motivo); return false; }

            if (sub == 1) // YO DEMUELO
            {
                if (yo == null || yo.Role != RoleType.Assault) { RejectOrder("SOLO EL ASALTO DEMUELE"); return false; }
                var d = yo.GetComponent<DemoledorAsalto>() ?? yo.gameObject.AddComponent<DemoledorAsalto>();
                if (!d.IniciarComoJugador(marcador, out motivo)) { RejectOrder(motivo); return false; }
                Avisar("QUEDATE QUIETO Y AGACHADO: DEMOLIENDO");
                return true;
            }

            // ASALTO DEMUELE: el soldado de asalto libre va, se agacha 4 s y lo vuela.
            Soldier asalto = null;
            foreach (var s in DestinatariosDeOrden())
                if (s.Role == RoleType.Assault) { asalto = s; break; }
            if (asalto == null) { RejectOrder(yo != null && yo.Role == RoleType.Assault ? "VOS SOS EL ASALTO: USA YO DEMUELO" : "NO HAY UN ASALTO LIBRE"); return false; }
            var dem = asalto.GetComponent<DemoledorAsalto>() ?? asalto.gameObject.AddComponent<DemoledorAsalto>();
            if (!dem.IniciarComoAliado(marcador, out motivo)) { RejectOrder(motivo); return false; }
            Avisar(asalto.DisplayName.ToUpperInvariant() + " VA A DEMOLER");
            return true;
        }

        void Avisar(string texto)
        {
            if (ModeToast != null) ModeToast.Show(texto);
        }

        void CycleLivingAlly(int direction)
        {
            if (Squad == null || Squad.Count == 0) return;
            if (direction == 0) direction = 1;
            int start = Squad.IndexOf(Brain.Current);
            for (int step = 1; step <= Squad.Count; step++)
            {
                var candidate = Squad[((start + step * direction) % Squad.Count + Squad.Count) % Squad.Count];
                if (candidate == null || candidate == Brain.Current) continue;
                // Ya no se salta a los montados en el vehiculo: TryPossess
                // ahora sabe tomar ese asiento en vez de rechazar.
                if (!candidate.Health.IsAlive) continue;
                TryPossess(candidate);
                return;
            }
            RejectOrder("NO QUEDAN ALIADOS VIVOS");
        }
    }
}
