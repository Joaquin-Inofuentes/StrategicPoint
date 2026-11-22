using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Pulido del nivel 2 (Sheets, Centro de datos: "estan durmiendo siesta"): los enemigos abatidos se quedaban tirados en el piso para siempre y,
    // vistos desde la camara tactica, parecian soldados dormidos. A los 14 s de caer, el cuerpo de un ENEMIGO se hunde 0,6 m en 1,4 s y deja de dibujarse.
    // Solo enemigos (los aliados caidos se revivian y se ven en el HUD): el soldado sigue registrado, con su collider/vida, asi que ningun
    // contador ni objetivo cambia; solo se apagan sus renderers. Si algo lo revive antes, no se toca.
    public sealed class RetiroDeEnemigosCaidos : MonoBehaviour
    {
        public const float SegundosAntesDeRetirar = 14f;
        public const float SegundosDeAchique = 1.4f;   // R17 2.9b: ahora es el tiempo del hundimiento
        public const float MetrosDeHundimiento = 0.6f;
        public static int Retirados { get; private set; }

        System.IDisposable suscripcion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            Retirados = 0; PolvosAlCaer = 0; ArmasSoltadas = 0;
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
            Asegurar(SceneManager.GetActiveScene());
        }

        static void AlCargar(Scene s, LoadSceneMode modo) => Asegurar(s);

        static void Asegurar(Scene s)
        {
            if (!s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            if (Object.FindFirstObjectByType<RetiroDeEnemigosCaidos>() != null) return;
            new GameObject("RetiroDeEnemigosCaidos", typeof(RetiroDeEnemigosCaidos));
        }

        void OnEnable() { suscripcion = EventBus.Instance.Subscribe<EntityDiedEvent>(AlMorir); }
        void OnDisable() { suscripcion?.Dispose(); suscripcion = null; }

        void AlMorir(EntityDiedEvent e)
        {
            var s = ActorRegistry.FindById(e.ActorId);
            if (s == null || s.Team != TeamId.Enemy) return;
            StartCoroutine(Retirar(s));
            StartCoroutine(PolvoAlCaer(s));
            SoltarElArma(s);
        }

        // R17 (pendiente 2.9): al caer el cuerpo levanta un poco de polvo cuando toca el piso (~0,6 s despues del impacto).
        public static int PolvosAlCaer { get; private set; }
        static IEnumerator PolvoAlCaer(Soldier s)
        {
            yield return new WaitForSeconds(0.6f);
            if (s == null || s.Health == null || s.Health.IsAlive) yield break;
            SpriteFx.Lanzar("smoke_02", s.transform.position + Vector3.up * 0.05f, new Color(0.62f, 0.58f, 0.52f, 0.30f), 0.35f, 0.9f, 0.7f, Random.Range(-30f, 30f), Vector3.up * 0.2f, 1.0f, 1);
            PolvosAlCaer++;
        }

        // R17 (pendiente 2.9): el arma se le cae de la mano. Se clona el modelo del arma (sin scripts ni colliders, asi no empuja a nadie),
        // se apaga el de la mano y el clon cae con gravedad y un poco de giro hasta quedar tirado al lado del cuerpo; desaparece junto con el.
        public const float GravedadDelArma = 9.8f;
        public static int ArmasSoltadas { get; private set; }
        void SoltarElArma(Soldier s)
        {
            var wh = s.GetComponent<WeaponHolder>();
            var rend = wh != null ? wh.WeaponVisualRenderer : null;
            if (rend == null) return;   // (otro suscriptor de la muerte ya puede haber apagado el renderer: el clon se enciende igual)
            var go = Object.Instantiate(rend.gameObject, rend.transform.position, rend.transform.rotation);
            go.name = "ArmaCaida";
            go.transform.SetParent(null, true);
            go.transform.localScale = rend.transform.lossyScale;
            foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(m);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in rend.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            go.SetActive(true);
            ArmasSoltadas++;
            Object.Destroy(go, SegundosAntesDeRetirar + SegundosDeAchique);
            StartCoroutine(CaerElArma(go.transform, s.transform.position.y + 0.06f));
        }

        static IEnumerator CaerElArma(Transform arma, float sueloY)
        {
            float vy = 0f, giro = Random.Range(120f, 260f) * (Random.value < 0.5f ? -1f : 1f);
            float yaw = arma.eulerAngles.y, roll = Random.Range(70f, 90f);
            while (arma != null && arma.position.y > sueloY)
            {
                float dt = Time.deltaTime;
                vy -= GravedadDelArma * dt;
                var p = arma.position; p.y = Mathf.Max(sueloY, p.y + vy * dt); arma.position = p;
                arma.Rotate(giro * dt, 0f, 0f, Space.Self);
                yield return null;
            }
            if (arma != null) arma.rotation = Quaternion.Euler(0f, yaw, roll);   // queda de costado sobre el piso
        }

        static IEnumerator Retirar(Soldier s)
        {
            float t = 0f;
            while (t < SegundosAntesDeRetirar)
            {
                yield return null;
                t += Time.deltaTime;
                if (s == null || s.Health == null || s.Health.IsAlive) yield break;
            }
            // R17 2.9b: en vez de encoger (el "pop"), el cuerpo se hunde 0,6 m con un poco de polvo; despues se apagan los renderers.
            var pos0 = s.transform.position;
            SpriteFx.Lanzar("smoke_02", pos0 + Vector3.up * 0.1f, new Color(0.6f, 0.57f, 0.52f, 0.35f), 0.4f, 1.2f, 1.0f, Random.Range(-30f, 30f), Vector3.up * 0.3f, 1.5f, 1);
            t = 0f;
            while (t < SegundosDeAchique)
            {
                yield return null;
                t += Time.deltaTime;
                if (s == null) yield break;
                if (s.Health.IsAlive) { s.transform.position = pos0; yield break; }
                s.transform.position = pos0 + Vector3.down * (MetrosDeHundimiento * Mathf.SmoothStep(0f, 1f, t / SegundosDeAchique));
            }
            foreach (var r in s.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Retirados++;
        }
    }
}
