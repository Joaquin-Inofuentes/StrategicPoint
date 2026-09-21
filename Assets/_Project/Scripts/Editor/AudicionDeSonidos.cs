using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SP.Combat;
using SP.Presentation;

namespace SP.EditorTools
{
    // Item 65 (Ronda 12): el analisis de audio mide, pero el timbre y "si queda bien" solo lo juzga un oido. Esta herramienta
    // baja el costo de esa escucha a unos minutos: renderiza TODOS los sonidos del juego (SfxKind y los de cada arma) en un
    // unico WAV, en orden, con 0,6 s de silencio entre uno y otro, mas un indice con el segundo en que empieza cada uno.
    // Salida: Docs/AUDICION_SONIDOS.wav y Docs/AUDICION_SONIDOS.txt. Menu: Strategic Point > Reportes > Audicion de sonidos.
    public static class AudicionDeSonidos
    {
        public const int FrecuenciaSalida = 44100;
        public const float SilencioEntreSonidos = 0.6f;

        [MenuItem("Strategic Point/Reportes/Audicion de sonidos")]
        public static void Generar()
        {
            var clips = new List<KeyValuePair<string, AudioClip>>();
            foreach (SfxKind k in Enum.GetValues(typeof(SfxKind))) clips.Add(new KeyValuePair<string, AudioClip>("Sfx_" + k, GenericSfx.Get(k)));
            foreach (WeaponKind w in Enum.GetValues(typeof(WeaponKind)))
            {
                clips.Add(new KeyValuePair<string, AudioClip>("Disparo_" + w, GenericSfx.GetWeaponShot(w)));
                clips.Add(new KeyValuePair<string, AudioClip>("Recarga_" + w, GenericSfx.GetWeaponReload(w)));
                clips.Add(new KeyValuePair<string, AudioClip>("Desenfundar_" + w, GenericSfx.GetWeaponDraw(w)));
                clips.Add(new KeyValuePair<string, AudioClip>("Seco_" + w, GenericSfx.GetWeaponDry(w)));
            }

            var muestras = new List<short>();
            var indice = new StringBuilder("Audicion de los sonidos del juego. Cada uno va seguido de 0,6 s de silencio.\r\nsegundo | sonido | duracion\r\n");
            int n = 0;
            foreach (var par in clips)
            {
                var clip = par.Value;
                if (clip == null) { indice.AppendLine($"  --   | {par.Key} | SIN CLIP"); continue; }
                float t0 = muestras.Count / (float)FrecuenciaSalida;
                var datos = new float[clip.samples * clip.channels];
                if (!clip.GetData(datos, 0)) { indice.AppendLine($"  --   | {par.Key} | SIN DATOS"); continue; }
                int canales = Mathf.Max(1, clip.channels);
                int largo = Mathf.RoundToInt(clip.length * FrecuenciaSalida);
                for (int i = 0; i < largo; i++)
                {
                    int src = Mathf.Min(clip.samples - 1, (int)((long)i * clip.samples / Mathf.Max(1, largo)));
                    float m = datos[src * canales];
                    muestras.Add((short)Mathf.RoundToInt(Mathf.Clamp(m, -1f, 1f) * 32000f));
                }
                int silencio = Mathf.RoundToInt(SilencioEntreSonidos * FrecuenciaSalida);
                for (int i = 0; i < silencio; i++) muestras.Add(0);
                indice.AppendLine($"{t0,6:0.0} | {par.Key} | {clip.length:0.00} s");
                n++;
            }

            EscribirWav("Docs/AUDICION_SONIDOS.wav", muestras);
            File.WriteAllText("Docs/AUDICION_SONIDOS.txt", indice.ToString(), new UTF8Encoding(false));
            Debug.Log($"[AUDIO] Audicion de {n} sonidos, {muestras.Count / (float)FrecuenciaSalida:0.0} s. Docs/AUDICION_SONIDOS.wav + .txt");
        }

        static void EscribirWav(string ruta, List<short> m)
        {
            using (var fs = new FileStream(ruta, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = m.Count * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE")); w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(FrecuenciaSalida); w.Write(FrecuenciaSalida * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                for (int i = 0; i < m.Count; i++) w.Write(m[i]);
            }
        }
    }
}
