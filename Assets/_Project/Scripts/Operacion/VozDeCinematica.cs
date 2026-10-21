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

        public const string Carpeta = "Audio/Cine/";
        public const float Volumen = 1f;

        public static readonly List<string> Reproducidas = new List<string>();
        public static readonly List<string> Faltantes = new List<string>();
        public static string Actual { get; private set; } = "";

        static AudioSource fuente;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { fuente = null; Reproducidas.Clear(); Faltantes.Clear(); Actual = ""; }

        public static AudioClip Clip(Linea l) => SP.Core.RecursosCache.Cargar<AudioClip>(Carpeta + l.Id);

        static AudioSource Fuente()
        {
            if (fuente != null) return fuente;
            var go = new GameObject("VozDeCinematica");
            Object.DontDestroyOnLoad(go);
            fuente = go.AddComponent<AudioSource>();
            fuente.spatialBlend = 0f; fuente.playOnAwake = false; fuente.priority = 0;
            return fuente;
        }

        // La linea anterior se corta si la nueva arranca (nunca se encimen dos voces).
        public static bool Decir(Linea l)
        {
            var clip = Clip(l);
            if (clip == null) { if (!Faltantes.Contains(l.Id)) Faltantes.Add(l.Id); return false; }
            var f = Fuente();
            f.Stop();
            f.clip = clip;
            f.volume = Mathf.Clamp01(Volumen * AudioDirector.GainFor(SfxChannel.Voice));
            f.Play();
            Actual = l.Id;
            Reproducidas.Add(l.Id);
            return true;
        }

        public static void Callar() { if (fuente != null) fuente.Stop(); Actual = ""; }
        public static bool Hablando => fuente != null && fuente.isPlaying;

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
