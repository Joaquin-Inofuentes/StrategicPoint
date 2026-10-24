using System.Collections.Generic;
using UnityEngine;

namespace SP.Operacion
{
    // Las reservas aparecian en suelo abierto, sin ningun acceso (bug 12). Al activarse una reserva se levanta, una sola vez por
    // lugar, una compuerta (marco + hoja oscura + luz de aviso) orientada hacia el centro del mapa, y sale una nube de polvo.
    public static class EntradaDeReserva
    {
        static readonly List<Vector3> hechas = new List<Vector3>();
        static Material mMarco, mHoja, mLuz;

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
            for (int i = 0; i < hechas.Count; i++) if ((hechas[i] - pos).sqrMagnitude < 36f) { Polvo(pos); return; }
            hechas.Add(pos);
            var raiz = new GameObject("Compuerta_Reserva").transform;
            raiz.position = pos;
            var haciaCentro = -pos; haciaCentro.y = 0f;
            if (haciaCentro.sqrMagnitude > 1f) raiz.rotation = Quaternion.LookRotation(haciaCentro.normalized);
            Pieza(raiz, new Vector3(-1.4f, 1.6f, 0f), new Vector3(0.35f, 3.2f, 0.5f), Mat(ref mMarco, new Color(0.18f, 0.19f, 0.2f)));
            Pieza(raiz, new Vector3(1.4f, 1.6f, 0f), new Vector3(0.35f, 3.2f, 0.5f), mMarco);
            Pieza(raiz, new Vector3(0f, 3.3f, 0f), new Vector3(3.2f, 0.4f, 0.5f), mMarco);
            Pieza(raiz, new Vector3(0f, 1.5f, 0.15f), new Vector3(2.45f, 3f, 0.15f), Mat(ref mHoja, new Color(0.04f, 0.04f, 0.05f)));
            Pieza(raiz, new Vector3(0f, 3.75f, 0f), new Vector3(0.4f, 0.25f, 0.4f), Mat(ref mLuz, new Color(1f, 0.55f, 0.1f), true));
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
