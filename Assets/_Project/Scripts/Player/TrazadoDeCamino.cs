using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Presentation;

namespace SP.Player
{
    // Trazar un recorrido con [Ctrl] y arrancarlo con [Espacio].
    //
    // La diferencia con Shift+click (que ya encolaba ordenes) es cuando se
    // decide: con Shift cada click sale al instante y no hay vuelta atras;
    // aca el recorrido se dibuja entero, se mira, y recien despues se
    // ejecuta -- o se descarta con [X] sin que nadie se haya movido.
    //
    // Estatico y con UN solo trazado a la vez: lo dibuja el jugador, y el
    // jugador es uno. La cola de ordenes de verdad vive en AiBrain; esto
    // es nada mas la lista de puntos hasta que se aprieta Espacio.
    public static class TrazadoDeCamino
    {
        // Tope para que el trazado no crezca sin fin apoyado en Ctrl: el
        // pool de marcadores tiene 64 y son compartidos con las ordenes
        // normales.
        public const int MaximoDePuntos = 12;
        // Dos puntos mas juntos que esto son el mismo punto: sin el corte,
        // un click que resbala un pixel agrega un tramo de 10 cm que el
        // soldado cumple sin moverse.
        public const float SeparacionMinima = 1.5f;

        static readonly List<Vector3> puntos = new List<Vector3>();

        public static IReadOnlyList<Vector3> Puntos => puntos;
        public static int Cantidad => puntos.Count;
        public static bool HayTrazado => puntos.Count > 0;

        // Devuelve false y no agrega nada si el punto no sirve. El llamador
        // usa eso para avisar por pantalla en vez de fallar en silencio.
        public static bool Marcar(Vector3 punto)
        {
            if (puntos.Count >= MaximoDePuntos) return false;
            if (!OrderService.IsValidDestination(punto)) return false;
            if (puntos.Count > 0 && (puntos[puntos.Count - 1] - punto).sqrMagnitude < SeparacionMinima * SeparacionMinima)
                return false;

            puntos.Add(punto);
            // Se dibuja con el marcador de cola (numerado y fijo, no se
            // desvanece) porque eso es exactamente lo que es: un tramo
            // planificado todavia sin cumplir.
            OrderMarkerFx.Spawn(punto, OrderMarkerFx.MoveColor, puntos.Count);
            return true;
        }

        public static void Limpiar()
        {
            if (puntos.Count == 0) return;
            puntos.Clear();
            OrderMarkerFx.ClearQueuedMarkers();
        }

        // Manda el recorrido entero a la seleccion. El primer tramo
        // reemplaza lo que estuvieran haciendo; los demas se encolan
        // detras. Devuelve cuantos tramos se emitieron (0 si no habia
        // trazado o no habia a quien mandarselo).
        public static int Ejecutar(IReadOnlyList<Soldier> seleccion)
        {
            if (puntos.Count == 0 || seleccion == null || seleccion.Count == 0) return 0;

            var vivos = new List<Soldier>();
            foreach (var s in seleccion)
                if (s != null && s.Health != null && s.Health.IsAlive) vivos.Add(s);
            if (vivos.Count == 0) return 0;

            for (int i = 0; i < puntos.Count; i++)
            {
                // El frente de cada tramo es la direccion desde el punto
                // anterior: con varios soldados, la formacion se acomoda
                // mirando hacia donde van y no siempre al norte.
                Vector3 frente = i == 0 ? Vector3.forward : (puntos[i] - puntos[i - 1]);
                frente.y = 0f;
                if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;

                OrderService.IssueFormationOrderForSelection(vivos, puntos[i], frente.normalized,
                    FormationKind.Cuadricula, queued: i > 0);
            }

            int tramos = puntos.Count;
            GameLog.Line($"Se ejecuto un recorrido de {tramos} tramos con {vivos.Count} soldados");
            // Los puntos se sueltan, pero los marcadores NO: a partir de
            // aca representan la cola real del AiBrain y se van apagando a
            // medida que cada tramo se cumple.
            puntos.Clear();
            return tramos;
        }
    }
}
