using UnityEngine;
using SP.Core;
using SP.Combat;
using SP.Vehicles;

namespace SP.Ai
{
    // Pedido explicito: "que los enemigos me ataquen si estoy en el tanque". Antes esto era imposible:
    // el jugador (o cualquier aliado) montado en un vehiculo desactiva su propio GameObject (ver
    // Vehicle.Mount), y AiBrain.Tick() ya descarta a proposito cualquier target sin
    // activeInHierarchy -- asi que ningun enemigo podia siquiera considerarlo un blanco. Este archivo
    // agrega una amenaza SEPARADA del sensado de soldados de siempre: un soldado sin blanco de carne y
    // hueso (target == null) tambien mira si hay un vehiculo hostil cerca y a la vista, y le
    // dispara igual que le dispararia a un soldado (mismo gatillo, DispararConRafagas). No entra al
    // estado Chase/Attack (esos siguen siendo solo de Soldier) ni lo persigue caminando: es fuego de
    // oportunidad para el que ya esta cerca, suficiente para que un tanque parado en medio de una linea
    // enemiga (o pasando frente a una patrulla) reciba disparos reales en vez de ser invisible para la
    // infanteria. El dano en si ya funcionaba (Projectile.cs golpea Vehicle.TakeDamage en un impacto
    // directo): lo unico que faltaba era la DECISION de apuntarle.
    //
    // Bug #069/#071: ahora tambien lo hace el bando del jugador (los aliados le tiran a las camionetas enemigas), una camioneta
    // sin tripulacion cuenta como blanco (Vehicle.Hostil), el soldado de ASALTO saca el lanzacohetes cuando el vehiculo esta
    // entre 8 y 45 m, y el jugador puede ordenarlo (OrderService.IssueAttackVehicleOrder: Q o clic derecho sobre el vehiculo).
    public partial class AiBrain
    {
        public const float CoheteMinimo = 8f, CoheteMaximo = 45f, SegundosDeCohete = 6f, EsperaEntreCohetes = 4f, SegundosDeOrdenDeVehiculo = 15f;

        Vehicle vehiculoAmenaza;
        float relojAmenazaVehiculo;
        Vehicle vehiculoOrdenado;
        float ordenDeVehiculoHasta;
        int ranuraAntesDelCohete = -1;
        float coheteDesde = -1f, proximoCohete;

        public Vehicle VehiculoAmenaza => vehiculoAmenaza;
        public Vehicle VehiculoOrdenado => vehiculoOrdenado;
        public bool ConCoheteParaVehiculo => ranuraAntesDelCohete >= 0;
        public int DisparosAVehiculos { get; private set; }

        // El jugador manda atacar un vehiculo: se suelta el blanco de infanteria, se camina hacia el vehiculo y en cuanto esta al
        // alcance y a la vista TickAtaqueAVehiculo toma el control del tick.
        public void IssueAttackVehicleOrder(Vehicle v)
        {
            if (v == null || self == null || !v.Hostil(self.Team)) return;
            if (!bootstrapped) Bootstrap();
            NuevaOrden();
            vehiculoOrdenado = v;
            ordenDeVehiculoHasta = Time.time + SegundosDeOrdenDeVehiculo;
            vehiculoAmenaza = v;
            relojAmenazaVehiculo = 0.5f;
            target = null;
            orderIsAttack = false;
            mountTarget = null;
            followTarget = null;
            orderQueue.Clear();
            hasOrder = true;
            orderDestination = v.transform.position;
            PlanPathTo(orderDestination);
            SetState(AiState.MovingToOrder);
            forceSense = true;
        }

        // El pivote de una camioneta esta a ras del piso: un rayo hasta ahi termina DENTRO del colisionador del suelo y la linea de tiro
        // daba siempre "tapado". Se mira al centro del vehiculo, a 1 m de altura.
        static Vector3 CentroDe(Vehicle v) => v.transform.position + Vector3.up * 1.0f;

        Vehicle BuscarVehiculoAmenaza()
        {
            if (vehiculoOrdenado != null)
            {
                if (!vehiculoOrdenado.Hostil(self.Team) || Time.time > ordenDeVehiculoHasta) vehiculoOrdenado = null;
                else return vehiculoOrdenado;
            }
            Vehicle mejor = null;
            float mejorDist = float.MaxValue;
            float alcance = EffectiveVisionRange * AlcanceExtendido;
            foreach (var v in Vehicle.Todos)
            {
                if (v == null || !v.Hostil(self.Team)) continue;
                float d = Vector3.Distance(self.transform.position, v.transform.position);
                if (d > alcance || d >= mejorDist) continue;
                if (!NavService.HayLineaDeTiro(self.transform.position, CentroDe(v), self.transform, v.transform)) continue;
                mejor = v;
                mejorDist = d;
            }
            return mejor;
        }

