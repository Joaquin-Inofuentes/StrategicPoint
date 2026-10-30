using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Bug #309: arte del portón blindado (dos hojas de acero con costillas, cruz, banda de peligro, remaches, cerradura y dintel).
    // Se corre sobre la escena ABIERTA y es idempotente: reemplaza "Porton_Blindado_Arte", hermano del bloqueo invisible `Porton_Blindado`
    // (que conserva su collider y su referencia serializada). El arte no lleva colliders.
    public static partial class OperacionBuilder
    {
        [MenuItem("Strategic Point/Arte/Portón blindado (SC_Operacion)")]
        static void PortonBlindadoMenu() => Debug.Log(PortonBlindadoEscenaAbierta());

        static GameObject CajaPB(Transform p, string n, Vector3 local, Vector3 s, Material m, float rotZ = 0f, bool sombra = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = n; go.transform.SetParent(p, false);
            go.transform.localPosition = local; go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ); go.transform.localScale = s;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = sombra ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static string PortonBlindadoEscenaAbierta()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "SC_Operacion") return "escena no soportada: " + escena.name;
            var porton = GameObject.Find("Porton_Blindado");
            if (porton == null)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == "Porton_Blindado" && t.gameObject.scene == escena) { porton = t.gameObject; break; }
            }
            if (porton == null) return "no esta Porton_Blindado";
            var viejo = GameObject.Find("Porton_Blindado_Arte");
            if (viejo != null) Object.DestroyImmediate(viejo);

            // Se apaga el dibujo del bloqueo (queda el collider) y de sus hijos decorativos.
            foreach (var r in porton.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var pt = porton.transform;
            float ancho = Mathf.Abs(pt.lossyScale.x), alto = Mathf.Abs(pt.lossyScale.y);
            float mitad = ancho * 0.5f;
            var arte = new GameObject("Porton_Blindado_Arte");
            arte.transform.SetParent(pt.parent, false);
            arte.transform.SetPositionAndRotation(pt.position, pt.rotation);   // escala 1: las hojas giran sin deformarse
            var a = arte.transform;

            var acero = Mat("PortonAcero", new Color(0.20f, 0.22f, 0.25f));
            var acero2 = Mat("PortonCostilla", new Color(0.30f, 0.32f, 0.35f));
            var negro = Negro;
            var amarillo = Mat("PortonPeligroAmarillo", new Color(0.95f, 0.72f, 0.08f));
            var luzRoja = Mat("PortonLuzRoja", new Color(1f, 0.12f, 0.08f), 2.5f);

            float hoja = mitad - 0.1f;          // ancho de cada hoja
            float espesor = 0.45f;
            float altoHoja = alto - 0.1f;
            Renderer cerradura = null;
            Transform izq = null, der = null;

            for (int lado = -1; lado <= 1; lado += 2)
            {
                var pivote = new GameObject(lado < 0 ? "Hoja_Izq" : "Hoja_Der").transform;
                pivote.SetParent(a, false);
                pivote.localPosition = new Vector3(lado * (mitad - 0.05f), 0f, 0f);
                if (lado < 0) izq = pivote; else der = pivote;
                float s = -lado;               // la hoja se extiende hacia el centro desde la bisagra
                float cx = s * hoja * 0.5f;
                CajaPB(pivote, "Panel", new Vector3(cx, 0f, 0f), new Vector3(hoja, altoHoja, espesor), acero);
                for (int cara = -1; cara <= 1; cara += 2)
                {
                    float z = cara * (espesor * 0.5f + 0.05f);
                    for (int i = 0; i < 4; i++)
                        CajaPB(pivote, "Costilla", new Vector3(cx, -altoHoja * 0.5f + 0.6f + i * (altoHoja - 1.2f) / 3f, z), new Vector3(hoja - 0.4f, 0.22f, 0.12f), acero2);
                    float diag = Mathf.Atan2(altoHoja - 1.2f, hoja - 0.4f) * Mathf.Rad2Deg;
                    float largo = Mathf.Sqrt((hoja - 0.4f) * (hoja - 0.4f) + (altoHoja - 1.2f) * (altoHoja - 1.2f));
                    CajaPB(pivote, "Cruz_A", new Vector3(cx, 0f, z), new Vector3(largo, 0.18f, 0.1f), acero2, diag, false);
                    CajaPB(pivote, "Cruz_B", new Vector3(cx, 0f, z), new Vector3(largo, 0.18f, 0.1f), acero2, -diag, false);
                    // banda de peligro inferior en bloques de 0,5 m
                    int bloques = Mathf.FloorToInt(hoja / 0.5f);
                    for (int b = 0; b < bloques; b++)
                        CajaPB(pivote, "Peligro", new Vector3(s * (0.25f + b * 0.5f), -altoHoja * 0.5f + 0.2f, z + cara * 0.01f), new Vector3(0.5f, 0.35f, 0.04f), b % 2 == 0 ? amarillo : negro, 0f, false);
                    // remaches por los bordes superior e inferior
                    for (int r = 0; r < Mathf.FloorToInt(hoja / 0.8f); r++)
                    {
                        float x = s * (0.4f + r * 0.8f);
                        CajaPB(pivote, "Remache", new Vector3(x, altoHoja * 0.5f - 0.15f, z), new Vector3(0.09f, 0.09f, 0.06f), negro, 0f, false);
                        CajaPB(pivote, "Remache", new Vector3(x, -altoHoja * 0.5f + 0.5f, z), new Vector3(0.09f, 0.09f, 0.06f), negro, 0f, false);
                    }
                }
                if (lado > 0)
                {
                    // cerradura central en el borde de encuentro (hoja derecha, hacia el centro)
                    CajaPB(pivote, "Cerradura_Placa", new Vector3(s * (hoja - 0.3f), 0f, -(espesor * 0.5f + 0.1f)), new Vector3(0.6f, 1.2f, 0.15f), negro, 0f, false);
                    var luz = CajaPB(pivote, "Cerradura_Luz", new Vector3(s * (hoja - 0.3f), 0.35f, -(espesor * 0.5f + 0.2f)), new Vector3(0.25f, 0.25f, 0.08f), luzRoja, 0f, false);
                    cerradura = luz.GetComponent<Renderer>();
                }
            }

            // marco: dintel; jambas solo si no hay ya un pilar del portico a menos de 1 m
            CajaPB(a, "Dintel", new Vector3(0f, alto * 0.5f + 0.3f, 0f), new Vector3(ancho + 0.8f, 0.6f, 2.4f), acero);
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var punto = pt.TransformPoint(new Vector3(lado * 0.5f * (ancho + 0.6f) / Mathf.Max(0.01f, ancho), 0f, 0f));
                bool hayPilar = false;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (t.name.StartsWith("P_Esc_Porton_Pilar") && Vector3.Distance(new Vector3(t.position.x, 0f, t.position.z), new Vector3(punto.x, 0f, punto.z)) < 1f) { hayPilar = true; break; }
                if (!hayPilar) CajaPB(a, "Jamba", new Vector3(lado * (mitad + 0.25f), 0f, 0f), new Vector3(0.5f, alto, 2.0f), acero);
            }

            // El horneado junta mallas visuales: el arte no debe entrar a la navegacion (el bloqueo invisible ya la cierra).
            var mod = arte.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            mod.ignoreFromBuild = true; mod.applyToChildren = true;
            var anim = arte.AddComponent<PortonBlindadoAnimado>();
            anim.hojaIzq = izq; anim.hojaDer = der; anim.cerradura = cerradura;
            EditorUtility.SetDirty(arte);
            EditorSceneManager.MarkSceneDirty(escena);
            return $"Porton_Blindado_Arte: {arte.GetComponentsInChildren<Renderer>(true).Length} piezas, ancho={ancho:0.0} alto={alto:0.0}";
        }
    }
}
