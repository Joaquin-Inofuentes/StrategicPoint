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

        // Antes todos iban exactamente al mismo punto y terminaban
        // superpuestos en una sola coordenada. Anillos concentricos
        // alrededor del punto pedido: el primero se queda en el centro,
        // los siguientes se reparten en circulos de radio creciente.
        public static Vector3[] FormationPoints(Vector3 center, int count)
        {
            var points = new Vector3[count];
            if (count > 0) points[0] = center;

            int assigned = 1;
            int ring = 1;
            while (assigned < count)
            {
                float radius = ring * FormationSpacing;
                // Cuantos caben en este anillo sin violar la separacion
                // minima entre vecinos del propio anillo.
                int capacity = Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * radius / FormationSpacing));
                int take = Mathf.Min(capacity, count - assigned);
                for (int i = 0; i < take; i++)
                {
                    float a = (float)i / take * Mathf.PI * 2f;
                    points[assigned + i] = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                }
                assigned += take;
                ring++;
            }
            return points;
        }

        // Un destino no es valido si cae encima de un obstaculo: el
        // soldado camina hasta el borde y se queda trabado ahi para
        // siempre, sin que nada avise que la orden no se pudo cumplir.
        public static bool IsValidDestination(Vector3 point)
        {
            foreach (var obstacle in Object.FindObjectsByType<ObstacleMarker>(FindObjectsSortMode.None))
            {
                var d = obstacle.transform.position - point;
                d.y = 0f;
                if (d.magnitude <= obstacle.transform.localScale.x * 0.5f + 0.5f) return false;
            }
            return true;
        }

        public static void IssueAttackOrder(Soldier soldier, Soldier enemy)
        {
            var brain = soldier.GetComponent<AiBrain>();
            brain?.IssueAttackOrder(enemy);
            OrderMarkerFx.Spawn(enemy.transform.position, OrderMarkerFx.AttackColor);
        }

        public static void IssueMoveOrderForSelection(IEnumerable<Soldier> selection, Vector3 point, bool queued = false)
        {
            var list = new List<Soldier>(selection);
            if (list.Count == 0) return;

            var spots = FormationPoints(point, list.Count);
            for (int i = 0; i < list.Count; i++) IssueMoveOrder(list[i], spots[i], queued);

            // El sonido de orden pertenece al LOTE, no a cada soldado: con
            // uno por soldado, cincuenta seleccionados serian cincuenta
            // tonos superpuestos. Por eso se reproduce aca (que conoce el
            // lote entero) y no en IssueMoveOrder.
            PlayOrderSound();

            // Antes decia siempre lo mismo sin importar si eran uno o
            // diez soldados: si la seleccion no era la esperada, no habia
            // forma de darse cuenta hasta ver a quien realmente se movio.
            GameLog.Line(list.Count == 1 ? "Se dio la orden de ir a una posicion a 1 soldado" : $"Se dio la orden de ir a una posicion a {list.Count} soldados");

            // Destello del anillo de los que efectivamente recibieron la
            // orden: el sonido unico no dice QUIENES la recibieron, y si
            // la seleccion no era la esperada no habia forma de notarlo.
            EventBus.Instance.Publish(new OrderAcknowledgedEvent(list.ConvertAll(s => s.Id).ToArray()));
        }

        // PlayClipAtPoint crea un GameObject que se autodestruye con
        // Destroy(), ilegal fuera de Play mode -- y el test headless corre
        // las fases en Edit mode.
        static void PlayAt(SfxKind kind, float volume)
        {
            if (!Application.isPlaying) return;
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(GenericSfx.Get(kind), cam != null ? cam.transform.position : Vector3.zero, volume);
        }

        static void PlayOrderSound() => PlayAt(SfxKind.Order, 0.5f);

        public static void PlayRejectSound() => PlayAt(SfxKind.EmptyClick, 0.6f);

        public static void IssueMountOrder(Soldier soldier, Vehicle vehicle)
        {
            var brain = soldier.GetComponent<AiBrain>();
            brain?.IssueMountOrder(vehicle);
            OrderMarkerFx.Spawn(vehicle.transform.position, OrderMarkerFx.MountColor);
        }

        public static void IssueMountOrderForSelection(IEnumerable<Soldier> selection, Vehicle vehicle)
        {
            foreach (var s in selection) IssueMountOrder(s, vehicle);
            GameLog.Line("Se dio la orden de ir al auto");
        }
    }
}
