using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ambiente de la Operacion (pedido: "el terreno debe ser tierra, algo de pasto, tiendas de campana y todo militar, y en la ruta desierto con
    // cactus y plantaciones"). Todo es DECORADO: sin colliders, fuera del horneado de la NavMesh y con static batching.
    //   - Suelo: tierra (textura Dirt_Diffuse repetida). Manchas de pasto y matas de pasto sueltas en el valle del cuartel y el patio.
    //   - Desierto: una losa de arena sobre el suelo desde el patio del tanque hasta la ciudad, con cactus (malla generada), rocas, arbustos secos
    //     y plantaciones (surcos verdes con casita y tanque de agua) a los costados de la autopista.
    //   - Campamentos militares en la aproximacion al cuartel: carpas (malla generada), red de camuflaje, tambores de combustible, cajones, camion,
    //     cerco de alambre, bolsas de arena, mastil con bandera y farol.
    // Las mallas generadas se guardan como assets (Assets/_Project/Models/Operacion) para que la escena no las lleve embebidas.
    public static partial class OperacionBuilder
    {
        const string CarpetaMallas = "Assets/_Project/Models/Operacion";
        const string RutaTierra = "Assets/_Project/Terrains/Dirt_Diffuse.png";

        static Material Arena => Mat("Arena", Color.white);
        static Material PastoVerde => Mat("PastoVerde", new Color(0.26f, 0.38f, 0.16f));
        static Material PastoSeco => Mat("PastoSeco", new Color(0.55f, 0.48f, 0.26f));
        static Material CactusMat => Mat("Cactus", new Color(0.22f, 0.42f, 0.2f));
        static Material RocaMat => Mat("Roca", new Color(0.50f, 0.42f, 0.34f));
        static Material LonaVerde => Mat("LonaVerde", new Color(0.40f, 0.48f, 0.29f));
        static Material LonaArena => Mat("LonaArena", new Color(0.66f, 0.58f, 0.38f));
        static Material RedCamuflaje => Mat("RedCamuflaje", new Color(0.16f, 0.22f, 0.12f));
        static Material TamborRojo => Mat("TamborRojo", new Color(0.55f, 0.18f, 0.10f));
        static Material CultivoA => Mat("CultivoA", new Color(0.22f, 0.45f, 0.14f));
        static Material CultivoB => Mat("CultivoB", new Color(0.40f, 0.50f, 0.18f));
        static Material SurcoTierra => Mat("SurcoTierra", new Color(0.32f, 0.22f, 0.14f));
        static Material MaderaVieja => Mat("MaderaVieja", new Color(0.36f, 0.26f, 0.17f));

        // ---------------------------------------------------------------
        // Generador de mallas (caras planas: los vertices se duplican por cara)
        // ---------------------------------------------------------------
        sealed class MallaGen
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<int> t = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 centro, bool dosCaras = false)
            {
                // Orienta la cara hacia afuera de "centro" (Unity: el frente es el sentido horario, la normal es (b-a)x(c-a)).
                var n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - centro) < 0f) { var x = b; b = c; c = x; }
                Agregar(a, b, c);
                if (dosCaras) Agregar(a, c, b);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centro, bool dosCaras = false) { Tri(a, b, c, centro, dosCaras); Tri(a, c, d, centro, dosCaras); }
            void Agregar(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            // Tubo (cilindro/cono truncado) de p0 a p1 con tapas.
            public void Tubo(Vector3 p0, Vector3 p1, float r0, float r1, int lados)
            {
                var f = (p1 - p0).normalized;
                var u = Vector3.Cross(f, Mathf.Abs(f.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                var w = Vector3.Cross(f, u);
                var centro = (p0 + p1) * 0.5f;
                for (int i = 0; i < lados; i++)
                {
                    float a0 = i * Mathf.PI * 2f / lados, a1 = (i + 1) * Mathf.PI * 2f / lados;
                    var d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0);
                    var d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                    Quad(p0 + d0 * r0, p0 + d1 * r0, p1 + d1 * r1, p1 + d0 * r1, centro);
                    Tri(p1, p1 + d0 * r1, p1 + d1 * r1, centro);
                    Tri(p0, p0 + d1 * r0, p0 + d0 * r0, centro);
                }
            }
            public Mesh Crear(string nombre)
            {
                var m = new Mesh { name = nombre };
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetTriangles(t, 0);
                var uv = new List<Vector2>(v.Count);
                foreach (var p in v) uv.Add(new Vector2(p.x + p.z, p.y));
                m.SetUVs(0, uv);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        static Mesh GuardarMalla(Mesh nuevo)
        {
            if (!Directory.Exists(CarpetaMallas)) Directory.CreateDirectory(CarpetaMallas);
            string ruta = $"{CarpetaMallas}/{nuevo.name}.asset";
            var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente == null) { AssetDatabase.CreateAsset(nuevo, ruta); return nuevo; }
            existente.Clear(); EditorUtility.CopySerialized(nuevo, existente);
            Object.DestroyImmediate(nuevo);
            return existente;
        }

        static Mesh MallaCarpa()
        {
            // Carpa de campana a dos aguas: 5 m de ancho, 7 m de largo, paredes de 1,4 m y cumbrera de 3,3 m. El frente (+z) queda cerrado:
            // la puerta es un cajon oscuro aparte.
            var g = new MallaGen(); var c = new Vector3(0f, 1.3f, 0f);
            const float W = 2.5f, L = 3.5f, Hp = 1.4f, Hc = 3.3f;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            g.Quad(P(W, Hp, -L), P(0f, Hc, -L), P(0f, Hc, L), P(W, Hp, L), c);
            g.Quad(P(-W, Hp, -L), P(0f, Hc, -L), P(0f, Hc, L), P(-W, Hp, L), c);
            g.Quad(P(W, 0f, -L), P(W, Hp, -L), P(W, Hp, L), P(W, 0f, L), c);
            g.Quad(P(-W, 0f, -L), P(-W, Hp, -L), P(-W, Hp, L), P(-W, 0f, L), c);
            foreach (float z in new[] { -L, L })
            {
                g.Quad(P(-W, 0f, z), P(W, 0f, z), P(W, Hp, z), P(-W, Hp, z), c);
                g.Tri(P(-W, Hp, z), P(W, Hp, z), P(0f, Hc, z), c);
            }
            return GuardarMalla(g.Crear("Op_Carpa"));
        }

        static Mesh MallaCactus()
        {
            var g = new MallaGen();
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            g.Tubo(P(0f, 0f, 0f), P(0f, 4.4f, 0f), 0.4f, 0.2f, 8);
            g.Tubo(P(0.3f, 1.9f, 0f), P(1.15f, 1.9f, 0f), 0.2f, 0.2f, 6);
            g.Tubo(P(1.15f, 1.85f, 0f), P(1.15f, 3.5f, 0f), 0.2f, 0.13f, 6);
            g.Tubo(P(-0.3f, 1.3f, 0f), P(-1.0f, 1.3f, 0f), 0.18f, 0.18f, 6);
            g.Tubo(P(-1.0f, 1.25f, 0f), P(-1.0f, 2.6f, 0f), 0.18f, 0.12f, 6);
            return GuardarMalla(g.Crear("Op_Cactus"));
        }

        static Mesh MallaMata(string nombre, int cuchillas, float altoMin, float altoMax, int semilla)
        {
            var g = new MallaGen(); var rnd = new System.Random(semilla); var c = new Vector3(0f, 0.3f, 0f);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < cuchillas; i++)
            {
                float ang = (float)(i * 2.4 + rnd.NextDouble()) , r = R(0.02f, 0.35f), alto = R(altoMin, altoMax), ancho = R(0.05f, 0.09f), incl = R(0.1f, 0.5f);
                var baseP = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                var lado = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * ancho;
                var punta = baseP + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * incl + Vector3.up * alto;
                g.Tri(baseP - lado, baseP + lado, punta, c, true);
            }
            return GuardarMalla(g.Crear(nombre));
        }

        static Mesh MallaRoca()
        {
            var g = new MallaGen();
            g.Tubo(new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.85f, 0.05f), 1.0f, 0.55f, 6);
            return GuardarMalla(g.Crear("Op_Roca"));
        }

        // ---------------------------------------------------------------
        // Colocacion
        // ---------------------------------------------------------------
        static GameObject Poner(Transform g, Mesh m, Material mat, Vector3 pos, float yaw, Vector3 escala, string nombre)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(g, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = escala;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // Cubo/cilindro en el espacio LOCAL de un grupo (los campamentos se arman de frente y se giran enteros).
        static GameObject Cl(Transform g, string n, float lx, float lz, float w, float h, float d, Material m, float yaw = 0f, float y0 = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n; go.transform.SetParent(g, false);
            go.transform.localPosition = new Vector3(lx, y0 + h * 0.5f, lz);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(w, h, d);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }
        static GameObject Cil(Transform g, string n, float lx, float lz, float diam, float h, Material m, float y0 = 0f, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = n; go.transform.SetParent(g, false);
            go.transform.localPosition = new Vector3(lx, y0 + h * 0.5f, lz);
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = new Vector3(diam, h * 0.5f, diam);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static void SinNavMesh(Transform t)
        {
            var mod = t.gameObject.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true; mod.applyToChildren = true;
        }

        public const string NombreAmbiente = "Terreno_Ambiente";
        public static string ResumenAmbiente { get; private set; } = "";

        // Textura "tileable" (el ruido envuelve en los bordes) de tierra o arena, generada por codigo y guardada como PNG.
        static Texture2D TexturaDeSuelo(string nombre, Color a, Color b, int semilla)
        {
            const int N = 512;
            string ruta = $"Assets/_Project/Textures/Operacion/{nombre}.png";
            var dir = Path.GetDirectoryName(ruta);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var rnd = new System.Random(semilla);
            int[] celdas = { 2, 4, 8, 16, 32, 64 }; float[] peso = { 0.34f, 0.26f, 0.18f, 0.12f, 0.06f, 0.04f };
            var rejillas = new float[celdas.Length][,];
            for (int o = 0; o < celdas.Length; o++)
            {
                rejillas[o] = new float[celdas[o], celdas[o]];
                for (int i = 0; i < celdas[o]; i++) for (int j = 0; j < celdas[o]; j++) rejillas[o][i, j] = (float)rnd.NextDouble();
            }
            float Ruido(int o, float u, float v)
            {
                int n = celdas[o]; float x = u * n, y = v * n; int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0; fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
                int xa = ((x0 % n) + n) % n, xb = (xa + 1) % n, ya = ((y0 % n) + n) % n, yb = (ya + 1) % n;
                return Mathf.Lerp(Mathf.Lerp(rejillas[o][xa, ya], rejillas[o][xb, ya], fx), Mathf.Lerp(rejillas[o][xa, yb], rejillas[o][xb, yb], fx), fy);
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N, t = 0f;
                for (int o = 0; o < celdas.Length; o++) t += Ruido(o, u, v) * peso[o];
                float grano = (float)rnd.NextDouble();
                float k = Mathf.Clamp01((t - 0.5f) * 1.55f + 0.5f + (grano - 0.5f) * 0.10f);
                var c = Color.Lerp(a, b, k);
                if (grano > 0.985f) c *= 1.25f; else if (grano < 0.012f) c *= 0.7f;    // piedritas claras y oscuras sueltas
                tex.SetPixel(x, y, new Color(c.r, c.g, c.b, 1f));
            }
            tex.Apply();
            File.WriteAllBytes(ruta, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        static void TexturarSuelo()
        {
            // Mismo trimsheet que el resto: tierra y arena son celdas de paleta; la variedad la ponen los decals (OperacionBuilder.Decals.cs).
            var suelo = Suelo;
            PonerCeldaTrimsheet(suelo, "Suelo", Color.white, "Tierra");
            suelo.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(suelo);
            var arena = Arena;
            PonerCeldaTrimsheet(arena, "Arena", Color.white, "Arena_Desierto");
            arena.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(arena);
        }

        // Se llama desde Construir, DESPUES de CiudadDeCubos y antes de hornear la NavMesh.
        public static void Ambiente(Transform op, Transform[] ruta)
        {
            var viejo = GameObject.Find(NombreAmbiente);
            if (viejo != null) Object.DestroyImmediate(viejo);
            TexturarSuelo();
            var ocup = LeerOcupacionCc(op);
            var raizA = new GameObject(NombreAmbiente).transform;
            SinNavMesh(raizA);
            var rnd = new System.Random(7001);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var mallaCarpa = MallaCarpa(); var mallaCactus = MallaCactus(); var mallaRoca = MallaRoca();
            var mallaMata = MallaMata("Op_Mata", 11, 0.35f, 0.8f, 11);
            var mallaMataSeca = MallaMata("Op_MataSeca", 9, 0.25f, 0.55f, 23);

            // Polilinea de la autopista para medir distancias.
            var poli = new List<Vector2>();
            if (ruta != null) foreach (var r in ruta) if (r != null) poli.Add(new Vector2(r.position.x, r.position.z));
            float DistARuta(float x, float z)
            {
                float mejor = 1e9f; var p = new Vector2(x, z);
                for (int i = 0; i < poli.Count - 1; i++)
                {
                    var a = poli[i]; var ab = poli[i + 1] - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                    mejor = Mathf.Min(mejor, (p - (a + ab * k)).magnitude);
                }
                return mejor;
            }

            int cactus = 0, rocas = 0, matas = 0, secas = 0, campos = 0, carpas = 0, parches = 0;

            // ---- 1) Desierto: losa de arena desde el patio del tanque hasta la ciudad ----
            var gDes = Grupo(raizA, "Desierto");
            var losa = Cubo(gDes, "Losa_Arena", 0f, 222f, 776f, 0.03f, 332f, Arena, 0f, 0f, false);
            GameObjectUtility.SetStaticEditorFlags(losa, StaticEditorFlags.BatchingStatic);

            // Cactus, rocas y arbustos secos repartidos donde no hay nada (la ocupacion lee la autopista, la ciudad y el patio).
            var gCact = Grupo(gDes, "Cactus"); var gRoca = Grupo(gDes, "Rocas"); var gSeca = Grupo(gDes, "ArbustosSecos");
            for (int intento = 0; intento < 3000 && cactus < 170; intento++)
            {
                float x = R(-380f, 380f), z = R(70f, 382f);
                if (!ocup.Libre(x - 1f, z - 1f, x + 1f, z + 1f)) continue;
                if (DistARuta(x, z) < 15f) continue;
                float e = R(0.8f, 1.5f);
                Poner(gCact, mallaCactus, CactusMat, new Vector3(x, 0f, z), R(0f, 360f), new Vector3(e, e * R(0.9f, 1.25f), e), "Cactus"); cactus++;
            }
            for (int intento = 0; intento < 2000 && rocas < 90; intento++)
            {
                float x = R(-380f, 380f), z = R(70f, 382f);
                if (!ocup.Libre(x - 1f, z - 1f, x + 1f, z + 1f) || DistARuta(x, z) < 12f) continue;
                float e = R(0.8f, 2.6f);
                Poner(gRoca, mallaRoca, RocaMat, new Vector3(x, -0.1f, z), R(0f, 360f), new Vector3(e, e * R(0.5f, 0.9f), e * R(0.7f, 1.2f)), "Roca"); rocas++;
            }
            for (int intento = 0; intento < 2500 && secas < 220; intento++)
            {
                float x = R(-380f, 380f), z = R(62f, 382f);
                if (!ocup.Libre(x - 1f, z - 1f, x + 1f, z + 1f)) continue;
                float e = R(1.2f, 2.4f);
                Poner(gSeca, mallaMataSeca, PastoSeco, new Vector3(x, 0f, z), R(0f, 360f), new Vector3(e, e, e), "ArbustoSeco"); secas++;
            }

            // ---- 2) Plantaciones junto a la autopista ----
            var gCampos = Grupo(gDes, "Plantaciones");
            var centrosCampo = new List<Vector2>();
            for (int intento = 0; intento < 600 && campos < 6; intento++)
            {
                float x = R(-330f, 230f), z = R(90f, 360f);
                const float W = 44f, D = 30f;   // ancho (x) y largo (z) del campo
                if (!ocup.Libre(x - W * 0.5f, z - D * 0.5f, x + W * 0.5f, z + D * 0.5f)) continue;
                float dr = DistARuta(x, z);
                if (dr < 30f || dr > 110f) continue;
                bool lejos = true; foreach (var c in centrosCampo) if ((c - new Vector2(x, z)).magnitude < 75f) lejos = false;
                if (!lejos) continue;
                centrosCampo.Add(new Vector2(x, z)); campos++;
                CampoDeCultivo(gCampos, x, z, W, D, campos % 2 == 0, rnd);
            }

            // ---- 3) Pasto: manchas y matas en el valle del cuartel, el patio del tanque y la entrada de la ciudad ----
            var gPasto = Grupo(raizA, "Pasto");
            for (int i = 0; i < 70; i++)
            {
                float x = R(-58f, 58f), z = R(-378f, 50f);
                if (z > -345f && z < -335f) continue;
                if (Mathf.Abs(x) < 7f && z < -340f) continue;
                if (!ocup.Libre(x - 1f, z - 1f, x + 1f, z + 1f) && z > -340f) continue;
                float w = R(5f, 14f), d = R(4f, 12f);
                var p = Pintura(gPasto, "ParchePasto", x, z, w, d, PastoVerde, R(0f, 180f), 0.012f, 0.012f);
                GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic); parches++;
            }
            for (int intento = 0; intento < 2200 && matas < 380; intento++)
            {
                float x = R(-58f, 58f), z = R(-378f, 52f);
                if (Mathf.Abs(x) < 6f && z < -338f) continue;
                if (z > -342f && z < -218f) continue;               // dentro del cuartel hay hormigon: nada de pasto
                if (!ocup.Libre(x - 0.5f, z - 0.5f, x + 0.5f, z + 0.5f) && z > -216f) continue;
                float e = R(0.8f, 1.7f);
                Poner(gPasto, mallaMata, PastoVerde, new Vector3(x, 0f, z), R(0f, 360f), new Vector3(e, e, e), "Mata"); matas++;
            }
            // Entrada de la ciudad y bordes de la autopista: matas verdes sueltas.
            for (int intento = 0; intento < 900 && matas < 560; intento++)
            {
                float x = R(-380f, 380f), z = R(70f, 382f);
                if (!ocup.Libre(x - 0.5f, z - 0.5f, x + 0.5f, z + 0.5f)) continue;
                if (DistARuta(x, z) > 40f) continue;
                float e = R(0.9f, 1.8f);
                Poner(gPasto, mallaMata, PastoVerde, new Vector3(x, 0f, z), R(0f, 360f), new Vector3(e, e, e), "Mata"); matas++;
            }

            // ---- 4) Campamentos militares ----
            var gCamp = Grupo(raizA, "Campamentos");
            carpas += CampamentoMilitar(gCamp, "Campamento_Aproximacion_O", -38f, -362f, 90f, mallaCarpa, true);
            carpas += CampamentoMilitar(gCamp, "Campamento_Aproximacion_E", 38f, -362f, -90f, mallaCarpa, false);

            ResumenAmbiente = $"[Operacion] Ambiente: {cactus} cactus, {rocas} rocas, {secas} arbustos secos, {campos} plantaciones, {matas} matas y {parches} manchas de pasto, {carpas} carpas.";
            Debug.Log(ResumenAmbiente);
            Debug.Log("[Operacion] Trimsheet: " + string.Join(", ", ResumenTrimsheet));
        }

        // Campo de cultivo: surcos de tierra con filas verdes, una casita y un tanque de agua en un costado.
        static void CampoDeCultivo(Transform padre, float cx, float cz, float w, float d, bool alternado, System.Random rnd)
        {
            var g = Grupo(padre, "Plantacion");
            var baseTierra = Pintura(g, "Tierra_Campo", cx, cz, w, d, SurcoTierra, 0f, 0.03f, 0.02f);
            GameObjectUtility.SetStaticEditorFlags(baseTierra, StaticEditorFlags.BatchingStatic);
            int surcos = Mathf.FloorToInt((w - 4f) / 2.2f);
            for (int i = 0; i < surcos; i++)
            {
                float x = cx - w * 0.5f + 3f + i * 2.2f;
                var fila = Pintura(g, "Surco", x, cz, 1.1f, d - 3f, (i % 2 == 0) == alternado ? CultivoA : CultivoB, 0f, 0.05f, 0.55f + (float)rnd.NextDouble() * 0.2f);
                GameObjectUtility.SetStaticEditorFlags(fila, StaticEditorFlags.BatchingStatic);
            }
            // Casita de campo y tanque de agua en la esquina.
            float hx = cx + w * 0.5f + 5f, hz = cz - d * 0.5f + 4f;
            var casa = Decor(g, "Casita", hx, hz, 6f, 3f, 5f, Casa); GameObjectUtility.SetStaticEditorFlags(casa, StaticEditorFlags.BatchingStatic);
            var techo = Decor(g, "Casita_Techo", hx, hz, 6.6f, 0.4f, 5.6f, Techo, 0f, 3f); GameObjectUtility.SetStaticEditorFlags(techo, StaticEditorFlags.BatchingStatic);
            var puerta = Decor(g, "Casita_Puerta", hx, hz - 2.52f, 1.2f, 2f, 0.1f, MaderaVieja); GameObjectUtility.SetStaticEditorFlags(puerta, StaticEditorFlags.BatchingStatic);
            var tanque = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tanque.name = "TanqueDeAgua"; tanque.transform.SetParent(g, false);
            tanque.transform.position = new Vector3(hx + 6f, 3.2f, hz + 1f); tanque.transform.localScale = new Vector3(3f, 1.4f, 3f);
            tanque.GetComponent<MeshRenderer>().sharedMaterial = MetalClaro; Object.DestroyImmediate(tanque.GetComponent<CapsuleCollider>());
            GameObjectUtility.SetStaticEditorFlags(tanque, StaticEditorFlags.BatchingStatic);
            foreach (var s in new[] { new Vector2(-1.1f, -1.1f), new Vector2(1.1f, -1.1f), new Vector2(-1.1f, 1.1f), new Vector2(1.1f, 1.1f) })
            {
                var pata = Decor(g, "TanqueDeAgua_Pata", hx + 6f + s.x, hz + 1f + s.y, 0.25f, 2.4f, 0.25f, MaderaVieja);
                GameObjectUtility.SetStaticEditorFlags(pata, StaticEditorFlags.BatchingStatic);
            }
        }

        // Campamento de ~32 x 28 m. Se arma de frente (+z local = hacia donde miran las puertas) y se gira entero. Devuelve la cantidad de carpas.
        static int CampamentoMilitar(Transform padre, string nombre, float cx, float cz, float yaw, Mesh carpa, bool conCamion)
        {
            var g = Grupo(padre, nombre);
            g.position = new Vector3(cx, 0f, cz); g.rotation = Quaternion.Euler(0f, yaw, 0f);
            int n = 0;
            // Fila de carpas (puertas hacia +z).
            float[] xs = { -11.5f, -3.8f, 3.8f, 11.5f };
            for (int i = 0; i < xs.Length; i++)
            {
                var c = Poner(g, carpa, i % 2 == 0 ? LonaVerde : LonaArena, g.TransformPoint(new Vector3(xs[i], 0f, 7f)), yaw, Vector3.one, "Carpa_" + (i + 1));
                n++;
                Cl(g, "Carpa_Puerta_" + (i + 1), xs[i], 10.52f, 1.4f, 2.1f, 0.08f, Negro);
                // Estacas y cuerdas (postes finos en las esquinas).
                foreach (float dx in new[] { -2.9f, 2.9f }) Cl(g, "Estaca", xs[i] + dx, 10.9f, 0.1f, 0.7f, 0.1f, MaderaVieja);
            }
            // Red de camuflaje sobre postes, con cajones y tambores debajo.
            float rz = -7f;
            foreach (var sx in new[] { -12f, 12f }) foreach (var sz in new[] { rz - 4f, rz + 4f }) Cl(g, "Red_Poste", sx, sz, 0.18f, 3.6f, 0.18f, MaderaVieja);
            Cl(g, "Red", 0f, rz, 26f, 0.12f, 10f, RedCamuflaje, 0f, 3.6f);
            Cl(g, "Red_Caida", 0f, rz + 5.6f, 26f, 1.4f, 0.1f, RedCamuflaje, 0f, 2.2f);
            for (int i = 0; i < 6; i++) Cil(g, "Tambor", -10f + (i % 3) * 0.95f, rz - 2f + (i / 3) * 0.95f, 0.8f, 1.05f, i % 2 == 0 ? TamborRojo : OlivaOsc);
            for (int i = 0; i < 4; i++) Cl(g, "Cajon", -2f + i * 1.5f, rz - 3f, 1.3f, 1.0f + (i % 2) * 0.5f, 1.2f, Cobertura, i * 8f);
            Cl(g, "Cajon_Apilado", -0.6f, rz - 3f, 1.2f, 0.9f, 1.1f, Cobertura, 14f, 1.5f);
            // Camion militar (cabina, caja, ruedas).
            if (conCamion)
            {
                float tx = 8f, tz = rz;
                Cl(g, "Camion_Cabina", tx, tz + 2.8f, 2.4f, 2.4f, 2.1f, Oliva, 0f, 0.7f);
                Cl(g, "Camion_Parabrisas", tx, tz + 3.88f, 2.0f, 0.9f, 0.06f, Cristal, 0f, 1.6f);
                Cl(g, "Camion_Caja", tx, tz - 0.6f, 2.5f, 2.4f, 5.4f, OlivaClaro, 0f, 0.7f);
                Cl(g, "Camion_Lona", tx, tz - 0.6f, 2.35f, 0.3f, 5.2f, LonaVerde, 0f, 3.1f);
                foreach (var wz in new[] { tz - 2.2f, tz + 0.8f, tz + 3.0f })
                    foreach (var wx in new[] { tx - 1.3f, tx + 1.3f })
                        Cil(g, "Camion_Rueda", wx, wz, 1.1f, 0.4f, Negro, 0.55f, new Vector3(0f, 0f, 90f));
            }
            // Pozo de mortero con anillo de bolsas de arena.
            float mx = -11f, mz = -1f;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                if (i == 3) continue;   // hueco de acceso
                Cl(g, "Saco_Anillo", mx + Mathf.Cos(a) * 2.2f, mz + Mathf.Sin(a) * 2.2f, 1.5f, 0.7f, 0.7f, Cobertura, -a * Mathf.Rad2Deg + 90f);
                if (i % 2 == 0) Cl(g, "Saco_Anillo_Alto", mx + Mathf.Cos(a) * 2.2f, mz + Mathf.Sin(a) * 2.2f, 1.4f, 0.65f, 0.65f, Cobertura, -a * Mathf.Rad2Deg + 90f + 12f, 0.7f);
            }
            Cil(g, "Mortero_Tubo", mx, mz, 0.22f, 1.5f, MetalOsc, 0.2f, new Vector3(-24f, 0f, 0f));
            Cl(g, "Mortero_Base", mx, mz, 0.7f, 0.12f, 0.7f, MetalOsc);
            // Cerco de alambre (postes cada 4 m con dos travesanos finos), con un hueco al frente.
            float ancho = 32f, fondo = 28f, fz0 = -fondo * 0.5f, fz1 = fondo * 0.5f, fx0 = -ancho * 0.5f, fx1 = ancho * 0.5f;
            for (float x = fx0; x <= fx1 + 0.1f; x += 4f)
            {
                Cl(g, "Cerco_Poste", x, fz0, 0.14f, 1.9f, 0.14f, MaderaVieja);
                if (Mathf.Abs(x) > 6f) Cl(g, "Cerco_Poste", x, fz1, 0.14f, 1.9f, 0.14f, MaderaVieja);
            }
            for (float z = fz0 + 4f; z < fz1; z += 4f) { Cl(g, "Cerco_Poste", fx0, z, 0.14f, 1.9f, 0.14f, MaderaVieja); Cl(g, "Cerco_Poste", fx1, z, 0.14f, 1.9f, 0.14f, MaderaVieja); }
            foreach (float alt in new[] { 0.7f, 1.4f })
            {
                Cl(g, "Cerco_Alambre", 0f, fz0, ancho, 0.05f, 0.05f, MetalOsc, 0f, alt);
                Cl(g, "Cerco_Alambre", fx0, 0f, 0.05f, 0.05f, fondo, MetalOsc, 0f, alt);
                Cl(g, "Cerco_Alambre", fx1, 0f, 0.05f, 0.05f, fondo, MetalOsc, 0f, alt);
                Cl(g, "Cerco_Alambre", fx0 * 0.5f - 3f, fz1, ancho * 0.5f - 6f, 0.05f, 0.05f, MetalOsc, 0f, alt);
                Cl(g, "Cerco_Alambre", fx1 * 0.5f + 3f, fz1, ancho * 0.5f - 6f, 0.05f, 0.05f, MetalOsc, 0f, alt);
            }
            // Mastil con bandera y farol.
            Cl(g, "Mastil", 0f, 1f, 0.2f, 9f, 0.2f, Blanco);
            Cl(g, "Bandera", 1.2f, 1f, 2.2f, 1.4f, 0.05f, Rojo, 0f, 7.4f);
            Cl(g, "Farol_Poste", 14f, 11f, 0.2f, 5f, 0.2f, MetalOsc);
            Cl(g, "Farol_Cabeza", 14f, 11f, 0.9f, 0.3f, 0.6f, Lampara, 0f, 5f);
            var pl = g.TransformPoint(new Vector3(14f, 4.7f, 11f));
            LuzPuntual(g, "Farol_Luz", pl.x, pl.y, pl.z, new Color(1f, 0.9f, 0.7f), 2.6f, 22f);
            return n;
        }
    }
}
