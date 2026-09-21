using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Interaction;
using SP.Vehicles;
using SP.UI;
using SP.Core;

namespace SP.Player
{
    // PlayerInputDriver (parte): detector central de IInteractable.
    //
    // Archivo NUEVO y aditivo: no reemplaza nada de PlayerInputDriver.Radial.cs,
    // solo agrega un paso previo que ResolverGestoDeQ consulta en el TAP de [Q]
    // (ver el cambio quirurgico de dos lineas en ese archivo). Si hay un
    // IInteractable valido en la mira o pegado al jugador, un TAP de [Q] lo
    // dispara; si no hay ninguno, [Q] sigue comportandose exactamente igual que
    // antes (ciclar aliado / abrir el radial de ordenes).
    //
    // A la fecha ningun objeto del proyecto implementa IInteractable todavia
    // (ver comentario en IInteractable.cs), asi que este detector hoy nunca
    // encuentra nada y el TAP de [Q] cae siempre al comportamiento previo: queda
    // listo para el proximo interactuable que se agregue.
    public partial class PlayerInputDriver
    {
        // Reusa el mismo radio que ya usan pickups/vehiculo/torreta (interactRadius,
        // ver el campo en PlayerInputDriver.cs): un IInteractable pegado al jugador
        // se detecta al mismo alcance que el resto de las interacciones por [E],
        // en vez de inventar otra distancia que hay que sincronizar aparte.
        // Busca primero en lo que se esta apuntando (raycast de mira), y si no
        // hay nada ahi, en lo mas cercano dentro de interactRadius.
        public IInteractable ObjetivoInteractuable()
        {
            var aim = UltimaMira;
            if (aim.HitTransform != null)
            {
                var enMira = aim.HitTransform.GetComponentInParent<IInteractable>();
                if (enMira != null && enMira.CanInteract(this)) return enMira;
            }

            if (Brain == null || Brain.Current == null) return null;
            var origen = Brain.Current.transform.position;
            var candidatos = Physics.OverlapSphere(origen, interactRadius, ~0, QueryTriggerInteraction.Collide);
            IInteractable mejor = null;
            float mejorDist = float.MaxValue;
            foreach (var c in candidatos)
            {
                var it = c.GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract(this)) continue;
                float d = Vector3.Distance(origen, c.transform.position);
                if (d < mejorDist) { mejorDist = d; mejor = it; }
            }
            return mejor;
        }

        // Prompt de UI consistente: "[Q] " + GetPrompt(). Se muestra desde el
        // mismo lugar que ya arma el texto de instrucciones (BuildFpsInstruction
        // lo puede anteponer llamando a esto).
        public string PromptDeInteraccion()
        {
            var it = ObjetivoInteractuable();
            return it == null ? null : "[Q] " + it.GetPrompt(this);
        }

        // Ronda 12: el TOQUE de [Q] y el [Q] SOSTENIDO ya no dan lo mismo sobre lo apuntado. Sostenido abre el radial completo
        // (elegis vos); el toque ejecuta AL INSTANTE la accion natural para lo que tenes en la mira, sin menu:
        //   enemigo -> los tuyos lo atacan | aliado herido -> el medico lo atiende | caido -> el medico lo revive
        //   aliado sano -> ese soldado te sigue | tanque aliado con lugar -> subir todos | nada -> "SIGANME" a la escuadra.
        // Devuelve el texto de lo que hizo (para la suite y el tutorial) o null si cayo al fallback de seguir.
        public string UltimaAccionRapida { get; private set; }

        void AccionRapidaDeQ()
        {
            UltimaAccionRapida = null;
            var aim = ultimoResultadoDeMira;
            var yo = Brain != null ? Brain.Current : null;
            bool hecho = false;
            switch (aim.Type)
            {
                case AimTargetType.Enemy:
                    if (aim.Soldier != null && aim.Soldier.Health != null && aim.Soldier.Health.IsAlive)
                    { hecho = EjecutarOrdenRadial(MenuDeOrdenes.Atacar, 0); UltimaAccionRapida = "ATACAR"; }
                    break;
                case AimTargetType.Caido:
                    if (aim.Soldier != null && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null)
                    { hecho = EjecutarOrdenRadial(MenuDeOrdenes.Curar, 3); UltimaAccionRapida = "REVIVIR"; }
                    break;
                case AimTargetType.Ally:
                    if (aim.Soldier != null && aim.Soldier != yo)
                    {
                        if (Herido(aim.Soldier) && PedidoDeCuracion.MedicoDisponible(aim.Soldier) != null)
                        { hecho = EjecutarOrdenRadial(MenuDeOrdenes.Curar, 2); UltimaAccionRapida = "CURAR"; }
                        else if (yo != null)
                        {
                            OrderService.IssueFollowOrderForSelection(new List<Soldier> { aim.Soldier }, yo);
                            Avisar(aim.Soldier.DisplayName.ToUpperInvariant() + " TE SIGUE");
                            hecho = true; UltimaAccionRapida = "SEGUIR ALIADO";
                        }
                    }
                    break;
                case AimTargetType.Vehicle:
                    if (aim.Vehicle != null && !aim.Vehicle.IsDestroyed && aim.Vehicle.Bando == TeamId.Player && aim.Vehicle.HasAnyRoom && !currentSeat.HasValue)
                    { hecho = EjecutarOrdenRadial(MenuDeOrdenes.Tanque, 0); UltimaAccionRapida = "SUBIR TODOS"; }
                    break;
            }
            if (!hecho) { UltimaAccionRapida = null; SeguirAlPoseido(); }
        }

        // Llamado desde el TAP de [Q] (ResolverGestoDeQ). Devuelve true si
        // encontro y ejecuto un interactuable -- en ese caso el llamador NO debe
        // hacer tambien el comportamiento previo (ciclar aliado / radial).
        public bool TryInteractuarConMira()
        {
            var it = ObjetivoInteractuable();
            if (it == null) return false;
            it.Interact(this);
            return true;
        }
    }
}
