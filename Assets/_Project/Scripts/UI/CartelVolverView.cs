using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Mision;
using SP.CameraSystem;

namespace SP.UI
{
    // Ronda 13 T10: lo que necesita el cartel «DEBES VOLVER A LA GUERRA» de cualquier director (Mision = nivel 1, Operacion = nivel 2).
    public interface IObjetivoParaVolver
    {
        bool Activa { get; }          // false = el director no quiere cartel (cinematica, tanque, victoria...)
        float Distancia();            // metros en el plano al objetivo
        float Umbral();               // por encima de esto (y alejandose 4 s) se muestra
        Vector3 Punto();              // objetivo en el mundo (flecha)
    }

    // Un solo cartel de «volver»: texto + distancia + flecha, fondo transparente, pulso, SIN collider (solo canvas).
    public class CartelVolverView : MonoBehaviour
    {
        public const string TextoCartel = "DEBES VOLVER A LA GUERRA";

        public static CartelVolverView Actual { get; private set; }
        static float forzarHasta;

        // Muestra el cartel `segundos` aunque el jugador no se este alejando (p. ej. al cruzar la compuerta de bajada de la mansion).
        public static bool Forzado => Time.time < forzarHasta;
        public static void Forzar(float segundos) { forzarHasta = Mathf.Max(forzarHasta, Time.time + segundos); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Actual = null; forzarHasta = 0f; }

        GameObject cuerpo;
        RectTransform panel;
        Text texto;
        RectTransform flecha;
        Image flechaImg, fondoImg;
        IObjetivoParaVolver fuente;

        float lastDist;
        float tiempoAlejandose;
        float alpha;

        // Lectura para checks y para el arnes.
        public float Alfa => alpha;
        public float SegundosAlejandose => tiempoAlejandose;
        public bool Visible => alpha > 0.5f;
        public string TextoActual => texto != null && alpha > 0.01f ? texto.text : "";
        public bool TieneCollider => GetComponentInChildren<Collider>(true) != null || GetComponentInChildren<Collider2D>(true) != null;

        sealed class FuenteMision : IObjetivoParaVolver
        {
            public bool Activa => MisionDirector.Instancia != null && MisionDirector.Activo;
            public float Distancia() => MisionDirector.Instancia != null ? MisionDirector.Instancia.DistanciaAlObjetivo() : 0f;
            public float Umbral()
            {
                var d = MisionDirector.Instancia;
                switch (d != null ? d.Fase : FaseDeMision.Infiltrar)
                {
                    case FaseDeMision.Infiltrar: return 50f;
                    case FaseDeMision.Resistir: return d != null ? d.RadioResistencia : 32f;
                    case FaseDeMision.Rescatar: return 40f;
                    case FaseDeMision.Escapar: return 60f;
                    default: return 100f;
                }
            }
            public Vector3 Punto() => MisionDirector.Instancia != null ? MisionDirector.Instancia.PuntoObjetivoActual() : Vector3.zero;
        }

        // fuente == null: la del modo Mision. padre == null: la capa Hud_Feedback.
        public static CartelVolverView Crear(IObjetivoParaVolver fuente = null, Transform padre = null)
        {
            if (padre == null)
            {
                var feedback = CapasDeHud.Instancia != null ? CapasDeHud.Instancia.Hud_Feedback : null;
                if (feedback == null) return null;
                padre = feedback.transform;
            }
            if (Actual != null) { Destroy(Actual.gameObject); Actual = null; }

            var go = new GameObject("CartelVolver", typeof(RectTransform), typeof(CartelVolverView));
            go.transform.SetParent(padre, false);
            var view = go.GetComponent<CartelVolverView>();
            view.fuente = fuente ?? new FuenteMision();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.78f);
            rt.anchorMax = new Vector2(0.5f, 0.78f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(520f, 84f);

            // El cuerpo cuelga de un hijo para poder apagarlo entero cuando no hay cartel (el arnes busca textos activos).
            var cuerpo = new GameObject("Cuerpo", typeof(RectTransform));
            cuerpo.transform.SetParent(rt, false);
            var crt = cuerpo.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.sizeDelta = Vector2.zero;
            view.cuerpo = cuerpo;
            view.panel = crt;

            // Fondo transparente (alfa 0,45) para no tapar la vista.
            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(crt, false);
            var fondoRt = fondo.GetComponent<RectTransform>();
            fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
            fondoRt.sizeDelta = Vector2.zero;
            view.fondoImg = fondo.GetComponent<Image>();
            view.fondoImg.raycastTarget = false;
            view.fondoImg.color = new Color(0.1f, 0.05f, 0.05f, 0.45f);

            var font = SP.Presentation.FuentesBelicas.Titulo ?? SP.Presentation.FuentesBelicas.Para(26);
            var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Shadow));
            txtGo.transform.SetParent(crt, false);
            view.texto = txtGo.GetComponent<Text>();
            view.texto.raycastTarget = false;
            view.texto.font = font;
            view.texto.fontSize = 26;
            view.texto.supportRichText = true;
            view.texto.color = new Color(1f, 0.3f, 0.25f);
            view.texto.alignment = TextAnchor.MiddleCenter;
            view.texto.text = Loc.T(TextoCartel);
            var txtRt = view.texto.rectTransform;
            txtRt.anchorMin = new Vector2(0.12f, 0f); txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

