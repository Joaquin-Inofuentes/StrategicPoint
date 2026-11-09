using System;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;
using SP.Presentation;

namespace SP.UI
{
    // Bug #398 ("me falta feedback sonoro y visual de cuando me impactan los proyectiles enemigos"): el daño al soldado tiene viñeta roja, flecha y
    // sonido (DamageTakenEvent), pero el que recibe el tanque viaja como VehicleDamagedEvent y nadie lo mostraba en pantalla. Cuando le pegan al
    // vehiculo que manejas: viñeta roja proporcional al golpe, golpe seco 2D, sacudon de camara hacia donde vino el tiro y un destello en la barra.
    public sealed class ImpactoEnVehiculoView : MonoBehaviour
    {
        public static ImpactoEnVehiculoView Instancia { get; private set; }
        public static int Mostrados { get; private set; }
        public float AlphaActual => rojo != null ? rojo.color.a : 0f;
        public float UltimaIntensidad { get; private set; }
        public const float Enfriamiento = 0.12f;

        Image rojo; IDisposable sub; float pico, inicio, ultimoSonido = -10f;
        static Sprite sprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; Mostrados = 0; sprite = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (Instancia != null) return;
            var go = new GameObject("ImpactoEnVehiculoView") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 58;
            var v = go.AddComponent<ImpactoEnVehiculoView>(); Instancia = v;
            var img = new GameObject("Rojo", typeof(RectTransform), typeof(Image)); img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            v.rojo = img.GetComponent<Image>(); v.rojo.sprite = Sprite(); v.rojo.raycastTarget = false; v.rojo.color = new Color(0.9f, 0.05f, 0.05f, 0f);
        }

        static Sprite Sprite()
        {
            if (sprite != null) return sprite;
            const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.35f) / 0.9f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            t.Apply();
            return sprite = UnityEngine.Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void OnEnable() { sub?.Dispose(); sub = EventBus.Instance.Subscribe<VehicleDamagedEvent>(Al_Golpe); }
        void OnDisable() { sub?.Dispose(); }

        void Al_Golpe(VehicleDamagedEvent e)
        {
            var d = PlayerInputDriver.Activo;
            if (d == null || e.Vehicle == null || e.Vehicle != d.Vehicle || e.Vehicle.Bando != SP.Combat.TeamId.Player) return;
            float frac = e.MaxHealth > 0 ? Mathf.Clamp01((float)e.Amount / e.MaxHealth) : 0.05f;
            UltimaIntensidad = Mathf.Clamp(0.25f + frac * 6f, 0.25f, 0.8f);
            pico = Mathf.Min(0.85f, Mathf.Max(AlphaActual, 0f) + UltimaIntensidad); inicio = Time.unscaledTime;
            Mostrados++;
            if (Time.unscaledTime - ultimoSonido >= Enfriamiento)
            {
                ultimoSonido = Time.unscaledTime;
                try { GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.VehicleHit), 0.9f * AudioDirector.GainFor(SfxChannel.Sfx), UnityEngine.Random.Range(0.85f, 1.05f), "ImpactoEnVehiculo"); } catch { }
            }
            var atacante = e.Vehicle.Health != null ? ActorRegistry.FindById(e.Vehicle.Health.LastAttackerId) : null;
            var rig = SP.CameraSystem.CameraRig.Instance;
            if (rig != null && atacante != null) rig.KickDirectional(atacante.transform.position - e.Vehicle.transform.position, Mathf.Lerp(0.15f, 0.5f, UltimaIntensidad));
            HudDeTanqueSimple.Destellar();
        }

        void Update()
        {
            if (rojo == null) return;
            float k = Mathf.Clamp01((Time.unscaledTime - inicio) / 0.45f);
            var c = rojo.color; c.a = pico * (1f - k) * (1f - k); rojo.color = c;
        }
    }
}
