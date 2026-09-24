using System;
using System.Collections;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;

namespace SP.Presentation
{
    // Único puente entre el bus de eventos y lo que se ve/oye de un soldado.
    // No decide nada de gameplay: solo reacciona. Se auto-inicializa en Awake
    // leyendo su propio Soldier, para sobrevivir a un domain reload.
    [RequireComponent(typeof(AudioSource))]
    public class CubeFxReactor : MonoBehaviour
    {
        // Cuanto queda tirado el cuerpo, ya con la animacion de morir
        // terminada, antes de desaparecer. Pedido explicito del usuario:
        // "quiero q ... luego desaparescan al cabo de 2 segundos".
        const float SegundosHastaDesaparecer = 2f;

        Soldier soldier;
        AudioSource audioSource;
        Renderer rend;
        Animator animator;
        Color baseColor;
        Vector3 baseScale;
        bool bootstrapped;

        IDisposable damageSub, deathSub, shotSub;

        void Awake() => Bootstrap();

        // Revivir (HeadlessTestRunner y AutoDemoRunner llaman Health.Initialize()
        // y despues, en los casos donde el cuerpo se habia ocultado,
        // gameObject.SetActive(true)) tiene que deshacer TODO lo que
        // OnDeath dejo puesto -- collider apagado, cuerpo oculto, Animator
        // trabado en "Muerto" -- y no hay un RevivedEvent en el bus para
        // engancharse. OnEnable es el momento correcto igual: es lo que
        // Unity ya llama cada vez que el GameObject se reactiva, y en el
        // primer spawn (con el soldado ya vivo y de pie) simplemente repite
        // valores que ya eran esos, sin costo.
        void OnEnable()
        {
            if (!bootstrapped || soldier == null || soldier.Health == null || !soldier.Health.IsAlive) return;

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
            soldier.SetBodyVisible(true);
            if (animator != null) animator.SetBool(SP.Presentation.SoldierAnimatorDriver.ParamMuerto, false);
        }

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            soldier = GetComponent<Soldier>();
            rend = GetComponentInChildren<Renderer>();
            animator = GetComponentInChildren<Animator>(true);
            // El color propio del soldado ya no vive en un material por
            // instancia (item 230: eso eran 50 materiales y cero batching).
            // Se lee del bloque de propiedades, que es donde lo dejo el
            // constructor de escena.
            baseColor = ReadTint(rend);
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
            if (soldier != null && soldier.Health != null) soldier.Health.Revivido += AlRevivir;
        }

        // Ronda 12: revivir (medico o [E]) solo reponia la vida: el cuerpo ya se habia ocultado 2 s despues de morir y el aliado
        // revivido quedaba invisible. Se deshace todo lo que dejo OnDeath y se corta la corutina que lo iba a ocultar.
        void AlRevivir()
        {
            if (!Application.isPlaying || soldier == null) return;
            StopAllCoroutines();
            transform.localScale = baseScale;
            WriteTint(rend, baseColor);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
            if (gameObject.activeInHierarchy && animator != null) animator.SetBool(SP.Presentation.SoldierAnimatorDriver.ParamMuerto, false);
            soldier.SetBodyVisible(true);
            if (animator == null) transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        void OnDestroy()
        {
            if (soldier != null && soldier.Health != null) soldier.Health.Revivido -= AlRevivir;
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
            if (soldier == null || soldier.Health == null || !soldier.Health.IsAlive) return;
            
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
            WriteTint(rend, baseColor);

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Marca de kill: solo enemigos, para no llenar de rayos la
            // pantalla cada vez que cae un aliado propio.
            if (soldier != null && soldier.Team == TeamId.Enemy)
            {
                KillCylinderFx.Spawn(transform.position);
                // Pedido explicito (ronda nueva): "revisa q al... matar
                // enemigos... tengan sistemas de particulas interesantes".
                // Estallido naranja/rojo a la altura del pecho -- lectura de
                // impacto/energia liberada, sin sangre (tono del proyecto).
                SparkleBurstFx.Spawn(transform.position + Vector3.up * 0.9f, new Color(0.95f, 0.4f, 0.15f), 0.7f, 3f, 30, 3.5f);
            }

            // Con arte real y Animator, la muerte es una animacion de
            // verdad (una de las 6 del pack, sorteada) y no el volteo de
            // 90 grados que fingia caerse un cubo. Los soldados sin
            // Animator (cubos de la suite headless, si queda alguno) siguen
            // con el volteo de siempre.
            if (animator != null) StartCoroutine(MorirAnimado());
            else StartCoroutine(FallOver());
        }

        // Pedido explicito: reproducir la animacion de morir y, pasados 2
        // segundos, desaparecer -- SetBodyVisible(false) y no
        // gameObject.SetActive(false), para no cortar de golpe ningun otro
        // componente (barra de vida, marcador de kill feed) que todavia
        // tenga una referencia viva a este soldado en el mismo frame.
        IEnumerator MorirAnimado()
        {
            animator.SetInteger(SP.Presentation.SoldierAnimatorDriver.ParamMuerteVariante,
                UnityEngine.Random.Range(0, SP.Presentation.SoldierAnimatorDriver.CantidadDeMuertes));
            animator.SetBool(SP.Presentation.SoldierAnimatorDriver.ParamMuerto, true);

            yield return new WaitForSeconds(SegundosHastaDesaparecer);
            if (soldier.Health != null && soldier.Health.IsAlive) yield break;
            soldier.SetBodyVisible(false);
        }


        // --- Tinte por instancia sin material por instancia (item 230) ---
        // Con un material COMPARTIDO por equipo, escribir sharedMaterial.color
        // pintaria a los 50 soldados iguales. MaterialPropertyBlock aplica el
        // color a ESTE renderer sin romper el batching.
        static MaterialPropertyBlock tintBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void WriteTint(Renderer r, Color c)
        {
            if (r == null) return;
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            r.GetPropertyBlock(tintBlock);
            // Las dos propiedades porque el shader puede ser URP (_BaseColor)
            // o el Standard de fallback (_Color).
            tintBlock.SetColor(BaseColorId, c);
            tintBlock.SetColor(ColorId, c);
            r.SetPropertyBlock(tintBlock);
        }

        public static Color ReadTint(Renderer r)
        {
            if (r == null) return Color.white;
            if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
            r.GetPropertyBlock(tintBlock);
            // Si nadie escribio el bloque todavia, el bloque devuelve negro
            // transparente: en ese caso vale el color del material.
            var c = tintBlock.GetColor(BaseColorId);
            if (c.a <= 0f) c = tintBlock.GetColor(ColorId);
            if (c.a <= 0f && r.sharedMaterial != null) c = r.sharedMaterial.color;
            return c;
        }

        IEnumerator FlashAndPunch()
        {
            var punchedColor = Color.white;
            WriteTint(rend, punchedColor);
            transform.localScale = baseScale * 1.15f;

            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(baseScale * 1.15f, baseScale, t / 0.15f);
                yield return null;
            }

            transform.localScale = baseScale;
            WriteTint(rend, baseColor);
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
