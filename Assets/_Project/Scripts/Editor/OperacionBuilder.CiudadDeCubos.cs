using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Operacion;

namespace SP.EditorTools
{
    // Ciudad de cubos que puebla el RESTO del mapa (800 x 800) fuera del recorrido jugable: manzanas de edificios-cubo de alturas y colores
    // variados, calles de 16 m, plazas, parques, estacionamientos y patios de contenedores, con arboles, farolas y autos estacionados, mas gente y
    // autos-cubo que circulan (CivilesDeAmbiente). Es SOLO decorado:
    //   - nada del recorrido jugable se toca: la ocupacion se LEE de la escena ya construida (renderers y transforms reales del cuartel, la muralla,
    //     los puestos, el centro de datos, el patio del tanque, la ruta de la autopista con 32 m de margen desde su eje, la ciudad NE, las entradas, las
    //     reservas, los waypoints y la zona de la cinematica de apertura) y cada lote que cae en zona ocupada se descarta;
    //   - los edificios llevan BoxCollider y el mismo volumen "no caminable" que los demas solidos (CopiarModificadorDeNavegacion), asi la NavMesh y
    //     NavService los tratan como cualquier otro edificio; todo lo demas (calles, ventanas, props) va fusionado en pocas mallas SIN collider y fuera del
    //     horneado de la NavMesh;
    //   - rendimiento: todo Static, sombras apagadas, materiales compartidos (SRP Batcher) y ventanas/props/calles fusionados por region de 200 m
    //     (unas decenas de renderers en vez de miles de objetos); los edificios no entran al minimapa (su camara solo dibuja la capa 8: iconos);
    //   - idempotente: destruye y recrea el grupo "CiudadDeCubos" (Construir lo llama antes de hornear la NavMesh; el menu lo rehace solo).
    public static partial class OperacionBuilder
    {
        public const string NombreCiudadDeCubos = "CiudadDeCubos";
        public static string ResumenCiudadDeCubos { get; private set; } = "";

        // Parametros de la cuadricula: 9 x 9 manzanas de 70 m con calles de 16 m (paso 86 m).
        const int CcN = 9;
        const float CcBloque = 70f, CcCalle = 16f, CcPaso = CcBloque + CcCalle, CcX0 = -378f, CcAreaMitad = 386f;
        const float CcRegion = 200f;

        // ---------------------------------------------------------------
        // Ocupacion (raster de 4 m sobre -400..400)
        // ---------------------------------------------------------------
        sealed class OcupacionCc
        {
            public const float Celda = 4f, Mitad = 400f;
            readonly bool[,] oc; readonly int n;
            public OcupacionCc() { n = (int)(2f * Mitad / Celda); oc = new bool[n, n]; }
            int I(float v) => Mathf.Clamp(Mathf.FloorToInt((v + Mitad) / Celda), 0, n - 1);
            public void Rect(float x0, float z0, float x1, float z1, float margen)
            {
                int a = I(x0 - margen), b = I(x1 + margen), c = I(z0 - margen), d = I(z1 + margen);
                for (int i = a; i <= b; i++) for (int j = c; j <= d; j++) oc[i, j] = true;
            }
            public void Disco(float x, float z, float radio)
            {
                int a = I(x - radio), b = I(x + radio), c = I(z - radio), d = I(z + radio);
                float r2 = (radio + Celda * 0.5f) * (radio + Celda * 0.5f);
                for (int i = a; i <= b; i++) for (int j = c; j <= d; j++)
                {
                    float cx = -Mitad + (i + 0.5f) * Celda - x, cz = -Mitad + (j + 0.5f) * Celda - z;
                    if (cx * cx + cz * cz <= r2) oc[i, j] = true;
                }
            }
            public void Segmento(Vector2 p, Vector2 q, float radio)
            {
                float largo = (q - p).magnitude;
                int pasos = Mathf.Max(1, Mathf.CeilToInt(largo / (Celda * 0.5f)));
                for (int s = 0; s <= pasos; s++) { var pt = Vector2.Lerp(p, q, s / (float)pasos); Disco(pt.x, pt.y, radio); }
            }
            public bool Libre(float x0, float z0, float x1, float z1)
            {
                int a = I(x0), b = I(x1), c = I(z0), d = I(z1);
                for (int i = a; i <= b; i++) for (int j = c; j <= d; j++) if (oc[i, j]) return false;
                return true;
            }
            public int Ocupadas() { int k = 0; foreach (var v in oc) if (v) k++; return k; }
        }

