using System;
using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // ITEM 226 -- Grafo de waypoints (grilla + A* sobre el plano XZ).
    //
    // POR QUE EXISTE (y por que NO es una optimizacion).
    // El movimiento de este juego es SoldierMotor.MoveTowards: una resta,
    // un magnitude y un normalize. Es O(1) por unidad por frame y no hay
    // nada mas barato que eso. Cualquier busqueda de camino AGREGA costo,
    // no lo saca. Este grafo NO se justifica por rendimiento y seria
    // deshonesto venderlo asi.
    //
    // Se justifica por COMPORTAMIENTO: hoy un soldado al que le ordenan ir
    // detras de un obstaculo camina en linea recta, choca contra el cubo y
    // se queda empujandolo para siempre (OrderService.IsValidDestination
    // solo tapa el caso de clickear ENCIMA del obstaculo, no el de tener
    // uno en el medio del camino). Un camino que rodea es una regla de
    // juego distinta -- deliberadamente distinta -- no el mismo resultado
    // mas rapido.
    //
    // GARANTIA DE "NO ROMPE NADA" EN EL CASO LIBRE.
    // Con el suavizado activado (SmoothPaths, default true), si entre
    // origen y destino no hay ningun obstaculo el resultado es
    // EXACTAMENTE dos puntos: [from, to]. O sea, la linea recta de
    // siempre, bit a bit. Solo cuando hay algo en el medio aparecen
    // puntos intermedios. Esto es verificable desde afuera sin escena
    // (ver TryFindPath y PathLength).
    //
    // COSTO EN MEMORIA (arrays persistentes, una sola vez por grafo):
    //   blocked      1 byte/nodo
    //   gScore       4 bytes/nodo
    //   cameFrom     4 bytes/nodo
    //   openStamp    4 bytes/nodo
    //   closedStamp  4 bytes/nodo
    //   ------------------------------------
    //   total       17 bytes/nodo
    //   + el heap, que crece bajo demanda hasta a lo sumo 8 entradas por
    //     nodo * 8 bytes; en la practica se queda muy por debajo.
    //
    //   Con el piso real de este proyecto (160 x 160) y spacing 2:
    //   81 x 81 = 6561 nodos -> ~112 KB + heap. Con spacing 1.5:
    //   108 x 108 = 11664 nodos -> ~198 KB. Es memoria que se reserva UNA
    //   vez al construir el nivel, no por unidad ni por consulta.
    //
    // COSTO EN TIEMPO:
    //   Build       O(N) nodos (y O(N) llamadas al predicado isBlocked,
    //               que es lo mas caro del armado si el predicado barre
    //               la escena -- conviene pasarle un predicado que lea de
    //               una lista ya cacheada, no un FindObjectsByType).
    //   TryFindPath O(E log N) en el peor caso, con E = 8N. En la
    //               practica A* con heuristica octil expande una fraccion
    //               chica; LastExpandedNodes lo dice medido, no estimado.
    //   Suavizado   O(P * L), P puntos crudos del camino y L muestras por
    //               tramo. Es lo que convierte el zigzag de 8 direcciones
    //               en la recta cuando no hay nada en el medio.
    //
    // SIN ASIGNAR POR CONSULTA: todos los buffers son campos reusados y
    // el resultado se escribe en la lista que pasa quien llama. Despues
    // de la primera consulta (que puede hacer crecer el heap) las
    // siguientes no reservan memoria. Importa porque esto puede llamarse
    // para 50 unidades en el frame de una orden de escuadra.
    //
    // DETERMINISTA: no hay Random en ningun lado, el orden de vecinos es
    // una tabla fija y los empates de prioridad en el heap se resuelven
    // por indice de nodo (el mas chico primero). Mismo input, mismo
    // output, corrida tras corrida.
    //
    // PURO: es una clase C# comun, no un MonoBehaviour. No lee Time, ni
    // la escena, ni corrutinas. Se puede construir y consultar desde una
    // fase headless en Edit mode igual que OrderService.FormationPoints.
    // Vector3 y Mathf son structs y matematica estatica, no runtime.
    public sealed class WaypointGraph
    {
        // Orden FIJO de vecinos (8 direcciones). Es antipodal en +4:
        // el opuesto de la direccion d es (d + 4) & 7. FlowField depende
        // de esa propiedad para invertir la arista sin tablas extra.
        // Los indices pares son cardinales (costo 1) y los impares
        // diagonales (costo raiz de 2).
        static readonly int[] NeighborDX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        static readonly int[] NeighborDZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

        const float DiagonalCost = 1.41421356f;
        const float Diag = 0.70710678f; // componente unitaria de una diagonal

        static readonly Vector3[] Directions =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(Diag, 0f, Diag),
            new Vector3(0f, 0f, 1f),
            new Vector3(-Diag, 0f, Diag),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-Diag, 0f, -Diag),
            new Vector3(0f, 0f, -1f),
            new Vector3(Diag, 0f, -Diag)
        };

        public const int NeighborCount = 8;

        // Techo de seguridad: una caja grande con spacing chico puede
        // pedir decenas de millones de nodos por un cero de mas tipeado en
        // una llamada. Mejor no construir y avisar que reventar la memoria
        // del editor en silencio.
        public const int MaxNodes = 400000;

        // --- topologia ---
        bool[] blocked;
        int cols, rows, nodeCount;
        float spacing = 1f;
        Vector3 origin;   // esquina (minX, y, minZ); la Y se conserva tal cual
        bool built;

        // --- buffers de consulta (reusados, nunca reasignados por query) ---
        float[] gScore;
        int[] cameFrom;
        int[] openStamp;
        int[] closedStamp;
        int queryStamp;
        readonly NodeMinHeap open = new NodeMinHeap();
        readonly List<int> pathNodes = new List<int>(256);
        readonly List<Vector3> rawPath = new List<Vector3>(256);

        // ------------------------------------------------------------------
        // Lectura publica del grafo
        // ------------------------------------------------------------------
        public bool IsBuilt => built;
        public int Columns => cols;
        public int Rows => rows;
        public int NodeCount => nodeCount;
        public int BlockedCount { get; private set; }
        public int EdgeCount { get; private set; }
        public float Spacing => spacing;
        public Vector3 Origin => origin;

        // Cuantos nodos saco de la cola A* la ultima consulta. Es la
        // medida honesta del costo de una busqueda: no hay que estimarla,
        // se lee. Comparado contra NodeCount dice cuanto del mapa hizo
        // falta mirar.
        public int LastExpandedNodes { get; private set; }

        // Puntos que devolvio la ultima consulta ANTES de suavizar. Con
        // suavizado y sin obstaculos el resultado final tiene 2 puntos
        // aunque este numero sea 40: sirve para ver cuanto colapso.
        public int LastRawPathPoints { get; private set; }

        // Cuantas celdas se acepta alejarse del punto pedido para
        // engancharlo a un nodo libre cuando cae justo sobre uno
        // bloqueado (el jugador clickeando el borde de un cubo, o una
        // unidad spawneada pegada a uno). Con 0 la regla es estricta:
        // destino bloqueado => TryFindPath devuelve false. Es publico
        // justamente para poder verificar ese caso desde una prueba.
        public int MaxSnapRings { get; set; } = 3;

        // Suavizado por "string pulling". Con true (default) el camino sin
        // obstaculos colapsa a la linea recta exacta. Con false se
        // devuelve el camino crudo de 8 direcciones, que sirve para
        // comparar en una prueba cuanto acorta el suavizado.
        public bool SmoothPaths { get; set; } = true;

        public static Vector3 NeighborDirection(int dir) => Directions[dir & 7];

        // ------------------------------------------------------------------
        // Construccion
        // ------------------------------------------------------------------
        // min/max son dos esquinas opuestas de la caja en XZ (la Y de min
        // se usa como altura de los nodos y la de max se ignora: esto es
        // un grafo plano, el juego camina sobre un piso unico).
        //
        // isBlocked recibe la posicion de mundo del CENTRO del nodo y
        // decide si es intransitable. Se llama exactamente una vez por
        // nodo, nunca durante una consulta: el predicado puede ser caro
        // (barrer obstaculos) sin que eso se pague por unidad ni por
        // frame. Un predicado null construye una grilla toda libre.
        //
        // IMPORTANTE sobre el predicado: conviene que ya venga INFLADO por
        // el radio del soldado (por ejemplo, radio del obstaculo + 0.5,
        // que es el margen que ya usa OrderService.IsValidDestination).
        // La grilla no infla nada por su cuenta: si el predicado marca
        // solo el volumen exacto del cubo, el camino va a rozarlo.
        public void Build(Vector3 min, Vector3 max, float spacing, System.Func<Vector3, bool> isBlocked)
        {
            built = false;
            BlockedCount = 0;
            EdgeCount = 0;
            LastExpandedNodes = 0;
            LastRawPathPoints = 0;

            if (spacing <= 0.0001f) spacing = 1f;

            // Se ordenan las esquinas en vez de exigir que vengan bien: un
            // min/max invertido daria cols/rows negativos y una excepcion
            // de array, que es un fallo mucho peor de diagnosticar que
            // simplemente aceptar la caja al derecho.
            float minX = Mathf.Min(min.x, max.x);
            float maxX = Mathf.Max(min.x, max.x);
            float minZ = Mathf.Min(min.z, max.z);
            float maxZ = Mathf.Max(min.z, max.z);

            this.spacing = spacing;
            origin = new Vector3(minX, min.y, minZ);

            cols = Mathf.FloorToInt((maxX - minX) / spacing) + 1;
            rows = Mathf.FloorToInt((maxZ - minZ) / spacing) + 1;
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);

            long total = (long)cols * rows;
            if (total > MaxNodes)
            {
                // No se construye a proposito: IsBuilt queda en false y
                // TryFindPath devuelve false sin colgarse ni reservar.
                Debug.LogWarning($"[WaypointGraph] {cols}x{rows} = {total} nodos supera MaxNodes ({MaxNodes}). " +
                                 "Subi el spacing o achicá la caja. El grafo queda sin construir.");
                cols = rows = 0;
                nodeCount = 0;
                return;
            }

            nodeCount = cols * rows;
            EnsureCapacity(nodeCount);

            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int i = z * cols + x;
                    var world = new Vector3(minX + x * spacing, origin.y, minZ + z * spacing);
                    bool isBad = isBlocked != null && isBlocked(world);
                    blocked[i] = isBad;
                    if (isBad) BlockedCount++;
                }
            }

            // Las aristas NO se almacenan: se derivan de la tabla de
            // vecinos en O(1) cuando hacen falta. Guardar 8 ints por nodo
            // costaria 32 bytes/nodo (mas que todo el resto junto) para
            // no ahorrar ni una operacion. EdgeCount se cuenta igual, una
            // sola vez, porque es el dato que dice si el grafo quedo
            // conectado o hecho pedazos por los obstaculos.
            for (int i = 0; i < nodeCount; i++)
            {
                if (blocked[i]) continue;
                for (int d = 0; d < NeighborCount; d++)
                    if (TryGetNeighbor(i, d, out _, out _)) EdgeCount++;
            }
            // Cada arista se conto dos veces (una por punta).
            EdgeCount /= 2;

            built = true;
        }

        void EnsureCapacity(int n)
        {
            if (blocked != null && blocked.Length >= n) return;
            blocked = new bool[n];
            gScore = new float[n];
            cameFrom = new int[n];
            openStamp = new int[n];
            closedStamp = new int[n];
            queryStamp = 0;
        }

        // ------------------------------------------------------------------
        // Indexado (mismas convenciones que SpatialGrid: entero por eje,
        // un solo indice lineal, sin tuplas ni claves compuestas)
        // ------------------------------------------------------------------
        // SpatialGrid empaqueta (cx, cz) en un long porque su grilla es
        // infinita y dispersa (diccionario). Aca la grilla es acotada y
        // densa, asi que el indice lineal z * cols + x es estrictamente
        // mejor: es un indice de array, no un hash, y no necesita ni
        // Offset ni empaquetado.
        public int NodeIndex(int x, int z)
        {
            if (x < 0 || z < 0 || x >= cols || z >= rows) return -1;
            return z * cols + x;
        }

        public void NodeToXZ(int node, out int x, out int z)
        {
            x = node % cols;
            z = node / cols;
        }

        // Nodo mas cercano a una posicion de mundo, o -1 si cae fuera de
        // la caja. No mira si esta bloqueado: para eso esta
        // FindFreeNodeNear.
        public int NodeAt(Vector3 worldPos)
        {
            if (!built) return -1;
            int x = Mathf.RoundToInt((worldPos.x - origin.x) / spacing);
            int z = Mathf.RoundToInt((worldPos.z - origin.z) / spacing);
            return NodeIndex(x, z);
        }

        public Vector3 NodeToWorld(int node)
        {
            if (node < 0 || node >= nodeCount) return Vector3.zero;
            NodeToXZ(node, out int x, out int z);
            return new Vector3(origin.x + x * spacing, origin.y, origin.z + z * spacing);
        }

        public bool IsBlockedNode(int node)
        {
            if (node < 0 || node >= nodeCount) return true; // fuera del mapa = intransitable
            return blocked[node];
        }

        public bool IsBlockedAt(Vector3 worldPos) => IsBlockedNode(NodeAt(worldPos));

        // Vecino d de un nodo, con la regla de esquinas aplicada.
        // Publico porque FlowField construye su Dijkstra sobre ESTA misma
        // relacion de adyacencia: si fueran dos definiciones distintas, el
        // campo de flujo y la vista previa podrian mostrarle al jugador
        // dos caminos diferentes para la misma orden.
        public bool TryGetNeighbor(int node, int dir, out int neighbor, out float stepCost)
        {
            neighbor = -1;
            stepCost = 0f;
            if (!built || node < 0 || node >= nodeCount || blocked[node]) return false;

            dir &= 7;
            NodeToXZ(node, out int x, out int z);
            int nx = x + NeighborDX[dir];
            int nz = z + NeighborDZ[dir];
            int n = NodeIndex(nx, nz);
            if (n < 0 || blocked[n]) return false;

            // REGLA DE ESQUINAS: una diagonal solo vale si las DOS celdas
            // ortogonales que comparte estan libres. Sin esto las unidades
            // se cuelan por la esquina exacta de un cubo -- pasan a traves
            // del obstaculo por un punto de medida cero -- que es
            // justamente el bug que este grafo viene a arreglar.
            // La regla es simetrica: desde el vecino se evaluan esas dos
            // mismas celdas, asi que la adyacencia sirve igual en los dos
            // sentidos (FlowField lo necesita para invertir la arista).
            if ((dir & 1) == 1)
            {
                if (IsBlockedNode(NodeIndex(nx, z))) return false;
                if (IsBlockedNode(NodeIndex(x, nz))) return false;
            }

            neighbor = n;
            stepCost = ((dir & 1) == 1 ? DiagonalCost : 1f) * spacing;
            return true;
        }

        // Nodo LIBRE mas cercano a una posicion, buscando en anillos
        // crecientes hasta MaxSnapRings. Determinista: recorre dz de menor
        // a mayor y dentro de cada fila dx de menor a mayor, asi que ante
        // dos candidatos a la misma distancia siempre gana el de indice
        // mas chico. Devuelve -1 si no hay ninguno libre en ese radio, que
        // es como se traduce "destino inalcanzable" cuando el jugador
        // clickea el centro de un obstaculo grande.
        public int FindFreeNodeNear(Vector3 worldPos)
        {
            if (!built) return -1;

            // Se clampea en vez de devolver -1 para un punto de afuera: un
            // soldado empujado un metro fuera del borde del mapa igual
            // tiene que poder recibir ordenes.
            int cx = Mathf.Clamp(Mathf.RoundToInt((worldPos.x - origin.x) / spacing), 0, cols - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt((worldPos.z - origin.z) / spacing), 0, rows - 1);

            int center = cz * cols + cx;
            if (!blocked[center]) return center;

            int maxRings = Mathf.Max(0, MaxSnapRings);
            for (int r = 1; r <= maxRings; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        // Solo el borde del anillo: el interior ya se miro
                        // en las vueltas anteriores.
                        if (dx > -r && dx < r && dz > -r && dz < r) continue;
                        int n = NodeIndex(cx + dx, cz + dz);
                        if (n >= 0 && !blocked[n]) return n;
                    }
                }
            }
            return -1;
        }

        // ------------------------------------------------------------------
        // A*
        // ------------------------------------------------------------------
        // Devuelve true y llena result con la ruta (siempre empieza en
        // 'from' exacto y termina en 'to' exacto). Devuelve false -- SIN
        // colgarse y sin tocar result mas alla de vaciarlo -- cuando:
        //   * el grafo no esta construido,
        //   * origen o destino no tienen ningun nodo libre a menos de
        //     MaxSnapRings celdas (destino adentro de un obstaculo),
        //   * el destino esta en una region desconectada del origen. Aca
        //     A* agota su propia componente conexa y sale: el conjunto
        //     cerrado es finito y cada nodo se expande a lo sumo una vez,
        //     asi que el bucle termina si o si.
        //
        // El costo de la consulta queda en LastExpandedNodes.
        public bool TryFindPath(Vector3 from, Vector3 to, List<Vector3> result)
        {
            if (result == null) return false;
            result.Clear();
            LastExpandedNodes = 0;
            LastRawPathPoints = 0;
            if (!built) return false;

            int start = FindFreeNodeNear(from);
            int goal = FindFreeNodeNear(to);
            if (start < 0 || goal < 0) return false;

            // Mismo nodo: no hay nada que rodear y no hace falta despertar
            // el heap. Es el caso mas comun de todos (ordenes cortas).
            if (start == goal)
            {
                result.Add(from);
                result.Add(to);
                LastRawPathPoints = 2;
                return true;
            }

            // Un stamp nuevo invalida gScore/cameFrom de la consulta
            // anterior sin recorrer los arrays: limpiar 4 arrays de miles
            // de entradas por cada una de 50 unidades seria mas caro que
            // la busqueda misma.
            queryStamp++;
            open.Clear();

            openStamp[start] = queryStamp;
            gScore[start] = 0f;
            cameFrom[start] = -1;
            open.Push(start, Heuristic(start, goal));

            bool found = false;
            while (open.Count > 0)
            {
                int current = open.Pop();

                // Borrado perezoso: el heap no tiene decrease-key, se
                // empujan duplicados con la prioridad nueva y el primero
                // que sale (el mejor) es el que vale. Es la variante mas
                // simple que sigue siendo correcta, y evita el array de
                // posiciones-en-el-heap.
                if (closedStamp[current] == queryStamp) continue;
                closedStamp[current] = queryStamp;
                LastExpandedNodes++;

                if (current == goal) { found = true; break; }

                float gCur = gScore[current];
                for (int d = 0; d < NeighborCount; d++)
                {
                    if (!TryGetNeighbor(current, d, out int n, out float step)) continue;
                    if (closedStamp[n] == queryStamp) continue;

                    float ng = gCur + step;
                    if (openStamp[n] == queryStamp && ng >= gScore[n]) continue;

                    openStamp[n] = queryStamp;
                    gScore[n] = ng;
                    cameFrom[n] = current;
                    open.Push(n, ng + Heuristic(n, goal));
                }
            }

            if (!found) return false;

            // Reconstruccion: del destino hacia atras y despues se da
            // vuelta en el lugar (List.Reverse no reserva memoria).
            pathNodes.Clear();
            int c = goal;
            while (c >= 0)
            {
                pathNodes.Add(c);
                c = cameFrom[c];
            }
            pathNodes.Reverse();

            rawPath.Clear();
            rawPath.Add(from);
            for (int i = 0; i < pathNodes.Count; i++) rawPath.Add(NodeToWorld(pathNodes[i]));
            rawPath.Add(to);
            LastRawPathPoints = rawPath.Count;

            if (SmoothPaths) Smooth(rawPath, result);
            else result.AddRange(rawPath);

            return true;
        }

        // Heuristica octil, escalada por spacing: es exactamente el costo
        // del mejor camino posible en una grilla de 8 direcciones SIN
        // obstaculos, asi que es admisible (nunca sobreestima) y
        // consistente. Admisible importa: con una heuristica que
        // sobreestime, A* devolveria caminos peores que el optimo y el
        // "sin obstaculos = linea recta" dejaria de valer.
        float Heuristic(int a, int b)
        {
            NodeToXZ(a, out int ax, out int az);
            NodeToXZ(b, out int bx, out int bz);
            int dx = Mathf.Abs(ax - bx);
            int dz = Mathf.Abs(az - bz);
            int lo = Mathf.Min(dx, dz);
            int hi = Mathf.Max(dx, dz);
            return (lo * DiagonalCost + (hi - lo)) * spacing;
        }

        // ------------------------------------------------------------------
        // Suavizado ("string pulling")
        // ------------------------------------------------------------------
        // El camino crudo va de centro de celda en centro de celda en 8
        // direcciones: en campo abierto eso es un zigzag hasta 8% mas
        // largo que la recta y se ve como un soldado borracho. Este paso
        // tira de la cuerda: mientras se vea el punto siguiente en linea
        // recta, se saltea el intermedio.
        //
        // Es lo que hace que la propiedad verificable (a) sea EXACTA y no
        // aproximada: sin obstaculos, HasLineOfSight(from, to) da true a
        // la primera y el resultado son dos puntos, la misma recta que
        // caminaba SoldierMotor antes de que existiera nada de esto.
        void Smooth(List<Vector3> raw, List<Vector3> outPath)
        {
            if (raw.Count <= 2)
            {
                outPath.AddRange(raw);
                return;
            }

            outPath.Add(raw[0]);
            int anchor = 0;
            for (int i = 2; i < raw.Count; i++)
            {
                if (HasLineOfSight(raw[anchor], raw[i])) continue;
                // Se corto la visual: el ultimo que SI se veia (i - 1) se
                // vuelve el ancla nuevo y queda como vertice del camino.
                anchor = i - 1;
                outPath.Add(raw[anchor]);
            }
            outPath.Add(raw[raw.Count - 1]);
        }

        // Visual libre entre dos puntos de mundo, muestreando la grilla.
        //
        // HONESTIDAD SOBRE EL METODO: esto muestrea el segmento cada
        // spacing/4, no hace un recorrido supercover exacto de celdas. Un
        // segmento que apenas roce la esquina de una celda bloqueada por
        // menos de un cuarto de celda puede pasar. Es aceptable aca
        // porque el predicado isBlocked que se le pasa a Build viene
        // inflado con el margen del soldado (radio del obstaculo + 0.5),
        // asi que ese "rasguño" cae dentro del margen y no dentro del
        // cubo. Si algun dia hace falta exactitud milimetrica, este es el
        // unico metodo a cambiar.
        public bool HasLineOfSight(Vector3 a, Vector3 b)
        {
            if (!built) return false;

            Vector3 d = b - a;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.0001f) return !IsBlockedAt(a);

            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / (spacing * 0.25f)));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = a + d * ((float)i / steps);
                if (IsBlockedAt(p)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Helpers de verificacion (puros, sin escena)
        // ------------------------------------------------------------------
        // Largo total de una polilinea en el plano XZ. Es la medida
        // objetiva de las tres afirmaciones que hay que poder sostener:
        //   (a) sin obstaculos: PathLength(camino) == Vector3.Distance(from, to)
        //       dentro de un epsilon, y camino.Count == 2.
        //   (b) con un obstaculo en el medio: TryFindPath sigue dando true
        //       y PathLength es MAYOR que la recta (rodear cuesta), y
        //       ningun punto del camino cae sobre una celda bloqueada
        //       (verificable con IsBlockedAt sobre cada punto).
        //   (c) destino inalcanzable: TryFindPath devuelve false y
        //       LastExpandedNodes queda acotado por NodeCount -- no se
        //       cuelga, porque cada nodo se cierra a lo sumo una vez.
        public static float PathLength(IReadOnlyList<Vector3> path)
        {
            if (path == null || path.Count < 2) return 0f;
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float dx = path[i].x - path[i - 1].x;
                float dz = path[i].z - path[i - 1].z;
                total += Mathf.Sqrt(dx * dx + dz * dz);
            }
            return total;
        }

        // Verificacion directa de (b): ningun tramo del camino atraviesa
        // una celda bloqueada. Se apoya en el mismo muestreo que el
        // suavizado, asi que si esto da true el camino es tan bueno como
        // el suavizado promete.
        public bool PathIsClear(IReadOnlyList<Vector3> path)
        {
            if (!built || path == null || path.Count == 0) return false;
            for (int i = 1; i < path.Count; i++)
                if (!HasLineOfSight(path[i - 1], path[i])) return false;
            return true;
        }
    }

    // Cola de prioridad minima sobre indices de nodo. Vive aca (y no en su
    // propio archivo) porque la comparten WaypointGraph y FlowField y son
    // el mismo tipo de cola exacta; internal para que no se filtre como
    // API publica del juego.
    //
    // Sin decrease-key a proposito: se permiten entradas duplicadas del
    // mismo nodo y quien la usa descarta las repetidas con su marca de
    // "cerrado". Eso ahorra el array de posiciones-en-el-heap (4 bytes por
    // nodo) a cambio de un heap un poco mas grande, que es memoria
    // reusada y no reservada por consulta.
    internal sealed class NodeMinHeap
    {
        int[] nodes = new int[128];
        float[] keys = new float[128];
        int count;

        public int Count => count;

        public void Clear() => count = 0;

        // Empate por PRIORIDAD resuelto por INDICE DE NODO: es lo que hace
        // determinista al algoritmo entero. Sin este desempate, dos
        // corridas identicas pueden devolver dos caminos distintos (ambos
        // optimos, pero distintos) segun como haya quedado el heap.
        bool Less(int a, int b)
        {
            if (keys[a] < keys[b]) return true;
            if (keys[a] > keys[b]) return false;
            return nodes[a] < nodes[b];
        }

        void Swap(int a, int b)
        {
            int tn = nodes[a]; nodes[a] = nodes[b]; nodes[b] = tn;
            float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
        }

        public void Push(int node, float key)
        {
            if (count == nodes.Length)
            {
                Array.Resize(ref nodes, nodes.Length * 2);
                Array.Resize(ref keys, keys.Length * 2);
            }

            nodes[count] = node;
            keys[count] = key;
            int i = count;
            count++;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (!Less(i, parent)) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public int Pop()
        {
            int top = nodes[0];
            count--;
            if (count > 0)
            {
                nodes[0] = nodes[count];
                keys[0] = keys[count];
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    if (left >= count) break;
                    int right = left + 1;
                    int best = (right < count && Less(right, left)) ? right : left;
                    if (!Less(best, i)) break;
                    Swap(i, best);
                    i = best;
                }
            }
            return top;
        }
    }
}
