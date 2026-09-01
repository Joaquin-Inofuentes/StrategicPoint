using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;

namespace SP.Core
{
    // Registro simple de soldados vivos en la escena, para consultas de
    // distancia y de sensado. Sustituye a FindObjectOfType.
    public static class ActorRegistry
    {
        static readonly List<Soldier> soldiers = new List<Soldier>();

        public static void Register(Soldier soldier)
        {
            if (!soldiers.Contains(soldier)) soldiers.Add(soldier);
        }

        public static void Unregister(Soldier soldier) => soldiers.Remove(soldier);

        public static void Clear() => soldiers.Clear();

        public static IReadOnlyList<Soldier> All => soldiers;

        // Soldier.Awake() -- que es quien registra -- NO corre en un
        // GameObject que ya está desactivado cuando carga la escena (un
        // soldado guardado adentro del vehículo, por ejemplo). Ese soldado
        // quedaba fuera del registro para siempre: invisible para el
        // sensado de la IA, para la condición de victoria y para los
        // contadores del HUD, aunque estuviera perfectamente vivo. Este
        // barrido incluye los inactivos y los da de alta.
        public static void EnsureAllRegistered()
        {
            var found = UnityEngine.Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in found)
            {
                if (s == null) continue;
                s.Bootstrap();
                Register(s);
            }
        }

        // Cuenta vivos de un equipo. Un soldado montado en un vehículo
        // tiene el GameObject desactivado pero sigue vivo y sigue siendo
        // parte de la escuadra: no puede desaparecer del marcador solo
        // por haberse subido al tanque.
        public static int CountAlive(TeamId team)
        {
            EnsureAllRegistered();
            int n = 0;
            foreach (var s in soldiers)
                if (s != null && s.Team == team && s.Health != null && s.Health.IsAlive) n++;
            return n;
        }

        public static int CountDead(TeamId team)
        {
            EnsureAllRegistered();
            int n = 0;
            foreach (var s in soldiers)
                if (s != null && s.Team == team && s.Health != null && !s.Health.IsAlive) n++;
            return n;
        }

        public static Soldier FindById(int id)
        {
            foreach (var s in soldiers)
                if (s != null && s.Id == id) return s;
            return null;
        }

        public static Soldier FindNearest(Vector3 point, Func<Soldier, bool> predicate)
        {
            Soldier best = null;
            float bestDist = float.MaxValue;
            foreach (var s in soldiers)
            {
                if (s == null || !predicate(s)) continue;
                float d = Vector3.Distance(point, s.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = s;
                }
            }
            return best;
        }

        // Es la consulta mas repetida del juego: cada soldado no ocupado
        // en combate la llama en cada tick para saber si hay un enemigo
        // cerca. Con range acotado, SpatialGrid la resuelve mirando solo
        // las celdas vecinas en vez de barrer a todos los soldados vivos.
        public static Soldier FindNearestEnemyInRange(Vector3 point, TeamId excludeTeam, float range)
        {
            return SpatialGrid.FindNearestInRange(point, range, s =>
                s.Health.IsAlive && s.Team != excludeTeam);
        }
    }
}
