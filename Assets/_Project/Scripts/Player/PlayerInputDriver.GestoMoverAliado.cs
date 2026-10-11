using UnityEngine;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Player
{
    // PlayerInputDriver (parte): bug #205 ("si mantengo sobre un aliado y mantengo apretada la barra espaciadora y muevo el cursor, es para decirle que
    // vaya a donde suelte la barra espaciadora con el cursor"). Funciona igual en FPS (la mira) y en RTS (el cursor).
    //   - Apretar [Espacio] con la mira/cursor sobre un aliado vivo lo "agarra".
    //   - Mientras se mantiene, se mueve la mira/cursor: el aviso recuerda a quien se esta llevando.
    //   - Al soltar, el aliado va al piso que quede bajo la mira/cursor (a un lugar libre cercano si cae sobre un solido).
    public partial class PlayerInputDriver
    {
        public const float SegundosMinimosDelGestoDeMover = 0.12f;
        Soldier gestoAliado;
        float gestoDesde;

        public bool GestoDeMoverAliadoEnCurso => gestoAliado != null;

        void TickGestoMoverAliado(Keyboard kb, AimResult aim, Ray rayo)
        {
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame && gestoAliado == null)
            {
                var yo = Brain != null ? Brain.Current : null;
                if (aim.Type == AimTargetType.Ally && aim.Soldier != null && aim.Soldier != yo && aim.Soldier.Health != null && aim.Soldier.Health.IsAlive
                    && aim.Soldier.Team == TeamId.Player && !PedidoDeCuracion.CurandoAlJugador(yo))
                {
                    gestoAliado = aim.Soldier; gestoDesde = Time.time;
                    Avisar("LLEVA A " + gestoAliado.DisplayName.ToUpperInvariant() + ": MOVE LA MIRA Y SOLTA [ESPACIO]");
                }
            }
            if (gestoAliado == null) return;

            if (gestoAliado.Health == null || !gestoAliado.Health.IsAlive) { gestoAliado = null; return; }
            if (kb.spaceKey.isPressed) return;

            // Soltado: se emite la orden.
            var s = gestoAliado; gestoAliado = null;
            if (Time.time - gestoDesde < SegundosMinimosDelGestoDeMover) return;
            if (!AimTargeting.PisoBajoRayo(rayo, out var piso)) { RejectOrder("NO HAY PISO AHI"); return; }
            if (!OrderService.TryResolveDestination(piso, out var destino)) { RejectOrder("DESTINO BLOQUEADO"); return; }
            OrderService.IssueMoveOrder(s, destino);
            Avisar(s.DisplayName.ToUpperInvariant() + " VA AHI");
            Feedback_OrdenConfirmada(destino);
            SesionLog.Orden("MOVER (gesto)", s, destino.ToString("0.0"));
        }
    }
}
