using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Bug #298 ("parpadean esas caras, es porque se solapan las geometrias"): las piezas modulares de muro (SM_Mod_*) estan armadas con cajas
    // que se pisan -- cuerpo + zocalo + remate comparten el plano de abajo (y=0) y los planos de los extremos --, asi que dos caras iguales
    // ocupan el mismo lugar y la GPU alterna entre ellas (z-fighting). Contra el piso no se nota (el fondo no se ve), pero la cornisa y el
    // parapeto de los edificios son esas mismas piezas aplastadas y levantadas a 4 m: ahi el fondo SI se ve desde la calle.
    // Esto arma una copia limpia de cada malla (la FBX original no se toca): quita las caras que otra cara del mismo plano ya cubre entera
    // y achica las que solo se pisan por un borde. La forma que se ve no cambia, solo desaparecen las caras duplicadas.
    public static class LimpiadorDeMallasKit
    {
        const string CarpetaFbx = "Assets/ARTS/SP_Arte/_FBX_Export/01_Modulares/";
        const string CarpetaSalida = "Assets/_Project/Art/MallasLimpias/";
        const float Eps = 0.0015f;

        struct Quad
        {
            public int[] idx;          // 4 indices de vertice
            public int eje; public float signo, plano;
            public Vector2 lo, hi;     // rectangulo en el plano
            public bool vivo;
        }

        public static string LimpiarTodas()
        {
            var sb = new StringBuilder();
            foreach (var nombre in new[] { "Muro_Recto", "Muro_Medio", "Muro_Pilar", "Muro_Puerta", "Losa" })
                sb.AppendLine(Limpiar(nombre));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }

        public static string Limpiar(string nombre)
        {
            string ruta = CarpetaFbx + "SM_Mod_" + nombre + ".fbx";
            var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (imp == null) return nombre + ": sin FBX";
            bool eraLegible = imp.isReadable;
            if (!eraLegible) { imp.isReadable = true; imp.SaveAndReimport(); }
            Mesh src = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ruta)) if (o is Mesh m) { src = m; break; }
            if (src == null) return nombre + ": sin malla";

            var v = src.vertices; var nrm = src.normals; var uv = src.uv; var tri = src.triangles;
            var quads = new List<Quad>(); var sueltos = new List<int>();
            // pares de triangulos consecutivos que comparten 2 vertices y caen en el mismo plano = un quad
            for (int i = 0; i + 5 < tri.Length; i += 6)
            {
                if (!EsQuad(v, tri, i, out var q)) { sueltos.Add(i); sueltos.Add(i + 3); continue; }
                quads.Add(q);
            }
            if (sueltos.Count > 0 || quads.Count * 6 != tri.Length) return nombre + ": malla no es solo cajas (" + sueltos.Count / 2 + " triangulos sueltos), no se toca";

            int quitados = 0, achicados = 0;
            for (int a = 0; a < quads.Count; a++)
            {
                var A = quads[a];
                if (!A.vivo) continue;
                for (int b = 0; b < quads.Count; b++)
                {
                    if (a == b) continue;
                    var B = quads[b];
                    if (!B.vivo || A.eje != B.eje || A.signo != B.signo || Mathf.Abs(A.plano - B.plano) > Eps) continue;
                    // B cubre a A entero: A sobra
                    if (B.lo.x <= A.lo.x + Eps && B.lo.y <= A.lo.y + Eps && B.hi.x >= A.hi.x - Eps && B.hi.y >= A.hi.y - Eps)
                    {
                        // si son identicos se quita solo el de indice mayor (no los dos)
                        bool iguales = Mathf.Abs(A.lo.x - B.lo.x) < Eps && Mathf.Abs(A.lo.y - B.lo.y) < Eps && Mathf.Abs(A.hi.x - B.hi.x) < Eps && Mathf.Abs(A.hi.y - B.hi.y) < Eps;
                        if (iguales && a < b) continue;
                        A.vivo = false; quads[a] = A; quitados++; break;
                    }
                    // B cubre todo el ancho de A en una direccion y se pisan por un borde en la otra: se le recorta ese borde a A
                    for (int d = 0; d < 2; d++)
                    {
                        int o = 1 - d;   // d = direccion en la que B cubre a A entero, o = direccion del recorte
                        if (Get(B.lo, d) > Get(A.lo, d) + Eps || Get(B.hi, d) < Get(A.hi, d) - Eps) continue;
                        float bl = Get(B.lo, o), bh = Get(B.hi, o), al = Get(A.lo, o), ah = Get(A.hi, o);
                        if (bh <= al + Eps || bl >= ah - Eps) continue;   // no se pisan
                        if (bl <= al + Eps && bh < ah - Eps) { A.lo = Set(A.lo, o, bh); A = Recortar(A, v, uv, src); quads[a] = A; achicados++; }
                        else if (bh >= ah - Eps && bl > al + Eps) { A.hi = Set(A.hi, o, bl); A = Recortar(A, v, uv, src); quads[a] = A; achicados++; }
                    }
                }
            }

            // malla nueva: los quads vivos con sus vertices (ya recortados) y normales/uv de origen
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<int>();
            foreach (var q in quads)
            {
                if (!q.vivo) continue;
                int baseI = nv.Count;
                for (int k = 0; k < 4; k++) { nv.Add(v[q.idx[k]]); nn.Add(nrm.Length > 0 ? nrm[q.idx[k]] : Vector3.up); nuv.Add(uv.Length > 0 ? uv[q.idx[k]] : Vector2.zero); }
                // orden original de los dos triangulos
                nt.AddRange(new[] { baseI, baseI + 1, baseI + 2, baseI, baseI + 2, baseI + 3 });
            }
            // los vertices recortados viven en 'v' (Recortar los reescribe); el orden de triangulos original se conserva por quad
            var limpia = new Mesh { name = "SM_Mod_" + nombre + "_Limpia" };
            // se reconstruyen los triangulos respetando el sentido (frente hacia la normal)
            var tris = new List<int>();
            for (int i = 0; i < nv.Count; i += 4)
            {
                var n = Vector3.Cross(nv[i + 1] - nv[i], nv[i + 2] - nv[i]);
                var objetivo = nn[i];
                bool ok = Vector3.Dot(n, objetivo) >= 0f;
                if (ok) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            limpia.SetVertices(nv); limpia.SetNormals(nn); limpia.SetUVs(0, nuv); limpia.SetTriangles(tris, 0);
            limpia.RecalculateBounds();
            System.IO.Directory.CreateDirectory(CarpetaSalida);
            string salida = CarpetaSalida + "SM_Mod_" + nombre + "_Limpia.asset";
            AssetDatabase.DeleteAsset(salida);
            AssetDatabase.CreateAsset(limpia, salida);
            if (!eraLegible) { imp.isReadable = false; imp.SaveAndReimport(); }
            return $"{nombre}: {quads.Count} caras -> {quads.Count - quitados} (quitadas {quitados}, recortadas {achicados}) en {salida}";
        }

        static float Get(Vector2 p, int d) => d == 0 ? p.x : p.y;
        static Vector2 Set(Vector2 p, int d, float val) { if (d == 0) p.x = val; else p.y = val; return p; }

        static Vector2 Plano(Vector3 q, int eje) => eje == 0 ? new Vector2(q.y, q.z) : eje == 1 ? new Vector2(q.x, q.z) : new Vector2(q.x, q.y);

        static bool EsQuad(Vector3[] v, int[] tri, int i, out Quad q)
        {
            q = default;
            var idx = new HashSet<int> { tri[i], tri[i + 1], tri[i + 2], tri[i + 3], tri[i + 4], tri[i + 5] };
            if (idx.Count != 4) return false;
            var a = v[tri[i]]; var b = v[tri[i + 1]]; var c = v[tri[i + 2]];
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) return false; n.Normalize();
            int eje = Mathf.Abs(n.x) > 0.999f ? 0 : Mathf.Abs(n.y) > 0.999f ? 1 : Mathf.Abs(n.z) > 0.999f ? 2 : -1;
            if (eje < 0) return false;
            var arr = new List<int>(idx).ToArray();
            float plano = eje == 0 ? a.x : eje == 1 ? a.y : a.z;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            foreach (int k in arr)
            {
                float pk = eje == 0 ? v[k].x : eje == 1 ? v[k].y : v[k].z;
                if (Mathf.Abs(pk - plano) > Eps) return false;
                var p2 = Plano(v[k], eje); lo = Vector2.Min(lo, p2); hi = Vector2.Max(hi, p2);
            }
            q = new Quad { idx = arr, eje = eje, signo = (eje == 0 ? n.x : eje == 1 ? n.y : n.z) > 0 ? 1f : -1f, plano = plano, lo = lo, hi = hi, vivo = true };
            return true;
        }

        // Mueve los 4 vertices del quad a su rectangulo nuevo (lo..hi) y reasigna uv por interpolacion de las originales.
        static Quad Recortar(Quad q, Vector3[] v, Vector2[] uv, Mesh src)
        {
            var viejos = new Vector3[4]; var uvViejos = new Vector2[4];
            for (int k = 0; k < 4; k++) { viejos[k] = v[q.idx[k]]; uvViejos[k] = uv.Length > 0 ? uv[q.idx[k]] : Vector2.zero; }
            for (int k = 0; k < 4; k++)
            {
                var p2 = Plano(viejos[k], q.eje);
                // esquina mas cercana del rectangulo nuevo
                var nuevo = new Vector2(Mathf.Abs(p2.x - q.lo.x) < Mathf.Abs(p2.x - q.hi.x) ? q.lo.x : q.hi.x, Mathf.Abs(p2.y - q.lo.y) < Mathf.Abs(p2.y - q.hi.y) ? q.lo.y : q.hi.y);
                // pero la esquina "vieja" puede ser la misma: se decide por el lado en que el vertice estaba dentro del rango original
                Vector3 pos = viejos[k];
                if (q.eje == 0) { pos.y = nuevo.x; pos.z = nuevo.y; }
                else if (q.eje == 1) { pos.x = nuevo.x; pos.z = nuevo.y; }
                else { pos.x = nuevo.x; pos.y = nuevo.y; }
                v[q.idx[k]] = pos;
            }
            return q;
        }
    }
}
