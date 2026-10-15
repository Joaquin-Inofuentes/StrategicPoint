using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pulido de rendimiento de SC_Operacion: la ciudad tiene ~13.000 renderers y casi ninguno era estatico (9.361 draw calls en el Profiler).
    // Todo lo que no se mueve ni se rompe se marca BatchingStatic, asi Unity lo funde en pocas mallas al cargar la escena.
    //
    // "Dinamico" = cualquier cosa bajo un componente propio del juego (ObstacleMarker: se demuele; torretas, luces, cargas, rampas...),
    // un Rigidbody, un Animator, una luz o un TextMesh. Los componentes de NavMesh no cuentan: solo marcan zonas.
    public static partial class OperacionBuilder
    {
        public static int MarcarEstaticos(Transform raiz)
        {
            if (raiz == null) return 0;
            int marcados = 0;
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is SkinnedMeshRenderer || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponent<TextMesh>() != null) continue;
                if (EsDinamico(r.transform, raiz)) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0) continue;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.BatchingStatic);
                marcados++;
            }
            return marcados;
        }

        static bool EsDinamico(Transform t, Transform raiz)
        {
            for (; t != null && t != raiz; t = t.parent)   // la raiz lleva el director y el texto de mundo: no cuenta
            {
                if (t.GetComponent<Rigidbody>() != null || t.GetComponent<Animator>() != null || t.GetComponent<Light>() != null) return true;
                foreach (var m in t.GetComponents<MonoBehaviour>())
                {
                    if (m == null) continue;
                    string n = m.GetType().Name;
                    if (n.StartsWith("NavMesh") || n == "UniversalAdditionalLightData") continue;
                    return true;
                }
            }
            return false;
        }

        // Para la escena abierta (sin reconstruir): marca y guarda. Devuelve cuantos renderers marco.
        public static string MarcarEstaticosEscenaAbierta()
        {
            var activa = EditorSceneManager.GetActiveScene();
            var o = GameObject.Find("Operacion");
            if (o == null) return "no hay un objeto 'Operacion' en " + activa.name;
            int n = MarcarEstaticos(o.transform);
            if (n > 0) { EditorSceneManager.MarkSceneDirty(activa); EditorSceneManager.SaveScene(activa); }
            return $"{n} renderers marcados BatchingStatic en {activa.name}";
        }
    }
}
