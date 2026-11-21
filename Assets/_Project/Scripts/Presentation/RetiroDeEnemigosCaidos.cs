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
            Retirados = 0;
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
