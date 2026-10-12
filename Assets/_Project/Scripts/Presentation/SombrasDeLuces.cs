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
        public const int MaximoDePuntuales = 8;
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
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l != null && l.type == LightType.Point) puntuales.Add(l);
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
