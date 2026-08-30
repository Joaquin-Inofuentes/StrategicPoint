using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;

namespace SP.UI
{
    // Marca en el borde de pantalla que apunta hacia de donde vino el
    // ultimo golpe. Antes, recibir un disparo desde fuera de camara no
    // daba ninguna pista de la direccion: el jugador giraba al azar
    // buscando al atacante y a menudo moria mientras lo hacia.
    public class DamageDirectionView : MonoBehaviour
    {
        Image arrow;
        PlayerBrain brain;
        Coroutine routine;

        public void Bind(Image arrowImage, PlayerBrain playerBrain)
        {
            arrow = arrowImage;
            brain = playerBrain;
            arrow.gameObject.SetActive(false);
        }

        IDisposable sub;

        // Initialize() se llama al armar la escena en el Editor (fuera de
        // Play mode), y esa suscripcion al EventBus NO sobrevive al
        // domain reload al entrar en Play -- mismo motivo por el que
        // AimUI/DamageVignetteView vuelven a suscribirse solas en
        // OnEnable en vez de confiar en la suscripcion original.
        void OnEnable()
        {
            // `arrow` (asignado por Bind() al armar la escena en Editor)
            // es un campo privado comun -- no sobrevive al domain reload
            // de entrar en Play mode, igual que `brain`. Se re-busca por
            // nombre entre los hijos.
            if (arrow == null)
            {
                var t = transform.Find("Arrow");
                if (t != null) arrow = t.GetComponent<Image>();
            }
            if (brain == null) brain = FindAnyObjectByType<PlayerBrain>();
            if (sub == null) Initialize();
        }

        public void Initialize()
        {
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        void OnDestroy() => sub?.Dispose();

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || arrow == null || brain == null || brain.Current == null) return;
            if (evt.TargetId != brain.Current.Id) return;

            var attacker = ActorRegistry.FindById(evt.AttackerId);
            if (attacker == null) return;

            // Angulo entre hacia-donde-mira el jugador y hacia-donde-esta
            // el atacante, medido en el plano horizontal (Y no importa
            // para "de que lado viene el tiro").
            Vector3 toAttacker = attacker.transform.position - brain.Current.transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return;

            Vector3 forward = brain.Current.transform.forward;
            forward.y = 0f;

            float signedAngle = Vector3.SignedAngle(forward, toAttacker, Vector3.up);
            // La flecha en el Canvas rota en el plano de pantalla: un
            // atacante a la derecha (angulo positivo en mundo) debe
            // rotar la flecha en sentido horario, por eso el signo se
            // invierte respecto del giro de mundo.
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -signedAngle);

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowAndHide());
        }

        IEnumerator ShowAndHide()
        {
            arrow.gameObject.SetActive(true);
            arrow.color = new Color(0.95f, 0.25f, 0.2f, 1f);
            const float holdTime = 0.9f;
            float t = 0f;
            while (t < holdTime)
            {
                t += Time.deltaTime;
                arrow.color = new Color(0.95f, 0.25f, 0.2f, Mathf.Lerp(1f, 0f, t / holdTime));
                yield return null;
            }
            arrow.gameObject.SetActive(false);
        }
    }
}
