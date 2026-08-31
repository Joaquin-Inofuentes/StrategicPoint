using UnityEngine;
using SP.Core;
using SP.Combat;

namespace SP.Vehicles
{
    // Arma montada en el vehículo: gira sola (no con el chasis) y dispara
    // proyectiles del mismo pool que las armas de mano.
    public class TurretWeapon : MonoBehaviour
    {
        [SerializeField] float fireCooldown = 0.5f;
        [SerializeField] int damage = 45;
        [SerializeField] Color projectileColor = new Color(0.9f, 0.35f, 0.1f);
        [SerializeField] ProjectilePool pool;
        // Radio de la granada: 0 desactivaría la explosión: siempre tiene
        // zona de daño, pedido explícito ("q el proyectil tenga zona de
        // explosion").
        [SerializeField] float explosionRadius = 3f;
        // Grados por segundo: "que rote lento, que demore en tener la
        // mira en el cursor" -- antes giraba lo que el mouse moviera, ya
        // (instantáneo). Ahora persigue un ángulo objetivo con velocidad
        // limitada, así se nota el retraso.
        [SerializeField] float turnSpeedDegPerSec = 50f;
        // El giro bajo control del jugador es mas rapido que el de la IA:
        // la IA "lidera" un blanco que se mueve solo, el jugador esta
        // apuntando activamente y un giro de 50 deg/s se siente roto.
        // Sigue siendo limitado: el peso del cañon es parte del diseño
        // ("que rote lento, que demore en tener la mira en el cursor").
        [SerializeField] float playerTurnSpeedDegPerSec = 110f;
        public Transform Muzzle;

        float cooldownTimer;
        Vehicle vehicle;
        bool bootstrapped;

        Transform barrel;
        Vector3 barrelRestLocalPos;
        float barrelRecoil;
        const float RecoilDistance = 0.45f;
        const float RecoilRecoverPerSec = 2.2f;
        static readonly Color MuzzleFlashColor = new Color(1f, 0.75f, 0.35f);

        // Un solo tipo de proyectil significa que no hay ninguna decision
        // antes de disparar. Dos crean una eleccion tactica constante:
        // area contra grupos, perforante contra un blanco duro.
        public enum AmmoType { Explosive, ArmorPiercing }
        public AmmoType Ammo { get; private set; } = AmmoType.Explosive;
        public void CycleAmmo() => Ammo = Ammo == AmmoType.Explosive ? AmmoType.ArmorPiercing : AmmoType.Explosive;

        public float ExplosionRadius => Ammo == AmmoType.Explosive ? explosionRadius : 0f;
        public int CurrentDamage => Ammo == AmmoType.Explosive ? damage : Mathf.RoundToInt(damage * 1.8f);
        public Color CurrentProjectileColor => Ammo == AmmoType.Explosive ? projectileColor : new Color(0.55f, 0.85f, 1f);

        // Caida del proyectil de tanque. El de arma de mano sigue recto:
        // vuela tan poco tiempo que un arco no aportaria nada y volveria
        // impredecible el tiro a quemarropa.
        [SerializeField] float projectileGravity = 9.8f;
        public float ProjectileGravity => Ammo == AmmoType.Explosive ? projectileGravity : projectileGravity * 0.45f;

        // El cooldown fijo permitia disparar indefinidamente al mismo
        // ritmo, o sea ninguna decision sobre CUANDO disparar. El calor
        // sube por disparo y baja con el tiempo, y estira el cooldown.
        public float Heat { get; private set; }
        const float HeatPerShot = 0.34f;
        const float HeatCoolPerSec = 0.22f;
        public float EffectiveCooldown => fireCooldown * (1f + Heat * 1.6f);

        // Antes el cooldown de medio segundo era completamente invisible:
        // se apretaba y no pasaba nada, sin saber cuanto faltaba.
        public float CooldownFraction01 => EffectiveCooldown <= 0f ? 1f : Mathf.Clamp01(1f - cooldownTimer / EffectiveCooldown);

        // Angulo al que el jugador quiere apuntar, separado de donde esta
        // realmente el cañon: es la brecha entre los dos la que da sentido
        // al reticulo de torreta (llegue / todavia girando).
        public float DesiredYaw { get; private set; }
        bool desiredYawInit;

        public float YawGapDeg => Mathf.DeltaAngle(transform.eulerAngles.y, DesiredYaw);
        public bool IsOnTarget(float toleranceDeg = 4f) => Mathf.Abs(YawGapDeg) <= toleranceDeg;

        void Awake() => Bootstrap();

        public void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;
            vehicle = GetComponentInParent<Vehicle>();
            barrel = transform.Find("TurretBarrel");
            if (barrel != null) barrelRestLocalPos = barrel.localPosition;
            WorldSystemsRegistry.Register(this);
        }

