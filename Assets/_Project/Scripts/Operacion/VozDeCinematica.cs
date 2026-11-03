using System.Collections.Generic;
using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // Voz del COMANDANTE en las cinematicas de apertura (sobrevuelo + rapel). Cada linea es un clip aparte en Resources/Audio/Cine/<id>
    // (mp3/wav/ogg): se dispara cuando la camara llega a su cuadro clave (sobrevuelo) o en su segundo exacto (rapel), asi que sigue pegada
    // al video aunque se retoque la velocidad de la camara. Si el clip no existe, la linea se salta sin error (la cinematica corre muda).
    //
    // `Ventana` es el tiempo MAXIMO que tiene la linea antes de que arranque la siguiente (o termine el cuadro): Validar() avisa si un clip
    // real se pasa, para recortar el texto o acelerar la voz. El guion completo con los tiempos esta en Docs/GuionDelComandante.md.
    public static class VozDeCinematica
    {
        public struct Linea
        {
            public string Id;        // nombre del clip en Resources/Audio/Cine
            public string Texto;     // lo que dice (para generar el audio y para las pruebas)
            public float Ventana;    // segundos disponibles
            public Linea(string id, float ventana, string texto) { Id = id; Ventana = ventana; Texto = texto; }
        }

        public static readonly Linea Inicio = new Linea("cmd_01_inicio", 6.5f,
            "Escuadra, habla el comandante. Esta noche entramos al cuartel enemigo.");
        public static readonly Linea Torretas = new Linea("cmd_02_torretas", 9.5f,
            "Primero, las dos torretas de artillería. Si siguen en pie no llegamos al centro. Cargas explosivas, y rápido.");
        public static readonly Linea Cuartel = new Linea("cmd_03_cuartel", 5.4f,
            "Después, limpien el cuartel. No dejen a nadie con vida.");
        public static readonly Linea CentroDeDatos = new Linea("cmd_04_centro", 7.8f,
            "Luego, dos puestos y el centro de datos. Asalto coloca las cargas, los demás cubren.");
        public static readonly Linea Blindado = new Linea("cmd_05_blindado", 6.0f,
            "Un tanque jefe custodia el patio. Derríbenlo y escapen en él.");
        public static readonly Linea Ciudad = new Linea("cmd_06_ciudad", 6.4f,
            "En la ciudad, tomen la radio y defiendan. Dos posiciones alcanzan.");
        public static readonly Linea Helipuerto = new Linea("cmd_07_helipuerto", 5.5f,
            "Al final, el helicóptero. Suban todos. Comandante fuera.");
        public static readonly Linea RapelPreparar = new Linea("cmd_08_rapel_a", 5.4f,
            "Zona de descenso a la vista. Prepárense para el rapel.");
        public static readonly Linea RapelBajar = new Linea("cmd_09_rapel_b", 5.9f,
            "¡Abajo, abajo! Suerte, escuadra. Los estoy mirando.");

        public static readonly Linea[] Todas = { Inicio, Torretas, Cuartel, CentroDeDatos, Blindado, Ciudad, Helipuerto, RapelPreparar, RapelBajar };


        public static readonly List<string> Reproducidas = new List<string>();
        public static readonly List<string> Faltantes = new List<string>();
        public static string Actual { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { vista = null; Reproducidas.Clear(); Faltantes.Clear(); Actual = ""; }

        // Ya no hay voz grabada ni sintetizada: cada linea se muestra como DIALOGO escrito abajo (caja con el nombre de quien habla),
        // el tiempo que dura su ventana. Se mantiene el nombre de la clase y de Decir para no tocar las cinematicas que la llaman.
        public static AudioClip Clip(Linea l) => null;

        static DialogoView vista;

        // La linea anterior se reemplaza si la nueva arranca (nunca se encimen dos cajas).
        public static bool Decir(Linea l)
        {
            if (vista == null) vista = DialogoView.Crear();
            vista.Mostrar("COMANDANTE", l.Texto, l.Ventana);
            Actual = l.Id;
            Reproducidas.Add(l.Id);
            return true;
        }

        public static void Callar() { if (vista != null) vista.Ocultar(); Actual = ""; }
        public static bool Hablando => vista != null && vista.Visible;
        public static string TextoVisible => vista != null ? vista.TextoActual : "";

        // Caja de dialogo en pantalla: nombre del que habla (dorado) y su linea, con fondo oscuro para leerse sobre cualquier escena.
        sealed class DialogoView : MonoBehaviour
        {
            UnityEngine.UI.Text nombre, texto; CanvasGroup grupo; float hasta; bool visible;
            public bool Visible => visible;
            public string TextoActual => visible ? texto.text : "";

            public static DialogoView Crear()
            {
                var go = new GameObject("DialogoDeCinematica");
                Object.DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 950;
                var sc = go.AddComponent<UnityEngine.UI.CanvasScaler>();
                sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(960f, 540f); sc.matchWidthOrHeight = 0.5f;
                var v = go.AddComponent<DialogoView>();
                var fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var caja = new GameObject("Caja", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
                caja.transform.SetParent(go.transform, false);
                var rt = (RectTransform)caja.transform;
                rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(700f, 84f); rt.anchoredPosition = new Vector2(0f, 46f);
                var img = caja.GetComponent<UnityEngine.UI.Image>(); img.color = new Color(0f, 0f, 0f, 0.62f); img.raycastTarget = false;
                v.grupo = caja.GetComponent<CanvasGroup>(); v.grupo.alpha = 0f; v.grupo.blocksRaycasts = false; v.grupo.interactable = false;

                v.nombre = NuevoTexto(caja.transform, "Nombre", fuente, 14, new Color(1f, 0.82f, 0.3f), TextAnchor.UpperLeft, new Vector2(16f, 58f), new Vector2(-16f, -6f));
                v.texto = NuevoTexto(caja.transform, "Linea", fuente, 18, Color.white, TextAnchor.UpperLeft, new Vector2(16f, 6f), new Vector2(-16f, -26f));
                v.texto.horizontalOverflow = HorizontalWrapMode.Wrap; v.texto.resizeTextForBestFit = true; v.texto.resizeTextMinSize = 11; v.texto.resizeTextMaxSize = 18;
                return v;
            }

            static UnityEngine.UI.Text NuevoTexto(Transform padre, string n, Font f, int tam, Color c, TextAnchor al, Vector2 offMin, Vector2 offMax)
            {
                var go = new GameObject(n, typeof(RectTransform), typeof(UnityEngine.UI.Text));
                go.transform.SetParent(padre, false);
                var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = offMin; rt.offsetMax = offMax;
                var t = go.GetComponent<UnityEngine.UI.Text>(); t.font = f; t.fontSize = tam; t.color = c; t.alignment = al; t.raycastTarget = false;
                return t;
            }

            public void Mostrar(string quien, string linea, float segundos)
            {
                nombre.text = quien; texto.text = linea; visible = true;
                hasta = Time.unscaledTime + Mathf.Max(2.5f, segundos);
                grupo.alpha = 1f;
            }

            public void Ocultar() { visible = false; if (grupo != null) grupo.alpha = 0f; }

            void Update()
            {
                if (!visible) return;
                float resto = hasta - Time.unscaledTime;
                if (resto <= 0f) { Ocultar(); return; }
                grupo.alpha = Mathf.Clamp01(resto / 0.4f);   // se desvanece al final
            }
        }

        // Lineas cuyo clip real dura mas que su ventana (o no existe). Vacia = todo encaja.
        public static List<string> Validar(bool exigirClips = false)
        {
            var problemas = new List<string>();
            foreach (var l in Todas)
            {
                var c = Clip(l);
                if (c == null) { if (exigirClips) problemas.Add($"{l.Id}: falta el audio"); continue; }
                if (c.length > l.Ventana + 0.05f) problemas.Add($"{l.Id}: dura {c.length:0.0} s y la ventana es de {l.Ventana:0.0} s");
            }
            return problemas;
        }
    }
}
