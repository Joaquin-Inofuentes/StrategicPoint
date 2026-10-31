using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Orden del ENTORNO de SC_Operacion. Todo lo estatico y solo visual cuelga de UNA raiz ("Environment"), asi se apaga y se prende de un solo toque:
    //
    //   Environment
    //     Cuarteles   (z < 45: aproximacion, cuartel, muralla, centro de datos, patio del tanque)
    //     Ruta        (z >= 45 hasta la ciudad)
    //     Ciudad      (x >= 190 y z >= 100)
    //     Mundo       (lo que no es de un sector: suelo, sol, terreno ambiente, perimetro, horizonte)
    //
    // Dentro de cada sector: "Fusionados/<Tipo>" (las mallas combinadas: Calles, Estructura, Aberturas, Luces, Metal, Decals), mas los grupos de arte
    // que no tienen ningun script. Lo interactuable y dinamico (soldados, enemigos, vehiculos, demolibles, puertas, torretas, pools) NO se toca:
    // (los grupos conservan su nombre original porque los builders y los checks los buscan por nombre) sigue en Operacion/, PlayerSquad, Enemies, Vehicles... Solo se mueve un grupo si NINGUN hijo suyo tiene un MonoBehaviour de juego.
    // Idempotente. Cada sector guarda sus mallas fusionadas como prefab (Prefabs/Escena/Fusionados/Fus_<Sector>.prefab).
    public static partial class OperacionBuilder
    {
        const string CarpetaPrefabsFusion = "Assets/_Project/Prefabs/Escena/Fusionados";
        const string RaizDelEntorno = "Environment";
        static readonly string[] SectoresDelEntorno = { "Cuarteles", "Ruta", "Ciudad" };
        static readonly string[] GruposDeFusion = { "Calles", "Decals", "Estructura", "Aberturas", "Luces", "Metal", "Otros" };

        [MenuItem("Strategic Point/Arte/Ordenar Entorno (SC_Operacion)")]
        static void OrdenarEntornoMenu() => Debug.Log(OrdenarEntornoEscenaAbierta());

        [MenuItem("Strategic Point/Arte/Ordenar Fusionados (SC_Operacion)")]
        static void OrdenarFusionadosMenu() => Debug.Log(OrdenarFusionadosEscenaAbierta());

        public static string SectorDelEntorno(Vector3 p)
        {
            if (p.z < 45f) return "Cuarteles";
            if (p.x >= 190f && p.z >= 100f) return "Ciudad";
            return "Ruta";
        }

        static string GrupoDeFusion(string material)
        {
            string m = material.Replace("M_Op_", "");
            if (m.StartsWith("Decal")) return "Decals";
            switch (m)
            {
                case "Asfalto": case "Amarillo": case "Blanco": case "Cemento": return "Calles";
                case "Hormigon": case "Techo": case "TechoOcre": case "Casa": case "PisoTrinchera": return "Estructura";
                case "Ventana": case "VentanaApagada": case "Puerta": return "Aberturas";
                case "Lampara": case "RackLuz": case "Pantalla": return "Luces";
                case "MetalClaro": case "MetalOsc": case "Negro": return "Metal";
                default: return "Otros";
            }
        }

        // Nombre legible de una malla fusionada: material sin prefijo + esquina de su celda de 40 m (con signo).
        static string NombreDeFusion(string material, Vector3 centro, float celda = 40f)
        {
            int x = Mathf.FloorToInt(centro.x / celda) * (int)celda, z = Mathf.FloorToInt(centro.z / celda) * (int)celda;
            return $"{material.Replace("M_Op_", "")}_x{x}_z{z}";
        }

        static Transform Hijo(Transform padre, string nombre)
        {
            var t = padre.Find(nombre);
            if (t == null) { t = new GameObject(nombre).transform; t.SetParent(padre, false); }
            return t;
        }

        static void AsegurarCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta) || Directory.Exists(ruta)) return;   // Directory.Exists: antes del Refresh CreateFolder inventaba "Calles 2"
            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            AsegurarCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        // Mueve conservando la posicion. Un hijo de una instancia de prefab no se puede reparentar: se desempaqueta solo la capa externa
        // (los prefabs anidados, como P_Esc_Edificio_N, siguen conectados) y se reintenta.
        static bool Mover(Transform t, Transform nuevoPadre)
        {
            if (t.parent == nuevoPadre) return true;
            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
            {
                var raiz = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (raiz != null) PrefabUtility.UnpackPrefabInstance(raiz, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }
            t.SetParent(nuevoPadre, true);
            return t.parent == nuevoPadre;
        }

        // Solo visual: ningun hijo tiene un componente de juego (se admiten los modificadores de NavMesh y la data de luz de URP).
        static bool EsSoloVisual(Transform t)
        {
            foreach (var m in t.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m == null) return false;
                string n = m.GetType().Name;
                if (n == "NavMeshModifier" || n == "NavMeshModifierVolume" || n == "UniversalAdditionalLightData") continue;
                return false;
            }
            return t.GetComponentInChildren<Rigidbody>(true) == null && t.GetComponentInChildren<Animator>(true) == null;
        }

        static Vector3 CentroDe(Transform t)
        {
            bool ini = false; var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { if (!ini) { b = r.bounds; ini = true; } else b.Encapsulate(r.bounds); }
            return b.center;
        }

        static Transform RaizEntorno()
        {
            var env = GameObject.Find(RaizDelEntorno);
            return env != null ? env.transform : null;
        }

        // Las mallas combinadas: por sector (segun donde estan) y por tipo, con nombre material + zona.
        public static string OrdenarFusionadosEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            var env = RaizEntorno();
            if (env == null) return "no hay Environment";
            var op = GameObject.Find("Operacion");

            var fuentes = new List<Transform>();
            var viejo = op != null ? op.transform.Find(NombreRaizFusion) : null;
            if (viejo != null) fuentes.Add(viejo);
            foreach (var s in SectoresDelEntorno) { var sec = env.Find(s); var f = sec != null ? sec.Find(NombreRaizFusion) : null; if (f != null) fuentes.Add(f); }
            if (fuentes.Count == 0) return "no hay mallas fusionadas";

            // un prefab conectado no deja reordenar sus hijos: se desempaqueta primero (se vuelve a armar al final)
            foreach (var f in fuentes)
                foreach (var t in f.GetComponentsInChildren<Transform>(true))
                    if (t != null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var mallas = new List<MeshRenderer>();
            foreach (var f in fuentes)
                foreach (var r in f.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null && r.sharedMaterial != null) mallas.Add(r);

            var usados = new HashSet<string>();
            int movidas = 0;
            foreach (var r in mallas)
            {
                string sector = SectorDelEntorno(r.bounds.center), grupo = GrupoDeFusion(r.sharedMaterial.name);
                string baseNombre = NombreDeFusion(r.sharedMaterial.name, r.bounds.center), nombre = baseNombre;
                for (int i = 2; !usados.Add(sector + "/" + grupo + "/" + nombre); i++) nombre = baseNombre + "_" + i;
                var destino = Hijo(Hijo(Hijo(env, sector), NombreRaizFusion), grupo);
                r.transform.name = nombre;
                if (Mover(r.transform, destino)) movidas++;
            }

            // orden fijo y limpieza de grupos vacios (incluida la vieja Operacion/Fusionados)
            foreach (var s in SectoresDelEntorno)
            {
                var sec = env.Find(s); var f = sec != null ? sec.Find(NombreRaizFusion) : null; if (f == null) continue;
                int orden = 0;
                foreach (var g in GruposDeFusion)
                {
                    var gt = f.Find(g); if (gt == null) continue;
                    if (gt.childCount == 0) { Object.DestroyImmediate(gt.gameObject); continue; }
                    var hijos = new List<Transform>(); foreach (Transform h in gt) hijos.Add(h);
                    hijos.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    for (int i = 0; i < hijos.Count; i++) hijos[i].SetSiblingIndex(i);
                    gt.SetSiblingIndex(orden++);
                }
            }
            if (viejo != null && viejo.GetComponentsInChildren<MeshRenderer>(true).Length == 0) Object.DestroyImmediate(viejo.gameObject);

            // prefabs: uno por sector (los viejos, uno por tipo, se retiran)
            AsegurarCarpeta(CarpetaPrefabsFusion);
            foreach (var g in GruposDeFusion) AssetDatabase.DeleteAsset($"{CarpetaPrefabsFusion}/Fus_{g}.prefab");
            int prefabs = 0;
            foreach (var s in SectoresDelEntorno)
            {
                var sec = env.Find(s); var f = sec != null ? sec.Find(NombreRaizFusion) : null; if (f == null) continue;
                PrefabUtility.SaveAsPrefabAssetAndConnect(f.gameObject, $"{CarpetaPrefabsFusion}/Fus_{s}.prefab", InteractionMode.AutomatedAction);
                prefabs++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            return $"Fusionados: {movidas} mallas repartidas por sector, {prefabs} prefabs";
        }

        // Todo el entorno en una raiz: sectores + Mundo, sin tocar lo interactuable.
        public static string OrdenarEntornoEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            var env = RaizEntorno();
            if (env == null) return "no hay Environment";
            var sb = new System.Text.StringBuilder();

            foreach (var s in SectoresDelEntorno) Hijo(env, s);
            var mundo = Hijo(env, "Mundo");
            // el suelo y el sol de siempre pasan a Mundo
            foreach (var n in new[] { "Ground", "SunLight" }) { var t = env.Find(n); if (t != null) t.SetParent(mundo, true); }

            sb.AppendLine(OrdenarFusionadosEscenaAbierta());

            // Grupos enteros que van a un lugar fijo (solo si son puramente visuales).
            var fijos = new (string ruta, string sector, string nombre)[]
            {
                ("9_Servidores", "Cuarteles", "9_Servidores"),
                ("ArteSalidaNorte", "Cuarteles", "ArteSalidaNorte"),
                ("Terreno_Ambiente", "Mundo", "Terreno_Ambiente"),
                ("Operacion/0_Perimetro", "Mundo", "0_Perimetro"),
                ("Operacion/8_Horizonte", "Mundo", "8_Horizonte"),
                ("Operacion/7_Arte/PropsDeLaRuta", "Ruta", "PropsDeLaRuta"),
                ("Operacion/9_ArteExtra/Arboledas", "Ruta", "Arboledas"),
                ("Operacion/9_ArteExtra/Crateres", "Ruta", "Crateres"),
            };
            foreach (var f in fijos)
            {
                var g = GameObject.Find(f.ruta);
                if (g == null) { sb.AppendLine("  (ya movido o no existe) " + f.ruta); continue; }
                if (!EsSoloVisual(g.transform)) { sb.AppendLine("  SE QUEDA (tiene scripts de juego): " + f.ruta); continue; }
                g.name = f.nombre;
                if (Mover(g.transform, Hijo(env, f.sector))) sb.AppendLine("  -> " + f.sector + "/" + f.nombre); else sb.AppendLine("  NO SE PUDO MOVER: " + f.ruta);
            }

            // Los edificios sueltos del detalle: cada uno al sector donde esta.
            var detalle = GameObject.Find("Operacion/7_Arte/DetalleDeEdificios");
            if (detalle != null)
            {
                var hijos = new List<Transform>(); foreach (Transform h in detalle.transform) hijos.Add(h);
                int movidos = 0, quedan = 0;
                foreach (var h in hijos)
                {
                    if (!EsSoloVisual(h)) { quedan++; continue; }
                    if (Mover(h, Hijo(Hijo(env, SectorDelEntorno(CentroDe(h))), "Edificios"))) movidos++; else quedan++;
                }
                if (detalle.transform.childCount == 0) Object.DestroyImmediate(detalle);
                sb.AppendLine($"  DetalleDeEdificios: {movidos} piezas a su sector, {quedan} se quedan");
            }

            // Restos de pruebas de camara dejados sueltos en la escena.
            foreach (var go in escena.GetRootGameObjects())
                if ((go.name == "TmpCam" || go.name == "TmpShot" || go.name == "Tmp") && go.transform.childCount == 0 && go.GetComponents<Component>().Length <= 2)
                { sb.AppendLine("  borrado resto: " + go.name); Object.DestroyImmediate(go); }

            // orden de la raiz: sectores, Mundo
            int orden = 0;
            foreach (var s in SectoresDelEntorno) env.Find(s).SetSiblingIndex(orden++);
            mundo.SetSiblingIndex(orden);
            foreach (var s in SectoresDelEntorno) { var sec = env.Find(s); if (sec.childCount == 0) Object.DestroyImmediate(sec.gameObject); }

            EditorSceneManager.MarkSceneDirty(escena);
            return sb.ToString();
        }
    }
}
