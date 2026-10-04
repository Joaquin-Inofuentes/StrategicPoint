using UnityEngine;
using SP.Combat;
using SP.Vehicles;

namespace SP.Operacion
{
    // CAMIONETA enemiga con metralleta que persigue al tanque por la ruta de escape (los enemigos manejan camionetas, el jugador un
    // tanque). Es un Vehicle (vida + dano real: el canon le pega por el mismo camino que a un tanque) pero se mueve solo, sin
    // VehicleMotor ni tripulacion. Al morir queda de carcasa unos segundos y el director manda otra.
    //
    // Recorrido y ataque (pedido: "que tengan mejor recorrido los enemigos y ataquen mejor"): ya no se quedan clavados en una
    // posicion detras del tanque. Cada una cambia de TACTICA cada 5-9 s:
    //   Acosar     detras y al costado, tirando rafagas con la mira adelantada (lidera al tanque, que va a 17 m/s)
    //   Flanquear  en paralelo, a quemarropa: el cañon tiene que girar 90 grados para contestar
    //   Adelantar  pasa al tanque y se cruza adelante, disparando hacia atras
    //   Embestir   se tira encima del tanque: el choque le saca vida al tanque (y a ella misma)
    // y zigzaguea todo el tiempo para ser un blanco dificil. La dispersion baja con la cercania.
    [RequireComponent(typeof(Vehicle))]
    public partial class OperacionAuto : MonoBehaviour
    {
        Transform visualCache;
        public enum Tactica { Acosar, Flanquear, Adelantar, Embestir }

        public Transform objetivo;
        public Vector3 offsetDeseado = new Vector3(5f, 0f, -14f);   // x lateral, z relativo al frente del tanque
        public int danoPorBala = 7;
        public float rafagaSegundos = 0.9f, pausaSegundos = 0.9f, cadencia = 0.1f, alcance = 60f, dispersionGrados = 3.2f;
        public int danoDeEmbestida = 24;
        public Transform metralleta;                                // cubo que apunta al tanque
        public Transform boca;

        public Vehicle Vehiculo { get; private set; }
        public bool Muerto => Vehiculo != null && Vehiculo.IsDestroyed;
        public int Disparos { get; private set; }
        public int Embestidas { get; private set; }
        public float VelocidadActual { get; private set; }
        public Tactica TacticaActual { get; private set; } = Tactica.Acosar;
        float proximoDisparo, rafagaHasta, pausaHasta, muertoDesde = -1f, cambioDeTactica, proximaEmbestida, faseZigzag;
        float lateralBase = 5f;
        VehicleMotor motorObjetivo;
        Vehicle vehiculoObjetivo;
        ProjectilePool pool;
        Vector3 posPrevObjetivo; Vector3 velObjetivo;

        void Awake()
        {
            Vehiculo = GetComponent<Vehicle>();
            if (Vehiculo != null) Vehiculo.MarcarBando(TeamId.Enemy);
        }

        // Bug #047: "velocidades reales". Antes aceleraba a 20 m/s² y llegaba a 37 m/s (135 km/h) para alcanzar al tanque,
        // y giraba 130°/s a cualquier velocidad (doblaba en seco como en rieles). Ahora: tope ~100 km/h, 5 m/s² (0-100 en
        // ~5,5 s, una camioneta cargada) y el giro limitado por la aceleracion lateral que aguantan las ruedas.
        public const float VelocidadMaxima = 28f, Aceleracion = 5f, Frenada = 8f, AceleracionLateral = 7f;

        static float GiroMaximo(float v) => Mathf.Min(130f, Mathf.Rad2Deg * AceleracionLateral / Mathf.Max(1f, Mathf.Abs(v)) * 1.6f);

        void Start()
        {
            pausaHasta = Time.time + Random.Range(0.3f, 1.2f);
            if (objetivo != null)
            {
                motorObjetivo = objetivo.GetComponent<VehicleMotor>();
                vehiculoObjetivo = objetivo.GetComponent<Vehicle>();
                posPrevObjetivo = objetivo.position;
            }
            lateralBase = Mathf.Max(3.5f, Mathf.Abs(offsetDeseado.x));
            faseZigzag = Random.value * 6.28f;
            ElegirTactica();
            PrepararEfectos();
        }

