using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Presentation
{
    // Condición de victoria: cuando todos los enemigos de la lista mueren
    // y todavía queda algún soldado propio vivo, muestra la pantalla de
    // victoria. La derrota la maneja PlayerInputDriver directamente (es
    // quien ya sabe cuándo no queda a quién poseer).
    //
    // Campos públicos (no privados + Bind()): a diferencia de las vistas
    // de UI de este proyecto, acá no hace falta el patrón de auto-sanado
    // en OnEnable -- un campo público SÍ se serializa con la escena y
    // sobrevive al entrar en Play mode sin ayuda (mismo motivo por el que
    // PlayerInputDriver.Squad siempre funcionó sin Bind()).
    [DefaultExecutionOrder(-50)]
    public class BattleManager : MonoBehaviour
    {
        public List<Soldier> Enemies;
        public List<Soldier> Squad;
        public GameOutcomeController Outcome;

        IDisposable sub;

        void OnEnable() => sub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnEntityDied);
        void OnDisable() => sub?.Dispose();

        void OnEntityDied(EntityDiedEvent evt)
        {
            if (Outcome == null) return;

            // Antes solo miraba la lista `Enemies` (los 4 de la patrulla),
            // pero el mapa tiene 7 enemigos: matando esos 4 saltaba
            // "Ganaste" con 3 enemigos todavía vivos y disparando. La
            // victoria se decide contra TODOS los enemigos vivos, que es
            // lo que el jugador ve.
            if (ActorRegistry.CountAlive(SP.Combat.TeamId.Enemy) > 0) return;
            if (ActorRegistry.CountAlive(SP.Combat.TeamId.Player) > 0) Outcome.ShowVictory();
        }
    }
}
