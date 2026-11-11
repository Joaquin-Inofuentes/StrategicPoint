using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Pulido del nivel 2 (SC_Operacion + 3 sectores aditivos), 2026-11-11. Corre con las 4 escenas abiertas. Todo es IDEMPOTENTE y se guarda
    // como cambio de la escena (nada al vuelo en runtime):
    //   1. Duplicados: apaga en la escena la malla (y collider) repetida de piezas puestas dos veces en el mismo lugar.
    //   2. Colliders: da cuerpo a props que se veian macizos pero se atravesaban (rocas, casitas, tanques de agua, arcos, obeliscos, pedestales,
    //      autos de bloqueo, anillos de sacos, canteros, cactus).
    //   3. Titulos: apaga los rotulos con nombre de objeto/zona (PUESTO, ANTIAEREA, GENERADOR, AYUNTAMIENTO, RADIO DE CAMPAÑA...). Quedan los
    //      carteles de ruta (CIUDAD >>, PLAZA >>, BIENVENIDOS A) y los de CARGA PORTON (solo aparecen con la mecha encendida).
    public static class PulidoNivel2Nov11
    {
        static readonly string[] NombresConCuerpo =
        {
            "Roca", "Casita", "TanqueDeAgua", "Arco_Pilar", "Arco_Base", "Obelisco", "Obelisco_Base", "Pedestal", "Fuentecita_Cuenco",
            "Cabina", "Saco_Anillo", "Saco_Anillo_Alto", "Cantero", "Topiario", "Cactus",
        };
        static readonly string[] RotulosFuera = { "Rotulo_PUESTO", "Rotulo_ANTIAEREA", "Rotulo_GENERADOR", "Rotulo_SECTOR", "Rotulo_AYUNTAMIENTO", "Rotulo_RADIO" };

        static string Ruta(Transform t) { var s = t.name; int g = 0; while (t.parent != null && g++ < 6) { t = t.parent; s = t.name + "/" + s; } return s; }

        [MenuItem("Strategic Point/Operacion/Pulido nivel 2 (11-nov): duplicados, colliders y titulos")]
        public static string Correr()
        {
            var sb = new StringBuilder();
            int dup = Duplicados(sb);
            int col = Colliders(sb);
            int tit = Titulos(sb);
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded) EditorSceneManager.MarkSceneDirty(s);
            }
            EditorSceneManager.SaveOpenScenes();
            sb.Insert(0, $"duplicados apagados={dup} colliders nuevos={col} titulos apagados={tit}\n");
            System.IO.File.WriteAllText("Temp/pulido_nivel2.txt", sb.ToString());
            return sb.ToString().Split('\n')[0];
        }

        struct Clave : System.IEquatable<Clave>
        {
            public int malla, material; public Vector3Int pos, esc;
            public bool Equals(Clave o) => malla == o.malla && material == o.material && pos == o.pos && esc == o.esc;
            public override bool Equals(object o) => o is Clave k && Equals(k);
            public override int GetHashCode() { unchecked { return ((malla * 397 ^ material) * 397 ^ pos.GetHashCode()) * 397 ^ esc.GetHashCode(); } }
        }
        static Vector3Int Q(Vector3 v, float p) => new Vector3Int(Mathf.RoundToInt(v.x / p), Mathf.RoundToInt(v.y / p), Mathf.RoundToInt(v.z / p));
        static int H(object o) => o == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);

        static int Duplicados(StringBuilder sb)
        {
            int n = 0;
            var vistos = new Dictionary<Clave, MeshRenderer>();
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (mr == null || !mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                if (mr.GetComponentInParent<Rigidbody>() != null || mr.GetComponentInParent<Animator>() != null) continue;
                if (mr.GetComponentInParent<SP.Actors.Soldier>() != null || mr.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                var k = new Clave { malla = H(mf.sharedMesh), material = H(mr.sharedMaterial), pos = Q(mr.transform.position, 0.15f), esc = Q(mr.transform.lossyScale, 0.1f) };
                if (!vistos.TryGetValue(k, out var primero)) { vistos[k] = mr; continue; }
                // la copia: se apaga el renderer y, si el collider es de la misma pieza, tambien
                Undo.RecordObject(mr, "dup"); mr.enabled = false;
                var c = mr.GetComponent<Collider>(); if (c != null && c.enabled) { Undo.RecordObject(c, "dup"); c.enabled = false; }
                n++; sb.AppendLine("DUP apagado " + Ruta(mr.transform) + " @" + mr.transform.position.ToString("F1"));
            }
            return n;
        }

        static int Colliders(StringBuilder sb)
        {
            int n = 0;
            var set = new HashSet<string>(NombresConCuerpo);
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (mr == null) continue;
                string nombre = mr.name;
                // los renderers de Auto_Quemado_n_Cabina y Casita/Muro_Puerta tambien
                bool coincide = set.Contains(nombre) || (nombre.StartsWith("Auto_Quemado_") && nombre.EndsWith("_Cabina"));
                if (!coincide) continue;
                if (mr.GetComponentInParent<SP.Actors.Soldier>() != null || mr.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                if (mr.GetComponent<Collider>() != null) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                var bc = Undo.AddComponent<BoxCollider>(mr.gameObject);
                float fx = 1f, fz = 1f;
                if (nombre == "Cactus") { fx = 0.3f; fz = 0.3f; }          // solo el tronco, no los brazos
                else if (nombre == "Roca") { fx = 0.85f; fz = 0.85f; }
                bc.center = b.center; bc.size = new Vector3(b.size.x * fx, b.size.y, b.size.z * fz);
                n++;
            }
            sb.AppendLine("COLLIDERS nuevos: " + n);
            return n;
        }

        static int Titulos(StringBuilder sb)
        {
            int n = 0;
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
            {
                if (tm == null) continue;
                bool fuera = false;
                for (var p = tm.transform; p != null && !fuera; p = p.parent)
                    foreach (var pre in RotulosFuera) if (p.name.StartsWith(pre, System.StringComparison.Ordinal)) { fuera = true; break; }
                if (!fuera) continue;
                var r = tm.GetComponent<MeshRenderer>();
                if (r != null && r.enabled) { Undo.RecordObject(r, "titulo"); r.enabled = false; n++; sb.AppendLine("TITULO apagado '" + tm.text + "' " + Ruta(tm.transform)); }
            }
            return n;
        }
    }
}
