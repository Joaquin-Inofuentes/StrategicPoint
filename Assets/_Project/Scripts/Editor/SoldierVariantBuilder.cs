using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Arma las VARIANTES de soldado (asalto, flanqueador, medico, fusilero y
    // francotirador) a partir de los FBX de arte de Assets/ARTS/SP_Arte/
    // _FBX_Export/04_Personajes.
    //
    // Esos FBX son mallas estaticas (cuerpo + casco/boina/gorra + mochila
    // medica + cinturones) y no tienen esqueleto, asi que por si solos no se
    // pueden animar. Se hace lo mismo que SkinTransfer hizo con M_Soldado:
    // se le transfieren los pesos de hueso del rig mixamo (lego.fbx) por
    // punto mas cercano de la superficie. Las piezas de equipo (casco, mochila,
    // cinturon) se atan 100% al hueso donde van puestas. Todo se funde en UNA
    // malla con un solo submesh (material trimsheet), asi que el soldado sigue
    // usando el mismo Animator y las mismas animaciones de siempre.
    //
    // Las armas que traen esos FBX en la mano NO se incluyen: el juego cuelga
    // el arma real del loadout de la clase (WeaponHolder / ArmaEnLaMano).
    public static class SoldierVariantBuilder
    {
        const string FbxDir = "Assets/ARTS/SP_Arte/_FBX_Export/04_Personajes/";
        const string MallaRiggeada = "Assets/ARTS/Slim Shooter Pack/lego.fbx";
        public const string SalidaDir = "Assets/_Project/Resources/Soldados";
        const string TrimsheetMat = "Assets/_Project/Materials/ArteMundo/MAT_Trimsheet.mat";

        struct Pieza { public string Fbx; public string Nodo; public string Hueso; }

        class Variante { public string Nombre; public string FbxCuerpo; public Pieza[] Extras; }

        static readonly Variante[] Variantes =
        {
            new Variante { Nombre = "Asalto", FbxCuerpo = "SM_Chr_Soldado_Comando", Extras = new[] {
                new Pieza { Fbx = "SM_Chr_Soldado_Comando", Nodo = "SM_Chr_Equipo_Casco", Hueso = "Head" },
                new Pieza { Fbx = "SM_Chr_Soldado_Comando", Nodo = "SM_Wpn_CuchilloCinto", Hueso = "Hips" },
                new Pieza { Fbx = "SM_Chr_Soldado_Comando", Nodo = "SM_Wpn_GranadaCinto", Hueso = "Spine" } } },
            new Variante { Nombre = "Flanqueador", FbxCuerpo = "SM_Chr_Soldado_Explorador", Extras = new[] {
                new Pieza { Fbx = "SM_Chr_Soldado_Francotirador", Nodo = "SM_Chr_Equipo_Boina", Hueso = "Head" },
                new Pieza { Fbx = "SM_Chr_Soldado_Explorador", Nodo = "SM_Wpn_CuchilloCinto", Hueso = "Hips" } } },
            new Variante { Nombre = "Medico", FbxCuerpo = "SM_Chr_Soldado_Medico", Extras = new[] {
                new Pieza { Fbx = "SM_Chr_Soldado_Medico", Nodo = "SM_Chr_Equipo_GorraMedico", Hueso = "Head" },
                new Pieza { Fbx = "SM_Chr_Soldado_Medico", Nodo = "SM_Chr_Equipo_Medico", Hueso = "Spine2" } } },
            new Variante { Nombre = "Fusilero", FbxCuerpo = "SM_Chr_Soldado_Fusilero", Extras = new[] {
                new Pieza { Fbx = "SM_Chr_Soldado_Fusilero", Nodo = "SM_Chr_Equipo_Casco", Hueso = "Head" },
                new Pieza { Fbx = "SM_Chr_Soldado_Fusilero", Nodo = "SM_Wpn_GranadaCinto", Hueso = "Spine" } } },
            new Variante { Nombre = "Francotirador", FbxCuerpo = "SM_Chr_Soldado_Francotirador", Extras = new[] {
                new Pieza { Fbx = "SM_Chr_Soldado_Francotirador", Nodo = "SM_Chr_Equipo_Boina", Hueso = "Head" },
                new Pieza { Fbx = "SM_Chr_Soldado_Francotirador", Nodo = "SM_Wpn_CuchilloCinto", Hueso = "Hips" } } },
        };

        [MenuItem("Strategic Point/Arte/8. Generar variantes de soldado (asalto, flanqueador, medico...)")]
        public static void GenerarTodas()
        {
            var goRig = AssetDatabase.LoadAssetAtPath<GameObject>(MallaRiggeada);
            var piel = goRig != null ? goRig.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (piel == null) { Debug.LogError("[SoldierVariantBuilder] Falta " + MallaRiggeada); return; }
            Directory.CreateDirectory(SalidaDir);

            int ok = 0;
            foreach (var v in Variantes)
                if (Generar(v, piel)) ok++;

            CrearMaterialEnemigo();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SoldierVariantBuilder] {ok}/{Variantes.Length} variantes generadas en {SalidaDir}");
        }

        static bool Generar(Variante v, SkinnedMeshRenderer piel)
        {
            var goCuerpo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + v.FbxCuerpo + ".fbx");
            if (goCuerpo == null) { Debug.LogError("[SoldierVariantBuilder] Falta " + v.FbxCuerpo); return false; }

            var cuerpoFiltro = Buscar(goCuerpo, v.FbxCuerpo + "_Cuerpo");
            if (cuerpoFiltro == null) { Debug.LogError("[SoldierVariantBuilder] Sin cuerpo en " + v.FbxCuerpo); return false; }

            var destino = piel.sharedMesh;
            var vertsRig = destino.vertices;
            var pesosRig = destino.boneWeights;
            var trisRig = destino.triangles;

            // Espacio: del mundo del FBX al espacio de la malla riggeada, apoyando
            // pies con pies y centrando en XZ, con escala uniforme.
            var mCuerpo = cuerpoFiltro.transform.localToWorldMatrix;
            var bC = BoundsMundo(cuerpoFiltro.sharedMesh, mCuerpo);
            var bD = destino.bounds;
            float escala = bD.size.y / Mathf.Max(bC.size.y, 0.0001f);
            var anclaC = new Vector3(bC.center.x, bC.min.y, bC.center.z);
            var anclaD = new Vector3(bD.center.x, bD.min.y, bD.center.z);

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var pesos = new List<BoneWeight>();

            // ---- cuerpo: pesos por superficie ----
            {
                var m = cuerpoFiltro.sharedMesh;
                var vs = m.vertices; var ns = m.normals; var uv = m.uv; var tr = m.triangles;
                int baseIdx = verts.Count;
                for (int i = 0; i < vs.Length; i++)
                {
                    var w = mCuerpo.MultiplyPoint3x4(vs[i]);
                    var p = (w - anclaC) * escala + anclaD;
                    verts.Add(p);
                    norms.Add(mCuerpo.MultiplyVector(ns[i]).normalized);
                    uvs.Add(uv.Length > i ? uv[i] : Vector2.zero);
                    pesos.Add(PesoPorSuperficie(p, vertsRig, pesosRig, trisRig));
                }
                foreach (var t in tr) tris.Add(baseIdx + t);
            }

            // ---- equipo: 100% a un hueso ----
            foreach (var extra in v.Extras)
            {
                var goE = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + extra.Fbx + ".fbx");
                var mf = goE != null ? Buscar(goE, extra.Nodo) : null;
                if (mf == null) { Debug.LogWarning($"[SoldierVariantBuilder] {v.Nombre}: falta la pieza {extra.Nodo}"); continue; }
                int hueso = IndiceDeHueso(piel, extra.Hueso);
                var mE = mf.transform.localToWorldMatrix;
                var m = mf.sharedMesh;
                var vs = m.vertices; var ns = m.normals; var uv = m.uv; var tr = m.triangles;
                int baseIdx = verts.Count;
                for (int i = 0; i < vs.Length; i++)
                {
                    var w = mE.MultiplyPoint3x4(vs[i]);
                    verts.Add((w - anclaC) * escala + anclaD);
                    norms.Add(mE.MultiplyVector(ns[i]).normalized);
                    uvs.Add(uv.Length > i ? uv[i] : Vector2.zero);
                    pesos.Add(new BoneWeight { boneIndex0 = hueso, weight0 = 1f });
                }
                foreach (var t in tr) tris.Add(baseIdx + t);
            }

            var malla = new Mesh { name = "Mesh_" + v.Nombre };
            malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            malla.SetVertices(verts);
            malla.SetNormals(norms);
            malla.SetUVs(0, uvs);
            malla.SetTriangles(tris, 0);
            malla.boneWeights = pesos.ToArray();
            malla.bindposes = destino.bindposes;
            malla.RecalculateTangents();
            malla.RecalculateBounds();

            var ruta = $"{SalidaDir}/Mesh_{v.Nombre}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(malla, ruta);
            Debug.Log($"[SoldierVariantBuilder] {v.Nombre}: {verts.Count} vertices, escala {escala:F3}");
            return true;
        }

        static void CrearMaterialEnemigo()
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(TrimsheetMat);
            if (baseMat == null) { Debug.LogWarning("[SoldierVariantBuilder] Falta " + TrimsheetMat); return; }
            var ruta = SalidaDir + "/MAT_Trimsheet_Enemigo.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(baseMat) { name = "MAT_Trimsheet_Enemigo" };
                AssetDatabase.CreateAsset(m, ruta);
            }
            // Tinte rojizo: el enemigo se distingue del aliado de un vistazo.
            var rojo = new Color(1f, 0.62f, 0.55f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", rojo);
            if (m.HasProperty("_Color")) m.SetColor("_Color", rojo);
            EditorUtility.SetDirty(m);

            // Copia del trimsheet dentro de Resources para poder cargarla en runtime.
            var rutaA = SalidaDir + "/MAT_Trimsheet_Aliado.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(rutaA) == null)
                AssetDatabase.CreateAsset(new Material(baseMat) { name = "MAT_Trimsheet_Aliado" }, rutaA);
        }

        static MeshFilter Buscar(GameObject raiz, string nombre)
        {
            foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == nombre) return mf;
            return null;
        }

        static Bounds BoundsMundo(Mesh m, Matrix4x4 mat)
        {
            var vs = m.vertices;
            var b = new Bounds(mat.MultiplyPoint3x4(vs[0]), Vector3.zero);
            foreach (var p in vs) b.Encapsulate(mat.MultiplyPoint3x4(p));
            return b;
        }

        static int IndiceDeHueso(SkinnedMeshRenderer piel, string nombre)
        {
            for (int i = 0; i < piel.bones.Length; i++)
                if (piel.bones[i].name == "mixamorig:" + nombre) return i;
            Debug.LogWarning("[SoldierVariantBuilder] Hueso no encontrado: " + nombre);
            return 0;
        }

        static BoneWeight PesoPorSuperficie(Vector3 p, Vector3[] vertsRig, BoneWeight[] pesosRig, int[] tris)
        {
            float mejor = float.MaxValue;
            int mejorTri = 0;
            Vector3 mejorBar = new Vector3(1f, 0f, 0f);
            for (int t = 0; t < tris.Length; t += 3)
            {
                var q = PuntoMasCercano(p, vertsRig[tris[t]], vertsRig[tris[t + 1]], vertsRig[tris[t + 2]], out var bar);
                float d = (q - p).sqrMagnitude;
                if (d < mejor) { mejor = d; mejorTri = t; mejorBar = bar; }
            }
            var acum = new Dictionary<int, float>();
            Sumar(acum, pesosRig[tris[mejorTri]], mejorBar.x);
            Sumar(acum, pesosRig[tris[mejorTri + 1]], mejorBar.y);
            Sumar(acum, pesosRig[tris[mejorTri + 2]], mejorBar.z);
            var lista = new List<KeyValuePair<int, float>>(acum);
            lista.Sort((x, y) => y.Value.CompareTo(x.Value));
            var res = new BoneWeight();
            float total = 0f;
            for (int i = 0; i < 4 && i < lista.Count; i++) total += lista[i].Value;
            if (total <= 0f) { res.boneIndex0 = 0; res.weight0 = 1f; return res; }
            for (int i = 0; i < 4 && i < lista.Count; i++)
            {
                float w = lista[i].Value / total; int idx = lista[i].Key;
                if (i == 0) { res.boneIndex0 = idx; res.weight0 = w; }
                else if (i == 1) { res.boneIndex1 = idx; res.weight1 = w; }
                else if (i == 2) { res.boneIndex2 = idx; res.weight2 = w; }
                else { res.boneIndex3 = idx; res.weight3 = w; }
            }
            return res;
        }

        static void Sumar(Dictionary<int, float> d, BoneWeight w, float k)
        {
            if (k <= 0f) return;
            Agregar(d, w.boneIndex0, w.weight0 * k); Agregar(d, w.boneIndex1, w.weight1 * k);
            Agregar(d, w.boneIndex2, w.weight2 * k); Agregar(d, w.boneIndex3, w.weight3 * k);
        }

        static void Agregar(Dictionary<int, float> d, int k, float v)
        {
            if (v <= 0f) return;
            d[k] = d.TryGetValue(k, out var previo) ? previo + v : v;
        }

        // Punto mas cercano de un triangulo (Ericson, Real-Time Collision Detection).
        static Vector3 PuntoMasCercano(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bar)
        {
            var ab = b - a; var ac = c - a; var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { bar = new Vector3(1f, 0f, 0f); return a; }
            var bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { bar = new Vector3(0f, 1f, 0f); return b; }
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float w = d1 / (d1 - d3); bar = new Vector3(1f - w, w, 0f); return a + ab * w; }
            var cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { bar = new Vector3(0f, 0f, 1f); return c; }
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float w = d2 / (d2 - d6); bar = new Vector3(1f - w, 0f, w); return a + ac * w; }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); bar = new Vector3(0f, 1f - w, w); return b + (c - b) * w; }
            float den = 1f / (va + vb + vc);
            float vv = vb * den, ww = vc * den;
            bar = new Vector3(1f - vv - ww, vv, ww);
            return a + ab * vv + ac * ww;
        }
    }
}
