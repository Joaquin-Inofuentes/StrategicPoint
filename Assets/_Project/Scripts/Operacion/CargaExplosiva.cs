using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Operacion
{
    // WP9a (#082, #084): punto de carga de un puesto de la muralla. SOLO el ASALTO la coloca (mantener [E] 3,5 s a menos de 2,5 m, o mandarlo
    // con [Q] apuntando al punto). Al colocarla arde una mecha de 20 s: el contador va en el HUD y en una etiqueta 3D que parpadea (bip que
    // se acelera los ultimos 5 s), el director lanza el contraataque, y a los 20 s el bunker colapsa (Fragmentador), cae su nido de
    // ametralladora y mueren los enemigos del radio. Los enemigos NO la desactivan (frustraba: #082): el objetivo es sobrevivir.
    //
    // Mismo contrato que OperacionTerminal (IInteractable + IOrdenableAAliados): el aim, el radial, el cartel de rol y las animaciones de
    // accion (AccionesEnCurso "DETONANDO" -> AnimacionDeAccion.Demoler, engranajes) funcionan solos.
    public class CargaExplosiva : MonoBehaviour, IInteractable, IOrdenableAAliados
    {
        public enum Estado { Intacta, Ardiendo, Volada }

        public const float SegundosParaPlantar = 3.5f;
        public const float SegundosDeMecha = 20f;
        public const float RadioDeInteraccion = 2.5f;
        public const float RadioDeExplosion = 11f;
        public const int DanoDeExplosion = 400;
        public const float SegundosDeAdvertencia = 5f;      // ultimos segundos: el bip se acelera

        [Header("Identidad")]
        public string lado = "OESTE";                       // "OESTE" / "ESTE"
        public RoleType rol = RoleType.Assault;

        [Header("Piezas")]
        public Transform bunker;                            // raiz del bunker: sus ObstacleMarker se demuelen, el resto se apaga
        public Soldier nido;                                // el artillero del techo: muere con el bunker
        public Renderer led;                                // luz de estado de la carga (rojo fijo -> parpadeo)
        public Renderer caja;                               // la caja de la carga (se tine al armarse)
        public TextMesh etiqueta;                           // "CARGA OESTE · 0:20"

        [Header("Tiempos")]
        public float duracion = SegundosParaPlantar;
        public float segundosDeMecha = SegundosDeMecha;
        public float radio = RadioDeInteraccion;

        public Estado Situacion { get; private set; } = Estado.Intacta;
        public float Progreso01 { get; private set; }       // avance de plantado (0..1)
        public float MechaRestante { get; private set; }
        public bool Habilitado { get; set; } = true;
        public Soldier Plantador { get; private set; }
        public Soldier Asignado { get; private set; }
        public bool EstaPlantada => Situacion != Estado.Intacta;
        public bool EstaVolada => Situacion == Estado.Volada;
        public string UltimoMensaje { get; private set; } = "";
        public int Bips { get; private set; }

        // Bug #106: registro estatico de cargas con la mecha encendida. Los aliados de la IA lo leen (AiBrain.Cargas.cs) para alejarse del
        // radio de la explosion cuando quedan menos de SegundosParaAlejarse.
        public const float SegundosParaAlejarse = 6f, MargenDeSeguridad = 3f;
        static readonly List<CargaExplosiva> armadas = new List<CargaExplosiva>();
        public static IReadOnlyList<CargaExplosiva> Armadas => armadas;
        public static bool EstaArmada(CargaExplosiva c) => c != null && armadas.Contains(c);
        public Vector3 Posicion => transform.position;
        public float RadioDePeligro => RadioDeExplosion;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarRegistro() { armadas.Clear(); }
        void OnEnable() { if (Situacion == Estado.Ardiendo && !armadas.Contains(this)) armadas.Add(this); }
        void OnDisable() { armadas.Remove(this); }
        void OnDestroy() { armadas.Remove(this); }

        public event Action<CargaExplosiva> AlPlantar;
        public event Action<CargaExplosiva> AlExplotar;

        public string Titulo => "CARGA " + lado;
        // "intacto" / "carga 0:14" / "VOLADO": el texto del HUD del objetivo.
        public string TextoDeEstado
        {
            get
            {
                switch (Situacion)
                {
                    case Estado.Ardiendo: return "carga " + Reloj(MechaRestante);
                    case Estado.Volada: return "VOLADO";
                    default: return Progreso01 > 0.01f ? $"colocando {Mathf.RoundToInt(Progreso01 * 100f)} %" : "intacto";
                }
            }
        }

        static readonly Color Rojo = new Color(1f, 0.15f, 0.1f), Ambar = new Color(1f, 0.7f, 0.1f), Blanco = new Color(1f, 1f, 1f);
        MaterialPropertyBlock mpbLed, mpbCaja;
        float proximoBip, reintento;
        bool asignadoTrabajando;

        static Soldier Poseido()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        public static string Reloj(float segundos)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(segundos));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        float Distancia(Soldier s)
        {
            var a = s.transform.position - transform.position; a.y = 0f;
            return a.magnitude;
        }

        void Start()
        {
            if (etiqueta == null) etiqueta = GetComponentInChildren<TextMesh>(true);
            Pintar(Rojo);
            ActualizarEtiqueta(true);
        }

        // ---- IInteractable ----
        public string GetPrompt(PlayerInputDriver player) => $"Enviar al ASALTO a colocar la carga {lado.ToLowerInvariant()}";
        public bool CanInteract(PlayerInputDriver player) => Habilitado && Situacion == Estado.Intacta;
        public void Interact(PlayerInputDriver player) { if (player != null) player.EnviarAInteractuar(this, 0); }

        // ---- IOrdenableAAliados ----
        public string NombreParaOrden => Titulo;
        public string QuienDebe => RolRequerido.Nombre(rol);
        public string VerboDeUso => "MANTENÉ [E] PARA COLOCAR LA CARGA";
        public Transform Raiz => transform;
        bool IOrdenableAAliados.Completo => EstaPlantada;

        public bool PuedeOperar(Soldier s) => s != null && s.Health != null && s.Health.IsAlive && s.Team == TeamId.Player && s.Role == rol;

        public Soldier ElegirOperador(IList<Soldier> candidatos)
        {
            Soldier mejor = null; float md = float.MaxValue;
            if (candidatos == null) return null;
            for (int i = 0; i < candidatos.Count; i++)
            {
                var s = candidatos[i];
                if (!PuedeOperar(s)) continue;
                float d = Distancia(s);
                if (d < md) { md = d; mejor = s; }
            }
            return mejor;
        }

        Vector3 PuntoDeTrabajo(Soldier s)
        {
            var dir = s.transform.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            var p = transform.position + dir.normalized * Mathf.Max(1.5f, radio * 0.7f);
            p.y = s.transform.position.y;
            return p;
        }

        public bool EnviarA(Soldier s, out string motivo)
        {
            motivo = null;
            if (EstaPlantada || !Habilitado) { motivo = "YA ESTA HECHO"; return false; }
            if (s == null || s.Health == null || !s.Health.IsAlive) { motivo = "NO DISPONIBLE"; return false; }
            if (!PuedeOperar(s)) { motivo = $"SOLO EL {QuienDebe} PUEDE"; return false; }
            if (OrderService.LoManejaElJugador(s)) { motivo = "VOS SOS EL INDICADO: ACERCATE Y MANTEN [E]"; return false; }
            SoltarAsignado();
            Asignado = s;
            asignadoTrabajando = false;
            reintento = Time.time + 1.5f;
            OrderService.IssueMoveOrder(s, PuntoDeTrabajo(s));
            AlertQueue.Push($"{s.DisplayName.ToUpperInvariant()} VA A COLOCAR LA {Titulo}", AlertPriority.Media, 2f);
            return true;
        }

        void SoltarAsignado()
        {
            if (Asignado != null && asignadoTrabajando && Asignado.Brain != null) Asignado.Brain.Quieto = false;
            Asignado = null;
            asignadoTrabajando = false;
        }

        // ---- Plantado ----
        void Update()
        {
            float dt = Time.deltaTime;
            switch (Situacion)
            {
                case Estado.Intacta: if (Habilitado) UpdateIntacta(dt); break;
                case Estado.Ardiendo: UpdateMecha(dt); break;
            }
        }

        void UpdateIntacta(float dt)
        {
            var hud = OperacionHud.Instancia;
            var yo = Poseido();
            UpdateAsignado(dt);
            AvisarSiEstaLejos(yo);

            bool cerca = yo != null && yo.Health != null && yo.Health.IsAlive && Distancia(yo) <= radio;
            bool sosteniendo = false;
            if (cerca)
            {
                if (!PuedeOperar(yo))
                {
                    UltimoMensaje = $"SOLO EL {QuienDebe} COLOCA LA CARGA · APUNTALE Y TOCA [Q] PARA MANDARLO";
                    if (hud != null && AvisoCentral.TextoActual == null) hud.Prompt(UltimoMensaje);
                    // #084: apretar [E] con el rol equivocado dice "DEBES SER ASALTO PARA COLOCAR LA CARGA" y a quien mandar con [Q].
                    if (OperacionTerminal.EToque() || OperacionTerminal.EMantenida()) RolRequerido.Avisar(yo, rol, "COLOCAR LA CARGA");
                }
                else
                {
                    sosteniendo = OperacionTerminal.EMantenida();
                    UltimoMensaje = $"MANTEN [E] PARA COLOCAR LA {Titulo} · {Mathf.RoundToInt(Progreso01 * 100f)} %";
                    if (hud != null) hud.Prompt(UltimoMensaje, Progreso01);
                    if (sosteniendo) AccionesEnCurso.Reportar(yo, "DETONANDO", transform.position, Progreso01, (1f - Progreso01) * duracion, transform);
                }
            }
            bool alguienTrabaja = asignadoTrabajando && Asignado != null;
            Avanzar(sosteniendo ? dt : (!alguienTrabaja ? -dt * 0.6f : 0f), cerca && sosteniendo ? yo : null);
            if (Situacion == Estado.Intacta) Pintar(Progreso01 > 0.001f ? Ambar : Rojo);
        }

        void UpdateAsignado(float dt)
        {
            var s = Asignado;
            if (s == null) return;
            var hud = OperacionHud.Instancia;
            if (s.Health == null || !s.Health.IsAlive)
            {
                SoltarAsignado();
                if (hud != null) hud.Aviso($"{Titulo}: EL ALIADO CAYO · VOLVE A MANDAR AL {QuienDebe} CON [Q]", 3f);
                return;
            }
            if (OrderService.LoManejaElJugador(s)) { SoltarAsignado(); return; }   // el jugador lo tomo: coloca el con [E]

            float d = Distancia(s);
            if (d <= radio)
            {
                if (!asignadoTrabajando)
                {
                    asignadoTrabajando = true;
                    if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.Quieto = true; }
                    var dir = transform.position - s.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
                AccionesEnCurso.Reportar(s, "DETONANDO", transform.position, Progreso01, (1f - Progreso01) * duracion, transform);
                Avanzar(dt, s);
                return;
            }
            if (Time.time >= reintento)
            {
                reintento = Time.time + 1.5f;
                bool yendo = s.Brain != null && s.Brain.State == SP.Ai.AiState.MovingToOrder;
                if (!yendo) OrderService.IssueMoveOrder(s, PuntoDeTrabajo(s));
            }
        }

        // El jugador apreto [E] apuntando a la carga pero esta lejos: avisa que se acerque.
        void AvisarSiEstaLejos(Soldier yo)
        {
            if (yo == null || !KeyBindings.WasPressed(KeyBindings.Interactuar)) return;
            var drv = PlayerInputDriver.Activo;
            if (drv == null) return;
            var hit = drv.UltimaMira.HitTransform;
            if (hit == null || !(hit == transform || hit.IsChildOf(transform) || transform.IsChildOf(hit))) return;
            float d = Distancia(yo);
            if (d <= radio) return;
            var hud = OperacionHud.Instancia;
            string msg = $"ACERCATE A LA {Titulo} · ESTAS A {d:0} m (MAX {radio:0.#} m)";
            if (hud != null) hud.Aviso(msg, 1.6f); else AlertQueue.Push(msg, AlertPriority.Media, 1.6f);
        }

        // Publico para las pruebas.
        public void Avanzar(float dt, Soldier quien = null)
        {
            if (Situacion != Estado.Intacta || duracion <= 0f) return;
            Progreso01 = Mathf.Clamp01(Progreso01 + dt / duracion);
            if (Progreso01 >= 1f) Plantar(quien ?? Asignado);
        }

        // Para las pruebas: planta sin pasar por los 3,5 s de [E].
        public bool PlantarPorPrueba(Soldier s)
        {
            if (Situacion != Estado.Intacta) return false;
            Plantar(s);
            return true;
        }

        void Plantar(Soldier quien)
        {
            if (Situacion != Estado.Intacta) return;
            Situacion = Estado.Ardiendo;
            Plantador = quien;
            Progreso01 = 1f;
            MechaRestante = segundosDeMecha;
            if (!armadas.Contains(this)) armadas.Add(this);
            SoltarAsignado();
            proximoBip = 0f;
            AudioDirector.PlayAt(SfxKind.BombPlant, transform.position, 0.9f, 1f);
            AlertQueue.Push($"{Titulo} COLOCADA · EXPLOTA EN {Mathf.CeilToInt(segundosDeMecha)} s", AlertPriority.Alta, 2.5f);
            GameLog.Line($"[Operacion] {Titulo} colocada por {(quien != null ? quien.DisplayName : "?")}");
            AlPlantar?.Invoke(this);
        }

        // ---- Mecha ----
        void UpdateMecha(float dt)
        {
            MechaRestante -= dt;
            var hud = OperacionHud.Instancia;
            // Aviso de peligro: quien maneja la escuadra esta dentro del radio de la explosion.
            var yo = Poseido();
            if (hud != null && yo != null && Vector3.Distance(yo.transform.position, transform.position) < RadioDeExplosion + 2f && MechaRestante < 12f)
                hud.Prompt($"¡ALEJATE DEL PUESTO {lado}! LA CARGA ESTALLA EN {Mathf.CeilToInt(Mathf.Max(0f, MechaRestante))} s");
            // Bip: cada 1 s y, en los ultimos 5, cada vez mas rapido (de 0,5 s a 0,12 s).
            if (Time.time >= proximoBip)
            {
                Bips++;
                float k = Mathf.Clamp01(MechaRestante / SegundosDeAdvertencia);
                float intervalo = MechaRestante > SegundosDeAdvertencia ? 1f : Mathf.Lerp(0.12f, 0.5f, k);
                proximoBip = Time.time + intervalo;
                AudioDirector.PlayAt(SfxKind.BombTick, transform.position + Vector3.up * 0.8f, 0.7f, 1f);
            }
            ActualizarEtiqueta(false);
            if (MechaRestante <= 0f) Detonar();
        }

        // Estado visual segun la mecha: el LED y la etiqueta parpadean mas rapido al final.
        void ActualizarEtiqueta(bool forzar)
        {
            if (etiqueta == null) return;
            string txt;
            Color c;
            if (Situacion == Estado.Ardiendo)
            {
                float frecuencia = MechaRestante > SegundosDeAdvertencia ? 2f : Mathf.Lerp(8f, 3f, Mathf.Clamp01(MechaRestante / SegundosDeAdvertencia));
                bool on = Mathf.Repeat(Time.time * frecuencia, 1f) < 0.55f;
                txt = $"{Titulo} · {Reloj(MechaRestante)}";
                c = on ? Rojo : Blanco;
                Pintar(on ? Rojo : new Color(0.25f, 0.02f, 0.02f));
            }
            else { txt = Titulo; c = new Color(1f, 0.82f, 0.1f); }
            if (forzar || etiqueta.text != txt) etiqueta.text = txt;
            etiqueta.color = c;
        }

        // El rotulo mira a la camara (de lejos se lee igual desde cualquier lado).
        void LateUpdate()
        {
            if (etiqueta == null || Situacion == Estado.Volada) return;
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return;
            var rot = etiqueta.transform.parent != null ? etiqueta.transform.parent : etiqueta.transform;
            var dir = rot.position - cam.transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) rot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        void Pintar(Color c)
        {
            if (led != null)
            {
                if (mpbLed == null) mpbLed = new MaterialPropertyBlock();
                led.GetPropertyBlock(mpbLed);
                mpbLed.SetColor("_BaseColor", c);
                mpbLed.SetColor("_EmissionColor", c * 1.4f);
                led.SetPropertyBlock(mpbLed);
            }
            if (caja != null && Situacion == Estado.Ardiendo)
            {
                if (mpbCaja == null) mpbCaja = new MaterialPropertyBlock();
                caja.GetPropertyBlock(mpbCaja);
                mpbCaja.SetColor("_BaseColor", Color.Lerp(new Color(0.55f, 0.1f, 0.08f), new Color(1f, 0.25f, 0.1f), c.r));
                caja.SetPropertyBlock(mpbCaja);
            }
        }

        // ---- Explosion ----
        void Detonar()
        {
            if (Situacion == Estado.Volada) return;
            Situacion = Estado.Volada;
            MechaRestante = 0f;
            armadas.Remove(this);
            var centro = transform.position + Vector3.up * 1.5f;
            int dueno = Plantador != null ? Plantador.Id : -1;

            // 1) el estallido: dano a los enemigos del radio (la escuadra no se lastima: el objetivo es aguantar, no morir de la propia carga).
            AudioDirector.PlayAt(SfxKind.Explosion, centro, 1f, 1f, PerfilEspacial.Explosion);
            ImpactFx.SpawnExplosion(centro, 7f, true);
            ImpactFx.SpawnShockwaveRing(new Vector3(centro.x, 0.3f, centro.z), new Color(1f, 0.75f, 0.4f), RadioDeExplosion, 0.9f);
            Projectile.ExplodeAt(centro, RadioDeExplosion, DanoDeExplosion, dueno, TeamId.Player);

            // 2) el bunker colapsa: sus muros (ObstacleMarker) se parten en trozos con fisica; lo demas (parapetos, rotulos) se apaga.
            if (bunker != null)
            {
                var marcas = bunker.GetComponentsInChildren<ObstacleMarker>(false);
                foreach (var m in marcas) if (m != null && !m.IsCollapsed) m.Demoler(centro, 11f);
                foreach (Transform hijo in bunker)
                {
                    if (hijo == null || !hijo.gameObject.activeSelf) continue;
                    if (hijo.GetComponentInChildren<ObstacleMarker>(true) != null) continue;
                    ImpactFx.SpawnExplosion(hijo.position + Vector3.up * 0.5f, 2.2f, false);
                    hijo.gameObject.SetActive(false);
                }
            }

            // 3) el nido de ametralladora cae con el bunker: su artillero muere (la baja es del Asalto que planto) y el cuerpo baja al piso.
            if (nido != null && nido.Health != null && nido.Health.IsAlive)
            {
                TorreDestruible.Matar(nido, dueno);
                var suelo = new Vector3(transform.position.x, 0.8f, transform.position.z + 1.5f);
                nido.transform.position = suelo;
                ApoyoEnElPiso.Apoyar(nido.transform);
            }

            // 4) la caja y el rotulo de la carga ya cumplieron.
            foreach (Transform hijo in transform) hijo.gameObject.SetActive(false);
            Coberturas.Registrar();
            GameLog.Line($"[Operacion] {Titulo} detono: bunker {(bunker != null ? bunker.name : "-")} colapsado");
            AlExplotar?.Invoke(this);
        }

        // Para pruebas, saltos de fase y restaurar un estado guardado: el puesto ya estaba volado (sin estallido ni evento).
        public void VolarYa()
        {
            if (Situacion == Estado.Volada) return;
            Situacion = Estado.Volada;
            MechaRestante = 0f;
            Progreso01 = 1f;
            armadas.Remove(this);
            SoltarAsignado();
            if (bunker != null)
                foreach (Transform hijo in bunker) if (hijo != null) hijo.gameObject.SetActive(false);
            if (nido != null) nido.gameObject.SetActive(false);
            foreach (Transform hijo in transform) hijo.gameObject.SetActive(false);
        }

        // Para pruebas: acorta la mecha de una carga ya colocada (no cambia el contraataque ni nada mas).
        public void AcortarMecha(float restante)
        {
            if (Situacion == Estado.Ardiendo) MechaRestante = Mathf.Min(MechaRestante, restante);
        }

        // Restaurar un estado guardado con la mecha ya encendida: sigue ardiendo con el tiempo que quedaba (no relanza el contraataque).
        public void RestaurarMecha(float restante)
        {
            if (Situacion != Estado.Intacta || restante <= 0f) return;
            Situacion = Estado.Ardiendo;
            Progreso01 = 1f;
            MechaRestante = Mathf.Min(restante, segundosDeMecha);
            if (!armadas.Contains(this)) armadas.Add(this);
            proximoBip = 0f;
        }
    }
}
