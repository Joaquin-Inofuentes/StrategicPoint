using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Bug #138 ("la luz no es real, no deberia pasar obstaculos"): las luces puntuales (farolas, luces de los edificios) proyectan sombra, asi la luz no
    // atraviesa paredes. Como el atlas de sombras de URP no alcanza para las ~60 luces del nivel (cada una usa 6 mapas), solo las 8 mas cercanas a la camara
    // la proyectan; las demas quedan sin sombra (estan lejos y no se nota). Los reflectores (spot) proyectan siempre: son 4 y cuestan 1 mapa cada uno.
    public class SombrasDeLuces : MonoBehaviour
    {
        // Bug #358/#357/#361/#365 (2026-11-06): con 8 puntuales eran 48 mapas de sombra (+4 reflectores) y no cabian en el atlas de 4096: URP avisaba
        // en CADA fotograma ("Reduced additional punctual light shadows resolution...") con stack trace; esa inundacion de la consola congelaba el
        // Editor ~2,4 s y las sombras costaban mucho de GPU. Con 2 puntuales son 12 mapas + 4 de reflectores = 16 (4x4 de 1024): entra justo.
        public const int MaximoDePuntuales = 2;
        // Bug #361 ("baja la intensidad de las luces, estan muy fuertes"): las luces de la escena se bajan una sola vez al descubrirlas.
        // Coherencia de la ciudad (2026-11-06): plaza 5,9 / farolas 4,6 / mansion 7,2 contra callejones 0,9: lo que pasa de TopeParejo se comprime al 30 %.
        public const float TopeParejo = 3f, TopeParejoReflector = 4f;
        public const float FactorDeIntensidadPuntual = 0.65f, FactorDeIntensidadReflector = 0.8f;
        readonly HashSet<Light> atenuadas = new HashSet<Light>();
        public static int LucesAtenuadas { get; private set; }
        readonly List<Light> puntuales = new List<Light>(80);
        float proximo, proximaBusqueda;
        static SombrasDeLuces instancia;   // lo crea solo Crear(): con la referencia alcanza, sin barrer la escena

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (instancia != null) return;
            var go = new GameObject("SombrasDeLuces") { hideFlags = HideFlags.HideInHierarchy };
            instancia = go.AddComponent<SombrasDeLuces>();
        }

        // Opciones de ILUMINACION y BRILLO (menu de Configuraciones, AjustesDeJuego.Graficos): se aplican a las luces DIRECCIONALES (el sol) y al
        // ambiente, partiendo siempre de los valores que traia la escena. Iluminacion BAJA apaga todas las sombras; ALTA suaviza la del sol.
        readonly Dictionary<Light, (float intensidad, LightShadows sombra)> sol = new Dictionary<Light, (float, LightShadows)>();
        float ambienteBase = -1f; int versionAplicada = -1;
        public static int SolesGestionados => instancia != null ? instancia.sol.Count : 0;

        void AplicarGraficos()
        {
            versionAplicada = SP.UI.AjustesDeJuego.VersionGrafica;
            float b = SP.UI.AjustesDeJuego.Brillo; var nivel = SP.UI.AjustesDeJuego.Iluminacion;
            foreach (var kv in sol)
            {
                var l = kv.Key; if (l == null) continue;
                l.intensity = kv.Value.intensidad * b;
                var s = kv.Value.sombra;
                l.shadows = nivel == SP.UI.AjustesDeJuego.NivelDeIluminacion.Baja ? LightShadows.None
                          : nivel == SP.UI.AjustesDeJuego.NivelDeIluminacion.Alta && s != LightShadows.None ? LightShadows.Soft : s;
            }
            if (ambienteBase < 0f) ambienteBase = RenderSettings.ambientIntensity;
            RenderSettings.ambientIntensity = ambienteBase * b;
        }

        // Cada escena trae su propio ambiente: se vuelve a tomar como base.
        void OnEnable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargar; }
        void OnDisable() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargar; }
        void AlCargar(UnityEngine.SceneManagement.Scene e, UnityEngine.SceneManagement.LoadSceneMode m) { ambienteBase = -1f; versionAplicada = -1; }

        void Update()
        {
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.4f;
            if (versionAplicada != SP.UI.AjustesDeJuego.VersionGrafica) AplicarGraficos();
            if (Time.unscaledTime >= proximaBusqueda)
            {
                proximaBusqueda = Time.unscaledTime + 4f;
                puntuales.Clear();
                // los soles de una escena descargada quedan como claves nulas: se podan para que el diccionario no crezca
                sol.Keys.Where(k => k == null).ToList().ForEach(k => sol.Remove(k));
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (l == null) continue;
                    if (l.type == LightType.Point) puntuales.Add(l);
                    if (l.type == LightType.Directional && !sol.ContainsKey(l) && l.gameObject.scene.IsValid()) { sol[l] = (l.intensity, l.shadows); versionAplicada = -1; }
                    if ((l.type == LightType.Point || l.type == LightType.Spot) && atenuadas.Add(l) && l.gameObject.scene.IsValid() && l.GetComponentInParent<SP.Actors.Soldier>() == null)
                    { l.intensity *= l.type == LightType.Point ? FactorDeIntensidadPuntual : FactorDeIntensidadReflector; { float tope = l.type == LightType.Point ? TopeParejo : TopeParejoReflector; if (l.intensity > tope) l.intensity = tope + (l.intensity - tope) * 0.3f; } LucesAtenuadas++; }
                }
            }
            var cam = CamaraPrincipal.Actual;
            if (cam == null) return;
            var p = cam.transform.position;
            puntuales.RemoveAll(l => l == null);
            puntuales.Sort((a, b) => (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude));
            for (int i = 0; i < puntuales.Count; i++)
            {
                var l = puntuales[i];
                int tope = SP.UI.AjustesDeJuego.Iluminacion == SP.UI.AjustesDeJuego.NivelDeIluminacion.Baja ? 0 : MaximoDePuntuales;
                bool si = i < tope && (l.transform.position - p).sqrMagnitude < (l.range + 40f) * (l.range + 40f);
                var quiero = si ? LightShadows.Hard : LightShadows.None;
                if (l.shadows != quiero) l.shadows = quiero;
            }
        }
    }
}
