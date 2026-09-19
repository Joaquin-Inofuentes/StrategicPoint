using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;

namespace SP.Combat
{
    // Un solo evento para "este soldado tiene otra arma puesta", sin
    // importar el camino (recogida del piso, ciclado 1/2/3, IA). Antes solo
    // existia WeaponPickedUpEvent (especifico de WeaponPickup), asi que
    // cualquier UI que quisiera enterarse de un cambio de arma via evento
    // se perdia los cambios que no vinieran de recoger algo del piso -- ver
    // RosterRowView, que necesita el arma actual de CUALQUIER soldado.
    public readonly struct WeaponChangedEvent
    {
        public readonly int SoldierId;
        public readonly WeaponKind Kind;
        public WeaponChangedEvent(int soldierId, WeaponKind kind)
        {
            SoldierId = soldierId;
            Kind = kind;
        }
    }

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

        // Modelo real (mallas de Assets/ARTS/SP_Arte/_FBX_Export/05_Armas,
        // armadas en prefab por WeaponPrefabBuilder) colgado como hijo del
        // cubo de siempre. Pedido explicito: "quiero que los enemigos, los
        // aliados y el jugador usen las geometrias correctas -- usa sus
        // geometrias". El cubo NO desaparece: sigue siendo el pivote que
        // ArmaEnLaMano cuelga de la mano y del que Muzzle saca su posicion
        // (ver ApplyWeaponVisualModel) -- solo se apaga su Renderer.
        GameObject visualModelInstance;

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

        // G2: agachado dispara mas preciso. spreadDeg (la racha acumulada)
        // no cambia por agacharse -- lo que cambia es CUANTO de esa racha
        // se aplica al tiro. Separado de SpreadFraction01/SpreadDegEfectivo
        // de abajo para que un test pueda leer el numero real sin depender
        // del azar de ApplySpread (Random.Range).
        const float FactorAgachado = 0.4f;
        float MultiplicadorPostura => (owner != null && owner.Motor != null && owner.Motor.IsCrouching) ? FactorAgachado : 1f;

        // RoleType.Sniper estaba declarado (Soldado_2_Kes nace con este rol
        // en SC_Gameplay) sin ningun efecto de juego -- disparaba identico
        // a Assault. El alcance extendido vive en AiBrain.EffectiveAttackRange;
        // esto es la otra mitad, la precision: un francotirador ensancha su
        // cono de dispersion mucho mas lento que el resto.
        const float FactorSniper = 0.35f;
        float MultiplicadorRol => (owner != null && owner.Role == RoleType.Sniper) ? FactorSniper : 1f;
        public float SpreadDegEfectivo => spreadDeg * MultiplicadorPostura * MultiplicadorRol;
        public float SpreadFraction01 => Mathf.Clamp01(SpreadDegEfectivo / MaxSpreadDeg);

        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public WeaponKind CurrentWeaponKind { get; private set; } = WeaponKind.Rifle;

