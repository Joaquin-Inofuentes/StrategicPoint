using System;
using System.Collections.Generic;
using SP.Actors;
using SP.Combat;

namespace SP.Core
{
    // Pedido explicito: "en rojo los enemigos solamente los que los aliados
    // hayan visto o vos hayas visto o te hayan disparado" -- antes el rombo
    // rojo mostraba a CUALQUIER enemigo dentro de rango, aunque nadie del
    // bando del jugador lo hubiera detectado nunca (vision de rayos X
    // gratis a traves de paredes y vegetacion). Este registro guarda que
    // enemigos ya fueron revelados, por cualquiera de las tres fuentes:
    //   - un aliado (AiBrain) lo senso visualmente (AiBrain.Sentidos.cs,
    //     SenseNearestEnemy -> aca via Revelar).
    //   - el jugador lo tuvo dentro de su cono de vision Y con linea de
    //     tiro libre (UnitLocatorCylinder.Update -> aca via Revelar).
    //   - le disparo al jugador (visto o no) -- se subscribe solo a
    //     DamageTakenEvent con blanco el soldado poseido.
    // Una vez revelado queda revelado para el resto de la mision: no hace
    // falta "seguir viendolo" (igual que en la vida real uno no olvida a un
    // enemigo que ya identifico).
    public static class InteligenciaDeEnemigos
    {
        static readonly HashSet<int> revelados = new HashSet<int>();
        static IDisposable subDano;

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload
        // (mismo patron que el resto del proyecto): sin este reset, los
        // enemigos revelados de una sesion de Play anterior seguirian
        // marcados como revelados en la siguiente.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            revelados.Clear();
            subDano?.Dispose();
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(AlRecibirDano);
        }

        static void AlRecibirDano(DamageTakenEvent evt)
        {
            var yo = SP.Player.PlayerBrain.Activo != null ? SP.Player.PlayerBrain.Activo.Current : null;
            if (yo == null || evt.TargetId != yo.Id) return;
            var atacante = ActorRegistry.FindById(evt.AttackerId);
            if (atacante != null && atacante.Team == TeamId.Enemy) Revelar(atacante.Id);
        }

        public static void Revelar(int enemigoId) => revelados.Add(enemigoId);
        public static bool EstaRevelado(int enemigoId) => revelados.Contains(enemigoId);
    }
}
