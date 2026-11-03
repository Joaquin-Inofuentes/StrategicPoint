using UnityEngine;

namespace SP.Operacion
{
    // Hoja del porton: se desliza hacia un costado cuando el director abre la entrada (con collider mientras esta cerrada).
    public class PortonDeMansion : MonoBehaviour
    {
        public Vector3 cerrada, abierta;
        public bool Abierto { get; private set; }
        public float Avance { get; private set; }
        public float segundos = 2.6f;

        public void Abrir() { Abierto = true; }
        public void Cerrar() { Abierto = false; }

        void Update()
        {
            float meta = Abierto || OperacionDirector.ApagarMansion ? 1f : 0f;   // sin mansion (checks de la defensa de flancos) el porton no cierra el paso
            Avance = OperacionDirector.ApagarMansion ? 1f : Mathf.MoveTowards(Avance, meta, Time.deltaTime / Mathf.Max(0.1f, segundos));
            transform.position = Vector3.Lerp(cerrada, abierta, Mathf.SmoothStep(0f, 1f, Avance));
            var c = GetComponent<Collider>();
            if (c != null) c.enabled = Avance < 0.6f;
        }
    }
}
