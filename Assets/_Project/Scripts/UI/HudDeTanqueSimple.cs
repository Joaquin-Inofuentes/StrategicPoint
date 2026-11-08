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

        GameObject raiz; Image vida, recarga; Text texto; Vehicle vehiculo; TurretWeapon torreta;

        public static void Mostrar(Vehicle v)
        {
            if (Instancia == null) Crear();
            Instancia.vehiculo = v;
            Instancia.torreta = v != null ? v.GetComponentInChildren<TurretWeapon>(true) : null;
            Instancia.raiz.SetActive(v != null);
        }

        public static void Ocultar() { if (Instancia != null && Instancia.raiz != null) Instancia.raiz.SetActive(false); }

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
            rt.anchoredPosition = new Vector2(0f, 34f); rt.sizeDelta = new Vector2(520f, 40f);
            h.vida = Barra(rt, "Vida", new Vector2(0f, 14f), new Vector2(520f, 22f), new Color(0.35f, 0.9f, 0.4f));
            h.recarga = Barra(rt, "Recarga", new Vector2(0f, 4f), new Vector2(520f, 6f), new Color(1f, 0.8f, 0.3f));
            var tgo = new GameObject("Texto", typeof(RectTransform), typeof(Text)); tgo.transform.SetParent(h.vida.transform.parent, false);
            h.texto = tgo.GetComponent<Text>(); h.texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); h.texto.fontSize = 18; h.texto.alignment = TextAnchor.MiddleCenter; h.texto.color = Color.white;
            var trt = (RectTransform)tgo.transform; trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f); trt.pivot = new Vector2(0.5f, 0f); trt.anchoredPosition = new Vector2(0f, 12f); trt.sizeDelta = new Vector2(0f, 26f);
            h.raiz.SetActive(false);
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
            if (vehiculo == null || vehiculo.IsDestroyed || !CapasDeHud.EnModoTanque) { raiz.SetActive(false); return; }
            var hp = vehiculo.Health;
            FraccionDeVida = hp != null && hp.MaxHealth > 0 ? Mathf.Clamp01(hp.Current / (float)hp.MaxHealth) : 0f;
            vida.fillAmount = FraccionDeVida;
            vida.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.35f, 0.9f, 0.4f), FraccionDeVida);
            texto.text = hp != null ? $"TANQUE  {hp.Current} / {hp.MaxHealth}" : "TANQUE";
            FraccionDeRecarga = torreta != null ? torreta.CooldownFraction01 : 1f;
            recarga.fillAmount = FraccionDeRecarga;
            recarga.color = FraccionDeRecarga >= 0.999f ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.8f, 0.3f);
        }
    }
}
