using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Bug #008 (Sheets): "sumar shader (ambientacion de fuente de agua)". El agua de las fuentes era un cilindro con un color plano. Ahora
    // las piezas de agua (nombres "Fuente*Agua*", "Fuentecita_Agua", "Fuente*Chorro", "Fuente_Gota") usan SP/AguaDeFuente (ondas que se
    // mueven, brillo en las crestas, borde claro). El shader vive en Resources/ para entrar en la build. Se aplica al cargar cada escena
    // aditiva del entorno (las fuentes estan en SC_Operacion_Ciudad) y es idempotente.
    public static class FuentesDeAgua
    {
        public static int Aplicadas { get; private set; }
        static Material matSuperficie, matChorro;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribir()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            if (!s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            int n = Aplicar();
            if (n > 0) GameLog.Line($"Agua de fuentes: {n} piezas con el shader animado al cargar {s.name}");
        }

        public static bool EsAgua(string n) => n.StartsWith("Fuente", System.StringComparison.Ordinal)
            && (n.Contains("Agua") || n.Contains("Chorro") || n.Contains("Gota"));

        public static int Aplicar()
        {
            var shader = RecursosCache.Cargar<Shader>("Shaders/AguaDeFuente");
            if (shader == null || !shader.isSupported) { ArmarParticulas(); return 0; }
            if (matSuperficie == null) { matSuperficie = new Material(shader) { name = "M_AguaDeFuente", hideFlags = HideFlags.DontSave }; }
            if (matChorro == null)
            {
                matChorro = new Material(shader) { name = "M_AguaDeFuente_Chorro", hideFlags = HideFlags.DontSave };
                matChorro.SetFloat("_Flujo", 1f); matChorro.SetFloat("_Alfa", 0.7f);
            }
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r == null || !EsAgua(r.name) || r is ParticleSystemRenderer) continue;
                bool chorro = r.name.Contains("Chorro") || r.name.Contains("Gota");
                var m = chorro ? matChorro : matSuperficie;
                if (r.sharedMaterial == m) continue;
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                n++;
            }
            Aplicadas += n;
            n += CorregirSolapes();
            ArmarParticulas();
            return n;
        }

        // ---------------------------------------------------------------- R17 (plan 2.8): particulas del chorro y salpicadura en el cuenco
        // Un unico hijo "R17_Particulas" por cada "Fuente_Chorro" (idempotente), con 2 sistemas: el chorro (40/s) y la salpicadura en anillo (25/s);
        // una niebla fina y suman 116 particulas maximas por fuente (tope 120). Material transparente COMPARTIDO por todas las fuentes, sin colision, sin luces; con un AudioSource 3D de murmullo.
        public const string NombreParticulas = "R17_Particulas";
        public static int ParticulasArmadas { get; private set; }
        static Material matParticulas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarParticulas() { ParticulasArmadas = 0; matParticulas = null; }

        public static int ArmarParticulas()
        {
            if (!Application.isPlaying) return 0;
            int armadas = 0;
            var todos = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            foreach (var r in todos)
            {
                if (r == null || r is ParticleSystemRenderer || r.name != "Fuente_Chorro") continue;
                if (r.transform.Find(NombreParticulas) != null) continue;
                if (matParticulas == null) matParticulas = ParticleMaterialFactory.CreateTransparent(new Color(0.75f, 0.88f, 1f, 0.55f));

                var b = r.bounds;
                var boca = new Vector3(b.center.x, b.max.y, b.center.z);
                // superficie del cuenco: el "Fuente_Agua" mas cercano (el mismo criterio que CorregirSolapes), o la base del chorro
                float ySuperficie = b.min.y;
                float mejor = 1.5f;
                foreach (var o in todos)
                {
                    if (o == null || o.name != "Fuente_Agua") continue;
                    var d = o.bounds.center - b.center; d.y = 0f;
                    if (d.magnitude < mejor) { mejor = d.magnitude; ySuperficie = o.bounds.max.y; }
                }

                var raiz = new GameObject(NombreParticulas);
                raiz.transform.SetParent(r.transform, false);
                raiz.transform.position = boca; raiz.transform.rotation = Quaternion.identity;

                var chorro = NuevoSistema(raiz.transform, "Chorro", boca, 60);
                var m = chorro.main;
                m.startSpeed = 3.5f; m.gravityModifier = 1f; m.startLifetime = 0.9f; m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                m.startColor = new Color(0.75f, 0.88f, 1f, 0.55f);
                var em = chorro.emission; em.rateOverTime = 40f;
                var sh = chorro.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.03f;
                chorro.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // el cono emite hacia +Z local: se levanta para que apunte arriba
                chorro.Play();

                var salpica = NuevoSistema(raiz.transform, "Salpicadura", new Vector3(b.center.x, ySuperficie + 0.03f, b.center.z), 40);
                var ms = salpica.main;
                ms.startSpeed = 0.4f; ms.gravityModifier = 0.3f; ms.startLifetime = 0.5f; ms.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                ms.startColor = new Color(0.85f, 0.94f, 1f, 0.5f);
                var ee = salpica.emission; ee.rateOverTime = 25f;
                var ss = salpica.shape; ss.shapeType = ParticleSystemShapeType.Circle; ss.radius = 0.4f; ss.radiusThickness = 0f; ss.arc = 360f;
                var vel = salpica.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0.6f); vel.z = new ParticleSystem.MinMaxCurve(0f);
                salpica.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // el circulo queda horizontal
                salpica.Play();

                // niebla fina sobre el cuenco: 16 particulas grandes y casi transparentes que suben despacio (con las otras dos suman 116 <= 120)
                var niebla = NuevoSistema(raiz.transform, "Niebla", new Vector3(b.center.x, ySuperficie + 0.15f, b.center.z), 16);
                var mn = niebla.main;
                mn.startSpeed = 0.05f; mn.gravityModifier = 0f; mn.startLifetime = 2.5f; mn.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                mn.startColor = new Color(0.9f, 0.95f, 1f, 0.10f);
                var en = niebla.emission; en.rateOverTime = 6f;
                var sn = niebla.shape; sn.shapeType = ParticleSystemShapeType.Circle; sn.radius = 0.5f; sn.radiusThickness = 1f; sn.arc = 360f;
                var vn = niebla.velocityOverLifetime; vn.enabled = true; vn.space = ParticleSystemSimulationSpace.World;
                vn.x = new ParticleSystem.MinMaxCurve(0f); vn.y = new ParticleSystem.MinMaxCurve(0.15f); vn.z = new ParticleSystem.MinMaxCurve(0f);
                niebla.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                niebla.Play();

                // murmullo del agua: 3D, en lazo, se oye a unos 16 m (el clip es uno solo, compartido por todas las fuentes)
                var au = raiz.AddComponent<AudioSource>();
                au.clip = SfxSintetico.AguaDeFuente(); au.loop = true; au.playOnAwake = false;
                au.spatialBlend = 1f; au.rolloffMode = AudioRolloffMode.Linear; au.minDistance = 2f; au.maxDistance = 16f; au.volume = 0.3f;
                au.Play();

                // el chorro fijo ("Fuente_Gota") del mismo surtidor se apaga: con particulas se veria doble
                foreach (var o in todos)
                {
                    if (o == null || o.name != "Fuente_Gota" || !o.enabled) continue;
                    var d = o.bounds.center - b.center; d.y = 0f;
                    if (d.magnitude < 1.5f) o.enabled = false;
                }
                armadas++;
            }
            ParticulasArmadas += armadas;
            return armadas;
        }

        static ParticleSystem NuevoSistema(Transform padre, string nombre, Vector3 pos, int maxParticulas)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = maxParticulas;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var col = ps.collision; col.enabled = false;
            var luces = ps.lights; luces.enabled = false;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = matParticulas;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        // Bug #362 (2026-11-06: "el agua se ve mal y solapado"): la fuente de la Plaza Civica y la de Prestigio estan en el MISMO punto y se pisaban:
        // la copa de una se metia en la taza alta de la otra (agua adentro de piedra) y el espejo de abajo estaba a ras del borde del cuenco (parpadeo).
        // Se oculta la copa duplicada y el agua se apoya apenas por encima del borde de su cuenco.
        public static int Solapes { get; private set; }
        static int CorregirSolapes()
        {
            int c = 0;
            Renderer copa = null, tazaAlta = null;
            var aguasDeAbajo = new System.Collections.Generic.List<Renderer>(); var cuencos = new System.Collections.Generic.List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r == null) continue;
                switch (r.name)
                {
                    case "Fuente_Copa": copa = r; break;
                    case "Fuente_Taza_Alta": tazaAlta = r; break;
                    case "Fuente_Agua": aguasDeAbajo.Add(r); break;
                    case "Fuente_Cuenco": cuencos.Add(r); break;
                }
            }
            if (copa != null && tazaAlta != null && copa.enabled)
            {
                var d = copa.bounds.center - tazaAlta.bounds.center; d.y = 0f;
                if (d.magnitude < 1.5f) { copa.enabled = false; foreach (var col in copa.GetComponentsInChildren<Collider>(true)) col.enabled = false; c++; }
            }
            foreach (var agua in aguasDeAbajo)
                foreach (var cuenco in cuencos)
                {
                    var d = agua.bounds.center - cuenco.bounds.center; d.y = 0f;
                    if (d.magnitude > 1f) continue;
                    float techo = cuenco.bounds.max.y + 0.05f;
                    if (agua.bounds.center.y < techo - 0.001f)
                    {
                        var p = agua.transform.position; p.y += techo - agua.bounds.center.y; agua.transform.position = p; c++;
                    }
                }
            Solapes += c;
            return c;
        }
    }
}
