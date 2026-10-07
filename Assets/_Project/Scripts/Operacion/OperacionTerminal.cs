using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Interaction;
using SP.Player;
using SP.UI;

namespace SP.Operacion
{
    // Punto de interaccion de la Operacion Cuartel: el panel de cada puesto de control (se mantiene [E] y SOLO lo puede hacer el
    // FLANQUEADOR) y la computadora del centro de datos (un soldado se queda interactuando 30 s mientras los demas lo defienden).
    //
    //  - ModoSostener: hay que mantener [E] cerca del panel; si se suelta el progreso retrocede (Decae).
    //  - Modo hacker: un toque de [E] "engancha" al soldado poseido; mientras ese soldado siga vivo y pegado a la computadora el
    //    progreso corre solo (aunque el jugador pase a otro soldado). Si cae o se aleja, se pausa hasta que alguien retome con [E].
    //  - Ordenable (Q): apuntarle y tocar [Q] (o elegir INTERACTUAR en el radial) manda al aliado que corresponde (el FLANQUEADOR
    //    para los paneles, el mas cercano para la computadora): camina hasta alli y lo opera solo. Ver IOrdenableAAliados.
    public class OperacionTerminal : MonoBehaviour, IInteractable, IOrdenableAAliados
    {
        public string titulo = "PANEL";
        public float duracion = 4f;
        public bool soloUnRol;
        public RoleType rol = RoleType.Flanker;
        public string nombreDelRol = "FLANQUEADOR";
        public float radio = 3.6f;
        public bool modoSostener = true;
        public bool decae = true;
        public Transform indicador;            // cubo fino que crece con el progreso
        public Renderer luz;                   // cubo/pantalla que pasa de rojo a verde

        public float Progreso01 { get; private set; }
        public bool Completo { get; private set; }
        public bool Habilitado { get; set; } = true;
        public Soldier Operador { get; private set; }
        // El aliado al que el jugador mando con [Q]: camina hasta aca y opera solo.
        public Soldier Asignado { get; private set; }
        public event Action<OperacionTerminal> Completado;

        // Costura de pruebas: la suite y las capturas no tienen teclado real (con el Editor sin foco los eventos de tecla se pierden).
        public static bool PruebaMantenerE;
        public static bool PruebaToqueE;
        public static bool EMantenida() => PruebaMantenerE || KeyBindings.IsPressed(KeyBindings.Interactuar);
        public static bool EToque()
        {
            if (PruebaToqueE) { PruebaToqueE = false; return true; }
            return KeyBindings.WasPressed(KeyBindings.Interactuar);
        }

        static readonly Color Rojo = new Color(1f, 0.15f, 0.1f), Verde = new Color(0.2f, 1f, 0.35f), Ambar = new Color(1f, 0.7f, 0.1f);
        Vector3 indicadorEscalaBase;
        bool indicadorInit;
        public string UltimoMensaje { get; private set; } = "";

        void Start() { Pintar(Rojo); }

        static Soldier Poseido()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        float Distancia(Soldier s)
        {
            var a = s.transform.position - transform.position; a.y = 0f;
            return a.magnitude;
        }

        public bool PuedeOperar(Soldier s) => s != null && s.Health != null && s.Health.IsAlive && s.Team == TeamId.Player && (!soloUnRol || s.Role == rol);

        // ---- IInteractable / IOrdenableAAliados ----
        public string NombreParaOrden => titulo;
        public string QuienDebe => soloUnRol ? nombreDelRol : "CUALQUIER ALIADO";
        public Transform Raiz => transform;
        bool IOrdenableAAliados.Completo => Completo;
        bool IOrdenableAAliados.Habilitado => Habilitado;

        // Texto del verbo para el cartel de la mira: lo que hace [E] aca cuando el que mira puede operarlo.
        public string VerboDeUso => modoSostener ? "MANTENÉ [E] PARA DESACTIVAR" : "APRETÁ [E] PARA OPERAR";

        public string GetPrompt(PlayerInputDriver player) => soloUnRol ? $"Enviar al {nombreDelRol} a {titulo.ToLowerInvariant()}" : $"Enviar a un aliado a {titulo.ToLowerInvariant()}";
        public bool CanInteract(PlayerInputDriver player) => Habilitado && !Completo;
        public void Interact(PlayerInputDriver player) { if (player != null) player.EnviarAInteractuar(this, 0); }

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

        // Punto desde donde el aliado trabaja: sobre la linea hacia donde viene, a ~70 % del radio del panel.
        Vector3 PuntoDeTrabajo(Soldier s)
        {
            var dir = s.transform.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            var p = transform.position + dir.normalized * Mathf.Max(1.6f, radio * 0.7f);
            p.y = s.transform.position.y;
            return p;
        }

