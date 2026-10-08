using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.UI;

namespace SP.Operacion
{
    // WP10 (#101): estado y reglas del objetivo 5, "DIRIGIR LA DEFENSA DESDE LA RADIO". El director (OperacionDirector.Mando.cs) maneja la fase; esta
    // clase es lo que consultan los demas sistemas (camara, input, ordenes) sin conocer al director:
    //   - quien esta en la radio (P7 #120: la radio es un ROL, no un modo de camara: el operador puede ser el jugador o un aliado de IA y el
    //     jugador puede volver a FPS o poseer a otro sin soltarla; ya nada bloquea Tab ni la posesion);
    //   - quienes son los milicianos (aliados de la ciudad que NO suben al helicoptero);
    //   - las REGLAS puras del reloj y de la caida de sectores, aparte del estado, para poder probarlas sin escena.
    public static class MandoTactico
    {
        // ---- Constantes de diseno (las del plan) ----
        public const float SegundosDeAnuncio = 12f;       // la oleada se anuncia 12 s antes de salir (antes 8: Resistir era el unico pico duro)
        public const float SegundosParaCaer = 10f;        // un sector amenazado y vacio cae a los 10 s (antes 6)
        public const float SegundosParaRecuperar = 4f;    // recuperarlo: aliados adentro durante 4 s
        public const int AliadosParaRecuperar = 2;
        // #122: 3 de escuadra + 4 milicianos = 7; menos el operador = 6 = 3 sectores x 2 aliados. (Todos los textos salen de estas constantes.)
        public const int CantidadDeMilicianos = 4;
        public const int CantidadDeSectores = 3;
        // Interruptores SOLO para los checks (reproducir el comportamiento de antes de P7 y medir el "despues"). En el juego quedan en su valor por defecto.
        public static int MilicianosAActivar = CantidadDeMilicianos;   // #122 (antes 2)
        public static bool RadioObligaARts;                             // #120b (antes la radio bloqueaba Tab y la posesion y forzaba RTS)
        public static bool SoltarConToque;                              // #120b (antes un toque de [E] soltaba la radio)
        public const float SegundosParaSoltarLaRadio = 1f;   // #120b: en FPS la radio se suelta MANTENIENDO [E] 1 s (un toque no hace nada)
        public const float FactorDeDanoDelOperador = 0.5f;
        public const float AlcanceDeLaRadio = 3.6f;       // distancia maxima a la radio para apretar [E]
        public const int PasosQueBloqueanElReloj = 4;     // mientras no se completan los pasos 1-4 el reloj no corre

        public static Soldier Operador { get; private set; }
        public static Soldier UltimoOperador { get; private set; }
        public static bool RadioTomada { get; private set; }
        public static readonly HashSet<Soldier> Milicianos = new HashSet<Soldier>();

        // Hay un operador vivo en la radio (el jugador o un aliado de IA). Ya NO implica vista tactica obligatoria.
        public static bool EnRadio => RadioTomada && Operador != null && Operador.Health != null && Operador.Health.IsAlive;

        // El operador lo controla el jugador ahora mismo (si no, es un aliado de IA "OPERANDO LA RADIO", clavado en su puesto).
        public static bool OperadorEsElJugador => EnRadio && Operador.Brain != null && Operador.Brain.IsPossessedByPlayer;
        public static bool EsOperadorDeRadio(Soldier s) => s != null && EnRadio && ReferenceEquals(s, Operador);
        public static bool EsMiliciano(Soldier s) => s != null && Milicianos.Contains(s);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            Operador = null; UltimoOperador = null; RadioTomada = false; Milicianos.Clear();
            PrimerIngresoHecho = false; MilicianosAActivar = CantidadDeMilicianos; RadioObligaARts = false; SoltarConToque = false;
            CameraRig.ProveedorDeRtsForzado = null;
            PosicionDeLaRadio = null;
            CameraRig.LimiteDePaneoRts = null;
        }

        // #119: donde esta la radio de campana mientras dura la fase de Resistir (null fuera de ella).
        public static Vector3? PosicionDeLaRadio;
        public const float RadioDeLaRadioParaApuntar = 3.2f;
        // El colisionador (Radio_Mesa, Radio_Antena...) o el piso pegado a la radio cuentan como "apuntar a la radio".
        public static bool EsParteDeLaRadio(Collider c, Vector3 punto)
        {
            if (!PosicionDeLaRadio.HasValue || c == null) return false;
            var r = PosicionDeLaRadio.Value;
            float d = new Vector2(punto.x - r.x, punto.z - r.z).magnitude;
            if (c.name.StartsWith("Radio_")) return d <= RadioDeLaRadioParaApuntar;
            return d <= 1.2f;
        }

