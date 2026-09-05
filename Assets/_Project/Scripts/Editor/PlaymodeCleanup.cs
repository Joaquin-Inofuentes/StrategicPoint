using UnityEditor;
using UnityEngine;
using SP.Presentation;

namespace SP.EditorTools
{
    // BUG REAL: decals (agujeros de bala, crateres) y escombros creados en
    // Play mode quedaban manchando la escena despues de frenar. Ambos pools
    // usan hideFlags DontSaveInEditor|DontSaveInBuild -- eso evita que se
    // ESCRIBAN al guardar la escena, pero no garantiza que Unity los
    // destruya al salir de Play si algo mas corre entre medio. Se limpian
    // a mano, en el momento exacto de la transicion, en vez de confiar en
    // que el descarte automatico llegue a tiempo.
    //
    // SEGUNDA VUELTA -- lo que el arreglo anterior no cubria. ClearAll()
    // recorre las colecciones ESTATICAS del pool, y esas colecciones se
    // vacian solas en el domain reload al entrar a Play. Todo lo que
    // quedaba vivo de una sesion anterior se volvia inalcanzable: objetos
    // con hideFlags DontSave, sin escena (gameObject.scene.name vacio) y
    // sin nadie que los recordara. Y como sus MATERIALES si se liberan
    // (un material de runtime no lo referencia ningun asset), quedaban
    // renderers con sharedMaterial nulo, que Unity dibuja en MAGENTA.
    //
    // Eso era el confeti rosa que aparecia sobre el mapa: 104 renderers
    // huerfanos de ImpactFxPool, OrderMarkerPool y DebrisPool. No era un
    // shader roto -- ninguna busqueda de materiales rotos los encontraba,
    // porque el material ya no existia -- ni tenia que ver con el arte
    // nuevo (se veia igual con toda la carpeta Arte desactivada).
    //
    // Por eso el barrido de abajo busca por NOMBRE DE RAIZ con
    // Resources.FindObjectsOfTypeAll, que es lo unico que alcanza a un
    // objeto sin escena y sin referencias: no depende de que ningun static
    // haya sobrevivido.
    [InitializeOnLoad]
    public static class PlaymodeCleanup
    {
        // Raices que crean los pools en runtime. Todas se regeneran solas
        // la proxima vez que alguien las necesita, asi que borrarlas fuera
        // de Play no pierde nada.
        static readonly string[] RaicesDePool =
        {
            "DecalPool",
            "DebrisPool",
            "ImpactFxPool",
            "OrderMarkerPool",
            "MuzzleLightPool",
            "SelectionRing",
            "EntityStateDebugView",
        };

        static PlaymodeCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Tambien al abrir el proyecto: si quedaron huerfanos de la
            // sesion pasada, se van antes de que nadie saque una captura.
            EditorApplication.delayCall += BarrerHuerfanos;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode &&
                state != PlayModeStateChange.EnteredEditMode) return;

            // Antes faltaban los dos pools de ImpactFx: sus roots eran los
            // unicos que nadie destruia nunca, y se acumulaban de a uno por
            // sesion de Play. Limpiar() los cubre a los cuatro.
            SP.Presentation.LimpiezaDeEscena.Limpiar();
            BarrerHuerfanos();
        }

        public static void BarrerHuerfanos()
        {
            if (EditorApplication.isPlaying) return;

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.transform.parent != null) continue;
                if (AssetDatabase.Contains(go)) continue;
                if (System.Array.IndexOf(RaicesDePool, go.name) < 0) continue;
                Object.DestroyImmediate(go);
            }
        }
    }
}
