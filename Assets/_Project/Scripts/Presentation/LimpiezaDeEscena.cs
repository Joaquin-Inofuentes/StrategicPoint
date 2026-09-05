using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.Presentation
{
    // Del plan del usuario: "Al re cargar la escena no se limpian los
    // decals".
    //
    // Es cierto y no era solo de los decals. Los tres pools de efectos
    // cuelgan sus objetos de un root creado por codigo con
    // DontSaveInEditor|DontSaveInBuild. El comentario que hay en esos
    // pools da por sentado que asi el root SI se destruye al cargar una
    // escena (por eso no usan DontSave a secas). Medido en Play, con 40
    // decals pintados y un SceneManager.LoadScene de la misma escena:
    // los 40 seguian ahi y visibles, con scene.IsValid() == false. La
    // escena si se habia recargado -- los soldados volvieron a su punto
    // de partida con la vida llena y el enemigo muerto revivio -- asi que
    // el jugador reinicia la partida y se encuentra el piso con los
    // agujeros de bala de la partida anterior.
    //
    // Se limpia en UN solo lugar, por evento, y no dentro de cada pool:
    // los tres tienen el mismo problema por la misma razon, y tres
    // suscripciones sueltas es justo lo que se olvida de actualizar el
    // dia que aparezca un cuarto pool.
    public static class LimpiezaDeEscena
    {
        static bool enganchado;

        // Corre solo, sin necesidad de que ningun objeto de la escena se
        // acuerde de llamarlo: si dependiera de un componente, una escena
        // sin ese componente volveria al comportamiento viejo en silencio.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Enganchar()
        {
            if (enganchado) return;
            enganchado = true;
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        static void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            // Additive no reemplaza nada: borrar los efectos de la escena
            // que sigue viva seria peor que no limpiar.
            if (modo != LoadSceneMode.Single) return;
            Limpiar();
        }

        // Publico para poder medirlo y para que la suite pueda dejar el
        // terreno limpio entre fases sin recargar nada.
        public static int Limpiar()
        {
            int antes = ContarHuerfanos();
            DecalPool.ClearAll();
            DebrisPool.ClearAll();
            ImpactFx.ClearAll();
            // El cuarto pool. Medido: sin esto, 7 marcadores de orden de la
            // partida anterior quedaban pintados sobre el mapa nuevo.
            OrderMarkerFx.LimpiarTodo();
            BarrerRootsSueltos();
            return antes - ContarHuerfanos();
        }

        static readonly string[] NombresDeRoot =
        {
            "DecalPool", "DebrisPool", "ImpactFxPool", "ShockwaveRingPool",
            "OrderMarkerPool",
        };

        // Los ClearAll de arriba solo destruyen el root que el pool tiene
        // EN LA MANO. Los de sesiones anteriores ya no los conoce nadie, y
        // el barrido de huerfanos de cada pool mira los efectos, no los
        // roots vacios. Medido en este Editor: 25 roots ShockwaveRingPool
        // acumulados -- uno por sesion de Play -- con 38 anillos colgando.
        static void BarrerRootsSueltos()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || t.parent != null) continue;
                var go = t.gameObject;
                if (go.scene.IsValid()) continue;
                bool esRoot = false;
                for (int i = 0; i < NombresDeRoot.Length && !esRoot; i++)
                    if (t.name == NombresDeRoot[i]) esRoot = true;
                if (!esRoot) continue;
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
            }
        }

        // Cuenta lo que quedo fuera de toda escena: es exactamente la
        // medida del problema, y sirve de criterio de exito.
        public static int ContarHuerfanos()
        {
            int n = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null) continue;
                var go = t.gameObject;
                if (go.scene.IsValid()) continue;
                if (t.name.StartsWith("Decal_") || t.name.StartsWith("Debris")
                    || t.name.StartsWith("OrderMarker")
                    || t.name == "DecalPool" || t.name == "DebrisPool"
                    || t.name == "ImpactFxPool" || t.name == "ShockwaveRingPool"
                    || go.GetComponent<ImpactFx>() != null)
                    n++;
            }
            return n;
        }
    }
}
