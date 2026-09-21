using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    public enum DecalKind { Crater, BulletHole }

    // Marcas persistentes en el terreno (crateres de explosion y agujeros
    // de bala). Mismo problema que los escombros: sin un cupo duro, una
    // batalla larga deja miles de quads. Cada tipo tiene su propio cupo y
    // recicla el mas antiguo al superarlo, asi que la marca mas vieja se
    // va y la mas reciente -- la que informa donde esta cayendo el fuego
    // AHORA -- siempre sobrevive.
    public static class DecalPool
    {
        public const int CraterBudget = 24;
        public const int BulletHoleBudget = 48;

        static readonly Dictionary<DecalKind, List<GameObject>> pools = new Dictionary<DecalKind, List<GameObject>>();
        static Transform root;

        public static int Budget(DecalKind kind) => kind == DecalKind.Crater ? CraterBudget : BulletHoleBudget;
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
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.name.StartsWith("Decal_")) continue;
                if (Application.isPlaying) Object.Destroy(t.gameObject);
                else Object.DestroyImmediate(t.gameObject);
            }
        }

        public static GameObject Spawn(DecalKind kind, Vector3 position, Vector3 normal, float size)
        {
            ResetIfStale();
            if (root == null)
            {
                DestroyOrphans();
                var rootGo = new GameObject("DecalPool");
                rootGo.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild; // ver el comentario en DebrisPool
                root = rootGo.transform;
            }
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
            var spr = SpritesReales.Obtener(kind == DecalKind.Crater ? (Random.value < 0.5f ? "scorch_03" : "scorch_01") : (Random.value < 0.5f ? "scorch_02" : "scorch_01"));
            var decalSr = decal.GetComponent<SpriteRenderer>();
            decalSr.sprite = spr;
            decalSr.color = kind == DecalKind.Crater ? new Color(0.06f, 0.05f, 0.04f, 0.92f) : new Color(0.04f, 0.04f, 0.045f, 0.95f);
            decalSr.sortingOrder = kind == DecalKind.Crater ? -2 : -1;

            // Levantado un pelo sobre la superficie para no pelear con ella
            // por el z-buffer.
            decal.transform.position = position + normal * 0.02f;
            decal.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            // El sprite mide 1 m a escala 1 y su marca ocupa ~60 % del cuadro: se agranda para que "size" sea el ancho visible.
            decal.transform.localScale = Vector3.one * size * 1.6f;
            decal.SetActive(true);

            list.Add(decal);
            return decal;
        }
    }
}
