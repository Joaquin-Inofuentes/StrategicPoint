using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SP.Presentation
{
    // Ambiente del menu principal (item 21): musica de fondo y un fondo tactico animado (mapa con cuadricula que se
    // desplaza y contactos azules/rojos que se mueven), en vez de tres botones sobre un color liso. Se arma solo al
    // cargar SC_MainMenu; es procedural (sin arte propio), asi que sigue siendo un fondo provisorio de programacion.
    public class MenuAmbiente : MonoBehaviour
    {
        public const string EscenaDelMenu = "SC_MainMenu";
        public const float LadoDeCelda = 64f;
        public const int Contactos = 7;
        public static bool Existe { get; private set; }

        RectTransform rejilla;
        RectTransform[] contactos;
        Image[] imagenes;
        float[] fase;
        AudioSource musica;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
            Crear(SceneManager.GetActiveScene());
        }
        static void AlCargar(Scene e, LoadSceneMode m) => Crear(e);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Existe = false; }

        public static MenuAmbiente Crear(Scene escena)
        {
            if (!Application.isPlaying || escena.name != EscenaDelMenu) return null;
            var existente = Object.FindAnyObjectByType<MenuAmbiente>();
            if (existente != null) return existente;
            Canvas canvas = null;
            foreach (var raiz in escena.GetRootGameObjects()) { canvas = raiz.GetComponent<Canvas>(); if (canvas != null) break; }
            if (canvas == null) return null;
            var go = new GameObject("MenuAmbiente", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling();   // detras del titulo, el panel y los botones
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            var a = go.AddComponent<MenuAmbiente>();
            a.Construir();
            return a;
        }

        static Image Barra(Transform padre, string nombre, Vector2 tam, Vector2 pos, Color c)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            var rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = tam; rt.anchoredPosition = pos;
            var im = g.GetComponent<Image>(); im.color = c; im.raycastTarget = false;
            return im;
        }

        void Construir()
        {
            Existe = true;
            var g = new GameObject("Rejilla", typeof(RectTransform));
            g.transform.SetParent(transform, false);
            rejilla = (RectTransform)g.transform;
            rejilla.anchorMin = rejilla.anchorMax = new Vector2(0.5f, 0.5f);
            rejilla.sizeDelta = Vector2.zero;
            var linea = new Color(0.35f, 0.5f, 0.8f, 0.13f);
            for (int x = -12; x <= 12; x++) Barra(rejilla, "V", new Vector2(2f, 1400f), new Vector2(x * LadoDeCelda, 0f), linea);
            for (int y = -9; y <= 9; y++) Barra(rejilla, "H", new Vector2(2000f, 2f), new Vector2(0f, y * LadoDeCelda), linea);

            contactos = new RectTransform[Contactos];
            imagenes = new Image[Contactos];
            fase = new float[Contactos];
            for (int i = 0; i < Contactos; i++)
            {
                bool aliado = i < 3;
                var col = aliado ? new Color(0.3f, 0.65f, 1f, 0.7f) : new Color(1f, 0.35f, 0.3f, 0.7f);
                var im = Barra(transform, aliado ? "Aliado" : "Enemigo", new Vector2(12f, 12f), Vector2.zero, col);
                im.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                contactos[i] = (RectTransform)im.transform; imagenes[i] = im;
                fase[i] = i * 0.9f + 0.4f;
            }

            var clip = SP.Core.RecursosCache.Cargar<AudioClip>("Audio/Music/Calm");
            if (clip != null)
            {
                var m = new GameObject("MusicaDelMenu");
                m.transform.SetParent(transform, false);
                musica = m.AddComponent<AudioSource>();
                musica.clip = clip; musica.loop = true; musica.spatialBlend = 0f; musica.playOnAwake = false;
                musica.volume = 0.35f * AudioDirector.GainFor(SfxChannel.Ambient);
                musica.Play();
            }
            Animar(0f);
        }

        void Animar(float t)
        {
            // La rejilla se desliza en diagonal y vuelve a empezar cada celda: parece un mapa que avanza sin fin.
            float k = t * 9f;
            rejilla.anchoredPosition = new Vector2(k % LadoDeCelda, -(k * 0.6f) % LadoDeCelda);
            for (int i = 0; i < contactos.Length; i++)
            {
                float f = fase[i];
                float rx = 180f + i * 55f, ry = 90f + (i % 3) * 45f;
                float ang = t * (0.12f + i * 0.02f) * (i % 2 == 0 ? 1f : -1f) + f;
                contactos[i].anchoredPosition = new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
                var c = imagenes[i].color; c.a = 0.35f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 2f + f * 3f));
                imagenes[i].color = c;
            }
        }

        void Update() => Animar(Time.unscaledTime);
    }
}
