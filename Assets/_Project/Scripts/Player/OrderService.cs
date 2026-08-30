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
            foreach (var s in selection) IssueMoveOrder(s, point);
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
        }
    }
}
