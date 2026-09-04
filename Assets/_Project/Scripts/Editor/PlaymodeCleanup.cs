using UnityEditor;
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
    [InitializeOnLoad]
    static class PlaymodeCleanup
    {
        static PlaymodeCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            DecalPool.ClearAll();
            DebrisPool.ClearAll();
        }
    }
}