        void ElegirTactica()
        {
            cambioDeTactica = Time.time + Random.Range(5f, 9f);
            float r = Random.value;
            TacticaActual = r < 0.38f ? Tactica.Acosar : r < 0.62f ? Tactica.Flanquear : r < 0.82f ? Tactica.Adelantar : Tactica.Embestir;
            float lado = Random.value < 0.5f ? -1f : 1f;
            switch (TacticaActual)
            {
                case Tactica.Acosar: offsetDeseado = new Vector3(lado * lateralBase, 0f, -Random.Range(11f, 19f)); break;
                case Tactica.Flanquear: offsetDeseado = new Vector3(lado * Random.Range(5.2f, 6.5f), 0f, Random.Range(-2f, 2f)); break;
                case Tactica.Adelantar: offsetDeseado = new Vector3(lado * Random.Range(2.5f, 5f), 0f, Random.Range(14f, 22f)); break;
                default: offsetDeseado = new Vector3(lado * 1.2f, 0f, -4.2f); break;
            }
        }

        void Update()
        {
            if (Muerto)
            {
                if (muertoDesde < 0f) { muertoDesde = Time.time; ExplotarGrande(); }
                TickCarcasa();
                if (Time.time - muertoDesde > 6f) Destroy(gameObject);
                return;
            }
            if (Retirado) { TickRetirada(Time.deltaTime); return; }
            if (asalto) { TickAsalto(Time.deltaTime); TickRastro(Time.deltaTime); return; }
            if (objetivo == null) return;
            float dt = Time.deltaTime;
            if (dt > 0f) velObjetivo = Vector3.Lerp(velObjetivo, (objetivo.position - posPrevObjetivo) / dt, 0.2f);
            posPrevObjetivo = objetivo.position;
            if (Time.time >= cambioDeTactica) ElegirTactica();

            var frente = objetivo.forward; frente.y = 0f; frente.Normalize();
            var lado = new Vector3(frente.z, 0f, -frente.x);
            // Zigzag lateral (mas amplio cuando esta lejos, casi nada al embestir).
            float amplitud = TacticaActual == Tactica.Embestir ? 0.4f : TacticaActual == Tactica.Flanquear ? 1.1f : 2.4f;
            float zig = Mathf.Sin(Time.time * 1.9f + faseZigzag) * amplitud;
            var meta = objetivo.position + frente * offsetDeseado.z + lado * (offsetDeseado.x + zig);
            meta.y = transform.position.y;
            var aMeta = meta - transform.position; aMeta.y = 0f;
            float d = aMeta.magnitude;

            float vObj = motorObjetivo != null ? Mathf.Abs(motorObjetivo.CurrentSpeed) : 6f;
            // Iguala al tanque y corrige la distancia: si esta lejos acelera hasta ~2x, si esta pasada frena. Al embestir, un poco mas.
            float adelante = Vector3.Dot(aMeta, frente);
            float tope = TacticaActual == Tactica.Embestir ? 2.1f : 1.9f;
            float quiere = Mathf.Clamp(vObj + adelante * 0.8f, 0f, Mathf.Min(VelocidadMaxima, vObj * tope + 5f));
            VelocidadActual = Mathf.MoveTowards(VelocidadActual, quiere, (quiere > VelocidadActual ? Aceleracion : Frenada) * dt);

            // Rumbo: hacia la meta cuando esta lejos, paralelo al tanque cuando esta cerca.
            var rumbo = d > 8f ? aMeta.normalized : frente;
            if (rumbo.sqrMagnitude > 0.01f)
            {
                var rot = Quaternion.LookRotation(rumbo, Vector3.up);
                Girar(rot, GiroMaximo(VelocidadActual), dt);   // bug #058: entra y sale de la curva redondeando
            }
            var paso = transform.forward * VelocidadActual * dt;
            // Correccion lateral suave hacia la meta para no zigzaguear mal.
            paso += (Vector3.Project(aMeta, lado)).normalized * Mathf.Min(Mathf.Abs(Vector3.Dot(aMeta, lado)), 7f) * dt * 0.55f;
            transform.position += paso;
            // Balanceo al doblar: una camioneta a 100 km/h no va en rieles.
            float giro = Vector3.SignedAngle(frente, transform.forward, Vector3.up);
            if (visualCache == null) foreach (Transform hijo in transform) if (hijo.name == "Visual") { visualCache = hijo; break; }
            var visual = visualCache;
            if (visual != null) visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, 0f, -Mathf.Clamp(giro * 0.25f, -7f, 7f)), 6f * dt);

