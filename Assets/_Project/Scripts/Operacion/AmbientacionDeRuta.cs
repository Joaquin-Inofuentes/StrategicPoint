using System.Collections.Generic;
using UnityEngine;

namespace SP.Operacion
{
    // Bug #006 (Sheets, Huir): "acomodar ruta (darle sentido al nivel a traves de ambientacion)". La ruta de escape del tanque (el camino de ~870 m)
    // era un tramo de calle sin nada que la distinguiera: no se entendia por donde seguir ni por que. Se la ambienta sobre la propia ruta:
    //   - faroles de borde de calzada, alternados a izquierda y derecha cada ~14 m (poste fino + cabeza luminosa calida);
    //   - flechas pintadas en el piso (chevrones ambar) cada ~10 m apuntando en el sentido de la marcha;
    //   - un balizado rojo y blanco (conos) en las curvas cerradas, para que se lea el giro.
    // Todo sin colliders ni NavMesh (no cambia el recorrido del tanque ni la IA), con un material compartido por tipo y fusionado en pocos lotes.
    // Se arma una sola vez al empezar la operacion. Un punto se omite si cae dentro de un solido (edificio, muro, prop).
    public static class AmbientacionDeRuta
    {
        public const string NombreRaiz = "AmbientacionDeRuta";
        public static int Faroles { get; private set; }
        public static int Flechas { get; private set; }
        public static int Conos { get; private set; }
        const float SeparacionFarol = 14f, SeparacionFlecha = 10f, MargenDeCalzada = 5.2f;

        static Material matPoste, matLuz, matFlecha, matConoRojo, matConoBlanco;

        static Material Mat(ref Material m, Color c, bool emisivo)
        {
            if (m != null) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(sh) { name = "M_Ruta_" + (emisivo ? "Luz" : "Mate"), hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            if (emisivo) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2.2f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            return m;
        }

        public static int Armar(Transform[] ruta)
        {
            if (ruta == null || ruta.Length < 2) return 0;
            var previa = GameObject.Find(NombreRaiz);
            if (previa != null) Object.Destroy(previa);
            Faroles = Flechas = Conos = 0;
            var raiz = new GameObject(NombreRaiz).transform;

            Mat(ref matPoste, new Color(0.13f, 0.14f, 0.16f), false);
            Mat(ref matLuz, new Color(1f, 0.78f, 0.4f), true);
            Mat(ref matFlecha, new Color(1f, 0.62f, 0.1f), true);
            Mat(ref matConoRojo, new Color(0.85f, 0.12f, 0.08f), false);
            Mat(ref matConoBlanco, new Color(0.92f, 0.92f, 0.9f), false);

            float acFarol = SeparacionFarol * 0.5f, acFlecha = 3f; int lado = 1;
            for (int i = 0; i < ruta.Length - 1; i++)
            {
                if (ruta[i] == null || ruta[i + 1] == null) continue;
                var a = ruta[i].position; var b = ruta[i + 1].position; a.y = 0f; b.y = 0f;
                var d = b - a; float largo = d.magnitude; if (largo < 0.5f) continue;
                var dir = d / largo; var normal = Vector3.Cross(Vector3.up, dir);
                // curva cerrada: el angulo con el tramo siguiente
                if (i + 2 < ruta.Length && ruta[i + 2] != null)
                {
                    var c = ruta[i + 2].position; c.y = 0f; var d2 = (c - b); if (d2.sqrMagnitude > 0.01f && Vector3.Angle(dir, d2.normalized) > 35f) Conos += ConosDeCurva(raiz, b, dir, d2.normalized);
                }
                for (float s = 0f; s < largo; s += 1f)
                {
                    acFarol += 1f; acFlecha += 1f;
                    var p = a + dir * s;
                    if (acFarol >= SeparacionFarol) { acFarol = 0f; lado = -lado; if (PonerFarol(raiz, p + normal * (MargenDeCalzada * lado))) Faroles++; }
                    // Bug #346 ("quitar las marcas de flechas de la ruta, solo dejar las lineas"): ya no se pintan chevrones sobre la calzada.
                    // PonerFlecha queda disponible pero no se llama; Flechas vale siempre 0.
                    if (acFlecha >= SeparacionFlecha) acFlecha = 0f;
                }
            }
            // Un lote por material: pocos draw calls.
            try { StaticBatchingUtility.Combine(raiz.gameObject); } catch { }
            return Faroles + Flechas + Conos;
        }

        static bool Bloqueado(Vector3 p, float r)
        {
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1.2f, r, ~0, QueryTriggerInteraction.Ignore))
                if (c != null && !c.isTrigger && c.bounds.size.y > 0.8f && c.bounds.max.y > 0.7f && c.GetComponentInParent<SP.Actors.Soldier>() == null && c.GetComponentInParent<SP.Vehicles.Vehicle>() == null) return true;
            return false;
        }

        static GameObject Pieza(Transform padre, PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala, Quaternion rot, Material m)
        {
            var g = GameObject.CreatePrimitive(tipo); g.name = nombre;
            var col = g.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
            g.transform.SetParent(padre, false); g.transform.SetPositionAndRotation(pos, rot); g.transform.localScale = escala;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            g.isStatic = true;
            return g;
        }

        static bool PonerFarol(Transform raiz, Vector3 p)
        {
            if (Bloqueado(p, 0.7f)) return false;
            Pieza(raiz, PrimitiveType.Cylinder, "Farol_Poste", p + Vector3.up * 1.6f, new Vector3(0.12f, 1.6f, 0.12f), Quaternion.identity, matPoste);
            Pieza(raiz, PrimitiveType.Cube, "Farol_Brazo", p + Vector3.up * 3.2f, new Vector3(0.7f, 0.08f, 0.08f), Quaternion.identity, matPoste);
            Pieza(raiz, PrimitiveType.Cube, "Farol_Luz", p + Vector3.up * 3.12f, new Vector3(0.5f, 0.12f, 0.3f), Quaternion.identity, matLuz);
            return true;
        }

        // Chevron: dos barras en V sobre la calzada, con la punta hacia delante.
        static bool PonerFlecha(Transform raiz, Vector3 p, Vector3 dir)
        {
            if (Bloqueado(p, 1.2f)) return false;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            foreach (float s in new[] { -1f, 1f })
            {
                var lateral = rot * new Vector3(0.7f * s, 0f, -0.45f);
                var r = rot * Quaternion.Euler(0f, -38f * s, 0f);
                Pieza(raiz, PrimitiveType.Cube, "Flecha_Ala", new Vector3(p.x, 0.11f, p.z) + lateral, new Vector3(0.22f, 0.02f, 1.5f), r, matFlecha);
            }
            return true;
        }

        static int ConosDeCurva(Transform raiz, Vector3 vertice, Vector3 entra, Vector3 sale)
        {
            // Hacia el lado exterior de la curva (donde el conductor tiende a irse): una hilera de 4 conos.
            var giro = Vector3.Cross(entra, sale).y; var exterior = Vector3.Cross(Vector3.up, entra) * (giro > 0 ? -1f : 1f);
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                var p = vertice + exterior * (MargenDeCalzada - 1.4f) - entra * (k * 1.8f);
                if (Bloqueado(p, 0.5f)) continue;
                var m = k % 2 == 0 ? matConoRojo : matConoBlanco;
                Pieza(raiz, PrimitiveType.Cylinder, "Cono", p + Vector3.up * 0.35f, new Vector3(0.38f, 0.35f, 0.38f), Quaternion.identity, m);
                n++;
            }
            return n;
        }
    }
}
