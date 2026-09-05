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

        // El set existe SOLO para que Register sea O(1). Antes la unica
        // estructura era la lista y Register hacia soldiers.Contains(),
        // que es un barrido lineal: como EnsureAllRegistered vuelve a dar
        // de alta a todos, el alta completa costaba O(n^2). Con cincuenta
        // soldados eso son 2.500 comparaciones cada vez que alguien la
        // llamaba.
        static readonly HashSet<Soldier> registrados = new HashSet<Soldier>();

        public static void Register(Soldier soldier)
        {
            if (soldier == null) return;
            if (registrados.Add(soldier)) soldiers.Add(soldier);
        }

        public static void Unregister(Soldier soldier)
        {
            if (soldier == null) return;
            if (registrados.Remove(soldier)) soldiers.Remove(soldier);
        }

        public static void Clear()
        {
            soldiers.Clear();
            registrados.Clear();
            proximoBarrido = 0f;
        }

        public static IReadOnlyList<Soldier> All => soldiers;

        // Fuerza que el proximo EnsureAllRegistered vuelva a barrer. Lo
        // llama quien instancia soldados fuera del ciclo normal: un
        // soldado creado ya DESACTIVADO no corre Awake y por lo tanto no
        // se registra solo.
        public static void Invalidate() => proximoBarrido = 0f;

        static float proximoBarrido;

        // Soldier.Awake() -- que es quien registra -- NO corre en un
        // GameObject que ya está desactivado cuando carga la escena (un
        // soldado guardado adentro del vehículo, por ejemplo). Ese soldado
        // quedaba fuera del registro para siempre: invisible para el
        // sensado de la IA, para la condición de victoria y para los
        // contadores del HUD, aunque estuviera perfectamente vivo. Este
        // barrido incluye los inactivos y los da de alta.
        // EL BARRIDO YA NO SE PAGA POR TICK. Antes esto corria entero en
        // cada llamada, y SpatialGrid.Rebuild() lo llama UNA VEZ POR TICK
        // (60 veces por segundo): un FindObjectsByType completo de la
        // escena mas un alta O(n^2), justo adentro de la funcion que
        // existe para que el sensado deje de ser O(n^2). La grilla
        // arreglaba el sensado y volvia a meter el costo por la puerta de
        // atras. Medido con 70 soldados: 0,202 ms por tick, o sea 12 ms
        // de CPU por cada segundo de partida solo para esto.
        //
        // No se cachea "para siempre" a proposito: el barrido existe
        // justamente para encontrar soldados que arrancan DESACTIVADOS y
        // por lo tanto nunca corren Awake ni se registran solos. Si se
        // hiciera una unica vez, uno creado despues del primer tick
        // quedaria invisible para la IA y para la condicion de victoria
        // -- que es el bug que este barrido vino a tapar. Con el
        // intervalo, el peor caso es que tarde medio segundo en verlo, y
        // el costo cae de 60 barridos por segundo a 2.
        const float IntervaloDeBarrido = 0.5f;

        public static void EnsureAllRegistered()
        {
            float ahora = Time.realtimeSinceStartup;
            if (ahora < proximoBarrido) return;
            proximoBarrido = ahora + IntervaloDeBarrido;
            Rebarrer();
        }

        // El barrido de verdad, sin la guarda. Se puede forzar cuando hace
        // falta certeza absoluta (la suite lo usa entre escenarios).
        public static void Rebarrer()
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