        // ---- Tomar y soltar la radio ----
        public static void Establecer(Soldier operador)
        {
            Operador = operador; UltimoOperador = operador; RadioTomada = operador != null;
            CameraRig.ProveedorDeRtsForzado = RadioObligaARts && RadioTomada ? (System.Func<bool>)(() => EnRadio) : null;   // P7: ya no es obligatoria (salvo el interruptor de pruebas)
            if (operador != null && operador.Health != null) operador.Health.FactorDeDano = FactorDeDanoDelOperador;
        }

        public static void Soltar()
        {
            if (Operador != null && Operador.Health != null) Operador.Health.FactorDeDano = 1f;
            Operador = null; RadioTomada = false;
            CameraRig.ProveedorDeRtsForzado = null;
        }

        // ---- Bloqueos que consultan el input y la posesion ----
        // P7 (#120b): ya no bloquean nada (antes Tab y la posesion quedaban trabados mientras habia operador). Se conservan los metodos porque los
        // consultan PlayerInputDriver y PossessionService; siempre devuelven false.
        // Pista de la primera vez: al tomar la radio la camara pasa a RTS UNA vez como sugerencia y se avisa como volver.
        public static bool PrimerIngresoHecho;
        public const string TextoDeTabLibre = "[TAB] VOLVER A FPS · EL OPERADOR SIGUE EN LA RADIO";
        public const string TextoDeMantenerParaSoltar = "MANTENÉ [E] 1 s PARA SOLTAR LA RADIO";

        public static bool BloqueaElCambioDeVista() => RadioObligaARts && EnRadio;
        public static bool BloqueaLaPosesion() => RadioObligaARts && EnRadio;

        // Aliado vivo y a pie al que pasa el jugador cuando suelta al operador de la radio con [Tab] (el mas cercano al operador).
        public static Soldier AliadoParaRelevar(Soldier operador)
        {
            if (operador == null) return null;
            Soldier mejor = null; float dm = float.MaxValue;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a == operador || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                float d = (a.transform.position - operador.transform.position).sqrMagnitude;
                if (d < dm) { dm = d; mejor = a; }
            }
            return mejor;
        }

        // ---- Reglas puras ----
        // El reloj del helicoptero avanza solo si: se completaron los pasos de ubicacion, alguien esta en la radio, ningun sector esta caido y cada
        // sector amenazado tiene al menos un aliado adentro.
        public static bool RelojCorre(int pasosCompletos, bool enRadio, bool algunSectorCaido, int amenazadosSinAliados)
            => pasosCompletos >= PasosQueBloqueanElReloj && enRadio && !algunSectorCaido && amenazadosSinAliados == 0;

        // Motivo legible de la pausa ("" = el reloj corre).
        public static string MotivoDePausa(int pasosCompletos, bool enRadio, bool algunSectorCaido, int amenazadosSinAliados)
        {
            if (!enRadio) return "RADIO ABANDONADA: EL RELOJ SE DETUVO";
            if (pasosCompletos < PasosQueBloqueanElReloj) return "UBICA A TU EQUIPO PARA QUE EL HELICOPTERO SE ACERQUE";
            if (algunSectorCaido) return "SECTOR CAIDO: " + TextoDeRecuperar();
            if (amenazadosSinAliados > 0) return "SECTOR AMENAZADO SIN DEFENSORES: MANDA ALGUIEN";
            return "";
        }

        // #122: el texto sale de la constante (nunca un "2" escrito a mano).
        public static string TextoDeRecuperar() => $"RECUPERALO CON {AliadosParaRecuperar} ALIADOS";

        // Un sector amenazado, sin aliados adentro, cae cuando lleva SegundosParaCaer vacio.
        public static bool DebeCaer(float segundosVacio) => segundosVacio >= SegundosParaCaer;

        // Un sector caido se recupera con los aliados necesarios adentro durante SegundosParaRecuperar. "necesarios" = 2, o los que haya vivos si son menos.
        public static int AliadosNecesarios(int aliadosVivosSinOperador) => Mathf.Clamp(aliadosVivosSinOperador, 1, AliadosParaRecuperar);
        public static bool SeRecupera(int aliadosAdentro, int aliadosVivosSinOperador, float segundosCumplidos)
            => aliadosAdentro >= AliadosNecesarios(aliadosVivosSinOperador) && segundosCumplidos >= SegundosParaRecuperar;
    }
}