            // Flecha hacia el objetivo (triangulo generado, a la izquierda del texto).
            var flGo = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
            flGo.transform.SetParent(crt, false);
            view.flecha = flGo.GetComponent<RectTransform>();
            view.flecha.anchorMin = view.flecha.anchorMax = new Vector2(0.06f, 0.5f);
            view.flecha.anchoredPosition = Vector2.zero;
            view.flecha.sizeDelta = new Vector2(40f, 40f);
            view.flechaImg = flGo.GetComponent<Image>();
            view.flechaImg.raycastTarget = false;
            view.flechaImg.sprite = SpriteTriangulo();
            view.flechaImg.color = new Color(1f, 0.3f, 0.25f);

            view.SetAlpha(0f);
            Actual = view;
            return view;
        }

        static Sprite triangulo;
        static Sprite SpriteTriangulo()
        {
            if (triangulo != null) return triangulo;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "CartelVolver_Flecha", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Punta hacia arriba: el ancho crece de la punta (y = n-1) a la base (y = 0).
                    float semi = (1f - (y / (n - 1f))) * 0.5f * n;
                    bool dentro = Mathf.Abs(x - (n - 1) * 0.5f) <= semi;
                    tex.SetPixel(x, y, dentro ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            tex.Apply(false, true);
            triangulo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return triangulo;
        }

        void OnDestroy() { if (Actual == this) Actual = null; }

        void SetAlpha(float a)
        {
            alpha = a;
            if (cuerpo != null && cuerpo.activeSelf != (a > 0.001f)) cuerpo.SetActive(a > 0.001f);
            if (fondoImg != null) fondoImg.color = new Color(0.1f, 0.05f, 0.05f, 0.45f * a);
            if (texto != null) { var c = texto.color; c.a = a; texto.color = c; }
            if (flechaImg != null) { var c = flechaImg.color; c.a = a; flechaImg.color = c; }
        }

        void Update()
        {
            bool forzado = Time.time < forzarHasta;
            bool activa = fuente != null && fuente.Activa;
            if (!activa && !forzado)
            {
                tiempoAlejandose = 0f;
                if (alpha > 0f) SetAlpha(0f);
                return;
            }

            float dist = fuente != null ? fuente.Distancia() : 0f;
            if (activa)
            {
                float dt = Time.deltaTime;
                if (dist > lastDist + 0.5f * dt) tiempoAlejandose += dt;
                else if (dist < lastDist - 0.5f * dt) tiempoAlejandose = 0f;
            }
            else tiempoAlejandose = 0f;
            lastDist = dist;

            bool debeMostrar = forzado || (tiempoAlejandose >= 4f && dist > fuente.Umbral());
            float targetAlpha = debeMostrar ? 1f : 0f;
            if (Mathf.Abs(alpha - targetAlpha) > 0.01f) SetAlpha(Mathf.MoveTowards(alpha, targetAlpha, Time.unscaledDeltaTime * 4f));
            if (alpha <= 0f) return;

            texto.text = Loc.T(TextoCartel) + (dist > 1f ? $"\n{Mathf.RoundToInt(dist)} m" : "");

            // Pulso
            float scale = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            panel.localScale = Vector3.one * scale;

            // Flecha hacia el objetivo, relativa a la camara.
            var cam = CamaraPrincipal.Actual;
            if (cam != null && fuente != null)
            {
                var yo = PlayerInputDriverPos();
                Vector3 toObj = fuente.Punto() - yo; toObj.y = 0f;
                Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
                if (toObj.sqrMagnitude > 0.1f && camFwd.sqrMagnitude > 0.1f)
                    flecha.localRotation = Quaternion.Euler(0f, 0f, -Vector3.SignedAngle(camFwd.normalized, toObj.normalized, Vector3.up));
            }
        }

        static Vector3 PlayerInputDriverPos()
        {
            var d = SP.Player.PlayerInputDriver.Activo;
            if (d == null || d.Brain == null || d.Brain.Current == null) return Vector3.zero;
            if (d.CurrentSeat.HasValue && d.Vehicle != null) return d.Vehicle.transform.position;
            return d.Brain.Current.transform.position;
        }
    }
}
