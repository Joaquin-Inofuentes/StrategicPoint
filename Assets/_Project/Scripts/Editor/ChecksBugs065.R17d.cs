using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Operacion;
using SP.Presentation;
using SP.UI;

namespace SP.EditorTools
{
    // Ronda 17d: requisito de entrega "menu de opciones modificable en partida: resolucion, calidad grafica, iluminacion y sensibilidad del mouse".
    public static partial class ChecksBugs065
    {
        static Button BotonDe(Transform panel, string nombre) { var t = SP.Core.BuscarHijo.Ruta(panel, nombre); return t != null ? t.GetComponent<Button>() : null; }
        static string ValorDe(Transform panel, string nombre) { var t = SP.Core.BuscarHijo.Ruta(panel, nombre); var v = t != null ? SP.Core.BuscarHijo.Ruta(t, "Value") : null; var tx = v != null ? v.GetComponent<Text>() : null; return tx != null ? tx.text : "?"; }

        static IEnumerator Bug991a()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var pc = PausaDelJuego();
            if (pc == null) { ModoDios.Poner(false); Fin("FALLO no hay PauseController"); yield break; }
            int q0 = AjustesDeJuego.CalidadActual; float lod0 = QualitySettings.lodBias; var urpAntes = QualitySettings.renderPipeline;
            var ilum0 = AjustesDeJuego.Iluminacion; float brillo0 = AjustesDeJuego.Brillo;
            pc.ShowPause(); pc.OnSettingsClicked();
            foreach (var x in Esperar(0.8f)) yield return x;
            var sp = HijoDe(pc.transform, "SettingsPanel");
            if (sp == null || !sp.gameObject.activeInHierarchy) { CerrarPausa(pc); ModoDios.Poner(false); Fin("FALLO SettingsPanel no esta activo"); yield break; }

            // --- resolucion (desplegable con las resoluciones del monitor) y modo de pantalla
            var ddT = HijoDe(sp, "Resolucion"); var dd = ddT != null ? ddT.GetComponent<Dropdown>() : null;
            W8Ok(ref ok, dd != null && dd.options.Count > 0, sb, $"resolucion: desplegable con {(dd != null ? dd.options.Count : 0)} opciones, actual {AjustesDeJuego.TextoResolucion()}");
            W8Ok(ref ok, BotonDe(sp, "Pantalla") != null && BotonDe(sp, "Aplicar") != null, sb, "hay boton de modo de pantalla (completa/ventana) y APLICAR");

            // --- sensibilidad del mouse (slider conectado al jugador)
            var sensT = HijoDe(sp, "Sensibilidad de mouse_Slider"); var sens = sensT != null ? sensT.GetComponent<Slider>() : null;
            var drv = SP.Player.PlayerInputDriver.Activo;
            float s0 = drv != null ? drv.LookSensitivity : -1f;
            if (sens != null) sens.value = Mathf.Lerp(sens.minValue, sens.maxValue, 0.8f);
            W8Ok(ref ok, sens != null && drv != null && !Mathf.Approximately(drv.LookSensitivity, s0) && Mathf.Approximately(drv.LookSensitivity, sens.value), sb, $"sensibilidad del mouse: {s0:0.00} -> {(drv != null ? drv.LookSensitivity : -1f):0.00} en el jugador");
            if (sens != null) sens.value = s0;

            // --- calidad grafica
            var bq = BotonDe(sp, "Calidad");
            W8Ok(ref ok, bq != null && AjustesDeJuego.CantidadDeCalidades >= 2, sb, $"calidad: boton presente, {AjustesDeJuego.CantidadDeCalidades} niveles");
            if (bq != null)
            {
                bq.onClick.Invoke();   // ALTA -> BAJA
                int q1 = AjustesDeJuego.CalidadActual;
                var urp = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                W8Ok(ref ok, q1 != q0 && ValorDe(sp, "Calidad") == AjustesDeJuego.TextoCalidad(), sb, $"calidad {AjustesDeJuego.NombreDeCalidad(q0)} -> {AjustesDeJuego.NombreDeCalidad(q1)}; el boton muestra '{ValorDe(sp, "Calidad")}'");
                W8Ok(ref ok, urp != null && urp != urpAntes && AjustesDeJuego.EscalaDeRenderActual < 0.99f && QualitySettings.lodBias < lod0, sb, $"calidad BAJA se nota: escala de render {AjustesDeJuego.EscalaDeRenderActual:0.00}, MSAA {(urp != null ? urp.msaaSampleCount : -1)}, lodBias {lod0:0.00} -> {QualitySettings.lodBias:0.00}, asset copiado (el del proyecto no se toca)");
                W8Ok(ref ok, PlayerPrefs.GetInt("sp_calidad", -1) == q1, sb, "la calidad queda guardada (PlayerPrefs)");
            }

