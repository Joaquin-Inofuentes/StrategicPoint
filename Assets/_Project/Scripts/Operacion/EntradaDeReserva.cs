using System.Collections.Generic;
using UnityEngine;

namespace SP.Operacion
{
    // Las reservas aparecian en suelo abierto, sin ningun acceso (bug 12). Al activarse una reserva se levanta, una sola vez por
    // lugar, una compuerta (marco + hoja oscura + luz de aviso) orientada hacia el centro del mapa, y sale una nube de polvo.
    public static class EntradaDeReserva
    {
        static readonly List<Vector3> hechas = new List<Vector3>(); static readonly List<float> cuando = new List<float>();
        static Material mMarco, mHoja, mLuz;

        // Al reintentar (la escena se recarga y los estaticos sobreviven) las compuertas tienen que poder levantarse de nuevo.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reiniciar() { hechas.Clear(); cuando.Clear(); }

        static Material Mat(ref Material m, Color c, bool emisivo = false)
        {
            if (m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "M_EntradaReserva" };
            m.SetColor("_BaseColor", c);
            if (emisivo) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2.5f); }
            return m;
        }

        public static void Marcar(Vector3 pos)
        {
            pos.y = 0f;
            for (int i = 0; i < hechas.Count; i++) if ((hechas[i] - pos).sqrMagnitude < 36f)
            {
                // el mismo lugar vuelve a recibir refuerzos: llega otro helicoptero si pasaron 12 s desde el anterior (si no, solo el polvo)
                if (Time.time - cuando[i] > 12f) { cuando[i] = Time.time; HeliDeDescarga.Soltar(pos); }
                Polvo(pos); return;
            }
            hechas.Add(pos); cuando.Add(Time.time);
            // Pedido 2026-11-07: sin portones que aparecen de la nada. El punto queda marcado con un ancla vacia (sin piezas ni colliders) y el
            // refuerzo llega en helicoptero (HeliDeDescarga: se queda 14 m arriba con dos sogas y se va).
            var raiz = new GameObject("Compuerta_Reserva").transform;
            raiz.position = pos;
            var haciaCentro = -pos; haciaCentro.y = 0f;
            if (haciaCentro.sqrMagnitude > 1f) raiz.rotation = Quaternion.LookRotation(haciaCentro.normalized);
            HeliDeDescarga.Soltar(pos);
            Polvo(pos);
        }

        static void Pieza(Transform padre, Vector3 local, Vector3 escala, Material m)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = "Pieza";
            o.transform.SetParent(padre, false);
            o.transform.localPosition = local; o.transform.localScale = escala;
            o.GetComponent<Renderer>().sharedMaterial = m;
            var c = o.GetComponent<Collider>(); if (c != null) Object.Destroy(c);
        }

        static void Polvo(Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
                SP.Presentation.ImpactFx.Spawn(pos + new Vector3(Random.Range(-1f, 1f), 0.3f + i * 0.4f, Random.Range(-1f, 1f)), new Color(0.6f, 0.55f, 0.45f), 1.6f, 0.6f);
        }
    }
}
