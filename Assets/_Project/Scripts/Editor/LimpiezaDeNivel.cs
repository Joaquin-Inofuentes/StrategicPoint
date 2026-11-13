using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Retoques de escena del 2026-11-13 hechos por codigo (la escena SC_Operacion es un artefacto del OperacionBuilder y no se re-genera):
    //  - quitar TODAS las barricadas (jersey, sacos, erizos, vallas...) que bordean o cruzan la ruta del tanque;
    //  - llevar la fuente de la plaza al centro de la plaza.
    // Cada metodo devuelve un informe; los que modifican la escena la guardan. Se llaman por "unity command eval".
    public static class LimpiezaDeNivel
    {
        const string EscenaOperacion = "Assets/_Project/Scenes/SC_Operacion.unity";
        public const float DistanciaAlaRuta = 20f;
        static readonly string[] Patrones = { "barricada", "jersey", "erizo", "valla", "barrera" };

        static bool AbrirEscena(StringBuilder sb)
        {
            if (Application.isPlaying) { sb.AppendLine("FALLO el editor esta en Play"); return false; }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) { sb.AppendLine("FALLO hay una escena con cambios sin guardar: " + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path); return false; }
            // el nivel es una escena base + escenas aditivas por sector (Ruta, Ciudad, Cuarteles): se abren todas para ver y guardar cada una
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != EscenaOperacion) EditorSceneManager.OpenScene(EscenaOperacion, OpenSceneMode.Single);
            foreach (var extra in new[] { "SC_Operacion_Ruta", "SC_Operacion_Ciudad", "SC_Operacion_Cuarteles" })
            {
                string path = "Assets/_Project/Scenes/Operacion/" + extra + ".unity"; bool abierta = false;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path == path) abierta = true;
                if (!abierta) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            return true;
        }

        static bool EsBarricada(string n)
        {
            string m = n.ToLowerInvariant();
            foreach (var p in Patrones) if (m.Contains(p)) return true;
            return false;
        }

        static bool EnZonaProtegida(Transform t)
        {
            for (var q = t; q != null; q = q.parent)
            {
                string n = q.name;
                if (n.Contains("8_Mansion") || n.Contains("Cuartel") || n.Contains("PlazaCivica") || n.Contains("Mansion_")) return true;
            }
            return false;
        }

        static Vector3[] RutaDelNivel()
        {
            var l = new List<Vector3>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "RutaTanque") continue;
                for (int i = 0; i < t.childCount; i++) l.Add(t.GetChild(i).position);
            }
            return l.Count >= 2 ? l.ToArray() : null;
        }

        static float DistanciaPlano(Vector3 p, Vector3[] ruta)
        {
            float mejor = float.MaxValue; var pp = new Vector2(p.x, p.z);
            for (int i = 0; i < ruta.Length - 1; i++)
            {
                var a = new Vector2(ruta[i].x, ruta[i].z); var b = new Vector2(ruta[i + 1].x, ruta[i + 1].z);
                var ab = b - a; float k = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(pp - a, ab) / ab.sqrMagnitude) : 0f;
                mejor = Mathf.Min(mejor, Vector2.Distance(pp, a + ab * k));
            }
            return mejor;
        }

        // Objetos cuya propia nombre es de barricada y cuyo padre no lo es (la raiz de cada barricada), a menos de DistanciaAlaRuta de la ruta.
        static List<GameObject> Candidatas(Vector3[] ruta, out int protegidas)
        {
            protegidas = 0; var l = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !EsBarricada(t.name)) continue;
                if (t.parent != null && EsBarricada(t.parent.name)) continue;
                if (t.GetComponent<OperacionDirector>() != null) continue;
                if (DistanciaPlano(t.position, ruta) > DistanciaAlaRuta) continue;
                if (EnZonaProtegida(t)) { protegidas++; continue; }
                l.Add(t.gameObject);
            }
            return l;
        }

        public static string InspeccionarBarricadas()
        {
            var sb = new StringBuilder();
            if (!AbrirEscena(sb)) return sb.ToString();
            var ruta = RutaDelNivel(); if (ruta == null) return "FALLO sin ruta del tanque";
            var c = Candidatas(ruta, out int prot);
            var por = new SortedDictionary<string, int>();
            foreach (var g in c) { string n = System.Text.RegularExpressions.Regex.Replace(g.name, @"[_ ]?\d+$|\(Clone\)", ""); por[n] = por.TryGetValue(n, out int k) ? k + 1 : 1; }
            sb.AppendLine($"ruta de {ruta.Length} puntos; barricadas a menos de {DistanciaAlaRuta} m: {c.Count} (protegidas por zona: {prot})");
            foreach (var kv in por) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            return sb.ToString();
        }

        public static string QuitarBarricadasDeLaRuta()
        {
            var sb = new StringBuilder();
            if (!AbrirEscena(sb)) return sb.ToString();
            var ruta = RutaDelNivel(); if (ruta == null) return "FALLO sin ruta del tanque";
            var c = Candidatas(ruta, out int prot);
            int n = 0;
            foreach (var g in c) if (g != null) { Object.DestroyImmediate(g); n++; }
            EditorSceneManager.MarkAllScenesDirty();   // las barricadas viven en escenas aditivas distintas de la activa: hay que marcarlas todas o no se guardan
            EditorSceneManager.SaveOpenScenes();
            sb.AppendLine($"quitadas {n} barricadas de la ruta (protegidas {prot}); escena guardada");
            sb.Append(InspeccionarBarricadas());
            return sb.ToString();
        }

        // ---------------------------------------------------------------- fuente
        static bool EsPiezaDeFuente(string n) => n.StartsWith("Fuente_") || n.StartsWith("Luz_Fuente") || n.StartsWith("Foco_Fuente") || n.StartsWith("Neblina_Fuente") || n.StartsWith("Banco_Fuente");

        public static string InspeccionarFuente()
        {
            var sb = new StringBuilder();
            if (!AbrirEscena(sb)) return sb.ToString();
            var plaza = GameObject.Find("Plaza");
            sb.AppendLine("Plaza (centro del director): " + (plaza != null ? plaza.transform.position.ToString() : "no encontrada"));
            int n = 0; Bounds b = new Bounds(); bool primero = true;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!EsPiezaDeFuente(t.name) || (t.parent != null && EsPiezaDeFuente(t.parent.name))) continue;
                n++; if (primero) { b = new Bounds(t.position, Vector3.zero); primero = false; } else b.Encapsulate(t.position);
            }
            sb.AppendLine($"piezas de fuente: {n}; caja de posiciones: centro {b.center} tamano {b.size}");
            // lo que hay en la plaza (radio 22 m del centro 322,228): nombres agrupados
            var por = new SortedDictionary<string, int>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var p = t.position; if (new Vector2(p.x - 322f, p.z - 228f).magnitude > 22f || t.childCount > 0 && t.GetComponent<Renderer>() == null) continue;
                if (t.GetComponent<Renderer>() == null) continue;
                string nombre = System.Text.RegularExpressions.Regex.Replace(t.name, @"[_ ]?\d+$|\(Clone\)", "");
                por[nombre] = por.TryGetValue(nombre, out int k) ? k + 1 : 1;
            }
            foreach (var kv in por) sb.AppendLine($"  {kv.Key}: {kv.Value}");
            return sb.ToString();
        }

        // Pedido 2026-11-13: "la fuente de agua que este en el centro de la plaza y limpia un poco". El OperacionBuilder la dejo al sur del centro (322, 215,5) para que el centro
        // fuera el puesto de la defensa; ahora se lleva al centro (322, 228) con sus chorros, luces y neblina, se quita el desorden suelto del medio de la plaza y se le pone un
        // obstaculo de navegacion para que nadie planee caminos a traves de la taza.
        public const float CentroX = 322f, CentroZ = 228f;
        public static string CentrarFuente()
        {
            var sb = new StringBuilder();
            if (!AbrirEscena(sb)) return sb.ToString();
            Transform cuenco = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.name == "Fuente_Cuenco") cuenco = t;
            if (cuenco == null) return "FALLO no hay Fuente_Cuenco";
            float dz = CentroZ - cuenco.position.z, dx = CentroX - cuenco.position.x;
            int movidas = 0;
            if (Mathf.Abs(dz) > 0.01f || Mathf.Abs(dx) > 0.01f)
            {
                var off = new Vector3(dx, 0f, dz); float zViejo = cuenco.position.z;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    bool fuente = EsPiezaDeFuente(t.name) && !(t.parent != null && EsPiezaDeFuente(t.parent.name));
                    bool bancoLateral = t.name == "Banco" && Mathf.Abs(t.position.z - zViejo) < 1.6f && Mathf.Abs(t.position.x - CentroX) < 8f;
                    if (!fuente && !bancoLateral) continue;
                    // las piezas hechas con Caja() llevan la geometria en el mesh con el transform en el origen: se compara por la caja de sus renderers
                    t.position += off; movidas++;
                }
            }
            // obstaculo de navegacion de la taza (radio 3 m)
            var ob = cuenco.GetComponent<UnityEngine.AI.NavMeshObstacle>(); if (ob == null) ob = cuenco.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            ob.carving = true; ob.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule; ob.radius = 0.55f; ob.height = 4f; ob.center = Vector3.zero;
            // limpieza: lo suelto del medio de la plaza (cajas, neumaticos, palets, escombros, carteles, sacos) en 9 m a la redonda de la fuente
            int quitados = 0; var nombres = new SortedDictionary<string, int>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                string n = t.name;
                bool desorden = n.StartsWith("SM_Env_") || n.StartsWith("P_Env_") || n.StartsWith("GRP_Env_") || n == "Saco_Plaza";
                if (!desorden || (t.parent != null && (t.parent.name.StartsWith("SM_Env_") || t.parent.name.StartsWith("P_Env_") || t.parent.name.StartsWith("GRP_Env_")))) continue;
                var p = t.position; if (new Vector2(p.x - CentroX, p.z - CentroZ).magnitude > 9f) continue;
                nombres[n] = nombres.TryGetValue(n, out int k) ? k + 1 : 1; Object.DestroyImmediate(t.gameObject); quitados++;
            }
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            sb.AppendLine($"fuente al centro: {movidas} piezas movidas ({dx:0.0}, {dz:0.0}); desorden quitado {quitados}; escenas guardadas");
            foreach (var kv in nombres) sb.AppendLine($"  quitado {kv.Key}: {kv.Value}");
            return sb.ToString();
        }
    }
}