            // --- iluminacion (sombras del sol y de las luces) y brillo
            var bi = BotonDe(sp, "Iluminacion"); var bb = BotonDe(sp, "Brillo");
            W8Ok(ref ok, bi != null && bb != null, sb, "iluminacion y brillo: botones presentes");
            Light sol = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional && l.gameObject.scene.IsValid()) { sol = l; break; }
            W8Ok(ref ok, sol != null && SombrasDeLuces.SolesGestionados > 0, sb, $"hay luz direccional gestionada ({SombrasDeLuces.SolesGestionados})");
            if (sol != null && bi != null && bb != null)
            {
                AjustesDeJuego.PonerBrillo(1f); AjustesDeJuego.PonerIluminacion(AjustesDeJuego.NivelDeIluminacion.Media);
                foreach (var x in Esperar(0.8f)) yield return x;
                float i1 = sol.intensity; var sombra1 = sol.shadows;
                bi.onClick.Invoke(); foreach (var x in Esperar(0.7f)) yield return x;      // MEDIA -> ALTA
                var enAlta = AjustesDeJuego.Iluminacion; var sombraAlta = sol.shadows;
                bi.onClick.Invoke(); foreach (var x in Esperar(0.7f)) yield return x;      // ALTA -> BAJA
                bool bajaSinSombras = AjustesDeJuego.Iluminacion == AjustesDeJuego.NivelDeIluminacion.Baja && sol.shadows == LightShadows.None;
                int conSombraPuntual = 0;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Point && l.shadows != LightShadows.None) conSombraPuntual++;
                W8Ok(ref ok, enAlta == AjustesDeJuego.NivelDeIluminacion.Alta && (sombra1 == LightShadows.None || sombraAlta == LightShadows.Soft), sb, $"iluminacion ALTA: sombra del sol {sombra1} -> {sombraAlta}");
                W8Ok(ref ok, bajaSinSombras && conSombraPuntual == 0, sb, $"iluminacion BAJA: sol {sol.shadows}, luces puntuales con sombra={conSombraPuntual}; el boton dice '{ValorDe(sp, "Iluminacion")}'");
                W8Ok(ref ok, PlayerPrefs.GetInt("sp_iluminacion", -1) == 0, sb, "la iluminacion queda guardada");
                float ambiente1 = RenderSettings.ambientIntensity;
                bb.onClick.Invoke(); foreach (var x in Esperar(0.7f)) yield return x;       // brillo 100 % -> 120 %
                float i2 = sol.intensity;
                W8Ok(ref ok, Mathf.Approximately(AjustesDeJuego.Brillo, 1.2f) && Mathf.Abs(i2 - i1 * 1.2f) < 0.02f * Mathf.Max(1f, i1), sb, $"brillo 100 % -> {AjustesDeJuego.TextoBrillo()}: sol {i1:0.00} -> {i2:0.00}, ambiente {ambiente1:0.00} -> {RenderSettings.ambientIntensity:0.00}; el boton dice '{ValorDe(sp, "Brillo")}'");
            }

            // --- el panel entra en pantalla (todas las filas visibles, sin pisarse)
            var layout = sp.GetComponent<LayoutDeAjustes>();
            if (layout != null)
            {
                layout.Aplicar(); Canvas.ForceUpdateCanvases();
                var prob = LayoutDeAjustes.Diagnosticar(sp.gameObject, layout.AreaDelCanvas());
                W8Ok(ref ok, prob.Count == 0, sb, $"el panel con la seccion GRAFICOS entra en pantalla ({(prob.Count > 0 ? prob[0] : "sin defectos")})");
            }
            foreach (var x in CapturarPantalla("r17_opciones_graficos.png")) yield return x;

            // restaurar
            AjustesDeJuego.PonerIluminacion(ilum0); AjustesDeJuego.PonerBrillo(brillo0); AjustesDeJuego.PonerCalidad(q0);
            PlayerPrefs.DeleteKey("sp_calidad"); PlayerPrefs.DeleteKey("sp_iluminacion"); PlayerPrefs.DeleteKey("sp_brillo");
            CerrarPausa(pc); ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
