using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.Tutorial
{
    // Texturas de teclas dibujadas por codigo (no hay assets: el tutorial se
    // arma solo). Cada tecla es un RawImage con una de tres texturas:
    //   normal  -> gris azulado
    //   lit     -> amarillo brillante con halo (la tecla esta apretada AHORA)
    //   hecha   -> verde (ya se uso en este paso)
    public static class TutorialTextures
    {
        public enum Estado { Normal, Encendida, Hecha }

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Color Fondo(Estado e) => e == Estado.Encendida ? new Color(1f, 0.86f, 0.22f)
            : e == Estado.Hecha ? new Color(0.20f, 0.66f, 0.36f) : new Color(0.17f, 0.20f, 0.27f);
        static Color Borde(Estado e) => e == Estado.Encendida ? new Color(1f, 1f, 0.75f)
            : e == Estado.Hecha ? new Color(0.62f, 1f, 0.72f) : new Color(0.50f, 0.58f, 0.72f);

        // Rectangulo redondeado con borde y un brillo arriba (efecto de tecla).
        public static Texture2D Tecla(int w, int h, Estado e)
        {
            string clave = $"k{w}x{h}{e}";
            if (cache.TryGetValue(clave, out var t) && t != null) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Tecla_" + clave };
            float r = Mathf.Min(w, h) * 0.22f;
            var fondo = Fondo(e); var borde = Borde(e);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = DistRedondeada(x + 0.5f, y + 0.5f, w, h, r);
                    if (d > 0.5f) { t.SetPixel(x, y, Color.clear); continue; }
                    float a = Mathf.Clamp01(0.5f - d);
                    Color c = fondo;
                    float brillo = Mathf.Clamp01((y - h * 0.55f) / (h * 0.45f)) * 0.16f;   // franja clara arriba
                    c = Color.Lerp(c, Color.white, brillo);
                    if (d > -2.6f) c = borde;
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            cache[clave] = t;
            return t;
        }

        static float DistRedondeada(float px, float py, float w, float h, float r)
        {
            float cx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float cy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(cx, 0f), oy = Mathf.Max(cy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(cx, cy), 0f) - r;
        }

        // Mouse: 0 = quieto, 1 = boton izquierdo, 2 = boton derecho, 3 = movimiento.
        public static Texture2D Mouse(int modo)
        {
            string clave = "m" + modo;
            if (cache.TryGetValue(clave, out var t) && t != null) return t;
            const int w = 56, h = 84;
            t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Mouse_" + modo };
            var cuerpo = modo == 3 ? new Color(1f, 0.86f, 0.22f) : new Color(0.17f, 0.20f, 0.27f);
            var luz = new Color(1f, 0.86f, 0.22f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Cuerpo: elipse alta con base redondeada.
                    float nx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                    float ny = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                    float e = nx * nx + Mathf.Pow(Mathf.Abs(ny), 2.6f) - 1f;
                    if (e > 0.02f) { t.SetPixel(x, y, Color.clear); continue; }
                    Color c = cuerpo;
                    bool borde = e > -0.16f;
                    if (borde) c = new Color(0.55f, 0.62f, 0.76f);
                    else
                    {
                        bool arriba = y > h * 0.52f;
                        if (arriba && Mathf.Abs(x + 0.5f - w * 0.5f) < 1.6f) c = new Color(0.55f, 0.62f, 0.76f);          // division de botones
                        else if (arriba && modo == 1 && x < w * 0.5f) c = luz;
                        else if (arriba && modo == 2 && x >= w * 0.5f) c = luz;
                        // rueda
                        if (Mathf.Abs(x + 0.5f - w * 0.5f) < 3.2f && y > h * 0.62f && y < h * 0.80f) c = new Color(0.55f, 0.62f, 0.76f);
                    }
                    c.a = Mathf.Clamp01(1f - Mathf.Max(0f, e) * 40f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            cache[clave] = t;
            return t;
        }

        public static Texture2D Casilla(bool hecha)
        {
            string clave = "c" + hecha;
            if (cache.TryGetValue(clave, out var t) && t != null) return t;
            const int n = 32;
            t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Casilla_" + hecha };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = DistRedondeada(x + 0.5f, y + 0.5f, n, n, 6f);
                    if (d > 0.5f) { t.SetPixel(x, y, Color.clear); continue; }
                    Color c = hecha ? new Color(0.30f, 0.86f, 0.46f) : new Color(0.08f, 0.10f, 0.14f);
                    if (d > -2.4f) c = hecha ? new Color(0.80f, 1f, 0.85f) : new Color(0.55f, 0.62f, 0.76f);
                    if (hecha)
                    {
                        // tilde dibujado: dos segmentos
                        float a1 = SegDist(x + 0.5f, y + 0.5f, 8f, 16f, 14f, 9f);
                        float a2 = SegDist(x + 0.5f, y + 0.5f, 14f, 9f, 25f, 23f);
                        if (Mathf.Min(a1, a2) < 2.2f) c = new Color(0.05f, 0.25f, 0.10f);
                    }
                    c.a = Mathf.Clamp01(0.5f - d);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            cache[clave] = t;
            return t;
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }

    // Una tecla (o boton de mouse) del cuadro de dialogo: un RawImage que se
    // ILUMINA mientras esta apretada y queda VERDE cuando ya se uso en el paso.
    // Suena un tick y da un pequeño "salto" al encenderse.
    public class KeyCapView : MonoBehaviour
    {
        public string Token { get; private set; }
        public bool Encendida { get; private set; }
        public bool Hecha { get; private set; }
        public bool Rastrear = true;   // si es false no queda verde (solo se ilumina)

        RawImage img;
        Text label;
        Func<bool> apretada;
        bool eraApretada;
        float salto;
        float movidoHasta;
        int ancho, alto;
        bool esMouse;
        int modoMouse;

        public static KeyCapView Crear(Transform padre, string token, Font fuente)
        {
            var go = new GameObject("Key_" + token, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(padre, false);
            var v = go.AddComponent<KeyCapView>();
            v.Token = token;
            v.img = go.GetComponent<RawImage>();
            v.img.raycastTarget = false;

            bool ancha = token == "Shift" || token == "Ctrl" || token == "Tab" || token == "Espacio";
            v.esMouse = token == "MOUSE" || token == "LMB" || token == "RMB";
            v.ancho = v.esMouse ? 34 : (ancha ? 68 : 42);
            v.alto = v.esMouse ? 50 : 42;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(v.ancho, v.alto);

            if (!v.esMouse)
            {
                var tgo = new GameObject("Etiqueta", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                tgo.transform.SetParent(go.transform, false);
                var trt = (RectTransform)tgo.transform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                v.label = tgo.GetComponent<Text>();
                v.label.font = fuente;
                v.label.fontStyle = FontStyle.Bold;
                v.label.fontSize = ancha ? 16 : 22;
                v.label.alignment = TextAnchor.MiddleCenter;
                v.label.text = token;
                v.label.color = Color.white;
                v.label.raycastTarget = false;
            }
            else
            {
                v.modoMouse = token == "LMB" ? 1 : token == "RMB" ? 2 : 3;
            }

            v.apretada = v.Mapear(token, v);
            v.Pintar();
            return v;
        }

        Func<bool> Mapear(string t, KeyCapView v)
        {
            switch (t)
            {
                case "MOUSE": return () => Time.unscaledTime < v.movidoHasta;
                case "LMB": return () => Mouse.current != null && Mouse.current.leftButton.isPressed;
                case "RMB": return () => Mouse.current != null && Mouse.current.rightButton.isPressed;
                case "Shift": return () => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                case "Ctrl": return () => Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
                case "Espacio": return () => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
                case "Tab": return () => Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            }
            Key k;
            if (t.Length == 1 && char.IsDigit(t[0])) k = (Key)Enum.Parse(typeof(Key), "Digit" + t);
            else if (!Enum.TryParse(t, true, out k)) return () => false;
            return () => Keyboard.current != null && Keyboard.current[k].isPressed;
        }

        void Update()
        {
            if (esMouse && modoMouse == 3 && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.5f)
                movidoHasta = Time.unscaledTime + 0.25f;

            bool p = apretada != null && apretada();
            if (p && !eraApretada)
            {
                salto = 1f;
                if (Application.isPlaying) AudioDirector.PlayUi2D(SfxKind.TutKey, 0.35f, 0.5f);
            }
            eraApretada = p;
            Encendida = p;
            if (p && Rastrear) Hecha = true;
            salto = Mathf.MoveTowards(salto, 0f, Time.unscaledDeltaTime * 5f);
            float escala = (p ? 1.10f : 1f) + salto * 0.14f;
            transform.localScale = new Vector3(escala, escala, 1f);
            Pintar();
        }

        void Pintar()
        {
            if (img == null) return;
            if (esMouse)
            {
                img.texture = TutorialTextures.Mouse(!Encendida ? 0 : modoMouse);
                img.color = Color.white;
                return;
            }
            var e = Encendida ? TutorialTextures.Estado.Encendida : (Hecha && Rastrear ? TutorialTextures.Estado.Hecha : TutorialTextures.Estado.Normal);
            img.texture = TutorialTextures.Tecla(ancho, alto, e);
            img.color = Color.white;
            if (label != null) label.color = e == TutorialTextures.Estado.Encendida ? new Color(0.10f, 0.08f, 0f) : Color.white;
        }
    }
}
