using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Operacion
{
    // WP9b (#097): lienzo y bloqueo compartidos por las dos cinematicas de la huida (la toma del jefe y la cinematica final). Mismo bloqueo que
    // CinematicaDeOperacion: driver y rig de camara apagados (sin input), IA pausada, HUD oculto y rombos suprimidos; barras de cine, tarjeta
    // de jefe, subtitulo y un cartel central. Si el componente se apaga a mitad (Stop, cambio de escena) todo se restablece.
    public class CineDeHuida : MonoBehaviour
    {
        public static bool Activa { get; private set; }
        public static CineDeHuida Instancia { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activa = false; Instancia = null; EscalaDeTiempoPropia = 1f; }

        public Camera Camara { get; private set; }
        public float CampoDeVisionOriginal { get; private set; }
        public string TextoDelSubtitulo => subtitulo != null && grupoSub != null && grupoSub.alpha > 0.05f ? subtitulo.text : "";
        public string TextoDelCartel => cartel != null && grupoCartel != null && grupoCartel.alpha > 0.05f ? cartel.text : "";
        public string TextoDeLaTarjeta => tarjetaNombre != null && grupoTarjeta != null && grupoTarjeta.alpha > 0.05f ? tarjetaNombre.text : "";
        public float AlturaDeBarras { get; private set; }

        GameObject lienzo;
        Image barraArriba, barraAbajo, negro;
        Text subtitulo, cartel, tarjetaNombre, tarjetaSub;
        CanvasGroup grupoSub, grupoCartel, grupoTarjeta;
        PlayerInputDriver driver;
        readonly List<Canvas> canvasApagados = new List<Canvas>();
        bool bloqueado;
        static Font fuente;

        public static CineDeHuida Abrir(bool bloquear = true)
        {
            if (Instancia != null) return Instancia;
            var go = new GameObject("CineDeHuida");
            var c = go.AddComponent<CineDeHuida>();
            Instancia = c;
            c.Armar();
            if (bloquear) c.Bloquear();
            return c;
        }

        // Cierre inmediato desde afuera (saltos de fase, reinicios, pruebas). Seguro de llamar sin cinematica abierta.
        public static void Cancelar()
        {
            if (Instancia != null) Instancia.Cerrar();
        }

        public void Cerrar()
        {
            Restaurar();
            if (Camara != null) Camara.fieldOfView = CampoDeVisionOriginal;
            if (lienzo != null) Destroy(lienzo);
            lienzo = null;
            if (Instancia == this) Instancia = null;
            Activa = false;
            Destroy(gameObject);
        }

        void OnDisable() { if (bloqueado) { Restaurar(); Activa = false; } }
        void OnDestroy()
        {
            if (bloqueado) { Restaurar(); Activa = false; }
            if (lienzo != null) Destroy(lienzo);
            if (Instancia == this) Instancia = null;
        }

        void Bloquear()
        {
            bloqueado = true;
            Activa = true;
            SP.Ai.AiBrain.IAPausada = true;
            RomboVisibilidad.Suprimidos = true;
            driver = PlayerInputDriver.Activo;
            if (driver != null)
            {
                driver.enabled = false;
                if (driver.Rig != null) driver.Rig.enabled = false;
            }
            Camara = CamaraPrincipal.Actual;
            if (Camara == null && driver != null && driver.Rig != null) Camara = driver.Rig.Cam;
            if (Camara != null) CampoDeVisionOriginal = Camara.fieldOfView;
            canvasApagados.Clear();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == null || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
                if (lienzo != null && c.gameObject == lienzo) continue;
                if (c.name == "SesionLog_Indicador" || c.name == "CanvasPausa") continue;
                c.enabled = false;
                canvasApagados.Add(c);
            }
            AlertQueue.Clear();
        }

        // Camara lenta: escala el tiempo (y el paso de fisica) y lo restablece al cerrar, pase lo que pase.
        float escalaGuardada = -1f, pasoFisicoGuardado;
        public static float EscalaDeTiempoPropia { get; private set; } = 1f;
        public void Lento(float escala)
        {
            if (escalaGuardada < 0f) { escalaGuardada = Time.timeScale; pasoFisicoGuardado = Time.fixedDeltaTime; }
            Time.timeScale = Mathf.Max(0.02f, escala);
            Time.fixedDeltaTime = pasoFisicoGuardado * Time.timeScale;
            EscalaDeTiempoPropia = Time.timeScale;
        }
        public void TiempoNormal()
        {
            if (escalaGuardada < 0f) return;
            Time.timeScale = escalaGuardada; Time.fixedDeltaTime = pasoFisicoGuardado;
            escalaGuardada = -1f; EscalaDeTiempoPropia = 1f;
        }

        void Restaurar()
        {
            TiempoNormal();
            if (!bloqueado) return;
            bloqueado = false;
            SP.Ai.AiBrain.IAPausada = false;
            RomboVisibilidad.Suprimidos = false;
            if (driver != null)
            {
                driver.enabled = true;
                if (driver.Rig != null) driver.Rig.enabled = true;
            }
            foreach (var c in canvasApagados) if (c != null) c.enabled = true;
            canvasApagados.Clear();
        }

        // ---------------- interfaz ----------------
        static Text NuevaTexto(Transform padre, string nombre, int tam, TextAnchor ancla, Color color)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            g.transform.SetParent(padre, false);
            var t = g.GetComponent<Text>();
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = fuente; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.alignment = ancla; t.color = color;
            t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = g.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        static Image NuevaImagen(Transform padre, string nombre, Color color)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            var i = g.GetComponent<Image>(); i.color = color; i.raycastTarget = false;
            return i;
        }

        static void Anclar(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivote, Vector2 pos, Vector2 tam)
        {
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivote; r.anchoredPosition = pos; r.sizeDelta = tam;
        }

        void Armar()
        {
            lienzo = new GameObject("CineDeHuidaLienzo", typeof(Canvas), typeof(CanvasScaler));
            var cv = lienzo.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 850;
            var sc = lienzo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            var raiz = lienzo.transform;

            negro = NuevaImagen(raiz, "Negro", new Color(0f, 0f, 0f, 0f));
            Anclar(negro.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Tarjeta del jefe: nombre grande y una linea chica debajo, a la izquierda de la mitad de pantalla.
            var tg = new GameObject("TarjetaDeJefe", typeof(RectTransform), typeof(CanvasGroup));
            tg.transform.SetParent(raiz, false);
            grupoTarjeta = tg.GetComponent<CanvasGroup>(); grupoTarjeta.alpha = 0f;
            Anclar(tg.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1500f, 220f));
            var fondoT = NuevaImagen(tg.transform, "Fondo", new Color(0.12f, 0.02f, 0.02f, 0.6f));
            Anclar(fondoT.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tarjetaNombre = NuevaTexto(tg.transform, "Nombre", 96, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.25f));
            tarjetaNombre.horizontalOverflow = HorizontalWrapMode.Overflow;
            Anclar(tarjetaNombre.rectTransform, new Vector2(0, 0.4f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tarjetaSub = NuevaTexto(tg.transform, "Sub", 38, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.85f));
            Anclar(tarjetaSub.rectTransform, new Vector2(0, 0), new Vector2(1, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Cartel central (¡FUEGO!).
            var cg = new GameObject("CartelCentral", typeof(RectTransform), typeof(CanvasGroup));
            cg.transform.SetParent(raiz, false);
            grupoCartel = cg.GetComponent<CanvasGroup>(); grupoCartel.alpha = 0f;
            Anclar(cg.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1500f, 260f));
            cartel = NuevaTexto(cg.transform, "Texto", 150, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.3f));
            cartel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Anclar(cartel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Subtitulo abajo.
            var sg = new GameObject("Subtitulo", typeof(RectTransform), typeof(CanvasGroup));
            sg.transform.SetParent(raiz, false);
            grupoSub = sg.GetComponent<CanvasGroup>(); grupoSub.alpha = 0f;
            Anclar(sg.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0f, 150f), new Vector2(1500f, 120f));
            var fondoS = NuevaImagen(sg.transform, "Fondo", new Color(0.03f, 0.05f, 0.08f, 0.62f));
            Anclar(fondoS.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            subtitulo = NuevaTexto(sg.transform, "Texto", 44, TextAnchor.MiddleCenter, Color.white);
            Anclar(subtitulo.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            subtitulo.rectTransform.offsetMin = new Vector2(24f, 8f); subtitulo.rectTransform.offsetMax = new Vector2(-24f, -8f);
            subtitulo.resizeTextForBestFit = true; subtitulo.resizeTextMinSize = 24; subtitulo.resizeTextMaxSize = 46;

            // Barras de cine (encima de todo).
            barraArriba = NuevaImagen(raiz, "BarraArriba", Color.black);
            Anclar(barraArriba.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            barraAbajo = NuevaImagen(raiz, "BarraAbajo", Color.black);
            Anclar(barraAbajo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));
        }

        // k 0..1: de ocultas a 110 px.
        public void Barras(float k)
        {
            AlturaDeBarras = Mathf.Clamp01(k) * 110f;
            if (barraArriba == null) return;
            barraArriba.rectTransform.sizeDelta = new Vector2(0f, AlturaDeBarras);
            barraAbajo.rectTransform.sizeDelta = new Vector2(0f, AlturaDeBarras);
        }

        public void TarjetaDeJefe(string nombre, string linea, float alfa)
        {
            if (tarjetaNombre == null) return;
            tarjetaNombre.text = nombre; tarjetaSub.text = linea;
            grupoTarjeta.alpha = Mathf.Clamp01(alfa);
        }

        public void Subtitulo(string texto, float alfa)
        {
            if (subtitulo == null) return;
            if (texto != null) subtitulo.text = texto;
            grupoSub.alpha = Mathf.Clamp01(alfa);
        }

        public void Cartel(string texto, float alfa, Color? color = null)
        {
            if (cartel == null) return;
            if (texto != null) cartel.text = texto;
            if (color.HasValue) cartel.color = color.Value;
            grupoCartel.alpha = Mathf.Clamp01(alfa);
        }

        public void Fundido(float alfa)
        {
            if (negro != null) negro.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alfa));
        }

        public void PonerCamara(Vector3 pos, Quaternion rot, float fov = -1f)
        {
            if (Camara == null) Camara = CamaraPrincipal.Actual;
            if (Camara == null) return;
            Camara.transform.SetPositionAndRotation(pos, rot);
            if (fov > 0f) Camara.fieldOfView = fov;
        }
    }
}
