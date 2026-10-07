using System;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.UI
{
    // Bug #059: "quiero que cuando impacte el proyectil, alrededor tenga rayitas, como indicando que el impacto llego al enemigo".
    // Marca de impacto clasica: cuatro rayitas en X alrededor del centro de la pantalla (donde estan la mira a pie y la del
    // cañon) cada vez que un disparo PROPIO le pega a un enemigo o a un vehiculo enemigo. Blanca al herir. Se arma sola en la
    // primera escena con soldados y vive en su propio canvas (no depende del HUD).
    //
    // Bug #065 ("el cursor de baja mas poderoso"): la baja ya no es "la misma marca mas grande y roja". Es otra cosa: 8 rayas que
    // explotan hacia afuera, un anillo que se expande de 24 a 70 px de radio en 0.35 s, un golpe de escala 2.0 -> 1.0, rojo
    // intenso y 0.7 s de vida. Si la baja fue de un HEADSHOT (HeadshotEvent del mismo golpe) el color es dorado y se suma un
    // segundo anillo: se lee distinto sin mirar nada mas.
    public class MarcaDeImpacto : MonoBehaviour
    {
        public static MarcaDeImpacto Instancia { get; private set; }
        public static int Mostradas { get; private set; }
        public static bool UltimaFueBaja { get; private set; }
        public static bool UltimaFueHeadshot { get; private set; }
        public bool Visible => grupo != null && grupo.alpha > 0.01f;

        public const float Duracion = 0.28f, DuracionBaja = 0.7f;
        const float AnilloDesde = 24f, AnilloHasta = 70f, DuracionAnillo = 0.35f, GolpeDeEscala = 0.12f;
        const int RayasNormales = 4, RayasDeBaja = 8;
        static readonly Color ColorImpacto = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color ColorBaja = new Color(1f, 0.1f, 0.06f, 1f);
        static readonly Color ColorHeadshot = new Color(1f, 0.82f, 0.18f, 1f);

        CanvasGroup grupo;
        RectTransform raiz;
        readonly RectTransform[] rayas = new RectTransform[RayasDeBaja];
        readonly Image[] imagenes = new Image[RayasDeBaja];
        RectTransform anillo, anilloDoble;
        Image anilloImg, anilloDobleImg;
        float hasta, inicio, dur;
        bool baja, headshot;
        IDisposable subDano, subVehiculo, subHeadshot;
        // Mientras se espera la baja que cierra un HeadshotEvent (se publica ANTES del daño del mismo tiro).
        int headshotObjetivo = -1; float headshotEn = -99f;

        // Para checks y capturas: congela la animacion en este progreso (0..1); null = corre sola.
        public float? ProgresoForzado;
        public bool EsBaja => baja;
        public bool EsHeadshot => headshot;
        public float DuracionActual => dur;
        public bool AnilloVisible => anillo != null && anillo.gameObject.activeSelf && anilloImg.color.a > 0.01f;
        public bool AnilloDobleVisible => anilloDoble != null && anilloDoble.gameObject.activeSelf && anilloDobleImg.color.a > 0.01f;
        public float RadioDelAnillo => anillo != null ? anillo.sizeDelta.x * 0.5f : 0f;
        public int RayasActivas { get { int n = 0; for (int i = 0; i < rayas.Length; i++) if (rayas[i] != null && rayas[i].gameObject.activeSelf) n++; return n; } }
        public float EscalaActual => raiz != null ? raiz.localScale.x : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; Mostradas = 0; UltimaFueBaja = false; UltimaFueHeadshot = false; }

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

            // Todo cuelga de una raiz centrada: el golpe de escala 2.0 -> 1.0 de la baja la agranda entera.
            var rg = new GameObject("Raiz", typeof(RectTransform));
            rg.transform.SetParent(go.transform, false);
            m.raiz = rg.GetComponent<RectTransform>();
            m.raiz.anchorMin = m.raiz.anchorMax = new Vector2(0.5f, 0.5f);
            m.raiz.sizeDelta = Vector2.zero;

            var ring = SpriteDeAnillo();
            m.anillo = NuevoAnillo(m.raiz, "Anillo", ring, out m.anilloImg);
            m.anilloDoble = NuevoAnillo(m.raiz, "AnilloDoble", ring, out m.anilloDobleImg);

            for (int i = 0; i < RayasDeBaja; i++)
            {
                var r = new GameObject("Raya" + i, typeof(RectTransform), typeof(Image));
                r.transform.SetParent(m.raiz, false);
                var img = r.GetComponent<Image>(); img.raycastTarget = false; img.color = ColorImpacto;
                var rt = r.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(3f, 13f);
                var sombra = r.AddComponent<Outline>(); sombra.effectColor = new Color(0f, 0f, 0f, 0.7f); sombra.effectDistance = new Vector2(1f, -1f);
                m.rayas[i] = rt; m.imagenes[i] = img;
            }
            m.Esconder();
            Instancia = m;
        }

        static RectTransform NuevoAnillo(Transform padre, string nombre, Sprite sprite, out Image img)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            img = g.GetComponent<Image>(); img.raycastTarget = false; img.sprite = sprite; img.color = Color.clear;
            var rt = g.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(AnilloDesde * 2f, AnilloDesde * 2f);
            return rt;
        }

        // Anillo liso (borde de ~7% del diametro, suavizado) generado una sola vez: sin assets nuevos.
        static Sprite SpriteDeAnillo()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f, rExt = c - 1f, grosor = n * 0.07f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(Mathf.Min(rExt - d, d - (rExt - grosor)) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        void OnEnable()
        {
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDano);
            subVehiculo = EventBus.Instance.Subscribe<VehicleDamagedEvent>(OnVehiculo);
            subHeadshot = EventBus.Instance.Subscribe<HeadshotEvent>(OnHeadshot);
        }

        void OnDisable() { subDano?.Dispose(); subVehiculo?.Dispose(); subHeadshot?.Dispose(); subDano = subVehiculo = subHeadshot = null; }

        static int IdPropio()
        {
            var drv = SP.Player.PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            return yo != null ? yo.Id : int.MinValue;
        }

        void OnHeadshot(HeadshotEvent e)
        {
            if (e.ShooterId != IdPropio()) return;
            headshotObjetivo = e.TargetId; headshotEn = Time.unscaledTime;
        }

        void OnDano(DamageTakenEvent e)
        {
            if (e.AttackerId != IdPropio()) return;
            var victima = ActorRegistry.FindById(e.TargetId);
            if (victima == null || victima.Team != SP.Combat.TeamId.Enemy) return;
            bool esBaja = e.RemainingHealth <= 0;
            bool fueCabeza = esBaja && headshotObjetivo == e.TargetId && Time.unscaledTime - headshotEn < 0.25f;
            Mostrar(esBaja, fueCabeza);
        }

        void OnVehiculo(VehicleDamagedEvent e)
        {
            var v = e.Vehicle;
            if (v == null || v.Bando != SP.Combat.TeamId.Enemy || v.Health == null || v.Health.LastAttackerId != IdPropio()) return;
            Mostrar(e.RemainingHealth <= 0);
        }

        public void Mostrar(bool esBaja, bool conHeadshot = false)
        {
            baja = esBaja;
            headshot = esBaja && conHeadshot;
            UltimaFueBaja = esBaja;
            UltimaFueHeadshot = headshot;
            Mostradas++;
            inicio = Time.unscaledTime;
            dur = esBaja ? DuracionBaja : Duracion;
            hasta = inicio + dur;
            var color = !esBaja ? ColorImpacto : headshot ? ColorHeadshot : ColorBaja;
            int n = esBaja ? RayasDeBaja : RayasNormales;
            for (int i = 0; i < rayas.Length; i++)
            {
                rayas[i].gameObject.SetActive(i < n);
                imagenes[i].color = color;
            }
            anilloImg.color = esBaja ? color : Color.clear;
            anilloDobleImg.color = headshot ? ColorHeadshot : Color.clear;
            anillo.gameObject.SetActive(esBaja);
            anilloDoble.gameObject.SetActive(headshot);
            Actualizar(0f);
        }

        void Esconder()
        {
            if (grupo != null) grupo.alpha = 0f;
            if (anillo != null) anillo.gameObject.SetActive(false);
            if (anilloDoble != null) anilloDoble.gameObject.SetActive(false);
        }

        static float SalidaSuave(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k); }

        void Actualizar(float k)
        {
            if (!baja)
            {
                // Herida: salta hacia afuera y se encoge mientras se apaga: se lee como un "toc".
                // Por fuera del circulo de la mira del cañon (radio ~36 px de referencia) para que no la tape el rombo de impacto.
                float distN = Mathf.Lerp(34f, 50f, Mathf.Sqrt(k));
                float largoN = Mathf.Lerp(18f, 10f, k);
                for (int i = 0; i < RayasNormales; i++)
                {
                    float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                    rayas[i].anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * distN;
                    rayas[i].sizeDelta = new Vector2(4f, largoN);
                    rayas[i].localRotation = Quaternion.Euler(0f, 0f, (45f + 90f * i) - 90f);
                }
                raiz.localScale = Vector3.one;
                grupo.alpha = 1f - k * k;
                return;
            }

            // BAJA: 8 rayas (4 en X y 4 en cruz) que explotan hacia afuera mas rapido y mas lejos, anillo que se expande y
            // golpe de escala 2.0 -> 1.0 en los primeros 0.12 s.
            float t = k * dur;
            float salida = SalidaSuave(t / 0.35f);
            float dist = Mathf.Lerp(30f, 118f, salida);
            float largo = Mathf.Lerp(34f, 12f, SalidaSuave(t / 0.5f));
            float ancho = Mathf.Lerp(7f, 3.5f, k);
            for (int i = 0; i < RayasDeBaja; i++)
            {
                float angGrados = i < 4 ? 45f + 90f * i : 90f * (i - 4);
                float ang = angGrados * Mathf.Deg2Rad;
                // Las de la cruz salen un poco mas cortas y atrasadas: se lee como un estallido y no como una rueda.
                float f = i < 4 ? 1f : 0.82f;
                rayas[i].anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (dist * f);
                rayas[i].sizeDelta = new Vector2(ancho, largo * (i < 4 ? 1f : 0.75f));
                rayas[i].localRotation = Quaternion.Euler(0f, 0f, angGrados - 90f);
            }
            float kAnillo = SalidaSuave(t / DuracionAnillo);
            float radio = Mathf.Lerp(AnilloDesde, AnilloHasta, kAnillo);
            anillo.sizeDelta = new Vector2(radio * 2f, radio * 2f);
            SetAlfa(anilloImg, Mathf.Clamp01(1f - (t - 0.1f) / 0.5f));
            if (headshot)
            {
                // Segundo anillo dorado, un instante despues y mas chico que el primero: la firma del tiro a la cabeza.
                float kd = SalidaSuave((t - 0.14f) / 0.4f);
                float rd = Mathf.Lerp(AnilloDesde * 0.7f, AnilloHasta, kd);
                anilloDoble.sizeDelta = new Vector2(rd * 2f, rd * 2f);
                SetAlfa(anilloDobleImg, t < 0.14f ? 0f : Mathf.Clamp01(1f - (t - 0.3f) / 0.4f));
            }
            float golpe = Mathf.Lerp(2f, 1f, SalidaSuave(t / GolpeDeEscala));
            raiz.localScale = Vector3.one * golpe;
            grupo.alpha = k < 0.55f ? 1f : 1f - Mathf.Pow((k - 0.55f) / 0.45f, 2f);
        }

        static void SetAlfa(Image img, float a) { var c = img.color; c.a = a; img.color = c; }

        void Update()
        {
            if (grupo == null) return;
            if (ProgresoForzado.HasValue) { grupo.alpha = Mathf.Max(grupo.alpha, 0.0001f); Actualizar(Mathf.Clamp01(ProgresoForzado.Value)); return; }
            if (grupo.alpha <= 0f && Time.unscaledTime >= hasta) return;
            float k = Mathf.Clamp01((Time.unscaledTime - inicio) / Mathf.Max(0.01f, dur));
            if (Time.unscaledTime >= hasta) { Esconder(); return; }
            Actualizar(k);
        }
    }
}