        static OcupacionCc LeerOcupacionCc(Transform op)
        {
            var o = new OcupacionCc();
            // 1) renderers reales del recorrido (cuartel, muralla, puestos, centro de datos, patio, autopista, ciudad NE, helicoptero, reservas...) con 10 m de margen.
            foreach (var r in op.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                if (b.size.x >= 200f || b.size.z >= 200f) continue;     // Fondo_Horizonte, bordes del mapa, muros del valle (se tratan aparte)
                o.Rect(b.min.x, b.min.z, b.max.x, b.max.z, 10f);
            }
            // 2) el valle del cuartel y el patio del tanque como una sola pieza (adentro hay espacios libres entre los edificios): rectangulo de sus muros +28 m.
            var perim = op.Find("0_Perimetro");
            if (perim != null)
            {
                bool hay = false; var bb = new Bounds();
                foreach (var nombre in new[] { "Valle_Oeste", "Valle_Este", "Valle_Sur" })
                {
                    var t = perim.Find(nombre); if (t == null) continue;
                    var r = t.GetComponent<Renderer>(); if (r == null) continue;
                    if (!hay) { bb = r.bounds; hay = true; } else bb.Encapsulate(r.bounds);
                }
                if (hay) o.Rect(bb.min.x, bb.min.z, bb.max.x, bb.max.z, 28f);
            }
            foreach (var nombre in new[] { "4_PatioDelTanque", "3_CentroDeDatos", "2_MurallaDeContencion", "1_Cuartel", "1a_Aproximacion" })
            {
                var t = op.Find(nombre); if (t == null) continue;
                bool hay = false; var bb = new Bounds();
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.bounds.size.x >= 200f || r.bounds.size.z >= 200f) continue;
                    if (!hay) { bb = r.bounds; hay = true; } else bb.Encapsulate(r.bounds);
                }
                if (hay) o.Rect(bb.min.x, bb.min.z, bb.max.x, bb.max.z, 22f);
            }
            // 3) la ciudad NE (casas, cierre, calles): su caja completa +22 m.
            var ciu = op.Find("6_Ciudad");
            if (ciu != null)
            {
                bool hay = false; var bb = new Bounds();
                foreach (var r in ciu.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.bounds.size.x >= 200f || r.bounds.size.z >= 200f) continue;
                    if (!hay) { bb = r.bounds; hay = true; } else bb.Encapsulate(r.bounds);
                }
                if (hay) o.Rect(bb.min.x, bb.min.z, bb.max.x, bb.max.z, 22f);
            }
            // 4) la ruta del tanque: 32 m desde su eje (calzada de 14 m + 25 m de margen).
            var ruta = op.Find("5_Autopista/RutaTanque");
            if (ruta != null)
            {
                var pts = new List<Vector2>();
                var tanque = Object.FindFirstObjectByType<SP.Vehicles.Vehicle>(FindObjectsInactive.Include);
                if (tanque != null) pts.Add(new Vector2(tanque.transform.position.x, tanque.transform.position.z));
                foreach (Transform p in ruta) pts.Add(new Vector2(p.position.x, p.position.z));
                for (int i = 0; i + 1 < pts.Count; i++) o.Segmento(pts[i], pts[i + 1], 32f);
            }
            // 5) puntos reales: entradas, anclas, reservas, waypoints, enemigos, vehiculos, helicoptero (20 m).
            void Puntos(Transform t, float radio) { if (t == null) return; foreach (var x in t.GetComponentsInChildren<Transform>(true)) o.Disco(x.position.x, x.position.z, radio); }
            foreach (var nombre in new[] { "Entradas_Fase", "Anclas_Zona", "Reservas", "Heli_Inicio", "Helicoptero_Extraccion" }) Puntos(op.Find(nombre), 20f);
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == "Enemies" || g.name == "Waypoints" || g.name == "Vehicles" || g.name == "PlayerSquad") Puntos(g.transform, 20f);
            // 6) la zona de la cinematica de apertura (el helicoptero llega por el NE/NO a unos 170 m de la escuadra, a 12-15 m de altura): disco de 135 m alrededor del inicio.
            var e0 = op.Find("Entradas_Fase/Entrada_Infiltrar");
            var ini = e0 != null ? e0.position : new Vector3(0f, 0f, -373f);
            o.Disco(ini.x, ini.z, 135f);
            // ...y el corredor por donde llega volando (rumbo E-O sobre la zona de inicio, hasta ~170 m a cada lado): nada alto ahi.
            o.Rect(ini.x - 200f, ini.z - 60f, ini.x + 200f, ini.z + 75f, 0f);
            return o;
        }

        // ---------------------------------------------------------------
        // Mallas fusionadas por region y material
        // ---------------------------------------------------------------
        sealed class MallaCc
        {
            public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector3> n = new List<Vector3>(); public readonly List<int> t = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d); n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
                // Unity: frente = sentido horario; normal = cross(b - a, c - a).
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
                else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            }
            // Rectangulo horizontal (x, z centro; ancho x, largo z; yaw).
            public void Plano(float x, float y, float z, float w, float d, float yaw)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 P(float dx, float dz) => new Vector3(x, y, z) + q * new Vector3(dx, 0f, dz);
                Quad(P(-w / 2, -d / 2), P(-w / 2, d / 2), P(w / 2, d / 2), P(w / 2, -d / 2), Vector3.up);
            }
            // Caja sin la cara de abajo (base en y0).
            public void Caja(float x, float y0, float z, float sx, float sy, float sz, float yaw)
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 P(float dx, float dy, float dz) => new Vector3(x, y0, z) + q * new Vector3(dx, dy, dz);
                float hx = sx / 2, hz = sz / 2;
                Quad(P(-hx, sy, -hz), P(-hx, sy, hz), P(hx, sy, hz), P(hx, sy, -hz), q * Vector3.up);
                Quad(P(-hx, 0, hz), P(hx, 0, hz), P(hx, sy, hz), P(-hx, sy, hz), q * Vector3.forward);
                Quad(P(hx, 0, -hz), P(-hx, 0, -hz), P(-hx, sy, -hz), P(hx, sy, -hz), q * Vector3.back);
                Quad(P(hx, 0, hz), P(hx, 0, -hz), P(hx, sy, -hz), P(hx, sy, hz), q * Vector3.right);
                Quad(P(-hx, 0, -hz), P(-hx, 0, hz), P(-hx, sy, hz), P(-hx, sy, -hz), q * Vector3.left);
            }
            public Mesh Construir(string nombre)
            {
                var m = new Mesh { name = nombre };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        sealed class RegionesCc
        {
            public readonly Dictionary<(int, int, string), MallaCc> mallas = new Dictionary<(int, int, string), MallaCc>();
            public MallaCc De(float x, float z, string mat)
            {
                var k = ((int)Mathf.Floor((x + 400f) / CcRegion), (int)Mathf.Floor((z + 400f) / CcRegion), mat);
                if (!mallas.TryGetValue(k, out var m)) { m = new MallaCc(); mallas[k] = m; }
                return m;
            }
        }

        static readonly Dictionary<string, Material> ccMats = new Dictionary<string, Material>();
        static Material MatCc(string clave)
        {
            if (ccMats.TryGetValue(clave, out var m) && m != null) return m;
            switch (clave)
            {
                case "Acera": m = Mat("CC_Acera", new Color(0.40f, 0.41f, 0.43f)); break;
                case "Calle": m = Mat("CC_Calle", new Color(0.17f, 0.18f, 0.20f)); break;
                case "Linea": m = Mat("CC_Linea", new Color(0.85f, 0.70f, 0.15f), 0.25f); break;
                case "Cebra": m = Mat("CC_Cebra", new Color(0.82f, 0.82f, 0.80f)); break;
                case "Plaza": m = Mat("CC_Plaza", new Color(0.55f, 0.55f, 0.52f)); break;
                case "Parque": m = Mat("CC_Parque", new Color(0.24f, 0.38f, 0.20f)); break;
                case "Estac": m = Mat("CC_Estac", new Color(0.24f, 0.25f, 0.27f)); break;
                case "VentanaCalida": m = Mat("CC_VentanaCalida", new Color(1f, 0.74f, 0.38f), 2.0f); break;
                case "VentanaFria": m = Mat("CC_VentanaFria", new Color(0.55f, 0.82f, 1f), 1.7f); break;
                case "VentanaOscura": m = Mat("CC_VentanaOscura", new Color(0.05f, 0.07f, 0.10f)); break;
                case "Tronco": m = Mat("CC_Tronco", new Color(0.30f, 0.20f, 0.12f)); break;
                case "CopaA": m = Mat("CC_CopaA", new Color(0.16f, 0.36f, 0.16f)); break;
                case "CopaB": m = Mat("CC_CopaB", new Color(0.26f, 0.42f, 0.18f)); break;
                case "Poste": m = Mat("CC_Poste", new Color(0.30f, 0.31f, 0.34f)); break;
                case "Farola": m = Mat("CC_Farola", new Color(1f, 0.88f, 0.6f), 2.4f); break;
                case "TechoDet": m = Mat("CC_TechoDet", new Color(0.30f, 0.31f, 0.33f)); break;
                case "Banco": m = Mat("CC_Banco", new Color(0.35f, 0.25f, 0.16f)); break;
                case "Fuente": m = Mat("CC_Fuente", new Color(0.20f, 0.45f, 0.75f), 0.6f); break;
                case "AutoCabina": m = Mat("CC_AutoCabina", new Color(0.10f, 0.12f, 0.15f)); break;
                default:
                    if (clave.StartsWith("Edif")) { m = Mat("CC_" + clave, PaletaEdificio[int.Parse(clave.Substring(4))]); break; }
                    if (clave.StartsWith("Auto")) { m = Mat("CC_" + clave, PaletaAuto[int.Parse(clave.Substring(4))]); break; }
                    if (clave.StartsWith("Cont")) { m = Mat("CC_" + clave, PaletaContenedor[int.Parse(clave.Substring(4))]); break; }
                    if (clave.StartsWith("Camisa")) { m = Mat("CC_" + clave, PaletaCamisa[int.Parse(clave.Substring(6))]); break; }
                    m = Mat("CC_" + clave, Color.magenta); break;
            }
            ccMats[clave] = m;
            return m;
        }

        static readonly Color[] PaletaEdificio =
        {
            new Color(0.34f, 0.38f, 0.47f), new Color(0.62f, 0.56f, 0.46f), new Color(0.52f, 0.27f, 0.23f), new Color(0.20f, 0.42f, 0.45f),
            new Color(0.70f, 0.58f, 0.26f), new Color(0.68f, 0.68f, 0.70f), new Color(0.22f, 0.25f, 0.31f), new Color(0.38f, 0.42f, 0.28f),
            new Color(0.74f, 0.50f, 0.44f), new Color(0.45f, 0.36f, 0.55f), new Color(0.80f, 0.78f, 0.70f), new Color(0.30f, 0.48f, 0.60f),
        };
        static readonly Color[] PaletaAuto =
        {
            new Color(0.75f, 0.12f, 0.10f), new Color(0.12f, 0.30f, 0.70f), new Color(0.88f, 0.88f, 0.86f), new Color(0.10f, 0.10f, 0.12f),
        };
        static readonly Color[] PaletaContenedor =
        {
            new Color(0.70f, 0.22f, 0.15f), new Color(0.15f, 0.35f, 0.62f), new Color(0.80f, 0.55f, 0.12f),
        };
        static readonly Color[] PaletaCamisa =
        {
            new Color(0.80f, 0.20f, 0.18f), new Color(0.20f, 0.45f, 0.80f), new Color(0.88f, 0.78f, 0.20f), new Color(0.25f, 0.65f, 0.35f),
            new Color(0.85f, 0.85f, 0.85f), new Color(0.55f, 0.30f, 0.65f), new Color(0.90f, 0.50f, 0.15f),
        };

        // ---------------------------------------------------------------
        // Entrada
        // ---------------------------------------------------------------
        [MenuItem("Strategic Point/Operacion/Ciudad de cubos (rehacer solo este grupo y la NavMesh)")]
        public static void ReconstruirCiudadDeCubos()
        {
            var op = GameObject.Find("Operacion");
            if (op == null) { Debug.LogError("[Operacion] No hay 'Operacion' en la escena abierta."); return; }
            mats.Clear();
            CiudadDeCubos(op.transform);
            LevelBlockoutBuilder.BakeNavMesh();
            EditorSceneManager.MarkSceneDirty(op.scene);
            Debug.Log("[Operacion] " + ResumenCiudadDeCubos);
        }

        public static void CiudadDeCubos(Transform padre)
        {
            ccMats.Clear();
            // Idempotente: se destruye el grupo viejo (y sus mallas propias) antes de leer la ocupacion.
            var vieja = padre.Find(NombreCiudadDeCubos);
            if (vieja != null)
            {
                var mallasViejas = new HashSet<Mesh>();
                foreach (var mf in vieja.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh) && mf.sharedMesh.name.StartsWith("CC_")) mallasViejas.Add(mf.sharedMesh);
                Object.DestroyImmediate(vieja.gameObject);
                foreach (var m in mallasViejas) Object.DestroyImmediate(m);
            }
            var ocup = LeerOcupacionCc(padre);
            var usada = new OcupacionCc();      // lo que esta ciudad ya construyo (calles y lotes): las arboledas van en lo que queda libre
            var g = Grupo(padre, NombreCiudadDeCubos);
            var rnd = new System.Random(2026);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            int RI(int a, int b) => rnd.Next(a, b);
            var regiones = new RegionesCc();
            var gEdif = Grupo(g, "Edificios");
            var gDecor = Grupo(g, "Decorado");                      // mallas fusionadas: fuera del horneado de la NavMesh
            var modDecor = gDecor.gameObject.AddComponent<NavMeshModifier>(); modDecor.ignoreFromBuild = true; modDecor.applyToChildren = true;
            var gGente = Grupo(g, "Gente");
            var modGente = gGente.gameObject.AddComponent<NavMeshModifier>(); modGente.ignoreFromBuild = true; modGente.applyToChildren = true;

            int edificios = 0, plazas = 0, parques = 0, estac = 0, patios = 0, arboles = 0, farolas = 0, autosEst = 0, ventanas = 0, tramosCalle = 0, tris = 0, lotesDescartados = 0;
            var rutas = new List<CivilesDeAmbiente.Ruta>();
            var figuras = new List<CivilesDeAmbiente.Figura>();

            // Posiciones de las manzanas y de las calles.
            float Bx(int i) => CcX0 + i * CcPaso;      // borde minimo del bloque i
            var libres = new bool[CcN, CcN];
            var calleH = new bool[CcN, CcN - 1];       // calle entre las filas j y j+1, a lo largo de la columna i
            var calleV = new bool[CcN - 1, CcN];       // calle entre las columnas i e i+1, a lo largo de la fila j

            // ---- calles ----
            for (int i = 0; i < CcN; i++) for (int j = 0; j < CcN - 1; j++)
            {
                float zc = Bx(j) + CcBloque + CcCalle * 0.5f;
                float xa = Bx(i) - CcCalle * 0.5f, xb = Bx(i) + CcBloque + CcCalle * 0.5f;
                if (i == 0) xa = Bx(i); if (i == CcN - 1) xb = Bx(i) + CcBloque;
                if (!ocup.Libre(xa, zc - CcCalle * 0.5f, xb, zc + CcCalle * 0.5f)) continue;
                calleH[i, j] = true; tramosCalle++; usada.Rect(xa, zc - CcCalle * 0.5f, xb, zc + CcCalle * 0.5f, 0f);
                var mc = regiones.De((xa + xb) * 0.5f, zc, "Calle"); mc.Plano((xa + xb) * 0.5f, 0.04f, zc, xb - xa, CcCalle, 0f); tris += 2;
                var ml = regiones.De((xa + xb) * 0.5f, zc, "Linea");
                for (float x = xa + 6f; x < xb - 6f; x += 9f) { ml.Plano(x, 0.10f, zc, 4f, 0.3f, 0f); tris += 2; }
            }
            for (int i = 0; i < CcN - 1; i++) for (int j = 0; j < CcN; j++)
            {
                float xc = Bx(i) + CcBloque + CcCalle * 0.5f;
                float za = Bx(j) - CcCalle * 0.5f, zb = Bx(j) + CcBloque + CcCalle * 0.5f;
                if (j == 0) za = Bx(j); if (j == CcN - 1) zb = Bx(j) + CcBloque;
                if (!ocup.Libre(xc - CcCalle * 0.5f, za, xc + CcCalle * 0.5f, zb)) continue;
                calleV[i, j] = true; tramosCalle++; usada.Rect(xc - CcCalle * 0.5f, za, xc + CcCalle * 0.5f, zb, 0f);
                var mc = regiones.De(xc, (za + zb) * 0.5f, "Calle"); mc.Plano(xc, 0.04f, (za + zb) * 0.5f, CcCalle, zb - za, 0f); tris += 2;
                var ml = regiones.De(xc, (za + zb) * 0.5f, "Linea");
                for (float z = za + 6f; z < zb - 6f; z += 9f) { ml.Plano(xc, 0.10f, z, 0.3f, 4f, 0f); tris += 2; }
            }

            // ---- manzanas ----
            for (int bi = 0; bi < CcN; bi++) for (int bj = 0; bj < CcN; bj++)
            {
                float x0 = Bx(bi), z0 = Bx(bj), x1 = x0 + CcBloque, z1 = z0 + CcBloque;
                if (x1 > CcAreaMitad || z1 > CcAreaMitad || x0 < -CcAreaMitad || z0 < -CcAreaMitad) continue;
                // Lotes de la manzana (subdivision recursiva).
                var lotes = new List<Vector4>();   // x0, z0, x1, z1
                void Dividir(float a0, float b0, float a1, float b1)
                {
                    float w = a1 - a0, d = b1 - b0;
                    if ((w <= 36f && d <= 36f && rnd.NextDouble() < 0.8) || w < 16f || d < 16f) { lotes.Add(new Vector4(a0, b0, a1, b1)); return; }
                    if (w >= d) { float c = a0 + w * R(0.38f, 0.62f); Dividir(a0, b0, c, b1); Dividir(c, b0, a1, b1); }
                    else { float c = b0 + d * R(0.38f, 0.62f); Dividir(a0, b0, a1, c); Dividir(a0, c, a1, b1); }
                }
                // Acera perimetral de 3 m: los edificios arrancan adentro de ella.
                bool bloqueLibre = ocup.Libre(x0, z0, x1, z1);
                float acera = 3f;
                Dividir(x0 + acera, z0 + acera, x1 - acera, z1 - acera);
                bool algo = false;
                foreach (var l in lotes)
                {
                    if (!ocup.Libre(l.x, l.y, l.z, l.w)) { lotesDescartados++; continue; }
                    algo = true; usada.Rect(l.x, l.y, l.z, l.w, 0f);
                    float cx = (l.x + l.z) * 0.5f, cz = (l.y + l.w) * 0.5f, w = l.z - l.x, d = l.w - l.y;
                    double tipo = rnd.NextDouble();
                    if (tipo < 0.08)            // plaza
                    {
                        plazas++;
                        var mp = regiones.De(cx, cz, "Plaza"); mp.Plano(cx, 0.05f, cz, w, d, 0f); tris += 2;
                        var mf = regiones.De(cx, cz, "VentanaFria"); mf.Caja(cx, 0.05f, cz, 3.2f, 0.7f, 3.2f, 0f); tris += 10;
                        var mpo = regiones.De(cx, cz, "Poste"); mpo.Caja(cx, 0.05f, cz, 1.6f, 1.1f, 1.6f, 0f); tris += 10;
                        int nb = RI(2, 5); var mb = regiones.De(cx, cz, "Tronco");
                        for (int k = 0; k < nb; k++) { float a = k * Mathf.PI * 2f / nb; mb.Caja(cx + Mathf.Cos(a) * Mathf.Min(w, d) * 0.28f, 0.05f, cz + Mathf.Sin(a) * Mathf.Min(w, d) * 0.28f, 2.2f, 0.6f, 0.7f, -a * Mathf.Rad2Deg + 90f); tris += 10; }
                        int nt = RI(3, 7);
                        for (int k = 0; k < nt; k++) { Arbol(regiones, rnd, l.x + R(2f, w - 2f), l.y + R(2f, d - 2f)); arboles++; tris += 20; }
                        RutaPeatonal(rutas, figuras, rnd, gGente, new Vector3(cx - w * 0.3f, 0.05f, cz), new Vector3(cx + w * 0.3f, 0.05f, cz), 1);
                    }
                    else if (tipo < 0.14)       // parque
                    {
                        parques++;
                        var mp = regiones.De(cx, cz, "Parque"); mp.Plano(cx, 0.05f, cz, w, d, 0f); tris += 2;
                        int nt = Mathf.Clamp((int)(w * d / 90f), 4, 12);
                        for (int k = 0; k < nt; k++) { Arbol(regiones, rnd, l.x + R(2f, w - 2f), l.y + R(2f, d - 2f)); arboles++; tris += 20; }
                        RutaPeatonal(rutas, figuras, rnd, gGente, new Vector3(l.x + 3f, 0.05f, cz), new Vector3(l.z - 3f, 0.05f, cz + R(-4f, 4f)), 1);
                    }
                    else if (tipo < 0.19)       // estacionamiento
                    {
                        estac++;
                        var mp = regiones.De(cx, cz, "Estac"); mp.Plano(cx, 0.05f, cz, w, d, 0f); tris += 2;
                        var ml = regiones.De(cx, cz, "Cebra");
                        for (float x = l.x + 3f; x < l.z - 2f; x += 3.4f)
                            for (float z = l.y + 5f; z < l.w - 5f; z += 8.5f)
                            {
                                ml.Plano(x, 0.07f, z, 0.15f, 5f, 0f); tris += 2;
                                if (rnd.NextDouble() < 0.5) { AutoEstacionado(regiones, rnd, x + 1.7f, z, 0f); autosEst++; tris += 20; }
                            }
                    }
                    else if (tipo < 0.23)       // contenedores
                    {
                        patios++;
                        var mp = regiones.De(cx, cz, "Estac"); mp.Plano(cx, 0.05f, cz, w, d, 0f); tris += 2;
                        for (float x = l.x + 4f; x < l.z - 6f; x += 7f)
                            for (float z = l.y + 3f; z < l.w - 3f; z += 3.2f)
                            {
                                if (rnd.NextDouble() < 0.35) continue;
                                int pila = RI(1, 4);
                                for (int p = 0; p < pila; p++) { var mcn = regiones.De(x, z, "Cont" + RI(0, PaletaContenedor.Length)); mcn.Caja(x, 0.05f + p * 2.6f, z, 6.0f, 2.5f, 2.4f, 0f); tris += 10; }
                            }
                    }
                    else                        // edificio
                    {
                        // Altura: base baja + realce hacia dos "centros" de la ciudad.
                        float d1 = Mathf.Sqrt((cx + 215f) * (cx + 215f) + (cz - 175f) * (cz - 175f));
                        float d2 = Mathf.Sqrt((cx - 200f) * (cx - 200f) + (cz + 190f) * (cz + 190f));
                        float k = Mathf.Max(Mathf.Exp(-(d1 / 150f) * (d1 / 150f)), Mathf.Exp(-(d2 / 140f) * (d2 / 140f)));
                        float h = R(8f, 17f) + k * R(10f, 42f) + (rnd.NextDouble() < 0.1 ? R(3f, 10f) : 0f);
                        h = Mathf.Min(h, 58f);
                        float inset = R(0.2f, 2.2f);
                        float bw = Mathf.Max(9f, w - inset - R(0.2f, 2.2f)), bd = Mathf.Max(9f, d - inset - R(0.2f, 2.2f));
                        float bx = cx + R(-0.5f, 0.5f), bz = cz + R(-0.5f, 0.5f);
                        var pal = RI(0, PaletaEdificio.Length);
                        var go = Solido(gEdif, "Edif_" + (++edificios), bx, bz, bw, h, bd, MatCc("Edif" + pal));
                        CopiarModificadorDeNavegacion(go);
                        PrepararDecorado(go, true);
                        tris += 12;
                        // Remate escalonado en los altos.
                        if (h > 24f && rnd.NextDouble() < 0.5)
                        {
                            float th = R(5f, 12f);
                            var top = Cubo(gEdif, "Edif_" + edificios + "_Remate", bx, bz, bw * R(0.5f, 0.7f), th, bd * R(0.5f, 0.7f), MatCc("Edif" + RI(0, PaletaEdificio.Length)), 0f, h, false);
                            PrepararDecorado(top, false); tris += 12;
                        }
                        // Detalles del techo (unidades de aire, tanque).
                        var mt = regiones.De(bx, bz, "Poste");
                        int ndet = RI(1, 3);
                        for (int q = 0; q < ndet; q++) { mt.Caja(bx + R(-bw * 0.35f, bw * 0.35f), h, bz + R(-bd * 0.35f, bd * 0.35f), R(1.5f, 3.2f), R(0.8f, 1.8f), R(1.5f, 3.2f), R(0f, 90f)); tris += 10; }
                        ventanas += Ventanas(regiones, rnd, bx, bz, bw, bd, h, ref tris);
                    }
                }
                // Arboles y farolas sobre la acera perimetral; autos estacionados contra el cordon.
                if (algo || bloqueLibre)
                {
                    for (int lado = 0; lado < 4; lado++)
                    {
                        float len = CcBloque;
                        for (float s = 6f; s < len - 5f; s += 14f)
                        {
                            float px, pz;
                            switch (lado)
                            {
                                case 0: px = x0 + s; pz = z0 + 2.3f; break;
                                case 1: px = x0 + s; pz = z1 - 2.3f; break;
                                case 2: px = x0 + 2.3f; pz = z0 + s; break;
                                default: px = x1 - 2.3f; pz = z0 + s; break;
                            }
                            if (!ocup.Libre(px - 1f, pz - 1f, px + 1f, pz + 1f)) continue;
                            double q = rnd.NextDouble();
                            if (q < 0.14) { Arbol(regiones, rnd, px, pz); arboles++; tris += 20; }
                            else if (q < 0.27) { Farola(regiones, px, pz); farolas++; tris += 20; }
                        }
                    }
                }
            }

            // ---- autos estacionados contra el cordon de las calles pintadas y peatones sobre las veredas ----
            for (int i = 0; i < CcN; i++) for (int j = 0; j < CcN - 1; j++) if (calleH[i, j])
            {
                float zc = Bx(j) + CcBloque + CcCalle * 0.5f; float xa = Bx(i) + 2f, xb = Bx(i) + CcBloque - 2f;
                for (float x = xa + 5f; x < xb - 5f; x += 7.5f) for (int lado = -1; lado <= 1; lado += 2)
                    if (rnd.NextDouble() < 0.10) { AutoEstacionado(regiones, rnd, x, zc + lado * 5.4f, 90f); autosEst++; tris += 20; }
                RutaPeatonal(rutas, figuras, rnd, gGente, new Vector3(xa, 0.05f, zc + 8.7f), new Vector3(xb, 0.05f, zc + 8.7f), RI(0, 2));
                RutaPeatonal(rutas, figuras, rnd, gGente, new Vector3(xa, 0.05f, zc - 8.7f), new Vector3(xb, 0.05f, zc - 8.7f), RI(0, 2));
            }
            for (int i = 0; i < CcN - 1; i++) for (int j = 0; j < CcN; j++) if (calleV[i, j])
            {
                float xc = Bx(i) + CcBloque + CcCalle * 0.5f; float za = Bx(j) + 2f, zb = Bx(j) + CcBloque - 2f;
                for (float z = za + 5f; z < zb - 5f; z += 7.5f) for (int lado = -1; lado <= 1; lado += 2)
                    if (rnd.NextDouble() < 0.10) { AutoEstacionado(regiones, rnd, xc + lado * 5.4f, z, 0f); autosEst++; tris += 20; }
                RutaPeatonal(rutas, figuras, rnd, gGente, new Vector3(xc + 8.7f, 0.05f, za), new Vector3(xc + 8.7f, 0.05f, zb), RI(0, 2));
                RutaPeatonal(rutas, figuras, rnd, gGente, new Vector3(xc - 8.7f, 0.05f, za), new Vector3(xc - 8.7f, 0.05f, zb), RI(0, 2));
            }

            // ---- autos que circulan: una vuelta por manzana cuyas 4 calles estan pintadas ----
            int autosMoviles = 0;
            for (int bi = 1; bi < CcN - 1 && autosMoviles < 24; bi++) for (int bj = 1; bj < CcN - 1 && autosMoviles < 24; bj++)
            {
                if (!(calleH[bi, bj - 1] && calleH[bi, bj] && calleV[bi - 1, bj] && calleV[bi, bj])) continue;
                if (rnd.NextDouble() < 0.35) continue;
                float xl = Bx(bi) - 5f, xr = Bx(bi) + CcBloque + 5f, zb = Bx(bj) - 5f, zt = Bx(bj) + CcBloque + 5f;
                const float c = 6f;
                var puntos = new[]
                {
                    new Vector3(xl + c, 0.06f, zb), new Vector3(xr - c, 0.06f, zb), new Vector3(xr, 0.06f, zb + c), new Vector3(xr, 0.06f, zt - c),
                    new Vector3(xr - c, 0.06f, zt), new Vector3(xl + c, 0.06f, zt), new Vector3(xl, 0.06f, zt - c), new Vector3(xl, 0.06f, zb + c),
                };
                rutas.Add(new CivilesDeAmbiente.Ruta { puntos = puntos, circuito = true });
                int ri = rutas.Count - 1;
                int cuantos = RI(1, 3);
                for (int q = 0; q < cuantos; q++)
                {
                    var auto = NuevoAuto(gGente, RI(0, PaletaAuto.Length));
                    figuras.Add(new CivilesDeAmbiente.Figura { t = auto.transform, ruta = ri, distancia = q * 160f + R(0f, 40f), velocidad = R(6.5f, 10f), auto = true });
                    autosMoviles++;
                }
            }

            // ---- arboledas en los claros que quedan (campo abierto entre la ciudad y el recorrido) ----
            int arboledas = 0;
            for (int intento = 0; intento < 900 && arboledas < 70; intento++)
            {
                float ax = R(-370f, 370f), az = R(-370f, 370f);
                if (!ocup.Libre(ax - 8f, az - 8f, ax + 8f, az + 8f) || !usada.Libre(ax - 8f, az - 8f, ax + 8f, az + 8f)) continue;
                int nt = RI(3, 6);
                for (int q = 0; q < nt; q++) { Arbol(regiones, rnd, ax + R(-6f, 6f), az + R(-6f, 6f)); arboles++; tris += 20; }
                usada.Disco(ax, az, 14f); arboledas++;
            }

            // ---- instanciar las mallas fusionadas (una por region y material) ----
            int renderersFusionados = 0;
            foreach (var kv in regiones.mallas)
            {
                var (rx, rz, mat) = kv.Key;
                var go = new GameObject($"CC_r{rx}_{rz}_{mat}");
                go.transform.SetParent(gDecor, false);
                var mesh = kv.Value.Construir($"CC_{rx}_{rz}_{mat}");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MatCc(mat);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                PrepararDecorado(go, false);
                renderersFusionados++;
            }

            // ---- gente y autos que se mueven ----
            var gestor = new GameObject("CivilesDeAmbiente");
            gestor.transform.SetParent(g, false);
            var ci = gestor.AddComponent<CivilesDeAmbiente>();
            ci.rutas = rutas.ToArray(); ci.figuras = figuras.ToArray(); ci.maxActivos = 60; ci.radioDeActividad = 150f;
            ci.ColocarInicial();
            EditorUtility.SetDirty(ci);
            int civiles = 0, autosM = 0; foreach (var f in figuras) { if (f.auto) autosM++; else civiles++; }

            EdificiosDeCubos = edificios;
            ResumenCiudadDeCubos = $"[CiudadDeCubos] {edificios} edificios, {plazas} plazas, {parques} parques, {estac} estacionamientos, {patios} patios de contenedores, {tramosCalle} tramos de calle, {arboles} arboles, {farolas} farolas, {autosEst} autos estacionados, {ventanas} ventanas, {civiles} civiles y {autosM} autos en circulacion (max 60 activos); {renderersFusionados} mallas fusionadas, ~{tris} triangulos en total, {lotesDescartados} lotes descartados por zona ocupada, {ocup.Ocupadas()} celdas ocupadas.";
            return;
        }

        public static int EdificiosDeCubos { get; private set; }

        // Todo Static y sin sombras; los edificios con collider conservan el suyo.
        static void PrepararDecorado(GameObject go, bool edificio)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (!edificio) mr.receiveShadows = false; }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        static void Arbol(RegionesCc reg, System.Random rnd, float x, float z)
        {
            float h = 2.0f + (float)rnd.NextDouble() * 1.4f, s = 2.2f + (float)rnd.NextDouble() * 1.6f;
            reg.De(x, z, "Tronco").Caja(x, 0.05f, z, 0.45f, h, 0.45f, 0f);
            reg.De(x, z, rnd.NextDouble() < 0.5 ? "CopaA" : "CopaB").Caja(x, 0.05f + h - 0.4f, z, s, s, s, (float)rnd.NextDouble() * 90f);
        }

        static void Farola(RegionesCc reg, float x, float z)
        {
            reg.De(x, z, "Poste").Caja(x, 0.05f, z, 0.22f, 6.2f, 0.22f, 0f);
            reg.De(x, z, "Farola").Caja(x, 6.1f, z, 0.9f, 0.25f, 0.5f, 0f);
        }

        static void AutoEstacionado(RegionesCc reg, System.Random rnd, float x, float z, float yaw)
        {
            reg.De(x, z, "Auto" + rnd.Next(0, PaletaAuto.Length)).Caja(x, 0.25f, z, 1.9f, 0.8f, 4.2f, yaw + (rnd.NextDouble() < 0.5 ? 0f : 180f));
            reg.De(x, z, "AutoCabina").Caja(x, 1.05f, z - 0.1f * Mathf.Cos(yaw * Mathf.Deg2Rad), 1.7f, 0.7f, 2.1f, yaw);
        }

        // Ventanas: cuadros finos pegados a las cuatro caras (encendidas calidas/frias y apagadas), en mallas fusionadas.
        static int Ventanas(RegionesCc reg, System.Random rnd, float cx, float cz, float w, float d, float h, ref int tris)
        {
            int total = 0;
            var calida = reg.De(cx, cz, "VentanaCalida"); var fria = reg.De(cx, cz, "VentanaFria"); var oscura = reg.De(cx, cz, "VentanaOscura");
            for (int cara = 0; cara < 4; cara++)
            {
                Vector3 n, centro; float largo;
                switch (cara)
                {
                    case 0: n = Vector3.forward; centro = new Vector3(cx, 0f, cz + d * 0.5f + 0.04f); largo = w; break;
                    case 1: n = Vector3.back; centro = new Vector3(cx, 0f, cz - d * 0.5f - 0.04f); largo = w; break;
                    case 2: n = Vector3.right; centro = new Vector3(cx + w * 0.5f + 0.04f, 0f, cz); largo = d; break;
                    default: n = Vector3.left; centro = new Vector3(cx - w * 0.5f - 0.04f, 0f, cz); largo = d; break;
                }
                var der = Vector3.Cross(Vector3.up, -n);     // derecha vista desde afuera
                int cols = Mathf.FloorToInt((largo - 2.6f) / 3.6f), pisos = Mathf.FloorToInt((h - 2.2f) / 3.4f);
                if (cols < 1 || pisos < 1) continue;
                int celdas = cols * pisos;
                int nEnc = Mathf.Min(celdas, 5), nApag = Mathf.Min(celdas, 2);
                var usadas = new HashSet<int>();
                for (int q = 0; q < nEnc + nApag; q++)
                {
                    int c = rnd.Next(celdas);
                    if (!usadas.Add(c)) continue;
                    int col = c % cols, piso = c / cols;
                    float u = (col - (cols - 1) * 0.5f) * 3.6f, y = 2.6f + piso * 3.4f;
                    var p = centro + der * u + Vector3.up * y;
                    var malla = q < nEnc ? (rnd.NextDouble() < 0.7 ? calida : fria) : oscura;
                    float hw = 1.1f, hh = 0.75f;
                    malla.Quad(p - der * hw - Vector3.up * hh, p - der * hw + Vector3.up * hh, p + der * hw + Vector3.up * hh, p + der * hw - Vector3.up * hh, n);
                    tris += 2; total++;
                }
            }
            return total;
        }

        // ---------------------------------------------------------------
        // Gente
        // ---------------------------------------------------------------
        static readonly Dictionary<int, Mesh> mallasDeCivil = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> mallasDeAuto = new Dictionary<int, Mesh>();

        static Mesh MallaDeCivil(int camisa)
        {
            if (mallasDeCivil.TryGetValue(camisa, out var m) && m != null) return m;
            // 3 submallas: camisa, pantalon, piel.
            var cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var ci = new CombineInstance[3];
            ci[0] = new CombineInstance { mesh = cubo, subMeshIndex = 0, transform = Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.identity, new Vector3(0.5f, 0.7f, 0.3f)) };
            ci[1] = new CombineInstance { mesh = cubo, subMeshIndex = 0, transform = Matrix4x4.TRS(new Vector3(0f, 0.4f, 0f), Quaternion.identity, new Vector3(0.4f, 0.8f, 0.26f)) };
            ci[2] = new CombineInstance { mesh = cubo, subMeshIndex = 0, transform = Matrix4x4.TRS(new Vector3(0f, 1.67f, 0f), Quaternion.identity, new Vector3(0.28f, 0.28f, 0.28f)) };
            m = new Mesh { name = "CC_Civil" + camisa };
            m.CombineMeshes(ci, false, true);
            mallasDeCivil[camisa] = m;
            return m;
        }

        static Mesh MallaDeAuto(int color)
        {
            if (mallasDeAuto.TryGetValue(color, out var m) && m != null) return m;
            var cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var ci = new CombineInstance[2];
            ci[0] = new CombineInstance { mesh = cubo, subMeshIndex = 0, transform = Matrix4x4.TRS(new Vector3(0f, 0.65f, 0f), Quaternion.identity, new Vector3(1.9f, 0.8f, 4.2f)) };
            ci[1] = new CombineInstance { mesh = cubo, subMeshIndex = 0, transform = Matrix4x4.TRS(new Vector3(0f, 1.35f, -0.1f), Quaternion.identity, new Vector3(1.7f, 0.7f, 2.1f)) };
            m = new Mesh { name = "CC_Auto" + color };
            m.CombineMeshes(ci, false, true);
            mallasDeAuto[color] = m;
            return m;
        }

        static GameObject NuevoAuto(Transform padre, int color)
        {
            var go = new GameObject("AutoCubo");
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = MallaDeAuto(color);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { MatCc("Auto" + color), MatCc("AutoCabina") };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        static GameObject NuevoCivil(Transform padre, int camisa)
        {
            var go = new GameObject("CivilCubo");
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = MallaDeCivil(camisa);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { MatCc("Camisa" + camisa), MatCc("AutoCabina"), MatCc("Tronco") };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        static void RutaPeatonal(List<CivilesDeAmbiente.Ruta> rutas, List<CivilesDeAmbiente.Figura> figuras, System.Random rnd, Transform gGente, Vector3 a, Vector3 b, int cuantos)
        {
            if (cuantos <= 0) return;
            rutas.Add(new CivilesDeAmbiente.Ruta { puntos = new[] { a, b }, circuito = false });
            int ri = rutas.Count - 1;
            float largo = Vector3.Distance(a, b);
            for (int k = 0; k < cuantos; k++)
            {
                var go = NuevoCivil(gGente, rnd.Next(0, PaletaCamisa.Length));
                figuras.Add(new CivilesDeAmbiente.Figura { t = go.transform, ruta = ri, distancia = (float)rnd.NextDouble() * largo * 2f, velocidad = 1.1f + (float)rnd.NextDouble() * 0.7f, fase = (float)rnd.NextDouble() * 6f, auto = false });
            }
        }
    }
}
