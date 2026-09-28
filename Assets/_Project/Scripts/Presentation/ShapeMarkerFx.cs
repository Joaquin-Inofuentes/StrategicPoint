using UnityEngine;
using UnityEngine.Rendering;

namespace SP.Presentation
{
    public static class ShapeMarkerFx
    {
        static Material matCirculo;

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

        // Pedido explicito: "elimina la Flecha, el triangulo que aparece
        // arriba -- que jamas se genere". El marcador de objetivo queda
        // solo con el anillo del piso.
        public static GameObject CrearMarcador(Color c, float radio)
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

            return root;
        }
    }
}
