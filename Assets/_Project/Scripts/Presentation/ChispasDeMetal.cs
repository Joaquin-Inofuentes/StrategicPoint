using UnityEngine;

namespace SP.Presentation
{
    // Chispas de una bala contra metal (el escudo del escudero): una rafaga de cubitos incandescentes que sale disparada por la normal, cae con gravedad y se
    // apaga. Pool fijo de sistemas (Cupo) reciclados en rotacion: nunca crece ni deja basura. Pedido 2026-11-13: "particulas si disparo contra el escudo".
    public static class ChispasDeMetal
    {
        public const int Cupo = 10, ChispasPorImpacto = 60;
        public static int Rafagas { get; private set; }
        static readonly ParticleSystem[] pool = new ParticleSystem[Cupo];
        static int siguiente; static Transform raiz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Rafagas = 0; siguiente = 0; raiz = null; for (int i = 0; i < pool.Length; i++) pool[i] = null; }

        static ParticleSystem Crear()
        {
            if (raiz == null)
            {
                var r = new GameObject("ChispasDeMetalPool") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
                raiz = r.transform;
            }
            var go = new GameObject("ChispasDeMetal") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            go.transform.SetParent(raiz, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 160;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            main.gravityModifier = 1.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.7f), new Color(1f, 0.5f, 0.08f));
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 40f; sh.radius = 0.03f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0.6f), new GradientColorKey(new Color(0.5f, 0.2f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var tam = ps.sizeOverLifetime; tam.enabled = true; tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            rend.mesh = ParticleMaterialFactory.MallaCubo();
            rend.material = ParticleMaterialFactory.CreateTransparent(Color.white);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
            return ps;
        }

        // Una rafaga en 'punto', saliendo hacia 'normal' (del metal hacia afuera, es decir hacia el que dispara).
        public static void Spawn(Vector3 punto, Vector3 normal, int cantidad = ChispasPorImpacto)
        {
            if (!Application.isPlaying) return;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;
            int i = siguiente++ % Cupo;
            if (pool[i] == null) pool[i] = Crear();
            var ps = pool[i];
            ps.transform.SetPositionAndRotation(punto, Quaternion.LookRotation(normal.normalized));
            ps.Clear(true);
            ps.Emit(Mathf.Clamp(cantidad, 1, 160));
            Rafagas++;
            // el destello del golpe: un fogonazo corto sobre el metal
            SparkleBurstFx.Spawn(punto + normal.normalized * 0.05f, new Color(1f, 0.92f, 0.55f), 0.16f, 0.5f, 5, 0f);
        }

        // Particulas vivas en todo el pool (para medirlo desde un check).
        public static int VivasAhora { get { int n = 0; foreach (var p in pool) if (p != null) n += p.particleCount; return n; } }
    }
}
