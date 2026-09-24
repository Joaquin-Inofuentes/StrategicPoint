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

        Renderer rend;
        PlayerInputDriver driver;
        float edad;

        public const string PrefabMoneda = "Pickups/P_MunicionMoneda";
        float alturaBase;

        public static MunicionPickup Crear(Vector3 pos)
        {
            // Ronda 12: moneda 3D con una bala en relieve que gira y flota (prefab generado por MonedaMunicionBuilder).
            // Si el prefab no esta (build sin la carpeta), queda el cubito de siempre.
            var prefab = SP.Core.RecursosCache.Cargar<GameObject>(PrefabMoneda);
            if (prefab != null)
            {
                var moneda = Instantiate(prefab);
                moneda.name = "MunicionPickup";
                moneda.transform.position = pos + Vector3.up * 0.8f;
                moneda.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                moneda.transform.localScale = Vector3.one * 0.55f;
                var mp = moneda.AddComponent<MunicionPickup>();
                mp.alturaBase = moneda.transform.position.y;
                return mp;
            }

            // Ronda 13 (punto 5): el respaldo ya no es un cubo (se leia como "un cubo que aparece al matar"): es una moneda plana.
            var raiz = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            raiz.name = "MunicionPickup";
            raiz.transform.position = pos + Vector3.up * 0.3f;
            raiz.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            raiz.transform.localScale = new Vector3(0.4f, 0.04f, 0.4f);

            var colViejo = raiz.GetComponent<Collider>();
            if (colViejo != null) Destroy(colViejo);
            var col = raiz.AddComponent<SphereCollider>();
            col.isTrigger = true;

            var rendMoneda = raiz.GetComponent<Renderer>();
            rendMoneda.sharedMaterial = SafeMaterial.Create(ColorMoneda); // distinto del verde de CajaDeSuministros
            rendMoneda.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // pedido explicito: cilindros sin sombra

            var pk = raiz.AddComponent<MunicionPickup>();
            pk.alturaBase = raiz.transform.position.y;
            return pk;
        }

        void Awake()
        {
            rend = GetComponent<Renderer>();
        }

        void Update()
        {
            edad += Time.deltaTime;
            if (edad >= VidaMaxima) { Destroy(gameObject); return; }

            // Gira sobre su eje vertical y flota un poco, como una moneda de plataformas.
            transform.Rotate(0f, 200f * Time.deltaTime, 0f, Space.World);
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
