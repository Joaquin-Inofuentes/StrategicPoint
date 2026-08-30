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
            if (Enemies == null || Enemies.Count == 0 || Outcome == null) return;

            foreach (var e in Enemies) if (e != null && e.Health.IsAlive) return;

            bool anySquadAlive = false;
            if (Squad != null) foreach (var s in Squad) if (s != null && s.Health.IsAlive) { anySquadAlive = true; break; }
            if (anySquadAlive) Outcome.ShowVictory();
        }
    }
}
