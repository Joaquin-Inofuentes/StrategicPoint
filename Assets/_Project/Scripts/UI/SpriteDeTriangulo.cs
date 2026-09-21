using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Ronda 12: las flechas del HUD (aliados fuera de encuadre, bajas) eran rectangulos lisos de 16x22. Deben ser triangulos.
    // Un solo sprite triangular (punta hacia arriba, borde suavizado con supermuestreo) armado por codigo y compartido.
    public static class SpriteTriangulo
    {
        static Sprite cache;

        public static Sprite Obtener()
        {
            if (cache != null) return cache;
            const int lado = 64;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { name = "TrianguloHud", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            const float ax = 0f, ay = 0.95f, bx = -0.7f, by = -0.85f, cx = 0.7f, cy = -0.85f;   // punta arriba, base abajo
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    int dentro = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            float nx = (x + (sx + 0.5f) / 3f) / lado * 2f - 1f;
                            float ny = (y + (sy + 0.5f) / 3f) / lado * 2f - 1f;
                            if (Dentro(nx, ny, ax, ay, bx, by, cx, cy)) dentro++;
                        }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, dentro / 9f));
                }
            tex.Apply(false, false);
            cache = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f);
            cache.name = "TrianguloHud";
            return cache;
        }

        // Deja la Image como triangulo (sin estirarlo): se llama cada vez, es barato.
        public static void Aplicar(Image img)
        {
            if (img == null || img.sprite != null && img.sprite.name == "TrianguloHud") return;
            img.sprite = Obtener();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            var rt = img.rectTransform;
            if (rt.sizeDelta.x < 24f) rt.sizeDelta = new Vector2(26f, 26f);
        }

        static bool Dentro(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Signo(px, py, ax, ay, bx, by), d2 = Signo(px, py, bx, by, cx, cy), d3 = Signo(px, py, cx, cy, ax, ay);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
        static float Signo(float x1, float y1, float x2, float y2, float x3, float y3) => (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);
    }
}
