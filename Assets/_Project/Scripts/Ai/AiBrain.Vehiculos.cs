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
    // agrega una amenaza SEPARADA del sensado de soldados de siempre: un enemigo sin blanco de carne y
    // hueso (target == null) tambien mira si hay un vehiculo hostil OCUPADO cerca y a la vista, y le
    // dispara igual que le dispararia a un soldado (mismo gatillo, DispararConRafagas). No entra al
    // estado Chase/Attack (esos siguen siendo solo de Soldier) ni lo persigue caminando: es fuego de
    // oportunidad para el que ya esta cerca, suficiente para que un tanque parado en medio de una linea
    // enemiga (o pasando frente a una patrulla) reciba disparos reales en vez de ser invisible para la
    // infanteria. El dano en si ya funcionaba (Projectile.cs golpea Vehicle.TakeDamage en un impacto
    // directo): lo unico que faltaba era la DECISION de apuntarle.
    public partial class AiBrain
    {
        Vehicle vehiculoAmenaza;
        float relojAmenazaVehiculo;

        Vehicle BuscarVehiculoAmenaza()
        {
            Vehicle mejor = null;
            float mejorDist = float.MaxValue;
            float alcance = EffectiveVisionRange * AlcanceExtendido;
            foreach (var v in Vehicle.Todos)
            {
                if (v == null || v.IsDestroyed || v.OccupantCount == 0 || v.Bando == self.Team) continue;
                float d = Vector3.Distance(self.transform.position, v.transform.position);
                if (d > alcance || d >= mejorDist) continue;
                if (!NavService.HayLineaDeTiro(self.transform.position, v.transform.position, self.transform, v.transform)) continue;
                mejor = v;
                mejorDist = d;
            }
            return mejor;
        }

        // Devuelve true si este tick lo uso en reaccionar/apuntar/disparar contra un vehiculo -- el
        // llamador corta el resto del tick de combate, mismo patron que TickGranadas.
        bool TickAtaqueAVehiculo(float dt)
        {
            if (target != null || Pasivo || self.Team != TeamId.Enemy) { vehiculoAmenaza = null; return false; }

            relojAmenazaVehiculo -= dt;
            if (relojAmenazaVehiculo <= 0f || (vehiculoAmenaza != null && (vehiculoAmenaza.IsDestroyed || vehiculoAmenaza.OccupantCount == 0)))
            {
                relojAmenazaVehiculo = 0.5f;   // mismo costo que TickRetarget: barato, no hace falta cada tick
                vehiculoAmenaza = BuscarVehiculoAmenaza();
            }
            if (vehiculoAmenaza == null) return false;

            float dist = Vector3.Distance(self.transform.position, vehiculoAmenaza.transform.position);
            if (dist > EffectiveAttackRange) return false;
            if (!NavService.HayLineaDeTiro(self.transform.position, vehiculoAmenaza.transform.position, self.transform, vehiculoAmenaza.transform)) return false;

            self.Motor.SetCrouching(true);
            self.Motor.LookTowards(vehiculoAmenaza.transform.position, dt);
            self.Weapon.Tick(dt);

            Vector3 flatDir = vehiculoAmenaza.transform.position - self.transform.position;
            flatDir.y = 0f;
            bool apunta = flatDir.sqrMagnitude < 0.0001f || Vector3.Angle(self.transform.forward, flatDir) <= aimToleranceDeg;
            if (StanceAllowsFire && apunta)
            {
                var muzzle = self.Weapon.Muzzle;
                Vector3 origen = muzzle != null ? muzzle.position : self.transform.position;
                Vector3 direccion = vehiculoAmenaza.transform.position - origen;
                if (direccion.sqrMagnitude > 0.0001f) DispararConRafagas(direccion.normalized, dt);
            }
            return true;
        }
    }
}
