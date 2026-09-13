using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // Item 227: campo de flujo.
    //
    // ADVERTENCIA HONESTA SOBRE CUANDO USARLO. Este proyecto mueve a los
    // soldados en linea recta (SoldierMotor.MoveTowards: una resta, un
    // magnitude y un normalize, O(1) por unidad por frame). Un campo de
    // flujo NO ahorra nada frente a eso: lo AGREGA. Su valor real es otro,
    // y es el unico motivo por el que existe aca:
    //
    //   * permite RODEAR obstaculos en vez de encajarse contra ellos, y
    //   * amortiza el rodeo entre MUCHAS unidades que van al MISMO punto,
    //     que es exactamente la forma de una orden de escuadra.
    //
    // Con 1 unidad, la linea recta gana siempre. Con 1 unidad y un
    // obstaculo en el medio, conviene WaypointGraph.TryFindPath (un A* y
    // listo). El campo de flujo recien gana a partir de ~8-10 unidades
    // hacia el mismo destino, porque el costo del barrido se paga UNA vez
    // y despues cada unidad hace una consulta O(1).
    //
    // Costo: un barrido Dijkstra sobre la grilla del grafo (O(N log N) con
    // la cola, N = nodos) por cada Compute, y 2 arrays de N (float + byte).
    // Con la grilla tipica de este mapa (180x180 unidades, spacing 4) son
    // ~2000 nodos: unos 10 KB y un barrido despreciable por orden.
    public sealed class FlowField
    {
        WaypointGraph graph;

        // Costo acumulado hasta el destino, por nodo. Infinito = inalcanzable.
        float[] cost;
        // Direccion (indice de vecino 0..7) que baja el costo. 255 = ninguna.
        byte[] flow;

        const byte NoDirection = 255;

        public bool IsComputed { get; private set; }
        public int ReachableCount { get; private set; }
        public Vector3 Destination { get; private set; }

        // Cola de prioridad minima sobre un binary heap. Se reusa entre
        // llamadas: asignar una por orden seria basura por orden.
        //
        // BUG REAL: la key se guardaba en un array indexado POR NODO
        // (heapKey[node]), no por POSICION del heap (el patron correcto,
        // que ya usa NodeMinHeap en WaypointGraph.cs, es un array paralelo
        // a las entradas). Cuando un nodo se relaja dos veces queda
        // pusheado dos veces -- la entrada vieja (obsoleta) y la nueva
        // comparten la MISMA celda heapKey[node], asi que la segunda
        // pisaba a la primera. Al popear la entrada vieja, "key" volvia a
        // leer el costo ACTUAL (el mejor), no el costo con el que esa
        // entrada se pusheo -- el chequeo "if (k > cost[node]) continue"
        // de mas abajo, pensado para descartarla, nunca podia disparar
        // porque k terminaba siendo siempre igual a cost[node]. Resultado:
        // todo nodo relajado mas de una vez se procesaba de nuevo (vecinos
        // re-relajados de mas), inflando ReachableCount y desperdiciando
        // el barrido que este campo de flujo existe justamente para pagar
        // UNA sola vez.
        readonly List<int> heap = new List<int>();
        readonly List<float> heapSlotKey = new List<float>();

        public void Attach(WaypointGraph waypointGraph)
        {
            graph = waypointGraph;
            IsComputed = false;
        }

        // Un solo barrido desde el destino hacia afuera. Es Dijkstra y no
        // BFS porque las diagonales cuestan raiz de 2: con BFS el campo
        // saldria sesgado a las diagonales y las unidades caminarian en
        // zigzag sobre terreno abierto.
        public bool Compute(Vector3 destination)
        {
            IsComputed = false;
            ReachableCount = 0;
            if (graph == null || !graph.IsBuilt) return false;

            int n = graph.NodeCount;
            EnsureBuffers(n);

            for (int i = 0; i < n; i++)
            {
                cost[i] = float.PositiveInfinity;
                flow[i] = NoDirection;
            }

            // Si el destino cae sobre un obstaculo, se toma el nodo libre
            // mas cercano: una orden a un punto invalido tiene que hacer
            // algo razonable, no fallar en silencio.
            int start = graph.IsBlockedAt(destination)
                ? graph.FindFreeNodeNear(destination)
                : graph.NodeAt(destination);
            if (start < 0) return false;

            Destination = graph.NodeToWorld(start);
            cost[start] = 0f;

            heap.Clear();
            heapSlotKey.Clear();
            HeapPush(start, 0f);

            while (heap.Count > 0)
            {
                float k;
                int node = HeapPop(out k);
                // Entrada obsoleta: ya salio con un costo menor.
                if (k > cost[node]) continue;
                ReachableCount++;

                for (int dir = 0; dir < WaypointGraph.NeighborCount; dir++)
                {
                    int neighbor;
                    float step;
                    if (!graph.TryGetNeighbor(node, dir, out neighbor, out step)) continue;

                    float candidate = cost[node] + step;
                    if (candidate >= cost[neighbor]) continue;

                    cost[neighbor] = candidate;
                    // El vecino tiene que caminar HACIA node, o sea la
                    // direccion opuesta a la que usamos para llegar a el.
                    // WaypointGraph garantiza que el opuesto de d es
                    // (d + 4) & 7, asi que no hace falta ninguna tabla.
                    flow[neighbor] = (byte)((dir + 4) & 7);
                    HeapPush(neighbor, candidate);
                }
            }

            IsComputed = true;
            return true;
        }

        // O(1) por unidad por frame: esto es lo que amortiza el barrido.
        // Vector3.zero significa "ya llegaste" o "no hay camino": el
        // llamador decide si eso es quedarse quieto o caer a la linea recta.
        public Vector3 DirectionAt(Vector3 worldPos)
        {
            if (!IsComputed || graph == null) return Vector3.zero;
            int node = graph.NodeAt(worldPos);
            if (node < 0 || node >= flow.Length) return Vector3.zero;
            byte dir = flow[node];
            if (dir == NoDirection) return Vector3.zero;
            return WaypointGraph.NeighborDirection(dir);
        }

        // Distancia real por el campo (rodeando), no en linea recta. Sirve
        // para saber quien esta mas cerca DE VERDAD cuando hay un muro en
        // el medio.
        public float CostAt(Vector3 worldPos)
        {
            if (!IsComputed || graph == null) return float.PositiveInfinity;
            int node = graph.NodeAt(worldPos);
            if (node < 0 || node >= cost.Length) return float.PositiveInfinity;
            return cost[node];
        }

        public bool IsReachable(Vector3 worldPos) => !float.IsPositiveInfinity(CostAt(worldPos));

        void EnsureBuffers(int n)
        {
            if (cost == null || cost.Length < n)
            {
                cost = new float[n];
                flow = new byte[n];
            }
        }

        // --- binary heap minimo, sin asignar por operacion ---
        // heapSlotKey es paralelo a heap (por POSICION en la cola, no por
        // nodo): mismo patron que NodeMinHeap en WaypointGraph.cs. Asi una
        // entrada obsoleta conserva la key con la que fue pusheada aunque
        // el mismo nodo se vuelva a pushear despues con un costo mejor.

        void HeapPush(int node, float key)
        {
            heap.Add(node);
            heapSlotKey.Add(key);
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heapSlotKey[parent] <= heapSlotKey[i]) break;
                Swap(parent, i);
                i = parent;
            }
        }

        int HeapPop(out float key)
        {
            int top = heap[0];
            key = heapSlotKey[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heapSlotKey[0] = heapSlotKey[last];
            heap.RemoveAt(last);
            heapSlotKey.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;
                if (left < heap.Count && heapSlotKey[left] < heapSlotKey[smallest]) smallest = left;
                if (right < heap.Count && heapSlotKey[right] < heapSlotKey[smallest]) smallest = right;
                if (smallest == i) break;
                Swap(smallest, i);
                i = smallest;
            }
            return top;
        }

        void Swap(int a, int b)
        {
            int tn = heap[a]; heap[a] = heap[b]; heap[b] = tn;
            float tk = heapSlotKey[a]; heapSlotKey[a] = heapSlotKey[b]; heapSlotKey[b] = tk;
        }
    }
}
