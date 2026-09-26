using UnityEngine;
using UnityEngine.Rendering;

namespace SP.Presentation
{
    public static class ShapeMarkerFx
    {
        static Material matCirculo;
        static Mesh meshFlecha;

        public static Material MatCirculo(Color c)
        {
            if (matCirculo == null)
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        float dx = (x - 31.5f) / 31.5f;
                        float dy = (y - 31.5f) / 31.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = 0f;
                        if (d >= 0.6f && d <= 1f) {
                            if (d < 0.8f) a = Mathf.InverseLerp(0.6f, 0.8f, d);
                            else a = Mathf.InverseLerp(1f, 0.8f, d);
                        }
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                matCirculo = CoverHologram.NuevoTransparente(Color.white);
                matCirculo.mainTexture = tex;
            }
            var clon = Object.Instantiate(matCirculo);
            clon.color = c;
            return clon;
        }

        public static Mesh MeshFlechaAbajo()
        {
            if (meshFlecha == null)
            {
                meshFlecha = new Mesh { name = "FlechaAbajo" };
                meshFlecha.vertices = new[] {
                    new Vector3(-0.4f, 0.6f, 0f),
                    new Vector3(0.4f, 0.6f, 0f),
                    new Vector3(0f, 0f, 0f)
                };
                meshFlecha.triangles = new[] { 0, 1, 2, 0, 2, 1 };
                meshFlecha.RecalculateNormals();
            }
            return meshFlecha;
        }

        public static GameObject CrearMarcador(Color c, float radio, float escalaFlecha, float alturaFlecha = 1.2f)
        {
            var root = new GameObject("MarcadorObjetivo");
            var circulo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(circulo.GetComponent<Collider>());
            circulo.transform.SetParent(root.transform, false);
            circulo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            circulo.transform.localScale = new Vector3(radio * 2f, radio * 2f, 1f);
            var mc = circulo.GetComponent<MeshRenderer>();
            mc.sharedMaterial = MatCirculo(c);
            mc.shadowCastingMode = ShadowCastingMode.Off;

            var flecha = new GameObject("Flecha");
            flecha.transform.SetParent(root.transform, false);
            flecha.transform.localPosition = new Vector3(0f, alturaFlecha, 0f);
            flecha.transform.localScale = new Vector3(escalaFlecha, escalaFlecha, escalaFlecha);
            var mf = flecha.AddComponent<MeshFilter>();
            mf.sharedMesh = MeshFlechaAbajo();
            var mrf = flecha.AddComponent<MeshRenderer>();
            var matF = CoverHologram.NuevoTransparente(c);
            mrf.sharedMaterial = matF;
            mrf.shadowCastingMode = ShadowCastingMode.Off;
            
            flecha.AddComponent<AnimadorFlecha>();

            return root;
        }
    }

    public class AnimadorFlecha : MonoBehaviour
    {
        Vector3 basePos;
        void Start() { basePos = transform.localPosition; }
        void Update()
        {
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 6f) * 0.15f);
            var cam = Camera.main;
            if (cam != null)
            {
                var dir = cam.transform.position - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(-dir);
            }
        }
    }
}
