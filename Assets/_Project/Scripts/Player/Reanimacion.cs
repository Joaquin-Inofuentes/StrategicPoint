using System;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Core;

namespace SP.Player
{
    // Ronda 13 (puntos 1 y 8): UNICO camino para revivir a un soldado caido. Lo usan la tecla [E], la habilidad del
    // medico ([Ctrl]), PedidoDeCuracion (orden de reanimar y reanimacion automatica en calma), RescateAutomatico y el
    // EstadoDePartida. Ver AiBrain.Revivir.cs: reponer solo la vida dejaba el cerebro con el estado de antes de morir.
    public static class Reanimacion
    {
        // Lo escuchan SelectionController (devuelve al revivido a la seleccion de RTS) y la presentacion.
        public static event Action<Soldier> Revivido;

        // fraccionDeVida 1 = vida completa (lo normal). Devuelve false si no habia nadie caido que revivir.
        public static bool Ejecutar(Soldier caido, float fraccionDeVida = 1f)
        {
            if (caido == null || caido.Health == null || caido.Health.IsAlive) return false;

            // WP11: antes era Initialize(id, vida): con fraccion < 1 (la escuadra revive "a mitad de vida") el MAXIMO quedaba partido a la mitad
            // para siempre (50 de 50 en lugar de 50 de 100). Se reinicia con el maximo y recien despues se baja la vida actual.
            int max = caido.Health.MaxHealth;
            int vida = Mathf.Max(1, Mathf.RoundToInt(max * Mathf.Clamp01(fraccionDeVida)));
            caido.Health.Initialize(caido.Id, max);
            if (vida < max) caido.Health.RestaurarVida(vida);
            caido.Motor.ResetMotionState();
            caido.SetBodyVisible(true);

            // El cuerpo que el jugador manejaba al morir sigue "poseido" mientras la camara esta en RTS o en otro soldado:
            // se suelta para que la IA vuelva a obedecer. Si el jugador lo sigue manejando en primera persona (camara de
            // muerte, se vuelve a FPS solo) se conserva.
            var brain = caido.Brain;
            if (brain != null)
            {
                bool loManejaEnFps = AjustesDeEscuadra.Lider == caido;
                bool soltar = brain.IsPossessedByPlayer && !loManejaEnFps && caido.gameObject.activeInHierarchy;
                brain.ResetearTrasRevivir(soltar);
            }

            Revivido?.Invoke(caido);
            return true;
        }
    }
}
