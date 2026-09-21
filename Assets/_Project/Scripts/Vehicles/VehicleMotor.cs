using UnityEngine;
using SP.Core;

namespace SP.Vehicles
{
    // Motor del vehículo: acelera y frena de forma progresiva (no es
    // instantáneo como el motor de un soldado), y gira más rápido cuanto
    // más rápido va.
    //
    // BUG REAL: esto escribia el transform sin consultar fisica nunca, asi
    // que el tanque atravesaba el Muro, los arboles, las barricadas y los
    // barriles como si no existieran -- el mismo agujero que tenia el
    // soldado. Ahora los dos pasan por SP.Core.Deslizador, que ademas
    // frena el vehiculo al chocar: seguir acelerando contra una pared
    // dejaba CurrentSpeed al maximo y el tanque salia disparado en cuanto
    // encontraba un hueco.
    public class VehicleMotor : MonoBehaviour
    {
        [SerializeField] float maxSpeed = 12f;
        [SerializeField] float acceleration = 8f;
        [SerializeField] float brakeDeceleration = 14f;
        [SerializeField] float dragWhenIdle = 4f;
        [SerializeField] float turnDegPerSec = 70f;

        public float CurrentSpeed { get; private set; }
        public float MaxSpeed => maxSpeed;

        // throttle: -1..1 (atrás/adelante). steer: -1..1 (izq/der).
        public void Drive(float throttle, float steer, float dt)
        {
            if (Mathf.Abs(throttle) > 0.01f)
            {
                CurrentSpeed += throttle * acceleration * dt;
            }
            else
            {
                CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, dragWhenIdle * dt);
            }

            CurrentSpeed = Mathf.Clamp(CurrentSpeed, -maxSpeed * 0.5f, maxSpeed);

            float speedFactor = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / maxSpeed);
            if (Mathf.Abs(CurrentSpeed) > 0.05f)
                transform.Rotate(Vector3.up, steer * turnDegPerSec * speedFactor * Mathf.Sign(CurrentSpeed) * dt, Space.World);

            Avanzar(CurrentSpeed * dt, dt);
            KnockNearbyProps();
            AtropellarSoldados();
        }

        public void Brake(float dt)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, brakeDeceleration * dt);
            Avanzar(CurrentSpeed * dt, dt);
            KnockNearbyProps();
            AtropellarSoldados();
        }

        Collider cuerpo;
        float radio = -1f;

        void EnsureCuerpo()
        {
            if (cuerpo != null) return;
            cuerpo = GetComponent<Collider>();
            if (cuerpo == null) cuerpo = GetComponentInChildren<Collider>();
            // usarMayor=true: un tanque es mucho mas largo que ancho (acá
            // 3.6 x 2.2), y el lado corto que usa el resto de los cuerpos
            // (pensado para algo casi cuadrado, como un soldado) dejaba la
            // punta/cola sobresalir del radio de barrido -- ver el
            // comentario en Deslizador.RadioDe.
            radio = Deslizador.RadioDe(cuerpo, transform, 1f, usarMayor: true);
        }

        // BUG REAL que esto corrige ("se siente como rebote, es un tanque,
        // es pesado"): el corte de velocidad al chocar usaba
        // Mathf.MoveTowards(CurrentSpeed, 0f, Mathf.Abs(CurrentSpeed)) --
        // el delta maximo es LA VELOCIDAD ENTERA, asi que un tanque a
        // 9,8 m/s pasaba a 0,00 en UN SOLO frame (0,02s) al tocar algo.
        // Medido: de 9,76 a 0,00 instantaneo. Eso no es una desaceleracion,
        // es un freno de mano infinito -- se siente como pegar contra una
        // pared de goma (rebote), no como el impacto de algo pesado que
        // tarda una fraccion de segundo en frenar. ImpactoDecelPerSec (mas
        // dura que el frenado normal, brakeDeceleration=14, porque no fue
        // una decision del jugador) reemplaza el snap por una frenada
        // fuerte pero real: a 12 m/s tarda ~0,34s en pararse en vez de
        // 0,02s.
        const float ImpactoDecelPerSec = 35f;

        void Avanzar(float distancia, float dt)
        {
            EnsureCuerpo();

            // Ronda 12: el tanque revienta los obstaculos destructibles en su camino (y pierde un poco de velocidad por cada uno).
            int aplastados = Atropello.AplastarObstaculos(transform, CurrentSpeed, radio);
            if (aplastados > 0) CurrentSpeed *= Mathf.Pow(0.88f, aplastados);

            var pedido = transform.forward * distancia;
            var real = Deslizador.Resolver(transform, cuerpo, pedido, radio);
            transform.position += real;

            // Si el choque se comio casi todo el avance, la velocidad se
            // corta. Sin esto el tanque queda apoyado contra la pared con
            // el acelerador a fondo y sale disparado al primer hueco.
            float pedidoLargo = Mathf.Abs(distancia);
            if (pedidoLargo > 0.0001f)
            {
                var realPlano = new Vector3(real.x, 0f, real.z);
                if (realPlano.magnitude < pedidoLargo * 0.25f)
                    CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, ImpactoDecelPerSec * dt);
            }
        }

        // Antes el vehiculo atravesaba el escenario sin alterar nada, lo
        // que reforzaba la sensacion de que flota en vez de pesar. Solo
        // cuenta si va con algo de velocidad: estar apoyado contra un
        // bidon quieto no deberia voltearlo.
        // Los soldados en el camino. Va pegado a KnockNearbyProps porque
        // es el mismo barrido conceptual: lo que el vehiculo se lleva
        // puesto cuando pasa. Ver SP.Vehicles.Atropello.
        Vehicle datos;

        void AtropellarSoldados()
        {
            if (datos == null) datos = GetComponent<Vehicle>();
            Atropello.Barrer(transform, cuerpo, CurrentSpeed, datos);
        }

        void KnockNearbyProps()
        {
            if (Mathf.Abs(CurrentSpeed) < 1f) return;
            var props = SP.Presentation.LightProp.All;
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (prop == null || prop.IsKnocked) continue;
                var d = prop.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= prop.KnockRadius + 1.4f) prop.Knock(transform.forward * Mathf.Sign(CurrentSpeed));
            }
        }

        public bool IsStopped => Mathf.Abs(CurrentSpeed) < 0.05f;

        // Empujoncito que NO viene de acelerar (el retroceso del cañon al
        // disparar, ver TurretWeapon.KickChassis/RecoverChassisShake).
        // BUG REAL que esto corrige: antes esas dos escribian
        // vehicle.transform.position DIRECTO, sin pasar por Deslizador --
        // un tanque apoyado contra un obstaculo se metia un poco adentro
        // en cada cañonazo (el empuje no chocaba con nada) y, con rafagas
        // sostenidas cerca de una pared, terminaba visiblemente enterrado.
        // Nudge corre el mismo desplazamiento por el resolutor de
        // colisiones que ya usa Avanzar, asi que el retroceso tambien
        // choca contra el escenario en vez de atravesarlo.
        public void Nudge(Vector3 worldDelta)
        {
            EnsureCuerpo();
            var real = Deslizador.Resolver(transform, cuerpo, worldDelta, radio);
            transform.position += real;
        }
    }
}
