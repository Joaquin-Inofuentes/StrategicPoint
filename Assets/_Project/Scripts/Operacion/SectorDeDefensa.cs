using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // WP10 (#101): un SECTOR de la defensa de la ciudad. Anillo amarillo de 9 m de radio en el piso (se lee desde la vista tactica), con su letra
    // gigante (la pone el builder) y un anillo gemelo en el minimapa. El director le dice si esta amenazado, caido o cubierto; el sector solo se pinta
    // y responde "esta posicion cae adentro" y "estos son tus puntos de cobertura". Los aliados que llegan se cubren solos en esos puntos (#074).
    public class SectorDeDefensa : MonoBehaviour
    {
        public const float RadioPorDefecto = 9f;

        public string letra = "A";
        public string nombre = "CALLE NORTE";
        public float radio = RadioPorDefecto;
        // Direccion horizontal desde la que llegan los enemigos (hacia afuera del sector): las coberturas buenas son las que tapan de ese lado.
        public Vector3 haciaLaAmenaza = Vector3.forward;

        public enum Estado { Libre, Cubierto, Amenazado, Caido }

        // --- Los llena el director cada tick ---
        public bool Amenazado { get; set; }
        public bool Caido { get; set; }
        public int AliadosDentro { get; set; }
        public float SegundosVacio { get; set; }           // segundos seguidos amenazado y sin nadie adentro
        public float SegundosRecuperando { get; set; }     // segundos seguidos con los aliados necesarios adentro estando caido
        public int Indice { get; set; }

        public Vector3 Centro { get { var p = transform.position; return new Vector3(p.x, 0f, p.z); } }

        public Estado Situacion => Caido ? Estado.Caido : Amenazado && AliadosDentro == 0 ? Estado.Amenazado : AliadosDentro > 0 ? Estado.Cubierto : Estado.Libre;

        public bool Contiene(Vector3 p, float margen = 0f)
        {
            float dx = p.x - transform.position.x, dz = p.z - transform.position.z;
            float r = radio + margen;
            return dx * dx + dz * dz <= r * r;
        }

        // ---------------------------------------------------------------- visual (en Play)
        static Texture2D texAro;
        static Material matMundo, matMapa;
        static Mesh quad;
        Transform aroMundo, aroMapa;
        MeshRenderer rMundo, rMapa;
        MaterialPropertyBlock bloque;
        public static readonly Color Amarillo = new Color(1f, 0.82f, 0.15f, 1f), Verde = new Color(0.35f, 1f, 0.45f, 1f), Rojo = new Color(1f, 0.2f, 0.15f, 1f);
        public Color ColorActual { get; private set; } = Amarillo;
        public bool Dibujado => rMundo != null && rMundo.enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { texAro = null; matMundo = null; matMapa = null; quad = null; }

        static Texture2D TexturaDeAro()
        {
            if (texAro != null) return texAro;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Aro grueso en el borde, relleno suave adentro y marcas de cuatro puntos cardinales.
                    float aro = Mathf.Clamp01(1f - Mathf.Abs(r - 0.95f) / 0.035f);
                    float fill = r < 0.93f ? 0.11f : 0f;
                    float ang = Mathf.Atan2(dy, dx);
                    float marca = (r > 0.82f && r < 0.93f) ? Mathf.Clamp01(Mathf.Abs(Mathf.Sin(ang * 2f)) > 0.985f ? 1f : 0f) * 0.9f : 0f;
                    float a = Mathf.Clamp01(Mathf.Max(aro, Mathf.Max(fill, marca))) * Mathf.Clamp01((1f - r) / 0.015f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return texAro = tex;
        }

        static Mesh Quad()
        {
            if (quad != null) return quad;
            var m = new Mesh { name = "SectorQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
            return quad = m;
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            Construir();
        }

        void Construir()
        {
            if (aroMundo != null) return;
            if (matMundo == null) matMundo = DiamondGizmo.NuevoMaterialTransparente(Color.white, TexturaDeAro());
            if (matMapa == null) matMapa = DiamondGizmo.NuevoMaterialTransparente(Color.white, TexturaDeAro());
            bloque = new MaterialPropertyBlock();

            // Anillo del mundo, a ras (apenas sobre el asfalto, que esta a 0,08).
            var go = new GameObject("AnilloDelSector_" + letra);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(transform.position.x, 0.32f, transform.position.z);
            go.transform.localScale = new Vector3(radio * 2f, 1f, radio * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            rMundo = go.AddComponent<MeshRenderer>();
            rMundo.sharedMaterial = matMundo;
            rMundo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rMundo.receiveShadows = false;
            aroMundo = go.transform;

            // Anillo gemelo en el minimapa (capa Minimap, a y=44: debajo de los iconos de unidades).
            int layer = LayerMask.NameToLayer("Minimap");
            if (layer < 0) layer = 8;
            var gm = new GameObject("AnilloDelSectorMinimapa_" + letra) { layer = layer };
            gm.transform.SetParent(transform, false);
            gm.transform.position = new Vector3(transform.position.x, 44f, transform.position.z);
            gm.transform.localScale = new Vector3(radio * 2.4f, 1f, radio * 2.4f);
            gm.AddComponent<MeshFilter>().sharedMesh = Quad();
            rMapa = gm.AddComponent<MeshRenderer>();
            rMapa.sharedMaterial = matMapa;
            rMapa.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rMapa.receiveShadows = false;
            aroMapa = gm.transform;
            Pintar(Amarillo, 0.3f);
        }

        public const float AlfaMaximoDelAro = 0.35f;
        public float AlfaActual { get; private set; }
        public float AlfaMaximoPintado { get; private set; }

        void Pintar(Color c, float alfa)
        {
            if (rMundo == null || bloque == null) return;
            ColorActual = c;
            // #118: el aro tapaba el piso (alfa 0,55-0,85): tope 0,35.
            alfa = Mathf.Min(alfa, AlfaMaximoDelAro);
            c.a = alfa; AlfaActual = alfa; AlfaMaximoPintado = Mathf.Max(AlfaMaximoPintado, alfa);
            bloque.SetColor("_BaseColor", c); bloque.SetColor("_Color", c);
            rMundo.SetPropertyBlock(bloque);
            rMapa.SetPropertyBlock(bloque);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || rMundo == null) return;
            switch (Situacion)
            {
                case Estado.Caido: Pintar(Rojo, 0.3f); break;
                case Estado.Amenazado: Pintar(Rojo, 0.2f + 0.15f * (0.5f + 0.5f * Mathf.Sin(Time.time * 9f))); break;
                case Estado.Cubierto: Pintar(Verde, 0.28f); break;
                default: Pintar(Amarillo, 0.22f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f))); break;
            }
        }

        public void Mostrar(bool si)
        {
            if (rMundo != null) rMundo.enabled = si;
            if (rMapa != null) rMapa.enabled = si;
        }

        // ---------------------------------------------------------------- coberturas
        // Los "max" puntos de cobertura del sector: adentro del anillo, con el obstaculo ENTRE el punto y la amenaza (o al menos de costado), y
        // separados entre si. Se calcula sobre Coberturas.Puntos (las barricadas y sacos del builder y los que arma el director).
        public readonly struct PuntoDeCobertura
        {
            public readonly Vector3 punto; public readonly Collider dueno;
            public PuntoDeCobertura(Vector3 p, Collider c) { punto = p; dueno = c; }
        }

        public List<PuntoDeCobertura> PuntosDeCobertura(int max = 4)
        {
            var res = new List<PuntoDeCobertura>();
            var puntos = Coberturas.Puntos; var duenos = Coberturas.Duenos;
            var hacia = haciaLaAmenaza; hacia.y = 0f; hacia = hacia.sqrMagnitude > 0.01f ? hacia.normalized : Vector3.forward;
            var centro = Centro;
            // Dos pasadas: primero los que tapan de verdad del lado de la amenaza, despues lo que sirva.
            for (int pasada = 0; pasada < 2 && res.Count < max; pasada++)
            {
                float minimo = pasada == 0 ? 0.35f : -0.2f;
                while (res.Count < max)
                {
                    int mejor = -1; float mejorPuntaje = float.MaxValue;
                    for (int i = 0; i < puntos.Count; i++)
                    {
                        var p = puntos[i];
                        if (duenos[i] == null) continue;
                        float dx = p.x - centro.x, dz = p.z - centro.z;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        if (d > radio - 0.8f) continue;
                        if (Mathf.Abs(p.y) > 1.5f) continue;
                        var obstaculoHacia = -Coberturas.FrenteDe(i);   // del punto hacia el obstaculo
                        if (Vector3.Dot(obstaculoHacia, hacia) < minimo) continue;
                        bool ocupado = false;
                        foreach (var r in res) { var q = r.punto - p; q.y = 0f; if (q.sqrMagnitude < 2.2f * 2.2f) { ocupado = true; break; } }
                        if (ocupado) continue;
                        // Mejor: del lado de la amenaza (sin alejarse del centro mas de la cuenta), lejos del resto.
                        float puntaje = -Vector3.Dot(obstaculoHacia, hacia) * 4f + d * 0.25f;
                        if (puntaje < mejorPuntaje) { mejorPuntaje = puntaje; mejor = i; }
                    }
                    if (mejor < 0) break;
                    res.Add(new PuntoDeCobertura(puntos[mejor], duenos[mejor]));
                }
            }
            return res;
        }
    }
}
