using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SP.EditorTools
{
    // Reparte los ELEMENTOS ENTEROS de Operacion (SC_Operacion) en las escenas de sector (SC_Operacion_<Sector>), siempre como prefab.
    //
    // Un elemento viaja si: tiene algo que dibujar (render o luz), no tiene ningun componente de juego salvo el ObstacleMarker (lo que se
    // derrumba y bloquea, que se registra solo al activarse), y ningun script de la escena lo apunta ni el apunta a nada de afuera.
    // Todo lo demas (torres, cargas, generador, bunkers, marcadores del director, terminal) se queda en SC_Operacion porque Unity no
    // serializa referencias entre escenas. Un contenedor con componente propio no se abre: sus piezas se rompen con el.
    //
    // Si el elemento ya es una instancia de prefab se mueve tal cual; si no, se crea su prefab en Prefabs/Escena/Elementos (los
    // iguales comparten prefab) y queda enlazado. Necesita las 3 escenas del entorno abiertas (Abrir Operacion completa).
    public static partial class OperacionBuilder
    {
        const string CarpetaElementos = "Assets/_Project/Prefabs/Escena/Elementos";

        [MenuItem("Strategic Point/Arte/Escenas del Entorno/Distribuir elementos de Operacion en sectores")]
        static void DistribuirElementosMenu() => Debug.Log(DistribuirElementosEnSectores());

        static bool ComponenteQueViaja(string tipo) => tipo == "ObstacleMarker" || tipo == "NavMeshModifier" || tipo == "NavMeshModifierVolume" || tipo == "UniversalAdditionalLightData";

        static bool EsComponenteDeJuegoQueSeQueda(Component c)
        {
            if (c == null) return false;
            if (c is Transform || c is MeshFilter || c is Renderer || c is Collider || c is Light) return false;
            if (c is MonoBehaviour) { string n = c.GetType().Name; return !ComponenteQueViaja(n) && n != "UniversalAdditionalCameraData"; }
            return c is Rigidbody || c is Animator || c is Canvas || c is AudioSource || c is ParticleSystem;
        }

        // Dos elementos con la misma firma (jerarquia, componentes, mallas, materiales, luces) comparten prefab.
        static string FirmaDeElemento(Transform raiz)
        {
            var sb = new StringBuilder();
            void Rec(Transform t, bool esRaiz)
            {
                sb.Append(esRaiz ? "#" : t.name).Append('|');
                if (!esRaiz) sb.Append(t.localPosition.ToString("0.00")).Append(t.localEulerAngles.ToString("0")).Append(t.localScale.ToString("0.00"));
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null) { sb.Append("null;"); continue; }
                    sb.Append(c.GetType().Name);
                    if (c is Light l) sb.Append($"[{l.type},{l.color},{l.intensity:0.00},{l.range:0.0},{l.spotAngle:0},{l.shadows}]");
                    if (c is MeshFilter mf && mf.sharedMesh != null) sb.Append("[" + mf.sharedMesh.name + "]");
                    if (c is Renderer r) foreach (var m in r.sharedMaterials) sb.Append("{" + (m != null ? m.name : "-") + "}");
                    sb.Append(';');
                }
                foreach (Transform h in t) Rec(h, false);
                sb.Append('/');
            }
            Rec(raiz, true);
            return sb.ToString();
        }

        static Vector3 CentroDeElemento(Transform t)
        {
            bool ini = false; var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { if (r is ParticleSystemRenderer) continue; if (!ini) { b = r.bounds; ini = true; } else b.Encapsulate(r.bounds); }
            return b.center;
        }

        public static string DistribuirElementosEnSectores()
        {
            var baseEsc = SceneManager.GetSceneByPath(EscenaBase);
            if (!baseEsc.IsValid() || !baseEsc.isLoaded) return "abri SC_Operacion primero";
            foreach (var s in SectoresDelEntorno)
            {
                var e = SceneManager.GetSceneByPath(RutaEscenaDeSector(s));
                if (!e.IsValid() || !e.isLoaded) return "faltan las escenas del entorno abiertas: usa 'Abrir Operacion completa'";
            }
            var op = GameObject.Find("Operacion");
            if (op == null) return "no hay Operacion";
            AsegurarCarpeta(CarpetaElementos);

            // referencias entre objetos de una misma escena: (quien apunta, a quien apunta)
            var refs = new List<(Transform dueno, Transform objetivo)>();
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (m == null) continue;
                var so = new SerializedObject(m); var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var o = it.objectReferenceValue; var g = o as GameObject; if (g == null && o is Component c) g = c.gameObject;
                    if (g != null && g.scene == m.gameObject.scene) refs.Add((m.transform, g.transform));
                }
            }
            bool Aislado(Transform n)
            {
                foreach (var (dueno, objetivo) in refs) if (dueno.IsChildOf(n) != objetivo.IsChildOf(n)) return false;
                return true;
            }
            bool SoloViaja(Transform n) { foreach (var c in n.GetComponentsInChildren<Component>(true)) if (EsComponenteDeJuegoQueSeQueda(c)) return false; return true; }
            bool PropioDeJuegoORef(Transform n)
            {
                foreach (var c in n.GetComponents<Component>()) if (EsComponenteDeJuegoQueSeQueda(c)) return true;
                foreach (var (_, objetivo) in refs) if (objetivo == n) return true;
                return false;
            }

            var elementos = new List<(Transform t, string origen)>();
            void Buscar(Transform nodo, string origen)
            {
                var hijos = new List<Transform>(); foreach (Transform h in nodo) hijos.Add(h);
                foreach (var h in hijos)
                {
                    if (h.name == NombreRaizFusion || h.name == "Computadora") continue;   // el terminal se busca por hijos (Mesa, Monitor)
                    bool dibuja = h.GetComponentInChildren<Renderer>(true) != null || h.GetComponentInChildren<Light>(true) != null;
                    if (!dibuja) continue;
                    if (SoloViaja(h) && Aislado(h))
                    {
                        // un contenedor propio de mas de 60 m (no prefab) reparte arte de varios sectores: se abre y viaja cada elemento
                        var caja = new Bounds(h.position, Vector3.zero); foreach (var r in h.GetComponentsInChildren<Renderer>(true)) caja.Encapsulate(r.bounds);
                        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(h.gameObject) && h.childCount > 1 && Mathf.Max(caja.size.x, caja.size.z) > 60f) Buscar(h, origen);
                        else elementos.Add((h, origen));
                    }
                    else if (!PropioDeJuegoORef(h) && h.childCount > 0) Buscar(h, origen);
                }
            }
            foreach (Transform g in op.transform) { if (g.name == "Reservas") continue; Buscar(g, g.name); }

            var env = RaizEntorno();
            var porFirma = new Dictionary<string, GameObject>(); var nombresUsados = new Dictionary<string, int>();
            int yaPrefab = 0, prefabsNuevos = 0, reutilizados = 0, fallos = 0;
            var porSector = new SortedDictionary<string, int>();
            var log = new StringBuilder();

            var padresOriginales = new List<Transform>();
            foreach (var (t, origen) in elementos)
            {
                if (t == null) continue;
                if (t.parent != null && !padresOriginales.Contains(t.parent)) padresOriginales.Add(t.parent);
                string sector = SectorDelEntorno(CentroDeElemento(t));
                var destino = Hijo(Sec(env, sector) ?? SecH(env, sector), origen);
                bool esPrefab = PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject);
                if (esPrefab)
                {
                    if (Mover(t, destino)) { yaPrefab++; porSector[sector] = (porSector.TryGetValue(sector, out var a) ? a : 0) + 1; } else fallos++;
                    continue;
                }
                string firma = FirmaDeElemento(t);
                if (porFirma.TryGetValue(firma, out var existente))
                {
                    // mismo elemento ya convertido: se instancia el prefab en su lugar
                    if (!Mover(t, destino)) { fallos++; continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(existente, destino.gameObject.scene);
                    inst.transform.SetParent(destino, false);
                    inst.transform.localPosition = t.localPosition; inst.transform.localRotation = t.localRotation; inst.transform.localScale = t.localScale;
                    inst.name = t.name; inst.layer = t.gameObject.layer; inst.SetActive(t.gameObject.activeSelf);
                    Object.DestroyImmediate(t.gameObject);
                    reutilizados++;
                }
                else
                {
                    if (!Mover(t, destino)) { fallos++; continue; }
                    string baseNombre = "P_Op_" + t.name.Replace(" ", "_");
                    nombresUsados.TryGetValue(baseNombre, out int n); nombresUsados[baseNombre] = n + 1;
                    string ruta = $"{CarpetaElementos}/{baseNombre}{(n > 0 ? "_" + (n + 1) : "")}.prefab";
                    while (File.Exists(ruta)) { n++; nombresUsados[baseNombre] = n + 1; ruta = $"{CarpetaElementos}/{baseNombre}_{n + 1}.prefab"; }
                    string nombreOriginal = t.name;
                    var asset = PrefabUtility.SaveAsPrefabAssetAndConnect(t.gameObject, ruta, InteractionMode.AutomatedAction);
                    if (asset == null) { fallos++; log.AppendLine("  no se pudo crear prefab de " + nombreOriginal); continue; }
                    t.name = nombreOriginal;
                    porFirma[firma] = asset; prefabsNuevos++;
                }
                porSector[sector] = (porSector.TryGetValue(sector, out var b) ? b : 0) + 1;
            }

            // solo los grupos que quedaron vacios por esta distribucion (los marcadores vacios del director no se tocan)
            int vacios = 0;
            foreach (var p0 in padresOriginales)
            {
                var p = p0;
                while (p != null && p != op.transform && p.childCount == 0 && p.GetComponents<Component>().Length == 1 && !PrefabUtility.IsPartOfPrefabInstance(p.gameObject))
                {
                    bool referenciado = false; foreach (var (_, objetivo) in refs) if (objetivo == p) { referenciado = true; break; }
                    if (referenciado) break;
                    var sup = p.parent; Object.DestroyImmediate(p.gameObject); vacios++; p = sup;
                }
            }

            foreach (var s in SectoresDelEntorno) EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneByPath(RutaEscenaDeSector(s)));
            EditorSceneManager.MarkSceneDirty(baseEsc);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            var res = new StringBuilder();
            res.AppendLine($"Elementos distribuidos: {elementos.Count} (ya eran prefab: {yaPrefab}; prefabs nuevos: {prefabsNuevos}; instancias de un prefab nuevo: {reutilizados}; fallos: {fallos}); grupos vacios borrados: {vacios}");
            foreach (var kv in porSector) res.AppendLine($"  {kv.Key}: {kv.Value}");
            res.Append(log);
            File.WriteAllText("Temp/distribuir_resultado.txt", res.ToString());
            return res.ToString();
        }
    }
}
