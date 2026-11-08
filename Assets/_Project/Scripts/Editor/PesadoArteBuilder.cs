using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SP.EditorTools
{
    // Arte del enemigo PESADO (pedido 2026-11-07: "que haga referencia a ese soldado [el Heavy de TF2], muy parecido pero con mi estilo de arte;
    // vestilo distinto, un cinturon, geometria mas diferente, mismo trimsheet"). Todo sale del mismo trimsheet de 8x8 celdas (cada pieza usa UNA
    // celda: su UV cae en el centro de la celda, asi no hay degradados):
    //   - cuerpo: el del Comando SIN casco (calvo), ensanchado en la propia malla (torso, caderas, brazos, cuello y botas), con el color por zona:
    //     camisa roja, antebrazos y cabeza piel, guantes y botas negros, pantalon oliva, barba oscura (caras inferiores y delanteras de la cabeza);
    //   - piezas: chaleco oscuro, panza de camisa roja bajo el chaleco, cinturon ancho de cuero con hebilla de laton y cartucheras, bandolera
    //     cruzada de balas de laton, ceja fruncida y mochila de municion (todas atadas 100 % a un hueso);
    //   - arma: la minigun (P_Wpn_Minigun): seis canones, abrazaderas, carcasa, motor, tambor de municion con cinta de balas y asas.
    // Menu: Strategic Point/Arte/9. Generar enemigo pesado (malla + minigun).
    public static class PesadoArteBuilder
    {
        // Celdas del trimsheet (columna + 8 * fila contando desde ABAJO). Se evitan los azules/verdes: el material enemigo los vuelve oxido.
        // 2026-11-07: paleta propia del pesado (mono NARANJA + pantalon gris acero) para que no se confunda con el resto de los enemigos, que son rojos/oxido.
        // "Roja" y "Oliva" conservan el nombre por historia: ahora son naranja (celda 12) y acero (celda 34).
        const int Roja = 12, Piel = 24, PielOscura = 25, Negro = 1, Chaleco = 46, Cuero = 29, CueroOscuro = 60, Laton = 38, Oliva = 34, Barba = 63, Metal = 45, Acero = 47, Laton2 = 28;

        static Vector2 Celda(int c) => new Vector2((c % 8 + 0.5f) / 8f, (c / 8 + 0.5f) / 8f);

        // ----------------------------------------------------------------- utilidades de malla
        public class Acum
        {
            public List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public List<Vector2> UV = new List<Vector2>();
            public List<int> T = new List<int>();
            public List<BoneWeight> W = new List<BoneWeight>();
        }

        static readonly int[][] CarasCaja =
        {
            new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 },
        };
        static readonly Vector3[] Esquinas =
        {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
            new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1),
        };

        public static void Caja(Acum a, Vector3 centro, Vector3 tam, Quaternion rot, int celda, int hueso)
        {
            var uv = Celda(celda);
            foreach (var cara in CarasCaja)
            {
                int b = a.V.Count;
                var p0 = Esquinas[cara[0]]; var p1 = Esquinas[cara[1]]; var p2 = Esquinas[cara[2]];
                var n = -(rot * Vector3.Cross(Vector3.Scale(p1 - p0, tam), Vector3.Scale(p2 - p1, tam)).normalized);   // las caras de Esquinas estan definidas con la normal hacia adentro
                foreach (int k in cara)
                {
                    a.V.Add(centro + rot * Vector3.Scale(Esquinas[k] * 0.5f, tam));
                    a.N.Add(n); a.UV.Add(uv); a.W.Add(new BoneWeight { boneIndex0 = hueso, weight0 = 1f });
                }
                a.T.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
        }

        // Cilindro cuyo eje es +Z local (despues se orienta con rot).
        public static void Cilindro(Acum a, Vector3 centro, float radio, float largo, Quaternion rot, int celda, int hueso, int lados = 10)
        {
            var uv = Celda(celda);
            int b0 = a.V.Count;
            for (int i = 0; i < lados; i++)
            {
                float t0 = i / (float)lados * Mathf.PI * 2f, t1 = (i + 1) / (float)lados * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(t0), Mathf.Sin(t0), 0), d1 = new Vector3(Mathf.Cos(t1), Mathf.Sin(t1), 0);
                Vector3 z = Vector3.forward * largo * 0.5f;
                int b = a.V.Count;
                a.V.Add(centro + rot * (d0 * radio - z)); a.V.Add(centro + rot * (d1 * radio - z)); a.V.Add(centro + rot * (d1 * radio + z)); a.V.Add(centro + rot * (d0 * radio + z));
                a.N.Add(rot * d0); a.N.Add(rot * d1); a.N.Add(rot * d1); a.N.Add(rot * d0);
                for (int k = 0; k < 4; k++) { a.UV.Add(uv); a.W.Add(new BoneWeight { boneIndex0 = hueso, weight0 = 1f }); }
                a.T.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                // tapas
                foreach (int signo in new[] { -1, 1 })
                {
                    int c = a.V.Count; Vector3 zc = Vector3.forward * largo * 0.5f * signo; Vector3 nn = rot * (Vector3.forward * signo);
                    a.V.Add(centro + rot * zc); a.V.Add(centro + rot * (d0 * radio + zc)); a.V.Add(centro + rot * (d1 * radio + zc));
                    for (int k = 0; k < 3; k++) { a.N.Add(nn); a.UV.Add(uv); a.W.Add(new BoneWeight { boneIndex0 = hueso, weight0 = 1f }); }
                    if (signo > 0) a.T.AddRange(new[] { c, c + 1, c + 2 }); else a.T.AddRange(new[] { c, c + 2, c + 1 });
                }
            }
        }

        static string NombreHueso(SkinnedMeshRenderer piel, int idx) => idx >= 0 && idx < piel.bones.Length ? piel.bones[idx].name.Replace("mixamorig:", "") : "";
        static int Dominante(BoneWeight w)
        {
            int b = w.boneIndex0; float m = w.weight0;
            if (w.weight1 > m) { m = w.weight1; b = w.boneIndex1; }
            if (w.weight2 > m) { m = w.weight2; b = w.boneIndex2; }
            if (w.weight3 > m) { b = w.boneIndex3; }
            return b;
        }
        enum Zona { Otro, Cabeza, Cuello, Torso, Caderas, Brazo, Antebrazo, Mano, Pierna, Bota }
        static Zona ZonaDe(string h)
        {
            if (h.StartsWith("Head")) return Zona.Cabeza;
            if (h == "Neck") return Zona.Cuello;
            if (h.StartsWith("Spine")) return Zona.Torso;
            if (h == "Hips") return Zona.Caderas;
            if (h.EndsWith("Shoulder") || h.EndsWith("Arm") && !h.EndsWith("ForeArm")) return Zona.Brazo;
            if (h.EndsWith("ForeArm")) return Zona.Antebrazo;
            if (h.Contains("Hand")) return Zona.Mano;
            if (h.EndsWith("UpLeg") || h.EndsWith("Leg")) return Zona.Pierna;
            if (h.EndsWith("Foot") || h.EndsWith("Toe_End") || h.EndsWith("ToeBase")) return Zona.Bota;
            return Zona.Otro;
        }

        // ----------------------------------------------------------------- cuerpo del pesado
        // Recibe el cuerpo ya llevado al espacio de la malla riggeada (con pesos) y lo rehace: engorda, repinta por zonas y agrega las piezas.
        public static void ArmarCuerpo(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, List<BoneWeight> pesos, SkinnedMeshRenderer piel)
        {
            int n = verts.Count;
            var zona = new Zona[n];
            for (int i = 0; i < n; i++) zona[i] = ZonaDe(NombreHueso(piel, Dominante(pesos[i])));

            // alto del cuerpo para medir todo en "u" (1 u = alto/1,8: un cuerpo de 1,8 m mide 1,8 u)
            float ymin = float.MaxValue, ymax = float.MinValue;
            foreach (var p in verts) { ymin = Mathf.Min(ymin, p.y); ymax = Mathf.Max(ymax, p.y); }
            float u = (ymax - ymin) / 1.8f;

            Bounds Caja_(System.Predicate<Zona> f)
            {
                bool primero = true; var b = new Bounds();
                for (int i = 0; i < n; i++) if (f(zona[i])) { if (primero) { b = new Bounds(verts[i], Vector3.zero); primero = false; } else b.Encapsulate(verts[i]); }
                return b;
            }
            var bCabeza = Caja_(z => z == Zona.Cabeza);
            var bTorso0 = Caja_(z => z == Zona.Torso);
            float cx = bTorso0.center.x, cz = bTorso0.center.z;

            // --- engorde en la propia malla (en el espacio de la malla; los huesos no cambian)
            for (int i = 0; i < n; i++)
            {
                var p = verts[i]; float sx = 1f, sz = 1f;
                switch (zona[i])
                {
                    case Zona.Torso: sx = 1.40f; sz = 1.38f; break;
                    case Zona.Caderas: sx = 1.32f; sz = 1.28f; break;
                    case Zona.Brazo: case Zona.Antebrazo: case Zona.Mano: sx = 1.40f; sz = 1.15f; break;
                    case Zona.Cuello: sx = 1.45f; sz = 1.45f; break;
                    case Zona.Pierna: sx = 1.18f; sz = 1.18f; break;
                    case Zona.Bota: sx = 1.22f; sz = 1.30f; break;
                }
                if (zona[i] == Zona.Pierna || zona[i] == Zona.Bota) { /* piernas: ensanche alrededor de su propio eje (x de cada pierna) */
                    float lado = p.x >= cx ? 1f : -1f; float eje = cx + lado * Mathf.Abs(bTorso0.size.x) * 0.16f; p.x = eje + (p.x - eje) * sx; }
                else p.x = cx + (p.x - cx) * sx;
                p.z = cz + (p.z - cz) * sz;
                verts[i] = p;
                norms[i] = new Vector3(norms[i].x / sx, norms[i].y, norms[i].z / sz).normalized;
            }
            bCabeza = Caja_(z => z == Zona.Cabeza);
            var bTorso = Caja_(z => z == Zona.Torso);
            var bCad = Caja_(z => z == Zona.Caderas);

            // --- triangulos desligados por cara (una celda por triangulo: sin degradados de trimsheet)
            var a = new Acum();
            float yBarba = bCabeza.min.y + bCabeza.size.y * 0.52f;
            for (int t = 0; t < tris.Count; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                var zz = zona[i0] == zona[i1] || zona[i0] == zona[i2] ? zona[i0] : zona[i1] == zona[i2] ? zona[i1] : zona[i0];
                var cen = (verts[i0] + verts[i1] + verts[i2]) / 3f;
                var nrm = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).normalized;
                int celda;
                switch (zz)
                {
                    case Zona.Cabeza:
                        celda = (cen.y < yBarba && (cen.z > bCabeza.center.z - bCabeza.size.z * 0.05f || nrm.y < -0.5f)) ? Barba : Piel; break;
                    case Zona.Cuello: celda = PielOscura; break;
                    case Zona.Torso: celda = Roja; break;
                    case Zona.Caderas: celda = Oliva; break;
                    case Zona.Brazo: celda = Roja; break;
                    case Zona.Antebrazo: celda = PielOscura; break;
                    case Zona.Mano: celda = Negro; break;
                    case Zona.Pierna: celda = Oliva; break;
                    case Zona.Bota: celda = Negro; break;
                    default: celda = Chaleco; break;
                }
                var uv = Celda(celda);
                foreach (int i in new[] { i0, i1, i2 })
                {
                    a.T.Add(a.V.Count); a.V.Add(verts[i]); a.N.Add(norms[i]); a.UV.Add(uv); a.W.Add(pesos[i]);
                }
            }

            int hSp2 = IndiceDeHuesoPorNombre(piel, "Spine2"), hHips = IndiceDeHuesoPorNombre(piel, "Hips"), hHead = IndiceDeHuesoPorNombre(piel, "Head");
            var q = Quaternion.identity;
            float tx = bTorso.size.x, ty = bTorso.size.y, tz = bTorso.size.z;
            Vector3 tc = bTorso.center; Vector3 hc = bCad.center;

            // chaleco oscuro sobre el torso (abierto en el cuello: mas bajo que los hombros) y panza de camisa roja que asoma por debajo
            Caja(a, tc + new Vector3(0, -ty * 0.06f, 0), new Vector3(tx * 1.02f, ty * 0.80f, tz * 1.10f), q, Chaleco, hSp2);
            Caja(a, new Vector3(hc.x, bCad.max.y - bCad.size.y * 0.05f, hc.z + bCad.size.z * 0.08f), new Vector3(bCad.size.x * 1.00f, bCad.size.y * 0.55f, bCad.size.z * 1.34f), q, Roja, hHips);
            // cinturon ancho de cuero con hebilla de laton y dos cartucheras
            float yBelt = bCad.max.y - bCad.size.y * 0.14f;
            Caja(a, new Vector3(hc.x, yBelt, hc.z + bCad.size.z * 0.05f), new Vector3(bCad.size.x * 1.14f, 0.11f * u, bCad.size.z * 1.46f), q, Cuero, hHips);
            Caja(a, new Vector3(hc.x, yBelt, hc.z + bCad.size.z * 0.05f + bCad.size.z * 0.73f), new Vector3(0.15f * u, 0.12f * u, 0.035f * u), q, Laton2, hHips);
            foreach (float lado in new[] { -1f, 1f })
            {
                Caja(a, new Vector3(hc.x + lado * bCad.size.x * 0.40f, yBelt - 0.02f * u, hc.z + bCad.size.z * 0.62f), new Vector3(0.10f * u, 0.12f * u, 0.07f * u), q, CueroOscuro, hHips);
                Caja(a, new Vector3(hc.x + lado * bCad.size.x * 0.40f, yBelt + 0.045f * u, hc.z + bCad.size.z * 0.62f), new Vector3(0.105f * u, 0.025f * u, 0.075f * u), q, Laton, hHips);
            }
            // bandolera cruzada: tira de cuero en diagonal + balas de laton con punta roja
            var rotB = Quaternion.Euler(0, 0, -38f);
            Caja(a, tc + new Vector3(0, ty * 0.02f, 0), new Vector3(0.12f * u, ty * 1.08f, tz * 1.16f), rotB, Cuero, hSp2);
            int balas = 8;
            for (int k = 0; k < balas; k++)
            {
                float s = Mathf.Lerp(-0.46f, 0.46f, k / (float)(balas - 1)) * ty * 1.02f;
                var centro = tc + new Vector3(0, ty * 0.02f, 0) + rotB * new Vector3(0, s, 0);
                var frente = new Vector3(0, 0, tz * 0.58f + 0.015f * u);
                var rotBala = rotB;
                Caja(a, centro + frente, new Vector3(0.075f * u, 0.12f * u, 0.07f * u), rotBala, Laton, hSp2);
                Caja(a, centro + frente + rotB * new Vector3(0, 0.07f * u, 0), new Vector3(0.06f * u, 0.04f * u, 0.06f * u), rotBala, Roja, hSp2);
            }
            // mochila de municion a la espalda
            Caja(a, tc + new Vector3(0, -ty * 0.05f, -tz * 0.58f - 0.07f * u), new Vector3(tx * 0.62f, ty * 0.52f, 0.16f * u), q, CueroOscuro, hSp2);
            Caja(a, tc + new Vector3(0, ty * 0.19f, -tz * 0.58f - 0.07f * u), new Vector3(tx * 0.66f, 0.05f * u, 0.18f * u), q, Laton, hSp2);
            // ceja fruncida y mandibula cuadrada (cabeza calva con barba)
            Caja(a, new Vector3(bCabeza.center.x, bCabeza.min.y + bCabeza.size.y * 0.62f, bCabeza.max.z + 0.005f * u), new Vector3(bCabeza.size.x * 0.92f, bCabeza.size.y * 0.09f, 0.05f * u), Quaternion.Euler(8f, 0, 0), Negro, hHead);
            Caja(a, new Vector3(bCabeza.center.x, bCabeza.min.y + bCabeza.size.y * 0.16f, bCabeza.max.z - 0.01f * u), new Vector3(bCabeza.size.x * 0.84f, bCabeza.size.y * 0.30f, 0.07f * u), q, Barba, hHead);

            verts.Clear(); norms.Clear(); uvs.Clear(); tris.Clear(); pesos.Clear();
            verts.AddRange(a.V); norms.AddRange(a.N); uvs.AddRange(a.UV); tris.AddRange(a.T); pesos.AddRange(a.W);
            Debug.Log($"[PesadoArte] cuerpo: {verts.Count} vertices, 1 u = {u:0.000}, torso {bTorso.size}, caderas {bCad.size}, cabeza {bCabeza.size}");
        }

        static int IndiceDeHuesoPorNombre(SkinnedMeshRenderer piel, string nombre)
        {
            for (int i = 0; i < piel.bones.Length; i++) if (piel.bones[i].name == "mixamorig:" + nombre) return i;
            return 0;
        }

        // ----------------------------------------------------------------- minigun
        const string PrefabRuta = "Assets/_Project/Resources/Weapons/P_Wpn_Minigun.prefab";
        const string MallaRuta = "Assets/_Project/Resources/Weapons/Mesh_Minigun.asset";
        const string TrimsheetMat = "Assets/_Project/Materials/ArteMundo/MAT_Trimsheet.mat";

        public static void GenerarMinigun()
        {
            var a = new Acum(); var q = Quaternion.identity; var ejeZ = Quaternion.identity;
            // la boca apunta a +Z (igual que el resto de armas); el largo total ronda 1,1 m y el origen queda en su centro
            // seis canones en circulo + tres abrazaderas
            for (int k = 0; k < 6; k++)
            {
                float ang = k / 6f * Mathf.PI * 2f;
                Cilindro(a, new Vector3(Mathf.Cos(ang) * 0.052f, Mathf.Sin(ang) * 0.052f, 0.30f), 0.017f, 0.58f, ejeZ, Acero, 0, 8);
            }
            Cilindro(a, new Vector3(0, 0, 0.56f), 0.082f, 0.04f, ejeZ, Metal, 0, 12);
            Cilindro(a, new Vector3(0, 0, 0.36f), 0.080f, 0.03f, ejeZ, Metal, 0, 12);
            Cilindro(a, new Vector3(0, 0, 0.12f), 0.084f, 0.04f, ejeZ, Metal, 0, 12);
            // carcasa del bloque de canones y motor trasero
            Cilindro(a, new Vector3(0, 0, -0.06f), 0.095f, 0.30f, ejeZ, Chaleco, 0, 12);
            Caja(a, new Vector3(0, 0, -0.34f), new Vector3(0.20f, 0.22f, 0.30f), q, Negro, 0);
            Caja(a, new Vector3(0, 0.125f, -0.30f), new Vector3(0.10f, 0.04f, 0.18f), q, Roja, 0);
            // tambor de municion bajo el arma (eje X) y cinta de balas que sube a la recamara
            Cilindro(a, new Vector3(0, -0.20f, -0.20f), 0.15f, 0.17f, Quaternion.Euler(0, 90f, 0), Cuero, 0, 14);
            Cilindro(a, new Vector3(0, -0.20f, -0.20f), 0.155f, 0.03f, Quaternion.Euler(0, 90f, 0), Laton2, 0, 14);
            for (int k = 0; k < 7; k++)
            {
                float t = k / 6f;
                Caja(a, new Vector3(0.08f, Mathf.Lerp(-0.12f, -0.01f, t), Mathf.Lerp(-0.18f, -0.04f, t)), new Vector3(0.05f, 0.055f, 0.045f), Quaternion.Euler(-30f, 0, 0), Laton, 0);
            }
            // asa de carga arriba, empunadura trasera y agarre delantero
            Caja(a, new Vector3(0, 0.15f, -0.02f), new Vector3(0.035f, 0.035f, 0.30f), q, Negro, 0);
            Caja(a, new Vector3(0, 0.115f, 0.12f), new Vector3(0.035f, 0.08f, 0.035f), q, Negro, 0);
            Caja(a, new Vector3(0, 0.115f, -0.14f), new Vector3(0.035f, 0.08f, 0.035f), q, Negro, 0);
            Caja(a, new Vector3(0, -0.15f, -0.43f), new Vector3(0.05f, 0.18f, 0.06f), Quaternion.Euler(-12f, 0, 0), Cuero, 0);
            Caja(a, new Vector3(0, -0.12f, 0.10f), new Vector3(0.05f, 0.14f, 0.06f), q, Cuero, 0);

            var m = new Mesh { name = "Mesh_Minigun" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(a.V); m.SetNormals(a.N); m.SetUVs(0, a.UV); m.SetTriangles(a.T, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            var c = m.bounds.center; // recentrado: el origen queda en el centro de su geometria (igual que WeaponPrefabBuilder)
            var vs = m.vertices; for (int i = 0; i < vs.Length; i++) vs[i] -= c; m.vertices = vs; m.RecalculateBounds();

            Directory.CreateDirectory("Assets/_Project/Resources/Weapons");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(MallaRuta) != null) AssetDatabase.DeleteAsset(MallaRuta);
            AssetDatabase.CreateAsset(m, MallaRuta);
            var go = new GameObject("P_Wpn_Minigun");
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MallaRuta);
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TrimsheetMat);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabRuta);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PesadoArte] minigun lista: {m.vertexCount} vertices, tamaño {m.bounds.size}");
        }

        [MenuItem("Strategic Point/Arte/9. Generar enemigo pesado (malla + minigun)")]
        public static void GenerarTodo()
        {
            SoldierVariantBuilder.GenerarSoloPesado();
            GenerarMinigun();
            AssetDatabase.Refresh();
        }
    }
}
