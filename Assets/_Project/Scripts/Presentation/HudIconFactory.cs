using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Iconos chicos para el HUD de abajo a la derecha (cuchillo/granada/
    // recarga). Pedido explicito: "q use iconos... intenta evitar textos".
    // Mismo criterio que RoleIconFactory: formas armadas a mano en una
    // textura Alpha8, sin bajar ningun asset de internet. El color lo pone
    // quien use el Image.
    public static class HudIconFactory
    {
        const int Size = 96;

        static Texture2D cuchillo, granada, reloj, escudo;
        static Sprite spriteCuchillo, spriteGranada, spriteReloj, spriteEscudo;

        // Los iconos viven como PNG en Resources/UI/HudIcons (asi la escena guarda una referencia a un asset real: antes las
        // Image de abajo a la derecha se serializaban con un Sprite generado por codigo, sin textura al volver a abrir la escena,
        // y se dibujaban como ovalos punteados). Si falta el PNG se genera por codigo como respaldo.
        const string CarpetaIconos = "UI/HudIcons/";
        static Sprite Cargar(string nombre) => RecursosCache.Cargar<Sprite>(CarpetaIconos + nombre);

        public static Sprite Cuchillo() => Elegir(ref spriteCuchillo, "Icono_Cuchillo", () => AsSprite(cuchillo != null ? cuchillo : (cuchillo = BuildCuchillo()), "HudIcon_Cuchillo"));
        public static Sprite Granada() => Elegir(ref spriteGranada, "Icono_Granada", () => AsSprite(granada != null ? granada : (granada = BuildGranada()), "HudIcon_Granada"));
        public static Sprite Reloj() => Elegir(ref spriteReloj, "Icono_Reloj", () => AsSprite(reloj != null ? reloj : (reloj = BuildReloj()), "HudIcon_Reloj"));
        public static Sprite Escudo() => Elegir(ref spriteEscudo, "Icono_Escudo", () => AsSprite(escudo != null ? escudo : (escudo = BuildEscudo()), "HudIcon_Escudo"));

        // Comparacion estilo Unity (un Sprite destruido cuenta como nulo) y no `??=`: con el dominio sin recargar el cache
        // podia guardar un Sprite muerto para siempre.
        static Sprite Elegir(ref Sprite cache, string archivo, System.Func<Sprite> generar)
        {
            if (cache != null) return cache;
            cache = Cargar(archivo);
            if (cache == null) cache = generar();
            return cache;
        }

        // Escribe los cuatro iconos como PNG (menu del editor / pruebas). Devuelve la lista de rutas escritas.
        public static Texture2D TexturaGenerada(string nombre) => nombre switch
        {
            "Icono_Cuchillo" => BuildCuchillo(),
            "Icono_Granada" => BuildGranada(),
            "Icono_Reloj" => BuildReloj(),
            _ => BuildEscudo(),
        };

        static Sprite AsSprite(Texture2D tex, string spriteName = "HudIcon")
        {
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = spriteName;
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Texture2D NuevaTextura() => new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "HudIcon", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        // Pulido de UI: los dibujos anteriores (daga vertical de 5 px y granada con costillas) se veian como ovalos
        // punteados a 30 px. Ahora se dibujan con supermuestreo 3x3 (bordes suaves) y formas mas gruesas y reconocibles.
        static Texture2D Dibujar(System.Func<float, float, bool> dentro)
        {
            var tex = NuevaTextura();
            const int ss = 3;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int n = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float nx = (x + (sx + 0.5f) / ss) / Size * 2f - 1f;
                    float ny = (y + (sy + 0.5f) / ss) / Size * 2f - 1f;
                    if (dentro(nx, ny)) n++;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, n / (float)(ss * ss)));
            }
            tex.Apply();
            return tex;
        }

        // Cuchillo de combate en diagonal (punta arriba a la derecha): hoja ancha con punta curva, guarda y mango.
        static Texture2D BuildCuchillo()
        {
            const float c = 0.7071f;
            return Dibujar((nx, ny) =>
            {
                float u = nx * c + ny * c;    // a lo largo de la hoja
                float v = -nx * c + ny * c;   // perpendicular
                if (u < -0.98f || u > 0.98f) return false;
                if (u < -0.42f) return Mathf.Abs(v) <= 0.15f && u >= -0.95f;   // mango
                if (u < -0.30f) return Mathf.Abs(v) <= 0.34f;                  // guarda
                float w = u < 0.4f ? 0.24f : 0.24f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((u - 0.4f) / 0.58f, 2f)));
                return v >= -w && v <= w * 0.55f + 0.02f;                      // hoja: filo abajo, lomo recto arriba
            });
        }

        // Granada de fragmentacion: cuerpo con cuadricula tallada, cuello, palanca y anillo del seguro.
        static Texture2D BuildGranada()
        {
            return Dibujar((nx, ny) =>
            {
                float by = ny + 0.2f;
                bool cuerpo = (nx * nx) / (0.5f * 0.5f) + (by * by) / (0.55f * 0.55f) <= 1f;
                if (cuerpo)
                {
                    // Cuadricula: dos familias de lineas diagonales finas talladas en el cuerpo.
                    float d1 = Mathf.Abs(Mathf.Repeat((nx + by) * 3.4f, 1f) - 0.5f);
                    float d2 = Mathf.Abs(Mathf.Repeat((nx - by) * 3.4f, 1f) - 0.5f);
                    if (d1 > 0.44f || d2 > 0.44f) return false;
                    return true;
                }
                bool cuello = Mathf.Abs(nx) <= 0.17f && ny > 0.3f && ny <= 0.58f;
                bool palanca = nx > 0.14f && nx <= 0.34f && ny > 0.46f && ny <= 0.6f;
                float dx = nx + 0.3f, dy = ny - 0.68f;
                float dr = Mathf.Sqrt(dx * dx + dy * dy);
                bool anillo = dr <= 0.2f && dr >= 0.12f;
                return cuello || palanca || anillo;
            });
        }

        // Escudo: forma clasica de blason (punta abajo) con una marca en V adentro -- identifica
        // el cartel de MODO DIOS (invencibilidad) de un vistazo, sin depender solo del color dorado.
        static Texture2D BuildEscudo()
        {
            var tex = NuevaTextura();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                // Contorno del blason: rectangulo arriba, se angosta a una punta abajo.
                bool enFormaBase;
                if (ny > -0.1f) enFormaBase = ny <= 0.85f && Mathf.Abs(nx) <= 0.62f;
                else enFormaBase = ny > -0.85f && Mathf.Abs(nx) <= Mathf.Lerp(0.62f, 0f, Mathf.InverseLerp(-0.1f, -0.85f, ny));
                // Se resta un anillo interior (mismo contorno, mas chico) para que quede hueco
                // y una marca en V (tilde) al medio, como el resto de los iconos de esta clase.
                bool enFormaInterior;
                if (ny > -0.02f) enFormaInterior = ny <= 0.68f && Mathf.Abs(nx) <= 0.46f;
                else enFormaInterior = ny > -0.68f && Mathf.Abs(nx) <= Mathf.Lerp(0.46f, 0f, Mathf.InverseLerp(-0.02f, -0.68f, ny));
                bool marco = enFormaBase && !enFormaInterior;

                bool tildeIzq = (nx + ny * 0.4f) is > -0.42f and < -0.1f && ny < 0.15f && ny > -0.35f && nx < 0.02f;
                bool tildeDer = (nx - ny * 0.9f) is > -0.1f and < 0.2f && ny < 0.35f && ny > -0.35f && nx >= -0.05f;
                bool tilde = enFormaInterior && (tildeIzq || tildeDer);

                bool dentro = marco || tilde;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, dentro ? 1f : 0f));
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
                bool cuerpo = Mathf.Abs(ny) <= mitadAlto - grosorMarco && Mathf.Abs(nx) <= Mathf.Lerp(mitadAncho - grosorMarco, 0.06f, t);
                bool dentro = marcoArriba || marcoAbajo || cuerpo;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, dentro ? 1f : 0f));
            }
            tex.Apply();
            return tex;
        }
    }
}
