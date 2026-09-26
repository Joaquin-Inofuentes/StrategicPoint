using UnityEngine;
using SP.Vehicles;
using SP.Combat;
using SP.Actors;

namespace SP.Mision
{
    public class MotoEnemigaDirector : MonoBehaviour
    {
        public static MotoEnemigaDirector Instancia { get; private set; }
        
        float proximaOleada = 0f;
        bool activo = false;
        
        void Awake()
        {
            Instancia = this;
        }
        
        public void IniciarOleadas()
        {
            activo = true;
            proximaOleada = Time.time + 5f;
        }
        
        void Update()
        {
            if (!activo || Camioneta.Instancia == null || !Camioneta.Instancia.EnRuta) return;
            
            if (Time.time >= proximaOleada)
            {
                LanzarOleada();
                proximaOleada = Time.time + Random.Range(10f, 15f);
            }
        }
        
        void LanzarOleada()
        {
            int n = Random.Range(2, 4);
            for(int i=0; i<n; i++)
            {
                var go = new GameObject("MotoEnemiga_" + i);
                go.transform.position = Camioneta.Instancia.transform.position + new Vector3(Random.Range(-10f, 10f), 0, Random.Range(25f, 40f));
                go.AddComponent<MotoEnemiga>();
                // Spawn enemies to ride it (2 seats)
                if (MisionDirector.Instancia != null)
                {
                    var moto = go.GetComponent<MotoEnemiga>();
                    var driver = MisionDirector.Instancia.CrearEnemigo("Moto_" + i + "_Driver", go.transform.position);
                    var gunner = MisionDirector.Instancia.CrearEnemigo("Moto_" + i + "_Gunner", go.transform.position);
                    if (driver != null) moto.Vehiculo.Mount(driver, VehicleSeatRole.Driver, true);
                    if (gunner != null) moto.Vehiculo.Mount(gunner, VehicleSeatRole.Passenger1, true);
                }
            }
        }
        
        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }
    }
}
