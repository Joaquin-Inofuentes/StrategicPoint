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
        // Pedido explicito: "las helices del helicoptero q al inicio esten
        // apagado solamente se encienda cuando estes volviendo". Antes
        // objetivoVueltas arrancaba directo en VueltasEnEspera: el
        // helicoptero giraba en ralenti desde el primer frame de la
        // mision, aunque el jugador estuviera del otro lado del mapa sin
        // haber rescatado a nadie todavia. Ahora arranca parado (0) y solo
        // se prende con el primer Alerta(...) real -- que en los hechos ya
        // solo llega cuando arranca la fase Escapar (MisionDirector.
        // TickRescatar llama Alerta(false) justo al empezar a volver, y
        // TickEscapar sigue llamando Alerta(cerca) de ahi en mas), asi que
        // no hace falta tocar ningun otro archivo.
        float objetivoVueltas = 0f;
        float proximoDisparo, rafagaHasta, pausaHasta;
        ProjectilePool pool;

        public float Vueltas { get; private set; } = 0f;
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
            ArmarPolvo();
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (matDisco != null) Destroy(matDisco);
            if (polvo != null) Destroy(polvo.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }

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

        // ---------------- polvo del rotor ----------------
        // Pedido explicito: "q tenga un sistema de particulas para emular
        // el polvo q expulsa". Antes esto eran puffs sueltos de ImpactFx
        // (el mismo helper de "fisica simple" que usa el resto del juego
        // para impactos/curaciones) -- funcional, pero un helicoptero
        // levantando tierra de verdad se lee mejor como una nube continua
        // que como bochas individuales apareciendo una por una. Este es el
        // primer ParticleSystem de verdad del proyecto (no hay ningun otro
        // en el codebase para copiar): shape en disco a ras de piso,
        // emision solo mientras el rotor gira rapido, color tierra que se
        // desvanece y crece un poco con la vida de la particula.
        ParticleSystem polvo;

        void ArmarPolvo()
        {
            var go = new GameObject("PolvoDelRotor");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            polvo = go.AddComponent<ParticleSystem>();
            var main = polvo.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
            // Tierra clara (no el marron oscuro del piso): contra el piso de
            // tierra del helipuerto, un polvo del MISMO tono quedaba
            // practicamente invisible en las capturas -- mas claro y con
            // mas alpha para que se note la nube contra el suelo oscuro.
            main.startColor = new Color(0.78f, 0.72f, 0.6f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = polvo.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // arranca apagado; Update() lo prende segun k

            var shape = polvo.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6f;
            shape.arc = 360f;
            shape.radiusThickness = 1f; // 1 = todo el disco (no solo el borde): tierra levantada bajo toda el area de las palas

            var colorOverLifetime = polvo.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradiente = new Gradient();
            gradiente.SetKeys(
                new[] { new GradientColorKey(new Color(0.78f, 0.72f, 0.6f), 0f), new GradientColorKey(new Color(0.85f, 0.8f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradiente;

            var sizeOverLifetime = polvo.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material = SafeMaterial.Create(Color.white);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            polvo.Play();
        }

        // ---------------- frame ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            Vueltas = Mathf.MoveTowards(Vueltas, objetivoVueltas, 420f * dt);
            if (rotorPrincipal != null) rotorPrincipal.Rotate(0f, Vueltas * dt, 0f, Space.Self);
            if (rotorCola != null) rotorCola.Rotate(Vueltas * 1.6f * dt, 0f, 0f, Space.Self);

            float k = Mathf.InverseLerp(VueltasEnEspera, VueltasEnAlerta, Vueltas);
            // Motor apagado (Vueltas todavia subiendo desde 0 hacia el ralenti):
            // sin este factor el piso de volumen de abajo (0.30) sonaba igual
            // de fuerte con el rotor parado que en ralenti real.
            float arrancando = Mathf.InverseLerp(0f, VueltasEnEspera, Vueltas);
            if (sonido != null)
            {
                // El rotor no pasaba por ningun canal de mezcla: sonaba
                // igual de fuerte aunque el jugador bajara "Efectos" a
                // cero en Opciones. GainFor se relee cada frame (barato,
                // cache en memoria) para que el slider surta efecto en vivo.
                sonido.volume = Mathf.Lerp(0.30f, 1f, k) * arrancando * SP.Presentation.AudioDirector.GainFor(SP.Presentation.SfxChannel.Sfx);
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

            // Nube continua (ParticleSystem) en vez de puffs sueltos de
            // ImpactFx: emite mientras el rotor gira rapido y no esta en la
            // cinematica de despegue (Volando), con la tasa creciendo con
            // k para que se note la diferencia entre ralenti y alerta.
            if (polvo != null)
            {
                var emission = polvo.emission;
                emission.rateOverTime = (k > 0.5f && !Volando) ? Mathf.Lerp(30f, 90f, Mathf.InverseLerp(0.5f, 1f, k)) : 0f;
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