        // Sale del lanzacohetes y vuelve al arma con la que venia (ranura 0 por defecto).
        void RestaurarArmaTrasCohete()
        {
            if (ranuraAntesDelCohete < 0) return;
            if (self != null && self.Weapon != null && self.Weapon.CurrentWeaponKind == WeaponKind.Rocket) self.Weapon.EquipFromLoadout(ranuraAntesDelCohete);
            ranuraAntesDelCohete = -1;
            coheteDesde = -1f;
            proximoCohete = Time.time + EsperaEntreCohetes;
        }

        // El de ASALTO (o cualquiera que lleve cohete) lo saca con el vehiculo entre 8 y 45 m: un cohete pega 2,5 veces mas contra vehiculos.
        void ElegirArmaContraVehiculo(float dist)
        {
            var w = self.Weapon;
            if (w == null) return;
            int slot = w.Loadout.IndexOf(WeaponKind.Rocket);
            bool enRango = dist >= CoheteMinimo && dist <= CoheteMaximo;
            if (ranuraAntesDelCohete >= 0)
            {
                if (!enRango || Time.time - coheteDesde > SegundosDeCohete || w.CurrentWeaponKind != WeaponKind.Rocket) RestaurarArmaTrasCohete();
                return;
            }
            if (slot < 0 || !enRango || Time.time < proximoCohete || w.CurrentWeaponKind == WeaponKind.Rocket) return;
            ranuraAntesDelCohete = Mathf.Max(0, w.CurrentLoadoutIndex == slot ? 0 : w.CurrentLoadoutIndex);
            coheteDesde = Time.time;
            w.EquipFromLoadout(slot);
            reaccionPendiente = false;
            rafagaRestante = 0;
            pausaRestante = 0f;
        }

        // Punto al que apuntar: el centro del vehiculo, adelantado si se mueve y con la caida del cohete compensada.
        Vector3 PuntoDeTiroAVehiculo(Vehicle v, Vector3 origen)
        {
            Vector3 p = v.transform.position + Vector3.up * 1.0f;
            if (self.Weapon != null && self.Weapon.CurrentWeaponKind == WeaponKind.Rocket)
            {
                var espec = WeaponCatalog.Get(WeaponKind.Rocket);
                float vel = Projectile.VelocidadBase * Mathf.Max(0.05f, espec.ProjectileSpeed);
                float t = Vector3.Distance(origen, p) / vel;
                p += Vector3.up * (0.5f * espec.ProjectileGravity * t * t);
            }
            return p;
        }

        // Devuelve true si este tick lo uso en reaccionar/apuntar/disparar contra un vehiculo -- el
        // llamador corta el resto del tick de combate, mismo patron que TickGranadas.
        bool TickAtaqueAVehiculo(float dt)
        {
            bool deJugador = self.Team == TeamId.Player;
            bool ordenado = vehiculoOrdenado != null;
            if ((target != null && !ordenado) || Pasivo || self.Role == RoleType.Civilian
                || (deJugador && SP.Player.AccionesEnCurso.De(self, out _)))
            {
                vehiculoAmenaza = null;
                RestaurarArmaTrasCohete();
                return false;
            }
            if (ordenado) target = null;   // la orden del jugador manda sobre el blanco de infanteria

            relojAmenazaVehiculo -= dt;
            if (relojAmenazaVehiculo <= 0f || (vehiculoAmenaza != null && !vehiculoAmenaza.Hostil(self.Team)))
            {
                relojAmenazaVehiculo = 0.5f;   // mismo costo que TickRetarget: barato, no hace falta cada tick
                vehiculoAmenaza = BuscarVehiculoAmenaza();
            }
            if (vehiculoAmenaza == null) { RestaurarArmaTrasCohete(); return false; }

            float dist = Vector3.Distance(self.transform.position, vehiculoAmenaza.transform.position);
            ElegirArmaContraVehiculo(dist);
            // Con el cohete el alcance es el del lanzacohetes, no el del fusil.
            float alcanceDeTiro = self.Weapon != null && self.Weapon.CurrentWeaponKind == WeaponKind.Rocket ? CoheteMaximo : EffectiveAttackRange;
            if (dist > alcanceDeTiro) { RestaurarArmaTrasCohete(); return false; }
            if (!NavService.HayLineaDeTiro(self.transform.position, CentroDe(vehiculoAmenaza), self.transform, vehiculoAmenaza.transform)) return false;

            if (!deJugador) self.Motor.SetCrouching(true);
            self.Motor.LookTowards(vehiculoAmenaza.transform.position, dt);
            self.Weapon.Tick(dt);

            Vector3 flatDir = vehiculoAmenaza.transform.position - self.transform.position;
            flatDir.y = 0f;
            bool apunta = flatDir.sqrMagnitude < 0.0001f || Vector3.Angle(self.transform.forward, flatDir) <= aimToleranceDeg;
            if (StanceAllowsFire && apunta)
            {
                var muzzle = self.Weapon.Muzzle;
                Vector3 origen = muzzle != null ? muzzle.position : self.transform.position;
                Vector3 direccion = PuntoDeTiroAVehiculo(vehiculoAmenaza, origen) - origen;
                if (direccion.sqrMagnitude > 0.0001f && DispararConRafagas(direccion.normalized, dt)) DisparosAVehiculos++;
            }
            return true;
        }
    }
}
