using System;
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
    // WP9b (#097): el BLINDADO GOLIAT, jefe del objetivo 4. Es el tanque de la fuga en manos enemigas (bando Enemy, Driver + Passenger2: nunca
    // Gunner, la TurretAI generica lo trata como humano y se calla) con 1600 de vida y un PISO del 5 %: no baja de ahi y, al llegar, se rinde.
    //   * Patrulla 4 puntos del patio a 4 m/s (VehicleBrain) mientras la torreta apunta al soldado mas cercano.
    //   * CAÑONAZO cada 5 s con aviso en el piso (AnilloDeAviso): 1,5 s antes cae un circulo rojo de 5,5 m sobre el soldado; 120 de dano en el
    //     centro, con caida hacia el borde. La escuadra lo esquiva corriendo.
    //   * Ametralladora coaxial: 8 por bala contra quien este a menos de 30 m.
    //   * Dano efectivo: cohetes (237), granadas y explosiones en general, y la CARGA ADOSADA del Asalto (600; [E] 2,5 s pegado al tanque, solo si
    //     esta quieto o a 3 m o menos). Una bala directa hace 1 y avisa "NECESITAS COHETES O EXPLOSIVOS".
    // Al llegar al piso: frena, humo negro, y a los 2 s la tripulacion baja a pie (AlTerminarDeRendirse). Despues el director lo hace del jugador.
    public class TanqueJefe : MonoBehaviour
    {
        public const string NombreDelJefe = "BLINDADO GOLIAT";
        public const int VidaMaxima = 1600;
        public const float PisoDeVida01 = 0.05f;
        public const float RadioDelCanon = 5.5f;
        public const int DanoDelCanon = 120;
        public const float SegundosEntreCanonazos = 5f, SegundosDeAviso = 1.5f, AlcanceDelCanon = 75f;
        public const float AlcanceDeMetralla = 30f;
        public const int DanoDeMetralla = 8;
        public const float VelocidadDePatrulla = 4f;
        public const string CartelDeBalas = "NECESITAS COHETES O EXPLOSIVOS";
        // Carga adosada del Asalto.
        public const float SegundosParaAdosar = 2.5f, RadioParaAdosar = 3.5f, DistanciaSiSeMueve = 3f, SegundosDeMechaAdosada = 3f;
        public const int DanoDeCargaAdosada = 600;
        public const float SegundosAntesDeBajarLaTripulacion = 2f;
        // Un cohete que pega en el casco hace ~86 (la explosion cae en la superficie, lejos del centro del vehiculo, y cae con la distancia):
        // contra 1600 de vida serian 18 cohetes. La explosion de arma pesada cuenta x2,2 contra el jefe (blindaje debil a explosivos): ~8 cohetes.
        public const float FactorDeExplosiones = 2.2f;

        public enum Estado { Dormido, Peleando, Rendido }

        public static TanqueJefe Instancia { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Instancia = null;

        public Estado Situacion { get; private set; } = Estado.Dormido;
        public Vehicle Vehiculo { get; private set; }
        public int Canonazos { get; private set; }
        public int BalasDeMetralla { get; private set; }
        public int BalasAbsorbidas { get; private set; }
        public int CartelesDeBalas { get; private set; }
        public int CargasAdosadas { get; private set; }
        public float VelocidadActual => motor != null ? Mathf.Abs(motor.CurrentSpeed) : 0f;
        public bool TripulacionBajo { get; private set; }
        public AnilloDeAviso AnilloActual => anillo;
        public Vector3 PuntoDeImpacto { get; private set; }
        public float ProgresoDeCarga01 { get; private set; }
        public float Fraccion01 => Vehiculo != null && Vehiculo.Health != null ? Vehiculo.Health.Current / (float)Vehiculo.Health.MaxHealth : 0f;
        public event Action AlRendirse;
        public event Action AlTerminarDeRendirse;
        // Historial para los checks: cuando empezo cada aviso y cuando cayo cada cañonazo (Time.time).
        public readonly List<float> InicioDeAvisos = new List<float>();
        public readonly List<float> InstantesDeImpacto = new List<float>();

        VehicleBrain brain;
        VehicleMotor motor;
        TurretWeapon canon, metralleta;
        TurretAI aiCanon;
        AnilloDeAviso anillo;
        float proximoCanonazo, impactoEn = -1f, rafagaHasta, pausaHasta, proximoTiroMg, proximoHumo, rendidoDesde = -1f, proximoCartel;
        bool fogonazoHecho, cargaEnCurso;
        Vector3[] patrulla;
        // Carga adosada.
        float mechaAdosada = -1f;
        Soldier quienAdosa;

        public static TanqueJefe Preparar(Vehicle v)
        {
            var j = v.GetComponent<TanqueJefe>();
            if (j == null) j = v.gameObject.AddComponent<TanqueJefe>();
            j.Vehiculo = v;
            j.motor = v.GetComponent<VehicleMotor>();
            j.brain = v.GetComponent<VehicleBrain>();
            j.canon = v.TorretaCanon;
            j.metralleta = v.TorretaMetralleta;
            foreach (var ai in v.GetComponentsInChildren<TurretAI>(true)) if (ai.Asiento == VehicleSeatRole.Gunner) { j.aiCanon = ai; break; }
            if (j.aiCanon != null) j.aiCanon.Silenciada = true;   // dormido: no dispara hasta Despertar
            Instancia = j;
            return j;
        }

        // Vuelve a "dormido y quieto" (entrada de la fase, antes de la toma): sin patrulla ni fuego, con el filtro de dano ya puesto.
        public void Reposo()
        {
            Situacion = Estado.Dormido;
            TripulacionBajo = false; rendidoDesde = -1f; mechaAdosada = -1f;
            CancelarAnillo();
            if (Vehiculo != null) { Vehiculo.PisoDeVida01 = PisoDeVida01; Vehiculo.AjusteDeDano = Filtro; }
            if (aiCanon != null) aiCanon.Silenciada = true;
            if (brain != null) { brain.ConfigurarPatrulla(null, false, 1f); brain.Stop(); }
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        // Empieza la pelea: piso del 5 %, filtro de dano, patrulla.
        public void Despertar(Vector3[] puntosDePatrulla)
        {
            if (Vehiculo == null) return;
            Situacion = Estado.Peleando;
            TripulacionBajo = false; rendidoDesde = -1f; mechaAdosada = -1f;
            Vehiculo.PisoDeVida01 = PisoDeVida01;
            Vehiculo.AjusteDeDano = Filtro;
            if (aiCanon != null) aiCanon.Silenciada = true;
            if (motor != null) motor.Configurar(VelocidadDePatrulla, 6f);
            patrulla = puntosDePatrulla;
            if (brain != null && patrulla != null && patrulla.Length > 0) { brain.ConfigurarPatrulla(patrulla, false, 1f); }
            proximoCanonazo = Time.time + 1.5f;
            impactoEn = -1f;
            CancelarAnillo();
            SesionLog.Evento("JEFE: BLINDADO GOLIAT despierta");
        }

        // Para reinicios y para cuando el director lo cede al jugador: vuelve a ser un tanque sin filtro ni piso propio.
        public void Soltar()
        {
            Situacion = Estado.Dormido;
            CancelarAnillo();
            mechaAdosada = -1f;
            if (Vehiculo != null) { Vehiculo.AjusteDeDano = null; Vehiculo.PisoDeVida01 = 0f; }
            if (aiCanon != null) aiCanon.Silenciada = false;
            if (brain != null) { brain.ConfigurarPatrulla(null, false, 1f); brain.Stop(); }
        }

        void CancelarAnillo() { if (anillo != null) anillo.Cancelar(); anillo = null; impactoEn = -1f; }

        // Solo explosiones y golpes grandes pasan; la bala directa hace 1 (con cartel).
        int Filtro(int dano)
        {
            if (Vehicle.DanoDeExplosionEnCurso) return cargaEnCurso ? dano : Mathf.RoundToInt(dano * FactorDeExplosiones);
            BalasAbsorbidas++;
            if (Time.time >= proximoCartel)
            {
                proximoCartel = Time.time + 4f;
                CartelesDeBalas++;
                AvisoCentral.Mostrar(CartelDeBalas, 2.2f, new Color(0.45f, 0.06f, 0.05f, 0.9f));
            }
            return 1;
        }

        static Soldier BlancoMasCercano(Vector3 desde, float max)
        {
            var s = ActorRegistry.FindNearest(desde, x => x.Team == TeamId.Player && x.Role != RoleType.Civilian && x.Health != null && x.Health.IsAlive && x.gameObject.activeInHierarchy);
            if (s == null) return null;
            var d = s.transform.position - desde; d.y = 0f;
            return d.magnitude <= max ? s : null;
        }

        void Update()
        {
            if (Situacion == Estado.Dormido || Vehiculo == null) return;
            float dt = Time.deltaTime;
            if (Vehiculo.IsDestroyed) { CancelarAnillo(); return; }
            if (Situacion == Estado.Rendido) { TickRendido(dt); return; }

            if (Vehiculo.Health.Current <= Vehiculo.PisoDeVida) { Rendirse(); return; }
            TickCarga(dt);
            if (CinematicaDeOperacion.IntroActiva || SP.Ai.AiBrain.IAPausada) return;   // durante las tomas no dispara

            var blanco = BlancoMasCercano(transform.position, AlcanceDelCanon);
            // Cañon: aviso en el piso y golpe.
            if (impactoEn >= 0f)
            {
                if (canon != null) canon.AimAt(PuntoDeImpacto + Vector3.up * 0.5f, dt);
                float t = Time.time - (impactoEn - SegundosDeAviso);
                if (!fogonazoHecho && t >= SegundosDeAviso - 0.3f) { FogonazoDeBoca(); fogonazoHecho = true; }
                if (Time.time >= impactoEn) Impactar();
            }
            else
            {
                if (blanco != null && canon != null) canon.AimAt(blanco.transform.position + Vector3.up * 0.8f, dt);
                if (blanco != null && Time.time >= proximoCanonazo) EmpezarAviso(blanco);
            }
            TickMetralla(dt);
        }

        void EmpezarAviso(Soldier blanco)
        {
            var p = blanco.transform.position; p.y -= 0.8f;
            PuntoDeImpacto = p;
            anillo = AnilloDeAviso.Mostrar(p, RadioDelCanon, SegundosDeAviso);
            InicioDeAvisos.Add(Time.time);
            impactoEn = Time.time + SegundosDeAviso;
            proximoCanonazo = Time.time + SegundosEntreCanonazos;
            fogonazoHecho = false;
        }

        void FogonazoDeBoca()
        {
            var boca = canon != null && canon.Muzzle != null ? canon.Muzzle.position : transform.position + Vector3.up * 2f;
            ImpactFx.Spawn(boca, new Color(1f, 0.75f, 0.35f), 0.9f, 0.14f);
            MuzzleLightPool.Flash(boca, new Color(1f, 0.75f, 0.35f));
            AudioDirector.PlayAt(SfxKind.CannonBody, boca, 1f, 0.95f);
            AudioDirector.PlayAt(SfxKind.CannonCrack, boca, 0.3f, 0.9f);
        }

        void Impactar()
        {
            impactoEn = -1f;
            Canonazos++;
            InstantesDeImpacto.Add(Time.time);
            // El obus cae sobre el circulo (centro = punto del aviso). La explosion perdona a los enemigos y reparte con caida hacia el borde.
            Projectile.ExplodeAt(PuntoDeImpacto + Vector3.up * 0.4f, RadioDelCanon, DanoDelCanon, -3, TeamId.Enemy, Vehiculo);
            anillo = null;
        }

        // Ametralladora coaxial: rafagas de 10 a 0,1 s contra el mas cercano a menos de 30 m.
        void TickMetralla(float dt)
        {
            if (metralleta == null) return;
            var blanco = BlancoMasCercano(metralleta.transform.position, AlcanceDeMetralla);
            if (blanco == null) return;
            var objetivo = blanco.transform.position + Vector3.up * 0.9f;
            metralleta.AimAt(objetivo, dt);
            if (!metralleta.IsAimedAt(objetivo, 8f)) return;
            // Rafaga de 1 s, pausa de 0,8..1,4 s (la pausa corre DESPUES de la rafaga: antes se cortaba al empezar).
            if (Time.time >= pausaHasta && Time.time >= rafagaHasta) { rafagaHasta = Time.time + 1.0f; pausaHasta = rafagaHasta + UnityEngine.Random.Range(0.8f, 1.4f); }
            if (Time.time >= proximoTiroMg && Time.time < rafagaHasta)
            {
                proximoTiroMg = Time.time + 0.1f;
                var pool = ProjectilePool.Activo;
                if (pool == null) return;
                var origen = metralleta.Muzzle != null ? metralleta.Muzzle.position : metralleta.transform.position;
                var dir = (objetivo - origen).normalized;
                dir = (dir + UnityEngine.Random.insideUnitSphere * 0.045f).normalized;
                pool.Spawn(origen, dir, -3, TeamId.Enemy, DanoDeMetralla, new Color(1f, 0.4f, 0.2f));
                MuzzleLightPool.Flash(origen, new Color(1f, 0.6f, 0.3f), 4f, 7f);
                BalasDeMetralla++;
                if ((BalasDeMetralla & 1) == 0) AudioDirector.PlayClipAt(GenericSfx.GetWeaponShot(WeaponKind.Heavy), origen, 0.7f, 0.8f, PerfilEspacial.Disparo);
            }
        }

        // ---- Carga adosada del Asalto ----
        void TickCarga(float dt)
        {
            var hud = OperacionHud.Instancia;
            if (mechaAdosada >= 0f)
            {
                mechaAdosada -= dt;
                if (hud != null) hud.Prompt($"CARGA ADOSADA · ESTALLA EN {Mathf.CeilToInt(Mathf.Max(0f, mechaAdosada))} s · ALEJATE", 1f - Mathf.Clamp01(mechaAdosada / SegundosDeMechaAdosada));
                if (mechaAdosada <= 0f) DetonarCarga();
                return;
            }
            var yo = Poseido();
            if (yo == null || yo.Health == null || !yo.Health.IsAlive || yo.Role != RoleType.Assault) { ProgresoDeCarga01 = 0f; return; }
            float d = Plano(yo.transform.position, transform.position).magnitude;
            bool quieto = VelocidadActual < 1.2f;
            bool cerca = quieto ? d <= RadioParaAdosar + 1.5f : d <= DistanciaSiSeMueve;
            if (!cerca) { ProgresoDeCarga01 = Mathf.Max(0f, ProgresoDeCarga01 - dt * 0.6f); return; }
            bool sosteniendo = OperacionTerminal.EMantenida();
            if (hud != null) hud.Prompt(sosteniendo ? $"ADOSANDO LA CARGA · {Mathf.RoundToInt(ProgresoDeCarga01 * 100f)} %" : "MANTENE [E] PARA ADOSAR UNA CARGA AL BLINDADO", ProgresoDeCarga01);
            if (sosteniendo)
            {
                AccionesEnCurso.Reportar(yo, "DETONANDO", transform.position, ProgresoDeCarga01, (1f - ProgresoDeCarga01) * SegundosParaAdosar, transform);
                ProgresoDeCarga01 = Mathf.Clamp01(ProgresoDeCarga01 + dt / SegundosParaAdosar);
                if (ProgresoDeCarga01 >= 1f) { quienAdosa = yo; AdosarCarga(); }
            }
            else ProgresoDeCarga01 = Mathf.Max(0f, ProgresoDeCarga01 - dt * 0.6f);
        }

        // Para las pruebas: ados a la carga sin pasar por los 2,5 s de [E].
        public void AdosarCarga()
        {
            if (mechaAdosada >= 0f || Situacion != Estado.Peleando) return;
            mechaAdosada = SegundosDeMechaAdosada;
            CargasAdosadas++;
            ProgresoDeCarga01 = 0f;
            AudioDirector.PlayAt(SfxKind.BombPlant, transform.position, 0.9f, 1f);
            AlertQueue.Push("CARGA ADOSADA AL BLINDADO · ESTALLA EN " + Mathf.CeilToInt(SegundosDeMechaAdosada) + " s", AlertPriority.Alta, 2.2f);
        }

        void DetonarCarga()
        {
            mechaAdosada = -1f;
            var centro = transform.position + Vector3.up * 1.2f;
            ImpactFx.SpawnExplosion(centro, 6f, false);
            AudioDirector.PlayAt(SfxKind.Explosion, centro, 1f, 1f, PerfilEspacial.Explosion);
            // Cuenta como explosion: pasa el filtro del jefe. La escuadra no se lastima (la carga es suya).
            Vehicle.DanoDeExplosionEnCurso = true; cargaEnCurso = true;
            try { Vehiculo.TakeDamage(DanoDeCargaAdosada, quienAdosa != null ? quienAdosa.Id : -1); }
            finally { Vehicle.DanoDeExplosionEnCurso = false; cargaEnCurso = false; }
        }

        // ---- Rendicion ----
        void Rendirse()
        {
            Situacion = Estado.Rendido;
            rendidoDesde = Time.time;
            CancelarAnillo();
            mechaAdosada = -1f;
            if (brain != null) { brain.ConfigurarPatrulla(null, false, 1f); brain.Stop(); }
            AlertQueue.Push("¡EL BLINDADO ESTA FUERA DE COMBATE!", AlertPriority.Alta, 2.5f);
            AudioDirector.PlayAt(SfxKind.ImpactoPesado, transform.position, 1f, 1f);
            SesionLog.Evento("JEFE: BLINDADO GOLIAT se rinde");
            AlRendirse?.Invoke();
        }

        void TickRendido(float dt)
        {
            if (motor != null && !motor.IsStopped) motor.Brake(dt);
            // Humo negro y chispas mientras espera.
            if (Time.time >= proximoHumo)
            {
                proximoHumo = Time.time + 0.2f;
                var p = transform.position + Vector3.up * 2.2f + new Vector3(UnityEngine.Random.Range(-0.7f, 0.7f), 0f, UnityEngine.Random.Range(-0.9f, 0.9f));
                SpriteFx.Lanzar("smoke_04", p, new Color(0.12f, 0.11f, 0.11f, 0.85f), 1.0f, 4.2f, 3.0f, UnityEngine.Random.Range(-25f, 25f), Vector3.up * UnityEngine.Random.Range(1.8f, 3.0f), 0.3f, 2);
                if (UnityEngine.Random.value < 0.35f) SpriteFx.Lanzar("fire_02", p, new Color(1f, 0.5f, 0.15f, 0.9f), 0.5f, 1.1f, 0.45f, UnityEngine.Random.Range(-50f, 50f), Vector3.up * 0.8f, 0.5f, 3);
            }
            if (!TripulacionBajo && Time.time - rendidoDesde >= SegundosAntesDeBajarLaTripulacion) BajarTripulacion();
        }

        public void BajarTripulacion()
        {
            if (TripulacionBajo) return;
            TripulacionBajo = true;
            var bajan = new List<Soldier>();
            foreach (var o in new List<Soldier>(Vehiculo.Occupants)) { if (Vehiculo.Dismount(o)) bajan.Add(o); }
            foreach (var s in bajan) { if (s.Brain != null) { s.Brain.enabled = true; s.Brain.ReactivarNavegacion(); } }
            CazaDeEnemigos.Marcar(bajan);
            AlertQueue.Push("LA TRIPULACION BAJA DEL BLINDADO", AlertPriority.Alta, 2.2f);
            AlTerminarDeRendirse?.Invoke();
        }

        // Para las pruebas y los saltos de subfase: lo deja directo en el piso, ya rendido y con la tripulacion abajo.
        public void RendirYa()
        {
            if (Situacion == Estado.Dormido) Despertar(patrulla);
            Vehiculo.PonerVida01(PisoDeVida01);
            if (Situacion == Estado.Peleando) Rendirse();
            rendidoDesde = Time.time - SegundosAntesDeBajarLaTripulacion;
        }

        static Soldier Poseido()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        static Vector3 Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return a - b; }
    }
}
