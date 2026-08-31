using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Vehicles;
using SP.Presentation;

namespace SP.Player
{
    // Disposiciones posibles de un lote de destinos. Vive AFUERA de
    // OrderService (que es una clase estatica) para que el driver de input
    // pueda nombrarla directo como FormationKind.Linea sin prefijo.
    public enum FormationKind
    {
        Cuadricula, // anillos concentricos: la disposicion historica, y el default
        Linea,      // una sola fila perpendicular al frente
        Cuna,       // V con la punta adelante
        Columna     // fila india sobre el eje del frente
    }

    // Traduce un punto o un objetivo en una orden real sobre el AiBrain
    // del soldado elegido. No decide combate, solo entrega la orden.
    public static class OrderService
    {
        public static Soldier FindNearestFreeAlly(Vector3 point, TeamId team, Soldier exclude)
        {
            return ActorRegistry.FindNearest(point, s =>
                s.Health.IsAlive && s.Team == team && s != exclude);
        }

        public static void IssueMoveOrder(Soldier soldier, Vector3 point, bool queued = false)
        {
            var brain = soldier.GetComponent<AiBrain>();
            // Una orden explícita manda igual aunque el soldado sea el que
            // estás poseyendo: en RTS no lo estás manejando con WASD, así
            // que "IsPossessedByPlayer" no debería frenar a la IA acá (antes
            // seleccionar tu propio soldado y darle "ir ahí" no hacía nada).
            if (brain != null) brain.IsPossessedByPlayer = false;
            brain?.IssueMoveOrder(point, queued);
            EventBus.Instance.Publish(new MoveOrderIssuedEvent(soldier.Id, point));
            int index = queued && brain != null ? brain.QueuedOrderCount : 0;
            OrderMarkerFx.Spawn(point, OrderMarkerFx.MoveColor, index);
        }

        // Separacion minima entre destinos de un mismo lote: por debajo de
        // esto los cubos se solapan visiblemente.
        const float FormationSpacing = 1.8f;

        // Sobrecarga historica: todos los llamadores que ya existian
        // pedian la unica disposicion que habia. Delega en Cuadricula
        // pasando FormationSpacing EXPLICITAMENTE (y no el default de la
        // firma nueva, que es 2) para que el resultado siga siendo
        // exactamente el mismo de antes.
        public static Vector3[] FormationPoints(Vector3 center, int count)
        {
            return FormationPoints(center, Vector3.forward, count, FormationKind.Cuadricula, FormationSpacing);
        }

        // Funcion PURA: mismas entradas, mismas salidas, sin tocar escena
        // ni registro ni tiempo. Es la unica via de verificacion objetiva
        // de la geometria de las formaciones sin entrar en Play mode, asi
        // que no puede depender de nada del runtime.
        public static Vector3[] FormationPoints(Vector3 center, Vector3 forward, int count, FormationKind kind, float spacing = 2f)
        {
            if (count <= 0) return new Vector3[0];
            if (spacing <= 0f) spacing = FormationSpacing;

            // El frente lo va a mandar el arrastre del mouse: puede venir
            // sin normalizar, con componente vertical, o directamente en
            // cero si el jugador solo hizo click sin arrastrar. Se aplana
            // al plano XZ y se cae a "norte" cuando no dice nada, porque
            // normalizar un vector cero devuelve cero y toda la formacion
            // colapsaria sobre el centro.
            var fwd = new Vector3(forward.x, 0f, forward.z);
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            // Perpendicular a la derecha del frente: Cross(up, fwd).
            var right = new Vector3(fwd.z, 0f, -fwd.x);

            var points = new Vector3[count];
            switch (kind)
            {
                case FormationKind.Linea:
                    // Todos a la MISMA profundidad, repartidos a los
                    // costados y centrados en el punto pedido: el centro
                    // de la linea cae donde el jugador hizo click, no su
                    // extremo izquierdo.
                    for (int i = 0; i < count; i++)
                        points[i] = center + right * ((i - (count - 1) * 0.5f) * spacing);
                    break;

                case FormationKind.Cuna:
                    // V con la punta adelante: el primero es la punta y
                    // cada uno de los siguientes se abre un escalon hacia
                    // un costado (alternando) y otro hacia atras.
                    points[0] = center;
                    for (int i = 1; i < count; i++)
                    {
                        int rank = (i + 1) / 2;
                        float side = (i % 2 == 1) ? -1f : 1f;
                        points[i] = center + right * (side * rank * spacing) - fwd * (rank * spacing);
                    }
                    break;

                case FormationKind.Columna:
                    // Fila india sobre el eje del frente: el primero
                    // adelante, en el punto pedido, y cada uno detras del
                    // anterior.
                    for (int i = 0; i < count; i++)
                        points[i] = center - fwd * (i * spacing);
                    break;

                default:
                    FillConcentricRings(points, center, spacing);
                    break;
            }
            return points;
        }

