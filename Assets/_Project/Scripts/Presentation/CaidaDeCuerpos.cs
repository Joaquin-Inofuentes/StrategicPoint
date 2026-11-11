using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;

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
        static void Reiniciar() { instancia = null; Caidos = 0; }

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
    }
}
