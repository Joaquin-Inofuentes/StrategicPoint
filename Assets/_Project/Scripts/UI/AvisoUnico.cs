using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // EL UNICO lugar de la pantalla donde aparecen los textos sorpresivos (abajo al centro). Antes cada vista ponia su cartel donde
    // queria (arriba, al centro, bajo la mira...) y se pisaban entre si. Ahora todas (avisos de fase, modo, racha, headshot, muertos,
    // "[E] subir", temporizadores, instrucciones...) llaman a esta clase y se muestra UN solo texto, normalizado: misma letra, mismo
    // tamano, mayusculas, una sola linea (los saltos de linea pasan a " · ") y sin etiquetas de formato.
    //
    //   Mostrar(texto, segundos, color, prioridad)  aviso temporal. Uno a la vez: una prioridad mayor corta al actual, igual o menor espera en cola.
    //   Fijo(clave, texto, color)                  texto que vale mientras se lo siga pidiendo (cada frame) o hasta Fijo(clave, null). Se ve solo si no hay aviso temporal.
    public sealed class AvisoUnico : MonoBehaviour
    {
        public const int PrioridadBaja = 0, PrioridadMedia = 1, PrioridadAlta = 2, PrioridadCritica = 3;
        const float VidaDeUnFijo = 0.4f, EsperaMaximaEnCola = 6f, PermanenciaMinima = 0.9f, AlturaBase = 140f;   // Bug #173: AlturaBase sube la caja sobre la barra de unidades y la etiqueta del asiento
        const int MaximoEnCola = 6;

        struct Aviso { public string texto; public Color color; public float segundos, encolado; public int prioridad; public bool vibra; }
        struct Fijado { public string texto; public Color color; public float vence; public int orden; public bool vibra; }

        static AvisoUnico instancia;
        static readonly List<Aviso> cola = new List<Aviso>();
        static readonly Dictionary<string, Fijado> fijos = new Dictionary<string, Fijado>();
        static readonly List<string> historial = new List<string>();
        static int ordenFijo;
        static readonly Regex etiquetas = new Regex("<[^>]+>", RegexOptions.Compiled);
        static readonly Regex espacios = new Regex(@"\s*\n+\s*", RegexOptions.Compiled);

        Aviso actual; float actualHasta = -1f;
        Text texto; Image fondo; CanvasGroup grupo; RectTransform caja;
        string mostrado = ""; float alfa, cambioDeTexto = -10f; bool mostradoEraTemporal;

        public static string TextoActual => instancia != null && instancia.grupo != null && instancia.alfa > 0.05f ? instancia.mostrado : "";
        public static bool Visible => TextoActual.Length > 0;
        public static IReadOnlyList<string> Historial => historial;
        public static bool HayTemporal => instancia != null && Time.unscaledTime < instancia.actualHasta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; cola.Clear(); fijos.Clear(); historial.Clear(); ordenFijo = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear() { if (Application.isPlaying) Asegurar(); }

        static AvisoUnico Asegurar()
        {
            if (instancia != null) return instancia;
            if (!Application.isPlaying) return null;
            var go = new GameObject("AvisoUnico");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<AvisoUnico>();
            return instancia;
        }

        // "OPERACION CUARTEL\nINFILTRATE" -> "OPERACION CUARTEL · INFILTRATE"; sin etiquetas <size>/<color>; en mayusculas.
        public static string Normalizar(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = etiquetas.Replace(t, "");
            t = espacios.Replace(t.Trim(), " · ");
            return t.ToUpperInvariant();
        }

        // Pedido de pulido: sin textos en pantalla salvo el de mision. Solo se apaga el DIBUJO: la cola, el historial y TextoActual siguen igual.
        public static bool Dibujar = true;

        // Bug #308: un aviso temporal que pulsa y tiembla ("debes acercarte y apretar E").
        public static void MostrarVibrante(string mensaje, float segundos = 2.5f, Color? color = null, int prioridad = PrioridadCritica)
            => Mostrar(mensaje, segundos, color ?? new Color(1f, 0.92f, 0.55f), prioridad, true);

        public static void Mostrar(string mensaje, float segundos = 3f, Color? color = null, int prioridad = PrioridadMedia, bool vibra = false)
        {
            string t = Normalizar(mensaje);
            if (t.Length == 0) return;
            historial.Add(t); if (historial.Count > 64) historial.RemoveAt(0);
            var a = new Aviso { texto = t, color = color ?? Color.white, segundos = Mathf.Max(0.8f, segundos), encolado = Time.unscaledTime, prioridad = prioridad, vibra = vibra };
            var v = Asegurar();
            if (v == null) return;
            // Mismo texto ya en pantalla o en espera: se refresca en vez de repetir.
            if (Time.unscaledTime < v.actualHasta && v.actual.texto == t) { v.actualHasta = Time.unscaledTime + a.segundos; return; }
            for (int i = 0; i < cola.Count; i++) if (cola[i].texto == t) { cola[i] = a; return; }
            if (Time.unscaledTime >= v.actualHasta || prioridad > v.actual.prioridad) { v.Empezar(a); return; }
            if (cola.Count >= MaximoEnCola)
            {
                int peor = 0;
                for (int i = 1; i < cola.Count; i++) if (cola[i].prioridad < cola[peor].prioridad) peor = i;
                if (cola[peor].prioridad >= prioridad) return;
                cola.RemoveAt(peor);
            }
            cola.Add(a);
        }

        public static void Fijo(string clave, string mensaje, Color? color = null)
        {
            if (string.IsNullOrEmpty(clave)) return;
            string t = Normalizar(mensaje);
            if (t.Length == 0) { fijos.Remove(clave); return; }
            Asegurar();
            ordenFijo++;
            fijos[clave] = new Fijado { texto = t, color = color ?? new Color(1f, 0.92f, 0.55f), vence = Time.unscaledTime + VidaDeUnFijo, orden = ordenFijo };
        }

        // Un fijo que no se renueva cada frame (instrucciones del tutorial): dura hasta que se lo quite.
        public static void FijoPermanente(string clave, string mensaje, Color? color = null)
        {
            Fijo(clave, mensaje, color);
            if (fijos.TryGetValue(clave, out var f)) { f.vence = float.PositiveInfinity; fijos[clave] = f; }
        }

        // Bug #161: "si me alejo un cartel vibrante abajo diga que debo volver, el mismo y unico cartel de textos": un fijo que pulsa y tiembla.
        public static void FijoVibrante(string clave, string mensaje, Color? color = null)
        {
            Fijo(clave, mensaje, color);
            if (fijos.TryGetValue(clave, out var f)) { f.vibra = true; fijos[clave] = f; }
        }

        public static void QuitarFijo(string clave) { if (clave != null) fijos.Remove(clave); }
        public static void Limpiar() { cola.Clear(); fijos.Clear(); if (instancia != null) instancia.actualHasta = -1f; }

        void Empezar(Aviso a) { actual = a; actualHasta = Time.unscaledTime + a.segundos; }

        void Awake()
        {
            if (instancia != null && instancia != this) { Destroy(gameObject); return; }
            instancia = this;
            Construir();
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        void Construir()
        {
            var c = gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 860;
            var sc = gameObject.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;

            var cajaGo = new GameObject("Caja", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            cajaGo.transform.SetParent(transform, false);
            caja = cajaGo.GetComponent<RectTransform>();
            caja.anchorMin = caja.anchorMax = new Vector2(0.5f, 0f);
            caja.pivot = new Vector2(0.5f, 0f);
            caja.anchoredPosition = new Vector2(0f, AlturaBase);
            caja.sizeDelta = new Vector2(1200f, 64f);
            fondo = cajaGo.GetComponent<Image>();
            fondo.color = new Color(0.03f, 0.04f, 0.06f, 0.72f);
            fondo.raycastTarget = false;
            grupo = cajaGo.GetComponent<CanvasGroup>();
            grupo.alpha = 0f; grupo.interactable = false; grupo.blocksRaycasts = false;

            var tGo = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Outline));
            tGo.transform.SetParent(cajaGo.transform, false);
            texto = tGo.GetComponent<Text>();
            var fuente = SP.Presentation.FuentesBelicas.Texto;
            texto.font = fuente != null ? fuente : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 30; texto.fontStyle = FontStyle.Bold;
            texto.alignment = TextAnchor.MiddleCenter; texto.raycastTarget = false;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap; texto.verticalOverflow = VerticalWrapMode.Truncate;
            texto.resizeTextForBestFit = true; texto.resizeTextMinSize = 18; texto.resizeTextMaxSize = 30;
            var rt = texto.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(24f, 4f); rt.offsetMax = new Vector2(-24f, -4f);
            var o = tGo.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.95f); o.effectDistance = new Vector2(2f, -2f);
        }

        void Update()
        {
            if (texto == null) return;
            float ahora = Time.unscaledTime;
            if (ahora >= actualHasta)
            {
                // El siguiente de la cola: mayor prioridad primero, y entre iguales el mas viejo; lo que espero demasiado ya no sirve.
                int mejor = -1;
                for (int i = cola.Count - 1; i >= 0; i--)
                {
                    if (cola[i].prioridad < PrioridadCritica && ahora - cola[i].encolado > EsperaMaximaEnCola) { cola.RemoveAt(i); continue; }
                }
                for (int i = 0; i < cola.Count; i++)
                    if (mejor < 0 || cola[i].prioridad > cola[mejor].prioridad) mejor = i;
                if (mejor >= 0) { var a = cola[mejor]; cola.RemoveAt(mejor); Empezar(a); }
            }

            string t = ""; Color col = Color.white; bool temporal = ahora < actualHasta; bool vibrando = false;
            if (temporal) { t = actual.texto; col = actual.color; vibrando = actual.vibra; }
            else if (fijos.Count > 0)
            {
                // Varios fijos a la vez ("[E] subir" + "TRAYECTO 40"): se unen en el mismo renglon, el mas reciente primero.
                var vivos = new List<string>(); int mejorOrden = -1;
                List<string> vencidos = null;
                foreach (var kv in fijos)
                {
                    if (ahora > kv.Value.vence) { (vencidos ??= new List<string>()).Add(kv.Key); continue; }
                    vivos.Add(kv.Key);
                    if (kv.Value.orden > mejorOrden) { mejorOrden = kv.Value.orden; col = kv.Value.color; }
                }
                if (vencidos != null) foreach (var k in vencidos) fijos.Remove(k);
                vivos.Sort((a, b) => fijos[b].orden.CompareTo(fijos[a].orden));
                if (vivos.Count > 0)
                {
                    // Bug #167 ("el texto de abajo al centro se bugeo, se solapan 2 textos"): dos fijos largos juntos armaban 3 renglones. Se unen solo si
                    // entran los dos en una linea corta; si no, se muestra el mas reciente solo.
                    vibrando = fijos[vivos[0]].vibra;
                    string primero = fijos[vivos[0]].texto;
                    if (vivos.Count > 1 && !vibrando && primero.Length + fijos[vivos[1]].texto.Length <= 70) t = primero + "  ·  " + fijos[vivos[1]].texto;
                    else t = primero;
                }
            }

            // Bug #173 ("el texto de abajo parpadea, hay 2 textos intentando imprimirse"): dos fijos que se renuevan en cuadros alternados, o un
            // temporal que vence justo cuando otro fijo cambia de orden, hacian que la caja saltara entre dos textos cada 1-2 cuadros. Un texto que
            // ya se esta mostrando se queda al menos PermanenciaMinima s; solo lo corta antes uno temporal de mayor prioridad (que ya empezo arriba).
            if (t.Length > 0 && mostrado.Length > 0 && t != mostrado && alfa > 0.05f && ahora - cambioDeTexto < PermanenciaMinima
                && !(temporal && actual.prioridad >= PrioridadAlta && ahora - actual.encolado < 0.05f))
            {
                bool sigueVigente = false;
                if (mostradoEraTemporal) sigueVigente = false;
                else foreach (var kv in fijos) if (ahora <= kv.Value.vence && (kv.Value.texto == mostrado || mostrado.Contains(kv.Value.texto))) { sigueVigente = true; break; }
                if (sigueVigente) { t = mostrado; col = texto.color; vibrando = false; }
            }
            if (t.Length > 0)
            {
                if (mostrado != t) { mostrado = t; cambioDeTexto = ahora; mostradoEraTemporal = temporal; }
                if (texto.text != t) texto.text = t;
                texto.color = col;
            }
            float objetivo = t.Length > 0 ? 1f : 0f;
            alfa = Mathf.MoveTowards(alfa, objetivo, Time.unscaledDeltaTime * (objetivo > alfa ? 8f : 2.5f));
            grupo.alpha = Dibujar ? alfa : 0f;   // pedido de pulido: sin cartel en pantalla; la cola y TextoActual siguen funcionando
            if (vibrando && t.Length > 0)
            {
                caja.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(ahora * 13f));
                caja.anchoredPosition = new Vector2(Mathf.Sin(ahora * 47f) * 4f, AlturaBase + Mathf.Sin(ahora * 53f) * 2.5f);
                texto.color = Color.Lerp(col, new Color(1f, 0.85f, 0.7f), 0.5f + 0.5f * Mathf.Sin(ahora * 9f));
            }
            else { caja.localScale = Vector3.one; caja.anchoredPosition = new Vector2(0f, AlturaBase); }
            if (alfa <= 0.001f && mostrado.Length > 0 && t.Length == 0) mostrado = "";
            // El cuadro se ajusta al largo del texto (hasta 1500 px), no queda una barra enorme para tres palabras.
            if (t.Length > 0) caja.sizeDelta = new Vector2(Mathf.Clamp(texto.preferredWidth + 80f, 320f, 1500f), t.Length > 62 ? 92f : 64f);
        }
    }
}
