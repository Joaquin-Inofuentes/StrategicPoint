using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Cilindro que aparece en el punto de una orden y se achica con un lerp
    // hasta desaparecer. El color indica qué tipo de orden fue: mover,
    // atacar o subir a un vehículo. Puramente cosmético, no afecta lógica.
    public class OrderMarkerFx : MonoBehaviour
    {
        public static readonly Color MoveColor = new Color(0.35f, 0.85f, 0.35f);
        public static readonly Color AttackColor = new Color(0.92f, 0.2f, 0.18f);
        public static readonly Color MountColor = new Color(0.25f, 0.55f, 0.95f);

        static bool shaderWarmed;

        // orderIndex 0 = orden inmediata (marcador normal, se desvanece).
        // 1..n = posicion en la cola planificada: el marcador se queda
        // fijo hasta que ese tramo se cumple, y se dibuja con tantas
        // marcas verticales como su numero de orden -- con varios puntos
        // encolados los marcadores eran indistinguibles entre si y no se
        // podia leer la secuencia planificada.
        public static void Spawn(Vector3 position, Color color, int orderIndex, float duration = 0.6f)
        {
            if (orderIndex <= 0) { Spawn(position, color, duration); return; }
            var marker = SpawnCylinder(position, color);
            marker.transform.localScale = new Vector3(1.1f, 0.05f, 1.1f);
            marker.name = $"OrderMarker_Queued_{orderIndex}";
            for (int i = 0; i < orderIndex; i++)
            {
                var pip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var pipCol = pip.GetComponent<Collider>();
                if (pipCol != null) { if (Application.isPlaying) Object.Destroy(pipCol); else Object.DestroyImmediate(pipCol); }
                pip.transform.SetParent(marker.transform, false);
                // El padre esta aplastado en Y (0.05), asi que una altura
                // util en el mundo pide una escala local enorme en Y.
                pip.transform.localScale = new Vector3(0.12f, 12f, 0.12f);
                pip.transform.localPosition = new Vector3((i - (orderIndex - 1) * 0.5f) * 0.22f, 6f, 0f);
                pip.GetComponent<MeshRenderer>().sharedMaterial = marker.GetComponent<MeshRenderer>().sharedMaterial;
            }
            QueuedMarkers.Add(marker);
        }

        // Los marcadores de cola no se autodestruyen: representan un plan
        // todavia pendiente. Los limpia quien cancela o consume la orden.
        public static readonly List<GameObject> QueuedMarkers = new List<GameObject>();

        public static void ClearQueuedMarkers()
        {
            foreach (var m in QueuedMarkers)
            {
                if (m == null) continue;
                if (Application.isPlaying) Destroy(m);
                else DestroyImmediate(m);
            }
            QueuedMarkers.Clear();
        }

        static GameObject SpawnCylinder(Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "OrderMarker";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.transform.position = new Vector3(position.x, 0.05f, position.z);
            go.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            rend.sharedMaterial = mat;
            return go;
        }

        public static void Spawn(Vector3 position, Color color, float duration = 0.6f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "OrderMarker";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            // Siempre a nivel del piso, sin importar la altura del punto de
            // origen (un ataque usa la posición del pecho del enemigo, subir
            // usa el centro del vehículo -- ninguno es "el suelo").
            go.transform.position = new Vector3(position.x, 0.05f, position.z);
            go.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            rend.sharedMaterial = mat;

            var fx = go.AddComponent<OrderMarkerFx>();
            fx.StartCoroutine(fx.LerpAndDie(duration));
        }

        // La primera vez que se pinta un cilindro con este shader, Unity
        // compila esa variante y el frame se traba (a veces sale directamente
        // negro si justo se saca una captura ahí). Se precalienta uno, bien
        // lejos y chiquito, apenas se arma el nivel, para que la primera
        // orden real del jugador ya encuentre el shader listo.
        public static void Prewarm()
        {
            if (shaderWarmed) return;
            shaderWarmed = true;
            Spawn(new Vector3(0f, -500f, 0f), MoveColor, 0.05f);

            // El Spawn de arriba confia en la corrutina LerpAndDie para
            // limpiarse, pero las corrutinas NO corren en Edit mode: el
            // marcador de precalentamiento quedaba serializado en la escena
            // en (0,-500,0) para siempre. Invisible, pero es basura que se
            // arrastra commit a commit. AttackLineManager.Prewarm y
            // OrderLineManager.Prewarm ya hacen esta limpieza explicita.
            if (!Application.isPlaying) DestroyPrewarmLeftovers();
        }

        static void DestroyPrewarmLeftovers()
        {
            foreach (var go in Object.FindObjectsByType<OrderMarkerFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null) Object.DestroyImmediate(go.gameObject);
        }

        IEnumerator LerpAndDie(float duration)
        {
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                transform.localScale = Vector3.Lerp(startScale, new Vector3(0f, startScale.y, 0f), k);
                yield return null;
            }

            // El test automático (HeadlessTestRunner) dispara órdenes en Edit
            // mode, donde Destroy() tira warning y no libera de una: hay que
            // usar DestroyImmediate fuera de Play mode.
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
