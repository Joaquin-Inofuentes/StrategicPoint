using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Ronda 13 · T16: assets menos primitivos. Se editan los PREFAB ASSETS (Mobiliario, PlazaCivica, Cam2_Patios y los dos camiones). Se conserva el GO raiz con su
    // NOMBRE y su COLLIDER (cobertura y NavMesh no cambian) y se agregan hijos visuales 'Detalle_R13' SIN collider, de UNA sola malla por prop (submeshes por material):
    //   auto (capo/techo escalonados, 4 ruedas, faros y luces emisivos, parabrisas oscuro, paragolpes), arbol (P_Env_ArbolA/B del kit), banco (listones y patas),
    //   contenedor (tapa, ruedas, refuerzos), hidrante, puesto de feria y CAMION nuevo (cabina con parabrisas, caja con lona, 6 ruedas, paragolpes, faros; UV en metros
    //   y normal map procedural) para Camion_3 y Camion_Patio.
    // Los scripts que dependen de nombres (PulidoNivel2Nov11.NombresConCuerpo, ColisionesDeLaCiudad, DestructibilidadDelNivel 402a/712a, DespejarServicio, CentrarFuente)
    // siguen viendo los mismos GO raiz: los renderers de las primitivas NO se apagan (salvo Tronco y Copa de los arboles, que se reemplazan por el arbol del kit).
    public static partial class OperacionBuilder
    {
        const string PrefabMobiliario = "Assets/_Project/Prefabs/Escena/Elementos/P_Op_Ciudad_Mobiliario.prefab";
        const string PrefabCamionCuartel = "Assets/_Project/Prefabs/Escena/Props/P_Esc_Camion.prefab";
        const string PrefabCamionPatio = "Assets/_Project/Prefabs/Escena/Unicos/P_Esc_Camion_Patio.prefab";

        // ---------------------------------------------------------------------------------------------------------------------
        // Constructor de mallas por piezas (cajas y cilindros) con UV planar en metros y submeshes por material
        // ---------------------------------------------------------------------------------------------------------------------
        sealed class PiezasR13
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector3> n = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<Vector4> tg = new List<Vector4>();
            readonly List<int>[] tri; readonly float tile;
            public int Piezas;
            public PiezasR13(int slots, float tile) { tri = new List<int>[slots]; for (int i = 0; i < slots; i++) tri[i] = new List<int>(); this.tile = tile; }

            void Poligono(Vector3[] p, Vector3[] nor, int slot)
            {
                var gn = (nor[0] + nor[1] + nor[2] + nor[nor.Length > 3 ? 3 : 2]).normalized;
                var g = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                bool dir = Vector3.Dot(g, gn) > 0f;
                int i0 = v.Count;
                for (int i = 0; i < p.Length; i++)
                {
                    v.Add(p[i]); n.Add(nor[i]);
                    var a = new Vector3(Mathf.Abs(gn.x), Mathf.Abs(gn.y), Mathf.Abs(gn.z));
                    Vector2 q; Vector3 t;
                    if (a.x >= a.y && a.x >= a.z) { q = new Vector2(p[i].z, p[i].y); t = Vector3.forward; }
                    else if (a.y >= a.z) { q = new Vector2(p[i].x, p[i].z); t = Vector3.right; }
                    else { q = new Vector2(p[i].x, p[i].y); t = Vector3.right; }
                    uv.Add(q / tile); tg.Add(new Vector4(t.x, t.y, t.z, -1f));
                }
                for (int i = 1; i < p.Length - 1; i++)
                {
                    if (dir) { tri[slot].Add(i0); tri[slot].Add(i0 + i); tri[slot].Add(i0 + i + 1); }
                    else { tri[slot].Add(i0); tri[slot].Add(i0 + i + 1); tri[slot].Add(i0 + i); }
                }
            }

            public void Caja(Vector3 c, Vector3 tam, int slot, Vector3 euler = default)
            {
                var q = Quaternion.Euler(euler); var h = tam * 0.5f;
                Vector3 P(float sx, float sy, float sz) => c + q * new Vector3(sx * h.x, sy * h.y, sz * h.z);
                void Cara(Vector3 nl, Vector3 a, Vector3 b, Vector3 cc, Vector3 d) { var nn = q * nl; Poligono(new[] { a, b, cc, d }, new[] { nn, nn, nn, nn }, slot); }
                Cara(Vector3.forward, P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1));
                Cara(Vector3.back, P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1));
                Cara(Vector3.right, P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1));
                Cara(Vector3.left, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1));
                Cara(Vector3.up, P(-1, 1, 1), P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1));
                Cara(Vector3.down, P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1));
                Piezas++;
            }

            // eje 0 = X, 1 = Y, 2 = Z (el eje del cilindro, antes de la rotacion)
            public void Cil(Vector3 c, int eje, float radio, float largo, int seg, int slot, Vector3 euler = default)
            {
                var q = Quaternion.Euler(euler);
                Vector3 a = eje == 0 ? Vector3.right : eje == 1 ? Vector3.up : Vector3.forward;
                Vector3 b = eje == 0 ? Vector3.up : eje == 1 ? Vector3.forward : Vector3.right;
                Vector3 cc = Vector3.Cross(a, b);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    var d0 = b * Mathf.Cos(a0) + cc * Mathf.Sin(a0); var d1 = b * Mathf.Cos(a1) + cc * Mathf.Sin(a1);
                    var p0 = c + q * (d0 * radio - a * largo * 0.5f); var p1 = c + q * (d1 * radio - a * largo * 0.5f);
                    var p2 = c + q * (d1 * radio + a * largo * 0.5f); var p3 = c + q * (d0 * radio + a * largo * 0.5f);
                    Poligono(new[] { p0, p1, p2, p3 }, new[] { q * d0, q * d1, q * d1, q * d0 }, slot);
                    var c0 = c + q * (-a * largo * 0.5f); var c1 = c + q * (a * largo * 0.5f);
                    Poligono(new[] { c1, p3, p2 }, new[] { q * a, q * a, q * a }, slot);
                    Poligono(new[] { c0, p1, p0 }, new[] { q * -a, q * -a, q * -a }, slot);
                }
                Piezas++;
            }

            public int Triangulos { get { int t = 0; foreach (var l in tri) t += l.Count / 3; return t; } }

            /// <summary>Guarda la malla como asset. escalaInversa = 1/escala del GO que la lleva (mallas en espacio unitario como Op_Camion*); rot = giro final en Y.</summary>
            public Mesh Guardar(string nombre, Vector3? escalaReal = null, float giroY = 0f)
            {
                var vs = new List<Vector3>(v); var ns = new List<Vector3>(n);
                var rot = Quaternion.Euler(0f, giroY, 0f);
                for (int i = 0; i < vs.Count; i++)
                {
                    vs[i] = rot * vs[i]; ns[i] = rot * ns[i];
                    if (escalaReal.HasValue)
                    {
                        var s = escalaReal.Value;
                        vs[i] = new Vector3(vs[i].x / s.x, vs[i].y / s.y, vs[i].z / s.z);
                        ns[i] = new Vector3(ns[i].x * s.x, ns[i].y * s.y, ns[i].z * s.z).normalized;
                    }
                }
                EscenasR13.AsegurarCarpetaGenerada();
                string ruta = $"{EscenasR13.CarpetaGenerada}/{nombre}.asset";
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
                bool nueva = m == null;
                if (nueva) m = new Mesh { name = nombre }; else m.Clear();
                m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uv); m.SetTangents(tg);
                m.subMeshCount = tri.Length;
                for (int s = 0; s < tri.Length; s++) m.SetTriangles(tri[s], s);
                m.RecalculateBounds();
                if (nueva) AssetDatabase.CreateAsset(m, ruta); else EditorUtility.SetDirty(m);
                return m;
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Detalle por tipo (todo en metros, origen en el centro del GO que lo lleva, salvo el auto: origen en el piso)
        // ---------------------------------------------------------------------------------------------------------------------
        static Mesh MallaAutoR13(out int tris, out int piezas)
        {
            var g = new PiezasR13(6, 2f);   // 0 chapa, 1 vidrio, 2 negro, 3 faro, 4 luz trasera, 5 cromo
            g.Caja(new Vector3(0f, 0.42f, 0f), new Vector3(1.95f, 0.34f, 4.5f), 2);                              // faldon
            g.Caja(new Vector3(0f, 1.17f, 1.45f), new Vector3(1.86f, 0.10f, 1.35f), 0);                           // capo
            g.Caja(new Vector3(0f, 1.17f, -1.55f), new Vector3(1.86f, 0.10f, 1.0f), 0);                           // baul
            g.Caja(new Vector3(0f, 1.45f, 0.62f), new Vector3(1.6f, 0.62f, 0.06f), 1, new Vector3(35f, 0f, 0f));  // parabrisas
            g.Caja(new Vector3(0f, 1.45f, -0.98f), new Vector3(1.6f, 0.58f, 0.06f), 1, new Vector3(-35f, 0f, 0f)); // luneta
            g.Caja(new Vector3(0f, 1.80f, -0.2f), new Vector3(1.62f, 0.08f, 1.1f), 0);                            // techo
            g.Caja(new Vector3(0.86f, 1.45f, -0.2f), new Vector3(0.04f, 0.42f, 1.5f), 1);                         // ventanillas
            g.Caja(new Vector3(-0.86f, 1.45f, -0.2f), new Vector3(0.04f, 0.42f, 1.5f), 1);
            foreach (float x in new[] { -0.97f, 0.97f }) foreach (float z in new[] { -1.4f, 1.4f }) g.Cil(new Vector3(x, 0.34f, z), 0, 0.34f, 0.26f, 14, 2);   // ruedas
            g.Caja(new Vector3(0.62f, 0.9f, 2.21f), new Vector3(0.34f, 0.16f, 0.06f), 3); g.Caja(new Vector3(-0.62f, 0.9f, 2.21f), new Vector3(0.34f, 0.16f, 0.06f), 3);   // faros
            g.Caja(new Vector3(0.66f, 0.95f, -2.21f), new Vector3(0.30f, 0.14f, 0.06f), 4); g.Caja(new Vector3(-0.66f, 0.95f, -2.21f), new Vector3(0.30f, 0.14f, 0.06f), 4); // traseras
            g.Caja(new Vector3(0f, 0.5f, 2.25f), new Vector3(1.9f, 0.18f, 0.14f), 5); g.Caja(new Vector3(0f, 0.5f, -2.25f), new Vector3(1.9f, 0.18f, 0.14f), 5);          // paragolpes
            g.Caja(new Vector3(0f, 0.85f, 2.22f), new Vector3(0.9f, 0.12f, 0.05f), 2);                            // parrilla
            tris = g.Triangulos; piezas = g.Piezas;
            return g.Guardar("R13_Detalle_Auto");
        }

        static Mesh MallaHidranteR13(out int tris, out int piezas)
        {
            var g = new PiezasR13(2, 1f);   // 0 cuerpo (pintura del hidrante), 1 cromo
            g.Cil(new Vector3(0f, -0.38f, 0f), 1, 0.23f, 0.14f, 10, 0);       // base
            g.Cil(new Vector3(0f, 0.43f, 0f), 1, 0.21f, 0.10f, 10, 0);        // tapa
            g.Cil(new Vector3(0f, 0.52f, 0f), 1, 0.11f, 0.10f, 10, 1);        // domo
            g.Cil(new Vector3(0f, 0.12f, 0f), 0, 0.07f, 0.56f, 8, 1);         // bocas laterales
            g.Cil(new Vector3(0f, 0.04f, -0.2f), 2, 0.09f, 0.14f, 8, 1);      // boca frontal
            g.Caja(new Vector3(0f, 0.59f, 0f), new Vector3(0.12f, 0.06f, 0.12f), 1);   // tuerca
            tris = g.Triangulos; piezas = g.Piezas;
            return g.Guardar("R13_Detalle_Hidrante");
        }

        static Mesh MallaContenedorR13(Vector3 s, out int tris, out int piezas)
        {
            var g = new PiezasR13(2, 2f);   // 0 chapa, 1 negro
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
            g.Caja(new Vector3(0f, hy + 0.04f, 0f), new Vector3(s.x + 0.1f, 0.08f, s.z + 0.1f), 0);                       // tapa
            foreach (float x in new[] { -hx + 0.25f, hx - 0.25f }) foreach (float z in new[] { -hz - 0.03f, hz + 0.03f }) g.Cil(new Vector3(x, -hy + 0.1f, z), 2, 0.1f, 0.06f, 10, 1);   // ruedas
            g.Caja(new Vector3(0f, hy - 0.1f, hz + 0.05f), new Vector3(s.x * 0.8f, 0.06f, 0.06f), 1);                    // asa
            foreach (float x in new[] { -hx * 0.5f, 0f, hx * 0.5f }) g.Caja(new Vector3(x, 0f, hz + 0.02f), new Vector3(0.08f, s.y * 0.9f, 0.05f), 0);   // refuerzos
            tris = g.Triangulos; piezas = g.Piezas;
            return g.Guardar($"R13_Detalle_Contenedor_{s.x:0.0}_{s.y:0.0}_{s.z:0.0}");
        }

        static Mesh MallaBancoR13(Vector3 s, out int tris, out int piezas)
        {
            var g = new PiezasR13(2, 1f);   // 0 madera, 1 metal
            bool largoZ = s.z >= s.x; float L = Mathf.Max(s.x, s.z), A = Mathf.Min(s.x, s.z), hy = s.y * 0.5f;
            Vector3 V(float a, float y, float b) => largoZ ? new Vector3(b, y, a) : new Vector3(a, y, b);   // a: a lo largo, b: a lo ancho
            Vector3 T(float a, float y, float b) => largoZ ? new Vector3(b, y, a) : new Vector3(a, y, b);
            foreach (float b in new[] { -A * 0.3f, 0f, A * 0.3f }) g.Caja(V(0f, hy + 0.03f, b), T(L, 0.06f, A * 0.22f), 0);          // listones del asiento
            foreach (float a in new[] { -L * 0.42f, L * 0.42f }) foreach (float b in new[] { -A * 0.38f, A * 0.38f }) g.Caja(V(a, -0.01f, b), T(0.08f, s.y - 0.06f, 0.08f), 1);   // patas
            g.Caja(V(0f, hy + 0.35f, -A * 0.46f), T(L, 0.10f, 0.05f), 0); g.Caja(V(0f, hy + 0.20f, -A * 0.46f), T(L, 0.10f, 0.05f), 0);   // respaldo
            tris = g.Triangulos; piezas = g.Piezas;
            return g.Guardar($"R13_Detalle_Banco_{L:0.0}_{A:0.0}_{(largoZ ? "Z" : "X")}");
        }

        static Mesh MallaPuestoR13(Vector3 s, out int tris, out int piezas)
        {
            var g = new PiezasR13(3, 1f);   // 0 madera, 1 lona, 2 cajones
            float hx = s.x * 0.5f, hy = s.y * 0.5f, hz = s.z * 0.5f;
            foreach (float x in new[] { -hx + 0.12f, hx - 0.12f }) foreach (float z in new[] { -hz + 0.1f, hz - 0.1f }) g.Caja(new Vector3(x, 0.9f, z), new Vector3(0.06f, 1.8f, 0.06f), 0);   // postes del toldo
            g.Caja(new Vector3(0f, hy - 0.06f, -hz - 0.02f), new Vector3(s.x * 0.96f, 0.35f, 0.03f), 1);                       // faldon delantero
            for (int i = 0; i < 4; i++) g.Caja(new Vector3(-hx * 0.6f + i * hx * 0.4f, hy + 0.11f, -hz * 0.2f), new Vector3(0.42f, 0.22f, 0.32f), 2, new Vector3(0f, i * 11f, 0f));   // cajones de fruta
            g.Caja(new Vector3(0f, -hy * 0.2f, hz - 0.08f), new Vector3(s.x * 0.9f, 0.04f, 0.3f), 0);                         // estante trasero
            // toldo a dos aguas (lona), mostrador, barras laterales y mas cajones: el puesto pasa de 10 a 18 cajas (216 triangulos, Bug985a pide >= 200 por prop de un solo renderer)
            g.Caja(new Vector3(0f, hy + 0.02f, -hz * 0.5f), new Vector3(s.x * 1.04f, 0.04f, hz * 1.1f), 1, new Vector3(-8f, 0f, 0f));
            g.Caja(new Vector3(0f, hy + 0.02f, hz * 0.5f), new Vector3(s.x * 1.04f, 0.04f, hz * 1.1f), 1, new Vector3(8f, 0f, 0f));
            g.Caja(new Vector3(0f, -hy * 0.1f, -hz + 0.2f), new Vector3(s.x * 0.92f, 0.05f, 0.45f), 0);                           // mostrador
            foreach (float x in new[] { -hx + 0.12f, hx - 0.12f }) g.Caja(new Vector3(x, 1.75f, 0f), new Vector3(0.05f, 0.05f, s.z * 0.9f), 0);   // largueros del toldo
            for (int i = 0; i < 3; i++) g.Caja(new Vector3(hx * 0.5f - i * 0.3f, hy + 0.3f, -hz * 0.2f), new Vector3(0.36f, 0.18f, 0.28f), 2, new Vector3(0f, 25f + i * 9f, 0f));
            tris = g.Triangulos; piezas = g.Piezas;
            return g.Guardar($"R13_Detalle_Puesto_{s.x:0.0}_{s.y:0.0}_{s.z:0.0}");
        }

        /// <summary>Camion en espacio unitario del GO raiz (que lleva la escala real). largoEnX = frente en +X.</summary>
        static Mesh MallaCamionR13(Vector3 escala, out int tris, out int piezas)
        {
            bool largoEnX = escala.x > escala.z;
            float L = Mathf.Max(escala.x, escala.z), W = Mathf.Min(escala.x, escala.z), H = escala.y;
            var g = new PiezasR13(6, 2f);   // 0 chapa, 1 vidrio, 2 negro, 3 lona, 4 faro, 5 luz trasera
            // modelo de 3 x 2,8 x 7 m (centro del cajon en el origen; el piso queda en y = -1,4)
            g.Caja(new Vector3(0f, -0.85f, 0f), new Vector3(1.9f, 0.35f, 6.6f), 2);                                    // chasis
            g.Caja(new Vector3(0f, 0.2f, -1.05f), new Vector3(2.7f, 1.8f, 4.4f), 0);                                    // caja de carga
            g.Caja(new Vector3(0f, 1.25f, -1.05f), new Vector3(2.6f, 0.3f, 4.2f), 3);                                  // lona
            g.Caja(new Vector3(0f, 1.05f, -1.05f), new Vector3(2.76f, 0.1f, 4.46f), 3);                                // faldon de la lona
            g.Caja(new Vector3(0f, -0.1f, 1.7f), new Vector3(2.6f, 1.9f, 1.4f), 0);                                     // cabina
            g.Caja(new Vector3(0f, 0.88f, 1.7f), new Vector3(2.7f, 0.08f, 1.5f), 0);                                    // techo de cabina
            g.Caja(new Vector3(0f, -0.55f, 2.95f), new Vector3(2.3f, 1.0f, 1.1f), 0);                                   // capo
            g.Caja(new Vector3(0f, 0.45f, 2.41f), new Vector3(2.3f, 0.65f, 0.05f), 1, new Vector3(-12f, 0f, 0f));      // parabrisas
            g.Caja(new Vector3(1.31f, 0.3f, 1.7f), new Vector3(0.05f, 0.6f, 1.0f), 1); g.Caja(new Vector3(-1.31f, 0.3f, 1.7f), new Vector3(0.05f, 0.6f, 1.0f), 1);   // ventanillas
            foreach (float z in new[] { 2.8f, -0.6f, -2.1f }) foreach (float x in new[] { -1.3f, 1.3f }) g.Cil(new Vector3(x, -0.85f, z), 0, 0.55f, 0.4f, 16, 2);    // 6 ruedas
            g.Caja(new Vector3(0f, -1.05f, 3.55f), new Vector3(2.5f, 0.25f, 0.15f), 2);                                 // paragolpes
            g.Caja(new Vector3(0.85f, -0.45f, 3.52f), new Vector3(0.35f, 0.25f, 0.06f), 4); g.Caja(new Vector3(-0.85f, -0.45f, 3.52f), new Vector3(0.35f, 0.25f, 0.06f), 4);   // faros
            g.Caja(new Vector3(0.9f, -0.5f, -3.28f), new Vector3(0.3f, 0.2f, 0.05f), 5); g.Caja(new Vector3(-0.9f, -0.5f, -3.28f), new Vector3(0.3f, 0.2f, 0.05f), 5);    // luces traseras
            for (int i = 0; i < 5; i++) { float z = -2.8f + i * 0.9f; g.Caja(new Vector3(1.37f, 0.2f, z), new Vector3(0.05f, 1.7f, 0.08f), 2); g.Caja(new Vector3(-1.37f, 0.2f, z), new Vector3(0.05f, 1.7f, 0.08f), 2); }   // travesanos
            tris = g.Triangulos; piezas = g.Piezas;
            // se reescala a las medidas reales del GO y se pasa a espacio unitario; la variante X gira el frente de +Z a +X
            var m = g.Guardar(largoEnX ? "R13_Camion_X" : "R13_Camion_Z", largoEnX ? new Vector3(L, H, W) : new Vector3(W, H, L), largoEnX ? 90f : 0f);
            return m;
        }

        static Material[] MaterialesCamionR13() => new[]
        {
            MatTexR13("Camion_Chapa", new Color(0.40f, 0.46f, 0.30f), "Panel", 2f, 0.9f, 0.22f),
            Mat("Cristal", new Color(0.35f, 0.6f, 0.75f)), Mat("Negro", new Color(0.07f, 0.07f, 0.08f)), Mat("LonaVerde", new Color(0.40f, 0.48f, 0.29f)),
            Mat("R13_Faro", new Color(1f, 0.95f, 0.7f), 1.8f), Mat("R13_LuzTrasera", new Color(1f, 0.10f, 0.08f), 1.6f),
        };

        // ---------------------------------------------------------------------------------------------------------------------
        // Aplicacion sobre los prefabs
        // ---------------------------------------------------------------------------------------------------------------------
        static GameObject HijoDetalleR13(Transform padre, Mesh m, Material[] mats)
        {
            for (int i = padre.childCount - 1; i >= 0; i--) if (padre.GetChild(i).name == "Detalle_R13") Object.DestroyImmediate(padre.GetChild(i).gameObject);
            var go = new GameObject("Detalle_R13");
            go.transform.SetParent(padre, false);
            var s = padre.localScale;   // los GO de las primitivas llevan la escala: el detalle (en metros) la compensa
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return go;
        }

        static Material MaterialDe(Transform t)
        {
            var r = t.GetComponent<MeshRenderer>(); if (r == null) r = t.GetComponentInChildren<MeshRenderer>(true);
            return r != null ? r.sharedMaterial : null;
        }

        static GameObject ArbolDelKitR13(Transform padre, float alturaObjetivo, int variante)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(DirArte + (variante % 2 == 0 ? "P_Env_ArbolA.prefab" : "P_Env_ArbolB.prefab"));
            if (pf == null) return null;
            for (int i = padre.childCount - 1; i >= 0; i--) if (padre.GetChild(i).name == "Detalle_R13") Object.DestroyImmediate(padre.GetChild(i).gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, padre);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Detalle_R13";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0f, variante * 47f, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            // altura medida con los bounds de las mallas (en el espacio del prefab, escala 1)
            float alto = 0f;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) { var b = mf.sharedMesh.bounds; alto = Mathf.Max(alto, (b.max.y * mf.transform.lossyScale.y)); }
            float k = alto > 0.1f ? alturaObjetivo / alto : 1f;
            var ps = padre.lossyScale; go.transform.localScale = new Vector3(k / Mathf.Max(0.0001f, ps.x), k / Mathf.Max(0.0001f, ps.y), k / Mathf.Max(0.0001f, ps.z));
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return go;
        }

        static int DetallarPrefabR13(GameObject raiz, StringBuilder sb, Dictionary<string, int> cuenta, ref int trisTotal)
        {
            int tocados = 0;
            Material cromo = Mat("MetalClaro", new Color(0.55f, 0.57f, 0.60f)), negro = Mat("Negro", new Color(0.07f, 0.07f, 0.08f)), madera = Mat("MaderaVieja", new Color(0.36f, 0.26f, 0.17f));
            Material faro = Mat("R13_Faro", new Color(1f, 0.95f, 0.7f), 1.8f), trasera = Mat("R13_LuzTrasera", new Color(1f, 0.10f, 0.08f), 1.6f), vidrio = Mat("Ciudad_VidrioOscuro", new Color(0.10f, 0.14f, 0.18f));
            int variante = 0;
            void Cuenta(string tipo) { cuenta[tipo] = (cuenta.TryGetValue(tipo, out var c) ? c : 0) + 1; tocados++; }
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t.name == "Detalle_R13" || t.IsChildOf(raiz.transform) == false) continue;
                string n = t.name;
                bool dentroDeDetalle = false; for (var q = t.parent; q != null; q = q.parent) if (q.name == "Detalle_R13") dentroDeDetalle = true;
                if (dentroDeDetalle) continue;
                if (n.StartsWith("Auto_") && t.Find("Carroceria") != null)
                {
                    var m = MallaAutoR13(out int tr, out int pz);
                    var chapa = MaterialDe(t.Find("Carroceria")) ?? Mat("AutoRojo", new Color(0.72f, 0.12f, 0.1f));
                    HijoDetalleR13(t, m, new[] { chapa, vidrio, negro, faro, trasera, cromo }); trisTotal += tr; Cuenta("Auto");
                }
                else if (n == "Hidrante")
                {
                    var m = MallaHidranteR13(out int tr, out int pz);
                    HijoDetalleR13(t, m, new[] { MaterialDe(t) ?? Mat("Ciudad_Chapa0", new Color(0.55f, 0.16f, 0.14f)), cromo }); trisTotal += tr; Cuenta("Hidrante");
                }
                else if (n == "Contenedor" && t.GetComponent<MeshRenderer>() != null)
                {
                    var ls = t.lossyScale; var m = MallaContenedorR13(ls, out int tr, out int pz);
                    HijoDetalleR13(t, m, new[] { MaterialDe(t), negro }); trisTotal += tr; Cuenta("Contenedor");
                }
                else if (n == "Banco" && t.GetComponent<MeshRenderer>() != null)
                {
                    var ls = t.lossyScale; var m = MallaBancoR13(ls, out int tr, out int pz);
                    HijoDetalleR13(t, m, new[] { MaterialDe(t) ?? madera, cromo }); trisTotal += tr; Cuenta("Banco");
                }
                else if (n == "Patio_Puesto" && t.GetComponent<MeshRenderer>() != null)
                {
                    var ls = t.lossyScale; var m = MallaPuestoR13(ls, out int tr, out int pz);
                    HijoDetalleR13(t, m, new[] { madera, Mat("Ciudad_Chapa1", new Color(0.20f, 0.30f, 0.45f)), Mat("Cobertura", new Color(0.62f, 0.52f, 0.34f)) }); trisTotal += tr; Cuenta("Puesto");
                }
                else if ((n.StartsWith("Arbol_") || n == "Patio_Arbol") && t.Find("Tronco") != null && t.Find("Copa") != null)
                {
                    var tronco = t.Find("Tronco"); float e = Mathf.Max(0.4f, tronco.localScale.y / 1.6f);
                    foreach (var r in new[] { tronco.GetComponent<MeshRenderer>(), t.Find("Copa").GetComponent<MeshRenderer>() }) if (r != null) r.enabled = false;   // el collider del Tronco se queda
                    var kit = ArbolDelKitR13(t, 5.4f * e, variante++);
                    if (kit != null) { trisTotal += TriangulosDeR13(kit); Cuenta("Arbol"); }
                }
            }
            return tocados;
        }

        [MenuItem("Strategic Point/Operacion/R13 T16 · Assets menos primitivos")]
        static void MenuDetalleR13() => Debug.Log(DetalleR13EscenaAbierta());

        /// <summary>
        /// T16. Edita los prefabs Mobiliario, PlazaCivica, Cam2_Patios y los camiones (Camion_3 y Camion_Patio: malla nueva en el GO raiz, mismo collider y escala).
        /// Firma: OperacionBuilder.DetalleR13EscenaAbierta(bool soloInforme = false). Debe correr DESPUES de T15 (mismo prefab de la plaza) y de T12.
        /// </summary>
        public static string DetalleR13EscenaAbierta(bool soloInforme = false)
        {
            var sb = new StringBuilder("== R13 T16 detalle" + (soloInforme ? " (SOLO INFORME)" : "") + "\n");
            if (!EscenasR13.Asegurar(sb, soloInforme)) return EscenasR13.Cerrar(sb, "r13_t16.txt");
            var prefabs = new[] { PrefabMobiliario, PrefabPlazaCivica, PrefabPatios };
            var cuentaTotal = new Dictionary<string, int>(); int trisTotal = 0;

            foreach (var p in prefabs)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf == null) { sb.AppendLine("AVISO no existe " + p); continue; }
                if (soloInforme)
                {
                    int autos = 0, arboles = 0, otros = 0;
                    foreach (var t in pf.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Auto_") && t.Find("Carroceria") != null) autos++;
                        else if ((t.name.StartsWith("Arbol_") || t.name == "Patio_Arbol") && t.Find("Tronco") != null) arboles++;
                        else if (t.name == "Hidrante" || t.name == "Contenedor" || t.name == "Banco" || t.name == "Patio_Puesto") otros++;
                    }
                    sb.AppendLine($"{p}: {autos} autos, {arboles} arboles, {otros} otros props a detallar");
                    continue;
                }
                var cuenta = new Dictionary<string, int>(); int tr0 = 0;
                EscenasR13.EditarPrefab(p, raiz => { DetallarPrefabR13(raiz, sb, cuenta, ref tr0); }, sb);
                foreach (var kv in cuenta) { sb.AppendLine($"{p}: {kv.Value} {kv.Key}"); cuentaTotal[kv.Key] = (cuentaTotal.TryGetValue(kv.Key, out var c) ? c : 0) + kv.Value; }
                trisTotal += tr0;
            }

            // camiones
            foreach (var p in new[] { PrefabCamionCuartel, PrefabCamionPatio })
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf == null) { sb.AppendLine("AVISO no existe " + p); continue; }
                var mf = pf.GetComponent<MeshFilter>();
                sb.AppendLine($"{p}: malla actual {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-")} escala {pf.transform.localScale}");
                if (soloInforme) continue;
                EscenasR13.EditarPrefab(p, raiz =>
                {
                    var f = raiz.GetComponent<MeshFilter>(); var r = raiz.GetComponent<MeshRenderer>();
                    if (f == null || r == null) { sb.AppendLine("AVISO " + p + " no tiene MeshFilter/MeshRenderer en la raiz"); return; }
                    f.sharedMesh = MallaCamionR13(raiz.transform.localScale, out int tr, out int pz);
                    r.sharedMaterials = MaterialesCamionR13();
                    trisTotal += tr; cuentaTotal["Camion"] = (cuentaTotal.TryGetValue("Camion", out var c) ? c : 0) + 1;
                    sb.AppendLine($"  camion nuevo: {pz} piezas, {tr} triangulos");
                }, sb);
            }

            if (!soloInforme) AssetDatabase.SaveAssets();
            var resumen = new StringBuilder();
            foreach (var kv in cuentaTotal) resumen.Append($"{kv.Key}={kv.Value} ");
            sb.Insert(0, $"props detallados: {resumen}triangulos nuevos={trisTotal}. Falta: contar renderers activos de la ciudad (Bug985a) y porta sin ejecutar a ArmarAuto/ArmarArbol/MallaCamion si se quiere que sobreviva a una reconstruccion.\n");
            return EscenasR13.Cerrar(sb, "r13_t16.txt");
        }
    }
}