        // Disposicion historica (Cuadricula). Antes todos iban exactamente
        // al mismo punto y terminaban superpuestos en una sola coordenada:
        // anillos concentricos alrededor del punto pedido, el primero se
        // queda en el centro y los siguientes se reparten en circulos de
        // radio creciente. El cuerpo es el de siempre, movido tal cual a
        // un helper para no cambiar ni un decimal del default.
        static void FillConcentricRings(Vector3[] points, Vector3 center, float spacing)
        {
            int count = points.Length;
            if (count > 0) points[0] = center;

            int assigned = 1;
            int ring = 1;
            while (assigned < count)
            {
                float radius = ring * spacing;
                // Cuantos caben en este anillo sin violar la separacion
                // minima entre vecinos del propio anillo.
                int capacity = Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * radius / spacing));
                int take = Mathf.Min(capacity, count - assigned);
                for (int i = 0; i < take; i++)
                {
                    float a = (float)i / take * Mathf.PI * 2f;
                    points[assigned + i] = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                }
                assigned += take;
                ring++;
            }
        }

        // Funcion PURA: desviacion estandar (poblacional) de las
        // distancias de cada posicion al centroide del conjunto. Es la
        // medida objetiva de "que tan desparramada esta la escuadra", y el
        // criterio de aceptacion de RegroupSelection: si reagrupar sirve,
        // este numero baja. Se mide en el plano XZ porque la altura no es
        // dispersion tactica (todos caminan sobre el mismo piso) y solo
        // ensuciaria la comparacion.
        public static float SpreadOf(IReadOnlyList<Vector3> positions)
        {
            if (positions == null || positions.Count == 0) return 0f;

            // Acumuladores en double: con cincuenta unidades y coordenadas
            // de mundo grandes, la suma de cuadrados en float pierde
            // precision suficiente como para que dos mediciones parecidas
            // se ordenen al reves, que es justo lo que se quiere comparar.
            double cx = 0.0, cz = 0.0;
            for (int i = 0; i < positions.Count; i++)
            {
                cx += positions[i].x;
                cz += positions[i].z;
            }
            cx /= positions.Count;
            cz /= positions.Count;

            double sum = 0.0, sumSq = 0.0;
            for (int i = 0; i < positions.Count; i++)
            {
                double dx = positions[i].x - cx;
                double dz = positions[i].z - cz;
                double d = System.Math.Sqrt(dx * dx + dz * dz);
                sum += d;
                sumSq += d * d;
            }

            double mean = sum / positions.Count;
            double variance = sumSq / positions.Count - mean * mean;
            // Con todos a la misma distancia del centroide la resta da un
            // negativo minusculo por redondeo y Sqrt devolveria NaN.
            if (variance < 0.0) variance = 0.0;
            return (float)System.Math.Sqrt(variance);
        }

        // Un destino no es valido si cae encima de un obstaculo: el
        // soldado camina hasta el borde y se queda trabado ahi para
        // siempre, sin que nada avise que la orden no se pudo cumplir.
        public static bool IsValidDestination(Vector3 point)
        {
            // WorldSystemsRegistry en vez de FindObjectsByType: esto solo
            // corre una vez por orden (no por frame), asi que nunca fue el
            // cuello de botella real -- pero el registro ya existe y esta
            // poblado, asi que evitar el barrido es gratis.
            var obstacles = SP.Core.WorldSystemsRegistry.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                if (obstacle == null) continue;
                var d = obstacle.transform.position - point;
                d.y = 0f;
                if (d.magnitude <= obstacle.transform.localScale.x * 0.5f + 0.5f) return false;
            }
            return true;
        }

        // OJO: esto corre UNA VEZ POR SOLDADO. Aca NO va PlayOrderSound()
        // (ver el comentario de AnnounceBatch): con la escuadra entera
        // seleccionada serian cincuenta tonos superpuestos. El sonido de
        // la orden de ataque lo pone IssueAttackOrderForSelection.
        public static void IssueAttackOrder(Soldier soldier, Soldier enemy)
        {
            var brain = soldier.GetComponent<AiBrain>();
            brain?.IssueAttackOrder(enemy);
            OrderMarkerFx.Spawn(enemy.transform.position, OrderMarkerFx.AttackColor);
        }

        // Version de LOTE de la orden de ataque: existia el equivalente
        // suelto en el driver de input (un foreach sobre la seleccion) que
        // no sonaba ni acusaba recibo, asi que atacar era la unica orden
        // sin confirmacion sonora. Recorre la seleccion y suena UNA vez.
        public static void IssueAttackOrderForSelection(IEnumerable<Soldier> selection, Soldier enemy)
        {
            if (selection == null || enemy == null) return;
            var list = new List<Soldier>(selection);
            if (list.Count == 0) return;

            for (int i = 0; i < list.Count; i++) IssueAttackOrder(list[i], enemy);

            AnnounceBatch(list, $"Se dio la orden de atacar a {enemy.DisplayName}");
        }

        public static void IssueMoveOrderForSelection(IEnumerable<Soldier> selection, Vector3 point, bool queued = false)
        {
            // Sin frente pedido, el de siempre: Cuadricula es simetrica,
            // asi que la direccion no cambia nada del resultado historico.
            IssueFormationOrderForSelection(selection, point, Vector3.forward, FormationKind.Cuadricula, queued);
        }

        // El frente de la formacion es un parametro real: quien llama pasa
        // el centro y la direccion (por ejemplo, la del arrastre del mouse
        // desde donde se apreto hasta donde se solto) y la formacion sale
        // rotada hacia ahi. Este metodo NO lee input: recibe centro y
        // direccion ya resueltos, para que el cableado de teclas y mouse
        // quede del lado del driver.
        public static void IssueFormationOrderForSelection(IEnumerable<Soldier> selection, Vector3 center, Vector3 forward, FormationKind kind = FormationKind.Cuadricula, bool queued = false)
        {
            if (selection == null) return;
            var list = new List<Soldier>(selection);
            if (list.Count == 0) return;

            // Se pasa FormationSpacing y no el default de la firma pura:
            // 1.8 es la separacion minima real por debajo de la cual los
            // cubos de este juego se solapan visiblemente.
            var spots = FormationPoints(center, forward, list.Count, kind, FormationSpacing);
            for (int i = 0; i < list.Count; i++) IssueMoveOrder(list[i], spots[i], queued);

            AnnounceBatch(list, list.Count == 1 ? "Se dio la orden de ir a una posicion a 1 soldado" : $"Se dio la orden de ir a una posicion a {list.Count} soldados");
        }

        // Cierre comun de TODA orden de lote: un solo sonido, una linea de
        // log y el destello de los que efectivamente la recibieron.
        static void AnnounceBatch(List<Soldier> list, string logLine)
        {
            // El sonido de orden pertenece al LOTE, no a cada soldado: con
            // uno por soldado, cincuenta seleccionados serian cincuenta
            // tonos superpuestos. Por eso se reproduce aca (que conoce el
            // lote entero) y nunca dentro de IssueMoveOrder /
            // IssueAttackOrder / IssueMountOrder, que corren una vez por
            // soldado.
            PlayOrderSound();
            // 221: el lote es exactamente la granularidad correcta para el
            // historial -- una entrada por orden dada, no una por soldado.
            OrderHistory.Record(logLine, list != null ? list.Count : 0);

            // Antes decia siempre lo mismo sin importar si eran uno o
            // diez soldados: si la seleccion no era la esperada, no habia
            // forma de darse cuenta hasta ver a quien realmente se movio.
            GameLog.Line(logLine);

            // Destello del anillo de los que efectivamente recibieron la
            // orden: el sonido unico no dice QUIENES la recibieron, y si
            // la seleccion no era la esperada no habia forma de notarlo.
            EventBus.Instance.Publish(new OrderAcknowledgedEvent(list.ConvertAll(s => s.Id).ToArray()));
        }

        // Confirmacion de orden: canal Ui y 2D.
        //
        // La version anterior hacia AudioSource.PlayClipAtPoint en
        // cam.transform.position, es decir un sonido POSICIONAL puesto
        // exactamente encima del oyente para simular uno plano. Eso es un
        // 2D mal hecho: pagaba atenuacion, panorama y rolloff para que
        // dieran neutro, y quedaba a merced de donde estuviera la camara
        // ese frame (en vista RTS la camara se mueve sola). Una
        // confirmacion de orden no ocurre en ningun lugar del mundo: la
        // emite la interfaz, no la escena. Va por el canal Ui, que ademas
        // le da al jugador un volumen propio, separado del de efectos.
        //
        // Prioridad alta: es acuse de recibo de algo que el jugador acaba
        // de pedir. Si se lo come el limite de voces, el juego parece que
        // ignoro la orden. Ademas el canal Ui tiene reserva propia (6
        // voces), asi que no compite contra el tiroteo.
        static void PlayUi(SfxKind kind, float volume, float priority)
        {
            // La suite headless corre en Edit mode. AudioDirector lo vuelve
            // a chequear; salir antes evita hasta generar el clip.
            if (!Application.isPlaying) return;
            // Si no hay director todavia, PlayUi2D devuelve false en
            // silencio en vez de tirar.
            AudioDirector.PlayUi2D(kind, volume, priority);
        }

        static void PlayOrderSound() => PlayUi(SfxKind.Order, 0.5f, 0.9f);

        // El "no" mecanico va todavia mas arriba que la confirmacion: es la
        // UNICA senal de que la accion se rechazo. Perderla deja al jugador
        // creyendo que el juego no registro el click.
        public static void PlayRejectSound() => PlayUi(SfxKind.EmptyClick, 0.6f, 0.95f);

        // "Que me sigan": normalmente leader es Brain.Current (el soldado
        // poseido). No entra en el patron de a-un-punto-fijo del resto:
        // no hay OrderMarkerFx.Spawn por soldado porque el destino se
        // mueve con el lider, un cubo fijo en el piso mentiria apenas
        // caminara un paso.
        public static void IssueFollowOrder(Soldier soldier, Soldier leader)
        {
            var brain = soldier.GetComponent<AiBrain>();
            if (brain != null) brain.IsPossessedByPlayer = false;
            brain?.IssueFollowOrder(leader);
        }

        public static void IssueFollowOrderForSelection(IEnumerable<Soldier> selection, Soldier leader)
        {
            if (selection == null || leader == null) return;
            var list = new List<Soldier>(selection);
            if (list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == leader) continue;
                IssueFollowOrder(list[i], leader);
            }

            OrderMarkerFx.Spawn(leader.transform.position, OrderMarkerFx.FollowColor);
            AnnounceBatch(list, list.Count == 1 ? "Se dio la orden de seguir a 1 soldado" : $"Se dio la orden de seguir a {list.Count} soldados");
        }

        public static void IssueMountOrder(Soldier soldier, Vehicle vehicle)
        {
            var brain = soldier.GetComponent<AiBrain>();
            brain?.IssueMountOrder(vehicle);
            OrderMarkerFx.Spawn(vehicle.transform.position, OrderMarkerFx.MountColor);
        }

        public static void IssueMountOrderForSelection(IEnumerable<Soldier> selection, Vehicle vehicle)
        {
            if (selection == null || vehicle == null) return;
            var list = new List<Soldier>(selection);
            if (list.Count == 0) return;

            for (int i = 0; i < list.Count; i++) IssueMountOrder(list[i], vehicle);

            AnnounceBatch(list, "Se dio la orden de ir al auto");
        }

        // Reagrupa a los seleccionados VIVOS alrededor de su propio
        // centroide. Devuelve los destinos emitidos (array vacio si no
        // quedaba nadie vivo) justamente para poder comparar
        // SpreadOf(posiciones de antes) contra SpreadOf(destinos) sin
        // entrar en Play mode: ese es el criterio de aceptacion medible,
        // la desviacion estandar de las distancias al centroide tiene que
        // bajar.
        public static Vector3[] RegroupSelection(IEnumerable<Soldier> selection, FormationKind kind = FormationKind.Cuadricula)
        {
            if (selection == null) return new Vector3[0];

            // Solo los VIVOS: un caido seguiria pesando en el centroide y
            // arrastraria a toda la escuadra hacia el lugar donde murio.
            var alive = new List<Soldier>();
            foreach (var s in selection)
                if (s != null && s.Health != null && s.Health.IsAlive) alive.Add(s);
            if (alive.Count == 0) return new Vector3[0];

            Vector3 centroid = Vector3.zero;
            Vector3 facing = Vector3.zero;
            for (int i = 0; i < alive.Count; i++)
            {
                centroid += alive[i].transform.position;
                // Frente promedio de la escuadra: para Cuadricula da lo
                // mismo (es simetrica) pero Linea, Cuna y Columna sin un
                // frente sensato saldrian siempre mirando al norte.
                facing += alive[i].transform.forward;
            }
            centroid /= alive.Count;

            var spots = FormationPoints(centroid, facing, alive.Count, kind, FormationSpacing);

            // Asignacion codiciosa: cada punto se lo lleva el soldado vivo
            // libre mas cercano. Sin esto manda el orden de la seleccion y
            // la escuadra se cruza entera para reagruparse. Es O(n^2) UNA
            // vez por orden -- no por soldado ni por frame: con cincuenta
            // son 2500 comparaciones de distancia en el frame de la tecla.
            var taken = new bool[alive.Count];
            for (int j = 0; j < spots.Length; j++)
            {
                int best = -1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < alive.Count; i++)
                {
                    if (taken[i]) continue;
                    float sqr = (alive[i].transform.position - spots[j]).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; best = i; }
                }
                if (best < 0) continue;
                taken[best] = true;
                IssueMoveOrder(alive[best], spots[j]);
            }

            AnnounceBatch(alive, alive.Count == 1 ? "Se reagrupo a 1 soldado" : $"Se reagrupo a {alive.Count} soldados");
            return spots;
        }

        // Cuanto se aleja cada soldado al retirarse.
        public const float RetreatDistance = 15f;

        // Horizonte de amenaza de la busqueda por grilla. Cubre de sobra
        // el rango de alerta del juego (30) y el de la torreta (40); si no
        // hay nadie adentro se cae al barrido lineal del registro.
        const float RetreatThreatRange = 60f;

        // INTERPRETACION ACOTADA DE "RETIRADA" -- decision de diseno
        // explicita. El proyecto no tiene NINGUN concepto de "punto
        // seguro": no hay datos de cobertura, ni zonas, ni un spawn propio
        // marcado como refugio (ObstacleMarker es un obstaculo que BLOQUEA
        // el paso, no una cobertura detras de la cual parapetarse).
        // Inventar un sistema de coberturas seria diseno nuevo, no esta
        // orden. Asi que "retirarse" se define como lo unico que el mundo
        // actual permite definir sin ambiguedad: cada soldado se aleja en
        // linea recta del enemigo vivo mas cercano, una distancia fija, y
        // el destino se acota a los limites del mapa. El dia que existan
        // coberturas, este metodo es el unico lugar a cambiar.
        //
        // mapHalfExtent: el unico limite de mapa del proyecto vive en
        // CameraRig como campo serializado PRIVADO (mapHalfExtent = 90) y
        // no lo expone ninguna API, asi que se recibe como parametro con
        // ese mismo valor por defecto en vez de espiarlo por reflexion.
        public static void IssueRetreatOrderForSelection(IEnumerable<Soldier> selection, float distance = RetreatDistance, float mapHalfExtent = 90f)
        {
            if (selection == null) return;

            var list = new List<Soldier>();
            foreach (var s in selection)
                if (s != null && s.Health != null && s.Health.IsAlive) list.Add(s);
            if (list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                var soldier = list[i];
                var pos = soldier.transform.position;

                // Se reusan las busquedas que YA existen en vez de barrer
                // la escena de nuevo: primero la acotada por grilla (mira
                // solo las celdas vecinas) y, solo si no hay nadie en el
                // horizonte de amenaza, el barrido lineal del registro.
                // Nada de FindObjectsByType por soldado.
                var enemy = ActorRegistry.FindNearestEnemyInRange(pos, soldier.Team, RetreatThreatRange);
                if (enemy == null)
                    enemy = ActorRegistry.FindNearest(pos, e => e.Health != null && e.Health.IsAlive && e.Team != soldier.Team);

                // Sin ningun enemigo vivo no hay de que huir: retroceder
                // "hacia atras del enemigo" no esta definido, asi que se
                // retira de espaldas a su propio frente, que es la unica
                // direccion con sentido disponible.
                Vector3 away = enemy != null ? pos - enemy.transform.position : -soldier.transform.forward;
                away.y = 0f;
                // Exactamente encima del enemigo la resta da cero y
                // normalized devolveria (0,0,0): la orden seria "quedate
                // donde estas", justo lo contrario de retirarse.
                away = away.sqrMagnitude < 0.0001f ? Vector3.forward : away.normalized;

                Vector3 dest = pos + away * distance;
                dest.x = Mathf.Clamp(dest.x, -mapHalfExtent, mapHalfExtent);
                dest.z = Mathf.Clamp(dest.z, -mapHalfExtent, mapHalfExtent);
                IssueMoveOrder(soldier, dest);
            }

            AnnounceBatch(list, list.Count == 1 ? "Se dio la orden de retirada a 1 soldado" : $"Se dio la orden de retirada a {list.Count} soldados");
        }
    }
}
