using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // WP8 (#078): reflector de vigilancia del cuartel. Torre de 6 m con un operador (soldado enemigo en la plataforma) y una lampara
    // (BoxCollider de 0,8 m, Luminaria de vida 1) con un SpotLight de 45 m y 18 grados, sin sombras.
    //   - En reposo barre +-60 grados a 25 grados por segundo.
    //   - Si alguien de la escuadra entra en el cono con linea de vision a 45 m o menos (o un enemigo cercano le esta tirando a alguien
    //     a esa distancia), SE ENGANCHA y lo sigue con 0,35 s de retraso: "te alumbra directamente".
    //   - Mientras alumbra avisa a los enemigos de 50 m a la redonda (van a por el iluminado), les mejora la punteria contra el (x0,75
    //     de dispersion, hook en WeaponHolder.TryFire) y al jugador le pone vineta blanca y el cartel "TE ESTAN ILUMINANDO".
    //   - Se rompe disparandole a la lampara (Luminaria: vidrio, chispas, se apaga) o con una explosion a 3,5 m. Si muere el operador
    //     la lampara queda fija, apuntando donde estaba.
    [DisallowMultipleComponent]
    public class ReflectorVigia : MonoBehaviour
    {
        public const float AlcanceDeEnganche = 45f;
        public const float AlcanceDeRetencion = 55f;
        public const float RadioDeAlerta = 50f;
        public const float VelocidadDeBarrido = 25f;     // grados por segundo
        public const float ArcoDeBarrido = 60f;          // +- grados
        public const float RetrasoDeSeguimiento = 0.35f; // segundos
        public const float CabeceoDeReposo = 14f;        // grados hacia el suelo
        public const float FactorDeDispersion = 0.75f;
        public const float SegundosSinVistaParaSoltar = 1.5f;
        public const float PeriodoDeAlerta = 4f;
        public const int MaximoDeAvisadosPorAlerta = 3;

        public Soldier operador;
        public Transform cabeza;        // la lampara: Light + Luminaria + collider
        public Vector3 pies;            // donde se para el operador (sobre la plataforma)
        public float yawBase;           // centro del barrido

        static readonly List<ReflectorVigia> todos = new List<ReflectorVigia>();
        public static IReadOnlyList<ReflectorVigia> Todos => todos;
        static readonly List<ReflectorVigia> rotos = new List<ReflectorVigia>();
        public static IReadOnlyList<ReflectorVigia> Rotos => rotos;
        public static float UltimoAlumbradoDelPoseidoT { get; private set; } = -99f;
        public static bool PoseidoIluminado => Time.time - UltimoAlumbradoDelPoseidoT < 0.35f && Time.timeScale > 0f;
        public static event System.Action<ReflectorVigia> Roto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todos.Clear(); rotos.Clear(); UltimoAlumbradoDelPoseidoT = -99f; Roto = null; }

        Light luz;
        Luminaria luminaria;
        float yaw, pitch, dirBarrido = 1f;
        Vector3 puntoApuntado;
        float proximaBusqueda, sinVista, proximaAlerta;
        bool montado;

        public bool EstaRoto { get; private set; }
        public Soldier Blanco { get; private set; }
        public bool Siguiendo => Blanco != null;
        public int Alertas { get; private set; }
        public Light Luz => luz;
        public Vector3 DireccionDelHaz => cabeza != null ? cabeza.forward : Vector3.forward;
        public bool OperadorVivo => operador != null && operador.gameObject.activeInHierarchy && operador.Health != null && operador.Health.IsAlive;

        void Awake()
        {
            if (!todos.Contains(this)) todos.Add(this);
            Resolver();
        }

        void OnDestroy() { todos.Remove(this); }

        void Resolver()
        {
            if (cabeza == null) return;
            luz = cabeza.GetComponent<Light>();
            luminaria = cabeza.GetComponent<Luminaria>();
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            Resolver();
            yaw = yawBase;
            pitch = CabeceoDeReposo;
            Aplicar();
            CrearHaz();
            if (operador != null && !montado) { montado = true; PuestoElevado.Montar(operador, pies, yawBase); }
        }

        // Haz visible: un cono abierto y casi transparente (se desvanece hacia la punta) que sigue a la lampara. Se apaga con ella.
        GameObject haz;
        public bool HazVisible => haz != null && haz.activeSelf;
        void CrearHaz()
        {
            if (haz != null || cabeza == null) return;
            const float largo = 40f;
            float mitad = (luz != null ? luz.spotAngle : 18f) * 0.5f;
            float radio = largo * Mathf.Tan(mitad * Mathf.Deg2Rad);
            const int lados = 24;
            var vs = new Vector3[lados + 1]; var cs = new Color[lados + 1]; var ts = new int[lados * 3];
            vs[0] = Vector3.zero; cs[0] = new Color(1f, 0.97f, 0.82f, 0.16f);
            for (int i = 0; i < lados; i++)
            {
                float a = i / (float)lados * Mathf.PI * 2f;
                vs[i + 1] = new Vector3(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio, largo);
                cs[i + 1] = new Color(1f, 0.97f, 0.82f, 0f);
                ts[i * 3] = 0; ts[i * 3 + 1] = i + 1; ts[i * 3 + 2] = (i + 1) % lados + 1;
            }
            var mesh = new Mesh { name = "HazDeReflector", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vs; mesh.colors = cs; mesh.triangles = ts; mesh.RecalculateBounds();
            haz = new GameObject("HazVisible");
            haz.transform.SetParent(cabeza, false);
            haz.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, cabeza.localScale.x), 1f / Mathf.Max(0.01f, cabeza.localScale.y), 1f / Mathf.Max(0.01f, cabeza.localScale.z));
            haz.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = haz.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // La lampara sale de las luces tacticas al apagarse; el reflector solo mira si se rompio.
        void Update()
        {
            if (!Application.isPlaying || cabeza == null) return;
            if (!EstaRoto && ((luz != null && !luz.enabled) || (luminaria != null && luminaria.Rota))) Romperse();
            if (haz != null && haz.activeSelf == (EstaRoto || (luz != null && !luz.enabled))) haz.SetActive(!(EstaRoto || (luz != null && !luz.enabled)));
            if (EstaRoto) { Blanco = null; return; }
            if (!OperadorVivo)
            {
                // Sin operador: queda fijo apuntando donde estaba.
                Blanco = null;
                return;
            }
            float dt = Time.deltaTime;
            if (Time.time >= proximaBusqueda)
            {
                proximaBusqueda = Time.time + 0.1f;
                Evaluar();
            }

            if (Blanco != null)
            {
                float k = 1f - Mathf.Exp(-dt / RetrasoDeSeguimiento);
                puntoApuntado = Vector3.Lerp(puntoApuntado, Pecho(Blanco), k);
                var d = puntoApuntado - cabeza.position;
                if (d.sqrMagnitude > 0.01f) cabeza.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                // Desde aca el barrido retoma por donde quedo.
                var e = cabeza.eulerAngles;
                yaw = e.y; pitch = Mathf.DeltaAngle(0f, e.x);
                if (EstaAlumbrando(Blanco))
                {
                    if (EsElPoseido(Blanco)) UltimoAlumbradoDelPoseidoT = Time.time;
                    if (Time.time >= proximaAlerta) { proximaAlerta = Time.time + PeriodoDeAlerta; AlertarA(Blanco); }
                }
            }
            else
            {
                yaw += dirBarrido * VelocidadDeBarrido * dt;
                float rel = Mathf.DeltaAngle(yawBase, yaw);
                if (rel > ArcoDeBarrido) { yaw = yawBase + ArcoDeBarrido; dirBarrido = -1f; }
                else if (rel < -ArcoDeBarrido) { yaw = yawBase - ArcoDeBarrido; dirBarrido = 1f; }
                pitch = Mathf.MoveTowards(pitch, CabeceoDeReposo, 30f * dt);
                Aplicar();
            }
        }

        void Aplicar() { cabeza.rotation = Quaternion.Euler(pitch, yaw, 0f); }

        static Vector3 Pecho(Soldier s) => s.transform.position + Vector3.up * 0.25f;
        static bool EsElPoseido(Soldier s)
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            return d != null && d.Brain != null && d.Brain.Current == s;
        }

        float AnguloAlHaz(Vector3 punto) => Vector3.Angle(cabeza.forward, punto - cabeza.position);

        bool EnElCono(Soldier s)
        {
            float mitad = luz != null ? luz.spotAngle * 0.5f : 9f;
            return AnguloAlHaz(Pecho(s)) <= mitad + 2f;
        }

        bool ConVista(Soldier s)
        {
            return NavService.HayLineaDeTiro(cabeza.position, Pecho(s), transform, s.transform);
        }

        // Alumbra de verdad: lampara intacta, operador vivo, el soldado dentro del cono, a distancia de haz y con linea de vision.
        public bool EstaAlumbrando(Soldier s)
        {
            if (EstaRoto || cabeza == null || s == null || !OperadorVivo || (luz != null && !luz.enabled)) return false;
            if (Vector3.Distance(cabeza.position, Pecho(s)) > (luz != null ? Mathf.Min(luz.range, AlcanceDeRetencion) : AlcanceDeRetencion)) return false;
            return EnElCono(s) && ConVista(s);
        }

        static bool Candidato(Soldier t) => t != null && t.Team == TeamId.Player && t.Role != RoleType.Civilian && t.Health != null && t.Health.IsAlive && t.gameObject.activeInHierarchy;

        void Evaluar()
        {
            if (CinematicaDeOperacion.IntroActiva) { Blanco = null; return; }   // la cinematica inicial: el haz solo barre
            if (Blanco != null)
            {
                bool vale = Candidato(Blanco) && Vector3.Distance(cabeza.position, Pecho(Blanco)) <= AlcanceDeRetencion && ConVista(Blanco);
                if (vale) { sinVista = 0f; return; }
                sinVista += 0.1f;
                if (sinVista >= SegundosSinVistaParaSoltar) { Blanco = null; sinVista = 0f; }
                return;
            }
            // Candidatos: la escuadra dentro del cono a menos de 45 m con vision (el poseido tiene prioridad).
            Soldier mejor = null; float md = float.MaxValue;
            var todosLosActores = ActorRegistry.All;
            for (int i = 0; i < todosLosActores.Count; i++)
            {
                var t = todosLosActores[i];
                if (!Candidato(t)) continue;
                float d = Vector3.Distance(cabeza.position, Pecho(t));
                if (d > AlcanceDeEnganche || !EnElCono(t)) continue;
                if (!ConVista(t)) continue;
                if (EsElPoseido(t)) d -= 1000f;
                if (d < md) { md = d; mejor = t; }
            }
            // "El operador tiene blanco": un enemigo a menos de 25 m del reflector que le esta tirando a alguien a distancia de haz.
            if (mejor == null)
            {
                for (int i = 0; i < todosLosActores.Count && mejor == null; i++)
                {
                    var e = todosLosActores[i];
                    if (e == null || e.Team != TeamId.Enemy || e == operador || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) continue;
                    if (e.Brain == null || e.Brain.MontadoEnVehiculo) continue;
                    if (e.Brain.State != AiState.Attack) continue;
                    if ((e.transform.position - cabeza.position).sqrMagnitude > 25f * 25f) continue;
                    var t = e.Brain.CurrentTarget;
                    if (!Candidato(t) || Vector3.Distance(cabeza.position, Pecho(t)) > AlcanceDeEnganche || !ConVista(t)) continue;
                    mejor = t;
                }
            }
            if (mejor != null)
            {
                Blanco = mejor; sinVista = 0f; proximaAlerta = 0f;
                // El haz arranca desde donde esta apuntando y converge: no salta de golpe.
                puntoApuntado = cabeza.position + cabeza.forward * Mathf.Min(Vector3.Distance(cabeza.position, Pecho(mejor)), 30f);
            }
        }

        // Los enemigos cercanos que estan libres van a por el iluminado; los mas cercanos primero y no mas de unos pocos por aviso.
        void AlertarA(Soldier iluminado)
        {
            var avisados = new List<(float d, Soldier s)>();
            var todosLosActores = ActorRegistry.All;
            for (int i = 0; i < todosLosActores.Count; i++)
            {
                var e = todosLosActores[i];
                if (e == null || e.Team != TeamId.Enemy || e == operador || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) continue;
                var br = e.Brain;
                if (br == null || br.MontadoEnVehiculo || br.Pasivo || br.CurrentTarget != null || br.TieneOrden) continue;
                if (br.State != AiState.Patrol && br.State != AiState.Idle) continue;
                float d = Vector3.Distance(e.transform.position, cabeza.position);
                if (d > RadioDeAlerta) continue;
                avisados.Add((d, e));
            }
            avisados.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < avisados.Count && i < MaximoDeAvisadosPorAlerta; i++)
            {
                avisados[i].s.Brain.IssueAttackOrder(iluminado);
                Alertas++;
            }
            if (avisados.Count > 0) GameLog.Line($"[Operacion] {name}: alerta a {Mathf.Min(avisados.Count, MaximoDeAvisadosPorAlerta)} enemigos (ilumina a {iluminado.DisplayName})");
        }

        void Romperse()
        {
            EstaRoto = true;
            Blanco = null;
            if (!rotos.Contains(this)) rotos.Add(this);
            Roto?.Invoke(this);
            GameLog.Line($"[Operacion] se rompe el reflector {name}");
        }

        // Para restaurar un estado guardado: el reflector ya estaba roto.
        public void RomperYa()
        {
            if (EstaRoto) return;
            Resolver();
            if (luminaria != null && !luminaria.Rota) luminaria.TakeDamage(luminaria.Health + 1000, cabeza != null ? cabeza.position : transform.position);
            if (luz != null) luz.enabled = false;
            Romperse();
        }

        // ---- Punteria de los enemigos contra alguien alumbrado ----
        // Alguien alumbrado por algun reflector en marcha cerca de la direccion de tiro: la dispersion del enemigo vale x0,75.
        public static float FactorDeDispersionContraIluminado(Vector3 origen, Vector3 direccion)
        {
            if (todos.Count == 0 || direccion.sqrMagnitude < 0.0001f) return 1f;
            for (int i = 0; i < todos.Count; i++)
            {
                var r = todos[i];
                if (r == null || r.EstaRoto || r.Blanco == null) continue;
                var b = r.Blanco;
                if (!r.EstaAlumbrando(b)) continue;
                var v = Pecho(b) - origen;
                if (v.sqrMagnitude > 90f * 90f) continue;
                if (Vector3.Angle(direccion, v) <= 8f) return FactorDeDispersion;
            }
            return 1f;
        }

        public static bool Iluminado(Soldier s)
        {
            for (int i = 0; i < todos.Count; i++)
            {
                var r = todos[i];
                if (r != null && r.Blanco == s && r.EstaAlumbrando(s)) return true;
            }
            return false;
        }
    }
}
