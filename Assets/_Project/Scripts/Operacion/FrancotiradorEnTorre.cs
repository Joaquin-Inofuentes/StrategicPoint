using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // WP8 (#078): tiradores fijos de las torres del cuartel. Son soldados enemigos comunes (con vida, cuerpo y baja) subidos a una
    // plataforma: el cerebro normal no corre (PuestoElevado.Montar) y esto lo reemplaza. No bajan de la torre; mueren con ella.
    //   - FrancotiradorEnTorre (torre de 12 m): arma Sniper (95 de dano, 1 tiro cada 2,8 s, alcance 90 m) con telegrafia: un laser rojo
    //     sigue al blanco 1,0 s antes de cada tiro (los ultimos 0,25 s queda fijo: se puede esquivar) y despues suena el cerrojo.
    //   - ArtilleroDeTorreta (plataforma de 5 m): ametralladora, rafagas de 8 tiros y pausa de 1,4 a 2,4 s, arco de 120 grados.
    public abstract class TiradorDeTorre : MonoBehaviour
    {
        public Soldier soldado;
        public Vector3 pies;            // donde se para (sobre la plataforma)
        public float yawBase;           // hacia donde mira al empezar
        public float alcance = 90f;
        public float arcoGrados = 360f;

        protected static readonly List<TiradorDeTorre> lista = new List<TiradorDeTorre>();
        public static IReadOnlyList<TiradorDeTorre> Todos => lista;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { lista.Clear(); }

        public Soldier Objetivo { get; protected set; }
        public int Disparos { get; protected set; }
        protected float proximaBusqueda;
        bool montado;

        public bool Activo => soldado != null && soldado.gameObject.activeInHierarchy && soldado.Health != null && soldado.Health.IsAlive;

        protected virtual void Awake() { if (!lista.Contains(this)) lista.Add(this); }
        protected virtual void OnDestroy() { lista.Remove(this); }

        protected virtual void Start()
        {
            if (!Application.isPlaying || soldado == null) return;
            Montar();
        }

        protected void Montar()
        {
            if (montado) return;
            montado = true;
            PuestoElevado.Montar(soldado, pies, yawBase);
            Equipar();
        }

        protected abstract void Equipar();
        protected abstract WeaponKind ArmaEsperada { get; }
        // SoldierLook le pone al soldado el arma de su clase cuando le toca (puede ser despues de Start): se la vuelve a dar si cambio.
        protected void ReafirmarArma()
        {
            if (soldado != null && soldado.Weapon != null && soldado.Weapon.CurrentWeaponKind != ArmaEsperada) Equipar();
        }

        public Vector3 Boca
        {
            get
            {
                var w = soldado.Weapon;
                if (w != null && w.Muzzle != null) return w.Muzzle.position;
                return soldado.transform.position + Vector3.up * 0.3f + soldado.transform.forward * 0.5f;
            }
        }

        public static Vector3 Pecho(Soldier s) => s.transform.position + Vector3.up * 0.25f;

        protected bool EnArco(Vector3 hacia)
        {
            if (arcoGrados >= 359f) return true;
            hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.01f) return true;
            float yaw = Mathf.Atan2(hacia.x, hacia.z) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(yawBase, yaw)) <= arcoGrados * 0.5f;
        }

        // Blanco valido: soldado del jugador vivo y a la vista (alcance, arco y linea de tiro desde la boca hasta el pecho).
        protected bool Valido(Soldier t)
        {
            if (CinematicaDeOperacion.IntroActiva) return false;   // la cinematica inicial: nadie dispara
            if (t == null || t.Team != TeamId.Player || t.Role == RoleType.Civilian || t.Health == null || !t.Health.IsAlive) return false;
            if (!t.gameObject.activeInHierarchy) return false;
            var boca = Boca; var pecho = Pecho(t);
            var d = pecho - boca;
            if (d.sqrMagnitude > alcance * alcance) return false;
            if (!EnArco(d)) return false;
            return NavService.HayLineaDeTiro(boca, pecho, soldado.transform, t.transform);
        }

        protected Soldier Buscar()
        {
            Soldier mejor = null; float md = float.MaxValue;
            var todos = ActorRegistry.All;
            var origen = soldado.transform.position;
            for (int i = 0; i < todos.Count; i++)
            {
                var t = todos[i];
                if (t == null || t.Team != TeamId.Player) continue;
                float d = (t.transform.position - origen).sqrMagnitude;
                if (d >= md || d > alcance * alcance) continue;
                if (!Valido(t)) continue;
                md = d; mejor = t;
            }
            return mejor;
        }

        protected void Girar(Vector3 haciaPunto, float dt, float grados = 400f)
        {
            var v = haciaPunto - soldado.transform.position; v.y = 0f;
            if (v.sqrMagnitude < 0.01f) return;
            float yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            if (arcoGrados < 359f) yaw = yawBase + Mathf.Clamp(Mathf.DeltaAngle(yawBase, yaw), -arcoGrados * 0.5f, arcoGrados * 0.5f);
            float actual = soldado.transform.eulerAngles.y;
            soldado.transform.rotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(actual, yaw, grados * dt), 0f);
        }
    }

    public class FrancotiradorEnTorre : TiradorDeTorre
    {
        public const int Dano = 95;
        public const float Cadencia = 2.8f;        // segundos entre tiros
        public const float Telegrafia = 1.0f;      // laser antes del tiro
        public const float BloqueoDeMira = 0.75f;  // desde aca el laser queda fijo
        public const float AlcanceDelTiro = 90f;
        public const float DispersionBase = 0.5f;

        enum Fase { Libre, Apuntando, Enfriando }
        Fase fase;
        float tFase, proximoCiclo, sinVista;
        Vector3 puntoFijo;
        bool cerrojoSono;
        LineRenderer laser;

        public bool LaserActivo => laser != null && laser.enabled;
        public float LaserEncendidoDesde { get; private set; } = -1f;   // Time.time en que se prendio el laser actual
        public float UltimoDisparoT { get; private set; } = -1f;
        public float LaserAntesDelUltimoTiro { get; private set; } = -1f;
        public bool Apuntando => fase == Fase.Apuntando;

        protected override void Awake() { base.Awake(); alcance = AlcanceDelTiro; arcoGrados = 360f; }

        protected override WeaponKind ArmaEsperada => WeaponKind.Sniper;
        protected override void Equipar()
        {
            var w = soldado.Weapon;
            w.EquipWeapon(WeaponKind.Sniper, Dano, Cadencia, new Color(0.7f, 0.8f, 0.95f));
            w.ConfigurarCargador(60, 2.4f);
            if (laser == null) CrearLaser();
        }

        void CrearLaser()
        {
            var go = new GameObject("LaserDeFrancotirador");
            go.transform.SetParent(transform, false);
            laser = go.AddComponent<LineRenderer>();
            laser.positionCount = 2;
            laser.useWorldSpace = true;
            laser.startWidth = laser.endWidth = 0.06f;
            var sh = Shader.Find("Sprites/Default");
            laser.sharedMaterial = new Material(sh) { color = new Color(1f, 0.08f, 0.05f, 0.95f) };
            laser.startColor = laser.endColor = new Color(1f, 0.1f, 0.05f, 0.95f);
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
            laser.enabled = false;
        }

        void PonerLaser(bool si)
        {
            if (laser == null) return;
            if (si && !laser.enabled) LaserEncendidoDesde = Time.time;
            laser.enabled = si;
        }

        void OnDisable() { if (laser != null) laser.enabled = false; }

        void Update()
        {
            if (!Application.isPlaying || soldado == null) return;
            if (laser == null) return;   // la torre cayo: TorreDestruible se llevo el laser junto con la raiz
            if (!Activo) { PonerLaser(false); return; }
            ReafirmarArma();
            float dt = Time.deltaTime;
            switch (fase)
            {
                case Fase.Libre:
                    PonerLaser(false);
                    if (Time.time >= proximaBusqueda)
                    {
                        proximaBusqueda = Time.time + 0.2f;
                        Objetivo = Buscar();
                    }
                    if (Objetivo != null && Time.time >= proximoCiclo)
                    {
                        fase = Fase.Apuntando; tFase = 0f; sinVista = 0f; cerrojoSono = false;
                        puntoFijo = Pecho(Objetivo);
                        PonerLaser(true);
                    }
                    break;

                case Fase.Apuntando:
                    tFase += dt;
                    if (!Valido(Objetivo))
                    {
                        sinVista += dt;
                        if (sinVista > 0.35f) { PonerLaser(false); fase = Fase.Libre; proximoCiclo = Time.time + 0.6f; Objetivo = null; break; }
                    }
                    else sinVista = 0f;
                    if (Objetivo != null && tFase < BloqueoDeMira && sinVista <= 0f) puntoFijo = Pecho(Objetivo);
                    Girar(puntoFijo, dt);
                    var boca = Boca;
                    laser.SetPosition(0, boca);
                    laser.SetPosition(1, puntoFijo + (puntoFijo - boca).normalized * 2f);
                    if (tFase >= Telegrafia) Disparar();
                    break;

                case Fase.Enfriando:
                    tFase += dt;
                    if (!cerrojoSono && tFase >= 0.9f)
                    {
                        cerrojoSono = true;
                        AudioDirector.PlayClipAt(GenericSfx.GetWeaponReload(WeaponKind.Sniper), Boca, 0.85f, 0.6f);
                    }
                    if (tFase >= Cadencia - Telegrafia) { fase = Fase.Libre; proximoCiclo = Time.time; }
                    break;
            }
        }

        void Disparar()
        {
            float antes = Time.time - LaserEncendidoDesde;
            PonerLaser(false);
            var boca = Boca;
            var dir = (puntoFijo - boca).normalized;
            if (soldado.Weapon.TryFire(boca, dir, DispersionBase))
            {
                Disparos++;
                UltimoDisparoT = Time.time;
                LaserAntesDelUltimoTiro = antes;
            }
            fase = Fase.Enfriando; tFase = 0f;
        }
    }

    public class ArtilleroDeTorreta : TiradorDeTorre
    {
        public const int Dano = 10;
        public const float Cadencia = 0.1f;
        public const int Rafaga = 8;
        public const float AlcanceDeLaTorreta = 55f;
        public const float ArcoDeLaTorreta = 120f;

        float reaccion, pausa;
        int restantes;
        public int Rafagas { get; private set; }

        protected override void Awake() { base.Awake(); alcance = AlcanceDeLaTorreta; arcoGrados = ArcoDeLaTorreta; }

        protected override WeaponKind ArmaEsperada => WeaponKind.Smg;
        protected override void Equipar()
        {
            var w = soldado.Weapon;
            w.EquipWeapon(WeaponKind.Smg, Dano, Cadencia, new Color(1f, 0.85f, 0.3f));
            w.ConfigurarCargador(400, 3.2f);
            w.MultiplicadorTorretaFija = 0.5f;   // montada sobre un pivote: mas estable que a mano
        }

        void Update()
        {
            if (!Application.isPlaying || soldado == null || !Activo) return;
            ReafirmarArma();
            float dt = Time.deltaTime;
            if (Time.time >= proximaBusqueda)
            {
                proximaBusqueda = Time.time + 0.25f;
                var nuevo = Buscar();
                if (nuevo != Objetivo) { reaccion = 0.5f; }
                Objetivo = nuevo;
            }
            if (Objetivo == null) { restantes = 0; return; }
            if (!Valido(Objetivo)) { Objetivo = null; return; }
            Girar(Pecho(Objetivo), dt);
            if (reaccion > 0f) { reaccion -= dt; return; }
            if (pausa > 0f) { pausa -= dt; return; }
            if (restantes <= 0) { restantes = Rafaga; Rafagas++; }
            var boca = Boca;
            var dir = (Pecho(Objetivo) - boca).normalized;
            if (soldado.Weapon.TryFire(boca, dir, 2.2f))
            {
                Disparos++;
                restantes--;
                if (restantes <= 0) pausa = Random.Range(1.4f, 2.4f);
            }
        }
    }
}