        // Lista pública de armas a distancia disponibles para este soldado,
        // en el orden en que [Rueda del mouse] las cicla. Pedido explícito:
        // antes 1/2/3 saltaban directo al catálogo entero (cualquier arma,
        // la tuvieras o no) -- esto es lo que de verdad "tiene" este
        // soldado. Pública y de instancia (no static) porque cada soldado
        // podría terminar con un loadout distinto (recoger/perder armas),
        // aunque hoy todos arrancan con el mismo trío.
        public readonly List<WeaponKind> Loadout = new List<WeaponKind> { WeaponKind.Rifle, WeaponKind.Pistol, WeaponKind.Heavy };
        public int CurrentLoadoutIndex { get; private set; }

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
            // BUG REAL: owner se resolvia UNA sola vez, adentro de la
            // guarda de bootstrapped. Si ese primer llamado ocurria antes
            // de que GetComponent<Soldier>() pudiera devolver algo (orden
            // de inicializacion raro), owner quedaba en null PARA SIEMPRE:
            // el "bootstrap defensivo" que EquipWeapon/TryFire/TryMelee
            // llaman con "if (owner == null) Bootstrap()" no hacia nada,
            // porque Bootstrap salia en la primera linea sin reintentar
            // GetComponent. Ahora se reintenta resolver owner en cada
            // llamada mientras siga null, bootstrapped o no.
            if (owner == null) owner = GetComponent<Soldier>();
            if (bootstrapped) return;
            bootstrapped = true;
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
            ApplyWeaponVisualModel(CurrentWeaponKind);
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
                // La escala a compensar es la del PADRE DEL ARMA, no la de
                // la raiz del soldado. Daban lo mismo mientras el arma
                // colgaba de la raiz; desde que SP.Presentation.ArmaEnLaMano
                // la cuelga del hueso de la mano, usar la de la raiz le
                // aplicaba dos veces la escala del rig y el rifle salia
                // deformado o enorme.
                var padre = WeaponVisualRenderer.transform.parent;
                var escalaPadre = padre != null ? padre.lossyScale : Vector3.one;
                // Un eje en cero convertiria la division en infinito y la
                // malla desapareceria sin ningun error en consola.
                if (Mathf.Abs(escalaPadre.x) < 0.0001f) escalaPadre.x = 1f;
                if (Mathf.Abs(escalaPadre.y) < 0.0001f) escalaPadre.y = 1f;
                if (Mathf.Abs(escalaPadre.z) < 0.0001f) escalaPadre.z = 1f;
                var wanted = WeaponCatalog.Get(kind).VisualScale;
                WeaponVisualRenderer.transform.localScale = new Vector3(wanted.x / escalaPadre.x, wanted.y / escalaPadre.y, wanted.z / escalaPadre.z);
            }

            ApplyWeaponVisualModel(kind);

            // BUG REAL: EquipFromLoadout actualiza CurrentLoadoutIndex,
            // pero EquipWeapon (el camino que usan WeaponPickup.EquipOn y
            // la IA/tests) nunca lo tocaba. Resultado: agarrar un arma del
            // piso cambiaba CurrentWeaponKind sin mover el indice, asi que
            // el proximo CycleNext/CyclePrevious (rueda del mouse) calculaba
            // el "siguiente" arma relativo a un indice viejo que ya no
            // coincidia con lo que el jugador tenia en la mano -- podia
            // re-equipar el arma que ya estaba usando o saltearse la que de
            // verdad seguia en el loadout.
            int loadoutIdx = Loadout.IndexOf(kind);
            if (loadoutIdx >= 0) CurrentLoadoutIndex = loadoutIdx;

            // Bootstrap defensivo (mismo patron que TryMelee/TryFire): un
            // enemigo de IA puede llamar EquipWeapon antes de que Awake
            // termine de correr Bootstrap en algun orden de inicializacion
            // raro, y sin owner no hay a quien avisarle este evento.
            if (owner == null) Bootstrap();
            if (owner != null) EventBus.Instance.Publish(new WeaponChangedEvent(owner.Id, kind));
        }

        // Cuelga la malla REAL (multi-parte: cañon, cuerpo, culata, etc.)
        // como hijo del cubo, y apaga el Renderer del cubo -- el cubo sigue
        // vivo como pivote (Muzzle y ArmaEnLaMano dependen de su transform),
        // solo deja de dibujarse. Si el prefab no esta (Resources.Load
        // devolvio null) no toca nada y se queda con el cubo de color de
        // siempre: mismo respaldo silencioso que ya usa ApplyWeaponVisualColor.
        void ApplyWeaponVisualModel(WeaponKind kind)
        {
            if (WeaponVisualRenderer == null) return;

            // BUG REAL (el "cubito" visible en juego): este return temprano
            // vivia DESPUES del if de abajo, asi que si WeaponModels.Get
            // fallaba (Resources.Load sin encontrar el prefab), el metodo
            // se iba antes de apagar el Renderer del cubo -- quedaba el
            // cubo de color plano flotando junto al arma real (o solo, si
            // el arma real nunca llego a cargar). El cubo es un pivote
            // puro (Muzzle y ArmaEnLaMano cuelgan de el): nunca debe
            // dibujarse, haya o no malla real disponible.
            WeaponVisualRenderer.enabled = false;

            var prefab = WeaponModels.Get(kind);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponHolder] No se encontro el modelo real de {kind}: el arma se queda sin malla visible (el cubo permanece oculto a proposito).");
                return;
            }

            // BUG REAL: Destroy() no hace nada util fuera de Play mode --
            // Unity tira "Destroy may not be called from edit mode" y NO
            // destruye el objeto (queda huerfano colgando del cubo para
            // siempre). La suite headless corre sus fases en Edit mode
            // (HeadlessTestRunner prueba equipar las 3 armas una tras otra
            // sobre el mismo soldado), asi que cada cambio de arma dejaba
            // un "RealModel" viejo acumulado en vez de reemplazarlo.
            if (visualModelInstance != null)
            {
                if (Application.isPlaying) Destroy(visualModelInstance);
                else DestroyImmediate(visualModelInstance);
            }

            visualModelInstance = Instantiate(prefab, WeaponVisualRenderer.transform);
            visualModelInstance.name = "RealModel";
            visualModelInstance.transform.localPosition = Vector3.zero;
            visualModelInstance.transform.localRotation = Quaternion.identity;

            // El cubo (padre) puede llevar una escala no uniforme -- el
            // VisualScale de mas arriba, pensado para que un cubo sin
            // textura se distinga por forma -- que deformaria el modelo
            // real si se le dejara heredarla. Se cancela con la escala
            // MUNDO efectiva del cubo (incluye la compensacion del hueso
            // de la mano, no solo el VisualScale), asi que el arma real
            // siempre sale a su tamaño natural sin importar que escala
            // tenga el cubo en ese momento.
            var padreEscala = WeaponVisualRenderer.transform.lossyScale;
            float cancelaX = Mathf.Abs(padreEscala.x) > 0.0001f ? 1f / padreEscala.x : 1f;
            float cancelaY = Mathf.Abs(padreEscala.y) > 0.0001f ? 1f / padreEscala.y : 1f;
            float cancelaZ = Mathf.Abs(padreEscala.z) > 0.0001f ? 1f / padreEscala.z : 1f;

            // Pedido explicito: "las armas sean representadas con prismas y
            // siempre siempre el arma debe entrar en ese prisma" -- cada
            // WeaponKind tiene ahora una caja limite (WeaponModels.Prisma,
            // ancho x alto x largo a escala 1) y esto mide el modelo real
            // (WeaponModels.MeasuredNaturalSize, medido una sola vez y
            // cacheado) contra esa caja. Si al escalarse a tamaño natural
            // se pasa de la caja en cualquier eje, se lo achica de forma
            // UNIFORME (un solo factor para los tres ejes, nunca por eje
            // separado) lo suficiente para entrar entero -- uniforme para
            // no deformar el modelo, y solo hacia abajo (nunca agranda un
            // arma que ya entraba de sobra).
            float factorPrisma = 1f;
            var medido = WeaponModels.MeasuredNaturalSize(kind);
            var prisma = WeaponModels.Prisma(kind);
            if (medido.x > 0.0001f) factorPrisma = Mathf.Min(factorPrisma, prisma.x / medido.x);
            if (medido.y > 0.0001f) factorPrisma = Mathf.Min(factorPrisma, prisma.y / medido.y);
            if (medido.z > 0.0001f) factorPrisma = Mathf.Min(factorPrisma, prisma.z / medido.z);

            visualModelInstance.transform.localScale = new Vector3(
                cancelaX * factorPrisma, cancelaY * factorPrisma, cancelaZ * factorPrisma);

            // El modelo real ya trae su propio material (el trimsheet
            // compartido de WeaponPrefabBuilder): antes se lo pisaba aca con
            // un SafeMaterial.Create(tint) por renderer -- un Material
            // CLONADO por cada arma de cada soldado, ninguno compartible
            // entre si (rompe SRP/static batching) y encima tapaba la
            // textura real con un color solido. Pedido explicito: usar el
            // material nuevo del trimsheet. La forma de cada arma (rifle
            // flaco y largo, pistola chica, pesada gruesa) ya distingue cual
            // esta equipada; no hace falta ademas un tinte por tipo.
            foreach (var r in visualModelInstance.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Cada arma tiene un largo real distinto (la pistola cabe casi
            // sobre el puño, el rifle y la pesada necesitan adelantarse
            // mas o la culata queda enterrada en el torso) -- ver
            // ArmaEnLaMano.Reposicionar. No-op silencioso si el arma
            // todavia no se colgo de la mano (soldado sin Animator humano,
            // como los cubos de la suite headless) o si este soldado no
            // tiene ArmaEnLaMano (nunca deberia faltar, es RequireComponent,
            // pero GetComponent nunca revienta si falta).
            GetComponent<SP.Presentation.ArmaEnLaMano>()?.Reposicionar(kind);
        }

        // Cambia al arma en la posición `index` del Loadout público y la
        // deja como CurrentWeaponKind, leyendo sus stats del catálogo --
        // mismo camino que EquipWeapon (recogida del piso), para que no
        // existan dos formas distintas de "tener puesta" un arma.
        public void EquipFromLoadout(int index)
        {
            if (Loadout.Count == 0) return;
            index = ((index % Loadout.Count) + Loadout.Count) % Loadout.Count;
            CurrentLoadoutIndex = index;
            var kind = Loadout[index];
            var spec = WeaponCatalog.Get(kind);
            EquipWeapon(kind, spec.Damage, spec.Cooldown, spec.Color);
        }

        // Cinta de balas de una ametralladora fija: cargador y recarga propios, sin tocar el catalogo.
        public void ConfigurarCargador(int tamano, float segundosDeRecarga)
        {
            magazineSize = Mathf.Max(1, tamano);
            reloadDuration = segundosDeRecarga;
            CurrentAmmo = magazineSize;
            IsReloading = false;
            reloadTimer = 0f;
        }

        public void CycleNext() => EquipFromLoadout(CurrentLoadoutIndex + 1);
        public void CyclePrevious() => EquipFromLoadout(CurrentLoadoutIndex - 1);

        // --------------------------------------------------------------
        // Cuchillo: golpe rápido cuerpo a cuerpo. Pedido explícito ([V] =
        // "ataque de cuchillo rapido"). Independiente del arma a distancia
        // equipada -- no la reemplaza ni la toca -- con su propio
        // enfriamiento corto, sin munición ni recarga. No usa
        // WeaponCatalog: no es un arma del loadout, es una acción aparte
        // siempre disponible.
        // --------------------------------------------------------------
        const float KnifeRange = 2.2f;
        const float KnifeArcDeg = 70f;
        const int KnifeDamage = 55;
        const float KnifeCooldown = 0.55f;
        float knifeCooldownTimer;
        public float KnifeCooldownRemaining => Mathf.Max(0f, knifeCooldownTimer);

        // Busca el enemigo vivo más cercano dentro del arco/alcance del
        // cuchillo. ActorRegistry.All (mismo patrón que NearbySquadListView)
        // en vez de SpatialGrid: el cuchillo es de uso ocasional, no un
        // disparo por frame -- no justifica la complejidad de la grilla que
        // sí paga Projectile por volumen.
        public bool TryMelee()
        {
            if (owner == null) Bootstrap();
            if (owner == null || knifeCooldownTimer > 0f) return false;
            knifeCooldownTimer = KnifeCooldown;

            Soldier best = null;
            float bestDist = KnifeRange;
            foreach (var s in ActorRegistry.All)
            {
                // BUG REAL: faltaba el mismo chequeo que ya tienen
                // Projectile.BuscarBlancoEnElTramo y Projectile.ExplodeAt --
                // un soldado montado en un vehiculo queda inactivo (oculto)
                // y no deberia poder recibir impactos desde afuera. Sin
                // esto, el cuchillo SI podia "apuñalar" a traves del casco
                // a alguien escondido adentro, inconsistente con las balas
                // y las explosiones.
                if (s == null || s == owner || s.Team == owner.Team || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                var to = s.transform.position - owner.transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > KnifeRange) continue;
                if (dist > 0.05f && Vector3.Angle(owner.transform.forward, to) > KnifeArcDeg) continue;
                if (dist < bestDist) { bestDist = dist; best = s; }
            }

            if (best != null) best.Health.TakeDamage(KnifeDamage, owner.Id);
            EventBus.Instance.Publish(new MeleeAttackEvent(owner.Id, best != null));
            return true;
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
            if (knifeCooldownTimer > 0f) knifeCooldownTimer -= dt;
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
            var spreadDir = ApplySpread(direction, SpreadDegEfectivo);
            spreadDeg = Mathf.Min(MaxSpreadDeg, spreadDeg + SpreadGrowthPerShot);

            var spawnPos = Muzzle != null ? Muzzle.position : origin;
            var espec = WeaponCatalog.Get(CurrentWeaponKind);
            if (espec.Pellets > 1)
            {
                // Escopeta: un cono de perdigones por disparo.
                for (int p = 0; p < espec.Pellets; p++)
                    pool.Spawn(spawnPos, ApplySpread(spreadDir, espec.PelletSpreadDeg), owner.Id, owner.Team, damage, projectileColor);
            }
            else if (espec.ExplosionRadius > 0f)
            {
                // Lanzacohetes: proyectil lento con explosion de area.
                pool.Spawn(spawnPos, spreadDir, owner.Id, owner.Team, damage, projectileColor, espec.ExplosionRadius, espec.ProjectileGravity, null, espec.ProjectileSpeed);
            }
            else
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

        // Prueba visual del prisma (WeaponModels.Prisma): dibuja la caja
        // limite del arma equipada alrededor de su pivote. OnDrawGizmos es
        // exclusivo de la Scene View -- Unity nunca lo llama en Game View
        // ni en un build -- asi que esto no aparece en gameplay, solo
        // sirve para confirmar a ojo en el editor que el modelo real
        // siempre entra adentro.
        void OnDrawGizmos()
        {
            if (WeaponVisualRenderer == null) return;
            var prisma = WeaponModels.Prisma(CurrentWeaponKind);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.matrix = WeaponVisualRenderer.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, prisma);
        }
    }
}
