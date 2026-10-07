using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // WP9b-2 (#097 / #073): la carrera (emboscadas del canadon, helicoptero Halcon) y la cinematica final en camara lenta.
    public partial class OperacionDirector
    {
        [Header("4c. Carrera y final (WP9b-2)")]
        public Transform halcon;                       // plantilla INACTIVA del HALCON (HelicopteroEnemigo + Vehicle + visuales)
        public EmboscadaDeCohetes[] emboscadas;        // 2 emboscadas de 3 tiradores con lanzacohetes en cornisas de 6 m

        public const float SegundosDeCamaraLenta = 4f, EscalaDeCamaraLenta = 0.25f, VentanaDeFuego = 2f;
        public const float SegundosDeTomaBaja = 2.8f, SegundosDePanoramica = 2.7f;

        public HelicopteroEnemigo Halcon { get; private set; }
        public int HalconesCreados { get; private set; }
        public int HalconesDerribados { get; private set; }
        bool halconLanzado;

        // ---- Estado de la cinematica final (para los checks) ----
        public bool FinalActiva { get; private set; }
        public bool FinalTerminada { get; private set; }
        public float FinalSegundosLentos { get; private set; }        // tiempo REAL con timeScale 0.25
        public float FinalSegundosTotales { get; private set; }       // tiempo real de toda la cinematica
        public bool FinalOtroHelicoptero { get; private set; }
        public bool FinalDisparo { get; private set; }
        public bool FinalDisparoAutomatico { get; private set; }
        public float FinalEscalaMinima { get; private set; } = 1f;
        public int FinalEnPieAlTerminar { get; private set; }
        public float FinalDistanciaMaximaDeLaEscuadra { get; private set; }
        public Vector3 FinalPosicionDelTanque { get; private set; }
        public string FinalSubtitulo { get; private set; } = "";
        public int FinalSegundoDeInicio => 0;
        public static bool PruebaDisparar;                           // costura de pruebas (el editor sin foco pierde los eventos de teclado)
        readonly List<Health> invulnerables = new List<Health>();

        // ---------------------------------------------------------------
        // Carrera
        // ---------------------------------------------------------------
        void IniciarCarrera()
        {
            halconLanzado = false;
            HalconesDerribados = 0;
            if (Halcon != null) Destroy(Halcon.gameObject);
            Halcon = null;
            if (emboscadas != null) foreach (var e in emboscadas) if (e != null) e.Armar(tanque);
        }

        void TickCarreraExtras()
        {
            if (!halconLanzado && Reloj >= HelicopteroEnemigo.SegundoDeEntrada && halcon != null)
            {
                halconLanzado = true;
                Halcon = HelicopteroEnemigo.Iniciar(halcon, tanque);
                if (Halcon != null)
                {
                    HalconesCreados++;
                    Halcon.AlDerribarse += () => HalconesDerribados++;
                    Aviso("¡UN HELICOPTERO ENEMIGO!\nTIENE UN LIMITE DE ELEVACION: APUNTA ARRIBA CON EL CANON");
                }
            }
            if (hud == null) return;
            if (Halcon != null && !Halcon.Derribado) hud.BarraDeJefe(HelicopteroEnemigo.Nombre, Halcon.Fraccion01);
            else hud.OcultarBarraDeJefe();
        }

        public int EmbosCohetesLanzados { get { int n = 0; if (emboscadas != null) foreach (var e in emboscadas) if (e != null) n += e.CohetesLanzados; return n; } }
        public int EmbosTiradoresVivos { get { int n = 0; if (emboscadas != null) foreach (var e in emboscadas) if (e != null) n += e.TiradoresVivos; return n; } }

        // ---------------------------------------------------------------
        // Final
        // ---------------------------------------------------------------
        void IniciarCinematicaFinal()
        {
            if (Subfase == (int)SubfaseHuida.CinematicaFinal) return;
            EntrarSubfase((int)SubfaseHuida.CinematicaFinal);
            if (hud != null) { hud.OcultarTimer(); hud.OcultarBarraDeJefe(); }
            StartCoroutine(CinematicaFinal());
        }

        // Aborta una cinematica en curso (saltos de prueba): devuelve el tiempo, el control y la vulnerabilidad.
        void FinalAbortar()
        {
            FinalActiva = false;
            CineDeHuida.Cancelar();
            foreach (var h in invulnerables) if (h != null) h.Invulnerable = false;
            invulnerables.Clear();
        }

        static bool QuiereDisparar()
        {
            if (PruebaDisparar) { PruebaDisparar = false; return true; }
            var m = Mouse.current; var k = Keyboard.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.fKey.wasPressedThisFrame)) return true;
            return false;
        }

        void QuitarAutosDeLaCarrera()
        {
            foreach (var a in autos)
            {
                if (a == null || a.Muerto) continue;
                var camara = CamaraPrincipal.Actual;
                bool cerca = camara != null && Vector3.Distance(camara.transform.position, a.transform.position) < 60f;
                if (cerca && !a.Retirado) ImpactFx.SpawnExplosion(a.transform.position + Vector3.up, 3f);
                Destroy(a.gameObject);
            }
            autos.Clear();
        }

        IEnumerator CinematicaFinal()
        {
            FinalActiva = true; FinalTerminada = false;
            FinalSegundosLentos = 0f; FinalDisparo = false; FinalDisparoAutomatico = false; FinalOtroHelicoptero = false; FinalEscalaMinima = 1f;
            FinalSubtitulo = "";
            float inicioReal = Time.realtimeSinceStartup;
            var motor = tanque.GetComponent<VehicleMotor>();
            var vb = tanque.GetComponent<VehicleBrain>();
            var canon = tanque.TorretaCanon;
            var squad = new List<Soldier>(tanque.Occupants);
            foreach (var s in Escuadra(false)) if (!squad.Contains(s)) squad.Add(s);
            invulnerables.Clear();
            foreach (var s in squad) if (s != null && s.Health != null) { s.Health.Invulnerable = true; invulnerables.Add(s.Health); }

            // Se retiran los perseguidores y las emboscadas; se corta todo lo que estuviera en el aire.
            foreach (var a in autos) if (a != null && !a.Muerto) a.Retirarse();
            if (emboscadas != null) foreach (var e in emboscadas) if (e != null) e.Silenciar();
            AnilloDeAviso.CancelarTodos();
            foreach (var c in new List<CoheteDeAviso>(CoheteDeAviso.Vivos)) if (c != null) Destroy(c.gameObject);
            if (baliza != null) { baliza.Quitar(); baliza = null; }

            var cine = CineDeHuida.Abrir(true);
            if (cine == null || cine.Camara == null)
            {
                // Sin camara: el cierre de siempre (el tanque llega, baja la escuadra y vuela).
                FinalAbortar();
                FinalActiva = false;
                BajarDelTanque();
                DestruirTanque();
                yield break;
            }
            if (vb != null) vb.Stop();

            // El helicoptero: el que sigue vivo, o uno nuevo si ya cayo el primero ("¡OTRO HELICOPTERO!").
            var fwd = tanque.transform.forward; fwd.y = 0f; fwd.Normalize();
            var posHeli = tanque.transform.position + fwd * 62f + Vector3.up * 15f;
            var heli = Halcon;
            if (heli == null || heli.Derribado || heli.Vehiculo == null || heli.Vehiculo.IsDestroyed)
            {
                heli = halcon != null ? HelicopteroEnemigo.Iniciar(halcon, tanque) : null;
                if (heli != null) { HalconesCreados++; Halcon = heli; FinalOtroHelicoptero = true; heli.transform.position = posHeli; heli.AlDerribarse += () => HalconesDerribados++; }
            }
            if (heli != null)
            {
                var haciaTanque = tanque.transform.position - posHeli; haciaTanque.y = 0f;
                heli.FijarParaLaCinematica(posHeli, Quaternion.LookRotation(haciaTanque.normalized, Vector3.up));
                heli.transform.position = posHeli;
            }
            if (hud != null) hud.OcultarBarraDeJefe();
            var objetivoHeli = heli != null ? heli.transform : null;
            var focoSiNoHay = posHeli;

            // ---- camara lenta ----
            float t0 = Time.realtimeSinceStartup;
            cine.Lento(EscalaDeCamaraLenta);
            bool dispararYa = false, heliVolo = false, tanqueVolo = false;
            CoheteDeAviso proyectil = null;
            var cohetesAlTanque = new List<CoheteDeAviso>();
            bool cohetesLanzados = false;
            float tHeliVolo = 0f;
            const float tCohetes = 0.4f, vueloCohetesMundo = 0.75f;
            float fovInicial = cine.CampoDeVisionOriginal;
            while (true)
            {
                float t = Time.realtimeSinceStartup - t0;
                if (t >= SegundosDeCamaraLenta) break;
                float dtR = Time.unscaledDeltaTime;
                FinalSegundosLentos = t;
                FinalEscalaMinima = Mathf.Min(FinalEscalaMinima, Time.timeScale);
                var posObj = objetivoHeli != null ? objetivoHeli.position : focoSiNoHay;
                if (motor != null && !tanqueVolo) motor.Brake(Time.deltaTime);
                if (canon != null && !tanqueVolo) canon.AimAt(posObj, dtR);

                // La camara: detras de la torreta, sobre el hombro del artillero, mirando hacia el helicoptero.
                if (!tanqueVolo && canon != null)
                {
                    var tf = canon.transform;
                    var cam = tf.position - tf.forward * 3.8f + Vector3.up * 1.9f + tf.right * 0.9f;
                    var mira = Vector3.Lerp(tf.position + tf.forward * 20f, posObj, 0.75f);
                    cine.PonerCamara(cam, Quaternion.LookRotation(mira - cam, Vector3.up), Mathf.Lerp(fovInicial, fovInicial * 0.72f, Mathf.Clamp01(t / SegundosDeCamaraLenta)));
                }
                cine.Barras(Mathf.Clamp01(t / 0.4f));

                // El helicoptero dispara cohetes lentos al tanque (llegan a los ~3,4 s reales).
                if (!cohetesLanzados && t >= tCohetes && heli != null)
                {
                    cohetesLanzados = true;
                    var boca = heli.transform.position + heli.transform.forward * 1.5f + Vector3.down * 0.8f;
                    for (int k = 0; k < 2; k++)
                    {
                        var blanco = tanque.transform.position + Vector3.up * 1.0f + tanque.transform.right * ((k == 0 ? -1f : 1f) * 1.1f);
                        cohetesAlTanque.Add(CoheteDeAviso.Lanzar(boca + heli.transform.right * ((k == 0 ? -1f : 1f) * 0.9f), blanco, vueloCohetesMundo, 0, 1f, false, false));
                    }
                }
                // ¡FUEGO!: ventana de 2 s; al apretar (o al vencer) sale el obus.
                if (t >= 0.4f && !FinalDisparo)
                {
                    float restante = VentanaDeFuego - (t - 0.4f);
                    cine.Cartel(FinalOtroHelicoptero && t < 1.0f ? "¡OTRO HELICÓPTERO!" : "¡FUEGO!", 1f, new Color(1f, 0.9f, 0.3f));
                    if (QuiereDisparar()) dispararYa = true;
                    if (restante <= 0f) { dispararYa = true; FinalDisparoAutomatico = true; }
                }
                if (dispararYa && !FinalDisparo)
                {
                    FinalDisparo = true;
                    cine.Cartel(null, 0f);
                    var boca = canon != null && canon.Muzzle != null ? canon.Muzzle.position : tanque.transform.position + Vector3.up * 2.4f;
                    ImpactFx.Spawn(boca, new Color(1f, 0.75f, 0.35f), 1.1f, 0.2f);
                    MuzzleLightPool.Flash(boca, new Color(1f, 0.75f, 0.35f));
                    AudioDirector.PlayAt(SfxKind.CannonBody, boca, 1f, 0.9f);
                    AudioDirector.PlayAt(SfxKind.CannonCrack, boca, 0.4f, 0.85f);
                    proyectil = CoheteDeAviso.Lanzar(boca, posObj, 0.3f, 0, 1f, false, false);
                    var gr = proyectil != null ? proyectil.transform : null;
                    if (gr != null) { gr.localScale = new Vector3(0.45f, 0.45f, 1.1f); }
                }
                // Impacto del obus: el helicoptero explota en el aire.
                if (FinalDisparo && !heliVolo && (proyectil == null || proyectil.Progreso01 >= 0.97f))
                {
                    heliVolo = true; tHeliVolo = t;
                    if (heli != null) heli.ExplotarEnElAire();
                    SesionLog.Evento("FINAL: el helicoptero explota");
                }
                // Los cohetes del helicoptero llegan al tanque: la escuadra baja ilesa y el tanque vuela por los aires.
                if (!tanqueVolo && heliVolo && t >= tHeliVolo + 0.15f && AlgunCoheteLlego(cohetesAlTanque, t))
                {
                    tanqueVolo = true;
                    DestruirTanqueDeLaCinematica(squad);
                }
                yield return null;
            }
            // Si el obus no llego a salir (no deberia), se resuelve igual.
            if (!heliVolo && heli != null) heli.ExplotarEnElAire();
            if (!tanqueVolo) DestruirTanqueDeLaCinematica(squad);
            foreach (var c in cohetesAlTanque) if (c != null) Destroy(c.gameObject);
            cine.Cartel(null, 0f);
            cine.TiempoNormal();
            FinalSegundosLentos = Time.realtimeSinceStartup - t0;

            // ---- toma baja: todos en pie ----
            var centro = CentroDe(squad);
            FinalSubtitulo = "¡Todos en pie! ¡A la plaza!";
            float tb = 0f;
            // La camara queda del lado de la escuadra que da la espalda a los restos: los tres en primer plano y el tanque ardiendo detras.
            var restos = FinalPosicionDelTanque;
            var alejar = centro - restos; alejar.y = 0f;
            if (alejar.sqrMagnitude < 0.5f) alejar = -tanque.transform.right;
            alejar.Normalize();
            var perp = new Vector3(-alejar.z, 0f, alejar.x);
            var posCam0 = centro + alejar * 9.5f + perp * 2.6f + Vector3.up * 1.1f;
            var posCam1 = centro + alejar * 6.6f + perp * 1.0f + Vector3.up * 1.4f;
            var foco = Vector3.Lerp(centro, restos, 0.3f) + Vector3.up * 1.5f;
            while (tb < SegundosDeTomaBaja)
            {
                tb += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(tb / SegundosDeTomaBaja);
                var cam = Vector3.Lerp(posCam0, posCam1, k);
                cine.PonerCamara(cam, Quaternion.LookRotation(foco - cam, Vector3.up), 48f);
                cine.Subtitulo(FinalSubtitulo, Mathf.Clamp01(tb / 0.3f));
                yield return null;
            }
            FinalEnPieAlTerminar = ContarEnPie(squad, out float distMax);
            FinalDistanciaMaximaDeLaEscuadra = distMax;

            // ---- panoramica hacia la ciudad ----
            Vector3 haciaPlaza = plaza != null ? plaza.position - centro : Vector3.right * 100f;
            haciaPlaza.y = 0f;
            float distPlaza = haciaPlaza.magnitude;
            if (plaza != null) PonerBaliza("LA PLAZA", new Color(1f, 0.55f, 0.25f), plaza.position, null, 3f);
            var rotHacia = Quaternion.LookRotation(haciaPlaza.sqrMagnitude > 1f ? haciaPlaza.normalized : Vector3.right, Vector3.up);
            float tp = 0f;
            var cam0 = posCam1;
            var rot0 = Quaternion.LookRotation(foco - cam0, Vector3.up);
            var camFin = centro - rotHacia * Vector3.forward * 5f + Vector3.up * 3.2f;
            var rotFin = rotHacia * Quaternion.Euler(8f, 0f, 0f);
            while (tp < SegundosDePanoramica)
            {
                tp += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(tp / SegundosDePanoramica);
                float e = k * k * (3f - 2f * k);
                cine.PonerCamara(Vector3.Lerp(cam0, camFin, e), Quaternion.Slerp(rot0, rotFin, e), Mathf.Lerp(48f, 56f, e));
                cine.Subtitulo($"LA PLAZA · {Mathf.RoundToInt(distPlaza)} m", 1f);
                yield return null;
            }

            // ---- cierre: se devuelve el control y empieza la defensa ----
            FinalSegundosTotales = Time.realtimeSinceStartup - inicioReal;
            foreach (var h in invulnerables) if (h != null) h.Invulnerable = false;
            invulnerables.Clear();
            CineDeHuida.Cancelar();
            FinalActiva = false; FinalTerminada = true;
            QuitarAutosDeLaCarrera();
            EnTanque = false;
            llegadaDesde = -1f;
            BajaronDelTanque = true;
            if (hud != null) hud.OcultarTimer();
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.VehicleStatus != null) { drv.VehicleStatus.UpdateFrom(null, null); drv.VehicleStatus.gameObject.SetActive(false); }
            var yo = Poseido();
            if (yo != null && entradas != null && entradas.Length > 4 && entradas[4] != null && Plano(yo.transform.position, entradas[4].position).magnitude > 12f)
                TeletransportarEscuadra(new Vector3(entradas[4].position.x - 6f, 0f, entradas[4].position.z), entradas[4].rotation);
            ProgramarAutoguardado("fin CineDeHuida (cinemática final)", 0.6f);   // P10 (#120): se combina con el "inicio objetivo 5" de EntrarFase
            EntrarFase(FaseOperacion.Resistir);
        }

        bool AlgunCoheteLlego(List<CoheteDeAviso> lista, float t)
        {
            if (lista.Count == 0) return t >= 3.4f;
            foreach (var c in lista) if (c == null || c.Progreso01 >= 0.97f) return true;
            return false;
        }

        static Vector3 CentroDe(List<Soldier> lista)
        {
            var c = Vector3.zero; int n = 0;
            foreach (var s in lista) if (s != null) { c += s.transform.position; n++; }
            return n > 0 ? c / n : Vector3.zero;
        }

        int ContarEnPie(List<Soldier> lista, out float distanciaMaxima)
        {
            int n = 0; distanciaMaxima = 0f;
            foreach (var s in lista)
            {
                if (s == null || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (tanque != null && tanque.Occupants.Contains(s)) continue;
                n++;
                distanciaMaxima = Mathf.Max(distanciaMaxima, Plano(s.transform.position, FinalPosicionDelTanque).magnitude);
            }
            return n;
        }

        // La escuadra baja sin un rasguño y queda parada junto al tanque; despues el tanque vuela por los aires (guion).
        void DestruirTanqueDeLaCinematica(List<Soldier> squad)
        {
            FinalPosicionDelTanque = tanque.transform.position;
            var drv = PlayerInputDriver.Activo;
            if (drv != null && drv.CurrentSeat.HasValue && drv.Vehicle == tanque) drv.ExitVehicle();
            foreach (var o in new List<Soldier>(tanque.Occupants)) tanque.Dismount(o);
            if (drv != null && drv.VehicleStatus != null) { drv.VehicleStatus.UpdateFrom(null, null); drv.VehicleStatus.gameObject.SetActive(false); }
            // Parados a 3,5..4,5 m del tanque, del lado opuesto a la camara, mirando hacia la ciudad.
            var fwdT = tanque.transform.forward; fwdT.y = 0f; fwdT.Normalize();
            var lado = tanque.transform.right; lado.y = 0f; lado.Normalize();
            int i = 0;
            foreach (var s in squad)
            {
                if (s == null) continue;
                s.gameObject.SetActive(true);
                var p = tanque.transform.position + lado * (-4.2f + (i % 2) * 0.6f) - fwdT * (0.8f + i * 1.7f - 1.7f);
                s.transform.position = new Vector3(p.x, s.transform.position.y, p.z);
                s.transform.rotation = Quaternion.LookRotation(fwdT, Vector3.up);
                ApoyoEnElPiso.Apoyar(s.transform);
                if (s.Brain != null) { s.Brain.enabled = true; s.Brain.CancelOrder(); s.Brain.ReactivarNavegacion(); }
                i++;
            }
            tanque.DestruirPorGuion();
            ImpactFx.SpawnExplosion(tanque.transform.position + Vector3.up, 6f, false);
            SesionLog.Evento("FINAL: el tanque explota, la escuadra queda en pie");
        }
    }
}
