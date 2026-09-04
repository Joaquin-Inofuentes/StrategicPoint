using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // POR QUE EXISTE ESTO.
    //
    // El soldado del juego venia con la textura ESTIRADA en franjas, y no
    // era un problema del material: son dos mallas distintas.
    //
    //   M_Soldado.fbx  601 vertices, UVs empaquetadas en 0.252 .. 0.748.
    //                  Es la malla que se pinto en Substance -- el set de
    //                  texturas lego_Material_* corresponde a ESTAS UVs.
    //                  No tiene esqueleto.
    //
    //   lego.fbx       546 vertices, UVs en una grilla generica de caja
    //                  (0, 0.125, 0.25, 0.375 ... 1). Es la malla que trae
    //                  el esqueleto mixamo con el que estan hechas las 7
    //                  animaciones. Nadie pinto nunca sobre estas UVs.
    //
    // Pegarle la textura de la primera a la segunda da exactamente lo que
    // se veia: los colores correctos untados en tiras verticales.
    //
    // La solucion real seria exportar M_Soldado ya riggeado desde Maya.
    // Mientras tanto, esto arma esa malla: toma la geometria y las UVs
    // BUENAS de M_Soldado y les transfiere los pesos de hueso de lego por
    // vertice mas cercano. Funciona bien aca por dos motivos concretos, no
    // por suerte: las dos mallas estan en la misma pose (brazos abajo,
    // ancho 0.89 vs 0.96 -- si alguna estuviera en T-pose el ancho seria
    // casi igual al alto) y el personaje es de bloques rigidos, sin
    // deformacion suave que un vecino mas cercano pudiera arruinar.
    public static class SkinTransfer
    {
        const string MallaPintada = "Assets/ARTS/SP_Arte/M_Soldado.fbx";
        const string MallaRiggeada = "Assets/ARTS/Slim Shooter Pack/lego.fbx";
        const string Salida = "Assets/_Project/Models/M_Soldado_Skinned.asset";

        [MenuItem("Strategic Point/Arte/Regenerar malla del soldado (UVs buenas + skinning)")]
        public static Mesh Generar()
        {
            var goPintada = AssetDatabase.LoadAssetAtPath<GameObject>(MallaPintada);
            var goRig = AssetDatabase.LoadAssetAtPath<GameObject>(MallaRiggeada);
            if (goPintada == null || goRig == null)
            {
                Debug.LogError("[SkinTransfer] Falta " + MallaPintada + " o " + MallaRiggeada);
                return null;
            }

            var filtro = goPintada.GetComponentInChildren<MeshFilter>(true);
            var piel = goRig.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (filtro == null || piel == null) { Debug.LogError("[SkinTransfer] No encuentro las mallas."); return null; }

            var origen = filtro.sharedMesh;   // UVs buenas, sin huesos
            var destino = piel.sharedMesh;    // huesos, UVs inservibles

            // Las dos mallas viven en espacios distintos (la pintada trae
            // el nodo escalado 0.327 y corrido en Z). Se lleva la pintada
            // al espacio de la riggeada apoyando pies con pies y centrando
            // en XZ: escala UNIFORME, para no deformar el personaje al
            // acomodarlo.
            var bo = origen.bounds;
            var bd = destino.bounds;
            float escala = bd.size.y / Mathf.Max(bo.size.y, 0.0001f);
            var anclaOrigen = new Vector3(bo.center.x, bo.min.y, bo.center.z);
            var anclaDestino = new Vector3(bd.center.x, bd.min.y, bd.center.z);

            var vertsOrigen = origen.vertices;

            // COMO SE ELIGEN LOS PESOS. Se probaron dos caminos peores
            // antes de este, y las dos fallas se veian en pantalla:
            //
            //   1. Copiar del VERTICE mas cercano de lego. Donde las
            //      mallas no coinciden (M_Soldado tiene gorra y lentes que
            //      lego no tiene) el vecino caia en otra parte del cuerpo y
            //      el brazo se estiraba hacia el pecho al animar.
            //   2. Atar cada vertice ENTERO al hueso mas cercano. Los
            //      miembros quedaban rigidos pero mal repartidos: los
            //      brazos se plegaban en cunas al doblar el codo.
            //
            // Lo que sirve es lo que se usa siempre para esto: para cada
            // vertice se busca el punto mas cercano sobre la SUPERFICIE de
            // lego -- triangulo por triangulo, no vertice por vertice -- y
            // se mezclan los pesos de los tres vertices de ese triangulo
            // con sus coordenadas baricentricas. Los pesos que salen son
            // los que dejo el rigger de lego, interpolados donde
            // corresponde, y no una aproximacion inventada aca.
            var vertsDestino = destino.vertices;
            var pesosDestino = destino.boneWeights;
            var triangulos = destino.triangles;

            var vertsNuevos = new Vector3[vertsOrigen.Length];
            var pesosNuevos = new BoneWeight[vertsOrigen.Length];
            float peorDistancia = 0f;

            for (int i = 0; i < vertsOrigen.Length; i++)
            {
                var v = (vertsOrigen[i] - anclaOrigen) * escala + anclaDestino;
                vertsNuevos[i] = v;

                float mejorSqr = float.MaxValue;
                int mejorTri = 0;
                Vector3 mejorBar = new Vector3(1f, 0f, 0f);

                for (int t = 0; t < triangulos.Length; t += 3)
                {
                    var a = vertsDestino[triangulos[t]];
                    var b = vertsDestino[triangulos[t + 1]];
                    var c = vertsDestino[triangulos[t + 2]];
                    var q = PuntoMasCercanoEnTriangulo(v, a, b, c, out var bar);
                    float d = (q - v).sqrMagnitude;
                    if (d < mejorSqr) { mejorSqr = d; mejorTri = t; mejorBar = bar; }
                }

                pesosNuevos[i] = Mezclar(
                    pesosDestino[triangulos[mejorTri]], mejorBar.x,
                    pesosDestino[triangulos[mejorTri + 1]], mejorBar.y,
                    pesosDestino[triangulos[mejorTri + 2]], mejorBar.z);

                peorDistancia = Mathf.Max(peorDistancia, Mathf.Sqrt(mejorSqr));
            }

            var malla = new Mesh { name = "M_Soldado_Skinned" };
            malla.indexFormat = origen.indexFormat;
            malla.vertices = vertsNuevos;
            malla.normals = origen.normals;
            malla.uv = origen.uv;
            if (origen.colors.Length == vertsOrigen.Length) malla.colors = origen.colors;
            malla.subMeshCount = origen.subMeshCount;
            for (int s = 0; s < origen.subMeshCount; s++) malla.SetTriangles(origen.GetTriangles(s), s);
            malla.boneWeights = pesosNuevos;
            malla.bindposes = destino.bindposes;
            malla.RecalculateTangents();
            malla.RecalculateBounds();

            Directory.CreateDirectory(Path.GetDirectoryName(Salida));
            var previa = AssetDatabase.LoadAssetAtPath<Mesh>(Salida);
            if (previa != null) AssetDatabase.DeleteAsset(Salida);
            AssetDatabase.CreateAsset(malla, Salida);
            AssetDatabase.SaveAssets();

            // La peor distancia es la del vertice de M_Soldado que quedo
            // mas lejos de la superficie de lego (gorra, lentes: cosas que
            // lego no tiene). Sirve de control de que las dos mallas siguen
            // estando en la misma pose.
            Debug.Log($"[SkinTransfer] {malla.name}: {vertsNuevos.Length} vertices, " +
                      $"escala {escala:F3}, peor distancia a la superficie {peorDistancia:F3} m.");
            return malla;
        }

        // Punto mas cercano de un triangulo a p, con sus coordenadas
        // baricentricas. Algoritmo clasico por regiones de Voronoi
        // (Ericson, Real-Time Collision Detection): cubre los tres
        // vertices, las tres aristas y el interior, sin proyectar fuera.
        static Vector3 PuntoMasCercanoEnTriangulo(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bar)
        {
            var ab = b - a; var ac = c - a; var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { bar = new Vector3(1f, 0f, 0f); return a; }

            var bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { bar = new Vector3(0f, 1f, 0f); return b; }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float w = d1 / (d1 - d3);
                bar = new Vector3(1f - w, w, 0f);
                return a + ab * w;
            }

            var cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { bar = new Vector3(0f, 0f, 1f); return c; }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                bar = new Vector3(1f - w, 0f, w);
                return a + ac * w;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                bar = new Vector3(0f, 1f - w, w);
                return b + (c - b) * w;
            }

            float den = 1f / (va + vb + vc);
            float vv = vb * den, ww = vc * den;
            bar = new Vector3(1f - vv - ww, vv, ww);
            return a + ab * vv + ac * ww;
        }

        // Suma tres BoneWeight ponderados y se queda con las 4 influencias
        // mas fuertes, normalizadas: es el maximo que admite un
        // SkinnedMeshRenderer.
        static BoneWeight Mezclar(BoneWeight a, float pa, BoneWeight b, float pb, BoneWeight c, float pc)
        {
            var acum = new System.Collections.Generic.Dictionary<int, float>();
            Sumar(acum, a, pa);
            Sumar(acum, b, pb);
            Sumar(acum, c, pc);

            var lista = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, float>>(acum);
            lista.Sort((x, y) => y.Value.CompareTo(x.Value));

            var res = new BoneWeight();
            float total = 0f;
            for (int i = 0; i < 4 && i < lista.Count; i++) total += lista[i].Value;
            if (total <= 0f) { res.boneIndex0 = 0; res.weight0 = 1f; return res; }

            for (int i = 0; i < 4 && i < lista.Count; i++)
            {
                float w = lista[i].Value / total;
                int idx = lista[i].Key;
                if (i == 0) { res.boneIndex0 = idx; res.weight0 = w; }
                else if (i == 1) { res.boneIndex1 = idx; res.weight1 = w; }
                else if (i == 2) { res.boneIndex2 = idx; res.weight2 = w; }
                else { res.boneIndex3 = idx; res.weight3 = w; }
            }
            return res;
        }

        static void Sumar(System.Collections.Generic.Dictionary<int, float> d, BoneWeight w, float k)
        {
            if (k <= 0f) return;
            Agregar(d, w.boneIndex0, w.weight0 * k);
            Agregar(d, w.boneIndex1, w.weight1 * k);
            Agregar(d, w.boneIndex2, w.weight2 * k);
            Agregar(d, w.boneIndex3, w.weight3 * k);
        }

        static void Agregar(System.Collections.Generic.Dictionary<int, float> d, int k, float v)
        {
            if (v <= 0f) return;
            d[k] = d.TryGetValue(k, out var previo) ? previo + v : v;
        }

        public static Mesh Cargar() => AssetDatabase.LoadAssetAtPath<Mesh>(Salida);
    }
}
