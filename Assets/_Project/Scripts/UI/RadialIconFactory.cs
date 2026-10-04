using UnityEngine;

namespace SP.UI
{
    // Pedido explicito: "para el radial, para todas las opciones, quiero
    // texto chico e iconos grandes, descriptivos y claros". El menu de
    // ordenes ([Q] sostenido) no tenia NINGUN icono -- cada porcion solo
    // mostraba texto. Un icono simple por CATEGORIA (mismo criterio que
    // RoleIconFactory: formas dibujadas a mano, Alpha8, sin depender de
    // ningun asset externo) deja leer de un vistazo que hace cada porcion
    // del anillo interior, mientras el texto se reduce a una etiqueta chica
    // de apoyo (ver MenuDeOrdenes.TamanoLetra).
    public static class RadialIconFactory
    {
        const int Size = 64;

        static Texture2D irAlli, cubrirse, atacar, posicion, curar, tanque, poseer, demoler, torreta, calavera, engranaje;
        static Sprite spriteIrAlli, spriteCubrirse, spriteAtacar, spritePosicion, spriteCurar, spriteTanque, spritePoseer, spriteDemoler, spriteTorreta, spriteCalavera, spriteEngranaje;

        // Iconos de OPCION (anillo exterior): mas chicos y genericos que los de
        // categoria, reutilizados entre varias opciones semanticamente parecidas
        // ("TODOS" en Ir alli/Cubrirse/Atacar, "SOLO N" en varias categorias...).
        static Texture2D grupo, corazon, flechaArriba, lineaFormacion, cunaFormacion, granada, siguiente, equis;
        static Sprite spriteGrupo, spriteCorazon, spriteFlechaArriba, spriteLinea, spriteCuna, spriteGranada, spriteSiguiente, spriteEquis;

        public static Sprite Calavera() => spriteCalavera ??= AsSprite(calavera ??= BuildCalavera());

        // Mismo orden que MenuDeOrdenes.Porciones / los ids IrAlli..Torreta.
        public static Sprite ForCategoria(int categoria) => categoria switch
        {
            MenuDeOrdenes.IrAlli => spriteIrAlli ??= AsSprite(irAlli ??= BuildFlechaAbajo()),
            MenuDeOrdenes.Cubrirse => spriteCubrirse ??= AsSprite(cubrirse ??= BuildEscudo()),
            MenuDeOrdenes.Atacar => spriteAtacar ??= AsSprite(atacar ??= BuildMira()),
            MenuDeOrdenes.Posicion => spritePosicion ??= AsSprite(posicion ??= BuildBandera()),
            MenuDeOrdenes.Curar => spriteCurar ??= AsSprite(curar ??= BuildCruz()),
            MenuDeOrdenes.Tanque => spriteTanque ??= AsSprite(tanque ??= BuildTanque()),
            MenuDeOrdenes.Poseer => spritePoseer ??= AsSprite(poseer ??= BuildPersona()),
            MenuDeOrdenes.Demoler => spriteDemoler ??= AsSprite(demoler ??= BuildEstallido()),
            MenuDeOrdenes.Torreta => spriteTorreta ??= AsSprite(torreta ??= BuildTorreta()),
            MenuDeOrdenes.Interactuar => spriteEngranaje ??= AsSprite(engranaje ??= BuildEngranaje()),
            _ => spriteIrAlli ??= AsSprite(irAlli ??= BuildFlechaAbajo()),
        };

