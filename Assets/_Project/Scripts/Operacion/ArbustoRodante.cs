using System.Collections.Generic;
using UnityEngine;
using SP.Core;

namespace SP.Operacion
{
    // Ronda 13 T14: ambientacion de desierto. El "sector Ruta" es la franja de la autopista del tanque (a SectorDeRuta.Margen m de su polilinea, sin la ciudad).
    public static class SectorDeRuta
    {
        public const float Margen = 90f;
        static readonly List<Vector2> poli = new List<Vector2>();
        public static int Puntos => poli.Count;

        public static void Definir(Transform[] ruta)
        {
            poli.Clear();
            if (ruta != null) foreach (var r in ruta) if (r != null) poli.Add(new Vector2(r.position.x, r.position.z));
        }

        public static float DistanciaALaRuta(Vector3 p)
        {
            if (poli.Count == 0) return float.MaxValue;
            if (poli.Count == 1) return (new Vector2(p.x, p.z) - poli[0]).magnitude;
            float mejor = float.MaxValue; var q = new Vector2(p.x, p.z);
            for (int i = 0; i < poli.Count - 1; i++)
            {
                var a = poli[i]; var ab = poli[i + 1] - a;
                float k = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                mejor = Mathf.Min(mejor, (q - (a + ab * k)).magnitude);
            }
            return mejor;
        }

        // Dentro de la franja de la ruta y fuera de la ciudad (x >= 190 y z >= 100).
        public static bool Contiene(Vector3 p) => poli.Count > 0 && !(p.x >= 190f && p.z >= 100f) && DistanciaALaRuta(p) <= Margen;
    }

    // Arbusto seco que rueda empujado por el viento. Cinematico: sin Rigidbody ni collider (no bloquea ni frena nada).
    // Pool de 10: reaparecen a 30-60 m de la camara, solo mientras la camara esta en el sector Ruta.
    public sealed class ArbustoRodante : MonoBehaviour
    {
        public const int Cantidad = 10;
        public const float DistMin = 30f, DistMax = 60f, DistReciclado = 75f;
        public static readonly Vector3 Viento = new Vector3(0.93f, 0f, 0.37f);

        public static int Activos { get { int n = 0; foreach (var a in Pool) if (a != null && a.gameObject.activeSelf) n++; return n; } }
        public static readonly List<ArbustoRodante> Pool = new List<ArbustoRodante>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Pool.Clear(); malla = null; material = null; }

        static Mesh malla; static Material material;

        float velocidad, escala, saltoFase, giro; Transform cuerpo;

        public static void Armar(Transform[] ruta)
        {
            if (!Application.isPlaying) return;
            SectorDeRuta.Definir(ruta);
            var previo = GameObject.Find("ArbustosRodantes"); if (previo != null) Object.Destroy(previo);
            Pool.Clear();
            BuscarMallaYMaterial();
            var raiz = new GameObject("ArbustosRodantes");
            for (int i = 1; i <= Cantidad; i++)
            {
                var go = new GameObject("ArbustoRodante_" + i);
                go.transform.SetParent(raiz.transform, false);
                var cuerpo = new GameObject("Cuerpo").transform; cuerpo.SetParent(go.transform, false);
                var mf = cuerpo.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = malla;
                var mr = cuerpo.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                var a = go.AddComponent<ArbustoRodante>(); a.cuerpo = cuerpo;
                a.Parametros(i);
                Pool.Add(a);
                go.SetActive(false);   // el primer Update del coordinador los coloca
            }
            raiz.AddComponent<CoordinadorDeRodantes>();
        }

        void Parametros(int semilla)
        {
            var rng = new System.Random(semilla * 31 + 5);
            velocidad = 1.6f + (float)rng.NextDouble() * 2.2f;
            escala = 0.9f + (float)rng.NextDouble() * 0.7f;
            saltoFase = (float)rng.NextDouble() * 6.28f;
            giro = 70f + (float)rng.NextDouble() * 60f;
            cuerpo.localScale = Vector3.one * escala;
        }

