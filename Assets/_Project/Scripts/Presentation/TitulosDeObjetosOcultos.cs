using UnityEngine;

namespace SP.Presentation
{
    // Pulido del nivel 2 (2026-11-07): "titulos de zonas innecesarias / titulos de nombres de objeto". Los letreros de texto flotante de los puestos,
    // las torres antiaereas y el generador nombraban cosas que ya se leen por su forma. Al cargar el entorno se apagan SOLO esos textos (los
    // carteles de ruta «CIUDAD >>», «PLAZA >>» y «BIENVENIDOS A» se mantienen: orientan el recorrido). Los objetos quedan, solo se oculta el rotulo.
    public static class TitulosDeObjetosOcultos
    {
        public static int Ocultos { get; private set; }
        static readonly string[] Prefijos = { "Rotulo_PUESTO", "Rotulo_ANTIAEREA", "Rotulo_GENERADOR", "Rotulo_SECTOR" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Suscribir()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode modo)
        {
            if (s.name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) Aplicar();
        }

        static bool Corresponde(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                foreach (var pre in Prefijos) if (p.name.StartsWith(pre, System.StringComparison.Ordinal)) return true;
            return false;
        }

        public static int Aplicar()
        {
            int c = 0;
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
            {
                if (tm == null || !Corresponde(tm.transform)) continue;
                var r = tm.GetComponent<MeshRenderer>();
                if (r != null && r.enabled) { r.enabled = false; c++; }
            }
            Ocultos += c;
            if (c > 0) SP.Core.GameLog.Line($"Titulos de objetos ocultos: {c}");
            return c;
        }
    }
}
