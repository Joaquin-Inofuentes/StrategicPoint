using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;

namespace SP.EditorTools
{
    // Ronda 18: (1) el punto de arranque queda limpio de arboles y arbustos; (2) el muro sur desaparece y detras del arranque crece un bosque poblado;
    // (3) mas decoracion a lo largo del valle que recorre la escuadra. Idempotente: vuelve a correr sin duplicar (borra sus grupos R18_* antes de plantar).
    public static class BosqueDeInicioR18
    {
        public static readonly Vector2 Arranque = new Vector2(-2.2f, -373f);
        public const float RadioLimpio = 16f;
        const string DirArte = "Assets/_Project/Prefabs/ArteMundo/";

        static GameObject Prefab(string n) => AssetDatabase.LoadAssetAtPath<GameObject>(DirArte + n + ".prefab");
        static bool EsRaizDeVegetacion(Transform t) =>
            (t.name.StartsWith("P_Env_Arbol") || t.name.StartsWith("P_Env_Arbusto")) && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject);

        public static string Aplicar()
        {
            var sb = new StringBuilder();
            if (!EscenasR13.Asegurar(sb, false)) return sb.ToString();
            var escena = SceneManager.GetSceneByName("SC_Operacion_Cuarteles");
            if (!escena.IsValid()) return "no esta abierta SC_Operacion_Cuarteles";

            // 0) idempotencia
            foreach (var t in EscenasR13.Todos()) if (t != null && (t.name == "R18_BosqueSur" || t.name == "R18_DecoradoDelValle")) Object.DestroyImmediate(t.gameObject);

            // 1) claro de arranque
            int apagados = 0;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.gameObject.activeSelf || !EsRaizDeVegetacion(t)) continue;
                if ((new Vector2(t.position.x, t.position.z) - Arranque).magnitude > RadioLimpio) continue;
                t.gameObject.SetActive(false); EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); apagados++;
            }
            sb.AppendLine($"claro de arranque: {apagados} arboles/arbustos apagados en {RadioLimpio:0} m");

            // 2) el muro sur: se apaga el de la escena base (solo el dibujo, el limite fisico Borde_Sur sigue) y el Valle_Sur entero
            int muros = 0;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null) continue;
                if (t.name == "Borde_Sur") { var r = t.GetComponent<Renderer>(); if (r != null && r.enabled) { r.enabled = false; EscenasR13.Tocar(r); EscenasR13.Sucia(t.gameObject.scene); muros++; } }
                else if (t.name == "Valle_Sur" && t.gameObject.activeSelf) { t.gameObject.SetActive(false); EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); muros++; }
            }
            // el muro modular del cuartel que cierra el sur (P_Mod_Muro_* a z -384..-396): se apaga entero (el limite fisico lo da Borde_Sur)
            int modulos = 0;
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.gameObject.activeSelf || !(t.name.StartsWith("P_Mod_Muro") || t.name.StartsWith("P_Mod_Losa") || t.name.StartsWith("P_Mod_Valla")) || !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                if (t.position.z > -384f || t.position.z < -396f) continue;
                t.gameObject.SetActive(false); EscenasR13.Tocar(t.gameObject); EscenasR13.Sucia(t.gameObject.scene); modulos++;
            }
            sb.AppendLine($"muro modular sur: {modulos} modulos apagados");
            sb.AppendLine($"muro sur: {muros} piezas apagadas");

            var tipos = new[] { Prefab("P_Env_ArbolA"), Prefab("P_Env_ArbolB"), Prefab("P_Env_Arbusto_Grande"), Prefab("P_Env_Arbusto_Medio") };
            foreach (var p in tipos) if (p == null) { sb.AppendLine("faltan prefabs de arbol/arbusto"); return sb.ToString(); }
            var rnd = new System.Random(1801);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // 3) bosque poblado detras del arranque (z < -381, hasta el horizonte), solo fuera del claro
            var bosque = new GameObject("R18_BosqueSur"); SceneManager.MoveGameObjectToScene(bosque, escena);
            int nBosque = 0;
            void Plantar(Transform padre, float x, float z)
            {
                if ((new Vector2(x, z) - Arranque).magnitude < RadioLimpio) return;
                int ti = rnd.Next(100) < 68 ? rnd.Next(2) : 2 + rnd.Next(2);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(tipos[ti], padre);
                go.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale *= ti < 2 ? R(1.0f, 1.7f) : R(0.9f, 1.4f);
            }
            // franja jugable (entre Valle_Sur y Borde_Sur): densa
            for (float z = -381f; z >= -389f; z -= 2.6f) for (float x = -60f; x <= 60f; x += 2.8f) { Plantar(bosque.transform, x + R(-1.1f, 1.1f), z + R(-0.9f, 0.9f)); nBosque++; }
            // el bosque propiamente dicho, detras del limite: el visual que se ve desde el arranque y desde las alturas
            for (float z = -391f; z >= -450f; z -= 3.4f) for (float x = -95f; x <= 95f; x += 3.6f) { Plantar(bosque.transform, x + R(-1.5f, 1.5f), z + R(-1.4f, 1.4f)); nBosque++; }
            // los costados del valle, para que el bosque no termine en una pared recta
            for (float z = -381f; z >= -420f; z -= 3.4f) foreach (float lado in new[] { -1f, 1f }) for (float x = 62f; x <= 120f; x += 3.8f) { Plantar(bosque.transform, lado * x + R(-1.5f, 1.5f), z + R(-1.4f, 1.4f)); nBosque++; }
            sb.AppendLine($"bosque sur: {bosque.transform.childCount} plantas");

            // 4) decoracion del valle: de la entrada a la salida (z -365 a -45)
            var deco = new GameObject("R18_DecoradoDelValle"); SceneManager.MoveGameObjectToScene(deco, escena);
            string[] props = { "P_Env_Barril", "P_Env_Caja_Madera", "P_Env_Palet", "P_Env_Neumaticos", "P_Env_Escombro_Pila_A", "P_Env_Escombro_Pila_B", "P_Env_Auto_Quemado", "P_Env_Poste_Caido", "P_Env_Crater", "P_Env_Barricada_Sacos", "P_Env_Barricada_Tablones", "P_Env_Muro_Caido", "P_Env_Cartel", "P_Env_Farol" };
            float[] esc = { 1.0f, 1.2f, 1.0f, 1.0f, 0.9f, 0.9f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.1f };
            var soldados = new List<Vector3>();
            foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Include, FindObjectsSortMode.None)) soldados.Add(s.transform.position);
            bool CercaDeSoldado(Vector3 p) { foreach (var q in soldados) if ((q - p).sqrMagnitude < 25f) return true; return false; }
            int nProps = 0, nVeg = 0;
            Physics.SyncTransforms();
            for (int intento = 0; intento < 3500 && nProps + nVeg < 420; intento++)
            {
                float z = R(-362f, -45f);
                float x = R(-58f, 58f);
                if (Mathf.Abs(x) < 9f) continue;                                              // la calzada y sus bordes quedan libres
                if ((new Vector2(x, z) - Arranque).magnitude < RadioLimpio + 4f) continue;     // el claro de arranque sigue limpio
                var pos = new Vector3(x, 0f, z);
                if (CercaDeSoldado(pos)) continue;
                bool borde = Mathf.Abs(x) > 40f;
                if (borde && rnd.Next(100) < 60)
                {   // vegetacion en los bordes del valle
                    int ti = rnd.Next(100) < 55 ? rnd.Next(2) : 2 + rnd.Next(2);
                    float e = ti < 2 ? R(1.0f, 1.6f) : R(0.9f, 1.3f);
                    if (Physics.CheckBox(new Vector3(x, 1.25f, z), new Vector3(1.2f * e, 1.0f, 1.2f * e), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var v = (GameObject)PrefabUtility.InstantiatePrefab(tipos[ti], deco.transform);
                    v.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, R(0f, 360f), 0f)); v.transform.localScale *= e; nVeg++;
                    continue;
                }
                int pi = rnd.Next(props.Length);
                var pf = Prefab(props[pi]); if (pf == null) continue;
                float yaw = R(0f, 360f);
                if (Physics.CheckBox(new Vector3(x, 1.25f, z), new Vector3(1.6f * esc[pi], 1.0f, 1.6f * esc[pi]), Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, deco.transform);
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f)); go.transform.localScale = Vector3.one * esc[pi];
                var m = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>(); m.ignoreFromBuild = true;
                nProps++;
                Physics.SyncTransforms();
            }
            sb.AppendLine($"decoracion del valle: {nProps} props y {nVeg} plantas");

            EscenasR13.Sucia(escena);
            EscenasR13.Guardar(sb);
            return sb.ToString();
        }
    }
}
