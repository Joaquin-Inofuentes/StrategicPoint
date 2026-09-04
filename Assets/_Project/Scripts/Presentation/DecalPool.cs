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
        public static void ClearAll()
        {
            foreach (var kv in pools)
                foreach (var go in kv.Value)
                    if (go != null) Object.DestroyImmediate(go);
            pools.Clear();
            DestroyOrphans();
            if (root != null) { Object.DestroyImmediate(root.gameObject); root = null; }
        }

        static readonly Color CraterColor = new Color(0.18f, 0.14f, 0.10f);
        static readonly Color BulletHoleColor = new Color(0.12f, 0.12f, 0.13f);


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
                decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
                decal.name = $"Decal_{kind}";
            // hideFlags va en CADA pieza, no solo en el root: los flags
            // NO se heredan. Con el flag solo en el padre, las piezas se
            // serializaban igual y al recargar la escena aparecian como
            // objetos sueltos de raiz (el padre no se guardaba), fuera de
            // todo cupo y acumulandose en cada build. Verificado: los
            // decals llegaron a triplicar su tope asi.
                decal.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                var col = decal.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Object.Destroy(col);
                    else Object.DestroyImmediate(col);
                }
                decal.transform.SetParent(root, false);
                var rend = decal.GetComponent<MeshRenderer>();
                rend.sharedMaterial = SafeMaterial.Create(kind == DecalKind.Crater ? CraterColor : BulletHoleColor);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Mismo caso que en DebrisPool: el Material creado por codigo
            // no es un asset, y una reconstruccion de escena lo destruye
            // dejando al Renderer sin material aunque el objeto siga vivo.
            var decalRend = decal.GetComponent<MeshRenderer>();
            if (decalRend != null && decalRend.sharedMaterial == null)
                decalRend.sharedMaterial = SafeMaterial.Create(kind == DecalKind.Crater ? CraterColor : BulletHoleColor);

            // Levantado un pelo sobre la superficie para no pelear con ella
            // por el z-buffer.
            decal.transform.position = position + normal * 0.02f;
            decal.transform.rotation = Quaternion.LookRotation(-normal);
            decal.transform.localScale = Vector3.one * size;
            decal.SetActive(true);

            list.Add(decal);
            return decal;
        }
    }
}
