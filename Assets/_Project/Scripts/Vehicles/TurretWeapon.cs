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
        // BUG REAL: el cañon solo giraba en yaw (eje Y) -- el mouse.delta.y
        // (arriba/abajo) se leia y se tiraba, asi que un tanque nunca podia
        // apuntar a nada mas alto o mas bajo que su propia altura. Limites
        // asimetricos a proposito: un cañon de tanque real baja poco (el
        // propio chasis lo tapa) pero sube bastante mas para poder pegarle
        // a algo en una loma o un techo.
        [SerializeField] float minPitchDeg = -8f;
        [SerializeField] float maxPitchDeg = 35f;
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

        // La bala de mano paso de 40 a 160 m/s. El obus del tanque NO
        // tiene que acelerarse con ella: es un tiro con arco y gravedad, y
        // cuadruplicarlo aplanaria la parabola hasta volverla otra arma.
        // Con base 160 y multiplicador 0,5 la velocidad efectiva del obus
        // sigue siendo 80 m/s, exactamente la de antes (40 x 2).
        public const float SpeedMultiplier = 0.5f;

        // Antes esto era un 40 escrito a mano con un comentario pidiendo
        // que se actualizara junto con el prefab. Ahora se lee de la unica
        // fuente: si la velocidad base cambia, la prediccion de impacto y
        // el liderado de blanco la siguen solos.
        public const float ProjectileSpeed = SP.Combat.Projectile.VelocidadBase;

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

        // Mismo concepto que DesiredYaw pero en elevacion. Separado porque
        // el jugador los mueve con ejes de mouse distintos (x/y) y cada uno
        // persigue su angulo objetivo de forma independiente.
        public float DesiredPitch { get; private set; }
        bool desiredPitchInit;

        public float YawGapDeg => Mathf.DeltaAngle(transform.eulerAngles.y, DesiredYaw);
        public bool IsOnTarget(float toleranceDeg = 4f) => Mathf.Abs(YawGapDeg) <= toleranceDeg;

        // eulerAngles.x devuelve 0..360; para un pitch chico negativo (p.ej.
        // -5) eso se lee como 355, y Clamp/MoveTowardsAngle contra un rango
        // como (-8, 35) se rompe sin este pasaje a -180..180.
        static float NormalizePitch(float x) => x > 180f ? x - 360f : x;

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

        void EnsureDesiredPitch()
        {
            if (desiredPitchInit) return;
            desiredPitchInit = true;
            DesiredPitch = NormalizePitch(transform.eulerAngles.x);
        }

        public void AddDesiredPitch(float delta)
        {
            EnsureDesiredPitch();
            DesiredPitch = Mathf.Clamp(DesiredPitch + delta, minPitchDeg, maxPitchDeg);
        }

        // Giro bajo control del jugador: el cañon persigue DesiredYaw/Pitch
        // a velocidad limitada, igual que AimAt hace con el blanco de la
        // IA. Los dos ejes en la MISMA rotacion (Quaternion.Euler(pitch,
        // yaw, 0) en vez de dos transforms separados): Unity compone Euler
        // como yaw (mundo) por fuera y pitch (local, ya rotado por el yaw)
        // por dentro -- exactamente el gimbal de una torreta real, así que
        // transform.forward ya sale apuntando donde corresponde sin tocar
        // el disparo ni el Muzzle (ambos cuelgan de este mismo transform).
        public void TickPlayerAim(float dt)
        {
            if (!bootstrapped) Bootstrap();
            EnsureDesiredYaw();
            EnsureDesiredPitch();
            if (vehicle != null && vehicle.IsDestroyed) return;
            float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, DesiredYaw, playerTurnSpeedDegPerSec * dt);
            float newPitch = Mathf.MoveTowardsAngle(NormalizePitch(transform.eulerAngles.x), DesiredPitch, playerTurnSpeedDegPerSec * dt);
            transform.rotation = Quaternion.Euler(newPitch, newYaw, 0f);
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
            // Prioridad media: es informacion util para el artillero, pero
            // si el pool esta saturado tiene que perder contra disparos e
            // impactos, que son lo que decide si seguis vivo.
            if (wasReloading && cooldownTimer <= 0f) PlayAtMuzzle(SP.Presentation.SfxKind.TurretReloaded, 0.45f, 0.5f);

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
            // Pedido explicito: la bala del cañon vuela al doble de la
            // velocidad base del pool (comun con las armas de mano, que
            // siguen en 1x porque no pasan este parametro). SpeedMultiplier
            // es publico porque TurretAimView.PredictedImpactPoint tiene que
            // simular la MISMA velocidad real para que el anillo de impacto
            // no quede corto.
            pool.Spawn(spawnPos, transform.forward, shooterId, team, CurrentDamage,
                CurrentProjectileColor, ExplosionRadius, ProjectileGravity, vehicle, speedMultiplier: SpeedMultiplier);
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
            //
            // Las dos van en la boca del cañon (spawnPos ya ES esa
            // posicion, la misma que usa el proyectil y el fogonazo) y con
            // la prioridad mas alta del canal de efectos: es el sonido mas
            // fuerte del juego, si se lo come el limite de voces el disparo
            // mas potente queda mudo, que es peor que perder cualquier otra
            // cosa. El cuerpo va por encima del crack porque es el que
            // lleva el peso; si solo entra uno, que entre ese.
            PlayAtMuzzle(SP.Presentation.SfxKind.CannonBody, 0.9f, 0.95f);
            PlayAtMuzzle(SP.Presentation.SfxKind.CannonCrack, 0.55f, 0.9f);

            // Item 190: duck leve al disparar el cañon. Un cañonazo tiene
            // que "aplastar" un instante el resto de la mezcla o se pierde
            // entre cincuenta fusiles sonando a la vez; 0.25 es leve a
            // proposito, esto pasa muy seguido y un duck marcado aca
            // dejaria el combate entero bombeando. El marcado es el del
            // item 197 (camara lenta), que pasa una sola vez por partida.
            //
            // Respeta el techo de volumen del usuario porque AudioDucking
            // lo relee de PlayerPrefs: nunca sube el volumen mas alla del
            // slider. Va DESPUES de reproducir, aunque en la practica da
            // igual: AudioListener.volume se aplica en vivo, asi que el
            // propio cañonazo tambien se atenua un poco -- que es lo que
            // hace un compresor real y suena bien.
            SP.Presentation.AudioDucking.Duck(0.25f);

            SpawnMuzzleDust(spawnPos);
            KickChassis();

            // Pedido explicito: sacar la vibracion de camara del disparo
            // del cañon. Antes cada tiro sacudia la pantalla del artillero
            // (rig.KickDirectional) -- con el cooldown corto de rafaga
            // sostenida eso se sentia como un temblor constante, no como
            // un golpe puntual. El retroceso sigue existiendo (cañon que
            // se hunde, chasis que da un empujon), solo que ya no mueve la
            // camara del jugador.

            return true;
        }

        // Item 193: BUG REAL que arregla esto. La version anterior
        // reproducia el cañon con AudioSource.PlayClipAtPoint en
        // cam.transform.position, o sea EN LA OREJA DEL JUGADOR: sonaba
        // exactamente igual de fuerte y de centrado tuvieras el tanque al
        // lado o a ochenta metros, y nunca desde el lado correcto. El audio
        // posicional del arma mas ruidosa del juego estaba, en la practica,
        // apagado. Ahora se pasa la posicion REAL de la boca del cañon y la
        // atenuacion, el panorama y el filtro por distancia los aplica
        // AudioDirector.
        //
        // Deja de ser estatico a proposito: la posicion de la boca es
        // estado de ESTA torreta.
        void PlayAtMuzzle(SP.Presentation.SfxKind kind, float volume, float priority)
        {
            // La suite headless corre las fases en Edit mode: nada de audio
            // puede ejecutarse ahi. AudioDirector lo vuelve a chequear, pero
            // salir antes evita hasta generar el clip.
            if (!Application.isPlaying) return;
            // Instance null (Edit mode, o antes de que se construya el
            // director): PlayAt devuelve false en silencio, no tira.
            SP.Presentation.AudioDirector.PlayAt(kind, MuzzlePosition, volume, priority);
        }

        // Sin Muzzle cableado se cae al centro de la torreta, que es la
        // mejor aproximacion disponible y sigue siendo una posicion del
        // mundo -- nunca la de la camara.
        Vector3 MuzzlePosition => Muzzle != null ? Muzzle.position : transform.position;

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
            if (!barrelColorCached) 
            { 
                barrelColorCached = true; 
                if (rend.sharedMaterial != null) rend.sharedMaterial = new Material(rend.sharedMaterial);
                barrelBaseColor = rend.sharedMaterial != null ? rend.sharedMaterial.color : Color.white; 
            }
            rend.sharedMaterial.color = Color.Lerp(barrelBaseColor, new Color(1f, 0.25f, 0.1f), Heat);
        }
    }
}
