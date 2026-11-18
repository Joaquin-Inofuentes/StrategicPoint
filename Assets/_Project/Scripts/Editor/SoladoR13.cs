using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Ronda 13 · T15: la plaza deja de ser un damero de 100 cubos "Adoquin" y pasa a ser un solado real: texturas procedurales (generadas en C#, NO se
    // descarga ninguna imagen: las fotos de plazas porteñas y de Lavalle son solo referencia visual) con normal map y smoothness por material,
    // UV en metros, guardas de granito oscuro en cruz y perimetro, cordon, y sendas peatonales en las 4 bocas. Tambien trae los materiales y las
    // mallas combinadas que reusan T9 (veredas), T13 (hormigon con juntas) y T16.
    public static partial class OperacionBuilder
    {
        const string PrefabPlazaCivica = "Assets/_Project/Prefabs/Escena/Elementos/P_Op_Ciudad_PlazaCivica.prefab";
        const string PrefabPatios = "Assets/_Project/Prefabs/Escena/Elementos/P_Op_Cam2_Patios.prefab";

        // ---------------------------------------------------------------------------------------------------------------------
        // Texturas procedurales R13 (brillo en gris; el material pone el tinte) + normal map derivado de la altura (Sobel)
        // ---------------------------------------------------------------------------------------------------------------------
        static readonly Dictionary<string, Texture2D[]> texturasR13 = new Dictionary<string, Texture2D[]>();
        public static bool ForzarTexturasR13;   // true = regenera los PNG aunque ya existan

        static void PixelR13(string nombre, int x, int y, int N, RuidoT ruido, out float b, out float a)
        {
            float u = (float)x / N, v = (float)y / N, fino = Hash(x, y, 7);
            b = 0.8f; a = 0.5f;
            switch (nombre)
            {
                case "HormigonJuntas":   // losa de 3 x 3 m con junta de ~2 cm y un biselito
                {
                    float f = ruido.Fbm(u, v);
                    b = 0.78f + (f - 0.5f) * 0.28f + (fino - 0.5f) * 0.06f;
                    a = f * 0.6f + fino * 0.2f;
                    if (x < 2 || y < 2) { b = 0.30f; a = 0f; }
                    else if (x < 4 || y < 4) { b *= 0.85f; a *= 0.5f; }
                    break;
                }
                case "Granito":          // tile de 1,2 m: 3 filas de losas de 0,6 x 0,4 m trabadas (media losa de corrimiento por fila), con veta y motitas
                {
                    int fila = Mathf.FloorToInt(v * 3f); float fy = v * 3f - fila;
                    float uu = u * 2f + (fila % 2 == 0 ? 0f : 0.5f); int col = Mathf.FloorToInt(uu); float fx = uu - col;
                    int cm = ((col % 2) + 2) % 2;
                    float tono = 0.58f + Hash(cm, fila, 3) * 0.22f;
                    float f = ruido.Fbm(u, v);
                    float vena = Mathf.Abs(Mathf.Sin((u * 3f + v * 5f + f * 2f) * Mathf.PI * 2f)); vena = Mathf.Pow(vena, 10f);
                    b = tono + (f - 0.5f) * 0.14f - vena * 0.16f;
                    if (fino > 0.92f) b += 0.12f; else if (fino < 0.05f) b -= 0.14f;
                    a = 0.7f + f * 0.2f;
                    if (fx < 0.013f || fy < 0.025f) { b = 0.22f; a = 0f; }
                    break;
                }
                case "Adoquin":          // tile de 1,6 m = 16 celdas de 0,1 m; ladrillos de 0,2 x 0,1 m en espina de pez
                {
                    float cx = u * 16f, cy = v * 16f; int ca = Mathf.FloorToInt(cx), cb = Mathf.FloorToInt(cy);
                    float fx = cx - ca, fy = cy - cb;
                    int s = (((ca + cb) % 4) + 4) % 4;
                    float xl, yl; int ia, ib; bool horiz;
                    if (s == 0) { horiz = true; xl = fx / 2f; yl = fy; ia = ca; ib = cb; }
                    else if (s == 1) { horiz = true; xl = (1f + fx) / 2f; yl = fy; ia = ca - 1; ib = cb; }
                    else if (s == 2) { horiz = false; xl = fx; yl = fy / 2f; ia = ca; ib = cb; }
                    else { horiz = false; xl = fx; yl = (1f + fy) / 2f; ia = ca; ib = cb - 1; }
                    ia = ((ia % 16) + 16) % 16; ib = ((ib % 16) + 16) % 16;
                    float jw = 0.07f, jx = horiz ? jw / 2f : jw, jy = horiz ? jw : jw / 2f;
                    float tono = 0.60f + Hash(ia, ib, 5) * 0.30f;
                    float f = ruido.Fbm(u, v);
                    bool junta = xl < jx || xl > 1f - jx || yl < jy || yl > 1f - jy;
                    b = junta ? 0.24f : tono + (f - 0.5f) * 0.10f + (fino - 0.5f) * 0.05f;
                    a = junta ? 0f : 0.75f + Hash(ia, ib, 9) * 0.10f;
                    break;
                }
                case "Vereda":           // tile de 0,6 m = 3 x 3 baldosas de 0,2 m "de 9 panes" (3 x 3 cuadraditos en relieve)
                {
                    float cx = u * 3f, cy = v * 3f; int bi = Mathf.FloorToInt(cx), bj = Mathf.FloorToInt(cy);
                    float fx = cx - bi, fy = cy - bj;
                    float f = ruido.Fbm(u, v);
                    float tono = 0.64f + Hash(bi, bj, 8) * 0.07f + (f - 0.5f) * 0.10f;
                    bool junta = fx < 0.03f || fx > 0.97f || fy < 0.03f || fy > 0.97f;
                    float px = (fx - 0.08f) / 0.84f * 3f, py = (fy - 0.08f) / 0.84f * 3f;
                    bool dentro = px >= 0f && px < 3f && py >= 0f && py < 3f;
                    float qx = px - Mathf.Floor(px), qy = py - Mathf.Floor(py);
                    bool pan = dentro && qx > 0.12f && qx < 0.88f && qy > 0.12f && qy < 0.88f;
                    if (junta) { b = 0.28f; a = 0f; }
                    else if (pan) { b = tono + 0.06f; a = 0.95f; }
                    else { b = tono - 0.12f; a = 0.25f; }
                    b += (fino - 0.5f) * 0.05f;
                    break;
                }
                case "Panel":            // tile de 2 m: paneles de chapa militar de 1 x 1 m con junta hundida y remaches
                {
                    float f = ruido.Fbm(u, v);
                    b = 0.74f + (f - 0.5f) * 0.22f + (fino - 0.5f) * 0.06f; a = 0.6f + f * 0.2f;
                    int mx = x % 128, my = y % 128;
                    if (mx < 2 || my < 2) { b *= 0.55f; a = 0.1f; }
                    bool remache = (mx > 8 && mx < 12 || mx > 116 && mx < 120) && (my > 8 && my < 12 || my > 116 && my < 120);
                    if (remache) { b += 0.12f; a = 0.95f; }
                    break;
                }
                case "Cebra":            // tile de 1 m: franja blanca de 0,5 m y hueco de asfalto de 0,5 m (se alterna en U)
                {
                    float f = ruido.Fbm(u, v);
                    bool blanco = u < 0.5f;
                    b = blanco ? 0.92f - f * 0.30f - (fino > 0.95f ? 0.25f : 0f) : 0.20f + f * 0.12f + (fino - 0.5f) * 0.08f;
                    a = blanco ? 0.55f + f * 0.2f : fino * 0.35f + 0.3f;
                    break;
                }
                default:
                    b = 0.8f + (fino - 0.5f) * 0.1f; a = fino;
                    break;
            }
            b = Mathf.Clamp01(b); a = Mathf.Clamp01(a);
        }

        static float FuerzaNormalR13(string nombre) => nombre == "Vereda" ? 4.5f : nombre == "Adoquin" ? 4f : nombre == "Granito" ? 2.2f : nombre == "Cebra" ? 0.7f : nombre == "Panel" ? 2.0f : 1.6f;

        /// <summary>[0] = albedo en gris, [1] = normal map. Se guardan en Textures/Operacion/T_R13_&lt;nombre&gt;(_N).png.</summary>
        static Texture2D[] TexturaR13(string nombre)
        {
            if (texturasR13.TryGetValue(nombre, out var hecha) && hecha[0] != null && hecha[1] != null) return hecha;
            string rutaC = $"{CarpetaTex}/T_R13_{nombre}.png", rutaN = $"{CarpetaTex}/T_R13_{nombre}_N.png";
            if (!ForzarTexturasR13 && File.Exists(rutaC) && File.Exists(rutaN))
            {
                var c0 = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaC); var n0 = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN);
                if (c0 != null && n0 != null) { texturasR13[nombre] = new[] { c0, n0 }; return texturasR13[nombre]; }
            }
            const int N = 256;
            var ruido = new RuidoT(nombre.Length * 977 + nombre[0] * 31, 4, 8, 16, 32);
            var brillo = new float[N, N]; var altura = new float[N, N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { PixelR13(nombre, x, y, N, ruido, out float b, out float a); brillo[x, y] = b; altura[x, y] = a; }
            if (!Directory.Exists(CarpetaTex)) Directory.CreateDirectory(CarpetaTex);
            var colores = new Color32[N * N]; var normales = new Color32[N * N];
            float fuerza = FuerzaNormalR13(nombre);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                byte g8 = (byte)Mathf.RoundToInt(brillo[x, y] * 255f);
                colores[y * N + x] = new Color32(g8, g8, g8, 255);
                float dx = altura[(x + 1) % N, y] - altura[(x - 1 + N) % N, y], dy = altura[x, (y + 1) % N] - altura[x, (y - 1 + N) % N];
                var nn = new Vector3(-dx * fuerza, -dy * fuerza, 1f).normalized;
                normales[y * N + x] = new Color32((byte)Mathf.RoundToInt((nn.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nn.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nn.z * 0.5f + 0.5f) * 255f), 255);
            }
            var tc = new Texture2D(N, N, TextureFormat.RGBA32, false); tc.SetPixels32(colores); tc.Apply();
            File.WriteAllBytes(rutaC, tc.EncodeToPNG()); Object.DestroyImmediate(tc);
            var tn = new Texture2D(N, N, TextureFormat.RGBA32, false); tn.SetPixels32(normales); tn.Apply();
            File.WriteAllBytes(rutaN, tn.EncodeToPNG()); Object.DestroyImmediate(tn);
            AssetDatabase.ImportAsset(rutaC, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(rutaN, ImportAssetOptions.ForceUpdate);
            ConfigurarImportador(rutaC, false); ConfigurarImportador(rutaN, true);
            var par = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(rutaC), AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN) };
            texturasR13[nombre] = par;
            return par;
        }

        /// <summary>
        /// Material M_Op_&lt;clave&gt; con textura R13 + normal map y smoothness propio; registra los metros por tile para que Cubo() use MallaDeCaja con UV en metros.
        /// Se vuelve a aplicar siempre (Mat() deja una celda plana del trimsheet).
        /// </summary>
        static Material MatTexR13(string clave, Color tinte, string textura, float metros, float relieve, float brillo)
        {
            var mat = Mat(clave, tinte);
            var par = TexturaR13(textura);
            mat.SetTexture("_BaseMap", par[0]);
            mat.SetTextureScale("_BaseMap", Vector2.one); mat.SetTextureOffset("_BaseMap", Vector2.zero);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", par[0]);
            mat.SetColor("_BaseColor", tinte);
            mat.SetTexture("_BumpMap", par[1]);
            mat.SetFloat("_BumpScale", relieve);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", brillo);
            metrosPorTile[mat] = metros;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MatVeredaR13() => MatTexR13("Vereda", new Color(0.80f, 0.78f, 0.74f), "Vereda", 0.6f, 1.0f, 0.10f);
        static Material MatGranitoR13() => MatTexR13("Plaza_Granito", new Color(0.80f, 0.76f, 0.72f), "Granito", 1.2f, 0.8f, 0.30f);
        static Material MatGuardaR13() => MatTexR13("Plaza_Guarda", new Color(0.36f, 0.35f, 0.36f), "Granito", 1.2f, 0.8f, 0.35f);
        static Material MatSendaR13() => MatTexR13("Senda_Cebra", Color.white, "Cebra", 1f, 0.4f, 0.12f);
        static Material MatAdoquinR13() => MatTexR13("Patio_Adoquin", new Color(0.66f, 0.62f, 0.58f), "Adoquin", 1.6f, 1.0f, 0.15f);

        // ---------------------------------------------------------------------------------------------------------------------
        // Malla combinada de cajas (sin cara inferior) con UV en metros del mundo: una malla por material, un renderer por malla
        // ---------------------------------------------------------------------------------------------------------------------
        struct CajaR13 { public Vector3 min, max; public bool girarUV; public CajaR13(Vector3 mn, Vector3 mx, bool girar = false) { min = mn; max = mx; girarUV = girar; } }

        static CajaR13 CajaXZ(float x0, float x1, float y0, float y1, float z0, float z1, bool girar = false) => new CajaR13(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), girar);

        static Mesh MallaDeCajasR13(string nombre, List<CajaR13> cajas, float tile)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>(); var tri = new List<int>();
            foreach (var cj in cajas)
            {
                var a = cj.min; var b = cj.max; var d = b - a;
                void Cara(Vector3 o, Vector3 eu, Vector3 ev, Vector3 nor, int tipo)
                {
                    int i0 = v.Count;
                    var ps = new[] { o, o + eu, o + eu + ev, o + ev };
                    foreach (var p in ps)
                    {
                        v.Add(p); n.Add(nor);
                        Vector2 q = tipo == 0 ? (cj.girarUV ? new Vector2(p.z, p.x) : new Vector2(p.x, p.z)) : tipo == 1 ? new Vector2(p.x, p.y) : new Vector2(p.z, p.y);
                        uv.Add(q / tile);
                        Vector3 t = tipo == 0 ? (cj.girarUV ? Vector3.forward : Vector3.right) : tipo == 1 ? Vector3.right : Vector3.forward;
                        tg.Add(new Vector4(t.x, t.y, t.z, -1f));
                    }
                    tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 1); tri.Add(i0); tri.Add(i0 + 3); tri.Add(i0 + 2);
                }
                Cara(new Vector3(a.x, a.y, a.z), new Vector3(d.x, 0, 0), new Vector3(0, d.y, 0), Vector3.back, 1);       // -Z
                Cara(new Vector3(b.x, a.y, b.z), new Vector3(-d.x, 0, 0), new Vector3(0, d.y, 0), Vector3.forward, 1);    // +Z
                Cara(new Vector3(a.x, a.y, b.z), new Vector3(0, 0, -d.z), new Vector3(0, d.y, 0), Vector3.left, 2);       // -X
                Cara(new Vector3(b.x, a.y, a.z), new Vector3(0, 0, d.z), new Vector3(0, d.y, 0), Vector3.right, 2);       // +X
                Cara(new Vector3(a.x, b.y, a.z), new Vector3(d.x, 0, 0), new Vector3(0, 0, d.z), Vector3.up, 0);          // +Y
            }
            EscenasR13.AsegurarCarpetaGenerada();
            string ruta = $"{EscenasR13.CarpetaGenerada}/{nombre}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            bool nueva = m == null;
            if (nueva) m = new Mesh { name = nombre }; else m.Clear();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTangents(tg); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            if (nueva) AssetDatabase.CreateAsset(m, ruta); else EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject PonerMallaR13(Transform padre, string nombre, Mesh m, Material mat)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return go;
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // T15
        // ---------------------------------------------------------------------------------------------------------------------
        [MenuItem("Strategic Point/Operacion/R13 T15 · Solado de la plaza y patios")]
        static void MenuSoladoDePlaza() => Debug.Log(SoladoDePlazaR13EscenaAbierta());

        public static string SoladoDePlazaR13() => SoladoDePlazaR13EscenaAbierta();

        /// <summary>
        /// T15. Edita el prefab P_Op_Ciudad_PlazaCivica: borra los 100 cubos 'Adoquin' y crea el grupo R13_Solado (4 cuadrantes de granito de 15,1 m a y=0,23,
        /// guardas de granito oscuro de 0,6 m en cruz y perimetro, cordon con 4 bocas de 12 m y 4 sendas peatonales). En P_Op_Cam2_Patios reemplaza cada damero
        /// 'Patio_Losa' por una sola losa de adoquin en espina de pez. Sin colliders, ignoreFromBuild. Sube los decals tapados de la plaza a y=0,245.
        /// Firma: OperacionBuilder.SoladoDePlazaR13EscenaAbierta(bool soloInforme = false)
        /// </summary>
        public static string SoladoDePlazaR13EscenaAbierta(bool soloInforme = false)
        {
            var sb = new StringBuilder("== R13 T15 solado de la plaza" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t15.txt");

            // ---- inventario (prefab de la plaza)
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPlazaCivica);
            if (pf == null) { sb.AppendLine("FALLO no existe " + PrefabPlazaCivica); return EscenasR13.Cerrar(sb, "r13_t15.txt"); }
            int adoquines = 0, rend = 0;
            foreach (Transform h in pf.transform) { if (h.name == "Adoquin") adoquines++; if (h.GetComponent<MeshRenderer>() != null) rend++; }
            sb.AppendLine($"plaza: {adoquines} Adoquin, {rend} renderers directos en el prefab");
            var pp = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPatios);
            int losas = 0;
            if (pp != null) foreach (var t in pp.GetComponentsInChildren<Transform>(true)) if (t.name == "Patio_Losa") losas++;
            sb.AppendLine($"patios: {losas} Patio_Losa en {(pp != null ? PrefabPatios : "(prefab inexistente)")}");

            // ---- decals tapados (se listan siempre)
            var decals = new List<Transform>();
            foreach (var t in EscenasR13.Todos())
            {
                if (t == null || !t.name.StartsWith("Decal_")) continue;
                var p = t.position;
                if (p.x > 305f && p.x < 339f && p.z > 211f && p.z < 245f && p.y < 0.2f) decals.Add(t);
            }
            sb.AppendLine($"decals que quedan bajo el solado (y < 0,2 en la plaza): {decals.Count}");
            if (soloInforme) return EscenasR13.Cerrar(sb, "r13_t15.txt");

            // ---- materiales
            var granito = MatGranitoR13(); var guarda = MatGuardaR13(); var senda = MatSendaR13(); var adoquin = MatAdoquinR13();
            AssetDatabase.SaveAssets();

            // ---- plaza
            const float cx = 322f, cz = 228f, y0 = 0.17f, y1 = 0.23f;
            bool okPlaza = EscenasR13.EditarPrefab(PrefabPlazaCivica, raiz =>
            {
                var borrar = new List<GameObject>();
                foreach (Transform h in raiz.transform) if (h.name == "Adoquin" || h.name == "R13_Solado") borrar.Add(h.gameObject);
                foreach (var g in borrar) Object.DestroyImmediate(g);
                var grupo = new GameObject("R13_Solado").transform; grupo.SetParent(raiz.transform, false);

                var quads = new List<CajaR13>();
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                {
                    float xa = sx > 0 ? cx + 0.3f : cx - 15.4f, xb = sx > 0 ? cx + 15.4f : cx - 0.3f;
                    float za = sz > 0 ? cz + 0.3f : cz - 15.4f, zb = sz > 0 ? cz + 15.4f : cz - 0.3f;
                    quads.Add(CajaXZ(xa, xb, y0, y1, za, zb));
                }
                var guardas = new List<CajaR13>
                {
                    // perimetro (0,6 m)
                    CajaXZ(cx - 16f, cx + 16f, y0, y1, cz + 15.4f, cz + 16f), CajaXZ(cx - 16f, cx + 16f, y0, y1, cz - 16f, cz - 15.4f),
                    CajaXZ(cx + 15.4f, cx + 16f, y0, y1, cz - 15.4f, cz + 15.4f), CajaXZ(cx - 16f, cx - 15.4f, y0, y1, cz - 15.4f, cz + 15.4f),
                    // cruz central (sin solapar: el eje E-O entero y el N-S partido en dos)
                    CajaXZ(cx - 15.4f, cx + 15.4f, y0, y1, cz - 0.3f, cz + 0.3f),
                    CajaXZ(cx - 0.3f, cx + 0.3f, y0, y1, cz + 0.3f, cz + 15.4f), CajaXZ(cx - 0.3f, cx + 0.3f, y0, y1, cz - 15.4f, cz - 0.3f),
                };
                var cordon = new List<CajaR13>();
                const float yc0 = 0.05f, yc1 = 0.25f, boca = 6f;
                foreach (int s in new[] { -1, 1 })
                {
                    float zA = s > 0 ? cz + 16f : cz - 16.3f, zB = s > 0 ? cz + 16.3f : cz - 16f;     // lados N y S
                    cordon.Add(CajaXZ(cx - 16.3f, cx - boca, yc0, yc1, zA, zB)); cordon.Add(CajaXZ(cx + boca, cx + 16.3f, yc0, yc1, zA, zB));
                    float xA = s > 0 ? cx + 16f : cx - 16.3f, xB = s > 0 ? cx + 16.3f : cx - 16f;     // lados E y O
                    cordon.Add(CajaXZ(xA, xB, yc0, yc1, cz - 16.3f, cz - boca)); cordon.Add(CajaXZ(xA, xB, yc0, yc1, cz + boca, cz + 16.3f));
                }
                const float ys0 = 0.075f, ys1 = 0.095f;
                var sendas = new List<CajaR13>
                {
                    CajaXZ(cx - 18f, cx - 16f, ys0, ys1, cz - 6f, cz + 6f, true), CajaXZ(cx + 16f, cx + 18f, ys0, ys1, cz - 6f, cz + 6f, true),   // x 304..306 y 338..340
                    CajaXZ(cx - 6f, cx + 6f, ys0, ys1, cz - 18f, cz - 16f), CajaXZ(cx - 6f, cx + 6f, ys0, ys1, cz + 16f, cz + 18f),             // z 210..212 y 244..246
                };
                PonerMallaR13(grupo, "R13_Solado_Granito", MallaDeCajasR13("R13_Plaza_Granito", quads, 1.2f), granito);
                PonerMallaR13(grupo, "R13_Solado_Guardas", MallaDeCajasR13("R13_Plaza_Guardas", guardas, 1.2f), guarda);
                PonerMallaR13(grupo, "R13_Solado_Cordon", MallaDeCajasR13("R13_Plaza_Cordon", cordon, 1.2f), guarda);
                PonerMallaR13(grupo, "R13_Solado_Sendas", MallaDeCajasR13("R13_Plaza_Sendas", sendas, 1f), senda);
            }, sb);
            sb.AppendLine(okPlaza ? $"plaza: {adoquines} Adoquin borrados; R13_Solado creado con 4 renderers" : "FALLO la plaza no se edito");

            // ---- patios (Caminos2)
            int clusters = 0;
            if (pp != null)
            {
                EscenasR13.EditarPrefab(PrefabPatios, raiz =>
                {
                    var losasT = new List<Renderer>();
                    foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == "Patio_Losa") { var r = t.GetComponent<Renderer>(); if (r != null) losasT.Add(r); }
                    var cajas = new List<Bounds>();
                    var usado = new bool[losasT.Count];
                    for (int i = 0; i < losasT.Count; i++)
                    {
                        if (usado[i]) continue;
                        var b = losasT[i].bounds; usado[i] = true;
                        bool crecio = true;
                        while (crecio)
                        {
                            crecio = false;
                            for (int j = 0; j < losasT.Count; j++)
                            {
                                if (usado[j]) continue;
                                var bj = losasT[j].bounds; var e = bj; e.Expand(0.06f);
                                if (e.Intersects(b)) { b.Encapsulate(bj); usado[j] = true; crecio = true; }
                            }
                        }
                        cajas.Add(b);
                    }
                    var borrar = new List<GameObject>();
                    foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == "Patio_Losa" || t.name == "R13_Solado_Patios") borrar.Add(t.gameObject);
                    foreach (var g in borrar) if (g != null) Object.DestroyImmediate(g);
                    var grupo = new GameObject("R13_Solado_Patios").transform; grupo.SetParent(raiz.transform, false);
                    for (int k = 0; k < cajas.Count; k++)
                    {
                        var b = cajas[k];
                        var lista = new List<CajaR13> { CajaXZ(b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z) };
                        PonerMallaR13(grupo, "R13_Solado_Patio_" + (k + 1), MallaDeCajasR13("R13_Patio_Adoquin_" + (k + 1), lista, 1.6f), adoquin);
                    }
                    clusters = cajas.Count;
                }, sb);
                sb.AppendLine($"patios: {losas} Patio_Losa -> {clusters} losas de adoquin");
            }

            // ---- decals de la plaza: arriba del solado (0,24)
            int subidos = 0;
            foreach (var t in decals) { if (t == null) continue; var p = t.position; t.position = new Vector3(p.x, 0.245f, p.z); EscenasR13.Sucia(t.gameObject.scene); subidos++; }
            sb.AppendLine($"decals subidos a y=0,245: {subidos}");

            AssetDatabase.SaveAssets();
            if (EscenasR13.EstaAbierta(EscenasR13.RutaSector("Ciudad")) && subidos > 0) EscenasR13.Guardar(sb);
            sb.Insert(0, "Solado de la plaza hecho. Pendiente (sin ejecutar): portar a ArmarPlazaCivica y Caminos2.Patio para que una reconstruccion no recree el damero. Fotos de referencia: anotarlas en Docs/RONDA_13/referencias_plaza.md (no se descargo ninguna imagen).\n");
            return EscenasR13.Cerrar(sb, "r13_t15.txt");
        }
    }
}
