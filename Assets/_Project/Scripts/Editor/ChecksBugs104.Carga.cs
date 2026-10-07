using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Mision;
using SP.Presentation;

namespace SP.EditorTools
{
    // P11 (#130, #131): pantalla de carga con mapa y texto de la intro.
    //   Bug130  (SC_MainMenu) SceneLoader.Cargar("SC_Gameplay"/"SC_Operacion"): SC_Loading muestra el mapa del destino (textura != null), el trazo del recorrido
    //           tiene >= 3 puntos y crece con la barra; con otro destino (menu) no hay mapa.
    //   Bug131  (SC_Gameplay) ninguna diapositiva de la intro dice "luego volver al helicoptero"; la del objetivo habla del fondo; el subtitulo se ve en pantalla.
    public static partial class ChecksBugs065
    {
        static IEnumerable ProbarCarga(string destino, bool conMapa, string captura, StringBuilder sb, System.Action<bool> fin)
        {
            bool ok = true;
            SceneLoader.Cargar(destino);
            LoadingScreenController c = null;
            float t0 = Time.realtimeSinceStartup;
            while (c == null && Time.realtimeSinceStartup - t0 < 5f)
            {
                yield return null;
                c = Object.FindFirstObjectByType<LoadingScreenController>();
            }
            if (c == null) { sb.Append($"[{destino}: no aparecio SC_Loading] "); fin(false); yield break; }
            // La barra avanza sola (0,5 s de espera + 2,6 s de carga): se toman dos fotos del estado mientras la escena de carga sigue viva.
            int revelados1 = -1, revelados2 = -1, total = 0, hitos = 0; bool mapaVisible = false, infoCorrida = false; string escenaMapa = "", texto = "null";
            bool hizoCaptura = false, vivo = true;
            while (c != null && Time.realtimeSinceStartup - t0 < 30f)
            {
                total = c.PuntosDelRecorrido; hitos = c.Hitos; mapaVisible = c.MapaVisible; escenaMapa = c.EscenaDelMapa;
                texto = c.TexturaDelMapa != null ? c.TexturaDelMapa.width + "x" + c.TexturaDelMapa.height : "null";
                infoCorrida = c.info != null && c.info.anchoredPosition.x > 100f;
                if (revelados1 < 0 && c.PuntosRevelados >= 1) revelados1 = c.PuntosRevelados;
                if (revelados1 >= 0 && !hizoCaptura && c.PuntosRevelados >= Mathf.Max(2, Mathf.RoundToInt(total * 0.5f)))
                {
                    hizoCaptura = true; revelados2 = c.PuntosRevelados;
                    if (!string.IsNullOrEmpty(captura)) ScreenCapture.CaptureScreenshot(RutaValidacion(captura));
                }
                yield return null;
                if (c == null) break;
            }
            if (!conMapa) { vivo = true; }
            if (conMapa)
            {
                bool texOk = mapaVisible && texto != "null" && escenaMapa == destino;
                bool puntosOk = total >= 3;
                bool crece = revelados1 >= 1 && revelados2 >= revelados1 && revelados1 < total;
                bool hitosOk = hitos >= 3;
                ok = texOk && puntosOk && crece && hitosOk && infoCorrida;
                sb.Append($"[{destino}: mapa visible={mapaVisible} textura={texto}{(texOk ? "" : " (MAL)")}; trazo {total} puntos{(puntosOk ? "" : " (MAL)")}, hitos {hitos}{(hitosOk ? "" : " (MAL)")}; revelados {revelados1}->{revelados2} de {total}{(crece ? "" : " (MAL)")}; info corrida a la derecha={infoCorrida}{(infoCorrida ? "" : " (MAL)")}] ");
            }
            else
            {
                ok = !mapaVisible && total == 0 && !infoCorrida;
                sb.Append($"[{destino}: sin mapa (visible={mapaVisible}, puntos={total}, info corrida={infoCorrida}){(ok ? "" : " (MAL)")}] ");
            }
            // Espera a que termine de cargar el destino.
            t0 = Time.realtimeSinceStartup;
            while (Object.FindFirstObjectByType<LoadingScreenController>() != null && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            foreach (var x in Esperar(1.0f)) yield return x;
            fin(ok);
        }

        [EscenaDelCheck("SC_MainMenu")]
        static IEnumerator Bug130()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true, r = true;
            foreach (var x in ProbarCarga("SC_Gameplay", true, "v3_130_carga_gameplay.png", sb, v => r = v)) yield return x;
            ok &= r;
            foreach (var x in ProbarCarga("SC_Operacion", true, "v3_130_carga_operacion.png", sb, v => r = v)) yield return x;
            ok &= r;
            foreach (var x in ProbarCarga("SC_MainMenu", false, "v3_130_carga_menu.png", sb, v => r = v)) yield return x;
            ok &= r;
            Fin((ok ? "OK " : "FALLO ") + sb);
        }

        [EscenaDelCheck("SC_Gameplay")]
        static IEnumerator Bug131()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SC_Gameplay") { Fin("FALLO la escena no es SC_Gameplay"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            var ws = Object.FindObjectsByType<CinematicaWaypoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool viejo = false, fondo = false;
            foreach (var w in ws)
            {
                string s = (w.subtitulo ?? "").ToLowerInvariant();
                if (s.Contains("luego volver al helicoptero")) viejo = true;
                if (s.Contains("fondo")) fondo = true;
                sb.Append($"{w.name}=[{w.subtitulo}] ");
            }
            W8Ok(ref ok, ws.Length >= 4 && !viejo, sb, $"{ws.Length} diapositivas, ninguna con la frase vieja");
            W8Ok(ref ok, fondo, sb, "la del objetivo habla del fondo de la base");
            // En pantalla: el subtitulo aparece durante la intro.
            var intro = Object.FindAnyObjectByType<CinematicaDeIntro>();
            string visto = ""; bool capturado = false;
            float t0 = Time.realtimeSinceStartup;
            while (intro != null && Time.realtimeSinceStartup - t0 < 25f && !capturado)
            {
                foreach (var tx in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (tx.name == "Subtitulo" && tx.text.Length > 3 && tx.color.a > 0.9f) { visto = tx.text; capturado = true; break; }
                yield return null;
            }
            if (capturado) { ScreenCapture.CaptureScreenshot(RutaValidacion("v3_131_intro_subtitulo.png")); foreach (var x in Esperar(0.6f)) yield return x; }
            W8Ok(ref ok, capturado && visto.ToLowerInvariant().Contains("fondo") && !visto.ToLowerInvariant().Contains("luego volver"), sb, "subtitulo en pantalla: '" + visto + "'");
            if (intro != null && intro.EnCurso) intro.Saltar();
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
