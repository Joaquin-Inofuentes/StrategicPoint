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

    // WP9a (#082/#097): un puesto de la muralla de contencion = un bunker con su punto de carga. El "Asalto" coloca la carga, arde la mecha,
    // sale el contraataque por la puerta lateral y el bunker vuela. Reemplaza a los 3 puestos de control con panel (z -185/-150/-115).
    [Serializable]
    public class PuestoDeVoladura
    {
        public string nombre;                 // "PUESTO OESTE"
        public string lado;                   // "OESTE" / "ESTE"
        public Transform bunker;              // raiz del bunker (cuerpo, garita, parapetos)
        public CargaExplosiva carga;          // el punto de carga
        public Soldier[] guardias;            // 5 a cubierto, mirando al sur
        public Soldier nido;                  // el artillero de la ametralladora del techo (muere con el bunker)
        public OleadaDeReserva contraataque;  // 6 reservistas dentro de la puerta lateral (salen 4 la primera vez, 6 la segunda)
        public Transform puerta;              // la puerta lateral del bunker
        public Transform salidaCamioneta;     // de donde sale la camioneta de asalto (solo con la segunda carga)
        public Transform paradaCamioneta;     // donde se planta a tirar
        [NonSerialized] public bool volado;
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
    //   2. PUESTOS     muralla de contencion con dos bunkers: solo el ASALTO coloca las cargas; hay que aguantar el contraataque y volarlos
    //   3. DATOS       un soldado interactua 30 s con la computadora del centro de datos mientras llegan oleadas
    //   4. HUIR        subir al tanque (el jugador es el artillero: maneja SOLO el canon) y recorrer 60 s de carretera
    //                  mientras autos con metralleta lo persiguen en bucle; al final del trayecto lo destruyen
    //   5. RESISTIR    40 s a pie en una ciudad chica hasta que llegue el helicoptero (vuela hasta la plaza: no esta estacionado)
    //   6. EXTRAER     subir al helicoptero (tiene metralleta de puerta) -> fin de la mision
    public partial class OperacionDirector : MonoBehaviour, IEstadoGuardable
    {
        public static OperacionDirector Instancia { get; private set; }
        public static bool Activo => Instancia != null && Instancia.isActiveAndEnabled;

        [Header("1. Cuartel")]
        public Soldier[] enemigosCuartel;
        public GameObject portonCuartel;
        public Transform[] entradas;                 // un punto de partida por fase (para SaltarA y reinicios)
        // WP8 (#078): torres de francotirador (2) y de torreta (2), y reflectores (4) del cuartel. Los que estan arriba (tiradores y operadores)
        // tambien van en enemigosCuartel: el objetivo se cumple con todos muertos.
        public TorreDestruible[] torres;
        public ReflectorVigia[] reflectores;

        [Header("2. Muralla de contencion: dos puestos a volar")]
        public PuestoDeVoladura[] puestos;                // Oeste y Este
        public GameObject portonBlindado;                 // se hunde al volar los dos
        public Soldier[] soldadosDeTrinchera;             // 2 en la trinchera de la zona de aproximacion (z -195)
        public Soldier[] soldadosDelCorredor;             // 3 rezagados del corredor de trincheras (z -165..-100)
        [Header("Anclas de zona (WP9a): una por objetivo; las usa la cinematica inicial. WP9b y WP10 deben mantenerlas.")]
        public Transform[] anclasDeZona;                  // 1 cuartel, 2 muralla, 3 centro de datos, 4 patio del tanque, 5 plaza, 6 helipuerto

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
        public float segundosDeTrayecto = 70f;
        public int autosSimultaneos = 4;   // bug #073: eran 3

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
        // Puestos volados (0..2).
        public int PuestoActual { get; private set; }
        public float Reloj { get; private set; }               // segundos dentro de la fase actual
        public bool EnTanque { get; private set; }
        public int AutosDestruidos { get; private set; }
        public int AutosCreados { get; private set; }
        public float TrayectoRestante => EnTanque ? Mathf.Max(0f, segundosDeTrayecto - Reloj) : segundosDeTrayecto;
        public float ResistenciaRestante => Mathf.Max(0f, segundosDeResistencia - (Fase == FaseOperacion.Resistir && MandoActivo ? RelojEfectivo : Reloj));
        public bool HeliAterrizo { get; private set; }
        // #127: el helicoptero esta sobre la zona de aterrizaje (en estacionario cubriendo o ya posado).
        public bool HeliEnLaZona => HeliAterrizo || CoberturaActiva;
        public bool SubiendoAlHeli { get; private set; }
        public Helicoptero ScriptHeli { get; private set; }
        public string Motivo { get; private set; } = "";

        // ---- Columna de la Operacion (WP0) ----
        // Cantidad de objetivos que muestra el HUD ("OBJETIVO n/6"): un solo lugar para cambiarla si el nivel suma o saca uno.
        public const int TotalObjetivos = 6;
        public static string TituloObjetivo(int n, string nombre) => $"OBJETIVO {n}/{TotalObjetivos} · {nombre}";

        // Orden real de las fases jugables. El enum NO cambia (los rediseños usan subfases internas): quien compare fases
        // por orden usa Indice() y no el valor numerico del enum. Victoria y Derrota quedan despues del ultimo (Indice = Orden.Length).
        public static readonly FaseOperacion[] Orden =
            { FaseOperacion.Infiltrar, FaseOperacion.Puestos, FaseOperacion.CentroDeDatos, FaseOperacion.Huir, FaseOperacion.Resistir, FaseOperacion.Extraer };
        public static int Indice(FaseOperacion f)
        {
            int i = Array.IndexOf(Orden, f);
            return i >= 0 ? i : Orden.Length;
        }

        // Paso interno de una fase (0 = el de siempre). Las fases rediseñadas lo usan para partirse en tramos sin tocar el enum.
        public int Subfase { get; private set; }
        protected void EntrarSubfase(int s)
        {
            Subfase = s;
            SesionLog.Evento($"SUBFASE de la Operacion -> {Fase}/{s}");
            AlEntrarSubfase(s);
        }
        // WP9b: cada fase que se parte en subfases engancha aca lo que tiene que preparar (hoy solo Huir, en OperacionDirector.Jefe.cs).
        partial void AlEntrarSubfase(int s);

        OperacionHud hud;
        TutorialBeacon baliza;
        float hackInicio = -1f;
        readonly HashSet<int> oleadasLanzadas = new HashSet<int>();
        readonly List<OperacionAuto> autos = new List<OperacionAuto>();
        int indiceRuta;
        float proximoAuto;
        float velocidadOriginal = -1f;

        // ---------------------------------------------------------------
        void Awake()
        {
            Instancia = this;
            ResistirSinMando = false;
            AnuncioDeZonas.Desactivado = true;
            // Bug #095: el HUD tiene que decir la municion total REAL, no "infinita": la Operacion usa reservas (4 cargadores por arma).
            WeaponHolder.CargadoresDeReserva = CargadoresPorDificultad();
            WeaponHolder.ReservasActivas = true;
        }

        // #110: reservas de municion por dificultad (el doble en FACIL, proporcional en MEDIO y DIFICIL). Aplica al soldado que manejas
        // (los aliados de la IA siguen sin limite hasta que los poseas: ahi heredan la reserva).
        public const int CargadoresIniciales = 4;   // el valor de DIFICIL (base)
        public static int CargadoresPorDificultad(NivelDificultad n)
        {
            switch (n)
            {
                case NivelDificultad.Facil: return CargadoresIniciales * 2;
                case NivelDificultad.Medio: return CargadoresIniciales + 2;
                default: return CargadoresIniciales;
            }
        }
        public static int CargadoresPorDificultad() => CargadoresPorDificultad(Dificultad.Actual);

        // Al empezar cada objetivo el soldado que manejas repone todas sus armas (no hay cajas de suministros en el nivel).
        void ReabastecerAlJugador()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            var yo = d != null && d.Brain != null ? d.Brain.Current : null;
            WeaponHolder.CargadoresDeReserva = CargadoresPorDificultad();   // #110: la dificultad pudo cambiar desde el menu
            if (yo != null && yo.Weapon != null) yo.Weapon.ReponerMunicion();
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            WeaponHolder.CargadoresDeReserva = WeaponHolder.CargadoresPorDefecto;
            SonidosDeOperacion.DetenerAmbiente();
            AnuncioDeZonas.Desactivado = false;
            if (baliza != null) baliza.Quitar();
            OnDestroyMando();
            DesuscribirPuestos();
        }

        void Start()
        {
            // P10: si se llega por CONTINUAR, la partida se restaura al arrancar (en un host que sobrevive a SaltarA) y no se repiten las cinematicas.
            bool continuando = Application.isPlaying && PartidaGuardada.HayPendiente;
            if (continuando) PartidaGuardada.ProgramarRestauracion();
            // El marcador de aparicion/rondas de los reservistas queda apagado hasta su oleada.
            SuscribirPuestos();
            GuardarTanqueInicial();
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
            if (Application.isPlaying) GuiaDeObjetivo.Asegurar();   // #105: flecha 3D + ruta punteada + flecha a los pies
            EntrarFase(FaseOperacion.Infiltrar);
            // WP9a (#097): cinematica inicial con los pasos de la operacion. Bloquea todo hasta terminar (o hasta mantener [Espacio] 1 s).
            // P9 (#123): primero el rapel desde el helicoptero (5 s); al terminar encadena la cinematica de los pasos.
            if (continuando) return;
            if (Application.isPlaying && !CinematicaDeRapel.YaVista) CinematicaDeRapel.Iniciar(this);
            else if (Application.isPlaying && !CinematicaDeOperacion.YaVista) CinematicaDeOperacion.Iniciar(this);
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
                if (MandoTactico.EsMiliciano(s)) continue;   // WP10: los milicianos de la ciudad no suben al helicoptero
                r.Add(s);
            }
            return r;
        }

        // #128: a quien se evacua en el helicoptero: la escuadra Y los milicianos de la ciudad (antes Escuadra() los excluia y quedaban en tierra).
        // P9 (#123): la escuadra viva y activa (sin milicianos), para la cinematica de rapel.
        public static List<Soldier> EscuadraDeLaOperacion() => Escuadra(true);

        // MilicianosSuben = false reproduce el bug #128 (solo para los checks).
        public static bool MilicianosSuben = true;
        public static List<Soldier> AliadosAEvacuar(bool soloVivos = true)
        {
            var r = Escuadra(soloVivos);
            if (!MilicianosSuben) return r;
            foreach (var m in MandoTactico.Milicianos)
            {
                if (m == null || m.Health == null || r.Contains(m)) continue;
                if (soloVivos && !m.Health.IsAlive) continue;
                if (!m.gameObject.activeInHierarchy) continue;
                r.Add(m);
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
            if (Fase == FaseOperacion.Resistir && f != FaseOperacion.Resistir) { AbandonarElMando(); ResistirSinMando = false; }   // WP10: suelta la radio y quita el panel
            if (f != FaseOperacion.Infiltrar && hud != null) hud.Subobjetivos(null);   // la linea de torres/torretas/reflectores es solo del cuartel
            Fase = f;
            Subfase = 0;
            Reloj = 0f;
            if (terminoUnObjetivo) SonarObjetivoCumplido();
            SesionLog.Evento("FASE de la Operacion -> " + f + (f == FaseOperacion.Puestos ? " (volados " + PuestoActual + "/" + (puestos != null ? puestos.Length : 0) + ")" : ""));
            if (hud == null) hud = OperacionHud.Crear();
            if (Application.isPlaying) ReabastecerAlJugador();
            if (Application.isPlaying && Indice(f) <= Indice(FaseOperacion.Huir)) StartCoroutine(SeguirAlJugadorAlEmpezar(f));
            switch (f)
            {
                case FaseOperacion.Infiltrar:
                    PonerBaliza("SALIDA NORTE", new Color(1f, 0.82f, 0.3f), portonCuartel != null ? portonCuartel.transform.position : transform.position, null);
                    Aviso("OPERACION CUARTEL\nINFILTRATE Y ELIMINA A TODOS");
                    if (Application.isPlaying && !cuartelAtrincherado) { cuartelAtrincherado = true; AtrincherarGuardias(GuardiasDeSuelo(enemigosCuartel)); }
                    break;
                case FaseOperacion.Puestos:
                    PuestoActual = ContarVolados();
                    BalizaDelPuesto();
                    Aviso("MURALLA DE CONTENCIÓN\nVOLÁ LOS DOS PUESTOS: SOLO EL ASALTO COLOCA LAS CARGAS");
                    if (Application.isPlaying && !puestosAtrincherados)
                    {
                        puestosAtrincherados = true;
                        AtrincherarGuardias(GuardiasDeLaMuralla());
                    }
                    break;
                case FaseOperacion.CentroDeDatos:
                    PonerBaliza("COMPUTADORA", new Color(0.35f, 0.8f, 1f), computadora.transform.position, null, 2.4f);
                    Aviso("CENTRO DE DATOS\nUN SOLDADO INTERACTUA 30 s · LOS DEMAS DEFIENDEN");
                    break;
                case FaseOperacion.Huir:
                    // WP9b (#097): el tanque es un jefe enemigo (BLINDADO GOLIAT); las oleadas salen mientras Kes lo repara.
                    PrepararHuida();
                    Aviso("DATOS DESCARGADOS\nAVANZA AL PATIO: UN BLINDADO ENEMIGO CUSTODIA LA FUGA");
                    break;
                case FaseOperacion.Resistir:
                    if (MandoActivo)
                    {
                        // WP10 (#101): el objetivo 5 se juega desde la radio del ayuntamiento, en vista tactica.
                        IniciarLlegadaAlMando();
                        Aviso("TANQUE DESTRUIDO\nANDA A LA RADIO DEL AYUNTAMIENTO Y DIRIGI LA DEFENSA");
                    }
                    else
                    {
                        AbandonarElMando();
                        PonerBaliza("PLAZA", new Color(1f, 0.55f, 0.25f), plaza.position, null, 3f);
                        Aviso("TANQUE DESTRUIDO\nRESISTE " + Mathf.RoundToInt(segundosDeResistencia) + " s: EL HELICOPTERO ESTA EN CAMINO");
                    }
                    ArrancarHeli();
                    break;
                case FaseOperacion.Extraer:
                    MostrarFlechaDelHeli();   // P7 (#114): en Resistir con mando no se muestra; aparece ahora, con el heli ya en la zona de aterrizaje
                    PonerBaliza("HELICOPTERO", new Color(0.35f, 1f, 0.5f), heli.position, heli, 3.6f);
                    if (Application.isPlaying && !HeliAterrizo) IniciarCoberturaDeExtraer();   // #127: estacionario, cubriendo con fuego pesado
                    else Aviso("EL HELICOPTERO ATERRIZO\nSUBE CON [E]");
                    break;
                case FaseOperacion.Victoria:
                    if (baliza != null) { baliza.Quitar(); baliza = null; }
                    break;
            }
            // P10: autoguardado al empezar cada objetivo (despues de reabastecer, cuando termina cualquier cinematica).
            if (Application.isPlaying && Indice(f) < Orden.Length && !PartidaGuardada.Restaurando) ProgramarAutoguardado($"inicio objetivo {Indice(f) + 1}");
        }

        // Bug #042: los guardias arrancan detras de coberturas (o de una barricada) mirando hacia donde viene la escuadra.
        bool cuartelAtrincherado, puestosAtrincherados;
        void AtrincherarGuardias(IEnumerable<Soldier> guardias)
        {
            var yo = Poseido();
            var amenaza = yo != null ? yo.transform.position : (entradas != null && entradas.Length > 0 && entradas[0] != null ? entradas[0].position : transform.position);
            Atrincherar.Aplicar(guardias, amenaza);
        }

        // Los tiradores de las torres y los operadores de reflector no se atrincheran ni cazan: estan arriba, fijos.
        List<Soldier> GuardiasDeSuelo(IEnumerable<Soldier> todos)
        {
            var r = new List<Soldier>();
            if (todos == null) return r;
            foreach (var s in todos)
            {
                if (s == null) continue;
                if (EsOcupanteDeTorre(s)) continue;
                if (EsOperadorDeReflector(s)) continue;
                r.Add(s);
            }
            return r;
        }

        bool EsOcupanteDeTorre(Soldier s)
        {
            if (torres != null) foreach (var t in torres) if (t != null && t.ocupante == s) return true;
            return false;
        }

        static bool EsOperadorDeReflector(Soldier s)
        {
            var rs = ReflectorVigia.Todos;
            for (int i = 0; i < rs.Count; i++) if (rs[i] != null && rs[i].operador == s) return true;
            return false;
        }

        void Aviso(string t) { if (hud != null) hud.Aviso(t, 4f); GameLog.Line("[Operacion] " + t.Replace("\n", " · ")); }

        void PonerBaliza(string texto, Color c, Vector3 pos, Transform sigue, float radio = 3f)
        {
            if (baliza != null) baliza.Quitar();
            baliza = TutorialBeacon.Crear(texto, c, new Vector3(pos.x, 0.15f, pos.z), sigue, radio, 26f);
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
            if (hud != null && (Fase == FaseOperacion.Infiltrar || Fase == FaseOperacion.CentroDeDatos)) hud.OcultarTimer();   // Puestos usa el timer para la mecha
            ActualizarPasos();
            TickAnilloDeSubida();
        }

        // #105: la guia de objetivo (GuiaDeObjetivo) solo corre en las fases 1 a 4 y sigue a la baliza activa.
        public bool GuiaPermitida(out Vector3 objetivo)
        {
            objetivo = Vector3.zero;
            if (baliza == null || Fase == FaseOperacion.Victoria || Fase == FaseOperacion.Derrota) return false;
            // P7 (#114): tambien guia la llegada a la radio del ayuntamiento (objetivo 5, subfase 0); despues la guia es la del mando (GuiaDeMando).
            bool llegadaALaRadio = Fase == FaseOperacion.Resistir && MandoActivo && Subfase == 0;
            if (Indice(Fase) > Indice(FaseOperacion.Huir) && !llegadaALaRadio) return false;
            if (Fase == FaseOperacion.Huir && EnTanque) return false;
            objetivo = baliza.Posicion;
            return true;
        }

        // #111: el anillo vibrante del tanque, solo en el abordaje y mientras el jugador no este adentro.
        void TickAnilloDeSubida()
        {
            bool debe = Fase == FaseOperacion.Huir && Subfase == (int)SubfaseHuida.Abordaje && !EnTanque && tanque != null && tanque.Health != null && tanque.Health.IsAlive;
            if (debe)
            {
                var drv = PlayerInputDriver.Activo;
                if (drv != null && drv.Vehicle == tanque && drv.CurrentSeat.HasValue) debe = false;
            }
            if (debe) { if (!AnilloDeSubida.EstaActivo) AnilloDeSubida.Mostrar(tanque.transform); }
            else if (AnilloDeSubida.EstaActivo) AnilloDeSubida.Ocultar();
        }

        // #105: al empezar cada objetivo de las fases 1-4, la escuadra sin orden recibe Follow al jugador (con un solo cartel agrupado).
        public const float SegundosParaSeguirAlEmpezar = 1.2f;
        public static int SeguimientosAutomaticos { get; private set; }
        IEnumerator SeguirAlJugadorAlEmpezar(FaseOperacion f)
        {
            yield return new WaitForSeconds(SegundosParaSeguirAlEmpezar);
            if (Fase != f) yield break;
            var yo = Poseido();
            if (yo == null || yo.Brain == null) yield break;
            var lista = new List<Soldier>();
            foreach (var s in Escuadra())
            {
                if (s == yo || s.Brain == null || s.Brain.IsPossessedByPlayer || s.Brain.MontadoEnVehiculo) continue;
                if (s.Brain.TieneOrden) continue;
                lista.Add(s);
            }
            if (lista.Count > 0) { SeguimientosAutomaticos += lista.Count; OrderService.IssueFollowOrderForSelection(lista, yo); }
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
            kv["subfase"] = Subfase.ToString();
            kv["enTanque"] = EnTanque.ToString();
            kv["indiceRuta"] = indiceRuta.ToString();
            kv["autosDestruidos"] = AutosDestruidos.ToString();
            if (Fase == FaseOperacion.Huir && tanque != null && tanque.Health != null)
            {
                kv["jefeVida"] = (tanque.Health.Current / (float)tanque.Health.MaxHealth).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                kv["reparacion"] = (reparacion != null ? reparacion.Progreso01 : 0f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            }
            kv["hackSegundos"] = (hackInicio >= 0f ? Time.time - hackInicio : -1f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            kv["oleadasCentro"] = Csv(oleadasLanzadas);
            kv["oleadasCiudad"] = Csv(lanzadasCiudad);
            for (int i = 0; i < puestos.Length; i++)
            {
                var p = puestos[i];
                // WP9a: claves puestoOeste / puestoEste (volado y mecha restante).
                kv["puesto" + Capital(p.lado)] = $"volado={(p.carga != null && p.carga.EstaVolada)};mecha={(p.carga != null ? p.carga.MechaRestante : 0f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)};guardiasVivos={Vivos(p.guardias)}";
            }
            if (computadora != null) kv["computadora"] = $"progreso={computadora.Progreso01.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)};completo={computadora.Completo};operador={(computadora.Operador != null ? computadora.Operador.DisplayName : "-")}";
            CapturarMando(kv);
            kv["cuartelVivos"] = Vivos(enemigosCuartel).ToString();
            kv["torresCaidas"] = IndicesCaidos(torres);
            kv["reflectoresRotos"] = IndicesRotos(reflectores);
            if (heli != null) kv["heli"] = $"activo={heli.gameObject.activeSelf};pos={heli.position.x:0.0},{heli.position.y:0.0},{heli.position.z:0.0};aterrizo={HeliAterrizo}";
        }

        static string Capital(string t) => string.IsNullOrEmpty(t) ? "" : char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();

        static string IndicesCaidos(TorreDestruible[] lista)
        {
            var r = new List<int>();
            if (lista != null) for (int i = 0; i < lista.Length; i++) if (lista[i] != null && lista[i].EstaCaida) r.Add(i);
            return string.Join(",", r);
        }

        static string IndicesRotos(ReflectorVigia[] lista)
        {
            var r = new List<int>();
            if (lista != null) for (int i = 0; i < lista.Length; i++) if (lista[i] != null && lista[i].EstaRoto) r.Add(i);
            return string.Join(",", r);
        }

        static float F(Dictionary<string, string> kv, string k, float def = 0f)
            => kv.TryGetValue(k, out var s) && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

        public void RestaurarEstado(Dictionary<string, string> kv)
        {
            if (!kv.TryGetValue("fase", out var fs) || !Enum.TryParse(fs, out FaseOperacion fase)) return;
            // SaltarA deja hechas todas las fases anteriores (cuartel limpio, puestos abiertos...) sin mover a nadie.
            SaltarA(fase, false);
            if (fase == FaseOperacion.Puestos)
            {
                for (int i = 0; i < puestos.Length; i++)
                {
                    var p = puestos[i];
                    if (p.carga == null || !kv.TryGetValue("puesto" + Capital(p.lado), out var ps)) continue;
                    bool volado = ps.Contains("volado=True");
                    float mecha = 0f;
                    int im = ps.IndexOf("mecha=", StringComparison.Ordinal);
                    if (im >= 0)
                    {
                        string t = ps.Substring(im + 6); int fin = t.IndexOf(';'); if (fin >= 0) t = t.Substring(0, fin);
                        float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mecha);
                    }
                    if (volado) { p.volado = true; p.carga.VolarYa(); plantadas++; }
                    else if (mecha > 0f) { p.carga.RestaurarMecha(mecha); plantadas++; }
                }
                NavService.Invalidate();
                NavMeshViva.Solicitar();
                Coberturas.Registrar();
                PuestoActual = ContarVolados();
                BalizaDelPuesto();
            }
            if (fase == FaseOperacion.CentroDeDatos && computadora != null)
            {
                computadora.ResetearProgreso();
                float segHack = F(kv, "hackSegundos", 0f) > 0f ? F(kv, "hackSegundos") : 0f;
                if (kv.TryGetValue("computadora", out var cs))
                {
                    int ip = cs.IndexOf("progreso=", StringComparison.Ordinal);
                    if (ip >= 0)
                    {
                        string t = cs.Substring(ip + 9); int fin = t.IndexOf(';'); if (fin >= 0) t = t.Substring(0, fin);
                        if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pr)) segHack = Mathf.Max(segHack, pr * computadora.duracion);
                    }
                }
                computadora.Avanzar(Mathf.Min(computadora.duracion * 0.999f, segHack));
                hackInicio = -1f;
                if (kv.TryGetValue("oleadasCentro", out var oc)) LeerCsv(oc, oleadasLanzadas);
            }
            if (fase == FaseOperacion.Huir && kv.TryGetValue("enTanque", out var et) && et == "True")
            {
                Embarcar();
                indiceRuta = Mathf.Clamp((int)F(kv, "indiceRuta"), 0, Mathf.Max(0, rutaDelTanque.Length - 1));
                EmitirOrdenDeRuta();
            }
            else if (fase == FaseOperacion.Huir && (int)F(kv, "subfase") > 0)
            {
                // WP9b: vuelve al tramo del jefe donde estaba (el jefe con su vida, la reparacion con su progreso).
                int sf = (int)F(kv, "subfase");
                EntrarSubfase(sf);
                if (sf == (int)SubfaseHuida.Jefe && tanque != null) tanque.PonerVida01(F(kv, "jefeVida", 1f));
                if (sf == (int)SubfaseHuida.Reparacion && reparacion != null) reparacion.FijarProgreso(F(kv, "reparacion"));
            }
            if (fase == FaseOperacion.Resistir || fase == FaseOperacion.Huir)
            {
                if (kv.TryGetValue("oleadasCiudad", out var ow)) LeerCsv(ow, lanzadasCiudad);
            }
            // WP8 (#078): las torres caidas y los reflectores rotos del cuartel se reponen sin animacion.
            if (kv.TryGetValue("torresCaidas", out var tc) && !string.IsNullOrEmpty(tc) && torres != null)
                foreach (var p in tc.Split(',')) if (int.TryParse(p, out var ti) && ti >= 0 && ti < torres.Length && torres[ti] != null) torres[ti].CaerYa();
            if (kv.TryGetValue("reflectoresRotos", out var rr) && !string.IsNullOrEmpty(rr) && reflectores != null)
                foreach (var p in rr.Split(',')) if (int.TryParse(p, out var ri) && ri >= 0 && ri < reflectores.Length && reflectores[ri] != null) reflectores[ri].RomperYa();
            Reloj = F(kv, "reloj");
            Subfase = (int)F(kv, "subfase");
            AutosDestruidos = (int)F(kv, "autosDestruidos");
            if (heli != null && kv.TryGetValue("heli", out var hs) && fase == FaseOperacion.Resistir)
            {
                // el helicoptero vuela segun el reloj de la fase: ya queda donde estaba
                VolarHeli(Mathf.Clamp01(Reloj / segundosDeResistencia));
            }
            RestaurarMando(kv);
            SesionLog.Evento($"Operacion restaurada: fase={fase} reloj={Reloj:0.0} puesto={PuestoActual + 1}");
        }

        // ---------------------------------------------------------------
        // Pruebas y capturas: salta a una fase dejando hecho todo lo anterior.
        // ---------------------------------------------------------------
        public void SaltarA(FaseOperacion destino, bool teletransportar = true, int subfase = 0)
        {
            StopAllCoroutines();
            LiberarAutoguardadoCortado();   // P10
            FinalAbortar();
            if (Indice(destino) > Indice(FaseOperacion.Infiltrar))
            {
                foreach (var s in enemigosCuartel) if (s != null) s.gameObject.SetActive(false);
                if (portonCuartel != null) { portonCuartel.SetActive(false); }
            }
            if (Indice(destino) > Indice(FaseOperacion.Puestos))
            {
                foreach (var p in puestos) DejarPuestoVolado(p);
                ApagarMuralla();
                PuestoActual = puestos.Length;
            }
            else if (destino == FaseOperacion.Puestos) PuestoActual = 0;
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            if (teletransportar && Application.isPlaying) PosesionInicial();   // #110: el que manejas al empezar un objetivo es Kes (Smg)
            int iDestino = Indice(destino);
            if (teletransportar && iDestino < entradas.Length && entradas[iDestino] != null) TeletransportarEscuadra(entradas[iDestino].position, entradas[iDestino].rotation);
            if (destino == FaseOperacion.Extraer)
            {
                // Atajo de pruebas: el helicoptero ya esta posado en la plaza.
                ArrancarHeli();
                heli.position = heliAterrizaje.position;
                if (ScriptHeli != null) ScriptHeli.Volando = false;
                HeliAterrizo = true;
                if (CoberturaEnSaltoDePrueba) PonerHeliEnEstacionario();   // #127: solo si la prueba lo pide, el helicoptero queda en estacionario cubriendo
            }
            EntrarFase(destino);
            if (subfase > 0)
            {
                // WP10: Arrancar(5, _, 1) deja la radio tomada y la escuadra ya ubicada en A/B/C (atajo de pruebas).
                SaltoDePrueba = destino == FaseOperacion.Resistir;
                EntrarSubfase(subfase);
                SaltoDePrueba = false;
                if (destino == FaseOperacion.Huir && teletransportar) AcomodarEscuadraHuida(subfase);
            }
        }

        // Prueba por CLI (OperacionPrueba): arranca en el puesto 'indice' (0 Oeste, 1 Este). Los anteriores ya estan volados y la escuadra queda
        // 8 m al sur de su punto de carga, mirando al norte.
        public void SaltarAPuesto(int indice)
        {
            indice = Mathf.Clamp(indice, 0, puestos.Length - 1);
            SaltarA(FaseOperacion.Puestos, true);
            for (int i = 0; i < indice; i++) DejarPuestoVolado(puestos[i]);
            PuestoActual = ContarVolados();
            NavService.Invalidate();
            NavMeshViva.Solicitar();
            Coberturas.Registrar();
            var carga = puestos[indice].carga;
            if (carga != null)
                TeletransportarEscuadra(carga.transform.position + Vector3.back * 8f, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            BalizaDelPuesto();
        }

        // #110: "al inicio de la partida el jugador seleccionado sea de disparo continuo": Kes (Flanqueador, Smg). Vega sigue en la
        // escuadra con el fusil. Es el mismo criterio que PlayerInputDriver.SoldadoInicial (arranque normal); aca cubre los saltos de prueba.
        void PosesionInicial()
        {
            var drv = PlayerInputDriver.Activo;
            if (drv == null || drv.Brain == null) return;
            var yo = drv.Brain.Current;
            if (yo != null && yo.Role == RoleType.Flanker) return;
            if (yo != null && drv.Vehicle != null && drv.Vehicle.RoleOf(yo) != null) return;   // a bordo de un vehiculo no se toca
            Soldier kes = null;
            foreach (var s in Escuadra()) if (s.Role == RoleType.Flanker) { kes = s; break; }
            if (kes != null) drv.TryPossess(kes);
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
