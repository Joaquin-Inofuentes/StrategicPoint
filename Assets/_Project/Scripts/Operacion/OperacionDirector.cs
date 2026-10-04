using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    public enum FaseOperacion { Infiltrar = 0, Puestos = 1, CentroDeDatos = 2, Huir = 3, Resistir = 4, Extraer = 5, Victoria = 6, Derrota = 7 }

    [Serializable]
    public class PuestoDeControl
    {
        public string nombre;
        public GameObject barrera;            // la pluma/barrera que corta la ruta
        public OperacionTerminal panel;       // solo el FLANQUEADOR lo desactiva
        public Soldier[] guardias;
        [NonSerialized] public bool abierto;
    }

    [Serializable]
    public class OleadaDeReserva
    {
        public float segundo;                 // desde que arranca la fase
        public string aviso;
        public Soldier[] soldados;
        public Transform destino;
    }

    // "Operacion Cuartel" (nivel de blockout SC_Operacion). Flujo de la mision:
    //   1. INFILTRAR   matar a todos en el cuartel militar (se abre el porton norte)
    //   2. PUESTOS     tres puestos de control sobre la ruta; cada panel solo lo desactiva el FLANQUEADOR
    //   3. DATOS       un soldado interactua 30 s con la computadora del centro de datos mientras llegan oleadas
    //   4. HUIR        subir al tanque (el jugador es el artillero: maneja SOLO el canon) y recorrer 60 s de carretera
    //                  mientras autos con metralleta lo persiguen en bucle; al final del trayecto lo destruyen
    //   5. RESISTIR    40 s a pie en una ciudad chica hasta que llegue el helicoptero (vuela hasta la plaza: no esta estacionado)
    //   6. EXTRAER     subir al helicoptero (tiene metralleta de puerta) -> fin de la mision
    public class OperacionDirector : MonoBehaviour, IEstadoGuardable
    {
        public static OperacionDirector Instancia { get; private set; }
        public static bool Activo => Instancia != null && Instancia.isActiveAndEnabled;

        [Header("1. Cuartel")]
        public Soldier[] enemigosCuartel;
        public GameObject portonCuartel;
        public Transform[] entradas;                 // un punto de partida por fase (para SaltarA y reinicios)

        [Header("2. Puestos de control")]
        public PuestoDeControl[] puestos;

        [Header("3. Centro de datos")]
        public OperacionTerminal computadora;
        public OleadaDeReserva[] oleadasDelCentro;
        public OleadaDeReserva oleadaDeHuida;        // entre el centro de datos y el tanque
        // Bug #043: ametralladoras fijas junto a la salida norte del centro de datos y camionetas enemigas que bajan por la ruta
        // mientras se hackea (hay que destruirlas con las torretas). Las torretas las pone el editor (ver ValidacionBugs/Builder).
        public GameObject[] torretasDelCentro;
        public float[] segundosDeCamionetasDelCentro = { 12f, 24f };
        public int camionetasPorTanda = 2;

        [Header("4. Huida en tanque")]
        public Vehicle tanque;
        public Transform[] rutaDelTanque;
        public GameObject autoPlantilla;
        public float segundosDeTrayecto = 60f;
        public int autosSimultaneos = 3;

        [Header("5. Ciudad y helicoptero")]
        public OleadaDeReserva[] oleadasDeLaCiudad;
        public float segundosDeResistencia = 40f;
        public Transform plaza;
        public Transform heli;
        public Transform heliInicio;
        public Transform heliAterrizaje;
        // Bug #063: la ciudad ampliada llega a ~105 m de la plaza (y el tanque frena en la entrada, a ~93 m): antes 85.
        public float radioDeLaCiudad = 105f;

        public FaseOperacion Fase { get; private set; } = FaseOperacion.Infiltrar;
        public int PuestoActual { get; private set; }
        public float Reloj { get; private set; }               // segundos dentro de la fase actual
        public bool EnTanque { get; private set; }
        public int AutosDestruidos { get; private set; }
        public int AutosCreados { get; private set; }
        public float TrayectoRestante => EnTanque ? Mathf.Max(0f, segundosDeTrayecto - Reloj) : segundosDeTrayecto;
        public float ResistenciaRestante => Mathf.Max(0f, segundosDeResistencia - Reloj);
        public bool HeliAterrizo { get; private set; }
        public bool SubiendoAlHeli { get; private set; }
        public Helicoptero ScriptHeli { get; private set; }
        public string Motivo { get; private set; } = "";

        OperacionHud hud;
        TutorialBeacon baliza;
        float hackInicio = -1f;
        readonly HashSet<int> oleadasLanzadas = new HashSet<int>();
        readonly List<OperacionAuto> autos = new List<OperacionAuto>();
        int indiceRuta;
        float proximoAuto;
        float velocidadOriginal = -1f;
        bool oleadaHuidaLanzada;

        // ---------------------------------------------------------------
        void Awake()
        {
            Instancia = this;
            AnuncioDeZonas.Desactivado = true;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            SonidosDeOperacion.DetenerAmbiente();
            AnuncioDeZonas.Desactivado = false;
            if (baliza != null) baliza.Quitar();
        }

        void Start()
        {
            // El marcador de aparicion/rondas de los reservistas queda apagado hasta su oleada.
            foreach (var p in puestos) if (p.panel != null) p.panel.Habilitado = true;
            // El prefab P_Soldier_Enemy trae rol Medic: todos los enemigos del nivel figuraban (y se comportaban) como medicos.
            foreach (var s in FindObjectsByType<Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.Team == TeamId.Enemy && s.Role != RoleType.Enemy) s.SetRole(RoleType.Enemy);
            if (heli != null)
            {
                ScriptHeli = heli.GetComponent<Helicoptero>();
                heli.gameObject.SetActive(false);
            }
            // Pedido: "quita el sonido de fondo de las olas que molesta" (era el viento sintetico del desierto, sonaba a mar): ya no
            // se enciende. SonidosDeOperacion.IniciarAmbiente queda disponible por si se quiere otro ambiente.
            CazaDeEnemigos.Reiniciar();
            if (torretasDelCentro != null) foreach (var t in torretasDelCentro) if (t != null) SP.Vehicles.TorretaFija.Instalar(t);
            PrepararRefuerzosDeLaCiudad();
            ArmarCoberturasDeLaPlaza();
            SP.Presentation.PrecalentadoDeCombate.Ejecutar();
            EntrarFase(FaseOperacion.Infiltrar);
        }

        // ---- Bug #061: coberturas en la plaza ----
        // "Aqui deberian haber coberturas para cubrirse y revisa que los aliados y enemigos usen coberturas". La plaza era un
        // cuadrado abierto. Se arma (al cargar, no en plena pelea) un anillo de barricadas alrededor del helipuerto mirando
        // hacia afuera, para la escuadra, y otras en las calles de entrada mirando hacia la plaza, para los que llegan. La IA de
        // los dos bandos ya elige coberturas sola (CoberturasPuntuadas): lo que faltaba eran coberturas que elegir.
        public const float RadioAnilloDeLaPlaza = 12.5f, LibreAlrededorDelHeli = 8.5f;
        public int CoberturasDeLaPlaza { get; private set; }

        void ArmarCoberturasDeLaPlaza()
        {
            if (plaza == null || !Application.isPlaying) return;
            var c = new Vector3(plaza.position.x, 0f, plaza.position.z);
            var heliPos = heliAterrizaje != null ? new Vector3(heliAterrizaje.position.x, 0f, heliAterrizaje.position.z) : c;
            var lugares = new List<(Vector3, Vector3)>();
            for (int i = 0; i < 10; i++)
            {
                float a = (i * 36f + 18f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = c + dir * RadioAnilloDeLaPlaza;
                if (Plano(p, heliPos).magnitude < LibreAlrededorDelHeli) continue;
                lugares.Add((p, dir));
            }
            // Calles de entrada: a mitad de camino entre cada entrada y la plaza, corridas de costado (no tapan la calle entera).
            var vistas = new List<Vector3>();
            foreach (var e in entradasDeLaCiudad)
            {
                var ep = new Vector3(e.x, 0f, e.z);
                if (vistas.Exists(v => Plano(v, ep).magnitude < 10f)) continue;
                vistas.Add(ep);
                var haciaPlaza = (c - ep); haciaPlaza.y = 0f;
                float d = haciaPlaza.magnitude; if (d < 20f) continue;
                haciaPlaza /= d;
                var lado = Vector3.Cross(Vector3.up, haciaPlaza);
                lugares.Add((ep + haciaPlaza * (d * 0.45f) + lado * 3.2f, haciaPlaza));
                lugares.Add((ep + haciaPlaza * (d * 0.62f) - lado * 3.2f, haciaPlaza));
            }
            // Medido despues del #061: los enemigos pelean desde las 4 calles de la cruz, a 20-40 m de la plaza, y ahi no habia
            // nada detras de que cubrirse (solo paredes paralelas a la linea de tiro). Barricadas a media distancia, alternando
            // de vereda; cada una mira hacia la plaza.
            var ejes = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var eje in ejes)
            {
                var lado = Vector3.Cross(Vector3.up, eje);
                int k = 0;
                foreach (float dist in new[] { 21f, 31f, 42f })
                    lugares.Add((c + eje * dist + lado * ((k++ & 1) == 0 ? 3.6f : -3.6f), -eje));
            }
            CoberturasDeLaPlaza = Atrincherar.ArmarCoberturas(lugares);
        }

        // ---- Bug #049 / #050: refuerzos SECUENCIALES en la ciudad ----
        // "No me aparecen los enemigos reales, deberia haber mas y aparecer de manera secuencial" / "mas frenetismo, mas
        // enemigos". Las 3 oleadas de reserva siguen (con su aviso), y ademas entra UN enemigo cada ~2 s por alguna de las
        // entradas de la ciudad (los puntos donde esperaban los reservistas), hasta un tope de vivos para no saturar.
        [Header("Refuerzos secuenciales de la ciudad")]
        public float segundosEntreRefuerzos = 2.1f;
        public int refuerzosVivosMaximo = 16;
        public int refuerzosTotalesMaximo = 26;
        readonly List<Vector3> entradasDeLaCiudad = new List<Vector3>();
        GameObject plantillaDeRefuerzo;
        float proximoRefuerzo;
        int indiceEntrada;
        readonly List<Soldier> refuerzosCiudad = new List<Soldier>();
        public int RefuerzosCreados { get; private set; }
        public int RefuerzosVivos { get { int n = 0; foreach (var r in refuerzosCiudad) if (r != null && r.Health != null && r.Health.IsAlive) n++; return n; } }

        void PrepararRefuerzosDeLaCiudad()
        {
            entradasDeLaCiudad.Clear();
            if (oleadasDeLaCiudad == null) return;
            foreach (var o in oleadasDeLaCiudad)
            {
                if (o == null || o.soldados == null) continue;
                foreach (var r in o.soldados)
                {
                    if (r == null) continue;
                    if (plantillaDeRefuerzo == null && !r.gameObject.activeSelf)
                    {
                        // Copia intacta tomada al arrancar (inactiva: no corre Awake ni se registra). Clonar al reservista en
                        // plena fase copiaria su estado de ese momento (muerto, caido, a mitad de animacion).
                        plantillaDeRefuerzo = Instantiate(r.gameObject, r.transform.parent);
                        plantillaDeRefuerzo.name = "PlantillaDeRefuerzo_Ciudad";
                    }
                    entradasDeLaCiudad.Add(r.transform.position);
                }
            }
        }

        void TickRefuerzosDeLaCiudad()
        {
            if (plantillaDeRefuerzo == null || entradasDeLaCiudad.Count == 0 || plaza == null) return;
            // Ni en los primeros segundos (todavia se esta llegando) ni en los ultimos (el helicoptero ya baja).
            if (Reloj < 3f || ResistenciaRestante < 5f || ciudadPausada) return;
            if (Reloj < proximoRefuerzo || RefuerzosCreados >= refuerzosTotalesMaximo || RefuerzosVivos >= refuerzosVivosMaximo) return;
            proximoRefuerzo = Reloj + segundosEntreRefuerzos * UnityEngine.Random.Range(0.75f, 1.25f);

            // Entradas en orden rotado (salteando una al azar a veces) para que lleguen de todos lados, no siempre del mismo.
            // Bug #063: tambien por las calles nuevas del anillo (no solo por los 3 extremos de la cruz).
            var aparicion = AparicionesDeLaCiudad();
            indiceEntrada = (indiceEntrada + 1 + UnityEngine.Random.Range(0, 3)) % aparicion.Count;
            var pos = aparicion[indiceEntrada] + new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;

            var go = Instantiate(plantillaDeRefuerzo, pos, Quaternion.LookRotation(Plano(plaza.position, pos).normalized, Vector3.up), plantillaDeRefuerzo.transform.parent);
            go.name = "Refuerzo_Ciudad_" + (++RefuerzosCreados);
            go.SetActive(true);
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return; }
            refuerzosCiudad.Add(s);
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null)
            {
                s.Brain.ReactivarNavegacion();
                var meta = plaza.position + new Vector3(UnityEngine.Random.Range(-6f, 6f), 0f, UnityEngine.Random.Range(-6f, 6f));
                s.Brain.SetPatrolRoute(new[] { meta, plaza.position });
            }
            CazaDeEnemigos.Marcar(new[] { s });
        }

        // ---- Bug #063: oleadas de la ciudad escalonadas y dispersas ----
        // "Que tenga mas logica que vengan ... que no sean de grupos asi sino mas dispersos y con temporizadores". Cada oleada
        // salia entera de golpe (5-6 soldados amontonados en el mismo punto). Ahora, cuando le toca a una oleada, sus soldados
        // entran DE A UNO cada 1-2,5 s, cada uno por una boca distinta de las calles de ese lado de la ciudad, y van hacia un
        // punto distinto alrededor de la plaza. El HUD muestra cuanto falta para la proxima oleada.
        // Bocas de entrada relativas a la plaza: el anillo de calles nuevo (norte, sur, este, oeste) que arma AmpliarCiudad.
        static readonly Vector2[] BocasDeLaCiudad =
        {
            new Vector2(-52f, 68f), new Vector2(-22f, 68f), new Vector2(23f, 68f), new Vector2(50f, 68f),
            new Vector2(-52f, -66f), new Vector2(-22f, -66f), new Vector2(23f, -66f), new Vector2(50f, -66f),
            new Vector2(58f, -48f), new Vector2(58f, -23f), new Vector2(58f, 22f), new Vector2(58f, 47f),
            new Vector2(-56f, -48f), new Vector2(-56f, -28f), new Vector2(-56f, 27f), new Vector2(-56f, 52f),
        };
        readonly List<(Soldier s, float t)> pendientesCiudad = new List<(Soldier, float)>();
        List<Vector3> aparicionesCiudad;
        public int PendientesDeLaCiudad => pendientesCiudad.Count;
        public int LiberadosEscalonados { get; private set; }

        // Segundos hasta la proxima oleada de la ciudad (-1 si ya salieron todas).
        public float ProximaOleadaDeLaCiudadEn
        {
            get
            {
                float mejor = -1f;
                if (oleadasDeLaCiudad == null) return mejor;
                for (int i = 0; i < oleadasDeLaCiudad.Length; i++)
                {
                    if (lanzadasCiudad.Contains(i)) continue;
                    float f = oleadasDeLaCiudad[i].segundo - Reloj;
                    if (mejor < 0f || f < mejor) mejor = Mathf.Max(0f, f);
                }
                return mejor;
            }
        }

        List<Vector3> bocasCiudad;

        // Las bocas del anillo de calles, sobre el NavMesh.
        List<Vector3> BocasEnElNavMesh()
        {
            if (bocasCiudad != null) return bocasCiudad;
            bocasCiudad = new List<Vector3>();
            if (plaza != null)
                foreach (var b in BocasDeLaCiudad)
                {
                    var p = plaza.position + new Vector3(b.x, 0f, b.y);
                    if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 5f, UnityEngine.AI.NavMesh.AllAreas)) bocasCiudad.Add(h.position);
                }
            return bocasCiudad;
        }

        // Refuerzos sueltos: por las entradas de siempre y por las bocas nuevas.
        List<Vector3> AparicionesDeLaCiudad()
        {
            if (aparicionesCiudad != null) return aparicionesCiudad;
            aparicionesCiudad = new List<Vector3>(entradasDeLaCiudad);
            aparicionesCiudad.AddRange(BocasEnElNavMesh());
            return aparicionesCiudad;
        }

        void ActivarEscalonada(OleadaDeReserva o)
        {
            if (o == null || o.soldados == null) return;
            if (hud != null && !string.IsNullOrEmpty(o.aviso)) hud.Aviso(o.aviso, 2.5f);
            else if (!string.IsNullOrEmpty(o.aviso)) AlertQueue.Push(o.aviso, AlertPriority.Alta, 3f);
            float t = Reloj;
            foreach (var s in o.soldados)
            {
                if (s == null) continue;
                pendientesCiudad.Add((s, t));
                t += UnityEngine.Random.Range(1f, 2.5f);
            }
        }

        void TickPendientesDeLaCiudad()
        {
            for (int i = pendientesCiudad.Count - 1; i >= 0; i--)
            {
                var (s, t) = pendientesCiudad[i];
                if (Reloj < t) continue;
                pendientesCiudad.RemoveAt(i);
                if (s != null) LiberarEnLaCiudad(s);
            }
        }

        void LiberarEnLaCiudad(Soldier s)
        {
            // Por una de las 4 bocas mas cercanas a donde esperaba (respeta "por la calle este", etc.), al azar.
            var origen = s.transform.position;
            var bocas = new List<Vector3>(BocasEnElNavMesh());
            bocas.Sort((a, b) => Plano(a, origen).sqrMagnitude.CompareTo(Plano(b, origen).sqrMagnitude));
            var pos = bocas.Count > 0 ? bocas[UnityEngine.Random.Range(0, Mathf.Min(4, bocas.Count))] : origen;
            pos += new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0f, UnityEngine.Random.Range(-2.5f, 2.5f));
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
            s.transform.position = new Vector3(pos.x, origen.y, pos.z);
            if (plaza != null) s.transform.rotation = Quaternion.LookRotation(Plano(plaza.position, pos).normalized, Vector3.up);
            s.gameObject.SetActive(true);
            ApoyoEnElPiso.Apoyar(s.transform);
            CazaDeEnemigos.Marcar(new[] { s });
            if (s.Brain != null && plaza != null)
            {
                s.Brain.ReactivarNavegacion();
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f), r = UnityEngine.Random.Range(14f, 24f);
                var meta = plaza.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                s.Brain.SetPatrolRoute(new[] { meta, plaza.position });
            }
            LiberadosEscalonados++;
        }

        // Fin de un objetivo: el "flash" de un tajo (como cortar un zapallo) + campana de ping.
        static void SonarObjetivoCumplido()
        {
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.ObjetivoCumplido), 0.85f * AudioDirector.GainFor(SfxChannel.Sfx), 1f, "ObjetivoCumplido");
        }

        static List<Soldier> Escuadra(bool soloVivos = true)
        {
            var r = new List<Soldier>();
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null) continue;
                if (soloVivos && !s.Health.IsAlive) continue;
                if (!s.gameObject.activeInHierarchy) continue;
                r.Add(s);
            }
            return r;
        }

        static Soldier Poseido()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        static Vector3 Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return a - b; }

        // ---------------------------------------------------------------
        // Entradas de fase
        // ---------------------------------------------------------------
        public void EntrarFase(FaseOperacion f)
        {
            bool terminoUnObjetivo = Application.isPlaying && f != FaseOperacion.Infiltrar && f != Fase;
            Fase = f;
            Reloj = 0f;
            if (terminoUnObjetivo) SonarObjetivoCumplido();
            SesionLog.Evento("FASE de la Operacion -> " + f + (f == FaseOperacion.Puestos ? " (puesto " + (PuestoActual + 1) + ")" : ""));
            if (hud == null) hud = OperacionHud.Crear();
            switch (f)
            {
                case FaseOperacion.Infiltrar:
                    PonerBaliza("SALIDA NORTE", new Color(1f, 0.82f, 0.3f), portonCuartel != null ? portonCuartel.transform.position : transform.position, null);
                    Aviso("OPERACION CUARTEL\nINFILTRATE Y ELIMINA A TODOS");
                    if (Application.isPlaying && !cuartelAtrincherado) { cuartelAtrincherado = true; AtrincherarGuardias(enemigosCuartel); }
                    break;
                case FaseOperacion.Puestos:
                    PuestoActual = Mathf.Clamp(PuestoActual, 0, puestos.Length - 1);
                    BalizaDelPuesto();
                    Aviso("RUTA ABIERTA · 3 PUESTOS DE CONTROL\nSOLO EL FLANQUEADOR LOS DESACTIVA");
                    if (Application.isPlaying && !puestosAtrincherados)
                    {
                        puestosAtrincherados = true;
                        var guardias = new List<Soldier>();
                        foreach (var pu in puestos) if (pu != null && pu.guardias != null) guardias.AddRange(pu.guardias);
                        AtrincherarGuardias(guardias);
                    }
                    break;
                case FaseOperacion.CentroDeDatos:
                    PonerBaliza("COMPUTADORA", new Color(0.35f, 0.8f, 1f), computadora.transform.position, null, 2.4f);
                    Aviso("CENTRO DE DATOS\nUN SOLDADO INTERACTUA 30 s · LOS DEMAS DEFIENDEN");
                    break;
                case FaseOperacion.Huir:
                    EnTanque = false;
                    if (!oleadaHuidaLanzada) { oleadaHuidaLanzada = true; Activar(oleadaDeHuida, oleadaDeHuida != null && oleadaDeHuida.destino != null ? oleadaDeHuida.destino.position : tanque.transform.position); }
                    PonerBaliza("TANQUE", new Color(0.4f, 1f, 0.5f), tanque.transform.position, tanque.transform, 2.6f);
                    Aviso("DATOS DESCARGADOS\nABRETE PASO Y SUBE AL TANQUE");
                    break;
                case FaseOperacion.Resistir:
                    PonerBaliza("PLAZA", new Color(1f, 0.55f, 0.25f), plaza.position, null, 3f);
                    Aviso("TANQUE DESTRUIDO\nRESISTE " + Mathf.RoundToInt(segundosDeResistencia) + " s: EL HELICOPTERO ESTA EN CAMINO");
                    ArrancarHeli();
                    break;
                case FaseOperacion.Extraer:
                    PonerBaliza("HELICOPTERO", new Color(0.35f, 1f, 0.5f), heli.position, heli, 3.6f);
                    Aviso("EL HELICOPTERO ATERRIZO\nSUBE CON [E]");
                    break;
                case FaseOperacion.Victoria:
                    if (baliza != null) { baliza.Quitar(); baliza = null; }
                    break;
            }
        }

        // Bug #042: los guardias arrancan detras de coberturas (o de una barricada) mirando hacia donde viene la escuadra.
        bool cuartelAtrincherado, puestosAtrincherados;
        void AtrincherarGuardias(IEnumerable<Soldier> guardias)
        {
            var yo = Poseido();
            var amenaza = yo != null ? yo.transform.position : (entradas != null && entradas.Length > 0 && entradas[0] != null ? entradas[0].position : transform.position);
            Atrincherar.Aplicar(guardias, amenaza);
        }

        void Aviso(string t) { if (hud != null) hud.Aviso(t, 4f); GameLog.Line("[Operacion] " + t.Replace("\n", " · ")); }

        void PonerBaliza(string texto, Color c, Vector3 pos, Transform sigue, float radio = 3f)
        {
            if (baliza != null) baliza.Quitar();
            baliza = TutorialBeacon.Crear(texto, c, new Vector3(pos.x, 0.15f, pos.z), sigue, radio, 26f);
        }

        void BalizaDelPuesto()
        {
            var p = puestos[PuestoActual];
            PonerBaliza("PUESTO " + (PuestoActual + 1) + "/" + puestos.Length, new Color(1f, 0.82f, 0.3f), p.panel.transform.position, null, 2.6f);
        }

        // ---------------------------------------------------------------
        // Frame
        // ---------------------------------------------------------------
        void Update()
        {
            if (hud == null) { hud = OperacionHud.Crear(); }
            float dt = Time.deltaTime;
            Reloj += dt;
            if (Fase != FaseOperacion.Victoria && Fase != FaseOperacion.Derrota && !SubiendoAlHeli) CazaDeEnemigos.Tick(dt);
            switch (Fase)
            {
                case FaseOperacion.Infiltrar: TickInfiltrar(); break;
                case FaseOperacion.Puestos: TickPuestos(); break;
                case FaseOperacion.CentroDeDatos: TickCentro(); break;
                case FaseOperacion.Huir: TickHuir(dt); break;
                case FaseOperacion.Resistir: TickResistir(dt); break;
                case FaseOperacion.Extraer: TickExtraer(); break;
            }
            if (hud != null && (Fase == FaseOperacion.Infiltrar || Fase == FaseOperacion.Puestos || Fase == FaseOperacion.CentroDeDatos)) hud.OcultarTimer();
        }

        static int Vivos(Soldier[] lista)
        {
            int n = 0;
            if (lista == null) return 0;
            foreach (var s in lista) if (s != null && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive) n++;
            return n;
        }

        float DistanciaDelLider(Vector3 p)
        {
            var yo = Poseido();
            if (yo == null) return 0f;
            return Plano(yo.transform.position, p).magnitude;
        }

        // ---- 1. cuartel ----
        void TickInfiltrar()
        {
            int vivos = Vivos(enemigosCuartel);
            if (hud != null) hud.Objetivo("OBJETIVO 1/6 · INFILTRAR EL CUARTEL", vivos > 0 ? $"Elimina a TODOS los soldados del cuartel · quedan {vivos}" : "Cuartel limpio: el porton norte se abre", 1f - (float)vivos / Mathf.Max(1, enemigosCuartel.Length), new Color(1f, 0.82f, 0.3f));
            if (vivos == 0) { AbrirPorton(); PuestoActual = 0; EntrarFase(FaseOperacion.Puestos); }
        }

        void AbrirPorton()
        {
            if (portonCuartel != null && portonCuartel.activeSelf) StartCoroutine(Hundir(portonCuartel));
        }

        // La barrera baja al piso (no se rompe: es un puesto, no una pared) y se avisa a la navegacion.
        IEnumerator Hundir(GameObject cubo)
        {
            var t = cubo.transform;
            var inicio = t.position;
            var alto = t.lossyScale.y;
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / 1.1f;
                t.position = inicio + Vector3.down * (alto + 0.2f) * Mathf.SmoothStep(0f, 1f, k);
                yield return null;
            }
            cubo.SetActive(false);
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            Coberturas.Registrar();
        }

        // ---- 2. puestos ----
        void TickPuestos()
        {
            var p = puestos[PuestoActual];
            int guardias = Vivos(p.guardias);
            var rol = p.panel != null ? p.panel.nombreDelRol : "FLANQUEADOR";
            if (hud != null)
                hud.Objetivo($"OBJETIVO 2/6 · PUESTO DE CONTROL {PuestoActual + 1}/{puestos.Length}",
                    $"Solo el {rol} puede desactivar el panel · mantene [E] · guardias vivos: {guardias}",
                    (PuestoActual + p.panel.Progreso01) / puestos.Length, new Color(1f, 0.82f, 0.3f));
            if (p.panel.Completo && !p.abierto) AbrirPuesto(p);
        }

        void AbrirPuesto(PuestoDeControl p)
        {
            p.abierto = true;
            if (p.barrera != null) StartCoroutine(Hundir(p.barrera));
            AlertQueue.Push($"{p.nombre} DESACTIVADO", AlertPriority.Alta, 2.5f);
            if (PuestoActual + 1 < puestos.Length) SonarObjetivoCumplido();   // el ultimo ya suena por el cambio de fase
            PuestoActual++;
            if (PuestoActual >= puestos.Length) EntrarFase(FaseOperacion.CentroDeDatos);
            else BalizaDelPuesto();
        }

        // ---- 3. centro de datos ----
        void TickCentro()
        {
            if (computadora.Operador != null && hackInicio < 0f) hackInicio = Time.time;
            if (hackInicio >= 0f)
            {
                float t = Time.time - hackInicio;
                for (int i = 0; i < oleadasDelCentro.Length; i++)
                {
                    if (oleadasLanzadas.Contains(i) || t < oleadasDelCentro[i].segundo) continue;
                    oleadasLanzadas.Add(i);
                    Activar(oleadasDelCentro[i], computadora.transform.position);
                }
                for (int i = 0; segundosDeCamionetasDelCentro != null && i < segundosDeCamionetasDelCentro.Length; i++)
                {
                    if (tandasDeCamionetas.Contains(i) || t < segundosDeCamionetasDelCentro[i]) continue;
                    tandasDeCamionetas.Add(i);
                    LanzarCamionetasDelCentro();
                }
            }
            if (hud != null)
            {
                string det = computadora.Operador != null
                    ? $"{computadora.Operador.DisplayName} esta hackeando · {Mathf.CeilToInt((1f - computadora.Progreso01) * computadora.duracion)} s · oleadas {oleadasLanzadas.Count}/{oleadasDelCentro.Length}"
                      + (CamionetasDelCentroVivas > 0 ? $" · ¡{CamionetasDelCentroVivas} vehiculos! usa la ametralladora" : "")
                    : "Entra al centro de datos y apreta [E] en la computadora";
                hud.Objetivo("OBJETIVO 3/6 · CENTRO DE DATOS", det, computadora.Progreso01, new Color(0.35f, 0.8f, 1f));
                if (computadora.Operador != null) hud.Timer("DESCARGANDO DATOS", Mathf.CeilToInt((1f - computadora.Progreso01) * computadora.duracion), new Color(0.5f, 0.9f, 1f));
                else hud.OcultarTimer();
            }
            if (computadora.Completo) { if (hud != null) hud.OcultarTimer(); EntrarFase(FaseOperacion.Huir); }
        }

        // ---- Bug #043: camionetas de asalto contra el centro de datos ----
        readonly HashSet<int> tandasDeCamionetas = new HashSet<int>();
        readonly List<OperacionAuto> camionetasDelCentro = new List<OperacionAuto>();
        public int CamionetasDelCentroCreadas { get; private set; }
        public int CamionetasDelCentroVivas { get { int n = 0; foreach (var a in camionetasDelCentro) if (a != null && !a.Muerto) n++; return n; } }

        // Bug #047: "la mitad de tamaño los enemigos": las camionetas eran mas grandes que el propio tanque (5,6 m de largo
        // contra 3,6). Se instancian a escala 0,5 y se apoyan en el piso (el pivote de la plantilla queda a 0,6 m de altura,
        // que a mitad de escala dejaba las ruedas flotando).
        public const float EscalaDeCamioneta = 0.5f;

        GameObject InstanciarCamioneta(Vector3 pos, Quaternion rot)
        {
            var go = Instantiate(autoPlantilla, pos, rot);
            go.transform.localScale = autoPlantilla.transform.localScale * EscalaDeCamioneta;
            go.SetActive(true);   // antes de medir: un renderer inactivo devuelve una caja vacia
            bool hay = false; var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            if (hay) { var p = go.transform.position; p.y -= b.min.y; go.transform.position = p; }
            return go;
        }

        void LanzarCamionetasDelCentro()
        {
            if (autoPlantilla == null || computadora == null) return;
            var c = computadora.transform.position;
            AlertQueue.Push("¡VEHICULOS ENEMIGOS POR LA RUTA NORTE! USA LAS AMETRALLADORAS FIJAS", AlertPriority.Alta, 3.5f);
            if (hud != null) hud.Aviso("¡VEHICULOS ENEMIGOS!\nTOMA UNA AMETRALLADORA FIJA [E]", 3f);
            for (int k = 0; k < camionetasPorTanda; k++)
            {
                float lado = (k % 2 == 0 ? -1f : 1f) * (2.2f + 1.5f * (k / 2));
                var salida = new Vector3(c.x + lado, 0.6f, c.z + 70f + k * 9f);      // patio del tanque, al norte
                // ~25 m al norte de la computadora; cada tanda frena mas atras y mas abierta (antes la segunda se encimaba a la primera).
                int tanda = tandasDeCamionetas.Count - 1;
                var parada = new Vector3(c.x + lado * (1.6f + 1.4f * tanda), 0.6f, c.z + 26f + k * 3f + tanda * 9f);
                var go = InstanciarCamioneta(salida, Quaternion.LookRotation(Vector3.back, Vector3.up));
                go.name = "Camioneta_Asalto_" + (++CamionetasDelCentroCreadas);
                var a = go.GetComponent<OperacionAuto>();
                a.ConfigurarAsalto(parada);
                camionetasDelCentro.Add(a);
            }
            GameLog.Line($"[Operacion] Camionetas de asalto contra el centro de datos: {camionetasPorTanda}");
        }

        void Activar(OleadaDeReserva o, Vector3 destino)
        {
            if (o == null || o.soldados == null) return;
            // Antes salia dos veces a la vez (cartel grande + caja de alertas): un solo cartel.
            if (hud != null && !string.IsNullOrEmpty(o.aviso)) hud.Aviso(o.aviso, 2.5f);
            else if (!string.IsNullOrEmpty(o.aviso)) AlertQueue.Push(o.aviso, AlertPriority.Alta, 3f);
            var meta = o.destino != null ? o.destino.position : destino;
            CazaDeEnemigos.Marcar(o.soldados);   // bugs #20/#21: la oleada sale a CAZAR a la escuadra, no a patrullar un punto fijo
            foreach (var s in o.soldados)
            {
                if (s == null) continue;
                s.gameObject.SetActive(true);
                ApoyoEnElPiso.Apoyar(s.transform);
                if (s.Brain != null)
                {
                    s.Brain.ReactivarNavegacion();
                    var jitter = new Vector3(UnityEngine.Random.Range(-4f, 4f), 0f, UnityEngine.Random.Range(-4f, 4f));
                    s.Brain.SetPatrolRoute(new[] { meta + jitter, meta - jitter });
                }
            }
        }

        // ---- 4. huida en tanque ----
        void TickHuir(float dt)
        {
            if (!EnTanque) { TickAntesDelTanque(); return; }
            TickTrayecto(dt);
        }

        void TickAntesDelTanque()
        {
            var yo = Poseido();
            float d = yo != null ? Plano(yo.transform.position, tanque.transform.position).magnitude : 999f;
            if (hud != null) hud.Objetivo("OBJETIVO 4/6 · HUIR EN EL TANQUE", $"Abrete paso hasta el tanque · {Mathf.RoundToInt(d)} m · el tanque avanza solo: vos manejas el CANON", 0f, new Color(0.4f, 1f, 0.5f));
            if (d <= 6.5f && yo != null && yo.Health.IsAlive)
            {
                if (hud != null) hud.Prompt("[E] SUBIR AL TANQUE (VAS DE ARTILLERO: SOLO EL CANON)");
                if (OperacionTerminal.EToque()) Embarcar();
            }
            else if (yo != null && yo.Health.IsAlive && hud != null)
            {
                string pista = PistaDeLanzacohetes(yo);
                if (pista != null) hud.Prompt(pista);
            }
        }

        // Bug #055: "que aqui haya un cartel que diga use la tecla 3 para usar el lanzacohetes contra los vehiculos". Mientras
        // haya camionetas enemigas a la vista (a menos de 160 m) se indica la tecla del lanzacohetes del soldado poseido, o
        // quien lo lleva si el poseido no tiene. Con el lanzacohetes ya en la mano no hace falta repetirlo.
        public const float DistanciaPistaLanzacohetes = 160f;
        public string PistaDeLanzacohetes(Soldier yo)
        {
            bool hayVehiculo = false;
            foreach (var v in SP.Core.WorldSystemsRegistry.Vehicles)
                if (v != null && v.isActiveAndEnabled && !v.IsDestroyed && v.Bando == TeamId.Enemy && Plano(v.transform.position, yo.transform.position).magnitude < DistanciaPistaLanzacohetes) { hayVehiculo = true; break; }
            if (!hayVehiculo || yo.Weapon == null) return null;
            int slot = yo.Weapon.Loadout.IndexOf(WeaponKind.Rocket);
            if (slot >= 0)
                return yo.Weapon.CurrentWeaponKind == WeaponKind.Rocket ? null : $"VEHICULOS ENEMIGOS · TECLA [{slot + 1}] = LANZACOHETES: UN COHETE LOS DESTRUYE";
            foreach (var s in Escuadra())
                if (s != yo && s.Weapon != null && s.Weapon.Loadout.Contains(WeaponKind.Rocket))
                    return $"VEHICULOS ENEMIGOS · EL {s.ClassName} ({s.DisplayName}) TIENE LANZACOHETES: POSEELO Y USA LA TECLA [{s.Weapon.Loadout.IndexOf(WeaponKind.Rocket) + 1}]";
            return null;
        }

        System.Collections.IEnumerator PonerDeArtillero(PlayerInputDriver driver, Soldier yo)
        {
            for (int i = 0; i < 120 && tanque.IsMountAnimating(yo); i++) yield return null;
            for (int intento = 0; intento < 5 && driver.CurrentSeat != VehicleSeatRole.Gunner; intento++)
            {
                driver.SwitchSeat(VehicleSeatRole.Gunner);
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Sube a la escuadra: un aliado maneja, el resto de pasajeros, y el soldado poseido queda de artillero.
        public void Embarcar()
        {
            if (EnTanque) return;
            var driver = PlayerInputDriver.Activo;
            var yo = Poseido();
            if (driver == null || yo == null) return;

            var otros = Escuadra();
            otros.Remove(yo);
            Soldier conductor = null;
            foreach (var s in otros) if (s.Role == RoleType.Flanker) { conductor = s; break; }
            if (conductor == null && otros.Count > 0) conductor = otros[0];
            if (conductor != null)
            {
                otros.Remove(conductor);
                tanque.Mount(conductor, VehicleSeatRole.Driver, true);
            }
            var libres = new[] { VehicleSeatRole.Passenger1, VehicleSeatRole.Passenger2 };
            for (int i = 0; i < otros.Count && i < libres.Length; i++) tanque.Mount(otros[i], libres[i], true);

            driver.Vehicle = tanque;   // el driver trabaja sobre "su" vehiculo (campo serializado en la escena)
            driver.EnterVehicle(tanque);
            // El jugador maneja solo el canon: asiento de artillero. SwitchSeat se rechaza mientras dura la animacion de subida,
            // asi que se pide en cuanto termina.
            StartCoroutine(PonerDeArtillero(driver, yo));

            EnTanque = true;
            Reloj = 0f;
            indiceRuta = 0;
            proximoAuto = 2.5f;
            // Velocidad del tanque tal que la ruta entera dure ~el trayecto (el motor es [SerializeField] privado).
            var motor = tanque.GetComponent<VehicleMotor>();
            if (motor != null)
            {
                var f = typeof(VehicleMotor).GetField("maxSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null)
                {
                    if (velocidadOriginal < 0f) velocidadOriginal = (float)f.GetValue(motor);
                    // Ruta del doble de largo (~870 m) recorrida en ~52 s: ~17 m/s, 2,4 veces la marcha de antes (7 m/s). Mas aceleracion
                    // para que arranque y salga de cada curva con empuje en vez de "flotar" hasta la velocidad final.
                    f.SetValue(motor, LargoDeLaRuta() / (segundosDeTrayecto * 0.97f));
                    var fa = typeof(VehicleMotor).GetField("acceleration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fa != null) fa.SetValue(motor, 16f);
                }
            }
            PotenciarCanon();
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            Aviso("A BORDO · TRAYECTO DE " + Mathf.RoundToInt(segundosDeTrayecto) + " s\nDESTRUI A LOS AUTOS CON EL CANON");
            EmitirOrdenDeRuta();
        }

        // Pedido: "mas poder a los ataques e impactos de tanques: lo que manejas es un tanque, los enemigos manejan camionetas". El obus
        // pega 3 veces mas fuerte, la explosion cubre casi el doble de radio y el cañon pesa mas (recarga de ~1,1 s en vez de 0,5 s).
        public const int DanoDelCanon = 135;
        public const float RadioDelCanon = 5.5f;
        public const float RecargaDelCanon = 1.1f;

        void PotenciarCanon()
        {
            var t = tanque != null ? tanque.TorretaCanon : null;
            if (t == null) return;
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(TurretWeapon).GetField("damage", bf)?.SetValue(t, DanoDelCanon);
            typeof(TurretWeapon).GetField("explosionRadius", bf)?.SetValue(t, RadioDelCanon);
            typeof(TurretWeapon).GetField("fireCooldown", bf)?.SetValue(t, RecargaDelCanon);
            // Bug #047: el artillero apunta al suelo cerca del tanque: el tubo necesita bajar mas que los 10° de antes.
            t.FijarLimitesDeElevacion(25f, 15f);
        }

        float LargoDeLaRuta()
        {
            float l = 0f;
            var prev = tanque.transform.position;
            foreach (var t in rutaDelTanque) { l += Plano(t.position, prev).magnitude; prev = t.position; }
            return Mathf.Max(50f, l);
        }

        void EmitirOrdenDeRuta()
        {
            var vb = tanque.GetComponent<VehicleBrain>();
            if (vb == null || indiceRuta >= rutaDelTanque.Length) return;
            vb.IssueMoveOrder(rutaDelTanque[indiceRuta].position);
        }

        void TickTrayecto(float dt)
        {
            var vb = tanque.GetComponent<VehicleBrain>();
            // El tanque no se detiene: cuando esta cerca del punto actual, ya se pide el siguiente.
            if (indiceRuta < rutaDelTanque.Length)
            {
                float d = Plano(rutaDelTanque[indiceRuta].position, tanque.transform.position).magnitude;
                if (d < 7f && indiceRuta < rutaDelTanque.Length - 1) { indiceRuta++; EmitirOrdenDeRuta(); }
                else if (vb != null && !vb.HasOrder && indiceRuta < rutaDelTanque.Length - 1) { indiceRuta++; EmitirOrdenDeRuta(); }
                else if (vb != null && !vb.HasOrder) EmitirOrdenDeRuta();
            }

            // El tanque aguanta hasta el final del trayecto (el guion lo destruye): nunca baja del 30 %.
            var vida = tanque.Health;
            if (vida != null && vida.IsAlive && vida.Current < vida.MaxHealth * 0.3f) vida.Heal(Mathf.CeilToInt(vida.MaxHealth * 0.4f));

            if (Reloj >= segundosDeTrayecto) { TickLlegada(); return; }
            MantenerAutos();
            int rest = Mathf.CeilToInt(TrayectoRestante);
            if (hud != null)
            {
                hud.Objetivo("OBJETIVO 4/6 · HUIR EN EL TANQUE", $"Dispara el CANON a los autos con metralleta · derribados: {AutosDestruidos} · el tanque sigue su camino", Reloj / segundosDeTrayecto, new Color(0.4f, 1f, 0.5f));
                hud.Timer("TRAYECTO", rest, rest <= 8 ? new Color(1f, 0.4f, 0.3f) : Color.white);
            }
        }

        // ---- Bug #060: llegada en calma ----
        // "El cambio no debe ser tan brusco: 3 segundos de margen de seguridad en que sigue la ruta en calma hasta que te bajas,
        // y un feedback sonoro de que termino el viaje y debes seguir a pie". Al cumplirse el trayecto: los perseguidores
        // abandonan (frenan y quedan atras, sin disparar), el tanque frena suave durante SegundosDeLlegada, la escuadra se baja
        // con una campana de "fin del viaje" y recien SegundosHastaElAtaque despues un cohete enemigo destruye el tanque vacio.
        public const float SegundosDeLlegada = 3f, SegundosHastaElAtaque = 2.6f;
        float llegadaDesde = -1f, velocidadDeViaje;
        public bool EnLlegada => llegadaDesde >= 0f;
        public bool BajaronDelTanque { get; private set; }

        void TickLlegada()
        {
            var motor = tanque.GetComponent<VehicleMotor>();
            var campoVel = typeof(VehicleMotor).GetField("maxSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (llegadaDesde < 0f)
            {
                llegadaDesde = Reloj;
                BajaronDelTanque = false;
                velocidadDeViaje = motor != null && campoVel != null ? (float)campoVel.GetValue(motor) : 0f;
                foreach (var a in autos) if (a != null && !a.Muerto) a.Retirarse();
                if (hud != null) hud.OcultarTimer();
                Aviso("LLEGANDO A LA CIUDAD\nEL TANQUE FRENA: PREPARATE PARA BAJAR");
                AudioDirector.PlayUi2D(SfxKind.RadialConfirm, 0.6f, 0.9f);
            }
            float t = Reloj - llegadaDesde;
            // Frenada suave hasta detenerse al final del margen.
            if (motor != null && campoVel != null) campoVel.SetValue(motor, Mathf.Lerp(velocidadDeViaje, 0.5f, Mathf.SmoothStep(0f, 1f, t / SegundosDeLlegada)));
            if (hud != null)
                hud.Objetivo("OBJETIVO 4/6 · HUIR EN EL TANQUE", BajaronDelTanque ? "Fin del viaje · segui a pie hasta la plaza de la ciudad" : "Llegando a la ciudad · el tanque frena en la entrada", 1f, new Color(0.4f, 1f, 0.5f));
            if (t >= SegundosDeLlegada && !BajaronDelTanque) BajarDelTanque();
            if (t >= SegundosDeLlegada + SegundosHastaElAtaque) DestruirTanque();
        }

        void BajarDelTanque()
        {
            BajaronDelTanque = true;
            var vb = tanque.GetComponent<VehicleBrain>();
            if (vb != null) vb.Stop();
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.CurrentSeat.HasValue && drv.Vehicle == tanque) drv.ExitVehicle();
            foreach (var o in new List<Soldier>(tanque.Occupants)) tanque.Dismount(o);
            // El panel de tripulacion (CO/CA/ME) y el cartel de asiento quedaban prendidos a pie.
            if (drv != null && drv.VehicleStatus != null) { drv.VehicleStatus.UpdateFrom(null, null); drv.VehicleStatus.gameObject.SetActive(false); }
            var yo = Poseido();
            if (yo != null) foreach (var s in Escuadra()) if (s != yo && s.Brain != null) OrderService.IssueFollowOrder(s, yo, default, 2f, true);
            AudioDirector.PlayUi2D(SfxKind.ObjetivoCumplido, 0.85f, 1f);
            Aviso("FIN DEL VIAJE\nSEGUI A PIE HASTA LA PLAZA");
            GameLog.Line("[Operacion] Fin del viaje en tanque: la escuadra baja");
        }

        void MantenerAutos()
        {
            autos.RemoveAll(a => a == null);
            int vivos = 0;
            foreach (var a in autos) if (!a.Muerto) { vivos++; }
            if (vivos < autosSimultaneos && Reloj >= proximoAuto && autoPlantilla != null)
            {
                proximoAuto = Reloj + UnityEngine.Random.Range(1.5f, 3f);
                CrearAuto();
            }
            // Los muertos ya sumaron: se cuentan una sola vez al detectar la muerte.
            foreach (var a in autos) if (a.Muerto && !contados.Contains(a)) { contados.Add(a); AutosDestruidos++; AlertQueue.Push("AUTO ENEMIGO DESTRUIDO · " + AutosDestruidos, AlertPriority.Media, 1.6f); }
        }

        readonly HashSet<OperacionAuto> contados = new HashSet<OperacionAuto>();

        void CrearAuto()
        {
            var t = tanque.transform;
            var frente = t.forward; frente.y = 0f; frente.Normalize();
            var lado = new Vector3(frente.z, 0f, -frente.x);
            float signo = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float lat = signo * UnityEngine.Random.Range(4.5f, 7.5f);
            float atras = -UnityEngine.Random.Range(11f, 20f);
            var pos = t.position - frente * 55f + lado * lat;
            pos.y = 0.6f;
            var go = InstanciarCamioneta(pos, Quaternion.LookRotation(frente, Vector3.up));
            go.name = "Auto_Perseguidor_" + (++AutosCreados);
            var a = go.GetComponent<OperacionAuto>();
            a.objetivo = tanque.transform;
            a.offsetDeseado = new Vector3(lat, 0f, atras);
            autos.Add(a);
        }

        void DestruirTanque()
        {
            // Los autos que quedan ya se retiraron (TickLlegada): se van sin explosion; solo explota alguno si quedo a la vista.
            var camara = SP.Core.CamaraPrincipal.Actual;
            foreach (var a in autos)
            {
                if (a == null || a.Muerto) continue;
                bool cerca = camara != null && Vector3.Distance(camara.transform.position, a.transform.position) < 60f;
                if (cerca && !a.Retirado) ImpactFx.SpawnExplosion(a.transform.position + Vector3.up, 3f);
                Destroy(a.gameObject);
            }
            autos.Clear();
            llegadaDesde = -1f;
            var vida = tanque.Health;
            if (vida != null) vida.Invulnerable = false;
            tanque.TakeDamage(9999999, -1);
            if (!tanque.IsDestroyed)
            {
                // Modo dios u otra proteccion: se fuerza igual.
                foreach (var o in new List<Soldier>(tanque.Occupants)) tanque.Dismount(o);
                tanque.FinalExplosion();
            }
            EnTanque = false;
            if (hud != null) hud.OcultarTimer();
            // El HUD de tripulacion y el cartel de asiento quedaban prendidos tras la explosion.
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.VehicleStatus != null) { drv.VehicleStatus.UpdateFrom(null, null); drv.VehicleStatus.gameObject.SetActive(false); }
            EntrarFase(FaseOperacion.Resistir);
        }

        // ---- 5. ciudad ----
        void ArrancarHeli()
        {
            if (heli == null) return;
            heli.gameObject.SetActive(true);
            VolarHeli(0f);
            if (ScriptHeli == null) ScriptHeli = heli.GetComponent<Helicoptero>();
            if (ScriptHeli != null) { ScriptHeli.Volando = true; ScriptHeli.Alerta(true); ScriptHeli.DisparaCobertura = true; }
            HeliAterrizo = false;
            lanzadasCiudad.Clear();
            pendientesCiudad.Clear();
            entroALaCiudad = false;
            // Bug #048: flecha con la distancia al helicoptero desde que sale hasta que la escuadra sube.
            if (flechaHeli == null) flechaHeli = SP.UI.FlechaDeObjetivo.Crear(heli, "HELICOPTERO", new Color(0.35f, 1f, 0.5f, 0.95f));
        }

        SP.UI.FlechaDeObjetivo flechaHeli;
        public SP.UI.FlechaDeObjetivo FlechaHeli => flechaHeli;
        void QuitarFlechaHeli() { if (flechaHeli != null) { flechaHeli.Quitar(); flechaHeli = null; } }

        readonly HashSet<int> lanzadasCiudad = new HashSet<int>();
        bool ciudadPausada, entroALaCiudad;

        void TickResistir(float dt)
        {
            var yo = Poseido();
            float dCiudad = yo != null ? Plano(yo.transform.position, plaza.position).magnitude : 0f;
            bool dentro = dCiudad <= radioDeLaCiudad;
            // El reloj de la fase solo corre mientras se resiste dentro de la ciudad.
            if (!dentro) { Reloj -= dt; ciudadPausada = true; } else { ciudadPausada = false; entroALaCiudad = true; }
            // Si el tanque frena antes del pueblo, todavia no se "salio" de la ciudad: se pide ENTRAR, no VOLVER.
            string pausa = entroALaCiudad ? "VOLVE A LA CIUDAD" : "ENTRA A LA CIUDAD";
            Reloj = Mathf.Max(0f, Reloj);

            for (int i = 0; i < oleadasDeLaCiudad.Length; i++)
            {
                if (lanzadasCiudad.Contains(i) || Reloj < oleadasDeLaCiudad[i].segundo) continue;
                lanzadasCiudad.Add(i);
                ActivarEscalonada(oleadasDeLaCiudad[i]);
            }
            TickPendientesDeLaCiudad();

            TickRefuerzosDeLaCiudad();
            VolarHeli(Mathf.Clamp01(Reloj / segundosDeResistencia));

            int seg = Mathf.CeilToInt(ResistenciaRestante);
            if (hud != null)
            {
                float proxima = ProximaOleadaDeLaCiudadEn;
                string oleadas = proxima >= 0f ? $"proxima oleada en {Mathf.CeilToInt(proxima)} s" : $"oleadas {lanzadasCiudad.Count}/{oleadasDeLaCiudad.Length}";
                hud.Objetivo("OBJETIVO 5/6 · RESISTIR EN LA CIUDAD", ciudadPausada ? (entroALaCiudad ? "VOLVE A LA CIUDAD: el temporizador esta detenido" : "Camina hasta el pueblo: la resistencia empieza al entrar") : $"Resiste {seg} s · {oleadas} · el helicoptero esta llegando", Reloj / segundosDeResistencia, ciudadPausada ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.55f, 0.25f));
                hud.Timer(ciudadPausada ? pausa : "RESISTI", seg, seg <= 5 ? new Color(1f, 0.3f, 0.25f) : Color.white);
            }
            if (Reloj >= segundosDeResistencia)
            {
                heli.position = heliAterrizaje.position;
                if (ScriptHeli != null) ScriptHeli.Volando = false;
                HeliAterrizo = true;
                if (hud != null) hud.OcultarTimer();
                EntrarFase(FaseOperacion.Extraer);
            }
        }

        // Bug #050: "la llegada del helicoptero deberia ser mas realista, con curva de velocidad". Antes arrancaba QUIETO a
        // 170 m (aceleraba desde 0 con un SmoothStep) y cruzaba a ~5 m/s: un helicoptero de rescate llega volando.
        // Ahora viene de lejos (~1 km) a velocidad de crucero (40 m/s, 144 km/h) y a 55 m de altura, con la nariz baja;
        // a mitad de la aproximacion empieza a frenar con desaceleracion constante, levanta la nariz (flare) y baja de
        // altura, queda en estacionario sobre la plaza y desciende despacio a posarse justo al terminar la cuenta.
        public const float VelocidadCruceroHeli = 40f, AlturaCruceroHeli = 55f, AlturaEstacionarioHeli = 16f;
        const float FraccionAproximacion = 0.78f, FraccionCrucero = 0.55f;
        public float VelocidadHeli { get; private set; }

        void VolarHeli(float u)
        {
            float T = Mathf.Max(5f, segundosDeResistencia);
            float t = Mathf.Clamp01(u) * T;
            float T1 = T * FraccionAproximacion;          // hasta quedar en estacionario sobre la plaza
            float tc = T1 * FraccionCrucero, td = T1 - tc; // crucero + frenado
            float v = VelocidadCruceroHeli;
            float D = v * tc + v * td * 0.5f;
            var sobre = heliAterrizaje.position + Vector3.up * AlturaEstacionarioHeli;
            var fin = heliAterrizaje.position;
            var haciaInicio = Plano(heliInicio.position, sobre);
            if (haciaInicio.sqrMagnitude < 1f) haciaInicio = Vector3.forward;
            haciaInicio.Normalize();
            var yaw = Quaternion.LookRotation(-haciaInicio, Vector3.up).eulerAngles.y;

            Vector3 pos; float inclinacion;
            if (t < T1)
            {
                float recorrido, vel, frenado;
                if (t < tc) { recorrido = v * t; vel = v; frenado = 0f; }
                else { float tau = t - tc; recorrido = v * tc + v * tau - v * tau * tau / (2f * td); vel = v * (1f - tau / td); frenado = 1f - tau / td; }
                float falta = D - recorrido;
                var plano = new Vector3(sobre.x, 0f, sobre.z) + haciaInicio * falta;
                // Altura: crucero hasta que empieza a frenar; despues baja hacia la del estacionario.
                float bajada = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tc * 0.85f, T1, t));
                pos = new Vector3(plano.x, Mathf.Lerp(heliAterrizaje.position.y + AlturaCruceroHeli, sobre.y, bajada), plano.z);
                // Nariz baja a velocidad de crucero; al frenar la levanta (flare) y vuelve a nivel en el estacionario.
                float avanceDelFrenado = 1f - frenado;   // 0 al empezar a frenar, 1 en el estacionario
                inclinacion = t < tc ? 9f
                    : Mathf.Lerp(9f, 0f, Mathf.SmoothStep(0f, 1f, avanceDelFrenado / 0.2f)) - 11f * Mathf.Sin(avanceDelFrenado * Mathf.PI);
                VelocidadHeli = vel;
            }
            else
            {
                float k = Mathf.Clamp01((t - T1) / Mathf.Max(0.01f, T - T1));
                pos = Vector3.Lerp(sobre, fin, Mathf.SmoothStep(0f, 1f, k));
                inclinacion = 0f;
                VelocidadHeli = 0f;
            }
            heli.position = pos;
            heli.rotation = Quaternion.Euler(inclinacion, yaw, 0f);
        }

        // ---- 6. extraccion ----
        void TickExtraer()
        {
            if (SubiendoAlHeli) return;
            var yo = Poseido();
            float d = yo != null ? Plano(yo.transform.position, heli.position).magnitude : 999f;
            if (hud != null) hud.Objetivo("OBJETIVO 6/6 · SUBIR AL HELICOPTERO", $"El helicoptero (con metralleta) aterrizo · {Mathf.RoundToInt(d)} m · acercate y apreta [E]", 0.9f, new Color(0.35f, 1f, 0.5f));
            if (d <= 11f && yo != null && yo.Health.IsAlive)
            {
                if (hud != null) hud.Prompt("[E] SUBIR AL HELICOPTERO");
                if (OperacionTerminal.EToque()) SubirAlHeli();
            }
        }

        public void SubirAlHeli()
        {
            if (SubiendoAlHeli) return;
            SubiendoAlHeli = true;
            QuitarFlechaHeli();
            StartCoroutine(RutinaDeSubida());
        }

        IEnumerator RutinaDeSubida()
        {
            var driver = PlayerInputDriver.Activo;
            if (driver != null)
            {
                OrderService.ManejadoAMano = null;
                driver.enabled = false;
                if (driver.Brain != null && driver.Brain.Current != null && driver.Brain.Current.Brain != null)
                    driver.Brain.Current.Brain.IsPossessedByPlayer = false;
            }
            var cam = CamaraPrincipal.Actual != null ? CamaraPrincipal.Actual : (driver != null && driver.Rig != null ? driver.Rig.Cam : null);
            if (driver != null && driver.Rig != null) driver.Rig.enabled = false;
            if (hud != null) hud.Aviso("SUBIENDO AL HELICOPTERO...", 3f);
            if (ScriptHeli != null) { ScriptHeli.Alerta(true); ScriptHeli.DisparaCobertura = true; }

            var puerta = heli.position + heli.right * 1.6f;
            var pasajeros = PrepararEmbarque();
            float proximaOrden = 0f;

            // Pedido: "mejora la animacion del final cuando me subo, que suene mejor". Cada soldado que sube suena (asiento +
            // golpe de puerta, un poco mas agudo cada uno), al despegar hay un barrido de camara con el rotor a fondo y al
            // terminar una fanfarria de logro + objetivo cumplido.
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.BoardAll), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1f, "BoardAll");
            fundido = CrearFundido();
            var negro = fundido;
            float t = 0f;
            bool despego = false;
            int subidos = 0;
            var posHeli = heli.position;
            var rumboSalida = heli.forward; rumboSalida.y = 0f; rumboSalida = rumboSalida.sqrMagnitude > 0.01f ? rumboSalida.normalized : Vector3.forward;
            Vector3 camaraFija = Vector3.zero; bool camaraFijada = false;
            float proximaRafagaEnemiga = 0f;
            // Bug #051: "le faltaron 4 segundos mas donde el helicoptero se va volando de los ataques enemigos y se aleja por 3
            // segundos". Linea de tiempo (12,4 s, antes 8,4):
            //   0    - 5,2  suben los soldados
            //   5,2  - 7,4  despegue vertical (acelera hacia arriba, sin avanzar) hasta ~9 m
            //   7,4  - 12,4 baja la nariz y acelera hacia adelante (6 m/s², ~30 m/s al final) trepando; los enemigos que quedan
            //               le tiran trazadoras y la metralleta de la puerta contesta
            //   9,4  - 12,4 la camara deja de seguirlo y lo ve alejarse; el rotor se apaga de a poco y fundido a negro al final
            const float DuracionSalida = 12.4f, InicioDespegue = 5.2f, InicioAvance = 7.4f, InicioAlejarse = 9.4f;
            while (t < DuracionSalida)
            {
                t += Time.deltaTime;
                // Bug #064: nada los distrae (curar, cubrirse, volver con el lider): cada segundo se les repite la orden de subir.
                if (t < InicioDespegue && Time.time >= proximaOrden)
                {
                    proximaOrden = Time.time + 1f;
                    foreach (var p in pasajeros) OrdenDeSubir(p, puerta);
                }
                if (t > 2.2f)
                    for (int i = pasajeros.Count - 1; i >= 0; i--)
                    {
                        var p = pasajeros[i];
                        if (p == null) { pasajeros.RemoveAt(i); continue; }
                        var dd = puerta - p.transform.position; dd.y = 0f;
                        if (dd.magnitude < 1.8f || t > 5f)
                        {
                            p.gameObject.SetActive(false); pasajeros.RemoveAt(i);
                            AudioDirector.PlayAt(SfxKind.SeatChange, puerta, 0.9f, 0.8f);
                            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.SeatChange), 0.35f * AudioDirector.GainFor(SfxChannel.Sfx), 1f + 0.06f * subidos, "SeatChange");
                            subidos++;
                        }
                    }
                if (t > InicioDespegue)
                {
                    if (!despego)
                    {
                        GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.CameraSwoosh), 0.8f * AudioDirector.GainFor(SfxChannel.Sfx), 0.8f, "CameraSwoosh");
                        if (hud != null) hud.Aviso("¡DESPEGANDO! EXTRACCION EXITOSA", 3f);
                        SubidosAlHeli = subidos;
                    }
                    despego = true;
                    if (ScriptHeli != null) ScriptHeli.Volando = true;
                    heli.position = PosicionDeSalida(posHeli, rumboSalida, t - InicioDespegue, InicioAvance - InicioDespegue, out float pitch);
                    heli.rotation = Quaternion.Euler(pitch, Quaternion.LookRotation(rumboSalida).eulerAngles.y, 0f);
                    if (t > InicioAvance - 0.8f && Time.time >= proximaRafagaEnemiga) proximaRafagaEnemiga = Time.time + TirarleAlHeli();
                }
                if (cam != null)
                {
                    var mira = heli.position + Vector3.up * 1.5f;
                    Vector3 pos;
                    if (!despego) pos = posHeli + new Vector3(-9f, 4.5f, -12f);
                    else if (t < InicioAlejarse) pos = heli.position - rumboSalida * 15f + Vector3.Cross(Vector3.up, rumboSalida) * 7f + Vector3.up * 3.5f;
                    else
                    {
                        // Se queda donde estaba y lo ve irse (3 s): el helicoptero se achica contra el cielo.
                        if (!camaraFijada) { camaraFijada = true; camaraFija = cam.transform.position; }
                        pos = camaraFija;
                    }
                    cam.transform.position = Vector3.Lerp(cam.transform.position, pos, Mathf.Clamp01(Time.deltaTime * (despego ? 3f : 4f)));
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation((mira - cam.transform.position).normalized), Mathf.Clamp01(Time.deltaTime * 5f));
                }
                // El rotor se apaga de a poco mientras se aleja (antes seguia sonando a todo volumen en la pantalla de victoria).
                if (ScriptHeli != null) ScriptHeli.VolumenExtra = 1f - Mathf.Clamp01((t - (DuracionSalida - 2.6f)) / 2.4f);
                // Fundido corto a negro (cierre de escena) que despues SE RETIRA: antes quedaba al 85% para siempre encima de la
                // pantalla de victoria y con timeScale 0 nada lo sacaba ("la pantalla como que se puso apagada").
                if (negro != null && t > DuracionSalida - 1.3f) negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01((t - (DuracionSalida - 1.3f)) / 1.2f));
                yield return null;
            }
            if (ScriptHeli != null) ScriptHeli.ApagarSonido();
            TerminarEmbarque();
            Ganar();
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.ObjetivoCumplido), 0.8f * AudioDirector.GainFor(SfxChannel.Sfx), 1f, "ObjetivoCumplido");
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Logro), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1.12f, "Logro");
            // Ganar() pone timeScale 0: el fundido se retira con tiempo NO escalado.
            float r = 0f;
            while (negro != null && r < 0.9f)
            {
                r += Time.unscaledDeltaTime;
                negro.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(r / 0.9f));
                yield return null;
            }
            QuitarFundido();
        }

        // ---- Bug #064: subida al helicoptero ----
        // "Se cayeron unos aliados. Quiero que cuando suban los aliados ignoren todo lo que esten haciendo y le den prioridad a
        // subir". Iban caminando a la puerta en modo pasivo (no contestaban el fuego) con 20+ enemigos encima, y el medico
        // ademas cortaba la subida para ir a curar: dos cayeron en el camino. Desde que se aprieta [E]: la escuadra es
        // invulnerable, se cancelan las curaciones/reanimaciones automaticas, los caidos se levantan (los suben heridos) y
        // todos van derecho a la puerta.
        public int HeridosRescatados { get; private set; }
        bool atencionPrevia = true, calmaPrevia = true, embarcando;

        List<Soldier> PrepararEmbarque()
        {
            embarcando = true;
            atencionPrevia = PedidoDeCuracion.AtencionAutomatica;
            calmaPrevia = PedidoDeCuracion.ReanimarEnCalma;
            PedidoDeCuracion.Cancelar();
            PedidoDeCuracion.LimpiarCola();
            PedidoDeCuracion.AtencionAutomatica = false;
            PedidoDeCuracion.ReanimarEnCalma = false;
            HeridosRescatados = 0;
            var pasajeros = new List<Soldier>();
            foreach (var s in Escuadra(false))
            {
                if (!s.Health.IsAlive)
                {
                    if (!SP.Player.Reanimacion.Ejecutar(s, 0.35f)) continue;
                    HeridosRescatados++;
                }
                s.Health.Invulnerable = true;
                pasajeros.Add(s);
            }
            SP.Mision.EstadisticasDeMision.HeridosRescatados = HeridosRescatados;
            if (HeridosRescatados > 0) GameLog.Line($"[Operacion] Subida al helicoptero: {HeridosRescatados} caido(s) levantado(s) para subir");
            return pasajeros;
        }

        static void OrdenDeSubir(Soldier s, Vector3 puerta)
        {
            if (s == null || !s.gameObject.activeInHierarchy || s.Brain == null) return;
            s.Brain.IsPossessedByPlayer = false;
            s.Brain.Quieto = false;
            s.Brain.Pasivo = true;
            s.Brain.Atrincherado = false;
            if (s.Motor != null) s.Motor.SetCrouching(false);
            var lado = new Vector3(((s.Id * 37) % 7 - 3) * 0.3f, 0f, ((s.Id * 53) % 5 - 2) * 0.3f);
            s.Brain.IssueMoveOrder(puerta + lado);
        }

        void TerminarEmbarque()
        {
            if (!embarcando) return;
            embarcando = false;
            PedidoDeCuracion.AtencionAutomatica = atencionPrevia;
            PedidoDeCuracion.ReanimarEnCalma = calmaPrevia;
            foreach (var s in Escuadra(false)) if (s.Health != null) s.Health.Invulnerable = false;
            SP.Mision.EstadisticasDeMision.SupervivientesForzados = SubidosAlHeli;
        }

        // Despegue: primero vertical (acelera hacia arriba sin avanzar), despues baja la nariz y acelera hacia adelante
        // trepando, como un helicoptero real (antes subia y avanzaba con una curva cubica a la vez desde el piso).
        static Vector3 PosicionDeSalida(Vector3 origen, Vector3 rumbo, float k, float vertical, out float pitch)
        {
            const float altura = 9f;
            if (k < vertical)
            {
                float x = k / vertical;
                pitch = Mathf.Lerp(0f, 3f, x);
                return origen + Vector3.up * (altura * x * x * (3f - 2f * x));
            }
            float a = k - vertical;
            const float aceleracion = 6f, trepada = 4.2f;   // m/s² hacia adelante, m/s de ascenso
            pitch = Mathf.Lerp(3f, 13f, Mathf.Clamp01(a / 1.6f));
            return origen + Vector3.up * (altura + trepada * a + 0.4f * a * a) + rumbo * (0.5f * aceleracion * a * a);
        }

        // Los enemigos que quedan le tiran al helicoptero mientras se va (trazadoras: no le hacen nada, es la cinematica).
        float TirarleAlHeli()
        {
            if (heli == null) return 1f;
            var pool = ProjectilePool.Activo;
            if (pool == null) return 1f;
            int disparos = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (disparos >= 6) break;
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if ((s.transform.position - heli.position).sqrMagnitude > 140f * 140f) continue;
                var boca = s.transform.position + Vector3.up * 1.4f;
                var dir = (heli.position + Vector3.up * 1f - boca).normalized;
                dir = (dir + UnityEngine.Random.insideUnitSphere * 0.06f).normalized;
                pool.Spawn(boca, dir, s.Id, TeamId.Enemy, 0, new Color(1f, 0.4f, 0.2f));
                AudioDirector.PlayAt(SfxKind.Shoot, boca, 0.3f);
                disparos++;
            }
            return disparos > 0 ? UnityEngine.Random.Range(0.12f, 0.25f) : 0.5f;
        }

        Image fundido;
        public static int SubidosAlHeli { get; private set; }
        public bool FundidoActivo => fundido != null && fundido.color.a > 0.02f;

        void QuitarFundido()
        {
            if (fundido == null) return;
            var c = fundido.canvas != null ? fundido.canvas.gameObject : fundido.gameObject;
            fundido = null;
            Destroy(c);
        }

        void OnDisable() { QuitarFundido(); QuitarFlechaHeli(); }

        Image CrearFundido()
        {
            var go = new GameObject("OperacionFundido", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 900;
            var im = new GameObject("Negro", typeof(RectTransform), typeof(Image));
            im.transform.SetParent(go.transform, false);
            var r = im.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var img = im.GetComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = false;
            return img;
        }

        void Ganar()
        {
            EntrarFase(FaseOperacion.Victoria);
            // Bug #051: nada del helicoptero sigue sonando debajo de la pantalla de victoria (que corre en timeScale 0).
            if (ScriptHeli != null) { ScriptHeli.ApagarSonido(); ScriptHeli.DisparaCobertura = false; }
            if (hud != null) hud.Aviso("MISION CUMPLIDA", 6f);
            GameLog.Line("[Operacion] VICTORIA");
            var outcome = GameOutcomeController.Activo;
            if (outcome != null) outcome.ShowVictory();
        }

        public void Perder(string motivo)
        {
            if (Fase == FaseOperacion.Victoria || Fase == FaseOperacion.Derrota) return;
            Motivo = motivo;
            Fase = FaseOperacion.Derrota;
            QuitarFlechaHeli();
            var outcome = GameOutcomeController.Activo;
            if (outcome != null) outcome.ShowDefeat(motivo);
        }

        // ---------------------------------------------------------------
        // Registro de sesion: capturar / restaurar el estado de la mision (ver SesionLog)
        // ---------------------------------------------------------------
        public string ClaveEstado => "Operacion";

        static string Csv(HashSet<int> h) => string.Join(",", h);
        static void LeerCsv(string s, HashSet<int> destino)
        {
            destino.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var p in s.Split(',')) if (int.TryParse(p, out var n)) destino.Add(n);
        }

        public void CapturarEstado(Dictionary<string, string> kv)
        {
            kv["fase"] = Fase.ToString();
            kv["reloj"] = Reloj.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            kv["puestoActual"] = PuestoActual.ToString();
            kv["enTanque"] = EnTanque.ToString();
            kv["indiceRuta"] = indiceRuta.ToString();
            kv["autosDestruidos"] = AutosDestruidos.ToString();
            kv["hackSegundos"] = (hackInicio >= 0f ? Time.time - hackInicio : -1f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            kv["oleadasCentro"] = Csv(oleadasLanzadas);
            kv["oleadasCiudad"] = Csv(lanzadasCiudad);
            for (int i = 0; i < puestos.Length; i++)
            {
                var p = puestos[i];
                kv["puesto" + (i + 1)] = $"abierto={p.abierto};progreso={(p.panel != null ? p.panel.Progreso01 : 0f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)};completo={(p.panel != null && p.panel.Completo)};guardiasVivos={Vivos(p.guardias)}";
            }
            if (computadora != null) kv["computadora"] = $"progreso={computadora.Progreso01.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)};completo={computadora.Completo};operador={(computadora.Operador != null ? computadora.Operador.DisplayName : "-")}";
            kv["cuartelVivos"] = Vivos(enemigosCuartel).ToString();
            if (heli != null) kv["heli"] = $"activo={heli.gameObject.activeSelf};pos={heli.position.x:0.0},{heli.position.y:0.0},{heli.position.z:0.0};aterrizo={HeliAterrizo}";
        }

        static float F(Dictionary<string, string> kv, string k, float def = 0f)
            => kv.TryGetValue(k, out var s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

        public void RestaurarEstado(Dictionary<string, string> kv)
        {
            if (!kv.TryGetValue("fase", out var fs) || !Enum.TryParse(fs, out FaseOperacion fase)) return;
            int puesto = (int)F(kv, "puestoActual");
            // SaltarA deja hechas todas las fases anteriores (cuartel limpio, puestos abiertos...) sin mover a nadie.
            SaltarA(fase, false);
            if (fase == FaseOperacion.Puestos)
            {
                for (int i = 0; i < puestos.Length; i++)
                {
                    var p = puestos[i];
                    if (i < puesto)
                    {
                        p.abierto = true;
                        if (p.panel != null) p.panel.Avanzar(99f);
                        if (p.barrera != null) p.barrera.SetActive(false);
                    }
                    else
                    {
                        p.abierto = false;
                        if (p.barrera != null) p.barrera.SetActive(true);
                        if (p.panel != null) { p.panel.ResetearProgreso(); p.panel.Habilitado = true; }
                    }
                }
                PuestoActual = Mathf.Clamp(puesto, 0, puestos.Length - 1);
                BalizaDelPuesto();
            }
            if (fase == FaseOperacion.CentroDeDatos && computadora != null)
            {
                computadora.ResetearProgreso();
                computadora.Avanzar(F(kv, "hackSegundos", 0f) > 0f ? Mathf.Min(computadora.duracion * 0.999f, F(kv, "hackSegundos")) : 0f);
                hackInicio = -1f;
                if (kv.TryGetValue("oleadasCentro", out var oc)) LeerCsv(oc, oleadasLanzadas);
            }
            if (fase == FaseOperacion.Huir && kv.TryGetValue("enTanque", out var et) && et == "True")
            {
                Embarcar();
                indiceRuta = Mathf.Clamp((int)F(kv, "indiceRuta"), 0, Mathf.Max(0, rutaDelTanque.Length - 1));
                EmitirOrdenDeRuta();
            }
            if (fase == FaseOperacion.Resistir || fase == FaseOperacion.Huir)
            {
                if (kv.TryGetValue("oleadasCiudad", out var ow)) LeerCsv(ow, lanzadasCiudad);
            }
            Reloj = F(kv, "reloj");
            AutosDestruidos = (int)F(kv, "autosDestruidos");
            if (heli != null && kv.TryGetValue("heli", out var hs) && fase == FaseOperacion.Resistir)
            {
                // el helicoptero vuela segun el reloj de la fase: ya queda donde estaba
                VolarHeli(Mathf.Clamp01(Reloj / segundosDeResistencia));
            }
            SesionLog.Evento($"Operacion restaurada: fase={fase} reloj={Reloj:0.0} puesto={PuestoActual + 1}");
        }

        // ---------------------------------------------------------------
        // Pruebas y capturas: salta a una fase dejando hecho todo lo anterior.
        // ---------------------------------------------------------------
        public void SaltarA(FaseOperacion destino, bool teletransportar = true)
        {
            StopAllCoroutines();
            if ((int)destino > (int)FaseOperacion.Infiltrar)
            {
                foreach (var s in enemigosCuartel) if (s != null) s.gameObject.SetActive(false);
                if (portonCuartel != null) { portonCuartel.SetActive(false); }
            }
            if ((int)destino > (int)FaseOperacion.Puestos)
            {
                foreach (var p in puestos)
                {
                    p.abierto = true;
                    if (p.barrera != null) p.barrera.SetActive(false);
                    foreach (var g in p.guardias) if (g != null) g.gameObject.SetActive(false);
                    if (p.panel != null) { p.panel.Habilitado = false; }
                }
                PuestoActual = puestos.Length;
            }
            else if (destino == FaseOperacion.Puestos) PuestoActual = 0;
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            if (teletransportar && (int)destino < entradas.Length && entradas[(int)destino] != null) TeletransportarEscuadra(entradas[(int)destino].position, entradas[(int)destino].rotation);
            if (destino == FaseOperacion.Extraer)
            {
                // Atajo de pruebas: el helicoptero ya esta posado en la plaza.
                ArrancarHeli();
                heli.position = heliAterrizaje.position;
                if (ScriptHeli != null) ScriptHeli.Volando = false;
                HeliAterrizo = true;
            }
            EntrarFase(destino);
        }

        // Prueba por CLI (OperacionPrueba): arranca en el puesto N (0..2) con los anteriores abiertos, la escuadra 7 m antes de su panel.
        public void SaltarAPuesto(int indice)
        {
            indice = Mathf.Clamp(indice, 0, puestos.Length - 1);
            SaltarA(FaseOperacion.Puestos, true);
            for (int i = 0; i < indice; i++)
            {
                var p = puestos[i];
                p.abierto = true;
                if (p.barrera != null) p.barrera.SetActive(false);
                foreach (var g in p.guardias) if (g != null) g.gameObject.SetActive(false);
                if (p.panel != null) p.panel.Habilitado = false;
            }
            PuestoActual = indice;
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            if (indice > 0 && puestos[indice].panel != null && puestos[indice - 1].panel != null)
            {
                var hacia = Plano(puestos[indice].panel.transform.position, puestos[indice - 1].panel.transform.position).normalized;
                var punto = puestos[indice].panel.transform.position - hacia * 7f;
                TeletransportarEscuadra(punto, Quaternion.LookRotation(hacia, Vector3.up));
            }
            BalizaDelPuesto();
        }

        public void TeletransportarEscuadra(Vector3 punto, Quaternion rot)
        {
            var lista = Escuadra(false);
            var yo = Poseido();
            lista.Remove(yo);
            if (yo != null) lista.Insert(0, yo);
            for (int i = 0; i < lista.Count; i++)
            {
                var s = lista[i];
                var off = rot * new Vector3((i - 1) * 2.2f, 0f, -i * 1.2f);
                s.transform.position = new Vector3(punto.x + off.x, s.transform.position.y, punto.z + off.z);
                s.transform.rotation = rot;
                ApoyoEnElPiso.Apoyar(s.transform);
                if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
            }
        }
    }
}
