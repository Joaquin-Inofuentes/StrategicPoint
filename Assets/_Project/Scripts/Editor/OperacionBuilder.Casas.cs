using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    public static partial class OperacionBuilder
    {
        // Bug #296 ("se rompio la geometria del edificio, repararlo"): las 16 casas de la ciudad (Casa_1..Casa_16) tenian el MeshRenderer apagado
        // (una limpieza de "mallas duplicadas" hecha por eval sobre la escena binaria), asi que solo se veia el vestido de PulidoFinal y de ArteOp
        // (pilastras, cornisa, ventanas) flotando, sin el cuerpo de la casa. Metodo idempotente: vuelve a prender el render del cuerpo de toda
        // casa que tenga collider y no dibuje. Devuelve un resumen. No toca Casa_*_Arte (contenedores vacios) ni ningun otro grupo.
        public static string RepararCasasSinCuerpoEscenaAbierta()
        {
            var sb = new StringBuilder();
            int reparadas = 0, ok = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                var n = t.name;
                if (!n.StartsWith("Casa_") || n.EndsWith("_Arte")) continue;
                if (t.parent == null || t.parent.name != "6_Ciudad") continue;
                var mr = t.GetComponent<MeshRenderer>(); var mf = t.GetComponent<MeshFilter>(); var col = t.GetComponent<BoxCollider>();
                if (mr == null || mf == null || col == null) continue;
                bool roto = !mr.enabled || mr.forceRenderingOff || mr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || mf.sharedMesh == null || mr.sharedMaterial == null || !t.gameObject.activeSelf;
                if (!roto) { ok++; continue; }
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                if (mf.sharedMesh == null) mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                mr.enabled = true; mr.forceRenderingOff = false; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                EditorUtility.SetDirty(mr); EditorUtility.SetDirty(mf); EditorUtility.SetDirty(t.gameObject);
                reparadas++; sb.Append(n).Append(' ');
            }
            if (reparadas > 0) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return $"Casas reparadas={reparadas} ya_ok={ok} :: {sb}";
        }
    }
}
