using UnityEngine;
using UnityEngine.AI;

namespace SP.Operacion
{
    // Hoja del porton: se desliza hacia un costado cuando el director abre la entrada (con collider mientras esta cerrada).
    public class PortonDeMansion : MonoBehaviour
    {
        public Vector3 cerrada, abierta;
        public bool Abierto { get; private set; }
        public float Avance { get; private set; }
        public float segundos = 2.6f;
        NavMeshObstacle obstaculo;

        public void Abrir() { Abierto = true; }
        public void Cerrar() { Abierto = false; }

        // 2026-11-13 ("los perros no encuentran bien el camino"): la malla de navegacion se horneo con el porton abierto, asi que los caminos pasaban por la reja cerrada y
        // quien los seguia (los perros, la IA) se quedaba empujando la hoja. Ahora cada hoja talla la malla mientras su collider esta activo.
        void Awake()
        {
            var c = GetComponent<Collider>(); if (c == null) return;
            obstaculo = GetComponent<NavMeshObstacle>(); if (obstaculo == null) obstaculo = gameObject.AddComponent<NavMeshObstacle>();
            AjustarObstaculo.Ajustar(obstaculo, c);
        }

        void Update()
        {
            float meta = Abierto || OperacionDirector.ApagarMansion ? 1f : 0f;   // sin mansion (checks de la defensa de flancos) el porton no cierra el paso
            Avance = OperacionDirector.ApagarMansion ? 1f : Mathf.MoveTowards(Avance, meta, Time.deltaTime / Mathf.Max(0.1f, segundos));
            transform.position = Vector3.Lerp(cerrada, abierta, Mathf.SmoothStep(0f, 1f, Avance));
            var c = GetComponent<Collider>();
            if (c != null) c.enabled = Avance < 0.6f;
            if (obstaculo != null) obstaculo.enabled = c != null && c.enabled;
        }
    }

    // Pone un NavMeshObstacle que talla la malla con la caja del collider (eje alineado, como los setos, pilares y hojas del porton del nivel).
    public static class AjustarObstaculo
    {
        public static int Creados { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reiniciar() { Creados = 0; }

        public static void Ajustar(NavMeshObstacle o, Collider c)
        {
            var b = c.bounds; var t = c.transform; var ls = t.lossyScale;
            o.carving = true; o.carveOnlyStationary = false; o.shape = NavMeshObstacleShape.Box;
            o.center = t.InverseTransformPoint(b.center);
            o.size = new Vector3(b.size.x / Mathf.Max(0.01f, Mathf.Abs(ls.x)), Mathf.Max(b.size.y, 1.5f) / Mathf.Max(0.01f, Mathf.Abs(ls.y)), b.size.z / Mathf.Max(0.01f, Mathf.Abs(ls.z)));
            Creados++;
        }

        // Setos y pilares de arco del jardin: tienen collider pero no talan la malla, y los caminos pasaban por ellos.
        public static int TallarSetosYPilares()
        {
            int n = 0;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c == null || c.isTrigger || !c.enabled) continue;
                string nom = c.name;
                if (!(nom.StartsWith("Seto_") || nom.StartsWith("Arco_Pilar"))) continue;
                if (c.GetComponent<NavMeshObstacle>() != null) continue;
                Ajustar(c.gameObject.AddComponent<NavMeshObstacle>(), c); n++;
            }
            return n;
        }
    }
}
