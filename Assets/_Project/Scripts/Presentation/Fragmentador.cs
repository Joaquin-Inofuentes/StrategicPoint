using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Ronda 12: "quiero fisicas... que las cosas se destruyan de a piezas reales, que se fragmente". Antes un obstaculo que
    // colapsaba soltaba cubitos de 0,2 m sin colision que volaban por una formula propia y desaparecian. Ahora el bloque se
    // PARTE de verdad: se corta su volumen en trozos irregulares (corte recursivo por el eje mas largo), cada trozo es un
    // cuerpo rigido con su colision, masa proporcional a su volumen y velocidad que sale del punto del impacto. Los trozos
    // caen, rebotan contra el suelo y contra los otros, se apilan y se van hundiendo al final de su vida.
    // Los trozos usan el material del propio obstaculo (el del arte si lo tiene) y un mesh de caras rotas (cubo con los
    // vertices corridos), asi no se leen como cajas perfectas. Cupo fijo: el mas viejo se recicla.
    public class Fragmento : MonoBehaviour
    {
        public float Edad, Vida, Escala0;
        public Rigidbody Cuerpo;
        public Vector3 Tam;
        public bool EnUso;

        void Update()
        {
            if (!EnUso) return;
            Edad += Time.deltaTime;
            if (transform.position.y < -30f) { Liberar(); return; }
            float resto = Vida - Edad;
            if (resto <= Fragmentador.SegundosDeHundido)
            {
                float k = Mathf.Clamp01(resto / Fragmentador.SegundosDeHundido);
                transform.localScale = Tam * k;
                if (resto <= 0f) Liberar();
            }
        }

        public void Liberar()
        {
            EnUso = false;
            if (Cuerpo != null) { Cuerpo.linearVelocity = Vector3.zero; Cuerpo.angularVelocity = Vector3.zero; }
            gameObject.SetActive(false);
        }
    }

    public static class Fragmentador
    {
        public const int Cupo = 140;
        public const float SegundosDeHundido = 0.6f;
        public const int LayerSinRayos = 2;   // "Ignore Raycast": los rayos por defecto (apuntar, camara, vision) no los ven

        static readonly List<Fragmento> pool = new List<Fragmento>();
        static Transform root;
        static Mesh[] mallas;
        public static int Lanzados { get; private set; }
        public static int Activos { get { int n = 0; for (int i = 0; i < pool.Count; i++) if (pool[i] != null && pool[i].EnUso) n++; return n; } }
        public static IReadOnlyList<Fragmento> Piezas => pool;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { pool.Clear(); root = null; Lanzados = 0; }

        public static void LimpiarTodo()
        {
            foreach (var f in pool) if (f != null) { if (Application.isPlaying) Object.Destroy(f.gameObject); else Object.DestroyImmediate(f.gameObject); }
            pool.Clear();
            if (root != null) { if (Application.isPlaying) Object.Destroy(root.gameObject); else Object.DestroyImmediate(root.gameObject); root = null; }
        }

        // Cubo unitario de caras planas con cada vertice corrido hasta un 14 % hacia adentro/afuera: 6 variantes.
        static Mesh[] Mallas()
        {
            if (mallas != null && mallas.Length > 0 && mallas[0] != null) return mallas;
            mallas = new Mesh[6];
            var rnd = new System.Random(20240912);
            Vector3[] esquinas = new Vector3[8];
            for (int i = 0; i < 6; i++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var b = new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f);
                    esquinas[c] = b + new Vector3(J(rnd), J(rnd), J(rnd));
                }
                mallas[i] = ConstruirCubo(esquinas);
            }
            return mallas;
        }
        static float J(System.Random r) => ((float)r.NextDouble() * 2f - 1f) * 0.14f;

        static Mesh ConstruirCubo(Vector3[] e)
        {
            // Caras como cuadrilateros (indices de esquinas, sentido antihorario visto desde afuera).
            int[][] caras =
            {
                new[] { 0, 2, 3, 1 }, // -z... el orden se valida abajo con la normal
                new[] { 4, 5, 7, 6 },
                new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 },
                new[] { 0, 4, 6, 2 },
                new[] { 1, 3, 7, 5 },
            };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 centro = Vector3.zero;
            for (int i = 0; i < 8; i++) centro += e[i];
            centro /= 8f;
            foreach (var cara in caras)
            {
                var a = e[cara[0]]; var b = e[cara[1]]; var c = e[cara[2]]; var d = e[cara[3]];
                var n = Vector3.Cross(b - a, c - a);
                bool haciaAfuera = Vector3.Dot(n, (a + b + c + d) * 0.25f - centro) > 0f;
                int i0 = v.Count;
                if (haciaAfuera) { v.Add(a); v.Add(b); v.Add(c); v.Add(d); }
                else { v.Add(a); v.Add(d); v.Add(c); v.Add(b); }
                uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0.5f, 0.5f));
                t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
            }
            var m = new Mesh { name = "FragmentoMalla", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Fragmento Tomar()
        {
            if (root == null)
            {
                pool.Clear();
                root = new GameObject("FragmentosPool") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild }.transform;
            }
            pool.RemoveAll(x => x == null);
            for (int i = 0; i < pool.Count; i++) if (!pool[i].EnUso) return pool[i];
            if (pool.Count >= Cupo)
            {
                Fragmento viejo = pool[0];
                for (int i = 1; i < pool.Count; i++) if (pool[i].Edad > viejo.Edad) viejo = pool[i];
                return viejo;
            }
            var go = new GameObject("Fragmento") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild, layer = LayerSinRayos };
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            var f = go.AddComponent<Fragmento>();
            f.Cuerpo = rb;
            pool.Add(f);
            return f;
        }

        // Caja orientada del objeto: (centro mundial, tamano mundial, rotacion).
        static bool CajaDe(Transform o, out Vector3 centro, out Vector3 tam, out Quaternion rot)
        {
            var bc = o.GetComponent<BoxCollider>();
            if (bc != null)
            {
                centro = o.TransformPoint(bc.center);
                tam = Vector3.Scale(bc.size, o.lossyScale);
                tam = new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z));
                rot = o.rotation;
                return true;
            }
            var rends = o.GetComponentsInChildren<Renderer>(true);
            Bounds b = default; bool hay = false;
            foreach (var r in rends) { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
            centro = b.center; tam = b.size; rot = Quaternion.identity;
            return hay;
        }

        // Colores del arte: los modulos usan una paleta/atlas, asi que un trozo con UV de cubo mostraria pixeles sueltos.
        // Cada trozo toma UN punto de la textura del arte (centro de un triangulo al azar) y lo muestra plano: sale de la paleta real.
        static readonly List<Vector2> puntosUv = new List<Vector2>();
        static readonly MaterialPropertyBlock bloque = new MaterialPropertyBlock();
        static void JuntarUv(Transform o)
        {
            puntosUv.Clear();
            foreach (var mf in o.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.transform == o) continue;
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                var uvs = m.uv; var tri = m.triangles;
                if (uvs == null || uvs.Length == 0 || tri.Length < 3) continue;
                for (int k = 0; k < 40; k++)
                {
                    int t = Random.Range(0, tri.Length / 3) * 3;
                    puntosUv.Add((uvs[tri[t]] + uvs[tri[t + 1]] + uvs[tri[t + 2]]) / 3f);
                }
            }
        }

        // Material que se ve en el objeto: el del arte hijo (con textura) o, si no lo hay, el del propio cubo con su tinte.
        static Material MaterialDe(Transform o, out Color tinte, out bool usarTinte)
        {
            tinte = Color.white; usarTinte = false;
            var rends = o.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in rends)
                if (r.enabled && r.sharedMaterial != null && r.transform != o) return r.sharedMaterial;
            var propio = o.GetComponent<MeshRenderer>();
            if (propio != null && propio.sharedMaterial != null)
            {
                tinte = CubeFxReactor.ReadTint(propio); usarTinte = true;
                return propio.sharedMaterial;
            }
            return SafeMaterial.CreateShared();
        }

        // Parte el objeto en trozos y los lanza. origen = punto del golpe/explosion; fuerza ~ m/s del trozo mas cercano.
        public static int Romper(Transform objeto, Vector3 origen, float fuerza, int piezas = 0)
        {
            if (!Application.isPlaying || objeto == null) return 0;
            if (!CajaDe(objeto, out var centro, out var tam, out var rot)) return 0;
            var material = MaterialDe(objeto, out var tinte, out bool usarTinte);
            var mallasFrag = Mallas();
            JuntarUv(objeto);
            if (piezas <= 0) piezas = Mathf.Clamp(Mathf.RoundToInt(tam.x * tam.y * tam.z * 2.2f) + 8, 9, 22);

            // Corte recursivo en espacio local de la caja: siempre se parte el trozo mas grande por su eje mas largo.
            var cajas = new List<(Vector3 c, Vector3 s)> { (Vector3.zero, tam) };
            int guardia = 0;
            while (cajas.Count < piezas && guardia++ < 200)
            {
                int mayor = 0; float vol = -1f;
                for (int i = 0; i < cajas.Count; i++) { float vv = cajas[i].s.x * cajas[i].s.y * cajas[i].s.z; if (vv > vol) { vol = vv; mayor = i; } }
                var (c, s) = cajas[mayor];
                int eje = s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2);
                float t = Random.Range(0.32f, 0.68f);
                float largo = s[eje];
                if (largo < 0.12f) break;
                var s1 = s; s1[eje] = largo * t;
                var s2 = s; s2[eje] = largo * (1f - t);
                var c1 = c; c1[eje] = c[eje] - largo * 0.5f + s1[eje] * 0.5f;
                var c2 = c; c2[eje] = c[eje] + largo * 0.5f - s2[eje] * 0.5f;
                cajas[mayor] = (c1, s1); cajas.Add((c2, s2));
            }

            int n = 0;
            float diametro = Mathf.Max(tam.x, Mathf.Max(tam.y, tam.z));
            foreach (var (c, s) in cajas)
            {
                var f = Tomar();
                f.EnUso = true; f.Edad = 0f; f.Vida = Random.Range(5.5f, 8f);
                f.gameObject.SetActive(false);
                var pos = centro + rot * c;
                f.transform.SetPositionAndRotation(pos, rot);
                var tamPieza = new Vector3(Mathf.Max(0.05f, s.x * 0.98f), Mathf.Max(0.05f, s.y * 0.98f), Mathf.Max(0.05f, s.z * 0.98f));
                f.Tam = tamPieza;
                f.transform.localScale = tamPieza;
                f.GetComponent<MeshFilter>().sharedMesh = mallasFrag[Random.Range(0, mallasFrag.Length)];
                var mr = f.GetComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                if (usarTinte) CubeFxReactor.WriteTint(mr, tinte);
                else if (puntosUv.Count > 0)
                {
                    var uvp = puntosUv[Random.Range(0, puntosUv.Count)];
                    bloque.Clear();
                    bloque.SetVector("_BaseMap_ST", new Vector4(0f, 0f, uvp.x, uvp.y));
                    bloque.SetVector("_MainTex_ST", new Vector4(0f, 0f, uvp.x, uvp.y));
                    mr.SetPropertyBlock(bloque);
                }
                else mr.SetPropertyBlock(null);
                var bc = f.GetComponent<BoxCollider>();
                bc.size = Vector3.one; bc.center = Vector3.zero;
                var rb = f.Cuerpo;
                rb.mass = Mathf.Clamp(tamPieza.x * tamPieza.y * tamPieza.z * 400f, 2f, 900f);
                rb.linearDamping = 0.05f; rb.angularDamping = 0.25f;
                f.gameObject.SetActive(true);
                var dir = pos - origen; float dist = dir.magnitude;
                dir = dist > 0.01f ? dir / dist : Random.onUnitSphere;
                dir = (dir + Vector3.up * 0.55f + Random.insideUnitSphere * 0.25f).normalized;
                float v = fuerza * Random.Range(0.55f, 1.15f) / (1f + dist * 0.12f);
                rb.linearVelocity = dir * v;
                rb.angularVelocity = Random.insideUnitSphere * (v * 1.6f);
                n++;
            }
            Lanzados += n;

            // Polvo del derrumbe: una nube en el centro y dos voluta en la base.
            float pol = Mathf.Clamp(diametro * 0.9f, 0.8f, 3.5f);
            var polvo = new Color(0.62f, 0.58f, 0.52f, 0.7f);
            SpriteFx.Lanzar("smoke_04", centro, polvo, pol * 0.5f, pol * 1.7f, 1.4f, Random.Range(-25f, 25f), Vector3.up * 0.6f, 0.5f, 1);
            SpriteFx.Lanzar("smoke_02", centro + Vector3.down * tam.y * 0.4f, polvo, pol * 0.4f, pol * 1.4f, 1.1f, Random.Range(-25f, 25f), Vector3.up * 0.3f, 0.5f, 1);
            return n;
        }
    }
}
