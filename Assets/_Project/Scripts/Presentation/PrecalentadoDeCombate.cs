using UnityEngine;

namespace SP.Presentation
{
    // Bug #052/#053/#062 ("se me trabo todo", "se laguea muchisimo"): el profiler mostro que los tirones de 80-150 ms salian
    // de cosas que se creaban o cargaban la PRIMERA vez que hacian falta, en pleno combate: la malla de los sprites de
    // explosion (SpriteMeshGenerator), los pools de efectos y de trozos de derrumbe (AddComponent en el frame del estallido)
    // y los sonidos (Resources.LoadAll + datos de audio). Todo eso se hace aca una sola vez, durante la carga del nivel.
    public static class PrecalentadoDeCombate
    {
        public static bool Hecho { get; private set; }
        public static string Resumen { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Hecho = false; Resumen = ""; }

        public static void Ejecutar()
        {
            if (Hecho || !Application.isPlaying) return;
            Hecho = true;
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            int sprites = SpritesReales.Precargar();
            SpriteFx.Precalentar();
            Fragmentador.Precalentar();
            DebrisPool.Prewarm();
            ImpactFx.Prewarm();
            int sonidos = GenericSfx.Precargar();
            Resumen = $"sprites={sprites} sonidos={sonidos} ms={reloj.ElapsedMilliseconds}";
            SP.Core.GameLog.Line("Precalentado de combate: " + Resumen);
        }
    }
}
