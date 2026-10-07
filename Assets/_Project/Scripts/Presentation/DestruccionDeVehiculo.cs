using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SP.Presentation
{
    // Bug #072 (parte visual): explosiones con CRATER en el piso (que antes quedaba escondido bajo el asfalto), chapas que salen volando
    // al destruirse un vehiculo, y el piso donde apoyar todo eso. Cupos duros (cráteres 16, chapas 24): mismo criterio que
    // DecalPool / DebrisPool / Fragmentador, el mas viejo se recicla.
    public static class DestruccionDeVehiculo
    {
        static readonly RaycastHit[] golpes = new RaycastHit[8];

        // Altura del piso bajo un punto. El asfalto de la Operacion son cubos SIN collider (tope en y = 0,08) sobre un piso en y = 0: el
        // rayo da 0 y la malla de navegacion (que si sigue el asfalto) da ~0,08; se toma la mayor de las dos si estan cerca.
        // Tope del asfalto (cubos "Pintura" de 0,08 m): nada que se apoye en el piso de la Operacion puede quedar por debajo.
        public const float TopeDelAsfalto = 0.08f;

        public static float AlturaDelPiso(Vector3 p)
        {
            float y = 0f;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 0.5f, p.z), Vector3.down, golpes, 8f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.MaxValue; bool hay = false;
            for (int i = 0; i < n; i++)
            {
                var h = golpes[i];
                if (h.collider == null || h.distance >= mejor) continue;
                if (!SP.Core.NavService.BlocksMovement(h.collider)) continue;
                // Solo geometria fija: el chasis del propio vehiculo (o cualquier cuerpo suelto) no es el piso (el cráter quedaba flotando a 1,1 m).
                if (h.collider.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                mejor = h.distance; y = h.point.y; hay = true;
            }
            if (!hay) y = 0f;
            if (NavMesh.SamplePosition(new Vector3(p.x, y, p.z), out var nh, 1.2f, NavMesh.AllAreas) && Mathf.Abs(nh.position.y - y) < 0.35f)
                y = Mathf.Max(y, nh.position.y);
            return Mathf.Max(y, TopeDelAsfalto);
        }

        // ---- Chapas ----
        public static int ChapasActivas => ChapaVolante.Activas;
        public static int ChapasLanzadas => ChapaVolante.Lanzadas;

        public static void Chapas(Vector3 centro, int cantidad, float fuerza)
        {
            if (!Application.isPlaying) return;
            float suelo = AlturaDelPiso(centro) + 0.03f;
            for (int i = 0; i < cantidad; i++)
            {
                var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.7f + 0.55f;
                ChapaVolante.Lanzar(centro + Random.insideUnitSphere * 0.5f, dir.normalized * fuerza * Random.Range(0.55f, 1.15f), suelo);
            }
        }

        // Parte los cubos (y piezas) de la carroceria que se nombren: cada uno sale en pocos trozos con fisica y la pieza original se apaga.
        public static int RomperPiezas(Transform visual, string[] nombres, Vector3 origen, float fuerza, int piezasPorCubo)
        {
            if (!Application.isPlaying || visual == null) return 0;
            int n = 0;
            foreach (var nombre in nombres)
            {
                var t = visual.Find(nombre);
                if (t == null || !t.gameObject.activeSelf) continue;
                n += Fragmentador.Romper(t, origen, fuerza, piezasPorCubo);
                t.gameObject.SetActive(false);
            }
            return n;
        }
    }

    // Una chapa de carroceria: cubo plano de 0,6 x 0,05 x 0,4 que vuela, rebota en el piso girando y se queda unos segundos.
    public class ChapaVolante : MonoBehaviour
    {
        public const int Cupo = 24;
        const float Gravedad = 16f, Vida = 6.5f, Hundido = 0.6f;
        static readonly List<ChapaVolante> todas = new List<ChapaVolante>();
        static Transform raiz;
        static Material material;
        public static int Lanzadas { get; private set; }
        public static int Activas { get { int n = 0; for (int i = 0; i < todas.Count; i++) if (todas[i] != null && todas[i].activa) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todas.Clear(); raiz = null; material = null; Lanzadas = 0; }

        public static void LimpiarTodo()
        {
            foreach (var c in todas) if (c != null) { if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject); }
            todas.Clear();
            if (raiz != null) { if (Application.isPlaying) Destroy(raiz.gameObject); else DestroyImmediate(raiz.gameObject); raiz = null; }
        }

        bool activa;
        float edad;
        Vector3 vel, giro, tam;
        float suelo;
        int rebotes;
        bool quieta;

        static ChapaVolante Tomar()
        {
            if (raiz == null)
            {
                todas.Clear();
                raiz = new GameObject("ChapasPool") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild }.transform;
            }
            todas.RemoveAll(x => x == null);
            for (int i = 0; i < todas.Count; i++) if (!todas[i].activa) return todas[i];
            if (todas.Count >= Cupo)
            {
                var viejo = todas[0];
                for (int i = 1; i < todas.Count; i++) if (todas[i].edad > viejo.edad) viejo = todas[i];
                return viejo;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Chapa";
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Destroy(go.GetComponent<BoxCollider>());
            go.transform.SetParent(raiz, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material == null) material = SafeMaterial.CreateShared();
            mr.sharedMaterial = material;
            var c = go.AddComponent<ChapaVolante>();
            todas.Add(c);
            return c;
        }

        public static void Lanzar(Vector3 pos, Vector3 velocidad, float sueloY)
        {
            var c = Tomar();
            c.activa = true; c.edad = 0f; c.rebotes = 0; c.quieta = false;
            c.suelo = sueloY;
            c.vel = velocidad;
            c.giro = Random.insideUnitSphere * 720f;
            float k = Random.Range(0.7f, 1.3f);
            c.tam = new Vector3(0.6f, 0.05f, 0.4f) * k;
            c.transform.SetPositionAndRotation(pos, Random.rotation);
            c.transform.localScale = c.tam;
            var mr = c.GetComponent<MeshRenderer>();
            if (mr.sharedMaterial == null) mr.sharedMaterial = material = SafeMaterial.CreateShared();
            CubeFxReactor.WriteTint(mr, Color.Lerp(new Color(0.14f, 0.12f, 0.11f), new Color(0.5f, 0.16f, 0.1f), Random.value));
            c.gameObject.SetActive(true);
            Lanzadas++;
        }

        void Update()
        {
            if (!activa) return;
            float dt = Time.deltaTime;
            edad += dt;
            if (!quieta)
            {
                vel.y -= Gravedad * dt;
                transform.position += vel * dt;
                transform.Rotate(giro * dt, Space.Self);
                if (transform.position.y <= suelo && vel.y < 0f)
                {
                    var p = transform.position; p.y = suelo; transform.position = p;
                    rebotes++;
                    vel.y = -vel.y * 0.4f;
                    vel.x *= 0.65f; vel.z *= 0.65f;
                    giro *= 0.6f;
                    if (vel.y < 1.3f || rebotes >= 4)
                    {
                        // Queda acostada sobre el piso.
                        quieta = true; vel = Vector3.zero;
                        var e = transform.eulerAngles;
                        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
                    }
                }
            }
            float resto = Vida - edad;
            if (resto <= Hundido)
            {
                transform.localScale = tam * Mathf.Clamp01(resto / Hundido);
                if (resto <= 0f) { activa = false; gameObject.SetActive(false); }
            }
        }
    }

    // Crater de explosion: disco oscuro con un borde de 8 bloques bajos de tierra irregulares, de 2,5 a 3,5 m, sobre el piso (con la altura del
    // asfalto: el decal plano de antes quedaba enterrado). Pool de 16 con vida de 60 s.
    public static class CraterPool
    {
        public const int Cupo = 16;
        public const float Vida = 60f;
        public const float DiametroMin = 2.5f, DiametroMax = 3.5f;
        static readonly List<CraterVida> todos = new List<CraterVida>();
        static Transform raiz;
        static Material matDisco, matBorde;
        static readonly Mesh[] mallas = new Mesh[4];

        public static int Activos { get { int n = 0; for (int i = 0; i < todos.Count; i++) if (todos[i] != null && todos[i].gameObject.activeSelf) n++; return n; } }
        public static int Lanzados { get; private set; }
        public static IReadOnlyList<CraterVida> Piezas => todos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { todos.Clear(); raiz = null; matDisco = matBorde = null; for (int i = 0; i < mallas.Length; i++) mallas[i] = null; Lanzados = 0; }

        public static void LimpiarTodo()
        {
            foreach (var c in todos) if (c != null) { if (Application.isPlaying) Object.Destroy(c.gameObject); else Object.DestroyImmediate(c.gameObject); }
            todos.Clear();
            if (raiz != null) { if (Application.isPlaying) Object.Destroy(raiz.gameObject); else Object.DestroyImmediate(raiz.gameObject); raiz = null; }
        }

        // Dos submallas: 0 = disco, 1 = borde. Radio 1: se escala a la medida del crater.
        static Mesh Malla(int i)
        {
            if (mallas[i] != null) return mallas[i];
            var rnd = new System.Random(7000 + i * 31);
            var v = new List<Vector3>(); var disco = new List<int>(); var borde = new List<int>();
            const int seg = 24;
            v.Add(new Vector3(0f, 0.015f, 0f));
            for (int k = 0; k < seg; k++)
            {
                float a = k / (float)seg * Mathf.PI * 2f;
                float r = 0.86f + (float)rnd.NextDouble() * 0.2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, 0.015f, Mathf.Sin(a) * r));
            }
            for (int k = 0; k < seg; k++) { disco.Add(0); disco.Add(1 + (k + 1) % seg); disco.Add(1 + k); }
            // Borde: 8 bloques bajos de tierra, irregulares, orientados a lo largo del anillo.
            for (int b = 0; b < 8; b++)
            {
                float a = (b + (float)rnd.NextDouble() * 0.5f) / 8f * Mathf.PI * 2f;
                float rr = 0.98f + (float)rnd.NextDouble() * 0.16f;
                float w = 0.34f + (float)rnd.NextDouble() * 0.22f, d = 0.2f + (float)rnd.NextDouble() * 0.16f, h = 0.1f + (float)rnd.NextDouble() * 0.12f;
                var centro = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                var tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                AgregarBloque(v, borde, centro, tang * w * 0.5f, radial * d * 0.5f, h, rnd);
            }
            var m = new Mesh { name = "CraterMalla", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild, subMeshCount = 2 };
            m.SetVertices(v);
            m.SetTriangles(disco, 0);
            m.SetTriangles(borde, 1);
            m.RecalculateNormals(); m.RecalculateBounds();
            mallas[i] = m;
            return m;
        }

        // Bloque con tapa irregular (cada esquina superior corrida un poco): 8 vertices, 12 triangulos.
        static void AgregarBloque(List<Vector3> v, List<int> tri, Vector3 c, Vector3 ex, Vector3 ez, float h, System.Random rnd)
        {
            int i0 = v.Count;
            float J() => ((float)rnd.NextDouble() - 0.5f) * 0.5f;
            v.Add(c - ex - ez); v.Add(c + ex - ez); v.Add(c + ex + ez); v.Add(c - ex + ez);
            v.Add(c - ex - ez + Vector3.up * (h * (1f + J()))); v.Add(c + ex - ez + Vector3.up * (h * (1f + J())));
            v.Add(c + ex + ez + Vector3.up * (h * (1f + J()))); v.Add(c - ex + ez + Vector3.up * (h * (1f + J())));
            int[] q = { 4, 5, 6, 4, 6, 7,   0, 4, 7, 0, 7, 3,   1, 2, 6, 1, 6, 5,   0, 1, 5, 0, 5, 4,   3, 7, 6, 3, 6, 2 };
            foreach (var k in q) tri.Add(i0 + k);
        }

        static void Materiales()
        {
            if (matDisco == null) matDisco = SafeMaterial.Create(new Color(0.045f, 0.04f, 0.035f));
            if (matBorde == null) matBorde = SafeMaterial.Create(new Color(0.24f, 0.18f, 0.12f));
        }

        static CraterVida Tomar()
        {
            if (raiz == null)
            {
                todos.Clear();
                raiz = new GameObject("CraterPool") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild }.transform;
            }
            todos.RemoveAll(x => x == null);
            for (int i = 0; i < todos.Count; i++) if (!todos[i].gameObject.activeSelf) return todos[i];
            if (todos.Count >= Cupo)
            {
                var viejo = todos[0];
                for (int i = 1; i < todos.Count; i++) if (todos[i].Nace < viejo.Nace) viejo = todos[i];
                return viejo;
            }
            var go = new GameObject("Crater") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            go.transform.SetParent(raiz, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Malla(todos.Count % mallas.Length);
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var c = go.AddComponent<CraterVida>();
            todos.Add(c);
            return c;
        }

        // y = altura del piso; el crater queda 2 cm encima (sobre el asfalto, no debajo).
        public static CraterVida Crear(Vector3 punto, float pisoY, float diametro)
        {
            if (!Application.isPlaying) return null;
            Materiales();
            var c = Tomar();
            var mr = c.GetComponent<MeshRenderer>();
            if (mr.sharedMaterials.Length != 2 || mr.sharedMaterials[0] == null || mr.sharedMaterials[1] == null) mr.sharedMaterials = new[] { matDisco, matBorde };
            float s = Mathf.Clamp(diametro, DiametroMin, DiametroMax) * 0.5f;
            c.transform.SetPositionAndRotation(new Vector3(punto.x, pisoY + 0.02f, punto.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            c.transform.localScale = new Vector3(s, 1f, s);
            c.Nace = Time.time;
            c.Escala = c.transform.localScale;
            c.gameObject.SetActive(true);
            Lanzados++;
            return c;
        }
    }

    public class CraterVida : MonoBehaviour
    {
        public float Nace;
        public Vector3 Escala;

        void Update()
        {
            float resto = CraterPool.Vida - (Time.time - Nace);
            if (resto > 2f) return;
            // Los ultimos 2 s se hunde en el piso.
            float k = Mathf.Clamp01(resto / 2f);
            transform.localScale = new Vector3(Escala.x, Escala.y * k, Escala.z);
            if (resto <= 0f) gameObject.SetActive(false);
        }
    }
}
