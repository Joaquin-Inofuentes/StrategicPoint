using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido ("hay lineas separadoras entre los tiles; no deberian tener tiles y si decals para emular variedad y que no haya monotonia"):
    // las texturas ya no llevan juntas ni costuras; la variedad la dan decals sueltos (manchas, aceite, quemaduras, grietas, huellas, arena, humedad,
    // oxido) con alfa, colocados al azar sobre el piso y las paredes. Son quads sin colision ni navmesh, con material Lit transparente.
    public static partial class OperacionBuilder
    {
        static readonly Dictionary<string, Material> materialesDeDecal = new Dictionary<string, Material>();
        static Mesh quadDeDecal;
        public static string ResumenDecals { get; private set; } = "";

        // ---------------------------------------------------------------
        // Texturas con alfa
        // ---------------------------------------------------------------
        static Material MaterialDeDecal(string tipo, int variante)
        {
            string clave = tipo + variante;
            if (materialesDeDecal.TryGetValue(clave, out var hecho) && hecho != null) return hecho;
            const int N = 256;
            var ruido = new RuidoT((clave.GetHashCode() & 0xFFFF) + variante * 977, 4, 8, 16, 32);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;     // 0 centro .. 1 borde
                float f = ruido.Fbm(u, v), g = ruido.Octava(2, u, v);
                float borde = Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 2f) * 6f);   // alfa 0 en el marco del quad
                Color c = Color.black; float a = 0f;
                switch (tipo)
                {
                    case "Mancha":
                        a = Mathf.SmoothStep(0f, 1f, (0.85f - (r + (f - 0.5f) * 0.9f)) * 2.2f) * 0.8f;
                        c = Color.Lerp(new Color(0.16f, 0.12f, 0.08f), new Color(0.28f, 0.21f, 0.14f), g);
                        break;
                    case "Aceite":
                        a = Mathf.SmoothStep(0f, 1f, (0.75f - (r + (f - 0.5f) * 0.7f)) * 3f) * 0.75f;
                        c = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), new Color(0.09f, 0.08f, 0.12f), g);
                        break;
                    case "Quemado":
                    {
                        float m = 0.8f - (r + (ruido.Octava(3, u, v) - 0.5f) * 0.7f + (f - 0.5f) * 0.5f);
                        a = Mathf.SmoothStep(0f, 1f, m * 2.4f) * (0.55f + 0.4f * (1f - r));
                        c = Color.Lerp(new Color(0.02f, 0.02f, 0.02f), new Color(0.12f, 0.10f, 0.09f), g);
                        break;
                    }
                    case "Grieta":
                    {
                        float linea = Mathf.Abs(ruido.Octava(1, u, v) - 0.5f) + Mathf.Abs(ruido.Octava(2, u + 0.4f, v) - 0.5f) * 0.6f;
                        a = Mathf.Clamp01(1f - linea / 0.045f) * Mathf.Clamp01(1.15f - r) * 0.85f;
                        c = new Color(0.05f, 0.045f, 0.04f);
                        break;
                    }
                    case "Huellas":
                    {
                        float carril = Mathf.Min(Mathf.Abs(v - 0.34f), Mathf.Abs(v - 0.66f));
                        float surco = Mathf.Clamp01(1f - carril / 0.07f);
                        float taco = 0.6f + 0.4f * Mathf.Sin(u * Mathf.PI * 2f * 36f);
                        float irregular = Mathf.Clamp01(0.5f + (ruido.Octava(1, u, v) - 0.5f) * 1.6f);
                        a = surco * taco * irregular * 0.5f * Mathf.Clamp01((0.5f - Mathf.Abs(dx)) * 5f);
                        c = new Color(0.12f, 0.09f, 0.06f);
                        break;
                    }
                    case "Arena":
                    {
                        float onda = 0.5f + 0.5f * Mathf.Sin((u * 0.8f + v * 0.6f + f * 0.25f) * Mathf.PI * 2f * 9f);
                        a = Mathf.SmoothStep(0f, 1f, (0.95f - (r + (f - 0.5f) * 0.8f)) * 2f) * (0.45f + onda * 0.25f);
                        c = Color.Lerp(new Color(0.72f, 0.60f, 0.40f), new Color(0.88f, 0.76f, 0.54f), onda);
                        break;
                    }
                    case "Humedad":     // chorreado desde arriba, para paredes
                    {
                        float col = ruido.Octava(2, u, v * 0.06f, 1, 1) + ruido.Octava(1, u * 2f % 1f, 0.3f) * 0.3f;
                        float caida = Mathf.Pow(v, 1.4f);
                        a = Mathf.Clamp01((col - 0.40f) * 2.2f) * caida * 0.85f * Mathf.Clamp01(1.2f - Mathf.Pow(1f - v, 4f) * 0f);
                        c = Color.Lerp(new Color(0.05f, 0.07f, 0.05f), new Color(0.14f, 0.15f, 0.12f), g);
                        break;
                    }
                    case "Oxido":
                    {
                        float col = ruido.Octava(2, u, v * 0.08f, 1, 1);
                        a = Mathf.Clamp01((col - 0.45f) * 2.6f) * Mathf.Pow(v, 1.2f) * 0.9f;
                        c = Color.Lerp(new Color(0.36f, 0.16f, 0.07f), new Color(0.55f, 0.28f, 0.10f), g);
                        break;
                    }
                    case "Parche":      // mancha enorme y suave de tono: rompe la monotonia del piso a distancia
                        a = Mathf.SmoothStep(0f, 1f, (1f - (r + (f - 0.5f) * 0.7f)) * 1.1f) * 0.34f;
                        c = Color.Lerp(new Color(0.20f, 0.14f, 0.09f), new Color(0.55f, 0.43f, 0.28f), Mathf.SmoothStep(0f, 1f, (g - 0.3f) * 2.5f));   // degradado: sin bordes duros
                        break;
                    case "Grava":
                    {
                        float pied = Hash(x / 3, y / 3, 11);
                        float blob = Mathf.SmoothStep(0f, 1f, (0.9f - (r + (f - 0.5f) * 0.8f)) * 2.5f);
                        a = blob * (pied > 0.55f ? 0.8f : 0.1f);
                        c = pied > 0.8f ? new Color(0.62f, 0.56f, 0.46f) : new Color(0.26f, 0.22f, 0.18f);
                        break;
                    }
                    case "Polvo":
                        a = Mathf.SmoothStep(0f, 1f, (0.9f - (r + (f - 0.5f) * 0.6f)) * 2.5f) * 0.35f;
                        c = new Color(0.78f, 0.68f, 0.50f);
                        break;
                }
                a *= borde;
                px[y * N + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(Mathf.Clamp01(a) * 255f));
            }
            if (!Directory.Exists(CarpetaTex)) Directory.CreateDirectory(CarpetaTex);
            string rutaT = $"{CarpetaTex}/D_Op_{clave}.png";
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply();
            File.WriteAllBytes(rutaT, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(rutaT, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(rutaT) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaT);

            if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Operacion");
            string rutaM = $"{CarpetaMat}/M_Op_Decal_{clave}.mat";
            var m2 = AssetDatabase.LoadAssetAtPath<Material>(rutaM);
            if (m2 == null) { m2 = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_Decal_" + clave }; AssetDatabase.CreateAsset(m2, rutaM); }
            m2.SetFloat("_Surface", 1f); m2.SetFloat("_Blend", 0f);
            m2.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m2.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m2.SetFloat("_ZWrite", 0f);
            m2.SetOverrideTag("RenderType", "Transparent");
            m2.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m2.SetShaderPassEnabled("ShadowCaster", false);
            m2.renderQueue = 3000;
            m2.SetTexture("_BaseMap", tex); m2.SetColor("_BaseColor", Color.white);
            m2.SetFloat("_Smoothness", tipo == "Aceite" ? 0.7f : 0.05f);
            EditorUtility.SetDirty(m2);
            materialesDeDecal[clave] = m2;
            return m2;
        }

        // Quad apoyado sobre el piso (cara hacia arriba) o pegado a una pared (cara hacia 'normal').
        static GameObject PonerDecal(Transform padre, string tipo, Vector3 pos, Vector3 normal, float yawOrRoll, float ancho, float alto)
        {
            if (quadDeDecal == null) quadDeDecal = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var go = new GameObject("Decal_" + tipo);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            if (normal.y > 0.9f) go.transform.rotation = Quaternion.LookRotation(Vector3.down, new Vector3(Mathf.Sin(yawOrRoll * Mathf.Deg2Rad), 0f, Mathf.Cos(yawOrRoll * Mathf.Deg2Rad)));
            else go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            go.transform.localScale = new Vector3(ancho, alto, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quadDeDecal;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialDeDecal(tipo, (int)((Mathf.Abs(pos.x * 7.3f + pos.z * 3.1f)) % 4f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mod = go.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true;
            return go;
        }

        static void Decals(Transform g, Transform[] ruta, Vector2[] pts, System.Random rnd)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var gD = Grupo(g, "Decals");
            int nPiso = 0, nPared = 0, nRuta = 0;
            string[] piso = { "Mancha", "Mancha", "Aceite", "Quemado", "Grieta", "Grieta", "Polvo", "Arena", "Huellas", "Parche", "Parche", "Grava", "Grava" };
            float yDecal = 0.0f;
            // Altura real del piso en el punto: el de mas arriba que no sea un edificio (rayo corto desde 1,5 m).
            bool Suelo(Vector3 p, out float y)
            {
                y = 0.08f;
                if (Physics.CheckSphere(new Vector3(p.x, 0.9f, p.z), 0.45f, ~0, QueryTriggerInteraction.Ignore)) return false;   // hay algo (edificio, prop)
                if (Physics.Raycast(new Vector3(p.x, 1.5f, p.z), Vector3.down, out var h, 3f, ~0, QueryTriggerInteraction.Ignore)) y = h.point.y + 0.075f + (float)rnd.NextDouble() * 0.015f;   // los pisos de cubo miden 5 cm sin colision: el decal va por encima
                return true;
            }
            void Zona(float x0, float x1, float z0, float z1, int cuantos, float tam)
            {
                for (int i = 0, intentos = 0; i < cuantos && intentos < cuantos * 6; intentos++)
                {
                    var p = new Vector3(R(x0, x1), yDecal, R(z0, z1));
                    if (!Suelo(p, out float y)) continue;
                    p.y = y;
                    string t = piso[rnd.Next(piso.Length)];
                    float s = t == "Huellas" ? R(5f, 9f) : t == "Arena" ? R(4f, 9f) : t == "Polvo" ? R(5f, 10f) : t == "Parche" ? R(9f, 18f) : t == "Grava" ? R(3f, 6f) : R(1.6f, tam);
                    PonerDecal(gD, t, p, Vector3.up, R(0f, 360f), s, t == "Huellas" ? s * 0.5f : s);
                    i++; nPiso++;
                }
            }
            Zona(-58f, 58f, -378f, -226f, 480, 5f);   // cuartel
            Zona(-58f, 58f, -214f, -112f, 220, 5f);    // muralla y puestos
            Zona(-58f, 58f, -104f, -2f, 200, 5f);      // centro de datos
            Zona(-58f, 58f, 6f, 46f, 130, 5f);         // patio del tanque

            // Ruta del tanque: huellas y manchas encima del eje + arena acumulada a los costados.
            for (int i = 0, intentos = 0; i < 160 && intentos < 900; intentos++)
            {
                int k = rnd.Next(pts.Length - 1);
                var a = pts[k]; var b = pts[k + 1]; var dir = (b - a).normalized;
                if (dir == Vector2.zero) continue;
                var c = Vector2.Lerp(a, b, (float)rnd.NextDouble());
                bool encima = rnd.NextDouble() < 0.45;
                var lado = new Vector2(-dir.y, dir.x) * (encima ? R(-2f, 2f) : R(-14f, 14f));
                var q = c + lado;
                if (q.x > -160f && q.x < 30f && q.y > 300f) continue;    // cauce del rio
                var p = new Vector3(q.x, 0f, q.y);
                if (!Suelo(p, out float y)) continue;
                p.y = y;
                string t = encima ? (rnd.NextDouble() < 0.6 ? "Huellas" : "Aceite") : (rnd.NextDouble() < 0.5 ? "Arena" : "Polvo");
                float yaw = encima ? Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg : R(0f, 360f);
                float s = t == "Huellas" ? R(6f, 10f) : R(5f, 11f);
                PonerDecal(gD, t, p, Vector3.up, yaw, s, t == "Huellas" ? s * 0.5f : s);
                i++; nRuta++;
            }

            // Paredes de los edificios: humedad chorreada, oxido, manchas y grietas, 2-5 por edificio y lado.
            string[] pared = { "Humedad", "Humedad", "Oxido", "Mancha", "Grieta", "Quemado" };
            foreach (var b in edificiosCreados)
            {
                if (b == null) continue;
                var e = b.lossyScale;
                if (e.y < 2f) continue;
                int cuantos = 4 + rnd.Next(5);
                for (int k = 0; k < cuantos; k++)
                {
                    int lado = rnd.Next(4);
                    bool largoX = lado < 2;
                    var normal = largoX ? b.forward * (lado == 0 ? 1f : -1f) : b.right * (lado == 2 ? 1f : -1f);
                    var paralelo = largoX ? b.right : b.forward;
                    float semi = (largoX ? e.z : e.x) * 0.5f + 0.02f;
                    float largo = (largoX ? e.x : e.z) * 0.5f - 0.6f;
                    if (largo < 0.5f) continue;
                    string t = pared[rnd.Next(pared.Length)];
                    float alto = t == "Humedad" || t == "Oxido" ? Mathf.Min(e.y * 0.7f, R(2.5f, 6f)) : R(1.3f, 3f);
                    float ancho = t == "Humedad" || t == "Oxido" ? R(1.8f, 4f) : alto;
                    float yBase = t == "Humedad" || t == "Oxido" ? b.position.y + e.y * 0.5f - alto * 0.5f - 0.15f : R(0.9f, Mathf.Max(1f, e.y - 1f));
                    var pos = b.position + normal * semi + paralelo * R(-largo, largo);
                    pos.y = yBase;
                    PonerDecal(gD, t, pos, normal, 0f, ancho, alto);
                    nPared++;
                }
            }
            ResumenDecals = $"[Operacion] Decals: {nPiso} en el piso, {nRuta} en la ruta, {nPared} en paredes.";
            Debug.Log(ResumenDecals);
        }
    }
}