        void EnsureDesiredYaw()
        {
            if (desiredYawInit) return;
            desiredYawInit = true;
            DesiredYaw = transform.eulerAngles.y;
        }

        public void AddDesiredYaw(float delta)
        {
            EnsureDesiredYaw();
            DesiredYaw += delta;
        }

        // Giro bajo control del jugador: el cañon persigue DesiredYaw a
        // velocidad limitada, igual que AimAt hace con el blanco de la IA.
        public void TickPlayerAim(float dt)
        {
            if (!bootstrapped) Bootstrap();
            EnsureDesiredYaw();
            if (vehicle != null && vehicle.IsDestroyed) return;
            float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, DesiredYaw, playerTurnSpeedDegPerSec * dt);
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        }

        void OnDestroy() => WorldSystemsRegistry.Unregister(this);

        public void SetPool(ProjectilePool projectilePool) => pool = projectilePool;

        public void RotateYaw(float yawDelta) => transform.Rotate(Vector3.up, yawDelta, Space.World);

        // Apunta hacia un punto del mundo con velocidad de giro limitada
        // (turnSpeedDegPerSec): el cañón persigue el ángulo objetivo en
        // vez de saltar directo a él, para que se note que "le cuesta"
        // seguir el blanco -- usado por el auto-apuntado en batalla.
        public void AimAt(Vector3 worldPoint, float dt)
        {
            if (!bootstrapped) Bootstrap();
            if (vehicle != null && vehicle.IsDestroyed) return;
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;

            float desiredYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            float currentYaw = transform.eulerAngles.y;
            float newYaw = Mathf.MoveTowardsAngle(currentYaw, desiredYaw, turnSpeedDegPerSec * dt);
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

            // Mantiene sincronizado el angulo deseado del jugador con el
            // que persigue la IA: si no, al bajarse el artillero humano y
            // volver a subir, el cañon pegaria un salto de vuelta al
            // ultimo angulo que el jugador habia pedido hace rato.
            desiredYawInit = true;
            DesiredYaw = desiredYaw;
        }

        // Qué tan cerca está el cañón de apuntar realmente a ese punto --
        // para no disparar mientras todavía está girando hacia el blanco.
        public bool IsAimedAt(Vector3 worldPoint, float toleranceDeg = 4f)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            float desiredYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            return Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw)) <= toleranceDeg;
        }

        public void Tick(float dt)
        {
            if (!bootstrapped) Bootstrap();
            bool wasReloading = cooldownTimer > 0f;
            if (cooldownTimer > 0f) cooldownTimer -= dt;
            // El fin del cooldown era invisible y mudo: habia que mirar el
            // HUD justo cuando hay que mirar el campo. Suena al
            // COMPLETARSE, no al iniciarse.
            if (wasReloading && cooldownTimer <= 0f) PlayAt(SP.Presentation.SfxKind.TurretReloaded, 0.45f);

            Heat = Mathf.Max(0f, Heat - HeatCoolPerSec * dt);
            ApplyHeatColor();
            RecoverChassisShake(dt);

            // El disparo mas potente del juego no movia nada: el cañon
            // quedaba estatico, con menos presencia que un rifle. Se
            // hunde de golpe al disparar y vuelve por lerp.
            if (barrel != null && barrelRecoil > 0f)
            {
                barrelRecoil = Mathf.MoveTowards(barrelRecoil, 0f, RecoilRecoverPerSec * dt);
                barrel.localPosition = barrelRestLocalPos - Vector3.forward * barrelRecoil;
            }
        }

        public bool TryFire()
        {
            if (!bootstrapped) Bootstrap();
            // Un tanque destruido no debería poder seguir disparando --
            // ojo que Tick()/TryFire() se llaman por método directo desde
            // WorldSimulationDriver, no por el Update() automático de
            // Unity, así que "enabled=false" solo no alcanza para
            // frenarlo: hay que chequear el estado real acá.
            if (vehicle != null && vehicle.IsDestroyed) return false;
            if (cooldownTimer > 0f || pool == null) return false;

            int shooterId = vehicle != null && vehicle.Gunner != null ? vehicle.Gunner.Id : -1;
            var team = vehicle != null && vehicle.Gunner != null ? vehicle.Gunner.Team : TeamId.Player;

            var spawnPos = Muzzle != null ? Muzzle.position : transform.position;
            pool.Spawn(spawnPos, transform.forward, shooterId, team, CurrentDamage,
                CurrentProjectileColor, ExplosionRadius, ProjectileGravity, vehicle);
            cooldownTimer = EffectiveCooldown;
            Heat = Mathf.Clamp01(Heat + HeatPerShot);

            // Se hunde YA, no en el proximo Tick: si se deja para el Tick
            // siguiente, ese mismo Tick tambien le descuenta la
            // recuperacion y el cañon nunca llega a mostrar el hundimiento
            // completo -- se veia la mitad del retroceso, un frame tarde.
            barrelRecoil = RecoilDistance;
            if (barrel != null) barrel.localPosition = barrelRestLocalPos - Vector3.forward * barrelRecoil;
            // Fogonazo de boca: bastante mas grande que el de un arma de
            // mano (SP.Presentation.CubeFxReactor usa 0.22), acorde al
            // calibre.
            SP.Presentation.ImpactFx.Spawn(spawnPos, MuzzleFlashColor, 0.7f, 0.12f);
            // Luz real de un par de frames: un destello plano no ilumina
            // nada, y con poca luz ambiente la diferencia de potencia
            // percibida es enorme.
            SP.Presentation.MuzzleLightPool.Flash(spawnPos, MuzzleFlashColor);

            // Dos capas: cuerpo grave (el peso) y crack agudo (el golpe).
            // Un tono unico no suena a cañon por mas fuerte que sea.
            PlayAt(SP.Presentation.SfxKind.CannonBody, 0.9f);
            PlayAt(SP.Presentation.SfxKind.CannonCrack, 0.55f);

            SpawnMuzzleDust(spawnPos);
            KickChassis();

            // El culatazo empuja la camara HACIA ATRAS del eje de disparo:
            // una vibracion sin direccion no se lee como retroceso.
            var rig = Object.FindAnyObjectByType<SP.CameraSystem.CameraRig>();
            if (rig != null) rig.KickDirectional(-transform.forward, 0.35f);

            return true;
        }

        static void PlayAt(SP.Presentation.SfxKind kind, float volume)
        {
            // PlayClipAtPoint crea un objeto que se autodestruye con
            // Destroy(), ilegal fuera de Play mode -- y la suite headless
            // corre las fases en Edit mode.
            if (!Application.isPlaying) return;
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(SP.Presentation.GenericSfx.Get(kind),
                cam != null ? cam.transform.position : Vector3.zero, volume);
        }

        // Solo cuando la boca esta cerca del suelo: disparar con el cañon
        // levantado no deberia levantar polvo de la nada.
        const float MuzzleDustHeight = 2.2f;
        public bool MuzzleIsNearGround => (Muzzle != null ? Muzzle.position.y : transform.position.y) <= MuzzleDustHeight;

        void SpawnMuzzleDust(Vector3 spawnPos)
        {
            if (!MuzzleIsNearGround) return;
            var dustColor = new Color(0.68f, 0.62f, 0.5f);
            for (int i = 0; i < 5; i++)
            {
                // Cono alrededor del eje del cañon, no una esfera: el
                // polvo lo empuja el fogonazo hacia adelante.
                var dir = Vector3.Slerp(transform.forward, Random.onUnitSphere, 0.4f).normalized;
                SP.Presentation.DebrisPool.Spawn(spawnPos, dir * Random.Range(2f, 5f), dustColor, Random.Range(0.3f, 0.55f), 0.9f);
            }
        }

        // Solo se sacudia la camara, asi que desde afuera (vista RTS) un
        // tanque disparando era indistinguible de uno quieto.
        Vector3 chassisKick;
        const float ChassisKickDistance = 0.22f;
        const float ChassisRecoverPerSec = 1.4f;

        void KickChassis()
        {
            if (vehicle == null) return;
            chassisKick = -transform.forward * ChassisKickDistance;
            vehicle.transform.position += chassisKick;
        }

        void RecoverChassisShake(float dt)
        {
            if (vehicle == null || chassisKick.sqrMagnitude < 0.000001f) return;
            var step = Vector3.MoveTowards(chassisKick, Vector3.zero, ChassisRecoverPerSec * dt);
            vehicle.transform.position -= (chassisKick - step);
            chassisKick = step;
        }

        // El metal se pone al rojo con el calor acumulado: el estado de
        // sobrecalentamiento tiene que verse en el mundo, no solo en un
        // numero del HUD.
        Color barrelBaseColor;
        bool barrelColorCached;

        void ApplyHeatColor()
        {
            if (barrel == null) return;
            var rend = barrel.GetComponent<MeshRenderer>();
            if (rend == null) return;
            if (!barrelColorCached) { barrelColorCached = true; barrelBaseColor = rend.sharedMaterial.color; }
            rend.sharedMaterial.color = Color.Lerp(barrelBaseColor, new Color(1f, 0.25f, 0.1f), Heat);
        }
    }
}
