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

        // Antes el tanque podía llegar a 0 de vida y seguir manejándose,
        // disparando y subiendo gente como si nada -- la barra de vida
        // bajaba pero no pasaba NADA. Ahora, al morir: expulsa a todos
        // los ocupantes, se apagan motor/torreta/IA, y el chasis queda
        // bien oscuro (carcasa quemada) en vez de su color normal.
        public bool IsDestroyed { get; private set; }

        public void TakeDamage(int amount, int attackerId)
        {
            bool wasAlive = Health.IsAlive;
            Health.TakeDamage(amount, attackerId);

            // El cacheo del color base tiene que pasar ANTES de publicar el
            // evento. El bus es sincrono: VehicleFxReactor pinta el chasis
            // del dorado de chispa dentro del Publish de abajo, y si
            // CacheColorIfNeeded corria recien despues (desde OnDestroyed)
            // se quedaba guardando ESE dorado como color base -- el tanque
            // destruido terminaba mostaza en vez de su color de equipo
            // oscurecido. Verificado: el chasis quemado daba (0.150,0.127,
            // 0.075) en vez del (0.147,0.098,0.023) que corresponde.
            CacheColorIfNeeded();

            // Antes un impacto en el vehiculo solo bajaba una barra --
            // ningun flash en el chasis, ningun sonido distinto al de un
            // soldado. Evento propio para que la reaccion visual/sonora
            // del vehiculo sea la suya, no la de carne y hueso.
            if (wasAlive) EventBus.Instance.Publish(new VehicleDamagedEvent(this, amount, Health.Current, Health.MaxHealth));
            if (wasAlive && !Health.IsAlive) OnDestroyed();
        }

        // Antes esto apagaba todo y oscurecia el chasis en un SOLO frame:
        // la muerte del elemento mas poderoso del campo era visualmente
        // anticlimatica. Ahora son dos etapas con un intervalo entre
        // medio: agonia (sistemas caidos, humo, todavia reconocible) y
        // explosion final (torreta por el aire, casco quemado).
        public const float AgonySeconds = 1.2f;
        public bool IsInAgony { get; private set; }
        public bool FinalExplosionDone { get; private set; }

        void OnEnable() => SP.Core.WorldSystemsRegistry.Register(this);
        void OnDisable() => SP.Core.WorldSystemsRegistry.Unregister(this);

        void OnDestroyed()
        {
            IsDestroyed = true;
            IsInAgony = true;

            foreach (var occupant in new List<Soldier>(Occupants)) Dismount(occupant);

            var motor = GetComponent<VehicleMotor>();
            if (motor != null) motor.enabled = false;
            var vb = GetComponent<VehicleBrain>();
            if (vb != null) vb.enabled = false;
            foreach (var turret in GetComponentsInChildren<TurretWeapon>()) turret.enabled = false;
            foreach (var ai in GetComponentsInChildren<TurretAI>()) ai.enabled = false;

            // Etapa 1: se apaga, pero todavia con su color reconocible --
            // solo un poco apagado. El negro de carcasa quemada es de la
            // etapa 2, si no la explosion final no tendria nada que
            // cambiar visualmente.
            CacheColorIfNeeded();
            if (chassisRenderers != null)
                foreach (var r in chassisRenderers) if (r != null) r.sharedMaterial.color = Color.Lerp(baseColor, Color.black, 0.35f);

            EventBus.Instance.Publish(new VehicleDestroyedEvent(this));

            if (Application.isPlaying) Invoke(nameof(FinalExplosion), AgonySeconds);
            else FinalExplosion(); // en Edit mode (suite headless) no hay Invoke util
        }

        public void FinalExplosion()
        {
            if (FinalExplosionDone) return;
            FinalExplosionDone = true;
            IsInAgony = false;

            if (chassisRenderers != null)
                foreach (var r in chassisRenderers) if (r != null) r.sharedMaterial.color = Color.Lerp(baseColor, Color.black, 0.85f);

            SP.Presentation.ImpactFx.SpawnExplosion(transform.position + Vector3.up, 4f);
            DetachTurret();
        }

        // La destruccion del tanque no tenia una señal legible a
        // distancia: en vista RTS costaba saber si murio. Un cañon
        // volando por el aire se ve desde cualquier zoom.
        void DetachTurret()
        {
            var turret = GetComponentInChildren<TurretWeapon>(true);
            if (turret == null) return;
            var t = turret.transform;
            if (t.parent == null) return;

            t.SetParent(null, true);
            var flier = t.gameObject.AddComponent<DetachedTurretFlight>();
            flier.Launch();
        }

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

        // Lo pone/saca PlayerInputDriver al entrar/salir de un asiento
        // (cualquiera, no solo artillero). Mismo patron que
        // VehicleBrain.IsPlayerDriving: barato de mantener porque solo
        // cambia en esos dos puntos, y evita que quien necesite saber
        // "esta el jugador ADENTRO de este vehiculo" (la vibracion del
        // cañon, item pedido explicito) tenga que salir a buscar al
        // PlayerInputDriver con un Find por disparo.
        public bool PlayerAboard { get; set; }

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
            // BUG REAL: esto no chequeaba vida. Un muerto se montaba
            // igual (devolvia true, sumaba al conteo de ocupantes) porque
            // nada en el camino de Mount() miraba Health.IsAlive -- cada
            // llamador (el auto-mount de EnterVehicle, IssueMountOrder,
            // etc.) tenia que acordarse de filtrar por su cuenta, y no
            // todos lo hacian. La guarda va aca, en la fuente unica, para
            // que ningun camino futuro pueda repetir el olvido.
            if (soldier == null || IsDestroyed || !soldier.Health.IsAlive) return false;
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

            var brain = soldier.Brain;
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
            // Antes todos bajaban exactamente al mismo punto (derecha del
            // chasis), sin importar el asiento -- con varios ocupantes
            // quedaban superpuestos o dentro del chasis. Cada asiento
            // baja por su propio costado.
            soldier.transform.position = transform.position + DismountOffsetFor(foundRole.Value);
            soldier.transform.rotation = transform.rotation;

            var brain = soldier.Brain;
            if (brain != null) brain.enabled = true;
            RefreshOccupancyColor();
            return true;
        }

        // Un costado y una distancia distinta por asiento: conductor a la
        // izquierda, artillero a la derecha, pasajeros atras a cada lado
        // -- para que cuatro ocupantes bajando a la vez no terminen los
        // cuatro en el mismo punto ni adentro del chasis.
        Vector3 DismountOffsetFor(VehicleSeatRole role)
        {
            switch (role)
            {
                case VehicleSeatRole.Driver: return -transform.right * 2.5f;
                case VehicleSeatRole.Gunner: return transform.right * 2.5f;
                case VehicleSeatRole.Passenger1: return -transform.right * 2.5f - transform.forward * 2f;
                case VehicleSeatRole.Passenger2: return transform.right * 2.5f - transform.forward * 2f;
                default: return transform.right * 2.5f;
            }
        }

        public VehicleSeatRole? RoleOf(Soldier soldier)
        {
            foreach (var kv in seats) if (kv.Value == soldier) return kv.Key;
            return null;
        }
    }
}
