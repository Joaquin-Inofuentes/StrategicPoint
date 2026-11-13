using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;

namespace SP.Presentation
{
    // Bug #421 ("hay cuerpos flotando, no deberia pasar"): un soldado muerto que queda en el aire (lo mataron colgado de la cuerda de un helicoptero, un
    // ragdoll de explosion que se desarma antes de aterrizar, el helicoptero que cae...) se quedaba congelado donde murio: el motor solo busca piso cuando el
    // soldado CAMINA (SoldierMotor.RevisarBorde), y un cadaver no camina. Este vigilante revisa cada 0,5 s a los muertos activos y, si alguno esta a mas de
    // HuecoMinimo sobre su piso (y no es un ragdoll en vuelo), lo hace caer con gravedad hasta apoyarse.
    public sealed class CaidaDeCuerpos : MonoBehaviour
    {
        public const float HuecoMinimo = 0.6f, Gravedad = 22f, SegundosEntreRevisiones = 0.5f;
        public static int Caidos { get; private set; }
        public static int CayendoAhora => instancia != null ? instancia.cayendo.Count : 0;

        sealed class Cuerpo { public Soldier soldado; public float velocidad; }
        static CaidaDeCuerpos instancia;
        readonly List<Cuerpo> cayendo = new List<Cuerpo>();
        readonly HashSet<Soldier> enCaida = new HashSet<Soldier>();
        float proxima;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; Caidos = 0; AcostadosEnRampa = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("CaidaDeCuerpos") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<CaidaDeCuerpos>();
        }

        void Update()
        {
            if (Time.time >= proxima) { proxima = Time.time + SegundosEntreRevisiones; Buscar(); }
            if (cayendo.Count > 0) Caer(Time.deltaTime);
        }

        void Buscar()
        {
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s.Health == null || s.Health.IsAlive || !s.gameObject.activeInHierarchy || enCaida.Contains(s)) continue;
                var r = s.GetComponent<RagdollDeExplosion>();
                if (r != null && r.Activo) continue;   // el ragdoll en vuelo se mueve solo
                var p = s.transform.position;
                // Sobre una escalera/rampa: el piso es la superficie de la rampa, no el suelo de abajo. El cuerpo se apoya y se acuesta siguiendo la pendiente.
                if (RampaCaminable.SosteniendoA(p, SoldierMotor.PivoteSobrePiso, out var rampa, out float ySup)) { if (acostados.Add(s)) Acostar(s, rampa, ySup); continue; }
                acostados.Remove(s);
                if (!PisoReal.Altura(p, out float piso, out _, 1f, 40f)) continue;
                if (p.y - piso - SoldierMotor.PivoteSobrePiso <= HuecoMinimo) continue;
                cayendo.Add(new Cuerpo { soldado = s }); enCaida.Add(s);
            }
        }

        void Caer(float dt)
        {
            for (int i = cayendo.Count - 1; i >= 0; i--)
            {
                var c = cayendo[i]; var s = c.soldado;
                if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || s.Health.IsAlive) { Quitar(i); continue; }
                var r = s.GetComponent<RagdollDeExplosion>();
                if (r != null && r.Activo) { Quitar(i); continue; }
                c.velocidad += Gravedad * dt;
                var p = s.transform.position; p.y -= c.velocidad * dt;
                if (PisoReal.Altura(p + Vector3.up * 1f, out float piso, out _, 1.5f, 40f) && p.y <= piso + SoldierMotor.PivoteSobrePiso)
                {
                    p.y = piso + SoldierMotor.PivoteSobrePiso;
                    s.transform.position = p; Caidos++; Quitar(i); continue;
                }
                s.transform.position = p;
            }
        }

        void Quitar(int i) { enCaida.Remove(cayendo[i].soldado); cayendo.RemoveAt(i); }

        public static int AcostadosEnRampa { get; private set; }
        readonly HashSet<Soldier> acostados = new HashSet<Soldier>();

        // El cuerpo queda con los pies sobre la superficie de la rampa y su "arriba" alineado con la normal de la cuesta (yaw conservado): tendido PARALELO a los
        // escalones en vez de horizontal, que es lo que lo hacia atravesarlos. El pivote queda 0,8 m sobre la superficie siguiendo la normal.
        static void Acostar(Soldier s, RampaCaminable rampa, float ySuperficie)
        {
            var n = rampa.NormalDeLaSuperficie();
            var p = s.transform.position;
            s.transform.position = new Vector3(p.x, ySuperficie, p.z) + n * SoldierMotor.PivoteSobrePiso;
            float yaw = s.transform.eulerAngles.y;
            s.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, yaw, 0f);
            // el Animator / el motor le devuelven la rotacion vertical cada cuadro: se la fija mientras siga muerto
            var fijo = s.GetComponent<CuerpoEnRampa>(); if (fijo == null) fijo = s.gameObject.AddComponent<CuerpoEnRampa>();
            fijo.Fijar(s, n, yaw);
            AcostadosEnRampa++;
        }

        // Mantiene tendido sobre la pendiente al soldado muerto en una escalera; se retira solo si revive.
        [DefaultExecutionOrder(32000)]
        sealed class CuerpoEnRampa : MonoBehaviour
        {
            Soldier soldado; Vector3 normal; float yaw;
            public void Fijar(Soldier s, Vector3 n, float y) { soldado = s; normal = n; yaw = y; enabled = true; }
            void LateUpdate()
            {
                if (soldado == null || soldado.Health == null) { Destroy(this); return; }
                if (soldado.Health.IsAlive) { transform.rotation = Quaternion.Euler(0f, yaw, 0f); Destroy(this); return; }
                transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
