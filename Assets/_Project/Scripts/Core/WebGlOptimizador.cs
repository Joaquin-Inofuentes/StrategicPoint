#if UNITY_WEBGL
using UnityEngine;

namespace SP.Core
{
    // Ajustes de ejecucion solo para la build WebGL, sin tocar los assets de calidad de PC:
    // el navegador corre en un solo hilo y con WebGL2, asi que se usa el nivel de calidad "Mobile"
    // (Forward, sin sombras de luces puntuales, escala 0,8) y se evita todo lo que no rinde alli.
    public static class WebGlOptimizador
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Aplicar()
        {
            QualitySettings.SetQualityLevel(0, true);       // 0 = Mobile (ver ProjectSettings/QualitySettings)
            QualitySettings.vSyncCount = 1;                 // el navegador sincroniza con requestAnimationFrame
            Application.targetFrameRate = -1;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 30f);
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.softParticles = false;
            Application.runInBackground = false;
        }
    }
}
#endif
