using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // Fase 5 (RESISTIR), WP10 (#101): "DIRIGIR LA DEFENSA DESDE LA RADIO". El objetivo solo se gana coordinando desde la vista tactica.
    //   Subfase 0  llegada: la escuadra entra a la ciudad y va a la radio del ayuntamiento ([E] TOMAR LA RADIO). No pasa nada hasta tomarla.
    //   Subfase 1  radio tomada: la camara pasa a RTS UNA vez (sugerencia; P7 #120: ya no es obligatoria, la radio es un ROL: el operador sigue
    //              ahi como IA aunque el jugador vuelva a FPS o posea a otro), salen 4 milicianos (#122), tres sectores (A, B, C) y un tutorial guiado.
    //              El reloj del helicoptero ("reloj efectivo", 90 s) solo avanza si el equipo esta ubicado y cada sector amenazado tiene a alguien adentro.
    //              5 oleadas dirigidas a un sector, anunciadas 8 s antes con una flecha roja. Un sector amenazado vacio 6 s CAE: sus enemigos van a la radio y el
    //              reloj se frena hasta recuperarlo (2 aliados adentro 4 s). Los que llegan a un sector se cubren solos (#074).
    //   Final      a los 90 s efectivos el helicoptero aterriza, se suelta la radio y sigue Extraer (sin cambios).
    public partial class OperacionDirector
    {
        [Header("5b. Mando tactico (WP10, #101)")]
        public Transform radioDeCampana;                 // la radio, en las escalinatas del ayuntamiento
        public SectorDeDefensa[] sectoresDeDefensa;      // A calle norte, B mercado oeste, C calle este
        public GameObject[] prefabsDeMiliciano;          // 0 asalto, 1 medico (se instancian al tomar la radio)
        public Transform[] salidasDeMiliciano;           // de donde salen del ayuntamiento
        public Transform campanario;                     // centro de la plataforma del campanario (opcional)

        // Pruebas heredadas (ValidacionBugs, Bug075): el flujo de antes del WP10, sin radio ni sectores.
        public static bool ResistirSinMando;

        public bool MandoActivo => !ResistirSinMando && radioDeCampana != null && sectoresDeDefensa != null && sectoresDeDefensa.Length > 0;

        public const int PasosDelMando = 6;
        public const int CantidadDeOleadas = 5;
        public const float AlturaDelCampanario = 6.5f, RadioDelCampanario = 3.4f;
        public const float AlcanceExtraDelCampanario = 1.35f;
        public const float AlturaDeLaVistaTactica = 66f, AnguloDeLaVistaTactica = 40f;

        // ---- Estado del mando ----
        public float RelojEfectivo { get; private set; }
        public bool RelojDelMandoCorre { get; private set; }
        public string MotivoDeLaPausa { get; private set; } = "";
        public bool SaltoDePrueba { get; private set; }
        public Soldier UnidadA { get; private set; }
        public Soldier UnidadC { get; private set; }
        public Soldier MedicoDeApoyo { get; private set; }
        public IReadOnlyList<Soldier> Milicianos => milicianosActivos;
        public bool[] PasoHecho => pasoHecho;
        public int PasoActualDelMando { get; private set; } = -1;
        public bool CampanarioOcupado { get; private set; }
        public int SectoresCaidosTotales { get; private set; }
        public int CoberturasAutomaticas { get; private set; }
        public int OleadasDelMandoLanzadas { get { int n = 0; foreach (var o in oleadasMando) if (o.lanzada) n++; return n; } }
        public int OleadasDelMandoAnunciadas { get { int n = 0; foreach (var o in oleadasMando) if (o.anunciada) n++; return n; } }
        public IReadOnlyList<OleadaDelMando> OleadasDelMando => oleadasMando;

        readonly bool[] pasoHecho = new bool[PasosDelMando];
        readonly List<Soldier> milicianosActivos = new List<Soldier>();
        readonly Dictionary<int, int> ordenSector = new Dictionary<int, int>();     // id del soldado -> sector del ultimo destino ordenado (-1 = ninguno)
        readonly Dictionary<Soldier, int> destinoDeOleada = new Dictionary<Soldier, int>();
        readonly List<OleadaDelMando> oleadasMando = new List<OleadaDelMando>();
        IDisposable subOrdenes;
        bool subAtaque;
        float proximaCobertura, proximoRumbo;
        bool ordenDeAtaqueAVehiculo, hubieronCamionetas;
        float soltoLaRadioHasta;
        GameObject[] marcasDeGuia = new GameObject[0];
        readonly List<SelectionRingFx> anillosDeGuia = new List<SelectionRingFx>();
        float tiempoDeLlegada, proximoHud;
        public bool ForzarHud { get; set; }

        public sealed class OleadaDelMando
        {
            public int numero;                 // 1..5
            public int[] sectores;             // indices de sector
            public int[] cantidades;
            public int camionetas;             // 0 o 1 (sale por el sector indicado en "sectorDeCamioneta")
            public int sectorDeCamioneta = -1;
            public float fraccion;             // del reloj efectivo total
            public bool anunciada, lanzada;
            public float segundosDesdeLanzada;
            public readonly List<Soldier> soldados = new List<Soldier>();
            public readonly List<OperacionAuto> vehiculos = new List<OperacionAuto>();
            public float TiempoDeLanzamiento(float total) => total * fraccion;
            public int Vivos { get { int n = 0; foreach (var s in soldados) if (s != null && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive) n++; return n; } }
            public int Pendientes(OperacionDirector d) { int n = 0; foreach (var s in soldados) if (s != null && !s.gameObject.activeSelf) n++; return n; }
            public int VehiculosVivos { get { int n = 0; foreach (var v in vehiculos) if (v != null && !v.Muerto) n++; return n; } }
        }

        // Plan de las 5 oleadas: A, B, C (con camioneta), A+C, B (con camioneta). Fraccion = cuando SALE sobre el reloj efectivo total; se anuncia 8 s antes.
        static readonly (int[] sectores, int[] cantidades, int camionetas, int sectorCamioneta, float fraccion)[] PlanDeOleadas =
        {
            (new[] { 0 }, new[] { 6 }, 0, -1, 0.13f),
            (new[] { 1 }, new[] { 6 }, 0, -1, 0.31f),
            (new[] { 2 }, new[] { 7 }, 1, 2, 0.50f),
            (new[] { 0, 2 }, new[] { 5, 5 }, 0, -1, 0.68f),
            (new[] { 1 }, new[] { 8 }, 1, 1, 0.84f),
        };

        // Salida y parada de la camioneta de cada sector (relativas a la plaza; siempre sobre una calle libre).
        static readonly (Vector2 salida, Vector2 parada)[] CallesDeCamioneta =
        {
            (new Vector2(-60f, 68f), new Vector2(-3f, 68f)),      // A: por la calle norte, desde el oeste
            (new Vector2(-56f, 66f), new Vector2(-55f, -2f)),     // B: por la calle oeste, desde el norte
            (new Vector2(58f, -60f), new Vector2(57f, 3f)),      // C: por la calle este, desde el sur
        };

        [Serializable] struct Vacio { }

        // ------------------------------------------------------------------ entrada de la fase
        // Al salir de la fase (a Extraer, Derrota o un salto de prueba): suelta la radio sin tocar la camara y quita todo lo del mando.
        void AbandonarElMando()
        {
            if (MandoTactico.RadioTomada) SoltarLaRadio("", false);
            LimpiarElMando();
            MandoTactico.PosicionDeLaRadio = null;
            CameraRig.LimiteDePaneoRts = null;
            operadorPendiente = null;
        }

        void IniciarLlegadaAlMando()
        {
            if (MandoTactico.RadioTomada) SoltarLaRadio("", false);
            LimpiarElMando();
            RelojEfectivo = 0f; RelojDelMandoCorre = false; MotivoDeLaPausa = "";
            oleadasMando.Clear(); destinoDeOleada.Clear(); QuitarMilicianos();
            for (int i = 0; i < pasoHecho.Length; i++) pasoHecho[i] = false;
            ordenSector.Clear();
            ordenDeAtaqueAVehiculo = false; hubieronCamionetas = false; CampanarioOcupado = false; CoberturasAutomaticas = 0; SectoresCaidosTotales = 0;
            tiempoDeLlegada = 0f;
            MandoTactico.PrimerIngresoHecho = false; eSostenida = 0f;
            if (sectoresDeDefensa != null)
                foreach (var s in sectoresDeDefensa)
                    if (s != null) { s.Amenazado = false; s.Caido = false; s.AliadosDentro = 0; s.SegundosVacio = 0f; s.SegundosRecuperando = 0f; s.Mostrar(false); }
            MandoTactico.PosicionDeLaRadio = radioDeCampana != null ? radioDeCampana.position : (Vector3?)null;
            operadorPendiente = null;
            if (radioDeCampana != null)
                PonerBaliza("RADIO DE CAMPAÑA", new Color(1f, 0.82f, 0.25f), radioDeCampana.position, null, 3.4f);
            PanelDeMando.Quitar();
            FlechaDeOleada.QuitarTodas();
        }

        // Se llama desde EntrarSubfase (OperacionDirector.Jefe.cs) cuando la fase es Resistir.
        void EntrarSubfaseResistir(int s)
        {
            if (ResistirSinMando) return;
            if (s == 1) IniciarMando(SaltoDePrueba);
        }

        // ------------------------------------------------------------------ llegada (subfase 0)
        void TickLlegadaAlMando(float dt)
        {
            tiempoDeLlegada += dt;
            var yo = Poseido();
            VolarHeli(0f);
            if (radioDeCampana == null) return;
            float d = yo != null ? Plano(yo.transform.position, radioDeCampana.position).magnitude : 999f;
            if (hud != null)
            {
                hud.Objetivo(TituloObjetivo(5, "DIRIGIR LA DEFENSA"), $"Anda a la radio del ayuntamiento · {Mathf.RoundToInt(d)} m · el helicoptero solo aterriza si coordinas la defensa", 0f, new Color(1f, 0.55f, 0.25f));
                hud.OcultarTimer();
            }
            if (yo != null && yo.Health.IsAlive && d <= MandoTactico.AlcanceDeLaRadio)
            {
                if (hud != null) hud.Prompt("[E] TOMAR LA RADIO · el helicóptero solo aterriza si coordinás la defensa");
                if (OperacionTerminal.EToque()) EntrarSubfase(1);
            }
        }

        // Para las pruebas: toma la radio como lo haria [E] (con el poseido).
        public void TomarLaRadioPorPrueba(bool ubicarSolos = false)
        {
            if (Fase != FaseOperacion.Resistir || Subfase != 0) return;
            SaltoDePrueba = ubicarSolos;
            EntrarSubfase(1);
            SaltoDePrueba = false;
        }

        // ------------------------------------------------------------------ subfase 1: la radio tomada
        void IniciarMando(bool ubicarSolos)
        {
            var yo = Poseido();
            if (yo == null) return;
            if (baliza != null) { baliza.Quitar(); baliza = null; }
            for (int i = 0; i < pasoHecho.Length; i++) pasoHecho[i] = false;
            ordenSector.Clear(); destinoDeOleada.Clear(); lanzadasCiudad.Clear(); pendientesCiudad.Clear(); bocaDeLaOla.Clear(); pendientesMando.Clear();
            RelojEfectivo = 0f;
            ArmarOleadasDelMando();
            if (sectoresDeDefensa != null)
            {
                for (int i = 0; i < sectoresDeDefensa.Length; i++)
                {
                    var s = sectoresDeDefensa[i];
                    if (s == null) continue;
                    s.Indice = i; s.Mostrar(true);
                    s.Amenazado = false; s.Caido = false; s.AliadosDentro = 0; s.SegundosVacio = 0f; s.SegundosRecuperando = 0f;
                }
            }
            ActivarMilicianos(PartidaGuardada.Restaurando);   // P10: al continuar no se anuncia una llegada que ya ocurrio
            AsignarUnidades(yo);
            SuscribirMando();
            TomarLaRadio(yo);
            CameraRig.LimiteDePaneoRts = LimiteDeLaZonaJugable;
            GuiaDeMando.Asegurar();   // #114: flecha 3D + punteado del paso actual
            var panelMando = PanelDeMando.Asegurar();
            panelMando.Mostrar(true);
            panelMando.AlSoltarRadio = () => SoltarLaRadio("RADIO ABANDONADA · EL RELOJ SE DETUVO", true);
            AlertQueue.Push("VISTA TÁCTICA · DIRIGÍ A TU EQUIPO", AlertPriority.Alta, 3.5f);
            SesionLog.Evento($"MANDO: radio tomada por {yo.DisplayName}; unidades A={(UnidadA != null ? UnidadA.DisplayName : "-")} C={(UnidadC != null ? UnidadC.DisplayName : "-")} milicianos={milicianosActivos.Count}");
            if (ubicarSolos) UbicarLaEscuadraSola();
        }

        void ArmarOleadasDelMando()
        {
            oleadasMando.Clear();
            for (int i = 0; i < PlanDeOleadas.Length; i++)
            {
                var p = PlanDeOleadas[i];
                oleadasMando.Add(new OleadaDelMando { numero = i + 1, sectores = p.sectores, cantidades = p.cantidades, camionetas = p.camionetas, sectorDeCamioneta = p.sectorCamioneta, fraccion = p.fraccion });
            }
        }

        // Quita a los milicianos de una partida anterior de la fase (si se re-arma el objetivo 5 no deben quedar vivos ni en MandoTactico.Milicianos).
        void QuitarMilicianos()
        {
            foreach (var s in milicianosActivos) if (s != null) { MandoTactico.Milicianos.Remove(s); s.gameObject.SetActive(false); Destroy(s.gameObject); }
            milicianosActivos.Clear();
            MandoTactico.Milicianos.Clear();
            ActorRegistry.Invalidate();
        }

        void ActivarMilicianos(bool silencioso = false)
        {
            QuitarMilicianos();
            if (prefabsDeMiliciano == null) return;
            // #122: 4 milicianos (asalto, medico, flanqueador, asalto) para que 3 de escuadra + 4 - el operador = 6 = 3 sectores x 2 aliados.
            string[] nombres = { "MILICIANO 1", "MILICIANO 2", "MILICIANO 3", "MILICIANO 4" };
            RoleType[] roles = { RoleType.Assault, RoleType.Medic, RoleType.Flanker, RoleType.Assault };
            for (int i = 0; i < prefabsDeMiliciano.Length && i < MandoTactico.MilicianosAActivar; i++)
            {
                if (prefabsDeMiliciano[i] == null) continue;
                var punto = salidasDeMiliciano != null && i < salidasDeMiliciano.Length && salidasDeMiliciano[i] != null ? salidasDeMiliciano[i].position : (radioDeCampana != null ? radioDeCampana.position + new Vector3((i % 2 == 0 ? -6f : 6f) * (1 + i / 2), 0f, 3f) : plaza.position);
                if (UnityEngine.AI.NavMesh.SamplePosition(punto, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) punto = h.position;
                var rot = Quaternion.LookRotation(radioDeCampana != null ? Plano(radioDeCampana.position, punto).normalized : Vector3.back, Vector3.up);
                var go = Instantiate(prefabsDeMiliciano[i], new Vector3(punto.x, 0.8f, punto.z), rot);
                go.name = "Miliciano_" + (i + 1);
                go.SetActive(true);
                var s = go.GetComponent<Soldier>();
                if (s == null) { Destroy(go); continue; }
                s.Configure(nombres[i], TeamId.Player, roles[i], 100);
                if (s.Brain != null) { s.Brain.IsPossessedByPlayer = false; s.Brain.Atrincherado = false; s.Brain.Quieto = false; s.Brain.Pasivo = false; s.Brain.ReactivarNavegacion(); s.Brain.CancelOrder(); }
                ApoyoEnElPiso.Apoyar(s.transform);
                // Armas livianas: subfusil en la ranura principal (el medico lleva ademas su botiquin).
                var w = s.Weapon;
                if (w != null && w.Loadout.Count > 0) { w.Loadout[0] = WeaponKind.Smg; w.EquipFromLoadout(0); w.ReponerMunicion(); }
                MandoTactico.Milicianos.Add(s);
                milicianosActivos.Add(s);
            }
            ActorRegistry.Invalidate();
            // #115: las filas del roster de los milicianos y el aviso de llegada (pulso en cada uno, voz de radio).
            if (RosterView.Activo != null) RosterView.Activo.Rebuild();
            if (milicianosActivos.Count > 0 && !silencioso)
            {
                AlertQueue.Push($"LLEGARON {milicianosActivos.Count} MILICIANOS", AlertPriority.Alta, 3.2f);
                AudioDirector.PlayUi2D(SfxKind.Order, 0.7f, 0.9f);
                AudioDirector.PlayVoice2D(SfxKind.OrderBark, 0.8f, 0.85f);
                foreach (var m in milicianosActivos)
                    if (m != null) Feedback.Accion(SfxKind.Select, "MILICIA", m.transform.position, Feedback.Info, aviso: false, pulso: true, volumen: 0.3f);
            }
        }

        static int PrioridadDeRol(Soldier s) => s.Role == RoleType.Flanker || s.Role == RoleType.Sniper ? 0 : s.Role == RoleType.Assault ? 1 : s.Role == RoleType.Medic ? 3 : 2;

        void AsignarUnidades(Soldier operador)
        {
            var cand = Escuadra(true);
            cand.Remove(operador);
            cand.Sort((a, b) => PrioridadDeRol(a).CompareTo(PrioridadDeRol(b)));
            UnidadA = cand.Count > 0 ? cand[0] : null;
            UnidadC = cand.Count > 1 ? cand[1] : null;
            MedicoDeApoyo = cand.Find(x => x.Role == RoleType.Medic);
            if (MedicoDeApoyo == null) foreach (var m in milicianosActivos) if (m != null && m.Role == RoleType.Medic) { MedicoDeApoyo = m; break; }
        }

        void SuscribirMando()
        {
            DesuscribirMando();
            subOrdenes = EventBus.Instance.Subscribe<MoveOrderIssuedEvent>(AlOrdenarMovimiento);
            OrderService.AtaqueAVehiculoOrdenado += AlOrdenarAtaqueAVehiculo;
            subAtaque = true;
        }

        void DesuscribirMando()
        {
            if (subOrdenes != null) { subOrdenes.Dispose(); subOrdenes = null; }
            if (subAtaque) { OrderService.AtaqueAVehiculoOrdenado -= AlOrdenarAtaqueAVehiculo; subAtaque = false; }
        }

        void AlOrdenarMovimiento(MoveOrderIssuedEvent e)
        {
            if (Fase != FaseOperacion.Resistir || sectoresDeDefensa == null) return;
            int idx = -1;
            for (int i = 0; i < sectoresDeDefensa.Length; i++) if (sectoresDeDefensa[i] != null && sectoresDeDefensa[i].Contiene(e.Destination, 1.5f)) { idx = i; break; }
            ordenSector[e.ActorId] = idx;
        }

        void AlOrdenarAtaqueAVehiculo(IReadOnlyList<Soldier> seleccion, Vehicle v)
        {
            if (Fase == FaseOperacion.Resistir) ordenDeAtaqueAVehiculo = true;
        }

        // ------------------------------------------------------------------ la radio
        Vector3 PuestoDelOperador()
        {
            var p = radioDeCampana.position - radioDeCampana.forward * 1.5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 2.5f, UnityEngine.AI.NavMesh.AllAreas)) p = h.position;
            return p;
        }

        void TomarLaRadio(Soldier yo)
        {
            if (radioDeCampana == null || yo == null) return;
            var drv = PlayerInputDriver.Activo;
            var posAntes = yo.transform.position;
            var pos = PuestoDelOperador();
            yo.transform.position = new Vector3(pos.x, yo.transform.position.y, pos.z);
            var mira = radioDeCampana.position - yo.transform.position; mira.y = 0f;
            if (mira.sqrMagnitude > 0.01f) yo.transform.rotation = Quaternion.LookRotation(mira.normalized, Vector3.up);
            ApoyoEnElPiso.Apoyar(yo.transform);
            if (yo.Brain != null) { yo.Brain.CancelOrder(); yo.Brain.IsPossessedByPlayer = true; }
            if (yo.Motor != null) yo.Motor.SetCrouching(true);
            MandoTactico.Establecer(yo);
            // P7 (#120b): la vista tactica es solo una SUGERENCIA de la primera vez (con el aviso de como volver a FPS); si se retoma la
            // radio despues, la camara se queda como esta.
            if (drv != null && drv.Rig != null && !MandoTactico.PrimerIngresoHecho)
            {
                MandoTactico.PrimerIngresoHecho = true;
                // #121: la camara tactica nace donde estaba el soldado (no en el centro de los sectores). Si ya hubo una vista RTS se
                // conservan su giro, inclinacion y zoom; la primera vez se usa el picado tactico (norte arriba, 40 grados, 66 m).
                var foco = posAntes;
                var rig = drv.Rig;
                bool habiaVista = rig.TieneVistaRtsGuardada;
                rig.SetMode(ControlMode.Rts, foco);
                if (!habiaVista)
                {
                    rig.rtsYaw = 0f;
                    rig.rtsInclinacionAdelante = AnguloDeLaVistaTactica;
                    rig.SetRtsView(foco);
                    rig.CentrarRtsEn(foco, (AlturaDeLaVistaTactica - 18f) / 2.4f);
                }
                else rig.CentrarRtsEn(foco, 0f);
                if (drv.Selection != null) drv.Selection.Clear();
                AlertQueue.Push(MandoTactico.TextoDeTabLibre, AlertPriority.Alta, 5f);
            }
            OrderService.ManejadoAMano = null;
        }

        // El foco: el centro de los tres sectores y la radio (la plaza queda al medio).
        Vector3 FocoDeLaVistaTactica()
        {
            var c = Vector3.zero; int n = 0;
            if (sectoresDeDefensa != null) foreach (var s in sectoresDeDefensa) if (s != null) { c += s.Centro; n++; }
            if (radioDeCampana != null) { c += radioDeCampana.position; n++; }
            if (n == 0) return plaza != null ? plaza.position : Vector3.zero;
            c /= n; c.y = 0f;
            c.z -= 8f;   // la camara mira hacia el norte con picado: el sector A (el mas al norte) queda arriba y B y C no se pegan al borde de abajo
            return c;
        }

        void SoltarLaRadio(string motivo, bool pasoAFps)
        {
            var op = MandoTactico.Operador;
            MandoTactico.Soltar();
            if (op != null)
            {
                AccionesEnCurso.Terminar(op);
                if (op.Motor != null) op.Motor.SetCrouching(false);
            }
            var drv = PlayerInputDriver.Activo;
            if (op != null && op.Brain != null && !op.Brain.IsPossessedByPlayer) op.Brain.Quieto = false;   // el operador IA vuelve a la normalidad
            if (pasoAFps && drv != null && drv.Rig != null && op != null && op.Health.IsAlive && drv.Brain != null && drv.Brain.Current == op)
            {
                drv.Rig.SetMode(ControlMode.Fps);
                drv.Rig.ResetPitch();
                drv.ReclamarControl(op);
            }
            soltoLaRadioHasta = Time.time + 0.4f;
            if (hud != null && !string.IsNullOrEmpty(motivo)) hud.Aviso(motivo, 3f);
            SesionLog.Evento("MANDO: radio soltada (" + motivo + ")");
        }

        // ------------------------------------------------------------------ #119: operar la radio por orden
        // Zona jugable de la defensa (la camara tactica no sale de ahi, #116): la ciudad y sus calles.
        public static readonly Rect LimiteDeLaZonaJugable = new Rect(258f, 158f, 128f, 146f);
        Soldier operadorPendiente;
        float operadorPendienteHasta;
        public Soldier OperadorPendiente => operadorPendiente;
        public const float SegundosParaLlegarALaRadio = 40f;
        public const float DistanciaParaOperarDeInmediato = 3f;

        // El jugador (en RTS) manda a alguien a operar la radio: si ya esta a <= 3 m la toma en el acto, si no camina hasta el puesto y la
        // toma al llegar. El que la operaba (si hay alguien) queda libre: es el relevo explicito. Devuelve false con el motivo si no se pudo.
        public bool OrdenarOperarLaRadio(IReadOnlyList<Soldier> seleccion, out string motivo)
        {
            motivo = null;
            if (Fase != FaseOperacion.Resistir || Subfase < 1 || radioDeCampana == null) { motivo = "LA RADIO NO ESTA LISTA TODAVIA"; return false; }
            Soldier elegido = null; bool soloOperador = false;
            if (seleccion != null)
                foreach (var s in seleccion)
                {
                    if (s == null || s.Health == null || !s.Health.IsAlive || s.Team != TeamId.Player || !s.gameObject.activeInHierarchy) continue;
                    if (MandoTactico.EsOperadorDeRadio(s)) { soloOperador = true; continue; }
                    if (elegido == null || Plano(s.transform.position, radioDeCampana.position).sqrMagnitude < Plano(elegido.transform.position, radioDeCampana.position).sqrMagnitude) elegido = s;
                }
            if (elegido == null) { motivo = soloOperador ? "YA ESTA OPERANDO LA RADIO" : "NADIE VIVO PARA OPERAR LA RADIO"; return false; }
            var puesto = PuestoDelOperador();
            if (Plano(elegido.transform.position, puesto).magnitude <= DistanciaParaOperarDeInmediato) { RelevarOperador(elegido); return true; }
            operadorPendiente = elegido; operadorPendienteHasta = Time.time + SegundosParaLlegarALaRadio;
            OrderService.IssueMoveOrder(elegido, puesto);
            AlertQueue.Push($"{elegido.DisplayName.ToUpperInvariant()} VA A OPERAR LA RADIO", AlertPriority.Media, 2.2f);
            SesionLog.Evento("MANDO: " + elegido.DisplayName + " va a operar la radio");
            return true;
        }

        void TickOperadorPendiente(float dt)
        {
            var s = operadorPendiente;
            if (s == null) return;
            if (s.Health == null || !s.Health.IsAlive || Time.time > operadorPendienteHasta || MandoTactico.EsOperadorDeRadio(s)) { operadorPendiente = null; return; }
            if (Plano(s.transform.position, PuestoDelOperador()).magnitude <= 2.2f) { operadorPendiente = null; RelevarOperador(s); }
        }

        // El soldado "nuevo" pasa a operar la radio; el anterior, si lo habia, queda libre (sigue en RTS, sin cambiar de camara ni de poseido).
        public void RelevarOperador(Soldier nuevo)
        {
            if (nuevo == null || radioDeCampana == null) return;
            var viejo = MandoTactico.Operador;
            if (viejo != null && !ReferenceEquals(viejo, nuevo)) SoltarLaRadio("", false);
            var pos = PuestoDelOperador();
            nuevo.transform.position = new Vector3(pos.x, nuevo.transform.position.y, pos.z);
            var mira = radioDeCampana.position - nuevo.transform.position; mira.y = 0f;
            if (mira.sqrMagnitude > 0.01f) nuevo.transform.rotation = Quaternion.LookRotation(mira.normalized, Vector3.up);
            ApoyoEnElPiso.Apoyar(nuevo.transform);
            if (nuevo.Brain != null) { nuevo.Brain.CancelOrder(); nuevo.Brain.Quieto = true; }
            if (nuevo.Motor != null) nuevo.Motor.SetCrouching(true);
            MandoTactico.Establecer(nuevo);
            operadorPendiente = null;
            AlertQueue.Push($"{nuevo.DisplayName.ToUpperInvariant()} OPERA LA RADIO", AlertPriority.Alta, 2.4f);
            SesionLog.Evento("MANDO: radio operada por " + nuevo.DisplayName + " (relevo)");
        }

        // ------------------------------------------------------------------ tick del mando (subfase 1)
        void TickMando(float dt)
        {
            var yo = Poseido();

            // --- la radio (P7: un rol; el operador puede ser el jugador o un aliado de IA) ---
            if (MandoTactico.RadioTomada && !MandoTactico.EnRadio) SoltarLaRadio("¡SE PERDIÓ LA RADIO! · EL OPERADOR CAYÓ", false);
            TickOperadorPendiente(dt);
            var panelRadio = PanelDeMando.Instancia;
            if (panelRadio != null) panelRadio.MostrarBotonSoltar(MandoTactico.EnRadio);
            var rigActual = PlayerInputDriver.Activo != null ? PlayerInputDriver.Activo.Rig : null;
            bool enRts = rigActual != null && rigActual.Mode == ControlMode.Rts;
            if (MandoTactico.EnRadio)
            {
                var op = MandoTactico.Operador;
                bool loManejoYo = op.Brain != null && op.Brain.IsPossessedByPlayer;
                // Operador de IA ("OPERANDO LA RADIO"): quieto en su puesto, sin ordenes pendientes.
                if (op.Brain != null)
                {
                    if (loManejoYo) { if (op.Brain.Quieto) op.Brain.Quieto = false; }
                    else
                    {
                        if (!op.Brain.Quieto) op.Brain.Quieto = true;
                        if (op.Brain.TieneOrden) op.Brain.CancelOrder();
                    }
                }
                // El operador sigue en su puesto, de rodillas, tecleando (la animacion Operar sale del reporte de la accion).
                if (Plano(op.transform.position, radioDeCampana.position).magnitude > 3.2f) { var pos = PuestoDelOperador(); op.transform.position = new Vector3(pos.x, op.transform.position.y, pos.z); }
                if (op.Motor != null && !op.Motor.IsCrouching) op.Motor.SetCrouching(true);
                AccionesEnCurso.Reportar(op, "OPERANDO LA RADIO", radioDeCampana.position, 0f, 0f, radioDeCampana);
                // #120b/#124: en RTS la radio se suelta con el boton del panel. A pie (FPS, poseyendo al operador) se suelta MANTENIENDO [E] 1 s
                // (un toque no hace nada); revivir a un caido al alcance tiene prioridad sobre soltar.
                if (!enRts && loManejoYo) TickSoltarConE(dt);
                else eSostenida = 0f;
            }
            else
            {
                eSostenida = 0f;
                if (yo != null && yo.Health.IsAlive && Time.time >= soltoLaRadioHasta && radioDeCampana != null && Plano(yo.transform.position, radioDeCampana.position).magnitude <= MandoTactico.AlcanceDeLaRadio)
                {
                    if (hud != null) hud.Prompt("[E] RETOMAR LA RADIO · sin la radio el reloj no corre");
                    if (OperacionTerminal.EToque()) { TomarLaRadio(yo); }
                }
            }

            // --- amenazas, conteos, caidas ---
            ActualizarAmenazas(dt);
            ContarAliadosPorSector();
            TickPasosDelMando();
            ActualizarAnillosDeGuia();
            TickCaidaDeSectores(dt);

            // --- reloj ---
            int hechos = 0; for (int i = 0; i < MandoTactico.PasosQueBloqueanElReloj; i++) if (pasoHecho[i]) hechos++;
            int vaciosAmenazados = 0; bool algunCaido = false;
            if (sectoresDeDefensa != null)
                foreach (var s in sectoresDeDefensa)
                {
                    if (s == null) continue;
                    if (s.Caido) algunCaido = true;
                    if (s.Amenazado && s.AliadosDentro == 0) vaciosAmenazados++;
                }
            RelojDelMandoCorre = MandoTactico.RelojCorre(hechos, MandoTactico.EnRadio, algunCaido, vaciosAmenazados);
            MotivoDeLaPausa = MandoTactico.MotivoDePausa(hechos, MandoTactico.EnRadio, algunCaido, vaciosAmenazados);
            if (RelojDelMandoCorre) RelojEfectivo += dt;

            TickOleadasDelMando(dt);
            TickPendientesDelMando();
            TickRumboDeLosAtacantes(dt);
            TickCoberturaAutomatica(dt);
            TickCampanario();
            VolarHeli(Mathf.Clamp01(RelojEfectivo / Mathf.Max(1f, segundosDeResistencia)));
            ActualizarHudDelMando();

            if (RelojEfectivo >= segundosDeResistencia) FinalizarElMando();
        }

        // #120b: [E] mantenida 1 s suelta la radio (FPS, el jugador es el operador). Publico para los checks.
        float eSostenida;
        public float ESostenidaParaSoltar => eSostenida;
        void TickSoltarConE(float dt)
        {
            var drv = PlayerInputDriver.Activo;
            bool caidoAlAlcance = drv != null && drv.FindNearestDownedAlly() != null;
            if (MandoTactico.SoltarConToque && OperacionTerminal.EToque()) { SoltarLaRadio("RADIO ABANDONADA · EL RELOJ SE DETUVO", true); return; }   // comportamiento de antes (solo checks)
            if (OperacionTerminal.EMantenida() && !caidoAlAlcance)
            {
                eSostenida += Mathf.Min(dt, 0.1f);
                if (hud != null) hud.Prompt($"SOLTANDO LA RADIO... {Mathf.Clamp01(eSostenida / MandoTactico.SegundosParaSoltarLaRadio) * 100f:0} %");
                if (eSostenida >= MandoTactico.SegundosParaSoltarLaRadio)
                {
                    eSostenida = 0f;
                    SoltarLaRadio("RADIO ABANDONADA · EL RELOJ SE DETUVO", true);
                }
            }
            else
            {
                eSostenida = 0f;
                if (hud != null && !caidoAlAlcance) hud.Prompt(MandoTactico.TextoDeMantenerParaSoltar + " · el reloj del helicóptero se detiene");
            }
        }

        // ---- sectores amenazados: por una oleada anunciada (hasta que salgan y mueran todos, o 50 s) ----
        void ActualizarAmenazas(float dt)
        {
            if (sectoresDeDefensa == null) return;
            foreach (var s in sectoresDeDefensa) if (s != null) s.Amenazado = false;
            foreach (var o in oleadasMando)
            {
                if (!o.anunciada) continue;
                if (o.lanzada) o.segundosDesdeLanzada += dt;
                bool viva = !o.lanzada || ((o.Vivos > 0 || o.Pendientes(this) > 0 || o.VehiculosVivos > 0) && o.segundosDesdeLanzada < 50f);
                if (!viva) continue;
                foreach (int i in o.sectores) if (i >= 0 && i < sectoresDeDefensa.Length && sectoresDeDefensa[i] != null) sectoresDeDefensa[i].Amenazado = true;
            }
        }

        // Aliados vivos (sin el operador) dentro de cada anillo.
        void ContarAliadosPorSector()
        {
            if (sectoresDeDefensa == null) return;
            foreach (var s in sectoresDeDefensa) if (s != null) s.AliadosDentro = 0;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                if (MandoTactico.EsOperadorDeRadio(a)) continue;
                foreach (var s in sectoresDeDefensa) if (s != null && s.Contiene(a.transform.position)) { s.AliadosDentro++; break; }
            }
        }

        int AliadosVivosSinOperador()
        {
            int n = 0;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                if (MandoTactico.EsOperadorDeRadio(a)) continue;
                n++;
            }
            return n;
        }

        void TickCaidaDeSectores(float dt)
        {
            if (sectoresDeDefensa == null) return;
            int vivos = AliadosVivosSinOperador();
            foreach (var s in sectoresDeDefensa)
            {
                if (s == null) continue;
                if (!s.Caido)
                {
                    if (s.Amenazado && s.AliadosDentro == 0) s.SegundosVacio += dt; else s.SegundosVacio = 0f;
                    if (MandoTactico.DebeCaer(s.SegundosVacio))
                    {
                        s.Caido = true; s.SegundosRecuperando = 0f; SectoresCaidosTotales++;
                        if (hud != null) hud.Aviso($"¡SECTOR {s.letra} CAÍDO!\nSUS ENEMIGOS AVANZAN HACIA LA RADIO · {MandoTactico.TextoDeRecuperar()}", 3.5f);
                        SesionLog.Evento($"MANDO: sector {s.letra} CAIDO");
                    }
                }
                else
                {
                    int necesarios = MandoTactico.AliadosNecesarios(vivos);
                    if (s.AliadosDentro >= necesarios) s.SegundosRecuperando += dt; else s.SegundosRecuperando = 0f;
                    if (MandoTactico.SeRecupera(s.AliadosDentro, vivos, s.SegundosRecuperando))
                    {
                        s.Caido = false; s.SegundosVacio = 0f; s.SegundosRecuperando = 0f;
                        if (hud != null) hud.Aviso($"SECTOR {s.letra} RECUPERADO", 2.5f);
                        SesionLog.Evento($"MANDO: sector {s.letra} recuperado");
                    }
                }
            }
        }

        // ---- oleadas dirigidas ----
        void TickOleadasDelMando(float dt)
        {
            foreach (var o in oleadasMando)
            {
                float t = o.TiempoDeLanzamiento(segundosDeResistencia);
                if (!o.anunciada && RelojEfectivo >= t - MandoTactico.SegundosDeAnuncio) AnunciarOleada(o);
                if (o.anunciada && !o.lanzada && RelojEfectivo >= t) LanzarOleada(o);
            }
        }

        string LetrasDe(OleadaDelMando o)
        {
            var sb = new System.Text.StringBuilder();
            foreach (int i in o.sectores) { if (sb.Length > 0) sb.Append("+"); sb.Append(sectoresDeDefensa != null && i < sectoresDeDefensa.Length && sectoresDeDefensa[i] != null ? sectoresDeDefensa[i].letra : "?"); }
            return sb.ToString();
        }

        int EnemigosDe(OleadaDelMando o) { int n = 0; foreach (int c in o.cantidades) n += c; return n; }

        void AnunciarOleada(OleadaDelMando o)
        {
            o.anunciada = true;
            string texto = $"OLEADA {o.numero}/{CantidadDeOleadas} · SECTOR {LetrasDe(o)} EN {Mathf.RoundToInt(MandoTactico.SegundosDeAnuncio)} s · {EnemigosDe(o)} ENEMIGOS{(o.camionetas > 0 ? " + CAMIONETA" : "")}";
            if (hud != null) hud.Aviso(texto, 4f); else AlertQueue.Push(texto, AlertPriority.Alta, 4f);
            for (int k = 0; k < o.sectores.Length; k++)
            {
                var s = sectoresDeDefensa != null && o.sectores[k] < sectoresDeDefensa.Length ? sectoresDeDefensa[o.sectores[k]] : null;
                if (s == null) continue;
                // La flecha: del acceso (la boca de calle mas cercana al sector) hacia el centro del sector, sobre la calle.
                var boca = BocaMasCercana(s.Centro);
                var centro = (boca + s.Centro) * 0.5f;
                FlechaDeOleada.Mostrar(centro, s.Centro - boca, MandoTactico.SegundosDeAnuncio + 4f);
            }
            SesionLog.Evento("MANDO: " + texto);
        }

        Vector3 BocaMasCercana(Vector3 p)
        {
            var bocas = BocasEnElNavMesh();
            Vector3 mejor = p + Vector3.forward * 30f; float dm = float.MaxValue;
            foreach (var b in bocas) { float d = Plano(b, p).sqrMagnitude; if (d < dm) { dm = d; mejor = b; } }
            mejor.y = 0f;
            return mejor;
        }

        void LanzarOleada(OleadaDelMando o)
        {
            o.lanzada = true; o.segundosDesdeLanzada = 0f;
            if (hud != null) hud.Aviso($"¡OLEADA {o.numero}/{CantidadDeOleadas}!\nSECTOR {LetrasDe(o)}", 2.5f);
            float t = Reloj;
            for (int k = 0; k < o.sectores.Length; k++)
            {
                int sector = o.sectores[k];
                for (int i = 0; i < o.cantidades[k]; i++)
                {
                    var s = TomarAtacante(o, sector, i);
                    if (s == null) continue;
                    destinoDeOleada[s] = sector;
                    o.soldados.Add(s);
                    pendientesMando.Add((s, t, sector));
                    t += UnityEngine.Random.Range(0.8f, 2.0f);
                }
                t = Reloj + UnityEngine.Random.Range(0.2f, 1.0f);   // los sectores de una misma oleada entran en paralelo
            }
            if (o.camionetas > 0 && o.sectorDeCamioneta >= 0) LanzarCamionetaDeOleada(o, o.sectorDeCamioneta);
            lanzadasCiudad.Add(o.numero - 1);
        }

        // Los atacantes de las oleadas dirigidas salen de a uno (cada 0,8-2 s) por una boca de calle del lado de la amenaza de su sector.
        readonly List<(Soldier s, float t, int sector)> pendientesMando = new List<(Soldier, float, int)>();
        public int PendientesDelMando => pendientesMando.Count;

        void TickPendientesDelMando()
        {
            for (int i = pendientesMando.Count - 1; i >= 0; i--)
            {
                var (s, t, sector) = pendientesMando[i];
                if (Reloj < t) continue;
                pendientesMando.RemoveAt(i);
                if (s != null) LiberarEnSector(s, sector);
            }
        }

        void LiberarEnSector(Soldier s, int sector)
        {
            if (sectoresDeDefensa == null || sector < 0 || sector >= sectoresDeDefensa.Length || sectoresDeDefensa[sector] == null) return;
            var sec = sectoresDeDefensa[sector];
            var amenaza = sec.haciaLaAmenaza; amenaza.y = 0f; amenaza = amenaza.sqrMagnitude > 0.01f ? amenaza.normalized : Vector3.forward;
            // Bocas del lado de la amenaza, ni encima del sector (llegan caminando) ni tan lejos que tarden una eternidad.
            var candidatas = new List<Vector3>();
            foreach (var b in BocasEnElNavMesh())
            {
                var d = Plano(b, sec.Centro);
                float dist = d.magnitude;
                if (dist < 24f || dist > 80f) continue;
                if (Vector3.Dot(d / dist, amenaza) < 0.25f) continue;
                candidatas.Add(b);
            }
            if (candidatas.Count == 0) foreach (var b in BocasEnElNavMesh()) if (Plano(b, sec.Centro).magnitude >= 24f) candidatas.Add(b);
            var pos = candidatas.Count > 0 ? candidatas[UnityEngine.Random.Range(0, candidatas.Count)] : sec.Centro + amenaza * 40f;
            pos += new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0f, UnityEngine.Random.Range(-2.5f, 2.5f));
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = h.position;
            s.transform.position = new Vector3(pos.x, 0.8f, pos.z);
            s.transform.rotation = Quaternion.LookRotation(Plano(sec.Centro, pos).normalized, Vector3.up);
            s.gameObject.SetActive(true);
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null)
            {
                s.Brain.ReactivarNavegacion();
                var jit = new Vector3(UnityEngine.Random.Range(-4f, 4f), 0f, UnityEngine.Random.Range(-4f, 4f));
                s.Brain.SetPatrolRoute(new[] { sec.Centro + jit, sec.Centro - jit });
            }
            LiberadosEscalonados++;
        }

        // Un soldado enemigo inactivo: primero la reserva de la ciudad, despues una copia de la plantilla.
        Soldier TomarAtacante(OleadaDelMando o, int sector, int i)
        {
            if (oleadasDeLaCiudad != null)
                foreach (var ol in oleadasDeLaCiudad)
                {
                    if (ol == null || ol.soldados == null) continue;
                    foreach (var r in ol.soldados)
                        if (r != null && !r.gameObject.activeSelf && !destinoDeOleada.ContainsKey(r) && r.Health != null && r.Health.IsAlive) return r;
                }
            if (plantillaDeRefuerzo == null) return null;
            var go = Instantiate(plantillaDeRefuerzo, plantillaDeRefuerzo.transform.position, Quaternion.identity, plantillaDeRefuerzo.transform.parent);
            go.name = $"Atacante_O{o.numero}_{sectoresDeDefensa[sector].letra}{i + 1}";
            var s = go.GetComponent<Soldier>();
            if (s == null) { Destroy(go); return null; }
            go.SetActive(false);
            return s;
        }

        void LanzarCamionetaDeOleada(OleadaDelMando o, int sector)
        {
            if (autoPlantilla == null || plaza == null || sector < 0 || sector >= CallesDeCamioneta.Length) return;
            var c = CallesDeCamioneta[sector];
            var salida = new Vector3(plaza.position.x + c.salida.x, 0.6f, plaza.position.z + c.salida.y);
            var parada = new Vector3(plaza.position.x + c.parada.x, 0.6f, plaza.position.z + c.parada.y);
            var hacia = parada - salida; hacia.y = 0f;
            var rot = Quaternion.LookRotation(hacia.normalized, Vector3.up);
            salida = BuscarLugarParaCamioneta(salida, rot);
            parada = BuscarLugarParaCamioneta(parada, rot);
            var go = InstanciarCamioneta(salida, rot);
            go.name = $"Camioneta_Oleada{o.numero}";
            var a = go.GetComponent<OperacionAuto>();
            if (a == null) { Destroy(go); return; }
            a.ConfigurarAsalto(parada);
            o.vehiculos.Add(a);
            hubieronCamionetas = true;
            AlertQueue.Push("¡UNA CAMIONETA DE ASALTO! · CLIC DERECHO SOBRE ELLA PARA ATACARLA", AlertPriority.Alta, 3.5f);
        }

        // Los atacantes de una oleada van al centro de SU sector; si el sector cayo, a la radio. Cada ~1,5 s se les renueva el rumbo (si estan libres).
        void TickRumboDeLosAtacantes(float dt)
        {
            proximoRumbo -= dt;
            if (proximoRumbo > 0f) return;
            proximoRumbo = 1.5f;
            if (sectoresDeDefensa == null || radioDeCampana == null) return;
            foreach (var o in oleadasMando)
            {
                if (!o.lanzada) continue;
                foreach (var s in o.soldados)
                {
                    if (s == null || !s.gameObject.activeInHierarchy || s.Health == null || !s.Health.IsAlive || s.Brain == null) continue;
                    if (!destinoDeOleada.TryGetValue(s, out int idx) || idx < 0 || idx >= sectoresDeDefensa.Length) continue;
                    var sec = sectoresDeDefensa[idx];
                    var b = s.Brain;
                    if (b.CurrentTarget != null || b.State == AiState.Chase || b.State == AiState.Attack) continue;   // peleando: su IA manda
                    var meta = sec.Caido ? radioDeCampana.position : sec.Centro;
                    var jit = new Vector3(Mathf.Sin(s.Id * 2.3f), 0f, Mathf.Cos(s.Id * 2.3f)) * (sec.Caido ? 3f : 4f);
                    b.SetPatrolRoute(new[] { meta + jit, meta - jit * 0.5f });
                }
            }
        }

        // ---- cobertura automatica: los aliados quietos adentro de un sector se cubren solos (#074) ----
        readonly Dictionary<SectorDeDefensa, List<SectorDeDefensa.PuntoDeCobertura>> puntosDeSector = new Dictionary<SectorDeDefensa, List<SectorDeDefensa.PuntoDeCobertura>>();
        int versionDeCoberturas = -1;

        List<SectorDeDefensa.PuntoDeCobertura> PuntosDe(SectorDeDefensa s)
        {
            if (Coberturas.Version != versionDeCoberturas) { puntosDeSector.Clear(); versionDeCoberturas = Coberturas.Version; }
            if (!puntosDeSector.TryGetValue(s, out var l)) { l = s.PuntosDeCobertura(4); puntosDeSector[s] = l; }
            return l;
        }

        public List<SectorDeDefensa.PuntoDeCobertura> PuntosDeCoberturaDelSector(int i) => sectoresDeDefensa != null && i >= 0 && i < sectoresDeDefensa.Length && sectoresDeDefensa[i] != null ? PuntosDe(sectoresDeDefensa[i]) : new List<SectorDeDefensa.PuntoDeCobertura>();

        void TickCoberturaAutomatica(float dt)
        {
            proximaCobertura -= dt;
            if (proximaCobertura > 0f || sectoresDeDefensa == null) return;
            proximaCobertura = 0.5f;
            var todos = ActorRegistry.All;
            foreach (var sec in sectoresDeDefensa)
            {
                if (sec == null) continue;
                var puntos = PuntosDe(sec);
                if (puntos.Count == 0) continue;
                for (int i = 0; i < todos.Count; i++)
                {
                    var a = todos[i];
                    if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                    if (MandoTactico.EsOperadorDeRadio(a) || !sec.Contiene(a.transform.position)) continue;
                    var b = a.Brain;
                    if (b == null || b.IsPossessedByPlayer || b.EnCobertura || b.YendoACobertura || b.TieneOrden || b.Quieto || b.Pasivo || b.MontadoEnVehiculo) continue;
                    // El punto libre mas cercano (nadie en cobertura ahi ni yendo para alla).
                    int mejor = -1; float dm = float.MaxValue;
                    for (int k = 0; k < puntos.Count; k++)
                    {
                        if (puntos[k].dueno == null) continue;
                        if (PuntoTomado(puntos[k].punto, a, todos)) continue;
                        float d = Plano(puntos[k].punto, a.transform.position).sqrMagnitude;
                        if (d < dm) { dm = d; mejor = k; }
                    }
                    if (mejor < 0) continue;
                    b.IssueCoverOrder(puntos[mejor].punto, puntos[mejor].dueno);
                    CoberturasAutomaticas++;
                }
            }
        }

        static bool PuntoTomado(Vector3 p, Soldier yo, IReadOnlyList<Soldier> todos)
        {
            for (int i = 0; i < todos.Count; i++)
            {
                var o = todos[i];
                if (o == null || o == yo || o.Team != TeamId.Player || o.Brain == null || o.Health == null || !o.Health.IsAlive) continue;
                if (!(o.Brain.EnCobertura || o.Brain.YendoACobertura)) continue;
                if (Plano(o.Brain.CoberturaPunto, p).sqrMagnitude < 1.2f * 1.2f) return true;
            }
            return false;
        }

        // ---- campanario: un soldado arriba revela a los enemigos en el minimapa y dispara con alcance extra ----
        void TickCampanario()
        {
            bool ocupado = false;
            if (campanario != null)
            {
                var todos = ActorRegistry.All;
                for (int i = 0; i < todos.Count; i++)
                {
                    var a = todos[i];
                    if (a == null || a.Team != TeamId.Player || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                    bool arriba = a.transform.position.y > AlturaDelCampanario - 1.2f && Plano(a.transform.position, campanario.position).magnitude <= RadioDelCampanario;
                    if (a.Brain != null) a.Brain.BonusDeAlcance = arriba ? AlcanceExtraDelCampanario : 1f;
                    if (arriba) ocupado = true;
                }
            }
            CampanarioOcupado = ocupado;
            WorldUiDirector.RevelarEnemigos = ocupado;
        }

        // ------------------------------------------------------------------ tutorial guiado
        bool Seleccion(out IReadOnlyList<Soldier> sel)
        {
            var drv = PlayerInputDriver.Activo;
            sel = drv != null && drv.Selection != null ? drv.Selection.Selected : null;
            return sel != null;
        }

        // #114: los pasos del panel van de a UNO. Solo se evalua el paso actual; recien al cumplirse se pasa al siguiente (antes cada paso se
        // tachaba por su cuenta apenas se cumplia la condicion, y se veia el paso 1 activo con el 3 tachado). Interruptor solo para las pruebas.
        public static bool PasosSecuenciales = true;

        bool CumpleElPaso(int i, IReadOnlyList<Soldier> sel)
        {
            switch (i)
            {
                case 0: return UnidadA == null || (sel != null && sel.Count == 1 && sel[0] == UnidadA);
                case 1: return UnidadA == null || (ordenSector.TryGetValue(UnidadA.Id, out int a) && a == 0);
                case 2:
                    {
                        if (milicianosActivos.Count == 0 || sel == null) return false;
                        foreach (var m in milicianosActivos) if (m != null && m.Health.IsAlive && !ListaContiene(sel, m)) return false;
                        return true;
                    }
                case 3:
                    {
                        bool todos = milicianosActivos.Count > 0;
                        foreach (var m in milicianosActivos) if (m != null && m.Health.IsAlive && !(ordenSector.TryGetValue(m.Id, out int b) && b == 1)) todos = false;
                        return todos;
                    }
                case 4: return UnidadC == null || (ordenSector.TryGetValue(UnidadC.Id, out int c) && c == 2);
            }
            return false;
        }

        bool CamionetaViva()
        {
            foreach (var o in oleadasMando) if (o.VehiculosVivos > 0) return true;
            return false;
        }

        // El paso actual: el primero de 1-5 sin hacer; el de la camioneta solo cuando hay una.
        int PrimerPasoPendiente()
        {
            for (int i = 0; i < 5; i++) if (!pasoHecho[i]) return i;
            if (!pasoHecho[5] && CamionetaViva()) return 5;
            return -1;
        }

        void TickPasosDelMando()
        {
            Seleccion(out var sel);
            if (PasosSecuenciales)
            {
                // Se encadenan los pasos cuyo estado ya estaba cumplido (una orden dada antes no se pierde), pero NUNCA se salta el actual.
                for (int guardia = 0; guardia < 6; guardia++)
                {
                    int actualAhora = PrimerPasoPendiente();
                    if (actualAhora < 0 || actualAhora > 4 || !CumpleElPaso(actualAhora, sel)) break;
                    pasoHecho[actualAhora] = true;
                }
                bool primeros = true;
                for (int i = 0; i < 5; i++) if (!pasoHecho[i]) primeros = false;
                if (primeros && !pasoHecho[5] && (ordenDeAtaqueAVehiculo || (hubieronCamionetas && !CamionetaViva()))) pasoHecho[5] = true;
            }
            else
            {
                // Comportamiento anterior (solo para reproducir el #114 en las pruebas): cada paso se tacha solo.
                for (int i = 0; i < 5; i++) if (!pasoHecho[i] && CumpleElPaso(i, sel)) pasoHecho[i] = true;
                if (!pasoHecho[5] && (ordenDeAtaqueAVehiculo || (hubieronCamionetas && !CamionetaViva()))) pasoHecho[5] = true;
            }
            PasoActualDelMando = PrimerPasoPendiente();
        }

        static bool ListaContiene(IReadOnlyList<Soldier> l, Soldier s) { for (int i = 0; i < l.Count; i++) if (l[i] == s) return true; return false; }

        static string Nombre(Soldier s) => s != null ? RolRequerido.NombreCorto(s) : "?";

        string Letra(int i) => sectoresDeDefensa != null && i >= 0 && i < sectoresDeDefensa.Length && sectoresDeDefensa[i] != null ? sectoresDeDefensa[i].letra : "?";

        // #114: texto corto con verbo + tecla + destino. Los numeros salen de las constantes / listas reales (nunca "los dos" escrito a mano).
        PanelDeMando.Paso[] PasosParaElPanel()
        {
            string a = Nombre(UnidadA), c = Nombre(UnidadC);
            int milicias = 0; foreach (var m in milicianosActivos) if (m != null && m.Health != null && m.Health.IsAlive) milicias++;
            return new[]
            {
                new PanelDeMando.Paso { texto = $"SELECCIONÁ A {a}: CLIC EN SU ROMBO", clicIzq = true },
                new PanelDeMando.Paso { texto = $"MANDALO AL SECTOR {Letra(0)}: CLIC DERECHO EN EL CÍRCULO {Letra(0)}", clicDer = true },
                new PanelDeMando.Paso { texto = $"SELECCIONÁ A LOS {milicias} MILICIANOS: ARRASTRÁ UN RECUADRO", arrastre = true },
                new PanelDeMando.Paso { texto = $"MANDALOS AL SECTOR {Letra(1)}: CLIC DERECHO EN EL CÍRCULO {Letra(1)}", clicDer = true },
                new PanelDeMando.Paso { texto = $"{c} AL SECTOR {Letra(2)}: SELECCIONALO Y CLIC DERECHO EN EL CÍRCULO {Letra(2)}", clicIzq = true, clicDer = true },
                new PanelDeMando.Paso { texto = "LLEGÓ LA CAMIONETA: CLIC DERECHO SOBRE ELLA PARA ATACAR", clicDer = true },
            };
        }

        // #114: lo que marca la guia del paso actual (la dibuja GuiaDeMando): flecha 3D sobre el objetivo del paso y, cuando hay un destino, el
        // punteado del piso desde quien tiene que moverse hasta el sector. false = no hay guia (sin radio, sin paso activo).
        public struct GuiaDePaso { public int paso; public Vector3 flechaEn; public bool hayRuta; public Vector3 origen, destino; public Soldier unidad; }

        public bool GuiaDelPaso(out GuiaDePaso g)
        {
            g = default;
            if (Fase != FaseOperacion.Resistir || Subfase < 1 || !MandoTactico.EnRadio) return false;
            int paso = PasoActualDelMando;
            if (paso < 0) return false;
            g.paso = paso;
            Soldier primero = null;
            foreach (var m in milicianosActivos) if (m != null && m.Health != null && m.Health.IsAlive) { primero = m; break; }
            Vector3 Centro(int i) => sectoresDeDefensa != null && i < sectoresDeDefensa.Length && sectoresDeDefensa[i] != null ? sectoresDeDefensa[i].Centro : Vector3.zero;
            switch (paso)
            {
                case 0: if (UnidadA == null) return false; g.unidad = UnidadA; g.flechaEn = UnidadA.transform.position + Vector3.up * 2.4f; return true;
                case 1: if (UnidadA == null) return false; g.unidad = UnidadA; g.hayRuta = true; g.origen = UnidadA.transform.position; g.destino = Centro(0); g.flechaEn = g.destino; return true;
                case 2: if (primero == null) return false; g.unidad = primero; g.flechaEn = primero.transform.position + Vector3.up * 2.4f; return true;
                case 3: if (primero == null) return false; g.unidad = primero; g.hayRuta = true; g.origen = primero.transform.position; g.destino = Centro(1); g.flechaEn = g.destino; return true;
                case 4:
                    {
                        if (UnidadC == null) return false;
                        g.unidad = UnidadC; g.hayRuta = true; g.origen = UnidadC.transform.position; g.destino = Centro(2);
                        bool seleccionada = Seleccion(out var sel) && sel.Count == 1 && sel[0] == UnidadC;
                        g.flechaEn = seleccionada ? g.destino : UnidadC.transform.position + Vector3.up * 2.4f;
                        return true;
                    }
                case 5:
                    foreach (var o in oleadasMando) foreach (var v in o.vehiculos) if (v != null && !v.Muerto) { g.flechaEn = v.transform.position + Vector3.up * 2.6f; return true; }
                    return false;
            }
            return false;
        }

        // Un aliado caido: el panel lo recuerda ("seleccioná a DOC y clic derecho sobre el caído").
        string AvisoDeCaido()
        {
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                if (MandoTactico.EsOperadorDeRadio(a)) continue;
                string med = MedicoDeApoyo != null && MedicoDeApoyo.Health.IsAlive && MedicoDeApoyo != a ? Nombre(MedicoDeApoyo) : "UN MÉDICO";
                return $"{Nombre(a)} CAYÓ: seleccioná a {med} y clic derecho sobre el caído";
            }
            return "";
        }

        // ---- anillo guia: el paso actual del tutorial marca con un anillo pulsante a quien hay que seleccionar ----
        IEnumerable<Soldier> ObjetivosDeGuia()
        {
            if (!MandoTactico.EnRadio) yield break;
            switch (PasoActualDelMando)
            {
                case 0: if (UnidadA != null) yield return UnidadA; break;
                case 2: foreach (var m in milicianosActivos) if (m != null) yield return m; break;
                case 4: if (UnidadC != null) yield return UnidadC; break;
            }
        }

        public int AnillosDeGuia { get { int n = 0; foreach (var r in anillosDeGuia) if (r != null) n++; return n; } }

        void ActualizarAnillosDeGuia()
        {
            var objetivos = new List<Soldier>();
            foreach (var s in ObjetivosDeGuia()) if (s != null && s.Health != null && s.Health.IsAlive) objetivos.Add(s);
            for (int i = anillosDeGuia.Count - 1; i >= 0; i--)
            {
                var r = anillosDeGuia[i];
                bool sigue = false;
                if (r != null) foreach (var s in objetivos) if (r.Target == s.transform) { sigue = true; break; }
                if (!sigue) { if (r != null) Destroy(r.gameObject); anillosDeGuia.RemoveAt(i); }
            }
            foreach (var s in objetivos)
            {
                bool ya = false;
                foreach (var r in anillosDeGuia) if (r != null && r.Target == s.transform) { ya = true; break; }
                if (!ya) anillosDeGuia.Add(SelectionRingFx.Spawn(s.transform, new Color(1f, 0.85f, 0.15f, 1f), 1.7f));
            }
        }

        // ------------------------------------------------------------------ HUD
        static string Marca(SectorDeDefensa s) => s == null ? "?" : s.Caido ? "✗" : s.AliadosDentro > 0 ? "✓" : "✗";

        void ActualizarHudDelMando()
        {
            // El HUD y el panel se refrescan 6 veces por segundo (armar textos cada frame generaba basura sin necesidad).
            if (Time.unscaledTime < proximoHud && !ForzarHud) return;
            proximoHud = Time.unscaledTime + 0.16f; ForzarHud = false;
            int seg = Mathf.CeilToInt(Mathf.Max(0f, segundosDeResistencia - RelojEfectivo));
            var sb = new System.Text.StringBuilder("Sectores cubiertos: ");
            if (sectoresDeDefensa != null)
                for (int i = 0; i < sectoresDeDefensa.Length; i++)
                {
                    var s = sectoresDeDefensa[i];
                    if (s == null) continue;
                    if (i > 0) sb.Append(" · ");
                    sb.Append(s.letra).Append(' ').Append(Marca(s));
                }
            sb.Append($" · helicóptero en {seg} s");
            bool pausado = !RelojDelMandoCorre;
            var color = pausado ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.55f, 0.25f);
            if (hud != null)
            {
                hud.Objetivo(TituloObjetivo(5, "DIRIGIR LA DEFENSA"), sb.ToString(), RelojEfectivo / Mathf.Max(1f, segundosDeResistencia), color);
                hud.Timer("HELICÓPTERO", seg, pausado ? new Color(1f, 0.3f, 0.25f) : (seg <= 10 ? new Color(0.45f, 1f, 0.55f) : Color.white));
            }
            var panel = PanelDeMando.Asegurar();
            panel.Fijar(PasosParaElPanel(), pasoHecho, PasoActualDelMando);
            // Aviso del panel: sector caido > aliado caido > motivo de la pausa.
            string aviso = ""; Color ca = SectorDeDefensa.Amarillo;
            string caido = AvisoDeCaido();
            bool hayCaido = false;
            if (sectoresDeDefensa != null) foreach (var s in sectoresDeDefensa) if (s != null && s.Caido) { hayCaido = true; aviso = $"¡SECTOR {s.letra} CAÍDO! {MandoTactico.TextoDeRecuperar()}"; ca = SectorDeDefensa.Rojo; break; }
            if (!hayCaido && caido != "") { aviso = caido; ca = new Color(1f, 0.6f, 0.3f); }
            if (aviso == "" && pausado) { aviso = MotivoDeLaPausa; ca = MotivoDeLaPausa.StartsWith("UBICA") ? SectorDeDefensa.Amarillo : SectorDeDefensa.Rojo; }
            panel.Aviso(aviso, ca);
        }

        // ------------------------------------------------------------------ final
        void FinalizarElMando()
        {
            if (hud != null) hud.Aviso("¡EL HELICÓPTERO ESTÁ ATERRIZANDO!", 4f);
            if (MandoTactico.RadioTomada) SoltarLaRadio("", true);
            LimpiarElMando();
            if (CoberturaEstacionaria) PonerHeliEnEstacionario();   // #127: se queda arriba cubriendo hasta que todos esten en la zona
            else
            {
                heli.position = heliAterrizaje.position;
                if (ScriptHeli != null) ScriptHeli.Volando = false;
                HeliAterrizo = true;
            }
            if (hud != null) hud.OcultarTimer();
            EntrarFase(FaseOperacion.Extraer);
        }

        // Quita todo lo visual y los enganches del mando (al terminar la fase, perder o reiniciar).
        void LimpiarElMando()
        {
            DesuscribirMando();
            PanelDeMando.Quitar();
            FlechaDeOleada.QuitarTodas();
            WorldUiDirector.RevelarEnemigos = false;
            if (sectoresDeDefensa != null) foreach (var s in sectoresDeDefensa) if (s != null) s.Mostrar(false);
            foreach (var m in milicianosActivos) if (m != null && m.Brain != null) m.Brain.BonusDeAlcance = 1f;
            foreach (var r in anillosDeGuia) if (r != null) Destroy(r.gameObject);
            anillosDeGuia.Clear();
        }

        // ------------------------------------------------------------------ pruebas
        // Las unidades quedan ubicadas SOLAS en los sectores (subfase 1 pedida por Arrancar(5, _, 1)) y los pasos 1-5 hechos.
        void UbicarLaEscuadraSola()
        {
            if (sectoresDeDefensa == null || sectoresDeDefensa.Length < 3) return;
            Teletransportar(UnidadA, sectoresDeDefensa[0].Centro, 0f);
            for (int i = 0; i < milicianosActivos.Count; i++) Teletransportar(milicianosActivos[i], sectoresDeDefensa[1].Centro, (i - (milicianosActivos.Count - 1) * 0.5f) * 1.6f);
            Teletransportar(UnidadC, sectoresDeDefensa[2].Centro, 0f);
            if (UnidadA != null) ordenSector[UnidadA.Id] = 0;
            foreach (var m in milicianosActivos) if (m != null) ordenSector[m.Id] = 1;
            if (UnidadC != null) ordenSector[UnidadC.Id] = 2;
            for (int i = 0; i < 5; i++) pasoHecho[i] = true;
        }

        static void Teletransportar(Soldier s, Vector3 centro, float lado)
        {
            if (s == null) return;
            var p = centro + new Vector3(lado, 0f, 0f);
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 4f, UnityEngine.AI.NavMesh.AllAreas)) p = h.position;
            s.transform.position = new Vector3(p.x, s.transform.position.y, p.z);
            ApoyoEnElPiso.Apoyar(s.transform);
            if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
        }

        // ------------------------------------------------------------------ estado de la sesion
        static string Nom(Soldier s) => s != null ? s.name : "";

        static Soldier PorNombre(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++) if (todos[i] != null && todos[i].name == nombre) return todos[i];
            return null;
        }

        // Una oleada del mando "termino" si ya salio y no queda nadie vivo ni pendiente (soldados ni camionetas).
        bool OleadaTerminada(OleadaDelMando o) => o.lanzada && o.Vivos == 0 && o.Pendientes(this) == 0 && o.VehiculosVivos == 0;

        void CapturarMando(Dictionary<string, string> kv)
        {
            // Los milicianos siguen vivos en Extraer (suben al helicoptero): se guardan para recrearlos al continuar.
            if (Fase == FaseOperacion.Resistir || Fase == FaseOperacion.Extraer)
            {
                var nm = new List<string>(); foreach (var m in milicianosActivos) if (m != null) nm.Add(m.name);
                kv["milicianos"] = string.Join(",", nm);
            }
            if (Fase != FaseOperacion.Resistir) return;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            kv["relojEfectivo"] = RelojEfectivo.ToString("0.00", ic);
            kv["radioTomada"] = MandoTactico.RadioTomada.ToString();
            var caidos = new List<int>();
            if (sectoresDeDefensa != null) for (int i = 0; i < sectoresDeDefensa.Length; i++) if (sectoresDeDefensa[i] != null && sectoresDeDefensa[i].Caido) caidos.Add(i);
            kv["sectoresCaidos"] = string.Join(",", caidos);
            var pasos = new List<int>(); for (int i = 0; i < pasoHecho.Length; i++) if (pasoHecho[i]) pasos.Add(i);
            kv["pasosMando"] = string.Join(",", pasos);
            // P10: el estado de la radio y de la defensa que antes no se guardaba.
            kv["operador"] = Nom(MandoTactico.Operador);
            kv["primerIngreso"] = MandoTactico.PrimerIngresoHecho.ToString();
            kv["unidadA"] = Nom(UnidadA); kv["unidadC"] = Nom(UnidadC);
            var os = new List<string>();
            foreach (var par in ordenSector) { var s = ActorRegistry.FindById(par.Key); if (s != null) os.Add(s.name + "=" + par.Value); }
            kv["ordenSector"] = string.Join(";", os);
            // Oleadas: las terminadas no vuelven; las que estaban en curso se relanzan (los enemigos de la zona se reinician) y el reloj retrocede
            // hasta antes de su aviso para que se anuncien de nuevo.
            var hechas = new List<int>(); float rebobinar = -1f;
            for (int i = 0; i < oleadasMando.Count; i++)
            {
                var o = oleadasMando[i];
                if (OleadaTerminada(o)) hechas.Add(i);
                else if (o.anunciada)
                {
                    float t = o.TiempoDeLanzamiento(segundosDeResistencia) - MandoTactico.SegundosDeAnuncio - 0.5f;
                    if (rebobinar < 0f || t < rebobinar) rebobinar = Mathf.Max(0f, t);
                }
            }
            kv["oleadasMandoHechas"] = string.Join(",", hechas);
            kv["relojOleada"] = rebobinar.ToString("0.00", ic);
        }

        void RestaurarMando(Dictionary<string, string> kv)
        {
            if (Fase == FaseOperacion.Extraer)
            {
                // Los milicianos de la ciudad se vuelven a crear (despues SesionLog les devuelve vida y posicion por nombre).
                if (kv.TryGetValue("milicianos", out var mil) && !string.IsNullOrEmpty(mil) && milicianosActivos.Count == 0) ActivarMilicianos(true);
                return;
            }
            if (!kv.TryGetValue("radioTomada", out var rt) || ResistirSinMando) return;
            bool tomada = rt == "True";
            if (!tomada && Subfase < 1) return;   // todavia llegando a la radio: no hay nada del mando que restaurar
            MandoTactico.PrimerIngresoHecho = true;   // P10: la camara se restaura aparte; no se manda a RTS "por primera vez"
            SaltoDePrueba = false;
            if (Subfase != 1) EntrarSubfase(1); else IniciarMando(false);
            // El operador de la radio es el que era (IniciarMando la tomo con el poseido de ese momento).
            var op = PorNombre(kv.TryGetValue("operador", out var opn) ? opn : "");
            if (tomada && op != null && op.Health != null && op.Health.IsAlive && !ReferenceEquals(op, MandoTactico.Operador)) CambiarOperadorRestaurado(op);
            else if (!tomada) SoltarLaRadio("", false);
            if (kv.TryGetValue("primerIngreso", out var pi)) MandoTactico.PrimerIngresoHecho = pi == "True";
            var ua = PorNombre(kv.TryGetValue("unidadA", out var na) ? na : ""); if (ua != null) UnidadA = ua;
            var uc = PorNombre(kv.TryGetValue("unidadC", out var nc) ? nc : ""); if (uc != null) UnidadC = uc;
            // Pasos del panel y ordenes a sectores tal como estaban.
            if (kv.TryGetValue("pasosMando", out var pm))
            {
                for (int i = 0; i < pasoHecho.Length; i++) pasoHecho[i] = false;
                if (!string.IsNullOrEmpty(pm)) foreach (var p in pm.Split(',')) if (int.TryParse(p, out var i) && i >= 0 && i < pasoHecho.Length) pasoHecho[i] = true;
            }
            ordenSector.Clear();
            if (kv.TryGetValue("ordenSector", out var osr) && !string.IsNullOrEmpty(osr))
                foreach (var par in osr.Split(';'))
                {
                    int ie = par.LastIndexOf('=');
                    if (ie <= 0 || !int.TryParse(par.Substring(ie + 1), out var sec)) continue;
                    var s = PorNombre(par.Substring(0, ie)); if (s != null) ordenSector[s.Id] = sec;
                }
            RelojEfectivo = F(kv, "relojEfectivo");
            float rebobinar = F(kv, "relojOleada", -1f);
            if (rebobinar >= 0f) RelojEfectivo = Mathf.Min(RelojEfectivo, rebobinar);
            if (kv.TryGetValue("sectoresCaidos", out var sc) && !string.IsNullOrEmpty(sc) && sectoresDeDefensa != null)
                foreach (var p in sc.Split(',')) if (int.TryParse(p, out var i) && i >= 0 && i < sectoresDeDefensa.Length && sectoresDeDefensa[i] != null) sectoresDeDefensa[i].Caido = true;
            if (kv.TryGetValue("oleadasMandoHechas", out var oh))
            {
                if (!string.IsNullOrEmpty(oh))
                    foreach (var p in oh.Split(',')) if (int.TryParse(p, out var i) && i >= 0 && i < oleadasMando.Count) { oleadasMando[i].anunciada = true; oleadasMando[i].lanzada = true; lanzadasCiudad.Add(i); }
            }
            else if (kv.TryGetValue("oleadasCiudad", out var ow) && !string.IsNullOrEmpty(ow))   // partidas/reportes viejos
                foreach (var p in ow.Split(',')) if (int.TryParse(p, out var i) && i >= 0 && i < oleadasMando.Count) { oleadasMando[i].anunciada = true; oleadasMando[i].lanzada = true; }
        }

        // La radio pasa al soldado guardado (IniciarMando la dejo en el poseido del momento).
        void CambiarOperadorRestaurado(Soldier nuevo)
        {
            var viejo = MandoTactico.Operador;
            MandoTactico.Soltar();
            if (viejo != null)
            {
                AccionesEnCurso.Terminar(viejo);
                if (viejo.Motor != null) viejo.Motor.SetCrouching(false);
                if (viejo.Brain != null && !viejo.Brain.IsPossessedByPlayer) viejo.Brain.Quieto = false;
            }
            if (radioDeCampana != null)
            {
                var pos = PuestoDelOperador();
                nuevo.transform.position = new Vector3(pos.x, nuevo.transform.position.y, pos.z);
                ApoyoEnElPiso.Apoyar(nuevo.transform);
            }
            if (nuevo.Motor != null) nuevo.Motor.SetCrouching(true);
            MandoTactico.Establecer(nuevo);
        }

        void OnDestroyMando()
        {
            DesuscribirMando();
            if (MandoTactico.RadioTomada) MandoTactico.Soltar();
            WorldUiDirector.RevelarEnemigos = false;
        }
    }
}
