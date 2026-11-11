using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;

namespace SP.Operacion
{
    // Bug #381 (2026-11-08: "quiero q haya flancotiradores"). Soldado enemigo que ve a su objetivo a mas de 20 m se planta, lo MARCA con un laser rojo
    // 1,4 s (avisa que viene el tiro) y dispara un solo balazo rapido y fuerte; recarga 3,6 s. Se lo reconoce por el laser y por el tono verde oliva.
    public sealed class EnemigoFrancotirador : MonoBehaviour
    {
        public const float AlcanceMaximo = 95f, AlcanceMinimo = 18f, SegundosDeMira = 1.4f, SegundosEntreTiros = 3.6f;
        public const int Dano = 38;
        public static readonly Color ColorDelLaser = new Color(1f, 0.1f, 0.08f, 0.9f);

        public static int Creados { get; private set; }
        public static int TirosTotales { get; private set; }
        public int Tiros { get; private set; }
        public bool Marcando { get; private set; }
        public LineRenderer Laser { get; private set; }

        Soldier soldado; AiBrain cerebro; float marcaDesde = -1f, proximoTiro;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Creados = 0; TirosTotales = 0; }

        public static EnemigoFrancotirador Aplicar(Soldier s)
        {
            if (s == null || s.Team != TeamId.Enemy) return null;
            var ya = s.GetComponent<EnemigoFrancotirador>(); if (ya != null) return ya;
            s.gameObject.name = "Francotirador_" + (++Creados);
            return s.gameObject.AddComponent<EnemigoFrancotirador>();
        }

        void Start()
        {
            soldado = GetComponent<Soldier>(); cerebro = soldado != null ? soldado.Brain : null;
            var go = new GameObject("Laser_Francotirador"); go.transform.SetParent(transform, false);
            Laser = go.AddComponent<LineRenderer>();
            Laser.positionCount = 2; Laser.startWidth = 0.035f; Laser.endWidth = 0.02f; Laser.useWorldSpace = true; Laser.enabled = false;
            var m = new Material(Shader.Find("Sprites/Default")); m.color = ColorDelLaser; Laser.material = m;
            Laser.startColor = Laser.endColor = ColorDelLaser; Laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            proximoTiro = Time.time + Random.Range(1.5f, 3f);
            // Tono verde oliva: se distingue del asalto comun.
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var mats = r.materials;
                foreach (var mm in mats) if (mm != null && mm.HasProperty("_BaseColor")) mm.SetColor("_BaseColor", Color.Lerp(mm.GetColor("_BaseColor"), new Color(0.35f, 0.42f, 0.2f), 0.55f));
                r.materials = mats;
            }
        }

        void Update()
        {
            if (cerebro == null || soldado == null || soldado.Health == null || !soldado.Health.IsAlive) { Apagar(); return; }
            var objetivo = cerebro.CurrentTarget;
            if (objetivo == null || objetivo.Health == null || !objetivo.Health.IsAlive) objetivo = BuscarObjetivo();   // el francotirador elige solo: el mas cercano a tiro
            bool ve = objetivo != null;
            float d = ve ? Vector3.Distance(transform.position, objetivo.transform.position) : 0f;
            if (ve && d < AlcanceMinimo) { var otro = BuscarObjetivo(); if (otro != null) { objetivo = otro; d = Vector3.Distance(transform.position, objetivo.transform.position); } }
            if (!ve || d > AlcanceMaximo || d < AlcanceMinimo) { Razon = ve ? (d > AlcanceMaximo ? "lejos" : "cerca") : "sin objetivo"; Apagar(); cerebro.Quieto = false; return; }
            cerebro.Quieto = true;   // se planta a tirar desde lejos
            var origen = transform.position + Vector3.up * 1.55f;
            var blanco = objetivo.transform.position + Vector3.up * 1.1f;
            var dir = (blanco - origen).normalized;
            // Sin linea de tiro limpia no marca ni dispara.
            if (Physics.Raycast(origen, dir, out var hit, d, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Soldier>() != objetivo && hit.collider.GetComponentInParent<SP.Vehicles.Vehicle>() == null) { Razon = "sin linea de tiro (" + hit.collider.name + ")"; Apagar(); return; }
            if (Time.time < proximoTiro) { Razon = "recargando"; Apagar(); return; }
            Razon = "marcando";
            if (!Marcando) { Marcando = true; marcaDesde = Time.time; }
            Laser.enabled = true; Laser.SetPosition(0, origen); Laser.SetPosition(1, blanco);
            if (Time.time - marcaDesde < SegundosDeMira) return;
            Disparar(origen, dir);
        }

        public string Razon { get; private set; } = "";

        Soldier BuscarObjetivo()
        {
            Soldier mejor = null; float md = AlcanceMaximo;
            foreach (var a in ActorRegistry.All)
            {
                if (a == null || a.Team != TeamId.Player || a.Encarcelado || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(transform.position, a.transform.position);
                if (d < AlcanceMinimo) continue;   // a menos de 18 m no es trabajo de francotirador
                if (d < md) { md = d; mejor = a; }
            }
            return mejor;
        }

        void Disparar(Vector3 origen, Vector3 dir)
        {
            var pool = ProjectilePool.Activo;
            if (pool != null)
            {
                pool.Spawn(origen, dir, soldado.Id, TeamId.Enemy, Dano, new Color(1f, 0.95f, 0.6f), 0f, 0f, null, 2.2f);
                SP.Presentation.MuzzleLightPool.Flash(origen + dir * 0.6f, new Color(1f, 0.8f, 0.4f), 5f, 9f);
            }
            Tiros++; TirosTotales++;
            proximoTiro = Time.time + SegundosEntreTiros * Random.Range(0.9f, 1.2f);
            Apagar();
        }

        void Apagar() { Marcando = false; marcaDesde = -1f; if (Laser != null) Laser.enabled = false; }
    }
}
