using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Bug #298 ("parpadean esas caras, es porque se solapan las geometrias"): z-fighting = dos caras con la misma normal, en el mismo plano
    // y que se pisan en area. Este detector recorre las mallas LEGIBLES de una zona (los cubos y las mallas fusionadas; las del kit modular
    // importado no son legibles y se revisan por bounds en el retoque) y lista los pares de triangulos alineados a un eje que cumplen eso.
    // Solo lee la escena: no modifica nada.
    public static class DetectorDeSolapes
    {
        public struct Solape
        {
            public string a, b, matA, matB; public bool mismaMalla;
            public int eje; public bool positivo; public float plano, area;
            public Vector2 desde;
            public bool MismoMaterial => matA == matB;
            public override string ToString() => $"eje {"XYZ"[eje]}{(positivo ? "+" : "-")} plano={plano:0.000} area={area:0.00} en ({desde.x:0.0},{desde.y:0.0}) | {a} <> {b}{(mismaMalla ? " (MISMA MALLA)" : "")} [{matA}{(MismoMaterial ? "" : " / " + matB)}]";
        }

        struct Cara { public int r; public Vector2 lo, hi, p0, p1, p2; }

        public static List<Solape> Escanear(Bounds zona, int maximo = 60, float tolerancia = 0.004f, float areaMinima = 0.02f, bool soloVerticales = false)
        {
            var caras = new Dictionary<long, List<Cara>>();
            var nombres = new List<string>(); var materiales = new List<string>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || !r.bounds.Intersects(zona)) continue;   // un renderer apagado (p.ej. los Borde_* del perimetro) no se dibuja: no puede parpadear
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                int id = nombres.Count;
                nombres.Add(r.transform.parent != null ? r.transform.parent.name + "/" + r.name : r.name); materiales.Add(r.sharedMaterial != null ? r.sharedMaterial.name : "-");
                var v = mf.sharedMesh.vertices; var t = mf.sharedMesh.triangles; var M = r.transform.localToWorldMatrix;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var a = M.MultiplyPoint3x4(v[t[i]]); var b = M.MultiplyPoint3x4(v[t[i + 1]]); var c = M.MultiplyPoint3x4(v[t[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-10f) continue;
                    n.Normalize();
                    int ax = Mathf.Abs(n.x) > 0.999f ? 0 : Mathf.Abs(n.y) > 0.999f ? 1 : Mathf.Abs(n.z) > 0.999f ? 2 : -1;
                    if (ax < 0 || (soloVerticales && ax == 1)) continue;
                    float signo = ax == 0 ? n.x : ax == 1 ? n.y : n.z;
                    float plano = ax == 0 ? a.x : ax == 1 ? a.y : a.z;
                    var pa = Proyectar(a, ax); var pb = Proyectar(b, ax); var pc = Proyectar(c, ax);
                    var cara = new Cara { r = id, lo = Vector2.Min(pa, Vector2.Min(pb, pc)), hi = Vector2.Max(pa, Vector2.Max(pb, pc)), p0 = pa, p1 = pb, p2 = pc };
                    long clave = ((long)ax * 2 + (signo > 0 ? 1 : 0)) * 100000000L + (long)Mathf.Round(plano / tolerancia) + 50000000L;
                    if (!caras.TryGetValue(clave, out var lista)) { lista = new List<Cara>(); caras[clave] = lista; }
                    lista.Add(cara);
                }
            }
            var res = new List<Solape>();
            foreach (var kv in caras)
            {
                var l = kv.Value;
                if (l.Count < 2) continue;
                long k = kv.Key; int eje = (int)(k / 100000000L) / 2; bool pos = (k / 100000000L) % 2 == 1; float plano = ((k % 100000000L) - 50000000L) * tolerancia;
                if (eje == 1 && !pos && plano < 0.05f) continue;   // fondos de las cajas apoyadas en el piso: no se ven
                for (int i = 0; i < l.Count && res.Count < maximo; i++)
                    for (int j = i + 1; j < l.Count && res.Count < maximo; j++)
                    {
                        float ox = Mathf.Min(l[i].hi.x, l[j].hi.x) - Mathf.Max(l[i].lo.x, l[j].lo.x);
                        float oy = Mathf.Min(l[i].hi.y, l[j].hi.y) - Mathf.Max(l[i].lo.y, l[j].lo.y);
                        if (ox <= 0.03f || oy <= 0.03f || ox * oy < areaMinima) continue;
                        if (!TriangulosSePisan(l[i], l[j])) continue;
                        res.Add(new Solape { a = nombres[l[i].r], b = nombres[l[j].r], matA = materiales[l[i].r], matB = materiales[l[j].r], mismaMalla = l[i].r == l[j].r, eje = eje, positivo = pos, plano = plano, area = ox * oy, desde = new Vector2(Mathf.Max(l[i].lo.x, l[j].lo.x), Mathf.Max(l[i].lo.y, l[j].lo.y)) });
                    }
            }
            return res;
        }

        // SAT en 2D: se pisan con area si ningun eje de los 6 lados los separa (tocarse por un borde o una esquina no cuenta).
        static bool TriangulosSePisan(Cara a, Cara b)
        {
            var ta = new[] { a.p0, a.p1, a.p2 }; var tb = new[] { b.p0, b.p1, b.p2 };
            for (int k = 0; k < 2; k++)
            {
                var t = k == 0 ? ta : tb;
                for (int i = 0; i < 3; i++)
                {
                    var e = t[(i + 1) % 3] - t[i];
                    var eje = new Vector2(-e.y, e.x).normalized;
                    Rango(ta, eje, out float a0, out float a1); Rango(tb, eje, out float b0, out float b1);
                    if (Mathf.Min(a1, b1) - Mathf.Max(a0, b0) <= 0.01f) return false;
                }
            }
            return true;
        }

        static void Rango(Vector2[] t, Vector2 eje, out float min, out float max)
        {
            min = max = Vector2.Dot(t[0], eje);
            for (int i = 1; i < 3; i++) { float d = Vector2.Dot(t[i], eje); if (d < min) min = d; if (d > max) max = d; }
        }

        static Vector2 Proyectar(Vector3 q, int eje) => eje == 0 ? new Vector2(q.y, q.z) : eje == 1 ? new Vector2(q.x, q.z) : new Vector2(q.x, q.y);

        public static string Informe(Vector3 centro, Vector3 tamano, int maximo = 60, bool soloVerticales = false)
        {
            var l = Escanear(new Bounds(centro, tamano), maximo, 0.004f, 0.02f, soloVerticales);
            var sb = new StringBuilder("solapes=" + l.Count + "\n");
            foreach (var s in l) sb.AppendLine(s.ToString());
            return sb.ToString();
        }
    }
}