        // Icono de cada OPCION del anillo exterior (sub real, indice de OpcionesDe).
        // Pedido explicito: "para todas las opciones... iconos grandes descriptivos
        // y claros" -- el anillo interior (categorias) ya los tenia, el exterior
        // (opciones) se habia quedado solo con texto. Se cubren las ~30 opciones
        // reutilizando un puñado de formas ya existentes o nuevas segun la accion.
        public static Sprite ForOpcion(int categoria, int sub)
        {
            var g = MenuDeOrdenes.Porciones;
            switch (categoria)
            {
                case MenuDeOrdenes.IrAlli:
                case MenuDeOrdenes.Cubrirse:
                    return sub == 0 ? Grupo() : Persona();
                case MenuDeOrdenes.Atacar:
                    if (sub == 0) return Grupo();
                    if (sub == MenuDeOrdenes.SubSuprimir) return AsSprite(demoler ??= BuildEstallido());
                    if (sub == MenuDeOrdenes.SubGranada) return Granada();
                    return Persona();
                case MenuDeOrdenes.Posicion:
                    switch (sub)
                    {
                        case 0: return Grupo();
                        case 1: return AsSprite(irAlli ??= BuildFlechaAbajo());
                        case 2: return Linea();
                        case 3: return Cuna();
                        default: return FlechaArriba();
                    }
                case MenuDeOrdenes.Curar:
                    return sub == 3 ? Corazon() : AsSprite(curar ??= BuildCruz());
                case MenuDeOrdenes.Tanque:
                    switch (sub)
                    {
                        case 0: return FlechaArriba();
                        case 1: return AsSprite(irAlli ??= BuildFlechaAbajo());
                        case 2: return AsSprite(tanque ??= BuildTanque());
                        default: return Persona();
                    }
                case MenuDeOrdenes.Poseer:
                    return sub == 3 ? Siguiente() : Persona();
                case MenuDeOrdenes.Demoler:
                    if (sub == 0) return AsSprite(demoler ??= BuildEstallido());
                    if (sub == 2) return Equis();
                    return Persona();
                case MenuDeOrdenes.Interactuar:
                    return sub == 0 ? AsSprite(engranaje ??= BuildEngranaje()) : Persona();
                case MenuDeOrdenes.Torreta:
                    if (sub == 0) return AsSprite(torreta ??= BuildTorreta());
                    if (sub == 1) return Equis();
                    return Persona();
                default:
                    return Persona();
            }
        }

        static Sprite Grupo() => spriteGrupo ??= AsSprite(grupo ??= BuildGrupo());
        static Sprite Persona() => spritePoseer ??= AsSprite(poseer ??= BuildPersona());
        static Sprite Corazon() => spriteCorazon ??= AsSprite(corazon ??= BuildCorazon());
        static Sprite FlechaArriba() => spriteFlechaArriba ??= AsSprite(flechaArriba ??= BuildFlechaArriba());
        static Sprite Linea() => spriteLinea ??= AsSprite(lineaFormacion ??= BuildLinea());
        static Sprite Cuna() => spriteCuna ??= AsSprite(cunaFormacion ??= BuildCuna());
        static Sprite Granada() => spriteGranada ??= AsSprite(granada ??= BuildGranada());
        static Sprite Siguiente() => spriteSiguiente ??= AsSprite(siguiente ??= BuildSiguiente());
        static Sprite Equis() => spriteEquis ??= AsSprite(equis ??= BuildEquis());

        static Sprite AsSprite(Texture2D tex) => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        static Texture2D NuevaTextura() => new Texture2D(Size, Size, TextureFormat.Alpha8, false) { name = "RadialIcon", hideFlags = HideFlags.HideAndDontSave };

