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

        // Bug #068: anillo de DESTINO. Anillo con barrido angular (estela que se apaga alrededor del circulo, una punta brillante y cuatro
        // marcas en el borde) para que al girar se note el giro; un anillo uniforme rotando es invisible. Material UNLIT transparente
        // COMPARTIDO por todos los marcadores: el color y el alfa de cada uno van por MaterialPropertyBlock (no se clona nada).
        static Material matDestino;
        static Texture2D texturaDestino;

        public static Material MaterialDeDestino()
        {
            if (matDestino != null) return matDestino;
            if (texturaDestino == null)
            {
                const int lado = 128;
                var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                var pix = new Color32[lado * lado];
                const float c0 = (lado - 1) * 0.5f;
                for (int y = 0; y < lado; y++)
                {
                    for (int x = 0; x < lado; x++)
                    {
                        float dx = (x - c0) / c0, dy = (y - c0) / c0;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);                       // -pi..pi
                        float frac = Mathf.Repeat(ang, Mathf.PI * 2f) / (Mathf.PI * 2f);   // 0 en la punta brillante, crece en sentido antihorario
                        // Anillo principal, bordes suaves.
                        float anillo = Mathf.Clamp01((d - 0.60f) / 0.05f) * Mathf.Clamp01((0.86f - d) / 0.05f);
                        float estela = Mathf.Lerp(1f, 0.12f, Mathf.Pow(frac, 0.7f));
                        float a = anillo * estela;
                        // Cabeza: punto brillante en la punta del barrido.
                        float hx = (dx - 0.73f), hy = dy;
                        a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.09f));
                        // Cuatro marcas cortas en el borde exterior, cada 90 grados.
                        float cuarto = Mathf.Repeat(ang, Mathf.PI * 0.5f);
                        float distAMarca = Mathf.Min(cuarto, Mathf.PI * 0.5f - cuarto) * d;
                        float marca = Mathf.Clamp01((d - 0.89f) / 0.03f) * Mathf.Clamp01((1.0f - d) / 0.03f) * Mathf.Clamp01(1f - distAMarca / 0.035f);
                        a = Mathf.Max(a, marca * 0.9f);
                        pix[y * lado + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                }
                tex.SetPixels32(pix);
                tex.Apply(true, false);
                texturaDestino = tex;
            }
            matDestino = DiamondGizmo.NuevoMaterialTransparente(Color.white, texturaDestino);
            matDestino.name = "M_AnilloDestino";
            return matDestino;
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
