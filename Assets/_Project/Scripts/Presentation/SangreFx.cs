using UnityEngine;

namespace SP.Presentation
{
    // Ronda 13 (T1): sangre de la cuchillada. Pedido del dueno: "particulas de sangre MUY visibles".
    // Una rafaga de 48 particulas (gotas rapidas + grumos grandes que se achican) en rojo saturado HDR, con material propio,
    // saliendo en un cono de 50 grados en la direccion del tajo, mas un destello y una mancha en el piso (8 s).
    // Pool fijo de 4 sistemas (SangreFx_<n>) reciclados en rotacion: no crece ni deja basura. No usa DebrisPool (lo comparten las explosiones).
    public static class SangreFx
    {
        public const int Cupo = 4, ParticulasPorGolpe = 48;
        public static int Rafagas { get; private set; }
        public static int Manchas { get; private set; }
        static readonly ParticleSystem[] pool = new ParticleSystem[Cupo];
        static int siguiente; static Transform raiz; static Material material;

        // Rojo saturado HDR (1,0; 0,05; 0,03) x 2,5: queda por encima de 1 para que brille aun a pleno sol.
        // Con x2,5 el tonemapping lo viraba a naranja (se vio en la captura): se baja el brillo y se quita el verde/azul para que siga leyendose ROJO sangre.
        public static readonly Color RojoHdr = new Color(0.72f, 0.0f, 0.02f, 1f);   // rojo sangre LDR: el tonemapping viraba cualquier rojo HDR a naranja

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Rafagas = 0; Manchas = 0; siguiente = 0; raiz = null; material = null; for (int i = 0; i < pool.Length; i++) pool[i] = null; }

        public static void ResetearContadores() { Rafagas = 0; Manchas = 0; }

        static Material Mat()
        {
            if (material != null) return material;
            // Sin niebla: se arma como material de particulas transparente (Unlit) y se le apaga la niebla por keyword cuando el shader la tiene.
            material = ParticleMaterialFactory.CreateTransparent(Color.white);
            material.name = "MAT_SangreFx";
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.DisableKeyword("FOG_LINEAR"); material.DisableKeyword("FOG_EXP"); material.DisableKeyword("FOG_EXP2");
            return material;
        }

        static ParticleSystem Crear(int n)
        {
            if (raiz == null)
            {
                var r = new GameObject("SangreFxPool") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
                raiz = r.transform;
            }
            var go = new GameObject("SangreFx_" + n) { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            go.transform.SetParent(raiz, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 160;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.gravityModifier = 1f;
            main.startColor = RojoHdr;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 50f; sh.radius = 0.04f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.5f, 0.5f), 0.7f), new GradientColorKey(new Color(0.55f, 0.35f, 0.35f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var tam = ps.sizeOverLifetime; tam.enabled = true; tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            rend.mesh = ParticleMaterialFactory.MallaEsfera();
            rend.sharedMaterial = Mat();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
            return ps;
        }

        // Rafaga en 'punto' hacia 'dir' (la direccion del tajo, del que pega al herido). Devuelve el sistema usado.
        public static ParticleSystem Spawn(Vector3 punto, Vector3 dir)
        {
            if (!Application.isPlaying) return null;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up; else dir.Normalize();
            dir = (dir + Vector3.up * 0.25f).normalized;   // sale un poco hacia arriba para que la lluvia se vea antes de caer
            int i = siguiente++ % Cupo;
            if (pool[i] == null) pool[i] = Crear(i);
            var ps = pool[i];
            ps.transform.SetPositionAndRotation(punto, Quaternion.LookRotation(dir));
            ps.Clear(true);
            var cola = new ParticleSystem.EmitParams();
            for (int k = 0; k < ParticulasPorGolpe; k++)
            {
                bool grumo = k < 10;   // 10 grumos grandes que se quedan en el aire, 38 gotas rapidas
                // cono de 50 grados alrededor de dir
                var d = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.AngleAxis(Random.Range(0f, 50f) * Mathf.Sqrt(Random.value), Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up).normalized) * dir;
                cola.position = punto + Random.insideUnitSphere * 0.08f;
                cola.velocity = d * (grumo ? Random.Range(1.5f, 3.2f) : Random.Range(3f, 6f));
                cola.startLifetime = grumo ? Random.Range(0.55f, 0.9f) : Random.Range(0.6f, 1.1f);
                cola.startSize = grumo ? Random.Range(0.11f, 0.2f) : Random.Range(0.05f, 0.1f);
                cola.startColor = new Color(RojoHdr.r * Random.Range(0.8f, 1f), RojoHdr.g, RojoHdr.b, 1f);
                ps.Emit(cola, 1);
            }
            Rafagas++;
            // destello rojo corto y mancha en el piso
            // El destello a 0,35 tapaba a la victima con hexagonos naranjas (se vio en la captura): ahora es chico y rojo oscuro; la sangre la ponen las particulas.
            ImpactFx.Spawn(punto, new Color(0.7f, 0.0f, 0.0f), 0.1f, 0.2f);
            ManchaEnElPiso(punto);
            return ps;
        }

        static void ManchaEnElPiso(Vector3 punto)
        {
            var pos = punto; var n = Vector3.up;
            if (SP.Core.PisoReal.Altura(punto, out var pisoY, out var pisoN, 1.5f, 12f)) { pos = new Vector3(punto.x, pisoY, punto.z); n = pisoN; }
            else pos = new Vector3(punto.x, punto.y - 0.9f, punto.z);
            DecalPool.Spawn(DecalKind.Sangre, pos, n, 0.9f);
            Manchas++;
        }

        // Particulas vivas en todo el pool (para medirlo desde un check).
        public static int VivasAhora { get { int n = 0; foreach (var p in pool) if (p != null) n += p.particleCount; return n; } }
    }
}
