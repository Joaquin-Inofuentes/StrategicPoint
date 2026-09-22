using UnityEngine;

namespace SP.UI
{
    // Pedido explicito: "imagen low-poly futurista belico" para el fondo de
    // los botones, y "mejorar el arte de la UI" en general -- sin generador
    // de imagenes disponible, se resuelve como el resto del arte del
    // proyecto (skybox, soldados, decals): una textura generada por codigo,
    // no un asset descargado. El motivo de un PANEL ANGULADO (esquinas
    // cortadas en diagonal, no redondeadas) en vez de un rectangulo liso es
    // el mismo lenguaje visual "low-poly" que ya usan los cubos de
    // blockout: caras planas, cortes rectos, sin curvas.
    //
    // Una sola textura compartida por TODOS los botones (Sprite.Type.Sliced
    // para que el corte de esquina no se deforme sin importar el tamano del
    // boton), tenida despues por el color propio de cada boton
    // (Image.color, ya puesto por cada pantalla) -- multiplicar un gris
    // medio por un color dejar ver el tinte sin perder el bisel.
    public static class MilitaryButtonSkin
    {
        const int Tamano = 48;
        const int Chaflan = 9;   // profundidad del corte de esquina, en px
        const int Borde = 3;     // grosor del borde brillante, en px

        static Sprite sharedSprite;

        public static Sprite Shared
        {
            get
            {
                if (sharedSprite == null) sharedSprite = Construir();
                return sharedSprite;
            }
        }

        static Sprite Construir()
        {
            var tex = new Texture2D(Tamano, Tamano, TextureFormat.RGBA32, false, true)
            {
                name = "T_MilitaryButtonPanel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                // No se destruye con Resources.UnloadUnusedAssets: la unica
                // referencia viva es este campo estatico, que para Unity no
                // cuenta como "en uso" hasta que algun Sprite/Image la
                // toma. Mismo agujero que documenta SafeMaterial.
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pix = new Color32[Tamano * Tamano];
            for (int y = 0; y < Tamano; y++)
            {
                for (int x = 0; x < Tamano; x++)
                {
                    if (!Adentro(x, y, out int distAlCorte))
                    {
                        pix[y * Tamano + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    int distAlBorde = Mathf.Min(x, Tamano - 1 - x, y, Tamano - 1 - y, distAlCorte);
                    bool enElBorde = distAlBorde < Borde;
                    byte v = enElBorde ? (byte)232 : (byte)145;
                    pix[y * Tamano + x] = new Color32(v, v, v, 255);
                }
            }
            tex.SetPixels32(pix);
            tex.Apply(false, true);

            // Borde = Chaflan + 1: el 9-slice tiene que cubrir al menos la
            // zona de la diagonal para que el corte de esquina no se
            // estire ni se aplaste al escalar un boton mas ancho o mas alto.
            int b = Chaflan + 1;
            var borde = new Vector4(b, b, b, b);
            return Sprite.Create(tex, new Rect(0, 0, Tamano, Tamano), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, borde);
        }

        // Rectangulo con las cuatro esquinas cortadas en diagonal (chaflan
        // parejo en las 4, no solo dos, para que el resultado sea simetrico
        // y facil de razonar). distAlCorte sale en unidades "manhattan"
        // comparables a distAlBorde de arriba, para que el mismo Borde sirva
        // de grosor de brillo tanto en el borde recto como en la diagonal.
        static bool Adentro(int x, int y, out int distAlCorte)
        {
            distAlCorte = int.MaxValue;
            int izq = x, der = Tamano - 1 - x, arr = y, abj = Tamano - 1 - y;

            if (izq < Chaflan && arr < Chaflan) return ChequearEsquina(izq + arr, out distAlCorte);
            if (der < Chaflan && arr < Chaflan) return ChequearEsquina(der + arr, out distAlCorte);
            if (izq < Chaflan && abj < Chaflan) return ChequearEsquina(izq + abj, out distAlCorte);
            if (der < Chaflan && abj < Chaflan) return ChequearEsquina(der + abj, out distAlCorte);

            return true;
        }

        static bool ChequearEsquina(int sumaDeLados, out int distAlCorte)
        {
            distAlCorte = sumaDeLados - Chaflan;
            return distAlCorte >= 0;
        }
    }
}
