using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Combat
{
    // Arma en mano de un soldado: pide proyectiles al pool, nunca instancia.
    // Lee dueño y equipo de su propio Soldier — nunca los cachea por su cuenta,
    // así sobrevive a un domain reload sin que nadie tenga que recablearlo.
    public class WeaponHolder : MonoBehaviour, IWeapon
    {
        [SerializeField] float fireCooldown = 0.35f;
        [SerializeField] int damage = 34;
        [SerializeField] ProjectilePool pool;

        public Transform Muzzle;

        // Cubo chico pegado a la mano/arma del soldado: se ve tanto en FPS
        // (el jugador lo ve colgando adelante suyo) como en RTS (parte del
        // cuerpo), y se tiñe del color del arma equipada — así se nota a
        // simple vista con qué arma anda cada uno, sin abrir ningún menú.
        public Renderer WeaponVisualRenderer;

        Soldier owner;
        float cooldownTimer;
        bool bootstrapped;
        Color projectileColor = new Color(1f, 0.92f, 0.35f);

        int magazineSize = 8;
        float reloadDuration = 1.5f;
        float reloadTimer;

        // Dispersion real: antes la mirilla no comunicaba nada de la
        // precision real del arma, y el proyectil siempre salia
        // perfectamente derecho sin importar cuanto se disparara
        // seguido. Ahora cada tiro ensancha el cono de dispersion (mas
        // dificil acertar en rafaga sostenida) y decae solo al dejar de
        // disparar -- la mirilla en pantalla refleja este mismo valor,
        // no es un efecto puramente cosmetico separado de la puntería
        // real.
        float spreadDeg;
        const float MaxSpreadDeg = 6f;
        const float SpreadGrowthPerShot = 1.6f;
        // OJO: con un fireCooldown tipico de 0.3-0.35s entre disparos, un
        // decay de 10 grados/seg (probado primero) borraba 3-3.5 grados
        // entre CADA tiro -- mas de lo que un solo disparo hace crecer
        // (1.6), asi que la dispersion nunca llegaba a acumularse en
        // cadencia real, solo en pruebas con huecos artificialmente
        // largos entre tiros. Bajado a 3, para que la rafaga sostenida
        // realmente ensanche el cono y solo se recupere al soltar el
        // gatillo por un rato.
        const float SpreadDecayPerSec = 3f;
        public float SpreadFraction01 => Mathf.Clamp01(spreadDeg / MaxSpreadDeg);

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public WeaponKind CurrentWeaponKind { get; private set; } = WeaponKind.Rifle;

        // Para la barra de recarga/enfriamiento en la UI: 0 = recién
        // disparada (o recargando), 1 = lista para disparar de nuevo.
        public float ReadinessFraction01 => IsReloading
            ? 1f - Mathf.Clamp01(reloadTimer / reloadDuration)
            : (fireCooldown > 0f ? 1f - Mathf.Clamp01(cooldownTimer / fireCooldown) : 1f);

        public int CurrentAmmo { get; private set; } = 8;
        public int MagazineSize => magazineSize;
        public bool IsReloading { get; private set; }

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            owner = GetComponent<Soldier>();
            CurrentAmmo = magazineSize;

            // BUG REAL: el cubo del arma nace con un Material creado en
            // tiempo de Editor y guardado adentro del prefab (mismo caso
            // documentado en Projectile.cs) -- esa referencia no sobrevive
            // instanciar el prefab, y el Renderer queda con sharedMaterial
            // null hasta que alguien llame EquipWeapon(). Como eso solo
            // pasa al recoger un arma del piso o con las teclas 1/2/3 del
            // jugador, CUALQUIER soldado de IA que nunca cambia de arma
            // (o sea, casi todos) se quedaba con el cubo del arma en el
            // magenta de "sin material" toda la partida. Se autocura acá
            // con el color de catálogo del arma con la que ya arranca.
            ApplyWeaponVisualColor(WeaponCatalog.Get(CurrentWeaponKind).Color);
        }

        public void SetPool(ProjectilePool projectilePool) => pool = projectilePool;

        public void SetTuning(int weaponDamage, float cooldown)
        {
            damage = weaponDamage;
            fireCooldown = cooldown;
        }

        // Cambiar de arma (recogida en el mundo) es cambiar estos tres
        // números y el color de lo que dispara. Nada más.
        public void EquipWeapon(WeaponKind kind, int weaponDamage, float cooldown, Color color)
        {
            CurrentWeaponKind = kind;
            damage = weaponDamage;
            fireCooldown = cooldown;
            projectileColor = color;
            // Cambiar de arma no debería dejarte esperando el enfriamiento
            // del arma anterior: se puede disparar de una con la nueva.
            cooldownTimer = 0f;

            var catalogSpec = WeaponCatalog.Get(kind);
            magazineSize = catalogSpec.MagazineSize;
            reloadDuration = catalogSpec.ReloadDuration;
            CurrentAmmo = magazineSize;
            IsReloading = false;
            reloadTimer = 0f;

            ApplyWeaponVisualColor(color);
            // Cada arma tiene su propia forma (chica/larga/gruesa), no solo
            // color: así se distingue de un vistazo cuál está equipada. El
            // cuerpo del soldado tiene escala no uniforme (0.9/1.6/0.9): hay
            // que compensarla para que el cubo del arma no salga deformado.
            if (WeaponVisualRenderer != null)
            {
                var rootScale = transform.lossyScale;
                var wanted = WeaponCatalog.Get(kind).VisualScale;
                WeaponVisualRenderer.transform.localScale = new Vector3(wanted.x / rootScale.x, wanted.y / rootScale.y, wanted.z / rootScale.z);
            }
        }

        // Igual que con el material de Projectile: un Material creado en
        // runtime y guardado dentro de un prefab (PrefabUtility.SaveAsPrefabAsset)
        // puede quedar null en la instancia — se recrea sola si hace falta.
        void ApplyWeaponVisualColor(Color color)
        {
            if (WeaponVisualRenderer == null) return;
            if (WeaponVisualRenderer.sharedMaterial == null)
                WeaponVisualRenderer.sharedMaterial = SP.Presentation.SafeMaterial.Create(Color.white);
            WeaponVisualRenderer.sharedMaterial.color = color;
        }

        public void Tick(float dt)
        {
            spreadDeg = Mathf.MoveTowards(spreadDeg, 0f, SpreadDecayPerSec * dt);

            // El enfriamiento corre SIEMPRE, tambien durante la recarga.
            // Antes esto estaba despues del return de abajo, asi que el
            // cooldown quedaba congelado mientras se recargaba: al
            // terminar la recarga todavia faltaba esperar el enfriamiento
            // del ultimo tiro. MEDIDO: una recarga de 1,50 s tardaba en
            // realidad 1,80 s hasta poder volver a disparar (18 ticks
            // extra). El arma mentia sobre su propia estadistica en un
            // 20%, y de paso hacia que recargar a proposito (Reload())
            // costara mas de lo que dice la UI.
            if (cooldownTimer > 0f) cooldownTimer -= dt;

            if (IsReloading)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0f)
                {
                    IsReloading = false;
                    CurrentAmmo = magazineSize;
                }
                return;
            }
        }

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            if (owner == null) Bootstrap();
            if (IsReloading || cooldownTimer > 0f || pool == null || owner == null) return false;

            if (CurrentAmmo <= 0)
            {
                StartReload();
                return false;
            }

            // El desvio se calcula con la dispersion ANTES de este tiro
            // (el patron acumulado hasta ahora), y recien despues crece
            // para el proximo -- si no, hasta el primer disparo de una
            // rafaga saldria desviado por su propio impacto.
            var spreadDir = ApplySpread(direction, spreadDeg);
            spreadDeg = Mathf.Min(MaxSpreadDeg, spreadDeg + SpreadGrowthPerShot);

            var spawnPos = Muzzle != null ? Muzzle.position : origin;
            pool.Spawn(spawnPos, spreadDir, owner.Id, owner.Team, damage, projectileColor);
            cooldownTimer = fireCooldown;
            CurrentAmmo--;
            if (CurrentAmmo <= 0) StartReload();
            EventBus.Instance.Publish(new ShotFiredEvent(owner.Id));
            return true;
        }

        static Vector3 ApplySpread(Vector3 direction, float maxDeg)
        {
            if (maxDeg <= 0f) return direction;
            if (direction.sqrMagnitude < 0.000001f) return direction;

            // BUG REAL: esto giraba sobre Vector3.up y Vector3.right, que
            // son ejes del MUNDO, mientras el comentario decia -- y el
            // juego necesitaba -- ejes perpendiculares AL DISPARO. La
            // diferencia se nota segun hacia donde mire el soldado:
            // disparando hacia el norte los dos ejes desviaban de verdad,
            // pero disparando hacia el este el giro sobre Vector3.right
            // era un ROLL sobre el propio eje de tiro, que no mueve la
            // bala ni un grado. La dispersion valia la mitad en unas
            // direcciones y entera en otras, y el arma se sentia mas
            // precisa mirando para un lado que para el otro.
            //
            // Ahora los ejes se construyen contra la direccion real.
            var adelante = direction.normalized;
            var derecha = Vector3.Cross(Vector3.up, adelante);
            // Disparo casi vertical: 'up' y la direccion son paralelos y el
            // producto cruz se va a cero. Cualquier perpendicular sirve.
            if (derecha.sqrMagnitude < 0.000001f) derecha = Vector3.Cross(Vector3.forward, adelante);
            derecha.Normalize();
            var arriba = Vector3.Cross(adelante, derecha);

            float yaw = UnityEngine.Random.Range(-maxDeg, maxDeg);
            float pitch = UnityEngine.Random.Range(-maxDeg, maxDeg);
            var rot = Quaternion.AngleAxis(yaw, arriba) * Quaternion.AngleAxis(pitch, derecha);
            return rot * adelante;
        }

        void StartReload()
        {
            IsReloading = true;
            reloadTimer = reloadDuration;
        }

        // Antes solo se recargaba solo al vaciar el cargador del todo. No
        // habia forma de rellenar un cargador a medias antes de entrar en
        // combate, que es una decision tactica basica en cualquier shooter.
        public bool Reload()
        {
            if (IsReloading || CurrentAmmo >= magazineSize) return false;
            StartReload();
            return true;
        }
    }
}
