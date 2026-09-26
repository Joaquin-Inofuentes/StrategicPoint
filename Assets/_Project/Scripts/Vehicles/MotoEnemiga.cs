using UnityEngine;
using SP.Vehicles;
using SP.Combat;
using SP.Actors;

namespace SP.Vehicles
{
    public class MotoEnemiga : MonoBehaviour
    {
        public Vehicle Vehiculo { get; private set; }
        public VehicleBrain Cerebro { get; private set; }
        
        // BUG REAL (ver mismo problema en SP.Mision.Camioneta): armar Vehicle desde
        // dentro del propio Awake() es un AddComponent anidado -- Vehicle.OnEnable crea
        // ademas el rombo del vehiculo como efecto secundario de ESE AddComponent, y
        // Unity no sostiene esa doble anidacion de forma confiable (gameObject.
        // AddComponent<Vehicle>() devolvia null en silencio). Se separa en
        // Inicializar(), llamado por quien crea la moto (MotoEnemigaDirector).
        void Awake()
        {
        }

        public void Inicializar()
        {
            ConstruirBloqueoVisual();
            Vehiculo = gameObject.AddComponent<Vehicle>();
            gameObject.AddComponent<VehicleMotor>();
            Cerebro = gameObject.AddComponent<VehicleBrain>();

            Vehiculo.AsignarBando(TeamId.Enemy, Color.red);
        }
        
        void ConstruirBloqueoVisual()
        {
            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuerpo.transform.SetParent(transform, false);
            cuerpo.transform.localPosition = new Vector3(0, 1f, 0);
            cuerpo.transform.localScale = new Vector3(0.8f, 1f, 2f);
            Destroy(cuerpo.GetComponent<Collider>());
            
            for(int i=0; i<2; i++) {
                var rueda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rueda.transform.SetParent(transform, false);
                float z = (i==0 ? 1 : -1) * 0.8f;
                rueda.transform.localPosition = new Vector3(0, 0.5f, z);
                rueda.transform.localRotation = Quaternion.Euler(0, 0, 90);
                rueda.transform.localScale = new Vector3(1f, 0.2f, 1f);
                Destroy(rueda.GetComponent<Collider>());
            }
            
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.size = new Vector3(0.8f, 1f, 2f);
            
            var h = gameObject.AddComponent<Health>();
            h.Initialize(-1, 60);
        }
        
        void Update()
        {
            if (Vehiculo != null && Vehiculo.IsDestroyed) {
                SP.Presentation.ImpactFx.SpawnExplosion(transform.position, 2f);
                Destroy(gameObject);
                return;
            }
            if (Cerebro != null && SP.Mision.Camioneta.Instancia != null && SP.Mision.Camioneta.Instancia.EnRuta)
            {
                var target = SP.Mision.Camioneta.Instancia.transform.position;
                var dir = (transform.position - target).normalized;
                var offset = dir * Random.Range(12f, 25f);
                Cerebro.IssueMoveOrder(target + offset);
            }
        }
    }
}
