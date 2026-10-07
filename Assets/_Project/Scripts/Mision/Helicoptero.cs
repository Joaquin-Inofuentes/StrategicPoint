using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Mision
{
    // El helicoptero de extraccion (geometria P_Veh_Heli del arte): helices que giran, disco de
    // desenfoque a tope de vueltas, sonido de rotor real (Resources/Audio/Heli) con respaldo
    // procedural, nube de polvo al piso, ondas de viento circulares, y fuego de cobertura de sus dos
    // ametralladoras de puerta (PivoteMetralleta, una por lado, con artilleros) con balas trazadoras
    // cuando el jugador viene llegando.
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
        // #127: en la extraccion el heli cubre con fuego pesado: mas dano por bala y el radio de blanco que se pide (por defecto, el de siempre).
        public int DanoDeCobertura { get; set; } = 6;
        public float RadioDeBlancoActual { get; set; } = RadioDeBlanco;
        public float DispersionDeCobertura { get; set; } = 0.035f;   // radianes de dispersion del canon
        public float FactorDePausa { get; set; } = 1f;               // multiplica la pausa entre rafagas
        public void RestaurarCobertura() { DanoDeCobertura = 6; RadioDeBlancoActual = RadioDeBlanco; DispersionDeCobertura = 0.035f; FactorDePausa = 1f; }
        public int DisparosHechos { get; private set; }
        public bool Volando { get; set; }            // la cinematica lo mueve (el polvo ya no depende de esto: sale segun la altura sobre el piso)

        // Bug #075: altura sobre el piso (rayo hacia abajo cada 0,1 s) que maneja el polvo y las ondas de viento.
        public const float AlturaDeViento = 25f;
        public float Altura { get; private set; } = 999f;
        public Vector3 PuntoDePiso { get; private set; }
        public float EmisionDePolvo { get; private set; }
        public float EmisionDeResiduos { get; private set; }
        public int ParticulasDePolvo => polvo != null ? polvo.particleCount : 0;
        public int ParticulasDeResiduos => residuos != null ? residuos.particleCount : 0;
        public int MaximoDePolvo => polvo != null ? polvo.main.maxParticles : 0;
        public OndasDeRotor Ondas { get; private set; }
        public TripulacionDelHeli Tripulacion { get; private set; }
        public PivoteMetralleta[] Pivotes { get; private set; } = new PivoteMetralleta[0];
        // Bug #076: ultimo disparo de las ametralladoras (para checks y trazas): helicoptero, pivote, origen, direccion.
        public Vector3 UltimoOrigenDeDisparo { get; private set; }
        public PivoteMetralleta UltimoPivoteQueDisparo { get; private set; }
        public static event System.Action<Helicoptero, PivoteMetralleta, Vector3, Vector3> AlDisparar;
        float proximoRayo;
        static readonly RaycastHit[] bufferRayo = new RaycastHit[12];

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
            Pivotes = GetComponentsInChildren<PivoteMetralleta>(true);
            ArmarSonido();
            ArmarDisco();
            ArmarPolvo();
            ArmarResiduos();
            ArmarLuces();
            Ondas = gameObject.AddComponent<OndasDeRotor>();
            Tripulacion = gameObject.AddComponent<TripulacionDelHeli>();
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (matDisco != null) Destroy(matDisco);
            if (matBalizaCola != null) Destroy(matBalizaCola);
            if (matBalizaVientre != null) Destroy(matBalizaVientre);
            if (polvo != null) Destroy(polvo.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            if (residuos != null) Destroy(residuos.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }

        // Bug #051: "se quedo trabado el sonido del helicoptero" en la pantalla de victoria. La pantalla pone timeScale 0 y el
        // loop del rotor (AudioSource propio, no pasa por la pausa de AudioListener) seguia sonando a todo volumen para siempre.
        // La cinematica de salida lo baja con VolumenExtra mientras se aleja y al final lo apaga del todo.
        public float VolumenExtra { get; set; } = 1f;
        bool sonidoApagado;
        public bool SonidoApagado => sonidoApagado || sonido == null || !sonido.isPlaying;
        public void ApagarSonido()
        {
            sonidoApagado = true;
            VolumenExtra = 0f;
            if (sonido != null) sonido.Stop();
        }

        // P9 (#123): la cinematica de rapel lo necesita ya con el rotor a fondo (entra a toda velocidad) y apagado del todo al terminar.
        public void RotorAFondo() { Vueltas = VueltasEnAlerta; objetivoVueltas = VueltasEnAlerta; }
        public void PararRotor() { Vueltas = 0f; objetivoVueltas = 0f; }

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
            // Pedido explicito: "hay un sonido que ya no deberia estar del
            // antiguo efecto de helices, eliminalo el viejo, deja solo el
            // nuevo". Si el GameObject ya trae un AudioSource guardado en la
            // escena (de una version anterior, de antes de que este metodo
            // armara el suyo por codigo), sonarian DOS rotores a la vez -- el
            // viejo con su clip de esa epoca y el nuevo de aca abajo. Se
            // destruye cualquier AudioSource previo antes de crear el propio,
            // asi solo queda uno sonando.
            foreach (var viejo in GetComponents<AudioSource>()) Destroy(viejo);

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

        // ---------------- luces (que se vea de noche) ----------------
        // Pedido explicito: "el helicoptero de huida debe verse bien". Es un bulto oscuro contra un bosque oscuro: un foco calido
        // lo ilumina desde arriba (viaja con el, tambien al despegar) y dos balizas de navegacion parpadean para poder seguirlo
        // con la vista aunque se aleje: roja en la cola, blanca de destello abajo.
        Renderer balizaCola, balizaVientre;
        Material matBalizaCola, matBalizaVientre;

        void ArmarLuces()
        {
            // Solo las mallas del arte (no el sistema de particulas ni el disco del rotor, que aun no tienen caja valida).
            var rs = System.Array.FindAll(GetComponentsInChildren<MeshRenderer>(false), m => m.gameObject != disco && m.enabled);
            if (rs.Length == 0) return;
            var b = new Bounds(transform.InverseTransformPoint(rs[0].bounds.center), Vector3.zero);
            foreach (var r in rs)
            {
                var c = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var esq = c.center + Vector3.Scale(c.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    b.Encapsulate(transform.InverseTransformPoint(esq));
                }
            }
            // El largo del fuselaje puede ir sobre Z o sobre X segun el arte: la cola queda del lado del extremo mas alejado.
            bool largoEnZ = b.size.z >= b.size.x;
            Vector3 cola = b.center + (largoEnZ ? new Vector3(0f, b.extents.y * 0.3f, -b.extents.z) : new Vector3(-b.extents.x, b.extents.y * 0.3f, 0f));

            var foco = new GameObject("FocoDelHelicoptero");
            foco.transform.SetParent(transform, false);
            foco.transform.localPosition = new Vector3(b.center.x + 2f, b.max.y + 2.7f, b.center.z + 1f);
            var luz = foco.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.range = 24f;
            luz.intensity = 45f;
            luz.color = new Color(1f, 0.92f, 0.78f);
            luz.shadows = LightShadows.None;

            balizaCola = CrearBaliza("BalizaCola", cola, new Color(1f, 0.15f, 0.1f), 0.4f, out matBalizaCola);
            balizaVientre = CrearBaliza("BalizaVientre", new Vector3(b.center.x, b.min.y + 0.1f, b.center.z), new Color(1f, 1f, 0.95f), 0.32f, out matBalizaVientre);
        }

        Renderer CrearBaliza(string nombre, Vector3 posLocal, Color color, float diametro, out Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = nombre;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = posLocal;
            go.transform.localScale = Vector3.one * diametro;
            material = SafeMaterial.CreateLinea(color);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        void ParpadearBalizas()
        {
            float t = Time.time;
            if (balizaCola != null) balizaCola.enabled = Mathf.Repeat(t, 1.4f) < 0.7f;
            if (balizaVientre != null) balizaVientre.enabled = Mathf.Repeat(t, 1.1f) < 0.12f || (Mathf.Repeat(t, 1.1f) > 0.24f && Mathf.Repeat(t, 1.1f) < 0.36f);
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
        //
        // BUG REAL ("las particulas del helicoptero estan muy rotas"): la
        // primera version usaba SafeMaterial.Create() (material Lit/opaco
        // de un primitivo solido) en un renderer Billboard. Ese shader no
        // lee el color de vertice de colorOverLifetime NI hace blend
        // alfa -- las particulas nunca se desvanecian, aparecian y
        // desaparecian de golpe como cuadrados solidos girando para
        // encarar la camara. Ahora usa ParticleMaterialFactory (shader de
        // particulas de URP, con blend real) y ParticleSystemRenderMode.
        // Mesh con esferas de verdad en vez de cuadrados de cartel --
        // pedido explicito: "cubistos o esferas como se vean mejor".
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            // Tierra clara (no el marron oscuro del piso): contra el piso de
            // tierra del helipuerto, un polvo del MISMO tono quedaba
            // practicamente invisible en las capturas -- mas claro y con
            // mas alpha para que se note la nube contra el suelo oscuro.
            main.startColor = new Color(0.78f, 0.72f, 0.6f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 320;

            var emission = polvo.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // arranca apagado; Update() lo prende segun k

            var shape = polvo.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6f;
            shape.arc = 360f;
            shape.radiusThickness = 1f; // 1 = todo el disco (no solo el borde): tierra levantada bajo toda el area de las palas
            shape.rotation = new Vector3(90f, 0f, 0f);   // el circulo del Shape es vertical por defecto: se acuesta sobre el piso

            // Velocidad hacia afuera (radial), como tierra empujada por el
            // aire de las palas hacia los costados, no solo flotando quieta.
            var velOverLifetime = polvo.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velOverLifetime.radial = new ParticleSystem.MinMaxCurve(1.1f);

            // Rotacion continua por particula: mallas 3D quietas se leen
            // como piedras flotando; girando lento se leen como tierra
            // suelta arremolinada por el viento del rotor.
            var rotOverLifetime = polvo.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

            var colorOverLifetime = polvo.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradiente = new Gradient();
            gradiente.SetKeys(
                new[] { new GradientColorKey(new Color(0.78f, 0.72f, 0.6f), 0f), new GradientColorKey(new Color(0.85f, 0.8f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradiente;

            var sizeOverLifetime = polvo.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            rend.mesh = ParticleMaterialFactory.MallaEsfera();
            rend.alignment = ParticleSystemRenderSpace.World;
            rend.material = ParticleMaterialFactory.CreateTransparent(Color.white);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            polvo.Play();
        }

        // Bug #075: segunda capa: hojas, papeles y piedritas que vuela el viento del rotor. 20 particulas de malla chica,
        // chatas (tamano 3D) y con color al azar entre papel y hoja seca.
        ParticleSystem residuos;
        public const int MaximoDeResiduos = 20;

        void ArmarResiduos()
        {
            var go = new GameObject("ResiduosDelRotor");
            go.transform.SetParent(transform, false);
            residuos = go.AddComponent<ParticleSystem>();
            var main = residuos.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.9f, 0.82f, 1f), new Color(0.46f, 0.4f, 0.2f, 1f));
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaximoDeResiduos;

            var emission = residuos.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = residuos.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 3f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var vel = residuos.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(2.2f);   // todas las curvas de velocidad en el mismo modo (constante)
            vel.radial = new ParticleSystem.MinMaxCurve(2f);

            var rot = residuos.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            rend.mesh = ParticleMaterialFactory.MallaCubo();
            rend.alignment = ParticleSystemRenderSpace.World;
            rend.material = ParticleMaterialFactory.CreateTransparent(Color.white);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            residuos.Play();
        }

        // Rayo hacia abajo (cada 0,1 s) ignorando al propio helicoptero y a los soldados: altura y punto de piso.
        void MedirAltura()
        {
            if (Time.time < proximoRayo) return;
            proximoRayo = Time.time + 0.1f;
            var origen = transform.position + Vector3.up * 1.5f;
            int n = Physics.RaycastNonAlloc(origen, Vector3.down, bufferRayo, 200f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue; var punto = new Vector3(transform.position.x, 0f, transform.position.z); bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var c = bufferRayo[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<Soldier>() != null) continue;
                if (bufferRayo[i].distance < mejor) { mejor = bufferRayo[i].distance; punto = bufferRayo[i].point; hay = true; }
            }
            Altura = hay ? Mathf.Max(0f, transform.position.y - punto.y) : 999f;
            PuntoDePiso = punto;
            if (hay) CasquillosDeMetralleta.PisoY = punto.y;
        }

        // ---------------- frame ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            ParpadearBalizas();
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
                sonido.volume = Mathf.Lerp(0.30f, 1f, k) * arrancando * Mathf.Clamp01(VolumenExtra) * SP.Presentation.AudioDirector.GainFor(SP.Presentation.SfxChannel.Sfx);
                sonido.pitch = Mathf.Lerp(0.85f, 1.25f, k);
                if (sonidoApagado && sonido.isPlaying) sonido.Stop();
            }
            if (disco != null && matDisco != null)
            {
                bool ver = k > 0.35f;
                if (disco.activeSelf != ver) disco.SetActive(ver);
                var c = matDisco.color; c.a = Mathf.Lerp(0f, 0.28f, Mathf.InverseLerp(0.35f, 1f, k));
                matDisco.color = c;
                if (matDisco.HasProperty("_BaseColor")) matDisco.SetColor("_BaseColor", c);
            }

            // Bug #075: polvo, residuos y ondas segun la altura sobre el piso (antes solo con k>0.5 y !Volando, y el director
            // ponia Volando al bajar y al despegar: casi nunca habia polvo). Con el rotor a fondo y a menos de 25 m del piso
            // la tasa va de 40 a 160 con la cercania, tanto al aterrizar como al despegar.
            MedirAltura();
            float cerca = 1f - Mathf.Clamp01(Altura / AlturaDeViento);
            bool sopla = k > 0.5f && Altura < AlturaDeViento;
            EmisionDePolvo = sopla ? Mathf.Lerp(40f, 160f, cerca) : 0f;
            EmisionDeResiduos = sopla ? Mathf.Lerp(4f, 14f, cerca) : 0f;
            if (polvo != null)
            {
                var emission = polvo.emission;
                emission.rateOverTime = EmisionDePolvo;
                var forma = polvo.shape;
                forma.radius = Mathf.Lerp(6f, 10f, 1f - cerca);   // el chorro se abre con la altura
            }
            if (residuos != null)
            {
                var emission = residuos.emission;
                emission.rateOverTime = EmisionDeResiduos;
            }
            // Las ondas van 0,3 m sobre el piso: el asfalto y la losa del helipuerto son visuales sin colision y quedan a ~0,1 m (a 0,1 las tapaban).
            if (Ondas != null) Ondas.Tick(sopla, PuntoDePiso + Vector3.up * 0.3f, Mathf.Lerp(0.55f, 1f, cerca), dt);

            if (DisparaCobertura && k > 0.6f) Cobertura(dt);
        }

        // Los emisores de polvo y residuos van sobre el piso, bajo el helicoptero, siempre horizontales.
        void LateUpdate()
        {
            var p = new Vector3(transform.position.x, (Altura < 900f ? PuntoDePiso.y : transform.position.y - 2f) + 0.12f, transform.position.z);
            if (polvo != null) polvo.transform.SetPositionAndRotation(p, Quaternion.identity);
            if (residuos != null) residuos.transform.SetPositionAndRotation(p + Vector3.up * 0.1f, Quaternion.identity);
        }

        // ---------------- ametralladoras de puerta (bug #076) ----------------
        // Dos pivotes (izquierda y derecha) que giran hacia el blanco dentro de +-70 grados de su lado y disparan balas REALES
        // (con estela) desde la boca del canon: 8 por segundo en rafagas de 1,2 s con pausas. Blanco: el enemigo mas cercano al
        // helicoptero o al lider, a menos de 70 m, que caiga dentro del arco del pivote. Fogonazo, humo, luz y casquillos.
        public const float RadioDeBlanco = 70f, CadenciaPorSegundo = 8f, DuracionDeRafaga = 1.2f;

        class EstadoDePivote { public Soldier objetivo; public float rafagaHasta, pausaHasta, proximoDisparo; public int cuenta; }
        EstadoDePivote[] estados;

        void Cobertura(float dt)
        {
            if (Pivotes == null || Pivotes.Length == 0) { CoberturaFija(dt); return; }
            if (estados == null || estados.Length != Pivotes.Length)
            {
                estados = new EstadoDePivote[Pivotes.Length];
                for (int i = 0; i < estados.Length; i++) estados[i] = new EstadoDePivote();
            }
            if (pool == null) pool = ProjectilePool.Activo;
            for (int i = 0; i < Pivotes.Length; i++) TickPivote(Pivotes[i], estados[i], dt);
        }

        void TickPivote(PivoteMetralleta p, EstadoDePivote e, float dt)
        {
            if (p == null) return;
            bool valido = e.objetivo != null && e.objetivo.Health != null && e.objetivo.Health.IsAlive && e.objetivo.gameObject.activeInHierarchy;
            if (Time.time >= e.rafagaHasta || !valido)
            {
                // Rafaga terminada (o blanco perdido): pausa y busca otro.
                if (Time.time < e.pausaHasta) { p.Reposo(dt); return; }
                var nuevo = BuscarObjetivo(p);
                if (nuevo == null) { e.objetivo = null; e.pausaHasta = Time.time + 0.4f; p.Reposo(dt); return; }
                e.objetivo = nuevo;
                e.rafagaHasta = Time.time + DuracionDeRafaga;
                e.pausaHasta = e.rafagaHasta + Random.Range(0.5f, 0.9f) * FactorDePausa;
                e.proximoDisparo = Time.time + 0.1f;   // el canon necesita un instante para encarar
            }
            var mira = e.objetivo.transform.position + Vector3.up * 0.9f;
            float error = p.Apuntar(mira, dt);
            if (error > 6f || Time.time < e.proximoDisparo || pool == null) return;
            e.proximoDisparo = Time.time + 1f / CadenciaPorSegundo;
            Disparar(p, e, mira);
        }

        void Disparar(PivoteMetralleta p, EstadoDePivote e, Vector3 mira)
        {
            var boca = p.Boca.position;
            var dir = (mira - boca).normalized;
            dir = (dir + Random.insideUnitSphere * DispersionDeCobertura).normalized;
            pool.Spawn(boca, dir, -1, TeamId.Player, DanoDeCobertura, new Color(1f, 0.85f, 0.35f));
            UltimoOrigenDeDisparo = boca; UltimoPivoteQueDisparo = p;
            DisparosHechos++; e.cuenta++;
            // Fogonazo (como OperacionAuto.EfectoDeDisparo), luz, estampido de ametralladora pesada y casquillo.
            SpriteFx.Lanzar("muzzle_01", boca + dir * 0.25f, new Color(1f, 0.82f, 0.45f, 1f), 0.55f, 0.85f, 0.06f, Random.Range(0f, 360f), default, 0f, 6);
            if (e.cuenta % 3 == 0)
                SpriteFx.Lanzar("smoke_02", boca + dir * 0.4f, new Color(0.6f, 0.58f, 0.55f, 0.35f), 0.25f, 0.9f, 0.45f, Random.Range(-40f, 40f), dir * 1.5f + Vector3.up * 0.4f, 2.5f, 1);
            MuzzleLightPool.Flash(boca, new Color(1f, 0.8f, 0.4f), 6f, 10f);
            var clip = GenericSfx.GetWeaponShot(WeaponKind.Heavy);
            if (clip != null) AudioDirector.PlayClipAt(clip, boca, 0.6f, 0.9f, PerfilEspacial.Disparo, 0.95f + 0.1f * Random.value);
            CasquillosDeMetralleta.Expulsar(boca - dir * 0.5f + Vector3.up * 0.1f, p.transform.right * 0.6f + p.transform.up * 0.3f + transform.right * p.lado);
            AlDisparar?.Invoke(this, p, boca, dir);
        }

        Soldier BuscarObjetivo(PivoteMetralleta p)
        {
            Soldier mejor = null; float mejorD = RadioDeBlancoActual * RadioDeBlancoActual;
            var lider = SP.Ai.AjustesDeEscuadra.Lider;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                float d = (s.transform.position - transform.position).sqrMagnitude;
                if (lider != null) d = Mathf.Min(d, (s.transform.position - lider.transform.position).sqrMagnitude);
                if (d >= mejorD) continue;
                // Tiene que caer dentro del arco de este pivote (con margen de 8 grados).
                float rumbo = p.RumboHacia(s.transform.position + Vector3.up * 0.9f, out _);
                if (!p.EnArco(rumbo, 8f)) continue;
                mejorD = d; mejor = s;
            }
            return mejor;
        }

        // ---- respaldo: helicoptero sin pivotes (el modelo de arte P_Veh_Heli de la mision vieja, SC_Gameplay) ----
        // Ametralladora de puerta fija a la derecha, como antes del bug #076: rafagas de ~1 s contra el enemigo mas cercano.
        float proximoDisparoFija, rafagaHastaFija, pausaHastaFija;
        Soldier objetivoFija;

        void CoberturaFija(float dt)
        {
            if (Time.time < pausaHastaFija) return;
            if (Time.time > rafagaHastaFija)
            {
                objetivoFija = BuscarObjetivoFijo();
                if (objetivoFija == null) { pausaHastaFija = Time.time + 0.5f; return; }
                rafagaHastaFija = Time.time + Random.Range(0.7f, 1.2f);
                pausaHastaFija = rafagaHastaFija + Random.Range(0.5f, 0.9f);
            }
            if (Time.time < rafagaHastaFija && Time.time >= proximoDisparoFija && objetivoFija != null && objetivoFija.Health.IsAlive)
            {
                proximoDisparoFija = Time.time + 0.09f;
                if (pool == null) pool = ProjectilePool.Activo;
                if (pool == null) return;
                var boca = transform.position + transform.right * 1.3f + Vector3.up * 1.2f;
                var mira = objetivoFija.transform.position + Vector3.up * 0.9f;
                var dir = (mira - boca).normalized;
                dir = (dir + Random.insideUnitSphere * 0.035f).normalized;
                pool.Spawn(boca, dir, -1, TeamId.Player, 6, new Color(1f, 0.85f, 0.35f));
                MuzzleLightPool.Flash(boca, new Color(1f, 0.8f, 0.4f), 6f, 10f);
                AudioDirector.PlayAt(SfxKind.Shoot, boca, 0.45f);
                UltimoOrigenDeDisparo = boca;
                DisparosHechos++;
            }
        }

        Soldier BuscarObjetivoFijo()
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