        static void Rellenar(Texture2D tex, System.Func<float, float, bool> dentro)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float nx = (x + 0.5f) / Size * 2f - 1f;
                float ny = (y + 0.5f) / Size * 2f - 1f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, dentro(nx, ny) ? 1f : 0f));
            }
            tex.Apply();
        }

        static bool EnTriangulo(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Signo(px, py, ax, ay, bx, by);
            float d2 = Signo(px, py, bx, by, cx, cy);
            float d3 = Signo(px, py, cx, cy, ax, ay);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        static float Signo(float x1, float y1, float x2, float y2, float x3, float y3)
            => (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);

        // IR ALLI: flecha solida apuntando hacia abajo (moverse hacia un punto en el piso).
        static Texture2D BuildFlechaAbajo()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool cabeza = EnTriangulo(nx, ny, 0f, -0.85f, -0.55f, 0.05f, 0.55f, 0.05f);
                bool cola = Mathf.Abs(nx) <= 0.2f && ny >= 0.0f && ny <= 0.75f;
                return cabeza || cola;
            });
            return tex;
        }

        // CUBRIRSE: escudo (pentagono redondeado) solido.
        static Texture2D BuildEscudo()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                float anchoTope = 0.72f;
                if (ny < -0.8f || ny > 0.75f) return false;
                if (ny > 0.35f)
                {
                    float t = Mathf.InverseLerp(0.75f, 0.35f, ny);
                    return Mathf.Abs(nx) <= anchoTope * t;
                }
                float k = Mathf.InverseLerp(0.35f, -0.8f, ny);
                float ancho = Mathf.Lerp(anchoTope, 0f, k * k);
                return Mathf.Abs(nx) <= ancho;
            });
            return tex;
        }

        // ATACAR: mira/crosshair (anillo + cruz), igual espiritu que RoleIconFactory.BuildMira.
        static Texture2D BuildMira()
        {
            var tex = NuevaTextura();
            const float rExt = 0.85f, rInt = 0.66f, grosor = 0.09f, hueco = 0.22f, rPunto = 0.1f;
            Rellenar(tex, (nx, ny) =>
            {
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                bool anillo = dist <= rExt && dist >= rInt;
                bool h = Mathf.Abs(ny) <= grosor && Mathf.Abs(nx) >= hueco && Mathf.Abs(nx) <= rExt;
                bool v = Mathf.Abs(nx) <= grosor && Mathf.Abs(ny) >= hueco && Mathf.Abs(ny) <= rExt;
                bool punto = dist <= rPunto;
                return anillo || h || v || punto;
            });
            return tex;
        }

        // POSICION: banderin (asta + tela triangular).
        static Texture2D BuildBandera()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool asta = Mathf.Abs(nx + 0.55f) <= 0.08f && ny >= -0.8f && ny <= 0.8f;
                bool tela = EnTriangulo(nx, ny, -0.47f, 0.8f, -0.47f, 0.1f, 0.65f, 0.45f);
                return asta || tela;
            });
            return tex;
        }

        // CURAR: cruz medica (igual forma que RoleIconFactory.BuildCruz).
        static Texture2D BuildCruz()
        {
            var tex = NuevaTextura();
            const float mitadAncho = 0.24f, mitadLargo = 0.78f;
            Rellenar(tex, (nx, ny) =>
                (Mathf.Abs(nx) <= mitadAncho && Mathf.Abs(ny) <= mitadLargo) ||
                (Mathf.Abs(ny) <= mitadAncho && Mathf.Abs(nx) <= mitadLargo));
            return tex;
        }

        // INTERACTUAR: engranaje (anillo con dientes y agujero central).
        static Texture2D BuildEngranaje()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                if (r < 0.28f) return false;                      // agujero
                float ang = Mathf.Atan2(ny, nx);
                float diente = Mathf.Abs(Mathf.Sin(ang * 4f));    // 8 dientes
                float rMax = diente > 0.55f ? 0.9f : 0.68f;
                return r <= rMax;
            });
            return tex;
        }

        // TANQUE: silueta simple (casco + torreta + cañon).
        static Texture2D BuildTanque()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool oruga = Mathf.Abs(nx) <= 0.85f && ny >= -0.55f && ny <= -0.25f;
                bool casco = Mathf.Abs(nx) <= 0.68f && ny >= -0.3f && ny <= 0.05f;
                bool torreta = (nx * nx) / (0.42f * 0.42f) + ((ny - 0.15f) * (ny - 0.15f)) / (0.35f * 0.35f) <= 1f && ny >= -0.05f;
                bool cañon = nx >= 0.05f && nx <= 0.78f && Mathf.Abs(ny - 0.22f) <= 0.08f;
                return oruga || casco || torreta || cañon;
            });
            return tex;
        }

        // POSEER: silueta de persona (cabeza + torso), misma idea que el icono de civil.
        static Texture2D BuildPersona()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                float dCabeza = Mathf.Sqrt(nx * nx + (ny - 0.45f) * (ny - 0.45f));
                bool cabeza = dCabeza <= 0.32f;
                bool torso = false;
                const float tope = 0.08f, baseY = -0.85f;
                if (ny <= tope && ny >= baseY)
                {
                    float t = Mathf.InverseLerp(tope, baseY, ny);
                    float ancho = Mathf.Lerp(0.75f, 0.5f, t) * 0.5f;
                    torso = Mathf.Abs(nx) <= ancho;
                }
                return cabeza || torso;
            });
            return tex;
        }

        // DEMOLER: estallido (estrella de 8 puntas).
        static Texture2D BuildEstallido()
        {
            var tex = NuevaTextura();
            const int puntas = 8;
            Rellenar(tex, (nx, ny) =>
            {
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float ang = Mathf.Atan2(ny, nx);
                float ciclo = Mathf.Repeat(ang / (Mathf.PI * 2f) * puntas, 1f);
                float onda = ciclo < 0.5f ? Mathf.Lerp(0.9f, 0.32f, ciclo * 2f) : Mathf.Lerp(0.32f, 0.9f, (ciclo - 0.5f) * 2f);
                return r <= onda;
            });
            return tex;
        }

        // TORRETA FIJA: base + cañon horizontal (mismo espiritu que el tanque, mas achatado).
        static Texture2D BuildTorreta()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool base_ = Mathf.Abs(nx) <= 0.7f && ny >= -0.75f && ny <= -0.35f;
                bool cuerpo = (nx * nx) / (0.5f * 0.5f) + (ny * ny) / (0.42f * 0.42f) <= 1f && ny >= -0.4f;
                bool cañon = nx >= 0f && nx <= 0.85f && Mathf.Abs(ny - 0.08f) <= 0.09f;
                return base_ || cuerpo || cañon;
            });
            return tex;
        }

        // GRUPO ("TODOS"): tres cabezas solidas, mas ancha que POSEER para no confundirse con "un solo soldado".
        static Texture2D BuildGrupo()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool Cabeza(float cx) => (nx - cx) * (nx - cx) / (0.3f * 0.3f) + (ny - 0.15f) * (ny - 0.15f) / (0.3f * 0.3f) <= 1f;
                bool Torso(float cx, float ancho)
                {
                    const float tope = -0.15f, baseY = -0.8f;
                    if (ny > tope || ny < baseY) return false;
                    return Mathf.Abs(nx - cx) <= ancho;
                }
                return Cabeza(0f) || Cabeza(-0.55f) || Cabeza(0.55f) || Torso(0f, 0.4f) || Torso(-0.55f, 0.28f) || Torso(0.55f, 0.28f);
            });
            return tex;
        }

        // CORAZON ("REVIVIR"): dos lobulos + punta.
        static Texture2D BuildCorazon()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                float dI = (nx + 0.28f) * (nx + 0.28f) + (ny - 0.15f) * (ny - 0.15f);
                float dD = (nx - 0.28f) * (nx - 0.28f) + (ny - 0.15f) * (ny - 0.15f);
                bool lobulos = dI <= 0.38f * 0.38f || dD <= 0.38f * 0.38f;
                bool punta = EnTriangulo(nx, ny, -0.62f, 0.12f, 0.62f, 0.12f, 0f, -0.85f);
                return lobulos || punta;
            });
            return tex;
        }

        // FLECHA ARRIBA ("SUBIR" / "RETIRADA"): igual forma que la de IR ALLI pero invertida.
        static Texture2D BuildFlechaArriba()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool cabeza = EnTriangulo(nx, ny, 0f, 0.85f, -0.55f, -0.05f, 0.55f, -0.05f);
                bool cola = Mathf.Abs(nx) <= 0.2f && ny <= 0f && ny >= -0.75f;
                return cabeza || cola;
            });
            return tex;
        }

        // FORMAR LINEA: tres cuadrados alineados en horizontal.
        static Texture2D BuildLinea()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool Cuadro(float cx) => Mathf.Abs(nx - cx) <= 0.18f && Mathf.Abs(ny) <= 0.18f;
                return Cuadro(0f) || Cuadro(-0.6f) || Cuadro(0.6f);
            });
            return tex;
        }

        // FORMAR CUÑA: tres cuadrados en punta de flecha (uno adelante, dos atras).
        static Texture2D BuildCuna()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool Cuadro(float cx, float cy) => Mathf.Abs(nx - cx) <= 0.18f && Mathf.Abs(ny - cy) <= 0.18f;
                return Cuadro(0f, 0.5f) || Cuadro(-0.5f, -0.4f) || Cuadro(0.5f, -0.4f);
            });
            return tex;
        }

        // GRANADA: cuerpo ovalado + traba superior.
        static Texture2D BuildGranada()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                bool cuerpo = nx * nx / (0.55f * 0.55f) + (ny + 0.1f) * (ny + 0.1f) / (0.7f * 0.7f) <= 1f && ny <= 0.5f;
                bool traba = Mathf.Abs(nx) <= 0.12f && ny >= 0.45f && ny <= 0.78f;
                bool anilla = Mathf.Abs(Mathf.Sqrt((nx - 0.18f) * (nx - 0.18f) + (ny - 0.78f) * (ny - 0.78f)) - 0.16f) <= 0.06f;
                return cuerpo || traba || anilla;
            });
            return tex;
        }

        // SIGUIENTE: doble flecha ">>" (ciclar).
        static Texture2D BuildSiguiente()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
                EnTriangulo(nx, ny, -0.55f, 0.5f, -0.55f, -0.5f, -0.05f, 0f) ||
                EnTriangulo(nx, ny, 0.05f, 0.5f, 0.05f, -0.5f, 0.55f, 0f));
            return tex;
        }

        // EQUIS ("CANCELAR" / "SALIR"): dos trazos cruzados.
        static Texture2D BuildEquis()
        {
            var tex = NuevaTextura();
            const float grosor = 0.16f;
            Rellenar(tex, (nx, ny) =>
            {
                float d1 = Mathf.Abs(nx - ny) / 1.41421356f;
                float d2 = Mathf.Abs(nx + ny) / 1.41421356f;
                bool dentroDiagonal = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)) <= 0.8f;
                return dentroDiagonal && (d1 <= grosor || d2 <= grosor);
            });
            return tex;
        }

        static Texture2D BuildCalavera()
        {
            var tex = NuevaTextura();
            Rellenar(tex, (nx, ny) =>
            {
                float r = nx * nx + (ny - 0.2f) * (ny - 0.2f);
                bool cabeza = r <= 0.6f * 0.6f;
                bool mandibula = Mathf.Abs(nx) <= 0.35f && ny >= -0.6f && ny <= 0.2f;
                if (!(cabeza || mandibula)) return false;

                float ojoI = (nx + 0.25f) * (nx + 0.25f) + (ny - 0.1f) * (ny - 0.1f);
                float ojoD = (nx - 0.25f) * (nx - 0.25f) + (ny - 0.1f) * (ny - 0.1f);
                if (ojoI <= 0.18f * 0.18f || ojoD <= 0.18f * 0.18f) return false;

                bool nariz = EnTriangulo(nx, ny, 0f, -0.1f, -0.1f, -0.28f, 0.1f, -0.28f);
                if (nariz) return false;

                bool dientes = ny >= -0.6f && ny <= -0.3f && (Mathf.Abs(Mathf.Repeat(nx + 0.05f, 0.2f) - 0.1f) < 0.04f);
                if (dientes) return false;

                return true;
            });
            return tex;
        }
    }
}
