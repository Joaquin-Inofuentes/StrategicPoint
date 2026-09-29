using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Opciones que no tenian donde vivir: pantalla completa, resolucion, calidad, paleta para daltonismo y
    // HUD minimo. Se guardan en PlayerPrefs y se aplican solas al arrancar cada escena.
    public static class AjustesDeJuego
    {
        const string PrefCompleta = "sp_pantalla_completa", PrefRes = "sp_resolucion", PrefCalidad = "sp_calidad",
                     PrefDalto = "sp_daltonismo", PrefHudMin = "sp_hud_minimo", PrefEscala = "sp_escala_interfaz";

        public static bool Daltonismo { get; private set; }
        public static bool HudMinimo { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Daltonismo = false; HudMinimo = false; Escala = 1f; }

        // Tamano de la interfaz (accesibilidad, item 28): 100 %, 125 % o 150 %. Se aplica achicando la resolucion de
        // referencia de los CanvasScaler del HUD, la pausa y el menu (todos de 960x540).
        public const float AlturaDeReferencia = 540f;
        public static readonly float[] Escalas = { 1f, 1.25f, 1.5f };
        public static float Escala { get; private set; } = 1f;
        public static string TextoEscala() => Mathf.RoundToInt(Escala * 100f) + " %";
        public static void SiguienteEscala()
        {
            int i = System.Array.FindIndex(Escalas, e => Mathf.Approximately(e, Escala));
            PonerEscala(Escalas[(i + 1) % Escalas.Length]);
        }
        public static void PonerEscala(float e)
        {
            Escala = Mathf.Clamp(e, 1f, 1.5f);
            PlayerPrefs.SetInt(PrefEscala, Mathf.RoundToInt(Escala * 100f)); PlayerPrefs.Save();
            AplicarEscala();
        }
        public static void AplicarEscala()
        {
            foreach (var cs in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cs.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                float h = cs.referenceResolution.y;
                bool nuestro = false;
                foreach (var e in Escalas) if (Mathf.Abs(h - AlturaDeReferencia / e) < 0.6f) nuestro = true;
                if (!nuestro) continue;   // solo los canvas de 960x540 (y sus versiones escaladas)
                float ancho = 960f / Escala, alto = AlturaDeReferencia / Escala;
                cs.referenceResolution = new Vector2(ancho, alto);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            Daltonismo = false;
            HudMinimo = false;
            // Tamano de interfaz, daltonismo, HUD minimo y subtitulos se quitaron de Configuraciones a pedido: cualquier valor
            // guardado por una version anterior se descarta para que no quede una opcion activa sin forma de apagarla.
            Escala = 1f;
            PlayerPrefs.DeleteKey(PrefEscala);
            SP.Presentation.Subtitulos.Poner(false);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
            if (Application.isEditor) return;   // en el Editor no se toca la ventana del juego ni la calidad
            if (PlayerPrefs.HasKey(PrefRes) || PlayerPrefs.HasKey(PrefCompleta)) AplicarPantalla();
        }

        static void AlCargarEscena(UnityEngine.SceneManagement.Scene e, UnityEngine.SceneManagement.LoadSceneMode m) { if (!Mathf.Approximately(Escala, 1f)) AplicarEscala(); }

        // ---- Pantalla
        public static bool PantallaCompleta => PlayerPrefs.HasKey(PrefCompleta) ? PlayerPrefs.GetInt(PrefCompleta) == 1 : Screen.fullScreen;

        public static List<Resolution> Resoluciones()
        {
            var lista = new List<Resolution>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width < 800) continue;
                bool repetida = false;
                foreach (var e in lista) if (e.width == r.width && e.height == r.height) { repetida = true; break; }
                if (!repetida) lista.Add(r);
            }
            if (lista.Count == 0) lista.Add(new Resolution { width = Screen.width, height = Screen.height });
            return lista;
        }

        public static int IndiceResolucion()
        {
            var l = Resoluciones();
            int guardado = PlayerPrefs.GetInt(PrefRes, -1);
            if (guardado >= 0 && guardado < l.Count) return guardado;
            for (int i = 0; i < l.Count; i++) if (l[i].width == Screen.width && l[i].height == Screen.height) return i;
            return l.Count - 1;
        }
        public static string TextoResolucion() { var l = Resoluciones(); var r = l[Mathf.Clamp(IndiceResolucion(), 0, l.Count - 1)]; return $"{r.width}x{r.height}"; }

        public static void AlternarPantallaCompleta() { PlayerPrefs.SetInt(PrefCompleta, PantallaCompleta ? 0 : 1); PlayerPrefs.Save(); AplicarPantalla(); }
        public static void SiguienteResolucion() { var l = Resoluciones(); PlayerPrefs.SetInt(PrefRes, (IndiceResolucion() + 1) % l.Count); PlayerPrefs.Save(); AplicarPantalla(); }

        public static void AplicarPantalla()
        {
            var l = Resoluciones();
            var r = l[Mathf.Clamp(IndiceResolucion(), 0, l.Count - 1)];
            if (Application.isEditor) return;
            Screen.SetResolution(r.width, r.height, PantallaCompleta ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        // ---- Accesibilidad
        public static void PonerDaltonismo(bool v) { Daltonismo = v; }
        public static void PonerHudMinimo(bool v) { HudMinimo = v; }

        // Verde -> azul y rojo -> naranja: la pareja rojo/verde es la que confunden las formas mas comunes de daltonismo.
        public static Color Adaptar(Color c)
        {
            if (!Daltonismo) return c;
            float a = c.a;
            if (c.g > c.r * 1.15f && c.g > c.b * 1.15f) return new Color(0.25f, 0.62f, 0.98f, a);
            if (c.r > c.g * 1.7f && c.r > c.b * 1.7f) return new Color(0.98f, 0.62f, 0.10f, a);
            return c;
        }
    }
}
