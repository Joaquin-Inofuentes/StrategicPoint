using System;
using System.Collections;
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
            if (SP.Core.ModoDios.Protege(Health)) return;   // [F4]
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

        // Las dos torretas se resuelven UNA vez (antes UpdateInVehicle hacia dos Transform.Find por frame).
        // Cada una se busca por su pivote, sin adivinar (hay dos TurretWeapon: canon y metralleta).
        TurretWeapon torretaCanon, torretaMetralleta;
        bool torretasResueltas;
        public TurretWeapon TorretaCanon { get { ResolverTorretas(); return torretaCanon; } }
        public TurretWeapon TorretaMetralleta { get { ResolverTorretas(); return torretaMetralleta; } }
        void ResolverTorretas()
        {
            if (torretasResueltas) return;
            torretasResueltas = true;
            var c = transform.Find("TurretMount/TurretPivot");
            torretaCanon = c != null ? c.GetComponent<TurretWeapon>() : null;
            var m = transform.Find("MetralletaMount/MetralletaPivot");
            torretaMetralleta = m != null ? m.GetComponent<TurretWeapon>() : null;
        }
        public bool FinalExplosionDone { get; private set; }

        // Todos los vehiculos activos (para que la IA de las torretas pueda
        // elegir un tanque enemigo como blanco).
        public static readonly List<Vehicle> Todos = new List<Vehicle>();

        GameObject diamondMarker;
        Material diamondMaterial;

        void OnEnable() 
        { 
            SP.Core.WorldSystemsRegistry.Register(this); 
            if (!Todos.Contains(this)) Todos.Add(this); 
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.Contains("Sphere") || r.GetComponent<SphereCollider>() != null) r.enabled = false;
            }
            if (diamondMarker == null)
            {
                diamondMaterial = SP.Presentation.DiamondGizmo.NuevoMaterial(SP.Presentation.DiamondGizmo.ColorVacio);
                diamondMarker = SP.Presentation.DiamondGizmo.CrearCara("RomboTanque", transform, 1.8f, diamondMaterial);
                diamondMarker.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            }
            RefreshOccupancyColor();
        }
        void OnDisable() { SP.Core.WorldSystemsRegistry.Unregister(this); Todos.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTodos() => Todos.Clear();

        // Equipo "dueño" del vehiculo (el de su tripulacion original): sirve
        // para pintarlo y para que las torretas sepan si es hostil.
        public SP.Combat.TeamId Bando { get; private set; } = SP.Combat.TeamId.Player;

        // Tiñe el casco con el color del equipo (los tanques enemigos rojos).
        public void AsignarBando(SP.Combat.TeamId bando, Color tinte)
        {
            Bando = bando;
            CacheColorIfNeeded();
            baseColor = tinte;
            if (chassisRenderers != null)
                foreach (var r in chassisRenderers) if (r != null && r.sharedMaterial != null) r.sharedMaterial.color = tinte;
        }

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
            // BUG REAL que esto corrige: GetComponentInChildren<TurretWeapon>()
            // devolvia CUALQUIERA de los dos TurretWeapon del tanque desde
            // que existe MetralletaPivot -- segun el orden de la jerarquia,
            // la explosion final podia terminar lanzando por el aire la
            // metralleta chica en vez del cañon grande, justo lo opuesto
            // de "se ve desde cualquier zoom" que pide el comentario de
            // arriba. Se busca la torreta del cañon por nombre, sin
            // adivinar.
            var turretPivotT = transform.Find("TurretMount/TurretPivot");
            var turret = turretPivotT != null ? turretPivotT.GetComponent<TurretWeapon>() : GetComponentInChildren<TurretWeapon>(true);
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
        readonly Dictionary<Soldier, Coroutine> mountAnimations = new Dictionary<Soldier, Coroutine>();
        readonly Dictionary<Soldier, Vector3> mountTrueScale = new Dictionary<Soldier, Vector3>();

        public bool IsMountAnimating(Soldier soldier) => mountAnimations.ContainsKey(soldier);

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
            for (int i = 0; i < chassisRenderers.Length; i++)
            {
                var r = chassisRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                r.sharedMaterial = new Material(r.sharedMaterial);
            }
            if (chassisRenderers.Length > 0 && chassisRenderers[0].sharedMaterial != null) baseColor = chassisRenderers[0].sharedMaterial.color;
        }

        public void RefreshOccupancyColor()
        {
            CacheColorIfNeeded();
            if (chassisRenderers != null && chassisRenderers.Length > 0)
            {
                Color target = seats.Count > 0 ? Color.Lerp(baseColor, Color.black, 0.28f) : baseColor;
                foreach (var r in chassisRenderers) r.sharedMaterial.color = target;
            }

            if (diamondMaterial != null)
            {
                Color diamondTarget = SP.Presentation.DiamondGizmo.ColorVacio;
                if (seats.Count > 0)
                {
                    bool hasAllied = false;
                    bool hasEnemy = false;
                    foreach (var s in seats.Values)
                    {
                        if (s.Team == SP.Combat.TeamId.Player) hasAllied = true;
                        if (s.Team == SP.Combat.TeamId.Enemy) hasEnemy = true;
                    }
                    if (hasAllied && hasEnemy) diamondTarget = SP.Presentation.DiamondGizmo.ColorMixto;
                    else if (hasAllied) diamondTarget = SP.Presentation.DiamondGizmo.ColorAliado;
                    else if (hasEnemy) diamondTarget = SP.Presentation.DiamondGizmo.ColorEnemigo;
                }
                diamondMaterial.color = diamondTarget;
                if (diamondMaterial.HasProperty("_BaseColor")) diamondMaterial.SetColor("_BaseColor", diamondTarget);
            }
            var minimapIcon = GetComponentInChildren<SP.Presentation.MinimapIcon>();
            if (minimapIcon != null) minimapIcon.RepintarPorEquipo(true);
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

        // A donde debe CAMINAR un soldado para subir, ahora que el chasis
        // del vehiculo es solido para el movimiento (ver NavService.
        // BlocksMovement) -- antes AiBrain apuntaba directo a
        // transform.position (el pivote, adentro del chasis) porque total
        // el vehiculo no frenaba a nadie; con el chasis solido, apuntar al
        // pivote dejaria al soldado empujado contra el casco para siempre,
        // sin llegar nunca al arriveThreshold. El punto mas cercano de la
        // caja de colision, empujado un poco hacia afuera, es a donde de
        // verdad se puede LLEGAR caminando.
        [SerializeField] float margenAbordaje = 0.5f;

        Collider colliderPrincipal;
        Collider ColliderPrincipal()
        {
            if (colliderPrincipal == null)
                foreach (var c in GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) { colliderPrincipal = c; break; }
            return colliderPrincipal;
        }

        public Vector3 ClosestBoardingPoint(Vector3 desde)
        {
            var col = ColliderPrincipal();
            if (col == null) return transform.position;

            var puntoEnCaja = col.ClosestPoint(desde);
            // ClosestPoint devuelve un punto SOBRE la superficie (o adentro
            // si 'desde' ya estaba adentro) -- se empuja hacia afuera en la
            // direccion desde el centro del collider, para que el destino
            // de caminata quede fuera del casco y no justo pegado a él.
            var centro = col.bounds.center;
            var dir = puntoEnCaja - centro;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
            dir.Normalize();
            var destino = puntoEnCaja + dir * margenAbordaje;
            destino.y = desde.y;
            return destino;
        }

        public bool IsSeatFree(VehicleSeatRole role) => !seats.ContainsKey(role);

        public VehicleSeatRole? FirstFreeSeat()
        {
            foreach (var role in AllRoles) if (!seats.ContainsKey(role)) return role;
            return null;
        }

        // Tripulacion de arranque (tanques enemigos): los soldados y su rol se
        // guardan en la escena y se suben al empezar Play, sin animacion. Los
        // asientos NO se serializan (viven en un diccionario), asi que subirlos
        // en el editor se perderia al entrar en Play.
        [SerializeField] Soldier[] tripulacionInicial;
        [SerializeField] VehicleSeatRole[] rolesIniciales;
        [SerializeField] bool esTanqueEnemigo;
        public bool EsTanqueEnemigo => esTanqueEnemigo;

        public void ConfigurarTripulacion(Soldier[] tripulantes, VehicleSeatRole[] roles, bool enemigo)
        {
            tripulacionInicial = tripulantes;
            rolesIniciales = roles;
            esTanqueEnemigo = enemigo;
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (esTanqueEnemigo) AsignarBando(TeamId.Enemy, new Color(0.55f, 0.13f, 0.11f));
            SP.Presentation.BarraDeVidaVehiculo.Asegurar(this);   // ronda 13 (punto 9): barra de vida flotante
            if (tripulacionInicial == null) return;
            for (int i = 0; i < tripulacionInicial.Length; i++)
            {
                var s = tripulacionInicial[i];
                if (s == null) continue;
                var rol = rolesIniciales != null && i < rolesIniciales.Length ? rolesIniciales[i] : VehicleSeatRole.Driver;
                Mount(s, rol, instantaneo: true);
            }
        }

        // Ronda 13 (punto 12): FUENTE UNICA de "este soldado puede subir". Un tanque enemigo (con o sin tripulacion viva) NO
        // se aborda ni se puede mandar a abordar: solo se destruye. Lo usan Mount, las ordenes de montaje (IA, [G]/[U], RTS,
        // radial), el indicador de montaje, los carteles y la posesion desde RTS.
        public const string MotivoEnemigo = "TANQUE ENEMIGO: SOLO SE PUEDE DESTRUIR";
        public bool PuedeAbordar(Soldier soldier, out string motivo)
        {
            motivo = null;
            if (soldier == null) { motivo = "NADIE PARA SUBIR"; return false; }
            if (IsDestroyed) { motivo = "VEHICULO DESTRUIDO"; return false; }
            if (Bando == TeamId.Enemy && soldier.Team != TeamId.Enemy) { motivo = MotivoEnemigo; return false; }
            return true;
        }
        public bool PuedeAbordar(Soldier soldier) => PuedeAbordar(soldier, out _);

        public bool Mount(Soldier soldier, VehicleSeatRole? preferredRole = null, bool instantaneo = false)
        {
            // BUG REAL: esto no chequeaba vida. Un muerto se montaba
            // igual (devolvia true, sumaba al conteo de ocupantes) porque
            // nada en el camino de Mount() miraba Health.IsAlive -- cada
            // llamador (el auto-mount de EnterVehicle, IssueMountOrder,
            // etc.) tenia que acordarse de filtrar por su cuenta, y no
            // todos lo hacian. La guarda va aca, en la fuente unica, para
            // que ningun camino futuro pueda repetir el olvido.
            if (soldier == null || IsDestroyed || !soldier.Health.IsAlive) return false;
            if (!PuedeAbordar(soldier)) return false;   // ronda 13: un tanque enemigo no se aborda
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
            bool esJugador = brain != null && brain.IsPossessedByPlayer;
            if (brain != null) { brain.enabled = false; brain.MontadoEnVehiculo = true; }

            // Pedido explicito: un log legible de quien ocupa que asiento
            // de que vehiculo -- distinto segun sea el jugador (cambia de
            // camara, es la accion mas visible) o un aliado que sube por
            // su cuenta (orden de montaje, auto-mount). GameObject.name
            // (no solo DisplayName) porque el pedido explicito fue
            // "gameobject" -- para poder ubicar la instancia exacta en la
            // jerarquia si hace falta, no solo el nombre de fantasía.
            SP.Core.GameLog.Line(esJugador
                ? $"Usuario ocupó el asiento {RoleLabelEs(role)} del vehículo {gameObject.name}"
                : $"El aliado {soldier.DisplayName} se subió al vehículo {gameObject.name} como {RoleLabelEs(role)}");

            // Pedido explicito: una animacion real al subir, no el
            // desaparecer instantaneo de antes. En Play mode el soldado
            // se acerca al vehiculo y se achica hasta desaparecer; recien
            // ahi se desactiva. En Edit mode (la suite headless corre
            // Mount() sin Play) StartCoroutine no funciona -- se mantiene
            // el camino sincronico de siempre para no romper esos tests.
            if (Application.isPlaying && !instantaneo)
            {
                if (!mountTrueScale.ContainsKey(soldier)) mountTrueScale[soldier] = soldier.transform.localScale;

                if (mountAnimations.TryGetValue(soldier, out var existingCo) && existingCo != null)
                {
                    StopCoroutine(existingCo);
                    soldier.transform.localScale = mountTrueScale[soldier];
                }

                mountAnimations[soldier] = StartCoroutine(PlayMountAnimation(soldier, role));
            }
            else
            {
                var agent = soldier.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.enabled = false;
                }
                soldier.gameObject.SetActive(false);
            }

            RefreshOccupancyColor();
            return true;
        }

        readonly Dictionary<Soldier, Transform> padreAnterior = new Dictionary<Soldier, Transform>();

        const float MountAnimationSeconds = 0.35f;

        Vector3 MountOffsetFor(VehicleSeatRole role) => DismountOffsetFor(role) * 0.4f;

        IEnumerator PlayMountAnimation(Soldier soldier, VehicleSeatRole role)
        {
            var startPos = soldier.transform.position;
            var startScale = mountTrueScale[soldier];

            // Metralleta ("de arriba"): pedido explicito -- a diferencia
            // del conductor y el cañon (van ADENTRO del casco, por eso se
            // esconden), el artillero de la metralleta esta de pie,
            // visible, asomando por la escotilla. No se achica ni
            // desaparece: camina/sube hasta el punto de pie y se queda
            // ahi visible, colgado del CHASIS (transform, para seguir al
            // tanque al andar) y no de MetralletaPivot -- ese gira con la
            // punteria, y colgarlo de ahi lo haria inclinarse con el cañon
            // cada vez que alguien apunta.
            if (role == VehicleSeatRole.Passenger1)
            {
                // BUG REAL: SwitchSeat (PlayerInputDriver) desactiva el
                // GameObject del soldado ANTES de llamar a Mount, asumiendo
                // que esta rama lo va a reactivar como cualquier otro
                // asiento -- pero esta rama nunca toca SetActive porque
                // asume que el soldado YA esta visible (el caso normal:
                // caminar hasta el vehiculo estando activo). Sin esto, subir
                // a la metralleta desde OTRO asiento del mismo vehiculo
                // dejaba al artillero invisible para siempre.
                soldier.gameObject.SetActive(true);
                var standPoint = transform.Find("MetralletaStandPoint");
                var startRot = soldier.transform.rotation;
                float tMg = 0f;
                while (tMg < MountAnimationSeconds)
                {
                    if (soldier == null) yield break;
                    if (RoleOf(soldier) == null)
                    {
                        mountAnimations.Remove(soldier);
                        mountTrueScale.Remove(soldier);
                        yield break;
                    }
                    tMg += Time.deltaTime;
                    float kMg = Mathf.Clamp01(tMg / MountAnimationSeconds);
                    float easedMg = 1f - (1f - kMg) * (1f - kMg);
                    Vector3 targetPosMg = standPoint != null ? standPoint.position : transform.position + MountOffsetFor(role);
                    Quaternion targetRotMg = standPoint != null ? standPoint.rotation : transform.rotation;
                    soldier.transform.position = Vector3.Lerp(startPos, targetPosMg, easedMg);
                    soldier.transform.rotation = Quaternion.Slerp(startRot, targetRotMg, easedMg);
                    yield return null;
                }
                if (soldier == null) yield break;
                if (RoleOf(soldier) == null)
                {
                    mountAnimations.Remove(soldier);
                    mountTrueScale.Remove(soldier);
                    yield break;
                }
                // BUG REAL: colgarlo directo del chasis (transform) con una
                // simple contra-escala arreglaba el TAMAÑO en el instante de
                // subir, pero no el andar/girar despues -- el chasis tiene
                // escala NO uniforme (2.2/1.4/3.6), y con una rotacion local
                // no identidad (la de standPoint) entre medio, escala no
                // uniforme + rotacion no conmutan: el resultado es una
                // MATRIZ CON SHEAR, no un cuerpo rigido. Se nota poco en un
                // caño de cañon (simetrico) pero mucho en un cuerpo humano
                // (se ve estirado/retorcido en cuanto el tanque gira).
                // La solucion real es un "carrito" intermedio con escala
                // uniforme (1,1,1) colgado del chasis en la posicion del
                // punto de pie: shear y rotacion SI conmutan con escala
                // uniforme, asi que lo que cuelgue de el rota rigido.
                if (standPoint != null)
                {
                    var carrito = ObtenerOCrearCarritoDeParado(standPoint);
                    // Se recuerda de quien colgaba para devolverlo ahi al bajar
                    // (antes quedaba suelto en la raiz de la escena).
                    if (soldier.transform.parent != carrito) padreAnterior[soldier] = soldier.transform.parent;
                    soldier.transform.SetParent(carrito, false);
                    soldier.transform.localPosition = Vector3.zero;
                    soldier.transform.localRotation = Quaternion.identity;
                    soldier.transform.localScale = startScale;
                }
                mountAnimations.Remove(soldier);
                mountTrueScale.Remove(soldier);
                yield break;
            }

            float t = 0f;
            while (t < MountAnimationSeconds)
            {
                // Si en el medio lo bajaron (Dismount, o el vehiculo se
                // destruyo y expulso a todos) cortar aca y devolverle la
                // escala real: RoleOf devuelve null apenas Dismount() lo
                // saca de seats, ANTES de reposicionarlo -- sin este
                // chequeo, la corutina seguiria de largo y le pisaria la
                // posicion que Dismount le puso, para terminar
                // desactivandolo (invisible) a alguien que se acababa de
                // bajar y deberia seguir de pie afuera.
                if (soldier == null) yield break;
                if (RoleOf(soldier) == null) 
                { 
                    soldier.transform.localScale = startScale; 
                    mountAnimations.Remove(soldier);
                    mountTrueScale.Remove(soldier);
                    yield break; 
                }
                Vector3 targetPos = transform.position + MountOffsetFor(role);
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / MountAnimationSeconds);
                // Ease-out: rapido al principio, se frena justo antes de
                // desaparecer -- un lerp lineal se sentia mecanico para
                // algo tan corto.
                float eased = 1f - (1f - k) * (1f - k);
                soldier.transform.position = Vector3.Lerp(startPos, targetPos, eased);
                soldier.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                yield return null;
            }

            if (soldier == null) yield break;
            // Restaura la escala ANTES de desactivar: Dismount() no la
            // toca, asi que si quedara en cero el soldado reapareceria
            // invisible la proxima vez que se baje.
            soldier.transform.localScale = startScale;
            var agentMount = soldier.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agentMount != null && agentMount.isActiveAndEnabled && agentMount.isOnNavMesh)
            {
                agentMount.ResetPath();
                agentMount.enabled = false;
            }
            soldier.gameObject.SetActive(false);
            
            mountAnimations.Remove(soldier);
            mountTrueScale.Remove(soldier);
        }

        const string NombreCarritoDeParado = "MetralletaStandCarrier";
        Transform carritoDeParado;

        // Carrito con escala uniforme (1,1,1) colgado del chasis en la
        // posicion/rotacion del punto de pie: ver el comentario en
        // PlayMountAnimation sobre por que hace falta (shear al girar).
        // Idempotente: se crea una sola vez y se reutiliza.
        Transform ObtenerOCrearCarritoDeParado(Transform standPoint)
        {
            if (carritoDeParado != null) return carritoDeParado;
            var existente = transform.Find(NombreCarritoDeParado);
            if (existente != null) { carritoDeParado = existente; return carritoDeParado; }

            var go = new GameObject(NombreCarritoDeParado);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = standPoint.localPosition;
            go.transform.localRotation = standPoint.localRotation;
            var chassisScale = transform.localScale;
            go.transform.localScale = new Vector3(
                1f / (Mathf.Abs(chassisScale.x) > 0.0001f ? chassisScale.x : 1f),
                1f / (Mathf.Abs(chassisScale.y) > 0.0001f ? chassisScale.y : 1f),
                1f / (Mathf.Abs(chassisScale.z) > 0.0001f ? chassisScale.z : 1f));
            carritoDeParado = go.transform;
            return carritoDeParado;
        }

        // Baja a un soldado y lo reaparece junto al vehículo.
        public bool Dismount(Soldier soldier)
        {
            VehicleSeatRole? foundRole = null;
            foreach (var kv in seats) if (kv.Value == soldier) { foundRole = kv.Key; break; }
            if (foundRole == null) return false;

            seats.Remove(foundRole.Value);

            soldier.gameObject.SetActive(true);
            // El artillero de la metralleta quedo colgado del chasis
            // (transform) para seguir al tanque de pie y visible -- hay
            // que soltarlo antes de reposicionarlo, o se queda pegado al
            // tanque para siempre (siguiendolo incluso ya "afuera").
            // worldPositionStays=true porque la posicion de mundo actual
            // no importa: dos lineas mas abajo se pisa con el offset de
            // desmontaje de todas formas.
            if (soldier.transform.parent == transform || soldier.transform.parent == carritoDeParado)
            {
                padreAnterior.TryGetValue(soldier, out var padre);
                soldier.transform.SetParent(padre, true);
                padreAnterior.Remove(soldier);
            }
            soldier.transform.localScale = Vector3.one;
            // Antes todos bajaban exactamente al mismo punto (derecha del
            // chasis), sin importar el asiento -- con varios ocupantes
            // quedaban superpuestos o dentro del chasis. Cada asiento
            // baja por su propio costado.
            var destinoBajada = transform.position + DismountOffsetFor(foundRole.Value);
            // Ronda 13 (punto 11): la tripulacion que sale de un tanque tiene que caer sobre la malla de navegacion (si no, queda
            // dentro de un muro/roca y su cerebro no puede moverse ni apuntar) y con el cerebro reiniciado para que combata.
            //
            // BUG REAL ("al salir del tanque quedas enterrado"): antes se
            // adoptaba tambien la ALTURA que devuelve SamplePosition -- pero
            // esa altura es la del nodo de malla horneado mas cercano en un
            // radio de 4 m, que con un chasis grande de por medio puede ser
            // un nodo de OTRO lado (una rampa, un pozo, un techo) a una
            // altura totalmente distinta a la del piso real bajo el tanque.
            // Con el soldado ya teletransportado ahi abajo, ApoyoEnElPiso
            // (que tira el rayo HACIA ABAJO desde su propia cabeza) nunca
            // podia encontrar el piso real si este quedaba POR ARRIBA del
            // punto de partida -- de ahi "enterrado" sin forma de corregirse
            // solo. La altura del VEHICULO es la referencia confiable (esta
            // parado sobre el terreno real ahora mismo): solo se toma X/Z
            // del muestreo de malla, para no bajar dentro de una roca/pared.
            if (UnityEngine.AI.NavMesh.SamplePosition(destinoBajada, out var golpeNav, 4f, UnityEngine.AI.NavMesh.AllAreas))
                destinoBajada = new Vector3(golpeNav.position.x, transform.position.y, golpeNav.position.z);
            soldier.transform.position = destinoBajada;
            soldier.transform.rotation = transform.rotation;
            // El muestreo del NavMesh da una posicion aproximada (la malla
            // horneada no es identica a la geometria real): sin este apoyo,
            // el soldado quedaba levemente hundido o flotando al bajar, y
            // como ApoyoEnElPiso.ApoyarATodos() solo corre una vez al
            // iniciar la partida, nadie lo corregia despues -- de ahi que
            // moverse despues de bajar del tanque se sintiera roto.
            SP.Core.ApoyoEnElPiso.Apoyar(soldier.transform);

            if (soldier.Brain != null) soldier.Brain.ReactivarNavegacion();

            var brain = soldier.Brain;
            if (brain != null) { brain.enabled = true; brain.MontadoEnVehiculo = false; }
            RefreshOccupancyColor();
            return true;
        }

        void LateUpdate()
        {
            if (carritoDeParado != null)
            {
                var standPoint = transform.Find("TurretMount/TurretPivot/MetralletaStandPoint");
                if (standPoint == null) standPoint = transform.Find("MetralletaStandPoint");
                if (standPoint != null)
                {
                    carritoDeParado.position = standPoint.position;
                    carritoDeParado.rotation = standPoint.rotation;
                }
            }
            if (diamondMarker != null)
            {
                var cam = SP.Core.CamaraPrincipal.Actual;
                if (cam != null) diamondMarker.transform.rotation = cam.transform.rotation;
                diamondMarker.SetActive(!IsDestroyed);
            }
        }

        // Intercambia los asientos de DOS ocupantes (el jugador que pide un
        // asiento ocupado por un aliado, y el aliado que pasa al asiento que
        // el jugador libero). Los baja a los dos y los vuelve a montar cada
        // uno en el asiento del otro, por el mismo camino de siempre, asi que
        // el que queda de pie en la metralleta se cuelga del chasis, el que
        // va adentro se esconde, y los Brain quedan apagados como corresponde.
        // Devuelve false sin tocar nada si alguno no esta a bordo o esta a
        // mitad de la animacion de subir.
        public bool SwapSeats(Soldier a, Soldier b)
        {
            if (a == null || b == null || a == b) return false;
            var roleA = RoleOf(a);
            var roleB = RoleOf(b);
            if (roleA == null || roleB == null) return false;
            if (IsMountAnimating(a) || IsMountAnimating(b)) return false;

            Dismount(a);
            Dismount(b);
            a.gameObject.SetActive(false);
            b.gameObject.SetActive(false);
            Mount(a, roleB.Value);
            Mount(b, roleA.Value);

            SP.Core.GameLog.Line($"{a.DisplayName} y {b.DisplayName} intercambian asiento en {gameObject.name}: {RoleLabelEs(roleB.Value)} <-> {RoleLabelEs(roleA.Value)}");
            return true;
        }

        // Pasa a un ocupante a OTRO asiento, que tiene que estar libre. Es el
        // mismo camino que SwapSeats (bajar y volver a montar), asi que el que
        // queda de pie en la metralleta se cuelga del chasis y los demas se
        // esconden, sin dejar un Brain encendido ni el soldado a mitad de
        // camino. Devuelve false sin tocar nada si no esta a bordo o el
        // asiento esta ocupado.
        public bool MoveToSeat(Soldier soldier, VehicleSeatRole role)
        {
            if (soldier == null || RoleOf(soldier) == null || !IsSeatFree(role)) return false;
            if (IsMountAnimating(soldier)) return false;
            Dismount(soldier);
            soldier.gameObject.SetActive(false);
            return Mount(soldier, role);
        }

        // Un costado y una distancia distinta por asiento: conductor a la
        // izquierda, artillero a la derecha, pasajeros atras a cada lado
        // -- para que cuatro ocupantes bajando a la vez no terminen los
        // cuatro en el mismo punto ni adentro del chasis.
        //
        // BUG REAL: los 2.5/2 m de antes eran fijos, tuneados para un
        // vehiculo mas chico que el tanque -- con un chasis real mas ancho
        // que eso, el punto de bajada caia DENTRO del propio collider solido
        // del vehiculo, y todo lo que viene despues (muestreo de NavMesh,
        // apoyo al piso) arranca de un punto ya invalido. Igual que
        // ClosestBoardingPoint (para SUBIR), la mitad real del casco +
        // el mismo margen de abordaje garantiza que el punto de BAJADA
        // tambien quede afuera del casco, sea cual sea su tamaño.
        Vector3 DismountOffsetFor(VehicleSeatRole role)
        {
            float mitadAncho = 2.5f, mitadLargo = 2f;
            var col = ColliderPrincipal();
            if (col != null)
            {
                var ext = col.bounds.extents;
                mitadAncho = ext.x + margenAbordaje;
                mitadLargo = ext.z + margenAbordaje;
            }
            switch (role)
            {
                case VehicleSeatRole.Driver: return -transform.right * mitadAncho;
                case VehicleSeatRole.Gunner: return transform.right * mitadAncho;
                case VehicleSeatRole.Passenger1: return -transform.right * mitadAncho - transform.forward * mitadLargo;
                case VehicleSeatRole.Passenger2: return transform.right * mitadAncho - transform.forward * mitadLargo;
                default: return transform.right * mitadAncho;
            }
        }

        public VehicleSeatRole? RoleOf(Soldier soldier)
        {
            foreach (var kv in seats) if (kv.Value == soldier) return kv.Key;
            return null;
        }

        // Gunner pasa de "torreta" a "cañón" y Passenger1 de "tripulante" a
        // "metralleta": pedido explicito ("ahora es cañon y metralleta y
        // conductor") -- el tanque tiene DOS armas montadas distintas
        // (TurretPivot=cañón, MetralletaPivot=metralleta) y el log tiene
        // que nombrar la que corresponde, no una etiqueta generica que las
        // mezclaba.
        static string RoleLabelEs(VehicleSeatRole role) => role switch
        {
            VehicleSeatRole.Driver => "conductor",
            VehicleSeatRole.Gunner => "cañón",
            VehicleSeatRole.Passenger1 => "metralleta",
            _ => "tripulante",
        };

        // Lo usa el HUD (VehicleStatusView) para mostrar la lista completa
        // de puestos ocupados, no solo el del jugador: seats es privado a
        // proposito (nadie de afuera deberia mutarlo), esto es la unica
        // puerta de lectura por asiento.
        public IReadOnlyList<VehicleSeatRole> AllSeatRoles => AllRoles;
        public Soldier SoldierInSeat(VehicleSeatRole role) => seats.TryGetValue(role, out var s) ? s : null;
    }
}
