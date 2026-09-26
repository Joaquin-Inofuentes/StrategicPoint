using UnityEngine;
using SP.Vehicles;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.Mision
{
    public class Camioneta : MonoBehaviour
    {
        public static Camioneta Instancia { get; private set; }
        public static void ReiniciarActivo() => Instancia = null;
        public Vehicle Vehiculo { get; private set; }
        public VehicleBrain Cerebro { get; private set; }
        
        public bool EnRuta { get; private set; }
        int waypointIdx = 0;
        Vector3[] ruta;

        void Awake()
        {
            Instancia = this;
            ConstruirBloqueoVisual();
            
            Vehiculo = gameObject.AddComponent<Vehicle>();
            gameObject.AddComponent<VehicleMotor>();
            Cerebro = gameObject.AddComponent<VehicleBrain>();
            
            Vehiculo.AsignarBando(TeamId.Player, new Color(0.2f, 0.4f, 0.2f));
        }

        void ConstruirBloqueoVisual()
        {
            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuerpo.transform.SetParent(transform, false);
            cuerpo.transform.localPosition = new Vector3(0, 1.5f, 0);
            cuerpo.transform.localScale = new Vector3(2.2f, 1.5f, 4.5f);
            Destroy(cuerpo.GetComponent<Collider>());
            
            for(int i=0; i<4; i++) {
                var rueda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rueda.transform.SetParent(transform, false);
                float x = (i%2==0 ? 1 : -1) * 1.2f;
                float z = (i<2 ? 1 : -1) * 1.8f;
                rueda.transform.localPosition = new Vector3(x, 0.6f, z);
                rueda.transform.localRotation = Quaternion.Euler(0, 0, 90);
                rueda.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
                Destroy(rueda.GetComponent<Collider>());
            }
            
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.5f, 0);
            col.size = new Vector3(2.2f, 1.5f, 4.5f);
        }
        
        public void SubirATodos()
        {
            var driver = PlayerInputDriver.Activo;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                bool esJugador = (driver != null && driver.Brain != null && driver.Brain.Current == s);
                Vehiculo.Mount(s, esJugador ? VehicleSeatRole.Gunner : (VehicleSeatRole?)null, instantaneo: true);
                if (esJugador && driver != null)
                {
                    driver.SwitchToVehicle(Vehiculo, VehicleSeatRole.Gunner);
                }
            }
        }
        
        public void IniciarEscape()
        {
            EnRuta = true;
            SubirATodos();
            ruta = new Vector3[] {
                transform.position + new Vector3(0, 0, -30),
                transform.position + new Vector3(0, 0, -80),
                transform.position + new Vector3(0, 0, -150)
            };
            waypointIdx = 0;
            Cerebro.IssueMoveOrder(ruta[waypointIdx]);
            if (MotoEnemigaDirector.Instancia != null) MotoEnemigaDirector.Instancia.IniciarOleadas();
        }
        
        void Update()
        {
            if (Vehiculo != null && Vehiculo.IsDestroyed && EnRuta)
            {
                EnRuta = false;
                MisionDirector.Instancia.Perder("CAMIONETA DESTRUIDA");
            }
            if (EnRuta && Cerebro != null)
            {
                if (!Cerebro.HasOrder)
                {
                    waypointIdx++;
                    if (waypointIdx < ruta.Length)
                    {
                        Cerebro.IssueMoveOrder(ruta[waypointIdx]);
                    }
                    else
                    {
                        EnRuta = false;
                        MisionDirector.Instancia.GanarEnCamioneta();
                    }
                }
            }
        }
        
        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }
    }
}
