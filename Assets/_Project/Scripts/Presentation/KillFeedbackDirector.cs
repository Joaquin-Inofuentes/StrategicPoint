using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Un solo lugar que decide como se comunica una baja. Estaba repartido
    // (el kill feed apilaba lineas por su cuenta, la mirilla hacia lo suyo)
    // y ninguna parte sabia de las otras, asi que en combate masivo se
    // superponian avisos y ninguno quedaba legible.
    public class KillFeedbackDirector : MonoBehaviour
    {
        public static KillFeedbackDirector Instance { get; private set; }

        IDisposable diedSub, damageSub;

        // --- 168 agrupacion de bajas proximas en el tiempo ---
        const float GroupWindow = 1.5f;
        float groupUntil;
        public int GroupedKills { get; private set; }

        // --- 169 racha ---
        const float StreakTimeout = 6f;
        float streakExpiresAt;
        public int Streak { get; private set; }

        // --- 171 quien hizo la baja ---
        public bool LastKillWasPlayer { get; private set; }

        // --- 174 quien te mato ---
        public Soldier LastKiller { get; private set; }

        public SP.Player.PlayerBrain Brain;
        public UI.OffscreenKillMarkerView OffscreenMarker;
        public UI.KillFeedView Feed;

        void OnEnable()
        {
            Instance = this;
            diedSub?.Dispose();
            damageSub?.Dispose();
            diedSub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnDied);
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        void OnDisable()
        {
            diedSub?.Dispose();
            damageSub?.Dispose();
            // El disparador de la camara lenta es "murio el ultimo
            // enemigo", o sea justo cuando la pantalla de victoria puede
            // desactivar esto a mitad de la corrutina. Sin este cierre,
            // Time.timeScale se quedaba en 0.25 PARA SIEMPRE.
            EndSlowMotion();
            if (Instance == this) Instance = null;
        }

        // La racha se corta al recibir daño, no solo por tiempo: es lo que
        // le da tension (podes perderla) en vez de ser un contador que solo
        // sube.
        void OnDamage(DamageTakenEvent evt)
        {
            if (Brain == null || Brain.Current == null || evt.TargetId != Brain.Current.Id) return;
            Streak = 0;
            LastKiller = ActorRegistry.FindById(evt.AttackerId);
        }

        void OnDied(EntityDiedEvent evt)
        {
            var victim = ActorRegistry.FindById(evt.ActorId);
            if (victim == null || victim.Team != TeamId.Enemy) return;

            bool byPlayer = Brain != null && Brain.Current != null
                && victim.Health != null && victim.Health.LastAttackerId == Brain.Current.Id;
            LastKillWasPlayer = byPlayer;

            GroupedKills = Time.time <= groupUntil ? GroupedKills + 1 : 1;
            groupUntil = Time.time + GroupWindow;

            if (byPlayer)
            {
                Streak = Time.time <= streakExpiresAt ? Streak + 1 : 1;
                streakExpiresAt = Time.time + StreakTimeout;
                PlayStreakTone(Streak);
            }

            // Recien ahora que el estado de arriba esta actualizado se le
            // avisa al feed, que lee FeedText()/LastKillWasPlayer.
            if (Feed != null) Feed.ShowKill();

            if (Application.isPlaying) StartCoroutine(SilhouetteFlash(victim));
            if (OffscreenMarker != null) OffscreenMarker.Report(victim.transform.position);

            TrySlowMotionOnLastKill();
        }

        // 167: destello del contorno al morir. Un cubo agrandado y pintado
        // detras del cuerpo hace de contorno sin necesitar un shader
        // dedicado, que este proyecto no usa en ningun otro lado.
        static readonly Color SilhouetteColor = new Color(1f, 0.95f, 0.7f);

        IEnumerator SilhouetteFlash(Soldier victim)
        {
            var rend = victim.GetComponentInChildren<Renderer>();
            if (rend == null) yield break;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DeathSilhouette";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = victim.transform.position;
            go.transform.rotation = victim.transform.rotation;
            var baseScale = victim.transform.localScale * 1.25f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(shader) { color = SilhouetteColor };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = t / duration;
                go.transform.localScale = Vector3.Lerp(baseScale, baseScale * 1.6f, k);
                if (victim != null) go.transform.position = victim.transform.position;
                yield return null;
            }
            // Sin residuo: se destruye entero, no queda un objeto invisible
            // acumulandose por cada baja. Destruir el GameObject NO libera
            // el Material creado en runtime -- queda huerfano hasta cambiar
            // de escena, o sea un material filtrado por cada baja.
            if (mr != null && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
            Destroy(go);
        }

        // 169: tono que sube con la racha. Se reusa la paleta existente
        // cambiandole el pitch, en vez de generar un clip por nivel.
        void PlayStreakTone(int streak)
        {
            if (!Application.isPlaying || streak < 2) return;
            // Mismo caso que el tono critico de la mirilla: hace falta un
            // AudioSource propio porque el pitch varia. El bloque vivia
            // duplicado en los dos lados; ahora es un solo helper.
            GenericSfx.PlayOneShot2D(
                GenericSfx.Get(SfxKind.Swap),
                0.5f,
                Mathf.Min(2f, 1f + (streak - 1) * 0.16f),
                "StreakTone");
        }

        // 170: la ultima baja de la partida es el climax y era
        // indistinguible de cualquier otra. Se restaura timeScale ANTES de
        // que aparezca la pantalla final: dejar el tiempo alterado ahi
        // rompe la UI de victoria (que corre en timeScale 0).
        public const float SlowMotionScale = 0.25f;
        public const float SlowMotionSeconds = 0.9f;
        public bool SlowMotionActive { get; private set; }

        void TrySlowMotionOnLastKill()
        {
            if (SlowMotionActive) return;
            if (ActorRegistry.CountAlive(TeamId.Enemy) > 0) return;
            if (Application.isPlaying) StartCoroutine(SlowMotionRoutine());
        }

        IEnumerator SlowMotionRoutine()
        {
            SlowMotionActive = true;
            Time.timeScale = SlowMotionScale;
            // unscaled: si se esperara en tiempo de juego, la propia camara
            // lenta estiraria su propia duracion.
            yield return new WaitForSecondsRealtime(SlowMotionSeconds);
            EndSlowMotion();
        }

        // Se restaura a 1 y NO al valor capturado al arrancar, y solo si el
        // timeScale sigue siendo el nuestro. Antes se guardaba el valor
        // previo y se reescribia al final: si el jugador pausaba durante
        // esos 0.9 s reales (la pausa pone timeScale en 0), al terminar la
        // corrutina se le escribia el 1 guardado encima y el juego se
        // despausaba solo, por detras del panel de pausa.
        void EndSlowMotion()
        {
            if (!SlowMotionActive) return;
            SlowMotionActive = false;
            if (Mathf.Approximately(Time.timeScale, SlowMotionScale)) Time.timeScale = 1f;
        }

        // Texto ya agrupado para el feed, en vez de una linea por baja.
        public string FeedText()
        {
            string who = LastKillWasPlayer ? "ABATIDO" : "ABATIDO POR TU ESCUADRA";
            if (GroupedKills > 1) return $"{who} x{GroupedKills}";
            if (LastKillWasPlayer && Streak >= 3) return $"{who}   ·   RACHA {Streak}";
            return who;
        }
    }
}
