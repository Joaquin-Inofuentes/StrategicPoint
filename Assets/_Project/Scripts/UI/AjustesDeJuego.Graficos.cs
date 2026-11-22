using UnityEngine;

namespace SP.UI
{
    // Requisito de la entrega: el menu de opciones debe permitir cambiar EN PARTIDA resolucion, calidad grafica, iluminacion y sensibilidad del mouse.
    // Resolucion y sensibilidad ya existian; aca estan CALIDAD (nivel de calidad de Unity/URP), ILUMINACION (sombras) y BRILLO. Se guardan en
    // PlayerPrefs y se reaplican solos al arrancar cada escena. Los botones estan en PanelAjustesExtra (seccion GRAFICOS).
    public static partial class AjustesDeJuego
    {
        const string PrefIluminacion = "sp_iluminacion", PrefBrilloV = "sp_brillo";

        public enum NivelDeIluminacion { Baja = 0, Media = 1, Alta = 2 }
        public static NivelDeIluminacion Iluminacion { get; private set; } = NivelDeIluminacion.Media;
        public static readonly float[] Brillos = { 0.8f, 1f, 1.2f, 1.4f };
        public static float Brillo { get; private set; } = 1f;
        // Cambia cada vez que se toca iluminacion o brillo: SombrasDeLuces lo compara y reaplica sin buscar nada nuevo.
        public static int VersionGrafica { get; private set; }
        public static event System.Action GraficosCambiaron;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ReiniciarGraficos() { Iluminacion = NivelDeIluminacion.Media; Brillo = 1f; VersionGrafica = 0; GraficosCambiaron = null; CalidadActual = 2; originalesTomados = false; urpCopia = null; urpOriginal = null; EscalaDeRenderActual = 1f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IniciarGraficos()
        {
            Iluminacion = (NivelDeIluminacion)Mathf.Clamp(PlayerPrefs.GetInt(PrefIluminacion, 1), 0, 2);
            Brillo = Mathf.Clamp(PlayerPrefs.GetFloat(PrefBrilloV, 1f), Brillos[0], Brillos[Brillos.Length - 1]);
            CalidadActual = Mathf.Clamp(PlayerPrefs.GetInt(PrefCalidad, 2), 0, NivelesDeCalidad - 1);
            // En el Editor no se toca la calidad al arrancar (solo si se la cambia a mano); en una build se respeta lo que el jugador dejo guardado.
            if (!Application.isEditor && PlayerPrefs.HasKey(PrefCalidad)) AplicarCalidad();
        }

        // ---- Calidad grafica: 3 perfiles propios (el proyecto solo trae 1 nivel de calidad de Unity para PC, asi que cambiar de nivel no hacia nada).
        // BAJA / MEDIA / ALTA modifican lo que mas pesa en GPU: escala de render y MSAA (en una COPIA del asset de URP, para no tocar el del proyecto),
        // resolucion de texturas, distancia de LOD y filtrado anisotropico. ALTA = los valores originales del proyecto.
        public const int NivelesDeCalidad = 3;
        public static int CalidadActual { get; private set; } = 2;
        static UnityEngine.Rendering.RenderPipelineAsset urpOriginal, urpCopia;
        static bool originalesTomados; static float lodBiasOriginal; static AnisotropicFiltering anisoOriginal; static int mipOriginal;
        public static int CantidadDeCalidades => NivelesDeCalidad;
        public static string NombreDeCalidad(int nivel) => nivel <= 0 ? "BAJA" : nivel == 1 ? "MEDIA" : "ALTA";
        public static string TextoCalidad() => NombreDeCalidad(CalidadActual);
        public static float EscalaDeRenderActual { get; private set; } = 1f;

        public static void PonerCalidad(int nivel)
        {
            CalidadActual = Mathf.Clamp(nivel, 0, NivelesDeCalidad - 1);
            PlayerPrefs.SetInt(PrefCalidad, CalidadActual); PlayerPrefs.Save();
            AplicarCalidad();
            GraficosCambiaron?.Invoke();
        }
        public static void SiguienteCalidad() => PonerCalidad((CalidadActual + 1) % NivelesDeCalidad);

        static void AplicarCalidad()
        {
            if (!originalesTomados)
            {
                originalesTomados = true;
                lodBiasOriginal = QualitySettings.lodBias; anisoOriginal = QualitySettings.anisotropicFiltering; mipOriginal = QualitySettings.globalTextureMipmapLimit;
                urpOriginal = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                // al salir (tambien al terminar el Play del Editor) se devuelve el asset original: la copia no sobrevive
                Application.quitting += RestaurarCalidadOriginal;
            }
            int n = CalidadActual;
            QualitySettings.globalTextureMipmapLimit = n == 0 ? Mathf.Max(mipOriginal, 2) : n == 1 ? Mathf.Max(mipOriginal, 1) : mipOriginal;
            QualitySettings.lodBias = lodBiasOriginal * (n == 0 ? 0.7f : n == 1 ? 0.85f : 1f);
            QualitySettings.anisotropicFiltering = n == 0 ? AnisotropicFiltering.Disable : n == 1 ? AnisotropicFiltering.Enable : anisoOriginal;
            var baseUrp = urpOriginal as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (baseUrp == null) return;
            if (urpCopia == null) urpCopia = Object.Instantiate(baseUrp);
            var copia = (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)urpCopia;
            copia.renderScale = baseUrp.renderScale * (n == 0 ? 0.7f : n == 1 ? 0.85f : 1f);
            copia.msaaSampleCount = n == 0 ? 1 : n == 1 ? Mathf.Min(2, baseUrp.msaaSampleCount) : baseUrp.msaaSampleCount;
            EscalaDeRenderActual = copia.renderScale;
            QualitySettings.renderPipeline = copia;
        }

        static void RestaurarCalidadOriginal()
        {
            if (!originalesTomados) return;
            QualitySettings.renderPipeline = urpOriginal;
            QualitySettings.lodBias = lodBiasOriginal; QualitySettings.anisotropicFiltering = anisoOriginal; QualitySettings.globalTextureMipmapLimit = mipOriginal;
            originalesTomados = false;
        }

        // ---- Iluminacion (sombras): BAJA = sin sombras, MEDIA = las de siempre, ALTA = sombra suave del sol y mas luces con sombra.
        public static string TextoIluminacion() => Iluminacion == NivelDeIluminacion.Baja ? "BAJA" : Iluminacion == NivelDeIluminacion.Media ? "MEDIA" : "ALTA";
        public static void PonerIluminacion(NivelDeIluminacion n)
        {
            Iluminacion = n; VersionGrafica++;
            PlayerPrefs.SetInt(PrefIluminacion, (int)n); PlayerPrefs.Save();
            GraficosCambiaron?.Invoke();
        }
        public static void SiguienteIluminacion() => PonerIluminacion((NivelDeIluminacion)(((int)Iluminacion + 1) % 3));

        // ---- Brillo: multiplica la luz direccional y la ambiental (80 %, 100 %, 120 %, 140 %).
        public static string TextoBrillo() => Mathf.RoundToInt(Brillo * 100f) + " %";
        public static void PonerBrillo(float b)
        {
            Brillo = Mathf.Clamp(b, Brillos[0], Brillos[Brillos.Length - 1]); VersionGrafica++;
            PlayerPrefs.SetFloat(PrefBrilloV, Brillo); PlayerPrefs.Save();
            GraficosCambiaron?.Invoke();
        }
        public static void SiguienteBrillo()
        {
            int i = System.Array.FindIndex(Brillos, b => Mathf.Approximately(b, Brillo));
            PonerBrillo(Brillos[(i + 1) % Brillos.Length]);
        }
    }
}
