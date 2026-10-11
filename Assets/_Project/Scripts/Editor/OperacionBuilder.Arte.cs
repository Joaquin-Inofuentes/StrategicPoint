using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Pedido ("no quiero nada de blocking, todo arte"): los cubos del nivel ya no son de color plano. Cada material del nivel se asocia a una textura
    // procedural tileable (hormigon, ladrillo, chapa, madera, lona, asfalto...) con su mapa de normales, y cada cubo se construye con una malla cuyas UV
    // estan en METROS (la textura repite cada N m sin estirarse, sea el cubo un muro de 60 m o un cajon de 40 cm).
    public static partial class OperacionBuilder
    {
        struct EspecDeTextura
        {
            public string textura; public float metros; public float brillo; public float relieve; public string techo; public bool emisivo;
            public EspecDeTextura(string t, float m, float b, float r, string techo = null, bool emisivo = false) { textura = t; metros = m; brillo = b; relieve = r; this.techo = techo; this.emisivo = emisivo; }
        }

        static readonly Dictionary<string, EspecDeTextura> especDeMaterial = new Dictionary<string, EspecDeTextura>
        {
            // hormigon
            { "Cemento", new EspecDeTextura("Hormigon", 6f, 0.08f, 0.9f) }, { "CementoOsc", new EspecDeTextura("Hormigon", 6f, 0.08f, 0.9f) },
            { "Hormigon", new EspecDeTextura("Hormigon", 6f, 0.08f, 0.9f) }, { "PisoHormigon", new EspecDeTextura("Hormigon", 8f, 0.05f, 0.7f) },
            { "PisoAproximacion", new EspecDeTextura("Hormigon", 8f, 0.05f, 0.7f) }, { "PisoOcre", new EspecDeTextura("Hormigon", 8f, 0.05f, 0.7f) },
            { "PisoOliva", new EspecDeTextura("Hormigon", 8f, 0.05f, 0.7f) }, { "PisoAzul", new EspecDeTextura("Hormigon", 8f, 0.05f, 0.7f) },
            { "Datos", new EspecDeTextura("Hormigon", 6f, 0.1f, 0.8f) },
            // casas / edificios: estuco y ladrillo
            { "Casa", new EspecDeTextura("Estuco", 5f, 0.05f, 0.8f) }, { "CasaB", new EspecDeTextura("Estuco", 5f, 0.05f, 0.8f) },
            { "CasaC", new EspecDeTextura("Ladrillo", 2f, 0.05f, 1f) }, { "Ocre", new EspecDeTextura("Estuco", 5f, 0.05f, 0.8f) },
            { "Barraca", new EspecDeTextura("Estuco", 5f, 0.05f, 0.8f) }, { "OlivaClaro", new EspecDeTextura("Estuco", 5f, 0.05f, 0.8f) },
            { "Oliva", new EspecDeTextura("Placa", 4f, 0.2f, 0.9f) }, { "OlivaOsc", new EspecDeTextura("Placa", 4f, 0.2f, 0.9f) },
            // techos y chapa
            { "Techo", new EspecDeTextura("Chapa", 2f, 0.3f, 1f) }, { "TechoOcre", new EspecDeTextura("Chapa", 2f, 0.3f, 1f) },
            { "TechoAzul", new EspecDeTextura("Chapa", 2f, 0.3f, 1f) }, { "AzulAcero", new EspecDeTextura("Placa", 4f, 0.3f, 0.9f) },
            // metal
            { "MetalOsc", new EspecDeTextura("Placa", 4f, 0.35f, 0.9f) }, { "MetalClaro", new EspecDeTextura("Placa", 4f, 0.4f, 0.9f) },
            { "Blindado", new EspecDeTextura("Placa", 4f, 0.3f, 0.9f) }, { "Rack", new EspecDeTextura("Placa", 1f, 0.35f, 0.7f) },
            { "TamborRojo", new EspecDeTextura("Chapa", 1f, 0.3f, 0.8f) },
            // madera, lona, sacos
            { "MaderaVieja", new EspecDeTextura("Madera", 2f, 0.05f, 1f) }, { "Puerta", new EspecDeTextura("Madera", 2f, 0.05f, 1f) },
            { "Cobertura", new EspecDeTextura("Lona", 1.2f, 0.02f, 1.2f) }, { "LonaVerde", new EspecDeTextura("Lona", 1.5f, 0.02f, 1.1f) },
            { "LonaArena", new EspecDeTextura("Lona", 1.5f, 0.02f, 1.1f) }, { "RedCamuflaje", new EspecDeTextura("Lona", 1.2f, 0.02f, 1.4f) },
            // calzada y tierra
            { "Asfalto", new EspecDeTextura("Asfalto", 4f, 0.04f, 0.9f) }, { "PisoTrinchera", new EspecDeTextura("Estuco", 5f, 0.02f, 1.2f) },
            { "SurcoTierra", new EspecDeTextura("Estuco", 2f, 0.02f, 1.5f) },
            { "Montana", new EspecDeTextura("Estuco", 45f, 0.02f, 1.6f) },
            { "Rio", new EspecDeTextura("Agua", 9f, 0.85f, 1.3f) },
            // techos planos de los edificios altos y fachadas de oficinas (ventanas con emision: algunas encendidas de noche)
            { "TechoEdificio", new EspecDeTextura("Hormigon", 6f, 0.05f, 0.9f) },
            { "Relleno_A", new EspecDeTextura("Oficina", 12f, 0.35f, 1f, "TechoEdificio", true) }, { "Relleno_B", new EspecDeTextura("Oficina", 12f, 0.35f, 1f, "TechoEdificio", true) },
            { "Relleno_C", new EspecDeTextura("Oficina", 12f, 0.35f, 1f, "TechoEdificio", true) },
        };
        static readonly Dictionary<Material, Material> techoDeMaterial = new Dictionary<Material, Material>();

        static readonly Dictionary<Material, float> metrosPorTile = new Dictionary<Material, float>();
        static readonly Dictionary<string, Texture2D[]> texturasGeneradas = new Dictionary<string, Texture2D[]>();
        static readonly Dictionary<long, Mesh> mallasDeCaja = new Dictionary<long, Mesh>();

        const string CarpetaTex = "Assets/_Project/Textures/Operacion";

        // Se llama desde Mat(): si el material tiene textura asignada, la pone (con su normal) y recuerda cada cuantos metros repite.
        // Pedido (#149): todos los materiales usan el MISMO trimsheet del proyecto (Assets/ARTS/SP_Arte/Trimsheet_1024.png, 8x8 celdas de color plano).
        // Cada material apunta (escala 0 + offset) al centro de la celda de paleta mas cercana a su color. Excepcion: fachadas con ventanas emisivas.
        [Serializable] sealed class CeldaTrim { public string name; public int[] rgb; public float[] uv_center; }
        [Serializable] sealed class MapaTrim { public CeldaTrim[] cells; }
        static MapaTrim mapaTrim; static Texture2D texTrim;
        const string GuidTrimsheet = "9738b8985cc876248ba1213997c2bd8c";
        public static readonly List<string> ResumenTrimsheet = new List<string>();

        static CeldaTrim CeldaDeTrimsheet(string nombre, Color c, out Texture2D tex)
        {
            if (mapaTrim == null)
            {
                string pt = AssetDatabase.GUIDToAssetPath(GuidTrimsheet);
                texTrim = AssetDatabase.LoadAssetAtPath<Texture2D>(pt);
                string pj = "Assets/ARTS/SP_Arte/Trimsheet_1024_map.json";
                mapaTrim = JsonUtility.FromJson<MapaTrim>(File.ReadAllText(pj));
            }
            tex = texTrim;
            CeldaTrim mejor = null; float md = 1e9f;
            foreach (var ce in mapaTrim.cells)
            {
                if (nombre != null && ce.name == nombre) return ce;
                if (nombre != null) continue;
                float dr = ce.rgb[0] / 255f - c.r, dg = ce.rgb[1] / 255f - c.g, db = ce.rgb[2] / 255f - c.b;
                float d = dr * dr * 0.9f + dg * dg * 1.2f + db * db * 0.8f;
                if (d < md) { md = d; mejor = ce; }
            }
            return mejor;
        }

        static void PonerCeldaTrimsheet(Material mat, string clave, Color c, string celdaFija = null)
        {
            var ce = CeldaDeTrimsheet(celdaFija, c, out var tex);
            if (ce == null || tex == null) return;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", Vector2.zero);
            mat.SetTextureOffset("_BaseMap", new Vector2(ce.uv_center[0], ce.uv_center[1]));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");
            metrosPorTile.Remove(mat);
            ResumenTrimsheet.Add($"{clave}->{ce.name}");
        }

        static void AplicarTextura(Material mat, string clave, Color color, float emision)
        {
            especDeMaterial.TryGetValue(clave, out var spec);
            bool emisivoTex = spec.emisivo;
            if (!emisivoTex && emision <= 0f)
            {
                PonerCeldaTrimsheet(mat, clave, color);
                if (spec.textura != null) mat.SetFloat("_Smoothness", spec.brillo);
                return;
            }
            AplicarTextura(mat, clave);
        }

        static void AplicarTextura(Material mat, string clave)
        {
            if (!especDeMaterial.TryGetValue(clave, out var e)) return;
            var par = TexturaProcedural(e.textura);
            if (par == null) return;
            mat.SetTexture("_BaseMap", par[0]);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetTexture("_BumpMap", par[1]);
            mat.SetFloat("_BumpScale", e.relieve);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", e.brillo);
            metrosPorTile[mat] = e.metros;
            if (e.emisivo)
            {
                mat.SetTexture("_EmissionMap", par[2]);
                mat.SetColor("_EmissionColor", Color.white * 1.4f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (e.techo != null) techoDeMaterial[mat] = Mat(e.techo, new Color(0.40f, 0.40f, 0.42f));
        }

        static Material TechoDe(Material m) => m != null && techoDeMaterial.TryGetValue(m, out var t) ? t : null;

        static float MetrosPorTile(Material m) => m != null && metrosPorTile.TryGetValue(m, out var t) ? t : 0f;

        // ---------------------------------------------------------------
        // Malla de caja con UV en metros
        // ---------------------------------------------------------------
        static Mesh MallaDeCaja(float w, float h, float d, float tile, int variante, bool techoAparte = false)
        {
            long clave = (techoAparte ? 1L << 60 : 0L) | ((long)Mathf.RoundToInt(w * 20f) & 0xFFFF) | (((long)Mathf.RoundToInt(h * 20f) & 0xFFFF) << 16) | (((long)Mathf.RoundToInt(d * 20f) & 0xFFFF) << 32)
                         | (((long)Mathf.RoundToInt(tile * 10f) & 0xFF) << 48) | (((long)variante & 0xF) << 56);
            if (mallasDeCaja.TryGetValue(clave, out var m) && m != null) return m;
            var v = new List<Vector3>(24); var n = new List<Vector3>(24); var uv = new List<Vector2>(24); var tg = new List<Vector4>(24); var tri = new List<int>(36);
            float ox = (variante * 0.37f) % 1f, oy = (variante * 0.61f) % 1f;
            // cara: origen, eje u, eje v, normal; tamanos en metros de u y v
            var triTecho = new List<int>(6);
            void Cara(Vector3 o, Vector3 eu, Vector3 ev, Vector3 nor, float su, float sv)
            {
                int b = v.Count;
                var destino = techoAparte && nor == Vector3.up ? triTecho : tri;
                v.Add(o); v.Add(o + eu); v.Add(o + eu + ev); v.Add(o + ev);
                for (int i = 0; i < 4; i++) { n.Add(nor); tg.Add(new Vector4(eu.normalized.x, eu.normalized.y, eu.normalized.z, -1f)); }
                uv.Add(new Vector2(ox, oy)); uv.Add(new Vector2(ox + su / tile, oy)); uv.Add(new Vector2(ox + su / tile, oy + sv / tile)); uv.Add(new Vector2(ox, oy + sv / tile));
                destino.Add(b); destino.Add(b + 2); destino.Add(b + 1); destino.Add(b); destino.Add(b + 3); destino.Add(b + 2);
            }
            // El cubo es unitario (la escala la pone el transform), asi que las UV se calculan con las medidas reales.
            Cara(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.right, Vector3.up, Vector3.back, w, h);      // -Z
            Cara(new Vector3(0.5f, -0.5f, 0.5f), Vector3.left, Vector3.up, Vector3.forward, w, h);       // +Z
            Cara(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.back, Vector3.up, Vector3.left, d, h);         // -X
            Cara(new Vector3(0.5f, -0.5f, -0.5f), Vector3.forward, Vector3.up, Vector3.right, d, h);     // +X
            Cara(new Vector3(-0.5f, 0.5f, -0.5f), Vector3.right, Vector3.forward, Vector3.up, w, d);     // +Y
            Cara(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.right, Vector3.back, Vector3.down, w, d);      // -Y
            m = new Mesh { name = $"Caja_{w:0.#}x{h:0.#}x{d:0.#}_{variante}" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTangents(tg);
            if (techoAparte) { m.subMeshCount = 2; m.SetTriangles(tri, 0); m.SetTriangles(triTecho, 1); } else m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            mallasDeCaja[clave] = m;
            return m;
        }

        // ---------------------------------------------------------------
        // Texturas procedurales tileables (brillo en gris; el color lo pone el material) + normal desde el relieve
        // ---------------------------------------------------------------
        sealed class RuidoT
        {
            readonly float[][,] g; readonly int[] celdas;
            public RuidoT(int semilla, params int[] celdas)
            {
                this.celdas = celdas; var r = new System.Random(semilla); g = new float[celdas.Length][,];
                for (int o = 0; o < celdas.Length; o++)
                {
                    g[o] = new float[celdas[o], celdas[o]];
                    for (int i = 0; i < celdas[o]; i++) for (int j = 0; j < celdas[o]; j++) g[o][i, j] = (float)r.NextDouble();
                }
            }
            public float Octava(int o, float u, float v, int estirarU = 1, int estirarV = 1)
            {
                int nx = celdas[o] * estirarU, ny = celdas[o] * estirarV;
                float x = u * nx, y = v * ny; int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0; fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
                int cx = celdas[o], cy = celdas[o];
                int xa = ((x0 % cx) + cx) % cx, xb = (xa + 1) % cx, ya = ((y0 % cy) + cy) % cy, yb = (ya + 1) % cy;
                return Mathf.Lerp(Mathf.Lerp(g[o][xa, ya], g[o][xb, ya], fx), Mathf.Lerp(g[o][xa, yb], g[o][xb, yb], fx), fy);
            }
            public float Fbm(float u, float v)
            {
                float t = 0f, p = 0.5f, s = 0f;
                for (int o = 0; o < celdas.Length; o++) { t += Octava(o, u, v) * p; s += p; p *= 0.5f; }
                return t / s;
            }
        }

        static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1274126177; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFF) / 65535f; } }

        // f(u, v) -> (brillo 0..1, altura 0..1); u y v en [0,1)
        static Texture2D[] TexturaProcedural(string nombre)
        {
            if (texturasGeneradas.TryGetValue(nombre, out var hecha) && hecha[0] != null) return hecha;
            const int N = 256;
            var ruido = new RuidoT(nombre.GetHashCode() & 0xFFFF, 4, 8, 16, 32);
            var brillo = new float[N, N]; var altura = new float[N, N]; var encendida = new bool[N, N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N, b = 0.8f, a = 0.5f;
                float fino = Hash(x, y, 7);
                switch (nombre)
                {
                    case "Hormigon":
                    {
                        float f = ruido.Fbm(u, v);
                        float mancha = ruido.Octava(0, u, v, 2, 1);                      // manchas estiradas en vertical (chorreado de lluvia)
                        b = 0.80f + (f - 0.5f) * 0.30f - mancha * 0.10f + (ruido.Octava(0, u + 0.37f, v + 0.11f) - 0.5f) * 0.14f + (fino - 0.5f) * 0.07f;
                        a = f * 0.6f + fino * 0.2f;
                        break;
                    }
                    case "Estuco":
                    {
                        float f = ruido.Fbm(u, v), g = ruido.Octava(1, u, v);
                        b = 0.84f + (f - 0.5f) * 0.20f + (g - 0.5f) * 0.08f + (fino - 0.5f) * 0.06f;
                        a = f * 0.7f + g * 0.2f;
                        break;
                    }
                    case "Ladrillo":
                    {
                        const int filas = 12, porFila = 6;
                        float fy = v * filas; int fila = Mathf.FloorToInt(fy);
                        float fx = u * porFila + (fila % 2 == 0 ? 0f : 0.5f); int col = Mathf.FloorToInt(fx);
                        float iy = fy - fila, ix = fx - col;
                        bool junta = iy < 0.10f || ix < 0.05f;
                        float tono = 0.62f + Hash(col % porFila, fila, 3) * 0.30f;
                        b = junta ? 0.88f : tono + (ruido.Fbm(u, v) - 0.5f) * 0.12f + (fino - 0.5f) * 0.05f;
                        a = junta ? 0f : 0.7f + Hash(col, fila, 9) * 0.3f;
                        break;
                    }
                    case "Chapa":
                    {
                        float onda = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 12f);
                        float oxido = ruido.Octava(0, u, v, 3, 1);
                        b = 0.60f + onda * 0.30f - oxido * 0.12f + (fino - 0.5f) * 0.06f;
                                                a = onda;
                        break;
                    }
                    case "Placa":
                    {
                        float f = ruido.Fbm(u, v);
                        b = 0.74f + (f - 0.5f) * 0.22f + (ruido.Octava(1, u + 0.2f, v + 0.6f) - 0.5f) * 0.10f + (fino - 0.5f) * 0.07f;
                        a = 0.6f + f * 0.2f;
                        break;
                    }
                    case "Madera":
                    {
                        const int tablas = 6;
                        float fy = v * tablas; int t = Mathf.FloorToInt(fy); float iy = fy - t;
                        float vena = ruido.Octava(2, u + Hash(t, 1, 5), v * 0.1f + t * 0.17f, 1, 8);
                        float tono = 0.70f + Hash(t, 4, 2) * 0.10f;
                        b = tono + (vena - 0.5f) * 0.30f + (fino - 0.5f) * 0.04f;
                                                a = 0.6f + vena * 0.3f;
                        break;
                    }
                    case "Lona":
                    {
                        float trama = 0.5f + 0.25f * Mathf.Sin(u * Mathf.PI * 2f * 64f) * Mathf.Sin(v * Mathf.PI * 2f * 64f);
                        float f = ruido.Fbm(u, v);
                        b = 0.76f + (trama - 0.5f) * 0.20f + (f - 0.5f) * 0.26f;
                        a = trama * 0.6f + f * 0.3f;
                        break;
                    }
                    case "Asfalto":
                    {
                        float f = ruido.Fbm(u, v);
                        b = 0.62f + (f - 0.5f) * 0.22f + (fino - 0.5f) * 0.28f;
                        if (fino > 0.985f) b = 0.95f;                                       // piedritas
                        a = f * 0.5f + fino * 0.4f;
                        break;
                    }
                    case "Agua":
                    {
                        float f = ruido.Fbm(u, v);
                        float ola = Mathf.Sin((u + f * 0.35f) * Mathf.PI * 2f * 5f) * Mathf.Sin((v + f * 0.25f) * Mathf.PI * 2f * 4f);
                        b = 0.78f + ola * 0.14f + (f - 0.5f) * 0.12f;
                        if (ola > 0.82f) b = 0.98f;                                          // crestas de espuma
                        a = 0.5f + ola * 0.35f;
                        break;
                    }
                    case "Oficina":
                    {
                        const int cols = 4, filas = 4;
                        float cx = u * cols, cy = v * filas; int ix = Mathf.FloorToInt(cx), iy = Mathf.FloorToInt(cy);
                        float fx = cx - ix, fy = cy - iy;
                        float f = ruido.Fbm(u, v);
                        b = 0.74f + (f - 0.5f) * 0.18f + (fino - 0.5f) * 0.05f;          // muro de hormigon
                        a = 0.6f;
                        bool ventana = fx > 0.16f && fx < 0.84f && fy > 0.30f && fy < 0.82f;
                        bool marco = fx > 0.12f && fx < 0.88f && fy > 0.26f && fy < 0.86f;
                        if (fy < 0.06f) { b *= 0.65f; a = 0.2f; }                         // losa entre pisos
                        if (marco && !ventana) { b = 0.58f; a = 0.8f; }
                        if (ventana)
                        {
                            bool prendida = Hash(ix, iy, 31) < 0.30f;
                            encendida[x, y] = prendida && !(Mathf.Abs(fx - 0.5f) < 0.025f || Mathf.Abs(fy - 0.56f) < 0.02f);
                            b = prendida ? 0.95f : 0.14f + Hash(ix, iy, 5) * 0.08f;
                            a = 0.1f;
                            bool montante = Mathf.Abs(fx - 0.5f) < 0.025f || Mathf.Abs(fy - 0.56f) < 0.02f;
                            if (montante) { b = 0.5f; a = 0.7f; }
                        }
                        break;
                    }
                    default:
                        b = 0.8f + (fino - 0.5f) * 0.1f; a = fino;
                        break;
                }
                brillo[x, y] = Mathf.Clamp01(b); altura[x, y] = Mathf.Clamp01(a);
            }

            if (!Directory.Exists(CarpetaTex)) Directory.CreateDirectory(CarpetaTex);
            string rutaC = $"{CarpetaTex}/T_Op_{nombre}.png", rutaN = $"{CarpetaTex}/T_Op_{nombre}_N.png";
            var colores = new Color32[N * N]; var normales = new Color32[N * N];
            const float fuerza = 3.2f;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                byte g8 = (byte)Mathf.RoundToInt(brillo[x, y] * 255f);
                colores[y * N + x] = new Color32(g8, g8, g8, 255);
                float dx = altura[(x + 1) % N, y] - altura[(x - 1 + N) % N, y], dy = altura[x, (y + 1) % N] - altura[x, (y - 1 + N) % N];
                var nn = new Vector3(-dx * fuerza, -dy * fuerza, 1f).normalized;
                normales[y * N + x] = new Color32((byte)Mathf.RoundToInt((nn.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nn.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((nn.z * 0.5f + 0.5f) * 255f), 255);
            }
            var tc = new Texture2D(N, N, TextureFormat.RGBA32, false); tc.SetPixels32(colores); tc.Apply();
            File.WriteAllBytes(rutaC, tc.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tc);
            var tn = new Texture2D(N, N, TextureFormat.RGBA32, false); tn.SetPixels32(normales); tn.Apply();
            File.WriteAllBytes(rutaN, tn.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tn);
            AssetDatabase.ImportAsset(rutaC, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(rutaN, ImportAssetOptions.ForceUpdate);
            ConfigurarImportador(rutaC, false); ConfigurarImportador(rutaN, true);
            Texture2D emisionTex = null;
            if (nombre == "Oficina")
            {
                string rutaE = $"{CarpetaTex}/T_Op_{nombre}_E.png";
                var em = new Color32[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) em[y * N + x] = encendida[x, y] ? new Color32(255, 205, 120, 255) : new Color32(0, 0, 0, 255);
                var te = new Texture2D(N, N, TextureFormat.RGBA32, false); te.SetPixels32(em); te.Apply();
                File.WriteAllBytes(rutaE, te.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(te);
                AssetDatabase.ImportAsset(rutaE, ImportAssetOptions.ForceUpdate); ConfigurarImportador(rutaE, false);
                emisionTex = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaE);
            }
            var par = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(rutaC), AssetDatabase.LoadAssetAtPath<Texture2D>(rutaN), emisionTex };
            texturasGeneradas[nombre] = par;
            return par;
        }

        static void ConfigurarImportador(string ruta, bool normal)
        {
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp == null) return;
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !normal;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.anisoLevel = 4;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
    }
}
