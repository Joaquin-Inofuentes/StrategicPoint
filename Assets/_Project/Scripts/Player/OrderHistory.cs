using System.Collections.Generic;
using UnityEngine;

namespace SP.Player
{
    // Item 221: historial de ordenes.
    //
    // El jugador daba una orden a un lote y a los pocos segundos ya no
    // tenia forma de saber que habia pedido ni a quienes: el unico rastro
    // era el marcador en el suelo, que se desvanece. Con varias ordenes
    // encadenadas era imposible reconstruir el plan.
    //
    // Buffer circular estatico y de tamaño fijo: registrar ordenes NO
    // puede crecer sin limite en una partida larga, y un historial es
    // justamente algo de lo que solo importan las ultimas entradas.
    public static class OrderHistory
    {
        public const int Capacity = 12;

        public readonly struct Entry
        {
            public readonly string Description;
            public readonly int ActorCount;
            public readonly float Time;

            public Entry(string description, int actorCount, float time)
            {
                Description = description;
                ActorCount = actorCount;
                Time = time;
            }
        }

        static readonly Entry[] buffer = new Entry[Capacity];
        static int head;   // proxima posicion a escribir
        static int count;

        public static int Count => count;

        public static void Record(string description, int actorCount)
        {
            if (string.IsNullOrEmpty(description)) return;
            // unscaledTime: el historial es informacion de interfaz, no
            // deberia estirarse con la camara lenta ni congelarse en pausa.
            buffer[head] = new Entry(description, actorCount, Time.unscaledTime);
            head = (head + 1) % Capacity;
            if (count < Capacity) count++;
        }

        // De la mas reciente a la mas vieja. index 0 = la ultima orden.
        public static bool TryGet(int index, out Entry entry)
        {
            entry = default;
            if (index < 0 || index >= count) return false;
            int pos = ((head - 1 - index) % Capacity + Capacity) % Capacity;
            entry = buffer[pos];
            return true;
        }

        public static void Clear()
        {
            count = 0;
            head = 0;
        }

        // Texto listo para un panel, de la mas reciente hacia abajo.
        public static string RecentText(int maxLines = 5)
        {
            var sb = new System.Text.StringBuilder();
            int n = Mathf.Min(maxLines, count);
            for (int i = 0; i < n; i++)
            {
                Entry e;
                if (!TryGet(i, out e)) break;
                if (i > 0) sb.Append('\n');
                sb.Append(e.Description);
                if (e.ActorCount > 0) sb.Append("  (").Append(e.ActorCount).Append(')');
            }
            return sb.ToString();
        }

        // Solo para tests y para reiniciar entre partidas: los estaticos
        // sobreviven a la recarga de escena.
        public static IReadOnlyList<Entry> Snapshot()
        {
            var list = new List<Entry>(count);
            for (int i = 0; i < count; i++)
            {
                Entry e;
                if (TryGet(i, out e)) list.Add(e);
            }
            return list;
        }
    }
}
