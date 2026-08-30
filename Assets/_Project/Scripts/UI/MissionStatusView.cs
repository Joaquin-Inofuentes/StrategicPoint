using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.UI
{
    // Cuántos enemigos quedan y cuántos de los tuyos siguen vivos. Antes
    // no había forma de saberlo: se peleaba a ciegas, sin saber si
    // faltaban dos enemigos o diez, y la pantalla de victoria aparecía de
    // sorpresa. Es la única parte del HUD que se muestra IGUAL en FPS y
    // en RTS a propósito -- no es información de puntería (que se oculta
    // al pasar a vista táctica), es el estado de la partida.
    public class MissionStatusView : MonoBehaviour
    {
        Text label;
        public List<Soldier> Enemies;
        public List<Soldier> Squad;

        IDisposable diedSub;

        void OnEnable()
        {
            if (label == null) label = GetComponentInChildren<Text>(true);
            // Se recalcula por evento de muerte, no cada frame: el número
            // solo puede cambiar cuando alguien muere.
            diedSub?.Dispose();
            diedSub = EventBus.Instance.Subscribe<EntityDiedEvent>(_ => Refresh());
            Refresh();
        }

        void OnDisable() => diedSub?.Dispose();

        void Start() => Refresh();

        public void Refresh()
        {
            if (label == null) return;

            int enemiesAlive = ActorRegistry.CountAlive(TeamId.Enemy);
            int squadAlive = ActorRegistry.CountAlive(TeamId.Player);

            label.text = $"ENEMIGOS  {enemiesAlive}          ESCUADRA  {squadAlive}";
        }

        // Nota: se cuenta contra el registro global (ActorRegistry), no
        // contra estas listas cableadas en la escena. La lista de
        // "enemigos de la patrulla" tiene 4, pero en el mapa hay 7
        // enemigos vivos: mostrar "4" mientras te disparan 5 es peor que
        // no mostrar nada. El jugador cuenta lo que ve en pantalla, no la
        // sublista que usa el guión de la misión.
    }
}
