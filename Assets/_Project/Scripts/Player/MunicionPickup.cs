using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;

namespace SP.Player
{
    // Municion suelta al morir un enemigo (item nuevo). 100% por codigo, sin
    // prefab ni asset externo -- mismo espiritu que CajaDeSuministros.Crear,
    // pero mas chica y sin reposicion (se recoge una vez y desaparece).
    //
    // Deteccion por DISTANCIA en Update, no OnTriggerEnter: los soldados de
    // este proyecto no llevan Rigidbody (ver Rehen.cs para el mismo
    // razonamiento), asi que un trigger normal de Unity nunca dispararia
    // contra ellos. El Collider que lleva el pickup esta en isTrigger solo
    // para no bloquear el paso de nadie mientras esta en el piso.
    public class MunicionPickup : MonoBehaviour
    {
        public const float Radio = 1.6f;
        // Se cae sola si nadie la recoge en un rato: si no, el piso de una
        // mision larga terminaria lleno de cajitas de balas de cada baja.
        public const float VidaMaxima = 45f;

        PlayerInputDriver driver;
        float edad;

        float alturaBase;

        public static MunicionPickup Crear(Vector3 pos)
        {
            var raiz = new GameObject("MunicionPickup");
            raiz.transform.position = pos;

            var col = raiz.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var pk = raiz.AddComponent<MunicionPickup>();
            pk.alturaBase = pos.y;

            InteractableDiamond.Agregar(raiz.transform, Vector3.up * 0.8f, DiamondGizmo.ColorMunicion);

            return pk;
        }

        void Update()
        {
            edad += Time.deltaTime;
            if (edad >= VidaMaxima) { Destroy(gameObject); return; }

            // Flota un poco
            var p = transform.position;
            p.y = alturaBase + Mathf.Sin(edad * 3.2f) * 0.07f;
            transform.position = p;

            if (driver == null) driver = PlayerInputDriver.Activo;
            var yo = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            if (yo == null || yo.Weapon == null || !yo.Health.IsAlive) return;

            var d = yo.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > Radio * Radio) return;

            yo.Weapon.AgregarMunicion(1);

            // Pedido explicito: "no quiero texto, quiero visuales" -- antes
            // esto pasaba por Feedback.Accion(..., "+MUNICION", ...), que
            // ademas del sonido siempre agrega el texto flotante en el
            // mundo Y el aviso en la cola de HUD. Aca se arma a mano solo
            // la mitad sin texto: sonido dedicado (AmmoPickup, no el
            // generico de curacion de antes) + el mismo anillo pulsante que
            // usa el feedback de ordenes (OrderMarkerFx), sin ninguna
            // palabra en pantalla.
            AudioDirector.PlayAt(SfxKind.AmmoPickup, transform.position, 0.5f, 0.7f);
            OrderMarkerFx.Spawn(transform.position, ColorMoneda, 0.5f);

            // Pedido explicito (ronda nueva): "revisa q al recojer... tenga
            // sistema de particulas interesante". El anillo de OrderMarkerFx
            // de arriba ya es un ParticleSystem, pero es el mismo aviso
            // generico que comparten las ordenes -- este estallido dorado
            // hacia arriba es propio del pickup, mas vistoso y distinto.
            SparkleBurstFx.Spawn(transform.position, ColorMoneda, 0.6f, 2f, 22, 3.2f);

            // Pedido explicito: "al recoger una municion que tenga fisicas
            // y que desaparezca con un efecto de fisicas simples" -- en vez
            // de un Destroy() seco, unas chispitas doradas salen disparadas
            // con la misma fisica simple (gravedad + rebote) que ya usa
            // DebrisPool para escombros de impacto.
            for (int i = 0; i < 5; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 1.4f).normalized;
                DebrisPool.Spawn(transform.position, dir * Random.Range(2.5f, 5f), ColorMoneda, Random.Range(0.06f, 0.11f), 0.7f);
            }
            Destroy(gameObject);
        }

        static readonly Color ColorMoneda = new Color(0.85f, 0.65f, 0.15f);

        // ------------------------------------------------------------------
        // Enganche con la muerte de un enemigo: un solo listener global (no
        // uno por soldado) que crea el pickup en la posicion de cualquier
        // Soldier del equipo Enemy que muera durante la partida.
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribirse()
        {
            EventBus.Instance.Subscribe<EntityDiedEvent>(OnEnemigoMuerto);
        }

        static void OnEnemigoMuerto(EntityDiedEvent evt)
        {
            if (!Application.isPlaying) return;
            var soldado = ActorRegistry.FindById(evt.ActorId);
            if (soldado == null || soldado.Team != TeamId.Enemy) return;
            Crear(soldado.transform.position);
        }
    }
}
