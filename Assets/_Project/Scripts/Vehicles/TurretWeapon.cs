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

        public float ExplosionRadius => explosionRadius;

        // Antes el cooldown de medio segundo era completamente invisible:
        // se apretaba y no pasaba nada, sin saber cuanto faltaba.
        public float CooldownFraction01 => fireCooldown <= 0f ? 1f : Mathf.Clamp01(1f - cooldownTimer / fireCooldown);

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
            if (cooldownTimer > 0f) cooldownTimer -= dt;

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
            pool.Spawn(spawnPos, transform.forward, shooterId, team, damage, projectileColor, explosionRadius);
            cooldownTimer = fireCooldown;

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
            return true;
        }
    }
}
