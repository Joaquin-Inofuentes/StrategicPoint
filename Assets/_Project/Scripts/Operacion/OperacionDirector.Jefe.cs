using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // Subfases del objetivo 4 (HUIR). WP9b (#097 parte 2): el tanque ya no espera con las puertas abiertas; es un JEFE que hay que derribar.
    //   Acercamiento  caminar hasta el patio; al llegar a z > -30 una toma de 3,5 s presenta al BLINDADO GOLIAT.
    //   Jefe          pelea contra el BLINDADO (TanqueJefe): cohetes y explosivos lo bajan hasta el 5 %, donde se rinde y la tripulacion baja.
    //   Reparacion    Kes lo repara 25 s (ReparacionDeTanque) mientras la escuadra la cubre de tres oleadas.
    //   Abordaje      el tanque ya es nuestro (bando Player, 70 % de vida): [E] para subir.
    //   Carrera       el trayecto en tanque (TickTrayecto, WP9b-2: ruta larga, helicoptero Halcon).
    //   CinematicaFinal  (WP9b-2) el cierre en camara lenta.
    public enum SubfaseHuida { Acercamiento = 0, Jefe = 1, Reparacion = 2, Abordaje = 3, Carrera = 4, CinematicaFinal = 5 }

    public partial class OperacionDirector
    {
        [Header("4b. Jefe del patio (WP9b)")]
        public Soldier[] tripulacionDelJefe;          // 2: conductor y artillero de la metralleta del BLINDADO
        public Transform[] patrullaDelJefe;           // 4 puntos del patio
        public Transform[] cajasDeCohetes;            // 2 cajas de suministros del patio (se crean al empezar la pelea)
        public OleadaDeReserva[] oleadasDeReparacion; // 0 = 5 del este (t = 9 s), 1 = 6 del oeste (t = 18 s)
        public float segundosDeRecargaDeCajas = 25f;

        public const float ZParaLaToma = -30f, SegundosDeLaToma = 3.5f;
        public const float SegundoOleadaEste = 9f, SegundoOleadaOeste = 18f;
        public static readonly Color ColorDelJefe = new Color(1f, 0.38f, 0.28f);

        public SubfaseHuida SubfaseDeHuida => Fase == FaseOperacion.Huir ? (SubfaseHuida)Subfase : SubfaseHuida.Acercamiento;
        public TanqueJefe Jefe => jefe;
        public ReparacionDeTanque Reparacion => reparacion;
        public bool TomaDelJefeActiva { get; private set; }
        public bool TomaDelJefeHecha { get; private set; }
        public bool ReparacionCompletada { get; private set; }
        public int OleadasDeReparacionLanzadas { get; private set; }
        public int EnemigosDeLasOleadas { get; private set; }
        public int CamionetasDeReparacion { get; private set; }
        public float RelojDeSubfase => relojSub;

        TanqueJefe jefe;
        ReparacionDeTanque reparacion;
        float relojSub;
        Vector3 posTanqueInicial; Quaternion rotTanqueInicial; bool tanqueGuardado;
        float velocidadInicial = -1f, aceleracionInicial = -1f;
        bool oleadaNorteLanzada, oleadaEsteLanzada, oleadaOesteLanzada;
        readonly List<CajaDeSuministros> cajasDelJefe = new List<CajaDeSuministros>();
        readonly List<OperacionAuto> camionetasDeLaReparacion = new List<OperacionAuto>();

        // Se llama desde OperacionDirector.EntrarSubfase.
        partial void AlEntrarSubfase(int s)
        {
            relojSub = 0f;
            if (Fase == FaseOperacion.Resistir && Application.isPlaying) { EntrarSubfaseResistir(s); return; }   // WP10
            if (Fase != FaseOperacion.Huir || !Application.isPlaying) return;
            switch ((SubfaseHuida)s)
            {
                case SubfaseHuida.Jefe: IniciarJefe(); break;
                case SubfaseHuida.Reparacion: IniciarReparacion(); break;
                case SubfaseHuida.Abordaje: IniciarAbordaje(); break;
            }
        }

        void GuardarTanqueInicial()
        {
            if (tanque == null || tanqueGuardado) return;
            tanqueGuardado = true;
            posTanqueInicial = tanque.transform.position;
            rotTanqueInicial = tanque.transform.rotation;
            var m = tanque.GetComponent<VehicleMotor>();
            if (m != null) { velocidadInicial = m.MaxSpeed; aceleracionInicial = m.Aceleracion; }
        }

        // Entrada del objetivo 4: el tanque vuelve a su sitio como BLINDADO enemigo dormido, con su tripulacion adentro.
        void PrepararHuida()
        {
            EnTanque = false;
            TomaDelJefeActiva = false; TomaDelJefeHecha = false; ReparacionCompletada = false;
            CineDeHuida.Cancelar();
            if (tanque == null) return;
            GuardarTanqueInicial();
            LimpiarHuida();
            if (!Application.isPlaying) return;
            FinalAbortar();

            // Si venia de una carrera previa (saltos de prueba): se baja a todos y se frena.
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.Vehicle == tanque && drv.CurrentSeat.HasValue) drv.ExitVehicle();
            foreach (var o in new List<Soldier>(tanque.Occupants)) tanque.Dismount(o);
            var vb = tanque.GetComponent<VehicleBrain>();
            if (vb != null) vb.Stop();
            var motor = tanque.GetComponent<VehicleMotor>();
            if (motor != null && velocidadInicial > 0f) motor.Configurar(velocidadInicial, aceleracionInicial);

            tanque.transform.SetPositionAndRotation(posTanqueInicial, rotTanqueInicial);
            tanque.PisoDeVida01 = 0f; tanque.AjusteDeDano = null;
            if (tanque.Health != null && tanque.Health.IsAlive) tanque.PonerVida01(1f);
            tanque.AsignarBando(TeamId.Enemy, new Color(0.9f, 0.25f, 0.2f));
            jefe = TanqueJefe.Preparar(tanque);
            jefe.Reposo();

            // Tripulacion enemiga adentro (conductor + metralleta; el canon lo apunta la IA del jefe: nunca Gunner).
            if (tripulacionDelJefe != null)
            {
                var asientos = new[] { VehicleSeatRole.Driver, VehicleSeatRole.Passenger2 };
                for (int i = 0; i < tripulacionDelJefe.Length && i < asientos.Length; i++)
                {
                    var s = tripulacionDelJefe[i];
                    if (s == null || s.Health == null) continue;
                    if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
                    if (s.Health.IsAlive) tanque.Mount(s, asientos[i], true);
                }
            }
            PonerBaliza("BLINDADO", ColorDelJefe, tanque.transform.position, tanque.transform, 3f);
        }

        // Borra lo que dejo una pasada anterior por la huida (pruebas que saltan varias veces).
        void LimpiarHuida()
        {
            if (reparacion != null) { reparacion.AlCompletar -= TerminarReparacion; Destroy(reparacion.gameObject); reparacion = null; }
            if (jefe != null) { jefe.AlTerminarDeRendirse -= AlBajarLaTripulacionDelJefe; jefe.Soltar(); }
            foreach (var c in cajasDelJefe) if (c != null) Destroy(c.gameObject);
            cajasDelJefe.Clear();
            AnilloDeAviso.CancelarTodos();
            if (hud != null) hud.OcultarBarraDeJefe();
            relojSub = 0f;
            if (Halcon != null) Destroy(Halcon.gameObject);
            Halcon = null; halconLanzado = false;
            if (emboscadas != null) foreach (var e in emboscadas) if (e != null) e.Desarmar();
            OleadasDeReparacionLanzadas = 0;
            oleadaNorteLanzada = oleadaEsteLanzada = oleadaOesteLanzada = false;
        }

        // ---------------------------------------------------------------
        // Ticks
        // ---------------------------------------------------------------
        void TickHuir(float dt)
        {
            relojSub += dt;
            if (EnTanque && Subfase < (int)SubfaseHuida.CinematicaFinal) { TickTrayecto(dt); return; }
            switch ((SubfaseHuida)Subfase)
            {
                case SubfaseHuida.Acercamiento: TickAcercamiento(); break;
                case SubfaseHuida.Jefe: TickJefe(); break;
                case SubfaseHuida.Reparacion: TickReparacion(); break;
                case SubfaseHuida.CinematicaFinal: break;   // la lleva la corrutina CinematicaFinal (OperacionDirector.Final.cs)
                default: TickAntesDelTanque(); break;
            }
        }

        static readonly Color VerdeHuida = new Color(0.4f, 1f, 0.5f);

        void TickAcercamiento()
        {
            var yo = Poseido();
            if (hud != null)
                hud.Objetivo(TituloObjetivo(4, "HUIR EN EL TANQUE"), "Avanza al patio · hay un BLINDADO enemigo custodiando la fuga", 0f, ColorDelJefe);
            if (TomaDelJefeActiva || yo == null || !yo.Health.IsAlive) return;
            if (yo.transform.position.z > ZParaLaToma) StartCoroutine(TomaDelJefe());
        }

        void TickJefe()
        {
            if (jefe == null || tanque == null) return;
            float frac = jefe.Fraccion01;
            if (hud != null)
            {
                hud.BarraDeJefe(TanqueJefe.NombreDelJefe, frac);
                hud.Objetivo(TituloObjetivo(4, "DERRIBAR AL BLINDADO"), "COHETES o CARGAS del Asalto · esquiva el circulo rojo · recarga en las cajas del patio", 1f - frac, ColorDelJefe);
                var yo = Poseido();
                if (yo != null && yo.Health.IsAlive)
                {
                    string pista = PistaDeLanzacohetes(yo);
                    if (pista != null) hud.Prompt(pista);
                }
            }
        }

        void TickReparacion()
        {
            if (reparacion == null || tanque == null) return;
            // Oleadas: la del norte arranca de una; este a los 9 s; oeste (con una camioneta) a los 18 s.
            var destino = tanque.transform.position;
            if (!oleadaNorteLanzada)
            {
                oleadaNorteLanzada = true; OleadasDeReparacionLanzadas++;
                EnemigosDeLasOleadas += Contar(oleadaDeHuida);
                Activar(oleadaDeHuida, destino);
            }
            if (!oleadaEsteLanzada && relojSub >= SegundoOleadaEste && oleadasDeReparacion != null && oleadasDeReparacion.Length > 0)
            {
                oleadaEsteLanzada = true; OleadasDeReparacionLanzadas++;
                EnemigosDeLasOleadas += Contar(oleadasDeReparacion[0]);
                Activar(oleadasDeReparacion[0], destino);
            }
            if (!oleadaOesteLanzada && relojSub >= SegundoOleadaOeste && oleadasDeReparacion != null && oleadasDeReparacion.Length > 1)
            {
                oleadaOesteLanzada = true; OleadasDeReparacionLanzadas++;
                EnemigosDeLasOleadas += Contar(oleadasDeReparacion[1]);
                Activar(oleadasDeReparacion[1], destino);
                LanzarCamionetaDeLaReparacion();
            }
            if (hud != null)
            {
                string det = reparacion.Pausada && !string.IsNullOrEmpty(reparacion.MotivoDePausa)
                    ? "Reparacion en pausa: " + reparacion.MotivoDePausa
                    : "Kes repara el blindado · cubrila de las oleadas";
                hud.Objetivo(TituloObjetivo(4, "REPARAR EL BLINDADO"), det, reparacion.Progreso01, VerdeHuida);
                if (!reparacion.ElJugadorRepara) hud.Prompt(reparacion.TextoDeBarra + (reparacion.Pausada && !string.IsNullOrEmpty(reparacion.MotivoDePausa) ? " · " + reparacion.MotivoDePausa : ""), reparacion.Progreso01);
            }
        }

        static int Contar(OleadaDeReserva o)
        {
            int n = 0;
            if (o != null && o.soldados != null) foreach (var s in o.soldados) if (s != null) n++;
            return n;
        }

        // ---------------------------------------------------------------
        // H0 -> H1: la toma del jefe
        // ---------------------------------------------------------------
        IEnumerator TomaDelJefe()
        {
            TomaDelJefeActiva = true;
            var cine = CineDeHuida.Abrir(true);
            if (cine == null || cine.Camara == null) { CineDeHuida.Cancelar(); TomaDelJefeActiva = false; TomaDelJefeHecha = true; EntrarSubfase((int)SubfaseHuida.Jefe); yield break; }
            var objetivo = tanque.transform.position + Vector3.up * 2.2f;
            var p0 = tanque.transform.position + new Vector3(-16f, 6f, -44f);
            var p1 = tanque.transform.position + new Vector3(-9f, 3.4f, -27f);
            float fov0 = 46f, fov1 = 36f;
            AudioDirector.PlayUi2D(SfxKind.RadialConfirm, 0.5f, 0.7f);
            float t = 0f;
            while (t < SegundosDeLaToma)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / SegundosDeLaToma);
                float e = k * k * (3f - 2f * k);
                var pos = Vector3.Lerp(p0, p1, e);
                cine.PonerCamara(pos, Quaternion.LookRotation(objetivo - pos, Vector3.up), Mathf.Lerp(fov0, fov1, e));
                cine.Barras(Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((SegundosDeLaToma - t) / 0.4f));
                float alfa = Mathf.Clamp01((t - 0.5f) / 0.4f) * Mathf.Clamp01((SegundosDeLaToma - 0.3f - t) / 0.3f);
                cine.TarjetaDeJefe(TanqueJefe.NombreDelJefe, "Blindaje pesado · solo lo dañan cohetes y explosivos", alfa);
                yield return null;
            }
            CineDeHuida.Cancelar();
            TomaDelJefeActiva = false; TomaDelJefeHecha = true;
            EntrarSubfase((int)SubfaseHuida.Jefe);
            ProgramarAutoguardado("fin CineDeHuida (toma del jefe)", 0.6f);   // P10 (#120)
        }

        // ---------------------------------------------------------------
        // H1: el jefe
        // ---------------------------------------------------------------
        Vector3[] PuntosDePatrulla()
        {
            if (patrullaDelJefe != null && patrullaDelJefe.Length > 0)
            {
                var r = new List<Vector3>();
                foreach (var t in patrullaDelJefe) if (t != null) r.Add(t.position);
                if (r.Count > 0) return r.ToArray();
            }
            return new[] { new Vector3(-14f, 0.6f, 12f), new Vector3(14f, 0.6f, 12f), new Vector3(14f, 0.6f, 30f), new Vector3(-14f, 0.6f, 30f) };
        }

        void IniciarJefe()
        {
            CineDeHuida.Cancelar();
            TomaDelJefeActiva = false;
            if (tanque == null) return;
            if (jefe == null || jefe.Vehiculo != tanque) PrepararHuida();
            if (jefe.Situacion == TanqueJefe.Estado.Dormido) jefe.Despertar(PuntosDePatrulla());
            jefe.AlTerminarDeRendirse -= AlBajarLaTripulacionDelJefe;
            jefe.AlTerminarDeRendirse += AlBajarLaTripulacionDelJefe;
            if (cajasDelJefe.Count == 0 && cajasDeCohetes != null)
                foreach (var t in cajasDeCohetes) if (t != null) cajasDelJefe.Add(CajaDeSuministros.Crear(t.position, segundosDeRecargaDeCajas));
            PonerBaliza("BLINDADO", ColorDelJefe, tanque.transform.position, tanque.transform, 3f);
            Aviso(TanqueJefe.NombreDelJefe + "\nDERRIBALO CON COHETES O CARGAS · RECARGA EN LAS CAJAS");
            if (hud != null) hud.BarraDeJefe(TanqueJefe.NombreDelJefe, jefe.Fraccion01);
        }

        void AlBajarLaTripulacionDelJefe()
        {
            if (Fase == FaseOperacion.Huir && Subfase == (int)SubfaseHuida.Jefe) EntrarSubfase((int)SubfaseHuida.Reparacion);
        }

        // ---------------------------------------------------------------
        // H2: la reparacion
        // ---------------------------------------------------------------
        void IniciarReparacion()
        {
            if (tanque == null) return;
            if (jefe == null || jefe.Vehiculo != tanque) PrepararHuida();
            if (jefe.Situacion != TanqueJefe.Estado.Rendido) jefe.RendirYa();
            if (!jefe.TripulacionBajo) jefe.BajarTripulacion();
            jefe.AlTerminarDeRendirse -= AlBajarLaTripulacionDelJefe;
            if (hud != null) hud.OcultarBarraDeJefe();
            // Se anula cualquier reparacion previa y se empieza de cero.
            if (reparacion != null) { reparacion.AlCompletar -= TerminarReparacion; Destroy(reparacion.gameObject); }
            ReparacionCompletada = false;
            oleadaNorteLanzada = false; oleadaEsteLanzada = false; oleadaOesteLanzada = false;
            OleadasDeReparacionLanzadas = 0; EnemigosDeLasOleadas = 0;
            reparacion = ReparacionDeTanque.Iniciar(tanque);
            reparacion.AlCompletar += TerminarReparacion;
            PonerBaliza("REPARANDO", VerdeHuida, tanque.transform.position, tanque.transform, 3.4f);
            // No hay sistema de dialogos con voz: la linea de Kes sale como subtitulo central + campana.
            AvisoCentral.Mostrar("KES: " + ReparacionDeTanque.AvisoDeInicio, 4.5f, new Color(0.08f, 0.3f, 0.2f, 0.92f));
            Aviso("EL BLINDADO ESTA FUERA DE COMBATE\nKES LO REPARA: CUBRILA");
            AudioDirector.PlayUi2D(SfxKind.RadialConfirm, 0.6f, 1.1f);
            SesionLog.Evento("HUIDA: reparacion del blindado empieza");
        }

        void LanzarCamionetaDeLaReparacion()
        {
            if (autoPlantilla == null) return;
            var rotSur = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var salida = BuscarLugarParaCamioneta(new Vector3(4f, 0.6f, 96f), rotSur);
            var parada = BuscarLugarParaCamioneta(new Vector3(7f, 0.6f, 34f), rotSur);
            var go = InstanciarCamioneta(salida, rotSur);
            go.name = "Camioneta_Reparacion_" + (++CamionetasDeReparacion);
            var a = go.GetComponent<OperacionAuto>();
            a.ConfigurarAsalto(parada);
            camionetasDeLaReparacion.Add(a);
            AlertQueue.Push("¡UNA CAMIONETA ENEMIGA ENTRA AL PATIO!", AlertPriority.Alta, 2.6f);
        }

        // El tanque pasa a ser nuestro: bando Player, 70 % de vida, piso del 20 % durante la carrera.
        void EntregarTanqueAlJugador()
        {
            if (jefe != null) { jefe.AlTerminarDeRendirse -= AlBajarLaTripulacionDelJefe; jefe.Soltar(); }
            var motor = tanque.GetComponent<VehicleMotor>();
            if (motor != null && velocidadInicial > 0f) motor.Configurar(velocidadInicial, aceleracionInicial);
            tanque.AsignarBando(TeamId.Player, Color.white);
            tanque.PonerVida01(ReparacionDeTanque.VidaFinal01);
            tanque.PisoDeVida01 = PisoDeVidaDelTanque;
            AnilloDeAviso.CancelarTodos();
            if (hud != null) hud.OcultarBarraDeJefe();
        }

        void TerminarReparacion()
        {
            ReparacionCompletada = true;
            EntregarTanqueAlJugador();
            AvisoCentral.Mostrar("BLINDADO REPARADO · VIDA " + Mathf.RoundToInt(ReparacionDeTanque.VidaFinal01 * 100f) + "%", 3f, new Color(0.08f, 0.3f, 0.2f, 0.92f));
            AudioDirector.PlayUi2D(SfxKind.ObjetivoCumplido, 0.8f, 1f);
            SesionLog.Evento("HUIDA: blindado reparado");
            EntrarSubfase((int)SubfaseHuida.Abordaje);
        }

        // ---------------------------------------------------------------
        // H3: abordaje
        // ---------------------------------------------------------------
        void IniciarAbordaje()
        {
            if (tanque == null) return;
            CineDeHuida.Cancelar();
            if (jefe == null || jefe.Vehiculo != tanque) PrepararHuida();
            SaltearJefeYReparacion();
            PonerBaliza("TANQUE", VerdeHuida, tanque.transform.position, tanque.transform, 2.6f);
            Aviso("BLINDADO REPARADO\nSUBI AL TANQUE CON [E]");
        }

        // Deja hechos el jefe y la reparacion (saltos de prueba, Embarcar() directo, restauraciones): tripulacion enemiga fuera, tanque del jugador.
        public void SaltearJefeYReparacion()
        {
            if (tanque == null) return;
            if (reparacion != null) { reparacion.AlCompletar -= TerminarReparacion; Destroy(reparacion.gameObject); reparacion = null; }
            foreach (var o in new List<Soldier>(tanque.Occupants))
                if (o != null && o.Team == TeamId.Enemy) { tanque.Dismount(o); o.gameObject.SetActive(false); }
            if (tripulacionDelJefe != null)
                foreach (var s in tripulacionDelJefe) if (s != null && s.Health != null && s.Health.IsAlive && !TripulacionBajoALaCalle(s)) s.gameObject.SetActive(false);
            EntregarTanqueAlJugador();
            ReparacionCompletada = true;
        }

        // Un tripulante que ya bajo a pelear (esta activo y fuera del tanque) se deja donde esta.
        bool TripulacionBajoALaCalle(Soldier s) => jefe != null && jefe.TripulacionBajo && s.gameObject.activeInHierarchy;

        // Fin de la carrera / carrera interrumpida: se quitan las camionetas del patio y las cajas.
        void QuitarRestosDelPatio()
        {
            foreach (var a in camionetasDeLaReparacion) if (a != null && !a.Muerto) Destroy(a.gameObject);
            camionetasDeLaReparacion.Clear();
            foreach (var c in cajasDelJefe) if (c != null) Destroy(c.gameObject);
            cajasDelJefe.Clear();
            if (hud != null) hud.OcultarBarraDeJefe();
        }

        // SaltarA con subfase: la escuadra queda junto al patio, segun donde arranque.
        void AcomodarEscuadraHuida(int subfase)
        {
            float z = subfase <= 1 ? -12f : (subfase == 2 ? 2f : 10f);
            TeletransportarEscuadra(new Vector3(0f, 0f, z), Quaternion.identity);
        }
    }
}
