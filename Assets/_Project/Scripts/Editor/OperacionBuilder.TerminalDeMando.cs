using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SP.EditorTools
{
    // Bug #315 ("mejora la geometria de aqui"): la terminal del Centro de Datos tenia un monitor rojo de 3,2 x 1,5 m y tres paneles holo
    // flotando sobre un poste. Ahora es un puesto de mando a escala humana: monitor central con bisel y pie, dos monitores laterales, un
    // teclado-consola y una pantalla superior enmarcada entre dos postes. Mesa, Torre_Servidor y BarraProgreso no se tocan (colliders y
    // referencias de OperacionTerminal). Idempotente: rehace "Computadora/ArteTerminal".
    public static partial class OperacionBuilder
    {
        [MenuItem("Strategic Point/Arte/Terminal de mando (SC_Operacion)")]
        static void TerminalDeMandoMenu() => Debug.Log(TerminalDeMandoEscenaAbierta());

        static GameObject CajaTM(Transform p, string n, Vector3 pos, Vector3 s, Material m, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = n; go.transform.SetParent(p, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f); go.transform.localScale = s;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static string TerminalDeMandoEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            Transform grupo = null, mesa = null, monitor = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.gameObject.scene != escena) continue;
                if (t.name == "Computadora" && t.Find("Mesa") != null) { grupo = t; mesa = t.Find("Mesa"); monitor = t.Find("Monitor"); }
            }
            if (grupo == null) return "no esta Computadora/Mesa";
            var viejo = grupo.Find("ArteTerminal");
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

            // paneles flotantes viejos
            int borrados = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.gameObject.scene == escena && t.parent != null && t.parent.name == "Terminal" && t.parent.parent != null && t.parent.parent.name == "CentroDeDatos"
                    && (t.name == "Panel_Central" || t.name == "Panel_Izq" || t.name == "Panel_Der" || t.name == "Soporte_Central"))
                { Object.DestroyImmediate(t.gameObject); borrados++; }

            var arte = new GameObject("ArteTerminal").transform;
            arte.SetParent(grupo, false);
            arte.localPosition = Vector3.zero; arte.localRotation = Quaternion.identity; arte.localScale = Vector3.one;

            var negro = Negro;
            var metal = Mat("RackMetal", new Color(0.10f, 0.12f, 0.16f));
            var azul = Mat("PantallaAzul", new Color(0.35f, 0.75f, 1f), 1.8f);
            var holo = Mat("HoloCian", new Color(0.2f, 0.75f, 1f), 1.4f);
            var tecla = Mat("TeclaTenue", new Color(0.2f, 0.9f, 1f), 0.8f);

            float mz = mesa.position.z, top = mesa.position.y + mesa.lossyScale.y * 0.5f;   // cara superior de la mesa
            float zMon = mz + 0.6f;

            // monitor central (el existente, referenciado por OperacionTerminal.luz): mas chico, con bisel y pie
            if (monitor != null)
            {
                monitor.position = new Vector3(0f, top + 0.7f, zMon);
                monitor.localScale = new Vector3(2.4f, 1.0f, 0.08f);
            }
            CajaTM(arte, "Bisel_Central", new Vector3(0f, top + 0.7f, zMon + 0.06f), new Vector3(2.6f, 1.2f, 0.1f), negro);
            CajaTM(arte, "Pie_Central", new Vector3(0f, top + 0.15f, zMon + 0.08f), new Vector3(0.25f, 0.3f, 0.25f), negro);

            // monitores laterales
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * 1.75f;
                CajaTM(arte, "Bisel_Lateral", new Vector3(x, top + 0.55f, zMon + 0.02f), new Vector3(1.05f, 0.8f, 0.08f), negro, s * -20f);
                var p = CajaTM(arte, "Pantalla_Lateral", new Vector3(x - s * 0.02f, top + 0.55f, zMon - 0.03f), new Vector3(0.95f, 0.7f, 0.04f), azul, s * -20f);
                CajaTM(arte, "Pie_Lateral", new Vector3(x, top + 0.1f, zMon + 0.05f), new Vector3(0.2f, 0.2f, 0.2f), negro);
            }

            // teclado-consola en el borde frontal
            CajaTM(arte, "Consola", new Vector3(0f, top + 0.04f, mz - 0.35f), new Vector3(2.0f, 0.06f, 0.5f), negro);
            CajaTM(arte, "Consola_Teclas", new Vector3(0f, top + 0.075f, mz - 0.35f), new Vector3(1.8f, 0.01f, 0.32f), tecla);

            // pantalla superior enmarcada entre dos postes (sobre la plataforma, detras de la mesa)
            Transform plataforma = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Plataforma" && t.gameObject.scene == escena) { plataforma = t; break; }
            float cx = plataforma != null ? plataforma.position.x : 0f;
            float baseY = plataforma != null ? plataforma.position.y + plataforma.lossyScale.y * 0.5f : 0.15f;
            float zS = mz + 1.1f;
            foreach (float s in new[] { -1f, 1f })
                CajaTM(arte, "Poste", new Vector3(cx + s * 1.7f, (baseY + 3.8f) * 0.5f, zS), new Vector3(0.12f, 3.8f - baseY, 0.12f), metal);
            CajaTM(arte, "Travesano", new Vector3(cx, 3.8f, zS), new Vector3(3.5f, 0.12f, 0.12f), metal);
            CajaTM(arte, "Marco_Superior", new Vector3(cx, 3.1f, zS + 0.03f), new Vector3(3.2f, 1.4f, 0.05f), negro);
            CajaTM(arte, "Pantalla_Superior", new Vector3(cx, 3.1f, zS - 0.01f), new Vector3(3.0f, 1.2f, 0.04f), holo);

            var mod = arte.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.ignoreFromBuild = true; mod.applyToChildren = true;
            EditorUtility.SetDirty(arte.gameObject);
            EditorSceneManager.MarkSceneDirty(escena);
            return $"ArteTerminal: {arte.childCount} piezas, paneles viejos borrados={borrados}, mesa top={top:0.00} z={mz:0.00}";
        }
    }
}
