using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;

namespace SP.UI
{
    // Viñeta negra granulada que se degrada hacia el centro (oscura en los
    // bordes, transparente en el medio) y destella cada vez que el soldado
    // poseído recibe daño -- el típico "flash de impacto" de un FPS, para
    // que golpear se sienta en pantalla y no solo se lea en la barra de vida.
    public class DamageVignetteView : MonoBehaviour
    {
        Image image;
        PlayerBrain brain;
        IDisposable sub;
        Coroutine routine;
        static Texture2D cachedTexture;

        public void Bind(Image img, PlayerBrain playerBrain)
        {
            image = img;
            brain = playerBrain;
            image.sprite = Sprite.Create(GetOrBuildTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
        }

        void OnEnable()
        {
            if (image == null) image = GetComponent<Image>();
            // `brain` no sobrevive al domain reload al entrar en Play mode
            // (se construyó en editor, vía Bind()): el campo queda null y
            // OnDamage nunca encuentra a quién le pertenece el golpe. Se
            // busca solo, igual que NearbySquadListView.
            if (brain == null) brain = FindAnyObjectByType<PlayerBrain>();
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        void OnDisable() => sub?.Dispose();

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || image == null || brain == null) return;
            if (brain.Current == null || evt.TargetId != brain.Current.Id) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashAndFade());
        }

        IEnumerator FlashAndFade()
        {
            const float peakAlpha = 0.75f;
            const float fadeTime = 0.55f;

            image.color = new Color(0f, 0f, 0f, peakAlpha);
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                image.color = new Color(0f, 0f, 0f, Mathf.Lerp(peakAlpha, 0f, t / fadeTime));
                yield return null;
            }
            image.color = new Color(0f, 0f, 0f, 0f);
        }

        // Textura 128x128 generada una sola vez (no depende de ningún
        // asset): alfa 0 en el centro, sube hacia 1 en los bordes, con un
        // ruido granulado mezclado encima para que no se vea un degradé
        // perfectamente liso (pedido explícitamente "efecto granular").
        static Texture2D GetOrBuildTexture()
        {
            if (cachedTexture != null) return cachedTexture;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.Alpha8, false);
            var rng = new System.Random(1234);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - size * 0.5f) / (size * 0.5f);
                    float ny = (y - size * 0.5f) / (size * 0.5f);
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    // El primer intento (0.25 a 1) oscurecía casi toda la
                    // pantalla en cuanto se estira esta textura cuadrada
                    // sobre un canvas ancho (16:9): con la mitad del
                    // centro ya en la zona de degradado, se veía como un
                    // panel plano en vez de un borde. Corrido bien afuera
                    // para que quede como viñeta de verdad: clara en el
                    // medio, oscura solo cerca del borde.
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.15f, dist));
                    float grain = (float)rng.NextDouble() * 0.25f;
                    float a = Mathf.Clamp01(edge * (0.8f + grain));
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();
            cachedTexture = tex;
            return tex;
        }
    }
}
