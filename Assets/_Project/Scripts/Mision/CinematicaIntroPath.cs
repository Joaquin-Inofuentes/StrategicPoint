using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SP.Mision
{
    // Camino de la camara de la cinematica de apertura: una lista ORDENADA de CinematicaWaypoint,
    // hijos directos de este mismo GameObject. Pedido explicito: "usa un camino de emptys... y usa
    // iconos para esos gameobject emptys... y dime el nombre del gameobject padre para reubicarlos
    // mejor luego" y "una funcion llamable desde inspector para reconectar y con gizmos quiero ver
    // las lineas de la secuencia".
    //
    // Reubicar los hijos a mano en la Scene view (o agregar/borrar/reordenar) y despues apretar
    // "Reconectar waypoints" (boton de contexto, tres puntitos del componente en el Inspector) es
    // todo lo que hace falta: no hay que tocar este script ni CinematicaDeIntro.
    public class CinematicaIntroPath : MonoBehaviour
    {
        public CinematicaWaypoint[] Waypoints = new CinematicaWaypoint[0];

        [ContextMenu("Reconectar waypoints (hijos directos, en orden)")]
        public void Reconectar()
        {
            var lista = new List<CinematicaWaypoint>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var w = transform.GetChild(i).GetComponent<CinematicaWaypoint>();
                if (w != null) lista.Add(w);
            }
            Waypoints = lista.ToArray();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log($"[CinematicaIntroPath] Reconectados {Waypoints.Length} waypoints: " + string.Join(" -> ", lista.ConvertAll(w => w.name)));
#endif
        }

        static readonly Color ColorLinea = new Color(0.3f, 0.9f, 1f, 0.9f);
        static readonly Color ColorMira = new Color(1f, 0.85f, 0.25f, 0.55f);

        // Sin assets de icono (todo este proyecto se arma por codigo): un gizmo de "camara" dibujado
        // a mano con primitivas -- un cuerpo (cubo achatado) + un "lente" (esfera) apuntando hacia
        // adelante -- es suficiente para distinguir cada punto de camara de un waypoint cualquiera
        // en la Scene view, sin depender de un PNG en una carpeta Gizmos/.
        static void DibujarIconoCamara(Vector3 pos, Vector3 mira, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(pos, new Vector3(0.5f, 0.35f, 0.35f));
            var dir = (mira - pos).sqrMagnitude > 0.01f ? (mira - pos).normalized : Vector3.forward;
            Gizmos.DrawWireSphere(pos + dir * 0.35f, 0.18f);
        }

        void OnDrawGizmos()
        {
            if (Waypoints == null) return;
            Vector3 anterior = default;
            bool hayAnterior = false;
            for (int i = 0; i < Waypoints.Length; i++)
            {
                var w = Waypoints[i];
                if (w == null) continue;
                var pos = w.transform.position;

                DibujarIconoCamara(pos, w.mirarPunto, ColorLinea);

                if (hayAnterior)
                {
                    Gizmos.color = ColorLinea;
                    Gizmos.DrawLine(anterior, pos);
                }
                anterior = pos;
                hayAnterior = true;

                // Linea punteada (segmentos cortos) hacia donde mira desde este punto: distinguirla
                // de la linea de RECORRIDO (solida, entre puntos) de un vistazo.
                if (w.mirarPunto != Vector3.zero || (w.mirarPunto - pos).sqrMagnitude > 0.01f)
                {
                    Gizmos.color = ColorMira;
                    DibujarLineaPunteada(pos, w.mirarPunto, 0.4f);
                }

#if UNITY_EDITOR
                Handles.color = Color.white;
                Handles.Label(pos + Vector3.up * 0.6f, $"{i + 1}. {w.name}\n{w.segundosParaLlegar:0.0}s llegar · {w.segundosDeEspera:0.0}s espera");
#endif
            }
        }

        static void DibujarLineaPunteada(Vector3 a, Vector3 b, float paso)
        {
            float dist = Vector3.Distance(a, b);
            if (dist < 0.01f) return;
            var dir = (b - a) / dist;
            int n = Mathf.Max(1, Mathf.FloorToInt(dist / (paso * 2f)));
            for (int i = 0; i < n; i++)
            {
                var p0 = a + dir * (paso * 2f * i);
                var p1 = a + dir * Mathf.Min(dist, paso * 2f * i + paso);
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
