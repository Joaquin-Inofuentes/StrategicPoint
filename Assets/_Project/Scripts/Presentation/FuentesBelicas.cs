using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SP.Presentation
{
    // Ronda 12: dos fuentes belicas (OFL, Resources/UI/Fuentes) para TODOS los textos: menu inicial, tutorial y partida.
    //  - Black Ops One para titulos y cifras grandes (tamano >= UmbralTitulo).
    //  - Stardos Stencil Bold (plantilla militar) para el resto del HUD y los textos del mundo.
    // Los ~55 sitios que crean un Text usan la fuente por defecto de Unity; en vez de tocarlos uno por uno, este gestor cambia
    // la fuente de todo Text/TextMesh (activo o no) al cargar una escena y cada 0,25 s (los que se crean en partida).
    public class FuentesBelicas : MonoBehaviour
    {
        public const int UmbralTitulo = 26;
        public const string RutaTitulo = "UI/Fuentes/BlackOpsOne-Regular", RutaTexto = "UI/Fuentes/StardosStencil-Bold";
        public static int Cambiados { get; private set; }

        static FuentesBelicas instancia;
        float proximo;

        public static Font Titulo => SP.Core.RecursosCache.Cargar<Font>(RutaTitulo);
        public static Font Texto => SP.Core.RecursosCache.Cargar<Font>(RutaTexto);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; Cambiados = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            if (instancia != null) return;
            var go = new GameObject("FuentesBelicas");
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<FuentesBelicas>();
            SceneManager.sceneLoaded += (e, m) => Aplicar();
            Aplicar();
        }

        void Update()
        {
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.25f;
            Aplicar();
        }

        public static bool EsBelica(Font f) => f != null && (f.name == "BlackOpsOne-Regular" || f.name == "StardosStencil-Bold");

        // Aplica las fuentes a todo lo que este en las escenas cargadas. Devuelve cuantos textos cambio.
        public static int Aplicar()
        {
            var titulo = Titulo; var texto = Texto;
            if (titulo == null || texto == null) return 0;
            int n = 0;
            var textos = Object.FindObjectsByType<Text>(FindObjectsInactive.Include);
            for (int i = 0; i < textos.Length; i++) if (Aplicar(textos[i], titulo, texto)) n++;
            var mallas = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include);
            for (int i = 0; i < mallas.Length; i++) if (Aplicar(mallas[i], texto)) n++;
            Cambiados += n;
            return n;
        }

        public static bool Aplicar(Text t, Font titulo, Font texto)
        {
            if (t == null || EsBelica(t.font)) return false;
            bool grande = t.fontSize >= UmbralTitulo;
            t.font = grande ? titulo : texto;
            // Stardos ya es Bold: pedirle negrita de nuevo la sintetiza y la empasta.
            if (t.fontStyle == FontStyle.Bold) t.fontStyle = FontStyle.Normal;
            else if (t.fontStyle == FontStyle.BoldAndItalic) t.fontStyle = FontStyle.Italic;
            if (grande && t.fontStyle != FontStyle.Normal) t.fontStyle = FontStyle.Normal;
            return true;
        }

        public static bool Aplicar(TextMesh m, Font texto)
        {
            if (m == null || EsBelica(m.font)) return false;
            m.font = texto;
            if (m.fontStyle == FontStyle.Bold) m.fontStyle = FontStyle.Normal;
            var mr = m.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = texto.material;
            return true;
        }
    }
}
