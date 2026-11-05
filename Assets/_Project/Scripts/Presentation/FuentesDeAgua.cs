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
            var shader = Resources.Load<Shader>("Shaders/AguaDeFuente");
            if (shader == null || !shader.isSupported) return 0;
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
            return n;
        }
    }
}
