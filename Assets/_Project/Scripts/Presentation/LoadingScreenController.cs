using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SP.Presentation
{
    // P11 (#130): lo que MapasDeCarga (Editor) deja en Mapa_<escena>.json: el recorrido de la escena normalizado (0..1, arriba = norte) y sus hitos.
    [System.Serializable]
    public class DatosDelMapa
    {
        public string escena;
        public float aspecto;                  // ancho / alto de la imagen
        public Vector2[] recorrido;
        public int[] indicesDeHitos;
        public string[] etiquetasDeHitos;
        public string[] colores;               // #RRGGBB
        public string leyenda;
        public float metrosX, metrosZ;
    }

    // Un mapa por escena destino (lo arma LoadingSceneBuilder con los PNG/JSON generados).
    [System.Serializable]
    public class MapaDeCarga
    {
        public string escena;
        public Texture2D textura;
        public TextAsset datos;
    }

    // Unico contenido de SC_Loading: una barra que se llena mientras la escena
    // destino (SP.Core.SceneLoader.Destino) se carga en segundo plano, y despues
    // la activa. La referencia a la barra la deja puesta LoadingSceneBuilder al
    // armar la escena (misma convencion que el resto del proyecto: todo por
    // codigo, nada de arrastrar a mano en el Inspector).
    public class LoadingScreenController : MonoBehaviour
    {
        public Image barra;
        public Text textoPorcentaje;

        // P11 (#130): mapa del destino con el recorrido que se va trazando a medida que avanza la barra.
        public MapaDeCarga[] mapas;
        public RectTransform info;               // contenedor de CARGANDO + barra + %: se corre a la derecha si hay mapa
        public RectTransform mapaContenedor;     // marco del mapa (se ajusta a su proporcion)
        public RawImage mapaImagen;
        public RectTransform capaRecorrido;      // hija de mapaContenedor: los puntos se anclan en 0..1
        public Text leyenda;

        public bool MapaVisible { get; private set; }
        public string EscenaDelMapa { get; private set; } = "";
        public Texture TexturaDelMapa => mapaImagen != null ? mapaImagen.texture : null;
        public int PuntosDelRecorrido => puntos.Count;
        public int PuntosRevelados { get; private set; }
        public int Hitos => hitos.Count;
        readonly List<RectTransform> puntos = new List<RectTransform>();
        readonly List<RectTransform> hitos = new List<RectTransform>();
        readonly List<int> hitoEnPunto = new List<int>();
        const float SeparacionDePuntos = 0.022f;   // en alturas del mapa

        // SceneManager tapa el progreso real en 0.9 mientras allowSceneActivation
        // esta en false -- sin este mapeo la barra se queda pegada en 90% varios
        // cuadros y despues salta de golpe a 100 al activarse.
        const float TopeProgresoReal = 0.9f;

        // Pedido explicito: "que inicie la pantalla, espere medio segundo y
        // inicie a cargar" -- la pantalla de CARGANDO aparece de inmediato
        // (Start ya puso 0%), pero SceneManager.LoadSceneAsync recien arranca
        // despues de esta espera, para que el jugador alcance a leer "CARGANDO"
        // antes de que el numero empiece a moverse.
        const float EsperaInicial = 0.5f;

        // Pedido explicito: "que termine de cargar lentamente, quiero que se
        // vea el porcentaje de carga lentamente" -- una escena chica carga de
        // verdad en 1-2 cuadros (SceneManager.LoadSceneAsync.progress salta
        // de 0 a 0.9 casi instantaneo), asi que el numero mostrado NO sigue el
        // progreso real: sube a este ritmo fijo, mas lento que cualquier carga
        // real, y se queda esperando al progreso real solo si este fuera mas
        // lento todavia (nunca se adelanta a la carga de verdad).
        const float DuracionMinimaCarga = 2.6f;

        // Minimo de tiempo que se ve esta pantalla. Si la escena destino ya esta
        // en cache (recargar SC_Gameplay al Reintentar, por ejemplo) la carga
        // tarda menos de un cuadro y la pantalla de carga parpadearia sin que se
        // le pueda leer nada.
        const float MinimoVisibleSegundos = 0.35f;

        void Start()
        {
            // Image.Type.Filled no recorta sin sprite: la barra se veia llena desde el 0 %.
            if (barra != null && barra.sprite == null) barra.sprite = Cuadrado();
            StartCoroutine(CargarEscenaDestino());
        }

        static Sprite cuadrado;
        static Sprite Cuadrado()
        {
            if (cuadrado != null) return cuadrado;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            cuadrado = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return cuadrado;
        }

        IEnumerator CargarEscenaDestino()
        {
            string destino = SP.Core.SceneLoader.Destino;
            // Nadie deberia entrar a SC_Loading sin pasar por SceneLoader.Cargar
            // (F5 directo sobre esta escena en el Editor, por ejemplo), pero si
            // pasa, mejor caer al menu que quedarse trabado en una barra que
            // nunca arranca.
            if (string.IsNullOrEmpty(destino)) destino = "SC_MainMenu";

            PrepararMapa(destino);
            SetProgreso(0f);
            yield return new WaitForSecondsRealtime(EsperaInicial);

            float inicio = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(destino);
            op.allowSceneActivation = false;

            float t = 0f;
            while (t < 1f || op.progress < TopeProgresoReal)
            {
                t = Mathf.Min(1f, t + Mathf.Min(Time.unscaledDeltaTime, 0.1f) / DuracionMinimaCarga);   // P11: un tiron de carga no salta el trazo del mapa
                SetProgreso(Mathf.Min(t, op.progress / TopeProgresoReal));
                yield return null;
            }
            SetProgreso(1f);

            float faltante = MinimoVisibleSegundos - (Time.unscaledTime - inicio);
            if (faltante > 0f) yield return new WaitForSecondsRealtime(faltante);

            op.allowSceneActivation = true;
        }

        void SetProgreso(float t)
        {
            t = Mathf.Clamp01(t);
            if (barra != null) barra.fillAmount = t;
            if (textoPorcentaje != null) textoPorcentaje.text = Mathf.RoundToInt(t * 100f) + "%";
            ActualizarRecorrido(t);
        }

        // ---------------------------------------------------------------- mapa (#130)
        static Sprite circulo;
        static Sprite Circulo()
        {
            if (circulo != null) return circulo;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f)));
            }
            tex.Apply();
            circulo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return circulo;
        }

        void PrepararMapa(string destino)
        {
            MapaVisible = false; EscenaDelMapa = "";
            MapaDeCarga elegido = null;
            if (mapas != null) foreach (var m in mapas) if (m != null && m.escena == destino && m.textura != null) { elegido = m; break; }
            if (mapaContenedor != null) mapaContenedor.gameObject.SetActive(elegido != null);
            if (leyenda != null) leyenda.gameObject.SetActive(false);
            if (elegido == null || mapaImagen == null) { if (info != null) info.anchoredPosition = Vector2.zero; return; }

            DatosDelMapa d = null;
            if (elegido.datos != null) { try { d = JsonUtility.FromJson<DatosDelMapa>(elegido.datos.text); } catch { d = null; } }
            float aspecto = d != null && d.aspecto > 0.05f ? d.aspecto : (float)elegido.textura.width / elegido.textura.height;
            mapaImagen.texture = elegido.textura;
            // El marco cabe en 470 x 410 (referencia 960x540) con la proporcion del mapa.
            const float maxW = 470f, maxH = 410f;
            float h = maxH, w = h * aspecto;
            if (w > maxW) { w = maxW; h = w / aspecto; }
            mapaContenedor.anchorMin = mapaContenedor.anchorMax = new Vector2(0.5f, 0.5f);
            mapaContenedor.anchoredPosition = new Vector2(-215f, 8f);
            mapaContenedor.sizeDelta = new Vector2(w + 8f, h + 8f);   // + borde
            if (info != null) info.anchoredPosition = new Vector2(250f, 20f);
            EscenaDelMapa = elegido.escena; MapaVisible = true;
            if (leyenda != null)
            {
                leyenda.gameObject.SetActive(d != null && !string.IsNullOrEmpty(d.leyenda));
                if (d != null) leyenda.text = d.leyenda;
            }
            ConstruirRecorrido(d, aspecto);
        }

        void ConstruirRecorrido(DatosDelMapa d, float aspecto)
        {
            foreach (var p in puntos) if (p != null) Destroy(p.gameObject);
            foreach (var p in hitos) if (p != null) Destroy(p.gameObject);
            puntos.Clear(); hitos.Clear(); hitoEnPunto.Clear(); PuntosRevelados = 0;
            if (d == null || d.recorrido == null || d.recorrido.Length < 2 || capaRecorrido == null) return;
            // Puntos equiespaciados a lo largo de la polilinea (la distancia se mide con la proporcion real del mapa).
            float acum = 0f, siguiente = 0f;
            var posiciones = new List<Vector2>();
            var tramoInicio = new List<int>();   // indice del primer punto que cae a partir de cada vertice del recorrido
            for (int i = 0; i < d.recorrido.Length - 1; i++)
            {
                var a = d.recorrido[i]; var b = d.recorrido[i + 1];
                float len = Vector2.Distance(new Vector2(a.x * aspecto, a.y), new Vector2(b.x * aspecto, b.y));
                tramoInicio.Add(posiciones.Count);
                while (siguiente <= acum + len)
                {
                    float f = len > 1e-5f ? (siguiente - acum) / len : 0f;
                    posiciones.Add(Vector2.Lerp(a, b, f));
                    siguiente += SeparacionDePuntos;
                }
                acum += len;
            }
            tramoInicio.Add(posiciones.Count);
            posiciones.Add(d.recorrido[d.recorrido.Length - 1]);
            foreach (var pos in posiciones) puntos.Add(NuevoPunto(pos, 5f, new Color(1f, 0.92f, 0.45f, 0.95f), "Punto"));

            if (d.indicesDeHitos != null)
                for (int k = 0; k < d.indicesDeHitos.Length; k++)
                {
                    int iv = Mathf.Clamp(d.indicesDeHitos[k], 0, d.recorrido.Length - 1);
                    var pos = d.recorrido[iv];
                    Color c = Color.white;
                    if (d.colores != null && k < d.colores.Length) ColorUtility.TryParseHtmlString(d.colores[k], out c);
                    var marca = NuevoPunto(pos, 15f, c, "Hito");
                    var etq = new GameObject("Etiqueta", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                    etq.transform.SetParent(marca, false);
                    etq.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    etq.text = d.etiquetasDeHitos != null && k < d.etiquetasDeHitos.Length ? d.etiquetasDeHitos[k] : "";
                    etq.fontSize = 12; etq.fontStyle = FontStyle.Bold; etq.color = Color.white; etq.raycastTarget = false;
                    etq.horizontalOverflow = HorizontalWrapMode.Overflow; etq.verticalOverflow = VerticalWrapMode.Overflow;
                    // Etiqueta del lado donde hay lugar (si el hito esta a la derecha del mapa, va a la izquierda).
                    bool izq = pos.x > 0.55f;
                    etq.alignment = izq ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                    var ert = etq.rectTransform; ert.sizeDelta = new Vector2(150f, 18f);
                    ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f); ert.pivot = new Vector2(izq ? 1f : 0f, 0.5f);
                    // Hitos pegados (INICIO / HELICOPTERO en la base): la etiqueta baja para no pisar la del anterior.
                    int cercanos = 0;
                    for (int q = 0; q < k && q < d.indicesDeHitos.Length; q++)
                    {
                        var otro = d.recorrido[Mathf.Clamp(d.indicesDeHitos[q], 0, d.recorrido.Length - 1)];
                        if (Vector2.Distance(new Vector2(otro.x * aspecto, otro.y), new Vector2(pos.x * aspecto, pos.y)) < 0.09f) cercanos++;
                    }
                    ert.anchoredPosition = new Vector2(izq ? -13f : 13f, -16f * cercanos);
                    var sombra = etq.gameObject.AddComponent<Shadow>(); sombra.effectColor = new Color(0f, 0f, 0f, 0.9f); sombra.effectDistance = new Vector2(1f, -1f);
                    hitos.Add(marca);
                    int ip = iv < tramoInicio.Count ? Mathf.Min(tramoInicio[iv], posiciones.Count - 1) : posiciones.Count - 1;
                    hitoEnPunto.Add(ip);
                }
            ActualizarRecorrido(0f);
        }

        RectTransform NuevoPunto(Vector2 norm, float px, Color c, string nombre)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(capaRecorrido, false);
            var im = go.GetComponent<Image>(); im.sprite = Circulo(); im.color = c; im.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = norm; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(px, px);
            return rt;
        }

        // El trazo avanza con la barra: los puntos hasta t*N se ven; los hitos estan siempre, apagados hasta que el trazo llega.
        void ActualizarRecorrido(float t)
        {
            if (puntos.Count == 0) return;
            int n = Mathf.Clamp(Mathf.CeilToInt(t * puntos.Count), 0, puntos.Count);
            PuntosRevelados = n;
            for (int i = 0; i < puntos.Count; i++) if (puntos[i] != null) puntos[i].gameObject.SetActive(i < n);
            for (int k = 0; k < hitos.Count; k++)
            {
                if (hitos[k] == null) continue;
                bool alcanzado = hitoEnPunto[k] < n || t >= 0.999f;
                var im = hitos[k].GetComponent<Image>();
                var c = im.color; c.a = alcanzado ? 1f : 0.45f; im.color = c;
                hitos[k].localScale = Vector3.one * (alcanzado ? 1f : 0.8f);
            }
        }

    }
}
