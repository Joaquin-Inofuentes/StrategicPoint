using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    public enum DecalKind { Crater, BulletHole, Sangre }

    // La quemadura del crater dura 25 s entera y se desvanece en los 35 s siguientes; despues se apaga y su lugar se reusa.
    public class DecalDesvanecido : MonoBehaviour
    {
        public const float Mantener = 25f, Desvanecer = 35f;
        SpriteRenderer sr; Color baseColor; float nace;
        float mantener = Mantener, desvanecer = Desvanecer;

        public void Iniciar(SpriteRenderer render, Color color) { sr = render; baseColor = color; nace = Time.time; mantener = Mantener; desvanecer = Desvanecer; }
        // Ronda 13 (T1): la mancha de sangre dura 8 s en total (6 s entera y 2 s de desvanecido).
        public void Iniciar(SpriteRenderer render, Color color, float mantenerS, float desvanecerS) { Iniciar(render, color); mantener = mantenerS; desvanecer = Mathf.Max(0.01f, desvanecerS); }

        void Update()
        {
            if (sr == null) return;
            float edad = Time.time - nace - mantener;
            if (edad <= 0f) return;
            float k = 1f - Mathf.Clamp01(edad / desvanecer);
            var c = baseColor; c.a *= k; sr.color = c;
            if (k <= 0f) gameObject.SetActive(false);
        }
    }

    // Marcas persistentes en el terreno (crateres de explosion y agujeros
    // de bala). Mismo problema que los escombros: sin un cupo duro, una
    // batalla larga deja miles de quads. Cada tipo tiene su propio cupo y
    // recicla el mas antiguo al superarlo, asi que la marca mas vieja se
    // va y la mas reciente -- la que informa donde esta cayendo el fuego
    // AHORA -- siempre sobrevive.
    public static class DecalPool
    {
        public const int CraterBudget = 24;
        public const int BulletHoleBudget = 120;   // bug #238: los disparos que impactan dejan marca (decal) y duran mas
        public static readonly Color ColorCrater = new Color(0.17f, 0.14f, 0.11f, 0.55f);
        public const float EscalaCrater = 0.95f;   // antes 1.6 (mancha de ~12 m para un obus de 6 m de radio)

        static readonly Dictionary<DecalKind, List<GameObject>> pools = new Dictionary<DecalKind, List<GameObject>>();
        static Transform root;

        public const int SangreBudget = 24;   // Ronda 13 (T1): manchas de cuchillada (dos por golpe como mucho), cada una dura 8 s
        public static int Budget(DecalKind kind) => kind == DecalKind.Crater ? CraterBudget : kind == DecalKind.Sangre ? SangreBudget : BulletHoleBudget;
        public static int CountOf(DecalKind kind) => pools.TryGetValue(kind, out var l) ? l.Count : 0;

        public static void ResetIfStale()
        {
            if (root != null) return;
            pools.Clear();
        }

        // BUG REAL: los decals quedaban manchando la escena despues de
        // frenar Play mode. DontSaveInEditor evita que se escriban al
        // archivo de escena, pero NO los destruye por si solo -- si algo
        // dispara ANTES de que Unity termine de descartar el estado de
        // Play, los quads sobreviven como huerfanos visibles en Editor.
        // Se llama explicitamente al salir de Play (ver
        // Scripts/Editor/PlaymodeCleanup.cs) para que la limpieza no
        // dependa de ese timing.
        static void Destruir(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        public static void ClearAll()
        {
            // DestroyImmediate dentro de Play, ahora que esto corre
            // tambien al cargar una escena y no solo al salir del
            // Editor, destruye el objeto en medio del recorrido de
            // otro sistema. Destroy espera al final del frame.
            foreach (var kv in pools)
                foreach (var go in kv.Value)
                    if (go != null) Destruir(go);
            pools.Clear();
            DestroyOrphans();
            if (root != null) { Destruir(root.gameObject); root = null; }
        }


        // Entrar en Play mode NO destruye los objetos de la escena, pero
        // SI reinicia los estaticos: el root creado en tiempo de edicion
        // sigue vivo mientras las listas que lo indexaban quedan vacias, y
        // sus hijos pasan a ser huerfanos fuera de todo cupo (verificado:
        // duplicaba los decals). Al rearmarse, el pool barre los roots
        // viejos que quedaron sueltos.
        static void DestroyOrphans()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || !t.name.StartsWith("Decal_")) continue;
                if (Application.isPlaying) Object.Destroy(t.gameObject);
                else Object.DestroyImmediate(t.gameObject);
            }
        }

        static void AsegurarRoot()
        {
            if (root != null) return;
            DestroyOrphans();   // barre TODOS los Transform de la escena (~25 ms): por eso se hace en el precalentado, no en el primer disparo
            var rootGo = new GameObject("DecalPool");
            rootGo.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild; // ver el comentario en DebrisPool
            root = rootGo.transform;
        }

        // WP11: el primer agujero de bala del combate tardaba 27 ms (el barrido de huerfanos de arriba); ahora se paga durante la carga del nivel.
        public static void Prewarm()
        {
            ResetIfStale();
            AsegurarRoot();
        }

        static Sprite spriteImpacto;
        // Marca de impacto de bala generada una sola vez (sin assets): 64 px, nucleo oscuro, corona de astillas claras y 5 grietas.
        static Sprite SpriteDeImpacto()
        {
            if (spriteImpacto != null) return spriteImpacto;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            var rng = new System.Random(4242);
            var grietas = new float[5]; for (int i = 0; i < 5; i++) grietas[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0 centro .. 1 borde
                    float a = 0f; byte r = 20, g = 20, b = 22;
                    if (d < 0.20f) { a = 1f; }                                                  // el agujero
                    else if (d < 0.34f) { a = Mathf.Lerp(0.95f, 0.5f, (d - 0.20f) / 0.14f); r = 38; g = 36; b = 36; }   // borde hundido
                    else if (d < 0.62f)
                    {
                        // astillas claras: puntos al azar (hash estable) sobre la corona
                        float h = Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f)) * 43758.5453f; h -= Mathf.Floor(h);
                        if (h > 0.9f) { a = 0.3f * (1f - (d - 0.34f) / 0.28f); r = 120; g = 116; b = 108; }   // #269: corona de astillas mucho mas discreta (antes parecia un aro sucio)
                    }
                    float ang = Mathf.Atan2(dy, dx);
                    for (int i = 0; i < 3; i++)
                    {
                        float da = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, grietas[i] * Mathf.Rad2Deg));
                        float ancho = Mathf.Lerp(3f, 0.8f, d);
                        if (d > 0.18f && d < 0.7f && da < ancho) { float k = 0.55f * (1f - d); if (k > a) { a = k; r = 16; g = 16; b = 18; } }
                    }
                    px[y * n + x] = new Color32(r, g, b, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a)));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            spriteImpacto = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            spriteImpacto.hideFlags = HideFlags.HideAndDontSave;
            return spriteImpacto;
        }

        static Sprite spriteSangre;
        // Mancha de sangre generada una sola vez: un charco irregular (nucleo opaco, borde lobulado) con salpicaduras alrededor.
        static Sprite SpriteDeSangre()
        {
            if (spriteSangre != null) return spriteSangre;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            var rng = new System.Random(777);
            var fase = new float[4]; for (int i = 0; i < 4; i++) fase[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);
            var gotas = new Vector3[9];
            for (int i = 0; i < gotas.Length; i++) { float a = (float)(rng.NextDouble() * Mathf.PI * 2f), r = 0.55f + (float)rng.NextDouble() * 0.35f; gotas[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.04f + (float)rng.NextDouble() * 0.06f); }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float radio = 0.42f + 0.10f * Mathf.Sin(ang * 3f + fase[0]) + 0.06f * Mathf.Sin(ang * 5f + fase[1]) + 0.04f * Mathf.Sin(ang * 8f + fase[2]);
                    float a = d < radio ? Mathf.Clamp01((radio - d) / 0.08f) : 0f;
                    for (int i = 0; i < gotas.Length; i++)
                    {
                        float gd = Mathf.Sqrt((dx - gotas[i].x) * (dx - gotas[i].x) + (dy - gotas[i].y) * (dy - gotas[i].y));
                        if (gd < gotas[i].z) a = Mathf.Max(a, Mathf.Clamp01((gotas[i].z - gd) / 0.03f));
                    }
                    px[y * n + x] = new Color32(150, 6, 8, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a)));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            spriteSangre = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            spriteSangre.hideFlags = HideFlags.HideAndDontSave;
            return spriteSangre;
        }

        public static GameObject Spawn(DecalKind kind, Vector3 position, Vector3 normal, float size)
        {
            ResetIfStale();
            AsegurarRoot();
            if (!pools.TryGetValue(kind, out var list)) { list = new List<GameObject>(); pools[kind] = list; }
            list.RemoveAll(x => x == null);

            GameObject decal;
            if (list.Count >= Budget(kind))
            {
                // Cupo lleno: se reusa el mas antiguo (frente de la lista)
                // y se lo vuelve a poner al final. Nunca se instancia de
                // mas ni se destruye: el cupo es un tope real.
                decal = list[0];
                list.RemoveAt(0);
            }
            else
            {
                decal = new GameObject($"Decal_{kind}");
                // hideFlags va en CADA pieza, no solo en el root: los flags
                // NO se heredan. Con el flag solo en el padre, las piezas se
                // serializaban igual y al recargar la escena aparecian como
                // objetos sueltos de raiz (el padre no se guardaba), fuera de
                // todo cupo y acumulandose en cada build. Verificado: los
                // decals llegaron a triplicar su tope asi.
                decal.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                decal.transform.SetParent(root, false);
                var sr = decal.AddComponent<SpriteRenderer>();
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
            }

            // Ronda 12: antes era un quad gris liso (placeholder). Ahora es una marca real: sprite de quemadura (Kenney, CC0)
            // teñido oscuro y girado al azar para que no se repitan.
            // Bug #238: el agujero de bala es una marca propia (nucleo oscuro, borde astillado claro y grietas), no una quemadura.
            var spr = kind == DecalKind.Crater ? SpritesReales.Obtener(Random.value < 0.5f ? "scorch_03" : "scorch_01") : kind == DecalKind.Sangre ? SpriteDeSangre() : SpriteDeImpacto();
            var decalSr = decal.GetComponent<SpriteRenderer>();
            decalSr.sprite = spr;
            // Pedido: la mancha del crater era un charco negro de ~12 m que no se iba nunca. Ahora es una quemadura marron oscura,
            // translucida (se ve el piso debajo), mas chica, y se desvanece sola (DecalDesvanecido).
            var colorMarca = kind == DecalKind.Crater ? ColorCrater : kind == DecalKind.Sangre ? new Color(1f, 1f, 1f, 0.92f) : Color.white;
            decalSr.color = colorMarca;
            if (kind == DecalKind.Sangre)
            {
                if (!decal.TryGetComponent<DecalDesvanecido>(out var fadeS)) fadeS = decal.AddComponent<DecalDesvanecido>();
                fadeS.Iniciar(decalSr, colorMarca, 6f, 2f);
            }
            else if (kind == DecalKind.Crater)
            {
                if (!decal.TryGetComponent<DecalDesvanecido>(out var fade)) fade = decal.AddComponent<DecalDesvanecido>();
                fade.Iniciar(decalSr, colorMarca);
            }
            decalSr.sortingOrder = kind == DecalKind.Crater ? -2 : -1;

            // Levantado un pelo sobre la superficie para no pelear con ella
            // por el z-buffer.
            // Pedido (disparo del tanque): el crater SIEMPRE va echado sobre el piso -- ni vertical ni de canto --, adaptado al terreno de
            // verdad: se busca el suelo justo debajo y se usa su normal (inclinada como mucho ~35 grados). Y el giro se hace con una
            // referencia estable (LookRotation(-up) sin referencia salta a cualquier angulo y el decal "se rompia").
            if (kind == DecalKind.Crater)
            {
                // Bug #287: el piso REAL (PisoReal), no el techo de un saco, un escombro o una camioneta que haya justo debajo.
                normal = Vector3.up;
                if (SP.Core.PisoReal.Altura(position, out var pisoY, out var pisoN, 1.5f, 12f))
                {
                    position = new Vector3(position.x, pisoY, position.z);
                    normal = pisoN;
                }
            }
            var referencia = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            decal.transform.position = position + normal * 0.02f;
            decal.transform.rotation = Quaternion.LookRotation(-normal, referencia) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            // El sprite mide 1 m a escala 1 y su marca ocupa ~60 % del cuadro: se agranda para que "size" sea el ancho visible.
            decal.transform.localScale = Vector3.one * size * (kind == DecalKind.Crater ? EscalaCrater : 1.6f);
            decal.SetActive(true);

            list.Add(decal);
            return decal;
        }
    }
}
