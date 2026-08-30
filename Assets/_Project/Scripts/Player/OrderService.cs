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

        public static void IssueMoveOrder(Soldier soldier, Vector3 point)
        {
            var brain = soldier.GetComponent<AiBrain>();
            // Una orden explícita manda igual aunque el soldado sea el que
            // estás poseyendo: en RTS no lo estás manejando con WASD, así
            // que "IsPossessedByPlayer" no debería frenar a la IA acá (antes
            // seleccionar tu propio soldado y darle "ir ahí" no hacía nada).
            if (brain != null) brain.IsPossessedByPlayer = false;
            brain?.IssueMoveOrder(point);
            EventBus.Instance.Publish(new MoveOrderIssuedEvent(soldier.Id, point));
            OrderMarkerFx.Spawn(point, OrderMarkerFx.MoveColor);
        }

        public static void IssueAttackOrder(Soldier soldier, Soldier enemy)
        {
            var brain = soldier.GetComponent<AiBrain>();
            brain?.IssueAttackOrder(enemy);
            OrderMarkerFx.Spawn(enemy.transform.position, OrderMarkerFx.AttackColor);
        }

        public static void IssueMoveOrderForSelection(IEnumerable<Soldier> selection, Vector3 point)
        {
            int count = 0;
            foreach (var s in selection) { IssueMoveOrder(s, point); count++; }
            // Antes decia siempre lo mismo sin importar si eran uno o
            // diez soldados: si la seleccion no era la esperada, no habia
            // forma de darse cuenta hasta ver a quien realmente se movio.
            GameLog.Line(count == 1 ? "Se dio la orden de ir a una posicion a 1 soldado" : $"Se dio la orden de ir a una posicion a {count} soldados");
        }

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
