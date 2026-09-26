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
        Image redImage;
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
            
            if (redImage == null)
            {
                var child = transform.Find("RedImpact");
                if (child != null)
                {
                    redImage = child.GetComponent<Image>();
                }
                else
                {
                    var go = new GameObject("RedImpact");
                    go.transform.SetParent(transform, false);
                    redImage = go.AddComponent<Image>();
                    redImage.raycastTarget = false;
                    
                    var rt = redImage.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                }
                redImage.sprite = Sprite.Create(GetOrBuildTexture(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
                redImage.color = new Color(0.85f, 0.05f, 0.05f, 0f);
            }

            // `brain` no sobrevive al domain reload al entrar en Play mode
            // (se construyó en editor, vía Bind()): el campo queda null y
            // OnDamage nunca encuentra a quién le pertenece el golpe. Se
            // busca solo, igual que NearbySquadListView.
            if (brain == null) brain = PlayerBrain.Activo;
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        void OnDisable()
        {
            sub?.Dispose();
            if (redImage != null) redImage.color = new Color(0.85f, 0.05f, 0.05f, 0f);
        }

        float currentRedAlpha = 0f;

        void OnDamage(DamageTakenEvent evt)
        {
            if (image == null || brain == null || redImage == null) return;
            if (brain.Current == null || evt.TargetId != brain.Current.Id) return;

            if (!SP.CameraSystem.CameraFxSettings.Enabled) return;

            if (routine != null) StopCoroutine(routine);

            float maxHealth = brain.Current.Health.MaxHealth;
            // Intensity proportional to damage amount
            float damageFrac = maxHealth > 0 ? (float)evt.Amount / maxHealth : 0.5f;
            
            // Proportional to damage amount, with a cap so it doesn't fully obscure
            float targetPeak = Mathf.Clamp(damageFrac * 1.5f, 0.15f, 0.7f);
            
            // Must not stack beyond a maximum intensity
            float peak = Mathf.Min(currentRedAlpha + targetPeak, 0.85f);

            if (!Application.isPlaying)
            {
                currentRedAlpha = peak;
                redImage.color = new Color(0.85f, 0.05f, 0.05f, peak);
            }
            else
            {
                routine = StartCoroutine(FlashAndFadeRed(peak));
            }

            float remainingFrac = maxHealth > 0 ? Mathf.Clamp01((float)evt.RemainingHealth / maxHealth) : 1f;
            // 176: aberracion cromatica proporcional al daño, mas fuerte
            // cuanto menos vida queda. Es una segunda capa sobre la misma
            // señal que ya da el vignette, pero actua sobre la IMAGEN del
            // mundo y no sobre un overlay, asi que se lee incluso mirando
            // al centro de la pantalla.
            var postFx = SP.Presentation.PostFxDirector.Instance;
            if (postFx != null) postFx.PulseDamageAberration(Mathf.Lerp(0.9f, 0.25f, remainingFrac));
        }

        // 182: viñeta de velocidad. Reusa ESTA Image en vez de crear una
        // segunda capa a pantalla completa (fill-rate por una señal de
        // prioridad baja). El flash de daño se suma por encima de este
        // piso, en vez de interpolar siempre hacia 0.
        float baselineAlpha;

        public void SetSpeedFraction(float frac01)
        {
            baselineAlpha = SP.CameraSystem.CameraFxSettings.Enabled
                ? Mathf.Lerp(0f, 0.4f, Mathf.Clamp01(frac01))
                : 0f;
            // Si no hay un flash de daño en curso, el piso se aplica ya
            // mismo; si lo hay, la corrutina lo va a respetar al terminar.
            if (image != null)
                image.color = new Color(0f, 0f, 0f, baselineAlpha);
        }

        public float CurrentAlpha => image != null ? image.color.a : 0f;
        public float CurrentRedAlpha => redImage != null ? redImage.color.a : 0f;

        IEnumerator FlashAndFadeRed(float targetAlpha)
        {
            float t = 0f;
            // lasts 0.35s
            const float duration = 0.35f;
            
            // With overshoot animation (slightly exceeds target then settles)
            float overshootAlpha = Mathf.Min(targetAlpha * 1.25f, 0.95f);
            
            // Ensure first frame is strictly > 0 for immediate feedback and tests
            currentRedAlpha = Mathf.Max(currentRedAlpha, overshootAlpha * 0.1f);
            if (redImage != null) redImage.color = new Color(0.85f, 0.05f, 0.05f, currentRedAlpha);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float frac = t / duration;
                
                float a;
                // Pop to overshoot (0 to 15%)
                if (frac < 0.15f)
                {
                    a = Mathf.Lerp(currentRedAlpha, overshootAlpha, frac / 0.15f);
                }
                // Settle to target (15% to 35%)
                else if (frac < 0.35f)
                {
                    a = Mathf.Lerp(overshootAlpha, targetAlpha, (frac - 0.15f) / 0.2f);
                }
                // Fade to 0
                else
                {
                    a = Mathf.Lerp(targetAlpha, 0f, (frac - 0.35f) / 0.65f);
                }
                
                currentRedAlpha = a;
                redImage.color = new Color(0.85f, 0.05f, 0.05f, a);
                yield return null;
            }
            
            currentRedAlpha = 0f;
            redImage.color = new Color(0.85f, 0.05f, 0.05f, 0f);
            routine = null;
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

