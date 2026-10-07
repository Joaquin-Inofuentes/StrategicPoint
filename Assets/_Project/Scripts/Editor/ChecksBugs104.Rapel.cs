using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Tanda #104-#132, P9: cinematica de apertura con rapel desde el helicoptero (#123).
    // El arnes de checks entra a un Play fresco y deja asentar 4 s (el rapel dura 5 s y el arnes lo salta antes de cada check), asi que el check la
    // vuelve a lanzar con ReiniciarPorPrueba (mismo camino que el Start: soldados en su lugar de inicio, sin OperacionPrueba.Arrancar). El Play fresco
    // real (la cinematica arrancando sola al entrar) se mide por CLI aparte (ver el informe 07).
    public static partial class ChecksBugs065
    {
        const string P9Prefijo = "v3_";

        static IEnumerator Bug123()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var d = OperacionDirector.Instancia; var drv = PlayerInputDriver.Activo;
            if (d == null || drv == null) { Fin("FALLO no hay director o driver"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            bool opVistaAntes = CinematicaDeOperacion.YaVista;
            try
            {
                // Posiciones de inicio de la escuadra (antes de la cinematica) y zona de inicio.
                var inicio = new Dictionary<Soldier, Vector3>();
                foreach (var s in OperacionDirector.EscuadraDeLaOperacion()) inicio[s] = s.transform.position;
                var zona = d.entradas[0].position;
                P6Ok(ref ok, inicio.Count == 3, sb, $"{inicio.Count} soldados de la escuadra en su lugar de inicio");

                // ---- A) corre 5 +- 1 s, con las tres tomas, cuerdas, tres soldados colgando y sacudida ----
                var cin = CinematicaDeRapel.ReiniciarPorPrueba(false);
                yield return null; yield return null;
                P6Ok(ref ok, CinematicaDeRapel.Activa && cin != null, sb, "arranca la cinematica de rapel");
                P6Ok(ref ok, !drv.enabled && SP.Ai.AiBrain.IAPausada && (drv.Rig == null || !drv.Rig.enabled), sb, $"bloqueo total: driver apagado={!drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}");
                var heli = d.heli;
                P6Ok(ref ok, heli != null && heli.gameObject.activeInHierarchy, sb, "el helicoptero esta en escena durante la cinematica");
                float t0 = Time.realtimeSinceStartup;
                int maxCuerdas = 0, maxColgando = 0, maxSuelo = 0; var tomas = new HashSet<int>(); float velMax = 0f; float tUltimo = 0f; float temblor = 0f; Vector3 camPrev = Vector3.zero; bool primera = true;
                Vector3 heliPrev = heli.position; float tPrev = 0f; float alturaEnHover = 0f;
                var momentos = new float[] { 0.7f, 1.3f, 2.0f, 2.7f, 3.2f, 3.8f, 4.5f };
                int siguienteFoto = 0;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - t0 < 14f)
                {
                    yield return null;
                    if (cin == null) break;
                    float t = cin.Tiempo; tUltimo = t;
                    tomas.Add(cin.Toma);
                    maxCuerdas = Mathf.Max(maxCuerdas, cin.CuerdasVisibles); maxColgando = Mathf.Max(maxColgando, cin.CopiasColgando); maxSuelo = Mathf.Max(maxSuelo, cin.SoldadosEnElSuelo);
                    float dtt = t - tPrev; if (dtt > 0.001f) { velMax = Mathf.Max(velMax, Vector3.Distance(heli.position, heliPrev) / dtt); }
                    heliPrev = heli.position; tPrev = t;
                    if (t > 2.0f && t < 3.4f) alturaEnHover = Mathf.Max(alturaEnHover, heli.position.y - (zona.y - 0.8f));
                    var cp = cin.PosicionDeCamara;
                    if (!primera && t > 1.6f && t < 3.1f) temblor += Vector3.Distance(cp, camPrev);   // movimiento de camara dentro de la toma 2 (orbita + sacudida)
                    camPrev = cp; primera = false;
                    if (siguienteFoto < momentos.Length && t >= momentos[siguienteFoto])
                    {
                        string nombre = P9Prefijo + "123_rapel_" + (siguienteFoto + 1) + "_t" + momentos[siguienteFoto].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_') + ".png";
                        siguienteFoto++;
                        foreach (var x in CapturarPantalla(nombre)) yield return x;
                    }
                }
                float durReal = Time.realtimeSinceStartup - t0;
                P6Ok(ref ok, tUltimo >= 4.6f && tUltimo <= 5.2f && durReal >= 3.5f && durReal <= 9f, sb, $"duracion de la cinematica {tUltimo:0.00} s de juego ({durReal:0.0} s reales con las capturas; se piden 5 +- 1)");
                P6Ok(ref ok, tomas.Contains(1) && tomas.Contains(2) && tomas.Contains(3), sb, $"tres tomas ({string.Join(",", tomas)})");
                P6Ok(ref ok, maxCuerdas >= 3 && maxColgando >= 3, sb, $"hasta {maxCuerdas} cuerdas a la vez y {maxColgando} soldados colgando (se piden 3 y 3)");
                P6Ok(ref ok, velMax > 35f, sb, $"el helicoptero entra a {velMax:0} m/s (frenetico: > 35)");
                P6Ok(ref ok, alturaEnHover > 8f && alturaEnHover < 18f, sb, $"el helicoptero se estaciona a {alturaEnHover:0.0} m sobre el piso (cuerdas de ~{CinematicaDeRapel.AlturaDeRapel:0} m)");
                P6Ok(ref ok, temblor > 0.3f, sb, $"la camara se mueve/sacude en la toma 2 (recorrido {temblor:0.00} m)");
                P6Ok(ref ok, maxSuelo >= 3, sb, $"{maxSuelo} soldados tocaron el piso");

                // ---- B) al final: los 3 en el suelo en la zona de inicio, control del jugador (Kes), helicoptero fuera ----
                yield return null; yield return null;
                bool todosAbajo = true; float maxDesvio = 0f; int visibles = 0;
                foreach (var kv in inicio)
                {
                    var s = kv.Key; if (s == null) { todosAbajo = false; continue; }
                    if (Mathf.Abs(s.transform.position.y - 0.8f) > 0.1f) todosAbajo = false;
                    maxDesvio = Mathf.Max(maxDesvio, Vector3.Distance(s.transform.position, kv.Value));
                    bool vis = false; foreach (var r in s.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<ParticleSystem>() == null && r.GetComponent<TrailRenderer>() == null) { vis = true; break; }
                    if (vis) visibles++;
                    if (Vector3.Distance(new Vector3(s.transform.position.x, 0f, s.transform.position.z), new Vector3(zona.x, 0f, zona.z)) > 20f) todosAbajo = false;
                }
                P6Ok(ref ok, todosAbajo && visibles == inicio.Count && maxDesvio < 0.3f, sb, $"al final los {visibles}/{inicio.Count} soldados estan visibles en el suelo (y = 0,8 +- 0,1) en la zona de inicio (movieron {maxDesvio:0.00} m de su lugar)");
                var yo = drv.Brain.Current;
                P6Ok(ref ok, drv.enabled && (drv.Rig == null || drv.Rig.enabled) && !SP.Ai.AiBrain.IAPausada && yo != null && yo.Role == RoleType.Flanker, sb, $"el control es del jugador: driver activo={drv.enabled}, IA pausada={SP.Ai.AiBrain.IAPausada}, poseido={(yo != null ? yo.DisplayName + " (" + yo.Role + ")" : "nadie")}");
                P6Ok(ref ok, !heli.gameObject.activeInHierarchy && GameObject.Find("RapelCuerda_" + yo.name) == null && CinematicaDeRapel.YaVista, sb, "el helicoptero, las copias y las cuerdas se retiran y queda marcada como vista");
                foreach (var x in CapturarPantalla(P9Prefijo + "123_despues_control.png")) yield return x;

                // ---- C) saltar con [Espacio] 1 s ----
                var cin2 = CinematicaDeRapel.ReiniciarPorPrueba(false);
                yield return null; yield return null;
                foreach (var x in Esperar(0.5f)) yield return x;
                CinematicaDeRapel.PruebaMantenerEspacio = true;
                float tS = Time.realtimeSinceStartup;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - tS < 3f) yield return null;
                float tSalto = Time.realtimeSinceStartup - tS;
                CinematicaDeRapel.PruebaMantenerEspacio = false;
                yield return null;
                P6Ok(ref ok, !CinematicaDeRapel.Activa && CinematicaDeRapel.UltimaFueSaltada && tSalto >= 0.8f && tSalto <= 1.8f, sb, $"mantener [Espacio] la salta a los {tSalto:0.0} s (se espera ~1)");
                P6Ok(ref ok, drv.enabled && !SP.Ai.AiBrain.IAPausada && !heli.gameObject.activeInHierarchy && visibles == inicio.Count, sb, "tras saltar: control devuelto, helicoptero apagado, soldados visibles");

                // ---- D) encadena con la cinematica de los pasos ----
                CinematicaDeOperacion.YaVista = false;
                var cin3 = CinematicaDeRapel.ReiniciarPorPrueba(true);
                yield return null; yield return null;
                CinematicaDeRapel.PruebaMantenerEspacio = true;
                float tD = Time.realtimeSinceStartup;
                while (CinematicaDeRapel.Activa && Time.realtimeSinceStartup - tD < 3f) yield return null;
                CinematicaDeRapel.PruebaMantenerEspacio = false;
                yield return null; yield return null;
                P6Ok(ref ok, !CinematicaDeRapel.Activa && CinematicaDeOperacion.Activa, sb, $"al terminar el rapel arranca la cinematica de los pasos (activa={CinematicaDeOperacion.Activa})");
                CinematicaDeOperacion.Saltar();
                yield return null;
                P6Ok(ref ok, !CinematicaDeOperacion.Activa && drv.enabled, sb, "se la corta y el control vuelve");

                // ---- E) OperacionPrueba.Arrancar sigue saltando las cinematicas ----
                CinematicaDeOperacion.YaVista = false;
                CinematicaDeRapel.ReiniciarPorPrueba(true);
                yield return null; yield return null;
                foreach (var x in Esperar(0.4f)) yield return x;
                bool activaAntes = CinematicaDeRapel.Activa;
                OperacionPrueba.Arrancar(2);
                yield return null; yield return null;
                P6Ok(ref ok, activaAntes && !CinematicaDeRapel.Activa && !CinematicaDeOperacion.Activa && drv.enabled && CinematicaDeRapel.YaVista, sb, $"Arrancar(2) con el rapel en curso ({activaAntes}) lo corta sin encadenar nada: rapel activo={CinematicaDeRapel.Activa}, pasos activos={CinematicaDeOperacion.Activa}");
            }
            finally
            {
                CinematicaDeRapel.PruebaMantenerEspacio = false;
                CinematicaDeRapel.Saltar(); CinematicaDeOperacion.Saltar();
                CinematicaDeOperacion.YaVista = true;
            }
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
