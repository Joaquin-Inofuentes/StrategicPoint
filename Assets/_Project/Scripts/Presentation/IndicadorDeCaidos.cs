using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Presentation
{
    // Bug #10: "falta un indicador para saber que necesita reanimacion y ubicacion; en el minimapa debe parpadear".
    // Por cada aliado CAIDO (muerto pero recuperable) se dibuja en pantalla una cruz roja que late con el nombre, la
    // distancia y "REANIMAR". Si el caido esta fuera de la pantalla (o detras de la camara) la cruz se pega al borde y
    // una flecha apunta hacia el. Al caer suena un aviso y aparece "¡KES CAIDO! NECESITA REANIMACION". El parpadeo en el
    // minimapa lo hace MinimapIcon (el icono del caido ya no se destruye). Se arma solo al entrar en una escena de juego.
    public class IndicadorDeCaidos : MonoBehaviour
    {
        public static IndicadorDeCaidos Instancia { get; private set; }

        class Marca
        {
            public RectTransform raiz, flecha;
            public Image cruz, fondo;
            public Text texto;
        }

        readonly Dictionary<Soldier, Marca> marcas = new Dictionary<Soldier, Marca>();
        readonly HashSet<Soldier> avisados = new HashSet<Soldier>();
        readonly List<Soldier> borrar = new List<Soldier>();
        RectTransform lienzo;
        Font fuente;
        static Sprite spriteCruz, spriteFlecha;

        public int MarcasVisibles { get { int n = 0; foreach (var m in marcas.Values) if (m.raiz.gameObject.activeSelf) n++; return n; } }
        public bool TieneMarca(Soldier s) => s != null && marcas.TryGetValue(s, out var m) && m.raiz.gameObject.activeSelf;
        public string TextoDe(Soldier s) => s != null && marcas.TryGetValue(s, out var m) ? m.texto.text : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AlCargar()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
            Asegurar();
        }

        static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Asegurar();

        public static IndicadorDeCaidos Asegurar()
        {
            if (Instancia != null) return Instancia;
            var go = new GameObject("IndicadorDeCaidos", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 120;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f); sc.matchWidthOrHeight = 0.5f;
            Instancia = go.AddComponent<IndicadorDeCaidos>();
            Instancia.lienzo = go.GetComponent<RectTransform>();
            Instancia.fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return Instancia;
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        static Sprite Cruz()
        {
            if (spriteCruz != null) return spriteCruz;
            const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool borde = (x >= 18 && x < 46) || (y >= 18 && y < 46);
                    bool centro = (x >= 23 && x < 41) || (y >= 23 && y < 41);
                    bool dentro = x >= 4 && x < 60 && y >= 4 && y < 60;
                    px[y * n + x] = !dentro ? new Color32(0, 0, 0, 0) : centro ? new Color32(255, 255, 255, 255) : borde ? new Color32(20, 20, 20, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px); tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
            spriteCruz = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            spriteCruz.hideFlags = HideFlags.HideAndDontSave;
            return spriteCruz;
        }

        static Sprite Flecha()
        {
            if (spriteFlecha != null) return spriteFlecha;
            const int n = 48; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // triangulo que apunta hacia +Y
                    float t = y / (float)(n - 1); float media = (1f - t) * (n * 0.5f);
                    bool dentro = Mathf.Abs(x - n * 0.5f) < media && y > 4;
                    px[y * n + x] = dentro ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px); tex.Apply(); tex.hideFlags = HideFlags.HideAndDontSave;
            spriteFlecha = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
            spriteFlecha.hideFlags = HideFlags.HideAndDontSave;
            return spriteFlecha;
        }

        Marca Crear(Soldier s)
        {
            var m = new Marca();
            var raiz = new GameObject("Caido_" + s.name, typeof(RectTransform));
            raiz.transform.SetParent(lienzo, false);
            m.raiz = raiz.GetComponent<RectTransform>();
            m.raiz.sizeDelta = new Vector2(260f, 120f);
            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(raiz.transform, false);
            m.fondo = fondo.GetComponent<Image>(); m.fondo.color = new Color(0f, 0f, 0f, 0.55f); m.fondo.raycastTarget = false;
            var frt = fondo.GetComponent<RectTransform>(); frt.anchoredPosition = new Vector2(0f, -40f); frt.sizeDelta = new Vector2(250f, 50f);
            var cruz = new GameObject("Cruz", typeof(RectTransform), typeof(Image));
            cruz.transform.SetParent(raiz.transform, false);
            m.cruz = cruz.GetComponent<Image>(); m.cruz.sprite = Cruz(); m.cruz.raycastTarget = false;
            var crt = cruz.GetComponent<RectTransform>(); crt.anchoredPosition = new Vector2(0f, 18f); crt.sizeDelta = new Vector2(64f, 64f);
            var tx = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            tx.transform.SetParent(raiz.transform, false);
            m.texto = tx.GetComponent<Text>(); m.texto.font = fuente; m.texto.fontSize = 20; m.texto.fontStyle = FontStyle.Bold;
            m.texto.alignment = TextAnchor.MiddleCenter; m.texto.color = Color.white; m.texto.raycastTarget = false;
            var trt = tx.GetComponent<RectTransform>(); trt.anchoredPosition = new Vector2(0f, -40f); trt.sizeDelta = new Vector2(250f, 50f);
            var fl = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
            fl.transform.SetParent(raiz.transform, false);
            var fimg = fl.GetComponent<Image>(); fimg.sprite = Flecha(); fimg.color = new Color(1f, 0.25f, 0.2f); fimg.raycastTarget = false;
            m.flecha = fl.GetComponent<RectTransform>(); m.flecha.sizeDelta = new Vector2(40f, 40f);
            return m;
        }

        void LateUpdate()
        {
            var cam = CamaraPrincipal.Actual;
            var drv = SP.Player.PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            var todos = ActorRegistry.All;
            float latido = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null) continue;
                bool caido = !s.Health.IsAlive && s.gameObject.activeInHierarchy;
                if (!caido) { avisados.Remove(s); continue; }

                if (!avisados.Contains(s))
                {
                    avisados.Add(s);
                    string quien = s.DisplayName.Replace("Soldado_", "").Replace("_", " ").ToUpperInvariant();
                    SP.UI.AlertQueue.Push($"¡{quien} CAIDO! NECESITA REANIMACION", SP.UI.AlertPriority.Alta, 3f);
                    AudioDirector.PlayUi2D(SfxKind.Wounded, 0.8f, 0.9f);
                    SesionLog.Evento($"{s.DisplayName} CAIDO: necesita reanimacion");
                }
                if (!marcas.TryGetValue(s, out var m) || m.raiz == null) { m = Crear(s); marcas[s] = m; }
                if (cam == null || s == yo) { m.raiz.gameObject.SetActive(false); continue; }   // al propio caido lo maneja la camara de muerte

                var punto = s.transform.position + Vector3.up * 1.2f;
                var sp = cam.WorldToScreenPoint(punto);
                bool detras = sp.z < 0f;
                if (detras) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
                float margen = 70f;
                // El cartel ("2 KES · REANIMANDO") mide ~250 px de ancho: pegado al borde lateral con 70 px quedaba cortado.
                float margenX = 150f * Screen.width / 1920f + 40f;
                bool fuera = detras || sp.x < margenX || sp.x > Screen.width - margenX || sp.y < margen || sp.y > Screen.height - margen;
                var centro = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 pos = sp;
                if (fuera)
                {
                    var dir = ((Vector2)sp - centro); if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                    dir.Normalize();
                    float kx = (Screen.width * 0.5f - margenX) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                    float ky = (Screen.height * 0.5f - margen) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                    pos = centro + dir * Mathf.Min(kx, ky);
                    m.flecha.gameObject.SetActive(true);
                    m.flecha.anchoredPosition = dir * 70f;
                    m.flecha.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                }
                else m.flecha.gameObject.SetActive(false);

                // De pixeles de pantalla al lienzo escalado.
                float escala = lienzo.rect.width / Mathf.Max(1f, Screen.width);
                m.raiz.anchorMin = m.raiz.anchorMax = Vector2.zero;
                m.raiz.anchoredPosition = pos * escala;
                m.raiz.gameObject.SetActive(true);

                m.cruz.color = Color.Lerp(new Color(1f, 0.15f, 0.12f), Color.white, latido * 0.6f);
                m.cruz.rectTransform.localScale = Vector3.one * (0.9f + 0.25f * latido);
                float d = yo != null ? Vector3.Distance(yo.transform.position, s.transform.position) : 0f;
                string nombre = s.DisplayName.Replace("Soldado_", "").Replace("_", " ").ToUpperInvariant();
                string quienReanima = SP.Player.PedidoDeCuracion.Reanimando && SP.Player.PedidoDeCuracion.Herido == s && SP.Player.PedidoDeCuracion.Enfermero != null
                    ? "REANIMANDO " + Mathf.RoundToInt(SP.Player.PedidoDeCuracion.ProgresoDeReanimar * 100f) + "%" : "REANIMAR";
                m.texto.text = $"{nombre} · {quienReanima} · {Mathf.RoundToInt(d)} m";
            }

            borrar.Clear();
            foreach (var kv in marcas)
            {
                var s = kv.Key;
                bool sigue = s != null && s.Health != null && !s.Health.IsAlive && s.gameObject.activeInHierarchy;
                if (!sigue) borrar.Add(s);
            }
            foreach (var s in borrar) { if (marcas[s].raiz != null) Destroy(marcas[s].raiz.gameObject); marcas.Remove(s); }
        }
    }
}
