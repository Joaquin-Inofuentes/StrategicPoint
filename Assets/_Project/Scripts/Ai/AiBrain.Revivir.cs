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

        Soldier targetCuracion;
        float progresoCuracion;

        public void CancelarCuracionLocal()
        {
            if (targetCuracion != null)
            {
                SP.Player.AccionesEnCurso.Terminar(self);
                targetCuracion = null;
            }
            progresoCuracion = 0f;
        }

        bool EsHeridoOCaidoLocal(Soldier s)
        {
            if (s == null || s.Health == null) return false;
            if (!s.Health.IsAlive) return true;
            return s.Health.Current < s.Health.MaxHealth;
        }

        void BuscarTargetCuracionLocal()
        {
            CancelarCuracionLocal();
            Soldier mejor = null;
            float mejorDist = SP.Player.PedidoDeCuracion.AlcanceDeCuracion;
            foreach (var a in SP.Core.ActorRegistry.All)
            {
                if (a == self || a.Team != self.Team) continue;
                if (!a.gameObject.activeInHierarchy) continue;
                if (!EsHeridoOCaidoLocal(a)) continue;

                float d = Vector3.Distance(transform.position, a.transform.position);
                if (d <= mejorDist)
                {
                    mejor = a;
                    mejorDist = d;
                }
            }
            if (mejor != null)
            {
                targetCuracion = mejor;
            }
        }

        void TickCuracionLocal(float dt)
        {
            if (self.Role != RoleType.Medic) return;

            if (Pasivo || MontadoEnVehiculo || State == AiState.Chase || State == AiState.Attack || State == AiState.MovingToAttackOrder || target != null) 
            {
                CancelarCuracionLocal();
                return;
            }

            if (targetCuracion == null || !targetCuracion.gameObject.activeInHierarchy || (!EsHeridoOCaidoLocal(targetCuracion)))
            {
                BuscarTargetCuracionLocal();
            }

            if (targetCuracion != null)
            {
                float dist = Vector3.Distance(transform.position, targetCuracion.transform.position);
                if (dist > SP.Player.PedidoDeCuracion.AlcanceDeCuracion)
                {
                    CancelarCuracionLocal();
                    return;
                }

                if (!targetCuracion.Health.IsAlive)
                {
                    progresoCuracion += dt;
                    SP.Player.AccionesEnCurso.Reportar(self, "REVIVIENDO", targetCuracion.transform.position, progresoCuracion / SP.Player.PedidoDeCuracion.SegundosDeReanimar, SP.Player.PedidoDeCuracion.SegundosDeReanimar - progresoCuracion, targetCuracion.transform);
                    if (progresoCuracion >= SP.Player.PedidoDeCuracion.SegundosDeReanimar)
                    {
                        SP.Player.Reanimacion.Ejecutar(targetCuracion);
                        SP.Presentation.Feedback.Accion(SP.Core.SfxKind.Revive, "¡" + targetCuracion.DisplayName.ToUpperInvariant() + " DE VUELTA!", targetCuracion.transform.position, SP.Presentation.Feedback.Ok, aviso: true, pulso: true, volumen: 0.9f);
                        CancelarCuracionLocal();
                    }
                }
                else
                {
                    progresoCuracion += SP.Player.PedidoDeCuracion.CuracionPorSegundo * dt;
                    int curacion = Mathf.FloorToInt(progresoCuracion);
                    if (curacion > 0)
                    {
                        progresoCuracion -= curacion;
                        targetCuracion.Health.Heal(curacion);
                    }
                    float rest = targetCuracion.Health.MaxHealth - targetCuracion.Health.Current;
                    SP.Player.AccionesEnCurso.Reportar(self, "CURANDO", targetCuracion.transform.position, (float)targetCuracion.Health.Current / targetCuracion.Health.MaxHealth, rest / (float)SP.Player.PedidoDeCuracion.CuracionPorSegundo, targetCuracion.transform);
                    
                    if (targetCuracion.Health.Current >= targetCuracion.Health.MaxHealth)
                    {
                        SP.Presentation.Feedback.Accion(SP.Core.SfxKind.HealDone, "¡" + targetCuracion.DisplayName.ToUpperInvariant() + " CURADO!", targetCuracion.transform.position, SP.Presentation.Feedback.Ok, aviso: true, pulso: true, volumen: 0.8f);
                        CancelarCuracionLocal();
                    }
                }
            }
        }
    }
}
