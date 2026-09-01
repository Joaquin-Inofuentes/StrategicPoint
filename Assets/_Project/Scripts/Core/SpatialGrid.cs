using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Core
{
    // ActorRegistry.FindNearest(EnemyInRange) barre linealmente TODOS los
    // soldados en cada llamada. AiBrain.Tick() llama a esa busqueda para
    // CADA soldado no ocupado en combate, en CADA tick de simulacion: con
    // n soldados eso es un barrido O(n) por soldado, o sea O(n^2) por tick
    // completo. Con cincuenta contra cincuenta son miles de comparaciones
    // por tick, y crece al cuadrado con cualquier ejercito futuro.
    //
    // La grilla reparte a los soldados vivos en celdas de mundo antes de
    // que arranque el tick, en un solo barrido O(n) (Rebuild). Cada
    // soldado despues solo compara contra los vecinos de su propia celda
    // y las adyacentes, no contra todos: la busqueda deja de crecer al
    // cuadrado con la cantidad de unidades.
    //
    // Rebuild() se llama UNA vez al principio de cada paso de simulacion
    // (WorldSimulationDriver.Update en Play mode real, SimStep en el
    // runner de pruebas de Editor), nunca dentro de una busqueda -- así
    // el costo de "reacomodar la grilla" se paga una vez por tick y no una
    // vez por soldado que pregunta.
    public static class SpatialGrid
    {
        // Los rangos tipicos del juego son grandes (vision 10, alerta 30,
        // torreta 40): con una celda chica, cubrir esos rangos exige
        // revisar decenas de celdas -- la mayoria vacias con pocas
        // unidades -- y el costo de esas búsquedas en el diccionario
        // termina superando al ahorro. Medido: con celda de 8 y sesenta
        // soldados, la grilla perdia contra el barrido lineal original.
        // Con la celda mas grande, menos celdas por consulta y la grilla
        // gana ya en ese mismo escenario.
        const float CellSize = 20f;

        // Clave como long en vez de ValueTuple<int,int>: medido, el hashing
        // y la comparacion de un ValueTuple como clave de Dictionary tiene
        // overhead constante notable comparado con un long simple, y a
        // escalas chicas (alrededor de cincuenta unidades) ese overhead
        // por celda alcanzaba a comerse la ganancia entera de no barrer
        // todos los soldados.
        static readonly Dictionary<long, List<Soldier>> cells = new Dictionary<long, List<Soldier>>();
        static bool built;

        static readonly List<long> emptyKeysBuffer = new List<long>();
        public static int CellCount => cells.Count;

        const int Offset = 1 << 20; // desplaza coordenadas negativas antes de empacar

        static long Key(int cx, int cz) => ((long)(cx + Offset) << 32) | (uint)(cz + Offset);

        static void CellCoordsOf(Vector3 pos, out int cx, out int cz)
        {
            cx = Mathf.FloorToInt(pos.x / CellSize);
            cz = Mathf.FloorToInt(pos.z / CellSize);
        }

        static long CellOf(Vector3 pos)
        {
            CellCoordsOf(pos, out int cx, out int cz);
            return Key(cx, cz);
        }

        public static void Rebuild()
        {
            // WorldSimulationDriver.Step() llama Rebuild() una vez por tick,
            // antes de cualquier sensado de IA -- es la puerta de entrada real
            // de la simulacion. Sin este EnsureAllRegistered(), un soldado que
            // arranca la escena ya desactivado (ej. premontado en un vehiculo)
            // nunca entra en ActorRegistry.All -- y por lo tanto nunca en esta
            // grilla -- salvo que, por casualidad, algo mas (CountAlive /
            // CollectLivingAllies) lo haya registrado antes. Mismo arreglo que
            // ya tienen esos dos, aplicado donde realmente hace falta.
            ActorRegistry.EnsureAllRegistered();

            foreach (var list in cells.Values) list.Clear();

            // OJO: sin filtro de activeInHierarchy a proposito. El
            // ActorRegistry.FindNearest original tampoco lo tenia -- un
            // soldado montado en un vehiculo (inactivo) igual podia ser
            // sensado como objetivo. Agregar ese filtro aca seria cambiar
            // una regla de juego disfrazado de optimizacion de
            // rendimiento, que no es lo que se pidio (detectado
            // comparando resultados contra el barrido original).
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                var key = CellOf(s.transform.position);
                if (!cells.TryGetValue(key, out var list))
                {
                    list = new List<Soldier>();
                    cells[key] = list;
                }
                list.Add(s);
            }

            // Purga las celdas que quedaron vacias en ESTE Rebuild: sin esto,
            // toda celda que alguna vez tuvo un soldado se queda en el
            // diccionario para siempre, y Rebuild() -- que corre una vez por
            // Step, sesenta veces por segundo -- se pone mas lento con la
            // vida de la partida solo por el tamaño del diccionario, no por
            // la cantidad real de soldados.
            emptyKeysBuffer.Clear();
            foreach (var kvp in cells)
                if (kvp.Value.Count == 0) emptyKeysBuffer.Add(kvp.Key);
            for (int i = 0; i < emptyKeysBuffer.Count; i++) cells.Remove(emptyKeysBuffer[i]);

            built = true;
        }

        // Si nadie llamo Rebuild todavia (por ejemplo un test que consulta
        // la grilla sin pasar por un tick completo) se construye una vez
        // sola bajo demanda, para no devolver "no encontrado" por un
        // detalle de orden de llamadas en vez de por la busqueda en si.
        static void EnsureBuilt()
        {
            if (!built) Rebuild();
        }

        // Mismo contrato que ActorRegistry.FindNearest, pero acotado a un
        // radio: solo recorre las celdas que podrian contener algo dentro
        // de range, no la lista completa.
        public static Soldier FindNearestInRange(Vector3 point, float range, Func<Soldier, bool> predicate)
        {
            EnsureBuilt();

            CellCoordsOf(point, out int centerX, out int centerZ);
            // El +1 no es margen de sobra: el punto de consulta puede estar
            // pegado al borde de su propia celda, no en su centro. Un
            // enemigo a distancia exactamente "range" en esa direccion
            // puede caer una celda mas alla de ceil(range/CellSize) por
            // ese desfasaje. Sin el +1, esos casos de borde se perdian
            // (detectado comparando contra un barrido de fuerza bruta).
            int radius = Mathf.Max(1, Mathf.CeilToInt(range / CellSize) + 1);
            float bestSqr = range * range;
            Soldier best = null;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (!cells.TryGetValue(Key(centerX + dx, centerZ + dz), out var list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var s = list[i];
                        if (s == null || !predicate(s)) continue;
                        float sqr = (s.transform.position - point).sqrMagnitude;
                        if (sqr <= bestSqr) { bestSqr = sqr; best = s; }
                    }
                }
            }

            return best;
        }
    }
}
