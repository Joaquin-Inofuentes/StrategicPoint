using UnityEngine;
using SP.Actors;
using SP.Combat;

namespace SP.Ai
{
    // Ronda 13 (punto 1): "el companero revivido no me sigue ni obedece ninguna orden".
    //
    // Habia cuatro caminos para revivir (tecla [E], habilidad del medico, PedidoDeCuracion, RescateAutomatico y el
    // EstadoDePartida en calma) y cada uno reponia solo la vida: el cerebro conservaba el estado con el que murio.
    // Se detectaron estas causas (cualquiera basta para que "no obedezca"):
    //   - IsPossessedByPlayer seguia en true en el cuerpo que el jugador manejaba al morir y luego paso a RTS: Tick()
    //     sale antes de hacer nada, el soldado esta vivo pero inerte;
    //   - Quieto seguia en true (murio cargando una demolicion o tras "todos quietos"): TickSeguirAlJugador no lo mueve
    //     y su postura quedo en Defensiva;
    //   - la orden, el objetivo, la ruta y la cobertura de antes de morir seguian puestos;
    //   - SelectionController lo habia sacado de la seleccion al morir y no volvia a entrar (las ordenes de RTS a "la
    //     seleccion" no le llegaban).
    // Reanimacion.Ejecutar es ahora el UNICO camino y llama a esto.
    public partial class AiBrain
    {
        // soltarPosesion: el jugador ya no esta manejando este cuerpo (paso a RTS o a otro soldado).
        public void ResetearTrasRevivir(bool soltarPosesion)
        {
            if (!bootstrapped) Bootstrap();
            if (self == null) return;

            if (soltarPosesion && IsPossessedByPlayer) IsPossessedByPlayer = false;

            NuevaOrden();                    // suelta la cobertura, "seguir solo" y Quieto (y con eso restaura la postura)
            if (self.Role != RoleType.Civilian) Pasivo = false;

            hasOrder = false;
            orderIsAttack = false;
            mountTarget = null;
            followTarget = null;
            followOffsetLocal = Vector3.zero;
            attackMoveDestination = null;
            orderQueue.Clear();
            ClearPath();
            SoltarCobertura();
            target = null;
            sensedTarget = null;
            segundosSinLineaDeTiro = 0f;
            agentTieneDestino = false;
            reaccionPendiente = false;
            rafagaRestante = 0;
            pausaRestante = 0f;

            if (self.Health != null && self.Health.IsAlive) SetState(AiState.Patrol);
            forceSense = true;

            if (!IsPossessedByPlayer && gameObject.activeInHierarchy) ReactivarNavegacion();
        }
    }
}
