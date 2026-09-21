using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.Presentation
{
    // Ronda 13 (puntos 2 y 3). Dibuja lo que reporta AccionesEnCurso:
    //  * una BARRA DE CARGA en el mundo, en el punto medio entre el soldado y lo que esta usando (caido, muro, herido),
    //    con el tiempo que falta en texto ("2.3 s") y una linea fina que une a los dos;
    //  * el texto de ESTADO abajo a la izquierda, sobre la lista de la escuadra: la accion actual del soldado que
    //    manejas ("Reviviendo", "Detonando"...) y, debajo, las de tus aliados.
    // Se crea sola al cargar cada escena (no hay que cablearla): un unico objeto por juego.
    public class AccionesEnCursoView : MonoBehaviour
    {
        public const float AnchoBarraMundo = 1.7f;
        public const float DistanciaMaxima = 70f;
        public const float SeparacionMaximaDelActor = 5f;   // si el objetivo esta lejos (afinar punteria) la barra no se va a 40 m
        const int LineasMaximas = 3;

        sealed class BarraMundo
        {
            public GameObject raiz;
            public RectTransform rt;
            public Canvas canvas;
            public Image relleno;
            public Text tiempo;
            public Text verbo;
            public LineRenderer linea;
        }

        static AccionesEnCursoView instancia;
        public static AccionesEnCursoView Instancia => instancia;

        readonly List<BarraMundo> pool = new List<BarraMundo>();
        readonly List<AccionesEnCurso.Accion> acciones = new List<AccionesEnCurso.Accion>();
        Canvas hud;
        RectTransform hudRaiz;
        Text[] lineas;
        Font fuente;
        Material matLinea;

        public int BarrasVisibles { get; private set; }
        public string TextoDeEstado => lineas != null && lineas[0] != null && lineas[0].gameObject.activeSelf ? lineas[0].text : "";
        public string TextoDeEstadoCompleto
        {
            get
            {
                if (lineas == null) return "";
                var sb = new System.Text.StringBuilder();
                foreach (var l in lineas) if (l != null && l.gameObject.activeSelf) sb.AppendLine(l.text);
                return sb.ToString().TrimEnd();
            }
        }
        public bool HudVisible => hud != null && hud.enabled && lineas[0].gameObject.activeSelf;
        public RectTransform RaizDelHud => hudRaiz;
        public IReadOnlyList<Vector3> PosicionesDeBarras
        {
            get
            {
                var l = new List<Vector3>();
                foreach (var b in pool) if (b.raiz.activeSelf) l.Add(b.raiz.transform.position);
                return l;
            }
        }
        public float RellenoDeLaPrimeraBarra
        {
            get { foreach (var b in pool) if (b.raiz.activeSelf) return b.relleno.fillAmount; return -1f; }
        }
        public string TiempoDeLaPrimeraBarra
        {
            get { foreach (var b in pool) if (b.raiz.activeSelf) return b.tiempo.text; return null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (!Application.isPlaying) return;
            Asegurar();
        }

        public static AccionesEnCursoView Asegurar()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("AccionesEnCursoView");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<AccionesEnCursoView>();
            return instancia;
        }

        void Awake()
        {
            if (instancia != null && instancia != this) { Destroy(gameObject); return; }
            instancia = this;
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ConstruirHud();
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        // ---------------- HUD de estado (abajo a la izquierda) ----------------
        void ConstruirHud()
        {
            var go = new GameObject("EstadoDeAccionCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            hud = go.GetComponent<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
            hud.sortingOrder = 40;
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1280f, 720f);
            cs.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("EstadoDeAccion", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            hudRaiz = (RectTransform)panel.transform;
            hudRaiz.anchorMin = hudRaiz.anchorMax = hudRaiz.pivot = new Vector2(0f, 0f);
            // Justo encima de la lista de la escuadra (que ocupa los ~75 px de abajo).
            hudRaiz.anchoredPosition = new Vector2(22f, 84f);
            hudRaiz.sizeDelta = new Vector2(420f, 24f * LineasMaximas);

            lineas = new Text[LineasMaximas];
            for (int i = 0; i < LineasMaximas; i++)
            {
                var t = new GameObject("Linea" + i, typeof(RectTransform), typeof(Text), typeof(Shadow)).GetComponent<Text>();
                t.transform.SetParent(panel.transform, false);
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
                // La linea 0 (la del jugador) va abajo, pegada a la escuadra; las demas suben.
                r.anchoredPosition = new Vector2(0f, i * 24f);
                r.sizeDelta = new Vector2(420f, 24f);
                t.font = fuente;
                t.fontSize = i == 0 ? 20 : 16;
                t.fontStyle = i == 0 ? FontStyle.Bold : FontStyle.Normal;
                t.alignment = TextAnchor.LowerLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.color = i == 0 ? new Color(1f, 0.86f, 0.35f) : new Color(0.85f, 0.92f, 0.85f);
                var sh = t.GetComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
                sh.effectDistance = new Vector2(1.5f, -1.5f);
                t.gameObject.SetActive(false);
                lineas[i] = t;
            }
        }

        // ---------------- barras en el mundo ----------------
        BarraMundo NuevaBarra()
        {
            var go = new GameObject("BarraDeAccion", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var b = new BarraMundo { raiz = go, canvas = go.GetComponent<Canvas>(), rt = (RectTransform)go.transform };
            b.canvas.renderMode = RenderMode.WorldSpace;
            b.rt.sizeDelta = new Vector2(340f, 64f);
            go.transform.localScale = Vector3.one * (AnchoBarraMundo / 340f);

            Imagen("Fondo", go.transform, new Color(0f, 0f, 0f, 0.72f), new Vector2(0f, 0f), new Vector2(1f, 0.42f), 0f);
            b.relleno = Imagen("Relleno", go.transform, new Color(0.35f, 0.9f, 0.45f), new Vector2(0f, 0f), new Vector2(1f, 0.42f), 3f);
            b.relleno.sprite = SP.UI.SpriteBlanco.Obtener();
            b.relleno.type = Image.Type.Filled;
            b.relleno.fillMethod = Image.FillMethod.Horizontal;
            b.relleno.fillOrigin = (int)Image.OriginHorizontal.Left;

            b.verbo = Texto("Verbo", go.transform, 24, TextAnchor.LowerLeft, new Vector2(0f, 0.42f), new Vector2(0.62f, 1f));
            b.tiempo = Texto("Tiempo", go.transform, 28, TextAnchor.LowerRight, new Vector2(0.55f, 0.42f), new Vector2(1f, 1f));

            var lg = new GameObject("Union", typeof(LineRenderer));
            lg.transform.SetParent(go.transform, false);
            b.linea = lg.GetComponent<LineRenderer>();
            b.linea.useWorldSpace = true;
            b.linea.positionCount = 2;
            b.linea.startWidth = b.linea.endWidth = 0.025f;
            b.linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.linea.receiveShadows = false;
            if (matLinea == null) matLinea = SafeMaterial.Create(new Color(1f, 0.95f, 0.6f, 0.9f));
            b.linea.sharedMaterial = matLinea;

            pool.Add(b);
            return b;
        }

        static Image Imagen(string nombre, Transform padre, Color c, Vector2 anchorMin, Vector2 anchorMax, float margen)
        {
            var g = new GameObject(nombre, typeof(Image));
            g.transform.SetParent(padre, false);
            var img = g.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = new Vector2(margen, margen); r.offsetMax = new Vector2(-margen, -margen);
            return img;
        }

        Text Texto(string nombre, Transform padre, int tam, TextAnchor ancla, Vector2 anchorMin, Vector2 anchorMax)
        {
            var g = new GameObject(nombre, typeof(Text), typeof(Shadow));
            g.transform.SetParent(padre, false);
            var t = g.GetComponent<Text>();
            t.font = fuente; t.fontSize = tam; t.fontStyle = FontStyle.Bold;
            t.alignment = ancla; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.rectTransform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = r.offsetMax = Vector2.zero;
            var sh = g.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.9f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        void LateUpdate() => Refrescar();

        // Publico: la suite lo ejerce sin esperar un frame.
        public void Refrescar()
        {
            AccionesEnCurso.Vigentes(acciones);
            var cam = CamaraPrincipal.Actual;
            int usadas = 0;

            for (int i = 0; i < acciones.Count; i++)
            {
                var a = acciones[i];
                if (a.Actor == null || !a.Actor.gameObject.activeInHierarchy) continue;
                var desde = a.Actor.transform.position + Vector3.up * 1.15f;
                var hasta = a.Punto;
                var v = hasta - desde;
                if (v.magnitude > SeparacionMaximaDelActor) hasta = desde + v.normalized * SeparacionMaximaDelActor;
                var medio = (desde + hasta) * 0.5f + Vector3.up * 0.35f;

                if (cam != null && (cam.transform.position - medio).sqrMagnitude > DistanciaMaxima * DistanciaMaxima) continue;

                var b = usadas < pool.Count ? pool[usadas] : NuevaBarra();
                usadas++;
                if (!b.raiz.activeSelf) b.raiz.SetActive(true);
                b.raiz.transform.position = medio;
                if (cam != null) b.raiz.transform.rotation = cam.transform.rotation;
                b.relleno.fillAmount = a.Progreso01;
                b.verbo.text = Loc.T(a.VerboEs);
                b.tiempo.text = a.Restante.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
                b.linea.SetPosition(0, desde);
                b.linea.SetPosition(1, hasta);
            }
            for (int i = usadas; i < pool.Count; i++) if (pool[i].raiz.activeSelf) pool[i].raiz.SetActive(false);
            BarrasVisibles = usadas;

            RefrescarHud();
        }

        void RefrescarHud()
        {
            if (lineas == null) return;
            var propio = AjustesDeEscuadra.Lider;
            int n = 0;

            // 1) la accion del soldado que manejas
            if (propio != null && AccionesEnCurso.De(propio, out var mia))
                Poner(n++, Linea(mia, true));

            // 2) las de tus aliados (IA), las mas urgentes primero no importa: son pocas
            for (int i = 0; i < acciones.Count && n < LineasMaximas; i++)
            {
                var a = acciones[i];
                if (a.Actor == null || a.Actor == propio || a.Actor.Team != TeamId.Player) continue;
                Poner(n++, Linea(a, false));
            }

            for (int i = n; i < lineas.Length; i++) if (lineas[i].gameObject.activeSelf) lineas[i].gameObject.SetActive(false);
        }

        // Texto de estado de una accion: "▶ REVIVIENDO · 2.3 s" (la mia) o "REVIVIENDO · Kes · 2.3 s" (un aliado). Publico y estatico
        // para que la suite lo ejerza sin necesidad de Play.
        public static string Linea(in AccionesEnCurso.Accion a, bool propia)
        {
            string tiempo = a.Restante.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
            return propia ? "▶ " + Loc.T(a.VerboEs) + " · " + tiempo
                          : Loc.T(a.VerboEs) + " · " + (a.Actor != null ? a.Actor.DisplayName : "?") + " · " + tiempo;
        }

        void Poner(int i, string texto)
        {
            if (i >= lineas.Length) return;
            if (!lineas[i].gameObject.activeSelf) lineas[i].gameObject.SetActive(true);
            lineas[i].text = texto;
        }
    }
}
