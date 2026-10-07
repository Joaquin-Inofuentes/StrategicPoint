using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Operacion
{
    // WP9b (#097/#073): cohete LENTO con aviso en el piso, comun a las emboscadas del canadon y al helicoptero Halcon. Al lanzarlo se muestra un
    // AnilloDeAviso en el punto de impacto durante "segundos"; el cohete vuela en arco hasta ahi (se ve, deja humo) y al llegar explota
    // (ExplodeAt, perdona a los enemigos). Se mueve con el tiempo ESCALADO: en la camara lenta del final se ve despacio.
    public class CoheteDeAviso : MonoBehaviour
    {
        public static int Lanzados { get; private set; }
        public static int Impactos { get; private set; }
        public static readonly List<CoheteDeAviso> Vivos = new List<CoheteDeAviso>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Lanzados = 0; Impactos = 0; Vivos.Clear(); }

        Vector3 desde, hasta;
        float dur, t, radio, altoDelArco;
        int dano;
        bool explota;
        float proximoHumo;
        AnilloDeAviso anillo;
        public Vector3 Destino => hasta;
        public float Progreso01 => dur > 0f ? Mathf.Clamp01(t / dur) : 1f;

        public static CoheteDeAviso Lanzar(Vector3 desde, Vector3 hasta, float segundos, int dano, float radio, bool conAnillo = true, bool explota = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CoheteDeAviso";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.position = desde;
            go.transform.localScale = new Vector3(0.28f, 0.28f, 0.8f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SafeMaterial.Create(new Color(1f, 0.55f, 0.18f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = go.AddComponent<CoheteDeAviso>();
            c.desde = desde; c.hasta = hasta; c.dur = Mathf.Max(0.2f, segundos); c.dano = dano; c.radio = radio; c.explota = explota;
            c.altoDelArco = Mathf.Clamp(Vector3.Distance(desde, hasta) * 0.08f, 0.5f, 6f);
            if (conAnillo) c.anillo = AnilloDeAviso.Mostrar(hasta, radio, segundos);
            Lanzados++;
            Vivos.Add(c);
            AudioDirector.PlayAt(SfxKind.CannonBody, desde, 0.5f, 1.5f);
            return c;
        }

        void OnDestroy() { Vivos.Remove(this); }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            var antes = transform.position;
            var pos = Vector3.Lerp(desde, hasta, k) + Vector3.up * (altoDelArco * 4f * k * (1f - k));
            transform.position = pos;
            var v = pos - antes;
            if (v.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(v.normalized);
            if (Time.time >= proximoHumo)
            {
                proximoHumo = Time.time + 0.05f;
                SpriteFx.Lanzar("smoke_04", pos, new Color(0.8f, 0.78f, 0.74f, 0.7f), 0.35f, 1.1f, 0.5f, Random.Range(-30f, 30f), Vector3.up * 0.4f, 0.3f, 2);
            }
            if (k >= 1f) Impactar();
        }

        void Impactar()
        {
            Impactos++;
            if (explota)
            {
                var p = hasta + Vector3.up * 0.4f;
                ImpactFx.SpawnExplosion(hasta + Vector3.up * 0.3f, Mathf.Max(2.5f, radio * 0.8f), false);
                AudioDirector.PlayAt(SfxKind.Explosion, p, 0.9f, 1.1f, PerfilEspacial.Explosion);
                Projectile.ExplodeAt(p, radio, dano, -3, TeamId.Enemy, null, 1f);
            }
            if (anillo != null) anillo.Cancelar();
            Destroy(gameObject);
        }
    }

    // Emboscada del canadon: 3 tiradores con lanzacohetes en una cornisa de 6 m. Cuando el tanque pasa a menos de DistanciaDeActivacion cada uno lanza
    // un cohete lento con aviso en el piso, adelantado al punto donde va a estar el tanque (1,3 s). Se los puede matar con el canon o la metralleta.
    public class EmboscadaDeCohetes : MonoBehaviour
    {
        public Soldier[] tiradores;
        public const float DistanciaDeActivacion = 75f, SegundosDeAviso = 1.3f, SegundosEntreSalvas = 7f, SegundosEntreTiradores = 0.45f;
        public const int DanoDelCohete = 60;
        public const float RadioDelCohete = 4.5f;

        public bool Armada { get; private set; }
        public int CohetesLanzados { get; private set; }
        public int TiradoresVivos { get { int n = 0; if (tiradores != null) foreach (var s in tiradores) if (s != null && s.Health != null && s.Health.IsAlive) n++; return n; } }
        public int TiradoresTotales => tiradores != null ? tiradores.Length : 0;

        Vehicle objetivo;
        VehicleMotor motorObjetivo;
        Vector3[] puestos;
        float[] proximoTiro;
        float proximaSalva;

        public void Armar(Vehicle tanque)
        {
            objetivo = tanque;
            motorObjetivo = tanque != null ? tanque.GetComponent<VehicleMotor>() : null;
            if (tiradores == null) return;
            puestos = new Vector3[tiradores.Length];
            proximoTiro = new float[tiradores.Length];
            for (int i = 0; i < tiradores.Length; i++)
            {
                var s = tiradores[i];
                if (s == null) continue;
                puestos[i] = s.transform.position;
                if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
                Fijar(s);
                proximoTiro[i] = 0f;
            }
            proximaSalva = Time.time;
            Armada = true;
        }

        public void Silenciar() { Armada = false; }
        public void Desarmar()
        {
            Armada = false;
            if (tiradores != null) foreach (var s in tiradores) if (s != null) s.gameObject.SetActive(false);
        }

        static void Fijar(Soldier s)
        {
            var agent = s.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.Quieto = true; }
        }

        void Update()
        {
            if (!Armada || objetivo == null || objetivo.IsDestroyed || tiradores == null) return;
            if (CinematicaDeOperacion.IntroActiva || CineDeHuida.Activa) return;
            var tp = objetivo.transform.position;
            // Salva: cuando toca y el tanque esta en alcance de alguno, cada tirador vivo suelta su cohete escalonado (0,45 s entre uno y otro).
            bool enAlcance = false;
            for (int i = 0; i < tiradores.Length && !enAlcance; i++)
            {
                var s = tiradores[i];
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                var dd = tp - s.transform.position; dd.y = 0f;
                if (dd.magnitude <= DistanciaDeActivacion) enAlcance = true;
            }
            if (enAlcance && Time.time >= proximaSalva)
            {
                proximaSalva = Time.time + SegundosEntreSalvas;
                for (int i = 0; i < tiradores.Length; i++) proximoTiro[i] = Time.time + SegundosEntreTiradores * i;
            }
            for (int i = 0; i < tiradores.Length; i++)
            {
                var s = tiradores[i];
                if (s == null || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                // Siguen quietos en la cornisa (el agente del NavMesh los bajaria al piso).
                if (s.transform.position.y < puestos[i].y - 1.5f) { s.transform.position = puestos[i]; }
                var agent = s.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled) agent.enabled = false;
                var d = tp - s.transform.position; d.y = 0f;
                if (d.magnitude > DistanciaDeActivacion) continue;
                if (d.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.Slerp(s.transform.rotation, Quaternion.LookRotation(d.normalized), 1f - Mathf.Exp(-6f * Time.deltaTime));
                if (proximoTiro[i] <= 0f || Time.time < proximoTiro[i]) continue;
                proximoTiro[i] = 0f;
                // Punto de impacto: donde va a estar el tanque cuando llegue el cohete (adelantado por su velocidad y rumbo).
                float vel = motorObjetivo != null ? motorObjetivo.CurrentSpeed : 0f;
                var fwd = objetivo.transform.forward; fwd.y = 0f; fwd.Normalize();
                var punto = tp + fwd * (vel * SegundosDeAviso) + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                punto.y = tp.y - 0.6f;
                var boca = s.transform.position + Vector3.up * 1.2f + s.transform.forward * 0.6f;
                CoheteDeAviso.Lanzar(boca, punto, SegundosDeAviso, DanoDelCohete, RadioDelCohete);
                CohetesLanzados++;
            }
        }
    }
}
