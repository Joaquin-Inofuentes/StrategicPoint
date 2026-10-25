using System.Collections.Generic;
using UnityEngine;
using SP.Presentation;
using SP.UI;

namespace SP.Tutorial
{
    // Pedido: "en el minimapa se vean los objetivos bien marcados". Cada baliza activa (TutorialBeacon) se dibuja en el minimapa como
    // un rombo amarillo con borde blanco, pulsando y mucho mas grande que los puntos de las unidades. Si el objetivo queda fuera del
    // encuadre se pega al borde del minimapa (mas chico), asi siempre se sabe para donde ir.
    // Un solo objeto para todas las balizas: las balizas del director se recrean cada segundo y un icono propio de cada una parpadearia.
    public class MarcaDeObjetivosEnMinimapa : MonoBehaviour
    {
        // Tamano del rombo (semidiagonal) como fraccion del radio visible del minimapa: se ve igual de grande con cualquier zoom.
        public const float FraccionDelEncuadre = 0.15f, FraccionAlBorde = 0.11f;

        static MarcaDeObjetivosEnMinimapa instancia;
        public static MarcaDeObjetivosEnMinimapa Instancia => instancia;

        class Marca
        {
            public Transform raiz, borde, interior;
            public bool AlBorde;
        }

        // Objetivos que no son una baliza (p. ej. la plaza o los sectores de la fase Resistir): los aporta quien los conoce.
        public static System.Func<IEnumerable<Vector3>> Extras;

        readonly List<Marca> marcas = new List<Marca>();
        readonly List<Vector3> puntos = new List<Vector3>();
        int capa;
        float t;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        public static MarcaDeObjetivosEnMinimapa Asegurar()
        {
            if (!Application.isPlaying) return null;
            if (instancia != null) return instancia;
            var go = new GameObject("MarcaDeObjetivosEnMinimapa");
            instancia = go.AddComponent<MarcaDeObjetivosEnMinimapa>();
            return instancia;
        }

        // Cuantas marcas se dibujan ahora y cuantas quedaron pegadas al borde (para las pruebas).
        public int Visibles { get; private set; }
        public int EnElBorde { get; private set; }
        public Vector3 PosicionDe(int i) => i >= 0 && i < marcas.Count ? marcas[i].raiz.position : Vector3.zero;

        void Awake()
        {
            capa = LayerMask.NameToLayer("Minimap");
            if (capa < 0) capa = 8;
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        Marca Nueva()
        {
            var m = new Marca();
            var raiz = new GameObject("MarcaObjetivo");
            raiz.transform.SetParent(transform, false);
            raiz.layer = capa;
            // El rombo del mundo es vertical (plano XY): acostado para que la camara cenital lo vea de frente.
            raiz.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Contorno negro por debajo: el blanco solo se pierde contra las paredes grises y los puntos rojos del minimapa.
            var sombra = DiamondGizmo.CrearCara("Sombra", raiz.transform, 1.3f, DiamondGizmo.NuevoMaterialCompartido(new Color(0.02f, 0.02f, 0.03f, 1f)));
            sombra.layer = capa;
            sombra.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            var borde = DiamondGizmo.CrearCara("Borde", raiz.transform, 1f, DiamondGizmo.NuevoMaterialCompartido(Color.white));
            var interior = DiamondGizmo.CrearCara("Interior", raiz.transform, 0.66f, DiamondGizmo.NuevoMaterialCompartido(DiamondGizmo.ColorObjetivo));
            borde.layer = capa; interior.layer = capa;
            // -Z local queda hacia arriba una vez acostado: el interior encima del borde, sin pelear en Z.
            interior.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            m.raiz = raiz.transform; m.borde = borde.transform; m.interior = interior.transform;
            marcas.Add(m);
            return m;
        }

        void LateUpdate()
        {
            t += Time.unscaledDeltaTime;
            var mf = MinimapFollow.Activo;
            var cam = mf != null ? mf.MinimapCamera : null;
            puntos.Clear();
            var balizas = TutorialBeacon.Activas;
            for (int i = 0; i < balizas.Count; i++) if (balizas[i] != null) puntos.Add(balizas[i].Posicion);
            if (Extras != null) puntos.AddRange(Extras());
            int usadas = 0, borde = 0;
            if (cam != null)
            {
                float medioY = cam.orthographicSize, medioX = cam.orthographicSize * Mathf.Max(0.01f, cam.aspect);
                var ct = cam.transform;
                for (int i = 0; i < puntos.Count; i++)
                {
                    var p = puntos[i];
                    var d = p - ct.position;
                    float dx = Vector3.Dot(d, ct.right), dy = Vector3.Dot(d, ct.up);
                    float lado = cam.orthographicSize * FraccionDelEncuadre;
                    bool fuera = false;
                    float limX = medioX - lado * 1.2f, limY = medioY - lado * 1.2f;
                    if (Mathf.Abs(dx) > limX || Mathf.Abs(dy) > limY)
                    {
                        float k = Mathf.Min(limX / Mathf.Max(Mathf.Abs(dx), 0.001f), limY / Mathf.Max(Mathf.Abs(dy), 0.001f));
                        dx *= k; dy *= k; fuera = true;
                        lado = cam.orthographicSize * FraccionAlBorde;
                    }
                    while (marcas.Count <= usadas) Nueva();
                    var m = marcas[usadas++];
                    m.AlBorde = fuera; if (fuera) borde++;
                    m.raiz.gameObject.SetActive(true);
                    var pos = ct.position + ct.right * dx + ct.up * dy;
                    pos.y = ct.position.y - 12f;   // debajo de la camara, por encima de los puntos de las unidades
                    m.raiz.position = pos;
                    float pulso = 1f + (fuera ? 0.18f : 0.12f) * Mathf.Sin(t * 4f);
                    m.raiz.localScale = Vector3.one * lado * pulso;
                }
            }
            for (int i = usadas; i < marcas.Count; i++) if (marcas[i].raiz.gameObject.activeSelf) marcas[i].raiz.gameObject.SetActive(false);
            Visibles = usadas; EnElBorde = borde;
        }
    }
}
