using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.UI;

namespace SP.Player
{
    // PlayerInputDriver (parte): la orden CONTEXTUAL de un toque de [Q] y el envio de aliados a interactuar.
    //
    // Pedido explicito: "Q sola: si es terreno que te sigan, si es enemigo atacar, si es aliado herido curar, si es destruible
    // destruir, etc., y solo apuntarle al interactuable y apretar Q y que haga el resto".
    public partial class PlayerInputDriver
    {
        // El interactuable ordenable que se tiene en la mira (panel, computadora...), o null.
        public IOrdenableAAliados OrdenableEnLaMira(AimResult aim)
        {
            if (aim.HitTransform == null) return null;
            return aim.HitTransform.GetComponentInParent<IOrdenableAAliados>();
        }

        // Manda al aliado que corresponde (sub 0 = el mejor de los libres; 1..3 = ese soldado de la escuadra) a operar 'o'.
        public bool EnviarAInteractuar(IOrdenableAAliados o, int sub)
        {
            if (o == null) return false;
            var yo = Brain != null ? Brain.Current : null;
            if (o.Completo) { RejectOrder("YA ESTA HECHO"); return false; }
            if (!o.Habilitado) { RejectOrder("TODAVIA NO SE PUEDE"); return false; }

            var dest = DestinatariosPorSub(sub, out string quien);
            var operador = o.ElegirOperador(dest);
            if (operador == null)
            {
                if (sub > 0 && dest.Count > 0) { RejectOrder($"SOLO EL {o.QuienDebe} PUEDE"); return false; }
                if (yo != null && o.PuedeOperar(yo)) { RejectOrder($"VOS SOS EL {o.QuienDebe}: ACERCATE Y MANTEN [E]"); return false; }
                RejectOrder($"NADIE VIVO PUEDE ({o.QuienDebe}) · TAB PARA CAMBIAR DE SOLDADO");
                return false;
            }
            if (!o.EnviarA(operador, out string motivo)) { RejectOrder(motivo ?? "NO SE PUEDE"); return false; }
            Avisar($"{operador.DisplayName.ToUpperInvariant()} VA A {o.NombreParaOrden}");
            GameLog.Line($"Orden: {operador.DisplayName} va a {o.NombreParaOrden}");
            SesionLog.Orden("INTERACTUAR", operador, o.NombreParaOrden);
            Feedback_OrdenConfirmada(o.Raiz != null ? o.Raiz.position : operador.transform.position);
            return true;
        }

        void Feedback_OrdenConfirmada(Vector3 donde)
        {
            SP.Presentation.Feedback.Accion(SP.Presentation.SfxKind.RadialConfirm, null, donde, ColorDeCategoria[MenuDeOrdenes.Interactuar],
                aviso: false, pulso: true, volumen: 0.45f);
        }

        // La accion de un toque de [Q] segun lo apuntado. Devuelve el nombre de lo que hizo, o null si no hizo nada.
        public string AccionContextualDeQ(AimResult aim)
        {
            var yo = Brain != null ? Brain.Current : null;
            if (yo == null) return null;

            switch (aim.Type)
            {
                case AimTargetType.Caido:
                    if (aim.Soldier != null && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null && EjecutarOrdenRadial(MenuDeOrdenes.Curar, 3)) return "REVIVIR";
                    break;

                case AimTargetType.Interactuar:
                {
                    var o = OrdenableEnLaMira(aim);
                    if (o != null) return EnviarAInteractuar(o, 0) ? "INTERACTUAR" : null;
                    break;
                }

                case AimTargetType.Enemy:
                {
                    var dest = DestinatariosDeOrden();
                    if (aim.Soldier == null || dest.Count == 0) { RejectOrder("NADIE A QUIEN ORDENAR"); return null; }
                    OrderService.IssueAttackOrderForSelection(dest, aim.Soldier);
                    Avisar("ATAQUEN A " + aim.Soldier.DisplayName.ToUpperInvariant());
                    return "ATACAR";
                }

                case AimTargetType.Ally:
                    // Bug #054: "si le apunto a un aliado y aprieto y suelto Q en menos de 0,2 s quiero poseerlo, igual que los
                    // interactuables". El toque de Q sobre un aliado vivo lo posee (curarlo sigue en el radial: CURAR).
                    if (aim.Soldier != null && aim.Soldier != yo && aim.Soldier.Health != null && aim.Soldier.Health.IsAlive && TryPossess(aim.Soldier))
                    {
                        Avisar("AHORA SOS " + aim.Soldier.DisplayName.ToUpperInvariant());
                        return "POSEER";
                    }
                    break;

                case AimTargetType.Obstacle:
                case AimTargetType.Cubrirse:
                {
                    var m = Demolicion.MarcadorDeLaMira(aim);
                    if (m != null && Demolicion.EsDemolible(m, out _))
                    {
                        bool aliadoAsalto = DestinatariosDeOrden().Exists(d => d.Role == RoleType.Assault);
                        if (aliadoAsalto && EjecutarOrdenRadial(MenuDeOrdenes.Demoler, 0)) return "DEMOLER";
                        if (yo.Role == RoleType.Assault) { Avisar("SOS EL ASALTO: ACERCATE Y MANTEN [E] QUIETO FRENTE AL MURO"); return "DEMOLER_YO"; }
                        RejectOrder("NECESITAS UN SOLDADO DE ASALTO PARA DEMOLER");
                        return null;
                    }
                    break;
                }

                case AimTargetType.Torreta:
                    if (aim.Torreta != null && aim.Torreta.Libre && EjecutarOrdenRadial(MenuDeOrdenes.Torreta, 2)) return "TORRETA";
                    return null;

                case AimTargetType.Vehicle:
                    // Bug #071: Q sobre un vehiculo ENEMIGO manda a los aliados a atacarlo (el de Asalto saca el lanzacohetes).
                    if (aim.Vehicle != null && aim.Vehicle.Hostil(TeamId.Player))
                    {
                        var dest = DestinatariosDeOrden();
                        if (dest.Count == 0) { RejectOrder("NADIE A QUIEN ORDENAR"); return null; }
                        if (OrderService.IssueAttackVehicleOrderForSelection(dest, aim.Vehicle) == 0) { RejectOrder("NADIE PUEDE ATACARLO"); return null; }
                        Avisar("ATAQUEN AL VEHICULO ENEMIGO");
                        return "ATACAR VEHICULO";
                    }
                    if (aim.Vehicle != null && !aim.Vehicle.IsDestroyed && aim.Vehicle.Bando == TeamId.Player && EjecutarOrdenRadial(MenuDeOrdenes.Tanque, 0)) return "SUBIR AL TANQUE";
                    return null;
            }

            // Terreno (o cualquier otra cosa sin accion propia): que TODOS te sigan.
            if (SeguirTodosSiOSi() > 0) return "SEGUIR TODOS";
            RejectOrder("NADIE A QUIEN ORDENAR");
            return null;
        }
    }
}
