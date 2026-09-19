using System.Collections.Generic;
using UnityEngine;
using SP.UI;

namespace SP.Presentation
{
    // Subtitulos de sonido (accesibilidad, item 28): con la opcion activa, los sonidos que importan en combate
    // (disparos, explosiones, granadas, canon, bala que pasa cerca, bajas) escriben un aviso con el LADO y la
    // DISTANCIA de donde vinieron, para quien no oye o juega sin sonido. Se apaga en Configuraciones.
    public static class Subtitulos
    {
        const string Pref = "sp_subtitulos";
        public const float DistanciaMaxima = 70f;
        public const float SegundosEntreAvisos = 1.2f;

        static bool cargado, activos;
        public static bool Activos
        {
            get { if (!cargado) { activos = PlayerPrefs.GetInt(Pref, 0) == 1; cargado = true; } return activos; }
        }
        public static void Poner(bool v) { activos = v; cargado = true; PlayerPrefs.SetInt(Pref, v ? 1 : 0); PlayerPrefs.Save(); }
        public static int Emitidos { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { cargado = false; ultimo.Clear(); Emitidos = 0; }

        static readonly Dictionary<string, float> ultimo = new Dictionary<string, float>();

        // Nombre del sonido -> palabra del subtitulo (null si no se subtitula).
        public static string Etiqueta(string nombreDeClip)
        {
            if (string.IsNullOrEmpty(nombreDeClip)) return null;
            var n = nombreDeClip.ToLowerInvariant();
            if (n.Contains("explo")) return "EXPLOSION";
            if (n.Contains("grenade")) return "GRANADA";
            if (n.Contains("whizz")) return "BALA CERCA";
            if (n.Contains("cannon") || n.Contains("tankfire")) return "CANON";
            if (n.Contains("shot")) return "DISPARO";
            if (n.Contains("death")) return "BAJA";
            if (n.Contains("knife")) return "CUCHILLO";
            return null;
        }

        public static string Lado(Vector3 oyente, float yawGrados, Vector3 fuente)
        {
            var d = fuente - oyente; d.y = 0f;
            var f = Quaternion.Euler(0f, yawGrados, 0f) * Vector3.forward;
            float ang = Vector3.SignedAngle(f, d, Vector3.up);
            return Mathf.Abs(ang) < 25f ? "ENFRENTE ^" : Mathf.Abs(ang) > 140f ? "ATRAS vv" : ang > 0f ? "DERECHA >>" : "<< IZQUIERDA";
        }

        // "[EXPLOSION] DERECHA >> 12 m", o null si no se subtitula o esta muy lejos.
        public static string Describir(string nombreDeClip, Vector3 oyente, float yawGrados, Vector3 fuente)
        {
            var e = Etiqueta(nombreDeClip);
            if (e == null) return null;
            float dist = Vector3.Distance(new Vector3(oyente.x, 0f, oyente.z), new Vector3(fuente.x, 0f, fuente.z));
            if (dist > DistanciaMaxima) return null;
            return $"[{e}] {Lado(oyente, yawGrados, fuente)} {dist:0} m";
        }

        // Lo llama el director de audio por cada sonido posicional. Barato cuando esta apagado.
        public static void Anunciar(AudioClip clip, Vector3 posicion)
        {
            if (!Activos || clip == null) return;
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) return;
            var e = Etiqueta(clip.name);
            if (e == null) return;
            if (ultimo.TryGetValue(e, out var t) && Time.unscaledTime - t < SegundosEntreAvisos) return;
            var texto = Describir(clip.name, cam.transform.position, cam.transform.eulerAngles.y, posicion);
            if (texto == null) return;
            ultimo[e] = Time.unscaledTime;
            Emitidos++;
            AlertQueue.Push(texto, AlertPriority.Baja, 1.6f);
        }
    }
}
