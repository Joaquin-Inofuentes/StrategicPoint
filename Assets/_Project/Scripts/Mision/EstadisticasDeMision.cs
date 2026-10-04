using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Mision
{
    // Bug #064: "quiero que diga con cuantos termine vivos y metricas de cuantos eliminaron mis aliados y cuantas muertes tuve
    // y otros numeros". La pantalla final solo decia "Bajas enemigas / Bajas propias / Tiempo" (y en la Operacion salia
    // vacia). Esto cuenta en vivo, desde que carga la escena, quien mato a quien (vos = el soldado que manejabas en ese
    // momento, o tus aliados), cuantas veces cayo el soldado que manejabas, cuantos caidos tuvo la escuadra, reanimaciones,
    // vehiculos enemigos destruidos y tu punteria. GameOutcomeController arma las filas de la pantalla con Filas().
    public class EstadisticasDeMision : MonoBehaviour
    {
        public static EstadisticasDeMision Instancia { get; private set; }

        public int BajasPorMi { get; private set; }
        public int BajasPorAliados { get; private set; }
        public int BajasOtras { get; private set; }            // torretas, vehiculos o explosiones sin autor de la escuadra
        public int MisMuertes { get; private set; }            // veces que cayo el soldado que manejabas
        public int CaidosDeLaEscuadra { get; private set; }
        public int Reanimaciones { get; private set; }
        public int VehiculosDestruidos { get; private set; }
        public int Disparos { get; private set; }
        public int Impactos { get; private set; }
        public float Inicio { get; private set; }
        readonly Dictionary<string, int> bajasPorSoldado = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> BajasPorSoldado => bajasPorSoldado;
        public int BajasEnemigas => BajasPorMi + BajasPorAliados + BajasOtras;

        IDisposable subMuerte, subDisparo, subDano, subVehiculo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (Instancia != null) return;
            var go = new GameObject("EstadisticasDeMision");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<EstadisticasDeMision>();
        }

        void Awake()
        {
            Limpiar();
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        void OnDestroy() { SceneManager.sceneLoaded -= AlCargarEscena; }

        void AlCargarEscena(Scene s, LoadSceneMode m) { if (m == LoadSceneMode.Single) Limpiar(); }

        public void Limpiar()
        {
            BajasPorMi = BajasPorAliados = BajasOtras = MisMuertes = CaidosDeLaEscuadra = 0;
            Reanimaciones = VehiculosDestruidos = Disparos = Impactos = 0;
            bajasPorSoldado.Clear();
            SupervivientesForzados = -1; HeridosRescatados = 0;
            Inicio = Time.time;
        }

        void OnEnable()
        {
            subMuerte = EventBus.Instance.Subscribe<EntityDiedEvent>(OnMuerte);
            subDisparo = EventBus.Instance.Subscribe<ShotFiredEvent>(OnDisparo);
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDano);
            subVehiculo = EventBus.Instance.Subscribe<VehicleDestroyedEvent>(OnVehiculo);
            SP.Player.Reanimacion.Revivido += OnReanimado;
        }

        void OnDisable()
        {
            subMuerte?.Dispose(); subDisparo?.Dispose(); subDano?.Dispose(); subVehiculo?.Dispose();
            subMuerte = subDisparo = subDano = subVehiculo = null;
            SP.Player.Reanimacion.Revivido -= OnReanimado;
        }

        static Soldier Yo()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        static bool EsEscuadra(Soldier s) => s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian;

        void OnMuerte(EntityDiedEvent e)
        {
            var victima = ActorRegistry.FindById(e.ActorId);
            if (victima == null) return;
            var yo = Yo();
            if (victima.Team == TeamId.Enemy)
            {
                var asesino = victima.Health != null ? ActorRegistry.FindById(victima.Health.LastAttackerId) : null;
                if (!EsEscuadra(asesino)) { BajasOtras++; return; }
                if (asesino == yo) BajasPorMi++; else BajasPorAliados++;
                var n = asesino.DisplayName ?? asesino.name;
                bajasPorSoldado.TryGetValue(n, out var c);
                bajasPorSoldado[n] = c + 1;
            }
            else if (EsEscuadra(victima))
            {
                CaidosDeLaEscuadra++;
                if (victima == yo) MisMuertes++;
            }
        }

        void OnDisparo(ShotFiredEvent e)
        {
            var yo = Yo();
            if (yo != null && e.ShooterId == yo.Id) Disparos++;
        }

        void OnDano(DamageTakenEvent e)
        {
            var yo = Yo();
            if (yo == null || e.AttackerId != yo.Id) return;
            var v = ActorRegistry.FindById(e.TargetId);
            if (v != null && v.Team == TeamId.Enemy) Impactos++;
        }

        void OnVehiculo(VehicleDestroyedEvent e)
        {
            if (e.Vehicle != null && (e.Vehicle.Bando == TeamId.Enemy || e.Vehicle.GetComponent<SP.Operacion.OperacionAuto>() != null)) VehiculosDestruidos++;
        }

        void OnReanimado(Soldier s) { if (EsEscuadra(s)) Reanimaciones++; }

        // ---------------------------------------------------------------
        public struct Fila
        {
            public string Etiqueta, Valor;
            public int Numero;          // >= 0: el valor se anima contando desde 0 (Valor puede llevar un sufijo con {0})
            public bool Destacada;
        }

        // vivos/total: lo pone quien sabe como termino (la Operacion cuenta los que subieron al helicoptero).
        public static int SupervivientesForzados = -1, HeridosRescatados;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarForzados() { SupervivientesForzados = -1; HeridosRescatados = 0; }

        public static List<Fila> Filas(bool victoria)
        {
            var r = new List<Fila>();
            var e = Instancia;
            int total = 0, vivos = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (!EsEscuadra(s) || s.Health == null) continue;
                total++;
                if (s.Health.IsAlive) vivos++;
            }
            if (SupervivientesForzados >= 0) vivos = SupervivientesForzados;
            total = Mathf.Max(total, vivos);
            string rescate = HeridosRescatados > 0 ? $"  ({HeridosRescatados} herido{(HeridosRescatados > 1 ? "s" : "")} rescatado{(HeridosRescatados > 1 ? "s" : "")})" : "";
            r.Add(new Fila { Etiqueta = victoria ? "SUPERVIVIENTES" : "SOLDADOS EN PIE", Valor = "{0} / " + total + rescate, Numero = vivos, Destacada = true });
            if (e == null)
            {
                r.Add(new Fila { Etiqueta = "BAJAS ENEMIGAS", Valor = "{0}", Numero = ActorRegistry.CountDead(TeamId.Enemy) });
                return r;
            }
            r.Add(new Fila { Etiqueta = "BAJAS ENEMIGAS", Valor = "{0}", Numero = e.BajasEnemigas, Destacada = true });
            r.Add(new Fila { Etiqueta = "   por vos", Valor = "{0}", Numero = e.BajasPorMi });
            r.Add(new Fila { Etiqueta = "   por tus aliados", Valor = "{0}", Numero = e.BajasPorAliados });
            string mejor = null; int mejorN = 0;
            foreach (var kv in e.bajasPorSoldado) if (kv.Value > mejorN) { mejor = kv.Key; mejorN = kv.Value; }
            if (mejor != null) r.Add(new Fila { Etiqueta = "MEJOR SOLDADO", Valor = NombreCorto(mejor) + " · " + mejorN + " bajas", Numero = -1 });
            r.Add(new Fila { Etiqueta = "TUS MUERTES", Valor = "{0}", Numero = e.MisMuertes });
            r.Add(new Fila { Etiqueta = "CAIDOS DE LA ESCUADRA", Valor = "{0}" + (e.Reanimaciones > 0 ? "  ·  " + e.Reanimaciones + (e.Reanimaciones == 1 ? " reanimado" : " reanimados") : ""), Numero = e.CaidosDeLaEscuadra });
            if (e.VehiculosDestruidos > 0) r.Add(new Fila { Etiqueta = "VEHICULOS DESTRUIDOS", Valor = "{0}", Numero = e.VehiculosDestruidos });
            if (e.Disparos > 0) r.Add(new Fila { Etiqueta = "PUNTERIA", Valor = "{0}%  (" + e.Disparos + " disparos)", Numero = Mathf.Clamp(Mathf.RoundToInt(100f * e.Impactos / e.Disparos), 0, 100) });
            float t = Mathf.Max(0f, Time.time - e.Inicio);
            r.Add(new Fila { Etiqueta = "TIEMPO", Valor = $"{Mathf.FloorToInt(t / 60f):00}:{Mathf.FloorToInt(t % 60f):00}", Numero = -1 });
            return r;
        }

        // "Soldado_2_Kes" -> "Kes"
        static string NombreCorto(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            int i = n.LastIndexOf('_');
            return i >= 0 && i < n.Length - 1 ? n.Substring(i + 1) : n;
        }
    }
}
