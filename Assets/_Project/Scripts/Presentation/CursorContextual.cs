using UnityEngine;

namespace SP.Presentation
{
    // Ronda 11 (punto 16): en RTS, con soldados seleccionados, el cursor cambia al apuntar algo con lo que se puede interactuar
    // (vehiculo, ametralladora fija, aliado caido) o a un enemigo (atacar). Los dibujos se generan por codigo (32x32, sin assets)
    // y se cachean; con el cursor normal se devuelve el del sistema (Cursor.SetCursor(null)).
    public enum CursorTipo { Normal, Interactuable, Atacar }

    public static class CursorContextual
    {
        public static CursorTipo Actual { get; private set; } = CursorTipo.Normal;

        static Texture2D texInteractuable, texAtacar;

        public static void Aplicar(CursorTipo tipo)
        {
            if (tipo == Actual) return;
            Actual = tipo;
            switch (tipo)
            {
                case CursorTipo.Interactuable: Cursor.SetCursor(Tex(ref texInteractuable, new Color(1f, 0.85f, 0.2f), true), new Vector2(16, 16), CursorMode.Auto); break;
                case CursorTipo.Atacar: Cursor.SetCursor(Tex(ref texAtacar, new Color(1f, 0.25f, 0.2f), false), new Vector2(16, 16), CursorMode.Auto); break;
                default: Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); break;
            }
        }

        public static void Restaurar() => Aplicar(CursorTipo.Normal);

        static Texture2D Tex(ref Texture2D t, Color c, bool anillo)
        {
            if (t != null) return t;
            t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var px = new Color32[32 * 32];
            var borde = new Color32(0, 0, 0, 255);
            var col = (Color32)c;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - 15.5f, dy = y - 15.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool marca;
                    if (anillo) marca = (r >= 9f && r <= 12f) || r <= 2.5f;                       // interactuable: anillo con punto
                    else marca = (r >= 9f && r <= 11f) || (Mathf.Abs(dx) <= 1.2f && r >= 4f && r <= 15f) || (Mathf.Abs(dy) <= 1.2f && r >= 4f && r <= 15f); // atacar: mira
                    bool halo = !marca && ((anillo && ((r >= 8f && r <= 13f) || r <= 3.5f)) || (!anillo && ((r >= 8f && r <= 12f) || (Mathf.Abs(dx) <= 2.4f && r >= 3f && r <= 16f) || (Mathf.Abs(dy) <= 2.4f && r >= 3f && r <= 16f))));
                    px[y * 32 + x] = marca ? col : halo ? borde : new Color32(0, 0, 0, 0);
                }
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }
    }
}
