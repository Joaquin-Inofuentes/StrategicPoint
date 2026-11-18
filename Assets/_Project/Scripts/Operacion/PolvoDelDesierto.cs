using UnityEngine;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // Ronda 13 T14: polvo del desierto. Un ParticleSystem en espacio mundo que sigue a la camara (maximo 150 particulas, alfa 0,15, viento constante)
    // y 3 o 4 remolinos fijos "RemolinoDePolvo_<n>" a los costados de la ruta. Solo emite mientras la camara esta en el sector Ruta.
    public sealed class PolvoDelDesierto : MonoBehaviour
    {
        public const int MaximoDeParticulas = 150;
        public const float AlfaDelPolvo = 0.15f;
        public static readonly Vector3 Viento = new Vector3(2.6f, 0.15f, 1.0f);
        public static ParticleSystem Emisor { get; private set; }
        public static int Remolinos { get; private set; }

        static Texture2D suave;
        ParticleSystem ps; ParticleSystem.EmissionModule em;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Emisor = null; Remolinos = 0; suave = null; }

        // Disco blando (alfa radial) para que no sean cuadrados.
        static Texture2D TexturaSuave()
        {
            if (suave != null) return suave;
            const int n = 32;
            suave = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "PolvoSuave", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                    suave.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            suave.Apply(false, true);
            return suave;
        }

        static ParticleSystem NuevoSistema(string nombre, Transform padre, Vector3 pos)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false); go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = ParticleMaterialFactory.CreateTransparent(new Color(0.85f, 0.74f, 0.55f, 1f), TexturaSuave());
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
            return ps;
        }

        public static void Armar(Transform[] ruta)
        {
            if (!Application.isPlaying) return;
            var previo = GameObject.Find("PolvoDelDesierto_Raiz"); if (previo != null) Object.Destroy(previo);
            var raiz = new GameObject("PolvoDelDesierto_Raiz").transform;

            // 1) polvo que sigue a la camara
            var ps = NuevoSistema("PolvoDelDesierto", raiz, Vector3.zero);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startColor = new Color(0.86f, 0.75f, 0.56f, AlfaDelPolvo);
            main.maxParticles = MaximoDeParticulas;
            var em = ps.emission; em.rateOverTime = 22f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60f, 5f, 60f); sh.position = new Vector3(0f, 1.5f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = Viento.x; vel.y = Viento.y; vel.z = Viento.z;   // viento constante
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));
            ps.gameObject.AddComponent<PolvoDelDesierto>();
            Emisor = ps;

            // 2) remolinos fijos junto a la ruta
            Remolinos = 0;
            if (ruta != null && ruta.Length >= 2)
            {
                float[] frac = { 0.15f, 0.4f, 0.65f, 0.85f };
                for (int i = 0; i < frac.Length; i++)
                {
                    int idx = Mathf.Clamp(Mathf.RoundToInt(frac[i] * (ruta.Length - 1)), 0, ruta.Length - 1);
                    if (ruta[idx] == null) continue;
                    int sig = Mathf.Min(idx + 1, ruta.Length - 1), ant = Mathf.Max(idx - 1, 0);
                    var tan = ruta[sig].position - ruta[ant].position; tan.y = 0f; if (tan.sqrMagnitude < 0.01f) tan = Vector3.forward;
                    var lado = Vector3.Cross(Vector3.up, tan.normalized) * ((i % 2 == 0 ? 1f : -1f) * (28f + 6f * i));
                    var pos = ruta[idx].position + lado;
                    if (Physics.Raycast(pos + Vector3.up * 40f, Vector3.down, out var h, 90f, ~0, QueryTriggerInteraction.Ignore)) pos = h.point; else pos.y = ruta[idx].position.y;
                    CrearRemolino(raiz, ++Remolinos, pos);
                }
            }
            GameLog.Line($"[Operacion] Polvo del desierto: emisor sigue a la camara ({MaximoDeParticulas} max), {Remolinos} remolinos");
        }

        static void CrearRemolino(Transform raiz, int n, Vector3 pos)
        {
            var ps = NuevoSistema("RemolinoDePolvo_" + n, raiz, pos);
            var main = ps.main;
            main.startLifetime = 3.2f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startColor = new Color(0.86f, 0.75f, 0.56f, 0.25f);
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 18f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.5f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = 2.6f; vel.orbitalY = 3.2f; vel.radial = 0.5f;   // columna que gira y se ensancha
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        void Awake() { ps = GetComponent<ParticleSystem>(); em = ps.emission; }

        void LateUpdate()
        {
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return;
            transform.position = cam.transform.position;   // sigue a la camara (las particulas viven en espacio mundo)
            bool enSector = SectorDeRuta.Contiene(cam.transform.position);
            if (em.enabled != enSector) em.enabled = enSector;
        }
    }
}
