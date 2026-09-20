using UnityEngine;
using UnityEngine.Rendering;
using SP.Actors;
using SP.Core;

namespace SP.Presentation
{
    // Holograma de cobertura. Mientras el jugador mantiene [Shift] apuntando a
    // una cobertura se muestra, ESTATICO y 90 % transparente, la misma
    // geometria del aliado que la tomaria -- agachado y apuntando -- parado en
    // el punto exacto, mas un anillo en el piso y una linea desde el aliado.
    // Ademas resalta el obstaculo. Es solo una vista previa: no cambia nada del
    // juego hasta que se aprieta [T].
    public class CoverHologram : MonoBehaviour
    {
        public const float Alpha = 0.10f;   // 90 % transparente
        static readonly Color Celeste = new Color(0.30f, 0.85f, 1f);

        static CoverHologram instance;
        public static CoverHologram Instance => instance;
        public static bool Visible => instance != null && instance.root != null && instance.root.activeSelf;
        public static Vector3 PuntoActual => instance != null ? instance.punto : Vector3.zero;
        public static Soldier ModeloActual => instance != null ? instance.modelo : null;

        GameObject root;          // contenedor (posicion = punto + pivote)
        GameObject ghost;         // clon del "Visual" del aliado, en pose de agachado
        GameObject ring;
        LineRenderer line;
        Soldier modelo;
        Vector3 punto;
        Renderer obstaculoRend;
        Color obstaculoTinte;
        Material ghostMat;
        Material ringMat;
        float t;

        internal static Material NuevoTransparente(Color c)
        {
            var m = SafeMaterial.Create(c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        static CoverHologram Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("CoverHologram");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            instance = go.AddComponent<CoverHologram>();
            instance.Build();
            return instance;
        }

        void Build()
        {
            root = new GameObject("HoloRoot");
            root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root.transform.SetParent(transform, false);
            ghostMat = NuevoTransparente(new Color(0.85f, 1f, 1f, Alpha));
            ringMat = NuevoTransparente(new Color(Celeste.r, Celeste.g, Celeste.b, 0.45f));

            ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var col = ring.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;
            ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ring.transform.SetParent(transform, false);
            ring.transform.localScale = new Vector3(1.6f, 0.015f, 1.6f);

            line = new GameObject("HoloLine").AddComponent<LineRenderer>();
            line.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            line.transform.SetParent(transform, false);
            line.positionCount = 2;
            line.widthMultiplier = 0.06f;
            line.material = NuevoTransparente(new Color(Celeste.r, Celeste.g, Celeste.b, 0.55f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            root.SetActive(false);
            ring.SetActive(false);
            line.gameObject.SetActive(false);
        }

        // Muestra (o mueve) el holograma de 'quien' agachado en 'p', mirando a
        // 'frente' (horizontal). 'obstaculo' es el cubo a resaltar (o null).
        public static void Mostrar(Soldier quien, Vector3 p, Vector3 frente, Transform obstaculo)
        {
            if (quien == null) { Ocultar(); return; }
            var h = Ensure();
            bool eraVisible = Visible;
            bool cambioPunto = !eraVisible || (h.punto - p).sqrMagnitude > 0.01f;
            if (h.modelo != quien || h.ghost == null) h.Reconstruir(quien);

            h.punto = p;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
            float pivote = h.AlturaDelPivote(quien);
            h.root.transform.SetPositionAndRotation(p + Vector3.up * pivote, Quaternion.LookRotation(frente.normalized, Vector3.up));
            h.root.SetActive(true);

            h.ring.transform.position = p + Vector3.up * 0.03f;
            h.ring.SetActive(true);

            h.line.gameObject.SetActive(true);
            h.line.SetPosition(0, quien.transform.position + Vector3.up * 0.1f);
            h.line.SetPosition(1, p + Vector3.up * 0.1f);

            h.Resaltar(obstaculo);
            if (cambioPunto && Application.isPlaying)
                AudioDirector.PlayUi2D(SfxKind.HoloOn, 0.35f, 0.5f);
        }

        // Solo el resaltado del obstaculo (sin holograma): feedback de cobertura al apuntarla en RTS.
        public static void ResaltarSolo(Transform obstaculo) => Ensure().Resaltar(obstaculo);

        public static void Ocultar()
        {
            if (instance == null) return;
            instance.Resaltar(null);
            if (instance.root != null) instance.root.SetActive(false);
            if (instance.ring != null) instance.ring.SetActive(false);
            if (instance.line != null) instance.line.gameObject.SetActive(false);
        }

        float AlturaDelPivote(Soldier s)
        {
            var col = s.GetComponent<Collider>();
            return col != null ? Mathf.Max(0.1f, s.transform.position.y - col.bounds.min.y) : 0.8f;
        }

        // Clona el "Visual" del aliado (malla + esqueleto), le saca el Animator
        // y lo deja en la primera pose del clip "agachado apuntando".
        void Reconstruir(Soldier quien)
        {
            if (ghost != null) { if (Application.isPlaying) Destroy(ghost); else DestroyImmediate(ghost); ghost = null; }
            modelo = quien;

            var visual = quien.transform.Find("Visual");
            if (visual == null) return;
            var animator = visual.GetComponent<Animator>();

            ghost = Instantiate(visual.gameObject, root.transform);
            ghost.name = "HoloVisual";
            ghost.transform.localPosition = visual.localPosition;
            ghost.transform.localRotation = visual.localRotation;
            ghost.transform.localScale = visual.localScale;

            var anim = ghost.GetComponent<Animator>();
            if (anim != null) { if (Application.isPlaying) Destroy(anim); else DestroyImmediate(anim); }
            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) { if (Application.isPlaying) Destroy(mb); else DestroyImmediate(mb); }

            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = new Material[] { ghostMat };
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var sk = r as SkinnedMeshRenderer;
                if (sk != null) sk.updateWhenOffscreen = true;
            }

            // Pose de agachado apuntando (la misma animacion del juego).
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && clip.name == "idle crouching aiming")
                    {
                        clip.SampleAnimation(ghost, 0f);
                        break;
                    }
                }
            }
        }

        void Resaltar(Transform obstaculo)
        {
            Renderer nuevo = obstaculo != null ? obstaculo.GetComponentInChildren<Renderer>() : null;
            if (nuevo == obstaculoRend) return;
            if (obstaculoRend != null) CubeFxReactor.WriteTint(obstaculoRend, obstaculoTinte);
            obstaculoRend = nuevo;
            if (nuevo != null)
            {
                obstaculoTinte = CubeFxReactor.ReadTint(nuevo);
                CubeFxReactor.WriteTint(nuevo, Color.Lerp(obstaculoTinte, new Color(1f, 0.92f, 0.35f), 0.55f));
            }
        }

        void Update()
        {
            if (root == null || !root.activeSelf) return;
            t += Time.deltaTime;
            float k = 1.6f + Mathf.Sin(t * 5f) * 0.18f;
            if (ring != null) ring.transform.localScale = new Vector3(k, 0.015f, k);
        }

        void OnDestroy()
        {
            if (obstaculoRend != null) CubeFxReactor.WriteTint(obstaculoRend, obstaculoTinte);
            if (instance == this) instance = null;
        }
    }
}
