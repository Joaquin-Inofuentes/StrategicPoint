using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.EditorTools
{
    // El entorno de SC_Operacion repartido en tres escenas aditivas: SC_Operacion_Cuarteles, _Ruta y _Ciudad.
    // Cada una tiene como unica raiz el sector (Cuarteles / Ruta / Ciudad) con sus grupos adentro. SC_Operacion conserva lo dinamico
    // y el mundo comun (Environment/Mundo). En juego las carga SP.Core.CargadorDeEntorno.
    public static partial class OperacionBuilder
    {
        const string EscenaBase = "Assets/_Project/Scenes/SC_Operacion.unity";
        const string CarpetaEscenasEntorno = "Assets/_Project/Scenes/Operacion";

        public static string RutaEscenaDeSector(string sector) => $"{CarpetaEscenasEntorno}/SC_Operacion_{sector}.unity";

        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Separar en 3 escenas")]
        static void SepararMenu() => Debug.Log(SepararEntornoEnEscenas());

        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Abrir Operacion completa (3 sectores)")]
        static void AbrirCompletaMenu() => Debug.Log(AbrirOperacionCompleta());

        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Aislar Cuarteles")]
        static void AislarCuartelesMenu() => Debug.Log(AislarSector("Cuarteles"));
        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Aislar Ruta")]
        static void AislarRutaMenu() => Debug.Log(AislarSector("Ruta"));
        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Aislar Ciudad")]
        static void AislarCiudadMenu() => Debug.Log(AislarSector("Ciudad"));

        // Unity no serializa referencias entre escenas: un objeto del entorno al que apunta un script de SC_Operacion (el director,
        // por ejemplo) dejaria de estar enlazado al guardar. Antes de repartir, todo lo referenciado desde fuera del entorno vuelve a
        // SC_Operacion, dentro de Operacion/<grupo de origen>. Devuelve cuantos rescato.
        static int RescatarReferenciadosDesdeFuera(Transform env, StringBuilderLite log)
        {
            var sectores = new HashSet<Transform>(); foreach (var s in SectoresDelEntorno) { var t = env.Find(s); if (t != null) sectores.Add(t); }
            var objetivos = new List<GameObject>();
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (m == null || m.transform.IsChildOf(env)) continue;
                var so = new SerializedObject(m); var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var o = it.objectReferenceValue; var g = o as GameObject; if (g == null && o is Component c) g = c.gameObject;
                    if (g == null || !g.scene.IsValid() || !g.transform.IsChildOf(env)) continue;
                    var sec = g.transform; while (sec.parent != null && sec.parent != env) sec = sec.parent;
                    if (sectores.Contains(sec) && !objetivos.Contains(g)) objetivos.Add(g);
                }
            }
            var op = GameObject.Find("Operacion");
            int n = 0;
            foreach (var g in objetivos)
            {
                var grupo = g.transform; Transform origen = null;
                while (grupo.parent != null) { if (grupo.parent.parent != null && grupo.parent.parent.parent == env) origen = grupo.parent; grupo = grupo.parent; }
                string nombre = origen != null ? origen.name : "RescatadosDelEntorno";
                var destino = Hijo(op.transform, nombre);
                if (Mover(g.transform, destino)) { n++; log.Add("  rescatado: " + g.name + " -> Operacion/" + nombre); }
            }
            return n;
        }

        public class StringBuilderLite { readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(); public void Add(string s) => sb.AppendLine(s); public override string ToString() => sb.ToString(); }

        // Mueve cada sector de Environment a una escena propia y la guarda. Idempotente: si el sector ya es una escena, la deja.
        public static string SepararEntornoEnEscenas()
        {
            var baseEsc = SceneManager.GetSceneByPath(EscenaBase);
            if (!baseEsc.IsValid() || !baseEsc.isLoaded) return "abri SC_Operacion primero";
            SceneManager.SetActiveScene(baseEsc);
            var env = RaizEntorno();
            if (env == null) return "no hay Environment en SC_Operacion";
            AsegurarCarpeta(CarpetaEscenasEntorno);
            var sb = new System.Text.StringBuilder();
            var rescate = new StringBuilderLite();
            int rescatados = RescatarReferenciadosDesdeFuera(env, rescate);
            if (rescatados > 0) sb.AppendLine($"{rescatados} objetos referenciados por scripts volvieron a SC_Operacion:\n" + rescate);

            foreach (var sector in SectoresDelEntorno)
            {
                var raiz = env.Find(sector);
                if (raiz == null) { sb.AppendLine(sector + ": ya no esta en Environment"); continue; }
                string ruta = RutaEscenaDeSector(sector);
                Scene esc;
                if (File.Exists(ruta)) { esc = SceneManager.GetSceneByPath(ruta); if (!esc.IsValid() || !esc.isLoaded) esc = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive); }
                else esc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                int objetos = raiz.GetComponentsInChildren<Transform>(true).Length;
                raiz.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(raiz.gameObject, esc);
                bool ok = EditorSceneManager.SaveScene(esc, ruta);
                sb.AppendLine($"{sector}: {objetos} objetos -> {ruta} (guardada={ok})");
            }

            // Build Settings: las escenas del entorno tienen que poder cargarse por nombre
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var sector in SectoresDelEntorno)
            {
                string ruta = RutaEscenaDeSector(sector);
                if (!lista.Exists(e => e.path == ruta)) lista.Add(new EditorBuildSettingsScene(ruta, true));
            }
            EditorBuildSettings.scenes = lista.ToArray();

            // el cargador va en Systems (o en una raiz propia)
            if (Object.FindFirstObjectByType<SP.Core.CargadorDeEntorno>() == null)
            {
                var host = GameObject.Find("Systems");
                var go = host != null ? host : new GameObject("Systems");
                if (go.scene != baseEsc) SceneManager.MoveGameObjectToScene(go, baseEsc);
                go.AddComponent<SP.Core.CargadorDeEntorno>();
                sb.AppendLine("CargadorDeEntorno agregado a " + go.name);
            }

            EditorSceneManager.MarkSceneDirty(baseEsc);
            EditorSceneManager.SaveScene(baseEsc);
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        // SC_Operacion + las 3 escenas del entorno abiertas juntas (como en juego).
        public static string AbrirOperacionCompleta()
        {
            EditorSceneManager.OpenScene(EscenaBase, OpenSceneMode.Single);
            foreach (var sector in SectoresDelEntorno) EditorSceneManager.OpenScene(RutaEscenaDeSector(sector), OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(EscenaBase));
            return "abiertas SC_Operacion + " + string.Join(", ", SectoresDelEntorno);
        }

        // Solo la escena del sector (sin SC_Operacion ni las otras): para validar que se sostiene sola.
        public static string AislarSector(string sector)
        {
            EditorSceneManager.OpenScene(RutaEscenaDeSector(sector), OpenSceneMode.Single);
            return "aislada: " + sector;
        }
    }
}