        // R14 (#4): la malla Op_MataSeca son astillas sueltas (de pie parecen una mata, rodando se ve la geometria "rota"). El rodante
        // ahora es una bola cubica lowpoly cerrada: icosfera de 80 caras, facetada, con los vertices desplazados un poco para que parezca un matorral enredado.
        public static Mesh CrearMallaBola()
        {
            var v = new List<Vector3>(); var f = new List<int[]>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            v.Add(new Vector3(-1, t, 0)); v.Add(new Vector3(1, t, 0)); v.Add(new Vector3(-1, -t, 0)); v.Add(new Vector3(1, -t, 0));
            v.Add(new Vector3(0, -1, t)); v.Add(new Vector3(0, 1, t)); v.Add(new Vector3(0, -1, -t)); v.Add(new Vector3(0, 1, -t));
            v.Add(new Vector3(t, 0, -1)); v.Add(new Vector3(t, 0, 1)); v.Add(new Vector3(-t, 0, -1)); v.Add(new Vector3(-t, 0, 1));
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            int[][] caras = { new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                              new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
            var medio = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (medio.TryGetValue(k, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized); medio[k] = v.Count - 1; return v.Count - 1;
            }
            foreach (var c in caras)
            {
                int a = Mid(c[0], c[1]), b = Mid(c[1], c[2]), d = Mid(c[2], c[0]);
                f.Add(new[] { c[0], a, d }); f.Add(new[] { c[1], b, a }); f.Add(new[] { c[2], d, b }); f.Add(new[] { a, b, d });
            }
            // desplazamiento por vertice compartido (determinista): 0,78..1,12 del radio
            var radio = new float[v.Count];
            for (int i = 0; i < v.Count; i++) { var h = Mathf.Abs(Mathf.Sin(v[i].x * 91.7f + v[i].y * 57.3f + v[i].z * 33.1f) * 43758.5453f); radio[i] = 0.78f + (h - Mathf.Floor(h)) * 0.34f; }
            var vs = new List<Vector3>(); var tris = new List<int>();
            foreach (var c in f)
                for (int k = 0; k < 3; k++) { vs.Add(v[c[k]] * radio[c[k]] * 0.5f); tris.Add(vs.Count - 1); }   // diametro ~1 m a escala 1
            var m2 = new Mesh { name = "R14_BolaRodante" };
            m2.SetVertices(vs); m2.SetTriangles(tris, 0); m2.RecalculateNormals(); m2.RecalculateBounds();
            return m2;
        }

        static void BuscarMallaYMaterial()
        {
            if (malla == null) malla = CrearMallaBola();   // ronda 16: no crear una malla nueva (y fugar la anterior) en cada Armar
            if (material == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
                material = new Material(sh) { name = "M_Op_MataRodante" };
                var c = new Color(0.55f, 0.42f, 0.22f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
                if (material.HasProperty("_Color")) material.SetColor("_Color", c);
            }
        }

        // Lo llama el coordinador: lo pone a 30-60 m de la camara (en el sector, sobre un piso razonable). false = no encontro lugar.
        public bool Reaparecer(Camera cam)
        {
            for (int intento = 0; intento < 14; intento++)
            {
                float ang = Random.value * Mathf.PI * 2f, d = Random.Range(DistMin, DistMax);
                var p = cam.transform.position + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d);
                if (!SectorDeRuta.Contiene(p)) continue;
                if (!Physics.Raycast(new Vector3(p.x, cam.transform.position.y + 25f, p.z), Vector3.down, out var h, 80f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (h.normal.y < 0.85f || h.point.y > cam.transform.position.y + 3f) continue;
                transform.position = h.point; gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var p = transform.position + Viento * velocidad * dt;
            // pega al piso (rebote suave): un raycast corto hacia abajo
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var h, 12f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y > 0.7f) p.y = h.point.y;
            transform.position = p;
            saltoFase += dt * 5f;
            cuerpo.localPosition = new Vector3(0f, escala * 0.42f + Mathf.Abs(Mathf.Sin(saltoFase)) * 0.15f, 0f);
            cuerpo.Rotate(Vector3.Cross(Vector3.up, Viento), giro * dt, Space.World);   // rueda en el sentido del viento
        }
    }

    // Mantiene el pool: activa/desactiva segun el sector y recicla los que quedaron lejos de la camara.
    public sealed class CoordinadorDeRodantes : MonoBehaviour
    {
        float proximo;
        void Update()
        {
            if (Time.time < proximo) return; proximo = Time.time + 0.25f;
            var cam = CamaraPrincipal.Actual;
            bool enSector = cam != null && SectorDeRuta.Contiene(cam.transform.position);
            foreach (var a in ArbustoRodante.Pool)
            {
                if (a == null) continue;
                if (!enSector) { if (a.gameObject.activeSelf) a.gameObject.SetActive(false); continue; }
                bool lejos = a.gameObject.activeSelf && (Vector3.Distance(a.transform.position, cam.transform.position) > ArbustoRodante.DistReciclado || !SectorDeRuta.Contiene(a.transform.position));
                if (!a.gameObject.activeSelf || lejos) a.Reaparecer(cam);
            }
        }
    }
}
