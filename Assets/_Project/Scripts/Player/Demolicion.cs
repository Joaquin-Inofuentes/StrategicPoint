using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Player
{
    // DEMOLER (habilidad del soldado de ASALTO): plantar una carga en un muro o cobertura.
    // Hay que estar AGACHADO y QUIETO frente al objetivo durante 4 segundos; al llegar a
    // cero el obstaculo estalla y desaparece (con nube de escombros, onda y sonido).
    public static class Demolicion
    {
        public const float SegundosNormales = 4f;
        // Tiempo de carga vigente. Es 4 s salvo en el paso del tutorial que practica CANCELAR.
        public static float Segundos = SegundosNormales;
        public const float AlcanceMaximo = 4.5f;  // distancia maxima al obstaculo para cargar (Ronda 11: era 9 m; ahora la mitad)
        // Muros mas largos que esto no se pueden volar. 60 m: los muros del chicane (53 y 57 m) son muros normales y tienen que caer;
        // quedan afuera solo los bordes del mapa (Norte/Sur/Este/Oeste, el fondo de la base y la muralla sur de la base: 100-314 m).
        public const float LadoMaximo = 60f;
        public const int DanoDeLaCarga = 35;      // a los enemigos pegados al muro (nunca a los propios)
        public const float RadioDeLaCarga = 4.5f;

        public static ObstacleMarker MarcadorApuntado(Transform golpeado, Vector3 punto)
        {
            if (golpeado != null)
            {
                var m = golpeado.GetComponentInParent<ObstacleMarker>();
                if (m != null) return m;
            }
            return null;
        }

        // El obstaculo que el jugador tiene en la mira. BUG REAL reportado jugando: "agacho al asalto frente al muro con franjas
        // y no pasa nada". AimTargeting devuelve Cubrirse (no Obstacle) cuando el muro apuntado tambien sirve de cobertura -- la
        // compuerta del fortin lo es --, y la demolicion solo miraba Obstacle: nunca arrancaba la carga ni aparecia DEMOLER en el radial.
        public static ObstacleMarker MarcadorDeLaMira(AimResult aim)
        {
            if (aim.Type != AimTargetType.Obstacle && aim.Type != AimTargetType.Cubrirse) return null;
            return MarcadorApuntado(aim.HitTransform, aim.Point);
        }

        // Vida con la que los builders marcan lo "indestructible" (edificios, rocas, muros de blockout).
        public const int VidaIndestructible = 999999;
        public const string MotivoIndestructible = "ESTO NO SE PUEDE DEMOLER";

        public static bool EsDemolible(ObstacleMarker m, out string motivo)
        {
            motivo = null;
            if (m == null || m.IsCollapsed || !m.gameObject.activeInHierarchy) { motivo = "NADA QUE DEMOLER"; return false; }
            // Bug #083: en la Operacion lo indestructible (edificios, rocas) no se vuela con una carga. En SC_Gameplay y el tutorial
            // los muros del blockout tambien tienen vida 999999 y SI se demuelen (es la habilidad del Asalto): ahi no se filtra.
            if (m.MaxHealth >= VidaIndestructible && SP.Operacion.OperacionDirector.Instancia != null) { motivo = MotivoIndestructible; return false; }
            var col = m.GetComponent<Collider>();
            if (col == null) { motivo = "NO SE PUEDE DEMOLER"; return false; }
            var b = col.bounds;
            if (Mathf.Max(b.size.x, b.size.z) > LadoMaximo || b.size.y > 9f) { motivo = "MURO DEMASIADO GRANDE PARA UNA CARGA"; return false; }
            return true;
        }

        public static float DistanciaA(Soldier s, ObstacleMarker m)
        {
            var col = m.GetComponent<Collider>();
            if (col == null) return float.MaxValue;
            var p = col.ClosestPoint(s.transform.position);
            var d = p - s.transform.position; d.y = 0f;
            return d.magnitude;
        }

        static float proximoAvisoE;

        // Bug #083/#084: [E] frente a un muro y "no pasa nada". Se llama cada cuadro desde el driver (solo mira si [E] se apreto
        // ESE cuadro y no hay otra cosa para hacer con [E]). Dice lo que falta: el rol (Asalto) o que eso no se puede demoler.
        // Devuelve el texto mostrado (para las pruebas) o null.
        public static string AvisarSiNoSePuede(Soldier yo, AimResult aim, Ray rayoDeMira, bool apretoE)
        {
            if (yo == null || !apretoE || Time.unscaledTime < proximoAvisoE) return null;
            string texto = null;
            var m = MarcadorDeLaMira(aim);
            if (m != null)
            {
                if (DistanciaA(yo, m) > AlcanceMaximo * 2f) return null;
                if (!EsDemolible(m, out string motivo)) texto = motivo;
                else if (yo.Role != RoleType.Assault)
                {
                    // El cartel de rol lleva su propio enfriamiento.
                    RolRequerido.Avisar(yo, RoleType.Assault, "DEMOLER ESTO");
                    proximoAvisoE = Time.unscaledTime + 0.5f;
                    return RolRequerido.UltimoTexto;
                }
            }
            else if (yo.Role == RoleType.Assault && ApuntaAUnMuroSinMarcador(rayoDeMira, AlcanceMaximo * 2f)) texto = MotivoIndestructible;
            if (texto == null) return null;
            proximoAvisoE = Time.unscaledTime + 1.2f;
            Feedback.Accion(SfxKind.EmptyClick, texto, null, Feedback.Warn, aviso: true, pulso: false, volumen: 0.4f);
            return texto;
        }

        // El rayo de la mira pega en una pared (no suelo, no un actor ni un objeto con uso) a menos de 'alcance'.
        static bool ApuntaAUnMuroSinMarcador(Ray rayo, float alcance)
        {
            if (!Physics.Raycast(rayo, out var hit, alcance + 4f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            var c = hit.collider;
            if (c == null || hit.normal.y > 0.5f) return false;
            if (c.GetComponentInParent<ObstacleMarker>() != null || c.GetComponentInParent<Soldier>() != null) return false;
            if (c.GetComponentInParent<SP.Interaction.IInteractable>() != null || c.GetComponentInParent<TorretaFija>() != null) return false;
            if (c.GetComponentInParent<Vehicle>() != null) return false;
            var d = hit.point - rayo.origin; d.y = 0f;
            return d.magnitude <= alcance + 3f;   // la camara va unos metros detras del soldado
        }

        // Aviso para el tutorial y las pruebas: se demolio este obstaculo.
        public static event System.Action<ObstacleMarker, Soldier> Demolido;

        // El estallido: onda + fuego + escombros, un poco de daño a enemigos cercanos.
        public static void Ejecutar(ObstacleMarker m, Soldier quien)
        {
            if (m == null || m.IsCollapsed) return;
            var col = m.GetComponent<Collider>();
            var centro = col != null ? col.bounds.center : m.transform.position;
            ImpactFx.SpawnExplosion(centro, RadioDeLaCarga);
            ImpactFx.SpawnShockwaveRing(centro, RadioDeLaCarga);
            Projectile.ExplodeAt(centro, RadioDeLaCarga, DanoDeLaCarga, quien != null ? quien.Id : -1, TeamId.Player);
            AudioDirector.PlayAt(SfxKind.CannonBody, centro, 1f, 2f);
            Feedback.Accion(SfxKind.CannonCrack, "¡MURO DEMOLIDO!", centro, Feedback.Ok, aviso: true, pulso: true, volumen: 0.9f);
            GameLog.Line($"{(quien != null ? quien.DisplayName : "?")} demolio {m.name}");
            m.Demoler();
            Demolido?.Invoke(m, quien);
        }
    }

    // Componente del soldado de asalto. Dos modos:
    //  * jugador: si lo manejas, se agacha (Ctrl), quieto y apuntando a un obstaculo a <= 4,5 m
    //    carga durante 4 s (anillo en pantalla) y estalla.
    //  * aliado: la orden "ASALTO DEMUELE" del radial lo manda al muro, se agacha 4 s y lo vuela.
    public class DemoledorAsalto : MonoBehaviour
    {
        static readonly List<DemoledorAsalto> todos = new List<DemoledorAsalto>();

        Soldier yo;
        ObstacleMarker objetivo;
        bool modoAliado, cargando;
        Vector3 puntoDeCarga, ultimaPos;
        float tiempoDeOrden, proximoAviso, proximoTic;
        public float Progreso { get; private set; }

        // Tic de la carga que se arma: mas rapido a medida que se acerca la detonacion.
        void TicDeCarga()
        {
            if (Time.time < proximoTic) return;
            proximoTic = Time.time + Mathf.Lerp(0.6f, 0.14f, Progreso);
            AudioDirector.PlayAt(SfxKind.BombTick, yo.transform.position, Mathf.Lerp(0.35f, 0.9f, Progreso), 0.6f);
        }
        public ObstacleMarker Objetivo => objetivo;
        public bool EstaCargando => cargando;
        TextMesh etiqueta;
        GameObject carga;
        PlayerInputDriver driver;

        public static IReadOnlyList<DemoledorAsalto> Todos => todos;

        // Agrega el componente a todos los asaltos aliados de la escena.
        public static int AsegurarEnAsaltos()
        {
            int n = 0;
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Team == TeamId.Player && s.Role == RoleType.Assault && s.GetComponent<DemoledorAsalto>() == null)
                { s.gameObject.AddComponent<DemoledorAsalto>(); n++; }
            return n;
        }

        public static bool HayEnCurso
        {
            get { foreach (var d in todos) if (d != null && (d.cargando || d.modoAliado || d.Progreso > 0f)) return true; return false; }
        }

        public static bool CancelarTodos()
        {
            bool hubo = false;
            foreach (var d in todos) if (d != null && (d.cargando || d.modoAliado || d.Progreso > 0f)) { d.Cancelar("DEMOLICION CANCELADA"); hubo = true; }
            return hubo;
        }

        void Awake() { yo = GetComponent<Soldier>(); todos.Add(this); }
        void OnDestroy() { todos.Remove(this); LimpiarVisuales(); }
        void OnDisable() { if (cargando) Cancelar(null); }

        public bool IniciarComoJugador(ObstacleMarker m, out string motivo)
        {
            motivo = null;
            if (yo == null || yo.Role != RoleType.Assault) { motivo = "SOLO EL ASALTO DEMUELE"; return false; }
            if (Demolicion.DistanciaA(yo, m) > Demolicion.AlcanceMaximo) { motivo = "ACERCATE MAS AL MURO (MAX " + Demolicion.AlcanceMaximo.ToString("0.#") + " m)"; return false; }
            objetivo = m;
            return true;
        }

        public bool IniciarComoAliado(ObstacleMarker m, out string motivo)
        {
            motivo = null;
            if (yo == null || yo.Health == null || !yo.Health.IsAlive) { motivo = "NO DISPONIBLE"; return false; }
            var col = m.GetComponent<Collider>();
            var cerca = col != null ? col.ClosestPoint(yo.transform.position) : m.transform.position;
            var dir = yo.transform.position - cerca; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -yo.transform.forward;
            puntoDeCarga = cerca + dir.normalized * 2.4f;
            puntoDeCarga.y = yo.transform.position.y;
            objetivo = m;
            modoAliado = true;
            cargando = false;
            Progreso = 0f;
            tiempoDeOrden = 0f;
            OrderService.IssueMoveOrder(yo, puntoDeCarga);
            SP.Core.SesionLog.Orden("DEMOLER", yo, "muro " + m.name);
            return true;
        }

        public void Cancelar(string aviso)
        {
            bool activo = cargando || modoAliado;
            cargando = false; modoAliado = false; objetivo = null; Progreso = 0f;
            if (yo != null && yo.Brain != null)
            {
                if (yo.Brain.Quieto) { yo.Brain.Quieto = false; }
                if (!yo.Brain.IsPossessedByPlayer) yo.Motor.SetCrouching(false);
            }
            if (driver != null) driver.DemolicionEnCurso = false;
            LimpiarVisuales();
            if (activo && aviso != null) Feedback.Accion(SfxKind.EmptyClick, aviso, yo != null ? yo.transform.position : (Vector3?)null, Feedback.Warn, aviso: true, pulso: false, volumen: 0.5f);
        }

        void Update()
        {
            if (yo == null) return;
            if (!yo.Health.IsAlive) { Cancelar(null); return; }
            float dt = Time.deltaTime;
            bool jugador = yo.Brain != null && yo.Brain.IsPossessedByPlayer;
            if (jugador) { ModoAutomatico(dt); ultimaPos = yo.transform.position; }
            else if (modoAliado) ModoAliado(dt);
            else if (Progreso > 0f) Cancelar(null);
            ActualizarVisuales();
        }

        // Jugador: solo si esta agachado, quieto y apuntando a algo demolible a tiro de carga.
        public static int AvisosDeNoSePuede;   // solo para los checks (#124)
        void ModoAutomatico(float dt)
        {
            if (driver == null) driver = PlayerInputDriver.Activo;
            if (driver == null) return;
            // #124: en la vista RTS no se planta nada con [E]/agachado (la [E] sube la camara y el operador de la radio esta siempre agachado).
            if (driver.Rig != null && driver.Rig.Mode == SP.CameraSystem.ControlMode.Rts)
            {
                if (cargando) cargando = false;
                Progreso = 0f; objetivo = null;
                driver.DemolicionEnCurso = false;
                driver.OcultarProgresoDemolicion();
                return;
            }

            var aim = driver.UltimaMira;
            ObstacleMarker m = Demolicion.MarcadorDeLaMira(aim);
            bool apuntaAlMuro = m != null;
            if (m == null && objetivo != null && !objetivo.IsCollapsed) m = objetivo;

            string motivo = null;
            bool demolible = m != null && Demolicion.EsDemolible(m, out motivo);
            float distancia = m != null ? Demolicion.DistanciaA(yo, m) : 0f;
            bool valido = demolible && distancia <= Demolicion.AlcanceMaximo;
            // Pedido explicito: "cambia CTRL por E". Ahora se mantiene [E] (la habilidad de clase) quieto frente al muro; agacharse
            // con [Ctrl] sigue valiendo (compatibilidad), pero ya no hace falta.
            bool eSostenida = driver.EspecialSostenido;
            bool agachado = eSostenida || yo.Motor.IsCrouching;
            float velocidad = dt > 0f ? (yo.transform.position - ultimaPos).magnitude / dt : 0f;
            bool quieto = velocidad < 0.35f;

            if (valido && apuntaAlMuro && m != objetivo) { objetivo = m; Progreso = 0f; }

            if (valido && agachado && quieto && Time.timeScale > 0f)
            {
                if (!cargando) { cargando = true; Feedback.Accion(SfxKind.BombPlant, "PLANTANDO CARGA…", yo.transform.position, Feedback.Warn, aviso: true, pulso: true, volumen: 0.7f); }
                Progreso = Mathf.Min(1f, Progreso + dt / Demolicion.Segundos);
                TicDeCarga();
                ReportarAccion();
                driver.DemolicionEnCurso = true;
                driver.MostrarProgresoDemolicion(Progreso);
                if (Progreso >= 1f) Terminar();
                return;
            }

            if (cargando && Progreso > 0f)
            {
                cargando = false;
                string por = !agachado ? "SOLTASTE [E]" : !quieto ? "TE MOVISTE" : "PERDISTE EL OBJETIVO";
                Feedback.Accion(SfxKind.EmptyClick, "CARGA CANCELADA: " + por, yo.transform.position, Feedback.Warn, aviso: true, pulso: false, volumen: 0.5f);
            }
            Progreso = Mathf.Max(0f, Progreso - dt * 1.5f);
            driver.DemolicionEnCurso = false;
            driver.OcultarProgresoDemolicion();

            // Consejo cada tanto: apuntas a algo demolible pero no cumplis las condiciones.
            if (valido && Time.time >= proximoAviso && apuntaAlMuro)
            {
                proximoAviso = Time.time + 7f;
                Feedback.Accion(SfxKind.TutSub, "MANTEN [E] QUIETO 4 s PARA DEMOLER", null, Feedback.Info, aviso: true, pulso: false, volumen: 0.3f);
            }
            // BUG REAL reportado jugando: "apreto Ctrl y no detona". Con la camara detras del soldado el muro parece mas cerca de lo
            // que esta, y fuera de los 4,5 m (o si es de los muros enormes) no aparecia NINGUN aviso: la carga simplemente no arrancaba.
            else if ((agachado || KeyBindings.WasPressed(KeyBindings.Interactuar)) && apuntaAlMuro && !valido && Time.time >= proximoAviso)
            {
                proximoAviso = Time.time + 1.2f;
                string aviso = !demolible ? motivo : "ACERCATE AL MURO: ESTAS A " + distancia.ToString("0.0") + " m (MAX " + Demolicion.AlcanceMaximo.ToString("0.#") + " m)";
                AvisosDeNoSePuede++;
                Feedback.Accion(SfxKind.EmptyClick, aviso, null, Feedback.Warn, aviso: true, pulso: false, volumen: 0.4f);
            }
            if (!valido) objetivo = null;
        }

        void ModoAliado(float dt)
        {
            if (objetivo == null || objetivo.IsCollapsed) { Cancelar(null); return; }
            tiempoDeOrden += dt;
            if (tiempoDeOrden > 40f) { Cancelar("DEMOLICION: NO LLEGO A TIEMPO"); return; }

            var d = puntoDeCarga - yo.transform.position; d.y = 0f;
            if (!cargando)
            {
                if (d.magnitude > 1.7f) return;
                cargando = true;
                yo.Brain.CancelOrder();
                yo.Brain.Quieto = true;
                Feedback.Accion(SfxKind.BombPlant, yo.DisplayName.ToUpperInvariant() + " PLANTA LA CARGA", yo.transform.position, Feedback.Warn, aviso: true, pulso: true, volumen: 0.7f);
            }

            yo.Motor.SetCrouching(true);
            var col = objetivo.GetComponent<Collider>();
            if (col != null)
            {
                var mira = col.bounds.center - yo.transform.position; mira.y = 0f;
                if (mira.sqrMagnitude > 0.01f) yo.transform.rotation = Quaternion.Slerp(yo.transform.rotation, Quaternion.LookRotation(mira.normalized), dt * 8f);
            }
            bool enCombate = yo.Brain.State == SP.Ai.AiState.Chase || yo.Brain.State == SP.Ai.AiState.Attack;
            if (!enCombate) { Progreso = Mathf.Min(1f, Progreso + dt / Demolicion.Segundos); TicDeCarga(); ReportarAccion(); }
            if (Progreso >= 1f) Terminar();
        }

        // Ronda 13 (puntos 2 y 3): barra de carga entre el soldado y el muro + estado "DETONANDO".
        void ReportarAccion()
        {
            if (objetivo == null) return;
            var col = objetivo.GetComponent<Collider>();
            var punto = col != null ? col.ClosestPoint(yo.transform.position) : objetivo.transform.position;
            AccionesEnCurso.Reportar(yo, "DETONANDO", punto, Progreso, (1f - Progreso) * Demolicion.Segundos, objetivo.transform);
        }

        void Terminar()
        {
            AccionesEnCurso.Terminar(yo);
            var m = objetivo;
            bool eraJugador = yo.Brain != null && yo.Brain.IsPossessedByPlayer;
            cargando = false; modoAliado = false; objetivo = null; Progreso = 0f;
            if (!eraJugador) { yo.Brain.Quieto = false; yo.Motor.SetCrouching(false); }
            if (driver != null) { driver.DemolicionEnCurso = false; driver.OcultarProgresoDemolicion(); }
            LimpiarVisuales();
            Demolicion.Ejecutar(m, yo);
        }

        // ---------------- visuales ----------------
        void ActualizarVisuales()
        {
            bool mostrar = cargando && objetivo != null;
            if (!mostrar) { LimpiarVisuales(); return; }

            if (etiqueta == null)
            {
                var go = new GameObject("DemolicionEtiqueta");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                etiqueta = go.AddComponent<TextMesh>();
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                etiqueta.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                etiqueta.characterSize = 0.09f; etiqueta.fontSize = 60;
                etiqueta.anchor = TextAnchor.MiddleCenter; etiqueta.alignment = TextAlignment.Center;
                etiqueta.color = new Color(1f, 0.62f, 0.2f);
            }
            etiqueta.text = $"DEMOLIENDO {Mathf.RoundToInt(Progreso * 100f)}%";
            if (SP.Core.CamaraPrincipal.Actual != null) etiqueta.transform.rotation = Quaternion.LookRotation(etiqueta.transform.position - SP.Core.CamaraPrincipal.Actual.transform.position);

            // La carga: un cubito rojo que parpadea pegado al muro.
            var col = objetivo.GetComponent<Collider>();
            if (carga == null && col != null)
            {
                carga = GameObject.CreatePrimitive(PrimitiveType.Cube);
                carga.name = "CargaDeDemolicion";
                Destroy(carga.GetComponent<Collider>());
                carga.transform.localScale = new Vector3(0.45f, 0.3f, 0.3f);
                carga.GetComponent<MeshRenderer>().sharedMaterial = SafeMaterial.Create(new Color(0.9f, 0.1f, 0.05f));
            }
            if (carga != null && col != null)
            {
                var p = col.ClosestPoint(transform.position);
                carga.transform.position = p + Vector3.up * 0.25f;
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * (6f + Progreso * 20f));
                carga.GetComponent<MeshRenderer>().enabled = k > 0.35f;
            }
        }

        void LimpiarVisuales()
        {
            if (etiqueta != null) { Destroy(etiqueta.gameObject); etiqueta = null; }
            if (carga != null) { Destroy(carga); carga = null; }
        }
    }
}
