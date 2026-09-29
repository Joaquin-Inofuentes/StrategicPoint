using UnityEngine;

namespace SP.UI
{
    // Iconos de estado del soldado para el roster (arriba de cada tarjeta): calmado / atacando / interactuando. Formas blancas
    // dibujadas por codigo una sola vez (mascara de alpha suave, sin assets externos); el color lo pone quien use el Image.
    // Se crean en runtime y no se serializan en la escena.
    public enum EstadoDeFila { Calmo, Atacando, Interactuando }

    public static class EstadoIconFactory
    {
        const int Tam = 48;
        static Sprite calmo, atacando, interactuando, disco;

        public static Sprite Para(EstadoDeFila e)
        {
            switch (e)
            {
                case EstadoDeFila.Atacando: if (atacando == null) atacando = Crear(Mira); return atacando;
                case EstadoDeFila.Interactuando: if (interactuando == null) interactuando = Crear(Engranaje); return interactuando;
                default: if (calmo == null) calmo = Crear(Hoja); return calmo;
            }
        }

        // Fondo circular oscuro detras del glifo.
        public static Sprite Disco()
        {
            if (disco == null) disco = Crear((x, y) => Suave(Tam * 0.5f - Mathf.Sqrt(x * x + y * y), 1.2f));
            return disco;
        }

        static float Suave(float distanciaAlBorde, float ancho) => Mathf.Clamp01(distanciaAlBorde / ancho + 0.5f);

        // Coordenadas centradas en el sprite (-24..24).
        static float Mira(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float anillo = Suave(3f - Mathf.Abs(r - 14f), 1.2f);
            float puntoCentral = Suave(3.2f - r, 1.2f);
            float ticks = (Mathf.Abs(x) < 2.2f || Mathf.Abs(y) < 2.2f) && r > 8f && r < 21f ? 1f : 0f;
            return Mathf.Max(anillo, Mathf.Max(puntoCentral, ticks));
        }

        static float Engranaje(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x);
            // 8 dientes: el radio exterior sube y baja con el angulo.
            float diente = Mathf.Sin(ang * 8f) > 0f ? 19f : 15.5f;
            float cuerpo = Suave(diente - r, 1.2f);
            float agujero = Suave(r - 6f, 1.2f);
            return cuerpo * agujero;
        }

        // Calmo: una hoja simple (paz), mas amable que un punto.
        static float Hoja(float x, float y)
        {
            // Elipse girada 45 grados con una nervadura que la parte al medio.
            float u = (x + y) * 0.7071f, v = (x - y) * 0.7071f;
            float elipse = Suave(1f - Mathf.Sqrt(u * u / (19f * 19f) + v * v / (10f * 10f)), 0.09f);
            float nervadura = Mathf.Abs(v) < 1.5f && Mathf.Abs(u) < 17f ? 1f : 0f;
            return Mathf.Clamp01(elipse - nervadura * 0.85f);
        }

        static Sprite Crear(System.Func<float, float, float> f)
        {
            var tex = new Texture2D(Tam, Tam, TextureFormat.RGBA32, false) { name = "EstadoIcon", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pix = new Color32[Tam * Tam];
            for (int j = 0; j < Tam; j++)
                for (int i = 0; i < Tam; i++)
                {
                    float a = Mathf.Clamp01(f(i - Tam * 0.5f + 0.5f, j - Tam * 0.5f + 0.5f));
                    pix[j * Tam + i] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pix);
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, Tam, Tam), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }
    }
}
