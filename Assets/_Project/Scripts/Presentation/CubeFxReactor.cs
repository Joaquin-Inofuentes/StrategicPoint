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

            // El AudioSource propio YA NO reproduce nada: todo el audio de
            // este reactor pasa por AudioDirector (items 186-193). Se sigue
            // configurando igual porque el componente es obligatorio por
            // [RequireComponent] y el constructor de escena lo agrega a
            // cada soldado: dejarlo con playOnAwake en true haria sonar el
            // clip que quedara asignado en cuanto el soldado se habilite.
            //
            // POR QUE SE FUE: con cincuenta soldados habia cincuenta
            // AudioSource compitiendo sin ningun limite global. El director
            // tiene 24 voces 3D y decide a quien le toca; ademas aplica la
            // ganancia de canal, la atenuacion por distancia y el filtro,
            // que un PlayOneShot suelto no aplica.
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
            var kind = soldier.Weapon != null ? soldier.Weapon.CurrentWeaponKind : SP.Combat.WeaponKind.Rifle;

            // Fogonazo en la boca del arma: no habia ninguna señal en el
            // arma misma al disparar, el unico indicio era el proyectil
            // que ya salio (a veces ni se ve). Reusa ImpactFx -- mismo
            // crecer/achicar, solo que bien chico y bien rapido.
            var muzzle = soldier != null && soldier.Weapon != null ? soldier.Weapon.Muzzle : null;
            var flashPos = muzzle != null ? muzzle.position : transform.position;
            ImpactFx.Spawn(flashPos, MuzzleFlashColor, 0.22f, 0.08f);

            // El disparo suena en la BOCA del arma, el mismo punto donde se
            // dibuja el fogonazo: antes salia del AudioSource del cubo, o
            // sea del centro del cuerpo. La diferencia es chica de cerca
            // pero es la que hace que ver y oir coincidan.
            //
            // Prioridad media-alta: un disparo es la senal mas util del
            // combate (te dice de donde te tiran) pero pierde contra una
            // muerte o el cañon, que son sucesos unicos.
            AudioDirector.PlayClipAt(GenericSfx.GetWeaponShot(kind), flashPos, 0.9f, 0.6f);
        }

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || !IsMe(evt.TargetId) || !gameObject.activeInHierarchy) return;
            // Voz propia del herido, distinta del "tac" del impacto: mas
            // grave, para que se lea como un quejido y no como el mismo
            // golpe metalico que ya suena en la mirilla del que dispara.
            // Un enemigo herido y uno muerto tienen que sonar distinto o
            // el audio no ayuda a decidir si seguir tirandole.
            //
            // Es un clip propio y NO el AudioSource a pitch 0.75: este
            // AudioSource lo comparten OnShot y OnDeath, y PlayOneShot no
            // congela el pitch -- lo lee en vivo cada frame, asi que
            // devolverlo a 1 en la linea siguiente borraba el efecto antes
            // de que sonara una sola muestra. Tampoco un AudioSource
            // temporal: son 50 soldados comiendo balas, un GameObject por
            // impacto seria basura por frame justo en el peor momento.
            //
            // Ahora ademas va por AudioDirector: el clip propio sigue
            // siendo la solucion correcta (el director tampoco expone
            // pitch), pero la voz sale del pool con limite global, con la
            // atenuacion y el filtro por distancia del item 187, y con la
            // ganancia del canal de efectos. Prioridad un poco por encima
            // del disparo: saber que le pegaste a alguien decide si seguis
            // tirandole o pasas al siguiente.
            AudioDirector.PlayAt(SfxKind.Wounded, transform.position, 0.7f, 0.65f);
            StopAllCoroutines();
            StartCoroutine(FlashAndPunch());
        }

        void OnDeath(EntityDiedEvent evt)
        {
            if (!Application.isPlaying || !IsMe(evt.ActorId) || !gameObject.activeInHierarchy) return;
            // Prioridad la mas alta de las tres: una muerte pasa UNA vez
            // por soldado, un disparo pasa varias veces por segundo. Si el
            // pool esta saturado, lo que tiene que sobrevivir es esto.
            AudioDirector.PlayAt(SfxKind.Death, transform.position, 1f, 0.8f);

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
