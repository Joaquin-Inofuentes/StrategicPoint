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
    // WP9b (#097): H2 de la huida. Kes (el Flanqueador) dice "¡Puedo repararlo, cubrime!", recibe una orden forzada de ir al tanque y lo repara
    // 25 s con la animacion de reparar (AnimacionDeAccion.Reparar + engranajes de AccionesEnCurso, WP5b). El jugador tiene que cubrirla:
    //   * el progreso se pausa si Kes esta caida ("KES CAIDA") o si hay un enemigo a 4 m o menos de ella ("¡KES BAJO FUEGO!");
    //   * si el jugador ES Kes, el que repara es el Flanqueador poseido: mantener [E] pegado al tanque (mismo componente, mismo progreso);
    //   * la vida del tanque sube con el progreso (de 5 % a 70 %); al terminar el director lo pasa al bando del jugador.
    public class ReparacionDeTanque : MonoBehaviour
    {
        public const float SegundosDeReparacion = 25f;
        public const float RadioParaTrabajar = 4.3f, DistanciaDeEnemigo = 4f;
        public const float VidaInicial01 = 0.05f, VidaFinal01 = 0.70f;
        public const string Verbo = "REPARANDO";
        public const string AvisoDeInicio = "¡Está casi entero! ¡Puedo repararlo, cubrime!";

        public static ReparacionDeTanque Activa { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => Activa = null;

        public Vehicle Tanque { get; private set; }
        public Soldier Reparador { get; private set; }
        public float Progreso01 { get; private set; }
        public bool Pausada { get; private set; }
        public string MotivoDePausa { get; private set; } = "";
        public bool Completa { get; private set; }
        public bool ElJugadorRepara { get; private set; }
        public float SegundosPausados { get; private set; }
        public int AvisosBajoFuego { get; private set; }
        public int Intervalos { get; private set; }
        public string TextoDeBarra => $"REPARANDO EL BLINDADO · {Mathf.RoundToInt(Progreso01 * 100f)}%";
        public event Action AlCompletar;

        float proximaOrden, proximoAviso;
        bool trabajando;
        Soldier quietado;

        public static ReparacionDeTanque Iniciar(Vehicle tanque)
        {
            if (Activa != null) Destroy(Activa.gameObject);
            var go = new GameObject("ReparacionDeTanque");
            var r = go.AddComponent<ReparacionDeTanque>();
            r.Tanque = tanque;
            Activa = r;
            return r;
        }

        void OnDestroy()
        {
            SoltarReparador();
            if (Activa == this) Activa = null;
        }

        static Soldier Poseido()
        {
            var d = PlayerInputDriver.Activo;
            return d != null && d.Brain != null ? d.Brain.Current : null;
        }

        static Vector3 Plano(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return a - b; }

        // El Flanqueador vivo de la escuadra (poseido o no). null si no hay.
        public static Soldier BuscarFlanqueador()
        {
            Soldier mejor = null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role != RoleType.Flanker || s.Health == null) continue;
                if (!s.gameObject.activeInHierarchy) continue;
                if (s.Health.IsAlive) return s;
                mejor = s;   // caida: se queda como candidata (la reparacion espera a que la revivan)
            }
            return mejor;
        }

        // Cuantos enemigos vivos hay a menos de "radio" metros de un punto.
        public static int EnemigosCerca(Vector3 punto, float radio)
        {
            int n = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (Plano(s.transform.position, punto).magnitude <= radio) n++;
            }
            return n;
        }

        Vector3 PuntoDeTrabajo(Soldier s) => Tanque.ClosestBoardingPoint(s.transform.position);

        void Update()
        {
            if (Tanque == null || Completa) return;
            float dt = Time.deltaTime;
            var kes = BuscarFlanqueador();
            if (kes != Reparador) { SoltarReparador(); Reparador = kes; }
            if (Reparador == null) { Pausar("NO HAY FLANQUEADOR"); return; }

            bool vivo = Reparador.Health != null && Reparador.Health.IsAlive;
            if (!vivo) { SoltarReparador(); Pausar("KES CAIDA · REVIVILA"); return; }

            ElJugadorRepara = Poseido() == Reparador;
            float d = Plano(Reparador.transform.position, Tanque.transform.position).magnitude;
            bool cerca = d <= RadioParaTrabajar;
            bool quiere;
            if (ElJugadorRepara)
            {
                // Mantener [E] pegado al tanque.
                quiere = cerca && OperacionTerminal.EMantenida();
                var hud = OperacionHud.Instancia;
                if (hud != null && !quiere) hud.Prompt(cerca ? "MANTENE [E] PARA REPARAR EL BLINDADO" : "ACERCATE AL BLINDADO PARA REPARARLO");
                trabajando = quiere;
            }
            else
            {
                // Orden forzada: ir al tanque (se reemite aunque el jugador le haya dado otra) y quedarse trabajando.
                if (!cerca)
                {
                    trabajando = false;
                    SoltarQuieto();
                    if (Time.time >= proximaOrden)
                    {
                        proximaOrden = Time.time + 1.2f;
                        OrderService.IssueMoveOrder(Reparador, PuntoDeTrabajo(Reparador));
                    }
                }
                else if (!trabajando)
                {
                    trabajando = true;
                    if (Reparador.Brain != null) { Reparador.Brain.CancelOrder(); Reparador.Brain.Quieto = true; quietado = Reparador; }
                }
                quiere = cerca;
                if (cerca)
                {
                    var dirT = Tanque.transform.position - Reparador.transform.position; dirT.y = 0f;
                    if (dirT.sqrMagnitude > 0.01f) Reparador.transform.rotation = Quaternion.Slerp(Reparador.transform.rotation, Quaternion.LookRotation(dirT.normalized), 1f - Mathf.Exp(-8f * dt));
                }
            }
            if (!quiere) { Pausada = !ElJugadorRepara && !cerca; MotivoDePausa = ElJugadorRepara ? "" : (cerca ? "" : "KES VA AL BLINDADO"); return; }

            // Hay un enemigo pegado a ella: no avanza.
            if (EnemigosCerca(Reparador.transform.position, DistanciaDeEnemigo) > 0)
            {
                Pausar("¡KES BAJO FUEGO!");
                if (Time.time >= proximoAviso)
                {
                    proximoAviso = Time.time + 1.5f;
                    AvisosBajoFuego++;
                    AvisoCentral.Mostrar("¡KES BAJO FUEGO!", 1.4f, new Color(0.55f, 0.07f, 0.05f, 0.92f));
                }
                return;
            }
            Pausada = false; MotivoDePausa = "";
            Progreso01 = Mathf.Clamp01(Progreso01 + dt / SegundosDeReparacion);
            Tanque.PonerVida01(Mathf.Lerp(VidaInicial01, VidaFinal01, Progreso01));
            AccionesEnCurso.Reportar(Reparador, Verbo, Tanque.transform.position, Progreso01, (1f - Progreso01) * SegundosDeReparacion, Tanque.transform);
            if (Progreso01 >= 1f) Terminar();
        }

        void Pausar(string motivo)
        {
            if (!Pausada) Intervalos++;
            Pausada = true; MotivoDePausa = motivo;
            SegundosPausados += Time.deltaTime;
        }

        void SoltarQuieto()
        {
            if (quietado != null && quietado.Brain != null) quietado.Brain.Quieto = false;
            quietado = null;
        }

        void SoltarReparador()
        {
            SoltarQuieto();
            trabajando = false;
            if (Reparador != null) AccionesEnCurso.Terminar(Reparador);
        }

        // Para las pruebas: da la reparacion por terminada ya.
        public void CompletarYa()
        {
            if (Completa) return;
            Progreso01 = 1f;
            Terminar();
        }

        // Restauraciones de sesion: retoma el progreso donde estaba.
        public void FijarProgreso(float p)
        {
            if (Completa) return;
            Progreso01 = Mathf.Clamp01(p);
            Tanque.PonerVida01(Mathf.Lerp(VidaInicial01, VidaFinal01, Progreso01));
        }

        void Terminar()
        {
            if (Completa) return;
            Completa = true;
            Tanque.PonerVida01(VidaFinal01);
            SoltarReparador();
            AlCompletar?.Invoke();
        }
    }
}