        float proximoReintento;
        bool asignadoTrabajando;

        public bool EnviarA(Soldier s, out string motivo)
        {
            motivo = null;
            if (Completo || !Habilitado) { motivo = "YA ESTA HECHO"; return false; }
            if (s == null || s.Health == null || !s.Health.IsAlive) { motivo = "NO DISPONIBLE"; return false; }
            if (!PuedeOperar(s)) { motivo = $"SOLO EL {nombreDelRol} PUEDE"; return false; }
            if (OrderService.LoManejaElJugador(s)) { motivo = "VOS SOS EL INDICADO: ACERCATE Y MANTEN [E]"; return false; }
            SoltarAsignado();
            Asignado = s;
            asignadoTrabajando = false;
            proximoReintento = Time.time + 1.5f;
            OrderService.IssueMoveOrder(s, PuntoDeTrabajo(s));
            AlertQueue.Push($"{s.DisplayName.ToUpperInvariant()} VA A {titulo}", AlertPriority.Media, 2f);
            return true;
        }

        void SoltarAsignado()
        {
            if (Asignado != null && asignadoTrabajando && Asignado.Brain != null) Asignado.Brain.Quieto = false;
            Asignado = null;
            asignadoTrabajando = false;
        }

        void UpdateAsignado(float dt)
        {
            var s = Asignado;
            if (s == null) return;
            var hud = OperacionHud.Instancia;
            if (s.Health == null || !s.Health.IsAlive)
            {
                SoltarAsignado();
                if (hud != null) hud.Aviso($"{titulo}: EL ALIADO CAYO · VOLVE A MANDAR A ALGUIEN CON [Q]", 3f);
                return;
            }
            if (OrderService.LoManejaElJugador(s)) { SoltarAsignado(); return; }   // el jugador lo tomo: opera el con [E]

            float d = Distancia(s);
            if (d <= radio)
            {
                if (!modoSostener)
                {
                    SoltarAsignado();
                    Enganchar(s);   // la computadora: queda hackeando solo, igual que cuando el jugador lo hace
                    return;
                }
                if (!asignadoTrabajando)
                {
                    asignadoTrabajando = true;
                    if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.Quieto = true; }
                    var dir = transform.position - s.transform.position; dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
                Avanzar(dt, s);
                AccionesEnCurso.Reportar(s, "DESACTIVANDO", transform.position, Progreso01, (1f - Progreso01) * duracion, transform);
                return;
            }
            // Todavia no llego (o el combate lo desvio): se le reitera el destino.
            if (Time.time >= proximoReintento)
            {
                proximoReintento = Time.time + 1.5f;
                bool yendo = s.Brain != null && s.Brain.State == SP.Ai.AiState.MovingToOrder;
                if (!yendo) OrderService.IssueMoveOrder(s, PuntoDeTrabajo(s));
            }
        }

        // El jugador apreto [E] apuntando al panel pero esta lejos: avisa que se acerque.
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
            string msg = $"ACERCATE AL {titulo} · ESTAS A {d:0} m (MAX {radio:0.#} m)";
            if (hud != null) hud.Aviso(msg, 1.6f); else AlertQueue.Push(msg, AlertPriority.Media, 1.6f);
        }

        void Update()
        {
            if (Completo || !Habilitado) return;
            var hud = OperacionHud.Instancia;
            var yo = Poseido();

            UpdateAsignado(Time.deltaTime);
            AvisarSiEstaLejos(yo);
            if (modoSostener) UpdateSostener(yo, hud, Time.deltaTime);
            else UpdateHacker(yo, hud, Time.deltaTime);
            ActualizarVisual();
        }

        void UpdateSostener(Soldier yo, OperacionHud hud, float dt)
        {
            bool cerca = yo != null && yo.Health.IsAlive && Distancia(yo) <= radio;
            bool sosteniendo = false;
            if (cerca)
            {
                if (!PuedeOperar(yo))
                {
                    UltimoMensaje = $"SOLO EL {nombreDelRol} PUEDE DESACTIVARLO · APUNTALE Y TOCA [Q] PARA MANDARLO";
                    // Mientras el cartel rojo de rol (AvisoCentral) esta en pantalla el prompt viejo sobra: decian lo mismo y se encimaban.
                    if (hud != null && SP.Presentation.AvisoCentral.TextoActual == null) hud.Prompt(UltimoMensaje);
                    // Bug #083/#084: apretar [E] con el rol equivocado no hacia nada. Ahora dice que rol hace falta.
                    if (EToque() || EMantenida()) RolRequerido.Avisar(yo, rol, "DESACTIVAR " + titulo);
                }
                else
                {
                    sosteniendo = EMantenida();
                    UltimoMensaje = $"MANTEN [E] PARA DESACTIVAR {titulo} · {Mathf.RoundToInt(Progreso01 * 100f)} %";
                    if (hud != null) hud.Prompt(UltimoMensaje, Progreso01);
                    // El que mantiene [E] tambien se ve trabajando (igual que el aliado mandado con [Q]).
                    if (sosteniendo) AccionesEnCurso.Reportar(yo, "DESACTIVANDO", transform.position, Progreso01, (1f - Progreso01) * duracion, transform);
                }
            }
            // Si hay un aliado trabajando (mandado con [Q]) el progreso no decae aunque nadie mantenga [E].
            bool alguienTrabaja = asignadoTrabajando && Asignado != null;
            Avanzar(sosteniendo ? dt : (decae && !alguienTrabaja ? -dt * 0.6f : 0f), yo);
        }

