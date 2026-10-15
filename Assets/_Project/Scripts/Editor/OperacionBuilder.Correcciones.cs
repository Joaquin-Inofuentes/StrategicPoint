using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Correcciones de los bugs #236 / #239 / #252 sobre SC_Operacion ya construida (se corre despues de RetocarEscenaAbierta):
    //   #252  "quita ese muro que obstaculiza feo": las vallas de tablones (14 m de largo x 3 m de alto) cruzaban las calles y tapaban la vista.
    //   #236  "revisa que las coberturas esten a escala": un soldado mide ~1,8 m y agachado ~1,1 m; los sacos (0,77 m) no lo tapaban, el cubo
    //         de concreto (0,86 m) tampoco, y los barriles eran de 1 m de ancho. Se llevan a escala real.
    //   #239  "no deberia tener collider el escombro de piso": craters y pilas de escombro bajo son solo decoracion (trababan a los soldados).
    public static partial class OperacionBuilder
    {
        public const float AlturaDeCoberturaBaja = 1.1f;

        public static string CorregirCoberturasEscenaAbierta()
        {
            var o = GameObject.Find("Operacion");
            if (o == null) return "no hay 'Operacion'";
            int tablones = 0, sacos = 0, concreto = 0, barriles = 0, colsSacados = 0;

            // #239: los prefabs de piso (crater y pilas de escombro) sin collider.
            foreach (var nombre in new[] { "P_Env_Crater", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B" })
            {
                var path = "Assets/_Project/Prefabs/ArteMundo/" + nombre + ".prefab";
                var raiz = PrefabUtility.LoadPrefabContents(path);
                var cols = raiz.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols) { Object.DestroyImmediate(c); colsSacados++; }
                if (cols.Length > 0) PrefabUtility.SaveAsPrefabAsset(raiz, path);
                PrefabUtility.UnloadPrefabContents(raiz);
            }

            var aBorrar = new List<GameObject>();
            foreach (var t in o.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("P_Env_Barricada_Tablones")) { aBorrar.Add(t.gameObject); continue; }
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;   // solo la raiz de cada prop (los hijos heredan la escala)
                if (n.StartsWith("P_Env_Barricada_Sacos") && !ObjetoYaCorregido(t)) { EscalarA(t, 1f, 1.4f, 1.5f); sacos++; }
                else if (n.StartsWith("P_Env_Barricada_Concreto") && !ObjetoYaCorregido(t)) { EscalarA(t, 1.2f, 1.2f, 1.2f); concreto++; }
                else if (n.StartsWith("P_Env_Barril") && !ObjetoYaCorregido(t))
                {
                    var b = BoundsDe(t);
                    if (b.size.x > 0.8f) { EscalarA(t, 0.62f, 1f, 0.62f); barriles++; }
                }
            }
            foreach (var g in aBorrar) { if (g != null) { Object.DestroyImmediate(g); tablones++; } }

            var activa = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activa);
            EditorSceneManager.SaveScene(activa);
            AssetDatabase.SaveAssets();
            return $"{tablones} vallas de tablones sacadas, {sacos} sacos / {concreto} cubos de concreto / {barriles} barriles a escala, {colsSacados} colliders de escombro sacados";
        }

        // Marca (nombre del hijo vacio) para no escalar dos veces si se corre el paso otra vez.
        const string MarcaDeEscala = "__escala_ok";
        static bool ObjetoYaCorregido(Transform t) => t.Find(MarcaDeEscala) != null;

        static void EscalarA(Transform t, float fx, float fy, float fz)
        {
            var s = t.localScale;
            t.localScale = new Vector3(s.x * fx, s.y * fy, s.z * fz);
            var m = new GameObject(MarcaDeEscala);
            m.transform.SetParent(t, false);
            m.hideFlags = HideFlags.None;
        }

        static Bounds BoundsDe(Transform t)
        {
            var b = new Bounds(t.position, Vector3.zero); bool hay = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name == MarcaDeEscala) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
