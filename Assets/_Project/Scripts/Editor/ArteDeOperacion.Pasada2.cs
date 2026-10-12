using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Segunda pasada de arte ("mejora todo lo que puedas"): lo que despues de la primera seguia leyendose como blocking.
    //
    //  - Techos: cada edificio (y las torres de relleno del fondo de la ciudad) suma equipos de aire, tanques de agua, casetas
    //    de escalera, ventilaciones y antenas, combinados en UNA malla por edificio (no suma un renderer por pieza).
    //  - Suelo: el plano del desierto era un solo color. Se le agregan manchas irregulares de arena, tierra y grava, huellas de
    //    vehiculo, piedras, arbustos y crateres, solo sobre piso libre (nada encima de calles, pisos o props). Manchas, huellas y
    //    piedras van combinadas por sector (6 x 6); arbustos y crateres son instancias del kit marcadas estaticas.
    //  - Surcos de la plantacion: lomo de tierra con matas encima (antes, franjas verdes planas).
    //  - Patas de las torres: reticulado de acero con zapata (antes, una columna maciza de 3 x 12 x 3).
    //
    // Nada de esto tiene collider y todo cuelga de raices "ArteOp" con NavMeshModifier (ignorar): ni la fisica ni la
    // navegacion cambian. Mismo criterio de idempotencia que la primera pasada.
    public static partial class ArteDeOperacion
    {
        const string ContTechos = "Op_Techos";
        const string ContSuelo = "Op_Suelo";
        static readonly HashSet<string> contenedoresLimpios = new HashSet<string>();
        static int techosN;

        static string F(float x) => x.ToString("0.##", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------
        // Lote: piezas (malla + matriz + material) -> una malla con un submesh por material
        // ------------------------------------------------------------------
        sealed class Lote
        {
            readonly Dictionary<Material, List<CombineInstance>> porMat = new Dictionary<Material, List<CombineInstance>>();
            readonly List<Mesh> temporales = new List<Mesh>();
            public int Piezas;

            public void Agregar(Mesh m, Matrix4x4 m4, Material mat, int sub = 0)
            {
                if (m == null || mat == null) return;
                if (!porMat.TryGetValue(mat, out var l)) porMat[mat] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = m, transform = m4, subMeshIndex = sub });
                Piezas++;
            }

            public void Agregar(Gen g, Matrix4x4 m4, Material mat)
            {
                if (g == null || g.v.Count == 0) return;
                var m = g.Crear("tmp"); temporales.Add(m);
                Agregar(m, m4, mat);
            }

            public Mesh Construir(string nombre, out Material[] mats)
            {
                var subs = new List<CombineInstance>(); var lm = new List<Material>();
                foreach (var kv in porMat)
                {
                    var parcial = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    parcial.CombineMeshes(kv.Value.ToArray(), true, true);
                    temporales.Add(parcial);
                    subs.Add(new CombineInstance { mesh = parcial, transform = Matrix4x4.identity });
                    lm.Add(kv.Key);
                }
                int vertices = 0; foreach (var c in subs) vertices += c.mesh.vertexCount;
                var final = new Mesh { name = nombre, indexFormat = vertices < 65000 ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32 };
                final.CombineMeshes(subs.ToArray(), false, false);
                final.RecalculateBounds();
                foreach (var m in temporales) Object.DestroyImmediate(m);
                temporales.Clear();
                mats = lm.ToArray();
                return final;
            }
        }

        static GameObject PonerLote(Transform padre, Lote lote, string nombre, string contenedor)
        {
            if (lote.Piezas == 0) return null;
            var mesh = GuardarEnContenedor(lote.Construir(nombre, out var mats), contenedor);
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // Varias mallas en un solo .asset (una por edificio / sector). La primera de cada corrida descarta el contenedor anterior:
        // sus usuarios ya se borraron al limpiar las raices "ArteOp".
        static Mesh GuardarEnContenedor(Mesh m, string contenedor)
        {
            string ruta = $"{CarpetaMallas}/{contenedor}.asset";
            if (contenedoresLimpios.Add(ruta)) AssetDatabase.DeleteAsset(ruta);
            if (AssetDatabase.LoadMainAssetAtPath(ruta) == null) AssetDatabase.CreateAsset(m, ruta);
            else AssetDatabase.AddObjectToAsset(m, ruta);
            return m;
        }

        static Material Mat(string nombre) => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Operacion/" + nombre + ".mat");

        // Copia de un material de paleta (una celda del trimsheet) con el color multiplicado.
        static readonly Dictionary<string, Material> variantes = new Dictionary<string, Material>();
        static Material Variante(Material baseMat, string sufijo, Color k)
        {
            string key = baseMat.name + "_" + sufijo;
            if (variantes.TryGetValue(key, out var hecho) && hecho != null) return hecho;
            string ruta = "Assets/_Project/Materials/ArteMundo/MAT_Op_Var_" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, ruta); }
            else mat.CopyPropertiesFromMaterial(baseMat);
            mat.SetColor("_BaseColor", k); if (mat.HasProperty("_Color")) mat.SetColor("_Color", k);
            EditorUtility.SetDirty(mat);
            variantes[key] = mat;
            return mat;
        }

        // ------------------------------------------------------------------
        // Piezas geometricas
        // ------------------------------------------------------------------
        // Barra de seccion cuadrada entre dos puntos (reticulados, patas, antenas).
        static void Barra(Gen g, Vector3 a, Vector3 b, float r)
        {
            var d = (b - a).normalized;
            var u = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized * r;
            var w = Vector3.Cross(d, u).normalized * r;
            var c = (a + b) * 0.5f;
            var p = new[] { a - u - w, a + u - w, a + u + w, a - u + w, b - u - w, b + u - w, b + u + w, b - u + w };
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; g.QuadFuera(p[i], p[j], p[j + 4], p[i + 4], c); }
            g.QuadFuera(p[0], p[1], p[2], p[3], c); g.QuadFuera(p[4], p[5], p[6], p[7], c);
        }

        // Triangulo indexado (vertices ya cargados) orientado para mirar hacia +Y.
        static void TriArriba(Gen g, int a, int b, int c)
        {
            if (Vector3.Cross(g.v[b] - g.v[a], g.v[c] - g.v[a]).y < 0f) { int x = b; b = c; c = x; }
            g.t.Add(a); g.t.Add(b); g.t.Add(c);
        }

        static void Escalar(Gen g, Vector3 k)
        {
            for (int i = 0; i < g.v.Count; i++) g.v[i] = Vector3.Scale(g.v[i], k);
        }

        // Equipo de aire acondicionado 1,3 x 1,0 x 1,0 m: cuerpo sobre patines, rejilla al frente y ventilador arriba.
        static Mesh MallaEquipoAire()
        {
            var g = new Gen();
            g.Caja(-0.65f, 0.65f, 0.12f, 0.95f, -0.5f, 0.5f);
            g.Caja(-0.6f, -0.45f, 0f, 0.12f, -0.45f, 0.45f); g.Caja(0.45f, 0.6f, 0f, 0.12f, -0.45f, 0.45f);
            for (int i = 0; i < 6; i++) { float y = 0.22f + i * 0.11f; g.Caja(-0.58f, 0.58f, y, y + 0.04f, -0.53f, -0.5f); }
            g.Prisma(0.2f, 0f, 0.95f, 1.0f, 0.36f, 0.36f, 0.36f, 0.36f, 10);
            g.Prisma(0.2f, 0f, 1.0f, 1.04f, 0.08f, 0.08f, 0.08f, 0.08f, 6);
            return Guardar(g.Crear("Op_EquipoAire"));
        }

        // Tanque de agua de techo: cuatro patas, plataforma, cuerpo con zunchos y tapa conica (alto 3 m).
        static Mesh MallaTanqueTecho()
        {
            var g = new Gen();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Barra(g, new Vector3(sx * 0.72f, 0f, sz * 0.72f), new Vector3(sx * 0.62f, 1.1f, sz * 0.62f), 0.06f);
            g.Caja(-0.85f, 0.85f, 1.1f, 1.22f, -0.85f, 0.85f);
            g.Prisma(0f, 0f, 1.22f, 2.7f, 0.78f, 0.78f, 0.78f, 0.78f, 12, false);
            g.Prisma(0f, 0f, 2.7f, 3.05f, 0.8f, 0.8f, 0.1f, 0.1f, 12, true);
            g.Prisma(0f, 0f, 1.72f, 1.78f, 0.81f, 0.81f, 0.81f, 0.81f, 12, false);
            g.Prisma(0f, 0f, 2.25f, 2.31f, 0.81f, 0.81f, 0.81f, 0.81f, 12, false);
            return Guardar(g.Crear("Op_TanqueTecho"));
        }

        // Antena: mastil de 4,6 m con tres travesaños, caja de equipo y tres tirantes.
        static Mesh MallaAntena()
        {
            var g = new Gen();
            g.Caja(-0.3f, 0.3f, 0f, 0.08f, -0.3f, 0.3f);
            Barra(g, new Vector3(0f, 0f, 0f), new Vector3(0f, 4.6f, 0f), 0.05f);
            float[] ys = { 2.6f, 3.4f, 4.1f }, ls = { 1.3f, 0.95f, 0.6f };
            for (int i = 0; i < 3; i++)
            {
                Barra(g, new Vector3(-ls[i] * 0.5f, ys[i], 0f), new Vector3(ls[i] * 0.5f, ys[i], 0f), 0.025f);
                foreach (float sx in new[] { -1f, 1f }) Barra(g, new Vector3(sx * ls[i] * 0.5f, ys[i], 0f), new Vector3(sx * ls[i] * 0.5f, ys[i] + 0.28f, 0f), 0.02f);
            }
            g.Caja(0.05f, 0.32f, 0.9f, 1.35f, -0.13f, 0.13f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.4f;
                Barra(g, new Vector3(0f, 3.0f, 0f), new Vector3(Mathf.Cos(a) * 1.25f, 0.02f, Mathf.Sin(a) * 1.25f), 0.012f);
            }
            return Guardar(g.Crear("Op_Antena"));
        }

        // Caseta de escalera 2,2 x 2,45 x 2,0 m con losa en alero. Submesh 0: cuerpo (color del edificio); 1: puerta de metal.
        static Mesh MallaCaseta()
        {
            var cuerpo = new Gen();
            cuerpo.Caja(-1.1f, 1.1f, 0f, 2.3f, -1.0f, 1.0f);
            cuerpo.Caja(-1.25f, 1.25f, 2.3f, 2.45f, -1.15f, 1.15f);
            var puerta = new Gen();
            puerta.Caja(-0.48f, 0.48f, 0f, 2.0f, -1.05f, -1.0f);
            puerta.Caja(0.28f, 0.4f, 0.95f, 1.05f, -1.09f, -1.05f);   // manija
            var a = cuerpo.Crear("a"); var b = puerta.Crear("b");
            var m = new Mesh { name = "Op_CasetaTecho" };
            m.CombineMeshes(new[] { new CombineInstance { mesh = a, transform = Matrix4x4.identity }, new CombineInstance { mesh = b, transform = Matrix4x4.identity } }, false, false);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            return Guardar(m);
        }

        // Ventilacion: caja baja con caño y sombrerete.
        static Mesh MallaVentilacion()
        {
            var g = new Gen();
            g.Caja(-0.3f, 0.3f, 0f, 0.45f, -0.3f, 0.3f);
            g.Prisma(0f, 0f, 0.45f, 1.1f, 0.14f, 0.14f, 0.14f, 0.14f, 8, false);
            g.Prisma(0f, 0f, 1.1f, 1.26f, 0.27f, 0.27f, 0.02f, 0.02f, 8, true);
            return Guardar(g.Crear("Op_Ventilacion"));
        }

        // Piedra chica facetada (unidad, base en y=0): anillo de base, anillo medio y punta, sacudidos por ruido. Sin tapa de abajo
        // (va semienterrada): 18 caras, porque se repite cientos de veces dentro de las mallas del suelo.
        static Mesh MallaPiedra(int semilla)
        {
            var g = new Gen();
            const int L = 6;
            Vector3 Sacudir(Vector3 p)
            {
                float n = Ruido(p.x * 4f + 3f, p.y * 4f, p.z * 4f, semilla) - 0.5f;
                return new Vector3(p.x * (1f + n * 0.5f), p.y > 0.01f ? p.y * (1f + n * 0.4f) : 0f, p.z * (1f + n * 0.5f));
            }
            var bajo = new Vector3[L]; var medio = new Vector3[L];
            for (int i = 0; i < L; i++)
            {
                float a = i * Mathf.PI * 2f / L;
                bajo[i] = Sacudir(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.44f));
                medio[i] = Sacudir(new Vector3(Mathf.Cos(a + 0.4f) * 0.4f, 0.45f, Mathf.Sin(a + 0.4f) * 0.35f));
            }
            var punta = Sacudir(new Vector3(0.05f, 0.8f, 0f)); var centro = new Vector3(0f, 0.3f, 0f);
            for (int i = 0; i < L; i++)
            {
                int j = (i + 1) % L;
                g.QuadFuera(bajo[i], bajo[j], medio[j], medio[i], centro);
                g.TriFuera(medio[i], medio[j], punta, centro);
            }
            return Guardar(g.Crear("Op_Piedra_" + semilla));
        }

        // ------------------------------------------------------------------
        // Techos
        // ------------------------------------------------------------------
        static Mesh mAire, mTanque, mAntena, mCaseta, mVentila;
        static Material matMetalClaro, matMetalOsc, matCemento;
        static readonly List<Renderer> techosSueltos = new List<Renderer>();

        static void PrepararTechos()
        {
            techosN = 0;
            variantes.Clear();
            matMetalClaro = Mat("M_Op_MetalClaro"); matMetalOsc = Mat("M_Op_MetalOsc"); matCemento = Mat("M_Op_Cemento");
            mAire = MallaEquipoAire(); mTanque = MallaTanqueTecho(); mAntena = MallaAntena();
            mCaseta = MallaCaseta(); mVentila = MallaVentilacion();
            AssetDatabase.DeleteAsset(CarpetaMallas + "/Op_CasetaPuerta.asset");          // de una version previa: la puerta ya es submesh de la caseta
            AssetDatabase.DeleteAsset($"{CarpetaMallas}/{ContTechos}.asset");               // idem: los techos eran una malla unica por edificio
            surcosUsados.Clear();
            // Las casas de la ciudad tienen la losa del techo como cubo aparte ("Techo", "Techo_A"...): esa es la cara de arriba real.
            techosSueltos.Clear();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.enabled && r.name.StartsWith("Techo")) techosSueltos.Add(r);
        }

        static int VestirTecho(Transform raiz, MeshRenderer mr, Vector3 s)
        {
            if (mAire == null || matMetalClaro == null) return 0;
            var cubo = mr.transform;
            float yTecho = cubo.position.y + s.y * 0.5f;
            var b = mr.bounds;
            foreach (var r in techosSueltos)
            {
                if (r == null) continue;
                var c = r.bounds.center; float top = r.bounds.max.y;
                if (c.x > b.min.x && c.x < b.max.x && c.z > b.min.z && c.z < b.max.z && top > yTecho && top < yTecho + 2f) yTecho = top;
            }
            float y = yTecho - cubo.position.y;            // la raiz cancela la escala y el cubo solo gira en Y: metros locales

            const float celda = 2.6f;
            int nx = Mathf.FloorToInt((s.x - 1.8f) / celda), nz = Mathf.FloorToInt((s.z - 1.8f) / celda);
            if (nx < 1 || nz < 1) return 0;
            var celdas = new List<Vector2>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    celdas.Add(new Vector2(-nx * celda * 0.5f + (i + 0.5f) * celda, -nz * celda * 0.5f + (j + 0.5f) * celda));
            var rnd = new System.Random(Hash(cubo.name + cubo.position.ToString("0")));
            for (int i = celdas.Count - 1; i > 0; i--) { int k = rnd.Next(i + 1); (celdas[i], celdas[k]) = (celdas[k], celdas[i]); }

            float area = s.x * s.z;
            int cuantos = Mathf.Min(Mathf.Clamp(Mathf.RoundToInt(area / 45f), 1, 7), Mathf.Max(1, celdas.Count / 2));
            // la caseta toma el color del edificio si es de paleta; las torres de relleno tienen textura de oficina (ventanas)
            var matEdif = mr.sharedMaterial;
            bool paleta = matEdif != null && matEdif.HasProperty("_BaseMap") && matEdif.GetTextureScale("_BaseMap").sqrMagnitude < 1e-6f;
            var matCaseta = paleta ? matEdif : matCemento;

            // Instancias de 5 mallas compartidas (no una malla combinada por edificio: 117 mallas unicas pesaban 11 MB de asset).
            // El nombre no empieza con "Techo": PrepararTechos y otras busquedas toman los cubos "Techo*" como losas reales.
            var grupo = new GameObject("EquiposDeTecho").transform;
            grupo.SetParent(raiz, false);
            bool antena = false; int n = 0;
            for (int i = 0; i < cuantos; i++)
            {
                var p = celdas[i];
                var pos = new Vector3(p.x, y, p.y); float yaw = rnd.Next(4) * 90f;
                if (i == 0 && area > 80f) { PonerPieza(grupo, mCaseta, pos, yaw, matCaseta, matMetalOsc); n++; }
                else if (i == 1 && rnd.Next(3) == 0) { PonerPieza(grupo, mTanque, pos, yaw, matMetalClaro); n++; }
                else
                {
                    int d = rnd.Next(10);
                    if (d < 6) PonerPieza(grupo, mAire, pos, yaw, matMetalClaro);
                    else if (d < 9 || antena) PonerPieza(grupo, mVentila, pos, yaw, matMetalOsc);
                    else { PonerPieza(grupo, mAntena, pos, yaw, matMetalOsc); antena = true; }
                    n++;
                }
            }
            if (n == 0) { Object.DestroyImmediate(grupo.gameObject); return 0; }
            techosN++;
            return n;
        }

        static void PonerPieza(Transform padre, Mesh m, Vector3 pos, float yaw, params Material[] mats)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------
        // Surcos y torres (cambio de malla; collider y escala intactos)
        // ------------------------------------------------------------------
        static bool VestirSurco(MeshRenderer mr)
        {
            var mf = mr.GetComponent<MeshFilter>();
            var s = mr.transform.localScale;
            bool aX = s.x > s.z;
            float W = aX ? s.z : s.x, L = aX ? s.x : s.z, H = s.y;
            if (L < 3f || W > 4f || H < 0.1f) return false;
            // la malla se arma en la unidad del cubo: se cuantizan las medidas para compartirla (los surcos van de 0,55 a 0,75 de alto;
            // sin esto salian 21 mallas casi iguales). La diferencia queda en unos centimetros del lomo.
            W = Mathf.Round(W * 10f) / 10f; H = Mathf.Max(0.1f, Mathf.Round(H * 10f) / 10f); L = Mathf.Round(L);
            var mats = mr.sharedMaterials;
            var cultivo = mats.Length > 1 ? mats[1] : mats[0];
            var tierra = Mat("M_Op_SurcoTierra");
            if (cultivo == null || tierra == null) return false;

            // en metros: el cubo va de -H/2 a +H/2 y estaba medio enterrado; el lomo sube hasta 3/4 y las matas siguen arriba
            var lomo = new Gen(); var matas = new Gen();
            float y0 = -H * 0.5f, y1 = -H * 0.5f + H * 0.75f, z0 = -L * 0.5f, z1 = L * 0.5f, cen = 0f;
            var bajo = new[] { new Vector3(-W * 0.5f, y0, z0), new Vector3(W * 0.5f, y0, z0), new Vector3(W * 0.5f, y0, z1), new Vector3(-W * 0.5f, y0, z1) };
            var alto = new[] { new Vector3(-W * 0.2f, y1, z0 + 0.35f), new Vector3(W * 0.2f, y1, z0 + 0.35f), new Vector3(W * 0.2f, y1, z1 - 0.35f), new Vector3(-W * 0.2f, y1, z1 - 0.35f) };
            var centro = new Vector3(0f, y0, 0f);
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; lomo.QuadFuera(bajo[i], bajo[j], alto[j], alto[i], centro); }
            lomo.QuadFuera(alto[0], alto[1], alto[2], alto[3], centro);
            // matas anchas y juntas: desde la camara RTS el surco se tiene que leer verde, con la tierra apenas entre hileras
            int n = Mathf.Max(2, Mathf.FloorToInt((L - 0.6f) / 0.55f));
            for (int i = 0; i < n; i++)
            {
                float z = z0 + 0.45f + i * (L - 0.9f) / (n - 1);
                float jx = (Ruido(i * 1.7f, 0f, 3f, 11) - 0.5f) * W * 0.15f, alto1 = 0.4f + Ruido(i * 2.3f, 1f, 0f, 5) * 0.22f;
                float ra = W * (0.26f + Ruido(i * 3.1f, 2f, 1f, 7) * 0.08f);
                matas.Prisma(cen + jx, z, y1 - 0.02f, y1 + alto1 * 0.45f, ra * 0.25f, ra * 0.25f, ra, ra, 5, false);
                matas.Prisma(cen + jx, z, y1 + alto1 * 0.45f, y1 + alto1, ra, ra, ra * 0.15f, ra * 0.15f, 5, true);
            }
            var k = new Vector3(1f / W, 1f / H, 1f / L);
            Escalar(lomo, k); Escalar(matas, k);
            if (aX) { lomo.GirarY90(); matas.GirarY90(); }
            string nombre = "Op_Surco_" + F(W) + "x" + F(H) + "x" + F(L) + (aX ? "X" : "Z");
            surcosUsados.Add(nombre);
            if (surcosHechos.TryGetValue(nombre, out var hecha) && hecha != null) { mf.sharedMesh = hecha; mr.sharedMaterials = new[] { tierra, cultivo }; return true; }
            var lote = new Lote();
            lote.Agregar(lomo, Matrix4x4.identity, tierra);
            lote.Agregar(matas, Matrix4x4.identity, cultivo);
            var mesh = Guardar(lote.Construir(nombre, out var orden));
            surcosHechos[nombre] = mesh;
            mf.sharedMesh = mesh;
            mr.sharedMaterials = orden;
            return true;
        }

        static readonly HashSet<string> surcosUsados = new HashSet<string>();
        static readonly Dictionary<string, Mesh> surcosHechos = new Dictionary<string, Mesh>();

        // Borra las mallas de surco de corridas anteriores que ya nadie usa.
        static void LimpiarSurcosViejos()
        {
            foreach (var guid in AssetDatabase.FindAssets("Op_Surco_ t:Mesh", new[] { CarpetaMallas }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (!surcosUsados.Contains(System.IO.Path.GetFileNameWithoutExtension(ruta))) AssetDatabase.DeleteAsset(ruta);
            }
            surcosHechos.Clear();
        }

        static bool VestirTorrePata(MeshRenderer mr)
        {
            var mf = mr.GetComponent<MeshFilter>();
            var s = mr.transform.localScale;
            if (s.y < 4f || Mathf.Min(s.x, s.z) < 1f) return false;
            var g = new Gen();
            float hx = s.x * 0.5f - 0.18f, hz = s.z * 0.5f - 0.18f, y0 = -s.y * 0.5f, y1 = s.y * 0.5f;
            g.Caja(-s.x * 0.5f, s.x * 0.5f, y0, y0 + 0.35f, -s.z * 0.5f, s.z * 0.5f);              // zapata
            var esq = new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
            foreach (var e in esq) Barra(g, new Vector3(e.x, y0 + 0.35f, e.y), new Vector3(e.x, y1, e.y), 0.13f);
            int seg = Mathf.Max(2, Mathf.RoundToInt((s.y - 0.35f) / 2.4f));
            float dy = (s.y - 0.35f) / seg;
            for (int k = 0; k <= seg; k++)
            {
                float y = y0 + 0.35f + k * dy - (k == seg ? 0.08f : 0f);
                for (int i = 0; i < 4; i++) { var a = esq[i]; var b = esq[(i + 1) % 4]; Barra(g, new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y), 0.07f); }
                if (k == seg) break;
                float ya = y, yb = y + dy;
                for (int i = 0; i < 4; i++)
                {
                    var a = esq[i]; var b = esq[(i + 1) % 4];
                    Barra(g, new Vector3(a.x, ya, a.y), new Vector3(b.x, yb, b.y), 0.045f);
                    Barra(g, new Vector3(b.x, ya, b.y), new Vector3(a.x, yb, a.y), 0.045f);
                }
            }
            Escalar(g, new Vector3(1f / s.x, 1f / s.y, 1f / s.z));
            mf.sharedMesh = Guardar(g.Crear("Op_TorrePata_" + F(s.x) + "x" + F(s.y) + "x" + F(s.z)));
            return true;
        }

        // ------------------------------------------------------------------
        // Suelo del desierto
        // ------------------------------------------------------------------
        static string VestirSuelo()
        {
            Transform ground = null, losa = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "Ground" && t.GetComponent<MeshRenderer>() != null) ground = t;
                else if (t.name == "Losa_Arena" && t.GetComponent<MeshRenderer>() != null) losa = t;
            }
            if (ground == null) return "sin Ground";
            var gr = ground.GetComponent<MeshRenderer>();
            var gb = gr.bounds; float ySuelo = gb.max.y;
            var lr = losa != null ? losa.GetComponent<MeshRenderer>() : null;
            var lb = lr != null ? lr.bounds : new Bounds(Vector3.one * 1e6f, Vector3.zero);
            bool EnLosa(float x, float z) => x > lb.min.x && x < lb.max.x && z > lb.min.z && z < lb.max.z;
            float YEn(float x, float z) => EnLosa(x, z) ? lb.max.y : ySuelo;

            // --- ocupacion: celdas de 2 m tapadas por cualquier render o collider a ras del piso (calles, pisos, props, muros)
            const float C = 2f;
            int N = Mathf.CeilToInt(gb.size.x / C), M = Mathf.CeilToInt(gb.size.z / C);
            var ocupado = new bool[N, M];
            void Marcar(Bounds b, float margen)
            {
                int i0 = Mathf.Max(0, (int)((b.min.x - margen - gb.min.x) / C)), i1 = Mathf.Min(N - 1, (int)((b.max.x + margen - gb.min.x) / C));
                int j0 = Mathf.Max(0, (int)((b.min.z - margen - gb.min.z) / C)), j1 = Mathf.Min(M - 1, (int)((b.max.z + margen - gb.min.z) / C));
                for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) ocupado[i, j] = true;
            }
            bool Libre(float x, float z, float r)
            {
                int i0 = (int)((x - r - gb.min.x) / C), i1 = (int)((x + r - gb.min.x) / C), j0 = (int)((z - r - gb.min.z) / C), j1 = (int)((z + r - gb.min.z) / C);
                if (i0 < 1 || j0 < 1 || i1 >= N - 1 || j1 >= M - 1) return false;
                for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) if (ocupado[i, j]) return false;
                return true;
            }
            void Ocupar(float x, float z, float r) => Marcar(new Bounds(new Vector3(x, 0f, z), new Vector3(r * 2f, 0f, r * 2f)), 0f);

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform == ground || r.transform == losa || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.x > 250f || b.size.z > 250f || b.min.y > ySuelo + 3f) continue;   // horizonte y montañas / lamparas y techos en el aire
                Marcar(b, 1.2f);
            }
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!c.enabled || c.transform == ground) continue;
                var b = c.bounds;
                if (b.size.x > 250f || b.size.z > 250f || b.min.y > ySuelo + 3f) continue;
                Marcar(b, 1.2f);
            }

            // --- sectores
            var raiz = Raiz_(ground, ground.localScale);
            var aLocal = raiz.worldToLocalMatrix;            // traslacion pura: la raiz cancela la escala del suelo
            const int S = 6;
            var gens = new Dictionary<Material, Gen>[S, S];
            var lotes = new Lote[S, S];
            for (int i = 0; i < S; i++) for (int j = 0; j < S; j++) { gens[i, j] = new Dictionary<Material, Gen>(); lotes[i, j] = new Lote(); }
            (int, int) Sector(float x, float z) => (Mathf.Clamp((int)((x - gb.min.x) / gb.size.x * S), 0, S - 1), Mathf.Clamp((int)((z - gb.min.z) / gb.size.z * S), 0, S - 1));
            Gen GenDe(float x, float z, Material m) { var (i, j) = Sector(x, z); if (!gens[i, j].TryGetValue(m, out var g)) gens[i, j][m] = g = new Gen(); return g; }

            var baseSuelo = gr.sharedMaterial; var baseArena = lr != null ? lr.sharedMaterial : baseSuelo;
            // cada mancha es un nucleo con el tono pleno y un anillo exterior a medio camino: sin transparencias, el borde no queda duro
            Material V(bool arena, int tipo, bool suave = false)
            {
                var bm = arena ? baseArena : baseSuelo;
                Color k;
                switch (tipo)
                {
                    case 0: k = new Color(0.84f, 0.83f, 0.82f); break;
                    case 1: k = new Color(1.12f, 1.10f, 1.05f); break;
                    case 2: k = new Color(1.04f, 0.95f, 0.86f); break;
                    default: return Variante(bm, "Huella", new Color(0.70f, 0.68f, 0.66f));
                }
                if (suave) k = Color.Lerp(Color.white, k, 0.5f);
                return Variante(bm, (tipo == 0 ? "Oscura" : tipo == 1 ? "Clara" : "Tierra") + (suave ? "Suave" : ""), k);
            }
            var rnd = new System.Random(4242);
            float U() => (float)rnd.NextDouble();
            float losaBorde = lb.min.z;

            // --- manchas irregulares (sin superponerse entre si: mismas alturas de dibujo)
            int manchas = 0;
            for (int intento = 0; intento < 6000 && manchas < 650; intento++)
            {
                float x = Mathf.Lerp(gb.min.x, gb.max.x, U()), z = Mathf.Lerp(gb.min.z, gb.max.z, U());
                float R = 2.5f + 6.5f * U() * U(), asp = 1f + 1.2f * U(), ang = U() * Mathf.PI;
                float Rmax = R * asp * 1.15f;
                if (!Libre(x, z, Rmax)) continue;
                if (lr != null && Mathf.Abs(z - losaBorde) < Rmax + 1f && x > lb.min.x && x < lb.max.x) continue;   // no cruzar el borde de la losa de arena
                int tipo = U() < 0.4f ? 0 : U() < 0.6f ? 1 : 2;
                bool arena = EnLosa(x, z);
                float y = YEn(x, z) + 0.012f + tipo * 0.008f;
                foreach (bool nucleo in new[] { false, true })
                {
                    var g = GenDe(x, z, V(arena, tipo, !nucleo));
                    float yy = y + (nucleo ? 0.004f : 0f), escala = nucleo ? 0.62f : 1f;
                    // abanico indexado: el piso es plano, la normal promediada sigue siendo "arriba" y pesa un tercio que con caras sueltas
                    const int P = 18; int b0 = g.v.Count;
                    g.v.Add(new Vector3(x, yy, z)); g.uv.Add(Vector2.zero);
                    for (int k = 0; k < P; k++)
                    {
                        float a = k * Mathf.PI * 2f / P;
                        float rr = R * escala * (0.7f + 0.45f * Ruido(k * 0.9f + (nucleo ? 5f : 0f), intento * 0.37f, 0f, 3));
                        float lx = Mathf.Cos(a) * rr * asp, lz = Mathf.Sin(a) * rr;
                        g.v.Add(new Vector3(x + lx * Mathf.Cos(ang) - lz * Mathf.Sin(ang), yy, z + lx * Mathf.Sin(ang) + lz * Mathf.Cos(ang))); g.uv.Add(Vector2.zero);
                    }
                    for (int k = 0; k < P; k++) TriArriba(g, b0, b0 + 1 + k, b0 + 1 + (k + 1) % P);
                }
                Ocupar(x, z, Rmax * 0.8f);
                manchas++;
            }

            // --- huellas de vehiculo: dos bandas paralelas que serpentean hasta toparse con algo
            int huellas = 0;
            for (int intento = 0; intento < 400 && huellas < 26; intento++)
            {
                float x = Mathf.Lerp(gb.min.x, gb.max.x, U()), z = Mathf.Lerp(gb.min.z, gb.max.z, U());
                if (!Libre(x, z, 3f)) continue;
                float rumbo = U() * Mathf.PI * 2f, giro = (U() - 0.5f) * 0.08f;
                var pts = new List<Vector2> { new Vector2(x, z) };
                for (int k = 0; k < 40; k++)
                {
                    rumbo += giro + (U() - 0.5f) * 0.05f;
                    var sig = pts[pts.Count - 1] + new Vector2(Mathf.Cos(rumbo), Mathf.Sin(rumbo)) * 2f;
                    if (!Libre(sig.x, sig.y, 1.8f) || (lr != null && Mathf.Abs(sig.y - losaBorde) < 2f)) break;
                    pts.Add(sig);
                }
                if (pts.Count < 10) continue;
                bool arena = EnLosa(x, z); var mat = V(arena, 3);
                var g = GenDe(x, z, mat);
                foreach (float lado in new[] { -0.8f, 0.8f })
                {
                    int b0 = g.v.Count;
                    for (int k = 0; k < pts.Count; k++)
                    {
                        var d = (pts[Mathf.Min(k + 1, pts.Count - 1)] - pts[Mathf.Max(k - 1, 0)]).normalized; var nrm = new Vector2(-d.y, d.x);
                        float yk = YEn(pts[k].x, pts[k].y) + 0.034f;
                        var p0 = pts[k] + nrm * (lado - 0.18f); var p1 = pts[k] + nrm * (lado + 0.18f);
                        g.v.Add(new Vector3(p0.x, yk, p0.y)); g.v.Add(new Vector3(p1.x, yk, p1.y)); g.uv.Add(Vector2.zero); g.uv.Add(Vector2.zero);
                    }
                    for (int k = 0; k + 1 < pts.Count; k++)
                    {
                        int i = b0 + k * 2;
                        TriArriba(g, i, i + 1, i + 3); TriArriba(g, i, i + 3, i + 2);
                    }
                }
                foreach (var p in pts) Ocupar(p.x, p.y, 1.2f);
                huellas++;
            }

            // --- piedras: grupos de 2 a 5, medio enterradas (combinadas en el sector)
            var piedras = new[] { MallaPiedra(1), MallaPiedra(2), MallaPiedra(3) };
            var matRoca = Mat("M_Op_Roca");
            int grupos = 0, nPiedras = 0;
            for (int intento = 0; intento < 3000 && grupos < 110; intento++)
            {
                float x = Mathf.Lerp(gb.min.x, gb.max.x, U()), z = Mathf.Lerp(gb.min.z, gb.max.z, U());
                if (!Libre(x, z, 2f)) continue;
                int k = 2 + rnd.Next(4);
                var (si, sj) = Sector(x, z);
                for (int q = 0; q < k; q++)
                {
                    float px = x + (U() - 0.5f) * 3f, pz = z + (U() - 0.5f) * 3f, tam = 0.25f + U() * U() * 0.8f;
                    var pos = new Vector3(px, YEn(px, pz) - tam * 0.2f, pz);
                    var m4 = aLocal * Matrix4x4.TRS(pos, Quaternion.Euler(0f, U() * 360f, 0f), new Vector3(tam * (0.9f + U() * 0.5f), tam, tam * (0.9f + U() * 0.5f)));
                    lotes[si, sj].Agregar(piedras[rnd.Next(piedras.Length)], m4, matRoca);
                    nPiedras++;
                }
                Ocupar(x, z, 2f);
                grupos++;
            }

            // --- arbustos y crateres del kit: instancias estaticas (no se destruyen: el lote estatico las junta en juego)
            int arbustos = 0, crateres = 0;
            Transform PonerEstatico(string prefab, Vector3 pos, float yaw, float esc)
            {
                var p = P(prefab); if (p == null) return null;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(p, raiz);
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
                }
                inst.transform.position = pos; inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f); inst.transform.localScale = Vector3.one * esc;
                return inst.transform;
            }
            for (int intento = 0; intento < 3000 && arbustos < 80; intento++)
            {
                float x = Mathf.Lerp(gb.min.x, gb.max.x, U()), z = Mathf.Lerp(gb.min.z, gb.max.z, U());
                float d = U(); string pf = d < 0.55f ? "P_Env_Arbusto_Chico" : d < 0.9f ? "P_Env_Arbusto_Medio" : "P_Env_Arbusto_Grande";
                float r = d < 0.55f ? 1f : d < 0.9f ? 1.5f : 2.2f;
                if (!Libre(x, z, r)) continue;
                if (PonerEstatico(pf, new Vector3(x, YEn(x, z) - 0.05f, z), U() * 360f, 0.8f + U() * 0.45f) == null) break;
                Ocupar(x, z, r); arbustos++;
            }
            for (int intento = 0; intento < 1500 && crateres < 12; intento++)
            {
                float x = Mathf.Lerp(-150f, 150f, U()), z = Mathf.Lerp(gb.min.z, 40f, U());       // la aproximacion a la base: zona bombardeada
                if (!Libre(x, z, 3.5f)) continue;
                if (PonerEstatico("P_Env_Crater", new Vector3(x, YEn(x, z) - 0.12f, z), U() * 360f, 0.9f + U() * 0.6f) == null) break;
                Ocupar(x, z, 3.5f); crateres++;
            }

            // --- volcado de sectores
            int sectores = 0;
            for (int i = 0; i < S; i++)
                for (int j = 0; j < S; j++)
                {
                    foreach (var kv in gens[i, j]) lotes[i, j].Agregar(kv.Value, aLocal, kv.Key);
                    var go = PonerLote(raiz, lotes[i, j], $"Suelo_{i}_{j}", ContSuelo);
                    if (go == null) continue;
                    go.GetComponent<MeshRenderer>().receiveShadows = true;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    sectores++;
                }
            return $"{manchas} manchas, {huellas} huellas de vehiculo, {nPiedras} piedras en {grupos} grupos, {arbustos} arbustos y {crateres} crateres en {sectores} sectores";
        }
    }
}
