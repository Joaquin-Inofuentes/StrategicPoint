using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Marca el lugar donde cae un enemigo con un cilindro delgado que sube
    // muy por encima del mapa y se desvanece solo. Mismo patron de pool +
    // material compartido que OrderMarkerFx: crear un primitivo nuevo y
    // resolver el shader en cada muerte seria el mismo tiron que ya se evito
    // ahi, y una escaramuza entera de enemigos cayendo a la vez es
    // exactamente el peor caso.
    public class KillCylinderFx : MonoBehaviour
    {
        static readonly Color BeaconColor = new Color(0.95f, 0.35f, 0.12f);

        // Altura grande en vez de literalmente infinita: un cilindro real
        // necesita un tamano, y esto ya sobra por lejos a la camara mas alta
        // del juego (RTS a full zoom).
        const float Altura = 400f;
        const float Radio = 0.35f;
        const float Duracion = 2.5f;

        public static int Budget => 16;
        public static int ActiveCount { get { Purge(); return inUse.Count; } }
        public static int TotalCount { get { Purge(); return all.Count; } }

        static readonly List<KillCylinderFx> all = new List<KillCylinderFx>();
        static readonly Queue<KillCylinderFx> free = new Queue<KillCylinderFx>();
        static readonly List<KillCylinderFx> inUse = new List<KillCylinderFx>();
        static Transform root;
        static Material sharedMaterial;

        public static void ResetIfStale()
        {
            if (root != null) return;
            all.Clear();
            free.Clear();
            inUse.Clear();
        }

        static void Purge()
        {
            all.RemoveAll(x => x == null);
            inUse.RemoveAll(x => x == null);
        }

        static void DestroyOrphans()
        {
            foreach (var m in Object.FindObjectsByType<KillCylinderFx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (m == null || all.Contains(m)) continue;
                if (Application.isPlaying) Object.Destroy(m.gameObject);
                else Object.DestroyImmediate(m.gameObject);
            }
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            DestroyOrphans();
            var go = new GameObject("KillCylinderPool");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;
        }

        static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    sharedMaterial = SafeMaterial.CreateShared();
                    sharedMaterial.name = "M_KillCylinder";
                }
                return sharedMaterial;
            }
        }

        static MaterialPropertyBlock propertyBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static void ApplyColor(Renderer rend, Color c)
        {
            if (rend == null) return;
            if (rend.sharedMaterial == null) rend.sharedMaterial = SharedMaterial;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            propertyBlock.SetColor(ColorId, c);
            rend.SetPropertyBlock(propertyBlock);
        }

        static KillCylinderFx Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "KillCylinder";
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            go.transform.SetParent(root, false);
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = SharedMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // pedido explicito: cilindros sin sombra

            var fx = go.AddComponent<KillCylinderFx>();
            go.SetActive(false);
            all.Add(fx);
            return fx;
        }

        static KillCylinderFx Take()
        {
            ResetIfStale();
            EnsureRoot();
            Purge();

            KillCylinderFx m = null;
            while (free.Count > 0 && m == null) m = free.Dequeue();

            if (m == null)
            {
                if (all.Count < Budget || inUse.Count == 0) m = Create();
                else
                {
                    m = inUse[0];
                    inUse.RemoveAt(0);
                    m.Recycle();
                }
            }

            inUse.Add(m);
            return m;
        }

        static void Release(KillCylinderFx m)
        {
            if (m == null) return;
            inUse.Remove(m);
            if (!free.Contains(m)) free.Enqueue(m);
        }

        public static void Spawn(Vector3 position)
        {
            var m = Take();
            if (m == null) return;
            m.Launch(position);
        }

        public static void Prewarm()
        {
            Spawn(new Vector3(0f, -500f, 0f));
            if (!Application.isPlaying) RecycleAllImmediate();
        }

        static void RecycleAllImmediate()
        {
            Purge();
            for (int i = inUse.Count - 1; i >= 0; i--)
            {
                var m = inUse[i];
                if (m == null) continue;
                m.Recycle();
                if (!free.Contains(m)) free.Enqueue(m);
            }
            inUse.Clear();
        }

        public static void LimpiarTodo()
        {
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null) continue;
                if (Application.isPlaying) Object.Destroy(m.gameObject);
                else Object.DestroyImmediate(m.gameObject);
            }
            all.Clear();
            free.Clear();
            inUse.Clear();
            if (root != null)
            {
                if (Application.isPlaying) Object.Destroy(root.gameObject);
                else Object.DestroyImmediate(root.gameObject);
                root = null;
            }
        }

        // --- Instancia --------------------------------------------------

        float age;
        Vector3 baseScale;
        MeshRenderer cachedRenderer;

        MeshRenderer Rend
        {
            get
            {
                if (cachedRenderer == null) cachedRenderer = GetComponent<MeshRenderer>();
                return cachedRenderer;
            }
        }

        void Launch(Vector3 position)
        {
            gameObject.name = "KillCylinder";
            gameObject.SetActive(true);

            // Un cilindro primitivo mide 2 unidades de alto con localScale
            // Y=1: la mitad de Altura da la escala correcta, y hay que subir
            // el pivote (que esta en el centro) a la mitad de esa altura
            // para que la base quede apoyada en el piso.
            baseScale = new Vector3(Radio, Altura * 0.5f, Radio);
            transform.localScale = baseScale;
            transform.position = new Vector3(position.x, Altura * 0.5f, position.z);

            ApplyColor(Rend, BeaconColor);
            age = 0f;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / Duracion);
            // Se desvanece angostandose (el radio a cero), no bajando la
            // altura: asi se sigue leyendo como un rayo hasta el final en
            // vez de encogerse como una columna que se hunde.
            float radio = Mathf.Lerp(Radio, 0f, k);
            transform.localScale = new Vector3(radio, baseScale.y, radio);
            if (age < Duracion) return;

            Recycle();
            Release(this);
        }

        // No destruye: apaga y deja el cilindro listo para el proximo uso.
        public void Recycle()
        {
            age = 0f;
            gameObject.SetActive(false);
        }
    }
}
