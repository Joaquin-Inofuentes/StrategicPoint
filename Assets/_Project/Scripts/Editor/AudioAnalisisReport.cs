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
    // Item 65 (Ronda 11): "los sonidos se verificaron por metricas y no de oido". Un agente no oye, pero puede medir mas que
    // "no esta mudo": este reporte analiza cada sonido del juego (SfxKind y los de cada arma) y marca los que suenan mal por
    // razones objetivas: recorte (clipping), corriente continua (DC), clic al arrancar o al cortar, volumen muy bajo o muy
    // alto, duracion absurda. Escribe Docs/AUDIO_ANALISIS.csv. Menu: Strategic Point > Reportes > Analisis de audio.
    // No reemplaza escuchar los sonidos: solo saca de la lista los que fallan por una causa medible.
    public static class AudioAnalisisReport
    {
        public struct Fila
        {
            public string Nombre; public float Duracion; public float PicoDb, RmsDb, Recorte, Dc, ClicInicio, ClicFinal, CentroideHz;
            public string Avisos;
        }

        public const float LimiteRecorte = 0.005f;      // mas de 0,5 % de muestras al tope
        public const float LimiteDc = 0.05f;
        public const float LimiteClic = 0.2f;           // amplitud de la primera/ultima muestra
        public const float RmsMinimoDb = -45f, RmsMaximoDb = -3f;

        [MenuItem("Strategic Point/Reportes/Analisis de audio")]
        public static void Generar()
        {
            var filas = Analizar();
            var sb = new StringBuilder("sonido,duracion_s,pico_dBFS,rms_dBFS,recorte_pct,dc,clic_inicio,clic_final,centroide_Hz,avisos\n");
            int marcados = 0;
            foreach (var f in filas)
            {
                if (f.Avisos.Length > 0) marcados++;
                sb.AppendLine(string.Join(",", f.Nombre, F(f.Duracion), F(f.PicoDb), F(f.RmsDb), F(f.Recorte * 100f), F(f.Dc), F(f.ClicInicio), F(f.ClicFinal), F(f.CentroideHz), f.Avisos.Trim()));
            }
            File.WriteAllText("Docs/AUDIO_ANALISIS.csv", sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[AUDIO] {filas.Count} sonidos analizados, {marcados} con avisos. Docs/AUDIO_ANALISIS.csv");
        }

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        public static List<Fila> Analizar()
        {
            var lista = new List<Fila>();
            foreach (SfxKind k in Enum.GetValues(typeof(SfxKind))) Medir(lista, "Sfx_" + k, GenericSfx.Get(k));
            foreach (WeaponKind w in Enum.GetValues(typeof(WeaponKind)))
            {
                Medir(lista, "Disparo_" + w, GenericSfx.GetWeaponShot(w));
                Medir(lista, "Recarga_" + w, GenericSfx.GetWeaponReload(w));
                Medir(lista, "Desenfundar_" + w, GenericSfx.GetWeaponDraw(w));
                Medir(lista, "Seco_" + w, GenericSfx.GetWeaponDry(w));
            }
            return lista;
        }

        static void Medir(List<Fila> lista, string nombre, AudioClip clip)
        {
            var f = new Fila { Nombre = nombre, Avisos = "" };
            if (clip == null) { f.Avisos = "SIN_CLIP"; lista.Add(f); return; }
            f.Duracion = clip.length;
            var datos = new float[clip.samples * clip.channels];
            if (!clip.GetData(datos, 0)) { f.Avisos = "SIN_DATOS"; lista.Add(f); return; }
            int canales = Mathf.Max(1, clip.channels), n = clip.samples;
            double suma = 0, suma2 = 0; float pico = 0f; int tope = 0, cruces = 0; float previa = 0f;
            for (int i = 0; i < n; i++)
            {
                float m = datos[i * canales];   // primer canal: los sonidos sinteticos son mono
                if (float.IsNaN(m) || float.IsInfinity(m)) { f.Avisos = "NAN"; lista.Add(f); return; }
                float a = Mathf.Abs(m);
                if (a > pico) pico = a;
                if (a >= 0.999f) tope++;
                suma += m; suma2 += m * m;
                if (i > 0 && (m >= 0f) != (previa >= 0f)) cruces++;
                previa = m;
            }
            f.Dc = (float)(suma / n);
            f.PicoDb = 20f * Mathf.Log10(Mathf.Max(pico, 1e-6f));
            f.RmsDb = 10f * Mathf.Log10(Mathf.Max((float)(suma2 / n), 1e-12f));
            f.Recorte = tope / (float)n;
            f.ClicInicio = Mathf.Abs(datos[0]);
            f.ClicFinal = Mathf.Abs(datos[(n - 1) * canales]);
            // Cruces por cero por segundo / 2 ~ frecuencia dominante: sirve para comparar, no como espectro exacto.
            f.CentroideHz = cruces / Mathf.Max(f.Duracion, 0.001f) * 0.5f;

            if (f.Recorte > LimiteRecorte) f.Avisos += "RECORTE ";
            if (Mathf.Abs(f.Dc) > LimiteDc) f.Avisos += "DC ";
            if (f.ClicInicio > LimiteClic) f.Avisos += "CLIC_INICIO ";
            if (f.ClicFinal > LimiteClic) f.Avisos += "CLIC_FINAL ";
            if (f.RmsDb < RmsMinimoDb) f.Avisos += "MUY_BAJO ";
            if (f.RmsDb > RmsMaximoDb) f.Avisos += "MUY_ALTO ";
            if (f.Duracion > 8f) f.Avisos += "MUY_LARGO ";
            lista.Add(f);
        }
    }
}