        void UpdateHacker(Soldier yo, OperacionHud hud, float dt)
        {
            if (Operador != null && (Operador.Health == null || !Operador.Health.IsAlive))
            {
                SoltarOperador();
                if (hud != null) hud.Aviso("EL SOLDADO QUE HACKEABA CAYO · OTRO DEBE RETOMAR CON [E] (O MANDALO CON [Q])", 3f);
            }

            if (Operador == null)
            {
                bool alLado = yo != null && yo.Health.IsAlive && Distancia(yo) <= radio;
                if (alLado && !PuedeOperar(yo))
                {
                    if (EToque()) RolRequerido.Avisar(yo, rol, "OPERAR " + titulo);
                    return;
                }
                bool cerca = alLado && PuedeOperar(yo);
                if (cerca)
                {
                    UltimoMensaje = Progreso01 > 0f ? $"[E] RETOMAR LA COMPUTADORA · {Mathf.RoundToInt(Progreso01 * 100f)} %" : "[E] INTERACTUAR CON LA COMPUTADORA (el soldado se queda 30 s)";
                    if (hud != null) hud.Prompt(UltimoMensaje, Progreso01 > 0f ? Progreso01 : -1f);
                    if (EToque()) Enganchar(yo);
                }
                return;
            }

            bool pegado = Distancia(Operador) <= radio + 1.2f;
            if (pegado) Avanzar(dt, Operador);
            else if (hud != null) hud.Prompt("EL SOLDADO SE ALEJO DE LA COMPUTADORA: EL PROGRESO ESTA PAUSADO");
            if (pegado && yo == Operador && hud != null) hud.Prompt($"HACKEANDO · {Mathf.CeilToInt((1f - Progreso01) * duracion)} s · defiende a {Operador.DisplayName} (podes cambiar de soldado con [TAB])", Progreso01);
            if (pegado) AccionesEnCurso.Reportar(Operador, "HACKEANDO", transform.position, Progreso01, (1f - Progreso01) * duracion, transform);
        }

        public void Enganchar(Soldier s)
        {
            if (s == null || Completo) return;
            Operador = s;
            if (s.Brain != null) { s.Brain.CancelOrder(); s.Brain.Quieto = true; }
            // Se planta mirando a la pantalla.
            var dir = transform.position - s.transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        void SoltarOperador()
        {
            if (Operador != null && Operador.Brain != null) Operador.Brain.Quieto = false;
            Operador = null;
        }

        // Publico para las pruebas y para que el director lo fuerce.
        public void Avanzar(float dt, Soldier quien = null)
        {
            if (Completo || duracion <= 0f) return;
            Progreso01 = Mathf.Clamp01(Progreso01 + dt / duracion);
            if (Progreso01 >= 1f) Terminar();
        }

        void Terminar()
        {
            Completo = true;
            SoltarOperador();
            SoltarAsignado();
            Pintar(Verde);
            ActualizarVisual();
            Completado?.Invoke(this);
        }

        public void ResetearProgreso() { Completo = false; Progreso01 = 0f; SoltarOperador(); SoltarAsignado(); Pintar(Rojo); ActualizarVisual(); }

        void ActualizarVisual()
        {
            if (indicador != null)
            {
                if (!indicadorInit) { indicadorInit = true; indicadorEscalaBase = indicador.localScale; }
                indicador.localScale = new Vector3(Mathf.Max(0.001f, indicadorEscalaBase.x * Progreso01), indicadorEscalaBase.y, indicadorEscalaBase.z);
            }
            if (!Completo) Pintar(Progreso01 > 0.001f ? Ambar : Rojo);
        }

        MaterialPropertyBlock mpb;
        void Pintar(Color c)
        {
            if (luz == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            luz.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_EmissionColor", c * 0.9f);
            luz.SetPropertyBlock(mpb);
        }
    }
}
