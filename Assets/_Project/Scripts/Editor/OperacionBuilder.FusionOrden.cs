using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Orden de "Operacion/Fusionados": 350+ mallas combinadas que se llamaban 0001, 0002... Ahora cuelgan de un grupo por tipo
    // (Calles, Decals, Estructura, Aberturas, Luces, Metal), se llaman "<Material>_x<X>_z<Z>" (X, Z = esquina de la celda de 40 m) y los assets
    // viven en Generated/FusionOperacion/<Grupo>/. Cada grupo se guarda ademas como prefab en Prefabs/Escena/Fusionados y la escena queda conectada.
    // Es idempotente: se puede correr de nuevo despues de fusionar otra vez.
    public static partial class OperacionBuilder
    {
        const string CarpetaPrefabsFusion = "Assets/_Project/Prefabs/Escena/Fusionados";
        static readonly string[] GruposDeFusion = { "Calles", "Decals", "Estructura", "Aberturas", "Luces", "Metal", "Otros" };

        [MenuItem("Strategic Point/Arte/Ordenar Fusionados (SC_Operacion)")]
        static void OrdenarFusionadosMenu() => Debug.Log(OrdenarFusionadosEscenaAbierta());

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

        // Nombre legible de una malla fusionada: material sin prefijo + esquina de su celda (con signo).
        static string NombreDeFusion(string material, Vector3 centro, float celda = 40f)
        {
            int x = Mathf.FloorToInt(centro.x / celda) * (int)celda, z = Mathf.FloorToInt(centro.z / celda) * (int)celda;
            return $"{material.Replace("M_Op_", "")}_x{x}_z{z}";
        }

        static void AsegurarCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta) || Directory.Exists(ruta)) return;   // Directory.Exists: antes del Refresh la base aun no ve la carpeta recien creada y CreateFolder inventaba "Calles 2"
            string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
            AsegurarCarpeta(padre);
            AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
        }

        // Restos "Calles 2", "Calles 3"... de una corrida anterior con la carpeta ya existente: se borran si estan vacios.
        static void LimpiarCarpetasDuplicadasDeFusion()
        {
            foreach (var sub in AssetDatabase.GetSubFolders(CarpetaFusion))
            {
                string nombre = Path.GetFileName(sub);
                int espacio = nombre.LastIndexOf(' ');
                if (espacio <= 0 || !int.TryParse(nombre.Substring(espacio + 1), out _)) continue;
                if (System.Array.IndexOf(GruposDeFusion, nombre.Substring(0, espacio)) < 0) continue;
                if (AssetDatabase.FindAssets("", new[] { sub }).Length == 0) AssetDatabase.DeleteAsset(sub);
            }
        }

        public static string OrdenarFusionadosEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            var op = GameObject.Find("Operacion");
            var raiz = op != null ? op.transform.Find(NombreRaizFusion) : null;
            if (raiz == null) return "no hay Operacion/Fusionados";

            // 1) las mallas: todo hijo con MeshRenderer, a mano o ya agrupado
            var mallas = new List<(Transform t, MeshRenderer r)>();
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null && r.sharedMaterial != null) mallas.Add((r.transform, r));
            if (mallas.Count == 0) return "Fusionados no tiene mallas";

            // un prefab conectado no se puede reordenar por dentro: se desempaqueta antes
            foreach (var g in GruposDeFusion)
            {
                var existente = raiz.Find(g);
                if (existente != null && PrefabUtility.IsAnyPrefabInstanceRoot(existente.gameObject))
                    PrefabUtility.UnpackPrefabInstance(existente.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var grupos = new Dictionary<string, Transform>();
            Transform Grupo(string nombre)
            {
                if (grupos.TryGetValue(nombre, out var t)) return t;
                var ex = raiz.Find(nombre);
                if (ex == null) { ex = new GameObject(nombre).transform; ex.SetParent(raiz, false); }
                grupos[nombre] = ex; return ex;
            }

            var usados = new HashSet<string>();
            int renombradas = 0, movidasAsset = 0;
            var pendientes = new List<(Transform t, string grupo, string nombre, Mesh malla)>();
            foreach (var (t, r) in mallas)
            {
                string grupo = GrupoDeFusion(r.sharedMaterial.name);
                string baseNombre = NombreDeFusion(r.sharedMaterial.name, r.bounds.center), nombre = baseNombre;
                for (int i = 2; !usados.Add(grupo + "/" + nombre); i++) nombre = baseNombre + "_" + i;
                pendientes.Add((t, grupo, nombre, t.GetComponent<MeshFilter>().sharedMesh));
            }
            string errorMover = "";
            {
                foreach (var p in pendientes)
                {
                    string ruta = AssetDatabase.GetAssetPath(p.malla);
                    if (!string.IsNullOrEmpty(ruta) && ruta.StartsWith(CarpetaFusion))
                    {
                        string carpeta = CarpetaFusion + "/" + p.grupo;
                        AsegurarCarpeta(carpeta);
                        string destino = carpeta + "/" + p.nombre + ".asset";
                        if (ruta == destino) continue;
                        string err = AssetDatabase.MoveAsset(ruta, destino);
                        if (string.IsNullOrEmpty(err)) movidasAsset++; else if (errorMover.Length == 0) errorMover = err;
                    }
                }
            }
            LimpiarCarpetasDuplicadasDeFusion();
            AssetDatabase.Refresh();

            foreach (var p in pendientes)
            {
                p.t.name = p.nombre; renombradas++;
                p.t.SetParent(Grupo(p.grupo), false);
            }
            foreach (var g in grupos.Values)
            {
                var hijos = new List<Transform>(); foreach (Transform h in g) hijos.Add(h);
                hijos.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                for (int i = 0; i < hijos.Count; i++) hijos[i].SetSiblingIndex(i);
            }
            // grupos vacios fuera, y en orden fijo
            int orden = 0;
            foreach (var nombre in GruposDeFusion)
            {
                var g = raiz.Find(nombre); if (g == null) continue;
                if (g.childCount == 0) { Object.DestroyImmediate(g.gameObject); continue; }
                g.SetSiblingIndex(orden++);
            }

            // 2) prefabs por grupo, y la escena conectada a ellos
            AsegurarCarpeta(CarpetaPrefabsFusion);
            int prefabs = 0;
            foreach (var nombre in GruposDeFusion)
            {
                var g = raiz.Find(nombre); if (g == null) continue;
                PrefabUtility.SaveAsPrefabAssetAndConnect(g.gameObject, $"{CarpetaPrefabsFusion}/Fus_{nombre}.prefab", InteractionMode.AutomatedAction);
                prefabs++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            return $"Fusionados ordenado: {renombradas} mallas renombradas, {movidasAsset} assets movidos, {prefabs} prefabs en {CarpetaPrefabsFusion}" + (errorMover.Length > 0 ? " | error al mover: " + errorMover : "");
        }
    }
}
