using UnityEngine;
using UnityEngine.UI;
using SP.Vehicles;

namespace SP.UI
{
    // Bug #374 (2026-11-08): "la barra de vida que aparezca en el centro abajo y oculta lo de los aliados temporalmente mientras estoy en el tanque.
    // Cuando estoy en el tanque unicamente deberia ver la barra de vida y una barra de recarga delgada simple".
    // Overlay propio (no depende del layout del HUD de a pie): una barra de vida del tanque y, debajo, una barra de recarga del canon de 6 px.
    // CapasDeHud.ModoTanque lo prende al subir y lo apaga al bajar; la lista blanca del modo tanque ya no deja pasar a la escuadra (Roster).
    public sealed class HudDeTanqueSimple : MonoBehaviour
    {
        public static HudDeTanqueSimple Instancia { get; private set; }
        public static bool Visible => Instancia != null && Instancia.raiz != null && Instancia.raiz.activeSelf;
        public float FraccionDeVida { get; private set; }
        public float FraccionDeRecarga { get; private set; }

        public const float AnchoDeBarra = 760f;
        GameObject raiz; Image vida, recarga; Text texto, textoRecarga; Vehicle vehiculo; TurretWeapon torreta;
        GameObject insigniaDios;
        public bool InsigniaDeDiosVisible => insigniaDios != null && insigniaDios.activeInHierarchy;
        public float AnchoDeLaBarraDeVida => vida != null ? ((RectTransform)vida.transform.parent).rect.width : 0f;
        public static Rect RectEnPantalla(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        }
        public Rect RectDeVida => RectEnPantalla((RectTransform)vida.transform.parent);
        public Rect RectDeRecarga => RectEnPantalla((RectTransform)recarga.transform.parent);
        public Rect RectDeInsignia => RectEnPantalla((RectTransform)insigniaDios.transform);
        // #396: el HUD duplicado (VehicleStatusView, abajo a la derecha) y el cartel de modo dios (ModoDiosView, pisaba las barras) se esconden mientras
        // este HUD esta visible; el modo dios lo muestra una insignia propia a la izquierda de la barra de vida.
        readonly System.Collections.Generic.List<CanvasGroup> escondidos = new System.Collections.Generic.List<CanvasGroup>();
        float proximoBarrido;

        public static void Mostrar(Vehicle v)
        {
            if (Instancia == null) Crear();
            Instancia.vehiculo = v;
            // #399: la barra de recarga tiene que leer el CANON (la ametralladora tambien es TurretWeapon y su cooldown no baja al disparar el canon).
            Instancia.torreta = v != null ? (v.TorretaCanon != null ? v.TorretaCanon : v.GetComponentInChildren<TurretWeapon>(true)) : null;
            Instancia.raiz.SetActive(v != null);
        }

