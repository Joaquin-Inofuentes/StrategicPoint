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
            n += CorregirSolapes();
            return n;
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
