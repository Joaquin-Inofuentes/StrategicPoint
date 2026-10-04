using System;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Bug #059: "quiero que cuando impacte el proyectil, alrededor tenga rayitas, como indicando que el impacto llego al enemigo".
    // Marca de impacto clasica: cuatro rayitas en X alrededor del centro de la pantalla (donde estan la mira a pie y la del
    // cañon) cada vez que un disparo PROPIO le pega a un enemigo o a un vehiculo enemigo. Blanca al herir, roja y mas grande
    // al matar o destruir. Se arma sola en la primera escena con soldados y vive en su propio canvas (no depende del HUD).
    public class MarcaDeImpacto : MonoBehaviour
    {
        public static MarcaDeImpacto Instancia { get; private set; }
        public static int Mostradas { get; private set; }
        public static bool UltimaFueBaja { get; private set; }
        public bool Visible => grupo != null && grupo.alpha > 0.01f;

        const float Duracion = 0.28f, DuracionBaja = 0.45f;
        static readonly Color ColorImpacto = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color ColorBaja = new Color(1f, 0.22f, 0.15f, 1f);

        CanvasGroup grupo;
        readonly RectTransform[] rayas = new RectTransform[4];
        readonly Image[] imagenes = new Image[4];
        float hasta, inicio, dur;
        bool baja;
        IDisposable subDano, subVehiculo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; Mostradas = 0; UltimaFueBaja = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (Instancia != null) return;
            var go = new GameObject("MarcaDeImpacto", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            DontDestroyOnLoad(go);
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 55;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
            var m = go.AddComponent<MarcaDeImpacto>();
            m.grupo = go.GetComponent<CanvasGroup>(); m.grupo.alpha = 0f; m.grupo.blocksRaycasts = false; m.grupo.interactable = false;
            for (int i = 0; i < 4; i++)
            {
                var r = new GameObject("Raya" + i, typeof(RectTransform), typeof(Image));
                r.transform.SetParent(go.transform, false);
                var img = r.GetComponent<Image>(); img.raycastTarget = false; img.color = ColorImpacto;
                var rt = r.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(3f, 13f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                var sombra = r.AddComponent<Outline>(); sombra.effectColor = new Color(0f, 0f, 0f, 0.7f); sombra.effectDistance = new Vector2(1f, -1f);
                m.rayas[i] = rt; m.imagenes[i] = img;
            }
            Instancia = m;
        }

        void OnEnable()
        {
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDano);
            subVehiculo = EventBus.Instance.Subscribe<VehicleDamagedEvent>(OnVehiculo);
        }

        void OnDisable() { subDano?.Dispose(); subVehiculo?.Dispose(); subDano = subVehiculo = null; }

        static int IdPropio()
        {
            var drv = SP.Player.PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            return yo != null ? yo.Id : int.MinValue;
        }

        void OnDano(DamageTakenEvent e)
        {
            if (e.AttackerId != IdPropio()) return;
            var victima = ActorRegistry.FindById(e.TargetId);
            if (victima == null || victima.Team != SP.Combat.TeamId.Enemy) return;
            Mostrar(e.RemainingHealth <= 0);
        }

        void OnVehiculo(VehicleDamagedEvent e)
        {
            var v = e.Vehicle;
            if (v == null || v.Bando != SP.Combat.TeamId.Enemy || v.Health == null || v.Health.LastAttackerId != IdPropio()) return;
            Mostrar(e.RemainingHealth <= 0);
        }

        public void Mostrar(bool esBaja)
        {
            baja = esBaja;
            UltimaFueBaja = esBaja;
            Mostradas++;
            inicio = Time.unscaledTime;
            dur = esBaja ? DuracionBaja : Duracion;
            hasta = inicio + dur;
            foreach (var img in imagenes) img.color = esBaja ? ColorBaja : ColorImpacto;
            Actualizar(0f);
        }

        void Actualizar(float k)
        {
            // Salta hacia afuera y se encoge mientras se apaga: se lee como un "toc".
            // Por fuera del circulo de la mira del cañon (radio ~36 px de referencia) para que no la tape el rombo de impacto.
            float dist = Mathf.Lerp(baja ? 40f : 34f, baja ? 62f : 50f, Mathf.Sqrt(k));
            float largo = Mathf.Lerp(baja ? 24f : 18f, baja ? 14f : 10f, k);
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                rayas[i].anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                rayas[i].sizeDelta = new Vector2(baja ? 5f : 4f, largo);
                rayas[i].localRotation = Quaternion.Euler(0f, 0f, (45f + 90f * i) - 90f);
            }
            grupo.alpha = 1f - k * k;
        }

        void Update()
        {
            if (grupo == null || grupo.alpha <= 0f && Time.unscaledTime >= hasta) return;
            float k = Mathf.Clamp01((Time.unscaledTime - inicio) / Mathf.Max(0.01f, dur));
            if (Time.unscaledTime >= hasta) { grupo.alpha = 0f; return; }
            Actualizar(k);
        }
    }
}
