using UnityEngine;

namespace SP.Presentation
{
    // Iconos chicos para el HUD de abajo a la derecha (cuchillo/granada/
    // recarga). Pedido explicito: "q use iconos... intenta evitar textos".
    // Mismo criterio que RoleIconFactory: formas armadas a mano en una
    // textura Alpha8, sin bajar ningun asset de internet. El color lo pone
    // quien use el Image.
    public static class HudIconFactory
    {
        const int Size = 64;

        static Texture2D cuchillo, granada, reloj;
        static Sprite spriteCuchillo, spriteGranada, spriteReloj;

        public static Sprite Cuchillo() => spriteCuchillo ??= AsSprite(cuchillo ??= BuildCuchillo());
        public static Sprite Granada() => spriteGranada ??= AsSprite(granada ??= BuildGranada());
        public static Sprite Reloj() => spriteReloj ??= AsSprite(reloj ??= BuildReloj());

        static Sprite AsSprite(Texture2D tex)
            => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        static Texture2D NuevaTextura() => new Texture2D(Size, Size, TextureFormat.Alpha8, false) { name = "HudIcon", hideFlags = HideFlags.HideAndDontSave };

        // Daga vertical: hoja (triangulo + vastago fino) + guarda horizontal + mango corto.
        static Texture2D BuildCuchillo()
        {
            var tex = NuevaTextura();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                bool puntaHoja = ny > 0.35f && ny <= 0.9f && Mathf.Abs(nx) <= Mathf.Lerp(0.16f, 0.01f, Mathf.InverseLerp(0.35f, 0.9f, ny));
                bool vastago = ny > -0.05f && ny <= 0.35f && Mathf.Abs(nx) <= 0.09f;
                bool guarda = ny > -0.16f && ny <= -0.05f && Mathf.Abs(nx) <= 0.32f;
                bool mango = ny > -0.55f && ny <= -0.16f && Mathf.Abs(nx) <= 0.11f;
                bool dentro = puntaHoja || vastago || guarda || mango;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, dentro ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        // Granada: cuerpo ovalado con costillas horizontales + palanca + anillo del seguro.
        static Texture2D BuildGranada()
        {
            var tex = NuevaTextura();
            const float rx = 0.42f, ry = 0.52f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f - 0.05f; // cuerpo centrado un poco abajo
                bool cuerpo = (nx * nx) / (rx * rx) + (ny * ny) / (ry * ry) <= 1f;
                // Costillas: franjas horizontales finas dentro del cuerpo (textura de granada).
                bool costilla = cuerpo && (Mathf.Abs(ny - 0.15f) < 0.045f || Mathf.Abs(ny - -0.15f) < 0.045f);
                float nyPalanca = (y + 0.5f) / Size * 2f - 1f;
                bool palanca = nyPalanca > 0.55f && nyPalanca <= 0.8f && nx > 0.02f && nx <= 0.24f;
                bool anillo = false;
                float dxAnillo = nx - 0.13f, dyAnillo = nyPalanca - 0.88f;
                float dAnillo = Mathf.Sqrt(dxAnillo * dxAnillo + dyAnillo * dyAnillo);
                if (dAnillo <= 0.12f && dAnillo >= 0.07f) anillo = true;
                bool dentro = (cuerpo && !costilla) || palanca || anillo;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, dentro ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        // Reloj de arena: dos triangulos punta a punta -- feedback de "esperando/recargando".
        static Texture2D BuildReloj()
        {
            var tex = NuevaTextura();
            const float mitadAncho = 0.62f, mitadAlto = 0.78f, grosorMarco = 0.09f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                bool marcoArriba = ny > mitadAlto - grosorMarco && ny <= mitadAlto && Mathf.Abs(nx) <= mitadAncho;
                bool marcoAbajo = ny < -(mitadAlto - grosorMarco) && ny >= -mitadAlto && Mathf.Abs(nx) <= mitadAncho;
                float t = Mathf.InverseLerp(mitadAlto - grosorMarco, 0f, Mathf.Abs(ny));
                bool cuerpo = Mathf.Abs(ny) <= mitadAlto - grosorMarco && Mathf.Abs(nx) <= Mathf.Lerp(0.06f, mitadAncho - grosorMarco, t);
                bool dentro = marcoArriba || marcoAbajo || cuerpo;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, dentro ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }
    }
}
