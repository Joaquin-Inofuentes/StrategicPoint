using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Pulido del nivel 2 (Sheets, Centro de datos: "estan durmiendo siesta"): los enemigos abatidos se quedaban tirados en el piso para siempre y,
    // vistos desde la camara tactica, parecian soldados dormidos. Solo enemigos (los aliados caidos se revivian y se ven en el HUD): el soldado
    // sigue registrado, con su collider/vida, asi que ningun contador ni objetivo cambia; solo se apagan sus renderers.
    //
    // R19 punto 6 ("Animacion de eliminacion de enemigos"): el hundimiento de la R17 pasaba a los 14 s y casi nadie lo veia. Ahora el cierre es
    // una DESINTEGRACION EN CUBOS (el lenguaje visual del juego: ImpactCubes, DebrisPool) a los 4 s, con una maquina de estados por cadaver:
    //   Caido (4,0 s; pesado 5,5) -> Destello (0,2 s, tinte calido que tapa el corte) -> Desintegrando (0,8 s, cuerpo apagado, cubos desde
    //   los huesos) -> Retirado (Retirados++).
    // Sin corutinas: Tick(dt) acumulado (R3), asi la suite headless lo puede simular. Sin busquedas globales (R1): instancia estatica.
    public sealed class RetiroDeEnemigosCaidos : MonoBehaviour
    {
        public enum Fase { Caido, Destello, Desintegrando, Retirado }

        public const float SegundosAntesDeRetirar = 4f;          // R19: antes 14
        public const float SegundosAntesDeRetirarPesado = 5.5f;
        public const float SegundosDeDestello = 0.20f;
        public const float SegundosDeCubos = 0.80f;
        // Compatibilidad (R17): lo que tarda la parte visible del retiro, despues de la espera. Lo usa el Destroy del arma caida.
        public const float SegundosDeAchique = SegundosDeDestello + SegundosDeCubos;
        public const int CubosPorCuerpo = 12, CubosPorCuerpoPesado = 20, CubosSinAnimator = 8, CubosConPoolLleno = 6;
        public const int UmbralDelPool = 110;                    // de DebrisPool.Budget (160), compartido con impactos
        public const int IniciosPorVentana = 4;                   // como mucho 4 desintegraciones nuevas cada 0,25 s
        public const float SegundosDeVentana = 0.25f;
        public const float MetrosDelSonido = 30f;
        public const float SegundosDelPolvo = 0.6f;
        public static readonly Color ColorDelDestello = new Color(1.0f, 0.45f, 0.30f) * 1.6f;
        public static readonly Color ColorCubosComun = new Color(1.00f, 0.30f, 0.24f);
        public static readonly Color ColorCubosPesado = new Color(0.45f, 0.13f, 0.72f);

        public static int Retirados { get; private set; }
        public static int Desintegrados { get; private set; }
        public static int CubosLanzados { get; private set; }
        public static int UltimosCubos { get; private set; }      // cubos de la ultima desintegracion (pesado >= 18)
        public static int Cancelados { get; private set; }        // revividos a mitad de camino
        public static int EnCurso { get { int n = 0; foreach (var c in caidos) if (c.fase == Fase.Destello || c.fase == Fase.Desintegrando) n++; return n; } }
        public static int Pendientes => caidos.Count;
        // Para los checks: fase del cadaver de 's' (null si no esta en la maquina: vivo, retirado o nunca registrado).
        public static Fase? FaseDe(Soldier s) { foreach (var c in caidos) if (c.s == s) return c.fase; return null; }

        // Funcion pura (H25.3): fase de un cadaver a los t segundos de morir, sin tope ni esperas.
        public static Fase FaseEn(float t, float espera)
        {
            if (t < espera) return Fase.Caido;
            if (t < espera + SegundosDeDestello) return Fase.Destello;
            if (t < espera + SegundosDeDestello + SegundosDeCubos) return Fase.Desintegrando;
            return Fase.Retirado;
        }

        sealed class Cadaver
        {
            public Soldier s;
            public GameObject arma;
            public RagdollDeExplosion ragdoll;
            public Renderer[] cuerpo;          // renderers a teñir (SkinnedMeshRenderer)
            public Color[] tintes;             // tinte original de cada uno
            public Fase fase;
            public float t, tFase, espera;
            public bool grande, polvo;
            public Color colorCubos;
        }

        static readonly List<Cadaver> caidos = new List<Cadaver>();
        static RetiroDeEnemigosCaidos instancia;
        static float reloj, inicioVentana;
        static int iniciosEnVentana;

        System.IDisposable suscripcion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEstaticos()
        {
            caidos.Clear(); instancia = null; reloj = 0f; inicioVentana = -1f; iniciosEnVentana = 0;
            Retirados = 0; Desintegrados = 0; CubosLanzados = 0; UltimosCubos = 0; Cancelados = 0; PolvosAlCaer = 0; ArmasSoltadas = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            Retirados = 0; PolvosAlCaer = 0; ArmasSoltadas = 0; Desintegrados = 0; CubosLanzados = 0; UltimosCubos = 0; Cancelados = 0;
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
            Asegurar(SceneManager.GetActiveScene());
        }

        static void AlCargar(Scene s, LoadSceneMode modo) => Asegurar(s);

        // R19: tambien en el tutorial (el Nivel 1 debe mostrar lo mismo).
        public static bool AplicaEn(string escena) =>
            escena != null && (escena.StartsWith("SC_Operacion", System.StringComparison.Ordinal) || escena.StartsWith("SC_Tutorial", System.StringComparison.Ordinal));

        static void Asegurar(Scene s)
        {
            if (!AplicaEn(s.name)) return;
            if (instancia != null) return;
            new GameObject("RetiroDeEnemigosCaidos", typeof(RetiroDeEnemigosCaidos));
        }

        void Awake() { if (instancia == null) instancia = this; }
        void OnEnable()
        {
            if (instancia == null) instancia = this;
            if (instancia == this) suscripcion = EventBus.Instance.Subscribe<EntityDiedEvent>(AlMorir);
        }
        void OnDisable() { suscripcion?.Dispose(); suscripcion = null; }
        void OnDestroy() { if (instancia == this) { instancia = null; caidos.Clear(); } }
        void Update() => Tick(Time.deltaTime);

        void AlMorir(EntityDiedEvent e)
        {
            var s = ActorRegistry.FindById(e.ActorId);
            if (s == null || s.Team != TeamId.Enemy) return;
            var arma = SoltarElArma(s);
            Registrar(s, arma);
        }

        // Publico para la suite/checks: mete un caido en la maquina sin pasar por el bus.
        public static void Registrar(Soldier s, GameObject arma = null)
        {
            if (s == null) return;
            foreach (var c in caidos) if (c.s == s) return;
            var pesado = s.GetComponent<SP.Operacion.EnemigoPesado>() != null;
            var escudero = s.GetComponent<SP.Operacion.EnemigoEscudero>() != null;
            var smrs = s.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var cad = new Cadaver
            {
                s = s, arma = arma, ragdoll = s.GetComponent<RagdollDeExplosion>(),
                cuerpo = smrs, tintes = new Color[smrs.Length],
                fase = Fase.Caido, espera = pesado ? SegundosAntesDeRetirarPesado : SegundosAntesDeRetirar,
                grande = pesado,
                colorCubos = pesado ? ColorCubosPesado : escudero ? SoldierLook.ColorDeCubosEscudero : ColorCubosComun,
            };
            caidos.Add(cad);
        }

        public static void Tick(float dt)
        {
            if (dt <= 0f) return;
            reloj += dt;
            if (reloj - inicioVentana >= SegundosDeVentana) { inicioVentana = reloj; iniciosEnVentana = 0; }
            for (int i = caidos.Count - 1; i >= 0; i--)
            {
                var c = caidos[i];
                if (!Avanzar(c, dt)) caidos.RemoveAt(i);
            }
        }

        // false = sale de la lista (retirado, destruido o revivido).
        static bool Avanzar(Cadaver c, float dt)
        {
            if (c.s == null || c.s.Health == null) { if (c.arma != null) Object.Destroy(c.arma); return false; }
            if (c.s.Health.IsAlive) { Cancelar(c); return false; }
            c.t += dt;
            if (!c.polvo && c.t >= SegundosDelPolvo) { c.polvo = true; Polvo(c.s); }
            switch (c.fase)
            {
                case Fase.Caido:
                    if (c.t < c.espera) return true;
                    if (c.ragdoll != null && c.ragdoll.Activo) return true;      // la explosion todavia lo esta revoleando
                    if (iniciosEnVentana >= IniciosPorVentana) return true;       // espera su turno
                    iniciosEnVentana++;
                    for (int i = 0; i < c.cuerpo.Length; i++) c.tintes[i] = CubeFxReactor.ReadTint(c.cuerpo[i]);
                    c.fase = Fase.Destello; c.tFase = 0f;
                    return true;
                case Fase.Destello:
                    c.tFase += dt;
                    {
                        float k = Mathf.Clamp01(c.tFase / SegundosDeDestello);
                        for (int i = 0; i < c.cuerpo.Length; i++) if (c.cuerpo[i] != null) CubeFxReactor.WriteTint(c.cuerpo[i], Color.Lerp(c.tintes[i], ColorDelDestello, k));
                    }
                    if (c.tFase < SegundosDeDestello) return true;
                    Desintegrar(c);
                    c.fase = Fase.Desintegrando; c.tFase = 0f;
                    return true;
                case Fase.Desintegrando:
                    c.tFase += dt;
                    if (c.tFase < SegundosDeCubos) return true;
                    c.fase = Fase.Retirado;
                    Retirados++;
                    return false;
            }
            return false;
        }

        // Revivido en cualquier fase: tinte y renderers como estaban.
        static void Cancelar(Cadaver c)
        {
            if (c.fase == Fase.Destello || c.fase == Fase.Desintegrando)
            {
                for (int i = 0; i < c.cuerpo.Length; i++) if (c.cuerpo[i] != null) CubeFxReactor.WriteTint(c.cuerpo[i], c.tintes[i]);
                foreach (var r in c.s.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                c.s.SetBodyVisible(true);
            }
            Cancelados++;
        }

        static void Desintegrar(Cadaver c)
        {
            var s = c.s;
            for (int i = 0; i < c.cuerpo.Length; i++) if (c.cuerpo[i] != null) CubeFxReactor.WriteTint(c.cuerpo[i], c.tintes[i]);   // el pool de tintes queda limpio por si revive
            var anim = s.GetComponentInChildren<Animator>(true);
            int pedidos = anim != null && anim.isHuman ? (c.grande ? CubosPorCuerpoPesado : CubosPorCuerpo) : CubosSinAnimator;
            if (DebrisPool.ActiveCount > UmbralDelPool) pedidos = CubosConPoolLleno;

            // Las posiciones se toman ANTES de apagar los renderers (los limites del cuerpo se usan de respaldo).
            var limites = new Bounds(s.transform.position + Vector3.up * 0.9f, new Vector3(0.5f, 1.6f, 0.5f));
            bool hay = false;
            foreach (var r in c.cuerpo) if (r != null) { if (!hay) { limites = r.bounds; hay = true; } else limites.Encapsulate(r.bounds); }
            var centro = limites.center;

            float escala = c.grande ? 2f : 1f;
            int lanzados = 0;
            for (int i = 0; i < pedidos; i++)
            {
                Vector3 p;
                Transform hueso = anim != null && anim.isHuman ? anim.GetBoneTransform(Huesos[i % Huesos.Length]) : null;
                if (hueso != null) p = hueso.position + (i >= Huesos.Length ? Random.insideUnitSphere * 0.12f * escala : Vector3.zero);
                else p = new Vector3(Random.Range(limites.min.x, limites.max.x), Random.Range(limites.min.y, limites.max.y), Random.Range(limites.min.z, limites.max.z));
                var radial = p - centro; radial.y = 0f;
                radial = radial.sqrMagnitude < 0.0001f ? Random.insideUnitSphere : radial.normalized;
                radial.y = 0f;
                var vel = Vector3.up * Random.Range(1.2f, 2.4f) + radial.normalized * Random.Range(0.4f, 1.0f);
                var col = c.colorCubos * Random.Range(0.85f, 1.1f); col.a = 1f;
                if (DebrisPool.Spawn(p, vel, col, Random.Range(0.16f, 0.26f) * escala, Random.Range(0.7f, 1.0f)) != null) lanzados++;
            }
            CubosLanzados += lanzados; UltimosCubos = lanzados; Desintegrados++;

            foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (c.arma != null) { Object.Destroy(c.arma); c.arma = null; }

            var cam = CamaraPrincipal.Actual;
            if (cam != null && (cam.transform.position - centro).sqrMagnitude < MetrosDelSonido * MetrosDelSonido)
                AudioDirector.PlayAt(SfxKind.ImpactDirt, centro, 0.35f, 1f, PerfilEspacial.Para(SfxKind.ImpactDirt), 1.4f);
        }

        // 12 huesos (los primeros) y 8 mas para la variante grande del pesado.
        static readonly HumanBodyBones[] Huesos =
        {
            HumanBodyBones.Head, HumanBodyBones.Chest, HumanBodyBones.Spine, HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.Neck, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder, HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Spine,
        };

        // R17 (pendiente 2.9): al caer el cuerpo levanta un poco de polvo cuando toca el piso (~0,6 s despues del impacto).
        public static int PolvosAlCaer { get; private set; }
        static void Polvo(Soldier s)
        {
            SpriteFx.Lanzar("smoke_02", s.transform.position + Vector3.up * 0.05f, new Color(0.62f, 0.58f, 0.52f, 0.30f), 0.35f, 0.9f, 0.7f, Random.Range(-30f, 30f), Vector3.up * 0.2f, 1.0f, 1);
            PolvosAlCaer++;
        }

        // R17 (pendiente 2.9): el arma se le cae de la mano. Se clona el modelo del arma (sin scripts ni colliders, asi no empuja a nadie),
        // se apaga el de la mano y el clon cae con gravedad y un poco de giro hasta quedar tirado al lado del cuerpo; desaparece junto con el
        // (R19: se destruye al desintegrarse; el Destroy con tiempo queda de respaldo, con margen para un ragdoll de explosion).
        public const float GravedadDelArma = 9.8f;
        public static int ArmasSoltadas { get; private set; }
        GameObject SoltarElArma(Soldier s)
        {
            var wh = s.GetComponent<WeaponHolder>();
            var rend = wh != null ? wh.WeaponVisualRenderer : null;
            if (rend == null) return null;   // (otro suscriptor de la muerte ya puede haber apagado el renderer: el clon se enciende igual)
            var go = Object.Instantiate(rend.gameObject, rend.transform.position, rend.transform.rotation);
            go.name = "ArmaCaida";
            go.transform.SetParent(null, true);
            go.transform.localScale = rend.transform.lossyScale;
            foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(m);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in rend.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            go.SetActive(true);
            ArmasSoltadas++;
            Object.Destroy(go, SegundosAntesDeRetirarPesado + SegundosDeAchique + RagdollDeExplosion.SegundosActivo);
            StartCoroutine(CaerElArma(go.transform, s.transform.position.y + 0.06f));
            return go;
        }

        static IEnumerator CaerElArma(Transform arma, float sueloY)
        {
            float vy = 0f, giro = Random.Range(120f, 260f) * (Random.value < 0.5f ? -1f : 1f);
            float yaw = arma.eulerAngles.y, roll = Random.Range(70f, 90f);
            while (arma != null && arma.position.y > sueloY)
            {
                float dt = Time.deltaTime;
                vy -= GravedadDelArma * dt;
                var p = arma.position; p.y = Mathf.Max(sueloY, p.y + vy * dt); arma.position = p;
                arma.Rotate(giro * dt, 0f, 0f, Space.Self);
                yield return null;
            }
            if (arma != null) arma.rotation = Quaternion.Euler(0f, yaw, roll);   // queda de costado sobre el piso
        }
    }
}
