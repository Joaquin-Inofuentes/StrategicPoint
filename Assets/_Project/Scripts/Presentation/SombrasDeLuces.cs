using System.Collections.Generic;
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

        void Update()
        {
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.4f;
            if (Time.unscaledTime >= proximaBusqueda)
            {
                proximaBusqueda = Time.unscaledTime + 4f;
                puntuales.Clear();
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (l == null) continue;
                    if (l.type == LightType.Point) puntuales.Add(l);
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
                bool si = i < MaximoDePuntuales && (l.transform.position - p).sqrMagnitude < (l.range + 40f) * (l.range + 40f);
                var quiero = si ? LightShadows.Hard : LightShadows.None;
                if (l.shadows != quiero) l.shadows = quiero;
            }
        }
    }
}
