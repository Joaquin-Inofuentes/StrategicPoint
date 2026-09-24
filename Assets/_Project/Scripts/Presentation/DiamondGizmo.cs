using UnityEngine;
using UnityEngine.Rendering;

namespace SP.Presentation
{
    // Utilidad compartida para armar el gizmo de rombo que reemplaza a los
    // cilindros localizadores. Pedido explicito: "esos rombos... son 2. El
    // fondo blanco que hace de contorno por que es mas grande. Y el
    // interior varia: Rojo enemigo, Azul aliado, Amarillo objetivo" -- y
    // que siempre apunten al jugador principal (billboard) con mucho
    // contraste contra el ambiente. La usan UnitLocatorCylinder (enemigo/
    // aliado) y ObjectiveDiamondMarker (objetivo).
    public static class DiamondGizmo
    {
        static Mesh caraCompartida;

        // Malla de un rombo unidad en el plano XY (vertices arriba/derecha/
        // abajo/izquierda), pensada para vivir en un GameObject que despues
        // se rota entero para mirar a la camara (billboard) -- no hace
        // falta que la malla en si sepa nada de camaras.
        static Mesh MallaCompartida()
        {
            if (caraCompartida != null) return caraCompartida;
            var m = new Mesh { name = "DiamondQuad", hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(-1f, 0f, 0f),
            };
            m.uv = new[] { new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f) };
            // BUG REAL: antes esto tenia DOS sets de triangulos (uno para
            // cada cara) compartiendo los mismos 4 vertices. RecalculateNormals
            // promedia las normales de todos los triangulos que tocan un
            // vertice -- con las dos caras opuestas presentes, las normales de
            // ida y vuelta se cancelaban entre si y quedaban practicamente en
            // cero, asi que el shader Lit dibujaba el rombo NEGRO solido en
            // vez del color pedido. Un solo set de triangulos (cara unica,
            // normales limpias) y "Cull Off" en el material (ver NuevoMaterial)
            // resuelven lo mismo -- que se vea desde cualquier lado del
            // billboard -- sin duplicar geometria ni romper las normales.
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            caraCompartida = m;
            return m;
        }

        // Un solo GameObject hijo con la malla del rombo, escalado a
        // "tamano" y pintado con "mat". No tiene collider (es un marcador
        // visual puro).
        public static GameObject CrearCara(string nombre, Transform padre, float tamano, Material mat)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localScale = Vector3.one * tamano;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = MallaCompartida();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        // BUG REAL encontrado jugando: la emision iba al 100% del color base
        // (blanco puro en el borde, rojo/azul/amarillo puros adentro). Con
        // Bloom activo eso vuela la exposicion de TODA la pantalla apenas
        // hay dos o tres rombos a la vista -- "la luz que emana es
        // demasiada, opaca todo". El color base (mat.color, sin emision) ya
        // es solido y saturado y se lee bien de por si; la emision ahora es
        // solo un empujon chico para que no se apague en el modo noche, no
        // el brillo principal.
        const float IntensidadEmision = 0.12f;

        // Material SOLIDO (no transparente) y con un toque de emision: pedido
        // explicito de "mucho contraste con el ambiente", y estos rombos
        // tienen que leerse igual de bien en el modo noche/niebla del nivel
        // que a pleno dia.
        public static Material NuevoMaterial(Color color)
        {
            var mat = SafeMaterial.Create(color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * IntensidadEmision);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            // Cull Off: la malla del rombo tiene una sola cara (ver el
            // comentario en MallaCompartida); esto la hace visible aunque el
            // billboard quede mirando de canto por un frame en vez de
            // desaparecer o duplicar geometria.
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
            return mat;
        }
    }
}
