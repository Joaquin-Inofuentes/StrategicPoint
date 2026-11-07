using UnityEngine;

namespace SP.Presentation
{
    // Coherencia de la ciudad (2026-11-06): el barrido de piezas grandes sin collider encontro props macizos que se atravesaban caminando:
    // setos, topiarios, quiosco, obelisco y cabinas. Al cargar cada escena del entorno se les agrega un
    // BoxCollider ajustado al mesh (las barandas NO: las de escaleras son inclinadas y la caja las convertia en una rampa invisible que levanta soldados y frena balas) (idempotente: si ya tienen collider no se toca). Solo piezas bajo "Ciudad" y con esos nombres.
    public static class ColisionesDeLaCiudad
    {
        public static int Agregados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribir()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            if (!s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            Aplicar();
        }

        public static bool Corresponde(string n) =>
            n.StartsWith("Seto", System.StringComparison.Ordinal) || n == "Topiario" || n == "Quiosco" || n == "Obelisco" || n == "Cabina";

        public static int Aplicar()
        {
            int c = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (mf == null || mf.sharedMesh == null || !Corresponde(mf.name)) continue;
                if (mf.GetComponent<Renderer>() is ParticleSystemRenderer || mf.GetComponentInParent<Collider>() != null) continue;
                var ruta = mf.transform; bool enCiudad = false;
                for (var t = ruta; t != null; t = t.parent) if (t.name == "Ciudad") { enCiudad = true; break; }
                if (!enCiudad) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size;
                c++;
            }
            Agregados += c;
            if (c > 0) SP.Core.GameLog.Line($"Colisiones de la ciudad: {c} props macizos con collider");
            return c;
        }
    }
}
