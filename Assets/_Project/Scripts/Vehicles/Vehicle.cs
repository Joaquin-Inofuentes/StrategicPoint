using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Vehicles
{
    // Vehículo con 4 asientos: conductor, artillero y dos pasajeros. Los
    // soldados que suben quedan ocultos (su cuerpo se desactiva) y el
    // vehículo pasa a moverlos a todos con su propio transform.
    public class Vehicle : MonoBehaviour
    {
        // Reusa el mismo componente Health que los soldados (no se registra
        // en ActorRegistry: eso es solo para sensado de soldados, el
        // vehículo no lo necesita). Le da al tanque vida real -- antes un
        // proyectil que le pegaba solo disparaba el flash de la mirilla,
        // sin bajarle nada.
        [SerializeField] int maxHealth = 260;
        Health health;
        bool healthBootstrapped;

        public Health Health
        {
            get
            {
                if (!healthBootstrapped)
                {
                    healthBootstrapped = true;
                    health = GetComponent<Health>();
                    if (health == null) health = gameObject.AddComponent<Health>();
                    health.Initialize(-1, maxHealth);
                }
                return health;
            }
        }

        public void TakeDamage(int amount, int attackerId) => Health.TakeDamage(amount, attackerId);

        // Orden de asignación automática: pasajero antes que artillero, para
        // que ese asiento quede libre si el jugador quiere pasarse ahí (2).
        static readonly VehicleSeatRole[] AllRoles =
        {
            VehicleSeatRole.Driver, VehicleSeatRole.Passenger1, VehicleSeatRole.Passenger2, VehicleSeatRole.Gunner
        };

        readonly Dictionary<VehicleSeatRole, Soldier> seats = new Dictionary<VehicleSeatRole, Soldier>();

        // Feedback de color: el chasis se pone un poco más oscuro/saturado
        // cuando tiene gente adentro, y vuelve a su color de base al vaciarse.
        Renderer[] chassisRenderers;
        Color baseColor;
        bool colorCached;

        void CacheColorIfNeeded()
        {
            if (colorCached) return;
            colorCached = true;
            chassisRenderers = GetComponentsInChildren<Renderer>();
            if (chassisRenderers.Length > 0) baseColor = chassisRenderers[0].sharedMaterial.color;
        }

        public void RefreshOccupancyColor()
        {
            CacheColorIfNeeded();
            if (chassisRenderers == null || chassisRenderers.Length == 0) return;
            Color target = seats.Count > 0 ? Color.Lerp(baseColor, Color.black, 0.28f) : baseColor;
            foreach (var r in chassisRenderers) r.sharedMaterial.color = target;
        }

        public int Capacity => AllRoles.Length;
        public int OccupantCount => seats.Count;
        public bool HasAnyRoom => seats.Count < Capacity;
        public Soldier Driver => seats.TryGetValue(VehicleSeatRole.Driver, out var s) ? s : null;
        public Soldier Gunner => seats.TryGetValue(VehicleSeatRole.Gunner, out var s) ? s : null;

        public IReadOnlyList<Soldier> Occupants
        {
            get
            {
                var list = new List<Soldier>();
                foreach (var role in AllRoles) if (seats.TryGetValue(role, out var s)) list.Add(s);
                return list;
            }
        }

        public bool IsSeatFree(VehicleSeatRole role) => !seats.ContainsKey(role);

        public VehicleSeatRole? FirstFreeSeat()
        {
            foreach (var role in AllRoles) if (!seats.ContainsKey(role)) return role;
            return null;
        }

        public bool Mount(Soldier soldier, VehicleSeatRole? preferredRole = null)
        {
            if (soldier == null) return false;
            foreach (var kv in seats) if (kv.Value == soldier) return false; // ya está adentro

            VehicleSeatRole role;
            if (preferredRole.HasValue && IsSeatFree(preferredRole.Value))
            {
                role = preferredRole.Value;
            }
            else
            {
                var free = FirstFreeSeat();
                if (free == null) return false;
                role = free.Value;
            }

            seats[role] = soldier;

            var brain = soldier.GetComponent<AiBrain>();
            if (brain != null) brain.enabled = false;
            soldier.gameObject.SetActive(false);
            RefreshOccupancyColor();
            return true;
        }

        // Baja a un soldado y lo reaparece junto al vehículo.
        public bool Dismount(Soldier soldier)
        {
            VehicleSeatRole? foundRole = null;
            foreach (var kv in seats) if (kv.Value == soldier) { foundRole = kv.Key; break; }
            if (foundRole == null) return false;

            seats.Remove(foundRole.Value);

            soldier.gameObject.SetActive(true);
            soldier.transform.position = transform.position + transform.right * 2.5f;
            soldier.transform.rotation = transform.rotation;

            var brain = soldier.GetComponent<AiBrain>();
            if (brain != null) brain.enabled = true;
            RefreshOccupancyColor();
            return true;
        }

        public VehicleSeatRole? RoleOf(Soldier soldier)
        {
            foreach (var kv in seats) if (kv.Value == soldier) return kv.Key;
            return null;
        }
    }
}
