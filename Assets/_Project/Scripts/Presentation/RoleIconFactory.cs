using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // Iconos chicos para el roster (abajo a la izquierda). Pedido
    // explicito: "mas simplificado con iconos simples que descargues de
    // internet o crealos" -- se optó por crearlos a mano en vez de bajar
    // archivos de terceros (mismo criterio que ya usan DamageDirectionView
    // y DiamondGizmo: una textura Alpha8 armada pixel a pixel una sola vez
    // y cacheada, sin depender de ningun asset externo). El color lo pone
    // quien use el Image (Image.color), la textura es solo la forma.
    public static class RoleIconFactory
    {
        const int Size = 64;

        static Texture2D cruz, mira, flecha, generico;
        static Sprite spriteCruz, spriteMira, spriteFlecha, spriteGenerico;

        public static Sprite For(RoleType role) => role switch
        {
            RoleType.Medic => spriteCruz ??= AsSprite(cruz ??= BuildCruz()),
            RoleType.Sniper => spriteMira ??= AsSprite(mira ??= BuildMira()),
            RoleType.Assault => spriteFlecha ??= AsSprite(flecha ??= BuildFlecha()),
            _ => spriteGenerico ??= AsSprite(generico ??= BuildGenerico()),
        };

        static Sprite AsSprite(Texture2D tex)
            => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        static Texture2D NuevaTextura() => new Texture2D(Size, Size, TextureFormat.Alpha8, false) { name = "RoleIcon", hideFlags = HideFlags.HideAndDontSave };

        // Cruz medica: dos barras (vertical + horizontal) del mismo ancho.
        static Texture2D BuildCruz()
        {
            var tex = NuevaTextura();
            const float mitadAncho = 0.22f, mitadLargo = 0.75f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                bool barraVertical = Mathf.Abs(nx) <= mitadAncho && Mathf.Abs(ny) <= mitadLargo;
                bool barraHorizontal = Mathf.Abs(ny) <= mitadAncho && Mathf.Abs(nx) <= mitadLargo;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, (barraVertical || barraHorizontal) ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        // Mira de francotirador: anillo + cruz fina (reticula de scope) y un punto central.
        static Texture2D BuildMira()
        {
            var tex = NuevaTextura();
            const float radioExterior = 0.85f, radioInterior = 0.68f;
            const float grosorLinea = 0.06f, huecoCentral = 0.22f;
            const float radioPunto = 0.09f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                bool anillo = dist <= radioExterior && dist >= radioInterior;
                bool lineaH = Mathf.Abs(ny) <= grosorLinea && Mathf.Abs(nx) >= huecoCentral && Mathf.Abs(nx) <= radioExterior;
                bool lineaV = Mathf.Abs(nx) <= grosorLinea && Mathf.Abs(ny) >= huecoCentral && Mathf.Abs(ny) <= radioExterior;
                bool punto = dist <= radioPunto;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, (anillo || lineaH || lineaV || punto) ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        // Asalto: flecha/triangulo solido apuntando hacia arriba.
        static Texture2D BuildFlecha()
        {
            var tex = NuevaTextura();
            const float ax = 0f, ay = 0.85f;
            const float bx = -0.75f, by = -0.65f;
            const float cx = 0.75f, cy = -0.65f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                bool inside = PointInTriangle(nx, ny, ax, ay, bx, by, cx, cy);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, inside ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        // Fallback (civil / rol sin icono propio): circulo simple.
        static Texture2D BuildGenerico()
        {
            var tex = NuevaTextura();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                bool inside = (nx * nx + ny * ny) <= 0.75f * 0.75f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, inside ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        static bool PointInTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Sign(px, py, ax, ay, bx, by);
            float d2 = Sign(px, py, bx, by, cx, cy);
            float d3 = Sign(px, py, cx, cy, ax, ay);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        static float Sign(float x1, float y1, float x2, float y2, float x3, float y3)
            => (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);
    }
}
