using System.Collections.Generic;
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

        static Texture2D cruz, mira, flecha, civil, generico;
        static Sprite spriteCruz, spriteMira, spriteFlecha, spriteCivil, spriteGenerico;

        public static Sprite For(RoleType role) => role switch
        {
            RoleType.Medic => spriteCruz ??= AsSprite(cruz ??= BuildCruz()),
            RoleType.Sniper => spriteMira ??= AsSprite(mira ??= BuildMira()),
            RoleType.Assault => spriteFlecha ??= AsSprite(flecha ??= BuildFlecha()),
            // Pedido explicito: "que aparezca... con icono de civil" -- antes
            // un civil caia en el mismo circulo generico que cualquier rol
            // sin icono propio (Flanker, etc.), sin distinguirse de un rol
            // "sin clasificar". Silueta simple de cabeza + torso, propia.
            RoleType.Civilian => spriteCivil ??= AsSprite(civil ??= BuildCivil()),
            _ => spriteGenerico ??= AsSprite(generico ??= BuildGenerico()),
        };

        static Sprite AsSprite(Texture2D tex)
            => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        // Pedido explicito: "los rombos dentro de los aliados tengan iconos
        // segun su especialidad, y lo mismo para enemigos". Estos iconos son
        // Alpha8 (sin color propio, ver el comentario de arriba): perfectos
        // para un Image de UI (que los usa como mascara tenida por
        // Image.color) pero un material Unlit de mundo sobre una malla NO
        // hace ese tratamiento especial -- leeria RGB=0 y el icono saldria
        // negro/invisible sin importar el tinte del material. Esta variante
        // vuelca la MISMA mascara de alpha a una textura RGBA32 blanca real,
        // que un material comun si puede tenir como corresponde (mismo
        // truco que TexturaEngranaje en DiamondGizmo).
        static readonly Dictionary<RoleType, Texture2D> worldIconCache = new Dictionary<RoleType, Texture2D>();

        public static Texture2D WorldIconTexture(RoleType role)
        {
            if (worldIconCache.TryGetValue(role, out var cached) && cached != null) return cached;
            var origen = For(role).texture;
            int w = origen.width, h = origen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "WorldIcon_" + role, hideFlags = HideFlags.HideAndDontSave };
            var pix = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = (byte)(origen.GetPixel(x, y).a * 255f);
                pix[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pix);
            tex.Apply(false, false);
            worldIconCache[role] = tex;
            return tex;
        }

        // Los enemigos no traen un RoleType propio por especialidad (todos
        // comparten TeamId.Enemy) -- la variedad real esta en el arma que
        // llevan (SoldierClasses.Enemigos). Se aproxima el icono al arma:
        // mira de francotirador para Sniper, flecha para el resto (fusil,
        // pesada, metralleta).
        public static Texture2D WorldIconTextureForWeapon(WeaponKind kind)
            => WorldIconTexture(kind == WeaponKind.Sniper ? RoleType.Sniper : RoleType.Assault);

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

        // Civil: silueta simple de cabeza (circulo) + torso (trapecio), para
        // distinguirse de un rol de combate sin necesitar ningun asset externo.
        static Texture2D BuildCivil()
        {
            var tex = NuevaTextura();
            const float radioCabeza = 0.30f, centroCabezaY = 0.42f;
            const float anchoHombros = 0.7f, anchoCintura = 0.42f, topeTorsoY = 0.12f, baseTorsoY = -0.85f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                float dCabeza = Mathf.Sqrt(nx * nx + (ny - centroCabezaY) * (ny - centroCabezaY));
                bool cabeza = dCabeza <= radioCabeza;
                bool torso = false;
                if (ny <= topeTorsoY && ny >= baseTorsoY)
                {
                    float t = Mathf.InverseLerp(topeTorsoY, baseTorsoY, ny);
                    float anchoEn = Mathf.Lerp(anchoHombros, anchoCintura, t) * 0.5f;
                    torso = Mathf.Abs(nx) <= anchoEn;
                }
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, (cabeza || torso) ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }

        // Fallback (rol sin icono propio): circulo simple.
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
