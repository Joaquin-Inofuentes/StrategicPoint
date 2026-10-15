using System;
using UnityEngine;
using SP.Actors;
using SP.Combat;

namespace SP.Core
{
    // Bug #240 ("quiero un sistema de dificultad para que se ajuste y sea desafiante, y que vayan variando los danos de los enemigos").
    // Encima de la dificultad elegida (Dificultad), un DIRECTOR ADAPTATIVO mide como le va a la escuadra y mueve el dano de los enemigos:
    //   - si la escuadra esta sana y casi no recibe dano, el reto SUBE (hasta x1,6);
    //   - si hay caidos recientes o la escuadra viene muy golpeada, el reto BAJA (hasta x0,7) para que pueda reponerse;
    //   - ademas el dano "respira" (una ola lenta de +-12 %) y cada enemigo tiene su propio rasgo fijo (+-15 %: unos pegan flojo y otros duro),
    //     asi no todos los tiros valen lo mismo ni el nivel se siente plano.
    // Solo rige en la partida principal (Dificultad.Activa); el tutorial y la suite headless siguen deterministas (factor x1).
    public static class DificultadAdaptativa
    {
        public const float Minimo = 0.7f, Maximo = 1.6f, PeriodoDeEvaluacion = 4f, PasoMaximo = 0.06f;
        public const float AmplitudDeLaOla = 0.12f, SegundosDeLaOla = 22f, AmplitudDeRasgo = 0.15f, VentanaDeCaidos = 45f;

        public static bool Habilitada = true;
        // Para checks: congela el factor (null = se mueve solo).
        public static float? Forzado;

        public static float Factor { get; private set; } = 1f;
        public static float Objetivo { get; private set; } = 1f;
        public static float VidaPromedioDeLaEscuadra { get; private set; } = 1f;
        public static int CaidosRecientes { get; private set; }
        public static int Evaluaciones { get; private set; }

        static float proximaEvaluacion;
        static readonly System.Collections.Generic.List<float> caidos = new System.Collections.Generic.List<float>();
        static float danoAcumulado; static float ultimoDano = -99f;
        static IDisposable subMuerte, subDano;
        static float ultimoLogFactor = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            Factor = Objetivo = 1f; VidaPromedioDeLaEscuadra = 1f; CaidosRecientes = 0; Evaluaciones = 0;
            proximaEvaluacion = 0f; caidos.Clear(); danoAcumulado = 0f; ultimoDano = -99f; ultimoLogFactor = 1f;
            subMuerte?.Dispose(); subDano?.Dispose(); subMuerte = subDano = null; Forzado = null; Habilitada = true;
        }

        // Una partida nueva (o un reintento) arranca otra vez en x1.
        public static void Resetear()
        {
            Factor = Objetivo = 1f; CaidosRecientes = 0; caidos.Clear(); danoAcumulado = 0f; ultimoDano = -99f; ultimoLogFactor = 1f;
            proximaEvaluacion = 0f;
        }

        static void Suscribir()
        {
            if (subMuerte != null) return;
            subMuerte = EventBus.Instance.Subscribe<EntityDiedEvent>(e =>
            {
                var s = ActorRegistry.FindById(e.ActorId);
                if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian) caidos.Add(Time.time);
            });
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(e =>
            {
                var s = ActorRegistry.FindById(e.TargetId);
                if (s != null && s.Team == TeamId.Player && s.Role != RoleType.Civilian) { danoAcumulado += e.Amount; ultimoDano = Time.time; }
            });
        }

        // Multiplicador del dano de UN golpe enemigo (id del atacante; -1 = sin dueno: torreta, explosion, vehiculo).
        public static float MultiplicadorEnemigo(int atacanteId)
        {
            if (!Habilitada) return 1f;
            float f = Forzado ?? Factor;
            float ola = 1f + AmplitudDeLaOla * Mathf.Sin(Time.time / SegundosDeLaOla * 2f * Mathf.PI);
            return f * ola * RasgoDelEnemigo(atacanteId);
        }

        // Cada enemigo trae un rasgo fijo (segun su id): 0,85 a 1,15.
        public static float RasgoDelEnemigo(int id)
        {
            if (id < 0) return 1f;
            uint h = (uint)id * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            float u = (h & 0xFFFFu) / 65535f;
            return 1f + (u * 2f - 1f) * AmplitudDeRasgo;
        }

        // Se llama desde un ticker de escena (DificultadAdaptativaTicker) o a mano desde las pruebas.
        public static void Tick(float ahora)
        {
            if (!Habilitada || !Dificultad.Activa) return;
            Suscribir();
            if (ahora < proximaEvaluacion) return;
            proximaEvaluacion = ahora + PeriodoDeEvaluacion;
            Evaluar(ahora);
        }

        public static void Evaluar(float ahora)
        {
            Evaluaciones++;
            caidos.RemoveAll(t => ahora - t > VentanaDeCaidos);
            CaidosRecientes = caidos.Count;

            float suma = 0f; int n = 0;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null || !s.gameObject.activeInHierarchy) continue;
                n++;
                if (s.Health.IsAlive && s.Health.MaxHealth > 0) suma += (float)s.Health.Current / s.Health.MaxHealth;
            }
            VidaPromedioDeLaEscuadra = n > 0 ? suma / n : 1f;
            danoAcumulado *= 0.5f;   // el dano pasado pesa cada vez menos

            // Puntaje: > 0 = le va bien a la escuadra (hay que apretar), < 0 = le va mal (hay que aflojar).
            float puntaje = (VidaPromedioDeLaEscuadra - 0.6f) * 1.2f;
            if (ahora - ultimoDano > 12f) puntaje += 0.2f;           // 12 s sin recibir dano: esta dominando
            puntaje -= 0.35f * CaidosRecientes;                       // cada caido reciente baja el reto
            Objetivo = Mathf.Clamp(1f + puntaje * 0.9f, Minimo, Maximo);
            Factor = Mathf.MoveTowards(Factor, Objetivo, PasoMaximo);

            if (Mathf.Abs(Factor - ultimoLogFactor) >= 0.1f)
            {
                ultimoLogFactor = Factor;
                SesionLog.Evento($"DIFICULTAD ADAPTATIVA: dano enemigo x{Factor:0.00} (escuadra {VidaPromedioDeLaEscuadra * 100f:0}% de vida, {CaidosRecientes} caidos recientes)");
            }
        }
    }

    // Lo mueve una sola vez por escena: se crea solo en las escenas de juego (no en el tutorial: Dificultad.Activa = false).
    public class DificultadAdaptativaTicker : MonoBehaviour
    {
        static DificultadAdaptativaTicker instancia;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("DificultadAdaptativaTicker");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<DificultadAdaptativaTicker>();
            DificultadAdaptativa.Resetear();
        }

        void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena; }
        void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena; }
        static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { DificultadAdaptativa.Resetear(); }

        void Update() { DificultadAdaptativa.Tick(Time.time); }
    }
}
