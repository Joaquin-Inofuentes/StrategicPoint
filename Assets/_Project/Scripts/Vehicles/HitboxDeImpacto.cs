using UnityEngine;

namespace SP.Vehicles
{
    // Marca un collider que existe SOLO para que le puedan pegar -- no es un
    // obstaculo real. NavService.BlocksMovement lo excluye para que no
    // bloquee caminata ni linea de tiro, y Coberturas.Solidos() (que usa esa
    // misma definicion) no lo cuente como un obstaculo de cobertura aparte.
    // Ver TurretHitbox en HeadlessTestRunner.BuildAndSaveVehiclePrefab: el
    // chasis del vehiculo ya bloquea todo eso por su cuenta: esto solo
    // agranda la zona que puede recibir dano (Projectile.ChocoContraElMundo
    // resuelve el dueño via GetComponentInParent<Vehicle>(), sin pasar por
    // BlocksMovement).
    public class HitboxDeImpacto : MonoBehaviour 
    {
        void Awake()
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }
}
