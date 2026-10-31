using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SP.EditorTools
{
    // La RAM del editor crecia con cada Play (hasta ~14 GB). Causas medidas fuera de Play (editor parado, SC_Operacion abierta):
    //   1. Asset Import Workers en espera permanente (eran 2 de standby + hasta 4 activos) (~1,5 GB cada uno) por standbyImportWorkerCount=2 y 60 s de espera antes de apagarse.
    //   2. 1123 capturas PNG de validacion dentro de Assets (476 MB) importadas como textura: ahora viven en Assets/Validacion~ (carpeta ignorada).
    //   3. Texturas y materiales de runtime (HideAndDontSave) que cada sesion de Play deja huerfanos: Toon_*, Trimsheet_*, etc. (se duplicaban por Play).
    //   4. El pool nativo no devuelve lo reservado: sin un UnloadUnusedAssets + GC al salir de Play quedaba ~1,5 GB reservado sin usar.
    // Esta clase fija los ajustes de importacion y barre los restos de runtime al entrar y salir de Play. No toca nada del editor (Gizmo, UI Toolkit...).
    [InitializeOnLoad]
    public static class MemoriaDelEditor
    {
        public const int WorkersDeImportacion = 1, WorkersEnEspera = 0, EsperaParaApagarWorkersMs = 3000;

        // Texturas de runtime de las fabricas del juego (se regeneran solas al volver a Play).
        static readonly string[] PrefijosDeTexturaDeRuntime = { "Toon_", "Trimsheet_", "Soldier", "Hud", "Icon_", "Rol_", "Role", "Decal", "Procedural", "Tex_" };

        static MemoriaDelEditor()
        {
            EditorApplication.playModeStateChanged += AlCambiarDeModo;
            EditorApplication.delayCall += AjustarImportacion;
        }

        static void AjustarImportacion()
        {
            try
            {
                EditorUserSettings.desiredImportWorkerCount = WorkersDeImportacion;
                EditorUserSettings.standbyImportWorkerCount = WorkersEnEspera;
                EditorUserSettings.idleImportWorkerShutdownDelayMilliseconds = EsperaParaApagarWorkersMs;
            }
            catch (Exception e) { Debug.LogWarning("[MemoriaDelEditor] ajustes de importacion: " + e.Message); }
        }

        static void AlCambiarDeModo(PlayModeStateChange estado)
        {
            if (estado == PlayModeStateChange.ExitingEditMode) BarrerTexturasHuerfanas();
            else if (estado == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Liberar;
        }

        [MenuItem("Strategic Point/Memoria/Liberar RAM ahora")]
        static void LiberarMenu() => Debug.Log(LiberarYMedir());

        public static string LiberarYMedir()
        {
            long antes = Profiler_Reservada();
            int tex = BarrerTexturasHuerfanas();
            int mat = PlaymodeCleanup.BarrerMaterialesHuerfanos();
            Liberar();
            return $"texturas huerfanas={tex}, materiales huerfanos={mat}, reservado {antes / 1048576} -> {Profiler_Reservada() / 1048576} MB";
        }

        static long Profiler_Reservada() => UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();

        public static void Liberar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Texturas de runtime sin asset que ningun material usa: restos de la sesion anterior.
        public static int BarrerTexturasHuerfanas()
        {
            if (EditorApplication.isPlaying) return 0;
            var usadas = new HashSet<Texture>();
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null) continue;
                foreach (var n in m.GetTexturePropertyNames()) { var t = m.GetTexture(n); if (t != null) usadas.Add(t); }
            }
            foreach (var r in Resources.FindObjectsOfTypeAll<UnityEngine.UI.RawImage>()) if (r != null && r.texture != null) usadas.Add(r.texture);
            foreach (var s in Resources.FindObjectsOfTypeAll<SpriteRenderer>()) if (s != null && s.sprite != null && s.sprite.texture != null) usadas.Add(s.sprite.texture);
            foreach (var s in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>()) if (s != null && s.sprite != null && s.sprite.texture != null) usadas.Add(s.sprite.texture);
            int n2 = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (t == null || EditorUtility.IsPersistent(t) || t.hideFlags != HideFlags.HideAndDontSave || usadas.Contains(t)) continue;
                bool dejuego = false;
                foreach (var p in PrefijosDeTexturaDeRuntime) if (t.name.StartsWith(p, StringComparison.Ordinal)) { dejuego = true; break; }
                if (!dejuego) continue;
                UnityEngine.Object.DestroyImmediate(t);
                n2++;
            }
            return n2;
        }
    }
}