        static float destelloHasta;
        public static void Destellar() { destelloHasta = Time.unscaledTime + 0.25f; }   // #398: la barra de vida destella cuando le pegan al tanque
        public static void Ocultar() { if (Instancia != null && Instancia.raiz != null) { Instancia.raiz.SetActive(false); Instancia.Restaurar(); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; }

        static void Crear()
        {
            var go = new GameObject("HudDeTanqueSimple"); DontDestroyOnLoad(go);
            var h = go.AddComponent<HudDeTanqueSimple>(); Instancia = h;
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            h.raiz = new GameObject("Raiz", typeof(RectTransform)); h.raiz.transform.SetParent(go.transform, false);
            var rt = (RectTransform)h.raiz.transform; rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 26f); rt.sizeDelta = new Vector2(AnchoDeBarra, 64f);
            h.vida = Barra(rt, "Vida", new Vector2(0f, 26f), new Vector2(AnchoDeBarra, 28f), new Color(0.35f, 0.9f, 0.4f));
            h.recarga = Barra(rt, "Recarga", new Vector2(0f, 2f), new Vector2(AnchoDeBarra, 18f), new Color(1f, 0.8f, 0.3f));
            h.textoRecarga = Etiqueta(h.recarga.transform.parent, "TextoRecarga", 13, new Vector2(0f, 0f), new Vector2(0f, 18f));
            h.insigniaDios = Insignia(rt);
            var tgo = new GameObject("Texto", typeof(RectTransform), typeof(Text)); tgo.transform.SetParent(h.vida.transform.parent, false);
            h.texto = tgo.GetComponent<Text>(); h.texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); h.texto.fontSize = 20; h.texto.raycastTarget = false; h.texto.alignment = TextAnchor.MiddleCenter; h.texto.color = Color.white;
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f); trt.offsetMin = trt.offsetMax = Vector2.zero;   // centrado DENTRO de la barra
            h.raiz.SetActive(false);
        }

        static Text Etiqueta(Transform padre, string nombre, int tam, Vector2 pos, Vector2 delta)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text)); go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = tam; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return t;
        }

        // Insignia de modo dios (variante pedida en el #396): escudo dorado a la izquierda de la barra de vida, fuera de ella.
        static GameObject Insignia(RectTransform padre)
        {
            var go = new GameObject("InsigniaDios", typeof(RectTransform), typeof(Image)); go.transform.SetParent(padre, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f); r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = new Vector2(-AnchoDeBarra * 0.5f - 8f, 26f); r.sizeDelta = new Vector2(96f, 28f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.19f, 0.02f, 0.9f);
            var t = Etiqueta(go.transform, "Texto", 15, Vector2.zero, Vector2.zero); t.text = "DIOS · F4"; t.color = new Color(1f, 0.85f, 0.3f); t.fontStyle = FontStyle.Bold;
            go.SetActive(false);
            return go;
        }

        void Esconder()
        {
            if (Time.unscaledTime < proximoBarrido) return;
            proximoBarrido = Time.unscaledTime + 0.5f;
            foreach (var v in FindObjectsByType<VehicleStatusView>(FindObjectsInactive.Include)) Atenuar(v.gameObject);
            foreach (var m in FindObjectsByType<ModoDiosView>(FindObjectsInactive.Include)) Atenuar(m.gameObject);
        }

        void Atenuar(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>(); if (g == null) g = go.AddComponent<CanvasGroup>();
            if (!escondidos.Contains(g)) escondidos.Add(g);
        }

        void Restaurar() { foreach (var g in escondidos) if (g != null) g.alpha = 1f; escondidos.Clear(); }

        void LateUpdate()
        {
            if (raiz == null || !raiz.activeSelf) return;
            foreach (var g in escondidos) if (g != null) g.alpha = 0f;
        }

        static Image Barra(RectTransform padre, string nombre, Vector2 pos, Vector2 tam, Color color)
        {
            var fondo = new GameObject(nombre + "_Fondo", typeof(RectTransform), typeof(Image)); fondo.transform.SetParent(padre, false);
            var frt = (RectTransform)fondo.transform; frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f); frt.pivot = new Vector2(0.5f, 0f); frt.anchoredPosition = pos; frt.sizeDelta = tam;
            fondo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var relleno = new GameObject(nombre, typeof(RectTransform), typeof(Image)); relleno.transform.SetParent(fondo.transform, false);
            var rrt = (RectTransform)relleno.transform; rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = new Vector2(1f, 1f); rrt.offsetMax = new Vector2(-1f, -1f); rrt.pivot = new Vector2(0f, 0.5f);
            var img = relleno.GetComponent<Image>(); img.color = color; img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 1f;
            return img;
        }

        void Update()
        {
            if (raiz == null || !raiz.activeSelf) return;
            if (vehiculo == null || vehiculo.IsDestroyed || !CapasDeHud.EnModoTanque) { raiz.SetActive(false); Restaurar(); return; }
            Esconder();
            if (insigniaDios != null) insigniaDios.SetActive(SP.Core.ModoDios.Activo);
            var hp = vehiculo.Health;
            FraccionDeVida = hp != null && hp.MaxHealth > 0 ? Mathf.Clamp01(hp.Current / (float)hp.MaxHealth) : 0f;
            vida.fillAmount = FraccionDeVida;
            vida.color = Time.unscaledTime < destelloHasta ? Color.white : Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.35f, 0.9f, 0.4f), FraccionDeVida);
            texto.text = hp != null ? $"TANQUE  {hp.Current} / {hp.MaxHealth}" : "TANQUE";
            FraccionDeRecarga = torreta != null ? torreta.CooldownFraction01 : 1f;
            recarga.fillAmount = FraccionDeRecarga;
            recarga.color = FraccionDeRecarga >= 0.999f ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.8f, 0.3f);
            if (textoRecarga != null) textoRecarga.text = FraccionDeRecarga >= 0.999f ? "CAÑÓN · LISTO" : $"CAÑÓN · RECARGANDO {Mathf.RoundToInt(FraccionDeRecarga * 100f)} %";
        }
    }
}
