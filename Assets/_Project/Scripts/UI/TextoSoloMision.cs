using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SP.UI
{
    // Pedido de pulido de UI: "eliminar textos: todos los textos exceptuando el de mision, y el de mision con las minimas palabras posibles".
    //
    // En los niveles (tutorial, nivel 1 y nivel 2) solo se dibujan los textos que su dueno marco como "de mision" (Permitir): el titulo y el
    // detalle del cartel de objetivo, el numero del temporizador y el titulo del paso del tutorial. Todo lo demas -- numeros del roster y de
    // la municion, N del minimapa, carteles de racha, avisos, kill feed, rotulos de las paredes, etiquetas del mundo, hints --
    // se oculta aca, en UN solo lugar, en vez de quitar cada fuente por separado (y que la proxima que alguien agregue vuelva a aparecer).
    //
    // No toca los menus: el barrido se frena con el juego en pausa (Time.timeScale ~ 0: pausa, resultado, pantalla final), en el menu
    // principal y en la carga, y respeta los objetos cuyo nombre es de una cinematica o de un menu.
    //
    // Se oculta con CanvasRenderer.cull (el Text sigue "activo" para su dueno, que puede leer .text y cambiarlo sin enterarse) y los
    // TextMesh del mundo apagando su MeshRenderer.
    public class TextoSoloMision : MonoBehaviour
    {
        // #259/#281: con el barrido activo faltaban el roster, la municion, el objetivo y los botones de resultado ("revisa los textos de
        // todos lados"): los textos vuelven a verse. El barrido queda disponible, apagado.
        public static bool Activo = false;
        public const float PeriodoDeBarrido = 0.1f;

        static readonly HashSet<Graphic> permitidos = new HashSet<Graphic>();
        public static void Permitir(Graphic g) { if (g != null) permitidos.Add(g); }
        public static bool EsDeMision(Graphic g) => g != null && permitidos.Contains(g);

        // Escenas en las que no se barre (menus y carga).
        static readonly string[] EscenasLibres = { "SC_MainMenu", "SC_Loading" };
        // Un objeto con alguno de estos fragmentos en su camino de nombres es un menu o una cinematica: se deja en paz.
        static readonly string[] FragmentosLibres = { "pausa", "pause", "menu", "resultado", "victoria", "derrota", "cinematica", "intro", "loading", "ajustes", "config", "tabla", "rebind", "trampas", "panelfinal" };

        static TextoSoloMision instancia;
        float proximo;
        readonly Dictionary<Transform, bool> libres = new Dictionary<Transform, bool>();
        static readonly List<Text> bufferTextos = new List<Text>(256);
        static readonly List<TextMesh> bufferMallas = new List<TextMesh>(128);

        public static int OcultosEnElUltimoBarrido { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { permitidos.Clear(); instancia = null; Activo = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("TextoSoloMision") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<TextoSoloMision>();
        }

        void Update()
        {
            if (!Activo || Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + PeriodoDeBarrido;
            if (Time.timeScale < 0.05f) return;   // pausa, resultado, pantalla final
            string escena = SceneManager.GetActiveScene().name;
            foreach (var libre in EscenasLibres) if (escena == libre) return;
            Barrer();
        }

        public void Barrer()
        {
            int ocultos = 0;
            permitidos.RemoveWhere(g => g == null);

            var textos = bufferTextos; textos.Clear();
            FindTextos(textos);
            for (int i = 0; i < textos.Count; i++)
            {
                var t = textos[i];
                if (t == null || permitidos.Contains(t)) continue;
                if (EsLibre(t.transform)) continue;
                var cr = t.canvasRenderer;
                if (cr != null && !cr.cull) cr.cull = true;
                ocultos++;
            }

            var mallas = bufferMallas; mallas.Clear();
            foreach (var m in FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude))
            {
                if (m == null) continue;
                if (EsLibre(m.transform)) continue;
                if (m.name.StartsWith("Letra_")) continue;   // las letras de los sectores (A, B) en el piso: la mision los nombra
                var r = m.GetComponent<MeshRenderer>();
                if (r != null && r.enabled) { r.enabled = false; ocultos++; }
            }
            OcultosEnElUltimoBarrido = ocultos;
        }

        static void FindTextos(List<Text> destino)
        {
            foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Exclude)) destino.Add(t);
        }

        // Camino de nombres hacia la raiz: si algun tramo es de un menu o una cinematica, se respeta. Cacheado por instancia.
        bool EsLibre(Transform t)
        {
            if (libres.TryGetValue(t, out bool r)) return r;
            r = false;
            for (var x = t; x != null && !r; x = x.parent)
            {
                string n = x.name.ToLowerInvariant();
                foreach (var f in FragmentosLibres) if (n.Contains(f)) { r = true; break; }
            }
            if (libres.Count > 4000) libres.Clear();
            libres[t] = r;
            return r;
        }
    }
}
