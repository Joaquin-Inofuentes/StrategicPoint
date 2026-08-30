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

        public static Soldier FindNearestEnemyInRange(Vector3 point, TeamId excludeTeam, float range)
        {
            return FindNearest(point, s =>
                s.Health.IsAlive &&
                s.Team != excludeTeam &&
                Vector3.Distance(point, s.transform.position) <= range);
        }
    }
}
