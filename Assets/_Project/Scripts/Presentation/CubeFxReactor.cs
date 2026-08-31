using System;
using System.Collections;
using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Presentation
{
    // Único puente entre el bus de eventos y lo que se ve/oye de un soldado.
    // No decide nada de gameplay: solo reacciona. Se auto-inicializa en Awake
    // leyendo su propio Soldier, para sobrevivir a un domain reload.
    [RequireComponent(typeof(AudioSource))]
    public class CubeFxReactor : MonoBehaviour
    {
        Soldier soldier;
        AudioSource audioSource;
        Renderer rend;
        Color baseColor;
        Vector3 baseScale;
        bool bootstrapped;

        IDisposable damageSub, deathSub, shotSub;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            soldier = GetComponent<Soldier>();
            rend = GetComponentInChildren<Renderer>();
            baseColor = rend != null ? rend.sharedMaterial.color : Color.white;
            baseScale = transform.localScale;

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
            deathSub = EventBus.Instance.Subscribe<EntityDiedEvent>(OnDeath);
            shotSub = EventBus.Instance.Subscribe<ShotFiredEvent>(OnShot);
        }

        void OnDestroy()
        {
            damageSub?.Dispose();
            deathSub?.Dispose();
            shotSub?.Dispose();
        }

        bool IsMe(int actorId) => soldier != null && soldier.Id == actorId;

        static readonly Color MuzzleFlashColor = new Color(1f, 0.92f, 0.6f);

        void OnShot(ShotFiredEvent evt)
        {
            if (!Application.isPlaying || !IsMe(evt.ShooterId) || !gameObject.activeInHierarchy) return;
            audioSource.PlayOneShot(GenericSfx.Get(SfxKind.Shoot));

            // Fogonazo en la boca del arma: no habia ninguna señal en el
            // arma misma al disparar, el unico indicio era el proyectil
            // que ya salio (a veces ni se ve). Reusa ImpactFx -- mismo
            // crecer/achicar, solo que bien chico y bien rapido.
            var muzzle = soldier != null && soldier.Weapon != null ? soldier.Weapon.Muzzle : null;
            var flashPos = muzzle != null ? muzzle.position : transform.position;
            ImpactFx.Spawn(flashPos, MuzzleFlashColor, 0.22f, 0.08f);
        }

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || !IsMe(evt.TargetId) || !gameObject.activeInHierarchy) return;
            audioSource.PlayOneShot(GenericSfx.Get(SfxKind.Hit));
            StopAllCoroutines();
            StartCoroutine(FlashAndPunch());
        }

        void OnDeath(EntityDiedEvent evt)
        {
            if (!Application.isPlaying || !IsMe(evt.ActorId) || !gameObject.activeInHierarchy) return;
            audioSource.PlayOneShot(GenericSfx.Get(SfxKind.Death));

            // Si la muerte llega en medio del flash de daño, StopAllCoroutines
            // corta el lerp a mitad de camino y el material queda pegado en
            // blanco. Hay que devolverlo a su color antes de caer.
            StopAllCoroutines();
            transform.localScale = baseScale;
            if (rend != null) rend.sharedMaterial.color = baseColor;

            StartCoroutine(FallOver());
        }

        IEnumerator FlashAndPunch()
        {
            var punchedColor = Color.white;
            if (rend != null) rend.sharedMaterial.color = punchedColor;
            transform.localScale = baseScale * 1.15f;

            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(baseScale * 1.15f, baseScale, t / 0.15f);
                yield return null;
            }

            transform.localScale = baseScale;
            if (rend != null) rend.sharedMaterial.color = baseColor;
        }

        IEnumerator FallOver()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(90f, 0f, 0f);
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, end, t / 0.6f);
                yield return null;
            }
        }
    }
}