            if (TacticaActual == Tactica.Embestir) Embestir();
            Disparar(dt);
            TickRastro(dt);
        }

        // ---- Bug #060: fin del viaje: los perseguidores abandonan ----
        // Dejan de disparar, frenan y se abren hacia un costado de la ruta: el tanque llega en calma.
        public bool Retirado { get; private set; }
        float ladoDeRetirada = 1f;
        public void Retirarse() { Retirado = true; ladoDeRetirada = Random.value < 0.5f ? -1f : 1f; }

        void TickRetirada(float dt)
        {
            if (dt <= 0f) return;
            VelocidadActual = Mathf.MoveTowards(VelocidadActual, 0f, Frenada * 0.6f * dt);
            var abrirse = Quaternion.LookRotation(Quaternion.Euler(0f, 35f * ladoDeRetirada, 0f) * transform.forward, Vector3.up);
            if (VelocidadActual > 1f) Girar(abrirse, GiroMaximo(VelocidadActual) * 0.5f, dt);
            transform.position += transform.forward * VelocidadActual * dt;
            TickRastro(dt);
        }

        // ---- ASALTO (bug #043: "una torreta fija para tomar y disparar y que vengan vehiculos que deba destruirlos con torretas") ----
        // La camioneta baja por la ruta hasta puntoDeAsalto con una curva de velocidad (acelera, crucero, frena), se planta y su
        // metralleta le tira al soldado de la escuadra mas cercano. Si la tocan mucho, retrocede unos metros y vuelve.
        [HideInInspector] public bool asalto;
        [HideInInspector] public Vector3 puntoDeAsalto;
        public float velocidadDeAsalto = 13f;
        float proximoBlanco;

        public void ConfigurarAsalto(Vector3 punto)
        {
            asalto = true;
            puntoDeAsalto = punto;
            VelocidadActual = velocidadDeAsalto * 0.6f;
        }

        void TickAsalto(float dt)
        {
            if (dt <= 0f) return;
            // Blanco: el soldado de la escuadra vivo mas cercano (se revisa 2 veces por segundo).
            if (Time.time >= proximoBlanco || objetivo == null || !objetivo.gameObject.activeInHierarchy)
            {
                proximoBlanco = Time.time + 0.5f;
                var s = SP.Core.ActorRegistry.FindNearest(transform.position, x => x.Team == TeamId.Player && x.Role != RoleType.Civilian
                    && x.Health != null && x.Health.IsAlive && x.gameObject.activeInHierarchy);
                objetivo = s != null ? s.transform : null;
                velObjetivo = Vector3.zero;
                if (objetivo != null) posPrevObjetivo = objetivo.position;
            }
            if (objetivo != null) { velObjetivo = Vector3.Lerp(velObjetivo, (objetivo.position - posPrevObjetivo) / dt, 0.2f); posPrevObjetivo = objetivo.position; }

            var aMeta = puntoDeAsalto - transform.position; aMeta.y = 0f;
            float d = aMeta.magnitude;
            // Curva de velocidad: crucero lejos, frena en los ultimos 14 m (v ~ raiz de la distancia), quieta al llegar.
            float quiere = d < 0.8f ? 0f : Mathf.Min(velocidadDeAsalto, Mathf.Sqrt(2f * 6f * Mathf.Max(0f, d - 0.6f)));
            VelocidadActual = Mathf.MoveTowards(VelocidadActual, quiere, (quiere > VelocidadActual ? 7f : 12f) * dt);
            if (d > 0.8f)
            {
                var rot = Quaternion.LookRotation(aMeta.normalized, Vector3.up);
                Girar(rot, 70f, dt);
            }
            transform.position += transform.forward * VelocidadActual * dt;
            if (objetivo != null) Disparar(dt);
        }

        // Choque contra el tanque: le saca vida al tanque, mucho menos a ella misma que al reves (es una camioneta contra un blindado).
        void Embestir()
        {
            if (Time.time < proximaEmbestida) return;
            var delta = objetivo.position - transform.position; delta.y = 0f;
            if (delta.magnitude > 4.6f) return;
            proximaEmbestida = Time.time + 1.4f;
            Embestidas++;
            if (vehiculoObjetivo != null && !vehiculoObjetivo.IsDestroyed) vehiculoObjetivo.TakeDamage(danoDeEmbestida, -2);
            Vehiculo.TakeDamage(Mathf.Max(8, Vehiculo.Health != null ? Vehiculo.Health.MaxHealth / 4 : 20), -1);
            SP.Presentation.ImpactFx.SpawnArmorSparks(Vector3.Lerp(transform.position, objetivo.position, 0.5f) + Vector3.up * 1.2f, -delta.normalized);
            SP.Presentation.AudioDirector.PlayAt(SP.Presentation.SfxKind.Explosion, transform.position, 0.45f, 1.5f);
            // Rebote lateral: se aparta para volver a tomar carrera.
            offsetDeseado.z -= 6f;
            cambioDeTactica = Mathf.Min(cambioDeTactica, Time.time + 1.2f);
        }

        void Disparar(float dt)
        {
            // Mira adelantada: el tanque va a ~17 m/s y la bala tarda; sin esto disparaba siempre detras.
            float distancia = Vector3.Distance(transform.position, objetivo.position);
            Vector3 hacia = objetivo.position + Vector3.up * 1.1f + velObjetivo * Mathf.Clamp(distancia / 75f, 0f, 0.7f);
            if (metralleta != null)
            {
                var dirM = hacia - metralleta.position; dirM.y = 0f;
                if (dirM.sqrMagnitude > 0.01f) metralleta.rotation = Quaternion.LookRotation(dirM.normalized, Vector3.up);
            }
            float dist = distancia;
            if (dist > alcance || Time.time < pausaHasta) return;
            if (Time.time > rafagaHasta)
            {
                float k = TacticaActual == Tactica.Flanquear ? 1.5f : 1f;
                rafagaHasta = Time.time + rafagaSegundos * k;
                pausaHasta = rafagaHasta + pausaSegundos * Random.Range(0.6f, 1.2f);
            }
            if (Time.time < rafagaHasta && Time.time >= proximoDisparo)
            {
                proximoDisparo = Time.time + cadencia;
                if (pool == null) pool = ProjectilePool.Activo;
                if (pool == null) return;
                var origen = boca != null ? boca.position : transform.position + Vector3.up * 1.5f;
                var dir = (hacia - origen).normalized;
                float disp = dispersionGrados * Mathf.Clamp(dist / 30f, 0.45f, 1.25f);
                dir = (dir + Random.insideUnitSphere * Mathf.Tan(disp * Mathf.Deg2Rad)).normalized;
                pool.Spawn(origen, dir, -2, TeamId.Enemy, danoPorBala, new Color(1f, 0.35f, 0.2f));
                SP.Presentation.MuzzleLightPool.Flash(origen, new Color(1f, 0.6f, 0.3f), 4f, 8f);
                EfectoDeDisparo(origen, dir);   // bug #058: fogonazo + estampido real de ametralladora
                Disparos++;
            }
        }
    }
}
