using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Mision
{
    // El helicoptero de extraccion (geometria P_Veh_Heli del arte): helices que giran, disco de
    // desenfoque a tope de vueltas, sonido de rotor real (Resources/Audio/Heli) con respaldo
    // procedural, nube de polvo al piso y fuego de cobertura de su ametralladora de puerta con
    // balas trazadoras cuando el jugador viene llegando.
    public class Helicoptero : MonoBehaviour
    {
        public const float VueltasEnEspera = 260f;    // grados/s con el motor al ralenti
        public const float VueltasEnAlerta = 1150f;   // grados/s con el motor a fondo

        public static Helicoptero Instancia { get; private set; }

        Transform rotorPrincipal, rotorCola;
        AudioSource sonido;
        GameObject disco;
        Material matDisco;
        float objetivoVueltas = VueltasEnEspera;
        float proximoPolvo, proximoDisparo, rafagaHasta, pausaHasta;
        ProjectilePool pool;

        public float Vueltas { get; private set; } = VueltasEnEspera;
        public bool EnAlerta => objetivoVueltas > VueltasEnEspera;
        public bool DisparaCobertura { get; set; }
        public int DisparosHechos { get; private set; }
        public bool Volando { get; set; }            // la cinematica lo mueve: sin polvo de suelo

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Instancia = null;

        void Awake()
        {
            Instancia = this;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("RotorPrincipal")) rotorPrincipal = t;
                else if (t.name.Contains("RotorCola")) rotorCola = t;
            }
            ArmarSonido();
            ArmarDisco();
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; if (matDisco != null) Destroy(matDisco); }

        public void Alerta(bool on)
        {
            objetivoVueltas = on ? VueltasEnAlerta : VueltasEnEspera;
        }

        // ---------------- sonido ----------------
        // Rotor real: "Helicopter Rotor Loop" de qubodup (freesound.org/people/qubodup/sounds/187681),
        // extraido de un video de una agencia del gobierno de EE.UU. -- CC0/dominio publico. Recortado
        // (se le sacaron los bordes de silencio y se le puso un fundido de 12 ms en cada punta para que
        // el loop no chasquee).
        void ArmarSonido()
        {
            var clip = SP.Core.RecursosCache.Cargar<AudioClip>("Audio/Heli/RotorReal_Freesound_qubodup");
            if (clip == null) clip = GenerarRotor();
            sonido = gameObject.AddComponent<AudioSource>();
            sonido.clip = clip;
            sonido.loop = true;
            sonido.spatialBlend = 1f;
            sonido.minDistance = 12f;
            sonido.maxDistance = 220f;
            sonido.rolloffMode = AudioRolloffMode.Linear;
            sonido.volume = 0.35f;
            sonido.Play();
        }

        public AudioClip ClipDeRotor => sonido != null ? sonido.clip : null;

        // Respaldo procedural: golpe de pala a ~11 Hz sobre ruido grave.
        static AudioClip GenerarRotor()
        {
            const int sr = 22050;
            int n = sr * 2;
            var d = new float[n];
            var rng = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float golpe = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 11f * t)), 6f);
                float ruido = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (ruido - lp) * 0.12f;
                d[i] = Mathf.Clamp((golpe * 0.9f + 0.25f) * lp * 1.8f + 0.25f * Mathf.Sin(2f * Mathf.PI * 55f * t) * golpe, -1f, 1f);
            }
            var clip = AudioClip.Create("RotorProcedural", n, 1, sr, false);
            clip.SetData(d, 0);
            return clip;
        }

        // ---------------- disco de desenfoque ----------------
        void ArmarDisco()
        {
            if (rotorPrincipal == null) return;
            disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disco.name = "DiscoDelRotor";
            Destroy(disco.GetComponent<Collider>());
            disco.transform.SetParent(rotorPrincipal, false);
            disco.transform.localPosition = Vector3.zero;
            // Cilindro de 1 m de diametro por 2 de alto, aplastado: disco de 8,6 m.
            disco.transform.localScale = new Vector3(8.6f, 0.01f, 8.6f);
            matDisco = CoverHologram.NuevoTransparente(new Color(0.85f, 0.88f, 0.9f, 0f));
            var r = disco.GetComponent<MeshRenderer>();
            r.sharedMaterial = matDisco;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disco.SetActive(false);
        }

        // ---------------- frame ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            Vueltas = Mathf.MoveTowards(Vueltas, objetivoVueltas, 420f * dt);
            if (rotorPrincipal != null) rotorPrincipal.Rotate(0f, Vueltas * dt, 0f, Space.Self);
            if (rotorCola != null) rotorCola.Rotate(Vueltas * 1.6f * dt, 0f, 0f, Space.Self);

            float k = Mathf.InverseLerp(VueltasEnEspera, VueltasEnAlerta, Vueltas);
            if (sonido != null)
            {
                // El rotor no pasaba por ningun canal de mezcla: sonaba
                // igual de fuerte aunque el jugador bajara "Efectos" a
                // cero en Opciones. GainFor se relee cada frame (barato,
                // cache en memoria) para que el slider surta efecto en vivo.
                sonido.volume = Mathf.Lerp(0.30f, 1f, k) * SP.Presentation.AudioDirector.GainFor(SP.Presentation.SfxChannel.Sfx);
                sonido.pitch = Mathf.Lerp(0.85f, 1.25f, k);
            }
            if (disco != null && matDisco != null)
            {
                bool ver = k > 0.35f;
                if (disco.activeSelf != ver) disco.SetActive(ver);
                var c = matDisco.color; c.a = Mathf.Lerp(0f, 0.28f, Mathf.InverseLerp(0.35f, 1f, k));
                matDisco.color = c;
                if (matDisco.HasProperty("_BaseColor")) matDisco.SetColor("_BaseColor", c);
            }

            if (k > 0.5f && !Volando && Time.time >= proximoPolvo)
            {
                proximoPolvo = Time.time + 0.16f;
                float ang = Random.value * Mathf.PI * 2f;
                var p = transform.position + new Vector3(Mathf.Cos(ang), 0.25f, Mathf.Sin(ang)) * Random.Range(3f, 6.5f);
                ImpactFx.Spawn(p, new Color(0.62f, 0.5f, 0.36f, 1f), Random.Range(1.4f, 2.6f), 0.7f);
            }

            if (DisparaCobertura && k > 0.6f) Cobertura(dt);
        }

        // ---------------- ametralladora de puerta ----------------
        // Rafagas de ~0,8 s con pausas: tira balas REALES (con estela) contra el enemigo
        // mas cercano al helicoptero o al jugador, para que se vea el fuego de cobertura.
        void Cobertura(float dt)
        {
            if (Time.time < pausaHasta) return;
            if (Time.time > rafagaHasta)
            {
                var objetivoNuevo = BuscarObjetivo();
                if (objetivoNuevo == null) { pausaHasta = Time.time + 0.5f; return; }
                rafagaHasta = Time.time + Random.Range(0.7f, 1.2f);
                pausaHasta = rafagaHasta + Random.Range(0.5f, 0.9f);
                objetivo = objetivoNuevo;
            }
            if (Time.time < rafagaHasta && Time.time >= proximoDisparo && objetivo != null && objetivo.Health.IsAlive)
            {
                proximoDisparo = Time.time + 0.09f;
                if (pool == null) pool = ProjectilePool.Activo;
                if (pool == null) return;
                var boca = transform.position + transform.right * 1.3f + Vector3.up * 1.2f;
                var mira = objetivo.transform.position + Vector3.up * 0.9f;
                var dir = (mira - boca).normalized;
                dir = (dir + Random.insideUnitSphere * 0.035f).normalized;
                pool.Spawn(boca, dir, -1, TeamId.Player, 6, new Color(1f, 0.85f, 0.35f));
                MuzzleLightPool.Flash(boca, new Color(1f, 0.8f, 0.4f), 6f, 10f);
                AudioDirector.PlayAt(SfxKind.Shoot, boca, 0.45f);
                DisparosHechos++;
            }
        }

        Soldier objetivo;

        Soldier BuscarObjetivo()
        {
            Soldier mejor = null; float mejorD = 55f * 55f;
            var lider = SP.Ai.AjustesDeEscuadra.Lider;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = (s.transform.position - transform.position).sqrMagnitude;
                if (lider != null) d = Mathf.Min(d, (s.transform.position - lider.transform.position).sqrMagnitude);
                if (d < mejorD) { mejorD = d; mejor = s; }
            }
            return mejor;
        }
    }
}
