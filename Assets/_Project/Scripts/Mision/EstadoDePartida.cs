using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.UI;
using SP.Presentation;

namespace SP.Mision
{
    // Ronda 11 (puntos 1 y 20). Antes la derrota se decidia UNA sola vez, en la corrutina de muerte del soldado poseido,
    // asi que si moria el ultimo aliado despues nadie volvia a mirar y la partida seguia "en Infiltrar" con 0 vivos.
    // Este evaluador corre siempre (WorldSimulationDriver.Step, el camino que comparten el juego y la suite) y mira la escuadra entera:
    //   - con algun aliado vivo no hace nada;
    //   - con TODOS caidos y enemigos cerca (accion): derrota a los 2 s;
    //   - con TODOS caidos y calma (ningun enemigo vivo a menos de RadioDeAccion de un caido): cuenta de 4 s y reviven a mitad de vida.
    // Si la accion vuelve durante la cuenta, la cuenta se reinicia y rige la regla de derrota.
    public static class EstadoDePartida
    {
        public const float SegundosParaDerrota = 2f;
        public const float SegundosDeCalma = 4f;
        public const float RadioDeAccion = 25f;
        public const float FraccionAlRevivir = 0.5f;

        public static float SinVivos { get; private set; }
        public static float CalmaAcumulada { get; private set; }
        public static bool Revivio { get; private set; }
        public static bool Perdio { get; private set; }

        static bool avisoDeCuenta;
        static GameOutcomeController outcomeSuelto;

        public static void Reiniciar()
        {
            SinVivos = 0f; CalmaAcumulada = 0f; Revivio = false; Perdio = false; avisoDeCuenta = false; outcomeSuelto = null;
        }

        static bool CuentaComoEscuadra(Soldier s) => s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.Health != null;

        public static void Tick(float dt)
        {
            if (MisionDirector.Activo && (MisionDirector.Instancia.Fase == FaseDeMision.Victoria || MisionDirector.Instancia.Fase == FaseDeMision.Derrota)) return;
            int vivos = 0, caidos = 0;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (!CuentaComoEscuadra(s)) continue;
                if (s.Health.IsAlive) vivos++; else caidos++;
            }
            if (vivos > 0 || caidos == 0) { SinVivos = 0f; CalmaAcumulada = 0f; avisoDeCuenta = false; Perdio = false; return; }   // al reintentar hay vivos otra vez: se rearma
            if (Perdio) return;

            SinVivos += dt;
            if (HayAccion(todos))
            {
                CalmaAcumulada = 0f;
                avisoDeCuenta = false;
                if (SinVivos >= SegundosParaDerrota) Perder();
                return;
            }

            CalmaAcumulada += dt;
            if (!avisoDeCuenta)
            {
                avisoDeCuenta = true;
                AlertQueue.Push("SIN ACCION: LA ESCUADRA SE RECUPERA EN " + Mathf.CeilToInt(SegundosDeCalma) + " s", AlertPriority.Media, SegundosDeCalma);
                GameLog.Line("Escuadra caida y en calma: reviven en " + SegundosDeCalma + " s si no hay accion");
            }
            if (CalmaAcumulada >= SegundosDeCalma) RevivirEscuadra(todos);
        }

        static bool HayAccion(System.Collections.Generic.IReadOnlyList<Soldier> todos)
        {
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (!CuentaComoEscuadra(s)) continue;
                if (ActorRegistry.FindNearestEnemyInRange(s.transform.position, TeamId.Player, RadioDeAccion) != null) return true;
            }
            return false;
        }

        static void RevivirEscuadra(System.Collections.Generic.IReadOnlyList<Soldier> todos)
        {
            int n = 0;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (!CuentaComoEscuadra(s) || s.Health.IsAlive) continue;
                s.Health.Initialize(s.Id, Mathf.Max(1, Mathf.RoundToInt(s.Health.MaxHealth * FraccionAlRevivir)));
                s.Motor.ResetMotionState();
                s.SetBodyVisible(true);
                EventBus.Instance.Publish(new HealedEvent(s.Id, s.Health.Current, s.Health.Current));
                n++;
            }
            Revivio = true;
            CalmaAcumulada = 0f; SinVivos = 0f; avisoDeCuenta = false;
            AlertQueue.Push("ESCUADRA REVIVIDA", AlertPriority.Alta, 2.5f);
            GameLog.Line("La escuadra revivio en calma (" + n + " soldados)");
        }

        static void Perder()
        {
            Perdio = true;
            if (MisionDirector.Activo) { MisionDirector.Instancia.Perder("TODA LA ESCUADRA CAYO"); return; }
            if (outcomeSuelto == null) outcomeSuelto = Object.FindFirstObjectByType<GameOutcomeController>();
            if (outcomeSuelto != null) { GameLog.Line("Perdiste (escuadra completa caida)"); outcomeSuelto.ShowDefeat(); }
        }
    }
}
